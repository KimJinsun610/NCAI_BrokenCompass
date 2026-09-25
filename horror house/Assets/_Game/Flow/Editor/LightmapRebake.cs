using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 라이트맵 재베이크를 <b>한 번에, 중간에 끊겨도 씬이 이상해지지 않게</b> 돌립니다.
/// 메뉴 「NightDuty/아트/라이트맵 재베이크 (창 광원 켜고 굽고 되돌림)」.
///
/// <para><b>왜 그냥 Generate Lighting을 누르면 안 되는가.</b> 이 씬의 창문 채광은
/// <c>WindowLights_&lt;방이름&gt;</c> 그룹의 Baked 면광원이 맡는데, <b>평소에는 전부 꺼져 있습니다</b>
/// (런타임 비용이 없게 하려는 벤더 관례입니다). 그 상태로 구우면 <b>모든 방이 창 빛을 잃습니다</b> —
/// 지금 남아 있는 라이트맵보다 훨씬 어두워집니다(2026-09-22 실측: 꺼진 면광원 30여 개).</para>
///
/// <para>그래서 이 명령은 ⑴ 꺼진 Baked/Mixed 창 광원을 전부 켜고 ⑵ 굽고 ⑶ 끝나면 원래대로 되돌립니다.
/// 되돌리기는 <see cref="Lightmapping.bakeCompleted"/>와 취소 양쪽에 걸려 있습니다.</para>
///
/// <para><b>오래 걸립니다.</b> ContributeGI 렌더러가 4천 개 넘고 아틀라스가 2048 다섯 장입니다.
/// 굽는 동안 에디터를 만지지 마십시오. 중간에 멈추려면 Lighting 창의 Cancel입니다 —
/// 그때도 창 광원은 되돌아갑니다.</para>
/// </summary>
public static class LightmapRebake
{
    private static readonly List<GameObject> s_turnedOn = new List<GameObject>();
    private static bool s_running;

    [MenuItem("NightDuty/아트/창 광원이 몇 개나 꺼져 있는지 보기")]
    public static void ReportWindowLights()
    {
        List<Light> all = CollectWindowLights();
        int off = 0;

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        for (int i = 0; i < all.Count; i++)
        {
            if (!all[i].gameObject.activeInHierarchy)
            {
                off++;
            }
        }

        sb.AppendLine("[아트] WindowLights_* 아래 Baked/Mixed 광원 " + all.Count + "개 중 꺼진 것 " + off + "개");
        sb.AppendLine("구우려면 이것들이 켜져 있어야 합니다. 「라이트맵 재베이크」 명령이 알아서 켰다 끕니다.");
        Debug.Log(sb.ToString());
    }

    [MenuItem("NightDuty/아트/라이트맵 재베이크 (창 광원 켜고 굽고 되돌림)")]
    public static void Rebake()
    {
        if (s_running || Lightmapping.isRunning)
        {
            Debug.LogWarning("[아트] 이미 굽는 중입니다.");
            return;
        }

        if (!EditorUtility.DisplayDialog("라이트맵 재베이크",
                "창 광원을 켜고 씬 전체를 다시 굽습니다. 끝나면 창 광원을 원래대로 되돌립니다.\n\n" +
                "오래 걸립니다. 씬이 바뀝니다. LFS 잠금을 확인하셨습니까?", "굽는다", "그만둔다"))
        {
            return;
        }

        Begin();
    }

    /// <summary>대화상자 없이 시작한다(자동화용).</summary>
    public static void Begin()
    {
        if (s_running || Lightmapping.isRunning)
        {
            Debug.LogWarning("[아트] 이미 굽는 중입니다.");
            return;
        }

        s_turnedOn.Clear();

        List<Light> all = CollectWindowLights();
        for (int i = 0; i < all.Count; i++)
        {
            GameObject go = all[i].gameObject;
            if (go.activeSelf)
            {
                continue;
            }

            s_turnedOn.Add(go);
            go.SetActive(true);
        }

        s_running = true;
        Lightmapping.bakeCompleted -= OnDone;
        Lightmapping.bakeCompleted += OnDone;
        EditorApplication.update -= Watch;
        EditorApplication.update += Watch;

        Debug.Log("[아트] 창 광원 " + s_turnedOn.Count + "개를 켰습니다. 굽기 시작합니다.");

        if (!Lightmapping.BakeAsync())
        {
            Debug.LogError("[아트] 굽기를 시작하지 못했습니다.");
            Restore();
        }
    }

    /// <summary>취소되어도 되돌려 놓는다. <c>bakeCompleted</c>는 취소 때 안 오는 판본이 있다.</summary>
    private static void Watch()
    {
        if (!s_running || Lightmapping.isRunning)
        {
            return;
        }

        Restore();
    }

    private static void OnDone()
    {
        Restore();
        Debug.Log("[아트] 굽기가 끝났습니다. 씬을 저장하십시오(Ctrl+S).");
    }

    private static void Restore()
    {
        if (!s_running)
        {
            return;
        }

        s_running = false;
        Lightmapping.bakeCompleted -= OnDone;
        EditorApplication.update -= Watch;

        int n = 0;
        for (int i = 0; i < s_turnedOn.Count; i++)
        {
            if (s_turnedOn[i] != null)
            {
                s_turnedOn[i].SetActive(false);
                n++;
            }
        }

        s_turnedOn.Clear();
        Debug.Log("[아트] 창 광원 " + n + "개를 원래대로(꺼짐) 되돌렸습니다.");
    }

    /// <summary><c>WindowLights_</c>로 시작하는 그룹 아래의 Baked/Mixed 광원.</summary>
    private static List<Light> CollectWindowLights()
    {
        List<Light> found = new List<Light>();
        Light[] all = Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (int i = 0; i < all.Length; i++)
        {
            if (all[i].lightmapBakeType == LightmapBakeType.Realtime)
            {
                continue;
            }

            Transform t = all[i].transform;
            bool under = false;

            while (t != null && !under)
            {
                if (t.name.StartsWith("WindowLights_", System.StringComparison.Ordinal))
                {
                    under = true;
                }

                t = t.parent;
            }

            if (under)
            {
                found.Add(all[i]);
            }
        }

        return found;
    }
}

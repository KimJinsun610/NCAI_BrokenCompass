using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 변기 물(61차, 2026-10-07 플레이테스트: 「변기 물 적용 안 되어 있음 — 진선님 에셋 있음」). 근무 씬에 자동으로 선다.
/// <list type="bullet">
/// <item>화장실의 변기(ToiletA·ToiletC)마다 김진선님 맑은 물(<c>prop.toilet.water</c> ← <c>HorrorProp_ToiletA_ClearWater</c>)을 겹친다 —
/// 그 프리팹 안의 변기 몸통은 꺼서 씬 변기와 겹치지 않게 하고 물만 보인다. ToiletC는 몸이 조금 낮아 물을 <see cref="ToiletCDrop"/>m 내린다.</item>
/// <item>T-1 이상(<c>InspectionAnomalies</c>)은 <see cref="Blood"/> — 그 변기의 맑은 물을 감추고 핏물·머리카락 변기(<c>prop.toilet.bloodhair</c>)를 겹친다.
/// 가까이(0.8m) 가면 핏물이 솟는다(김진선님 <c>ToiletBloodGush</c>). 이상이 걷히면 <see cref="Restore"/>.</item>
/// </list>
/// 씬 파일은 고치지 않는다. <b>판정과 무관하다.</b>
/// </summary>
[DisallowMultipleComponent]
public sealed class ToiletBowls : MonoBehaviour
{
    /// <summary>맑은 물 대역 ID.</summary>
    public const string WaterId = "prop.toilet.water";

    /// <summary>핏물·머리카락 대역 ID.</summary>
    public const string BloodId = "prop.toilet.bloodhair";

    /// <summary>ToiletC는 ToiletA보다 몸이 낮다(0.83 : 0.91m) — 물을 이만큼 내린다.</summary>
    public const float ToiletCDrop = 0.022f;

    /// <summary>화장실 상자(엠비언트 기본값과 같은 실측, 바닥 y=1.5).</summary>
    public static readonly Bounds ToiletBox = new Bounds(new Vector3(1.45f, 3.4f, 35.65f), new Vector3(9.1f, 4f, 8.7f));

    private static ToiletBowls s_active;
    private readonly Dictionary<Transform, GameObject> _water = new Dictionary<Transform, GameObject>();

    /// <summary>지금 근무 씬의 것. 없으면 null.</summary>
    public static ToiletBowls Active
    {
        get { return s_active; }
    }

    /// <summary>물을 얹은 변기 수(검수용).</summary>
    public int Count
    {
        get { return _water.Count; }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Install()
    {
        s_active = null;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InstallForFirstScene()
    {
        EnsureFor(SceneManager.GetActiveScene());
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureFor(scene);
    }

    private static void EnsureFor(Scene scene)
    {
        if (!FlowAutoInstall.IsDutyScene(scene) || FlowAutoInstall.Exists<ToiletBowls>(scene)) return;
        FlowAutoInstall.CreateHost<ToiletBowls>(scene, "ToiletBowls (auto)");
    }

    private void OnEnable()
    {
        s_active = this;
    }

    private void OnDisable()
    {
        if (s_active == this) s_active = null;
    }

    private void Awake()
    {
        GameObject prefab = Resources.Load<GameObject>("StandIns/" + WaterId);
        if (prefab == null)
        {
            Debug.LogWarning("[ToiletBowls] Resources/StandIns/" + WaterId + "가 없습니다(StandInPrefabBuilder로 만든다).");
            return;
        }

        foreach (LODGroup lod in FindObjectsByType<LODGroup>(FindObjectsSortMode.None))
        {
            Transform t = lod.transform;
            bool a = t.name.StartsWith("ToiletA");
            bool c = t.name.StartsWith("ToiletC");
            if ((!a && !c) || !ToiletBox.Contains(t.position + Vector3.up * 0.5f) || _water.ContainsKey(t)) continue;
            GameObject w = Overlay(prefab, t, "변기 물 · " + t.name);
            if (c) w.transform.position += Vector3.down * ToiletCDrop;
            _water[t] = w;
        }
    }

    /// <summary>그 변기 자리·방향에 김진선님 변기 프리팹을 겹치고 안쪽 변기 몸통은 끈다.</summary>
    private GameObject Overlay(GameObject prefab, Transform toilet, string name)
    {
        GameObject go = Instantiate(prefab, toilet.position, toilet.rotation, transform);
        go.name = name;
        foreach (Transform child in go.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == "ToiletA" && child.GetComponent<LODGroup>() != null) child.gameObject.SetActive(false);
        }

        Transform aim = go.transform.Find("Aim");
        if (aim != null) Destroy(aim.gameObject);
        return go;
    }

    /// <summary>
    /// 그 변기(또는 그 아래 자식의 변기)에 핏물·머리카락을 겹친다 — 맑은 물은 감춘다. 만든 것을 돌려준다(지울 때 <see cref="Restore"/>). 실패하면 null.
    /// </summary>
    public static GameObject Blood(Transform toilet)
    {
        ToiletBowls me = s_active;
        if (me == null || toilet == null) return null;
        GameObject prefab = Resources.Load<GameObject>("StandIns/" + BloodId);
        if (prefab == null) return null;
        Transform root = Root(toilet);
        GameObject water;
        if (me._water.TryGetValue(root, out water) && water != null) water.SetActive(false);
        GameObject blood = me.Overlay(prefab, root, "변기 핏물·머리카락 · " + root.name);
        if (root.name.StartsWith("ToiletC")) blood.transform.position += Vector3.down * ToiletCDrop;
        return blood;
    }

    /// <summary>맑은 물을 다시 보인다(핏물 대역은 부르는 쪽이 지운다).</summary>
    public static void Restore(Transform toilet)
    {
        ToiletBowls me = s_active;
        if (me == null || toilet == null) return;
        GameObject water;
        if (me._water.TryGetValue(Root(toilet), out water) && water != null) water.SetActive(true);
    }

    /// <summary>점검 기준점의 부모 등 — LODGroup이 달린 변기 뿌리.</summary>
    private static Transform Root(Transform t)
    {
        for (Transform p = t; p != null; p = p.parent)
        {
            if (p.GetComponent<LODGroup>() != null && (p.name.StartsWith("ToiletA") || p.name.StartsWith("ToiletC"))) return p;
        }

        return t;
    }
}

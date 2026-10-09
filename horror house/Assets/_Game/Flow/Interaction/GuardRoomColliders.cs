using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 경비실 충돌 보강(61차, 2026-10-07 플레이테스트: 「경비실에 충돌 박스가 없는 몇몇 물체에 추가」).
/// 씬 파일은 고치지 않는다 — 근무 씬이 열릴 때 경비실 상자 안의 <b>바닥에 선 소품</b>을 훑어
/// <list type="bullet">
/// <item>막는 콜라이더가 아예 없거나(화분),</item>
/// <item>있어도 보이는 몸의 아래쪽 절반도 못 덮거나(경비실 사물함 — 피벗에 놓인 기본 1×1×1 상자라 바닥 아래로 반이 묻혀 있었다),</item>
/// </list>
/// 이면 보이는 크기에 맞춘 상자 콜라이더를 덧붙인다. 피벗의 기본 상자(크기 1·중심 0, 메시 없는 부모)는 끈다.
/// 문(DoorScript)이 달린 것은 조준을 가리지 않게 건드리지 않는다.
/// </summary>
[DisallowMultipleComponent]
public sealed class GuardRoomColliders : MonoBehaviour
{
    /// <summary>경비실 상자(엠비언트 기본값과 같은 실측, 바닥 y=1.5).</summary>
    public static readonly Bounds Room = new Bounds(new Vector3(34.05f, 3.4f, 46.15f), new Vector3(7.5f, 4f, 3.9f));

    private const float Floor = 1.5f;

    /// <summary>덧붙인 상자 수(검수용).</summary>
    public int Added { get; private set; }

    /// <summary>덧붙인 소품 이름(검수용).</summary>
    public readonly List<string> AddedNames = new List<string>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Install()
    {
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
        if (!FlowAutoInstall.IsDutyScene(scene) || FlowAutoInstall.Exists<GuardRoomColliders>(scene)) return;
        FlowAutoInstall.CreateHost<GuardRoomColliders>(scene, "GuardRoomColliders (auto)");
    }

    private void Start()
    {
        Dictionary<Transform, Bounds> props = new Dictionary<Transform, Bounds>();
        foreach (MeshRenderer r in FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (r.gameObject.scene != gameObject.scene || !Room.Contains(r.bounds.center)) continue;
            Transform root = PropRoot(r.transform);
            Bounds b;
            if (props.TryGetValue(root, out b))
            {
                b.Encapsulate(r.bounds);
                props[root] = b;
            }
            else
            {
                props[root] = r.bounds;
            }
        }

        foreach (KeyValuePair<Transform, Bounds> kv in props)
        {
            Fix(kv.Key, kv.Value);
        }

        if (Added > 0) Debug.Log("[GuardRoomColliders] 경비실 충돌 상자 " + Added + "개 덧붙임 — " + string.Join(", ", AddedNames));
    }

    /// <summary>경비실 안에 머무는 가장 높은 조상(렌더러 40개 미만) — 소품 하나의 뿌리.</summary>
    private static Transform PropRoot(Transform t)
    {
        while (t.parent != null && Room.Contains(t.parent.position) && t.parent.GetComponentsInChildren<Renderer>().Length < 40)
        {
            t = t.parent;
        }

        return t;
    }

    private void Fix(Transform root, Bounds look)
    {
        string n = root.name;
        if (n.StartsWith("Wall") || n.StartsWith("Floor") || n.StartsWith("Ceiling") || n.StartsWith("Door") || n.StartsWith("Window")) return;
        if (look.min.y > Floor + 0.4f || look.size.y < 0.3f || Mathf.Max(look.size.x, look.size.z) < 0.25f) return;   // 바닥에 선 것만
        foreach (MonoBehaviour m in root.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (m != null && m.GetType().Name == "DoorScript") return;   // 여닫는 것은 조준을 가리지 않게 둔다
        }

        // 피벗의 기본 상자(메시 없는 부모의 1×1×1)는 자리만 차지하고 몸을 못 덮는다 — 끈다.
        foreach (BoxCollider box in root.GetComponents<BoxCollider>())
        {
            if (root.GetComponent<MeshFilter>() == null && box.size == Vector3.one && box.center == Vector3.zero) box.enabled = false;
        }

        float wantTop = Mathf.Min(look.max.y, Floor + 1.8f);
        float covered = 0f;
        foreach (Collider c in root.GetComponentsInChildren<Collider>())
        {
            if (!c.enabled || c.isTrigger) continue;
            float lo = Mathf.Max(c.bounds.min.y, Floor);
            float hi = Mathf.Min(c.bounds.max.y, wantTop);
            covered = Mathf.Max(covered, hi - lo);
        }

        if (covered >= (wantTop - Floor) * 0.7f) return;

        GameObject go = new GameObject("guard room collider · " + n);
        go.transform.SetParent(transform, false);
        go.transform.position = look.center;
        BoxCollider added = go.AddComponent<BoxCollider>();
        added.size = new Vector3(Mathf.Max(0.05f, look.size.x - 0.02f), look.size.y, Mathf.Max(0.05f, look.size.z - 0.02f));
        Added++;
        AddedNames.Add(n);
    }
}

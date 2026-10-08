using System.Collections.Generic;
using NightDuty;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 66차(민: 「점검은 안 겹칠수록 좋아 — 점검 항목을 추가해도 돼」): 씬에 아직 놓지 않은 점검 항목의 대상(<c>inspect.&lt;ID&gt;</c>)을 근무 중에만 세운다. 씬은 고치지 않는다.
/// <list type="bullet">
/// <item>소품 하나: 소품 아래 자식 <c>Inspect &lt;ID&gt;</c> — 상자 콜라이더(소품 LOD0 + 4cm) + <see cref="JudgeTarget"/>. 점검 대상 배치 메뉴(<c>InspectionTargetPlacer</c>)와 같은 모양.</item>
/// <item>소품 여럿(세면대 넷 · 의자 둘 · 플라스크 두 묶음): 씬 뿌리에 묶음 <c>Inspect group &lt;ID&gt;</c>(<see cref="OutlineGroup"/> — 외곽선은 묶인 소품들) 아래 같은 자식. 소품은 옮기지도 부모를 바꾸지도 않는다.</item>
/// <item>씬에 이미 그 ID의 대상이 있으면(배치 메뉴로 놓았으면) 손대지 않는다.</item>
/// </list>
/// 이상 연출(<see cref="InspectionAnomalies"/>)은 <see cref="Prop"/>으로 소품을 찾는다.
/// </summary>
[DisallowMultipleComponent]
public sealed class RuntimeInspectTargets : MonoBehaviour
{
    private const float Inflate = 0.04f;

    /// <summary>항목 ID → 씬 경로(첫 번째가 대표 소품). 소품을 옮기거나 이름을 바꾸면 여기를 고친다.</summary>
    public static readonly KeyValuePair<string, string[]>[] Paths =
    {
        P("H-5", "Interior/Corridors/Speaker (2)"),
        P("H-6", "Interior/Corridors/Bench (6)"),
        P("C-4", "Interior/Classroom02/ClockC (2)"),
        P("C-5", "Interior/Classroom02/TeacherChair (3)"),
        P("S-4", "Interior/science classroom/Globe (2)"),
        P("S-5", "Interior/science classroom/Aged Erlenmeyer Flask (2)", "Interior/science classroom/Aged Erlenmeyer Flask (4)"),
        P("S-6", "Interior/science classroom/Map"),
        P("T-4", "Interior/Toilet02/Toilet_Sink (6)", "Interior/Toilet02/Toilet_Sink (7)", "Interior/Toilet02/Toilet_Sink (8)", "Interior/Toilet02/Toilet_Sink (9)"),
        P("T-5", "Interior/Toilet02/TowelB (2)"),
        P("L-4", "Interior/Classroom01/Blackboard (1)"),
        P("L-5", "Interior/Classroom01/TrashCanSmall (1)"),
        P("K-2", "Interior/janitor's room/Distressed Metal Folding Chair", "Interior/janitor's room/Distressed Metal Folding Chair (2)"),
        P("K-3", "Interior/janitor's room/Plant01 (1)")
    };

    /// <summary>S-6 지도와 같은 자리에 겹쳐 놓인 두 번째 지도(이상이면 숨긴다).</summary>
    public const string MapTwinPath = "Interior/science classroom/Map (1)";

    private readonly List<GameObject> _created = new List<GameObject>();

    /// <summary>지금 근무 씬의 것. 없으면 null.</summary>
    public static RuntimeInspectTargets Active { get; private set; }

    /// <summary>세운 대상 수(시험·검수).</summary>
    public int CreatedCount
    {
        get { return _created.Count; }
    }

    private static KeyValuePair<string, string[]> P(string id, params string[] paths)
    {
        return new KeyValuePair<string, string[]>(id, paths);
    }

    /// <summary>그 항목의 씬 경로들. 없으면 빈 배열.</summary>
    public static string[] PathsOf(string itemId)
    {
        for (int i = 0; i < Paths.Length; i++)
        {
            if (Paths[i].Key == itemId) return Paths[i].Value;
        }

        return new string[0];
    }

    /// <summary>그 항목의 <paramref name="index"/>번째 소품(씬 경로로). 없으면 null.</summary>
    public static Transform Prop(string itemId, int index)
    {
        string[] paths = PathsOf(itemId);
        if (index < 0 || index >= paths.Length) return null;
        GameObject go = GameObject.Find("/" + paths[index]);
        return go != null ? go.transform : null;
    }

    /// <summary>그 항목의 소품 수.</summary>
    public static int PropCount(string itemId)
    {
        return PathsOf(itemId).Length;
    }

    // ── 자동 설치 ─────────────────────────────────────────────

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Install()
    {
        Active = null;
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
        if (!FlowAutoInstall.IsDutyScene(scene) || FlowAutoInstall.Exists<RuntimeInspectTargets>(scene)) return;
        FlowAutoInstall.CreateHost<RuntimeInspectTargets>(scene, "RuntimeInspectTargets (auto)");
    }

    private void OnEnable()
    {
        Active = this;
        for (int i = 0; i < Paths.Length; i++) Ensure(Paths[i].Key, Paths[i].Value);
    }

    private void OnDisable()
    {
        for (int i = 0; i < _created.Count; i++)
        {
            if (_created[i] != null) Destroy(_created[i]);
        }

        _created.Clear();
        if (Active == this) Active = null;
    }

    // ── 대상 ─────────────────────────────────────────────────

    private void Ensure(string itemId, string[] paths)
    {
        string id = InspectionCatalog.TargetPrefix + itemId;
        JudgeTarget existing;
        if (JudgeTargetRegistry.TryGet(id, out existing) && existing != null) return;
        if (InspectionCatalog.Find(itemId) == null) return;

        List<Transform> props = new List<Transform>();
        for (int i = 0; i < paths.Length; i++)
        {
            GameObject go = GameObject.Find("/" + paths[i]);
            if (go == null)
            {
                Debug.LogWarning("[RuntimeInspectTargets] 씬에 " + itemId + " 소품이 없습니다: " + paths[i]);
                continue;
            }

            props.Add(go.transform);
        }

        if (props.Count == 0) return;

        Bounds b;
        if (!Lod0Bounds(props, out b)) return;

        Transform parent = props[0];
        if (props.Count > 1)
        {
            GameObject holder = new GameObject("Inspect group " + itemId);
            SceneManager.MoveGameObjectToScene(holder, gameObject.scene);
            holder.layer = props[0].gameObject.layer;
            holder.transform.position = b.center;
            OutlineGroup group = holder.AddComponent<OutlineGroup>();
            for (int i = 0; i < props.Count; i++) group.AddProp(props[i]);
            parent = holder.transform;
            _created.Add(holder);
        }

        GameObject child = new GameObject("Inspect " + itemId);
        child.layer = parent.gameObject.layer;
        child.transform.SetParent(parent, false);
        child.transform.SetPositionAndRotation(b.center, Quaternion.identity);
        BoxCollider box = child.AddComponent<BoxCollider>();
        Vector3 s = child.transform.lossyScale;
        Vector3 size = b.size + Vector3.one * Inflate;
        box.size = new Vector3(size.x / Mathf.Max(0.0001f, Mathf.Abs(s.x)), size.y / Mathf.Max(0.0001f, Mathf.Abs(s.y)), size.z / Mathf.Max(0.0001f, Mathf.Abs(s.z)));
        JudgeTarget t = child.AddComponent<JudgeTarget>();
        t.SetIds(id);
        if (props.Count == 1) _created.Add(child);
    }

    /// <summary>소품들의 LOD0 렌더러를 감싸는 월드 상자(점검 대상 자식은 뺀다).</summary>
    public static bool Lod0Bounds(List<Transform> props, out Bounds bounds)
    {
        bounds = new Bounds();
        bool any = false;
        for (int p = 0; p < props.Count; p++)
        {
            Transform prop = props[p];
            HashSet<Renderer> skip = new HashSet<Renderer>();
            foreach (LODGroup lod in prop.GetComponentsInChildren<LODGroup>(true))
            {
                LOD[] lods = lod.GetLODs();
                for (int i = 1; i < lods.Length; i++)
                {
                    foreach (Renderer r in lods[i].renderers)
                    {
                        if (r != null) skip.Add(r);
                    }
                }
            }

            foreach (Renderer r in prop.GetComponentsInChildren<Renderer>())
            {
                if (r == null || skip.Contains(r) || r.name.StartsWith("Inspect ")) continue;
                if (r is ParticleSystemRenderer) continue;
                if (!any)
                {
                    bounds = r.bounds;
                    any = true;
                }
                else
                {
                    bounds.Encapsulate(r.bounds);
                }
            }
        }

        return any;
    }
}

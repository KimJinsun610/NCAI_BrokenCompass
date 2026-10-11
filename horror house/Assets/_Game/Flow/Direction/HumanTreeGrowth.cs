using NightDuty;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 인체 나무(김진선님 <c>HorrorCreature_EggmanTree</c>를 감싼 대역 <c>Resources/StandIns/mob.humantree</c>) — 천장에 매달려 자라는 나무.
/// <list type="bullet">
/// <item>91차 B(날마다 자람) → 94차(민: 「에그맨 트리가 화분에 나오는 건 폐기 · 경비실 옆 정문 앞 부분에, 천장에 매달려서 점점 자라나게 ·
/// 배치 축에 따라서 성장하는 느낌」): 화분 묘목은 없앴다. 정문(<c>DoorMain</c>) 안쪽 현관, 경비실 쪽 천장(<see cref="CeilingSpot"/>)에 거꾸로 매달려
/// 그 밤 <b>배치 축</b> 값만큼 아래로 자란다 — 0이면 <see cref="MinLength"/>m 봉오리, 100이면 <see cref="MaxLength"/>m(바닥 가까이).
/// 축이 오르면 천천히(<see cref="GrowSpeed"/> m/초) 자라고, 재시작으로 축이 내려가면 그만큼 움츠러든다.</item>
/// <item>밤마다 선다(근무 밤이면). 그 밤 편성에 사람 나무 조우(<see cref="ProgramCatalog.PeopleTree"/>)가 있으면 그쪽이 나오므로 세우지 않는다.</item>
/// <item>콜라이더는 끈다(길을 막지 않게). 김진선님 「바라보면 화면 물듦」은 그대로. 손전등 없이도 윤곽만 보일 만큼 아주 약하게 스스로 빛난다.</item>
/// </list>
/// 판정·축과 무관한 무대 장치(축을 읽기만 한다). 씬·김진선님 프리팹은 고치지 않는다. 근무 씬에 자동으로 선다.
/// </summary>
[DisallowMultipleComponent]
public sealed class HumanTreeGrowth : MonoBehaviour
{
    /// <summary>대역 경로(Resources).</summary>
    public const string PrefabPath = "StandIns/mob.humantree";

    /// <summary>매다는 천장 자리(수평 위치, PlayScene 실측) — 정문 안쪽 현관의 경비실 쪽(현관 x 25.3~29.8, 정문 z 49).</summary>
    public static readonly Vector3 CeilingSpot = new Vector3(28.2f, 2.5f, 47.0f);

    /// <summary>나무가 바라보는 쪽(현관에서 중앙 복도 쪽 — 복도에서 현관으로 들어서는 사람을 본다).</summary>
    public static readonly Vector3 Facing = new Vector3(28.2f, 2.5f, 40f);

    /// <summary>배치 축 0일 때 길이(m) — 천장의 봉오리.</summary>
    public const float MinLength = 0.5f;

    /// <summary>배치 축 100일 때 길이(m) — 바닥(천장 4m) 가까이까지.</summary>
    public const float MaxLength = 3.5f;

    /// <summary>자라는 빠르기(m/초) — 눈에 띄지 않게 천천히.</summary>
    public const float GrowSpeed = 0.06f;

    /// <summary>움츠러드는 빠르기(m/초) — 재시작으로 축이 내려갔을 때.</summary>
    public const float ShrinkSpeed = 0.5f;

    /// <summary>스스로 빛나는 세기 — 어둠 속 윤곽만.</summary>
    public const float Glow = 0.09f;

    /// <summary>배치 축 값(0~100)에 맞는 길이(m).</summary>
    public static float LengthFor(int layout)
    {
        return Mathf.Lerp(MinLength, MaxLength, Mathf.Clamp01(layout / 100f));
    }

    /// <summary>지금 근무 씬의 것. 없으면 null.</summary>
    public static HumanTreeGrowth Active { get; private set; }

    /// <summary>매단 나무의 뿌리(천장 점, 비례로 키운다). 없으면 null(검수용).</summary>
    public GameObject Tree
    {
        get { return _pivot; }
    }

    /// <summary>지금 길이(m, 검수용).</summary>
    public float Length
    {
        get { return _length; }
    }

    private GameObject _pivot;
    private float _unitHeight = 1f;
    private float _length;
    private InspectionPlan _plan;

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
        if (!FlowAutoInstall.IsDutyScene(scene) || FlowAutoInstall.Exists<HumanTreeGrowth>(scene)) return;
        FlowAutoInstall.CreateHost<HumanTreeGrowth>(scene, "HumanTreeGrowth (auto)");
    }

    private void OnEnable()
    {
        Active = this;
    }

    private void OnDisable()
    {
        Clear();
        if (Active == this) Active = null;
    }

    private void Update()
    {
        if (!NightRun.IsNightActive || NightRun.Axes == null)
        {
            return;
        }

        InspectionPlan plan = NightRun.Inspections != null ? NightRun.Inspections.Plan : null;
        if (!ReferenceEquals(plan, _plan))
        {
            _plan = plan;
            Rebuild();
        }

        if (_pivot == null) return;
        float want = LengthFor(NightRun.Axes.GetValue(FearAxis.Layout));
        float rate = want >= _length ? GrowSpeed : ShrinkSpeed;
        _length = Mathf.MoveTowards(_length, want, rate * Time.deltaTime);
        ApplyLength();
    }

    /// <summary>그 밤에 맞게 다시 세운다(디버그에서도 부른다).</summary>
    public void Rebuild()
    {
        Clear();
        if (PeopleTreeTonight()) return;
        _pivot = Hang();
        if (_pivot == null) return;
        _length = NightRun.Axes != null ? LengthFor(NightRun.Axes.GetValue(FearAxis.Layout)) : MinLength;
        ApplyLength();
        if (DirectionStage.Verbose) Debug.Log("[HumanTreeGrowth] " + NightRun.Day + "일차 — 현관 천장에 매닮 " + _length.ToString("0.00") + "m (배치 축에 따라 자람)");
    }

    private void ApplyLength()
    {
        if (_pivot == null) return;
        float k = Mathf.Max(0.01f, _length / Mathf.Max(0.01f, _unitHeight));
        _pivot.transform.localScale = new Vector3(k, k, k);
    }

    private static bool PeopleTreeTonight()
    {
        if (NightRun.Tension == null) return false;
        foreach (EncounterRun r in NightRun.Tension.Runs)
        {
            if (r != null && r.Def != null && r.Def.Id == ProgramCatalog.PeopleTree) return true;
        }

        return false;
    }

    private void Clear()
    {
        if (_pivot != null) Destroy(_pivot);
        _pivot = null;
    }

    /// <summary>천장 점에 뿌리를 두고 거꾸로 매단다 — 뿌리(천장 점)를 축으로 키우면 아래로만 자란다.</summary>
    private GameObject Hang()
    {
        GameObject prefab = Resources.Load<GameObject>(PrefabPath);
        if (prefab == null)
        {
            Debug.LogWarning("[HumanTreeGrowth] 대역이 없습니다: Resources/" + PrefabPath);
            return null;
        }

        Vector3 ceiling = CeilingSpot;
        RaycastHit hit;
        if (Physics.Raycast(CeilingSpot, Vector3.up, out hit, 6f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) ceiling = hit.point;
        else ceiling.y = CeilingSpot.y + 3f;

        GameObject pivot = new GameObject("인체 나무(현관 천장)");
        pivot.transform.position = ceiling;
        GameObject tree = Instantiate(prefab, pivot.transform);
        tree.transform.localPosition = Vector3.zero;
        tree.transform.localRotation = Quaternion.identity;
        foreach (Collider c in tree.GetComponentsInChildren<Collider>(true)) c.enabled = false;

        // 바로 선 채로 뿌리(묶음 상자 아랫면 가운데)를 뿌리 점에 맞춘 뒤 뒤집는다.
        Bounds b;
        if (RenderBounds(tree, out b))
        {
            _unitHeight = Mathf.Max(0.05f, b.size.y);
            tree.transform.position += new Vector3(ceiling.x - b.center.x, ceiling.y - b.min.y, ceiling.z - b.center.z);
        }

        Vector3 face = Facing - ceiling;
        face.y = 0f;
        Quaternion yaw = face.sqrMagnitude > 0.01f ? Quaternion.LookRotation(face.normalized, Vector3.up) : Quaternion.identity;
        // 뒤집기 — 앞(z)축으로 180°: 위가 아래로, 바라보는 쪽은 그대로.
        pivot.transform.rotation = yaw * Quaternion.Euler(0f, 0f, 180f);
        DoorDuck.SelfLight(tree, Glow);
        return pivot;
    }

    private static bool RenderBounds(GameObject go, out Bounds b)
    {
        b = new Bounds();
        bool any = false;
        foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
        {
            if (!r.enabled) continue;
            if (!any)
            {
                b = r.bounds;
                any = true;
            }
            else b.Encapsulate(r.bounds);
        }

        return any;
    }
}

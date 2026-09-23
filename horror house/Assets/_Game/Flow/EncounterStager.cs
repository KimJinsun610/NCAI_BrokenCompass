using System.Collections.Generic;
using NightDuty;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 조우 연출기(<see cref="EncounterDirector"/>)가 씬에 손을 대는 통로. <b>연출기는 위치도 시야도 모른다</b>
/// — 그 두 가지를 씬 쪽에서 채워 주는 것이 이 컴포넌트의 전부다.
///
/// <list type="bullet">
/// <item><b><see cref="EncounterDirector.IsVisible"/></b> — 그 장면의 모형이 <b>지금 보이는가</b>.
/// 프러스텀(시야 안)과 라인캐스트(가림 없음)를 둘 다 만족해야 true다.</item>
/// <item><b><see cref="EncounterDirector.PlaceModel"/></b> — 모형을 그 접근 지점으로 옮긴다.</item>
/// </list>
///
/// <para>
/// <b>이것이 기획서 제1 금기를 막는 자리다.</b> 「플레이어가 보는 앞에서 모형이 순간이동하지 않는다」는
/// 연출기가 <see cref="EncounterDirector.IsVisible"/>에 물어보고 지키는 규칙인데, 그 물음에 답하는 코드가
/// 여기밖에 없다. 이 컴포넌트가 없으면 연출기는 경고만 찍고 <b>조우를 아예 진행하지 않는다</b>
/// (EncounterDirector.IsVisibleSafe). 그래서 시험용 하네스가 아니라 제품 코드에 있어야 한다.
/// </para>
///
/// <para>
/// <b>모형 프리팹이 아직 없다.</b> <see cref="modelPrefab"/>이 비어 있으면 눈에 보이는 대역을 만들어 세운다.
/// 진짜 인체모형이 오면 프리팹만 꽂으면 되고 이 파일은 그대로 둔다.
/// </para>
///
/// <para>
/// 씬에 직접 놓지 않아도 된다 — <see cref="NightRunDriver"/>와 같은 규칙으로 근무 씬에 자동으로 선다.
/// </para>
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-600)]
public sealed class EncounterStager : MonoBehaviour
{
    /// <summary>표식은 점이라 시야 판정에서 사람 크기만큼 부풀려 본다.</summary>
    private static readonly Vector3 ModelBox = new Vector3(0.6f, 1.8f, 0.6f);

    [Tooltip("조우에 쓸 모형 프리팹. 애니메이션이 붙은 리깅 모델이 오면 여기에 꽂는다.\n비워 두면 눈에 보이는 임시 대역을 세운다.")]
    [SerializeField] private GameObject modelPrefab;

    [Tooltip("비워 두면 Camera.main을 쓴다.")]
    [SerializeField] private Camera playerCamera;

    [Tooltip("모형이 표식보다 얼마나 위에 설지(m). 표식이 바닥에 있으면 올려 준다.")]
    [SerializeField] private float modelLift;

    [Tooltip("이 장면의 모형으로 쓸 씬 오브젝트. 비워 두면 대역을 만든다.\n실물이 이미 씬에 있으면(과학실 인체모형 등) 여기에 꽂아 그것을 움직인다.")]
    [SerializeField] private SceneModel[] sceneModels;

    [Tooltip("모형을 이만큼 오래 바라보면 「관찰했다」로 본다(초).")]
    [SerializeField] private float observeSeconds = 0.35f;

    [Tooltip("화면 중앙에서 이 각도 안에 들어와야 바라본 것으로 친다(도).")]
    [SerializeField] private float observeAngle = 14f;

    /// <summary>장면 하나와 그 장면이 쓸 씬 모형.</summary>
    [System.Serializable]
    public struct SceneModel
    {
        [Tooltip("조우 장면 ID(scene.sa 등).")] public string SceneId;
        [Tooltip("그 장면이 움직일 씬 오브젝트.")] public Transform Model;
    }

    private readonly Dictionary<string, GameObject> _models = new Dictionary<string, GameObject>();
    private readonly Dictionary<string, string> _placedAt = new Dictionary<string, string>();
    private readonly HashSet<string> _warnedTargets = new HashSet<string>();
    private readonly Plane[] _planes = new Plane[6];

    private EncounterDirector _hooked;
    private EncounterTableSO _table;

    // 지금 바라보고 있는 장면과 얼마나 오래 봤는지. 장면 하나를 한 번 놓을 때마다 한 번만 보낸다.
    private string _gazedScene = string.Empty;
    private float _gazedSeconds;
    private readonly HashSet<string> _observedAt = new HashSet<string>();

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
        if (!scene.IsValid() || !scene.isLoaded) return;

        foreach (EncounterStager existing in FindObjectsByType<EncounterStager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (existing.gameObject.scene == scene) return;
        }

        // 근무 씬인지 판별하는 기준을 NightRunDriver와 똑같이 잡는다 — 게임 시계가 있는 씬.
        bool isDutyScene = false;
        foreach (GameTime candidate in FindObjectsByType<GameTime>(FindObjectsSortMode.None))
        {
            if (candidate.gameObject.scene == scene)
            {
                isDutyScene = true;
                break;
            }
        }

        if (!isDutyScene) return;

        GameObject go = new GameObject("EncounterStager (auto)");
        SceneManager.MoveGameObjectToScene(go, scene);
        go.AddComponent<EncounterStager>();
    }

    private void Update()
    {
        Hook();
        StepObserve();
    }

    /// <summary>
    /// 모형을 바라보면 <see cref="SignalKind.ModelObserved"/>를 보낸다. <b>이 신호가 없으면 조우가 한 단계도 나아가지 않는다.</b>
    /// <para>콜라이더가 아니라 <b>각도 + 가림 판정</b>으로 본다 — 모형에 콜라이더를 달면 그 뒤의 판정 대상이
    /// 응시에서 가려지고 통행도 막힌다.</para>
    /// </summary>
    private void StepObserve()
    {
        if (!NightRun.IsNightActive) return;

        Camera cam = ResolveCamera();
        if (cam == null) return;

        string looking = string.Empty;
        float best = observeAngle;

        foreach (KeyValuePair<string, string> pair in _placedAt)
        {
            if (_observedAt.Contains(pair.Key + "@" + pair.Value)) continue;

            Transform marker = MarkerOf(pair.Value);
            if (marker == null) continue;

            Vector3 aim = marker.position + Vector3.up * (modelLift + 0.9f);
            float angle = Vector3.Angle(cam.transform.forward, aim - cam.transform.position);
            if (angle > best) continue;
            if (!IsSceneVisible(pair.Key)) continue;

            best = angle;
            looking = pair.Key;
        }

        if (looking != _gazedScene)
        {
            _gazedScene = looking;
            _gazedSeconds = 0f;
        }

        if (looking.Length == 0) return;

        _gazedSeconds += Time.deltaTime;
        if (_gazedSeconds < observeSeconds) return;

        string at;
        _placedAt.TryGetValue(looking, out at);
        _observedAt.Add(looking + "@" + at);

        JudgeSignal signal = JudgeSignal.Target(SignalKind.ModelObserved, looking);
        NightRun.Send(signal);
    }

    private void OnDestroy()
    {
        // 연출기는 이 회차 것이고 우리가 꽂은 통로도 이 씬 것이다. 씬을 떠나면 걷어 낸다.
        if (_hooked == null) return;

        if (_hooked.IsVisible == IsSceneVisible) _hooked.IsVisible = null;
        if (_hooked.PlaceModel == PlaceSceneModel) _hooked.PlaceModel = null;
        _hooked = null;
    }

    /// <summary>
    /// 연출기는 회차가 열려야 생기고 <b>새 회차마다 새 인스턴스</b>다. 그래서 매 프레임 가볍게 확인하고
    /// 들고 있던 것과 다를 때만 다시 꽂는다.
    /// </summary>
    private void Hook()
    {
        EncounterDirector director = NightRun.Encounter;
        if (director == null || director == _hooked) return;

        director.IsVisible = IsSceneVisible;
        director.PlaceModel = PlaceSceneModel;
        _hooked = director;
        _placedAt.Clear();
    }

    // ─────────────────────────────── 시야 ───────────────────────────────

    /// <summary>
    /// 그 장면의 모형이 지금 보이는가. <b>판단할 표식이 없으면 false</b>다 —
    /// 아직 아무것도 놓이지 않았다는 뜻이고, 놓이지 않은 것은 보일 수 없다.
    /// </summary>
    private bool IsSceneVisible(string sceneId)
    {
        Transform marker = CurrentMarker(sceneId);
        if (marker == null) return false;

        Camera cam = ResolveCamera();
        if (cam == null) return false;

        GeometryUtility.CalculateFrustumPlanes(cam, _planes);
        Bounds box = new Bounds(marker.position + Vector3.up * modelLift, ModelBox);
        if (!GeometryUtility.TestPlanesAABB(_planes, box)) return false;

        // 가려졌는가. 트리거는 「가시 충돌체」가 아니므로 무시한다 — GazeProbe와 같은 기준이어야
        // 「응시로는 잡히는데 시야 판정은 안 보인다고 한다」 같은 어긋남이 생기지 않는다.
        Vector3 eye = cam.transform.position;
        Vector3 aim = marker.position + Vector3.up * modelLift;
        RaycastHit hit;
        if (Physics.Linecast(eye, aim, out hit, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.transform != marker && !hit.transform.IsChildOf(marker))
            {
                GameObject model;
                if (!_models.TryGetValue(sceneId, out model) || model == null ||
                    (hit.transform != model.transform && !hit.transform.IsChildOf(model.transform)))
                {
                    return false;   // 벽·문이 앞을 막았다.
                }
            }
        }

        return true;
    }

    /// <summary>그 장면의 모형이 지금 서 있는 표식. 아직 놓이지 않았으면 null.</summary>
    private Transform CurrentMarker(string sceneId)
    {
        string targetId;
        if (!_placedAt.TryGetValue(sceneId, out targetId) || string.IsNullOrEmpty(targetId))
        {
            // 아직 PlaceModel을 못 받았으면 연출기의 단계에서 되짚는다(이 컴포넌트가 늦게 선 경우).
            targetId = TargetFromDirector(sceneId);
        }

        return string.IsNullOrEmpty(targetId) ? null : MarkerOf(targetId);
    }

    private string TargetFromDirector(string sceneId)
    {
        EncounterDirector director = NightRun.Encounter;
        if (director == null) return string.Empty;

        EncounterTableSO table = Table();
        if (table == null) return string.Empty;

        EncounterTableSO.Scene scene = table.Find(sceneId);
        if (scene == null) return string.Empty;

        int step = director.StepOf(sceneId);
        if (step > 0) return scene.ApproachOf(step);
        if (director.IsExitGatePlaced(sceneId)) return scene.ExitGateTargetId;

        return string.Empty;
    }

    // ─────────────────────────────── 배치 ───────────────────────────────

    /// <summary>모형을 그 접근 지점으로 옮긴다. 장면마다 모형은 하나다.</summary>
    private void PlaceSceneModel(string sceneId, string targetId)
    {
        _placedAt[sceneId] = targetId;

        Transform marker = MarkerOf(targetId);
        if (marker == null)
        {
            // 조우 지점이 씬에 없으면 조우가 통째로 진행되지 않는다. 한 번만 알린다.
            if (_warnedTargets.Add(targetId))
            {
                Debug.LogWarning("[EncounterStager] 조우 지점 '" + targetId + "'이 씬에 없습니다. " +
                                 sceneId + "의 모형을 세울 자리가 없어 이 장면은 진행되지 않습니다.", this);
            }

            return;
        }

        GameObject model = ModelFor(sceneId);

        // 자리만 옮기고 <b>방향은 건드리지 않는다.</b> 씬 모형은 임포트 회전이 이미 들어가 있어
        // (과학실 인체모형은 270, 270, 0) 표식 회전을 그대로 씌우면 모형이 드러눕는다.
        // 플레이어 쪽을 보게 하는 것은 진짜 모형 프리팹이 온 뒤에 정한다.
        model.transform.position = marker.position + Vector3.up * modelLift;
        if (!model.activeSelf) model.SetActive(true);
    }

    private GameObject ModelFor(string sceneId)
    {
        GameObject model;
        if (_models.TryGetValue(sceneId, out model) && model != null) return model;

        // 실물이 이미 씬에 있으면 그것을 움직인다 — 대역을 하나 더 세우면 같은 모형이 둘이 된다.
        Transform inScene = SceneModelFor(sceneId);
        if (inScene != null)
        {
            model = inScene.gameObject;
        }
        else if (modelPrefab != null)
        {
            model = Instantiate(modelPrefab);
            model.name = "Encounter Model (" + sceneId + ")";
        }
        else
        {
            model = BuildStandIn(sceneId);
        }

        _models[sceneId] = model;
        return model;
    }

    /// <summary>
    /// <b>임시 대역이다.</b> 애니메이션이 붙은 리깅 모델이 오면 <see cref="modelPrefab"/>에 꽂으면 되고
    /// 이 메서드는 그대로 남겨 둔다 — 프리팹이 빠졌을 때 조우가 통째로 사라지는 것보다 대역이 서는 편이 낫다.
    /// <para>과학실에 있는 인체모형은 <b>쓰지 않는다</b>. 그것은 S1의 「보관 위치」이고 조우 모형은 따로 온다.</para>
    /// <para><b>콜라이더를 붙이지 않는다</b> — 응시 레이를 가로채면 판정 대상이 모형에 가려지고 통행도 막힌다.</para>
    /// </summary>
    private GameObject BuildStandIn(string sceneId)
    {
        GameObject root = new GameObject("Encounter Model (임시대역: " + sceneId + ")");

        Material mat = StandInMaterial();
        Limb(root.transform, PrimitiveType.Cube, new Vector3(0f, 0.85f, 0f), new Vector3(0.5f, 1.7f, 0.3f), mat);
        Limb(root.transform, PrimitiveType.Sphere, new Vector3(0f, 1.82f, 0f), new Vector3(0.26f, 0.3f, 0.26f), mat);
        Limb(root.transform, PrimitiveType.Cube, new Vector3(-0.32f, 1.05f, 0f), new Vector3(0.14f, 0.62f, 0.14f), mat);
        Limb(root.transform, PrimitiveType.Cube, new Vector3(0.32f, 1.05f, 0f), new Vector3(0.14f, 0.62f, 0.14f), mat);

        return root;
    }

    private static void Limb(Transform parent, PrimitiveType kind, Vector3 pos, Vector3 scale, Material mat)
    {
        GameObject go = GameObject.CreatePrimitive(kind);
        Collider col = go.GetComponent<Collider>();
        if (col != null) Destroy(col);

        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;

        Renderer r = go.GetComponent<Renderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
    }

    private Material _standInMat;

    private Material StandInMaterial()
    {
        if (_standInMat != null) return _standInMat;

        // 밤이 캄캄해서 빛을 받는 재질은 아예 안 보인다. 대역은 보여야 하므로 Unlit을 쓴다.
        // 밝게 잡으면 블룸에 날아가 형체가 뭉개지므로 어둡게 잡는다(2026-09-23 실측과 같은 이유).
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");

        _standInMat = new Material(shader);
        _standInMat.color = new Color(0.52f, 0.48f, 0.43f);   // 2026-09-23 실측: 0.30은 밤에 형체가 안 잡힌다
        return _standInMat;
    }

    // ─────────────────────────────── 거들기 ───────────────────────────────

    private Transform SceneModelFor(string sceneId)
    {
        if (sceneModels == null) return null;

        for (int i = 0; i < sceneModels.Length; i++)
        {
            if (sceneModels[i].SceneId == sceneId) return sceneModels[i].Model;
        }

        return null;
    }

    private static Transform MarkerOf(string targetId)
    {
        if (string.IsNullOrEmpty(targetId)) return null;

        JudgeTarget target;
        return JudgeTargetRegistry.TryGet(targetId, out target) && target != null ? target.transform : null;
    }

    private Camera ResolveCamera()
    {
        if (playerCamera != null) return playerCamera;

        playerCamera = Camera.main;
        return playerCamera;
    }

    private EncounterTableSO Table()
    {
        if (_table != null) return _table;

        _table = Resources.Load<EncounterTableSO>("EncounterTable");
        return _table;
    }
}

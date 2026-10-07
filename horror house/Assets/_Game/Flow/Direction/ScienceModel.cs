using NightDuty;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 과학실의 몬스터 인체 모형(2026-10-04 42차, 민: 「첫날부터 정지한 모습으로 과학실에 있다가, 점점 움직이다가 복도에서 급습」).
/// 근무 내내 과학실에 서 있는 모형 하나(<c>mob.dummy.stand</c>)를 세우고, 밤마다·과학실을 나갈 때마다 한 칸씩 자리를 옮긴다.
/// <list type="bullet">
/// <item>자리 0 테이프 안, 두 문 사이(복도 쪽)를 보고 선 정지(1일차 — 「모형의 정상 위치 학습」) → 1 테이프 안에서 플레이어가 들어온 문을 봄 →
/// 2 테이프 밖 동쪽 문 안쪽 구석, 문을 봄 → 3 과학실 앞 복도 끝(동쪽 끝)에서 복도를 봄.</item>
/// <item>자리 진행 규칙은 Core <see cref="ModelProgress"/>. 그 밤의 시작 자리 = max(일차 자리 1일 0·2일 1·3일~ 2, 구간 자리 조도·배치 중 큰 쪽이 1이면 1, 2 이상이면 2).
/// 1일차는 움직이지 않는다. 2일차부터 플레이어가 과학실을 <b>나갈 때마다</b>(보지 않을 때) 한 칸씩, 그 밤 최대 자리까지 옮긴다.</item>
/// <item>최대 자리는 평소 2(과학실 안). <b>모형 급습이 가능한 밤</b>(조도 3 + 배치 2 — 최종 기획서 「배치 3: 모형이 복도로 나올 수 있음」, 또는 그 밤 편성에 모형 급습)만 3(복도).</item>
/// <item>모형 급습(<see cref="ProgramCatalog.ModelRush"/>)이 대면하면 이 모형은 사라지고(급습 대역이 대신 나온다) 그 밤에는 돌아오지 않는다.
/// 복도 끝에 선 자(<see cref="ProgramCatalog.HallEndFigure"/>)가 나와 있는 동안에도 숨는다.</item>
/// <item>S3 「인체 모형을 빛으로 확인하십시오」의 대상 <c>rule.S3.model</c>은 이 모형의 조준점이다 — 몸을 감싸는 단단한 응시 상자를 붙여 응시 원뿔이 잡는다.
/// 씬의 바닥 토르소(옛 대상, 콜라이더가 없어 응시로 잡히지 않았다)는 근무 중 숨긴다. 씬 파일은 고치지 않는다.</item>
/// <item>재시작하면 그 밤의 시작 자리로 돌아간다. 판정과 무관(S3 응시 대상 자리만 따라간다).</item>
/// </list>
/// 근무 씬에 자동으로 선다.
/// </summary>
[DisallowMultipleComponent]
public sealed class ScienceModel : MonoBehaviour
{
    /// <summary>대역 ID(<c>Resources/StandIns/mob.dummy.stand</c>).</summary>
    public const string StandInId = "mob.dummy.stand";

    /// <summary>자리 수.</summary>
    public const int SpotCount = 4;

    // 2026-10-04 실측(빈 바닥 칸): 테이프(science.tape) 가운데 · 과학실 동쪽 문 안쪽 구석(통로 z 42.75를 막지 않는 자리) · 과학실 앞 복도 동쪽 끝. y는 바닥을 쏘아 정한다.
    private static readonly Vector3 TapeFallback = new Vector3(46.03f, 0f, 41.04f);
    private static readonly Vector3 AisleSpot = new Vector3(51.6f, 0f, 41.3f);
    private static readonly Vector3 HallSpot = new Vector3(53.3f, 0f, 46.4f);

    // 과학실 문(복도 쪽 벽 z = 44). 플레이어가 들어온 문을 본다.
    private static readonly Vector3 WestDoor = new Vector3(44f, 0f, 44f);
    private static readonly Vector3 EastDoor = new Vector3(52f, 0f, 44f);

    private GameObject _model;
    private SpaceZones _zones;
    private GameObject _sceneTarget;
    private Transform _sceneProp;
    private Renderer[] _hiddenRenderers = new Renderer[0];
    private Collider[] _hiddenColliders = new Collider[0];
    private int _day = -1;
    private int _base;
    private int _max;
    private int _spot;
    private bool _rushed;
    private SpaceId _lastSpace = SpaceId.None;
    private Vector3 _door = WestDoor;

    /// <summary>지금 근무 씬의 것. 없으면 null.</summary>
    public static ScienceModel Active { get; private set; }

    /// <summary>지금 자리(0~3).</summary>
    public int Spot
    {
        get { return _spot; }
    }

    /// <summary>그 밤 시작 자리.</summary>
    public int BaseSpot
    {
        get { return _base; }
    }

    /// <summary>그 밤 최대 자리(3이면 복도까지).</summary>
    public int MaxSpot
    {
        get { return _max; }
    }

    /// <summary>세워 둔 모형(숨겨져 있을 수 있음). 없으면 null.</summary>
    public GameObject Model
    {
        get { return _model; }
    }

    /// <summary>지금 보이는지(급습·복도 끝 연출 중이거나 급습 뒤면 숨는다).</summary>
    public bool Visible
    {
        get { return _model != null && _model.activeSelf; }
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
        if (!FlowAutoInstall.IsDutyScene(scene) || FlowAutoInstall.Exists<ScienceModel>(scene)) return;
        FlowAutoInstall.CreateHost<ScienceModel>(scene, "ScienceModel (auto)");
    }

    private void OnEnable()
    {
        Active = this;
        EventBus.NightRestarted += OnRestarted;
    }

    private void OnDisable()
    {
        EventBus.NightRestarted -= OnRestarted;
        RestoreSceneModel();
        if (_model != null) Destroy(_model);
        _model = null;
        if (Active == this) Active = null;
    }

    // ── 공개 ─────────────────────────────────────────────────

    /// <summary>디버그: 그 자리로 옮긴다(0~3).</summary>
    public void DebugSpot(int spot)
    {
        _spot = Mathf.Clamp(spot, 0, SpotCount - 1);
        _rushed = false;
        Place();
    }

    // ── 밤 따라가기 ──────────────────────────────────────────

    private void Update()
    {
        if (!NightRun.IsNightActive)
        {
            if (_model != null && _model.activeSelf) _model.SetActive(false);
            return;
        }

        if (NightRun.Day != _day)
        {
            _day = NightRun.Day;
            NewNight();
        }

        TrackSpace();

        bool staged = false;
        DirectionStage stage = DirectionStage.Active;
        if (stage != null)
        {
            foreach (string id in stage.StagedIds)
            {
                if (id == ProgramCatalog.ModelRush)
                {
                    _rushed = true;
                    staged = true;
                }
                else if (id == ProgramCatalog.HallEndFigure)
                {
                    staged = true;
                }
            }
        }

        bool show = !_rushed && !staged;
        if (_model != null && _model.activeSelf != show) _model.SetActive(show);
    }

    private void NewNight()
    {
        Band i = NightRun.Shown != null ? NightRun.Shown.GetBand(FearAxis.Illuminance) : Band.Band0;
        Band l = NightRun.Shown != null ? NightRun.Shown.GetBand(FearAxis.Layout) : Band.Band0;
        bool rush = NightRun.Program != null && NightRun.Program.HasEncounter(ProgramCatalog.ModelRush);
        ModelProgress.Spots(_day, i, l, rush, out _base, out _max);
        _spot = _base;
        _rushed = false;
        _lastSpace = SpaceId.None;
        Place();
        if (DirectionStage.Verbose) Debug.Log("[ScienceModel] " + _day + "일차 모형 자리 " + _spot + "(최대 " + _max + ")");
    }

    private void OnRestarted(RestartResult result)
    {
        if (_day < 0) return;
        _spot = _base;
        _rushed = false;
        _lastSpace = SpaceId.None;   // 재시작 자리로 옮겨지는 것을 「과학실을 나갔다」로 세지 않는다
        Place();
    }

    private void TrackSpace()
    {
        SpaceId now = SpaceIds.Canonical(NightRun.CurrentSpace);
        if (now == _lastSpace) return;

        if (now == SpaceId.ScienceRoom)
        {
            // 들어온 문 — 가까운 쪽.
            Transform player = Camera.main != null ? Camera.main.transform : null;
            if (player != null)
            {
                float w = Mathf.Abs(player.position.x - WestDoor.x);
                float e = Mathf.Abs(player.position.x - EastDoor.x);
                _door = w <= e ? WestDoor : EastDoor;
            }
        }
        else if (_lastSpace == SpaceId.ScienceRoom && _spot < _max && !_rushed)
        {
            // 등 뒤에서 — 과학실을 나가면 한 칸.
            _spot++;
            Place();
            if (DirectionStage.Verbose) Debug.Log("[ScienceModel] 모형이 움직였다 → 자리 " + _spot);
        }

        _lastSpace = now;
    }

    // ── 세우기 ───────────────────────────────────────────────

    private void Place()
    {
        Vector3 at;
        Quaternion rot;
        Pose(_spot, out at, out rot);

        if (_model == null)
        {
            HideSceneModel();
            _model = StandInFactory.Create(StandInId, at, rot, string.Empty);
            if (_model == null) return;
            _model.name = "과학실 인체 모형 (몬스터)";
            StandInFactory.Dress(_model, true, FinalCues.ModelTarget);
        }
        else
        {
            _model.transform.SetPositionAndRotation(at, rot);
        }
    }

    private void Pose(int spot, out Vector3 at, out Quaternion rot)
    {
        Vector3 tape = TapeFallback;
        if (_zones == null) _zones = FindFirstObjectByType<SpaceZones>();
        SpaceZones zones = _zones;
        Bounds b;
        if (zones != null && zones.TryGetSignalZone("science.tape", out b)) tape = new Vector3(b.center.x, 0f, b.center.z);

        Vector3 p;
        Vector3 face;
        switch (spot)
        {
            case 0:
                p = tape;
                face = (WestDoor + EastDoor) * 0.5f - tape;   // 51차(민: 「처음 등장할 때 칠판을 향해 뒤돌아 있다」): 남쪽 벽은 칠판이었다 — 문 두 개 쪽(북)을 본다
                break;
            case 1:
                p = tape;
                face = _door - tape;
                break;
            case 2:
                p = AisleSpot;
                face = _door - AisleSpot;
                break;
            default:
                p = HallSpot;
                face = Vector3.left;   // 복도 서쪽(경비실 쪽)을 내려다본다
                break;
        }

        face.y = 0f;
        if (face.sqrMagnitude < 0.0001f) face = Vector3.forward;
        at = new Vector3(p.x, FloorY(p), p.z);
        rot = Quaternion.LookRotation(face.normalized, Vector3.up);
    }

    private float FloorY(Vector3 p)
    {
        RaycastHit[] hits = Physics.RaycastAll(new Vector3(p.x, 3.4f, p.z), Vector3.down, 4f, ~0, QueryTriggerInteraction.Ignore);
        float best = 1.5f;
        float bestDist = float.MaxValue;
        for (int i = 0; i < hits.Length; i++)
        {
            if (_model != null && hits[i].collider.transform.IsChildOf(_model.transform)) continue;
            if (_sceneProp != null && hits[i].collider.transform.IsChildOf(_sceneProp)) continue;
            if (hits[i].normal.y < 0.7f || hits[i].distance >= bestDist) continue;
            bestDist = hits[i].distance;
            best = hits[i].point.y;
        }

        return best;
    }

    /// <summary>씬의 옛 S3 대상(바닥 토르소)을 숨기고 그 기준점을 끈다 — 기준점은 이 모형의 조준점으로 옮긴다.</summary>
    private void HideSceneModel()
    {
        JudgeTarget old;
        if (!JudgeTargetRegistry.TryGet(FinalCues.ModelTarget, out old) || old == null) return;
        _sceneTarget = old.gameObject;
        _sceneProp = old.transform.parent;
        if (_sceneProp != null)
        {
            _hiddenRenderers = _sceneProp.GetComponentsInChildren<Renderer>();
            _hiddenColliders = _sceneProp.GetComponentsInChildren<Collider>();
            for (int i = 0; i < _hiddenRenderers.Length; i++) _hiddenRenderers[i].enabled = false;
            for (int i = 0; i < _hiddenColliders.Length; i++) _hiddenColliders[i].enabled = false;
        }

        _sceneTarget.SetActive(false);
    }

    private void RestoreSceneModel()
    {
        for (int i = 0; i < _hiddenRenderers.Length; i++)
        {
            if (_hiddenRenderers[i] != null) _hiddenRenderers[i].enabled = true;
        }

        for (int i = 0; i < _hiddenColliders.Length; i++)
        {
            if (_hiddenColliders[i] != null) _hiddenColliders[i].enabled = true;
        }

        if (_sceneTarget != null) _sceneTarget.SetActive(true);
        _hiddenRenderers = new Renderer[0];
        _hiddenColliders = new Collider[0];
        _sceneTarget = null;
    }
}

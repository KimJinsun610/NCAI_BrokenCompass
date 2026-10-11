using NightDuty;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 과학실의 몬스터 인체 모형(2026-10-04 42차, 민: 「첫날부터 정지한 모습으로 과학실에 있다가, 점점 움직이다가 복도에서 급습」).
/// 근무 내내 과학실에 서 있는 모형 하나(<c>mob.dummy.stand</c>)를 세우고, 밤마다·과학실을 나갈 때마다 한 칸씩 자리를 옮긴다.
/// <list type="bullet">
/// <item>자리 0 서쪽 문 바로 옆 북서 구석에서 교실 안을 보고 선 정지(1일차 — 「모형의 정상 위치 학습」, 57차 민 스크린샷) → 1 테이프 안에서 플레이어가 들어온 문을 봄 →
/// 2 테이프 밖 동쪽 문 안쪽 구석, 문을 봄 → 3 과학실 앞 복도 끝(동쪽 끝)에서 복도를 봄.</item>
/// <item>자리 진행 규칙은 Core <see cref="ModelProgress"/>. 그 밤의 시작 자리 = max(일차 자리 1일 0·2일 1·3일~ 2, 배치 구간이 1이면 1, 2 이상이면 2 — 56차부터 배치만).
/// 1일차는 움직이지 않는다. 2일차부터 플레이어가 과학실을 <b>나갈 때마다</b>(보지 않을 때) 한 칸씩, 그 밤 최대 자리까지 옮긴다.</item>
/// <item>최대 자리는 평소 2(과학실 안). <b>복도까지 나오는 밤</b>(배치 3 — 최종 기획서 「배치 3: 모형이 복도로 나올 수 있음」, 또는 그 밤 편성에 모형 급습)만 3(복도).</item>
/// <item>모형 급습(<see cref="ProgramCatalog.ModelRush"/>)이 대면하면 이 모형은 사라지고(급습 대역이 대신 나온다) 그 밤에는 돌아오지 않는다.
/// 70차 ④: 복도로 나오는 것은 날짜 사슬뿐이다 — 복도 끝에 선 자의 밤과 다음 날 급습 밤에는 이 모형이 복도 끝(<see cref="HallFigure"/>)에 나가 있어 과학실에는 없고,
/// 그 밖의 밤에는 과학실 안(자리 0~2)에만 있다(옛 「배치 3이면 복도 자리 3」은 쓰지 않는다 — 둘이 되지 않게).</item>
/// <item>S3 「인체 모형을 빛으로 확인하십시오」의 대상 <c>rule.S3.model</c>은 이 모형의 조준점이다 — 몸을 감싸는 단단한 응시 상자를 붙여 응시 원뿔이 잡는다.
/// 씬의 바닥 토르소(옛 대상, 콜라이더가 없어 응시로 잡히지 않았다)는 근무 중 숨긴다. 씬 파일은 고치지 않는다.</item>
/// <item>재시작하면 그 밤의 시작 자리로 돌아간다. 판정과 무관(S3 응시 대상 자리만 따라간다).</item>
/// <item>64차(플레이테스트 2026-10-07 「인체모형 더 활발히 이동」): 2일차부터 ① 플레이어가 같은 곳에 있는데 모형이 화면에 보이지 않은 채 몇 초(2일 5 · 3일 3.5 · 4일~ 2.5) 지나면
/// 플레이어 쪽으로 한 걸음(0.5 · 0.75 · 1m) 다가서서 플레이어를 본다(2.5m 안으로는 오지 않는다) ② 최대 자리에 닿은 뒤에도 과학실을 나갈 때마다 그 밤 범위의 다른 자리로 옮긴다(<see cref="ModelProgress.NextSpot"/>).</item>
/// <item>70차(민: 「목이 돌아가는 연출과 오래 바라보면 사망하는 연출을 복도 끝에 선 인체 모형에게 옮겨 줘」): 이 모형은 이제 목이 꺾이지도, 오래 바라봐 붙잡지도 않는다 — <see cref="HallFigure"/>로 옮겼다.</item>
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

    // 57차(민: 「인체 모형은 과학실의 이 공간에 처음 위치하게」 — 서쪽 문 바로 왼쪽 구석). 벽(x 42.25 · z 43.75)에서 0.5m, 반지름 0.3 캡슐이 아무것과도 겹치지 않는 자리.
    private static readonly Vector3 CornerSpot = new Vector3(42.75f, 0f, 43.25f);

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

    // 64차
    /// <summary>보지 않을 때 다가오기 시작하는 날.</summary>
    public const int CreepFromDay = 2;

    /// <summary>플레이어에게 이보다 가까이 다가오지 않는다(수평 m).</summary>
    public const float CreepMinDistance = 2.5f;

    private float _unseenSince = -1f;
    private float _nextCreepCheck;
    private int _creeps;
    private bool _displaced;
    /// <summary>64차: 그 밤 보지 않는 사이 다가선 걸음 수.</summary>
    public int Creeps
    {
        get { return _creeps; }
    }

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
            foreach (string id in stage.PresentedIds)
            {
                if (id == ProgramCatalog.ModelRush) staged = true;   // 70차: 급습 대역이 복도 끝에 서 있는 동안 — 같은 자리에 둘이 겹치지 않게
            }

            foreach (string id in stage.StagedIds)
            {
                if (id == ProgramCatalog.ModelRush)
                {
                    _rushed = true;
                    staged = true;
                }
            }
        }

        // 70차 ④(민: 「복도 끝 모형과 과학실 모형은 겹치면 안 돼. 과학실 모형이 복도에 나오고, 다음 날 급습해야 해」):
        // 복도 끝에 선 자의 밤과 급습 밤에는 이 모형이 복도 끝(HallFigure)에 나가 있다 — 과학실은 비어 있다.
        NightProgram program = NightRun.Program;
        bool outside = program != null && (program.HasEncounter(ProgramCatalog.HallEndFigure) || program.HasEncounter(ProgramCatalog.ModelRush));

        // 64차: 인체 모형 사망 컷신(DeathCutscene_Illuminance)이 도는 동안은 컷신의 모형만 — 과학실에서 붙잡히면 둘이 겹쳐 보인다.
        bool show = !outside && !_rushed && !staged && DeathCutscene.Playing == null;
        if (_model != null && _model.activeSelf != show) _model.SetActive(show);
        if (show && _model != null && !NightRun.Sandbox) Creep();   // 64차 · 71차: 흐름 정지 중에는 다가오지 않는다
        else _unseenSince = -1f;
    }

    private void NewNight()
    {
        Band l = NightRun.Shown != null ? NightRun.Shown.GetBand(FearAxis.Layout) : Band.Band0;   // 56차: 배치만(자리가 바뀌는 것은 배치의 언어)
        bool rush = NightRun.Program != null && NightRun.Program.HasEncounter(ProgramCatalog.ModelRush);
        ModelProgress.Spots(_day, l, rush, out _base, out _max);
        // 70차 ④: 복도로 나오는 것은 날짜 사슬(복도 끝에 선 자 → 다음 날 급습)로만 — 과학실 모형이 따로 복도 자리(3)에 서면 둘이 된다.
        _max = Mathf.Min(_max, ModelProgress.HallSpot - 1);
        _base = Mathf.Min(_base, _max);
        _spot = _base;
        _rushed = false;
        _lastSpace = SpaceId.None;
        _creeps = 0;
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
        else if (_lastSpace == SpaceId.ScienceRoom && _max > _base && !_rushed && !NightRun.Sandbox)   // 71차: 흐름 정지 중에는 자리를 옮기지 않는다
        {
            // 등 뒤에서 — 과학실을 나가면 한 칸. 64차: 최대 자리에 닿은 뒤에도 나갈 때마다 그 밤 범위의 다른 자리로(「더 활발히」).
            _spot = ModelProgress.NextSpot(_spot, _base, _max, Random.Range(0, 1000));
            Place();
            if (DirectionStage.Verbose) Debug.Log("[ScienceModel] 모형이 움직였다 → 자리 " + _spot);
        }
        else if (_lastSpace == SpaceId.ScienceRoom && _displaced && _spot < ModelProgress.HallSpot)
        {
            // 64차: 다가섰던 모형은 플레이어가 나가면 제 자리로 — 다음에 들어올 때 문간을 막고 서 있지 않게.
            Place();
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

        _unseenSince = -1f;
        _displaced = false;
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
                // 57차: 첫 자리는 북서 구석 — 교실 안(테이프 쪽)을 본다. 51차 「칠판을 향해 뒤돌아 있다」는 다시 생기지 않는다(남쪽 칠판은 등 뒤가 아니다).
                p = CornerSpot;
                face = tape - CornerSpot;
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

    // ── 64차: 보지 않을 때 다가옴 ─────────────────────────────

    /// <summary>
    /// 2일차부터, 플레이어가 모형과 같은 곳(과학실 — 복도 자리면 복도)에 있고 모형이 화면에 보이지 않은 채 <see cref="CreepDelay"/>초가 지나면
    /// 플레이어 쪽으로 한 걸음(<see cref="CreepStep"/>) 다가서서 플레이어를 본다. <see cref="CreepMinDistance"/>m 안으로는 오지 않는다.
    /// </summary>
    private void Creep()
    {
        if (_day < CreepFromDay || _rushed)
        {
            _unseenSince = -1f;
            return;
        }

        if (Time.time < _nextCreepCheck) return;
        _nextCreepCheck = Time.time + 0.2f;

        Camera cam = Camera.main;
        Transform root = PlayerSensors.Active != null ? PlayerSensors.Active.PlayerRoot : null;
        if (cam == null || root == null || !SameArea())
        {
            _unseenSince = -1f;
            return;
        }

        if (UnseenDespawn.VisibleTo(_model, cam))
        {
            _unseenSince = -1f;
            return;
        }

        if (_unseenSince < 0f) _unseenSince = Time.time;
        if (Time.time - _unseenSince < CreepDelay(_day)) return;
        _unseenSince = Time.time;   // 다음 걸음은 다시 기다린다
        if (TryStep(root.position) && DirectionStage.Verbose) Debug.Log("[ScienceModel] 보지 않는 사이 다가섰다 → " + _model.transform.position);
    }

    /// <summary>플레이어가 모형과 같은 곳에 있는지 — 과학실 자리면 과학실, 복도 자리면 복도.</summary>
    private bool SameArea()
    {
        SpaceId now = SpaceIds.Canonical(NightRun.CurrentSpace);
        return _spot >= ModelProgress.HallSpot ? now == SpaceId.Corridor : now == SpaceId.ScienceRoom;
    }

    /// <summary>그날 보이지 않은 채 몇 초 지나면 한 걸음 다가오는지.</summary>
    public static float CreepDelay(int day)
    {
        if (day <= 2) return 5f;
        if (day == 3) return 3.5f;
        return 2.5f;
    }

    /// <summary>그날 한 걸음(m).</summary>
    public static float CreepStep(int day)
    {
        if (day <= 2) return 0.5f;
        if (day == 3) return 0.75f;
        return 1f;
    }

    private bool TryStep(Vector3 playerFeet)
    {
        Vector3 from = _model.transform.position;
        Vector3 to = playerFeet - from;
        to.y = 0f;
        float dist = to.magnitude;
        if (dist <= CreepMinDistance + 0.05f) return false;
        Vector3 dir = to / dist;
        float step = Mathf.Min(CreepStep(_day), dist - CreepMinDistance);

        float[] turns = { 0f, 35f, -35f, 70f, -70f };
        for (int i = 0; i < turns.Length; i++)
        {
            Vector3 d = Quaternion.Euler(0f, turns[i], 0f) * dir;
            Vector3 p = from + d * step;
            if (!InsideArea(p) || Blocked(p)) continue;
            p.y = FloorY(p);
            Vector3 face = playerFeet - p;
            face.y = 0f;
            Quaternion rot = face.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(face.normalized, Vector3.up) : _model.transform.rotation;
            _model.transform.SetPositionAndRotation(p, rot);
            _creeps++;
            _displaced = true;
            return true;
        }

        return false;
    }

    /// <summary>모형의 자리 공간 상자 안(벽에서 0.35m)인지. 상자를 모르면 참.</summary>
    private bool InsideArea(Vector3 p)
    {
        if (_zones == null) _zones = FindFirstObjectByType<SpaceZones>();
        if (_zones == null) return true;
        Bounds box;
        SpaceId area = _spot >= ModelProgress.HallSpot ? SpaceId.Corridor : SpaceId.ScienceRoom;
        if (!_zones.TryGetSpaceBox(area, out box)) return true;
        box.Expand(new Vector3(-0.7f, 0f, -0.7f));
        return p.x >= box.min.x && p.x <= box.max.x && p.z >= box.min.z && p.z <= box.max.z;
    }

    /// <summary>그 자리에 모형 몸(반지름 0.3 캡슐, 발목 위)이 들어갈 수 없는지 — 자기·플레이어 콜라이더와 트리거는 빼고.</summary>
    private bool Blocked(Vector3 p)
    {
        float y = FloorY(p);
        Vector3 a = new Vector3(p.x, y + 0.4f, p.z);
        Vector3 b = new Vector3(p.x, y + 1.6f, p.z);
        Transform player = PlayerSensors.Active != null ? PlayerSensors.Active.PlayerRoot : null;
        Collider[] hits = Physics.OverlapCapsule(a, b, 0.3f, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits.Length; i++)
        {
            Transform t = hits[i].transform;
            if (t.IsChildOf(_model.transform)) continue;
            if (player != null && t.IsChildOf(player)) continue;
            return true;
        }

        return false;
    }
}

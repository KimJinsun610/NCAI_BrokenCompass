using NightDuty;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 복도 끝에 선 자(70차, 민: 「복도 끝에 서 있는 자는 인체 모형으로 · 모형 급습 전날에 배치 · 위치는 모형 급습이 시작되는 곳과 같게 ·
/// 그냥 가만히 서 있기만 하고 별도의 행동은 하지 않게 · 다른 곳에 있던 목이 돌아가는 연출과 오래 바라보면 사망하는 연출을 이 모형에게 · 추적·공격·이동은 추가하지 말 것」).
/// <list type="bullet">
/// <item>70차 ④: 이 모형이 곧 <b>과학실 모형이 복도로 나온 것</b>이다 — 그 이틀 동안 과학실에는 모형이 없다(<see cref="ScienceModel"/>이 숨는다).
/// 급습 밤에도 복도 끝에 그대로 서 있다가, 급습 대역이 서는 순간(플레이어는 과학실 안이라 보지 못한다) 물러난다. 급습 뒤 그 밤에는 다시 서지 않는다(재시작이면 다시).</item>
/// <item>그 밤 편성에 <see cref="ProgramCatalog.HallEndFigure"/>가 있으면(<c>ProgramDirector</c> 날짜 사슬 — 모형 급습 전날) 밤 내내
/// 과학실 옆 복도 동쪽 끝, 비상구 유도등 아래(<see cref="DirectionStage.RushHallSpot"/> — 다음 날 모형 급습이 달려 나오는 자리)에
/// 인체 모형(<c>mob.dummy.stand</c>)이 복도(서쪽)를 보고 서 있다. 움직이지 않는다.</item>
/// <item>오래 바라보면 — 조준점 <see cref="FinalCues.HallFigureTarget"/>을 Core <see cref="FixedMobStare"/>가 세어 배치 축이 오르고 끝내 붙잡힌다
/// (인체 모형 사망 컷신 <c>DeathCutscene_Illuminance</c>). 2.5초 이어 보면 우두둑 소리와 함께 목이 0.45초 만에 플레이어 쪽으로 꺾이고 계속 따라 본다
/// (64차 과학실 모형에서 옮겨 옴 — 과학실 모형은 이제 목이 꺾이지도, 응시로 붙잡지도 않는다).</item>
/// <item>S5 단서 창(슬롯)에서 긴장 디렉터가 이 모형을 대역으로 쓴다(<see cref="DirectionStage"/> — 지우지 않는다). 창이 닫혀도 그대로 서 있다.</item>
/// <item>재시작하면 목만 쉬는 자세로. 사망 컷신이 도는 동안은 숨는다(컷신의 모형과 겹치지 않게).</item>
/// </list>
/// 근무 씬에 자동으로 선다.
/// </summary>
[DisallowMultipleComponent]
public sealed class HallFigure : MonoBehaviour
{
    /// <summary>대역 ID.</summary>
    public const string StandInId = "mob.dummy.stand";

    /// <summary>이만큼 이어서 바라보면 목이 꺾인다(초).</summary>
    public const float NeckStareSeconds = 2.5f;

    /// <summary>목이 다 꺾이는 데 걸리는 시간(초).</summary>
    public const float NeckTurnSeconds = 0.45f;

    public const float NeckMaxYaw = 130f;
    public const float NeckMaxPitch = 35f;
    private const float NeckShare = 0.35f;

    /// <summary>목 꺾이는 소리(연출 소리표).</summary>
    public const string NeckSoundKey = "model.neck";

    private GameObject _figure;
    private object _program;
    private Transform _neck;
    private Transform _head;
    private Quaternion _neckRest = Quaternion.identity;
    private Quaternion _headRest = Quaternion.identity;
    private bool _neckTurned;
    private float _neckSince;
    private bool _debug;

    /// <summary>지금 근무 씬의 것. 없으면 null.</summary>
    public static HallFigure Active { get; private set; }

    /// <summary>세워 둔 모형(그 밤이 아니면 null).</summary>
    public GameObject Figure
    {
        get { return _figure; }
    }

    /// <summary>목이 꺾여 플레이어를 보고 있는지.</summary>
    public bool NeckTurned
    {
        get { return _neckTurned; }
    }

    /// <summary>이 모형인지(연출 실행기가 지우지 않게).</summary>
    public static bool Owns(GameObject go)
    {
        return go != null && Active != null && Active._figure == go;
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
        if (!FlowAutoInstall.IsDutyScene(scene) || FlowAutoInstall.Exists<HallFigure>(scene)) return;
        FlowAutoInstall.CreateHost<HallFigure>(scene, "HallFigure (auto)");
    }

    private void OnEnable()
    {
        Active = this;
        EventBus.NightRestarted += OnRestarted;
    }

    private void OnDisable()
    {
        EventBus.NightRestarted -= OnRestarted;
        Remove();
        if (Active == this) Active = null;
    }

    // ── 밤 따라가기 ──────────────────────────────────────────

    private void Update()
    {
        NightProgram program = NightRun.IsNightActive ? NightRun.Program : null;
        // 70차 ④(민: 「복도 끝 모형과 과학실 모형은 겹치면 안 돼. 과학실 모형이 복도에 나오고, 다음 날 급습해야 해」):
        // 복도 끝에 선 자의 밤에 나온 과학실 모형이 다음 날(급습 밤)에도 복도 끝에 그대로 서 있다가, 급습 대역이 서면 물러난다.
        bool rushNight = program != null && program.HasEncounter(ProgramCatalog.ModelRush);
        bool rushing = rushNight && RushOnStage();
        if (rushing) _rushedTonight = true;
        bool want = program != null && (program.HasEncounter(ProgramCatalog.HallEndFigure) || (rushNight && !_rushedTonight));
        if (!ReferenceEquals(program, _program))
        {
            _program = program;
            _debug = false;
            _rushedTonight = false;
            Remove();
            if (want) Place();
        }
        else if (want && _figure == null)
        {
            Place();
        }
        else if (!want && !_debug && _figure != null)
        {
            Remove();
        }

        if (_figure == null) return;
        bool show = DeathCutscene.Playing == null;   // 사망 컷신의 모형과 겹치지 않게
        if (_figure.activeSelf != show) _figure.SetActive(show);
    }

    private bool _rushedTonight;

    /// <summary>모형 급습 대역이 세워졌거나 대면 중인지(같은 자리에 둘이 겹치지 않게 — 급습이 이 모형을 이어받는다).</summary>
    private static bool RushOnStage()
    {
        DirectionStage stage = DirectionStage.Active;
        if (stage == null) return false;
        foreach (string id in stage.PresentedIds) if (id == ProgramCatalog.ModelRush) return true;
        foreach (string id in stage.StagedIds) if (id == ProgramCatalog.ModelRush) return true;
        return false;
    }

    private void Place()
    {
        Vector3 at = DirectionStage.FloorBelow(DirectionStage.RushHallSpot);
        _figure = StandInFactory.Create(StandInId, at, Quaternion.LookRotation(Vector3.left, Vector3.up), string.Empty);
        if (_figure == null) return;
        _figure.name = "복도 끝 인체 모형";
        StandInFactory.Dress(_figure, true, FinalCues.HallFigureTarget);
        FindBones();
        _neckTurned = false;
        if (DirectionStage.Verbose) Debug.Log("[HallFigure] 복도 끝에 인체 모형을 세움 @" + at.ToString("F1"));
    }

    private void Remove()
    {
        if (_figure != null) Destroy(_figure);
        _figure = null;
        _neck = null;
        _head = null;
        _neckTurned = false;
    }

    private void OnRestarted(RestartResult result)
    {
        ResetNeck();
        _rushedTonight = false;   // 재시작하면 급습도 다시 기다린다 — 모형도 다시 복도 끝에
    }

    // ── 오래 바라보면 목이 꺾인다(64차 과학실 모형에서 옮겨 옴) ──

    private void LateUpdate()
    {
        if (_figure == null || _head == null || !_figure.activeInHierarchy) return;
        Camera cam = Camera.main;
        if (cam == null) return;

        if (!_neckTurned && NightRun.IsNightActive)
        {
            FixedMobStare stare = NightRun.Stare;
            if (stare.TargetId == FinalCues.HallFigureTarget && stare.Seconds >= NeckStareSeconds)
            {
                _neckTurned = true;
                _neckSince = Time.time;
                PlayNeckSound();
                if (DirectionStage.Verbose) Debug.Log("[HallFigure] 오래 바라봐 목이 꺾였다");
            }
        }

        if (!_neckTurned) return;
        float w = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Time.time - _neckSince) / NeckTurnSeconds));

        // 쉬는 자세에서 시작해 플레이어(카메라) 쪽으로 — 모형 몸 기준 좌우·위아래로 잰다.
        Transform body = _figure.transform;
        if (_neck != null) _neck.localRotation = _neckRest;
        _head.localRotation = _headRest;
        Quaternion headRest = _head.rotation;
        Vector3 to = body.InverseTransformDirection((cam.transform.position - _head.position).normalized);
        float yaw = Mathf.Clamp(Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg, -NeckMaxYaw, NeckMaxYaw);
        float pitch = Mathf.Clamp(-Mathf.Asin(Mathf.Clamp(to.y, -1f, 1f)) * Mathf.Rad2Deg, -NeckMaxPitch, NeckMaxPitch);
        Quaternion turn = body.rotation * Quaternion.Euler(pitch * w, yaw * w, 0f) * Quaternion.Inverse(body.rotation);
        if (_neck != null) _neck.rotation = Quaternion.Slerp(Quaternion.identity, turn, NeckShare) * _neck.rotation;
        _head.rotation = turn * headRest;
    }

    private void ResetNeck()
    {
        _neckTurned = false;
        if (_neck != null) _neck.localRotation = _neckRest;
        if (_head != null) _head.localRotation = _headRest;
    }

    private void FindBones()
    {
        _neck = null;
        _head = null;
        foreach (Transform t in _figure.GetComponentsInChildren<Transform>(true))
        {
            string n = t.name.ToLowerInvariant();
            if (n == "neck" && _neck == null) _neck = t;
            else if (n == "head" && _head == null) _head = t;
        }

        if (_neck != null) _neckRest = _neck.localRotation;
        if (_head != null) _headRest = _head.localRotation;
    }

    private void PlayNeckSound()
    {
        float volume;
        AudioClip clip = DirectionSoundTableSO.FindExact(NeckSoundKey, out volume);
        if (clip == null) return;
        GameObject go = new GameObject("sfx 복도 끝 모형 목");
        go.transform.SetParent(_head, false);
        AudioSource s = go.AddComponent<AudioSource>();
        s.clip = clip;
        s.volume = volume;
        s.spatialBlend = 1f;
        s.minDistance = 2.5f;
        s.maxDistance = 20f;
        s.priority = 32;
        NightDutyMixer.Route(s, NightDutyMixer.Bus.Direction);
        s.Play();
        Destroy(go, clip.length + 0.2f);
    }

    // ── 디버그 ───────────────────────────────────────────────

    /// <summary>디버그: 편성과 무관하게 이 밤에 세운다(개발자 모드).</summary>
    public void DebugPlace()
    {
        Remove();
        Place();
        _debug = _figure != null;
    }
}

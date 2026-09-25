using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using NightDuty;

/// <summary>
/// 씬을 고치지 않고 <b>플레이 중에만</b> 판정·조우를 굴려 보기 위한 런타임 하네스입니다. 화면 왼쪽에 상태판이 뜹니다(F3).
/// </summary>
/// <remarks>
/// <b>버릴 실험용 코드</b>입니다. 개인 폴더(Assembly-CSharp)에 있습니다. <c>NightRunDebugPanel</c>과 같은 성격입니다.
///
/// 하는 일(단계마다 독립적이고, 하나가 터져도 나머지는 계속 돕니다)
/// - ① 센서 부착: FPController에 PlayerSensors·FlashlightRelay를 붙이고, 씬에 켜진 채 저장된 손전등을 끕니다.
/// - ② 문 발신기: 판정 대상인 문 8개에 DoorRelay를 붙입니다(경로가 아니라 JudgeTarget ID로 찾습니다).
/// - ③ 빠진 판정 대상: 카드가 요구하는데 씬에 없는 ID를 임시 표식으로 만들어 그 공간의 존 안에 흩뜨립니다.
/// - ④ 콜라이더 보충: 표식은 있는데 레이가 맞힐 콜라이더가 없는 대상에 작은 상자를 붙입니다.
/// - ⑤ 조우 대상: 조우 표가 요구하는 27종 지점을 만들고, NightRun.Encounter에 시야·배치 통로를 꽂습니다.
/// - ⑥ 게임 시계: GameTime의 endHour·timeMultiplier를 런타임 값으로만 바꿉니다(리플렉션).
/// - ⑦ 응시로 대신 보내기: <b>아직 발신기가 없는 신호 둘</b>을 응시로 흉내 냅니다(아래).
///
/// <b>⑦이 메우는 구멍</b>
/// - <c>ModelObserved</c>를 보내는 발신기가 프로젝트에 <b>없습니다</b>(GazeProbe 주석의 「앞으로 만들」).
///   그래서 조우는 모형이 다가오기만 하고 <b>관찰이 영영 성립하지 않아</b> 매일 이월되고, S5·T3도 안 열립니다.
/// - <c>DoorAutoOpenObserved</c>는 문이 스스로 움직여야 나가는데, 그 문을 여는 연출도 애니메이션도 없습니다.
///   그래서 H1·T1은 덱에 들어와도 미판정으로 끝납니다.
///
/// 둘 다 <b>1초 응시</b>로 대신합니다. 실제 게임의 조작이 아니라 <b>시험용 대역</b>이며,
/// 진짜 발신기가 생기면 이 단계를 끄십시오(<c>fakeMissingSignals</c>).
///
/// <b>씬 파일을 더럽히지 않는 것이 이 하네스의 첫째 규칙입니다.</b>
/// - 새로 만드는 GameObject는 <b>hideFlags를 건드리지 않습니다</b>. 런타임 생성물은 플레이를 끄면 씬과 함께 사라집니다.
///   <c>HideFlags.DontSave</c>를 붙이면 <b>정반대</b>가 됩니다 — 씬이 내려갈 때 파괴되지 않고 에디터 메모리에 남아
///   플레이할 때마다 쌓입니다(2026-09-22 실측: 두 번 플레이에 잔재 80개).
/// - 기존 오브젝트에 컴포넌트를 붙이는 일은 <c>Start</c>(플레이 중)에서만 합니다. 에디트 모드에서는 아무 일도 하지 않습니다.
/// - 기존 오브젝트의 <b>직렬화 값을 바꾸지 않습니다</b>. 인스펙터에 보이는 private 필드는 리플렉션으로 런타임 값만 덮습니다.
/// - <c>UnityEditor</c> 네임스페이스를 쓰지 않습니다(런타임 코드).
///
/// 상태판은 <b>읽기 전용</b>입니다. 버튼을 두지 않습니다 —
/// NightRunDebugPanel의 시나리오 버튼이 진행 중인 실제 밤을 날려 버린 사고가 이미 있었습니다.
/// </remarks>
[AddComponentMenu("NightDuty/Debug/NightDuty Test Harness")]
[DisallowMultipleComponent]
[DefaultExecutionOrder(-1000)]
public sealed class NightDutyTestHarness : MonoBehaviour
{
    private const float PanelWidth = 430f;

    /// <summary>임시 표식의 콜라이더 한 변(m). 기획 수치가 아니라 「레이가 맞힐 수 있는 가장 작은 크기」다.</summary>
    private const float MarkerSize = 0.4f;

    /// <summary>근무 종료 시각. 기획서 B절 「04:00 무조건 자동 종료」.</summary>
    private const int EndHour = 4;

    /// <summary>
    /// 대역 신호를 보내기까지 필요한 연속 응시 시간(초). 기획서의 모형 관찰 시간과 같은 1초다 —
    /// S1의 <c>GazeCondition(1f)</c>과 맞춰 두면 「한 번 보면 되는구나」가 한 가지 감각으로 남는다.
    /// </summary>
    private const float ObserveSeconds = 1f;

    /// <summary>존 상자 안에 표식을 흩뜨릴 격자. 한 공간에 6x6=36개까지 서로 겹치지 않는다.</summary>
    private const int GridCols = 6;
    private const int GridRows = 6;

    /// <summary>판정 대상인 문 8개. <b>경로가 아니라 ID로 찾는다</b> — 경로는 오브젝트를 옮기는 순간 깨진다.</summary>
    private static readonly string[] DoorIds =
    {
        "corridor.door.auto", "corridor.door.back", "corridor.door.11", "corridor.door.13",
        "science.door", "toilet.door", "toilet.stall.outer", "toilet.stall.inner"
    };

    /// <summary>연출 자동 개방을 보고해야 하는 문(H1 복도 자동문 · T1 입구 쪽 칸). 나머지는 꺼 둔다.</summary>
    private static readonly string[] AutoOpenDoorIds = { "corridor.door.auto", "toilet.stall.outer" };

    /// <summary>
    /// <see cref="SpaceZones"/>가 상자로 처리하는 구역 대상. <b>레이캐스트를 쓰지 않으므로 콜라이더를 붙이면 안 된다</b> —
    /// 통행 구역 한가운데에 상자를 세우면 플레이어를 막거나 응시 레이를 가로챈다.
    /// </summary>
    private static readonly string[] ZoneOnlyIds =
    {
        "corridor.passage", "toilet.stall.outer.inside", "toilet.stall.inner.inside", "science.zone.glass"
    };

    private static readonly string[] AxisNames = { "청각", "조도", "배치", "신뢰" };

    private static NightDutyTestHarness s_instance;

    [Header("무엇을 할까 (전부 플레이 중에만)")]
    [Tooltip("① FPController에 PlayerSensors·FlashlightRelay를 붙이고 씬 손전등을 끈다.")]
    [SerializeField] private bool wireSensors = true;

    [Tooltip("⑦ 발신기가 없는 신호(ModelObserved · DoorAutoOpenObserved)를 1초 응시로 대신 보낸다.")]
    [SerializeField] private bool fakeMissingSignals = true;

    [Tooltip("⑧ 잠긴 문도 열 수 있게 한다(열쇠 연출 대기). 플레이 중 F4로 바꿀 수 있다.")]
    [SerializeField] private bool ignoreDoorLocks = true;

    [Tooltip("⑨ 문 개폐 정책 표를 무시하고 씬의 문을 전부 연다. 둘러볼 때만. 플레이 중 F5.")]
    [SerializeField] private bool openEveryDoor;

    [Tooltip("② 판정 대상인 문 8개에 DoorRelay를 붙인다.")]
    [SerializeField] private bool wireDoors = true;

    [Tooltip("③ 카드가 요구하는데 씬에 없는 판정 대상을 임시로 만든다.")]
    [SerializeField] private bool fillMissingTargets = true;

    [Tooltip("④ 콜라이더가 없어 레이가 못 맞히는 판정 대상에 작은 상자를 붙인다.")]
    [SerializeField] private bool ensureColliders = true;

    [Tooltip("⑤ 조우 대상 27종을 만들고 NightRun.Encounter에 시야·배치 통로를 꽂는다.")]
    [SerializeField] private bool wireEncounter = true;

    [Header("게임 시계 (런타임 값만 바꾼다)")]
    [Tooltip("⑥ 켜면 GameTime의 endHour를 4로, 배속을 아래 값으로 덮는다. 씬 파일은 바뀌지 않는다.")]
    [SerializeField] private bool fixClock = true;

    [Tooltip("게임 시계 배속. 재설계 배속은 30~34다.")]
    [SerializeField, Min(1f)] private float timeMultiplier = 32f;

    [Header("화면")]
    [Tooltip("개발용 상태판을 보일지. 플레이 중 F3으로 바꿀 수 있다. 기본은 꺼짐 — 지침은 F1 태블릿에서 봅니다.")]
    [SerializeField] private bool _show;

    // 조우 연결
    private EncounterTableSO _encounterTable;
    private EncounterDirector _hookedDirector;
    private readonly Dictionary<string, string> _lastPlaced = new Dictionary<string, string>();   // 장면 ID → 지금 모형이 선 대상 ID
    private readonly Dictionary<string, GameObject> _models = new Dictionary<string, GameObject>(); // 장면 ID → 시각용 큐브
    private readonly Plane[] _planes = new Plane[6];   // 프러스텀 평면은 매 호출 새로 만들지 않는다(신호마다 불린다)

    // 존 상자 (SpaceZones.zones를 리플렉션으로 읽어 둔 것)
    private readonly Dictionary<SpaceId, Bounds> _zoneBox = new Dictionary<SpaceId, Bounds>();
    private readonly Dictionary<SpaceId, int> _fill = new Dictionary<SpaceId, int>();   // 공간마다 다음에 쓸 격자 칸
    private bool _zonesRead;

    /// <summary>준비를 두 번 하지 않는다. 씬에 붙여 둔 채로 런타임 생성 경로가 겹칠 때를 막는다.</summary>
    private bool _prepared;

    // ⑦ 응시 대역
    private PlayerSensors _sensors;
    private readonly Dictionary<string, DoorRelay> _autoDoors = new Dictionary<string, DoorRelay>();
    private string _gazedId = string.Empty;
    private float _gazedSeconds;
    private readonly HashSet<string> _firedTonight = new HashSet<string>();   // 이번 밤에 이미 대역을 보낸 키
    private int _firedDay = -1;
    private string _lastFake = string.Empty;

    private Camera _camera;

    // 하네스가 만든 것 세기
    private int _attached;
    private int _doorsWired;
    private int _created;
    private int _collidersAdded;
    private int _flashlightsOff;

    // 한 번만 남길 경고 (필드 이름이 바뀌어도 하네스가 터지거나 로그를 도배하면 안 된다)
    private readonly HashSet<string> _warned = new HashSet<string>();

    private readonly List<string> _notes = new List<string>();

    private Vector2 _scroll;
    private GUIStyle _small;
    private GUIStyle _bold;

    // ─────────────────────────────── 자동 생성 ───────────────────────────────

    /// <summary>
    /// 플레이 중에만 하네스를 만든다. <b>씬에 컴포넌트를 저장할 필요가 없게 하려는 것</b>이 목적이다.
    /// 씬에 직접 붙여 둔 경우(s_instance가 이미 있음)에는 만들지 않는다.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (s_instance != null)
        {
            return;
        }

        // 판정 씬의 표식은 SpaceZones다. 다른 씬(메뉴·결과창)에서는 아무것도 하지 않는다.
        if (FindAnyObjectByType<SpaceZones>() == null)
        {
            return;
        }

        // hideFlags를 붙이지 않는다 — DontSave는 플레이를 꺼도 오브젝트를 남긴다(주석 머리말 참고).
        GameObject go = new GameObject("__NightDutyHarness (runtime)");
        go.AddComponent<NightDutyTestHarness>();
    }

    /// <summary>
    /// <b>준비는 Awake에서 한다.</b> <c>NightRunDriver.Start</c>가 밤을 여는 순간 판정이 대상 참조를 검사하므로,
    /// 그 전에 표식이 다 서 있어야 한다. Start에 두면 실행 순서 하나에 기대게 되고, 실제로
    /// 「[RuleBook] 대상 참조 누락」 경고가 났다(2026-09-22 실측).
    /// <para><c>AutoCreate</c>는 <c>AfterSceneLoad</c>에서 돈다 — 씬 오브젝트의 Awake 뒤, <b>첫 Start 앞</b>이다.
    /// 그래서 런타임 생성이든 씬에 붙여 둔 것이든 이 Awake는 어떤 Start보다 앞선다.</para>
    /// </summary>
    private void Awake()
    {
        s_instance = this;
        Prepare();
    }

    private void OnDestroy()
    {
        if (s_instance == this)
        {
            s_instance = null;
        }
    }

    // ─────────────────────────────── 단계 실행 ───────────────────────────────

    private void Prepare()
    {
        // 에디트 모드(프리팹 미리보기 등)에서는 아무것도 붙이지 않는다. 씬이 더러워지는 유일한 경로를 막는다.
        if (!Application.isPlaying || _prepared)
        {
            return;
        }

        _prepared = true;

        // 에디터가 초점을 잃으면 플레이 루프가 통째로 멈춘다(Run In Background 꺼짐).
        // 알트탭 한 번에 게임이 얼어붙어 「문이 안 열린다」로 보였다(2026-09-22 실측: 3분 동안 frame=2).
        // 런타임 값만 덮는다 — 프로젝트 설정은 건드리지 않는다.
        Application.runInBackground = true;

        // 열쇠 연출이 아직 없어 잠긴 문 12개가 회차를 막는다. 시험 동안만 무시한다(F4로 끌 수 있다).
        PlayerInteractor.IgnoreLocks = ignoreDoorLocks;
        DoorPolicySO.OpenEverythingOverride = openEveryDoor;

        ReadZones();

        Step("① 센서 부착", WireSensors);
        Step("② 문 발신기", WireDoors);
        Step("③ 빠진 판정 대상", FillMissingTargets);
        Step("④ 콜라이더 보충", EnsureColliders);
        Step("⑤ 조우 대상", WireEncounter);
        Step("⑥ 게임 시계", TuneClock);

        Debug.Log("[하네스] 준비 완료 — 부착 " + _attached + " · 문 " + _doorsWired + " · 만든 대상 " + _created +
                  " · 콜라이더 " + _collidersAdded, this);
    }

    /// <summary>한 단계. 예외가 나도 다음 단계는 그대로 돈다 — 씬이 덜 갖춰진 상태에서도 나머지를 시험할 수 있어야 한다.</summary>
    private void Step(string name, Action body)
    {
        try
        {
            body();
        }
        catch (Exception e)
        {
            Warn(name + " 단계에서 예외가 났습니다(나머지 단계는 계속합니다).");
            Debug.LogException(e, this);
        }
    }

    private void Update()
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        // NightRunDebugPanel이 F2를 쓰므로 여기는 F3이다.
        if (Input.GetKeyDown(KeyCode.F3)) _show = !_show;

        // 잠긴 문을 무시할지. 열쇠 연출이 생기면 이 줄과 ignoreDoorLocks를 같이 지운다.
        if (Input.GetKeyDown(KeyCode.F4))
        {
            ignoreDoorLocks = !ignoreDoorLocks;
            PlayerInteractor.IgnoreLocks = ignoreDoorLocks;
        DoorPolicySO.OpenEverythingOverride = openEveryDoor;
            Note("[대역] 잠긴 문 무시 " + (ignoreDoorLocks ? "켬" : "끔"));
        }

        // 기획에 없는 문까지 전부 연다. 씬을 둘러볼 때만 쓰십시오.
        if (Input.GetKeyDown(KeyCode.F5))
        {
            openEveryDoor = !openEveryDoor;
            DoorPolicySO.OpenEverythingOverride = openEveryDoor;
            Note("[대역] 모든 문 열기 " + (openEveryDoor ? "켬" : "끔"));
        }
#endif

        HookEncounter();
        FakeMissingSignals();
    }

    // ─────────────────────────── ⑦ 없는 발신기를 응시로 대신 ───────────────────────────

    /// <summary>
    /// 지금 보고 있는 대상을 <see cref="ObserveSeconds"/>만큼 붙들고 있으면, 그 자리에 걸맞은
    /// <b>아직 아무도 안 보내는 신호</b>를 대신 보낸다.
    /// <para>
    /// <b>조우 지점</b>(모형이 실제로 서 있는 자리) → <c>ModelObserved(장면 ID)</c>.
    /// 장면 ID가 곧 S5·T3의 트리거 ID이기도 해서 두 카드도 같이 열린다.
    /// </para>
    /// <para>
    /// <b>자동 개방 문</b>(H1 복도 자동문 · T1 입구 쪽 칸) → 연출 개방을 흉내 낸다.
    /// <c>BeginDirectionMove</c>로 출처를 Direction으로 못 박은 뒤 <c>ReportMoveStarted</c>를 부르면,
    /// <c>DoorRelay</c>가 <b>그 문을 보고 있는지 스스로 확인하고</b> <c>DoorAutoOpenObserved</c>를 보낸다.
    /// 응시가 조건이라 대역을 응시로 거는 것이 자연스럽다.
    /// </para>
    /// <para><b>밤마다 한 번씩</b>이다. 같은 자리를 계속 보고 있다고 신호가 쏟아지면 시험이 아니라 소음이 된다.</para>
    /// </summary>
    private void FakeMissingSignals()
    {
        if (!fakeMissingSignals || _sensors == null || !NightRun.IsNightActive)
        {
            return;
        }

        // 일차가 바뀌면 이번 밤 기록을 비운다.
        if (_firedDay != NightRun.Day)
        {
            _firedDay = NightRun.Day;
            _firedTonight.Clear();
        }

        string id = _sensors.Gaze.CurrentId;
        if (string.IsNullOrEmpty(id))
        {
            _gazedId = string.Empty;
            _gazedSeconds = 0f;
            return;
        }

        if (id != _gazedId)
        {
            _gazedId = id;
            _gazedSeconds = 0f;
        }

        _gazedSeconds += Time.deltaTime;
        if (_gazedSeconds < ObserveSeconds || _firedTonight.Contains(id))
        {
            return;
        }

        _firedTonight.Add(id);

        string sceneId = SceneAtTarget(id);
        if (sceneId.Length > 0)
        {
            NightRun.Send(JudgeSignal.Target(SignalKind.ModelObserved, sceneId));
            _lastFake = "모형 관찰 → " + sceneId;
            Note("[대역] " + sceneId + "의 모형을 관찰한 것으로 보냅니다(" + id + ").");
            return;
        }

        DoorRelay door;
        if (_autoDoors.TryGetValue(id, out door) && door != null)
        {
            door.BeginDirectionMove();
            door.ReportMoveStarted(false, ActionSource.Direction);
            _lastFake = "자동 개방 → " + id;
            Note("[대역] " + id + "이(가) 스스로 열린 것으로 보냅니다.");
        }
    }

    /// <summary>그 대상 ID에 <b>지금</b> 모형이 서 있는 조우 장면. 없으면 빈 문자열.</summary>
    private string SceneAtTarget(string targetId)
    {
        foreach (KeyValuePair<string, string> pair in _lastPlaced)
        {
            if (pair.Value == targetId)
            {
                return pair.Key;
            }
        }

        return string.Empty;
    }

    // ─────────────────────────────── ① 센서 부착 ───────────────────────────────

    /// <summary>
    /// 플레이어에 <see cref="PlayerSensors"/>·<see cref="FlashlightRelay"/>를 붙이고, 씬에 없으면
    /// <see cref="AnomalyCueDirector"/>도 만든다. 전부 런타임 부착이라 씬 파일은 그대로다.
    /// </summary>
    private void WireSensors()
    {
        if (!wireSensors)
        {
            return;
        }

        GameObject player = FindPlayer();
        if (player == null)
        {
            Warn("플레이어(FPController)를 찾지 못해 센서를 붙이지 못했습니다.");
            return;
        }

        Camera cam = player.GetComponentInChildren<Camera>(true);
        if (cam == null)
        {
            cam = Camera.main;
        }

        _camera = cam;

        PlayerSensors sensors = player.GetComponent<PlayerSensors>();
        if (sensors == null)
        {
            sensors = player.AddComponent<PlayerSensors>();
            _attached++;
        }

        // gazeCamera·playerRoot는 private [SerializeField]다. 인스펙터 값을 건드리지 않고 런타임 값만 채운다.
        SetPrivate(sensors, "gazeCamera", cam);
        SetPrivate(sensors, "playerRoot", cam != null ? cam.transform.root : player.transform);

        _sensors = sensors;   // ⑦이 매 프레임 Gaze.CurrentId를 읽는다.

        FlashlightRelay relay = player.GetComponentInChildren<FlashlightRelay>(true);
        if (relay == null)
        {
            relay = player.AddComponent<FlashlightRelay>();
            _attached++;
        }

        GameObject lamp = FindChildByNameContains(player.transform, "Flashlight_ON");
        SetPrivate(relay, "flashlightRoot", lamp);

        // 경비실 출발은 Off다. 밤 시작 직후 FlashlightRelay가 현재 상태를 1회 보내므로 코어와 화면이 어긋나지 않는다.
        SetPrivate(relay, "startOn", false);

        // AddComponent 순간 OnEnable이 이미 돌아서 flashlightRoot가 빈 채로 라이트를 캐싱했다.
        // 껐다 켜서 방금 넣은 값으로 다시 한 번 OnEnable을 돌린다.
        relay.enabled = false;
        relay.enabled = true;

        TurnOffSavedFlashlights();

        AnomalyCueDirector cue = AnomalyCueDirector.Active;
        if (cue == null)
        {
            cue = FindAnyObjectByType<AnomalyCueDirector>();
        }

        if (cue == null)
        {
            // 표 슬롯은 비워 둔다 — AnomalyCueDirector가 비어 있으면 Resources에서 스스로 찾는다.
            GameObject go = NewRuntimeObject("__harness_CueDirector", Vector3.zero);
            go.AddComponent<AnomalyCueDirector>();
            _attached++;
            Note("AnomalyCueDirector가 씬에 없어 런타임 오브젝트로 만들었습니다.");
        }
    }

    /// <summary>
    /// 씬에 <b>켜진 채로 저장된</b> 손전등을 전부 끈다(FPController의 1인칭 손전등, Interior/Toilet02/Flashlight_ON 등).
    /// 경로는 깨지기 쉬우므로 이름에 <c>Flashlight_ON</c>이 들어간 것을 전부 찾는다.
    /// <para>끄지 않으면 코어는 Off로 시작하는데 화면만 밝아, H3는 통과하고 C5는 실패하는 유령 상태가 된다.</para>
    /// </summary>
    private void TurnOffSavedFlashlights()
    {
        List<Transform> all = AllTransforms();
        for (int i = 0; i < all.Count; i++)
        {
            Transform t = all[i];
            if (t == null || !t.gameObject.activeSelf)
            {
                continue;
            }

            if (t.name.IndexOf("Flashlight_ON", StringComparison.OrdinalIgnoreCase) < 0)
            {
                continue;
            }

            // SetActive는 런타임 상태만 바꾼다(씬 파일의 직렬화 값은 그대로다).
            t.gameObject.SetActive(false);
            _flashlightsOff++;
        }

        if (_flashlightsOff > 0)
        {
            Note("켜진 채 저장된 손전등 " + _flashlightsOff + "개를 런타임에 껐습니다.");
        }
    }

    // ─────────────────────────────── ② 문 발신기 ───────────────────────────────

    /// <summary>
    /// 판정 대상인 문 8개에 <see cref="DoorRelay"/>를 붙인다.
    /// <para><b>경로가 아니라 ID로 찾는다.</b> 벤더 문은 오브젝트 이름이 <c>DoorNarrowSolid (8)</c>처럼 번호에 의존해서
    /// 레벨을 손보는 순간 경로가 통째로 어긋난다. <see cref="JudgeTarget"/> ID는 카드와 글자까지 같으니 그쪽이 기준이다.</para>
    /// <para>발신기는 <b>Animation을 가진 오브젝트</b>에 붙인다. 표식이 자식 메시에 있어도
    /// <c>DoorRelay</c>가 자식·부모에서 표식을 다시 찾으므로 ID는 잃지 않는다.</para>
    /// </summary>
    private void WireDoors()
    {
        if (!wireDoors)
        {
            return;
        }

        List<JudgeTarget> targets = AllJudgeTargets();
        for (int i = 0; i < targets.Count; i++)
        {
            JudgeTarget target = targets[i];
            if (target == null)
            {
                continue;
            }

            string matched = MatchedId(target, DoorIds);
            if (matched.Length == 0)
            {
                continue;
            }

            Transform host = AnimationHost(target.transform);
            if (host.GetComponent<DoorRelay>() != null)
            {
                continue;   // 이미 붙어 있다(씬에 저장돼 있거나 앞 표식에서 붙였다).
            }

            DoorRelay relay = host.gameObject.AddComponent<DoorRelay>();
            _attached++;
            _doorsWired++;

            // 표식이 여럿 달린 문(문 ID + 응시 ID)에서 엉뚱한 ID가 잡히지 않게 못을 박는다.
            SetPrivate(relay, "doorIdOverride", matched);

            // H1(복도 자동문)·T1(입구 쪽 칸)만 자동 개방을 보고한다. 나머지가 보고하면 없는 연출을 본 것이 된다.
            bool reportsAutoOpen = Contains(AutoOpenDoorIds, matched);
            SetPrivate(relay, "reportAutoOpenObserved", reportsAutoOpen);

            if (reportsAutoOpen)
            {
                _autoDoors[matched] = relay;   // ⑦이 응시로 연출 개방을 흉내 낸다.
            }

            // OnEnable이 AddComponent 때 이미 돌았으므로 방금 넣은 ID로 다시 한 번 돌린다.
            relay.enabled = false;
            relay.enabled = true;
        }

        if (_doorsWired < DoorIds.Length)
        {
            Warn("문 " + _doorsWired + "/" + DoorIds.Length + "개에만 DoorRelay를 붙였습니다(나머지는 표식을 못 찾았거나 이미 붙어 있음).");
        }
    }

    /// <summary>표식에서 위로 올라가며 <see cref="Animation"/>을 가진 오브젝트를 찾는다. 없으면 표식 자신.</summary>
    private static Transform AnimationHost(Transform from)
    {
        Transform t = from;
        while (t != null)
        {
            if (t.GetComponent<Animation>() != null)
            {
                return t;
            }

            t = t.parent;
        }

        return from;
    }

    // ─────────────────────────────── ③ 빠진 판정 대상 ───────────────────────────────

    /// <summary>
    /// 카드가 요구하는데 씬에 없는 판정 대상을 임시 표식으로 만든다.
    /// <para><b>오늘 덱이 아니라 편성표의 모든 카드</b>를 읽는다 — 하루치만 채우면 다른 일차를 시험할 때 또 비어 버린다.</para>
    /// <para>위치는 그 카드의 <c>Space</c>에 해당하는 존 상자 안이다. 좌표를 하드코딩하면 레벨을 옮기는 순간 허공에 뜬다.</para>
    /// </summary>
    private void FillMissingTargets()
    {
        if (!fillMissingTargets)
        {
            return;
        }

        NightDeckTableSO table = Resources.Load<NightDeckTableSO>(NightDeckTableSO.ResourcePath);
        if (table == null)
        {
            Warn("Resources/" + NightDeckTableSO.ResourcePath + " 편성표가 없어 빠진 대상을 채우지 못했습니다.");
            return;
        }

        // ID → 그 ID를 요구한 카드의 공간. 같은 ID를 여러 카드가 쓰면 먼저 만난 쪽을 쓴다(공간은 같다).
        Dictionary<string, SpaceId> want = new Dictionary<string, SpaceId>();
        List<string> required = new List<string>();
        for (int day = 1; day <= table.DayCount; day++)
        {
            IReadOnlyList<RuleSO> deck = table.DeckFor(day);
            if (deck == null)
            {
                continue;
            }

            for (int i = 0; i < deck.Count; i++)
            {
                RuleSO card = deck[i];
                if (card == null)
                {
                    continue;
                }

                // TargetIds만 보면 모자란다 — 시작 신호의 대상(S2 science.glass.break)과
                // 성공·실패·취소 조건이 가리키는 ID가 빠진다. RuleBook이 검사하는 것과 같은 목록을 써야
                // 「대상 참조 누락」 경고가 남지 않는다.
                required.Clear();
                card.CollectReferences(required);
                for (int k = 0; k < required.Count; k++)
                {
                    string id = required[k] != null ? required[k].Trim() : string.Empty;
                    if (id.Length == 0 || id == TargetMatchIds.Any || id == TargetMatchIds.Trigger)
                    {
                        continue;   // 「아무거나」·「시작 대상」은 실제 오브젝트가 아니다.
                    }

                    if (!want.ContainsKey(id))
                    {
                        want[id] = card.Space;
                    }
                }
            }
        }

        HashSet<string> present = SceneTargetIds();
        foreach (KeyValuePair<string, SpaceId> pair in want)
        {
            if (present.Contains(pair.Key))
            {
                continue;
            }

            if (MakeTarget(pair.Key, pair.Value))
            {
                present.Add(pair.Key);
            }
        }
    }

    // ─────────────────────────────── ④ 콜라이더 보충 ───────────────────────────────

    /// <summary>
    /// 표식은 있는데 자기·자식에 콜라이더가 하나도 없어 응시 레이가 못 맞히는 대상에 작은 상자를 붙인다.
    /// <para><b>구역 ID는 건너뛴다.</b> <see cref="SpaceZones"/>가 상자 안팎으로 판정하므로 레이캐스트를 쓰지 않고,
    /// 통행 구역 한가운데에 콜라이더를 세우면 플레이어를 막거나 응시를 가로챈다.</para>
    /// <para>트리거로 만들지 않는 이유: <c>GazeProbe</c>가 <c>QueryTriggerInteraction.Ignore</c>로 쏘기 때문에
    /// 트리거 콜라이더는 영원히 맞지 않는다.</para>
    /// </summary>
    private void EnsureColliders()
    {
        if (!ensureColliders)
        {
            return;
        }

        List<JudgeTarget> targets = AllJudgeTargets();
        for (int i = 0; i < targets.Count; i++)
        {
            JudgeTarget target = targets[i];
            if (target == null)
            {
                continue;
            }

            if (MatchedId(target, ZoneOnlyIds).Length > 0)
            {
                continue;   // 구역은 레이캐스트를 쓰지 않는다.
            }

            if (target.GetComponentInChildren<Collider>(true) != null)
            {
                continue;
            }

            BoxCollider box = target.gameObject.AddComponent<BoxCollider>();
            box.center = Vector3.zero;
            box.size = new Vector3(MarkerSize, MarkerSize, MarkerSize);
            _collidersAdded++;
        }

        if (_collidersAdded > 0)
        {
            Note("콜라이더가 없던 판정 대상 " + _collidersAdded + "개에 " + MarkerSize + "m 상자를 붙였습니다.");
        }
    }

    // ─────────────────────────────── ⑤ 조우 대상 ───────────────────────────────

    /// <summary>
    /// 조우 표가 요구하는 접근 3지점·퇴실 게이트 지점을 만들고, <see cref="NightRun.Encounter"/>에 통로를 꽂을 준비를 한다.
    /// <para>지점이 하나라도 없으면 그 장면은 「더 다가오지 못하는」 조우가 된다(EncounterDirector가 뜻을 접는다).</para>
    /// </summary>
    private void WireEncounter()
    {
        if (!wireEncounter)
        {
            return;
        }

        _encounterTable = Resources.Load<EncounterTableSO>(EncounterTableSO.ResourcePath);
        if (_encounterTable == null)
        {
            Warn("Resources/" + EncounterTableSO.ResourcePath + " 조우 표가 없어 조우 지점을 만들지 못했습니다.");
            return;
        }

        // 지점 ID → 공간. 공간은 그 지점을 쓰는 장면의 Space다.
        Dictionary<string, SpaceId> spaceOf = new Dictionary<string, SpaceId>();
        IReadOnlyList<EncounterTableSO.Scene> scenes = _encounterTable.Scenes;
        for (int i = 0; i < scenes.Count; i++)
        {
            EncounterTableSO.Scene scene = scenes[i];
            if (scene == null)
            {
                continue;
            }

            for (int step = 1; step <= EncounterTableSO.ApproachCount; step++)
            {
                RememberSpace(spaceOf, scene.ApproachOf(step), scene.Space);
            }

            RememberSpace(spaceOf, scene.ExitGateTargetId, scene.Space);
        }

        List<string> ids = new List<string>();
        _encounterTable.CollectReferences(ids);

        HashSet<string> present = SceneTargetIds();
        for (int i = 0; i < ids.Count; i++)
        {
            string id = ids[i] != null ? ids[i].Trim() : string.Empty;
            if (id.Length == 0 || present.Contains(id))
            {
                continue;
            }

            SpaceId space;
            if (!spaceOf.TryGetValue(id, out space))
            {
                space = SpaceId.None;
            }

            if (MakeTarget(id, space))
            {
                present.Add(id);
            }
        }
    }

    private static void RememberSpace(Dictionary<string, SpaceId> map, string id, SpaceId space)
    {
        if (string.IsNullOrEmpty(id))
        {
            return;
        }

        string key = id.Trim();
        if (key.Length > 0 && !map.ContainsKey(key))
        {
            map[key] = space;
        }
    }

    /// <summary>
    /// <see cref="NightRun.Encounter"/>에 시야·배치 통로를 꽂는다.
    /// <para><b>매 프레임 확인하는 이유</b>: 연출기는 회차가 열려야 생기고(그 전에는 null), 새 회차마다 <b>새 인스턴스</b>다.
    /// 들고 있던 인스턴스와 다를 때만 다시 꽂으므로 한 인스턴스에 두 번 꽂지 않는다.</para>
    /// </summary>
    private void HookEncounter()
    {
        if (!wireEncounter)
        {
            return;
        }

        // 제품 구동기(EncounterStager)가 서 있으면 그쪽이 정본이다. 둘이 같은 자리에 꽂으면
        // 나중에 꽂은 쪽이 이기는데, 어느 쪽이 나중인지는 프레임 순서에 달려 있어 재현이 안 된다.
        if (FindAnyObjectByType<EncounterStager>() != null)
        {
            return;
        }

        EncounterDirector director = NightRun.Encounter;
        if (director == null || director == _hookedDirector)
        {
            return;
        }

        director.IsVisible = IsSceneVisible;
        director.PlaceModel = PlaceSceneModel;
        _hookedDirector = director;
        _lastPlaced.Clear();
        Note("조우 연출기에 시야·배치 통로를 연결했습니다.");
    }

    /// <summary>
    /// 그 장면의 모형이 지금 <b>보이는가</b>. 프러스텀(시야 안) + 라인캐스트(가림 없음) 둘 다 만족해야 true다.
    /// <para>판단할 마커가 없으면 false다 — 「보인다」로 답하면 모형이 영원히 멈춘다.</para>
    /// </summary>
    private bool IsSceneVisible(string sceneId)
    {
        Transform marker = CurrentMarker(sceneId);
        if (marker == null)
        {
            return false;
        }

        Camera cam = ResolveCamera();
        if (cam == null)
        {
            return false;
        }

        // 시야 안인가. 표식은 점이라 조금 부풀려서 본다.
        GeometryUtility.CalculateFrustumPlanes(cam, _planes);
        Bounds box = new Bounds(marker.position, new Vector3(0.6f, 1.8f, 0.6f));
        if (!GeometryUtility.TestPlanesAABB(_planes, box))
        {
            return false;
        }

        // 가려졌는가. 트리거는 「가시 충돌체」가 아니므로 무시한다(GazeProbe와 같은 기준).
        Vector3 eye = cam.transform.position;
        RaycastHit hit;
        if (Physics.Linecast(eye, marker.position, out hit, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.transform != marker && !hit.transform.IsChildOf(marker))
            {
                return false;   // 벽·문이 앞을 막았다.
            }
        }

        return true;
    }

    /// <summary>그 장면의 모형이 지금 서 있는 표식. 아직 아무것도 놓이지 않았으면 null.</summary>
    private Transform CurrentMarker(string sceneId)
    {
        string targetId;
        if (!_lastPlaced.TryGetValue(sceneId, out targetId) || string.IsNullOrEmpty(targetId))
        {
            // 아직 PlaceModel을 못 받았으면 연출기의 단계에서 되짚는다(하네스가 나중에 붙은 경우).
            targetId = string.Empty;
            EncounterTableSO.Scene scene = _encounterTable != null ? _encounterTable.Find(sceneId) : null;
            EncounterDirector director = NightRun.Encounter;
            if (scene != null && director != null)
            {
                int step = director.StepOf(sceneId);
                if (step > 0)
                {
                    targetId = scene.ApproachOf(step);
                }
                else if (director.IsExitGatePlaced(sceneId))
                {
                    targetId = scene.ExitGateTargetId;
                }
            }
        }

        if (string.IsNullOrEmpty(targetId))
        {
            return null;
        }

        return MarkerOf(targetId);
    }

    /// <summary>
    /// 모형을 그 지점으로 옮긴다. 실제 인체모형이 아직 없으므로 <b>시각용 큐브</b>를 쓴다.
    /// 큐브는 장면마다 하나다. 콜라이더는 떼어 낸다 — 응시 레이와 이동을 막으면 안 된다.
    /// </summary>
    private void PlaceSceneModel(string sceneId, string targetId)
    {
        _lastPlaced[sceneId] = targetId;

        GameObject model;
        if (!_models.TryGetValue(sceneId, out model) || model == null)
        {
            model = GameObject.CreatePrimitive(PrimitiveType.Cube);
            model.name = "__harness_model " + sceneId;
            Collider col = model.GetComponent<Collider>();
            if (col != null)
            {
                Destroy(col);
            }

            model.transform.localScale = new Vector3(0.5f, 1.7f, 0.5f);
            _models[sceneId] = model;
        }

        Transform marker = MarkerOf(targetId);
        if (marker != null)
        {
            model.transform.position = marker.position;
        }

        Debug.Log("[조우] " + sceneId + " → " + targetId, this);
    }

    // ─────────────────────────────── ⑥ 게임 시계 ───────────────────────────────

    /// <summary>
    /// <c>GameTime</c>의 <c>endHour</c>를 기획값 4로, 배속을 인스펙터 값으로 덮는다.
    /// <para><b>리플렉션으로 런타임 필드만 바꾼다</b> — 인스펙터에서 고치면 씬 파일이 바뀌고, 그것은 이 하네스의 금기다.
    /// <c>GameTime</c>은 다른 담당자의 폴더에 있어 타입을 직접 참조하지 않고 이름으로 찾는다.</para>
    /// </summary>
    private void TuneClock()
    {
        if (!fixClock)
        {
            return;
        }

        Component clock = FindByTypeName("GameTime");
        if (clock == null)
        {
            Warn("씬에서 GameTime을 찾지 못해 시계를 손대지 못했습니다.");
            return;
        }

        bool a = SetPrivate(clock, "endHour", EndHour);
        bool b = SetPrivate(clock, "timeMultiplier", timeMultiplier);

        // endHour만 바꾸면 소용없다 — GameTime은 Awake에서 endSeconds를 미리 계산해 두고
        // Update는 그 값만 본다. 그리고 GameTime의 Awake는 이 하네스보다 먼저 끝나 있다
        // (하네스는 AfterSceneLoad에서 생기므로 씬 오브젝트의 Awake 뒤다).
        // 그래서 계산된 값을 같은 규칙으로 다시 써 준다. 2026-09-22 실측:
        // 이 줄이 없을 때 하네스는 「종료 4시」라고 찍으면서 실제로는 6:00에 끝나고 있었다.
        bool c = RecomputeEndSeconds(clock);

        if (a || b)
        {
            Note("게임 시계(런타임) — 종료 " + (a && c ? EndHour + "시" : a ? "4시로 못 바꿈(endSeconds 실패)" : "그대로") +
                 " · 배속 " + (b ? timeMultiplier.ToString("0") : "그대로"));
        }
    }

    /// <summary><c>GameTime.endSeconds</c>를 <c>endHour</c>·<c>endMinute</c>에서 다시 계산한다. Awake의 식과 같아야 한다.</summary>
    private bool RecomputeEndSeconds(Component clock)
    {
        FieldInfo endSec = FindField(clock.GetType(), "endSeconds");
        FieldInfo startSec = FindField(clock.GetType(), "startSeconds");
        FieldInfo hour = FindField(clock.GetType(), "endHour");
        FieldInfo minute = FindField(clock.GetType(), "endMinute");
        if (endSec == null || startSec == null || hour == null || minute == null)
        {
            WarnOnce("GameTime.endSeconds");
            return false;
        }

        float start = System.Convert.ToSingle(startSec.GetValue(clock));
        float end = (System.Convert.ToInt32(hour.GetValue(clock)) * 60 + System.Convert.ToInt32(minute.GetValue(clock))) * 60f;
        if (end <= start)
        {
            end += 24f * 60f * 60f;   // 자정을 넘는 근무. Awake와 같은 처리다.
        }

        endSec.SetValue(clock, end);
        return true;
    }

    // ─────────────────────────────── 표식 만들기 ───────────────────────────────

    /// <summary>
    /// 임시 판정 대상 하나를 만든다. 위치는 그 공간의 존 상자 안 격자 칸이라 같은 공간의 표식끼리 겹치지 않는다.
    /// 존이 없으면 만들지 않는다 — 허공(0,0,0)에 세우면 근접·응시가 전부 거짓 판정이 된다.
    /// </summary>
    private bool MakeTarget(string id, SpaceId space)
    {
        Vector3 spot;
        if (!NextSpot(space, out spot))
        {
            Warn("존을 몰라 " + id + "를 만들지 못했습니다(공간 " + SpaceName(space) + ").");
            return false;
        }

        GameObject go = NewRuntimeObject("__harness_target " + id, spot);

        BoxCollider box = go.AddComponent<BoxCollider>();
        box.center = Vector3.zero;
        box.size = new Vector3(MarkerSize, MarkerSize, MarkerSize);

        JudgeTarget target = go.AddComponent<JudgeTarget>();
        target.SetIds(id);   // AddComponent 때는 ID가 비어 있으므로 여기서 등록된다.

        _created++;
        return true;
    }

    /// <summary>그 공간의 다음 격자 칸. 존 상자를 (칸+1)로 나눠 가장자리에 붙지 않게 안쪽에만 놓는다.</summary>
    private bool NextSpot(SpaceId space, out Vector3 spot)
    {
        spot = Vector3.zero;

        Bounds box;
        if (space == SpaceId.None || !_zoneBox.TryGetValue(space, out box))
        {
            return false;
        }

        int index;
        if (!_fill.TryGetValue(space, out index))
        {
            index = 0;
        }

        _fill[space] = index + 1;

        int cx = index % GridCols;
        int cz = (index / GridCols) % GridRows;
        float stepX = box.size.x / (GridCols + 1);
        float stepZ = box.size.z / (GridRows + 1);

        // y는 상자 가운데 높이. 바닥에 박히거나 천장을 뚫지 않는 무난한 높이다.
        spot = new Vector3(box.min.x + stepX * (cx + 1), box.center.y, box.min.z + stepZ * (cz + 1));
        return true;
    }

    /// <summary><see cref="SpaceZones"/>의 존 상자를 리플렉션으로 읽는다(<c>zones</c>는 private, <c>Zone</c>은 public struct).</summary>
    private void ReadZones()
    {
        if (_zonesRead)
        {
            return;
        }

        _zonesRead = true;

        SpaceZones zones = FindAnyObjectByType<SpaceZones>();
        if (zones == null)
        {
            Warn("씬에 SpaceZones가 없어 존 상자를 읽지 못했습니다. 임시 대상은 만들지 않습니다.");
            return;
        }

        FieldInfo field = FindField(zones.GetType(), "zones");
        if (field == null)
        {
            WarnOnce("SpaceZones.zones");
            return;
        }

        SpaceZones.Zone[] list = field.GetValue(zones) as SpaceZones.Zone[];
        if (list == null)
        {
            return;
        }

        for (int i = 0; i < list.Length; i++)
        {
            SpaceZones.Zone zone = list[i];
            if (zone.OutOfScope || zone.Space == SpaceId.None)
            {
                continue;   // 스코프 밖(2층·경비실)에는 판정 대상을 두지 않는다.
            }

            if (!_zoneBox.ContainsKey(zone.Space))
            {
                _zoneBox[zone.Space] = zone.Box;
            }
        }
    }

    private GameObject NewRuntimeObject(string name, Vector3 position)
    {
        GameObject go = new GameObject(name);

        // 플레이를 끄면 사라져야 한다. 그래서 hideFlags를 <b>건드리지 않는다</b> —
        // 런타임 생성물은 씬이 내려갈 때 함께 파괴되고, 플레이 중에는 씬 저장 자체가 막혀 있다.
        // 여기에 DontSave를 붙이면 파괴를 면해 에디터 메모리에 계속 쌓인다.
        go.transform.position = position;
        return go;
    }

    // ─────────────────────────────── 도우미 ───────────────────────────────

    private GameObject FindPlayer()
    {
        GameObject byName = GameObject.Find("FPController");
        if (byName != null)
        {
            return byName;
        }

        FPController fp = FindAnyObjectByType<FPController>();
        if (fp != null)
        {
            return fp.gameObject;
        }

        Camera main = Camera.main;
        return main != null ? main.transform.root.gameObject : null;
    }

    private Camera ResolveCamera()
    {
        if (_camera != null)
        {
            return _camera;
        }

        _camera = Camera.main;
        return _camera;
    }

    /// <summary>ID로 씬의 표식을 찾는다. 등록부는 하네스가 만든 표식도 함께 들고 있다.</summary>
    private static Transform MarkerOf(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return null;
        }

        JudgeTarget target;
        if (!JudgeTargetRegistry.TryGet(id.Trim(), out target) || target == null)
        {
            return null;
        }

        return target.transform;
    }

    /// <summary>로드된 씬의 모든 Transform(꺼진 것 포함).</summary>
    private static List<Transform> AllTransforms()
    {
        List<Transform> list = new List<Transform>();
        for (int s = 0; s < SceneManager.sceneCount; s++)
        {
            UnityEngine.SceneManagement.Scene scene = SceneManager.GetSceneAt(s);
            if (!scene.isLoaded)
            {
                continue;
            }

            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i] != null)
                {
                    list.AddRange(roots[i].GetComponentsInChildren<Transform>(true));
                }
            }
        }

        return list;
    }

    /// <summary>씬의 모든 <see cref="JudgeTarget"/>(꺼진 것 포함). 등록부는 켜진 것만 들고 있어 직접 훑는다.</summary>
    private static List<JudgeTarget> AllJudgeTargets()
    {
        List<JudgeTarget> list = new List<JudgeTarget>();
        List<Transform> all = AllTransforms();
        for (int i = 0; i < all.Count; i++)
        {
            if (all[i] == null)
            {
                continue;
            }

            JudgeTarget target = all[i].GetComponent<JudgeTarget>();
            if (target != null)
            {
                list.Add(target);
            }
        }

        return list;
    }

    /// <summary>씬에 이미 있는 판정 대상 ID 전부.</summary>
    private static HashSet<string> SceneTargetIds()
    {
        HashSet<string> set = new HashSet<string>();
        List<JudgeTarget> targets = AllJudgeTargets();
        for (int i = 0; i < targets.Count; i++)
        {
            IReadOnlyList<string> ids = targets[i].Ids;
            for (int k = 0; k < ids.Count; k++)
            {
                string id = ids[k] != null ? ids[k].Trim() : string.Empty;
                if (id.Length > 0)
                {
                    set.Add(id);
                }
            }
        }

        return set;
    }

    /// <summary>표식의 ID 중 목록에 든 첫 번째. 없으면 빈 문자열.</summary>
    private static string MatchedId(JudgeTarget target, string[] pool)
    {
        IReadOnlyList<string> ids = target.Ids;
        for (int i = 0; i < ids.Count; i++)
        {
            string id = ids[i] != null ? ids[i].Trim() : string.Empty;
            if (id.Length > 0 && Contains(pool, id))
            {
                return id;
            }
        }

        return string.Empty;
    }

    private static bool Contains(string[] pool, string id)
    {
        for (int i = 0; i < pool.Length; i++)
        {
            if (string.Equals(pool[i], id, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static GameObject FindChildByNameContains(Transform root, string fragment)
    {
        Transform[] all = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i].name.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return all[i].gameObject;
            }
        }

        return null;
    }

    /// <summary>타입 이름만으로 씬의 컴포넌트를 찾는다(다른 폴더의 타입을 컴파일 의존 없이 다루기 위해서다).</summary>
    private static Component FindByTypeName(string typeName)
    {
        List<Transform> all = AllTransforms();
        for (int i = 0; i < all.Count; i++)
        {
            if (all[i] == null)
            {
                continue;
            }

            MonoBehaviour[] behaviours = all[i].GetComponents<MonoBehaviour>();
            for (int k = 0; k < behaviours.Length; k++)
            {
                if (behaviours[k] != null && behaviours[k].GetType().Name == typeName)
                {
                    return behaviours[k];
                }
            }
        }

        return null;
    }

    // ─────────────────────────────── 리플렉션 ───────────────────────────────

    /// <summary>
    /// private 필드에 런타임 값을 넣는다. <b>필드가 없으면 조용히 넘어가고 경고를 한 번만 남긴다</b> —
    /// 남의 코드의 필드 이름이 바뀌었다고 하네스가 터지면, 정작 시험하려던 것을 못 본다.
    /// </summary>
    private bool SetPrivate(object target, string fieldName, object value)
    {
        if (target == null)
        {
            return false;
        }

        FieldInfo field = FindField(target.GetType(), fieldName);
        if (field == null)
        {
            WarnOnce(target.GetType().Name + "." + fieldName);
            return false;
        }

        try
        {
            object converted = value;
            if (value != null && !field.FieldType.IsInstanceOfType(value) && value is IConvertible)
            {
                converted = Convert.ChangeType(value, field.FieldType);
            }

            field.SetValue(target, converted);
            return true;
        }
        catch (Exception e)
        {
            WarnOnce(target.GetType().Name + "." + fieldName);
            Debug.LogException(e, this);
            return false;
        }
    }

    /// <summary>상속 계층을 거슬러 올라가며 필드를 찾는다(private은 선언한 타입에서만 보인다).</summary>
    private static FieldInfo FindField(Type type, string fieldName)
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly;
        Type t = type;
        while (t != null)
        {
            FieldInfo field = t.GetField(fieldName, Flags);
            if (field != null)
            {
                return field;
            }

            t = t.BaseType;
        }

        return null;
    }

    private void WarnOnce(string what)
    {
        if (!_warned.Add(what))
        {
            return;
        }

        Warn(what + " 필드를 찾지 못해 건너뛰었습니다(이름이 바뀐 듯합니다).");
    }

    /// <summary>알림 한 줄. 상태판에도 남는다.</summary>
    private void Note(string line)
    {
        Debug.Log("[하네스] " + line, this);
        Keep(line);
    }

    /// <summary>손볼 거리가 있는 알림. 콘솔에서 눈에 띄게 경고로 남긴다.</summary>
    private void Warn(string line)
    {
        Debug.LogWarning("[하네스] " + line, this);
        Keep(line);
    }

    private void Keep(string line)
    {
        _notes.Add(line);
        while (_notes.Count > 20)
        {
            _notes.RemoveAt(0);
        }
    }

    // ─────────────────────────────── 화면 (읽기 전용) ───────────────────────────────

    private void OnGUI()
    {
        if (!_show)
        {
            return;
        }

        if (_small == null)
        {
            _small = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true };
            _bold = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold };
        }

        float scale = Mathf.Clamp(Screen.width * 0.40f / PanelWidth, 0.4f, 1.1f);
        Matrix4x4 saved = GUI.matrix;
        GUIUtility.ScaleAroundPivot(new Vector2(scale, scale), Vector2.zero);

        float h = Screen.height / scale;
        try
        {
            // NightRunDebugPanel이 오른쪽에 뜨므로 이쪽은 왼쪽이다. 두 패널이 겹치지 않는다.
            GUILayout.BeginArea(new Rect(10f, 10f, PanelWidth, h - 20f));
            GUILayout.BeginVertical(GUI.skin.box);

            GUILayout.Label("런타임 테스트 하네스 (읽기 전용)", _bold);

            _scroll = GUILayout.BeginScrollView(_scroll);
            DrawRun();
            DrawDeck();
            DrawEncounter();
            DrawGaze();
            DrawBuilt();
            GUILayout.EndScrollView();

            GUILayout.Label("F1 지침 · F2 판정 · F3 이 패널 · F6 표식 · F7 구역 · F4 잠긴 문 무시(" + (ignoreDoorLocks ? "켬" : "끔") + ") · F5 = 모든 문 열기(" + (openEveryDoor ? "켬" : "끔") + ")", _small);

            GUILayout.EndVertical();
            GUILayout.EndArea();
        }
        finally
        {
            // 예외가 나도 다음 IMGUI 그리기가 어긋나지 않게 반드시 되돌린다.
            GUI.matrix = saved;
        }
    }

    private void DrawRun()
    {
        string state = NightRun.IsCaptured
            ? "포획됨 — " + NightRun.Cause
            : (NightRun.IsNightActive ? "진행 중" : "밤 아님");
        GUILayout.Label(NightRun.Day + "일차 · " + state, _bold);

        IFearAxisReader axes = NightRun.Axes;
        GUILayout.BeginHorizontal();
        for (int i = 0; i < 4; i++)
        {
            FearAxis axis = (FearAxis)i;
            GUILayout.Label(AxisNames[i] + " " + axes.GetValue(axis) + " (구간 " + (int)axes.GetBand(axis) + ")",
                _small, GUILayout.Width(100));
        }

        GUILayout.EndHorizontal();
    }

    private void DrawDeck()
    {
        GUILayout.Space(4);
        GUILayout.Label("오늘 덱", _bold);

        RuleBook book = NightRun.CurrentBook;
        if (book == null)
        {
            GUILayout.Label("밤을 시작하면 오늘 덱이 여기에 나옵니다.", _small);
            return;
        }

        for (int i = 0; i < book.Watchers.Count; i++)
        {
            RuleWatcher watcher = book.Watchers[i];
            Color saved = GUI.color;
            GUI.color = StateColor(watcher.State);
            GUILayout.Label(watcher.Card.CardId + "  " + StateName(watcher.State), _small);
            GUI.color = saved;
        }
    }

    private void DrawEncounter()
    {
        GUILayout.Space(4);
        GUILayout.Label("오늘 조우", _bold);

        EncounterDirector director = NightRun.Encounter;
        if (director == null)
        {
            GUILayout.Label("아직 연출기가 없습니다(회차가 열리면 생깁니다).", _small);
            return;
        }

        IReadOnlyList<string> today = director.TodayScenes;
        if (today.Count == 0)
        {
            GUILayout.Label("오늘 깔린 장면이 없습니다.", _small);
        }

        for (int i = 0; i < today.Count; i++)
        {
            string sceneId = today[i];
            string gate = EncounterDirector.UsesExitGate(sceneId)
                ? (director.IsExitGatePlaced(sceneId) ? " · 게이트 섬" : " · 게이트 아직")
                : string.Empty;
            GUILayout.Label(sceneId + "  " + EncounterStateName(director.StateOf(sceneId))
                            + " · 접근 " + director.StepOf(sceneId) + "/" + EncounterDirector.ApproachStepCount
                            + " · " + SpaceName(director.SpaceOf(sceneId)) + gate, _small);
        }

        IReadOnlyList<string> carried = director.CarriedOver;
        if (carried.Count > 0)
        {
            string line = string.Empty;
            for (int i = 0; i < carried.Count; i++)
            {
                line += (line.Length > 0 ? ", " : string.Empty) + carried[i];
            }

            GUILayout.Label("이월: " + line, _small);
        }

        GUILayout.Label(_hookedDirector == director ? "시야·배치 통로 연결됨" : "시야·배치 통로 아직 안 꽂힘", _small);
    }

    /// <summary>
    /// 지금 무엇을 보고 있고 대역까지 몇 초 남았는지. <b>손으로 시험할 때 이 한 줄이 가장 쓸모 있다</b> —
    /// 표식은 눈에 보이지 않으므로(Renderer 없음) 조준이 맞았는지 알 길이 여기밖에 없다.
    /// </summary>
    private void DrawGaze()
    {
        if (!fakeMissingSignals)
        {
            return;
        }

        GUILayout.Space(4);
        GUILayout.Label("응시 대역 (⑦)", _bold);

        if (_sensors == null)
        {
            GUILayout.Label("센서가 없습니다.", _small);
            return;
        }

        string id = _sensors.Gaze.CurrentId;
        if (string.IsNullOrEmpty(id))
        {
            GUILayout.Label("보고 있는 대상 없음", _small);
        }
        else
        {
            string kind = string.Empty;
            string scene = SceneAtTarget(id);
            if (scene.Length > 0)
            {
                kind = " — 모형(" + scene + ")";
            }
            else if (_autoDoors.ContainsKey(id))
            {
                kind = " — 자동 개방 문";
            }

            string state = _firedTonight.Contains(id)
                ? "이번 밤 이미 보냄"
                : (kind.Length > 0 ? _gazedSeconds.ToString("0.0") + " / " + ObserveSeconds.ToString("0.0") + "초" : "대역 대상 아님");

            GUILayout.Label(id + kind + "\n" + state, _small);
        }

        if (_lastFake.Length > 0)
        {
            GUILayout.Label("마지막 대역: " + _lastFake, _small);
        }
    }

    private void DrawBuilt()
    {
        GUILayout.Space(4);
        GUILayout.Label("하네스가 만든 것", _bold);
        GUILayout.Label("부착한 컴포넌트 " + _attached + "개 (문 " + _doorsWired + "개 포함)"
                        + "\n만든 판정 대상 " + _created + "개"
                        + "\n보충한 콜라이더 " + _collidersAdded + "개"
                        + "\n런타임에 끈 손전등 " + _flashlightsOff + "개", _small);

        if (_notes.Count == 0)
        {
            return;
        }

        GUILayout.Space(2);
        GUILayout.Label("알림", _bold);
        for (int i = _notes.Count - 1; i >= 0; i--)
        {
            GUILayout.Label(_notes[i], _small);
        }
    }

    private static string StateName(CardState state)
    {
        switch (state)
        {
            case CardState.Waiting: return "대기";
            case CardState.Active: return "진행";
            case CardState.Complied: return "준수";
            case CardState.Violated: return "위반";
            case CardState.Undetermined: return "미판정";
            case CardState.Locked: return "잠금";
            default: return state.ToString();
        }
    }

    private static Color StateColor(CardState state)
    {
        switch (state)
        {
            case CardState.Active: return new Color(1f, 0.9f, 0.4f);
            case CardState.Complied: return new Color(0.5f, 1f, 0.5f);
            case CardState.Violated: return new Color(1f, 0.45f, 0.4f);
            case CardState.Undetermined: return new Color(0.7f, 0.7f, 0.7f);
            case CardState.Locked: return new Color(0.8f, 0.5f, 1f);
            default: return Color.white;
        }
    }

    private static string EncounterStateName(EncounterState state)
    {
        switch (state)
        {
            case EncounterState.Idle: return "대기";
            case EncounterState.Active: return "깔림";
            case EncounterState.Observed: return "관찰됨";
            case EncounterState.Done: return "끝남";
            default: return state.ToString();
        }
    }

    private static string SpaceName(SpaceId space)
    {
        switch (space)
        {
            case SpaceId.Corridor: return "복도";
            case SpaceId.Classroom_1_1: return "1-1";
            case SpaceId.Classroom_1_3: return "1-3";
            case SpaceId.ScienceRoom: return "과학실";
            case SpaceId.Toilet: return "화장실";
            case SpaceId.None: return "없음";
            default: return space.ToString();
        }
    }
}

using NightDuty;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 게임 흐름(GameSession·GameTime)과 판정 코어(<see cref="NightRun"/>)를 잇는 Play 씬 구동기.
/// <list type="bullet">
/// <item>Start: 최종 기획서 규칙 셋을 켠다 — 판정 시간창(<see cref="NightRun.JudgingWindowEnabled"/>),
/// 점검표 편성(<see cref="NightRun.InspectionsEnabled"/>), 밤 편성(<see cref="NightRun.ProgramEnabled"/>).
/// 그리고 <see cref="NightRun.BeginNight"/>(현재 일차, <b>밤 시계 분</b>). 씬의 JudgeTarget이 모두 켜진 뒤라 대상 검사가 맞다.</item>
/// <item>Update: 게임 시계가 흐를 때만 <see cref="NightRun.Tick"/>. 판정 시간은 <b>실제 초</b>다 — 시계 배속을 곱하지 않는다.
/// DayIntro 연출 중(시계 정지)·일시정지(timeScale 0)·근무 종료 뒤에는 흐르지 않는다. 판정 정지 구간(출근·이완·03:30 뒤)은 코어가 거른다.</item>
/// <item>GameTime.ShiftEnded: <see cref="NightRun.RequestEndNight"/> → <see cref="EventBus.DayEnded"/> → PlayResultRouter가 결과창으로 보낸다.</item>
/// <item>파괴될 때 밤이 아직 열려 있으면(메인으로 나가기 등) <see cref="NightRun.AbandonNight"/>.</item>
/// </list>
/// <para>
/// <b>밤 시계(2026-09-30 최종 기획서).</b> 코어는 「근무 시작부터의 분, 0~300(00:00~05:00, 67차)」으로 판정 시간창을 본다(<see cref="NightClock"/>).
/// GameTime은 자정 기준 절대 분을 주고 근무 범위도 프리팹 값(현재 02:00~05:00)을 따르므로, 여기서
/// <c>(현재 − 시작) × 300 / (종료 − 시작)</c>으로 <b>비례 환산</b>한다 — GameTime 범위가 무엇이든 구간 비율이 기획서와 같다.
/// <see cref="MatchDesignPace"/>가 켜져 있으면 배속도 바꿔 한 밤이 실시간 12분 30초(67차 — 59차 10분, 그 전 15분)가 되게 한다(GameTime의 공개 API <c>SetTimeMultiplier</c>).
/// 태블릿의 <b>표시 시각</b>은 GameTime 몫이라 건드리지 않는다 — 00:00~04:00 표시는 김진선님과 맞출 일이다.
/// </para>
/// <para>
/// <b>씬·프리팹에 직접 놓지 않아도 된다.</b> 씬이 로드될 때 GameTime이 있고 이 컴포넌트가 없으면 자동으로 하나 만든다
/// (통일 씬 작업과 프리팹 잠금이 겹치지 않게 하기 위함). 직접 놓아 두면 자동 생성은 건너뛴다.
/// </para>
/// <para>
/// TODO(재시작 연결): 붙잡힘 → <see cref="NightRun.RestartAfterCapture"/>는 코어가 끝냈다. 씬 다시 불러오기와
/// 게임 시계를 <see cref="RestartResult.StartMinute"/>(00:00 또는 02:16)로 되돌리는 일은 GameTime·사망 화면(김진선님) 쪽 API가 필요하다.
/// 지금은 밤 시계 추적기만 되돌린다.
/// </para>
/// </summary>
[DisallowMultipleComponent]
public sealed class NightRunDriver : MonoBehaviour
{
    /// <summary>
    /// 켜 두면 한 밤이 실시간 10분(<see cref="NightClock.RealSecondsPerNight"/>)이 되도록 GameTime 배속을 맞춘다.
    /// 끄면 GameTime 프리팹의 배속을 그대로 쓴다(구간 비율은 어느 쪽이든 기획서와 같다). 자동 생성되는 구동기라 정적 설정으로 둔다.
    /// </summary>
    public static bool MatchDesignPace = true;

    [Tooltip("비워 두면 씬에서 자동으로 찾는다.")]
    [SerializeField] private GameTime gameTime;

    private bool _begun;
    private readonly NightClockTracker _tracker = new NightClockTracker();
    private float _rewindOffset;

    // 지금 밤을 연 구동기. 씬 전환 때 옛 구동기의 OnDestroy가 새 밤을 버리지 않게 한다.
    private static NightRunDriver s_owner;

    /// <summary>이 구동기가 연결된 게임 시계.</summary>
    public GameTime Clock => gameTime;

    /// <summary>밤 시계 구간 추적기. 호출 1·2·잔여 알림·근무 종료 경계를 한 번씩 알린다.</summary>
    public NightClockTracker Tracker => _tracker;

    /// <summary>지금 밤을 연 구동기. 없으면 null. 호출·잔여 알림 UI가 <see cref="Tracker"/>를 구독할 때 쓴다.</summary>
    public static NightRunDriver Current => s_owner;

    /// <summary>지금 밤 시계(근무 시작부터의 게임 분, 0~240). 시계를 모르면 -1.</summary>
    public float NightMinute
    {
        get
        {
            if (gameTime == null) return -1f;
            int span = gameTime.EndMinutes - gameTime.StartMinutes;
            if (span <= 0) return -1f;

            float elapsed = gameTime.CurrentMinutes - gameTime.StartMinutes;
            float minute = elapsed * NightClock.ShiftEnd / span + _rewindOffset;
            return Mathf.Clamp(minute, 0f, NightClock.ShiftEnd);
        }
    }

    /// <summary>
    /// 디버그: 밤 시계를 그 분(0~240)으로 옮긴다. 게임 시계(GameTime)도 같은 분으로 옮긴다(47차 — 전에는 표시를 두고 오프셋만 바꿔,
    /// 03:59로 뛰어도 화면 시계가 00:12라 04:00 근무 종료·5일차 피날레가 오지 않았다). 남는 차이는 오프셋으로 메운다.
    /// 구간 추적기는 그 분에서 다시 시작한다(지나간 호출을 다시 울리지 않음).
    /// </summary>
    public void DebugJumpToNightMinute(float minute)
    {
        if (gameTime == null) return;
        int span = gameTime.EndMinutes - gameTime.StartMinutes;
        if (span <= 0) return;

        minute = Mathf.Clamp(minute, 0f, NightClock.ShiftEnd - 1f);
        CaptureDirector.SetClockMinute(gameTime, gameTime.StartMinutes + Mathf.FloorToInt(minute * span / NightClock.ShiftEnd));
        float raw = (gameTime.CurrentMinutes - gameTime.StartMinutes) * NightClock.ShiftEnd / (float)span;
        _rewindOffset = minute - raw;
        _tracker.Reset(NightMinute);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Install()
    {
        s_owner = null;
        MatchDesignPace = true;
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
        foreach (NightRunDriver existing in FindObjectsByType<NightRunDriver>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (existing.gameObject.scene == scene) return;
        }

        GameTime clock = null;
        foreach (GameTime candidate in FindObjectsByType<GameTime>(FindObjectsSortMode.None))
        {
            if (candidate.gameObject.scene == scene)
            {
                clock = candidate;
                break;
            }
        }

        if (clock == null) return;   // 근무 씬이 아니다(메인·로딩·결과·시험 씬).

        GameObject go = new GameObject("NightRunDriver (auto)");
        SceneManager.MoveGameObjectToScene(go, scene);
        NightRunDriver driver = go.AddComponent<NightRunDriver>();
        driver.gameTime = clock;
    }

    private void Start()
    {
        if (gameTime == null) gameTime = FindAnyObjectByType<GameTime>();
        if (gameTime == null)
        {
            Debug.LogWarning("[NightRunDriver] 씬에 GameTime이 없어 밤을 시작하지 않습니다.", this);
            enabled = false;
            return;
        }

        if (MatchDesignPace)
        {
            int span = gameTime.EndMinutes - gameTime.StartMinutes;
            if (span > 0)
            {
                // 게임 초 / 실제 초. 02:00~05:00(180분)이면 18배속 → 실시간 10분.
                gameTime.SetTimeMultiplier(span * 60f / NightClock.RealSecondsPerNight);
            }
        }

        gameTime.ShiftEnded += OnShiftEnded;
        EventBus.NightRestarted += OnNightRestarted;
        _tracker.HourStruck += OnHourStruck;   // 67차(민: 「1시간마다 알림」)

        // 최종 기획서 규칙 셋. 옛 테스트는 모두 꺼진 상태를 기준으로 쓰였으므로 코어의 기본값은 꺼 둔다.
        NightRun.JudgingWindowEnabled = true;
        NightRun.InspectionsEnabled = true;
        NightRun.InspectionDripEnabled = true;   // 50차: 점검은 순차 지시로
        NightRun.ProgramEnabled = true;
        NightRun.DutiesEnabled = true;           // 54차: [근무 지시]
        NightRun.BatteryEnabled = true;          // 56차: 손전등 배터리
        NightRun.FixedMobStareEnabled = true;    // 64차: 고정 몹 응시

        _rewindOffset = 0f;
        _tracker.Reset(NightMinute);
        DoorRelay.RescanForNight(gameObject.scene);
        NightRun.BeginNight(GameSession.CurrentDay, CurrentNightMinute);
        _begun = true;
        s_owner = this;
    }

    private void Update()
    {
        if (!_begun || s_owner != this) return;
        bool flowing = gameTime.IsRunning && !gameTime.IsEnded;
        if (flowing) _tracker.Advance(NightMinute);

        if (!NightRun.IsNightActive) return;
        // 71차: 개발자 모드 흐름 정지 — 게임 시계는 멈춰 있어도 판정·연출 시간은 흐른다(직접 부른 수칙·연출이 끝까지 돌게).
        if (flowing || (NightRun.Sandbox && !gameTime.IsEnded)) NightRun.Tick(Time.deltaTime);
    }

    private void OnDestroy()
    {
        if (gameTime != null) gameTime.ShiftEnded -= OnShiftEnded;
        EventBus.NightRestarted -= OnNightRestarted;

        if (s_owner != this) return;
        s_owner = null;

        if (NightRun.IsNightActive)
        {
            NightRun.AbandonNight();
        }

        // 켜 둔 규칙 스위치를 기본값(꺼짐)으로 돌린다. 플레이 종료 뒤 도메인 리로드가 없으면 정적 값이 남아
        // EditMode 테스트(옛 규칙 기준)가 깨진다 — 2026-10-01 실측 4건.
        NightRun.JudgingWindowEnabled = false;
        NightRun.InspectionsEnabled = false;
        NightRun.InspectionDripEnabled = false;
        NightRun.ProgramEnabled = false;
        NightRun.DutiesEnabled = false;
        NightRun.BatteryEnabled = false;
        NightRun.FixedMobStareEnabled = false;
        NightRun.BatterySeed = null;
        NightRun.DirectorAutoRun = true;
    }

    private void OnHourStruck(int hour)
    {
        if (s_owner != this || !NightRun.IsNightActive || NightRun.IsCaptured || NightRun.Finale.Active) return;
        EventBus.RaiseHourStruck(hour);
    }

    private void OnShiftEnded()
    {
        if (s_owner != this || !NightRun.IsNightActive) return;   // 이미 포획·중단된 밤

        // 5일차 04:00 — 정산하지 않고 피날레(11단계). 결말에서 FinaleDirector가 NightRun.EndFinale로 정산한다.
        if (NightRun.Finale.Active) return;
        if (NightRun.Day >= FinaleWatch.Day && FinaleDirector.Active != null && FinaleDirector.Active.Begin()) return;

        if (!NightRun.RequestEndNight() && !NightRun.IsCaptured)
        {
            Debug.LogWarning("[NightRunDriver] 근무 종료 요청이 거절됐습니다.", this);
        }
    }

    private void OnNightRestarted(RestartResult result)
    {
        if (s_owner != this || result.Kind == RestartKind.None || result.Kind == RestartKind.Absent) return;

        // GameTime을 되돌릴 API가 아직 없다. 밤 시계만 재시작 지점으로 맞춘다 — 판정 시간창은 이 값을 본다.
        _rewindOffset = 0f;
        _rewindOffset = result.StartMinute - NightMinute;
        _tracker.Reset(result.StartMinute);
        Debug.Log("[NightRunDriver] 밤 재시작(k=" + result.K + ", " + result.Kind + "). 게임 시계 되돌리기는 GameTime 쪽 연결이 필요합니다.", this);
    }

    /// <summary>디버그: 지금 밤을 버리고 그 일차로 다시 연다(피날레 시험용 5일차). 회차 축·기록은 그대로다.</summary>
    public void DebugRestartAsDay(int day)
    {
        if (s_owner != this) return;
        if (NightRun.IsNightActive) NightRun.AbandonNight();
        GameSession.SetDay(day);
        _rewindOffset = 0f;
        _tracker.Reset(NightMinute);
        DoorRelay.RescanForNight(gameObject.scene);
        NightRun.BeginNight(GameSession.CurrentDay, CurrentNightMinute);
        Debug.Log("[NightRunDriver] 디버그: " + GameSession.CurrentDay + "일차로 다시 열었습니다.", this);
    }

    private int CurrentNightMinute()
    {
        float minute = NightMinute;
        return minute < 0f ? -1 : Mathf.FloorToInt(minute);
    }
}

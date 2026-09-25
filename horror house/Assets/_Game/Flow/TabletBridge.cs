using NightDuty;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 태블릿(김진선 리그)과 판정 코어를 잇는 다리. <b>양쪽 어느 쪽도 서로를 몰라도 되게</b> 한다 —
/// 태블릿은 판정을 모르고, 코어는 화면을 모른다. 이 컴포넌트만 지우면 둘이 다시 떨어진다.
///
/// <list type="bullet">
/// <item><b>Tab 신호</b> — 태블릿을 들고 내릴 때 <see cref="JudgeSignal.Tab"/>을 보낸다.
/// 정본 명세상 Tab 중에는 게임 시계와 판정 타이머가 멈춰야 하므로 <see cref="GameTime.SetRunning"/>도 함께 끈다.</item>
/// <item><b>역설 문자</b> — <see cref="EventBus.MessageSent"/>를 받아 태블릿 메시지함에 넣는다.
/// 넣는 순간 <see cref="TabletAlarm"/>이 울린다(TabletMessageList.MessageReceived 경유).</item>
/// <item><b>화면 글리치</b> — 역설 문자가 도착할 때만 잠깐 올린다.
/// <b>신뢰 수치에 연결하지 않는다</b> — 2026-09-17에 폐기된 안이다.</item>
/// </list>
///
/// <para>
/// <b>씬에 직접 놓지 않아도 된다.</b> <see cref="NightRunDriver"/>와 같은 방식으로, 씬에 GameTime이 있고
/// 이 컴포넌트가 없으면 자동으로 하나 만든다. 태블릿이 그 씬에 없으면 아무 일도 하지 않는다.
/// </para>
///
/// <para>
/// <b>시계를 멈추는 방법이 둘인데 하나만 맞다.</b> <c>Time.timeScale = 0</c>을 쓰면
/// <see cref="ViewmodelTime.Paused"/>가 참이 되어 태블릿 리그 전체(토글 키·스크롤·글리치·알람)가 같이 얼어붙는다.
/// 그래서 시계만 멈추는 <see cref="GameTime.SetRunning"/>을 쓰고, 혹시 누군가 timeScale로 멈추더라도
/// 태블릿은 계속 살아 있도록 <see cref="ViewmodelTime.ignoreTimeScale"/>을 Tab 동안 켜 둔다.
/// </para>
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(60)]
public sealed class TabletBridge : MonoBehaviour
{
    /// <summary>글리치가 올라가는 세기. 문자 한 통에 화면이 아주 망가지면 읽을 수가 없다.</summary>
    private const float MessageGlitch = 0.55f;

    /// <summary>글리치가 다시 내려가기까지의 시간(초).</summary>
    private const float GlitchHoldSeconds = 1.4f;

    [Tooltip("비워 두면 씬에서 자동으로 찾는다.")]
    [SerializeField] private PlayerTablet tablet;

    [Tooltip("비워 두면 태블릿에서 자동으로 찾는다.")]
    [SerializeField] private TabletMessageList messages;

    [SerializeField] private TabletGlitch glitch;
    [SerializeField] private GameTime gameTime;

    [Tooltip("Tab을 여는 동안 게임 시계를 멈춘다(기획서 Tab 규칙). 끄면 신호만 보내고 시계는 계속 흐른다.")]
    [SerializeField] private bool freezeClockWhileOpen = true;

    private bool _wasOpen;
    private bool _clockWasRunning;
    private float _glitchUntil;
    private bool _glitchOn;

    // 같은 역설을 두 번 받아도 알람이 울리도록 번호를 붙인다.
    // TabletMessageList.Add는 같은 id면 본문만 갈아 끼우고 알람을 울리지 않는다.
    private int _messageSerial;

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

        foreach (TabletBridge existing in FindObjectsByType<TabletBridge>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (existing.gameObject.scene == scene) return;
        }

        // 근무 씬인지 판별하는 기준을 NightRunDriver와 똑같이 잡는다 — 게임 시계가 있는 씬.
        GameTime clock = null;
        foreach (GameTime candidate in FindObjectsByType<GameTime>(FindObjectsSortMode.None))
        {
            if (candidate.gameObject.scene == scene)
            {
                clock = candidate;
                break;
            }
        }

        if (clock == null) return;

        GameObject go = new GameObject("TabletBridge (auto)");
        SceneManager.MoveGameObjectToScene(go, scene);
        TabletBridge bridge = go.AddComponent<TabletBridge>();
        bridge.gameTime = clock;
    }

    private void OnEnable()
    {
        EventBus.MessageSent += OnMessageSent;
    }

    private void OnDisable()
    {
        EventBus.MessageSent -= OnMessageSent;

        // 씬을 떠날 때 Tab 상태가 열린 채로 남으면 다음 씬의 센서가 영원히 침묵한다.
        PlayerSensors.SetTabOpen(false);

        // 태블릿을 연 채로 씬을 떠나면 시계가 멈춘 채로 남는다.
        RestoreClock();
    }

    private void Update()
    {
        if (!Bind()) return;

        bool open = tablet.IsOpened;
        if (open != _wasOpen)
        {
            _wasOpen = open;
            OnToggled(open);
        }

        StepGlitch();
    }

    /// <summary>
    /// 태블릿을 열고 닫는 순간. <b>신호가 먼저, 시계가 나중</b>이다 —
    /// 코어가 Tab을 알기 전에 시계를 멈추면 그 사이의 Tick이 한 프레임 비어 버린다.
    /// </summary>
    private void OnToggled(bool open)
    {
        if (NightRun.IsNightActive)
        {
            JudgeSignal signal = JudgeSignal.Tab(open);
            NightRun.Send(signal);
        }

        // 센서 허브는 Tab이 열린 동안 아무 신호도 보내지 않아야 한다(정본 공통 명세 2절).
        // 이 창구가 유일한 호출자다 — 다른 곳에서 부르면 상태가 두 번 뒤집힌다.
        PlayerSensors.SetTabOpen(open);

        if (!freezeClockWhileOpen || gameTime == null) return;

        if (open)
        {
            _clockWasRunning = gameTime.IsRunning;
            if (_clockWasRunning) gameTime.SetRunning(false);

            // 누군가 timeScale로 멈추더라도 태블릿은 계속 읽을 수 있어야 한다.
            ViewmodelTime.ignoreTimeScale = true;
        }
        else
        {
            RestoreClock();
        }
    }

    private void RestoreClock()
    {
        ViewmodelTime.ignoreTimeScale = false;

        if (gameTime == null || !_clockWasRunning) return;

        _clockWasRunning = false;
        if (!gameTime.IsEnded) gameTime.SetRunning(true);
    }

    /// <summary>
    /// 역설 문자 한 통. 태블릿은 이것이 무슨 카드의 짝인지 알 필요가 없다 — 본문만 받는다.
    /// <para><b>카드 ID를 화면에 적지 않는다</b>(설계 금기 2.8). 제작자용 ID는 로그에만 남긴다.</para>
    /// </summary>
    private void OnMessageSent(ParadoxMessage message)
    {
        if (!Bind() || messages == null) return;
        if (string.IsNullOrEmpty(message.Text)) return;

        _messageSerial++;
        string id = "paradox." + message.ParadoxId + "." + _messageSerial;
        messages.Add(id, message.Text, message.Minute);

        if (glitch != null)
        {
            glitch.SetIntensity(MessageGlitch);
            _glitchUntil = Time.unscaledTime + GlitchHoldSeconds;
            _glitchOn = true;
        }
    }

    private void StepGlitch()
    {
        if (!_glitchOn || Time.unscaledTime < _glitchUntil) return;

        _glitchOn = false;
        if (glitch != null) glitch.SetIntensity(0f);
    }

    /// <summary>태블릿은 씬 어디에 있어도 되고 늦게 나타날 수도 있으므로 매번 가볍게 확인한다.</summary>
    private bool Bind()
    {
        if (tablet == null) tablet = FindAnyObjectByType<PlayerTablet>();
        if (tablet == null) return false;

        if (messages == null) messages = tablet.GetComponentInChildren<TabletMessageList>(true);
        if (glitch == null) glitch = tablet.GetComponentInChildren<TabletGlitch>(true);
        if (gameTime == null) gameTime = FindAnyObjectByType<GameTime>();

        return true;
    }
}

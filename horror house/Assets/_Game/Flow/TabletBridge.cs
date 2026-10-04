using System.Collections.Generic;
using NightDuty;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 태블릿(김진선 리그)과 판정 코어를 잇는 다리. <b>양쪽 어느 쪽도 서로를 몰라도 되게</b> 한다 —
/// 태블릿은 판정을 모르고, 코어는 화면을 모른다. 이 컴포넌트만 지우면 둘이 다시 떨어진다.
///
/// <list type="bullet">
/// <item><b>Tab 신호</b> — 태블릿을 들고 내릴 때 <see cref="JudgeSignal.Tab"/>을 보낸다.
/// 정본 명세는 Tab 중 게임 시계 정지지만, 2026-09-29 결정으로 <b>시계는 흐르게</b> 했다(<see cref="freezeClockWhileOpen"/> = false).
/// 켜면 <see cref="GameTime.Hold"/>로 시계도 함께 멈춘다.</item>
/// <item><b>역설 문자</b> — <see cref="EventBus.MessageSent"/>를 받아 태블릿 메시지함에 넣는다.
/// 넣는 순간 <see cref="TabletAlarm"/>이 울린다(TabletMessageList.MessageReceived 경유).</item>
/// <item><b>점검 지시 문자</b>(32차) — 그날 점검표를 메시지 한 통(<see cref="NightRun.ChecklistMessage"/>)으로 넣는다.
/// 새 점검 편성이면 지우고 새로 넣고(알람 한 번), 보고·안전한 읽기·재시작 때는 같은 ID로 본문만 고친다(알람 없음).</item>
/// <item><b>안전한 읽기</b>(10단계) — <see cref="EventBus.SafeReadConfirmed"/>를 받으면 태블릿이 짧게 떨고(약한 글리치) 진동음(<c>tablet.buzz</c>) 한 번, 태블릿 글을 다시 읽는다(점검표에 「확인 필요」/「이상 없음」).
/// 수칙 위반 때도 같은 진동음이다(2026-10-04 사운드 전달본 PUN-02).</item>
/// <item><b>글자 깨짐</b> — 그 밤 수칙에 변조·검게 지운 줄이 있으면 태블릿을 처음 올릴 때 「틱틱」(<c>tablet.corrupt</c>)과 짧은 글리치.</item>
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
/// 그래서 시계만 멈추는 <see cref="GameTime.Hold"/>를 쓰고, 혹시 누군가 timeScale로 멈추더라도
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

    /// <summary>안전한 읽기 진동 — 글리치 세기.</summary>
    private const float ConfirmGlitch = 0.22f;

    /// <summary>위반 진동 — 글리치 세기(안전한 읽기보다 조금 세게).</summary>
    private const float ViolationGlitch = 0.32f;

    /// <summary>안전한 읽기 진동 — 길이(초).</summary>
    private const float ConfirmHoldSeconds = 0.35f;

    private static AudioClip s_tick;
    private ParadoxPlan _corruptShownFor;
    private InspectionPlan _checklistPlan;
    private string _checklistText;
    private AudioSource _tickSource;

    [Tooltip("비워 두면 씬에서 자동으로 찾는다.")]
    [SerializeField] private PlayerTablet tablet;

    [Tooltip("비워 두면 태블릿에서 자동으로 찾는다.")]
    [SerializeField] private TabletMessageList messages;

    [SerializeField] private TabletGlitch glitch;
    [SerializeField] private GameTime gameTime;

    // 2026-09-29 김진선 결정: 태블릿을 보는 동안에도 인게임 시간은 흐른다. 모든 것이 멈추는 것은 일시정지(GamePause)뿐이다.
    // 기획서 Tab 규칙(「Tab 중 게임 시계 정지」)과 다르므로, 되돌리려면 이 값만 true로 바꾼다.
    [Tooltip("Tab을 여는 동안 게임 시계를 멈춘다(기획서 Tab 규칙). 끄면 신호만 보내고 시계는 계속 흐른다. 현재 결정은 끔.")]
    [SerializeField] private bool freezeClockWhileOpen = false;

    private bool _wasOpen;
    private bool _clockHeld;
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
        EventBus.SafeReadConfirmed += OnSafeReadConfirmed;
        EventBus.InspectionReported += OnInspectionReported;
        EventBus.NightRestarted += OnNightRestarted;
        EventBus.TabletTextChanged += OnTabletTextChanged;
        EventBus.FinalRuleSettled += OnFinalRuleSettled;
    }

    private void OnDisable()
    {
        EventBus.MessageSent -= OnMessageSent;
        EventBus.SafeReadConfirmed -= OnSafeReadConfirmed;
        EventBus.InspectionReported -= OnInspectionReported;
        EventBus.NightRestarted -= OnNightRestarted;
        EventBus.TabletTextChanged -= OnTabletTextChanged;
        EventBus.FinalRuleSettled -= OnFinalRuleSettled;

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
            if (open) ShowCorruptOnce();
        }

        StepGlitch();

        // 본문은 사건(보고·안전한 읽기·재시작) 때만 다시 만든다. 매 프레임은 편성이 바뀌었는지·문자가 지워졌는지만 본다.
        if (NightRun.IsNightActive && (NightRun.Inspections.Plan != _checklistPlan || (!string.IsNullOrEmpty(_checklistText) && !Contains(NightRun.ChecklistMessageId))))
        {
            SyncChecklist(true);
        }
    }

    private void OnInspectionReported(InspectionReport report)
    {
        SyncChecklist(false);
    }

    private void OnNightRestarted(RestartResult result)
    {
        SyncChecklist(false);
        ReloadDocuments();
    }

    /// <summary>수칙 얼룩·재입실 불가 등 태블릿 글이 바뀌었다 — 다시 읽는다.</summary>
    private void OnTabletTextChanged()
    {
        if (!Bind()) return;
        SyncChecklist(false);
        ReloadDocuments();
    }

    /// <summary>위반 — 태블릿이 한 번 떤다(최종 기획서 「위반 피드백」 2단계). 얼룩은 코어가 수칙 글에 입힌다.</summary>
    private void OnFinalRuleSettled(FinalRuleResult result)
    {
        if (result.Outcome != FinalOutcome.Violated || !Bind()) return;
        if (glitch != null && !_glitchOn)
        {
            glitch.SetIntensity(ViolationGlitch);
            _glitchUntil = Time.unscaledTime + ConfirmHoldSeconds;
            _glitchOn = true;
        }

        PlayTick();
    }

    /// <summary>그 밤 수칙에 변조·검게 지운 줄이 있으면 태블릿을 처음 올릴 때 한 번 글자가 깨지는 소리와 글리치.</summary>
    private void ShowCorruptOnce()
    {
        if (!NightRun.IsNightActive || NightRun.Paradox == null) return;
        ParadoxPlan plan = NightRun.Paradox.Plan;
        if (plan == null || ReferenceEquals(plan, _corruptShownFor)) return;
        _corruptShownFor = plan;
        if (string.IsNullOrEmpty(plan.Tampered) && string.IsNullOrEmpty(plan.Blacked)) return;

        PlayTabletSound("tablet.corrupt");
        if (glitch != null && !_glitchOn)
        {
            glitch.SetIntensity(ConfirmGlitch);
            _glitchUntil = Time.unscaledTime + 0.5f;
            _glitchOn = true;
        }
    }

    private static void ReloadDocuments()
    {
        foreach (TabletDocument doc in FindObjectsByType<TabletDocument>(FindObjectsInactive.Include, FindObjectsSortMode.None)) doc.Reload();
    }

    /// <summary>
    /// 점검 지시 문자를 맞춘다. 점검 편성이 바뀌었으면(새 밤) 옛 문자를 지우고 새로 넣는다 — 알람이 한 번 울린다.
    /// 같은 편성이면 본문이 바뀐 때만 같은 ID로 다시 넣는다(TabletMessageList.Add — 알람 없음).
    /// 메시지함이 비워져 문자가 없어졌으면 다시 넣는다.
    /// </summary>
    private void SyncChecklist(bool force)
    {
        if (messages == null) return;

        // 피날레는 「들어왔다」에서 문자를 전부 지운다 — 다시 넣지 않는다.
        if (NightRun.Finale.Active && !Contains(NightRun.ChecklistMessageId)) return;

        InspectionPlan plan = NightRun.IsNightActive ? NightRun.Inspections.Plan : _checklistPlan;
        string text = NightRun.IsNightActive ? NightRun.ChecklistMessage : _checklistText;
        bool present = Contains(NightRun.ChecklistMessageId);

        if (plan != _checklistPlan)
        {
            _checklistPlan = plan;
            _checklistText = null;
            if (present) messages.Remove(NightRun.ChecklistMessageId);
            present = false;
        }

        if (string.IsNullOrEmpty(text))
        {
            if (present) messages.Remove(NightRun.ChecklistMessageId);
            _checklistText = text;
            return;
        }

        if (!force && present && text == _checklistText) return;
        _checklistText = text;
        messages.Add(NightRun.ChecklistMessageId, text);
    }

    private bool Contains(string id)
    {
        IReadOnlyList<TabletMessageList.Message> list = messages.Messages;
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i].id == id) return true;
        }

        return false;
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
            // 시계가 이미 (DayIntro 등으로) 멈춰 있어도 태블릿 몫으로 따로 멈춰 둔다.
            // 「멈춰 있었으면 건너뛴다」로 두면, 태블릿을 연 채로 그 연출이 끝나는 순간 시계가 흘러 버린다.
            gameTime.Hold(this);
            _clockHeld = true;

            // 누군가 timeScale로 멈추더라도 태블릿은 계속 읽을 수 있어야 한다.
            ViewmodelTime.ignoreTimeScale = true;
        }
        else
        {
            RestoreClock();
        }
    }

    /// <summary>태블릿 몫만 푼다. 사망 화면 · DayIntro가 멈춰 둔 것은 그대로 남는다.</summary>
    private void RestoreClock()
    {
        ViewmodelTime.ignoreTimeScale = false;

        if (gameTime == null || !_clockHeld) return;

        _clockHeld = false;
        gameTime.Release(this);
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
        // ParadoxMessage.Minute는 「근무 시작부터의 분」이고 메시지함은 「자정 기준 분」이다 — 받는 순간의 게임 시계로 적는다.
        messages.Add(id, message.Text);

        if (glitch != null)
        {
            glitch.SetIntensity(MessageGlitch);
            _glitchUntil = Time.unscaledTime + GlitchHoldSeconds;
            _glitchOn = true;
        }
    }

    /// <summary>
    /// 안전한 읽기 — 태블릿이 짧게 떨고 「틱」 한 번. 무엇이 드러났는지는 점검표에만 쓴다(소리·진동은 같다 — 이상 여부를 소리로 알려 주지 않는다).
    /// </summary>
    private void OnSafeReadConfirmed(SafeReadReveal reveal)
    {
        if (!Bind()) return;

        SyncChecklist(false);
        foreach (TabletDocument doc in FindObjectsByType<TabletDocument>(FindObjectsInactive.Include, FindObjectsSortMode.None)) doc.Reload();

        if (glitch != null && !_glitchOn)
        {
            glitch.SetIntensity(ConfirmGlitch);
            _glitchUntil = Time.unscaledTime + ConfirmHoldSeconds;
            _glitchOn = true;
        }

        PlayTick();
    }

    /// <summary>태블릿 진동음(연출 소리 표 <c>tablet.buzz</c>). 표에 없으면 코드로 만든 「틱」(2.4kHz, 40ms 감쇠).</summary>
    private void PlayTick()
    {
        if (PlayTabletSound("tablet.buzz")) return;
        if (s_tick == null)
        {
            const int rate = 44100;
            int n = rate * 40 / 1000;
            float[] data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float time = (float)i / rate;
                data[i] = Mathf.Sin(2f * Mathf.PI * 2400f * time) * Mathf.Exp(-time * 120f) * 0.6f;
            }

            s_tick = AudioClip.Create("tablet.tick", n, 1, rate, false);
            s_tick.SetData(data, 0);
        }

        EnsureTickSource();
        _tickSource.PlayOneShot(s_tick);
    }

    /// <summary>연출 소리 표의 태블릿 소리 하나를 몸 가까이(2D)에서. 없으면 false.</summary>
    private bool PlayTabletSound(string key)
    {
        float volume;
        AudioClip clip = DirectionSoundTableSO.FindExact(key, out volume);
        if (clip == null) return false;
        EnsureTickSource();
        _tickSource.PlayOneShot(clip, volume);
        return true;
    }

    private void EnsureTickSource()
    {
        if (_tickSource != null) return;
        _tickSource = gameObject.AddComponent<AudioSource>();
        _tickSource.playOnAwake = false;
        _tickSource.spatialBlend = 0f;
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

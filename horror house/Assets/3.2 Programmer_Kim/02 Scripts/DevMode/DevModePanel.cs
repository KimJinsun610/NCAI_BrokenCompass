#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using NightDuty;
using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// 플레이 중 '-' 키로 여닫는 개발자 모드 패널입니다. 화면 오른쪽에 뜹니다.
/// </summary>
/// <remarks>
/// 탭 세 개:
/// - <b>일차</b>: 일차를 골라 Play 씬을 처음부터 다시 불러옵니다.
/// - <b>게임 시간</b>: 배속 변경, 정해진 시각(정수 시)으로 이동.
/// - <b>지시 사항</b>: Lee의 판정 시스템을 연결할 자리. 지금은 버튼만 있습니다.
///
/// 씬 파일을 고치지 않습니다. 플레이를 시작하면 DontDestroyOnLoad 오브젝트로 자동으로 생기고,
/// 에디터와 Development Build에만 들어갑니다. 패널이 열려 있는 동안 FPController를 꺼서 시점이 돌지 않게 합니다.
/// </remarks>
public sealed class DevModePanel : MonoBehaviour
{
    private const float PanelWidth = 380f;
    private const int MaxLog = 30;

    private static readonly string[] TabNames = { "① 일차 · 사망", "② 게임 시간", "③ 지시 사항" };
    private static readonly float[] SpeedFactors = { 1f, 2f, 5f, 10f, 30f };
    private static readonly int[] QuickHours = { 1, 2, 3, 4, 5, 6 };
    private static readonly string[] SpaceNames = { "복도", "교실", "과학실", "화장실" };

    private static readonly Color SelectedTab = new Color(1f, 0.8f, 0.4f);
    private static readonly Color SelectedItem = new Color(0.6f, 0.85f, 1f);
    private static readonly Color ComplyColor = new Color(0.55f, 0.95f, 0.55f);
    private static readonly Color ViolateColor = new Color(1f, 0.55f, 0.5f);

    private static DevModePanel instance;

    // GameTime은 씬마다 새로 생기므로 고른 배속은 여기에 두고 새 GameTime에 다시 적용한다.
    private static float speedFactor = 1f;

    private bool show;
    private bool collapsed;
    private int tab;
    private int dayInput = 1;
    private int hourInput = 1;
    private int space;
    private Vector2 scroll;

    private GameTime gameTime;
    private float baseMultiplier;

    private bool cursorWasVisible;
    private readonly List<Behaviour> lockedPlayers = new List<Behaviour>();
    private readonly List<string> log = new List<string>();

    private GUIStyle small;
    private GUIStyle bold;

    // ③ 탭에서 태블릿으로 보낼 메시지
    private static readonly string[] SampleMessages =
    {
        "복도를 점검하십시오",
        "1-1 교실을 점검하십시오",
        "과학실 소등을 확인하십시오",
        "화장실 칸을 확인하십시오",
        "경비실로 돌아오십시오"
    };
    private string messageText = string.Empty;
    private int messageSample;
    private int messageCounter;

    // IMGUI는 Layout과 입력 이벤트에서 컨트롤 수가 같아야 한다. 버튼 동작은 여기에 넣었다가 Update에서 실행한다.
    private readonly List<Action> pending = new List<Action>();

    // ─────────────────────────────── 자동 생성 ───────────────────────────────

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        instance = null;
        speedFactor = 1f;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (instance != null) return;

        GameObject go = new GameObject("__DevMode (runtime)");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<DevModePanel>();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private void Start()
    {
        dayInput = GameSession.CurrentDay;
        BindGameTime();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 이전 씬의 플레이어는 이미 파괴됐다. 새 씬의 플레이어는 열려 있으면 Update에서 다시 잡는다.
        lockedPlayers.Clear();
        gameTime = null;
        dayInput = GameSession.CurrentDay;
        BindGameTime();
    }

    private void Update()
    {
        if (TogglePressed()) SetShow(!show);

        RunPending();

        if (!show) return;
        if (gameTime == null) BindGameTime();
        HoldPlayers();
    }

    // ─────────────────────────────── 조작 ───────────────────────────────

    private static bool TogglePressed()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && (keyboard.minusKey.wasPressedThisFrame || keyboard.numpadMinusKey.wasPressedThisFrame)) return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.Minus) || Input.GetKeyDown(KeyCode.KeypadMinus)) return true;
#endif
        return false;
    }

    private void SetShow(bool value)
    {
        if (show == value) return;
        show = value;

        if (show)
        {
            cursorWasVisible = Cursor.visible;
            dayInput = GameSession.CurrentDay;
            BindGameTime();
            HoldPlayers();
        }
        else
        {
            ReleasePlayers();
        }
    }

    /// <summary>
    /// 켜져 있는 FPController를 끈다. DayIntro가 인트로 뒤에 다시 켜는 경우가 있어 열려 있는 동안 매 프레임 확인한다.
    /// </summary>
    private void HoldPlayers()
    {
        foreach (FPController player in FindObjectsByType<FPController>(FindObjectsSortMode.None))
        {
            if (!player.enabled) continue;
            player.enabled = false;
            lockedPlayers.Add(player);
        }

        Cursor.visible = true;
    }

    private void ReleasePlayers()
    {
        bool restored = false;
        foreach (Behaviour player in lockedPlayers)
        {
            if (player == null) continue;
            player.enabled = true;   // FPController.OnEnable이 커서 표시를 원래대로 되돌린다
            restored = true;
        }
        lockedPlayers.Clear();

        if (!restored) Cursor.visible = cursorWasVisible;
    }

    private void BindGameTime()
    {
        if (gameTime != null) return;

        gameTime = FindAnyObjectByType<GameTime>();
        if (gameTime == null) return;

        baseMultiplier = gameTime.TimeMultiplier;
        if (!Mathf.Approximately(speedFactor, 1f))
        {
            gameTime.SetTimeMultiplier(baseMultiplier * speedFactor);
        }
    }

    private void RestartDay(int day)
    {
        GameSession.SetDay(day);
        dayInput = GameSession.CurrentDay;
        Note(GameSession.CurrentDay + "일차로 Play 씬 다시 시작");
        SceneFlow.GoTo(GameScene.Play, false);
    }

    private void DieNow()
    {
        if (FindAnyObjectByType<PlayResultRouter>() == null)
        {
            Note("이 씬에는 사망 처리(PlayResultRouter)가 없습니다.");
            return;
        }

        // 패널이 끈 플레이어를 먼저 돌려준 뒤 사망 처리가 다시 끄게 한다 (닫을 때 되살아나지 않도록)
        SetShow(false);
        Note("즉시 사망 → 사망 화면");
        EventBus.RaiseAxisCritical(FearAxis.Auditory);
    }

    private void SetSpeed(float factor)
    {
        speedFactor = factor;
        if (gameTime == null)
        {
            Note("배속 ×" + factor + " — 게임 시간이 있는 씬에 들어가면 적용");
            return;
        }

        gameTime.SetTimeMultiplier(baseMultiplier * factor);
        Note("배속 ×" + factor + " (현실 1초 = 게임 " + gameTime.TimeMultiplier.ToString("0.#") + "초)");
    }

    private void JumpTo(int hour)
    {
        if (gameTime == null)
        {
            Note("이 씬에는 게임 시간(GameTime)이 없습니다.");
            return;
        }

        if (gameTime.IsEnded)
        {
            Note("이미 근무가 끝나 시각을 옮길 수 없습니다.");
            return;
        }

        bool clamped = gameTime.JumpToHour(hour);
        Note(clamped
            ? hour + "시는 근무 종료 이후 → 종료 시각 " + gameTime.CurrentTimeText + "으로 이동 (시계가 흐르면 근무 종료)"
            : hour + "시로 이동 → " + gameTime.CurrentTimeText);
    }

    private void Placeholder(string action)
    {
        Note("(연결 예정) " + SpaceNames[space] + " · " + action);
    }

    private void Note(string line)
    {
        Debug.Log("[DevMode] " + line, this);
        log.Add(line);
        while (log.Count > MaxLog) log.RemoveAt(0);
    }

    private void Later(Action action)
    {
        pending.Add(action);
    }

    private void RunPending()
    {
        if (pending.Count == 0) return;
        Action[] actions = pending.ToArray();
        pending.Clear();
        for (int i = 0; i < actions.Length; i++)
        {
            try
            {
                actions[i]();
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
            }
        }
    }

    // ─────────────────────────────── 화면 ───────────────────────────────

    private void OnGUI()
    {
        if (!show) return;
        if (small == null)
        {
            small = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true };
            bold = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold };
        }

        float scale = Mathf.Clamp(Screen.width * 0.35f / PanelWidth, 0.4f, 1.1f);
        Matrix4x4 saved = GUI.matrix;
        GUIUtility.ScaleAroundPivot(new Vector2(scale, scale), Vector2.zero);

        float w = Screen.width / scale;
        float h = Screen.height / scale;
        try
        {
            GUILayout.BeginArea(new Rect(w - PanelWidth - 10f, 10f, PanelWidth, h - 20f));
            GUILayout.BeginVertical(GUI.skin.box);

            DrawHeader();
            if (!collapsed)
            {
                DrawStatus();
                DrawTabs();
                scroll = GUILayout.BeginScrollView(scroll);
                if (tab == 0) DrawDayTab();
                else if (tab == 1) DrawTimeTab();
                else DrawInstructionTab();
                DrawLog();
                GUILayout.EndScrollView();
            }

            GUILayout.EndVertical();
            GUILayout.EndArea();
        }
        finally
        {
            GUI.matrix = saved;
        }
    }

    private void DrawHeader()
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label("개발자 모드", bold, GUILayout.Width(120));
        GUILayout.FlexibleSpace();
        if (GUILayout.Button(collapsed ? "펼치기" : "접기", GUILayout.Width(56))) Later(() => collapsed = !collapsed);
        if (GUILayout.Button("닫기(-)", GUILayout.Width(70))) Later(() => SetShow(false));
        GUILayout.EndHorizontal();
    }

    private void DrawStatus()
    {
        string clock;
        if (gameTime == null)
        {
            clock = "게임 시간 없음";
        }
        else
        {
            string state = gameTime.IsEnded ? " (종료)" : gameTime.IsRunning ? string.Empty : " (멈춤)";
            clock = gameTime.CurrentTimeText + state + " · 배속 ×" + speedFactor + " (x" + gameTime.TimeMultiplier.ToString("0.#") + ")";
        }

        GUILayout.Label(GameSession.CurrentDay + "일차 · " + clock, small);
    }

    private void DrawTabs()
    {
        GUILayout.BeginHorizontal();
        for (int i = 0; i < TabNames.Length; i++)
        {
            int index = i;
            Color saved = GUI.backgroundColor;
            if (tab == i) GUI.backgroundColor = SelectedTab;
            if (GUILayout.Button(TabNames[i], GUILayout.Height(26))) Later(() => tab = index);
            GUI.backgroundColor = saved;
        }
        GUILayout.EndHorizontal();
    }

    // ─────────────────────────────── ① 일차 ───────────────────────────────

    private void DrawDayTab()
    {
        GUILayout.Label("일차를 바꾸면 Play 씬을 처음부터 다시 불러옵니다(Day 인트로부터). 결과창은 거치지 않습니다.", small);

        GUILayout.BeginHorizontal();
        for (int day = 1; day <= GameSession.FinalDay; day++)
        {
            int target = day;
            Color saved = GUI.backgroundColor;
            if (GameSession.CurrentDay == day) GUI.backgroundColor = SelectedItem;
            if (GUILayout.Button("Day " + day, GUILayout.Height(24))) Later(() => RestartDay(target));
            GUI.backgroundColor = saved;
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("일차", GUILayout.Width(30));
        if (GUILayout.Button("−", GUILayout.Width(24))) Later(() => dayInput = Mathf.Max(1, dayInput - 1));
        GUILayout.Label(dayInput.ToString(), GUILayout.Width(20));
        if (GUILayout.Button("+", GUILayout.Width(24))) Later(() => dayInput = Mathf.Min(GameSession.FinalDay, dayInput + 1));
        if (GUILayout.Button("이 날로 다시 시작")) Later(() => RestartDay(dayInput));
        GUILayout.EndHorizontal();

        GUILayout.Space(6);
        GUILayout.Label("사망", bold);
        GUILayout.Label("페이드 후 사망 화면(YOU DIED / Restart / Quit)을 띄웁니다. 패널은 닫힙니다.", small);
        Color savedColor = GUI.backgroundColor;
        GUI.backgroundColor = ViolateColor;
        if (GUILayout.Button("즉시 사망 ▶", GUILayout.Height(26))) Later(() => DieNow());
        GUI.backgroundColor = savedColor;
    }

    // ─────────────────────────────── ② 게임 시간 ───────────────────────────────

    private void DrawTimeTab()
    {
        if (gameTime == null)
        {
            GUILayout.Label("이 씬에는 게임 시간(GameTime)이 없습니다. Play 씬에서 쓸 수 있습니다.", small);
        }
        else
        {
            GUILayout.Label("근무 시간 " + gameTime.StartTimeText + " → " + gameTime.EndTimeText
                            + " · 기본 배속 x" + baseMultiplier.ToString("0.#"), small);
        }

        GUILayout.Label("시간 빠르게", bold);
        GUILayout.BeginHorizontal();
        for (int i = 0; i < SpeedFactors.Length; i++)
        {
            float factor = SpeedFactors[i];
            Color saved = GUI.backgroundColor;
            if (Mathf.Approximately(speedFactor, factor)) GUI.backgroundColor = SelectedItem;
            if (GUILayout.Button(i == 0 ? "기본" : "×" + factor, GUILayout.Height(24))) Later(() => SetSpeed(factor));
            GUI.backgroundColor = saved;
        }
        GUILayout.EndHorizontal();
        GUILayout.Label("고른 배속은 일차를 바꾸거나 씬을 다시 불러와도 유지됩니다.", small);

        GUILayout.Label("시각 이동 (정각)", bold);
        GUI.enabled = gameTime != null;
        GUILayout.BeginHorizontal();
        for (int i = 0; i < QuickHours.Length; i++)
        {
            int hour = QuickHours[i];
            if (GUILayout.Button(hour + "시", GUILayout.Height(24))) Later(() => JumpTo(hour));
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("시", GUILayout.Width(30));
        if (GUILayout.Button("−", GUILayout.Width(24))) Later(() => hourInput = (hourInput + 23) % 24);
        GUILayout.Label(hourInput.ToString(), GUILayout.Width(20));
        if (GUILayout.Button("+", GUILayout.Width(24))) Later(() => hourInput = (hourInput + 1) % 24);
        if (GUILayout.Button("이 시각으로 이동")) Later(() => JumpTo(hourInput));
        GUILayout.EndHorizontal();
        GUILayout.Label("12시는 자정으로 봅니다. 근무 종료 시각 이후를 고르면 종료 시각으로 이동해 근무가 끝납니다.", small);

        GUI.enabled = true;
    }

    // ─────────────────────────────── ③ 지시 사항 ───────────────────────────────

    private void DrawInstructionTab()
    {
        DrawMessageSender();

        GUILayout.Space(8);
        GUILayout.Label("Lee님의 판정 시스템을 연결할 자리입니다. 지금은 버튼만 있고 누르면 기록에 '연결 예정'만 남습니다.", small);

        GUILayout.BeginHorizontal();
        for (int i = 0; i < SpaceNames.Length; i++)
        {
            int index = i;
            Color saved = GUI.backgroundColor;
            if (space == i) GUI.backgroundColor = SelectedItem;
            if (GUILayout.Button(SpaceNames[i], GUILayout.Height(24))) Later(() => space = index);
            GUI.backgroundColor = saved;
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginVertical(GUI.skin.box);
        GUILayout.Label(SpaceNames[space] + " 지시 사항", bold);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("지시 사항 확인")) Later(() => Placeholder("지시 사항 확인"));
        Color savedColor = GUI.backgroundColor;
        GUI.backgroundColor = ComplyColor;
        if (GUILayout.Button("지키기 ▶", GUILayout.Width(80))) Later(() => Placeholder("지키기"));
        GUI.backgroundColor = ViolateColor;
        if (GUILayout.Button("어기기 ▶", GUILayout.Width(80))) Later(() => Placeholder("어기기"));
        GUI.backgroundColor = savedColor;
        GUILayout.EndHorizontal();
        GUILayout.EndVertical();
    }

    /// <summary>
    /// 태블릿에 메시지를 보내 본다. 메시지가 들어가면 알람(진동·소리·화면 표시)도 같이 울린다.
    /// 알람이 꺼지는지 확인하려면 태블릿을 들고 메시지 탭을 보면 된다.
    /// </summary>
    private void DrawMessageSender()
    {
        GUILayout.BeginVertical(GUI.skin.box);
        GUILayout.Label("메시지 보내기 (태블릿 알람)", bold);

        TabletMessageList list = FindAnyObjectByType<TabletMessageList>();
        TabletAlarm alarm = FindAnyObjectByType<TabletAlarm>();

        if (list == null)
        {
            GUILayout.Label("이 씬에는 태블릿(HUD_Tablet)이 없습니다.", small);
            GUILayout.EndVertical();
            return;
        }

        GUILayout.BeginHorizontal();
        GUILayout.Label("내용", GUILayout.Width(36));
        messageText = GUILayout.TextField(messageText, GUILayout.MinWidth(150));
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("보내기 ▶", GUILayout.Height(24)))
        {
            string text = string.IsNullOrEmpty(messageText) ? SampleMessages[messageSample % SampleMessages.Length] : messageText;
            Later(() => SendDevMessage(list, text));
        }

        if (GUILayout.Button("예시 문구", GUILayout.Width(80), GUILayout.Height(24)))
        {
            Later(() =>
            {
                messageSample++;
                messageText = SampleMessages[messageSample % SampleMessages.Length];
            });
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("모두 읽음 처리", GUILayout.Height(22)))
        {
            Later(() =>
            {
                list.MarkAllRead();
                Note("메시지를 모두 읽음으로 표시했습니다. 탭 옆 점이 사라집니다.");
            });
        }
        if (GUILayout.Button("목록 비우기", GUILayout.Width(90), GUILayout.Height(22)))
        {
            Later(() =>
            {
                list.Clear();
                Note("메시지를 모두 비웠습니다.");
            });
        }
        GUILayout.EndHorizontal();

        string alarmState = alarm == null ? "알람 컴포넌트 없음" : (alarm.IsActive ? "알람 울리는 중" : "알람 꺼짐");
        GUILayout.Label("메시지 " + list.Count + "개 (안 읽음 " + list.UnreadCount + ") · " + alarmState, small);

        if (alarm != null && alarm.IsActive && GUILayout.Button("알람 강제로 끄기", GUILayout.Height(22)))
        {
            Later(() =>
            {
                alarm.Acknowledge();
                Note("알람을 강제로 껐습니다.");
            });
        }

        GUILayout.EndVertical();
    }

    private void SendDevMessage(TabletMessageList list, string text)
    {
        // 같은 id면 알람이 울리지 않으므로 보낼 때마다 새 id를 만든다.
        messageCounter++;
        list.Add("dev.message." + messageCounter, text);
        Note("메시지 보냄: " + text);
    }

    private void DrawLog()
    {
        GUILayout.Space(6);
        GUILayout.BeginHorizontal();
        GUILayout.Label("기록 (최신이 위)", bold);
        if (GUILayout.Button("지우기", GUILayout.Width(60))) Later(() => log.Clear());
        GUILayout.EndHorizontal();
        for (int i = log.Count - 1; i >= 0 && i >= log.Count - 10; i--)
        {
            GUILayout.Label(log[i], small);
        }
    }
}
#endif

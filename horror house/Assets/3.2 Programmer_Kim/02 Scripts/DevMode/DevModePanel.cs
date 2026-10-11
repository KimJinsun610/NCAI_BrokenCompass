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
/// 탭 여덟 개(두 줄):
/// - <b>① 일차</b>: 일차를 골라 Play 씬을 처음부터 다시 불러오기(김진선님) · 이 자리에서 그 일차로 빠르게 다시 열기 · 즉시 사망 · 축별 붙잡힘 · 재시작 · 밤 종료 · 손전등·배터리 · 공간 이동.
/// - <b>② 시간</b>: 배속 · 정각 이동(김진선님) + 밤 시계 구간 점프(판정 시작·슬롯 A·이완·슬롯 B·슬롯 C·판정 끝) · 판정 강제 · 자동 연출 · 시계 멈춤 · 문 잠금.
/// - <b>③ 축</b>: 네 축 막대(구간 경계선 · 연출 구간 · 방금 바뀐 양이 깜빡임) · ± 버튼 · 「무엇이 올렸는지」 변화 기록.
/// - <b>④ 조우</b>: 오늘 흐름(슬롯 A·B·C — 그 시각·자리로 가서 자연 발동, 또는 바로 실행) · 진행 중 연출의 단계(대기 → 전조 → 세움(보면 시작) → 대면 → 마무리 → 끝) · 조우 15개 전부.
/// - <b>⑤ 장면</b>: 과학실 인체 모형(자리·응시·목·다가옴) · 붙잡힘 장면 미리 보기(청각·조도·배치·인체 모형) · 가짜 놀람 · 피날레 몹.
/// - <b>⑥ 수칙</b>: 오늘 덱(방아쇠·위반) · 수칙마다 이동·단서·조우 · 덱에 추가 · 역설·회피 불가 · 정산 결과.
/// - <b>⑦ 점검</b>: 오늘 점검표 · 순차 지시 · 항목마다 이동·정상·이상 보고.
/// - <b>⑧ 메시지·기록</b>: 태블릿 메시지 보내기 · CSV 메시지 연출(김진선님) · 연출·수칙·점검·재시작 기록.
/// - <b>⑨ 상호작용</b>(71차): 지금 플레이어가 무엇을 보고·비추고·어디에 서 있는지, 들어온 판정 신호·정산·축 변화를 실시간으로.
///
/// 71차(이성현 — 민: 「개발자 모드를 켜면 원래 게임의 흐름이 멈추고, 수칙과 연출을 내가 직접 호출하고 상호작용을 확인할 수 있게」):
/// 패널을 열면 <b>흐름 정지</b>(<c>NightRun.Sandbox</c>)가 켜진다 — 게임 시계가 멈추고, 조우·수칙 단서·가짜 놀람·점검/근무 지시·역설 문자가 저절로 나오지 않으며,
/// 판정은 시각과 무관하게 늘 하고, 붙잡히지 않는다(축 99에서 멈춤). 패널을 닫아도 흐름 정지는 남는다(왼쪽 위 상호작용 확인 창).
/// 머리줄 버튼으로 흐름을 재개하면 다음에 패널을 열어도 자동으로 멈추지 않는다.
///
/// 씬 파일을 고치지 않습니다. 플레이를 시작하면 DontDestroyOnLoad 오브젝트로 자동으로 생기고,
/// 에디터와 Development Build에만 들어갑니다. 패널이 열려 있는 동안 FPController를 꺼서 시점이 돌지 않게 합니다.
/// 2026-10-08(64차, 이성현): 야간근무 디버그 콘솔(F3)을 없애고 이 패널에 합쳤습니다 — 야간근무 쪽 탭은 <c>DevModePanel.NightDuty.cs</c>.
/// </remarks>
public sealed partial class DevModePanel : MonoBehaviour
{
    private const float PanelWidth = 500f;
    private const int MaxLog = 120;

    private static readonly string[] TabNames = { "① 일차", "② 시간", "③ 축", "④ 조우", "⑤ 장면", "⑥ 수칙", "⑦ 점검·지시", "⑧ 메시지·기록", "⑨ 상호작용" };
    private const int TabsPerRow = 5;
    private static readonly float[] SpeedFactors = { 1f, 2f, 5f, 10f, 30f };
    private static readonly int[] QuickHours = { 1, 2, 3, 4, 5, 6 };

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
    private Vector2 scroll;

    private GameTime gameTime;
    private float baseMultiplier;

    private bool cursorWasVisible;
    private CursorLockMode cursorWasLock;
    private readonly List<Behaviour> lockedPlayers = new List<Behaviour>();
    private readonly List<string> log = new List<string>();

    private GUIStyle small;
    private GUIStyle bold;
    private GUIStyle rich;
    private GUIStyle section;
    private GUIStyle panelStyle;
    private Texture2D panelBg;
    private Texture2D sectionBg;

    // ⑧ 탭에서 태블릿으로 보낼 메시지
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

        // 개발용 기본값(옛 F3 콘솔에서 옮김): 에디터가 초점을 잃어도 플레이 루프가 멈추지 않게(알트탭 한 번에 게임이 얼던 문제). 런타임 값만.
        Application.runInBackground = true;

        GameObject go = new GameObject("__DevMode (runtime)");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<DevModePanel>();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        HookNightDuty(true);
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        HookNightDuty(false);
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
        if (panelBg != null) Destroy(panelBg);
        if (sectionBg != null) Destroy(sectionBg);
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

        TrackAxes();   // 패널이 닫혀 있어도 축 변화는 기록한다
        RunPending();
        RunAfterSettle();

        if (!show) return;
        if (gameTime == null) BindGameTime();
        HoldPlayers();
    }

    // ─────────────────────────────── 조작 ───────────────────────────────

    /// <summary>
    /// '-' 키가 눌렸는가. <b>입력 방식은 하나만 본다.</b>
    /// 두 방식(Input System · 기존 Input Manager)을 함께 보면, 같은 한 번의 입력이 서로 다른 프레임에
    /// 「눌림」으로 잡혀 열리자마자 닫히는 일이 생긴다. Input System이 있으면 그것만 보고, 없을 때만 기존 방식을 쓴다.
    /// </summary>
    private static bool TogglePressed()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null) return keyboard.minusKey.wasPressedThisFrame || keyboard.numpadMinusKey.wasPressedThisFrame;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.Minus) || Input.GetKeyDown(KeyCode.KeypadMinus);
#else
        return false;
#endif
    }

    private void SetShow(bool value)
    {
        if (show == value) return;
        show = value;

        if (show)
        {
            cursorWasVisible = Cursor.visible;
            cursorWasLock = Cursor.lockState;
            dayInput = GameSession.CurrentDay;
            BindGameTime();
            HoldPlayers();
            if (autoSandbox && !NightRun.Sandbox) SetSandbox(true);   // 71차: 개발자 모드를 켜면 흐름이 멈춘다
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

        Cursor.lockState = CursorLockMode.None;
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

        if (!restored)
        {
            Cursor.visible = cursorWasVisible;
            Cursor.lockState = cursorWasLock;
        }
    }

    private void BindGameTime()
    {
        if (gameTime != null) return;

        gameTime = FindAnyObjectByType<GameTime>();
        if (gameTime == null) return;

        baseMultiplier = gameTime.TimeMultiplier;
        if (NightRun.Sandbox) gameTime.Hold(SandboxHold);   // 71차: 씬을 다시 불러와도 흐름 정지 유지
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

    private void Note(string line)
    {
        Debug.Log("[DevMode] " + line, this);
        AddLog(line);
    }

    private void AddLog(string line)
    {
        int minute = NightRun.NightMinute;
        log.Add("<color=#888>[" + Clock(minute) + "]</color> " + line);
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

    private void EnsureStyles()
    {
        if (small != null) return;
        small = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true, richText = true };
        bold = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, richText = true };
        rich = new GUIStyle(GUI.skin.label) { fontSize = 13, richText = true, wordWrap = false };

        // 뒤의 HUD가 비치지 않게 불투명 배경(옛 콘솔: 「가독성이 떨어짐」).
        panelBg = new Texture2D(1, 1);
        panelBg.SetPixel(0, 0, new Color(0.07f, 0.07f, 0.08f, 0.97f));
        panelBg.Apply();
        sectionBg = new Texture2D(1, 1);
        sectionBg.SetPixel(0, 0, new Color(0.13f, 0.13f, 0.15f, 1f));
        sectionBg.Apply();

        panelStyle = new GUIStyle(GUI.skin.box) { padding = new RectOffset(8, 8, 8, 8) };
        panelStyle.normal.background = panelBg;
        section = new GUIStyle(GUI.skin.box) { padding = new RectOffset(8, 8, 6, 8), margin = new RectOffset(0, 0, 4, 4) };
        section.normal.background = sectionBg;
    }

    private void OnGUI()
    {
        if (!show && !NightRun.Sandbox && !monitorAlways) return;
        EnsureStyles();

        float scale = Mathf.Clamp(Screen.width * 0.36f / PanelWidth, 0.45f, 1.15f);
        Matrix4x4 saved = GUI.matrix;
        GUIUtility.ScaleAroundPivot(new Vector2(scale, scale), Vector2.zero);

        float w = Screen.width / scale;
        float h = Screen.height / scale;
        GUI.depth = -100;
        try
        {
            DrawMonitorOverlay(h);   // 71차: 왼쪽 위 상호작용 확인 창(흐름 정지 중이거나 패널이 열려 있을 때)
            if (!show) return;
            GUILayout.BeginArea(new Rect(w - PanelWidth - 10f, 10f, PanelWidth, h - 20f));
            GUILayout.BeginVertical(panelStyle);

            DrawHeader();
            if (!collapsed)
            {
                DrawStatus();
                DrawTabs();
                scroll = GUILayout.BeginScrollView(scroll);
                switch (tab)
                {
                    case 0: DrawDayTab(); break;
                    case 1: DrawTimeTab(); break;
                    case 2: DrawAxesTab(); break;
                    case 3: DrawEncounterTab(); break;
                    case 4: DrawSceneTab(); break;
                    case 5: DrawRulesTab(); break;
                    case 6: DrawInspectionTab(); break;
                    case 7: DrawMessageTab(); break;
                    default: DrawInteractionTab(); break;
                }
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
        GUILayout.Label("개발자 모드", bold, GUILayout.Width(96));
        if (NightRun.Sandbox)
        {
            if (ColorButton("■ 흐름 정지 중 — 재개", ViolateColor, GUILayout.Width(170))) Later(() => SetSandbox(false));
        }
        else
        {
            if (ColorButton("▶ 흐름 진행 중 — 정지", ComplyColor, GUILayout.Width(170))) Later(() => SetSandbox(true));
        }
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
            clock = gameTime.CurrentTimeText + state + " · 배속 ×" + speedFactor;
        }

        GUILayout.Label("<b>" + GameSession.CurrentDay + "일차</b> · " + clock + "   " + NightStatusLine(), small);
        DrawAxisStrip();
    }

    private void DrawTabs()
    {
        for (int row = 0; row * TabsPerRow < TabNames.Length; row++)
        {
            GUILayout.BeginHorizontal();
            for (int i = row * TabsPerRow; i < row * TabsPerRow + TabsPerRow && i < TabNames.Length; i++)
            {
                int index = i;
                Color saved = GUI.backgroundColor;
                if (tab == i) GUI.backgroundColor = SelectedTab;
                if (GUILayout.Button(TabNames[i], GUILayout.Height(26))) Later(() =>
                {
                    tab = index;
                    scroll = Vector2.zero;
                });
                GUI.backgroundColor = saved;
            }
            GUILayout.EndHorizontal();
        }
    }

    /// <summary>제목 있는 묶음 상자를 연다. 닫을 때는 <see cref="EndSection"/>.</summary>
    private void BeginSection(string title, string hint = null)
    {
        GUILayout.BeginVertical(section);
        GUILayout.Label("<color=#ffd27a>" + title + "</color>", bold);
        if (!string.IsNullOrEmpty(hint)) GUILayout.Label("<color=#9a9a9a>" + hint + "</color>", small);
    }

    private static void EndSection()
    {
        GUILayout.EndVertical();
    }

    private static bool ColorButton(string label, Color color, params GUILayoutOption[] options)
    {
        Color saved = GUI.backgroundColor;
        GUI.backgroundColor = color;
        bool pressed = GUILayout.Button(label, options);
        GUI.backgroundColor = saved;
        return pressed;
    }

    // ─────────────────────────────── ① 일차 ───────────────────────────────

    private void DrawDayTab()
    {
        BeginSection("일차 다시 시작", "Day 버튼은 Play 씬을 처음부터(Day 인트로부터) 다시 불러옵니다. 「이 자리에서」는 씬을 다시 부르지 않고 그 일차의 밤을 새로 편성합니다(빠름).");

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
        GUILayout.Label("이 자리에서", GUILayout.Width(70));
        for (int day = 1; day <= GameSession.FinalDay; day++)
        {
            int target = day;
            if (GUILayout.Button(day + "일차", GUILayout.Height(22))) Later(() => ReopenAsDay(target));
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("일차", GUILayout.Width(30));
        if (GUILayout.Button("−", GUILayout.Width(24))) Later(() => dayInput = Mathf.Max(1, dayInput - 1));
        GUILayout.Label(dayInput.ToString(), GUILayout.Width(20));
        if (GUILayout.Button("+", GUILayout.Width(24))) Later(() => dayInput = Mathf.Min(GameSession.FinalDay, dayInput + 1));
        if (GUILayout.Button("이 날로 다시 시작")) Later(() => RestartDay(dayInput));
        GUILayout.EndHorizontal();
        EndSection();

        BeginSection("사망 · 붙잡힘", "「즉시 사망」은 사망 화면(YOU DIED / Restart / Quit)으로 바로 갑니다. 축 버튼은 그 축을 100으로 올려 실제 붙잡힘 장면 → 재시작 카드까지 갑니다.");
        if (ColorButton("즉시 사망 ▶", ViolateColor, GUILayout.Height(26))) Later(() => DieNow());
        DrawCaptureButtons();
        EndSection();

        DrawNightControls();
    }

    // ─────────────────────────────── ② 시간 ───────────────────────────────

    private void DrawTimeTab()
    {
        BeginSection("게임 시간", gameTime == null
            ? "이 씬에는 게임 시간(GameTime)이 없습니다. Play 씬에서 쓸 수 있습니다."
            : "근무 시간 " + gameTime.StartTimeText + " → " + gameTime.EndTimeText + " · 기본 배속 x" + baseMultiplier.ToString("0.#"));

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
        EndSection();

        DrawNightClockControls();
    }

    // ─────────────────────────────── ⑧ 메시지·기록 ───────────────────────────────

    private void DrawMessageTab()
    {
        DrawMessageSender();
        DrawMessageEvents();
        DrawLog();
    }

    /// <summary>
    /// 태블릿에 메시지를 보내 본다. 메시지가 들어가면 알람(진동·소리·화면 표시)도 같이 울린다.
    /// 알람이 꺼지는지 확인하려면 태블릿을 들고 메시지 탭을 보면 된다.
    /// </summary>
    private void DrawMessageSender()
    {
        BeginSection("메시지 보내기 (태블릿 알람)");

        TabletMessageList list = FindAnyObjectByType<TabletMessageList>();
        TabletAlarm alarm = FindAnyObjectByType<TabletAlarm>();

        if (list == null)
        {
            GUILayout.Label("이 씬에는 태블릿(HUD_Tablet)이 없습니다.", small);
            EndSection();
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

        EndSection();
    }

    /// <summary>
    /// CSV(Resources/TabletMessageEvents.csv)에 적어 둔 메시지 연출 이벤트를 실행한다.
    /// 첫 메시지는 바로, 그 뒤로는 CSV에 적은 간격마다 하나씩 태블릿에 들어온다.
    /// </summary>
    private void DrawMessageEvents()
    {
        BeginSection("메시지 연출 이벤트 (CSV)");

        if (GUILayout.Button("CSV 다시 읽기", GUILayout.Width(100)))
        {
            Later(() =>
            {
                TabletMessageEvents.Reload();
                Note("CSV를 다시 읽었습니다. 이벤트 " + TabletMessageEvents.EventNames.Count + "개.");
            });
        }

        IReadOnlyList<string> names = TabletMessageEvents.EventNames;
        if (names.Count == 0)
        {
            GUILayout.Label("이벤트가 없습니다. Resources/" + TabletMessageEvents.ResourceName + ".csv 를 확인하십시오.", small);
        }

        for (int i = 0; i < names.Count; i++)
        {
            string name = names[i];
            TabletMessageEvents.EventData data = TabletMessageEvents.Get(name);
            bool playing = TabletMessageEvents.IsPlaying(name);

            GUILayout.BeginHorizontal();
            GUILayout.Label(name + "  (" + data.Messages.Length + "개 · " + data.Interval.ToString("0.##") + "초)", small);

            if (ColorButton(playing ? "멈춤" : "실행 ▶", playing ? ViolateColor : ComplyColor, GUILayout.Width(70), GUILayout.Height(22)))
            {
                if (playing)
                {
                    Later(() =>
                    {
                        TabletMessageEvents.Stop(name);
                        Note("이벤트 멈춤: " + name);
                    });
                }
                else
                {
                    Later(() =>
                    {
                        if (TabletMessageEvents.Play(name)) Note("이벤트 실행: " + name);
                        else Note("이벤트를 실행하지 못했습니다: " + name + " (콘솔 경고 확인)");
                    });
                }
            }
            GUILayout.EndHorizontal();
        }

        if (names.Count > 0 && GUILayout.Button("모두 멈춤", GUILayout.Height(22)))
        {
            Later(() =>
            {
                TabletMessageEvents.StopAll();
                Note("진행 중인 이벤트를 모두 멈췄습니다.");
            });
        }

        EndSection();
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
        BeginSection("기록 (최신이 위)", "연출(파랑) · 수칙(초록 지킴/빨강 위반) · 점검(노랑) · 재시작(보라) · 패널 조작");
        if (GUILayout.Button("지우기", GUILayout.Width(60))) Later(() => log.Clear());
        for (int i = log.Count - 1; i >= 0 && i >= log.Count - 60; i--)
        {
            GUILayout.Label(log[i], small);
        }
        EndSection();
    }
}
#endif

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using NightDuty;

/// <summary>
/// 판정 코어(<see cref="NightRun"/>)를 버튼으로 돌려 보는 디버그 패널입니다. 화면 오른쪽에 뜹니다.
/// </summary>
/// <remarks>
/// 탭 세 개:
/// - <b>카드 시험</b>(기본): 공간을 고르고 카드의 [지키기]/[어기기]를 누르면, 그 카드만 들어간 새 밤을 열고
///   <see cref="CardScenarios"/>의 행동을 재생한 뒤 결과를 보여 줍니다. 처음 보는 사람용.
/// - <b>직접 조작</b>: 아래 설명의 모든 버튼(신호를 하나씩 보내며 시험).
/// - <b>도움말</b>
/// </remarks>
/// <remarks>
/// <b>버릴 실험용 코드</b>입니다. 개인 폴더(Assembly-CSharp)에 있습니다.
///
/// 하는 일
/// - 새 회차 · 밤 시작 · 밤 종료 요청 · 포획 시험
/// - 판정 시간 흘리기(자동/+초). 시간이 흐르는 동안 0.1초마다 「응시 대상」으로 응시 샘플을 함께 보냅니다.
/// - 공간 들어가기/나오기, 점검 완료, 손전등, Tab
/// - 아무 신호나 골라 보내기(종류·대상·플래그·값·출처), 카드별 「시작 신호」 바로 보내기
/// - 카드 상태(대기·진행·준수·위반·미판정·잠금)와 4축, 정산 기록
///
/// 축 공급원
/// - 「슬라이더」: 기존처럼 DebugAxisDriver가 조명·이상현상 리그를 움직입니다.
/// - 「판정 코어」: DebugAxisDriver를 끄고, 판정 결과로 오른 축이 조명·이상현상 리그를 움직입니다.
///   밤을 시작하면 자동으로 이 모드가 됩니다.
///
/// 플레이 중 <c>_Test_AxisRig</c> 씬에 자동으로 생깁니다(씬 파일 수정 없음). F2로 숨기기/보이기.
/// </remarks>
[AddComponentMenu("NightDuty/Debug/NightRun Debug Panel")]
[DisallowMultipleComponent]
public sealed class NightRunDebugPanel : MonoBehaviour
{
    private const string SceneName = "_Test_AxisRig";
    private const float Step = 0.1f;
    private const float PanelWidth = 470f;

    /// <summary>판정 코어가 축 공급원인지. 이상현상 리그가 읽는다.</summary>
    public static bool CoreMode { get; private set; }

    private static readonly SpaceId[] Spaces =
    {
        SpaceId.Corridor, SpaceId.Toilet, SpaceId.Classroom_1_1, SpaceId.Classroom_1_3, SpaceId.ScienceRoom
    };

    private static readonly string[] AxisNames = { "청각", "조도", "배치", "신뢰" };

    private static readonly SignalKind[] SendableKinds =
    {
        SignalKind.DoorAutoOpenObserved, SignalKind.DoorCommandAccepted, SignalKind.DoorCloseCompleted,
        SignalKind.ZoneEntered, SignalKind.ZoneExited, SignalKind.PassageCompleted,
        SignalKind.ClueDelivered, SignalKind.ClueIdentified, SignalKind.SequenceEnded,
        SignalKind.ProximitySample, SignalKind.GazeSample, SignalKind.ModelObserved,
        SignalKind.SpaceEntered, SignalKind.SpaceExited, SignalKind.InspectionCompleted
    };

    [SerializeField] private bool _show = true;
    [SerializeField] private int _day = 1;

    private bool _collapsed;
    private int _tab;                          // 0 카드 시험 · 1 직접 조작 · 2 도움말
    private int _group;                        // 0 복도 · 1 교실 · 2 과학실 · 3 화장실
    private bool _slow = true;                 // 한 걸음씩 천천히 재생
    private string _openHelp = string.Empty;   // 설명을 펼친 카드 ID
    private readonly Dictionary<string, string> _cardResult = new Dictionary<string, string>();
    private readonly Dictionary<string, CardState> _cardState = new Dictionary<string, CardState>();
    private readonly Dictionary<string, RuleSO> _cardAssets = new Dictionary<string, RuleSO>();

    // 재생 중인 시나리오
    private CardScenario _run;
    private bool _runComply;
    private readonly List<ScenarioStep> _runSteps = new List<ScenarioStep>();
    private int _runIndex;
    private int _runTicksLeft = -1;
    private float _runPause;
    private RuleBook _runBook;
    private readonly int[] _runBefore = new int[4];
    private string _lastTitle = string.Empty;
    private string _lastResult = string.Empty;
    private CardState _lastState = CardState.Waiting;
    private readonly List<string> _lastSteps = new List<string>();

    private static readonly string[] GroupNames = { "복도", "교실", "과학실", "화장실" };
    private static readonly char[] GroupPrefix = { 'H', 'C', 'S', 'T' };
    private bool _autoTime;
    private float _accum;
    private float _judgeSeconds;
    private string _gazeTarget = string.Empty;
    private int _kindIndex;
    private string _targetId = string.Empty;
    private bool _flag = true;
    private string _valueText = "1.0";
    private bool _fromDirection;
    private Vector2 _scroll;
    private readonly List<string> _ids = new List<string>();
    private readonly List<string> _log = new List<string>();
    private RuleBook _hookedBook;
    private MonoBehaviour _driver;
    private GUIStyle _small;
    private GUIStyle _bold;
    private bool _quitting;

    // IMGUI는 Layout과 입력 이벤트에서 컨트롤 수가 같아야 한다. 버튼 동작은 여기에 넣었다가 Update에서 실행한다.
    private readonly List<Action> _pending = new List<Action>();

    private void Later(Action action)
    {
        _pending.Add(action);
    }

    private void RunPending()
    {
        if (_pending.Count == 0) return;
        Action[] actions = _pending.ToArray();
        _pending.Clear();
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

    // ─────────────────────────────── 자동 생성 ───────────────────────────────

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        CoreMode = false;
        if (SceneManager.GetActiveScene().name != SceneName) return;
        if (FindAnyObjectByType<NightRunDebugPanel>() != null) return;

        // 플레이 중에만 생기므로 씬에 저장되지 않는다. (DontSave를 붙이면 FindAnyObjectByType으로 찾을 수 없다)
        GameObject go = new GameObject("__NightRunDebug (runtime)");
        go.AddComponent<NightRunDebugPanel>();
    }

    private void Start()
    {
        GameObject host = GameObject.Find("__AxisTest");
        if (host != null)
        {
            // DebugAxisDriver는 에디터·디버그 빌드에만 있으므로 이름으로 찾는다.
            foreach (MonoBehaviour mb in host.GetComponents<MonoBehaviour>())
            {
                if (mb != null && mb.GetType().Name == "DebugAxisDriver")
                {
                    _driver = mb;
                    break;
                }
            }
        }

        if (_driver == null)
        {
            Debug.LogWarning("[NightRunDebug] __AxisTest의 DebugAxisDriver를 찾지 못했습니다. 판정 코어 모드에서도 슬라이더 방송이 섞일 수 있습니다.", this);
        }

        RebuildIds();
    }

    private void OnApplicationQuit()
    {
        _quitting = true;
    }

    private void OnDisable()
    {
        Unhook();
        if (_quitting)
        {
            // 플레이 종료 중에는 드라이버를 다시 켜서 방송시키지 않는다.
            CoreMode = false;
            return;
        }

        SetCoreMode(false);
    }

    private void Update()
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.F2)) _show = !_show;
#endif

        RunPending();
        HookBook();
        TickScenario();

        if (_autoTime && _run == null && NightRun.IsNightActive)
        {
            _accum += Time.deltaTime;
            int guard = 0;
            while (_accum >= Step && guard++ < 20)
            {
                _accum -= Step;
                StepOnce();
            }
        }
    }

    // ─────────────────────────────── 조작 ───────────────────────────────

    private void SetCoreMode(bool on)
    {
        if (CoreMode == on) return;
        CoreMode = on;
        if (_driver != null)
        {
            _driver.enabled = !on;   // 켜질 때 OnEnable에서 슬라이더 값을 다시 방송한다.
        }

        if (on)
        {
            // 조명·이상현상 리그가 슬라이더 구간에 머물지 않게 판정 코어의 현재 구간을 다시 보낸다.
            NightRun.DebugRebroadcast();
        }

        Note(on ? "축 공급원 → 판정 코어" : "축 공급원 → 슬라이더");
    }

    private void NewRun()
    {
        Unhook();
        NightRun.StartNewRun();
        _judgeSeconds = 0f;
        Note("새 회차 (4축 0)");
    }

    private void BeginNight()
    {
        SetCoreMode(true);
        _judgeSeconds = 0f;
        NightRun.BeginNight(_day, () => Mathf.FloorToInt(_judgeSeconds / 60f));
        HookBook();
        RebuildIds();

        RuleBook book = NightRun.CurrentBook;
        string targets = NightRun.TargetsInUse == null ? "참조 검사 건너뜀(씬 표식 없음)" : "씬 표식 " + NightRun.TargetsInUse.Count + "개로 검사";
        Note(NightRun.Day + "일차 밤 시작 — 카드 " + (book != null ? book.Watchers.Count : 0) + "장, " + targets);
    }

    private void EndNight()
    {
        RuleBook book = NightRun.CurrentBook;
        bool accepted = NightRun.RequestEndNight();
        if (book != null && _hookedBook == book)
        {
            // 밤 종료 정산 결과는 Settled로 이미 기록됐다.
            Unhook();
        }

        DaySummary s = NightRun.LastSummary;
        Note(accepted
            ? "밤 종료 수락 — 점검 " + s.PatrolDone + "/" + s.PatrolTotal + ", 위반 시각 " + s.ViolationMinutes.Count + "건"
            : "밤 종료 거절(진행 중인 밤 없음 또는 포획)");
    }

    private void StepOnce()
    {
        StepOnce(_gazeTarget);
    }

    private void StepOnce(string gaze)
    {
        RuleBook book = NightRun.CurrentBook;
        if (book != null && book.World.TabOpen)
        {
            return;   // Tab 중에는 판정 시간·시계가 멈춘다.
        }

        NightRun.Tick(Step);
        NightRun.Send(JudgeSignal.Gaze(gaze, Step));
        _judgeSeconds += Step;
    }

    private void Advance(float seconds)
    {
        int steps = Mathf.RoundToInt(seconds / Step);
        for (int i = 0; i < steps; i++) StepOnce();
        Note("시간 +" + seconds.ToString("0.0") + "초" + (_gazeTarget.Length > 0 ? " (응시 " + _gazeTarget + ")" : ""));
    }

    private void Send(JudgeSignal signal)
    {
        if (!NightRun.IsNightActive)
        {
            Note("밤이 시작되지 않아 무시: " + signal.Kind);
            return;
        }

        NightRun.Send(signal);
        Note("→ " + Describe(signal));
    }

    private void EnterSpace(SpaceId space)
    {
        RuleBook book = NightRun.CurrentBook;
        SpaceId current = book != null ? book.World.CurrentSpace : SpaceId.None;
        if (current != SpaceId.None && current != space)
        {
            Send(JudgeSignal.OfSpace(SignalKind.SpaceExited, current));
        }

        if (space != SpaceId.None && current != space)
        {
            Send(JudgeSignal.OfSpace(SignalKind.SpaceEntered, space));
        }
    }

    private void SendCustom()
    {
        SignalKind kind = SendableKinds[_kindIndex];
        float value;
        if (!float.TryParse(_valueText, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out value)) value = 0f;
        ActionSource source = _fromDirection ? ActionSource.Direction : ActionSource.Player;

        SpaceId space = SpaceId.None;
        if (!SignalCondition.UsesTarget(kind))
        {
            RuleBook book = NightRun.CurrentBook;
            space = book != null ? book.World.CurrentSpace : SpaceId.None;
        }

        Send(new JudgeSignal(kind, space, _targetId, source, _flag, value));
    }

    private void SendTrigger(RuleSO card)
    {
        SignalKind kind = card.TriggerKind;
        if (SignalCondition.UsesTarget(kind))
        {
            string id = card.TriggerId;
            if (id.Length == 0 || id == TargetMatchIds.Any)
            {
                id = card.TargetIds.Count > 0 ? card.TargetIds[0] : string.Empty;
            }

            bool flag = kind == SignalKind.DoorCommandAccepted;
            Send(new JudgeSignal(kind, card.TriggerSpace, id, ActionSource.Player, flag, 0f));
        }
        else
        {
            Send(JudgeSignal.OfSpace(kind, card.TriggerSpace));
        }
    }

    private void HookBook()
    {
        RuleBook book = NightRun.CurrentBook;
        if (book == _hookedBook) return;
        Unhook();
        if (book == null) return;
        _hookedBook = book;
        _hookedBook.Settled += OnSettled;
    }

    private void Unhook()
    {
        if (_hookedBook != null) _hookedBook.Settled -= OnSettled;
        _hookedBook = null;
    }

    private void OnSettled(RuleResult r)
    {
        string delta = r.HasDelta ? " " + AxisNames[(int)r.Axis] + " +" + r.Delta : "";
        Note("★ " + r.CardId + " " + StateName(r.State) + delta + "  " + r.Reason);
    }

    private void RebuildIds()
    {
        _ids.Clear();
        _ids.Add(string.Empty);
        List<string> refs = new List<string>();
        RuleBook book = NightRun.CurrentBook;
        if (book != null)
        {
            for (int i = 0; i < book.Watchers.Count; i++)
            {
                RuleSO card = book.Watchers[i].Card;
                refs.Clear();
                card.CollectReferences(refs);
                if (card.TriggerId.Length > 0) refs.Add(card.TriggerId);
                foreach (string id in refs)
                {
                    if (id != TargetMatchIds.Any && id != TargetMatchIds.Trigger && !_ids.Contains(id)) _ids.Add(id);
                }
            }
        }

        foreach (string id in JudgeTargetRegistry.Snapshot())
        {
            if (!_ids.Contains(id)) _ids.Add(id);
        }
    }

    private void Note(string line)
    {
        Debug.Log("[NightRunDebug] " + line, this);
        _log.Add(line);
        while (_log.Count > 40) _log.RemoveAt(0);
    }

    // ─────────────────────────────── 화면 ───────────────────────────────

    private void OnGUI()
    {
        if (!_show) return;
        if (_small == null)
        {
            _small = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true };
            _bold = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold };
        }

        float scale = Mathf.Clamp(Screen.width * 0.47f / PanelWidth, 0.4f, 1.1f);
        Matrix4x4 saved = GUI.matrix;
        GUIUtility.ScaleAroundPivot(new Vector2(scale, scale), Vector2.zero);

        float w = Screen.width / scale;
        float h = Screen.height / scale;
        try
        {
            GUILayout.BeginArea(new Rect(w - PanelWidth - 10f, 10f, PanelWidth, h - 20f));
            GUILayout.BeginVertical(GUI.skin.box);

            DrawHeader();
            if (!_collapsed)
            {
                DrawTabs();
                _scroll = GUILayout.BeginScrollView(_scroll);
                if (_tab == 0)
                {
                    DrawScenarioTab();
                }
                else if (_tab == 1)
                {
                    GUI.enabled = _run == null;   // 카드 시험 재생 중에는 직접 조작을 막는다
                    DrawNight();
                    DrawAxes();
                    DrawTimeAndPlace();
                    DrawSender();
                    DrawCards();
                    DrawLog();
                    GUI.enabled = true;
                }
                else
                {
                    DrawHelp();
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
        GUILayout.Label("판정 시험 패널", _bold, GUILayout.Width(120));
        GUILayout.FlexibleSpace();
        if (GUILayout.Button(_collapsed ? "펼치기" : "접기", GUILayout.Width(56))) Later(() => _collapsed = !_collapsed);
        if (GUILayout.Button("숨김(F2)", GUILayout.Width(70))) Later(() => _show = false);
        GUILayout.EndHorizontal();
    }

    private void DrawTabs()
    {
        GUILayout.BeginHorizontal();
        string[] names = { "① 카드 시험", "② 직접 조작", "③ 도움말" };
        for (int i = 0; i < names.Length; i++)
        {
            int index = i;
            Color saved = GUI.backgroundColor;
            if (_tab == i) GUI.backgroundColor = new Color(1f, 0.8f, 0.4f);
            if (GUILayout.Button(names[i], GUILayout.Height(26))) Later(() => _tab = index);
            GUI.backgroundColor = saved;
        }
        GUILayout.EndHorizontal();
    }

    // ─────────────────────────────── ① 카드 시험 ───────────────────────────────

    private void DrawScenarioTab()
    {
        GUILayout.Label("공간을 고르고, 카드의 [지키기] 또는 [어기기]를 누르세요. 그 카드만 넣은 새 밤을 열고 행동을 자동으로 재생한 뒤 결과를 보여 줍니다.", _small);

        GUILayout.BeginHorizontal();
        for (int i = 0; i < GroupNames.Length; i++)
        {
            int index = i;
            Color saved = GUI.backgroundColor;
            if (_group == i) GUI.backgroundColor = new Color(0.6f, 0.85f, 1f);
            if (GUILayout.Button(GroupNames[i] + " (" + GroupPrefix[i] + "1~6)", GUILayout.Height(24))) Later(() => _group = index);
            GUI.backgroundColor = saved;
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        _slow = GUILayout.Toggle(_slow, "천천히 보기 (조명·이상현상 변화를 보며 한 걸음씩)");
        GUILayout.EndHorizontal();

        DrawAxisBars();
        DrawLastRun();

        GUILayout.Space(6);
        IReadOnlyList<CardScenario> all = CardScenarios.All;
        for (int i = 0; i < all.Count; i++)
        {
            CardScenario sc = all[i];
            if (sc.CardId[0] != GroupPrefix[_group]) continue;
            DrawScenarioRow(sc);
        }
    }

    private void DrawScenarioRow(CardScenario sc)
    {
        bool busy = _run != null;
        GUILayout.BeginVertical(GUI.skin.box);
        GUILayout.BeginHorizontal();
        GUILayout.Label(sc.CardId + "  " + sc.Title, _bold, GUILayout.Width(210));
        GUI.enabled = !busy;
        Color saved = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.55f, 0.95f, 0.55f);
        if (GUILayout.Button("지키기 ▶", GUILayout.Width(80))) Later(() => StartScenario(sc, true));
        GUI.backgroundColor = new Color(1f, 0.55f, 0.5f);
        if (GUILayout.Button(sc.ViolateButton + " ▶", GUILayout.Width(80))) Later(() => StartScenario(sc, false));
        GUI.backgroundColor = saved;
        GUI.enabled = true;
        bool open = _openHelp == sc.CardId;
        if (GUILayout.Button(open ? "▲" : "?", GUILayout.Width(30))) Later(() => _openHelp = open ? string.Empty : sc.CardId);
        GUILayout.EndHorizontal();

        string result;
        if (_cardResult.TryGetValue(sc.CardId, out result))
        {
            Color c = GUI.color;
            GUI.color = StateColor(_cardState[sc.CardId]);
            GUILayout.Label(result, _small);
            GUI.color = c;
        }

        if (open)
        {
            RuleSO card = CardAsset(sc.CardId);
            if (card != null) GUILayout.Label("지침: " + card.PlayerText, _small);
            string need = sc.SetupValue > 0 ? "시작 조건: " + AxisNames[(int)sc.SetupAxis] + " 값 = " + sc.SetupValue + " (재생 전에 맞춤)\n" : string.Empty;
            GUILayout.Label(need + "지키기 = " + sc.ComplyText + "\n" + sc.ViolateButton + " = " + sc.ViolateText, _small);
        }

        GUILayout.EndVertical();
    }

    private void DrawAxisBars()
    {
        IFearAxisReader axes = NightRun.Axes;
        GUILayout.BeginHorizontal();
        for (int i = 0; i < 4; i++)
        {
            int value = axes.GetValue((FearAxis)i);
            GUILayout.BeginVertical(GUILayout.Width(105));
            GUILayout.Label(AxisNames[i] + " " + value + " (구간 " + (int)axes.GetBand((FearAxis)i) + ")", _small);
            Rect r = GUILayoutUtility.GetRect(100, 8);
            GUI.DrawTexture(r, Texture2D.grayTexture);
            Color saved = GUI.color;
            GUI.color = i == 3 ? new Color(0.5f, 0.9f, 0.5f) : new Color(1f, 0.5f, 0.3f);
            GUI.DrawTexture(new Rect(r.x, r.y, r.width * value / 100f, r.height), Texture2D.whiteTexture);
            GUI.color = saved;
            GUILayout.EndVertical();
        }
        GUILayout.EndHorizontal();
        GUILayout.Label("청각·조도·배치가 100이 되면 포획, 신뢰는 지켰을 때만 오릅니다.", _small);
    }

    private void DrawLastRun()
    {
        if (_run == null && _lastTitle.Length == 0) return;

        GUILayout.BeginVertical(GUI.skin.box);
        if (_run != null)
        {
            GUILayout.Label("재생 중: " + _lastTitle, _bold);
        }
        else
        {
            GUILayout.Label("마지막 시험: " + _lastTitle, _bold);
            Color c = GUI.color;
            GUI.color = StateColor(_lastState);
            GUILayout.Label(_lastResult, _bold);
            GUI.color = c;
        }

        for (int i = 0; i < _lastSteps.Count; i++)
        {
            GUILayout.Label((i + 1) + ". " + _lastSteps[i], _small);
        }

        GUILayout.EndVertical();
    }

    private RuleSO CardAsset(string id)
    {
        RuleSO card;
        if (_cardAssets.TryGetValue(id, out card)) return card;

        NightDeckTableSO table = Resources.Load<NightDeckTableSO>(NightDeckTableSO.ResourcePath);
        if (table != null)
        {
            for (int day = 1; day <= table.DayCount; day++)
            {
                foreach (RuleSO c in table.DeckFor(day))
                {
                    if (c != null && !_cardAssets.ContainsKey(c.CardId)) _cardAssets[c.CardId] = c;
                }
            }
        }

        _cardAssets.TryGetValue(id, out card);
        return card;
    }

    private void StartScenario(CardScenario sc, bool comply)
    {
        if (_run != null) return;
        RuleSO card = CardAsset(sc.CardId);
        if (card == null)
        {
            Note(sc.CardId + " 카드 에셋을 편성표에서 찾지 못했습니다. NightDuty ▸ 모든 공간 카드 에셋 생성을 실행하세요.");
            return;
        }

        // 새 회차 · 축 0 · 이 카드만 넣은 밤
        SetCoreMode(true);
        Unhook();
        NightRun.StartNewRun();
        _judgeSeconds = 0f;
        if (sc.SetupValue > 0) NightRun.DebugAddAxis(sc.SetupAxis, sc.SetupValue);

        NightRun.DeckOverride = d => new List<RuleSO> { card };
        try
        {
            NightRun.BeginNight(_group + 1, () => Mathf.FloorToInt(_judgeSeconds / 60f));
        }
        finally
        {
            NightRun.DeckOverride = null;
        }

        NightRun.DebugRebroadcast();
        HookBook();
        _runBook = NightRun.CurrentBook;
        for (int i = 0; i < 4; i++) _runBefore[i] = NightRun.Axes.GetValue((FearAxis)i);

        _run = sc;
        _runComply = comply;
        _runSteps.Clear();
        _runSteps.AddRange(comply ? sc.Comply : sc.Violate);
        _runIndex = 0;
        _runTicksLeft = -1;
        _runPause = 0f;
        _lastTitle = sc.CardId + " " + sc.Title + " — " + (comply ? "지키기" : sc.ViolateButton);
        _lastSteps.Clear();
        if (sc.SetupValue > 0) _lastSteps.Add("(준비) " + AxisNames[(int)sc.SetupAxis] + " 값 = " + sc.SetupValue + " (카드 시작 조건)");
        Note("▶ " + _lastTitle);

        if (!_slow)
        {
            while (_run != null) AdvanceScenario(float.MaxValue);
        }
    }

    private void TickScenario()
    {
        if (_run == null || !_slow) return;
        AdvanceScenario(Time.deltaTime);
    }

    /// <summary>시나리오를 진행한다. dt가 매우 크면 끝까지 한 번에 돈다.</summary>
    private void AdvanceScenario(float dt)
    {
        bool instant = dt >= float.MaxValue;
        if (_runPause > 0f && !instant)
        {
            _runPause -= dt;
            return;
        }

        if (_runIndex >= _runSteps.Count)
        {
            FinishScenario();
            return;
        }

        ScenarioStep step = _runSteps[_runIndex];
        if (step.Kind == ScenarioStepKind.Wait)
        {
            if (_runTicksLeft < 0)
            {
                _runTicksLeft = Mathf.RoundToInt(step.Seconds / Step);   // 0.1초 단위 정수로 센다
                _accum = 0f;
                _lastSteps.Add(step.Label);
            }

            if (instant)
            {
                while (_runTicksLeft > 0)
                {
                    _runTicksLeft--;
                    StepOnce(step.GazeTarget);
                }
            }
            else
            {
                _accum += dt;
                while (_accum >= Step && _runTicksLeft > 0)
                {
                    _accum -= Step;
                    _runTicksLeft--;
                    StepOnce(step.GazeTarget);
                }
            }

            if (_runTicksLeft <= 0)
            {
                _runTicksLeft = -1;
                _runIndex++;
            }

            return;
        }

        _lastSteps.Add(step.Label);
        if (step.Kind == ScenarioStepKind.Signal)
        {
            if (NightRun.IsNightActive) NightRun.Send(step.Signal);
        }
        else
        {
            NightRun.RequestEndNight();
        }

        _runIndex++;
        _runPause = 0.6f;
    }

    private void FinishScenario()
    {
        CardScenario sc = _run;
        if (NightRun.IsNightActive && !NightRun.IsCaptured)
        {
            NightRun.RequestEndNight();   // 밤 종료 정산 카드도 결과가 나오게
        }

        RuleWatcher w = _runBook != null && _runBook.Watchers.Count > 0 ? _runBook.Watchers[0] : null;
        CardState state = w != null ? w.State : CardState.Undetermined;

        string delta = string.Empty;
        for (int i = 0; i < 4; i++)
        {
            int d = NightRun.Axes.GetValue((FearAxis)i) - _runBefore[i];
            if (d > 0) delta += (delta.Length > 0 ? ", " : "") + AxisNames[i] + " +" + d;
        }

        string verdict;
        switch (state)
        {
            case CardState.Complied: verdict = "[지킴] 준수"; break;
            case CardState.Violated: verdict = "[어김] 위반"; break;
            case CardState.Undetermined: verdict = "[판정 없음] 미판정"; break;
            case CardState.Locked: verdict = "[잠김]"; break;
            default: verdict = StateName(state); break;
        }

        string text = verdict + (delta.Length > 0 ? " · " + delta : " · 축 변화 없음");
        if (NightRun.IsCaptured) text += " · 100 도달 → 포획!";

        bool expected = state == (_runComply ? CardState.Complied : sc.ViolateExpect);
        if (!expected) text += "  (주의: 예상과 다름 — " + (_runComply ? "준수" : StateName(sc.ViolateExpect)) + " 기대)";

        _lastResult = text;
        _lastState = state;
        _cardResult[sc.CardId] = (_runComply ? "지키기" : sc.ViolateButton) + " → " + text;
        _cardState[sc.CardId] = state;
        Note("= " + sc.CardId + " " + text);

        _run = null;
        _runBook = null;
    }

    // ─────────────────────────────── ③ 도움말 ───────────────────────────────

    private void DrawHelp()
    {
        GUILayout.Label("이 패널은 무엇인가요?", _bold);
        GUILayout.Label("플레이어·씬 없이, 근무수칙 카드가 기획서대로 판정되는지 확인하는 도구입니다. 결과는 화면의 조명과 왼쪽 이상현상 패널에도 반영됩니다.", _small);

        GUILayout.Label("① 카드 시험 (처음이라면 여기)", _bold);
        GUILayout.Label("1. 위쪽에서 공간(복도·교실·과학실·화장실)을 고릅니다.\n"
                        + "2. 카드 줄의 [지키기 ▶] 또는 [어기기 ▶]를 누릅니다.\n"
                        + "3. 네 축이 0인 새 밤이 열리고, 그 카드만 들어간 상태에서 행동이 차례로 재생됩니다.\n"
                        + "4. '마지막 시험' 칸에 재생한 행동과 결과(지킴·어김, 오른 축)가 나옵니다.\n"
                        + "5. [?]를 누르면 지침 문구와 각 버튼이 무슨 행동인지 볼 수 있습니다.", _small);

        GUILayout.Label("색과 말", _bold);
        GUILayout.Label("초록 = 지킴(준수, 신뢰가 오름) · 빨강 = 어김(위반, 청각·조도·배치 중 하나가 오름) · 회색 = 판정 없음 · 노랑 = 진행 중\n"
                        + "축은 줄지 않습니다. 청각·조도·배치 중 하나라도 100이 되면 포획입니다.\n"
                        + "'주의: 예상과 다름'이 보이면 카드 데이터가 바뀌었다는 뜻이니 알려 주세요.", _small);

        GUILayout.Label("② 직접 조작 (익숙해진 뒤)", _bold);
        GUILayout.Label("일차 전체 덱으로 밤을 열고, 공간 이동·손전등·시간·신호를 하나씩 보내며 시험합니다. "
                        + "'축: 판정 코어/슬라이더'로 조명을 무엇이 움직일지 고를 수 있습니다.", _small);

        GUILayout.Label("단축키", _bold);
        GUILayout.Label("F2 = 이 패널 숨기기/보이기 · F1 = 왼쪽 이상현상 패널", _small);
    }

    private void DrawNight()
    {
        GUILayout.BeginHorizontal();
        if (GUILayout.Button(CoreMode ? "조명 기준: 판정 코어" : "조명 기준: 슬라이더", GUILayout.Width(150))) Later(() => SetCoreMode(!CoreMode));
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("일차", GUILayout.Width(30));
        if (GUILayout.Button("−", GUILayout.Width(24))) Later(() => _day = Mathf.Max(1, _day - 1));
        GUILayout.Label(_day.ToString(), GUILayout.Width(20));
        if (GUILayout.Button("+", GUILayout.Width(24))) Later(() => _day = Mathf.Min(9, _day + 1));
        if (GUILayout.Button("새 회차")) Later(() => NewRun());
        if (GUILayout.Button("밤 시작")) Later(() => BeginNight());
        GUI.enabled = NightRun.IsNightActive;
        if (GUILayout.Button("밤 종료 요청")) Later(() => EndNight());
        GUI.enabled = _run == null;
        GUILayout.EndHorizontal();

        RuleBook book = NightRun.CurrentBook;
        string state = NightRun.IsCaptured
            ? "포획됨 — " + NightRun.Cause
            : (book != null ? NightRun.Day + "일차 진행 중" : "밤 아님");
        string where = book != null ? SpaceName(book.World.CurrentSpace) : "-";
        string light = book != null && book.World.FlashlightOn ? "On" : "Off";
        string tab = book != null && book.World.TabOpen ? "열림" : "닫힘";
        GUILayout.Label(state + " · 공간 " + where + " · 손전등 " + light + " · Tab " + tab
                        + " · 판정 " + _judgeSeconds.ToString("0.0") + "초", _small);
    }

    private void DrawAxes()
    {
        IFearAxisReader axes = NightRun.Axes;
        GUILayout.BeginHorizontal();
        for (int i = 0; i < 4; i++)
        {
            FearAxis axis = (FearAxis)i;
            GUILayout.Label(AxisNames[i] + " " + axes.GetValue(axis) + "·B" + (int)axes.GetBand(axis), _small, GUILayout.Width(78));
            if (GUILayout.Button("+", GUILayout.Width(22))) Later(() => NightRun.DebugAddAxis(axis, NextBandGap(axes.GetValue(axis))));
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("포획 시험", _small, GUILayout.Width(60));
        for (int i = 0; i < 4; i++)
        {
            FearAxis target = (FearAxis)i;   // 람다가 루프 변수를 붙잡지 않게 복사
            if (GUILayout.Button(AxisNames[i] + " 100")) Later(() => { NightRun.DebugForceCapture(target); Note(AxisNames[(int)target] + " 100으로 올림"); });
        }
        GUILayout.EndHorizontal();
    }

    private void DrawTimeAndPlace()
    {
        GUILayout.BeginHorizontal();
        _autoTime = GUILayout.Toggle(_autoTime, "시간 자동", GUILayout.Width(80));
        if (GUILayout.Button("+0.5초")) Later(() => Advance(0.5f));
        if (GUILayout.Button("+1초")) Later(() => Advance(1f));
        if (GUILayout.Button("+2초")) Later(() => Advance(2f));
        if (GUILayout.Button("+3초")) Later(() => Advance(3f));
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("응시 대상", _small, GUILayout.Width(60));
        _gazeTarget = Cycler(_gazeTarget, 250);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        for (int i = 0; i < Spaces.Length; i++)
        {
            SpaceId space = Spaces[i];
            if (GUILayout.Button(SpaceName(space))) Later(() => EnterSpace(space));
        }
        if (GUILayout.Button("밖으로")) Later(() => EnterSpace(SpaceId.None));
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        RuleBook book = NightRun.CurrentBook;
        bool lightOn = book != null && book.World.FlashlightOn;
        bool tabOpen = book != null && book.World.TabOpen;
        if (GUILayout.Button(lightOn ? "손전등 끄기" : "손전등 켜기")) Later(() => Send(JudgeSignal.Flashlight(!lightOn)));
        if (GUILayout.Button(tabOpen ? "Tab 닫기" : "Tab 열기")) Later(() => Send(JudgeSignal.Tab(!tabOpen)));
        if (GUILayout.Button("점검 완료(현재 공간)") && book != null)
        {
            Later(() => Send(JudgeSignal.OfSpace(SignalKind.InspectionCompleted, book.World.CurrentSpace)));
        }
        GUILayout.EndHorizontal();
    }

    private void DrawSender()
    {
        GUILayout.Space(4);
        GUILayout.Label("신호 보내기", _bold);

        GUILayout.BeginHorizontal();
        GUILayout.Label("종류", _small, GUILayout.Width(40));
        if (GUILayout.Button("◀", GUILayout.Width(26))) Later(() => _kindIndex = (_kindIndex + SendableKinds.Length - 1) % SendableKinds.Length);
        GUILayout.Label(SendableKinds[_kindIndex].ToString(), GUILayout.Width(170));
        if (GUILayout.Button("▶", GUILayout.Width(26))) Later(() => _kindIndex = (_kindIndex + 1) % SendableKinds.Length);
        GUILayout.EndHorizontal();

        SignalKind kind = SendableKinds[_kindIndex];
        if (SignalCondition.UsesTarget(kind))
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("대상", _small, GUILayout.Width(40));
            _targetId = Cycler(_targetId, 180);
            _targetId = GUILayout.TextField(_targetId, GUILayout.Width(130));
            GUILayout.EndHorizontal();
        }
        else
        {
            GUILayout.Label("공간 신호 — 현재 공간으로 보냅니다.", _small);
        }

        GUILayout.BeginHorizontal();
        _flag = GUILayout.Toggle(_flag, "플래그(닫기 명령)", GUILayout.Width(130));
        _fromDirection = GUILayout.Toggle(_fromDirection, "출처=연출", GUILayout.Width(90));
        GUILayout.Label("값", _small, GUILayout.Width(18));
        _valueText = GUILayout.TextField(_valueText, GUILayout.Width(50));
        if (GUILayout.Button("보내기")) Later(() => SendCustom());
        GUILayout.EndHorizontal();
        GUILayout.Label("값 = 근접 거리(m) / 응시 샘플 초. 문 명령의 플래그 켜짐 = 닫기.", _small);
    }

    private void DrawCards()
    {
        GUILayout.Space(4);
        GUILayout.Label("카드", _bold);
        RuleBook book = NightRun.CurrentBook;
        if (book == null)
        {
            GUILayout.Label("밤을 시작하면 오늘 덱이 여기에 나옵니다.", _small);
            return;
        }

        IFearAxisReader axes = NightRun.Axes;
        for (int i = 0; i < book.Watchers.Count; i++)
        {
            RuleWatcher w = book.Watchers[i];
            RuleSO c = w.Card;
            Color saved = GUI.color;
            GUI.color = StateColor(w.State);
            GUILayout.BeginHorizontal();
            string elig = c.IsEligible(axes) ? "" : " (자격 밖)";
            GUILayout.Label(c.CardId + " " + StateName(w.State) + (c.IsLongTerm ? " · 장기" : "") + elig, _bold, GUILayout.Width(170));
            GUI.color = saved;
            GUI.enabled = w.State == CardState.Waiting && c.TriggerKind != SignalKind.NightBegan;
            if (GUILayout.Button("시작 신호", GUILayout.Width(80))) Later(() => SendTrigger(c));
            GUI.enabled = _run == null;
            GUILayout.EndHorizontal();

            string trigger = c.TriggerKind + " " + (SignalCondition.UsesTarget(c.TriggerKind) ? c.TriggerId : SpaceName(c.TriggerSpace));
            string detail = "시작: " + trigger;
            if (c.FailureCondition != null) detail += "\n위반: " + c.FailureCondition.Describe() + " → " + AxisNames[(int)c.FailureAxis] + " +" + c.FailureDelta;
            if (c.SuccessCondition != null) detail += "\n준수: " + c.SuccessCondition.Describe() + " → 신뢰 +" + c.SuccessDelta
                                                      + (c.SettleAt == SettleAt.AtNightEnd ? " (밤 종료 정산)" : "");
            if (!string.IsNullOrEmpty(w.Reason)) detail += "\n사유: " + w.Reason;
            GUILayout.Label(detail, _small);
        }
    }

    private void DrawLog()
    {
        GUILayout.Space(4);
        GUILayout.BeginHorizontal();
        GUILayout.Label("기록 (최신이 위)", _bold);
        if (GUILayout.Button("지우기", GUILayout.Width(60))) Later(() => _log.Clear());
        GUILayout.EndHorizontal();
        for (int i = _log.Count - 1; i >= 0 && i >= _log.Count - 15; i--)
        {
            GUILayout.Label(_log[i], _small);
        }
    }

    /// <summary>ID 목록을 ◀▶로 고르는 칸.</summary>
    private string Cycler(string current, float width)
    {
        if (_ids.Count == 0) RebuildIds();
        int index = Mathf.Max(0, _ids.IndexOf(current));
        if (GUILayout.Button("◀", GUILayout.Width(26))) { index = (index + _ids.Count - 1) % _ids.Count; current = _ids[index]; }
        GUILayout.Label(current.Length == 0 ? "(없음)" : current, _small, GUILayout.Width(width - 60f));
        if (GUILayout.Button("▶", GUILayout.Width(26))) { index = (index + 1) % _ids.Count; current = _ids[index]; }
        return current;
    }

    // ─────────────────────────────── 도우미 ───────────────────────────────

    private static int NextBandGap(int value)
    {
        int b = (int)Bands.Of(value);
        int target = b >= 4 ? Bands.UpperBound(Band.Band4) : Bands.LowerBound((Band)(b + 1));
        return Mathf.Max(0, target - value);
    }

    private static string Describe(JudgeSignal s)
    {
        string text = s.Kind.ToString();
        if (s.Space != SpaceId.None) text += " " + SpaceName(s.Space);
        if (s.TargetId.Length > 0) text += " " + s.TargetId;
        if (s.Kind == SignalKind.DoorCommandAccepted) text += s.Flag ? " 닫기" : " 열기";
        if (s.Kind == SignalKind.FlashlightChanged || s.Kind == SignalKind.TabChanged) text += s.Flag ? " On" : " Off";
        if (s.Kind == SignalKind.ProximitySample || s.Kind == SignalKind.GazeSample) text += " " + s.Value.ToString("0.00");
        if (s.Source == ActionSource.Direction) text += " (연출)";
        return text;
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

    private static string SpaceName(SpaceId s)
    {
        switch (s)
        {
            case SpaceId.Corridor: return "복도";
            case SpaceId.Classroom_1_1: return "1-1";
            case SpaceId.Classroom_1_3: return "1-3";
            case SpaceId.ScienceRoom: return "과학실";
            case SpaceId.Toilet: return "화장실";
            case SpaceId.None: return "없음";
            default: return s.ToString();
        }
    }
}

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
/// 야간근무 디버그 콘솔(F3, 2026-10-01). <b>걸어 다니지 않고</b> 수칙·조우·점검·연출을 확인하는 화면이다.
/// <list type="bullet">
/// <item><b>개요</b>: 밤 시계 구간 점프(00:16 · 슬롯 A · 이완 · 슬롯 B · 슬롯 C · 03:30) · 판정 강제 · 자동 연출 켜고 끄기 · 시계 정지 · 축 ± · 밤 종료·붙잡힘·재시작 · 손전등·달리기 신호.</item>
/// <item><b>수칙</b>: 오늘 덱의 상태(방아쇠·위반·진행 중)와 수칙마다 [이동] [단서] [끝] [조우], 31장 중 아무 수칙이나 [덱에 추가].</item>
/// <item><b>조우</b>: 오늘 슬롯의 진행 상태, 조우 15개 각각 [이동+실행]·[여기서 실행], [다음 단계], [무대 정리], 피날레 몹 두 배역(창밖 정장 남자 · 내 자리 무언가) [세우기/치우기]·비트 버튼(배역표 FinaleCast).</item>
/// <item><b>점검</b>: 오늘 점검표, 항목마다 [이동](항목 앞 1.6m에서 바라봄) · [정상] · [이상] 보고.</item>
/// <item><b>로그</b>: 연출 알림 · 수칙 정산 · 점검 보고 · 재시작.</item>
/// </list>
/// 열려 있는 동안 FPController를 꺼서 시점이 돌지 않게 한다(김진선님의 개발자 모드 패널과 같은 방식, 키는 겹치지 않게 F3).
/// 에디터와 Development Build에만 들어간다. 근무 씬이면 스스로 설치된다.
/// </summary>
[DisallowMultipleComponent]
public sealed class NightDutyDebugConsole : MonoBehaviour
{
    private const int MaxLog = 80;

    private static readonly string[] Tabs = { "개요", "수칙", "조우", "점검", "로그" };
    private static readonly string[] AxisNames = { "청각", "조도", "배치", "신뢰" };
    private static readonly SpaceId[] TeleportSpaces =
    {
        SpaceId.SecurityRoom, SpaceId.Corridor, SpaceId.Classroom, SpaceId.ScienceRoom, SpaceId.Toilet, SpaceId.Library
    };

    private readonly List<string> _log = new List<string>();
    private readonly List<Behaviour> _locked = new List<Behaviour>();
    private bool _show;
    private int _tab;
    private Vector2 _scroll;
    private Rect _window = new Rect(20f, 20f, 600f, 700f);
    private bool _dockRight = true;   // 기본은 화면 오른쪽에 붙는다. 끌어 옮기면 그 자리를 지킨다.
    private bool _clockHeld;
    private bool _running;
    private bool _cursorWasVisible;
    private CursorLockMode _cursorWasLock;
    private GUIStyle _small;
    private GUIStyle _rich;
    private GUIStyle _windowStyle;
    private Texture2D _windowBg;
    private Action _later;
    private Action _afterSettle;
    private float _settleAt;
    private int _settleFrame;
    private SpaceId _settleSpace = SpaceId.None;

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
        if (!FlowAutoInstall.IsDutyScene(scene)) return;
        if (FlowAutoInstall.Exists<NightDutyDebugConsole>(scene)) return;
        GameObject go = new GameObject("NightDutyDebugConsole (auto)");
        SceneManager.MoveGameObjectToScene(go, scene);
        go.AddComponent<NightDutyDebugConsole>();
    }

    private void Awake()
    {
        // 옛 하네스에서 옮겨 온 개발용 기본값(2026-10-01, 하네스 삭제).
        // ① 에디터가 초점을 잃어도 플레이 루프가 멈추지 않게(알트탭 한 번에 게임이 얼던 문제). 런타임 값만 — 프로젝트 설정은 그대로.
        // ② 열쇠 연출이 아직 없어 잠긴 문 12개가 회차를 막는다 — 개발 빌드에서는 잠금을 무시한다(콘솔에서 끌 수 있음).
        Application.runInBackground = true;
        PlayerInteractor.IgnoreLocks = false;   // 동선의 문은 시작할 때 풀린다(PlayerInteractor). 쓰지 않는 문은 잠긴 채 — 실제 게임과 같게. 개요 탭 버튼으로 켤 수 있다.
    }

    private void OnEnable()
    {
        EventBus.DirectionEmitted += OnDirection;
        EventBus.FinalRuleSettled += OnRule;
        EventBus.InspectionReported += OnReport;
        EventBus.NightRestarted += OnRestart;
    }

    private void OnDisable()
    {
        EventBus.DirectionEmitted -= OnDirection;
        EventBus.FinalRuleSettled -= OnRule;
        EventBus.InspectionReported -= OnReport;
        EventBus.NightRestarted -= OnRestart;
        SetShow(false);
        if (_windowBg != null) Destroy(_windowBg);
    }

    private void OnDirection(DirectionEvent e)
    {
        if (e.Kind == DirectionEventKind.Note && e.Text.StartsWith("예산")) return;
        Log("<color=#9cf>연출</color> " + e);
    }

    private void OnRule(FinalRuleResult r)
    {
        string color = r.Outcome == FinalOutcome.Violated ? "#f77" : r.Outcome == FinalOutcome.Noted ? "#ccc" : "#7f7";
        Log("<color=" + color + ">수칙</color> " + r);
    }

    private void OnReport(InspectionReport r)
    {
        Log("<color=#fd7>점검</color> " + r.ItemId + " " + (r.SaidAnomaly ? "[이상]" : "[정상]") + " → " + r.Outcome + " " + r.Axis + " " + r.Change);
    }

    private void OnRestart(RestartResult r)
    {
        Log("<color=#f9f>재시작</color> " + r.Kind + " k=" + r.K + " 시작 " + r.StartMinute + "분");
    }

    private void Log(string line)
    {
        _log.Add("[" + Clock(NightRun.NightMinute) + "] " + line);
        if (_log.Count > MaxLog) _log.RemoveAt(0);
    }

    // ── 열고 닫기 ───────────────────────────────────────────

    private static bool TogglePressed()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null) return keyboard.f3Key.wasPressedThisFrame;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.F3);
#else
        return false;
#endif
    }

    private void Update()
    {
        if (TogglePressed()) SetShow(!_show);
        if (_show) HoldPlayers();

        if (_afterSettle != null && Settled())
        {
            Action s = _afterSettle;
            _afterSettle = null;
            try { s(); }
            catch (Exception e) { Debug.LogException(e, this); }
        }

        if (_later != null)
        {
            Action a = _later;
            _later = null;
            try { a(); }
            catch (Exception e) { Debug.LogException(e, this); }
        }
    }

    /// <summary>옮긴 뒤 디렉터가 새 공간을 알게 됐는지(센서 샘플 3프레임 이상) — 2초가 지나면 그냥 진행.</summary>
    private bool Settled()
    {
        if (Time.unscaledTime > _settleAt + 2f) return true;
        if (Time.frameCount < _settleFrame + 3 || Time.unscaledTime < _settleAt) return false;
        TensionDirector t = NightRun.Tension;
        return _settleSpace == SpaceId.None || t == null || t.Space == SpaceIds.Canonical(_settleSpace);
    }

    private void SetShow(bool value)
    {
        if (_show == value) return;
        _show = value;
        if (_show)
        {
            _cursorWasVisible = Cursor.visible;
            _cursorWasLock = Cursor.lockState;
            HoldPlayers();
        }
        else
        {
            bool restored = false;
            foreach (Behaviour b in _locked)
            {
                if (b == null) continue;
                b.enabled = true;
                restored = true;
            }

            _locked.Clear();
            if (!restored)
            {
                Cursor.visible = _cursorWasVisible;
                Cursor.lockState = _cursorWasLock;
            }
        }
    }

    private void HoldPlayers()
    {
        foreach (FPController p in FindObjectsByType<FPController>(FindObjectsSortMode.None))
        {
            if (!p.enabled) continue;
            p.enabled = false;
            _locked.Add(p);
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    /// <summary>버튼 동작은 GUI 이벤트 밖(다음 Update)에서 한다 — 레이아웃 도중 목록이 바뀌면 IMGUI가 깨진다.</summary>
    private void Later(Action a)
    {
        _later += a;
    }

    // ── 화면 ───────────────────────────────────────────────

    private void OnGUI()
    {
        if (!_show) return;
        if (_small == null)
        {
            _small = new GUIStyle(GUI.skin.label) { fontSize = 12, richText = true, wordWrap = true };
            _rich = new GUIStyle(GUI.skin.label) { fontSize = 13, richText = true, wordWrap = false };

            // 뒤의 HUD(조작 안내 등)가 비치지 않게 불투명 배경.
            _windowBg = new Texture2D(1, 1);
            _windowBg.SetPixel(0, 0, new Color(0.08f, 0.08f, 0.09f, 0.97f));
            _windowBg.Apply();
            _windowStyle = new GUIStyle(GUI.skin.window);
            _windowStyle.normal.background = _windowBg;
            _windowStyle.onNormal.background = _windowBg;
            _windowStyle.normal.textColor = Color.white;
            _windowStyle.onNormal.textColor = Color.white;
            _windowStyle.fontStyle = FontStyle.Bold;
        }

        _window.height = Mathf.Min(Screen.height - 40f, 760f);
        if (_dockRight)
        {
            _window.x = Mathf.Max(0f, Screen.width - _window.width - 20f);
            _window.y = 20f;
        }

        GUI.depth = -100;
        Vector2 before = _window.position;
        _window = GUILayout.Window(0x4E44, _window, DrawWindow, "야간근무 디버그 콘솔 (F3)", _windowStyle);
        if ((_window.position - before).sqrMagnitude > 0.25f) _dockRight = false;   // 사용자가 끌었다.
        _window.x = Mathf.Clamp(_window.x, 0f, Mathf.Max(0f, Screen.width - _window.width));
        _window.y = Mathf.Clamp(_window.y, 0f, Mathf.Max(0f, Screen.height - 40f));
    }

    private void DrawWindow(int id)
    {
        DrawHeader();
        _tab = GUILayout.Toolbar(_tab, Tabs);
        _scroll = GUILayout.BeginScrollView(_scroll);
        switch (_tab)
        {
            case 0: DrawOverview(); break;
            case 1: DrawRules(); break;
            case 2: DrawEncounters(); break;
            case 3: DrawInspections(); break;
            case 4: DrawLog(); break;
        }

        GUILayout.EndScrollView();
        GUI.DragWindow(new Rect(0f, 0f, 10000f, 20f));
    }

    private void DrawHeader()
    {
        int minute = NightRun.NightMinute;
        TensionDirector t = NightRun.Tension;
        string phase = minute >= 0 ? NightClock.PhaseAt(minute).ToString() : "-";
        GUILayout.Label("<b>" + NightRun.Day + "일차</b>  " + Clock(minute) + " (" + minute + "분, " + phase + ")  판정 "
                        + (NightRun.IsJudgingNow ? "<color=#7f7>중</color>" : "<color=#f77>정지</color>")
                        + (NightRun.IsNightActive ? string.Empty : "  <color=#f77>밤 닫힘</color>")
                        + (NightRun.IsCaptured ? "  <color=#f44>붙잡힘</color>" : string.Empty), _rich);

        IFearAxisReader axes = NightRun.Axes;
        IFearAxisReader shown = NightRun.Shown;
        string a = string.Empty;
        for (int i = 0; i < 4; i++)
        {
            FearAxis ax = (FearAxis)i;
            a += AxisNames[i] + " " + axes.GetValue(ax) + "(구간 " + (int)shown.GetBand(ax) + ")  ";
        }

        GUILayout.Label(a, _rich);
        FinalRuleBook book = NightRun.FinalRules;
        GUILayout.Label("공간 " + (book != null ? book.World.Space.ToString() : "-")
                        + (t != null ? "  ·  디렉터 " + t.Mood + "  ·  예산 " + t.Budget.Usage + "  ·  가짜 놀람 " + t.FakesUsed : "  ·  디렉터 없음(새 편성 꺼짐)"), _small);
    }

    // ── 개요 ───────────────────────────────────────────────

    private void DrawOverview()
    {
        GUILayout.Label("<b>밤 시계로 이동</b> (태블릿 시계 표시는 바뀌지 않습니다)", _rich);
        GUILayout.BeginHorizontal();
        JumpButton("00:16 판정 시작", NightClock.JudgingStart + 1);
        JumpButton("01:00 슬롯 A", NightClock.Call1 + 1);
        JumpButton("01:52 이완", NightClock.RelaxStart + 1);
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        JumpButton("02:16 슬롯 B", NightClock.Call2 + 1);
        JumpButton("03:08 슬롯 C", NightClock.SlotCStart + 1);
        JumpButton("03:30 판정 끝", NightClock.JudgingEnd);
        if (GUILayout.Button("+5분")) Later(() => Jump(NightRun.NightMinute + 5));
        GUILayout.EndHorizontal();

        GUILayout.Space(6f);
        GUILayout.BeginHorizontal();
        bool forced = !NightRun.JudgingWindowEnabled;
        if (GUILayout.Button(forced ? "판정 강제: 켬(시간창 무시)" : "판정 강제: 끔")) Later(() => NightRun.JudgingWindowEnabled = !NightRun.JudgingWindowEnabled);
        if (GUILayout.Button(NightRun.DirectorAutoRun ? "자동 연출: 켬" : "자동 연출: 끔(버튼으로만)")) Later(() => NightRun.DirectorAutoRun = !NightRun.DirectorAutoRun);
        if (GUILayout.Button(_clockHeld ? "시계: 멈춤" : "시계: 흐름")) Later(ToggleClock);
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        if (GUILayout.Button(PlayerInteractor.IgnoreLocks ? "잠긴 문 무시: 켬" : "잠긴 문 무시: 끔")) Later(() => PlayerInteractor.IgnoreLocks = !PlayerInteractor.IgnoreLocks);
        if (GUILayout.Button(DoorPolicySO.OpenEverythingOverride ? "모든 문 열기: 켬" : "모든 문 열기: 끔")) Later(() => DoorPolicySO.OpenEverythingOverride = !DoorPolicySO.OpenEverythingOverride);
        GUILayout.EndHorizontal();

        GUILayout.Space(6f);
        GUILayout.Label("<b>축(생존 수치)</b>", _rich);
        for (int i = 0; i < 4; i++)
        {
            FearAxis ax = (FearAxis)i;
            GUILayout.BeginHorizontal();
            GUILayout.Label(AxisNames[i] + " " + NightRun.Axes.GetValue(ax), GUILayout.Width(90f));
            if (GUILayout.Button("−10")) Later(() => NightRun.DebugLowerAxis(ax, 10));
            if (GUILayout.Button("+10")) Later(() => NightRun.DebugAddAxis(ax, 10));
            if (GUILayout.Button("+25")) Later(() => NightRun.DebugAddAxis(ax, 25));
            GUILayout.EndHorizontal();
        }

        GUILayout.Space(6f);
        GUILayout.Label("<b>플레이어 신호</b>", _rich);
        GUILayout.BeginHorizontal();
        FlashlightRelay light = FlashlightRelay.Active;
        if (GUILayout.Button(light != null && light.IsOn ? "손전등 끄기" : "손전등 켜기") && light != null) Later(() => light.SetOn(!light.IsOn));
        if (GUILayout.Button(_running ? "달리기 신호 끄기" : "달리기 신호 켜기")) Later(() =>
        {
            _running = !_running;
            NightRun.Send(JudgeSignal.Run(_running));
        });
        GUILayout.EndHorizontal();

        GUILayout.Label("<b>이동</b>", _rich);
        GUILayout.BeginHorizontal();
        for (int i = 0; i < TeleportSpaces.Length; i++)
        {
            SpaceId s = TeleportSpaces[i];
            if (GUILayout.Button(SpaceName(s))) Later(() => TeleportToSpace(s));
        }

        GUILayout.EndHorizontal();

        GUILayout.Space(6f);
        GUILayout.Label("<b>밤</b>", _rich);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("밤 종료 요청(04:00 정산)")) Later(() => NightRun.RequestEndNight());
        if (GUILayout.Button("붙잡힘(청각)")) Later(() => NightRun.DebugForceCapture(FearAxis.Auditory));
        if (GUILayout.Button("재시작")) Later(() => NightRun.RestartAfterCapture());
        GUILayout.EndHorizontal();

        // 피날레(11단계) — 5일차 04:00에 구동기가 여는 것을 여기서 바로 연다.
        FinaleDirector fd = FinaleDirector.Active;
        FinaleWatch fw = NightRun.Finale;
        GUILayout.Label("<b>피날레(5일차)</b>  " + (fd != null && fd.IsRunning ? fd.Phase + " · 시도 " + fw.Attempt + " · 봤다 " + (fw.Seen ? "예" : "아니오") + " · CCTV " + fw.ChannelsSeen + "채널" : "대기") + (NightRun.LastFinaleEnding != FinaleEnding.None ? " · 지난 결말 " + NightRun.LastFinaleEnding : string.Empty), _rich);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("5일차로 다시 열기")) Later(() =>
        {
            if (NightRunDriver.Current != null) NightRunDriver.Current.DebugRestartAsDay(FinaleWatch.Day);
            Log("5일차로 다시 열었습니다(태블릿에 K4·G3)");
        });
        if (GUILayout.Button("피날레 시작")) Later(() => Log(fd != null && fd.Begin() ? "피날레 시작" : "피날레를 열 수 없음(5일차 밤이 아님·진행 중)"));
        if (GUILayout.Button("CCTV 건너뛰기")) Later(() =>
        {
            if (fd != null) fd.DebugSkipCctv();
        });
        if (GUILayout.Button("창을 봤다(비춤)")) Later(() => NightRun.Send(JudgeSignal.Beam(FinaleWatch.WindowTarget, 0.1f)));
        GUILayout.EndHorizontal();

        // 역설·변조(10단계).
        ParadoxRun pr = NightRun.Paradox;
        string prState = pr.RuleId == null ? "없음" : pr.RuleId + " " + pr.Pattern + (pr.Plan.WillSend ? "" : " (보류)") + " · " + (pr.SafeRead ? "안전한 읽기 완료" : pr.Sent ? "보냄 " + Mathf.RoundToInt(pr.Progress * 100f) + "%" : "대기");
        GUILayout.Label("<b>역설·변조(10단계)</b>  " + Escape(prState) + "  <color=#999>" + Escape(pr.Plan.Report) + "</color>", _rich);
        GUILayout.BeginHorizontal();
        GUILayout.Label("문자 보내기:", GUILayout.ExpandWidth(false));
        IReadOnlyList<RuleDef> deck = NightRun.Program.Deck;
        for (int i = 0; i < deck.Count; i++)
        {
            ParadoxEntry e = ParadoxCatalog.Find(deck[i].Id);
            if (e == null || !e.HasMessage) continue;
            string rid = deck[i].Id;
            if (GUILayout.Button(rid, GUILayout.ExpandWidth(false))) Later(() => Log(NightRun.DebugSendParadox(rid) ? "역설 문자 " + rid : "보낼 수 없음(" + rid + ")"));
        }

        if (GUILayout.Button("안전한 읽기 완료", GUILayout.ExpandWidth(false))) Later(() => Log(NightRun.DebugSafeRead() ? "안전한 읽기 완료" : "보낸 문자가 없음"));
        GUILayout.EndHorizontal();

        TensionDirector t = NightRun.Tension;
        if (t != null)
        {
            GUILayout.Space(6f);
            GUILayout.Label("<b>오늘 편성</b>  " + Escape(NightRun.Program.ToString()), _small);
        }
    }

    private void JumpButton(string label, int minute)
    {
        if (GUILayout.Button(label)) Later(() => Jump(minute));
    }

    private void Jump(int minute)
    {
        NightRunDriver d = NightRunDriver.Current;
        if (d == null)
        {
            Log("<color=#f77>밤 시계 구동기가 없습니다</color>");
            return;
        }

        d.DebugJumpToNightMinute(minute);
        Log("밤 시계 → " + Clock(minute));
    }

    private void ToggleClock()
    {
        NightRunDriver d = NightRunDriver.Current;
        if (d == null || d.Clock == null) return;
        _clockHeld = !_clockHeld;
        if (_clockHeld) d.Clock.Hold(this);
        else d.Clock.Release(this);
    }

    // ── 수칙 ───────────────────────────────────────────────

    private void DrawRules()
    {
        FinalRuleBook book = NightRun.FinalRules;
        if (book == null)
        {
            GUILayout.Label("새 편성이 꺼져 있어 새 수칙 판정이 없습니다.", _rich);
            return;
        }

        GUILayout.Label("<b>오늘 덱</b>  (✓ 방아쇠 · ✗ 위반)", _rich);
        IReadOnlyList<FinalJudge> judges = book.Judges;
        for (int i = 0; i < judges.Count; i++)
        {
            FinalJudge j = judges[i];
            RuleDef d = j.Def;
            GUILayout.BeginHorizontal();
            string mark = (j.Triggered ? "<color=#7f7>✓</color>" : "·") + (j.Violated ? "<color=#f77>✗</color>" : " ");
            GUILayout.Label(mark + " <b>" + d.Id + "</b> " + (d.IsThreat ? "<color=#f96>위협</color> " : string.Empty)
                            + (d.HasAxis ? AxisNames[(int)d.Axis] : "-") + "  <color=#ccc>" + Escape(d.Text) + "</color>"
                            + (j.Status.Length > 0 ? "  <color=#9cf>" + j.Status + "</color>" : string.Empty), _small, GUILayout.Width(360f));

            string rid = d.Id;
            if (d.Space != SpaceId.None && GUILayout.Button("이동", GUILayout.Width(44f))) Later(() => TeleportForRule(rid));
            if (RuleTriggers.Find(rid) != null)
            {
                if (GUILayout.Button("단서", GUILayout.Width(44f))) Later(() => NightRun.DebugFireRuleCue(rid));
                if (GUILayout.Button("끝", GUILayout.Width(32f))) Later(() => NightRun.DebugEndRuleCue(rid));
            }
            else if (d.BoundEncounter.Length > 0)
            {
                string enc = d.BoundEncounter;
                if (GUILayout.Button("조우", GUILayout.Width(44f))) Later(() => ForceEncounter(enc, true));
            }

            GUILayout.EndHorizontal();
        }

        GUILayout.Space(6f);
        GUILayout.Label("<b>덱에 추가</b>", _rich);
        GUILayout.BeginHorizontal();
        int n = 0;
        foreach (RuleDef r in ProgramCatalog.AllRules)
        {
            if (book.Judge(r.Id) != null) continue;
            string rid = r.Id;
            if (GUILayout.Button(rid, GUILayout.Width(44f))) Later(() => NightRun.DebugAddFinalRule(rid));
            if (++n % 10 == 0)
            {
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
            }
        }

        GUILayout.EndHorizontal();

        GUILayout.Space(6f);
        GUILayout.Label("<b>정산 결과</b>", _rich);
        IReadOnlyList<FinalRuleResult> results = NightRun.FinalResults;
        for (int i = results.Count - 1; i >= 0 && i >= results.Count - 15; i--)
        {
            GUILayout.Label(Escape(results[i].ToString()), _small);
        }
    }

    // ── 조우 ───────────────────────────────────────────────

    private void DrawEncounters()
    {
        TensionDirector t = NightRun.Tension;
        if (t == null)
        {
            GUILayout.Label("디렉터가 없습니다(새 편성 꺼짐).", _rich);
            return;
        }

        GUILayout.Label("<b>오늘 슬롯</b>", _rich);
        for (int i = 0; i < t.Runs.Count; i++)
        {
            EncounterRun r = t.Runs[i];
            string color = r.State == EncounterRunState.Done ? "#7f7" : r.State == EncounterRunState.Missed ? "#f77" : r.State == EncounterRunState.Waiting ? "#ccc" : "#fd7";
            GUILayout.Label("<color=" + color + ">" + Escape(r.ToString()) + "</color>" + (r.FalseCount > 0 ? "  헛예고 " + r.FalseCount : string.Empty), _small);
        }

        if (t.RuleRuns.Count > 0)
        {
            string s = "수칙 단서: ";
            for (int i = 0; i < t.RuleRuns.Count; i++)
            {
                RuleTriggerRun r = t.RuleRuns[i];
                s += r.Script.RuleId + (r.Done ? "(끝) " : r.Running ? "(울림) " : "(대기 " + r.NeedDwell.ToString("0") + "초) ");
            }

            GUILayout.Label(s, _small);
        }

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("다음 단계로")) Later(NightRun.DebugSkipDirectionPhase);
        if (GUILayout.Button("무대 정리")) Later(() =>
        {
            if (DirectionStage.Active != null) DirectionStage.Active.ClearAll(DirectionPhase.Aborted);
        });
        GUILayout.EndHorizontal();

        DrawFinaleMobs();

        GUILayout.BeginHorizontal();
        GUILayout.Label("가짜 놀람(여기서, 예산 안 씀):", _small, GUILayout.Width(170f));
        foreach (string fake in TensionDirector.FakeScares)
        {
            string id = fake;
            if (GUILayout.Button(FakeName(id))) Later(() => Log(NightRun.DebugForceFake(id) ? "가짜 놀람: " + FakeName(id) : "가짜 놀람 실패: " + id));
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(6f);
        GUILayout.Label("<b>조우 15개</b>  [이동+실행] = 그 공간으로 옮긴 뒤 전조 2초 → 대면", _rich);
        foreach (EncounterDef e in ProgramCatalog.AllEncounters)
        {
            EncounterScript s = EncounterScripts.Find(e.Id);
            GUILayout.BeginHorizontal();
            GUILayout.Label("<b>" + e.Name + "</b>  " + AxisNames[(int)e.Axis] + " 강도 " + e.Intensity + " · " + SpaceName(SpaceIds.Canonical(e.Space))
                            + " · 대응 " + e.ResponseRule + (e.IsCross ? " · 교차" : string.Empty), _small, GUILayout.Width(380f));
            string id = e.Id;
            if (GUILayout.Button("이동+실행", GUILayout.Width(80f))) Later(() => ForceEncounter(id, true));
            if (GUILayout.Button("여기서", GUILayout.Width(56f))) Later(() => ForceEncounter(id, false));
            GUILayout.EndHorizontal();
            if (s != null && s.Note.Length > 0) GUILayout.Label("   <color=#999>" + Escape(s.Note) + "</color>", _small);
        }
    }

    private void ForceEncounter(string id, bool move)
    {
        EncounterDef e = ProgramCatalog.Encounter(id);
        EncounterScript s = EncounterScripts.Find(id);
        if (e == null || s == null) return;

        // 판정 단서가 들어가도록 판정 책에 그 수칙이 없으면 붙인다.
        if (!string.IsNullOrEmpty(e.ResponseRule)) NightRun.DebugAddFinalRule(e.ResponseRule);
        if (!string.IsNullOrEmpty(e.SecondRule)) NightRun.DebugAddFinalRule(e.SecondRule);

        Action run = () =>
        {
            if (!NightRun.DebugForceEncounter(id)) Log("<color=#f77>조우 실행 실패: " + id + "</color>");
        };

        if (!move)
        {
            run();
            return;
        }

        // 옮긴 뒤 센서가 새 자리·공간을 한두 번 샘플할 때까지 기다린다(0.1초 샘플) — 바로 실행하면 옛 자리로 판정한다.
        SpaceId room = s.ExactSpace != SpaceId.None ? s.ExactSpace : s.Space;
        StageAnchor fixedAt = StageAnchor.Find(s.StageAnchor);
        if (fixedAt != null) TeleportToView(fixedAt, room);   // 고정 자리 몹은 그 자리가 보이는 곳으로.
        else TeleportToSpace(room);
        _afterSettle = run;
        _settleAt = Time.unscaledTime + 0.35f;
        _settleFrame = Time.frameCount;
        _settleSpace = s.Space;
    }

    // ── 점검 ───────────────────────────────────────────────

    private void DrawInspections()
    {
        InspectionBoard board = NightRun.Inspections;
        if (board == null || board.Plan == null || board.Plan.Assignments.Count == 0)
        {
            GUILayout.Label("오늘 점검표가 없습니다.", _rich);
            return;
        }

        GUILayout.Label("<b>오늘 점검표</b>  " + board.Plan.Day + "일차 · 늦게 열리는 공간 " + board.Plan.LateSpace, _rich);
        IReadOnlyList<InspectionAssignment> rows = board.Plan.Assignments;
        for (int i = 0; i < rows.Count; i++)
        {
            InspectionAssignment a = rows[i];
            string id = a.Id;
            GUILayout.BeginHorizontal();
            GUILayout.Label("<b>" + id + "</b> " + Escape(a.Item.Name) + (a.IsAnomaly ? " <color=#f77>[이상 " + a.Intensity + "]</color>" : " <color=#7f7>[정상]</color>")
                            + (a.IsLate ? " 늦게" : string.Empty) + "  " + board.StateOf(id), _small, GUILayout.Width(330f));
            if (GUILayout.Button("이동", GUILayout.Width(44f))) Later(() => TeleportToTarget(a.Item.TargetId, a.Item.Space));
            if (GUILayout.Button("정상", GUILayout.Width(44f))) Later(() => NightRun.ReportInspection(id, false));
            if (GUILayout.Button("이상", GUILayout.Width(44f))) Later(() => NightRun.ReportInspection(id, true));
            GUILayout.EndHorizontal();
        }
    }

    // ── 로그 ───────────────────────────────────────────────

    private void DrawLog()
    {
        if (GUILayout.Button("지우기")) Later(_log.Clear);
        for (int i = _log.Count - 1; i >= 0; i--)
        {
            GUILayout.Label(_log[i], _small);
        }
    }

    // ── 이동 ───────────────────────────────────────────────

    private void TeleportForRule(string ruleId)
    {
        RuleDef d = ProgramCatalog.Rule(ruleId);
        if (d == null) return;

        // 기준점이 있는 수칙은 기준점 앞 3m로, 없으면 공간으로.
        string anchor = AnchorOf(ruleId);
        JudgeTarget t;
        if (anchor.Length > 0 && JudgeTargetRegistry.TryGet(anchor, out t) && t != null)
        {
            TeleportNear(t.AnchorPosition, d.Space, 3f);
            return;
        }

        TeleportToSpace(d.Space);
    }

    private static string AnchorOf(string ruleId)
    {
        switch (ruleId)
        {
            case "H1": return FinalCues.H1Object;
            case "L1": return FinalCues.L1Shelf;
            case "L4": return FinalCues.L4Box;
            case "S1": return FinalCues.S1Center;
            case "S3": return FinalCues.ModelTarget;
            default: return string.Empty;
        }
    }

    private void TeleportToTarget(string targetId, SpaceId space)
    {
        JudgeTarget t;
        if (!JudgeTargetRegistry.TryGet(targetId, out t) || t == null)
        {
            Log("<color=#f77>씬에 대상이 없습니다: " + targetId + "</color>");
            TeleportToSpace(space);
            return;
        }

        TeleportNear(t.AnchorPosition, space, 1.6f);
    }

    private void TeleportToSpace(SpaceId space)
    {
        SpaceZones zones = FindAnyObjectByType<SpaceZones>();
        Bounds box;
        if (SpaceIds.Canonical(space) == SpaceId.Classroom) space = SpaceId.Classroom_1_3;   // 점검 항목이 있는 교실(Classroom02).
        if (zones == null || !zones.TryGetSpaceBox(space, out box))
        {
            Log("<color=#f77>공간 상자를 모릅니다: " + space + "</color>");
            return;
        }

        Vector3 spot;
        if (!FreeSpot(new Vector3(box.center.x, box.min.y + 1f, box.center.z), box, 0f, out spot))
        {
            Log("<color=#f77>빈자리를 찾지 못했습니다: " + space + "</color>");
            return;
        }

        Teleport(spot, null);
        Log("이동 → " + SpaceName(space));
    }

    /// <summary>고정 연출 자리가 보이는 곳(자리 앞 <see cref="StageAnchor.DebugViewDistance"/>m)으로 옮기고 그쪽을 보게 한다.</summary>
    // ── 피날레 몹(배역표 FinaleCast) ─────────────────────────

    /// <summary>피날레 배역마다 [세우기/치우기]와 비트 버튼. 팀원 프리팹을 배역표에 넣고 여기서 바로 틀어 본다.</summary>
    private void DrawFinaleMobs()
    {
        // 붙잡힘 장면(연출표 CaptureCast) — 재시작·카드 없이 장면만.
        GUILayout.BeginHorizontal();
        GUILayout.Label("붙잡힘 장면 미리 보기(" + _captureRepeat + "회차):", _small, GUILayout.Width(220f));
        foreach (FearAxis axis in new[] { FearAxis.Auditory, FearAxis.Illuminance, FearAxis.Layout })
        {
            FearAxis a = axis;
            CaptureCastSO.Entry e = CaptureCastSO.Load().Get(a);
            if (GUILayout.Button(CaptureCastSO.Label(a) + (e.prefab != null ? "" : "(기본)"))) Later(() =>
            {
                CaptureDirector cd = CaptureDirector.Active;
                if (cd == null || !cd.Preview(a, _captureRepeat))
                {
                    Log("붙잡힘 장면 미리 보기 실패(연출기 없음 또는 진행 중)");
                    return;
                }

                StartCoroutine(LogWhenDone(cd));
            });
        }

        if (GUILayout.Button("회차 바꾸기")) _captureRepeat = _captureRepeat % 3 + 1;
        GUILayout.EndHorizontal();

        DirectionStage stage = DirectionStage.Active;
        foreach (FinaleRole role in new[] { FinaleRole.WindowMan, FinaleRole.SeatFigure })
        {
            FinaleRole r = role;
            FinaleMob mob = stage != null ? stage.FinaleOf(r) : null;
            string state = mob == null ? "없음" : (mob.HasArt ? "아트" : "대역") + " · " + FinaleBeats.Label(mob.CurrentBeat) + (mob.IsBusy ? "…" : string.Empty);
            GUILayout.BeginHorizontal();
            GUILayout.Label("피날레 " + FinaleBeats.Label(r) + " (" + state + "):", _small, GUILayout.Width(220f));
            if (GUILayout.Button(mob == null ? "세우기" : "치우기")) Later(() => ToggleFinale(r));
            foreach (FinaleBeat beat in FinaleBeats.For(r))
            {
                FinaleBeat b = beat;
                if (GUILayout.Button(FinaleBeats.Label(b))) Later(() => PlayFinale(r, b));
            }

            GUILayout.EndHorizontal();
        }
    }

    private int _captureRepeat = 1;

    private System.Collections.IEnumerator LogWhenDone(CaptureDirector cd)
    {
        yield return null;
        while (cd != null && cd.IsPlaying) yield return null;
        if (cd != null) Log("붙잡힘 장면: " + cd.LastScene);
    }

    private FinaleMob EnsureFinale(FinaleRole r, bool teleport)
    {
        DirectionStage stage = DirectionStage.Active;
        if (stage == null) return null;
        FinaleMob mob = stage.FinaleOf(r);
        if (mob != null) return mob;

        mob = stage.StageFinale(r);
        if (mob == null)
        {
            Log("피날레 " + FinaleBeats.Label(r) + ": 씬에 자리가 없습니다");
            return null;
        }

        mob.BeatFinished += (m, b) => Log("피날레 " + m.LastLog);
        mob.Cued += (m, cue) => Log("피날레 " + FinaleBeats.Label(m.Role) + " 신호: " + cue);
        if (teleport)
        {
            StageAnchor a = StageAnchor.Find(FinaleCastSO.Load().Get(r).anchorId);
            if (a != null) TeleportToView(a, SpaceId.SecurityRoom);
        }

        Log("피날레 " + FinaleBeats.Label(r) + " 세움 — " + mob.LastLog);
        return mob;
    }

    private void ToggleFinale(FinaleRole r)
    {
        DirectionStage stage = DirectionStage.Active;
        if (stage == null) return;
        if (stage.FinaleOf(r) != null)
        {
            stage.ClearFinale(r);
            Log("피날레 " + FinaleBeats.Label(r) + " 치움");
            return;
        }

        EnsureFinale(r, true);
    }

    private void PlayFinale(FinaleRole r, FinaleBeat b)
    {
        FinaleMob mob = EnsureFinale(r, false);
        if (mob == null) return;
        if (!mob.Supports(b)) Log("피날레 " + FinaleBeats.Label(r) + " · " + FinaleBeats.Label(b) + ": Animator에 없음 — 건너뜀");
        mob.Play(b);
    }

    private void TeleportToView(StageAnchor anchor, SpaceId space)
    {
        SpaceZones zones = FindAnyObjectByType<SpaceZones>();
        Bounds box;
        if (zones == null || !zones.TryGetSpaceBox(space, out box))
        {
            TeleportToSpace(space);
            return;
        }

        Vector3 f = anchor.transform.forward;
        f.y = 0f;
        if (f.sqrMagnitude < 0.01f) f = Vector3.forward;
        Vector3 want = anchor.transform.position + f.normalized * anchor.DebugViewDistance;
        Vector3 spot;
        if (!FreeSpot(want, box, 0f, out spot))
        {
            TeleportToSpace(space);
            return;
        }

        Vector3 look = anchor.transform.position - spot;
        look.y = 0f;
        Teleport(spot, Mathf.Atan2(look.x, look.z) * Mathf.Rad2Deg);
        Log("이동 → " + SpaceName(space) + " (" + anchor.AnchorId + " 앞)");
    }

    private void TeleportNear(Vector3 target, SpaceId space, float distance)
    {
        SpaceZones zones = FindAnyObjectByType<SpaceZones>();
        Bounds box;
        bool hasBox = zones != null && zones.TryGetSpaceBox(space, out box);
        if (!hasBox) box = new Bounds(target, new Vector3(12f, 6f, 12f));
        else zones.TryGetSpaceBox(space, out box);

        Vector3 toward = box.center - target;
        toward.y = 0f;
        if (toward.sqrMagnitude < 0.01f) toward = Vector3.forward;
        Vector3 want = target + toward.normalized * distance;

        Vector3 spot;
        if (!FreeSpot(want, box, 0.5f, out spot))
        {
            Log("<color=#f77>빈자리를 찾지 못했습니다</color>");
            return;
        }

        Vector3 look = target - spot;
        look.y = 0f;
        Teleport(spot, Mathf.Atan2(look.x, look.z) * Mathf.Rad2Deg);
    }

    /// <summary><paramref name="want"/> 둘레에서 사람이 설 수 있는 바닥 점을 찾는다(나선, 0.6m 간격, 상자 안).</summary>
    private static bool FreeSpot(Vector3 want, Bounds box, float minFromWant, out Vector3 spot)
    {
        Transform player = PlayerRoot();
        for (int ring = 0; ring < 8; ring++)
        {
            int steps = ring == 0 ? 1 : ring * 8;
            for (int k = 0; k < steps; k++)
            {
                float ang = k * Mathf.PI * 2f / steps;
                Vector3 p = want + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * (ring * 0.6f);
                if (p.x < box.min.x + 0.4f || p.x > box.max.x - 0.4f || p.z < box.min.z + 0.4f || p.z > box.max.z - 0.4f) continue;

                Vector3 floor = DirectionStage.FloorBelow(new Vector3(p.x, box.min.y + 1.2f, p.z));
                if (floor.y > box.min.y + 1.2f) continue;   // 책상 위 등

                if (!Blocked(floor, player))
                {
                    spot = floor;
                    return true;
                }
            }
        }

        spot = Vector3.zero;
        return false;
    }

    private static bool Blocked(Vector3 floor, Transform player)
    {
        Collider[] hits = Physics.OverlapCapsule(floor + Vector3.up * 0.45f, floor + Vector3.up * 1.6f, 0.3f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits.Length; i++)
        {
            if (player != null && hits[i].transform.IsChildOf(player)) continue;
            return true;
        }

        return false;
    }

    private static Transform PlayerRoot()
    {
        PlayerSensors hub = PlayerSensors.Active;
        if (hub != null && hub.PlayerRoot != null) return hub.PlayerRoot;
        FPController fp = FindAnyObjectByType<FPController>();
        return fp != null ? fp.transform : null;
    }

    private void Teleport(Vector3 floor, float? yaw)
    {
        Transform root = PlayerRoot();
        if (root == null) return;

        // 루트 피벗에서 발바닥(콜라이더 아래끝)까지 — 그만큼 바닥 위에 놓는다.
        Collider body = root.GetComponent<Collider>();
        if (body == null) body = root.GetComponentInChildren<Collider>();
        float lift = body != null ? root.position.y - body.bounds.min.y + 0.02f : 0.85f;
        if (lift < 0f || lift > 3f) lift = 0.85f;
        Vector3 target = floor + Vector3.up * lift;

        Rigidbody rb = root.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.position = target;
        }

        root.position = target;
        if (yaw.HasValue)
        {
            root.rotation = Quaternion.Euler(0f, yaw.Value, 0f);
            if (rb != null) rb.rotation = root.rotation;
        }

        Physics.SyncTransforms();
    }

    // ── 도구 ───────────────────────────────────────────────

    private static string FakeName(string id)
    {
        switch (id)
        {
            case "fake.locker.rattle": return "덜컹이는 사물함";
            case "fake.locker.row": return "열려 있는 사물함";
            case "fake.flashlight.flicker": return "손전등 깜빡임";
            case TensionDirector.FakeBugs: return "벌레 떼";
            default: return id;
        }
    }

    private static string SpaceName(SpaceId s)
    {
        switch (SpaceIds.Canonical(s))
        {
            case SpaceId.Corridor: return "복도";
            case SpaceId.Classroom: return "교실";
            case SpaceId.ScienceRoom: return "과학실";
            case SpaceId.Toilet: return "화장실";
            case SpaceId.Library: return "도서관";
            case SpaceId.SecurityRoom: return "경비실";
            default: return s.ToString();
        }
    }

    private static string Clock(int minute)
    {
        if (minute < 0) return "--:--";
        return (minute / 60).ToString("00") + ":" + (minute % 60).ToString("00");
    }

    private static string Escape(string s)
    {
        return string.IsNullOrEmpty(s) ? string.Empty : s.Replace("<", "‹").Replace(">", "›");
    }
}
#endif

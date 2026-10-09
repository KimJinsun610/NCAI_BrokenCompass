using System;
using System.Collections.Generic;
using NightDuty;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 점검 보고 센서(2026-09-30 최종 기획서 「보고와 점검 수칙」·「태블릿·시간 규칙」).
/// <list type="bullet">
/// <item>오늘 점검표의 항목마다 씬의 점검 대상(<see cref="JudgeTarget"/> ID <c>inspect.H-2</c> 등)을 찾아
/// <b>2m 안에서 1초 응시</b>하면 보고 가능으로 켠다(0.2초 이내 끊김은 연속, 켜진 뒤에는 2m 안에 있는 동안 유지).
/// 켜지는 순간 <see cref="ReadyTicked"/>(틱 소리용)를 낸다.</item>
/// <item>보고 가능한 항목 중 가장 가까운 것이 <see cref="Focus"/>다. 태블릿을 올리면 그 항목에 바로 포커스가 간다(태블릿 UI가 읽는다).</item>
/// <item>[정상]/[이상]은 0.5초 길게 눌러 확정한다. 60차: 보고한 항목도 다시 겨누면(2m·1초 응시) 다른 판정 키로 바꿀 수 있다(<see cref="NightRun.ReviseInspection"/>) — 보고 전 항목이 포커스에서 먼저다. 태블릿을 올리지 않고 쓰는 전용 키(기본 Z = 정상, X = 이상)를 여기서 받는다.
/// 태블릿 UI는 <see cref="TryReportFocused"/>를 부르면 된다(길게 누르기는 UI가 잰다).</item>
/// <item>점검 수칙 「가까이」: 0.8m 안에서 0.3초 들여다보면 <see cref="NightRun.InspectionStartle"/>(항목마다 한 번, 판정 시간창 안에서만).
/// 「건드리기」·「뒤로 돌아가기」는 항목별 연출 단계에서 더한다.</item>
/// </list>
/// <para>
/// 0.1초 틱은 <see cref="PlayerSensors.Sampled"/>를 받아 쓴다 — 응시가 갱신된 뒤라 같은 샘플의 <see cref="GazeProbe.CurrentId"/>를 읽는다.
/// <b>씬에 놓지 않아도 된다</b> — 근무 씬이 열리면 플레이어 루트에 자동으로 붙는다. 점검 대상이 씬에 없으면 아무 일도 하지 않는다.
/// </para>
/// </summary>
[DisallowMultipleComponent]
public sealed class InspectionSensor : MonoBehaviour
{
    [Header("전용 키(길게 누르기)")]
    [Tooltip("[정상] 전용 키. 0.5초 길게 눌러 확정.")]
    [SerializeField] private KeyCode normalKey = KeyCode.Z;

    [Tooltip("[이상] 전용 키. 0.5초 길게 눌러 확정.")]
    [SerializeField] private KeyCode anomalyKey = KeyCode.X;

    [Tooltip("화면 중앙 아래에 보고 안내를 띄운다(50차 — InteractionHud.InspectionPrompt).")]
    [SerializeField] private bool showPrompt = true;

    private readonly Dictionary<string, ReportReadiness> _readiness = new Dictionary<string, ReportReadiness>(StringComparer.Ordinal);
    private string _focus = string.Empty;
    private float _hold;
    private bool _holdAnomaly;
    private int _day = -1;
    private InspectionPlan _plan;
    private string _lastResult = string.Empty;
    private float _lastResultUntil;

    private static InspectionSensor s_active;

    /// <summary>지금 살아 있는 센서. 없으면 null.</summary>
    public static InspectionSensor Active
    {
        get { return s_active; }
    }

    /// <summary>보고 포커스(보고 가능한 항목 중 가장 가까운 것)가 바뀌었다. 인자: 항목 ID(없으면 빈 문자열).</summary>
    public static event Action<string> FocusChanged;

    /// <summary>항목이 보고 가능으로 켜졌다(틱 소리). 인자: 항목 ID.</summary>
    public static event Action<string> ReadyTicked;

    /// <summary>지금 보고 포커스 항목 ID. 없으면 빈 문자열.</summary>
    public string Focus
    {
        get { return _focus; }
    }

    /// <summary>전용 키 길게 누르기 진행도(0~1). 누르고 있지 않으면 0.</summary>
    public float HoldProgress
    {
        get { return Mathf.Clamp01(_hold / SensingRules.ReportHoldSeconds); }
    }

    /// <summary>지금 누르고 있는 키가 [이상]인지.</summary>
    public bool HoldingAnomaly
    {
        get { return _holdAnomaly; }
    }

    /// <summary>항목의 보고 준비 상태. 모르면 null.</summary>
    public ReportReadiness ReadinessOf(string itemId)
    {
        ReportReadiness r;
        return itemId != null && _readiness.TryGetValue(itemId, out r) ? r : null;
    }

    /// <summary>
    /// 포커스 항목을 보고한다(태블릿 UI가 0.5초 길게 누르기를 잰 뒤 부른다). 포커스가 없으면 받지 않는다 — 보고는 현장에서만.
    /// </summary>
    public static InspectionReport TryReportFocused(bool anomaly)
    {
        InspectionSensor s = s_active;
        if (s == null || s._focus.Length == 0)
        {
            return InspectionReport.Reject(string.Empty, anomaly, ReportRejection.NotOpenYet);
        }

        return s.Report(anomaly);
    }

    // ── 자동 설치 ───────────────────────────────────────────

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Install()
    {
        s_active = null;
        FocusChanged = null;
        ReadyTicked = null;
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
        if (FlowAutoInstall.Exists<InspectionSensor>(scene)) return;

        Camera cam = FlowAutoInstall.FindCamera(scene);
        if (cam == null) return;

        cam.transform.root.gameObject.AddComponent<InspectionSensor>();
    }

    private void OnEnable()
    {
        s_active = this;
        PlayerSensors.Sampled += OnSampled;
        EventBus.InspectionPlanned += OnPlanned;
    }

    private void OnDisable()
    {
        PlayerSensors.Sampled -= OnSampled;
        EventBus.InspectionPlanned -= OnPlanned;
        if (s_active == this) s_active = null;
        InteractionHud.InspectionPrompt = string.Empty;
        InteractionHud.InspectionProgress = 0f;
    }

    private void OnPlanned(InspectionPlan plan)
    {
        ResetState();
    }

    private void ResetState()
    {
        _readiness.Clear();
        SetFocus(string.Empty);
        _hold = 0f;
        _plan = null;
    }

    // ── 0.1초 샘플 ──────────────────────────────────────────

    private void OnSampled(float step)
    {
        if (!NightRun.IsNightActive || NightRun.IsCaptured)
        {
            SetFocus(string.Empty);
            return;
        }

        InspectionBoard board = NightRun.Inspections;
        if (_day != NightRun.Day || !ReferenceEquals(_plan, board.Plan))
        {
            _day = NightRun.Day;
            _readiness.Clear();
            _plan = board.Plan;
        }

        PlayerSensors hub = PlayerSensors.Active;
        if (hub == null) return;

        Transform root = hub.PlayerRoot != null ? hub.PlayerRoot : transform;
        string gazed = hub.Gaze.CurrentId;
        int minute = NightRun.NightMinute;

        string best = string.Empty;
        float bestDistance = float.MaxValue;
        string bestReported = string.Empty;   // 60차: 보고한 항목도 다시 겨누면 정정할 수 있다 — 보고 전 항목이 먼저
        float bestReportedDistance = float.MaxValue;

        IReadOnlyList<InspectionAssignment> rows = board.Plan.Assignments;
        for (int i = 0; i < rows.Count; i++)
        {
            InspectionAssignment row = rows[i];
            string id = row.Id;
            bool reported = board.StateOf(id) != InspectionState.Pending;
            if (reported && board.IsReverse(id)) continue;   // T4 역보고가 걸린 변기는 판정이 이미 났다

            JudgeTarget target;
            if (!JudgeTargetRegistry.TryGet(row.Item.TargetId, out target) || target == null) continue;

            ReportReadiness r;
            if (!_readiness.TryGetValue(id, out r))
            {
                r = new ReportReadiness();
                _readiness[id] = r;
            }

            float distance = SensingRules.HorizontalDistance(root.position, target.AnchorPosition);
            bool gazing = gazed == row.Item.TargetId;

            bool near;
            bool changed = r.Feed(distance, gazing, step, out near);

            if (near)
            {
                NightRun.InspectionStartle(id);
            }

            bool open = reported ? board.CanRevise(id, minute) : board.IsOpen(id, minute);
            if (changed && r.Ready && open && !reported)
            {
                Raise(ReadyTicked, id);
            }

            if (reported)
            {
                if (r.Ready && open && distance < bestReportedDistance)
                {
                    bestReported = id;
                    bestReportedDistance = distance;
                }

                continue;
            }

            if (r.Ready && open && distance < bestDistance)
            {
                best = id;
                bestDistance = distance;
            }
        }

        SetFocus(best.Length > 0 ? best : bestReported);
    }

    // ── 전용 키 ─────────────────────────────────────────────

    private void Update()
    {
        RequestFocusOutline();

        if (_focus.Length == 0 || Time.timeScale <= 0f)
        {
            _hold = 0f;
            return;
        }

        bool normal = Input.GetKey(normalKey);
        bool anomaly = Input.GetKey(anomalyKey);
        if (normal == anomaly)
        {
            // 둘 다 누르거나 둘 다 뗐다 — 확정하지 않는다.
            _hold = 0f;
            return;
        }

        if (_hold > 0f && _holdAnomaly != anomaly)
        {
            _hold = 0f;   // 키를 바꿔 잡았으면 처음부터.
        }

        // 60차: 보고한 항목은 지금과 다른 판정 키만 받는다(같은 키는 할 일이 없다).
        InspectionState focusState = NightRun.Inspections != null ? NightRun.Inspections.StateOf(_focus) : InspectionState.Pending;
        if (focusState != InspectionState.Pending && (focusState == InspectionState.ReportedAnomaly) == anomaly)
        {
            _hold = 0f;
            return;
        }

        _holdAnomaly = anomaly;
        _hold += Time.unscaledDeltaTime;
        if (_hold >= SensingRules.ReportHoldSeconds)
        {
            _hold = 0f;
            Report(anomaly);
        }
    }

    /// <summary>점검 대상 외곽선을 옅게 그리는 거리(m, 수평). 벽 너머는 깊이 검사로 가려진다.</summary>
    public const float OutlineRange = 2.6f;   // 53차 민: 「충분히 점검 물품에 접근해야 외곽선이」 — 9m → 2.6m(보고 거리 2m 바로 바깥)

    /// <summary>아직 보고 가능하지 않은 점검 대상 외곽선의 진하기(보고 가능한 포커스는 1).</summary>
    public const float HintOutline = 0.65f;

    /// <summary>
    /// 점검 대상 외곽선. 2026-10-03: 보고 가능한 포커스만 → <b>2026-10-04(42차) 민: 「비슷한 물품이 많아 무엇을 점검해야 하는지 모르겠다」</b> —
    /// 오늘 점검표에서 아직 보고하지 않았고 열려 있는 항목의 소품은 <see cref="OutlineRange"/> 안에 오면 옅게(<see cref="HintOutline"/>),
    /// 보고 가능한 포커스는 진하게 그린다. 점검 표식(<c>Inspect H-1</c> 등)은 소품의 자식인 보이지 않는 상자라 그 <b>부모</b>(소화기·현미경·CRT 모니터 …)에 그린다.
    /// 정상·이상 어느 쪽이든 같은 외곽선이다(이상 여부를 알려 주지 않는다). 보고한 항목·아직 열리지 않은 항목(호출 2)은 그리지 않는다.
    /// </summary>
    private void RequestFocusOutline()
    {
        if (_plan == null || !NightRun.IsNightActive || NightRun.IsCaptured) return;
        InspectionBoard board = NightRun.Inspections;
        if (board == null) return;

        PlayerSensors hub = PlayerSensors.Active;
        Transform root = hub != null && hub.PlayerRoot != null ? hub.PlayerRoot : transform;
        int minute = NightRun.NightMinute;
        IReadOnlyList<InspectionAssignment> rows = _plan.Assignments;
        for (int i = 0; i < rows.Count; i++)
        {
            string id = rows[i].Id;
            bool focus = id == _focus;
            if (!focus && (board.StateOf(id) != InspectionState.Pending || !board.IsOpen(id, minute))) continue;

            JudgeTarget target;
            if (!JudgeTargetRegistry.TryGet(rows[i].Item.TargetId, out target) || target == null) continue;
            if (!focus && SensingRules.HorizontalDistance(root.position, target.AnchorPosition) > OutlineRange) continue;
            InteractionOutline.Request(target.transform.parent != null ? target.transform.parent : target.transform, focus ? 1f : HintOutline);
        }
    }

    private InspectionReport Report(bool anomaly)
    {
        string id = _focus;
        InspectionBoard board = NightRun.Inspections;
        bool revising = board != null && board.StateOf(id) != InspectionState.Pending;
        if (revising && (board.StateOf(id) == InspectionState.ReportedAnomaly) == anomaly)
        {
            return InspectionReport.Reject(id, anomaly, ReportRejection.CannotRevise);
        }

        InspectionReport report = revising ? NightRun.ReviseInspection(id, anomaly) : NightRun.ReportInspection(id, anomaly);
        if (report.Accepted)
        {
            _readiness.Remove(id);
            SetFocus(string.Empty);
            _lastResult = "[" + ReportWord(id, anomaly) + "]" + (revising ? "으로 바꿈" : " 보고함");
            _lastResultUntil = Time.unscaledTime + 1.5f;
        }

        return report;
    }

    /// <summary>
    /// 65차(민: 「사다리 없는 자리에서 없다고 보고할 수 있도록」): 보고 키의 이름 — 「있는지 확인」 항목(C-3 사다리)은 [있음]/[없음], 나머지는 [정상]/[이상].
    /// 판정은 그대로다([없음] = 이상).
    /// </summary>
    public static string ReportWord(string itemId, bool anomaly)
    {
        return InspectionCatalog.ReportWord(itemId, anomaly);
    }

    private void SetFocus(string id)
    {
        if (id == null) id = string.Empty;
        if (id == _focus) return;

        _focus = id;
        _hold = 0f;
        Raise(FocusChanged, id);
    }

    private static void Raise(Action<string> handler, string id)
    {
        if (handler == null) return;
        foreach (Delegate d in handler.GetInvocationList())
        {
            try { ((Action<string>)d)(id); }
            catch (Exception e) { Debug.LogException(e); }
        }
    }

    // 50차(민: 「점검 관련 UI 문구가 화면 중앙 아래에」): 개발용 OnGUI(왼쪽 아래) 대신 HUD 화면 중앙 아래 패널로 — 정식 빌드에도 뜬다.
    private void LateUpdate()
    {
        string text = string.Empty;
        float progress = 0f;
        if (showPrompt)
        {
            if (_focus.Length > 0)
            {
                InspectionItem item = InspectionCatalog.Find(_focus);
                string name = item != null ? item.Name : _focus;
                InspectionState st = NightRun.Inspections != null ? NightRun.Inspections.StateOf(_focus) : InspectionState.Pending;
                string ok = ReportWord(_focus, false);
                string bad = ReportWord(_focus, true);
                if (st == InspectionState.Pending) text = name + "    [" + normalKey + "] " + ok + "   [" + anomalyKey + "] " + bad;
                else if (st == InspectionState.ReportedAnomaly) text = name + " — [" + bad + "] 보고함    [" + normalKey + "] " + ok + "으로 바꾸기";   // 60차 정정
                else text = name + " — [" + ok + "] 보고함    [" + anomalyKey + "] " + bad + "으로 바꾸기";
                progress = _hold > 0f ? HoldProgress : 0f;
            }
            else if (Time.unscaledTime < _lastResultUntil)
            {
                text = _lastResult;
            }
        }

        InteractionHud.InspectionPrompt = text;
        InteractionHud.InspectionProgress = progress;
    }

}

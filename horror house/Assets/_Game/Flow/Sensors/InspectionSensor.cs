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
/// <item>[정상]/[이상]은 0.5초 길게 눌러 확정한다. 태블릿을 올리지 않고 쓰는 전용 키(기본 Z = 정상, X = 이상)를 여기서 받는다.
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

    [Tooltip("개발 빌드에서 화면 왼쪽 아래에 보고 안내를 띄운다(정식 UI가 붙기 전 확인용).")]
    [SerializeField] private bool showDevPrompt = true;

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

        IReadOnlyList<InspectionAssignment> rows = board.Plan.Assignments;
        for (int i = 0; i < rows.Count; i++)
        {
            InspectionAssignment row = rows[i];
            string id = row.Id;
            if (board.StateOf(id) != InspectionState.Pending) continue;

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

            bool open = board.IsOpen(id, minute);
            if (changed && r.Ready && open)
            {
                Raise(ReadyTicked, id);
            }

            if (r.Ready && open && distance < bestDistance)
            {
                best = id;
                bestDistance = distance;
            }
        }

        SetFocus(best);
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

        _holdAnomaly = anomaly;
        _hold += Time.unscaledDeltaTime;
        if (_hold >= SensingRules.ReportHoldSeconds)
        {
            _hold = 0f;
            Report(anomaly);
        }
    }

    /// <summary>
    /// 보고 가능한 포커스 항목의 소품에 외곽선(2026-10-03 민 요청). 점검 표식(<c>Inspect H-1</c> 등)은 소품의 자식인
    /// 보이지 않는 상자라, 그 <b>부모</b>(소화기·현미경·CRT 모니터 …)에 그린다. 정상·이상 어느 쪽이든 같은 외곽선이다.
    /// </summary>
    private void RequestFocusOutline()
    {
        if (_focus.Length == 0 || _plan == null) return;

        IReadOnlyList<InspectionAssignment> rows = _plan.Assignments;
        for (int i = 0; i < rows.Count; i++)
        {
            if (rows[i].Id != _focus) continue;
            JudgeTarget target;
            if (JudgeTargetRegistry.TryGet(rows[i].Item.TargetId, out target) && target != null)
            {
                InteractionOutline.Request(target.transform.parent != null ? target.transform.parent : target.transform);
            }

            return;
        }
    }

    private InspectionReport Report(bool anomaly)
    {
        string id = _focus;
        InspectionReport report = NightRun.ReportInspection(id, anomaly);
        if (report.Accepted)
        {
            _readiness.Remove(id);
            SetFocus(string.Empty);
            _lastResult = (anomaly ? "[이상]" : "[정상]") + " 보고함";
            _lastResultUntil = Time.unscaledTime + 1.5f;
        }

        return report;
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

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private void OnGUI()
    {
        if (!showDevPrompt) return;

        string text = null;
        if (_focus.Length > 0)
        {
            InspectionItem item = InspectionCatalog.Find(_focus);
            string name = item != null ? item.Name : _focus;
            text = name + " — [" + normalKey + "] 정상 / [" + anomalyKey + "] 이상 (길게)";
            if (_hold > 0f) text += "  " + Mathf.RoundToInt(HoldProgress * 100f) + "%";
        }
        else if (Time.unscaledTime < _lastResultUntil)
        {
            text = _lastResult;
        }

        if (text == null) return;
        GUI.Label(new Rect(16f, Screen.height - 48f, 640f, 32f), text);
    }
#endif
}

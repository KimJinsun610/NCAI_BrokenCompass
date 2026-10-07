using System.Collections.Generic;
using NightDuty;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 결과창(근무 일지)에 그 밤 점검 결과를 적는다(2026-10-04 43차, 민: 「점검 결과만 결과창에 표기」 — 최종 기획서 「보고별: 정확 / 오보 / 놓침 / 미완료」).
/// 김진선님 <c>DutyLogView</c>는 고치지 않는다 — 그 아래 「특이사항」 칸을 「금일 점검」 칸으로 쓰고(실행 중 인스턴스의 글·크기만 바꿈),
/// 항목 이름과 결과를 편성 순서대로 적는다. 틀린 것(오보·놓침·미완료)은 붉게. 수치·대처법은 적지 않는다.
/// <list type="bullet">
/// <item>기록: 근무 씬에서 <see cref="EventBus.InspectionReported"/>(받아들여진 보고의 판정)를 모으고, <see cref="EventBus.DayEnded"/>에
/// 점검판(<see cref="NightRun.Inspections"/>) 상태와 맞춰 <see cref="InspectionLedger"/>로 확정한다. 재시작으로 되돌려진 보고는 판 상태를 따른다.</item>
/// <item>표시: 근무 일지가 있는 씬이 열리면 자동으로 서서, 일지가 보이는 동안 칸을 채운다.</item>
/// </list>
/// </summary>
[DisallowMultipleComponent]
public sealed class ResultInspectionNotes : MonoBehaviour
{
    private const string Label = "금일 점검";
    private const string WrongColor = "#B3261E";
    private const float BoxHeight = 78f;

    private static readonly Dictionary<string, ReportOutcome> s_outcomes = new Dictionary<string, ReportOutcome>();
    private static InspectionPlan s_plan;
    private static string s_text = string.Empty;
    private static int s_day = -1;

    private DutyLogView _view;
    private TMP_Text _notes;
    private bool _laidOut;

    /// <summary>마지막으로 확정한 점검 결과 글(서식 포함). 없으면 빈 문자열. 디버그·검수.</summary>
    public static string LastText
    {
        get { return s_text; }
    }

    /// <summary><see cref="LastText"/>가 몇 일차 것인지.</summary>
    public static int LastDay
    {
        get { return s_day; }
    }

    // ── 기록 ─────────────────────────────────────────────────

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        // EventBus는 SubsystemRegistration에 비워진다 — 그 뒤에 붙는다.
        s_outcomes.Clear();
        s_plan = null;
        s_text = string.Empty;
        s_day = -1;
        EventBus.InspectionPlanned -= OnPlanned;
        EventBus.InspectionPlanned += OnPlanned;
        EventBus.InspectionReported -= OnReported;
        EventBus.InspectionReported += OnReported;
        EventBus.DayEnded -= OnDayEnded;
        EventBus.DayEnded += OnDayEnded;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnPlanned(InspectionPlan plan)
    {
        if (ReferenceEquals(plan, s_plan)) return;
        s_plan = plan;
        s_outcomes.Clear();
    }

    private static void OnReported(InspectionReport report)
    {
        if (report.Outcome == ReportOutcome.Rejected) return;
        s_outcomes[report.ItemId] = report.Outcome;
    }

    private static void OnDayEnded(DaySummary summary)
    {
        InspectionBoard board = NightRun.Inspections;
        InspectionPlan plan = board != null ? board.Plan : s_plan;
        if (plan == null) return;

        // 판 상태가 정본 — 재시작으로 되돌려진 보고는 지운다.
        Dictionary<string, ReportOutcome> final = new Dictionary<string, ReportOutcome>();
        for (int i = 0; i < plan.Assignments.Count; i++)
        {
            InspectionAssignment a = plan.Assignments[i];
            InspectionState state = board != null ? board.StateOf(a.Id) : InspectionState.Pending;
            if (state == InspectionState.Pending) continue;
            bool saidAnomaly = state == InspectionState.ReportedAnomaly;
            ReportOutcome o;
            if (s_outcomes.TryGetValue(a.Id, out o) && Consistent(o, saidAnomaly)) final[a.Id] = o;
            else final[a.Id] = saidAnomaly ? (a.IsAnomaly ? ReportOutcome.CorrectAnomaly : ReportOutcome.FalseReport) : (a.IsAnomaly ? ReportOutcome.Missed : ReportOutcome.CorrectNormal);
        }

        s_text = Format(InspectionLedger.Build(plan, final));
        s_day = summary.Day;
    }

    private static bool Consistent(ReportOutcome o, bool saidAnomaly)
    {
        switch (o)
        {
            case ReportOutcome.CorrectAnomaly:
            case ReportOutcome.FalseReport:
            case ReportOutcome.ReverseViolated:
                return saidAnomaly;
            case ReportOutcome.CorrectNormal:
            case ReportOutcome.Missed:
            case ReportOutcome.ReverseKept:
                return !saidAnomaly;
            default:
                return false;
        }
    }

    /// <summary>「소화기 정확 · 식수대 놓침 · …」 — 틀린 것은 붉게.</summary>
    public static string Format(List<KeyValuePair<InspectionItem, LedgerMark>> rows)
    {
        if (rows == null || rows.Count == 0) return "점검 없음";
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        for (int i = 0; i < rows.Count; i++)
        {
            if (i > 0) sb.Append("  ·  ");
            // 「반납 상자 미완료」가 줄 끝에서 갈라지지 않게 — TMP는 한글 사이를 공백 없이도 끊으므로(47차: 「변기 / 미완료」) 항목 하나를 <nobr>로 묶는다.
            sb.Append("<nobr>").Append(rows[i].Key.Name.Replace(' ', '\u00A0')).Append('\u00A0');
            string label = InspectionLedger.Label(rows[i].Value);
            if (rows[i].Value == LedgerMark.Correct) sb.Append(label);
            else sb.Append("<color=").Append(WrongColor).Append('>').Append(label).Append("</color>");
            sb.Append("</nobr>");
        }

        return sb.ToString();
    }

    // ── 표시 ─────────────────────────────────────────────────

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.GetComponentInChildren<DutyLogView>(true) == null) continue;
            if (Object.FindFirstObjectByType<ResultInspectionNotes>() != null) return;
            GameObject host = new GameObject("ResultInspectionNotes (auto)");
            SceneManager.MoveGameObjectToScene(host, scene);
            host.AddComponent<ResultInspectionNotes>();
            return;
        }
    }

    private void LateUpdate()
    {
        if (_view == null)
        {
            _view = FindFirstObjectByType<DutyLogView>(FindObjectsInactive.Include);
            if (_view == null) return;
        }

        if (!_view.isActiveAndEnabled || s_text.Length == 0) return;
        if (_notes == null)
        {
            _notes = Find<TMP_Text>(_view.transform, "Txt_Notes");
            if (_notes == null) return;
        }

        if (!_laidOut) LayOut();
        if (_notes.text != s_text) _notes.text = s_text;
    }

    private void LayOut()
    {
        _laidOut = true;
        TMP_Text label = Find<TMP_Text>(_view.transform, "Txt_NotesLabel");
        if (label != null) label.text = Label;

        // 칸을 아래로 조금 늘려 두세 줄이 들어가게(위 가장자리는 그대로).
        RectTransform box = _notes.transform.parent as RectTransform;
        if (box != null && box.sizeDelta.y < BoxHeight)
        {
            float top = box.anchoredPosition.y + (1f - box.pivot.y) * box.sizeDelta.y;
            box.sizeDelta = new Vector2(box.sizeDelta.x, BoxHeight);
            box.anchoredPosition = new Vector2(box.anchoredPosition.x, top - (1f - box.pivot.y) * BoxHeight);
        }

        _notes.textWrappingMode = TMPro.TextWrappingModes.Normal;
        _notes.richText = true;
        _notes.enableAutoSizing = true;
        _notes.fontSizeMin = 14f;
        _notes.fontSizeMax = Mathf.Max(18f, _notes.fontSize);
        _notes.lineSpacing = -8f;
    }

    private static T Find<T>(Transform root, string name) where T : Component
    {
        foreach (T c in root.GetComponentsInChildren<T>(true))
        {
            if (c.name == name) return c;
        }

        return null;
    }
}

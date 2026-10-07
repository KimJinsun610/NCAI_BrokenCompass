using System.Collections.Generic;

namespace NightDuty
{
    /// <summary>결과창의 점검 한 줄 판정(최종 기획서 「근무일지 정산 화면 — 보고별: 정확 / 오보 / 놓침 / 미완료」).</summary>
    public enum LedgerMark
    {
        /// <summary>정확(역보고 준수 포함).</summary>
        Correct = 0,

        /// <summary>오보 — 정상을 [이상](역보고 위반 포함).</summary>
        FalseReport = 1,

        /// <summary>놓침 — 이상을 [정상].</summary>
        Missed = 2,

        /// <summary>미완료 — 끝내 보고하지 않음.</summary>
        Unfinished = 3
    }

    /// <summary>
    /// 그 밤 점검표의 보고별 결과(2026-10-04 43차, 민: 「점검 결과만 결과창에 표기」). 편성 순서대로 항목마다 하나.
    /// 보고 결과는 <see cref="EventBus.InspectionReported"/>의 판정을, 보고하지 않은 항목은 미완료로 친다. 수치(축 델타)는 싣지 않는다.
    /// </summary>
    public static class InspectionLedger
    {
        /// <summary>판정 → 결과창 표시.</summary>
        public static LedgerMark MarkOf(ReportOutcome outcome)
        {
            switch (outcome)
            {
                case ReportOutcome.CorrectAnomaly:
                case ReportOutcome.CorrectNormal:
                case ReportOutcome.ReverseKept:
                    return LedgerMark.Correct;
                case ReportOutcome.Missed:
                    return LedgerMark.Missed;
                case ReportOutcome.FalseReport:
                case ReportOutcome.ReverseViolated:
                    return LedgerMark.FalseReport;
                default:
                    return LedgerMark.Unfinished;
            }
        }

        /// <summary>표시 글자.</summary>
        public static string Label(LedgerMark mark)
        {
            switch (mark)
            {
                case LedgerMark.Correct: return "정확";
                case LedgerMark.FalseReport: return "오보";
                case LedgerMark.Missed: return "놓침";
                default: return "미완료";
            }
        }

        /// <summary>
        /// 편성 순서대로 (항목, 표시). <paramref name="outcomes"/>는 항목 ID → 받아들여진 마지막 보고의 판정. 없으면 미완료.
        /// </summary>
        public static List<KeyValuePair<InspectionItem, LedgerMark>> Build(InspectionPlan plan, IDictionary<string, ReportOutcome> outcomes)
        {
            List<KeyValuePair<InspectionItem, LedgerMark>> list = new List<KeyValuePair<InspectionItem, LedgerMark>>();
            if (plan == null) return list;
            for (int i = 0; i < plan.Assignments.Count; i++)
            {
                InspectionAssignment a = plan.Assignments[i];
                ReportOutcome o;
                LedgerMark mark = outcomes != null && outcomes.TryGetValue(a.Id, out o) && o != ReportOutcome.Rejected ? MarkOf(o) : LedgerMark.Unfinished;
                list.Add(new KeyValuePair<InspectionItem, LedgerMark>(a.Item, mark));
            }

            return list;
        }
    }
}

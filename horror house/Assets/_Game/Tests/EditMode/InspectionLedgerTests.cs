using System.Collections.Generic;
using NUnit.Framework;

namespace NightDuty.Tests
{
    /// <summary>결과창 점검 결과(43차, 최종 기획서 「보고별: 정확 / 오보 / 놓침 / 미완료」).</summary>
    public sealed class InspectionLedgerTests
    {
        [Test]
        public void 판정이_정확_오보_놓침_미완료로_간다()
        {
            Assert.AreEqual(LedgerMark.Correct, InspectionLedger.MarkOf(ReportOutcome.CorrectAnomaly));
            Assert.AreEqual(LedgerMark.Correct, InspectionLedger.MarkOf(ReportOutcome.CorrectNormal));
            Assert.AreEqual(LedgerMark.Correct, InspectionLedger.MarkOf(ReportOutcome.ReverseKept), "역보고 준수는 정확");
            Assert.AreEqual(LedgerMark.Missed, InspectionLedger.MarkOf(ReportOutcome.Missed));
            Assert.AreEqual(LedgerMark.FalseReport, InspectionLedger.MarkOf(ReportOutcome.FalseReport));
            Assert.AreEqual(LedgerMark.FalseReport, InspectionLedger.MarkOf(ReportOutcome.ReverseViolated));
            Assert.AreEqual(LedgerMark.Unfinished, InspectionLedger.MarkOf(ReportOutcome.Rejected));
            Assert.AreEqual("미완료", InspectionLedger.Label(LedgerMark.Unfinished));
        }

        [Test]
        public void 편성_순서대로_보고하지_않은_항목은_미완료()
        {
            InspectionPlan plan = new InspectionPlan(2, new[]
            {
                new InspectionAssignment(InspectionCatalog.Find("H-1"), true, Band.Band1, false),
                new InspectionAssignment(InspectionCatalog.Find("C-2"), false, Band.Band0, false),
                new InspectionAssignment(InspectionCatalog.Find("K-1"), false, Band.Band0, false)
            }, SpaceId.None, string.Empty);
            Dictionary<string, ReportOutcome> o = new Dictionary<string, ReportOutcome>
            {
                { "K-1", ReportOutcome.CorrectNormal },
                { "H-1", ReportOutcome.Missed }
            };

            List<KeyValuePair<InspectionItem, LedgerMark>> rows = InspectionLedger.Build(plan, o);
            Assert.AreEqual(3, rows.Count);
            Assert.AreEqual("H-1", rows[0].Key.Id);
            Assert.AreEqual(LedgerMark.Missed, rows[0].Value);
            Assert.AreEqual(LedgerMark.Unfinished, rows[1].Value);
            Assert.AreEqual(LedgerMark.Correct, rows[2].Value);
            Assert.AreEqual(0, InspectionLedger.Build(null, o).Count);
        }
    }
}

using System.Collections.Generic;
using NUnit.Framework;

namespace NightDuty.Tests
{
    /// <summary>
    /// 68차 — 민: 「조기퇴근은 모든 지시가 완료됐을 때만. 점검 완료 + 추가 지시 있음 → 불가 · 점검 완료 + 모든 추가 지시까지 완료 → 가능.
    /// 점검 완료 이후 새로 생기는 지시도 끝나기 전까지는 퇴근할 수 없게」.
    /// </summary>
    public sealed class SixtyEighthPassTests
    {
        private int _clock;

        [SetUp]
        public void SetUp()
        {
            NightRun.StartNewRun();
            _clock = 30;
        }

        [TearDown]
        public void TearDown()
        {
            NightRun.InspectionPlanOverride = null;
            NightRun.InspectionsEnabled = false;
            NightRun.DutiesEnabled = false;
            NightRun.JudgingWindowEnabled = false;
            NightRun.StartNewRun();
            EventBus.ClearAll();
        }

        private static void UsePlan(params InspectionAssignment[] rows)
        {
            NightRun.InspectionPlanOverride = (day, shown) => new InspectionPlan(day, rows, SpaceId.None, string.Empty);
        }

        private static InspectionAssignment Row(string id, bool anomaly)
        {
            return new InspectionAssignment(InspectionCatalog.Find(id), anomaly, Band.Band1, false);
        }

        /// <summary>제한 있는 지시를 내고 기한을 넘겨 끝낸다(놓친 지시는 다시 할 수 없으니 끝난 것).</summary>
        private static void IssueAndExpire(DutyDispatcher d, string id, float minute)
        {
            DutyEvent ev;
            Assert.IsTrue(d.Force(id, out ev), id);
            Assert.IsTrue(d.Pending(minute), id + " 진행 중");
            Assert.IsTrue(d.Tick(new DutyInput { Minute = minute, Dt = DutyCatalog.LimitSeconds + 1f }, out ev), id);
            Assert.AreEqual(DutyOutcome.Missed, ev.Outcome, id);
        }

        [Test]
        public void 지시기는_진행_중이거나_앞으로_나올_지시가_있으면_남았다고_한다()
        {
            DutyDispatcher d = new DutyDispatcher(1, s => 0f, 5);
            Assert.IsTrue(d.Pending(30f), "아직 하나도 안 냈다 — 상한까지 나온다");
            Assert.IsTrue(d.Pending(NightClock.Call1 - 1f), "마디 상한에 막혀도 다음 마디에 나온다");

            for (int i = 0; i < DutyCatalog.DailyCap(1); i++) IssueAndExpire(d, new[] { "W1", "W3", "W6" }[i], 30f);
            Assert.AreEqual(DutyCatalog.DailyCap(1), d.IssuedToday);
            Assert.IsTrue(d.Pending(30f), "근무일지(W5)는 이완 구간에 나온다");
            Assert.IsFalse(d.Pending(NightClock.Call2 + 1f), "상한까지 냈고 근무일지 시간도 지났다");

            DutyEvent ev;
            Assert.IsTrue(d.Force("W2", out ev));
            Assert.IsTrue(d.Pending(NightClock.JudgingEnd + 1f), "제한 없는 지시도 끝내기 전까지는 남은 것");
        }

        [Test]
        public void 점검을_다_보고해도_근무_지시가_남으면_조기퇴근할_수_없다()
        {
            UsePlan(Row("H-2", false), Row("C-1", true));
            NightRun.DutiesEnabled = true;
            NightRun.BeginNight(1, () => _clock);
            NightRun.ReportInspection("H-2", false);
            NightRun.ReportInspection("C-1", true);
            Assert.AreEqual(0, NightRun.Inspections.RemainingCount);

            Assert.IsTrue(NightRun.DutiesPending);
            Assert.IsTrue(NightRun.InstructionsPending);
            Assert.IsFalse(NightRun.CanEndShiftEarly, "점검 완료 + 추가 지시 있음 → 불가");

            DutyDispatcher d = NightRun.Duties;
            foreach (string id in new[] { "W1", "W3", "W6" }) IssueAndExpire(d, id, _clock);
            Assert.IsFalse(NightRun.CanEndShiftEarly, "근무일지(W5)가 아직");

            _clock = (int)NightClock.Call2 + 1;
            Assert.IsFalse(NightRun.DutiesPending);
            Assert.IsTrue(NightRun.CanEndShiftEarly, "점검 완료 + 모든 추가 지시 완료 → 가능");
        }

        [Test]
        public void 퇴근할_수_있게_된_뒤_새_지시가_오면_끝낼_때까지_다시_막힌다()
        {
            UsePlan(Row("H-2", false));
            NightRun.DutiesEnabled = true;
            List<string> texts = new List<string>();
            EventBus.DutySent += m => texts.Add(m.Text);
            NightRun.BeginNight(1, () => _clock);
            NightRun.ReportInspection("H-2", false);
            DutyDispatcher d = NightRun.Duties;
            foreach (string id in new[] { "W1", "W3", "W6" }) IssueAndExpire(d, id, _clock);
            _clock = (int)NightClock.Call2 + 1;
            Assert.IsTrue(NightRun.CanEndShiftEarly);

            NightRun.Tick(0.1f);
            NightRun.Tick(0.1f);
            Assert.AreEqual(1, texts.FindAll(t => t == NightRun.ShiftReadyText).Count, "모든 지시를 마쳤다는 문자는 한 번");

            DutyEvent ev;
            Assert.IsTrue(d.Force("W7", out ev), "점검을 끝낸 뒤 새로 생긴 지시");
            Assert.IsFalse(NightRun.CanEndShiftEarly, "그 지시가 끝나기 전에는 퇴근할 수 없다");
            NightRun.Tick(0.1f);

            Assert.IsTrue(d.Tick(new DutyInput { Minute = _clock, Dt = DutyCatalog.LimitSeconds + 1f }, out ev));
            Assert.IsTrue(NightRun.CanEndShiftEarly);
            NightRun.Tick(0.1f);
            Assert.AreEqual(2, texts.FindAll(t => t == NightRun.ShiftReadyText).Count, "다시 막혔다가 풀리면 다시 알린다");
        }

        [Test]
        public void 근무_지시가_남았으면_점검의_마지막_지시_안내를_붙이지_않는다()
        {
            UsePlan(Row("H-2", false));
            NightRun.DutiesEnabled = true;
            NightRun.InspectionDripEnabled = true;
            try
            {
                NightRun.BeginNight(1, () => _clock);
                Assert.IsTrue(NightRun.DebugIssueOrder());
                InspectionOrder last = NightRun.Orders.Orders[NightRun.Orders.Orders.Count - 1];
                Assert.IsFalse(NightRun.IsFinalOrder(last), "근무 지시가 아직 남았다");
                StringAssert.DoesNotContain(NightRun.FinalOrderNotice, NightRun.OrderMessage(last));
            }
            finally
            {
                NightRun.InspectionDripEnabled = false;
            }
        }
    }
}

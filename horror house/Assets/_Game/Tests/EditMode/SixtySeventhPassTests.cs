using System.Collections.Generic;
using NUnit.Framework;

namespace NightDuty.Tests
{
    /// <summary>
    /// 67차 — 민: 「지시 사항 중 물품 점검이 비중이 더 높은데 지시 사항과 비중이 균일했으면 · 제한시간이 존재하고 명시되면 · 문장은 너무 길지 않게 ·
    /// 하루 시간 05:00까지 · 1시간마다 알림 · 배터리 빈도·위치를 늘리고 1~5일차가 될수록 살짝 줄게」.
    /// </summary>
    public sealed class SixtySeventhPassTests
    {
        [Test]
        public void 근무_지시_문자에는_마감_시각이_붙고_제한_없는_지시는_없다()
        {
            // 67차 ②(민: 「120초로 고정 · 『몇 시 몇 분까지』로 표시 · W2·W4·W5·W15는 제한 없음」). 게임 시계 24배 — 120초 = 48분.
            Assert.AreEqual(48f, DutyCatalog.LimitGameMinutes, 0.001f);
            Assert.AreEqual("02:08까지", DutyCatalog.DeadlineText(80f));
            DutyDef w1 = DutyCatalog.Find("W1");
            Assert.AreEqual("[근무 지시] CCTV 전 채널 순회 바랍니다.\n02:08까지", DutyCatalog.MessageText(w1, 1, 80f));
            Assert.AreEqual("[근무 지시] 경비실 근무일지 서명", DutyCatalog.MessageText(DutyCatalog.Find("W5"), 1, 145f));
            Assert.AreEqual("[근무 지시] 복도 끝 비상구 응시 후 경비실 복귀\n01:08까지", DutyCatalog.MessageText(DutyCatalog.Find("W6"), 1, 20f));
            foreach (DutyDef d in DutyCatalog.All)
            {
                bool free = d.Id == "W2" || d.Id == "W4" || d.Id == "W5" || d.Id == "W15";
                Assert.AreEqual(free ? 0f : DutyCatalog.LimitSeconds, d.Seconds, d.Id);
                Assert.AreEqual(free, !DutyCatalog.MessageText(d, 1, 30f).Contains("까지"), d.Id);
                Assert.LessOrEqual(d.Order.Length, 32, d.Id + " 문장은 짧게: " + d.Order);
            }
        }

        [Test]
        public void 점검과_근무_지시_수가_비슷하다()
        {
            int[] items = { 4, 5, 5, 6, 6 };
            int[] duties = { 3, 3, 4, 4, 4 };
            for (int day = 1; day <= 5; day++)
            {
                Assert.AreEqual(items[day - 1], InspectionQuota.Items(day), day + "일차 점검");
                Assert.AreEqual(duties[day - 1], DutyCatalog.DailyCap(day), day + "일차 근무 지시");
                Assert.LessOrEqual(InspectionQuota.Anomalies(day), InspectionQuota.Items(day));
            }
        }

        [Test]
        public void 점검_차례면_근무_지시를_쉬고_차례가_오면_낸다()
        {
            DutyDispatcher d = new DutyDispatcher(1, s => 0f, 5);
            DutyEvent ev;
            DutyInput wait = new DutyInput { Minute = 40f, Dt = 0.5f, SinceInspection = 100f, InspectionTurn = true };
            for (int i = 0; i < 20; i++) Assert.IsFalse(d.Tick(wait, out ev), "점검 지시 차례");
            Assert.IsTrue(d.CanIssueSoon(40f));
            DutyInput go = wait;
            go.InspectionTurn = false;
            Assert.IsTrue(d.Tick(go, out ev));
            Assert.AreEqual(DutyOutcome.Issued, ev.Outcome);
            Assert.IsFalse(d.CanIssueSoon(40f), "진행 중인 지시가 있다");
        }

        [Test]
        public void 근무_지시는_이완_구간과_판정_밖에서는_곧_낼_수_없다()
        {
            DutyDispatcher d = new DutyDispatcher(1, s => 0f, 5);
            Assert.IsFalse(d.CanIssueSoon(NightClock.JudgingStart - 1f));
            Assert.IsFalse(d.CanIssueSoon(NightClock.RelaxStart + 1f));
            Assert.IsFalse(d.CanIssueSoon(NightClock.JudgingEnd));
            Assert.IsTrue(d.CanIssueSoon(NightClock.Call2 + 1f));
        }

        [Test]
        public void 점검_지시_제한시간은_120초_목격_지시는_없음()
        {
            InspectionOrder one = new InspectionOrder(1, OrderKind.Regular, 30, new List<string> { "H-1" }, SpaceId.Corridor);
            Assert.AreEqual(DutyCatalog.LimitSeconds, NightRun.OrderLimitSeconds(one));
            InspectionOrder many = new InspectionOrder(3, OrderKind.CatchUp, 200, new List<string> { "H-1", "H-2", "C-1", "C-2", "S-1", "T-1" }, SpaceId.None);
            Assert.AreEqual(DutyCatalog.LimitSeconds, NightRun.OrderLimitSeconds(many));
            InspectionOrder witness = new InspectionOrder(4, OrderKind.Witness, 200, new List<string> { "T-1" }, SpaceId.Toilet);
            Assert.AreEqual(0f, NightRun.OrderLimitSeconds(witness));
        }

        [Test]
        public void 배터리는_더_자주_놓이고_날이_갈수록_조금_준다()
        {
            int prev = int.MaxValue;
            for (int day = 1; day <= 5; day++)
            {
                int n = BatteryRules.PlacedOn(day);
                Assert.GreaterOrEqual(n, 4, day + "일차");
                Assert.LessOrEqual(n, prev, "줄기만 한다");
                Assert.LessOrEqual(prev == int.MaxValue ? 0 : prev - n, 1, "살짝");
                prev = n;
            }
        }

        [Test]
        public void 밤은_05시까지이고_정시는_넷()
        {
            Assert.AreEqual(300, NightClock.ShiftEnd);
            Assert.AreEqual(750f, NightClock.RealSecondsPerNight);
            CollectionAssert.AreEqual(new[] { 60, 120, 180, 240 }, NightClock.HourMarks);
            Assert.Less(NightClock.JudgingEnd, NightClock.ShiftEnd);
            Assert.Less(InspectionDispatcher.WorkDeadline, NightClock.ShiftEnd);
            Assert.Less(InspectionDispatcher.EarlyDeadline, NightClock.Call2);
        }
    }
}

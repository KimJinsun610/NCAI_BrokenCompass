using NUnit.Framework;

namespace NightDuty.Tests
{
    /// <summary>2026-11-06(김진선님: 「초반부에 익숙해지기 전 상황이 너무 급함」 — A+C안): 1·2일차는 밤이 길고 지시 간격이 넓다.</summary>
    public sealed class ShiftPaceTests
    {
        [TestCase(1, 900f, 16f)]
        [TestCase(2, 750f, 19.2f)]
        [TestCase(3, 600f, 24f)]
        [TestCase(5, 600f, 24f)]
        public void 일차별_밤_길이와_배속(int day, float seconds, float pace)
        {
            Assert.AreEqual(seconds, NightClock.RealSecondsPerNightFor(day), 0.001f);
            Assert.AreEqual(pace, NightClock.GameSecondsPerRealSecondFor(day), 0.001f);
            Assert.AreEqual(seconds, NightClock.RealSecondsAt(NightClock.ShiftEnd, day), 0.01f, "게임 시계 00:00~04:00은 그대로 — 실시간만 길다");
            Assert.AreEqual(NightClock.Call2, NightClock.MinuteAtRealSeconds(NightClock.RealSecondsAt(NightClock.Call2, day), day), 0.001f);
        }

        [Test]
        public void 셋째_날부터는_옛_값_그대로()
        {
            Assert.AreEqual(NightClock.RealSecondsPerNight, NightClock.RealSecondsPerNightFor(3));
            Assert.AreEqual(NightClock.RealSecondsAt(NightClock.Call1), NightClock.RealSecondsAt(NightClock.Call1, 4), 0.001f);
            Assert.AreEqual(InspectionDispatcher.NextAfterReport, InspectionDispatcher.NextAfterReportFor(3));
            Assert.AreEqual(DutyDispatcher.GapSeconds, DutyDispatcher.GapFor(3));
            Assert.AreEqual(ProgramCatalog.ClassroomDwellRealSeconds, ProgramCatalog.ClassroomDwellRealSecondsFor(3), 0.001f);
        }

        [TestCase(1, 15f, 30f)]
        [TestCase(2, 10f, 20f)]
        [TestCase(3, 4f, 10f)]
        public void 일차별_지시_간격(int day, float afterReport, float dutyGap)
        {
            Assert.AreEqual(afterReport, InspectionDispatcher.NextAfterReportFor(day));
            Assert.AreEqual(dutyGap, DutyDispatcher.GapFor(day));
        }

        [TestCase(1, 90f)]
        [TestCase(2, 75f)]
        [TestCase(3, 60f)]
        public void C6_판정_시간은_문구의_24분에_맞춘다(int day, float seconds)
        {
            Assert.AreEqual(seconds, ProgramCatalog.ClassroomDwellRealSecondsFor(day), 0.001f);
        }

        [Test]
        public void 마감_시각은_그날_배속으로_적는다()
        {
            Assert.AreEqual("01:24까지", DutyCatalog.DeadlineText(60f, 90f, 1), "1일차 16배 — 90초 = 24분");
            Assert.AreEqual("01:36까지", DutyCatalog.DeadlineText(60f, 90f, 3), "3일차 24배 — 90초 = 36분");
            Assert.AreEqual(DutyCatalog.DeadlineText(60f, 90f, 3), DutyCatalog.DeadlineText(60f, 90f));
        }
    }
}

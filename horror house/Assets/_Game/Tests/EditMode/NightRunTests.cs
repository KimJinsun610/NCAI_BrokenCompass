using System.Collections.Generic;
using NUnit.Framework;

namespace NightDuty.Tests
{
    /// <summary>회차 창구: 축 이월, 결과 전송, 포획 경로, 위반 시각, 근무일지. 수칙 위반은 새 편성의 G1으로 만든다(<see cref="TestKit"/>).</summary>
    public sealed class NightRunTests
    {
        private int _clock;

        [SetUp]
        public void SetUp()
        {
            NightRun.StartNewRun();
            _clock = 0;
        }

        [TearDown]
        public void TearDown()
        {
            NightRun.InspectionPlanOverride = null;
            NightRun.ProgramEnabled = false;
            NightRun.StartNewRun();
            EventBus.ClearAll();
        }

        [Test]
        public void 공간에_보이는_조도_구간은_축의_연출_구간을_따른다()
        {
            NightRun.BeginNight(1, () => _clock);
            Assert.AreEqual(Band.Band0, NightRun.ShownBand(SpaceId.Toilet, FearAxis.Illuminance));

            NightRun.DebugAddAxis(FearAxis.Illuminance, 55);

            Assert.AreEqual(NightRun.Shown.GetBand(FearAxis.Illuminance), NightRun.ShownBand(SpaceId.Toilet, FearAxis.Illuminance));
            Assert.AreEqual(Band.Band2, NightRun.ShownBand(SpaceId.Corridor, FearAxis.Illuminance));
            Assert.AreEqual(Band.Band2, NightRun.ShownBand(SpaceId.Library, FearAxis.Illuminance), "순찰 공간이 아니면 축 전체의 연출 구간");
            Assert.AreEqual(Band.Band0, NightRun.ShownBand(SpaceId.Corridor, FearAxis.Auditory), "다른 축은 그대로");
        }

        [Test]
        public void 축값은_다음날로_이월된다()
        {
            TestKit.BeginProgramNight(1, () => _clock);
            TestKit.ViolateRunning();
            Assert.IsTrue(NightRun.RequestEndNight());

            NightRun.BeginNight(2, () => _clock);

            Assert.AreEqual(2, NightRun.Day);
            Assert.AreEqual(Deltas.RuleViolation, NightRun.Axes.GetValue(FearAxis.Auditory));
        }

        [Test]
        public void 새회차는_축을_0으로_되돌린다()
        {
            TestKit.BeginProgramNight(1, () => _clock);
            TestKit.ViolateRunning();
            NightRun.StartNewRun();

            Assert.AreEqual(0, NightRun.Axes.GetValue(FearAxis.Auditory));
            Assert.AreEqual(0, NightRun.Day);
            Assert.IsFalse(NightRun.IsNightActive);
        }

        [Test]
        public void 종료요청은_DayEnded를_한번_보내고_위반시각을_담는다()
        {
            int received = 0;
            DaySummary got = default;
            EventBus.DayEnded += s =>
            {
                received++;
                got = s;
            };

            TestKit.BeginProgramNight(1, () => _clock);
            _clock = 41;
            TestKit.ViolateRunning();
            _clock = 55;

            Assert.IsTrue(NightRun.RequestEndNight());
            Assert.IsFalse(NightRun.RequestEndNight(), "이미 끝난 밤은 다시 끝나지 않는다");

            Assert.AreEqual(1, received);
            Assert.AreEqual(1, got.Day);
            Assert.AreEqual(NightOutcome.Completed, got.Outcome);
            Assert.AreEqual(1, got.Violations);
            CollectionAssert.AreEqual(new[] { 41 }, got.ViolationMinutes);
            Assert.AreEqual(Deltas.RuleViolation, got.Auditory);
            Assert.AreEqual(0, got.Layout, "어기지 않은 축은 0 그대로다");
            Assert.AreEqual(0, got.Illuminance, "어기지 않은 축은 0 그대로다");
            Assert.AreEqual("G1", got.Results[0].RuleId);
            Assert.AreEqual(FinalOutcome.Violated, got.Results[0].Outcome);
            Assert.AreEqual("0:41", DaySummary.FormatMinutes(got.ViolationMinutes[0]));
            Assert.IsNull(got.ImprintAxis);
            Assert.AreEqual(0, got.ConflictsTotal);
        }

        [Test]
        public void 포획되면_AxisCritical만_한번_가고_종료요청은_거절된다()
        {
            int critical = 0;
            int dayEnded = 0;
            EventBus.AxisCritical += a => critical++;
            EventBus.DayEnded += s => dayEnded++;

            NightRun.BeginNight(1, () => _clock);
            NightRun.Send(JudgeSignal.OfSpace(SignalKind.SpaceEntered, SpaceId.Corridor));
            NightRun.DebugForceCapture(FearAxis.Auditory);

            Assert.IsTrue(NightRun.IsCaptured);
            Assert.AreEqual(1, critical);
            Assert.AreEqual(FearAxis.Auditory, NightRun.Cause.Axis);
            Assert.AreEqual(SpaceId.Corridor, NightRun.Cause.Space);
            Assert.IsFalse(NightRun.RequestEndNight());
            Assert.AreEqual(0, dayEnded);

            DaySummary summary = NightRun.BuildSummary();
            Assert.AreEqual(NightOutcome.Captured, summary.Outcome);
            Assert.AreEqual(100, summary.Auditory);
        }

        [Test]
        public void 결과의_점검칸은_점검표_보고수와_항목수다()
        {
            NightRun.InspectionPlanOverride = (day, shown) => new InspectionPlan(day,
                new[]
                {
                    new InspectionAssignment(InspectionCatalog.Find("H-2"), false, Band.Band0, false),
                    new InspectionAssignment(InspectionCatalog.Find("H-4"), false, Band.Band0, false)
                },
                SpaceId.None, string.Empty, null);
            NightRun.BeginNight(1, () => _clock);
            Assert.IsTrue(NightRun.ReportInspection("H-2", false).Accepted);

            DaySummary summary = NightRun.BuildSummary();
            Assert.AreEqual(1, summary.PatrolDone);
            Assert.AreEqual(2, summary.PatrolTotal);
        }

        [Test]
        public void 근무일지는_편성덱_순서로_새수칙마다_한줄이고_어긴_수칙에만_빨간줄을_긋는다()
        {
            TestKit.BeginProgramNight(1, () => _clock);
            IReadOnlyList<RuleDef> deck = NightRun.Program.Deck;
            TestKit.ViolateRunning();
            Assert.IsTrue(NightRun.RequestEndNight());

            IReadOnlyList<DutyLogEntry> log = NightRun.LastSummary.DutyLog;
            Assert.AreEqual(deck.Count, log.Count, "편성된 새 수칙마다 한 줄");

            for (int i = 0; i < log.Count; i++)
            {
                Assert.AreEqual(i + 1, log[i].Number);
                Assert.AreEqual(deck[i].Id, log[i].RuleId);
                Assert.AreEqual(deck[i].Text, log[i].PlayerText);
                if (log[i].RuleId == "G1")
                {
                    Assert.AreEqual(RuleVerdict.Violated, log[i].Verdict);
                    Assert.IsTrue(log[i].Struck, "어긴 수칙");
                }
                else
                {
                    Assert.IsFalse(log[i].Struck, log[i].RuleId + ": 어기지 않은 수칙은 표시 없음(가지 않은 공간이어도)");
                }
            }

            Assert.Greater(NightRun.TodayDeck.Count, 0, "밤이 닫혀도 오늘 태블릿 수칙은 남는다");
        }

        [Test]
        public void 태블릿을_든_채_들어간_공간도_현재공간이다()
        {
            NightRun.BeginNight(1, () => _clock);
            NightRun.Send(JudgeSignal.Tab(true));
            NightRun.Send(JudgeSignal.OfSpace(SignalKind.SpaceEntered, SpaceId.Toilet));
            NightRun.Send(JudgeSignal.Tab(false));

            Assert.AreEqual(SpaceId.Toilet, NightRun.CurrentSpace, "태블릿을 든 채 들어간 공간도 현재 공간이다");
        }

        [Test]
        public void 새밤은_현재공간과_덱을_새로_쓴다()
        {
            TestKit.BeginProgramNight(1, () => _clock);
            NightRun.Send(JudgeSignal.OfSpace(SignalKind.SpaceEntered, SpaceId.Toilet));
            Assert.IsTrue(NightRun.RequestEndNight());

            NightRun.ProgramEnabled = false;
            NightRun.BeginNight(2, () => _clock);

            Assert.AreEqual(SpaceId.None, NightRun.CurrentSpace, "새 밤은 공간 신호가 올 때까지 모른다");
            Assert.AreEqual(0, NightRun.TodayDeck.Count, "편성이 꺼지면 태블릿 수칙도 비운다");
        }

        [Test]
        public void 포획되면_밤이_닫히고_구독자는_닫히기전_결과를_받는다()
        {
            DaySummary atCritical = default;
            EventBus.AxisCritical += a => atCritical = NightRun.BuildSummary();

            TestKit.BeginProgramNight(1, () => _clock);
            NightRun.Send(JudgeSignal.OfSpace(SignalKind.SpaceEntered, SpaceId.Corridor));
            NightRun.DebugForceCapture(FearAxis.Layout);

            Assert.IsFalse(NightRun.IsNightActive, "포획 즉시 그날 밤은 닫힌다");
            Assert.AreEqual(NightOutcome.Captured, atCritical.Outcome);
            Assert.AreEqual(NightRun.Program.Deck.Count, atCritical.DutyLog.Count);
            Assert.AreEqual(NightOutcome.Captured, NightRun.LastSummary.Outcome);

            TestKit.ViolateRunning();
            Assert.AreEqual(0, NightRun.Axes.GetValue(FearAxis.Auditory), "닫힌 밤의 신호와 시간은 무시한다");
            Assert.AreEqual(SpaceId.None, NightRun.CurrentSpace);
        }

        [Test]
        public void 신뢰100은_밤을_닫지않는다()
        {
            NightRun.BeginNight(1, () => _clock);
            NightRun.DebugForceCapture(FearAxis.Trust);

            Assert.IsFalse(NightRun.IsCaptured);
            Assert.IsTrue(NightRun.IsNightActive);
            Assert.IsTrue(NightRun.RequestEndNight());
        }

        [Test]
        public void 중단하면_정산없이_닫히고_DayEnded는_보내지않는다()
        {
            int dayEnded = 0;
            EventBus.DayEnded += s => dayEnded++;

            TestKit.BeginProgramNight(1, () => _clock);
            NightRun.Send(JudgeSignal.OfSpace(SignalKind.SpaceEntered, SpaceId.Corridor));
            NightRun.AbandonNight();

            Assert.IsFalse(NightRun.IsNightActive);
            Assert.AreEqual(0, dayEnded);
            Assert.AreEqual(0, NightRun.Axes.GetValue(FearAxis.Trust), "밤 종료 준수를 지급하지 않는다");
            Assert.IsFalse(NightRun.RequestEndNight());
            NightRun.AbandonNight();   // 두 번 불러도 안전
        }

        [Test]
        public void 밤이_시작되기_전의_신호와_시간은_무시한다()
        {
            NightRun.Tick(1f);
            TestKit.ViolateRunning();

            Assert.AreEqual(0, NightRun.Axes.GetValue(FearAxis.Auditory));
            Assert.IsFalse(NightRun.RequestEndNight());
        }
    }
}

using System.Collections.Generic;
using NUnit.Framework;

namespace NightDuty.Tests
{
    /// <summary>회차 창구: 축 이월, 결과 전송, 포획 경로, 위반 시각, 점검 수.</summary>
    public sealed class NightRunTests
    {
        private TestKit _kit;
        private RuleSO _h1;
        private int _clock;

        [SetUp]
        public void SetUp()
        {
            _kit = new TestKit();
            _h1 = _kit.Card(c =>
            {
                c.CardId = "H1";
                c.Space = SpaceId.Corridor;
                c.TriggerKind = SignalKind.DoorAutoOpenObserved;
                c.TargetIds = new[] { "corridor.door.auto", "corridor.door.13" };
                c.Failure = new SignalCondition(SignalKind.DoorCommandAccepted, TargetMatchIds.Trigger, SpaceId.None, FlagFilter.True);
                c.Success = new SignalCondition(SignalKind.PassageCompleted, "corridor.passage");
            });

            NightRun.StartNewRun();
            NightRun.RegisteredTargets = null;
            NightRun.DeckOverride = day => new List<RuleSO> { _h1 };
            _clock = 0;
        }

        [TearDown]
        public void TearDown()
        {
            NightRun.DeckOverride = null;
            NightRun.StartNewRun();
            _kit.Dispose();
        }

        private static void Violate(string door)
        {
            NightRun.Send(JudgeSignal.Target(SignalKind.DoorAutoOpenObserved, door));
            NightRun.Send(JudgeSignal.DoorCommand(door, true, ActionSource.Player));
        }

        [Test]
        public void 축값은_다음날로_이월된다()
        {
            NightRun.BeginNight(1, () => _clock);
            Violate("corridor.door.auto");
            Assert.IsTrue(NightRun.RequestEndNight());

            NightRun.BeginNight(2, () => _clock);

            Assert.AreEqual(2, NightRun.Day);
            Assert.AreEqual(12, NightRun.Axes.GetValue(FearAxis.Layout));
        }

        [Test]
        public void 새회차는_축을_0으로_되돌린다()
        {
            NightRun.BeginNight(1, () => _clock);
            Violate("corridor.door.auto");
            NightRun.StartNewRun();

            Assert.AreEqual(0, NightRun.Axes.GetValue(FearAxis.Layout));
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

            NightRun.BeginNight(3, () => _clock);
            _clock = 41;
            Violate("corridor.door.13");
            _clock = 55;

            Assert.IsTrue(NightRun.RequestEndNight());
            Assert.IsFalse(NightRun.RequestEndNight(), "이미 끝난 밤은 다시 끝나지 않는다");

            Assert.AreEqual(1, received);
            Assert.AreEqual(3, got.Day);
            Assert.AreEqual(NightOutcome.Completed, got.Outcome);
            Assert.AreEqual(1, got.Violations);
            CollectionAssert.AreEqual(new[] { 41 }, got.ViolationMinutes);
            Assert.AreEqual(12, got.Layout);
            Assert.AreEqual(1, got.Results.Count);
            Assert.AreEqual("0:41", DaySummary.FormatMinutes(got.ViolationMinutes[0]));
            Assert.IsNull(got.ImprintAxis);
            Assert.AreEqual(0, got.ConflictsTotal);
        }

        [Test]
        public void 자동으로_열린_그_문만_대상이다()
        {
            NightRun.BeginNight(1, () => _clock);
            NightRun.Send(JudgeSignal.Target(SignalKind.DoorAutoOpenObserved, "corridor.door.auto"));
            NightRun.Send(JudgeSignal.DoorCommand("corridor.door.13", true, ActionSource.Player));

            Assert.AreEqual(0, NightRun.Axes.GetValue(FearAxis.Layout), "다른 문을 닫은 것은 H1 위반이 아니다");

            NightRun.Send(JudgeSignal.DoorCommand("corridor.door.auto", true, ActionSource.Player));
            Assert.AreEqual(12, NightRun.Axes.GetValue(FearAxis.Layout));
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
        public void 점검완료는_공간별로_한번씩_센다()
        {
            NightRun.BeginNight(1, () => _clock);
            NightRun.Send(JudgeSignal.OfSpace(SignalKind.InspectionCompleted, SpaceId.Corridor));
            NightRun.Send(JudgeSignal.OfSpace(SignalKind.InspectionCompleted, SpaceId.Corridor));
            NightRun.Send(JudgeSignal.OfSpace(SignalKind.InspectionCompleted, SpaceId.Toilet));

            DaySummary summary = NightRun.BuildSummary();
            Assert.AreEqual(2, summary.PatrolDone);
            Assert.AreEqual(5, summary.PatrolTotal);
        }

        [Test]
        public void 밤이_시작되기_전의_신호와_시간은_무시한다()
        {
            NightRun.Tick(1f);
            Violate("corridor.door.auto");

            Assert.AreEqual(0, NightRun.Axes.GetValue(FearAxis.Layout));
            Assert.IsFalse(NightRun.RequestEndNight());
        }
    }
}

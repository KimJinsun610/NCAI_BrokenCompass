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

        private RuleSO CornerCard(string id, SpaceId space)
        {
            return _kit.Card(c =>
            {
                c.CardId = id;
                c.Space = space;
                c.TriggerKind = SignalKind.DoorAutoOpenObserved;
                c.TargetIds = new[] { id.ToLowerInvariant() + ".door" };
                c.Failure = new SignalCondition(SignalKind.DoorCommandAccepted, TargetMatchIds.Trigger, SpaceId.None, FlagFilter.True);
                c.Success = new SignalCondition(SignalKind.PassageCompleted, id.ToLowerInvariant() + ".passage");
            });
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
            // 2026-09-21 재설계: 3일차는 일차 하한 24가 먼저 깔리고 그 위에 위반 +12가 얹힌다.
            Assert.AreEqual(DayFloor.Of(3) + 12, got.Layout);
            Assert.AreEqual(DayFloor.Of(3), got.Auditory, "어기지 않은 축은 하한 그대로다");
            Assert.AreEqual(DayFloor.Of(3), got.Illuminance, "어기지 않은 축은 하한 그대로다");
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
        public void 근무일지는_덱순서로_위반과_미방문공간에_빨간줄을_긋는다()
        {
            RuleSO toilet = CornerCard("T1", SpaceId.Toilet);
            RuleSO corridorSafe = CornerCard("H2", SpaceId.Corridor);
            NightRun.DeckOverride = day => new List<RuleSO> { _h1, toilet, corridorSafe };

            NightRun.BeginNight(1, () => _clock);
            NightRun.Send(JudgeSignal.OfSpace(SignalKind.SpaceEntered, SpaceId.Corridor));
            Violate("corridor.door.auto");
            Assert.IsTrue(NightRun.RequestEndNight());

            IReadOnlyList<DutyLogEntry> log = NightRun.LastSummary.DutyLog;
            Assert.AreEqual(3, log.Count);

            Assert.AreEqual(1, log[0].Number);
            Assert.AreEqual("H1", log[0].CardId);
            Assert.AreEqual(CardState.Violated, log[0].State);
            Assert.IsTrue(log[0].Struck, "위반");

            Assert.AreEqual(2, log[1].Number);
            Assert.IsFalse(log[1].Visited);
            Assert.IsTrue(log[1].Struck, "가지 않은 공간");

            Assert.AreEqual(3, log[2].Number);
            Assert.IsTrue(log[2].Visited);
            Assert.IsFalse(log[2].Struck, "간 공간에서 조건이 걸리지 않은 카드는 표시 없음");
            Assert.AreEqual(corridorSafe.PlayerText, log[2].PlayerText);

            Assert.AreEqual(3, NightRun.TodayDeck.Count, "밤이 닫혀도 오늘 덱은 남는다");
        }

        [Test]
        public void Tab중의_공간진입은_방문으로_세지않는다()
        {
            NightRun.BeginNight(1, () => _clock);
            NightRun.Send(JudgeSignal.Tab(true));
            NightRun.Send(JudgeSignal.OfSpace(SignalKind.SpaceEntered, SpaceId.Toilet));
            NightRun.Send(JudgeSignal.Tab(false));

            Assert.IsFalse(NightRun.WasVisitedToday(SpaceId.Toilet));

            NightRun.Send(JudgeSignal.OfSpace(SignalKind.SpaceEntered, SpaceId.Toilet));
            Assert.IsTrue(NightRun.WasVisitedToday(SpaceId.Toilet));
        }

        [Test]
        public void 새밤은_방문기록과_덱을_새로_쓴다()
        {
            NightRun.BeginNight(1, () => _clock);
            NightRun.Send(JudgeSignal.OfSpace(SignalKind.SpaceEntered, SpaceId.Toilet));
            Assert.IsTrue(NightRun.RequestEndNight());

            NightRun.DeckOverride = day => new List<RuleSO>();
            NightRun.BeginNight(2, () => _clock);

            Assert.IsFalse(NightRun.WasVisitedToday(SpaceId.Toilet));
            Assert.AreEqual(0, NightRun.TodayDeck.Count);
        }

        [Test]
        public void 포획되면_밤이_닫히고_구독자는_닫히기전_결과를_받는다()
        {
            DaySummary atCritical = default;
            EventBus.AxisCritical += a => atCritical = NightRun.BuildSummary();

            NightRun.BeginNight(1, () => _clock);
            NightRun.Send(JudgeSignal.OfSpace(SignalKind.SpaceEntered, SpaceId.Corridor));
            NightRun.DebugForceCapture(FearAxis.Layout);

            Assert.IsFalse(NightRun.IsNightActive, "포획 즉시 그날 밤은 닫힌다");
            Assert.AreEqual(NightOutcome.Captured, atCritical.Outcome);
            Assert.AreEqual(1, atCritical.DutyLog.Count);
            Assert.AreEqual(NightOutcome.Captured, NightRun.LastSummary.Outcome);

            NightRun.Tick(1f);
            NightRun.Send(JudgeSignal.OfSpace(SignalKind.SpaceEntered, SpaceId.Toilet));
            Assert.IsFalse(NightRun.WasVisitedToday(SpaceId.Toilet), "닫힌 밤의 신호는 무시한다");
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
            RuleSO nightEnd = _kit.Card(c =>
            {
                c.CardId = "H4";
                c.Space = SpaceId.Corridor;
                c.IsLongTerm = true;
                c.TriggerKind = SignalKind.ClueIdentified;
                c.TargetIds = new[] { "corridor.box" };
                c.Failure = new ProximityCondition("");
                c.Success = new SignalCondition(SignalKind.PassageCompleted, "corridor.passage");
                c.SettleAt = SettleAt.AtNightEnd;
                c.Radius = 1.5f;
                c.FailureAxis = FearAxis.Layout;
                c.FailureDelta = 12;
            });
            NightRun.DeckOverride = day => new List<RuleSO> { nightEnd };

            NightRun.BeginNight(1, () => _clock);
            NightRun.Send(JudgeSignal.Target(SignalKind.ClueIdentified, "corridor.box"));
            NightRun.Send(JudgeSignal.Target(SignalKind.PassageCompleted, "corridor.passage"));
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
            Violate("corridor.door.auto");

            Assert.AreEqual(0, NightRun.Axes.GetValue(FearAxis.Layout));
            Assert.IsFalse(NightRun.RequestEndNight());
        }
    }
}

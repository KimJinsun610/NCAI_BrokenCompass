using System.Collections.Generic;
using NUnit.Framework;

namespace NightDuty.Tests
{
    /// <summary>
    /// 코어 확장: 밤 시작 신호(장기 카드 시작), 밤 종료 신호 전달, 순서 조건, 문 의무 장부, 준수 전용 카드.
    /// </summary>
    public sealed class CoreExtensionTests
    {
        private TestKit _kit;

        [SetUp]
        public void SetUp()
        {
            _kit = new TestKit();
        }

        [TearDown]
        public void TearDown()
        {
            _kit.Dispose();
        }

        private RuleSO NightLong(ICondition success, ICondition failure)
        {
            return _kit.Card(c =>
            {
                c.CardId = "C6";
                c.Space = SpaceId.Classroom_1_1;
                c.IsLongTerm = true;
                c.TriggerKind = SignalKind.NightBegan;
                c.TargetIds = new[] { "corridor.door.11", "corridor.door.13" };
                c.Success = success;
                c.Failure = failure;
                c.SuccessDelta = 3;
                c.FailureAxis = FearAxis.Layout;
                c.FailureDelta = 12;
                c.Radius = 0f;
            });
        }

        [Test]
        public void 밤시작신호_카드는_BeginNight에서_한번만_시작한다()
        {
            RuleSO card = NightLong(new SignalCondition(SignalKind.NightEndAccepted), new DoorObligationCondition(string.Empty));
            RuleBook book = _kit.Book(card);

            Assert.AreEqual(CardState.Waiting, book.Watchers[0].State);
            book.BeginNight();
            Assert.AreEqual(CardState.Active, book.Watchers[0].State);

            // 밖에서 보낸 밤 시작 신호도 BeginNight로 간다(두 번째는 무시).
            book.Dispatch(new JudgeSignal(SignalKind.NightBegan, SpaceId.None, "", ActionSource.Direction, false, 0f));
            Assert.AreEqual(CardState.Active, book.Watchers[0].State);
        }

        [Test]
        public void 밤종료신호로_준수가_정산된다()
        {
            RuleSO card = NightLong(new SignalCondition(SignalKind.NightEndAccepted), new DoorObligationCondition(string.Empty));
            RuleBook book = _kit.Book(card);
            book.BeginNight();

            book.EndNight();

            Assert.AreEqual(CardState.Complied, book.Watchers[0].State);
            Assert.AreEqual(3, _kit.Axes.GetValue(FearAxis.Trust));
        }

        [Test]
        public void 직접연문을_두개_남기면_밤종료에_배치12_한번()
        {
            RuleSO card = NightLong(new SignalCondition(SignalKind.NightEndAccepted), new DoorObligationCondition(string.Empty));
            RuleBook book = _kit.Book(card);
            book.BeginNight();

            book.Dispatch(JudgeSignal.DoorCommand("corridor.door.11", false, ActionSource.Player));
            book.Dispatch(JudgeSignal.DoorCommand("corridor.door.13", false, ActionSource.Player));
            Assert.AreEqual(CardState.Active, book.Watchers[0].State);

            book.EndNight();

            Assert.AreEqual(CardState.Violated, book.Watchers[0].State);
            Assert.AreEqual(12, _kit.Axes.GetValue(FearAxis.Layout));
            Assert.AreEqual(0, _kit.Axes.GetValue(FearAxis.Trust));
        }

        [Test]
        public void 열고닫으면_의무가_지워지고_다시열면_생긴다()
        {
            RuleSO card = NightLong(new SignalCondition(SignalKind.NightEndAccepted), new DoorObligationCondition(string.Empty));

            RuleBook closed = _kit.Book(card);
            closed.BeginNight();
            closed.Dispatch(JudgeSignal.DoorCommand("corridor.door.11", false, ActionSource.Player));
            closed.Dispatch(JudgeSignal.DoorCommand("corridor.door.11", true, ActionSource.Player));
            closed.EndNight();
            Assert.AreEqual(CardState.Complied, closed.Watchers[0].State);

            FearAxisSystem axes2 = new FearAxisSystem();
            RuleBook reopened = new RuleBook(new[] { card }, axes2, null, null);
            reopened.BeginNight();
            reopened.Dispatch(JudgeSignal.DoorCommand("corridor.door.11", false, ActionSource.Player));
            reopened.Dispatch(JudgeSignal.DoorCommand("corridor.door.11", true, ActionSource.Player));
            reopened.Dispatch(JudgeSignal.DoorCommand("corridor.door.11", false, ActionSource.Player));
            reopened.EndNight();
            Assert.AreEqual(CardState.Violated, reopened.Watchers[0].State);
            Assert.AreEqual(12, axes2.GetValue(FearAxis.Layout));
        }

        [Test]
        public void 연출이_연문과_대상밖문은_의무가_아니다()
        {
            RuleSO card = NightLong(new SignalCondition(SignalKind.NightEndAccepted), new DoorObligationCondition(string.Empty));
            RuleBook book = _kit.Book(card);
            book.BeginNight();

            book.Dispatch(JudgeSignal.DoorCommand("corridor.door.11", false, ActionSource.Direction));
            book.Dispatch(JudgeSignal.DoorCommand("science.door", false, ActionSource.Player));
            book.Dispatch(JudgeSignal.Target(SignalKind.DoorAutoOpenObserved, "corridor.door.13"));
            book.EndNight();

            Assert.AreEqual(CardState.Complied, book.Watchers[0].State);
        }

        [Test]
        public void 플레이어가_연문을_연출이_닫으면_의무가_지워진다()
        {
            RuleSO card = NightLong(new SignalCondition(SignalKind.NightEndAccepted), new DoorObligationCondition(string.Empty));
            RuleBook book = _kit.Book(card);
            book.BeginNight();

            book.Dispatch(JudgeSignal.DoorCommand("corridor.door.11", false, ActionSource.Player));
            book.Dispatch(JudgeSignal.DoorCommand("corridor.door.11", true, ActionSource.Direction));
            book.Dispatch(JudgeSignal.DoorCommand("", false, ActionSource.Player));   // 대상 없는 명령은 무시
            book.EndNight();

            Assert.AreEqual(CardState.Complied, book.Watchers[0].State);
        }

        [Test]
        public void 밤시작카드에_자격구간이나_취소조건이_있으면_검사에걸린다()
        {
            RuleSO card = _kit.Card(c =>
            {
                c.CardId = "C6";
                c.Space = SpaceId.Classroom_1_1;
                c.IsLongTerm = true;
                c.TriggerKind = SignalKind.NightBegan;
                c.UseEligibleBand = true;
                c.Success = new SignalCondition(SignalKind.NightEndAccepted);
                c.Failure = new DoorObligationCondition(string.Empty);
                c.Cancel = new SignalCondition(SignalKind.SpaceExited);
            });

            List<string> errors = new List<string>();
            card.Validate(errors);
            Assert.AreEqual(2, errors.Count, string.Join("\n", errors));
        }

        [Test]
        public void 순서조건_가드전에_사건이면_성립_가드뒤면_불성립()
        {
            ICondition before = new BeforeCondition(
                new SignalCondition(SignalKind.SpaceExited, "", SpaceId.Toilet),
                new SignalCondition(SignalKind.DoorCloseCompleted, "toilet.stall.inner"));

            RuleSO card = _kit.Card(c =>
            {
                c.CardId = "T3";
                c.Space = SpaceId.Toilet;
                c.TriggerKind = SignalKind.ModelObserved;
                c.TriggerId = "scene.ta";
                c.Failure = before;
                c.Success = new SignalCondition(SignalKind.SpaceExited, "", SpaceId.Toilet);
                c.FailureDelta = 15;
                c.Radius = 0f;
            });

            RuleBook early = _kit.Book(card);
            early.Dispatch(JudgeSignal.Target(SignalKind.ModelObserved, "scene.ta"));
            early.Dispatch(JudgeSignal.OfSpace(SignalKind.SpaceExited, SpaceId.Toilet));
            Assert.AreEqual(CardState.Violated, early.Watchers[0].State);

            FearAxisSystem axes2 = new FearAxisSystem();
            RuleBook late = new RuleBook(new[] { card }, axes2, null, null);
            late.Dispatch(JudgeSignal.Target(SignalKind.ModelObserved, "scene.ta"));
            late.Dispatch(new JudgeSignal(SignalKind.DoorCloseCompleted, SpaceId.None, "toilet.stall.inner", ActionSource.Player, true, 0f));
            late.Dispatch(JudgeSignal.OfSpace(SignalKind.SpaceExited, SpaceId.Toilet));
            Assert.AreEqual(CardState.Complied, late.Watchers[0].State);
        }

        [Test]
        public void 순서조건_두교실점검전_밤종료는_위반()
        {
            ICondition failure = new BeforeCondition(
                new SignalCondition(SignalKind.NightEndAccepted),
                new AllOfCondition(
                    new SignalCondition(SignalKind.InspectionCompleted, "", SpaceId.Classroom_1_1),
                    new SignalCondition(SignalKind.InspectionCompleted, "", SpaceId.Classroom_1_3)));
            RuleSO card = NightLong(new SignalCondition(SignalKind.NightEndAccepted), failure);

            RuleBook one = _kit.Book(card);
            one.BeginNight();
            one.Dispatch(JudgeSignal.OfSpace(SignalKind.InspectionCompleted, SpaceId.Classroom_1_1));
            one.EndNight();
            Assert.AreEqual(CardState.Violated, one.Watchers[0].State);

            FearAxisSystem axes2 = new FearAxisSystem();
            RuleBook both = new RuleBook(new[] { card }, axes2, null, null);
            both.BeginNight();
            both.Dispatch(JudgeSignal.OfSpace(SignalKind.InspectionCompleted, SpaceId.Classroom_1_3));
            both.Dispatch(JudgeSignal.OfSpace(SignalKind.InspectionCompleted, SpaceId.Classroom_1_1));
            both.EndNight();
            Assert.AreEqual(CardState.Complied, both.Watchers[0].State);
            Assert.AreEqual(3, axes2.GetValue(FearAxis.Trust));
        }

        [Test]
        public void 밖에서_보낸_밤종료신호는_무시한다()
        {
            RuleSO card = NightLong(new SignalCondition(SignalKind.NightEndAccepted), new DoorObligationCondition(string.Empty));
            RuleBook book = _kit.Book(card);
            book.BeginNight();

            book.Dispatch(new JudgeSignal(SignalKind.NightEndAccepted, SpaceId.None, "", ActionSource.Player, false, 0f));

            Assert.AreEqual(CardState.Active, book.Watchers[0].State);
        }

        [Test]
        public void Tab이_열려있어도_밤종료정산은_된다()
        {
            RuleSO card = NightLong(new SignalCondition(SignalKind.NightEndAccepted), new DoorObligationCondition(string.Empty));
            RuleBook book = _kit.Book(card);
            book.BeginNight();
            book.Dispatch(JudgeSignal.Tab(true));

            book.EndNight();

            Assert.AreEqual(CardState.Complied, book.Watchers[0].State);
        }

        [Test]
        public void 준수전용카드는_데이터검사를_통과하고_위반없이_동작한다()
        {
            RuleSO card = _kit.Card(c =>
            {
                c.CardId = "S1";
                c.Space = SpaceId.ScienceRoom;
                c.TriggerKind = SignalKind.SpaceEntered;
                c.TargetIds = new[] { "science.model.sa.face" };
                c.Success = new GazeCondition(string.Empty, 1f, 0f);
                c.Failure = null;
                c.FailureDelta = 0;
                c.SuccessDelta = 3;
                c.Radius = 0f;
            });

            List<string> errors = new List<string>();
            card.Validate(errors);
            CollectionAssert.IsEmpty(errors);

            RuleBook book = _kit.Book(card);
            book.Dispatch(JudgeSignal.OfSpace(SignalKind.SpaceEntered, SpaceId.ScienceRoom));
            TestKit.Advance(book, 1f, "science.model.sa.face");

            Assert.AreEqual(CardState.Complied, book.Watchers[0].State);
            Assert.AreEqual(3, _kit.Axes.GetValue(FearAxis.Trust));
        }

        [Test]
        public void 위반조건없이_위반델타만_있으면_검사에걸린다()
        {
            RuleSO card = _kit.Card(c =>
            {
                c.CardId = "S1";
                c.Space = SpaceId.ScienceRoom;
                c.TriggerKind = SignalKind.SpaceEntered;
                c.Success = new ElapsedCondition(1f);
                c.Failure = null;
                c.FailureDelta = 12;
            });

            List<string> errors = new List<string>();
            card.Validate(errors);
            Assert.AreEqual(1, errors.Count, string.Join("\n", errors));
        }

        [Test]
        public void 밤시작카드가_단기면_검사에걸린다()
        {
            RuleSO card = _kit.Card(c =>
            {
                c.CardId = "C6";
                c.Space = SpaceId.Classroom_1_1;
                c.IsLongTerm = false;
                c.TriggerKind = SignalKind.NightBegan;
                c.Success = new SignalCondition(SignalKind.NightEndAccepted);
                c.Failure = new DoorObligationCondition(string.Empty);
            });

            List<string> errors = new List<string>();
            card.Validate(errors);
            Assert.AreEqual(1, errors.Count, string.Join("\n", errors));
        }

        [Test]
        public void NightRun은_밤시작때_장기카드를_시작한다()
        {
            RuleSO card = NightLong(new SignalCondition(SignalKind.NightEndAccepted), new DoorObligationCondition(string.Empty));
            NightRun.StartNewRun();
            NightRun.RegisteredTargets = null;
            NightRun.DeckOverride = day => new List<RuleSO> { card };
            try
            {
                NightRun.BeginNight(1, null);
                Assert.AreEqual(CardState.Active, NightRun.CurrentBook.Watchers[0].State);

                NightRun.Send(JudgeSignal.DoorCommand("corridor.door.13", false, ActionSource.Player));
                Assert.IsTrue(NightRun.RequestEndNight());
                Assert.AreEqual(12, NightRun.Axes.GetValue(FearAxis.Layout));
                Assert.AreEqual(1, NightRun.LastSummary.ViolationMinutes.Count);
            }
            finally
            {
                NightRun.DeckOverride = null;
                NightRun.StartNewRun();
            }
        }

        [Test]
        public void 정산순서_배치88에서_밤종료위반이100이면_뒤카드는정산하지않는다()
        {
            _kit.Axes.Apply(FearAxis.Layout, 88, "setup", SpaceId.None);
            RuleSO first = NightLong(new SignalCondition(SignalKind.NightEndAccepted), new DoorObligationCondition(string.Empty));
            RuleSO second = NightLong(new SignalCondition(SignalKind.NightEndAccepted), new DoorObligationCondition(string.Empty));
            RuleBook book = _kit.Book(first, second);
            book.BeginNight();

            book.Dispatch(JudgeSignal.DoorCommand("corridor.door.11", false, ActionSource.Player));
            book.EndNight();

            Assert.AreEqual(100, _kit.Axes.GetValue(FearAxis.Layout));
            Assert.IsTrue(_kit.Axes.IsLocked);
            Assert.AreEqual(1, book.Results.Count);
            Assert.AreEqual(CardState.Violated, book.Watchers[0].State);
            Assert.AreEqual(CardState.Locked, book.Watchers[1].State);
        }
    }
}

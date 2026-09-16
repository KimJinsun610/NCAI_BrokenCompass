using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace NightDuty.Tests
{
    /// <summary>
    /// 복도 카드 형태(H1~H4)와 기획서 공통 명세 6절 교차 검증 중 판정 코어로 재현 가능한 항목.
    /// 카드 구성은 판정 구조를 확인하기 위한 예시이며, 실제 카드 데이터는 기획서에서 RuleSO 에셋으로 옮긴다.
    /// </summary>
    public sealed class RuleBookTests
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

        // ─── 카드 예시 ──────────────────────────────────────────────

        private RuleSO H1()
        {
            return _kit.Card(c =>
            {
                c.CardId = "H1";
                c.Space = SpaceId.Corridor;
                c.TriggerKind = SignalKind.DoorAutoOpenObserved;
                c.TargetIds = new[] { "H1.door" };
                c.Failure = new SignalCondition(SignalKind.DoorCommandAccepted, "", SpaceId.None, FlagFilter.True);
                c.Success = new SignalCondition(SignalKind.PassageCompleted, "H1.passage");
                c.SuccessDelta = 2;
                c.FailureAxis = FearAxis.Layout;
                c.FailureDelta = 12;
            });
        }

        private RuleSO H2()
        {
            return _kit.Card(c =>
            {
                c.CardId = "H2";
                c.Space = SpaceId.Corridor;
                c.UseEligibleBand = true;
                c.EligibleAxis = FearAxis.Auditory;
                c.EligibleFrom = Band.Band2;
                c.EligibleTo = Band.Band4;
                c.TriggerKind = SignalKind.ClueDelivered;
                c.TargetIds = new[] { "H2.door" };
                c.Failure = new GazeCondition("");
                c.Success = new SignalCondition(SignalKind.PassageCompleted, "H2.passage");
                c.GraceSeconds = 2f;
                c.GazeSeconds = 3f;
                c.FailureAxis = FearAxis.Auditory;
                c.FailureDelta = 15;
            });
        }

        private RuleSO H3()
        {
            return _kit.Card(c =>
            {
                c.CardId = "H3";
                c.Space = SpaceId.Corridor;
                c.TriggerKind = SignalKind.SpaceEntered;
                c.Failure = new FlashlightCondition(true);
                c.Success = new SignalCondition(SignalKind.PassageCompleted, "H3.passage");
                c.Cancel = new SignalCondition(SignalKind.SpaceExited, "", SpaceId.Corridor);
                c.GraceSeconds = 2f;
                c.FailureAxis = FearAxis.Illuminance;
                c.FailureDelta = 12;
            });
        }

        private RuleSO H4()
        {
            return _kit.Card(c =>
            {
                c.CardId = "H4";
                c.Space = SpaceId.Corridor;
                c.IsLongTerm = true;
                c.UseEligibleBand = true;
                c.EligibleAxis = FearAxis.Layout;
                c.EligibleFrom = Band.Band1;
                c.EligibleTo = Band.Band4;
                c.TriggerKind = SignalKind.ClueIdentified;
                c.TargetIds = new[] { "H4.box" };
                c.Failure = new ProximityCondition("");
                c.Success = new SignalCondition(SignalKind.PassageCompleted, "H4.passage");
                c.SettleAt = SettleAt.AtNightEnd;
                c.Radius = 1.5f;
                c.FailureAxis = FearAxis.Layout;
                c.FailureDelta = 12;
            });
        }

        private RuleSO TimedExit(string id, float limitSeconds)
        {
            return _kit.Card(c =>
            {
                c.CardId = id;
                c.Space = SpaceId.Toilet;
                c.TriggerKind = SignalKind.ClueDelivered;
                c.TargetIds = new[] { id + ".flush" };
                c.Failure = new ElapsedCondition(limitSeconds);
                c.Success = new SignalCondition(SignalKind.SpaceExited, "", SpaceId.Toilet);
                c.FailureAxis = FearAxis.Auditory;
                c.FailureDelta = 12;
            });
        }

        private static JudgeSignal T(SignalKind kind, string id)
        {
            return JudgeSignal.Target(kind, id);
        }

        // ─── H1: 자동 개방 관찰 ─────────────────────────────────────

        [Test]
        public void H1_플레이어닫기명령은_수락즉시_배치12()
        {
            RuleBook book = _kit.Book(H1());
            book.Dispatch(T(SignalKind.DoorAutoOpenObserved, "H1.door"));
            book.Dispatch(JudgeSignal.DoorCommand("H1.door", true, ActionSource.Player));

            Assert.AreEqual(12, _kit.Axes.GetValue(FearAxis.Layout));
            Assert.AreEqual(CardState.Violated, book.Watchers[0].State);
        }

        [Test]
        public void H1_연출이닫은문과_열기명령은_위반이아니다()
        {
            RuleBook book = _kit.Book(H1());
            book.Dispatch(T(SignalKind.DoorAutoOpenObserved, "H1.door"));
            book.Dispatch(JudgeSignal.DoorCommand("H1.door", true, ActionSource.Direction));
            book.Dispatch(JudgeSignal.DoorCommand("H1.door", false, ActionSource.Player));
            book.Dispatch(T(SignalKind.PassageCompleted, "H1.passage"));

            Assert.AreEqual(0, _kit.Axes.GetValue(FearAxis.Layout));
            Assert.AreEqual(2, _kit.Axes.GetValue(FearAxis.Trust));
        }

        [Test]
        public void H1_소리만으로는_시작하지않는다_미배정미단서()
        {
            RuleBook book = _kit.Book(H1());
            book.Dispatch(T(SignalKind.ClueDelivered, "H1.door"));
            book.Dispatch(JudgeSignal.DoorCommand("H1.door", true, ActionSource.Player));
            book.Dispatch(T(SignalKind.PassageCompleted, "H1.passage"));
            book.EndNight();

            Assert.AreEqual(0, _kit.Axes.GetValue(FearAxis.Layout));
            Assert.AreEqual(0, _kit.Axes.GetValue(FearAxis.Trust));
            Assert.AreEqual(CardState.Undetermined, book.Watchers[0].State);
        }

        // ─── H2: 유예 2초 + 연속 응시 3초 ──────────────────────────

        private RuleBook StartH2()
        {
            _kit.Axes.Apply(FearAxis.Auditory, 50, "setup", SpaceId.None);
            RuleBook book = _kit.Book(H2());
            book.Dispatch(T(SignalKind.ClueDelivered, "H2.door"));
            Assert.AreEqual(CardState.Active, book.Watchers[0].State);
            return book;
        }

        [Test]
        public void H2_유예뒤_2초9응시후이탈은_준수()
        {
            RuleBook book = StartH2();
            TestKit.Advance(book, 2f);
            TestKit.Advance(book, 2.9f, "H2.door");
            book.Dispatch(T(SignalKind.PassageCompleted, "H2.passage"));

            Assert.AreEqual(50, _kit.Axes.GetValue(FearAxis.Auditory));
            Assert.AreEqual(2, _kit.Axes.GetValue(FearAxis.Trust));
        }

        [Test]
        public void H2_유예뒤_3초0응시는_청각15()
        {
            RuleBook book = StartH2();
            TestKit.Advance(book, 2f);
            TestKit.Advance(book, 3f, "H2.door");

            Assert.AreEqual(65, _kit.Axes.GetValue(FearAxis.Auditory));
            Assert.AreEqual(CardState.Violated, book.Watchers[0].State);
        }

        [Test]
        public void H2_2초씩_두번끊어보기는_위반없음()
        {
            RuleBook book = StartH2();
            TestKit.Advance(book, 2f);
            TestKit.Advance(book, 2f, "H2.door");
            TestKit.Advance(book, 0.1f, "wall");
            TestKit.Advance(book, 2f, "H2.door");

            Assert.AreEqual(50, _kit.Axes.GetValue(FearAxis.Auditory));
            Assert.AreEqual(CardState.Active, book.Watchers[0].State);
        }

        [Test]
        public void H2_유예중응시는_쌓이지않는다()
        {
            RuleBook book = StartH2();
            TestKit.Advance(book, 2f, "H2.door");
            TestKit.Advance(book, 2.9f, "H2.door");

            Assert.AreEqual(50, _kit.Axes.GetValue(FearAxis.Auditory));
        }

        [Test]
        public void H2_청각49에서는_시작하지않는다()
        {
            _kit.Axes.Apply(FearAxis.Auditory, 49, "setup", SpaceId.None);
            RuleBook book = _kit.Book(H2());
            book.Dispatch(T(SignalKind.ClueDelivered, "H2.door"));

            Assert.AreEqual(CardState.Waiting, book.Watchers[0].State);
        }

        [Test]
        public void 준수후재진입_같은문3초응시는_추가델타없음()
        {
            RuleBook book = StartH2();
            book.Dispatch(T(SignalKind.PassageCompleted, "H2.passage"));
            book.Dispatch(T(SignalKind.ClueDelivered, "H2.door"));
            TestKit.Advance(book, 6f, "H2.door");

            Assert.AreEqual(50, _kit.Axes.GetValue(FearAxis.Auditory));
            Assert.AreEqual(2, _kit.Axes.GetValue(FearAxis.Trust));
            Assert.AreEqual(1, book.Results.Count);
        }

        // ─── H3: 손전등 유예 2초 ────────────────────────────────────

        [Test]
        public void H3_유예안에끄고통과하면_준수()
        {
            RuleBook book = _kit.Book(H3());
            book.Dispatch(JudgeSignal.Flashlight(true));
            book.Dispatch(JudgeSignal.OfSpace(SignalKind.SpaceEntered, SpaceId.Corridor));
            TestKit.Advance(book, 1f);
            book.Dispatch(JudgeSignal.Flashlight(false));
            TestKit.Advance(book, 3f);
            book.Dispatch(T(SignalKind.PassageCompleted, "H3.passage"));

            Assert.AreEqual(0, _kit.Axes.GetValue(FearAxis.Illuminance));
            Assert.AreEqual(2, _kit.Axes.GetValue(FearAxis.Trust));
        }

        [Test]
        public void H3_유예뒤On이면_조도12_한번만()
        {
            RuleBook book = _kit.Book(H3());
            book.Dispatch(JudgeSignal.Flashlight(true));
            book.Dispatch(JudgeSignal.OfSpace(SignalKind.SpaceEntered, SpaceId.Corridor));
            TestKit.Advance(book, 5f);
            book.Dispatch(JudgeSignal.Flashlight(false));
            book.Dispatch(JudgeSignal.Flashlight(true));

            Assert.AreEqual(12, _kit.Axes.GetValue(FearAxis.Illuminance));
        }

        [Test]
        public void H3_유예안에되돌아가면_대기로복귀_다음진입에다시시작()
        {
            RuleBook book = _kit.Book(H3());
            book.Dispatch(JudgeSignal.OfSpace(SignalKind.SpaceEntered, SpaceId.Corridor));
            TestKit.Advance(book, 1f);
            book.Dispatch(JudgeSignal.OfSpace(SignalKind.SpaceExited, SpaceId.Corridor));

            Assert.AreEqual(CardState.Waiting, book.Watchers[0].State);

            book.Dispatch(JudgeSignal.OfSpace(SignalKind.SpaceEntered, SpaceId.Corridor));
            Assert.AreEqual(CardState.Active, book.Watchers[0].State);
        }

        // ─── H4: 밤 종료까지 1.5m 금지 ──────────────────────────────

        private RuleBook StartH4()
        {
            _kit.Axes.Apply(FearAxis.Layout, 25, "setup", SpaceId.None);
            RuleBook book = _kit.Book(H4());
            book.Dispatch(T(SignalKind.ClueIdentified, "H4.box"));
            Assert.AreEqual(CardState.Active, book.Watchers[0].State);
            return book;
        }

        [Test]
        public void 금지반경_1m50은_바깥_밤종료에신뢰2()
        {
            RuleBook book = StartH4();
            book.Dispatch(T(SignalKind.PassageCompleted, "H4.passage"));
            book.Dispatch(JudgeSignal.Proximity("H4.box", 1.50f));

            Assert.AreEqual(0, _kit.Axes.GetValue(FearAxis.Trust), "통행 직후에는 준수를 지급하지 않는다");

            book.EndNight();

            Assert.AreEqual(25, _kit.Axes.GetValue(FearAxis.Layout));
            Assert.AreEqual(2, _kit.Axes.GetValue(FearAxis.Trust));
        }

        [Test]
        public void 금지반경_1m49는_배치12()
        {
            RuleBook book = StartH4();
            book.Dispatch(JudgeSignal.Proximity("H4.box", 1.49f));

            Assert.AreEqual(37, _kit.Axes.GetValue(FearAxis.Layout));
        }

        [Test]
        public void H4_통행후재방문진입은_신뢰선지급없이_배치12()
        {
            RuleBook book = StartH4();
            book.Dispatch(T(SignalKind.PassageCompleted, "H4.passage"));
            book.Dispatch(JudgeSignal.Proximity("H4.box", 1.4f));
            book.EndNight();

            Assert.AreEqual(37, _kit.Axes.GetValue(FearAxis.Layout));
            Assert.AreEqual(0, _kit.Axes.GetValue(FearAxis.Trust));
        }

        [Test]
        public void H4_통행없이밤종료는_미판정()
        {
            RuleBook book = StartH4();
            book.EndNight();

            Assert.AreEqual(CardState.Undetermined, book.Watchers[0].State);
            Assert.AreEqual(0, _kit.Axes.GetValue(FearAxis.Trust));
        }

        // ─── Tab과 제한시간 ────────────────────────────────────────

        [Test]
        public void Tab중에는_타이머가멈춘다_남은유효시간7초()
        {
            RuleBook book = _kit.Book(TimedExit("T2", 12f));
            book.Dispatch(T(SignalKind.ClueDelivered, "T2.flush"));
            TestKit.Advance(book, 5f);

            book.Dispatch(JudgeSignal.Tab(true));
            TestKit.Advance(book, 10f);
            book.Dispatch(JudgeSignal.OfSpace(SignalKind.SpaceExited, SpaceId.Toilet));
            book.Dispatch(JudgeSignal.Tab(false));

            Assert.AreEqual(CardState.Active, book.Watchers[0].State, "Tab 중 이동으로 회피할 수 없다");

            TestKit.Advance(book, 6.9f);
            Assert.AreEqual(CardState.Active, book.Watchers[0].State);

            TestKit.Advance(book, 0.1f);
            Assert.AreEqual(CardState.Violated, book.Watchers[0].State);
        }

        [Test]
        public void 제한시간안에퇴실하면_준수()
        {
            RuleBook book = _kit.Book(TimedExit("T2", 12f));
            book.Dispatch(T(SignalKind.ClueDelivered, "T2.flush"));
            TestKit.Advance(book, 11.9f);
            book.Dispatch(JudgeSignal.OfSpace(SignalKind.SpaceExited, SpaceId.Toilet));

            Assert.AreEqual(CardState.Complied, book.Watchers[0].State);
        }

        // ─── 공통 규약 ─────────────────────────────────────────────

        [Test]
        public void 같은신호에서_준수와위반이함께성립하면_위반우선()
        {
            RuleSO card = _kit.Card(c =>
            {
                c.CardId = "C2";
                c.Space = SpaceId.Classroom_1_1;
                c.TriggerKind = SignalKind.ClueDelivered;
                c.TriggerId = "clue";
                c.Success = new SignalCondition(SignalKind.ZoneEntered, "zone");
                c.Failure = new SignalCondition(SignalKind.ZoneEntered, "zone");
                c.FailureAxis = FearAxis.Layout;
            });

            RuleBook book = _kit.Book(card);
            book.Dispatch(T(SignalKind.ClueDelivered, "clue"));
            book.Dispatch(T(SignalKind.ZoneEntered, "zone"));

            Assert.AreEqual(CardState.Violated, book.Watchers[0].State);
            Assert.AreEqual(0, _kit.Axes.GetValue(FearAxis.Trust));
        }

        [Test]
        public void 대상참조누락은_크래시없이_미판정()
        {
            RuleSO card = H1();
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("대상 참조 누락"));
            RuleBook book = new RuleBook(new[] { card }, _kit.Axes, null, new HashSet<string> { "H1.passage" });

            book.Dispatch(T(SignalKind.DoorAutoOpenObserved, "H1.door"));
            book.Dispatch(JudgeSignal.DoorCommand("H1.door", true, ActionSource.Player));

            Assert.AreEqual(CardState.Undetermined, book.Watchers[0].State);
            Assert.AreEqual(0, _kit.Axes.GetValue(FearAxis.Layout));
            Assert.AreEqual(1, book.Results.Count, "미판정도 결과 기록에 남는다");
            Assert.AreEqual(0, book.Results[0].Delta);
        }

        [Test]
        public void 한방문에_신규단기사건은_하나_덱순서우선()
        {
            RuleSO first = _kit.Card(c =>
            {
                c.CardId = "H5";
                c.Space = SpaceId.Corridor;
                c.TriggerKind = SignalKind.ClueIdentified;
                c.TriggerId = "shared";
                c.Success = new SignalCondition(SignalKind.PassageCompleted, "p");
                c.Failure = new SignalCondition(SignalKind.ZoneEntered, "z");
            });
            RuleSO second = H1();

            RuleBook book = _kit.Book(first, second);
            book.Dispatch(JudgeSignal.OfSpace(SignalKind.SpaceEntered, SpaceId.Corridor));
            book.Dispatch(T(SignalKind.ClueIdentified, "shared"));
            book.Dispatch(T(SignalKind.DoorAutoOpenObserved, "H1.door"));

            Assert.AreEqual(CardState.Active, book.Watchers[0].State);
            Assert.AreEqual(CardState.Waiting, book.Watchers[1].State);
        }

        [Test]
        public void 정산순서_신뢰99에서_첫준수가100이면_뒤결과는미적용()
        {
            _kit.Axes.Apply(FearAxis.Trust, 99, "setup", SpaceId.None);
            _kit.Axes.Apply(FearAxis.Layout, 25, "setup", SpaceId.None);

            RuleSO a = H4();
            RuleSO b = _kit.Card(c =>
            {
                c.CardId = "T6";
                c.Space = SpaceId.Toilet;
                c.IsLongTerm = true;
                c.TriggerKind = SignalKind.ClueIdentified;
                c.TriggerId = "T6.stalls";
                c.Success = new SignalCondition(SignalKind.InspectionCompleted, "", SpaceId.Toilet);
                c.Failure = new SignalCondition(SignalKind.ZoneEntered, "T6.inside");
                c.SettleAt = SettleAt.AtNightEnd;
                c.FailureAxis = FearAxis.Layout;
                c.FailureDelta = 15;
            });

            RuleBook book = _kit.Book(a, b);
            book.Dispatch(T(SignalKind.ClueIdentified, "H4.box"));
            book.Dispatch(T(SignalKind.PassageCompleted, "H4.passage"));
            book.Dispatch(T(SignalKind.ClueIdentified, "T6.stalls"));
            book.Dispatch(JudgeSignal.OfSpace(SignalKind.InspectionCompleted, SpaceId.Toilet));
            book.EndNight();

            Assert.AreEqual(100, _kit.Axes.GetValue(FearAxis.Trust));
            Assert.IsTrue(_kit.Axes.IsLocked);
            Assert.AreEqual("H4", _kit.Axes.Cause.SourceId);
            Assert.AreEqual(1, book.Results.Count);
            Assert.AreEqual(CardState.Locked, book.Watchers[1].State);
        }

        [Test]
        public void 배치90에서_25위반은_100_종료후_신호무시()
        {
            _kit.Axes.Apply(FearAxis.Layout, 90, "setup", SpaceId.None);
            RuleSO h6 = _kit.Card(c =>
            {
                c.CardId = "H6";
                c.Space = SpaceId.Corridor;
                c.TriggerKind = SignalKind.ClueIdentified;
                c.TriggerId = "H6.tree";
                c.TargetIds = new[] { "H6.tree" };
                c.Failure = new ProximityCondition("", 2f);
                c.Success = new SignalCondition(SignalKind.PassageCompleted, "H6.passage");
                c.SuccessDelta = 4;
                c.FailureDelta = 25;
            });
            RuleSO h1 = H1();

            RuleBook book = _kit.Book(h6, h1);
            book.Dispatch(T(SignalKind.ClueIdentified, "H6.tree"));
            book.Dispatch(JudgeSignal.Proximity("H6.tree", 1.9f));

            Assert.AreEqual(100, _kit.Axes.GetValue(FearAxis.Layout));
            Assert.AreEqual(CardState.Locked, book.Watchers[1].State);

            book.Dispatch(T(SignalKind.DoorAutoOpenObserved, "H1.door"));
            book.EndNight();

            Assert.AreEqual(CardState.Locked, book.Watchers[1].State);
            Assert.AreEqual(1, book.Results.Count);
        }
    }
}

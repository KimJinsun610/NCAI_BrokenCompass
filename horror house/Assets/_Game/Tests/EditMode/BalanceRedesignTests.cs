using System.Collections.Generic;
using NUnit.Framework;

namespace NightDuty.Tests
{
    /// <summary>
    /// 일차 하한 곡선(2026-09-21 밸런스 재설계). 곡선 자체와, 「올리기만 하고 내리지 않는다」는 규약을 본다.
    /// </summary>
    public sealed class DayFloorTests
    {
        [TearDown]
        public void TearDown()
        {
            EventBus.ClearAll();
        }

        /// <summary>곡선은 0/0/12/24/48/72다. 값을 바꾸려면 DayFloor.Floors 배열 한 곳만 고쳐야 한다.</summary>
        [TestCase(0, 0)]
        [TestCase(1, 0)]
        [TestCase(2, 12)]
        [TestCase(3, 24)]
        [TestCase(4, 48)]
        [TestCase(5, 72)]
        public void 일차별_하한값은_0_0_12_24_48_72다(int day, int expected)
        {
            Assert.AreEqual(expected, DayFloor.Of(day));
        }

        [Test]
        public void 일차1은_아무_축도_올리지_않는다()
        {
            FearAxisSystem axes = new FearAxisSystem();

            int raised = DayFloor.Apply(axes, 1);

            Assert.AreEqual(0, raised, "1일차 하한은 0이라 올릴 축이 없다");
            Assert.AreEqual(0, axes.GetValue(FearAxis.Layout));
            Assert.AreEqual(0, axes.GetValue(FearAxis.Auditory));
            Assert.AreEqual(0, axes.GetValue(FearAxis.Illuminance));
            Assert.AreEqual(0, axes.GetValue(FearAxis.Trust));
        }

        [Test]
        public void 일차3은_감각_3축을_전부_24로_올린다()
        {
            FearAxisSystem axes = new FearAxisSystem();

            int raised = DayFloor.Apply(axes, 3);

            Assert.AreEqual(3, raised, "감각 3축이 전부 올라간다");
            Assert.AreEqual(DayFloor.Of(3), axes.GetValue(FearAxis.Layout));
            Assert.AreEqual(DayFloor.Of(3), axes.GetValue(FearAxis.Auditory));
            Assert.AreEqual(DayFloor.Of(3), axes.GetValue(FearAxis.Illuminance));
        }

        [Test]
        public void 신뢰는_하한_대상이_아니다()
        {
            FearAxisSystem axes = new FearAxisSystem();

            DayFloor.Apply(axes, 3);

            // 신뢰는 준수로만 오른다. 하한이 공짜로 신뢰를 올리면 역설 문자 배급량이 저절로 늘어난다.
            Assert.AreEqual(0, axes.GetValue(FearAxis.Trust));
        }

        [Test]
        public void 이미_하한보다_높은_축은_내려가지_않는다()
        {
            FearAxisSystem axes = new FearAxisSystem();
            axes.Apply(FearAxis.Layout, 50, "준비", SpaceId.None);

            int raised = DayFloor.Apply(axes, 3);

            Assert.AreEqual(50, axes.GetValue(FearAxis.Layout), "하한 24는 50을 끌어내리지 않는다");
            Assert.AreEqual(2, raised, "이미 높은 배치를 뺀 두 축만 올라간다");
            Assert.AreEqual(DayFloor.Of(3), axes.GetValue(FearAxis.Auditory));
            Assert.AreEqual(DayFloor.Of(3), axes.GetValue(FearAxis.Illuminance));
        }

        [Test]
        public void 같은_날_두번_적용해도_값이_같다()
        {
            FearAxisSystem axes = new FearAxisSystem();

            DayFloor.Apply(axes, 4);
            int again = DayFloor.Apply(axes, 4);

            Assert.AreEqual(0, again, "두 번째 적용은 델타가 0이라 아무 축도 올리지 않는다");
            Assert.AreEqual(DayFloor.Of(4), axes.GetValue(FearAxis.Layout));
            Assert.AreEqual(DayFloor.Of(4), axes.GetValue(FearAxis.Auditory));
            Assert.AreEqual(DayFloor.Of(4), axes.GetValue(FearAxis.Illuminance));
        }

        [Test]
        public void 마지막_일차를_넘는_일차는_마지막_하한으로_클램프한다()
        {
            FearAxisSystem axes = new FearAxisSystem();

            Assert.AreEqual(DayFloor.Of(DayFloor.LastDay), DayFloor.Of(DayFloor.LastDay + 2));
            Assert.AreEqual(72, DayFloor.Of(7));

            DayFloor.Apply(axes, 7);
            Assert.AreEqual(72, axes.GetValue(FearAxis.Illuminance));
        }
    }

    /// <summary>
    /// 하루 6장 배정(재설계안 7절 「모델 A」). 시드를 고정해 결정적으로 본다.
    /// 풀은 실제 24장과 같은 모양 — 축마다 6장씩에 자격 미달 카드를 섞어 둔다.
    /// </summary>
    public sealed class DayDirectorTests
    {
        /// <summary>고정 시드. 이 값이 바뀌면 아래 테스트의 조합도 바뀐다.</summary>
        private const int Seed = 20260921;

        private TestKit _kit;
        private List<RuleSO> _pool;
        private DayDirector _director;
        private FearAxisSystem _axes;

        [SetUp]
        public void SetUp()
        {
            _kit = new TestKit();
            _pool = BuildPool();
            _axes = new FearAxisSystem();
            _director = new DayDirector(_pool, new System.Random(Seed));
        }

        [TearDown]
        public void TearDown()
        {
            _director = null;
            _pool = null;
            _kit.Dispose();
        }

        /// <summary>자격 제한이 없는 보통 카드.</summary>
        private RuleSO Plain(string id, SpaceId space, FearAxis axis)
        {
            return _kit.Card(c =>
            {
                c.CardId = id;
                c.Space = space;
                c.FailureAxis = axis;
                c.TriggerKind = SignalKind.DoorAutoOpenObserved;
                c.TargetIds = new[] { id.ToLowerInvariant() + ".door" };
                c.Failure = new SignalCondition(SignalKind.DoorCommandAccepted, TargetMatchIds.Trigger, SpaceId.None, FlagFilter.True);
                c.Success = new SignalCondition(SignalKind.PassageCompleted, id.ToLowerInvariant() + ".passage");
            });
        }

        /// <summary>Band3 이상에서만 열리는 카드. 축이 0인 동안에는 후보에도 못 들어간다.</summary>
        private RuleSO Gated(string id, SpaceId space, FearAxis axis)
        {
            return _kit.Card(c =>
            {
                c.CardId = id;
                c.Space = space;
                c.FailureAxis = axis;
                c.UseEligibleBand = true;
                c.EligibleAxis = axis;
                c.EligibleFrom = Band.Band3;
                c.EligibleTo = Band.Band4;
                c.TriggerKind = SignalKind.DoorAutoOpenObserved;
                c.TargetIds = new[] { id.ToLowerInvariant() + ".door" };
                c.Failure = new SignalCondition(SignalKind.DoorCommandAccepted, TargetMatchIds.Trigger, SpaceId.None, FlagFilter.True);
                c.Success = new SignalCondition(SignalKind.PassageCompleted, id.ToLowerInvariant() + ".passage");
            });
        }

        /// <summary>
        /// 축마다 6장 + 1일차 고정 S1 + 자격 미달 3장 = 22장.
        /// 축별 6장은 의도적이다 — 5장이면 회차 재등장 한도(배치·청각 2회)에 5일차가 딱 맞아떨어져
        /// 뽑기 운에 따라 쿼터 미달이 날 수 있고, 그러면 테스트가 간헐적으로 흔들린다.
        /// </summary>
        private List<RuleSO> BuildPool()
        {
            List<RuleSO> pool = new List<RuleSO>();

            // 배치 6장
            pool.Add(Plain("H1", SpaceId.Corridor, FearAxis.Layout));
            pool.Add(Plain("H2", SpaceId.Corridor, FearAxis.Layout));
            pool.Add(Plain("C1", SpaceId.Classroom_1_1, FearAxis.Layout));
            pool.Add(Plain("C2", SpaceId.Classroom_1_1, FearAxis.Layout));
            pool.Add(Plain("S5", SpaceId.ScienceRoom, FearAxis.Layout));
            pool.Add(Plain(DayDirector.ConflictCardA, SpaceId.Toilet, FearAxis.Layout));   // T1

            // 청각 6장
            pool.Add(Plain("H3", SpaceId.Corridor, FearAxis.Auditory));
            pool.Add(Plain("H4", SpaceId.Corridor, FearAxis.Auditory));
            pool.Add(Plain("C3", SpaceId.Classroom_1_1, FearAxis.Auditory));
            pool.Add(Plain("S2", SpaceId.ScienceRoom, FearAxis.Auditory));
            pool.Add(Plain("S3", SpaceId.ScienceRoom, FearAxis.Auditory));
            pool.Add(Plain("T6", SpaceId.Toilet, FearAxis.Auditory));

            // 조도 6장
            pool.Add(Plain("H5", SpaceId.Corridor, FearAxis.Illuminance));
            pool.Add(Plain("H6", SpaceId.Corridor, FearAxis.Illuminance));
            pool.Add(Plain("C4", SpaceId.Classroom_1_1, FearAxis.Illuminance));
            pool.Add(Plain("S4", SpaceId.ScienceRoom, FearAxis.Illuminance));
            pool.Add(Plain("T2", SpaceId.Toilet, FearAxis.Illuminance));
            pool.Add(Plain(DayDirector.ConflictCardB, SpaceId.Toilet, FearAxis.Illuminance));   // T3

            // 1일차 고정 카드. 준수 전용이라 위반축은 기본값(배치)으로 남는다.
            pool.Add(Plain(DayDirector.FirstDayFixedCardId, SpaceId.ScienceRoom, FearAxis.Layout));

            // 자격 미달 3장 — 축이 0인 동안에는 한 장도 덱에 들어오면 안 된다.
            pool.Add(Gated("T4", SpaceId.Toilet, FearAxis.Layout));
            pool.Add(Gated("T5", SpaceId.Toilet, FearAxis.Auditory));
            pool.Add(Gated("C5", SpaceId.Classroom_1_1, FearAxis.Illuminance));

            return pool;
        }

        private static int CountAxis(IReadOnlyList<RuleSO> deck, FearAxis axis)
        {
            int count = 0;
            for (int i = 0; i < deck.Count; i++)
            {
                if (deck[i] != null && deck[i].FailureAxis == axis)
                {
                    count++;
                }
            }

            return count;
        }

        private static bool Has(IReadOnlyList<RuleSO> deck, string cardId)
        {
            for (int i = 0; i < deck.Count; i++)
            {
                if (deck[i] != null && deck[i].CardId == cardId)
                {
                    return true;
                }
            }

            return false;
        }

        private static List<string> IdsOf(IReadOnlyList<RuleSO> deck)
        {
            List<string> ids = new List<string>(deck.Count);
            for (int i = 0; i < deck.Count; i++)
            {
                ids.Add(deck[i] == null ? string.Empty : deck[i].CardId);
            }

            return ids;
        }

        [Test]
        public void 덱은_여섯장이다()
        {
            IReadOnlyList<RuleSO> deck = _director.BuildDeck(2, _axes);

            Assert.AreEqual(DayDirector.DeckSize, deck.Count, _director.LastReport);
        }

        [Test]
        public void 축쿼터는_배치둘_청각둘_조도둘이다()
        {
            // 1일차는 S1(위반축 기본값 배치)이 한 자리를 가져가므로 2일차로 본다.
            IReadOnlyList<RuleSO> deck = _director.BuildDeck(2, _axes);

            Assert.AreEqual(DayDirector.QuotaLayout, CountAxis(deck, FearAxis.Layout), _director.LastReport);
            Assert.AreEqual(DayDirector.QuotaAuditory, CountAxis(deck, FearAxis.Auditory), _director.LastReport);
            Assert.AreEqual(DayDirector.QuotaIlluminance, CountAxis(deck, FearAxis.Illuminance), _director.LastReport);
        }

        [Test]
        public void 일차1은_S1고정에_조도한장이다()
        {
            IReadOnlyList<RuleSO> deck = _director.BuildDeck(1, _axes);

            Assert.AreEqual(DayDirector.DeckSize, deck.Count, _director.LastReport);
            Assert.AreEqual(DayDirector.FirstDayFixedCardId, deck[0].CardId, "고정 카드가 그날의 척추라 맨 앞에 온다");
            Assert.AreEqual(DayDirector.QuotaIlluminanceFirstDay, CountAxis(deck, FearAxis.Illuminance), _director.LastReport);

            // S1은 위반축이 기본값(배치)이라 배치 칸으로 세어진다. 쿼터로 뽑은 배치 2장 + S1 = 3장.
            Assert.AreEqual(DayDirector.QuotaLayout + 1, CountAxis(deck, FearAxis.Layout), _director.LastReport);
            Assert.AreEqual(DayDirector.QuotaAuditory, CountAxis(deck, FearAxis.Auditory), _director.LastReport);
        }

        [Test]
        public void S1은_2일차부터_나오지_않는다()
        {
            Assert.IsTrue(Has(_director.BuildDeck(1, _axes), DayDirector.FirstDayFixedCardId), "1일차에는 나온다");

            for (int day = 2; day <= DayFloor.LastDay; day++)
            {
                IReadOnlyList<RuleSO> deck = _director.BuildDeck(day, _axes);
                Assert.IsFalse(Has(deck, DayDirector.FirstDayFixedCardId),
                    day + "일차에 " + DayDirector.FirstDayFixedCardId + "가 들어왔다: " + _director.LastReport);
            }
        }

        [Test]
        public void T1과_T3는_같은날_함께_나오지_않는다()
        {
            for (int day = 1; day <= DayFloor.LastDay; day++)
            {
                IReadOnlyList<RuleSO> deck = _director.BuildDeck(day, _axes);
                bool hasA = Has(deck, DayDirector.ConflictCardA);
                bool hasB = Has(deck, DayDirector.ConflictCardB);

                Assert.IsFalse(hasA && hasB,
                    day + "일차에 " + DayDirector.ConflictCardA + "·" + DayDirector.ConflictCardB +
                    "가 같이 배정됐다: " + _director.LastReport);
            }
        }

        [Test]
        public void 자격미달_카드는_배정되지_않는다()
        {
            // 축이 전부 0이면 Band0이라, Band3부터 열리는 카드는 후보에도 못 들어간다.
            for (int day = 1; day <= DayFloor.LastDay; day++)
            {
                IReadOnlyList<RuleSO> deck = _director.BuildDeck(day, _axes);

                Assert.IsFalse(Has(deck, "T4"), day + "일차: " + _director.LastReport);
                Assert.IsFalse(Has(deck, "T5"), day + "일차: " + _director.LastReport);
                Assert.IsFalse(Has(deck, "C5"), day + "일차: " + _director.LastReport);
            }
        }

        [Test]
        public void 같은날_두번_부르면_같은_결과를_돌려준다()
        {
            List<string> first = IdsOf(_director.BuildDeck(2, _axes));
            List<string> second = IdsOf(_director.BuildDeck(2, _axes));

            // 이력이 두 번 오르면 회차 재등장 한도가 어긋난다. 그래서 다시 뽑지 않고 직전 결과를 그대로 준다.
            CollectionAssert.AreEqual(first, second);
        }

        [Test]
        public void Reset뒤에는_다시_1일차처럼_동작한다()
        {
            _director.BuildDeck(1, _axes);
            _director.BuildDeck(2, _axes);

            _director.Reset();

            IReadOnlyList<RuleSO> deck = _director.BuildDeck(1, _axes);
            Assert.AreEqual(DayDirector.DeckSize, deck.Count, _director.LastReport);
            Assert.AreEqual(DayDirector.FirstDayFixedCardId, deck[0].CardId, "회차를 새로 시작하면 고정 카드가 다시 온다");
        }
    }

    /// <summary>
    /// 미방문 벌점(2026-09-21 재설계). 「경비실에 숨어 버티기」가 최적해가 되지 않게 하는 한 줄이다.
    /// 일차 하한이 0인 1일차로만 본다 — 하한이 섞이면 델타를 읽기 어렵다.
    /// </summary>
    public sealed class UnvisitedPenaltyTests
    {
        private TestKit _kit;
        private int _clock;

        [SetUp]
        public void SetUp()
        {
            _kit = new TestKit();
            NightRun.StartNewRun();
            NightRun.RegisteredTargets = null;
            _clock = 0;
        }

        [TearDown]
        public void TearDown()
        {
            // 정적 상태를 되돌리지 않으면 다른 테스트 파일이 이 덱을 물려받는다.
            NightRun.DeckOverride = null;
            NightRun.StartNewRun();
            _kit.Dispose();
        }

        private RuleSO Card(string id, SpaceId space, FearAxis axis)
        {
            return _kit.Card(c =>
            {
                c.CardId = id;
                c.Space = space;
                c.FailureAxis = axis;
                c.TriggerKind = SignalKind.DoorAutoOpenObserved;
                c.TargetIds = new[] { id.ToLowerInvariant() + ".door" };
                c.Failure = new SignalCondition(SignalKind.DoorCommandAccepted, TargetMatchIds.Trigger, SpaceId.None, FlagFilter.True);
                c.Success = new SignalCondition(SignalKind.PassageCompleted, id.ToLowerInvariant() + ".passage");
            });
        }

        [Test]
        public void 공간에_한번도_안_들어가면_밤종료에_벌점을_문다()
        {
            RuleSO card = Card("T2", SpaceId.Toilet, FearAxis.Illuminance);
            NightRun.DeckOverride = day => new List<RuleSO> { card };

            NightRun.BeginNight(1, () => _clock);
            Assert.AreEqual(0, NightRun.Axes.GetValue(FearAxis.Illuminance), "1일차 하한은 0이다");

            Assert.IsTrue(NightRun.RequestEndNight());

            Assert.AreEqual(NightRun.UnvisitedPenalty, NightRun.Axes.GetValue(FearAxis.Illuminance));
        }

        [Test]
        public void 방문했으면_벌점을_물지_않는다()
        {
            RuleSO card = Card("T2", SpaceId.Toilet, FearAxis.Illuminance);
            NightRun.DeckOverride = day => new List<RuleSO> { card };

            NightRun.BeginNight(1, () => _clock);
            NightRun.Send(JudgeSignal.OfSpace(SignalKind.SpaceEntered, SpaceId.Toilet));
            Assert.IsTrue(NightRun.RequestEndNight());

            // 갔는데 단서가 안 난 것은 플레이어의 선택이 아니다 — 0 그대로다.
            Assert.AreEqual(0, NightRun.Axes.GetValue(FearAxis.Illuminance));
        }

        [Test]
        public void 한_공간에_카드가_둘이면_둘_다_문다()
        {
            RuleSO first = Card("T2", SpaceId.Toilet, FearAxis.Auditory);
            RuleSO second = Card("T6", SpaceId.Toilet, FearAxis.Auditory);
            NightRun.DeckOverride = day => new List<RuleSO> { first, second };

            NightRun.BeginNight(1, () => _clock);
            Assert.IsTrue(NightRun.RequestEndNight());

            // 안 간 대가는 공간 단위가 아니라 수칙 단위다.
            Assert.AreEqual(NightRun.UnvisitedPenalty * 2, NightRun.Axes.GetValue(FearAxis.Auditory));
        }

        [Test]
        public void 이미_위반으로_정산된_카드는_추가로_물지_않는다()
        {
            RuleSO card = Card("H1", SpaceId.Corridor, FearAxis.Layout);
            NightRun.DeckOverride = day => new List<RuleSO> { card };

            NightRun.BeginNight(1, () => _clock);

            // 공간 진입 신호 없이 문만 건드려 위반을 만든다 — 방문 기록은 비어 있지만 판정은 났다.
            NightRun.Send(JudgeSignal.Target(SignalKind.DoorAutoOpenObserved, "h1.door"));
            NightRun.Send(JudgeSignal.DoorCommand("h1.door", true, ActionSource.Player));
            Assert.AreEqual(12, NightRun.Axes.GetValue(FearAxis.Layout), "위반 기본 델타");
            Assert.IsFalse(NightRun.WasVisitedToday(SpaceId.Corridor), "문 신호는 방문이 아니다");

            Assert.IsTrue(NightRun.RequestEndNight());

            // 결과가 났다면 그 공간에 있었다는 뜻이라 미방문 벌점을 겹쳐 물리지 않는다.
            Assert.AreEqual(12, NightRun.Axes.GetValue(FearAxis.Layout));
        }
    }

    /// <summary>
    /// 결산 3구분(2026-09-21 재설계). <see cref="DutyLogEntry"/>는 순수 구조체라 직접 만들어 표시만 본다.
    /// </summary>
    public sealed class DutyLogMarkTests
    {
        private static DutyLogEntry Entry(CardState state, bool visited, bool instructed)
        {
            return new DutyLogEntry(1, "H1", SpaceId.Corridor, "테스트 본문", state, visited, instructed);
        }

        [Test]
        public void 지킨_줄은_표시가_없다()
        {
            DutyLogEntry entry = Entry(CardState.Complied, true, false);

            Assert.IsFalse(entry.Struck);
            Assert.AreEqual(DutyMark.None, entry.Mark);
        }

        [Test]
        public void 그냥_어긴_줄은_어김이다()
        {
            DutyLogEntry entry = Entry(CardState.Violated, true, false);

            Assert.IsTrue(entry.Struck);
            Assert.AreEqual(DutyMark.Struck, entry.Mark);
        }

        [Test]
        public void 역설문자를_받고_어긴_줄은_지시를따름이다()
        {
            DutyLogEntry entry = Entry(CardState.Violated, true, true);

            // 수치 손해는 「어김」과 똑같다. 다른 것은 종이가 부르는 이름뿐이다.
            Assert.IsTrue(entry.Struck);
            Assert.AreEqual(DutyMark.Instructed, entry.Mark);
        }

        [Test]
        public void 문자를_받았어도_그_공간에_안_갔으면_어김이다()
        {
            // 가지 않았으면 지시를 따른 것이 아니다.
            DutyLogEntry entry = Entry(CardState.Waiting, false, true);

            Assert.IsTrue(entry.Struck);
            Assert.AreEqual(DutyMark.Struck, entry.Mark);
        }

        [Test]
        public void 미방문은_판정이_없어도_어김이다()
        {
            DutyLogEntry entry = Entry(CardState.Undetermined, false, false);

            Assert.IsTrue(entry.Struck);
            Assert.AreEqual(DutyMark.Struck, entry.Mark);
        }

        [Test]
        public void Struck은_예전_뜻_그대로다()
        {
            // 하위호환: 역설 문자를 모르는 예전 생성자는 Instructed가 false로 들어간다.
            DutyLogEntry complied = new DutyLogEntry(1, "H1", SpaceId.Corridor, "본문", CardState.Complied, true);
            DutyLogEntry violated = new DutyLogEntry(2, "H2", SpaceId.Corridor, "본문", CardState.Violated, true);
            DutyLogEntry unvisited = new DutyLogEntry(3, "H3", SpaceId.Corridor, "본문", CardState.Waiting, false);

            Assert.IsFalse(complied.Struck);
            Assert.IsTrue(violated.Struck, "위반");
            Assert.IsTrue(unvisited.Struck, "가지 않은 공간");

            Assert.AreEqual(DutyMark.None, complied.Mark);
            Assert.AreEqual(DutyMark.Struck, violated.Mark);
            Assert.AreEqual(DutyMark.Struck, unvisited.Mark);
        }

        [Test]
        public void 미방문인데_위반까지_난_줄은_문자를_받았으면_지시를따름으로_적힌다()
        {
            // 현재 코드의 동작을 그대로 못박아 둔다(DutyLogEntry.Mark).
            // Mark는 「어긴 줄 가운데 State == Violated인 것」만 지시를 따름으로 본다.
            // 실제 진행에서는 위반 판정이 났다면 그 공간에 있었다는 뜻이라 이 조합이 나오기 어렵다.
            DutyLogEntry entry = Entry(CardState.Violated, false, true);

            Assert.IsTrue(entry.Struck);
            Assert.AreEqual(DutyMark.Instructed, entry.Mark);
        }
    }
}

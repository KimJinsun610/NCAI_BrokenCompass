using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;

namespace NightDuty.Tests
{
    /// <summary>
    /// 실제 복도 카드 에셋(H1~H6)을 기획서 각 카드의 「검증 절차·기대 결과」로 검사한다.
    /// 에셋이 없으면 NightDuty ▸ 복도 카드 에셋 생성 (H1~H6) 메뉴로 만든다.
    /// </summary>
    public sealed class CorridorCardAssetTests
    {
        private const string Folder = "Assets/_Game/ScriptableObjects/Rules/Corridor/";
        private const string Passage = "corridor.passage";

        private FearAxisSystem _axes;

        [SetUp]
        public void SetUp()
        {
            _axes = new FearAxisSystem();
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.ClearAll();
        }

        private static RuleSO Load(string id)
        {
            RuleSO card = AssetDatabase.LoadAssetAtPath<RuleSO>(Folder + id + ".asset");
            Assert.IsNotNull(card, id + " 에셋이 없습니다. NightDuty ▸ 복도 카드 에셋 생성 (H1~H6) 메뉴를 실행하세요.");
            return card;
        }

        private RuleBook Book(string id)
        {
            return new RuleBook(new[] { Load(id) }, _axes, null, null);
        }

        private static JudgeSignal T(SignalKind kind, string id)
        {
            return JudgeSignal.Target(kind, id);
        }

        [Test]
        public void 여섯장_모두_데이터검사를_통과한다()
        {
            string[] ids = { "H1", "H2", "H3", "H4", "H5", "H6" };
            List<string> errors = new List<string>();
            for (int i = 0; i < ids.Length; i++)
            {
                RuleSO card = Load(ids[i]);
                Assert.AreEqual(ids[i], card.CardId);
                Assert.AreEqual(SpaceId.Corridor, card.Space);
                card.Validate(errors);
            }

            CollectionAssert.IsEmpty(errors);
        }

        // H1 — 문 닫힘 상태에서 자동 개방 관찰 후 통과: 신뢰 +4. E 닫기: 배치 +12.
        [Test]
        public void H1_자동개방관찰후_통과는_신뢰4()
        {
            RuleBook book = Book("H1");
            book.Dispatch(T(SignalKind.DoorAutoOpenObserved, "corridor.door.auto"));
            book.Dispatch(T(SignalKind.PassageCompleted, Passage));

            // 2026-09-21 재설계: 준수 신뢰 +2 → +4 (H1)
            Assert.AreEqual(4, _axes.GetValue(FearAxis.Trust));
        }

        [Test]
        public void H1_자동개방관찰후_E닫기는_배치12()
        {
            RuleBook book = Book("H1");
            book.Dispatch(T(SignalKind.DoorAutoOpenObserved, "corridor.door.13"));
            book.Dispatch(JudgeSignal.DoorCommand("corridor.door.13", true, ActionSource.Player));

            Assert.AreEqual(12, _axes.GetValue(FearAxis.Layout));
        }

        // H2 — 유예 뒤 2.9초 응시 후 이탈: 신뢰 +4. 3.0초 응시: 청각 +15. 2초씩 두 번 끊어 보기: 위반 없음.
        private RuleBook StartH2()
        {
            _axes.Apply(FearAxis.Auditory, 50, "setup", SpaceId.None);
            RuleBook book = Book("H2");
            book.Dispatch(T(SignalKind.ClueDelivered, "corridor.door.back"));
            Assert.AreEqual(CardState.Active, book.Watchers[0].State);
            TestKit.Advance(book, 2f);
            return book;
        }

        [Test]
        public void H2_2초9응시후이탈은_신뢰4()
        {
            RuleBook book = StartH2();
            TestKit.Advance(book, 2.9f, "corridor.door.back");
            book.Dispatch(T(SignalKind.PassageCompleted, Passage));

            // 2026-09-21 재설계: 준수 신뢰 +2 → +4 (H2)
            Assert.AreEqual(4, _axes.GetValue(FearAxis.Trust));
            Assert.AreEqual(50, _axes.GetValue(FearAxis.Auditory));
        }

        [Test]
        public void H2_3초0응시는_청각15()
        {
            RuleBook book = StartH2();
            TestKit.Advance(book, 3f, "corridor.door.back");

            Assert.AreEqual(65, _axes.GetValue(FearAxis.Auditory));
        }

        [Test]
        public void H2_2초씩두번끊어보기는_위반없음()
        {
            RuleBook book = StartH2();
            TestKit.Advance(book, 2f, "corridor.door.back");
            TestKit.Advance(book, 0.1f, "corridor.wall");
            TestKit.Advance(book, 2f, "corridor.door.back");

            Assert.AreEqual(50, _axes.GetValue(FearAxis.Auditory));
        }

        [Test]
        public void H2_청각47에서는_시작하지않는다()
        {
            // 2026-09-21 재설계: 구간 경계가 50 → 48로 내려가 49는 이제 Band2(자격 통과)다. 직전 값 47로 바꾼다.
            _axes.Apply(FearAxis.Auditory, 47, "setup", SpaceId.None);
            RuleBook book = Book("H2");
            book.Dispatch(T(SignalKind.ClueDelivered, "corridor.door.back"));

            Assert.AreEqual(CardState.Waiting, book.Watchers[0].State);
        }

        // H3 — On 입장 뒤 2초 내 Off 후 통과: 신뢰 +4. 유예 뒤 On: 조도 +12 한 번.
        [Test]
        public void H3_On입장뒤_2초내Off후통과는_신뢰4()
        {
            RuleBook book = Book("H3");
            book.Dispatch(JudgeSignal.Flashlight(true));
            book.Dispatch(T(SignalKind.ZoneEntered, Passage));
            TestKit.Advance(book, 1.5f);
            book.Dispatch(JudgeSignal.Flashlight(false));
            TestKit.Advance(book, 3f);
            book.Dispatch(T(SignalKind.PassageCompleted, Passage));

            // 2026-09-21 재설계: 준수 신뢰 +2 → +4 (H3)
            Assert.AreEqual(4, _axes.GetValue(FearAxis.Trust));
            Assert.AreEqual(0, _axes.GetValue(FearAxis.Illuminance));
        }

        [Test]
        public void H3_유예안에되돌아갔다가_같은방문에_다시들어오면_다시판정한다()
        {
            RuleBook book = Book("H3");
            book.Dispatch(JudgeSignal.OfSpace(SignalKind.SpaceEntered, SpaceId.Corridor));
            book.Dispatch(JudgeSignal.Flashlight(true));
            book.Dispatch(T(SignalKind.ZoneEntered, Passage));
            TestKit.Advance(book, 1f);
            book.Dispatch(T(SignalKind.ZoneExited, Passage));
            Assert.AreEqual(CardState.Waiting, book.Watchers[0].State);

            book.Dispatch(T(SignalKind.ZoneEntered, Passage));
            Assert.AreEqual(CardState.Active, book.Watchers[0].State, "취소된 시도는 방문 몫을 돌려준다");
            TestKit.Advance(book, 2f);

            Assert.AreEqual(12, _axes.GetValue(FearAxis.Illuminance));
        }

        [Test]
        public void H1_문을닫지않고_1_3으로_들어가면_신뢰4()
        {
            RuleBook book = Book("H1");
            book.Dispatch(JudgeSignal.OfSpace(SignalKind.SpaceEntered, SpaceId.Corridor));
            book.Dispatch(T(SignalKind.DoorAutoOpenObserved, "corridor.door.13"));
            book.Dispatch(JudgeSignal.OfSpace(SignalKind.SpaceExited, SpaceId.Corridor));
            book.Dispatch(JudgeSignal.OfSpace(SignalKind.SpaceEntered, SpaceId.Classroom_1_3));

            // 2026-09-21 재설계: 준수 신뢰 +2 → +4 (H1)
            Assert.AreEqual(4, _axes.GetValue(FearAxis.Trust));
            Assert.AreEqual(0, _axes.GetValue(FearAxis.Layout));
        }

        [Test]
        public void H3_유예뒤On은_조도12_한번()
        {
            RuleBook book = Book("H3");
            book.Dispatch(T(SignalKind.ZoneEntered, Passage));
            TestKit.Advance(book, 3f);
            book.Dispatch(JudgeSignal.Flashlight(true));
            book.Dispatch(JudgeSignal.Flashlight(false));
            book.Dispatch(JudgeSignal.Flashlight(true));

            Assert.AreEqual(12, _axes.GetValue(FearAxis.Illuminance));
        }

        // H4 — 통행 후 재방문하여 1.4m 진입: 신뢰 선지급 없이 배치 +12. 끝까지 1.5m 이상 유지: 밤 종료 신뢰 +4.
        private RuleBook StartH4()
        {
            _axes.Apply(FearAxis.Layout, 25, "setup", SpaceId.None);
            RuleBook book = Book("H4");
            book.Dispatch(T(SignalKind.ClueIdentified, "corridor.box"));
            Assert.AreEqual(CardState.Active, book.Watchers[0].State);
            return book;
        }

        [Test]
        public void H4_통행후재방문_1m4진입은_신뢰선지급없이_배치12()
        {
            RuleBook book = StartH4();
            book.Dispatch(T(SignalKind.PassageCompleted, Passage));
            Assert.AreEqual(0, _axes.GetValue(FearAxis.Trust));

            book.Dispatch(JudgeSignal.OfSpace(SignalKind.SpaceExited, SpaceId.Corridor));
            book.Dispatch(JudgeSignal.OfSpace(SignalKind.SpaceEntered, SpaceId.Corridor));
            book.Dispatch(JudgeSignal.Proximity("corridor.box", 1.4f));
            book.EndNight();

            Assert.AreEqual(37, _axes.GetValue(FearAxis.Layout));
            Assert.AreEqual(0, _axes.GetValue(FearAxis.Trust));
        }

        [Test]
        public void H4_끝까지1m5이상유지는_밤종료에_신뢰4()
        {
            RuleBook book = StartH4();
            book.Dispatch(T(SignalKind.PassageCompleted, Passage));
            book.Dispatch(JudgeSignal.Proximity("corridor.box", 1.5f));
            book.EndNight();

            // 2026-09-21 재설계: 준수 신뢰 +2 → +4 (H4)
            Assert.AreEqual(4, _axes.GetValue(FearAxis.Trust));
            Assert.AreEqual(25, _axes.GetValue(FearAxis.Layout));
        }

        // H5 — 낙하 후 우회 완료: 신뢰 +4. (배치 72~99)
        [Test]
        public void H5_낙하후우회완료는_신뢰4()
        {
            _axes.Apply(FearAxis.Layout, 75, "setup", SpaceId.None);
            RuleBook book = Book("H5");
            book.Dispatch(T(SignalKind.ClueIdentified, "corridor.debris"));
            book.Dispatch(JudgeSignal.Proximity("corridor.debris", 2.5f));
            book.Dispatch(T(SignalKind.PassageCompleted, Passage));

            // 2026-09-21 재설계: 준수 신뢰 +2 → +4 (H5)
            Assert.AreEqual(4, _axes.GetValue(FearAxis.Trust));
            Assert.AreEqual(75, _axes.GetValue(FearAxis.Layout));
        }

        [Test]
        public void H5_조각1m5미만진입은_배치12_배치71에서는_미발동()
        {
            // 2026-09-21 재설계: 구간 경계가 75 → 72로 내려가 74는 이제 Band3(자격 통과)다. 직전 값 71로 바꾼다.
            _axes.Apply(FearAxis.Layout, 71, "setup", SpaceId.None);
            RuleBook book = Book("H5");
            book.Dispatch(T(SignalKind.ClueIdentified, "corridor.debris"));
            Assert.AreEqual(CardState.Waiting, book.Watchers[0].State);

            _axes.Apply(FearAxis.Layout, 1, "setup", SpaceId.None);   // 71 + 1 = 72 → Band3
            book.Dispatch(T(SignalKind.ClueIdentified, "corridor.debris"));
            book.Dispatch(JudgeSignal.Proximity("corridor.debris", 1.49f));

            Assert.AreEqual(84, _axes.GetValue(FearAxis.Layout));   // 72 + 12
        }

        // H6 — 잔디 밖 통과: 신뢰 +8. 배치 90에서 진입: 100으로 제한하고 포획 종료 요청 1회.
        [Test]
        public void H6_풀밖통과는_신뢰8()
        {
            _axes.Apply(FearAxis.Layout, 90, "setup", SpaceId.None);
            RuleBook book = Book("H6");
            book.Dispatch(T(SignalKind.ClueIdentified, "corridor.tree"));
            book.Dispatch(JudgeSignal.Proximity("corridor.tree", 2.0f));
            book.Dispatch(T(SignalKind.PassageCompleted, Passage));

            // 2026-09-21 재설계: 준수 신뢰 +4 → +8 (H6만 두 배)
            Assert.AreEqual(8, _axes.GetValue(FearAxis.Trust));
            Assert.AreEqual(90, _axes.GetValue(FearAxis.Layout));
        }

        [Test]
        public void H6_배치90에서_진입은_100_포획신호한번()
        {
            int critical = 0;
            EventBus.AxisCritical += a => critical++;
            _axes.Apply(FearAxis.Layout, 90, "setup", SpaceId.None);
            RuleBook book = Book("H6");
            book.Dispatch(T(SignalKind.ClueIdentified, "corridor.tree"));
            book.Dispatch(JudgeSignal.Proximity("corridor.tree", 1.99f));
            book.Dispatch(JudgeSignal.Proximity("corridor.tree", 0.5f));

            Assert.AreEqual(100, _axes.GetValue(FearAxis.Layout));
            Assert.IsTrue(_axes.IsLocked);
            Assert.AreEqual("H6", _axes.Cause.SourceId);
            Assert.AreEqual(1, critical);
        }
    }
}

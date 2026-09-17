using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;

namespace NightDuty.Tests
{
    /// <summary>
    /// 실제 교실 카드 에셋(C1~C6)을 기획서 각 카드의 「검증 절차·기대 결과」와 분석 문서의 추가 시나리오로 검사한다.
    /// 에셋이 없으면 NightDuty ▸ 교실 카드 에셋 생성 (C1~C6) 메뉴로 만든다.
    /// </summary>
    public sealed class ClassroomCardAssetTests
    {
        private const string Folder = "Assets/_Game/ScriptableObjects/Rules/Classroom/";
        private const string CorridorFolder = "Assets/_Game/ScriptableObjects/Rules/Corridor/";
        private const string Passage = "corridor.passage";

        private const string Chalk = "cls11.chalk3";
        private const string Desk = "cls11.desk.turned";
        private const string BackDesk = "cls11.desk.back";
        private const string Lectern = "cls11.lectern";
        private const string LecternNoise = "cls11.lectern.noise";
        private const string Lights11 = "cls11.lights";
        private const string Lights13 = "cls13.lights";
        private const string Door11 = "corridor.door.11";
        private const string Door13 = "corridor.door.13";

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
            if (id.StartsWith("H", System.StringComparison.Ordinal))
            {
                RuleSO corridor = AssetDatabase.LoadAssetAtPath<RuleSO>(CorridorFolder + id + ".asset");
                Assert.IsNotNull(corridor, id + " 에셋이 없습니다. NightDuty ▸ 복도 카드 에셋 생성 (H1~H6) 메뉴를 실행하세요.");
                return corridor;
            }

            RuleSO card = AssetDatabase.LoadAssetAtPath<RuleSO>(Folder + id + ".asset");
            Assert.IsNotNull(card, id + " 에셋이 없습니다. NightDuty ▸ 교실 카드 에셋 생성 (C1~C6) 메뉴를 실행하세요.");
            return card;
        }

        /// <summary>덱 순서대로 카드를 읽어 판정을 만든다.</summary>
        private RuleBook Book(params string[] ids)
        {
            RuleSO[] deck = new RuleSO[ids.Length];
            for (int i = 0; i < ids.Length; i++)
            {
                deck[i] = Load(ids[i]);
            }

            return new RuleBook(deck, _axes, null, null);
        }

        private void Setup(FearAxis axis, int value)
        {
            _axes.Apply(axis, value, "setup", SpaceId.None);
        }

        private static JudgeSignal T(SignalKind kind, string id)
        {
            return JudgeSignal.Target(kind, id);
        }

        private static JudgeSignal Sp(SignalKind kind, SpaceId space)
        {
            return JudgeSignal.OfSpace(kind, space);
        }

        private static JudgeSignal Door(string id, bool close)
        {
            return JudgeSignal.DoorCommand(id, close, ActionSource.Player);
        }

        [Test]
        public void 여섯장_모두_데이터검사를_통과한다()
        {
            string[] ids = { "C1", "C2", "C3", "C4", "C5", "C6" };
            List<string> errors = new List<string>();
            for (int i = 0; i < ids.Length; i++)
            {
                RuleSO card = Load(ids[i]);
                Assert.AreEqual(ids[i], card.CardId);
                Assert.AreEqual(SpaceId.Classroom_1_1, card.Space, ids[i] + " 공간(두 교실 공통 카드는 대표값 1-1)");
                card.Validate(errors);
            }

            CollectionAssert.IsEmpty(errors);
            Assert.IsTrue(Load("C1").IsLongTerm, "C1은 장기(방문을 넘는 순서 의무)");
            Assert.IsTrue(Load("C6").IsLongTerm, "C6은 장기(밤 시작부터 감시)");
            Assert.AreEqual(SignalKind.NightBegan, Load("C6").TriggerKind);
        }

        // ───────── C1 ─────────
        // 기획서 검증: 1-3 문턱만 밟고 1-1 진입: 청각 +12. 1-3 점검 완료 후 1-1 진입: 신뢰 +2 한 번. 청각 50 시작: 새 발동 없음.

        [Test]
        public void C1_1_3문턱만밟고_1_1진입은_청각12()
        {
            RuleBook book = Book("C1");
            book.Dispatch(T(SignalKind.ClueDelivered, Chalk));
            Assert.AreEqual(CardState.Active, book.Watchers[0].State);

            book.Dispatch(Sp(SignalKind.SpaceEntered, SpaceId.Classroom_1_3));
            book.Dispatch(Sp(SignalKind.SpaceExited, SpaceId.Classroom_1_3));
            book.Dispatch(Sp(SignalKind.SpaceEntered, SpaceId.Classroom_1_1));

            Assert.AreEqual(CardState.Violated, book.Watchers[0].State);
            Assert.AreEqual(12, _axes.GetValue(FearAxis.Auditory));
            Assert.AreEqual(0, _axes.GetValue(FearAxis.Trust));
        }

        [Test]
        public void C1_1_3점검완료후_1_1진입은_신뢰2_한번()
        {
            RuleBook book = Book("C1");
            book.Dispatch(T(SignalKind.ClueDelivered, Chalk));
            book.Dispatch(Sp(SignalKind.SpaceEntered, SpaceId.Classroom_1_3));
            book.Dispatch(Sp(SignalKind.InspectionCompleted, SpaceId.Classroom_1_3));
            book.Dispatch(Sp(SignalKind.SpaceExited, SpaceId.Classroom_1_3));
            book.Dispatch(Sp(SignalKind.SpaceEntered, SpaceId.Classroom_1_1));
            book.Dispatch(Sp(SignalKind.InspectionCompleted, SpaceId.Classroom_1_3));   // 재점검 — 추가 지급 없음

            Assert.AreEqual(CardState.Complied, book.Watchers[0].State);
            Assert.AreEqual(2, _axes.GetValue(FearAxis.Trust));
            Assert.AreEqual(0, _axes.GetValue(FearAxis.Auditory));
        }

        [Test]
        public void C1_청각50에서_단서는_새발동없음()
        {
            Setup(FearAxis.Auditory, 50);
            RuleBook book = Book("C1");
            book.Dispatch(T(SignalKind.ClueDelivered, Chalk));
            Assert.AreEqual(CardState.Waiting, book.Watchers[0].State);

            book.EndNight();

            Assert.AreEqual(CardState.Undetermined, book.Watchers[0].State);
            Assert.AreEqual(50, _axes.GetValue(FearAxis.Auditory));
            Assert.AreEqual(0, _axes.GetValue(FearAxis.Trust));
        }

        // 기획서: 1-1 문을 E로 여는 것만으로는 위반하지 않는다.
        [Test]
        public void C1_1_1문E열기만은_위반아님()
        {
            RuleBook book = Book("C1");
            book.Dispatch(T(SignalKind.ClueDelivered, Chalk));
            book.Dispatch(Door(Door11, false));

            Assert.AreEqual(CardState.Active, book.Watchers[0].State);
            Assert.AreEqual(0, _axes.GetValue(FearAxis.Auditory));
        }

        // 장기 근거: 복도 방문에서 H3가 방문 몫을 먼저 써도 하루 1회 분필 단서가 버려지지 않는다.
        [Test]
        public void C1_복도에서_H3가먼저시작된방문에도_시작한다()
        {
            RuleBook book = Book("H3", "C1");
            book.Dispatch(Sp(SignalKind.SpaceEntered, SpaceId.Corridor));
            book.Dispatch(T(SignalKind.ZoneEntered, Passage));
            book.Dispatch(T(SignalKind.ClueDelivered, Chalk));

            Assert.AreEqual(CardState.Active, book.Watchers[0].State, "H3");
            Assert.AreEqual(CardState.Active, book.Watchers[1].State, "C1");
        }

        // ───────── C2 ─────────
        // 기획서 검증: C-A 1초 관찰 후 거리 유지·점검·퇴실: 신뢰 +2. 여러 의자 근접: 지정 대상 위반 한 번만 계산.

        [Test]
        public void C2_CA관찰후_거리유지_점검_퇴실은_신뢰2_배치0에서도_시작()
        {
            RuleBook book = Book("C2");
            book.Dispatch(Sp(SignalKind.SpaceEntered, SpaceId.Classroom_1_1));
            book.Dispatch(T(SignalKind.ModelObserved, "scene.ca"));
            book.Dispatch(T(SignalKind.ClueIdentified, Desk));
            Assert.AreEqual(CardState.Active, book.Watchers[0].State, "구간 검사를 끄고 좌석 식별로 시작한다");

            book.Dispatch(JudgeSignal.Proximity(Desk, 1.5f));
            book.Dispatch(Sp(SignalKind.InspectionCompleted, SpaceId.Classroom_1_1));
            book.Dispatch(Sp(SignalKind.SpaceExited, SpaceId.Classroom_1_1));

            Assert.AreEqual(2, _axes.GetValue(FearAxis.Trust));
            Assert.AreEqual(0, _axes.GetValue(FearAxis.Layout));
        }

        [Test]
        public void C2_여러의자근접은_지정대상위반_한번만()
        {
            Setup(FearAxis.Layout, 75);
            RuleBook book = Book("C2");
            book.Dispatch(T(SignalKind.ClueIdentified, Desk));
            book.Dispatch(JudgeSignal.Proximity("cls11.chair.r2", 0.5f));   // 지정 대상 아님
            Assert.AreEqual(75, _axes.GetValue(FearAxis.Layout));

            book.Dispatch(JudgeSignal.Proximity(Desk, 1.4f));
            book.Dispatch(JudgeSignal.Proximity(Desk, 1.0f));

            Assert.AreEqual(87, _axes.GetValue(FearAxis.Layout));
        }

        // 기획서: 점검하지 않고 퇴실하면 미판정 상태로 남겨 다음 유효 방문에 기회를 준다.
        [Test]
        public void C2_점검없이퇴실은_대기_다음방문에_준수가능()
        {
            RuleBook book = Book("C2");
            book.Dispatch(Sp(SignalKind.SpaceEntered, SpaceId.Classroom_1_1));
            book.Dispatch(T(SignalKind.ClueIdentified, Desk));
            book.Dispatch(Sp(SignalKind.SpaceExited, SpaceId.Classroom_1_1));
            Assert.AreEqual(CardState.Waiting, book.Watchers[0].State);

            book.Dispatch(Sp(SignalKind.SpaceEntered, SpaceId.Classroom_1_1));
            book.Dispatch(T(SignalKind.ClueIdentified, Desk));
            Assert.AreEqual(CardState.Active, book.Watchers[0].State);
            book.Dispatch(Sp(SignalKind.InspectionCompleted, SpaceId.Classroom_1_1));

            Assert.AreEqual(2, _axes.GetValue(FearAxis.Trust));
        }

        // 기획서: 수평 거리 1.5m 「미만」이 위반 — 1.50m는 바깥.
        [Test]
        public void C2_1m50은_바깥()
        {
            RuleBook book = Book("C2");
            book.Dispatch(T(SignalKind.ClueIdentified, Desk));
            book.Dispatch(JudgeSignal.Proximity(Desk, 1.5f));

            Assert.AreEqual(CardState.Active, book.Watchers[0].State);
            Assert.AreEqual(0, _axes.GetValue(FearAxis.Layout));
        }

        // ───────── C3 ─────────
        // 기획서 검증: 일반 퇴실 중 3초 응시: 청각 +15. C-A의 같은 타격음: C3 0. 소리 나기 전에 이미 실외: 사건 미시작.

        private RuleBook StartC3()
        {
            Setup(FearAxis.Auditory, 50);
            RuleBook book = Book("C3");
            book.Dispatch(T(SignalKind.ClueDelivered, BackDesk));
            Assert.AreEqual(CardState.Active, book.Watchers[0].State);
            return book;
        }

        [Test]
        public void C3_유예뒤_3초응시는_청각15_이후퇴실해도_신뢰0()
        {
            RuleBook book = StartC3();
            TestKit.Advance(book, 2f);
            TestKit.Advance(book, 3f, BackDesk);
            Assert.AreEqual(65, _axes.GetValue(FearAxis.Auditory));

            book.Dispatch(Sp(SignalKind.SpaceExited, SpaceId.Classroom_1_1));
            Assert.AreEqual(0, _axes.GetValue(FearAxis.Trust));
        }

        [Test]
        public void C3_CA의_같은타격음은_C3_0()
        {
            Setup(FearAxis.Auditory, 50);
            RuleBook book = Book("C3");
            book.Dispatch(T(SignalKind.ModelObserved, "scene.ca"));   // 모형 효과음 — 단서 신호 없음
            TestKit.Advance(book, 5f, BackDesk);
            book.Dispatch(Sp(SignalKind.SpaceExited, SpaceId.Classroom_1_1));

            Assert.AreEqual(CardState.Waiting, book.Watchers[0].State);
            Assert.AreEqual(50, _axes.GetValue(FearAxis.Auditory));
            Assert.AreEqual(0, _axes.GetValue(FearAxis.Trust));
        }

        [Test]
        public void C3_소리전에_이미실외면_미시작()
        {
            Setup(FearAxis.Auditory, 50);
            RuleBook book = Book("C3");
            book.Dispatch(Sp(SignalKind.SpaceExited, SpaceId.Classroom_1_1));
            book.EndNight();

            Assert.AreEqual(CardState.Undetermined, book.Watchers[0].State);
            Assert.AreEqual(50, _axes.GetValue(FearAxis.Auditory));
            Assert.AreEqual(0, _axes.GetValue(FearAxis.Trust));
        }

        [Test]
        public void C3_유예뒤_2초9응시후퇴실은_신뢰2()
        {
            RuleBook book = StartC3();
            TestKit.Advance(book, 2f);
            TestKit.Advance(book, 2.9f, BackDesk);
            book.Dispatch(Sp(SignalKind.SpaceExited, SpaceId.Classroom_1_1));

            Assert.AreEqual(2, _axes.GetValue(FearAxis.Trust));
            Assert.AreEqual(50, _axes.GetValue(FearAxis.Auditory));
        }

        // 기획서: 음원 시작 2초 뒤부터 센다 — 유예 중 응시는 쌓이지 않는다.
        [Test]
        public void C3_유예중응시는_쌓이지않는다()
        {
            RuleBook book = StartC3();
            TestKit.Advance(book, 2f, BackDesk);
            TestKit.Advance(book, 1.9f, BackDesk);

            Assert.AreEqual(CardState.Active, book.Watchers[0].State);
            Assert.AreEqual(50, _axes.GetValue(FearAxis.Auditory));
        }

        [Test]
        public void C3_청각49에서는_시작하지않는다()
        {
            Setup(FearAxis.Auditory, 49);
            RuleBook book = Book("C3");
            book.Dispatch(T(SignalKind.ClueDelivered, BackDesk));

            Assert.AreEqual(CardState.Waiting, book.Watchers[0].State);
        }

        // ───────── C4 ─────────
        // 기획서 검증: 칠판만 멎고 의자 소리 중 접근: 청각 +15. 모든 음원 종료 뒤 접근: 위반 없음. Tab 중 음원만 끝나면 동기화 오류.

        private RuleBook StartC4()
        {
            Setup(FearAxis.Auditory, 75);
            RuleBook book = Book("C4");
            book.Dispatch(T(SignalKind.ClueDelivered, LecternNoise));
            Assert.AreEqual(CardState.Active, book.Watchers[0].State);
            return book;
        }

        [Test]
        public void C4_칠판만멎고_의자소리중_접근은_청각15()
        {
            RuleBook book = StartC4();
            book.Dispatch(T(SignalKind.SequenceEnded, "cls11.chalk.scrape"));   // 부분 종료 — 무시
            Assert.AreEqual(CardState.Active, book.Watchers[0].State);

            book.Dispatch(JudgeSignal.Proximity(Lectern, 1.4f));

            Assert.AreEqual(90, _axes.GetValue(FearAxis.Auditory));
        }

        [Test]
        public void C4_모든음원종료뒤_접근은_위반없음()
        {
            RuleBook book = StartC4();
            book.Dispatch(T(SignalKind.SequenceEnded, LecternNoise));
            Assert.AreEqual(2, _axes.GetValue(FearAxis.Trust));

            book.Dispatch(JudgeSignal.Proximity(Lectern, 0.5f));

            Assert.AreEqual(75, _axes.GetValue(FearAxis.Auditory));
        }

        // Tab 중에는 코어가 신호를 버린다 → 클라이언트가 Tab 중 음원을 멈추지 않으면 종료 신호를 잃는다(동기화 오류 검출).
        [Test]
        public void C4_Tab중_음원만끝나면_끝난것으로_처리되지않는다()
        {
            RuleBook book = StartC4();
            book.Dispatch(JudgeSignal.Tab(true));
            book.Dispatch(T(SignalKind.SequenceEnded, LecternNoise));
            book.Dispatch(JudgeSignal.Tab(false));

            Assert.AreEqual(CardState.Active, book.Watchers[0].State);
            Assert.AreEqual(0, _axes.GetValue(FearAxis.Trust));
        }

        // 기획서: 그 전에 안전하게 퇴실하면 신뢰 +2.
        [Test]
        public void C4_종료전퇴실은_신뢰2()
        {
            RuleBook book = StartC4();
            book.Dispatch(Sp(SignalKind.SpaceExited, SpaceId.Classroom_1_1));

            Assert.AreEqual(2, _axes.GetValue(FearAxis.Trust));
        }

        // 기획서: 해당 방문에 다른 단기 사건이 없어야 한다.
        [Test]
        public void C4_같은방문에_C2가이미시작됐으면_미시작()
        {
            Setup(FearAxis.Layout, 25);
            Setup(FearAxis.Auditory, 75);
            RuleBook book = Book("C2", "C4");
            book.Dispatch(Sp(SignalKind.SpaceEntered, SpaceId.Classroom_1_1));
            book.Dispatch(T(SignalKind.ClueIdentified, Desk));
            book.Dispatch(T(SignalKind.ClueDelivered, LecternNoise));

            Assert.AreEqual(CardState.Active, book.Watchers[0].State, "C2");
            Assert.AreEqual(CardState.Waiting, book.Watchers[1].State, "C4");
        }

        // ───────── C5 ─────────
        // 기획서 검증: 유예 내 On 후 점검·퇴실: 신뢰 +3. 1-1에서 정산 후 1-3 재점검: 추가 0. 복도로 나온 후 중립 구역에서 Off: 위반 없음.

        /// <summary>조도 50에서 1-1 입장 → 식별 → 1초 뒤 On → 3초 → 점검 → 퇴실(준수).</summary>
        private void RunC5Complied(RuleBook book)
        {
            book.Dispatch(Sp(SignalKind.SpaceEntered, SpaceId.Classroom_1_1));
            book.Dispatch(T(SignalKind.ClueIdentified, Lights11));
            Assert.AreEqual(CardState.Active, book.Watchers[0].State);
            TestKit.Advance(book, 1f);
            book.Dispatch(JudgeSignal.Flashlight(true));
            TestKit.Advance(book, 3f);
            book.Dispatch(Sp(SignalKind.InspectionCompleted, SpaceId.Classroom_1_1));
            book.Dispatch(Sp(SignalKind.SpaceExited, SpaceId.Classroom_1_1));
        }

        [Test]
        public void C5_유예내On후_점검퇴실은_신뢰3()
        {
            Setup(FearAxis.Illuminance, 50);
            RuleBook book = Book("C5");
            RunC5Complied(book);

            Assert.AreEqual(3, _axes.GetValue(FearAxis.Trust));
            Assert.AreEqual(50, _axes.GetValue(FearAxis.Illuminance));
        }

        [Test]
        public void C5_1_1에서정산후_1_3재점검은_추가0()
        {
            Setup(FearAxis.Illuminance, 50);
            RuleBook book = Book("C5");
            RunC5Complied(book);

            book.Dispatch(JudgeSignal.Flashlight(false));
            book.Dispatch(Sp(SignalKind.SpaceEntered, SpaceId.Classroom_1_3));
            book.Dispatch(T(SignalKind.ClueIdentified, Lights13));
            TestKit.Advance(book, 3f);
            book.Dispatch(Sp(SignalKind.InspectionCompleted, SpaceId.Classroom_1_3));

            Assert.AreEqual(3, _axes.GetValue(FearAxis.Trust));
            Assert.AreEqual(50, _axes.GetValue(FearAxis.Illuminance));
        }

        [Test]
        public void C5_복도로나온뒤_중립구역Off는_위반없음()
        {
            Setup(FearAxis.Illuminance, 50);
            RuleBook book = Book("C5");
            RunC5Complied(book);

            book.Dispatch(JudgeSignal.Flashlight(false));
            TestKit.Advance(book, 3f);

            Assert.AreEqual(50, _axes.GetValue(FearAxis.Illuminance));
        }

        // 기획서: 유예가 끝나도 Off이면 조도 +12.
        [Test]
        public void C5_유예뒤Off는_조도12()
        {
            Setup(FearAxis.Illuminance, 50);
            RuleBook book = Book("C5");
            book.Dispatch(T(SignalKind.ClueIdentified, Lights11));
            TestKit.Advance(book, 2f);

            Assert.AreEqual(62, _axes.GetValue(FearAxis.Illuminance));
        }

        // 기획서: 이후 퇴실 전 Off로 바꾸면 조도 +12(한 번).
        [Test]
        public void C5_On후_퇴실전Off는_조도12_한번()
        {
            Setup(FearAxis.Illuminance, 50);
            RuleBook book = Book("C5");
            book.Dispatch(T(SignalKind.ClueIdentified, Lights11));
            book.Dispatch(JudgeSignal.Flashlight(true));
            TestKit.Advance(book, 3f);
            book.Dispatch(JudgeSignal.Flashlight(false));
            book.Dispatch(JudgeSignal.Flashlight(true));
            book.Dispatch(JudgeSignal.Flashlight(false));
            book.Dispatch(Sp(SignalKind.InspectionCompleted, SpaceId.Classroom_1_1));
            book.Dispatch(Sp(SignalKind.SpaceExited, SpaceId.Classroom_1_1));

            Assert.AreEqual(62, _axes.GetValue(FearAxis.Illuminance));
            Assert.AreEqual(0, _axes.GetValue(FearAxis.Trust));
        }

        // 기획서: 짧은 입구 왕복은 준수 없음. (해석: 대기로 돌아가 처음 유효하게 진행된 방문 — 여기서는 1-3 — 의 결과를 기록)
        [Test]
        public void C5_짧은입구왕복은_대기_1_3에서_다시시작해_준수()
        {
            Setup(FearAxis.Illuminance, 50);
            RuleBook book = Book("C5");
            book.Dispatch(Sp(SignalKind.SpaceEntered, SpaceId.Classroom_1_1));
            book.Dispatch(T(SignalKind.ClueIdentified, Lights11));
            TestKit.Advance(book, 1f);
            book.Dispatch(Sp(SignalKind.SpaceExited, SpaceId.Classroom_1_1));

            Assert.AreEqual(CardState.Waiting, book.Watchers[0].State);
            Assert.AreEqual(0, _axes.GetValue(FearAxis.Trust));
            Assert.AreEqual(50, _axes.GetValue(FearAxis.Illuminance));

            book.Dispatch(Sp(SignalKind.SpaceEntered, SpaceId.Classroom_1_3));
            book.Dispatch(T(SignalKind.ClueIdentified, Lights13));
            Assert.AreEqual(CardState.Active, book.Watchers[0].State);
            book.Dispatch(JudgeSignal.Flashlight(true));
            TestKit.Advance(book, 3f);
            book.Dispatch(Sp(SignalKind.InspectionCompleted, SpaceId.Classroom_1_3));

            Assert.AreEqual(3, _axes.GetValue(FearAxis.Trust));
            Assert.AreEqual(50, _axes.GetValue(FearAxis.Illuminance));
        }

        // 공통 교차 「빛 경계」: 교실에서 On 유지(C5 준수) → 경계 중립 구역에서 Off → 복도 통행(H3)은 Off라 위반 없음.
        [Test]
        public void C5_H3_빛경계_교실On_복도Off는_둘다위반없음()
        {
            Setup(FearAxis.Illuminance, 50);
            RuleBook book = Book("C5", "H3");
            RunC5Complied(book);
            Assert.AreEqual(3, _axes.GetValue(FearAxis.Trust));

            book.Dispatch(JudgeSignal.Flashlight(false));
            book.Dispatch(Sp(SignalKind.SpaceEntered, SpaceId.Corridor));
            book.Dispatch(T(SignalKind.ZoneEntered, Passage));
            TestKit.Advance(book, 3f);

            Assert.AreEqual(CardState.Active, book.Watchers[1].State, "H3");
            Assert.AreEqual(50, _axes.GetValue(FearAxis.Illuminance));
        }

        // ───────── C6 ─────────
        // 기획서 검증: 문 두 개 미폐쇄: 배치 +12 한 번. 자동 개방 문 유지·두 교실 점검: 신뢰 +3. E 재개방 후 미폐쇄: 종료 때 위반.

        private RuleBook StartC6(params string[] deck)
        {
            RuleBook book = Book(deck.Length > 0 ? deck : new[] { "C6" });
            int c6 = IndexOf(book, "C6");
            Assert.AreEqual(CardState.Waiting, book.Watchers[c6].State);
            book.BeginNight();
            Assert.AreEqual(CardState.Active, book.Watchers[c6].State, "C6은 밤 시작부터 감시");
            return book;
        }

        private static int IndexOf(RuleBook book, string id)
        {
            for (int i = 0; i < book.Watchers.Count; i++)
            {
                if (book.Watchers[i].Card.CardId == id)
                {
                    return i;
                }
            }

            Assert.Fail(id + "가 덱에 없습니다.");
            return -1;
        }

        private static void InspectBoth(RuleBook book)
        {
            book.Dispatch(Sp(SignalKind.InspectionCompleted, SpaceId.Classroom_1_1));
            book.Dispatch(Sp(SignalKind.InspectionCompleted, SpaceId.Classroom_1_3));
        }

        [Test]
        public void C6_문두개미폐쇄는_밤종료에_배치12_한번()
        {
            RuleBook book = StartC6();
            InspectBoth(book);
            book.Dispatch(Door(Door11, false));
            book.Dispatch(Door(Door13, false));
            Assert.AreEqual(0, _axes.GetValue(FearAxis.Layout), "밤 종료 전에는 정산하지 않는다");

            book.EndNight();

            Assert.AreEqual(CardState.Violated, book.Watchers[0].State);
            Assert.AreEqual(12, _axes.GetValue(FearAxis.Layout));
            Assert.AreEqual(0, _axes.GetValue(FearAxis.Trust));
        }

        [Test]
        public void C6_자동개방문유지_두교실점검은_신뢰3()
        {
            RuleBook book = StartC6();
            book.Dispatch(T(SignalKind.DoorAutoOpenObserved, Door13));
            InspectBoth(book);
            book.EndNight();

            Assert.AreEqual(CardState.Complied, book.Watchers[0].State);
            Assert.AreEqual(3, _axes.GetValue(FearAxis.Trust));
            Assert.AreEqual(0, _axes.GetValue(FearAxis.Layout));
        }

        [Test]
        public void C6_E재개방후_미폐쇄는_종료때위반()
        {
            RuleBook book = StartC6();
            book.Dispatch(Door(Door11, false));
            book.Dispatch(Door(Door11, true));
            book.Dispatch(Door(Door11, false));
            InspectBoth(book);
            book.EndNight();

            Assert.AreEqual(12, _axes.GetValue(FearAxis.Layout));
            Assert.AreEqual(0, _axes.GetValue(FearAxis.Trust));
        }

        [Test]
        public void C6_열고닫기완료_두교실점검은_신뢰3()
        {
            RuleBook book = StartC6();
            book.Dispatch(Door(Door11, false));
            book.Dispatch(Door(Door11, true));
            InspectBoth(book);
            book.EndNight();

            Assert.AreEqual(3, _axes.GetValue(FearAxis.Trust));
            Assert.AreEqual(0, _axes.GetValue(FearAxis.Layout));
        }

        // 기획서: 도달 가능한 교실의 점검 누락 → 배치 +12.
        [Test]
        public void C6_1_3미점검은_밤종료에_배치12()
        {
            RuleBook book = StartC6();
            book.Dispatch(Sp(SignalKind.InspectionCompleted, SpaceId.Classroom_1_1));
            book.EndNight();

            Assert.AreEqual(12, _axes.GetValue(FearAxis.Layout));
            Assert.AreEqual(0, _axes.GetValue(FearAxis.Trust));
        }

        // 기획서: 일반 재방문 때 지급하지 않는다(밤 종료 한 번).
        [Test]
        public void C6_재점검을_반복해도_밤종료전에는_지급없음()
        {
            RuleBook book = StartC6();
            InspectBoth(book);
            InspectBoth(book);
            book.Dispatch(Sp(SignalKind.InspectionCompleted, SpaceId.Classroom_1_1));

            Assert.AreEqual(CardState.Active, book.Watchers[0].State);
            Assert.AreEqual(0, _axes.GetValue(FearAxis.Trust));

            book.EndNight();
            Assert.AreEqual(3, _axes.GetValue(FearAxis.Trust));
        }

        // 기획서: 자동 개방 문은 의무를 만들지 않는다(연출 출처 명령 포함).
        [Test]
        public void C6_연출이연문은_의무없음()
        {
            RuleBook book = StartC6();
            book.Dispatch(JudgeSignal.DoorCommand(Door11, false, ActionSource.Direction));
            InspectBoth(book);
            book.EndNight();

            Assert.AreEqual(3, _axes.GetValue(FearAxis.Trust));
            Assert.AreEqual(0, _axes.GetValue(FearAxis.Layout));
        }

        // 공통 교차 「P3와 C6」: P3를 따라 1-1에 먼저 들어가면 C1 위반, 그래도 1-3 점검 의무는 남고 C6는 따로 준수.
        [Test]
        public void C6_C1_순서를어겨도_두교실점검하면_청각12_신뢰3()
        {
            RuleBook book = StartC6("C1", "C6");
            book.Dispatch(T(SignalKind.ClueDelivered, Chalk));
            book.Dispatch(Sp(SignalKind.SpaceEntered, SpaceId.Classroom_1_1));
            Assert.AreEqual(12, _axes.GetValue(FearAxis.Auditory), "C1 선진입 위반");

            book.Dispatch(Sp(SignalKind.InspectionCompleted, SpaceId.Classroom_1_1));
            book.Dispatch(Sp(SignalKind.SpaceExited, SpaceId.Classroom_1_1));
            book.Dispatch(Sp(SignalKind.SpaceEntered, SpaceId.Classroom_1_3));
            book.Dispatch(Sp(SignalKind.InspectionCompleted, SpaceId.Classroom_1_3));
            book.EndNight();

            Assert.AreEqual(12, _axes.GetValue(FearAxis.Auditory));
            Assert.AreEqual(3, _axes.GetValue(FearAxis.Trust));
            Assert.AreEqual(CardState.Violated, book.Watchers[0].State, "C1");
            Assert.AreEqual(CardState.Complied, book.Watchers[1].State, "C6");
        }

        // 밤 종료 신호는 Tab 여부와 무관하게 전달된다.
        [Test]
        public void C6_Tab이열린채_밤종료해도_정산된다()
        {
            RuleBook book = StartC6();
            InspectBoth(book);
            book.Dispatch(JudgeSignal.Tab(true));
            book.EndNight();

            Assert.AreEqual(CardState.Complied, book.Watchers[0].State);
            Assert.AreEqual(3, _axes.GetValue(FearAxis.Trust));
        }
    }
}

using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;

namespace NightDuty.Tests
{
    /// <summary>
    /// 실제 화장실 카드 에셋(T1~T6)을 기획서 각 카드의 「검증 절차·기대 결과」와 분석 문서의 추가 시나리오로 검사한다.
    /// 에셋이 없으면 NightDuty ▸ 화장실 카드 에셋 생성 (T1~T6) 메뉴로 만든다.
    /// </summary>
    public sealed class ToiletCardAssetTests
    {
        private const string Folder = "Assets/_Game/ScriptableObjects/Rules/Toilet/";

        private const string OuterStall = "toilet.stall.outer";
        private const string InnerStall = "toilet.stall.inner";
        private const string OuterInside = "toilet.stall.outer.inside";
        private const string InnerInside = "toilet.stall.inner.inside";
        private const string EntranceDoor = "toilet.door";
        private const string Flush = "toilet.flush";
        private const string Sink = "toilet.sink";
        private const string SinkCall = "toilet.sink.call";
        private const string StallLight = "toilet.stall.light";
        private const string BothOpen = "toilet.stalls.bothopen";

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
            Assert.IsNotNull(card, id + " 에셋이 없습니다. NightDuty ▸ 화장실 카드 에셋 생성 (T1~T6) 메뉴를 실행하세요.");
            return card;
        }

        private RuleBook Book(string id)
        {
            return new RuleBook(new[] { Load(id) }, _axes, null, null);
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

        private void AssertAllAxes(int auditory, int illuminance, int layout, int trust)
        {
            Assert.AreEqual(auditory, _axes.GetValue(FearAxis.Auditory), "청각");
            Assert.AreEqual(illuminance, _axes.GetValue(FearAxis.Illuminance), "조도");
            Assert.AreEqual(layout, _axes.GetValue(FearAxis.Layout), "배치");
            Assert.AreEqual(trust, _axes.GetValue(FearAxis.Trust), "신뢰");
        }

        [Test]
        public void 여섯장_모두_데이터검사를_통과한다()
        {
            string[] ids = { "T1", "T2", "T3", "T4", "T5", "T6" };
            List<string> errors = new List<string>();
            for (int i = 0; i < ids.Length; i++)
            {
                RuleSO card = Load(ids[i]);
                Assert.AreEqual(ids[i], card.CardId);
                Assert.AreEqual(SpaceId.Toilet, card.Space);
                card.Validate(errors);
            }

            CollectionAssert.IsEmpty(errors);
            Assert.IsTrue(Load("T1").IsLongTerm, "T1은 장기(그날 칸 개폐 금지)");
            Assert.IsTrue(Load("T6").IsLongTerm, "T6은 장기(밤 종료까지 칸 진입 금지)");
            // 2026-09-21: T6에 배치 자격이 되살아났다(구간 검사 끔 → 켬).
            // 2026-09-22 자격 재설계: 하한이 Band3 → Band2다.
            // 두 칸 개방 식별 신호는 여전히 필요하다 — 자격과 트리거는 별개다.
            RuleSO t6 = Load("T6");
            Assert.IsTrue(t6.UseEligibleBand, "T6은 배치 구간 검사를 쓴다");
            Assert.AreEqual(FearAxis.Layout, t6.EligibleAxis, "T6 자격 축은 배치");
            Assert.AreEqual(Band.Band2, t6.EligibleFrom, "T6 자격 하한은 Band2");
            Assert.AreEqual(Band.Band4, t6.EligibleTo, "T6 자격 상한은 Band4");
        }

        // ───────── T1 ─────────
        // 기획서 검증: 자동 움직임 후 출입문 E: 0. 칸 문 E: 배치 +12. 처음부터 열린 칸만 관찰: T1 미시작.
        // 2026-09-21 재설계: 준수 신뢰 +2 → +4.

        private RuleBook StartT1()
        {
            RuleBook book = Book("T1");
            book.Dispatch(T(SignalKind.DoorAutoOpenObserved, OuterStall));
            Assert.AreEqual(CardState.Active, book.Watchers[0].State);
            return book;
        }

        [Test]
        public void T1_자동움직임후_출입문E는_0_밤종료에_신뢰4()
        {
            RuleBook book = StartT1();
            book.Dispatch(Door(EntranceDoor, false));
            Assert.AreEqual(0, _axes.GetValue(FearAxis.Trust), "준수는 밤 종료에만");

            book.EndNight();

            Assert.AreEqual(CardState.Complied, book.Watchers[0].State);
            AssertAllAxes(0, 0, 0, 4);   // 2026-09-21 재설계: 준수 신뢰 +2 → +4
        }

        [Test]
        public void T1_칸문E는_배치12()
        {
            RuleBook book = StartT1();
            book.Dispatch(Door(InnerStall, true));

            Assert.AreEqual(CardState.Violated, book.Watchers[0].State);
            Assert.AreEqual(12, _axes.GetValue(FearAxis.Layout));
        }

        [Test]
        public void T1_처음부터열린칸만_관찰은_미시작()
        {
            RuleBook book = Book("T1");
            book.Dispatch(T(SignalKind.ClueIdentified, OuterStall));   // 자동 움직임 관찰 신호가 아니다
            book.Dispatch(Door(OuterStall, true));

            Assert.AreEqual(CardState.Waiting, book.Watchers[0].State);
            Assert.AreEqual(0, _axes.GetValue(FearAxis.Layout));
        }

        // 기획서: 방을 나가도 감시 지속.
        [Test]
        public void T1_방을나갔다와도_감시지속()
        {
            RuleBook book = StartT1();
            book.Dispatch(Sp(SignalKind.SpaceExited, SpaceId.Toilet));
            book.Dispatch(Sp(SignalKind.SpaceEntered, SpaceId.Toilet));
            book.Dispatch(Door(OuterStall, false));

            Assert.AreEqual(12, _axes.GetValue(FearAxis.Layout));
        }

        // 기획서: 위반 뒤 반복 개폐에는 델타 없음(재개방에 중복 벌점 없음).
        [Test]
        public void T1_위반후_반복개폐는_추가델타없음()
        {
            RuleBook book = StartT1();
            book.Dispatch(Door(InnerStall, true));
            book.Dispatch(Door(InnerStall, false));
            book.Dispatch(Door(OuterStall, true));
            book.EndNight();

            AssertAllAxes(0, 0, 12, 0);
        }

        // 자동/수동 출처 구분: 연출이 움직인 칸 문은 조작이 아니다.
        [Test]
        public void T1_연출출처_칸문동작은_위반아님()
        {
            RuleBook book = StartT1();
            book.Dispatch(JudgeSignal.DoorCommand(OuterStall, false, ActionSource.Direction));
            book.EndNight();

            AssertAllAxes(0, 0, 0, 4);   // 2026-09-21 재설계: 준수 신뢰 +2 → +4
        }

        // 자격: 배치 0~89(Band0~Band3).
        // 2026-09-21: 경계가 50/75 → 48/72로 밀려 상한이 74 → 71이었다.
        // 2026-09-22 자격 재설계: 상한을 Band2(71) → **Band3(89)**로 올렸다. 일차 하한이 5일차에 72라
        // 이 장기 카드가 마지막 날에 절대 안 열렸다(실측 5일차 0%). 경계 양쪽을 모두 건다.
        [TestCase(89, true)]
        [TestCase(90, false)]
        public void T1_배치구간자격(int layout, bool expectActive)
        {
            Setup(FearAxis.Layout, layout);
            RuleBook book = Book("T1");
            book.Dispatch(T(SignalKind.DoorAutoOpenObserved, OuterStall));

            Assert.AreEqual(expectActive ? CardState.Active : CardState.Waiting, book.Watchers[0].State);
        }

        // ───────── T2 ─────────
        // 기획서 검증: 11.9초에 실외: 신뢰 +5. 정확히 12초에 퇴실: 청각 +15. 5초에 Tab 10초 열고 닫기: 남은 게임플레이 시간 7초.
        // 2026-09-21 재설계: 준수 신뢰 +3 → +5. 청각 구간 자격은 없어졌다(항상 자격 통과).

        private RuleBook StartT2()
        {
            // 2026-09-21 재설계: T2의 청각 자격이 없어졌다. 아래 25는 자격용이 아니라
            // 위반 시 청각 누적(25 + 15 = 40)을 확인하기 위한 초기값일 뿐이다.
            Setup(FearAxis.Auditory, 25);
            RuleBook book = Book("T2");
            book.Dispatch(T(SignalKind.ClueDelivered, Flush));
            Assert.AreEqual(CardState.Active, book.Watchers[0].State);
            return book;
        }

        [Test]
        public void T2_11초9에_실외는_신뢰5()
        {
            RuleBook book = StartT2();
            TestKit.Advance(book, 11.9f);
            book.Dispatch(Sp(SignalKind.SpaceExited, SpaceId.Toilet));

            AssertAllAxes(25, 0, 0, 5);   // 2026-09-21 재설계: 준수 신뢰 +3 → +5
        }

        // 같은 프레임이면 Tick이 이동 신호보다 먼저 온다(연결 약속 보완) → 12초 퇴실은 실패.
        [Test]
        public void T2_정확히12초에_퇴실은_청각15()
        {
            RuleBook book = StartT2();
            TestKit.Advance(book, 12f);
            book.Dispatch(Sp(SignalKind.SpaceExited, SpaceId.Toilet));

            Assert.AreEqual(CardState.Violated, book.Watchers[0].State);
            AssertAllAxes(40, 0, 0, 0);
        }

        [Test]
        public void T2_5초에_Tab10초는_남은시간7초()
        {
            RuleBook book = StartT2();
            TestKit.Advance(book, 5f);
            book.Dispatch(JudgeSignal.Tab(true));
            TestKit.Advance(book, 10f);
            book.Dispatch(Sp(SignalKind.SpaceExited, SpaceId.Toilet));   // Tab 중 이동도 멈춤 — 버려진다
            book.Dispatch(JudgeSignal.Tab(false));
            Assert.AreEqual(CardState.Active, book.Watchers[0].State);

            TestKit.Advance(book, 6.9f);
            Assert.AreEqual(CardState.Active, book.Watchers[0].State);

            TestKit.Advance(book, 0.1f);
            Assert.AreEqual(CardState.Violated, book.Watchers[0].State);
            Assert.AreEqual(40, _axes.GetValue(FearAxis.Auditory));
        }

        // 기획서: 칸에서 공용부로 나오는 것은 퇴실이 아니다.
        [Test]
        public void T2_칸에서공용부로나옴은_퇴실아님()
        {
            RuleBook book = StartT2();
            book.Dispatch(T(SignalKind.ZoneExited, InnerInside));

            Assert.AreEqual(CardState.Active, book.Watchers[0].State);
            Assert.AreEqual(0, _axes.GetValue(FearAxis.Trust));
        }

        // 기획서: 이탈 뒤 재진입해도 동일 시퀀스로 다시 채점하지 않는다.
        [Test]
        public void T2_이탈뒤재진입해도_재채점없음()
        {
            RuleBook book = StartT2();
            TestKit.Advance(book, 3f);
            book.Dispatch(Sp(SignalKind.SpaceExited, SpaceId.Toilet));
            book.Dispatch(Sp(SignalKind.SpaceEntered, SpaceId.Toilet));
            book.Dispatch(T(SignalKind.ClueDelivered, Flush));
            TestKit.Advance(book, 13f);

            AssertAllAxes(25, 0, 0, 5);   // 2026-09-21 재설계: 준수 신뢰 +3 → +5 (재채점이 없으므로 여전히 1회분)
        }

        // 2026-09-21 재설계: T2의 청각 구간 자격이 사라져(항상 자격 통과) 「청각 24에서는 시작하지 않는다」의
        // 전제 자체가 없어졌다. 뜻을 뒤집어 「구간과 무관하게(청각 0에서도) 시작한다」를 검증한다.
        [Test]
        public void T2_청각0에서도_시작한다()
        {
            RuleBook book = Book("T2");
            book.Dispatch(T(SignalKind.ClueDelivered, Flush));

            Assert.AreEqual(CardState.Active, book.Watchers[0].State);
            Assert.AreEqual(0, _axes.GetValue(FearAxis.Auditory), "청각은 손대지 않았다");
        }

        // ───────── T3 ─────────
        // 기획서 검증: 관찰 후 열린 채 퇴실: 배치 +15. 닫기 완료·퇴실: 신뢰 +5. 닫고 재개방·퇴실: 배치 +15 한 번. 관찰 전 개폐: T3 0.
        // 2026-09-21 재설계: T3에 배치 Band2~Band4(48 이상) 자격이 새로 걸렸고 준수 신뢰가 +3 → +5가 됐다.
        // 그래서 아래 테스트들의 배치 기대값은 「자격 기준선 48 + 위반 델타」다.

        private RuleBook StartT3()
        {
            // 2026-09-21 재설계: T3 자격이 배치 Band2(48 이상)라 관찰로 시작시키기 전에 배치를 올려 둔다.
            Setup(FearAxis.Layout, 48);
            RuleBook book = Book("T3");
            book.Dispatch(T(SignalKind.ModelObserved, "scene.ta"));
            Assert.AreEqual(CardState.Active, book.Watchers[0].State);
            return book;
        }

        [Test]
        public void T3_관찰후_열린채퇴실은_배치15()
        {
            RuleBook book = StartT3();
            book.Dispatch(Sp(SignalKind.SpaceExited, SpaceId.Toilet));

            Assert.AreEqual(CardState.Violated, book.Watchers[0].State);
            AssertAllAxes(0, 0, 63, 0);   // 2026-09-21 재설계: 자격 기준선 48 + 위반 15
        }

        [Test]
        public void T3_닫기완료후_퇴실은_신뢰5()
        {
            RuleBook book = StartT3();
            book.Dispatch(Door(InnerStall, true));
            book.Dispatch(T(SignalKind.DoorCloseCompleted, InnerStall));
            Assert.AreEqual(CardState.Active, book.Watchers[0].State);
            book.Dispatch(Sp(SignalKind.SpaceExited, SpaceId.Toilet));

            Assert.AreEqual(CardState.Complied, book.Watchers[0].State);
            AssertAllAxes(0, 0, 48, 5);   // 2026-09-21 재설계: 배치는 자격 기준선 48 그대로, 준수 신뢰 +3 → +5
        }

        [Test]
        public void T3_닫고재개방후_퇴실은_배치15_한번()
        {
            RuleBook book = StartT3();
            book.Dispatch(Door(InnerStall, true));
            book.Dispatch(T(SignalKind.DoorCloseCompleted, InnerStall));
            book.Dispatch(Door(InnerStall, false));
            Assert.AreEqual(63, _axes.GetValue(FearAxis.Layout));   // 2026-09-21 재설계: 자격 기준선 48 + 위반 15

            book.Dispatch(Sp(SignalKind.SpaceExited, SpaceId.Toilet));

            AssertAllAxes(0, 0, 63, 0);
        }

        [Test]
        public void T3_관찰전개폐는_T3_0()
        {
            RuleBook book = Book("T3");
            book.Dispatch(Door(InnerStall, true));
            book.Dispatch(Door(InnerStall, false));
            book.EndNight();

            Assert.AreEqual(CardState.Undetermined, book.Watchers[0].State);
            AssertAllAxes(0, 0, 0, 0);
        }

        // 기획서: 문 닫힘 완료 전에 화장실을 나가면 위반.
        [Test]
        public void T3_닫기명령후_완료전퇴실은_배치15()
        {
            RuleBook book = StartT3();
            book.Dispatch(Door(InnerStall, true));
            book.Dispatch(Sp(SignalKind.SpaceExited, SpaceId.Toilet));

            AssertAllAxes(0, 0, 63, 0);   // 2026-09-21 재설계: 자격 기준선 48 + 위반 15
        }

        // 기획서: 시간 제한 없음 — 관찰·닫기 후 실내에 머무르면 아직 진행 중.
        [Test]
        public void T3_닫기후_실내체류는_진행중유지()
        {
            RuleBook book = StartT3();
            book.Dispatch(Door(InnerStall, true));
            book.Dispatch(T(SignalKind.DoorCloseCompleted, InnerStall));
            TestKit.Advance(book, 60f);

            Assert.AreEqual(CardState.Active, book.Watchers[0].State);
            AssertAllAxes(0, 0, 48, 0);   // 2026-09-21 재설계: 아직 정산 전 — 배치는 자격 기준선 48뿐
        }

        // 기획서: 관찰 전에 칸을 닫은 것은 위반이 아니다. 다시 열어 확인한 뒤부터 순서를 검사한다
        // (시작 전의 닫힘 완료는 세지 않는다).
        [Test]
        public void T3_관찰전에닫았다가_다시열고관찰하면_그때부터검사()
        {
            Setup(FearAxis.Layout, 48);   // 2026-09-21 재설계: T3 자격이 배치 Band2(48 이상)다
            RuleBook book = Book("T3");
            book.Dispatch(Door(InnerStall, true));
            book.Dispatch(T(SignalKind.DoorCloseCompleted, InnerStall));
            book.Dispatch(Door(InnerStall, false));
            book.Dispatch(T(SignalKind.ModelObserved, "scene.ta"));
            Assert.AreEqual(CardState.Active, book.Watchers[0].State);
            Assert.AreEqual(48, _axes.GetValue(FearAxis.Layout), "시작 전 개폐에는 델타가 없다");

            book.Dispatch(Sp(SignalKind.SpaceExited, SpaceId.Toilet));

            AssertAllAxes(0, 0, 63, 0);   // 자격 기준선 48 + 위반 15
        }

        // 공통 교차 「위반 중복」: 닫기 명령 → 완료 전 재개방 → 열린 채 퇴실 = 배치 +15 한 번.
        [Test]
        public void T3_닫기중재개방후_열린채퇴실도_배치15_한번()
        {
            RuleBook book = StartT3();
            book.Dispatch(Door(InnerStall, true));
            book.Dispatch(Door(InnerStall, false));
            book.Dispatch(Sp(SignalKind.SpaceExited, SpaceId.Toilet));
            book.EndNight();

            AssertAllAxes(0, 0, 63, 0);   // 2026-09-21 재설계: 자격 기준선 48 + 위반 15 한 번
        }

        // ───────── T4 ─────────
        // 기획서 검증: 호출 후 퇴실: 신뢰 +4. 세면대 접근: 청각 +12. Tab을 열지 않고 퇴실해도 준수. T-B 숨소리로 발동 없음.
        // 2026-09-21 재설계: 준수 신뢰 +2 → +4. 자격은 여전히 청각 Band2~Band4지만 하한이 50 → 48로 밀렸다.

        private RuleBook StartT4()
        {
            Setup(FearAxis.Auditory, 50);
            RuleBook book = Book("T4");
            book.Dispatch(T(SignalKind.ClueDelivered, SinkCall));
            Assert.AreEqual(CardState.Active, book.Watchers[0].State);
            return book;
        }

        [Test]
        public void T4_호출후_퇴실은_신뢰4()
        {
            RuleBook book = StartT4();
            book.Dispatch(JudgeSignal.Proximity(Sink, 2f));
            book.Dispatch(Sp(SignalKind.SpaceExited, SpaceId.Toilet));

            AssertAllAxes(50, 0, 0, 4);   // 2026-09-21 재설계: 준수 신뢰 +2 → +4
        }

        [Test]
        public void T4_세면대접근은_청각12()
        {
            RuleBook book = StartT4();
            book.Dispatch(JudgeSignal.Proximity(Sink, 1.4f));

            Assert.AreEqual(62, _axes.GetValue(FearAxis.Auditory));
        }

        [Test]
        public void T4_Tab을열지않고_퇴실해도_준수()
        {
            RuleBook book = StartT4();
            book.Dispatch(Sp(SignalKind.SpaceExited, SpaceId.Toilet));

            Assert.AreEqual(CardState.Complied, book.Watchers[0].State);
            Assert.AreEqual(4, _axes.GetValue(FearAxis.Trust));   // 2026-09-21 재설계: 준수 신뢰 +2 → +4
        }

        // T-B 숨소리는 효과음 — 호출 단서 신호가 아니다.
        [Test]
        public void T4_TB숨소리로는_발동없음()
        {
            Setup(FearAxis.Auditory, 50);
            RuleBook book = Book("T4");
            book.Dispatch(T(SignalKind.ModelObserved, "scene.tb"));
            book.Dispatch(JudgeSignal.Proximity(Sink, 0.5f));

            Assert.AreEqual(CardState.Waiting, book.Watchers[0].State);
            AssertAllAxes(50, 0, 0, 0);
        }

        // 2026-09-21 재설계: 구간 경계가 밀려 49는 이제 Band2(자격 안)다. 자격 직전 값은 47이다.
        [Test]
        public void T4_청각23에서는_시작하지않는다()
        {
            // 2026-09-22 자격 재설계: 자격이 Band2(48↑) → **Band1(24↑)**라 경계가 23/24다.
            Setup(FearAxis.Auditory, 23);
            RuleBook book = Book("T4");
            book.Dispatch(T(SignalKind.ClueDelivered, SinkCall));

            Assert.AreEqual(CardState.Waiting, book.Watchers[0].State);
        }

        // ───────── T5 ─────────
        // 기획서 검증: 열린 칸뿐인 상태: 미시작. 식별 후 문을 열고 On 유지: 조도 +12. Off 점검·퇴실: 신뢰 +4.
        // 2026-09-21 재설계: 준수 신뢰 +2 → +4. 조도 구간 자격은 없어졌다(아래 조도 50은 위반 누적 확인용 초기값).

        // 모든 칸이 열려 있으면 새는 빛 식별 대상이 없다(클라이언트 몫) → 시작 신호 없음.
        [Test]
        public void T5_열린칸뿐인상태는_미시작()
        {
            Setup(FearAxis.Illuminance, 50);
            RuleBook book = Book("T5");
            book.Dispatch(JudgeSignal.Flashlight(true));
            TestKit.Advance(book, 5f);

            Assert.AreEqual(CardState.Waiting, book.Watchers[0].State);
            Assert.AreEqual(50, _axes.GetValue(FearAxis.Illuminance));
        }

        [Test]
        public void T5_식별후_문을열고_On유지는_조도12()
        {
            Setup(FearAxis.Illuminance, 50);
            RuleBook book = Book("T5");
            book.Dispatch(JudgeSignal.Flashlight(true));
            book.Dispatch(T(SignalKind.ClueIdentified, StallLight));
            Assert.AreEqual(CardState.Active, book.Watchers[0].State);
            book.Dispatch(Door(InnerStall, false));
            TestKit.Advance(book, 2f);

            Assert.AreEqual(62, _axes.GetValue(FearAxis.Illuminance));
        }

        [Test]
        public void T5_유예내Off_점검퇴실은_신뢰4()
        {
            Setup(FearAxis.Illuminance, 50);
            RuleBook book = Book("T5");
            book.Dispatch(JudgeSignal.Flashlight(true));
            book.Dispatch(T(SignalKind.ClueIdentified, StallLight));
            TestKit.Advance(book, 1f);
            book.Dispatch(JudgeSignal.Flashlight(false));
            TestKit.Advance(book, 3f);
            book.Dispatch(Sp(SignalKind.InspectionCompleted, SpaceId.Toilet));
            book.Dispatch(Sp(SignalKind.SpaceExited, SpaceId.Toilet));

            AssertAllAxes(0, 50, 0, 4);   // 2026-09-21 재설계: 준수 신뢰 +2 → +4
        }

        // 기획서: 문 열기 자체는 T5 위반이 아니다. 빛 단서를 지워도 시작된 의무는 유지된다.
        [Test]
        public void T5_Off상태의_문열기는_위반아님_의무유지()
        {
            Setup(FearAxis.Illuminance, 50);
            RuleBook book = Book("T5");
            book.Dispatch(T(SignalKind.ClueIdentified, StallLight));
            book.Dispatch(Door(InnerStall, false));
            TestKit.Advance(book, 3f);

            Assert.AreEqual(CardState.Active, book.Watchers[0].State);
            Assert.AreEqual(50, _axes.GetValue(FearAxis.Illuminance));
        }

        // 해석(분석 문서 Q-T5-1): 점검 없이 퇴실하면 대기로 돌아간다.
        [Test]
        public void T5_점검없이퇴실은_대기()
        {
            Setup(FearAxis.Illuminance, 50);
            RuleBook book = Book("T5");
            book.Dispatch(Sp(SignalKind.SpaceEntered, SpaceId.Toilet));
            book.Dispatch(T(SignalKind.ClueIdentified, StallLight));
            TestKit.Advance(book, 3f);
            book.Dispatch(Sp(SignalKind.SpaceExited, SpaceId.Toilet));

            Assert.AreEqual(CardState.Waiting, book.Watchers[0].State);
            AssertAllAxes(0, 50, 0, 0);
        }

        // ───────── T6 ─────────
        // 기획서 검증: 문밖 관찰·점검: 밤 종료 신뢰 +4. 바깥 칸 내부 진입도 배치 +15. P4 안쪽 칸 도착: 같은 T6 한 번만 적용.
        // 2026-09-21: 준수 신뢰 +2 → +4. 자격으로 배치 Band3~Band4(72↑)가 걸렸다.
        // 2026-09-22 자격 재설계: **Band2~Band4(48↑)**로 한 칸 내렸다 — 5일차에만 나오던 카드였다(회차당 0.75회).
        // 아래 75는 여전히 자격 안이라 시나리오는 그대로 성립한다.

        private RuleBook StartT6()
        {
            Setup(FearAxis.Layout, 75);
            RuleBook book = Book("T6");
            book.Dispatch(T(SignalKind.ClueIdentified, BothOpen));
            Assert.AreEqual(CardState.Active, book.Watchers[0].State);
            return book;
        }

        [Test]
        public void T6_문밖관찰_점검은_밤종료에_신뢰4()
        {
            RuleBook book = StartT6();
            book.Dispatch(T(SignalKind.ZoneEntered, "toilet.stall.inner.front"));   // 칸 밖 관찰 지점 — 무관
            book.Dispatch(Door(InnerStall, true));                                   // 문 E 개폐 자체는 위반 아님
            book.Dispatch(Sp(SignalKind.InspectionCompleted, SpaceId.Toilet));
            book.Dispatch(Sp(SignalKind.SpaceExited, SpaceId.Toilet));
            Assert.AreEqual(0, _axes.GetValue(FearAxis.Trust), "준수는 밤 종료까지 보류");

            book.EndNight();

            Assert.AreEqual(CardState.Complied, book.Watchers[0].State);
            AssertAllAxes(0, 0, 75, 4);   // 2026-09-21 재설계: 준수 신뢰 +2 → +4
        }

        [Test]
        public void T6_바깥칸내부진입도_배치15()
        {
            RuleBook book = StartT6();
            book.Dispatch(T(SignalKind.ZoneEntered, OuterInside));

            Assert.AreEqual(90, _axes.GetValue(FearAxis.Layout));
        }

        [Test]
        public void T6_P4안쪽칸도착은_같은T6_한번만()
        {
            RuleBook book = StartT6();
            book.Dispatch(T(SignalKind.ZoneEntered, InnerInside));
            book.Dispatch(T(SignalKind.ZoneExited, InnerInside));
            book.Dispatch(T(SignalKind.ZoneEntered, InnerInside));
            book.EndNight();

            AssertAllAxes(0, 0, 90, 0);
        }

        // 기획서: 수치만으로는 시작하지 않는다.
        [Test]
        public void T6_수치만으로는_시작하지않는다()
        {
            Setup(FearAxis.Layout, 75);
            RuleBook book = Book("T6");
            book.Dispatch(T(SignalKind.ZoneEntered, InnerInside));
            book.EndNight();

            Assert.AreEqual(CardState.Undetermined, book.Watchers[0].State);
            AssertAllAxes(0, 0, 75, 0);
        }

        // 2026-09-21: 구간 검사를 되살렸다 — 두 칸 개방 식별만으로는 부족하고 배치 자격이 필요하다.
        // 2026-09-22 자격 재설계: 하한이 Band3(72) → Band2(48)라 경계가 47/48이다.
        [Test]
        public void T6_배치47에서는_시작하지않고_48에서_시작한다()
        {
            Setup(FearAxis.Layout, 47);
            RuleBook below = Book("T6");
            below.Dispatch(T(SignalKind.ClueIdentified, BothOpen));
            Assert.AreEqual(CardState.Waiting, below.Watchers[0].State, "배치 47은 Band1 — 자격 미달");

            Setup(FearAxis.Layout, 1);   // 47 + 1 = 48 → Band2
            RuleBook atBand2 = Book("T6");
            atBand2.Dispatch(T(SignalKind.ClueIdentified, BothOpen));

            Assert.AreEqual(CardState.Active, atBand2.Watchers[0].State, "배치 48은 Band2 — 자격 충족");
        }

        // 기획서: 재방문·문 개폐로 금지가 사라지지 않는다(준수 조건을 채운 뒤라도 진입하면 위반).
        [Test]
        public void T6_점검후_재방문에서_진입해도_배치15()
        {
            RuleBook book = StartT6();
            book.Dispatch(Sp(SignalKind.InspectionCompleted, SpaceId.Toilet));
            book.Dispatch(Sp(SignalKind.SpaceExited, SpaceId.Toilet));
            book.Dispatch(Sp(SignalKind.SpaceEntered, SpaceId.Toilet));
            book.Dispatch(Door(OuterStall, false));
            book.Dispatch(Door(OuterStall, true));
            book.Dispatch(T(SignalKind.ZoneEntered, OuterInside));
            book.EndNight();

            Assert.AreEqual(CardState.Violated, book.Watchers[0].State);
            AssertAllAxes(0, 0, 90, 0);
        }

        // 해석(분석 문서 Q-T6-2): 준수는 T6 시작 뒤의 공용부 점검만 센다 — 점검 없이 밤 종료면 미판정.
        [Test]
        public void T6_점검없이_밤종료는_미판정()
        {
            RuleBook book = StartT6();
            book.EndNight();

            Assert.AreEqual(CardState.Undetermined, book.Watchers[0].State);
            AssertAllAxes(0, 0, 75, 0);
        }
    }
}

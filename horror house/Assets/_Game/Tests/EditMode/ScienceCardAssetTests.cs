using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;

namespace NightDuty.Tests
{
    /// <summary>
    /// 실제 과학실 카드 에셋(S1~S6)을 기획서 각 카드의 「검증 절차·기대 결과」와 분석 문서의 추가 시나리오로 검사한다.
    /// 에셋이 없으면 NightDuty ▸ 과학실 카드 에셋 생성 (S1~S6) 메뉴로 만든다.
    /// </summary>
    public sealed class ScienceCardAssetTests
    {
        private const string Folder = "Assets/_Game/ScriptableObjects/Rules/Science/";
        private const string CorridorFolder = "Assets/_Game/ScriptableObjects/Rules/Corridor/";
        private const string Passage = "corridor.passage";

        private const string Face = "science.model.sa.face";
        private const string Shelf = "science.shelf.sa";
        private const string GlassBreak = "science.glass.break";
        private const string Bench = "science.bench.glass";
        private const string Clink = "science.bench.glass.clink";
        private const string LastLight = "science.light.last";
        private const string ModelSB = "science.model.sb";
        private const string GlassZone = "science.zone.glass";

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
            Assert.IsNotNull(card, id + " 에셋이 없습니다. NightDuty ▸ 과학실 카드 에셋 생성 (S1~S6) 메뉴를 실행하세요.");
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
            string[] ids = { "S1", "S2", "S3", "S4", "S5", "S6" };
            List<string> errors = new List<string>();
            for (int i = 0; i < ids.Length; i++)
            {
                RuleSO card = Load(ids[i]);
                Assert.AreEqual(ids[i], card.CardId);
                Assert.AreEqual(SpaceId.ScienceRoom, card.Space);
                card.Validate(errors);
            }

            CollectionAssert.IsEmpty(errors);

            // S1은 준수 전용 카드(위반 없음 · 미관찰은 업무 미완료).
            RuleSO s1 = Load("S1");
            Assert.IsNull(s1.FailureCondition);
            Assert.AreEqual(0, s1.FailureDelta);
            // 2026-09-21 재설계: 준수 신뢰 델타가 +3 → +5로 올랐다(S1·S5·C5·C6·T2·T3).
            Assert.AreEqual(5, s1.SuccessDelta);
            Assert.IsTrue(Load("S2").IsLongTerm, "S2는 장기(밤 종료까지 재진입 금지)");
        }

        // ───────── S1 ─────────
        // 기획서 검증: 얼굴 0.9초 후 시선 이탈: 미완료. 연속 1초: 신뢰 +5. 재방문 재응시: 0. 첫날 다른 방 선방문: S1 벌점 없음.

        private RuleBook EnterScienceWithS1()
        {
            RuleBook book = Book("S1");
            book.Dispatch(Sp(SignalKind.SpaceEntered, SpaceId.ScienceRoom));
            Assert.AreEqual(CardState.Active, book.Watchers[0].State);
            return book;
        }

        [Test]
        public void S1_얼굴0초9후_시선이탈은_미완료()
        {
            RuleBook book = EnterScienceWithS1();
            TestKit.Advance(book, 0.9f, Face);
            TestKit.Advance(book, 0.1f);
            TestKit.Advance(book, 0.9f, Face);   // 끊긴 뒤 다시 0.9초 — 누적되지 않음

            Assert.AreEqual(CardState.Active, book.Watchers[0].State);
            AssertAllAxes(0, 0, 0, 0);
        }

        [Test]
        public void S1_연속1초는_신뢰5()
        {
            RuleBook book = EnterScienceWithS1();
            TestKit.Advance(book, 1f, Face);

            Assert.AreEqual(CardState.Complied, book.Watchers[0].State);
            // 2026-09-21 재설계: 준수 신뢰 +3 → +5.
            AssertAllAxes(0, 0, 0, 5);
        }

        [Test]
        public void S1_재방문_재응시는_0()
        {
            RuleBook book = EnterScienceWithS1();
            TestKit.Advance(book, 1f, Face);
            book.Dispatch(Sp(SignalKind.SpaceExited, SpaceId.ScienceRoom));
            book.Dispatch(Sp(SignalKind.SpaceEntered, SpaceId.ScienceRoom));
            TestKit.Advance(book, 1f, Face);

            // 2026-09-21 재설계: 준수 신뢰 +3 → +5. 재응시 몫은 여전히 0이라 합계도 5뿐이다.
            AssertAllAxes(0, 0, 0, 5);
        }

        [Test]
        public void S1_첫날_다른방선방문은_벌점없음()
        {
            RuleBook book = Book("S1");
            book.Dispatch(Sp(SignalKind.SpaceEntered, SpaceId.Toilet));
            TestKit.Advance(book, 3f);
            book.Dispatch(Sp(SignalKind.SpaceExited, SpaceId.Toilet));

            Assert.AreEqual(CardState.Waiting, book.Watchers[0].State);
            AssertAllAxes(0, 0, 0, 0);
        }

        // 기획서: 보관 위치 바닥만 보는 것은 완료가 아니다.
        [Test]
        public void S1_바닥만보면_미완료()
        {
            RuleBook book = EnterScienceWithS1();
            TestKit.Advance(book, 2f, Shelf);

            Assert.AreEqual(CardState.Active, book.Watchers[0].State);
            AssertAllAxes(0, 0, 0, 0);
        }

        // 미관찰 퇴실은 벌점 없이 대기 → 재방문에 다시 시작하며 응시 시간은 처음부터 다시 센다.
        [Test]
        public void S1_미관찰퇴실은_대기_재방문에_다시시작()
        {
            RuleBook book = EnterScienceWithS1();
            TestKit.Advance(book, 0.5f, Face);
            book.Dispatch(Sp(SignalKind.SpaceExited, SpaceId.ScienceRoom));
            Assert.AreEqual(CardState.Waiting, book.Watchers[0].State);
            AssertAllAxes(0, 0, 0, 0);

            book.Dispatch(Sp(SignalKind.SpaceEntered, SpaceId.ScienceRoom));
            Assert.AreEqual(CardState.Active, book.Watchers[0].State);
            TestKit.Advance(book, 0.5f, Face);
            Assert.AreEqual(CardState.Active, book.Watchers[0].State, "이전 방문의 0.5초는 이어지지 않는다");
            TestKit.Advance(book, 0.5f, Face);

            // 2026-09-21 재설계: 준수 신뢰 +3 → +5.
            AssertAllAxes(0, 0, 0, 5);
        }

        // 기획서: 미관찰은 업무 미완료 — 벌점 없음(밤 종료에 미판정, 델타 0).
        [Test]
        public void S1_미관찰로_밤종료는_미판정_델타0()
        {
            RuleBook book = EnterScienceWithS1();
            TestKit.Advance(book, 0.5f, Face);
            book.EndNight();

            Assert.AreEqual(CardState.Undetermined, book.Watchers[0].State);
            AssertAllAxes(0, 0, 0, 0);
        }

        // 기획서(S6): S1 최초 관찰 전 방문은 제외 — S1이 그 방문의 신규 단기 사건 몫을 쥔다.
        [Test]
        public void S1_관찰전방문에서는_S6가_시작하지않는다()
        {
            RuleBook book = Book("S1", "S6");
            book.Dispatch(Sp(SignalKind.SpaceEntered, SpaceId.ScienceRoom));
            book.Dispatch(T(SignalKind.ZoneEntered, GlassZone));
            book.Dispatch(JudgeSignal.Flashlight(true));
            TestKit.Advance(book, 3f);

            Assert.AreEqual(CardState.Active, book.Watchers[0].State, "S1");
            Assert.AreEqual(CardState.Waiting, book.Watchers[1].State, "S6");
            Assert.AreEqual(0, _axes.GetValue(FearAxis.Illuminance));
        }

        // ───────── S2 ─────────
        // 기획서 검증: 문 E 개방만: 0. 실내 진입: 청각 +12. P1 목적지 도착 전 문턱만 넘기: S2 위반.

        private RuleBook StartS2(params string[] deck)
        {
            RuleBook book = Book(deck.Length > 0 ? deck : new[] { "S2" });
            return book;
        }

        [Test]
        public void S2_문E개방만은_0_밤종료에_신뢰4()
        {
            RuleBook book = StartS2();
            book.Dispatch(T(SignalKind.ClueDelivered, GlassBreak));
            Assert.AreEqual(CardState.Active, book.Watchers[0].State);
            book.Dispatch(JudgeSignal.DoorCommand("science.door", false, ActionSource.Player));
            Assert.AreEqual(0, _axes.GetValue(FearAxis.Trust), "준수는 밤 종료에만");

            book.EndNight();   // 단서 뒤 Tick이 없어도 밤 종료 신호로 준수

            Assert.AreEqual(CardState.Complied, book.Watchers[0].State);
            // 2026-09-21 재설계: 준수 신뢰 +2 → +4.
            AssertAllAxes(0, 0, 0, 4);
        }

        [Test]
        public void S2_실내진입은_청각12_밤종료에_신뢰0()
        {
            RuleBook book = StartS2();
            book.Dispatch(T(SignalKind.ClueDelivered, GlassBreak));
            book.Dispatch(Sp(SignalKind.SpaceEntered, SpaceId.ScienceRoom));
            Assert.AreEqual(12, _axes.GetValue(FearAxis.Auditory));

            book.EndNight();

            Assert.AreEqual(CardState.Violated, book.Watchers[0].State);
            AssertAllAxes(12, 0, 0, 0);
        }

        [Test]
        public void S2_P1도착전_문턱만넘기도_위반()
        {
            RuleBook book = StartS2();
            book.Dispatch(T(SignalKind.ClueDelivered, GlassBreak));
            book.Dispatch(Sp(SignalKind.SpaceEntered, SpaceId.ScienceRoom));
            book.Dispatch(Sp(SignalKind.SpaceExited, SpaceId.ScienceRoom));

            Assert.AreEqual(CardState.Violated, book.Watchers[0].State);
            Assert.AreEqual(12, _axes.GetValue(FearAxis.Auditory));
        }

        // 기획서: 소리가 멎거나 다른 방을 방문해도 의무는 지속된다.
        [Test]
        public void S2_소리가멎고_다른방을다녀와도_의무지속()
        {
            RuleBook book = StartS2();
            book.Dispatch(T(SignalKind.ClueDelivered, GlassBreak));
            TestKit.Advance(book, 10f);
            book.Dispatch(Sp(SignalKind.SpaceEntered, SpaceId.Toilet));
            book.Dispatch(Sp(SignalKind.InspectionCompleted, SpaceId.Toilet));
            book.Dispatch(Sp(SignalKind.SpaceExited, SpaceId.Toilet));
            Assert.AreEqual(CardState.Active, book.Watchers[0].State);

            book.Dispatch(Sp(SignalKind.SpaceEntered, SpaceId.ScienceRoom));

            Assert.AreEqual(12, _axes.GetValue(FearAxis.Auditory));
        }

        // 기획서: 재진입 위반은 즉시 한 번.
        [Test]
        public void S2_재진입위반은_한번()
        {
            RuleBook book = StartS2();
            book.Dispatch(T(SignalKind.ClueDelivered, GlassBreak));
            book.Dispatch(Sp(SignalKind.SpaceEntered, SpaceId.ScienceRoom));
            book.Dispatch(Sp(SignalKind.SpaceExited, SpaceId.ScienceRoom));
            book.Dispatch(Sp(SignalKind.SpaceEntered, SpaceId.ScienceRoom));
            book.EndNight();

            AssertAllAxes(12, 0, 0, 0);
        }

        // 장기 카드는 방문 몫을 쓰지 않는다 — 같은 방문에 단기(H3)가 먼저 시작돼도 S2는 시작한다.
        [Test]
        public void S2_장기라_단기사건이있는방문에도_시작한다()
        {
            RuleBook book = StartS2("H3", "S2");
            book.Dispatch(Sp(SignalKind.SpaceEntered, SpaceId.Corridor));
            book.Dispatch(T(SignalKind.ZoneEntered, Passage));
            book.Dispatch(T(SignalKind.ClueDelivered, GlassBreak));

            Assert.AreEqual(CardState.Active, book.Watchers[0].State, "H3");
            Assert.AreEqual(CardState.Active, book.Watchers[1].State, "S2");
        }

        // ───────── S3 ─────────
        // 기획서 검증: 소리만 나고 간격 단서가 가려짐: 미시작. 두 단서 식별 후 접근: 청각 +12. 다른 실험대 접근: 추가 벌점 없음.

        private RuleBook StartS3()
        {
            Setup(FearAxis.Auditory, 25);
            RuleBook book = Book("S3");
            book.Dispatch(T(SignalKind.ClueIdentified, Bench));
            Assert.AreEqual(CardState.Waiting, book.Watchers[0].State, "식별만으로는 시작하지 않는다");
            book.Dispatch(T(SignalKind.ClueDelivered, Clink));
            Assert.AreEqual(CardState.Active, book.Watchers[0].State);
            return book;
        }

        // 간격 단서가 가려지면 재생기가 접촉음 단서를 전달하지 않는다(클라이언트 몫) → 코어에는 시작 신호가 없다.
        [Test]
        public void S3_소리만나고_간격단서가려짐은_미시작()
        {
            Setup(FearAxis.Auditory, 25);
            RuleBook book = Book("S3");
            book.Dispatch(JudgeSignal.Proximity(Bench, 0.5f));
            book.EndNight();

            Assert.AreEqual(CardState.Undetermined, book.Watchers[0].State);
            AssertAllAxes(25, 0, 0, 0);
        }

        [Test]
        public void S3_두단서식별후_접근은_청각12()
        {
            RuleBook book = StartS3();
            book.Dispatch(JudgeSignal.Proximity(Bench, 1.4f));

            Assert.AreEqual(37, _axes.GetValue(FearAxis.Auditory));
        }

        [Test]
        public void S3_다른실험대접근은_추가벌점없음_점검완료로_신뢰4()
        {
            RuleBook book = StartS3();
            book.Dispatch(JudgeSignal.Proximity("science.bench.b", 0.3f));
            book.Dispatch(Sp(SignalKind.InspectionCompleted, SpaceId.ScienceRoom));

            // 2026-09-21 재설계: 준수 신뢰 +2 → +4.
            AssertAllAxes(25, 0, 0, 4);
        }

        // 기획서: 단서 없는 첫 방문은 미판정이며 다른 유효 방문에 발생 가능.
        [Test]
        public void S3_단서없는첫방문뒤_다음방문에_발생가능()
        {
            Setup(FearAxis.Auditory, 25);
            RuleBook book = Book("S3");
            book.Dispatch(Sp(SignalKind.SpaceEntered, SpaceId.ScienceRoom));
            book.Dispatch(Sp(SignalKind.InspectionCompleted, SpaceId.ScienceRoom));
            book.Dispatch(Sp(SignalKind.SpaceExited, SpaceId.ScienceRoom));
            Assert.AreEqual(CardState.Waiting, book.Watchers[0].State);

            book.Dispatch(Sp(SignalKind.SpaceEntered, SpaceId.ScienceRoom));
            book.Dispatch(T(SignalKind.ClueIdentified, Bench));
            book.Dispatch(T(SignalKind.ClueDelivered, Clink));
            book.Dispatch(Sp(SignalKind.InspectionCompleted, SpaceId.ScienceRoom));
            book.Dispatch(Sp(SignalKind.SpaceExited, SpaceId.ScienceRoom));

            // 2026-09-21 재설계: 준수 신뢰 +2 → +4.
            AssertAllAxes(25, 0, 0, 4);
        }

        // 2026-09-21 재설계: 구간 경계가 24의 배수로 바뀌어 24는 이제 Band1(자격 안)이다.
        // S3는 청각 Band1~Band4 자격이므로 자격 직전 값은 Band0 최댓값 23이다.
        [Test]
        public void S3_청각0에서도_시작한다()
        {
            // 2026-09-22 자격 재설계: S3 자격이 Band1(24↑) → **Band0**다. 청각 Band0 풀이
            // 3장(C1·S2·T2)뿐이라 1·2일차 청각 카드가 사실상 고정이었다. 네 장이 되면 그 자리가 갈린다.
            Setup(FearAxis.Auditory, 0);
            RuleBook book = Book("S3");
            book.Dispatch(T(SignalKind.ClueDelivered, Clink));

            Assert.AreEqual(CardState.Active, book.Watchers[0].State);
        }

        // 해석(분석 문서 Q-S3-1): 점검 없이 퇴실하면 대기로 돌아간다.
        [Test]
        public void S3_점검없이퇴실은_대기()
        {
            RuleBook book = StartS3();
            book.Dispatch(Sp(SignalKind.SpaceExited, SpaceId.ScienceRoom));

            Assert.AreEqual(CardState.Waiting, book.Watchers[0].State);
            AssertAllAxes(25, 0, 0, 0);
        }

        // ───────── S4 ─────────
        // 기획서 검증: 식별 뒤 1.9초 응시·시선 이탈: 위반 없음. 2초: 조도 +12. 처음부터 등 0개: 미발동.

        private RuleBook StartS4()
        {
            Setup(FearAxis.Illuminance, 75);
            RuleBook book = Book("S4");
            book.Dispatch(T(SignalKind.ClueIdentified, LastLight));
            Assert.AreEqual(CardState.Active, book.Watchers[0].State);
            return book;
        }

        [Test]
        public void S4_식별뒤_1초9응시후_시선이탈은_위반없음()
        {
            RuleBook book = StartS4();
            TestKit.Advance(book, 1.9f, LastLight);
            TestKit.Advance(book, 0.1f);

            Assert.AreEqual(CardState.Active, book.Watchers[0].State);
            Assert.AreEqual(75, _axes.GetValue(FearAxis.Illuminance));
        }

        [Test]
        public void S4_2초응시는_조도12()
        {
            RuleBook book = StartS4();
            TestKit.Advance(book, 2f, LastLight);

            Assert.AreEqual(87, _axes.GetValue(FearAxis.Illuminance));
        }

        // 2026-09-21: S4는 조도 Band3 전용(72~89) 창형이었다 — 18칸.
        // 2026-09-22 자격 재설계: **Band2~Band4(48~99)**로 넓혔다. 조도 카드가 다섯 장뿐인데
        // 그중 하나가 18칸 창에 갇혀 있어 실질 풀이 네 장이었고, 5일차에만 98% 확률로 나왔다.
        // 이제 밖은 아래쪽 47(Band1 최댓값) 하나다.
        [TestCase(47)]
        public void S4_자격구간밖에서는_미발동(int illuminance)
        {
            Setup(FearAxis.Illuminance, illuminance);
            RuleBook book = Book("S4");
            book.Dispatch(T(SignalKind.ClueIdentified, LastLight));

            Assert.AreEqual(CardState.Waiting, book.Watchers[0].State);
        }

        /// <summary>조도 90~99에서도 열린다 — 등이 0개라 대상이 없을 뿐, 자격으로 막지 않는다.</summary>
        [Test]
        public void S4_조도90에서도_자격은_통과한다()
        {
            Setup(FearAxis.Illuminance, 90);
            RuleBook book = Book("S4");
            book.Dispatch(T(SignalKind.ClueIdentified, LastLight));

            Assert.AreEqual(CardState.Active, book.Watchers[0].State);
        }

        // 기획서: 연속 시선이 끊기면 응시 시간만 0으로 초기화한다.
        [Test]
        public void S4_끊겼다다시봐도_누적되지않는다()
        {
            RuleBook book = StartS4();
            TestKit.Advance(book, 1.5f, LastLight);
            TestKit.Advance(book, 0.1f);
            TestKit.Advance(book, 1.5f, LastLight);

            Assert.AreEqual(CardState.Active, book.Watchers[0].State);
            Assert.AreEqual(75, _axes.GetValue(FearAxis.Illuminance));
        }

        [Test]
        public void S4_금지응시없이_점검퇴실은_신뢰4()
        {
            RuleBook book = StartS4();
            TestKit.Advance(book, 3f);
            book.Dispatch(Sp(SignalKind.InspectionCompleted, SpaceId.ScienceRoom));
            book.Dispatch(Sp(SignalKind.SpaceExited, SpaceId.ScienceRoom));

            // 2026-09-21 재설계: 준수 신뢰 +2 → +4.
            AssertAllAxes(0, 75, 0, 4);
        }

        // 해석(분석 문서 Q-S4-1): 점검 없이 나가면 대기로 돌아간다(다음 방문에 다시 식별 필요).
        [Test]
        public void S4_점검없이퇴실은_대기()
        {
            RuleBook book = StartS4();
            book.Dispatch(Sp(SignalKind.SpaceExited, SpaceId.ScienceRoom));

            Assert.AreEqual(CardState.Waiting, book.Watchers[0].State);
            AssertAllAxes(0, 75, 0, 0);
        }

        // ───────── S5 ─────────
        // 기획서 검증: 빈 보관 위치 관찰: 미시작. 문 옆 S-B 관찰 후 거리 유지·퇴실: 신뢰 +5. P1 중 근접: S5 0.
        // 2026-09-21: S5에 배치 Band1~Band4(24 이상) 자격이 걸렸다.
        // 2026-09-22 자격 재설계: **Band0~Band4**로 되돌렸다. 이 카드의 트리거 ID가 조우 장면 scene.sb라,
        // 자격이 24↑면 S-B 장면을 3일차 이전에 깔 수 없었다(기획서 v6 §4와 충돌).
        // 아래 시나리오는 배치 24를 그대로 둔다 — 자격 안이므로 결과가 달라지지 않고,
        // 「자격용으로 올린 값이 끝까지 남는다」는 단언도 그대로 성립한다.

        [Test]
        public void S5_빈보관위치관찰은_미시작()
        {
            Setup(FearAxis.Layout, 24);
            RuleBook book = Book("S5");
            book.Dispatch(T(SignalKind.ClueIdentified, Shelf));

            Assert.AreEqual(CardState.Waiting, book.Watchers[0].State);
        }

        [Test]
        public void S5_SB관찰후_거리유지_퇴실은_신뢰5()
        {
            Setup(FearAxis.Layout, 24);
            RuleBook book = Book("S5");
            book.Dispatch(T(SignalKind.ModelObserved, "scene.sb"));
            Assert.AreEqual(CardState.Active, book.Watchers[0].State);
            book.Dispatch(JudgeSignal.Proximity(ModelSB, 1.5f));
            book.Dispatch(Sp(SignalKind.SpaceExited, SpaceId.ScienceRoom));

            // 2026-09-21 재설계: 준수 신뢰 +3 → +5. 배치 24는 자격을 열려고 미리 올린 값이라 그대로 남는다.
            AssertAllAxes(0, 0, 24, 5);
        }

        // P1 경유 S-B는 장면 ID를 scene.sb.p1로 따로 보낸다(연결 약속 보완).
        [Test]
        public void S5_P1중_근접은_S5_0()
        {
            Setup(FearAxis.Layout, 24);
            RuleBook book = Book("S5");
            book.Dispatch(T(SignalKind.ModelObserved, "scene.sb.p1"));
            book.Dispatch(JudgeSignal.Proximity(ModelSB, 0.5f));

            Assert.AreEqual(CardState.Waiting, book.Watchers[0].State);
            // 배치 24는 자격을 열려고 미리 올린 값이다 — 시작하지 않았으니 여기서 더 오르지 않는다.
            AssertAllAxes(0, 0, 24, 0);
        }

        [Test]
        public void S5_관찰후_1m4는_배치15_이후퇴실해도_신뢰0()
        {
            Setup(FearAxis.Layout, 24);
            RuleBook book = Book("S5");
            book.Dispatch(T(SignalKind.ModelObserved, "scene.sb"));
            book.Dispatch(JudgeSignal.Proximity(ModelSB, 1.4f));
            book.Dispatch(Sp(SignalKind.SpaceExited, SpaceId.ScienceRoom));

            Assert.AreEqual(CardState.Violated, book.Watchers[0].State);
            // 2026-09-21 재설계: 위반 델타는 그대로 +15지만, 자격용 배치 24 위에 얹혀 24 + 15 = 39가 된다.
            AssertAllAxes(0, 0, 39, 0);
        }

        // 기획서: 모형을 먼저 발견해도 유효하다.
        [Test]
        public void S5_보관위치확인전_모형먼저발견도_유효()
        {
            Setup(FearAxis.Layout, 24);
            RuleBook book = Book("S5");
            book.Dispatch(Sp(SignalKind.SpaceEntered, SpaceId.ScienceRoom));
            book.Dispatch(T(SignalKind.ModelObserved, "scene.sb"));
            book.Dispatch(T(SignalKind.ClueIdentified, Shelf));

            Assert.AreEqual(CardState.Active, book.Watchers[0].State);
        }

        // ───────── S6 ─────────
        // 기획서 검증: 유예 내 Off·1초 점검·구역 이탈: 신뢰 +4. 과학실 다른 구역에서 On: 위반 없음. S-B 방문: 미시작.

        [Test]
        public void S6_유예내Off_점검후_구역이탈은_신뢰4()
        {
            RuleBook book = Book("S6");
            book.Dispatch(JudgeSignal.Flashlight(true));
            book.Dispatch(T(SignalKind.ZoneEntered, GlassZone));
            Assert.AreEqual(CardState.Active, book.Watchers[0].State);
            TestKit.Advance(book, 1f);
            book.Dispatch(JudgeSignal.Flashlight(false));
            TestKit.Advance(book, 2f);
            book.Dispatch(T(SignalKind.ZoneExited, GlassZone));

            Assert.AreEqual(CardState.Complied, book.Watchers[0].State);
            // 2026-09-21 재설계: 준수 신뢰 +2 → +4.
            AssertAllAxes(0, 0, 0, 4);
        }

        [Test]
        public void S6_과학실다른구역에서On은_위반없음()
        {
            RuleBook book = Book("S6");
            book.Dispatch(JudgeSignal.Flashlight(true));
            book.Dispatch(T(SignalKind.ZoneEntered, "science.zone.bench"));
            TestKit.Advance(book, 5f);

            Assert.AreEqual(CardState.Waiting, book.Watchers[0].State);
            AssertAllAxes(0, 0, 0, 0);
        }

        // S-B 방문: S5가 방문 몫을 쥐어 S6는 시작하지 않는다(S5 미배정 날은 클라이언트가 구역 신호를 억제).
        [Test]
        public void S6_SB방문에서는_미시작()
        {
            // 2026-09-21 재설계: S5는 배치 Band1~ 자격이라, 배치를 24로 올려야 방문 몫을 쥔다.
            Setup(FearAxis.Layout, 24);
            RuleBook book = Book("S5", "S6");
            book.Dispatch(Sp(SignalKind.SpaceEntered, SpaceId.ScienceRoom));
            book.Dispatch(T(SignalKind.ModelObserved, "scene.sb"));
            book.Dispatch(T(SignalKind.ZoneEntered, GlassZone));

            Assert.AreEqual(CardState.Active, book.Watchers[0].State, "S5");
            Assert.AreEqual(CardState.Waiting, book.Watchers[1].State, "S6");
        }

        // 기획서: 유예 종료에도 On이거나, 이후 나가기 전 On으로 바꾸면 조도 +12(한 번).
        [Test]
        public void S6_유예뒤On은_조도12_한번()
        {
            RuleBook book = Book("S6");
            book.Dispatch(JudgeSignal.Flashlight(true));
            book.Dispatch(T(SignalKind.ZoneEntered, GlassZone));
            TestKit.Advance(book, 2f);
            Assert.AreEqual(12, _axes.GetValue(FearAxis.Illuminance));

            book.Dispatch(JudgeSignal.Flashlight(false));
            book.Dispatch(JudgeSignal.Flashlight(true));
            book.Dispatch(T(SignalKind.ZoneExited, GlassZone));

            AssertAllAxes(0, 12, 0, 0);
        }

        [Test]
        public void S6_Off로들어와_유예뒤On전환은_조도12()
        {
            RuleBook book = Book("S6");
            book.Dispatch(T(SignalKind.ZoneEntered, GlassZone));
            TestKit.Advance(book, 3f);
            book.Dispatch(JudgeSignal.Flashlight(true));

            Assert.AreEqual(CardState.Violated, book.Watchers[0].State);
            Assert.AreEqual(12, _axes.GetValue(FearAxis.Illuminance));
        }

        // 기획서: 점검 없이 유예 안에 되돌아가면 미판정, 다음 유효 진입에 다시 시작 가능.
        [Test]
        public void S6_점검없이되돌아가면_대기_같은방문재진입에_다시시작()
        {
            RuleBook book = Book("S6");
            book.Dispatch(Sp(SignalKind.SpaceEntered, SpaceId.ScienceRoom));
            book.Dispatch(T(SignalKind.ZoneEntered, GlassZone));
            TestKit.Advance(book, 1f);
            book.Dispatch(T(SignalKind.ZoneExited, GlassZone));
            Assert.AreEqual(CardState.Waiting, book.Watchers[0].State);

            book.Dispatch(T(SignalKind.ZoneEntered, GlassZone));
            Assert.AreEqual(CardState.Active, book.Watchers[0].State, "취소된 시도는 방문 몫을 돌려준다");
            TestKit.Advance(book, 3f);
            book.Dispatch(T(SignalKind.ZoneExited, GlassZone));

            // 2026-09-21 재설계: 준수 신뢰 +2 → +4.
            AssertAllAxes(0, 0, 0, 4);
        }

        // 체류 3초(유예 2초 + 점검 1초) 전에 나가면 On이었어도 위반·준수 모두 없다(대기).
        [Test]
        public void S6_On으로들어와_1초5뒤이탈은_준수없음_대기()
        {
            RuleBook book = Book("S6");
            book.Dispatch(JudgeSignal.Flashlight(true));
            book.Dispatch(T(SignalKind.ZoneEntered, GlassZone));
            TestKit.Advance(book, 1.5f);
            book.Dispatch(T(SignalKind.ZoneExited, GlassZone));

            Assert.AreEqual(CardState.Waiting, book.Watchers[0].State);
            AssertAllAxes(0, 0, 0, 0);
        }
    }
}

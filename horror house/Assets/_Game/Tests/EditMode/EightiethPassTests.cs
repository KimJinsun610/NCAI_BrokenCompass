using NUnit.Framework;
using UnityEngine;

namespace NightDuty.Tests
{
    /// <summary>
    /// 80차(2026-10-10) — 민: 놀람 연출(인체 모형 응시 = 사망 대신 놀람) · 안전한 읽기 보상 = 빈칸 해독 · W16·W17 출입 카드 · 새 수칙 L7(뒷걸음질) ·
    /// L-4 칠판 폐기 · 가짜 수칙 4 추가.
    /// </summary>
    public sealed class EightiethPassTests
    {
        [Test]
        public void 빈칸_해독_문구()
        {
            Assert.AreEqual("교실의 소녀는 무시하십시오.", BlankDecoding.Decode("C2", ProgramCatalog.Rule("C2").Text));
            Assert.AreEqual("복도 끝에 선임이 서 있으면 빛을 끄고 기다리십시오.", BlankDecoding.Decode("S5", ProgramCatalog.Rule("S5").Text));
            Assert.AreEqual("도서관 담당자를 계속 비추십시오.", BlankDecoding.Decode("L3", ProgramCatalog.Rule("L3").Text));
            Assert.AreEqual("화면 속 사망자가 지나갈 때까지 CCTV에서 눈을 떼지 마십시오.",BlankDecoding.Decode("K1", ProgramCatalog.Rule("K1").Text));

            // 변조본에도 같이 적용된다.
            Assert.AreEqual("교실 의 소녀는 무시 대상 입니 다.", BlankDecoding.Decode("C2", ParadoxCatalog.Find("C2").Variant));
            Assert.AreEqual("도서관 담당자를 계속 비추 십니까? 예.", BlankDecoding.Decode("L3", ParadoxCatalog.Find("L3").Variant));
            StringAssert.Contains("선임이", BlankDecoding.Decode("S5", ParadoxCatalog.Find("S5").Variant));

            Assert.IsFalse(BlankDecoding.HasBlank("G1"));
            Assert.AreEqual("그대로", BlankDecoding.Decode("G1", "그대로"));
            CollectionAssert.AreEqual(new[] { "C2", "S5", "L3", "K1" }, new System.Collections.Generic.List<string>(BlankDecoding.RuleIds));
            Assert.IsFalse(NightRun.IsBlankDecoded("C2"), "해독 전에는 원문");
            Assert.AreEqual(ProgramCatalog.Rule("C2").Text, NightRun.ShownRuleText("C2", ProgramCatalog.Rule("C2").Text));
        }

        [Test]
        public void 출입_카드_지시는_집고_열쇠함에_넣으면_완료()
        {
            DutyDef w16 = DutyCatalog.Find("W16");
            DutyDef w17 = DutyCatalog.Find("W17");
            Assert.AreEqual("과학실에 두고 간 출입 카드를 경비실 열쇠함에 넣어주십시오.", w16.Order);
            Assert.AreEqual("교실 교탁 서랍 출입 카드를 경비실 열쇠함에 넣어주십시오.", w17.Order);   // 103차: 열쇠함 명시
            Assert.AreEqual(DutyKind.CardDelivery, w16.Kind);
            Assert.AreEqual(DutyCatalog.ScienceCardTarget, w16.Target);
            Assert.AreEqual(DutyCatalog.ClassroomCardTarget, w17.Target);

            DutyDispatcher d = new DutyDispatcher(1, s => 0f, 5);
            DutyEvent ev;
            Assert.IsFalse(d.WantsCard(DutyCatalog.ScienceCardTarget), "지시 전");
            Assert.IsTrue(d.Force("W16", out ev));
            Assert.IsTrue(d.WantsCard(DutyCatalog.ScienceCardTarget));
            Assert.IsFalse(d.WantsCard(DutyCatalog.ClassroomCardTarget), "다른 카드");
            Assert.IsFalse(d.NoteCardDelivered(out ev), "들지 않고는 넣을 수 없다");
            Assert.IsNull(d.CarriedCard);

            Assert.IsTrue(d.NoteCardTaken(DutyCatalog.ScienceCardTarget));
            Assert.AreEqual(DutyCatalog.ScienceCardTarget, d.CarriedCard);
            Assert.IsFalse(d.WantsCard(DutyCatalog.ScienceCardTarget), "이미 들었다");
            Assert.IsNotNull(d.Active, "집기만으로는 끝나지 않는다");

            Assert.IsTrue(d.NoteCardDelivered(out ev));
            Assert.AreEqual(DutyOutcome.Done, ev.Outcome);
            Assert.AreEqual("W16", ev.Def.Id);
            Assert.IsNull(d.CarriedCard);
            Assert.IsTrue(d.Finished("W16"));
        }

        [Test]
        public void L7_도서관에서_1초_넘게_뒷걸음질하면_위반()
        {
            Assert.AreEqual("도서관에서는 뒷걸음질하지 마십시오.", ProgramCatalog.Rule("L7").Text);
            Assert.AreEqual(SpaceId.Library, ProgramCatalog.Rule("L7").Space);
            Assert.IsFalse(ParadoxCatalog.Find("L7") != null && ParadoxCatalog.Find("L7").HasMessage, "역설은 채택하지 않음");

            // 바라보는 쪽 = +z(방향각 0). 뒤 = −z.
            FinalFixture f = new FinalFixture("L7");
            float z = 0f;
            float x = 0f;
            f.Enter(SpaceId.Library).Samples(() => JudgeSignal.Pose(new Vector3(0f, 0f, z -= 0.15f), 0f), 0.8f);
            Assert.AreEqual(0, f.Value(FearAxis.Layout), "한두 걸음 물러나는 건 괜찮다");
            f.Samples(() => JudgeSignal.Pose(new Vector3(0f, 0f, z += 0.15f), 0f), 2f);
            f.Samples(() => JudgeSignal.Pose(new Vector3(x += 0.15f, 0f, z), 0f), 2f);
            Assert.AreEqual(0, f.Value(FearAxis.Layout), "앞·옆으로 걷는 건 괜찮다");
            f.Samples(() => JudgeSignal.Pose(new Vector3(x, 0f, z -= 0.15f), 0f), 1.3f);
            Assert.AreEqual(Deltas.RuleViolation, f.Value(FearAxis.Layout));

            FinalFixture g = new FinalFixture("L7");
            g.Enter(SpaceId.Corridor).Samples(() => JudgeSignal.Pose(new Vector3(0f, 0f, z -= 0.15f), 0f), 3f);
            Assert.AreEqual(0, g.Value(FearAxis.Layout), "도서관 밖");

            Assert.IsTrue(NoBackwardWalkJudge.IsBackward(Vector3.forward, Vector3.back));
            Assert.IsFalse(NoBackwardWalkJudge.IsBackward(Vector3.forward, Vector3.right));
            Assert.IsFalse(NoBackwardWalkJudge.IsBackward(Vector3.forward, Vector3.forward));
        }
    }
}

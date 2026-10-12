using NUnit.Framework;
using UnityEngine;

namespace NightDuty.Tests
{
    /// <summary>
    /// 79차(2026-10-10) — 민: 수칙 S4·T3 합침(정전 중 손전등 금지, 3초 유예) · T1 문구 · T5·L5 폐기 · 새 수칙 T7·L6·G4·K5 채택 ·
    /// 가짜 수칙 1·9(2일차부터 하나씩, 하루 수칙 수와 따로) · 역설 문구 H3·C2·C4·T1·L1 · S2 5초 · T3·T5 역설 폐기.
    /// </summary>
    public sealed class SeventyNinthPassTests
    {
        [Test]
        public void 수칙_문구와_폐기()
        {
            Assert.AreEqual("정전 중에는 손전등을 켜지 마십시오.", ProgramCatalog.Rule("S4").Text);   // 103차 맞춤법
            Assert.AreEqual("화장실에서 물이 다 내려가기 전에 나오십시오.", ProgramCatalog.Rule("T1").Text);
            Assert.AreEqual("화장실 거울을 3초 이상 보지 마십시오.", ProgramCatalog.Rule("T7").Text);
            Assert.AreEqual("도서관에서는 손전등을 천장으로 향하지 마십시오.", ProgramCatalog.Rule("L6").Text);
            Assert.AreEqual("태블릿을 보면서 걷지 마십시오.", ProgramCatalog.Rule("G4").Text);
            Assert.AreEqual("경비실 전화는 세 번 울린 뒤에 받으십시오.", ProgramCatalog.Rule("K5").Text);
            foreach (string id in new[] { "T3", "T5", "L5" }) Assert.IsTrue(ProgramCatalog.IsRetired(id), id);
            Assert.AreEqual("S4", ProgramCatalog.Encounter(ProgramCatalog.ToiletBlackout).ResponseRule, "화장실 소등도 S4");
            Assert.IsFalse(ProgramCatalog.IsEncounterRetired(ProgramCatalog.SuitMan), "84차: 창밖 노란 얼굴(대응 수칙 L3)로 되살림 — 노란 얼굴 전날 날짜 사슬");
            Assert.IsFalse(ProgramCatalog.IsEncounterRetired(ProgramCatalog.CeilingLegs), "수칙 없는 조우는 해당 없음");
            CollectionAssert.Contains(ProgramDirector.SecurityPool, "K5");
        }

        [Test]
        public void 역설_문구와_폐기()
        {
            Assert.AreEqual("인원이 확인되면, 대기하십시오.", ParadoxCatalog.Find("H3").Message);
            Assert.AreEqual("교실 소음의 원인을 응시해주십시오.", ParadoxCatalog.Find("C2").Message);
            Assert.AreEqual("조명에 이상이 있을 시 손전등으로 확인해주십시오.", ParadoxCatalog.Find("C4").Message);
            Assert.AreEqual("변기 수압을 점검해주십시오.", ParadoxCatalog.Find("T1").Message);
            Assert.AreEqual("도서관 무너진 가구들을 점검해주십시오.", ParadoxCatalog.Find("L1").Message);
            Assert.IsFalse(ParadoxCatalog.Find("T3").HasMessage);
            Assert.IsFalse(ParadoxCatalog.Find("T5").HasMessage);
            Assert.AreEqual(FinalCues.MirrorTarget, ParadoxCatalog.Find("T7").Target, "T7 눈으로만 = 거울을 1초(3초면 위반)");
            Assert.AreEqual(5f, FinalJudges.ScienceLeaveSeconds, "민: S2 5초");
        }

        [Test]
        public void T7_거울을_3초_보면_위반()
        {
            FinalFixture f = new FinalFixture("T7");
            f.Enter(SpaceId.Toilet).Samples(() => JudgeSignal.Gaze(FinalCues.MirrorTarget, 0.1f), 2.5f).Samples(() => JudgeSignal.Gaze(string.Empty, 0.1f), 1f);
            Assert.AreEqual(0, f.Value(FearAxis.Layout), "2.5초에 눈을 떼면 괜찮다");
            f.Samples(() => JudgeSignal.Gaze(FinalCues.MirrorTarget, 0.1f), 3.1f);
            Assert.AreEqual(Deltas.RuleViolation, f.Value(FearAxis.Layout));
        }

        [Test]
        public void L6_도서관에서_손전등을_켜고_2초_올려다보면_위반()
        {
            FinalFixture f = new FinalFixture("L6");
            f.Enter(SpaceId.Library).Light(true).Samples(() => JudgeSignal.Pitch(50f), 1.5f).Samples(() => JudgeSignal.Pitch(0f), 0.5f);
            Assert.AreEqual(0, f.Value(FearAxis.Illuminance), "1.5초는 둘러본 것");
            f.Light(false).Samples(() => JudgeSignal.Pitch(60f), 3f);
            Assert.AreEqual(0, f.Value(FearAxis.Illuminance), "불을 끄고 보는 건 괜찮다");
            f.Light(true).Samples(() => JudgeSignal.Pitch(60f), 2.1f);
            Assert.AreEqual(Deltas.RuleViolation, f.Value(FearAxis.Illuminance));

            FinalFixture g = new FinalFixture("L6");
            g.Enter(SpaceId.Corridor).Light(true).Samples(() => JudgeSignal.Pitch(60f), 3f);
            Assert.AreEqual(0, g.Value(FearAxis.Illuminance), "도서관 밖");
        }

        [Test]
        public void G4_태블릿을_올린_채_1_5초_걸으면_위반()
        {
            FinalFixture f = new FinalFixture("G4");   // 103차: 배치(조도로 옮겼다가 되돌림)
            float x = 0f;
            f.Send(JudgeSignal.Tab(true)).Samples(() => JudgeSignal.Pose(new Vector3(0f, 0f, 0f), 0f), 3f);
            Assert.AreEqual(0, f.Value(FearAxis.Layout), "서서 보는 건 괜찮다");
            f.Send(JudgeSignal.Tab(false)).Samples(() => JudgeSignal.Pose(new Vector3(x += 0.15f, 0f, 0f), 0f), 3f);
            Assert.AreEqual(0, f.Value(FearAxis.Layout), "내리고 걷는 건 괜찮다");
            f.Send(JudgeSignal.Tab(true)).Samples(() => JudgeSignal.Pose(new Vector3(x += 0.15f, 0f, 0f), 0f), 1.7f);
            Assert.AreEqual(Deltas.RuleViolation, f.Value(FearAxis.Layout));
        }

        [Test]
        public void K5_세번째_벨이_끝나기_전에_받거나_안_받으면_위반()
        {
            Assert.AreEqual(8.1f, AnswerAfterRingsJudge.EarliestAnswerSeconds, 1e-3f);

            FinalFixture early = new FinalFixture("K5");
            early.Enter(SpaceId.SecurityRoom).Cue(FinalCues.PhoneRing).Wait(5f).Send(JudgeSignal.PhoneAnswer());
            Assert.AreEqual(Deltas.RuleViolation, early.Value(FearAxis.Auditory), "두 번째 벨에 받음");

            FinalFixture ok = new FinalFixture("K5");
            ok.Enter(SpaceId.SecurityRoom).Cue(FinalCues.PhoneRing).Wait(8.5f).Send(JudgeSignal.PhoneAnswer()).Wait(20f).End(FinalCues.PhoneRing);
            Assert.AreEqual(0, ok.Value(FearAxis.Auditory), "세 번 울린 뒤 받음");

            FinalFixture never = new FinalFixture("K5");
            never.Enter(SpaceId.SecurityRoom).Cue(FinalCues.PhoneRing).Wait(24f).End(FinalCues.PhoneRing);
            Assert.AreEqual(Deltas.RuleViolation, never.Value(FearAxis.Auditory), "받지 않음");
        }

        [Test]
        public void G4는_짝수_날에_하루_장수_안에서_맨_끝()
        {
            Assert.IsFalse(ProgramDirector.HasTabletRule(1));
            Assert.IsTrue(ProgramDirector.HasTabletRule(2));
            Assert.IsFalse(ProgramDirector.HasTabletRule(3));
            Assert.IsTrue(ProgramDirector.HasTabletRule(4));
            Assert.IsFalse(ProgramDirector.HasTabletRule(5));
        }

        [Test]
        public void 가짜_수칙은_1일차에만_두_줄_판정_없이()
        {
            int keep = FakeRuleCatalog.RunOffset;
            FakeRuleCatalog.RunOffset = 0;
            try
            {
            // 103차(사용자: 「첫날에 원래 수칙 4개 + 가짜 2개로 첫날에만」): 옛 79·80차는 2~5일차 하루 한 줄.
            Assert.AreEqual(2, FakeRuleCatalog.CountFor(1));
            Assert.AreEqual("근무는 개인입니다. 변수는 없습니다.", FakeRuleCatalog.ForDay(1, 0));
            Assert.AreEqual("퇴근 후 이 수칙서를 기억할 필요는 없습니다.", FakeRuleCatalog.ForDay(1, 1));
            for (int day = 2; day <= 6; day++)
            {
                Assert.AreEqual(0, FakeRuleCatalog.CountFor(day), day + "일차엔 없다");
                Assert.IsNull(FakeRuleCatalog.ForDay(day));
            }

            Assert.IsNull(FakeRuleCatalog.ForDay(1, 2), "하루 두 줄");
            Assert.AreEqual("F1", FakeRuleCatalog.IdForDay(1, 0));
            Assert.AreEqual("F2", FakeRuleCatalog.IdForDay(1, 1));
            Assert.IsNull(ProgramCatalog.Rule(FakeRuleCatalog.IdForDay(1)), "판정 수칙이 아니다");
            for (int n = 1; n < 8; n++) for (int k = 0; k < 2; k++) Assert.That(FakeRuleCatalog.SlotFor(1, n, k), Is.InRange(0, n - 1));
            Assert.AreNotEqual(FakeRuleCatalog.SlotFor(1, 4, 0), FakeRuleCatalog.SlotFor(1, 5, 1), "두 줄이 붙지 않게 다른 자리");

            // 80차: 다섯 줄을 회차마다 돌린다 — 시작 줄 4면 다섯째 줄(F5 「당신의 이름은 …」)과 첫째 줄.
            FakeRuleCatalog.RunOffset = 4;
            Assert.AreEqual("당신의 이름은 출근부에 이미 적혀 있습니다.", FakeRuleCatalog.ForDay(1, 0));
            Assert.AreEqual(FakeRuleCatalog.Lines[0], FakeRuleCatalog.ForDay(1, 1));
            Assert.AreEqual("F5", FakeRuleCatalog.IdForDay(1, 0));

            // 2~5일차의 그 자리는 진짜 수칙 한 장.
            Assert.AreEqual(4, ProgramDirector.RulesFor(1));
            Assert.AreEqual(6, ProgramDirector.RulesFor(2));
            Assert.AreEqual(6, ProgramDirector.RulesFor(4));
            Assert.AreEqual(7, ProgramDirector.RulesFor(5));
            }
            finally
            {
                FakeRuleCatalog.RunOffset = keep;
            }
        }
    }
}

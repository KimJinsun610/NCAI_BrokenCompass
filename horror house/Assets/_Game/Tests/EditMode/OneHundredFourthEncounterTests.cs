using System.Collections.Generic;
using NUnit.Framework;

namespace NightDuty.Tests
{
    /// <summary>
    /// 104차(사용자: 「변기는 정상입니다 → 빨간 여자, 노란 얼굴 도서관, CCTV 사람, 발자국 등 — 수칙과 지시로 트리거는 된 것 같은데 연출이 안 나왔어」) 1·2번.
    /// ① 조우가 끝나거나 놓칠 때까지 조기 퇴근을 막는다(<see cref="TensionDirector.EncountersPending"/>) · ② CCTV 사람 날은 K-1을 편성에 넣는다.
    /// </summary>
    public sealed class OneHundredFourthEncounterTests
    {
        [TearDown]
        public void TearDown()
        {
            NightRun.StartNewRun();
            EventBus.ClearAll();
        }

        [Test]
        public void 기다리는_조우가_있으면_남은_것으로_보고_놓치면_풀린다()
        {
            DirectorFixture f = new DirectorFixture(3, 0.99, new[] { "T4" }, DirectorFixture.Slot(EncounterSlot.C, ProgramCatalog.ToiletGirl));
            f.Wait(0.2f);
            Assert.IsTrue(f.Director.EncountersPending, "슬롯 C를 기다리는 동안은 남은 조우");

            f.Minute = NightClock.JudgingEnd + 1f;
            f.Wait(0.2f);
            Assert.AreEqual(EncounterRunState.Missed, f.Director.Runs[0].State);
            Assert.IsFalse(f.Director.EncountersPending, "놓침(03:32)이면 더 막지 않는다");
        }

        [Test]
        public void 조우가_없는_밤은_남은_조우가_없다()
        {
            DirectorFixture f = new DirectorFixture(1, 0.5, new string[0]);
            f.Wait(0.2f);
            Assert.IsFalse(f.Director.EncountersPending);
        }

        [Test]
        public void CCTV_사람_날은_K1을_편성에_넣는다()
        {
            InspectionPlan plan = new InspectionPlan(3, new[]
            {
                new InspectionAssignment(InspectionCatalog.Find("K-2"), false, Band.Band0, false),
                new InspectionAssignment(InspectionCatalog.Find("C-1"), true, Band.Band1, false),
            }, SpaceId.Corridor, "C-1");
            NightProgram cctv = new NightProgram(3, new List<SlotEncounter>(), new[] { ProgramCatalog.Rule("K1") }, "test", new[] { ProgramCatalog.Encounter(ProgramCatalog.CctvPerson) });
            Assert.IsTrue(cctv.HasEncounter(ProgramCatalog.CctvPerson), "겹침 조우도 편성된 조우");

            InspectionPlan patched = NightRun.PatchPlanForProgram(plan, cctv);
            InspectionAssignment k1 = patched.Find(InspectionCatalog.FirstInspection);
            Assert.IsNotNull(k1, "CCTV를 보러 올 지시(K-1)");
            Assert.IsFalse(k1.IsAnomaly);

            NightProgram none = new NightProgram(3, new List<SlotEncounter>(), new[] { ProgramCatalog.Rule("K2") }, "test");
            Assert.IsNull(NightRun.PatchPlanForProgram(plan, none).Find(InspectionCatalog.FirstInspection), "CCTV 사람이 없는 날은 그대로");
        }
    }
}

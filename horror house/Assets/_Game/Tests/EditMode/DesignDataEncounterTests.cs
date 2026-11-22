using InstructionDT;
using NUnit.Framework;

namespace NightDuty.Tests
{
    /// <summary>3단계 Encounter: 조우 카탈로그·고정 편성·유도 문자가 Encounter_Table에서 오는지, 소리 표가 ID 키로도 찾히는지.</summary>
    public class DesignDataEncounterTests
    {
        [Test]
        public void 조우_카탈로그는_표와_같다()
        {
            DesignData.Ensure();
            foreach (var r in DT.Encounter.All)
            {
                EncounterDef e = ProgramCatalog.Encounter(r.ID.ToString());
                Assert.IsNotNull(e, r.Code);
                Assert.AreEqual(r.Name, e.Name, r.Code);
                Assert.AreEqual(r.Rule_1_ID == 0 ? string.Empty : r.Rule_1_ID.ToString(), e.ResponseRule, r.Code);
                Assert.AreEqual((int)r.Intensity, e.Intensity, r.Code);
                Assert.AreEqual(r.Is_Cross, e.IsCross, r.Code);
            }

            Assert.AreEqual(DT.Encounter.All.Count, ProgramCatalog.AllEncounters.Count);
        }

        [Test]
        public void 유도_문자는_표의_문자_ID()
        {
            DesignData.Ensure();
            foreach (var r in DT.Encounter.All)
            {
                string want = DesignData.Msg(r.Guide_Message_ID != 0 ? r.Guide_Message_ID : 140019u);
                Assert.AreEqual(want, NightRun.GuideText(r.ID.ToString()), r.Code);
            }
        }

        [Test]
        public void 클라이맥스와_밤새_대기는_표의_편성_칸()
        {
            Assert.AreEqual(ProgramCatalog.CeilingLegs, ProgramDirector.ClimaxOf(2));
            Assert.AreEqual(ProgramCatalog.Footsteps, ProgramDirector.ClimaxOf(3));
            Assert.AreEqual(ProgramCatalog.BoyBang, ProgramDirector.ClimaxOf(4));
            Assert.AreEqual(ProgramCatalog.YellowFace, ProgramDirector.ClimaxOf(5));
            Assert.IsNull(ProgramDirector.ClimaxOf(1));
            CollectionAssert.AreEqual(new[] { ProgramCatalog.HallEndFigure }, ProgramDirector.FixedStanding(4));
            CollectionAssert.AreEqual(new[] { ProgramCatalog.ModelRush }, ProgramDirector.FixedStanding(5));
        }

        [Test]
        public void 소리_표는_조우_ID_키로도_찾힌다()
        {
            System.Type so = System.Type.GetType("DirectionSoundTableSO, Assembly-CSharp");   // Flow는 시험 어셈블리가 참조하지 않는다
            Assert.IsNotNull(so);
            var find = so.GetMethod("FindExact", new[] { typeof(string), typeof(float).MakeByRefType() });
            object[] byId = { ProgramCatalog.BoyBang + ".headbang", 0f };
            object[] byCode = { "E.BoyBang.headbang", 0f };
            Assert.IsNotNull(find.Invoke(null, byCode), "옛 키(에셋 정본)");
            Assert.IsNotNull(find.Invoke(null, byId), "ID 키(코드가 만드는 꼴)");
        }
    }
}

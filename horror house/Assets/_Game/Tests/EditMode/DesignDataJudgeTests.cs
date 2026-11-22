using NUnit.Framework;

namespace NightDuty.Tests
{
    /// <summary>
    /// 4단계 뒤 판정 수치 연결: <see cref="FinalJudges"/>가 칸 이름으로 읽는 Rule_Table 판정 칸이 모두 있고 비어 있지 않은지,
    /// 바꾸기 전 코드 상수와 값이 같은지(차이 0). 코드가 따로 계산하는 칸(C6 · 정지 반경)은 대조만.
    /// </summary>
    public class DesignDataJudgeTests
    {
        static readonly (uint id, string column, float old)[] Used =
        {
            (110001u, "Judge_Dist", 1.2f), (110001u, "Judge_Dist2", 3.6f),          // H1 반경 · 방아쇠(옛 반경 ×3)
            (110003u, "Judge_Time", 6f), (110003u, "Judge_Time2", 2f),              // H3
            (110004u, "Judge_Angle", 90f), (110004u, "Judge_Angle2", 30f), (110004u, "Judge_Angle3", 60f), (110004u, "Judge_Dist", 2f),   // H4
            (110005u, "Judge_Time", 1f),                                            // C1
            (110006u, "Judge_Time", 3f),                                            // C2
            (110008u, "Judge_Time", 1.5f),                                          // C4
            (110012u, "Judge_Time", 5f),                                            // S2
            (110013u, "Judge_Time", 1.5f),                                          // S3
            (110014u, "Judge_Time", 3f),                                            // S4
            (110015u, "Judge_Time", 3f),                                            // S5
            (110016u, "Judge_Time", 8f),                                            // T1
            (110018u, "Judge_Time", 3f),                                            // T3
            (110019u, "Judge_Time", 0.1f),                                          // T4 목격
            (110020u, "Judge_Time", 3f),                                            // T5
            (110021u, "Judge_Time", 3f),                                            // T7
            (110022u, "Judge_Dist", 1.5f), (110022u, "Judge_Dist2", 3f), (110022u, "Judge_Time", 3f),   // L1(방아쇠 = 옛 반경 ×2)
            (110023u, "Judge_Time", 10f),                                           // L2
            (110024u, "Judge_Time", 600f),                                          // L3
            (110025u, "Judge_Dist", 1.5f), (110025u, "Judge_Dist2", 4.5f),          // L4(방아쇠 = 옛 반경 ×3)
            (110026u, "Judge_Time", 2f),                                            // L5
            (110027u, "Judge_Time", 2f), (110027u, "Judge_Angle", 35f),             // L6
            (110028u, "Judge_Time", 1f), (110028u, "Judge_Dist", 0.04f), (110028u, "Judge_Angle", 55f),   // L7
            (110030u, "Judge_Time", 3f),                                            // K2
            (110031u, "Judge_Time", 45f),                                           // K3
            (110033u, "Judge_Time", 3f), (110033u, "Judge_Time2", 2.1f), (110033u, "Judge_Count", 3f),   // K5
            (110034u, "Judge_Time", 1f),                                            // G1
            (110035u, "Judge_Time", 1.5f), (110035u, "Judge_Dist", 0.04f),          // G4
            (110037u, "Judge_Count", 3f),                                           // H5
        };

        [Test]
        public void 판정기가_읽는_칸은_모두_있고_옛_상수와_같다()
        {
            var bad = new System.Collections.Generic.List<string>();
            foreach (var u in Used)
            {
                float v;
                try { v = DesignData.Judge(u.id, u.column); }
                catch (System.Exception e) { bad.Add(e.Message); continue; }
                if (System.Math.Abs(v - u.old) > 1e-4f) bad.Add(u.id + " " + u.column + " 표 " + v + " ≠ 옛 코드 " + u.old);
            }

            Assert.IsEmpty(bad, string.Join("\n", bad));
        }

        [Test]
        public void 판정기는_모든_수칙에_만들어진다()
        {
            foreach (RuleDef def in ProgramCatalog.AllRules)
                for (int day = 1; day <= 5; day++)
                    Assert.DoesNotThrow(() => FinalJudges.Create(def, day), def.Id + " " + day + "일차");
        }

        [Test]
        public void C6_교실_머묾은_3일차_실제_초와_같다()
        {
            Assert.AreEqual(DesignData.Judge(110010u, "Judge_Time"), ProgramCatalog.ClassroomDwellRealSecondsFor(3), 0.01f, "C6은 게임 24분을 일차 시계로 바꾼 값(코드 계산) — 표는 3일차 이후 값");
        }

        [Test]
        public void 움직임_판정_반경은_정지_반경과_같다()
        {
            foreach (uint id in new[] { 110007u, 110015u, 110018u })   // C3 · S5 · T3
                Assert.AreEqual(SensingRules.StillRadius, DesignData.Judge(id, "Judge_Dist"), 1e-4f, id + " — 코드는 Setting 390017(정지 반경)을 쓴다");
        }
    }
}

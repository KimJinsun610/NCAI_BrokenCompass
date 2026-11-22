using NUnit.Framework;

namespace NightDuty.Tests
{
    /// <summary>
    /// 4단계 뒤 조우 방아쇠 수치 연결: <see cref="EncounterScripts"/>·<see cref="TensionDirector"/>가 칸 이름으로 읽는 Encounter_Table 칸이
    /// 모두 있고 비어 있지 않은지, 바꾸기 전 코드 값과 같은지(차이 0). 코드에만 있는 방아쇠(노란 얼굴 4초·천장 다리 응시 0.5초)는 표에 없다.
    /// </summary>
    public class DesignDataEncounterValueTests
    {
        static readonly (uint id, string column, float old)[] Used =
        {
            (120001u, "Trigger_Value", 3f), (120001u, "Window_Sec", 15f), (120001u, "Ahead_M", 3f),
            (120002u, "Window_Sec", 18f), (120002u, "Ahead_M", 3f),
            (120003u, "Window_Sec", 6f), (120003u, "Ahead_M", 3f),
            (120004u, "Trigger_Value", 10f), (120004u, "Window_Sec", 8f), (120004u, "After_Sec", 2.2f), (120004u, "Behind_M", 12f),
            (120005u, "Trigger_Value", 5f), (120005u, "Window_Sec", 6f), (120005u, "Behind_M", 6f),
            (120006u, "Window_Sec", 30f), (120006u, "Ahead_M", 4f),
            (120007u, "Window_Sec", 10f), (120007u, "Ahead_M", 7f),
            (120008u, "Window_Sec", 20f), (120008u, "Ahead_M", 5f),
            (120009u, "Trigger_Value", 5f), (120009u, "Window_Sec", 12f),
            (120010u, "Trigger_Value", 4f), (120010u, "Window_Sec", 10f),
            (120011u, "Ahead_M", 8f), (120011u, "Presence_Sec", 90f),
            (120012u, "Foreshadow_Fixed_Sec", 1.2f), (120012u, "Window_Sec", 9f), (120012u, "Ahead_M", 1.1f),
            (120013u, "Trigger_Value", 3f), (120013u, "Ahead_M", 3f),
            (120014u, "Trigger_Value", 6f), (120014u, "Window_Sec", 10f), (120014u, "Ahead_M", 5f),
            (120015u, "Trigger_Value", 2f), (120015u, "Window_Sec", 10f),
            (120016u, "Trigger_Value", 6f), (120016u, "Window_Sec", 45f), (120016u, "After_Sec", 0f), (120016u, "Behind_M", 3f),
        };

        [Test]
        public void 대본이_읽는_칸은_모두_있고_옛_값과_같다()
        {
            var bad = new System.Collections.Generic.List<string>();
            foreach (var u in Used)
            {
                float v;
                try { v = DesignData.EncounterValue(u.id, u.column); }
                catch (System.Exception e) { bad.Add(e.Message); continue; }
                if (System.Math.Abs(v - u.old) > 1e-4f) bad.Add(u.id + " " + u.column + " 표 " + v + " ≠ 옛 코드 " + u.old);
            }

            Assert.IsEmpty(bad, string.Join("\n", bad));
        }

        [Test]
        public void 대본은_조우마다_만들어지고_표_값을_쓴다()
        {
            EncounterScript boy = EncounterScripts.Find(ProgramCatalog.BoySeated);
            Assert.IsNotNull(boy);
            Assert.AreEqual(DesignData.EncounterValue(120001u, "Window_Sec"), boy.Window);
            Assert.AreEqual(DesignData.EncounterValue(120004u, "Behind_M"), EncounterScripts.Find(ProgramCatalog.Footsteps).Distance);
            Assert.AreEqual(-1f, EncounterScripts.Find(ProgramCatalog.PeopleTree).Window, "존재형은 코드(-1)");
            Assert.AreEqual(90f, TensionDirector.SeqPresenceSeconds);
        }

        [Test]
        public void 모형_급습_근접_거리는_설정과_표가_같다()
        {
            Assert.AreEqual(EncounterScripts.RushNearMeters, DesignData.EncounterValue(120008u, "Trigger_Value"), 1e-4f, "코드는 Setting 390178을 쓴다");
        }
    }
}

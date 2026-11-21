using InstructionDT;
using NUnit.Framework;

namespace NightDuty.Tests
{
    /// <summary>3단계 Ending·Layout_Prop: 엔딩 enum ↔ 표, 응시 기준, 배치 소품 수가 표에서 오는지.</summary>
    public class DesignDataEndingLayoutTests
    {
        [Test]
        public void 엔딩_enum은_표의_행과_이어진다()
        {
            Assert.AreEqual(230001u, DesignData.EndingRowId(FinaleEnding.ShiftChange));
            Assert.AreEqual(230002u, DesignData.EndingRowId(FinaleEnding.ShiftOver));
            Assert.AreEqual(0u, DesignData.EndingRowId(FinaleEnding.None));
            Assert.AreEqual(1.5f, FinaleWatch.GazeSeconds, 1e-4f, "봤다 = 창밖 응시 합 1.5초");
            Assert.AreEqual(DT.Ending.Get(230001).Final_Text, DesignData.Msg(140033), "엔딩 1 마지막 문구 = 문자 140033");
        }

        [Test]
        public void 배치_소품_수는_표에서()
        {
            DesignData.Ensure();
            Assert.AreEqual(21, DT.LayoutProp.All.Count);
            foreach (var r in DT.LayoutProp.All)
            {
                int[] c = DesignData.LayoutCounts(r.ID);
                Assert.AreEqual(5, c.Length, r.Name);
                Assert.AreEqual(0, c[0], r.Name + " — 띠 0에는 배치 소품이 없다");
                for (int i = 1; i < 5; i++) Assert.GreaterOrEqual(c[i], c[i - 1], r.Name + " — 띠가 오를수록 줄지 않는다");
            }

            CollectionAssert.AreEqual(new[] { 0, 2, 4, 7, 12 }, DesignData.LayoutCounts(410001), "복도 없던 문");
            System.Type corridor = System.Type.GetType("LayoutCorridorMap, Assembly-CSharp");   // Flow는 시험 어셈블리가 참조하지 않는다
            Assert.IsNotNull(corridor);
            CollectionAssert.AreEqual(DesignData.LayoutCounts(410001), (int[])corridor.GetProperty("Doors").GetValue(null));
        }
    }
}

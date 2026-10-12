using NUnit.Framework;

namespace NightDuty.Tests
{
    /// <summary>
    /// 90차(2026-10-11) — 민: 점검 지시 문구 표(「~의 상태를 보고해주십시오.」 · 「~인지 확인해주십시오.」)로 바꿈.
    /// </summary>
    public sealed class NinetiethPassTests
    {
        [Test]
        public void 점검_지시_문구는_모두_해주십시오로_끝난다()
        {
            foreach (InspectionItem it in InspectionCatalog.All)
            {
                StringAssert.EndsWith("해주십시오.", it.TabletLine, it.Id);
                Assert.AreEqual(it.TabletLine.Trim(), it.TabletLine, it.Id + " 앞뒤 공백 없음");
            }
        }

        [TestCase("H-3", "경비실 문 옆 알림종이 조용한지 확인해주십시오.")]   // 104차(사용자 결정): 옛 「경비실 쪽 복도 위 알림종이 꺼져 있는지 확인해주십시오.」
        [TestCase("H-5", "도서관 옆 복도 스피커가 조용한지 확인해주십시오.")]   // 104차(사용자 결정): 옛 「도서관 옆 복도의 스피커가 꺼져 있는지 확인해주십시오.」
        [TestCase("L-2", "도서관 입구 맞은편 창 블라인드 둘이 내려져 있는지 확인해주십시오.")]
        [TestCase("T-1", "화장실 3번 칸 변기의 상태를 보고해주십시오.")]   // 104차(사용자 결정): 옛 「화장실 변기의 상태를 보고해주십시오.」
        public void 민_문구(string id, string line)
        {
            Assert.AreEqual(line, InspectionCatalog.Find(id).TabletLine);
        }
    }
}

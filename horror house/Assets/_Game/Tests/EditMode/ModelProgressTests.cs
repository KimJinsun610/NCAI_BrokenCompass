using NUnit.Framework;

namespace NightDuty.Tests
{
    /// <summary>과학실 몬스터 인체 모형의 자리 진행(42차, 민 — 「첫날부터 정지한 채 과학실에 있다가 점점 움직여 복도에서 급습」). 56차부터 배치 구간만 따른다.</summary>
    public sealed class ModelProgressTests
    {
        [Test]
        public void 첫날은_테이프_안에서_움직이지_않는다()
        {
            int start;
            int max;
            ModelProgress.Spots(1, Band.Band0, false, out start, out max);
            Assert.AreEqual(0, start);
            Assert.AreEqual(0, max, "1일차는 정지");
        }

        [Test]
        public void 일차가_지날수록_시작_자리가_문쪽으로_간다()
        {
            int s1, s2, s3, m;
            ModelProgress.Spots(1, Band.Band0, false, out s1, out m);
            ModelProgress.Spots(2, Band.Band0, false, out s2, out m);
            ModelProgress.Spots(3, Band.Band0, false, out s3, out m);
            Assert.That(s1 < s2 && s2 < s3, s1 + " " + s2 + " " + s3);
            Assert.AreEqual(2, m, "복도 조건이 없으면 과학실 밖(복도 자리)으로 나오지 않는다");
        }

        [Test]
        public void 복도는_배치_3이나_모형_급습이_편성된_밤에만()
        {
            int start;
            int max;
            ModelProgress.Spots(4, Band.Band2, false, out start, out max);
            Assert.Less(max, ModelProgress.HallSpot, "배치 2 — 복도 조건 미달");

            ModelProgress.Spots(4, Band.Band3, false, out start, out max);
            Assert.AreEqual(ModelProgress.HallSpot, max, "배치 3");

            ModelProgress.Spots(3, Band.Band0, true, out start, out max);
            Assert.AreEqual(ModelProgress.HallSpot, max, "그 밤 편성에 모형 급습(S3 위반 예약 등)");
            Assert.LessOrEqual(start, max);
        }

        [Test]
        public void 배치_구간이_높으면_첫날이_아니어도_더_나와_있다()
        {
            int low, high, m;
            ModelProgress.Spots(2, Band.Band0, false, out low, out m);
            ModelProgress.Spots(2, Band.Band2, false, out high, out m);
            Assert.Greater(high, low);
        }
    }
}

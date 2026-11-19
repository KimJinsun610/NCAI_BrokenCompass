using NUnit.Framework;

namespace NightDuty.Tests
{
    /// <summary>
    /// 설계 불변식 검사(2026-09-21 신설).
    /// <para>
    /// 2026-10-03: 옛 24장 카드 에셋 ↔ 카드 빌더 드리프트 검사, 「하루 6장 축 쿼터」 검사 3건,
    /// 「역설 최소 신뢰 = 신뢰 구간 1 하한」 검사를 지웠다(검사 대상 폐기).
    /// </para>
    /// <para>
    /// 2026-11-19: CLAUDE.md 본문에 구간 경계·신뢰 경계·일차 하한 숫자가 적혀 있는지 보던
    /// ClaudeMdDriftTests 4건을 지웠다. 문서에 코드 상수를 복사하게 강제했기 때문이다 —
    /// 이제 CLAUDE.md는 숫자 대신 정본 위치(Bands.LowerBounds · Bands.TrustLowerBounds · DayFloor.Floors)만 적는다.
    /// 값 자체의 검사는 각 시스템 테스트(TrustBandTests 등)가 맡는다.
    /// </para>
    /// </summary>
    public sealed class DesignInvariantTests
    {
        [TearDown]
        public void TearDown()
        {
            EventBus.ClearAll();
        }

        /// <summary>일차 하한 곡선이 뒤집히거나 Band4까지 공짜로 올라가는 것을 막는다(2026-09-30: 연출 구간 하한).</summary>
        [Test]
        public void 일차하한은_줄지않고_Band4에_닿지_않는다()
        {
            Assert.AreEqual(Band.Band0, DayFloor.Of(1), "1일차는 하한 없이 시작한다");
            for (int day = 2; day <= DayFloor.LastDay; day++)
            {
                Assert.GreaterOrEqual(
                    (int)DayFloor.Of(day),
                    (int)DayFloor.Of(day - 1),
                    day + "일차 하한 " + DayFloor.Of(day) + "이 " + (day - 1) + "일차 " + DayFloor.Of(day - 1) + "보다 낮습니다.");
            }

            Band last = DayFloor.Of(DayFloor.LastDay);
            Assert.Greater((int)last, (int)DayFloor.Of(1), "마지막 날 하한이 첫날과 같습니다. 날이 갈수록 나빠지지 않습니다.");
            Assert.Less(
                (int)last,
                (int)Band.Band4,
                "마지막 일차 하한 " + last + "이 Band4입니다. Band4는 「당신이 어겨서 여기까지 왔다」는 구간이라 "
                    + "하한으로 공짜로 주면 경고로서의 뜻이 사라집니다.");
        }
    }
}

namespace NightDuty
{
    /// <summary>
    /// 공포 축 값(0~100)과 구간(<see cref="Band"/>) 사이의 <b>원시 범위 표</b>.
    /// <para>
    /// <b>이 클래스에는 히스테리시스가 없다.</b> 값 하나를 넣으면 그 값이 속한 구간을 그대로 돌려줄 뿐이라,
    /// 경계값(예: 74 ↔ 75) 부근에서 값이 흔들리면 구간도 그대로 흔들린다.
    /// 경계 떨림을 막는 진입/이탈 임계 분리(히스테리시스)와 구간 변경 확정은
    /// W2의 <c>BandResolver</c>가 이 표를 재료로 삼아 처리한다.
    /// 연출 코드가 구간을 판정할 때는 <c>Bands.Of</c>가 아니라 <c>BandResolver</c>를 거쳐야 한다.
    /// </para>
    /// <para>
    /// 구간 폭은 균일하지 않다: 0~24 / 25~49 / 50~74 / 75~89 / 90~100.
    /// 경계 숫자는 아래 <c>LowerBounds</c> 표 한 곳에만 존재한다 — if 사슬로 흩뿌리지 말 것.
    /// </para>
    /// </summary>
    public static class Bands
    {
        /// <summary>축 값의 하한(포함).</summary>
        public const int Min = 0;

        /// <summary>축 값의 상한(포함).</summary>
        public const int Max = 100;

        /// <summary>
        /// 조도 축 75 이상 = "붉게 보인다" 판정 임계. 근무수칙 다수가 이 값을 참조한다.
        /// 손전등 상태와 무관하게 조도 축 값만으로 평가한다.
        /// 50~74(3200K)는 붉어 보이지만 붉음 판정에는 들지 않는 회색지대로 일부러 남겨 둔 구간이다.
        /// </summary>
        public const int RedThreshold = 75;

        /// <summary>구간 개수.</summary>
        public const int Count = 5;

        /// <summary>
        /// 각 구간의 하한(포함)과, 마지막에 <see cref="Max"/> + 1 센티널.
        /// 구간 i의 범위는 [ LowerBounds[i], LowerBounds[i + 1] - 1 ] 이다.
        /// 범위를 바꾸려면 오직 이 배열만 고치면 된다.
        /// </summary>
        private static readonly int[] LowerBounds = { 0, 25, 50, 75, 90, Max + 1 };

        /// <summary>
        /// 값이 속한 구간을 돌려준다. 히스테리시스 없음.
        /// 범위를 벗어난 입력은 예외 대신 클램프한다(0 미만 → Band0, 100 초과 → Band4).
        /// </summary>
        /// <param name="value">공포 축 값. 0~100을 기대하지만 벗어나도 안전하다.</param>
        public static Band Of(int value)
        {
            if (value <= Min)
            {
                return Band.Band0;
            }

            if (value >= Max)
            {
                return Band.Band4;
            }

            // 위쪽 구간부터 훑는다. LowerBounds[i] 이상이면 그 구간이다.
            for (int i = Count - 1; i > 0; i--)
            {
                if (value >= LowerBounds[i])
                {
                    return (Band)i;
                }
            }

            return Band.Band0;
        }

        /// <summary>
        /// 구간의 하한(포함). 범위 밖 열거값은 가장 가까운 구간으로 클램프한다.
        /// </summary>
        public static int LowerBound(Band band)
        {
            int i = ClampIndex((int)band);
            return LowerBounds[i];
        }

        /// <summary>
        /// 구간의 상한(포함). 범위 밖 열거값은 가장 가까운 구간으로 클램프한다.
        /// </summary>
        public static int UpperBound(Band band)
        {
            int i = ClampIndex((int)band);
            return LowerBounds[i + 1] - 1;
        }

        /// <summary>
        /// 구간 안에서의 진행도 0..1. 색온도 보간 등에 쓴다.
        /// 하한이면 0, 상한이면 1이며 그 사이를 선형 보간한다.
        /// 결과는 항상 0..1로 클램프되고, 폭이 0인 구간에서도 0으로 나누지 않는다.
        /// </summary>
        /// <param name="value">공포 축 값.</param>
        /// <param name="band">기준 구간. <paramref name="value"/>가 이 구간 밖이면 0 또는 1로 잘린다.</param>
        public static float Progress(int value, Band band)
        {
            int lower = LowerBound(band);
            int upper = UpperBound(band);
            int width = upper - lower;

            if (width <= 0)
            {
                // 폭이 1 이하인 구간 — 0으로 나누는 것을 막는다.
                return value >= upper ? 1f : 0f;
            }

            if (value <= lower)
            {
                return 0f;
            }

            if (value >= upper)
            {
                return 1f;
            }

            return (value - lower) / (float)width;
        }

        /// <summary>구간 인덱스를 0..Count-1로 클램프한다.</summary>
        private static int ClampIndex(int index)
        {
            if (index < 0)
            {
                return 0;
            }

            if (index > Count - 1)
            {
                return Count - 1;
            }

            return index;
        }
    }
}

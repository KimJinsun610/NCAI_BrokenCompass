namespace NightDuty
{
    /// <summary>
    /// 공포 축 값(0~100)과 구간(<see cref="Band"/>) 사이의 <b>원시 범위 표</b>.
    /// <para>
    /// 값 하나를 넣으면 그 값이 속한 구간을 그대로 돌려줄 뿐이다. 연출이 보는 구간(<b>연출 구간</b>)은
    /// 이 표를 재료로 <c>BandResolver</c>가 정한다 — 한 번 닿은 구간은 내려가지 않고(래칫),
    /// 일차 하한(<see cref="DayFloor"/>) 밑으로도 내려가지 않는다.
    /// 연출 코드가 구간을 판정할 때는 <c>Bands.Of</c>가 아니라 <c>BandResolver</c>를 거쳐야 한다.
    /// </para>
    /// <para>
    /// 감각 축 구간: 0–24 / 25–49 / 50–74 / 75–89 / 90–99 (2026-09-30 새 기획서).
    /// <b>신뢰는 전용 경계</b> 0–14 / 15–29 / 30–44 / 45–64 / 65–100을 쓴다(<see cref="OfTrust"/>, 2026-09-30 최종 기획서) —
    /// 감각 축과 같은 경계를 쓰면 신뢰 구간이 실력과 무관하게 0/1/1/2/3으로 고정된다(시뮬레이션).
    /// <b>100은 구간이 아니라 붙잡힘이다.</b> <c>Of(100)</c>은 표현 편의상 Band4를 돌려주지만,
    /// 100 도달 처리는 <c>FearAxisSystem</c>이 별도 경로로 맡는다.
    /// 경계 숫자는 아래 <c>LowerBounds</c>·<c>TrustLowerBounds</c> 표에만 존재한다 — if 사슬로 흩뿌리지 말 것.
    /// </para>
    /// </summary>
    public static class Bands
    {
        /// <summary>축 값의 하한(포함).</summary>
        public const int Min = 0;

        /// <summary>축 값의 상한(포함). 이 값에 도달하면 붙잡힘이다.</summary>
        public const int Max = 100;

        /// <summary>구간 개수.</summary>
        public const int Count = 5;

        /// <summary>
        /// 감각 축 각 구간의 하한(포함)과, 마지막에 <see cref="Max"/> 센티널.
        /// 구간 i의 범위는 [ LowerBounds[i], LowerBounds[i + 1] - 1 ] 이다. 따라서 Band4는 90~99이고 100은 어느 구간의 범위에도 들지 않는다.
        /// 범위를 바꾸려면 오직 이 배열만 고치면 된다.
        /// <para>
        /// <b>2026-09-30 새 기획서:</b> 25/50/75/90으로 되돌렸다. 24 배수 경계(2026-09-21)는
        /// 「위반 두 번이면 반드시 다음 구간」을 맞추려던 장치였는데, 이제 연출 구간은 일차 하한과 래칫이
        /// 밀어 올리므로 그 맞춤이 필요 없다. Band3(75–89)·Band4(90–99)가 좁은 것은 의도다 —
        /// 붙잡히기 직전 구간이 길면 긴장이 풀린다.
        /// </para>
        /// </summary>
        private static readonly int[] LowerBounds = { 0, 25, 50, 75, 90, Max };

        /// <summary>
        /// 신뢰 전용 구간 하한(포함)과 <see cref="Max"/> 센티널(2026-09-30 최종 기획서 「신뢰 전용 경계 15/30/45/65」).
        /// 신뢰는 줄지 않고 100이어도 붙잡힘이 아니므로 Band4는 65~100 전부다.
        /// <para>
        /// 이 경계로 4일차 시작에 신뢰 구간 3 이상인 비율이 능숙 84% · 보통 41% · 서툰 8%다(시드 5개 × 4,000회).
        /// 감각 축의 25/50/75를 그대로 쓰면 누구나 0/1/1/2/3이 되어 「잘하는 사람은 문서가 무섭다」가 사라진다.
        /// </para>
        /// </summary>
        private static readonly int[] TrustLowerBounds = { 0, 15, 30, 45, 65, Max };

        /// <summary>
        /// 감각 축 값이 속한 구간을 돌려준다. 래칫·하한 없음.
        /// 범위를 벗어난 입력은 예외 대신 클램프한다(0 미만 → Band0, 100 초과 → Band4).
        /// <b>신뢰는 <see cref="OfTrust"/>를 쓴다.</b>
        /// </summary>
        /// <param name="value">공포 축 값. 0~100을 기대하지만 벗어나도 안전하다.</param>
        public static Band Of(int value)
        {
            return OfTable(value, LowerBounds);
        }

        /// <summary>
        /// 신뢰 값이 속한 구간(신뢰 전용 경계 15/30/45/65). 범위 밖 입력은 클램프한다.
        /// </summary>
        /// <param name="trust">신뢰 값 0~100.</param>
        public static Band OfTrust(int trust)
        {
            return OfTable(trust, TrustLowerBounds);
        }

        /// <summary>
        /// 축에 맞는 표로 구간을 돌려준다. 신뢰면 <see cref="OfTrust"/>, 감각 축이면 <see cref="Of"/>.
        /// </summary>
        public static Band OfAxis(FearAxis axis, int value)
        {
            return axis == FearAxis.Trust ? OfTrust(value) : Of(value);
        }

        /// <summary>두 구간 중 높은 쪽.</summary>
        public static Band Higher(Band a, Band b)
        {
            return a >= b ? a : b;
        }

        /// <summary>
        /// 감각 축 구간의 하한(포함). 범위 밖 열거값은 가장 가까운 구간으로 클램프한다.
        /// </summary>
        public static int LowerBound(Band band)
        {
            int i = ClampIndex((int)band);
            return LowerBounds[i];
        }

        /// <summary>
        /// 감각 축 구간의 상한(포함). 범위 밖 열거값은 가장 가까운 구간으로 클램프한다.
        /// </summary>
        public static int UpperBound(Band band)
        {
            int i = ClampIndex((int)band);
            return LowerBounds[i + 1] - 1;
        }

        /// <summary>신뢰 구간의 하한(포함).</summary>
        public static int TrustLowerBound(Band band)
        {
            int i = ClampIndex((int)band);
            return TrustLowerBounds[i];
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

        private static Band OfTable(int value, int[] table)
        {
            if (value <= Min)
            {
                return Band.Band0;
            }

            if (value >= Max)
            {
                return Band.Band4;
            }

            // 위쪽 구간부터 훑는다. table[i] 이상이면 그 구간이다.
            for (int i = Count - 1; i > 0; i--)
            {
                if (value >= table[i])
                {
                    return (Band)i;
                }
            }

            return Band.Band0;
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

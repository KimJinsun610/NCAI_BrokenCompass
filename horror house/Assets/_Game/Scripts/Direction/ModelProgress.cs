using System;

namespace NightDuty
{
    /// <summary>
    /// 과학실 몬스터 인체 모형의 자리 진행(2026-10-04 42차, 민 — 「첫날부터 정지한 모습으로 과학실에 있다가, 점점 움직이다가 복도에서 급습」).
    /// 자리 0 테이프 안·등 돌림 → 1 테이프 안·문을 봄 → 2 테이프 밖 통로 → 3 과학실 앞 복도 끝. 씬 연출은 <c>ScienceModel</c>.
    /// <para>56차(민: 「배치에 어울리는 변화들이 조도에 가 있다」): 「있던 것이 다른 자리에 있다」는 <b>배치</b>의 언어라 배치 구간만 따른다.
    /// 전에는 조도·배치 중 큰 쪽이었다 — 조도는 빛과 시야만 바꾼다.</para>
    /// </summary>
    public static class ModelProgress
    {
        /// <summary>복도 자리.</summary>
        public const int HallSpot = 3;

        /// <summary>
        /// 그 밤의 시작 자리와 최대 자리. 시작 = max(일차 1일 0·2일 1·3일~ 2, 배치 구간 1 → 1, 2 이상 → 2).
        /// 최대 = 복도까지 나오는 밤(배치 3 이상, 또는 그 밤 편성에 모형 급습)이면 복도(3), 1일차는 시작 그대로(움직이지 않음), 그 밖에는 시작 + 1(과학실 안 2까지).
        /// </summary>
        public static void Spots(int day, Band layout, bool rushTonight, out int start, out int max)
        {
            int byDay = day <= 1 ? 0 : (day == 2 ? 1 : 2);
            int byBand = layout >= Band.Band2 ? 2 : (layout >= Band.Band1 ? 1 : 0);
            start = Math.Max(byDay, byBand);
            if (rushTonight || layout >= Band.Band3) max = HallSpot;
            else if (day <= 1) max = start;
            else max = Math.Min(start + 1, 2);
            if (max < start) max = start;
        }

        /// <summary>
        /// 64차(「인체모형 더 활발히 이동」): 과학실을 나갈 때 옮길 다음 자리. 최대 자리 전이면 한 칸 앞, 최대 자리면 그 밤 범위(시작~최대)의 <b>다른</b> 자리 하나(<paramref name="roll"/>로 고른다).
        /// 범위가 한 자리뿐이면(1일차) 그대로.
        /// </summary>
        public static int NextSpot(int spot, int start, int max, int roll)
        {
            if (max <= start) return spot;
            if (spot < max) return Math.Max(start, spot + 1);
            int others = max - start;
            int pick = start + (Math.Abs(roll) % others);
            if (pick >= spot) pick++;
            return Math.Min(pick, max);
        }
    }
}

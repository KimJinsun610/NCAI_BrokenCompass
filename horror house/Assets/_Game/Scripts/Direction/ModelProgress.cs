using System;

namespace NightDuty
{
    /// <summary>
    /// 과학실 몬스터 인체 모형의 자리 진행(2026-10-04 42차, 민 — 「첫날부터 정지한 모습으로 과학실에 있다가, 점점 움직이다가 복도에서 급습」).
    /// 자리 0 테이프 안·등 돌림 → 1 테이프 안·문을 봄 → 2 테이프 밖 통로 → 3 과학실 앞 복도 끝. 씬 연출은 <c>ScienceModel</c>.
    /// </summary>
    public static class ModelProgress
    {
        /// <summary>복도 자리.</summary>
        public const int HallSpot = 3;

        /// <summary>
        /// 그 밤의 시작 자리와 최대 자리. 시작 = max(일차 1일 0·2일 1·3일~ 2, 조도·배치 중 큰 구간 1 → 1, 2 이상 → 2).
        /// 최대 = 모형 급습이 가능한 밤(조도 3 + 배치 2, 또는 그 밤 편성에 모형 급습)이면 복도(3), 1일차는 시작 그대로(움직이지 않음), 그 밖에는 시작 + 1(과학실 안 2까지).
        /// </summary>
        public static void Spots(int day, Band illuminance, Band layout, bool rushTonight, out int start, out int max)
        {
            int byDay = day <= 1 ? 0 : (day == 2 ? 1 : 2);
            Band high = illuminance > layout ? illuminance : layout;
            int byBand = high >= Band.Band2 ? 2 : (high >= Band.Band1 ? 1 : 0);
            start = Math.Max(byDay, byBand);
            if (rushTonight || (illuminance >= Band.Band3 && layout >= Band.Band2)) max = HallSpot;
            else if (day <= 1) max = start;
            else max = Math.Min(start + 1, 2);
            if (max < start) max = start;
        }
    }
}

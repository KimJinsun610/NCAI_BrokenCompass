using System;

namespace NightDuty
{
    /// <summary>
    /// 66차(민: 「조도 축이 오르면 전체 조명이 미세하게 붉어지고, 수칙의 빨간 불빛은 초록으로」) — 천장 등 빛 색이 그 공간의 조도 <b>연출 구간</b>만큼 붉은 쪽으로 옮겨 가는 정도.
    /// 화면(<c>IlluminanceMap</c>)이 읽는다. 판정과 무관하다.
    /// </summary>
    public static class IlluminanceTint
    {
        private static readonly float[] AmountByBand = { 0f, 0.08f, 0.16f, 0.26f, 0.36f };

        /// <summary>붉은 쪽 색(빨강 · 초록 · 파랑).</summary>
        public const float R = 1f;

        /// <summary>붉은 쪽 색 초록 성분.</summary>
        public const float G = 0.32f;

        /// <summary>붉은 쪽 색 파랑 성분.</summary>
        public const float B = 0.26f;

        /// <summary>구간에 따른 섞는 정도(0~1). 「미세하게」 — 4구간도 36%.</summary>
        public static float Amount(Band band)
        {
            return AmountByBand[Math.Max(0, Math.Min(4, (int)band))];
        }
    }
}

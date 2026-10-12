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

        // ── 103차(사용자: 「조도 구간 3에 구간 4처럼 일부 조명만 빨개지거나 깜빡이는 현상」) ──

        /// <summary>일부 등만 붉어지거나 깜빡이기 시작하는 구간(구간 4 붉은 화면의 예고).</summary>
        public const Band AccentFrom = Band.Band3;

        private static readonly float[] AccentShareByBand = { 0f, 0f, 0f, 0.25f, 0.4f };

        /// <summary>그 공간 등 <paramref name="lamps"/>개 중 붉어지거나 깜빡일 등 수 — 구간 3 4분의 1 · 구간 4 5분의 2(등이 있으면 적어도 하나).</summary>
        public static int AccentCount(int lamps, Band band)
        {
            if (lamps <= 0 || band < AccentFrom) return 0;
            float share = AccentShareByBand[Math.Max(0, Math.Min(4, (int)band))];
            return Math.Min(lamps, Math.Max(1, (int)Math.Round(lamps * share)));
        }

        /// <summary>고른 순서(0부터)에서 붉은 등인지 — 첫째는 깜빡이고 둘째는 붉고, 번갈아.</summary>
        public static bool AccentRed(int order)
        {
            return order % 2 == 1;
        }

        /// <summary>등 고르는 순위(작을수록 먼저). 등 자리 열쇠와 일차로 섞는다 — 밤마다 다른 등, 같은 밤에는 늘 같은 등.</summary>
        /// <param name="lampKey">등 자리에서 만든 열쇠(위치 해시).</param>
        /// <param name="day">근무 일차.</param>
        public static uint AccentRank(int lampKey, int day)
        {
            unchecked
            {
                uint h = (uint)lampKey * 2654435761u ^ (uint)(day + 1) * 2246822519u;
                h ^= h >> 15;
                h *= 2246822519u;
                h ^= h >> 13;
                return h;
            }
        }
    }
}

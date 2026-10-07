using System;

namespace NightDuty
{
    /// <summary>
    /// 몸 계기 규칙(최종 기획서 「몸 계기 — 생존 수치를 계단으로 들려 줍니다」, 2′단계, 2026-10-04 37차).
    /// 몸은 <b>생존 수치</b>(0~100)를 따르되 연속으로 움직이지 않고 경계에서 층이 하나씩 붙는다 — 체력바처럼 읽히지 않게.
    /// <list type="table">
    /// <item><term>층 0 (0–24)</term><description>들리지 않음 · 에코 없음</description></item>
    /// <item><term>층 1 (25–49)</term><description>심박 62 조용히 · 눈 깜빡임 70~110초 · 에코 1회</description></item>
    /// <item><term>층 2 (50–74)</term><description>심박 72 + 귀 먹먹함 · 깜빡임 40~70초 · 에코 2회, 잔향 ×1.15</description></item>
    /// <item><term>층 3 (75–89)</term><description>심박 86 + 이명 · 깜빡임 22~40초 · 에코 3회, 잔향 ×1.3</description></item>
    /// <item><term>층 4 (90–99)</term><description>심박 104, 화면이 박동에 맞춰 흔들림 · 깜빡임 10~20초 · 에코가 한 박 늦게 걸어옴</description></item>
    /// </list>
    /// 축마다 그 축의 감각 하나로만 말한다(56차, 민: 「축의 컨셉에 맞게」) — 청각 = 귀(심박·먹먹함·이명, 소극적일 때 먼 숨), 조도 = 눈(깜빡임), 배치 = 공간(내 발소리의 에코).
    /// 경계 신호: 생존 수치가 70·85를 <b>그 밤 처음</b> 넘을 때 그 축의 언어로 한 번(청각 심박 급등 2초 · 조도 느린 깜빡임 · 배치 발소리 하나 더).
    /// 재시작 뒤 30초는 붙잡힌 축의 몸 신호가 한 층 약하게 시작한다. 화면 쪽(<c>BodyMeter</c>·<c>PlayerFootsteps</c>)이 이 규칙만 읽는다.
    /// </summary>
    public static class BodyRules
    {
        /// <summary>층 경계(이 값부터 층 1·2·3·4).</summary>
        public static readonly int[] TierFloors = { 25, 50, 75, 90 };

        /// <summary>경계 신호를 내는 생존 수치.</summary>
        public static readonly int[] Boundaries = { 70, 85 };

        /// <summary>재시작 뒤 붙잡힌 축이 한 층 약하게 시작하는 시간(초).</summary>
        public const float RestartWeakSeconds = 30f;

        /// <summary>생존 수치의 층(0~4).</summary>
        public static int Tier(int value)
        {
            int tier = 0;
            for (int i = 0; i < TierFloors.Length; i++)
            {
                if (value >= TierFloors[i]) tier = i + 1;
            }

            return tier;
        }

        /// <summary>재시작 직후처럼 한 층 약하게.</summary>
        public static int Weakened(int tier)
        {
            return Math.Max(0, tier - 1);
        }

        /// <summary><paramref name="before"/> → <paramref name="after"/>로 오르며 처음 넘은 경계의 비트(1 = 70, 2 = 85). <paramref name="alreadyCrossed"/> 비트는 다시 내지 않는다.</summary>
        public static int CrossedBoundaries(int before, int after, int alreadyCrossed)
        {
            int bits = 0;
            for (int i = 0; i < Boundaries.Length; i++)
            {
                int bit = 1 << i;
                if ((alreadyCrossed & bit) != 0) continue;
                if (before < Boundaries[i] && after >= Boundaries[i]) bits |= bit;
            }

            return bits;
        }

        /// <summary>청각 층의 심박(bpm). 층 0이면 0(들리지 않음).</summary>
        public static int HeartRate(int tier)
        {
            switch (tier)
            {
                case 1: return 62;
                case 2: return 72;
                case 3: return 86;
                case 4: return 104;
                default: return 0;
            }
        }

        /// <summary>귀 먹먹함이 깔리는지(청각 층 2부터 — 위 층은 아래 층의 소리에 더해진다).</summary>
        public static bool EarPressure(int tier)
        {
            return tier >= 2;
        }

        /// <summary>이명이 깔리는지(청각 층 3부터).</summary>
        public static bool Tinnitus(int tier)
        {
            return tier >= 3;
        }

        /// <summary>화면이 박동에 맞춰 흔들리는지(청각 층 4).</summary>
        public static bool PulseShake(int tier)
        {
            return tier >= 4;
        }

        /// <summary>청각 층의 먼 숨 주기(초, 53차부터 실제 간격은 <c>BodyMeter</c>가 55~120초로 늘림). 층 0이면 0(들리지 않음). 56차에 조도에서 청각으로 옮겼다 — 숨은 소리다.</summary>
        public static float BreathInterval(int tier)
        {
            switch (tier)
            {
                case 1: return 10f;
                case 2: return 8f;
                case 3: return 6f;
                case 4: return 4.5f;
                default: return 0f;
            }
        }

        /// <summary>눈 깜빡임 한 번(초) — 화면이 잠깐 감긴다.</summary>
        public const float BlinkSeconds = 0.12f;

        /// <summary>경계 신호의 느린 깜빡임(초).</summary>
        public const float SlowBlinkSeconds = 0.6f;

        /// <summary>조도 층의 눈 깜빡임 간격(초, 최소·최대). 층 0이면 둘 다 0(깜빡이지 않음). 층이 오를수록 잦아진다(56차).</summary>
        public static void BlinkGap(int tier, out float min, out float max)
        {
            switch (tier)
            {
                case 1: min = 70f; max = 110f; return;
                case 2: min = 40f; max = 70f; return;
                case 3: min = 22f; max = 40f; return;
                case 4: min = 10f; max = 20f; return;
                default: min = 0f; max = 0f; return;
            }
        }

        /// <summary>깜빡임 중 화면 가림 정도(0~1). <paramref name="t"/>는 0~1 진행 — 40% 감고 20% 머물고 40% 뜬다.</summary>
        public static float BlinkCover(float t)
        {
            if (t <= 0f || t >= 1f) return 0f;
            if (t < 0.4f) return t / 0.4f;
            if (t < 0.6f) return 1f;
            return (1f - t) / 0.4f;
        }

        /// <summary>배치 층의 발소리 에코 횟수.</summary>
        public static int EchoCount(int tier)
        {
            switch (tier)
            {
                case 1: return 1;
                case 2: return 2;
                case 3:
                case 4: return 3;
                default: return 0;
            }
        }

        /// <summary>배치 층의 잔향 배율(에코 간격이 이만큼 늘어난다).</summary>
        public static float EchoSpread(int tier)
        {
            if (tier >= 3) return 1.3f;
            if (tier == 2) return 1.15f;
            return 1f;
        }

        /// <summary>에코가 한 박 늦게 걸어오는지(배치 층 4 — 누가 뒤에서 따라 걷는 것처럼).</summary>
        public static bool EchoLate(int tier)
        {
            return tier >= 4;
        }
    }
}

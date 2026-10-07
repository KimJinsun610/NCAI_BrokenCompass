using System;
using System.Text;

namespace NightDuty
{
    /// <summary>
    /// 손전등 배터리 규칙(56차, 민: 「서랍이나 여닫는 칸에 배터리를 놓고 손전등을 충전하는 식 — 점검을 하면서 배터리도 간간히 파밍」).
    /// 설계 문서 「야간근무 손전등 배터리 설계안」의 값이다. 상태는 <see cref="FlashlightBattery"/>, 놓는 자리는 <see cref="BatteryPlan"/>,
    /// 빛·입력·줍기는 화면 쪽(<c>FlashlightPower</c>)이 맡는다.
    /// <list type="bullet">
    /// <item>완충 = 켜 둔 채 실제 <see cref="FullSeconds"/>초. 켜 둔 동안만 닳는다. 밤 시작 100%, 예비 0개.</item>
    /// <item>예비는 주머니에 <see cref="PocketMax"/>개까지. R로 갈면 100%가 되고 남은 양은 버린다(언제 갈지가 판단이 된다).
    /// 가는 동안 <see cref="SwapSeconds"/>초 꺼진다 — 그 짧은 어둠이 이 장치의 공포 순간이다.</item>
    /// <item><see cref="DimBelow"/> 아래 빛이 누렇게 약해지고(세기·거리가 0%에 가까울수록 <see cref="DimIntensity"/>·<see cref="DimRange"/>배) 가끔 진짜로 깜빡인다.
    /// <see cref="LowBelow"/> 아래 깜빡임이 잦아지고 비춤 판정 거리가 <see cref="LowBeamRange"/>m.</item>
    /// <item>0%면 꺼진다(그때만 기존 「꺼짐」 판정 신호). 희미해도 판정은 「켜짐」 — 수칙 판정기는 고칠 것이 없다.</item>
    /// <item>그날 칸에 놓이는 수 <see cref="PlacedOn"/>: 1·2일 3 · 3·4일 2 · 5일 1(5일차는 다 찾아도 모자라게).</item>
    /// </list>
    /// </summary>
    public static class BatteryRules
    {
        /// <summary>
        /// 완충으로 켜 둘 수 있는 실제 초. 59차(밤 15분 → 10분, 민: 「손전등 밸런스 조절도」): 240 → 160 — 밤 길이와 같은 2/3로 줄여
        /// 「다 찾으면 1·2일 넉넉(약 1.3배) · 3·4일 빠듯(약 1배) · 5일 모자람(약 0.7배)」 비율을 그대로 둔다.
        /// </summary>
        public const float FullSeconds = 160f;

        /// <summary>주머니에 들 수 있는 예비 수.</summary>
        public const int PocketMax = 2;

        /// <summary>갈아 끼우는 동안 꺼져 있는 초.</summary>
        public const float SwapSeconds = 1.2f;

        /// <summary>이 충전량 아래면 빛이 약해지고 가끔 깜빡인다.</summary>
        public const float DimBelow = 0.30f;

        /// <summary>이 충전량 아래면 깜빡임이 잦고 비춤 판정 거리가 짧다.</summary>
        public const float LowBelow = 0.10f;

        /// <summary>0%에 가까울 때의 빛 세기 배율.</summary>
        public const float DimIntensity = 0.45f;

        /// <summary>0%에 가까울 때의 빛 거리 배율.</summary>
        public const float DimRange = 0.60f;

        /// <summary><see cref="LowBelow"/> 아래의 비춤 판정 거리(m).</summary>
        public const float LowBeamRange = 5f;

        /// <summary>가짜 놀람 「손전등 끊김」은 이 충전량 이상일 때만 — 배터리 탓으로 읽히면 놀라지 않는다.</summary>
        public const float FakeFlickerMinCharge = 0.5f;

        /// <summary>밤마다 풀어 두는 복도 사물함 수(어느 것이 풀렸는지는 열어 봐야 안다).</summary>
        public const int UnlockedLockers = 3;

        /// <summary>
        /// 칸으로 쓰려면 몸통이 이만큼 서 있어야 한다(위 방향의 y 성분, cos 약 16°). 56차 QA: 복도의 「Bookcase」 중 다섯은 눕혀 문에 박은 판자,
        /// 하나는 문에 엎어 기댄 책장(19°)이라 열어도 안이 보이지 않는다 — 배터리의 거의 반이 거기 놓였다. 벽에 기댄 사물함(12°)은 칸이다.
        /// </summary>
        public const float MinUpright = 0.96f;

        /// <summary>몸통의 위 방향 y 성분(<paramref name="upY"/>)으로 보아 배터리를 둘 수 있는 칸인지.</summary>
        public static bool Upright(float upY)
        {
            return upY >= MinUpright;
        }

        /// <summary>그날 칸에 놓는 배터리 수.</summary>
        public static int PlacedOn(int day)
        {
            if (day <= 2) return 3;
            if (day <= 4) return 2;
            return 1;
        }

        /// <summary>충전량(0~1)의 빛 세기 배율.</summary>
        public static float IntensityScale(float charge)
        {
            return Scale(charge, DimIntensity);
        }

        /// <summary>충전량(0~1)의 빛 거리 배율.</summary>
        public static float RangeScale(float charge)
        {
            return Scale(charge, DimRange);
        }

        /// <summary>충전량(0~1)의 누런 정도(0 = 본래 색, 1 = 가장 누렇게).</summary>
        public static float Yellowing(float charge)
        {
            if (charge >= DimBelow) return 0f;
            return 1f - Clamp01(charge / DimBelow);
        }

        /// <summary>비춤 판정 거리(m).</summary>
        public static float BeamRange(float charge)
        {
            return charge < LowBelow ? LowBeamRange : SensingRules.BeamRange;
        }

        /// <summary>진짜 깜빡임 간격(초, 최소·최대). 깜빡이지 않으면 둘 다 0.</summary>
        public static void FlickerGap(float charge, out float min, out float max)
        {
            if (charge <= 0f || charge >= DimBelow)
            {
                min = 0f;
                max = 0f;
            }
            else if (charge < LowBelow)
            {
                min = 3f;
                max = 6f;
            }
            else
            {
                min = 10f;   // 59차: 15~30 → 10~20(완충 160초 — 30% 아래 구간이 48초라 그 안에 두세 번)
                max = 20f;
            }
        }

        /// <summary>태블릿 상태바 칸 수(0~3).</summary>
        public static int Cells(float charge)
        {
            if (charge <= 0f) return 0;
            int n = (int)Math.Ceiling(charge * 3f - 1e-4f);
            return Math.Max(1, Math.Min(3, n));
        }

        /// <summary>태블릿 상태바 문자열(신호 표시 뒤에 손전등 칸 + 예비 수).</summary>
        public static string StatusText(string signal, float charge, int spare)
        {
            StringBuilder sb = new StringBuilder(signal ?? string.Empty);
            if (sb.Length > 0) sb.Append("  ");
            int cells = Cells(charge);
            for (int i = 0; i < 3; i++) sb.Append(i < cells ? '■' : '□');
            if (spare > 0) sb.Append(" +").Append(spare);
            return sb.ToString();
        }

        private static float Scale(float charge, float floor)
        {
            if (charge >= DimBelow) return 1f;
            float k = Clamp01(charge / DimBelow);
            return floor + (1f - floor) * k;
        }

        private static float Clamp01(float v)
        {
            return v < 0f ? 0f : (v > 1f ? 1f : v);
        }
    }
}

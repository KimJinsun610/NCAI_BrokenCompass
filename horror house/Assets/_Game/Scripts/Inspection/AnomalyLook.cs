using System;

namespace NightDuty
{
    /// <summary>
    /// 점검 이상의 <b>보이는 정도</b>(최종 기획서 「공간별 설계」 — 「구간이 오를수록 이상은 뚜렷해지고, 옮김 이상은 최소 15° 회전 또는 실루엣이 바뀔 만큼 움직입니다」, 2026-10-04 41차).
    /// 강도는 <see cref="InspectionAssignment.Intensity"/>(그 축의 연출 구간, 최소 1)이고, 여기 수치는 구간이 오를수록 커지기만 한다.
    /// 씬 연출(<c>InspectionAnomalies</c>)이 이 값으로 문 각도·웅덩이 길이·빛 세기를 고른다. <b>판정과 무관하다.</b>
    /// </summary>
    public static class AnomalyLook
    {
        /// <summary>[옮김]의 최소 회전(도).</summary>
        public const float MinMoveDegrees = 15f;

        /// <summary>광과민 옵션의 점멸 상한(Hz). 점멸이 단서인 T-3은 이 아래의 느린 맥동으로 바뀐다.</summary>
        public const float SlowFlickerMaxHz = 2f;

        private static readonly float[] DoorDegreesByBand = { 0f, 20f, 35f, 55f, 85f };
        private static readonly float[] TurnDegreesByBand = { 0f, 20f, 45f, 100f, 180f };
        private static readonly float[] PullMetersByBand = { 0f, 0.35f, 0.45f, 0.55f, 0.7f };
        private static readonly float[] SpreadMetersByBand = { 0f, 0.6f, 1.4f, 2.2f, 3.4f };
        private static readonly float[] GlowByBand = { 0f, 0.8f, 1.2f, 1.7f, 2.4f };
        private static readonly float[] FlickerHzByBand = { 0f, 1.5f, 2.5f, 4f, 6f };
        private static readonly float[] SlowFlickerHzByBand = { 0f, 0.5f, 0.8f, 1.1f, 1.5f };
        private static readonly float[] FarPickByBand = { 0f, 0.4f, 0.6f, 0.8f, 1f };

        /// <summary>구간 → 0~1(구간 0은 0).</summary>
        public static float Strength(Band band)
        {
            return Index(band) / 4f;
        }

        /// <summary>H-4 사물함 문이 열린 각도.</summary>
        public static float DoorDegrees(Band band)
        {
            return DoorDegreesByBand[Index(band)];
        }

        /// <summary>S-1 인체 모형이 돌아간 각도(4구간이면 완전히 등을 돌린다).</summary>
        public static float TurnDegrees(Band band)
        {
            return TurnDegreesByBand[Index(band)];
        }

        /// <summary>L-1 의자가 빠져나온 거리(m).</summary>
        public static float PullMeters(Band band)
        {
            return PullMetersByBand[Index(band)];
        }

        /// <summary>H-2·T-1 물이 번진 길이(m) — 웅덩이 → 줄기 → 침수.</summary>
        public static float SpreadMeters(Band band)
        {
            return SpreadMetersByBand[Index(band)];
        }

        /// <summary>[빛] 이상의 빛 세기 배율.</summary>
        public static float Glow(Band band)
        {
            return GlowByBand[Index(band)];
        }

        /// <summary>T-3 거울 위 형광등이 깜빡이는 빈도(초당). 광과민 옵션이면 <see cref="SlowFlickerMaxHz"/> 아래의 맥동.</summary>
        public static float FlickerHz(Band band, bool photosensitiveSafe)
        {
            return photosensitiveSafe ? SlowFlickerHzByBand[Index(band)] : FlickerHzByBand[Index(band)];
        }

        /// <summary>C-1 화분이 옮겨 간 책상 — 가까운 책상(0)부터 가장 먼 책상(1)까지 중 어디인지.</summary>
        public static float FarPick(Band band)
        {
            return FarPickByBand[Index(band)];
        }

        /// <summary>그 항목이 눈에 보이는 이상 연출을 갖는지([소리] 틀은 소리만).</summary>
        public static bool HasLook(InspectionItem item)
        {
            return item != null && item.Template != AnomalyTemplate.Sound;
        }

        private static int Index(Band band)
        {
            return Math.Max(0, Math.Min(4, (int)band));
        }
    }
}

namespace NightDuty
{
    /// <summary>
    /// 수치 변화량 표(2026-09-30 새 기획서 「수치 표」). <b>변화량 숫자는 여기 한 곳에만 둔다.</b>
    /// 카드 에셋의 개별 델타(RuleSO)는 이 값을 초기값으로 쓰고, 특별한 카드만 따로 적는다.
    /// <para>
    /// 감각 축(청각·조도·배치)은 <b>생존 수치</b>에 얹힌다. 생존 수치는 붙잡힘(100)에만 쓰고,
    /// 연출 구간은 <see cref="BandResolver"/>가 따로 정한다(내려가지 않음).
    /// </para>
    /// </summary>
    public static class Deltas
    {
        // ── 감각 축 +(생존 수치) ────────────────────────────────

        /// <summary>근무수칙 위반.</summary>
        public const int RuleViolation = 12;

        /// <summary>위협 대응 실패(조우·추격 수칙).</summary>
        public const int ThreatFailure = 20;

        /// <summary>점검 수칙 위반. 가벼운 놀람 연출이 함께 나간다.</summary>
        public const int InspectionRuleViolation = 6;

        /// <summary>이상을 정상으로 보고(놓침).</summary>
        public const int MissedAnomaly = 8;

        /// <summary>경고 누적 처벌 이벤트 — 가장 높은 감각 축에 준다.</summary>
        public const int Punishment = 15;

        // ── 감각 축 −(생존 수치) ────────────────────────────────

        /// <summary>이상을 정확히 보고. 그 이상의 축에서 뺀다.</summary>
        public const int CorrectReportRelief = 5;

        /// <summary>정확한 보고로 뺄 수 있는 양의 상한 — 축마다, 밤마다.</summary>
        public const int ReliefCapPerAxisPerNight = 10;

        /// <summary>붙잡혀 그 밤을 다시 시작할 때 감각 축마다 빼는 양.</summary>
        public const int RestartRelief = 10;

        // ── 경고 ──────────────────────────────────────────────

        /// <summary>
        /// 경고 누적 한도. 정상을 이상으로 보고했거나 점검을 마치지 못하면 경고 1.
        /// 이 수에 닿으면 처벌 이벤트(<see cref="Punishment"/>)가 나간다.
        /// </summary>
        public const int WarningsForPunishment = 3;

        // ── 신뢰 +(줄지 않는다) ────────────────────────────────

        /// <summary>근무수칙 준수.</summary>
        public const int TrustComply = 2;

        /// <summary>위협 대응 성공.</summary>
        public const int TrustThreatSuccess = 3;

        /// <summary>역보고(태블릿 지시를 거스르고 이상을 보고).</summary>
        public const int TrustReverseReport = 3;

        /// <summary>역설 문자를 무시하고 근무수칙을 지킴.</summary>
        public const int TrustParadoxIgnored = 2;

        /// <summary>변조된 수칙 대신 원본을 지킴.</summary>
        public const int TrustVariantOriginalKept = 3;
    }
}

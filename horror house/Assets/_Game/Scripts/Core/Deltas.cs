namespace NightDuty
{
    /// <summary>
    /// 수치 변화량 표(2026-09-30 최종 기획서 「밸런스 수치표」). <b>변화량 숫자는 여기 한 곳에만 둔다.</b>
    /// 카드 에셋의 개별 델타(RuleSO)는 이 값을 초기값으로 쓰고, 특별한 카드만 따로 적는다.
    /// <para>
    /// 감각 축(청각·조도·배치)은 <b>생존 수치</b>에 얹힌다. 생존 수치는 붙잡힘(100)에만 쓰고,
    /// 연출 구간은 <see cref="BandResolver"/>가 따로 정한다(내려가지 않음).
    /// 재시작 보정·결근 같은 밤 단위 규칙은 <see cref="RestartPolicy"/>에 있다.
    /// </para>
    /// </summary>
    public static class Deltas
    {
        // ── 감각 축 +(생존 수치) ────────────────────────────────

        /// <summary>근무수칙 위반. 위협 수칙이 아닌 조우의 대응 실패도 이 값이다.</summary>
        public const int RuleViolation = 12;

        /// <summary>위협 수칙(H3·H4·C3·S5·T3·L3·L5) 대응 실패. 이때는 +12를 더하지 않는다.</summary>
        public const int ThreatFailure = 20;

        /// <summary>점검 수칙(「가까이」 0.8m) 위반. 그 항목의 축에 주고, 1초짜리 놀람이 함께 나간다.</summary>
        public const int InspectionRuleViolation = 6;

        /// <summary>이상을 정상으로 보고(놓침). 이상이 있던 항목을 끝내 못 했을 때도 경고에 더해 이 값을 준다.</summary>
        public const int MissedAnomaly = 8;

        /// <summary>
        /// 정상을 이상으로 보고(오보). 그 항목의 축에 준다(경고가 아니다).
        /// <para>+5면 서툰 플레이어에게 「보지 않고 [이상]만 누르기」가 정직한 판별보다 싸다 — +7에서 모든 유형·일차가 정직 &lt; 맹목 &lt; 미완료.
        /// 플레이테스트에서 탐지율이 설계보다 낮으면 +10으로 올린다(조정 스위치).</para>
        /// </summary>
        public const int FalseReport = 7;

        /// <summary>환청(머리 안쪽 2D 음)을 [이상]으로 기록 — 공통 수칙 G2 위반. 청각에 준다. 오보를 겹쳐 주지 않는다.</summary>
        public const int HallucinationRecorded = 6;

        /// <summary>경고 누적 처벌 이벤트 — 가장 높은 감각 축에 준다(<see cref="SoftCap"/>에서 멈춘다).</summary>
        public const int Punishment = 15;

        /// <summary>
        /// 붙잡힘을 일으키지 못하는 델타의 상한. 처벌·04:00 정산·판정 정지 구간·03:30 이후의 델타는 여기서 멈춘다
        /// (무한 루프 방지 장치 3 — 밤 시작 직후 즉사 방지).
        /// </summary>
        public const int SoftCap = 95;

        // ── 감각 축 −(생존 수치) ────────────────────────────────

        /// <summary>이상을 정확히 보고. 그 이상의 축에서 뺀다.</summary>
        public const int CorrectReportRelief = 5;

        /// <summary>정확한 보고로 뺄 수 있는 양의 상한 — 축마다, 밤마다.</summary>
        public const int ReliefCapPerAxisPerNight = 10;

        /// <summary>붙잡혀 그 밤을 다시 시작할 때 감각 축마다 빼는 양의 단위. 실제 양은 이 값 × k(<see cref="RestartPolicy"/>).</summary>
        public const int RestartRelief = 10;

        // ── 경고 ──────────────────────────────────────────────

        /// <summary>
        /// 경고 누적 한도. 점검을 마치지 못하면 경고 1.
        /// 이 수에 닿으면 처벌 이벤트(<see cref="Punishment"/>)가 나가고 경고는 초기화된다.
        /// </summary>
        public const int WarningsForPunishment = 3;

        // ── 신뢰 +(줄지 않는다) ────────────────────────────────

        /// <summary>근무수칙 준수. 그 밤 방아쇠가 한 번 이상 발동했고 위반이 없는 수칙에만 준다.</summary>
        public const int TrustComply = 2;

        /// <summary>위협 대응 성공. 이때는 준수 +2를 더하지 않는다.</summary>
        public const int TrustThreatSuccess = 3;

        /// <summary>역보고(T4) — 이상이 보여도 수칙대로 [정상]으로 보고.</summary>
        public const int TrustReverseReport = 3;

        /// <summary>역설 문자를 받고도 근무수칙을 지킴(준수 +2 대신).</summary>
        public const int TrustParadoxKept = 3;

        /// <summary>변조된 수칙을 원본 뜻대로 지킴(준수 +2 대신).</summary>
        public const int TrustVariantKept = 3;
    }
}

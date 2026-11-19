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
        public static int RuleViolation => global::NightDuty.DesignData.SettingInt(390032);   // DT 390032 일반 수칙 위반 기본값(Rule_Table.Violation_Value 기본)

        /// <summary>104차(사용자): 따라오는 발소리(H5) — 2번 이상 뒤돌아본 위반의 청각 증가.</summary>
        public const int FollowViolation = 15;

        /// <summary>위협 수칙(H3·H4·C3·S5·T3·L3·L5) 대응 실패. 이때는 +12를 더하지 않는다.</summary>
        public static int ThreatFailure => global::NightDuty.DesignData.SettingInt(390033);   // DT 390033 위협 수칙 실패 기본값

        /// <summary>점검 수칙(「가까이」 0.8m) 위반. 그 항목의 축에 주고, 1초짜리 놀람이 함께 나간다.</summary>
        public static int InspectionRuleViolation => global::NightDuty.DesignData.SettingInt(390034);   // DT 390034 점검 「가까이」 위반

        /// <summary>이상을 정상으로 보고(놓침). 이상이 있던 항목을 끝내 못 했을 때도 경고에 더해 이 값을 준다.</summary>
        public static int MissedAnomaly => global::NightDuty.DesignData.SettingInt(390035);   // DT 390035 이상을 정상으로 보고(놓침)

        /// <summary>
        /// 정상을 이상으로 보고(오보). 그 항목의 축에 준다(경고가 아니다).
        /// <para>+5면 서툰 플레이어에게 「보지 않고 [이상]만 누르기」가 정직한 판별보다 싸다 — +7에서 모든 유형·일차가 정직 &lt; 맹목 &lt; 미완료.
        /// 플레이테스트에서 탐지율이 설계보다 낮으면 +10으로 올린다(조정 스위치).</para>
        /// </summary>
        public static int FalseReport => global::NightDuty.DesignData.SettingInt(390036);   // DT 390036 정상을 이상으로 보고(오보)


        /// <summary>경고 누적 처벌 이벤트 — 가장 높은 감각 축에 준다(<see cref="SoftCap"/>에서 멈춘다).</summary>
        public static int Punishment => global::NightDuty.DesignData.SettingInt(390037);   // DT 390037 경고 누적 처벌

        /// <summary>
        /// 붙잡힘을 일으키지 못하는 델타의 상한. 처벌·04:00 정산·판정 정지 구간·03:30 이후의 델타는 여기서 멈춘다
        /// (무한 루프 방지 장치 3 — 밤 시작 직후 즉사 방지).
        /// </summary>
        public static int SoftCap => global::NightDuty.DesignData.SettingInt(390038);   // DT 390038 처벌·정산·판정 정지 구간 델타 상한

        // ── 감각 축 −(생존 수치) ────────────────────────────────

        /// <summary>이상을 정확히 보고. 그 이상의 축에서 뺀다.</summary>
        public static int CorrectReportRelief => global::NightDuty.DesignData.SettingInt(390039);   // DT 390039 이상을 정확히 보고하면 감소

        /// <summary>정확한 보고로 뺄 수 있는 양의 상한 — 축마다, 밤마다.</summary>
        public static int ReliefCapPerAxisPerNight => global::NightDuty.DesignData.SettingInt(390040);   // DT 390040 정확 보고 감소 상한(축·밤마다)

        /// <summary>붙잡혀 그 밤을 다시 시작할 때 감각 축마다 빼는 양의 단위. 실제 양은 이 값 × k(<see cref="RestartPolicy"/>).</summary>
        public static int RestartRelief => global::NightDuty.DesignData.SettingInt(390041);   // DT 390041 재시작 k번째 감각축 감소 단위

        // ── 경고 ──────────────────────────────────────────────

        /// <summary>
        /// 경고 누적 한도. 점검을 마치지 못하면 경고 1.
        /// 이 수에 닿으면 처벌 이벤트(<see cref="Punishment"/>)가 나가고 경고는 초기화된다.
        /// </summary>
        public static int WarningsForPunishment => global::NightDuty.DesignData.SettingInt(390042);   // DT 390042 경고 누적 처벌 한도

        // ── 신뢰 +(줄지 않는다) ────────────────────────────────

        /// <summary>근무수칙 준수. 그 밤 방아쇠가 한 번 이상 발동했고 위반이 없는 수칙에만 준다.</summary>
        public static int TrustComply => global::NightDuty.DesignData.SettingInt(390043);   // DT 390043 일반 수칙 밤 종료 준수 기본값(Rule_Table.Comply_Trust 기본)

        /// <summary>위협 대응 성공. 이때는 준수 +2를 더하지 않는다.</summary>
        public static int TrustThreatSuccess => global::NightDuty.DesignData.SettingInt(390044);   // DT 390044 위협 대응 성공 기본값

        /// <summary>역보고(T4) — 이상이 보여도 수칙대로 [정상]으로 보고.</summary>
        public static int TrustReverseReport => global::NightDuty.DesignData.SettingInt(390045);   // DT 390045 T4 역보고 준수

        /// <summary>역설 문자를 받고도 근무수칙을 지킴(준수 +2 대신).</summary>
        public static int TrustParadoxKept => global::NightDuty.DesignData.SettingInt(390046);   // DT 390046 역설 문자를 받고도 준수(+2 대체)

        /// <summary>변조된 수칙을 원본 뜻대로 지킴(준수 +2 대신).</summary>
        public static int TrustVariantKept => global::NightDuty.DesignData.SettingInt(390047);   // DT 390047 변조본을 보고도 원본대로 준수(+2 대체)
    }
}

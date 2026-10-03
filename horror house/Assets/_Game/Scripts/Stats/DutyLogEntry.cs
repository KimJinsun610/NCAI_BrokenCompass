namespace NightDuty
{
    /// <summary>
    /// 결과창 「금일 근무 지침」 한 줄에 붙는 표시(최종 기획서 「근무일지 정산 화면」: 준수 / 어김 / 지시를 따름 / 불가피).
    /// 「불가피」는 회피 불가 역설(10단계)이 들어오면 더한다.
    /// </summary>
    public enum DutyMark
    {
        /// <summary>표시 없음. 지켰거나, 방아쇠가 오지 않았다.</summary>
        None = 0,

        /// <summary>「어김」. 수칙을 어겼다.</summary>
        Struck = 1,

        /// <summary>
        /// 「지시를 따름」. 그날 이 수칙을 겨눈 역설 문자를 받고 따라서 어겼다.
        /// <b>수치 손해는 <see cref="Struck"/>과 똑같다</b> — 다른 것은 이름뿐이다.
        /// </summary>
        Instructed = 2
    }

    /// <summary>새 수칙 한 장의 그날 결과(근무일지용).</summary>
    public enum RuleVerdict
    {
        /// <summary>방아쇠가 오지 않았다(판정 기회 없음).</summary>
        NotTriggered = 0,

        /// <summary>방아쇠가 왔고 어기지 않았다(위협 대응 성공·밤 종료 준수 포함).</summary>
        Complied = 1,

        /// <summary>한 번이라도 어겼다.</summary>
        Violated = 2
    }

    /// <summary>
    /// 근무 일지(결과창 「금일 근무 지침」)의 한 줄 재료. 그날 편성 덱 순서대로 하나씩 만든다(<see cref="NightRun.BuildSummary"/>).
    /// <para>
    /// 결과창에는 <see cref="PlayerText"/>와 <see cref="Mark"/>(또는 <see cref="Struck"/>)만 쓴다. <see cref="RuleId"/>와 <see cref="Verdict"/>는
    /// 개발 로그·검증용이며 <b>플레이어 화면에 표시하지 않는다</b>. <b>수치는 절대 실리지 않는다.</b>
    /// </para>
    /// <para>2026-10-03: 「가지 않은 공간의 수칙에도 빨간 줄」(옛 24장 규칙)을 없앴다 — 최종 기획서의 정산은 수칙별 준수·어김뿐이다.</para>
    /// </summary>
    public readonly struct DutyLogEntry
    {
        /// <summary>그날 덱 순서(1부터).</summary>
        public readonly int Number;

        /// <summary>수칙 ID(H1 등). 화면 표시 금지.</summary>
        public readonly string RuleId;

        /// <summary>수칙이 속한 공간(공통 수칙은 None).</summary>
        public readonly SpaceId Space;

        /// <summary>태블릿에 실린 수칙 본문 그대로.</summary>
        public readonly string PlayerText;

        /// <summary>그날 결과.</summary>
        public readonly RuleVerdict Verdict;

        /// <summary>그날 이 수칙을 겨눈 역설 문자를 받았는지. 받은 상태에서 어기면 표시가 「지시를 따름」이 된다.</summary>
        public readonly bool Instructed;

        /// <summary>빨간 줄 여부 — 어겼다.</summary>
        public bool Struck
        {
            get { return Verdict == RuleVerdict.Violated; }
        }

        /// <summary>결과창에 그릴 표시. 어긴 줄 가운데 역설 문자를 받은 것만 따로 구분한다.</summary>
        public DutyMark Mark
        {
            get
            {
                if (!Struck)
                {
                    return DutyMark.None;
                }

                return Instructed ? DutyMark.Instructed : DutyMark.Struck;
            }
        }

        /// <summary>한 줄을 만든다.</summary>
        public DutyLogEntry(int number, string ruleId, SpaceId space, string playerText, RuleVerdict verdict, bool instructed)
        {
            Number = number;
            RuleId = ruleId ?? string.Empty;
            Space = space;
            PlayerText = playerText ?? string.Empty;
            Verdict = verdict;
            Instructed = instructed;
        }
    }
}

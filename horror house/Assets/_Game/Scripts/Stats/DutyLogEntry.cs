namespace NightDuty
{
    /// <summary>
    /// 근무 일지(결과창 「금일 근무 지침」)의 한 줄 재료. 그날 덱 순서대로 하나씩 만든다.
    /// <para>
    /// 결과창에는 <see cref="PlayerText"/>와 <see cref="Struck"/>만 쓴다. <see cref="CardId"/>와 <see cref="State"/>는
    /// 개발 로그·검증용이며 <b>플레이어 화면에 표시하지 않는다</b>.
    /// </para>
    /// </summary>
    public readonly struct DutyLogEntry
    {
        /// <summary>그날 덱 표시 순서(1부터). 태블릿 지침 번호와 같다.</summary>
        public readonly int Number;

        /// <summary>카드 ID(H1 등). 화면 표시 금지.</summary>
        public readonly string CardId;

        /// <summary>카드가 속한 공간.</summary>
        public readonly SpaceId Space;

        /// <summary>태블릿에 실린 수칙 본문 그대로.</summary>
        public readonly string PlayerText;

        /// <summary>그날 마지막 판정 상태. 결과가 없으면 <see cref="CardState.Waiting"/>.</summary>
        public readonly CardState State;

        /// <summary>그날 해당 공간에 들어간 적이 있는지.</summary>
        public readonly bool Visited;

        /// <summary>
        /// 빨간 줄 여부 — 위반했거나, 그 공간에 가지 않았다.
        /// 준수·미판정·조건 미충족은 모두 표시 없음이다(플레이어가 무엇을 지켰는지 드러내지 않는다).
        /// </summary>
        public bool Struck
        {
            get { return State == CardState.Violated || !Visited; }
        }

        /// <summary>한 줄을 만든다.</summary>
        public DutyLogEntry(int number, string cardId, SpaceId space, string playerText, CardState state, bool visited)
        {
            Number = number;
            CardId = cardId ?? string.Empty;
            Space = space;
            PlayerText = playerText ?? string.Empty;
            State = state;
            Visited = visited;
        }
    }
}

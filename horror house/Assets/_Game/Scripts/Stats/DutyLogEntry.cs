namespace NightDuty
{
    /// <summary>
    /// 결과창 「금일 근무 지침」 한 줄에 붙는 표시. 2026-09-21 재설계로 두 가지에서 <b>세 가지</b>가 됐다.
    /// </summary>
    public enum DutyMark
    {
        /// <summary>표시 없음. 지켰거나, 판정이 시작되지 않았다.</summary>
        None = 0,

        /// <summary>「어김」. 수칙을 그냥 어겼거나, 그 공간에 가지 않았다.</summary>
        Struck = 1,

        /// <summary>
        /// 「지시를 따름」. 그날 이 수칙의 짝 역설 문자를 받은 상태에서 어겼다.
        /// <b>수치 손해는 <see cref="Struck"/>과 똑같다</b> — 다른 것은 이름뿐이다.
        /// </summary>
        Instructed = 2
    }

    /// <summary>
    /// 근무 일지(결과창 「금일 근무 지침」)의 한 줄 재료. 그날 덱 순서대로 하나씩 만든다.
    /// <para>
    /// 결과창에는 <see cref="PlayerText"/>와 <see cref="Mark"/>만 쓴다. <see cref="CardId"/>와 <see cref="State"/>는
    /// 개발 로그·검증용이며 <b>플레이어 화면에 표시하지 않는다</b>.
    /// </para>
    /// <para>
    /// <b>수치는 절대 실리지 않는다.</b> 결산은 판정이 드러나는 유일한 창구이지만,
    /// 드러나는 것은 「어겼다 / 지시를 따랐다」라는 <b>말</b>이지 축 값이나 델타가 아니다(기획서 2.8절).
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
        /// 그날 이 수칙의 짝 역설 문자를 받았는지. 받은 상태에서 어기면 표시가 「지시를 따름」이 된다.
        /// </summary>
        public readonly bool Instructed;

        /// <summary>
        /// 빨간 줄 여부 — 위반했거나, 그 공간에 가지 않았다.
        /// 준수·미판정·조건 미충족은 모두 표시 없음이다(플레이어가 무엇을 지켰는지 드러내지 않는다).
        /// <para>기존 결과창을 위해 남겨 둔 값이다. 새 화면은 <see cref="Mark"/>를 쓴다.</para>
        /// </summary>
        public bool Struck
        {
            get { return State == CardState.Violated || !Visited; }
        }

        /// <summary>
        /// 결과창에 그릴 표시. 어긴 줄 가운데 <b>역설 문자를 받은 것만</b> 따로 구분한다.
        /// <para>
        /// 공간에 가지 않은 줄은 문자를 받았더라도 「어김」이다 — 가지 않았으면 지시를 따른 것이 아니다.
        /// </para>
        /// </summary>
        public DutyMark Mark
        {
            get
            {
                if (!Struck)
                {
                    return DutyMark.None;
                }

                return Instructed && State == CardState.Violated ? DutyMark.Instructed : DutyMark.Struck;
            }
        }

        /// <summary>한 줄을 만든다.</summary>
        public DutyLogEntry(int number, string cardId, SpaceId space, string playerText, CardState state, bool visited, bool instructed)
        {
            Number = number;
            CardId = cardId ?? string.Empty;
            Space = space;
            PlayerText = playerText ?? string.Empty;
            State = state;
            Visited = visited;
            Instructed = instructed;
        }

        /// <summary>역설 문자를 고려하지 않는 예전 형태. 기존 호출부를 위해 남겨 둔다.</summary>
        public DutyLogEntry(int number, string cardId, SpaceId space, string playerText, CardState state, bool visited)
            : this(number, cardId, space, playerText, state, visited, false)
        {
        }
    }
}

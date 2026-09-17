namespace NightDuty
{
    /// <summary>
    /// 근무수칙 카드의 하룻밤 상태. 확정 기획서 공통 명세 1절의 상태표를 그대로 따른다.
    /// <b>대기 상태를 성공으로 계산하지 않는다.</b>
    /// </summary>
    public enum CardState
    {
        /// <summary>배정됐고 단서·선택 기회를 기다린다. 델타 없음.</summary>
        Waiting = 0,

        /// <summary>진행 중. 대상·단서·유예·응시 시간·완료 조건을 추적한다. 아직 미정산.</summary>
        Active = 1,

        /// <summary>준수로 정산됐다. 그날 재진입은 무시한다.</summary>
        Complied = 2,

        /// <summary>위반으로 정산됐다. 그날 재진입은 무시한다.</summary>
        Violated = 3,

        /// <summary>미판정. 선택 기회가 없었거나 대상 참조 누락 등으로 중단됐다. 델타 0.</summary>
        Undetermined = 4,

        /// <summary>어느 축이든 100에 도달해 신규 판정·정산이 중단됐다.</summary>
        Locked = 5
    }

    /// <summary>
    /// 준수 정산 시점. 위반은 언제나 즉시 정산한다.
    /// <para>
    /// 기획서의 「통행 종료 / 퇴실 / 점검 완료」는 준수 조건 자체가 그 신호이므로 <see cref="WhenSuccessMet"/>로 표현한다.
    /// 「밤 종료까지 금지를 지키면 준수」(H4, T6 등)는 <see cref="AtNightEnd"/>다.
    /// </para>
    /// </summary>
    public enum SettleAt
    {
        /// <summary>준수 조건이 성립한 순간 정산한다.</summary>
        WhenSuccessMet = 0,

        /// <summary>
        /// 준수 조건은 기록만 해 두고(예: 안전 통행 1회), 밤 종료에 정산한다.
        /// 그때까지 위반 감시는 계속된다. 준수 조건이 한 번도 성립하지 않았다면 미판정이다.
        /// </summary>
        AtNightEnd = 1
    }

    /// <summary>
    /// 카드 한 장의 정산 결과. 개발 로그와 근무 종료 리뷰에 카드별로 분리 저장한다.
    /// 플레이 중 화면에 표시하지 않는다.
    /// </summary>
    public readonly struct RuleResult
    {
        /// <summary>카드 ID(H1 등).</summary>
        public readonly string CardId;

        /// <summary>정산 후 상태(준수·위반·미판정).</summary>
        public readonly CardState State;

        /// <summary>델타를 적용할 축. 미판정이면 의미 없음.</summary>
        public readonly FearAxis Axis;

        /// <summary>적용할 델타. 미판정이면 0.</summary>
        public readonly int Delta;

        /// <summary>카드가 속한 공간.</summary>
        public readonly SpaceId Space;

        /// <summary>원인·사유(개발 로그용).</summary>
        public readonly string Reason;

        /// <summary>결과를 만든다.</summary>
        public RuleResult(string cardId, CardState state, FearAxis axis, int delta, SpaceId space, string reason)
        {
            CardId = cardId;
            State = state;
            Axis = axis;
            Delta = delta;
            Space = space;
            Reason = reason ?? string.Empty;
        }

        /// <summary>델타가 실제로 축에 영향을 주는 결과인지.</summary>
        public bool HasDelta
        {
            get { return Delta > 0 && (State == CardState.Complied || State == CardState.Violated); }
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return CardId + " " + State + " " + Axis + " +" + Delta + " (" + Reason + ")";
        }
    }
}

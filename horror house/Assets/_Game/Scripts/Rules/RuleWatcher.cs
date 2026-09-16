namespace NightDuty
{
    /// <summary>
    /// 카드 한 장의 하룻밤 상태기계. 대기 → 진행 중 → 준수/위반/미판정, 그리고 종료 잠금.
    /// <para>
    /// 규약(기획서 공통 명세 1절):
    /// 위반은 즉시 한 번 정산한다. 준수는 카드의 정산 시점에만 지급한다.
    /// 같은 신호에서 준수와 위반이 함께 성립하면 위반이 우선한다.
    /// 정산한 결과는 되돌리지 않으며, 그날 재진입은 무시한다.
    /// </para>
    /// </summary>
    public sealed class RuleWatcher
    {
        private ConditionState _successState;
        private ConditionState _failureState;
        private ConditionState _cancelState;
        private bool _successMet;

        /// <summary>감시 중인 카드.</summary>
        public RuleSO Card { get; }

        /// <summary>그날 덱에서의 표시 순서(0부터). 동시 처리 순서의 기준이다.</summary>
        public int DeckIndex { get; }

        /// <summary>현재 상태.</summary>
        public CardState State { get; private set; }

        /// <summary>미판정·잠금 사유(개발 로그용).</summary>
        public string Reason { get; private set; }

        /// <summary>진행 중인 단기 사건인지. 진행 중에는 그 공간의 구간 반영을 미룬다.</summary>
        public bool IsActiveShortTerm
        {
            get { return State == CardState.Active && !Card.IsLongTerm; }
        }

        /// <summary>정산이 끝났는지(준수·위반·미판정·잠금).</summary>
        public bool IsFinished
        {
            get { return State != CardState.Waiting && State != CardState.Active; }
        }

        /// <summary>감시를 만든다.</summary>
        public RuleWatcher(RuleSO card, int deckIndex)
        {
            Card = card;
            DeckIndex = deckIndex;
            State = CardState.Waiting;
            Reason = string.Empty;
        }

        /// <summary>이 신호가 카드의 시작 신호인지(자격·방문 제한은 보지 않는다).</summary>
        public bool IsTrigger(in JudgeSignal signal)
        {
            if (State != CardState.Waiting || signal.Kind != Card.TriggerKind || !RuleSO.CanTrigger(signal.Kind))
            {
                return false;
            }

            if (!SignalCondition.UsesTarget(signal.Kind))
            {
                return Card.TriggerSpace == SpaceId.None || signal.Space == Card.TriggerSpace;
            }

            return TargetMatch.Matches(Card.TriggerId, Card, signal.TargetId);
        }

        /// <summary>
        /// 진행 중으로 전환한다. 호출자는 먼저 <see cref="IsTrigger"/>와 자격·방문 제한을 확인한다.
        /// </summary>
        public void Start(JudgeWorld world)
        {
            if (State != CardState.Waiting)
            {
                return;
            }

            _successState = new ConditionState();
            _failureState = new ConditionState();
            _cancelState = new ConditionState();
            _successMet = false;

            if (Card.SuccessCondition != null)
            {
                Card.SuccessCondition.OnStart(Card, world, _successState);
            }

            if (Card.FailureCondition != null)
            {
                Card.FailureCondition.OnStart(Card, world, _failureState);
            }

            if (Card.CancelCondition != null)
            {
                Card.CancelCondition.OnStart(Card, world, _cancelState);
            }

            State = CardState.Active;
        }

        /// <summary>
        /// 진행 중일 때 신호 하나를 관찰한다. 이 신호로 정산이 일어나면 true와 결과를 돌려준다.
        /// </summary>
        public bool Observe(in JudgeSignal signal, JudgeWorld world, out RuleResult result)
        {
            result = default;
            if (State != CardState.Active)
            {
                return false;
            }

            // 모든 조건이 같은 신호를 보도록 먼저 전부 평가한다(타이머가 신호를 놓치지 않게).
            bool failed = Card.FailureCondition != null && Card.FailureCondition.Observe(signal, Card, world, _failureState);
            bool succeeded = Card.SuccessCondition != null && Card.SuccessCondition.Observe(signal, Card, world, _successState);
            bool cancelled = Card.CancelCondition != null && Card.CancelCondition.Observe(signal, Card, world, _cancelState);

            // 1) 위반 우선.
            if (failed)
            {
                State = CardState.Violated;
                result = new RuleResult(Card.CardId, State, Card.FailureAxis, Card.FailureDelta, Card.Space,
                    "위반: " + Card.FailureCondition.Describe());
                return true;
            }

            if (succeeded)
            {
                _successMet = true;
            }

            // 2) 즉시 정산 카드의 준수.
            if (_successMet && Card.SettleAt == SettleAt.WhenSuccessMet)
            {
                State = CardState.Complied;
                result = new RuleResult(Card.CardId, State, FearAxis.Trust, Card.SuccessDelta, Card.Space,
                    "준수: " + Card.SuccessCondition.Describe());
                return true;
            }

            // 3) 유효하지 않은 시도 → 대기로 복귀. 이미 성립한 위반은 위에서 정산됐으므로 되돌릴 것이 없다.
            //    밤 종료 정산 카드가 준수 조건을 이미 채웠다면 되돌리지 않는다.
            if (cancelled && !_successMet)
            {
                State = CardState.Waiting;
                Reason = "취소: " + Card.CancelCondition.Describe();
            }

            return false;
        }

        /// <summary>
        /// 밤 종료 정산. 결과를 만들 필요가 있으면 true.
        /// 대기 중이거나 준수 조건을 채우지 못한 진행 중 카드는 미판정(델타 0)이다.
        /// </summary>
        public bool SettleAtNightEnd(out RuleResult result)
        {
            result = default;

            if (State == CardState.Active && _successMet)
            {
                State = CardState.Complied;
                result = new RuleResult(Card.CardId, State, FearAxis.Trust, Card.SuccessDelta, Card.Space, "밤 종료 준수");
                return true;
            }

            if (State == CardState.Active || State == CardState.Waiting)
            {
                string why = State == CardState.Waiting ? "밤 종료: 시작되지 않음" : "밤 종료: 준수 조건 미충족";
                MarkUndetermined(why);
                result = new RuleResult(Card.CardId, State, FearAxis.Trust, 0, Card.Space, why);
                return true;
            }

            return false;
        }

        /// <summary>미판정으로 확정한다. 이미 정산된 카드는 바꾸지 않는다.</summary>
        public void MarkUndetermined(string reason)
        {
            if (IsFinished)
            {
                return;
            }

            State = CardState.Undetermined;
            Reason = reason ?? string.Empty;
        }

        /// <summary>종료 잠금. 아직 정산되지 않은 카드만 잠근다(이미 정산한 결과는 그대로 둔다).</summary>
        public void Lock()
        {
            if (IsFinished)
            {
                return;
            }

            State = CardState.Locked;
            Reason = "종료 잠금";
        }
    }
}

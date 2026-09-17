namespace NightDuty
{
    /// <summary>
    /// 판정 중 공유하는 현재 상태 스냅숏. <see cref="RuleBook"/>이 신호를 받을 때마다 갱신한다.
    /// 조건은 이 값을 읽기만 한다.
    /// </summary>
    public sealed class JudgeWorld
    {
        /// <summary>축 값 공급원(구간 자격 검사용).</summary>
        public IFearAxisReader Axes { get; }

        /// <summary>플레이어 발밑 기준점이 현재 속한 공간. 경계 위에서는 직전 공간을 유지한다.</summary>
        public SpaceId CurrentSpace { get; internal set; }

        /// <summary>손전등이 켜져 있는지.</summary>
        public bool FlashlightOn { get; internal set; }

        /// <summary>태블릿(Tab)이 열려 있는지. 열려 있는 동안 신규 사건은 시작하지 않는다.</summary>
        public bool TabOpen { get; internal set; }

        /// <summary>상태를 만든다.</summary>
        public JudgeWorld(IFearAxisReader axes)
        {
            Axes = axes;
        }

        /// <summary>신호 하나를 반영한다.</summary>
        internal void Apply(in JudgeSignal signal)
        {
            switch (signal.Kind)
            {
                case SignalKind.SpaceEntered:
                    CurrentSpace = signal.Space;
                    break;
                case SignalKind.SpaceExited:
                    if (CurrentSpace == signal.Space)
                    {
                        CurrentSpace = SpaceId.None;
                    }
                    break;
                case SignalKind.FlashlightChanged:
                    FlashlightOn = signal.Flag;
                    break;
                case SignalKind.TabChanged:
                    TabOpen = signal.Flag;
                    break;
            }
        }
    }
}

namespace NightDuty
{
    // 64차 — 고정 몹(과학실 인체 모형·사람 나무) 응시. 규칙은 FixedMobStare.
    public static partial class NightRun
    {
        private static readonly FixedMobStare _stare = new FixedMobStare();

        /// <summary>
        /// 고정 몹을 오래 바라보면 배치 축이 오르게 할지. 게임 구동기(<c>NightRunDriver</c>)가 켠다.
        /// 꺼 두면(기본값) 응시 시간만 세고 축은 올리지 않는다 — 옛 테스트가 H1·S3 대상을 오래 바라보는 신호를 보낸다.
        /// </summary>
        public static bool FixedMobStareEnabled { get; set; }

        /// <summary>고정 몹 응시(연속 응시 시간·대상). 모형의 목 꺾임이 읽는다.</summary>
        public static FixedMobStare Stare
        {
            get { return _stare; }
        }

        /// <summary>응시 샘플을 센다. 켜져 있고 판정 시간창이면 그 점수를 배치 축에 준다(붙잡힘은 <see cref="Send"/>가 닫는다).</summary>
        private static void StareObserve(in JudgeSignal signal, bool judging)
        {
            if (signal.Kind != SignalKind.GazeSample) return;
            int delta = _stare.Observe(signal.TargetId, signal.Value, Day);
            if (delta <= 0 || !FixedMobStareEnabled || !judging || IsCaptured) return;
            SpaceId space = signal.Space != SpaceId.None ? signal.Space : _currentSpace;
            if (space == SpaceId.None) return;
            _axes.Apply(FixedMobStare.Axis, delta, FixedMobStare.SourcePrefix + _stare.TargetId, space);
        }
    }
}

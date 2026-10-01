namespace NightDuty
{
    public static partial class NightRun
    {
        private static bool _tabletOpen;

        /// <summary>
        /// 태블릿을 든 동안에도 판정할지(2026-09-30 최종 기획서 「태블릿·시간 규칙」 — 태블릿을 여는 동안에도 시간과 판정이 흐른다).
        /// <para>
        /// 켜면 <see cref="SignalKind.TabChanged"/>를 판정에 넘기지 않고(<see cref="RuleBook"/>이 얼지 않는다) <see cref="TabletOpen"/>에만 적는다.
        /// 응시 기준점은 센서가 태블릿 위 가운데(화면 높이 78%)로 옮긴다(<see cref="SensingRules.GazeViewportY"/>).
        /// 끄면(기본값·옛 테스트) 옛 규칙대로 Tab 중에는 모든 판정이 멈춘다. 게임 구동기가 켠다.
        /// </para>
        /// </summary>
        public static bool JudgeWhileTabOpen { get; set; }

        /// <summary>태블릿이 올라와 있는지(<see cref="JudgeWhileTabOpen"/>일 때 <see cref="SignalKind.TabChanged"/>로 적는다).</summary>
        public static bool TabletOpen
        {
            get { return _tabletOpen || (_book != null && _book.World.TabOpen); }
        }

        /// <summary>Tab 신호를 판정 대신 상태로만 받았으면 true.</summary>
        private static bool AbsorbTabSignal(in JudgeSignal signal)
        {
            if (signal.Kind != SignalKind.TabChanged || !JudgeWhileTabOpen)
            {
                return false;
            }

            _tabletOpen = signal.Flag;
            return true;
        }

        private static void ResetTabletState(bool clearSwitch)
        {
            _tabletOpen = false;
            if (clearSwitch)
            {
                JudgeWhileTabOpen = false;
            }
        }
    }
}

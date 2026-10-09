namespace NightDuty
{
    public static partial class NightRun
    {
        private static bool _tabletOpen;

        /// <summary>
        /// 태블릿이 올라와 있는지. 태블릿을 든 동안에도 시간과 판정은 흐른다(2026-09-30 최종 기획서 「태블릿·시간 규칙」) —
        /// <see cref="SignalKind.TabChanged"/>는 판정에 넘기지 않고 이 값에만 적는다. 응시 기준점은 센서가 태블릿 위 가운데로 옮긴다(<see cref="SensingRules.GazeViewportY"/>).
        /// <para>옛 규칙(Tab 중 판정 정지)을 고르던 스위치(JudgeWhileTabOpen)는 2026-10-03에 없앴다 — 게임은 늘 새 규칙으로 돌았다.</para>
        /// </summary>
        public static bool TabletOpen
        {
            get { return _tabletOpen; }
        }

        /// <summary>Tab 신호를 판정 대신 상태로만 받았으면 true.</summary>
        private static bool AbsorbTabSignal(in JudgeSignal signal)
        {
            if (signal.Kind != SignalKind.TabChanged)
            {
                return false;
            }

            _tabletOpen = signal.Flag;
            return true;
        }

        private static void ResetTabletState()
        {
            _tabletOpen = false;
        }
    }
}

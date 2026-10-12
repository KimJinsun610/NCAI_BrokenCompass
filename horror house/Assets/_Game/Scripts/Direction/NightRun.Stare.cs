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

        /// <summary>103차: 현관 인체 나무 응시가 축을 올릴 때의 출처 ID.</summary>
        public const string LobbyTreeSource = "stare.lobbytree";

        /// <summary>103차: 현관 인체 나무 응시 1초마다 오르는 축 값(이상·이하, 무작위).</summary>
        public const int LobbyTreeMin = 1;

        /// <inheritdoc cref="LobbyTreeMin"/>
        public const int LobbyTreeMax = 2;

        /// <summary>
        /// 103차(민: 「인체 나무는 제대로 응시하면 1초마다 축이 랜덤하게 조금씩 올라가게 · 화면 울렁임과 함께」): 현관 인체 나무를 1초 이어 바라볼 때마다
        /// 연출(<c>HumanTreeGrowth</c>)이 부른다 — 감각 축(청각·조도·배치) 중 하나에 <paramref name="delta"/>를 더한다.
        /// 고정 몹 응시와 같은 스위치(<see cref="FixedMobStareEnabled"/>)·판정 시간창·붙잡힘 규칙을 따른다. 올렸으면 true.
        /// </summary>
        public static bool AddLobbyTreeStare(FearAxis axis, int delta)
        {
            if (delta <= 0 || axis == FearAxis.Trust || _axes == null || !FixedMobStareEnabled || !IsNightActive || !IsJudgingNow || IsCaptured) return false;
            SpaceId space = _currentSpace != SpaceId.None ? _currentSpace : SpaceId.Corridor;   // 현관은 공간 상자 밖일 수 있다 — 복도로 친다
            _axes.Apply(axis, delta, LobbyTreeSource, space);
            CloseIfCaptured();
            return true;
        }
    }
}

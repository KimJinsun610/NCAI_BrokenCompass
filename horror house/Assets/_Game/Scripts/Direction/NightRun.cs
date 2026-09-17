using System;
using System.Collections.Generic;
using UnityEngine;

namespace NightDuty
{
    /// <summary>
    /// 회차·하룻밤 연결 창구. 클라이언트(게임 플로우)가 부르는 판정 코어의 유일한 진입점이다.
    /// <para>
    /// 씬이 바뀌어도 살아 있도록 정적 클래스로 두었다. <b>4축은 회차 내내 누적</b>되고 다음 날에도 초기화하지 않는다.
    /// 새 회차는 <see cref="StartNewRun"/>으로만 시작한다.
    /// </para>
    /// <para>
    /// 하룻밤 순서: <see cref="BeginNight"/> → (<see cref="Tick"/> · <see cref="Send"/> 반복) → <see cref="RequestEndNight"/>.
    /// 수락되면 <see cref="EventBus.DayEnded"/>로 결과를 보낸다.
    /// 청각·조도·배치 중 하나가 100이 되면 <see cref="FearAxisSystem"/>이 <see cref="EventBus.AxisCritical"/>을 한 번 보내고(신뢰 100은 포획이 아니다),
    /// 이후 판정·정산은 멈춘다. 이때 결과는 <see cref="BuildSummary"/>로 가져간다(DayEnded는 보내지 않는다).
    /// </para>
    /// <para>
    /// 아직 없는 것: 덱 배정 규칙(DayDirector) — 지금은 <see cref="NightDeckTableSO"/> 임시 편성표를 쓴다.
    /// 조우·문자·역설 정산, 종료 요청 수락 조건(필수 점검·오늘 조우 완료) — 조우 시스템이 생기기 전까지 요청은 항상 수락한다.
    /// 대상 참조 검사: <see cref="RegisteredTargets"/>를 직접 넣었으면(null이 아니면) 그것을, 아니면 씬의 <see cref="JudgeTargetRegistry"/>를 쓴다.
    /// <see cref="RegisteredTargets"/>가 null이고 등록부에도 ID가 없으면 검사를 건너뛴다.
    /// 등록부는 밤 시작 순간 <b>켜져 있는</b> 표식만 담는다.
    /// </para>
    /// </summary>
    public static class NightRun
    {
        /// <summary>전체 점검 ID 수(복도·1-1·1-3·과학실·화장실).</summary>
        public const int PatrolTotal = 5;

        private static FearAxisSystem _axes;
        private static BandResolver _bands;
        private static RuleBook _book;
        private static Func<int> _clockMinutes;
        private static readonly List<int> ViolationMinutesToday = new List<int>();
        private static readonly HashSet<SpaceId> InspectedToday = new HashSet<SpaceId>();
        private static readonly HashSet<SpaceId> VisitedToday = new HashSet<SpaceId>();
        private static readonly List<RuleSO> DeckToday = new List<RuleSO>();
        private static DaySummary _lastSummary;

        /// <summary>
        /// 테스트·에디터 도구가 덱을 직접 넣을 때 쓴다. null이면 <see cref="NightDeckTableSO"/>를 읽는다.
        /// </summary>
        public static Func<int, IReadOnlyList<RuleSO>> DeckOverride { get; set; }

        /// <summary>
        /// 대상 ID 목록을 직접 지정한다(테스트·도구). null이면 <see cref="JudgeTargetRegistry"/>를 쓰고,
        /// 등록부도 비어 있으면 참조 검사를 건너뛴다.
        /// </summary>
        public static ICollection<string> RegisteredTargets { get; set; }

        /// <summary>이번 밤 시작 때 실제로 쓴 대상 ID 목록. 검사를 건너뛰었으면 null.</summary>
        public static ICollection<string> TargetsInUse { get; private set; }

        /// <summary>현재 일차(1부터). 회차 시작 전에는 0.</summary>
        public static int Day { get; private set; }

        /// <summary>회차 누적 4축(읽기 전용).</summary>
        public static IFearAxisReader Axes
        {
            get
            {
                EnsureRun();
                return _axes;
            }
        }

        /// <summary>청각·조도·배치 중 하나가 100에 도달해 포획됐는지(신뢰 100은 포획이 아니다).</summary>
        public static bool IsCaptured
        {
            get { return _axes != null && _axes.IsLocked; }
        }

        /// <summary>최초 종료 원인. <see cref="IsCaptured"/>가 false면 의미 없음.</summary>
        public static TerminationCause Cause
        {
            get { return _axes != null ? _axes.Cause : default; }
        }

        /// <summary>하룻밤이 진행 중인지(<see cref="BeginNight"/> 이후, 종료 전).</summary>
        public static bool IsNightActive
        {
            get { return _book != null; }
        }

        /// <summary>진행 중인 하룻밤의 판정(디버그·테스트용). 없으면 null.</summary>
        public static RuleBook CurrentBook
        {
            get { return _book; }
        }

        /// <summary>그날 편성된 카드(덱 표시 순서). 밤이 닫힌 뒤에도 다음 <see cref="BeginNight"/> 전까지 남는다.</summary>
        public static IReadOnlyList<RuleSO> TodayDeck
        {
            get { return DeckToday; }
        }

        /// <summary>오늘 발밑 기준점으로 들어간 적이 있는 공간인지(Tab 중·포획 뒤의 진입은 세지 않는다).</summary>
        public static bool WasVisitedToday(SpaceId space)
        {
            return VisitedToday.Contains(space);
        }

        /// <summary>마지막으로 닫힌 하룻밤 결과(정상 종료·포획·중단 모두).</summary>
        public static DaySummary LastSummary
        {
            get { return _lastSummary; }
        }

        /// <summary>
        /// 새 회차를 시작한다. 4축을 0으로, 일차를 0으로 되돌리고 진행 중인 밤을 버린다.
        /// 메인 화면의 시작 버튼에서 부른다.
        /// </summary>
        public static void StartNewRun()
        {
            if (_axes != null && _bands != null)
            {
                _axes.ValueChanged -= _bands.OnValueChanged;
            }

            _axes = new FearAxisSystem();
            _bands = new BandResolver(_axes);
            _axes.ValueChanged += _bands.OnValueChanged;
            _book = null;
            _clockMinutes = null;
            Day = 0;
            ViolationMinutesToday.Clear();
            InspectedToday.Clear();
            VisitedToday.Clear();
            DeckToday.Clear();
            _lastSummary = default;
            TargetsInUse = null;
        }

        /// <summary>
        /// 하룻밤을 시작한다. Play 씬이 시작될 때 부른다.
        /// <para>씬의 <see cref="JudgeTarget"/>가 모두 켜진 뒤(보통 Start 이후)에 불러야 대상 참조 검사가 맞다.</para>
        /// </summary>
        /// <param name="day">일차(1부터).</param>
        /// <param name="clockMinutes">현재 게임 시각(0:00 기준 분)을 돌려주는 함수. 위반 시각 기록에 쓴다. null이면 -1로 기록.</param>
        public static void BeginNight(int day, Func<int> clockMinutes)
        {
            EnsureRun();

            if (_book != null)
            {
                Debug.LogWarning("[NightRun] 이전 밤이 끝나지 않은 채 새 밤을 시작합니다. 이전 밤의 남은 판정은 버립니다.");
                _book.Settled -= OnSettled;
                _book.Abandon();
            }

            Day = Mathf.Max(1, day);
            _clockMinutes = clockMinutes;
            ViolationMinutesToday.Clear();
            InspectedToday.Clear();
            VisitedToday.Clear();

            IReadOnlyList<RuleSO> deck = LoadDeck(Day);
            DeckToday.Clear();
            DeckToday.AddRange(deck);
            TargetsInUse = ResolveTargets();
            _book = new RuleBook(deck, _axes, _bands, TargetsInUse);
            _book.Settled += OnSettled;
            _book.BeginNight();   // 밤 시작부터 감시하는 장기 카드(C6)를 시작한다.

            // 씬이 새로 열렸으므로 연출에 현재 구간을 from == to로 한 번 알린다.
            _bands.BroadcastAll();

            if (_axes.IsLocked)
            {
                Debug.LogWarning("[NightRun] 이미 포획된 회차에서 밤을 시작했습니다. 판정은 동작하지 않습니다.");
            }
        }

        /// <summary>
        /// 판정 시간 경과. 매 프레임 불러도 된다.
        /// <b>Tab·일시정지 중에는 부르지 않는다</b>(0을 넘겨도 무시한다).
        /// </summary>
        public static void Tick(float judgeSeconds)
        {
            if (_book == null || judgeSeconds <= 0f)
            {
                return;
            }

            _book.Dispatch(JudgeSignal.Tick(judgeSeconds));
            CloseIfCaptured();
        }

        /// <summary>판정 신호 하나를 보낸다(연결 약속 §4). 밤이 진행 중이 아니면 무시한다.</summary>
        public static void Send(in JudgeSignal signal)
        {
            if (_book == null)
            {
                return;
            }

            bool counts = signal.Space != SpaceId.None && !_book.World.TabOpen && !IsCaptured;
            if (counts && signal.Kind == SignalKind.InspectionCompleted)
            {
                InspectedToday.Add(signal.Space);
            }

            if (counts && signal.Kind == SignalKind.SpaceEntered)
            {
                VisitedToday.Add(signal.Space);
            }

            _book.Dispatch(signal);
            CloseIfCaptured();
        }

        /// <summary>
        /// 밤을 정산 없이 버린다. 일시정지 메뉴에서 메인으로 나가는 등 Play 씬이 종료 요청 없이 사라질 때 부른다.
        /// 남은 카드는 정산하지 않고(델타 없음) <see cref="EventBus.DayEnded"/>도 보내지 않는다. 축 값은 그대로 둔다.
        /// </summary>
        public static void AbandonNight()
        {
            if (_book == null)
            {
                return;
            }

            _book.Abandon();
            _lastSummary = BuildSummary();
            CloseNight();
        }

        /// <summary>
        /// 근무 종료를 요청한다. 수락되면 남은 카드를 덱 순서로 정산하고 <see cref="EventBus.DayEnded"/>를 보낸 뒤 true.
        /// 밤이 진행 중이 아니거나 이미 포획됐으면 false.
        /// </summary>
        public static bool RequestEndNight()
        {
            if (_book == null || IsCaptured)
            {
                return false;
            }

            // TODO(조우 시스템): 필수 점검 완료 · 오늘 조우 관찰·퇴실 완료를 수락 조건으로 검사한다(기획서 공통 명세 1절).
            _book.EndNight();
            // TODO(문자 시스템): P형 미도달 정산은 여기, 수칙 정산 뒤에 온다.

            if (IsCaptured)
            {
                // 밤 종료 정산 중 100에 도달했다. 포획 경로(AxisCritical)가 이미 처리했으므로 DayEnded는 보내지 않는다.
                _lastSummary = BuildSummary();
                CloseNight();
                return false;
            }

            _lastSummary = BuildSummary();
            CloseNight();
            EventBus.RaiseDayEnded(_lastSummary);
            return true;
        }

        /// <summary>
        /// 지금까지의 하룻밤 결과를 만든다. 포획 결과창은 <see cref="EventBus.AxisCritical"/>을 받은 뒤 이것을 부른다.
        /// </summary>
        public static DaySummary BuildSummary()
        {
            EnsureRun();
            List<RuleResult> results = _book != null
                ? new List<RuleResult>(_book.Results)
                : new List<RuleResult>(_lastSummary.Results);
            List<DutyLogEntry> dutyLog = BuildDutyLog(results);

            return new DaySummary(
                Day,
                InspectedToday.Count,
                PatrolTotal,
                _axes.GetValue(FearAxis.Auditory),
                _axes.GetValue(FearAxis.Illuminance),
                _axes.GetValue(FearAxis.Layout),
                _axes.GetValue(FearAxis.Trust),
                _axes.IsLocked ? NightOutcome.Captured : NightOutcome.Completed,
                _axes.Cause,
                new List<int>(ViolationMinutesToday),
                results,
                dutyLog);
        }

        /// <summary>그날 덱 순서대로 근무 일지 줄을 만든다. 같은 카드의 결과가 여럿이면 마지막 것을 쓴다.</summary>
        private static List<DutyLogEntry> BuildDutyLog(List<RuleResult> results)
        {
            Dictionary<string, CardState> last = new Dictionary<string, CardState>();
            for (int i = 0; i < results.Count; i++)
            {
                if (!string.IsNullOrEmpty(results[i].CardId))
                {
                    last[results[i].CardId] = results[i].State;
                }
            }

            List<DutyLogEntry> log = new List<DutyLogEntry>(DeckToday.Count);
            for (int i = 0; i < DeckToday.Count; i++)
            {
                RuleSO card = DeckToday[i];
                if (card == null)
                {
                    continue;
                }

                CardState state;
                if (!last.TryGetValue(card.CardId, out state))
                {
                    state = CardState.Waiting;
                }

                log.Add(new DutyLogEntry(i + 1, card.CardId, card.Space, card.PlayerText, state, VisitedToday.Contains(card.Space)));
            }

            return log;
        }

        /// <summary>
        /// 포획됐으면 그날 밤을 닫는다. <see cref="EventBus.AxisCritical"/> 구독자는 닫히기 전에 호출되므로
        /// 그 안에서 <see cref="BuildSummary"/>를 불러도 결과가 온전하다.
        /// </summary>
        private static void CloseIfCaptured()
        {
            if (_book == null || !IsCaptured)
            {
                return;
            }

            _lastSummary = BuildSummary();
            CloseNight();
        }

        /// <summary>디버그: 지정 축을 100으로 올려 포획 경로를 시험한다(신뢰는 100이 돼도 포획되지 않는다). 에디터·디버그 빌드에서만 쓴다.</summary>
        public static void DebugForceCapture(FearAxis axis)
        {
            EnsureRun();
            int remain = Bands.Max - _axes.GetValue(axis);
            SpaceId space = _book != null ? _book.World.CurrentSpace : SpaceId.None;
            _axes.Apply(axis, remain, "debug", space);
            CloseIfCaptured();
        }

        /// <summary>
        /// 디버그: 축에 양수 델타를 더한다(감쇠 없음 규칙 그대로 음수는 무시). 구간 자격을 시험할 때 쓴다.
        /// 에디터·디버그 빌드에서만 쓴다.
        /// </summary>
        public static void DebugAddAxis(FearAxis axis, int delta)
        {
            EnsureRun();
            if (delta <= 0)
            {
                return;
            }

            SpaceId space = _book != null ? _book.World.CurrentSpace : SpaceId.None;
            _axes.Apply(axis, delta, "debug", space);
            CloseIfCaptured();
        }

        /// <summary>
        /// 디버그: 지금 구간을 연출에 다시 방송한다(from == to). 축 공급원을 판정 코어로 바꿀 때 쓴다.
        /// </summary>
        public static void DebugRebroadcast()
        {
            EnsureRun();
            _bands.BroadcastAll();
        }

        private static ICollection<string> ResolveTargets()
        {
            if (RegisteredTargets != null)
            {
                return new HashSet<string>(RegisteredTargets);
            }

            return JudgeTargetRegistry.Count > 0 ? JudgeTargetRegistry.Snapshot() : null;
        }

        private static void OnSettled(RuleResult result)
        {
            if (result.State != CardState.Violated)
            {
                return;
            }

            int minute = -1;
            if (_clockMinutes != null)
            {
                try
                {
                    minute = _clockMinutes();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }

            ViolationMinutesToday.Add(minute);
        }

        private static void CloseNight()
        {
            if (_book != null)
            {
                _book.Settled -= OnSettled;
            }

            _book = null;
        }

        private static IReadOnlyList<RuleSO> LoadDeck(int day)
        {
            if (DeckOverride != null)
            {
                return DeckOverride(day) ?? new List<RuleSO>();
            }

            NightDeckTableSO table = Resources.Load<NightDeckTableSO>(NightDeckTableSO.ResourcePath);
            if (table == null)
            {
                Debug.LogWarning("[NightRun] Resources/" + NightDeckTableSO.ResourcePath + " 편성표가 없습니다. 카드 없이 밤을 시작합니다. " +
                                 "NightDuty ▸ 복도 카드 에셋 생성 메뉴로 만들 수 있습니다.");
                return new List<RuleSO>();
            }

            return table.DeckFor(day);
        }

        private static void EnsureRun()
        {
            if (_axes == null)
            {
                StartNewRun();
            }
        }

        /// <summary>
        /// 도메인 리로드를 끈 상태에서 이전 플레이의 회차가 남지 않도록 플레이 시작마다 비운다.
        /// <b>참조하는 곳이 없어 보여도 지우지 말 것.</b>
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _axes = null;
            _bands = null;
            _book = null;
            _clockMinutes = null;
            Day = 0;
            ViolationMinutesToday.Clear();
            InspectedToday.Clear();
            VisitedToday.Clear();
            DeckToday.Clear();
            _lastSummary = default;
            DeckOverride = null;
            RegisteredTargets = null;
            TargetsInUse = null;
        }
    }
}

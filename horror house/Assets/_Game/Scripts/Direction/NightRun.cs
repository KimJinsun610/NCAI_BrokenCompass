using System;
using System.Collections.Generic;
using UnityEngine;

namespace NightDuty
{
    /// <summary>
    /// 회차·하룻밤 연결 창구. 클라이언트(게임 플로우)가 부르는 판정 코어의 유일한 진입점이다.
    /// <para>
    /// 씬이 바뀌어도 살아 있도록 정적 클래스로 두었다. <b>4축은 회차 내내 누적</b>되고 다음 날에도 초기화하지 않는다.
    /// 새 회차를 <b>명시적으로</b> 시작하는 길은 <see cref="StartNewRun"/> 하나뿐이지만,
    /// 회차가 아직 없는 상태(<c>_axes == null</c>)에서 다른 진입점을 부르면 내부의 <c>EnsureRun()</c>이
    /// <see cref="StartNewRun"/>을 대신 불러 준다 — 씬에서 바로 <see cref="BeginNight"/>부터 시작해도 터지지 않게 한 안전망이다.
    /// </para>
    /// <para>
    /// 하룻밤 순서: <see cref="BeginNight"/> → (<see cref="Tick"/> · <see cref="Send"/> 반복) → <see cref="RequestEndNight"/>.
    /// 수락되면 <see cref="EventBus.DayEnded"/>로 결과를 보낸다.
    /// 청각·조도·배치 중 하나가 100이 되면 <see cref="FearAxisSystem"/>이 <see cref="EventBus.AxisCritical"/>을 한 번 보내고(신뢰 100은 포획이 아니다),
    /// 이후 판정·정산은 멈춘다. 이때 결과는 <see cref="BuildSummary"/>로 가져간다(DayEnded는 보내지 않는다).
    /// </para>
    /// <para>
    /// 덱 배정은 <see cref="DayDirector"/>가 맡는다(<c>LoadDeck</c>에서 만들어 매일 6장을 고른다).
    /// <see cref="NightDeckTableSO"/>는 일차별 덱이 아니라 <b>카드 풀의 공급원</b>이다 — <c>CollectPool</c>이 모든 일차를 합쳐 중복 없이 읽는다.
    /// 역설 문자 발송은 <see cref="ParadoxDirector"/>가 맡는다.
    /// 아직 없는 것: <b>조우(EncounterDirector)와 P형 미도달 정산.</b>
    /// 종료 요청 수락 조건(필수 점검·오늘 조우 완료)도 그래서 비어 있다 — 조우 시스템이 생기기 전까지 요청은 항상 수락한다.
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
        private static readonly ParadoxDirector Paradox = new ParadoxDirector();
        private static DayDirector _director;
        private static EncounterDirector _encounter;
        private static DaySummary _lastSummary;

        /// <summary>
        /// 공간 미방문으로 끝난 카드에 물리는 감각축 벌점(2026-09-21 재설계).
        /// <para>
        /// <b>왜 9인가.</b> 가서 어기면 기본 +12다. 미방문이 +9면 <b>가는 쪽이 항상 3만큼 이긴다</b> —
        /// 도망은 불이익이되 「가서 어기느니 안 가고 만다」가 되지는 않는다.
        /// 이 한 줄이 없으면 <b>경비실에 숨어 버티기가 최적해</b>가 된다(축이 한 칸도 안 오르고 완주).
        /// </para>
        /// <para>
        /// 기획서 D절은 미판정 벌점을 전 유형 폐기했는데, <b>이 유형 하나만 예외</b>다.
        /// 방문했으나 단서가 안 났다 · 문자가 안 왔다 · 대상 참조가 없다 · 선택이 불가능했다는
        /// 전부 0 그대로다 — 그것들은 플레이어의 선택이 아니기 때문이다.
        /// </para>
        /// </summary>
        public const int UnvisitedPenalty = 9;

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
            _director = null;   // 첫 LoadDeck에서 카드 풀을 읽어 만든다.
            _encounter = null;  // 첫 BeginNight에서 조우 표를 읽어 만든다.
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

            // 일차 하한을 **덱보다 먼저** 적용한다. 카드의 발동 자격이 축 값을 보고 정해지므로
            // 순서가 뒤집히면 그날 하한이 카드 풀에 반영되지 않는다(DayFloor 주석 참조).
            DayFloor.Apply(_axes, Day);

            // 조우를 **덱보다 먼저** 정한다. DayDirector가 「그날 조우 공간의 카드 1장」을 보장하려면
            // 그 공간을 이미 알고 있어야 한다. 조우 배정은 축을 읽지 않으므로 하한 뒤·덱 앞이 안전하다.
            EnsureEncounter();
            if (_encounter != null)
            {
                _encounter.BeginNight(Day);
            }

            IReadOnlyList<RuleSO> deck = LoadDeck(Day);
            DeckToday.Clear();
            DeckToday.AddRange(deck);
            TargetsInUse = ResolveTargets();
            _book = new RuleBook(deck, _axes, _bands, TargetsInUse);
            _book.Settled += OnSettled;
            Paradox.BeginNight(_axes);   // 그날 상한을 근무 시작 시 신뢰로 고정한다(기획서 C절).
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

            JudgeSignal tick = JudgeSignal.Tick(judgeSeconds);
            _book.Dispatch(tick);
            ObserveEncounter(tick);   // 시야에 걸려 대기 중인 배치를 여기서 푼다.
            PollParadox();
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
            ObserveEncounter(signal);
            PollParadox();
            CloseIfCaptured();
        }

        /// <summary>
        /// 그날 조우 연출기. 씬(구동기)이 <see cref="EncounterDirector.IsVisible"/>과
        /// <see cref="EncounterDirector.PlaceModel"/>을 채워 준다. 회차가 시작되기 전에는 null이다.
        /// <para>
        /// <b>씬 쪽 연결이 없으면 조우가 통째로 멈추지 않고, 「항상 시야 밖」으로 보고 배치를 지시만 한다.</b>
        /// 반대(항상 보임)로 두면 조우가 조용히 사라져 빈 게임이 되는데 그게 알아채기 훨씬 어렵다.
        /// </para>
        /// </summary>
        public static EncounterDirector Encounter
        {
            get { return _encounter; }
        }

        /// <summary>
        /// 조우 연출기를 준비한다. <c>Resources</c>에 조우 표가 없으면 만들지 않고 경고만 남긴다 —
        /// 표가 없다고 밤이 시작되지 못하면 수칙 판정까지 같이 죽는다.
        /// </summary>
        private static void EnsureEncounter()
        {
            if (_encounter != null)
            {
                return;
            }

            EncounterTableSO table = Resources.Load<EncounterTableSO>(EncounterTableSO.ResourcePath);
            if (table == null)
            {
                Debug.LogWarning("[NightRun] Resources/" + EncounterTableSO.ResourcePath + " 조우 표가 없습니다. " +
                                 "조우·유도 문자 없이 밤을 시작합니다. 수칙 판정은 그대로 돕니다.");
                return;
            }

            _encounter = new EncounterDirector(table);
            _encounter.ClockMinutes = CurrentMinute;
            _encounter.ParadoxSentToday = ParadoxSentToday;
            _encounter.BeginRun();
        }

        /// <summary>그날 조우 장면이 쓰는 공간. 조우가 없으면 빈 목록.</summary>
        private static IReadOnlyList<SpaceId> EncounterSpacesToday()
        {
            List<SpaceId> spaces = new List<SpaceId>();
            if (_encounter == null)
            {
                return spaces;
            }

            IReadOnlyList<string> scenes = _encounter.TodayScenes;
            for (int i = 0; i < scenes.Count; i++)
            {
                SpaceId space = _encounter.SpaceOf(scenes[i]);
                if (space != SpaceId.None && !spaces.Contains(space))
                {
                    spaces.Add(space);
                }
            }

            return spaces;
        }

        /// <summary>그 조우 장면이 오늘 깔렸는지. 조우가 없으면 false.</summary>
        private static bool IsEncounterActive(string sceneId)
        {
            return _encounter != null && _encounter.IsActive(sceneId);
        }

        /// <summary>오늘 역설 문자를 한 통이라도 보냈는지. N1(재방문)이 같은 날 겹치지 않게 하려고 쓴다.</summary>
        private static bool ParadoxSentToday()
        {
            return Paradox.Today.Count > 0;
        }

        /// <summary>
        /// 조우 연출기에 신호를 흘린다. <b>수칙 판정이 먼저 본 뒤</b>에 부른다 —
        /// 모형 배치가 카드 판정에 끼어들지 않게 하기 위해서다. Tab 중·포획 뒤에는 보내지 않는다.
        /// </summary>
        private static void ObserveEncounter(in JudgeSignal signal)
        {
            if (_encounter == null || _book == null || _book.World.TabOpen || IsCaptured)
            {
                return;
            }

            try
            {
                _encounter.Observe(signal);
            }
            catch (Exception e)
            {
                // 조우가 넘어져도 수칙 판정은 계속 돌아야 한다.
                Debug.LogException(e);
            }
        }

        /// <summary>밤을 닫으며 미관찰 장면을 다음 날로 이월한다. 정상 종료·중단 모두에서 부른다.</summary>
        private static void EndEncounterNight()
        {
            if (_encounter == null)
            {
                return;
            }

            try
            {
                _encounter.EndNight();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        /// <summary>오늘 보낸 역설 문자(발송 순서).</summary>
        public static IReadOnlyList<ParadoxMessage> MessagesToday
        {
            get { return Paradox.Today; }
        }

        /// <summary>진행 중인 카드 중 역설 문자를 보낼 것이 있으면 보낸다. Tab 중에는 보내지 않는다.</summary>
        private static void PollParadox()
        {
            if (_book == null || _book.World.TabOpen || IsCaptured)
            {
                return;
            }

            Paradox.Poll(_book, _axes, CurrentMinute());
        }

        private static int CurrentMinute()
        {
            if (_clockMinutes == null)
            {
                return -1;
            }

            try
            {
                return _clockMinutes();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return -1;
            }
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
            EndEncounterNight();
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
            EndEncounterNight();
            ApplyUnvisitedPenalty();
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

        /// <summary>
        /// 그날 배정된 공간에 <b>한 번도 들어가지 않은</b> 카드에 감각축 <see cref="UnvisitedPenalty"/>를 물린다.
        /// 수칙 정산이 끝난 뒤, 문자 정산보다 앞에서 한 번만 부른다.
        /// <list type="bullet">
        /// <item>방문했으면 물리지 않는다 — 갔는데 단서가 안 난 것은 플레이어의 선택이 아니다.</item>
        /// <item>이미 준수·위반으로 정산된 카드는 건너뛴다. 결과가 났다면 그 공간에 있었다는 뜻이다.</item>
        /// <item>한 공간에 여러 카드가 걸려 있으면 <b>카드마다</b> 물린다. 안 간 대가는 그 공간이 아니라 수칙 단위다.</item>
        /// <item>도중에 100에 닿으면 <see cref="FearAxisSystem"/>이 잠긴다. 다만 그 잠금에 기대지 않고
        /// 루프 안의 <c>if (_axes.IsLocked) return;</c>로 <b>직접 끊는다</b> — 그래야 포획 뒤의 카드가
        /// 벌점 기록(<c>Apply</c> 호출)을 남기지 않아, 종료 원인이 어느 카드였는지가 로그에서 흐려지지 않는다.
        /// 이 가드는 중복이 아니다.</item>
        /// </list>
        /// </summary>
        private static void ApplyUnvisitedPenalty()
        {
            if (_axes == null || _axes.IsLocked)
            {
                return;
            }

            for (int i = 0; i < DeckToday.Count; i++)
            {
                RuleSO card = DeckToday[i];
                if (card == null || VisitedToday.Contains(card.Space))
                {
                    continue;
                }

                if (WasSettled(card.CardId))
                {
                    continue;
                }

                _axes.Apply(card.FailureAxis, UnvisitedPenalty, card.CardId + "(미방문)", card.Space);

                if (_axes.IsLocked)
                {
                    return;
                }
            }
        }

        /// <summary>그 카드가 준수 또는 위반으로 정산됐는지. 미판정·대기·진행 중은 false.</summary>
        private static bool WasSettled(string cardId)
        {
            if (_book == null)
            {
                return false;
            }

            IReadOnlyList<RuleWatcher> watchers = _book.Watchers;
            for (int i = 0; i < watchers.Count; i++)
            {
                RuleWatcher w = watchers[i];
                if (w.Card == null || w.Card.CardId != cardId)
                {
                    continue;
                }

                return w.State == CardState.Complied || w.State == CardState.Violated;
            }

            return false;
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

                log.Add(new DutyLogEntry(
                    i + 1,
                    card.CardId,
                    card.Space,
                    card.PlayerText,
                    state,
                    VisitedToday.Contains(card.Space),
                    Paradox.WasSentToday(card.CardId)));
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

            // 2026-09-21 재설계: 편성표는 이제 **카드 풀**로만 쓰고, 그날 6장은 DayDirector가 고른다.
            // 이전에는 일차별 고정 리스트였는데, 그러면 한 공간·한 축에 몰리는 날을 사람이 일일이 막아야 했고
            // 24장 중 일곱 장은 한 회차에 한 번도 나오지 않았다.
            if (_director == null)
            {
                _director = new DayDirector(CollectPool(table));
                _director.EncounterSpacesToday = EncounterSpacesToday;
                _director.IsEncounterActive = IsEncounterActive;
            }

            return _director.BuildDeck(day, _axes);
        }

        /// <summary>
        /// 편성표의 모든 일차를 훑어 중복 없는 카드 풀을 만든다.
        /// 편성표가 「일차별 덱」에서 「풀」로 뜻이 바뀌었지만, 기획팀이 쓰던 에셋을 그대로 살리려고
        /// 표의 모든 칸을 합쳐서 읽는다. 표가 비어 있으면 빈 풀이 되고, 그날은 카드 없는 밤이 된다.
        /// </summary>
        private static List<RuleSO> CollectPool(NightDeckTableSO table)
        {
            List<RuleSO> pool = new List<RuleSO>();
            HashSet<string> seen = new HashSet<string>();

            for (int day = 1; day <= DayFloor.LastDay; day++)
            {
                IReadOnlyList<RuleSO> cards = table.DeckFor(day);
                for (int i = 0; i < cards.Count; i++)
                {
                    RuleSO card = cards[i];
                    if (card != null && seen.Add(card.CardId))
                    {
                        pool.Add(card);
                    }
                }
            }

            if (pool.Count == 0)
            {
                Debug.LogWarning("[NightRun] 편성표에 카드가 하나도 없습니다. 카드 없이 밤을 시작합니다.");
            }

            return pool;
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
            _director = null;
            _encounter = null;
        }
    }
}

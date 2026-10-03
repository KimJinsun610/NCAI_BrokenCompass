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
    /// 하룻밤 순서: <see cref="BeginNight"/> → (<see cref="Tick"/> · <see cref="Send"/> · <see cref="ReportInspection"/> 반복) → <see cref="RequestEndNight"/>.
    /// 수락되면 <see cref="EventBus.DayEnded"/>로 결과를 보낸다.
    /// 청각·조도·배치 중 하나가 100이 되면 <see cref="FearAxisSystem"/>이 <see cref="EventBus.AxisCritical"/>을 한 번 보내고(신뢰 100은 포획이 아니다),
    /// 그 밤의 판정은 멈춘다. 한 신호에 두 축이 100이면 초과량이 큰 축 하나로 붙잡힌다(<see cref="FearAxisSystem.BeginFrame"/>).
    /// 붙잡힌 밤은 <see cref="RestartAfterCapture"/>로 다시 한다(2026-09-30 최종 기획서 — 스냅샷 복원 + 감각 축 −10×k, 하한 40,
    /// 6번째 시도에서도 붙잡히면 결근).
    /// </para>
    /// <para>
    /// <b>밤 시계(<see cref="NightClock"/>)</b>: <see cref="JudgingWindowEnabled"/>가 켜져 있으면 00:16~03:30(이완 01:52~02:16 제외)에만
    /// 수칙을 판정한다. 판정 정지 중에는 상태 신호(공간·구역·손전등·Tab)만 판정에 넘기고, 델타는 <see cref="Deltas.SoftCap"/>에서 멈춘다.
    /// 끄면(기본값·테스트) 시각과 무관하게 판정한다. 태블릿을 든 동안의 판정은 <see cref="JudgeWhileTabOpen"/>(NightRun.Tablet.cs).
    /// </para>
    /// <para>
    /// <b>점검(<see cref="InspectionBoard"/>)</b>: <see cref="InspectionsEnabled"/>가 켜져 있으면 밤 시작에 <see cref="AnomalyAssigner"/>가
    /// 그날 점검표를 편성한다(재시작해도 그대로). 보고는 <see cref="ReportInspection"/>, 「가까이」는 <see cref="InspectionStartle"/>.
    /// 04:00 정산에서 미완료마다 경고 1, 이상이 있던 항목은 +8. 끄면(기본값·테스트) 점검표가 비어 있다.
    /// 옛 「미방문 벌점 +9」는 점검 미완료 경고가 넘겨받아 없앴다(되살리지 말 것).
    /// 점검 편성 뒤에는 밤 편성(조우 슬롯 + 새 수칙 덱, <see cref="Program"/> — NightRun.Program.cs)이 온다.
    /// </para>
    /// <para>
    /// 옛 24장 카드(H·C·S·T 1~6)와 그 하루 6장 배정기(DayDirector)는 2026-10-03에 폐기했다. 판정은 새 수칙(<see cref="FinalRules"/>)이 하고,
    /// 옛 판정 책(<see cref="RuleBook"/>)에는 <see cref="DeckOverride"/>로 넣은 카드(테스트·도구)만 들어간다.
    /// 역설 문자 발송은 <see cref="ParadoxDirector"/>가 맡는다.
    /// 대상 참조 검사: <see cref="RegisteredTargets"/>를 직접 넣었으면(null이 아니면) 그것을, 아니면 씬의 <see cref="JudgeTargetRegistry"/>를 쓴다.
    /// <see cref="RegisteredTargets"/>가 null이고 등록부에도 ID가 없으면 검사를 건너뛴다.
    /// 등록부는 밤 시작 순간 <b>켜져 있는</b> 표식만 담는다.
    /// </para>
    /// </summary>
    public static partial class NightRun
    {
        /// <summary>옛 순찰 점검 ID 수(복도·1-1·1-3·과학실·화장실). 새 점검표는 <see cref="Inspections"/>.</summary>
        public const int PatrolTotal = 5;

        private static FearAxisSystem _axes;
        private static BandResolver _bands;
        private static WarningLedger _ledger;
        private static RuleBook _book;
        private static Func<int> _clockMinutes;
        private static readonly List<int> ViolationMinutesToday = new List<int>();
        private static readonly HashSet<SpaceId> InspectedToday = new HashSet<SpaceId>();
        private static readonly HashSet<SpaceId> VisitedToday = new HashSet<SpaceId>();
        private static readonly List<RuleSO> DeckToday = new List<RuleSO>();
        private static readonly ParadoxDirector Paradox = new ParadoxDirector();
        private static readonly InspectionBoard Board = new InspectionBoard();
        private static readonly List<string>[] RaisedThisAttempt = { new List<string>(), new List<string>(), new List<string>() };
        private static AnomalyAssigner _assigner;
        private static EncounterDirector _encounter;
        private static DaySummary _lastSummary;
        private static NightSnapshot _nightStart;
        private static NightSnapshot _checkpoint;
        private static List<string> _lastCaptureSources = new List<string>();

        /// <summary>
        /// 테스트·에디터 도구가 옛 판정 책에 덱을 직접 넣을 때 쓴다. null이면 빈 덱이다(옛 24장 편성표는 2026-10-03 폐기).
        /// </summary>
        public static Func<int, IReadOnlyList<RuleSO>> DeckOverride { get; set; }

        /// <summary>
        /// 대상 ID 목록을 직접 지정한다(테스트·도구). null이면 <see cref="JudgeTargetRegistry"/>를 쓰고,
        /// 등록부도 비어 있으면 참조 검사를 건너뛴다.
        /// </summary>
        public static ICollection<string> RegisteredTargets { get; set; }

        /// <summary>
        /// 밤 시계의 판정 시간창을 쓸지. 켜면 시계(근무 시작부터의 분)가 00:16~03:30(이완 제외)일 때만 수칙을 판정한다.
        /// 게임 구동기(<c>NightRunDriver</c>)가 켠다. 꺼 두면(기본값) 시각과 무관하게 판정한다 — 옛 테스트가 시각 0에서 판정한다.
        /// </summary>
        public static bool JudgingWindowEnabled { get; set; }

        /// <summary>
        /// 점검표를 편성할지. 게임 구동기가 켠다. 꺼 두면(기본값) 점검표가 비어 있고 04:00 미완료 경고도 없다 — 옛 테스트가 정산 델타를 0으로 본다.
        /// </summary>
        public static bool InspectionsEnabled { get; set; }

        /// <summary>테스트·도구가 점검 편성을 직접 넣을 때 쓴다(일차, 연출 구간 → 편성). 값이 있으면 <see cref="InspectionsEnabled"/>와 무관하게 쓴다.</summary>
        public static Func<int, IFearAxisReader, InspectionPlan> InspectionPlanOverride { get; set; }

        /// <summary>이번 밤 시작 때 실제로 쓴 대상 ID 목록. 검사를 건너뛰었으면 null.</summary>
        public static ICollection<string> TargetsInUse { get; private set; }

        /// <summary>현재 일차(1부터). 회차 시작 전에는 0.</summary>
        public static int Day { get; private set; }

        /// <summary>그 밤 몇 번 재시작했는지(k). 새 밤마다 0.</summary>
        public static int RestartsTonight { get; private set; }

        /// <summary>회차 누적 4축(읽기 전용).</summary>
        public static IFearAxisReader Axes
        {
            get
            {
                EnsureRun();
                return _axes;
            }
        }

        /// <summary>연출 구간으로 읽는 창구(값 = 생존 수치, 구간 = 연출 구간).</summary>
        public static IFearAxisReader Shown
        {
            get
            {
                EnsureRun();
                return _bands.Shown;
            }
        }

        /// <summary>
        /// 그 공간에 <b>지금 보이는</b> 축의 연출 구간(공간 보류 반영 — 단기 수칙이 걸린 공간은 풀릴 때까지 옛 구간).
        /// 순찰 공간이 아니면(도서관·경비실) 축 전체의 연출 구간. 화면 표현(조도 톤·소등, <c>IlluminanceMap</c>)이 읽는다.
        /// </summary>
        public static Band ShownBand(SpaceId space, FearAxis axis)
        {
            EnsureRun();
            return _bands.GetShown(space, axis);
        }

        /// <summary>경고 장부(도장·대기 처벌). 경고는 <see cref="AddWarning"/>으로 더한다.</summary>
        public static WarningLedger Warnings
        {
            get
            {
                EnsureRun();
                return _ledger;
            }
        }

        /// <summary>오늘 점검판(편성·보고 상태). 보고는 <see cref="ReportInspection"/>으로 한다.</summary>
        public static InspectionBoard Inspections
        {
            get { return Board; }
        }

        /// <summary>
        /// 경비실 전화로 근무를 일찍 끝낼 수 있는가(2026-10-03 민): 밤이 진행 중이고 붙잡히지 않았으며,
        /// 오늘 점검표가 있고 <b>전부 보고했을 때</b>. 끝내는 길은 04:00과 같은 <see cref="RequestEndNight"/>다 —
        /// 남은 카드 정산·조우 이월이 그대로 돈다(미완료 점검이 없으니 경고는 붙지 않는다).
        /// </summary>
        public static bool CanEndShiftEarly
        {
            get { return IsNightActive && !IsCaptured && Board.Total > 0 && Board.RemainingCount == 0; }
        }

        /// <summary>
        /// 마지막 재시작 직전 시도에서 붙잡힌 축을 올린 수칙·점검 항목 이름(처음 오른 순, 중복 없음). 재시작 카드가 읽는다.
        /// 무엇을 했는지·어떻게 했어야 하는지는 담지 않는다.
        /// </summary>
        public static IReadOnlyList<string> LastCaptureSources
        {
            get { return _lastCaptureSources; }
        }

        /// <summary>밤 시작 스냅샷. 밤이 시작되기 전에는 null.</summary>
        public static NightSnapshot NightStartSnapshot
        {
            get { return _nightStart; }
        }

        /// <summary>02:16 중간 서명으로 찍은 체크포인트. 서명하지 않았으면 null.</summary>
        public static NightSnapshot Checkpoint
        {
            get { return _checkpoint; }
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

        /// <summary>
        /// 지금 수칙을 판정하는 시각인지. <see cref="JudgingWindowEnabled"/>가 꺼져 있거나 시계를 모르면 항상 true.
        /// </summary>
        public static bool IsJudgingNow
        {
            get
            {
                if (!JudgingWindowEnabled)
                {
                    return true;
                }

                int minute = CurrentMinute();
                return minute < 0 || NightClock.IsJudging(minute);
            }
        }

        /// <summary>지금 밤 시계(근무 시작부터의 분). 모르면 -1.</summary>
        public static int NightMinute
        {
            get { return CurrentMinute(); }
        }

        /// <summary>진행 중인 하룻밤의 판정(디버그·테스트용). 없으면 null.</summary>
        public static RuleBook CurrentBook
        {
            get { return _book; }
        }

        /// <summary>
        /// 그날 편성된 카드(덱 표시 순서). 밤이 닫힌 뒤에도 다음 <see cref="BeginNight"/> 전까지 남는다.
        /// 새 편성(<see cref="ProgramEnabled"/>)이면 태블릿이 읽도록 <see cref="DisplayDeck"/>(새 수칙 + 점검표, 판정 조건 없는 표시용)를 돌려준다.
        /// </summary>
        public static IReadOnlyList<RuleSO> TodayDeck
        {
            get { return ProgramEnabled && _displayDeck.Count > 0 ? (IReadOnlyList<RuleSO>)_displayDeck : DeckToday; }
        }

        /// <summary>오늘 발밑 기준점으로 들어간 적이 있는 공간인지(Tab 중·포획 뒤의 진입은 세지 않는다).</summary>
        public static bool WasVisitedToday(SpaceId space)
        {
            return VisitedToday.Contains(space);
        }

        /// <summary>마지막으로 닫힌 하룻밤 결과(정상 종료·포획·결근·중단 모두).</summary>
        public static DaySummary LastSummary
        {
            get { return _lastSummary; }
        }

        /// <summary>
        /// 이번 시도에서 그 감각 축을 올린 수칙·점검 항목 이름(처음 오른 순, 중복 없음). 신뢰는 빈 목록.
        /// </summary>
        public static IReadOnlyList<string> RaisedSources(FearAxis axis)
        {
            int i = (int)axis;
            return i >= 0 && i < RaisedThisAttempt.Length ? RaisedThisAttempt[i] : (IReadOnlyList<string>)Array.Empty<string>();
        }

        /// <summary>
        /// 새 회차를 시작한다. 4축을 0으로, 일차를 0으로 되돌리고 진행 중인 밤을 버린다.
        /// 메인 화면의 시작 버튼에서 부른다.
        /// </summary>
        public static void StartNewRun()
        {
            if (_axes != null)
            {
                if (_bands != null) _axes.ValueChanged -= _bands.OnValueChanged;
                _axes.Raised -= OnAxisRaised;
            }

            if (_ledger != null)
            {
                _ledger.Changed -= EventBus.RaiseWarningsChanged;
            }

            _axes = new FearAxisSystem();
            _bands = new BandResolver(_axes);
            _axes.ValueChanged += _bands.OnValueChanged;
            _axes.Raised += OnAxisRaised;
            _ledger = new WarningLedger();
            _ledger.Changed += EventBus.RaiseWarningsChanged;
            _assigner = new AnomalyAssigner();
            Board.Begin(InspectionPlan.Empty(0));
            _book = null;
            _clockMinutes = null;
            Day = 0;
            RestartsTonight = 0;
            _nightStart = null;
            _checkpoint = null;
            _lastCaptureSources = new List<string>();
            ClearRaised();
            ResetExtensions(false);
            ViolationMinutesToday.Clear();
            InspectedToday.Clear();
            VisitedToday.Clear();
            DeckToday.Clear();
            _lastSummary = default;
            TargetsInUse = null;
            _encounter = null;  // 첫 BeginNight에서 조우 표를 읽어 만든다.
        }

        /// <summary>
        /// 하룻밤을 시작한다. Play 씬이 시작될 때 부른다.
        /// <para>씬의 <see cref="JudgeTarget"/>가 모두 켜진 뒤(보통 Start 이후)에 불러야 대상 참조 검사가 맞다.</para>
        /// <para>새 밤마다 재시작 수(k)와 체크포인트를 비우고, 점검표·밤 편성을 만든 뒤 <b>밤 시작 스냅샷</b>을 찍는다.</para>
        /// </summary>
        /// <param name="day">일차(1부터).</param>
        /// <param name="clockMinutes">현재 게임 시각(<b>근무 시작부터의 분</b>)을 돌려주는 함수. 위반 시각 기록과 판정 시간창에 쓴다. null이면 -1로 기록.</param>
        public static void BeginNight(int day, Func<int> clockMinutes)
        {
            EnsureRun();
            BeginNightCore(day, clockMinutes, false);
        }

        private static void BeginNightCore(int day, Func<int> clockMinutes, bool isRestart)
        {
            if (_book != null)
            {
                Debug.LogWarning("[NightRun] 이전 밤이 끝나지 않은 채 새 밤을 시작합니다. 이전 밤의 남은 판정은 버립니다.");
                _book.Settled -= OnSettled;
                _book.Abandon();
            }

            Day = Mathf.Max(1, day);
            _clockMinutes = clockMinutes;
            _axes.SoftCap = null;
            ViolationMinutesToday.Clear();
            InspectedToday.Clear();
            VisitedToday.Clear();
            ClearRaised();
            ResetTabletState(false);

            // 일차 하한을 **덱보다 먼저** 건다. 카드의 발동 자격이 연출 구간을 보고 정해지므로
            // 순서가 뒤집히면 그날 하한이 카드 풀에 반영되지 않는다. 하한은 연출 구간에만 걸리고
            // 생존 수치는 올리지 않는다(2026-09-30 새 기획서).
            _bands.SetDayFloor(DayFloor.Of(Day));

            InspectionPlan plannedToday = null;
            if (!isRestart)
            {
                RestartsTonight = 0;
                _checkpoint = null;

                // 옛 조우 연출기(8장면)는 새 편성이 꺼져 있을 때만 — 조우 배정은 축을 읽지 않으므로 하한 뒤·덱 앞이 안전하다.
                // 새 편성(ProgramEnabled)에서는 조우를 ProgramDirector가 정한다 — 옛 조우 연출기는 쓰지 않는다.
                if (!ProgramEnabled)
                {
                    EnsureEncounter();
                    if (_encounter != null)
                    {
                        _encounter.BeginNight(Day);
                    }
                }

                IReadOnlyList<RuleSO> deck = LoadDeck(Day);
                DeckToday.Clear();
                DeckToday.AddRange(deck);

                // 점검 편성도 밤 시작에 확정한다 — 이상의 축은 연출 구간(하한 적용 뒤)을 본다.
                plannedToday = BuildInspectionPlan(Day);
                Board.Begin(plannedToday);

                // 밤 편성(조우 슬롯 + 새 수칙 덱)은 점검 편성을 본다(소년 착석 = 교실 점검 등).
                OnNightPlanned(plannedToday);
            }

            // 재시작한 밤은 덱·점검을 다시 뽑지 않는다 — 이상 배정·덱·조우·역설 편성은 밤 시작에 확정된다(2026-09-30 최종 기획서).
            TargetsInUse = ResolveTargets();
            _book = new RuleBook(DeckToday, _axes, _bands, TargetsInUse);
            _book.Settled += OnSettled;
            Paradox.BeginNight(_bands.Shown);   // 그날 상한을 근무 시작 시 신뢰로 고정한다(기획서 C절).
            _book.BeginNight();   // 밤 시작부터 감시하는 장기 카드(C6)를 시작한다.

            if (!isRestart)
            {
                _nightStart = NightSnapshot.Take(Day, 0, false, _axes, _bands, _ledger, SnapshotParts());
            }

            // 씬이 새로 열렸으므로 연출에 현재 구간을 from == to로 한 번 알린다.
            _bands.BroadcastAll();

            if (plannedToday != null)
            {
                EventBus.RaiseInspectionPlanned(plannedToday);
            }

            if (_axes.IsLocked)
            {
                Debug.LogWarning("[NightRun] 이미 포획된 회차에서 밤을 시작했습니다. 판정은 동작하지 않습니다.");
            }
        }

        private static InspectionPlan BuildInspectionPlan(int day)
        {
            if (InspectionPlanOverride != null)
            {
                return InspectionPlanOverride(day, _bands.Shown) ?? InspectionPlan.Empty(day);
            }

            if (!InspectionsEnabled)
            {
                return InspectionPlan.Empty(day);
            }

            InspectionPlan plan = _assigner.Build(day, _bands.Shown);
            Debug.Log("[NightRun] " + _assigner.LastReport);
            return plan;
        }

        private static ISnapshotable[] SnapshotParts()
        {
            return _finalBook != null ? new ISnapshotable[] { Board, _finalBook } : new ISnapshotable[] { Board };
        }

        /// <summary>
        /// 판정 시간 경과. 매 프레임 불러도 된다.
        /// <b>Tab·일시정지 중에는 부르지 않는다</b>(0을 넘겨도 무시한다). 판정 정지 구간에는 시간이 흐르지 않는다.
        /// </summary>
        public static void Tick(float judgeSeconds)
        {
            if (_book == null || judgeSeconds <= 0f)
            {
                return;
            }

            // 긴장 디렉터는 판정 정지 구간에도 흐른다(슬롯·단서는 디렉터가 밤 시각으로 거른다).
            DirectionTick(judgeSeconds);
            if (_book == null)
            {
                return;
            }

            if (!IsJudgingNow)
            {
                return;
            }

            _axes.SoftCap = null;
            _axes.BeginFrame();
            try
            {
                JudgeSignal tick = JudgeSignal.Tick(judgeSeconds);
                _book.Dispatch(tick);
                FinalDispatch(tick, true);
                ObserveEncounter(tick);   // 시야에 걸려 대기 중인 배치를 여기서 푼다.
                PollParadox();
            }
            finally
            {
                _axes.EndFrame();
            }

            CloseIfCaptured();
        }

        /// <summary>
        /// 판정 신호 하나를 보낸다(연결 약속 §4). 밤이 진행 중이 아니면 무시한다.
        /// 판정 정지 구간에는 상태 신호(공간·구역·손전등·Tab)만 판정에 넘긴다 — 사건 신호는 버린다.
        /// 한 신호가 낸 델타는 한 프레임으로 묶는다 — 두 축이 함께 100이면 초과량이 큰 축 하나로 붙잡힌다.
        /// </summary>
        public static void Send(in JudgeSignal signal)
        {
            if (_book == null)
            {
                return;
            }

            if (AbsorbTabSignal(signal))
            {
                return;
            }

            bool judging = IsJudgingNow;
            _axes.SoftCap = judging ? (int?)null : Deltas.SoftCap;

            bool counts = signal.Space != SpaceId.None && !_book.World.TabOpen && !IsCaptured;
            if (counts && signal.Kind == SignalKind.InspectionCompleted)
            {
                InspectedToday.Add(signal.Space);
            }

            if (counts && signal.Kind == SignalKind.SpaceEntered)
            {
                VisitedToday.Add(signal.Space);
            }

            _axes.BeginFrame();
            try
            {
                if (judging || IsStateSignal(signal.Kind))
                {
                    _book.Dispatch(signal);
                }

                // 새 수칙은 모든 신호로 상태(공간·자세·손전등)를 갱신하고, 판정 구간에만 판정한다.
                if (!IsCaptured)
                {
                    FinalDispatch(signal, judging);
                }

                DirectionObserve(signal);

                if (judging)
                {
                    ObserveEncounter(signal);
                    PollParadox();
                }
            }
            finally
            {
                _axes.EndFrame();
            }

            // 대기 중인 처벌은 판정 구간에 다음 공간 경계를 넘을 때 나온다.
            if (counts && judging && signal.Kind == SignalKind.SpaceEntered && _book != null && !IsCaptured)
            {
                TryPunish(signal.Space);
            }

            _axes.SoftCap = null;
            CloseIfCaptured();
            FlushDirection();
        }

        /// <summary>판정 정지 중에도 판정에 넘기는 상태 신호인지. 넘기지 않으면 「나간 적 없는 공간에서 나감」이 생긴다.</summary>
        private static bool IsStateSignal(SignalKind kind)
        {
            return kind == SignalKind.SpaceEntered || kind == SignalKind.SpaceExited
                || kind == SignalKind.ZoneEntered || kind == SignalKind.ZoneExited
                || kind == SignalKind.FlashlightChanged || kind == SignalKind.TabChanged;
        }

        // ── 점검과 보고 ─────────────────────────────────────────

        /// <summary>
        /// 점검 보고(최종 기획서 「보고 경제」). 거리 2m·응시 1초·0.5초 길게 누르기는 센서·태블릿이 거른 뒤 부른다.
        /// 항목당 1회, 되돌릴 수 없다. 판정 정지 구간·03:30 이후의 델타는 95에서 멈춘다(보고는 04:00까지 받는다).
        /// </summary>
        /// <param name="itemId">항목 ID(「H-2」). 씬 대상 ID(<c>inspect.H-2</c>)도 받는다.</param>
        /// <param name="saysAnomaly">[이상]이면 true, [정상]이면 false.</param>
        public static InspectionReport ReportInspection(string itemId, bool saysAnomaly)
        {
            InspectionItem item = InspectionCatalog.FindByTarget(itemId);
            string id = item != null ? item.Id : itemId;

            if (_book == null) return InspectionReport.Reject(id, saysAnomaly, ReportRejection.NoNight);
            if (IsCaptured) return InspectionReport.Reject(id, saysAnomaly, ReportRejection.Captured);

            _axes.SoftCap = IsJudgingNow ? (int?)null : Deltas.SoftCap;
            InspectionReport report;
            try
            {
                report = Board.Report(id, saysAnomaly, CurrentMinute(), _axes, _book.World.CurrentSpace);
            }
            finally
            {
                _axes.SoftCap = null;
            }

            if (report.Accepted)
            {
                OnFinalInspectionReported(report);
                EventBus.RaiseInspectionReported(report);
            }

            CloseIfCaptured();
            return report;
        }

        /// <summary>
        /// 점검 수칙 「가까이」 위반(0.8m 안에서 들여다보기·뒤로 돌아가기·건드리기). 항목마다 한 번 그 축 +6과 1초 놀람.
        /// 수칙이라 판정 시간창 밖에서는 판정하지 않는다. 실제로 적용했으면 true.
        /// </summary>
        public static bool InspectionStartle(string itemId)
        {
            if (_book == null || IsCaptured || !IsJudgingNow) return false;

            InspectionItem item = InspectionCatalog.FindByTarget(itemId);
            if (item == null || !Board.Startle(item.Id, _axes, _book.World.CurrentSpace)) return false;

            EventBus.RaiseInspectionStartled(item.Id, item.Axis);
            CloseIfCaptured();
            return true;
        }

        /// <summary>환청이 그 항목 근처에서 났다(환청 연출이 부른다). 그 뒤 [이상] 보고는 G2 위반(청각 +6)이다.</summary>
        public static void MarkHallucination(string itemId)
        {
            InspectionItem item = InspectionCatalog.FindByTarget(itemId);
            Board.MarkHallucination(item != null ? item.Id : itemId);
        }

        /// <summary>T4 역보고를 그 항목에 건다(여자아이가 칸으로 들어가는 것을 봤을 때). 수칙·조우 쪽이 부른다.</summary>
        public static void SetReverseReport(string itemId, bool active)
        {
            InspectionItem item = InspectionCatalog.FindByTarget(itemId);
            Board.SetReverseReport(item != null ? item.Id : itemId, active);
        }

        // ── 경고와 처벌 ─────────────────────────────────────────

        /// <summary>
        /// 경고를 더한다(점검 미완료 등). 세 번째 도장이면 처벌이 대기에 들어가고, 판정 구간에 다음 공간 경계를 넘을 때 나온다.
        /// </summary>
        /// <param name="count">더할 경고 수.</param>
        /// <param name="sourceId">출처(로그용).</param>
        /// <returns>이번에 새로 대기에 들어간 처벌 수.</returns>
        public static int AddWarning(int count, string sourceId)
        {
            EnsureRun();
            return _ledger.Add(count);
        }

        /// <summary>
        /// 대기 중인 처벌을 하나 꺼내 가장 높은 감각 축에 준다(동점은 청각 &gt; 조도 &gt; 배치). 95에서 멈춘다 — 처벌로는 붙잡히지 않는다.
        /// </summary>
        private static void TryPunish(SpaceId space)
        {
            if (IsCaptured || _ledger.PendingPunishments <= 0)
            {
                return;
            }

            if (!_ledger.TryTakePunishment())
            {
                return;
            }

            FearAxis axis = HighestSensory();
            int? before = _axes.SoftCap;
            _axes.SoftCap = Deltas.SoftCap;
            _axes.Apply(axis, Deltas.Punishment, "처벌", space);
            _axes.SoftCap = before;
            EventBus.RaisePunished(axis);
        }

        /// <summary>가장 높은 감각 축. 동점이면 청각 &gt; 조도 &gt; 배치.</summary>
        public static FearAxis HighestSensory()
        {
            EnsureRun();
            FearAxis best = FearAxis.Auditory;
            int bestValue = _axes.GetValue(FearAxis.Auditory);
            FearAxis[] order = { FearAxis.Illuminance, FearAxis.Layout };
            for (int i = 0; i < order.Length; i++)
            {
                int v = _axes.GetValue(order[i]);
                if (v > bestValue)
                {
                    best = order[i];
                    bestValue = v;
                }
            }

            return best;
        }

        // ── 체크포인트와 재시작 ───────────────────────────────────

        /// <summary>
        /// 근무일지 중간 서명 — 02:16 체크포인트를 찍는다(무한 루프 방지 장치 5). 밤당 한 번, 이완 구간에만.
        /// 시계를 모르거나 판정 시간창이 꺼져 있으면 시각 검사를 건너뛴다.
        /// </summary>
        /// <returns>찍었으면 true.</returns>
        public static bool SignCheckpoint()
        {
            if (_book == null || IsCaptured || _checkpoint != null)
            {
                return false;
            }

            int minute = CurrentMinute();
            if (JudgingWindowEnabled && minute >= 0 && !NightClock.CanSignCheckpoint(minute))
            {
                return false;
            }

            _checkpoint = NightSnapshot.Take(Day, NightClock.Call2, true, _axes, _bands, _ledger, SnapshotParts());
            return true;
        }

        /// <summary>
        /// 붙잡힌 밤을 다시 시작한다(2026-09-30 최종 기획서 「붙잡힘과 재시작」).
        /// <list type="bullet">
        /// <item>체크포인트가 있으면 그 스냅샷, 없으면 밤 시작 스냅샷으로 돌아간다.</item>
        /// <item>감각 축 = max(min(스냅샷, 40), 스냅샷 − 10×k). 신뢰·연출 도달값·경고·처벌 대기·점검판(한도 사용량 등)은 스냅샷 그대로 —
        /// 단 스냅샷 뒤에 이미 나온 처벌은 되풀이하지 않는다. 이미 한 보고는 「보고됨」으로 남는다.</item>
        /// <item>덱·점검·밤 편성은 다시 뽑지 않는다. 판정은 새로 시작한다.</item>
        /// <item>이미 <see cref="RestartPolicy.AbsenceAfterRestarts"/>번 재시작한 밤이면 결근으로 넘긴다(<see cref="NightOutcome.Absent"/>, <see cref="EventBus.DayEnded"/>).</item>
        /// </list>
        /// 구동기는 결과의 <see cref="RestartResult.StartMinute"/>로 게임 시계를 돌려놓는다.
        /// 재시작 카드는 <see cref="LastCaptureSources"/>로 「그 축을 올린 수칙·점검 항목」을 보여 준다.
        /// </summary>
        public static RestartResult RestartAfterCapture()
        {
            if (_axes == null || !_axes.IsLocked || Day <= 0 || _nightStart == null)
            {
                return new RestartResult(RestartKind.None, RestartsTonight, -1, default);
            }

            FearAxis captured = _axes.Cause.Axis;
            _lastCaptureSources = new List<string>(RaisedSources(captured));
            if (RestartPolicy.IsAbsence(RestartsTonight))
            {
                return EndAsAbsent(captured);
            }

            RestartsTonight++;
            int k = RestartsTonight;
            NightSnapshot from = _checkpoint ?? _nightStart;

            _axes.Restore(
                RestartPolicy.RestoredValue(from.Value(FearAxis.Auditory), k),
                RestartPolicy.RestoredValue(from.Value(FearAxis.Illuminance), k),
                RestartPolicy.RestoredValue(from.Value(FearAxis.Layout), k),
                from.Value(FearAxis.Trust));
            _bands.RestoreReached(from.ReachedCopy());
            _ledger.RestoreFromSnapshot(from);
            from.RestoreParts(SnapshotParts());

            EndEncounterNight();
            BeginNightCore(Day, _clockMinutes, true);
            DirectionRestart(k, from.StartMinute);

            RestartResult result = new RestartResult(
                _checkpoint != null ? RestartKind.FromCheckpoint : RestartKind.FromNightStart,
                k,
                from.StartMinute,
                captured);
            EventBus.RaiseNightRestarted(result);
            return result;
        }

        /// <summary>
        /// 조퇴 — k≥4부터 근무일지에서 스스로 고른다. 결과는 결근과 같다. 조건이 안 되면 None.
        /// </summary>
        public static RestartResult LeaveVoluntarily()
        {
            if (_axes == null || Day <= 0 || _nightStart == null || !RestartPolicy.CanLeaveVoluntarily(RestartsTonight))
            {
                return new RestartResult(RestartKind.None, RestartsTonight, -1, default);
            }

            if (_book != null)
            {
                _book.Settled -= OnSettled;
                _book.Abandon();
                _book = null;
            }

            return EndAsAbsent(_axes.IsLocked ? _axes.Cause.Axis : HighestSensory());
        }

        /// <summary>
        /// 결근 처리: 판정 없이 그 밤을 넘긴다. 감각 축은 min(현재, 60), 신뢰·연출 도달값·경고는 밤 시작 스냅샷,
        /// 남은 점검은 경고 없는 미완료, 결과는 <see cref="NightOutcome.Absent"/>로 <see cref="EventBus.DayEnded"/>에 보낸다.
        /// </summary>
        private static RestartResult EndAsAbsent(FearAxis captured)
        {
            _axes.Restore(
                RestartPolicy.AbsenceValue(_axes.GetValue(FearAxis.Auditory)),
                RestartPolicy.AbsenceValue(_axes.GetValue(FearAxis.Illuminance)),
                RestartPolicy.AbsenceValue(_axes.GetValue(FearAxis.Layout)),
                _nightStart.Value(FearAxis.Trust));
            _bands.RestoreReached(_nightStart.ReachedCopy());
            _ledger.RestoreFromSnapshot(_nightStart);
            Board.Settle(_axes, false);
            EndEncounterNight();

            _lastSummary = BuildSummary(NightOutcome.Absent);
            CloseNight();

            RestartResult result = new RestartResult(RestartKind.Absent, RestartsTonight, -1, captured);
            EventBus.RaiseNightRestarted(result);
            EventBus.RaiseDayEnded(_lastSummary);
            return result;
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

        /// <summary>오늘 역설 문자를 한 통이라도 보냈는지. N1(재방문)이 같은 날 겹치지 않게 하려고 쓴다.</summary>
        private static bool ParadoxSentToday()
        {
            return Paradox.Today.Count > 0;
        }

        /// <summary>
        /// 조우 연출기에 신호를 흘린다. <b>수칙 판정이 먼저 본 뒤</b>에 부른다 —
        /// 모형 배치가 카드 판정에 끼어들지 않게 하기 위해서다. Tab 중(옛 규칙)·포획 뒤에는 보내지 않는다.
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

        /// <summary>밤을 닫으며 미관찰 장면을 다음 날로 이월한다. 정상 종료·중단·재시작·결근에서 부른다.</summary>
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

        /// <summary>진행 중인 카드 중 역설 문자를 보낼 것이 있으면 보낸다. Tab 중(옛 규칙)에는 보내지 않는다.</summary>
        private static void PollParadox()
        {
            if (_book == null || _book.World.TabOpen || IsCaptured)
            {
                return;
            }

            Paradox.Poll(_book, _bands.Shown, CurrentMinute());
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
        /// 근무 종료를 요청한다(04:00). 남은 카드를 덱 순서로 정산하고, 미완료 점검마다 경고 1(이상이 있던 항목은 +8)을 준 뒤
        /// <see cref="EventBus.DayEnded"/>를 보내고 true.
        /// 정산 델타는 95에서 멈춘다 — 04:00 정산으로는 붙잡히지 않는다(무한 루프 방지 장치 3). 04:00과 붙잡힘이 같은 프레임이면 04:00이 먼저다.
        /// 밤이 진행 중이 아니거나 이미 포획됐으면 false.
        /// </summary>
        public static bool RequestEndNight()
        {
            if (_book == null || IsCaptured)
            {
                return false;
            }

            _axes.SoftCap = Deltas.SoftCap;
            try
            {
                _book.EndNight();
                FinalEndNight();
                EndEncounterNight();
                InspectionSettlement settle = Board.Settle(_axes, true);
                if (settle.Unfinished > 0)
                {
                    _ledger.Add(settle.Unfinished);
                }

                // TODO(문자 시스템): P형 미도달 정산은 여기, 수칙 정산 뒤에 온다.
            }
            finally
            {
                _axes.SoftCap = null;
            }

            if (IsCaptured)
            {
                // 이미 100이던 축이 있었다(정산 델타로는 붙잡히지 않는다). 포획 경로가 처리했으므로 DayEnded는 보내지 않는다.
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
            return BuildSummary(_axes.IsLocked ? NightOutcome.Captured : NightOutcome.Completed);
        }

        private static DaySummary BuildSummary(NightOutcome outcome)
        {
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
                outcome,
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
            int? cap = _axes.SoftCap;
            _axes.SoftCap = null;
            _axes.Apply(axis, remain, "debug", space);
            _axes.SoftCap = cap;
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

            ViolationMinutesToday.Add(CurrentMinute());
        }

        /// <summary>감각 축을 올린 출처를 이번 시도 기록에 더한다(재시작 카드용). 디버그·처벌은 이름이 아니므로 뺀다.</summary>
        private static void OnAxisRaised(FearAxis axis, int amount, string sourceId)
        {
            int i = (int)axis;
            if (i < 0 || i >= RaisedThisAttempt.Length) return;

            string name = DisplaySource(sourceId);
            if (string.IsNullOrEmpty(name) || name == "debug" || name == "처벌") return;
            if (!RaisedThisAttempt[i].Contains(name)) RaisedThisAttempt[i].Add(name);
        }

        /// <summary>
        /// 출처 ID를 재시작 카드에 쓸 이름으로 줄인다: 「G2:H-3[이상]」 → 「G2」, 「H-2[이상]」 → 「H-2」, 「H-1(가까이)」 → 「H-1」.
        /// </summary>
        public static string DisplaySource(string sourceId)
        {
            if (string.IsNullOrEmpty(sourceId)) return string.Empty;

            int colon = sourceId.IndexOf(':');
            if (colon > 0) return sourceId.Substring(0, colon);

            int cut = sourceId.IndexOfAny(new[] { '[', '(' });
            return cut > 0 ? sourceId.Substring(0, cut) : sourceId;
        }

        private static void ClearRaised()
        {
            for (int i = 0; i < RaisedThisAttempt.Length; i++)
            {
                RaisedThisAttempt[i].Clear();
            }
        }

        private static void CloseNight()
        {
            DirectionAbort("밤 닫힘");
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

            // 옛 24장 카드와 그 편성표·하루 6장 배정기는 2026-10-03에 폐기했다 — 새 편성이든 아니든 옛 판정 책은 비어 있다.
            // 판정은 새 수칙(FinalRuleBook)이 한다. 되살리지 마십시오.
            return new List<RuleSO>();
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
            _ledger = null;
            _book = null;
            _clockMinutes = null;
            Day = 0;
            RestartsTonight = 0;
            _nightStart = null;
            _checkpoint = null;
            _lastCaptureSources = new List<string>();
            ClearRaised();
            ResetExtensions(true);
            ViolationMinutesToday.Clear();
            InspectedToday.Clear();
            VisitedToday.Clear();
            DeckToday.Clear();
            Board.Begin(InspectionPlan.Empty(0));
            _assigner = null;
            _lastSummary = default;
            DeckOverride = null;
            RegisteredTargets = null;
            TargetsInUse = null;
            JudgingWindowEnabled = false;
            InspectionsEnabled = false;
            InspectionPlanOverride = null;
            _encounter = null;
        }
    }
}

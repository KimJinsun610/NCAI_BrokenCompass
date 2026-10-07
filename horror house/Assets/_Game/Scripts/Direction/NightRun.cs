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
    /// 끄면(기본값·테스트) 시각과 무관하게 판정한다. 태블릿을 든 동안에도 판정은 흐른다(NightRun.Tablet.cs).
    /// </para>
    /// <para>
    /// <b>점검(<see cref="InspectionBoard"/>)</b>: <see cref="InspectionsEnabled"/>가 켜져 있으면 밤 시작에 <see cref="AnomalyAssigner"/>가
    /// 그날 점검표를 편성한다(재시작해도 그대로). 보고는 <see cref="ReportInspection"/>, 「가까이」는 <see cref="InspectionStartle"/>.
    /// 04:00 정산에서 미완료마다 경고 1, 이상이 있던 항목은 +8. 끄면(기본값·테스트) 점검표가 비어 있다.
    /// 옛 「미방문 벌점 +9」는 점검 미완료 경고가 넘겨받아 없앴다(되살리지 말 것).
    /// 점검 편성 뒤에는 밤 편성(조우 슬롯 + 새 수칙 덱, <see cref="Program"/> — NightRun.Program.cs)이 온다.
    /// </para>
    /// <para>
    /// 판정은 새 수칙(<see cref="FinalRules"/>, 그날 편성 <see cref="Program"/>)이 한다. 옛 24장 카드·하루 6장 배정기·옛 판정 책(RuleBook과 조건들)·
    /// 옛 역설/조우 연출기는 2026-10-03에 폐기했다. 밤 동안의 현재 공간은 여기서 직접 들고, 근무일지는 새 수칙 결과로 만든다.
    /// 역설 문자는 아직 새 편성에 없다(10단계).
    /// </para>
    /// </summary>
    public static partial class NightRun
    {
        private static FearAxisSystem _axes;
        private static BandResolver _bands;
        private static WarningLedger _ledger;
        private static bool _nightOpen;
        private static SpaceId _currentSpace;
        private static Func<int> _clockMinutes;
        private static readonly List<int> ViolationMinutesToday = new List<int>();
        private static readonly InspectionBoard Board = new InspectionBoard();
        private static readonly List<string>[] RaisedThisAttempt = { new List<string>(), new List<string>(), new List<string>() };
        private static AnomalyAssigner _assigner;
        private static DaySummary _lastSummary;
        private static NightSnapshot _nightStart;
        private static NightSnapshot _checkpoint;
        private static List<string> _lastCaptureSources = new List<string>();

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
            get { return _nightOpen; }
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

        /// <summary>플레이어 발밑 기준점이 지금 속한 공간(밤 동안 공간 신호로 갱신). 밤이 아니거나 모르면 None.</summary>
        public static SpaceId CurrentSpace
        {
            get { return _nightOpen ? _currentSpace : SpaceId.None; }
        }

        /// <summary>
        /// 태블릿 「근무 수칙」에 실을 그날 수칙 — 새 수칙의 표시 전용 <see cref="RuleSO"/>(<see cref="DisplayDeck"/>). 점검표는 메시지(<see cref="ChecklistMessage"/>)로 간다.
        /// 밤이 닫힌 뒤에도 다음 편성 전까지 남는다. 새 편성이 꺼져 있으면 비어 있다.
        /// </summary>
        public static IReadOnlyList<RuleSO> TodayDeck
        {
            get { return _displayDeck; }
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
            _nightOpen = false;
            _currentSpace = SpaceId.None;
            _clockMinutes = null;
            Day = 0;
            RestartsTonight = 0;
            _nightStart = null;
            _checkpoint = null;
            _lastCaptureSources = new List<string>();
            ClearRaised();
            ResetExtensions(false);
            ResetOrders();
            ViolationMinutesToday.Clear();
            _lastSummary = default;
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
            if (_nightOpen)
            {
                Debug.LogWarning("[NightRun] 이전 밤이 끝나지 않은 채 새 밤을 시작합니다. 이전 밤의 남은 판정은 버립니다.");
            }

            Day = Mathf.Max(1, day);
            _clockMinutes = clockMinutes;
            _axes.SoftCap = null;
            ViolationMinutesToday.Clear();
            ClearRaised();
            ResetTabletState();

            // 일차 하한을 **덱보다 먼저** 건다. 카드의 발동 자격이 연출 구간을 보고 정해지므로
            // 순서가 뒤집히면 그날 하한이 카드 풀에 반영되지 않는다. 하한은 연출 구간에만 걸리고
            // 생존 수치는 올리지 않는다(2026-09-30 새 기획서).
            _bands.SetDayFloor(DayFloor.Of(Day));

            InspectionPlan plannedToday = null;
            if (!isRestart)
            {
                RestartsTonight = 0;
                _checkpoint = null;

                // 점검 편성도 밤 시작에 확정한다 — 이상의 축은 연출 구간(하한 적용 뒤)을 본다.
                plannedToday = BuildInspectionPlan(Day);
                Board.Begin(plannedToday, DripFor(plannedToday));

                // 밤 편성(조우 슬롯 + 새 수칙 덱)은 점검 편성을 본다(소년 착석 = 교실 점검 등).
                OnNightPlanned(plannedToday);

                // 51차: 편성된 조우·수칙이 요구하는 점검을 맞춘다(시체 낙하 = 사다리 C-3, T4 = 변기 T-1 목격 전까지 묶음).
                InspectionPlan patched = PatchPlanForProgram(plannedToday, _program);
                if (!ReferenceEquals(patched, plannedToday))
                {
                    plannedToday = patched;
                    Board.Begin(plannedToday, DripFor(plannedToday));
                    Debug.Log("[NightRun] 점검 편성 보정 — " + plannedToday);
                }

                HoldForProgram(plannedToday, _program);

                // 50차: 점검 순차 지시기 — 조우 슬롯을 보고 호출 1에 아낄 공간을 정한다.
                BeginOrders(plannedToday);

                // 54차: [근무 지시] — 점검 공백을 걷고·닫고·끄고·적는 업무로 메운다.
                BeginDuties();

                // 56차: 손전등 배터리 — 밤 시작 100%·예비 0, 칸 자리는 화면 쪽이 씬의 칸으로 채운다.
                BeginBattery();
            }

            // 재시작한 밤은 덱·점검을 다시 뽑지 않는다 — 이상 배정·덱·조우·역설 편성은 밤 시작에 확정된다(2026-09-30 최종 기획서).
            _currentSpace = SpaceId.None;
            _nightOpen = true;

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
            List<ISnapshotable> parts = new List<ISnapshotable> { Board };
            if (_finalBook != null) parts.Add(_finalBook);
            if (_orders != null) parts.Add(_orders);
            if (_duties != null) parts.Add(_duties);
            if (_battery != null) parts.Add(_battery);
            if (_batteryPlan != null) parts.Add(_batteryPlan);
            return parts.ToArray();
        }

        /// <summary>
        /// 판정 시간 경과. 매 프레임 불러도 된다.
        /// <b>Tab·일시정지 중에는 부르지 않는다</b>(0을 넘겨도 무시한다). 판정 정지 구간에는 시간이 흐르지 않는다.
        /// </summary>
        public static void Tick(float judgeSeconds)
        {
            if (!_nightOpen || judgeSeconds <= 0f)
            {
                return;
            }

            // 긴장 디렉터는 판정 정지 구간에도 흐른다(슬롯·단서는 디렉터가 밤 시각으로 거른다).
            DirectionTick(judgeSeconds);
            if (!_nightOpen)
            {
                return;
            }

            // 점검 지시도 판정 정지 구간에 흐른다(출근 직후 첫 지시, 호출 2는 이완이 끝나는 순간).
            OrdersTick(judgeSeconds);
            DutiesTick(judgeSeconds);

            if (!IsJudgingNow)
            {
                return;
            }

            _axes.SoftCap = null;
            _axes.BeginFrame();
            try
            {
                JudgeSignal tick = JudgeSignal.Tick(judgeSeconds);
                FinalDispatch(tick, true);
                ParadoxObserve(tick, true);
                UnavoidableObserve(tick, true);
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
            if (!_nightOpen)
            {
                return;
            }

            if (AbsorbTabSignal(signal))
            {
                return;
            }

            bool judging = IsJudgingNow;
            _axes.SoftCap = judging ? (int?)null : Deltas.SoftCap;

            bool counts = signal.Space != SpaceId.None && !IsCaptured;
            _axes.BeginFrame();
            try
            {
                TrackSpace(signal);
                OrdersObserve(signal);
                DutiesObserve(signal);
                FinaleObserve(signal);

                // 새 수칙은 모든 신호로 상태(공간·자세·손전등)를 갱신하고, 판정 구간에만 판정한다.
                if (!IsCaptured)
                {
                    FinalDispatch(signal, judging);
                    ParadoxObserve(signal, judging);
                    UnavoidableObserve(signal, judging);
                }

                DirectionObserve(signal);
            }
            finally
            {
                _axes.EndFrame();
            }

            // 대기 중인 처벌은 판정 구간에 다음 공간 경계를 넘을 때 나온다.
            if (counts && judging && signal.Kind == SignalKind.SpaceEntered && _nightOpen && !IsCaptured)
            {
                TryPunish(signal.Space);
            }

            _axes.SoftCap = null;
            CloseIfCaptured();
            FlushDirection();
        }

        /// <summary>현재 공간을 갱신한다(판정 정지 중에도). 경계 위에서는 직전 공간을 유지한다 — 나간 공간이 지금 공간일 때만 비운다.</summary>
        private static void TrackSpace(in JudgeSignal signal)
        {
            if (signal.Kind == SignalKind.SpaceEntered)
            {
                _currentSpace = signal.Space;
            }
            else if (signal.Kind == SignalKind.SpaceExited && _currentSpace == signal.Space)
            {
                _currentSpace = SpaceId.None;
            }
        }

        // ── 점검과 보고 ─────────────────────────────────────────

        /// <summary>
        /// 점검 보고(최종 기획서 「보고 경제」). 거리 2m·응시 1초·0.5초 길게 누르기는 센서·태블릿이 거른 뒤 부른다.
        /// 항목당 1회(60차부터 <see cref="ReviseInspection"/>으로 판정을 바꿀 수 있다). 판정 정지 구간·03:30 이후의 델타는 95에서 멈춘다(보고는 04:00까지 받는다).
        /// </summary>
        /// <param name="itemId">항목 ID(「H-2」). 씬 대상 ID(<c>inspect.H-2</c>)도 받는다.</param>
        /// <param name="saysAnomaly">[이상]이면 true, [정상]이면 false.</param>
        public static InspectionReport ReportInspection(string itemId, bool saysAnomaly)
        {
            InspectionItem item = InspectionCatalog.FindByTarget(itemId);
            string id = item != null ? item.Id : itemId;

            if (!_nightOpen) return InspectionReport.Reject(id, saysAnomaly, ReportRejection.NoNight);
            if (IsCaptured) return InspectionReport.Reject(id, saysAnomaly, ReportRejection.Captured);
            if (item != null && _unavoidable.Banned != SpaceId.None && SpaceIds.Canonical(item.Space) == _unavoidable.Banned)
            {
                return InspectionReport.Reject(id, saysAnomaly, ReportRejection.SpaceClosed);   // 회피 불가 역설 — 금일 재입실 불가
            }

            _axes.SoftCap = IsJudgingNow ? (int?)null : Deltas.SoftCap;
            InspectionReport report;
            try
            {
                report = Board.Report(id, saysAnomaly, CurrentMinute(), _axes, _currentSpace);
            }
            finally
            {
                _axes.SoftCap = null;
            }

            if (report.Accepted)
            {
                OrdersNoteReport();
                OnFinalInspectionReported(report);
                EventBus.RaiseInspectionReported(report);
            }

            CloseIfCaptured();
            return report;
        }

        /// <summary>
        /// 60차 — 이미 보고한 항목의 판정을 바꾼다(민: 「점검을 보고한 물품들의 이상/정상 여부를 수정할 수 있게」). 현장에서만(센서가 거른 뒤 부른다).
        /// 지난 보고의 수치 몫을 되돌리고 새 판정으로 다시 보고한다(<see cref="InspectionBoard.Revise"/>). 점검 지시의 「보고 뒤 다음 지시」는 다시 세지 않는다.
        /// </summary>
        public static InspectionReport ReviseInspection(string itemId, bool saysAnomaly)
        {
            InspectionItem item = InspectionCatalog.FindByTarget(itemId);
            string id = item != null ? item.Id : itemId;
            if (!_nightOpen) return InspectionReport.Reject(id, saysAnomaly, ReportRejection.NoNight);
            if (IsCaptured) return InspectionReport.Reject(id, saysAnomaly, ReportRejection.Captured);
            if (item != null && _unavoidable.Banned != SpaceId.None && SpaceIds.Canonical(item.Space) == _unavoidable.Banned)
            {
                return InspectionReport.Reject(id, saysAnomaly, ReportRejection.SpaceClosed);
            }

            _axes.SoftCap = IsJudgingNow ? (int?)null : Deltas.SoftCap;
            InspectionReport report;
            try
            {
                report = Board.Revise(id, saysAnomaly, CurrentMinute(), _axes, _currentSpace);
            }
            finally
            {
                _axes.SoftCap = null;
            }

            if (report.Accepted) EventBus.RaiseInspectionReported(report);
            CloseIfCaptured();
            return report;
        }

        /// <summary>
        /// 점검 수칙 「가까이」 위반(0.8m 안에서 들여다보기·뒤로 돌아가기·건드리기). 항목마다 한 번 그 축 +6과 1초 놀람.
        /// 수칙이라 판정 시간창 밖에서는 판정하지 않는다. 실제로 적용했으면 true.
        /// </summary>
        public static bool InspectionStartle(string itemId)
        {
            if (!_nightOpen || IsCaptured || !IsJudgingNow) return false;

            InspectionItem item = InspectionCatalog.FindByTarget(itemId);
            if (item == null || !Board.IsIssued(item.Id)) return false;   // 50차: 지시받지 않은 물품은 아직 점검 대상이 아니다
            if (!Board.Startle(item.Id, _axes, _currentSpace)) return false;

            if (_tension != null) _tension.Pacer.Impulse(PacerImpulse.Startle);
            EventBus.RaiseInspectionStartled(item.Id, item.Axis);
            CloseIfCaptured();
            return true;
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
        /// 지금 근무일지에 서명할 수 있는지(<see cref="SignCheckpoint"/>가 받아 줄지) — 밤 진행 중 · 붙잡히지 않음 · 아직 서명 전 · 이완 구간(01:52~02:16).
        /// 시계를 모르거나 판정 시간창이 꺼져 있으면 시각 검사를 건너뛴다. 경비실 근무일지(<c>DutyLogBook</c>)가 [E] 안내를 띄울지 이것으로 정한다(54차 QA).
        /// </summary>
        public static bool CanSignCheckpointNow
        {
            get
            {
                if (!_nightOpen || IsCaptured || _checkpoint != null) return false;
                int minute = CurrentMinute();
                return !JudgingWindowEnabled || minute < 0 || NightClock.CanSignCheckpoint(minute);
            }
        }

        /// <summary>
        /// 근무일지 중간 서명 — 02:16 체크포인트를 찍는다(무한 루프 방지 장치 5). 밤당 한 번, 이완 구간에만.
        /// 시계를 모르거나 판정 시간창이 꺼져 있으면 시각 검사를 건너뛴다.
        /// </summary>
        /// <returns>찍었으면 true.</returns>
        public static bool SignCheckpoint()
        {
            if (!CanSignCheckpointNow)
            {
                return false;
            }

            DutiesNoteSigned();   // 54차: 근무일지 [근무 지시](W5) — 체크포인트가 「서명함」을 담도록 먼저
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
            ParadoxAfterRestore();

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

            _lastSummary = BuildSummary(NightOutcome.Absent);
            CloseNight();

            RestartResult result = new RestartResult(RestartKind.Absent, RestartsTonight, -1, captured);
            EventBus.RaiseNightRestarted(result);
            EventBus.RaiseDayEnded(_lastSummary);
            return result;
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
            if (!_nightOpen)
            {
                return;
            }

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
            if (!_nightOpen || IsCaptured)
            {
                return false;
            }

            _axes.SoftCap = Deltas.SoftCap;
            try
            {
                FinalEndNight();
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
            List<FinalRuleResult> results = new List<FinalRuleResult>(FinalResults);
            List<DutyLogEntry> dutyLog = BuildDutyLog();

            return new DaySummary(
                Day,
                Board.ReportedCount,   // 결과창 「점검」 칸 = 오늘 점검표에서 보고한 항목 수 / 전체
                Board.Total,
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

        /// <summary>
        /// 근무일지 줄 — 그날 편성 덱 순서(공간 수칙 → 경비실 → 공통)대로 새 수칙 한 줄씩(최종 기획서 「근무일지 정산 화면」).
        /// 한 번이라도 어겼으면 「어김」, 방아쇠가 왔고 어기지 않았으면 「준수」, 방아쇠가 오지 않았으면 표시 없음.
        /// 미방문 공간에 빨간 줄을 긋던 옛 규칙은 2026-10-03에 없앴다(최종 기획서에 없음). 「지시를 따름」은 그날 그 수칙을 겨눈 역설 문자를 받았는지(<see cref="ParadoxSentFor"/>), 안전한 읽기를 마쳤으면 「확인함 → …」 메모가 붙는다. 「불가피」는 회피 불가 역설이 들어오면 채운다.
        /// </summary>
        private static List<DutyLogEntry> BuildDutyLog()
        {
            List<DutyLogEntry> log = new List<DutyLogEntry>();
            if (_program == null)
            {
                return log;
            }

            IReadOnlyList<RuleDef> deck = _program.Deck;
            for (int i = 0; i < deck.Count; i++)
            {
                RuleDef def = deck[i];
                FinalJudge judge = _finalBook != null ? _finalBook.Judge(def.Id) : null;
                RuleVerdict verdict = judge == null || !judge.Triggered ? RuleVerdict.NotTriggered
                    : judge.Violated ? RuleVerdict.Violated : RuleVerdict.Complied;
                bool instructed = ParadoxSentFor(def.Id);
                string note = instructed && _paradox.SafeRead && _reveals.Count > 0 ? "확인함 → " + _reveals[_reveals.Count - 1].Label : string.Empty;
                log.Add(new DutyLogEntry(i + 1, def.Id, def.Space, def.Text, verdict, instructed, note, UnavoidableBrokeFor(def.Id)));
            }

            return log;
        }

        /// <summary>
        /// 포획됐으면 그날 밤을 닫는다. <see cref="EventBus.AxisCritical"/> 구독자는 닫히기 전에 호출되므로
        /// 그 안에서 <see cref="BuildSummary"/>를 불러도 결과가 온전하다.
        /// </summary>
        private static void CloseIfCaptured()
        {
            if (!_nightOpen || !IsCaptured)
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
            SpaceId space = CurrentSpace;
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

            SpaceId space = CurrentSpace;
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

        /// <summary>감각 축을 올린 출처를 이번 시도 기록에 더한다(재시작 카드용). 디버그·처벌은 이름이 아니므로 뺀다.</summary>
        private static void OnAxisRaised(FearAxis axis, int amount, string sourceId)
        {
            int i = (int)axis;
            if (i < 0 || i >= RaisedThisAttempt.Length) return;

            string name = DisplaySource(sourceId);
            if (string.IsNullOrEmpty(name) || name == "debug" || name == "처벌") return;
            if (!RaisedThisAttempt[i].Contains(name)) RaisedThisAttempt[i].Add(name);
            if (amount > 0 && _tension != null) _tension.Pacer.Impulse(PacerImpulse.Violation);
        }

        /// <summary>
        /// 출처 ID를 재시작 카드에 쓸 이름으로 줄인다: 「T4:T-1[정상]」 → 「T4」, 「H-2[이상]」 → 「H-2」, 「H-1(가까이)」 → 「H-1」.
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
            if (_finale.Active) _finale.End();
            DirectionAbort("밤 닫힘");
            _nightOpen = false;
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
            _nightOpen = false;
            _currentSpace = SpaceId.None;
            _clockMinutes = null;
            Day = 0;
            RestartsTonight = 0;
            _nightStart = null;
            _checkpoint = null;
            _lastCaptureSources = new List<string>();
            ClearRaised();
            ResetExtensions(true);
            ResetOrders();
            ResetDuties();
            InspectionDripEnabled = false;
            ViolationMinutesToday.Clear();
            Board.Begin(InspectionPlan.Empty(0));
            _assigner = null;
            _lastSummary = default;
            JudgingWindowEnabled = false;
            InspectionsEnabled = false;
            InspectionPlanOverride = null;
        }
    }
}

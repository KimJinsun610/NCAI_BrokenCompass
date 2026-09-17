using System.Collections.Generic;

namespace NightDuty
{
    /// <summary>하룻밤이 끝난 방식.</summary>
    public enum NightOutcome
    {
        /// <summary>근무 종료 요청이 수락되어 정상적으로 끝났다.</summary>
        Completed = 0,

        /// <summary>청각·조도·배치 중 하나가 100에 도달해 포획(게임오버)으로 끝났다. 신뢰 100은 포획이 아니다.</summary>
        Captured = 1
    }

    /// <summary>
    /// 하루 근무가 끝났을 때 정산 화면에 넘기는 결과 묶음.
    /// <para>
    /// 이 화면은 게임 전체에서 <b>숫자가 플레이어에게 노출되는 유일한 곳</b>이다.
    /// 근무 중 HUD에는 잔여 시간·체크리스트·문서 단축키·손전등 상태 네 가지만 표시되며,
    /// 공포 축 게이지나 위반 알림은 어떤 형태로도 띄우지 않는다.
    /// 정산 화면의 위반 기록조차 「지침 미준수 · 02:41」처럼 시각만 남기고 규칙 ID는 표시하지 않는다.
    /// </para>
    /// </summary>
    public readonly struct DaySummary
    {
        /// <summary>정산 대상 일차(1부터 시작).</summary>
        public readonly int Day;

        /// <summary>그날 일반 점검을 완료한 점검 ID 수(복도·1-1·1-3·과학실·화장실 중).</summary>
        public readonly int PatrolDone;

        /// <summary>전체 점검 ID 수(5).</summary>
        public readonly int PatrolTotal;

        /// <summary>그날 위반으로 정산된 카드 수. 화면에는 발생 시각만 보여 주고 어떤 수칙이었는지는 밝히지 않는다.</summary>
        public readonly int Violations;

        /// <summary>
        /// <b>폐기 예정.</b> 구버전 §0 조항의 모순(충돌) 처리 수. 확정 기획서에서 §0·전화가 폐기되어
        /// <see cref="NightRun"/>은 항상 0을 넣는다. 역설 문자 결과로 대체할지는 기획 결정 대기.
        /// </summary>
        public readonly int ConflictsHandled;

        /// <summary><b>폐기 예정.</b> <see cref="ConflictsHandled"/> 참조. 항상 0.</summary>
        public readonly int ConflictsTotal;

        /// <summary>청각 축 최종값(0~100).</summary>
        public readonly int Auditory;

        /// <summary>조도 축 최종값(0~100).</summary>
        public readonly int Illuminance;

        /// <summary>배치 축 최종값(0~100).</summary>
        public readonly int Layout;

        /// <summary>신뢰 축 최종값(0~100). 수칙 준수로만 오르며 월드에는 그려지지 않는다.</summary>
        public readonly int Trust;

        /// <summary>
        /// <b>폐기 예정.</b> 구버전의 각인축. 확정 기획서에서 프로파일링·각인축이 폐기되어
        /// <see cref="NightRun"/>은 항상 null을 넣는다. 기존 결과창 코드 호환을 위해서만 남겨 둔다.
        /// </summary>
        public readonly FearAxis? ImprintAxis;

        private static readonly int[] NoMinutes = new int[0];
        private static readonly RuleResult[] NoResults = new RuleResult[0];
        private static readonly DutyLogEntry[] NoLog = new DutyLogEntry[0];
        private readonly IReadOnlyList<DutyLogEntry> _dutyLog;

        private readonly IReadOnlyList<int> _violationMinutes;
        private readonly IReadOnlyList<RuleResult> _results;

        /// <summary>하룻밤이 끝난 방식. 기존 11인자 생성자로 만들면 <see cref="NightOutcome.Completed"/>.</summary>
        public readonly NightOutcome Outcome;

        /// <summary>포획으로 끝났을 때의 최초 종료 원인. <see cref="Outcome"/>이 Captured일 때만 의미가 있다.
        /// 화면에는 축 정도만 쓰고 카드·문자 ID는 표시하지 않는다.</summary>
        public readonly TerminationCause Cause;

        /// <summary>위반이 정산된 게임 시각(0:00 기준 분) 목록. 결과창의 위반 로그는 이 값만 표시한다.</summary>
        public IReadOnlyList<int> ViolationMinutes
        {
            get { return _violationMinutes ?? NoMinutes; }
        }

        /// <summary>카드별 정산 결과(미판정 포함). 개발 로그·근무 종료 리뷰용이며 화면 표시용이 아니다.</summary>
        public IReadOnlyList<RuleResult> Results
        {
            get { return _results ?? NoResults; }
        }

        /// <summary>
        /// 근무 일지 줄(그날 덱 순서). 결과창의 「금일 근무 지침」 재료다. 기존 생성자로 만들면 비어 있다.
        /// </summary>
        public IReadOnlyList<DutyLogEntry> DutyLog
        {
            get { return _dutyLog ?? NoLog; }
        }

        /// <summary>하루치 정산 결과를 구성한다.</summary>
        public DaySummary(
            int day,
            int patrolDone,
            int patrolTotal,
            int violations,
            int conflictsHandled,
            int conflictsTotal,
            int auditory,
            int illuminance,
            int layout,
            int trust,
            FearAxis? imprintAxis)
        {
            Day = day;
            PatrolDone = patrolDone;
            PatrolTotal = patrolTotal;
            Violations = violations;
            ConflictsHandled = conflictsHandled;
            ConflictsTotal = conflictsTotal;
            Auditory = auditory;
            Illuminance = illuminance;
            Layout = layout;
            Trust = trust;
            ImprintAxis = imprintAxis;
            Outcome = NightOutcome.Completed;
            Cause = default;
            _violationMinutes = null;
            _results = null;
            _dutyLog = null;
        }

        /// <summary>
        /// 확정 기획서 기준의 하룻밤 결과를 구성한다. 폐기 예정 필드(충돌·각인축)는 0/null로 채운다.
        /// </summary>
        public DaySummary(
            int day,
            int patrolDone,
            int patrolTotal,
            int auditory,
            int illuminance,
            int layout,
            int trust,
            NightOutcome outcome,
            TerminationCause cause,
            IReadOnlyList<int> violationMinutes,
            IReadOnlyList<RuleResult> results)
            : this(day, patrolDone, patrolTotal, auditory, illuminance, layout, trust, outcome, cause, violationMinutes, results, null)
        {
        }

        /// <summary>
        /// 하룻밤 결과에 근무 일지 줄까지 담는다. <see cref="NightRun.BuildSummary"/>가 쓴다.
        /// </summary>
        public DaySummary(
            int day,
            int patrolDone,
            int patrolTotal,
            int auditory,
            int illuminance,
            int layout,
            int trust,
            NightOutcome outcome,
            TerminationCause cause,
            IReadOnlyList<int> violationMinutes,
            IReadOnlyList<RuleResult> results,
            IReadOnlyList<DutyLogEntry> dutyLog)
        {
            Day = day;
            PatrolDone = patrolDone;
            PatrolTotal = patrolTotal;
            Violations = violationMinutes != null ? violationMinutes.Count : 0;
            ConflictsHandled = 0;
            ConflictsTotal = 0;
            Auditory = auditory;
            Illuminance = illuminance;
            Layout = layout;
            Trust = trust;
            ImprintAxis = null;
            Outcome = outcome;
            Cause = cause;
            _violationMinutes = violationMinutes;
            _results = results;
            _dutyLog = dutyLog;
        }

        /// <summary>게임 시각(분)을 「0:41」 형식으로 바꾼다. 24시 이후는 0시부터 다시 센다.</summary>
        public static string FormatMinutes(int minutes)
        {
            if (minutes < 0)
            {
                return "--:--";
            }

            int m = minutes % (24 * 60);
            return (m / 60) + ":" + (m % 60).ToString("00");
        }

        /// <summary>
        /// 축 종류로 최종값을 조회한다. UI가 네 축을 순회하며 그릴 수 있도록 제공한다.
        /// </summary>
        /// <param name="axis">조회할 공포 축.</param>
        /// <returns>해당 축의 0~100 최종값. 알 수 없는 값이면 0.</returns>
        public int AxisValue(FearAxis axis)
        {
            switch (axis)
            {
                case FearAxis.Auditory: return Auditory;
                case FearAxis.Illuminance: return Illuminance;
                case FearAxis.Layout: return Layout;
                case FearAxis.Trust: return Trust;
                default: return 0;
            }
        }
    }
}

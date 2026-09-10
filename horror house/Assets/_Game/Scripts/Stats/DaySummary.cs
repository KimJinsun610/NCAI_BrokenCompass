namespace NightDuty
{
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

        /// <summary>실제로 방문한 순찰 지점 수.</summary>
        public readonly int PatrolDone;

        /// <summary>그날 요구된 전체 순찰 지점 수(현재 스코프 기준 5).</summary>
        public readonly int PatrolTotal;

        /// <summary>기록된 지침 미준수 횟수. 발생 시각만 함께 보여 주고 어떤 수칙이었는지는 밝히지 않는다.</summary>
        public readonly int Violations;

        /// <summary>플레이어가 실제로 판단을 내린 모순(충돌) 상황 수.</summary>
        public readonly int ConflictsHandled;

        /// <summary>그날 지침록에 심어진 전체 모순 수. 신뢰 축 구간이 높을수록 늘어난다.</summary>
        public readonly int ConflictsTotal;

        /// <summary>청각 축 최종값(0~100).</summary>
        public readonly int Auditory;

        /// <summary>조도 축 최종값(0~100).</summary>
        public readonly int Illuminance;

        /// <summary>배치 축 최종값(0~100).</summary>
        public readonly int Layout;

        /// <summary>신뢰 축 최종값(0~100). 월드가 아니라 지침록의 모순·서체로만 드러난 값이다.</summary>
        public readonly int Trust;

        /// <summary>
        /// 각인축. Day 3부터 플레이어가 가장 크게 반응한 축이 각인축으로 결정된다.
        /// <para>
        /// 정산 화면은 <b>해당 축 이름만 다른 서체로</b> 표시하고 그 밖의 설명은 일절 붙이지 않는다.
        /// 「당신은 소리에 민감합니다」 같은 문구를 넣는 순간 프로파일링이 임의적으로 읽히기 때문이다.
        /// Day 3 이전에는 판정 근거가 부족하므로 null이다.
        /// </para>
        /// </summary>
        public readonly FearAxis? ImprintAxis;

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

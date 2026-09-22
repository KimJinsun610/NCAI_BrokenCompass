namespace NightDuty
{
    /// <summary>
    /// 근무 일차가 넘어갈 때 감각 3축에 깔아 주는 <b>바닥값</b>(2026-09-21 밸런스 재설계).
    /// <list type="bullet">
    /// <item>축은 여전히 <b>위반으로 오른다.</b> 하한은 그 위에 얹는 별개의 보정이며, <b>하루에 한 번, 밤이 시작될 때만</b> 적용한다.
    /// 단 이것은 <b>호출자가 지켜야 하는 규약</b>이지 코드가 막아 주는 것이 아니다 —
    /// <see cref="Apply"/>는 몇 번을 부르든 그대로 실행된다(이미 하한에 닿았으면 델타가 0이라 결과가 같을 뿐이다).</item>
    /// <item>이미 하한보다 높은 축은 <b>건드리지 않는다.</b> 하한은 올리기만 하지 내리지 않는다.</item>
    /// <item><b>신뢰는 대상이 아니다.</b> 신뢰는 준수로만 오른다.</item>
    /// </list>
    /// <para>
    /// <b>왜 필요한가.</b> 이 장치가 없으면 수칙을 잘 지키는 플레이어는 5일 내내 축이 0이라
    /// 조명이 6500K 백색에서 한 번도 변하지 않고, 이상현상 표 60칸 중 Band0의 12칸만 보고 회차가 끝난다.
    /// 즉 <b>잘할수록 아무 일도 일어나지 않는 공포게임</b>이 된다. 공포의 보상이 무(無)인 것이다.
    /// 하한은 그 플레이어에게도 연출을 열어 주고, 동시에 마지막 밤에는 위반 세 번이면 죽는 거리에 세운다
    /// (5일차 하한 72 + 12×2 = 96으로 두 번은 아직 모자라고, 세 번이면 108로 넘는다).
    /// </para>
    /// <para>
    /// <b>왜 12의 배수인가.</b> 기본 위반 델타가 +12라서, 하한을 12 배수에 두면
    /// 「하루치를 뒤지고 들어간 상태」가 정확히 위반 한 번 분량으로 읽힌다.
    /// 5일차의 72는 <see cref="Band.Band3"/>의 바로 밑바닥이라, 완벽하게 지킨 플레이어도
    /// 마지막 밤에는 <b>세 번 어기면 100을 넘는</b> 자리에 선다(72 + 36 = 108).
    /// </para>
    /// <para>
    /// <b>Band4(90~99)는 하한으로 주지 않는다.</b> 그 구간은 「당신이 어겨서 여기까지 왔다」는
    /// 뜻으로 남겨 둔다. 공짜로 도달하는 순간 경고로서의 의미가 사라진다.
    /// </para>
    /// <para>
    /// 플레이어에게는 아무것도 표시되지 않는다. 체감상으로는 「날이 갈수록 학교가 나빠진다」이다.
    /// </para>
    /// </summary>
    public static class DayFloor
    {
        /// <summary>
        /// 일차별 바닥값. 인덱스 0은 쓰지 않는다(일차는 1부터).
        /// 곡선을 바꾸려면 <b>오직 이 배열만</b> 고치면 된다 — if 사슬로 흩뿌리지 말 것.
        /// </summary>
        private static readonly int[] Floors = { 0, 0, 12, 24, 48, 72 };

        /// <summary>하한이 정의된 마지막 일차. 그 뒤의 일차는 이 값의 하한을 그대로 쓴다.</summary>
        public static int LastDay { get { return Floors.Length - 1; } }

        /// <summary>
        /// 그 일차의 바닥값. 1 미만은 0, <see cref="LastDay"/>를 넘으면 마지막 값으로 클램프한다.
        /// </summary>
        /// <param name="day">근무 일차(1부터).</param>
        public static int Of(int day)
        {
            if (day < 1)
            {
                return 0;
            }

            if (day > LastDay)
            {
                return Floors[LastDay];
            }

            return Floors[day];
        }

        /// <summary>
        /// 감각 3축을 그 일차의 바닥값까지 끌어올린다. 이미 높은 축은 그대로 둔다.
        /// <para>
        /// <b>반드시 덱을 짜기 전에 부른다.</b> 카드의 발동 자격(<see cref="RuleSO.IsEligible"/>)이
        /// 이 값을 보고 정해지므로, 순서가 뒤집히면 하한이 그날 카드 풀에 반영되지 않는다.
        /// </para>
        /// <para>같은 날 두 번 불러도 안전하다 — 이미 하한에 닿아 있으면 델타가 0이라 아무 일도 없다.</para>
        /// <para>
        /// <b>이미 포획된 회차(<see cref="FearAxisSystem.IsLocked"/>)에서는 아무것도 하지 않고 0을 돌려준다.</b>
        /// 종료가 확정된 뒤에 하한이 축을 더 올려 봐야 기록만 어지럽힌다.
        /// </para>
        /// </summary>
        /// <param name="axes">회차 축. null이면 아무것도 하지 않는다.</param>
        /// <param name="day">근무 일차(1부터).</param>
        /// <returns>실제로 올린 축의 수(0~3). 로그·테스트용.</returns>
        public static int Apply(FearAxisSystem axes, int day)
        {
            if (axes == null || axes.IsLocked)
            {
                return 0;
            }

            int floor = Of(day);
            if (floor <= 0)
            {
                return 0;
            }

            int raised = 0;
            for (int i = 0; i < FearAxisSystem.AxisCount; i++)
            {
                FearAxis axis = (FearAxis)i;
                if (!FearAxisSystem.IsTerminal(axis))
                {
                    // 신뢰는 하한 대상이 아니다. 신뢰는 준수로만 오른다.
                    continue;
                }

                int delta = floor - axes.GetValue(axis);
                if (delta <= 0)
                {
                    continue;
                }

                // 출처를 "일차하한"으로 남긴다. 하한은 100에 닿지 않지만(최대 72),
                // 혹시 곡선을 올리더라도 종료 원인이 무엇이었는지 읽을 수 있어야 한다.
                if (axes.Apply(axis, delta, "일차하한", SpaceId.None))
                {
                    raised++;
                }
            }

            return raised;
        }
    }
}

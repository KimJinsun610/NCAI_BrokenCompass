namespace NightDuty
{
    /// <summary>
    /// 근무 일차별 <b>연출 구간의 바닥</b>(2026-09-30 새 기획서).
    /// <list type="bullet">
    /// <item>일차 하한은 <b>생존 수치를 올리지 않는다.</b> 생존 수치는 위반·실패로만 오르고, 붙잡힘(100)에만 쓴다.</item>
    /// <item>하한은 <b>연출 구간</b>에만 걸린다. 연출 구간 = max(도달 구간, 일차 하한)이고 한 번 오르면 내려가지 않는다
    /// (<see cref="BandResolver"/>).</item>
    /// <item><b>신뢰는 대상이 아니다.</b> 신뢰는 준수로만 오른다.</item>
    /// </list>
    /// <para>
    /// <b>왜 필요한가.</b> 수칙을 잘 지키는 플레이어도 날이 갈수록 학교가 나빠지는 것을 봐야 한다.
    /// 잘할수록 아무 일도 일어나지 않는 공포게임이 되지 않게, 연출은 일차가 밀어 올린다.
    /// 반면 죽음(붙잡힘)은 플레이어가 한 일로만 다가온다 — 그래서 생존 수치는 건드리지 않는다.
    /// </para>
    /// <para>
    /// <b>Band4(90–99)는 하한으로 주지 않는다.</b> 그 구간은 「당신이 어겨서 여기까지 왔다」는 뜻으로 남겨 둔다.
    /// </para>
    /// </summary>
    public static class DayFloor
    {
        /// <summary>
        /// 일차별 연출 구간 하한. 인덱스 0은 쓰지 않는다(일차는 1부터).
        /// 곡선을 바꾸려면 <b>오직 이 배열만</b> 고치면 된다.
        /// </summary>
        private static readonly Band[] Floors = { Band.Band0, Band.Band0, Band.Band1, Band.Band1, Band.Band2, Band.Band3 };

        /// <summary>하한이 정의된 마지막 일차. 그 뒤의 일차는 이 값의 하한을 그대로 쓴다.</summary>
        public static int LastDay { get { return Floors.Length - 1; } }

        /// <summary>
        /// 그 일차의 연출 구간 하한. 1 미만은 Band0, <see cref="LastDay"/>를 넘으면 마지막 값으로 클램프한다.
        /// </summary>
        /// <param name="day">근무 일차(1부터).</param>
        public static Band Of(int day)
        {
            if (day < 1)
            {
                return Band.Band0;
            }

            if (day > LastDay)
            {
                return Floors[LastDay];
            }

            return Floors[day];
        }
    }
}

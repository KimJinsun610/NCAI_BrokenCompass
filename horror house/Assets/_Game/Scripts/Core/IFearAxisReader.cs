namespace NightDuty
{
    /// <summary>
    /// 공포 축의 현재 값과 구간을 <b>읽기 전용</b>으로 노출하는 창구.
    /// <para>
    /// 구현: 생존 수치(<c>FearAxisSystem</c>)와 연출 구간 창구(<c>BandResolver.Shown</c>). 읽는 쪽은 구체 타입 대신 이 인터페이스에 의존한다.
    /// (옛 에디터 슬라이더 DebugAxisDriver는 2026-10-03 폐기 — 축을 손으로 밀려면 F3 디버그 콘솔을 쓴다.)
    /// </para>
    /// <para>
    /// 값을 바꾸는 수단은 여기에 없다. 축을 올리고 내리는 것은 판정/스탯 쪽의 책임이고,
    /// 그 결과는 <see cref="EventBus"/>의 이벤트로만 건너온다.
    /// </para>
    /// </summary>
    public interface IFearAxisReader
    {
        /// <summary>축의 현재 값(0~100)을 돌려준다.</summary>
        /// <param name="axis">읽을 공포 축.</param>
        int GetValue(FearAxis axis);

        /// <summary>
        /// 축의 현재 구간을 돌려준다.
        /// <c>FearAxisSystem</c>은 생존 수치의 원시 구간을, <c>BandResolver.Shown</c>은 연출 구간
        /// (내려가지 않음, 일차 하한 적용)을 돌려준다. 연출·자격 판정은 후자를 읽는다.
        /// </summary>
        /// <param name="axis">읽을 공포 축.</param>
        Band GetBand(FearAxis axis);
    }
}

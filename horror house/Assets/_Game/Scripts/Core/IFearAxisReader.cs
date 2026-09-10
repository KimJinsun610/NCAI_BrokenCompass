namespace NightDuty
{
    /// <summary>
    /// 공포 축의 현재 값과 구간을 <b>읽기 전용</b>으로 노출하는 창구.
    /// <para>
    /// 존재 이유: 실제 판정 시스템인 <c>FearAxisSystem</c>과, 에디터 전용 슬라이더인
    /// <c>DebugAxisDriver</c>가 <b>둘 다 이 인터페이스를 구현한다</b>.
    /// 덕분에 클라이언트/연출 쪽(플레이어·UI·SpacePresenter)은 판정 시스템이 완성되기 전에도
    /// <c>DebugAxisDriver</c>를 물려 놓고 작업을 진행할 수 있다.
    /// 연출 코드는 항상 이 인터페이스에만 의존해야 하며, 구체 타입을 직접 참조하면 그 이점이 사라진다.
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
        /// 구현체는 <see cref="Bands.Of"/>의 원시 판정이 아니라
        /// 히스테리시스가 적용된 확정 구간을 돌려주는 것이 원칙이다.
        /// </summary>
        /// <param name="axis">읽을 공포 축.</param>
        Band GetBand(FearAxis axis);
    }
}

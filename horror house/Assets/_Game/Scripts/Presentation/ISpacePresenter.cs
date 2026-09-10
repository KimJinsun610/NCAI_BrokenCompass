namespace NightDuty
{
    /// <summary>
    /// 한 공간(복도·화장실·교실·과학실)의 연출을 담당하는 프리젠터.
    /// <para>
    /// 구현체는 클라이언트 측(Programmer_Kim)에 있으며 공간마다 하나씩 존재한다.
    /// 코어는 이 인터페이스를 <b>직접 호출하지 않는다</b> — 코어는 축이 변할 때 <see cref="EventBus"/>로
    /// 이벤트를 던질 뿐이고, 각 프리젠터가 스스로 구독한 뒤 자신의 <see cref="Space"/>와 일치하는
    /// 이벤트만 걸러서 처리한다. 이 방향을 뒤집으면 NightDuty.Core가 연출을 참조하게 되어
    /// 단방향 의존 구조가 깨진다.
    /// </para>
    /// <para>
    /// <see cref="FearAxis.Trust"/>는 원칙적으로 여기서 처리하지 않는다. 신뢰 축은 월드를 건드리지 않고
    /// 지침록으로만 드러나므로 <see cref="IDocumentView"/>가 담당한다.
    /// </para>
    /// </summary>
    public interface ISpacePresenter
    {
        /// <summary>이 프리젠터가 담당하는 공간. 이벤트 필터링의 기준값이다.</summary>
        SpaceId Space { get; }

        /// <summary>
        /// 축의 구간이 바뀌었을 때 호출된다. 형광등 개수처럼 <b>불연속적인</b> 연출은 여기서 즉시 스냅시킨다.
        /// (조도 기준: Band0 8등·6500K → Band1 6등·4500K → Band2 4등·3200K → Band3 2등·2000K → Band4 소등·붉은 잔광.
        /// 등 개수는 복도·교실 기준이며 화장실·과학실은 그 절반이다.)
        /// </summary>
        /// <param name="axis">변화한 공포 축.</param>
        /// <param name="from">이전 구간.</param>
        /// <param name="to">새 구간.</param>
        void OnBandChanged(FearAxis axis, Band from, Band to);

        /// <summary>
        /// 같은 구간 안에서 축 값이 움직일 때 호출된다. 색온도 보간처럼 <b>연속적인</b> 연출에 쓴다.
        /// </summary>
        /// <param name="axis">변화한 공포 축.</param>
        /// <param name="t01">현재 구간 내 진행도(0~1).</param>
        void OnBandProgress(FearAxis axis, float t01);

        /// <summary>
        /// 새 일차 시작 시 호출된다. 공간의 연출 상태(조명·소품 배치·소리)를 그날의 초기값으로 되돌린다.
        /// </summary>
        void ResetForNewDay();
    }
}

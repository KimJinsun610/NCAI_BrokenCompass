using UnityEngine;

/// <summary>
/// 공포 연출 하나가 요청하는 화면 울렁임의 세기.
/// <para>
/// <b>Timeline의 Animation 트랙이 <see cref="weight"/>를 키프레임한다.</b> 이 컴포넌트는 값만 들고 있고,
/// 실제로 화면에 적용하는 것은 <see cref="ScreenWobble"/>이다.
/// </para>
/// <para>
/// 연출마다 하나씩 둔다. 여러 연출이 동시에 재생되면 <see cref="ScreenWobble"/>이 그중 가장 큰 값을 쓴다 —
/// Timeline 둘이 같은 필드를 애니메이션하면 서로 덮어써 값이 튀기 때문이다.
/// </para>
/// <para>
/// 인스펙터의 weight는 <b>0으로 둔다.</b> Director가 멈추면(<c>Stop</c>) Timeline이 이 기본값으로 되돌린다.
/// </para>
/// </summary>
public class ScreenWobbleSource : MonoBehaviour
{
    [Tooltip("울렁임 세기. 0 = 없음, 1 = 최대. Timeline Animation 트랙으로 키프레임한다.\n곡선의 끝은 반드시 0으로 내린다 — Wrap Mode가 Hold면 마지막 값이 계속 남는다.")]
    [Range(0f, 1f)] public float weight;

    private void OnEnable()
    {
        ScreenWobble.Register(this);
    }

    private void OnDisable()
    {
        ScreenWobble.Unregister(this);
    }
}

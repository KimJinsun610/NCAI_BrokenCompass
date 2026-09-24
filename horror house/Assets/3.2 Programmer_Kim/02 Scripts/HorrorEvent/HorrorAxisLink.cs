using NightDuty;
using UnityEngine;

/// <summary>
/// 감각축이 지정한 구간에 올라서면 연출을 재생한다. <b>축 연출</b> 갈래의 연결부다.
/// <para>
/// 판정 시스템(NightDuty)은 <b>읽기만</b> 한다 — <see cref="EventBus.BandChanged"/>를 구독할 뿐 아무것도 보내지 않는다.
/// 같은 축 · 구간에 연결된 연출이 여럿이면 한꺼번에 재생된다.
/// </para>
/// <para>
/// <b>신뢰 축은 쓰지 않는다.</b> 신뢰는 월드에 그리지 않는 축이다(CLAUDE.md §2.2).
/// </para>
/// </summary>
public class HorrorAxisLink : MonoBehaviour
{
    [Tooltip("재생할 연출. 비워 두면 같은 오브젝트에서 찾는다.")]
    [SerializeField] private HorrorEvent target;

    [Tooltip("지켜볼 축 (청각 · 조도 · 배치)")]
    [SerializeField] private FearAxis axis = FearAxis.Layout;

    [Tooltip("이 구간 이상으로 올라서는 순간 재생한다.")]
    [SerializeField] private Band minBand = Band.Band1;

    [Tooltip("이 공간의 변화만 본다. None이면 모든 공간.")]
    [SerializeField] private SpaceId space = SpaceId.None;

    private void Awake()
    {
        if (target == null) target = GetComponent<HorrorEvent>();
        if (axis == FearAxis.Trust) Debug.LogWarning($"[HorrorAxisLink] {name}: 신뢰 축은 월드 연출에 쓰지 않습니다.", this);
    }

    private void OnEnable()
    {
        EventBus.BandChanged += OnBandChanged;
    }

    private void OnDisable()
    {
        // EventBus는 static이라 씬이 바뀌어도 살아 있다. 반드시 푼다.
        EventBus.BandChanged -= OnBandChanged;
    }

    private void OnBandChanged(SpaceId changedSpace, FearAxis changedAxis, Band from, Band to)
    {
        if (target == null || changedAxis != axis) return;
        if (space != SpaceId.None && changedSpace != space) return;

        // 문턱을 넘어선 순간만 본다. 같은 구간 재방송(from == to)이나 이미 넘은 뒤의 변화는 무시한다.
        if (from >= minBand || to < minBand) return;

        target.Play();
    }
}

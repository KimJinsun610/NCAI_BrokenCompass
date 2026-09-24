using UnityEngine;

/// <summary>
/// 플레이어가 이 구역에 들어오면 연출을 재생한다. Is Trigger 콜라이더와 같은 오브젝트에 붙인다.
/// <para>
/// 연출 프리팹의 자식 「Trigger」에 두고, 씬에서는 이 오브젝트의 위치 · 크기만 조절한다.
/// </para>
/// </summary>
[RequireComponent(typeof(Collider))]
public class HorrorTriggerZone : MonoBehaviour
{
    [Tooltip("재생할 연출. 비워 두면 부모에서 찾는다.")]
    [SerializeField] private HorrorEvent target;

    private void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void Awake()
    {
        if (target == null) target = GetComponentInParent<HorrorEvent>();
        if (target == null) Debug.LogWarning($"[HorrorTriggerZone] {name}: 재생할 HorrorEvent가 없습니다.", this);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (target == null) return;
        if (other.GetComponentInParent<FPController>() == null) return;

        target.Play();
    }
}

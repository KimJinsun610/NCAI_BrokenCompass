using UnityEngine;

/// <summary>
/// 플레이어가 지정한 지점을 일정 시간 바라보면 연출을 재생한다.
/// <para>
/// 「바라본다」 = 화면 가운데에서 <see cref="maxAngle"/> 안쪽에 있고, <see cref="maxDistance"/> 이내이며,
/// 카메라에서 그 지점까지 가린 것이 없는 상태가 <see cref="seconds"/> 동안 이어지는 것이다.
/// 시선을 떼면 시간은 처음부터 다시 센다.
/// </para>
/// <para>
/// 판정 시스템의 응시(GazeProbe)와는 별개다. 연출 전용이며 판정 신호를 보내지 않는다.
/// </para>
/// </summary>
public class HorrorGazeTrigger : MonoBehaviour
{
    [Tooltip("재생할 연출. 비워 두면 같은 오브젝트에서 찾는다.")]
    [SerializeField] private HorrorEvent target;

    [Tooltip("바라봐야 하는 지점. 문이라면 문짝 가운데.")]
    [SerializeField] private Transform lookPoint;

    [Tooltip("이 오브젝트(와 자식)에 시선이 막히는 것은 가린 것으로 치지 않는다. 비워 두면 Look Point의 부모.\n문짝 자체의 콜라이더가 시선을 받기 때문에 필요하다.")]
    [SerializeField] private Transform lookObject;

    [Tooltip("화면 가운데에서 이 각도(도) 안에 들어와야 바라본 것으로 친다.")]
    [SerializeField, Range(1f, 45f)] private float maxAngle = 12f;

    [Tooltip("이 거리(m)보다 멀면 바라봐도 발동하지 않는다.")]
    [SerializeField, Min(0.5f)] private float maxDistance = 12f;

    [Tooltip("이 시간(초) 동안 계속 바라봐야 발동한다.")]
    [SerializeField, Min(0f)] private float seconds = 0.5f;

    private float gazeTime;

    private void Awake()
    {
        if (target == null) target = GetComponent<HorrorEvent>();
        if (lookObject == null && lookPoint != null) lookObject = lookPoint.parent;
        if (target == null || lookPoint == null) Debug.LogWarning($"[HorrorGazeTrigger] {name}: Target 또는 Look Point가 비어 있습니다.", this);
    }

    private void Update()
    {
        if (target == null || lookPoint == null) return;
        if (target.HasPlayed || target.IsPlaying) return;

        // 일시정지 중에는 deltaTime이 0이라 저절로 멈춘다.
        if (IsLooking()) gazeTime += Time.deltaTime;
        else gazeTime = 0f;

        if (gazeTime >= seconds)
        {
            gazeTime = 0f;
            target.Play();
        }
    }

    private bool IsLooking()
    {
        Camera cam = Camera.main;
        if (cam == null) return false;

        Vector3 from = cam.transform.position;
        Vector3 to = lookPoint.position;
        Vector3 dir = to - from;

        if (dir.magnitude > maxDistance) return false;
        if (Vector3.Angle(cam.transform.forward, dir) > maxAngle) return false;

        if (Physics.Linecast(from, to, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore))
        {
            return lookObject != null && hit.collider.transform.IsChildOf(lookObject);
        }

        return true;
    }

    private void OnDrawGizmosSelected()
    {
        if (lookPoint == null) return;
        Gizmos.color = new Color(1f, 0.6f, 0.1f);
        Gizmos.DrawWireSphere(lookPoint.position, 0.15f);
    }
}

using UnityEngine;

/// <summary>
/// 「플레이어가 그 지점을 바라보고 있는가」 판정. 연출 전용이며 판정 시스템의 응시(GazeProbe)와는 별개다.
/// <para>
/// 바라본다 = 화면 가운데에서 maxAngle 안쪽이고, maxDistance 이내이며, 카메라에서 그 지점까지 가린 것이 없는 상태.
/// <paramref name="lookObject"/>(와 자식)에 시선이 막히는 것은 가린 것으로 치지 않는다 — 문짝처럼 대상 자신이 콜라이더를 가질 때 필요하다.
/// </para>
/// <para><see cref="HorrorGazeTrigger"/>(한 번 발동)와 <see cref="HorrorGazeHold"/>(보는 동안 유지)가 함께 쓴다.</para>
/// </summary>
public static class HorrorGaze
{
    public static bool IsLooking(Transform lookPoint, Transform lookObject, float maxAngle, float maxDistance)
    {
        if (lookPoint == null) return false;

        Camera cam = Camera.main;
        if (cam == null) return false;

        Vector3 from = cam.transform.position;
        Vector3 dir = lookPoint.position - from;

        if (dir.magnitude > maxDistance) return false;
        if (Vector3.Angle(cam.transform.forward, dir) > maxAngle) return false;

        if (Physics.Linecast(from, lookPoint.position, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore))
        {
            return lookObject != null && hit.collider.transform.IsChildOf(lookObject);
        }

        return true;
    }
}

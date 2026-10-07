using System;
using UnityEngine;

/// <summary>
/// 60차(민: 「도서관 조우는 플레이어가 몹을 마주쳤을 때 효과음이 들리게」) — 대역이 플레이어 시야에 들어오는 순간 한 번 부른다.
/// 시선에서 <see cref="ViewDegrees"/>° 안이고 화면 안이며 눈과 몸 사이를 가리는 것이 없을 때(대역 바로 앞 1m의 유리·창틀은 무시 — 창밖 남자).
/// 대역이 거둬지면 함께 사라진다. 판정과 무관.
/// </summary>
[DisallowMultipleComponent]
public sealed class SeenStinger : MonoBehaviour
{
    /// <summary>「마주쳤다」로 보는 시선 각(°).</summary>
    public const float ViewDegrees = 22f;

    /// <summary>대역 앞 이 거리 안의 가림(유리·창틀·문틀)은 무시한다(m).</summary>
    public const float IgnoreNearTarget = 1f;

    private Action _onSeen;
    private Transform _aim;
    private bool _done;

    /// <summary>불렀는지. 시험용.</summary>
    public bool Fired
    {
        get { return _done; }
    }

    /// <summary>대역에 붙인다. 이미 있으면 다시 건다.</summary>
    public static SeenStinger Attach(GameObject mob, Action onSeen)
    {
        if (mob == null) return null;
        SeenStinger s = mob.GetComponent<SeenStinger>();
        if (s == null) s = mob.AddComponent<SeenStinger>();
        s._onSeen = onSeen;
        s._done = false;
        s.enabled = true;
        foreach (Transform t in mob.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == "Aim")
            {
                s._aim = t;
                break;
            }
        }

        return s;
    }

    private Vector3 AimPoint()
    {
        if (_aim != null) return _aim.position;
        Renderer[] rs = GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return transform.position + Vector3.up * 1.4f;
        Bounds b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        return b.center + Vector3.up * b.extents.y * 0.5f;
    }

    private void Update()
    {
        if (_done) return;
        Camera cam = Camera.main;
        if (cam == null) return;
        Vector3 eye = cam.transform.position;
        Vector3 at = AimPoint();
        if (Vector3.Angle(cam.transform.forward, at - eye) > ViewDegrees) return;
        Vector3 vp = cam.WorldToViewportPoint(at);
        if (vp.z <= 0f || vp.x < 0f || vp.x > 1f || vp.y < 0f || vp.y > 1f) return;
        if (Blocked(eye, at, cam.transform.root)) return;

        _done = true;
        Action a = _onSeen;
        _onSeen = null;
        if (a != null) a();
        enabled = false;
    }

    private bool Blocked(Vector3 eye, Vector3 at, Transform player)
    {
        Vector3 d = at - eye;
        float len = d.magnitude;
        if (len < 0.01f) return false;
        foreach (RaycastHit h in Physics.RaycastAll(eye, d / len, len, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (h.collider.transform.IsChildOf(transform)) continue;
            if (player != null && h.collider.transform.IsChildOf(player)) continue;
            if (len - h.distance <= IgnoreNearTarget) continue;
            return true;
        }

        return false;
    }
}

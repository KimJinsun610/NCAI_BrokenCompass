using System.Collections.Generic;
using NightDuty;
using UnityEngine;

/// <summary>
/// 62차(민: 「소년, 시체의 디스폰은 플레이어 시야에서 완전히 벗어나면 사라지게」): 조우가 끝나도 대역을 바로 지우지 않고,
/// 화면 어디에도 보이지 않게 된 뒤(<see cref="AwaySeconds"/>초) 지운다.
/// <list type="bullet">
/// <item><b>보인다</b> = 렌더러 상자 중 하나라도 카메라 시야 사각뿔 안에 들고, 그 상자의 가운데·위·아래 중 하나까지 가리는 것이 없을 것(대역 자신·플레이어 몸 제외).
/// 화면 가장자리에 조금만 걸려도 보이는 것으로 본다.</item>
/// <item>넘겨받는 순간 판정 기준점(<see cref="JudgeTarget"/>)은 끈다 — 끝난 조우의 대역을 바라봐도 판정이 움직이지 않게.</item>
/// <item>오래(<see cref="MaxSeconds"/>초) 보고 있어도 결국 지운다. 재시작·하루 끝(중단)은 쓰지 않고 곧바로 지운다.</item>
/// </list>
/// </summary>
[DisallowMultipleComponent]
public sealed class UnseenDespawn : MonoBehaviour
{
    /// <summary>안 보인 채 이만큼(초)이면 지운다.</summary>
    public const float AwaySeconds = 0.3f;

    /// <summary>이만큼(초) 지나면 보고 있어도 지운다.</summary>
    public const float MaxSeconds = 120f;

    private static readonly Plane[] s_planes = new Plane[6];

    private bool _armed;
    private float _since;
    private float _awaySince = -1f;
    private Renderer[] _renderers;

    /// <summary>지울 차례를 기다리는 중인지(검수용).</summary>
    public bool Armed
    {
        get { return _armed; }
    }

    /// <summary>표시만 해 둔다 — 정리(<c>DirectionStage.Cleanup</c>)가 이 표시를 보고 바로 지우지 않고 <see cref="Begin"/>한다.</summary>
    public static void Mark(GameObject go)
    {
        if (go != null && go.GetComponent<UnseenDespawn>() == null) go.AddComponent<UnseenDespawn>();
    }

    /// <summary>안 보이게 되면 지운다. 표시가 없던 대역도 받는다.</summary>
    public static void Begin(GameObject go)
    {
        if (go == null) return;
        UnseenDespawn d = go.GetComponent<UnseenDespawn>();
        if (d == null) d = go.AddComponent<UnseenDespawn>();
        d.Arm();
    }

    private void Arm()
    {
        if (_armed) return;
        _armed = true;
        _since = Time.time;
        _renderers = GetComponentsInChildren<Renderer>(true);
        foreach (JudgeTarget t in GetComponentsInChildren<JudgeTarget>(true)) t.enabled = false;
    }

    private void Update()
    {
        if (!_armed) return;
        if (Time.time - _since >= MaxSeconds)
        {
            Destroy(gameObject);
            return;
        }

        Camera cam = Camera.main;
        if (cam != null && Visible(cam))
        {
            _awaySince = -1f;
            return;
        }

        if (_awaySince < 0f) _awaySince = Time.time;
        if (Time.time - _awaySince < AwaySeconds) return;
        if (DirectionStage.Verbose) Debug.Log("[Direction] 시야에서 벗어나 지움 — " + name);
        Destroy(gameObject);
    }

    /// <summary>64차: 아무 오브젝트가 화면 어디에든 보이는지(가림 포함) — 과학실 모형이 「보지 않을 때」 다가올 때 쓴다.</summary>
    public static bool VisibleTo(GameObject go, Camera cam)
    {
        if (go == null || cam == null) return false;
        return Seen(go.transform, go.GetComponentsInChildren<Renderer>(true), cam);
    }

    /// <summary>화면 어디에든 보이는지(가림 포함).</summary>
    public bool Visible(Camera cam)
    {
        if (_renderers == null) _renderers = GetComponentsInChildren<Renderer>(true);
        return Seen(transform, _renderers, cam);
    }

    private static bool Seen(Transform root, Renderer[] renderers, Camera cam)
    {
        Renderer[] _renderers = renderers;
        GeometryUtility.CalculateFrustumPlanes(cam, s_planes);
        Vector3 eye = cam.transform.position;
        Transform player = PlayerSensors.Active != null ? PlayerSensors.Active.PlayerRoot : null;
        for (int i = 0; i < _renderers.Length; i++)
        {
            Renderer r = _renderers[i];
            if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
            if (r is ParticleSystemRenderer) continue;
            Bounds b = r.bounds;
            if (!GeometryUtility.TestPlanesAABB(s_planes, b)) continue;
            if (Clear(root, eye, b.center, player) || Clear(root, eye, b.center + Vector3.up * b.extents.y * 0.8f, player) || Clear(root, eye, b.center - Vector3.up * b.extents.y * 0.8f, player)) return true;
        }

        return false;
    }

    private static bool Clear(Transform transform, Vector3 eye, Vector3 p, Transform player)
    {
        Vector3 d = p - eye;
        float dist = d.magnitude;
        if (dist < 0.01f) return true;
        foreach (RaycastHit h in Physics.RaycastAll(eye, d / dist, dist, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            Transform t = h.collider.transform;
            if (t == transform || t.IsChildOf(transform)) continue;
            if (player != null && t.IsChildOf(player)) continue;
            return false;
        }

        return true;
    }
}

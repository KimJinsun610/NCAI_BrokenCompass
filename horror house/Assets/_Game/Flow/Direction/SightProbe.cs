using System;
using NightDuty;
using UnityEngine;

/// <summary>
/// 61차(민: 「몹은 나타나 있되, 플레이어가 몹을 시야에 넣고 인지한 뒤에 연출이 시작되도록 — 플레이어가 놓치는 경우가 많다」).
/// 세워 둔 대역(<see cref="DirectionPhase.Present"/>)에 붙어, 플레이어가 그것을 <b>알아볼 만큼</b> 봤는지 잰다.
/// <list type="bullet">
/// <item>몸통 가운데·머리 중 하나가 화면 안쪽(가장자리 <see cref="ScreenMargin"/>은 뺀다)에 있고, <see cref="MaxDistance"/>m 안이고,
/// 카메라에서 그 점까지 가리는 것이 없을 것(대역 자신·플레이어 몸·대역 앞 1m의 유리·창틀은 무시).</item>
/// <item>그 상태가 <see cref="RecognizeSeconds"/>초 이어지면 한 번 알린다(<see cref="NightRun.EncounterSeen"/>) — 대면은 그다음 틱.</item>
/// </list>
/// <b>판정과 무관하다</b>(응시 판정은 판정 기준점이 따로 본다).
/// </summary>
[DisallowMultipleComponent]
public sealed class SightProbe : MonoBehaviour
{
    /// <summary>알아봤다고 칠 연속 시간(초).</summary>
    public const float RecognizeSeconds = 0.35f;

    /// <summary>화면 가장자리에서 빼는 몫(0~0.5). 곁눈으로 스친 것은 세지 않는다.</summary>
    public const float ScreenMargin = 0.12f;

    /// <summary>이보다 멀면 세지 않는다(m).</summary>
    public const float MaxDistance = 30f;

    private string _encounterId;
    private Action _onSeen;
    private Func<bool> _gate;   // 70차: 참일 때만 본 것으로 센다(모형 급습 = 플레이어가 복도에 있을 때)
    private float _seenFor;
    private bool _done;
    private Renderer[] _renderers;
    private bool _lookMode;
    private Vector3 _lookPoint;
    private float _lookAngle;

    /// <summary>지금까지 이어서 본 시간(초, 검수용).</summary>
    public float SeenFor
    {
        get { return _seenFor; }
    }

    /// <summary>알렸는지(검수용).</summary>
    public bool Reported
    {
        get { return _done; }
    }

    /// <summary>대역에 붙인다. 알아보면 <paramref name="onSeen"/>을 부른다.</summary>
    public static SightProbe Attach(GameObject mob, string encounterId, Action onSeen, Func<bool> gate = null)
    {
        SightProbe p = mob.GetComponent<SightProbe>();
        if (p == null) p = mob.AddComponent<SightProbe>();
        p._encounterId = encounterId;
        p._onSeen = onSeen;
        p._gate = gate;
        p._seenFor = 0f;
        p._done = false;
        p._renderers = mob.GetComponentsInChildren<Renderer>(true);
        p._lookMode = false;
        return p;
    }

    /// <summary>
    /// 92차(민: 「노란 얼굴의 조우가 진행될 때, 플레이어가 도서관 문 쪽으로 시야를 돌릴 때 조우 연출이 진행되도록」): 몹이 보이는지 대신
    /// 카메라가 <paramref name="point"/> 쪽(화면 가운데에서 <paramref name="angle"/>° 안)을 <see cref="RecognizeSeconds"/>초 보면 알아본 것으로 친다 — 문짝에 반쯤 가려도.
    /// </summary>
    public static SightProbe AttachLookToward(GameObject mob, string encounterId, Action onSeen, Vector3 point, float angle, Func<bool> gate = null)
    {
        SightProbe p = Attach(mob, encounterId, onSeen, gate);
        p._lookMode = true;
        p._lookPoint = point;
        p._lookAngle = angle;
        return p;
    }

    private void Update()
    {
        if (_done) return;
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        if ((_gate == null || _gate()) && Visible())
        {
            _seenFor += dt;
            if (_seenFor >= RecognizeSeconds)
            {
                _done = true;
                if (DirectionStage.Verbose) Debug.Log("[Direction] 몹을 알아봄 — " + _encounterId);
                Action a = _onSeen;
                if (a != null) a();
            }
        }
        else
        {
            _seenFor = Mathf.Max(0f, _seenFor - dt * 2f);
        }
    }

    private bool Visible()
    {
        // 「점 바라보기」는 이 대역 하나에만 걸린 설정이므로 인스턴스 쪽에서 본다
        // (static VisibleNow 안에 두면 인스턴스 필드를 읽을 수 없다 — 2026-10-11 머지 때 섞여 들어간 자리).
        if (_lookMode)
        {
            Camera cam = Camera.main;
            if (cam == null || !cam.isActiveAndEnabled) return false;
            CctvSystem cctv = CctvSystem.Active;
            if (cctv != null && cctv.IsViewing) return false;
            Vector3 to = _lookPoint - cam.transform.position;
            return to.sqrMagnitude > 0.01f && to.magnitude <= MaxDistance
                   && Vector3.Angle(cam.transform.forward, to) <= _lookAngle;
        }

        return VisibleNow(transform, _renderers);
    }

    /// <summary>
    /// 그 몹이 지금 화면에 보이는지 — 몸통 가운데·머리 중 하나가 화면 안쪽(가장자리 <see cref="ScreenMargin"/> 제외)·<see cref="MaxDistance"/>m 안·가림 없음.
    /// CCTV를 들여다보는 중이면 false. 2026-10-10: 피날레 창밖 남자 「봤다」 판정도 이것을 쓴다(<c>FinaleDirector</c>).
    /// </summary>
    public static bool VisibleNow(Transform root, Renderer[] renderers)
    {
        Camera cam = Camera.main;
        if (root == null || cam == null || !cam.isActiveAndEnabled) return false;
        CctvSystem cctv = CctvSystem.Active;
        if (cctv != null && cctv.IsViewing) return false;

        Bounds b;
        if (!TryBounds(renderers, out b)) return false;
        Vector3 chest = b.center;
        Vector3 head = b.center + Vector3.up * (b.extents.y * 0.65f);
        return PointVisible(cam, head, root) || PointVisible(cam, chest, root);
    }

    private static bool TryBounds(Renderer[] renderers, out Bounds b)
    {
        b = default(Bounds);
        bool has = false;
        if (renderers == null) return false;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
            if (r is ParticleSystemRenderer || r is LineRenderer || r is TrailRenderer) continue;
            if (!has)
            {
                b = r.bounds;
                has = true;
            }
            else
            {
                b.Encapsulate(r.bounds);
            }
        }

        return has;
    }

    private static bool PointVisible(Camera cam, Vector3 p, Transform root)
    {
        Vector3 v = cam.WorldToViewportPoint(p);
        if (v.z <= 0.1f || v.z > MaxDistance) return false;
        if (v.x < ScreenMargin || v.x > 1f - ScreenMargin || v.y < ScreenMargin || v.y > 1f - ScreenMargin) return false;

        Vector3 from = cam.transform.position;
        Vector3 dir = p - from;
        float dist = dir.magnitude;
        if (dist < 0.01f) return true;
        dir /= dist;
        Transform player = PlayerSensors.Active != null ? PlayerSensors.Active.PlayerRoot : null;
        foreach (RaycastHit h in Physics.RaycastAll(from, dir, dist, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            Transform t = h.collider.transform;
            if (t == root || t.IsChildOf(root)) continue;
            if (player != null && t.IsChildOf(player)) continue;
            if (dist - h.distance < 1f && IsGlass(t)) continue;   // 창밖 남자 — 대역 앞 유리·창틀
            return false;
        }

        return true;
    }

    private static bool IsGlass(Transform t)
    {
        string n = t.name;
        return n.IndexOf("Window", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("Glass", StringComparison.OrdinalIgnoreCase) >= 0;
    }
}

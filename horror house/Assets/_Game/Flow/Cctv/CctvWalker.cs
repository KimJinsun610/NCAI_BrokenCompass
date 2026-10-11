using System;
using NightDuty;
using UnityEngine;

/// <summary>
/// CCTV 화면 속 사람이 실제로 걷는다(71차, 민: 「순간이동하듯 위치가 바뀌는 방식 대신 걷는 애니메이션을 재생하면서 실제로 이동 · CCTV 화면에서도 자연스럽게」).
/// <list type="bullet">
/// <item>대역 <see cref="PrefabId"/>(검은 남자 — 걷기 클립 「Walk」가 있는 판)의 걷기를 틀고, 걷는 속도에 맞춰 재생 속도를 바꾸며(발이 미끄러지지 않게),
/// 걷는 쪽을 보고 매 프레임 <see cref="WalkSpeed"/>로 나아간다. 옛 「1.5초마다 0.7m 툭툭(스톱모션)」은 폐기.</item>
/// <item>K1 조우(<c>DirectionStage</c>)는 한 번 가로지르고 끝에서 사라진다. K-1 이상(<c>InspectionAnomalies</c>)은 끝에 닿으면 잠시 사라졌다가 반대로 걷는다.</item>
/// <item>CCTV 얼굴 점프스케어(<see cref="CctvFaceScare"/>) 동안은 멈춘다(<see cref="Paused"/>).</item>
/// <item>자리는 <see cref="CctvSpots"/>(코어 표)에서 오고, <see cref="TryResolve"/>가 실제 바닥 높이·화면 안·가림 없음을 다시 확인한다.</item>
/// </list>
/// </summary>
[DisallowMultipleComponent]
public sealed class CctvWalker : MonoBehaviour
{
    /// <summary>걷기 동작이 있는 검은 남자 대역(59차 「멀리 보이는 몹」과 같은 판 — 퇴장 달리기 컴포넌트는 떼어 낸다).</summary>
    public const string PrefabId = "mob.blackman.glimpse";

    /// <summary>걷는 속도(m/s) — 느릿하게, 그러나 멈추지 않고.</summary>
    public const float WalkSpeed = 0.85f;

    /// <summary>걷기 클립의 원래 걸음 속도를 모를 때(m/s).</summary>
    public const float FallbackNaturalSpeed = 1.2f;

    private Animator _anim;
    private CctvOnlyVisible _only;
    private float _natural = FallbackNaturalSpeed;
    private bool _hasWalk;
    private Vector3 _a;
    private Vector3 _b;
    private bool _forward = true;
    private bool _pingPong;
    private float _rest;
    private float _hiddenFor;
    private bool _walking;
    private Action _arrived;

    /// <summary>얼굴 점프스케어 동안 멈춘다(제자리, 동작도 멈춤).</summary>
    public bool Paused { get; set; }

    /// <summary>걷는 중인지(끝에 닿아 멈췄거나 잠시 사라진 동안은 거짓).</summary>
    public bool Walking
    {
        get { return _walking && _hiddenFor <= 0f; }
    }

    /// <summary>CCTV에만 보이게 하는 컴포넌트.</summary>
    public CctvOnlyVisible Only
    {
        get { return _only; }
    }

    /// <summary>
    /// CCTV에만 보이는 걷는 사람을 <paramref name="from"/>에 세운다(그 채널에서만 보임, 콜라이더 끔). 걷기는 <see cref="Walk"/>로.
    /// </summary>
    public static CctvWalker Create(Vector3 from, Vector3 to, int channel, string name)
    {
        GameObject go = StandInFactory.Create(PrefabId, from, to, string.Empty);
        if (go == null) return null;
        go.name = name;
        foreach (Collider c in go.GetComponentsInChildren<Collider>(true)) c.enabled = false;
        CctvWalker w = go.AddComponent<CctvWalker>();
        w.Setup(channel);
        return w;
    }

    private void Setup(int channel)
    {
        StandInExit exit = GetComponent<StandInExit>();
        if (exit != null)
        {
            if (exit.NaturalSpeed > 0.05f) _natural = exit.NaturalSpeed;
            Destroy(exit);   // 결과 단계에 기괴하게 달려 나가지 않게(CCTV 사람은 제 길 끝에서 사라진다)
        }

        _anim = GetComponentInChildren<Animator>();
        if (_anim != null)
        {
            _anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;   // 렌더러는 CCTV가 그릴 때만 켜진다 — 그 밖에도 걸음이 이어져야 한다
            _anim.applyRootMotion = false;
            _hasWalk = _anim.runtimeAnimatorController != null && _anim.HasState(0, Animator.StringToHash(StandInExit.WalkState));
        }

        _only = gameObject.AddComponent<CctvOnlyVisible>();
        _only.Channel = channel;
    }

    /// <summary>
    /// 걷기 시작한다. <paramref name="pingPong"/>이면 끝에 닿을 때마다 <paramref name="restSeconds"/>초 사라졌다가 반대로 걷는다(끝없이).
    /// 아니면 끝에 닿으면 멈추고 <paramref name="arrived"/>를 한 번 부른다(부르는 쪽이 감춘다).
    /// </summary>
    public void Walk(Vector3 from, Vector3 to, bool pingPong, float restSeconds, Action arrived)
    {
        _a = from;
        _b = to;
        _forward = true;
        _pingPong = pingPong;
        _rest = Mathf.Max(0f, restSeconds);
        _arrived = arrived;
        _hiddenFor = 0f;
        _walking = true;
        transform.position = from;
        Face(to - from, true);
        PlayWalk(true);
    }

    /// <summary>잠시 화면에서 감춘다(점프스케어 뒤 등). 다시 보일 때 제 길을 이어 걷는다.</summary>
    public void HideFor(float seconds)
    {
        _hiddenFor = Mathf.Max(_hiddenFor, seconds);
        if (_only != null) _only.enabled = false;
    }

    private void Update()
    {
        if (_hiddenFor > 0f)
        {
            _hiddenFor -= Time.deltaTime;
            if (_hiddenFor > 0f) return;
            if (_only != null) _only.enabled = true;
            if (_walking) PlayWalk(true);
        }

        if (!_walking || Paused)
        {
            if (_anim != null && _hasWalk) _anim.speed = 0f;
            return;
        }

        Vector3 target = _forward ? _b : _a;
        Vector3 pos = transform.position;
        Vector3 to = target - pos;
        to.y = 0f;
        float d = to.magnitude;
        float step = WalkSpeed * Time.deltaTime;
        if (d <= step || d < 0.01f)
        {
            transform.position = new Vector3(target.x, pos.y, target.z);
            Arrive();
            return;
        }

        transform.position = pos + to / d * step;
        Face(to, false);
        PlayWalk(false);
    }

    private void Arrive()
    {
        if (_pingPong)
        {
            _forward = !_forward;
            if (_rest > 0f) HideFor(_rest);
            Face((_forward ? _b : _a) - transform.position, true);
            return;
        }

        _walking = false;
        if (_anim != null && _hasWalk) _anim.speed = 0f;
        Action done = _arrived;
        _arrived = null;
        if (done == null) return;
        try
        {
            done();
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
    }

    private void Face(Vector3 dir, bool snap)
    {
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;
        Quaternion want = Quaternion.LookRotation(dir.normalized, Vector3.up);
        transform.rotation = snap ? want : Quaternion.Slerp(transform.rotation, want, 1f - Mathf.Exp(-6f * Time.deltaTime));
    }

    private void PlayWalk(bool restart)
    {
        if (_anim == null || !_hasWalk) return;
        _anim.speed = WalkSpeed / Mathf.Max(0.05f, _natural);
        if (restart && !_anim.GetCurrentAnimatorStateInfo(0).IsName(StandInExit.WalkState)) _anim.Play(StandInExit.WalkState, 0, UnityEngine.Random.value);
    }

    // ── 자리 확인 ─────────────────────────────────────────────

    /// <summary>
    /// 코어 표의 자리를 실제 바닥 두 점으로 바꾼다 — 바닥 높이를 맞추고, 길 위 세 점(처음·가운데·끝)에서 몸이 그 채널 카메라 화면 안에 있고 가려지지 않는지 본다.
    /// 둘 넘게 안 보이면 false(부르는 쪽이 그 채널 화면의 바닥으로 물러난다).
    /// </summary>
    public static bool TryResolve(Camera cam, CctvSpot spot, out Vector3 from, out Vector3 to)
    {
        from = spot != null ? spot.From : Vector3.zero;
        to = spot != null ? spot.To : Vector3.zero;
        if (cam == null || spot == null) return false;
        float y;
        if (!FloorY(spot.From, out y) && !FloorY(spot.To, out y)) return false;
        from.y = y;
        to.y = y;
        int seen = 0;
        for (int i = 0; i <= 2; i++) if (Visible(cam, Vector3.Lerp(from, to, i * 0.5f))) seen++;
        return seen >= 2;
    }

    /// <summary>발이 <paramref name="foot"/>에 선 사람의 허리·머리가 그 카메라 화면 안이고 가려지지 않았는지.</summary>
    public static bool Visible(Camera cam, Vector3 foot)
    {
        int ok = 0;
        foreach (float h in new[] { 1.0f, 1.6f })
        {
            Vector3 p = foot + Vector3.up * h;
            Vector3 v = cam.WorldToViewportPoint(p);
            if (v.z <= 0f || v.x < 0.03f || v.x > 0.97f || v.y < 0.03f || v.y > 0.97f) continue;
            Vector3 d = p - cam.transform.position;
            if (Physics.Raycast(cam.transform.position, d.normalized, d.magnitude - 0.3f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
            ok++;
        }

        return ok > 0;
    }

    /// <summary>그 점 바로 아래 바닥 높이(표의 높이 ±0.6m 안의 위를 향한 면).</summary>
    private static bool FloorY(Vector3 p, out float y)
    {
        y = p.y;
        RaycastHit[] hits = Physics.RaycastAll(p + Vector3.up * 0.6f, Vector3.down, 1.6f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        bool found = false;
        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i].normal.y < 0.7f) continue;
            float off = Mathf.Abs(hits[i].point.y - p.y);
            if (off > 0.6f || off >= best) continue;
            best = off;
            y = hits[i].point.y;
            found = true;
        }

        return found;
    }
}

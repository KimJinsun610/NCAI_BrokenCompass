using UnityEngine;

/// <summary>
/// 조우가 끝날 때 몹이 그 자리에서 사라지지 않고 <b>빠르게 걸어 나가며</b> 사라진다(58차, 민: 「businessduck — 문 조우·창문 조우에서 사라질 때 빠르게 걸어가면서」).
/// 빌더가 프리팹(<c>mob.duck</c>·<c>mob.windowman</c>)에 붙인다 — 걷기 동작은 옛 businessduck_walking을 새 오리 리그로 옮긴 제자리 걸음(애니메이터 「Walk」 상태).
/// <see cref="DirectionStage"/>가 결과(Result) 단계에서 <see cref="Leave"/>를 부르면, 옆(좌·우 중 1.8m 넘게 트인 먼 쪽 — 창 앞을 가로질러 지나가게), 없으면 뒤로 몸을 돌려 걷다가
/// 트인 거리 끝(벽 0.4m 앞)이나 <see cref="MaxSeconds"/>에서 사라지고, 그 전에 화면 밖이 되면 바로 지운다. 걸음은 기괴하게 빠르고(<see cref="Speed"/>) 박자가 툭툭 튄다.
/// 판정과 무관 — 걷는 동안 콜라이더(응시 상자)는 끈다.
/// <para>59차: 멀리 보이는 몹(<see cref="DistantGlimpse"/>)도 같은 걸음으로 <see cref="RunTo"/> — 플레이어 눈에서 가려지는 사각지대까지 달려가 가려지는 순간 사라진다.</para>
/// </summary>
[DisallowMultipleComponent]
public sealed class StandInExit : MonoBehaviour
{
    /// <summary>걷는 속도(m/s) — 58차 민: 「기괴하게 빠르면 좋겠어」 1.9 → 4.6(걸음 동작은 약 3배속).</summary>
    public const float Speed = 4.6f;

    /// <summary>걷는 최대 시간(초).</summary>
    public const float MaxSeconds = 2f;

    /// <summary>걸음 박자가 툭툭 끊기는 정도 — 애니메이터 속도가 0.15~0.3초마다 이 배수 범위(×)에서 튄다(사람 같지 않게).</summary>
    public const float StutterMin = 0.55f;
    public const float StutterMax = 1.6f;

    /// <summary>애니메이터 걷기 상태 이름.</summary>
    public const string WalkState = "Walk";

    [Tooltip("걷기 클립 원래 속도에서의 걸음 속도(m/s, 프리팹 크기 반영). 빌더가 적는다.")]
    [SerializeField, Min(0f)] private float naturalSpeed;

    private Vector3 _dir;
    private float _t;
    private bool _leaving;
    private float _until;
    private Animator _anim;
    private float _baseAnimSpeed = 1f;
    private float _stutter = 1f;
    private float _nextStutter;
    private System.Func<bool> _hidden;
    private Vector3 _target;
    private bool _toTarget;
    private bool _arrived;

    /// <summary>걸어 나가는 중인지.</summary>
    public bool Leaving
    {
        get { return _leaving; }
    }

    /// <summary>걸어 나간 방향(시험·검수).</summary>
    public Vector3 Direction
    {
        get { return _dir; }
    }

    /// <summary>걷기 클립 원래 속도에서의 걸음 속도(m/s). 71차: CCTV 사람(<c>CctvWalker</c>)이 같은 걷기 클립을 쓸 때 읽는다.</summary>
    public float NaturalSpeed
    {
        get { return naturalSpeed; }
    }

    /// <summary>빌더가 쓴다.</summary>
    public void SetNaturalSpeed(float metersPerSecond)
    {
        naturalSpeed = Mathf.Max(0f, metersPerSecond);
    }

    /// <summary>걸어 나가기 시작한다. 이미 나가는 중이면 그대로. 이 오브젝트는 스스로 지워진다.</summary>
    public void Leave()
    {
        if (_leaving) return;
        float clear;
        Vector3 dir = OpenSide(out clear);
        BeginMove(dir, Mathf.Clamp((clear - 0.4f) / Speed, 0.35f, MaxSeconds));
    }

    /// <summary>
    /// 59차: <paramref name="target"/>(바닥 점)까지 같은 기괴한 걸음으로 달려간다. <paramref name="hidden"/>이 참이 되는 순간(플레이어 눈에서 가려짐) 사라지고,
    /// 닿은 뒤에도 가려지지 않으면 <paramref name="maxSeconds"/>에 사라진다. 이미 나가는 중이면 그대로.
    /// </summary>
    public void RunTo(Vector3 target, float maxSeconds, System.Func<bool> hidden)
    {
        if (_leaving) return;
        Vector3 d = target - transform.position;
        d.y = 0f;
        _target = target;
        _toTarget = true;
        _hidden = hidden;
        BeginMove(Flat(d), Mathf.Max(0.3f, maxSeconds));
    }

    private void BeginMove(Vector3 dir, float until)
    {
        _leaving = true;
        transform.SetParent(null, true);
        _dir = dir;
        _until = until;
        transform.rotation = Quaternion.LookRotation(_dir, Vector3.up);
        foreach (Collider c in GetComponentsInChildren<Collider>(true)) c.enabled = false;

        Animator a = GetComponentInChildren<Animator>();
        if (a != null && a.runtimeAnimatorController != null && a.HasState(0, Animator.StringToHash(WalkState)))
        {
            a.enabled = true;
            a.CrossFade(WalkState, 0.05f, 0, 0f);   // 거의 끊어 바꾼다 — 돌아서자마자 달리듯 걷는다
            _baseAnimSpeed = naturalSpeed > 0.01f ? Speed / naturalSpeed : 1f;
            a.speed = _baseAnimSpeed;
            _anim = a;
        }
    }

    /// <summary>옆으로 걸어 나갈 최소 트인 거리(m).</summary>
    public const float SideMinClear = 1.8f;

    /// <summary>좌·우 중 더 트인 쪽이 <see cref="SideMinClear"/>를 넘으면 그쪽, 아니면 뒤(벽 속으로 걸어 들어가지 않게). <paramref name="clear"/> = 허리 높이로 트인 거리.</summary>
    private Vector3 OpenSide(out float clear)
    {
        Vector3 right = Flat(transform.right);
        float r = Clear(right);
        float l = Clear(-right);
        if (Mathf.Max(r, l) >= SideMinClear)
        {
            clear = Mathf.Max(r, l);
            return r >= l ? right : -right;
        }

        Vector3 back = Flat(-transform.forward);
        clear = Clear(back);
        return back;
    }

    private float Clear(Vector3 d)
    {
        Vector3 waist = transform.position + Vector3.up * 0.9f;
        float dist = 6f;
        foreach (RaycastHit h in Physics.SphereCastAll(waist, 0.25f, d, 6f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (h.collider.transform.IsChildOf(transform) || h.collider.GetComponentInParent<CharacterController>() != null) continue;
            dist = Mathf.Min(dist, h.distance);
        }

        return dist;
    }

    private static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.forward;
    }

    private void Update()
    {
        if (!_leaving) return;
        _t += Time.deltaTime;
        if (_t >= _nextStutter)
        {
            // 박자 튐 — 걸음과 이동이 함께 빨라졌다 느려졌다 한다(미끄러지지 않게 같은 배수).
            _nextStutter = _t + Random.Range(0.15f, 0.3f);
            _stutter = Random.Range(StutterMin, StutterMax);
            if (_anim != null && !_arrived) _anim.speed = _baseAnimSpeed * _stutter;
        }

        Vector3 step = _dir * (Speed * _stutter * Time.deltaTime);
        if (_toTarget)
        {
            Vector3 left = _target - transform.position;
            left.y = 0f;
            if (left.magnitude <= step.magnitude)
            {
                step = left;
                _arrived = true;
                if (_anim != null) _anim.speed = 0f;   // 닿았다 — 그 자리에 굳는다(가려지면 곧 사라진다)
            }
        }

        transform.position += step;

        bool seen = false;
        Camera cam = Camera.main;
        if (cam != null)
        {
            Vector3 vp = cam.WorldToViewportPoint(transform.position + Vector3.up * 1.2f);
            seen = vp.z > 0f && vp.x > -0.1f && vp.x < 1.1f && vp.y > -0.1f && vp.y < 1.1f;
        }

        if (_toTarget)
        {
            if ((_t > 0.15f && (!seen || (_hidden != null && _hidden()))) || _t >= _until) Destroy(gameObject);
            return;
        }

        if ((_t > 0.5f && !seen) || _t >= _until) Destroy(gameObject);
    }
}

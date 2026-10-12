using UnityEngine;

/// <summary>
/// 91차 문간 노란 얼굴(<c>E.YellowFace</c>, 대역 <c>mob.duck</c>) 손질 — 민: 「도망가기 전까진 플레이어 방향으로 계속 바라보다가 도망가게 ·
/// 도망갈 때 복도에서 갑자기 사라지지 않게 끝까지 뛰어갔다가 없어지게」. (맨눈 가시성 — 스스로 빛나게 — 은 민: 「너무 밝아서 이질감」으로 취소, 재질은 원래대로.)
/// <list type="bullet">
/// <item><b>바라봄</b>: 몸은 그대로 두고 <b>고개만</b>(목 <see cref="NeckShare"/> · 머리 나머지) 플레이어(카메라)를 따라 돌린다 — 민: 「전신이 돌아 빌보드 느낌 → 인체 모형처럼 얼굴만」.
/// 좌우 <see cref="MaxYaw"/>° · 위아래 <see cref="MaxPitch"/>°까지, 초당 <see cref="TurnDegreesPerSecond"/>°로 따라간다. 애니메이션(숨쉬기 루프) 위에 덧붙인다.
/// 달아나기(<see cref="StandInExit.Leaving"/>)가 시작되면 고개를 빠르게 풀고 달린다. 놀람 연출이 붙잡고 있는 동안(<see cref="ViolationScare.Holds"/>)은 손대지 않는다.</item>
/// <item><b>문 안으로 몇 걸음</b>(민: 「도서관 안에서 문 조우가 보이도록 도서관 안으로 살짝 걸어오게」): 문간에 선 뒤 <see cref="StepInDelay"/>초 있다가
/// 바라보는 쪽(도서관 안)으로 <see cref="StepInDistance"/>m 천천히 걸어 들어와 멈춘다(걷기 동작 · 앞이 막혔으면 그만큼 덜).</item>
/// <item><b>끝까지 달아남</b>: <see cref="StandInExit.RunFar"/>를 켠다 — 가장 길게 트인 쪽으로 끝까지 달려가 사라진다.</item>
/// </list>
/// 판정과 무관. <see cref="DirectionStage"/>가 세워 둘 때(보기를 기다림)와 대면 때 붙인다.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(200)]   // 애니메이터·ProceduralIdle 뒤에 고개를 덧붙인다
public sealed class DoorDuck : MonoBehaviour
{
    /// <summary>고개가 따라 도는 속도(°/초).</summary>
    public const float TurnDegreesPerSecond = 140f;

    /// <summary>좌우로 돌릴 수 있는 최대 각(°) — 어깨 너머로 돌아볼 만큼.</summary>
    public const float MaxYaw = 85f;

    /// <summary>위아래 최대 각(°).</summary>
    public const float MaxPitch = 30f;

    /// <summary>돌림 중 목이 맡는 몫(나머지는 머리).</summary>
    public const float NeckShare = 0.4f;

    /// <summary>달아날 때 고개를 푸는 속도(°/초).</summary>
    public const float UnwindDegreesPerSecond = 400f;

    /// <summary>문간에서 안으로 걸어 들어오는 거리(m).</summary>
    public const float StepInDistance = 1.6f;

    /// <summary>걸어 들어오는 빠르기(m/s) — 느릿하게.</summary>
    public const float StepInSpeed = 0.6f;

    /// <summary>문간에 선 뒤 걸어 들어오기까지(초).</summary>
    public const float StepInDelay = 0.8f;

    private Animator _anim;
    private Vector3 _stepTo;
    private float _stepWait;
    private bool _stepping;
    private bool _stepPlanned;
    private int _stepTries;

    /// <summary>안으로 걸어 들어오는 중인지(검수용).</summary>
    public bool SteppingIn
    {
        get { return _stepping; }
    }

    private StandInExit _exit;
    private Transform _neck;
    private Transform _head;
    private float _yaw;
    private float _pitch;

    /// <summary>지금 고개를 돌린 각(좌우, 검수용).</summary>
    public float Yaw
    {
        get { return _yaw; }
    }

    /// <summary>붙인다(이미 있으면 그것). 103차: <paramref name="stepIn"/>이 거짓이면 문 안으로 몇 걸음 들어오지 않는다(도서관 노란 얼굴은 <see cref="LibraryDuck"/>가 걷는다) — 고개 돌림만.</summary>
    public static DoorDuck Attach(GameObject go, bool stepIn = true)
    {
        if (go == null) return null;
        DoorDuck d = go.GetComponent<DoorDuck>();
        bool fresh = d == null;
        if (fresh) d = go.AddComponent<DoorDuck>();
        d._exit = go.GetComponent<StandInExit>();
        if (d._exit != null) d._exit.RunFar = true;
        d._neck = Find(go.transform, "Neck");
        d._head = Find(go.transform, "Head");
        d._anim = go.GetComponentInChildren<Animator>();
        if (fresh && stepIn) d.PlanStepIn();   // 세워 둘 때(보기를 기다릴 때) 한 번 — 대면 때 다시 붙여도 걸음은 이어진다
        return d;
    }

    /// <summary>그 아래 렌더러 재질(인스턴스)에 기본 맵을 발광 맵으로 깔아 <paramref name="glow"/>배로 빛나게 한다. 바꾼 재질 수. (복도 인체 나무 <see cref="HumanTreeGrowth"/>가 쓴다.)</summary>
    public static int SelfLight(GameObject go, float glow)
    {
        int n = 0;
        foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
        {
            if (!(r is SkinnedMeshRenderer) && !(r is MeshRenderer)) continue;
            Material[] mats = r.materials;   // 인스턴스
            for (int i = 0; i < mats.Length; i++)
            {
                Material m = mats[i];
                if (m == null || !m.HasProperty("_EmissionColor")) continue;
                Texture map = m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : m.mainTexture;
                Color baseColor = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : Color.white;
                if (m.HasProperty("_EmissionMap")) m.SetTexture("_EmissionMap", map);
                m.SetColor("_EmissionColor", new Color(baseColor.r, baseColor.g, baseColor.b, 1f) * glow);
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                n++;
            }

            r.materials = mats;
        }

        return n;
    }

    private static Transform Find(Transform t, string name)
    {
        if (t.name == name) return t;
        foreach (Transform c in t)
        {
            Transform f = Find(c, name);
            if (f != null) return f;
        }

        return null;
    }

    private void PlanStepIn()
    {
        // 자리는 걸음을 뗄 때 정한다 — 문짝이 열리는 중일 수 있다.
        _stepWait = StepInDelay;
        _stepTries = 0;
        _stepPlanned = false;
        _stepping = true;
    }

    /// <summary>바라보는 쪽으로 막히지 않은 만큼(최대 <see cref="StepInDistance"/>m) 걸어 들어갈 자리.</summary>
    private bool PlanTarget()
    {
        Vector3 dir = new Vector3(transform.forward.x, 0f, transform.forward.z);
        if (dir.sqrMagnitude < 0.01f) return false;
        dir.Normalize();
        float dist = StepInDistance;
        foreach (RaycastHit hit in Physics.SphereCastAll(transform.position + Vector3.up * 1.0f, 0.2f, dir, StepInDistance + 0.4f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (hit.transform.IsChildOf(transform) || hit.collider.GetComponentInParent<Rigidbody>() != null) continue;   // 자기 몸 · 플레이어
            dist = Mathf.Min(dist, hit.distance - 0.3f);
        }

        if (dist < 0.3f) return false;
        _stepTo = transform.position + dir * dist;
        return true;
    }

    private void Update()
    {
        if (!_stepping) return;
        if ((_exit != null && _exit.Leaving) || ViolationScare.Holds(gameObject))
        {
            _stepping = false;
            return;
        }

        if (_stepWait > 0f)
        {
            _stepWait -= Time.deltaTime;
            return;
        }

        if (!_stepPlanned)
        {
            if (!PlanTarget())
            {
                if (++_stepTries >= 6) _stepping = false;   // 끝내 막혀 있으면 문간에 그대로
                else _stepWait = 0.4f;
                return;
            }

            _stepPlanned = true;
            Anim(StandInExit.WalkState, StepInSpeed / Mathf.Max(0.05f, _exit != null ? _exit.NaturalSpeed : 1.2f));
        }

        Vector3 to = _stepTo - transform.position;
        to.y = 0f;
        float step = StepInSpeed * Time.deltaTime;
        if (to.magnitude <= step)
        {
            transform.position = new Vector3(_stepTo.x, transform.position.y, _stepTo.z);
            _stepping = false;
            Anim("Loop", 1f);
            return;
        }

        transform.position += to.normalized * step;
    }

    private void Anim(string state, float speed)
    {
        if (_anim == null || _anim.runtimeAnimatorController == null || !_anim.HasState(0, Animator.StringToHash(state))) return;
        _anim.speed = speed;
        _anim.CrossFadeInFixedTime(state, 0.25f, 0);
    }

    private void LateUpdate()
    {
        if (_head == null) return;
        if (ViolationScare.Holds(gameObject)) return;
        float dt = Time.deltaTime;
        float wantYaw = 0f, wantPitch = 0f;
        float rate = UnwindDegreesPerSecond;
        Camera cam = Camera.main;
        if ((_exit == null || !_exit.Leaving) && cam != null)
        {
            rate = TurnDegreesPerSecond;
            Vector3 to = cam.transform.position - _head.position;
            Vector3 flat = new Vector3(to.x, 0f, to.z);
            Vector3 fwd = new Vector3(transform.forward.x, 0f, transform.forward.z);
            if (flat.sqrMagnitude > 0.01f && fwd.sqrMagnitude > 0.01f)
            {
                wantYaw = Mathf.Clamp(Vector3.SignedAngle(fwd, flat, Vector3.up), -MaxYaw, MaxYaw);
                wantPitch = Mathf.Clamp(-Mathf.Atan2(to.y, flat.magnitude) * Mathf.Rad2Deg, -MaxPitch, MaxPitch);
            }
        }

        _yaw = Mathf.MoveTowards(_yaw, wantYaw, rate * dt);
        _pitch = Mathf.MoveTowards(_pitch, wantPitch, rate * dt);
        if (Mathf.Abs(_yaw) < 0.01f && Mathf.Abs(_pitch) < 0.01f) return;

        // 뼈 축과 무관하게 — 몸 기준 세계 회전(좌우는 위축, 위아래는 몸의 오른쪽 축)을 애니메이션 자세 위에 곱한다.
        Vector3 right = transform.right;
        if (_neck != null) Turn(_neck, NeckShare);
        Turn(_head, _neck != null ? 1f - NeckShare : 1f);

        void Turn(Transform bone, float share)
        {
            Quaternion yaw = Quaternion.AngleAxis(_yaw * share, Vector3.up);
            Quaternion pitch = Quaternion.AngleAxis(_pitch * share, yaw * right);
            bone.rotation = pitch * yaw * bone.rotation;
        }
    }
}

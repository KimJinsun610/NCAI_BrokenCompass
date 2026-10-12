using NightDuty;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 103차 도서관 노란 얼굴(<c>E.YellowFace</c>, 대역 <c>mob.duck</c>)의 움직임 — 민: 「몹이 등장하고 문이 열릴 때 문이 열리는 사운드 ·
/// 플레이어 쪽으로 천천히 걸어오게, 네비메쉬로 장애물 잘 피하도록 · 손전등으로 계속 비추면 문 밖으로 동선을 찾아서 도망가고 ·
/// 안 비추면 계속 다가와서 놀람 연출 발생시키고 도망가는 걸로 · 플레이어가 요리조리 피하고 도서관 문밖으로 나가려 해도 바로 놀람 연출」.
/// <list type="bullet">
/// <item><b>다가옴</b>: 대면부터 런타임 네비메쉬(<see cref="RuntimeNav"/>) 위를 <see cref="ApproachSpeed"/>m/s로 플레이어 발밑을 따라 걷는다(책장·책상을 돌아서).
/// 비추는 동안에는 멈칫 서 있는다(그래도 다 비추기 전에 빛을 떼면 다시 걷는다).</item>
/// <item><b>비추면 달아남</b>: 손전등 원뿔(<see cref="SensingRules.InBeam(Vector3, Vector3, Vector3, float)"/>) 안에 가림 없이 얼굴이 <see cref="FleeLitSeconds"/>초 이어서 들면(끊김 <see cref="LitGapSeconds"/>초까지 봐줌)
/// 대면 창을 바로 닫고(L3 「계속 비추십시오」 통과) 문 밖 자리(<c>stage.yellow.door</c>)까지 네비메쉬로 뛰어간 뒤, 복도 쪽으로 끝까지 달려가 사라진다(<see cref="StandInExit.RunFar"/>).</item>
/// <item><b>안 비추면 놀람</b>: <see cref="ScareRadius"/>m까지 다가오면 놀람 연출(<see cref="ViolationScare"/>) → 같은 길로 달아남.</item>
/// <item><b>문으로 빠져나가려 하면 놀람</b>: 다가오는 동안 플레이어가 도서관을 벗어나거나 문 밖 자리 <see cref="ExitRadius"/>m 안으로 가면 거리와 상관없이 바로 놀람 → 달아남.</item>
/// </list>
/// 네비메쉬가 준비되지 않았거나(굽는 중) 길이 없으면 그 자리에 서서 기다리고, <see cref="NavWaitSeconds"/>초가 지나도 안 되면 곧장 걸어온다(벽을 뚫지 않게 앞을 살핀다).
/// 판정은 수칙(L3 KeepBeam)이 따로 한다 — 이것은 창을 일찍 닫는 일만 한다(<see cref="NightRun.EndEncounterWindow"/>).
/// </summary>
[DisallowMultipleComponent]
public sealed class LibraryDuck : MonoBehaviour
{
    /// <summary>다가오는 빠르기(m/s) — 민: 「천천히」.</summary>
    public const float ApproachSpeed = 0.55f;

    /// <summary>달아나는 빠르기(m/s).</summary>
    public const float FleeSpeed = 3.6f;

    /// <summary>
    /// 이만큼(초) 이어서 비추면 달아난다. 103차(민: 「손전등을 비춘다고 바로 정지하는 게 아니라 3초 정도는 계속 접근」): 1.5 →
    /// <see cref="LitApproachSeconds"/>(계속 다가옴) + 멈칫 0.8초.
    /// </summary>
    public const float FleeLitSeconds = LitApproachSeconds + 0.8f;

    /// <summary>103차: 비춰도 이만큼(초)은 멈추지 않고 계속 다가온다 — 그 뒤 멈칫 서서 빛 쪽을 본다.</summary>
    public const float LitApproachSeconds = 3f;

    /// <summary>비춤이 이만큼(초) 끊겨도 이어진 것으로 본다(손 떨림).</summary>
    public const float LitGapSeconds = 0.35f;

    /// <summary>이 거리(m, 수평)까지 다가오면 놀람.</summary>
    public const float ScareRadius = 1.5f;

    /// <summary>다가오는 동안 플레이어가 문 밖 자리에서 이 거리(m, 수평) 안이면 빠져나가려는 것으로 보고 바로 놀람.</summary>
    public const float ExitRadius = 2.4f;

    /// <summary>대면 직후 이만큼(초)은 「문으로 빠져나가려 함」을 보지 않는다 — 문 쪽을 돌아본 순간 바로 놀라지 않게.</summary>
    public const float ExitGraceSeconds = 0.8f;

    /// <summary>네비메쉬를 기다리는 최대 시간(초).</summary>
    public const float NavWaitSeconds = 2f;

    /// <summary>목적지를 다시 잡는 간격(초).</summary>
    public const float RepathSeconds = 0.3f;

    private enum State
    {
        Approach,
        Scaring,
        Flee,
        Gone
    }

    private string _id;
    private Transform _doorSpot;
    private Vector3 _doorPoint;
    private State _state;
    private NavMeshAgent _agent;
    private Animator _anim;
    private StandInExit _exit;
    private Transform _head;
    private float _navWait;
    private float _repath;
    private float _lit;
    private float _gap;
    private bool _walkAnim;
    private float _grace;

    /// <summary>지금 비춘 시간(초). 시험·검수용.</summary>
    public float LitSeconds
    {
        get { return _lit; }
    }

    /// <summary>달아나는 중이거나 사라지는 중인지.</summary>
    public bool Fleeing
    {
        get { return _state == State.Flee || _state == State.Gone; }
    }

    /// <summary>네비메쉬 위를 걷고 있는지(검수용).</summary>
    public bool OnNav
    {
        get { return _agent != null && _agent.enabled && _agent.isOnNavMesh; }
    }

    /// <summary>
    /// 붙인다. <paramref name="doorSpot"/> = 문 밖 자리(대역이 처음 선 곳 — 도서관 쪽을 본다). 없으면 붙인 자리.
    /// 대면에서 부른다. 네비메쉬 굽기는 <see cref="PrepareNav"/>(세워 둘 때)가 미리 시작해 둔다.
    /// </summary>
    public static LibraryDuck Begin(GameObject go, string encounterId, Transform doorSpot)
    {
        if (go == null) return null;
        LibraryDuck d = go.GetComponent<LibraryDuck>();
        if (d == null) d = go.AddComponent<LibraryDuck>();
        d._id = encounterId;
        d._doorSpot = doorSpot;
        d._doorPoint = doorSpot != null ? doorSpot.position : go.transform.position;
        d._state = State.Approach;
        d._exit = go.GetComponent<StandInExit>();
        if (d._exit != null) d._exit.RunFar = true;
        d._anim = go.GetComponentInChildren<Animator>();
        d._head = FindBone(go.transform, "Head");
        d._navWait = NavWaitSeconds;
        d._grace = ExitGraceSeconds;
        PrepareNav(go.transform, d._doorPoint);
        return d;
    }

    /// <summary>도서관 상자 + 문 밖 자리 둘레를 덮는 네비메쉬 굽기를 시작한다(비동기 — 세워 둘 때 미리 부른다).</summary>
    public static void PrepareNav(Transform mob, Vector3 doorPoint)
    {
        Bounds area = new Bounds(doorPoint, new Vector3(6f, 4f, 6f));
        SpaceZones zones = FindAnyObjectByType<SpaceZones>();
        Bounds library;
        if (zones != null && zones.TryGetSpaceBox(SpaceId.Library, out library))
        {
            library.Expand(new Vector3(1f, 2f, 1f));
            area.Encapsulate(library);
        }

        RuntimeNav.Prepare(area, mob);
    }

    /// <summary>놀람 뒤 · 창이 끝났을 때(<see cref="DirectionStage"/> 정리) 부른다 — 문 밖으로 달아난다. 이미 달아나는 중이면 그대로.</summary>
    public void Flee()
    {
        if (_state == State.Flee || _state == State.Gone) return;
        _state = State.Flee;
        NightRun.EndEncounterWindow(_id);   // 창을 닫는다 — 비추다 달아나면 L3 통과, 놀람 뒤면 이미 판정 끝
        if (!EnsureAgent())
        {
            LeaveNow();
            return;
        }

        Vector3 to;
        if (!RuntimeNav.Snap(_doorPoint, 2f, out to))
        {
            LeaveNow();
            return;
        }

        _agent.isStopped = false;
        _agent.speed = FleeSpeed;
        _agent.acceleration = 14f;
        _agent.angularSpeed = 720f;
        _agent.stoppingDistance = 0.2f;
        _agent.SetDestination(to);
        Walk(FleeSpeed);
        if (DirectionStage.Verbose) Debug.Log("[LibraryDuck] 문 밖으로 달아남");
    }

    /// <summary>
    /// 디버그·시험: 지금 비춘 것으로 친다(<paramref name="seconds"/>초 더함).
    /// </summary>
    public void DebugLight(float seconds)
    {
        _lit += Mathf.Max(0f, seconds);
    }

    private void Update()
    {
        if (_state == State.Gone) return;
        if (!NightRun.IsNightActive || NightRun.IsCaptured)
        {
            Stop();
            return;
        }

        if (ViolationScare.Holds(gameObject))
        {
            Stop();
            return;
        }

        switch (_state)
        {
            case State.Approach:
                Approach();
                break;
            case State.Scaring:
                if (!ViolationScare.Busy) Flee();   // 놀람이 끝났는데 도망 콜백이 오지 않았다(다른 놀람에 밀림 등)
                break;
            case State.Flee:
                FleeStep();
                break;
        }

        UpdateAnim();
    }

    private void Approach()
    {
        float dt = Time.deltaTime;
        Camera cam = Camera.main;
        if (cam == null) return;

        // ① 비추면 달아난다.
        if (Lit(cam))
        {
            _lit += dt;
            _gap = 0f;
        }
        else
        {
            _gap += dt;
            if (_gap > LitGapSeconds) _lit = 0f;
        }

        if (_lit >= FleeLitSeconds)
        {
            Flee();
            return;
        }

        // ② 문으로 빠져나가려 하면 · 가까이 오면 놀람.
        Vector3 eye = cam.transform.position;
        _grace -= dt;
        bool lit = _lit > 0f;
        bool near = Flat(eye, transform.position) <= ScareRadius;
        bool close = near && !lit;   // 103차: 비추는 동안(L3을 지키는 동안)은 가까이 와도 덮치지 않는다 — 그 자리에 선다
        bool leaving = _grace <= 0f && (SpaceIds.Canonical(NightRun.CurrentSpace) != SpaceId.Library || Flat(eye, _doorPoint) <= ExitRadius);
        if (close || leaving)
        {
            Scare(close ? "가까이 옴" : "문으로 빠져나가려 함");
            return;
        }

        // ③ 다가온다 — 103차: 비춰도 처음 3초는 계속 걸어오고, 그 뒤(또는 비추는 채 코앞이면) 멈칫.
        bool lighting = _lit >= LitApproachSeconds || (lit && near);
        if (!EnsureAgent())
        {
            _navWait -= dt;
            if (_navWait <= 0f && !lighting) StepDirect(eye, dt);
            return;
        }

        _agent.speed = ApproachSpeed;
        _agent.isStopped = lighting;
        _repath -= dt;
        if (_repath <= 0f && !lighting)
        {
            _repath = RepathSeconds;
            Vector3 feet = new Vector3(eye.x, transform.position.y, eye.z);
            Vector3 to;
            if (RuntimeNav.Snap(feet, 2.5f, out to)) _agent.SetDestination(to);
        }

        if (lighting && _head != null)
        {
            // 멈칫 — 빛 쪽을 본다.
            Vector3 look = eye - transform.position;
            look.y = 0f;
            if (look.sqrMagnitude > 0.01f) transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(look), 90f * dt);
        }
    }

    private void Scare(string why)
    {
        if (_state != State.Approach) return;
        Stop();
        GameObject self = gameObject;
        if (ViolationScare.Play(self, "mob.duck", _id, () =>
            {
                LibraryDuck d = self != null ? self.GetComponent<LibraryDuck>() : null;
                if (d != null) d.Flee();
            }))
        {
            _state = State.Scaring;
            if (DirectionStage.Verbose) Debug.Log("[LibraryDuck] 놀람 — " + why);
        }
    }

    private void FleeStep()
    {
        if (_agent == null || !_agent.enabled || !_agent.isOnNavMesh)
        {
            LeaveNow();
            return;
        }

        if (_agent.pathPending) return;
        if (_agent.pathStatus == NavMeshPathStatus.PathInvalid || _agent.remainingDistance <= 0.35f) LeaveNow();
    }

    /// <summary>문 밖 자리에 왔다 — 도서관 쪽을 보고 서서, 복도의 가장 길게 트인 쪽으로 끝까지 달려가 사라진다.</summary>
    private void LeaveNow()
    {
        _state = State.Gone;
        if (_agent != null)
        {
            _agent.enabled = false;
            Destroy(_agent);
            _agent = null;
        }

        if (_doorSpot != null) transform.rotation = Quaternion.LookRotation(Flatten(_doorSpot.forward), Vector3.up);
        if (_exit != null)
        {
            _exit.RunFar = true;
            _exit.Leave();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private bool EnsureAgent()
    {
        if (_agent != null) return _agent.enabled && _agent.isOnNavMesh;
        if (!RuntimeNav.Ready) return false;
        Vector3 on;
        if (!RuntimeNav.Snap(transform.position, 1.5f, out on)) return false;
        transform.position = on;
        _agent = gameObject.AddComponent<NavMeshAgent>();
        // 103차(민: 「벽을 뚫으면서 쫓아와」): 씬의 옛 네비메쉬(요원 종류 0)가 아니라 런타임 굽기 전용 종류만 탄다 — 끄고 종류를 바꾼 뒤 그 메쉬 위로 옮긴다.
        _agent.enabled = false;
        _agent.agentTypeID = RuntimeNav.AgentTypeId;
        _agent.enabled = true;
        _agent.radius = RuntimeNav.AgentRadius;
        _agent.height = RuntimeNav.AgentHeight;
        _agent.baseOffset = 0f;
        _agent.speed = ApproachSpeed;
        _agent.acceleration = 3f;
        _agent.angularSpeed = 240f;
        _agent.stoppingDistance = 0.6f;
        _agent.autoBraking = true;
        _agent.obstacleAvoidanceType = ObstacleAvoidanceType.MedQualityObstacleAvoidance;
        _agent.Warp(on);
        return _agent.isOnNavMesh;
    }

    /// <summary>네비메쉬 없이 곧장 다가온다 — 앞 0.6m가 막혀 있으면 서 있는다.</summary>
    private void StepDirect(Vector3 eye, float dt)
    {
        Vector3 to = eye - transform.position;
        to.y = 0f;
        if (to.sqrMagnitude < 0.01f) return;
        Vector3 dir = to.normalized;
        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir), 180f * dt);
        if (Physics.SphereCast(transform.position + Vector3.up * 1f, 0.25f, dir, out RaycastHit hit, 0.6f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
            && !hit.transform.IsChildOf(transform) && hit.collider.attachedRigidbody == null)
        {
            return;
        }

        transform.position += dir * ApproachSpeed * dt;
        Walk(ApproachSpeed);
    }

    private void Stop()
    {
        if (_agent != null && _agent.enabled && _agent.isOnNavMesh) _agent.isStopped = true;
    }

    private bool Lit(Camera cam)
    {
        FlashlightRelay light = FlashlightRelay.Active;
        if (light == null || !light.IsOn) return false;
        Vector3 face = _head != null ? _head.position : transform.position + Vector3.up * 1.6f;
        Vector3 origin = cam.transform.position;
        if (!SensingRules.InBeam(origin, cam.transform.forward, face, NightRun.BeamRange)) return false;
        RaycastHit hit;
        if (!Physics.Linecast(origin, face, out hit, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return true;
        Transform h = hit.collider.transform;
        if (h.IsChildOf(transform)) return true;
        PlayerSensors hub = PlayerSensors.Active;
        return hub != null && hub.PlayerRoot != null && h.IsChildOf(hub.PlayerRoot);
    }

    private void UpdateAnim()
    {
        if (_state == State.Gone || _agent == null) return;   // 네비메쉬 없이 걸을 때는 StepDirect가 직접
        float speed = _agent != null && _agent.enabled ? _agent.velocity.magnitude : 0f;
        if (_state == State.Approach && speed < 0.05f && _walkAnim)
        {
            Anim("Loop", 1f);
            _walkAnim = false;
        }
        else if (speed >= 0.05f)
        {
            Walk(speed);
        }
    }

    private void Walk(float speed)
    {
        float natural = _exit != null && _exit.NaturalSpeed > 0.05f ? _exit.NaturalSpeed : 1.2f;
        float animSpeed = Mathf.Clamp(speed / natural, 0.3f, 4f);
        if (!_walkAnim)
        {
            Anim(StandInExit.WalkState, animSpeed);
            _walkAnim = true;
        }
        else if (_anim != null)
        {
            _anim.speed = animSpeed;
        }
    }

    private void Anim(string state, float speed)
    {
        if (_anim == null || _anim.runtimeAnimatorController == null || !_anim.HasState(0, Animator.StringToHash(state))) return;
        _anim.speed = speed;
        _anim.CrossFadeInFixedTime(state, 0.25f, 0);
    }

    private static float Flat(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    private static Vector3 Flatten(Vector3 v)
    {
        v.y = 0f;
        return v.sqrMagnitude > 1e-4f ? v.normalized : Vector3.forward;
    }

    private static Transform FindBone(Transform t, string name)
    {
        if (t.name == name) return t;
        foreach (Transform c in t)
        {
            Transform f = FindBone(c, name);
            if (f != null) return f;
        }

        return null;
    }
}

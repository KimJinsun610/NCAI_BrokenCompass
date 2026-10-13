using NightDuty;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 103차 도서관 노란 얼굴(<c>E.YellowFace</c>, 대역 <c>mob.duck</c>)의 움직임 — 민: 「몹이 등장하고 문이 열릴 때 문이 열리는 사운드 ·
/// 플레이어 쪽으로 천천히 걸어오게, 네비메쉬로 장애물 잘 피하도록 · 손전등으로 계속 비추면 문 밖으로 동선을 찾아서 도망가고 ·
/// 안 비추면 계속 다가와서 놀람 연출 발생시키고 도망가는 걸로 · 플레이어가 요리조리 피하고 도서관 문밖으로 나가려 해도 바로 놀람 연출」.
/// <para>104차(사용자: 「걸어오는 속도 더 빠르게, 플레이어가 목격했을 때 걷기 시작. 걸으면서 도서관추격 mp3 재생, 손전등으로 3초 이상 비추면 플레이어 코앞까지 온 뒤에 멈추고
/// 빠르게 문밖으로 나감. 플레이어가 등지고 도망가다가 잡히거나, 손전등으로 3초 이상 비추지 못했거나, 도서관을 나가려 한다면 조도 처형 장면 중 얼굴만 바로 나오는 걸로 처형 후 사망」)</para>
/// <list type="bullet">
/// <item><b>다가옴</b>: 문간을 보는 순간(대면 — <see cref="DirectionStage"/>의 시선 탐지) 런타임 네비메쉬(<see cref="RuntimeNav"/>) 위를 걷기 시작한다 —
/// 보고 있으면 <see cref="ApproachSpeed"/>m/s, 등지면(화면 밖) <see cref="BehindSpeed"/>m/s. 걷는 동안 도서관 추격 소리(<see cref="ChaseSoundKey"/>)를 되풀이한다.</item>
/// <item><b>3초 비추면 물러남</b>: 손전등 원뿔(<see cref="SensingRules.InBeam(Vector3, Vector3, Vector3, float)"/>) 안에 가림 없이 얼굴이 <see cref="SatisfySeconds"/>초 이어서 들면(끊김 <see cref="LitGapSeconds"/>초까지 봐줌)
/// 그대로 코앞(<see cref="FaceStopRadius"/>m)까지 와서 <see cref="FacePauseSeconds"/>초 멈춰 서고, 문 밖 자리(<c>stage.yellow.door</c>)까지 <see cref="FleeSpeed"/>m/s로 뛰어간 뒤 복도로 사라진다(L3 통과).
/// 비추는 동안(아직 3초 전)에는 가까이 와도 잡지 않고 그 자리에 선다.</item>
/// <item><b>사망</b>: 3초를 채우기 전 — 안 비춘 채 <see cref="CatchRadius"/>m까지 오면(등지고 도망가다 잡힘 포함) · 도서관을 벗어나거나 문 밖 자리 <see cref="ExitRadius"/>m 안으로 가면
/// 조도 축으로 붙잡힌다(<see cref="NightRun.CaptureByEncounter"/>, 출처 <see cref="CaptureSource"/>) — 붙잡힘 연출은 조도 사망 컷신의 얼굴 장면부터 튼다(<see cref="CaptureDirector"/>).</item>
/// </list>
/// 네비메쉬가 준비되지 않았거나(굽는 중) 길이 없으면 그 자리에 서서 기다리고, <see cref="NavWaitSeconds"/>초가 지나도 안 되면 곧장 걸어온다(벽을 뚫지 않게 앞을 살핀다).
/// 판정은 수칙(L3 KeepBeam)이 따로 한다 — 이것은 창을 일찍 닫는 일만 한다(<see cref="NightRun.EndEncounterWindow"/>).
/// </summary>
[DisallowMultipleComponent]
public sealed class LibraryDuck : MonoBehaviour
{
    /// <summary>보고 있을 때 다가오는 빠르기(m/s). 104차(사용자: 「더 빠르게」): 0.55 → 1.25(플레이어 걷기 1보다 빠르다).</summary>
    public const float ApproachSpeed = 1.25f;

    /// <summary>104차: 등지면(얼굴이 화면 밖) 더 빨리 따라온다 — 등지고 도망가면 잡힌다.</summary>
    public const float BehindSpeed = 2.1f;

    /// <summary>달아나는 빠르기(m/s). 104차(사용자: 「빠르게 문밖으로」): 3.6 → 4.6.</summary>
    public const float FleeSpeed = 4.6f;

    /// <summary>104차(사용자: 「손전등으로 3초 이상 비추면」): 이만큼(초) 이어서 비추면 물러난다(코앞까지 온 뒤).</summary>
    public const float SatisfySeconds = 3f;

    /// <summary>비춤이 이만큼(초) 끊겨도 이어진 것으로 본다(손 떨림).</summary>
    public const float LitGapSeconds = 0.35f;

    /// <summary>이 거리(m, 수평)까지 안 비춘 채 다가오면 붙잡힌다.</summary>
    public const float CatchRadius = 1.1f;

    /// <summary>3초를 채운 뒤 이 거리(m, 수평)까지 와서 멈춘다(코앞).</summary>
    public const float FaceStopRadius = 0.85f;

    /// <summary>3초를 채운 뒤 코앞까지 오는 데 쓰는 최대 시간(초) — 넘으면 그 자리에서 멈췄다 물러난다.</summary>
    public const float ArriveMaxSeconds = 6f;

    /// <summary>코앞에 멈춰 서 있는 시간(초).</summary>
    public const float FacePauseSeconds = 0.7f;

    /// <summary>다가오는 동안 플레이어가 문 밖 자리에서 이 거리(m, 수평) 안이면 빠져나가려는 것으로 본다.</summary>
    public const float ExitRadius = 2.4f;

    /// <summary>대면 직후 이만큼(초)은 「문으로 빠져나가려 함」을 보지 않는다 — 문 쪽을 돌아본 순간 바로 붙잡히지 않게.</summary>
    public const float ExitGraceSeconds = 0.8f;

    /// <summary>걷는 동안 되풀이하는 소리(연출 소리 표) — 사용자 제공 「도서관 추격」.</summary>
    public const string ChaseSoundKey = "duck.chase";

    /// <summary>붙잡힘 출처 — 붙잡힘 연출이 보고 조도 사망 컷신을 얼굴 장면부터 튼다.</summary>
    public const string CaptureSource = "E.YellowFace.caught";

    /// <summary>네비메쉬를 기다리는 최대 시간(초).</summary>
    public const float NavWaitSeconds = 2f;

    /// <summary>목적지를 다시 잡는 간격(초).</summary>
    public const float RepathSeconds = 0.3f;

    private enum State
    {
        Approach,
        Arrive,   // 3초 비춤을 채웠다 — 코앞까지 와서 멈춘다
        Pause,
        Flee,
        Gone,
        Caught
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
    private float _pause;
    private AudioSource _chase;
    private float _chaseVolume;
    private float _chaseFade;

    /// <summary>3초 비춤을 채웠는지(검수용).</summary>
    public bool Satisfied
    {
        get { return _state == State.Arrive || _state == State.Pause || _state == State.Flee || _state == State.Gone; }
    }

    /// <summary>붙잡았는지(검수용).</summary>
    public bool Caught
    {
        get { return _state == State.Caught; }
    }

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
        d._lit = 0f;
        PrepareNav(go.transform, d._doorPoint);
        d.StartChase();
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
        if (_state == State.Flee || _state == State.Gone || _state == State.Caught) return;
        _state = State.Flee;
        StopChase(0.8f);
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
        _gap = -LitGapSeconds;   // 다음 프레임에 「빛이 끊겼다」로 지워지지 않게
    }

    private void Update()
    {
        StepChaseFade();
        if (_state == State.Gone || _state == State.Caught) return;
        if (!NightRun.IsNightActive || NightRun.IsCaptured)
        {
            Stop();
            StopChase(0f);
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
            case State.Arrive:
                Arrive();
                break;
            case State.Pause:
                Stop();
                _pause -= Time.deltaTime;
                if (_pause <= 0f) Flee();
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

        // ① 3초 이어서 비추면 — 코앞까지 와서 멈췄다가 달아난다.
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

        if (_lit >= SatisfySeconds)
        {
            _state = State.Arrive;
            _repath = 0f;
            _pause = 0f;
            if (DirectionStage.Verbose) Debug.Log("[LibraryDuck] 3초 비춤 — 코앞까지 와서 물러남");
            return;
        }

        // ② 도서관을 나가려 하면 · 안 비춘 채 코앞까지 오면 붙잡힘(사망).
        Vector3 eye = cam.transform.position;
        _grace -= dt;
        bool lit = _lit > 0f;
        float dist = Flat(eye, transform.position);
        bool near = dist <= CatchRadius;
        bool caught = near && !lit;   // 비추는 동안(3초를 채우는 중)은 가까이 와도 잡지 않는다 — 그 자리에 선다
        bool leaving = _grace <= 0f && (SpaceIds.Canonical(NightRun.CurrentSpace) != SpaceId.Library || Flat(eye, _doorPoint) <= ExitRadius);
        if (caught || leaving)
        {
            Catch(caught ? "안 비춘 채 코앞" : "도서관을 나가려 함");
            return;
        }

        // ③ 다가온다 — 비추는 채 코앞이면 선다. 등지면(얼굴이 화면 밖) 빨라진다.
        bool hold = lit && dist <= FaceStopRadius + 0.3f;
        float speed = FaceOnScreen(cam) ? ApproachSpeed : BehindSpeed;
        WalkToward(eye, speed, hold, dt);

        // 멈칫한 동안에도 몸은 돌리지 않는다 — 103차 후속(민: 「목만 플레이어에게 고정」): 고개만 따라 돈다(DoorDuck).
    }

    /// <summary>3초를 채운 뒤 — 코앞까지 온다. 도착하면 잠깐 서서 바라보다 달아난다.</summary>
    private void Arrive()
    {
        Camera cam = Camera.main;
        if (cam == null) return;
        Vector3 eye = cam.transform.position;
        _pause -= Time.deltaTime;   // 코앞까지 오는 상한(플레이어가 계속 달아나면 그만 물러난다)
        if (Flat(eye, transform.position) <= FaceStopRadius || _pause <= -ArriveMaxSeconds)
        {
            Stop();
            _state = State.Pause;
            _pause = FacePauseSeconds;
            StopChase(0.4f);
            return;
        }

        WalkToward(eye, BehindSpeed, false, Time.deltaTime);
    }

    private void WalkToward(Vector3 eye, float speed, bool hold, float dt)
    {
        if (!EnsureAgent())
        {
            _navWait -= dt;
            if (_navWait <= 0f && !hold) StepDirect(eye, speed, dt);
            return;
        }

        _agent.speed = speed;
        _agent.acceleration = 8f;
        _agent.stoppingDistance = 0.35f;
        _agent.isStopped = hold;
        _repath -= dt;
        if (_repath <= 0f && !hold)
        {
            _repath = RepathSeconds;
            Vector3 feet = new Vector3(eye.x, transform.position.y, eye.z);
            Vector3 to;
            if (RuntimeNav.Snap(feet, 2.5f, out to)) _agent.SetDestination(to);
        }
    }

    /// <summary>
    /// 104차(사용자: 「붙잡히고 연출이 나온 뒤 죽는 게 아니라 그냥 넘어졌다가 일어나게」): true면 붙잡혀도 죽지 않는다 — 조도 컷신 얼굴 장면 뒤 그 자리에서 넘어졌다 일어나고
    /// 밤은 이어진다(<see cref="CaptureDirector.Knockdown"/>). false면 예전처럼 조도 축 사망(재시작).
    /// </summary>
    public static bool KnockdownOnCatch = true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        KnockdownOnCatch = true;   // 104차 QA: 도메인 다시 읽기 없이 플레이를 다시 시작해도 기본값
    }

    /// <summary>붙잡는다 — 조도 컷신 얼굴 장면 뒤 넘어졌다 일어남(<see cref="KnockdownOnCatch"/>), 아니면 조도 축 사망.</summary>
    private void Catch(string why)
    {
        if (_state != State.Approach) return;
        Stop();
        StopChase(0f);
        _state = State.Caught;
        Debug.Log("[LibraryDuck] 붙잡음 — " + why);
        CaptureDirector cd = CaptureDirector.Active;
        if (KnockdownOnCatch && cd != null)
        {
            // 장면 동안 대역이 컷신 화면에 끼지 않게 먼저 감추고, 화면이 검을 때 치운다. 창은 지금 닫는다(밤이 이어지므로).
            foreach (Renderer r in GetComponentsInChildren<Renderer>(true)) r.enabled = false;
            // 104차 QA: 창을 닫기 전에 붙잡힘을 알린다 — 안 그러면 한 번 비춘 기록만으로 L3가 통과된다.
            NightRun.Send(new JudgeSignal(SignalKind.EncounterCaught, SpaceId.Library, FinalCues.FaceTarget, ActionSource.Direction, true, 0f));
            NightRun.EndEncounterWindow(_id);
            if (cd.Knockdown(FearAxis.Illuminance, CaptureSource, Vanish)) return;
            foreach (Renderer r in GetComponentsInChildren<Renderer>(true)) r.enabled = true;
        }

        NightRun.CaptureByEncounter(FearAxis.Illuminance, CaptureSource);
    }

    /// <summary>넘어진 뒤 — 노란 얼굴은 사라져 있다.</summary>
    private void Vanish()
    {
        _state = State.Gone;
        if (this != null && gameObject != null) Destroy(gameObject);   // 104차 QA: 꺼 두기만 하면 연출 정리 목록에 밤 끝까지 남았다
    }

    /// <summary>얼굴(머리)이 화면 안에 보이는지(가림은 보지 않는다 — 등졌는지만).</summary>
    private bool FaceOnScreen(Camera cam)
    {
        Vector3 face = _head != null ? _head.position : transform.position + Vector3.up * 1.6f;
        Vector3 v = cam.WorldToViewportPoint(face);
        return v.z > 0f && v.x > 0f && v.x < 1f && v.y > 0f && v.y < 1f;
    }

    // ── 추격 소리 ─────────────────────────────────────────────

    private void StartChase()
    {
        float volume;
        AudioClip clip = DirectionSoundTableSO.FindExact(ChaseSoundKey, out volume);
        if (clip == null) return;
        if (_chase == null) _chase = gameObject.AddComponent<AudioSource>();
        _chase.playOnAwake = false;
        _chase.clip = clip;
        _chase.loop = true;
        _chase.volume = volume;
        _chase.spatialBlend = 0.6f;   // 몹 쪽에서 들리되 멀어져도 꺼지지 않게
        _chase.dopplerLevel = 0f;
        _chase.minDistance = 2f;
        _chase.maxDistance = 25f;
        _chase.Play();
        _chaseVolume = volume;
        _chaseFade = 0f;
    }

    private void StopChase(float fadeSeconds)
    {
        if (_chase == null || !_chase.isPlaying) return;
        if (fadeSeconds <= 0f)
        {
            _chase.Stop();
            return;
        }

        _chaseFade = fadeSeconds;
    }

    private void StepChaseFade()
    {
        if (_chase == null || _chaseFade <= 0f || !_chase.isPlaying) return;
        _chase.volume = Mathf.MoveTowards(_chase.volume, 0f, _chaseVolume / _chaseFade * Time.deltaTime);
        if (_chase.volume <= 0.001f)
        {
            _chase.Stop();
            _chaseFade = 0f;
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
    private void StepDirect(Vector3 eye, float speed, float dt)
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

        transform.position += dir * speed * dt;
        Walk(speed);
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
        if ((_state == State.Approach || _state == State.Arrive || _state == State.Pause) && speed < 0.05f && _walkAnim)
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

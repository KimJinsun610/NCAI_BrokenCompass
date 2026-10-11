using System;
using NightDuty;
using UnityEngine;

/// <summary>
/// 모형 급습(70차 · 91차 개편).
/// <list type="bullet">
/// <item>70차(민: 「복도 급습은 과학실 옆 복도 비상등에서 플레이어 방향으로 뛰어와서 놀래키는 연출」): 과학실 옆 복도 동쪽 끝 비상구 유도등 아래에 서 있다가,
/// 플레이어가 복도에서 알아보면(62차 세워 두고 보면 대면) 달려온다.</item>
/// <item>91차(민: 「정지해 있다가 서서 뛰어오는 애니메이션으로 교체 · 더 빠르게 플레이어를 뚫고 쌩 지나가도록 · 지나가면 플레이어가 넘어졌다가 일어나게」 —
/// 옛 구부정한 자세는 내려다보게 돼 놀랍지 않았다): 선 인체 모형(<c>mob.dummy.stand</c> — 전날 복도 끝에 서 있던 그 모습)이 <see cref="StillSeconds"/>초 꼼짝 않다가
/// 고개를 홱 돌리고(<see cref="SnapSeconds"/>), 곧게 선 채 무릎을 높이 들고 팔을 휘두르며 달려온다 — 처음 <see cref="StartSpeed"/>m/s에서 <see cref="AccelSeconds"/>초 만에
/// <see cref="RunSpeed"/>m/s. 인체 모형 FBX에 달리기 클립이 없어 뼈를 직접 돌린다(엉덩이·무릎·발목·팔·팔꿈치·상체 숙임·몸 들썩임).</item>
/// <item>손전등이 켜져 있으면 속도를 늦추지 않고 플레이어 몸을 그대로 뚫고 지나간다(<see cref="Arrived"/>(참) — 부르는 쪽이 점프스케어 + 넘어짐),
/// 등 뒤 <see cref="ThroughDistance"/>m까지 달려가 사라진다. 꺼져 있으면(S5 「빛을 끄고 기다리십시오」를 지키는 쪽) 옆으로 <see cref="PassSide"/>m 비켜 어깨를 스치고 지나간다(<see cref="Arrived"/>(거짓)).</item>
/// <item>달려오는 길이 벽에 막히면(플레이어가 과학실로 들어감) 거기서 사라진다. 최대 <see cref="MaxRunSeconds"/>초.</item>
/// </list>
/// </summary>
[DisallowMultipleComponent]
public sealed class MannequinRush : MonoBehaviour
{
    /// <summary>91차: 알아본 뒤 꼼짝 않고 서 있는 시간(초).</summary>
    public const float StillSeconds = 0.8f;

    /// <summary>91차: 고개를 플레이어 쪽으로 홱 돌리는 시간(초).</summary>
    public const float SnapSeconds = 0.18f;

    /// <summary>옛 이름 — 달려 나오기까지 전체(정지 + 고개 돌림).</summary>
    public const float WindupSeconds = StillSeconds + SnapSeconds;

    /// <summary>91차: 최고 속도(m/s). 옛 6.</summary>
    public const float RunSpeed = 13f;

    /// <summary>91차: 첫 걸음 속도(m/s).</summary>
    public const float StartSpeed = 4f;

    /// <summary>91차: 최고 속도까지(초).</summary>
    public const float AccelSeconds = 0.55f;

    /// <summary>91차: 몸을 뚫고 지난 뒤 더 달려가는 거리(m) — 그 전에 화면 밖이 되면 사라진다.</summary>
    public const float ThroughDistance = 7f;

    /// <summary>이만큼(m) 가까우면 몸을 뚫은 것으로 친다.</summary>
    public const float HitDistance = 0.6f;

    /// <summary>손전등을 끈 쪽 — 옆으로 비키기 시작하는 거리(m).</summary>
    public const float SidestepDistance = 3.5f;

    /// <summary>스쳐 지나갈 때 옆으로 비키는 거리.</summary>
    public const float PassSide = 0.75f;

    /// <summary>달리는 최대 시간(대응 창 10초 안).</summary>
    public const float MaxRunSeconds = 7f;

    /// <summary>한 주기(두 걸음) 길이(m) — 최고 속도에서. 느릴 때는 짧아진다.</summary>
    public const float StrideCycle = 3.2f;

    private enum Phase { Idle, Still, Snap, Run, Through, Pass, Done }

    private Phase _phase = Phase.Idle;
    private float _t;
    private float _runT;
    private float _gait;
    private int _lastStep;
    private Vector3 _dir;
    private Vector3 _passOffset;
    private float _passed;
    private Action<bool> _arrived;
    private System.Random _rng;
    private float _jitter = 1f;
    private float _jitterLeft;

    private Transform _model;
    private Vector3 _modelRest;
    private Transform _spine, _chest, _neck, _head, _thighL, _thighR, _shinL, _shinR, _footL, _footR, _armL, _armR, _foreL, _foreR;
    private Quaternion[] _rest;
    private Transform[] _bones;

    /// <summary>몸을 뚫었을 때(참) 또는 옆으로 스쳤을 때(거짓) 한 번.</summary>
    public event Action<bool> Arrived
    {
        add { _arrived += value; }
        remove { _arrived -= value; }
    }

    /// <summary>끝났는가(사라짐).</summary>
    public bool Finished
    {
        get { return _phase == Phase.Done; }
    }

    /// <summary>지금 속도(m/s, 검수용).</summary>
    public float Speed { get; private set; }

    /// <summary>시작한다.</summary>
    public static MannequinRush Begin(GameObject mob, Action<bool> arrived)
    {
        MannequinRush r = mob.GetComponent<MannequinRush>();
        if (r == null) r = mob.AddComponent<MannequinRush>();
        r._arrived = arrived;
        r.Init();
        return r;
    }

    private void Init()
    {
        _rng = new System.Random(Environment.TickCount);
        _model = transform.Find("Model");
        if (_model != null) _modelRest = _model.localPosition;
        _spine = Bone("spine");
        _chest = Bone("chest");
        _neck = Bone("neck");
        _head = Bone("head");
        _thighL = Bone("thigh.L");
        _thighR = Bone("thigh.R");
        _shinL = Bone("shin.L");
        _shinR = Bone("shin.R");
        _footL = Bone("foot.L");
        _footR = Bone("foot.R");
        _armL = Bone("upper_arm.L");
        _armR = Bone("upper_arm.R");
        _foreL = Bone("forearm.L");
        _foreR = Bone("forearm.R");
        _bones = new[] { _spine, _chest, _neck, _head, _thighL, _thighR, _shinL, _shinR, _footL, _footR, _armL, _armR, _foreL, _foreR };
        _rest = new Quaternion[_bones.Length];
        for (int i = 0; i < _bones.Length; i++) if (_bones[i] != null) _rest[i] = _bones[i].localRotation;
        foreach (Collider c in GetComponentsInChildren<Collider>()) if (!c.isTrigger) c.enabled = false;   // 플레이어를 밀지 않게(몸을 뚫고 지나간다)
        _phase = Phase.Still;
        _t = 0f;
        _runT = 0f;
        _gait = 0f;
        _lastStep = 0;
        Speed = 0f;
    }

    private Transform Bone(string name)
    {
        foreach (Transform t in GetComponentsInChildren<Transform>(true))
        {
            if (string.Equals(t.name, name, StringComparison.OrdinalIgnoreCase)) return t;
        }

        return null;
    }

    private static Transform Player()
    {
        PlayerSensors hub = PlayerSensors.Active;
        return hub != null ? hub.PlayerRoot : null;
    }

    private static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v;
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f || _phase == Phase.Idle || _phase == Phase.Done) return;
        _t += dt;
        Transform player = Player();
        if (player == null)
        {
            Vanish();
            return;
        }

        switch (_phase)
        {
            case Phase.Still:
                if (_t >= StillSeconds) Next(Phase.Snap);
                break;
            case Phase.Snap:
                if (_t >= SnapSeconds)
                {
                    // 몸통도 플레이어 쪽으로 — 고개는 이미 돌아가 있다.
                    Vector3 d = Flat(player.position - transform.position);
                    if (d.sqrMagnitude > 1e-4f) transform.rotation = Quaternion.LookRotation(d.normalized, Vector3.up);
                    DirectionStage.PlaySound("step.HALL.run", transform.position + Vector3.up * 0.1f, 2f, 1f);
                    Next(Phase.Run);
                }

                break;
            case Phase.Run:
                Run(player, dt);
                break;
            case Phase.Through:
            case Phase.Pass:
                Coast(player, dt);
                break;
        }
    }

    private void Next(Phase p)
    {
        _phase = p;
        _t = 0f;
    }

    private float CurrentSpeed(float dt)
    {
        _runT += dt;
        _jitterLeft -= dt;
        if (_jitterLeft <= 0f)
        {
            // 관절 인형의 덜컥거림은 조금만 — 곧게 내달리는 속도감이 우선.
            _jitterLeft = 0.1f + (float)_rng.NextDouble() * 0.12f;
            _jitter = 0.85f + (float)_rng.NextDouble() * 0.3f;
        }

        float k = Mathf.Clamp01(_runT / AccelSeconds);
        Speed = Mathf.Lerp(StartSpeed, RunSpeed, k * k) * _jitter;
        return Speed;
    }

    private void Run(Transform player, float dt)
    {
        Vector3 to = Flat(player.position - transform.position);
        float dist = to.magnitude;
        Vector3 dir = dist > 1e-3f ? to / dist : Flat(transform.forward).normalized;
        bool lightOn = FlashlightRelay.Active == null || FlashlightRelay.Active.IsOn;

        if (!lightOn && dist < SidestepDistance)
        {
            // 불을 끄고 기다리면 — 옆으로 비켜 어깨를 스치고 지나간다.
            Vector3 side = Vector3.Cross(Vector3.up, dir).normalized;
            if (_rng.NextDouble() < 0.5) side = -side;
            _dir = dir;
            _passOffset = side * PassSide;
            _passed = 0f;
            Next(Phase.Pass);
            Fire(false);
            return;
        }

        if (dist <= HitDistance)
        {
            // 91차: 멈추지 않고 몸을 뚫는다 — 부르는 쪽이 점프스케어 + 넘어짐.
            _dir = dir;
            _passOffset = Vector3.zero;
            _passed = 0f;
            Next(Phase.Through);
            Fire(true);
            return;
        }

        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir, Vector3.up), Mathf.Clamp01(dt * 14f));
        float step = CurrentSpeed(dt) * dt;
        if (Blocked(dir, Mathf.Min(step, dist) + 0.35f) || _runT > MaxRunSeconds)
        {
            Vanish();   // 벽에 막힘(플레이어가 방으로 들어감) — 거기서 사라진다
            return;
        }

        transform.position += dir * Mathf.Min(step, dist + 0.2f);
        Advance(step);
    }

    /// <summary>뚫고(스치고) 지난 뒤 — 같은 방향으로 내달리다 화면 밖이거나 <see cref="ThroughDistance"/>에서 사라진다.</summary>
    private void Coast(Transform player, float dt)
    {
        float step = CurrentSpeed(dt) * dt;
        Vector3 move = _dir * step;
        if (_phase == Phase.Pass && _passOffset.sqrMagnitude > 0f)
        {
            // 옆으로 비키는 것은 처음 0.25초에.
            Vector3 lateral = _passOffset * Mathf.Clamp01(dt / 0.25f);
            move += lateral;
        }

        transform.position += move;
        transform.rotation = Quaternion.LookRotation(_dir, Vector3.up);
        Advance(step);
        _passed += step;
        Vector3 back = Flat(transform.position - player.position);
        bool behind = Vector3.Dot(back, Flat(player.forward)) < 0f;
        if (_passed >= ThroughDistance || (_t > 0.25f && (behind || !InView())) || _t > 1.5f) Vanish();
    }

    private void Fire(bool through)
    {
        if (through) DirectionStage.PlaySound(ProgramCatalog.ModelRush + ".release", transform.position + Vector3.up * 1.2f, 3f, 0.5f);   // 쌩 — 스쳐 가는 소리
        Action<bool> a = _arrived;
        _arrived = null;
        if (a == null) return;
        try { a(through); }
        catch (Exception ex) { Debug.LogException(ex, this); }
    }

    /// <summary>걸음 위상을 거리만큼 돌리고 발이 닿을 때마다 쿵.</summary>
    private void Advance(float meters)
    {
        float stride = Mathf.Lerp(1.6f, StrideCycle, Mathf.InverseLerp(StartSpeed, RunSpeed, Speed));
        _gait += meters * (Mathf.PI * 2f / Mathf.Max(0.5f, stride));
        int stepIndex = Mathf.FloorToInt(_gait / Mathf.PI);
        if (stepIndex != _lastStep)
        {
            _lastStep = stepIndex;
            DirectionStage.PlaySound("step.HALL.run", transform.position + Vector3.up * 0.1f, 2f, 1f);
        }
    }

    private bool Blocked(Vector3 dir, float distance)
    {
        Vector3 from = transform.position + Vector3.up * 1.0f;
        Transform player = Player();
        foreach (RaycastHit h in Physics.SphereCastAll(from, 0.25f, dir, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            Transform t = h.collider.transform;
            if (t.IsChildOf(transform)) continue;
            if (player != null && t.IsChildOf(player)) continue;
            if (h.collider.GetComponentInParent<CharacterController>() != null) continue;
            if (h.collider.GetComponentInParent<FPController>() != null) continue;
            if (h.distance <= 0f) continue;   // 시작부터 겹친 것(바닥 등)
            return true;
        }

        return false;
    }

    private bool InView()
    {
        Camera cam = Camera.main;
        if (cam == null) return false;
        Vector3 v = cam.WorldToViewportPoint(transform.position + Vector3.up * 1.2f);
        return v.z > 0f && v.x > -0.05f && v.x < 1.05f && v.y > -0.05f && v.y < 1.05f;
    }

    private void Vanish()
    {
        if (_phase == Phase.Done) return;
        _phase = Phase.Done;
        foreach (Renderer r in GetComponentsInChildren<Renderer>()) r.enabled = false;
        foreach (Collider c in GetComponentsInChildren<Collider>()) c.enabled = false;
    }

    private void LateUpdate()
    {
        if (_bones == null || _phase == Phase.Idle || _phase == Phase.Done) return;
        for (int i = 0; i < _bones.Length; i++) if (_bones[i] != null) _bones[i].localRotation = _rest[i];

        Vector3 right = transform.right;
        bool running = _phase == Phase.Run || _phase == Phase.Through || _phase == Phase.Pass;
        float run = running ? Mathf.Clamp01(_runT / AccelSeconds) : 0f;   // 첫 걸음은 작게 → 다 펼친 달리기
        float s = Mathf.Sin(_gait);
        float c = Mathf.Cos(_gait);

        if (running)
        {
            // 곧게 선 채 살짝 앞으로 기울인다(내려다보게 숙이지 않는다).
            Turn(_spine, right, 6f + 4f * run);
            Turn(_chest, right, 3f);

            // 다리: 앞으로 = 오른쪽 축 −. 앞다리는 높이, 무릎은 뒤로 접힘(+), 발목은 지면에 맞춰.
            float swing = Mathf.Lerp(30f, 58f, run);
            Turn(_thighL, right, -swing * s - 8f * run);
            Turn(_thighR, right, swing * s - 8f * run);
            float knee = Mathf.Lerp(45f, 105f, run);
            Turn(_shinL, right, knee * Mathf.Max(0f, -c) + 12f);
            Turn(_shinR, right, knee * Mathf.Max(0f, c) + 12f);
            Turn(_footL, right, -18f * Mathf.Max(0f, -c));
            Turn(_footR, right, -18f * Mathf.Max(0f, c));

            // 팔: 다리와 반대로 크게 휘두르고 팔꿈치를 접는다.
            float arm = Mathf.Lerp(35f, 65f, run);
            Turn(_armL, right, arm * s);
            Turn(_armR, right, -arm * s);
            Turn(_foreL, right, -Mathf.Lerp(40f, 80f, run));
            Turn(_foreR, right, -Mathf.Lerp(40f, 80f, run));

            if (_model != null) _model.localPosition = _modelRest + Vector3.up * (Mathf.Abs(s) * Mathf.Lerp(0.04f, 0.1f, run));
        }
        else if (_model != null)
        {
            _model.localPosition = _modelRest;
        }

        // 고개: 꼼짝 않다가(Still) 홱 플레이어 쪽으로(Snap) — 달리는 동안도 플레이어를 노려본다.
        if (_head != null && _phase != Phase.Still)
        {
            float w = _phase == Phase.Snap ? Mathf.Clamp01(_t / SnapSeconds) : 1f;
            w = w * w * (3f - 2f * w);
            Camera cam = Camera.main;
            if (cam != null && _phase != Phase.Through && _phase != Phase.Pass)
            {
                Vector3 look = cam.transform.position - _head.position;
                if (look.sqrMagnitude > 1e-4f)
                {
                    Quaternion want = Quaternion.FromToRotation(transform.forward, look.normalized) * _head.rotation;
                    _head.rotation = Quaternion.Slerp(_head.rotation, want, w);
                }
            }

            // 고개를 살짝 비튼다(관절 인형).
            _head.rotation = Quaternion.AngleAxis(-14f * w, transform.forward) * _head.rotation;
        }
    }

    private static void Turn(Transform bone, Vector3 axis, float degrees)
    {
        if (bone == null || Mathf.Abs(degrees) < 0.01f) return;
        bone.rotation = Quaternion.AngleAxis(degrees, axis) * bone.rotation;
    }
}

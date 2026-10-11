using System;
using NightDuty;
using UnityEngine;

/// <summary>
/// 모형 급습(70차, 민: 「복도 급습은 과학실 옆 복도 비상등에서 플레이어 방향으로 뛰어와서 놀래키는 연출」).
/// <list type="bullet">
/// <item>대역(<c>mob.dummy</c>)은 과학실 옆 복도 동쪽 끝, 비상구 유도등(<c>Corridors/Sign_Exit</c> (54, 3.95, 46.39)) 아래에 서 있다가
/// 플레이어가 복도에서 알아보면(62차 세워 두고 보면 대면) 고개를 꺾고 → 플레이어 쪽으로 달려온다.</item>
/// <item>달리기 클립이 없어(인체 모형 FBX는 깨어남·넘어짐뿐) 다리·팔·상체를 뼈로 흔들고, 속도는 0.12~0.25초마다 덜컥거린다(관절 인형의 경련 달리기).
/// 발마다 쿵쿵(<c>step.HALL.run</c>).</item>
/// <item>닿으면: 손전등이 켜져 있으면 눈앞 0.75m에서 멈춰 덮치고(점프스케어, <see cref="Arrived"/>(참)) 암전 번쩍과 함께 사라진다.
/// 꺼져 있으면(S5 「빛을 끄고 기다리십시오」를 지키는 쪽) 어깨를 스치고 뒤로 지나가 사라진다(<see cref="Arrived"/>(거짓)).</item>
/// <item>달려오는 길이 벽에 막히면(플레이어가 과학실로 들어감) 거기서 멈춰 사라진다.</item>
/// </list>
/// </summary>
[DisallowMultipleComponent]
public sealed class MannequinRush : MonoBehaviour
{
    /// <summary>알아본 뒤 달려 나오기까지(고개를 꺾는 시간).</summary>
    public const float WindupSeconds = 0.7f;

    /// <summary>평균 달리기 속도(m/s). 덜컥거림으로 0.65~1.5배.</summary>
    public const float RunSpeed = 6f;

    /// <summary>덮칠 때 플레이어(카메라) 앞 거리.</summary>
    public const float LungeDistance = 0.75f;

    /// <summary>덮친 채 멈춰 있는 시간.</summary>
    public const float HoldSeconds = 0.35f;

    /// <summary>스쳐 지나갈 때 옆으로 비키는 거리.</summary>
    public const float PassSide = 0.7f;

    /// <summary>달리는 최대 시간(대응 창 10초 안).</summary>
    public const float MaxRunSeconds = 7f;

    private enum Phase { Idle, Windup, Run, Lunge, Pass, Done }

    private Phase _phase = Phase.Idle;
    private float _t;
    private float _burstLeft;
    private float _burst = 1f;
    private float _gait;
    private int _lastStep;
    private Vector3 _passTarget;
    private Vector3 _passDir;
    private Action<bool> _arrived;
    private System.Random _rng;

    private Transform _model;
    private Vector3 _modelRest;
    private Transform _spine, _chest, _head, _thighL, _thighR, _shinL, _shinR, _armL, _armR;
    private Quaternion[] _rest;
    private Transform[] _bones;

    /// <summary>닿았을 때 한 번(참 = 덮침, 거짓 = 스쳐 감).</summary>
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
        _head = Bone("head");
        _thighL = Bone("thigh.L");
        _thighR = Bone("thigh.R");
        _shinL = Bone("shin.L");
        _shinR = Bone("shin.R");
        _armL = Bone("upper_arm.L");
        _armR = Bone("upper_arm.R");
        _bones = new[] { _spine, _chest, _head, _thighL, _thighR, _shinL, _shinR, _armL, _armR };
        _rest = new Quaternion[_bones.Length];
        for (int i = 0; i < _bones.Length; i++) if (_bones[i] != null) _rest[i] = _bones[i].localRotation;
        foreach (Collider c in GetComponentsInChildren<Collider>()) if (!c.isTrigger) c.enabled = false;   // 플레이어를 밀지 않게
        _phase = Phase.Windup;
        _t = 0f;
        _gait = 0f;
        _lastStep = 0;
        FacePlayer(1f);
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

    private void FacePlayer(float k)
    {
        Transform p = Player();
        if (p == null) return;
        Vector3 d = Flat(p.position - transform.position);
        if (d.sqrMagnitude < 1e-4f) return;
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(d.normalized, Vector3.up), k);
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
            case Phase.Windup:
                FacePlayer(Mathf.Clamp01(dt * 8f));
                if (_t >= WindupSeconds)
                {
                    _phase = Phase.Run;
                    _t = 0f;
                }

                break;
            case Phase.Run:
                Run(player, dt);
                break;
            case Phase.Lunge:
                if (_t >= HoldSeconds) Vanish();
                break;
            case Phase.Pass:
                Move(_passDir, RunSpeed * 1.2f * dt);
                Advance(RunSpeed * 1.2f * dt);
                Vector3 back = Flat(transform.position - player.position);
                if (_t > 0.9f || (_t > 0.35f && Vector3.Dot(back, Flat(player.forward)) < 0f && !InView())) Vanish();
                break;
        }
    }

    private void Run(Transform player, float dt)
    {
        _burstLeft -= dt;
        if (_burstLeft <= 0f)
        {
            _burstLeft = 0.12f + (float)_rng.NextDouble() * 0.13f;
            _burst = 0.65f + (float)_rng.NextDouble() * 0.85f;
        }

        Vector3 to = Flat(player.position - transform.position);
        float dist = to.magnitude;
        Vector3 dir = dist > 1e-3f ? to / dist : transform.forward;
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir, Vector3.up), Mathf.Clamp01(dt * 12f));

        bool lightOn = FlashlightRelay.Active == null || FlashlightRelay.Active.IsOn;
        if (!lightOn && dist < 2.2f)
        {
            // 불을 끄고 기다리면 — 어깨를 스치고 뒤로 지나간다.
            Vector3 side = Vector3.Cross(Vector3.up, dir).normalized;
            _passDir = (dir * 2.2f + side * PassSide).normalized;
            _phase = Phase.Pass;
            _t = 0f;
            Fire(false);
            return;
        }

        if (dist <= LungeDistance + 0.15f)
        {
            Lunge(player);
            return;
        }

        float step = Mathf.Min(RunSpeed * _burst * dt, dist - LungeDistance);
        if (Blocked(dir, step + 0.35f) || _t > MaxRunSeconds)
        {
            Vanish();   // 벽에 막힘(플레이어가 방으로 들어감) — 거기서 사라진다
            return;
        }

        Move(dir, step);
        Advance(step);
    }

    private void Lunge(Transform player)
    {
        Vector3 fwd = Flat(player.forward);
        if (fwd.sqrMagnitude < 1e-4f) fwd = Flat(transform.position - player.position);
        fwd.Normalize();
        // 플레이어가 보는 쪽 정면으로 — 등 뒤에서 닿아도 돌아본 얼굴 앞에 오도록 하지는 않는다(시선 강제 금지): 다가온 쪽 그대로 두되 거리만 맞춘다.
        Vector3 from = Flat(transform.position - player.position);
        Vector3 at = from.sqrMagnitude > 1e-4f ? from.normalized : fwd;
        Vector3 p = player.position + at * LungeDistance;
        p.y = transform.position.y;
        transform.position = p;
        transform.rotation = Quaternion.LookRotation(-at, Vector3.up);
        _phase = Phase.Lunge;
        _t = 0f;
        Fire(true);
    }

    private void Fire(bool lunge)
    {
        Action<bool> a = _arrived;
        _arrived = null;
        if (a == null) return;
        try { a(lunge); }
        catch (Exception ex) { Debug.LogException(ex, this); }
    }

    private void Move(Vector3 dir, float step)
    {
        transform.position += dir * step;
    }

    /// <summary>걸음 위상을 거리만큼 돌리고 발이 닿을 때마다 쿵.</summary>
    private void Advance(float meters)
    {
        _gait += meters * (Mathf.PI * 2f / 2.4f);   // 한 주기(두 걸음) 2.4m
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
        return v.z > 0f && v.x > 0f && v.x < 1f && v.y > 0f && v.y < 1f;
    }

    private void Vanish()
    {
        if (_phase == Phase.Done) return;
        if (_phase == Phase.Lunge)
        {
            DirectionStage stage = DirectionStage.Active;
            if (stage != null && stage.Fx != null) stage.Fx.Flash("enc.rush.vanish", DirectionScreenFx.Kind.Blackout, 1, 1f);
        }

        _phase = Phase.Done;
        foreach (Renderer r in GetComponentsInChildren<Renderer>()) r.enabled = false;
        foreach (Collider c in GetComponentsInChildren<Collider>()) c.enabled = false;
    }

    private void LateUpdate()
    {
        if (_bones == null || _phase == Phase.Idle || _phase == Phase.Done) return;
        for (int i = 0; i < _bones.Length; i++) if (_bones[i] != null) _bones[i].localRotation = _rest[i];

        Vector3 right = transform.right;
        bool running = _phase == Phase.Run || _phase == Phase.Pass;
        float wind = _phase == Phase.Windup ? Mathf.SmoothStep(0f, 1f, _t / WindupSeconds) : 1f;
        float s = Mathf.Sin(_gait);
        float c = Mathf.Cos(_gait);

        // 상체를 앞으로 숙이고(오른쪽 축 +), 고개는 비튼다(관절 인형). 덮칠 때는 상체를 일으키고 두 팔을 앞으로 뻗는다.
        bool lunge = _phase == Phase.Lunge;
        Turn(_spine, right, lunge ? -18f : 14f * wind);
        Turn(_chest, right, lunge ? -12f : 10f * wind);
        if (lunge)
        {
            Turn(_armL, right, -80f);
            Turn(_armR, right, -70f);
        }
        if (_head != null) _head.rotation = Quaternion.AngleAxis(-22f * wind, transform.forward) * Quaternion.AngleAxis(-12f * wind, right) * _head.rotation;

        if (running)
        {
            // 다리: 앞으로 = 오른쪽 축 −. 무릎은 뒤로 굽힘(+).
            Turn(_thighL, right, -38f * s);
            Turn(_thighR, right, 38f * s);
            Turn(_shinL, right, 55f * Mathf.Max(0f, -c));
            Turn(_shinR, right, 55f * Mathf.Max(0f, c));
            // 팔: 다리와 반대.
            Turn(_armL, right, 40f * s);
            Turn(_armR, right, -40f * s);
        }

        if (_model != null) _model.localPosition = _modelRest + Vector3.up * (running ? Mathf.Abs(s) * 0.07f : 0f);
        if (_phase == Phase.Lunge) FitFace();
    }

    /// <summary>
    /// 덮칠 때 얼굴을 눈앞에(70차 실측: 구부정한 모형을 0.75m에 세우면 머리가 눈보다 1.9m 아래·화면 밖이라 아무것도 안 보였다).
    /// 상체를 일으켜 두 팔을 뻗고, 머리 뼈가 카메라 앞 <see cref="FaceDistance"/>m·눈 조금 아래에 오도록 몸을 띄우고 당긴다.
    /// </summary>
    private void FitFace()
    {
        Camera cam = Camera.main;
        if (cam == null || _head == null || _model == null) return;
        Vector3 c = cam.transform.position;
        Vector3 away = Flat(transform.position - c);
        if (away.sqrMagnitude < 1e-4f) away = Flat(cam.transform.forward);
        away.Normalize();
        Vector3 want = c + away * FaceDistance + Vector3.down * 0.12f;
        Vector3 delta = want - _head.position;
        transform.position += Flat(delta);
        _model.localPosition += Vector3.up * delta.y;
    }

    /// <summary>덮칠 때 카메라에서 머리까지(m).</summary>
    public const float FaceDistance = 0.5f;

    private static void Turn(Transform bone, Vector3 axis, float degrees)
    {
        if (bone == null || Mathf.Abs(degrees) < 0.01f) return;
        bone.rotation = Quaternion.AngleAxis(degrees, axis) * bone.rotation;
    }
}

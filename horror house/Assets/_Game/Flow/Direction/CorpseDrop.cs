using System.Collections.Generic;
using NightDuty;
using UnityEngine;

/// <summary>
/// 51차 시체 낙하(민: 「사다리를 확인하는 도중 시체가 물리효과를 일으키며 바로 앞에 떨어지고, 깜짝 놀란 플레이어가 시야를 돌리면 사라지게」).
/// <list type="bullet">
/// <item>천장 다리 대역(<c>mob.legs</c> = 치마 소년 리그)을 런타임 래그돌로 바꿔 천장 바로 밑에서 떨어뜨린다 — 뼈 11개에 리지드바디·콜라이더·캐릭터 조인트(약 40kg).</item>
/// <item>판정 기준점(<c>rule.C2.legs</c>)은 루트에 둔다 — 응시 레이가 어느 팔다리에 맞아도 「떨어진 것」을 본 것으로 센다.</item>
/// <item>첫 충돌에 몸 부딪는 소리(<c>corpse.fall</c>), 2.8초 뒤(또는 멈추면) 키네마틱으로 굳힌다.</item>
/// <item>착지 뒤 한 번 화면에 들어온 다음, 1.2초가 지나고 몸 어디도 시선(카메라 정면 30°·화면 안)에 없는 채 0.5초면 조용히 사라진다.</item>
/// </list>
/// 전조(먼지)는 <see cref="Dust"/>가 만든다. 플레이어 충돌체와는 부딪히지 않는다(길을 막거나 밀지 않게).
/// </summary>
[DisallowMultipleComponent]
public sealed class CorpseDrop : MonoBehaviour
{
    /// <summary>원본 대역(치마 소년 리그).</summary>
    public const string SourceStandIn = "mob.legs";

    /// <summary>시야 밖으로 본다는 각도(°).</summary>
    public const float LookAwayAngle = 30f;

    /// <summary>시야 밖에 이만큼 있으면 사라진다(초).</summary>
    public const float LookAwaySeconds = 0.5f;

    /// <summary>착지 뒤 이만큼은 사라지지 않는다(초).</summary>
    public const float MinShowSeconds = 1.2f;

    /// <summary>몸무게 합(kg).</summary>
    public const float TotalMass = 40f;

    private readonly List<Rigidbody> _bodies = new List<Rigidbody>();
    private float _spawnedAt;
    private float _landedAt = -1f;
    private float _awaySince = -1f;
    private bool _frozen;
    private bool _seen;
    private bool _gone;

    /// <summary>사라졌는지.</summary>
    public bool Gone
    {
        get { return _gone; }
    }

    /// <summary>땅에 닿았는지.</summary>
    public bool Landed
    {
        get { return _landedAt >= 0f; }
    }

    /// <summary>
    /// 시체를 <paramref name="drop"/>(바닥 점) 위 천장 바로 밑에서 떨어뜨린다. <paramref name="facePlayer"/> 쪽으로 머리가 오게 기울여 떨어진다.
    /// </summary>
    public static CorpseDrop Spawn(Vector3 drop, Vector3 facePlayer, float ceilingY, string anchorId)
    {
        // 천장이 높은 교실(5.5m)에서는 천장 바로 밑이 시야 밖이다 — 바닥 위 2.9m(눈높이 위 1.5m 남짓)에서 떨어뜨려 곧바로 시야에 들어오게.
        float top = Mathf.Clamp(ceilingY - 0.25f, drop.y + 1.9f, drop.y + 2.9f);
        Vector3 toPlayer = facePlayer - drop;
        toPlayer.y = 0f;
        if (toPlayer.sqrMagnitude < 0.01f) toPlayer = Vector3.forward;
        toPlayer.Normalize();

        // 대역 피벗은 천장(발이 아래로 매달림). 머리가 플레이어 쪽·아래로 오게 앞으로 70° 기울여 떨어뜨린다.
        Quaternion rot = Quaternion.LookRotation(toPlayer, Vector3.up) * Quaternion.Euler(-70f, 0f, 0f);
        GameObject go = StandInFactory.Create(SourceStandIn, new Vector3(drop.x, top, drop.z), rot, string.Empty);
        go.name = "시체 낙하";

        // 대역의 응시 상자(Aim)는 쓰지 않는다 — 팔다리 콜라이더가 응시 대상이다.
        Transform aim = go.transform.Find("Aim");
        if (aim != null)
        {
            aim.gameObject.SetActive(false);
            Destroy(aim.gameObject);
        }

        CorpseDrop c = go.AddComponent<CorpseDrop>();
        c.Build();
        if (!string.IsNullOrEmpty(anchorId))
        {
            JudgeTarget t = go.AddComponent<JudgeTarget>();
            t.SetIds(anchorId);
        }

        c.Launch(toPlayer);
        return c;
    }

    /// <summary>
    /// 전조 먼지 — 천장 점에서 가는 먼지가 떨어진다. <paramref name="seconds"/> 뒤 스스로 거둔다.
    /// </summary>
    public static GameObject Dust(Vector3 ceiling, float seconds)
    {
        GameObject go = new GameObject("천장 먼지");
        go.transform.SetPositionAndRotation(ceiling + Vector3.down * 0.05f, Quaternion.LookRotation(Vector3.down));
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = ps.main;
        main.duration = Mathf.Max(0.2f, seconds);
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.3f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.035f);
        main.startColor = new Color(0.55f, 0.52f, 0.48f, 0.85f);
        main.gravityModifier = 0.35f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 300;

        ParticleSystem.EmissionModule em = ps.emission;
        em.rateOverTime = 45f;
        em.SetBursts(new[]
        {
            new ParticleSystem.Burst(0f, 25),
            new ParticleSystem.Burst(Mathf.Max(0.1f, seconds * 0.6f), 45)
        });

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(0.6f, 0.6f, 0.02f);

        ParticleSystemRenderer r = go.GetComponent<ParticleSystemRenderer>();
        Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (sh == null) sh = Shader.Find("Sprites/Default");
        Material m = new Material(sh);
        Color dust = new Color(0.6f, 0.57f, 0.52f, 0.9f);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", dust);
        if (m.HasProperty("_Color")) m.SetColor("_Color", dust);
        r.sharedMaterial = m;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        ps.Play();
        Destroy(go, seconds + 3f);
        return go;
    }

    // ── 래그돌 ───────────────────────────────────────────────

    private struct Part
    {
        public string Bone;
        public string Tip;
        public string Parent;
        public float Radius;
        public float Mass;
        public int Shape;   // 0 캡슐, 1 상자, 2 구
        public float Low;
        public float High;
        public float Swing;
    }

    private static readonly Part[] Parts =
    {
        new Part { Bone = "Hips", Tip = "Spine", Parent = null, Radius = 0.12f, Mass = 0.16f, Shape = 1 },
        new Part { Bone = "Chest", Tip = "Neck", Parent = "Hips", Radius = 0.12f, Mass = 0.2f, Shape = 1, Low = -25f, High = 25f, Swing = 20f },
        new Part { Bone = "Head", Tip = null, Parent = "Chest", Radius = 0.1f, Mass = 0.08f, Shape = 2, Low = -40f, High = 25f, Swing = 30f },
        new Part { Bone = "LeftUpperArm", Tip = "LeftLowerArm", Parent = "Chest", Radius = 0.045f, Mass = 0.04f, Low = -70f, High = 10f, Swing = 50f },
        new Part { Bone = "LeftLowerArm", Tip = "LeftHand", Parent = "LeftUpperArm", Radius = 0.04f, Mass = 0.03f, Low = -90f, High = 0f, Swing = 0f },
        new Part { Bone = "RightUpperArm", Tip = "RightLowerArm", Parent = "Chest", Radius = 0.045f, Mass = 0.04f, Low = -70f, High = 10f, Swing = 50f },
        new Part { Bone = "RightLowerArm", Tip = "RightHand", Parent = "RightUpperArm", Radius = 0.04f, Mass = 0.03f, Low = -90f, High = 0f, Swing = 0f },
        new Part { Bone = "LeftUpperLeg", Tip = "LeftLowerLeg", Parent = "Hips", Radius = 0.06f, Mass = 0.1f, Low = -20f, High = 70f, Swing = 30f },
        new Part { Bone = "LeftLowerLeg", Tip = "LeftFoot", Parent = "LeftUpperLeg", Radius = 0.05f, Mass = 0.06f, Low = -80f, High = 0f, Swing = 0f },
        new Part { Bone = "RightUpperLeg", Tip = "RightLowerLeg", Parent = "Hips", Radius = 0.06f, Mass = 0.1f, Low = -20f, High = 70f, Swing = 30f },
        new Part { Bone = "RightLowerLeg", Tip = "RightFoot", Parent = "RightUpperLeg", Radius = 0.05f, Mass = 0.06f, Low = -80f, High = 0f, Swing = 0f },
    };

    private void Build()
    {
        Dictionary<string, Transform> bones = new Dictionary<string, Transform>();
        foreach (Transform t in GetComponentsInChildren<Transform>(true))
        {
            if (!bones.ContainsKey(t.name)) bones[t.name] = t;
        }

        // 기존 콜라이더(대역 몸통)는 끈다 — 래그돌 콜라이더만 남긴다.
        foreach (Collider old in GetComponentsInChildren<Collider>(true)) old.enabled = false;

        Dictionary<string, Rigidbody> made = new Dictionary<string, Rigidbody>();
        for (int i = 0; i < Parts.Length; i++)
        {
            Part p = Parts[i];
            Transform bone;
            if (!bones.TryGetValue(p.Bone, out bone)) continue;
            Transform tip = null;
            if (p.Tip != null) bones.TryGetValue(p.Tip, out tip);

            float s = Mathf.Max(0.0001f, Mathf.Abs(bone.lossyScale.x));
            Vector3 localTip = tip != null ? bone.InverseTransformPoint(tip.position) : Vector3.up * (0.16f / s);
            float length = localTip.magnitude;

            switch (p.Shape)
            {
                case 1:
                {
                    BoxCollider box = bone.gameObject.AddComponent<BoxCollider>();
                    box.center = localTip * 0.5f;
                    int axis = MajorAxis(localTip);
                    Vector3 size = Vector3.one * (p.Radius * 2f / s);
                    size[axis] = Mathf.Max(length, p.Radius / s);
                    if (axis != 0) size.x *= 1.35f;   // 어깨·골반 폭
                    box.size = size;
                    break;
                }
                case 2:
                {
                    SphereCollider sphere = bone.gameObject.AddComponent<SphereCollider>();
                    sphere.radius = p.Radius / s;
                    sphere.center = localTip * 0.5f;
                    break;
                }
                default:
                {
                    CapsuleCollider cap = bone.gameObject.AddComponent<CapsuleCollider>();
                    cap.direction = MajorAxis(localTip);
                    cap.center = localTip * 0.5f;
                    cap.radius = p.Radius / s;
                    cap.height = length + cap.radius * 2f;
                    break;
                }
            }

            Rigidbody rb = bone.gameObject.AddComponent<Rigidbody>();
            rb.mass = Mathf.Max(0.5f, TotalMass * p.Mass);
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            rb.linearDamping = 0.05f;
            rb.angularDamping = 0.6f;
            rb.solverIterations = 12;
            made[p.Bone] = rb;
            _bodies.Add(rb);

            Rigidbody parent;
            if (p.Parent != null && made.TryGetValue(p.Parent, out parent))
            {
                CharacterJoint j = bone.gameObject.AddComponent<CharacterJoint>();
                j.connectedBody = parent;
                j.axis = Vector3.right;
                j.swingAxis = Vector3.forward;
                j.lowTwistLimit = new SoftJointLimit { limit = p.Low };
                j.highTwistLimit = new SoftJointLimit { limit = p.High };
                j.swing1Limit = new SoftJointLimit { limit = p.Swing };
                j.swing2Limit = new SoftJointLimit { limit = p.Swing };
                j.enableProjection = true;
            }

            bone.gameObject.AddComponent<CorpseHitRelay>().Owner = this;
        }

        // 몸 콜라이더끼리, 그리고 플레이어와는 부딪히지 않는다.
        List<Collider> mine = new List<Collider>();
        foreach (Rigidbody rb in _bodies) mine.AddRange(rb.GetComponents<Collider>());
        for (int a = 0; a < mine.Count; a++)
        {
            for (int b = a + 1; b < mine.Count; b++) Physics.IgnoreCollision(mine[a], mine[b], true);
        }

        PlayerSensors hub = PlayerSensors.Active;
        if (hub != null && hub.PlayerRoot != null)
        {
            foreach (Collider pc in hub.PlayerRoot.GetComponentsInChildren<Collider>(true))
            {
                for (int a = 0; a < mine.Count; a++) Physics.IgnoreCollision(mine[a], pc, true);
            }
        }

        foreach (SkinnedMeshRenderer smr in GetComponentsInChildren<SkinnedMeshRenderer>(true)) smr.updateWhenOffscreen = true;
    }

    private static int MajorAxis(Vector3 v)
    {
        float ax = Mathf.Abs(v.x), ay = Mathf.Abs(v.y), az = Mathf.Abs(v.z);
        if (ax >= ay && ax >= az) return 0;
        return ay >= az ? 1 : 2;
    }

    private void Launch(Vector3 toPlayer)
    {
        _spawnedAt = Time.time;
        Vector3 side = Vector3.Cross(Vector3.up, toPlayer);
        for (int i = 0; i < _bodies.Count; i++)
        {
            Rigidbody rb = _bodies[i];
            rb.linearVelocity = Vector3.down * 3f + toPlayer * 0.4f;
            rb.angularVelocity = side * Random.Range(1.5f, 2.5f) + Random.insideUnitSphere * 0.8f;
        }
    }

    internal void OnPartHit(Collision c)
    {
        if (_landedAt >= 0f || _gone) return;
        if (c.collider != null && c.collider.transform.IsChildOf(transform)) return;
        _landedAt = Time.time;

        // 닿은 뒤에는 미끄러지지 않게 — 마찰 대신 감쇠를 높여 그 자리에 무너지게 한다.
        for (int i = 0; i < _bodies.Count; i++)
        {
            _bodies[i].linearDamping = 2.5f;
            _bodies[i].angularDamping = 3f;
        }

        float speed = c.relativeVelocity.magnitude;
        float volume;
        AudioClip clip = DirectionSoundTableSO.FindExact("corpse.fall", out volume);
        if (clip == null) return;

        GameObject go = new GameObject("시체 낙하 소리");
        go.transform.position = c.contactCount > 0 ? c.GetContact(0).point : transform.position;
        AudioSource s = go.AddComponent<AudioSource>();
        s.clip = clip;
        s.volume = volume * Mathf.Clamp(speed / 5f, 0.6f, 1f);
        s.spatialBlend = 0.7f;
        s.minDistance = 4f;
        s.maxDistance = 30f;
        s.dopplerLevel = 0f;
        s.priority = 8;
        s.Play();
        Destroy(go, clip.length + 0.2f);
    }

    private void Update()
    {
        if (_gone) return;
        float now = Time.time;
        if (_landedAt < 0f && now - _spawnedAt > 1.2f) _landedAt = now;   // 충돌을 못 받았어도 떨어진 것으로 본다

        if (!_frozen && _landedAt >= 0f && (now - _landedAt > 1.6f || (now - _landedAt > 0.8f && Resting())))
        {
            _frozen = true;
            for (int i = 0; i < _bodies.Count; i++)
            {
                _bodies[i].isKinematic = true;
                _bodies[i].interpolation = RigidbodyInterpolation.None;
            }
        }

        if (_landedAt < 0f) return;
        Camera cam = Camera.main;
        if (cam == null) return;
        bool inView = InView(cam);
        if (inView) _seen = true;
        if (now - _landedAt < MinShowSeconds) return;

        // 착지한 뒤 한 번은 화면에 들어와야 한다 — 보지도 못한 채 사라지면 놀랄 틈이 없다(그때는 조우 창이 닫힐 때 거둔다).
        if (inView || !_seen)
        {
            _awaySince = -1f;
            return;
        }

        if (_awaySince < 0f) _awaySince = now;
        if (now - _awaySince >= LookAwaySeconds)
        {
            Debug.Log("[CorpseDrop] 시선을 돌려 사라짐(착지 뒤 " + (now - _landedAt).ToString("F1") + "초)");
            Vanish();
        }
    }

    /// <summary>몸의 어느 부분이든 화면 안(가장자리 5% 여유 안쪽)에 있는지. 정면에서 <see cref="LookAwayAngle"/>° 밖이면 보이지 않는 것으로 본다.</summary>
    private bool InView(Camera cam)
    {
        Vector3 eye = cam.transform.position;
        for (int i = 0; i < _bodies.Count; i++)
        {
            Vector3 p = _bodies[i].worldCenterOfMass;
            Vector3 v = cam.WorldToViewportPoint(p);
            if (v.z <= 0f || v.x < 0.05f || v.x > 0.95f || v.y < 0.05f || v.y > 0.95f) continue;
            if (Vector3.Angle(cam.transform.forward, p - eye) <= LookAwayAngle) return true;
        }

        return false;
    }

    private bool Resting()
    {
        for (int i = 0; i < _bodies.Count; i++)
        {
            if (_bodies[i].linearVelocity.sqrMagnitude > 0.04f) return false;
        }

        return true;
    }

    /// <summary>사라진다(판정 기준점도 함께 꺼진다).</summary>
    public void Vanish()
    {
        if (_gone) return;
        _gone = true;
        gameObject.SetActive(false);
    }
}

/// <summary>래그돌 뼈의 충돌을 <see cref="CorpseDrop"/>로 넘긴다.</summary>
public sealed class CorpseHitRelay : MonoBehaviour
{
    /// <summary>주인.</summary>
    public CorpseDrop Owner;

    private void OnCollisionEnter(Collision c)
    {
        if (Owner != null) Owner.OnPartHit(c);
    }
}

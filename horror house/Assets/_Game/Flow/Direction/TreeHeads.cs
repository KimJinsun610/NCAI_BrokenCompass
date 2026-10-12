using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 104차 배치 연출 1단계 A(인체 나무) — 나무에 박힌 얼굴들(<c>*_Head</c> 뼈 일곱)이 <b>고개만</b> 플레이어 쪽으로 돈다.
/// <see cref="HumanTreeGrowth"/>가 심을 때 붙이고, 배치 구간이 <see cref="HumanTreeGrowth.HeadsBand"/> 이상인 밤에 <see cref="Watching"/>을 켠다.
/// <list type="bullet">
/// <item>얼굴마다 지금 바라보는 방향(머리뼈 축 중 나무 정면에 가장 가까운 축 — 처음에 한 번 고름)을 기준으로 좌우 <see cref="MaxYaw"/>°·위아래 <see cref="MaxPitch"/>°까지, 얼굴마다 다른 빠르기·늦음(<see cref="MaxDelay"/>초 안)으로
/// 하나씩 돌아본다 — 한꺼번에 돌면 기계 같다.</item>
/// <item><see cref="Range"/>m 밖이거나 꺼지면 천천히 제자리로.</item>
/// <item>애니메이터(흔들림) 자세 위에 더한다. 화면 밖이라 애니메이터가 이번 프레임에 쓰지 않았으면 지난 바탕 자세 위에 더한다(쌓이지 않게 — <see cref="ProceduralIdle"/>과 같은 방식).</item>
/// </list>
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(210)]
public sealed class TreeHeads : MonoBehaviour
{
    /// <summary>좌우 최대(°).</summary>
    public const float MaxYaw = 70f;

    /// <summary>위아래 최대(°).</summary>
    public const float MaxPitch = 35f;

    /// <summary>이 거리(m) 안에서만 돌아본다.</summary>
    public const float Range = 14f;

    /// <summary>얼굴마다 돌아보기 시작하는 늦음의 최대(초).</summary>
    public const float MaxDelay = 1.8f;

    /// <summary>제자리로 돌아가는 빠르기(°/초).</summary>
    public const float ReturnSpeed = 30f;

    private sealed class Head
    {
        public Transform Bone;
        public Vector3 LocalFace;
        public float Yaw;
        public float Pitch;
        public float Speed;
        public float Delay;
        public Quaternion Base;
        public Quaternion Applied;
        public bool Has;
    }

    private readonly List<Head> _heads = new List<Head>();
    private float _watchFor;

    /// <summary>돌아볼지(배치 구간 — <see cref="HumanTreeGrowth"/>가 켠다).</summary>
    public bool Watching { get; set; }

    /// <summary>찾은 얼굴 수(검수용).</summary>
    public int Count
    {
        get { return _heads.Count; }
    }

    /// <summary>지금 가장 크게 돌린 각(°, 검수용).</summary>
    public float MaxTurn { get; private set; }

    private void Start()
    {
        foreach (Transform t in GetComponentsInChildren<Transform>(true))
        {
            if (!t.name.EndsWith("_Head")) continue;
            _heads.Add(new Head
            {
                Bone = t,
                LocalFace = FaceAxis(t, transform.forward),
                Speed = Random.Range(35f, 80f),
                Delay = Random.Range(0f, MaxDelay)
            });
        }
    }

    private void LateUpdate()
    {
        float dt = Time.deltaTime;
        Camera cam = Camera.main;
        bool watch = Watching && cam != null && InRange(cam);
        _watchFor = watch ? _watchFor + dt : 0f;
        float max = 0f;
        for (int i = 0; i < _heads.Count; i++)
        {
            Head h = _heads[i];
            if (h.Bone == null) continue;

            // 바탕 자세: 애니메이터가 이번 프레임에 썼으면 그것, 아니면 지난 바탕(더한 것이 쌓이지 않게).
            Quaternion now = h.Bone.localRotation;
            if (!h.Has || now != h.Applied) h.Base = now;
            h.Bone.localRotation = h.Base;
            h.Has = true;

            // 바탕 자세에서 그 얼굴이 보는 방향(애니메이션 흔들림을 따라간다).
            Vector3 face = h.Bone.rotation * h.LocalFace;
            Vector3 faceFlat = new Vector3(face.x, 0f, face.z);
            if (faceFlat.sqrMagnitude < 1e-4f) faceFlat = transform.forward;
            faceFlat.Normalize();
            float wantYaw = 0f, wantPitch = 0f, rate = ReturnSpeed;
            if (watch && _watchFor >= h.Delay)
            {
                rate = h.Speed;
                Vector3 to = cam.transform.position - h.Bone.position;
                Vector3 flat = new Vector3(to.x, 0f, to.z);
                if (flat.sqrMagnitude > 0.01f)
                {
                    wantYaw = Mathf.Clamp(Vector3.SignedAngle(faceFlat, flat, Vector3.up), -MaxYaw, MaxYaw);
                    float facePitch = Mathf.Asin(Mathf.Clamp(face.normalized.y, -1f, 1f)) * Mathf.Rad2Deg;
                    float toPitch = Mathf.Atan2(to.y, flat.magnitude) * Mathf.Rad2Deg;
                    wantPitch = Mathf.Clamp(facePitch - toPitch, -MaxPitch, MaxPitch);   // 아래로 숙이면 +(오른쪽 축 둘레)
                }
            }

            h.Yaw = Mathf.MoveTowards(h.Yaw, wantYaw, rate * dt);
            h.Pitch = Mathf.MoveTowards(h.Pitch, wantPitch, rate * dt);
            if (Mathf.Abs(h.Yaw) > 0.01f || Mathf.Abs(h.Pitch) > 0.01f)
            {
                Quaternion yaw = Quaternion.AngleAxis(h.Yaw, Vector3.up);
                Vector3 right = Vector3.Cross(Vector3.up, faceFlat);
                Quaternion pitch = Quaternion.AngleAxis(h.Pitch, yaw * right);
                h.Bone.rotation = pitch * yaw * h.Bone.rotation;
            }

            h.Applied = h.Bone.localRotation;
            max = Mathf.Max(max, Mathf.Abs(h.Yaw));
        }

        MaxTurn = max;
    }

    /// <summary>머리뼈 로컬 축(±x·±y·±z) 중 수평으로 나무 정면(<paramref name="front"/>)에 가장 가까운 것 — 그 얼굴이 보는 쪽으로 친다.</summary>
    private static Vector3 FaceAxis(Transform bone, Vector3 front)
    {
        Vector3[] axes = { Vector3.forward, Vector3.back, Vector3.right, Vector3.left, Vector3.up, Vector3.down };
        front.y = 0f;
        front.Normalize();
        Vector3 best = Vector3.forward;
        float bestDot = float.NegativeInfinity;
        for (int i = 0; i < axes.Length; i++)
        {
            Vector3 w = bone.rotation * axes[i];
            w.y = 0f;
            if (w.sqrMagnitude < 1e-4f) continue;
            float dot = Vector3.Dot(w.normalized, front);
            if (dot > bestDot)
            {
                bestDot = dot;
                best = axes[i];
            }
        }

        return best;
    }

    private bool InRange(Camera cam)
    {
        Vector3 d = cam.transform.position - transform.position;
        d.y = 0f;
        return d.magnitude <= Range;
    }
}

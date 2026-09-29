using System;
using UnityEngine;

/// <summary>
/// 원래 애니메이션 <b>위에</b> 뼈 회전을 덧붙여 나무를 더 격하게 흔든다. 가끔 경련하듯 한 번씩 튄다.
/// <para>
/// 뼈 회전은 부모에서 자식으로 쌓이므로, 줄기 아래는 작게 · 위쪽과 가지 끝은 크게 준다(<see cref="Bone.weight"/>).
/// <see cref="gaze"/>를 연결하면 플레이어가 바라보는 동안 흔들림이 커지고 빨라진다.
/// </para>
/// <para>
/// Animator가 매 프레임 쓰지 않는 뼈(애니메이션에 없는 뼈, 화면 밖 컬링)도 회전이 누적되지 않도록
/// 지난 프레임에 자신이 쓴 값인지 확인하고 원래 자세를 기준으로 삼는다.
/// 게임 시간 기준이라 일시정지 중에는 멈춘다.
/// </para>
/// </summary>
public class HorrorTreeSway : MonoBehaviour
{
    [Serializable]
    public class Bone
    {
        public Transform bone;
        [Tooltip("이 뼈의 흔들림 배율. 부모 회전이 쌓이므로 줄기 아래는 작게 준다.")]
        [Range(0f, 3f)] public float weight = 1f;

        [NonSerialized] public Quaternion lastBase;
        [NonSerialized] public Quaternion lastWritten;
        [NonSerialized] public bool written;
        [NonSerialized] public float seed;
        [NonSerialized] public float speed;
    }

    [Header("흔들림")]
    [Tooltip("전체 세기. 0이면 원래 애니메이션만 나온다.")]
    [Range(0f, 3f)] public float intensity = 1f;
    [Tooltip("뼈 하나의 기본 흔들림 각도(도). 뼈 weight를 곱한다.")]
    [SerializeField, Range(0f, 20f)] private float amplitude = 5f;
    [Tooltip("흔들리는 빠르기(초당 왕복 수).")]
    [SerializeField, Range(0.05f, 5f)] private float frequency = 0.9f;

    [Header("경련")]
    [Tooltip("경련 한 번의 최대 각도(도). 0이면 끔.\n뼈마다 weight를 곱해 부모에서 자식으로 쌓이므로 꼭대기는 이 값의 약 2.5배 꺾인다.")]
    [SerializeField, Range(0f, 40f)] private float twitchAngle = 6f;
    [Tooltip("경련이 이어지는 시간(초). 꺾였다가 돌아오기까지.")]
    [SerializeField, Range(0.1f, 2f)] private float twitchSeconds = 0.7f;
    [Tooltip("경련 시간 중 꺾이는 데 쓰는 비율. 작을수록 확 꺾이고 천천히 돌아온다.\n시작 · 꼭짓점 · 끝에서 속도가 0이라 어느 값이어도 뚝 끊기지 않는다.")]
    [SerializeField, Range(0.1f, 0.9f)] private float twitchAttack = 0.35f;
    [Tooltip("경련이 끝난 뒤 다음 경련까지의 간격(초). 이 범위에서 무작위. 경련은 서로 겹치지 않는다.")]
    [SerializeField] private Vector2 twitchInterval = new Vector2(2f, 5f);

    [Header("바라볼 때")]
    [Tooltip("연결하면 바라보는 동안 아래 배율만큼 격해진다. 비우면 늘 같은 세기.")]
    [SerializeField] private HorrorGazeHold gaze;
    [Tooltip("바라볼 때 흔들림 각도 배율.")]
    [SerializeField, Range(1f, 4f)] private float gazeAmplitude = 1.8f;
    [Tooltip("바라볼 때 빠르기 배율.")]
    [SerializeField, Range(1f, 4f)] private float gazeSpeed = 2f;
    [Tooltip("바라볼 때 경련이 이 배수만큼 자주 온다.")]
    [SerializeField, Range(1f, 6f)] private float gazeTwitchRate = 3f;

    [Header("뼈")]
    [Tooltip("비워 두면 이름으로 찾아 채운다(Trunk · Crown · Branch).")]
    [SerializeField] private Bone[] bones = new Bone[0];

    private float clock;
    private float twitchTimer;
    private float twitchTime = float.MaxValue;
    private Vector3 twitchAxis;

    // 이름 → 기본 배율. 부모 회전이 쌓이므로 아래쪽은 작게.
    private static readonly (string name, float weight)[] DefaultBones =
    {
        ("Trunk_Lower", 0.35f), ("Trunk_Middle", 0.5f), ("Trunk_Upper", 0.7f), ("Crown", 1f),
        ("Branch_Low_Left", 1f), ("Branch_Left_Base", 0.8f), ("Branch_Left_Tip", 1.2f),
        ("Branch_Right_Base", 0.8f), ("Branch_Right_Tip", 1.2f), ("Branch_Hanging", 1.3f),
    };

    private void Reset()
    {
        FillBones();
    }

    private void Awake()
    {
        if (bones == null || bones.Length == 0) FillBones();

        foreach (Bone b in bones)
        {
            if (b == null) continue;
            b.seed = UnityEngine.Random.value * 100f;
            b.speed = UnityEngine.Random.Range(0.85f, 1.2f);   // 뼈마다 박자를 어긋나게
        }
        twitchTimer = UnityEngine.Random.Range(twitchInterval.x, twitchInterval.y);
    }

    private void LateUpdate()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;   // 일시정지 — 마지막 자세 유지

        float g = gaze != null ? gaze.Weight : 0f;
        float amp = amplitude * intensity * Mathf.Lerp(1f, gazeAmplitude, g);
        clock += dt * Mathf.Lerp(1f, gazeSpeed, g);

        UpdateTwitch(dt * Mathf.Lerp(1f, gazeTwitchRate, g));
        float twitch = TwitchEnvelope(twitchTime / twitchSeconds, twitchAttack);
        Vector3 twitchEuler = twitchAxis * (twitchAngle * intensity * twitch);

        foreach (Bone b in bones)
        {
            if (b == null || b.bone == null) continue;

            // Animator가 이번 프레임에 안 썼으면(지난번 내가 쓴 값 그대로면) 원래 자세를 기준으로 — 누적 방지.
            Quaternion current = b.bone.localRotation;
            Quaternion basePose = b.written && current == b.lastWritten ? b.lastBase : current;

            float t = clock * frequency * b.speed;
            Vector3 sway = new Vector3(
                Wave(t, b.seed),
                Wave(t * 0.7f, b.seed + 31f) * 0.4f,   // 비틀림은 작게
                Wave(t * 1.1f, b.seed + 57f));

            Quaternion result = basePose * Quaternion.Euler(sway * (amp * b.weight) + twitchEuler * b.weight);
            b.bone.localRotation = result;

            b.lastBase = basePose;
            b.lastWritten = result;
            b.written = true;
        }
    }

    private void OnDisable()
    {
        // 원래 자세로 돌려 둔다(Animator가 없는 뼈도 제자리로).
        foreach (Bone b in bones)
        {
            if (b == null || b.bone == null || !b.written) continue;
            if (b.bone.localRotation == b.lastWritten) b.bone.localRotation = b.lastBase;
            b.written = false;
        }
    }

    private void UpdateTwitch(float dt)
    {
        if (twitchAngle <= 0f) return;

        twitchTime += dt;

        // 경련 중에는 다음 경련을 세지 않는다. 중간에 새로 시작하면 꺾여 있던 가지가 한 프레임에 제자리로 튄다.
        if (twitchTime < twitchSeconds) return;

        twitchTimer -= dt;
        if (twitchTimer > 0f) return;

        twitchTimer = UnityEngine.Random.Range(twitchInterval.x, twitchInterval.y);
        twitchTime = 0f;
        twitchAxis = UnityEngine.Random.onUnitSphere;
        twitchAxis.y *= 0.3f;   // 옆으로 꺾이는 쪽 위주
    }

    /// <summary>
    /// 경련 세기 0~1. u = 경과 비율(0~1). attack 구간에 0→1, 나머지에 1→0.
    /// <para>
    /// 양쪽 모두 SmoothStep이라 <b>시작 · 꼭짓점 · 끝에서 속도가 0</b>이다.
    /// 사인 반쪽(sin(πu))을 쓰면 시작과 끝에서 속도가 최대라, 멈춰 있다가 한 번에 튀어나가고 뚝 멈춘다(실측 0 → 247°/s).
    /// </para>
    /// </summary>
    private static float TwitchEnvelope(float u, float attack)
    {
        if (u <= 0f || u >= 1f) return 0f;
        if (u < attack) return Smooth(u / attack);
        return 1f - Smooth((u - attack) / (1f - attack));
    }

    private static float Smooth(float x)
    {
        return x * x * (3f - 2f * x);
    }

    /// <summary>-1~1. 사인만 쓰면 규칙적으로 보여 잡음을 섞는다.</summary>
    private static float Wave(float t, float seed)
    {
        float noise = Mathf.PerlinNoise(t * 1.3f + seed, seed * 0.5f) * 2f - 1f;
        return Mathf.Sin((t + seed) * Mathf.PI * 2f) * 0.6f + noise * 0.4f;
    }

    private void FillBones()
    {
        var list = new System.Collections.Generic.List<Bone>();
        foreach ((string boneName, float weight) in DefaultBones)
        {
            Transform t = FindDeep(transform, boneName);
            if (t != null) list.Add(new Bone { bone = t, weight = weight });
        }
        bones = list.ToArray();
    }

    private static Transform FindDeep(Transform parent, string target)
    {
        if (parent.name == target) return parent;
        foreach (Transform child in parent)
        {
            Transform found = FindDeep(child, target);
            if (found != null) return found;
        }
        return null;
    }
}

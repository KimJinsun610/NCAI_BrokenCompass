using UnityEngine;

/// <summary>
/// 변기 물(핏물)이 소용돌이치며 내려간다. 내려가는 동안 수면에서 물방울이 튄다.
/// <para>
/// 기본은 켜지면(<see cref="playOnStart"/>) <see cref="startDelay"/> 뒤 바로 내려간다 — 트리거 없음.
/// 다른 연출에 묶을 때는 <see cref="playOnStart"/>를 끄고 <see cref="Play"/>를 부른다(T1 물 내림 단서 연결은 Lee님 작업).
/// </para>
/// <para>
/// <b>수면 모양</b>: 변기 그릇은 내려갈수록 좁아지고 둥글어진다. 빌더가 높이마다 그릇 가장자리를 재서
/// <see cref="levels"/> × <see cref="segments"/> 표(<see cref="radii"/>)로 구워 두고, 여기서는 그 표를 보간해
/// 매 프레임 수면 메시(부채꼴 정점 segments+1개)를 다시 만든다 — 어느 높이에서도 그릇 벽에 맞는다.
/// </para>
/// 프리팹은 메뉴 「Tools/Programmer_Kim/Liquid/Build Toilet Flush」(ToiletFlushBuilder)가 만든다.
/// </summary>
[DisallowMultipleComponent]
public class ToiletFlush : MonoBehaviour
{
    [Header("재생")]
    [SerializeField] private bool playOnStart = true;
    [SerializeField, Min(0f)] private float startDelay = 0.5f;
    [Tooltip("다 내려가기까지(초).")]
    [SerializeField, Min(0.5f)] private float drainSeconds = 5.5f;
    [Tooltip("진행(0~1)에 따른 수위(0 = 가득, 1 = 다 빠짐). 처음엔 천천히, 중간에 빨리, 끝에 꿀럭.")]
    [SerializeField] private AnimationCurve drainCurve = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 0.2f), new Keyframe(0.35f, 0.18f), new Keyframe(0.85f, 0.88f), new Keyframe(1f, 1f, 0.6f, 0f));

    [Header("수면")]
    [SerializeField] private MeshFilter water;
    [Tooltip("소용돌이 무늬(수면 위 원판). 수면과 함께 내려가며 돈다.")]
    [SerializeField] private Transform swirl;
    [Tooltip("소용돌이 최고 회전(도/초). 진행에 따라 빨라진다.")]
    [SerializeField] private float swirlMaxSpeed = 540f;

    [Header("튀는 물방울")]
    [SerializeField] private ParticleSystem splash;
    [Tooltip("초당 최대 물방울 수.")]
    [SerializeField, Min(0f)] private float splashRate = 70f;
    [Tooltip("진행(0~1)에 따른 물방울 비율. 소용돌이가 셀 때 많이, 끝 무렵엔 잦아든다.")]
    [SerializeField] private AnimationCurve splashCurve = new AnimationCurve(
        new Keyframe(0f, 0f), new Keyframe(0.15f, 0.6f), new Keyframe(0.45f, 1f), new Keyframe(0.8f, 0.5f), new Keyframe(0.92f, 0f));
    [Tooltip("다 빠지는 순간 꿀럭 하며 한 번에 튀는 물방울 수.")]
    [SerializeField, Min(0)] private int finalBurst = 14;

    [Header("소리")]
    [SerializeField] private AudioSource flushSource;
    [SerializeField] private AudioClip flushClip;
    [SerializeField, Range(0f, 1f)] private float flushVolume = 1f;

    // ── 빌더가 굽는 그릇 모양 (손대지 말 것) ──
    [HideInInspector, SerializeField] private Vector2 centerXZ;
    [HideInInspector, SerializeField] private float[] levels;   // 수면 높이(로컬 y), 높은 것부터
    [HideInInspector, SerializeField] private int segments;
    [HideInInspector, SerializeField] private float[] radii;    // levels.Length × segments, 가장자리까지 거리(겹침 포함)

    private Mesh mesh;
    private Vector3[] verts;
    private float t = -1f;          // -1 = 대기, 0~1 진행, >1 끝
    private float delay;
    private float swirlAngle;
    private bool burstDone;

    /// <summary>내려가는 중인지.</summary>
    public bool IsPlaying => t >= 0f && t <= 1f;

    /// <summary>다 내려갔을 때.</summary>
    public event System.Action Finished;

    private void Awake()
    {
        BuildMesh();
        SetLevel(0f);
    }

    private void Start()
    {
        if (playOnStart)
        {
            delay = startDelay;
            t = -2f; // 지연 대기
        }
    }

    /// <summary>지금 내려간다(처음 수위부터).</summary>
    public void Play()
    {
        ResetToFull();
        t = 0f;
        swirlAngle = 0f;
        burstDone = false;

        if (splash != null)
        {
            var em = splash.emission;
            em.rateOverTimeMultiplier = 0f;
            splash.Play(true);
        }

        if (flushSource != null && flushClip != null) flushSource.PlayOneShot(flushClip, flushVolume);
    }

    /// <summary>다시 가득 찬 상태로(소리·물방울 멈춤).</summary>
    public void ResetToFull()
    {
        t = -1f;
        if (splash != null) splash.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        if (water != null) water.gameObject.SetActive(true);
        if (swirl != null) swirl.gameObject.SetActive(true);
        SetLevel(0f);
    }

    private void Update()
    {
        if (t == -2f)
        {
            delay -= Time.deltaTime;
            if (delay <= 0f) Play();
            return;
        }
        if (t < 0f || t > 1f) return;

        t += Time.deltaTime / drainSeconds;
        float p = Mathf.Clamp01(t);
        float level = Mathf.Clamp01(drainCurve.Evaluate(p));
        SetLevel(level);

        // 소용돌이: 진행할수록 빨라짐
        swirlAngle += swirlMaxSpeed * Mathf.SmoothStep(0.15f, 1f, p) * Time.deltaTime;
        if (swirl != null) swirl.localRotation = Quaternion.Euler(0f, swirlAngle, 0f);

        if (splash != null)
        {
            var em = splash.emission;
            em.rateOverTimeMultiplier = splashRate * Mathf.Max(0f, splashCurve.Evaluate(p));
        }

        if (!burstDone && p >= 0.97f)
        {
            burstDone = true;
            if (splash != null && finalBurst > 0) splash.Emit(finalBurst);
        }

        if (t >= 1f)
        {
            t = 2f;
            if (splash != null) splash.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            if (water != null) water.gameObject.SetActive(false);
            if (swirl != null) swirl.gameObject.SetActive(false);
            Finished?.Invoke();
        }
    }

    // ───────────────────────── 수면 ─────────────────────────

    private void BuildMesh()
    {
        if (water == null || segments < 3 || levels == null || levels.Length == 0) return;

        mesh = new Mesh { name = "ToiletFlushWater (runtime)" };
        mesh.MarkDynamic();
        verts = new Vector3[segments + 1];
        var normals = new Vector3[segments + 1];
        var tris = new int[segments * 3];
        for (int i = 0; i < normals.Length; i++) normals[i] = Vector3.up;
        for (int i = 0; i < segments; i++)
        {
            tris[i * 3] = 0;
            tris[i * 3 + 1] = i + 1;
            tris[i * 3 + 2] = (i + 1) % segments + 1;
        }
        mesh.vertices = verts;
        mesh.normals = normals;
        mesh.triangles = tris;
        water.sharedMesh = mesh;
    }

    /// <summary>수위 0(가득) ~ 1(다 빠짐)에 맞춰 수면 메시·소용돌이·물방울 자리를 옮긴다.</summary>
    private void SetLevel(float level)
    {
        if (mesh == null) return;

        float f = level * (levels.Length - 1);
        int a = Mathf.Clamp(Mathf.FloorToInt(f), 0, levels.Length - 1);
        int b = Mathf.Min(a + 1, levels.Length - 1);
        float k = f - a;
        float y = Mathf.Lerp(levels[a], levels[b], k);

        var c = new Vector3(centerXZ.x, y, centerXZ.y);
        verts[0] = c;
        float minR = float.MaxValue;
        for (int i = 0; i < segments; i++)
        {
            float ang = i * Mathf.PI * 2f / segments;
            float r = Mathf.Lerp(radii[a * segments + i], radii[b * segments + i], k);
            minR = Mathf.Min(minR, r);
            verts[i + 1] = c + new Vector3(Mathf.Sin(ang), 0f, Mathf.Cos(ang)) * r;
        }
        mesh.vertices = verts;
        mesh.RecalculateBounds();

        if (swirl != null)
        {
            swirl.localPosition = c + Vector3.up * 0.0015f;
            float d = minR * 1.9f; // 원판이 수면 안에 들도록 가장 좁은 쪽 기준
            swirl.localScale = new Vector3(d, 1f, d);
        }

        if (splash != null)
        {
            splash.transform.localPosition = c;
            var shape = splash.shape;
            shape.radius = minR * 0.6f;
        }
    }

#if UNITY_EDITOR
    /// <summary>빌더 전용: 높이별 그릇 가장자리 표를 넣는다.</summary>
    public void BakeBowl(Vector2 center, float[] levelYs, int segmentCount, float[] edgeRadii)
    {
        centerXZ = center;
        levels = levelYs;
        segments = segmentCount;
        radii = edgeRadii;
    }
#endif
}

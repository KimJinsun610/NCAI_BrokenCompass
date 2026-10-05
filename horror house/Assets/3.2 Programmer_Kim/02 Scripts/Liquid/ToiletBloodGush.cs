using UnityEngine;

/// <summary>
/// 플레이어가 변기 물에 가까이(수평 <see cref="triggerRadius"/>m 안) 오면 물에서 핏물이 뿜어져 나온다.
/// <para>
/// 거리는 플레이어 발밑과 수면 가운데(<see cref="center"/>)의 <b>수평</b> 거리로 잰다 — 높이(앉음·계단)와 상관없다.
/// 기본은 한 번만. <see cref="playOnce"/>를 끄면 멀어졌다가(반경 + <see cref="rearmMargin"/>) 다시 오면,
/// <see cref="cooldown"/>이 지난 뒤 또 뿜는다.
/// </para>
/// 다른 조건(지침 결과·HorrorEvent 등)으로 부르고 싶으면 <see cref="Play"/>만 부르면 된다.
/// 프리팹은 메뉴 「Tools/Programmer_Kim/Liquid/Build Toilet Water」(ToiletWaterBuilder)가 만든다.
/// </summary>
[DisallowMultipleComponent]
public class ToiletBloodGush : MonoBehaviour
{
    [Header("거리")]
    [Tooltip("수면 가운데. 거리는 여기와 플레이어 발밑의 수평 거리.")]
    [SerializeField] private Transform center;
    [SerializeField, Min(0.1f)] private float triggerRadius = 0.8f;

    [Header("반복")]
    [SerializeField] private bool playOnce = true;
    [Tooltip("playOnce가 꺼져 있을 때: 반경보다 이만큼 더 멀어져야 다시 걸 수 있다(경계에서 떨림 방지).")]
    [SerializeField, Min(0f)] private float rearmMargin = 0.4f;
    [SerializeField, Min(0f)] private float cooldown = 6f;

    [Header("연출")]
    [Tooltip("뿜어지는 파티클(자식 포함 함께 재생).")]
    [SerializeField] private ParticleSystem gush;
    [SerializeField] private AudioSource gushSource;
    [SerializeField] private AudioClip gushClip;
    [SerializeField, Range(0f, 1f)] private float gushVolume = 1f;

    private Transform player;
    private bool armed = true;
    private float lastPlayTime = float.NegativeInfinity;

    /// <summary>한 번이라도 뿜었는지.</summary>
    public bool HasPlayed { get; private set; }

    /// <summary>뿜을 때마다.</summary>
    public event System.Action Gushed;

    private void Start()
    {
        if (center == null) center = transform;
        FindPlayer();
    }

    private void Update()
    {
        if (playOnce && HasPlayed) return;
        if (player == null && !FindPlayer()) return;

        float d = HorizontalDistance(player.position, center.position);

        if (!armed)
        {
            if (d > triggerRadius + rearmMargin) armed = true;
            return;
        }

        if (d < triggerRadius && Time.time - lastPlayTime >= cooldown)
        {
            Play();
        }
    }

    /// <summary>지금 뿜는다(거리 조건 없이).</summary>
    public void Play()
    {
        armed = false;
        HasPlayed = true;
        lastPlayTime = Time.time;

        if (gush != null)
        {
            gush.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            gush.Play(true);
        }

        if (gushSource != null && gushClip != null)
        {
            gushSource.PlayOneShot(gushClip, gushVolume);
        }

        Gushed?.Invoke();
    }

    private bool FindPlayer()
    {
        // 플레이어 몸(FPController)이 발밑 기준이다. 없으면 메인 카메라라도 쓴다(시험 씬).
        var fp = FindAnyObjectByType<FPController>();
        if (fp != null) player = fp.transform;
        else if (Camera.main != null) player = Camera.main.transform;
        return player != null;
    }

    private static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Transform c = center != null ? center : transform;
        Gizmos.color = new Color(0.8f, 0.1f, 0.1f);
        const int seg = 40;
        Vector3 prev = c.position + Vector3.right * triggerRadius;
        for (int i = 1; i <= seg; i++)
        {
            float a = i * Mathf.PI * 2f / seg;
            Vector3 p = c.position + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * triggerRadius;
            Gizmos.DrawLine(prev, p);
            prev = p;
        }
    }
#endif
}

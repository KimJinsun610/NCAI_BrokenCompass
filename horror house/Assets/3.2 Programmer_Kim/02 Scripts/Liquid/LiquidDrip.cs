using UnityEngine;

/// <summary>
/// 수도꼭지 끝에서 액체가 한 방울씩 떨어지는 연출.
/// <para>
/// 한 방울 = ① 꼭지 끝에 맺힘(점점 커짐) → ② 떨어짐(중력) → ③ 닿는 순간 튐 + 방울 소리.
/// 소리는 <b>닿는 프레임에</b> 그 자리에서 낸다 — 루프 소리를 깔아 두면 눈에 보이는 방울과 박자가 어긋난다.
/// </para>
/// <para>
/// 방울은 파티클이 아니라 작은 구 하나를 스크립트로 움직인다. 한 번에 하나만 떨어지므로
/// 닿는 시점을 정확히 알 수 있고, 프레임이 튀어도 소리가 늦지 않는다(이번 프레임에 바닥을 지났으면 그 자리에서 터뜨림).
/// </para>
/// 프리팹은 메뉴 「Tools/Programmer_Kim/Liquid/Build Lab Sink Drip」(LiquidDripBuilder)가 만든다.
/// </summary>
[DisallowMultipleComponent]
public class LiquidDrip : MonoBehaviour
{
    [Header("자리")]
    [Tooltip("방울이 맺히는 곳(수도꼭지 끝 바로 아래).")]
    [SerializeField] private Transform spout;
    [Tooltip("아래로 쏘아 닿는 면을 찾는다. 못 찾으면 fallbackFallDistance만큼 떨어진 곳에서 터진다.")]
    [SerializeField] private LayerMask landingMask = ~0;
    [SerializeField, Min(0.05f)] private float maxFallDistance = 3f;
    [Tooltip("아래에 충돌체가 없을 때 떨어지는 거리(m). 빌더가 모델을 재서 넣는다.")]
    [SerializeField, Min(0.01f)] private float fallbackFallDistance = 0.43f;

    [Header("방울")]
    [Tooltip("떨어지는 방울(작은 구). 꺼진 채로 둔다 — 스크립트가 켜고 끈다.")]
    [SerializeField] private Transform droplet;
    [Tooltip("다 맺혔을 때의 크기(지름, m).")]
    [SerializeField, Min(0.001f)] private float dropletSize = 0.012f;
    [Tooltip("떨어지는 동안 세로로 늘어나는 정도.")]
    [SerializeField, Range(1f, 2.5f)] private float fallStretch = 1.6f;
    [SerializeField, Min(0.1f)] private float gravity = 9.81f;

    [Header("박자")]
    [Tooltip("방울과 방울 사이(초) — 이 범위에서 무작위.")]
    [SerializeField] private Vector2 interval = new Vector2(1.4f, 3.2f);
    [Tooltip("꼭지 끝에 맺히는 시간(초).")]
    [SerializeField] private Vector2 swellTime = new Vector2(0.45f, 0.9f);
    [Tooltip("곧바로 한 방울 더 떨어질 확률 — 메트로놈처럼 들리지 않게.")]
    [SerializeField, Range(0f, 1f)] private float doubleDripChance = 0.15f;
    [SerializeField] private bool playOnEnable = true;

    [Header("닿을 때")]
    [Tooltip("닿는 자리에서 재생할 튐 파티클(자식으로 둔 것).")]
    [SerializeField] private ParticleSystem splash;
    [Tooltip("한 방울짜리 소리들 — 직전과 다른 것을 고른다.")]
    [SerializeField] private AudioClip[] dropClips;
    [Tooltip("3D 소리를 낼 소스. 닿는 자리로 옮겨서 PlayOneShot.")]
    [SerializeField] private AudioSource dropSource;
    [SerializeField] private Vector2 volume = new Vector2(0.35f, 0.55f);
    [SerializeField] private Vector2 pitch = new Vector2(0.92f, 1.08f);

    private enum Phase { Waiting, Swelling, Falling }

    private Phase phase;
    private float timer;
    private float phaseLength;
    private float fallSpeed;
    private float landY;
    private Vector3 landPoint;
    private int lastClip = -1;
    private bool dripping;

    /// <summary>지금 떨어지고 있는가(맺힘·낙하 포함).</summary>
    public bool IsDripping => dripping;

    /// <summary>한 방울이 닿을 때마다(위치). 바닥 얼룩·다른 연출을 붙일 자리.</summary>
    public event System.Action<Vector3> Landed;

    private void OnEnable()
    {
        HideDroplet();
        if (playOnEnable) StartDripping();
    }

    private void OnDisable()
    {
        dripping = false;
        HideDroplet();
    }

    public void StartDripping()
    {
        if (spout == null || droplet == null)
        {
            Debug.LogWarning($"[LiquidDrip] {name}: spout 또는 droplet이 비어 있습니다.", this);
            return;
        }

        dripping = true;
        // 켜자마자 일제히 떨어지지 않게 첫 방울은 간격 안 아무 때나
        BeginWait(Random.Range(0f, interval.y));
    }

    /// <summary>멈춘다. 떨어지던 방울은 마저 떨어진다.</summary>
    public void StopDripping()
    {
        dripping = false;
        if (phase != Phase.Falling)
        {
            HideDroplet();
            phase = Phase.Waiting;
        }
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        switch (phase)
        {
            case Phase.Waiting:
                if (!dripping) return;
                timer += dt;
                if (timer >= phaseLength) BeginSwell();
                break;

            case Phase.Swelling:
                timer += dt;
                float k = Mathf.Clamp01(timer / phaseLength);
                // 처음엔 빨리, 다 맺힐 즈음 천천히 — 물방울이 무게를 버티는 느낌
                float s = dropletSize * Mathf.Sqrt(k);
                droplet.position = spout.position + Vector3.down * (s * 0.5f);
                droplet.localScale = new Vector3(s, s * (1f + 0.25f * k), s);
                if (k >= 1f) BeginFall();
                break;

            case Phase.Falling:
                fallSpeed += gravity * dt;
                Vector3 p = droplet.position + Vector3.down * (fallSpeed * dt);
                if (p.y <= landY)
                {
                    Land();
                    break;
                }
                droplet.position = p;
                break;
        }
    }

    private void BeginWait(float seconds)
    {
        phase = Phase.Waiting;
        timer = 0f;
        phaseLength = seconds;
    }

    private void BeginSwell()
    {
        phase = Phase.Swelling;
        timer = 0f;
        phaseLength = Random.Range(swellTime.x, swellTime.y);
        droplet.localScale = Vector3.zero;
        droplet.position = spout.position;
        droplet.gameObject.SetActive(true);
    }

    private void BeginFall()
    {
        phase = Phase.Falling;
        fallSpeed = 0f;
        droplet.localScale = new Vector3(dropletSize, dropletSize * fallStretch, dropletSize);

        // 닿을 곳은 떨어지기 시작할 때 한 번 잰다 — 그 사이 물건이 놓여도 따라간다
        Vector3 from = spout.position;
        if (Physics.Raycast(from, Vector3.down, out RaycastHit hit, maxFallDistance, landingMask, QueryTriggerInteraction.Ignore))
        {
            landPoint = hit.point;
        }
        else
        {
            landPoint = from + Vector3.down * fallbackFallDistance;
        }
        landY = landPoint.y + dropletSize * 0.5f;
    }

    private void Land()
    {
        HideDroplet();

        if (splash != null)
        {
            splash.transform.position = landPoint;
            splash.Play(true);
        }

        PlayDropSound(landPoint);
        Landed?.Invoke(landPoint);

        if (!dripping)
        {
            phase = Phase.Waiting;
            return;
        }

        bool again = Random.value < doubleDripChance;
        BeginWait(again ? Random.Range(0.05f, 0.25f) : Random.Range(interval.x, interval.y));
    }

    private void PlayDropSound(Vector3 at)
    {
        if (dropSource == null || dropClips == null || dropClips.Length == 0) return;

        int i = Random.Range(0, dropClips.Length);
        if (dropClips.Length > 1 && i == lastClip) i = (i + 1 + Random.Range(0, dropClips.Length - 1)) % dropClips.Length;
        lastClip = i;

        AudioClip clip = dropClips[i];
        if (clip == null) return;

        dropSource.transform.position = at;
        dropSource.pitch = Random.Range(pitch.x, pitch.y);
        dropSource.PlayOneShot(clip, Random.Range(volume.x, volume.y));
    }

    private void HideDroplet()
    {
        if (droplet != null) droplet.gameObject.SetActive(false);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (spout == null) return;
        Gizmos.color = new Color(0.2f, 0.45f, 0.8f);
        Gizmos.DrawWireSphere(spout.position, dropletSize * 0.5f);
        Gizmos.DrawLine(spout.position, spout.position + Vector3.down * fallbackFallDistance);
    }
#endif
}

using UnityEngine;

/// <summary>
/// 손에 든 물건이 살아 있어 보이게 하는 미세한 흔들림.
/// <see cref="PlayerTablet"/>이 정한 기준 자세 위에 얹는 방식이라, 들고 내리는 연출과 충돌하지 않는다.
///
/// 세 가지를 더한다.
///   호흡 — 느린 상하 움직임. 가만히 서 있어도 멈춰 있지 않게 한다.
///   시선 지연 — 고개를 돌리면 손이 조금 늦게 따라온다. 3D 물건을 들고 있다는 느낌이 가장 크게 살아나는 부분.
///   걸음 흔들림 — 움직일 때 좌우·상하로 8자를 그린다.
///
/// 전부 인스펙터에서 따로 끄고 켤 수 있고, 세기도 조절할 수 있다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerTablet))]
public class ViewmodelSway : MonoBehaviour
{
    [Header("호흡")]
    public bool breathing = true;
    [Tooltip("한 번 들이쉬고 내쉬는 데 걸리는 시간(초).")]
    public float breathSeconds = 4.2f;
    [Tooltip("위아래로 움직이는 거리(m). 1cm만 넘어가도 과해 보인다.")]
    public float breathPosition = 0.004f;
    [Tooltip("같이 기울어지는 각도.")]
    public float breathRotation = 0.5f;

    [Header("시선 지연")]
    public bool lookSway = true;
    [Tooltip("고개를 돌릴 때 손이 밀리는 정도(m).")]
    public float lookPosition = 0.012f;
    [Tooltip("고개를 돌릴 때 손이 기울는 정도(도).")]
    public float lookRotation = 4f;
    [Tooltip("클수록 빨리 따라붙는다. 작을수록 더 늘어진다.")]
    public float lookFollow = 6f;
    [Tooltip("아무리 빨리 돌려도 이 값을 넘지 않는다.")]
    public float lookClamp = 1.5f;
    [Tooltip("고개 각속도(도/초)를 이 비율로 줄여서 쓴다. 키우면 살짝만 돌려도 크게 밀린다.")]
    public float lookSpeedScale = 0.012f;
    [Tooltip("입력 자체를 고르는 정도. 작을수록 더 뭉근하게 반응한다.")]
    public float lookInputSmooth = 10f;

    [Header("걸음 흔들림")]
    public bool walkBob = true;
    [Tooltip("이 속도(m/s)에서 흔들림이 최대가 된다.")]
    public float walkFullSpeed = 3f;
    public float walkPosition = 0.008f;
    public float walkRotation = 1.2f;
    [Tooltip("걸음 주기. 클수록 빨리 흔들린다.")]
    public float walkFrequency = 6f;
    [Tooltip("걷기 시작할 때 흔들림이 올라오는 시간(초).")]
    public float walkBlendIn = 0.18f;
    [Tooltip("멈출 때 흔들림이 잦아드는 시간(초). 켜질 때보다 길어야 자연스럽다.")]
    public float walkBlendOut = 0.35f;

    [Header("공통")]
    [Tooltip("태블릿을 내리고 있을 때도 흔들지 여부. 꺼 두면 올린 만큼만 흔들린다.")]
    public bool swayWhileClosed = false;
    [Tooltip("태블릿을 올리고 내리는 도중에 흔들림이 얼마나 부드럽게 붙을지. 클수록 더 뭉근하다.")]
    [Range(0f, 1f)] public float openBlendCurve = 0.6f;

    private PlayerTablet _tablet;
    private Transform _cameraTransform;

    private Vector2 _lookOffset;      // 시선 지연 누적값 (x = 좌우, y = 상하)
    private Vector2 _lookRaw;         // 고른 입력값
    private Vector2 _lookVelocity;    // SmoothDamp용
    private Vector3 _lastCameraPosition;
    private float _walkPhase;
    private float _walkAmount;        // 서서히 켜지고 꺼지는 걸음 세기
    private float _walkVelocity;
    private float _smoothedSpeed;
    private float _noiseSeed;

    private void Awake()
    {
        _tablet = GetComponent<PlayerTablet>();
        _noiseSeed = Random.value * 100f;
    }

    private void Start()
    {
        _cameraTransform = transform.parent != null ? transform.parent : (Camera.main != null ? Camera.main.transform : null);
        if (_cameraTransform != null)
        {
            _lastCameraPosition = _cameraTransform.position;
            // 첫 프레임에 "0도에서 갑자기 돌아왔다"고 계산되어 손이 튀는 것을 막는다.
            _lastLookEuler = _cameraTransform.eulerAngles;
        }
    }

    // PlayerTablet이 Update에서 기준 자세를 정한 뒤에 얹어야 해서 LateUpdate를 쓴다.
    private void LateUpdate()
    {
        if (_cameraTransform == null) return;

        float dt = Time.unscaledDeltaTime;
        if (dt <= 0f) return;

        // 올라오는 동안 흔들림이 선형으로 끼어들면 붙는 순간이 느껴진다. 곡선을 태워 부드럽게 섞는다.
        float open = _tablet.OpenAmount;
        float eased = Mathf.SmoothStep(0f, 1f, open);
        float strength = swayWhileClosed ? 1f : Mathf.Lerp(open, eased, openBlendCurve);

        Vector3 posOffset = Vector3.zero;
        Vector3 rotOffset = Vector3.zero;

        if (breathing) AddBreathing(ref posOffset, ref rotOffset);
        if (lookSway) AddLookSway(dt, ref posOffset, ref rotOffset);
        if (walkBob) AddWalkBob(dt, ref posOffset, ref rotOffset);

        transform.localPosition = _tablet.PosePosition + posOffset * strength;
        transform.localRotation = _tablet.PoseRotation * Quaternion.Euler(rotOffset * strength);
    }

    /// <summary>느린 사인파에 약간의 노이즈를 섞는다. 순수한 사인파만 쓰면 기계처럼 보인다.</summary>
    private void AddBreathing(ref Vector3 posOffset, ref Vector3 rotOffset)
    {
        float t = Time.unscaledTime;
        float phase = breathSeconds > 0.01f ? t / breathSeconds * Mathf.PI * 2f : 0f;
        float wave = Mathf.Sin(phase);
        float drift = Mathf.PerlinNoise(_noiseSeed, t * 0.15f) * 2f - 1f;   // -1 ~ 1

        posOffset.y += wave * breathPosition;
        posOffset.x += drift * breathPosition * 0.4f;
        rotOffset.x += wave * breathRotation;
        rotOffset.z += drift * breathRotation * 0.6f;
    }

    /// <summary>고개 회전 속도만큼 반대쪽으로 밀었다가 천천히 제자리로 돌아온다.</summary>
    private void AddLookSway(float dt, ref Vector3 posOffset, ref Vector3 rotOffset)
    {
        // 마우스 입력이 아니라 카메라가 실제로 돈 각도를 쓴다. 입력 방식이 바뀌어도 그대로 동작한다.
        float yawDelta = Mathf.DeltaAngle(_lastLookEuler.y, _cameraTransform.eulerAngles.y);
        float pitchDelta = Mathf.DeltaAngle(_lastLookEuler.x, _cameraTransform.eulerAngles.x);
        _lastLookEuler = _cameraTransform.eulerAngles;

        // 프레임당 각도를 그대로 쓰면 프레임레이트에 따라 세기가 달라지고 값이 튄다.
        // 초당 각속도로 바꾼 뒤 한 번 더 완만하게 고른다.
        Vector2 raw = new Vector2(-yawDelta, -pitchDelta) / Mathf.Max(dt, 0.0001f) * lookSpeedScale;
        raw.x = Mathf.Clamp(raw.x, -lookClamp, lookClamp);
        raw.y = Mathf.Clamp(raw.y, -lookClamp, lookClamp);
        _lookRaw = Vector2.Lerp(_lookRaw, raw, 1f - Mathf.Exp(-lookInputSmooth * dt));

        // 목표를 향해 스프링처럼 다가간다. 감속이 들어가서 멈출 때 딱 끊기지 않는다.
        _lookOffset.x = Mathf.SmoothDamp(_lookOffset.x, _lookRaw.x, ref _lookVelocity.x, 1f / Mathf.Max(0.01f, lookFollow), Mathf.Infinity, dt);
        _lookOffset.y = Mathf.SmoothDamp(_lookOffset.y, _lookRaw.y, ref _lookVelocity.y, 1f / Mathf.Max(0.01f, lookFollow), Mathf.Infinity, dt);

        posOffset.x += _lookOffset.x * lookPosition;
        posOffset.y += _lookOffset.y * lookPosition;
        rotOffset.y += -_lookOffset.x * lookRotation;
        rotOffset.x += -_lookOffset.y * lookRotation;
        rotOffset.z += _lookOffset.x * lookRotation * 0.6f;
    }

    private Vector3 _lastLookEuler;

    /// <summary>움직이는 속도에 맞춰 8자를 그린다. 걷기 시작하고 멈출 때 서서히 켜지고 꺼진다.</summary>
    private void AddWalkBob(float dt, ref Vector3 posOffset, ref Vector3 rotOffset)
    {
        Vector3 now = _cameraTransform.position;
        float speed = (now - _lastCameraPosition).magnitude / dt;
        _lastCameraPosition = now;

        // 한 프레임 이동량은 들쭉날쭉하다. 먼저 속도를 고른 뒤 세기를 정한다.
        _smoothedSpeed = Mathf.Lerp(_smoothedSpeed, speed, 1f - Mathf.Exp(-12f * dt));

        float targetAmount = walkFullSpeed > 0.01f ? Mathf.Clamp01(_smoothedSpeed / walkFullSpeed) : 0f;
        targetAmount = Mathf.SmoothStep(0f, 1f, targetAmount);
        // 걸음을 멈춰도 바로 끊기지 않고 잦아들게 한다(멈출 때가 켜질 때보다 조금 느리다).
        float blendTime = targetAmount > _walkAmount ? walkBlendIn : walkBlendOut;
        _walkAmount = Mathf.SmoothDamp(_walkAmount, targetAmount, ref _walkVelocity, Mathf.Max(0.02f, blendTime), Mathf.Infinity, dt);

        // 위상은 항상 흐르게 둔다. 멈췄다고 위상을 멈추면 다시 걸을 때 엉뚱한 지점에서 시작해 툭 튄다.
        _walkPhase += dt * walkFrequency * Mathf.Lerp(0.35f, 1f, _walkAmount);

        posOffset.x += Mathf.Sin(_walkPhase) * walkPosition * _walkAmount;
        posOffset.y += Mathf.Sin(_walkPhase * 2f) * walkPosition * 0.5f * _walkAmount;   // 2배 주기라 8자가 된다
        rotOffset.z += Mathf.Sin(_walkPhase) * walkRotation * _walkAmount;
    }
}

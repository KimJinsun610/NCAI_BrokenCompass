using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 102차(민: 「도서관에서 마우스 강제이동 현상 버그」) — 끊김 프레임에 시점이 제멋대로 휙 도는 것을 막는다.
/// <list type="bullet">
/// <item><b>까닭</b>: 김진선님 <c>FPController</c>의 시점은 「마우스 이동 × <c>Time.deltaTime</c> × 감도」다. 마우스 이동(<c>Input.GetAxis</c>)은 이미 그 프레임 동안 쌓인 양이라
/// 한 프레임이 길어지면(도서관처럼 무거운 곳의 끊김) 쌓인 이동에 긴 프레임 시간이 한 번 더 곱해져, 손은 조금 움직였는데 화면은 크게 돌아간다(프레임 시간의 제곱).</item>
/// <item><b>막기</b>: <c>FPController</c>는 고치지 않는다. 그 Update 바로 앞(실행 순서 -200)에서 이번 프레임 시간이 <see cref="MaxLookDelta"/>를 넘으면
/// 감도를 「상한 / 프레임 시간」배로 잠깐 줄이고, 뒤(LateUpdate)에서 되돌린다 — 상한 이하의 보통 프레임에는 아무것도 바꾸지 않는다.</item>
/// <item>감도 설정(<see cref="PlayerOptions"/>)이 처음 보는 조작기를 줄인 값으로 기억하지 않게, 줄이기 전에 먼저 등록시킨다.</item>
/// </list>
/// 104차(사용자: 「빌드하고 테스트해봤는데, 마우스에 뭔가 뚝 뚝 끊기면서 확 꺾이는 현상이 조금씩 있어」): 빌드는 프레임 제한이 없어(수직 동기 0 · 목표 프레임 −1)
/// 보통 프레임이 수 ms라, 20~50ms짜리 작은 끊김도 보통의 몇 배라 제곱으로 커져 휙 꺾였다(1/20초 상한은 에디터 기준이라 걸리지 않았다).
/// → 상한 대신 <b>최근 보통 프레임 시간</b>(<see cref="Steady"/>, 끊김은 거의 반영하지 않는 느린 평균)을 기준으로 매 프레임 감도를 「보통 ÷ 이번」배로 —
/// 꾸준한 프레임에서는 지금과 같은 손맛, 끊긴 프레임에는 쌓인 마우스 이동만큼만 돈다(프레임 시간 곱이 상쇄). 오래 이어지는 느려짐에는 천천히 따라간다.
/// 근무 씬에 스스로 선다.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-200)]
public sealed class LookSpikeGuard : MonoBehaviour
{
    /// <summary>시점 계산에 쓰는 프레임 시간의 상한(초) — 20fps 한 프레임. 104차: 보통 프레임 시간(<see cref="Steady"/>)도 이 위로는 올리지 않는다.</summary>
    public const float MaxLookDelta = 1f / 20f;

    /// <summary>104차: 보통 프레임 시간의 하한(초).</summary>
    public const float MinLookDelta = 1f / 500f;

    /// <summary>104차: 보통 프레임보다 이만큼(배) 넘게 길면 끊김으로 보고 평균에 거의 넣지 않는다.</summary>
    public const float SpikeRatio = 1.5f;

    /// <summary>104차: 감도 배수 범위.</summary>
    public const float MinScale = 0.03f;
    public const float MaxScale = 3f;

    private FPController _player;
    private Vector2 _saved;
    private Vector2 _applied;
    private bool _scaled;
    private float _steady;

    /// <summary>지금 근무 씬의 것. 없으면 null.</summary>
    public static LookSpikeGuard Active { get; private set; }

    /// <summary>줄인 프레임 수(검수용).</summary>
    public int ScaledFrames { get; private set; }

    /// <summary>104차: 지금 기준으로 삼는 보통 프레임 시간(초, 검수용).</summary>
    public float Steady
    {
        get { return _steady; }
    }

    /// <summary>이번 프레임 시간에 곱할 감도 배수(1 = 그대로) — 104차: 보통 프레임 시간 ÷ 이번 프레임 시간.</summary>
    public static float ScaleFor(float deltaTime, float steady)
    {
        if (deltaTime <= 0f || steady <= 0f) return 1f;
        return Mathf.Clamp(steady / deltaTime, MinScale, MaxScale);
    }

    /// <summary>104차: 보통 프레임 시간 갱신 — 보통 프레임은 빠르게(8%), 끊김(<see cref="SpikeRatio"/>배 넘게 긴 것)은 조금(2%)만 따라간다(한 번의 끊김은 거의 안 바뀌고, 이어지는 느려짐에는 1~2초에 따라붙음).</summary>
    public static float NextSteady(float steady, float deltaTime)
    {
        if (deltaTime <= 0f) return steady;
        float dt = Mathf.Clamp(deltaTime, MinLookDelta, MaxLookDelta);
        if (steady <= 0f) return dt;
        float rate = deltaTime > steady * SpikeRatio ? 0.02f : deltaTime < steady ? 0.25f : 0.08f;   // 빨라질 때는 빨리(씬 첫 긴 프레임에서 곧 내려옴)
        return Mathf.Clamp(Mathf.Lerp(steady, dt, rate), MinLookDelta, MaxLookDelta);
    }

    // ── 자동 설치 ─────────────────────────────────────────────

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Install()
    {
        Active = null;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InstallForFirstScene()
    {
        EnsureFor(SceneManager.GetActiveScene());
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureFor(scene);
    }

    private static void EnsureFor(Scene scene)
    {
        if (!FlowAutoInstall.IsDutyScene(scene) || FlowAutoInstall.Exists<LookSpikeGuard>(scene)) return;
        FlowAutoInstall.CreateHost<LookSpikeGuard>(scene, "LookSpikeGuard (auto)");
    }

    private void OnEnable()
    {
        Active = this;
    }

    private void OnDisable()
    {
        Restore();
        if (Active == this) Active = null;
    }

    // ── 매 프레임 ─────────────────────────────────────────────

    private void Update()
    {
        Restore();   // 지난 프레임에 못 되돌린 것이 있으면(LateUpdate를 건너뛴 경우) 먼저
        float dt = Time.deltaTime;
        if (dt <= 0f) return;   // 일시정지(시점도 멈춤)
        if (_steady <= 0f) _steady = Mathf.Clamp(dt, MinLookDelta, 1f / 30f);   // 씬을 막 연 첫 프레임은 길다 — 30fps 프레임 이하로 시작
        float k = ScaleFor(dt, _steady);
        _steady = NextSteady(_steady, dt);
        if (Mathf.Abs(k - 1f) < 0.01f) return;
        if (_player == null) _player = FindAnyObjectByType<FPController>();
        if (_player == null || !_player.enabled) return;
        PlayerOptions.ApplyMouseToNew();   // 감도 설정이 이 조작기의 기준을 먼저 기억하게(줄인 값을 기준으로 삼지 않게)
        _saved = _player.mouseSensitivity;
        _applied = _saved * k;
        _player.mouseSensitivity = _applied;
        _scaled = true;
        if (k < 1f) ScaledFrames++;
    }

    private void LateUpdate()
    {
        Restore();
    }

    private void Restore()
    {
        if (!_scaled) return;
        _scaled = false;
        if (_player == null) return;
        if (_player.mouseSensitivity == _applied) _player.mouseSensitivity = _saved;   // 그사이 감도 설정이 바꿨으면 그 값을 둔다
    }
}

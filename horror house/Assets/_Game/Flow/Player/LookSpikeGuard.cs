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
/// 근무 씬에 스스로 선다.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-200)]
public sealed class LookSpikeGuard : MonoBehaviour
{
    /// <summary>시점 계산에 쓰는 프레임 시간의 상한(초) — 20fps 한 프레임. 에디터의 보통 프레임(30ms 안팎)은 건드리지 않고 끊김(50ms 넘게)만 깎는다.</summary>
    public const float MaxLookDelta = 1f / 20f;

    private FPController _player;
    private Vector2 _saved;
    private Vector2 _applied;
    private bool _scaled;

    /// <summary>지금 근무 씬의 것. 없으면 null.</summary>
    public static LookSpikeGuard Active { get; private set; }

    /// <summary>줄인 프레임 수(검수용).</summary>
    public int ScaledFrames { get; private set; }

    /// <summary>이번 프레임 시간에 곱할 감도 배수(1 = 그대로).</summary>
    public static float ScaleFor(float deltaTime)
    {
        return deltaTime > MaxLookDelta ? MaxLookDelta / deltaTime : 1f;
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
        float k = ScaleFor(Time.deltaTime);
        if (k >= 1f) return;
        if (_player == null) _player = FindAnyObjectByType<FPController>();
        if (_player == null || !_player.enabled) return;
        PlayerOptions.ApplyMouseToNew();   // 감도 설정이 이 조작기의 기준을 먼저 기억하게(줄인 값을 기준으로 삼지 않게)
        _saved = _player.mouseSensitivity;
        _applied = _saved * k;
        _player.mouseSensitivity = _applied;
        _scaled = true;
        ScaledFrames++;
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

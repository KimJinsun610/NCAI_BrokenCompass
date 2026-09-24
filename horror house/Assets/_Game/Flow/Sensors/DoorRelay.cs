using NightDuty;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 문 발신기. 문 하나에 붙어 <see cref="SignalKind.DoorCommandAccepted"/>·
/// <see cref="SignalKind.DoorCloseCompleted"/>·<see cref="SignalKind.DoorAutoOpenObserved"/>를 보낸다.
///
/// <para><b>벤더 <c>DoorScript</c>를 참조하지 않는 이유.</b> <c>Assets/NOT_Lonely/</c>는 asmdef가 없어
/// <c>Assembly-CSharp</c>에 들어가고, 폴더 자체가 gitignore 대상이다(CLAUDE.md §5.1-2).
/// 그 타입을 이름으로 참조하면 패키지를 받지 않은 팀원의 빌드가 깨지고, 나중에 <c>_Game</c>에 자체 문 컨트롤러를 만들면
/// 이 파일을 통째로 다시 써야 한다. 그래서 이 발신기는 <b>같은 오브젝트의 <c>UnityEngine.Animation</c>만 관찰</b>한다.
/// <c>Animation</c>은 엔진 타입이므로 어떤 문 구현이 와도 그대로 쓸 수 있고, 자체 컨트롤러로 바꿀 때는
/// <see cref="ReportMoveStarted"/>·<see cref="ReportCloseCompleted"/>를 직접 부르면 된다.</para>
///
/// <para><b>씬에서 확인한 벤더 문의 동작.</b> 문마다 열기 클립이 하나뿐이고(<c>DoorNarrow_open</c> 등),
/// 닫기는 같은 클립을 <b>음수 speed</b>로 되감아 재생한다. 그래서 「재생 중인 상태의 speed 부호」가 곧 열기/닫기다.</para>
///
/// <list type="bullet">
/// <item><b><c>DoorCommandAccepted</c> = 명령 수락 = 애니메이션 시작.</b> 사거리 밖이라 벤더가 무시한 입력은
/// 애니메이션이 시작되지 않으므로 자동으로 보내지지 않는다 — 키 입력을 직접 신호로 바꾸지 않는 이유다.</item>
/// <item><b><c>DoorCloseCompleted</c> = 닫힘 애니메이션이 끝난 시점.</b> T3의 성공은
/// <c>AllOf(DoorCloseCompleted, SpaceExited)</c>라 이걸 안 보내면 T3는 영원히 실패한다.</item>
/// <item><b><c>DoorAutoOpenObserved</c>는 「열렸다」가 아니라 「보았다」.</b> 연출이 여는 동작이
/// <b>진행되는 동안</b> 플레이어가 실제로 그 문을 보고 있어야 한다. <see cref="PlayerSensors"/>의
/// <c>GazeProbe.CurrentId</c>로 판단한다. 한 번의 자동 개방에 한 번만 보낸다.</item>
/// </list>
///
/// <para><b>출처 구분(<see cref="ActionSource"/>)은 절대 섞으면 안 된다.</b>
/// 플레이어가 연 문은 <c>Player</c>, 연출이 연 문은 <c>Direction</c>이다.
/// <c>DoorObligationCondition</c>(C6)은 <b>플레이어가 연 문만</b> 의무로 잡고, H1의 실패는
/// 자동 개방을 본 뒤 <b>플레이어가</b> 닫기 명령을 낸 것이다. 섞으면 H1·C6·T3가 통째로 오판한다.</para>
///
/// <para><b>출처를 어떻게 정하는가.</b>
/// ① 연출이 움직이기 직전에 <see cref="BeginDirectionMove"/>를 부르면 그 움직임은 무조건 <c>Direction</c>이다.
/// ② 플레이어가 움직이기 직전에 <see cref="BeginPlayerMove"/>를 부르면 <c>Player</c>다 —
/// <see cref="PlayerInteractor"/>가 조준한 문에만 부른다. <b>①②가 정상 경로이고, 아래 둘은 폴백이다.</b>
/// ③ 예약이 없으면 <see cref="playerClaimSeconds"/> 안에 상호작용 키가 눌렸는지로 본다 —
/// 어느 문을 겨눴는지는 모르므로 추측이다.
/// ④ 그것도 아니면 <c>Direction</c>이다 — 문은 플레이어의 E 없이는 스스로 움직이지 않으므로,
/// 키 없이 시작된 움직임은 연출이라고 보는 쪽이 안전하다.</para>
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(60)]
public sealed class DoorRelay : MonoBehaviour
{
    [Header("문 ID")]
    [Tooltip("비우면 자기 자신·자식의 JudgeTarget 대표 ID를 쓴다. 벤더 문은 보통 자식 메시에 표식이 붙어 있다.")]
    [SerializeField] private string doorIdOverride = string.Empty;

    [Header("관찰")]
    [Tooltip("문 애니메이션. 비우면 같은 오브젝트에서 찾는다.")]
    [SerializeField] private Animation doorAnimation;

    [Tooltip("켜면 연출 자동 개방을 플레이어가 볼 때 DoorAutoOpenObserved를 보낸다(H1: corridor.door.auto, T1: toilet.stall.outer).")]
    [SerializeField] private bool reportAutoOpenObserved = true;

    [Header("출처 판단")]
    [Tooltip("문 상호작용 키. 벤더 DoorScript의 openButton과 같아야 한다(현재 E).")]
    [SerializeField] private KeyCode interactKey = KeyCode.E;

    [Tooltip("이 시간(초) 안에 상호작용 키가 눌렸으면 플레이어 조작으로 본다.")]
    [SerializeField, Min(0.05f)] private float playerClaimSeconds = 0.4f;

    [Header("디버그")]
    [SerializeField] private bool logSignals;

    private string _doorId = string.Empty;
    private bool _wasMoving;
    private bool _movingClose;
    private ActionSource _moveSource = ActionSource.Direction;
    private bool _autoOpenReported;
    private float _lastKeyTime = -999f;
    private float _directionClaimUntil = -999f;
    private float _playerClaimUntil = -999f;

    /// <summary>이 문의 판정 ID.</summary>
    public string DoorId
    {
        get { return _doorId; }
    }

    /// <summary>지금 움직이는 중인지.</summary>
    public bool IsMoving
    {
        get { return _wasMoving; }
    }

    /// <summary>
    /// 근무 씬이 열릴 때, 문으로 보이는 오브젝트마다 발신기를 하나씩 붙인다.
    /// <para>이 발신기는 <b>문 하나에 하나씩</b> 필요하므로 자동 설치도 전수로 한다.</para>
    /// </summary>
    // ────────────────────────────────────────────────────────────────────────
    // 자동 설치 (2026-09-24) — NightRunDriver·TabletBridge와 같은 방식
    // ────────────────────────────────────────────────────────────────────────

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Install()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;

        // 씬 전환 타이밍에 따라 sceneLoaded 시점에 아직 잡히지 않는 문이 있었다(2026-09-24: 8개 중 7개).
        // 문이 실제로 필요해지는 시점은 밤이 열릴 때이므로 그때 한 번 더 훑는다.
        EventBus.DayStarted -= OnDayStarted;
        EventBus.DayStarted += OnDayStarted;
    }

    private static void OnDayStarted(int day, ClauseZeroType clause)
    {
        EnsureFor(SceneManager.GetActiveScene());
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
        if (!FlowAutoInstall.IsDutyScene(scene)) return;

        Animation[] anims = FindObjectsByType<Animation>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int added = 0;
        for (int i = 0; i < anims.Length; i++)
        {
            Animation anim = anims[i];
            if (anim == null || anim.gameObject.scene != scene) continue;
            if (anim.GetComponent<DoorRelay>() != null) continue;

            JudgeTarget target = anim.GetComponentInChildren<JudgeTarget>(true);
            if (target == null || !LooksLikeDoor(target.PrimaryId)) continue;

            anim.gameObject.AddComponent<DoorRelay>();
            added++;
        }

        if (added > 0)
        {
            Debug.Log("[DoorRelay] 문 발신기 " + added + "개를 자동 설치했습니다.");
        }
    }

    /// <summary>
    /// 문으로 볼 ID인가. <b>표식으로 거르는 이유:</b> 씬에는 Animation을 가진 교탁·실험대도 있어서
    /// (<c>cls11.lectern</c> · <c>science.bench.glass</c>) 전부에 붙이면 책상이 움직일 때마다 문 신호가 나간다.
    /// </summary>
    private static bool LooksLikeDoor(string id)
    {
        if (string.IsNullOrEmpty(id)) return false;
        return id.IndexOf(".door", System.StringComparison.Ordinal) >= 0
            || id.IndexOf(".stall", System.StringComparison.Ordinal) >= 0;
    }

    private void OnEnable()
    {
        ResolveId();

        if (doorAnimation == null)
        {
            doorAnimation = GetComponent<Animation>();
        }

        _wasMoving = false;
        _autoOpenReported = false;

        if (string.IsNullOrEmpty(_doorId))
        {
            Debug.LogWarning("[DoorRelay] 문 ID를 찾지 못했습니다. JudgeTarget을 붙이거나 doorIdOverride를 채우십시오.", this);
        }

        if (doorAnimation == null)
        {
            Debug.LogWarning("[DoorRelay] Animation이 없습니다. 이 문은 애니메이션으로 열리지 않으므로 " +
                             "ReportMoveStarted/ReportCloseCompleted를 직접 불러야 합니다.", this);
        }
    }

    private void OnDisable()
    {
        // 정적 이벤트 구독은 없다(EventBus·JudgeTargetRegistry 모두 쓰지 않는다).
        // 상태만 비워 다음에 켜질 때 옛 움직임의 잔상이 신호로 나가지 않게 한다.
        _wasMoving = false;
        _autoOpenReported = false;
    }

    private void Update()
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(interactKey))
        {
            _lastKeyTime = Time.time;
        }
#endif

        bool canSend = NightRun.IsNightActive && !NightRun.IsCaptured && !PlayerSensors.TabOpen;

        bool moving;
        bool closing;
        ReadAnimation(out moving, out closing);

        if (moving && !_wasMoving)
        {
            _wasMoving = true;
            _movingClose = closing;
            _moveSource = ResolveSource();
            _autoOpenReported = false;

            if (canSend)
            {
                Send(JudgeSignal.DoorCommand(_doorId, _movingClose, _moveSource));
            }
        }
        else if (moving && _wasMoving)
        {
            // 되감기로 방향이 바뀌었으면 새 명령이다(열리는 중에 다시 E를 눌러 닫는 경우).
            if (closing != _movingClose)
            {
                _movingClose = closing;
                _moveSource = ResolveSource();
                _autoOpenReported = false;

                if (canSend)
                {
                    Send(JudgeSignal.DoorCommand(_doorId, _movingClose, _moveSource));
                }
            }
        }
        else if (!moving && _wasMoving)
        {
            _wasMoving = false;

            if (_movingClose && canSend)
            {
                // 닫힘 완료. 출처는 시작할 때 정한 것을 그대로 쓴다.
                Send(new JudgeSignal(SignalKind.DoorCloseCompleted, SpaceId.None, _doorId, _moveSource, false, 0f));
            }
        }

        // 자동 개방을 「보았다」 — 여는 동작이 진행되는 동안에만, 한 번만.
        if (!canSend || !reportAutoOpenObserved || !_wasMoving || _movingClose || _autoOpenReported)
        {
            return;
        }

        if (_moveSource != ActionSource.Direction)
        {
            return;   // 플레이어가 직접 연 문은 「자동 개방」이 아니다.
        }

        PlayerSensors sensors = PlayerSensors.Active;
        if (sensors == null || _doorId.Length == 0)
        {
            return;
        }

        if (string.Equals(sensors.Gaze.CurrentId, _doorId, System.StringComparison.Ordinal))
        {
            _autoOpenReported = true;
            Send(JudgeSignal.Target(SignalKind.DoorAutoOpenObserved, _doorId));
        }
    }

    /// <summary>
    /// 연출이 이 문을 움직이기 <b>직전</b>에 부른다. 이 예약 동안 시작된 움직임은 <see cref="ActionSource.Direction"/>이다.
    /// 앞으로 만들 <c>AnomalyCueDirector</c>(복도 Band0 「지정 통행에서 문 자동 개방 1회」,
    /// 화장실 Band0 「입구 쪽 칸 자동 개방 1회」)가 이것을 부른다.
    /// </summary>
    public void BeginDirectionMove(float seconds = 1f)
    {
        _directionClaimUntil = Time.time + Mathf.Max(0.05f, seconds);
    }

    /// <summary>
    /// 플레이어가 이 문을 움직이기 <b>직전</b>에 부른다. 이 예약 동안 시작된 움직임은 <see cref="ActionSource.Player"/>다.
    /// <see cref="PlayerInteractor"/>가 조준한 문에만 부르므로, 「0.4초 안에 아무 데서나 키가 눌렸나」라는
    /// <see cref="playerClaimSeconds"/> 추측보다 정확하다. 그 추측은 상호작용기가 없을 때의 폴백으로 남는다.
    /// </summary>
    public void BeginPlayerMove(float seconds = 1f)
    {
        _playerClaimUntil = Time.time + Mathf.Max(0.05f, seconds);
    }

    /// <summary>애니메이션이 아닌 문 구현이 쓸 수동 보고(명령 수락).</summary>
    public void ReportMoveStarted(bool isClose, ActionSource source)
    {
        _wasMoving = true;
        _movingClose = isClose;
        _moveSource = source;
        _autoOpenReported = false;

        if (NightRun.IsNightActive && !NightRun.IsCaptured && !PlayerSensors.TabOpen)
        {
            Send(JudgeSignal.DoorCommand(_doorId, isClose, source));
        }
    }

    /// <summary>애니메이션이 아닌 문 구현이 쓸 수동 보고(닫힘 완료).</summary>
    public void ReportCloseCompleted()
    {
        _wasMoving = false;

        if (NightRun.IsNightActive && !NightRun.IsCaptured && !PlayerSensors.TabOpen)
        {
            Send(new JudgeSignal(SignalKind.DoorCloseCompleted, SpaceId.None, _doorId, _moveSource, false, 0f));
        }
    }

    /// <summary>재생 중인 상태의 speed 부호로 열기/닫기를 읽는다.</summary>
    private void ReadAnimation(out bool moving, out bool closing)
    {
        moving = false;
        closing = false;

        if (doorAnimation == null || !doorAnimation.isPlaying)
        {
            return;
        }

        foreach (AnimationState state in doorAnimation)
        {
            if (state == null || !doorAnimation.IsPlaying(state.name))
            {
                continue;
            }

            closing = state.speed < 0f;

            // 클립 wrapMode가 ClampForever로 바뀌면 끝나도 isPlaying이 계속 true다.
            // 진행 방향의 끝에 닿았으면 움직임이 끝난 것으로 본다(닫힘 완료를 놓치지 않기 위한 안전장치).
            float t = state.normalizedTime;
            bool atEnd = closing ? t <= 0.0001f : t >= 0.9999f;
            moving = !atEnd;
            return;
        }
    }

    private ActionSource ResolveSource()
    {
        if (Time.time <= _directionClaimUntil)
        {
            return ActionSource.Direction;
        }

        if (Time.time <= _playerClaimUntil)
        {
            return ActionSource.Player;
        }

        if (Time.time - _lastKeyTime <= playerClaimSeconds)
        {
            return ActionSource.Player;
        }

        // 벤더 문은 플레이어의 E 없이는 스스로 움직이지 않는다. 키 없이 시작된 움직임은 연출로 본다.
        return ActionSource.Direction;
    }

    private void ResolveId()
    {
        if (!string.IsNullOrEmpty(doorIdOverride))
        {
            _doorId = doorIdOverride.Trim();
            return;
        }

        JudgeTarget target = GetComponentInChildren<JudgeTarget>(true);
        if (target == null)
        {
            target = GetComponentInParent<JudgeTarget>();
        }

        _doorId = target != null ? target.PrimaryId : string.Empty;
    }

    private void Send(in JudgeSignal signal)
    {
        if (logSignals)
        {
            Debug.Log("[DoorRelay] " + signal, this);
        }

        NightRun.Send(signal);
    }
}

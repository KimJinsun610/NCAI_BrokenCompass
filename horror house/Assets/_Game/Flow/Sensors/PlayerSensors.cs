using System;
using NightDuty;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 플레이어 센서 허브. <b>0.1초 누산 틱</b>으로 <see cref="GazeProbe"/>·<see cref="ProximityProbe"/>를
/// CLAUDE.md §4.4.1이 정한 고정 순서로 돌린다.
///
/// <para><b>왜 누산기인가.</b> <c>SpaceZones</c>처럼 <c>if (Time.time &lt; _next)</c> 게이트로 재면
/// 프레임이 0.016초씩 들쭉날쭉한 만큼 매 샘플이 뒤로 밀린다(60fps에서 샘플 하나가 0.1초가 아니라 0.100~0.116초).
/// 그런데 <c>GazeCondition</c>의 유예 2초와 응시 3초는 <b>샘플 Value의 합</b>으로만 흐르므로,
/// 게이트 방식이면 코어는 3초를 셌는데 실제로는 3.4초가 지나 있다(H2·C3). 누산기는 이 오차를 남기지 않는다.</para>
///
/// <para><b>실행 순서.</b> <c>[DefaultExecutionOrder(50)]</c>으로 기본 순서(0)의 <c>NightRunDriver</c>(Tick)와
/// <c>SpaceZones</c>(공간·구역·점검) 뒤에 돈다. 같은 프레임 안에서 §4.4.1의 「<c>Tick</c> 먼저」가 지켜진다.</para>
///
/// <para><b>태블릿(2026-09-30 최종 기획서).</b> 태블릿을 든 동안에도 시간과 판정이 흐르고(<see cref="NightRun.JudgeWhileTabOpen"/>),
/// 응시 기준점만 태블릿 위 가운데(화면 높이 78%)로 옮긴다. 옛 규칙(판정 정지)을 켜 두면 <see cref="TabOpen"/>이 참인 동안 발신을 멈춘다.</para>
///
/// <para><b>보내지 않는 것.</b> <c>Tick</c>·<c>NightBegan</c>·<c>NightEndAccepted</c>는 코어가 만든다.
/// <c>TabChanged</c>는 <c>TabletBridge</c>가 보낸다 — 이 허브는 태블릿 상태를 <b>읽기만</b> 한다.</para>
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(50)]
public sealed class PlayerSensors : MonoBehaviour
{
    [Header("참조")]
    [Tooltip("응시 레이를 쏠 카메라. 비우면 자식에서, 그래도 없으면 Camera.main을 쓴다.")]
    [SerializeField] private Camera gazeCamera;

    [Tooltip("근접의 플레이어 발밑 기준점(수평 좌표만 쓴다). 비우면 카메라의 최상위 부모를 쓴다.")]
    [SerializeField] private Transform playerRoot;

    [Header("틱")]
    [Tooltip("샘플 간격(초). 기획 정본 공통 명세 2절의 0.1초 해상도. 바꾸지 말 것.")]
    [SerializeField, Min(0.01f)] private float stepSeconds = 0.1f;

    [Tooltip("한 프레임에 몰아 보낼 수 있는 최대 샘플 수(무한 루프 방지). 넘치면 버리고 경고한다.")]
    [SerializeField, Min(1)] private int maxStepsPerFrame = 50;

    [Header("발신기")]
    [SerializeField] private GazeProbe gaze = new GazeProbe();
    [SerializeField] private ProximityProbe proximity = new ProximityProbe();

    [Header("태블릿(Tab)")]
    [Tooltip("이 오브젝트가 켜져 있으면 태블릿이 올라온 것으로 본다. 태블릿 UI 루트를 넣는다. 비워도 된다.")]
    [SerializeField] private GameObject tabRootToWatch;

    private float _acc;
    private int _lastDay = -1;
    private static bool s_tabOpen;
    private static PlayerSensors s_active;

    /// <summary>지금 살아 있는 허브. 없으면 null. (<c>FindAnyObjectByType</c>은 DontSave 오브젝트를 못 찾으므로 직접 등록한다.)</summary>
    public static PlayerSensors Active
    {
        get { return s_active; }
    }

    /// <summary>
    /// 한 샘플이 끝날 때마다 발생한다. 인자는 이 샘플이 대표하는 시간(초, 기본 0.1).
    /// <para>
    /// <b>이 허브 밖의 발신기(<c>AnomalyCueDirector</c>·<c>InspectionSensor</c> 등)가 자체 <c>Update</c>로 0.1초를 세지 않게 하려고 둔다.</b>
    /// 발신기마다 따로 재면 같은 순간의 신호 순서가 Unity의 Update 순서에 맡겨지고,
    /// 그러면 CLAUDE.md §4.4.1의 고정 순서(Tick → PassageCompleted → ZoneExited → InspectionCompleted → SpaceExited)를
    /// 보장할 수 없다.
    /// </para>
    /// <para>
    /// 응시·근접을 <b>먼저</b> 보낸 뒤에 발생하므로, 구독자는 이미 갱신된
    /// <see cref="GazeProbe.CurrentId"/>를 읽을 수 있다.
    /// </para>
    /// <para><b>구독자는 <c>OnDisable</c>에서 반드시 해제할 것</b> — static이라 씬을 바꿔도 살아 있다(§5.2-6).</para>
    /// </summary>
    public static event Action<float> Sampled;

    /// <summary>응시 발신기. 다른 발신기가 <c>CurrentId</c>를 읽는다.</summary>
    public GazeProbe Gaze
    {
        get { return gaze; }
    }

    /// <summary>근접 발신기.</summary>
    public ProximityProbe Proximity
    {
        get { return proximity; }
    }

    /// <summary>플레이어 발밑 기준점. 없으면 null.</summary>
    public Transform PlayerRoot
    {
        get { return playerRoot; }
    }

    /// <summary>태블릿이 올라와 있는지(판정 규칙과 무관한 실제 상태). 응시 기준점과 보고 포커스가 읽는다.</summary>
    public static bool TabletRaised
    {
        get { return s_tabOpen; }
    }

    /// <summary>
    /// <b>신호를 멈춰야 하는</b> 태블릿 상태인지 — 옛 규칙(Tab 중 판정 정지)일 때만 참이다.
    /// 최종 기획서 규칙(<see cref="NightRun.JudgeWhileTabOpen"/>)에서는 태블릿을 들어도 false라 발신기들이 그대로 돈다.
    /// 문·손전등·공간 발신기와 조작기가 이 값으로 멈춘다(이름은 하위 호환으로 남겼다).
    /// </summary>
    public static bool TabOpen
    {
        get { return s_tabOpen && !NightRun.JudgeWhileTabOpen; }
    }

    /// <summary>
    /// 태블릿 UI가 열고 닫을 때 부른다. <b>이 메서드는 <c>JudgeSignal.Tab</c>을 보내지 않는다</b> —
    /// Tab 신호와 게임 시계 정지는 태블릿 쪽이 한 창구에서 해야 두 번 보내는 일이 없다(Q6).
    /// </summary>
    public static void SetTabOpen(bool open)
    {
        s_tabOpen = open;
    }

    // ────────────────────────────────────────────────────────────────────────
    // 자동 설치 (2026-09-24) — NightRunDriver·TabletBridge와 같은 방식
    // ────────────────────────────────────────────────────────────────────────

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Install()
    {
        s_tabOpen = false;
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

    /// <summary>
    /// <b>씬에 직접 놓지 않아도 된다.</b> 근무 씬이 열릴 때 허브가 없으면 플레이어 루트에 하나 붙인다.
    /// <para>2026-09-24에 PlayScene에 이 허브가 저장돼 있지 않아 <b>응시·근접 판정이 통째로 죽어</b> 있던 것을
    /// 막는 안전장치다. 씬에 직접 놓아 두면(인스펙터로 값을 맞추려면) 자동 생성은 건너뛴다.</para>
    /// </summary>
    private static void EnsureFor(Scene scene)
    {
        if (!FlowAutoInstall.IsDutyScene(scene)) return;
        if (FlowAutoInstall.Exists<PlayerSensors>(scene)) return;

        Camera cam = FlowAutoInstall.FindCamera(scene);
        if (cam == null)
        {
            Debug.LogWarning("[PlayerSensors] 근무 씬에 카메라가 없어 센서 허브를 설치하지 못했습니다.");
            return;
        }

        // 근접은 발밑 기준점을 쓰므로 카메라의 최상위 부모(FPController)에 붙인다.
        PlayerSensors hub = cam.transform.root.gameObject.AddComponent<PlayerSensors>();
        hub.gazeCamera = cam;
        hub.playerRoot = cam.transform.root;

        // tabRootToWatch는 비워 둔다 — TabletBridge가 SetTabOpen으로 직접 알려 준다.
    }

    private void OnEnable()
    {
        s_active = this;
        _acc = 0f;
        _lastDay = -1;

        // EventBus는 구독하지 않는다. 대신 정적 등록부 이벤트를 쓰므로 OnDisable에서 반드시 푼다(CLAUDE.md §5.2-6).
        JudgeTargetRegistry.Changed += OnTargetsChanged;
    }

    private void OnDisable()
    {
        JudgeTargetRegistry.Changed -= OnTargetsChanged;

        if (s_active == this)
        {
            s_active = null;
        }
    }

    private void Update()
    {
        if (tabRootToWatch != null)
        {
            s_tabOpen = tabRootToWatch.activeInHierarchy;
        }

        // 밤이 아니거나 포획됐으면 발신하지 않는다. 누산기도 비워 둔다
        // (다시 열렸을 때 밀린 시간이 한꺼번에 쏟아지면 안 된다).
        if (!NightRun.IsNightActive || NightRun.IsCaptured || TabOpen)
        {
            _acc = 0f;
            return;
        }

        if (_lastDay != NightRun.Day)
        {
            _lastDay = NightRun.Day;
            _acc = 0f;
            gaze.Reset();
            proximity.Reset();
        }

        Camera cam = ResolveCamera();
        Transform root = ResolvePlayerRoot(cam);

        _acc += Time.deltaTime;

        int steps = 0;
        while (_acc >= stepSeconds)
        {
            _acc -= stepSeconds;
            steps++;

            if (steps > maxStepsPerFrame)
            {
                // 씬 로드·에디터 중단 등으로 deltaTime이 비정상적으로 크다. 남은 빚은 버린다.
                Debug.LogWarning("[PlayerSensors] 한 프레임에 " + maxStepsPerFrame + " 샘플을 넘겼습니다. 남은 " +
                                 _acc.ToString("F2") + "초를 버립니다.", this);
                _acc = 0f;
                break;
            }

            Sample(cam, root, stepSeconds);
        }
    }

    /// <summary>
    /// 한 샘플. <b>순서를 바꾸지 말 것.</b>
    /// ① 응시를 먼저 재고 보낸다 — 같은 프레임의 <c>DoorRelay</c>가 <see cref="GazeProbe.CurrentId"/>로
    /// 「자동 개방을 보았다」를 판단하기 때문이다. 태블릿을 들었으면 기준점이 화면 높이 78%로 올라간다.
    /// ② 근접을 보낸다.
    /// </summary>
    private void Sample(Camera cam, Transform root, float step)
    {
        gaze.Probe(cam, root, SensingRules.GazeViewportY(s_tabOpen));
        gaze.Send(step);

        proximity.Sample(root, step);

        // ③ 마지막으로 허브 밖 발신기에 같은 틱을 나눠 준다.
        //    응시·근접 뒤에 부르는 이유: 구독자가 갱신된 GazeProbe.CurrentId를 읽어 식별 0.2초를 세기 때문이다.
        //    구독자 하나가 던진 예외로 나머지 발신이 멈추면 안 되므로 한 건씩 감싼다.
        Action<float> handler = Sampled;
        if (handler == null) return;

        Delegate[] list = handler.GetInvocationList();
        for (int i = 0; i < list.Length; i++)
        {
            try
            {
                ((Action<float>)list[i])(step);
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
            }
        }
    }

    private void OnTargetsChanged()
    {
        proximity.MarkDirty();
    }

    private Camera ResolveCamera()
    {
        if (gazeCamera != null)
        {
            return gazeCamera;
        }

        gazeCamera = GetComponentInChildren<Camera>(true);
        if (gazeCamera == null)
        {
            gazeCamera = Camera.main;
        }

        return gazeCamera;
    }

    private Transform ResolvePlayerRoot(Camera cam)
    {
        if (playerRoot != null)
        {
            return playerRoot;
        }

        if (cam != null)
        {
            playerRoot = cam.transform.root;
        }

        return playerRoot;
    }

    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying || gaze == null || !gaze.HasCollider)
        {
            return;
        }

        Gizmos.color = gaze.CurrentId.Length > 0 ? Color.green : new Color(1f, 1f, 1f, 0.4f);
        Gizmos.DrawWireSphere(gaze.LastPoint, 0.08f);
    }
}

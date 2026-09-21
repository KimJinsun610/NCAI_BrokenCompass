using System;
using NightDuty;
using UnityEngine;

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
/// <para><b>보내지 않는 것.</b> <c>Tick</c>·<c>NightBegan</c>·<c>NightEndAccepted</c>는 코어가 만든다.
/// <c>TabChanged</c>는 태블릿 UI가 보낸다(아직 없음, Q6) — 이 허브는 Tab 상태를 <b>읽기만</b> 한다.</para>
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
    [Tooltip("이 오브젝트가 켜져 있으면 Tab이 열린 것으로 본다. 태블릿 UI 루트를 넣는다. 비워도 된다.")]
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
    /// <b>이 허브 밖의 발신기(<c>AnomalyCueDirector</c> 등)가 자체 <c>Update</c>로 0.1초를 세지 않게 하려고 둔다.</b>
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

    /// <summary>
    /// 태블릿이 열려 있는지. <b>열려 있는 동안에는 <c>TabChanged</c> 외에 아무 신호도 보내지 않는다.</b>
    /// 코어가 버리기는 하지만, 버려진 신호만큼 발신기 내부 상태(응시 연속 시간·문 상태)가 코어와 어긋나기 때문이다.
    /// </summary>
    public static bool TabOpen
    {
        get { return s_tabOpen; }
    }

    /// <summary>
    /// 태블릿 UI가 열고 닫을 때 부른다. <b>이 메서드는 <c>JudgeSignal.Tab</c>을 보내지 않는다</b> —
    /// Tab 신호와 게임 시계 정지는 태블릿 쪽이 한 창구에서 해야 두 번 보내는 일이 없다(Q6).
    /// </summary>
    public static void SetTabOpen(bool open)
    {
        s_tabOpen = open;
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
        if (!NightRun.IsNightActive || NightRun.IsCaptured || s_tabOpen)
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
    /// 「자동 개방을 보았다」를 판단하기 때문이다.
    /// ② 근접을 보낸다.
    /// </summary>
    private void Sample(Camera cam, Transform root, float step)
    {
        gaze.Probe(cam, root);
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

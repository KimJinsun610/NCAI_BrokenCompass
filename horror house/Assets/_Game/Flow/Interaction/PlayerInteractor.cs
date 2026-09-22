using NightDuty;
using UnityEngine;

/// <summary>
/// 플레이어 상호작용. <b>조준선이 맨 먼저 맞히는 것 하나</b>를 고르고, 상호작용 키로 그것을 다룬다.
/// 지금 다루는 것은 문뿐이지만, 고르는 일과 다루는 일이 갈라져 있어 대상이 늘어도 이 틀은 그대로다.
///
/// <para><b>왜 벤더 <c>DoorScript</c>에 맡기지 않는가.</b> 벤더는 ⑴ 카메라가 문 앞 트리거 상자 안에 있고
/// ⑵ <b>문짝이 아니라 손잡이</b>를 약 25° 안쪽으로 겨눠야(<c>dotProd &lt; -0.9f</c>) 열어 준다.
/// 그런데 씬의 문 56개는 전부 <c>doorTexts.enabled = false</c>라 「Press [E] to open」이 뜨지 않고
/// <c>doorSounds</c>의 클립도 비어 있어, <b>어디에 서서 무엇을 겨눌지 알 길이 없다</b>(2026-09-22 실측).
/// 조건은 멀쩡한데 보이지 않는 조작이라 사람이 문 앞에서 막힌다.</para>
///
/// <para><b>조준은 좁게.</b> <see cref="Physics.Raycast"/> 한 발을 쏘고 <b>맨 먼저 맞은 것</b>이 문일 때만
/// 대상으로 삼는다. 벽에 가린 문도, 옆에 선 문도 잡히지 않는다. 벤더의 커다란 트리거 상자는 무시한다 —
/// 그걸 세면 문을 안 보고 서 있기만 해도 안내가 뜬다.</para>
///
/// <para><b>열리는 문은 기획이 정한다.</b> <see cref="DoorPolicySO"/>에 적힌 문만 열린다.
/// 서랍·사물함·책장은 표와 무관하게 여닫힌다. 그 밖의 문은 「잠겨 있습니다」로 끝난다.</para>
///
/// <para><b>판정과의 약속.</b> 문이 움직이면 <see cref="DoorRelay"/>가 신호를 보내는데, 그 신호의
/// <see cref="ActionSource"/>가 <c>Player</c>인지 <c>Direction</c>인지가 C6·H1·T3의 판정을 가른다.
/// 여기서는 문을 움직이기 <b>직전에</b> <see cref="DoorRelay.BeginPlayerMove"/>로 출처를 못 박는다 —
/// 「0.4초 안에 키가 눌렸나」 같은 추측에 기대지 않는다. 그러려면 벤더가 자기 키로 몰래 여는 일이 없어야 하므로,
/// 시작할 때 문마다 <see cref="DoorHandle.SilenceVendorInput"/>로 벤더의 키를 거둔다.</para>
///
/// <para><b>막는 때.</b> 포획(사망 연출) 중과 태블릿을 펼친 동안과 일시정지 중에는 아무것도 하지 않는다.
/// <see cref="DoorRelay"/>도 같은 조건에서 신호를 보내지 않으므로 둘이 어긋나지 않는다.
/// 밤이 아닐 때는 <b>막지 않는다</b> — 출근과 퇴실은 밤 바깥에서 일어난다.</para>
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(50)]
public sealed class PlayerInteractor : MonoBehaviour
{
    /// <summary>
    /// 손이 닿는 거리(m). <b>짧게 잡는 것이 맞습니다</b> — 2.5m로 두었더니 복도 건너편 문까지 안내가 떠서
    /// 어느 문을 겨눈 것인지 알 수 없었습니다(2026-09-22 사용자 보고).
    /// </summary>
    private const float DefaultReach = 1.8f;

    /// <summary>기획에 없는 문, 그리고 열쇠가 필요한 문에 띄우는 한 줄.</summary>
    private const string SealedPrompt = "잠겨 있습니다";

    /// <summary>
    /// 조준선의 굵기(m). <b>0이면 안 됩니다</b> — 여닫이 두 짝 사이의 실틈으로 레이가 빠져나가
    /// 문 한가운데를 겨눴는데 아무것도 안 잡히는 일이 생깁니다(정문에서 실측).
    /// 6cm면 그 틈만 메우고, 겨눔의 정확함은 그대로입니다.
    /// </summary>
    private const float AimRadius = 0.06f;

    private static PlayerInteractor s_active;

    [Header("조준")]
    [Tooltip("조준선이 대상을 잡아 주는 거리(m). 짧을수록 「그 문을 겨눴다」가 분명해진다.")]
    [SerializeField, Min(0.5f)] private float reachMeters = DefaultReach;

    [Tooltip("조준에 쓸 카메라. 비우면 Camera.main을 쓴다.")]
    [SerializeField] private Camera aimCamera;

    [Header("조작")]
    [Tooltip("상호작용 키. DoorRelay의 interactKey와 같아야 한다.")]
    [SerializeField] private KeyCode interactKey = KeyCode.E;

    [Header("디버그")]
    [SerializeField] private bool logActions;

    private DoorHandle _door;
    private DoorRelay _relay;
    private string _prompt = string.Empty;
    private bool _actionable;
    private bool _silenced;
    private DoorPolicySO.Kind _kind = DoorPolicySO.Kind.Sealed;

    /// <summary>지금 살아 있는 상호작용기.</summary>
    public static PlayerInteractor Active
    {
        get { return s_active; }
    }

    /// <summary>
    /// 잠긴 문도 연다. <b>시험용</b>이며 기본은 꺼짐이다 — 열쇠 연출이 생기기 전까지
    /// 잠긴 문이 길을 막으므로, 하네스가 이것을 켜서 회차를 끝까지 돌려 본다.
    /// </summary>
    public static bool IgnoreLocks { get; set; }

    /// <summary>지금 화면에 띄울 안내. 없으면 빈 문자열.</summary>
    public string Prompt
    {
        get { return _prompt; }
    }

    /// <summary>지금 키를 누르면 실제로 무슨 일이 일어나는가. 조준선 강조에 쓴다.</summary>
    public bool HasAction
    {
        get { return _actionable; }
    }

    /// <summary>지금 겨누고 있는 대상의 이름. 없으면 빈 문자열. 상태판이 보여 준다.</summary>
    public string AimedName
    {
        get { return _door.IsValid && _door.Owner != null ? _door.Owner.name : string.Empty; }
    }

    /// <summary>지금 겨누고 있는 대상의 분류. 상태판이 보여 준다.</summary>
    public DoorPolicySO.Kind AimedKind
    {
        get { return _kind; }
    }

    // ─────────────────────────────── 설치 ───────────────────────────────

    /// <summary>
    /// 씬에 이미 있으면 건너뛰고, 없으면 만든다. <see cref="NightRunDriver"/>와 같은 방식이다.
    /// <para><b>민이 <c>PlaySystems</c> 프리팹에 이 컴포넌트를 붙이면 이 자동 설치는 아무 일도 하지 않는다.</b>
    /// 그 전까지는 씬 파일을 건드리지 않고도 상호작용이 돌게 하려는 임시 통로다.</para>
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (s_active != null || !DoorHandle.ImplementationExists())
        {
            return;
        }

        if (FindAnyObjectByType<PlayerInteractor>(FindObjectsInactive.Include) != null)
        {
            return;
        }

        if (Camera.main == null)
        {
            return;   // 1인칭 카메라가 없는 씬(메뉴·로딩·결과)에서는 할 일이 없다.
        }

        // hideFlags를 붙이지 않는다 — DontSave는 플레이를 꺼도 오브젝트를 남긴다(CLAUDE.md §5.5-23).
        GameObject go = new GameObject("PlayerInteractor (auto)");
        go.AddComponent<PlayerInteractor>();
    }

    private void OnEnable()
    {
        s_active = this;
        InteractionHud.EnsureExists();
    }

    private void OnDisable()
    {
        if (s_active == this)
        {
            s_active = null;
        }
    }

    // ─────────────────────────────── 매 프레임 ───────────────────────────────

    private void Update()
    {
        SilenceVendorDoorsOnce();

        _door = new DoorHandle();
        _relay = null;
        _prompt = string.Empty;
        _actionable = false;
        _kind = DoorPolicySO.Kind.Sealed;

        if (!CanInteract())
        {
            return;
        }

        Camera cam = ResolveCamera();
        if (cam == null)
        {
            return;
        }

        DoorHandle door = AimAtDoor(cam);
        if (!door.IsValid || door.OpensByItself)
        {
            return;   // 자동문은 다가가면 스스로 열린다. 안내할 조작이 없다.
        }

        _door = door;
        _kind = Classify(door);

        bool storage = _kind == DoorPolicySO.Kind.Storage;
        // 정책은 절대적이다. 잠금 무시(F4)는 열쇠만 건너뛰지 기획을 건너뛰지 않는다 —
        // 정책까지 무시하려면 DoorPolicySO.OpenEverythingOverride(F5)를 켜야 한다.
        bool sealedOff = _kind == DoorPolicySO.Kind.Sealed;
        bool needsKey = door.IsLocked && !IgnoreLocks;

        if (sealedOff || (needsKey && !door.IsOpen))
        {
            // 기획에 없는 문이거나 열쇠가 필요한 문이다. 흔들 수는 있지만 열리지 않는다.
            _prompt = SealedPrompt;
            _actionable = false;
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(interactKey))
            {
                door.Rattle();   // 문이 움직이지 않으므로 판정 신호도 나가지 않는다.
            }
#endif
            return;
        }

        _relay = door.Owner.GetComponent<DoorRelay>();

        if (door.IsOpen)
        {
            if (door.ClosesByItself)
            {
                _door = new DoorHandle();
                return;   // 멀어지면 스스로 닫히는 문이다. 「닫기」를 권하지 않는다.
            }

            _prompt = "[" + interactKey + "] " + (storage ? "닫기" : "문 닫기");
        }
        else
        {
            _prompt = "[" + interactKey + "] " + (storage ? "열기" : "문 열기");
        }

        _actionable = true;

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(interactKey))
        {
            Act();
        }
#endif
    }

    private void Act()
    {
        if (IgnoreLocks && _door.IsLocked)
        {
            _door.ForceUnlock();
        }

        // 출처를 못 박는 것이 먼저다. DoorRelay는 실행 순서 60이라 이 프레임의 뒤에서 움직임을 읽는다.
        if (_relay != null)
        {
            _relay.BeginPlayerMove();
        }

        bool wasOpen = _door.IsOpen;
        if (wasOpen)
        {
            _door.Close();
        }
        else
        {
            _door.Open();
        }

        if (logActions)
        {
            Debug.Log("[상호작용] " + _door.Owner.name + " " + (wasOpen ? "닫기" : "열기"), _door.Owner);
        }
    }

    /// <summary>
    /// 조준선이 <b>맨 먼저</b> 맞히는 것이 문일 때만 그 문. 벽에 가린 문도, 옆에 선 문도 잡히지 않는다.
    /// 레이가 아니라 아주 가는 구체를 쏜다(<see cref="AimRadius"/>) — 두 짝 사이 실틈을 메우기 위해서다.
    /// <para><b>트리거는 무시한다.</b> 문마다 앞에 벤더의 커다란 트리거 상자(1.2 × 2.2 × 3.3m)가 서 있어서,
    /// 트리거까지 세면 문을 보지 않고 서 있기만 해도 잡힌다. 문짝·손잡이·몸통에는 전부 트리거가 아닌
    /// 콜라이더가 따로 있으므로 조준에는 그쪽만 쓰면 된다(2026-09-22 씬 실측).</para>
    /// </summary>
    private DoorHandle AimAtDoor(Camera cam)
    {
        RaycastHit hit;
        if (!Physics.SphereCast(cam.transform.position, AimRadius, cam.transform.forward, out hit,
                reachMeters, ~0, QueryTriggerInteraction.Ignore))
        {
            return new DoorHandle();
        }

        return DoorHandle.Of(hit.collider);
    }

    /// <summary>이 문이 기획이 쓰는 문인가, 서랍인가, 아니면 그냥 배경인가.</summary>
    private static DoorPolicySO.Kind Classify(DoorHandle door)
    {
        DoorPolicySO policy = DoorPolicySO.Load();
        if (policy == null || door.Owner == null)
        {
            return DoorPolicySO.Kind.Openable;
        }

        Transform t = door.Owner.transform;
        string path = t.name;
        Transform p = t.parent;
        while (p != null)
        {
            path = p.name + "/" + path;
            p = p.parent;
        }

        JudgeTarget target = door.Owner.GetComponentInChildren<JudgeTarget>(true);
        if (target == null)
        {
            target = door.Owner.GetComponentInParent<JudgeTarget>();
        }

        return policy.Classify(t.name, path, target != null ? target.PrimaryId : string.Empty);
    }

    private bool CanInteract()
    {
        if (Time.timeScale <= 0f)
        {
            return false;   // 일시정지.
        }

        if (PlayerSensors.TabOpen)
        {
            return false;   // 태블릿을 펼친 동안은 조작하지 않는다(DoorRelay도 같은 조건에서 신호를 멈춘다).
        }

        return !NightRun.IsCaptured;
    }

    private Camera ResolveCamera()
    {
        if (aimCamera == null)
        {
            aimCamera = Camera.main;
        }

        return aimCamera;
    }

    /// <summary>
    /// 벤더가 자기 키로 스스로 여는 일을 한 번만 거둔다. 늦게 생기는 문은 없으므로 첫 프레임에 한 번이면 된다.
    /// <para>이것을 하지 않으면 같은 <c>E</c>에 벤더와 여기가 동시에 반응해 한 프레임에 두 번 열리고,
    /// 벤더가 먼저 연 경우 <see cref="DoorRelay.BeginPlayerMove"/>를 부를 기회가 없어 출처가 추측으로 떨어진다.
    /// 기획에 없는 문도 벤더 쪽 입력으로는 열리지 않게 된다.</para>
    /// </summary>
    private void SilenceVendorDoorsOnce()
    {
        if (_silenced)
        {
            return;
        }

        _silenced = true;

        DoorHandle[] doors = DoorHandle.All();
        int count = 0;
        for (int i = 0; i < doors.Length; i++)
        {
            if (!doors[i].IsValid)
            {
                continue;
            }

            doors[i].SilenceVendorInput();
            count++;
        }

        if (logActions)
        {
            Debug.Log("[상호작용] 문 " + count + "개의 벤더 입력을 거뒀습니다(여기서 전부 다룹니다).", this);
        }
    }
}

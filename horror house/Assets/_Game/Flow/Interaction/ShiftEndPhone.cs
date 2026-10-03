using NightDuty;
using UnityEngine;

/// <summary>
/// 경비실 전화기 — <b>근무를 일찍 끝내는 곳</b>(2026-10-03 민 요청). 근무는 기획서의 종료 시각(04:00)에 저절로 끝나지만,
/// 오늘 점검을 전부 보고했다면 경비실로 돌아와 이 전화로 먼저 끝낼 수 있다.
/// <list type="bullet">
/// <item><b>조건</b>은 코어가 정한다 — <see cref="NightRun.CanEndShiftEarly"/>(밤 진행 중 · 붙잡히지 않음 · 점검표 전부 보고).</item>
/// <item><b>끝내는 길은 04:00과 같다</b> — <see cref="NightRun.RequestEndNight"/> → <see cref="EventBus.DayEnded"/> → 결과창(<c>PlayResultRouter</c>).
/// 남은 카드 정산·조우 이월이 그대로 돈다.</item>
/// <item><b>두 번 눌러야 끝난다.</b> [E]를 누르면 3초 동안 「한 번 더」를 기다린다. 다른 곳을 보면 취소.</item>
/// <item>겨누면 외곽선(<see cref="InteractionOutline"/>) — 끝낼 수 있으면 진하게·밝은 조준선, 점검이 남았으면 흐리게(0.35)·「남은 점검 n건」(2026-10-03 민: 전화기에도 외곽선).</item>
/// </list>
/// 씬의 전화기 오브젝트에 붙인다(메뉴 「야간근무/경비실/전화기 놓기」). 조준·막는 조건은 <see cref="CctvSystem"/>와 같다.
/// </summary>
[DisallowMultipleComponent]
public sealed class ShiftEndPhone : MonoBehaviour
{
    private const float Reach = 1.8f;
    private const float AimRadius = 0.06f;
    private const float ConfirmSeconds = 3f;

    /// <summary>점검이 남아 끝낼 수 없을 때의 외곽선 진하기(끝낼 수 있으면 1).</summary>
    private const float LockedOutline = 0.35f;

    [SerializeField] private KeyCode interactKey = KeyCode.E;

    [Header("디버그")]
    [SerializeField] private bool logActions = true;

    private bool _ownsPrompt;
    private float _armedUntil = -1f;
    private bool _ended;

    private static ShiftEndPhone s_active;

    /// <summary>씬의 전화기. 없으면 null.</summary>
    public static ShiftEndPhone Active
    {
        get { return s_active; }
    }

    /// <summary>지금 겨누고 있고, 누르면 근무가 끝나는(또는 확인을 기다리는) 상태인가. 시험용.</summary>
    public bool IsReady { get; private set; }

    /// <summary>확인(두 번째 [E])을 기다리는 중인가.</summary>
    public bool IsArmed
    {
        get { return _armedUntil > Time.unscaledTime; }
    }

    private void OnEnable()
    {
        s_active = this;
    }

    private void OnDisable()
    {
        if (s_active == this) s_active = null;
        ReleasePrompt();
    }

    private void Update()
    {
        IsReady = false;
        if (_ended || !CanInteract() || !IsAimed())
        {
            _armedUntil = -1f;
            ReleasePrompt();
            return;
        }

        if (!NightRun.IsNightActive)
        {
            ReleasePrompt();
            return;
        }

        if (!NightRun.CanEndShiftEarly)
        {
            _armedUntil = -1f;
            InteractionOutline.Request(transform, LockedOutline);   // 겨누면 늘 보이게 — 아직 못 쓰니 흐리게.
            int left = NightRun.Inspections.RemainingCount;
            ClaimPrompt(left > 0 ? "점검을 모두 마쳐야 근무를 끝낼 수 있습니다 · 남은 점검 " + left + "건" : string.Empty, false);
            return;
        }

        IsReady = true;
        InteractionOutline.Request(transform);
        ClaimPrompt(IsArmed ? "[E] 한 번 더 — 근무를 종료합니다" : "[E] 근무 종료 보고", true);

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(interactKey))
        {
            if (IsArmed)
            {
                EndShift();
            }
            else
            {
                _armedUntil = Time.unscaledTime + ConfirmSeconds;
            }
        }
#endif
    }

    /// <summary>
    /// 근무를 끝낸다(확인 없이). 끝낼 수 없으면 false. 디버그·시험이 부른다 — 플레이어는 [E] 두 번.
    /// </summary>
    public bool EndShift()
    {
        if (_ended || !NightRun.CanEndShiftEarly) return false;

        bool ok = NightRun.RequestEndNight();
        if (logActions) Debug.Log("[전화기] 근무 종료 보고 → " + (ok ? "결과창으로" : "거절됨"), this);
        if (ok)
        {
            _ended = true;
            _armedUntil = -1f;
            ReleasePrompt();
        }

        return ok;
    }

    private bool IsAimed()
    {
        Camera cam = Camera.main;
        if (cam == null) return false;

        RaycastHit hit;
        if (!Physics.SphereCast(cam.transform.position, AimRadius, cam.transform.forward, out hit, Reach, ~0,
                QueryTriggerInteraction.Ignore))
        {
            return false;
        }

        return hit.transform == transform || hit.transform.IsChildOf(transform);
    }

    private static bool CanInteract()
    {
        if (Time.timeScale <= 0f || PlayerSensors.TabletRaised || NightRun.IsCaptured) return false;   // 태블릿을 든 동안엔 전화를 받지 않는다
        CctvSystem cctv = CctvSystem.Active;
        if (cctv != null && cctv.IsViewing) return false;

        PlayerTablet[] tablets = FindObjectsByType<PlayerTablet>(FindObjectsSortMode.None);
        for (int i = 0; i < tablets.Length; i++)
        {
            if (tablets[i].IsOpened) return false;   // 태블릿을 펼친 채로는 수화기를 들지 않는다.
        }

        return true;
    }

    private void ClaimPrompt(string line, bool hot)
    {
        if (line.Length == 0)
        {
            ReleasePrompt();
            return;
        }

        _ownsPrompt = true;
        InteractionHud.ExternalPrompt = line;
        InteractionHud.ExternalHot = hot;
    }

    private void ReleasePrompt()
    {
        if (!_ownsPrompt) return;
        _ownsPrompt = false;
        InteractionHud.ExternalPrompt = string.Empty;
        InteractionHud.ExternalHot = false;
    }
}

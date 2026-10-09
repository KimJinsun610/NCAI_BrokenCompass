using NightDuty;
using UnityEngine;

/// <summary>
/// 경비실 근무일지 — <b>02:16 체크포인트 서명</b>을 하는 곳(54차 QA: <see cref="NightRun.SignCheckpoint"/>를 부르는 곳이 없어
/// [근무 지시] W5 「근무일지 서명 바랍니다」를 실제 플레이어가 끝낼 수 없었고 체크포인트도 생기지 않았다).
/// <list type="bullet">
/// <item><b>언제</b>: 코어가 정한다 — <see cref="NightRun.CanSignCheckpointNow"/>(이완 구간 01:52~02:16 · 아직 서명 전 · 붙잡히지 않음). 그 밖에는 콜라이더를 꺼 두어 아무것도 뜨지 않고 조준도 가리지 않는다.</item>
/// <item><b>조작</b>: 경비 책상 위 펼친 장부를 <see cref="Reach"/> 안에서 겨누면 외곽선과 「[E] 근무일지 서명」, [E]로 서명.
/// 조준·막는 조건·안내 줄은 <see cref="ShiftEndPhone"/>·<see cref="CctvSystem"/>와 같다 — 조준선이 <b>맨 먼저</b> 맞힌 것만 보므로
/// 장부를 겨눈 프레임에는 문·모니터·전화기가 [E]를 받지 않는다.</item>
/// <item><b>되먹임</b>: 연출 소리 표에 <see cref="SoundKey"/>가 있으면 그 소리. 완료 답장(「근무일지 서명이 기록되었습니다. 01:53」)은 W5가 진행 중이면 태블릿으로 온다.</item>
/// </list>
/// 씬은 고치지 않는다 — <see cref="DutyStage"/>가 밤이 열릴 때 책상 위 장부 자리에 런타임 조준 상자로 세운다.
/// </summary>
[DisallowMultipleComponent]
public sealed class DutyLogBook : MonoBehaviour
{
    /// <summary>손이 닿는 거리(m).</summary>
    public const float Reach = 1.6f;

    /// <summary>안내 줄.</summary>
    public const string PromptText = "[E] 근무일지 서명";

    /// <summary>서명 소리 키(연출 소리 표). 지금은 표에 없어 소리 없이 태블릿 답장만 온다.</summary>
    public const string SoundKey = "duty.sign";

    private const float AimRadius = 0.06f;

    [SerializeField] private KeyCode interactKey = KeyCode.E;

    [Tooltip("외곽선을 그릴 장부 메시. 비우면 자기 자신.")]
    [SerializeField] private Transform outlineTarget;

    [Header("디버그")]
    [SerializeField] private bool logActions = true;

    private Collider _collider;
    private bool _ownsPrompt;

    /// <summary>씬의 근무일지. 없으면 null.</summary>
    public static DutyLogBook Active { get; private set; }

    /// <summary>지금 겨누고 있고 [E]를 누르면 서명하는 상태인가. 시험용.</summary>
    public bool IsReady { get; private set; }

    /// <summary>외곽선을 그릴 장부 메시를 정한다.</summary>
    public void SetOutlineTarget(Transform target)
    {
        outlineTarget = target;
    }

    private void OnEnable()
    {
        Active = this;
        _collider = GetComponent<Collider>();
    }

    private void OnDisable()
    {
        if (Active == this) Active = null;
        IsReady = false;
        ReleasePrompt();
    }

    private void Update()
    {
        IsReady = false;
        bool open = NightRun.CanSignCheckpointNow;
        if (_collider != null && _collider.enabled != open) _collider.enabled = open;
        if (!open || !CanInteract() || !IsAimed())
        {
            ReleasePrompt();
            return;
        }

        IsReady = true;
        InteractionOutline.Request(outlineTarget != null ? outlineTarget : transform);
        ClaimPrompt();

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(interactKey))
        {
            Sign();
        }
#endif
    }

    /// <summary>서명한다([E]와 같은 처리 — 조준 검사 없이). 서명할 수 없으면 false. 디버그·시험이 부른다.</summary>
    public bool Sign()
    {
        if (!NightRun.SignCheckpoint()) return false;
        IsReady = false;
        ReleasePrompt();
        PlaySound();
        if (logActions) Debug.Log("[근무일지] 서명 — 02:16 체크포인트", this);
        return true;
    }

    private void PlaySound()
    {
        float volume;
        AudioClip clip = DirectionSoundTableSO.FindExact(SoundKey, out volume);
        if (clip == null) return;
        GameObject go = new GameObject("sfx " + SoundKey);
        go.transform.position = transform.position;
        AudioSource s = go.AddComponent<AudioSource>();
        s.playOnAwake = false;
        s.clip = clip;
        s.volume = volume;
        s.spatialBlend = 1f;
        s.dopplerLevel = 0f;
        s.minDistance = 1f;
        s.maxDistance = 12f;
        s.Play();
        Destroy(go, clip.length + 0.2f);
    }

    private bool IsAimed()
    {
        Camera cam = Camera.main;
        if (cam == null) return false;

        RaycastHit hit;
        if (!Physics.SphereCast(cam.transform.position, AimRadius, cam.transform.forward, out hit, Reach, ~0, QueryTriggerInteraction.Ignore))
        {
            return false;
        }

        return hit.transform == transform || hit.transform.IsChildOf(transform);
    }

    private static bool CanInteract()
    {
        if (Time.timeScale <= 0f || PlayerSensors.TabletRaised || NightRun.IsCaptured) return false;
        CctvSystem cctv = CctvSystem.Active;
        if (cctv != null && cctv.IsViewing) return false;

        PlayerTablet[] tablets = FindObjectsByType<PlayerTablet>(FindObjectsSortMode.None);
        for (int i = 0; i < tablets.Length; i++)
        {
            if (TabletZoom.Reading(tablets[i])) return false;
        }

        return true;
    }

    private void ClaimPrompt()
    {
        _ownsPrompt = true;
        InteractionHud.ExternalPrompt = PromptText;
        InteractionHud.ExternalHot = true;
    }

    private void ReleasePrompt()
    {
        if (!_ownsPrompt) return;
        _ownsPrompt = false;
        InteractionHud.ExternalPrompt = string.Empty;
        InteractionHud.ExternalHot = false;
    }
}

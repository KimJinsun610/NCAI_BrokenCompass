using NightDuty;
using UnityEngine;

/// <summary>
/// 65차 [근무 지시] 줍기(W15 「도서관 안쪽 책상에 있는 biology 책을 습득하세요.」 — 민: 「클릭해서 습득하면 완료」).
/// <list type="bullet">
/// <item><b>언제</b>: 코어가 정한다 — 그 물건을 바라는 줍기 지시가 진행 중일 때만(<see cref="NightRun.DutyWants"/>) 겨누면 외곽선과 「[E] … 줍기」. 그 밖에는 그냥 책이다.</item>
/// <item><b>조작</b>: <see cref="Reach"/> 안에서 겨누고 [E] → 책이 사라지고(렌더러·콜라이더 끔) <see cref="NightRun.PickUpDutyItem"/> → 완료 답장. 조준·막는 조건·안내 줄은 <see cref="DutyLogBook"/>과 같다.</item>
/// <item><b>되돌림</b>: 그날 지시기에서 그 지시가 끝난 동안만 숨는다 — 새 밤(지시기가 바뀜)이나 줍기 전 체크포인트로 재시작하면 다시 놓인다.</item>
/// </list>
/// 씬은 고치지 않는다 — <see cref="DutyStage"/>가 밤이 열릴 때 씬의 책에 붙인다.
/// </summary>
[DisallowMultipleComponent]
public sealed class DutyPickupItem : MonoBehaviour
{
    /// <summary>손이 닿는 거리(m).</summary>
    public const float Reach = 2.1f;

    /// <summary>줍는 소리 키(연출 소리 표).</summary>
    public const string SoundKey = "duty.pickup";

    private const float AimRadius = 0.06f;

    private string _target = string.Empty;
    private string _dutyId = string.Empty;
    private string _prompt = "[E] 줍기";
    private Renderer[] _renderers = new Renderer[0];
    private Collider[] _colliders = new Collider[0];
    private DutyDispatcher _takenIn;
    private bool _ownsPrompt;
    private bool _hidden;
    private Renderer _staticOriginal;
    private GameObject _visual;

    /// <summary>102차: 정적 배칭 원본 대신 세운 깨끗한 복제(없으면 null). 시험·검수용.</summary>
    public GameObject Visual
    {
        get { return _visual; }
    }

    /// <summary>지금 겨누고 있고 [E]를 누르면 줍는 상태인가. 시험용.</summary>
    public bool IsReady { get; private set; }

    /// <summary>지금 주워져 숨어 있는지.</summary>
    public bool Taken
    {
        get { return _hidden; }
    }

    /// <summary>줍는 물건 ID · 지시 ID · 안내 줄을 정한다.</summary>
    public void Setup(string target, string dutyId, string prompt)
    {
        _target = target ?? string.Empty;
        _dutyId = dutyId ?? string.Empty;
        _prompt = string.IsNullOrEmpty(prompt) ? "[E] 줍기" : prompt;
        Renderer[] all = GetComponentsInChildren<Renderer>(true);
        System.Collections.Generic.List<Renderer> shown = new System.Collections.Generic.List<Renderer>(all.Length);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != _staticOriginal) shown.Add(all[i]);   // 102차: 숨긴 정적 원본은 다시 켜지 않는다
        }

        _renderers = shown.ToArray();
        _colliders = GetComponentsInChildren<Collider>(true);
    }

    /// <summary>
    /// 102차(민: 「biology 책 텍스쳐 안보임」): 씬의 책은 정적 배칭에 묶여 근무 씬에서 새까만 판으로 그려졌다(합친 메시 · 라이트맵을 꺼도 같음).
    /// 그 렌더러를 끄고, 같은 원본 프리팹을 감싼 대역(<paramref name="resourcePath"/>, Resources)을 자식으로 같은 자리에 세운다 — 콜라이더는 원본 것만 쓴다.
    /// 원본이 정적 배칭이 아니거나 대역이 없으면 아무것도 하지 않는다. <see cref="Setup"/>보다 먼저 부른다.
    /// </summary>
    public bool UseCleanVisual(string resourcePath)
    {
        if (_visual != null) return true;
        Renderer own = GetComponent<Renderer>();
        if (own == null || !own.isPartOfStaticBatch) return false;
        GameObject source = Resources.Load<GameObject>(resourcePath);
        if (source == null)
        {
            Debug.LogWarning("[DutyPickupItem] 대역 프리팹이 없습니다: " + resourcePath, this);
            return false;
        }

        _visual = Instantiate(source, transform, false);
        _visual.name = source.name + " (보이는 모형)";
        _visual.transform.localPosition = Vector3.zero;
        _visual.transform.localRotation = Quaternion.identity;
        _visual.transform.localScale = Vector3.one;
        foreach (Collider c in _visual.GetComponentsInChildren<Collider>(true)) DestroyImmediate(c);   // 겨눔은 원본 상자 콜라이더
        foreach (Renderer r in _visual.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = own.shadowCastingMode;
        own.enabled = false;
        _staticOriginal = own;
        return true;
    }

    private void OnDisable()
    {
        IsReady = false;
        ReleasePrompt();
        SetHidden(false);
    }

    private void Update()
    {
        IsReady = false;
        DutyDispatcher duties = NightRun.Duties;
        bool taken = _takenIn != null && ReferenceEquals(duties, _takenIn) && duties.Finished(_dutyId);
        if (!taken) _takenIn = null;
        SetHidden(taken);

        if (taken || !NightRun.DutyWants(_target) || !CanInteract() || !IsAimed())
        {
            ReleasePrompt();
            return;
        }

        IsReady = true;
        InteractionOutline.Request(transform);
        ClaimPrompt();

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.E))
        {
            PickUp();
        }
#endif
    }

    /// <summary>줍는다([E]와 같은 처리 — 조준 검사 없이). 지시가 없으면 false. 디버그·시험이 부른다.</summary>
    public bool PickUp()
    {
        DutyDispatcher duties = NightRun.Duties;
        if (!NightRun.PickUpDutyItem(_target)) return false;
        _takenIn = duties;
        IsReady = false;
        ReleasePrompt();
        SetHidden(true);
        PlaySound();
        Debug.Log("[근무 지시] 주움 — " + _target, this);
        return true;
    }

    private void SetHidden(bool hidden)
    {
        if (_hidden == hidden) return;
        _hidden = hidden;
        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] != null) _renderers[i].enabled = !hidden;
        }

        for (int i = 0; i < _colliders.Length; i++)
        {
            if (_colliders[i] != null) _colliders[i].enabled = !hidden;
        }
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
        InteractionHud.ExternalPrompt = _prompt;
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

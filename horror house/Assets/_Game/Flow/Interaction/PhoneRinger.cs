using NightDuty;
using UnityEngine;

/// <summary>
/// 79차 K5 「경비실 전화는 세 번 울린 뒤에 받으십시오.」 — 경비실 전화기(<see cref="ShiftEndPhone"/>)를 울린다.
/// <list type="bullet">
/// <item>수칙 단서(<see cref="FinalCues.PhoneRing"/>)가 오면 연출(<c>DirectionStage</c>)이 <see cref="Ring"/>을 부른다 —
/// 벨(<c>phone.ring</c> = 민 지정 Phone_ring_3)을 <see cref="AnswerAfterRingsJudge.RingPeriod"/>초마다, 주어진 시간 동안.</item>
/// <item>울리는 동안 전화기를 겨누고 [E] → 받음(<see cref="JudgeSignal.PhoneAnswer"/>) — 벨이 그치고 수화기 너머 거친 숨(<c>phone.breath</c>)이 들린다.
/// 몇 번째 벨에서 받았는지는 판정(<see cref="AnswerAfterRingsJudge"/>)이 잰다.</item>
/// <item>울리는 동안 근무 종료 전화(<see cref="ShiftEndPhone"/>)는 쉰다.</item>
/// </list>
/// </summary>
[DisallowMultipleComponent]
public sealed class PhoneRinger : MonoBehaviour
{
    /// <summary>벨 소리 이름(연출 소리 표).</summary>
    public const string RingSound = "phone.ring";

    /// <summary>받았을 때 수화기 너머 소리.</summary>
    public const string BreathSound = "phone.breath";

    /// <summary>103차: 수화기 숨소리 3D 최소 거리(m) — 받는 자리(1.8m 안)에서 줄지 않게.</summary>
    public const float BreathMinDistance = 2f;

    /// <summary>받기 안내.</summary>
    public const string AnswerLine = "[E] 전화 받기";

    /// <summary>80차: 벨 볼륨(표 값보다 작으면 이 값).</summary>
    public const float BellVolume = 1f;

    /// <summary>80차: 벨 3D 비율(1 = 완전 3D). 절반은 가운데로 들려 작아지지 않는다.</summary>
    public const float BellSpatial = 0.55f;

    private const float Reach = 1.8f;
    private const float AimRadius = 0.06f;

    private static PhoneRinger s_active;

    private AudioSource _bell;
    private float _until = -1f;
    private float _nextRing;
    private bool _ownsPrompt;

    /// <summary>지금 울리는 중인지.</summary>
    public static bool IsRinging
    {
        get { return s_active != null && s_active._until > Time.time; }
    }

    /// <summary>울린 벨 수(이번 울림, 시험·디버그).</summary>
    public int Rings { get; private set; }

    /// <summary>경비실 전화기를 <paramref name="seconds"/>초 동안 울린다. 전화기가 없으면 false.</summary>
    public static bool Ring(float seconds)
    {
        ShiftEndPhone phone = ShiftEndPhone.Active;
        if (phone == null) return false;
        PhoneRinger r = phone.GetComponent<PhoneRinger>();
        if (r == null) r = phone.gameObject.AddComponent<PhoneRinger>();
        r.Begin(seconds);
        return true;
    }

    /// <summary>받는다(디버그·시험 — 플레이어는 [E]). 울리는 중이 아니면 false.</summary>
    public static bool Answer()
    {
        if (!IsRinging) return false;
        s_active.DoAnswer();
        return true;
    }

    private void Begin(float seconds)
    {
        s_active = this;
        _until = Time.time + seconds;
        _nextRing = Time.time;
        Rings = 0;
        enabled = true;
    }

    private void OnDisable()
    {
        Stop();
        if (s_active == this) s_active = null;
    }

    private void Update()
    {
        if (_until <= Time.time || !NightRun.IsNightActive || NightRun.IsCaptured)
        {
            Stop();
            return;
        }

        if (Time.time >= _nextRing)
        {
            _nextRing += AnswerAfterRingsJudge.RingPeriod;
            PlayBell();
        }

        if (!Aimed())
        {
            ReleasePrompt();
            return;
        }

        InteractionOutline.Request(transform);
        _ownsPrompt = true;
        InteractionHud.ExternalPrompt = AnswerLine;
        InteractionHud.ExternalHot = true;
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.E)) DoAnswer();
#endif
    }

    private void DoAnswer()
    {
        Debug.Log("[전화기] K5 전화 받음 — 벨 " + Rings + "번째", this);
        NightRun.Send(JudgeSignal.PhoneAnswer());
        Stop();
        // 103차(사용자: 「전화기 호흡 소리 — 방향이 구분이 안 됨」): 2D 가운데(80차)였던 것을 전화기 자리의 3D로 — 수화기 쪽에서 들린다.
        // (80차 「오른쪽에서만 들린다」는 귀 오른쪽 0.15m에 둔 탓이었다 — 전화기는 조준한 정면에 있다.)
        DirectionStage.PlaySound(BreathSound, transform.position + Vector3.up * 0.1f, BreathMinDistance, 1f);
    }

    private void Stop()
    {
        _until = -1f;
        if (_bell != null) _bell.Stop();
        ReleasePrompt();
    }

    private void PlayBell()
    {
        float volume;
        AudioClip clip = DirectionSoundTableSO.FindExact(RingSound, out volume);
        if (clip == null) return;
        if (_bell == null)
        {
            _bell = gameObject.AddComponent<AudioSource>();
            // 80차(민: 「벨소리가 너무 작다」): 경비실 밖 복도에서도 또렷하게 — 가까이 8m까지는 줄지 않고, 절반은 2D라 방향만 살짝.
            _bell.spatialBlend = BellSpatial;
            _bell.rolloffMode = AudioRolloffMode.Linear;
            _bell.minDistance = 8f;
            _bell.maxDistance = 45f;
            _bell.dopplerLevel = 0f;
            _bell.playOnAwake = false;
            NightDutyMixer.Route(_bell, NightDutyMixer.Bus.Direction);
        }

        _bell.clip = clip;
        _bell.volume = Mathf.Max(volume, BellVolume);
        _bell.Play();
        Rings++;
    }

    private bool Aimed()
    {
        if (Time.timeScale <= 0f || PlayerSensors.TabletRaised) return false;
        Camera cam = Camera.main;
        if (cam == null) return false;
        RaycastHit hit;
        if (!Physics.SphereCast(cam.transform.position, AimRadius, cam.transform.forward, out hit, Reach, ~0, QueryTriggerInteraction.Ignore)) return false;
        return hit.transform == transform || hit.transform.IsChildOf(transform);
    }

    private void ReleasePrompt()
    {
        if (!_ownsPrompt) return;
        _ownsPrompt = false;
        InteractionHud.ExternalPrompt = string.Empty;
        InteractionHud.ExternalHot = false;
    }
}

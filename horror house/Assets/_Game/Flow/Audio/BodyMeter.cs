using System.Collections.Generic;
using NightDuty;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 몸 계기(2′단계, 2026-10-04 37차) — 생존 수치를 몸 소리로 들려준다. HUD는 없다. 규칙(층·주기·경계)은 코어 <see cref="BodyRules"/>.
/// <list type="bullet">
/// <item><b>청각 = 심박</b>(저음·가운데): 층마다 심박 루프가 62 → 72 → 86 → 104로 바뀌고, 층 2부터 귀 먹먹함, 층 3부터 이명이 더해진다.
/// 층 4는 화면이 박동에 맞춰 흔들린다(<see cref="ScreenShakeEnabled"/>로 끔 — 접근성).</item>
/// <item><b>조도 = 호흡</b>(가깝고 거친 숨): 층마다 10 → 8 → 6 → 4.5초 주기로 거친 숨 하나.</item>
/// <item><b>배치 = 내 발소리의 에코</b>: <see cref="PlayerFootsteps"/>가 <see cref="TierOf"/>를 읽어 낸다.</item>
/// <item><b>경계 신호</b>: 70·85를 그 밤 처음 넘으면 청각 심박 급등 2초 · 조도 숨 멎음 · 배치 발소리 하나 더.</item>
/// <item><b>회복</b>: 정확한 보고로 축이 내려가면 긴 한숨.</item>
/// <item>재시작 뒤 30초는 붙잡힌 축이 한 층 약하게. 밤이 아니거나 붙잡힌 동안은 모두 잦아든다.</item>
/// </list>
/// 소리는 연출 소리 표의 <c>body.*</c> 키. <b>판정과 무관하다.</b> 근무 씬에 자동으로 선다.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-500)]
public sealed class BodyMeter : MonoBehaviour
{
    /// <summary>층 4 박동 흔들림을 켤지(광과민·흔들림 옵션).</summary>
    public static bool ScreenShakeEnabled = true;

    private const float FadeSeconds = 1.5f;
    private const float ShakeDegrees = 0.55f;

    private static readonly FearAxis[] Sensory = { FearAxis.Auditory, FearAxis.Illuminance, FearAxis.Layout };
    private static BodyMeter s_active;

    private readonly string[] _heartKeys = { string.Empty, "body.heart.62", "body.heart.72", "body.heart.86", "body.heart.104" };
    private AudioSource[] _heart = new AudioSource[5];
    private AudioSource _ear;
    private AudioSource _tinnitus;
    private AudioSource _oneShot;
    private readonly Dictionary<AudioSource, float> _targets = new Dictionary<AudioSource, float>();

    private readonly int[] _last = new int[3];
    private readonly int[] _crossed = new int[3];
    private bool _known;
    private InspectionPlan _plan;
    private float _breathTimer = -1f;
    private float _followUp = -1f;
    private AudioSource _breath;
    private AudioClip _lastBreath;
    private FearAxis _weakAxis;
    private float _weakUntil = -1f;

    private Transform _cam;
    private Quaternion _shakeOffset = Quaternion.identity;
    private bool _shakeApplied;
    private int _lastBeat = -1;
    private float _kick;

    /// <summary>지금 근무 씬의 것. 없으면 null.</summary>
    public static BodyMeter Active
    {
        get { return s_active; }
    }

    /// <summary>
    /// 그 축의 지금 몸 계기 층(0~4). 밤이 아니거나 붙잡힌 동안 0, 재시작 뒤 30초는 붙잡힌 축이 한 층 약하다.
    /// </summary>
    public static int TierOf(FearAxis axis)
    {
        if (!NightRun.IsNightActive || NightRun.IsCaptured || NightRun.Axes == null) return 0;
        int tier = BodyRules.Tier(NightRun.Axes.GetValue(axis));
        BodyMeter m = s_active;
        if (m != null && axis == m._weakAxis && Time.time < m._weakUntil) tier = BodyRules.Weakened(tier);
        return tier;
    }

    /// <summary>놀람 — 심박 급등 2초(경계 신호와 같은 소리). 밤이 아니거나 붙잡힌 동안은 없다. 조우 대면이 부른다(44차 EncounterImpact).</summary>
    public static void Startle()
    {
        BodyMeter m = s_active;
        if (m == null || !NightRun.IsNightActive || NightRun.IsCaptured) return;
        m.Play2D("body.heart.boundary");
    }

    // ── 자동 설치 ─────────────────────────────────────────────

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Install()
    {
        s_active = null;
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
        if (!FlowAutoInstall.IsDutyScene(scene) || FlowAutoInstall.Exists<BodyMeter>(scene)) return;
        FlowAutoInstall.CreateHost<BodyMeter>(scene, "BodyMeter (auto)");
    }

    private void OnEnable()
    {
        s_active = this;
        EventBus.InspectionReported += OnReported;
        EventBus.NightRestarted += OnRestarted;
        EventBus.Captured += OnCaptured;
    }

    private void OnDisable()
    {
        EventBus.InspectionReported -= OnReported;
        EventBus.NightRestarted -= OnRestarted;
        EventBus.Captured -= OnCaptured;
        RemoveShake();
        if (s_active == this) s_active = null;
    }

    private void Awake()
    {
        for (int i = 1; i < _heart.Length; i++) _heart[i] = MakeLoop(_heartKeys[i]);
        _ear = MakeLoop("body.ear");
        _tinnitus = MakeLoop("body.tinnitus");
        _oneShot = gameObject.AddComponent<AudioSource>();
        _oneShot.playOnAwake = false;
        _oneShot.spatialBlend = 0f;
        _oneShot.priority = 20;
    }

    private AudioSource MakeLoop(string key)
    {
        float volume;
        AudioClip clip = DirectionSoundTableSO.FindExact(key, out volume);
        GameObject go = new GameObject("body " + key);
        go.transform.SetParent(transform, false);
        AudioSource s = go.AddComponent<AudioSource>();
        s.playOnAwake = false;
        s.loop = true;
        s.clip = clip;
        s.spatialBlend = 0f;
        s.priority = 24;
        s.volume = 0f;
        _targets[s] = volume;
        return s;
    }

    // ── 매 프레임 ─────────────────────────────────────────────

    private void Update()
    {
        RemoveShake();   // 지난 프레임에 얹은 흔들림을 걷어 낸다(카메라 조작이 그 위에 쌓이지 않게)

        bool live = NightRun.IsNightActive && !NightRun.IsCaptured && NightRun.Axes != null;
        if (!ReferenceEquals(NightRun.Inspections != null ? NightRun.Inspections.Plan : null, _plan))
        {
            _plan = NightRun.Inspections != null ? NightRun.Inspections.Plan : null;
            _known = false;
            for (int i = 0; i < _crossed.Length; i++) _crossed[i] = 0;   // 새 밤 — 경계 신호를 다시 낼 수 있다
        }

        int hear = live ? TierOf(FearAxis.Auditory) : 0;
        int light = live ? TierOf(FearAxis.Illuminance) : 0;
        float dt = Time.unscaledDeltaTime;

        for (int i = 1; i < _heart.Length; i++) Fade(_heart[i], i == hear, dt);
        Fade(_ear, BodyRules.EarPressure(hear), dt);
        Fade(_tinnitus, BodyRules.Tinnitus(hear), dt);

        if (live) Boundaries();
        Breath(light, dt);
        Pulse(hear, dt);
    }

    private void LateUpdate()
    {
        ApplyShake();
    }

    private void Fade(AudioSource s, bool on, float dt)
    {
        if (s == null || s.clip == null) return;
        float full;
        if (!_targets.TryGetValue(s, out full)) full = 1f;
        float target = on ? full : 0f;
        s.volume = Mathf.MoveTowards(s.volume, target, dt / FadeSeconds);
        if (s.volume > 0.0005f)
        {
            if (!s.isPlaying) s.Play();
        }
        else if (s.isPlaying)
        {
            s.Stop();
        }
    }

    private void Boundaries()
    {
        for (int i = 0; i < Sensory.Length; i++)
        {
            int now = NightRun.Axes.GetValue(Sensory[i]);
            if (!_known)
            {
                _last[i] = now;
                // 이미 넘어 있는 경계는 낸 것으로 친다(밤 시작·씬 진입에 한꺼번에 울리지 않게).
                for (int b = 0; b < BodyRules.Boundaries.Length; b++)
                {
                    if (now >= BodyRules.Boundaries[b]) _crossed[i] |= 1 << b;
                }

                continue;
            }

            int bits = BodyRules.CrossedBoundaries(_last[i], now, _crossed[i]);
            _last[i] = now;
            if (bits == 0) continue;
            _crossed[i] |= bits;
            BoundarySignal(Sensory[i]);
        }

        _known = true;
    }

    private void BoundarySignal(FearAxis axis)
    {
        switch (axis)
        {
            case FearAxis.Auditory:
                Play2D("body.heart.boundary");
                break;
            case FearAxis.Illuminance:
                Play2D("body.breath.stop");
                _breathTimer = Mathf.Max(_breathTimer, 3f);   // 숨이 멎은 뒤 잠깐은 거친 숨이 오지 않는다
                break;
            default:
                PlayerFootsteps steps = PlayerFootsteps.Active;
                if (steps != null) steps.GhostStep();
                break;
        }
    }

    private void Breath(int tier, float dt)
    {
        float interval = BodyRules.BreathInterval(tier);
        if (interval <= 0f)
        {
            _breathTimer = -1f;
            return;
        }

        if (_breathTimer < 0f) _breathTimer = Random.Range(1.5f, interval);   // 층에 막 들어서면 곧 한 번
        _breathTimer -= dt;
        if (_followUp > 0f)
        {
            _followUp -= dt;
            if (_followUp <= 0f) Breathe(tier, 0.6f);
        }

        if (_breathTimer > 0f) return;

        // 44차: 같은 숨이 메트로놈처럼 6초마다 들려 배경음이 됐다 — 간격을 크게 흔들고, 가끔 숨을 참고(건너뜀), 가끔 짧게 한 번 더 몰아쉰다.
        _breathTimer = interval * Random.Range(0.65f, 1.45f);
        float roll = Random.value;
        if (roll < 0.15f) return;   // 숨을 참는다
        Breathe(tier, 1f);
        if (roll > 0.8f) _followUp = Random.Range(0.9f, 1.5f);
    }

    /// <summary>거친 숨 하나 — 직전과 다른 판, 음높이 ±6%, 좌우 조금(전용 소스라 다른 몸 소리의 음높이를 건드리지 않는다).</summary>
    private void Breathe(int tier, float scale)
    {
        float volume;
        AudioClip clip = DirectionSoundTableSO.FindExact("body.breath", out volume);
        for (int i = 0; i < 3 && clip != null && clip == _lastBreath; i++) clip = DirectionSoundTableSO.FindExact("body.breath", out volume);
        if (clip == null) return;
        _lastBreath = clip;
        if (_breath == null)
        {
            _breath = gameObject.AddComponent<AudioSource>();
            _breath.playOnAwake = false;
            _breath.spatialBlend = 0f;
            _breath.priority = 22;
        }

        _breath.pitch = Random.Range(0.94f, 1.06f);
        _breath.panStereo = Random.Range(-0.12f, 0.12f);
        _breath.PlayOneShot(clip, volume * scale * (0.55f + 0.1f * tier) * Random.Range(0.85f, 1.05f));
        if (DirectionStage.Verbose) Debug.Log("[Body] body.breath ← " + clip.name);
    }

    // ── 층 4 박동 흔들림 ─────────────────────────────────────

    private void Pulse(int tier, float dt)
    {
        _kick = Mathf.MoveTowards(_kick, 0f, dt * 6f);
        AudioSource beat = _heart[4];
        if (!BodyRules.PulseShake(tier) || beat == null || beat.clip == null || !beat.isPlaying)
        {
            _lastBeat = -1;
            return;
        }

        int index = Mathf.FloorToInt(beat.time * BodyRules.HeartRate(4) / 60f);
        if (index != _lastBeat)
        {
            if (_lastBeat >= 0) _kick = 1f;
            _lastBeat = index;
        }
    }

    private void ApplyShake()
    {
        if (!ScreenShakeEnabled || _kick <= 0.001f || NightRun.IsCaptured) return;
        Camera cam = Camera.main;
        if (cam == null) return;
        _cam = cam.transform;
        float k = _kick * _kick;
        _shakeOffset = Quaternion.Euler(-ShakeDegrees * k, ShakeDegrees * 0.3f * k * Mathf.Sin(Time.time * 37f), 0f);
        _cam.localRotation = _cam.localRotation * _shakeOffset;
        _shakeApplied = true;
    }

    private void RemoveShake()
    {
        if (!_shakeApplied) return;
        _shakeApplied = false;
        if (_cam != null) _cam.localRotation = _cam.localRotation * Quaternion.Inverse(_shakeOffset);
    }

    // ── 사건 ─────────────────────────────────────────────────

    private void OnCaptured(FearAxis axis)
    {
        RemoveShake();   // 붙잡힘 연출이 카메라 방향을 저장하기 전에
        _kick = 0f;
    }

    private void OnReported(InspectionReport report)
    {
        if (report.Accepted && report.Change < 0) Play2D("body.sigh");   // 정확한 보고로 축이 내려갔다
    }

    private void OnRestarted(RestartResult result)
    {
        if (result.Kind == RestartKind.None) return;
        _weakAxis = result.CapturedAxis;
        _weakUntil = Time.time + BodyRules.RestartWeakSeconds;
        _known = false;   // 내려간 값에서 다시 잰다(넘었던 경계는 그 밤 동안 다시 내지 않는다)
    }

    private void Play2D(string key, float scale = 1f)
    {
        float volume;
        AudioClip clip = DirectionSoundTableSO.FindExact(key, out volume);
        if (clip == null) return;
        _oneShot.PlayOneShot(clip, volume * scale);
        if (DirectionStage.Verbose) Debug.Log("[Body] " + key + " ← " + clip.name);
    }
}

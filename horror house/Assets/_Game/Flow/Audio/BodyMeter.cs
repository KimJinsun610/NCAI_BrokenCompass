using System.Collections.Generic;
using NightDuty;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 몸 계기(2′단계, 2026-10-04 37차) — 생존 수치를 몸 소리로 들려준다. HUD는 없다. 규칙(층·주기·경계)은 코어 <see cref="BodyRules"/>.
/// <list type="bullet">
/// <item><b>청각 = 심박</b>(저음·가운데): 층마다 심박 루프가 62 → 72 → 86 → 104로 바뀌고, 층 2부터 귀 먹먹함, 층 3부터 이명이 더해진다.
/// 층 4는 화면이 박동에 맞춰 흔들린다(<see cref="ScreenShakeEnabled"/>로 끔 — 접근성). 플레이어가 한자리에 머뭇거리면 멀리서 누군가의 숨(53차 「먼 숨」, 56차에 조도에서 옮겨 옴 — 숨은 소리다).</item>
/// <item><b>조도 = 눈</b>(56차, 민: 「축의 컨셉에 맞게」): 층이 오를수록 잦아지는 짧은 눈 깜빡임(화면 0.12초 감김, <see cref="BlinkEnabled"/>로 끔 — 접근성).</item>
/// <item><b>배치 = 내 발소리의 에코</b>: <see cref="PlayerFootsteps"/>가 <see cref="TierOf"/>를 읽어 낸다.</item>
/// <item><b>경계 신호</b>: 70·85를 그 밤 처음 넘으면 청각 심박 급등 2초 · 조도 느린 깜빡임 · 배치 발소리 하나 더.</item>
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

    /// <summary>조도 몸 계기의 눈 깜빡임을 켤지(화면 깜빡임 옵션).</summary>
    public static bool BlinkEnabled = true;

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
    private float _lastSigh = -999f;
    private AudioSource _breath;
    private AudioClip _lastBreath;
    private FearAxis _weakAxis;
    private float _weakUntil = -1f;

    private Transform _cam;
    private Quaternion _shakeOffset = Quaternion.identity;
    private bool _shakeApplied;
    private int _lastBeat = -1;
    private float _kick;
    private float _kickDegrees = ShakeDegrees;
    private float _kickSide;

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

    /// <summary>
    /// 60차: 화면을 한 번 툭 흔든다(소년 머리 박기 — 「칠 때마다 약간의 흔들림」). <paramref name="degrees"/> = 최대 각(°). 0.2초 안에 가라앉는다.
    /// 접근성 설정(<see cref="ScreenShakeEnabled"/>)을 따른다.
    /// </summary>
    public static void Jolt(float degrees)
    {
        BodyMeter m = s_active;
        if (m == null || degrees <= 0f) return;
        m._kick = 1f;
        m._kickDegrees = degrees;
        m._kickSide = Random.Range(-1f, 1f);
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
        NightDutyMixer.Route(s, NightDutyMixer.Bus.Body);   // 67차
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
        Breath(hear, dt);   // 56차: 먼 숨은 청각
        Blink(light, dt);   // 56차: 조도 = 눈
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
                StartBlink(BodyRules.SlowBlinkSeconds);   // 56차: 숨 멎음(소리) 대신 느린 깜빡임(눈)
                break;
            default:
                PlayerFootsteps steps = PlayerFootsteps.Active;
                if (steps != null) steps.GhostStep();
                break;
        }
    }

    /// <summary>이만큼 제자리(0.6m 안)에 있으면 「소극적」으로 본다(초) — 53차.</summary>
    public const float PassiveSeconds = 12f;

    /// <summary>숨소리 사이 최소·최대(초) — 53차 민: 「불필요한 숨소리가 너무 많다 — 플레이어가 소극적일 때, 아주 멀리서, 아주 가끔」.</summary>
    public const float BreathGapMin = 55f;
    public const float BreathGapMax = 120f;

    private Vector3 _stillAt;
    private float _stillFor;
    private bool _hasStill;

    private void Breath(int tier, float dt)
    {
        // 53차: 층이 있어도, 플레이어가 한자리에 머뭇거릴 때만 — 멀리서 아주 가끔. 56차부터 청각 층을 따른다.
        PlayerSensors hub = PlayerSensors.Active;
        if (hub != null && hub.PlayerRoot != null)
        {
            Vector3 p = hub.PlayerRoot.position;
            if (!_hasStill || (p - _stillAt).sqrMagnitude > 0.36f)
            {
                _stillAt = p;
                _stillFor = 0f;
                _hasStill = true;
            }
            else
            {
                _stillFor += dt;
            }
        }

        if (tier <= 0 || BodyRules.BreathInterval(tier) <= 0f)
        {
            _breathTimer = -1f;
            return;
        }

        if (_breathTimer < 0f) _breathTimer = Random.Range(BreathGapMin, BreathGapMax) * 0.5f;
        _breathTimer -= dt;
        if (_breathTimer > 0f || _stillFor < PassiveSeconds) return;

        _breathTimer = Random.Range(BreathGapMin, BreathGapMax) / (1f + 0.15f * (tier - 1));
        if (Random.value < 0.25f) return;   // 그래도 가끔은 건너뛴다
        Breathe(tier, 1f);
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
            // 53차: 내 숨이 아니라 「저 멀리 누군가의 숨」 — 플레이어 뒤쪽 5~8m(61차, 옛 9~14m)의 3D 소리, 저역만 남긴다.
            GameObject go = new GameObject("body breath (far)");
            go.transform.SetParent(transform, false);
            _breath = go.AddComponent<AudioSource>();
            _breath.playOnAwake = false;
            _breath.spatialBlend = 1f;
            _breath.rolloffMode = AudioRolloffMode.Logarithmic;
            _breath.minDistance = 4f;   // 61차(민: 「숨소리 등 축 소리들이 너무 작다」) 2.5 → 4
            _breath.maxDistance = 30f;
            _breath.dopplerLevel = 0f;
            _breath.priority = 40;
            AudioLowPassFilter lp = go.AddComponent<AudioLowPassFilter>();
            lp.cutoffFrequency = 1800f;   // 61차: 1100 → 1800(먹먹하되 숨인 줄은 알게)
        }

        Transform player = PlayerSensors.Active != null ? PlayerSensors.Active.PlayerRoot : null;
        if (player != null)
        {
            Vector3 back = -player.forward;
            back.y = 0f;
            back = Quaternion.Euler(0f, Random.Range(-70f, 70f), 0f) * (back.sqrMagnitude > 0.01f ? back.normalized : Vector3.back);
            _breath.transform.position = player.position + back * Random.Range(5f, 8f) + Vector3.up * 0.4f;   // 61차: 9~14m → 5~8m
        }

        _breath.pitch = Random.Range(0.9f, 1.02f);
        _breath.PlayOneShot(clip, volume * scale * 0.95f * Random.Range(0.8f, 1f));   // 61차: 0.55 → 0.95
        if (DirectionStage.Verbose) Debug.Log("[Body] body.breath ← " + clip.name);
    }

    // ── 조도 = 눈 깜빡임(56차) ────────────────────────────────

    private Image _lid;
    private float _blinkTimer = -1f;
    private float _blinkFor;
    private float _blinkAt = -1f;

    /// <summary>지금 깜빡이는 중인지. 시험·디버그용.</summary>
    public bool Blinking
    {
        get { return _blinkAt >= 0f; }
    }

    /// <summary>디버그: 지금 한 번 깜빡인다(느리게면 경계 신호와 같다).</summary>
    public void DebugBlink(bool slow)
    {
        StartBlink(slow ? BodyRules.SlowBlinkSeconds : BodyRules.BlinkSeconds);
    }

    private void Blink(int tier, float dt)
    {
        float min;
        float max;
        BodyRules.BlinkGap(tier, out min, out max);
        if (max <= 0f || Time.timeScale <= 0f)
        {
            _blinkTimer = -1f;
        }
        else
        {
            if (_blinkTimer < 0f) _blinkTimer = Random.Range(min, max) * 0.5f;
            _blinkTimer -= dt;
            if (_blinkTimer <= 0f)
            {
                _blinkTimer = Random.Range(min, max);
                StartBlink(BodyRules.BlinkSeconds);
            }
        }

        // 56차 QA: 일시정지 중에는 감지 않는다 — 깜빡임은 실제 시간으로 흘러, Esc 직전에 시작한 느린 깜빡임(0.6초)이 일시정지 화면을 덮었다.
        if (Time.timeScale <= 0f || GamePause.IsPaused) _blinkAt = -1f;

        float cover = 0f;
        if (_blinkAt >= 0f)
        {
            float t = (Time.unscaledTime - _blinkAt) / Mathf.Max(0.01f, _blinkFor);
            if (t >= 1f) _blinkAt = -1f;
            else cover = BodyRules.BlinkCover(t);
        }

        if (!BlinkEnabled || NightRun.IsCaptured) cover = 0f;
        if (_lid == null)
        {
            if (cover <= 0f) return;
            _lid = MakeLid();
        }

        Color c = _lid.color;
        c.a = cover;
        _lid.color = c;
        if (_lid.enabled != cover > 0f) _lid.enabled = cover > 0f;
    }

    private void StartBlink(float seconds)
    {
        if (!BlinkEnabled || Time.timeScale <= 0f || GamePause.IsPaused) return;
        _blinkFor = seconds;
        _blinkAt = Time.unscaledTime;
        if (DirectionStage.Verbose) Debug.Log("[Body] 눈 깜빡임 " + seconds.ToString("F2") + "초");
    }

    private Image MakeLid()
    {
        GameObject go = new GameObject("body blink (eyelid)");
        go.transform.SetParent(transform, false);
        Canvas canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 300;   // 김진선님 페이드(200) 위, 피날레(450)·붙잡힘(500) 아래
        GameObject img = new GameObject("lid");
        img.transform.SetParent(go.transform, false);
        Image lid = img.AddComponent<Image>();
        lid.color = new Color(0f, 0f, 0f, 0f);
        lid.raycastTarget = false;
        RectTransform rt = lid.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        lid.enabled = false;
        return lid;
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
            if (_lastBeat >= 0)
            {
                _kick = 1f;
                _kickDegrees = ShakeDegrees;
                _kickSide = 0f;
            }
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
        _shakeOffset = Quaternion.Euler(-_kickDegrees * k, _kickDegrees * k * (0.3f * Mathf.Sin(Time.time * 37f) + 0.5f * _kickSide), 0f);
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

    /// <summary>
    /// 정확한 보고 뒤 안도의 한숨(작게, 90초에 한 번까지). 53차 플레이 점검에서 끈다 — 민: 「숨소리는 플레이어가 소극적일 때, 아주 멀리서, 아주 가끔만」.
    /// 내 숨이 아닌 「먼 숨」 하나만 남긴다. 되살리려면 true.
    /// </summary>
    public static bool ReliefSigh = false;

    private void OnReported(InspectionReport report)
    {
        if (!ReliefSigh) return;
        if (report.Accepted && report.Change < 0 && Time.time - _lastSigh > 90f)
        {
            _lastSigh = Time.time;
            Play2D("body.sigh", 0.45f);
        }
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

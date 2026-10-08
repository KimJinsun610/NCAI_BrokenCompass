using NightDuty;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 손전등 배터리의 화면 쪽(56차) — 켜 둔 동안 닳게 하고, 충전량대로 빛을 약하게·누렇게·깜빡이게 하고, R로 갈아 끼우고,
/// 태블릿 상태바(「●●○  ■■□」의 손전등 칸)에 충전량·예비 수를 적는다. 규칙 값은 코어 <see cref="BatteryRules"/>, 상태는 <see cref="NightRun.Battery"/>.
/// <list type="bullet">
/// <item><b>판정</b>: 희미해도 「켜짐」. 0%가 되는 순간에만 <see cref="FlashlightRelay.SetOn"/>(false) — 기존 「꺼짐」 신호. 10% 아래의 비춤 거리 5m는 코어(<see cref="NightRun.BeamRange"/>)가 판정기에 준다.</item>
/// <item><b>깜빡임</b>: 30% 아래 15~30초, 10% 아래 3~6초마다 빛만 두세 번 끊긴다(판정 신호 없음). 가짜 놀람 「손전등 끊김」은 반 아래에서 나오지 않는다(<c>DirectionStage</c>).</item>
/// <item><b>갈기</b>: R(예비가 있을 때) → <see cref="BatteryRules.SwapSeconds"/>초 꺼짐(그동안은 판정도 꺼짐) → 100%, 켜져 있었으면 다시 켬. 딱깍 두 번은 김진선님 <c>FlashlightSound</c>가 켜짐·꺼짐을 보고 낸다.</item>
/// <item><b>다 닳았을 때 F</b>: 켜지지 않고 딱깍 + 0.2초 희미한 빛(<see cref="FlashlightRelay.DryClicked"/>).</item>
/// </list>
/// 배터리가 꺼져 있으면(<see cref="NightRun.BatteryEnabled"/> 거짓 — 옛 테스트·시험 씬) 아무것도 하지 않는다. 근무 씬에 자동으로 선다.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(60)]
public sealed class FlashlightPower : MonoBehaviour
{
    /// <summary>갈아 끼우는 키.</summary>
    public const KeyCode SwapKey = KeyCode.R;

    private const float DryFlashSeconds = 0.2f;
    private const float DryFlashIntensity = 0.12f;
    private static readonly Color Yellow = new Color(1f, 0.78f, 0.45f);

    private Light _spot;
    private float _baseIntensity;
    private float _baseRange;
    private Color _baseColor;
    private float _flickerTimer = -1f;
    private float _flickerAt = -1f;
    private bool _flickerDark;
    private float _swapUntil = -1f;
    private bool _swapRestoreOn;
    private float _dryUntil = -1f;
    private float _statusPoll;
    private string _signal;
    private bool _statusWritten;
    private FlashlightSound _sound;
    private AudioSource _oneShot;

    /// <summary>지금 근무 씬의 것. 없으면 null.</summary>
    public static FlashlightPower Active { get; private set; }

    /// <summary>
    /// 플레이어가 손전등을 켤 수 있는지(<see cref="FlashlightRelay.Toggle"/>이 묻는다). 배터리가 꺼져 있거나 밤이 아니면 늘 참.
    /// </summary>
    public static bool CanTurnOn
    {
        get
        {
            if (!NightRun.HasFlashlight) return false;   // 66차: 1일차는 경비실 책상의 손전등을 주워야 켤 수 있다
            FlashlightBattery b = NightRun.Battery;
            if (b == null || !NightRun.IsNightActive) return true;
            if (b.IsEmpty) return false;
            return Active == null || !Active.Swapping;
        }
    }

    /// <summary>갈아 끼우는 중인지.</summary>
    public bool Swapping
    {
        get { return _swapUntil >= 0f; }
    }

    /// <summary>갈아 끼우기 진행(0~1). 가는 중이 아니면 0. 화면 배터리 표시(<see cref="BatteryHud"/>)가 막대를 다시 채우는 데 쓴다.</summary>
    public float SwapProgress
    {
        get { return Swapping ? 1f - Mathf.Clamp01((_swapUntil - Time.time) / BatteryRules.SwapSeconds) : 0f; }
    }

    /// <summary>지금 진짜 깜빡임으로 빛이 끊겨 있는지. 시험용.</summary>
    public bool FlickerDark
    {
        get { return _flickerDark; }
    }

    // ── 자동 설치 ─────────────────────────────────────────────

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Install()
    {
        Active = null;
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
        if (!FlowAutoInstall.IsDutyScene(scene) || FlowAutoInstall.Exists<FlashlightPower>(scene)) return;
        FlowAutoInstall.CreateHost<FlashlightPower>(scene, "FlashlightPower (auto)");
    }

    private void OnEnable()
    {
        Active = this;
        FlashlightRelay.DryClicked += OnDryClick;
        EventBus.NightRestarted += OnRestarted;
    }

    private void OnDisable()
    {
        FlashlightRelay.DryClicked -= OnDryClick;
        EventBus.NightRestarted -= OnRestarted;
        RestoreLight();
        RestoreStatus();
        if (Active == this) Active = null;
    }

    // ── 공개 ─────────────────────────────────────────────────

    /// <summary>갈아 끼운다(R과 같은 처리). 예비가 없거나 이미 가는 중이면 false. 디버그·시험이 부른다.</summary>
    public bool BeginSwap()
    {
        FlashlightBattery b = NightRun.Battery;
        FlashlightRelay relay = FlashlightRelay.Active;
        if (b == null || relay == null || Swapping || b.Spare <= 0 || !NightRun.IsNightActive || NightRun.IsCaptured) return false;
        _swapRestoreOn = relay.IsOn;
        _swapUntil = Time.time + BatteryRules.SwapSeconds;
        relay.SetOn(false);
        Debug.Log("[손전등] 배터리 갈기 시작 — " + b);
        return true;
    }

    // ── 매 프레임 ─────────────────────────────────────────────

    private void Update()
    {
        FlashlightBattery b = NightRun.Battery;
        FlashlightRelay relay = FlashlightRelay.Active;
        if (b == null || relay == null)
        {
            _swapUntil = -1f;
            RestoreLight();
            RestoreStatus();
            return;
        }

        // 56차 QA: 붙잡힘 연출은 재시작(스냅샷 복원)을 카드보다 먼저 한다 — 카드·암전이 도는 18초 남짓 동안 IsCaptured가 거짓이라
        // 켜진 채 붙잡히면 복원한 충전량이 조작도 못 하는 사이 7~8% 닳았다. 연출이 끝날 때까지는 「살아 있는 밤」이 아니다.
        CaptureDirector capture = CaptureDirector.Active;
        bool live = NightRun.IsNightActive && !NightRun.IsCaptured && (capture == null || !capture.IsPlaying);
        if (!live)
        {
            _swapUntil = -1f;   // 붙잡히면 갈기는 없던 일(예비를 쓰지 않는다)
        }
        else
        {
            if (Swapping && Time.time >= _swapUntil) FinishSwap(relay, b);
            else if (!Swapping && ReadSwapInput()) BeginSwap();

            if (relay.IsOn && !Swapping)
            {
                if (b.Drain(Time.deltaTime) || b.IsEmpty)
                {
                    relay.SetOn(false);
                    Debug.Log("[손전등] 배터리가 다 닳았다 — 꺼짐");
                }
            }
        }

        ApplyLight(relay, b, live);
        UpdateStatus(b);
    }

    private bool ReadSwapInput()
    {
        if (GamePause.IsPaused || Time.timeScale <= 0f) return false;
        FPController player = FlashlightRelay.Active != null ? FlashlightRelay.Active.GetComponentInParent<FPController>() : null;
        if (player != null && !player.enabled) return false;
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(SwapKey);
#else
        return false;
#endif
    }

    private void FinishSwap(FlashlightRelay relay, FlashlightBattery b)
    {
        _swapUntil = -1f;
        if (!b.Swap()) return;
        _flickerTimer = -1f;
        if (_swapRestoreOn) relay.SetOn(true);
        Debug.Log("[손전등] 배터리 갈기 끝 — " + b);
    }

    private void OnRestarted(RestartResult result)
    {
        _swapUntil = -1f;
        _flickerAt = -1f;
        _flickerTimer = -1f;
    }

    // ── 빛 ───────────────────────────────────────────────────

    private void ApplyLight(FlashlightRelay relay, FlashlightBattery b, bool live)
    {
        if (!FindSpot(relay)) return;
        float charge = b.Charge;

        if (_dryUntil >= 0f)
        {
            if (Time.time < _dryUntil && !relay.IsOn)
            {
                _spot.intensity = _baseIntensity * DryFlashIntensity;
                _spot.range = _baseRange * BatteryRules.DimRange;
                _spot.color = Color.Lerp(_baseColor, Yellow, 0.8f);
                return;
            }

            _dryUntil = -1f;
            if (!relay.IsOn && relay.Root != null) relay.Root.SetActive(false);
        }

        _spot.intensity = _baseIntensity * BatteryRules.IntensityScale(charge);
        _spot.range = _baseRange * BatteryRules.RangeScale(charge);
        _spot.color = Color.Lerp(_baseColor, Yellow, BatteryRules.Yellowing(charge) * 0.8f);

        // 진짜 깜빡임 — 빛만(판정 신호 없음). 가짜 놀람의 끊김과 겹치지 않게 내가 끊은 동안만 enabled를 만진다.
        float min;
        float max;
        BatteryRules.FlickerGap(charge, out min, out max);
        bool want = false;
        if (live && relay.IsOn && max > 0f && !Swapping)
        {
            if (_flickerTimer < 0f) _flickerTimer = Random.Range(min, max) * 0.5f;
            _flickerTimer -= Time.deltaTime;
            if (_flickerTimer <= 0f)
            {
                _flickerTimer = Random.Range(min, max);
                _flickerAt = Time.time;
            }

            if (_flickerAt >= 0f)
            {
                float t = Time.time - _flickerAt;
                // 끊김 세 번: 0~0.07 · 0.15~0.21 · 0.30~0.40
                want = t < 0.07f || (t >= 0.15f && t < 0.21f) || (t >= 0.30f && t < 0.40f);
                if (t >= 0.40f) _flickerAt = -1f;
            }
        }
        else
        {
            _flickerTimer = -1f;
            _flickerAt = -1f;
        }

        if (want != _flickerDark)
        {
            _flickerDark = want;
            _spot.enabled = !want && relay.IsOn;
        }
    }

    private bool FindSpot(FlashlightRelay relay)
    {
        if (_spot != null) return true;
        GameObject root = relay.Root;
        Light[] lights = root != null ? root.GetComponentsInChildren<Light>(true) : relay.GetComponentsInChildren<Light>(true);
        for (int i = 0; i < lights.Length; i++)
        {
            if (lights[i] == null || lights[i].type != LightType.Spot) continue;
            _spot = lights[i];
            _baseIntensity = _spot.intensity;
            _baseRange = _spot.range;
            _baseColor = _spot.color;
            return true;
        }

        return false;
    }

    private void RestoreLight()
    {
        if (_spot == null) return;
        _spot.intensity = _baseIntensity;
        _spot.range = _baseRange;
        _spot.color = _baseColor;
        if (_flickerDark)
        {
            _flickerDark = false;
            FlashlightRelay relay = FlashlightRelay.Active;
            _spot.enabled = relay == null || relay.IsOn;
        }
    }

    private void OnDryClick()
    {
        FlashlightRelay relay = FlashlightRelay.Active;
        if (relay == null) return;
        if (!NightRun.HasFlashlight)
        {
            DeskStarterKit.ShowNoFlashlight();   // 66차: 손이 비어 있다 — 딸깍도 없다
            return;
        }

        PlayClick(0.55f, 1.15f);
        if (Swapping || relay.Root == null || NightRun.Battery == null || !NightRun.Battery.IsEmpty) return;
        // 다 닳은 손전등 — 0.2초 희미한 빛(판정 신호 없음).
        relay.Root.SetActive(true);
        if (FindSpot(relay)) _spot.enabled = true;
        _dryUntil = Time.time + DryFlashSeconds;
    }

    /// <summary>김진선님 손전등 딸깍(켜짐 소리)을 빌려 한 번 — 켜지지 않은 손전등의 헛딸깍.</summary>
    private void PlayClick(float volume, float pitch)
    {
        if (_sound == null) _sound = FindAnyObjectByType<FlashlightSound>();
        if (_sound == null) return;
        System.Reflection.FieldInfo f = typeof(FlashlightSound).GetField("onClip", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        AudioClip clip = f != null ? f.GetValue(_sound) as AudioClip : null;
        if (clip == null) return;
        if (_oneShot == null)
        {
            _oneShot = gameObject.AddComponent<AudioSource>();
            _oneShot.playOnAwake = false;
            _oneShot.spatialBlend = 0f;
        }

        _oneShot.pitch = pitch;
        _oneShot.PlayOneShot(clip, volume);
    }

    /// <summary>줍기 소리(<see cref="BatteryStash"/>가 부른다) — 낮은 딸깍.</summary>
    public void PlayPickup()
    {
        PlayClick(0.5f, 0.8f);
    }

    // ── 태블릿 상태바 ─────────────────────────────────────────

    private void UpdateStatus(FlashlightBattery b)
    {
        _statusPoll -= Time.unscaledDeltaTime;
        if (_statusPoll > 0f) return;
        _statusPoll = 0.25f;
        foreach (TabletDocument doc in FindObjectsByType<TabletDocument>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (_signal == null)
            {
                string s = doc.statusIcons ?? string.Empty;
                int gap = s.IndexOf("  ", System.StringComparison.Ordinal);
                _signal = gap >= 0 ? s.Substring(0, gap) : s;
            }

            string text = BatteryRules.StatusText(_signal, b.Charge, b.Spare);
            doc.statusIcons = text;
            if (doc.statusIconText != null && doc.statusIconText.text != text) doc.statusIconText.text = text;
        }

        _statusWritten = true;
    }

    private void RestoreStatus()
    {
        if (!_statusWritten || _signal == null) return;
        _statusWritten = false;
        string text = _signal + "  ■■□";
        foreach (TabletDocument doc in FindObjectsByType<TabletDocument>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            doc.statusIcons = text;
            if (doc.statusIconText != null) doc.statusIconText.text = text;
        }
    }
}

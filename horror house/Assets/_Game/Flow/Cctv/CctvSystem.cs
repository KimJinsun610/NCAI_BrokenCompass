using System;
using System.Collections.Generic;
using NightDuty;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// 경비실 CCTV. 공간마다 카메라 한 대, 경비실 CRT 모니터 한 대. 플레이어는 모니터 앞에서 [E]로 들여다보고
/// [A]/[D]로 채널을 넘긴다.
///
/// <para><b>씬을 고치지 않는다.</b> 카메라·화면·글씨는 전부 플레이할 때 만든다. 자리와 모양은
/// <see cref="CctvConfigSO"/>(<c>Resources/CctvConfig</c>)에서 읽는다. <see cref="PlayerInteractor"/>의 자동 설치와 같은 방식이다.</para>
///
/// <para><b>한 번에 한 대만 그린다.</b> 선택한 채널의 카메라 하나를 저해상도 렌더 텍스처 한 장에,
/// 초당 몇 번만(<see cref="CctvConfigSO.ViewFps"/>) 그린다. 끊기는 것이 CCTV답고, 비용도 거의 없다.
/// 플레이어가 경비실에 없거나 모니터가 시야 밖이면 아예 그리지 않는다.</para>
///
/// <para><b>적외선 조명.</b> 밤 씬이 거의 새까매서 카메라만으로는 아무것도 안 보인다(2026-09-30 실측).
/// 카메라마다 스포트라이트를 하나 달고, <b>그 카메라가 그리는 동안에만</b> 켠다
/// (<c>RenderPipelineManager.beginCameraRendering</c>). 플레이어 눈에는 이 빛이 보이지 않는다.
/// 같은 방법으로 <see cref="CctvOnlyVisible"/>을 CCTV에서만 보이게 한다.</para>
///
/// <para><b>판정은 모른다.</b> 이 시스템은 채널·들여다보기 상태를 이벤트로만 알린다(<see cref="ChannelChanged"/> 등).
/// K1·K2 같은 수칙은 나중에 이 이벤트를 받아 판정 신호로 옮기면 된다.</para>
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(60)]
public sealed class CctvSystem : MonoBehaviour
{
    private const string RootName = "CCTV (auto)";
    private const string ShaderName = "NightDuty/CctvScreen";

    /// <summary>손이 닿는 거리(m). <see cref="PlayerInteractor"/>와 같다.</summary>
    private const float Reach = 1.8f;

    /// <summary>조준선 굵기(m). <see cref="PlayerInteractor"/>와 같다.</summary>
    private const float AimRadius = 0.06f;

    private const string EnterPrompt = "[E] CCTV 보기";
    private const string ViewPrompt = "[A] [D] 채널 넘기기    [E] 그만 보기";

    private static readonly Plane[] s_planes = new Plane[6];
    private static CctvSystem s_active;
    private static bool s_hooked;

    /// <summary>채널이 바뀌었다. 인자는 새 채널(0부터).</summary>
    public static event Action<int> ChannelChanged;

    /// <summary>플레이어가 모니터를 들여다보기 시작했다.</summary>
    public static event Action ViewEntered;

    /// <summary>플레이어가 모니터에서 눈을 뗐다.</summary>
    public static event Action ViewExited;

    [Tooltip("비우면 Resources/CctvConfig를 읽고, 그것도 없으면 코드 기본값을 쓴다.")]
    [SerializeField] private CctvConfigSO config;

    [SerializeField] private KeyCode interactKey = KeyCode.E;

    [Header("디버그")]
    [SerializeField] private bool logActions;

    private enum ViewState
    {
        None,
        Entering,
        Viewing,
        Leaving,
    }

    private sealed class TabletState
    {
        public PlayerTablet tablet;
        public bool readInput;
    }

    private Transform _monitor;
    private RenderTexture _rt;
    private Camera[] _cams = new Camera[0];
    private Light[] _irLights = new Light[0];
    private GameObject _screen;
    private Renderer _screenRenderer;
    private Collider _screenCollider;
    private Material _mat;
    private TextMeshPro _label;
    private bool _asciiLabels;
    private GameTime _clock;
    private float _clockSearchCooldown;

    private int _channel;
    private float _renderAcc;
    private Camera _renderedCam;
    private float _static;
    private float _signal = 1f;
    private float _labelCooldown;
    private string _labelText = string.Empty;
    private bool _ownsPrompt;
    private bool _built;

    private AudioSource _hum;
    private AudioSource _staticLoop;
    private AudioSource _sfx;
    private AudioClip _switchClip;

    private ViewState _view = ViewState.None;
    private float _blend;
    private Camera _playerCam;
    private Vector3 _savedLocalPos;
    private Quaternion _savedLocalRot;
    private Vector3 _fromPos;
    private Quaternion _fromRot;
    private FPController _fp;
    private bool _fpWasEnabled;
    private readonly List<TabletState> _tablets = new List<TabletState>();
    private readonly List<Renderer> _hiddenViewmodel = new List<Renderer>();

    // ── 피날레 화면 제어(11단계) ─────────────────────────────────

    private Texture _override;
    private float _forceRenderUntil;

    /// <summary>모니터 화면 중심(월드). 화면이 없으면 영벡터.</summary>
    public Vector3 ScreenCenter
    {
        get { return _screen != null ? _screen.transform.position : Vector3.zero; }
    }

    /// <summary>화면이 방 쪽으로 향한 방향(월드). 화면이 없으면 영벡터.</summary>
    public Vector3 ScreenNormal
    {
        get { return _monitor != null ? ScreenOutward() : Vector3.zero; }
    }

    /// <summary>모니터 화면 크기(m).</summary>
    public Vector2 ScreenSize
    {
        get { return config != null ? config.ScreenSize : Vector2.zero; }
    }

    /// <summary>화면을 덮어쓰고 있는지.</summary>
    public bool HasScreenOverride
    {
        get { return _override != null; }
    }

    /// <summary>CRT 험(웅웅거림)을 켜고 끈다. 피날레 「험이 멈춘다」.</summary>
    public void SetHum(bool on)
    {
        if (_hum == null || _hum.clip == null) return;
        if (on)
        {
            _hum.volume = config.HumVolume;
            if (!_hum.isPlaying) _hum.Play();
        }
        else
        {
            _hum.Stop();
        }
    }

    /// <summary>
    /// 화면을 다른 텍스처로 덮어쓴다(검정 = 꺼진 CRT, 렌더 텍스처 = 비친 모습). null이면 채널 화면으로 돌아간다.
    /// <paramref name="gain"/>은 화면 밝기 배수(꺼진 CRT에 비친 모습은 어둡게).
    /// </summary>
    public void SetScreenOverride(Texture texture, float gain = 1f)
    {
        _override = texture;
        if (_mat == null) return;
        Texture t = texture != null ? texture : (Texture)_rt;
        _mat.mainTexture = t;
        if (_mat.HasProperty("_BaseMap")) _mat.SetTexture("_BaseMap", t);
        _mat.SetFloat("_Gain", config.Gain * (texture != null ? gain : 1f));
        _mat.SetFloat("_Noise", config.Noise * (texture != null ? 0.15f : 1f));   // 꺼진 화면에 비친 모습은 지글거리지 않는다
    }

    /// <summary>그 시간 동안 플레이어가 화면을 보든 말든 지금 채널을 매 프레임 그린다(피날레 「한 프레임 웃는 얼굴」).</summary>
    public void ForceRenderFor(float seconds)
    {
        _forceRenderUntil = Time.time + Mathf.Max(0f, seconds);
    }

    /// <summary>지금 살아 있는 CCTV. 없으면 null.</summary>
    public static CctvSystem Active
    {
        get { return s_active; }
    }

    /// <summary>채널 수.</summary>
    public int ChannelCount
    {
        get { return _cams.Length; }
    }

    /// <summary>지금 채널(0부터).</summary>
    public int CurrentChannel
    {
        get { return _channel; }
    }

    /// <summary>플레이어가 모니터를 들여다보고 있는가(들어가고 나오는 중 포함).</summary>
    public bool IsViewing
    {
        get { return _view != ViewState.None; }
    }

    /// <summary>화면 신호 세기(0 = 신호 없음, 1 = 정상). 연출이 「신호 끊김」을 낼 때 쓴다.</summary>
    public float Signal
    {
        get { return _signal; }
        set { _signal = Mathf.Clamp01(value); }
    }

    /// <summary>채널 이름.</summary>
    public string ChannelLabel(int index)
    {
        if (config == null || index < 0 || index >= config.Channels.Count)
        {
            return string.Empty;
        }

        return config.Channels[index].label;
    }

    /// <summary>채널 카메라. 연출이 화각 안에 무엇을 둘지 계산할 때 쓴다. 범위 밖이면 null.</summary>
    public Camera ChannelCamera(int index)
    {
        return index >= 0 && index < _cams.Length ? _cams[index] : null;
    }

    // ─────────────────────────────── 설치 ───────────────────────────────

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        // 도메인 리로드를 꺼도 지난 플레이의 구독이 남지 않게(CLAUDE.md §5.2-7).
        s_active = null;
        ChannelChanged = null;
        ViewEntered = null;
        ViewExited = null;
        if (s_hooked)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            s_hooked = false;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InstallForFirstScene()
    {
        // 게임은 메인 → 로딩 → 플레이 순으로 씬을 바꾼다. 첫 씬만 보면 PlayScene을 놓친다.
        if (!s_hooked)
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            s_hooked = true;
        }

        TryInstall();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TryInstall();
    }

    private static void TryInstall()
    {
        if (s_active != null)
        {
            return;
        }

        if (FindAnyObjectByType<CctvSystem>(FindObjectsInactive.Include) != null)
        {
            return;
        }

        if (FindMonitor(CctvConfigSO.Load()) == null)
        {
            return;   // 경비실 모니터가 없는 씬(메뉴·로딩·결과·시험 씬)에서는 할 일이 없다.
        }

        // hideFlags를 붙이지 않는다 — DontSave는 플레이를 꺼도 오브젝트를 남긴다(CLAUDE.md §5.5-23).
        GameObject go = new GameObject(RootName);
        go.AddComponent<CctvSystem>();
    }

    private static Transform FindMonitor(CctvConfigSO cfg)
    {
        if (cfg == null || string.IsNullOrEmpty(cfg.MonitorPath))
        {
            return null;
        }

        GameObject byPath = GameObject.Find(cfg.MonitorPath);
        if (byPath != null)
        {
            return byPath.transform;
        }

        string leaf = cfg.MonitorPath;
        int slash = leaf.LastIndexOf('/');
        if (slash >= 0)
        {
            leaf = leaf.Substring(slash + 1);
        }

        GameObject byName = GameObject.Find(leaf);
        return byName != null ? byName.transform : null;
    }

    // ─────────────────────────────── 수명 ───────────────────────────────

    private void OnEnable()
    {
        s_active = this;
        RenderPipelineManager.beginCameraRendering += OnBeginCamera;
        RenderPipelineManager.endCameraRendering += OnEndCamera;
        InteractionHud.EnsureExists();
    }

    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
        RenderPipelineManager.endCameraRendering -= OnEndCamera;

        if (_view != ViewState.None)
        {
            FinishExit(true);
        }

        ReleasePrompt();

        if (s_active == this)
        {
            s_active = null;
        }
    }

    private void Start()
    {
        Build();
    }

    private void OnDestroy()
    {
        if (_rt != null)
        {
            _rt.Release();
            Destroy(_rt);
        }

        if (_mat != null)
        {
            Destroy(_mat);
        }
    }

    // ─────────────────────────────── 만들기 ───────────────────────────────

    private void Build()
    {
        if (config == null)
        {
            config = CctvConfigSO.Load();
        }

        _monitor = FindMonitor(config);
        if (_monitor == null || config.Channels.Count == 0)
        {
            Debug.LogWarning("[CCTV] 모니터나 채널이 없어 쉽니다. CctvConfig의 monitorPath와 채널을 확인하십시오.", this);
            enabled = false;
            return;
        }

        Vector2Int res = config.Resolution;
        _rt = new RenderTexture(Mathf.Max(64, res.x), Mathf.Max(48, res.y), 16, RenderTextureFormat.ARGB32);
        _rt.name = "CCTV Feed";
        _rt.filterMode = FilterMode.Bilinear;
        _rt.Create();

        BuildCameras();
        BuildScreen();
        BuildLabel();
        BuildAudio();

        _channel = 0;
        _renderAcc = float.MaxValue;   // 첫 프레임에 바로 한 장 그린다.
        _built = true;

        if (logActions)
        {
            Debug.Log("[CCTV] 채널 " + _cams.Length + "개, 화면 " + _rt.width + "x" + _rt.height + ".", this);
        }
    }

    private void BuildCameras()
    {
        int count = config.Channels.Count;
        _cams = new Camera[count];
        _irLights = new Light[count];

        for (int i = 0; i < count; i++)
        {
            CctvConfigSO.Channel ch = config.Channels[i];

            GameObject go = new GameObject("CAM " + (i + 1).ToString("00") + " " + ch.label);
            go.transform.SetParent(transform, false);
            go.transform.position = ch.position;
            Vector3 look = ch.lookAt - ch.position;
            go.transform.rotation = look.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(look) : Quaternion.identity;

            Camera cam = go.AddComponent<Camera>();
            cam.enabled = false;
            cam.targetTexture = _rt;
            cam.fieldOfView = ch.fieldOfView;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 80f;
            cam.depth = -10f;
            cam.useOcclusionCulling = false;   // 이 씬의 오클루전 데이터는 벽을 지운 전력이 있다(CLAUDE.md §11.4).
            cam.allowHDR = false;
            cam.allowMSAA = false;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;

            UniversalAdditionalCameraData data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = false;
            data.renderShadows = false;
            data.antialiasing = AntialiasingMode.None;
            data.requiresColorOption = CameraOverrideOption.Off;
            data.requiresDepthOption = CameraOverrideOption.Off;

            GameObject lightGo = new GameObject("IR");
            lightGo.transform.SetParent(go.transform, false);
            Light ir = lightGo.AddComponent<Light>();
            ir.type = LightType.Spot;
            ir.range = ch.irRange;
            ir.spotAngle = ch.irSpotAngle;
            ir.innerSpotAngle = ch.irSpotAngle * 0.6f;
            ir.intensity = ch.irIntensity;
            ir.color = Color.white;
            ir.shadows = LightShadows.None;
            ir.enabled = false;   // 이 카메라가 그리는 동안에만 켠다.

            _cams[i] = cam;
            _irLights[i] = ir;
        }
    }

    private void BuildScreen()
    {
        _screen = GameObject.CreatePrimitive(PrimitiveType.Quad);
        _screen.name = "CCTV Screen";
        _screen.transform.SetParent(transform, false);

        // 메시 콜라이더 대신 얇은 상자. 조준선이 화면을 잡게 한다.
        Collider mesh = _screen.GetComponent<Collider>();
        if (mesh != null)
        {
            Destroy(mesh);
        }

        BoxCollider box = _screen.AddComponent<BoxCollider>();
        box.size = new Vector3(1f, 1f, 0.02f);
        _screenCollider = box;

        PlaceOnMonitor(_screen.transform, 0.002f);
        _screen.transform.localScale = new Vector3(config.ScreenSize.x, config.ScreenSize.y, 1f);

        Shader shader = Shader.Find(ShaderName);
        if (shader == null)
        {
            Debug.LogWarning("[CCTV] 화면 셰이더 " + ShaderName + "를 못 찾아 URP Unlit로 대신합니다. 흑백·노이즈가 빠집니다.", this);
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        }

        _mat = new Material(shader);
        _mat.name = "CCTV Screen (runtime)";
        _mat.mainTexture = _rt;
        if (_mat.HasProperty("_BaseMap"))
        {
            _mat.SetTexture("_BaseMap", _rt);
        }

        ApplyLookParameters();

        _screenRenderer = _screen.GetComponent<Renderer>();
        _screenRenderer.sharedMaterial = _mat;
        _screenRenderer.shadowCastingMode = ShadowCastingMode.Off;
        _screenRenderer.receiveShadows = false;
    }

    private void ApplyLookParameters()
    {
        if (_mat == null)
        {
            return;
        }

        _mat.SetFloat("_Gain", config.Gain);
        _mat.SetFloat("_Noise", config.Noise);
        _mat.SetFloat("_Scan", config.Scanlines);
        _mat.SetColor("_Tint", config.Tint);
    }

    private void BuildLabel()
    {
        GameObject go = new GameObject("CCTV Label");
        go.transform.SetParent(transform, false);
        PlaceOnMonitor(go.transform, 0.004f);

        _label = go.AddComponent<TextMeshPro>();
        _label.rectTransform.sizeDelta = new Vector2(config.ScreenSize.x * 0.9f, config.ScreenSize.y * 0.88f);
        _label.alignment = TextAlignmentOptions.TopLeft;
        _label.textWrappingMode = TextWrappingModes.NoWrap;
        _label.fontSize = 0.16f;
        _label.color = new Color(0.86f, 1f, 0.88f, 0.9f);
        _label.raycastTarget = false;

        TMP_FontAsset font = FindKoreanFont();
        if (font != null)
        {
            _label.font = font;
        }
        else
        {
            _asciiLabels = true;   // 한글 글꼴이 없으면 채널 번호만 찍는다. 네모 글자보다 낫다.
        }

        MeshRenderer mr = go.GetComponent<MeshRenderer>();
        if (mr != null)
        {
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }
    }

    /// <summary>
    /// 모니터 소리. 험은 늘 모니터에서 나고(3D), 채널을 넘기면 '틱-지직', 신호가 약하면 지직 루프가 섞인다.
    /// 파일(<c>Resources/Cctv/cctv_hum</c> 등)이 없으면 그 소리만 빠진다.
    /// </summary>
    private void BuildAudio()
    {
        AudioClip hum = Resources.Load<AudioClip>("Cctv/cctv_hum");
        AudioClip stat = Resources.Load<AudioClip>("Cctv/cctv_static");
        _switchClip = Resources.Load<AudioClip>("Cctv/cctv_switch");

        _hum = MakeScreenSource("Hum", hum, true);
        _staticLoop = MakeScreenSource("Static", stat, true);
        _sfx = MakeScreenSource("Sfx", null, false);

        if (_hum != null && _hum.clip != null)
        {
            _hum.volume = config.HumVolume;
            _hum.Play();
        }
    }

    private AudioSource MakeScreenSource(string name, AudioClip clip, bool loop)
    {
        GameObject go = new GameObject("CCTV Audio " + name);
        go.transform.SetParent(_screen.transform, false);
        AudioSource s = go.AddComponent<AudioSource>();
        s.playOnAwake = false;
        s.loop = loop;
        s.clip = clip;
        s.spatialBlend = 1f;
        s.rolloffMode = AudioRolloffMode.Logarithmic;
        s.minDistance = 0.4f;
        s.maxDistance = 8f;
        s.dopplerLevel = 0f;
        s.volume = 0f;
        return s;
    }

    private void PlaySwitchSound()
    {
        if (_sfx != null && _switchClip != null)
        {
            _sfx.PlayOneShot(_switchClip, config.SwitchVolume);
        }
    }

    private void UpdateStaticAudio()
    {
        if (_staticLoop == null || _staticLoop.clip == null)
        {
            return;
        }

        float v = (1f - _signal) * config.StaticVolume;
        _staticLoop.volume = v;
        if (v > 0.001f && !_staticLoop.isPlaying)
        {
            _staticLoop.Play();
        }
        else if (v <= 0.001f && _staticLoop.isPlaying)
        {
            _staticLoop.Stop();
        }
    }

    /// <summary>모니터 화면 위, 화면 법선 쪽으로 <paramref name="lift"/>만큼 띄운 자리에 놓는다.</summary>
    private void PlaceOnMonitor(Transform t, float lift)
    {
        Vector3 outward = ScreenOutward();
        Vector3 up = _monitor.TransformDirection(Vector3.forward).normalized;   // 이 모니터는 로컬 Z가 위다.
        t.position = _monitor.TransformPoint(config.ScreenLocalCenter) + outward * lift;
        // 쿼드와 TMP는 -Z 쪽에서 읽힌다. 그래서 앞면(-Z)이 방 쪽(outward)을 보도록 forward를 모니터 안쪽으로 둔다.
        t.rotation = Quaternion.LookRotation(-outward, up);
    }

    /// <summary>화면이 바라보는 방향(월드). Old CRT Monitor는 로컬 -Y가 앞이다.</summary>
    private Vector3 ScreenOutward()
    {
        return _monitor.TransformDirection(Vector3.down).normalized;
    }

    private static TMP_FontAsset FindKoreanFont()
    {
        TMP_Text[] texts = FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < texts.Length; i++)
        {
            TMP_FontAsset f = texts[i] != null ? texts[i].font : null;
            if (f != null && f.HasCharacter('복', true, true))
            {
                return f;
            }
        }

        TMP_FontAsset[] all = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].HasCharacter('복', true, true))
            {
                return all[i];
            }
        }

        return null;
    }

    // ─────────────────────────────── 매 프레임 ───────────────────────────────

    private void Update()
    {
        // 지난 프레임에 한 장 그린 카메라를 끈다. 켜 두면 매 프레임 그린다.
        if (_renderedCam != null)
        {
            _renderedCam.enabled = false;
            _renderedCam = null;
        }

        if (!_built)
        {
            return;
        }

        switch (_view)
        {
            case ViewState.None:
                UpdateAim();
                break;

            case ViewState.Viewing:
                UpdateViewingInput();
                break;
        }
    }

    private void LateUpdate()
    {
        if (!_built)
        {
            return;
        }

        UpdateViewBlend();

        _static = Mathf.MoveTowards(_static, 0f, Time.deltaTime / Mathf.Max(0.01f, config.SwitchStaticSeconds));
        _mat.SetFloat("_Static", _static);
        _mat.SetFloat("_Signal", _signal);
        UpdateStaticAudio();

        bool forcing = Time.time < _forceRenderUntil;
        bool awake = _view != ViewState.None || PlayerCanSeeScreen() || forcing;
        _label.gameObject.SetActive(awake && _override == null);
        if (!awake || _override != null)
        {
            return;   // 화면을 덮어썼으면(피날레: 꺼진 CRT·비친 모습) 채널을 그리지 않는다
        }

        float fps = forcing ? 1000f : _view != ViewState.None ? config.ViewFps : config.IdleFps;
        _renderAcc += Time.deltaTime;
        if (_renderAcc >= 1f / Mathf.Max(1f, fps))
        {
            _renderAcc = 0f;
            Camera cam = _cams[_channel];
            cam.enabled = true;   // 이번 프레임에 한 장. 다음 Update에서 끈다.
            _renderedCam = cam;
        }

        UpdateLabel();
    }

    private bool PlayerCanSeeScreen()
    {
        Camera main = Camera.main;
        if (main == null || _screenRenderer == null)
        {
            return false;
        }

        if ((main.transform.position - _screen.transform.position).sqrMagnitude > config.WakeRadius * config.WakeRadius)
        {
            return false;
        }

        GeometryUtility.CalculateFrustumPlanes(main, s_planes);
        return GeometryUtility.TestPlanesAABB(s_planes, _screenRenderer.bounds);
    }

    private void UpdateLabel()
    {
        _labelCooldown -= Time.deltaTime;
        if (_labelCooldown > 0f)
        {
            return;
        }

        _labelCooldown = 0.25f;

        string name = _asciiLabels ? string.Empty : ChannelLabel(_channel);
        // 50차: 그날의 빈 방 채널(K2)에는 「공실」 표지 — 수칙 「공실 채널은 오래 보지 마십시오.」의 방아쇠가 화면에 보이게.
        if (NightRun.EmptyRoomChannel >= 0 && NightRun.EmptyRoomChannel == _channel) name = _asciiLabels ? "VACANT" : (name.Length > 0 ? name + " · 공실" : "공실");
        string clock = ClockText();
        bool rec = Mathf.Repeat(Time.time, 1f) < 0.5f;

        string text = "CAM" + (_channel + 1).ToString("00") + "  " + name + "\n"
                      + (rec ? "<color=#FF5050>REC</color>" : "<alpha=#00>REC</alpha>")
                      + (clock.Length > 0 ? "  " + clock : string.Empty);

        if (text != _labelText)
        {
            _labelText = text;
            _label.text = text;
        }
    }

    private string ClockText()
    {
        if (_clock == null)
        {
            _clockSearchCooldown -= Time.deltaTime;
            if (_clockSearchCooldown <= 0f)
            {
                _clockSearchCooldown = 1f;
                _clock = FindAnyObjectByType<GameTime>();
            }
        }

        return _clock != null && _clock.CurrentTimeText != null ? _clock.CurrentTimeText : string.Empty;
    }

    // ─────────────────────────────── 조준과 입력 ───────────────────────────────

    private void UpdateAim()
    {
        if (!CanInteract() || !IsAimingAtMonitor())
        {
            ReleasePrompt();
            return;
        }

        ClaimPrompt(EnterPrompt, true);
        InteractionOutline.Request(_monitor);   // 들여다볼 수 있을 때만 모니터 외곽선(2026-10-03).

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(interactKey))
        {
            EnterView();
        }
#endif
    }

    private void UpdateViewingInput()
    {
        if (NightRun.IsCaptured || Time.timeScale <= 0f)
        {
            FinishExit(true);   // 붙잡히거나 멈추면 곧바로 몸으로 돌려보낸다.
            return;
        }

        ClaimPrompt(ViewPrompt, false);

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(interactKey) || Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.S))
        {
            BeginExit();
            return;
        }

        if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))
        {
            Step(-1);
        }
        else if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow))
        {
            Step(1);
        }
        else
        {
            float wheel = Input.mouseScrollDelta.y;
            if (wheel > 0.1f)
            {
                Step(-1);
            }
            else if (wheel < -0.1f)
            {
                Step(1);
            }

            for (int i = 0; i < _cams.Length && i < 9; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + i))
                {
                    SetChannel(i);
                    break;
                }
            }
        }
#endif
    }

    private bool CanInteract()
    {
        if (Time.timeScale <= 0f || NightRun.IsCaptured)
        {
            return false;
        }

        PlayerTablet[] tablets = FindObjectsByType<PlayerTablet>(FindObjectsSortMode.None);
        for (int i = 0; i < tablets.Length; i++)
        {
            if (TabletZoom.Reading(tablets[i]))
            {
                return false;   // 태블릿을 펼친 채로는 모니터를 보지 않는다.
            }
        }

        return true;
    }

    private bool IsAimingAtMonitor()
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            return false;
        }

        RaycastHit hit;
        if (!Physics.SphereCast(cam.transform.position, AimRadius, cam.transform.forward, out hit, Reach, ~0,
                QueryTriggerInteraction.Ignore))
        {
            return false;
        }

        return hit.collider == _screenCollider || hit.transform == _monitor || hit.transform.IsChildOf(_monitor);
    }

    private void ClaimPrompt(string line, bool hot)
    {
        _ownsPrompt = true;
        InteractionHud.ExternalPrompt = line;
        InteractionHud.ExternalHot = hot;
    }

    private void ReleasePrompt()
    {
        if (!_ownsPrompt)
        {
            return;
        }

        _ownsPrompt = false;
        InteractionHud.ExternalPrompt = string.Empty;
        InteractionHud.ExternalHot = false;
    }

    // ─────────────────────────────── 채널 ───────────────────────────────

    /// <summary>채널을 바꾼다(0부터). 지직거림이 한 번 지나간다.</summary>
    public void SetChannel(int index)
    {
        if (_cams.Length == 0)
        {
            return;
        }

        int next = ((index % _cams.Length) + _cams.Length) % _cams.Length;
        if (next == _channel)
        {
            return;
        }

        _channel = next;
        _static = 1f;
        PlaySwitchSound();
        _renderAcc = float.MaxValue;   // 새 채널을 바로 한 장 그린다.
        _labelCooldown = 0f;

        if (logActions)
        {
            Debug.Log("[CCTV] 채널 " + (_channel + 1) + " " + ChannelLabel(_channel), this);
        }

        Action<int> handler = ChannelChanged;
        if (handler != null)
        {
            handler(_channel);
        }
    }

    private void Step(int delta)
    {
        SetChannel(_channel + delta);
    }

    // ─────────────────────────────── 들여다보기 ───────────────────────────────

    private void EnterView()
    {
        _playerCam = Camera.main;
        if (_playerCam == null)
        {
            return;
        }

        _fp = _playerCam.GetComponentInParent<FPController>();
        if (_fp != null)
        {
            _fpWasEnabled = _fp.enabled;
            _fp.enabled = false;   // 이동·시선·일시정지 입력이 전부 여기 있다.
        }

        Cursor.visible = false;   // FPController.OnDisable이 커서를 켠다.

        _tablets.Clear();
        PlayerTablet[] tablets = FindObjectsByType<PlayerTablet>(FindObjectsSortMode.None);
        for (int i = 0; i < tablets.Length; i++)
        {
            TabletState s = new TabletState();
            s.tablet = tablets[i];
            s.readInput = tablets[i].readInput;
            _tablets.Add(s);
            tablets[i].readInput = false;   // 모니터를 보는 동안 Tab으로 태블릿이 튀어나오지 않게.
        }

        // 손전등 같은 1인칭 소품이 화면을 가리지 않게 잠깐 숨긴다. 빛은 그대로 둔다.
        _hiddenViewmodel.Clear();
        Renderer[] held = _playerCam.GetComponentsInChildren<Renderer>();
        for (int i = 0; i < held.Length; i++)
        {
            if (held[i].enabled)
            {
                held[i].enabled = false;
                _hiddenViewmodel.Add(held[i]);
            }
        }

        _savedLocalPos = _playerCam.transform.localPosition;
        _savedLocalRot = _playerCam.transform.localRotation;
        _fromPos = _playerCam.transform.position;
        _fromRot = _playerCam.transform.rotation;
        _blend = 0f;
        _view = ViewState.Entering;
        _static = 1f;
        _renderAcc = float.MaxValue;
        PlaySwitchSound();   // 화면이 '깨어나는' 소리

        if (logActions)
        {
            Debug.Log("[CCTV] 들여다보기 시작", this);
        }

        Action handler = ViewEntered;
        if (handler != null)
        {
            handler();
        }
    }

    private void BeginExit()
    {
        if (_playerCam == null)
        {
            FinishExit(true);
            return;
        }

        _fromPos = _playerCam.transform.position;
        _fromRot = _playerCam.transform.rotation;
        _blend = 0f;
        _view = ViewState.Leaving;
        ReleasePrompt();
    }

    private void UpdateViewBlend()
    {
        if (_view != ViewState.Entering && _view != ViewState.Leaving && _view != ViewState.Viewing)
        {
            return;
        }

        if (_playerCam == null)
        {
            FinishExit(true);
            return;
        }

        Vector3 outward = ScreenOutward();
        Vector3 up = _monitor.TransformDirection(Vector3.forward).normalized;
        Vector3 viewPos = _monitor.TransformPoint(config.ScreenLocalCenter) + outward * config.ViewDistance;
        Quaternion viewRot = Quaternion.LookRotation(-outward, up);

        if (_view == ViewState.Viewing)
        {
            _playerCam.transform.SetPositionAndRotation(viewPos, viewRot);
            return;
        }

        float seconds = Mathf.Max(0.0001f, config.ViewBlendSeconds);
        _blend = Mathf.Min(1f, _blend + Time.deltaTime / seconds);
        float t = Mathf.SmoothStep(0f, 1f, _blend);

        if (_view == ViewState.Entering)
        {
            _playerCam.transform.SetPositionAndRotation(Vector3.Lerp(_fromPos, viewPos, t), Quaternion.Slerp(_fromRot, viewRot, t));
            if (_blend >= 1f)
            {
                _view = ViewState.Viewing;
            }

            return;
        }

        // Leaving: 몸은 움직이지 않았으므로 원래 자리는 부모 기준으로 그대로다.
        Transform parent = _playerCam.transform.parent;
        Vector3 homePos = parent != null ? parent.TransformPoint(_savedLocalPos) : _savedLocalPos;
        Quaternion homeRot = parent != null ? parent.rotation * _savedLocalRot : _savedLocalRot;
        _playerCam.transform.SetPositionAndRotation(Vector3.Lerp(_fromPos, homePos, t), Quaternion.Slerp(_fromRot, homeRot, t));

        if (_blend >= 1f)
        {
            FinishExit(false);
        }
    }

    /// <summary>몸으로 돌려보낸다. <paramref name="snap"/>이면 보간 없이 곧바로.</summary>
    private void FinishExit(bool snap)
    {
        if (_playerCam != null)
        {
            _playerCam.transform.localPosition = _savedLocalPos;
            _playerCam.transform.localRotation = _savedLocalRot;
        }

        if (_fp != null)
        {
            _fp.enabled = _fpWasEnabled;
        }

        for (int i = 0; i < _tablets.Count; i++)
        {
            if (_tablets[i].tablet != null)
            {
                _tablets[i].tablet.readInput = _tablets[i].readInput;
            }
        }

        for (int i = 0; i < _hiddenViewmodel.Count; i++)
        {
            if (_hiddenViewmodel[i] != null)
            {
                _hiddenViewmodel[i].enabled = true;
            }
        }

        _tablets.Clear();
        _hiddenViewmodel.Clear();
        _fp = null;
        _playerCam = null;
        _view = ViewState.None;
        ReleasePrompt();

        if (logActions)
        {
            Debug.Log("[CCTV] 들여다보기 끝" + (snap ? "(즉시)" : string.Empty), this);
        }

        Action handler = ViewExited;
        if (handler != null)
        {
            handler();
        }
    }

    // ─────────────────────────────── 카메라별 켜고 끄기 ───────────────────────────────

    private int IndexOf(Camera cam)
    {
        for (int i = 0; i < _cams.Length; i++)
        {
            if (_cams[i] == cam)
            {
                return i;
            }
        }

        return -1;
    }

    private void OnBeginCamera(ScriptableRenderContext context, Camera cam)
    {
        int ch = IndexOf(cam);
        if (ch < 0)
        {
            return;   // 플레이어·씬 뷰 카메라. CctvOnlyVisible은 평소 꺼져 있으므로 할 일이 없다.
        }

        Light ir = _irLights[ch];
        if (ir != null)
        {
            ir.enabled = ir.intensity > 0f;
        }

        CctvOnlyVisible.BeforeCamera(ch);
    }

    private void OnEndCamera(ScriptableRenderContext context, Camera cam)
    {
        int ch = IndexOf(cam);
        if (ch < 0)
        {
            return;
        }

        if (_irLights[ch] != null)
        {
            _irLights[ch].enabled = false;
        }

        CctvOnlyVisible.AfterCamera();
    }
}

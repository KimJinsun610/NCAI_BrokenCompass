using NightDuty;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 104차(사용자: 「경비실 camping 방지 — 약 30초 형광등이 깜빡이고 무전 잡음(문구 없음) · 약 60초 CCTV 화면이 검은 남자의 얼굴로 채워짐 ·
/// 약 90초 경비실 정전, 손전등만 · 그래도 남아 있으면 검은 남자에게 붙잡힘(검은 남자 얼굴 처형 장면, 사망 화면에 「경비실에 오래 머물지 마십시오.」)」):
/// 판단은 코어(<see cref="NightRun.CampStage"/> · <see cref="GuardRoomLoiter"/>)가 하고, 이것은 플레이어 자리를 알리고 단계대로 틀기만 한다.
/// <list type="bullet">
/// <item>자리: 경비실 상자 안(발 위 0.5m)이면 안, 상자에서 <see cref="FarOutsideMeters"/>m 넘게 떨어지면 「문에서 떨어진 밖」.</item>
/// <item>깜빡임: 경비실 묶음 조명을 짧게 껐다 켠다(<see cref="LightGroup.Flicker"/> — 실시간 등·발광·라이트맵만, 프로브는 그대로). 정전은 구운 빛·프로브까지(<see cref="RoomDarkness"/>에 경비실을 소등 방으로 넣었다).</item>
/// <item>무전 잡음: CCTV 지직 소리를 경비실에서 낮게 반복. 얼굴 단계에서 한 번 크게 튄다(시선을 끈다).</item>
/// <item>CCTV 얼굴: 맵 밖 아래에 검은 남자와 전용 카메라를 두고 그 화면을 CCTV 모니터에 덮어쓴다(<see cref="CctvSystem.SetScreenOverride"/>). 얼굴은 카메라를 똑바로 본다.</item>
/// <item>정전: 묶음을 끈다 — 켤 때는 끄기 직전 상태로(다른 연출이 끈 등은 그대로). 단계가 내려가거나 밤이 새로 시작하면 모두 되돌린다.</item>
/// <item>붙잡힘: 코어가 알리면 조도 축으로 붙잡는다(출처 <see cref="NightRun.CampCaptureSource"/>). 다른 연출 중이면 미룬다. 경비실 문은 잠그지 않는다.</item>
/// </list>
/// </summary>
[DisallowMultipleComponent]
public sealed class GuardRoomCamp : MonoBehaviour
{
    /// <summary>경비실 상자에서 이만큼(m) 떨어져야 「문에서 떨어진 밖」.</summary>
    public const float FarOutsideMeters = 1.2f;

    /// <summary>무전 잡음 소리(CCTV 지직 — 사운드 표 키).</summary>
    public const string StaticSound = "K2.cue";

    /// <summary>무전 잡음 크기 — 깜빡임 단계 / 그 뒤.</summary>
    public const float StaticVolumeLow = 0.18f;

    public const float StaticVolumeHigh = 0.32f;

    /// <summary>얼굴 화면 카메라와 얼굴 사이(m).</summary>
    public const float FaceDistance = 0.36f;   // PlayScene 실측: 0.55는 얼굴이 화면의 절반 — 화면을 채우게

    private static GuardRoomCamp s_active;

    private FPController _player;
    private SpaceZones _zones;
    private Bounds _box;
    private bool _hasBox;
    private LightGroup _lights;
    private LoiterStage _shown = LoiterStage.None;
    private AudioSource _static;
    private GameObject _rig;
    private Camera _faceCam;
    private RenderTexture _faceRt;
    private bool _overriding;
    private bool _capturing;
    private bool _blackout;
    private bool _blipOff;
    private float _nextBlip;
    private float _blipEnds;

    /// <summary>지금 살아 있는 것(검수).</summary>
    public static GuardRoomCamp Active
    {
        get { return s_active; }
    }

    /// <summary>지금 틀고 있는 단계(검수).</summary>
    public LoiterStage Shown
    {
        get { return _shown; }
    }

    /// <summary>CCTV를 얼굴로 덮고 있는지(검수).</summary>
    public bool ShowingFace
    {
        get { return _overriding && OwnsScreen(); }
    }

    /// <summary>CCTV 화면이 지금 실제로 우리 얼굴 화면인지.</summary>
    private bool OwnsScreen()
    {
        CctvSystem cctv = CctvSystem.Active;
        return cctv != null && _faceRt != null && cctv.ScreenOverride == _faceRt;
    }

    /// <summary>경비실 조명 묶음이 정전인지(검수).</summary>
    public bool Blackout
    {
        get { return _blackout; }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        s_active = null;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        TryInstall(SceneManager.GetActiveScene());
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TryInstall(scene);
    }

    private static void TryInstall(Scene scene)
    {
        if (!FlowAutoInstall.IsDutyScene(scene) || FlowAutoInstall.Exists<GuardRoomCamp>(scene)) return;
        if (Object.FindAnyObjectByType<SpaceZones>() == null) return;
        FlowAutoInstall.CreateHost<GuardRoomCamp>(scene, "GuardRoomCamp (auto)");
    }

    private void OnEnable()
    {
        s_active = this;
    }

    private void OnDisable()
    {
        Apply(LoiterStage.None);
        NightRun.SetCampPresence(false, false);
        if (s_active == this) s_active = null;
    }

    private void OnDestroy()
    {
        if (_faceRt != null)
        {
            _faceRt.Release();
            Destroy(_faceRt);   // 104차 QA: Release만으로는 객체가 남는다
        }

        if (_rig != null) Destroy(_rig);
    }

    private void Update()
    {
        if (!Ready())
        {
            NightRun.SetCampPresence(false, false);
            Apply(LoiterStage.None);
            return;
        }

        CaptureDirector cd = CaptureDirector.Active;
        bool busy = cd != null && cd.IsPlaying;
        Vector3 q = _player.transform.position + Vector3.up * 0.5f;
        bool inside = _box.Contains(q);
        float dist = inside ? 0f : Mathf.Sqrt(_box.SqrDistance(q));
        // 붙잡힘·넘어짐 연출 중에는 경비실에 있지 않은 것으로(그동안 시간이 쌓이지 않게).
        NightRun.SetCampPresence(inside && !busy, !busy && dist >= FarOutsideMeters);
        if (!busy && !NightRun.IsCaptured) _capturing = false;
        // 104차 QA: 우리가 붙잡는 동안에는 정전·얼굴을 그대로 둔다(옛: 붙잡힘이 시작되는 순간 불이 다시 켜졌다).
        LoiterStage want = _capturing ? _shown : busy || NightRun.IsCaptured ? LoiterStage.None : NightRun.CampStage;
        Apply(want);
        if (want >= LoiterStage.CctvFace && !OwnsScreen()) ShowFace(true);   // 다른 재생이 화면을 덮었으면 비는 대로 되찾는다(매 프레임 실제 화면으로 확인)
        Blip(want);

        if (NightRun.ConsumeCampCapture())
        {
            if (cd == null || busy || NightRun.IsCaptured) NightRun.DeferCampCapture();
            else
            {
                Debug.Log("[GuardRoomCamp] 경비실에 너무 오래 — 검은 남자에게 붙잡힘");
                _capturing = true;
                NightRun.CaptureByEncounter(FearAxis.Illuminance, NightRun.CampCaptureSource);
            }
        }
    }

    private bool Ready()
    {
        if (_player == null) _player = FindAnyObjectByType<FPController>();
        if (_zones == null)
        {
            _zones = FindAnyObjectByType<SpaceZones>();
            _hasBox = _zones != null && _zones.TryGetSpaceBox(SpaceId.SecurityRoom, out _box);
        }

        return _player != null && _hasBox;
    }

    private LightGroup Lights()
    {
        if (_lights == null && _zones != null) _lights = LightGroup.Collect(SpaceId.SecurityRoom, _zones);
        return _lights;
    }

    // ── 단계 ──────────────────────────────────────────────────

    private void Apply(LoiterStage want)
    {
        if (want == _shown) return;
        LoiterStage was = _shown;
        _shown = want;
        if (DirectionStage.Verbose) Debug.Log("[GuardRoomCamp] " + was + " → " + want);

        // 무전 잡음
        if (want >= LoiterStage.Flicker) StartStatic(want >= LoiterStage.CctvFace ? StaticVolumeHigh : StaticVolumeLow);
        else StopStatic();

        // CCTV 얼굴(정전 중에도 화면은 켜진 채 — 어둠 속에 얼굴만)
        if (want >= LoiterStage.CctvFace)
        {
            if (!OwnsScreen()) ShowFace(true);
            if (was < LoiterStage.CctvFace) Burst();
        }
        else if (_overriding)
        {
            ShowFace(false);
        }

        // 정전
        SetBlackout(want >= LoiterStage.Blackout);
        if (want < LoiterStage.Flicker) EndBlip();
    }

    private void SetBlackout(bool dark)
    {
        LightGroup g = Lights();
        if (dark == _blackout)
        {
            return;
        }

        EndBlip();
        _blackout = dark;
        if (g == null) return;
        if (dark) g.TurnOff();
        else g.TurnOn();   // 끄기 직전 상태로(다른 연출이 꺼 둔 등은 꺼진 채)
    }

    /// <summary>깜빡임 — 대부분 켜져 있다가 가끔 짧게 꺼진다(얼굴 단계는 더 잦게).</summary>
    private void Blip(LoiterStage want)
    {
        if (want < LoiterStage.Flicker || want >= LoiterStage.Blackout) return;
        LightGroup g = Lights();
        if (g == null) return;
        float now = Time.time;
        if (_blipOff)
        {
            if (now < _blipEnds) return;
            _blipOff = false;
            g.Flicker(false);
            float gap = want >= LoiterStage.CctvFace ? Random.Range(0.25f, 1.4f) : Random.Range(0.8f, 3f);
            _nextBlip = now + gap;
            return;
        }

        if (now < _nextBlip) return;
        _blipOff = true;
        _blipEnds = now + Random.Range(0.05f, 0.16f);
        g.Flicker(true);   // 104차 QA: 프로브까지 다시 쓰는 정전(TurnOff) 대신 가벼운 깜빡임
    }

    private void EndBlip()
    {
        if (!_blipOff) return;
        _blipOff = false;
        if (_lights != null) _lights.Flicker(false);
    }

    // ── 소리 ──────────────────────────────────────────────────

    private void StartStatic(float volume)
    {
        if (_static == null)
        {
            float v;
            AudioClip clip = DirectionSoundTableSO.Find(StaticSound, out v);
            if (clip == null) return;
            GameObject go = new GameObject("경비실 무전 잡음");
            go.transform.SetParent(transform, false);
            go.transform.position = _box.center + Vector3.up * 0.8f;
            _static = go.AddComponent<AudioSource>();
            _static.clip = clip;
            _static.loop = true;
            _static.playOnAwake = false;
            _static.spatialBlend = 0.7f;
            _static.minDistance = 2.5f;
            _static.maxDistance = 14f;
            _static.rolloffMode = AudioRolloffMode.Linear;
        }

        _static.volume = volume;
        if (!_static.isPlaying) _static.Play();
    }

    private void StopStatic()
    {
        if (_static != null && _static.isPlaying) _static.Stop();
    }

    private void Burst()
    {
        Vector3 at = _box.center + Vector3.up * 1f;
        CctvSystem cctv = CctvSystem.Active;
        if (cctv != null) at = cctv.ScreenCenter;
        DirectionStage.PlaySound(StaticSound, at, 3f, 0.6f);
    }

    // ── CCTV 얼굴 ─────────────────────────────────────────────

    private void ShowFace(bool on)
    {
        CctvSystem cctv = CctvSystem.Active;
        if (on)
        {
            if (cctv == null) return;
            if (cctv.HasScreenOverride && cctv.ScreenOverride != _faceRt) return;   // 피날레·놓친 이상 재생 등이 쓰는 중 — 덮지 않는다
            if (!EnsureRig()) return;
            _rig.SetActive(true);
            _faceCam.enabled = true;
            cctv.SetScreenOverride(_faceRt, 1.1f);
            _overriding = true;
            return;
        }

        _overriding = false;
        if (_rig != null) _rig.SetActive(false);
        if (cctv != null && _faceRt != null && cctv.ScreenOverride == _faceRt) cctv.SetScreenOverride(null);   // 우리가 덮은 것만 걷는다
    }

    /// <summary>맵 밖 아래에 검은 남자 · 그 얼굴만 보는 카메라 · 빛.</summary>
    private bool EnsureRig()
    {
        if (_rig != null) return true;
        Vector3 origin = new Vector3(0f, -120f, 0f);
        GameObject man = StandInFactory.Create("mob.blackman", origin, Quaternion.identity, null);
        if (man == null) return false;
        foreach (Collider c in man.GetComponentsInChildren<Collider>(true)) Destroy(c);
        foreach (MonoBehaviour b in man.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (b != null) b.enabled = false;
        }

        _rig = new GameObject("경비실 CCTV 얼굴");
        _rig.transform.position = origin;
        man.transform.SetParent(_rig.transform, true);
        foreach (Animator a in man.GetComponentsInChildren<Animator>(true)) a.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        Transform head = ScareFace.Find(man.transform, "head");
        Vector3 face = head != null ? head.position + Vector3.up * 0.12f : origin + Vector3.up * 1.55f;
        GameObject camGo = new GameObject("얼굴 카메라");
        camGo.transform.SetParent(_rig.transform, false);
        camGo.transform.position = face + man.transform.forward * FaceDistance + Vector3.up * 0.02f;
        camGo.transform.LookAt(face);
        _faceCam = camGo.AddComponent<Camera>();
        _faceCam.clearFlags = CameraClearFlags.SolidColor;
        _faceCam.backgroundColor = Color.black;
        _faceCam.fieldOfView = 38f;
        _faceCam.nearClipPlane = 0.05f;
        _faceCam.farClipPlane = 4f;
        _faceRt = new RenderTexture(512, 384, 16);
        _faceRt.name = "경비실 CCTV 얼굴";
        _faceCam.targetTexture = _faceRt;

        GameObject lightGo = new GameObject("얼굴 빛");
        lightGo.transform.SetParent(_rig.transform, false);
        lightGo.transform.position = face + man.transform.forward * 0.5f + Vector3.up * 0.45f + man.transform.right * 0.25f;
        Light key = lightGo.AddComponent<Light>();
        key.type = LightType.Point;
        key.range = 2.5f;
        key.intensity = 1.6f;
        key.color = new Color(0.82f, 0.9f, 1f);
        key.shadows = LightShadows.None;

        // 104차 QA: 시선은 얼굴 가운데에서 잰다(뼈에서 재면 카메라가 얼굴 앞 36cm라 크게 어긋난다 — 컷신과 같은 방식).
        if (head != null) ScareFace.Stare.Attach(man, head, _faceCam, 1f, CutsceneFace.BlackmanFaceAxis, head.InverseTransformPoint(face), false);
        _rig.SetActive(false);
        return true;
    }
}

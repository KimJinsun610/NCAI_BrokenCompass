using System;
using System.Collections.Generic;
using NightDuty;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 엠비언트 재생기. 네 겹으로 깐다.
/// <list type="number">
/// <item><b>학교 공통 바탕</b> — 건물 안이면 어디서나 룸톤 밑에 깔린다(2026-10-04 사운드 전달본).</item>
/// <item><b>룸톤</b> — 플레이어가 선 공간의 루프(복도·교실·과학실·화장실·도서관·경비실). 공간을 옮기면 천천히 바뀐다.
/// 공간마다 약한 판·강한 판이 있고, <b>그 공간의</b> 청각 구간이 <see cref="AmbienceConfigSO.HighFromBand"/>(3) 이상이면 강한 판으로 넘어간다.</item>
/// <item><b>불안 레이어</b> — 그 공간의 <b>청각축 표시 구간</b>을 따라 1~4번 루프가 쌓인다(구간 2면 1·2번).
/// 구간은 <see cref="EventBus.BandChanged"/>로만 받는다.</item>
/// <item><b>원샷</b> — 공간별 목록에서 무작위로, 플레이어 주변 6~14m 어딘가에서 3D로 난다. 구간이 오를수록 잦아진다.</item>
/// </list>
///
/// <para><b>판정과 무관하다.</b> 어떤 신호도 보내지 않는다(CLAUDE.md §2.7 「분위기 효과음은 카드 단서 ID를 보내지 않는다」).
/// 원샷 목록에도 단서와 헷갈릴 소리(3번 노크·분필·유리·물 내림)를 넣지 않았다.</para>
///
/// <para><b>씬을 고치지 않는다.</b> <see cref="CctvSystem"/>처럼 플레이할 때 스스로 설치된다.
/// 1인칭 컨트롤러(<see cref="FPController"/>)가 있는 씬에서만 켜진다. 설정은 <see cref="AmbienceConfigSO"/>.</para>
///
/// <para>소리 파일이 없으면 그 겹만 조용히 빠진다(처음 한 번 경고).</para>
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(70)]
public sealed class AmbiencePlayer : MonoBehaviour
{
    private const string RootName = "Ambience (auto)";
    private const int OneShotVoices = 4;
    private const float SampleSeconds = 0.2f;

    private static AmbiencePlayer s_active;
    private static bool s_hooked;

    /// <summary>원샷이 났다(클립 경로, 위치). 디버그·자막용.</summary>
    public static event Action<string, Vector3> OneShotPlayed;

    [Tooltip("비우면 Resources/AmbienceConfig를 읽고, 그것도 없으면 코드 기본값을 쓴다.")]
    [SerializeField] private AmbienceConfigSO config;

    [Header("디버그")]
    [SerializeField] private bool logActions;

    private sealed class RoomVoice
    {
        public string label;
        public AmbienceConfigSO.Zone zone;
        public AudioSource source;   // 약한 판
        public AudioSource high;     // 강한 판(없으면 null)
        public float volume;   // 공간 설정 볼륨
        public float level;    // 0~1, 지금 페이드 값
        public float mix;      // 0 = 약한 판, 1 = 강한 판
    }

    private readonly List<RoomVoice> _rooms = new List<RoomVoice>();
    private readonly Dictionary<string, RoomVoice> _roomByLabel = new Dictionary<string, RoomVoice>();
    private AudioSource[] _dread = new AudioSource[0];
    private float[] _dreadLevel = new float[0];
    private AudioSource[] _voices = new AudioSource[0];
    private int _nextVoice;
    private AudioSource _stinger;
    private AudioSource _base;
    private float _baseLevel;

    private readonly Band[] _auditory = new Band[16];
    private readonly bool[] _known = new bool[16];

    private AmbienceConfigSO.Zone _zone;
    private string _zoneLabel = string.Empty;
    private int _band;
    private float _sampleAcc = SampleSeconds;
    private float _shotTimer = -1f;
    private string _lastShot = string.Empty;
    private float _master = 1f;
    private float _duck = 1f;
    private float _duckTarget = 1f;
    private float _duckSeconds = 0.5f;
    private readonly HashSet<string> _missing = new HashSet<string>();
    private bool _building;
    private System.Random _rng;

    /// <summary>지금 살아 있는 재생기. 없으면 null.</summary>
    public static AmbiencePlayer Active
    {
        get { return s_active; }
    }

    /// <summary>플레이어가 선 공간 이름(복도·교실 …). 건물 밖이면 빈 문자열.</summary>
    public string CurrentZone
    {
        get { return _zoneLabel; }
    }

    /// <summary>불안 레이어가 따르는 청각 구간(0~4).</summary>
    public int CurrentBand
    {
        get { return _band; }
    }

    /// <summary>지금 공간의 룸톤이 강한 판 쪽인가(넘어가는 중이면 절반 넘었을 때).</summary>
    public bool CurrentRoomHigh
    {
        get
        {
            RoomVoice v;
            return _roomByLabel.TryGetValue(_zoneLabel, out v) && v.mix > 0.5f;
        }
    }

    // ─────────────────────────────── 설치 ───────────────────────────────

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        s_active = null;
        OneShotPlayed = null;
        if (s_hooked)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            s_hooked = false;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InstallForFirstScene()
    {
        // 메인 → 로딩 → 플레이 순이라 첫 씬만 보면 PlayScene을 놓친다(CctvSystem과 같다).
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

        if (FindAnyObjectByType<AmbiencePlayer>(FindObjectsInactive.Include) != null)
        {
            return;
        }

        if (FindAnyObjectByType<FPController>() == null)
        {
            return;   // 걸어 다니는 씬이 아니다(메뉴·로딩·결과).
        }

        if (!AmbienceConfigSO.Load().EnabledInGame)
        {
            return;
        }

        // hideFlags를 붙이지 않는다(CLAUDE.md §5.5-23).
        GameObject go = new GameObject(RootName);
        go.AddComponent<AmbiencePlayer>();
    }

    // ─────────────────────────────── 수명 ───────────────────────────────

    private void OnEnable()
    {
        s_active = this;
        EventBus.BandChanged += OnBandChanged;
    }

    private void OnDisable()
    {
        EventBus.BandChanged -= OnBandChanged;
        if (s_active == this)
        {
            s_active = null;
        }
    }

    private void Start()
    {
        if (config == null)
        {
            config = AmbienceConfigSO.Load();
        }

        _rng = new System.Random(Environment.TickCount);
        _building = true;
        BuildBase();
        BuildRooms();
        BuildDread();
        BuildVoices();
        _building = false;

        if (_missing.Count > 0)
        {
            // 파일마다 경고를 찍으면 콘솔이 덮인다. 한 줄로 알리고, 목록은 메뉴에서 본다.
            Debug.LogWarning("[Ambience] 소리 파일 " + _missing.Count + "개가 없어 그 겹만 빼고 재생합니다. " +
                             "메뉴 NightDuty/엠비언트 소리 파일 검사로 목록을 보십시오. 예: Resources/" + First(_missing), this);
        }
        ScheduleNextShot();

        if (logActions)
        {
            Debug.Log("[Ambience] 룸톤 " + _rooms.Count + " · 불안 레이어 " + _dread.Length + " · 원샷 목소리 " + _voices.Length, this);
        }
    }

    // ─────────────────────────────── 만들기 ───────────────────────────────

    private AudioClip LoadClip(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        AudioClip clip = Resources.Load<AudioClip>(path);
        if (clip == null && _missing.Add(path) && !_building)
        {
            Debug.LogWarning("[Ambience] 소리 파일이 없습니다: Resources/" + path + " — 그 소리만 빼고 재생합니다.", this);
        }

        return clip;
    }

    private static string First(HashSet<string> set)
    {
        foreach (string x in set)
        {
            return x;
        }

        return string.Empty;
    }

    private AudioSource MakeSource(string name, bool loop, float spatial)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);
        AudioSource s = go.AddComponent<AudioSource>();
        s.playOnAwake = false;
        s.loop = loop;
        s.spatialBlend = spatial;
        s.dopplerLevel = 0f;
        s.volume = 0f;
        return s;
    }

    private void BuildBase()
    {
        _base = MakeSource("School Base", true, 0f);
        _base.clip = LoadClip(config.BaseClip);
        _base.priority = 30;
    }

    private void BuildRooms()
    {
        AddRoom(config.DefaultZone);
        IReadOnlyList<AmbienceConfigSO.Zone> zones = config.Zones;
        for (int i = 0; i < zones.Count; i++)
        {
            AddRoom(zones[i]);
        }
    }

    private void AddRoom(AmbienceConfigSO.Zone z)
    {
        if (z == null || _roomByLabel.ContainsKey(z.label))
        {
            return;
        }

        RoomVoice v = new RoomVoice();
        v.label = z.label;
        v.zone = z;
        v.volume = z.roomVolume;
        v.source = MakeSource("Room " + z.label, true, 0f);
        v.source.clip = LoadClip(z.roomClip);
        v.source.priority = 32;
        if (!string.IsNullOrEmpty(z.roomClipHigh))
        {
            v.high = MakeSource("Room " + z.label + " High", true, 0f);
            v.high.clip = LoadClip(z.roomClipHigh);
            v.high.priority = 32;
        }

        _rooms.Add(v);
        _roomByLabel[z.label] = v;
    }

    private void BuildDread()
    {
        IReadOnlyList<string> clips = config.DreadClips;
        _dread = new AudioSource[clips.Count];
        _dreadLevel = new float[clips.Count];
        for (int i = 0; i < clips.Count; i++)
        {
            AudioSource s = MakeSource("Dread B" + (i + 1), true, 0f);
            s.clip = LoadClip(clips[i]);
            s.priority = 40;
            _dread[i] = s;
        }
    }

    private void BuildVoices()
    {
        _voices = new AudioSource[OneShotVoices];
        for (int i = 0; i < OneShotVoices; i++)
        {
            AudioSource s = MakeSource("OneShot " + i, false, 1f);
            s.rolloffMode = AudioRolloffMode.Logarithmic;
            s.minDistance = config.OneShotMinDistance;
            s.maxDistance = config.OneShotMaxDistance;
            s.priority = 96;
            _voices[i] = s;
        }

        _stinger = MakeSource("Stinger", false, 0f);
        _stinger.priority = 8;
    }

    // ─────────────────────────────── 구간 ───────────────────────────────

    private void OnBandChanged(SpaceId space, FearAxis axis, Band from, Band to)
    {
        if (axis != FearAxis.Auditory)
        {
            return;
        }

        int s = (int)space;
        if (s < 0 || s >= _auditory.Length)
        {
            return;
        }

        _auditory[s] = to;
        _known[s] = true;
    }

    private int BandFor(AmbienceConfigSO.Zone z)
    {
        if (z == null)
        {
            return 0;
        }

        switch (z.bandSource)
        {
            case AmbienceConfigSO.BandSource.MaxOfAll:
                return MaxBand();
            case AmbienceConfigSO.BandSource.MaxOfAllMinusOne:
                return Mathf.Max(0, MaxBand() - 1);
            default:
                int s = (int)z.space;
                return s >= 0 && s < _auditory.Length && _known[s] ? (int)_auditory[s] : 0;
        }
    }

    private int MaxBand()
    {
        int best = 0;
        for (int i = 0; i < _auditory.Length; i++)
        {
            if (_known[i] && (int)_auditory[i] > best)
            {
                best = (int)_auditory[i];
            }
        }

        return best;
    }

    // ─────────────────────────────── 매 프레임 ───────────────────────────────

    private void Update()
    {
        if (config == null)
        {
            return;
        }

        // 페이드는 실제 시간으로 — 일시정지(timeScale 0) 중에도 멈춘 소리가 어색하게 걸리지 않게.
        float dt = Time.unscaledDeltaTime;

        _sampleAcc += dt;
        if (_sampleAcc >= SampleSeconds)
        {
            _sampleAcc = 0f;
            SampleZone();
        }

        UpdateMaster(dt);
        UpdateBase(dt);
        UpdateRooms(dt);
        UpdateDread(dt);
        UpdateShots();
    }

    private void SampleZone()
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            return;
        }

        Vector3 p = cam.transform.position;
        AmbienceConfigSO.Zone found = null;
        IReadOnlyList<AmbienceConfigSO.Zone> zones = config.Zones;
        for (int i = 0; i < zones.Count; i++)
        {
            if (zones[i].box.Contains(p))
            {
                found = zones[i];
                break;
            }
        }

        if (found == null && config.Building.Contains(p))
        {
            found = config.DefaultZone;
        }

        string label = found != null ? found.label : string.Empty;
        if (label != _zoneLabel && logActions)
        {
            Debug.Log("[Ambience] 공간 " + (_zoneLabel.Length > 0 ? _zoneLabel : "밖") + " → " + (label.Length > 0 ? label : "밖"), this);
        }

        _zone = found;
        _zoneLabel = label;

        // 밤이 아니면(시작 전·결과 직전) 불안 레이어는 쉰다.
        int band = NightRun.IsNightActive ? BandFor(found) : 0;
        if (band != _band)
        {
            if (logActions)
            {
                Debug.Log("[Ambience] 청각 구간 " + _band + " → " + band, this);
            }

            // 구간이 오르면 다음 원샷을 새 간격의 최대치 안으로 당긴다(예전 느린 간격이 남지 않게).
            _shotTimer = Mathf.Min(_shotTimer, config.IntervalFor(band).y);
        }

        _band = band;
    }

    private void UpdateMaster(float dt)
    {
        float target = NightRun.IsCaptured ? 0f : 1f;
        float speed = 1f / Mathf.Max(0.05f, config.CaptureFadeSeconds);
        _master = Mathf.MoveTowards(_master, target, speed * dt);
        _duck = Mathf.MoveTowards(_duck, _duckTarget, dt / Mathf.Max(0.01f, _duckSeconds));
    }

    private float Gain
    {
        get { return config.MasterVolume * _master * _duck; }
    }

    private void UpdateBase(float dt)
    {
        float step = dt / Mathf.Max(0.05f, config.RoomFadeSeconds);
        _baseLevel = Mathf.MoveTowards(_baseLevel, _zoneLabel.Length > 0 ? 1f : 0f, step);
        Drive(_base, _baseLevel * config.BaseVolume * Gain);
    }

    private void UpdateRooms(float dt)
    {
        float step = dt / Mathf.Max(0.05f, config.RoomFadeSeconds);
        float mixStep = dt / Mathf.Max(0.05f, config.HighFadeSeconds);
        bool night = NightRun.IsNightActive;
        for (int i = 0; i < _rooms.Count; i++)
        {
            RoomVoice v = _rooms[i];
            float target = v.label == _zoneLabel ? 1f : 0f;
            v.level = Mathf.MoveTowards(v.level, target, step);

            // 강한 판: 그 공간의 청각 구간이 기준 이상일 때. 밤이 아니면 약한 판.
            bool wantHigh = v.high != null && v.high.clip != null && night && BandFor(v.zone) >= config.HighFromBand;
            if (v.level <= 0f)
            {
                v.mix = wantHigh ? 1f : 0f;   // 들리지 않는 동안은 바로 맞춰 둔다 — 들어설 때 넘어가는 소리가 나지 않게
            }
            else
            {
                v.mix = Mathf.MoveTowards(v.mix, wantHigh ? 1f : 0f, mixStep);
            }

            float lowGain = v.high != null ? Mathf.Cos(v.mix * Mathf.PI * 0.5f) : 1f;
            Drive(v.source, v.level * v.volume * lowGain * Gain);
            if (v.high != null) Drive(v.high, v.level * v.volume * Mathf.Sin(v.mix * Mathf.PI * 0.5f) * Gain);
        }
    }

    private void UpdateDread(float dt)
    {
        float step = dt / Mathf.Max(0.05f, config.DreadFadeSeconds);
        for (int i = 0; i < _dread.Length; i++)
        {
            float target = _band >= i + 1 && _zoneLabel.Length > 0 ? 1f : 0f;
            _dreadLevel[i] = Mathf.MoveTowards(_dreadLevel[i], target, step);
            float lv = _dreadLevel[i];
            // 등전력 곡선 — 선형 페이드는 중간이 꺼진 듯 들린다.
            Drive(_dread[i], Mathf.Sin(lv * Mathf.PI * 0.5f) * config.DreadLayerVolume(i) * config.DreadVolume * Gain);
        }
    }

    /// <summary>볼륨을 주고, 0이면 멈추고, 소리가 나야 하면 튼다. 루프는 무작위 지점에서 시작해 공간마다 같은 첫 소리가 반복되지 않게 한다.</summary>
    private void Drive(AudioSource s, float volume)
    {
        if (s == null || s.clip == null)
        {
            return;
        }

        s.volume = volume;
        if (volume > 0.0005f)
        {
            if (!s.isPlaying)
            {
                s.timeSamples = _rng.Next(0, Mathf.Max(1, s.clip.samples - 1));
                s.Play();
            }
        }
        else if (s.isPlaying)
        {
            s.Stop();
        }
    }

    // ─────────────────────────────── 원샷 ───────────────────────────────

    private void ScheduleNextShot()
    {
        Vector2 range = config.IntervalFor(_band);
        _shotTimer = Mathf.Lerp(range.x, range.y, (float)_rng.NextDouble());
    }

    private void UpdateShots()
    {
        // 게임 시간으로 센다 — 일시정지 중에는 원샷이 나지 않는다.
        if (_zone == null || _voices.Length == 0 || !NightRun.IsNightActive || NightRun.IsCaptured)
        {
            return;
        }

        _shotTimer -= Time.deltaTime;
        if (_shotTimer > 0f)
        {
            return;
        }

        ScheduleNextShot();

        List<string> pool = _zone.oneShots;
        if (pool == null || pool.Count == 0)
        {
            return;
        }

        string pick = pool[_rng.Next(pool.Count)];
        if (pool.Count > 1 && pick == _lastShot)
        {
            pick = pool[(pool.IndexOf(pick) + 1 + _rng.Next(pool.Count - 1)) % pool.Count];
        }

        Camera cam = Camera.main;
        if (cam == null)
        {
            return;
        }

        float angle = (float)_rng.NextDouble() * Mathf.PI * 2f;
        Vector2 d = config.OneShotDistance;
        float dist = Mathf.Lerp(d.x, d.y, (float)_rng.NextDouble());
        Vector3 pos = cam.transform.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * dist
                      + Vector3.up * Mathf.Lerp(-0.8f, 1.2f, (float)_rng.NextDouble());

        float vol = config.OneShotVolume * Mathf.Lerp(0.7f, 1f, (float)_rng.NextDouble());
        if (PlayAt(pick, pos, vol))
        {
            _lastShot = pick;
        }
    }

    // ─────────────────────────────── 공개 API ───────────────────────────────

    /// <summary>
    /// 원샷 하나를 월드 위치에서 튼다. 연출 코드가 「멀리서 노크」 같은 소리를 직접 낼 때 쓴다.
    /// <paramref name="path"/>는 Resources 기준 경로 또는 <c>Ambience/OneShots/</c> 아래 파일 이름.
    /// </summary>
    public bool PlayAt(string path, Vector3 position, float volume = 1f)
    {
        AudioClip clip = LoadClip(path.Contains("/") ? path : "Ambience/OneShots/" + path);
        if (clip == null || _voices.Length == 0)
        {
            return false;
        }

        AudioSource v = _voices[_nextVoice];
        _nextVoice = (_nextVoice + 1) % _voices.Length;
        v.transform.position = position;
        v.Stop();
        v.clip = clip;
        v.volume = Mathf.Clamp01(volume * Gain);
        v.pitch = Mathf.Lerp(0.94f, 1.06f, (float)_rng.NextDouble());   // 같은 파일이 똑같이 들리지 않게
        v.Play();

        if (logActions)
        {
            Debug.Log("[Ambience] 원샷 " + path + " @ " + position.ToString("F1"), this);
        }

        Action<string, Vector3> h = OneShotPlayed;
        if (h != null)
        {
            h(path, position);
        }

        return true;
    }

    /// <summary>
    /// 스팅어(점프스케어 효과음)를 2D로 튼다. 이름은 <c>stinger_hit</c> · <c>stinger_riser</c> · <c>stinger_breath</c> · <c>stinger_whisper</c>.
    /// 붙잡힘 페이드·덕킹의 영향을 받지 않는다 — 연출이 부르면 그대로 난다.
    /// </summary>
    public bool PlayStinger(string name, float volume = 1f)
    {
        AudioClip clip = LoadClip(name.Contains("/") ? name : "Ambience/Stingers/" + name);
        if (clip == null || _stinger == null)
        {
            return false;
        }

        _stinger.PlayOneShot(clip, Mathf.Clamp01(volume * config.StingerVolume * config.MasterVolume));
        return true;
    }

    /// <summary>
    /// 엠비언트 전체를 <paramref name="level"/>(0~1)까지 <paramref name="seconds"/>초에 걸쳐 줄인다(연출 중 정적을 만들 때).
    /// 1을 주면 되돌린다. 스팅어는 영향을 받지 않는다.
    /// </summary>
    public void Duck(float level, float seconds)
    {
        _duckTarget = Mathf.Clamp01(level);
        _duckSeconds = Mathf.Max(0.01f, seconds);
    }
}

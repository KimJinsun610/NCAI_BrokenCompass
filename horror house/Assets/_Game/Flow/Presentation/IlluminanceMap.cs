using System.Collections.Generic;
using NightDuty;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// 조도축 맵 변화(2026-10-03, 7단계 「로컬 Volume」 + 기획서 「구간별 맵·감각 변화 — 조도」). 공간마다 <b>그 공간에 보이는 조도 연출 구간</b>
/// (<see cref="NightRun.ShownBand"/>)을 화면과 등으로 그린다. 판정과 무관하다 — 읽기만 한다.
/// <list type="bullet">
/// <item><b>톤</b>: 공간마다 런타임 Volume 하나(전역, 플레이어 카메라의 볼륨 레이어, 우선순위 5 — 실내 보정 1 위, 조우 화면 효과 20~23 아래).
/// 플레이어가 서 있는 공간의 것만 무게 1(<see cref="IlluminanceToneSO.spaceBlendSeconds"/>에 걸쳐), 값은 그 공간 구간의 표 줄로
/// <see cref="IlluminanceToneSO.bandFadeSeconds"/>에 걸쳐 옮겨 간다. 실내 보정(<c>PP_Night_Interior</c>) 값에 얹으므로 밝기 설정·기존 비네팅을 덮지 않는다.
/// 방 상자 밖(좌측 복도 등)은 복도로 본다.</item>
/// <item><b>소등</b>: 복도 구간만큼 <b>좌측 복도 끝부터</b>(x 오름차순) 형광등을 끄고, 3구간부터 <b>과학실 앞 복도</b> 등도 끈다. 발광(LampFluo)도 함께
/// (<see cref="MaterialPropertyBlock"/> — 공유 머티리얼은 그대로). 조우 소등(<see cref="LightGroup"/>)이 다시 켜도 다음 프레임에 맞춘다.</item>
/// <item><b>화장실 조명이 붉어짐</b>: 화장실 구간 2부터.</item>
/// <item><b>일부 등만 붉어지거나 깜빡임</b>(103차): 공간 구간 3부터 그 공간의 켜진 천장 등 4분의 1(구간 4는 5분의 2, 적어도 하나)을 골라 깜빡이거나 붉게(공간·밤마다 번갈아). 밤마다 다른 등. 화장실은 빼고(이미 붉다), 구간 소등한 등·꺼 둔 교실 등·원래 깜빡이는 등·조우가 물들인 등은 건드리지 않는다.</item>
/// <item><b>모든 천장 등이 미세하게 붉어짐</b>(66차 민: 「조도 축이 오르면 전체 조명이 미세하게 붉어지고」): 등마다 제 공간 구간의 <see cref="IlluminanceTint.Amount"/>만큼 빛 색을 붉은 쪽으로(4초에 걸쳐). 다른 연출이 물들인 등(C4 단서 · 화장실 붉은 등)은 건드리지 않는다.</item>
/// </list>
/// 옛 SpaceLights(구간마다 등 8/6/4/2/0개 + 등 색온도)는 9.20V·최종 기획서와 달라 이 컴포넌트로 대신한다.
/// 근무 씬이면 스스로 설치된다(씬 파일을 고치지 않는다).
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(150)]
public sealed class IlluminanceMap : MonoBehaviour
{
    private const string InteriorVolumeName = "PostProcessing_interior";
    private const float Priority = 5f;
    private const float PollSeconds = 0.25f;

    /// <summary>톤을 그리는 공간(방 먼저, 복도는 마지막 = 어느 방에도 없을 때).</summary>
    private static readonly SpaceId[] ToneSpaces =
    {
        SpaceId.Classroom_1_3, SpaceId.ScienceRoom, SpaceId.Toilet, SpaceId.Library, SpaceId.SecurityRoom, SpaceId.Corridor
    };

    private sealed class Tone
    {
        public SpaceId Space;
        public bool HasBox;
        public Bounds Box;
        public Volume Volume;
        public VolumeProfile Profile;
        public WhiteBalance White;
        public ColorAdjustments Color;
        public Vignette Vignette;
        public Band Band;
        public IlluminanceToneSO.Row Now;
        public IlluminanceToneSO.Row Want;
    }

    private sealed class Lamp
    {
        public Light Light;
        public bool BaseEnabled;
        public Color BaseColor;
        public float BaseIntensity;
        public Renderer[] Glow;
        public Color[] GlowBase;
        public bool Off;
        public bool Red;
    }

    private sealed class Tintable
    {
        public Light Light;
        public Color Base;
        public SpaceId Space;
        public int Key;
        public float BaseIntensity;   // 103차 QA: 처음(설치 때) 세기 — 강조 등이 물든 세기를 기준으로 잡지 않게
    }

    /// <summary>103차: 구간 3부터 붉어지거나 깜빡이는 등 하나.</summary>
    private sealed class Accent
    {
        public Tintable Source;
        public Lamp Lamp;
        public bool Red;
        public float Seed;
        public bool Applied;
        public bool Dark;
    }

    /// <summary>103차: 등을 고르지 않는 공간(화장실은 구간 2부터 이미 붉다).</summary>
    private static readonly SpaceId[] AccentSpaces =
    {
        SpaceId.Corridor, SpaceId.Classroom_1_1, SpaceId.Classroom_1_3, SpaceId.ScienceRoom, SpaceId.Library, SpaceId.SecurityRoom
    };

    /// <summary>깜빡이는 등이 한 번 깜빡이는 간격(초, 등마다 조금씩 다름).</summary>
    private const float FlickerPeriod = 3.4f;

    /// <summary>깜빡임 한 번의 길이(초) — 그 안에서 빠르게 꺼졌다 켜진다.</summary>
    private const float FlickerBurst = 0.5f;

    /// <summary>깜빡여 어두울 때 남는 세기 비율.</summary>
    private const float FlickerDim = 0.06f;

    /// <summary>66차: 조도가 오를 때 천장 등이 옮겨 가는 붉은 색.</summary>
    public static readonly Color RedTint = new Color(IlluminanceTint.R, IlluminanceTint.G, IlluminanceTint.B);

    private const float TintSeconds = 4f;

    /// <summary>66차: 화면 색 필터가 옮겨 가는 붉은 쪽(4구간 36%면 (1, 0.93, 0.91) 정도).</summary>
    private static readonly Color FilterRed = new Color(1f, 0.8f, 0.76f);

    private static IlluminanceMap s_active;

    /// <summary>조도 구간 때문에 등 하나가 막 꺼졌다(인자: 등 위치). 소리(형광등이 지직이다 꺼짐)용 — 판정과 무관.</summary>
    public static event System.Action<Vector3> LampWentOff;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetLampEvent()
    {
        LampWentOff = null;
    }
    private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

    private readonly List<Tone> _tones = new List<Tone>();
    private readonly List<Lamp> _left = new List<Lamp>();
    private readonly List<Lamp> _scienceFront = new List<Lamp>();
    private readonly List<Lamp> _toilet = new List<Lamp>();
    private readonly List<Tintable> _tintable = new List<Tintable>();
    private readonly HashSet<Light> _redLamps = new HashSet<Light>();
    private readonly Dictionary<Light, Accent> _accents = new Dictionary<Light, Accent>();
    private readonly Dictionary<SpaceId, int> _accentKey = new Dictionary<SpaceId, int>();   // 103차 QA: 공간마다 고른 (일차·구간) — 같으면 다시 고르지 않는다
    private readonly Dictionary<SpaceId, float> _tintWant = new Dictionary<SpaceId, float>();
    private readonly Dictionary<SpaceId, float> _tintNow = new Dictionary<SpaceId, float>();
    private readonly List<KeyValuePair<SpaceId, Bounds>> _tintRooms = new List<KeyValuePair<SpaceId, Bounds>>();
    private IlluminanceToneSO _table;
    private MaterialPropertyBlock _block;
    private float _baseSaturation;
    private Color _baseFilter = Color.white;
    private float _baseVignette;
    private Color _baseVignetteColor = Color.black;
    private float _nextPoll;
    private bool _snapped;
    private SpaceId _current = SpaceId.Corridor;
    private Band _corridorBand;
    private Band _toiletBand;

    /// <summary>지금 살아 있는 것. 없으면 null.</summary>
    public static IlluminanceMap Active
    {
        get { return s_active; }
    }

    /// <summary>플레이어가 지금 톤을 받는 공간.</summary>
    public SpaceId CurrentSpace
    {
        get { return _current; }
    }

    /// <summary>좌측 복도에서 지금 끈 등 수.</summary>
    public int LeftOffCount
    {
        get
        {
            int n = 0;
            for (int i = 0; i < _left.Count; i++) if (_left[i].Off) n++;
            return n;
        }
    }

    /// <summary>과학실 앞 복도 등을 껐는지.</summary>
    public bool ScienceFrontOff
    {
        get { return _scienceFront.Count > 0 && _scienceFront[0].Off; }
    }

    /// <summary>화장실 조명이 붉은지.</summary>
    public bool ToiletRed
    {
        get { return _toilet.Count > 0 && _toilet[0].Red; }
    }

    /// <summary>66차: 그 공간 천장 등이 지금 붉은 쪽으로 옮겨 간 정도(0~1). 시험용.</summary>
    public float TintOf(SpaceId space)
    {
        float v;
        return _tintNow.TryGetValue(space, out v) ? v : 0f;
    }

    /// <summary>66차: 붉게 물들이는 천장 등 수. 시험용.</summary>
    public int TintableCount
    {
        get { return _tintable.Count; }
    }

    /// <summary>103차: 지금 붉어졌거나 깜빡이는 등 수(붉은 등, 깜빡이는 등). 시험용.</summary>
    public string AccentSummary
    {
        get
        {
            int red = 0, flicker = 0;
            foreach (Accent a in _accents.Values)
            {
                if (a.Red) red++;
                else flicker++;
            }

            return red + " / " + flicker;
        }
    }

    /// <summary>모은 등 수(좌측 복도, 과학실 앞, 화장실). 시험용.</summary>
    public string LampSummary
    {
        get { return _left.Count + " / " + _scienceFront.Count + " / " + _toilet.Count; }
    }

    /// <summary>그 공간 톤의 지금 무게(0~1). 시험용.</summary>
    public float WeightOf(SpaceId space)
    {
        Tone t = Find(space);
        return t != null && t.Volume != null ? t.Volume.weight : 0f;
    }

    /// <summary>그 공간 톤이 지금 따르는 구간. 시험용.</summary>
    public Band BandOf(SpaceId space)
    {
        Tone t = Find(space);
        return t != null ? t.Band : Band.Band0;
    }

    /// <summary>
    /// 좌측 복도에서 끌 등 수 = 복도 조도 구간(0~4). 표에 나오는 「좌측 복도 끝 1개(1) → 구간 수만큼(2~)」을 하나의 규칙으로 둔다.
    /// </summary>
    public static int LeftOffFor(Band corridorBand)
    {
        return Mathf.Clamp((int)corridorBand, 0, 4);
    }

    // ─────────────────────────────── 설치 ───────────────────────────────

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
        if (!FlowAutoInstall.IsDutyScene(scene) || FlowAutoInstall.Exists<IlluminanceMap>(scene)) return;
        if (Object.FindAnyObjectByType<SpaceZones>() == null) return;
        // hideFlags를 붙이지 않는다 — DontSave는 플레이를 꺼도 오브젝트를 남긴다(CLAUDE.md §5.5-23).
        FlowAutoInstall.CreateHost<IlluminanceMap>(scene, "IlluminanceMap (auto)");
    }

    // ─────────────────────────────── 준비 ───────────────────────────────

    private void OnEnable()
    {
        s_active = this;
    }

    private void OnDisable()
    {
        if (s_active == this) s_active = null;
        RestoreLamps();
    }

    private void OnDestroy()
    {
        for (int i = 0; i < _tones.Count; i++)
        {
            if (_tones[i].Profile != null) Destroy(_tones[i].Profile);
        }
    }

    private void Start()
    {
        _table = IlluminanceToneSO.Load();
        _block = new MaterialPropertyBlock();
        SpaceZones zones = FindAnyObjectByType<SpaceZones>();
        ReadBaseline();
        BuildTones(zones);
        CollectLamps(zones);
    }

    private void ReadBaseline()
    {
        Volume interior = null;
        foreach (Volume v in FindObjectsByType<Volume>(FindObjectsSortMode.None))
        {
            if (v.name == InteriorVolumeName && v.sharedProfile != null) interior = v;
        }

        if (interior == null) return;

        ColorAdjustments ca;
        if (interior.sharedProfile.TryGet(out ca))
        {
            if (ca.saturation.overrideState) _baseSaturation = ca.saturation.value;
            if (ca.colorFilter.overrideState) _baseFilter = ca.colorFilter.value;
        }

        Vignette vg;
        if (interior.sharedProfile.TryGet(out vg))
        {
            if (vg.intensity.overrideState) _baseVignette = vg.intensity.value;
            if (vg.color.overrideState) _baseVignetteColor = vg.color.value;
        }
    }

    private void BuildTones(SpaceZones zones)
    {
        int layer = VolumeLayer();
        for (int i = 0; i < ToneSpaces.Length; i++)
        {
            Tone t = new Tone { Space = ToneSpaces[i] };
            if (zones != null && ToneSpaces[i] != SpaceId.Corridor)
            {
                t.HasBox = zones.TryGetSpaceBox(ToneSpaces[i], out t.Box);
                if (!t.HasBox) continue;
            }

            GameObject go = new GameObject("Tone " + ToneSpaces[i]);
            go.transform.SetParent(transform, false);
            go.layer = layer;
            t.Volume = go.AddComponent<Volume>();
            t.Volume.isGlobal = true;
            t.Volume.priority = Priority;
            t.Volume.weight = 0f;

            t.Profile = ScriptableObject.CreateInstance<VolumeProfile>();
            t.Profile.name = "Tone " + ToneSpaces[i] + " (runtime)";
            t.White = t.Profile.Add<WhiteBalance>(true);
            t.Color = t.Profile.Add<ColorAdjustments>(false);
            t.Color.saturation.overrideState = true;
            t.Color.colorFilter.overrideState = true;
            t.Vignette = t.Profile.Add<Vignette>(false);
            t.Vignette.intensity.overrideState = true;
            t.Vignette.color.overrideState = true;
            t.Volume.sharedProfile = t.Profile;

            t.Now = _table.RowFor(Band.Band0);
            t.Want = t.Now;
            Apply(t);
            _tones.Add(t);
        }
    }

    /// <summary>플레이어 카메라가 보는 볼륨 레이어 중 첫 번째(이 프로젝트는 8 Viewmodel — 다른 레이어면 화면에 안 나온다).</summary>
    private static int VolumeLayer()
    {
        Camera cam = Camera.main;
        UniversalAdditionalCameraData data = cam != null ? cam.GetComponent<UniversalAdditionalCameraData>() : null;
        int mask = data != null ? data.volumeLayerMask.value : 1;
        for (int i = 0; i < 32; i++)
        {
            if ((mask & (1 << i)) != 0) return i;
        }

        return 0;
    }

    private void CollectLamps(SpaceZones zones)
    {
        List<Bounds> rooms = new List<Bounds>();
        Bounds science = default(Bounds);
        bool hasScience = false;
        Bounds toilet = default(Bounds);
        bool hasToilet = false;
        Bounds corridor = default(Bounds);
        bool hasCorridor = false;
        if (zones != null)
        {
            SpaceId[] roomIds = { SpaceId.Classroom_1_1, SpaceId.Classroom_1_3, SpaceId.ScienceRoom, SpaceId.Toilet, SpaceId.Library, SpaceId.SecurityRoom };
            for (int i = 0; i < roomIds.Length; i++)
            {
                Bounds b;
                if (zones.TryGetSpaceBox(roomIds[i], out b)) rooms.Add(Wide(b));
            }

            SpaceId[] tintIds = { SpaceId.Classroom_1_1, SpaceId.Classroom_1_3, SpaceId.ScienceRoom, SpaceId.Toilet, SpaceId.Library, SpaceId.SecurityRoom };
            for (int i = 0; i < tintIds.Length; i++)
            {
                Bounds b;
                if (zones.TryGetSpaceBox(tintIds[i], out b)) _tintRooms.Add(new KeyValuePair<SpaceId, Bounds>(tintIds[i], Wide(b)));
            }

            hasScience = zones.TryGetSpaceBox(SpaceId.ScienceRoom, out science);
            hasToilet = zones.TryGetSpaceBox(SpaceId.Toilet, out toilet);
            hasCorridor = zones.TryGetSpaceBox(SpaceId.Corridor, out corridor);
        }

        foreach (Light l in FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (l.type == LightType.Directional || l.bakingOutput.isBaked) continue;
            Transform lamp = l.transform.parent;
            if (lamp == null || !lamp.name.StartsWith("LampFluo")) continue;
            if (l.GetComponentInParent<FlashlightRelay>() != null || l.GetComponentInParent<CctvSystem>() != null) continue;
            Vector3 p = l.transform.position;
            _tintable.Add(new Tintable { Light = l, Base = l.color, Space = TintSpaceOf(p), Key = Mathf.RoundToInt(p.x * 10f) * 73856093 ^ Mathf.RoundToInt(p.y * 10f) * 19349663 ^ Mathf.RoundToInt(p.z * 10f) * 83492791, BaseIntensity = l.intensity });

            if (hasToilet && Wide(toilet).Contains(p))
            {
                _toilet.Add(MakeLamp(l, lamp));
                continue;
            }

            bool inRoom = false;
            for (int i = 0; i < rooms.Count && !inRoom; i++) inRoom = rooms[i].Contains(p);
            if (inRoom || UnderName(lamp, "Toilet")) continue;

            // 과학실 앞 복도: 과학실 상자의 x 범위 안, 과학실과 복도 북쪽 끝 사이.
            if (hasScience && hasCorridor && p.x >= science.min.x && p.x <= science.max.x && p.z >= science.max.z && p.z <= corridor.max.z)
            {
                _scienceFront.Add(MakeLamp(l, lamp));
                continue;
            }

            _left.Add(MakeLamp(l, lamp));
        }

        _left.Sort((a, b) => a.Light.transform.position.x.CompareTo(b.Light.transform.position.x));
    }

    private SpaceId TintSpaceOf(Vector3 p)
    {
        for (int i = 0; i < _tintRooms.Count; i++)
        {
            if (_tintRooms[i].Value.Contains(p)) return _tintRooms[i].Key;
        }

        return SpaceId.Corridor;
    }

    private static Bounds Wide(Bounds box)
    {
        // 천장 등은 상자 위에 있으므로 높이는 넉넉히(LightGroup과 같은 기준).
        return new Bounds(box.center + Vector3.up * 1f, new Vector3(box.size.x + 0.5f, box.size.y + 4f, box.size.z + 0.5f));
    }

    private static bool UnderName(Transform t, string part)
    {
        for (Transform p = t; p != null; p = p.parent)
        {
            if (p.name.Contains(part)) return true;
        }

        return false;
    }

    private static Lamp MakeLamp(Light l, Transform lamp)
    {
        Renderer[] glow = lamp.GetComponentsInChildren<Renderer>(true);
        Color[] baseGlow = new Color[glow.Length];
        for (int i = 0; i < glow.Length; i++)
        {
            Material m = glow[i].sharedMaterial;
            baseGlow[i] = m != null && m.HasProperty(EmissionId) ? m.GetColor(EmissionId) : Color.black;
        }

        return new Lamp
        {
            Light = l,
            BaseEnabled = l.enabled,
            BaseColor = l.color,
            BaseIntensity = l.intensity,
            Glow = glow,
            GlowBase = baseGlow,
        };
    }

    // ─────────────────────────────── 매 프레임 ───────────────────────────────

    private void LateUpdate()
    {
        if (_table == null) return;

        bool night = NightRun.IsNightActive;
        if (night && Time.unscaledTime >= _nextPoll)
        {
            _nextPoll = Time.unscaledTime + PollSeconds;
            Poll();
        }

        float dt = Time.unscaledDeltaTime;
        UpdateCurrentSpace();
        float fade = _snapped ? 1f - Mathf.Exp(-dt * 3f / Mathf.Max(0.01f, _table.bandFadeSeconds)) : 1f;
        float blend = dt / Mathf.Max(0.01f, _table.spaceBlendSeconds);
        for (int i = 0; i < _tones.Count; i++)
        {
            Tone t = _tones[i];
            t.Now = Lerp(t.Now, t.Want, fade);
            Apply(t);
            t.Volume.weight = Mathf.MoveTowards(t.Volume.weight, t.Space == _current ? 1f : 0f, _snapped ? blend : 1f);
        }

        if (night) _snapped = true;
        if (!night && _accents.Count > 0) ReleaseAllAccents();   // 103차 QA 3회: 밤이 끝나도(결과 화면까지) 깜빡이던 것
        EnforceLamps();
        DriveAccents();
        TintLamps(dt);
    }

    /// <summary>66차: 천장 등 빛 색을 공간 구간만큼 붉은 쪽으로(다른 연출이 물들인 등은 그대로).</summary>
    private void TintLamps(float dt)
    {
        List<SpaceId> keys = new List<SpaceId>(_tintWant.Keys);
        for (int i = 0; i < keys.Count; i++)
        {
            float now;
            _tintNow.TryGetValue(keys[i], out now);
            _tintNow[keys[i]] = Mathf.MoveTowards(now, _tintWant[keys[i]], dt / TintSeconds);
        }

        for (int i = 0; i < _tintable.Count; i++)
        {
            Tintable t = _tintable[i];
            if (t.Light == null || LightGroup.IsLightTinted(t.Light) || _redLamps.Contains(t.Light)) continue;
            float k;
            _tintNow.TryGetValue(t.Space, out k);
            // 등 색이 세기를 겸한 회색(0.4 등)이라 밝기는 그대로 두고 색만 — 붉은 쪽 목표 = 붉은 색 × 바탕 색의 가장 큰 성분.
            Color target = RedTint * Mathf.Max(t.Base.r, Mathf.Max(t.Base.g, t.Base.b));
            target.a = t.Base.a;
            Color c = Color.Lerp(t.Base, target, k);
            if (t.Light.color != c) t.Light.color = c;
        }
    }

    private void Poll()
    {
        for (int i = 0; i < _tones.Count; i++)
        {
            Tone t = _tones[i];
            t.Band = NightRun.ShownBand(t.Space, FearAxis.Illuminance);
            t.Want = _table.RowFor(t.Band);
        }

        _corridorBand = NightRun.ShownBand(SpaceId.Corridor, FearAxis.Illuminance);
        SpaceId[] tintSpaces = { SpaceId.Corridor, SpaceId.Classroom_1_1, SpaceId.Classroom_1_3, SpaceId.ScienceRoom, SpaceId.Toilet, SpaceId.Library, SpaceId.SecurityRoom };
        for (int i = 0; i < tintSpaces.Length; i++) _tintWant[tintSpaces[i]] = IlluminanceTint.Amount(NightRun.ShownBand(tintSpaces[i], FearAxis.Illuminance));
        _toiletBand = NightRun.ShownBand(SpaceId.Toilet, FearAxis.Illuminance);
        PlanAccents();
    }

    // ─────────────────────────────── 103차: 일부 등이 붉어지거나 깜빡임 ───────────────────────────────

    /// <summary>
    /// 공간 조도 구간이 3 이상이면 그 공간 천장 등 일부(<see cref="IlluminanceTint.AccentCount"/>)를 고른다 — 순서대로 깜빡임·붉음 번갈아.
    /// 밤마다 다른 등(<see cref="IlluminanceTint.AccentRank"/>), 같은 밤에는 같은 등. 구간이 내려가면(재시작) 되돌린다.
    /// </summary>
    private void PlanAccents()
    {
        Dictionary<Light, bool> want = new Dictionary<Light, bool>();
        Dictionary<Light, Tintable> source = new Dictionary<Light, Tintable>();
        HashSet<Light> fresh = new HashSet<Light>();   // 103차 QA 3회: 새로 고른 등 — 옛 강조가 남아 있어도 기준 발광을 새로 잡는다(전날 초록 C4 발광을 이어 쓰던 것)
        int day = NightRun.Day;

        // 구간 소등으로 꺼 둔 등(좌측 복도·과학실 앞)은 고르지 않는다.
        HashSet<Light> mapOff = new HashSet<Light>();
        // 103차 QA: 지금 꺼진 상태(Off)가 아니라 이번 구간에서 끌 등으로 본다 — 같은 폴링 뒤 EnforceLamps가 끄기 전에 골라, 곧 꺼질 등이 뽑히던 것.
        int leftOff = LeftOffFor(_corridorBand);
        for (int i = 0; i < _left.Count && i < leftOff; i++) mapOff.Add(_left[i].Light);
        if (_corridorBand >= _table.scienceFrontOffFrom) for (int i = 0; i < _scienceFront.Count; i++) mapOff.Add(_scienceFront[i].Light);

        for (int s = 0; s < AccentSpaces.Length; s++)
        {
            Band band = NightRun.ShownBand(AccentSpaces[s], FearAxis.Illuminance);
            int key = band < IlluminanceTint.AccentFrom ? -1 : day * 10 + (int)band;
            int was;
            if (key >= 0 && _accentKey.TryGetValue(AccentSpaces[s], out was) && was == key)
            {
                // 103차 QA: 같은 밤·같은 구간이면 고른 등을 그대로 둔다(조우 소등·물듦으로 켜진 등 수가 잠깐 바뀌어도 다시 고르지 않는다).
                foreach (KeyValuePair<Light, Accent> kv in _accents)
                {
                    if (kv.Key == null || kv.Value.Source.Space != AccentSpaces[s] || mapOff.Contains(kv.Key)) continue;
                    want[kv.Key] = kv.Value.Red;
                    source[kv.Key] = kv.Value.Source;
                }

                continue;
            }

            if (key < 0)
            {
                _accentKey.Remove(AccentSpaces[s]);
                continue;
            }

            // 켜져 있는 등만(꺼 둔 교실 등 · 원래 깜빡이는 등 · 조우가 물들인 등은 빼고).
            List<Tintable> here = new List<Tintable>();
            for (int i = 0; i < _tintable.Count; i++)
            {
                Tintable t = _tintable[i];
                if (t.Light == null || t.Space != AccentSpaces[s] || mapOff.Contains(t.Light) || !t.Light.enabled || LightGroup.IsLightTinted(t.Light)) continue;
                if (t.Light.transform.parent != null && t.Light.transform.parent.name.IndexOf("flicker", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;
                if (RedLightSpot.Active != null && RedLightSpot.Active.OwnsLampNear(t.Light.transform.position)) continue;   // 103차 QA 3회: C4 초록 등은 고르지 않는다
                here.Add(t);
            }

            int n = IlluminanceTint.AccentCount(here.Count, band);
            if (n == 0) continue;   // 켜진 등이 없으면 다음 폴링에 다시 본다(열쇠를 적지 않는다)
            _accentKey[AccentSpaces[s]] = key;
            here.Sort((a, b) => IlluminanceTint.AccentRank(a.Key, day).CompareTo(IlluminanceTint.AccentRank(b.Key, day)));
            for (int k = 0; k < n; k++)
            {
                fresh.Add(here[k].Light);
                want[here[k].Light] = IlluminanceTint.AccentRed(k + s + day);   // 공간마다·밤마다 깜빡임과 붉음이 번갈아(공간에 한 개뿐이어도 둘 다 보이게)
                source[here[k].Light] = here[k];
            }
        }

        List<Light> drop = new List<Light>();
        foreach (KeyValuePair<Light, Accent> kv in _accents)
        {
            bool red;
            if (!want.TryGetValue(kv.Key, out red) || red != kv.Value.Red || fresh.Contains(kv.Key)) drop.Add(kv.Key);
        }

        for (int i = 0; i < drop.Count; i++)
        {
            ReleaseAccent(_accents[drop[i]]);
            _accents.Remove(drop[i]);
        }

        foreach (KeyValuePair<Light, bool> kv in want)
        {
            if (_accents.ContainsKey(kv.Key)) continue;
            Tintable t = source[kv.Key];
            Lamp lamp = MakeLamp(kv.Key, kv.Key.transform.parent);
            lamp.BaseIntensity = t.BaseIntensity;   // 103차 QA: 지금(물든·깜빡이는 중일 수 있는) 세기가 아니라 처음 세기
            _accents[kv.Key] = new Accent
            {
                Source = t,
                Lamp = lamp,
                Red = kv.Value,
                Seed = (IlluminanceTint.AccentRank(t.Key, 7) % 1000u) / 100f,
            };
        }
    }

    /// <summary>매 프레임 — 붉은 등은 한 번 물들이고, 깜빡이는 등은 세기·발광을 흔든다. 꺼진 등·조우가 물들인 등은 건드리지 않는다.</summary>
    private void DriveAccents()
    {
        if (_accents.Count == 0) return;
        float now = Time.time;
        foreach (Accent a in _accents.Values)
        {
            Light l = a.Lamp.Light;
            if (l == null) continue;
            if (!l.enabled || LightGroup.IsLightTinted(l))
            {
                // 구간 소등·조우 소등 중이면 손대지 않는다(다시 켜지면 이어서).
                if (a.Applied && !a.Red) l.intensity = a.Lamp.BaseIntensity;
                a.Applied = false;
                continue;
            }

            if (a.Red)
            {
                if (a.Applied) continue;
                l.color = _table.toiletRed;
                l.intensity = a.Lamp.BaseIntensity * _table.toiletRedIntensity;
                _redLamps.Add(l);
                SetGlow(a.Lamp, _table.toiletRed, 1f);
                a.Applied = true;
                continue;
            }

            if (InspectionAnomalies.PhotosensitiveSafe)
            {
                // 103차 QA: 광과민 옵션이면 빠른 깜빡임 대신 느린 맥동(점검 이상 T-3과 같은 상한).
                float pulse = 0.55f + 0.45f * Mathf.Sin((now + a.Seed) * Mathf.PI * 2f * AnomalyLook.SlowFlickerMaxHz * 0.5f);
                l.intensity = a.Lamp.BaseIntensity * pulse;
                SetGlow(a.Lamp, Color.white, pulse);
                a.Applied = true;
                a.Dark = false;
                continue;
            }

            float period = FlickerPeriod + a.Seed * 0.15f;
            float phase = Mathf.Repeat(now + a.Seed, period);
            bool dark = phase < FlickerBurst && Mathf.PerlinNoise(a.Seed * 3.1f, now * 22f) < 0.55f;
            if (a.Applied && dark == a.Dark) continue;
            l.intensity = a.Lamp.BaseIntensity * (dark ? FlickerDim : 1f);
            SetGlow(a.Lamp, Color.white, dark ? FlickerDim : 1f);
            a.Dark = dark;
            a.Applied = true;
        }
    }

    private void SetGlow(Lamp lamp, Color tint, float scale)
    {
        for (int i = 0; i < lamp.Glow.Length; i++)
        {
            Renderer r = lamp.Glow[i];
            if (r == null) continue;
            r.GetPropertyBlock(_block);
            _block.SetColor(EmissionId, lamp.GlowBase[i] * tint * scale);
            r.SetPropertyBlock(_block);
        }
    }

    private void ReleaseAllAccents()
    {
        foreach (Accent a in _accents.Values) ReleaseAccent(a);
        _accents.Clear();
        _accentKey.Clear();
    }

    private void ReleaseAccent(Accent a)
    {
        Light l = a.Lamp.Light;
        if (l == null) return;
        if (a.Red)
        {
            _redLamps.Remove(l);
            if (!LightGroup.IsLightTinted(l)) l.color = a.Source.Base;   // 다음 프레임 TintLamps가 다시 미세하게 물들인다
        }

        l.intensity = a.Lamp.BaseIntensity;
        if (l.enabled)
        {
            for (int i = 0; i < a.Lamp.Glow.Length; i++)
            {
                if (a.Lamp.Glow[i] != null) a.Lamp.Glow[i].SetPropertyBlock(null);
            }
        }
    }

    private void UpdateCurrentSpace()
    {
        Camera cam = Camera.main;
        if (cam == null) return;
        Vector3 p = cam.transform.position;
        SpaceId found = SpaceId.Corridor;
        for (int i = 0; i < _tones.Count; i++)
        {
            Tone t = _tones[i];
            if (!t.HasBox) continue;
            Bounds b = t.Box;
            b.Expand(new Vector3(0f, 2f, 0f));   // 눈높이가 상자 위로 나가도 그 방이다.
            if (b.Contains(p))
            {
                found = t.Space;
                break;
            }
        }

        _current = found;
    }

    private void Apply(Tone t)
    {
        IlluminanceToneSO.Row r = t.Now;
        t.White.temperature.value = r.temperature;
        t.White.tint.value = r.tint;
        t.Color.saturation.value = Mathf.Clamp(_baseSaturation + r.saturation, -100f, 100f);
        float red;
        _tintNow.TryGetValue(t.Space, out red);
        t.Color.colorFilter.value = _baseFilter * r.colorFilter * Color.Lerp(Color.white, FilterRed, red);   // 66차: 베이크된 빛까지 「전체가 미세하게」 — 화면 필터도 같은 만큼
        t.Vignette.intensity.value = Mathf.Max(_baseVignette, r.vignette);
        t.Vignette.color.value = r.vignette > _baseVignette ? r.vignetteColor : _baseVignetteColor;
    }

    private static IlluminanceToneSO.Row Lerp(IlluminanceToneSO.Row a, IlluminanceToneSO.Row b, float k)
    {
        return new IlluminanceToneSO.Row
        {
            temperature = Mathf.Lerp(a.temperature, b.temperature, k),
            tint = Mathf.Lerp(a.tint, b.tint, k),
            saturation = Mathf.Lerp(a.saturation, b.saturation, k),
            colorFilter = Color.Lerp(a.colorFilter, b.colorFilter, k),
            vignette = Mathf.Lerp(a.vignette, b.vignette, k),
            vignetteColor = Color.Lerp(a.vignetteColor, b.vignetteColor, k),
        };
    }

    // ─────────────────────────────── 등 ───────────────────────────────

    private void EnforceLamps()
    {
        int leftOff = LeftOffFor(_corridorBand);
        for (int i = 0; i < _left.Count; i++) SetOff(_left[i], i < leftOff);

        bool front = _corridorBand >= _table.scienceFrontOffFrom;
        for (int i = 0; i < _scienceFront.Count; i++) SetOff(_scienceFront[i], front);

        bool red = _toiletBand >= _table.toiletRedFrom;
        for (int i = 0; i < _toilet.Count; i++) SetRed(_toilet[i], red);
    }

    private void SetOff(Lamp lamp, bool off)
    {
        if (lamp.Light == null) return;
        if (off)
        {
            // 조우 소등이 끝나며 다시 켜도(LightGroup.TurnOn) 다음 프레임에 맞춘다.
            if (lamp.Light.enabled) lamp.Light.enabled = false;
            if (!lamp.Off)
            {
                System.Action<Vector3> died = LampWentOff;
                if (died != null) died(lamp.Light.transform.position);
            }

            for (int i = 0; i < lamp.Glow.Length; i++)
            {
                Renderer r = lamp.Glow[i];
                if (r == null || (lamp.Off && r.HasPropertyBlock())) continue;
                r.GetPropertyBlock(_block);
                _block.SetColor(EmissionId, Color.black);
                r.SetPropertyBlock(_block);
            }

            lamp.Off = true;
            return;
        }

        if (!lamp.Off) return;
        lamp.Off = false;
        lamp.Light.enabled = lamp.BaseEnabled;
        for (int i = 0; i < lamp.Glow.Length; i++)
        {
            if (lamp.Glow[i] != null) lamp.Glow[i].SetPropertyBlock(null);
        }
    }

    private void SetRed(Lamp lamp, bool red)
    {
        if (lamp.Light == null) return;
        if (red)
        {
            if (!lamp.Red)
            {
                lamp.Light.color = _table.toiletRed;
                lamp.Light.intensity = lamp.BaseIntensity * _table.toiletRedIntensity;
                _redLamps.Add(lamp.Light);
            }

            for (int i = 0; i < lamp.Glow.Length; i++)
            {
                Renderer r = lamp.Glow[i];
                if (r == null || (lamp.Red && r.HasPropertyBlock())) continue;
                r.GetPropertyBlock(_block);
                _block.SetColor(EmissionId, lamp.GlowBase[i] * _table.toiletRed);
                r.SetPropertyBlock(_block);
            }

            lamp.Red = true;
            return;
        }

        if (!lamp.Red) return;
        lamp.Red = false;
        _redLamps.Remove(lamp.Light);
        lamp.Light.color = lamp.BaseColor;
        lamp.Light.intensity = lamp.BaseIntensity;
        for (int i = 0; i < lamp.Glow.Length; i++)
        {
            if (lamp.Glow[i] != null) lamp.Glow[i].SetPropertyBlock(null);
        }
    }

    private void RestoreLamps()
    {
        List<Lamp> all = new List<Lamp>();
        all.AddRange(_left);
        all.AddRange(_scienceFront);
        for (int i = 0; i < all.Count; i++) SetOff(all[i], false);
        for (int i = 0; i < _toilet.Count; i++) SetRed(_toilet[i], false);
        foreach (Accent a in _accents.Values) ReleaseAccent(a);
        _accents.Clear();
        _accentKey.Clear();
        for (int i = 0; i < _tintable.Count; i++)
        {
            if (_tintable[i].Light != null && !LightGroup.IsLightTinted(_tintable[i].Light)) _tintable[i].Light.color = _tintable[i].Base;
        }

        _tintNow.Clear();
    }

    private Tone Find(SpaceId space)
    {
        for (int i = 0; i < _tones.Count; i++)
        {
            if (_tones[i].Space == space) return _tones[i];
        }

        return null;
    }
}

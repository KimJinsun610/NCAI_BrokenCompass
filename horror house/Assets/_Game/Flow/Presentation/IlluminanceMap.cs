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
        EnforceLamps();
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
        _toiletBand = NightRun.ShownBand(SpaceId.Toilet, FearAxis.Illuminance);
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
        t.Color.colorFilter.value = _baseFilter * r.colorFilter;
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

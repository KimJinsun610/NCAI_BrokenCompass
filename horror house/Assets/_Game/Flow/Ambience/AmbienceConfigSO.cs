using System;
using System.Collections.Generic;
using NightDuty;
using UnityEngine;

/// <summary>
/// 엠비언트(학교 공통 바탕 · 공간 룸톤 약/강 · 청각축 불안 레이어 · 무작위 원샷) 설정.
///
/// <para><b>소리 파일은 이름으로 찾는다.</b> 전부 <c>Resources/Ambience/…</c> 아래에 두고
/// <see cref="Resources.Load{T}(string)"/>로 읽는다. 에셋이 파일을 직접 가리키지 않으므로
/// 파일을 나중에 넣거나 바꿔도 이 에셋을 고칠 필요가 없다. 없는 파일은 조용히 건너뛴다.</para>
///
/// <para><b>공간 판정은 이 에셋의 상자로 한다.</b> <see cref="SpaceZones"/>는 판정용이라 도서관·경비실 상자가 없고,
/// 소리는 판정과 무관하게 방의 실제 벽을 따라야 하기 때문이다. 상자는 2026-09-30 <c>PlayScene</c> 레이캐스트 실측값이다.
/// 어느 상자에도 없고 건물 안이면 <see cref="DefaultLabel"/>(복도)이다.</para>
///
/// <para>에셋이 없으면 <see cref="Load"/>가 코드 기본값으로 만든다.</para>
/// </summary>
[CreateAssetMenu(fileName = "AmbienceConfig", menuName = "NightDuty/엠비언트 설정")]
public sealed class AmbienceConfigSO : ScriptableObject
{
    public const string ResourceName = "AmbienceConfig";

    /// <summary>불안 레이어가 따라갈 청각 구간을 어디서 가져오는가.</summary>
    public enum BandSource
    {
        /// <summary><see cref="Zone.space"/>의 청각 구간.</summary>
        Space = 0,

        /// <summary>알려진 모든 공간 중 가장 높은 청각 구간.</summary>
        MaxOfAll = 1,

        /// <summary>가장 높은 구간보다 한 칸 낮게. 경비실처럼 '안전하지만 밖이 새어 드는' 곳.</summary>
        MaxOfAllMinusOne = 2,
    }

    [Serializable]
    public sealed class Zone
    {
        [Tooltip("같은 이름의 상자는 한 공간으로 친다(ㄱ자 방을 상자 둘로 덮을 때).")]
        public string label = "공간";

        [Tooltip("룸톤 루프(약한 판). Resources 기준 경로(확장자 없이).")]
        public string roomClip = "Ambience/Rooms/AMB_HALL_Low";

        [Tooltip("룸톤 강한 판. 이 공간의 청각 구간이 HighFromBand 이상이면 약한 판에서 이쪽으로 넘어간다. 비우면 약한 판만.")]
        public string roomClipHigh = "Ambience/Rooms/AMB_HALL_High";

        [Tooltip("월드 상자. 플레이어 카메라(귀) 위치로 검사한다.")]
        public Bounds box;

        public BandSource bandSource = BandSource.Space;

        [Tooltip("bandSource가 Space일 때 청각 구간을 읽을 판정 공간.")]
        public SpaceId space = SpaceId.Corridor;

        [Tooltip("이 공간에서 이따금 들리는 원샷들(Resources 기준 경로).")]
        public List<string> oneShots = new List<string>();

        [Range(0f, 2f)] public float roomVolume = 1f;
    }

    [Header("전체")]
    [Tooltip("끄면 엠비언트가 아예 설치되지 않는다.")]
    [SerializeField] private bool enabledInGame = true;

    [Range(0f, 2f)] [SerializeField] private float masterVolume = 1f;

    [Header("공간")]
    [SerializeField] private List<Zone> zones = new List<Zone>();

    [Tooltip("어느 상자에도 없고 건물 안일 때의 공간(복도).")]
    [SerializeField] private Zone defaultZone = new Zone();

    [Tooltip("건물 범위. 이 밖이면 룸톤이 모두 잦아든다. SpaceZones의 building과 같은 값.")]
    [SerializeField] private Bounds building = new Bounds(new Vector3(20f, 3.4f, 42f), new Vector3(70f, 4f, 30f));

    [Tooltip("공간을 옮길 때 룸톤이 바뀌는 시간(초).")]
    [Min(0.05f)] [SerializeField] private float roomFadeSeconds = 2.5f;

    [Tooltip("룸톤이 강한 판으로 넘어가는 청각 구간(이 값 이상). 2026-10-04 민 결정 = 3.")]
    [Range(1, 4)] [SerializeField] private int highFromBand = 3;

    [Tooltip("약한 판 ↔ 강한 판이 바뀌는 시간(초).")]
    [Min(0.05f)] [SerializeField] private float highFadeSeconds = 5f;

    [Header("학교 공통 바탕 — 건물 안이면 어디서나 룸톤 밑에 깔린다")]
    [SerializeField] private string baseClip = "Ambience/Base/AMB_COMMON_School_Base";

    [Range(0f, 2f)] [SerializeField] private float baseVolume = 0.45f;

    [Header("청각축 불안 레이어 — 구간 n이면 1..n이 쌓인다")]
    [SerializeField] private string[] dreadClips =
    {
        "Ambience/Dread/AMB_Dread_B1",
        "Ambience/Dread/AMB_Dread_B2",
        "Ambience/Dread/AMB_Dread_B3",
        "Ambience/Dread/dread_b4",   // 전달본 B4는 보류(2026-10-04) — 예전 파일을 그대로 쓴다
    };

    [SerializeField] private float[] dreadVolumes = { 1f, 1f, 1f, 1f };

    [Range(0f, 2f)] [SerializeField] private float dreadVolume = 1f;

    [Tooltip("구간이 바뀔 때 레이어가 차오르고 빠지는 시간(초). 길수록 '어느새' 느낌.")]
    [Min(0.05f)] [SerializeField] private float dreadFadeSeconds = 6f;

    [Header("원샷 — 청각 구간별 간격(초)")]
    [SerializeField] private Vector2[] oneShotInterval =
    {
        new Vector2(45f, 90f),
        new Vector2(30f, 60f),
        new Vector2(20f, 40f),
        new Vector2(12f, 28f),
        new Vector2(7f, 18f),
    };

    [Range(0f, 2f)] [SerializeField] private float oneShotVolume = 0.8f;

    [Tooltip("원샷이 나는 거리(m). 너무 가까우면 연출처럼 들린다.")]
    [SerializeField] private Vector2 oneShotDistance = new Vector2(6f, 14f);

    [Tooltip("3D 소리가 줄어들기 시작하는 거리(m).")]
    [Min(0.1f)] [SerializeField] private float oneShotMinDistance = 3f;

    [Min(1f)] [SerializeField] private float oneShotMaxDistance = 35f;

    [Header("스팅어")]
    [Range(0f, 2f)] [SerializeField] private float stingerVolume = 1f;

    [Header("붙잡혔을 때")]
    [Tooltip("붙잡히면 엠비언트를 이 시간(초)에 걸쳐 끈다. 포획 연출이 소리를 차지한다.")]
    [Min(0.05f)] [SerializeField] private float captureFadeSeconds = 1f;

    public bool EnabledInGame { get { return enabledInGame; } }
    public float MasterVolume { get { return masterVolume; } }
    public IReadOnlyList<Zone> Zones { get { return zones; } }
    public Zone DefaultZone { get { return defaultZone; } }
    public string DefaultLabel { get { return defaultZone != null ? defaultZone.label : "복도"; } }
    public Bounds Building { get { return building; } }
    public float RoomFadeSeconds { get { return roomFadeSeconds; } }
    public int HighFromBand { get { return highFromBand; } }
    public float HighFadeSeconds { get { return highFadeSeconds; } }
    public string BaseClip { get { return baseClip; } }
    public float BaseVolume { get { return baseVolume; } }
    public IReadOnlyList<string> DreadClips { get { return dreadClips; } }
    public float DreadVolume { get { return dreadVolume; } }
    public float DreadFadeSeconds { get { return dreadFadeSeconds; } }
    public float OneShotVolume { get { return oneShotVolume; } }
    public Vector2 OneShotDistance { get { return oneShotDistance; } }
    public float OneShotMinDistance { get { return oneShotMinDistance; } }
    public float OneShotMaxDistance { get { return oneShotMaxDistance; } }
    public float StingerVolume { get { return stingerVolume; } }
    public float CaptureFadeSeconds { get { return captureFadeSeconds; } }

    /// <summary>에디터 전용 — 인스펙터 버튼이 공간 목록을 고칠 때 쓴다.</summary>
    public List<Zone> EditableZones { get { return zones; } }

    public float DreadLayerVolume(int layer)
    {
        return dreadVolumes != null && layer >= 0 && layer < dreadVolumes.Length ? dreadVolumes[layer] : 1f;
    }

    /// <summary>청각 구간(0~4)의 원샷 간격(최소·최대 초).</summary>
    public Vector2 IntervalFor(int band)
    {
        if (oneShotInterval == null || oneShotInterval.Length == 0)
        {
            return new Vector2(30f, 60f);
        }

        int i = Mathf.Clamp(band, 0, oneShotInterval.Length - 1);
        return oneShotInterval[i];
    }

    public static AmbienceConfigSO Load()
    {
        AmbienceConfigSO found = Resources.Load<AmbienceConfigSO>(ResourceName);
        if (found != null && found.zones != null && found.zones.Count > 0)
        {
            return found;
        }

        AmbienceConfigSO made = CreateInstance<AmbienceConfigSO>();
        made.name = ResourceName + " (기본값)";
        made.FillDefaults();
        return made;
    }

    /// <summary>
    /// 기본 공간 6개. 상자는 2026-09-30 <c>PlayScene</c> 레이캐스트 실측이다(바닥 y=1.5, 귀 높이 약 2.6).
    /// 목록 순서가 우선순위다 — 앞의 상자가 이긴다.
    /// </summary>
    public void FillDefaults()
    {
        const float Y = 3.4f;
        const float H = 4f;

        zones = new List<Zone>
        {
            Make("경비실", "GUARD", Box(30.3f, 37.8f, 44.2f, 48.1f, Y, H), BandSource.MaxOfAllMinusOne, SpaceId.None,
                "os_wind_gust", "os_knock_1a", "os_knock_2a", "os_floor_creak_a"),
            Make("과학실", "LAB", Box(43.3f, 54.1f, 40.2f, 43.6f, Y, H), BandSource.Space, SpaceId.ScienceRoom,
                "os_electric_pop", "os_metal_groan_a", "os_pipe_clank_a", "os_floor_creak_b", "os_fluoro_flicker"),
            Make("교실", "CLASS", Box(38.3f, 49.8f, 30.0f, 39.7f, Y, H), BandSource.Space, SpaceId.Classroom_1_3,
                "os_debris", "os_floor_creak_a", "os_floor_creak_c", "os_wind_gust", "os_knock_2b"),
            Make("화장실", "TOILET", Box(-3.1f, 6.0f, 31.3f, 40.0f, Y, H), BandSource.Space, SpaceId.Toilet,
                "os_drip_a", "os_drip_b", "os_pipe_clank_b", "os_knock_4a", "os_metal_groan_b"),
            // 도서관은 ㄱ자라 상자 둘. 판정 공간(SpaceId)이 아직 없어 옆 복도의 청각 구간을 따른다.
            // 61차(민: 「도서관에서 고정적으로 등장하는 쾅 소리 제거」): 도서관 원샷에서 os_thud_upstairs를 뺐다.
            Make("도서관", "LIBRARY", Box(2.0f, 14.4f, 40.0f, 56.0f, Y, H), BandSource.Space, SpaceId.Corridor,
                "os_floor_creak_c", "os_knock_1a", "os_electric_pop"),
            Make("도서관", "LIBRARY", Box(14.4f, 18.0f, 48.5f, 56.0f, Y, H), BandSource.Space, SpaceId.Corridor,
                "os_floor_creak_c", "os_knock_1a", "os_electric_pop"),
        };

        // 61차(「화장실 엠비언스가 너무 작다」): 화장실 방 소리는 +7dB 판(Rooms/AMB_TOILET_*_Loud).
        Zone toilet = zones.Find(z => z.label == "화장실");
        if (toilet != null)
        {
            toilet.roomClip += "_Loud";
            toilet.roomClipHigh += "_Loud";
        }

        defaultZone = Make("복도", "HALL", new Bounds(), BandSource.Space, SpaceId.Corridor,
            "os_knock_1a", "os_knock_2a", "os_knock_2b", "os_knock_4a", "os_door_slam", "os_thud_upstairs",
            "os_pipe_clank_a", "os_pipe_clank_b", "os_metal_groan_a", "os_metal_groan_b", "os_fluoro_flicker",
            "os_floor_creak_b");
    }

    private static Bounds Box(float x0, float x1, float z0, float z1, float y, float h)
    {
        return new Bounds(new Vector3((x0 + x1) * 0.5f, y, (z0 + z1) * 0.5f), new Vector3(x1 - x0, h, z1 - z0));
    }

    private static Zone Make(string label, string room, Bounds box, BandSource source, SpaceId space, params string[] shots)
    {
        Zone z = new Zone();
        z.label = label;
        z.roomClip = "Ambience/Rooms/AMB_" + room + "_Low";
        z.roomClipHigh = "Ambience/Rooms/AMB_" + room + "_High";
        z.box = box;
        z.bandSource = source;
        z.space = space;
        z.oneShots = new List<string>();
        for (int i = 0; i < shots.Length; i++)
        {
            z.oneShots.Add("Ambience/OneShots/" + shots[i]);
        }

        return z;
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 경비실 CCTV 설정. 채널(카메라 자리)과 화면 모양을 여기서 정한다.
///
/// <para><b>왜 씬이 아니라 에셋인가.</b> 카메라를 씬에 두면 <c>PlayScene.unity</c>를 고쳐야 하고,
/// 씬은 LFS 잠금 대상이다(CLAUDE.md §9). 에셋은 잠금 없이 고칠 수 있고, 씬과 무관하게 팀원 모두에게 같이 간다.
/// <see cref="CctvSystem"/>이 플레이할 때 이 값을 읽어 카메라를 만든다 — <see cref="PlayerInteractor"/>의 자동 설치와 같은 방식이다.</para>
///
/// <para><b>튜닝은 인스펙터에서.</b> 채널마다 [씬 뷰로 보기]로 지금 화각을 확인하고, 씬 뷰를 옮긴 뒤
/// [현재 씬 뷰로 저장]을 누르면 그 자리가 채널이 된다(<c>CctvConfigSOEditor</c>).</para>
///
/// <para>에셋이 없으면 <see cref="Load"/>가 코드 기본값으로 만든다. 기본값은 2026-09-30에 <c>PlayScene</c>에서
/// 씬 뷰로 직접 보고 고른 자리다.</para>
/// </summary>
[CreateAssetMenu(fileName = "CctvConfig", menuName = "NightDuty/CCTV 설정")]
public sealed class CctvConfigSO : ScriptableObject
{
    /// <summary><c>Resources/</c> 아래 이름. <see cref="Load"/>가 이것으로 찾는다.</summary>
    public const string ResourceName = "CctvConfig";

    [Serializable]
    public sealed class Channel
    {
        [Tooltip("화면 왼쪽 위에 찍히는 이름. 예: 복도")]
        public string label = "채널";

        [Tooltip("카메라 위치(월드).")]
        public Vector3 position;

        [Tooltip("카메라가 바라보는 점(월드).")]
        public Vector3 lookAt;

        [Tooltip("세로 화각(도). CCTV는 넓게 잡는 편이 그럴듯하다.")]
        [Range(30f, 110f)] public float fieldOfView = 70f;

        [Header("적외선 조명 — 이 카메라가 그릴 때만 켜진다")]
        [Tooltip("0이면 끈다. 밤 씬이 거의 새까매서 이것이 없으면 화면에 아무것도 안 보인다.")]
        [Min(0f)] public float irIntensity = 2.5f;

        [Min(1f)] public float irRange = 30f;

        [Range(20f, 170f)] public float irSpotAngle = 100f;
    }

    [Header("채널")]
    [SerializeField] private List<Channel> channels = new List<Channel>();

    [Header("모니터")]
    [Tooltip("화면을 붙일 모니터의 씬 경로. 못 찾으면 이름(마지막 마디)으로 다시 찾는다.")]
    [SerializeField] private string monitorPath = "Interior/janitor's room/Old CRT Monitor";

    [Tooltip("모니터 메시 좌표계에서 화면 중심. Old CRT Monitor는 Z가 위, -Y가 앞이다(2026-09-30 메시 실측).")]
    [SerializeField] private Vector3 screenLocalCenter = new Vector3(0f, -0.472f, 0.075f);

    [Tooltip("화면 크기(m, 가로·세로). 4:3.")]
    [SerializeField] private Vector2 screenSize = new Vector2(0.30f, 0.225f);

    [Header("렌더")]
    [SerializeField] private Vector2Int resolution = new Vector2Int(320, 240);

    [Tooltip("모니터를 들여다보는 동안의 초당 프레임. 낮을수록 CCTV답게 끊긴다.")]
    [Range(2f, 30f)] [SerializeField] private float viewFps = 12f;

    [Tooltip("경비실 안에서 곁눈으로 볼 때의 초당 프레임.")]
    [Range(1f, 30f)] [SerializeField] private float idleFps = 6f;

    [Tooltip("플레이어가 모니터에서 이 거리(m) 안에 있고 화면이 시야에 들어올 때만 그린다.")]
    [Min(1f)] [SerializeField] private float wakeRadius = 5f;

    [Header("들여다보기")]
    [Tooltip("모니터를 들여다볼 때 눈과 화면 사이 거리(m).")]
    [Min(0.15f)] [SerializeField] private float viewDistance = 0.32f;

    [Tooltip("들여다보기로 들어가고 나오는 시간(초).")]
    [Min(0f)] [SerializeField] private float viewBlendSeconds = 0.35f;

    [Header("화면 질감")]
    [Tooltip("밝기 증폭. 적외선 조명이 있으니 1 근처로 둔다(2.4는 하얗게 날아갔다).")]
    [Min(0f)] [SerializeField] private float gain = 1.3f;

    [Tooltip("화면 노이즈. 플레이어 카메라의 후처리를 한 번 더 거치므로 작게 둔다(0.1이면 도서관처럼 어두운 채널이 알갱이에 묻힌다).")]
    [Range(0f, 1f)] [SerializeField] private float noise = 0.035f;
    [Range(0f, 1f)] [SerializeField] private float scanlines = 0.22f;
    [SerializeField] private Color tint = new Color(0.80f, 0.95f, 0.84f, 1f);

    [Tooltip("채널을 넘길 때 지직거리는 시간(초).")]
    [Min(0f)] [SerializeField] private float switchStaticSeconds = 0.3f;

    [Header("소리 — Resources/Cctv/cctv_hum·cctv_switch·cctv_static. 파일이 없으면 조용하다")]
    [Tooltip("모니터 험(3D, 모니터에서 난다). 들여다보면 가까워져 커진다.")]
    [Range(0f, 1f)] [SerializeField] private float humVolume = 0.35f;

    [Tooltip("채널 전환 '틱-지직'.")]
    [Range(0f, 1f)] [SerializeField] private float switchVolume = 0.6f;

    [Tooltip("신호가 약해질 때(Signal < 1) 섞이는 지직 루프의 최대 볼륨.")]
    [Range(0f, 1f)] [SerializeField] private float staticVolume = 0.5f;

    public IReadOnlyList<Channel> Channels { get { return channels; } }
    public string MonitorPath { get { return monitorPath; } }
    public Vector3 ScreenLocalCenter { get { return screenLocalCenter; } }
    public Vector2 ScreenSize { get { return screenSize; } }
    public Vector2Int Resolution { get { return resolution; } }
    public float ViewFps { get { return viewFps; } }
    public float IdleFps { get { return idleFps; } }
    public float WakeRadius { get { return wakeRadius; } }
    public float ViewDistance { get { return viewDistance; } }
    public float ViewBlendSeconds { get { return viewBlendSeconds; } }
    public float Gain { get { return gain; } }
    public float Noise { get { return noise; } }
    public float Scanlines { get { return scanlines; } }
    public Color Tint { get { return tint; } }
    public float SwitchStaticSeconds { get { return switchStaticSeconds; } }
    public float HumVolume { get { return humVolume; } }
    public float SwitchVolume { get { return switchVolume; } }
    public float StaticVolume { get { return staticVolume; } }

    /// <summary>에디터 전용 — 인스펙터 버튼이 채널을 고칠 때 쓴다.</summary>
    public List<Channel> EditableChannels { get { return channels; } }

    /// <summary><c>Resources/CctvConfig</c>를 읽는다. 없으면 기본값으로 만든 임시 인스턴스를 준다(저장되지 않는다).</summary>
    public static CctvConfigSO Load()
    {
        CctvConfigSO found = Resources.Load<CctvConfigSO>(ResourceName);
        if (found != null && found.channels != null && found.channels.Count > 0)
        {
            return found;
        }

        CctvConfigSO made = CreateInstance<CctvConfigSO>();
        made.name = ResourceName + " (기본값)";
        made.FillDefaults();
        return made;
    }

    /// <summary>
    /// 기본 채널 5개. 2026-09-30 <c>PlayScene</c>에서 씬 뷰로 보고 고른 자리다.
    /// 순서는 기획서의 채널 표를 따른다 — 1 복도 · 2 교실 · 3 과학실 · 4 화장실 · 5 도서관.
    /// </summary>
    public void FillDefaults()
    {
        channels = new List<Channel>
        {
            Make("복도",   new Vector3(14.2f, 4.8f, 42.9f), new Vector3(38.0f, 1.6f, 41.2f)),
            Make("교실",   new Vector3(49.2f, 4.9f, 34.2f), new Vector3(40.0f, 1.4f, 37.6f)),
            Make("과학실", new Vector3(44.7f, 4.8f, 43.1f), new Vector3(53.5f, 1.5f, 41.8f)),
            Make("화장실", new Vector3(-0.8f, 4.6f, 35.3f), new Vector3(5.0f, 1.3f, 33.2f), 1.2f), // 방이 좁아 벽이 가까우니 적외선을 약하게
            Make("도서관", new Vector3(3.6f, 4.9f, 55.3f),  new Vector3(12.5f, 1.3f, 44.5f)),
        };
    }

    private static Channel Make(string label, Vector3 position, Vector3 lookAt, float irIntensity = 2.5f)
    {
        Channel c = new Channel();
        c.irIntensity = irIntensity;
        c.label = label;
        c.position = position;
        c.lookAt = lookAt;
        return c;
    }
}

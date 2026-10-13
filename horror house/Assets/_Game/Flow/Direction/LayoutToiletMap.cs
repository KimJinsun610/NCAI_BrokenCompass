using NightDuty;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 104차 배치 지도 — 화장실(사용자: 「다음은 화장실. 변기, 핏물, 거울로 부탁해」).
/// 화장실 배치 구간 0~4: 벽에 거울(Toilet_MirrorA 사본 — 원래 거울 높이 언저리) <see cref="Mirrors"/> · 천장에 거꾸로 붙은 변기(ToiletA 사본) <see cref="Toilets"/> · 핏물(천장 핏방울 + 바닥 웅덩이) <see cref="Drips"/>.
/// 거울은 T-3(거울 — 의도된 헷갈림) 점검과 겹쳐 보일 수 있다(사용자 의도). 방이 좁아(8×4m · 북쪽 절반이 칸) 천장 간격 1.6m. 규칙은 <see cref="LayoutRoomMap"/>.
/// </summary>
[DisallowMultipleComponent]
public sealed class LayoutToiletMap : LayoutRoomMap
{
    public static readonly int[] Mirrors = { 0, 1, 2, 3, 5 };
    public static readonly int[] Toilets = { 0, 0, 1, 2, 3 };
    public static readonly int[] Drips = { 0, 1, 2, 3, 4 };

    public const string MirrorPath = "LayoutProps/wall_mirror";
    public const string ToiletPath = "LayoutProps/ceiling_toilet";

    protected override float CeilingSpacing
    {
        get { return 1.4f; }
    }

    protected override float CeilingWallGap
    {
        get { return 0.5f; }
    }

    protected override float ClockAvoid
    {
        get { return 0.9f; }   // 좁은 방 — 문간·소화기·점검 표식과 0.9m만
    }

    // 거울: 원래 거울(가운데 바닥 위 1.5m · 0.55 × 0.8m) 언저리.
    protected override float ClockMin
    {
        get { return 1.3f; }
    }

    protected override float ClockMax
    {
        get { return 1.8f; }
    }

    protected override float ClockHalf
    {
        get { return 0.32f; }
    }

    protected override string PathFor(Kind kind)
    {
        if (kind == Kind.Clock) return MirrorPath;
        if (kind == Kind.Desk) return ToiletPath;
        return base.PathFor(kind);
    }

    /// <summary>검수용 — 0~4로 구간을 강제(−1이면 실제 연출 구간).</summary>
    public static int DebugBand = -1;

    public static LayoutToiletMap Active { get; private set; }

    protected override SpaceId BoxSpace
    {
        get { return SpaceId.Toilet; }
    }

    protected override SpaceId BandSpace
    {
        get { return SpaceId.Toilet; }
    }

    protected override int ForcedBand
    {
        get { return DebugBand; }
    }

    protected override string Label
    {
        get { return "배치 지도 화장실 · "; }
    }

    protected override int Seed
    {
        get { return 4409; }
    }

    protected override int[] CountsFor(Kind kind)
    {
        switch (kind)
        {
            case Kind.Clock: return Mirrors;
            case Kind.Desk: return Toilets;
            case Kind.Drip: return Drips;
            default: return null;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Install()
    {
        Active = null;
        DebugBand = -1;
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
        if (!FlowAutoInstall.IsDutyScene(scene) || FlowAutoInstall.Exists<LayoutToiletMap>(scene)) return;
        FlowAutoInstall.CreateHost<LayoutToiletMap>(scene, "LayoutToiletMap (auto)");
    }

    protected override void OnEnable()
    {
        Active = this;
        base.OnEnable();
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        if (Active == this) Active = null;
    }
}

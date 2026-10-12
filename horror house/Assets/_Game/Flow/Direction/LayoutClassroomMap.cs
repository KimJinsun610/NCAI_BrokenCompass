using NightDuty;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 104차 배치 지도 — 교실(사용자: 「교실도 복도랑 같이 · 변화는 천장에 책상과 의자, 벽시계 복제, 칠판 복제, 핏물 정도 · 교실은 1-3만」).
/// 교실(1-3) 배치 구간 0~4: 벽시계 <see cref="Clocks"/> · 칠판 <see cref="Boards"/>(BlackboardSmall ×0.8, 책장 위 높이) · 천장 책상 <see cref="Desks"/> · 천장 의자 <see cref="Chairs"/> · 핏물 <see cref="Drips"/>.
/// 규칙은 <see cref="LayoutRoomMap"/>.
/// </summary>
[DisallowMultipleComponent]
public sealed class LayoutClassroomMap : LayoutRoomMap
{
    public static readonly int[] Clocks = { 0, 1, 2, 4, 6 };
    public static readonly int[] Boards = { 0, 0, 1, 2, 3 };
    public static readonly int[] Desks = { 0, 0, 1, 2, 4 };
    public static readonly int[] Chairs = { 0, 1, 2, 3, 5 };
    public static readonly int[] Drips = { 0, 1, 2, 3, 5 };

    /// <summary>검수용 — 0~4로 구간을 강제(−1이면 실제 연출 구간).</summary>
    public static int DebugBand = -1;

    public static LayoutClassroomMap Active { get; private set; }

    protected override SpaceId BoxSpace
    {
        get { return SpaceId.Classroom_1_3; }
    }

    protected override SpaceId BandSpace
    {
        get { return SpaceId.Classroom; }
    }

    protected override int ForcedBand
    {
        get { return DebugBand; }
    }

    protected override string Label
    {
        get { return "배치 지도 교실 · "; }
    }

    protected override int Seed
    {
        get { return 1043; }
    }

    protected override int[] CountsFor(Kind kind)
    {
        switch (kind)
        {
            case Kind.Clock: return Clocks;
            case Kind.Board: return Boards;
            case Kind.Desk: return Desks;
            case Kind.Chair: return Chairs;
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
        if (!FlowAutoInstall.IsDutyScene(scene) || FlowAutoInstall.Exists<LayoutClassroomMap>(scene)) return;
        FlowAutoInstall.CreateHost<LayoutClassroomMap>(scene, "LayoutClassroomMap (auto)");
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

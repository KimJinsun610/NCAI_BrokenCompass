using NightDuty;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 104차 배치 지도 — 도서관(사용자: 「마찬가지 핏물, 책 더미들, 그리고 몇몇 책장, 의자, 책상들이 천장에 배치되는 걸로」).
/// 도서관 배치 구간 0~4: 핏물(천장 핏방울 + 바닥 웅덩이) <see cref="Drips"/> · 바닥 책 더미 <see cref="Books"/> · 천장에 거꾸로 붙은 열람 의자 <see cref="Chairs"/> · 열람 탁자 <see cref="Tables"/> · 책장(×0.7) <see cref="Shelves"/>.
/// 책장·탁자·의자는 도서관 것(BookShelvingDouble_booksB · LibraryTable · LibraryChair 사본). 규칙은 <see cref="LayoutRoomMap"/>.
/// </summary>
[DisallowMultipleComponent]
public sealed class LayoutLibraryMap : LayoutRoomMap
{
    public static readonly int[] Drips = { 0, 1, 2, 3, 5 };
    public static readonly int[] Books = { 0, 1, 3, 5, 8 };
    public static readonly int[] Chairs = { 0, 1, 2, 3, 4 };
    public static readonly int[] Tables = { 0, 0, 1, 2, 3 };
    public static readonly int[] Shelves = { 0, 0, 1, 1, 2 };

    public const string TablePath = "LayoutProps/ceiling_libtable";
    public const string LibChairPath = "LayoutProps/ceiling_libchair";

    /// <summary>도서관 동쪽 벽(x 약 12.5~13 — PlayScene 실측) 안쪽만 — 도서관 상자(x 2~14.4)는 그 너머 복도 띠까지 덮는다.</summary>
    public const float EastWallX = 12.6f;

    protected override bool InRoom(Vector3 p)
    {
        return p.x < EastWallX;
    }

    protected override string PathFor(Kind kind)
    {
        if (kind == Kind.Desk) return TablePath;
        if (kind == Kind.Chair) return LibChairPath;
        return base.PathFor(kind);
    }

    /// <summary>검수용 — 0~4로 구간을 강제(−1이면 실제 연출 구간).</summary>
    public static int DebugBand = -1;

    public static LayoutLibraryMap Active { get; private set; }

    protected override SpaceId BoxSpace
    {
        get { return SpaceId.Library; }
    }

    protected override SpaceId BandSpace
    {
        get { return SpaceId.Library; }
    }

    protected override int ForcedBand
    {
        get { return DebugBand; }
    }

    protected override string Label
    {
        get { return "배치 지도 도서관 · "; }
    }

    protected override int Seed
    {
        get { return 3301; }
    }

    protected override int[] CountsFor(Kind kind)
    {
        switch (kind)
        {
            case Kind.Drip: return Drips;
            case Kind.Books: return Books;
            case Kind.Chair: return Chairs;
            case Kind.Desk: return Tables;
            case Kind.Shelf: return Shelves;
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
        if (!FlowAutoInstall.IsDutyScene(scene) || FlowAutoInstall.Exists<LayoutLibraryMap>(scene)) return;
        FlowAutoInstall.CreateHost<LayoutLibraryMap>(scene, "LayoutLibraryMap (auto)");
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

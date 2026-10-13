using NightDuty;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 104차 배치 지도 — 과학실(사용자: 「약품 방울(붉은색), 벽시계로 하자 · 인체 모형은 복제 x · 하지만 Anatomical Human Torso를 천장에 머리가 꽂힌 채 몇 개」).
/// 과학실 배치 구간 0~4: 붉은 약품 방울(천장 방울 + 바닥 웅덩이) <see cref="Drips"/> · 벽시계 <see cref="Clocks"/> · 천장에 머리가 박힌 인체 모형 <see cref="Torsos"/>.
/// 서 있는 인체 모형은 늘리지 않는다(S-1 점검 · S3 수칙 대상과 헷갈리지 않게 — 천장 것은 판정 대상이 아니다). 방이 좁아(12×4m) 천장 간격 1.8m. 규칙은 <see cref="LayoutRoomMap"/>.
/// </summary>
[DisallowMultipleComponent]
public sealed class LayoutScienceMap : LayoutRoomMap
{
    public static readonly int[] Drips = { 0, 1, 2, 3, 4 };
    public static readonly int[] Clocks = { 0, 1, 2, 3, 4 };
    public static readonly int[] Torsos = { 0, 0, 1, 2, 3 };

    protected override float CeilingSpacing
    {
        get { return 1.8f; }
    }

    /// <summary>검수용 — 0~4로 구간을 강제(−1이면 실제 연출 구간).</summary>
    public static int DebugBand = -1;

    public static LayoutScienceMap Active { get; private set; }

    protected override SpaceId BoxSpace
    {
        get { return SpaceId.ScienceRoom; }
    }

    protected override SpaceId BandSpace
    {
        get { return SpaceId.ScienceRoom; }
    }

    protected override int ForcedBand
    {
        get { return DebugBand; }
    }

    protected override string Label
    {
        get { return "배치 지도 과학실 · "; }
    }

    protected override int Seed
    {
        get { return 2207; }
    }

    protected override int[] CountsFor(Kind kind)
    {
        switch (kind)
        {
            case Kind.Clock: return Clocks;
            case Kind.Drip: return Drips;
            case Kind.Torso: return Torsos;
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
        if (!FlowAutoInstall.IsDutyScene(scene) || FlowAutoInstall.Exists<LayoutScienceMap>(scene)) return;
        FlowAutoInstall.CreateHost<LayoutScienceMap>(scene, "LayoutScienceMap (auto)");
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

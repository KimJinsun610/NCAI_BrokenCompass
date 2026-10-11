using NightDuty;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 79차 — 새 수칙의 판정 대상을 근무 씬 소품에 런타임으로 붙인다(씬 파일은 고치지 않는다 — LFS 잠금 불필요).
/// <list type="bullet">
/// <item>T7 「화장실 거울을 3초 이상 보지 마십시오.」: 화장실 공간 상자 안의 벽 거울(<c>Toilet_Mirror*</c> 콜라이더, 세워진 것만)에
/// <see cref="FinalCues.MirrorTarget"/>. 응시 탐침(<c>GazeProbe</c>)이 카메라 가운데로 맞힌 콜라이더의 표식을 보낸다.</item>
/// <item>L1 역설 「도서관 무너진 가구들을 점검해주십시오.」: 쓰러진 책장(<see cref="FinalCues.L1Shelf"/> 기준점의 소품)에 「점검」 미끼(<see cref="ParadoxLure"/>).</item>
/// </list>
/// 이미 다른 판정 표식이 붙은 소품은 건너뛴다(대표 ID를 빼앗지 않는다).
/// </summary>
[DisallowMultipleComponent]
public sealed class RuleTargetsInstaller : MonoBehaviour
{
    /// <summary>거울 오브젝트 이름 머리.</summary>
    public const string MirrorPrefix = "Toilet_Mirror";

    private bool _done;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Install()
    {
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
        if (!FlowAutoInstall.IsDutyScene(scene) || FlowAutoInstall.Exists<RuleTargetsInstaller>(scene)) return;
        FlowAutoInstall.CreateHost<RuleTargetsInstaller>(scene, "RuleTargetsInstaller (auto)");
    }

    private void Update()
    {
        if (_done) return;
        _done = MarkMirrors(gameObject.scene) >= 0;
        if (_done) MarkLures(gameObject.scene);
        if (_done) enabled = false;
    }

    /// <summary>L1 쓰러진 책장에 「점검」 미끼를 단다. 단 수.</summary>
    public static int MarkLures(Scene scene)
    {
        int n = 0;
        foreach (JudgeTarget t in FindObjectsByType<JudgeTarget>(FindObjectsSortMode.None))
        {
            if (t.gameObject.scene != scene || t.PrimaryId != FinalCues.L1Shelf) continue;
            Transform prop = t.transform.parent != null ? t.transform.parent : t.transform;
            if (ParadoxLure.Attach(prop.gameObject, "L1", 0f) != null) n++;
        }

        return n;
    }

    /// <summary>화장실 벽 거울에 T7 표식을 붙인다. 붙인 수(이미 붙어 있던 것 포함). 공간 상자를 아직 못 찾으면 −1.</summary>
    public static int MarkMirrors(Scene scene)
    {
        SpaceZones zones = FindFirstObjectByType<SpaceZones>();
        Bounds box;
        if (zones == null || !zones.TryGetSpaceBox(SpaceId.Toilet, out box)) return -1;

        int n = 0;
        foreach (Collider c in FindObjectsByType<Collider>(FindObjectsSortMode.None))
        {
            if (c.gameObject.scene != scene || c.isTrigger || !c.name.StartsWith(MirrorPrefix)) continue;
            Bounds b = c.bounds;
            if (b.size.y < 0.3f) continue;   // 바닥에 눕힌 것은 거울로 보지 않는다
            Vector3 p = b.center;
            if (p.x < box.min.x || p.x > box.max.x || p.z < box.min.z || p.z > box.max.z) continue;

            JudgeTarget existing = c.GetComponentInParent<JudgeTarget>();
            if (existing != null)
            {
                if (existing.PrimaryId == FinalCues.MirrorTarget) n++;
                continue;
            }

            c.gameObject.AddComponent<JudgeTarget>().SetIds(FinalCues.MirrorTarget);
            n++;
        }

        return n;
    }
}

using NightDuty;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 81차 발소리(H3) 위반 연출(민: 「발소리 놀람 연출은 앞으로 달려가기만 할거야. 검은 사람으로」 —
/// 80차 「방 안에 안 들어가면, 플레이어가 바라보는 방향으로 빠르게 달려가면서 사라지는」).
/// H3(<c>TakeShelterJudge</c>)이 위반으로 정산되면, 플레이어 옆 조금 앞(시야 가장자리)에 검은 사람(<c>mob.blackman.glimpse</c> — 달리기 걸음이 있는 대역)이 나타나
/// 플레이어가 바라보는 쪽으로 기괴하게 빠른 걸음(<see cref="StandInExit.RunTo"/>)으로 달려가다 트인 거리 끝에서 사라진다.
/// 덮치기·넘어짐은 없다(다른 넷의 <see cref="ViolationScare"/>와 다르다). 판정과 무관. 근무 씬에 자동으로 선다.
/// </summary>
[DisallowMultipleComponent]
public sealed class FootstepsDash : MonoBehaviour
{
    /// <summary>대역 ID.</summary>
    public const string StandInId = "mob.blackman.glimpse";

    /// <summary>나타나는 자리 — 플레이어 앞(m).</summary>
    public const float AppearAhead = 1.1f;

    /// <summary>나타나는 자리 — 플레이어 옆(m).</summary>
    public const float AppearSide = 0.75f;

    /// <summary>가장 멀리 달려가는 거리(m).</summary>
    public const float MaxRun = 9f;

    /// <summary>이보다 트인 거리가 짧으면 달리지 않는다(m) — 벽에 붙어 서 있을 때.</summary>
    public const float MinRun = 2.5f;

    /// <summary>지금 근무 씬의 것. 없으면 null.</summary>
    public static FootstepsDash Active { get; private set; }

    /// <summary>마지막으로 달려간 대역(시험·디버그).</summary>
    public GameObject Last { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Install()
    {
        Active = null;
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
        if (!FlowAutoInstall.IsDutyScene(scene) || FlowAutoInstall.Exists<FootstepsDash>(scene)) return;
        FlowAutoInstall.CreateHost<FootstepsDash>(scene, "FootstepsDash (auto)");
    }

    private void OnEnable()
    {
        Active = this;
        EventBus.FinalRuleSettled += OnSettled;
    }

    private void OnDisable()
    {
        EventBus.FinalRuleSettled -= OnSettled;
        if (Active == this) Active = null;
    }

    private void OnSettled(FinalRuleResult result)
    {
        if (result.Outcome != FinalOutcome.Violated || result.RuleId != "H3") return;
        if (!NightRun.IsNightActive || NightRun.IsCaptured) return;
        Dash();
    }

    /// <summary>검은 사람이 플레이어 옆에서 바라보는 쪽으로 달려가 사라진다. 세우지 못하면 false. 디버그가 부른다.</summary>
    public bool Dash()
    {
        Camera cam = Camera.main;
        if (cam == null) return false;
        Vector3 fwd = cam.transform.forward;
        fwd.y = 0f;
        if (fwd.sqrMagnitude < 1e-4f) return false;
        fwd.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, fwd);
        Vector3 eye = cam.transform.position;

        // 옆은 더 트인 쪽(벽 속에서 나오지 않게).
        float r = Clear(eye + Vector3.down * 0.7f, right, AppearSide + 0.4f);
        float l = Clear(eye + Vector3.down * 0.7f, -right, AppearSide + 0.4f);
        Vector3 side = r >= l ? right : -right;
        float sideDist = Mathf.Min(AppearSide, Mathf.Max(r, l) - 0.3f);
        if (sideDist < 0.2f) sideDist = 0f;

        Vector3 start = DirectionStage.FloorBelow(eye + fwd * AppearAhead + side * sideDist);
        float run = Mathf.Min(MaxRun, Clear(start + Vector3.up * 0.9f, fwd, MaxRun + 0.5f) - 0.5f);
        if (run < MinRun) return false;

        GameObject go = StandInFactory.Create(StandInId, start, Quaternion.LookRotation(fwd, Vector3.up), string.Empty);
        if (go == null) return false;
        go.name = "발소리 검은 사람";
        foreach (Collider c in go.GetComponentsInChildren<Collider>(true)) c.enabled = false;
        StandInExit exit = go.GetComponent<StandInExit>();
        if (exit == null) exit = go.AddComponent<StandInExit>();
        Vector3 target = start + fwd * run;
        Transform t = go.transform;
        exit.RunTo(target, run / StandInExit.Speed + 0.6f, () => t == null || (t.position - target).sqrMagnitude < 0.09f);
        Last = go;
        if (DirectionStage.Verbose) Debug.Log("[발소리] H3 위반 — 검은 사람이 앞으로 달려감 " + run.ToString("F1") + "m");
        return true;
    }

    private static float Clear(Vector3 from, Vector3 dir, float max)
    {
        float dist = max;
        foreach (RaycastHit h in Physics.SphereCastAll(from, 0.25f, dir, max, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (h.collider.GetComponentInParent<CharacterController>() != null) continue;
            if (h.distance <= 0f) continue;
            dist = Mathf.Min(dist, h.distance);
        }

        return dist;
    }
}

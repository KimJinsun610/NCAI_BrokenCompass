using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 103차(민: 「도서관 노란 얼굴은 플레이어 쪽으로 천천히 걸어오게 — 네비메쉬로 장애물 잘 피하도록」) — 연출 몹이 걸을 네비메쉬를 런타임에 굽는다.
/// <list type="bullet">
/// <item>씬은 고치지 않는다(굽기 결과는 씬에 저장하지 않는다). 몹이 필요해진 순간 그 구역(<see cref="Prepare"/>의 상자)만 비동기로 굽는다 — 한 번 구운 구역은 그 씬 동안 다시 쓴다.</item>
/// <item>재료: 물리 콜라이더(<see cref="NavMeshCollectGeometry.PhysicsColliders"/>). 트리거 · 리지드바디가 달린 것(플레이어 · 굴러가는 소품) · 여는 방문 문짝(<see cref="DoorHandle"/>, 기획 문)은 뺀다 — 문은 열려 있다고 보고 문간을 길로 둔다.</item>
/// <item>103차(민: 「노란 얼굴이 벽을 뚫으면서 쫓아와」): 씬에는 옛 네비메쉬(기본 요원 Humanoid, ID 0)가 구워져 있고 벽·책장을 무시한 곳이 있다.
/// 같은 요원 ID로 겹쳐 올리면 요원이 옛 메쉬를 타고 벽을 지나간다 → 이 굽기는 <b>제 요원 종류</b>(<see cref="NavMesh.CreateSettings"/>, <see cref="AgentTypeId"/>)로 만들고,
/// 요원·찾기(<see cref="Snap"/>)도 그 종류만 쓴다. 종류는 씬을 내릴 때·끝낼 때 지운다(<see cref="NavMesh.RemoveSettings"/>).</item>
/// <item>요원 반지름은 <see cref="AgentRadius"/> — 책장 사이 통로(약 0.8m)를 지나갈 수 있게.</item>
/// </list>
/// 판정과 무관.
/// </summary>
public static class RuntimeNav
{
    /// <summary>요원 반지름(m).</summary>
    public const float AgentRadius = 0.28f;

    /// <summary>요원 키(m).</summary>
    public const float AgentHeight = 1.85f;

    /// <summary>넘는 턱(m) — 문지방.</summary>
    public const float AgentClimb = 0.35f;

    private const int NoType = -1;

    private static NavMeshData s_data;
    private static NavMeshDataInstance s_instance;
    private static AsyncOperation s_job;
    private static Bounds s_bounds;
    private static bool s_has;
    private static int s_agentType = NoType;

    /// <summary>구운 네비메쉬가 쓸 수 있는 상태인지(굽는 중이면 false).</summary>
    public static bool Ready
    {
        get { return s_has && s_agentType != NoType && (s_job == null || s_job.isDone); }
    }

    /// <summary>이 굽기 전용 요원 종류 ID(없으면 -1). 요원(<see cref="NavMeshAgent.agentTypeID"/>)에 넣는다.</summary>
    public static int AgentTypeId
    {
        get { return s_agentType; }
    }

    /// <summary>이 굽기만 보는 찾기 거름.</summary>
    public static NavMeshQueryFilter Filter
    {
        get { return new NavMeshQueryFilter { agentTypeID = s_agentType, areaMask = NavMesh.AllAreas }; }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        RemoveAgentType();   // 도메인 재로드 없이 다시 플레이한 경우 지난번 종류를 치운다
        s_data = null;
        s_instance = default(NavMeshDataInstance);
        s_job = null;
        s_has = false;
        UnityEngine.SceneManagement.SceneManager.sceneUnloaded -= OnUnloaded;
        UnityEngine.SceneManagement.SceneManager.sceneUnloaded += OnUnloaded;
        Application.quitting -= Clear;
        Application.quitting += Clear;
    }

    private static void OnUnloaded(UnityEngine.SceneManagement.Scene scene)
    {
        Clear();
    }

    /// <summary>구운 것과 전용 요원 종류를 걷는다.</summary>
    public static void Clear()
    {
        DropData();
        RemoveAgentType();
    }

    private static void DropData()
    {
        if (s_instance.valid) s_instance.Remove();
        s_instance = default(NavMeshDataInstance);
        s_data = null;
        s_job = null;
        s_has = false;
    }

    private static void RemoveAgentType()
    {
        if (s_agentType == NoType) return;
        NavMesh.RemoveSettings(s_agentType);
        s_agentType = NoType;
    }

    private static NavMeshBuildSettings Settings()
    {
        NavMeshBuildSettings settings;
        if (s_agentType == NoType)
        {
            settings = NavMesh.CreateSettings();
            s_agentType = settings.agentTypeID;
        }
        else
        {
            settings = NavMesh.GetSettingsByID(s_agentType);
        }

        settings.agentRadius = AgentRadius;
        settings.agentHeight = AgentHeight;
        settings.agentClimb = AgentClimb;
        settings.agentSlope = 40f;
        return settings;
    }

    /// <summary>
    /// 그 상자를 덮는 네비메쉬를 준비한다(이미 덮고 있으면 아무것도 안 함). 굽기는 비동기 — <see cref="Ready"/>를 보고 쓴다.
    /// <paramref name="ignore"/> 아래의 콜라이더(걸을 몹 자신)는 재료에서 뺀다.
    /// </summary>
    public static void Prepare(Bounds area, Transform ignore)
    {
        if (s_has && s_bounds.Contains(area.min) && s_bounds.Contains(area.max)) return;
        if (s_has)
        {
            area.Encapsulate(s_bounds);   // 넓혀서 다시 굽는다(요원 종류는 그대로 둔다)
            DropData();
        }

        NavMeshBuildSettings settings = Settings();
        List<NavMeshBuildSource> sources = new List<NavMeshBuildSource>();
        NavMeshBuilder.CollectSources(area, Physics.DefaultRaycastLayers, NavMeshCollectGeometry.PhysicsColliders, 0, new List<NavMeshBuildMarkup>(), sources);
        for (int i = sources.Count - 1; i >= 0; i--)
        {
            if (Skip(sources[i].component, ignore)) sources.RemoveAt(i);
        }

        s_data = new NavMeshData(settings.agentTypeID);
        s_instance = NavMesh.AddNavMeshData(s_data);
        s_job = NavMeshBuilder.UpdateNavMeshDataAsync(s_data, settings, sources, area);
        s_bounds = area;
        s_has = true;
        if (DirectionStage.Verbose) Debug.Log("[RuntimeNav] 네비메쉬 굽기 시작(요원 종류 " + s_agentType + ") — 재료 " + sources.Count + "개 · 구역 " + area.center.ToString("F1") + " ± " + area.extents.ToString("F1"));
    }

    private static bool Skip(Component c, Transform ignore)
    {
        if (c == null) return false;
        Collider col = c as Collider;
        if (col != null)
        {
            if (col.isTrigger || !col.enabled) return true;
            if (col.attachedRigidbody != null && !col.attachedRigidbody.isKinematic) return true;   // 플레이어 · 굴러가는 소품
        }

        Transform t = c.transform;
        if (ignore != null && (t == ignore || t.IsChildOf(ignore))) return true;
        DoorHandle door = DoorHandle.Of(t);
        return door.IsValid && PlayerInteractor.Classify(door) == DoorPolicySO.Kind.Openable;   // 방문 문짝 — 문간은 길로 둔다(수납가구 · 막힌 문은 장애물 그대로)
    }

    /// <summary>그 점에서 가장 가까운 이 굽기 위의 점(옛 씬 네비메쉬는 보지 않는다). 없으면 false.</summary>
    public static bool Snap(Vector3 point, float maxDistance, out Vector3 snapped)
    {
        NavMeshHit hit;
        if (Ready && NavMesh.SamplePosition(point, out hit, maxDistance, Filter))
        {
            snapped = hit.position;
            return true;
        }

        snapped = point;
        return false;
    }
}

using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 씬 전환 진입점.
/// 기본은 로딩 씬을 거친다: GoTo(목적지) → 로딩 씬 로드 → LoadingController가 목적지를 꺼내 비동기 로드.
/// Play → Result처럼 가벼운 씬은 useLoading = false로 바로 이동한다.
/// </summary>
public static class SceneFlow
{
    private const string ConfigResourcePath = "SceneFlowConfig";

    private static SceneFlowConfig config;
    private static string pendingScenePath;
    private static bool isTransitioning;

    public static SceneFlowConfig Config
    {
        get
        {
            if (config == null)
            {
                config = Resources.Load<SceneFlowConfig>(ConfigResourcePath);
                if (config == null)
                {
                    Debug.LogError($"[SceneFlow] Resources/{ConfigResourcePath} 에셋을 찾을 수 없습니다.");
                }
            }
            return config;
        }
    }

    /// <summary>
    /// target 씬으로 이동한다.
    /// useLoading = true(기본)면 로딩 씬을 거치고, false면 바로 로드한다.
    /// </summary>
    public static void GoTo(GameScene target, bool useLoading = true)
    {
        // 버튼 연타 등으로 전환이 중복 요청되는 것 방지 (다음 씬이 로드되면 자동 해제)
        if (isTransitioning) return;
        if (Config == null) return;

        string targetPath = Config.GetPath(target);
        if (!IsInBuild(targetPath, target)) return;

        string loadPath = targetPath;
        if (useLoading)
        {
            loadPath = Config.GetPath(GameScene.Loading);
            if (!IsInBuild(loadPath, GameScene.Loading)) return;
            pendingScenePath = targetPath;
        }

        isTransitioning = true;

        // 일시정지(timeScale 0) 상태에서 전환해도 다음 씬이 멈춘 채 시작하지 않도록 복구
        Time.timeScale = 1f;

        SceneManager.LoadScene(loadPath);
    }

    /// <summary>
    /// 로딩 씬이 이동할 목적지를 꺼낸다. 한 번 꺼내면 비워진다.
    /// 로딩 씬을 직접 실행한 경우처럼 요청이 없었다면 null.
    /// </summary>
    public static string ConsumePendingScene()
    {
        string path = pendingScenePath;
        pendingScenePath = null;
        return path;
    }

    private static bool IsInBuild(string path, GameScene scene)
    {
        if (string.IsNullOrEmpty(path))
        {
            Debug.LogError($"[SceneFlow] SceneFlowConfig에 {scene} 씬이 지정되지 않았습니다.");
            return false;
        }
        if (SceneUtility.GetBuildIndexByScenePath(path) < 0)
        {
            Debug.LogError($"[SceneFlow] '{path}' 씬이 Build Settings에 없습니다. File > Build Profiles > Scene List에 추가하세요.");
            return false;
        }
        return true;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        isTransitioning = false;
    }

    // Domain Reload를 끈 상태에서도 이전 플레이 세션의 값이 남지 않도록 초기화
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        config = null;
        pendingScenePath = null;
        isTransitioning = false;

        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }
}

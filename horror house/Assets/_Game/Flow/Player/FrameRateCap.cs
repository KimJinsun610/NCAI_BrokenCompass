using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 104차(사용자: 「빌드 파일에서 프레임 최대를 144로 고정하여 마우스 튕기는 걸 완전히 방지해 줘」): 빌드에서 수직 동기를 끄고 목표 프레임을 <see cref="MaxFps"/>로 묶는다.
/// 빌드는 수직 동기 0 · 목표 프레임 −1(제한 없음)이라 보통 프레임이 수 ms였고, 김진선님 <c>FPController</c> 시점(마우스 이동 × 프레임 시간)이 작은 끊김에도
/// 프레임 시간 비율만큼 크게 꺾였다(<see cref="LookSpikeGuard"/>가 보정). 프레임을 묶으면 보통 프레임 시간이 고르게 길어져 끊김과의 차이가 줄어든다.
/// 에디터는 건드리지 않는다(에디터 재생은 게임 뷰 설정을 따른다). 씬을 읽을 때마다 다시 건다(다른 코드가 바꿔도 되돌린다).
/// </summary>
public static class FrameRateCap
{
    /// <summary>빌드 최대 프레임.</summary>
    public const int MaxFps = 144;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        if (Application.isEditor) return;
        Apply();
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Apply();
    }

    /// <summary>수직 동기를 끄고(목표 프레임이 무시되지 않게) 목표 프레임을 건다.</summary>
    public static void Apply()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = MaxFps;
    }
}

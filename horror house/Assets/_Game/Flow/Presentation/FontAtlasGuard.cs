using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 동적 TMP 폰트의 「Multi Atlas Textures」를 실행 중에 켠다(47차). 43차에 에셋에서 켰지만 김진선님 쪽 병합으로 폰트 에셋이 다시 들어오며
/// 5개가 꺼진 채로 돌아왔다(동적 폰트는 플레이할 때마다 에셋이 바뀌어 각자 커밋에 섞인다) — 5일차 결과창에 「□수대」·「가까□」가 다시 나왔다.
/// 에셋 값에 기대지 않고, 씬이 열릴 때마다 불러온 동적 폰트를 모두 켠다. 이미 켜져 있으면 아무것도 하지 않는다. 판정과 무관.
/// </summary>
public static class FontAtlasGuard
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Install()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void First()
    {
        Apply();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Apply();
    }

    /// <summary>불러온 동적 폰트 중 꺼진 것을 켠다. 켠 수.</summary>
    public static int Apply()
    {
        int n = 0;
        foreach (TMP_FontAsset font in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
        {
            if (font == null || font.atlasPopulationMode == AtlasPopulationMode.Static || font.isMultiAtlasTexturesEnabled) continue;
            font.isMultiAtlasTexturesEnabled = true;
            n++;
        }

        return n;
    }
}

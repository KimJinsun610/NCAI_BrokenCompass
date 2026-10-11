using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 88차(민: 「교실 창가쪽에서 시야를 돌리면 라이팅이 밝아졌다 돌아온다」 → 「달빛을 꺼보고 비교해봐」 → 「이렇게 바꿔줘」).
/// 원인: 씬에 구워진 오클루전 컬링이 <b>그림자 캐스터도 시야 기준으로</b> 걸러낸다(Umbra 그림자 캐스터 컬링).
/// 달빛 <c>Sun</c>(Mixed·IndirectOnly, 실시간 그림자, 고도 8°로 거의 수평)은 동쪽 벽들의 그림자로 실내에 안 들어오는데,
/// 특정 방향을 보면 그 벽들이 캐스터에서 빠져 그림자가 사라지고 창가 벽이 달빛으로 확 밝아졌다.
/// 그림자가 제대로면 달빛은 실내 어디에도 닿지 않는다(실내 11곳 × 12방향, 켬/끔 화면 차이 정확히 0).
/// 그래서 근무 씬(1인칭 조작기가 있는 씬)에서는 달빛 <b>라이트 컴포넌트만</b> 끈다:
/// <list type="bullet">
/// <item>구운 간접광은 라이트맵에 그대로(IndirectOnly) — 실내 모습 그대로.</item>
/// <item>오브젝트는 남겨 둔다 — <c>LightRays</c>·<c>ParticlesAutoColor</c>가 태그 "Sun"으로 방향·색을 읽는다.</item>
/// <item>오클루전 컬링은 켠 채로 두고, 달빛 그림자 맵(4 캐스케이드)도 안 그려 렌더가 가벼워진다
/// (에디터 측정 장당: 오클루전 끔+달빛 5.4ms → 오클루전 켬+달빛 끔 3.5ms, 원래 4.4ms).</item>
/// </list>
/// 건물 밖·옥상에서만 아주 조금 어두워진다(플레이어가 가지 않는 곳). 씬 에셋은 고치지 않는다(런타임).
/// </summary>
public static class MoonlightOff
{
    /// <summary>달빛을 끈 횟수(확인용).</summary>
    public static int Disabled { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        SceneManager.sceneLoaded -= OnLoaded;
        SceneManager.sceneLoaded += OnLoaded;
        Apply();
    }

    static void OnLoaded(Scene scene, LoadSceneMode mode) => Apply();

    /// <summary>근무 씬이면 달빛을 끈다. 껐으면 true.</summary>
    public static bool Apply()
    {
        if (Object.FindFirstObjectByType<FPController>() == null) return false;
        Light sun = RenderSettings.sun;
        if (!ShouldDisable(sun)) return false;
        sun.enabled = false;
        Disabled++;
        return true;
    }

    /// <summary>끌 대상인가: 켜진 실시간(혼합 포함) 방향광이고 카메라에 붙은 라이트(뷰모델 조명)가 아님.</summary>
    public static bool ShouldDisable(Light l)
    {
        if (l == null || !l.enabled) return false;
        if (l.type != LightType.Directional) return false;
        if (l.lightmapBakeType == LightmapBakeType.Baked) return false;
        return l.GetComponentInParent<Camera>() == null;
    }
}

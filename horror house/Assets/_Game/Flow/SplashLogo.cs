using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 게임을 켜고 처음 메인 화면이 열릴 때 한 번, 검은 화면에 Broken Compass(팀) 로고를 보여 준다.
/// <list type="bullet">
/// <item>로고 이미지는 <c>Resources/Logos/logo_brokencompass</c>(<see cref="CompanyLogos.BrokenCompass"/>) — 검은 바탕에 올라가므로 밝은 색 판을 넣는다. 이미지가 없으면 스플래시 없이 지나간다.
/// NCAI·하이테커 로고는 여기 쓰지 않는다(메인 화면 「Thanks for...」 팝업 전용).</item>
/// <item>서서히 나타남 → 머묾 → 서서히 사라짐. 아무 키나 마우스 클릭이면 건너뛴다(<see cref="SkipAfter"/>초 뒤부터).</item>
/// <item>로고가 모두 끝나면 검은 막이 걷히며 메인 화면이 보인다.</item>
/// <item>메인 화면으로 다시 돌아올 때(엔딩·결과창 뒤)는 나오지 않는다 — 프로세스당 한 번.</item>
/// </list>
/// 프리팹·씬은 고치지 않는다 — 이름이 「HUD_Main」으로 시작하는 루트가 있는 씬이 열릴 때 스스로 선다.
/// 메인 화면 자체의 연출(START·QUIT 글자가 나타남)은 막 뒤에서 그대로 진행된다.
/// </summary>
public sealed class SplashLogo : MonoBehaviour
{
    private const string HudPrefix = "HUD_Main";

    /// <summary>시작 전 검은 화면만 보이는 시간(초).</summary>
    public const float BlackLead = 0.4f;

    /// <summary>로고 하나가 나타나는 시간(초).</summary>
    public const float FadeIn = 0.7f;

    /// <summary>로고 하나가 머무는 시간(초).</summary>
    public const float Hold = 1.3f;

    /// <summary>로고 하나가 사라지는 시간(초).</summary>
    public const float FadeOut = 0.6f;

    /// <summary>마지막에 검은 막이 걷히는 시간(초).</summary>
    public const float Reveal = 0.6f;

    /// <summary>이 시간이 지나야 건너뛰기를 받는다(켜자마자 누른 키 무시).</summary>
    public const float SkipAfter = 0.3f;

    /// <summary>로고 폭(기준 해상도 1920×1080 기준 px). 높이는 이미지 비율을 따른다.</summary>
    public const float LogoWidth = 560f;

    private static bool _played;
    private bool _skip;

    // ── 자동 설치 ─────────────────────────────────────────────

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Install()
    {
        _played = false;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InstallForFirstScene()
    {
        TryStart(SceneManager.GetActiveScene());
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TryStart(scene);
    }

    private static void TryStart(Scene scene)
    {
        if (_played || !scene.IsValid() || !scene.isLoaded) return;
        if (!HasMainHud(scene)) return;
        if (CompanyLogos.BrokenCompass == null) return;   // 로고 이미지가 아직 없으면 건너뜀

        _played = true;
        new GameObject("SplashLogo").AddComponent<SplashLogo>();
    }

    private static bool HasMainHud(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name.StartsWith(HudPrefix)) return true;
        }

        return false;
    }

    // ── 동작 ──────────────────────────────────────────────────

    private IEnumerator Start()
    {
        CanvasGroup root = BuildCanvas(out RectTransform rect);
        CanvasGroup logo = BuildLogo(rect);

        yield return Wait(BlackLead);

        if (logo != null) yield return Slide(logo);

        // 막을 걷는다(건너뛰었으면 짧게).
        float reveal = _skip ? 0.25f : Reveal;
        for (float t = 0f; t < reveal; t += Time.unscaledDeltaTime)
        {
            root.alpha = 1f - Mathf.Clamp01(t / reveal);
            yield return null;
        }

        Destroy(root.gameObject);
        Destroy(gameObject);
    }

    private IEnumerator Slide(CanvasGroup group)
    {
        yield return Fade(group, 0f, 1f, FadeIn);
        if (!_skip) yield return Wait(Hold);
        yield return Fade(group, 1f, 0f, _skip ? 0.15f : FadeOut);
    }

    private IEnumerator Fade(CanvasGroup group, float from, float to, float seconds)
    {
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            group.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / seconds));
            if (PollSkip()) break;
            yield return null;
        }

        group.alpha = to;
    }

    private IEnumerator Wait(float seconds)
    {
        for (float t = 0f; t < seconds && !_skip; t += Time.unscaledDeltaTime)
        {
            PollSkip();
            yield return null;
        }
    }

    private bool PollSkip()
    {
        if (!_skip && Time.unscaledTime - _startTime >= SkipAfter && (Input.anyKeyDown || Input.GetMouseButtonDown(0)))
        {
            _skip = true;
        }

        return _skip;
    }

    private float _startTime;

    private void Awake()
    {
        _startTime = Time.unscaledTime;
    }

    // ── 화면 ──────────────────────────────────────────────────

    private static CanvasGroup BuildCanvas(out RectTransform rect)
    {
        GameObject go = new GameObject("SplashLogoCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        Canvas canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32000;   // 모든 HUD 위

        CanvasScaler scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // 검은 바탕(클릭도 막는다 — 메인 버튼이 로고 중에 눌리지 않게).
        GameObject bg = new GameObject("Black", typeof(RectTransform), typeof(Image));
        bg.transform.SetParent(go.transform, false);
        Stretch((RectTransform)bg.transform);
        Image img = bg.GetComponent<Image>();
        img.color = Color.black;
        img.raycastTarget = true;

        rect = (RectTransform)go.transform;
        return go.GetComponent<CanvasGroup>();
    }

    private static CanvasGroup BuildLogo(RectTransform parent)
    {
        Sprite sprite = CompanyLogos.BrokenCompass;
        if (sprite == null) return null;

        CanvasGroup group = NewSlide(parent, "BrokenCompass");
        MakeImage(group.transform, "Logo", sprite, Color.white, new Vector2(LogoWidth, LogoWidth / CompanyLogos.Aspect(sprite)));
        return group;
    }

    private static CanvasGroup NewSlide(RectTransform parent, string name)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup));
        go.transform.SetParent(parent, false);
        Stretch((RectTransform)go.transform);
        CanvasGroup group = go.GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        return group;
    }

    private static void MakeImage(Transform parent, string name, Sprite sprite, Color color, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = size;
        Image image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.preserveAspect = true;
        image.raycastTarget = false;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}

using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 메인 화면(HUD_Main) 왼쪽 아래 「Thanks for...」 버튼 — 누르면 제작지원 팝업([제작지원] · NCAI · 하이테커 · 오른쪽 아래 close).
/// <list type="bullet">
/// <item>버튼은 QUIT 글자가 나타나는 박자를 따라가고, QUIT이 눌릴 수 있게 되면 같이 눌린다(START·QUIT과 같은 규칙).</item>
/// <item>팝업은 반투명 검은 화면 + 로고(<see cref="CompanyLogos"/>). close · Esc로 닫는다. 열려 있는 동안 뒤의 메인 버튼은 눌리지 않는다.</item>
/// <item>글꼴은 QUIT 버튼의 것을 빌린다(한글이 없으면 한글이 있는 불러온 글꼴을 찾아 쓴다).</item>
/// </list>
/// 프리팹·씬은 고치지 않는다 — 이름이 「HUD_Main」으로 시작하는 루트에 스스로 붙는다(<see cref="MainMenuOptions"/>와 같은 방식).
/// </summary>
[DisallowMultipleComponent]
public sealed class MainMenuCredits : MonoBehaviour
{
    private const string HudPrefix = "HUD_Main";
    private const string QuitButtonName = "Btn_Quit";

    /// <summary>버튼 글자.</summary>
    public const string ButtonLabel = "Thanks for...";

    /// <summary>팝업 제목.</summary>
    public const string Title = "[제작지원]";

    /// <summary>닫기 버튼 글자.</summary>
    public const string CloseLabel = "close";

    /// <summary>팝업 바탕의 불투명도(0~1).</summary>
    public const float DimAlpha = 0.85f;

    private const float HighTecherWidth = 190f;   // 0.5배(옛 380)
    private const float LogoGap = 440f;   // 두 로고 가운데 사이 거리(가운데에 가깝게)
    private const float LogoY = 70f;      // 로고 높이(화면 가운데 기준, 위로)
    private const float GlowDiameter = 288f;   // 하이테커 뒤 흐린 흰 원 지름
    private const float GlowAlpha = 0.20f;     // 원 한가운데 불투명도

    private Button _quit;
    private TMP_Text _quitLabel;
    private CanvasGroup _quitGroup;
    private Button _button;
    private CanvasGroup _group;
    private TMP_FontAsset _font;
    private GameObject _popup;
    private bool _released;

    /// <summary>덧붙인 버튼(검수).</summary>
    public Button CreditsButton
    {
        get { return _button; }
    }

    /// <summary>세운 팝업(한 번도 안 열었으면 null, 검수).</summary>
    public GameObject Popup
    {
        get { return _popup; }
    }

    // ── 자동 설치 ─────────────────────────────────────────────

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
        if (!scene.IsValid() || !scene.isLoaded) return;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (!root.name.StartsWith(HudPrefix)) continue;
            if (root.GetComponent<MainMenuCredits>() != null) continue;
            root.AddComponent<MainMenuCredits>().Build();
        }
    }

    // ── 버튼 ─────────────────────────────────────────────────

    private void Build()
    {
        _quit = FindButton(QuitButtonName);
        if (_quit == null)
        {
            Debug.LogWarning("[MainMenuCredits] 메인 화면에서 Btn_Quit을 찾지 못했습니다 — 제작지원 버튼을 붙이지 않습니다.");
            return;
        }

        _quitLabel = _quit.GetComponentInChildren<TMP_Text>(true);
        _quitGroup = _quit.GetComponent<CanvasGroup>();
        _font = PickFont(_quitLabel);

        _button = MakeButton((RectTransform)transform, "Btn_Credits", ButtonLabel, 30f,
            new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(48f, 36f), new Vector2(360f, 56f),
            TextAlignmentOptions.MidlineLeft, Open);
        _group = _button.gameObject.GetComponent<CanvasGroup>();
        _released = _quit.interactable;
        _button.interactable = _released;
    }

    private Button FindButton(string buttonName)
    {
        foreach (Button b in GetComponentsInChildren<Button>(true))
        {
            if (b.name == buttonName) return b;
        }

        return null;
    }

    private void LateUpdate()
    {
        if (_button == null || _quit == null) return;

        // QUIT 글자가 나타나는 박자 · 다른 화면(OPTIONS)이 QUIT을 감출 때(CanvasGroup) 같이 감춘다.
        float a = _quitLabel != null ? _quitLabel.alpha : 1f;
        if (_quitGroup != null) a *= _quitGroup.alpha;
        _group.alpha = a;

        if (!_released && _quit.interactable)
        {
            _released = true;
            _button.interactable = true;
        }

        if (_popup != null && _popup.activeSelf && Input.GetKeyDown(KeyCode.Escape)) Close();
    }

    // ── 팝업 ─────────────────────────────────────────────────

    /// <summary>팝업을 연다(처음이면 세운다).</summary>
    public void Open()
    {
        if (_popup == null) _popup = BuildPopup();
        _popup.SetActive(true);
        if (UnityEngine.EventSystems.EventSystem.current != null) UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
    }

    /// <summary>팝업을 닫는다.</summary>
    public void Close()
    {
        if (_popup != null) _popup.SetActive(false);
        if (UnityEngine.EventSystems.EventSystem.current != null) UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
    }

    private GameObject BuildPopup()
    {
        // 메인 화면 캔버스와 따로 — 자기 캔버스가 위에 그려져 뒤의 버튼 클릭을 막는다.
        // 이름이 「HUD_Main」으로 시작하면 메인 화면 자동 설치에 걸리므로 다르게 짓는다.
        GameObject go = new GameObject("MainMenuCreditsPopup", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 300;

        CanvasScaler scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform root = (RectTransform)go.transform;

        // 반투명 검은 바탕(뒤 클릭을 막는다).
        GameObject dim = new GameObject("Dim", typeof(RectTransform), typeof(Image));
        dim.transform.SetParent(root, false);
        Stretch((RectTransform)dim.transform);
        Image dimImage = dim.GetComponent<Image>();
        dimImage.color = new Color(0f, 0f, 0f, DimAlpha);
        dimImage.raycastTarget = true;

        // 제목.
        TMP_Text title = MakeText(root, "Title", Title, 64f, TextAlignmentOptions.Center);
        RectTransform titleRt = title.rectTransform;
        titleRt.anchorMin = titleRt.anchorMax = titleRt.pivot = new Vector2(0.5f, 1f);
        titleRt.anchoredPosition = new Vector2(0f, -150f);
        titleRt.sizeDelta = new Vector2(900f, 100f);

        // 하이테커 — 높이를 기준으로 삼는다(NCAI도 같은 높이).
        Sprite tecer = CompanyLogos.HighTecher;
        float logoHeight = tecer != null ? HighTecherWidth / CompanyLogos.Aspect(tecer) : 150f;

        // NCAI(검은 바탕이라 흰 글자 판) — 하이테커와 같은 높이.
        Sprite ncai = CompanyLogos.NcaiOnDark;
        if (ncai != null)
        {
            Vector2 size = new Vector2(HighTecherWidth, HighTecherWidth / CompanyLogos.Aspect(ncai));
            MakeImage(root, "Ncai", ncai, Color.white, size, new Vector2(-LogoGap * 0.5f, LogoY));
        }

        // 하이테커(짙은 글자라 뒤에 스프레이처럼 경계가 풀어지는 반투명 흰 원).
        if (tecer != null)
        {
            Vector2 at = new Vector2(LogoGap * 0.5f, LogoY);
            MakeImage(root, "HighTecherGlow", SoftCircle(), new Color(1f, 1f, 1f, GlowAlpha),
                new Vector2(GlowDiameter, GlowDiameter), at);
            MakeImage(root, "HighTecher", tecer, Color.white, new Vector2(HighTecherWidth, logoHeight), at);
        }

        // 오른쪽 아래 close.
        MakeButton(root, "Btn_Close", CloseLabel, 36f,
            new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-64f, 48f), new Vector2(260f, 64f),
            TextAlignmentOptions.MidlineRight, Close);

        return go;
    }

    // ── 부품 ─────────────────────────────────────────────────

    private Button MakeButton(RectTransform parent, string objectName, string text, float fontSize,
        Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size, TextAlignmentOptions align, UnityEngine.Events.UnityAction onClick)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform), typeof(CanvasGroup));
        go.transform.SetParent(parent, false);
        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = position;
        rt.sizeDelta = size;

        TMP_Text label = MakeText(rt, "Text", text, fontSize, align);
        Stretch(label.rectTransform);
        label.raycastTarget = true;   // 글자 자리 전체가 누르는 자리

        Button button = go.AddComponent<Button>();
        button.targetGraphic = label;
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(1f, 1f, 1f, 0.65f);
        colors.highlightedColor = Color.white;
        colors.selectedColor = new Color(1f, 1f, 1f, 0.65f);
        colors.pressedColor = new Color(1f, 1f, 1f, 0.8f);
        colors.disabledColor = new Color(1f, 1f, 1f, 0.3f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.1f;
        button.colors = colors;
        button.onClick.AddListener(onClick);
        return button;
    }

    private TMP_Text MakeText(RectTransform parent, string objectName, string text, float fontSize, TextAlignmentOptions align)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI t = go.GetComponent<TextMeshProUGUI>();
        if (_font != null) t.font = _font;
        t.text = text;
        t.fontSize = fontSize;
        t.alignment = align;
        t.color = Color.white;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.raycastTarget = false;
        return t;
    }

    private static void MakeImage(RectTransform parent, string objectName, Sprite sprite, Color color, Vector2 size, Vector2 position)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
        Image image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.preserveAspect = true;
        image.raycastTarget = false;
    }

    private static Sprite _softCircle;

    /// <summary>가장자리가 부드럽게 풀어지는 흰 원(런타임 생성, 한 번만).</summary>
    private static Sprite SoftCircle()
    {
        if (_softCircle != null) return _softCircle;

        const int n = 256;
        Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        Color32[] px = new Color32[n * n];
        float c = (n - 1) * 0.5f;
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;   // 0 가운데 → 1 가장자리
                float a = Mathf.Clamp01((1f - d) / 0.6f);                          // 바깥 60%에서 서서히 사라짐
                a = a * a * (3f - 2f * a);                                         // smoothstep
                px[y * n + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
        }

        tex.SetPixels32(px);
        tex.Apply(false, true);
        _softCircle = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        return _softCircle;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    /// <summary>QUIT 글자의 글꼴을 쓰되 한글이 없으면 한글이 있는 불러온 글꼴을 찾는다.</summary>
    private static TMP_FontAsset PickFont(TMP_Text quitLabel)
    {
        TMP_FontAsset font = quitLabel != null ? quitLabel.font : null;
        if (font != null && font.HasCharacter('제', true, true)) return font;

        foreach (TMP_FontAsset f in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
        {
            if (f != null && f.HasCharacter('제', true, true)) return f;
        }

        return font;
    }
}

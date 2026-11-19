using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 메인 화면(HUD_Main) 오른쪽 아래 「언어」 버튼 — 2026-11-19, 김진선님: 「영어 버전을 만들려는데 우선 메인화면 우 하단에 언어」.
/// <list type="bullet">
/// <item>왼쪽 아래 「Thanks for...」(<see cref="MainMenuCredits"/>)와 같은 방식 — QUIT 글자가 나타나는 박자를 따라가고, QUIT이 눌릴 수 있게 되면 같이 눌린다.</item>
/// <item>누르면 반투명 검은 화면에 「한국어」·「English」 두 칸과 오른쪽 아래 close. 고른 칸은 밑줄·흰색. Esc·close로 닫는다.</item>
/// <item>선택값은 <see cref="GameLanguage"/>(PlayerPrefs) — 설정 화면(OPTIONS)의 LANGUAGE 줄과 같은 값이다. 글을 번역해 바꾸는 일은 아직 없다.</item>
/// </list>
/// 프리팹·씬은 고치지 않는다 — 이름이 「HUD_Main」으로 시작하는 루트에 스스로 붙는다.
/// </summary>
[DisallowMultipleComponent]
public sealed class MainMenuLanguage : MonoBehaviour
{
    private const string HudPrefix = "HUD_Main";
    private const string QuitButtonName = "Btn_Quit";

    /// <summary>팝업 바탕의 불투명도(0~1).</summary>
    public const float DimAlpha = 0.85f;

    private Button _quit;
    private TMP_Text _quitLabel;
    private CanvasGroup _quitGroup;
    private Button _button;
    private TMP_Text _buttonLabel;
    private CanvasGroup _group;
    private TMP_FontAsset _font;
    private GameObject _popup;
    private TMP_Text _title;
    private bool _released;

    private struct Cell
    {
        public GameLanguage.Lang Lang;
        public TMP_Text Label;
    }

    private readonly System.Collections.Generic.List<Cell> _cells = new System.Collections.Generic.List<Cell>();

    /// <summary>덧붙인 버튼(검수).</summary>
    public Button LanguageButton
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
            if (root.GetComponent<MainMenuLanguage>() != null) continue;
            root.AddComponent<MainMenuLanguage>().Build();
        }
    }

    // ── 버튼 ─────────────────────────────────────────────────

    private void Build()
    {
        _quit = FindButton(QuitButtonName);
        if (_quit == null)
        {
            Debug.LogWarning("[MainMenuLanguage] 메인 화면에서 Btn_Quit을 찾지 못했습니다 — 언어 버튼을 붙이지 않습니다.");
            return;
        }

        _quitLabel = _quit.GetComponentInChildren<TMP_Text>(true);
        _quitGroup = _quit.GetComponent<CanvasGroup>();
        _font = MainMenuCredits.PickFont(_quitLabel);

        _button = MakeButton((RectTransform)transform, "Btn_Language", GameLanguage.ButtonLabel, 30f,
            new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-48f, 36f), new Vector2(360f, 56f),
            TextAlignmentOptions.MidlineRight, Open);
        _buttonLabel = _button.GetComponentInChildren<TMP_Text>(true);
        _group = _button.gameObject.GetComponent<CanvasGroup>();
        _released = _quit.interactable;
        _button.interactable = _released;
        Button start = FindButton("Btn_Start");
        _startLabel = start != null ? start.GetComponentInChildren<TMP_Text>(true) : null;
        GameLanguage.Changed += OnLanguageChanged;
        Refresh();   // 처음 열릴 때도 저장된 언어로(한국어면 START·QUIT을 「입장」·「종료」로)
    }

    private TMP_Text _startLabel;

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

        // QUIT 글자가 나타나는 박자 · OPTIONS 화면이 QUIT을 감출 때(CanvasGroup) 같이 감춘다.
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

    private void OnDestroy()
    {
        GameLanguage.Changed -= OnLanguageChanged;
        if (_popup != null) Destroy(_popup);
    }

    // ── 팝업 ─────────────────────────────────────────────────

    /// <summary>팝업을 연다(처음이면 세운다).</summary>
    public void Open()
    {
        if (_popup == null) _popup = BuildPopup();
        _popup.SetActive(true);
        Refresh();
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
        // 메인 화면 캔버스와 따로 — 자기 캔버스가 위에 그려져 뒤의 버튼 클릭을 막는다(이름이 「HUD_Main」으로 시작하면 자동 설치에 걸리므로 다르게 짓는다).
        GameObject go = new GameObject("MainMenuLanguagePopup", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 300;

        CanvasScaler scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform root = (RectTransform)go.transform;

        GameObject dim = new GameObject("Dim", typeof(RectTransform), typeof(Image));
        dim.transform.SetParent(root, false);
        Stretch((RectTransform)dim.transform);
        Image dimImage = dim.GetComponent<Image>();
        dimImage.color = new Color(0f, 0f, 0f, DimAlpha);
        dimImage.raycastTarget = true;

        _title = MakeText(root, "Title", "", 64f, TextAlignmentOptions.Center);
        RectTransform titleRt = _title.rectTransform;
        titleRt.anchorMin = titleRt.anchorMax = titleRt.pivot = new Vector2(0.5f, 1f);
        titleRt.anchoredPosition = new Vector2(0f, -150f);
        titleRt.sizeDelta = new Vector2(900f, 100f);

        _cells.Clear();
        GameLanguage.Lang[] langs = { GameLanguage.Lang.Korean, GameLanguage.Lang.English };
        for (int i = 0; i < langs.Length; i++)
        {
            GameLanguage.Lang lang = langs[i];
            float x = (i == 0 ? -1f : 1f) * 220f;
            Button cell = MakeButton(root, "Btn_" + lang, GameLanguage.NativeName(lang), 48f,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, 20f), new Vector2(380f, 90f),
                TextAlignmentOptions.Center, () => GameLanguage.Set(lang));
            _cells.Add(new Cell { Lang = lang, Label = cell.GetComponentInChildren<TMP_Text>(true) });
        }

        MakeButton(root, "Btn_Close", "close", 36f,
            new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-64f, 48f), new Vector2(260f, 64f),
            TextAlignmentOptions.MidlineRight, Close);

        return go;
    }

    private void OnLanguageChanged()
    {
        Refresh();
    }

    /// <summary>글자·표시를 지금 언어에 맞춘다.</summary>
    private void Refresh()
    {
        SetLabel(_startLabel, "START");
        SetLabel(_quitLabel, "QUIT");
        if (_buttonLabel != null)
        {
            _buttonLabel.text = GameLanguage.ButtonLabel;
            GameLanguage.FitFont(_buttonLabel, _buttonLabel.text);
        }

        if (_title != null)
        {
            _title.text = GameLanguage.IsEnglish ? "[Language]" : "[언어]";
            GameLanguage.FitFont(_title, _title.text);
        }

        for (int i = 0; i < _cells.Count; i++)
        {
            Cell cell = _cells[i];
            if (cell.Label == null) continue;
            bool on = cell.Lang == GameLanguage.Current;
            string name = GameLanguage.NativeName(cell.Lang);
            cell.Label.text = on ? "<u>" + name + "</u>" : name;
            cell.Label.alpha = on ? 1f : 0.55f;
            GameLanguage.FitFont(cell.Label, name);
        }
    }

    private static void SetLabel(TMP_Text label, string english)
    {
        if (label == null) return;
        string text = GameLanguage.Localize(english);
        label.text = text;
        GameLanguage.FitFont(label, text);
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

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}

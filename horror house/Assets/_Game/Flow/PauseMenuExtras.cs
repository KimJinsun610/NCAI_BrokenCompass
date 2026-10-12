using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 일시정지 화면(<c>HUD_Pause_Design</c> — <see cref="PauseMenu"/>) 덧붙임.
/// 92차(민: 「esc를 누르면 resume, options, quit 이 세개가 나오고, options를 누르면 라이팅, 마우스감도, 볼륨조절, 뒤로가기 키가 나오도록」):
/// <list type="bullet">
/// <item><b>첫 화면</b>: RESUME · OPTIONS · QUIT(제목 PAUSE). 메뉴를 열 때마다 이 화면으로.</item>
/// <item><b>OPTIONS 화면</b>: LIGHT · MOUSE · VOLUME · BACK(제목 OPTIONS). 앞의 셋은 누르면 그 줄 오른쪽에 슬라이더 묶음이 펼쳐지고 나머지는 접힌다(86차와 같게).
/// BACK은 첫 화면으로.</item>
/// <item>버튼·묶음은 LIGHT 버튼(<c>Btn_Light</c>)·밝기 묶음(<c>Group_Brightness</c>)을 복제해 모양·글꼴이 같다. 복제한 버튼의 김진선님 OnClick은 비우고,
/// 복제한 묶음의 <see cref="BrightnessSlider"/>는 떼고 <see cref="OptionSlider"/>를 단다. 줄 간격 <see cref="RowStep"/>, 첫 줄은 LIGHT가 있던 자리.</item>
/// <item>RESUME은 김진선님 <c>FPController.ResumeGame</c>(계속하기)에 더해 커서를 곧바로 숨기고 화면 가운데에 묶는다(92차 — 「resume을 해도 커서가 남아있는」).</item>
/// </list>
/// 프리팹·씬·김진선님 스크립트는 고치지 않는다(런타임 덧붙임). 감시자(<see cref="EnsureWatcher"/>)가 일시정지 화면이 생기면 붙인다.
/// </summary>
[DisallowMultipleComponent]
public sealed class PauseMenuExtras : MonoBehaviour
{
    private const string LightButton = "Btn_Light";
    private const string QuitButton = "Btn_Quite";
    private const string BrightnessGroup = "Group_Brightness";
    private const string ResumeButton = "Btn_Resume";
    private const string TitleName = "Title_Pause";

    /// <summary>버튼 줄 간격(캔버스 1080 기준 px) — 버튼 높이 45 + 틈 20.</summary>
    public const float RowStep = 65f;

    /// <summary>설정 화면 슬라이더 묶음을 오른쪽으로 비키는 양(px) — 버튼 글자가 길어도 닿지 않게.</summary>
    public const float GroupShiftX = 36f;

    /// <summary>첫 화면 제목.</summary>
    public const string MainTitle = "PAUSE";

    /// <summary>설정 화면 제목.</summary>
    public const string OptionsTitle = "OPTIONS";

    private GameObject _brightness;
    private readonly List<GameObject> _groups = new List<GameObject>();
    private readonly List<GameObject> _mainButtons = new List<GameObject>();
    private readonly List<GameObject> _optionButtons = new List<GameObject>();
    private TMP_Text _title;
    private bool _onOptions;

    /// <summary>덧붙인 묶음(마우스·볼륨, 시험·검수).</summary>
    public IReadOnlyList<GameObject> Groups
    {
        get { return _groups; }
    }

    /// <summary>지금 설정 화면인지(시험·검수).</summary>
    public bool OnOptions
    {
        get { return _onOptions; }
    }

    private void OnEnable()
    {
        ShowMain();   // 메뉴를 열 때마다 첫 화면(PauseMenu가 밝기 묶음을 접는 것과 같게)
    }

    private void CloseOwn()
    {
        for (int i = 0; i < _groups.Count; i++)
        {
            if (_groups[i] != null && _groups[i].activeSelf) _groups[i].SetActive(false);
        }
    }

    private void CloseAll()
    {
        CloseOwn();
        if (_brightness != null && _brightness.activeSelf) _brightness.SetActive(false);
    }

    private void Toggle(GameObject group)
    {
        bool open = group != null && !group.activeSelf;
        CloseAll();
        if (group != null) group.SetActive(open);
    }

    /// <summary>첫 화면(RESUME · OPTIONS · QUIT).</summary>
    public void ShowMain()
    {
        SetPage(false);
    }

    /// <summary>설정 화면(LIGHT · MOUSE · VOLUME · BACK).</summary>
    public void ShowOptions()
    {
        SetPage(true);
    }

    private void SetPage(bool options)
    {
        _onOptions = options;
        CloseAll();
        for (int i = 0; i < _mainButtons.Count; i++) if (_mainButtons[i] != null) _mainButtons[i].SetActive(!options);
        for (int i = 0; i < _optionButtons.Count; i++) if (_optionButtons[i] != null) _optionButtons[i].SetActive(options);
        if (_title != null) _title.text = options ? OptionsTitle : MainTitle;
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);   // 누른 버튼이 강조된 채 남지 않게
    }

    /// <summary>계속하기 — 김진선님 계속하기(메뉴 닫기·시간 재개)에 더해 커서를 곧바로 숨기고 묶는다.</summary>
    public static void HideCursorNow()
    {
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
    }

    // ── 붙이기 ───────────────────────────────────────────────

    /// <summary>그 일시정지 화면에 붙인다(이미 붙어 있으면 그대로). 필요한 자식을 못 찾으면 덧붙이지 않는다.</summary>
    public static PauseMenuExtras Install(PauseMenu menu)
    {
        if (menu == null) return null;
        PauseMenuExtras have = menu.GetComponent<PauseMenuExtras>();
        if (have != null) return have;

        Transform root = menu.transform;
        RectTransform light = root.Find(LightButton) as RectTransform;
        RectTransform quit = root.Find(QuitButton) as RectTransform;
        RectTransform bright = root.Find(BrightnessGroup) as RectTransform;
        RectTransform resume = root.Find(ResumeButton) as RectTransform;
        PauseMenuExtras extras = menu.gameObject.AddComponent<PauseMenuExtras>();
        if (light == null || quit == null || bright == null || resume == null)
        {
            Debug.LogWarning("[PauseMenuExtras] 일시정지 화면에서 Btn_Light·Btn_Quite·Btn_Resume·Group_Brightness를 찾지 못했습니다 — 설정 화면을 붙이지 않습니다.");
            return extras;
        }

        extras._brightness = bright.gameObject;
        Transform title = root.Find(TitleName);
        extras._title = title != null ? title.GetComponent<TMP_Text>() : null;

        // 버튼마다 기준점(anchor)이 달라(QUIT는 아래 기준) anchoredPosition이 아니라 캔버스 안 자리(localPosition)로 줄을 맞춘다.
        float top = light.localPosition.y;
        float groupDy = bright.localPosition.y - top;
        int sibling = light.GetSiblingIndex();

        // 첫 화면: RESUME · OPTIONS · QUIT
        SetLocalY(resume, top);
        RectTransform options = extras.CloneButton(root, light, "OPTIONS", "Options", top - RowStep, sibling + 1);
        SetLocalY(quit, top - 2f * RowStep);
        Button optionsButton = options.GetComponent<Button>();
        if (optionsButton != null) optionsButton.onClick.AddListener(extras.ShowOptions);
        Button resumeButton = resume.GetComponent<Button>();
        if (resumeButton != null) resumeButton.onClick.AddListener(HideCursorNow);   // 김진선님 계속하기(HUDActions) 뒤에 덧붙임
        extras._mainButtons.Add(resume.gameObject);
        extras._mainButtons.Add(options.gameObject);
        extras._mainButtons.Add(quit.gameObject);

        // 설정 화면: LIGHT · MOUSE · VOLUME · BACK
        SetLocalY(light, top);
        SetLocalY(bright, top + groupDy);
        bright.anchoredPosition += new Vector2(GroupShiftX, 0f);   // 「MOUSE」·「VOLUME」 글자가 슬라이더에 닿지 않게(복제 묶음도 따라감)
        extras._optionButtons.Add(light.gameObject);
        extras._optionButtons.Add(extras.AddRow(root, light, bright, "MOUSE", "Mouse", OptionSlider.Kind.Mouse, top - RowStep, groupDy, sibling + 2));
        extras._optionButtons.Add(extras.AddRow(root, light, bright, "VOLUME", "Volume", OptionSlider.Kind.Volume, top - 2f * RowStep, groupDy, sibling + 3));
        RectTransform back = extras.CloneButton(root, light, "BACK", "Back", top - 3f * RowStep, sibling + 4);
        Button backButton = back.GetComponent<Button>();
        if (backButton != null) backButton.onClick.AddListener(extras.ShowMain);
        extras._optionButtons.Add(back.gameObject);

        Button lightButton = light.GetComponent<Button>();
        if (lightButton != null) lightButton.onClick.AddListener(extras.CloseOwn);   // LIGHT를 펼치면 우리 묶음은 접는다(밝기 여닫기는 김진선님 OnClick)
        extras.ShowMain();
        return extras;
    }

    /// <summary>LIGHT 버튼 모양의 버튼 하나(김진선님 OnClick은 비움).</summary>
    private RectTransform CloneButton(Transform root, RectTransform lightButton, string label, string id, float y, int sibling)
    {
        GameObject btn = Instantiate(lightButton.gameObject, root, false);
        btn.name = "Btn_" + id;
        RectTransform brt = (RectTransform)btn.transform;
        SetLocalY(brt, y);
        btn.transform.SetSiblingIndex(Mathf.Min(sibling, root.childCount - 1));
        TMP_Text text = btn.GetComponentInChildren<TMP_Text>(true);
        if (text != null) text.text = label;
        Button button = btn.GetComponent<Button>();
        if (button != null) button.onClick = new Button.ButtonClickedEvent();
        return brt;
    }

    private GameObject AddRow(Transform root, RectTransform lightButton, RectTransform brightGroup, string label, string id, OptionSlider.Kind kind, float y, float groupDy, int sibling)
    {
        RectTransform brt = CloneButton(root, lightButton, label, id, y, sibling);

        GameObject group = Instantiate(brightGroup.gameObject, root, false);
        group.SetActive(false);
        BrightnessSlider old = group.GetComponent<BrightnessSlider>();
        if (old != null) DestroyImmediate(old);
        group.name = "Group_" + id;
        RectTransform grt = (RectTransform)group.transform;
        SetLocalY(grt, y + groupDy);
        Slider slider = group.GetComponentInChildren<Slider>(true);
        if (slider != null) slider.onValueChanged = new Slider.SliderEvent();
        TMP_Text value = null;
        foreach (TMP_Text t in group.GetComponentsInChildren<TMP_Text>(true))
        {
            if (t.name.IndexOf("Value", System.StringComparison.OrdinalIgnoreCase) < 0) continue;
            value = t;
            t.name = "Txt_" + id + "Value";
            break;
        }

        if (slider != null) slider.name = "Slider_" + id;
        OptionSlider option = group.AddComponent<OptionSlider>();
        option.Setup(kind, slider, value);
        _groups.Add(group);

        Button button = brt.GetComponent<Button>();
        if (button != null) button.onClick.AddListener(() => Toggle(group));
        return brt.gameObject;
    }

    /// <summary>캔버스 안 높이(localPosition.y)를 맞춘다(기준점·축은 그대로).</summary>
    private static void SetLocalY(RectTransform rt, float y)
    {
        rt.anchoredPosition += new Vector2(0f, y - rt.localPosition.y);
    }

    // ── 감시자 ───────────────────────────────────────────────

    private static Watcher s_watcher;

    /// <summary>일시정지 화면을 찾아 붙이고, 새 1인칭 조작기에 감도를 바르는 감시자를 세운다(한 번).</summary>
    public static void EnsureWatcher()
    {
        if (s_watcher != null) return;
        GameObject go = new GameObject("PlayerOptions (auto)");
        DontDestroyOnLoad(go);
        s_watcher = go.AddComponent<Watcher>();
    }

    private sealed class Watcher : MonoBehaviour
    {
        private float _next;
        private FPController[] _players = new FPController[0];
        private HUDActions[] _screens = new HUDActions[0];
        // 개발자 모드 패널(DevModePanel)은 「#if UNITY_EDITOR || DEVELOPMENT_BUILD」로 막혀 있어 릴리즈 빌드에는 타입 자체가 없다
        // — 조건 없이 참조하면 Development Build를 끈 순간 CS0246으로 빌드가 멈춘다(2026-10-11·10-12 실제로 두 번).
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private DevModePanel _dev;
        private static readonly System.Reflection.FieldInfo DevShow =
            typeof(DevModePanel).GetField("show", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
#endif

        /// <summary>개발자 패널이 열려 있는지 — 릴리즈 빌드에는 패널이 없으므로 늘 false.</summary>
        private bool DevPanelOpen
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                return _dev != null && DevShow != null && (bool)DevShow.GetValue(_dev);
#else
                return false;
#endif
            }
        }

        private void Update()
        {
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 0.5f;
            foreach (PauseMenu menu in FindObjectsByType<PauseMenu>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (menu.GetComponent<PauseMenuExtras>() == null) Install(menu);
            }

            PlayerOptions.ApplyMouseToNew();
            _players = FindObjectsByType<FPController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            _screens = FindObjectsByType<HUDActions>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _dev = FindAnyObjectByType<DevModePanel>(FindObjectsInactive.Include);
#endif
        }

        /// <summary>
        /// 86차(민: 「마우스 커서는 esc 화면일 때만 나오게」): 1인칭 조작기가 있는 씬(근무)에서는 버튼을 누르는 화면 —
        /// 일시정지 메뉴·사망 화면(둘 다 <see cref="HUDActions"/>) — 이 떠 있을 때만 커서를 보이고 풀어 둔다. 그 밖에는 숨기고 화면 가운데에 묶는다.
        /// 김진선님 <c>FPController</c>는 <c>hideCursor</c>가 꺼져 있으면 커서를 켜 두고(OnEnable·Start·OnDisable), 묶지도 않아 플레이 중에도 커서가 보였다.
        /// 개발자 모드 패널이 열려 있는 동안은 건드리지 않는다. CCTV를 보는 동안은 숨기기만 한다(CCTV가 묶음을 직접 다룬다). 메뉴·결과 씬은 그대로.
        /// </summary>
        private void LateUpdate()
        {
            bool gameplay = false;
            for (int i = 0; i < _players.Length; i++)
            {
                if (_players[i] != null) { gameplay = true; break; }
            }

            if (!gameplay) return;
            if (DevPanelOpen) return;

            bool menu = false;
            for (int i = 0; i < _screens.Length; i++)
            {
                if (_screens[i] != null && _screens[i].gameObject.activeInHierarchy) { menu = true; break; }
            }

            if (menu)
            {
                if (!Cursor.visible) Cursor.visible = true;
                if (Cursor.lockState != CursorLockMode.None) Cursor.lockState = CursorLockMode.None;
                return;
            }

            if (Cursor.visible) Cursor.visible = false;
            bool cctv = CctvSystem.Active != null && CctvSystem.Active.IsViewing;
            if (!cctv && Cursor.lockState != CursorLockMode.Locked) Cursor.lockState = CursorLockMode.Locked;
        }

        private void OnDestroy()
        {
            if (s_watcher == this) s_watcher = null;
        }
    }
}

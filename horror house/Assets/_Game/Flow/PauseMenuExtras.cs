using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 86차(민: 「마우스 감도 조절, 볼륨을 esc 누르면 지금 라이팅처럼 조절이 가능한 인터페이스를 추가해줘」).
/// 일시정지 화면(<c>HUD_Pause_Design</c> — <see cref="PauseMenu"/>)에 <b>MOUSE</b>·<b>VOLUME</b> 버튼과 슬라이더 묶음을 런타임에 덧붙인다.
/// <list type="bullet">
/// <item>LIGHT 버튼(<c>Btn_Light</c>)과 밝기 묶음(<c>Group_Brightness</c>)을 복제한다 — 모양·글꼴이 같다. 순서는 LIGHT · MOUSE · VOLUME · RESUME · QUIT,
/// LIGHT 자리는 그대로 두고 <see cref="RowStep"/> 간격으로 아래로(RESUME·QUIT를 그만큼 내림 — 1080 화면 안).</item>
/// <item>버튼을 누르면 그 묶음을 펼치고 나머지(밝기 포함)는 접는다. LIGHT를 누르면 MOUSE·VOLUME 묶음을 접는다. 메뉴를 열 때마다 모두 접힌 채(밝기와 같게).</item>
/// <item>복제한 버튼의 김진선님 OnClick(밝기 여닫기)은 비우고, 복제한 묶음의 <see cref="BrightnessSlider"/>는 떼고 <see cref="OptionSlider"/>를 단다.</item>
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

    /// <summary>버튼 줄 간격(캔버스 1080 기준 px) — 버튼 높이 45 + 틈 20.</summary>
    public const float RowStep = 65f;

    private GameObject _brightness;
    private readonly List<GameObject> _groups = new List<GameObject>();

    /// <summary>덧붙인 묶음(시험·검수).</summary>
    public IReadOnlyList<GameObject> Groups
    {
        get { return _groups; }
    }

    private void OnEnable()
    {
        CloseOwn();   // 메뉴를 열 때마다 접힌 채(PauseMenu가 밝기 묶음을 접는 것과 같게)
    }

    private void CloseOwn()
    {
        for (int i = 0; i < _groups.Count; i++)
        {
            if (_groups[i] != null && _groups[i].activeSelf) _groups[i].SetActive(false);
        }
    }

    private void Toggle(GameObject group)
    {
        bool open = group != null && !group.activeSelf;
        CloseOwn();
        if (_brightness != null && _brightness.activeSelf) _brightness.SetActive(false);
        if (group != null) group.SetActive(open);
    }

    // ── 붙이기 ───────────────────────────────────────────────

    /// <summary>그 일시정지 화면에 붙인다(이미 붙어 있으면 그대로). 필요한 자식을 못 찾으면 null.</summary>
    public static PauseMenuExtras Install(PauseMenu menu)
    {
        if (menu == null) return null;
        PauseMenuExtras have = menu.GetComponent<PauseMenuExtras>();
        if (have != null) return have;

        Transform root = menu.transform;
        RectTransform light = root.Find(LightButton) as RectTransform;
        RectTransform quit = root.Find(QuitButton) as RectTransform;
        RectTransform bright = root.Find(BrightnessGroup) as RectTransform;
        PauseMenuExtras extras = menu.gameObject.AddComponent<PauseMenuExtras>();
        if (light == null || quit == null || bright == null)
        {
            Debug.LogWarning("[PauseMenuExtras] 일시정지 화면에서 Btn_Light·Btn_Quite·Group_Brightness를 찾지 못했습니다 — 감도·볼륨 슬라이더를 붙이지 않습니다.");
            return extras;
        }

        extras._brightness = bright.gameObject;
        // 버튼마다 기준점(anchor)이 달라(QUIT는 아래 기준) anchoredPosition이 아니라 캔버스 안 자리(localPosition)로 줄을 맞춘다.
        float top = light.localPosition.y;
        float groupDy = bright.localPosition.y - top;
        int sibling = light.GetSiblingIndex();
        extras.AddRow(root, light, bright, "MOUSE", "Mouse", OptionSlider.Kind.Mouse, top - RowStep, groupDy, sibling + 1);
        extras.AddRow(root, light, bright, "VOLUME", "Volume", OptionSlider.Kind.Volume, top - 2f * RowStep, groupDy, sibling + 2);
        RectTransform resume = root.Find(ResumeButton) as RectTransform;
        if (resume != null) SetLocalY(resume, top - 3f * RowStep);
        SetLocalY(quit, top - 4f * RowStep);

        Button lightButton = light.GetComponent<Button>();
        if (lightButton != null) lightButton.onClick.AddListener(extras.CloseOwn);   // LIGHT를 펼치면 우리 묶음은 접는다
        return extras;
    }

    private void AddRow(Transform root, RectTransform lightButton, RectTransform brightGroup, string label, string id, OptionSlider.Kind kind, float y, float groupDy, int sibling)
    {
        GameObject btn = Instantiate(lightButton.gameObject, root, false);
        btn.name = "Btn_" + id;
        RectTransform brt = (RectTransform)btn.transform;
        SetLocalY(brt, y);
        btn.transform.SetSiblingIndex(sibling);
        TMP_Text text = btn.GetComponentInChildren<TMP_Text>(true);
        if (text != null) text.text = label;

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

        Button button = btn.GetComponent<Button>();
        if (button != null)
        {
            button.onClick = new Button.ButtonClickedEvent();   // 복제된 김진선님 OnClick(밝기 여닫기)은 비운다
            button.onClick.AddListener(() => Toggle(group));
        }
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
        private DevModePanel _dev;
        private static readonly System.Reflection.FieldInfo DevShow =
            typeof(DevModePanel).GetField("show", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

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
            _dev = FindAnyObjectByType<DevModePanel>(FindObjectsInactive.Include);
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
            if (_dev != null && DevShow != null && (bool)DevShow.GetValue(_dev)) return;

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

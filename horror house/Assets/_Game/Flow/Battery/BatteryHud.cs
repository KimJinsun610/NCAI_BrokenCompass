using NightDuty;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 손전등 배터리 화면 표시(57차 민: 「손전등 배터리의 인터페이스와 배터리 갯수 UI」). 오른쪽 위 시계 아래에 작은 배터리 그림 하나와
/// 주머니 칸(<see cref="BatteryRules.PocketMax"/>개 — 채운 칸이 예비)을 둔다.
/// <list type="bullet">
/// <item>충전량은 배터리 속 막대 — 30% 위는 흰빛, 30% 아래는 호박색, 10% 아래는 붉게 숨 쉬듯 깜빡인다(<see cref="BatteryRules.DimBelow"/>·<see cref="BatteryRules.LowBelow"/>).</item>
/// <item>갈아 끼우는 동안(<see cref="FlashlightPower.Swapping"/>)은 막대가 왼쪽부터 다시 찬다.</item>
/// <item>예비가 있고 30% 아래면 그림 왼쪽에 「R」 — 갈기 키 안내. 글꼴은 HUD 시계 글씨에서 빌린다(TMP 기본 글꼴에는 한글이 없다 — 여기선 R뿐이지만 모양을 맞춘다).</item>
/// <item>배터리가 꺼져 있거나 밤이 아니거나 붙잡힘·피날레·일시정지·본 HUD가 꺼진 동안은 숨는다.</item>
/// </list>
/// 판정과 무관. 근무 씬에 자동으로 선다. 김진선님 HUD(HUD_Play_Design)는 고치지 않고 따로 캔버스를 둔다(정렬 10 — 본 HUD 위, 날짜 소개(100)·페이드(200) 아래).
/// </summary>
[DisallowMultipleComponent]
public sealed class BatteryHud : MonoBehaviour
{
    private const string MainHudName = "HUD_Play_Design";
    private const float BodyWidth = 50f;
    private const float BodyHeight = 22f;
    private const float Border = 2f;
    private const float Pad = 2f;
    private const float PipWidth = 10f;
    private const float PipHeight = 18f;

    private static readonly Color Frame = new Color(1f, 1f, 1f, 0.85f);
    private static readonly Color Full = new Color(0.88f, 0.92f, 0.86f, 0.92f);
    private static readonly Color Amber = new Color(1f, 0.72f, 0.3f, 0.95f);
    private static readonly Color Red = new Color(0.95f, 0.25f, 0.2f, 1f);
    private static readonly Color PipEmpty = new Color(1f, 1f, 1f, 0.22f);

    private Canvas _canvas;
    private CanvasGroup _group;
    private RectTransform _fill;
    private Image _fillImage;
    private Image[] _pips;
    private TMP_Text _hint;
    private Canvas _mainHud;
    private float _alpha;

    /// <summary>지금 근무 씬의 것. 없으면 null.</summary>
    public static BatteryHud Active { get; private set; }

    /// <summary>지금 보이는지(시험·검수).</summary>
    public bool Showing
    {
        get { return _group != null && _group.alpha > 0.01f; }
    }

    // ── 자동 설치 ─────────────────────────────────────────────

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Install()
    {
        Active = null;
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
        if (!FlowAutoInstall.IsDutyScene(scene) || FlowAutoInstall.Exists<BatteryHud>(scene)) return;
        FlowAutoInstall.CreateHost<BatteryHud>(scene, "BatteryHud (auto)");
    }

    private void OnEnable()
    {
        Active = this;
    }

    private void OnDisable()
    {
        if (_group != null) _group.alpha = 0f;
        if (Active == this) Active = null;
    }

    // ── 매 프레임 ─────────────────────────────────────────────

    private void LateUpdate()
    {
        FlashlightBattery b = NightRun.Battery;
        bool show = b != null && Visible();
        if (show && _canvas == null) Build();
        if (_canvas == null) return;

        _alpha = Mathf.MoveTowards(_alpha, show ? 1f : 0f, Time.unscaledDeltaTime * 4f);
        _group.alpha = _alpha;
        if (b == null || _alpha <= 0f) return;

        FlashlightPower power = FlashlightPower.Active;
        bool swapping = power != null && power.Swapping;
        float charge = swapping ? power.SwapProgress : b.Charge;
        float inner = BodyWidth - (Border + Pad) * 2f;
        _fill.sizeDelta = new Vector2(inner * Mathf.Clamp01(charge), _fill.sizeDelta.y);

        Color c;
        if (swapping) c = Full;
        else if (b.Charge < BatteryRules.LowBelow)
        {
            c = Red;
            c.a = 0.45f + 0.55f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5f));
        }
        else if (b.Charge < BatteryRules.DimBelow) c = Amber;
        else c = Full;
        _fillImage.color = c;

        for (int i = 0; i < _pips.Length; i++) _pips[i].color = i < b.Spare ? Frame : PipEmpty;

        bool hint = !swapping && b.Spare > 0 && b.Charge < BatteryRules.DimBelow;
        if (_hint != null && _hint.gameObject.activeSelf != hint) _hint.gameObject.SetActive(hint);
    }

    private bool Visible()
    {
        if (!NightRun.IsNightActive || NightRun.IsCaptured || GamePause.IsPaused) return false;
        CaptureDirector capture = CaptureDirector.Active;
        if (capture != null && capture.IsPlaying) return false;
        if (_mainHud == null)
        {
            GameObject go = GameObject.Find(MainHudName);
            _mainHud = go != null ? go.GetComponent<Canvas>() : null;
        }

        return _mainHud == null || (_mainHud.enabled && _mainHud.gameObject.activeInHierarchy);
    }

    // ── 만들기 ───────────────────────────────────────────────

    private void Build()
    {
        GameObject go = new GameObject("battery hud");
        go.transform.SetParent(transform, false);
        _canvas = go.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 10;
        CanvasScaler scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        _group = go.AddComponent<CanvasGroup>();
        _group.alpha = 0f;
        _group.interactable = false;
        _group.blocksRaycasts = false;

        // 시계(오른쪽 위, 위에서 41px) 바로 아래 오른쪽 끝에 맞춘다.
        RectTransform box = Rect("battery", go.transform, new Vector2(1f, 1f), new Vector2(-52f, -86f), new Vector2(200f, 30f));
        box.pivot = new Vector2(1f, 1f);

        // 본체: 테두리 네 변 + 오른쪽 꼭지.
        float x0 = -(BodyWidth + 5f);
        Bar(box, "top", x0, 0f, BodyWidth, Border, Frame);
        Bar(box, "bottom", x0, -(BodyHeight - Border), BodyWidth, Border, Frame);
        Bar(box, "left", x0, 0f, Border, BodyHeight, Frame);
        Bar(box, "right", x0 + BodyWidth - Border, 0f, Border, BodyHeight, Frame);
        Bar(box, "nub", -5f, -(BodyHeight - 10f) * 0.5f, 4f, 10f, Frame);
        float inner = BodyWidth - (Border + Pad) * 2f;
        _fillImage = Bar(box, "charge", x0 + Border + Pad, -(Border + Pad), inner, BodyHeight - (Border + Pad) * 2f, Full);
        _fill = _fillImage.rectTransform;

        // 주머니 칸 — 본체 왼쪽에 작은 배터리 PocketMax개(오른쪽부터 채움).
        _pips = new Image[BatteryRules.PocketMax];
        float px = x0 - 12f;
        for (int i = 0; i < _pips.Length; i++)
        {
            px -= PipWidth;
            _pips[i] = Bar(box, "spare " + (i + 1), px, -(BodyHeight - PipHeight) * 0.5f, PipWidth, PipHeight, PipEmpty);
            Bar(_pips[i].rectTransform, "cap", -PipWidth * 0.7f, 3f, PipWidth * 0.4f, 3f, PipEmpty).color = new Color(1f, 1f, 1f, 0.5f);
            px -= 5f;
        }

        _hint = MakeHint(box, px - 6f);
    }

    private static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    /// <summary>오른쪽 위 기준(x는 왼쪽 가장자리, y는 위 가장자리) 막대 하나.</summary>
    private static Image Bar(RectTransform parent, string name, float x, float y, float w, float h, Color color)
    {
        RectTransform rt = Rect(name, parent, new Vector2(1f, 1f), new Vector2(x, y), new Vector2(w, h));
        rt.pivot = new Vector2(0f, 1f);
        Image img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private static TMP_Text MakeHint(RectTransform parent, float right)
    {
        TMP_FontAsset font = null;
        GameObject time = GameObject.Find(MainHudName + "/Txt_Time");
        TMP_Text src = time != null ? time.GetComponent<TMP_Text>() : null;
        if (src != null) font = src.font;

        RectTransform rt = Rect("swap key", parent, new Vector2(1f, 1f), new Vector2(right, 1f), new Vector2(40f, BodyHeight + 2f));
        rt.pivot = new Vector2(1f, 1f);
        TextMeshProUGUI t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.text = "R";
        t.fontSize = 22f;
        t.alignment = TextAlignmentOptions.MidlineRight;
        t.color = new Color(1f, 0.85f, 0.6f, 0.9f);
        t.raycastTarget = false;
        rt.gameObject.SetActive(false);
        return t;
    }
}

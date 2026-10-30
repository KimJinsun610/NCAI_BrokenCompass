using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 2026-10-30(김진선님: 「1일차 시작하면 태블릿 옆 문구처럼 잠시 알려주는 게 뜨고 태블릿을 내리면 사라지게 · 좌측 조작 안내도 태블릿을 내리고 나서 뜨게」).
/// <list type="bullet">
/// <item>1일차 밤이 열리면 태블릿을 확대해 든 채 시작하고(<see cref="TabletZoom.Press"/>), 확대가 끝나면 화면 판 오른쪽에
/// 점·선·문구(「지시 사항을 확인하시오.」 · 「TAB - 메시지/수칙 전환」 · 「우클릭 - 태블릿 내리기」)를 띄운다. 자리는 매 프레임 화면 판을 재서 맞춘다.</item>
/// <item>우클릭으로 내리면(확대가 풀리면) 사라지고, 그때부터 왼쪽 위 조작 안내(<see cref="ControlHints"/>)가 보인다 — 그 전에는 <see cref="HoldsControlHints"/>가 가린다.</item>
/// <item>그 씬에서 한 번만. 붙잡혀 같은 밤을 다시 해도 다시 띄우지 않는다. 2일차부터는 없다(조작 안내는 옛날처럼 2일차까지).</item>
/// <item>안내가 떠 있는 동안(태블릿을 내리기 전까지) 출근 시계 붙듦을 풀지 않는다 — <see cref="NightDuty.NightRun.HoldStartUntilReleased"/>
/// (경비실을 나서도·60초가 지나도). 내리면 <see cref="NightDuty.NightRun.ReleaseStartHold"/>로 밤 시계가 흐른다. 2일차부터는 옛날처럼 경비실을 나설 때까지(최대 60초).</item>
/// <item>태블릿을 끝내 못 찾으면 <see cref="GiveUpSeconds"/> 뒤 그냥 끝낸다 — 조작 안내가 영영 가려지지 않게.</item>
/// </list>
/// 씬·프리팹은 고치지 않는다(런타임 HUD 캔버스 자식). 근무 씬에 자동으로 선다.
/// </summary>
[DisallowMultipleComponent]
public sealed class TabletIntroCallout : MonoBehaviour
{
    public const int Day = 1;
    public const string Title = "지시 사항을 확인하시오.";
    public static readonly string[] Lines = { "TAB - 메시지/수칙 전환", "우클릭 - 태블릿 내리기" };

    /// <summary>태블릿을 못 찾거나 확대가 안 될 때 기다리는 한도(실제 초).</summary>
    public const float GiveUpSeconds = 10f;

    private const string HudName = "HUD_Play_Design";

    // 1920×1080 기준 캔버스 단위.
    private const float DotGap = 26f;        // 화면 판 오른쪽 끝에서 점까지
    private const float HeightFromTop = 0.34f;   // 화면 판 위에서부터 높이 비율
    private const float DotSize = 16f;
    private const float LineLength = 120f;
    private const float LineThickness = 2f;
    private const float TextGap = 12f;
    private const float TitleSize = 30f;
    private const float LineSize = 19f;
    private const float LineStep = 27f;
    private const float FadeIn = 0.35f;
    private const float FadeOut = 0.25f;

    private enum Stage { Waiting, Zooming, Showing, Done }

    private static bool s_dismissed;
    private static Sprite s_ring;

    private Stage _stage;
    private float _waited;
    private RectTransform _canvasRect;
    private RectTransform _root;
    private CanvasGroup _group;

    /// <summary>1일차에 아직 안내를 거두지 않았는지 — 그동안 왼쪽 위 조작 안내(<see cref="ControlHints"/>)와 책상 손전등·배터리 안내(<see cref="DeskStarterKit"/>)를 가린다.</summary>
    public static bool HoldsControlHints
    {
        get { return GameSession.CurrentDay == Day && !s_dismissed; }
    }

    /// <summary>안내가 지금 떠 있는지(검수용).</summary>
    public bool Showing
    {
        get { return _stage == Stage.Showing; }
    }

    // ── 자동 설치 ─────────────────────────────────────────────

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Install()
    {
        s_dismissed = false;
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
        if (mode == LoadSceneMode.Single) s_dismissed = false;   // 근무 씬을 새로 열 때마다(날마다) 처음부터
        EnsureFor(scene);
    }

    private static void EnsureFor(Scene scene)
    {
        if (!FlowAutoInstall.IsDutyScene(scene) || FlowAutoInstall.Exists<TabletIntroCallout>(scene)) return;
        FlowAutoInstall.CreateHost<TabletIntroCallout>(scene, "TabletIntroCallout (auto)");
    }

    // ── 매 프레임 ─────────────────────────────────────────────

    private void Update()
    {
        if (_stage == Stage.Done)
        {
            Fade(0f, FadeOut);
            return;
        }

        if (GameSession.CurrentDay != Day || s_dismissed)
        {
            Dismiss();
            return;
        }

        TabletZoom zoom = TabletZoom.Active;
        switch (_stage)
        {
            case Stage.Waiting:
                _waited += Time.unscaledDeltaTime;
                if (_waited > GiveUpSeconds)
                {
                    Dismiss();
                    return;
                }

                if (zoom == null || !NightDuty.NightRun.IsNightActive || NightDuty.NightRun.IsCaptured) return;
                NightDuty.NightRun.HoldStartUntilReleased();   // 태블릿을 내릴 때까지 밤 시계를 붙든다(경비실을 나서도·60초가 지나도)
                if (!zoom.ZoomWanted) zoom.Press();
                if (zoom.ZoomWanted) _stage = Stage.Zooming;
                return;

            case Stage.Zooming:
                if (zoom == null || !zoom.ZoomWanted)
                {
                    Dismiss();   // 확대가 끝나기 전에 내렸다
                    return;
                }

                if (!zoom.IsZoomed) return;
                if (_root == null && !Build())
                {
                    Dismiss();
                    return;
                }

                _stage = Stage.Showing;
                Place(zoom);
                return;

            case Stage.Showing:
                if (zoom == null || !zoom.ZoomWanted || NightDuty.NightRun.IsCaptured)
                {
                    Dismiss();
                    return;
                }

                Place(zoom);
                Fade(1f, FadeIn);
                return;
        }
    }

    private void Dismiss()
    {
        NightDuty.NightRun.ReleaseStartHold("1일차 태블릿을 내림");   // 붙들어 두지 않았으면 아무 일도 없다
        s_dismissed = true;
        _stage = Stage.Done;
    }

    private void Fade(float target, float seconds)
    {
        if (_group == null) return;
        _group.alpha = Mathf.MoveTowards(_group.alpha, target, Time.unscaledDeltaTime / Mathf.Max(0.01f, seconds));
        bool visible = _group.alpha > 0.001f;
        if (_root.gameObject.activeSelf != visible) _root.gameObject.SetActive(visible);
    }

    /// <summary>점을 화면 판 오른쪽 끝, 위에서 <see cref="HeightFromTop"/> 높이에 둔다.</summary>
    private void Place(TabletZoom zoom)
    {
        Rect plate;
        if (_root == null || !zoom.TryGetPlateScreenRect(out plate)) return;
        Vector2 screen = new Vector2(plate.xMax, plate.yMax - plate.height * HeightFromTop);
        Vector2 local;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, screen, null, out local)) return;
        _root.anchoredPosition = local + new Vector2(DotGap, 0f);
        if (!_root.gameObject.activeSelf) _root.gameObject.SetActive(true);
    }

    // ── 그리기 ────────────────────────────────────────────────

    private bool Build()
    {
        GameObject hud = GameObject.Find(HudName);
        if (hud == null) return false;
        _canvasRect = (RectTransform)hud.transform;

        GameObject go = new GameObject("tablet intro callout (1일차)", typeof(RectTransform), typeof(CanvasGroup));
        go.transform.SetParent(hud.transform, false);
        go.transform.SetAsLastSibling();
        _root = (RectTransform)go.transform;
        _root.anchorMin = new Vector2(0.5f, 0.5f);
        _root.anchorMax = new Vector2(0.5f, 0.5f);
        _root.pivot = new Vector2(0f, 0.5f);
        _root.sizeDelta = Vector2.zero;
        _group = go.GetComponent<CanvasGroup>();
        _group.alpha = 0f;
        _group.interactable = false;
        _group.blocksRaycasts = false;

        // 점(고리)
        Image dot = NewImage("dot", RingSprite(), new Vector2(0f, 0f), new Vector2(DotSize, DotSize), new Vector2(0.5f, 0.5f));
        dot.preserveAspect = true;

        // 선
        NewImage("line", null, new Vector2(DotSize * 0.5f, 0f), new Vector2(LineLength, LineThickness), new Vector2(0f, 0.5f));

        // 글
        TMP_FontAsset font = PickFont(hud);
        float x = DotSize * 0.5f + LineLength + TextGap;
        NewText(Title, font, TitleSize, FontStyles.Bold, 1f, x, 0f);
        for (int i = 0; i < Lines.Length; i++)
        {
            NewText(Lines[i], font, LineSize, FontStyles.Normal, 0.85f, x + 1f, -(TitleSize * 0.5f + 8f + LineSize * 0.5f + i * LineStep));
        }

        go.SetActive(false);
        return true;
    }

    private Image NewImage(string name, Sprite sprite, Vector2 pos, Vector2 size, Vector2 pivot)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(_root, false);
        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(0f, 0.5f);
        rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        Image img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.color = Color.white;
        img.raycastTarget = false;
        return img;
    }

    private void NewText(string s, TMP_FontAsset font, float size, FontStyles style, float alpha, float x, float y)
    {
        GameObject go = new GameObject("text " + s, typeof(RectTransform));
        go.transform.SetParent(_root, false);
        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(0f, 0.5f);
        rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(600f, size * 1.4f);
        TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.text = s;
        t.fontSize = size;
        t.fontStyle = style;
        t.color = new Color(1f, 1f, 1f, alpha);
        t.alignment = TextAlignmentOptions.MidlineLeft;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Overflow;
        t.raycastTarget = false;
    }

    /// <summary>HUD 글씨와 같은 글꼴(Pretendard) — <see cref="ControlHints"/>와 같은 방식.</summary>
    private static TMP_FontAsset PickFont(GameObject hud)
    {
        foreach (TMP_Text t in hud.GetComponentsInChildren<TMP_Text>(true))
        {
            if (t.font != null) return t.font;
        }

        return null;
    }

    /// <summary>흰 고리 + 어두운 속(사진의 점). 32px.</summary>
    private static Sprite RingSprite()
    {
        if (s_ring != null) return s_ring;
        const int n = 32;
        const float outerR = 15f;
        const float ringWidth = 3.5f;
        Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        Color[] px = new Color[n * n];
        Color inner = new Color(0.08f, 0.08f, 0.08f, 0.85f);
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n * 0.5f, n * 0.5f));
                float outside = Mathf.Clamp01(outerR + 0.5f - d);            // 원 안 = 1
                float ring = Mathf.Clamp01(d - (outerR - ringWidth) + 0.5f);   // 고리 쪽 = 1
                Color c = Color.Lerp(inner, Color.white, ring);
                c.a *= outside;
                px[y * n + x] = c;
            }
        }

        tex.SetPixels(px);
        tex.Apply(false, true);
        s_ring = Sprite.Create(tex, new Rect(0f, 0f, n, n), new Vector2(0.5f, 0.5f), 100f);
        s_ring.name = "tablet intro ring";
        return s_ring;
    }
}

using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 90차(민: 「마우스 좌클릭으로 손전등 on/off, 우클릭 태블릿 들기, tab으로 메세지/수칙 전환」) — 화면 왼쪽 위 조작 안내를 새 키에 맞춘다.
/// <list type="bullet">
/// <item>김진선님 HUD(<c>HUD_Play_Design/Info</c>)의 안내는 글자까지 그려진 그림(<c>ui_bg_btn</c> · <c>ui_guide</c>)이라 고칠 수 없다 —
/// 그 두 그림만 끄고(오브젝트는 그대로) 같은 자리에 같은 모양(회색 키캡 + 흰 굵은 글씨)으로 다시 그린다.</item>
/// <item>보이는 날은 김진선님 <c>HudKeyGuide</c>를 따른다(근무일 ≤ <c>lastDay</c>, 기본 2) — 그 컴포넌트가 그림을 다시 켜도 매 프레임 끈다.</item>
/// <item>줄: WASD 이동 · E 상호작용 · 좌클릭 손전등 · 우클릭 태블릿 들기 · Tab 메시지/수칙 전환.</item>
/// </list>
/// 씬·프리팹·그림 에셋은 고치지 않는다(런타임). 근무 씬에 자동으로 선다.
/// </summary>
[DisallowMultipleComponent]
public sealed class ControlHints : MonoBehaviour
{
    private const string HudName = "HUD_Play_Design";
    private const string InfoName = "Info";
    private const string GuideImageName = "info_text";

    /// <summary>안내 줄(키 글자들, 설명).</summary>
    public static readonly string[][] Rows =
    {
        new[] { "W", "A", "S", "D", "이동" },
        new[] { "E", "상호작용" },
        new[] { "좌클릭", "손전등" },
        new[] { "우클릭", "태블릿 들기" },
        new[] { "Tab", "메시지 / 수칙 전환" },
    };

    // 옛 그림에서 잰 자리(1920×1080 기준, 화면 왼쪽 위 원점, 아래로 −).
    private const float Left = 58f;
    private const float Top = 51f;
    private const float KeyHeight = 32f;
    private const float KeyWidth = 31f;
    private const float KeyGap = 7.8f;
    private const float RowStep = 49f;
    private const float LabelGap = 13f;
    private const float LabelSize = 20f;
    private const float KeyFontSize = 19f;
    private const float WideKeyFontSize = 16f;
    private const float WidePad = 9f;

    private static readonly Color KeyFill = new Color(0.56f, 0.56f, 0.56f, 0.92f);

    private RectTransform _root;
    private Image _infoImage;
    private Image _guideImage;
    private Component _keyGuide;
    private FieldInfo _lastDay;
    private bool _failed;
    private static Sprite s_keySprite;

    /// <summary>지금 보이는지(검수용).</summary>
    public bool Showing
    {
        get { return _root != null && _root.gameObject.activeInHierarchy; }
    }

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
        if (!FlowAutoInstall.IsDutyScene(scene) || FlowAutoInstall.Exists<ControlHints>(scene)) return;
        FlowAutoInstall.CreateHost<ControlHints>(scene, "ControlHints (auto)");
    }

    private void LateUpdate()
    {
        if (_failed) return;
        if (_root == null && !Build()) return;

        // 김진선님 그림(글자가 박힌 옛 안내)은 늘 끈다 — HudKeyGuide가 날이 바뀔 때 다시 켠다.
        if (_infoImage != null && _infoImage.enabled) _infoImage.enabled = false;
        if (_guideImage != null && _guideImage.enabled) _guideImage.enabled = false;

        bool show = GameSession.CurrentDay <= LastDay();
        if (_root.gameObject.activeSelf != show) _root.gameObject.SetActive(show);
    }

    /// <summary>김진선님 HudKeyGuide의 lastDay(없으면 2).</summary>
    private int LastDay()
    {
        if (_keyGuide == null || _lastDay == null) return 2;
        object v = _lastDay.GetValue(_keyGuide);
        return v is int ? (int)v : 2;
    }

    private bool Build()
    {
        GameObject hud = GameObject.Find(HudName);
        if (hud == null) return false;
        Transform info = null;
        foreach (RectTransform rt in hud.GetComponentsInChildren<RectTransform>(true))
        {
            if (rt.name == InfoName) info = rt;
        }

        if (info == null)
        {
            _failed = true;
            Debug.LogWarning("[ControlHints] HUD 조작 안내(Info)를 찾지 못했습니다.");
            return false;
        }

        _infoImage = info.GetComponent<Image>();
        Transform guide = info.Find(GuideImageName);
        _guideImage = guide != null ? guide.GetComponent<Image>() : null;
        foreach (Component c in info.GetComponents<Component>())
        {
            if (c != null && c.GetType().Name == "HudKeyGuide")
            {
                _keyGuide = c;
                _lastDay = c.GetType().GetField("lastDay", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            }
        }

        // HUD 캔버스 바로 아래, 왼쪽 위 기준 판(Info 자식으로 두면 HudKeyGuide가 Awake에 모은 그림 목록과 섞이지 않는다).
        GameObject go = new GameObject("control hints (90차)", typeof(RectTransform));
        go.transform.SetParent(hud.transform, false);
        go.transform.SetSiblingIndex(info.GetSiblingIndex() + 1);
        _root = (RectTransform)go.transform;
        _root.anchorMin = new Vector2(0f, 1f);
        _root.anchorMax = new Vector2(0f, 1f);
        _root.pivot = new Vector2(0f, 1f);
        _root.anchoredPosition = Vector2.zero;
        _root.sizeDelta = new Vector2(600f, 320f);

        TMP_FontAsset font = PickFont(hud);
        for (int r = 0; r < Rows.Length; r++)
        {
            string[] row = Rows[r];
            float y = -(Top + r * RowStep);
            float x = Left;
            for (int k = 0; k < row.Length - 1; k++)
            {
                x += Key(row[k], font, x, y) + KeyGap;
            }

            Text(row[row.Length - 1], font, LabelSize, x - KeyGap + LabelGap, y, TextAlignmentOptions.MidlineLeft, 400f);
        }

        return true;
    }

    /// <summary>키캡 하나를 그리고 그 폭을 돌려준다. 글자가 한 자면 정사각, 길면 글자 폭 + 여백.</summary>
    private float Key(string label, TMP_FontAsset font, float x, float y)
    {
        bool wide = label.Length > 1;
        float size = wide ? WideKeyFontSize : KeyFontSize;
        float width = KeyWidth;
        TextMeshProUGUI probe = null;
        if (wide)
        {
            probe = Text(label, font, size, 0f, 0f, TextAlignmentOptions.Center, 200f);
            width = Mathf.Max(KeyWidth, probe.GetPreferredValues(label).x + WidePad * 2f);
            Destroy(probe.gameObject);
        }

        GameObject go = new GameObject("key " + label, typeof(RectTransform));
        go.transform.SetParent(_root, false);
        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(width, KeyHeight);
        Image img = go.AddComponent<Image>();
        img.sprite = KeySprite();
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = 2f;
        img.color = Color.white;
        img.raycastTarget = false;

        TextMeshProUGUI t = Text(label, font, size, 0f, 0f, TextAlignmentOptions.Center, width);
        t.rectTransform.SetParent(rt, false);
        t.rectTransform.anchorMin = Vector2.zero;
        t.rectTransform.anchorMax = Vector2.one;
        t.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        t.rectTransform.anchoredPosition = new Vector2(0f, 1f);
        t.rectTransform.sizeDelta = Vector2.zero;
        return width;
    }

    private TextMeshProUGUI Text(string s, TMP_FontAsset font, float size, float x, float y, TextAlignmentOptions align, float width)
    {
        GameObject go = new GameObject("text " + s, typeof(RectTransform));
        go.transform.SetParent(_root, false);
        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(width, KeyHeight);
        TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.text = s;
        t.fontSize = size;
        t.fontStyle = FontStyles.Bold;
        t.color = Color.white;
        t.alignment = align;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Overflow;
        t.raycastTarget = false;
        return t;
    }

    /// <summary>HUD 글씨와 같은 글꼴(Pretendard). HUD 안 TMP 글씨에서 빌린다.</summary>
    private static TMP_FontAsset PickFont(GameObject hud)
    {
        foreach (TMP_Text t in hud.GetComponentsInChildren<TMP_Text>(true))
        {
            if (t.font != null) return t.font;
        }

        foreach (TMP_Text t in FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t.font != null && t.font.name.Contains("Pretendard")) return t.font;
        }

        return null;
    }

    /// <summary>
    /// 둥근 키캡(9-슬라이스) — 회색 바탕 + 위가 밝고 아래가 어두운 테두리(옛 그림 모양). 48px, 모서리 12px, 테두리 3px(배율 2로 그리면 6px·1.5px).
    /// </summary>
    public static Sprite KeySprite()
    {
        if (s_keySprite != null) return s_keySprite;
        const int n = 48;
        const float radius = 12f;
        const float line = 3f;
        Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        Color[] px = new Color[n * n];
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float d = RoundedDistance(x + 0.5f, y + 0.5f, n, radius);   // 안쪽이 음수
                float outer = Mathf.Clamp01(0.5f - d);
                float inner = Mathf.Clamp01(0.5f - (d + line));
                float t = y / (float)(n - 1);   // 0 = 아래
                Color border = Color.Lerp(new Color(0.6f, 0.6f, 0.6f, 1f), new Color(0.97f, 0.97f, 0.97f, 1f), t);
                Color c = Color.Lerp(border, KeyFill, inner);
                c.a *= outer;
                px[y * n + x] = c;
            }
        }

        tex.SetPixels(px);
        tex.Apply(false, true);
        float b = radius + 2f;
        s_keySprite = Sprite.Create(tex, new Rect(0f, 0f, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
        s_keySprite.name = "control hint key";
        return s_keySprite;
    }

    private static float RoundedDistance(float x, float y, int n, float r)
    {
        float hx = n * 0.5f;
        float qx = Mathf.Abs(x - hx) - (hx - r);
        float qy = Mathf.Abs(y - hx) - (hx - r);
        float ox = Mathf.Max(qx, 0f);
        float oy = Mathf.Max(qy, 0f);
        return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
    }
}

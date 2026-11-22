using TMPro;
using UnityEngine;

/// <summary>
/// 게임 언어 설정(2026-11-19, 김진선님: 「로컬라이징 때문에 영어 버전을 만들려는데 — 메인화면 우 하단에 언어, 옵션에도 Language에 English·한국어」).
/// <list type="bullet">
/// <item>지금은 <b>선택값을 저장·알리는 것까지</b>다 — 글(수칙·지시·UI)을 영어로 바꾸는 번역 표는 아직 없다. 번역이 생기면 <see cref="Current"/>를 읽고 <see cref="Changed"/>를 들으면 된다.</item>
/// <item>저장은 PlayerPrefs(<see cref="Key"/>)라 씬이 바뀌어도, 껐다 켜도 이어진다. 처음 값은 <see cref="Default"/>(한국어).</item>
/// <item>메인 화면 「언어」 버튼(<c>MainMenuLanguage</c>)과 설정 화면(일시정지·메인 OPTIONS의 LANGUAGE 줄 — <c>PauseMenuExtras</c>)이 같은 값을 쓴다.</item>
/// </list>
/// </summary>
public static class GameLanguage
{
    /// <summary>언어 종류.</summary>
    public enum Lang
    {
        Korean = 0,
        English = 1,
    }

    /// <summary>저장 키.</summary>
    public const string Key = "nightduty.language";

    /// <summary>처음 값.</summary>
    public const Lang Default = Lang.Korean;

    private static Lang s_current = Default;
    private static bool s_loaded;

    /// <summary>언어가 바뀔 때.</summary>
    public static event System.Action Changed;

    /// <summary>지금 언어.</summary>
    public static Lang Current
    {
        get
        {
            Load();
            return s_current;
        }
    }

    /// <summary>영어인지.</summary>
    public static bool IsEnglish
    {
        get { return Current == Lang.English; }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        s_loaded = false;
        s_current = Default;
        s_originalFont.Clear();
        s_originalScaleX.Clear();
        Changed = null;
    }

    /// <summary>언어를 바꾸고 저장한다(같으면 아무 일 없음).</summary>
    public static void Set(Lang lang)
    {
        Load();
        if (lang == s_current) return;
        s_current = lang;
        PlayerPrefs.SetInt(Key, (int)lang);
        PlayerPrefs.Save();
        NightDuty.Loc.Set((NightDuty.TextLang)(int)lang);   // 2026-11-22: 코어(수칙·지시·문자) 글도 같은 언어로 — 태블릿이 그 자리에서 다시 그린다
        if (Changed != null) Changed();
    }

    /// <summary>2026-11-22: 첫 씬 전에 저장된 언어를 코어(<see cref="NightDuty.Loc"/>)에 넣는다 — 코어는 Flow를 모른다.</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void PushToCore()
    {
        Load();
    }

    /// <summary>그 언어를 가리키는 칸의 글자(자기 언어로 — 「한국어」·「English」).</summary>
    public static string NativeName(Lang lang)
    {
        return lang == Lang.English ? "English" : "한국어";
    }

    /// <summary>메인 화면 언어 버튼 글자(「언어」 / 「Language」).</summary>
    public static string ButtonLabel
    {
        get { return IsEnglish ? "Language" : "언어"; }
    }

    private static readonly System.Collections.Generic.Dictionary<string, string> s_korean =
        new System.Collections.Generic.Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
        {
            // 메인 화면
            { "START", "입장" },
            { "OPTIONS", "설정" },
            { "QUIT", "종료" },
            { "Thanks for...", "도움주신 분들..." },
            // 설정 화면(일시정지·메인 OPTIONS 공용)
            { "LIGHT", "밝기" },
            { "MOUSE", "마우스 감도" },
            { "VOLUME", "소리" },
            { "LANGUAGE", "언어" },
            { "BACK", "복귀하기" },
            // 일시정지 첫 화면
            { "PAUSE", "일시정지" },
            { "RESUME", "계속하기" },
        };

    /// <summary>
    /// 영어 글자(화면에 원래 적힌 것)를 지금 언어로 바꾼다 — 한국어면 표에 있는 것만 한국어로, 영어거나 표에 없으면 그대로.
    /// 새 글자를 번역하려면 위 표에 한 줄 더하면 된다.
    /// </summary>
    public static string Localize(string english)
    {
        if (string.IsNullOrEmpty(english) || IsEnglish) return english;
        string ko;
        return s_korean.TryGetValue(english, out ko) ? ko : english;
    }

    /// <summary>
    /// 그 글자 모양(TMP)을 쓸 수 있는 글꼴로 맞춘다 — 한글이 없는 글꼴이면 한글이 있는 불러온 글꼴로 바꾼다
    /// (일시정지 화면 글꼴에는 「한국어」 글자가 없을 수 있다).
    /// </summary>
    public static void FitFont(TMP_Text label, string text)
    {
        if (label == null || string.IsNullOrEmpty(text)) return;

        // 처음 본 글꼴을 기억해 둔다 — 한글 글자는 한글용 글꼴, 영어로 돌아오면 원래 글꼴로.
        TMP_FontAsset original;
        if (!s_originalFont.TryGetValue(label, out original))
        {
            original = label.font;
            s_originalFont[label] = original;
        }

        // 처음 본 가로 배율도 기억 — 한글만 장평을 줄이고, 영어로 돌아오면 되돌린다.
        float baseX;
        if (!s_originalScaleX.TryGetValue(label, out baseX))
        {
            baseX = label.rectTransform.localScale.x;
            s_originalScaleX[label] = baseX;
        }

        TMP_FontAsset korean = KoreanFont;
        if (korean != null && HasHangul(text))
        {
            if (label.font != korean) label.font = korean;
            SetScaleX(label, baseX * KoreanWidthScale);
            return;
        }

        SetScaleX(label, baseX);
        if (original != null && label.font != original) label.font = original;
        TMP_FontAsset font = label.font;
        if (font != null && HasAll(font, text)) return;

        foreach (TMP_FontAsset f in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
        {
            if (f != null && HasAll(f, text))
            {
                label.font = f;
                return;
            }
        }
    }

    /// <summary>
    /// 복제한 글자(<paramref name="copy"/>)가 원본(<paramref name="source"/>)이 처음 가졌던 글꼴·가로 배율을 기억하게 한다.
    /// 2026-11-22 김진선님: 「영어 버전 메인의 OPTIONS만 폰트가 다르다」 — QUIT이 이미 한글 글꼴로 바뀐 뒤에 복제하면
    /// 복제본은 한글 글꼴을 원래 글꼴로 기억해, 영어로 돌아와도 그대로 남았다(가로도 0.95가 한 번 더 곱해졌다).
    /// 원본을 아직 본 적이 없으면 원본의 지금 값이 곧 처음 값이다.
    /// </summary>
    public static void InheritOriginal(TMP_Text source, TMP_Text copy)
    {
        if (source == null || copy == null) return;

        TMP_FontAsset font;
        if (!s_originalFont.TryGetValue(source, out font)) font = source.font;
        s_originalFont[copy] = font;

        float x;
        if (!s_originalScaleX.TryGetValue(source, out x)) x = source.rectTransform.localScale.x;
        s_originalScaleX[copy] = x;
    }

    private static readonly System.Collections.Generic.Dictionary<TMP_Text, TMP_FontAsset> s_originalFont =
        new System.Collections.Generic.Dictionary<TMP_Text, TMP_FontAsset>();

    private static readonly System.Collections.Generic.Dictionary<TMP_Text, float> s_originalScaleX =
        new System.Collections.Generic.Dictionary<TMP_Text, float>();

    /// <summary>한글 글자의 장평(가로 배율). 1이면 그대로, 작을수록 좁아진다.</summary>
    public const float KoreanWidthScale = 0.95f;

    private static void SetScaleX(TMP_Text label, float x)
    {
        Vector3 s = label.rectTransform.localScale;
        if (!Mathf.Approximately(s.x, x))
        {
            s.x = x;
            label.rectTransform.localScale = s;
        }
    }

    private static bool HasHangul(string text)
    {
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if ((c >= 0xAC00 && c <= 0xD7A3) || (c >= 0x3130 && c <= 0x318F)) return true;
        }

        return false;
    }

    /// <summary>한국어 글자에 쓰는 글꼴(<c>Resources/MainMenuOptionsLook</c>의 koreanFont). 없으면 null.</summary>
    public static TMP_FontAsset KoreanFont
    {
        get
        {
            MainMenuOptionsLook look = MainMenuOptionsLook.Load();
            return look != null ? look.koreanFont : null;
        }
    }

    private static bool HasAll(TMP_FontAsset font, string text)
    {
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '<' || c == '>' || c == '/') continue;   // 서식 태그(밑줄)가 들어갈 수 있다 — 태그 글자 u는 어느 글꼴에나 있다
            if (!font.HasCharacter(c, true, true)) return false;
        }

        return true;
    }

    private static void Load()
    {
        if (s_loaded) return;
        s_loaded = true;
        int raw = PlayerPrefs.GetInt(Key, (int)Default);
        s_current = raw == (int)Lang.English ? Lang.English : Lang.Korean;
        NightDuty.Loc.Set((NightDuty.TextLang)(int)s_current);
    }
}

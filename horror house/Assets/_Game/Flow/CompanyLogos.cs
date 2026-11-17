using UnityEngine;

/// <summary>
/// 회사 로고 스프라이트(Resources/Logos) — 시작 스플래시(<see cref="SplashLogo"/>)와 메인 화면 제작지원 팝업(<see cref="MainMenuCredits"/>)이 같이 쓴다.
/// <list type="bullet">
/// <item>NCAI: 어두운 바탕용(흰 글자) · 밝은 바탕용(검은 글자) 두 판.</item>
/// <item>하이테커: 팝업에서 흰 바탕 없이 얇은 테두리만 두르고 올린다(글자가 짙은 색이라 어두운 바탕에선 흐릴 수 있음).</item>
/// <item>Broken Compass: 시작 스플래시 전용(<see cref="BrokenCompass"/>).</item>
/// </list>
/// 이미지는 투명 여백을 잘라 둔 상태다(하이테커). 교체하려면 같은 이름으로 덮어쓴다.
/// </summary>
public static class CompanyLogos
{
    public const string NcaiOnDarkPath = "Logos/logo_ncai_white";
    public const string NcaiOnLightPath = "Logos/logo_ncai_dark";
    public const string HighTecherPath = "Logos/logo_hightecher";

    /// <summary>NCAI(어두운 바탕용, 흰 글자). 없으면 null.</summary>
    public static Sprite NcaiOnDark
    {
        get { return Load(NcaiOnDarkPath); }
    }

    /// <summary>NCAI(밝은 바탕용, 검은 글자). 없으면 null.</summary>
    public static Sprite NcaiOnLight
    {
        get { return Load(NcaiOnLightPath); }
    }

    /// <summary>하이테커. 없으면 null.</summary>
    public static Sprite HighTecher
    {
        get { return Load(HighTecherPath); }
    }

    /// <summary>Broken Compass(팀) 로고 — 시작 스플래시용. 이미지를 <c>Resources/Logos/logo_brokencompass</c>로 넣으면 쓰인다. 없으면 null(경고 없음).</summary>
    public static Sprite BrokenCompass
    {
        get { return Resources.Load<Sprite>(BrokenCompassPath); }
    }

    public const string BrokenCompassPath = "Logos/logo_brokencompass";

    private static Sprite Load(string path)
    {
        Sprite sprite = Resources.Load<Sprite>(path);
        if (sprite == null) Debug.LogWarning("[CompanyLogos] 로고를 찾지 못했습니다: Resources/" + path);
        return sprite;
    }

    /// <summary>스프라이트의 가로 세로 비(너비 ÷ 높이). 없으면 1.</summary>
    public static float Aspect(Sprite sprite)
    {
        return sprite == null || sprite.rect.height <= 0f ? 1f : sprite.rect.width / sprite.rect.height;
    }
}

using UnityEngine;

/// <summary>
/// 재시작 카드의 「YOU DIED」 모습(민 요청 2026-10-03: 축 상징 대신 사망 UI). 디자인 사망 화면 프리팹을 <b>참조만</b> 하고,
/// 카드를 띄울 때 그 안의 배경·로고 두 조각만 복제해 카드 뒤에 깐다(버튼·<c>HUDActions</c>는 쓰지 않는다 — 메인으로 가지 않고 그 밤을 다시 한다).
/// 프리팹을 디자이너가 고치면 카드도 따라 바뀐다. 비어 있거나 조각을 못 찾으면 카드는 옛 축 상징(귀·눈·발자국)으로 돌아간다.
/// 자리: <c>Resources/CaptureCardLook.asset</c>.
/// </summary>
[CreateAssetMenu(menuName = "야간근무/붙잡힘 카드 모습", fileName = "CaptureCardLook")]
public sealed class CaptureCardLook : ScriptableObject
{
    [Tooltip("사망 화면 프리팹(지금: 0. Main/01 Scene/HUD_Death_Design). 고치지 않고 조각만 복제한다.")]
    public GameObject deathScreen;

    [Tooltip("배경 조각의 자식 이름. 비우면 배경은 깔지 않는다.")]
    public string backgroundChild = "Backgrond";

    [Tooltip("「YOU DIED」 로고 조각의 자식 이름.")]
    public string logoChild = "Txt_Death";

    /// <summary>Resources에서 읽는다. 없으면 null.</summary>
    public static CaptureCardLook Load()
    {
        return Resources.Load<CaptureCardLook>("CaptureCardLook");
    }

    /// <summary>프리팹 안의 조각. 없으면 null.</summary>
    public GameObject Part(string childName)
    {
        if (deathScreen == null || string.IsNullOrEmpty(childName)) return null;
        Transform t = deathScreen.transform.Find(childName);
        return t != null ? t.gameObject : null;
    }
}

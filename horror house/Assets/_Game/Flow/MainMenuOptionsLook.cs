using UnityEngine;

/// <summary>
/// 메인 화면 OPTIONS가 띄우는 설정 화면(2026-10-29, 김진선님: 「그 옵션을 메인 화면에서도 조작 가능하고 인게임 적용 되게」).
/// 일시정지 화면 프리팹을 <b>참조만</b> 하고, 메인 화면에서 한 벌 세워 설정 화면(LIGHT · MOUSE · VOLUME · BACK)만 쓴다 —
/// 일시정지 화면을 디자이너가 고치면 메인 화면 설정도 따라 바뀐다. 자리: <c>Resources/MainMenuOptionsLook.asset</c>.
/// </summary>
[CreateAssetMenu(menuName = "야간근무/메인 화면 설정 화면", fileName = "MainMenuOptionsLook")]
public sealed class MainMenuOptionsLook : ScriptableObject
{
    [Tooltip("일시정지 화면 프리팹(지금: 0. Main/01 Scene/HUD_Pause_Design). 고치지 않고 한 벌 세워 설정 화면만 쓴다.")]
    public GameObject pauseScreen;

    /// <summary>Resources에서 읽는다. 없으면 null.</summary>
    public static MainMenuOptionsLook Load()
    {
        return Resources.Load<MainMenuOptionsLook>("MainMenuOptionsLook");
    }
}

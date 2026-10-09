using TMPro;
using UnityEngine;

/// <summary>
/// HUD에 GameTime의 현재 시각을 표시한다 (HUD_Play 프리팹에 붙어 있음).
/// 씬에 GameTime이 없으면 빈 문자열을 표시한다.
/// </summary>
public class GameTimeView : MonoBehaviour
{
    [SerializeField] private TMP_Text timeText;

    [Tooltip("비워 두면 씬에서 자동으로 찾는다.")]
    [SerializeField] private GameTime gameTime;

    private void OnEnable()
    {
        if (gameTime == null) gameTime = FindAnyObjectByType<GameTime>();
        if (gameTime == null)
        {
            Show(string.Empty);
            return;
        }

        gameTime.TimeTextChanged += Show;
        Show(gameTime.CurrentTimeText);
    }

    private void OnDisable()
    {
        if (gameTime != null) gameTime.TimeTextChanged -= Show;
    }

    private void Show(string text)
    {
        if (timeText != null) timeText.text = text ?? string.Empty;
    }
}

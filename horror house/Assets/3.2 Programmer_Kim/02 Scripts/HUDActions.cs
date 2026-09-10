using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class HUDActions : MonoBehaviour
{
    [Header("RESUME")]
    [SerializeField] private Button resumeButton;

    public event Action OnResumeClicked;

    void Awake()
    {
        if (resumeButton != null)
        {
            resumeButton.onClick.AddListener(() => OnResumeClicked?.Invoke());
        }
    }

    // 씬 전환 버튼의 OnClick()에서 직접 호출 (인스펙터에서 sceneName 파라미터 입력)
    public void LoadScene(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
    }

    // 종료 버튼의 OnClick()에서 직접 호출
    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}

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

    // 시작 버튼의 OnClick()에서 호출 — 계약서 씬으로 이동. 서명 후 출근하기를 누르면 로딩을 거쳐 Play 씬으로 간다
    public void StartGame()
    {
        GameSession.StartNewRun(); // Day 1부터 새로 시작
        SceneFlow.GoTo(GameScene.Contract, false);
    }

    // 사망 화면의 Restart 버튼 — 로딩을 거쳐 메인으로
    public void GoToMain()
    {
        SceneFlow.GoTo(GameScene.Main);
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

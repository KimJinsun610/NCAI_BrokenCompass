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

    // 시작 버튼의 OnClick()에서 호출 — 오프닝(있으면) → 계약서 → 로딩 → Play 씬 순서로 간다.
    // 오프닝 씬을 SceneFlowConfig에 지정하지 않았으면 예전처럼 계약서로 바로 간다.
    public void StartGame()
    {
        GameSession.StartNewRun(); // Day 1부터 새로 시작

        SceneFlowConfig config = SceneFlow.Config;
        bool hasOpening = config != null && config.HasOpening;
        SceneFlow.GoTo(hasOpening ? GameScene.Opening : GameScene.Contract, false);
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

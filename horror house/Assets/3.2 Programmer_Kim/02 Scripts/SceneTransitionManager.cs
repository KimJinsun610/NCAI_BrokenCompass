using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneTransitionManager : MonoBehaviour
{

    public void LoadScene(string sceneName)
    {
        Debug.Log($"[SceneTransitionManager] 버튼 클릭됨! 이동할 씬: {sceneName}");
        SceneManager.LoadScene(sceneName);
    }

    public void QuitGame()
    {
        Debug.Log("[SceneTransitionManager] 게임 종료");

    #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
    #else
        Application.Quit();
    #endif
    }

}

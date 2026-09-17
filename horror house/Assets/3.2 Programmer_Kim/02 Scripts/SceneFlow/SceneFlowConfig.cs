using UnityEngine;

/// <summary>
/// 게임 흐름에서 쓰는 씬 경로 표.
/// Resources 폴더의 "SceneFlowConfig" 에셋 하나만 사용하며, SceneFlow가 자동으로 불러온다.
/// Play 씬을 바꾸려면 이 에셋의 Play Scene 칸에 새 씬을 드래그하면 된다.
/// </summary>
[CreateAssetMenu(fileName = "SceneFlowConfig", menuName = "Programmer_Kim/Scene Flow Config")]
public class SceneFlowConfig : ScriptableObject
{
    [ScenePath, SerializeField] private string mainScene;
    [ScenePath, SerializeField] private string loadingScene;
    [ScenePath, SerializeField] private string playScene;
    [ScenePath, SerializeField, Tooltip("결과창 씬. 아직 없으면 비워 둔다.")]
    private string resultScene;

    public string GetPath(GameScene scene)
    {
        switch (scene)
        {
            case GameScene.Main: return mainScene;
            case GameScene.Loading: return loadingScene;
            case GameScene.Play: return playScene;
            case GameScene.Result: return resultScene;
            default: return null;
        }
    }
}

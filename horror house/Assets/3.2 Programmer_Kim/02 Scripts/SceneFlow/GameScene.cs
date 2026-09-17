/// <summary>
/// 게임 흐름에서 이동할 수 있는 씬 종류.
/// 실제 씬 파일은 SceneFlowConfig 에셋에서 지정한다 — 씬을 바꿔도 코드는 수정하지 않는다.
/// </summary>
public enum GameScene
{
    Main,
    Loading,
    Play,
    Result,
    Contract,
}

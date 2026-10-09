/// <summary>
/// 게임 흐름에서 이동할 수 있는 씬 종류.
/// 실제 씬 파일은 SceneFlowConfig 에셋에서 지정한다 — 씬을 바꿔도 코드는 수정하지 않는다.
/// </summary>
public enum GameScene
{
    Main,
    /// <summary>계약서 앞에 한 번 보여 주는 오프닝(프롤로그). 지정하지 않으면 건너뛴다.</summary>
    Opening,
    Loading,
    Play,
    Result,
    Contract,
}

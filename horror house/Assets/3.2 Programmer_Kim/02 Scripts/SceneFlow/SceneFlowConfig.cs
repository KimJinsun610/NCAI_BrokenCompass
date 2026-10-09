using System;
using UnityEngine;

/// <summary>
/// 게임 흐름에서 쓰는 씬 경로 표.
/// Resources 폴더의 "SceneFlowConfig" 에셋 하나만 사용하며, SceneFlow가 자동으로 불러온다.
/// Play 씬을 바꾸려면 이 에셋의 Play Scene 칸에 새 씬을 드래그하면 된다.
///
/// 씬별 배경음(BGM)·입장 소리와 UI 버튼 클릭음도 여기서 정한다 — SceneBgmPlayer·UiClickSound가 읽는다(씬 파일은 고치지 않는다).
/// Play 씬은 자체 앰비언스가 있어 BGM 칸이 없다(들어가면 BGM을 끈다).
/// </summary>
[CreateAssetMenu(fileName = "SceneFlowConfig", menuName = "Programmer_Kim/Scene Flow Config")]
public class SceneFlowConfig : ScriptableObject
{
    [ScenePath, SerializeField] private string mainScene;
    [ScenePath, SerializeField] private string loadingScene;
    [ScenePath, SerializeField] private string playScene;
    [ScenePath, SerializeField, Tooltip("결과창 씬. 아직 없으면 비워 둔다.")]
    private string resultScene;
    [ScenePath, SerializeField, Tooltip("메인의 시작 버튼 뒤, Day 1 전에 보여 주는 계약서 씬")]
    private string contractScene;
    [ScenePath, SerializeField, Tooltip("계약서 앞에 보여 주는 오프닝 씬. 비워 두면 시작 버튼이 계약서로 바로 간다.")]
    private string openingScene;

    [Header("BGM — 씬별 배경음 (Play 씬은 BGM 없음)")]
    [SerializeField] private SceneBgm mainBgm = new SceneBgm();
    [SerializeField] private SceneBgm loadingBgm = new SceneBgm { keepPrevious = true };
    [SerializeField] private SceneBgm contractBgm = new SceneBgm { keepPrevious = true };
    [SerializeField] private SceneBgm openingBgm = new SceneBgm { keepPrevious = true };
    [SerializeField] private SceneBgm resultBgm = new SceneBgm();
    [Tooltip("BGM이 꺼지거나 다른 곡으로 넘어갈 때 앞 곡이 줄어드는 시간(초)")]
    [SerializeField, Min(0f)] private float bgmFadeOut = 1.0f;

    [Header("UI 소리")]
    [Tooltip("모든 씬의 UI 버튼을 마우스로 누를 때(누를 수 있는 버튼만). 비우면 소리 없음.")]
    [SerializeField] private AudioClip buttonClick;
    [SerializeField, Range(0f, 1f)] private float buttonClickVolume = 0.8f;

    public float BgmFadeOut { get { return bgmFadeOut; } }
    public AudioClip ButtonClick { get { return buttonClick; } }
    public float ButtonClickVolume { get { return buttonClickVolume; } }

    public string GetPath(GameScene scene)
    {
        switch (scene)
        {
            case GameScene.Main: return mainScene;
            case GameScene.Loading: return loadingScene;
            case GameScene.Play: return playScene;
            case GameScene.Result: return resultScene;
            case GameScene.Contract: return contractScene;
            case GameScene.Opening: return openingScene;
            default: return null;
        }
    }

    /// <summary>그 씬의 BGM 설정. Play 씬·알 수 없는 씬은 null(= BGM 끔).</summary>
    public SceneBgm GetBgm(GameScene scene)
    {
        switch (scene)
        {
            case GameScene.Main: return mainBgm;
            case GameScene.Loading: return loadingBgm;
            case GameScene.Result: return resultBgm;
            case GameScene.Contract: return contractBgm;
            case GameScene.Opening: return openingBgm;
            default: return null;
        }
    }

    /// <summary>오프닝 씬이 지정돼 있는가. 비어 있으면 시작 버튼은 계약서로 바로 간다.</summary>
    public bool HasOpening { get { return !string.IsNullOrEmpty(openingScene); } }

    /// <summary>씬 경로로 어느 GameScene인지 찾는다. 표에 없으면 false.</summary>
    public bool TryGetScene(string scenePath, out GameScene scene)
    {
        foreach (GameScene s in Enum.GetValues(typeof(GameScene)))
        {
            string path = GetPath(s);
            if (!string.IsNullOrEmpty(path) && string.Equals(path, scenePath, StringComparison.OrdinalIgnoreCase))
            {
                scene = s;
                return true;
            }
        }
        scene = default;
        return false;
    }
}

/// <summary>
/// 한 씬의 배경음.
/// · 곡이 있으면 그 곡을 튼다(이미 같은 곡이 나오고 있으면 끊지 않고 이어서).
/// · 곡이 비어 있고 「이전 곡 이어서」가 켜져 있으면 앞 씬의 곡을 그대로 둔다(예: 메인 → 로딩 → 계약서).
/// · 곡이 비어 있고 「이전 곡 이어서」가 꺼져 있으면 BGM을 끈다.
/// </summary>
[Serializable]
public class SceneBgm
{
    [Tooltip("비우면 아래 「이전 곡 이어서」에 따라 앞 곡을 잇거나 끈다.")]
    public AudioClip clip;
    [Range(0f, 1f)] public float volume = 0.6f;
    public bool loop = true;
    [Tooltip("새 곡이 커지는 시간(초)")]
    [Min(0f)] public float fadeIn = 1.5f;
    [Tooltip("곡이 비어 있을 때 앞 씬의 곡을 계속 튼다.")]
    public bool keepPrevious;

    [Tooltip("이 씬에 들어올 때 한 번 내는 소리(예: 계약서 종이 넘김). 비우면 없음.")]
    public AudioClip enterSound;
    [Range(0f, 1f)] public float enterVolume = 0.9f;
}

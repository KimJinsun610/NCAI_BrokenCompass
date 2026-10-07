using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 씬별 배경음 재생기. 씬이 바뀌어도 살아 있는 오브젝트 하나가 SceneFlowConfig의 BGM 칸대로 곡을 바꾼다.
///
/// <para>· 씬 파일에 놓지 않아도 된다 — 첫 씬이 열릴 때 스스로 선다(DontDestroyOnLoad).
/// · 곡이 바뀌면 크로스페이드(앞 곡은 Config.BgmFadeOut, 새 곡은 그 씬의 fadeIn). 같은 곡이면 끊지 않고 볼륨만 맞춘다.
/// · Play 씬·표에 없는 씬에 들어가면 BGM을 끈다(Play 씬은 자체 앰비언스).
/// · 결과 씬처럼 AudioListener가 없는 씬에서는 자기 리스너를 켠다 — 리스너가 둘이면 경고가 나서, 씬에 있으면 끈다.
/// · 시간 정지(timeScale 0)와 상관없이 페이드한다(unscaled 시간).</para>
/// </summary>
[DisallowMultipleComponent]
public class SceneBgmPlayer : MonoBehaviour
{
    public static SceneBgmPlayer Instance { get; private set; }

    private AudioSource current;   // 지금 곡
    private AudioSource previous;  // 줄어드는 앞 곡
    private AudioListener ownListener;
    private Coroutine fadeRoutine;

    /// <summary>지금 나오는 곡(없으면 null).</summary>
    public AudioClip CurrentClip { get { return current != null && current.isPlaying ? current.clip : null; } }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatic()
    {
        Instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (Instance != null) return;
        var go = new GameObject("SceneBgmPlayer (auto)");
        DontDestroyOnLoad(go);
        go.AddComponent<SceneBgmPlayer>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        current = NewSource();
        previous = NewSource();
        ownListener = gameObject.AddComponent<AudioListener>();
        ownListener.enabled = false;

        SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);   // 첫 씬
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        Instance = null;
    }

    private AudioSource NewSource()
    {
        var src = gameObject.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.spatialBlend = 0f;   // 2D
        src.priority = 0;        // 배경음은 끊기지 않게
        return src;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single) return;
        EnsureListener();

        SceneFlowConfig config = SceneFlow.Config;
        SceneBgm bgm = null;
        if (config != null && config.TryGetScene(scene.path, out GameScene gameScene))
        {
            bgm = config.GetBgm(gameScene);   // Play 씬은 null
        }
        float fadeOut = config != null ? config.BgmFadeOut : 1f;

        if (bgm == null)
        {
            FadeTo(null, 0f, true, 0f, fadeOut);          // Play·표에 없는 씬 → 끔
        }
        else if (bgm.clip != null)
        {
            FadeTo(bgm.clip, bgm.volume, bgm.loop, bgm.fadeIn, fadeOut);
        }
        else if (!bgm.keepPrevious)
        {
            FadeTo(null, 0f, true, 0f, fadeOut);
        }
        // 곡이 비어 있고 「이전 곡 이어서」면 아무것도 안 바꾼다
    }

    /// <summary>씬에 켜진 AudioListener가 없을 때만 자기 리스너를 켠다.</summary>
    private void EnsureListener()
    {
        ownListener.enabled = false;
        foreach (AudioListener l in FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
        {
            if (l != ownListener && l.isActiveAndEnabled) return;
        }
        ownListener.enabled = true;
    }

    /// <summary>
    /// 곡을 바꾼다. clip이 null이면 끈다. 같은 곡이 이미 나오고 있으면 다시 틀지 않고 볼륨만 맞춘다.
    /// 코드에서 직접 부를 수도 있다(예: 연출 중 BGM 끄기).
    /// </summary>
    public void FadeTo(AudioClip clip, float volume, bool loop, float fadeIn, float fadeOut)
    {
        if (fadeRoutine != null) StopCoroutine(fadeRoutine);

        if (clip != null && current.isPlaying && current.clip == clip)
        {
            current.loop = loop;
            fadeRoutine = StartCoroutine(FadeVolume(current, volume, fadeIn));
            return;
        }

        // 지금 곡을 앞 곡 자리로 넘겨 줄이고, 새 곡을 지금 자리에서 키운다
        AudioSource old = current;
        current = previous;
        previous = old;

        current.Stop();
        if (clip != null)
        {
            current.clip = clip;
            current.loop = loop;
            current.volume = 0f;
            current.Play();
        }
        fadeRoutine = StartCoroutine(Crossfade(clip != null ? volume : 0f, fadeIn, fadeOut));
    }

    private IEnumerator Crossfade(float targetVolume, float fadeIn, float fadeOut)
    {
        float startOld = previous.volume;
        float t = 0f;
        float duration = Mathf.Max(fadeIn, fadeOut, 0.01f);
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            if (previous.isPlaying) previous.volume = fadeOut > 0f ? Mathf.Lerp(startOld, 0f, t / fadeOut) : 0f;
            if (current.isPlaying) current.volume = fadeIn > 0f ? Mathf.Lerp(0f, targetVolume, t / fadeIn) : targetVolume;
            yield return null;
        }
        previous.Stop();
        previous.clip = null;
        if (current.isPlaying) current.volume = targetVolume;
        fadeRoutine = null;
    }

    private IEnumerator FadeVolume(AudioSource src, float target, float duration)
    {
        float start = src.volume;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            src.volume = Mathf.Lerp(start, target, t / duration);
            yield return null;
        }
        src.volume = target;
        fadeRoutine = null;
    }
}

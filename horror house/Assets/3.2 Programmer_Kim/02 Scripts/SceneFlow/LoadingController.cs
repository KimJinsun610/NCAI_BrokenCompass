using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 로딩 씬 진행: 페이드 인 → 게이지 진행(최소 표시 시간 보장) → 페이드 아웃 → 목적지 씬 활성화.
/// 목적지는 SceneFlow.GoTo()가 넘겨준다. 로딩 씬을 단독 실행하면 SceneFlowConfig의 Play 씬으로 간다.
/// </summary>
public class LoadingController : MonoBehaviour
{
    // allowSceneActivation = false 상태에서 AsyncOperation.progress는 0.9에서 멈춘다
    private const float ActivationReadyProgress = 0.9f;

    [Header("UI")]
    [Tooltip("Image Type = Filled")]
    [SerializeField] private Image gaugeFill;
    [SerializeField] private TMP_Text tipText;
    [SerializeField] private Image background;
    [Tooltip("화면 전체를 덮는 검정 이미지. alpha 1 = 가림")]
    [SerializeField] private CanvasGroup fade;

    [Header("DATA")]
    [SerializeField] private LoadingTipTable tipTable;

    [Header("AUDIO (선택)")]
    [Tooltip("클립이 비어 있으면 재생하지 않는다.")]
    [SerializeField] private AudioSource bgmSource;

    [Header("TIMING")]
    [Tooltip("로딩이 빨라도 최소 이 시간(초) 동안 게이지를 채운다. 안내 문구를 읽을 시간.")]
    [SerializeField, Min(0f)] private float minDisplayTime = 3f;
    [SerializeField, Min(0.01f)] private float fadeDuration = 0.5f;
    [Tooltip("게이지가 목표값을 따라가는 속도 (초당 비율)")]
    [SerializeField, Min(0.1f)] private float gaugeFollowSpeed = 1.5f;

    private float bgmBaseVolume;

    private IEnumerator Start()
    {
        string targetPath = SceneFlow.ConsumePendingScene();
        if (string.IsNullOrEmpty(targetPath) && SceneFlow.Config != null)
        {
            // 로딩 씬을 단독으로 실행해 테스트하는 경우
            targetPath = SceneFlow.Config.GetPath(GameScene.Play);
        }

        if (string.IsNullOrEmpty(targetPath))
        {
            Debug.LogError("[LoadingController] 이동할 씬이 없습니다. SceneFlowConfig를 확인하세요.");
            yield break;
        }

        ApplyTipAndBackground();
        SetGauge(0f);
        SetFade(1f);
        PlayBgm();

        AsyncOperation op = SceneManager.LoadSceneAsync(targetPath);
        if (op == null)
        {
            Debug.LogError($"[LoadingController] '{targetPath}' 로드에 실패했습니다. Build Settings를 확인하세요.");
            yield break;
        }
        op.allowSceneActivation = false;

        yield return Fade(1f, 0f, false);

        // 일시정지 후 넘어온 경우에도 멈추지 않도록 unscaled 시간 사용
        float elapsed = 0f;
        float shown = 0f;
        while (shown < 1f)
        {
            float dt = Time.unscaledDeltaTime;
            elapsed += dt;

            float realProgress = Mathf.Clamp01(op.progress / ActivationReadyProgress);
            float timeGate = minDisplayTime > 0f ? Mathf.Clamp01(elapsed / minDisplayTime) : 1f;

            // 게이지는 뒤로 가지 않고, 실제 진행도와 최소 표시 시간 중 느린 쪽을 따라간다
            shown = Mathf.MoveTowards(shown, Mathf.Min(realProgress, timeGate), gaugeFollowSpeed * dt);
            SetGauge(shown);
            yield return null;
        }

        // 활성화 순간 다음 씬의 Awake/Start 때문에 멈칫하는 구간을 검정 화면으로 가린다
        yield return Fade(0f, 1f, true);
        op.allowSceneActivation = true;
    }

    private void ApplyTipAndBackground()
    {
        if (tipTable == null) return;

        if (tipText != null)
        {
            tipText.text = tipTable.PickTip() ?? string.Empty;
        }

        if (background != null)
        {
            Sprite sprite = tipTable.PickBackground();
            if (sprite != null)
            {
                background.sprite = sprite;
                background.color = Color.white;

                AspectRatioFitter fitter = background.GetComponent<AspectRatioFitter>();
                if (fitter != null && sprite.rect.height > 0f)
                {
                    fitter.aspectRatio = sprite.rect.width / sprite.rect.height;
                }
            }
        }
    }

    private void PlayBgm()
    {
        if (bgmSource == null || bgmSource.clip == null) return;

        bgmBaseVolume = bgmSource.volume;
        bgmSource.Play();
    }

    private IEnumerator Fade(float from, float to, bool fadeOutBgm)
    {
        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            float alpha = Mathf.Lerp(from, to, t / fadeDuration);
            SetFade(alpha);

            if (fadeOutBgm && bgmSource != null && bgmSource.isPlaying)
            {
                bgmSource.volume = bgmBaseVolume * (1f - alpha);
            }
            yield return null;
        }
        SetFade(to);
    }

    private void SetGauge(float value)
    {
        if (gaugeFill != null) gaugeFill.fillAmount = value;
    }

    private void SetFade(float alpha)
    {
        if (fade != null) fade.alpha = alpha;
    }
}

using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 오프닝(프롤로그) 씬: 그림·자막을 한 장씩 넘겨 보이고 끝나면 계약서로 간다.
///
/// <para>슬라이드가 비어 있으면 <b>기다리지 않고 바로</b> 계약서로 넘긴다 —
/// 틀만 끼워 둔 상태에서 검은 화면에 갇히지 않게.</para>
///
/// <para>연출을 타임라인이나 다른 스크립트로 만들 거라면 이 컴포넌트는 쓰지 않고
/// 그쪽 끝에서 <see cref="OpeningExit.ToContract"/>만 부르면 된다.</para>
/// </summary>
public class OpeningController : MonoBehaviour
{
    [Header("장면")]
    [Tooltip("위에서 아래 순서로 보여 준다. 비우면 곧바로 계약서로 넘어간다.")]
    [SerializeField] private OpeningSlide[] slides = new OpeningSlide[0];

    [Header("화면")]
    [SerializeField] private Image image;
    [SerializeField] private TMP_Text caption;
    [Tooltip("화면 전체를 덮는 검은 판. 장면이 바뀔 때 이것으로 암전한다.")]
    [SerializeField] private Image fade;
    [Tooltip("\"아무 키나 눌러 건너뛰기\" 안내. 첫 입력이 들어오면 숨는다.")]
    [SerializeField] private GameObject skipHint;

    [Header("시간")]
    [Tooltip("장면이 나타나고 사라지는 데 걸리는 시간(초).")]
    [Min(0f)] [SerializeField] private float fadeSeconds = 0.6f;
    [Tooltip("첫 장면 앞의 검은 화면(초).")]
    [Min(0f)] [SerializeField] private float leadSeconds = 0.8f;
    [Tooltip("마지막 장면 뒤 계약서로 넘어가기 전의 검은 화면(초).")]
    [Min(0f)] [SerializeField] private float tailSeconds = 0.8f;

    [Header("건너뛰기")]
    [Tooltip("켜면 아무 키·클릭으로 오프닝을 통째로 건너뛴다.")]
    [SerializeField] private bool allowSkip = true;
    [Tooltip("씬이 열린 직후 이 시간(초) 동안은 건너뛰기를 받지 않는다 — 메뉴에서 누른 키가 그대로 넘어가는 것을 막는다.")]
    [Min(0f)] [SerializeField] private float skipLockSeconds = 0.5f;

    private AudioSource _voice;
    private bool _skipped;
    private float _openedAt;

    private void Start()
    {
        _openedAt = Time.unscaledTime;

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.None;

        if (skipHint != null) skipHint.SetActive(allowSkip);

        _voice = GetComponent<AudioSource>();
        if (_voice == null) _voice = gameObject.AddComponent<AudioSource>();
        _voice.playOnAwake = false;
        _voice.spatialBlend = 0f;

        StartCoroutine(Run());
    }

    private void Update()
    {
        if (!allowSkip || _skipped) return;
        if (Time.unscaledTime - _openedAt < skipLockSeconds) return;
        if (!Input.anyKeyDown) return;

        _skipped = true;
        if (skipHint != null) skipHint.SetActive(false);
        if (_voice != null) _voice.Stop();
        StopAllCoroutines();
        StartCoroutine(Leave());
    }

    private IEnumerator Run()
    {
        SetFade(1f);
        SetImage(null);
        SetCaption(null);

        if (slides == null || slides.Length == 0)
        {
            // 아직 장면을 안 넣었다 — 검은 화면에 갇히지 않게 그냥 넘긴다.
            OpeningExit.ToContract();
            yield break;
        }

        if (leadSeconds > 0f) yield return new WaitForSecondsRealtime(leadSeconds);

        for (int i = 0; i < slides.Length; i++)
        {
            OpeningSlide s = slides[i];
            if (s == null) continue;

            SetImage(s.image);
            SetCaption(s.caption);

            if (s.voice != null && _voice != null)
            {
                _voice.clip = s.voice;
                _voice.Play();
            }

            yield return Fade(1f, 0f, fadeSeconds);

            float hold = s.hold;
            if (s.voice != null) hold = Mathf.Max(hold, s.voice.length);
            if (hold > 0f) yield return new WaitForSecondsRealtime(hold);

            yield return Fade(0f, 1f, fadeSeconds);

            if (skipHint != null && i == 0) skipHint.SetActive(false);
        }

        yield return Leave();
    }

    private IEnumerator Leave()
    {
        SetImage(null);
        SetCaption(null);
        yield return Fade(GetFade(), 1f, fadeSeconds);
        if (tailSeconds > 0f) yield return new WaitForSecondsRealtime(tailSeconds);
        OpeningExit.ToContract();
    }

    private IEnumerator Fade(float from, float to, float seconds)
    {
        if (fade == null || seconds <= 0f)
        {
            SetFade(to);
            yield break;
        }

        float t = 0f;
        while (t < seconds)
        {
            t += Time.unscaledDeltaTime;
            SetFade(Mathf.Lerp(from, to, Mathf.Clamp01(t / seconds)));
            yield return null;
        }
        SetFade(to);
    }

    private void SetFade(float a)
    {
        if (fade == null) return;
        Color c = fade.color;
        c.a = a;
        fade.color = c;
    }

    private float GetFade()
    {
        return fade != null ? fade.color.a : 1f;
    }

    private void SetImage(Sprite sprite)
    {
        if (image == null) return;
        image.sprite = sprite;
        image.enabled = sprite != null;
    }

    private void SetCaption(string text)
    {
        if (caption == null) return;
        caption.text = text != null ? text : string.Empty;
        caption.gameObject.SetActive(!string.IsNullOrEmpty(text));
    }
}

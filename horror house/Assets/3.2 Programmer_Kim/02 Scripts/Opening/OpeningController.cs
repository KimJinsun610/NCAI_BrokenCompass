using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 오프닝(프롤로그) 씬: 나레이션에 맞춰 그림·자막을 한 장씩 넘기고 끝나면 계약서로 간다.
///
/// <para>그림이 <b>바뀌는 순간에만</b> 지직거리는 전환(색분리 · 가로 찢김 · 잡음)을 넣는다 —
/// 같은 그림으로 자막만 넘어갈 때는 조용히 지나간다.</para>
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
    [Tooltip("색분리용 잔상 둘(붉은 쪽 · 푸른 쪽). 평소에는 보이지 않는다.")]
    [SerializeField] private Image ghostA;
    [SerializeField] private Image ghostB;
    [Tooltip("지직거릴 때 겹치는 잡음 판. 비워 두면 잡음 없이 찢김만 보인다.")]
    [SerializeField] private RawImage noise;
    [SerializeField] private TMP_Text caption;
    [Tooltip("화면 전체를 덮는 검은 판. 처음과 끝의 암전에 쓴다.")]
    [SerializeField] private Image fade;
    [Tooltip("\"[F] 눌러서 건너뛰기\" 안내 묶음. 처음에 옅게 떠 있다 사라지고, F를 누르면 다시 나온다.")]
    [SerializeField] private CanvasGroup skipHint;
    [Tooltip("길게 누르는 동안 차오르는 막대(Image Type = Filled).")]
    [SerializeField] private Image skipGauge;

    [Header("시간")]
    [Tooltip("처음 나타나고 마지막에 사라지는 데 걸리는 시간(초).")]
    [Min(0f)] [SerializeField] private float fadeSeconds = 0.8f;
    [Tooltip("첫 장면 앞의 검은 화면(초).")]
    [Min(0f)] [SerializeField] private float leadSeconds = 1.0f;
    [Tooltip("마지막 장면 뒤 계약서로 넘어가기 전의 검은 화면(초).")]
    [Min(0f)] [SerializeField] private float tailSeconds = 1.2f;
    [Tooltip("나레이션이 끝난 뒤 다음 장면까지 쉬는 시간(초).")]
    [Min(0f)] [SerializeField] private float afterVoiceSeconds = 1.1f;
    [Tooltip("나레이션 재생 속도. 1보다 크면 빨라진다(Unity는 속도와 소리 높이가 함께 바뀐다). 문장이 머무는 시간도 이 값으로 나눈다.")]
    [Range(0.5f, 2.5f)] [SerializeField] private float voiceSpeed = 1.5f;

    [Header("지직거림")]
    [Tooltip("그림이 바뀔 때 지직거리는 시간(초). 0이면 그냥 바뀐다.")]
    [Min(0f)] [SerializeField] private float glitchSeconds = 0.45f;
    [Tooltip("가로로 찢겨 흔들리는 폭(1920 기준 픽셀).")]
    [Min(0f)] [SerializeField] private float glitchShift = 28f;
    [Tooltip("색분리 잔상의 진하기(0~1).")]
    [Range(0f, 1f)] [SerializeField] private float glitchSplit = 0.28f;
    [Tooltip("잡음 판의 진하기(0~1).")]
    [Range(0f, 1f)] [SerializeField] private float glitchNoise = 0.3f;

    [Header("건너뛰기")]
    [Tooltip("켜면 이 키를 길게 눌러 오프닝을 통째로 건너뛴다.")]
    [SerializeField] private bool allowSkip = true;
    [SerializeField] private KeyCode skipKey = KeyCode.F;
    [Tooltip("이만큼 누르고 있어야 건너뛴다(초).")]
    [Min(0.1f)] [SerializeField] private float skipHoldSeconds = 1.2f;
    [Tooltip("씬이 열린 직후 이 시간(초) 동안은 건너뛰기를 받지 않는다 — 메뉴에서 누른 키가 그대로 넘어가는 것을 막는다.")]
    [Min(0f)] [SerializeField] private float skipLockSeconds = 0.5f;
    [Tooltip("안내가 평소에 떠 있는 진하기(0~1). 누르고 있으면 1까지 올라간다.")]
    [Range(0f, 1f)] [SerializeField] private float hintIdleAlpha = 0.25f;
    [Tooltip("F를 누르지 않으면 이 시간(초) 뒤에 안내가 사라진다.")]
    [Min(0f)] [SerializeField] private float hintVisibleSeconds = 6f;
    [Tooltip("안내가 나타나고 사라지는 데 걸리는 시간(초).")]
    [Min(0f)] [SerializeField] private float hintFadeSeconds = 0.5f;

    private AudioSource _voice;
    private Texture2D _noiseTex;
    private bool _skipped;
    private float _openedAt;
    private Sprite _shown;
    private float _held;        // 지금까지 누르고 있은 시간
    private float _hintAge;     // 마지막으로 F를 누른 뒤 흐른 시간
    private bool _wasHeld;      // 지난 프레임에 눌려 있었는가(누르는 순간을 잡는다)

    private void Start()
    {
        _openedAt = Time.unscaledTime;

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.None;

        if (skipHint != null)
        {
            skipHint.gameObject.SetActive(allowSkip);
            skipHint.alpha = allowSkip ? hintIdleAlpha : 0f;   // 처음에는 옅게 떠 있다 사라진다
        }
        if (skipGauge != null)
        {
            skipGauge.fillAmount = 0f;
            skipGauge.enabled = false;
            FitGaugeToHint();
        }

        _voice = GetComponent<AudioSource>();
        if (_voice == null) _voice = gameObject.AddComponent<AudioSource>();
        _voice.playOnAwake = false;
        _voice.spatialBlend = 0f;
        _voice.pitch = voiceSpeed;

        SetupNoise();
        StartCoroutine(Run());
    }

    private void OnDestroy()
    {
        if (_noiseTex != null) Destroy(_noiseTex);
    }

    private void Update()
    {
        if (!allowSkip || _skipped) return;

        float dt = Time.unscaledDeltaTime;
        bool locked = Time.unscaledTime - _openedAt < skipLockSeconds;
        bool down = SkipKeyHeld();
        bool holding = !locked && down;

        // F를 누르는 순간 안내가 다시 떠오른다.
        if (holding && !_wasHeld) _hintAge = 0f;
        _wasHeld = down;

        if (holding)
        {
            _held += dt;
            _hintAge = 0f;
            if (_held >= skipHoldSeconds)
            {
                Skip();
                return;
            }
        }
        else
        {
            // 떼면 게이지가 빠르게 줄어든다.
            _held = Mathf.MoveTowards(_held, 0f, dt * skipHoldSeconds * 2.5f);
            _hintAge += dt;
        }

        DrawSkipHint(holding, dt);
    }

    /// <summary>
    /// 게이지 막대를 안내 글자와 같은 폭으로 줄인다 — 차오르는 시간은 그대로라 그만큼 천천히 찬다.
    /// 글자 폭은 글꼴 아틀라스가 준비된 실행 중에 재야 정확하다(에디터에서 재면 칸 크기가 그대로 나온다).
    /// </summary>
    private void FitGaugeToHint()
    {
        if (skipHint == null || skipGauge == null) return;

        TMP_Text label = skipHint.GetComponentInChildren<TMP_Text>(true);
        RectTransform track = skipGauge.transform.parent as RectTransform;
        if (label == null || track == null) return;

        label.ForceMeshUpdate();
        float width = label.preferredWidth;
        if (width <= 1f) return;

        track.sizeDelta = new Vector2(width, track.sizeDelta.y);
    }

    /// <summary>
    /// 건너뛰기 키가 눌려 있는가. 옛 입력(Input Manager)과 새 입력(Input System)을 모두 본다 —
    /// 프로젝트 입력 설정이 바뀌어도 건너뛰기가 죽지 않게.
    /// </summary>
    private bool SkipKeyHeld()
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKey(skipKey)) return true;
#endif
#if ENABLE_INPUT_SYSTEM
        UnityEngine.InputSystem.Keyboard board = UnityEngine.InputSystem.Keyboard.current;
        if (board != null)
        {
            string want = skipKey.ToString();
            foreach (UnityEngine.InputSystem.Controls.KeyControl key in board.allKeys)
            {
                if (key.keyCode.ToString() == want) return key.isPressed;
            }
        }
#endif
        return false;
    }

    /// <summary>안내의 진하기와 게이지를 그린다. 누르고 있으면 또렷하게, 가만히 두면 사라진다.</summary>
    private void DrawSkipHint(bool holding, float dt)
    {
        if (skipHint != null)
        {
            float target = holding ? 1f : (_hintAge < hintVisibleSeconds ? hintIdleAlpha : 0f);
            float speed = hintFadeSeconds > 0f ? dt / hintFadeSeconds : 1f;
            skipHint.alpha = Mathf.MoveTowards(skipHint.alpha, target, speed);
        }

        if (skipGauge != null)
        {
            float fill = Mathf.Clamp01(_held / skipHoldSeconds);
            skipGauge.fillAmount = fill;
            skipGauge.enabled = fill > 0.001f;
        }
    }

    private void Skip()
    {
        _skipped = true;
        if (skipGauge != null) skipGauge.fillAmount = 1f;
        if (skipHint != null) skipHint.alpha = 0f;
        if (_voice != null) _voice.Stop();
        StopAllCoroutines();
        StartCoroutine(Leave());
    }

    private IEnumerator Run()
    {
        SetFade(1f);
        SetImage(null);
        SetCaption(null);
        HideGlitch();

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

            bool first = i == 0;
            bool imageChanged = s.image != _shown;

            if (first)
            {
                // 첫 장면은 검은 화면에서 서서히 떠오른다.
                SetImage(s.image);
                SetCaption(s.caption);
                PlayVoice(s.voice);
                yield return Fade(1f, 0f, fadeSeconds);
                _hintAge = 0f;   // 암전이 걷힌 뒤부터 안내가 떠 있는 시간을 센다
            }
            else if (imageChanged && glitchSeconds > 0f)
            {
                // 그림이 바뀌는 순간에만 지직거린다. 자막·소리는 전환 한가운데에서 바뀐다.
                yield return Glitch(s);
            }
            else
            {
                SetImage(s.image);
                SetCaption(s.caption);
                PlayVoice(s.voice);
            }

            float hold = s.hold;
            if (s.voice != null) hold = s.voice.length / Mathf.Max(0.1f, voiceSpeed) + afterVoiceSeconds;
            if (hold > 0f) yield return new WaitForSecondsRealtime(hold);
        }

        yield return Leave();
    }

    /// <summary>그림이 바뀌는 지직거림. 가운데에서 그림·자막·소리가 한꺼번에 넘어간다.</summary>
    private IEnumerator Glitch(OpeningSlide next)
    {
        Sprite from = _shown;
        bool swapped = false;
        float t = 0f;

        while (t < glitchSeconds)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / glitchSeconds);

            if (!swapped && p >= 0.45f)
            {
                swapped = true;
                SetImage(next.image);
                SetCaption(next.caption);
                PlayVoice(next.voice);
            }

            // 가운데가 가장 심하게 흔들린다.
            float power = Mathf.Sin(p * Mathf.PI);

            // 신호가 끊긴 것처럼 두 그림이 섞여 깜빡인다.
            if (image != null && from != null && next.image != null)
            {
                bool flicker = Random.value < 0.18f * power;
                image.sprite = flicker ? (swapped ? from : next.image) : (swapped ? next.image : from);
                image.enabled = image.sprite != null;
            }

            Shake(image, power);
            Split(power);
            Noise(power);

            yield return null;
        }

        if (!swapped)
        {
            SetImage(next.image);
            SetCaption(next.caption);
            PlayVoice(next.voice);
        }
        else
        {
            SetImage(next.image);
        }
        HideGlitch();
    }

    private void Shake(Graphic target, float power)
    {
        if (target == null) return;
        float x = Random.Range(-glitchShift, glitchShift) * power;
        float y = Random.Range(-glitchShift, glitchShift) * power * 0.25f;
        target.rectTransform.anchoredPosition = new Vector2(x, y);
    }

    private void Split(float power)
    {
        float a = glitchSplit * power;
        SetGhost(ghostA, new Color(1f, 0.25f, 0.25f, a), Random.Range(6f, 22f) * power);
        SetGhost(ghostB, new Color(0.3f, 0.7f, 1f, a), -Random.Range(6f, 22f) * power);
    }

    private void SetGhost(Image ghost, Color color, float offsetX)
    {
        if (ghost == null) return;
        ghost.sprite = image != null ? image.sprite : null;
        ghost.enabled = ghost.sprite != null && color.a > 0.001f;
        ghost.color = color;
        ghost.rectTransform.anchoredPosition = new Vector2(offsetX, 0f);
    }

    private void Noise(float power)
    {
        if (noise == null) return;
        float a = glitchNoise * power * Random.Range(0.4f, 1f);
        noise.color = new Color(1f, 1f, 1f, a);
        noise.enabled = a > 0.001f;
        // 잡음 판을 매 프레임 다른 곳에서 잘라 쓴다 — 텍스처 한 장으로 흐르는 잡음이 된다.
        noise.uvRect = new Rect(Random.value, Random.value, Random.Range(2.5f, 5f), Random.Range(2.5f, 5f));
    }

    private void HideGlitch()
    {
        if (image != null) image.rectTransform.anchoredPosition = Vector2.zero;
        if (ghostA != null) ghostA.enabled = false;
        if (ghostB != null) ghostB.enabled = false;
        if (noise != null) noise.enabled = false;
    }

    private void SetupNoise()
    {
        if (noise == null) return;

        const int size = 128;
        _noiseTex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Point
        };
        Color32[] pixels = new Color32[size * size];
        for (int i = 0; i < pixels.Length; i++)
        {
            byte v = (byte)Random.Range(0, 256);
            // 가로줄이 살짝 보이게 — CRT 주사선 느낌.
            if ((i / size) % 3 == 0) v = (byte)(v * 0.45f);
            pixels[i] = new Color32(v, v, v, (byte)Random.Range(40, 255));
        }
        _noiseTex.SetPixels32(pixels);
        _noiseTex.Apply(false);

        noise.texture = _noiseTex;
        noise.enabled = false;
    }

    private void PlayVoice(AudioClip clip)
    {
        if (_voice == null) return;
        if (clip == null) { _voice.Stop(); return; }
        _voice.clip = clip;
        _voice.pitch = voiceSpeed;
        _voice.Play();
    }

    private IEnumerator Leave()
    {
        HideGlitch();
        yield return Fade(GetFade(), 1f, fadeSeconds);
        SetImage(null);
        SetCaption(null);
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
        _shown = sprite;
        if (image == null) return;
        image.sprite = sprite;
        image.enabled = sprite != null;
        image.color = Color.white;
    }

    private void SetCaption(string text)
    {
        if (caption == null) return;
        caption.text = text != null ? text : string.Empty;
        caption.gameObject.SetActive(!string.IsNullOrEmpty(text));
    }
}

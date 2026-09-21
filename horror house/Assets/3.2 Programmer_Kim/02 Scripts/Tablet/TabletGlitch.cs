using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 태블릿 화면이 디지털 기기처럼 지직거리게 한다.
///
/// <b>강도 하나(0~1)로만 조절한다.</b> 무엇이 그 값을 올릴지는 이 스크립트가 알지 않는다.
/// 나중에 기획이 정해지면 <see cref="SetIntensity"/>를 한 줄 부르기만 하면 된다.
///   예) tabletGlitch.SetIntensity(0.7f);
///
/// 강도가 올라가면 같이 심해지는 것:
///   화면 바탕 — 주사선 · 잡음 · 가로 찢김 · 깜빡임 (Custom/TabletScreen 셰이더)
///   글자     — 줄 단위로 좌우로 밀림, 심하면 글자가 깨져 보임
///
/// 주의: 기획 정본상 <b>신뢰 수치에 따라 태블릿을 왜곡하는 안은 폐기</b>됐다(2026-09-17).
/// 강도를 연결할 때 그 점을 확인할 것.
/// </summary>
[DisallowMultipleComponent]
public class TabletGlitch : MonoBehaviour
{
    [Header("강도")]
    [Tooltip("0 = 멀쩡한 화면, 1 = 심하게 지직거림. 코드에서 SetIntensity로 바꾼다.")]
    [Range(0f, 1f)] public float intensity = 0f;
    [Tooltip("강도가 바뀔 때 곧바로 바뀌지 않고 이 시간에 걸쳐 따라간다(초).")]
    public float blendSeconds = 0.6f;

    [Header("화면 바탕")]
    [Tooltip("Custom/TabletScreen 셰이더를 쓰는 배경 렌더러. 비우면 자식에서 찾는다.")]
    public Renderer screenRenderer;

    [Header("글자")]
    public List<TMP_Text> texts = new List<TMP_Text>();
    [Tooltip("글자가 줄 단위로 밀리는 최대 거리(화면 폭 기준 비율). 0.12면 화면 폭의 12%까지 밀린다.")]
    [Range(0f, 0.4f)] public float textShift = 0.12f;
    [Tooltip("글자가 흔들리는 빈도. 클수록 자주 바뀐다.")]
    public float textShiftSpeed = 12f;
    [Tooltip("강도가 이 값을 넘으면 글자가 깨져 보이기 시작한다.")]
    [Range(0f, 1f)] public float corruptionFrom = 0.55f;
    [Tooltip("깨진 글자로 바꿀 후보. 여기서 무작위로 고른다.")]
    public string corruptionChars = "▓▒░#%&@*/\\|=+<>";

    /// <summary>현재 실제로 적용 중인 강도(블렌딩된 값).</summary>
    public float Current { get { return _current; } }

    private static readonly int GlitchId = Shader.PropertyToID("_Glitch");

    private MaterialPropertyBlock _block;
    private float _current;
    private float _velocity;
    private float _seed;

    // 글자 깨짐을 되돌리기 위해 원래 문장을 들고 있는다.
    private readonly Dictionary<TMP_Text, string> _cleanText = new Dictionary<TMP_Text, string>();
    private float _nextCorruptionTime;
    private float _clock;   // 일시정지 중에는 멈추는 자체 시계

    private void Awake()
    {
        _block = new MaterialPropertyBlock();
        _seed = Random.value * 100f;

        if (screenRenderer == null)
        {
            Transform bg = transform.Find("BG");
            if (bg != null) screenRenderer = bg.GetComponent<Renderer>();
        }

        if (texts.Count == 0) texts.AddRange(GetComponentsInChildren<TMP_Text>(true));
    }

    /// <summary>바깥에서 강도를 지정한다. 0~1로 잘린다.</summary>
    public void SetIntensity(float value)
    {
        intensity = Mathf.Clamp01(value);
    }

    /// <summary>블렌딩 없이 즉시 적용한다. 연출상 갑자기 튀어야 할 때 쓴다.</summary>
    public void SetIntensityImmediate(float value)
    {
        intensity = Mathf.Clamp01(value);
        _current = intensity;
        _velocity = 0f;
        ApplyToScreen();
    }

    private void LateUpdate()
    {
        // 일시정지 중에는 화면도 그 순간에 멈춘다.
        float dt = ViewmodelTime.Delta;
        if (dt <= 0f) return;
        _clock += dt;
        _current = Mathf.SmoothDamp(_current, intensity, ref _velocity, Mathf.Max(0.01f, blendSeconds), Mathf.Infinity, dt);
        if (_current < 0.001f && intensity <= 0f) _current = 0f;

        ApplyToScreen();
        ApplyToTexts();
    }

    private void ApplyToScreen()
    {
        if (screenRenderer == null) return;

        // Awake보다 먼저 불릴 수 있다(알람이 화면을 켜는 순간 등). 없으면 그때 만든다.
        if (_block == null) _block = new MaterialPropertyBlock();

        // 재질을 복제하지 않고 이 렌더러에만 값을 덮어쓴다(다른 화면과 재질을 공유해도 안전).
        screenRenderer.GetPropertyBlock(_block);
        _block.SetFloat(GlitchId, _current);
        screenRenderer.SetPropertyBlock(_block);
    }

    private void ApplyToTexts()
    {
        bool corrupt = _current > corruptionFrom && !string.IsNullOrEmpty(corruptionChars);
        UpdateCorruption(corrupt);

        if (_current <= 0.001f) return;

        for (int i = 0; i < texts.Count; i++)
        {
            ShiftLines(texts[i], i);
        }
    }

    /// <summary>글자를 줄 단위로 좌우로 민다. 띠 몇 개만 밀어야 '찢긴' 것처럼 보인다.</summary>
    private void ShiftLines(TMP_Text text, int textIndex)
    {
        if (text == null || !text.gameObject.activeInHierarchy) return;

        text.ForceMeshUpdate();
        TMP_TextInfo info = text.textInfo;
        if (info.characterCount == 0) return;

        float step = Mathf.Floor(_clock * textShiftSpeed);
        // 글꼴 크기가 아니라 글상자 폭을 기준으로 민다. 그래야 글자가 화면 밖으로 튀어나가지 않는다.
        float maxShift = text.rectTransform.rect.width * textShift;

        for (int i = 0; i < info.characterCount; i++)
        {
            TMP_CharacterInfo character = info.characterInfo[i];
            if (!character.isVisible) continue;

            // 같은 줄은 같은 값만큼 민다.
            float bandRandom = Hash(character.lineNumber * 7.13f + step + _seed + textIndex * 3.7f);
            // 대부분의 줄은 그대로 두고 일부만 크게 민다.
            if (bandRandom < 1f - _current * 0.45f) continue;

            float offset = (Hash(bandRandom * 91.7f) - 0.5f) * 2f * maxShift * _current;

            int materialIndex = character.materialReferenceIndex;
            int vertexIndex = character.vertexIndex;
            Vector3[] vertices = info.meshInfo[materialIndex].vertices;

            for (int v = 0; v < 4; v++)
            {
                vertices[vertexIndex + v].x += offset;
            }
        }

        for (int i = 0; i < info.meshInfo.Length; i++)
        {
            info.meshInfo[i].mesh.vertices = info.meshInfo[i].vertices;
            text.UpdateGeometry(info.meshInfo[i].mesh, i);
        }
    }

    /// <summary>강도가 높을 때 글자 일부를 다른 기호로 바꿔 '깨진 화면'처럼 보이게 한다.</summary>
    private void UpdateCorruption(bool corrupt)
    {
        if (!corrupt)
        {
            RestoreCleanText();
            return;
        }

        if (_clock < _nextCorruptionTime) return;
        _nextCorruptionTime = _clock + Random.Range(0.05f, 0.18f);

        for (int i = 0; i < texts.Count; i++)
        {
            TMP_Text text = texts[i];
            if (text == null) continue;

            string clean;
            if (!_cleanText.TryGetValue(text, out clean))
            {
                clean = text.text;
                _cleanText[text] = clean;
            }

            // 강도가 셀수록 더 많은 글자를 바꾼다. 최대 15%.
            float ratio = Mathf.InverseLerp(corruptionFrom, 1f, _current) * 0.15f;
            char[] buffer = clean.ToCharArray();
            int count = Mathf.RoundToInt(buffer.Length * ratio);

            for (int c = 0; c < count; c++)
            {
                int index = Random.Range(0, buffer.Length);
                if (buffer[index] == '\n' || buffer[index] == ' ') continue;
                buffer[index] = corruptionChars[Random.Range(0, corruptionChars.Length)];
            }

            text.text = new string(buffer);
        }
    }

    private void RestoreCleanText()
    {
        if (_cleanText.Count == 0) return;

        foreach (KeyValuePair<TMP_Text, string> pair in _cleanText)
        {
            if (pair.Key != null) pair.Key.text = pair.Value;
        }
        _cleanText.Clear();
    }

    /// <summary>글자 내용이 바뀌면(쪽 넘기기 등) 원본을 다시 잡아야 한다.</summary>
    public void RefreshCleanText()
    {
        RestoreCleanText();
    }

    private static float Hash(float value)
    {
        float p = Mathf.Repeat(value * 0.1031f, 1f);
        p *= p + 33.33f;
        p *= p + p;
        return Mathf.Repeat(p, 1f);
    }

    private void OnDisable()
    {
        RestoreCleanText();
        _current = 0f;
        _velocity = 0f;
        ApplyToScreen();
    }
}

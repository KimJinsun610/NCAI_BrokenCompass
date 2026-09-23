using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 일시정지 화면의 밝기 슬라이더. <see cref="BrightnessSettings"/>에 값을 넘기는 것이 전부다.
///
/// <para>
/// <b>일시정지 중에도 움직여야 한다.</b> Esc를 누르면 <c>Time.timeScale</c>이 0이 되는데,
/// uGUI는 시간과 무관하게 입력을 받으므로 슬라이더는 그대로 동작한다.
/// 다만 이 컴포넌트는 <c>Time.deltaTime</c>을 쓰지 않아야 한다 — 쓰면 멈춘다.
/// </para>
///
/// <para>
/// 화면이 켜질 때마다 저장된 값을 슬라이더에 되읽는다. 슬라이더를 움직이면 즉시 화면에 반영되므로
/// 「확인」 버튼이 없다 — 보면서 맞추는 것이 밝기 설정의 자연스러운 방식이다.
/// </para>
/// </summary>
[DisallowMultipleComponent]
public sealed class BrightnessSlider : MonoBehaviour
{
    [Tooltip("0~1 슬라이더. 비워 두면 자식에서 찾는다.")]
    [SerializeField] private Slider slider;

    [Tooltip("오른쪽에 붙는 숫자. 비워 두면 자식에서 이름으로 찾는다.")]
    [SerializeField] private TMP_Text valueText;

    [Tooltip("숫자 형식. {0}에 0~100 백분율이 들어간다.")]
    [SerializeField] private string valueFormat = "{0}%";

    private bool _binding;

    private void Awake()
    {
        if (slider == null) slider = GetComponentInChildren<Slider>(true);
        if (valueText == null)
        {
            foreach (TMP_Text t in GetComponentsInChildren<TMP_Text>(true))
            {
                if (t.name.IndexOf("Value", System.StringComparison.OrdinalIgnoreCase) >= 0) { valueText = t; break; }
            }
        }

        if (slider != null)
        {
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            slider.onValueChanged.AddListener(OnSliderChanged);
        }
    }

    private void OnEnable()
    {
        Bind();
        BrightnessSettings.Changed += OnSettingChanged;
    }

    private void OnDisable()
    {
        BrightnessSettings.Changed -= OnSettingChanged;
    }

    private void OnDestroy()
    {
        if (slider != null) slider.onValueChanged.RemoveListener(OnSliderChanged);
    }

    /// <summary>저장된 값을 슬라이더에 되읽는다. 그 과정에서 onValueChanged가 다시 돌지 않게 막는다.</summary>
    private void Bind()
    {
        _binding = true;
        try
        {
            if (slider != null) slider.value = BrightnessSettings.Value;
            Redraw(BrightnessSettings.Value);
        }
        finally
        {
            _binding = false;
        }
    }

    private void OnSliderChanged(float value)
    {
        if (_binding) return;

        BrightnessSettings.Set(value);
        Redraw(value);
    }

    private void OnSettingChanged(float value)
    {
        if (slider != null && !Mathf.Approximately(slider.value, value))
        {
            _binding = true;
            try { slider.value = value; }
            finally { _binding = false; }
        }

        Redraw(value);
    }

    private void Redraw(float value)
    {
        if (valueText == null) return;

        valueText.text = string.Format(valueFormat, Mathf.RoundToInt(value * 100f));
    }

    /// <summary>「기본값」 버튼이 있으면 여기에 연결한다.</summary>
    public void ResetToDefault()
    {
        BrightnessSettings.Reset();
        Bind();
    }
}

using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 86차: 일시정지 화면의 마우스 감도·볼륨 슬라이더(<see cref="BrightnessSlider"/>와 같은 짜임 — 움직이면 바로 반영, 확인 버튼 없음).
/// <see cref="PauseMenuExtras"/>가 밝기 묶음(Group_Brightness)을 복제해 붙인다. <c>Time.deltaTime</c>을 쓰지 않는다(일시정지 중 timeScale 0).
/// </summary>
[DisallowMultipleComponent]
public sealed class OptionSlider : MonoBehaviour
{
    /// <summary>무엇을 조절하는지.</summary>
    public enum Kind
    {
        Mouse = 0,
        Volume = 1
    }

    [SerializeField] private Kind kind;
    [SerializeField] private Slider slider;
    [SerializeField] private TMP_Text valueText;

    private bool _binding;
    private bool _wired;

    /// <summary>복제 직후(꺼진 채) 설정한다.</summary>
    public void Setup(Kind what, Slider s, TMP_Text text)
    {
        kind = what;
        slider = s;
        valueText = text;
    }

    /// <summary>조절 대상.</summary>
    public Kind Target
    {
        get { return kind; }
    }

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

        if (slider != null && !_wired)
        {
            _wired = true;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            slider.onValueChanged.AddListener(OnSliderChanged);
        }
    }

    private void OnEnable()
    {
        Bind();
        PlayerOptions.Changed += Bind;
    }

    private void OnDisable()
    {
        PlayerOptions.Changed -= Bind;
    }

    private void OnDestroy()
    {
        if (slider != null) slider.onValueChanged.RemoveListener(OnSliderChanged);
    }

    private float Current
    {
        get { return kind == Kind.Mouse ? PlayerOptions.Mouse : PlayerOptions.Volume; }
    }

    private void Bind()
    {
        _binding = true;
        try
        {
            if (slider != null && !Mathf.Approximately(slider.value, Current)) slider.value = Current;
            Redraw(Current);
        }
        finally
        {
            _binding = false;
        }
    }

    private void OnSliderChanged(float value)
    {
        if (_binding) return;
        if (kind == Kind.Mouse) PlayerOptions.SetMouse(value);
        else PlayerOptions.SetVolume(value);
        Redraw(value);
    }

    private void Redraw(float value)
    {
        if (valueText == null) return;
        valueText.text = kind == Kind.Mouse
            ? PlayerOptions.MultiplierOf(value).ToString("0.0") + "x"   // 감도는 배수(1.0x = 제작자 값)
            : Mathf.RoundToInt(value * 100f) + "%";
    }
}

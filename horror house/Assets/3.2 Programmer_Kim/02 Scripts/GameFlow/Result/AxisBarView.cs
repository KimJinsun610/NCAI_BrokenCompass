using NightDuty;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 공포 축 한 줄: 축 이름 + 칸 막대(기본 8칸).
/// 기획상 수치와 "무엇 때문에 올랐는지"는 표시하지 않는다.
/// </summary>
public class AxisBarView : MonoBehaviour
{
    [SerializeField] private FearAxis axis;
    [SerializeField] private TMP_Text label;
    [SerializeField] private Image[] segments;
    [SerializeField] private Color litColor = new Color(0.85f, 0.85f, 0.85f, 1f);
    [SerializeField] private Color unlitColor = new Color(1f, 1f, 1f, 0.12f);

    private int targetCells;
    private TMP_FontAsset defaultFont;

    public FearAxis Axis => axis;

    public void Setup(int value, bool isImprint, TMP_FontAsset imprintFont)
    {
        int count = segments != null ? segments.Length : 0;

        // 0~100 → 칸 수 (올림, 정수 연산으로 부동소수 오차 방지)
        targetCells = (Mathf.Clamp(value, 0, 100) * count + 99) / 100;

        if (label != null)
        {
            if (defaultFont == null) defaultFont = label.font;
            label.text = AxisName(axis);

            // 각인축은 이름의 서체만 바꾼다 — 설명 문구는 붙이지 않는다
            label.font = isImprint && imprintFont != null ? imprintFont : defaultFont;
        }

        SetProgress(0f);
    }

    /// <summary>0~1: 목표 칸 수까지 채워진 정도</summary>
    public void SetProgress(float progress01)
    {
        if (segments == null) return;

        int lit = Mathf.RoundToInt(targetCells * Mathf.Clamp01(progress01));
        for (int i = 0; i < segments.Length; i++)
        {
            if (segments[i] != null) segments[i].color = i < lit ? litColor : unlitColor;
        }
    }

    public static string AxisName(FearAxis axis)
    {
        switch (axis)
        {
            case FearAxis.Auditory: return "청각";
            case FearAxis.Illuminance: return "조도";
            case FearAxis.Layout: return "배치";
            case FearAxis.Trust: return "신뢰";
            default: return axis.ToString();
        }
    }
}

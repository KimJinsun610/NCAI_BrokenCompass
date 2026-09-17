using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 위반 로그 목록.
/// 기획상 규칙 ID나 내용은 절대 표시하지 않고 「지침 미준수 · 시각」만 쓴다.
/// </summary>
public class ViolationLogView : MonoBehaviour
{
    [Tooltip("복제해서 쓸 줄 템플릿 (비활성 상태로 둔다)")]
    [SerializeField] private TMP_Text rowTemplate;
    [SerializeField] private string rowFormat = "지침 미준수 · {0}";
    [SerializeField] private string emptyText = "기록 없음";
    [SerializeField] private string overflowFormat = "외 {0}건";
    [Tooltip("최대 표시 줄 수. 넘치면 마지막 줄을 \"외 N건\"으로 표시한다.")]
    [SerializeField, Min(1)] private int maxRows = 6;

    private readonly List<CanvasGroup> rows = new List<CanvasGroup>();

    /// <summary>생성된 줄 (순차 연출용, 처음엔 alpha 0)</summary>
    public IReadOnlyList<CanvasGroup> Rows => rows;

    public void Build(string[] times)
    {
        Clear();
        if (rowTemplate == null) return;
        rowTemplate.gameObject.SetActive(false);

        int count = times != null ? times.Length : 0;
        if (count == 0)
        {
            AddRow(emptyText);
            return;
        }

        int shown = count > maxRows ? maxRows - 1 : count;
        for (int i = 0; i < shown; i++)
        {
            AddRow(string.Format(rowFormat, times[i]));
        }
        if (shown < count)
        {
            AddRow(string.Format(overflowFormat, count - shown));
        }
    }

    private void AddRow(string text)
    {
        TMP_Text row = Instantiate(rowTemplate, rowTemplate.transform.parent);
        row.gameObject.SetActive(true);
        row.text = text;

        CanvasGroup group = row.GetComponent<CanvasGroup>();
        if (group == null) group = row.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;

        rows.Add(group);
    }

    private void Clear()
    {
        foreach (CanvasGroup row in rows)
        {
            if (row != null) Destroy(row.gameObject);
        }
        rows.Clear();
    }
}

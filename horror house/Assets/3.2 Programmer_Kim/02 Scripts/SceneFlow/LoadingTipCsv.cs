using System.Collections.Generic;
using System.Text;

/// <summary>
/// 로딩 팁 CSV 한 줄.
/// </summary>
public readonly struct LoadingTipRow
{
    public readonly string Id;
    /// <summary>이 날(DAY) 이상부터 나온다. 1 = 처음부터.</summary>
    public readonly int UnlockDay;
    public readonly string Text;

    public LoadingTipRow(string id, int unlockDay, string text)
    {
        Id = id;
        UnlockDay = unlockDay;
        Text = text;
    }
}

/// <summary>
/// 로딩 팁 CSV 파서.
/// <para>
/// 형식(첫 줄은 머리글): <c>Id,UnlockDay,Text</c><br/>
/// · <b>UnlockDay</b> — 이 DAY 이상에서만 나온다. 비우면 1(언제나).
///   현장에서 알게 되는 내용(스포일러)은 2 이상으로 둔다.<br/>
/// · <b>Text</b>에 쉼표·줄바꿈이 있으면 큰따옴표로 감싼다. 큰따옴표 자체는 <c>""</c>.<br/>
/// · <c>#</c>으로 시작하는 줄과 빈 줄은 건너뛴다(메모용).
/// </para>
/// 엑셀에서 「CSV UTF-8」로 저장하면 한글이 깨지지 않는다.
/// </summary>
public static class LoadingTipCsv
{
    private const int ColId = 0;
    private const int ColUnlockDay = 1;
    private const int ColText = 2;

    public static List<LoadingTipRow> Parse(string csv, string sourceName = "LoadingTips.csv")
    {
        var rows = new List<LoadingTipRow>();
        if (string.IsNullOrEmpty(csv)) return rows;

        List<List<string>> records = SplitRecords(csv.TrimStart('﻿'));
        for (int i = 1; i < records.Count; i++) // 0번은 머리글
        {
            List<string> fields = records[i];
            if (IsBlankOrComment(fields)) continue;

            string text = Field(fields, ColText).Trim();
            if (text.Length == 0)
            {
                UnityEngine.Debug.LogWarning($"[LoadingTipCsv] {sourceName} {i + 1}번째 줄: Text가 비어 건너뜁니다.");
                continue;
            }

            string dayField = Field(fields, ColUnlockDay).Trim();
            int unlockDay = 1;
            if (dayField.Length > 0 && !int.TryParse(dayField, out unlockDay))
            {
                UnityEngine.Debug.LogWarning($"[LoadingTipCsv] {sourceName} {i + 1}번째 줄: UnlockDay '{dayField}'가 숫자가 아니라 1로 봅니다.");
                unlockDay = 1;
            }

            rows.Add(new LoadingTipRow(Field(fields, ColId).Trim(), unlockDay < 1 ? 1 : unlockDay, text));
        }

        return rows;
    }

    private static string Field(List<string> fields, int index) => index < fields.Count ? fields[index] : string.Empty;

    private static bool IsBlankOrComment(List<string> fields)
    {
        if (fields.Count == 0) return true;
        string first = fields[0].TrimStart();
        if (first.StartsWith("#")) return true;

        foreach (string f in fields)
        {
            if (!string.IsNullOrWhiteSpace(f)) return false;
        }
        return true;
    }

    /// <summary>RFC 4180 방식: 따옴표 안의 쉼표·줄바꿈은 값의 일부.</summary>
    private static List<List<string>> SplitRecords(string csv)
    {
        var records = new List<List<string>>();
        var fields = new List<string>();
        var sb = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < csv.Length; i++)
        {
            char c = csv[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < csv.Length && csv[i + 1] == '"') { sb.Append('"'); i++; }
                    else inQuotes = false;
                }
                else sb.Append(c);
                continue;
            }

            switch (c)
            {
                case '"':
                    inQuotes = true;
                    break;
                case ',':
                    fields.Add(sb.ToString());
                    sb.Clear();
                    break;
                case '\r':
                    break; // \r\n · \n 모두 \n에서 처리
                case '\n':
                    fields.Add(sb.ToString());
                    sb.Clear();
                    records.Add(fields);
                    fields = new List<string>();
                    break;
                default:
                    sb.Append(c);
                    break;
            }
        }

        if (sb.Length > 0 || fields.Count > 0)
        {
            fields.Add(sb.ToString());
            records.Add(fields);
        }

        return records;
    }
}

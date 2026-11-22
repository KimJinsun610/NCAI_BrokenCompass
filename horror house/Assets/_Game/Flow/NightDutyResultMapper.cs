using System;
using NightDuty;

/// <summary>
/// 판정 코어의 <see cref="DaySummary"/>를 결과창용 <see cref="DayResult"/>로 옮긴다.
/// 결과창에는 수칙 본문과 빨간 줄만 넘기고 카드 ID는 넘기지 않는다.
/// </summary>
public static class NightDutyResultMapper
{
    /// <summary>
    /// 결과를 만든다. <paramref name="clock"/>이 있으면 위반 시각을 결과창과 같은 표기(12시간제 등)로 쓴다.
    /// </summary>
    /// <param name="summary">코어 결과.</param>
    /// <param name="clock">표기용 게임 시계. 없으면 0:41 형식.</param>
    /// <param name="forcedCriticalAxis">코어를 거치지 않은 사망(디버그 메뉴 등)일 때의 원인 축.</param>
    public static DayResult From(DaySummary summary, GameTime clock, FearAxis? forcedCriticalAxis = null)
    {
        bool captured = summary.Outcome == NightOutcome.Captured;
        bool died = captured || forcedCriticalAxis.HasValue;
        FearAxis? axis = captured ? summary.Cause.Axis : forcedCriticalAxis;

        string[] times = new string[summary.ViolationMinutes.Count];
        for (int i = 0; i < times.Length; i++)
        {
            int minute = summary.ViolationMinutes[i];
            times[i] = minute < 0 ? "--:--"
                : clock != null ? clock.FormatTime(minute)
                : DaySummary.FormatMinutes(minute);
        }

        DutyLogLine[] lines = summary.DutyLog.Count == 0 ? Array.Empty<DutyLogLine>() : new DutyLogLine[summary.DutyLog.Count];
        for (int i = 0; i < lines.Length; i++)
        {
            DutyLogEntry entry = summary.DutyLog[i];
            lines[i] = new DutyLogLine(entry.Number, LineText(entry), entry.Struck);
        }

        return new DayResult(summary, died ? DayOutcome.Died : DayOutcome.Completed, died ? axis : null, times, lines);
    }

    /// <summary>
    /// 결과창 줄 글 — 수칙 원문 뒤에 표시(10단계: 「지시를 따름」·「불가피」, 안전한 읽기 「확인함 → …」)를 붙인다.
    /// 결과창(김진선님 DutyLogLine)은 글·빨간 줄만 받으므로 표시는 글 끝에 흐린 색으로 덧붙인다. 「어김」은 빨간 줄 자체다.
    /// </summary>
    /// <summary>2026-11-22: 근무일지 수칙 줄을 지금 언어로 — 기록은 한국어(PlayerText)라 영어면 수칙 ID로 다시 만든다(해독한 빈칸 포함).</summary>
    private static string ShownLine(DutyLogEntry entry)
    {
        if (Loc.IsKorean) return entry.PlayerText;
        RuleDef def = ProgramCatalog.Rule(entry.RuleId);
        return def == null ? entry.PlayerText : NightRun.ShownRuleText(entry.RuleId, def.ShownText);
    }

    /// <summary>2026-11-22: 근무일지 메모(코어가 한국어로 적은 「확인함 → 확인 필요/이상 없음」)를 지금 언어로.</summary>
    private static string ShownNote(string note)
    {
        const string Head = "확인함 → ";
        if (Loc.IsKorean || !note.StartsWith(Head, StringComparison.Ordinal)) return note;
        string label = note.Substring(Head.Length);
        if (label == ParadoxRun.RevealAnomaly) label = Loc.T("reveal.anomaly", "확인 필요");
        else if (label == ParadoxRun.RevealClear) label = Loc.T("reveal.clear", "이상 없음");
        return string.Format(Loc.T("result.note.checked", "확인함 → {0}"), label);
    }

    private static string LineText(DutyLogEntry entry)
    {
        string text = ShownLine(entry);
        switch (entry.Mark)
        {
            case DutyMark.Instructed: text += "  <color=#9AA5AB>— " + Loc.T("result.mark.instructed", "지시를 따름") + "</color>"; break;
            case DutyMark.Unavoidable: text += "  <color=#9AA5AB>— " + Loc.T("result.mark.unavoidable", "불가피") + "</color>"; break;
        }

        if (entry.Note.Length > 0) text += "  <color=#7FA6B8>(" + ShownNote(entry.Note) + ")</color>";
        return text;
    }
}

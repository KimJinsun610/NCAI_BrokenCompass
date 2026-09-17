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
            lines[i] = new DutyLogLine(entry.Number, entry.PlayerText, entry.Struck);
        }

        return new DayResult(summary, died ? DayOutcome.Died : DayOutcome.Completed, died ? axis : null, times, lines);
    }
}

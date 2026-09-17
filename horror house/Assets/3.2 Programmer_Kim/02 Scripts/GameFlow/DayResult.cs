using System;
using NightDuty;

/// <summary>하루 근무가 끝난 방식.</summary>
public enum DayOutcome
{
    /// <summary>근무 시간을 채움 → 근무 일지 확인 후 다음 날 (Day 5면 수치 결산)</summary>
    Completed,

    /// <summary>공포 축이 임계(100)에 도달 → 근무 일지 확인 후 Day 1부터 다시</summary>
    Died,
}

/// <summary>
/// 근무 일지 "금일 근무 지침"의 한 줄.
/// 준수한 항목과 조건이 걸리지 않은 항목은 똑같이 표시 없음(Struck = false)이다.
/// </summary>
public readonly struct DutyLogLine
{
    /// <summary>지침 번호. 지침록과 같은 번호를 쓴다 — 소거된 줄이 있어도 다시 매기지 않는다.</summary>
    public readonly int Number;

    /// <summary>지침 문구 그대로의 텍스트.</summary>
    public readonly string Text;

    /// <summary>빨간 줄 여부 — 위반했거나, 해당 공간을 방문하지 않은 항목.</summary>
    public readonly bool Struck;

    public DutyLogLine(int number, string text, bool struck)
    {
        Number = number;
        Text = text ?? string.Empty;
        Struck = struck;
    }
}

/// <summary>
/// Play 씬에서 Result 씬으로 넘기는 결과 묶음.
/// Core의 DaySummary에 없는 정보(종료 방식, 위반 시각, 근무 일지 줄)를 함께 담는다.
/// </summary>
public readonly struct DayResult
{
    public readonly DaySummary Summary;
    public readonly DayOutcome Outcome;

    /// <summary>사망 원인 축. 근무 완료면 null.</summary>
    public readonly FearAxis? CriticalAxis;

    /// <summary>
    /// 위반 발생 시각 표시 문자열(예: "12:41"). Day 5 수치 결산 화면에서 쓴다.
    /// DaySummary에는 아직 위반 개수만 있어서 여기서 따로 들고 간다.
    /// </summary>
    public readonly string[] ViolationTimes;

    /// <summary>근무 일지에 표시할 그날의 지침 목록 (순서대로).</summary>
    public readonly DutyLogLine[] LogLines;

    public DayResult(DaySummary summary, DayOutcome outcome, FearAxis? criticalAxis, string[] violationTimes, DutyLogLine[] logLines)
    {
        Summary = summary;
        Outcome = outcome;
        CriticalAxis = criticalAxis;
        ViolationTimes = violationTimes ?? Array.Empty<string>();
        LogLines = logLines ?? Array.Empty<DutyLogLine>();
    }
}

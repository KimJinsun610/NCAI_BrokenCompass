using System;
using NightDuty;

/// <summary>하루 근무가 끝난 방식.</summary>
public enum DayOutcome
{
    /// <summary>근무 시간을 채움 → 결과창에서 "다음 근무"(Day + 1)</summary>
    Completed,

    /// <summary>공포 축이 임계(100)에 도달 → 결과창에서 "처음부터"(Day 1)</summary>
    Died,
}

/// <summary>
/// Play 씬에서 Result 씬으로 넘기는 결과 묶음.
/// Core의 DaySummary에 없는 정보(종료 방식, 위반 시각)를 함께 담는다.
/// </summary>
public readonly struct DayResult
{
    public readonly DaySummary Summary;
    public readonly DayOutcome Outcome;

    /// <summary>사망 원인 축. 근무 완료면 null.</summary>
    public readonly FearAxis? CriticalAxis;

    /// <summary>
    /// 위반 발생 시각 표시 문자열(예: "12:41").
    /// DaySummary에는 아직 위반 개수만 있어서 여기서 따로 들고 간다.
    /// </summary>
    public readonly string[] ViolationTimes;

    public DayResult(DaySummary summary, DayOutcome outcome, FearAxis? criticalAxis, string[] violationTimes)
    {
        Summary = summary;
        Outcome = outcome;
        CriticalAxis = criticalAxis;
        ViolationTimes = violationTimes ?? Array.Empty<string>();
    }
}

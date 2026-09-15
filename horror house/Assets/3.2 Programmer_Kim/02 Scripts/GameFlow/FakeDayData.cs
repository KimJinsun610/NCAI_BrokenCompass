using System;
using NightDuty;
using UnityEngine;

/// <summary>
/// 판정 시스템(Programmer_Lee) 완성 전까지 결과창에 넣을 가짜 결과값.
/// PlaySystems 프리팹의 PlayResultRouter 인스펙터에서 값을 바꾸면 결과창에 그대로 반영된다.
/// </summary>
[Serializable]
public class FakeDayData
{
    [Header("순찰 · 충돌")]
    [Min(0)] public int patrolDone = 3;
    [Min(0)] public int patrolTotal = 3;
    [Min(0)] public int conflictsHandled = 1;
    [Min(0)] public int conflictsTotal = 1;

    [Header("공포 축 (0~100)")]
    [Range(0, 100)] public int auditory = 38;
    [Range(0, 100)] public int illuminance = 55;
    [Range(0, 100)] public int layout = 21;
    [Range(0, 100)] public int trust = 47;

    [Header("각인축 (Day 3부터 표시)")]
    public bool hasImprintAxis = true;
    public FearAxis imprintAxis = FearAxis.Illuminance;

    [Header("위반 로그")]
    [Tooltip("위반 발생 시각. 항목 수가 곧 위반 횟수다.")]
    public string[] violationTimes = { "12:41" };

    public DayResult Build(int day, DayOutcome outcome, FearAxis? criticalAxis)
    {
        string[] times = violationTimes ?? Array.Empty<string>();

        // 기획상 각인축은 Day 3 이전에는 판정 근거가 부족해 없다
        FearAxis? imprint = hasImprintAxis && day >= 3 ? (FearAxis?)imprintAxis : null;

        // 사망이면 원인 축을 임계값(100)으로 표시
        int a = criticalAxis == FearAxis.Auditory ? 100 : auditory;
        int i = criticalAxis == FearAxis.Illuminance ? 100 : illuminance;
        int l = criticalAxis == FearAxis.Layout ? 100 : layout;
        int t = criticalAxis == FearAxis.Trust ? 100 : trust;

        DaySummary summary = new DaySummary(
            day, patrolDone, patrolTotal, times.Length, conflictsHandled, conflictsTotal,
            a, i, l, t, imprint);

        return new DayResult(summary, outcome, criticalAxis, times);
    }
}

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
    /// <summary>근무 일지에 들어갈 지침 한 줄 (가짜)</summary>
    [Serializable]
    public class FakeRule
    {
        [TextArea(1, 3)] public string text;

        [Tooltip("이 지침이 속한 공간. 방문하지 않은 공간의 지침은 모두 빨간 줄로 표시된다.")]
        public SpaceId space;

        [Tooltip("켜면 위반 — 근무 일지에 빨간 줄")]
        public bool violated;

        public FakeRule()
        {
        }

        public FakeRule(string text, SpaceId space, bool violated)
        {
            this.text = text;
            this.space = space;
            this.violated = violated;
        }
    }

    [Header("근무 일지 — 금일 근무 지침 (표시 순서대로)")]
    public FakeRule[] rules =
    {
        new FakeRule("복도를 지나던 중 문이 열린다면, 닫지 말고 그대로 지나가십시오.", SpaceId.Corridor, false),
        new FakeRule("복도 끝을 오래 보지 마십시오.", SpaceId.Corridor, false),
        new FakeRule("화장실 칸 문은 모두 닫혀 있어야 합니다. 열린 칸이 있다면 닫으십시오.", SpaceId.Toilet, false),
        new FakeRule("세 번째 칸은 열지 마십시오. 그 안에 무엇이 있는지 궁금해하시면 위험합니다.", SpaceId.Toilet, true),
        new FakeRule("야간에 물소리가 들리는 것은 정상입니다.", SpaceId.Toilet, false),
        new FakeRule("본교 1-1 교실 책상은 스물다섯 개입니다. 숫자가 다르게 느껴져도 다시 세지 마십시오.", SpaceId.Classroom_1_1, true),
        new FakeRule("교실 바닥에 이 교실의 것이 아닌 물건이 있다면, 다가가지 마십시오.", SpaceId.Classroom_1_3, false),
        new FakeRule("교실은 오늘 특이사항 없음으로 보고되었습니다.", SpaceId.Classroom_1_3, false),
    };

    [Tooltip("방문하지 않은 공간. 이 공간에 속한 지침은 위반 여부와 상관없이 모두 빨간 줄로 표시된다.")]
    public SpaceId[] unvisitedSpaces = new SpaceId[0];

    [Header("수치 결산 (Day 5) — 순찰 · 충돌")]
    [Min(0)] public int patrolDone = 3;
    [Min(0)] public int patrolTotal = 3;
    [Min(0)] public int conflictsHandled = 1;
    [Min(0)] public int conflictsTotal = 1;

    [Header("수치 결산 (Day 5) — 공포 축 (0~100)")]
    [Range(0, 100)] public int auditory = 38;
    [Range(0, 100)] public int illuminance = 55;
    [Range(0, 100)] public int layout = 21;
    [Range(0, 100)] public int trust = 47;

    [Header("수치 결산 (Day 5) — 각인축 (Day 3부터 판정)")]
    public bool hasImprintAxis = true;
    public FearAxis imprintAxis = FearAxis.Illuminance;

    [Header("수치 결산 (Day 5) — 위반 로그")]
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

        return new DayResult(summary, outcome, criticalAxis, times, BuildLogLines());
    }

    /// <summary>지침 목록을 근무 일지 줄로 바꾼다. 위반 또는 미방문 공간이면 빨간 줄.</summary>
    public DutyLogLine[] BuildLogLines()
    {
        if (rules == null) return Array.Empty<DutyLogLine>();

        DutyLogLine[] lines = new DutyLogLine[rules.Length];
        for (int index = 0; index < rules.Length; index++)
        {
            FakeRule rule = rules[index];
            if (rule == null)
            {
                lines[index] = new DutyLogLine(index + 1, string.Empty, false);
                continue;
            }

            bool unvisited = unvisitedSpaces != null && Array.IndexOf(unvisitedSpaces, rule.space) >= 0;
            lines[index] = new DutyLogLine(index + 1, rule.text, rule.violated || unvisited);
        }
        return lines;
    }
}

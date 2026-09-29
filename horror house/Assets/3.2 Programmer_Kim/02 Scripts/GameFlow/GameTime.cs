using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 근무 시간 시계. 시작 시각부터 종료 시각까지 흐르고, 종료 시각이 되면 ShiftEnded를 발생시킨다.
/// 기본값: 02:00 → 05:00, 현실보다 30배 빠르게 (인게임 1시간 = 현실 2분, 하루 = 현실 6분).
/// 실제 값은 PlaySystems 프리팹에 저장된 값이 우선한다.
/// Time.deltaTime 기준이라 일시정지(timeScale 0) 중에는 시계도 멈춘다.
/// DayIntro · 태블릿 · 사망 화면이 각자 Hold(this)로 멈춰 두고 Release(this)로 푼다 — 모두 풀어야 다시 흐른다.
/// </summary>
public class GameTime : MonoBehaviour
{
    [Header("근무 시간 (24시간 기준, 0 = 자정)")]
    [SerializeField, Range(0, 23)] private int startHour = 2;
    [SerializeField, Range(0, 59)] private int startMinute = 0;
    [Tooltip("시작 시각보다 이르거나 같으면 다음 날로 계산한다 (예: 23:00 → 1:00)")]
    [SerializeField, Range(0, 23)] private int endHour = 5;
    [SerializeField, Range(0, 59)] private int endMinute = 0;

    [Header("속도")]
    [Tooltip("현실 1초 동안 흐르는 게임 시간(초). 30 = 30배속 (인게임 1시간 = 현실 2분)")]
    [SerializeField, Min(0.1f)] private float timeMultiplier = 30f;

    [Header("표시")]
    [Tooltip("켜면 12시간제(0시 → 12:00, 13시 → 1:00), 끄면 24시간제(00:00)")]
    [SerializeField] private bool use12HourFormat = true;

    /// <summary>표시 시각이 바뀔 때(분 단위) 발생. 인자: 표시 문자열</summary>
    public event Action<string> TimeTextChanged;

    /// <summary>종료 시각에 도달했을 때 한 번 발생</summary>
    public event Action ShiftEnded;

    public string CurrentTimeText { get; private set; }

    /// <summary>현재 게임 시각(자정 기준 누적 분). 판정 코어가 위반 시각을 기록할 때 읽는다. 표시는 FormatTime으로.</summary>
    public int CurrentMinutes => Mathf.FloorToInt(currentSeconds / 60f);

    public bool IsEnded => ended;

    /// <summary>시계가 흐르는 중인지. 누구든 <see cref="Hold"/>로 멈춰 두었으면 false.</summary>
    public bool IsRunning => holds.Count == 0;

    /// <summary>현실 1초 동안 흐르는 게임 시간(초)</summary>
    public float TimeMultiplier => timeMultiplier;

    public string StartTimeText => FormatTime(Mathf.FloorToInt(startSeconds / 60f));

    public string EndTimeText => FormatTime(Mathf.FloorToInt(endSeconds / 60f));

    /// <summary>근무 시작 시각(자정 기준 누적 분). 태블릿 문서의 발행 시각처럼 시작 시각을 기준으로 계산할 때 쓴다.</summary>
    public int StartMinutes => Mathf.FloorToInt(startSeconds / 60f);

    /// <summary>근무 종료 시각(자정 기준 누적 분). 시작보다 이르면 다음 날이라 1440을 넘을 수 있다.</summary>
    public int EndMinutes => Mathf.FloorToInt(endSeconds / 60f);

    private const float SecondsPerDay = 24f * 60f * 60f;

    private float currentSeconds;
    private float startSeconds;
    private float endSeconds;
    private int lastShownMinute = -1;
    private bool ended;

    // 시계를 멈춰 둔 주체들. 하나라도 남아 있으면 멈춘다.
    // bool 하나를 여럿이 켜고 끄면 마지막에 쓴 쪽이 이긴다 — DayIntro가 끝나며 켠 순간
    // 태블릿이 열려 있어도 시계가 흘렀다(2026-09-29 재현). 그래서 각자 자기 몫만 풀게 한다.
    private readonly HashSet<object> holds = new HashSet<object>();

    // SetRunning(bool)을 쓰는 옛 호출부의 몫. 서로 구분하지 못하므로 새 코드는 Hold/Release를 쓴다.
    private static readonly object LegacyHold = new object();

    private void Awake()
    {
        startSeconds = (startHour * 60 + startMinute) * 60f;
        currentSeconds = startSeconds;
        endSeconds = (endHour * 60 + endMinute) * 60f;
        if (endSeconds <= startSeconds)
        {
            endSeconds += SecondsPerDay;
        }

        RefreshText(true);
    }

    private void Update()
    {
        if (ended || holds.Count > 0) return;

        currentSeconds += Time.deltaTime * timeMultiplier;
        if (currentSeconds >= endSeconds)
        {
            currentSeconds = endSeconds;
            ended = true;
            RefreshText(false);
            ShiftEnded?.Invoke();
            return;
        }

        RefreshText(false);
    }

    /// <summary>
    /// <paramref name="owner"/> 몫으로 시계를 멈춘다. 같은 주체가 여러 번 불러도 한 번으로 친다.
    /// 다른 주체가 멈춰 둔 것은 건드리지 않는다 — 모든 주체가 <see cref="Release"/>해야 다시 흐른다.
    /// </summary>
    public void Hold(object owner)
    {
        if (owner != null) holds.Add(owner);
    }

    /// <summary><paramref name="owner"/>가 멈춰 둔 몫을 푼다. 멈춘 적이 없으면 아무 일도 없다.</summary>
    public void Release(object owner)
    {
        if (owner != null) holds.Remove(owner);
    }

    /// <summary>
    /// 옛 방식: 시계를 멈추거나(false) 다시 흐르게(true) 한다. 호출부끼리 구분하지 못해
    /// 서로의 정지를 풀어 버릴 수 있으므로 새 코드에서는 <see cref="Hold"/>/<see cref="Release"/>를 쓴다.
    /// </summary>
    public void SetRunning(bool value)
    {
        if (value) Release(LegacyHold);
        else Hold(LegacyHold);
    }

    /// <summary>테스트용: 다음 프레임에 종료 시각으로 건너뛴다 (정상 종료 경로를 그대로 탄다).</summary>
    public void SkipToEnd()
    {
        if (!ended) currentSeconds = endSeconds;
    }

    public void SetTimeMultiplier(float value)
    {
        timeMultiplier = Mathf.Max(0.1f, value);
    }

    /// <summary>
    /// 테스트용: 근무 구간 안의 hour:00으로 이동한다. 12시간제 표기면 12는 자정으로 본다.
    /// 종료 시각 이후를 고르면 종료 시각에 맞추고, 다음 프레임에 정상 종료 경로를 탄다.
    /// </summary>
    /// <returns>실제로 이동한 시각이 종료 시각으로 잘렸으면 true</returns>
    public bool JumpToHour(int hour)
    {
        if (ended) return false;

        hour = ((hour % 24) + 24) % 24;
        if (use12HourFormat && hour == 12) hour = 0;

        float target = hour * 3600f;
        if (target < startSeconds) target += SecondsPerDay;

        bool clamped = target > endSeconds;
        currentSeconds = clamped ? endSeconds : target;
        RefreshText(true);
        return clamped;
    }

    /// <summary>자정 기준 누적 분을 표시 문자열로 바꾼다.</summary>
    public string FormatTime(int totalMinutes)
    {
        int hour = (totalMinutes / 60) % 24;
        int minute = totalMinutes % 60;

        if (use12HourFormat)
        {
            int hour12 = hour % 12;
            if (hour12 == 0) hour12 = 12;
            return $"{hour12}:{minute:00}";
        }
        return $"{hour:00}:{minute:00}";
    }

    private void RefreshText(bool force)
    {
        int totalMinutes = Mathf.FloorToInt(currentSeconds / 60f);
        if (!force && totalMinutes == lastShownMinute) return;

        lastShownMinute = totalMinutes;
        CurrentTimeText = FormatTime(totalMinutes);
        TimeTextChanged?.Invoke(CurrentTimeText);
    }
}

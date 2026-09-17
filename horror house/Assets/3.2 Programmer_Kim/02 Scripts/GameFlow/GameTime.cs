using System;
using UnityEngine;

/// <summary>
/// 근무 시간 시계. 시작 시각부터 종료 시각까지 흐르고, 종료 시각이 되면 ShiftEnded를 발생시킨다.
/// 기본값: 0:00(12시간제 표기로 12:00) → 1:00, 현실보다 20배 빠르게 (인게임 1시간 = 현실 3분).
/// Time.deltaTime 기준이라 일시정지(timeScale 0) 중에는 시계도 멈춘다.
/// DayIntro 같은 연출이 SetRunning(false)로 잠시 멈춰 둘 수 있다.
/// </summary>
public class GameTime : MonoBehaviour
{
    [Header("근무 시간 (24시간 기준, 0 = 자정)")]
    [SerializeField, Range(0, 23)] private int startHour = 0;
    [SerializeField, Range(0, 59)] private int startMinute = 0;
    [Tooltip("시작 시각보다 이르거나 같으면 다음 날로 계산한다 (예: 23:00 → 1:00)")]
    [SerializeField, Range(0, 23)] private int endHour = 1;
    [SerializeField, Range(0, 59)] private int endMinute = 0;

    [Header("속도")]
    [Tooltip("현실 1초 동안 흐르는 게임 시간(초). 20 = 20배속 (인게임 1시간 = 현실 3분)")]
    [SerializeField, Min(0.1f)] private float timeMultiplier = 20f;

    [Header("표시")]
    [Tooltip("켜면 12시간제(0시 → 12:00, 13시 → 1:00), 끄면 24시간제(00:00)")]
    [SerializeField] private bool use12HourFormat = true;

    /// <summary>표시 시각이 바뀔 때(분 단위) 발생. 인자: 표시 문자열</summary>
    public event Action<string> TimeTextChanged;

    /// <summary>종료 시각에 도달했을 때 한 번 발생</summary>
    public event Action ShiftEnded;

    public string CurrentTimeText { get; private set; }

    public bool IsEnded => ended;

    /// <summary>시계가 흐르는 중인지. 연출 등으로 멈춰 두면 false.</summary>
    public bool IsRunning => running;

    /// <summary>현실 1초 동안 흐르는 게임 시간(초)</summary>
    public float TimeMultiplier => timeMultiplier;

    public string StartTimeText => FormatTime(Mathf.FloorToInt(startSeconds / 60f));

    public string EndTimeText => FormatTime(Mathf.FloorToInt(endSeconds / 60f));

    private const float SecondsPerDay = 24f * 60f * 60f;

    private float currentSeconds;
    private float startSeconds;
    private float endSeconds;
    private int lastShownMinute = -1;
    private bool ended;
    private bool running = true;

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
        if (ended || !running) return;

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

    /// <summary>시계를 멈추거나(false) 다시 흐르게(true) 한다.</summary>
    public void SetRunning(bool value)
    {
        running = value;
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

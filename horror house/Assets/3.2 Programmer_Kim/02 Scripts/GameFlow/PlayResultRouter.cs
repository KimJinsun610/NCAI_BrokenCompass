using System;
using NightDuty;
using UnityEngine;

/// <summary>
/// Play 씬의 하루 종료를 감지해 결과를 GameSession에 저장하고 Result 씬으로 보낸다.
/// 종료 경로:
///  1) GameTime 종료 시각 도달 → 근무 완료 (현재는 FakeDayData로 결과 생성)
///  2) EventBus.AxisCritical → 사망
///  3) EventBus.DayEnded → 근무 완료 (Lee의 판정 시스템이 완성되면 실제 수치가 이 경로로 들어온다)
/// </summary>
public class PlayResultRouter : MonoBehaviour
{
    [Tooltip("비워 두면 씬에서 자동으로 찾는다.")]
    [SerializeField] private GameTime gameTime;

    [Header("가짜 결과 (판정 시스템 완성 전 임시)")]
    [SerializeField] private FakeDayData fakeData = new FakeDayData();

    private bool finished;

    private void OnEnable()
    {
        if (gameTime == null) gameTime = FindAnyObjectByType<GameTime>();
        if (gameTime != null) gameTime.ShiftEnded += OnShiftEnded;

        EventBus.DayEnded += OnDayEnded;
        EventBus.AxisCritical += OnAxisCritical;
    }

    private void OnDisable()
    {
        if (gameTime != null) gameTime.ShiftEnded -= OnShiftEnded;

        EventBus.DayEnded -= OnDayEnded;
        EventBus.AxisCritical -= OnAxisCritical;
    }

    private void OnShiftEnded()
    {
        Finish(fakeData.Build(GameSession.CurrentDay, DayOutcome.Completed, null));
    }

    private void OnDayEnded(DaySummary summary)
    {
        // 위반 시각 목록은 아직 DaySummary에 없어 비워 둔다
        Finish(new DayResult(summary, DayOutcome.Completed, null, Array.Empty<string>()));
    }

    private void OnAxisCritical(FearAxis axis)
    {
        Finish(fakeData.Build(GameSession.CurrentDay, DayOutcome.Died, axis));
    }

    private void Finish(DayResult result)
    {
        // 시간 종료와 사망이 같은 프레임에 겹쳐도 한 번만 처리
        if (finished) return;
        finished = true;

        GameSession.SetResult(result);
        SceneFlow.GoTo(GameScene.Result, false);
    }
}

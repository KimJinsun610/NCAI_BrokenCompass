using System;
using System.Collections;
using NightDuty;
using UnityEngine;

/// <summary>
/// Play 씬의 하루 종료를 감지한다.
/// 종료 경로:
///  1) GameTime 종료 시각 도달 → NightRunDriver가 종료 요청 → 3) 경로로 들어온다.
///     판정 코어의 밤이 없을 때(구동기 없는 시험 씬)만 FakeDayData로 결과를 만든다.
///  2) EventBus.AxisCritical → 사망 → 페이드 → 이 씬 위에 사망 화면(HUD_Death). 결과창은 거치지 않는다
///  3) EventBus.DayEnded → 근무 완료. NightDutyResultMapper로 실제 수치·위반 시각·근무 일지를 옮긴다.
/// </summary>
public class PlayResultRouter : MonoBehaviour
{
    // 페이드 한 프레임에 진행할 수 있는 최대 시간(초). 프레임이 끊겨도 페이드가 보이도록 한다.
    private const float MaxFadeStep = 1f / 30f;

    [Tooltip("비워 두면 씬에서 자동으로 찾는다.")]
    [SerializeField] private GameTime gameTime;

    [Header("결과창 이동 연출")]
    [Tooltip("화면 전체를 덮는 검정 그룹 (PlaySystems/Canvas_Fade). 비우면 페이드 없이 바로 이동한다.")]
    [SerializeField] private CanvasGroup fadeOverlay;
    [Tooltip("결과창으로 넘어가기 전 화면이 검게 가려지는 시간(초)")]
    [SerializeField, Min(0f)] private float fadeOutDuration = 1f;

    [Header("사망 화면")]
    [Tooltip("사망 시 페이드가 끝난 뒤 생성하는 UI 프리팹 (HUD_Death). 캔버스 정렬 순서가 페이드(200)보다 커야 보인다.")]
    [SerializeField] private GameObject deathScreenPrefab;

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
        // 밤이 열려 있으면 NightRunDriver가 종료 요청을 보내고 DayEnded로 돌아온다(구독 순서와 무관).
        if (NightRun.IsNightActive) return;

        // 구동기의 종료 요청이 이미 DayEnded를 처리했으면 finished라 아래는 무시된다.
        Finish(fakeData.Build(GameSession.CurrentDay, DayOutcome.Completed, null));
    }

    private void OnDayEnded(DaySummary summary)
    {
        Finish(NightDutyResultMapper.From(summary, gameTime));
    }

    private void OnAxisCritical(FearAxis axis)
    {
        // 시간 종료와 사망이 같은 프레임에 겹쳐도 한 번만 처리
        if (finished) return;
        finished = true;

        if (gameTime != null) gameTime.SetRunning(false);
        foreach (FPController player in FindObjectsByType<FPController>(FindObjectsSortMode.None))
        {
            player.enabled = false;
        }

        StartCoroutine(FadeOutAndShowDeath());
    }

    private void Finish(DayResult result)
    {
        if (finished) return;
        finished = true;

        GameSession.SetResult(result);
        StartCoroutine(FadeOutAndGo());
    }

    private IEnumerator FadeOutAndGo()
    {
        yield return FadeOut();
        SceneFlow.GoTo(GameScene.Result, false);
    }

    private IEnumerator FadeOutAndShowDeath()
    {
        yield return FadeOut();

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        if (deathScreenPrefab == null)
        {
            Debug.LogWarning("[PlayResultRouter] 사망 화면 프리팹이 비어 있어 메인으로 이동합니다. Tools > Programmer_Kim > Game Flow > Setup Death Screen을 실행하세요.", this);
            SceneFlow.GoTo(GameScene.Main);
            yield break;
        }

        Instantiate(deathScreenPrefab);
    }

    private IEnumerator FadeOut()
    {
        if (fadeOverlay != null)
        {
            fadeOverlay.alpha = 0f;
            fadeOverlay.blocksRaycasts = true;
            fadeOverlay.gameObject.SetActive(true);

            // 종료 순간 프레임이 한 번 끊기면 그 프레임의 시간 간격이 커서 페이드가 통째로 건너뛰어진다
            // → 첫 프레임은 쉬고, 프레임당 진행량에도 상한을 둔다
            yield return null;

            // 일시정지 중에 사망해도 멈추지 않도록 unscaled 시간 사용
            float t = 0f;
            while (t < fadeOutDuration)
            {
                t += Mathf.Min(Time.unscaledDeltaTime, MaxFadeStep);
                fadeOverlay.alpha = Mathf.Clamp01(t / fadeOutDuration);
                yield return null;
            }
            fadeOverlay.alpha = 1f;
        }
    }
}

using System.Collections;
using System.Collections.Generic;
using NightDuty;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 결과창. GameSession의 직전 결과를 표시하고, 순차 연출이 끝나면 버튼을 활성화한다.
/// 연출 순서: 페이드 인 → 제목 → 요약 → 공포 축 막대 → 위반 로그 한 줄씩 → 버튼.
/// 연출 중 아무 키나 클릭하면 즉시 전부 표시한다.
/// </summary>
public class ResultController : MonoBehaviour
{
    [Header("텍스트")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text statsText;

    [Header("순차 연출 그룹")]
    [SerializeField] private CanvasGroup headerGroup;
    [SerializeField] private CanvasGroup statsGroup;
    [SerializeField] private CanvasGroup axisGroup;
    [SerializeField] private CanvasGroup logGroup;
    [SerializeField] private CanvasGroup buttonsGroup;
    [Tooltip("화면 전체를 덮는 검정 이미지. alpha 1 = 가림")]
    [SerializeField] private CanvasGroup fade;

    [Header("세부 뷰")]
    [SerializeField] private AxisBarView[] axisBars;
    [SerializeField] private ViolationLogView violationLog;
    [Tooltip("각인축 이름에만 적용할 서체. 비우면 서체 변화 없음.")]
    [SerializeField] private TMP_FontAsset imprintFont;

    [Header("버튼")]
    [SerializeField] private Button nextButton;
    [SerializeField] private TMP_Text nextLabel;
    [SerializeField] private Button mainButton;

    [Header("문구")]
    [SerializeField] private string completedTitleFormat = "DAY {0} — 근무 종료";
    [SerializeField] private string diedTitleFormat = "DAY {0} — 근무 중단";
    [Tooltip("{0} 순찰 완료, {1} 순찰 전체, {2} 위반 수, {3} 충돌 처리, {4} 충돌 전체")]
    [SerializeField] private string statsFormat = "순찰률 {0}/{1} · 위반 {2}건 · 충돌 처리 {3}/{4}";
    [SerializeField] private string nextDayLabel = "다음 근무";
    [SerializeField] private string restartLabel = "처음부터";

    [Header("연출 시간 (초)")]
    [SerializeField, Min(0f)] private float fadeDuration = 0.6f;
    [SerializeField, Min(0f)] private float revealDuration = 0.35f;
    [SerializeField, Min(0f)] private float stepDelay = 0.25f;
    [SerializeField, Min(0f)] private float barFillDuration = 0.8f;
    [SerializeField, Min(0f)] private float logLineInterval = 0.45f;

    [Header("미리보기 (결과 없이 이 씬만 실행했을 때)")]
    [SerializeField] private DayOutcome previewOutcome = DayOutcome.Completed;
    [SerializeField] private FakeDayData previewData = new FakeDayData();

    private DayResult result;
    private bool skipRequested;
    private bool sequenceDone;
    private bool leaving;

    private void Awake()
    {
        if (nextButton != null) nextButton.onClick.AddListener(OnNextClicked);
        if (mainButton != null) mainButton.onClick.AddListener(OnMainClicked);
    }

    private IEnumerator Start()
    {
        // Play 씬의 FPController가 커서를 숨겨 두었으므로 복구
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        if (GameSession.HasResult)
        {
            result = GameSession.LastResult;
        }
        else
        {
            FearAxis? previewAxis = previewOutcome == DayOutcome.Died ? (FearAxis?)FearAxis.Auditory : null;
            result = previewData.Build(GameSession.CurrentDay, previewOutcome, previewAxis);
        }

        Bind();
        PrepareHidden();

        yield return Animate(fade, 1f, 0f, fadeDuration);
        yield return Animate(headerGroup, 0f, 1f, revealDuration);
        yield return Wait(stepDelay);
        yield return Animate(statsGroup, 0f, 1f, revealDuration);
        yield return Wait(stepDelay);
        yield return Animate(axisGroup, 0f, 1f, revealDuration);
        yield return FillBars();
        yield return Wait(stepDelay);
        yield return Animate(logGroup, 0f, 1f, revealDuration);

        if (violationLog != null)
        {
            IReadOnlyList<CanvasGroup> rows = violationLog.Rows;
            for (int i = 0; i < rows.Count; i++)
            {
                yield return Animate(rows[i], 0f, 1f, revealDuration);
                yield return Wait(logLineInterval);
            }
        }

        yield return Wait(stepDelay);
        ShowAll();

        // 스킵용 클릭이 그대로 버튼 클릭으로 이어지지 않도록, 마우스를 뗀 뒤에 버튼을 연다
        while (Input.GetMouseButton(0)) yield return null;
        SetButtonsInteractable(true);
        sequenceDone = true;
    }

    private void Update()
    {
        // anyKeyDown은 마우스 버튼도 포함한다
        if (!sequenceDone && !skipRequested && Input.anyKeyDown)
        {
            skipRequested = true;
        }
    }

    /// <summary>"다음 근무" / "처음부터" 버튼</summary>
    public void OnNextClicked()
    {
        if (leaving) return;
        leaving = true;

        if (result.Outcome == DayOutcome.Died) GameSession.StartNewRun();
        else GameSession.AdvanceDay();

        SceneFlow.GoTo(GameScene.Play);
    }

    /// <summary>"메인으로" 버튼</summary>
    public void OnMainClicked()
    {
        if (leaving) return;
        leaving = true;

        SceneFlow.GoTo(GameScene.Main);
    }

    private void Bind()
    {
        DaySummary summary = result.Summary;
        bool died = result.Outcome == DayOutcome.Died;

        if (titleText != null)
        {
            titleText.text = string.Format(died ? diedTitleFormat : completedTitleFormat, summary.Day);
        }
        if (statsText != null)
        {
            statsText.text = string.Format(statsFormat,
                summary.PatrolDone, summary.PatrolTotal, summary.Violations,
                summary.ConflictsHandled, summary.ConflictsTotal);
        }

        if (axisBars != null)
        {
            foreach (AxisBarView bar in axisBars)
            {
                if (bar == null) continue;
                bar.Setup(summary.AxisValue(bar.Axis), summary.ImprintAxis == bar.Axis, imprintFont);
            }
        }

        if (violationLog != null) violationLog.Build(result.ViolationTimes);

        // 사망 → 처음부터 / 근무 완료 → 다음 근무 / 마지막 날 완료 → 메인으로만
        bool showNext = died || summary.Day < GameSession.FinalDay;
        if (nextButton != null) nextButton.gameObject.SetActive(showNext);
        if (nextLabel != null) nextLabel.text = died ? restartLabel : nextDayLabel;
    }

    private void PrepareHidden()
    {
        SetAlpha(fade, 1f);
        SetAlpha(headerGroup, 0f);
        SetAlpha(statsGroup, 0f);
        SetAlpha(axisGroup, 0f);
        SetAlpha(logGroup, 0f);
        SetAlpha(buttonsGroup, 0f);
        SetBars(0f);
        SetButtonsInteractable(false);
    }

    private void ShowAll()
    {
        SetAlpha(fade, 0f);
        SetAlpha(headerGroup, 1f);
        SetAlpha(statsGroup, 1f);
        SetAlpha(axisGroup, 1f);
        SetAlpha(logGroup, 1f);
        SetAlpha(buttonsGroup, 1f);
        SetBars(1f);

        if (violationLog != null)
        {
            foreach (CanvasGroup row in violationLog.Rows) SetAlpha(row, 1f);
        }
    }

    private IEnumerator FillBars()
    {
        float t = 0f;
        while (t < barFillDuration && !skipRequested)
        {
            t += Time.unscaledDeltaTime;
            SetBars(t / barFillDuration);
            yield return null;
        }
        SetBars(1f);
    }

    private IEnumerator Animate(CanvasGroup group, float from, float to, float duration)
    {
        if (group == null) yield break;

        float t = 0f;
        while (t < duration && !skipRequested)
        {
            t += Time.unscaledDeltaTime;
            group.alpha = Mathf.Lerp(from, to, t / duration);
            yield return null;
        }
        group.alpha = to;
    }

    private IEnumerator Wait(float seconds)
    {
        float t = 0f;
        while (t < seconds && !skipRequested)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private void SetBars(float progress01)
    {
        if (axisBars == null) return;
        foreach (AxisBarView bar in axisBars)
        {
            if (bar != null) bar.SetProgress(progress01);
        }
    }

    private void SetButtonsInteractable(bool on)
    {
        if (buttonsGroup == null) return;
        buttonsGroup.interactable = on;
        buttonsGroup.blocksRaycasts = on;
    }

    private static void SetAlpha(CanvasGroup group, float alpha)
    {
        if (group != null) group.alpha = alpha;
    }
}

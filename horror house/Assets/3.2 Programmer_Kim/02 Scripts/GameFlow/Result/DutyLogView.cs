using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 결과창 — 야간 근무 일지.
/// ① 문서 머리: 근무일만 바뀌고 근무 시간 · 순찰 지점은 5일 내내 고정
/// ② 금일 근무 지침: 그날 받은 지침을 같은 순서로 보여 주고, 위반 · 미방문 항목에만 빨간 줄
/// ③ 특이사항: 입력란처럼 보이지만 편집할 수 없는 "없음"
/// ④ 꼬리: 면책 문구 · 확인 버튼 · 계약 잔여 일수. 확인은 빨간 줄이 다 그어진 뒤에만 눌린다.
/// 기획상 축 수치 · 막대 · 등급 · 준수 표시 · 위반 이유는 이 화면에 절대 넣지 않는다.
/// </summary>
public class DutyLogView : MonoBehaviour
{
    [Header("① 문서 머리")]
    [SerializeField] private TMP_Text dayText;
    [Tooltip("{0} = 현재 일차, {1} = 마지막 일차")]
    [SerializeField] private string dayFormat = "근무일 {0}일차 / {1}";
    [SerializeField] private TMP_Text hoursText;
    [SerializeField] private string hoursLabel = "근무 시간 12:00–06:00";
    [SerializeField] private TMP_Text patrolText;
    [SerializeField] private string patrolLabel = "순찰 지점 4곳";

    [Header("② 금일 근무 지침")]
    [Tooltip("복제해서 쓸 줄 템플릿 (비활성 상태로 둔다). 자식: Number, Body, Body/Strike")]
    [SerializeField] private GameObject rowTemplate;

    [Header("③ 특이사항")]
    [SerializeField] private TMP_Text specialNotesText;
    [SerializeField] private string specialNotes = "없음";

    [Header("④ 꼬리")]
    [SerializeField] private TMP_Text disclaimerText;
    [SerializeField, TextArea(2, 4)] private string disclaimer = "본 지침 미준수로 발생한 사고 및 신체적 손상에 대해\n회사는 책임지지 않습니다.";
    [SerializeField] private Button confirmButton;
    [SerializeField] private TMP_Text remainingText;
    [Tooltip("{0} = 남은 일수 (마지막 일차 − 현재 일차)")]
    [SerializeField] private string remainingFormat = "계약 잔여 {0}일";

    [Header("빨간 줄 연출 (초)")]
    [Tooltip("일지가 보인 뒤 첫 줄을 긋기 전까지 대기")]
    [SerializeField, Min(0f)] private float strikeStartDelay = 0.8f;
    [Tooltip("한 줄을 긋는 시간")]
    [SerializeField, Min(0f)] private float strikeDuration = 0.45f;
    [Tooltip("줄과 줄 사이 대기")]
    [SerializeField, Min(0f)] private float strikeInterval = 0.35f;

    /// <summary>확인 버튼을 눌렀을 때 한 번 발생</summary>
    public event Action Confirmed;

    public bool CanConfirm => canConfirm;

    private readonly List<GameObject> rows = new List<GameObject>();
    private readonly List<RectTransform> strikes = new List<RectTransform>();
    private readonly List<float> strikeWidths = new List<float>();
    private bool playing;
    private bool skipRequested;
    private bool canConfirm;
    private bool confirmed;

    private void Awake()
    {
        if (confirmButton != null) confirmButton.onClick.AddListener(Confirm);
        if (rowTemplate != null) rowTemplate.SetActive(false);
    }

    private void Update()
    {
        // 줄을 긋는 동안 아무 키나 클릭하면 남은 줄을 즉시 긋는다
        if (playing && !skipRequested && Input.anyKeyDown)
        {
            skipRequested = true;
        }
    }

    /// <summary>일지 내용을 채운다. 빨간 줄은 아직 긋지 않은 상태(길이 0)로 둔다.</summary>
    public void Show(DayResult result, int finalDay)
    {
        int day = result.Summary.Day;

        SetText(dayText, string.Format(dayFormat, day, finalDay));
        SetText(hoursText, hoursLabel);
        SetText(patrolText, patrolLabel);
        SetText(specialNotesText, specialNotes);
        SetText(disclaimerText, disclaimer);
        SetText(remainingText, string.Format(remainingFormat, Mathf.Max(0, finalDay - day)));

        BuildRows(result.LogLines);

        canConfirm = false;
        confirmed = false;
        if (confirmButton != null) confirmButton.interactable = false;
    }

    /// <summary>빨간 줄을 위에서부터 차례로 긋고, 다 그은 뒤 확인 버튼을 연다.</summary>
    public IEnumerator PlayStrikes()
    {
        playing = true;
        skipRequested = false;

        yield return Wait(strikeStartDelay);
        for (int i = 0; i < strikes.Count; i++)
        {
            yield return Draw(i);
            if (i < strikes.Count - 1) yield return Wait(strikeInterval);
        }

        for (int i = 0; i < strikes.Count; i++)
        {
            SetStrikeProgress(i, 1f);
        }
        playing = false;

        // 스킵용 클릭이 그대로 확인 버튼 클릭으로 이어지지 않도록, 마우스를 뗀 뒤에 버튼을 연다
        while (Input.GetMouseButton(0)) yield return null;

        canConfirm = true;
        if (confirmButton != null) confirmButton.interactable = true;
    }

    /// <summary>확인 버튼. 빨간 줄이 다 그어지기 전에는 무시된다.</summary>
    public void Confirm()
    {
        if (!canConfirm || confirmed) return;

        confirmed = true;
        if (confirmButton != null) confirmButton.interactable = false;
        Confirmed?.Invoke();
    }

    private void BuildRows(DutyLogLine[] lines)
    {
        foreach (GameObject row in rows)
        {
            if (row != null) Destroy(row);
        }
        rows.Clear();
        strikes.Clear();
        strikeWidths.Clear();

        if (rowTemplate == null || lines == null) return;

        Transform parent = rowTemplate.transform.parent;
        foreach (DutyLogLine line in lines)
        {
            GameObject row = Instantiate(rowTemplate, parent);
            row.SetActive(true);
            rows.Add(row);

            SetText(FindText(row.transform, "Number"), line.Number + ".");
            SetText(FindText(row.transform, "Body"), line.Text);

            RectTransform strike = row.transform.Find("Body/Strike") as RectTransform;
            if (strike == null) continue;

            strike.gameObject.SetActive(line.Struck);
            if (line.Struck) strikes.Add(strike);
        }

        // 레이아웃을 확정한 뒤, 빨간 줄의 목표 길이를 실제 글자 길이로 정한다
        LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)parent);
        foreach (RectTransform strike in strikes)
        {
            float width = 0f;
            TMP_Text body = strike.parent.GetComponent<TMP_Text>();
            if (body != null)
            {
                body.ForceMeshUpdate();
                width = Mathf.Clamp(body.textBounds.max.x - body.rectTransform.rect.xMin, 0f, body.rectTransform.rect.width);
            }
            strikeWidths.Add(width);
        }

        for (int i = 0; i < strikes.Count; i++)
        {
            SetStrikeProgress(i, 0f);
        }
    }

    private IEnumerator Draw(int index)
    {
        float t = 0f;
        while (t < strikeDuration && !skipRequested)
        {
            t += Time.unscaledDeltaTime;
            SetStrikeProgress(index, t / strikeDuration);
            yield return null;
        }
        SetStrikeProgress(index, 1f);
    }

    private void SetStrikeProgress(int index, float progress01)
    {
        RectTransform strike = strikes[index];
        strike.sizeDelta = new Vector2(strikeWidths[index] * Mathf.Clamp01(progress01), strike.sizeDelta.y);
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

    private static TMP_Text FindText(Transform parent, string childName)
    {
        Transform child = parent.Find(childName);
        return child != null ? child.GetComponent<TMP_Text>() : null;
    }

    private static void SetText(TMP_Text target, string value)
    {
        if (target != null) target.text = value;
    }
}

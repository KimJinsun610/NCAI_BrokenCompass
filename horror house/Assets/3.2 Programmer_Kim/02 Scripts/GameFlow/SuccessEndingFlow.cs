using System.Collections;
using NightDuty.SuccessEnding;
using UnityEngine;

/// <summary>
/// 성공 엔딩 씬(SuccessEnding)의 흐름 — 5일차 피날레에서 창밖 남자를 보지 않고 전화로 퇴근했을 때(<b>엔딩 2</b>).
///  1) <see cref="SuccessEndingSequence"/>(SuccessEnding_hyun의 내용)를 그대로 재생한다.
///  2) 끝나면(onEndingFinished) 엔딩 크레딧(HUD_EndingCredits_Design, 「엔딩 2」)을 띄운다 — 진행은 <see cref="EndingCredits"/>(엔딩 1과 공용).
///  3) 크레딧을 넘기면 메인으로.
/// 시퀀스·크레딧 프리팹은 고치지 않는다(이벤트에 런타임으로 붙기만 한다).
/// </summary>
[DisallowMultipleComponent]
public sealed class SuccessEndingFlow : MonoBehaviour
{
    /// <summary>이 씬이 보여 주는 엔딩 번호(미응시 = 엔딩 2).</summary>
    public const int EndingNumber = 2;

    [Tooltip("비우면 씬에서 찾는다.")]
    [SerializeField] private SuccessEndingSequence sequence;
    [SerializeField] private GameObject creditsPrefab;

    [Header("크레딧")]
    [Tooltip("크레딧의 검은 덮개(Fade)가 걷히는 시간(초).")]
    [SerializeField, Min(0f)] private float creditsFadeIn = 1.5f;
    [Tooltip("QUIT 글자를 못 찾을 때 이 시간이 지나면 버튼을 켠다(프리팹 DOTween 2.5 + 4초).")]
    [SerializeField, Min(0f)] private float skipAfter = 6.5f;
    [Tooltip("크레딧이 뜬 뒤 이 시간이 지나면 저절로 메인으로. 0이면 저절로 넘어가지 않는다.")]
    [SerializeField, Min(0f)] private float autoReturnAfter = 15f;
    [Tooltip("메인으로 가기 전 검게 덮는 시간(초).")]
    [SerializeField, Min(0f)] private float fadeOutSeconds = 1f;

    private bool _creditsShown;

    private void Awake()
    {
        if (sequence == null) sequence = FindAnyObjectByType<SuccessEndingSequence>();
    }

    private void OnEnable()
    {
        if (sequence != null) sequence.onEndingFinished.AddListener(ShowCredits);
    }

    private void OnDisable()
    {
        if (sequence != null) sequence.onEndingFinished.RemoveListener(ShowCredits);
    }

    /// <summary>엔딩이 끝났다 — 크레딧을 띄운다(한 번만).</summary>
    public void ShowCredits()
    {
        if (_creditsShown) return;
        _creditsShown = true;
        StartCoroutine(CreditsRoutine());
    }

    private IEnumerator CreditsRoutine()
    {
        EndingCredits credits = new EndingCredits
        {
            FadeInSeconds = creditsFadeIn,
            SkipAfter = skipAfter,
            AutoReturnAfter = autoReturnAfter,
            FadeOutSeconds = fadeOutSeconds
        };
        yield return credits.Play(creditsPrefab, EndingNumber);
        SceneFlow.GoTo(GameScene.Main);
    }
}

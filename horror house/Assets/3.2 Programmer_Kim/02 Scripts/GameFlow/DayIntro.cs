using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Play 씬 시작 연출: 검은 화면에 "Day n" 표시 → 페이드 아웃 → 게임 시작.
/// 연출 동안 게임 시간(GameTime)과 플레이어 조작(FPController)을 멈춰 둔다.
/// PlaySystems 프리팹의 Canvas_DayIntro에 붙어 있다.
/// </summary>
public class DayIntro : MonoBehaviour
{
    [Header("UI")]
    [Tooltip("검은 배경과 텍스트를 묶은 그룹. alpha 1 = 가림")]
    [SerializeField] private CanvasGroup overlay;
    [SerializeField] private TMP_Text dayText;
    [Tooltip("{0} = 현재 일차")]
    [SerializeField] private string dayFormat = "Day {0}";

    [Header("연출 시간 (초)")]
    [Tooltip("검은 화면에서 글자가 나타나는 시간")]
    [SerializeField, Min(0f)] private float textFadeInDuration = 0.8f;
    [Tooltip("글자가 다 보인 뒤 유지하는 시간")]
    [SerializeField, Min(0f)] private float holdDuration = 1.5f;
    [Tooltip("검은 화면과 글자가 함께 사라지는 시간")]
    [SerializeField, Min(0f)] private float fadeOutDuration = 1.0f;

    [Header("연출 중 멈출 것")]
    [Tooltip("비워 두면 씬에서 자동으로 찾는다.")]
    [SerializeField] private GameTime gameTime;
    [Tooltip("켜면 연출 동안 FPController를 꺼서 이동 · 시점 · 일시정지 입력을 막는다.")]
    [SerializeField] private bool lockPlayer = true;

    private readonly List<Behaviour> lockedPlayers = new List<Behaviour>();

    /// <summary>연출이 진행 중인지</summary>
    public bool IsPlaying { get; private set; }

    private void Awake()
    {
        IsPlaying = true;

        if (overlay != null)
        {
            overlay.alpha = 1f;
            overlay.blocksRaycasts = true;
        }
        if (dayText != null)
        {
            dayText.text = string.Format(dayFormat, GameSession.CurrentDay);
            dayText.alpha = 0f;
        }

        // 다른 오브젝트의 Update가 돌기 전에 멈춰 둔다
        if (gameTime == null) gameTime = FindAnyObjectByType<GameTime>();
        if (gameTime != null) gameTime.SetRunning(false);

        if (lockPlayer)
        {
            foreach (FPController player in FindObjectsByType<FPController>(FindObjectsSortMode.None))
            {
                if (!player.enabled) continue;
                player.enabled = false;
                lockedPlayers.Add(player);
            }

            // FPController를 끄면 OnDisable에서 커서가 다시 보이므로, 연출 동안은 숨긴다 (끝나면 FPController가 원래대로 되돌림)
            Cursor.visible = false;
        }
    }

    private IEnumerator Start()
    {
        // 씬 활성화 직후 프레임은 시간 간격이 커서 연출이 건너뛰어지므로 한 프레임 쉰다
        yield return null;

        yield return Animate(a => { if (dayText != null) dayText.alpha = a; }, 0f, 1f, textFadeInDuration);
        yield return Wait(holdDuration);
        yield return Animate(a => { if (overlay != null) overlay.alpha = a; }, 1f, 0f, fadeOutDuration);

        Finish();
    }

    private void Finish()
    {
        foreach (Behaviour player in lockedPlayers)
        {
            if (player != null) player.enabled = true;
        }
        lockedPlayers.Clear();

        if (gameTime != null) gameTime.SetRunning(true);

        IsPlaying = false;

        if (overlay != null)
        {
            overlay.blocksRaycasts = false;
            overlay.gameObject.SetActive(false);
        }
    }

    private static IEnumerator Animate(Action<float> apply, float from, float to, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            apply(Mathf.Lerp(from, to, t / duration));
            yield return null;
        }
        apply(to);
    }

    private static IEnumerator Wait(float seconds)
    {
        float t = 0f;
        while (t < seconds)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }
    }
}

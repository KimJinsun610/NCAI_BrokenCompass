using System;
using UnityEngine;

/// <summary>
/// 게임 일시정지를 한곳에서 관리한다.
///
/// <b>규칙: 일시정지하면 일시정지 메뉴를 뺀 모든 게임 플레이가 멈춘다.</b>
/// 시간뿐 아니라 <b>소리도</b> 멈춘다 — 소리는 <see cref="Time.timeScale"/>과 아무 상관이 없어서,
/// 시간만 멈추면 태블릿 알람처럼 이미 재생 중이던 것이 계속 울린다(2026-09-23에 실제로 그랬다).
///
/// 멈추는 것:
/// - 게임 시간(<see cref="Time.timeScale"/> = 0) → GameTime · 판정 Tick · 애니메이션 · 물리
/// - <b>모든 소리</b>(<see cref="AudioListener.pause"/>) → 알람 · 발소리 · 환경음
/// - 연출 시계(<see cref="ViewmodelTime"/>가 timeScale을 보고 스스로 멈춘다)
/// - 플레이어 조작(FPController가 <see cref="IsPaused"/>를 보고 시점·이동·태블릿 입력을 건너뛴다)
///
/// <b>일시정지 메뉴에 소리를 붙일 때는 그 AudioSource에 `ignoreListenerPause = true`를 켜야 한다.</b>
/// 그러지 않으면 버튼 소리까지 같이 멈춰서 메뉴가 먹통처럼 느껴진다.
/// </summary>
public static class GamePause
{
    /// <summary>지금 일시정지 중인가.</summary>
    public static bool IsPaused { get; private set; }

    /// <summary>일시정지가 켜지거나 꺼질 때 알린다. 인자는 켜졌는지 여부.</summary>
    public static event Action<bool> Changed;

    /// <summary>일시정지를 켜고 끈다. 같은 값이면 아무 일도 하지 않는다.</summary>
    public static void Set(bool paused)
    {
        if (IsPaused == paused) return;

        IsPaused = paused;
        Time.timeScale = paused ? 0f : 1f;
        AudioListener.pause = paused;

        if (Changed != null) Changed(paused);
    }

    /// <summary>
    /// 무조건 원상복구한다. 씬을 옮길 때 부른다.
    /// 일시정지 중에 사망·근무 종료로 씬이 바뀌면 다음 씬이 멈춘 채, 소리도 꺼진 채 시작한다.
    /// </summary>
    public static void Clear()
    {
        IsPaused = false;
        Time.timeScale = 1f;
        AudioListener.pause = false;
    }

    /// <summary>
    /// 도메인 리로드를 끄면 static 값이 플레이 사이에 살아남는다.
    /// 일시정지 상태로 플레이를 껐다가 다시 켜면 처음부터 멈춰 있게 되므로 여기서 되돌린다.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        IsPaused = false;
        Changed = null;
        Time.timeScale = 1f;
        AudioListener.pause = false;
    }
}

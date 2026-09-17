using UnityEngine;

/// <summary>
/// 씬이 바뀌어도 유지되는 회차 진행 정보 (현재 일차, 직전 결과).
/// Play 씬이 사라져도 Result 씬이 결과를 읽을 수 있게 여기에 보관한다.
/// </summary>
public static class GameSession
{
    /// <summary>마지막 일차. 이 날을 완료하면 결과창에 "다음 근무" 없이 "메인으로"만 표시한다.</summary>
    public const int FinalDay = 5;

    public static int CurrentDay { get; private set; } = 1;

    public static bool HasResult { get; private set; }

    public static DayResult LastResult { get; private set; }

    /// <summary>새 회차 시작 — 메인의 시작 버튼, 사망 후 "처음부터"</summary>
    public static void StartNewRun()
    {
        CurrentDay = 1;
        NightDuty.NightRun.StartNewRun(); // 판정 코어의 회차(4축)도 함께 새로 시작
        ClearResult();
    }

    /// <summary>다음 날로 진행 — 근무 완료 후 "다음 근무"</summary>
    public static void AdvanceDay()
    {
        CurrentDay = Mathf.Min(CurrentDay + 1, FinalDay);
        ClearResult();
    }

    /// <summary>테스트용: 일차를 직접 지정한다 (1 ~ FinalDay)</summary>
    public static void SetDay(int day)
    {
        CurrentDay = Mathf.Clamp(day, 1, FinalDay);
        ClearResult();
    }

    public static void SetResult(DayResult result)
    {
        LastResult = result;
        HasResult = true;
    }

    private static void ClearResult()
    {
        LastResult = default;
        HasResult = false;
    }

    // Domain Reload를 끈 상태에서도 이전 플레이 세션의 값이 남지 않도록 초기화
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        CurrentDay = 1;
        ClearResult();
    }
}

using NightDuty;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 플레이 모드 테스트용 메뉴 (Tools > Programmer_Kim > Debug).
/// 판정 시스템 없이 근무 종료 · 사망 · 결과창 버튼 동작을 확인할 때 쓴다.
/// </summary>
public static class GameFlowDebugMenu
{
    private const string MenuRoot = "Tools/Programmer_Kim/Debug/";

    [MenuItem(MenuRoot + "End Shift Now")]
    private static void EndShiftNow()
    {
        GameTime gameTime = Object.FindAnyObjectByType<GameTime>();
        if (gameTime == null)
        {
            Debug.LogWarning("[GameFlowDebug] 씬에 GameTime이 없습니다.");
            return;
        }
        gameTime.SkipToEnd();
    }

    [MenuItem(MenuRoot + "Force Death (Auditory)")]
    private static void ForceDeath()
    {
        EventBus.RaiseAxisCritical(FearAxis.Auditory);
    }

    [MenuItem(MenuRoot + "Result Next Button")]
    private static void ResultNext()
    {
        ResultController controller = Object.FindAnyObjectByType<ResultController>();
        if (controller != null) controller.OnNextClicked();
        else Debug.LogWarning("[GameFlowDebug] 씬에 ResultController가 없습니다.");
    }

    [MenuItem(MenuRoot + "Result Main Button")]
    private static void ResultMain()
    {
        ResultController controller = Object.FindAnyObjectByType<ResultController>();
        if (controller != null) controller.OnMainClicked();
        else Debug.LogWarning("[GameFlowDebug] 씬에 ResultController가 없습니다.");
    }

    [MenuItem(MenuRoot + "Result Confirm (Duty Log)")]
    private static void ResultConfirm()
    {
        DutyLogView dutyLog = Object.FindAnyObjectByType<DutyLogView>();
        if (dutyLog == null)
        {
            Debug.LogWarning("[GameFlowDebug] 씬에 활성화된 근무 일지(DutyLogView)가 없습니다.");
            return;
        }
        if (!dutyLog.CanConfirm)
        {
            Debug.LogWarning("[GameFlowDebug] 빨간 줄이 아직 다 그어지지 않아 확인할 수 없습니다.");
            return;
        }
        dutyLog.Confirm();
    }

    [MenuItem(MenuRoot + "End Shift Now", true)]
    [MenuItem(MenuRoot + "Force Death (Auditory)", true)]
    [MenuItem(MenuRoot + "Result Next Button", true)]
    [MenuItem(MenuRoot + "Result Main Button", true)]
    [MenuItem(MenuRoot + "Result Confirm (Duty Log)", true)]
    private static bool IsPlaying()
    {
        return EditorApplication.isPlaying;
    }
}

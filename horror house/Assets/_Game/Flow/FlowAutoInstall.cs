using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 근무 씬 자동 설치의 공통 판단. <see cref="NightRunDriver"/>·<see cref="TabletBridge"/>·
/// <see cref="EncounterStager"/>가 각자 쓰던 「근무 씬인가」 판단과 카메라 찾기를 한 곳에 모은다.
///
/// <para><b>왜 필요했나 (2026-09-24).</b> PlayScene에 <c>PlayerSensors</c>·<c>FlashlightRelay</c>·
/// <c>DoorRelay</c>·<c>AnomalyCueDirector</c>가 저장돼 있지 않아, 정식 흐름(메인 → 로딩 → 플레이)으로
/// 들어오면 판정 코어는 밤을 열지만 <b>신호를 만드는 쪽이 통째로 비어 있었다</b>.
/// 그동안 테스트 하네스가 런타임에 대신 만들어 주고 있었을 뿐이다.
/// 씬 파일을 건드리면 LFS 잠금·병합 충돌이 생기므로, 기존 자동 설치 패턴을 센서에도 넓혔다.</para>
///
/// <para><b>판단 기준은 「게임 시계가 있는 씬」</b>으로 통일한다 — NightRunDriver가 쓰던 기준 그대로다.
/// 메인·로딩·결과·계약 씬에는 GameTime이 없으므로 아무것도 설치되지 않는다.</para>
/// </summary>
public static class FlowAutoInstall
{
    /// <summary>근무 씬인가. 기준은 그 씬에 <see cref="GameTime"/>이 있는지.</summary>
    public static bool IsDutyScene(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return false;
        }

        GameTime[] clocks = Object.FindObjectsByType<GameTime>(FindObjectsSortMode.None);
        for (int i = 0; i < clocks.Length; i++)
        {
            if (clocks[i] != null && clocks[i].gameObject.scene == scene)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 그 씬의 플레이어 카메라. <c>Camera.main</c>을 먼저 보고, 씬이 다르면 씬 안의 아무 카메라나 쓴다.
    /// 없으면 null.
    /// </summary>
    public static Camera FindCamera(Scene scene)
    {
        Camera main = Camera.main;
        if (main != null && main.gameObject.scene == scene)
        {
            return main;
        }

        Camera[] cams = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < cams.Length; i++)
        {
            if (cams[i] != null && cams[i].gameObject.scene == scene && cams[i].enabled)
            {
                return cams[i];
            }
        }

        return null;
    }

    /// <summary>씬 안에 그 타입이 이미 하나라도 있는지. 자동 설치 전에 중복을 막는다.</summary>
    public static bool Exists<T>(Scene scene) where T : Component
    {
        T[] found = Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < found.Length; i++)
        {
            if (found[i] != null && found[i].gameObject.scene == scene)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 빈 GameObject를 만들어 그 씬으로 옮기고 컴포넌트를 붙인다.
    /// 이름 끝에 <c>(auto)</c>를 달아 씬에 저장된 것과 구분한다.
    /// </summary>
    public static T CreateHost<T>(Scene scene, string name) where T : Component
    {
        GameObject go = new GameObject(name);
        SceneManager.MoveGameObjectToScene(go, scene);
        return go.AddComponent<T>();
    }
}

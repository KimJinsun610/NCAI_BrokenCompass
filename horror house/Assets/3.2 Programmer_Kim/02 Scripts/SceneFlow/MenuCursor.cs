using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 메뉴 씬(메인 · 계약 · 결과)에서는 마우스 커서를 항상 보이고 잠그지 않는다.
/// <para>
/// 씬 파일에 놓지 않는다 — 씬이 열릴 때 <see cref="SceneFlowConfig"/>의 Main · Contract · Result 경로와 비교해
/// 맞으면 스스로 생긴다(씬이 내려가면 함께 사라진다). 씬 파일을 바꿔도 Config만 맞으면 따라간다.
/// </para>
/// <para>
/// 왜 매 프레임인가: 커서를 숨기는 쪽이 여럿이다.
/// ① 로딩 씬(<see cref="LoadingController"/>)이 숨긴 채 다음 씬으로 넘어간다 — 메인 씬에는 다시 켜는 코드가 없었다.
/// ② 씬을 넘어 살아남는 오브젝트(사망 컷신 · 개발자 모드 패널 · 디버그 콘솔)가 나중에 숨길 수 있다.
/// 씬마다 Start에서 한 번 켜는 것으로는 ②를 막지 못한다. 그래서 프레임 마지막(LateUpdate)에 되돌린다.
/// </para>
/// </summary>
[DefaultExecutionOrder(10000)] // 다른 LateUpdate(커서를 숨기는 컷신 등)보다 뒤에
public class MenuCursor : MonoBehaviour
{
    private static readonly GameScene[] MenuScenes = { GameScene.Main, GameScene.Contract, GameScene.Result };

    private void OnEnable() => Show();
    private void LateUpdate() => Show();

    private static void Show()
    {
        if (!Cursor.visible) Cursor.visible = true;
        if (Cursor.lockState != CursorLockMode.None) Cursor.lockState = CursorLockMode.None;
    }

    // ───────────────────────── 자동 설치 ─────────────────────────

    // 도메인 리로드를 꺼도 구독이 겹치지 않게 매 플레이 시작에 다시 건다
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetHook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    // 첫 씬의 sceneLoaded도 받도록 씬 로드 전에 구독한다
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!IsMenuScene(scene.path)) return;

        // 같은 씬에 이미 있으면(추가 로드 등) 하나만 둔다
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.GetComponent<MenuCursor>() != null) return;
        }

        var go = new GameObject("MenuCursor (auto)");
        SceneManager.MoveGameObjectToScene(go, scene);
        go.AddComponent<MenuCursor>();
    }

    private static bool IsMenuScene(string scenePath)
    {
        SceneFlowConfig config = SceneFlow.Config;
        if (config == null || string.IsNullOrEmpty(scenePath)) return false;

        foreach (GameScene menu in MenuScenes)
        {
            if (SamePath(config.GetPath(menu), scenePath)) return true;
        }
        return false;
    }

    private static bool SamePath(string a, string b)
    {
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return false;
        return string.Equals(a.Replace('\\', '/'), b.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase);
    }
}

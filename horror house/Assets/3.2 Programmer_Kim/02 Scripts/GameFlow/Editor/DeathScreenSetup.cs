using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HUD_Death를 사망 화면으로 맞추고 PlaySystems의 PlayResultRouter에 연결한다 (Tools > Programmer_Kim > Game Flow).
/// 기존 문구 · 연결이 처음 상태일 때만 바꾸므로 여러 번 실행해도 인스펙터에서 고친 값을 덮어쓰지 않는다.
/// </summary>
public static class DeathScreenSetup
{
    private const string Root = "Assets/3.2 Programmer_Kim";
    private const string DeathPrefabPath = Root + "/03 Prefebs/01 UI/HUD_Death.prefab";
    private const string PlaySystemsPrefabPath = Root + "/03 Prefebs/03 System/PlaySystems.prefab";

    // 결과창 페이드(200)보다 위
    private const int SortingOrder = 300;

    [MenuItem("Tools/Programmer_Kim/Game Flow/Setup Death Screen")]
    public static void Setup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[DeathScreenSetup] 플레이 모드에서는 실행할 수 없습니다.");
            return;
        }

        FixDeathPrefab();
        LinkToPlaySystems();
        AssetDatabase.SaveAssets();
    }

    private static void FixDeathPrefab()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(DeathPrefabPath);
        try
        {
            bool changed = false;

            Canvas canvas = root.GetComponent<Canvas>();
            if (canvas != null && canvas.sortingOrder < SortingOrder)
            {
                canvas.sortingOrder = SortingOrder;
                changed = true;
            }

            changed |= ReplaceText(root.transform, "Txt_Death", "DUTY ENDED", "YOU DIED");
            changed |= ReplaceText(root.transform, "Btn_Main", "Go To Main", "Restart");
            changed |= ReplaceText(root.transform, "Btn_Quite", "Quite", "Quit");

            HUDActions actions = root.GetComponent<HUDActions>();
            Transform mainTf = FindDeep(root.transform, "Btn_Main");
            Button mainButton = mainTf != null ? mainTf.GetComponent<Button>() : null;
            if (actions != null && mainButton != null)
            {
                bool wired = false;
                for (int i = mainButton.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
                {
                    string method = mainButton.onClick.GetPersistentMethodName(i);
                    if (method == nameof(HUDActions.GoToMain))
                    {
                        wired = true;
                    }
                    else if (method == nameof(HUDActions.LoadScene))
                    {
                        // SceneManager로 바로 로드하던 호출 → SceneFlow(로딩 · timeScale 복구)로 교체
                        UnityEventTools.RemovePersistentListener(mainButton.onClick, i);
                        changed = true;
                    }
                }

                if (!wired)
                {
                    UnityEventTools.AddPersistentListener(mainButton.onClick, actions.GoToMain);
                    changed = true;
                }
            }
            else
            {
                Debug.LogWarning("[DeathScreenSetup] HUD_Death에서 HUDActions 또는 Btn_Main을 찾지 못해 Restart 연결을 건너뜁니다.");
            }

            if (changed)
            {
                PrefabUtility.SaveAsPrefabAsset(root, DeathPrefabPath);
                Debug.Log("[DeathScreenSetup] HUD_Death 수정: YOU DIED / Restart → 메인 / Quit, 정렬 순서 " + SortingOrder);
            }
            else
            {
                Debug.Log("[DeathScreenSetup] HUD_Death가 이미 설정돼 있어 건너뜁니다.");
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void LinkToPlaySystems()
    {
        GameObject deathPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DeathPrefabPath);
        GameObject root = PrefabUtility.LoadPrefabContents(PlaySystemsPrefabPath);
        try
        {
            PlayResultRouter router = root.GetComponent<PlayResultRouter>();
            if (router == null)
            {
                Debug.LogWarning("[DeathScreenSetup] PlaySystems에서 PlayResultRouter를 찾지 못했습니다.");
                return;
            }

            SerializedObject so = new SerializedObject(router);
            SerializedProperty prop = so.FindProperty("deathScreenPrefab");
            if (prop.objectReferenceValue != null)
            {
                Debug.Log("[DeathScreenSetup] PlaySystems에 사망 화면이 이미 연결돼 있어 건너뜁니다.");
                return;
            }

            prop.objectReferenceValue = deathPrefab;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, PlaySystemsPrefabPath);
            Debug.Log("[DeathScreenSetup] PlaySystems PlayResultRouter ← HUD_Death 연결");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// <summary>name 오브젝트 아래 첫 TMP 글자가 from으로 시작하면 to로 바꾼다.</summary>
    private static bool ReplaceText(Transform root, string name, string from, string to)
    {
        Transform target = FindDeep(root, name);
        TMP_Text text = target != null ? target.GetComponentInChildren<TMP_Text>(true) : null;
        if (text == null)
        {
            Debug.LogWarning($"[DeathScreenSetup] HUD_Death에서 {name} 글자를 찾지 못했습니다.");
            return false;
        }

        if (!text.text.Trim().StartsWith(from)) return false;
        text.text = to;
        return true;
    }

    private static Transform FindDeep(Transform parent, string name)
    {
        if (parent.name == name) return parent;
        foreach (Transform child in parent)
        {
            Transform found = FindDeep(child, name);
            if (found != null) return found;
        }
        return null;
    }
}

using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 104차 배치 지도(복도): 런타임에 세울 소품을 <c>Resources/LayoutProps</c>에 프리팹으로 만든다 — 원본(벤더·김진선님 프리팹)은 고치지 않고
/// 인스턴스를 풀어(unpack) 고정(static) 표시·충돌체를 뗀 사본으로 저장한다. 씬 소품을 런타임에 복제하면 정적 배칭 메시가 딸려 와 깨지므로 따로 둔다.
/// </summary>
public static class LayoutPropsBuilder
{
    public const string OutDir = "Assets/_Game/Resources/LayoutProps";

    /// <summary>(저장 이름, 원본 프리팹 경로, 충돌체를 뗄지).</summary>
    public static readonly string[,] Sources =
    {
        { "walled_door", "Assets/NOT_Lonely/HQ_AbandonedSchool/Prefabs/WalledDoor.prefab", "1" },
        { "ceiling_chair", "Assets/NOT_Lonely/HQ_AbandonedSchool/Prefabs/TeacherChair.prefab", "1" },
        { "blood_drip", "Assets/3.2 Programmer_Kim/03 Prefebs/04 Horror/Bloody/HorrorProp_CeilingBloodDrip.prefab", "1" },
        // 104차 배치 지도(교실 — 사용자: 「천장에 책상과 의자, 벽시계 복제, 칠판 복제, 핏물」)
        { "ceiling_desk", "Assets/NOT_Lonely/HQ_AbandonedSchool/Prefabs/StudentDeskB_Plastic_RED.prefab", "1" },
        { "wall_clock", "Assets/NOT_Lonely/HQ_AbandonedSchool/Prefabs/ClockC.prefab", "1" },
        { "blackboard", "Assets/NOT_Lonely/HQ_AbandonedSchool/Prefabs/BlackboardSmall.prefab", "1" },
        { "blood_pool", "Assets/3.2 Programmer_Kim/03 Prefebs/04 Horror/Bloody/BloodyFontain/Decal_WetSpot_DarkRed.prefab", "1" },
    };

    [MenuItem("야간근무/연출/배치 지도 소품 다시 만들기")]
    public static void BuildMenu()
    {
        Debug.Log("[LayoutProps] " + Build());
    }

    public static string Build()
    {
        if (!AssetDatabase.IsValidFolder(OutDir)) AssetDatabase.CreateFolder("Assets/_Game/Resources", "LayoutProps");
        int made = 0;
        string missing = string.Empty;
        for (int i = 0; i < Sources.GetLength(0); i++)
        {
            GameObject src = AssetDatabase.LoadAssetAtPath<GameObject>(Sources[i, 1]);
            if (src == null)
            {
                missing += " " + Sources[i, 0];
                continue;
            }

            GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(src);
            try
            {
                PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                inst.name = Sources[i, 0];
                foreach (Transform t in inst.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);
                if (Sources[i, 2] == "1")
                {
                    foreach (Collider c in inst.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
                }

                PrefabUtility.SaveAsPrefabAsset(inst, OutDir + "/" + Sources[i, 0] + ".prefab");
                made++;
            }
            finally
            {
                Object.DestroyImmediate(inst);
            }
        }

        AssetDatabase.SaveAssets();
        return made + "개" + (missing.Length > 0 ? " · 없음:" + missing : string.Empty);
    }
}

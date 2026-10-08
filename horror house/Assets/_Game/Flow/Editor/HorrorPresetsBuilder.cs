using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 66차: <see cref="HorrorPresetsSO"/>를 채운다 — 김진선님 폴더의 공포 연출 프리셋(민이 알려 준 여섯 개). 경로가 바뀌면 여기만 고친다.
/// </summary>
public static class HorrorPresetsBuilder
{
    public const string AssetPath = "Assets/_Game/Resources/HorrorPresets.asset";

    private const string Folder = "Assets/3.2 Programmer_Kim/03 Prefebs/04 Horror/";

    /// <summary>프리셋 이름(민 2026-10-08: 「HorrorEvent_CabinetCreak · CabinetBang · CabinetRampage · DrawerRampage · DoorSlam · OpenDoorSlam 이렇게가 진선님의 연출 프리셋」).</summary>
    public static readonly string[] Names =
    {
        "HorrorEvent_CabinetCreak", "HorrorEvent_CabinetBang", "HorrorEvent_CabinetRampage",
        "HorrorEvent_DrawerRampage", "HorrorEvent_DoorSlam", "HorrorEvent_OpenDoorSlam"
    };

    [MenuItem("야간근무/연출/진선님 연출 프리셋 표 다시 만들기")]
    public static void BuildMenu()
    {
        Debug.Log("[HorrorPresets] " + Build());
    }

    /// <summary>표를 다시 만들고 요약을 돌려준다.</summary>
    public static string Build()
    {
        List<HorrorPresetsSO.Entry> list = new List<HorrorPresetsSO.Entry>();
        List<string> missing = new List<string>();
        for (int i = 0; i < Names.Length; i++)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + Names[i] + ".prefab");
            if (prefab == null)
            {
                missing.Add(Names[i]);
                continue;
            }

            HorrorPresetsSO.Entry e;
            e.name = Names[i];
            e.prefab = prefab;
            list.Add(e);
        }

        HorrorPresetsSO so = AssetDatabase.LoadAssetAtPath<HorrorPresetsSO>(AssetPath);
        if (so == null)
        {
            so = ScriptableObject.CreateInstance<HorrorPresetsSO>();
            AssetDatabase.CreateAsset(so, AssetPath);
        }

        so.Entries = list.ToArray();
        EditorUtility.SetDirty(so);
        AssetDatabase.SaveAssets();
        return list.Count + "개" + (missing.Count > 0 ? " · 없음: " + string.Join(", ", missing) : string.Empty);
    }
}

using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// <c>Resources/DoorPolicy.asset</c>을 만든다. 메뉴 「NightDuty/문 개폐 정책 에셋 생성」.
///
/// <para><b>여기 적힌 목록이 정본은 아닙니다.</b> 씬을 실측해 「기획이 쓰는 문」으로 보이는 것을 추린
/// <b>첫 안</b>입니다(2026-09-22). 민이 걸어 보고 고치십시오 — 인스펙터에서 바로 됩니다.</para>
///
/// <para>추린 기준은 둘입니다. ⑴ 판정에 쓰이는 문(<c>JudgeTarget</c>이 붙은 것)은 표에 없어도 열립니다.
/// ⑵ 순찰 동선상 지나야 하는 공간 출입문과 정문을 경로로 적었습니다.</para>
/// </summary>
public static class DoorPolicyBuilder
{
    private const string Dir = "Assets/_Game/Resources";
    private const string Path = Dir + "/DoorPolicy.asset";

    /// <summary>
    /// 동선상 열려야 하는 문. <b>씬 하이어라키 경로</b>로 적는다 —
    /// 이름만으로는 <c>DoorBack</c>이 셋, <c>Bookcase</c>가 열한 개라 구분이 안 된다.
    /// </summary>
    private static readonly string[] OpenablePaths =
    {
        // 정문 — 퇴실은 여기로 나간다. 뒷문 DoorBack (55.0, 1.5, 46.4)은 잠겨 있다.
        "Exterior/Doors/DoorMain",

        // 화장실 — 존은 x -2~6 · z 32~36이고, 실제로 그 자리에 있는 것은 Toilet02 그룹이다.
        "Interior/Toilet02/DoorNarrowSolid (4)",          // toilet.door, 화장실 출입
        "Interior/Toilet02/ToiletCabin_openable (2)",     // toilet.stall.outer — T1·T3
        "Interior/Toilet02/ToiletCabin_openable (4)",     // toilet.stall.inner — T1·T3

        // 복도
        "Interior/Corridors/DoorNarrowSolid (8)",         // corridor.door.auto — H1 자동 개방
        "Interior/Corridors/DoorNarrowSolid (3)",         // corridor.door.back — H2
        "Interior/Corridors/DoorNarrow (3)",              // 복도 중간 (36.0, 43.9)

        // 과학실 — 존은 x 42~54 · z 40~44. 북쪽 경계가 복도와 맞닿는다.
        "Interior/Corridors/DoorNarrowSolid (9)",         // 과학실 출입 (52.0, 44.1)

        // 교실 1-1 — 존은 x 38~50 · z 48~56. 남쪽 경계가 복도와 맞닿는다.
        "Interior/Classroom01/DoorNarrow",                // 교실 1-1 출입 (48.0, 47.9)
        "Interior/Toilet01/DoorNarrowSolid"               // 복도 동쪽 끝 (52.0, 47.9)
    };

    /// <summary>
    /// 판정 ID로도 허용한다. 경로가 바뀌어도 ID는 남으므로 이쪽이 더 질기다.
    /// <c>judgeTargetsAreOpenable</c>이 켜져 있으면 사실 없어도 되지만, 꺼도 이 문들은 열리게 둔다.
    /// </summary>
    private static readonly string[] OpenableIds =
    {
        "corridor.door.auto", "corridor.door.back", "corridor.door.11", "corridor.door.13",
        "science.door", "toilet.door", "toilet.stall.outer", "toilet.stall.inner"
    };

    [MenuItem("NightDuty/문 개폐 정책 에셋 생성")]
    public static void Build()
    {
        if (!Directory.Exists(Dir))
        {
            Directory.CreateDirectory(Dir);
            AssetDatabase.Refresh();
        }

        DoorPolicySO asset = AssetDatabase.LoadAssetAtPath<DoorPolicySO>(Path);
        bool created = false;

        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<DoorPolicySO>();
            AssetDatabase.CreateAsset(asset, Path);
            created = true;
        }

        SerializedObject so = new SerializedObject(asset);
        WriteArray(so.FindProperty("openablePaths"), OpenablePaths);
        WriteArray(so.FindProperty("openableIds"), OpenableIds);
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[문 정책] " + (created ? "만들었습니다" : "갱신했습니다") + " — 열리는 문 경로 " +
                  OpenablePaths.Length + "개 · 판정 ID " + OpenableIds.Length + "개. " + Path, asset);
    }

    private static void WriteArray(SerializedProperty prop, string[] values)
    {
        if (prop == null)
        {
            Debug.LogWarning("[문 정책] 필드를 찾지 못했습니다(이름이 바뀐 듯합니다).");
            return;
        }

        prop.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
        {
            prop.GetArrayElementAtIndex(i).stringValue = values[i];
        }
    }
}

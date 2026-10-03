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
        // 2026-10-01 민: 「쓰는 문만 열고 나머지는 잠근다」. 기준 = 근무 공간(복도·교실 1-3·과학실·화장실·도서관·경비실)의 출입문. 건물 정문(Exterior/Doors/DoorMain)은 열지 않는다(민, 2026-10-02). 교실 1-1은 쓰지 않는다(민, 2026-10-01) — 문을 잠근다.
        // 문 양쪽 1m를 SpaceZones 상자로 재서 추렸다. 교실 1-3은 문 없는 출입구라 표에 없다.
        "Interior/Corridors/DoorNarrow (3)",            // 경비실 출입 (36.0, 43.9)
        "Interior/Corridors/DoorWide (2)",              // 도서관 출입 (13.1, 42.0) — 문간의 노란 얼굴(L3)
        "Interior/Corridors/DoorNarrowSolid (9)",       // 과학실 출입 (52.0, 44.1) — science.door
        "Interior/Corridors/DoorNarrowSolid (8)",       // 과학실 둘째 문 (44.0, 44.1) — corridor.door.auto, H2 자동 개방 우선
        "Interior/Toilet02/DoorNarrowSolid (4)",        // 화장실 출입 — toilet.door
        "Interior/Toilet02/ToiletCabin_openable (2)",   // 바깥쪽 칸 — toilet.stall.outer
        "Interior/Toilet02/ToiletCabin_openable (4)"    // 안쪽 칸 — toilet.stall.inner(T2 사용 중 칸)
        // 잠금(표에 없음): 뒷문 셋 · 2층 문 · 복도 남쪽·서쪽 문(DoorNarrow (1), DoorWide, DoorWide (1), DoorNarrowSolid (2)·(3)·(5)·(6)) ·
        // 사물함실 · 안 쓰는 화장실(Toilet01) 출입문과 칸 · 교실 1-1 출입문(Classroom01/DoorNarrow) · 건물 정문(DoorMain). corridor.door.back(옛 H2 카드 문)도 쓰지 않는 문이라 잠근다.
    };

    /// <summary>판정 ID로도 허용한다(경로가 바뀌어도 ID는 남는다). 위 경로와 같은 문들이다.</summary>
    private static readonly string[] OpenableIds =
    {
        "corridor.door.auto", "science.door", "toilet.door", "toilet.stall.outer", "toilet.stall.inner"
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
        SerializedProperty jt = so.FindProperty("judgeTargetsAreOpenable");
        if (jt != null) jt.boolValue = false;   // 판정 대상이라도 쓰지 않는 문(corridor.door.back)은 잠근다 — 표가 정본.
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

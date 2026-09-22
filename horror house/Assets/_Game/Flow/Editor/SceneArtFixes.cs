using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 2026-09-22에 진단한 씬 아트 문제 둘을 고치는 도구입니다. 메뉴 「NightDuty/아트/…」.
///
/// <para><b>씬을 바꾸는 항목은 민이 실행하십시오.</b> 씬·프리팹은 LFS 잠금을 먼저 확인해야 합니다(CLAUDE.md §9).
/// 메뉴 이름에 「(씬 수정)」이 붙은 것이 그것이고, 붙지 않은 것은 에셋만 만듭니다.</para>
///
/// <list type="number">
/// <item><b>라커 142개가 실시간 추가광을 하나도 못 받습니다.</b> 벤더 셰이더 <c>NOT_Lonely_MaskedPBR</c>은
/// URP 7.x Amplify 템플릿이라 키워드 스페이스에 <c>_CLUSTER_LIGHT_LOOP</c>가 없습니다. 렌더러는 Forward+인데
/// 셰이더가 클러스터 경로를 모르니 <c>GetAdditionalLightsCount()</c>가 0을 돌려줍니다.
/// → 고친 사본 <c>NightDuty/MaskedPBR_URP17</c>을 쓰는 머티리얼로 갈아 끼웁니다.</item>
///
/// <item><b>계단 바리케이드가 라이트맵에 없습니다.</b> <c>Contribute GI</c> 스태틱인데 <c>lightmapIndex = -1</c>입니다 —
/// 원본을 복제해 옮기고 재베이크를 안 했습니다. 계단 위 Baked 면광원(intensity 15)의 빛이 라이트맵 안에만 있어서
/// 같은 프리팹인데 형제는 밝고 이쪽은 새까맣습니다.
/// → <c>Contribute GI</c>만 끄면 <c>Corridor_LightProbes</c>(505개)에서 받습니다. <b>재베이크가 필요 없습니다.</b></item>
/// </list>
/// </summary>
public static class SceneArtFixes
{
    private const string ShaderName = "NightDuty/MaskedPBR_URP17";
    private const string MatDir = "Assets/2. Art/Materials";

    /// <summary>갈아 끼울 벤더 머티리얼 이름 → 새로 만들 사본 이름.</summary>
    private static readonly string[][] LockerMaterials =
    {
        new[] { "Lockers_mtl", "Lockers_URP17" },
        new[] { "Lockers02_mtl", "Lockers02_URP17" }
    };

    /// <summary>계단 바리케이드가 서 있는 구역. 이 상자 안에서만 손댑니다.</summary>
    private static readonly Bounds StairArea = new Bounds(new Vector3(33.5f, 3.5f, 39.0f), new Vector3(10f, 8f, 9f));

    // ─────────────────────────── ① 라커 셰이더 ───────────────────────────

    [MenuItem("NightDuty/아트/라커 머티리얼 사본 만들기 (에셋만)")]
    public static void CreateLockerMaterials()
    {
        Shader fixedShader = Shader.Find(ShaderName);
        if (fixedShader == null)
        {
            Debug.LogError("[아트] 셰이더 " + ShaderName + "을 찾지 못했습니다. " +
                           "Assets/2. Art/Shaders/NL_MaskedPBR_URP17.shader가 있는지 보십시오.");
            return;
        }

        if (!Directory.Exists(MatDir))
        {
            Directory.CreateDirectory(MatDir);
            AssetDatabase.Refresh();
        }

        int made = 0;
        for (int i = 0; i < LockerMaterials.Length; i++)
        {
            Material source = FindMaterial(LockerMaterials[i][0]);
            if (source == null)
            {
                Debug.LogWarning("[아트] 벤더 머티리얼 " + LockerMaterials[i][0] + "을 찾지 못했습니다.");
                continue;
            }

            string path = MatDir + "/" + LockerMaterials[i][1] + ".mat";
            Material copy = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (copy == null)
            {
                copy = new Material(source);
                AssetDatabase.CreateAsset(copy, path);
                made++;
            }

            // 값은 원본에서 그대로 옮기고 셰이더만 갈아 끼운다.
            copy.shader = fixedShader;
            copy.CopyPropertiesFromMaterial(source);
            copy.shader = fixedShader;   // CopyProperties가 셰이더를 되돌리는 판본이 있어 한 번 더 못 박는다.
            EditorUtility.SetDirty(copy);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[아트] 라커 머티리얼 사본 준비 완료(새로 만든 것 " + made + "개). " +
                  "씬에 적용하려면 「NightDuty/아트/라커 머티리얼 씬에 적용 (씬 수정)」입니다.");
    }

    [MenuItem("NightDuty/아트/라커 머티리얼 씬에 적용 (씬 수정)")]
    public static void ApplyLockerMaterials()
    {
        Dictionary<Material, Material> swap = new Dictionary<Material, Material>();
        for (int i = 0; i < LockerMaterials.Length; i++)
        {
            Material from = FindMaterial(LockerMaterials[i][0]);
            Material to = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/" + LockerMaterials[i][1] + ".mat");
            if (from != null && to != null)
            {
                swap[from] = to;
            }
        }

        if (swap.Count == 0)
        {
            Debug.LogError("[아트] 갈아 끼울 머티리얼 짝을 찾지 못했습니다. 먼저 사본을 만드십시오.");
            return;
        }

        if (!EditorUtility.DisplayDialog("라커 머티리얼 교체",
                "씬의 라커 렌더러를 URP 17용 머티리얼로 갈아 끼웁니다.\n\n" +
                "씬이 바뀝니다. LFS 잠금을 먼저 확인하셨습니까?", "바꾼다", "그만둔다"))
        {
            return;
        }

        Renderer[] all = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int changed = 0;

        for (int i = 0; i < all.Length; i++)
        {
            Material[] mats = all[i].sharedMaterials;
            bool touched = false;

            for (int m = 0; m < mats.Length; m++)
            {
                Material to;
                if (mats[m] != null && swap.TryGetValue(mats[m], out to))
                {
                    mats[m] = to;
                    touched = true;
                }
            }

            if (!touched)
            {
                continue;
            }

            Undo.RecordObject(all[i], "라커 머티리얼 교체");
            all[i].sharedMaterials = mats;
            EditorUtility.SetDirty(all[i]);
            changed++;
        }

        Debug.Log("[아트] 렌더러 " + changed + "개의 머티리얼을 갈아 끼웠습니다. 씬을 저장하십시오(Ctrl+S).");
    }

    // ─────────────────────── ② 계단 바리케이드 GI ───────────────────────

    [MenuItem("NightDuty/아트/계단 바리케이드 — 무엇이 어두운지 보기")]
    public static void ReportStairBarricade()
    {
        List<Renderer> found = CollectUnbakedInStairs();
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine("[아트] 계단 구역에서 Contribute GI인데 라이트맵에 없는 렌더러 " + found.Count + "개");

        for (int i = 0; i < found.Count; i++)
        {
            sb.AppendLine("  " + found[i].name + "  " + found[i].transform.position.ToString("F2"));
        }

        sb.AppendLine("끄면 Corridor_LightProbes에서 받습니다. 재베이크는 필요 없습니다.");
        Debug.Log(sb.ToString());
    }

    [MenuItem("NightDuty/아트/계단 바리케이드 Contribute GI 끄기 (씬 수정)")]
    public static void FixStairBarricade()
    {
        List<Renderer> found = CollectUnbakedInStairs();
        if (found.Count == 0)
        {
            Debug.Log("[아트] 고칠 것이 없습니다.");
            return;
        }

        if (!EditorUtility.DisplayDialog("계단 바리케이드 Contribute GI 끄기",
                found.Count + "개의 Contribute GI를 끕니다. 라이트 프로브에서 조명을 받게 됩니다.\n\n" +
                "씬이 바뀝니다. LFS 잠금을 먼저 확인하셨습니까?", "끈다", "그만둔다"))
        {
            return;
        }

        for (int i = 0; i < found.Count; i++)
        {
            GameObject go = found[i].gameObject;
            Undo.RecordObject(go, "Contribute GI 끄기");
            StaticEditorFlags flags = GameObjectUtility.GetStaticEditorFlags(go);
            GameObjectUtility.SetStaticEditorFlags(go, flags & ~StaticEditorFlags.ContributeGI);
            EditorUtility.SetDirty(go);
        }

        Debug.Log("[아트] " + found.Count + "개의 Contribute GI를 껐습니다. 씬을 저장하십시오(Ctrl+S).");
    }

    /// <summary>
    /// 계단 구역 안에서 <c>Contribute GI</c>이면서 라이트맵에 들어간 적이 없는 렌더러.
    /// <b>이름 목록을 박지 않습니다</b> — 오브젝트가 늘거나 줄어도 따라옵니다.
    /// </summary>
    private static List<Renderer> CollectUnbakedInStairs()
    {
        List<Renderer> found = new List<Renderer>();
        Renderer[] all = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (int i = 0; i < all.Length; i++)
        {
            Renderer r = all[i];
            if (r == null || r.lightmapIndex >= 0)
            {
                continue;   // 이미 구워져 있다.
            }

            if ((GameObjectUtility.GetStaticEditorFlags(r.gameObject) & StaticEditorFlags.ContributeGI) == 0)
            {
                continue;   // 프로브를 타고 있다. 문제 없음.
            }

            if (!StairArea.Contains(r.bounds.center))
            {
                continue;
            }

            found.Add(r);
        }

        return found;
    }

    private static Material FindMaterial(string assetName)
    {
        string[] guids = AssetDatabase.FindAssets("t:Material " + assetName);
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (Path.GetFileNameWithoutExtension(path) == assetName)
            {
                return AssetDatabase.LoadAssetAtPath<Material>(path);
            }
        }

        return null;
    }
}

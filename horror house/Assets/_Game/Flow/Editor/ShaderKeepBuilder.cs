using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 104차(사용자: 「빌드해서 테스트해보려는데, 다른 문제들은 없을지 한번 확인해줘」): 셰이더 변형 보존 재질.
/// 빌드는 <b>빌드에 들어가는 재질이 쓰는 키워드 조합</b>의 셰이더 변형만 넣는다(shader_feature). 그런데 런타임 코드가 재질 키워드를 켠다 —
/// 발광(<c>_EMISSION</c>: 문 오리 · C4 붉은 등 · 점검 이상 붉은 빛 · 지도 판), 원본 색(<c>TrueColor</c> — URP Unlit에 알파 컷 · 반투명).
/// 그 조합을 쓰는 재질이 빌드에 없으면 플레이어는 가장 가까운 변형으로 그려 <b>발광이 꺼지고 머리카락 카드가 네모로</b> 보인다(편집기는 그때그때 컴파일해 멀쩡).
/// 여기서 빌드 재질(빌드 씬 + Resources 의존)을 훑어 「URP Lit 키워드 조합 + _EMISSION」과 「URP Unlit + 알파 컷/반투명」이 빠진 조합마다
/// 그 조합을 쓰는 재질을 <see cref="OutDir"/>(Resources라 늘 빌드에 들어감)에 만든다. 재질을 더하거나 바꾸면 다시 돌린다(시험이 빠진 조합을 잡는다).
/// </summary>
public static class ShaderKeepBuilder
{
    public const string OutDir = "Assets/_Game/Resources/ShaderKeep";
    public const string LitName = "Universal Render Pipeline/Lit";
    public const string UnlitName = "Universal Render Pipeline/Unlit";

    /// <summary>TrueColor가 URP Unlit에 켜는 조합.</summary>
    public static readonly string[][] UnlitNeeded =
    {
        new[] { "_ALPHATEST_ON" },
        new[] { "_SURFACE_TYPE_TRANSPARENT" },
        new[] { "_ALPHATEST_ON", "_SURFACE_TYPE_TRANSPARENT" },
    };

    [MenuItem("야간근무/빌드/셰이더 변형 보존 재질 다시 만들기")]
    public static void Build()
    {
        if (AssetDatabase.IsValidFolder(OutDir)) AssetDatabase.DeleteAsset(OutDir);
        AssetDatabase.CreateFolder("Assets/_Game/Resources", "ShaderKeep");
        Dictionary<string, Material> have = BuildMaterials();
        int n = 0;
        foreach (KeyValuePair<string, Material> kv in have.ToList())
        {
            Material src = kv.Value;
            if (src.shader.name != LitName || src.IsKeywordEnabled("_EMISSION")) continue;
            string want = Key(src.shader, src.shaderKeywords.Concat(new[] { "_EMISSION" }));
            if (have.ContainsKey(want)) continue;
            Material m = new Material(src);   // 같은 맵·값을 그대로 — 키워드가 속성과 어긋나 지워지지 않게
            m.EnableKeyword("_EMISSION");
            // 가져올 때 URP 검증이 키워드를 속성에서 다시 정한다 — 발광색이 검지 않고 「검은 발광」 표시가 없어야 _EMISSION이 남는다.
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", new Color(0.2f, 0f, 0f, 1f));
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            Save(m, "Lit_" + (++n).ToString("00"));
            have[want] = m;
        }

        Shader unlit = Shader.Find(UnlitName);
        int u = 0;
        foreach (string[] kws in UnlitNeeded)
        {
            if (have.ContainsKey(Key(unlit, kws))) continue;
            Material m = new Material(unlit);
            if (kws.Contains("_ALPHATEST_ON"))
            {
                m.SetFloat("_AlphaClip", 1f);
                m.SetFloat("_Cutoff", 0.5f);
            }

            if (kws.Contains("_SURFACE_TYPE_TRANSPARENT"))
            {
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_ZWrite", 0f);
            }

            foreach (string k in kws) m.EnableKeyword(k);
            Save(m, "Unlit_" + (++u).ToString("00"));
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[ShaderKeep] 보존 재질 Lit " + n + "개 · Unlit " + u + "개 → " + OutDir);
    }

    /// <summary>빠진 조합(시험·확인용). 비어 있으면 됨.</summary>
    public static List<string> Missing()
    {
        Dictionary<string, Material> have = BuildMaterials();
        List<string> miss = new List<string>();
        foreach (Material src in have.Values)
        {
            if (src.shader.name != LitName || src.IsKeywordEnabled("_EMISSION")) continue;
            string want = Key(src.shader, src.shaderKeywords.Concat(new[] { "_EMISSION" }));
            if (!have.ContainsKey(want)) miss.Add(want);
        }

        Shader unlit = Shader.Find(UnlitName);
        foreach (string[] kws in UnlitNeeded) if (!have.ContainsKey(Key(unlit, kws))) miss.Add(Key(unlit, kws));
        return miss.Distinct().ToList();
    }

    /// <summary>빌드에 들어가는 URP Lit/Unlit 재질 — 키워드 조합 → 재질 하나.</summary>
    public static Dictionary<string, Material> BuildMaterials()
    {
        List<string> roots = new List<string>();
        foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes) if (s.enabled) roots.Add(s.path);
        foreach (string p in AssetDatabase.GetAllAssetPaths())
        {
            string q = p.Replace('\\', '/');
            if (q.StartsWith("Assets/") && q.Contains("/Resources/") && !q.Contains("/Editor/") && !AssetDatabase.IsValidFolder(q)) roots.Add(q);
        }

        Dictionary<string, Material> have = new Dictionary<string, Material>();
        foreach (string d in AssetDatabase.GetDependencies(roots.ToArray(), true))
        {
            if (!d.EndsWith(".mat")) continue;
            Material m = AssetDatabase.LoadAssetAtPath<Material>(d);
            if (m == null || m.shader == null || (m.shader.name != LitName && m.shader.name != UnlitName)) continue;
            string k = Key(m.shader, m.shaderKeywords);
            if (!have.ContainsKey(k)) have[k] = m;
        }

        return have;
    }

    public static string Key(Shader shader, IEnumerable<string> keywords)
    {
        return (shader != null ? shader.name : "?") + " | " + string.Join(" ", keywords.Distinct().OrderBy(x => x, System.StringComparer.Ordinal));
    }

    private static void Save(Material m, string file)
    {
        m.name = file;
        AssetDatabase.CreateAsset(m, OutDir + "/" + file + ".mat");
    }
}

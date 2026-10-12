using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace NightDuty.Tests
{
    /// <summary>
    /// 104차 빌드 점검: 런타임 코드가 켜는 셰이더 키워드 조합(URP Lit + _EMISSION · URP Unlit + 알파 컷/반투명)이 빌드 재질 어딘가에 있어야
    /// 플레이어에 그 변형이 들어간다. 빠지면 「야간근무/빌드/셰이더 변형 보존 재질 다시 만들기」(ShaderKeepBuilder — 시험 어셈블리에서 못 봐 같은 계산을 여기 둔다).
    /// </summary>
    public sealed class OneHundredFourthShaderKeepTests
    {
        private const string Lit = "Universal Render Pipeline/Lit";
        private const string Unlit = "Universal Render Pipeline/Unlit";

        private static string Key(string shader, IEnumerable<string> kws)
        {
            return shader + " | " + string.Join(" ", kws.Distinct().OrderBy(x => x, System.StringComparer.Ordinal));
        }

        [Test]
        public void 런타임이_켜는_키워드_조합이_빌드_재질에_있다()
        {
            List<string> roots = new List<string>();
            foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes) if (s.enabled) roots.Add(s.path);
            foreach (string p in AssetDatabase.GetAllAssetPaths())
            {
                string q = p.Replace('\\', '/');
                if (q.StartsWith("Assets/") && q.Contains("/Resources/") && !q.Contains("/Editor/") && !AssetDatabase.IsValidFolder(q)) roots.Add(q);
            }

            HashSet<string> have = new HashSet<string>();
            List<Material> lit = new List<Material>();
            foreach (string d in AssetDatabase.GetDependencies(roots.ToArray(), true))
            {
                if (!d.EndsWith(".mat")) continue;
                Material m = AssetDatabase.LoadAssetAtPath<Material>(d);
                if (m == null || m.shader == null || (m.shader.name != Lit && m.shader.name != Unlit)) continue;
                have.Add(Key(m.shader.name, m.shaderKeywords));
                if (m.shader.name == Lit) lit.Add(m);
            }

            List<string> miss = new List<string>();
            foreach (Material m in lit)
            {
                if (m.IsKeywordEnabled("_EMISSION")) continue;
                string want = Key(Lit, m.shaderKeywords.Concat(new[] { "_EMISSION" }));
                if (!have.Contains(want)) miss.Add(want + "  (" + m.name + ")");
            }

            foreach (string[] kws in new[] { new[] { "_ALPHATEST_ON" }, new[] { "_SURFACE_TYPE_TRANSPARENT" }, new[] { "_ALPHATEST_ON", "_SURFACE_TYPE_TRANSPARENT" } })
            {
                if (!have.Contains(Key(Unlit, kws))) miss.Add(Key(Unlit, kws));
            }

            Assert.IsEmpty(miss, "빌드에서 빠질 변형 — 야간근무/빌드/셰이더 변형 보존 재질 다시 만들기:\n" + string.Join("\n", miss.Distinct()));
        }
    }
}

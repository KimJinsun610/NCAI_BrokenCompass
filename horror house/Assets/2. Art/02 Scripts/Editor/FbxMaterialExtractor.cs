// 아트 편의 도구: 선택한 폴더/FBX의 내장 머티리얼을 .mat으로 추출하고 연결(Remap)합니다.
// 사용법: Project 창에서 폴더나 FBX를 선택 → 우클릭 → 「Art/FBX 머티리얼 추출·연결」
// - 같은 폴더에 같은 이름의 .mat이 이미 있으면 새로 만들지 않고 그걸로 연결합니다.
//   (모션 FBX가 본체 FBX와 머티리얼을 공유하게 됩니다 — 이름에 "motion"이 든 FBX는 뒤에 처리)
// - 내장 텍스처는 FBX 안에 그대로 두고, 추출된 머티리얼이 그걸 참조합니다.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace NightDuty.ArtTools
{
    public static class FbxMaterialExtractor
    {
        const string MenuPath = "Assets/Art/FBX 머티리얼 추출·연결";

        [MenuItem(MenuPath, false, 2000)]
        static void Run()
        {
            var fbxPaths = CollectFbx(Selection.assetGUIDs.Select(AssetDatabase.GUIDToAssetPath));
            if (fbxPaths.Count == 0)
            {
                Debug.LogWarning("[FBX 머티리얼] 선택한 곳에 FBX가 없습니다.");
                return;
            }

            // 본체 먼저, 모션 FBX는 나중에 (본체에서 추출한 .mat을 재사용하도록)
            fbxPaths = fbxPaths
                .OrderBy(p => Path.GetFileName(p).ToLowerInvariant().Contains("motion") ? 1 : 0)
                .ThenBy(p => p)
                .ToList();

            int extracted = 0, remapped = 0;
            foreach (var fbx in fbxPaths)
            {
                var importer = AssetImporter.GetAtPath(fbx) as ModelImporter;
                if (importer == null) continue;

                string dir = Path.GetDirectoryName(fbx).Replace('\\', '/');
                var embedded = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<Material>().ToList();
                bool changed = false;

                foreach (var mat in embedded)
                {
                    string matPath = dir + "/" + Sanitize(mat.name) + ".mat";
                    var existing = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                    if (existing != null)
                    {
                        importer.AddRemap(new AssetImporter.SourceAssetIdentifier(mat), existing);
                        changed = true;
                        remapped++;
                        continue;
                    }

                    string err = AssetDatabase.ExtractAsset(mat, matPath);
                    if (string.IsNullOrEmpty(err))
                    {
                        extracted++;
                        changed = true;
                    }
                    else
                    {
                        Debug.LogWarning("[FBX 머티리얼] " + fbx + " / " + mat.name + " 추출 실패: " + err);
                    }
                }

                if (changed)
                {
                    AssetDatabase.WriteImportSettingsIfDirty(fbx);
                    importer.SaveAndReimport();
                }
                Debug.Log("[FBX 머티리얼] " + fbx + " — 내장 머티리얼 " + embedded.Count + "개 처리");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[FBX 머티리얼] 완료 — FBX " + fbxPaths.Count + "개 · 새로 추출 " + extracted + " · 기존 .mat에 연결 " + remapped);
        }

        [MenuItem(MenuPath, true)]
        static bool Validate() => Selection.assetGUIDs.Length > 0;

        const string TexMenuPath = "Assets/Art/FBX 내장 텍스처 추출 → 머티리얼에 연결";

        // FBX 안에 박힌 텍스처(.fbm)를 꺼내고, 이미 추출된 외부 .mat에 옮겨 담습니다.
        // 절차: 텍스처 추출 → 잠시 리맵 해제하고 재임포트(내장 머티리얼이 텍스처를 물게 됨)
        //       → 내장 머티리얼의 값을 외부 .mat에 복사 → 리맵 복구. 외부 .mat의 GUID는 그대로라 씬 참조가 안 깨집니다.
        [MenuItem(TexMenuPath, false, 2001)]
        static void RunTextures()
        {
            var fbxPaths = CollectFbx(Selection.assetGUIDs.Select(AssetDatabase.GUIDToAssetPath));
            // 모션 FBX는 본체와 같은 텍스처를 들고 있으므로 건너뜁니다(머티리얼은 본체 것을 공유).
            var bases = fbxPaths.Where(p => !Path.GetFileName(p).ToLowerInvariant().Contains("motion")).OrderBy(p => p).ToList();

            int texTotal = 0, matTotal = 0;
            foreach (var fbx in bases)
            {
                var importer = AssetImporter.GetAtPath(fbx) as ModelImporter;
                if (importer == null) continue;
                string dir = Path.GetDirectoryName(fbx).Replace('\\', '/');
                string texDir = dir + "/Textures";
                if (!AssetDatabase.IsValidFolder(texDir)) AssetDatabase.CreateFolder(dir, "Textures");

                var before = new HashSet<string>(AssetDatabase.FindAssets("t:Texture2D", new[] { texDir }));
                if (!importer.ExtractTextures(texDir))
                    Debug.LogWarning("[FBX 텍스처] " + fbx + " — 내장 텍스처가 없거나 추출 실패");
                AssetDatabase.Refresh();

                // 노멀맵 타입 지정
                foreach (var g in AssetDatabase.FindAssets("t:Texture2D", new[] { texDir }))
                {
                    if (before.Contains(g)) continue;
                    texTotal++;
                    string tp = AssetDatabase.GUIDToAssetPath(g);
                    var ti = AssetImporter.GetAtPath(tp) as TextureImporter;
                    if (ti == null) continue;
                    string n = Path.GetFileNameWithoutExtension(tp).ToLowerInvariant();
                    if (n.Contains("normal") && ti.textureType != TextureImporterType.NormalMap)
                    {
                        ti.textureType = TextureImporterType.NormalMap;
                        ti.SaveAndReimport();
                    }
                    else if ((n.Contains("metal") || n.Contains("rough") || n.Contains("ao") || n.Contains("occlusion")) && ti.sRGBTexture)
                    {
                        ti.sRGBTexture = false;
                        ti.SaveAndReimport();
                    }
                }

                // 리맵을 잠시 풀어 내장 머티리얼이 텍스처를 물고 다시 만들어지게 합니다.
                var remaps = importer.GetExternalObjectMap()
                    .Where(kv => kv.Key.type == typeof(Material))
                    .ToDictionary(kv => kv.Key, kv => kv.Value as Material);
                foreach (var k in remaps.Keys) importer.RemoveRemap(k);
                importer.SaveAndReimport();

                var embedded = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<Material>().ToDictionary(m => m.name, m => m);
                foreach (var kv in remaps)
                {
                    Material src;
                    if (kv.Value != null && embedded.TryGetValue(kv.Key.name, out src))
                    {
                        kv.Value.shader = src.shader;
                        kv.Value.CopyPropertiesFromMaterial(src);
                        EditorUtility.SetDirty(kv.Value);
                        matTotal++;
                    }
                    if (kv.Value != null) importer.AddRemap(kv.Key, kv.Value);
                }
                importer.SaveAndReimport();
                Debug.Log("[FBX 텍스처] " + fbx + " — 머티리얼 " + remaps.Count + "개 갱신");
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[FBX 텍스처] 완료 — 본체 FBX " + bases.Count + "개 · 새 텍스처 " + texTotal + " · 텍스처 연결한 머티리얼 " + matTotal);
        }

        [MenuItem(TexMenuPath, true)]
        static bool ValidateTextures() => Selection.assetGUIDs.Length > 0;

        static List<string> CollectFbx(IEnumerable<string> paths)
        {
            var result = new HashSet<string>();
            foreach (var p in paths)
            {
                if (AssetDatabase.IsValidFolder(p))
                {
                    foreach (var g in AssetDatabase.FindAssets("t:Model", new[] { p }))
                    {
                        string mp = AssetDatabase.GUIDToAssetPath(g);
                        if (mp.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase)) result.Add(mp);
                    }
                }
                else if (p.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(p);
                }
            }
            return result.ToList();
        }

        static string Sanitize(string name)
        {
            foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            return name;
        }
    }
}

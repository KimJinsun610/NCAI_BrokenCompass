// 아트 편의 도구: 선택한 폴더의 스프라이트 텍스처를 "조명 받는 판(Quad)"으로 바꿉니다.
// 스프라이트(Sprite Renderer)는 조명을 받지 않아 어두운 씬에서 빛나 보이기 때문입니다.
//
// 사용법: 씬을 연 상태에서 Project 창의 폴더(예: m_derectionalsign)를 선택 → 우클릭 → Art → 「스프라이트 → 조명 받는 판으로 교체」
// 하는 일:
//   1) 열린 씬에서 그 폴더의 스프라이트를 쓰는 Sprite Renderer를 모두 찾음
//   2) 같은 오브젝트 아래에 같은 크기의 Quad(자식 "SignQuad")를 만들고 URP Lit 머티리얼을 씌움, Sprite Renderer는 제거
//   3) 텍스처 타입을 Sprite → Default로 바꾸고 머티리얼을 폴더/Materials/M_<텍스처>.mat 으로 저장
// 씬은 저장하지 않습니다(Ctrl+S는 직접). Ctrl+Z로 씬 변경은 되돌릴 수 있습니다.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NightDuty.ArtTools
{
    public static class SpriteToLitQuad
    {
        const string MenuPath = "Assets/Art/스프라이트 → 조명 받는 판으로 교체";

        [MenuItem(MenuPath, false, 2010)]
        static void Run()
        {
            string folder = Selection.assetGUIDs.Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault(AssetDatabase.IsValidFolder);
            if (folder == null) { Debug.LogWarning("[간판] 폴더를 선택하세요."); return; }

            // 1) 폴더 바로 아래의 스프라이트 텍스처
            var texPaths = AssetDatabase.FindAssets("t:Texture2D", new[] { folder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => Path.GetDirectoryName(p).Replace('\\', '/') == folder)
                .Where(p => (AssetImporter.GetAtPath(p) as TextureImporter)?.textureType == TextureImporterType.Sprite)
                .ToList();
            if (texPaths.Count == 0) { Debug.LogWarning("[간판] " + folder + " 에 Sprite 타입 텍스처가 없습니다."); return; }
            var texSet = new HashSet<string>(texPaths);

            // 2) 머티리얼 준비
            string matDir = folder + "/Materials";
            if (!AssetDatabase.IsValidFolder(matDir)) AssetDatabase.CreateFolder(folder, "Materials");
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) { Debug.LogError("[간판] URP Lit 셰이더를 찾을 수 없습니다."); return; }
            var mats = new Dictionary<string, Material>();
            foreach (var tp in texPaths)
            {
                string mp = matDir + "/M_" + Path.GetFileNameWithoutExtension(tp) + ".mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(mp);
                if (m == null)
                {
                    m = new Material(shader);
                    AssetDatabase.CreateAsset(m, mp);
                }
                mats[tp] = m;
            }

            // 3) 열린 씬의 Sprite Renderer 교체 (텍스처 재임포트 전에 해야 스프라이트 크기를 읽을 수 있음)
            var quadMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            int replaced = 0;
            for (int s = 0; s < SceneManager.sceneCount; s++)
            {
                var scene = SceneManager.GetSceneAt(s);
                if (!scene.isLoaded) continue;
                var srs = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<SpriteRenderer>(true)).ToList();
                foreach (var sr in srs)
                {
                    if (sr.sprite == null) continue;
                    string tp = AssetDatabase.GetAssetPath(sr.sprite.texture);
                    if (!texSet.Contains(tp)) continue;

                    Vector3 size = sr.sprite.bounds.size;
                    Vector3 center = sr.sprite.bounds.center;
                    if (sr.flipX) { size.x = -size.x; center.x = -center.x; }
                    if (sr.flipY) { size.y = -size.y; center.y = -center.y; }

                    var go = new GameObject("SignQuad");
                    Undo.RegisterCreatedObjectUndo(go, "Sprite → Quad");
                    go.layer = sr.gameObject.layer;
                    go.transform.SetParent(sr.transform, false);
                    go.transform.localPosition = center;
                    go.transform.localRotation = Quaternion.identity;
                    go.transform.localScale = new Vector3(size.x, size.y, 1f);
                    go.AddComponent<MeshFilter>().sharedMesh = quadMesh;
                    var mr = go.AddComponent<MeshRenderer>();
                    mr.sharedMaterial = mats[tp];
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    mr.receiveGI = ReceiveGI.LightProbes;
                    GameObjectUtility.SetStaticEditorFlags(go, 0);

                    if (sr.color != Color.white)
                        Debug.LogWarning("[간판] " + sr.name + " 의 Sprite 색상(" + sr.color + ")은 옮기지 않았습니다. 필요하면 머티리얼 Base Color로 조정하세요.", go);

                    Undo.DestroyObjectImmediate(sr);
                    replaced++;
                }
                if (replaced > 0) EditorSceneManager.MarkSceneDirty(scene);
            }

            // 4) 텍스처를 Default로 재임포트
            foreach (var tp in texPaths)
            {
                var ti = (TextureImporter)AssetImporter.GetAtPath(tp);
                ti.textureType = TextureImporterType.Default;
                ti.alphaIsTransparency = true;
                ti.SaveAndReimport();
            }

            // 5) 머티리얼 설정 (Lit + 알파 클리핑 + 양면)
            foreach (var kv in mats)
            {
                var m = kv.Value;
                m.shader = shader;
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(kv.Key);
                m.SetTexture("_BaseMap", tex);
                m.SetTexture("_MainTex", tex);
                m.SetColor("_BaseColor", Color.white);
                m.SetFloat("_Surface", 0f);
                m.SetFloat("_AlphaClip", 1f);
                m.SetFloat("_Cutoff", 0.5f);
                m.EnableKeyword("_ALPHATEST_ON");
                m.SetFloat("_Cull", 0f);            // 양면(스프라이트처럼 뒤에서도 보이게)
                m.SetFloat("_Smoothness", 0.2f);
                m.SetFloat("_Metallic", 0f);
                m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
                EditorUtility.SetDirty(m);
            }
            AssetDatabase.SaveAssets();

            Debug.Log("[간판] 완료 — 텍스처 " + texPaths.Count + "개 · 머티리얼 " + mats.Count + "개 · 씬에서 교체한 스프라이트 " + replaced + "개. 씬은 Ctrl+S로 저장하세요.");
        }

        [MenuItem(MenuPath, true)]
        static bool Validate() => Selection.assetGUIDs.Select(AssetDatabase.GUIDToAssetPath).Any(AssetDatabase.IsValidFolder);
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace NightDuty.Design.EditorTools
{
    // Builds the Noto Serif KR subtitle font assets used ONLY by the prologue (and later ending) subtitles.
    // - Static SDF atlas: every character in DispatchCallSample.Lines + SubtitleCharacters.txt + ASCII/punctuation.
    // - Dynamic fallback (same OTF, inside this folder) so an edited line never shows a missing-glyph box.
    // To add ending lines later: paste them into SubtitleCharacters.txt and run the menu item again
    // (the static asset is rebuilt in place, so its GUID and scene references stay valid).
    public static class NotoSerifKRSubtitleFontBuilder
    {
        public const string Folder = "Assets/1. Design/Fonts/NotoSerifKR";
        public const string SourcePath = Folder + "/NotoSerifKR-Medium.otf";
        public const string StaticPath = Folder + "/NotoSerifKR-Medium SDF Subtitle.asset";
        public const string FallbackPath = Folder + "/NotoSerifKR-Medium SDF Dynamic Fallback.asset";
        public const string CharactersPath = Folder + "/SubtitleCharacters.txt";
        public const int SamplingPointSize = 64;
        public const int Padding = 7;
        // Smallest atlas that holds every character wins (keeps the .asset small; grows when ending lines are added).
        private static readonly Vector2Int[] AtlasSizes = { new Vector2Int(1024, 1024), new Vector2Int(2048, 1024), new Vector2Int(2048, 2048), new Vector2Int(4096, 2048), new Vector2Int(4096, 4096) };
        // The project renders in Linear color space, which makes light TMP SDF text on black look noticeably bolder
        // (Noto Serif's thin strokes/serifs fill in and it reads like a gothic face). A negative face dilate restores
        // the real Noto Serif KR Medium stroke weight. Tune here or on the font asset's material.
        public const float SubtitleFaceDilate = -0.15f;
        public const string Punctuation = "…「」『』〈〉《》·•‥—–‘’“”、。！？：；（）［］～";

        [MenuItem("Tools/1. Design/Rebuild Noto Serif KR Subtitle Font")]
        private static void RebuildMenu() => Debug.Log(Rebuild());

        public static string GetCharacterSet()
        {
            string text = string.Concat(NightDuty.PrologueSample.DispatchCallSample.Lines);
            if (File.Exists(CharactersPath)) text += File.ReadAllText(CharactersPath);
            text += Punctuation;
            text += new string(Enumerable.Range(32, 95).Select(i => (char)i).ToArray());
            return new string(text.Where(c => !char.IsControl(c) && c != '\uFEFF').Distinct().OrderBy(c => c).ToArray());
        }

        public static string Rebuild()
        {
            var source = AssetDatabase.LoadAssetAtPath<Font>(SourcePath);
            if (source == null) throw new InvalidOperationException("Source font not found: " + SourcePath);
            string characters = GetCharacterSet();

            var fallback = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FallbackPath);
            if (fallback == null)
            {
                fallback = TMP_FontAsset.CreateFontAsset(source, SamplingPointSize, Padding, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
                fallback.name = Path.GetFileNameWithoutExtension(FallbackPath);
                SaveNew(fallback, FallbackPath);
                var so = new SerializedObject(fallback);
                var clear = so.FindProperty("m_ClearDynamicDataOnBuild");
                if (clear != null) { clear.boolValue = true; so.ApplyModifiedPropertiesWithoutUndo(); }
            }

            var asset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(StaticPath);
            if (asset == null)
            {
                asset = TMP_FontAsset.CreateFontAsset(source, SamplingPointSize, Padding, GlyphRenderMode.SDFAA, AtlasSizes[0].x, AtlasSizes[0].y, AtlasPopulationMode.Dynamic, false);
                asset.name = Path.GetFileNameWithoutExtension(StaticPath);
                SaveNew(asset, StaticPath);
            }
            // Rebuild in place (same GUID): try each atlas size until every character fits.
            bool fitted = false;
            string missing = characters;
            foreach (var size in AtlasSizes)
            {
                var so = new SerializedObject(asset);
                so.FindProperty("m_AtlasWidth").intValue = size.x;
                so.FindProperty("m_AtlasHeight").intValue = size.y;
                so.ApplyModifiedPropertiesWithoutUndo();
                asset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
                asset.ClearFontAssetData(false);
                if (asset.TryAddCharacters(characters, out missing)) { fitted = true; break; }
            }
            if (!fitted) throw new InvalidOperationException("Atlas could not hold every character. Missing: " + missing);
            asset.atlasPopulationMode = AtlasPopulationMode.Static;
            asset.material.SetFloat("_TextureWidth", asset.atlasWidth);
            asset.material.SetFloat("_TextureHeight", asset.atlasHeight);
            asset.material.SetFloat("_FaceDilate", SubtitleFaceDilate);
            asset.fallbackFontAssetTable = new List<TMP_FontAsset> { fallback };
            EditorUtility.SetDirty(asset.material);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssetIfDirty(asset);
            return string.Format("Noto Serif KR subtitle font: {0} characters, {1} glyphs, atlas {2}x{3}, sampling {4}pt, padding {5}.",
                asset.characterTable.Count, asset.glyphTable.Count, asset.atlasWidth, asset.atlasHeight, SamplingPointSize, Padding);
        }

        private static void SaveNew(TMP_FontAsset asset, string path)
        {
            AssetDatabase.CreateAsset(asset, path);
            foreach (var texture in asset.atlasTextures)
            {
                texture.name = asset.name + " Atlas";
                AssetDatabase.AddObjectToAsset(texture, asset);
            }
            asset.material.name = asset.name + " Material";
            AssetDatabase.AddObjectToAsset(asset.material, asset);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssetIfDirty(asset);
        }
    }
}

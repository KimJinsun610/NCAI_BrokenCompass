using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

/// <summary>
/// Pretendard 동적 SDF 폰트의 생성 설정을 한글용으로 바꾼다 (Tools > Programmer_Kim > Fonts).
/// 기존 설정(샘플 90pt · 1024 · Multi Atlas 꺼짐)은 한 장에 80~90자만 들어가 한글이 □로 대체되는 문제가 있었다.
/// 인스펙터의 Generation Settings → Apply와 같은 순서로 적용한다. 적용하면 기존에 구워진 글자 데이터는 지워지고
/// 쓰이는 글자부터 다시 채워진다.
/// </summary>
public static class PretendardFontSettings
{
    private const string FontFolder = "Assets/3.2 Programmer_Kim/99 Resources/01 Fonts/Pretendard";

    private const int SamplingPointSize = 44;
    private const int AtlasPadding = 5;
    private const int AtlasSize = 2048;

    [MenuItem("Tools/Programmer_Kim/Fonts/Apply Pretendard Dynamic Font Settings")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[PretendardFontSettings] 플레이 모드에서는 실행할 수 없습니다.");
            return;
        }

        MethodInfo updateData = typeof(TMP_FontAsset).GetMethod("UpdateFontAssetData", BindingFlags.Instance | BindingFlags.NonPublic);
        if (updateData == null)
        {
            Debug.LogError("[PretendardFontSettings] TMP_FontAsset.UpdateFontAssetData를 찾을 수 없습니다. TMP 버전을 확인하세요.");
            return;
        }

        string[] guids = AssetDatabase.FindAssets("t:TMP_FontAsset", new[] { FontFolder });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (font == null) continue;

            if (font.atlasPopulationMode != AtlasPopulationMode.Dynamic)
            {
                Debug.LogWarning($"[PretendardFontSettings] 동적 폰트가 아니어서 건너뜁니다: {path}");
                continue;
            }

            SerializedObject so = new SerializedObject(font);

            // 0) 원본 폰트를 먼저 찾는다 — 못 찾으면 아무것도 바꾸지 않고 건너뛴다 (반쯤 적용된 폰트 방지)
            Font source = font.sourceFontFile;
            if (source == null)
            {
                SerializedProperty guidProp = so.FindProperty("m_SourceFontFileGUID");
                string sourceGuid = guidProp != null ? guidProp.stringValue : null;
                if (!string.IsNullOrEmpty(sourceGuid))
                {
                    source = AssetDatabase.LoadAssetAtPath<Font>(AssetDatabase.GUIDToAssetPath(sourceGuid));
                }
            }
            if (source == null)
            {
                Debug.LogWarning($"[PretendardFontSettings] 원본 폰트 파일을 찾을 수 없어 건너뜁니다: {path}");
                continue;
            }

            SerializedProperty faceInfo = so.FindProperty("m_FaceInfo");
            SerializedProperty pointSize = faceInfo != null ? faceInfo.FindPropertyRelative("m_PointSize") : null;
            SerializedProperty faceIndexProp = faceInfo != null ? faceInfo.FindPropertyRelative("m_FaceIndex") : null;
            SerializedProperty paddingProp = so.FindProperty("m_AtlasPadding");
            SerializedProperty widthProp = so.FindProperty("m_AtlasWidth");
            SerializedProperty heightProp = so.FindProperty("m_AtlasHeight");
            SerializedProperty multiAtlasProp = so.FindProperty("m_IsMultiAtlasTexturesEnabled");
            if (pointSize == null || faceIndexProp == null || paddingProp == null || widthProp == null || heightProp == null || multiAtlasProp == null)
            {
                Debug.LogWarning($"[PretendardFontSettings] 생성 설정 필드를 찾을 수 없어 건너뜁니다 (TMP 버전 확인 필요): {path}");
                continue;
            }

            // 1) 생성 설정 변경
            if (pointSize.propertyType == SerializedPropertyType.Float) pointSize.floatValue = SamplingPointSize;
            else pointSize.intValue = SamplingPointSize;
            int faceIndex = faceIndexProp.intValue;

            paddingProp.intValue = AtlasPadding;
            widthProp.intValue = AtlasSize;
            heightProp.intValue = AtlasSize;
            multiAtlasProp.boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();

            // 2) 새 샘플 크기로 글자 정보 갱신
            if (FontEngine.LoadFontFace(source, SamplingPointSize, faceIndex) == FontEngineError.Success)
            {
                font.faceInfo = FontEngine.GetFaceInfo();
            }
            else
            {
                Debug.LogWarning($"[PretendardFontSettings] 폰트 페이스를 불러오지 못했습니다: {path}");
            }

            // 3) 머티리얼 갱신
            Material material = font.material;
            if (material != null)
            {
                material.SetFloat(ShaderUtilities.ID_TextureWidth, AtlasSize);
                material.SetFloat(ShaderUtilities.ID_TextureHeight, AtlasSize);
                if (material.HasProperty(ShaderUtilities.ID_GradientScale))
                {
                    material.SetFloat(ShaderUtilities.ID_GradientScale, AtlasPadding + 1);
                }
                EditorUtility.SetDirty(material);
            }

            // 4) 글자 · 텍스처 데이터 재생성 (인스펙터 Apply와 동일)
            updateData.Invoke(font, null);

            // 5) 생성 설정 기록
            FontAssetCreationSettings creation = font.creationSettings;
            creation.pointSize = SamplingPointSize;
            creation.padding = AtlasPadding;
            creation.atlasWidth = AtlasSize;
            creation.atlasHeight = AtlasSize;
            font.creationSettings = creation;

            EditorUtility.SetDirty(font);
            if (font.atlasTextures != null)
            {
                foreach (Texture2D texture in font.atlasTextures)
                {
                    if (texture != null) EditorUtility.SetDirty(texture);
                }
            }

            Debug.Log($"[PretendardFontSettings] 적용: {path} (샘플 {SamplingPointSize}pt, 여백 {AtlasPadding}, {AtlasSize}×{AtlasSize}, Multi Atlas 켜짐)");
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[PretendardFontSettings] 완료");
    }

    /// <summary>
    /// 폰트 에셋 인스펙터의 ⋮ → Clear Dynamic Data를 Pretendard 전체에 적용한다.
    /// 구워진 글자 표와 텍스처를 비워 파일 크기를 줄인다 (실행하면 필요한 글자가 다시 채워진다).
    /// 커밋 직전에 실행할 것.
    /// </summary>
    [MenuItem("Tools/Programmer_Kim/Fonts/Clear Pretendard Dynamic Data")]
    public static void ClearDynamicData()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[PretendardFontSettings] 플레이 모드에서는 실행할 수 없습니다.");
            return;
        }

        MethodInfo clearData = typeof(TMP_FontAsset).GetMethod("ClearCharacterAndGlyphTablesInternal", BindingFlags.Instance | BindingFlags.NonPublic);
        if (clearData == null)
        {
            Debug.LogError("[PretendardFontSettings] TMP_FontAsset.ClearCharacterAndGlyphTablesInternal을 찾을 수 없습니다. TMP 버전을 확인하세요.");
            return;
        }

        string[] guids = AssetDatabase.FindAssets("t:TMP_FontAsset", new[] { FontFolder });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (font == null || font.atlasPopulationMode != AtlasPopulationMode.Dynamic) continue;

            clearData.Invoke(font, null);

            // Clear Dynamic Data는 폰트 기능 표(커닝 · 합자)를 남겨 파일이 수 MB로 남는다 → 함께 비운다 (실행 중 글자가 추가될 때 다시 채워짐)
            MethodInfo clearFeatures = typeof(TMP_FontAsset).GetMethod("ClearFontFeaturesInternal", BindingFlags.Instance | BindingFlags.NonPublic);
            if (clearFeatures != null) clearFeatures.Invoke(font, null);

            TMPro_EventManager.ON_FONT_PROPERTY_CHANGED(true, font);

            EditorUtility.SetDirty(font);
            if (font.atlasTextures != null)
            {
                foreach (Texture2D texture in font.atlasTextures)
                {
                    if (texture != null) EditorUtility.SetDirty(texture);
                }
            }

            Debug.Log($"[PretendardFontSettings] 동적 데이터 비움: {path}");
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[PretendardFontSettings] 동적 데이터 비우기 완료");
    }
}

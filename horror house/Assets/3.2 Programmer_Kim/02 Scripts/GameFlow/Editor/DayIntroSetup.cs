using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// PlaySystems 프리팹에 Day 인트로 캔버스(검은 화면 + "Day n")를 추가한다.
/// GameFlowSetup.Setup()에서 호출되며, 이미 있으면 건너뛴다.
/// </summary>
public static class DayIntroSetup
{
    private const string BoldFontPath = "Assets/3.2 Programmer_Kim/99 Resources/01 Fonts/Pretendard/Pretendard-Bold SDF.asset";

    public static void AddToPlaySystems(string prefabPath)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            if (root.GetComponentInChildren<DayIntro>(true) != null)
            {
                Debug.Log("[GameFlowSetup] PlaySystems에 DayIntro가 이미 있어 건너뜁니다.");
                return;
            }

            GameObject canvasGo = new GameObject("Canvas_DayIntro", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            canvasGo.transform.SetParent(root.transform, false);

            Canvas canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100; // HUD_Play 등 다른 UI보다 위에 그린다

            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            CanvasGroup group = canvasGo.GetComponent<CanvasGroup>();
            group.alpha = 1f;

            GameObject bgGo = new GameObject("BG", typeof(RectTransform), typeof(Image));
            bgGo.transform.SetParent(canvasGo.transform, false);
            Image bg = bgGo.GetComponent<Image>();
            bg.color = Color.black;
            Stretch(bg.rectTransform);

            GameObject textGo = new GameObject("Txt_Day", typeof(RectTransform));
            textGo.transform.SetParent(canvasGo.transform, false);
            TextMeshProUGUI text = textGo.AddComponent<TextMeshProUGUI>();
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BoldFontPath);
            if (font != null) text.font = font;
            else Debug.LogWarning($"[GameFlowSetup] 폰트를 찾을 수 없습니다: {BoldFontPath}");
            text.text = "Day 1";
            text.fontSize = 72f;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            Stretch(text.rectTransform);

            DayIntro intro = canvasGo.AddComponent<DayIntro>();
            SerializedObject so = new SerializedObject(intro);
            so.FindProperty("overlay").objectReferenceValue = group;
            so.FindProperty("dayText").objectReferenceValue = text;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Debug.Log("[GameFlowSetup] PlaySystems에 Canvas_DayIntro + DayIntro 추가");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}

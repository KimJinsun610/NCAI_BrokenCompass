using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// PlaySystems 프리팹에 결과창 이동용 페이드 캔버스(검정 화면)를 추가하고 PlayResultRouter에 연결한다.
/// GameFlowSetup.Setup()에서 호출되며, 이미 연결돼 있으면 건너뛴다.
/// </summary>
public static class PlayFadeSetup
{
    public static void AddToPlaySystems(string prefabPath)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            PlayResultRouter router = root.GetComponent<PlayResultRouter>();
            if (router == null)
            {
                Debug.LogWarning("[GameFlowSetup] PlaySystems에서 PlayResultRouter를 찾지 못해 페이드 추가를 건너뜁니다.");
                return;
            }

            SerializedObject so = new SerializedObject(router);
            SerializedProperty overlayProp = so.FindProperty("fadeOverlay");
            if (overlayProp.objectReferenceValue != null)
            {
                Debug.Log("[GameFlowSetup] PlaySystems에 결과창 페이드가 이미 있어 건너뜁니다.");
                return;
            }

            GameObject canvasGo = new GameObject("Canvas_Fade", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            canvasGo.transform.SetParent(root.transform, false);

            Canvas canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200; // Day 인트로(100) · HUD보다 위에 그린다

            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            CanvasGroup group = canvasGo.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            GameObject bgGo = new GameObject("BG", typeof(RectTransform), typeof(Image));
            bgGo.transform.SetParent(canvasGo.transform, false);
            Image bg = bgGo.GetComponent<Image>();
            bg.color = Color.black;
            RectTransform bgRt = bg.rectTransform;
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;

            // 평소에는 꺼 두고, 결과창으로 넘어갈 때만 PlayResultRouter가 켠다
            canvasGo.SetActive(false);

            overlayProp.objectReferenceValue = group;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Debug.Log("[GameFlowSetup] PlaySystems에 Canvas_Fade 추가 및 PlayResultRouter 연결");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}

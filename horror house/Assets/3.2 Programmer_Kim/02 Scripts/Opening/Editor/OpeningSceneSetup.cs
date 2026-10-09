using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 오프닝(프롤로그) 씬을 만들고 SceneFlowConfig · Build Settings에 등록한다
/// (Tools > Programmer_Kim > Scene Flow > Setup Opening Scene).
///
/// <para>계약서 씬 빌더와 같은 틀이다. 임시 레이아웃이며 <c>BG_Prologue/prologue1~5</c>를
/// 장면으로 미리 꽂아 둔다 — 자막·머무는 시간은 인스펙터에서 고친다.</para>
///
/// 씬이 이미 있으면 다시 만들지 않는다.
/// </summary>
public static class OpeningSceneSetup
{
    private const string Root = "Assets/3.2 Programmer_Kim";
    private const string MainRoot = "Assets/0. Main";
    private const string OpeningScenePath = MainRoot + "/01 Scene/OpeningScene.unity";
    private const string ConfigPath = MainRoot + "/06 Data/Resources/SceneFlowConfig.asset";
    private const string PrologueFolder = MainRoot + "/06 Data/BG_Prologue/";

    private const string FontFolder = Root + "/99 Resources/01 Fonts/Pretendard/";
    private const string RegularFontPath = FontFolder + "Pretendard-Medium SDF.asset";

    [MenuItem("Tools/Programmer_Kim/Scene Flow/Setup Opening Scene")]
    public static void Setup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[OpeningSceneSetup] 플레이 모드에서는 실행할 수 없습니다.");
            return;
        }

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(OpeningScenePath) == null)
        {
            BuildScene();
        }
        else
        {
            Debug.Log("[OpeningSceneSetup] 오프닝 씬이 이미 있어 생성을 건너뜁니다: " + OpeningScenePath);
        }

        SceneFlowConfig config = AssetDatabase.LoadAssetAtPath<SceneFlowConfig>(ConfigPath);
        if (config != null)
        {
            SerializedObject so = new SerializedObject(config);
            SerializedProperty prop = so.FindProperty("openingScene");
            if (prop != null && string.IsNullOrEmpty(prop.stringValue))
            {
                prop.stringValue = OpeningScenePath;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(config);
                Debug.Log("[OpeningSceneSetup] SceneFlowConfig Opening 씬 지정: " + OpeningScenePath);
            }
        }
        else
        {
            Debug.LogWarning("[OpeningSceneSetup] SceneFlowConfig를 찾지 못했습니다: " + ConfigPath);
        }

        SceneFlowSetup.AddToBuildSettings(OpeningScenePath, false);
        AssetDatabase.SaveAssets();
    }

    private static void BuildScene()
    {
        TMP_FontAsset regular = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(RegularFontPath);

        Scene previous = SceneManager.GetActiveScene();
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);

        try
        {
            GameObject cameraGo = new GameObject("Camera");
            Camera cam = cameraGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cameraGo.AddComponent<AudioListener>();

            GameObject eventSystemGo = new GameObject("EventSystem", typeof(EventSystem));
            eventSystemGo.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();

            GameObject controllerGo = new GameObject("OpeningController");
            OpeningController controller = controllerGo.AddComponent<OpeningController>();
            controllerGo.AddComponent<AudioSource>().playOnAwake = false;
            // 연출을 다른 방식으로 바꿀 때 쓰는 출구. finishAfterSeconds가 0이라 저절로 넘기지는 않는다.
            controllerGo.AddComponent<OpeningExit>();

            GameObject canvasGo = new GameObject("Canvas_Opening", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            RectTransform canvasRt = (RectTransform)canvasGo.transform;

            // 화면을 채우는 그림. 1920×1080 그림이라 가로세로비를 지킨다.
            Image image = CreateImage("Img_Slide", canvasRt, Color.white);
            Stretch(image.rectTransform);
            image.preserveAspect = true;
            image.enabled = false;

            // 아래쪽 자막
            TextMeshProUGUI caption = CreateText("Txt_Caption", canvasRt, regular, string.Empty, 34f,
                new Color(0.93f, 0.93f, 0.9f, 1f), TextAlignmentOptions.Center);
            Place(caption.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(1400f, 160f));
            caption.textWrappingMode = TextWrappingModes.Normal;
            caption.lineSpacing = 10f;
            caption.gameObject.SetActive(false);

            // 암전 판 — 장면 전환을 이것으로 한다. 처음은 완전히 검게.
            Image fade = CreateImage("Img_Fade", canvasRt, Color.black);
            Stretch(fade.rectTransform);

            // 건너뛰기 안내 (오른쪽 아래)
            TextMeshProUGUI skipHint = CreateText("Txt_SkipHint", canvasRt, regular, "아무 키나 눌러 건너뛰기", 22f,
                new Color(1f, 1f, 1f, 0.45f), TextAlignmentOptions.MidlineRight);
            Place(skipHint.rectTransform, new Vector2(1f, 0f), new Vector2(-80f, 70f), new Vector2(500f, 40f));

            SerializedObject co = new SerializedObject(controller);
            co.FindProperty("image").objectReferenceValue = image;
            co.FindProperty("caption").objectReferenceValue = caption;
            co.FindProperty("fade").objectReferenceValue = fade;
            co.FindProperty("skipHint").objectReferenceValue = skipHint.gameObject;

            // BG_Prologue 그림을 번호 순서대로 장면으로 꽂는다(자막은 비워 둔다).
            List<Sprite> sprites = new List<Sprite>();
            for (int i = 1; i <= 16; i++)
            {
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(PrologueFolder + "prologue" + i + ".png");
                if (sprite != null) sprites.Add(sprite);
            }

            SerializedProperty slides = co.FindProperty("slides");
            slides.arraySize = sprites.Count;
            for (int i = 0; i < sprites.Count; i++)
            {
                SerializedProperty slide = slides.GetArrayElementAtIndex(i);
                slide.FindPropertyRelative("image").objectReferenceValue = sprites[i];
                slide.FindPropertyRelative("caption").stringValue = string.Empty;
                slide.FindPropertyRelative("hold").floatValue = 3.5f;
                slide.FindPropertyRelative("voice").objectReferenceValue = null;
            }
            co.ApplyModifiedPropertiesWithoutUndo();

            if (EditorSceneManager.SaveScene(scene, OpeningScenePath))
            {
                Debug.Log("[OpeningSceneSetup] 오프닝 씬 생성: " + OpeningScenePath + " (장면 " + sprites.Count + "개)");
            }
            else
            {
                Debug.LogError("[OpeningSceneSetup] 오프닝 씬 저장 실패: " + OpeningScenePath);
            }
        }
        finally
        {
            if (previous.IsValid()) SceneManager.SetActiveScene(previous);
            EditorSceneManager.CloseScene(scene, true);
        }
    }

    // ───────────────────────── UI 헬퍼 (계약서 빌더와 같음) ─────────────────────────

    private static RectTransform CreateRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static Image CreateImage(string name, Transform parent, Color color)
    {
        RectTransform rt = CreateRect(name, parent);
        Image image = rt.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static TextMeshProUGUI CreateText(string name, Transform parent, TMP_FontAsset font, string text, float size, Color color, TextAlignmentOptions alignment)
    {
        RectTransform rt = CreateRect(name, parent);
        TextMeshProUGUI tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) tmp.font = font;
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = alignment;
        tmp.raycastTarget = false;
        return tmp;
    }

    private static void Place(RectTransform rt, Vector2 anchor, Vector2 position, Vector2 size, Vector2? pivot = null)
    {
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = pivot ?? anchor;
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}

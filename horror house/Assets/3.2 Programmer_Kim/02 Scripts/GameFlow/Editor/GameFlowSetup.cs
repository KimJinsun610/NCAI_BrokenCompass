using NightDuty;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 게임 시간 · 결과창 초기 세팅을 한 번에 수행한다 (Tools > Programmer_Kim > Game Flow).
/// PlaySystems 프리팹 생성 → testScene에 배치 → HUD_Play에 시간 텍스트 추가 → ResultScene 생성 → 설정/Build 등록.
/// 이미 있는 항목은 건너뛰므로 여러 번 실행해도 안전하다.
/// </summary>
public static class GameFlowSetup
{
    private const string Root = "Assets/3.2 Programmer_Kim";
    private const string PrefabRoot = Root + "/03 Prefebs";
    private const string SystemPrefabFolder = PrefabRoot + "/03 System";
    private const string PlaySystemsPrefabPath = SystemPrefabFolder + "/PlaySystems.prefab";
    private const string HudPlayPrefabPath = PrefabRoot + "/01 UI/HUD_Play.prefab";
    private const string TestScenePath = Root + "/00 test/testScene.unity";
    // 씬 · 설정 데이터는 공통 폴더(0. Main)에 둔다
    private const string ResultScenePath = "Assets/0. Main/01 Scene/ResultScene.unity";
    private const string ConfigPath = "Assets/0. Main/06 Data/Resources/SceneFlowConfig.asset";

    private const string FontFolder = Root + "/99 Resources/01 Fonts/Pretendard/";
    private const string RegularFontPath = FontFolder + "Pretendard-Medium SDF.asset";
    private const string BoldFontPath = FontFolder + "Pretendard-Bold SDF.asset";
    private const string ImprintFontPath = FontFolder + "Pretendard-ExtraLight SDF.asset";

    private static readonly FearAxis[] AxisOrder = { FearAxis.Auditory, FearAxis.Illuminance, FearAxis.Layout, FearAxis.Trust };

    [MenuItem("Tools/Programmer_Kim/Game Flow/Setup Game Time & Result Scene")]
    public static void Setup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[GameFlowSetup] 플레이 모드에서는 실행할 수 없습니다.");
            return;
        }

        SetupTestScene();
        DayIntroSetup.AddToPlaySystems(PlaySystemsPrefabPath);
        PlayFadeSetup.AddToPlaySystems(PlaySystemsPrefabPath);
        DeathScreenSetup.Setup();
        AddTimeTextToHudPlay();

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ResultScenePath) == null)
        {
            BuildResultScene();
        }
        else
        {
            Debug.Log($"[GameFlowSetup] 결과 씬이 이미 있어 생성을 건너뜁니다: {ResultScenePath}");
        }
        DutyLogSetup.AddToResultScene(ResultScenePath);

        SceneFlowConfig config = AssetDatabase.LoadAssetAtPath<SceneFlowConfig>(ConfigPath);
        if (config != null)
        {
            SerializedObject so = new SerializedObject(config);
            SerializedProperty resultProp = so.FindProperty("resultScene");
            if (resultProp != null && string.IsNullOrEmpty(resultProp.stringValue))
            {
                resultProp.stringValue = ResultScenePath;
                so.ApplyModifiedPropertiesWithoutUndo();
                Debug.Log($"[GameFlowSetup] SceneFlowConfig Result 씬 지정: {ResultScenePath}");
            }
        }
        SceneFlowSetup.AddToBuildSettings(ResultScenePath, false);

        AssetDatabase.SaveAssets();
        Debug.Log("[GameFlowSetup] 세팅 완료");
    }

    // ───────────────────────── Play 씬 ─────────────────────────

    private static void SetupTestScene()
    {
        Scene previousActive = SceneManager.GetActiveScene();
        Scene scene = SceneManager.GetSceneByPath(TestScenePath);
        bool openedHere = !scene.isLoaded;
        if (openedHere)
        {
            scene = EditorSceneManager.OpenScene(TestScenePath, OpenSceneMode.Additive);
        }

        try
        {
            SceneManager.SetActiveScene(scene);
            GameObject prefab = LoadOrCreatePlaySystemsPrefab();

            bool hasGameTime = false;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.GetComponentInChildren<GameTime>(true) != null)
                {
                    hasGameTime = true;
                    break;
                }
            }

            if (!hasGameTime)
            {
                PrefabUtility.InstantiatePrefab(prefab, scene);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log($"[GameFlowSetup] PlaySystems 배치: {TestScenePath}");
            }
            else
            {
                Debug.Log($"[GameFlowSetup] {TestScenePath}에 GameTime이 이미 있어 배치를 건너뜁니다.");
            }
        }
        finally
        {
            if (previousActive.IsValid() && previousActive.isLoaded && previousActive != scene)
            {
                SceneManager.SetActiveScene(previousActive);
            }
            if (openedHere)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }

    private static GameObject LoadOrCreatePlaySystemsPrefab()
    {
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(PlaySystemsPrefabPath);
        if (existing != null) return existing;

        if (!AssetDatabase.IsValidFolder(SystemPrefabFolder))
        {
            AssetDatabase.CreateFolder(PrefabRoot, "03 System");
        }

        GameObject temp = new GameObject("PlaySystems");
        temp.AddComponent<GameTime>();
        temp.AddComponent<PlayResultRouter>();
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(temp, PlaySystemsPrefabPath);
        Object.DestroyImmediate(temp);

        Debug.Log($"[GameFlowSetup] 프리팹 생성: {PlaySystemsPrefabPath}");
        return prefab;
    }

    private static void AddTimeTextToHudPlay()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(HudPlayPrefabPath);
        try
        {
            if (root.GetComponent<GameTimeView>() != null)
            {
                Debug.Log("[GameFlowSetup] HUD_Play에 GameTimeView가 이미 있어 건너뜁니다.");
                return;
            }

            TextMeshProUGUI text = CreateText("Txt_Time", root.transform, LoadFont(RegularFontPath),
                "12:00", 40f, new Color(1f, 1f, 1f, 0.6f), TextAlignmentOptions.TopRight);
            RectTransform rt = text.rectTransform;
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-40f, -30f);
            rt.sizeDelta = new Vector2(320f, 60f);

            GameTimeView view = root.AddComponent<GameTimeView>();
            SerializedObject so = new SerializedObject(view);
            so.FindProperty("timeText").objectReferenceValue = text;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, HudPlayPrefabPath);
            Debug.Log("[GameFlowSetup] HUD_Play에 Txt_Time + GameTimeView 추가");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // ───────────────────────── Result 씬 ─────────────────────────

    private static void BuildResultScene()
    {
        TMP_FontAsset regular = LoadFont(RegularFontPath);
        TMP_FontAsset bold = LoadFont(BoldFontPath);
        TMP_FontAsset imprint = LoadFont(ImprintFontPath);

        Scene previous = SceneManager.GetActiveScene();
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);

        try
        {
            GameObject cameraGo = new GameObject("Camera");
            Camera cam = cameraGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;

            GameObject eventSystemGo = new GameObject("EventSystem", typeof(EventSystem));
            InputSystemUIInputModule inputModule = eventSystemGo.AddComponent<InputSystemUIInputModule>();
            inputModule.AssignDefaultActions();

            GameObject controllerGo = new GameObject("ResultController");
            ResultController controller = controllerGo.AddComponent<ResultController>();

            GameObject canvasGo = new GameObject("Canvas_Result", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            RectTransform canvasRt = (RectTransform)canvasGo.transform;

            Image bg = CreateImage("BG", canvasRt, new Color(0.03f, 0.03f, 0.035f, 1f));
            Stretch(bg.rectTransform);

            // 본문 영역 (화면 중앙 1100 × 780)
            RectTransform content = CreateRect("Content", canvasRt);
            content.anchorMin = new Vector2(0.5f, 0.5f);
            content.anchorMax = new Vector2(0.5f, 0.5f);
            content.pivot = new Vector2(0.5f, 0.5f);
            content.sizeDelta = new Vector2(1100f, 780f);

            // 제목
            CanvasGroup header = CreateGroup("Header", content, 0f, 80f);
            TextMeshProUGUI title = CreateText("Txt_Title", header.transform, bold, "DAY 1 — 근무 종료", 56f, Color.white, TextAlignmentOptions.MidlineLeft);
            Stretch(title.rectTransform);

            // 요약
            CanvasGroup stats = CreateGroup("Stats", content, 100f, 50f);
            TextMeshProUGUI statsText = CreateText("Txt_Stats", stats.transform, regular, "순찰률 3/3 · 위반 1건 · 충돌 처리 1/1", 30f, new Color(0.75f, 0.75f, 0.75f, 1f), TextAlignmentOptions.MidlineLeft);
            Stretch(statsText.rectTransform);

            // 공포 축 (2 × 2)
            CanvasGroup axis = CreateGroup("AxisGroup", content, 190f, 150f);
            RectTransform grid = CreateRect("Grid", axis.transform);
            PlaceRow(grid, 0f, 112f);
            GridLayoutGroup gridLayout = grid.gameObject.AddComponent<GridLayoutGroup>();
            gridLayout.cellSize = new Vector2(520f, 44f);
            gridLayout.spacing = new Vector2(60f, 24f);
            gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gridLayout.constraintCount = 2;
            gridLayout.childAlignment = TextAnchor.UpperLeft;

            AxisBarView[] bars = new AxisBarView[AxisOrder.Length];
            for (int i = 0; i < AxisOrder.Length; i++)
            {
                bars[i] = CreateAxisBar(grid, AxisOrder[i], regular);
            }

            TextMeshProUGUI threshold = CreateText("Txt_Threshold", axis.transform, regular, "임계 100", 22f, new Color(0.55f, 0.55f, 0.55f, 1f), TextAlignmentOptions.MidlineRight);
            PlaceRow(threshold.rectTransform, 120f, 30f);

            // 위반 로그
            CanvasGroup log = CreateGroup("LogGroup", content, 370f, 290f);
            TextMeshProUGUI logTitle = CreateText("Txt_LogTitle", log.transform, regular, "위반 로그", 28f, new Color(0.75f, 0.75f, 0.75f, 1f), TextAlignmentOptions.MidlineLeft);
            PlaceRow(logTitle.rectTransform, 0f, 40f);

            RectTransform rowsRt = CreateRect("Rows", log.transform);
            PlaceRow(rowsRt, 50f, 240f);
            VerticalLayoutGroup rowsLayout = rowsRt.gameObject.AddComponent<VerticalLayoutGroup>();
            rowsLayout.spacing = 6f;
            rowsLayout.padding = new RectOffset(24, 0, 0, 0);
            rowsLayout.childAlignment = TextAnchor.UpperLeft;
            rowsLayout.childControlWidth = true;
            rowsLayout.childControlHeight = true;
            rowsLayout.childForceExpandWidth = true;
            rowsLayout.childForceExpandHeight = false;

            TextMeshProUGUI rowTemplate = CreateText("Row_Template", rowsRt, regular, "지침 미준수 · 12:41", 26f, new Color(0.85f, 0.85f, 0.85f, 1f), TextAlignmentOptions.MidlineLeft);
            LayoutElement rowLayout = rowTemplate.gameObject.AddComponent<LayoutElement>();
            rowLayout.preferredHeight = 34f;
            rowTemplate.gameObject.SetActive(false);

            ViolationLogView logView = log.gameObject.AddComponent<ViolationLogView>();

            // 버튼 (오른쪽 정렬)
            CanvasGroup buttons = CreateGroup("Buttons", content, 690f, 70f);
            Button mainButton = CreateButton("Btn_Main", buttons.transform, regular, "메인으로", out _);
            SetRightAligned((RectTransform)mainButton.transform, 0f);
            Button nextButton = CreateButton("Btn_Next", buttons.transform, regular, "다음 근무", out TextMeshProUGUI nextLabel);
            SetRightAligned((RectTransform)nextButton.transform, -260f);

            // 페이드 (가장 위)
            Image fadeImage = CreateImage("Fade", canvasRt, Color.black);
            Stretch(fadeImage.rectTransform);
            CanvasGroup fade = fadeImage.gameObject.AddComponent<CanvasGroup>();
            fade.alpha = 1f;
            fade.blocksRaycasts = false;
            fade.interactable = false;

            // 참조 연결
            SerializedObject logSo = new SerializedObject(logView);
            logSo.FindProperty("rowTemplate").objectReferenceValue = rowTemplate;
            logSo.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject so = new SerializedObject(controller);
            so.FindProperty("titleText").objectReferenceValue = title;
            so.FindProperty("statsText").objectReferenceValue = statsText;
            so.FindProperty("headerGroup").objectReferenceValue = header;
            so.FindProperty("statsGroup").objectReferenceValue = stats;
            so.FindProperty("axisGroup").objectReferenceValue = axis;
            so.FindProperty("logGroup").objectReferenceValue = log;
            so.FindProperty("buttonsGroup").objectReferenceValue = buttons;
            so.FindProperty("fade").objectReferenceValue = fade;
            so.FindProperty("violationLog").objectReferenceValue = logView;
            so.FindProperty("imprintFont").objectReferenceValue = imprint;
            so.FindProperty("nextButton").objectReferenceValue = nextButton;
            so.FindProperty("nextLabel").objectReferenceValue = nextLabel;
            so.FindProperty("mainButton").objectReferenceValue = mainButton;
            SerializedProperty barsProp = so.FindProperty("axisBars");
            barsProp.arraySize = bars.Length;
            for (int i = 0; i < bars.Length; i++)
            {
                barsProp.GetArrayElementAtIndex(i).objectReferenceValue = bars[i];
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            if (EditorSceneManager.SaveScene(scene, ResultScenePath))
            {
                Debug.Log($"[GameFlowSetup] 결과 씬 생성: {ResultScenePath}");
            }
            else
            {
                Debug.LogError($"[GameFlowSetup] 결과 씬 저장 실패: {ResultScenePath}");
            }
        }
        finally
        {
            if (previous.IsValid()) SceneManager.SetActiveScene(previous);
            EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static AxisBarView CreateAxisBar(Transform parent, FearAxis axis, TMP_FontAsset font)
    {
        const int SegmentCount = 8;
        const float SegmentWidth = 40f;
        const float SegmentSpacing = 6f;

        RectTransform rt = CreateRect("Axis_" + axis, parent);
        HorizontalLayoutGroup row = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
        row.spacing = 16f;
        row.childAlignment = TextAnchor.MiddleLeft;
        row.childControlWidth = true;
        row.childControlHeight = true;
        row.childForceExpandWidth = false;
        row.childForceExpandHeight = false;

        TextMeshProUGUI label = CreateText("Label", rt, font, AxisBarView.AxisName(axis), 30f, Color.white, TextAlignmentOptions.MidlineLeft);
        LayoutElement labelLayout = label.gameObject.AddComponent<LayoutElement>();
        labelLayout.preferredWidth = 90f;
        labelLayout.preferredHeight = 44f;

        RectTransform segmentRoot = CreateRect("Segments", rt);
        HorizontalLayoutGroup segmentLayout = segmentRoot.gameObject.AddComponent<HorizontalLayoutGroup>();
        segmentLayout.spacing = SegmentSpacing;
        segmentLayout.childAlignment = TextAnchor.MiddleLeft;
        segmentLayout.childControlWidth = true;
        segmentLayout.childControlHeight = true;
        segmentLayout.childForceExpandWidth = false;
        segmentLayout.childForceExpandHeight = false;
        LayoutElement segmentRootLayout = segmentRoot.gameObject.AddComponent<LayoutElement>();
        segmentRootLayout.preferredWidth = SegmentCount * SegmentWidth + (SegmentCount - 1) * SegmentSpacing;
        segmentRootLayout.preferredHeight = 44f;

        Image[] segments = new Image[SegmentCount];
        for (int i = 0; i < SegmentCount; i++)
        {
            segments[i] = CreateImage("Seg_" + i, segmentRoot, new Color(1f, 1f, 1f, 0.12f));
            LayoutElement e = segments[i].gameObject.AddComponent<LayoutElement>();
            e.preferredWidth = SegmentWidth;
            e.preferredHeight = 16f;
        }

        AxisBarView view = rt.gameObject.AddComponent<AxisBarView>();
        SerializedObject so = new SerializedObject(view);
        so.FindProperty("axis").enumValueIndex = (int)axis;
        so.FindProperty("label").objectReferenceValue = label;
        SerializedProperty segProp = so.FindProperty("segments");
        segProp.arraySize = SegmentCount;
        for (int i = 0; i < SegmentCount; i++)
        {
            segProp.GetArrayElementAtIndex(i).objectReferenceValue = segments[i];
        }
        so.ApplyModifiedPropertiesWithoutUndo();

        return view;
    }

    // ───────────────────────── UI 헬퍼 ─────────────────────────

    private static TMP_FontAsset LoadFont(string path)
    {
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        if (font == null) Debug.LogWarning($"[GameFlowSetup] 폰트를 찾을 수 없습니다: {path}");
        return font;
    }

    private static RectTransform CreateRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    /// <summary>Content 안에서 위에서부터 top 위치, height 높이의 가로 전체 영역 + CanvasGroup</summary>
    private static CanvasGroup CreateGroup(string name, Transform parent, float top, float height)
    {
        RectTransform rt = CreateRect(name, parent);
        PlaceRow(rt, top, height);
        return rt.gameObject.AddComponent<CanvasGroup>();
    }

    private static void PlaceRow(RectTransform rt, float top, float height)
    {
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -top);
        rt.sizeDelta = new Vector2(0f, height);
    }

    private static void SetRightAligned(RectTransform rt, float x)
    {
        rt.anchorMin = new Vector2(1f, 0.5f);
        rt.anchorMax = new Vector2(1f, 0.5f);
        rt.pivot = new Vector2(1f, 0.5f);
        rt.anchoredPosition = new Vector2(x, 0f);
        rt.sizeDelta = new Vector2(240f, 64f);
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

    private static Button CreateButton(string name, Transform parent, TMP_FontAsset font, string text, out TextMeshProUGUI label)
    {
        Image image = CreateImage(name, parent, new Color(1f, 1f, 1f, 0.08f));
        image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        image.type = Image.Type.Sliced;
        image.raycastTarget = true;

        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;

        label = CreateText("Label", image.transform, font, text, 28f, Color.white, TextAlignmentOptions.Center);
        Stretch(label.rectTransform);
        return button;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}

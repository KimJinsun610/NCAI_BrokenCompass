using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 씬 흐름 초기 세팅을 한 번에 수행한다.
/// 설정 에셋 생성 → 로딩 씬 생성 → Build Settings 등록 → 메인 시작 버튼 연결.
/// 이미 있는 항목은 건너뛰므로 여러 번 실행해도 안전하다 (기존 값을 덮어쓰지 않음).
/// </summary>
public static class SceneFlowSetup
{
    private const string Root = "Assets/3.2 Programmer_Kim";
    // 씬 · 설정 데이터는 공통 폴더(0. Main)에 둔다. 프리팹 · 폰트는 개인 폴더 그대로
    private const string MainRoot = "Assets/0. Main";
    private const string DataFolder = MainRoot + "/06 Data";
    private const string ResourcesFolder = DataFolder + "/Resources";
    private const string ConfigPath = ResourcesFolder + "/SceneFlowConfig.asset";
    private const string TipTablePath = DataFolder + "/LoadingTipTable.asset";

    private const string MainScenePath = MainRoot + "/01 Scene/MainScene.unity";
    private const string LoadingScenePath = MainRoot + "/01 Scene/LoadingScene.unity";
    private const string DefaultPlayScenePath = MainRoot + "/01 Scene/PlayScene.unity";

    private const string MainHudPrefabPath = Root + "/03 Prefebs/01 UI/HUD_Main.prefab";
    private const string FontPath = Root + "/99 Resources/01 Fonts/Pretendard/Pretendard-Medium SDF.asset";

    // 조작 안내만 (FPController 기본 키 기준)
    private static readonly string[] DefaultTips =
    {
        "W A S D 로 이동하고, 마우스로 주변을 둘러봅니다.",
        "Shift 를 누른 채 이동하면 달릴 수 있습니다.",
        "Space 를 누르면 점프합니다.",
        "Tab 을 누르면 지침록을 열고 닫을 수 있습니다.",
        "Esc 를 누르면 일시정지 메뉴가 열립니다.",
    };

    [MenuItem("Tools/Programmer_Kim/Scene Flow/Setup Loading Scene")]
    public static void Setup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[SceneFlowSetup] 플레이 모드에서는 실행할 수 없습니다.");
            return;
        }

        EnsureFolder(MainRoot, "06 Data");
        EnsureFolder(DataFolder, "Resources");

        LoadingTipTable tipTable = LoadOrCreate<LoadingTipTable>(TipTablePath);
        if (tipTable.tips == null || tipTable.tips.Length == 0)
        {
            tipTable.tips = (string[])DefaultTips.Clone();
            EditorUtility.SetDirty(tipTable);
        }

        SceneFlowConfig config = LoadOrCreate<SceneFlowConfig>(ConfigPath);
        SerializedObject configSo = new SerializedObject(config);
        SetIfEmpty(configSo, "mainScene", MainScenePath);
        SetIfEmpty(configSo, "loadingScene", LoadingScenePath);
        SetIfEmpty(configSo, "playScene", DefaultPlayScenePath);
        configSo.ApplyModifiedPropertiesWithoutUndo();

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(LoadingScenePath) == null)
        {
            BuildLoadingScene(tipTable);
        }
        else
        {
            Debug.Log($"[SceneFlowSetup] 로딩 씬이 이미 있어 생성을 건너뜁니다: {LoadingScenePath}");
        }

        AddToBuildSettings(config.GetPath(GameScene.Main), true);
        AddToBuildSettings(config.GetPath(GameScene.Loading), false);
        AddToBuildSettings(config.GetPath(GameScene.Play), false);

        WireMainStartButton();
        ContractSceneSetup.Setup();

        AssetDatabase.SaveAssets();
        Debug.Log("[SceneFlowSetup] 세팅 완료");
    }

    /// <summary>씬을 Build Settings에 추가한다. 이미 있으면 활성화만 한다.</summary>
    public static void AddToBuildSettings(string scenePath, bool insertFirst)
    {
        if (string.IsNullOrEmpty(scenePath) || AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null) return;

        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        int index = scenes.FindIndex(s => s.path == scenePath);
        if (index >= 0)
        {
            if (scenes[index].enabled) return;
            scenes[index].enabled = true;
        }
        else
        {
            EditorBuildSettingsScene entry = new EditorBuildSettingsScene(scenePath, true);
            if (insertFirst) scenes.Insert(0, entry);
            else scenes.Add(entry);
        }

        EditorBuildSettings.scenes = scenes.ToArray();
        Debug.Log($"[SceneFlowSetup] Build Settings 등록: {scenePath}");
    }

    private static void BuildLoadingScene(LoadingTipTable tipTable)
    {
        Scene previous = SceneManager.GetActiveScene();
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);

        try
        {
            // 카메라: 검정 배경만 그리는 가벼운 구성
            GameObject cameraGo = new GameObject("Camera");
            Camera cam = cameraGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cameraGo.AddComponent<AudioListener>();

            // 진행 담당 + BGM 슬롯
            GameObject controllerGo = new GameObject("LoadingController");
            LoadingController controller = controllerGo.AddComponent<LoadingController>();
            AudioSource bgm = controllerGo.AddComponent<AudioSource>();
            bgm.playOnAwake = false;
            bgm.loop = true;

            // 캔버스
            GameObject canvasGo = new GameObject("Canvas_Loading", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            RectTransform canvasRt = (RectTransform)canvasGo.transform;

            // 배경 (이미지 미지정 시 어두운 임시 색)
            Image bg = CreateImage("BG_Image", canvasRt, new Color(0.06f, 0.06f, 0.07f, 1f));
            Stretch(bg.rectTransform);
            AspectRatioFitter fitter = bg.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = 16f / 9f;

            // 글자 가독성용 어둡게 덮기
            Image dim = CreateImage("Dim", canvasRt, new Color(0f, 0f, 0f, 0.45f));
            Stretch(dim.rectTransform);

            // 안내 문구
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font == null) Debug.LogWarning($"[SceneFlowSetup] 폰트를 찾을 수 없습니다: {FontPath}");

            RectTransform tipGroup = CreateRect("TipGroup", canvasRt);
            SetBottomCenter(tipGroup, new Vector2(0f, 140f), new Vector2(1400f, 130f));

            TextMeshProUGUI tipTitle = CreateText("TipTitle", tipGroup, font, "조작 안내", 28f, new Color(0.7f, 0.7f, 0.7f, 1f));
            tipTitle.rectTransform.anchorMin = new Vector2(0f, 1f);
            tipTitle.rectTransform.anchorMax = new Vector2(1f, 1f);
            tipTitle.rectTransform.pivot = new Vector2(0.5f, 1f);
            tipTitle.rectTransform.sizeDelta = new Vector2(0f, 40f);
            tipTitle.rectTransform.anchoredPosition = Vector2.zero;

            TextMeshProUGUI tipBody = CreateText("TipBody", tipGroup, font, DefaultTips[0], 36f, Color.white);
            Stretch(tipBody.rectTransform);
            tipBody.rectTransform.offsetMax = new Vector2(0f, -48f);

            // 게이지
            RectTransform progressGroup = CreateRect("ProgressGroup", canvasRt);
            SetBottomCenter(progressGroup, new Vector2(0f, 80f), new Vector2(1200f, 6f));

            Image gaugeBg = CreateImage("Gauge_BG", progressGroup, new Color(1f, 1f, 1f, 0.15f));
            Stretch(gaugeBg.rectTransform);

            Image gaugeFill = CreateImage("Gauge_Fill", progressGroup, new Color(0.85f, 0.85f, 0.85f, 1f));
            Stretch(gaugeFill.rectTransform);
            gaugeFill.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"); // Filled 타입은 스프라이트가 있어야 동작
            gaugeFill.type = Image.Type.Filled;
            gaugeFill.fillMethod = Image.FillMethod.Horizontal;
            gaugeFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            gaugeFill.fillAmount = 0f;

            // 페이드 (가장 위)
            Image fadeImage = CreateImage("Fade", canvasRt, Color.black);
            Stretch(fadeImage.rectTransform);
            CanvasGroup fade = fadeImage.gameObject.AddComponent<CanvasGroup>();
            fade.alpha = 1f;
            fade.blocksRaycasts = false;
            fade.interactable = false;

            SerializedObject so = new SerializedObject(controller);
            so.FindProperty("gaugeFill").objectReferenceValue = gaugeFill;
            so.FindProperty("tipText").objectReferenceValue = tipBody;
            so.FindProperty("background").objectReferenceValue = bg;
            so.FindProperty("fade").objectReferenceValue = fade;
            so.FindProperty("tipTable").objectReferenceValue = tipTable;
            so.FindProperty("bgmSource").objectReferenceValue = bgm;
            so.ApplyModifiedPropertiesWithoutUndo();

            if (EditorSceneManager.SaveScene(scene, LoadingScenePath))
            {
                Debug.Log($"[SceneFlowSetup] 로딩 씬 생성: {LoadingScenePath}");
            }
            else
            {
                Debug.LogError($"[SceneFlowSetup] 로딩 씬 저장 실패: {LoadingScenePath}");
            }
        }
        finally
        {
            if (previous.IsValid()) SceneManager.SetActiveScene(previous);
            EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static void WireMainStartButton()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(MainHudPrefabPath);
        try
        {
            HUDActions actions = root.GetComponent<HUDActions>();
            Transform startTf = FindDeep(root.transform, "Btn_Start");
            Button button = startTf != null ? startTf.GetComponent<Button>() : null;
            if (actions == null || button == null)
            {
                Debug.LogWarning("[SceneFlowSetup] HUD_Main에서 HUDActions 또는 Btn_Start를 찾지 못해 버튼 연결을 건너뜁니다.");
                return;
            }

            bool changed = false;
            bool alreadyWired = false;
            for (int i = button.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
            {
                string method = button.onClick.GetPersistentMethodName(i);
                if (method == nameof(HUDActions.StartGame))
                {
                    alreadyWired = true;
                }
                else if (method == nameof(HUDActions.LoadScene))
                {
                    // 기존 직접 로드(로딩 씬을 거치지 않음) 호출 제거
                    UnityEventTools.RemovePersistentListener(button.onClick, i);
                    changed = true;
                }
            }

            if (!alreadyWired)
            {
                UnityEventTools.AddPersistentListener(button.onClick, actions.StartGame);
                changed = true;
            }

            if (changed)
            {
                PrefabUtility.SaveAsPrefabAsset(root, MainHudPrefabPath);
                Debug.Log("[SceneFlowSetup] HUD_Main Btn_Start → HUDActions.StartGame 연결");
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static T LoadOrCreate<T>(string path) where T : ScriptableObject
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null) return asset;

        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        Debug.Log($"[SceneFlowSetup] 에셋 생성: {path}");
        return asset;
    }

    private static void SetIfEmpty(SerializedObject so, string propertyName, string value)
    {
        SerializedProperty property = so.FindProperty(propertyName);
        if (property != null && string.IsNullOrEmpty(property.stringValue))
        {
            property.stringValue = value;
        }
    }

    private static void EnsureFolder(string parent, string name)
    {
        if (!AssetDatabase.IsValidFolder(parent + "/" + name))
        {
            AssetDatabase.CreateFolder(parent, name);
        }
    }

    private static Transform FindDeep(Transform parent, string name)
    {
        if (parent.name == name) return parent;
        foreach (Transform child in parent)
        {
            Transform found = FindDeep(child, name);
            if (found != null) return found;
        }
        return null;
    }

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

    private static TextMeshProUGUI CreateText(string name, Transform parent, TMP_FontAsset font, string text, float size, Color color)
    {
        RectTransform rt = CreateRect(name, parent);
        TextMeshProUGUI tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) tmp.font = font;
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        return tmp;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static void SetBottomCenter(RectTransform rt, Vector2 position, Vector2 size)
    {
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
    }
}

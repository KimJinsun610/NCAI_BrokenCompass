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
/// <para>내용은 기획팀 프롤로그 샘플(<c>1. Design/01 Scene/PrologueCallSample</c>)을 따른다 —
/// 파견 담당자 11문장 · 그 나레이션 음성 · 하단 자막 스타일(Noto Serif KR).
/// 그림은 <c>0. Main/06 Data/BG_Prologue/prologue1~5</c>를 두 문장씩 순서대로 넘긴다.</para>
///
/// 씬을 이미 만든 뒤 내용만 다시 채우려면 <b>Rebuild Opening Scene</b>을 쓴다(덮어쓴다).
/// </summary>
public static class OpeningSceneSetup
{
    private const string Root = "Assets/3.2 Programmer_Kim";
    private const string MainRoot = "Assets/0. Main";
    private const string OpeningScenePath = MainRoot + "/01 Scene/OpeningScene.unity";
    private const string ConfigPath = MainRoot + "/06 Data/Resources/SceneFlowConfig.asset";
    private const string PrologueFolder = MainRoot + "/06 Data/BG_Prologue/";
    private const string VoiceFolder = Root + "/99 Resources/04 Sound/Prologue/";

    private const string FontFolder = Root + "/99 Resources/01 Fonts/Pretendard/";
    private const string RegularFontPath = FontFolder + "Pretendard-Medium SDF.asset";
    // 자막 글꼴은 기획팀 프롤로그와 같은 것을 쓴다(이 11문장에 맞춰 구운 정적 SDF).
    private const string SubtitleFontPath = "Assets/1. Design/Fonts/NotoSerifKR/NotoSerifKR-Medium SDF Subtitle.asset";

    /// <summary>파견 담당자 11문장 — 기획팀 <c>DispatchCallSample.Lines</c>와 같다.</summary>
    private static readonly string[] Lines =
    {
        "안녕하세요. 현장관리팀 파견 담당자입니다.",
        "지원하신 야간 근무 건으로 연락드렸습니다.",
        "이번 현장은 철거를 닷새 앞둔 폐교입니다.",
        "사전에 안내드린 대로, 철거 전 시설 점검이 필요한 장소입니다.",
        "철거 전까지 야간에 건물 상태를 점검하고 기록할 인원이 필요합니다.",
        "이전 근무자는 개인 사정으로 근무를 이어가지 못하게 되었습니다.",
        "자정부터 1층을 순찰하고, 지정된 대상을 점검해 태블릿으로 보고해 주시면 됩니다.",
        "근무 중에는 태블릿으로 점검표와 문자가 전달됩니다.",
        "계약서를 보내드리겠습니다.",
        "단순 점검 업무라 어려운 일은 없으실 겁니다.",
        "그럼 첫날 밤, 잘 부탁드립니다."
    };

    /// <summary>문장마다 쓸 그림 번호(1~5). 두 문장씩 넘어가고, 바뀔 때만 지직거린다.</summary>
    private static readonly int[] ImageOfLine = { 1, 1, 2, 2, 3, 3, 4, 4, 5, 5, 5 };

    /// <summary>그림 영역 크기(1920×1080 기준). 화면을 다 채우지 않아 아래 자막이 검은 바탕 위에 놓인다.</summary>
    private static readonly Vector2 ScreenSize = new Vector2(1280f, 720f);

    [MenuItem("Tools/Programmer_Kim/Scene Flow/Setup Opening Scene")]
    public static void Setup()
    {
        Run(false);
    }

    [MenuItem("Tools/Programmer_Kim/Scene Flow/Rebuild Opening Scene")]
    public static void Rebuild()
    {
        Run(true);
    }

    private static void Run(bool overwrite)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[OpeningSceneSetup] 플레이 모드에서는 실행할 수 없습니다.");
            return;
        }

        // 열려 있는 씬은 덮어쓸 수 없다 — 그냥 두면 Unity가 알기 어려운 오류를 내고 옛 씬이 남는다.
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            if (SceneManager.GetSceneAt(i).path != OpeningScenePath) continue;
            Debug.LogWarning("[OpeningSceneSetup] 오프닝 씬이 열려 있어 다시 만들 수 없습니다. 다른 씬을 연 뒤 실행하십시오: " + OpeningScenePath);
            return;
        }

        bool exists = AssetDatabase.LoadAssetAtPath<SceneAsset>(OpeningScenePath) != null;
        if (!exists || overwrite)
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
        TMP_FontAsset subtitleFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(SubtitleFontPath);
        if (subtitleFont == null)
        {
            subtitleFont = regular;
            Debug.LogWarning("[OpeningSceneSetup] 자막 글꼴을 찾지 못해 기본 글꼴을 씁니다: " + SubtitleFontPath);
        }

        Scene previous = SceneManager.GetActiveScene();
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);

        try
        {
            GameObject cameraGo = new GameObject("Camera") { tag = "MainCamera" };
            Camera cam = cameraGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cameraGo.AddComponent<AudioListener>();

            GameObject eventSystemGo = new GameObject("EventSystem", typeof(EventSystem));
            InputSystemUIInputModule module = eventSystemGo.AddComponent<InputSystemUIInputModule>();
            // 기본 입력 액션 연결은 에디터 상태에 따라 드물게 실패한다 — 씬 생성 자체를 멈추지는 않게 한다.
            // (오프닝은 버튼이 없고 아무 키나 받으므로 이것이 없어도 동작한다.)
            try { module.AssignDefaultActions(); }
            catch (System.Exception e) { Debug.LogWarning("[OpeningSceneSetup] 기본 입력 액션 연결 실패(무시): " + e.Message); }

            GameObject controllerGo = new GameObject("OpeningController");
            OpeningController controller = controllerGo.AddComponent<OpeningController>();
            AudioSource voice = controllerGo.AddComponent<AudioSource>();
            voice.playOnAwake = false;
            voice.spatialBlend = 0f;
            voice.volume = 0.9f;
            // 연출을 다른 방식으로 바꿀 때 쓰는 출구. finishAfterSeconds가 0이라 저절로 넘기지는 않는다.
            controllerGo.AddComponent<OpeningExit>();

            GameObject canvasGo = new GameObject("Canvas_Opening", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            RectTransform canvasRt = (RectTransform)canvasGo.transform;

            // 자막이 또렷하게 읽히도록 화면 전체에 검정을 깐다.
            Image backdrop = CreateImage("Img_Backdrop", canvasRt, Color.black);
            Stretch(backdrop.rectTransform);

            // 그림은 화면을 다 채우지 않고 가운데 위쪽에만 — 아래는 자막 자리로 비워 둔다.
            RectTransform screen = CreateRect("Screen", canvasRt);
            Place(screen, new Vector2(0.5f, 0.5f), new Vector2(0f, 90f), ScreenSize, new Vector2(0.5f, 0.5f));

            // 1920×1080 그림이라 가로세로비를 지킨다.
            Image image = CreateImage("Img_Slide", screen, Color.white);
            Stretch(image.rectTransform);
            image.preserveAspect = true;
            image.enabled = false;

            // 색분리 잔상 둘 — 지직거리는 동안에만 켜진다(그림 위에 겹친다).
            Image ghostA = CreateImage("Img_GhostR", screen, new Color(1f, 0.25f, 0.25f, 0f));
            Stretch(ghostA.rectTransform);
            ghostA.preserveAspect = true;
            ghostA.enabled = false;

            Image ghostB = CreateImage("Img_GhostB", screen, new Color(0.3f, 0.7f, 1f, 0f));
            Stretch(ghostB.rectTransform);
            ghostB.preserveAspect = true;
            ghostB.enabled = false;

            // 잡음 판(텍스처는 재생할 때 코드가 만든다) — 그림 영역에만 깔린다.
            RectTransform noiseRt = CreateRect("Raw_Noise", screen);
            Stretch(noiseRt);
            RawImage noise = noiseRt.gameObject.AddComponent<RawImage>();
            noise.raycastTarget = false;
            noise.color = new Color(1f, 1f, 1f, 0f);
            noise.enabled = false;

            // 아래쪽 자막 — 기획팀 프롤로그와 같은 배치·크기·색.
            TextMeshProUGUI caption = CreateText("Txt_Caption", canvasRt, subtitleFont, string.Empty, 34f,
                new Color(0.84f, 0.84f, 0.84f), TextAlignmentOptions.Center);
            RectTransform capRt = caption.rectTransform;
            capRt.anchorMin = new Vector2(0.12f, 0f);
            capRt.anchorMax = new Vector2(0.88f, 0f);
            capRt.pivot = new Vector2(0.5f, 0f);
            capRt.sizeDelta = new Vector2(0f, 150f);
            capRt.anchoredPosition = new Vector2(0f, 75f);
            caption.textWrappingMode = TextWrappingModes.Normal;
            caption.lineSpacing = 10f;
            caption.gameObject.SetActive(false);

            // 암전 판 — 처음과 끝에만 쓴다. 처음은 완전히 검게.
            Image fade = CreateImage("Img_Fade", canvasRt, Color.black);
            Stretch(fade.rectTransform);

            // 건너뛰기 안내 (오른쪽 아래) — 글자와 게이지를 한 묶음으로 두고 CanvasGroup으로 함께 흐려진다.
            RectTransform skipRoot = CreateRect("Skip", canvasRt);
            Place(skipRoot, new Vector2(1f, 0f), new Vector2(-80f, 60f), new Vector2(360f, 64f));
            CanvasGroup skipGroup = skipRoot.gameObject.AddComponent<CanvasGroup>();
            skipGroup.interactable = false;
            skipGroup.blocksRaycasts = false;

            TextMeshProUGUI skipText = CreateText("Txt_SkipHint", skipRoot, regular, "F키 눌러서 건너뛰기", 22f,
                new Color(1f, 1f, 1f, 1f), TextAlignmentOptions.MidlineRight);
            Place(skipText.rectTransform, new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(360f, 34f));

            // 게이지는 안내 글자와 같은 폭으로 — 차오르는 시간(1.2초)은 그대로라 그만큼 천천히 찬다.
            skipText.ForceMeshUpdate();
            float gaugeWidth = Mathf.Max(80f, skipText.preferredWidth);

            Image gaugeBg = CreateImage("Img_GaugeBg", skipRoot, new Color(1f, 1f, 1f, 0.18f));
            Place(gaugeBg.rectTransform, new Vector2(1f, 0f), new Vector2(0f, 10f), new Vector2(gaugeWidth, 4f));

            Image gauge = CreateImage("Img_SkipGauge", gaugeBg.rectTransform, new Color(0.93f, 0.93f, 0.9f, 0.95f));
            Stretch(gauge.rectTransform);
            // Filled 형식은 스프라이트가 있어야 그려진다 — 내장 UI 스프라이트를 쓴다.
            gauge.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            gauge.type = Image.Type.Filled;
            gauge.fillMethod = Image.FillMethod.Horizontal;
            gauge.fillOrigin = (int)Image.OriginHorizontal.Left;
            gauge.fillAmount = 0f;
            gauge.enabled = false;

            SerializedObject co = new SerializedObject(controller);
            co.FindProperty("image").objectReferenceValue = image;
            co.FindProperty("ghostA").objectReferenceValue = ghostA;
            co.FindProperty("ghostB").objectReferenceValue = ghostB;
            co.FindProperty("noise").objectReferenceValue = noise;
            co.FindProperty("caption").objectReferenceValue = caption;
            co.FindProperty("fade").objectReferenceValue = fade;
            co.FindProperty("skipHint").objectReferenceValue = skipGroup;
            co.FindProperty("skipGauge").objectReferenceValue = gauge;

            // 문장 · 음성 · 그림을 한 장씩 꽂는다.
            Sprite[] sprites = new Sprite[6];   // 1~5번만 쓴다
            for (int n = 1; n <= 5; n++)
            {
                sprites[n] = AssetDatabase.LoadAssetAtPath<Sprite>(PrologueFolder + "prologue" + n + ".png");
                if (sprites[n] == null) Debug.LogWarning("[OpeningSceneSetup] 그림을 찾지 못했습니다: prologue" + n);
            }

            SerializedProperty slides = co.FindProperty("slides");
            slides.arraySize = Lines.Length;
            int withVoice = 0;
            for (int i = 0; i < Lines.Length; i++)
            {
                string clipPath = VoiceFolder + "PRO_" + (i + 1).ToString("00") + "_Dispatcher.wav";
                AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath);
                if (clip != null) withVoice++;
                else Debug.LogWarning("[OpeningSceneSetup] 나레이션을 찾지 못했습니다: " + clipPath);

                SerializedProperty slide = slides.GetArrayElementAtIndex(i);
                slide.FindPropertyRelative("image").objectReferenceValue = sprites[ImageOfLine[i]];
                slide.FindPropertyRelative("caption").stringValue = Lines[i];
                slide.FindPropertyRelative("hold").floatValue = 3.5f;   // 음성이 있으면 음성 길이를 쓴다
                slide.FindPropertyRelative("voice").objectReferenceValue = clip;
            }
            co.ApplyModifiedPropertiesWithoutUndo();

            if (EditorSceneManager.SaveScene(scene, OpeningScenePath))
            {
                Debug.Log("[OpeningSceneSetup] 오프닝 씬 생성: " + OpeningScenePath
                    + " (문장 " + Lines.Length + " · 나레이션 " + withVoice + " · 그림 5)");
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

using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 계약서 씬을 만들고 SceneFlowConfig · Build Settings에 등록한다 (Tools > Programmer_Kim > Scene Flow).
/// 아트가 나오기 전 임시 레이아웃이다: 배경 · 폴더 · 클립 · 종이 · 서명 칸은 단색 Image이고,
/// 각 Image에 스프라이트를 넣으면 그대로 교체된다. 씬이 이미 있으면 다시 만들지 않는다.
/// </summary>
public static class ContractSceneSetup
{
    private const string Root = "Assets/3.2 Programmer_Kim";
    // 씬 · 설정 데이터는 공통 폴더(0. Main)에 둔다. 폰트는 개인 폴더 그대로
    private const string MainRoot = "Assets/0. Main";
    private const string ContractScenePath = MainRoot + "/01 Scene/ContractScene.unity";
    private const string ConfigPath = MainRoot + "/06 Data/Resources/SceneFlowConfig.asset";

    private const string FontFolder = Root + "/99 Resources/01 Fonts/Pretendard/";
    private const string RegularFontPath = FontFolder + "Pretendard-Medium SDF.asset";
    private const string BoldFontPath = FontFolder + "Pretendard-Bold SDF.asset";

    private static readonly Color InkColor = new Color(0.12f, 0.12f, 0.12f, 1f);

    private const string DocumentTitle = "파견 근무 계약서";

    // 계약서 본문(2026-10-07). 조항 사이는 \n(문단 간격), 조항 안 ①② 줄바꿈은 \v(문단 간격 없이 줄만 바꿈).
    // TMP는 한글을 글자 단위로 줄바꿈하므로 조항 끝 낱말을 <nobr>로 묶어 「다.」만 다음 줄로 떨어지지 않게 한다.
    private const string PlaceholderBody =
        "<b>제1조 (근무)</b> 철거 전 5일간, 매일 00:00부터 04:00까지 폐교 1층을 <nobr>순찰한다.</nobr>\n"
        + "<b>제2조 (보고)</b> 점검 대상을 확인하고 태블릿으로 [정상] 또는 [이상]을 <nobr>보고한다.</nobr>\n"
        + "<b>제3조 (근무 방식)</b>\v<margin-left=1.4em>① 근무는 태블릿으로 매일 전달되는 근무수칙에 <nobr>따른다.</nobr>\v② 근무자는 업무 호출에 지체 없이 응하여야 <nobr>한다.</nobr></margin>\n"
        + "<b>제4조 (수칙 준수)</b>\v<margin-left=1.4em>① 수칙은 매일 갱신되며, 근무자는 이를 임의로 변경할 수 <nobr>없다.</nobr>\v② 회사가 태블릿으로 전달하는 문자는 모두 정상적으로 발송된 <nobr>것이다.</nobr></margin>\n"
        + "<b>제5조 (안전)</b> 수칙을 따르지 않아 생긴 상해와 정신적 변화에 회사는 책임지지 <nobr>않는다.</nobr>\n"
        + "<b>제6조 (기밀)</b> 현장에서 보고 들은 내용은 외부에 누설하지 <nobr>않는다.</nobr>\n"
        + "<b>제7조 (해지)</b> 수칙을 반복 위반하면 회사는 통보 없이 계약을 해지할 수 <nobr>있다.</nobr>";

    [MenuItem("Tools/Programmer_Kim/Scene Flow/Setup Contract Scene")]
    public static void Setup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[ContractSceneSetup] 플레이 모드에서는 실행할 수 없습니다.");
            return;
        }

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ContractScenePath) == null)
        {
            BuildScene();
        }
        else
        {
            Debug.Log($"[ContractSceneSetup] 계약서 씬이 이미 있어 생성을 건너뜁니다: {ContractScenePath}");
        }

        SceneFlowConfig config = AssetDatabase.LoadAssetAtPath<SceneFlowConfig>(ConfigPath);
        if (config != null)
        {
            SerializedObject so = new SerializedObject(config);
            SerializedProperty prop = so.FindProperty("contractScene");
            if (prop != null && string.IsNullOrEmpty(prop.stringValue))
            {
                prop.stringValue = ContractScenePath;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(config);
                Debug.Log($"[ContractSceneSetup] SceneFlowConfig Contract 씬 지정: {ContractScenePath}");
            }
        }
        else
        {
            Debug.LogWarning($"[ContractSceneSetup] SceneFlowConfig를 찾지 못했습니다: {ConfigPath}");
        }

        SceneFlowSetup.AddToBuildSettings(ContractScenePath, false);
        AssetDatabase.SaveAssets();
    }

    private static void BuildScene()
    {
        TMP_FontAsset regular = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(RegularFontPath);
        TMP_FontAsset bold = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BoldFontPath);

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

            GameObject controllerGo = new GameObject("ContractController");
            ContractController controller = controllerGo.AddComponent<ContractController>();

            GameObject canvasGo = new GameObject("Canvas_Contract", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            RectTransform canvasRt = (RectTransform)canvasGo.transform;

            // 배경 (아트: 어두운 학교 복도)
            Image bg = CreateImage("Img_Background", canvasRt, new Color(0.07f, 0.07f, 0.075f, 1f));
            Stretch(bg.rectTransform);
            Image dim = CreateImage("Dim", canvasRt, new Color(0f, 0f, 0f, 0.4f));
            Stretch(dim.rectTransform);

            // 제목 + 밑줄 (왼쪽 위)
            TextMeshProUGUI title = CreateText("Txt_Title", canvasRt, bold, DocumentTitle, 44f, Color.white, TextAlignmentOptions.MidlineLeft);
            Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(110f, -130f), new Vector2(600f, 60f));
            Image titleLine = CreateImage("Img_TitleLine", canvasRt, new Color(1f, 1f, 1f, 0.7f));
            Place(titleLine.rectTransform, new Vector2(0f, 1f), new Vector2(68f, -222f), new Vector2(470f, 2f));

            // 서류 묶음 (가운데, 아래로 화면 밖까지 이어짐)
            RectTransform document = CreateRect("Document", canvasRt);
            document.anchorMin = new Vector2(0.5f, 0f);
            document.anchorMax = new Vector2(0.5f, 0f);
            document.pivot = new Vector2(0.5f, 0f);
            document.anchoredPosition = new Vector2(80f, -80f);
            document.sizeDelta = new Vector2(1000f, 1010f);

            Image folderBack = CreateImage("Img_FolderBack", document, new Color(0.5f, 0.5f, 0.5f, 1f));
            Place(folderBack.rectTransform, new Vector2(0f, 0f), new Vector2(90f, 0f), new Vector2(840f, 960f), new Vector2(0f, 0f));
            folderBack.rectTransform.localEulerAngles = new Vector3(0f, 0f, -1.5f);

            Image folder = CreateImage("Img_Folder", document, new Color(0.62f, 0.62f, 0.62f, 1f));
            Place(folder.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(860f, 980f), new Vector2(0f, 0f));
            folder.rectTransform.localEulerAngles = new Vector3(0f, 0f, 2.5f);

            Image clip = CreateImage("Img_Clip", document, new Color(0.35f, 0.35f, 0.37f, 1f));
            Place(clip.rectTransform, new Vector2(0f, 0f), new Vector2(60f, 260f), new Vector2(90f, 500f), new Vector2(0f, 0f));

            // 종이 (아트: 구겨진 흰 종이)
            Image paper = CreateImage("Img_Paper", document, new Color(0.93f, 0.93f, 0.92f, 1f));
            Place(paper.rectTransform, new Vector2(0f, 0f), new Vector2(255f, 0f), new Vector2(725f, 940f), new Vector2(0f, 0f));
            RectTransform paperRt = paper.rectTransform;

            TextMeshProUGUI heading = CreateText("Txt_Heading", paperRt, bold, DocumentTitle, 26f, InkColor, TextAlignmentOptions.MidlineLeft);
            Place(heading.rectTransform, new Vector2(0f, 1f), new Vector2(86f, -115f), new Vector2(560f, 40f));

            TextMeshProUGUI body = CreateText("Txt_Body", paperRt, regular, PlaceholderBody, 21f, InkColor, TextAlignmentOptions.TopLeft);
            Place(body.rectTransform, new Vector2(0f, 1f), new Vector2(86f, -240f), new Vector2(560f, 440f));
            body.lineSpacing = 12f;
            body.paragraphSpacing = 55f;
            body.textWrappingMode = TextWrappingModes.Normal;

            // 서명 칸 (종이 오른쪽 아래)
            Image signBox = CreateImage("Img_SignBox", paperRt, new Color(0.86f, 0.86f, 0.86f, 1f));
            Place(signBox.rectTransform, new Vector2(1f, 1f), new Vector2(-60f, -700f), new Vector2(260f, 130f), new Vector2(1f, 1f));
            Outline border = signBox.gameObject.AddComponent<Outline>();
            border.effectColor = new Color(0.78f, 0.12f, 0.12f, 1f);
            border.effectDistance = new Vector2(3f, -3f);

            TextMeshProUGUI signHint = CreateText("Txt_SignHint", signBox.rectTransform, regular, "여기에 서명", 20f, new Color(0f, 0f, 0f, 0.35f), TextAlignmentOptions.Center);
            Stretch(signHint.rectTransform);

            GameObject padGo = new GameObject("SignaturePad", typeof(RectTransform), typeof(RawImage));
            padGo.transform.SetParent(signBox.transform, false);
            RectTransform padRt = (RectTransform)padGo.transform;
            Stretch(padRt);
            padRt.offsetMin = new Vector2(6f, 6f);
            padRt.offsetMax = new Vector2(-6f, -6f);
            SignaturePad pad = padGo.AddComponent<SignaturePad>();

            // 출근하기 (오른쪽 아래)
            Button startButton = CreateButton("Btn_Start", canvasRt, bold, "출근하기");
            Place((RectTransform)startButton.transform, new Vector2(1f, 0f), new Vector2(-110f, 90f), new Vector2(240f, 64f), new Vector2(1f, 0f));

            SerializedObject so = new SerializedObject(controller);
            so.FindProperty("signaturePad").objectReferenceValue = pad;
            so.FindProperty("signHint").objectReferenceValue = signHint.gameObject;
            so.FindProperty("startButton").objectReferenceValue = startButton;
            so.ApplyModifiedPropertiesWithoutUndo();

            if (EditorSceneManager.SaveScene(scene, ContractScenePath))
            {
                Debug.Log($"[ContractSceneSetup] 계약서 씬 생성: {ContractScenePath}");
            }
            else
            {
                Debug.LogError($"[ContractSceneSetup] 계약서 씬 저장 실패: {ContractScenePath}");
            }
        }
        finally
        {
            if (previous.IsValid()) SceneManager.SetActiveScene(previous);
            EditorSceneManager.CloseScene(scene, true);
        }
    }

    // ───────────────────────── UI 헬퍼 ─────────────────────────

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

    private static Button CreateButton(string name, Transform parent, TMP_FontAsset font, string text)
    {
        Image image = CreateImage(name, parent, new Color(1f, 1f, 1f, 0.12f));
        image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        image.type = Image.Type.Sliced;
        image.raycastTarget = true;

        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.disabledColor = new Color(1f, 1f, 1f, 0.3f);
        button.colors = colors;

        TextMeshProUGUI label = CreateText("Label", image.transform, font, text, 30f, Color.white, TextAlignmentOptions.Center);
        Stretch(label.rectTransform);
        return button;
    }

    /// <summary>anchor 한 점 기준으로 위치 · 크기를 정한다. pivot을 생략하면 anchor와 같게 둔다.</summary>
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

using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// ResultScene에 "야간 근무 일지" UI를 추가하고 ResultController에 연결한다.
/// 기존 수치 결산 화면(Content)은 Day 5용으로 그대로 두고, 근무 일지를 그 위(Fade 아래)에 만든다.
/// GameFlowSetup.Setup()에서 호출되며, 이미 있으면 건너뛴다.
/// </summary>
public static class DutyLogSetup
{
    private const string FontFolder = "Assets/3.2 Programmer_Kim/99 Resources/01 Fonts/Pretendard/";
    private const string RegularFontPath = FontFolder + "Pretendard-Medium SDF.asset";
    private const string BoldFontPath = FontFolder + "Pretendard-Bold SDF.asset";

    // 종이 문서 레이아웃 (1920 × 1080 기준)
    private const float PaperWidth = 1200f;
    private const float PaperHeight = 960f;
    private const float Padding = 64f;
    private const float RowHeight = 48f;

    private static readonly Color PaperColor = new Color(0.96f, 0.95f, 0.92f, 1f);
    private static readonly Color InkColor = new Color(0.12f, 0.12f, 0.12f, 1f);
    private static readonly Color SubInkColor = new Color(0.45f, 0.45f, 0.45f, 1f);
    private static readonly Color RuleLineColor = new Color(0f, 0f, 0f, 0.12f);
    private static readonly Color StrikeColor = new Color(0.8f, 0.13f, 0.13f, 1f);

    private const string ResultScenePath = "Assets/3.2 Programmer_Kim/01 Scene/ResultScene.unity";

    /// <summary>근무 일지 레이아웃 코드를 바꾼 뒤 씬에 다시 반영할 때 쓴다. 인스펙터에서 바꾼 근무 일지 값은 초기화된다.</summary>
    [MenuItem("Tools/Programmer_Kim/Game Flow/Rebuild Duty Log (ResultScene)")]
    private static void RebuildMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[GameFlowSetup] 플레이 모드에서는 실행할 수 없습니다.");
            return;
        }
        AddToResultScene(ResultScenePath, true);
    }

    public static void AddToResultScene(string scenePath, bool rebuild = false)
    {
        Scene previousActive = SceneManager.GetActiveScene();
        Scene scene = SceneManager.GetSceneByPath(scenePath);
        bool openedHere = !scene.isLoaded;
        if (openedHere)
        {
            scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
        }

        try
        {
            SceneManager.SetActiveScene(scene);

            GameObject canvasGo = null;
            ResultController controller = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == "Canvas_Result") canvasGo = root;
                if (controller == null) controller = root.GetComponentInChildren<ResultController>(true);
            }

            if (canvasGo == null || controller == null)
            {
                Debug.LogWarning("[GameFlowSetup] ResultScene에서 Canvas_Result 또는 ResultController를 찾지 못해 근무 일지 추가를 건너뜁니다.");
                return;
            }
            DutyLogView existing = canvasGo.GetComponentInChildren<DutyLogView>(true);
            if (existing != null)
            {
                if (!rebuild)
                {
                    Debug.Log("[GameFlowSetup] ResultScene에 근무 일지가 이미 있어 건너뜁니다.");
                    return;
                }
                Object.DestroyImmediate(existing.gameObject);
                Debug.Log("[GameFlowSetup] 기존 근무 일지를 지우고 다시 만듭니다.");
            }

            Transform content = canvasGo.transform.Find("Content");
            Transform fade = canvasGo.transform.Find("Fade");

            DutyLogView view = BuildDutyLog(canvasGo.transform);
            if (fade != null) view.transform.SetSiblingIndex(fade.GetSiblingIndex()); // Fade는 항상 맨 위

            SerializedObject so = new SerializedObject(controller);
            so.FindProperty("dutyLog").objectReferenceValue = view;
            so.FindProperty("summaryRoot").objectReferenceValue = content != null ? content.gameObject : null;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[GameFlowSetup] ResultScene에 근무 일지(DutyLog) 추가 및 ResultController 연결");
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

    private static DutyLogView BuildDutyLog(Transform canvas)
    {
        TMP_FontAsset regular = LoadFont(RegularFontPath);
        TMP_FontAsset bold = LoadFont(BoldFontPath);

        RectTransform root = CreateRect("DutyLog", canvas);
        Stretch(root);

        // 종이
        Image paper = CreateImage("Paper", root, PaperColor);
        RectTransform paperRt = paper.rectTransform;
        paperRt.anchorMin = new Vector2(0.5f, 0.5f);
        paperRt.anchorMax = new Vector2(0.5f, 0.5f);
        paperRt.pivot = new Vector2(0.5f, 0.5f);
        paperRt.sizeDelta = new Vector2(PaperWidth, PaperHeight);

        // ① 문서 머리
        TextMeshProUGUI title = CreateText("Txt_Title", paperRt, bold, "야간 근무 일지", 42f, InkColor, TextAlignmentOptions.MidlineLeft);
        PlaceRow(title.rectTransform, 48f, 64f);
        Image titleLine = CreateImage("TitleLine", paperRt, InkColor);
        PlaceRow(titleLine.rectTransform, 118f, 2f);

        RectTransform meta = CreateRect("Meta", paperRt);
        PlaceRow(meta, 132f, 40f);
        TextMeshProUGUI metaDay = CreateText("Txt_Day", meta, regular, "근무일 1일차 / 5", 22f, InkColor, TextAlignmentOptions.MidlineLeft);
        SetColumn(metaDay.rectTransform, 0f, 0.36f);
        TextMeshProUGUI metaHours = CreateText("Txt_Hours", meta, regular, "근무 시간 12:00–06:00", 22f, InkColor, TextAlignmentOptions.MidlineLeft);
        SetColumn(metaHours.rectTransform, 0.36f, 0.7f);
        TextMeshProUGUI metaPatrol = CreateText("Txt_Patrol", meta, regular, "순찰 지점 4곳", 22f, InkColor, TextAlignmentOptions.MidlineLeft);
        SetColumn(metaPatrol.rectTransform, 0.7f, 1f);

        // ② 금일 근무 지침
        TextMeshProUGUI rulesLabel = CreateText("Txt_RulesLabel", paperRt, regular, "금일 근무 지침", 18f, SubInkColor, TextAlignmentOptions.MidlineLeft);
        rulesLabel.characterSpacing = 8f;
        PlaceRow(rulesLabel.rectTransform, 196f, 30f);

        RectTransform rowsRt = CreateRect("Rows", paperRt);
        PlaceRow(rowsRt, 230f, 440f);
        VerticalLayoutGroup rowsLayout = rowsRt.gameObject.AddComponent<VerticalLayoutGroup>();
        rowsLayout.childAlignment = TextAnchor.UpperLeft;
        rowsLayout.childControlWidth = true;
        rowsLayout.childControlHeight = true;
        rowsLayout.childForceExpandWidth = true;
        rowsLayout.childForceExpandHeight = false;

        GameObject rowTemplate = BuildRowTemplate(rowsRt, regular);

        // ③ 특이사항
        TextMeshProUGUI notesLabel = CreateText("Txt_NotesLabel", paperRt, regular, "특이사항", 18f, SubInkColor, TextAlignmentOptions.MidlineLeft);
        notesLabel.characterSpacing = 8f;
        PlaceRow(notesLabel.rectTransform, 690f, 30f);

        Image notesBox = CreateImage("NotesBox", paperRt, new Color(0f, 0f, 0f, 0.06f));
        PlaceRow(notesBox.rectTransform, 724f, 56f);
        TextMeshProUGUI notesText = CreateText("Txt_Notes", notesBox.rectTransform, regular, "없음", 22f, SubInkColor, TextAlignmentOptions.MidlineLeft);
        Stretch(notesText.rectTransform);
        notesText.rectTransform.offsetMin = new Vector2(20f, 0f);

        // ④ 꼬리
        Image tailLine = CreateImage("TailLine", paperRt, InkColor);
        PlaceRow(tailLine.rectTransform, 806f, 2f);

        TextMeshProUGUI disclaimer = CreateText("Txt_Disclaimer", paperRt, regular,
            "본 지침 미준수로 발생한 사고 및 신체적 손상에 대해\n회사는 책임지지 않습니다.", 17f, SubInkColor, TextAlignmentOptions.TopLeft);
        PlaceRow(disclaimer.rectTransform, 826f, 64f, 0f, 440f);

        Image buttonImage = CreateImage("Btn_Confirm", paperRt, InkColor);
        buttonImage.raycastTarget = true;
        RectTransform buttonRt = buttonImage.rectTransform;
        buttonRt.anchorMin = new Vector2(1f, 1f);
        buttonRt.anchorMax = new Vector2(1f, 1f);
        buttonRt.pivot = new Vector2(1f, 1f);
        buttonRt.anchoredPosition = new Vector2(-Padding, -818f);
        buttonRt.sizeDelta = new Vector2(190f, 60f);
        Button confirm = buttonImage.gameObject.AddComponent<Button>();
        confirm.targetGraphic = buttonImage;
        TextMeshProUGUI confirmLabel = CreateText("Label", buttonRt, bold, "확인", 24f, Color.white, TextAlignmentOptions.Center);
        Stretch(confirmLabel.rectTransform);

        TextMeshProUGUI remaining = CreateText("Txt_Remaining", paperRt, regular, "계약 잔여 4일", 18f, SubInkColor, TextAlignmentOptions.MidlineRight);
        RectTransform remainingRt = remaining.rectTransform;
        remainingRt.anchorMin = new Vector2(1f, 1f);
        remainingRt.anchorMax = new Vector2(1f, 1f);
        remainingRt.pivot = new Vector2(1f, 1f);
        remainingRt.anchoredPosition = new Vector2(-Padding, -884f);
        remainingRt.sizeDelta = new Vector2(300f, 30f);

        DutyLogView view = root.gameObject.AddComponent<DutyLogView>();
        SerializedObject so = new SerializedObject(view);
        so.FindProperty("dayText").objectReferenceValue = metaDay;
        so.FindProperty("hoursText").objectReferenceValue = metaHours;
        so.FindProperty("patrolText").objectReferenceValue = metaPatrol;
        so.FindProperty("rowTemplate").objectReferenceValue = rowTemplate;
        so.FindProperty("specialNotesText").objectReferenceValue = notesText;
        so.FindProperty("disclaimerText").objectReferenceValue = disclaimer;
        so.FindProperty("confirmButton").objectReferenceValue = confirm;
        so.FindProperty("remainingText").objectReferenceValue = remaining;
        so.ApplyModifiedPropertiesWithoutUndo();

        return view;
    }

    /// <summary>줄 템플릿: Number · Body(Strike) · Separator</summary>
    private static GameObject BuildRowTemplate(Transform parent, TMP_FontAsset font)
    {
        RectTransform row = CreateRect("Row_Template", parent);
        LayoutElement rowLayout = row.gameObject.AddComponent<LayoutElement>();
        rowLayout.preferredHeight = RowHeight;
        rowLayout.minHeight = RowHeight;

        TextMeshProUGUI number = CreateText("Number", row, font, "1.", 22f, InkColor, TextAlignmentOptions.MidlineLeft);
        RectTransform numberRt = number.rectTransform;
        numberRt.anchorMin = new Vector2(0f, 0f);
        numberRt.anchorMax = new Vector2(0f, 1f);
        numberRt.pivot = new Vector2(0f, 0.5f);
        numberRt.anchoredPosition = new Vector2(12f, 0f);
        numberRt.sizeDelta = new Vector2(40f, 0f);

        TextMeshProUGUI body = CreateText("Body", row, font, "지침 문구", 22f, InkColor, TextAlignmentOptions.MidlineLeft);
        body.textWrappingMode = TextWrappingModes.NoWrap;
        body.enableAutoSizing = true;
        body.fontSizeMin = 16f;
        body.fontSizeMax = 22f;
        Stretch(body.rectTransform);
        body.rectTransform.offsetMin = new Vector2(56f, 0f);

        // 스프라이트 없는 단색 막대. 길이는 DutyLogView가 글자 길이에 맞춰 0에서부터 늘린다
        Image strike = CreateImage("Strike", body.rectTransform, StrikeColor);
        RectTransform strikeRt = strike.rectTransform;
        strikeRt.anchorMin = new Vector2(0f, 0.5f);
        strikeRt.anchorMax = new Vector2(0f, 0.5f);
        strikeRt.pivot = new Vector2(0f, 0.5f);
        strikeRt.anchoredPosition = Vector2.zero;
        strikeRt.sizeDelta = new Vector2(0f, 3f);

        Image separator = CreateImage("Separator", row, RuleLineColor);
        RectTransform separatorRt = separator.rectTransform;
        separatorRt.anchorMin = new Vector2(0f, 0f);
        separatorRt.anchorMax = new Vector2(1f, 0f);
        separatorRt.pivot = new Vector2(0.5f, 0f);
        separatorRt.anchoredPosition = Vector2.zero;
        separatorRt.sizeDelta = new Vector2(0f, 1f);

        row.gameObject.SetActive(false);
        return row.gameObject;
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

    /// <summary>종이 안에서 위에서부터 top 위치, height 높이의 가로 영역 (좌우 여백 = Padding + 추가 여백)</summary>
    private static void PlaceRow(RectTransform rt, float top, float height, float extraLeft = 0f, float extraRight = 0f)
    {
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -top);
        rt.offsetMin = new Vector2(Padding + extraLeft, rt.offsetMin.y);
        rt.offsetMax = new Vector2(-(Padding + extraRight), rt.offsetMax.y);
        rt.sizeDelta = new Vector2(rt.sizeDelta.x, height);
        rt.anchoredPosition = new Vector2((extraLeft - extraRight) * 0.5f, -top);
    }

    private static void SetColumn(RectTransform rt, float from, float to)
    {
        rt.anchorMin = new Vector2(from, 0f);
        rt.anchorMax = new Vector2(to, 1f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}

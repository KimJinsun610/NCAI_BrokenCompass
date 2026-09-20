using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 1인칭 손 + 태블릿 리그(HUD_Tablet)를 만든다.
///
/// 본체는 `99 Resources/03 asset/Tablet/model.obj` 모델이다. 다른 모델로 바꾸려면
/// BodyModelPath와 화면(ScreenPosition·ScreenSizeMm) 값을 새 모델의 액정에 맞춰 고치면 된다.
/// </summary>
public static class TabletSetup
{
    private const string Root = "Assets/3.2 Programmer_Kim";
    private const string PrefabPath = Root + "/03 Prefebs/01 UI/HUD_Tablet.prefab";
    private const string MaterialFolder = Root + "/04 Materials";
    private const string BodyMaterialPath = MaterialFolder + "/Tablet_Model.mat";
    private const string HandsFolder = Root + "/99 Resources/03 asset/FirstPersonHands/Prefabs";
    // 오른손 프리팹은 지금 쓰지 않는다. 두 손으로 들 때 쓰려면: HandsFolder + "/firstPersonHand.prefab"
    private const string LeftHandPath = HandsFolder + "/firstPersonHand_left.prefab";
    private const string FontPath = Root + "/99 Resources/01 Fonts/Pretendard/Pretendard-Medium SDF.asset";

    // 태블릿 본체 모델. 원본은 1유닛 높이라 0.26을 곱해서 26cm짜리 단말기로 만든다.
    private const string BodyModelPath = Root + "/99 Resources/03 asset/Tablet/model.obj";
    private const string BodyMaterialSourceFolder = Root + "/99 Resources/03 asset/Tablet";
    private const float BodyScale = 0.26f;
    // 모델 원점이 바닥이라, 본체 가운데가 리그 원점에 오도록 절반만큼 내린다.
    private static readonly Vector3 BodyOffset = new Vector3(0f, -0.5f * BodyScale, 0f);

    // 화면(액정) 위치·크기. 모델의 액정 홈에 맞춰 눈으로 맞춘 값이다.
    private static readonly Vector3 ScreenPosition = new Vector3(0.0022f, 0.0211f, -0.0475f);
    private static readonly Vector2 ScreenSizeMm = new Vector2(102f, 125f);

    // 왼손 하나로 왼쪽 모서리를 쥔다(2026-09-20 모델 교체에 맞춰 다시 맞춘 값). 오른손은 다른 동작용으로 비워 둔다.
    private static readonly Vector3 LeftHandPosition = new Vector3(-0.098f, -0.070f, -0.012f);
    // 손 방향은 각도를 직접 적지 않고 "손가락이 향할 방향"과 "손바닥이 향할 방향"으로 정한다.
    // 손 프리팹은 팔뚝이 +Z, 손가락이 -Z, 손바닥이 +Y를 향하고 있어서 LookRotation(팔뚝방향, 손바닥방향)으로 맞춘다.
    private static readonly Vector3 LeftFingerDirection = new Vector3(0.182f, 0.907f, 0.380f);
    private static readonly Vector3 LeftPalmDirection = new Vector3(-0.918f, 0.018f, 0.396f);

    // 한 손으로 드는 만큼 살짝 기울여 든다.
    private static readonly Vector3 OpenedPosition = new Vector3(-0.015f, -0.03f, 0.42f);
    private static readonly Vector3 OpenedRotation = new Vector3(10f, -4f, -7f);

    // 손이 취할 포즈. 에디터에서도 같은 포즈로 보이도록 프리팹을 만들 때 한 번 샘플링해 둔다.
    private const string HandPoseState = "hand_pose_cupped";
    private const float HandPoseNormalizedTime = 0.5f;
    private const string HandClipSourcePath = Root + "/99 Resources/03 asset/FirstPersonHands/MaleHands/firstPersonHand.FBX";

    [MenuItem("Tools/Programmer_Kim/Tablet/Setup Tablet Rig", priority = 40)]
    public static void Setup()
    {
        // 이미 만들어 둔 리그가 있으면 손 위치 같은 조정값이 날아간다. 먼저 물어본다.
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
        {
            bool overwrite = EditorUtility.DisplayDialog(
                "태블릿 리그 다시 만들기",
                "HUD_Tablet 프리팹이 이미 있습니다.\n다시 만들면 손 위치·각도 등 직접 맞춘 값이 스크립트 기본값으로 되돌아갑니다.\n\n계속할까요?",
                "다시 만들기", "취소");
            if (!overwrite) return;
        }

        GameObject root = Build();
        if (root == null) return;

        EnsureFolder(Root + "/03 Prefebs", "01 UI");
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[TabletSetup] 태블릿 리그를 만들었습니다: " + PrefabPath);
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
    }

    [MenuItem("Tools/Programmer_Kim/Tablet/Preview In Temp Scene", priority = 41)]
    public static void Preview()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
        {
            Debug.LogError("[TabletSetup] 먼저 Setup Tablet Rig를 실행하세요.");
            return;
        }

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        GameObject camGo = new GameObject("Preview Camera", typeof(Camera));
        camGo.tag = "MainCamera";
        Camera cam = camGo.GetComponent<Camera>();
        cam.fieldOfView = 50f;          // FPController 카메라와 같은 화각
        cam.nearClipPlane = 0.05f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.08f, 0.08f, 0.09f);

        GameObject lightGo = new GameObject("Preview Light", typeof(Light));
        Light light = lightGo.GetComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.1f;
        lightGo.transform.rotation = Quaternion.Euler(35f, 200f, 0f);

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        instance.transform.SetParent(camGo.transform, false);

        PlayerTablet tablet = instance.GetComponent<PlayerTablet>();
        if (tablet != null) tablet.ApplyPose(1f);   // 올린 자세로 보여 준다

        Debug.Log("[TabletSetup] 임시 씬에 미리보기를 놓았습니다. 저장하지 마세요.");
    }

    private static GameObject Build()
    {
        GameObject leftHandPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(LeftHandPath);
        if (leftHandPrefab == null)
        {
            Debug.LogError("[TabletSetup] 손 프리팹을 찾을 수 없습니다: " + HandsFolder);
            return null;
        }

        GameObject root = new GameObject("HUD_Tablet");

        // --- 본체 ---
        GameObject bodyModel = AssetDatabase.LoadAssetAtPath<GameObject>(BodyModelPath);
        if (bodyModel == null)
        {
            Debug.LogError("[TabletSetup] 태블릿 모델을 찾을 수 없습니다: " + BodyModelPath);
            Object.DestroyImmediate(root);
            return null;
        }

        GameObject body = (GameObject)PrefabUtility.InstantiatePrefab(bodyModel);
        // 모델 프리팹 연결을 끊어야 이 리그 안에서 재질·그림자 설정을 따로 가질 수 있다.
        PrefabUtility.UnpackPrefabInstance(body, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        body.name = "Body";
        body.transform.SetParent(root.transform, false);
        body.transform.localScale = Vector3.one * BodyScale;
        body.transform.localPosition = BodyOffset;

        Material bodyMaterial = LoadOrCreateBodyMaterial();
        foreach (MeshRenderer renderer in body.GetComponentsInChildren<MeshRenderer>(true))
        {
            renderer.sharedMaterial = bodyMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        foreach (Collider collider in body.GetComponentsInChildren<Collider>(true))
        {
            Object.DestroyImmediate(collider);   // 1인칭 소품이라 충돌 필요 없음
        }

        // --- 화면(월드 캔버스) ---
        GameObject screen = new GameObject("Screen", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        screen.transform.SetParent(root.transform, false);
        // 캔버스 1단위 = 1mm가 되도록 0.001 배율을 쓴다. 폰트 크기를 mm로 다루게 된다.
        screen.transform.localScale = Vector3.one * 0.001f;
        screen.transform.localPosition = ScreenPosition;

        Canvas canvas = screen.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        RectTransform screenRect = screen.GetComponent<RectTransform>();
        screenRect.sizeDelta = ScreenSizeMm;

        GameObject bgGo = new GameObject("BG", typeof(RectTransform), typeof(Image));
        bgGo.transform.SetParent(screen.transform, false);
        Image bg = bgGo.GetComponent<Image>();
        bg.color = new Color(0.05f, 0.07f, 0.09f);
        Stretch(bg.rectTransform);

        GameObject textGo = new GameObject("Txt_Placeholder", typeof(RectTransform));
        textGo.transform.SetParent(screen.transform, false);
        TextMeshProUGUI text = textGo.AddComponent<TextMeshProUGUI>();
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font != null) text.font = font;
        text.text = "근무수칙\n\n여기에 지침록 내용이 들어갑니다.";
        text.fontSize = 11f;            // 캔버스 1단위 = 1mm
        text.color = new Color(0.85f, 0.92f, 0.95f);
        text.alignment = TextAlignmentOptions.TopLeft;
        text.margin = new Vector4(10f, 10f, 10f, 10f);
        Stretch(text.rectTransform);

        // --- 손 ---
        GameObject leftHand = (GameObject)PrefabUtility.InstantiatePrefab(leftHandPrefab);
        leftHand.name = "Hand_L";
        leftHand.transform.SetParent(root.transform, false);
        leftHand.transform.localPosition = LeftHandPosition;
        leftHand.transform.localRotation = Quaternion.LookRotation(-LeftFingerDirection.normalized, LeftPalmDirection.normalized);

        SampleHandPose(leftHand);
        DisableHandAnimator(leftHand);

        // --- 컴포넌트 연결 ---
        PlayerTablet tablet = root.AddComponent<PlayerTablet>();
        tablet.screenRoot = screen;
        tablet.openedPosition = OpenedPosition;
        tablet.openedRotation = OpenedRotation;

        return root;
    }

    /// <summary>
    /// 에디터에서도 쥔 손 모양으로 보이도록 포즈 클립을 한 번 적용한다.
    /// 실행 중에는 PlayerTablet이 같은 포즈를 애니메이터로 다시 잡는다.
    /// </summary>
    private static void SampleHandPose(GameObject hand)
    {
        AnimationClip clip = null;
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(HandClipSourcePath))
        {
            AnimationClip candidate = asset as AnimationClip;
            if (candidate != null && candidate.name == HandPoseState) clip = candidate;
        }

        if (clip == null)
        {
            Debug.LogWarning("[TabletSetup] 손 포즈 클립을 찾지 못했습니다: " + HandPoseState);
            return;
        }

        clip.SampleAnimation(hand, clip.length * HandPoseNormalizedTime);
    }

    /// <summary>
    /// 손은 프리팹에 저장된 포즈 그대로 쓴다. Animator가 켜져 있으면 실행할 때
    /// 기본 상태(hand_allAnimations)를 재생해서 포즈를 덮어쓰므로 꺼 둔다.
    /// 손을 실제로 움직일 일이 생기면 그때 다시 켜고 직접 제어할 것.
    /// </summary>
    private static void DisableHandAnimator(GameObject go)
    {
        Animator animator = go.GetComponentInChildren<Animator>(true);
        if (animator != null) animator.enabled = false;
    }

    /// <summary>
    /// 본체 재질. 모델에 딸려 온 재질은 노멀맵이 빠져 있어서, 베이스 컬러와 노멀맵을
    /// 둘 다 물린 URP 재질을 따로 만들어 쓴다.
    /// </summary>
    private static Material LoadOrCreateBodyMaterial()
    {
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(BodyMaterialPath);
        if (existing != null) return existing;

        Material material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        material.name = "Tablet_Model";

        Texture baseColor = AssetDatabase.LoadAssetAtPath<Texture>(BodyMaterialSourceFolder + "/model-material_0-base_color.png");
        Texture normal = AssetDatabase.LoadAssetAtPath<Texture>(BodyMaterialSourceFolder + "/model-material_0-normal.png");
        if (baseColor != null) material.SetTexture("_BaseMap", baseColor);
        if (normal != null)
        {
            material.SetTexture("_BumpMap", normal);
            material.EnableKeyword("_NORMALMAP");
        }
        material.SetFloat("_Smoothness", 0.25f);
        material.SetFloat("_Metallic", 0f);

        EnsureFolder(Root, "04 Materials");
        AssetDatabase.CreateAsset(material, BodyMaterialPath);
        return material;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void EnsureFolder(string parent, string folderName)
    {
        if (!AssetDatabase.IsValidFolder(parent + "/" + folderName))
        {
            AssetDatabase.CreateFolder(parent, folderName);
        }
    }
}

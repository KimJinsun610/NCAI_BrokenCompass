using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 경비실 전화기(근무 일찍 끝내기)와 상호작용 외곽선 머티리얼을 준비한다(2026-10-03 민 요청).
/// 메뉴 「야간근무/경비실/전화기 놓기」 — 여러 번 눌러도 같은 결과(이미 있으면 자리만 다시 맞춘다).
/// <list type="bullet">
/// <item>벤더 <c>Telephone01</c> 프리팹을 씬 루트 <c>Interactables/ShiftEndPhone</c>로 놓는다 — 자리 = 경비실 북쪽 책상
/// <c>TeacherTable02_static</c> 왼쪽 끝(펜·스탠드와 겹치지 않는 곳), 수화기가 방 안(남쪽)을 보게.</item>
/// <item>조준이 잡히도록 단단한 <see cref="BoxCollider"/>를 하나 붙이고(프리팹 콜라이더가 있어도 겹쳐서 무해) <see cref="ShiftEndPhone"/>을 붙인다.</item>
/// <item><c>Resources/InteractionOutline.mat</c>·<c>InteractionOutlineMask.mat</c>(외곽선·마스크 셰이더)이 없으면 만든다.</item>
/// </list>
/// </summary>
public static class GuardRoomPhoneBuilder
{
    private const string PhonePrefab = "Assets/NOT_Lonely/HQ_AbandonedSchool/Prefabs/Telephone01.prefab";
    private const string MaterialAsset = "Assets/_Game/Resources/InteractionOutline.mat";
    private const string ShaderName = "NightDuty/InteractionOutline";
    private const string MaskAsset = "Assets/_Game/Resources/InteractionOutlineMask.mat";
    private const string MaskShaderName = "NightDuty/InteractionOutlineMask";
    private const string RootName = "Interactables";
    private const string PhoneName = "ShiftEndPhone";
    private const string DeskName = "TeacherTable02_static";

    /// <summary>책상 위 자리(x, z)와 방향(yaw). 높이는 책상 윗면을 재서 맞춘다.</summary>
    private static readonly Vector3 PhoneSpot = new Vector3(31.35f, 0f, 47.55f);
    private const float PhoneYaw = 180f;

    [MenuItem("야간근무/경비실/전화기 놓기")]
    public static void BuildMenu()
    {
        Debug.Log(Build());
    }

    public static string Build()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine(EnsureMaterial(MaterialAsset, ShaderName, "InteractionOutline"));
        sb.AppendLine(EnsureMaterial(MaskAsset, MaskShaderName, "InteractionOutlineMask"));

        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(PhonePrefab);
        if (asset == null) return sb.Append("✗ 전화기 프리팹 없음: " + PhonePrefab).ToString();

        GameObject root = GameObject.Find(RootName);
        if (root == null)
        {
            root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "전화기 놓기");
        }

        Transform phone = root.transform.Find(PhoneName);
        if (phone == null)
        {
            GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(asset, root.transform);
            Undo.RegisterCreatedObjectUndo(go, "전화기 놓기");
            go.name = PhoneName;
            phone = go.transform;
        }

        float top = 2.11f;
        RaycastHit hit;
        if (Physics.Raycast(new Vector3(PhoneSpot.x, 3.2f, PhoneSpot.z), Vector3.down, out hit, 3f, ~0, QueryTriggerInteraction.Ignore)
            && hit.collider.transform.IsChildOf(phone) == false)
        {
            top = hit.point.y;
            sb.AppendLine("책상 윗면 " + top.ToString("F3") + " (" + hit.collider.name + ")");
        }

        Undo.RecordObject(phone, "전화기 자리");
        phone.SetPositionAndRotation(new Vector3(PhoneSpot.x, top, PhoneSpot.z), Quaternion.Euler(0f, PhoneYaw, 0f));

        // 조준용 단단한 상자(렌더러 경계에 맞춤).
        Bounds b = new Bounds(phone.position, Vector3.zero);
        bool any = false;
        foreach (Renderer r in phone.GetComponentsInChildren<Renderer>())
        {
            if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
        }

        BoxCollider box = phone.GetComponent<BoxCollider>();
        if (box == null) box = Undo.AddComponent<BoxCollider>(phone.gameObject);
        box.isTrigger = false;
        if (any)
        {
            box.center = phone.InverseTransformPoint(b.center);
            Vector3 s = phone.InverseTransformVector(b.size);
            box.size = new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z)) + Vector3.one * 0.02f;
        }

        if (phone.GetComponent<ShiftEndPhone>() == null) Undo.AddComponent<ShiftEndPhone>(phone.gameObject);

        EditorUtility.SetDirty(phone.gameObject);
        EditorSceneManager.MarkSceneDirty(phone.gameObject.scene);
        sb.Append("✓ 전화기 @" + phone.position.ToString("F2") + " 크기 " + b.size.ToString("F2"));
        return sb.ToString();
    }

    private static string EnsureMaterial(string assetPath, string shaderName, string name)
    {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
        Shader shader = Shader.Find(shaderName);
        if (shader == null) return "✗ 셰이더 없음: " + shaderName;
        if (mat != null)
        {
            if (mat.shader != shader) { mat.shader = shader; EditorUtility.SetDirty(mat); }
            return "– 머티리얼 있음: " + name;
        }

        mat = new Material(shader) { name = name };
        AssetDatabase.CreateAsset(mat, assetPath);
        AssetDatabase.SaveAssets();
        return "✓ 머티리얼 만듦: " + assetPath;
    }
}

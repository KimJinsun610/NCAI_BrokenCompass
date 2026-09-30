using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// CCTV 설정 인스펙터. 채널마다 두 버튼을 붙인다.
/// <list type="bullet">
/// <item><b>씬 뷰로 보기</b> — 씬 뷰 카메라를 그 채널 자리로 옮긴다. 화각을 눈으로 확인한다.</item>
/// <item><b>현재 씬 뷰로 저장</b> — 씬 뷰를 원하는 자리로 옮긴 뒤 누르면 그 자리가 채널이 된다.</item>
/// </list>
/// <para>밤 씬이 어두워 씬 뷰로는 잘 안 보일 수 있다. 화면 모양은 플레이해서 모니터로 확인한다.</para>
/// </summary>
[CustomEditor(typeof(CctvConfigSO))]
public sealed class CctvConfigSOEditor : Editor
{
    private const string AssetPath = "Assets/_Game/Resources/CctvConfig.asset";

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        CctvConfigSO cfg = (CctvConfigSO)target;
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("채널 자리 맞추기", EditorStyles.boldLabel);

        for (int i = 0; i < cfg.EditableChannels.Count; i++)
        {
            CctvConfigSO.Channel ch = cfg.EditableChannels[i];
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField((i + 1) + ". " + ch.label, GUILayout.Width(110));

                if (GUILayout.Button("씬 뷰로 보기"))
                {
                    LookFrom(ch);
                }

                if (GUILayout.Button("현재 씬 뷰로 저장"))
                {
                    SaveFromSceneView(cfg, ch);
                }
            }
        }

        EditorGUILayout.Space(4);
        if (GUILayout.Button("기본 채널 5개로 되돌리기"))
        {
            Undo.RecordObject(cfg, "CCTV 기본값");
            cfg.FillDefaults();
            EditorUtility.SetDirty(cfg);
        }
    }

    private static void LookFrom(CctvConfigSO.Channel ch)
    {
        SceneView sv = SceneView.lastActiveSceneView;
        if (sv == null)
        {
            return;
        }

        Vector3 dir = ch.lookAt - ch.position;
        if (dir.sqrMagnitude < 0.0001f)
        {
            dir = Vector3.forward;
        }

        GameObject temp = new GameObject("__cctv_view");
        try
        {
            temp.transform.SetPositionAndRotation(ch.position, Quaternion.LookRotation(dir));
            sv.orthographic = false;
            sv.cameraSettings.fieldOfView = ch.fieldOfView;
            sv.AlignViewToObject(temp.transform);
            sv.Repaint();
        }
        finally
        {
            DestroyImmediate(temp);
        }
    }

    private static void SaveFromSceneView(CctvConfigSO cfg, CctvConfigSO.Channel ch)
    {
        SceneView sv = SceneView.lastActiveSceneView;
        if (sv == null || sv.camera == null)
        {
            return;
        }

        Undo.RecordObject(cfg, "CCTV 채널 자리 저장");
        Transform cam = sv.camera.transform;
        ch.position = cam.position;
        ch.lookAt = cam.position + cam.forward * 10f;
        EditorUtility.SetDirty(cfg);
    }

    /// <summary>기본값으로 채운 설정 에셋을 <c>Resources/</c>에 만든다. 이미 있으면 그것을 고른다.</summary>
    [MenuItem("NightDuty/CCTV 설정 에셋 만들기")]
    private static void CreateAsset()
    {
        CctvConfigSO existing = AssetDatabase.LoadAssetAtPath<CctvConfigSO>(AssetPath);
        if (existing != null)
        {
            Selection.activeObject = existing;
            return;
        }

        string dir = Path.GetDirectoryName(AssetPath);
        if (!string.IsNullOrEmpty(dir) && !AssetDatabase.IsValidFolder(dir))
        {
            Directory.CreateDirectory(dir);
            AssetDatabase.Refresh();
        }

        CctvConfigSO cfg = CreateInstance<CctvConfigSO>();
        cfg.FillDefaults();
        AssetDatabase.CreateAsset(cfg, AssetPath);
        AssetDatabase.SaveAssets();
        Selection.activeObject = cfg;
        Debug.Log("[CCTV] 설정 에셋을 만들었습니다: " + AssetPath);
    }
}

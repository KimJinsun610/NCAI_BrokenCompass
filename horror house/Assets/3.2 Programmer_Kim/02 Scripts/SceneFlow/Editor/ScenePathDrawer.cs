using UnityEditor;
using UnityEngine;

/// <summary>
/// [ScenePath] string 필드를 씬 에셋 드래그 칸으로 보여 준다.
/// Build Settings에 없는 씬이면 경고와 함께 "Build에 추가" 버튼을 띄운다.
/// </summary>
[CustomPropertyDrawer(typeof(ScenePathAttribute))]
public class ScenePathDrawer : PropertyDrawer
{
    private const float Spacing = 2f;
    private const float ButtonWidth = 90f;

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float height = EditorGUIUtility.singleLineHeight;
        if (property.propertyType == SerializedPropertyType.String && NeedsBuildWarning(property.stringValue))
        {
            height += Spacing + EditorGUIUtility.singleLineHeight * 2f;
        }
        return height;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (property.propertyType != SerializedPropertyType.String)
        {
            EditorGUI.LabelField(position, label.text, "[ScenePath]는 string 필드에만 사용할 수 있습니다.");
            return;
        }

        Rect line = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        SceneAsset current = AssetDatabase.LoadAssetAtPath<SceneAsset>(property.stringValue);

        EditorGUI.BeginProperty(line, label, property);
        EditorGUI.BeginChangeCheck();
        SceneAsset picked = (SceneAsset)EditorGUI.ObjectField(line, label, current, typeof(SceneAsset), false);
        if (EditorGUI.EndChangeCheck())
        {
            property.stringValue = picked != null ? AssetDatabase.GetAssetPath(picked) : string.Empty;
        }
        EditorGUI.EndProperty();

        if (!NeedsBuildWarning(property.stringValue)) return;

        Rect box = new Rect(position.x, line.yMax + Spacing, position.width - ButtonWidth - 4f, EditorGUIUtility.singleLineHeight * 2f);
        EditorGUI.HelpBox(box, "Build Settings에 없는 씬입니다.", MessageType.Warning);

        Rect button = new Rect(box.xMax + 4f, box.y, ButtonWidth, box.height);
        if (GUI.Button(button, "Build에 추가"))
        {
            SceneFlowSetup.AddToBuildSettings(property.stringValue, false);
        }
    }

    private static bool NeedsBuildWarning(string path)
    {
        if (string.IsNullOrEmpty(path)) return false;

        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
        {
            if (scene.path == path && scene.enabled) return false;
        }
        return true;
    }
}

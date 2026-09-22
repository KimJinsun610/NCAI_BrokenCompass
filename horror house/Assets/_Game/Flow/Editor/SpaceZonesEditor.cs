using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

/// <summary>
/// <see cref="SpaceZones"/>의 상자들을 씬 뷰에서 손으로 끌어 조절한다.
/// 색은 기즈모와 같다: 초록 = 근무 공간, 빨강 = 제외, 파랑 = 점검 자리, 노랑 = 신호 구역, 흰색 = 건물 범위.
/// 점검 상자 크기가 0이면 판정을 안 하겠다는 뜻이라 핸들도 안 그린다.
/// </summary>
[CustomEditor(typeof(SpaceZones))]
public sealed class SpaceZonesEditor : Editor
{
    private static readonly Color Room = new Color(0.3f, 1f, 0.6f);
    private static readonly Color OutOfScope = new Color(1f, 0.3f, 0.3f);
    private static readonly Color Inspection = new Color(0.2f, 0.7f, 1f);
    private static readonly Color Signal = new Color(1f, 0.9f, 0.3f);

    private readonly BoxBoundsHandle _handle = new BoxBoundsHandle();

    private void OnSceneGUI()
    {
        serializedObject.Update();

        SerializedProperty zones = serializedObject.FindProperty("zones");
        for (int i = 0; i < zones.arraySize; i++)
        {
            SerializedProperty z = zones.GetArrayElementAtIndex(i);
            SerializedProperty space = z.FindPropertyRelative("Space");
            bool skip = z.FindPropertyRelative("OutOfScope").boolValue;
            string name = EnumName(space);

            Draw(z.FindPropertyRelative("Box"), skip ? OutOfScope : Room, name);
            Draw(z.FindPropertyRelative("InspectionBox"), Inspection, name + " 점검");
        }

        SerializedProperty signals = serializedObject.FindProperty("signalZones");
        for (int i = 0; i < signals.arraySize; i++)
        {
            SerializedProperty s = signals.GetArrayElementAtIndex(i);
            Draw(s.FindPropertyRelative("Box"), Signal, s.FindPropertyRelative("Id").stringValue);
        }

        Draw(serializedObject.FindProperty("building"), Color.white, "건물 범위");

        serializedObject.ApplyModifiedProperties();
    }

    private void Draw(SerializedProperty prop, Color color, string label)
    {
        Bounds box = prop.boundsValue;
        if (box.size.sqrMagnitude <= 0f) return;   // 크기 0 = 사용 안 함

        _handle.center = box.center;
        _handle.size = box.size;
        _handle.wireframeColor = color;
        _handle.handleColor = color;

        EditorGUI.BeginChangeCheck();
        _handle.DrawHandle();
        if (EditorGUI.EndChangeCheck())
        {
            box.center = _handle.center;
            box.size = _handle.size;
            prop.boundsValue = box;
        }

        Handles.color = color;
        Handles.Label(box.center + Vector3.up * (box.size.y * 0.5f + 0.3f), label);
    }

    private static string EnumName(SerializedProperty prop)
    {
        int i = prop.enumValueIndex;
        string[] names = prop.enumDisplayNames;
        return i >= 0 && i < names.Length ? names[i] : "?";
    }
}

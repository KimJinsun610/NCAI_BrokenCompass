using UnityEditor;
using UnityEngine;

namespace NightDuty.PrologueSample.Editor
{
    // 재생 중 Inspector에 「원하는 줄부터 보기」 버튼을 보여 줍니다. 에디터 전용(빌드에 들어가지 않음), 씬 값은 바꾸지 않습니다.
    [CustomEditor(typeof(DispatchCallSample))]
    public sealed class DispatchCallSampleEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var sample = (DispatchCallSample)target;
            EditorGUILayout.Space(2);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("원하는 줄부터 보기", EditorStyles.boldLabel);
                if (!Application.isPlaying)
                {
                    EditorGUILayout.HelpBox("재생(▶)을 시작하면 버튼이 켜집니다.", MessageType.Info);
                }
                else
                {
                    EditorGUILayout.LabelField("지금 줄: " + (sample.CurrentLine >= 0 ? (sample.CurrentLine + 1).ToString() : "-") + "  (" + sample.Stage + ")");
                    var lines = DispatchCallSample.Lines;
                    for (int i = 0; i < lines.Length; i++)
                    {
                        string text = lines[i];
                        if (text.Length > 26) text = text.Substring(0, 26) + "…";
                        if (GUILayout.Button((i + 1) + "번  " + text)) sample.PlayFromLine(i);
                    }
                }
            }
            EditorGUILayout.Space(4);
            DrawDefaultInspector();
            if (Application.isPlaying) Repaint();
        }
    }
}

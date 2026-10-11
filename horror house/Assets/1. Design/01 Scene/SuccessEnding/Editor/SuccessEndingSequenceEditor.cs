using UnityEditor;
using UnityEngine;

namespace NightDuty.SuccessEnding.Editor
{
    // 재생 중 Inspector에 「원하는 단계부터 보기」 버튼을 보여 줍니다. 에디터 전용(빌드에 들어가지 않음), 씬 값은 바꾸지 않습니다.
    [CustomEditor(typeof(SuccessEndingSequence))]
    public sealed class SuccessEndingSequenceEditor : UnityEditor.Editor
    {
        private static readonly (string label, SuccessEndingSequence.EndingJumpPoint point)[] Points =
        {
            ("처음부터 (무음 → 전화벨)", SuccessEndingSequence.EndingJumpPoint.Start),
            ("통화 (「현장관리팀 파견 담당자입니다.」)", SuccessEndingSequence.EndingJumpPoint.Call),
            ("수칙 쏟아짐 (「뒤를 돌아보지 마십시오.」…)", SuccessEndingSequence.EndingJumpPoint.BuildUp),
            ("「……규칙 말씀이십니까?」", SuccessEndingSequence.EndingJumpPoint.After),
            ("「고생하셨습니다.」", SuccessEndingSequence.EndingJumpPoint.Clean),
            ("꼬리 (「철거는 예정대로…」 → 퇴근 확인 → 발소리)", SuccessEndingSequence.EndingJumpPoint.Tail),
        };

        public override void OnInspectorGUI()
        {
            var seq = (SuccessEndingSequence)target;
            EditorGUILayout.Space(2);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("원하는 단계부터 보기", EditorStyles.boldLabel);
                if (!Application.isPlaying)
                {
                    EditorGUILayout.HelpBox("재생(▶)을 시작하면 버튼이 켜집니다.", MessageType.Info);
                }
                else
                {
                    EditorGUILayout.LabelField("지금 단계: " + seq.Stage);
                    foreach (var (label, point) in Points)
                        if (GUILayout.Button(label)) seq.PlayFrom(point);
                }
            }
            EditorGUILayout.Space(4);
            DrawDefaultInspector();
            if (Application.isPlaying) Repaint();
        }
    }
}

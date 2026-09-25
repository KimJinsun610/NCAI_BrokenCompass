using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace NightDuty.EditorTools
{
    /// <summary>
    /// 씬에 깔아 둔 판정 표식을 사람이 눈으로 검수하기 위한 목록 도구.
    /// <list type="bullet">
    /// <item><b>판정 대상 목록</b> — 콘솔 줄을 클릭하면 그 오브젝트로 Hierarchy가 이동한다.</item>
    /// <item><b>다음 판정 대상 보기</b> — 누를 때마다 하나씩 선택하고 Scene 뷰를 그 앞으로 옮긴다.</item>
    /// <item><b>공간 구역 요약</b> — 공간·점검 자리·구역의 좌표를 한 번에 찍는다.</item>
    /// </list>
    /// </summary>
    public static class JudgeSceneReport
    {
        private const string Root = "NightDuty/검수/";
        private static int _cursor;

        [MenuItem(Root + "판정 대상 목록")]
        private static void ListTargets()
        {
            List<JudgeTarget> targets = Targets();
            if (targets.Count == 0)
            {
                Debug.LogWarning("[검수] 이 씬에 판정 대상이 없습니다.");
                return;
            }

            Debug.Log("[검수] 판정 대상 " + targets.Count + "개 — 아래 줄을 클릭하면 그 오브젝트로 이동합니다.");
            for (int i = 0; i < targets.Count; i++)
            {
                JudgeTarget t = targets[i];
                Vector3 p = t.transform.position;
                Debug.Log(
                    string.Format("[검수] {0,2}. {1}  ←  {2}  ({3:F0}, {4:F0})",
                        i + 1, t.PrimaryId, t.gameObject.name, p.x, p.z),
                    t.gameObject);
            }
        }

        [MenuItem(Root + "다음 판정 대상 보기 _F3")]
        private static void Next()
        {
            List<JudgeTarget> targets = Targets();
            if (targets.Count == 0)
            {
                Debug.LogWarning("[검수] 이 씬에 판정 대상이 없습니다.");
                return;
            }

            _cursor = (_cursor % targets.Count + targets.Count) % targets.Count;
            JudgeTarget t = targets[_cursor];
            _cursor++;

            Selection.activeGameObject = t.gameObject;
            EditorGUIUtility.PingObject(t.gameObject);
            if (SceneView.lastActiveSceneView != null)
            {
                SceneView.lastActiveSceneView.Frame(new Bounds(t.transform.position, Vector3.one * 4f), false);
            }

            Debug.Log("[검수] " + _cursor + "/" + targets.Count + "  " + t.PrimaryId + "  ←  " + t.gameObject.name, t.gameObject);
        }

        [MenuItem(Root + "공간 구역 요약")]
        private static void Summary()
        {
            SpaceZones zones = Object.FindAnyObjectByType<SpaceZones>(FindObjectsInactive.Include);
            if (zones == null)
            {
                Debug.LogWarning("[검수] 씬에 JudgeZones가 없습니다.");
                return;
            }

            SerializedObject so = new SerializedObject(zones);
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine("[검수] 공간 구역");

            SerializedProperty list = so.FindProperty("zones");
            for (int i = 0; i < list.arraySize; i++)
            {
                SerializedProperty e = list.GetArrayElementAtIndex(i);
                Bounds box = e.FindPropertyRelative("Box").boundsValue;
                Bounds inspect = e.FindPropertyRelative("InspectionBox").boundsValue;
                bool outOfScope = e.FindPropertyRelative("OutOfScope").boolValue;
                sb.AppendLine(string.Format("  {0}{1}  x[{2:F0},{3:F0}] z[{4:F0},{5:F0}]{6}",
                    outOfScope ? "제외 " : string.Empty,
                    (SpaceId)e.FindPropertyRelative("Space").enumValueIndex,
                    box.min.x, box.max.x, box.min.z, box.max.z,
                    inspect.size.sqrMagnitude > 0f
                        ? string.Format("   점검 자리 x[{0:F0},{1:F0}] z[{2:F0},{3:F0}]", inspect.min.x, inspect.max.x, inspect.min.z, inspect.max.z)
                        : string.Empty));
            }

            SerializedProperty signals = so.FindProperty("signalZones");
            for (int i = 0; i < signals.arraySize; i++)
            {
                SerializedProperty e = signals.GetArrayElementAtIndex(i);
                Bounds box = e.FindPropertyRelative("Box").boundsValue;
                sb.AppendLine(string.Format("  구역 {0}  x[{1:F0},{2:F0}] z[{3:F0},{4:F0}]",
                    e.FindPropertyRelative("Id").stringValue, box.min.x, box.max.x, box.min.z, box.max.z));
            }

            Debug.Log(sb.ToString(), zones.gameObject);
        }

        private static List<JudgeTarget> Targets()
        {
            List<JudgeTarget> found = new List<JudgeTarget>(
                Object.FindObjectsByType<JudgeTarget>(FindObjectsInactive.Include, FindObjectsSortMode.None));
            found.Sort((a, b) => string.CompareOrdinal(a.PrimaryId, b.PrimaryId));
            return found;
        }
    }
}

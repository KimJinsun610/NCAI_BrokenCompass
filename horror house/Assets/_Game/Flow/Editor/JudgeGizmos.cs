using UnityEditor;
using UnityEngine;

namespace NightDuty.EditorTools
{
    /// <summary>
    /// 판정용 표시를 Scene 뷰에 그린다 — <b>선택하지 않아도 보인다</b>.
    /// <list type="bullet">
    /// <item>공간 상자(초록) · 제외 공간(빨강) · 점검 자리(파랑) · 구역(노랑)</item>
    /// <item>판정 대상(<see cref="JudgeTarget"/>)의 ID를 오브젝트 위에 글자로</item>
    /// </list>
    /// <para>씬을 눈으로 검수할 때만 쓰는 도구다. 런타임에는 아무것도 하지 않는다.</para>
    /// <para>끄고 싶으면 Scene 뷰 우측 상단 Gizmos 버튼을 끄면 된다.</para>
    /// </summary>
    public static class JudgeGizmos
    {
        private static readonly Color SpaceColor = new Color(0.25f, 0.9f, 0.55f, 1f);
        private static readonly Color OutColor = new Color(0.95f, 0.35f, 0.3f, 1f);
        private static readonly Color InspectColor = new Color(0.2f, 0.65f, 1f, 1f);
        private static readonly Color ZoneColor = new Color(1f, 0.85f, 0.25f, 1f);
        private static readonly Color TargetColor = new Color(1f, 0.55f, 0.15f, 1f);

        [DrawGizmo(GizmoType.NonSelected | GizmoType.Selected | GizmoType.Pickable)]
        private static void DrawZones(SpaceZones zones, GizmoType type)
        {
            SerializedObject so = new SerializedObject(zones);

            SerializedProperty list = so.FindProperty("zones");
            for (int i = 0; i < list.arraySize; i++)
            {
                SerializedProperty e = list.GetArrayElementAtIndex(i);
                bool outOfScope = e.FindPropertyRelative("OutOfScope").boolValue;
                SpaceId space = (SpaceId)e.FindPropertyRelative("Space").enumValueIndex;
                Bounds box = e.FindPropertyRelative("Box").boundsValue;
                string label = outOfScope ? "제외" : Label(space);

                Frame(box, outOfScope ? OutColor : SpaceColor, label);

                Bounds inspect = e.FindPropertyRelative("InspectionBox").boundsValue;
                if (!outOfScope && inspect.size.sqrMagnitude > 0f)
                {
                    Frame(inspect, InspectColor, "점검 자리");
                }
            }

            SerializedProperty signals = so.FindProperty("signalZones");
            for (int i = 0; i < signals.arraySize; i++)
            {
                SerializedProperty e = signals.GetArrayElementAtIndex(i);
                Frame(e.FindPropertyRelative("Box").boundsValue, ZoneColor, e.FindPropertyRelative("Id").stringValue);
            }

            Frame(so.FindProperty("building").boundsValue, new Color(1f, 1f, 1f, 0.5f), null);
        }

        [DrawGizmo(GizmoType.NonSelected | GizmoType.Selected | GizmoType.Pickable)]
        private static void DrawTarget(JudgeTarget target, GizmoType type)
        {
            Vector3 p = target.transform.position;
            Handles.color = TargetColor;
            Handles.DrawWireDisc(p, Vector3.up, 0.35f);
            Handles.DrawLine(p, p + Vector3.up * 1.2f);

            GUIStyle style = new GUIStyle(EditorStyles.miniLabel);
            style.normal.textColor = TargetColor;
            style.fontStyle = FontStyle.Bold;
            Handles.Label(p + Vector3.up * 1.35f, target.PrimaryId, style);
        }

        private static void Frame(Bounds box, Color color, string label)
        {
            if (box.size.sqrMagnitude <= 0f)
            {
                return;
            }

            Handles.color = color;
            Vector3 c = box.center;
            Vector3 x = Vector3.right * box.extents.x;
            Vector3 z = Vector3.forward * box.extents.z;
            Vector3 y = Vector3.up * box.extents.y;

            // 바닥 · 천장 사각형 + 기둥 네 개 (실내에서도 형태가 보이게 선으로만 그린다)
            foreach (int sign in new[] { -1, 1 })
            {
                Vector3 h = y * sign;
                Handles.DrawLine(c + h - x - z, c + h + x - z);
                Handles.DrawLine(c + h + x - z, c + h + x + z);
                Handles.DrawLine(c + h + x + z, c + h - x + z);
                Handles.DrawLine(c + h - x + z, c + h - x - z);
            }

            Handles.DrawLine(c - y - x - z, c + y - x - z);
            Handles.DrawLine(c - y + x - z, c + y + x - z);
            Handles.DrawLine(c - y + x + z, c + y + x + z);
            Handles.DrawLine(c - y - x + z, c + y - x + z);

            if (string.IsNullOrEmpty(label))
            {
                return;
            }

            GUIStyle style = new GUIStyle(EditorStyles.boldLabel);
            style.normal.textColor = color;
            Handles.Label(c + y, label, style);
        }

        private static string Label(SpaceId space)
        {
            switch (space)
            {
                case SpaceId.Corridor: return "복도";
                case SpaceId.Classroom_1_1: return "교실 1-1";
                case SpaceId.Classroom_1_3: return "교실 1-3";
                case SpaceId.ScienceRoom: return "과학실";
                case SpaceId.Toilet: return "화장실";
                default: return space.ToString();
            }
        }
    }
}

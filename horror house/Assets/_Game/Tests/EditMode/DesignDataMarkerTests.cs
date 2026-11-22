using System.Collections.Generic;
using System.Reflection;
using InstructionDT;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NightDuty.Tests
{
    /// <summary>
    /// 3단계 Marker(결정 6 — 씬이 정본, DT와는 대조만): 코드가 쓰는 표식 ID가 표에 있는지, PlayScene에 놓인 표식 좌표가 표와 같은지.
    /// 씬을 옮겼으면 xlsx Marker_Table 좌표를 고치고 내보내십시오(씬을 표에 맞추지 않는다).
    /// </summary>
    public class DesignDataMarkerTests
    {
        const string PlayScenePath = "Assets/0. Main/01 Scene/PlayScene.unity";
        const float Tolerance = 0.01f;
        const uint KindZone = 530103, KindStage = 530104;

        [Test]
        public void 코드의_표식_ID는_표에_있다()
        {
            DesignData.Ensure();
            var codes = new HashSet<string>();
            foreach (var r in DT.Marker.All) codes.Add(r.Code);
            var missing = new List<string>();
            foreach (System.Type t in new[] { typeof(FinalCues), typeof(StageAnchors) })
                foreach (FieldInfo f in t.GetFields(BindingFlags.Public | BindingFlags.Static))
                {
                    if (!f.IsLiteral || f.FieldType != typeof(string)) continue;
                    string v = (string)f.GetRawConstantValue();
                    if (!codes.Contains(v)) missing.Add(t.Name + "." + f.Name + " = " + v);
                }

            Assert.IsEmpty(missing, "Marker_Table에 없는 표식: " + string.Join(", ", missing));
        }

        [Test]
        public void PlayScene_표식_좌표는_표와_같다()
        {
            DesignData.Ensure();
            Scene scene = SceneManager.GetSceneByPath(PlayScenePath);
            bool opened = false;
            if (!scene.isLoaded)
            {
                scene = EditorSceneManager.OpenScene(PlayScenePath, OpenSceneMode.Additive);
                opened = true;
            }

            try
            {
                var points = new Dictionary<string, Vector3>();
                System.Type anchorType = System.Type.GetType("StageAnchor, Assembly-CSharp");   // Flow는 시험 어셈블리가 참조하지 않는다
                foreach (Component c in Object.FindObjectsByType(anchorType, FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (c.gameObject.scene == scene) points["A|" + anchorType.GetProperty("AnchorId").GetValue(c)] = c.transform.position;
                foreach (JudgeTarget jt in Object.FindObjectsByType<JudgeTarget>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (jt.gameObject.scene == scene)
                        foreach (string id in jt.Ids) points["J|" + id] = jt.transform.position;

                System.Type zonesType = System.Type.GetType("SpaceZones, Assembly-CSharp");
                Component zones = null;
                foreach (Component c in Object.FindObjectsByType(zonesType, FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (c.gameObject.scene == scene) zones = c;
                Assert.IsNotNull(zones, "SpaceZones");

                var bad = new List<string>();
                int checkedCount = 0;
                foreach (MarkerRow r in DT.Marker.All)
                {
                    if (!r.In_Scene || r.IsBlank("Pos_X")) continue;
                    var want = new Vector3(r.Pos_X, r.Pos_Y, r.Pos_Z);
                    checkedCount++;
                    if (r.Kind_ID == KindZone)
                    {
                        Bounds box;
                        if (!Zone(zones, r.Code, out box)) { bad.Add(r.Code + " 구역 없음"); continue; }
                        var size = new Vector3(r.Size_X, r.Size_Y, r.Size_Z);
                        if ((box.center - want).magnitude > Tolerance || (box.size - size).magnitude > Tolerance)
                            bad.Add(r.Code + " 구역 씬 " + box.center.ToString("F3") + "/" + box.size.ToString("F3") + " ≠ 표 " + want.ToString("F3") + "/" + size.ToString("F3"));
                        continue;
                    }

                    Vector3 at;
                    string key = (r.Kind_ID == KindStage ? "A|" : "J|") + r.Code;
                    if (!points.TryGetValue(key, out at)) { bad.Add(r.Code + " 씬에 없음"); continue; }
                    if ((at - want).magnitude > Tolerance) bad.Add(r.Code + " 씬 " + at.ToString("F3") + " ≠ 표 " + want.ToString("F3"));
                }

                Assert.Greater(checkedCount, 30);
                Assert.IsEmpty(bad, "씬과 Marker_Table이 다름(씬이 정본 — xlsx를 고치십시오):\n" + string.Join("\n", bad));
            }
            finally
            {
                if (opened) EditorSceneManager.CloseScene(scene, true);
            }
        }

        /// <summary>SpaceZones 신호 구역(「building」은 건물 범위).</summary>
        static bool Zone(Component zones, string id, out Bounds box)
        {
            if (id == "building")
            {
                box = new UnityEditor.SerializedObject(zones).FindProperty("building").boundsValue;
                return true;
            }

            object[] args = { id, null };
            bool ok = (bool)zones.GetType().GetMethod("TryGetSignalZone").Invoke(zones, args);
            box = ok ? (Bounds)args[1] : default(Bounds);
            return ok;
        }
    }
}

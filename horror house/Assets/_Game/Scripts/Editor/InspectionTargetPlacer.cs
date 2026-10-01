using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NightDuty.EditorTools
{
    /// <summary>
    /// 점검 항목 17개의 씬 대상을 놓는다(2026-10-01, 사용자 승인 씬 작업). 메뉴 <c>NightDuty ▸ 점검 대상 배치</c>.
    /// <para>
    /// 항목마다 씬의 소품 아래에 자식 <c>Inspect &lt;ID&gt;</c>를 만들고 <see cref="JudgeTarget"/>(ID <c>inspect.&lt;ID&gt;</c>)와
    /// 소품 렌더러를 감싸는 <b>트리거가 아닌</b> 상자 콜라이더를 붙인다 — 응시 원뿔(10°)은 판정 콜라이더가 있는 대상만 보고,
    /// 가림 검사의 첫 충돌이 이 상자여야 하므로 소품보다 조금(2cm) 크게 잡는다. 소품 자체의 컴포넌트는 건드리지 않는다.
    /// 여러 번 실행해도 같은 자식을 다시 맞출 뿐 중복을 만들지 않는다.
    /// </para>
    /// <para>
    /// 대응표는 소품 이름이 아니라 <b>씬 경로</b>다. 소품을 옮기거나 이름을 바꾸면 여기를 고친다.
    /// 공간은 SpaceZones·AmbienceConfig의 실측 상자로 골랐다(교실 = Classroom02, 화장실 = Toilet02, 도서관 = 1층 서쪽 방).
    /// </para>
    /// </summary>
    public static class InspectionTargetPlacer
    {
        private const string ChildPrefix = "Inspect ";
        private const float Inflate = 0.04f;

        /// <summary>항목 ID → 씬 경로.</summary>
        public static readonly KeyValuePair<string, string>[] Map =
        {
            Pair("H-1", "Interior/Corridors/FireExtinguisherHang (2)"),
            Pair("H-2", "Interior/Corridors/DrinkingFountain (1)"),
            Pair("H-3", "Interior/Corridors/Bell (7)"),
            Pair("H-4", "Interior/Corridors/LockerA (2)"),
            Pair("C-1", "Interior/Classroom02/Plant02 (1)"),
            Pair("C-2", "Interior/Classroom02/LampDesk (1)"),
            Pair("C-3", "Interior/Classroom02/LibraryLadder (1)"),
            Pair("S-1", "Interior/science classroom/Anatomical Human Torso"),
            Pair("S-2", "Interior/science classroom/Vintage Microscope"),
            Pair("S-3", "Interior/science classroom/Laboratory_Sink"),
            Pair("T-1", "Interior/Toilet02/ToiletA (3)"),
            Pair("T-2", "Interior/Toilet02/ToiletCabin_static (2)/ToiletCabinDoor"),
            Pair("T-3", "Interior/Toilet02/Toilet_MirrorB (1)"),
            Pair("L-1", "Interior/Library/LibraryChair (15)"),
            Pair("L-2", "Interior/Library/JalousieE (6)"),
            Pair("L-3", "Interior/Library/CardboardBoxB_open_booksC (2)"),
            Pair("K-1", "Interior/janitor's room/Old CRT Monitor")
        };

        private static KeyValuePair<string, string> Pair(string id, string path)
        {
            return new KeyValuePair<string, string>(id, path);
        }

        /// <summary>메뉴.</summary>
        [MenuItem("NightDuty/점검 대상 배치")]
        public static void PlaceFromMenu()
        {
            Debug.Log(PlaceAll(true));
        }

        /// <summary>대상 17개를 놓는다(열린 활성 씬). <paramref name="save"/>면 씬을 저장한다. 결과 보고를 돌려준다.</summary>
        public static string PlaceAll(bool save)
        {
            StringBuilder report = new StringBuilder();
            int placed = 0;
            for (int i = 0; i < Map.Length; i++)
            {
                string id = Map[i].Key;
                string path = Map[i].Value;
                GameObject host = GameObject.Find("/" + path);
                if (host == null)
                {
                    report.AppendLine("없음: " + id + " ← " + path);
                    continue;
                }

                if (InspectionCatalog.Find(id) == null)
                {
                    report.AppendLine("카탈로그에 없는 ID: " + id);
                    continue;
                }

                Bounds bounds;
                if (!TryBounds(host, out bounds))
                {
                    report.AppendLine("렌더러 없음: " + id + " ← " + path);
                    continue;
                }

                GameObject child = FindOrCreateChild(host, ChildPrefix + id);
                child.layer = host.layer;
                child.isStatic = false;
                child.transform.position = bounds.center;
                child.transform.rotation = Quaternion.identity;

                BoxCollider box = child.GetComponent<BoxCollider>();
                if (box == null) box = Undo.AddComponent<BoxCollider>(child);
                Vector3 scale = child.transform.lossyScale;
                Vector3 size = bounds.size + Vector3.one * Inflate;
                box.isTrigger = false;
                box.center = Vector3.zero;
                box.size = new Vector3(Div(size.x, scale.x), Div(size.y, scale.y), Div(size.z, scale.z));

                JudgeTarget target = child.GetComponent<JudgeTarget>();
                if (target == null) target = Undo.AddComponent<JudgeTarget>(child);
                SerializedObject so = new SerializedObject(target);
                SerializedProperty ids = so.FindProperty("_ids");
                ids.arraySize = 1;
                ids.GetArrayElementAtIndex(0).stringValue = InspectionCatalog.TargetPrefix + id;
                so.ApplyModifiedProperties();

                EditorUtility.SetDirty(child);
                placed++;
                report.AppendLine(id + " → " + path + " @" + bounds.center.ToString("F2") + " 크기 " + bounds.size.ToString("F2"));
            }

            UnityEngine.SceneManagement.Scene scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            if (save) EditorSceneManager.SaveScene(scene);

            return "[InspectionTargetPlacer] " + placed + "/" + Map.Length + "개 배치" + (save ? " · 씬 저장" : string.Empty) + "\n" + report;
        }

        private static float Div(float a, float b)
        {
            return Mathf.Abs(b) < 1e-5f ? a : a / Mathf.Abs(b);
        }

        private static GameObject FindOrCreateChild(GameObject host, string name)
        {
            Transform existing = host.transform.Find(name);
            if (existing != null) return existing.gameObject;

            GameObject child = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(child, "점검 대상 배치");
            child.transform.SetParent(host.transform, false);
            return child;
        }

        /// <summary>소품 렌더러를 모두 감싸는 월드 상자(이미 만든 Inspect 자식은 뺀다).</summary>
        private static bool TryBounds(GameObject host, out Bounds bounds)
        {
            bounds = new Bounds();
            bool any = false;
            Renderer[] renderers = host.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r == null || r.name.StartsWith(ChildPrefix) || r is ParticleSystemRenderer) continue;
                if (r.GetComponentInParent<LODGroup>() != null && !IsLod0(r)) continue;
                if (!any)
                {
                    bounds = r.bounds;
                    any = true;
                }
                else
                {
                    bounds.Encapsulate(r.bounds);
                }
            }

            return any;
        }

        private static bool IsLod0(Renderer r)
        {
            LODGroup group = r.GetComponentInParent<LODGroup>();
            LOD[] lods = group.GetLODs();
            if (lods.Length == 0) return true;
            Renderer[] first = lods[0].renderers;
            for (int i = 0; i < first.Length; i++)
            {
                if (first[i] == r) return true;
            }

            return false;
        }
    }
}

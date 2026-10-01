using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NightDuty.EditorTools
{
    /// <summary>
    /// 새 수칙 판정의 씬 기준점을 놓는다(2026-10-01, 사용자 승인 씬 작업). 메뉴 <c>NightDuty ▸ 새 수칙 기준점 배치</c>.
    /// <para>
    /// 소품 아래에 자식 <c>Rule &lt;ID&gt;</c>를 만들고 <see cref="JudgeTarget"/>(ID = <see cref="FinalCues"/>의 기준점 이름)만 붙인다.
    /// <b>콜라이더는 붙이지 않는다</b> — 근접(H1·L1·L4)·통로 판정(S1)은 위치만 쓰고, 비춤(S3)은 원뿔과 가림 검사를 따로 하므로
    /// 응시 원뿔이 이 기준점을 잡아 점검 대상 응시를 가로채면 안 된다. 여러 번 실행해도 같은 자식을 다시 맞출 뿐이다.
    /// </para>
    /// <para>
    /// 조우 소품(노란 얼굴·창밖 남자·천장 다리·복도 미분류 물체)의 기준점은 여기서 놓지 않는다 — 조우 연출(7단계)이 소품과 함께 만든다.
    /// </para>
    /// </summary>
    public static class RuleAnchorPlacer
    {
        private const string ChildPrefix = "Rule ";

        private struct Entry
        {
            public string Id;
            public string Path;
            public bool UseBounds;
            public Vector3 Fixed;
        }

        private static Entry Bounds(string id, string path)
        {
            return new Entry { Id = id, Path = path, UseBounds = true };
        }

        private static Entry At(string id, string path, Vector3 world)
        {
            return new Entry { Id = id, Path = path, UseBounds = false, Fixed = world };
        }

        private static readonly Entry[] Map =
        {
            // 과학실 긴 축(X 42~54)의 가운데. 들어온 쪽과 나간 쪽을 이 X로 가른다.
            At(FinalCues.S1Center, "Interior/science classroom", new Vector3(48f, 1.5f, 42f)),
            // 과학실 바닥에 선 인체 모형. 선반 위 모형(점검 S-1)과 다른 것이다. 둘레 1m가 테이프 구역(science.tape).
            Bounds(FinalCues.ModelTarget, "Interior/science classroom/Anatomical Human Torso (1)"),
            // 1층 도서관의 기울어진(쓰러진) 책장.
            Bounds(FinalCues.L1Shelf, "Interior/Library/BookShelvingDouble (10)"),
            // 도서관 가운데 통로의 상자.
            Bounds(FinalCues.L4Box, "Interior/Library/CardboardBoxA (24)")
        };

        /// <summary>메뉴.</summary>
        [MenuItem("NightDuty/새 수칙 기준점 배치")]
        public static void PlaceFromMenu()
        {
            Debug.Log(PlaceAll(true));
        }

        /// <summary>기준점을 놓는다(열린 활성 씬). <paramref name="save"/>면 씬을 저장한다. 결과 보고를 돌려준다.</summary>
        public static string PlaceAll(bool save)
        {
            StringBuilder report = new StringBuilder();
            int placed = 0;
            for (int i = 0; i < Map.Length; i++)
            {
                Entry e = Map[i];
                GameObject host = GameObject.Find("/" + e.Path);
                if (host == null)
                {
                    report.AppendLine("없음: " + e.Id + " ← " + e.Path);
                    continue;
                }

                Vector3 pos = e.Fixed;
                if (e.UseBounds && !TryCenter(host, out pos))
                {
                    report.AppendLine("렌더러 없음: " + e.Id + " ← " + e.Path);
                    continue;
                }

                string name = ChildPrefix + e.Id;
                Transform existing = host.transform.Find(name);
                GameObject child;
                if (existing != null) child = existing.gameObject;
                else
                {
                    child = new GameObject(name);
                    Undo.RegisterCreatedObjectUndo(child, "새 수칙 기준점 배치");
                    child.transform.SetParent(host.transform, false);
                }

                child.isStatic = false;
                child.transform.position = pos;
                child.transform.rotation = Quaternion.identity;

                JudgeTarget target = child.GetComponent<JudgeTarget>();
                if (target == null) target = Undo.AddComponent<JudgeTarget>(child);
                SerializedObject so = new SerializedObject(target);
                SerializedProperty ids = so.FindProperty("_ids");
                ids.arraySize = 1;
                ids.GetArrayElementAtIndex(0).stringValue = e.Id;
                so.ApplyModifiedProperties();

                EditorUtility.SetDirty(child);
                placed++;
                report.AppendLine(e.Id + " → " + e.Path + " @" + pos.ToString("F2"));
            }

            UnityEngine.SceneManagement.Scene scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            if (save) EditorSceneManager.SaveScene(scene);

            return "[RuleAnchorPlacer] " + placed + "/" + Map.Length + "개 배치" + (save ? " · 씬 저장" : string.Empty) + "\n" + report;
        }

        private static bool TryCenter(GameObject host, out Vector3 center)
        {
            center = Vector3.zero;
            bool any = false;
            Bounds b = new Bounds();
            Renderer[] renderers = host.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r == null || r is ParticleSystemRenderer) continue;
                if (!any)
                {
                    b = r.bounds;
                    any = true;
                }
                else
                {
                    b.Encapsulate(r.bounds);
                }
            }

            if (any) center = b.center;
            return any;
        }
    }
}

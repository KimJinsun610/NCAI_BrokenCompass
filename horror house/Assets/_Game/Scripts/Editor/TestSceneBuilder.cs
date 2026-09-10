using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using NightDuty;

namespace NightDuty.Editor
{
    /// <summary>
    /// 조도 축을 눈으로 확인하기 위한 가벼운 테스트 씬을 메뉴 한 번으로 만듭니다.
    /// </summary>
    /// <remarks>
    /// 왜 씬을 손으로 만들지 않고 코드로 만드는가 —
    /// 1) 벤더 데모씬(DemoScene_LeeTest)은 19MB라 열기 느리고, 충돌 시 병합이 불가능하며,
    ///    조명이 전부 Area Light(베이크 전용)라 실시간 조도 변화가 보이지 않습니다.
    /// 2) 손으로 만든 씬은 팀원마다 조금씩 달라집니다. 코드로 만들면 누가 실행해도 같은 씬이 나옵니다.
    /// 3) 씬 파일 대신 이 스크립트만 커밋하면 되므로 저장소가 가벼워지고 씬 락 충돌이 없습니다.
    ///
    /// 만들어지는 것 — 32m 복도 한 구간(바닥·천장·양쪽 벽), 천장 형광등 위치에 실시간 Point Light 8개,
    /// 그리고 DebugAxisDriver + AxisTestLightRig가 붙은 오브젝트 하나.
    /// 환경광을 거의 0으로 두었기 때문에 조도 축이 Band4에 도달하면 정말로 캄캄해집니다.
    /// 이 게임에서는 그것이 의도입니다 — 베이크된 간접광이 남아 있으면 「전부 소등」의 공포가 죽습니다.
    /// </remarks>
    public static class TestSceneBuilder
    {
        // ── 복도 치수 ────────────────────────────────────────────────
        // 조명 8개 × 4m 간격 = 28m. 양 끝에 2m씩 여유를 두어 32m.
        private const int   LightCount   = 8;
        private const float Spacing      = 4f;
        private const float CorridorLen  = 32f;
        private const float CorridorWide = 4f;
        private const float CeilingH     = 3.2f;
        private const float LightH       = 2.9f;   // 천장에서 30cm 내려온 형광등 높이

        private const string ScenePath = "Assets/_Game/Scenes/_Test_AxisRig.unity";

        [MenuItem("NightDuty/테스트 씬 생성 (조도 확인용)", false, 300)]
        private static void BuildTestScene()
        {
            // 현재 씬에 저장하지 않은 변경이 있으면 먼저 물어봅니다.
            // DemoScene_LeeTest는 19MB라 실수로 저장되면 곤란하므로 반드시 사용자에게 맡깁니다.
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            if (System.IO.File.Exists(ScenePath))
            {
                bool ok = EditorUtility.DisplayDialog(
                    "테스트 씬이 이미 있습니다",
                    ScenePath + "\n\n덮어쓸까요? 기존 씬의 내용은 사라집니다.",
                    "덮어쓰기", "취소");
                if (!ok) return;
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            BuildEnvironment();
            Transform lightRoot = BuildLights();
            BuildRig(lightRoot);
            BuildCamera();

            if (!EnsureFolder("Assets/_Game", "Scenes")) return;

            bool saved = EditorSceneManager.SaveScene(scene, ScenePath);
            if (!saved)
            {
                Debug.LogWarning("[TestSceneBuilder] 씬 저장에 실패했습니다: " + ScenePath);
                return;
            }

            AssetDatabase.Refresh();
            Debug.Log(
                "[TestSceneBuilder] 테스트 씬을 만들었습니다 — " + ScenePath +
                "\n  1. Hierarchy에서 __AxisTest 를 선택하십시오." +
                "\n  2. Debug Axis Driver 의 '조도' 슬라이더를 0에서 100까지 끌어 보십시오." +
                "\n  3. 등 개수와 색온도가 마음에 들지 않으면 BandTable.asset 을 고치십시오." +
                " Play 중에 고쳐도 값이 유지됩니다.");
        }

        // ── 환경 ─────────────────────────────────────────────────────
        private static void BuildEnvironment()
        {
            // 환경광을 거의 0으로. 조명이 전부 꺼지면 정말 캄캄해야 합니다.
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.02f, 0.02f, 0.03f);
            RenderSettings.fog = false;

            GameObject root = new GameObject("Corridor");

            float halfW = CorridorWide * 0.5f;
            MakeBox(root.transform, "Floor",     new Vector3(0f, -0.05f, 0f),          new Vector3(CorridorWide, 0.1f, CorridorLen));
            MakeBox(root.transform, "Ceiling",   new Vector3(0f, CeilingH, 0f),        new Vector3(CorridorWide, 0.1f, CorridorLen));
            MakeBox(root.transform, "Wall_L",    new Vector3(-halfW, CeilingH * 0.5f, 0f), new Vector3(0.1f, CeilingH, CorridorLen));
            MakeBox(root.transform, "Wall_R",    new Vector3( halfW, CeilingH * 0.5f, 0f), new Vector3(0.1f, CeilingH, CorridorLen));
            MakeBox(root.transform, "Wall_Far",  new Vector3(0f, CeilingH * 0.5f,  CorridorLen * 0.5f), new Vector3(CorridorWide, CeilingH, 0.1f));
            MakeBox(root.transform, "Wall_Near", new Vector3(0f, CeilingH * 0.5f, -CorridorLen * 0.5f), new Vector3(CorridorWide, CeilingH, 0.1f));

            Undo.RegisterCreatedObjectUndo(root, "테스트 씬 생성");
        }

        private static void MakeBox(Transform parent, string name, Vector3 pos, Vector3 size)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = size;
            go.isStatic = true;
        }

        // ── 조명 ─────────────────────────────────────────────────────
        private static Transform BuildLights()
        {
            GameObject root = new GameObject("TestLights");
            Undo.RegisterCreatedObjectUndo(root, "테스트 씬 생성");

            // 8개를 복도 길이에 걸쳐 균등 배치. 첫 등이 z = -14, 마지막이 z = +14.
            float startZ = -(LightCount - 1) * Spacing * 0.5f;

            for (int i = 0; i < LightCount; i++)
            {
                // 이름은 0 패딩. AxisTestLightRig가 이름 오름차순으로 정렬해 쓰기 때문에
                // TestLight_10 이 TestLight_2 보다 앞에 오면 꺼지는 순서가 뒤죽박죽이 됩니다.
                GameObject go = new GameObject("TestLight_" + i.ToString("00"));
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = new Vector3(0f, LightH, startZ + i * Spacing);

                Light light = go.AddComponent<Light>();
                light.type = LightType.Point;
                light.lightmapBakeType = LightmapBakeType.Realtime;   // 베이크면 실시간 변화가 안 보입니다
                light.shadows = LightShadows.None;                    // 아틀라스 2048에 8개는 부족합니다
                light.range = 7f;
                light.intensity = 1.4f;
                light.useColorTemperature = true;
                light.colorTemperature = 6500f;
                light.color = Color.white;
            }

            return root.transform;
        }

        // ── 리그 ─────────────────────────────────────────────────────
        private static void BuildRig(Transform lightRoot)
        {
            GameObject go = new GameObject("__AxisTest");
            Undo.RegisterCreatedObjectUndo(go, "테스트 씬 생성");

            go.AddComponent<DebugAxisDriver>();

            // AxisTestLightRig는 개인 폴더에 있어 Assembly-CSharp에 들어갑니다.
            // Editor 어셈블리는 Assembly-CSharp을 참조할 수 없으므로 이름으로 찾습니다.
            Type rigType = Type.GetType("AxisTestLightRig, Assembly-CSharp");
            if (rigType == null)
            {
                Debug.LogWarning(
                    "[TestSceneBuilder] AxisTestLightRig 를 찾지 못했습니다. " +
                    "__AxisTest 오브젝트에 직접 추가한 뒤 Light Root 에 TestLights 를 지정하십시오.");
                return;
            }

            Component rig = go.AddComponent(rigType);
            if (rig == null) return;

            // private [SerializeField] 필드는 SerializedObject로 설정합니다.
            SerializedObject so = new SerializedObject(rig);
            SetObjectRef(so, "_lightRoot", lightRoot);
            SetInt(so, "_maxLights", LightCount);
            SetEnum(so, "_space", (int)SpaceId.Corridor);

            BandTableSO table = BandTableAssetCreator.FindExisting();
            if (table != null)
            {
                SetObjectRef(so, "_bandTable", table);
            }
            else
            {
                Debug.LogWarning(
                    "[TestSceneBuilder] 조도 표 에셋이 없습니다. " +
                    "메뉴 NightDuty > 조도 표 에셋 생성 을 실행한 뒤, __AxisTest 의 Band Table 에 지정하십시오.");
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BuildCamera()
        {
            GameObject go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            go.transform.localPosition = new Vector3(0f, 1.6f, -(CorridorLen * 0.5f) + 1f);
            Camera cam = go.AddComponent<Camera>();
            cam.fieldOfView = 60f;
            cam.nearClipPlane = 0.05f;
            Undo.RegisterCreatedObjectUndo(go, "테스트 씬 생성");
        }

        // ── 유틸 ─────────────────────────────────────────────────────
        private static void SetObjectRef(SerializedObject so, string field, UnityEngine.Object value)
        {
            SerializedProperty p = so.FindProperty(field);
            if (p != null) p.objectReferenceValue = value;
        }

        private static void SetInt(SerializedObject so, string field, int value)
        {
            SerializedProperty p = so.FindProperty(field);
            if (p != null) p.intValue = value;
        }

        private static void SetEnum(SerializedObject so, string field, int value)
        {
            SerializedProperty p = so.FindProperty(field);
            if (p != null) p.enumValueIndex = value;
        }

        private static bool EnsureFolder(string parent, string child)
        {
            string full = parent + "/" + child;
            if (AssetDatabase.IsValidFolder(full)) return true;
            if (!AssetDatabase.IsValidFolder(parent))
            {
                Debug.LogWarning("[TestSceneBuilder] 상위 폴더가 없습니다: " + parent);
                return false;
            }
            AssetDatabase.CreateFolder(parent, child);
            return AssetDatabase.IsValidFolder(full);
        }
    }
}

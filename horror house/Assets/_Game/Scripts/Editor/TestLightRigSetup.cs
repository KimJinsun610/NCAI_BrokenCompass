using System;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NightDuty.Editor
{
    /// <summary>
    /// 테스트용 실시간 조명 리그를 씬에 깔아 주는 에디터 도구입니다.
    ///
    /// 왜 필요한가:
    /// 개발 베이스로 쓰는 벤더 폐교 에셋의 데모 씬은 조명이 전부 Area Light
    /// (<see cref="LightType.Rectangle"/> / <see cref="LightType.Disc"/>)로 구성되어 있습니다.
    /// Area Light는 라이트맵 베이크 전용이라 플레이 중에 껐다 켜거나 색온도를 바꿔도
    /// 화면에 전혀 반영되지 않습니다. 그런데 「야간근무」의 조도(照度) 축은
    /// "형광등을 실시간으로 껐다 켜고 색온도를 바꾼다"는 것이 메커니즘 그 자체이므로,
    /// 벤더 씬 그대로는 조도 축을 눈으로 확인할 방법이 없습니다.
    ///
    /// 그래서 이 도구가 실시간 Point Light를 일정 간격으로 자동 배치합니다.
    /// 손으로 8개를 만들어 위치를 잡고 Realtime/그림자 끄기를 매번 설정하는 반복 노동을 없애기 위한 것이며,
    /// 대상 공간이 4개(복도/화장실/교실/과학실)라 이 작업이 계속 반복됩니다.
    ///
    /// 주의: Area Light는 실시간 조명으로 전환할 수 없습니다.
    /// 메뉴 2(실시간 전환)는 Area Light를 건너뛰고 개수만 보고하며,
    /// 실시간 확인이 필요하면 이 도구로 Point Light를 새로 까는 것이 정답입니다.
    ///
    /// URP 렌더링 경로는 Forward+로 확인되어 per-object 조명 개수 제한이 없으므로
    /// 한 공간에 8개를 동시에 켜도 문제가 없습니다.
    /// </summary>
    public static class TestLightRigSetup
    {
        /// <summary>생성할 조명 개수. 복도/교실의 천장 형광등 8개 기준(조도 축 표에서 공간별 최대 등 개수).</summary>
        private const int LightCount = 8;

        /// <summary>조명 간 간격(미터). 실제 교실/복도의 형광등 열 간격에 맞춘 값이며 range 6m와 겹쳐 균일하게 깔립니다.</summary>
        private const float Spacing = 4f;

        /// <summary>부모 기준 로컬 높이(미터). 천장 형광등 높이를 가정한 값입니다.</summary>
        private const float Height = 3f;

        /// <summary>조명을 담을 자식 오브젝트 이름.</summary>
        private const string ContainerName = "TestLights";

        // ------------------------------------------------------------------
        // 메뉴 1 — 테스트 조명 생성
        // ------------------------------------------------------------------

        [MenuItem("NightDuty/테스트 조명 생성 (선택 오브젝트 아래)", false, 200)]
        private static void CreateTestLights()
        {
            GameObject root = Selection.activeGameObject;
            if (root == null)
            {
                EditorUtility.DisplayDialog(
                    "테스트 조명 생성",
                    "선택된 오브젝트가 없습니다.\n\n조명을 깔 기준이 될 GameObject(예: 복도 루트)를 하이어라키에서 먼저 선택한 뒤 다시 실행해 주세요.",
                    "확인");
                return;
            }

            try
            {
                // 컨테이너가 이미 있으면 재사용합니다(중복 실행 시 계층이 지저분해지지 않게).
                Transform containerTr = root.transform.Find(ContainerName);
                GameObject container;
                if (containerTr != null)
                {
                    container = containerTr.gameObject;
                }
                else
                {
                    container = new GameObject(ContainerName);
                    Undo.RegisterCreatedObjectUndo(container, "테스트 조명 컨테이너 생성");
                    Undo.SetTransformParent(container.transform, root.transform, "테스트 조명 컨테이너 부모 설정");
                    container.transform.localPosition = Vector3.zero;
                    container.transform.localRotation = Quaternion.identity;
                    container.transform.localScale = Vector3.one;
                }

                int created = 0;
                for (int i = 0; i < LightCount; i++)
                {
                    // 이름은 0 패딩으로 만듭니다.
                    // 조명을 이름 오름차순으로 정렬해 순서대로 껐다 켜기 때문에,
                    // 패딩이 없으면 "TestLight_10"이 "TestLight_2"보다 앞에 와서 꺼지는 순서가 뒤죽박죽이 됩니다.
                    string lightName = "TestLight_" + i.ToString("00");

                    Transform existing = container.transform.Find(lightName);
                    GameObject go;
                    if (existing != null)
                    {
                        go = existing.gameObject;
                    }
                    else
                    {
                        go = new GameObject(lightName);
                        Undo.RegisterCreatedObjectUndo(go, "테스트 조명 생성");
                        Undo.SetTransformParent(go.transform, container.transform, "테스트 조명 부모 설정");
                        created++;
                    }

                    Undo.RecordObject(go.transform, "테스트 조명 위치 설정");
                    go.transform.localPosition = new Vector3(i * Spacing, Height, 0f);
                    go.transform.localRotation = Quaternion.identity;
                    go.transform.localScale = Vector3.one;

                    Light light = go.GetComponent<Light>();
                    if (light == null)
                    {
                        light = Undo.AddComponent<Light>(go);
                    }
                    else
                    {
                        Undo.RecordObject(light, "테스트 조명 설정");
                    }

                    light.type = LightType.Point;

                    // lightmapBakeType은 UnityEditor 네임스페이스의 에디터 전용 API입니다.
                    // 이 값을 Realtime으로 두어야 플레이 중 on/off와 색온도 변경이 화면에 반영됩니다.
                    light.lightmapBakeType = LightmapBakeType.Realtime;

                    // 그림자는 기본 끔.
                    // Additional Light Shadow 아틀라스가 2048이라 8개가 전부 그림자를 던지면 해상도가 부족해
                    // 그림자가 뭉개지거나 일부 조명이 그림자를 잃습니다. 필요할 때만 개별적으로 켜세요.
                    light.shadows = LightShadows.None;

                    light.range = 6f;
                    light.intensity = 1.2f;
                    light.useColorTemperature = true;
                    light.colorTemperature = 6500f;
                    light.color = Color.white;
                    light.enabled = true;
                }

                Selection.activeGameObject = container;
                EditorSceneManager.MarkSceneDirty(container.scene);

                Debug.Log(string.Format(
                    "[테스트 조명] '{0}' 아래에 실시간 Point Light {1}개를 배치했습니다(신규 {2}개, 간격 {3}m, 높이 {4}m, 6500K, 그림자 없음).\n" +
                    "다음 할 일: 1) '{5}' 오브젝트를 공간 중앙으로 옮기고 회전시켜 조명 열을 실제 형광등 위치에 맞추세요. " +
                    "2) 조도 축 테스트 컴포넌트를 '{5}' 에 붙여 이름 오름차순(TestLight_00~)으로 제어하세요. " +
                    "3) 기존 벤더 Area Light는 실시간으로 바뀌지 않으니 필요하면 꺼 두세요.",
                    root.name, LightCount, created, Spacing, Height, ContainerName), container);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[테스트 조명] 생성 중 문제가 발생해 중단했습니다: " + e.Message);
            }
        }

        [MenuItem("NightDuty/테스트 조명 생성 (선택 오브젝트 아래)", true, 200)]
        private static bool ValidateCreateTestLights()
        {
            return Selection.activeGameObject != null;
        }

        // ------------------------------------------------------------------
        // 메뉴 2 — 선택 아래 조명을 실시간으로 전환
        // ------------------------------------------------------------------

        [MenuItem("NightDuty/선택 아래 조명을 실시간으로 전환", false, 201)]
        private static void ConvertLightsToRealtime()
        {
            GameObject root = Selection.activeGameObject;
            if (root == null)
            {
                EditorUtility.DisplayDialog(
                    "조명 실시간 전환",
                    "선택된 오브젝트가 없습니다.\n\n전환할 조명들을 포함한 부모 GameObject를 먼저 선택해 주세요.",
                    "확인");
                return;
            }

            try
            {
                Light[] lights = root.GetComponentsInChildren<Light>(true);
                if (lights.Length == 0)
                {
                    Debug.LogWarning(string.Format("[테스트 조명] '{0}' 아래에서 Light를 찾지 못했습니다.", root.name));
                    return;
                }

                int converted = 0;
                int skippedArea = 0;

                for (int i = 0; i < lights.Length; i++)
                {
                    Light light = lights[i];
                    if (light == null)
                    {
                        continue;
                    }

                    // Area Light(Rectangle/Disc)는 라이트맵 베이크 전용이라 실시간 조명이 될 수 없습니다.
                    // 건드려 봐야 Unity가 되돌리므로 건너뛰고 개수만 보고합니다.
                    if (IsAreaLight(light.type))
                    {
                        skippedArea++;
                        continue;
                    }

                    Undo.RecordObject(light, "조명 실시간 전환");
                    light.lightmapBakeType = LightmapBakeType.Realtime;
                    light.shadows = LightShadows.None; // 그림자 아틀라스(2048) 절약
                    light.useColorTemperature = true;
                    EditorUtility.SetDirty(light);
                    converted++;
                }

                EditorSceneManager.MarkSceneDirty(root.scene);

                Debug.Log(string.Format(
                    "[테스트 조명] '{0}' 아래 조명 {1}개 중 {2}개를 실시간(그림자 없음, 색온도 사용)으로 전환했습니다.",
                    root.name, lights.Length, converted), root);

                if (skippedArea > 0)
                {
                    Debug.LogWarning(string.Format(
                        "[테스트 조명] Area Light {0}개는 실시간 전환이 불가능해 건너뛰었습니다. " +
                        "실시간 조도 확인이 필요하면 'NightDuty/테스트 조명 생성' 으로 Point Light를 새로 깔아 주세요.",
                        skippedArea), root);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[테스트 조명] 실시간 전환 중 문제가 발생해 중단했습니다: " + e.Message);
            }
        }

        [MenuItem("NightDuty/선택 아래 조명을 실시간으로 전환", true, 201)]
        private static bool ValidateConvertLightsToRealtime()
        {
            return Selection.activeGameObject != null;
        }

        // ------------------------------------------------------------------
        // 메뉴 3 — 선택 아래 조명 진단
        // ------------------------------------------------------------------

        [MenuItem("NightDuty/선택 아래 조명 진단", false, 202)]
        private static void DiagnoseLights()
        {
            GameObject root = Selection.activeGameObject;
            if (root == null)
            {
                EditorUtility.DisplayDialog(
                    "조명 진단",
                    "선택된 오브젝트가 없습니다.\n\n진단할 조명들을 포함한 부모 GameObject를 먼저 선택해 주세요.",
                    "확인");
                return;
            }

            try
            {
                Light[] lights = root.GetComponentsInChildren<Light>(true);
                if (lights.Length == 0)
                {
                    Debug.Log(string.Format("[조명 진단] '{0}' 아래에 Light가 없습니다.", root.name), root);
                    return;
                }

                int point = 0, spot = 0, directional = 0, area = 0, etc = 0;
                int realtime = 0, baked = 0, mixed = 0;
                int shadowOn = 0;

                for (int i = 0; i < lights.Length; i++)
                {
                    Light light = lights[i];
                    if (light == null)
                    {
                        continue;
                    }

                    switch (light.type)
                    {
                        case LightType.Point: point++; break;
                        case LightType.Spot: spot++; break;
                        case LightType.Directional: directional++; break;
                        default:
                            if (IsAreaLight(light.type)) { area++; }
                            else { etc++; }
                            break;
                    }

                    switch (light.lightmapBakeType)
                    {
                        case LightmapBakeType.Realtime: realtime++; break;
                        case LightmapBakeType.Baked: baked++; break;
                        case LightmapBakeType.Mixed: mixed++; break;
                    }

                    if (light.shadows != LightShadows.None)
                    {
                        shadowOn++;
                    }
                }

                StringBuilder sb = new StringBuilder();
                sb.AppendLine(string.Format("[조명 진단] 대상: '{0}'", root.name));
                sb.AppendLine(string.Format("총 조명 개수: {0}개", lights.Length));
                sb.AppendLine(string.Format("타입별 — Point {0} / Spot {1} / Directional {2} / Area {3}{4}",
                    point, spot, directional, area, etc > 0 ? " / 기타 " + etc : string.Empty));
                sb.AppendLine(string.Format("베이크 타입별 — Realtime {0} / Baked {1} / Mixed {2}", realtime, baked, mixed));
                sb.AppendLine(string.Format("그림자 켜진 조명: {0}개", shadowOn));

                if (shadowOn > 4)
                {
                    sb.AppendLine(string.Format(
                        "경고: 그림자를 던지는 조명이 {0}개입니다. Additional Light Shadow 아틀라스가 2048이라 부족할 수 있습니다.",
                        shadowOn));
                }

                if (area > 0)
                {
                    sb.AppendLine(string.Format(
                        "경고: Area Light가 {0}개 있습니다. Area Light는 베이크 전용이므로 실시간 조도 변화가 보이지 않습니다. " +
                        "조도 축 테스트에는 'NightDuty/테스트 조명 생성' 으로 실시간 Point Light를 깔아 주세요.",
                        area));
                }

                Debug.Log(sb.ToString(), root);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[조명 진단] 진단 중 문제가 발생해 중단했습니다: " + e.Message);
            }
        }

        [MenuItem("NightDuty/선택 아래 조명 진단", true, 202)]
        private static bool ValidateDiagnoseLights()
        {
            return Selection.activeGameObject != null;
        }

        // ------------------------------------------------------------------
        // 내부 헬퍼
        // ------------------------------------------------------------------

        /// <summary>해당 조명 타입이 Area Light(Rectangle/Disc)인지 판정합니다. Area Light는 실시간이 될 수 없습니다.</summary>
        private static bool IsAreaLight(LightType type)
        {
            return type == LightType.Rectangle || type == LightType.Disc;
        }
    }
}

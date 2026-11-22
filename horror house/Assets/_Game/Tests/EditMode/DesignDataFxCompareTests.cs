using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using InstructionDT;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NightDuty.Tests
{
    /// <summary>
    /// 4단계 뒤 Fx 미처리 행 대조(결정 5 — 프리팹·설명 글 행은 대조만, 정본은 프리팹·씬·코드):
    /// 손전등 빛 420901 · 맵 소리 4204xx · 대역 모델 4215xx · 설명 글 행(4207xx·4216xx·4217xx)이 가리키는 ID.
    /// 다르면 xlsx를 고친다(결정 8).
    /// </summary>
    public class DesignDataFxCompareTests
    {
        const string PlayScenePath = "Assets/0. Main/01 Scene/PlayScene.unity";
        const string FlashlightPrefab = "Assets/NOT_Lonely/HQ_AbandonedSchool/Prefabs/Flashlight_ON_FirstPerson.prefab";
        const uint Flashlight = 420901u;
        const uint TypeMapSound = 490104u, TypeStandIn = 490115u;
        const uint BandBase = 330000u;
        const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;

        static System.Type Flow(string name) => System.Type.GetType(name + ", Assembly-CSharp");   // Flow는 시험 어셈블리가 참조하지 않는다

        /// <summary>행의 Param_k_Name이 name인 값. 칸이 없거나 비면 null.</summary>
        static float? Param(FxRow r, string name)
        {
            for (int k = 1; k <= 12; k++)
            {
                if ((string)typeof(FxRow).GetField("Param_" + k + "_Name").GetValue(r) != name) continue;
                if (r.IsBlank("Param_" + k + "_Value")) return null;
                return (float)typeof(FxRow).GetField("Param_" + k + "_Value").GetValue(r);
            }

            return null;
        }

        static void Near(List<string> bad, string what, float table, float actual, float tol = 0.001f)
        {
            if (Mathf.Abs(table - actual) > tol) bad.Add(what + " 표 " + table + " ≠ " + actual);
        }

        [Test]
        public void 손전등_프리팹_빛은_표와_같다()
        {
            DesignData.Ensure();
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(FlashlightPrefab);
            Assert.IsNotNull(go, FlashlightPrefab);
            Light l = go.GetComponentInChildren<Light>(true);
            Assert.IsNotNull(l, "Light");

            var bad = new List<string>();
            Near(bad, "기본 세기", DesignData.Fx(Flashlight, "기본 세기(cd)"), l.intensity);
            Near(bad, "도달 거리", DesignData.Fx(Flashlight, "빛 도달 거리(m)"), l.range);
            Near(bad, "원뿔 전체각", DesignData.Fx(Flashlight, "빛 원뿔 전체각(°)"), l.spotAngle);
            Near(bad, "안쪽 원뿔", DesignData.Fx(Flashlight, "안쪽 원뿔 전체각(°)"), l.innerSpotAngle);
            Color c = DesignData.FxColor(Flashlight, "빛 색");
            Near(bad, "색 R", c.r, l.color.r);
            Near(bad, "색 G", c.g, l.color.g);
            Near(bad, "색 B", c.b, l.color.b);
            Near(bad, "라이트 위치 Z", DesignData.Fx(Flashlight, "라이트 위치 Z(손전등 기준 m)"), l.transform.localPosition.z);
            if (l.cookie == null || !DesignData.FxRow(Flashlight).Asset_Name.Contains(l.cookie.name)) bad.Add("쿠키 " + (l.cookie ? l.cookie.name : "없음"));
            Assert.IsEmpty(bad, "손전등 프리팹과 Fx 420901이 다름(프리팹이 정본 — xlsx를 고치십시오):\n" + string.Join("\n", bad));
        }

        [Test]
        public void 손전등_씬_위치와_소리는_표와_같다()
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
                var bad = new List<string>();
                Transform model = null;
                foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (t.gameObject.scene == scene && t.name == "Flashlight_ON_FirstPerson") model = t;
                Assert.IsNotNull(model, "PlayScene의 Flashlight_ON_FirstPerson");
                Near(bad, "모델 위치 Z", DesignData.Fx(Flashlight, "손전등 모델 위치 Z(카메라 기준 m)"), model.localPosition.z);

                System.Type soundType = Flow("FlashlightSound");
                int sounds = 0;
                foreach (Component s in Object.FindObjectsByType(soundType, FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (s.gameObject.scene != scene) continue;
                    sounds++;
                    Near(bad, "켜기·끄기 소리 볼륨(" + s.name + ")", DesignData.Fx(Flashlight, "켜기·끄기 소리 볼륨"), (float)soundType.GetField("volume", Any).GetValue(s));
                }

                Assert.Greater(sounds, 0, "PlayScene의 FlashlightSound");
                Assert.IsEmpty(bad, "PlayScene과 Fx 420901이 다름(씬이 정본 — xlsx를 고치십시오):\n" + string.Join("\n", bad));
            }
            finally
            {
                if (opened) EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void 맵_소리_행은_WorldSounds의_청각_항목과_같다()
        {
            DesignData.Ensure();
            System.Type ws = Flow("WorldSounds");
            var code = new SortedSet<string>();
            foreach (object item in (System.Array)ws.GetField("Items", Any).GetValue(null))
            {
                System.Type it = item.GetType();
                if ((FearAxis)it.GetField("Axis").GetValue(item) != FearAxis.Auditory) continue;
                code.Add(it.GetField("Key").GetValue(item) + "@" + it.GetField("Level").GetValue(item));
            }

            code.Add(ws.GetField("ClassExitKey", Any).GetValue(null) + "@2");   // 교실 나가기 소리(구간 2) — Items 밖

            var table = new SortedSet<string>();
            foreach (FxRow r in DT.Fx.All)
                if (r.Type_ID == TypeMapSound) table.Add(r.Asset_Name + "@" + (r.Band_ID - BandBase));   // Band_Table 330000 = 구간 0

            Assert.Greater(table.Count, 10);
            CollectionAssert.AreEquivalent(code, table, "맵 소리(코드 WorldSounds가 정본 — xlsx를 고치십시오)");
        }

        [Test]
        public void 대역_행은_StandInFactory와_같다()
        {
            DesignData.Ensure();
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;   // StandInFactory.Part가 런타임용 Destroy를 부른다(에디터에서 오류 로그만 남음)
            System.Type f = Flow("StandInFactory");
            MethodInfo gaze = f.GetMethod("NeedsGazeCollider", Any);
            MethodInfo build = f.GetMethod("Build", Any);
            var bad = new List<string>();
            int rows = 0, heights = 0;
            foreach (FxRow r in DT.Fx.All)
            {
                if (r.Type_ID != TypeStandIn) continue;
                rows++;
                float? g = Param(r, "응시 콜라이더(1=있음)");
                bool want = g.HasValue && g.Value == 1f;
                if ((bool)gaze.Invoke(null, new object[] { r.Name }) != want) bad.Add(r.Name + " 응시 콜라이더 표 " + want);

                float? h = Param(r, "기본형 키(m)");
                if (!h.HasValue) continue;
                var go = (GameObject)build.Invoke(null, new object[] { r.Name });
                try
                {
                    float top = float.MinValue;
                    foreach (Renderer rend in go.GetComponentsInChildren<Renderer>(true)) top = Mathf.Max(top, rend.bounds.max.y - go.transform.position.y);
                    heights++;
                    Near(bad, r.Name + " 기본형 키", h.Value, top, 0.01f);
                }
                finally
                {
                    Object.DestroyImmediate(go);
                }
            }

            Assert.Greater(rows, 20);
            Assert.Greater(heights, 10);
            Assert.IsEmpty(bad, "대역 행과 StandInFactory가 다름(코드가 정본 — xlsx를 고치십시오):\n" + string.Join("\n", bad));
        }

        [Test]
        public void 설명_글_행이_가리키는_대역과_에셋은_있다()
        {
            DesignData.Ensure();
            var standIns = new HashSet<string>();
            foreach (FxRow r in DT.Fx.All) if (r.Type_ID == TypeStandIn) standIns.Add(r.Name);

            var bad = new List<string>();
            var token = new Regex(@"mob\.[a-z]+(\.[a-z]+)?");
            int texts = 0;
            foreach (FxRow r in DT.Fx.All)
            {
                if (r.ID / 100 != 4207 && r.ID / 100 != 4216 && r.ID / 100 != 4217) continue;
                texts++;
                foreach (Match m in token.Matches((r.Text ?? "") + " " + (r.Asset_Name ?? "")))
                    if (!standIns.Contains(m.Value)) bad.Add(r.ID + " " + m.Value + " — 대역 행에 없음");
            }

            if (AssetDatabase.FindAssets(DesignData.FxRow(421601u).Text).Length == 0) bad.Add("421601 " + DesignData.FxRow(421601u).Text + " 에셋 없음");
            bool success = false;
            foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes) success |= s.enabled && s.path.EndsWith("/SuccessEnding.unity");
            if (!success) bad.Add("421602 성공 엔딩 씬(SuccessEnding)이 빌드에 없음");

            Assert.Greater(texts, 30);
            Assert.IsEmpty(bad, string.Join("\n", bad));
        }
    }
}

using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace NightDuty.Tests
{
    /// <summary>
    /// 소리 연결 검사(36차, 2026-10-04 사운드 전달본). 화면 쪽 어셈블리를 참조하지 않으므로 에셋을 직렬화 필드로 읽는다.
    /// 코드가 이름으로 찾는 소리 키가 연출 소리 표에 실제 클립과 함께 있는지, 엠비언트 경로가 살아 있는지, 점검 소리 키가 실제 항목인지 본다.
    /// </summary>
    public sealed class SoundWiringTests
    {
        private const string TablePath = "Assets/_Game/Resources/DirectionSounds.asset";
        private const string AmbiencePath = "Assets/_Game/Resources/AmbienceConfig.asset";

        /// <summary>코드가 정확한 이름으로 찾는 키(CaptureDirector.Audio · NightDutySfx · TabletBridge · FinaleDirector · PlayerFootsteps · DirectionStage).</summary>
        private static readonly string[] RequiredKeys =
        {
            "*.foreshadow", "E.BoyBang.headbang", "C4.cue", "L2.cue", "S2.cue", "T1.cue", "H2.cue", "K2.cue",
            "fake.flashlight.flicker", "finale.letmein", "finale.smile", "finale.crtoff", "finale.wipe", "finale.knock",
            "capture.rise.auditory", "capture.rise.illuminance", "capture.rise.layout", "capture.rise2.auditory", "capture.breath",
            "capture.cut", "capture.cut.illuminance", "capture.swell", "capture.pre.auditory", "capture.pre.illuminance", "capture.gasp",
            "capture.face.low", "capture.face.mid.auditory", "capture.face.mid.illuminance", "capture.face.mid.layout",
            "capture.face.high.auditory", "capture.face.high.illuminance", "capture.face.high.layout",
            "capture.face.voice.auditory", "capture.face.voice.layout", "capture.tablet",
            "capture.dark.illuminance", "capture.choke.illuminance", "capture.choke.layout", "capture.tail", "capture.tail.auditory",
            "capture.precard.auditory", "capture.precard.illuminance", "capture.precard.layout",
            "capture.auditory", "capture.illuminance", "capture.layout", "capture.died", "capture.card", "capture.return", "capture.wake",
            "punish.stamp", "punish.auditory", "punish.illuminance", "punish.layout", "punish.hit", "punish.cut", "tension.confront",
            "tablet.buzz", "tablet.corrupt", "ui.ready", "ui.confirm", "inspect.near",
        };

        private static readonly string[] Surfaces = { "CLASS", "HALL", "LIBRARY", "TOILET", "LAB", "GUARD" };

        private static Dictionary<string, int> TableClips()
        {
            Object table = AssetDatabase.LoadMainAssetAtPath(TablePath);
            Assert.IsNotNull(table, TablePath + " 없음");
            SerializedObject so = new SerializedObject(table);
            SerializedProperty entries = so.FindProperty("entries");
            Dictionary<string, int> map = new Dictionary<string, int>();
            for (int i = 0; i < entries.arraySize; i++)
            {
                SerializedProperty e = entries.GetArrayElementAtIndex(i);
                string key = e.FindPropertyRelative("key").stringValue;
                int clips = e.FindPropertyRelative("clip").objectReferenceValue != null ? 1 : 0;
                SerializedProperty more = e.FindPropertyRelative("more");
                for (int k = 0; more != null && k < more.arraySize; k++)
                {
                    if (more.GetArrayElementAtIndex(k).objectReferenceValue != null) clips++;
                }

                map[key] = clips;
            }

            return map;
        }

        [Test]
        public void 소리표_코드가_찾는_키가_모두_클립과_함께_있다()
        {
            Dictionary<string, int> map = TableClips();
            List<string> missing = new List<string>();
            foreach (string key in RequiredKeys)
            {
                int n;
                if (!map.TryGetValue(key, out n) || n == 0) missing.Add(key);
            }

            foreach (string s in Surfaces)
            {
                foreach (string pace in new[] { ".walk", ".run" })
                {
                    int n;
                    if (!map.TryGetValue("step." + s + pace, out n) || n < 3) missing.Add("step." + s + pace);
                }
            }

            Assert.IsEmpty(missing, "없는 소리 키: " + string.Join(", ", missing));
        }

        [Test]
        public void 소리표_여러판_항목은_판이_둘_이상이다()
        {
            Dictionary<string, int> map = TableClips();
            Assert.GreaterOrEqual(map["*.foreshadow"], 3, "조우 예고음 _1~3");
            Assert.GreaterOrEqual(map["tablet.buzz"], 3, "PUN-02_1~3");
            Assert.GreaterOrEqual(map["inspect.near"], 2, "CloseImpact_01·02");
        }

        [Test]
        public void 클라이맥스_충격음은_가장_큰_지점이_재어져_있다()
        {
            Object table = AssetDatabase.LoadMainAssetAtPath(TablePath);
            SerializedProperty entries = new SerializedObject(table).FindProperty("entries");
            int checkedCount = 0;
            for (int i = 0; i < entries.arraySize; i++)
            {
                SerializedProperty e = entries.GetArrayElementAtIndex(i);
                string key = e.FindPropertyRelative("key").stringValue;
                if (!key.StartsWith("capture.face.") && !key.StartsWith("capture.rise")) continue;
                SerializedProperty hits = e.FindPropertyRelative("hits");
                AudioClip clip = (AudioClip)e.FindPropertyRelative("clip").objectReferenceValue;
                Assert.GreaterOrEqual(hits.arraySize, 1, key + " — hits 없음");
                float hit = hits.GetArrayElementAtIndex(0).floatValue;
                Assert.That(hit >= 0f && hit < clip.length, key + " — 가장 큰 지점 " + hit + "초가 길이 " + clip.length + "초 밖");
                if (key.StartsWith("capture.rise")) Assert.Greater(hit, 0.5f, key + " — 상승음은 차오른 뒤에 꼭대기");
                checkedCount++;
            }

            Assert.GreaterOrEqual(checkedCount, 12);
        }

        [Test]
        public void 점검_소리_키는_실제_점검_항목을_가리킨다()
        {
            foreach (string key in TableClips().Keys)
            {
                if (!key.StartsWith("inspect.") || key == "inspect.near") continue;
                string[] parts = key.Split('.');
                Assert.AreEqual(3, parts.Length, key);
                Assert.IsNotNull(InspectionCatalog.Find(parts[1]), key + " — 없는 점검 항목");
                Assert.That(parts[2] == "loop" || parts[2] == "near" || parts[2] == "near+", key);
            }
        }

        [Test]
        public void 엠비언트_경로가_모두_살아_있다()
        {
            Object cfg = AssetDatabase.LoadMainAssetAtPath(AmbiencePath);
            Assert.IsNotNull(cfg, AmbiencePath + " 없음");
            SerializedObject so = new SerializedObject(cfg);
            List<string> paths = new List<string>();
            paths.Add(so.FindProperty("baseClip").stringValue);
            SerializedProperty zones = so.FindProperty("zones");
            for (int i = 0; i < zones.arraySize; i++)
            {
                paths.Add(zones.GetArrayElementAtIndex(i).FindPropertyRelative("roomClip").stringValue);
                paths.Add(zones.GetArrayElementAtIndex(i).FindPropertyRelative("roomClipHigh").stringValue);
            }

            SerializedProperty def = so.FindProperty("defaultZone");
            paths.Add(def.FindPropertyRelative("roomClip").stringValue);
            paths.Add(def.FindPropertyRelative("roomClipHigh").stringValue);
            SerializedProperty dread = so.FindProperty("dreadClips");
            for (int i = 0; i < dread.arraySize; i++) paths.Add(dread.GetArrayElementAtIndex(i).stringValue);

            List<string> missing = new List<string>();
            foreach (string p in paths)
            {
                if (string.IsNullOrEmpty(p) || Resources.Load<AudioClip>(p) == null) missing.Add(p);
            }

            Assert.IsEmpty(missing, "없는 엠비언트: " + string.Join(", ", missing));
            Assert.AreEqual(3, so.FindProperty("highFromBand").intValue, "강한 판 = 청각 구간 3 이상(민 결정)");
        }

        [Test]
        public void CCTV_소리는_이름마다_하나뿐이다()
        {
            foreach (string n in new[] { "hum", "static", "switch" })
            {
                string[] guids = AssetDatabase.FindAssets("cctv_" + n + " t:AudioClip", new[] { "Assets/_Game/Resources/Cctv" });
                Assert.AreEqual(1, guids.Length, "cctv_" + n + " — 같은 이름이 둘이면 Resources.Load가 아무거나 고른다");
            }
        }

        [Test]
        public void 과학실_경비실_발소리는_한_걸음씩_잘려_있다()
        {
            foreach (string stem in new[] { "LAB_Walk", "GUARD_Walk" })
            {
                for (int i = 1; i <= 5; i++)
                {
                    AudioClip c = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Game/Audio/footsteps/SFX_STEP_" + stem + "_" + i.ToString("00") + ".wav");
                    Assert.IsNotNull(c, stem + " " + i);
                    Assert.Less(c.length, 0.6f, stem + " " + i + " — 한 걸음");
                }
            }

            foreach (string stem in new[] { "LAB_Run", "GUARD_Run" })
            {
                for (int i = 1; i <= 4; i++)
                {
                    AudioClip c = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Game/Audio/footsteps/SFX_STEP_" + stem + "_" + i.ToString("00") + ".wav");
                    Assert.IsNotNull(c, stem + " " + i);
                    Assert.Less(c.length, 0.6f, stem + " " + i + " — 한 걸음");
                }
            }
        }
    }
}

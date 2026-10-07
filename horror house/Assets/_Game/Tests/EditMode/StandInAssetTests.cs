using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace NightDuty.Tests
{
    /// <summary>58차 — 아트 새 몹(New_skirtboy · New_redgirl · New_businessduck · New_eggman)으로 바꾼 대역 프리팹.</summary>
    public sealed class StandInAssetTests
    {
        private const string Dir = "Assets/_Game/Resources/StandIns/";

        private static GameObject Load(string id)
        {
            GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(Dir + id + ".prefab");
            Assert.IsNotNull(go, id + " 프리팹 없음 — 야간근무/연출/몹 대역·소리 연결");
            return go;
        }

        private static bool HasComponent(GameObject go, string typeName)
        {
            foreach (Component c in go.GetComponentsInChildren<Component>(true))
            {
                if (c != null && c.GetType().Name == typeName) return true;
            }

            return false;
        }

        [TestCase("mob.boy")]
        [TestCase("mob.boy.stand")]
        [TestCase("mob.legs")]
        [TestCase("mob.girl")]
        [TestCase("mob.girl.stand")]
        [TestCase("mob.duck")]
        [TestCase("mob.windowman")]
        [TestCase("mob.tree")]
        public void 새_몹_모델을_쓴다(string id)
        {
            GameObject go = Load(id);
            Transform model = go.transform.Find("Model");
            Assert.IsNotNull(model, id);
            bool any = false;
            foreach (SkinnedMeshRenderer smr in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                string path = AssetDatabase.GetAssetPath(smr.sharedMesh);
                StringAssert.Contains("/New_", path, id + " 메시가 새 몹 폴더가 아니다");
                any = true;
            }

            Assert.IsTrue(any, id + " 메시 없음");
            Assert.IsNotNull(go.transform.Find("Aim"), id + " 조준점");
        }

        [Test]
        public void 앉은_소년에는_머리_박기_동작이_실려_있다()
        {
            GameObject boy = Load("mob.boy");
            Assert.IsTrue(HasComponent(boy, "StandInClips"), "kung에서 떼어 낸 머리 박기");
            Animator anim = boy.GetComponentInChildren<Animator>(true);
            Assert.IsTrue(anim == null || !anim.enabled, "앉은 자세를 애니메이터가 되돌리지 않게");
        }

        [Test]
        public void 화장실_소녀는_제자리_걸음으로_걷고_선_몹은_숨쉰다()
        {
            GameObject girl = Load("mob.girl");
            Assert.IsTrue(HasComponent(girl, "DirectionWalker"));
            Assert.IsNotNull(girl.GetComponentInChildren<Animator>(true).runtimeAnimatorController);
            foreach (string id in new[] { "mob.boy.stand", "mob.girl.stand", "mob.duck", "mob.windowman", "mob.tree" })
            {
                Animator a = Load(id).GetComponentInChildren<Animator>(true);
                Assert.IsNotNull(a, id);
                Assert.IsNotNull(a.runtimeAnimatorController, id + " 반복 동작");
            }
        }

        [Test]
        public void 청각_사망_컷신에_비명이_실려_있다()
        {
            // 58차(민: 「scream을 붙잡힘 장면으로 쓰자」): 김진선님 DeathCutscene_Auditory의 소년 숨쉬기 트랙을 이 컨트롤러(숨쉬기 → 비명)로 바꿔 끼운다.
            Object so = AssetDatabase.LoadMainAssetAtPath("Assets/_Game/Resources/CutsceneScream.asset");
            Assert.IsNotNull(so, "야간근무/연출/몹 대역·소리 연결");
            SerializedObject s = new SerializedObject(so);
            RuntimeAnimatorController ctrl = s.FindProperty("controller").objectReferenceValue as RuntimeAnimatorController;
            Assert.IsNotNull(ctrl);
            bool idle = false, scream = false;
            foreach (AnimationClip c in ctrl.animationClips)
            {
                if (c.name.EndsWith("idle")) idle = c.isLooping;
                if (c.name.EndsWith("scream")) scream = !c.isLooping && c.length > 1f;
            }

            Assert.IsTrue(idle, "숨쉬기는 반복");
            Assert.IsTrue(scream, "비명은 한 번");
            float peak = s.FindProperty("peakSeconds").floatValue;
            Assert.That(peak, Is.InRange(0.3f, 1.8f), "덮쳐 오는 정점");
            Assert.AreEqual("Boy idle", s.FindProperty("idleTrack").stringValue);
            Assert.IsNotNull(Resources.Load<GameObject>("DeathCutscene_Auditory"), "김진선님 청각 사망 컷신");
        }

        [TestCase("mob.duck")]
        [TestCase("mob.windowman")]
        public void 오리는_사라질_때_빠르게_걸어_나간다(string id)
        {
            // 58차(민: 「문 조우와 창문 조우에서 사라질 때 빠르게 걸어가면서」): 옛 businessduck_walking을 새 리그로 옮긴 Walk 상태 + StandInExit.
            GameObject go = Load(id);
            Assert.IsTrue(HasComponent(go, "StandInExit"), id);
            Animator a = go.GetComponentInChildren<Animator>(true);
            bool walk = false;
            foreach (AnimationClip c in a.runtimeAnimatorController.animationClips)
            {
                if (c.name.EndsWith(".walk") && c.isLooping && c.length > 0.5f) walk = true;
            }

            Assert.IsTrue(walk, id + " 걷기 클립");
            Component exit = null;
            foreach (Component c in go.GetComponents<Component>()) if (c.GetType().Name == "StandInExit") exit = c;
            float natural = new SerializedObject(exit).FindProperty("naturalSpeed").floatValue;
            Assert.That(natural, Is.InRange(0.8f, 2.5f), "원래 걸음 속도(m/s)");
        }

        [Test]
        public void 사람_나무는_에그맨이고_바라보면_화면이_물드는_효과를_잇는다()
        {
            GameObject tree = Load("mob.tree");
            Transform fx = tree.transform.Find("GazeFx");
            Assert.IsNotNull(fx, "김진선님 인간나무의 응시 화면 효과");
            Assert.IsNotNull(tree.transform.Find("LookPoint"));
            foreach (Renderer r in fx.GetComponentsInChildren<Renderer>(false)) Assert.Fail("옛 인간나무 메시가 보인다: " + r.name);
            Assert.IsTrue(HasComponent(fx.gameObject, "HorrorGazeHold"));
        }
    }
}

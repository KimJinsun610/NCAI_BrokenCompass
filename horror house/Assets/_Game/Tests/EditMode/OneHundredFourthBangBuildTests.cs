using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace NightDuty.Tests
{
    /// <summary>
    /// 104차 소년 머리 박기 빌드 누락(사용자: 「릴리스로 빌드하면 소년 머리박는게 적용이 안된다 · 어제 프리테스트 개발자 빌드에서도 안 나왔다」).
    /// 대역 <c>mob.boy</c>에는 애니메이터가 없고 <c>BoyHeadBang</c>은 클립을 <c>SampleAnimation</c>으로 굴린다 — 비레거시 클립은 빌드에서
    /// 「Non-Legacy animations cannot be sampled outside the Editor without an Animator」로 움직이지 않으므로(빌드 실측: 머리 이동 0 → 레거시 0.29m) 클립은 레거시여야 한다.
    /// </summary>
    public sealed class OneHundredFourthBangBuildTests
    {
        private const string BangPath = "Assets/_Game/Art/StandIns/mob.boy.bang.anim";
        private const string BoyPath = "Assets/_Game/Resources/StandIns/mob.boy.prefab";

        [Test]
        public void 머리_박기_클립은_레거시다()
        {
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(BangPath);
            Assert.IsNotNull(clip, BangPath + " 없음");
            Assert.IsTrue(clip.legacy, "비레거시면 빌드에서 SampleAnimation이 뼈를 움직이지 않는다");
            Assert.AreEqual(40, AnimationUtility.GetCurveBindings(clip).Length, "레거시로 바꿔도 곡선 40개가 그대로");
        }

        [Test]
        public void 소년_대역에는_애니메이터가_없어_레거시가_필요하다()
        {
            GameObject boy = AssetDatabase.LoadAssetAtPath<GameObject>(BoyPath);
            Assert.IsNotNull(boy, BoyPath + " 없음");
            Assert.IsEmpty(boy.GetComponentsInChildren<Animator>(true), "애니메이터를 달면 레거시 클립 대신 다른 방법을 검토");
        }

        [Test]
        public void 편집기에서_클립을_굴리면_머리가_움직인다()
        {
            GameObject boy = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(BoyPath));
            try
            {
                AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(BangPath);
                Transform head = null;
                foreach (Transform t in boy.GetComponentsInChildren<Transform>(true)) if (t.name == "Head") head = t;
                Assert.IsNotNull(head);
                GameObject root = head.gameObject;
                while (root.transform.parent != null && root.name != "Model") root = root.transform.parent.gameObject;
                clip.SampleAnimation(root, 0f);
                Vector3 p0 = head.position;
                clip.SampleAnimation(root, 0.23f);
                Assert.Greater((head.position - p0).magnitude, 0.1f, "첫 박기(0.23초)에 머리가 앞으로 나간다");
            }
            finally
            {
                Object.DestroyImmediate(boy);
            }
        }
    }
}

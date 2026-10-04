using System.Collections.Generic;
using NUnit.Framework;

namespace NightDuty.Tests
{
    /// <summary>씬 대상 등록부: 개수 세기·소유자·스냅숏. (옛 판정 책의 대상 참조 검사 테스트는 2026-10-03 RuleBook과 함께 지웠다.)</summary>
    public sealed class JudgeTargetRegistryTests
    {
        [SetUp]
        public void SetUp()
        {
            JudgeTargetRegistry.Clear();
            NightRun.StartNewRun();
        }

        [TearDown]
        public void TearDown()
        {
            NightRun.StartNewRun();
            JudgeTargetRegistry.Clear();
        }

        [Test]
        public void 같은ID는_올린횟수만큼_내려야_빠진다()
        {
            JudgeTargetRegistry.Register("corridor.passage", null);
            JudgeTargetRegistry.Register("corridor.passage", null);
            JudgeTargetRegistry.Unregister("corridor.passage", null);

            Assert.IsTrue(JudgeTargetRegistry.Contains("corridor.passage"));

            JudgeTargetRegistry.Unregister("corridor.passage", null);

            Assert.IsFalse(JudgeTargetRegistry.Contains("corridor.passage"));
            Assert.AreEqual(0, JudgeTargetRegistry.Count);
        }

        [Test]
        public void 먼저올린소유자가_빠져도_남은소유자로_찾는다()
        {
            UnityEngine.GameObject a = new UnityEngine.GameObject("target a");
            UnityEngine.GameObject b = new UnityEngine.GameObject("target b");
            try
            {
                JudgeTarget ta = a.AddComponent<JudgeTarget>();
                JudgeTarget tb = b.AddComponent<JudgeTarget>();
                // 에디트 모드에서는 OnEnable이 불리지 않으므로 등록부를 직접 부른다.
                JudgeTargetRegistry.Register("toilet.zone", ta);
                JudgeTargetRegistry.Register("toilet.zone", tb);

                JudgeTarget found;
                Assert.IsTrue(JudgeTargetRegistry.TryGet("toilet.zone", out found));
                Assert.AreSame(ta, found);

                JudgeTargetRegistry.Unregister("toilet.zone", ta);

                Assert.IsTrue(JudgeTargetRegistry.Contains("toilet.zone"));
                Assert.IsTrue(JudgeTargetRegistry.TryGet("toilet.zone", out found));
                Assert.AreSame(tb, found);

                JudgeTargetRegistry.Unregister("toilet.zone", tb);
                Assert.IsFalse(JudgeTargetRegistry.TryGet("toilet.zone", out found));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(a);
                UnityEngine.Object.DestroyImmediate(b);
            }
        }

        [Test]
        public void 빈ID와_없는ID는_무시한다()
        {
            JudgeTargetRegistry.Register("", null);
            JudgeTargetRegistry.Register(null, null);
            JudgeTargetRegistry.Unregister("corridor.none", null);

            Assert.AreEqual(0, JudgeTargetRegistry.Count);
        }

        [Test]
        public void 스냅숏은_이후_등록변화에_영향받지_않는다()
        {
            JudgeTargetRegistry.Register("corridor.box", null);
            HashSet<string> snap = JudgeTargetRegistry.Snapshot();
            JudgeTargetRegistry.Unregister("corridor.box", null);

            Assert.IsTrue(snap.Contains("corridor.box"));
        }

        [Test]
        public void 디버그축더하기는_양수만_반영한다()
        {
            NightRun.DebugAddAxis(FearAxis.Auditory, 30);
            NightRun.DebugAddAxis(FearAxis.Auditory, -10);

            Assert.AreEqual(30, NightRun.Axes.GetValue(FearAxis.Auditory));
        }
    }
}

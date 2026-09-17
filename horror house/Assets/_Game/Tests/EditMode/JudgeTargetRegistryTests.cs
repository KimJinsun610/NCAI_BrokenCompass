using System.Collections.Generic;
using NUnit.Framework;

namespace NightDuty.Tests
{
    /// <summary>씬 대상 등록부: 개수 세기, 밤 시작 때 참조 검사에 쓰이는지.</summary>
    public sealed class JudgeTargetRegistryTests
    {
        private TestKit _kit;
        private RuleSO _h4;

        [SetUp]
        public void SetUp()
        {
            _kit = new TestKit();
            JudgeTargetRegistry.Clear();
            _h4 = _kit.Card(c =>
            {
                c.CardId = "H4";
                c.Space = SpaceId.Corridor;
                c.IsLongTerm = true;
                c.TriggerKind = SignalKind.ClueIdentified;
                c.TriggerId = "corridor.box";
                c.Failure = new ProximityCondition("corridor.box");
                c.Success = new SignalCondition(SignalKind.PassageCompleted, "corridor.passage");
                c.SettleAt = SettleAt.AtNightEnd;
            });

            NightRun.StartNewRun();
            NightRun.RegisteredTargets = null;
            NightRun.DeckOverride = day => new List<RuleSO> { _h4 };
        }

        [TearDown]
        public void TearDown()
        {
            NightRun.DeckOverride = null;
            NightRun.RegisteredTargets = null;
            NightRun.StartNewRun();
            JudgeTargetRegistry.Clear();
            _kit.Dispose();
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
        public void 등록부가_비어있으면_참조검사를_건너뛴다()
        {
            NightRun.BeginNight(1, null);

            Assert.IsNull(NightRun.TargetsInUse);
            Assert.AreEqual(CardState.Waiting, NightRun.CurrentBook.Watchers[0].State);
        }

        [Test]
        public void 등록부에_ID가있으면_누락카드는_미판정으로_시작한다()
        {
            JudgeTargetRegistry.Register("corridor.box", null);   // corridor.passage 없음

            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Warning,
                new System.Text.RegularExpressions.Regex("H4 대상 참조 누락: corridor.passage"));
            NightRun.BeginNight(1, null);

            Assert.IsNotNull(NightRun.TargetsInUse);
            Assert.AreEqual(CardState.Undetermined, NightRun.CurrentBook.Watchers[0].State);
        }

        [Test]
        public void 등록부에_모두있으면_정상대기()
        {
            JudgeTargetRegistry.Register("corridor.box", null);
            JudgeTargetRegistry.Register("corridor.passage", null);

            NightRun.BeginNight(1, null);

            Assert.AreEqual(CardState.Waiting, NightRun.CurrentBook.Watchers[0].State);
        }

        [Test]
        public void 직접지정한_목록이_등록부보다_우선한다()
        {
            JudgeTargetRegistry.Register("corridor.box", null);
            NightRun.RegisteredTargets = new HashSet<string> { "corridor.box", "corridor.passage" };

            NightRun.BeginNight(1, null);

            Assert.AreEqual(CardState.Waiting, NightRun.CurrentBook.Watchers[0].State);
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

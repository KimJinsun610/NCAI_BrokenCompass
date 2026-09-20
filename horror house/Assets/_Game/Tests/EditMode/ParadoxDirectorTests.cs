using System.Collections.Generic;
using NUnit.Framework;

namespace NightDuty.Tests
{
    /// <summary>역설 발송: 신뢰 25부터, 구간별 하루 상한, 카드당 하루 한 번.</summary>
    public sealed class ParadoxDirectorTests
    {
        private TestKit _kit;
        private int _clock;

        [SetUp]
        public void SetUp()
        {
            _kit = new TestKit();
            _clock = 30;
            NightRun.StartNewRun();
            NightRun.RegisteredTargets = null;
        }

        [TearDown]
        public void TearDown()
        {
            NightRun.DeckOverride = null;
            NightRun.StartNewRun();
            _kit.Dispose();
        }

        /// <summary>단서를 받으면 진행 중이 되는 카드. 역설 문자가 붙어 있다.</summary>
        private RuleSO Card(string id, SpaceId space, string paradoxId)
        {
            return _kit.Card(c =>
            {
                c.CardId = id;
                c.Space = space;
                c.IsLongTerm = true;   // 한 방문에 단기 사건은 하나만 시작한다 — 여러 장을 동시에 진행시키려 장기로 둔다
                c.TriggerKind = SignalKind.ClueIdentified;
                c.TargetIds = new[] { id.ToLowerInvariant() + ".clue" };
                c.Success = new SignalCondition(SignalKind.PassageCompleted, id.ToLowerInvariant() + ".passage");
                c.Failure = new ProximityCondition("");
                c.Radius = 1.5f;
                c.FailureAxis = FearAxis.Layout;
                c.FailureDelta = 12;
                c.ParadoxId = paradoxId;
                c.ParadoxText = paradoxId + " 본문";
            });
        }

        private void Start(string id)
        {
            NightRun.Send(JudgeSignal.Target(SignalKind.ClueIdentified, id.ToLowerInvariant() + ".clue"));
        }

        [TestCase(0, 0)]
        [TestCase(24, 0)]
        [TestCase(25, 1)]
        [TestCase(49, 1)]
        [TestCase(50, 2)]
        [TestCase(74, 2)]
        [TestCase(75, 3)]
        [TestCase(89, 3)]
        [TestCase(90, 4)]
        [TestCase(100, 4)]
        public void 하루상한은_신뢰구간을_따른다(int trust, int expected)
        {
            Assert.AreEqual(expected, ParadoxDirector.DailyQuota(trust));
        }

        [Test]
        public void 신뢰25미만이면_한건도_보내지_않는다()
        {
            NightRun.DeckOverride = day => new List<RuleSO> { Card("H4", SpaceId.Corridor, "P4") };
            NightRun.BeginNight(1, () => _clock);
            NightRun.DebugAddAxis(FearAxis.Trust, 24);

            Start("H4");

            Assert.AreEqual(0, NightRun.MessagesToday.Count);
        }

        [Test]
        public void 진행중인_짝카드에_문자가_한번_간다()
        {
            ParadoxMessage got = default;
            int count = 0;
            EventBus.MessageSent += m => { got = m; count++; };

            NightRun.DeckOverride = day => new List<RuleSO> { Card("H4", SpaceId.Corridor, "P4") };
            NightRun.BeginNight(1, () => _clock);
            NightRun.DebugAddAxis(FearAxis.Trust, 30);

            Start("H4");
            Start("H4");   // 같은 카드는 하루 한 번

            Assert.AreEqual(1, count);
            Assert.AreEqual("P4", got.ParadoxId);
            Assert.AreEqual("H4", got.CardId);
            Assert.AreEqual(SpaceId.Corridor, got.Space);
            Assert.AreEqual("P4 본문", got.Text);
            Assert.AreEqual(30, got.Minute, "수신 시각은 게임 시계에서 온다");
        }

        [Test]
        public void 하루상한을_넘겨_보내지_않는다()
        {
            NightRun.DeckOverride = day => new List<RuleSO>
            {
                Card("H4", SpaceId.Corridor, "P4"),
                Card("H5", SpaceId.Corridor, "P5"),
                Card("H6", SpaceId.Corridor, "P6"),
            };
            NightRun.BeginNight(1, () => _clock);
            NightRun.DebugAddAxis(FearAxis.Trust, 30);   // 구간 1 → 하루 1쌍

            Start("H4");
            Start("H5");

            Assert.AreEqual(1, NightRun.MessagesToday.Count);

            NightRun.DebugAddAxis(FearAxis.Trust, 25);   // 55 → 하루 2쌍
            Start("H5");

            Assert.AreEqual(2, NightRun.MessagesToday.Count);
        }

        [Test]
        public void Tab중에는_보내지_않는다()
        {
            NightRun.DeckOverride = day => new List<RuleSO> { Card("H4", SpaceId.Corridor, "P4") };
            NightRun.BeginNight(1, () => _clock);
            NightRun.DebugAddAxis(FearAxis.Trust, 30);

            NightRun.Send(JudgeSignal.Tab(true));
            Start("H4");
            Assert.AreEqual(0, NightRun.MessagesToday.Count, "Tab 중에는 신규 사건도 문자도 없다");

            NightRun.Send(JudgeSignal.Tab(false));
            Start("H4");
            Assert.AreEqual(1, NightRun.MessagesToday.Count);
        }

        [Test]
        public void 새밤에_하루기록이_비워진다()
        {
            NightRun.DeckOverride = day => new List<RuleSO> { Card("H4", SpaceId.Corridor, "P4") };
            NightRun.BeginNight(1, () => _clock);
            NightRun.DebugAddAxis(FearAxis.Trust, 30);
            Start("H4");
            Assert.AreEqual(1, NightRun.MessagesToday.Count);

            Assert.IsTrue(NightRun.RequestEndNight());
            NightRun.BeginNight(2, () => _clock);

            Assert.AreEqual(0, NightRun.MessagesToday.Count);
        }
    }
}

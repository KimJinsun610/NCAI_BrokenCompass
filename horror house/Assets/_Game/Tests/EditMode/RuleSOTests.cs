using System.Collections.Generic;
using NUnit.Framework;

namespace NightDuty.Tests
{
    /// <summary>카드 데이터 검사.</summary>
    public sealed class RuleSOTests
    {
        private TestKit _kit;

        [SetUp]
        public void SetUp()
        {
            _kit = new TestKit();
        }

        [TearDown]
        public void TearDown()
        {
            _kit.Dispose();
        }

        [Test]
        public void 신뢰를_위반축으로쓰면_검사에걸린다()
        {
            RuleSO card = _kit.Card(c =>
            {
                c.CardId = "S1";
                c.Space = SpaceId.ScienceRoom;
                c.TriggerKind = SignalKind.ModelObserved;
                c.Success = new SignalCondition(SignalKind.SpaceExited, "", SpaceId.ScienceRoom);
                c.Failure = new SignalCondition(SignalKind.ZoneEntered, "x");
                c.FailureAxis = FearAxis.Trust;
            });

            List<string> errors = new List<string>();
            card.Validate(errors);

            Assert.IsTrue(errors.Exists(e => e.Contains("신뢰")));
        }

        [TestCase("H1", true)]
        [TestCase("T6", true)]
        [TestCase("H7", false)]
        [TestCase("L1", false)]
        [TestCase("h1", false)]
        public void 카드ID형식(string id, bool ok)
        {
            Assert.AreEqual(ok, RuleSO.CardIdPattern.IsMatch(id));
        }

        [Test]
        public void 참조수집은_시작신호와_조건대상을_모은다()
        {
            RuleSO card = _kit.Card(c =>
            {
                c.CardId = "H1";
                c.Space = SpaceId.Corridor;
                c.TriggerKind = SignalKind.DoorAutoOpenObserved;
                c.TargetIds = new[] { "H1.door" };
                c.Failure = new SignalCondition(SignalKind.DoorCommandAccepted);
                c.Success = new SignalCondition(SignalKind.PassageCompleted, "H1.passage");
            });

            List<string> missing = new List<string>();
            RuleReferenceCheck.FindMissing(card, new HashSet<string>(), missing);

            CollectionAssert.AreEquivalent(new[] { "H1.door", "H1.passage" }, missing);
        }
    }
}

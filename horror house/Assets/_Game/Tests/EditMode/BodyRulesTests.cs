using NUnit.Framework;

namespace NightDuty.Tests
{
    /// <summary>몸 계기 규칙(37차) — 최종 기획서 「몸 계기」 표와 같은지.</summary>
    public sealed class BodyRulesTests
    {
        [Test]
        public void 층은_25_50_75_90에서_하나씩_붙는다()
        {
            Assert.AreEqual(0, BodyRules.Tier(0));
            Assert.AreEqual(0, BodyRules.Tier(24));
            Assert.AreEqual(1, BodyRules.Tier(25));
            Assert.AreEqual(1, BodyRules.Tier(49));
            Assert.AreEqual(2, BodyRules.Tier(50));
            Assert.AreEqual(2, BodyRules.Tier(74));
            Assert.AreEqual(3, BodyRules.Tier(75));
            Assert.AreEqual(3, BodyRules.Tier(89));
            Assert.AreEqual(4, BodyRules.Tier(90));
            Assert.AreEqual(4, BodyRules.Tier(100));
        }

        [Test]
        public void 청각_심박과_층마다_더해지는_소리()
        {
            Assert.AreEqual(new[] { 0, 62, 72, 86, 104 }, new[] { BodyRules.HeartRate(0), BodyRules.HeartRate(1), BodyRules.HeartRate(2), BodyRules.HeartRate(3), BodyRules.HeartRate(4) });
            Assert.IsFalse(BodyRules.EarPressure(1));
            Assert.IsTrue(BodyRules.EarPressure(2));
            Assert.IsTrue(BodyRules.EarPressure(4), "위 층은 아래 층의 소리에 더해진다");
            Assert.IsFalse(BodyRules.Tinnitus(2));
            Assert.IsTrue(BodyRules.Tinnitus(3));
            Assert.IsFalse(BodyRules.PulseShake(3));
            Assert.IsTrue(BodyRules.PulseShake(4));
        }

        [Test]
        public void 조도_호흡_주기()
        {
            Assert.AreEqual(0f, BodyRules.BreathInterval(0));
            Assert.AreEqual(10f, BodyRules.BreathInterval(1));
            Assert.AreEqual(8f, BodyRules.BreathInterval(2));
            Assert.AreEqual(6f, BodyRules.BreathInterval(3));
            Assert.AreEqual(4.5f, BodyRules.BreathInterval(4));
        }

        [Test]
        public void 배치_발소리_에코()
        {
            Assert.AreEqual(new[] { 0, 1, 2, 3, 3 }, new[] { BodyRules.EchoCount(0), BodyRules.EchoCount(1), BodyRules.EchoCount(2), BodyRules.EchoCount(3), BodyRules.EchoCount(4) });
            Assert.AreEqual(1.15f, BodyRules.EchoSpread(2));
            Assert.AreEqual(1.3f, BodyRules.EchoSpread(3));
            Assert.IsFalse(BodyRules.EchoLate(3));
            Assert.IsTrue(BodyRules.EchoLate(4), "에코가 한 박 늦게 걸어옴");
        }

        [Test]
        public void 경계_신호는_70과_85를_처음_넘을_때_한_번씩()
        {
            Assert.AreEqual(0, BodyRules.CrossedBoundaries(60, 69, 0));
            Assert.AreEqual(1, BodyRules.CrossedBoundaries(60, 70, 0));
            Assert.AreEqual(3, BodyRules.CrossedBoundaries(60, 90, 0), "한 번에 둘을 넘으면 둘 다");
            Assert.AreEqual(2, BodyRules.CrossedBoundaries(60, 90, 1), "이미 낸 70은 다시 내지 않는다");
            Assert.AreEqual(0, BodyRules.CrossedBoundaries(90, 60, 0), "내려갈 때는 없다");
        }

        [Test]
        public void 재시작_뒤에는_한_층_약하게()
        {
            Assert.AreEqual(2, BodyRules.Weakened(3));
            Assert.AreEqual(0, BodyRules.Weakened(0));
            Assert.AreEqual(30f, BodyRules.RestartWeakSeconds);
        }
    }
}

using System.Linq;
using NUnit.Framework;

namespace NightDuty.Tests
{
    /// <summary>64차(2026-10-07 플레이테스트 피드백 4단계) — 고정 몹(과학실 인체 모형·사람 나무) 응시, 모형 이동, S-1 문구.</summary>
    public sealed class SixtyFourthPassTests
    {
        [SetUp]
        public void SetUp()
        {
            NightRun.StartNewRun();
        }

        [TearDown]
        public void TearDown()
        {
            NightRun.FixedMobStareEnabled = false;
            NightRun.StartNewRun();
            EventBus.ClearAll();
        }

        private static int Feed(FixedMobStare s, string id, float seconds, int day)
        {
            int sum = 0;
            int n = (int)System.Math.Round(seconds / 0.1f);
            for (int i = 0; i < n; i++) sum += s.Observe(id, 0.1f, day);
            return sum;
        }

        [Test]
        public void 고정_몹은_인체_모형과_사람_나무뿐이다()
        {
            Assert.IsTrue(FixedMobStare.IsFixedMob(FinalCues.ModelTarget));
            Assert.IsTrue(FixedMobStare.IsFixedMob(FinalCues.H1Object));
            Assert.IsFalse(FixedMobStare.IsFixedMob(FinalCues.BoyTarget));
            Assert.IsFalse(FixedMobStare.IsFixedMob(string.Empty));
            Assert.IsFalse(FixedMobStare.IsFixedMob(null));
        }

        [Test]
        public void 유예는_날마다_짧아지고_역설_눈으로만_1초보다_길다()
        {
            Assert.AreEqual(2.5f, FixedMobStare.GraceFor(1), 1e-5f);
            Assert.AreEqual(2f, FixedMobStare.GraceFor(2), 1e-5f);
            Assert.AreEqual(1.5f, FixedMobStare.GraceFor(3), 1e-5f);
            Assert.AreEqual(1.5f, FixedMobStare.GraceFor(5), 1e-5f);
            for (int d = 1; d <= 5; d++) Assert.Greater(FixedMobStare.GraceFor(d), ParadoxRun.EyesOnlySeconds, d + "일차");
        }

        [Test]
        public void 유예_안에서는_오르지_않고_넘으면_오래_볼수록_빨리_오른다()
        {
            FixedMobStare s = new FixedMobStare();
            Assert.AreEqual(0, Feed(s, FinalCues.ModelTarget, 1.5f, 3), "3일차 유예 1.5초");
            Assert.AreEqual(1.5f, s.Seconds, 1e-3f);
            int first = Feed(s, FinalCues.ModelTarget, 2f, 3);    // 0~2초: 3 + 0.5t
            int second = Feed(s, FinalCues.ModelTarget, 2f, 3);   // 2~4초
            Assert.AreEqual(7, first, "∫(3+0.5t) 0~2 = 7");
            Assert.AreEqual(9, second, "∫(3+0.5t) 2~4 = 9");
            Assert.Greater(second, first);
        }

        [Test]
        public void 속도는_초당_10에서_멈춘다()
        {
            FixedMobStare s = new FixedMobStare();
            Feed(s, FinalCues.H1Object, 1.5f + 20f, 3);
            Assert.AreEqual(10, Feed(s, FinalCues.H1Object, 1f, 3));
        }

        [Test]
        public void 잠깐_빗나간_시선은_이어서_세고_오래_떼면_처음부터()
        {
            FixedMobStare s = new FixedMobStare();
            Feed(s, FinalCues.ModelTarget, 1f, 3);
            Feed(s, string.Empty, 0.2f, 3);
            Assert.AreEqual(FinalCues.ModelTarget, s.TargetId, "0.2초 빗나감은 끊김이 아니다");
            Assert.AreEqual(1f, s.Seconds, 1e-3f, "빗나간 동안은 세지 않는다");

            Feed(s, string.Empty, 0.3f, 3);
            Assert.AreEqual(string.Empty, s.TargetId);
            Assert.AreEqual(0f, s.Seconds, 1e-5f);
        }

        [Test]
        public void 다른_고정_몹으로_옮기면_처음부터_센다()
        {
            FixedMobStare s = new FixedMobStare();
            Feed(s, FinalCues.ModelTarget, 2f, 3);
            Feed(s, FinalCues.H1Object, 0.1f, 3);
            Assert.AreEqual(FinalCues.H1Object, s.TargetId);
            Assert.AreEqual(0.1f, s.Seconds, 1e-4f);
        }

        [Test]
        public void 재시작_카드는_응시_출처를_사람말로_쓴다()
        {
            StringAssert.Contains("인체 모형", FixedMobStare.SourceName(FixedMobStare.SourcePrefix + FinalCues.ModelTarget));
            StringAssert.Contains("사람 나무", FixedMobStare.SourceName(FixedMobStare.SourcePrefix + FinalCues.H1Object));
            Assert.IsNull(FixedMobStare.SourceName("G1"));
            Assert.IsNull(FixedMobStare.SourceName(null));
        }

        private static void Gaze(string target, float seconds)
        {
            int n = (int)System.Math.Round(seconds / 0.1f);
            for (int i = 0; i < n; i++) NightRun.Send(JudgeSignal.Gaze(target, 0.1f));
        }

        [Test]
        public void 인체_모형을_계속_보면_배치_축이_오르다가_붙잡힌다()
        {
            NightRun.FixedMobStareEnabled = true;
            NightRun.BeginNight(3, () => 0);
            NightRun.Send(JudgeSignal.OfSpace(SignalKind.SpaceEntered, SpaceId.ScienceRoom));
            int before = NightRun.Axes.GetValue(FearAxis.Layout);

            Gaze(FinalCues.ModelTarget, 1.5f);
            Assert.AreEqual(before, NightRun.Axes.GetValue(FearAxis.Layout), "유예 안");

            Gaze(FinalCues.ModelTarget, 2f);
            Assert.AreEqual(before + 7, NightRun.Axes.GetValue(FearAxis.Layout));
            Assert.IsTrue(NightRun.RaisedSources(FearAxis.Layout).Contains(FixedMobStare.SourcePrefix + FinalCues.ModelTarget));

            Gaze(FinalCues.ModelTarget, 40f);
            Assert.IsTrue(NightRun.IsCaptured, "계속 보면 끝내 붙잡힌다");
            Assert.AreEqual(FearAxis.Layout, NightRun.Cause.Axis);
            Assert.AreEqual(FixedMobStare.SourcePrefix + FinalCues.ModelTarget, NightRun.Cause.SourceId, "붙잡힘 장면이 인체 모형 컷신을 고르는 열쇠");
        }

        [Test]
        public void 사람_나무도_같다()
        {
            NightRun.FixedMobStareEnabled = true;
            NightRun.BeginNight(4, () => 0);
            NightRun.Send(JudgeSignal.OfSpace(SignalKind.SpaceEntered, SpaceId.Corridor));
            int before = NightRun.Axes.GetValue(FearAxis.Layout);
            Gaze(FinalCues.H1Object, 3.5f);
            Assert.AreEqual(before + 7, NightRun.Axes.GetValue(FearAxis.Layout));
        }

        [Test]
        public void 끄면_응시_시간만_세고_축은_그대로()
        {
            NightRun.BeginNight(3, () => 0);
            NightRun.Send(JudgeSignal.OfSpace(SignalKind.SpaceEntered, SpaceId.ScienceRoom));
            int before = NightRun.Axes.GetValue(FearAxis.Layout);
            Gaze(FinalCues.ModelTarget, 5f);
            Assert.AreEqual(before, NightRun.Axes.GetValue(FearAxis.Layout));
            Assert.AreEqual(5f, NightRun.Stare.Seconds, 1e-3f, "목 꺾임은 꺼져 있어도 읽는다");
        }

        [Test]
        public void 새_밤이면_응시를_처음부터_센다()
        {
            NightRun.BeginNight(3, () => 0);
            Gaze(FinalCues.ModelTarget, 2f);
            NightRun.AbandonNight();
            NightRun.BeginNight(4, () => 0);
            Assert.AreEqual(0f, NightRun.Stare.Seconds, 1e-5f);
            Assert.AreEqual(string.Empty, NightRun.Stare.TargetId);
        }

        [Test]
        public void 모형은_최대_자리에서도_나갈_때마다_다른_자리로_옮긴다()
        {
            Assert.AreEqual(0, ModelProgress.NextSpot(0, 0, 0, 5), "1일차는 움직이지 않는다");
            Assert.AreEqual(2, ModelProgress.NextSpot(1, 1, 2, 7), "최대 전이면 한 칸 앞");
            Assert.AreEqual(1, ModelProgress.NextSpot(2, 1, 2, 0), "최대면 범위의 다른 자리");
            Assert.AreEqual(1, ModelProgress.NextSpot(2, 1, 2, 999));
            for (int roll = 0; roll < 30; roll++)
            {
                int next = ModelProgress.NextSpot(3, 2, 3, roll);
                Assert.AreEqual(2, next, "복도까지 나오는 밤: 3 → 2");
                int n2 = ModelProgress.NextSpot(3, 1, 3, roll);
                Assert.IsTrue(n2 == 1 || n2 == 2, "범위 안의 다른 자리: " + n2);
            }
        }

        [Test]
        public void S1_점검_문구는_정면을_보는_인체_모형이다()
        {
            Assert.AreEqual("인체 모형은 정면을 보고 있습니다.", InspectionCatalog.Find("S-1").TabletLine);
        }
    }
}

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace NightDuty.Tests
{
    /// <summary>판정 공통 정의(응시 10°·비춤 15°/8m·정지 0.2m)와 보고 준비(2m·1초).</summary>
    public sealed class SensingRulesTests
    {
        [Test]
        public void 원뿔과_비춤과_정지()
        {
            Vector3 eye = Vector3.zero;
            Assert.IsTrue(SensingRules.InCone(eye, Vector3.forward, new Vector3(0.17f, 0f, 1f), SensingRules.GazeConeDegrees), "약 9.6°");
            Assert.IsFalse(SensingRules.InCone(eye, Vector3.forward, new Vector3(0.19f, 0f, 1f), SensingRules.GazeConeDegrees), "약 10.8°");

            Assert.IsTrue(SensingRules.InBeam(eye, Vector3.forward, new Vector3(0f, 0f, 7.9f)));
            Assert.IsFalse(SensingRules.InBeam(eye, Vector3.forward, new Vector3(0f, 0f, 8.1f)), "8m 밖");
            Assert.IsFalse(SensingRules.InBeam(eye, Vector3.forward, new Vector3(1f, 0f, 3f)), "약 18°");

            Assert.IsTrue(SensingRules.IsStill(Vector3.zero, new Vector3(0.1f, 5f, 0.15f)), "높이는 보지 않는다");
            Assert.IsFalse(SensingRules.IsStill(Vector3.zero, new Vector3(0.2f, 0f, 0.1f)));
        }

        [Test]
        public void 태블릿을_들면_응시기준점이_78퍼센트로_올라간다()
        {
            Assert.AreEqual(0.5f, SensingRules.GazeViewportY(false), 1e-5f);
            Assert.AreEqual(0.78f, SensingRules.GazeViewportY(true), 1e-5f);
        }

        private static int FeedUntilReady(ReportReadiness r, float distance, int max)
        {
            bool near;
            for (int i = 1; i <= max; i++)
            {
                r.Feed(distance, true, 0.1f, out near);
                if (r.Ready) return i;
            }

            return -1;
        }

        [Test]
        public void 이미터_안에서_1초_응시하면_켜진다()
        {
            ReportReadiness r = new ReportReadiness();
            Assert.AreEqual(10, FeedUntilReady(r, 1.5f, 20));

            ReportReadiness far = new ReportReadiness();
            Assert.AreEqual(-1, FeedUntilReady(far, 2.1f, 20), "2m 밖에서는 켜지지 않는다");
        }

        [Test]
        public void 이백밀리초_이내의_끊김은_연속으로_본다()
        {
            ReportReadiness r = new ReportReadiness();
            bool near;
            for (int i = 0; i < 5; i++) r.Feed(1.5f, true, 0.1f, out near);
            r.Feed(1.5f, false, 0.1f, out near);
            r.Feed(1.5f, false, 0.1f, out near);
            for (int i = 0; i < 3; i++) r.Feed(1.5f, true, 0.1f, out near);
            Assert.IsTrue(r.Ready, "0.5 + 틈 0.2 + 0.3 = 1.0초");

            ReportReadiness broken = new ReportReadiness();
            for (int i = 0; i < 5; i++) broken.Feed(1.5f, true, 0.1f, out near);
            for (int i = 0; i < 3; i++) broken.Feed(1.5f, false, 0.1f, out near);
            for (int i = 0; i < 5; i++) broken.Feed(1.5f, true, 0.1f, out near);
            Assert.IsFalse(broken.Ready, "0.3초 끊기면 처음부터");
        }

        [Test]
        public void 켜진_뒤에는_이미터_안에_있는_동안_유지된다()
        {
            ReportReadiness r = new ReportReadiness();
            FeedUntilReady(r, 1.5f, 20);
            bool near;

            for (int i = 0; i < 30; i++) r.Feed(1.9f, false, 0.1f, out near);
            Assert.IsTrue(r.Ready, "보지 않아도 2m 안이면 유지");

            Assert.IsTrue(r.Feed(2.2f, false, 0.1f, out near), "상태가 바뀌었다");
            Assert.IsFalse(r.Ready, "2m를 벗어나면 꺼진다");
        }

        [Test]
        public void 가까이는_0점8미터_안에서_0점3초_들여다보면_한번_알린다()
        {
            ReportReadiness r = new ReportReadiness();
            int triggered = 0;
            bool near;
            for (int i = 0; i < 10; i++)
            {
                r.Feed(0.6f, true, 0.1f, out near);
                if (near) triggered++;
            }

            Assert.AreEqual(1, triggered);

            ReportReadiness passing = new ReportReadiness();
            passing.Feed(0.6f, true, 0.1f, out near);
            passing.Feed(0.6f, false, 0.1f, out near);
            passing.Feed(0.6f, true, 0.1f, out near);
            Assert.IsFalse(near, "스쳐 본 것은 세지 않는다(0.2초)");
        }
    }

    /// <summary>태블릿을 든 동안에도 판정이 흐른다(최종 기획서 「태블릿·시간 규칙」). 옛 규칙(Tab 중 정지) 스위치는 2026-10-03에 없앴다.</summary>
    public sealed class TabletJudgingTests
    {
        [SetUp]
        public void SetUp()
        {
            NightRun.StartNewRun();
        }

        [TearDown]
        public void TearDown()
        {
            NightRun.ProgramEnabled = false;
            NightRun.StartNewRun();
            EventBus.ClearAll();
        }

        [Test]
        public void 태블릿을_든_동안에도_판정한다()
        {
            TestKit.BeginProgramNight(1, () => 30);

            NightRun.Send(JudgeSignal.Tab(true));
            Assert.IsTrue(NightRun.TabletOpen);
            TestKit.ViolateRunning();

            Assert.AreEqual(Deltas.RuleViolation, NightRun.Axes.GetValue(FearAxis.Auditory));
            Assert.AreEqual(SpaceId.Corridor, NightRun.CurrentSpace, "태블릿을 든 채 들어간 공간도 현재 공간이다");

            NightRun.Send(JudgeSignal.Tab(false));
            Assert.IsFalse(NightRun.TabletOpen);
        }
    }
}

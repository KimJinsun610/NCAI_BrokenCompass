using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace NightDuty.Tests
{
    /// <summary>
    /// 71차 — 민: 「개발자 모드를 켜면 원래 게임의 흐름이 멈추고, 게임의 수칙과 연출을 내가 직접 호출하고, 상호작용을 확인할 수 있도록」.
    /// 코어 스위치 <see cref="NightRun.Sandbox"/>.
    /// </summary>
    public sealed class SeventyFirstPassTests
    {
        private int _clock;

        [SetUp]
        public void SetUp()
        {
            NightRun.StartNewRun();
            _clock = 5;
        }

        [TearDown]
        public void TearDown()
        {
            NightRun.Sandbox = false;
            NightRun.SandboxNoCapture = true;
            NightRun.ProgramEnabled = false;
            NightRun.InspectionPlanOverride = null;
            NightRun.InspectionDripEnabled = false;
            NightRun.JudgingWindowEnabled = false;
            NightRun.StartNewRun();
            EventBus.ClearAll();
        }

        private static void Run(float seconds)
        {
            int n = Mathf.RoundToInt(seconds * 10f);
            for (int i = 0; i < n; i++) NightRun.Tick(0.1f);
        }

        [Test]
        public void 흐름_정지_중에는_시각과_무관하게_판정하고_붙잡히지_않는다()
        {
            NightRun.JudgingWindowEnabled = true;
            NightRun.BeginNight(1, () => _clock);
            Assert.IsFalse(NightRun.IsJudgingNow, "00:05는 출근 — 판정 전");

            NightRun.Sandbox = true;
            Assert.IsTrue(NightRun.IsJudgingNow, "흐름 정지 = 늘 판정");
            for (int i = 0; i < 12; i++) NightRun.DebugAddAxis(FearAxis.Layout, 12);
            Assert.AreEqual(Bands.Max - 1, NightRun.Axes.GetValue(FearAxis.Layout), "99에서 멈춘다");
            Assert.IsFalse(NightRun.IsCaptured);

            NightRun.SandboxNoCapture = false;
            NightRun.DebugAddAxis(FearAxis.Layout, 12);
            Assert.IsTrue(NightRun.IsCaptured, "붙잡힘 막기를 끄면 붙잡힌다");
        }

        [Test]
        public void 흐름_정지_중에는_점검_지시가_저절로_나오지_않고_풀면_나온다()
        {
            NightRun.InspectionDripEnabled = true;
            List<InspectionAssignment> rows = new List<InspectionAssignment>
            {
                new InspectionAssignment(InspectionCatalog.Find("K-1"), false, Band.Band0, false),
                new InspectionAssignment(InspectionCatalog.Find("H-1"), false, Band.Band0, false)
            };
            NightRun.InspectionPlanOverride = (day, shown) => new InspectionPlan(day, rows, SpaceId.None, string.Empty);
            List<InspectionOrder> sent = new List<InspectionOrder>();
            EventBus.InspectionOrdered += sent.Add;
            _clock = 2;
            NightRun.BeginNight(2, () => _clock);
            NightRun.Sandbox = true;

            Run(20f);
            Assert.AreEqual(0, sent.Count, "흐름 정지 중에는 지시가 없다");
            Assert.IsTrue(NightRun.DebugIssueOrder(), "버튼으로는 낸다");
            Assert.AreEqual(1, sent.Count);

            NightRun.Sandbox = false;
            Assert.IsFalse(NightRun.BatteryFrozen);
        }

        [Test]
        public void 흐름_정지_중에도_강제로_건_조우는_끝까지_흐른다()
        {
            NightRun.ProgramEnabled = true;
            NightRun.BeginNight(1, () => _clock);
            NightRun.Sandbox = true;
            List<DirectionEvent> seen = new List<DirectionEvent>();
            EventBus.DirectionEmitted += seen.Add;
            Assert.IsTrue(NightRun.DebugForceEncounter(ProgramCatalog.Footsteps));
            Run(30f);
            Assert.IsTrue(seen.Exists(e => e.SourceId == ProgramCatalog.Footsteps && e.Phase == DirectionPhase.Confront),
                "시계가 멈춰 있어도(00:05 그대로) 판정·연출 시간은 흘러 대면까지 간다");
            Assert.AreEqual(5, NightRun.NightMinute);
        }

        [Test]
        public void 배터리_무한은_흐름_정지_중에만이다()
        {
            Assert.IsFalse(NightRun.BatteryFrozen, "평소에는 닳는다");
            NightRun.Sandbox = true;
            NightRun.SandboxInfiniteBattery = true;
            Assert.IsTrue(NightRun.BatteryFrozen);
            NightRun.SandboxInfiniteBattery = false;
            Assert.IsFalse(NightRun.BatteryFrozen);
            NightRun.SandboxInfiniteBattery = true;
        }

        // ── CCTV 사람 자리(71차 ②) ──────────────────────────────

        [Test]
        public void CCTV_자리_표는_다섯_채널_모두에_있고_빈_방_채널과_어제_자리를_피한다()
        {
            HashSet<int> channels = new HashSet<int>();
            HashSet<string> ids = new HashSet<string>();
            foreach (CctvSpot spot in CctvSpots.All)
            {
                Assert.IsTrue(spot.Channel >= 0 && spot.Channel < RuleTriggers.CctvChannels, spot.Id);
                Assert.IsTrue(ids.Add(spot.Id), "ID 중복 " + spot.Id);
                Assert.Greater(spot.Length, 2f, spot.Id + " — 걸어가는 것이 보일 만큼");
                channels.Add(spot.Channel);
            }

            Assert.AreEqual(RuleTriggers.CctvChannels, channels.Count, "기존에 나타날 수 있던 채널 다섯 모두");

            System.Random rng = new System.Random(3);
            HashSet<int> picked = new HashSet<int>();
            for (int i = 0; i < 200; i++)
            {
                CctvSpot a = CctvSpots.Pick(rng, -1, 2, "cctv.library.stacks");
                Assert.AreNotEqual(2, a.Channel, "빈 방 채널은 피한다");
                Assert.AreNotEqual("cctv.library.stacks", a.Id, "어제 자리는 피한다");
                picked.Add(a.Channel);
            }

            Assert.AreEqual(RuleTriggers.CctvChannels - 2, picked.Count, "과학실(=빈 방)·도서관(어제, 그 채널의 유일한 자리) 말고 고르게");
            Assert.AreEqual(0, CctvSpots.Pick(rng, 0).Channel, "채널을 정하면 그 채널에서");
        }

        [Test]
        public void K1_조우는_밤_시작에_정한_자리의_채널을_볼_때_걸린다()
        {
            DirectorFixture f = new DirectorFixture(3, 0.99, null, DirectorFixture.Slot(EncounterSlot.A, ProgramCatalog.CctvPerson));
            f.Director.CctvPersonChannel = "cctv.ch3";
            f.Minute = NightClock.Call1 + 10f;
            f.Enter(SpaceId.SecurityRoom);
            f.Send(JudgeSignal.Channel("cctv.ch0"));
            for (int i = 0; i < 40; i++) f.Send(JudgeSignal.CctvView("cctv.ch0", 0.1f)).Wait(0.1f);
            Assert.AreEqual(EncounterRunState.Waiting, f.Run(ProgramCatalog.CctvPerson).State, "다른 채널을 보고 있으면 기다린다");

            f.Send(JudgeSignal.Channel("cctv.ch3"));
            for (int i = 0; i < 10; i++) f.Send(JudgeSignal.CctvView("cctv.ch3", 0.1f)).Wait(0.1f);
            Assert.AreEqual(EncounterRunState.Waiting, f.Run(ProgramCatalog.CctvPerson).State, "그 채널로 넘긴 지 2초가 안 됐다");
            for (int i = 0; i < 30; i++) f.Send(JudgeSignal.CctvView("cctv.ch3", 0.1f)).Wait(0.1f);
            Assert.AreNotEqual(EncounterRunState.Waiting, f.Run(ProgramCatalog.CctvPerson).State, "정한 자리의 채널을 2초 봤다");
            Assert.IsTrue(f.Out.Exists(x => x.Kind == SignalKind.CueStarted && x.TargetId == FinalCues.CctvPerson + "@cctv.ch3"));
        }

        [Test]
        public void 슬롯_끝_무렵에는_어느_채널이든_걸린다()
        {
            DirectorFixture f = new DirectorFixture(3, 0.99, null, DirectorFixture.Slot(EncounterSlot.A, ProgramCatalog.CctvPerson));
            f.Director.CctvPersonChannel = "cctv.ch3";
            float from, to;
            TensionDirector.SlotWindow(EncounterSlot.A, out from, out to);
            f.Minute = to - 2f;
            f.Enter(SpaceId.SecurityRoom);
            f.Send(JudgeSignal.Channel("cctv.ch0"));
            for (int i = 0; i < 40; i++) f.Send(JudgeSignal.CctvView("cctv.ch0", 0.1f)).Wait(0.1f);
            Assert.AreNotEqual(EncounterRunState.Waiting, f.Run(ProgramCatalog.CctvPerson).State, "놓치지 않게 — 연출이 보고 있는 채널의 자리로 바꾼다");
        }
    }
}

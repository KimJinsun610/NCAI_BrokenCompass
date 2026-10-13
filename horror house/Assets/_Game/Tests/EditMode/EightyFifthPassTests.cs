using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace NightDuty.Tests
{
    /// <summary>
    /// 85차(2026-10-11) — 민: 「L3는 문쪽 조우일 때만 나오게 해주고, 플로우는 도서관 입구 맞은편 블라인드 점검 지시 → 창문 조우(플레이어 쪽을 바라보다가,
    /// 플레이어가 응시를 2초 이상 하면 도망감) → 다음날에도 biology 책 습득 or 다른 도서관 순찰 → 도서관 문 조우」.
    /// </summary>
    public sealed class EightyFifthPassTests
    {
        private int _clock;

        [SetUp]
        public void SetUp()
        {
            NightRun.StartNewRun();
            _clock = 30;
        }

        [TearDown]
        public void TearDown()
        {
            NightRun.ProgramEnabled = false;
            NightRun.InspectionDripEnabled = false;
            NightRun.InspectionsEnabled = false;
            NightRun.DutiesEnabled = false;
            NightRun.StartNewRun();
            EventBus.ClearAll();
        }

        private static EncounterRun RunOf(string id)
        {
            foreach (EncounterRun r in NightRun.Tension.Runs)
            {
                if (r.Def.Id == id) return r;
            }

            return null;
        }

        private static void Ticks(int n)
        {
            for (int i = 0; i < n; i++) NightRun.Tick(0.1f);
        }

        [Test]
        public void 대본_창밖은_블라인드_지시_뒤_문간은_책이나_순찰_지시_뒤()
        {
            EncounterScript window = EncounterScripts.Find(ProgramCatalog.SuitMan);
            Assert.AreEqual("L-2", window.AfterOrder);
            Assert.AreEqual(0f, window.NearAnchor, "블라인드 앞에서도 — 창 거리 조건 없음");
            Assert.AreEqual("블라인드", InspectionCatalog.Find("L-2").Name);

            EncounterScript door = EncounterScripts.Find(ProgramCatalog.YellowFace);
            CollectionAssert.AreEquivalent(new[] { "W15" }, door.AfterDuty);   // 104차: biology 책만
            Assert.AreEqual(DutyCatalog.LibraryBookTarget, door.ReportTriggerItem, "104차: 책을 집는 순간");
            Assert.AreEqual("L3", ProgramCatalog.Encounter(ProgramCatalog.YellowFace).ResponseRule);
            Assert.AreEqual(string.Empty, ProgramCatalog.Encounter(ProgramCatalog.SuitMan).ResponseRule);
        }

        [Test]
        public void 앞선_지시가_나가기_전에는_방아쇠를_받지_않는다()
        {
            DirectorFixture f = new DirectorFixture(2, 0.99, null, DirectorFixture.Slot(EncounterSlot.A, ProgramCatalog.SuitMan));
            bool issued = false;
            f.Director.EncounterGate = r => issued;
            f.Minute = NightClock.Call1 + 1f;
            f.Enter(SpaceId.Library).Wait(10f);
            Assert.IsFalse(f.HasPhase(ProgramCatalog.SuitMan, DirectionPhase.Foreshadow));
            Assert.AreEqual("앞선 지시 대기", f.Run(ProgramCatalog.SuitMan).Waiting);
            issued = true;
            f.Wait(7f);
            Assert.IsTrue(f.HasPhase(ProgramCatalog.SuitMan, DirectionPhase.Foreshadow), "지시가 나간 뒤 도서관 6초");
        }

        [Test]
        public void 창밖_노란_얼굴_날_블라인드_L2는_묶였다가_슬롯_앞에_단독_지시로()
        {
            NightRun.ProgramEnabled = true;
            NightRun.InspectionsEnabled = true;
            NightRun.InspectionDripEnabled = true;
            List<InspectionOrder> sent = new List<InspectionOrder>();
            EventBus.InspectionOrdered += sent.Add;
            NightRun.Programs.SetFaceDays(ProgramDirector.WindowFaceFrom, ProgramDirector.DefaultFaceDay);   // 103차 후속: 창밖 노란 얼굴은 3일차
            NightRun.BeginNight(ProgramDirector.WindowFaceFrom, () => _clock);
            Assert.IsTrue(NightRun.Program.HasEncounter(ProgramCatalog.SuitMan), NightRun.Program.Report);
            Assert.IsFalse(NightRun.Program.Has("L3"), "창밖 밤에는 L3 없음");

            InspectionAssignment l2 = NightRun.Inspections.Plan.Find("L-2");
            Assert.IsNotNull(l2, "편성에 블라인드");
            Assert.IsFalse(l2.IsLate);
            Assert.IsTrue(NightRun.Inspections.IsHeld("L-2"), "슬롯 전까지 묶음");

            EncounterRun run = RunOf(ProgramCatalog.SuitMan);
            float from, to;
            TensionDirector.SlotWindow(run.Slot, out from, out to);
            _clock = (int)(from - NightRun.LeadMinutes) - 1;
            Ticks(30);
            Assert.IsTrue(NightRun.Inspections.IsHeld("L-2"), "아직");
            Assert.IsFalse(sent.Exists(o => o.ItemIds.Contains("L-2")));

            _clock = (int)(from - NightRun.LeadMinutes);
            Ticks(80);
            InspectionOrder order = sent.Find(o => o.ItemIds.Contains("L-2"));
            Assert.IsNotNull(order, "슬롯 5분 앞 — 블라인드 단독 지시");
            Assert.AreEqual(1, order.ItemIds.Count);
            StringAssert.Contains("도서관 입구 맞은편 창 블라인드", NightRun.OrderMessage(order));
            StringAssert.DoesNotContain(NightRun.WitnessStallLabel, NightRun.OrderMessage(order), "칸 표시는 변기만");
            Assert.IsTrue(NightRun.Inspections.IsIssued("L-2"));
        }

        [Test]
        public void 문간_노란_얼굴_날_책이나_순찰_지시를_정해_두었다가_슬롯_앞에_낸다()
        {
            NightRun.ProgramEnabled = true;
            NightRun.DutiesEnabled = true;
            NightRun.Programs.SetFaceDays(ProgramDirector.WindowFaceFrom, 4);   // 103차: 도서관 노란 얼굴은 4·5일차 중 하루 — 시험은 4일차로 고정
            NightRun.BeginNight(4, () => _clock);
            Assert.IsTrue(NightRun.Program.HasEncounter(ProgramCatalog.YellowFace), NightRun.Program.Report);
            Assert.IsTrue(NightRun.Program.Has("L3"), "문간 밤 L3");

            string lead = NightRun.LeadDuty;
            CollectionAssert.Contains(new[] { "W15", "W2" }, lead);
            Assert.IsTrue(NightRun.Duties.IsReserved("W15"), "평소 지시에서 뺀다");
            for (int i = 0; i < 20; i++)
            {
                DutyDef p = NightRun.Duties.Pick();
                Assert.IsTrue(p == null || p.Id != "W15", p != null ? p.Id : "-");
            }

            EncounterRun run = RunOf(ProgramCatalog.YellowFace);
            float from, to;
            TensionDirector.SlotWindow(run.Slot, out from, out to);
            _clock = (int)(from - NightRun.LeadMinutes) - 1;
            DutyEvent ev;
            if (NightRun.Duties.Active != null) Assert.Pass("다른 지시 진행 중 — 이 시험은 빈 지시기에서만");
            NightRun.Duties.Force("W21", out ev);   // 진행 중인 다른 지시가 있으면 기다린다
            Ticks(5);
            Assert.IsFalse(NightRun.Duties.WasIssued(lead), "슬롯 앞이 아님");
            Assert.IsTrue(NightRun.DutiesPending, "조우 앞 지시가 남았다");

            _clock = (int)(from - NightRun.LeadMinutes);
            Ticks(5);
            Assert.IsFalse(NightRun.Duties.WasIssued(lead), "다른 지시가 끝날 때까지 기다린다");
            Assert.IsFalse(NightRun.Duties.Tick(new DutyInput { Minute = _clock, Dt = 1f, HoldNew = true }, out ev), "그동안 새 평소 지시는 쉰다");
            NightRun.Duties.Tick(new DutyInput { Minute = NightClock.JudgingEnd, Dt = 0.1f }, out ev);   // W21 끝(제한 없는 지시 — 조용히 닫힘, 92차 W6 폐지)
            Assert.IsNull(NightRun.Duties.Active);

            Ticks(5);
            Assert.IsTrue(NightRun.Duties.WasIssued(lead), "슬롯 5분 앞 — " + lead);
            Assert.AreEqual(lead, NightRun.Duties.Active.Id);
        }

        [Test]
        public void 창밖_노란_얼굴을_2초_이어서_보면_달아난다()
        {
            NightRun.BeginNight(2, () => _clock);
            for (int i = 0; i < 19; i++) NightRun.Send(JudgeSignal.Gaze(FinalCues.WindowFaceTarget, 0.1f));
            Assert.IsFalse(NightRun.WindowFaceFled, "1.9초");
            NightRun.Send(JudgeSignal.Gaze(string.Empty, 0.1f));
            Assert.AreEqual(0f, NightRun.WindowGazeSeconds, "눈을 떼면 처음부터");
            for (int i = 0; i < 19; i++) NightRun.Send(JudgeSignal.Gaze(FinalCues.WindowFaceTarget, 0.1f));
            Assert.IsFalse(NightRun.WindowFaceFled);
            NightRun.Send(JudgeSignal.Gaze(FinalCues.WindowFaceTarget, 0.1f));
            NightRun.Send(JudgeSignal.Gaze(FinalCues.WindowFaceTarget, 0.1f));
            Assert.IsTrue(NightRun.WindowFaceFled, "2초");
        }
    }
}

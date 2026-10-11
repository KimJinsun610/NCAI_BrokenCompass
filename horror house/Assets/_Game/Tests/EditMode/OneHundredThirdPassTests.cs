using System.Collections.Generic;
using NUnit.Framework;

namespace NightDuty.Tests
{
    /// <summary>
    /// 103차(2026-10-11) — 민: 교실 카드키 지시에 열쇠함 명시 · 창밖 노란 얼굴 2~3일차 / 도서관 노란 얼굴 4~5일차 ·
    /// 소년 머리 박기는 교실에 들어서면(자리가 안 보일 때) 곧장 · C3 「수업 종이 울리면 정지하십시오」(교실에 들어가면 곧 종) ·
    /// 도서관 노란 얼굴은 천천히 걸어오고 비추면 달아남(창을 일찍 닫음).
    /// </summary>
    public sealed class OneHundredThirdPassTests
    {
        [Test]
        public void 교실_카드키_지시는_경비실_열쇠함을_말한다()
        {
            DutyDef w17 = DutyCatalog.Find("W17");
            Assert.IsNotNull(w17);
            StringAssert.Contains("경비실 열쇠함", w17.Order);
            StringAssert.Contains("교탁 서랍", w17.Order);
        }

        [Test]
        public void C3_문구와_수업_종_대본()
        {
            Assert.AreEqual("수업 종이 울리면 정지하십시오.", ProgramCatalog.Rule("C3").Text);
            RuleTriggerScript bell = RuleTriggers.Find("C3");
            Assert.IsNotNull(bell, "C3는 교실에 들어가면 곧 울리는 수업 종이 단서");
            Assert.AreEqual(FinalCues.BellStart, bell.Cue);
            Assert.AreEqual(SpaceId.Classroom, bell.Space);
            Assert.LessOrEqual(bell.DwellMax, 3f, "「곧」 — 3초 안");
            Assert.IsTrue(bell.IgnoreGap, "다른 수칙 단서 간격(20초)에 밀리지 않는다");
            Assert.Greater(bell.Duration, 0f, "종이 울리는 동안 정지");
        }

        [Test]
        public void 소년_조우는_끝에_종을_울리지_않는다()
        {
            Assert.AreEqual(string.Empty, EncounterScripts.Find(ProgramCatalog.BoySeated).ReleaseCue);
            Assert.AreEqual(string.Empty, EncounterScripts.Find(ProgramCatalog.BoyBang).ReleaseCue);
        }

        [Test]
        public void 소년_머리_박기는_교실에_들어서면_걸린다()
        {
            EncounterScript bang = EncounterScripts.Find(ProgramCatalog.BoyBang);
            Assert.AreEqual(EncounterTrigger.EnterSpace, bang.Trigger);
            Assert.AreEqual(0f, bang.DeepMargin, "안쪽 깊이 조건 없음");
            Assert.AreEqual(SpaceId.Classroom_1_3, bang.ExactSpace);
        }

        [Test]
        public void 도서관_노란_얼굴은_걸어올_만큼_창이_길다()
        {
            Assert.GreaterOrEqual(EncounterScripts.Find(ProgramCatalog.YellowFace).Window, 30f);
        }

        [Test]
        public void 대면_창을_일찍_닫을_수_있다()
        {
            List<SlotEncounter> slots = new List<SlotEncounter> { new SlotEncounter(EncounterSlot.A, ProgramCatalog.Encounter(ProgramCatalog.ToiletGirl), false) };
            NightProgram program = new NightProgram(2, slots, new List<RuleDef>(), "test", new EncounterDef[0]);
            TensionDirector d = new TensionDirector(program, 2, 0, new FixedRandom(0.99));
            List<DirectionEvent> events = new List<DirectionEvent>();
            d.Emitted += events.Add;
            float minute = NightClock.Call1 + 10f;

            Assert.IsFalse(d.CloseWindowNow(ProgramCatalog.ToiletGirl), "대면 전에는 닫을 창이 없다");
            d.Observe(JudgeSignal.OfSpace(SignalKind.SpaceEntered, SpaceId.Toilet));
            EncounterRun run = null;
            for (int i = 0; i < 100 && (run == null || run.State != EncounterRunState.Active); i++)
            {
                d.Tick(minute, 0.1f, 30, Band.Band0, false);
                JudgeSignal s;
                while (d.TryDequeue(out s)) { }
                foreach (EncounterRun r in d.Runs) if (r.Def.Id == ProgramCatalog.ToiletGirl) run = r;
            }

            Assert.IsNotNull(run);
            Assert.AreEqual(EncounterRunState.Active, run.State, "대면");
            Assert.IsTrue(d.CloseWindowNow(ProgramCatalog.ToiletGirl));
            d.Tick(minute, 0.1f, 30, Band.Band0, false);
            Assert.IsTrue(events.Exists(e => e.SourceId == ProgramCatalog.ToiletGirl && e.Phase == DirectionPhase.WindowClose), "다음 틱에 창 닫힘");
        }

        [Test]
        public void 맞춤법_띄어쓰기_정리()
        {
            Assert.AreEqual("정전 중에는 손전등을 켜지 마십시오.", ProgramCatalog.Rule("S4").Text);
            Assert.AreEqual("열어 둔 문들을 정리해주십시오.", DutyCatalog.Find("W4").Order);
            Assert.AreEqual("경비실 접이식 의자 둘이 같은 쪽을 보는지 확인해주십시오.", InspectionCatalog.Find("K-2").TabletLine);
            foreach (RuleDef r in ProgramCatalog.AllRules) StringAssert.DoesNotContain("키지", r.Text, r.Id);
            foreach (DutyDef d in DutyCatalog.All) StringAssert.DoesNotContain(" 해주십시오", d.Order, d.Id);
        }

        [Test]
        public void 모형_급습은_과학실_쪽_복도에_들어서면()
        {
            EncounterScript rush = EncounterScripts.Find(ProgramCatalog.ModelRush);
            Assert.AreEqual(EncounterTrigger.EnterSpace, rush.Trigger);
            Assert.AreEqual(SpaceId.Corridor, rush.Space);
            Assert.AreEqual(EncounterScripts.RushNearMeters, rush.NearAnchor);
            Assert.AreEqual(StageAnchors.RushHall, rush.StageAnchor);
            Assert.GreaterOrEqual(rush.Window, 20f, "지켜보는 시간(최대 12초) + 달리기");
        }

        [Test]
        public void 현관_인체_나무_응시는_밤_판정_시간에만_축을_올린다()
        {
            NightRun.StartNewRun();
            try
            {
                NightRun.FixedMobStareEnabled = true;
                Assert.IsFalse(NightRun.AddLobbyTreeStare(FearAxis.Layout, 1), "밤 전");
                NightRun.BeginNight(2, () => 80);
                int before = NightRun.Axes.GetValue(FearAxis.Auditory);
                Assert.IsTrue(NightRun.AddLobbyTreeStare(FearAxis.Auditory, 2));
                Assert.AreEqual(before + 2, NightRun.Axes.GetValue(FearAxis.Auditory));
                Assert.IsFalse(NightRun.AddLobbyTreeStare(FearAxis.Trust, 2), "신뢰는 올리지 않는다");
                NightRun.FixedMobStareEnabled = false;
                Assert.IsFalse(NightRun.AddLobbyTreeStare(FearAxis.Layout, 2), "스위치가 꺼져 있으면 안 올린다");
            }
            finally
            {
                NightRun.FixedMobStareEnabled = false;
                NightRun.StartNewRun();
            }
        }
    }
}

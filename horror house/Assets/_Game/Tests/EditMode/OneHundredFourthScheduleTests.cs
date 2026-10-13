using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace NightDuty.Tests
{
    /// <summary>
    /// 104차(사용자: 「모든 연출에서 축 조건 제거 · 날짜 편성 … 이월은 없어. 의도한 날짜에 무조건 목격해야 해」 → 고정 분배 · 첫 역설 4일차 · 유도 지시 + 시계 멈춤 ·
    /// 창밖 노란 얼굴 3일차): 고정 날짜 편성과 순서대로 여는 디렉터.
    /// </summary>
    public sealed class OneHundredFourthScheduleTests
    {
        [SetUp]
        public void SetUp()
        {
            ProgramDirector.FixedSchedule = true;
        }

        [TearDown]
        public void TearDown()
        {
            ProgramDirector.FixedSchedule = false;
            NightRun.StartNewRun();
            EventBus.ClearAll();
        }

        private static ProgramRequest Request(int day, Band band)
        {
            return new ProgramRequest { Day = day, Shown = FixedBands.All(band), Survival = FixedBands.All(Band.Band0) };
        }

        private static ProgramDirector Director(string earlyBlackout, int lateDay)
        {
            ProgramDirector d = new ProgramDirector(new System.Random(7));
            d.SetBlackouts(earlyBlackout, lateDay);
            return d;
        }

        [Test]
        public void 날짜표대로_조우가_순서와_함께_편성된다()
        {
            ProgramDirector d = Director(ProgramCatalog.ScienceBlackout, 4);
            Assert.AreEqual(0, d.Build(Request(1, Band.Band0)).Sequence.Count, "1일차는 조우 없음");

            NightProgram day2 = d.Build(Request(2, Band.Band0));
            CollectionAssert.AreEquivalent(new[] { ProgramCatalog.CeilingLegs, ProgramCatalog.ScienceBlackout }, day2.Sequence);
            Assert.AreEqual(ProgramCatalog.CeilingLegs, day2.Sequence[day2.Sequence.Count - 1], "2일차 클라이맥스는 맨 뒤");
            Assert.IsTrue(day2.Sequential);
            Assert.IsTrue(day2.HasEncounter(ProgramCatalog.CeilingLegs));
            Assert.IsTrue(day2.Has("S4"));

            NightProgram day3 = d.Build(Request(3, Band.Band0));
            CollectionAssert.AreEquivalent(new[] { ProgramCatalog.SuitMan, ProgramCatalog.Footsteps, ProgramCatalog.CctvPerson, ProgramCatalog.ToiletBlackout }, day3.Sequence, "창밖 노란 얼굴 · 화장실 소등 3일차");
            Assert.AreEqual(ProgramCatalog.Footsteps, day3.Sequence[3]);
            Assert.IsTrue(day3.Has("H3"));
            Assert.IsTrue(day3.Has("K1"));

            NightProgram day4 = d.Build(Request(4, Band.Band0));
            CollectionAssert.AreEquivalent(new[] { ProgramCatalog.BoyBang, ProgramCatalog.ToiletGirl, ProgramCatalog.PeopleTree }, day4.Sequence);
            Assert.AreEqual(ProgramCatalog.BoyBang, day4.Sequence[2]);
            foreach (string id in new[] { "C2", "C3", "T4", "H1" }) Assert.IsTrue(day4.Has(id), id);
            Assert.IsTrue(day4.IsStanding(ProgramCatalog.HallEndFigure), "104차: 복도 끝 모형은 그냥 서 있다(조우 아님)");
            Assert.IsFalse(day4.HasEncounter(ProgramCatalog.HallEndFigure));
            Assert.IsFalse(day4.Has("S5"), "서 있을 뿐 — 수칙 없음");
            Assert.LessOrEqual(day4.Deck.Count, System.Math.Max(ProgramDirector.RulesFor(4), 6), "장수는 그대로 — 조우 수칙 말고 나머지를 뺀다");
            for (int day = 2; day <= 5; day++)
            {
                NightProgram p = new ProgramDirector(new System.Random(day)).Build(Request(day, Band.Band0));
                int locked = 0;
                foreach (RuleDef r in p.Deck) if (!r.IsStandalone || r.Id == "K4") locked++;
                Assert.LessOrEqual(p.Deck.Count, System.Math.Max(ProgramDirector.RulesFor(day), locked), day + "일차 장수");
            }

            NightProgram day5 = d.Build(Request(5, Band.Band0));
            CollectionAssert.AreEquivalent(new[] { ProgramCatalog.PeopleTree, ProgramCatalog.CallingVoice, ProgramCatalog.YellowFace }, day5.Sequence);
            Assert.AreEqual(ProgramCatalog.YellowFace, day5.Sequence[2], "5일차 클라이맥스는 도서관 노란 얼굴");
            Assert.AreEqual(EncounterSlot.C, day5.WindowOf(ProgramCatalog.YellowFace), "클라이맥스는 가장 늦은 창");
            foreach (string id in new[] { "K4", "H1", "H4", "L3", "S5" }) Assert.IsTrue(day5.Has(id), id);
            Assert.IsTrue(day5.IsStanding(ProgramCatalog.ModelRush), "104차: 모형 급습은 밤새 대기(빛을 받으면 곧바로)");
        }

        [Test]
        public void 축이_낮아도_조우가_빠지지_않는다()
        {
            ProgramDirector d = Director(ProgramCatalog.ScienceBlackout, 5);
            for (int day = 1; day <= 5; day++)
            {
                NightProgram p = d.Build(Request(day, Band.Band0));
                Assert.AreEqual(d.FixedEncounters(day).Count, p.Sequence.Count, day + "일차");
            }

            Assert.IsTrue(d.FixedOn(ProgramCatalog.ToiletBlackout, 3), "화장실 소등은 3일차");
            Assert.IsFalse(d.FixedOn(ProgramCatalog.ToiletBlackout, 5));
            Assert.IsTrue(d.FixedOn(ProgramCatalog.ScienceBlackout, 2), "과학실 소등은 2일차");
        }

        [Test]
        public void 첫_역설은_소년_날인_4일차()
        {
            Assert.AreEqual(4, ProgramCatalog.FirstParadoxDay);
            ProgramDirector.FixedSchedule = false;
            Assert.AreEqual(2, ProgramCatalog.FirstParadoxDay, "옛 편성은 2일차");
        }

        [Test]
        public void 회피_불가_강제는_그날_조우의_수칙만()
        {
            ProgramDirector d = Director(ProgramCatalog.ScienceBlackout, 4);
            Assert.IsFalse(d.CanForce("H3", 4), "발소리는 3일차뿐");
            Assert.IsTrue(d.CanForce("H3", 3));
            Assert.IsTrue(d.CanForce("K1", 3), "CCTV 날");
            Assert.IsFalse(d.CanForce("K1", 4));
            Assert.IsFalse(d.CanForce("K2", 3), "CCTV 날 경비실은 K1");
            Assert.IsTrue(d.CanForce("C2", 4));
            Assert.IsFalse(d.CanForce("C2", 2));
            Assert.IsTrue(d.CanForce("T1", 2), "혼자 서는 수칙");
        }

        private static DirectorFixture Sequential(params string[] ids)
        {
            List<SlotEncounter> slots = new List<SlotEncounter>();
            List<RuleDef> deck = new List<RuleDef>();
            for (int i = 0; i < ids.Length; i++)
            {
                EncounterDef e = ProgramCatalog.Encounter(ids[i]);
                slots.Add(new SlotEncounter((EncounterSlot)i, e, false));
                if (e.ResponseRule.Length > 0) deck.Add(ProgramCatalog.Rule(e.ResponseRule));
            }

            DirectorFixture f = new DirectorFixture(3, 0.99, new string[0]);
            TensionDirector t = new TensionDirector(new NightProgram(3, slots, deck, "test", null, ids), 3, 0, new FixedRandom(0.99));
            t.Emitted += f.Events.Add;
            typeof(DirectorFixture).GetField("Director").SetValue(f, t);
            return f;
        }

        [Test]
        public void 순서대로_하나씩_열고_놓치지_않는다()
        {
            DirectorFixture f = Sequential(ProgramCatalog.ToiletBlackout, ProgramCatalog.Footsteps);
            TensionDirector t = f.Director;
            Assert.IsTrue(t.Sequential);
            Assert.AreEqual(ProgramCatalog.ToiletBlackout, t.Head.Def.Id);

            f.Minute = 20f;   // 00:30 전에는 열지 않는다
            f.Enter(SpaceId.Corridor).Wait(15f);
            Assert.AreEqual(EncounterRunState.Waiting, t.Runs[1].State, "차례가 아닌 발소리는 복도를 걸어도 열리지 않는다");

            f.Minute = 200f;
            f.Wait(0.5f);
            f.Minute = NightClock.JudgingEnd + 5f;
            f.Wait(0.5f);
            Assert.AreEqual(EncounterRunState.Waiting, t.Runs[0].State, "03:32가 지나도 놓치지 않는다");
            Assert.IsTrue(t.EncountersPending);

            f.Minute = 100f;
            f.Exit(SpaceId.Corridor).Enter(SpaceId.Toilet).Wait(12f);   // 머묾 4초 + 발동 직전 1.5~6초
            Assert.AreNotEqual(EncounterRunState.Waiting, t.Runs[0].State, "화장실에 머물면 소등이 시작");
            Assert.AreEqual(ProgramCatalog.ToiletBlackout, t.Head.Def.Id);
        }

        [Test]
        public void 원래_시간_뼈대_안에서_창을_나눈다()
        {
            CollectionAssert.AreEqual(new[] { EncounterSlot.A, EncounterSlot.B }, NightProgram.SplitWindows(2));
            CollectionAssert.AreEqual(new[] { EncounterSlot.A, EncounterSlot.B, EncounterSlot.C }, NightProgram.SplitWindows(3));
            CollectionAssert.AreEqual(new[] { EncounterSlot.A, EncounterSlot.A, EncounterSlot.B, EncounterSlot.C }, NightProgram.SplitWindows(4));
            CollectionAssert.AreEqual(new[] { EncounterSlot.A, EncounterSlot.A, EncounterSlot.B, EncounterSlot.B, EncounterSlot.C }, NightProgram.SplitWindows(5));

            List<SlotEncounter> slots = new List<SlotEncounter> { new SlotEncounter(EncounterSlot.A, ProgramCatalog.Encounter(ProgramCatalog.ToiletBlackout), false) };
            NightProgram p = new NightProgram(2, slots, new[] { ProgramCatalog.Rule("S4") }, "test", null, new[] { ProgramCatalog.ToiletBlackout }, new[] { EncounterSlot.B });
            DirectorFixture f = new DirectorFixture(2, 0.5, new string[0]);
            TensionDirector t = new TensionDirector(p, 2, 0, new FixedRandom(0.5));
            typeof(DirectorFixture).GetField("Director").SetValue(f, t);
            f.Minute = 70f;
            f.Enter(SpaceId.Toilet).Wait(15f);
            Assert.AreEqual(EncounterRunState.Waiting, t.Runs[0].State, "창 B(02:16) 전에는 그 방에 있어도 열지 않는다");
            f.Minute = NightClock.RelaxStart + 1f;
            f.Wait(3f);
            Assert.AreEqual(EncounterRunState.Waiting, t.Runs[0].State, "이완(01:52~02:16)에도");
            f.Minute = NightClock.Call2 + 1f;
            f.Wait(12f);
            Assert.AreNotEqual(EncounterRunState.Waiting, t.Runs[0].State, "02:16부터");
        }

        [Test]
        public void 순서는_회차마다_섞이고_클라이맥스만_고정()
        {
            HashSet<string> firsts = new HashSet<string>();
            for (int seed = 0; seed < 30; seed++)
            {
                NightProgram p = new ProgramDirector(new System.Random(seed)).Build(Request(4, Band.Band0));
                Assert.AreEqual(ProgramCatalog.BoyBang, p.Sequence[p.Sequence.Count - 1]);
                firsts.Add(p.Sequence[0]);
            }

            Assert.GreaterOrEqual(firsts.Count, 2, "4일차 첫 조우가 회차마다 달라진다(104차: 소녀·사람 나무 둘 — 복도 끝 모형은 밤새 서 있을 뿐)");
        }

        [Test]
        public void 위반해도_다음_날_조우가_더해지지_않는다()
        {
            // 104차(사용자: 「후속 개념은 없애줘」): 고정 편성은 날짜표뿐 — 예약 자체를 받지 않는다.
            ProgramDirector d = Director(ProgramCatalog.ScienceBlackout, 4);
            d.Reserve(ProgramCatalog.Footsteps);
            d.Reserve(ProgramCatalog.ModelRush);
            CollectionAssert.IsEmpty(d.Reserved);
            NightProgram day4 = d.Build(Request(4, Band.Band0));
            CollectionAssert.AreEquivalent(new[] { ProgramCatalog.BoyBang, ProgramCatalog.ToiletGirl, ProgramCatalog.PeopleTree }, day4.Sequence);
            NightProgram day5 = d.Build(Request(5, Band.Band0));
            CollectionAssert.AreEquivalent(new[] { ProgramCatalog.PeopleTree, ProgramCatalog.CallingVoice, ProgramCatalog.YellowFace }, day5.Sequence);
        }

        [Test]
        public void 빈_칸은_한_번만_걸린다()
        {
            DirectorFixture f = Sequential(ProgramCatalog.ToiletBlackout);
            TensionDirector t = f.Director;
            t.SetWildcard("fake.locker.rattle", NightClock.Call1);
            f.Minute = 50f;
            f.Wait(1f);
            Assert.IsFalse(t.WildcardDone, "01:00 전에는 아직");
            typeof(TensionDirector).GetField("_wildcardAt", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(t, 0f);
            f.Wait(1f);
            Assert.IsTrue(t.WildcardDone);
            int n = 0;
            foreach (DirectionEvent e in f.Events) if (e.Kind == DirectionEventKind.FakeScare && e.SourceId == "fake.locker.rattle") n++;
            Assert.AreEqual(1, n);
        }

        [Test]
        public void 유도_뒤에는_방아쇠가_풀린다()
        {
            DirectorFixture f = Sequential(ProgramCatalog.Footsteps);
            TensionDirector t = f.Director;
            f.Minute = 60f;
            f.Enter(SpaceId.Corridor).Wait(1f);
            Assert.AreEqual(EncounterRunState.Waiting, t.Runs[0].State, "아직 10m를 걷지 않았다");

            t.Runs[0].GuidedAt = t.Now;
            f.Wait(TensionDirector.SeqLenientSeconds + TensionDirector.SeqArmMax + 1f);
            Assert.AreNotEqual(EncounterRunState.Waiting, t.Runs[0].State, "유도 뒤 25초 — 복도에 있기만 하면");
        }

        [Test]
        public void 밤_시작에는_경비실을_나설_때까지_시계를_붙든다()
        {
            NightRun.StartNewRun();
            NightRun.BeginNight(1, () => 0);
            Assert.IsTrue(NightRun.StartHeld, "1일차도");
            Assert.IsTrue(NightRun.ClockHeld);
            NightRun.Send(JudgeSignal.OfSpace(SignalKind.SpaceEntered, SpaceId.SecurityRoom));
            Assert.IsTrue(NightRun.StartHeld, "경비실 안에서는 그대로");
            NightRun.Send(JudgeSignal.OfSpace(SignalKind.SpaceExited, SpaceId.SecurityRoom));
            Assert.IsFalse(NightRun.StartHeld, "나서면 시작");
            Assert.IsTrue(NightRun.ShiftStarted);
            Assert.IsFalse(NightRun.ClockHeld);
        }

        [Test]
        public void 경비실에_머물러도_상한이_지나면_시작하고_체크포인트_시각에는_붙들지_않는다()
        {
            NightRun.StartNewRun();
            NightRun.BeginNight(3, () => 0);
            for (int i = 0; i < 10; i++) NightRun.Tick(NightRun.StartHoldMaxSeconds / 9f);
            Assert.IsFalse(NightRun.StartHeld, "상한");

            NightRun.StartNewRun();
            NightRun.BeginNight(3, () => 30);
            Assert.IsFalse(NightRun.StartHeld, "00:00이 아닌 시각(체크포인트·개발자 이동)");
        }

        [Test]
        public void 빈_칸은_체크포인트_전에_걸렸으면_재시작해도_다시_걸지_않는다()
        {
            DirectorFixture f = Sequential(ProgramCatalog.ToiletBlackout);
            TensionDirector t = f.Director;
            t.SetWildcard("fake.locker.rattle", NightClock.Call1);
            f.Minute = 70f;
            f.Wait(1f);
            Assert.IsTrue(t.WildcardDone);

            t.ResetToRest(1, 100f);
            Assert.IsTrue(t.WildcardDone, "02:… 체크포인트 — 이미 걸렸다");
            t.ResetToRest(2, 0f);
            Assert.IsFalse(t.WildcardDone, "00:00 재시작 — 다시");
            f.Minute = 30f;
            f.Wait(1f);
            Assert.IsFalse(t.WildcardDone, "01:00 전에는 아직(옛: 재시작하면 첫 판정 틱에 바로)");
        }

        [Test]
        public void 조우가_붙잡으면_그_축으로_포획되고_출처가_남는다()
        {
            NightRun.StartNewRun();
            NightRun.BeginNight(5, () => 150);
            NightRun.CaptureByEncounter(FearAxis.Illuminance, "E.YellowFace.caught");
            Assert.IsTrue(NightRun.IsCaptured);
            Assert.AreEqual(FearAxis.Illuminance, NightRun.Cause.Axis);
            Assert.AreEqual("E.YellowFace.caught", NightRun.Cause.SourceId, "붙잡힘 연출이 출처를 보고 얼굴 장면부터 튼다");
            NightRun.CaptureByEncounter(FearAxis.Auditory, "x");   // 이미 붙잡힘 — 무시
            Assert.AreEqual(FearAxis.Illuminance, NightRun.Cause.Axis);
        }

        [Test]
        public void 시체는_사다리_보고를_누르는_순간_떨어지고_보고를_잠깐_멈춘다()
        {
            DirectorFixture f = Sequential(ProgramCatalog.CeilingLegs);
            TensionDirector t = f.Director;
            t.GazeTargetReportMode = true;
            f.Minute = 60f;
            f.Enter(SpaceId.Classroom).Wait(2f);
            Assert.AreEqual(EncounterRunState.Waiting, t.Runs[0].State, "보고 키를 누르기 전에는 떨어지지 않는다");
            t.NoteReportHold(InspectionCatalog.TargetPrefix + "C-3");
            f.Wait(0.1f);
            Assert.AreNotEqual(EncounterRunState.Waiting, t.Runs[0].State, "누르는 순간");
            Assert.IsTrue(t.ReportInterrupted, "보고 도중 — 보고를 멈춘다");
            bool shortFore = false;
            foreach (DirectionEvent e in f.Events) if (e.Phase == DirectionPhase.Foreshadow && e.SourceId == ProgramCatalog.CeilingLegs) shortFore = e.Duration <= TensionDirector.ReportForeshadow + 0.01f;
            Assert.IsTrue(shortFore, "전조는 짧게");
            f.Wait(TensionDirector.ReportInterruptSeconds + 0.3f);
            Assert.IsFalse(t.ReportInterrupted);
        }

        [Test]
        public void 다른_항목을_보고해도_시체는_떨어지지_않는다()
        {
            DirectorFixture f = Sequential(ProgramCatalog.CeilingLegs);
            TensionDirector t = f.Director;
            t.GazeTargetReportMode = true;
            f.Minute = 60f;
            f.Enter(SpaceId.Classroom).Wait(1f);
            t.NoteReportHold(InspectionCatalog.TargetPrefix + "C-1");
            f.Wait(1f);
            Assert.AreEqual(EncounterRunState.Waiting, t.Runs[0].State);
        }

        [Test]
        public void 빈칸_해독은_기본으로_꺼져_있다()
        {
            Assert.IsFalse(NightRun.BlankDecodingEnabled, "104차: 수칙의 _? 가림표는 끝까지 그대로");
            Assert.AreEqual(ProgramCatalog.Rule("C2").Text, NightRun.ShownRuleText("C2", ProgramCatalog.Rule("C2").Text));
        }

        [Test]
        public void 밤새_서_있는_모형_급습은_곧바로_시작하고_남은_조우로_세지_않는다()
        {
            DirectorFixture f = Sequential(ProgramCatalog.PeopleTree);
            TensionDirector t = f.Director;
            f.Minute = 60f;
            f.Wait(0.5f);
            Assert.IsTrue(t.StartNow(ProgramCatalog.ModelRush));
            EncounterRun rush = t.Runs[t.Runs.Count - 1];
            Assert.AreEqual(ProgramCatalog.ModelRush, rush.Def.Id);
            Assert.IsTrue(rush.Forced);
            Assert.AreNotEqual(EncounterRunState.Waiting, rush.State, "전조 없이 곧바로");
            Assert.AreNotEqual(EncounterRunState.Foreshadow, rush.State);
            Assert.IsTrue(t.EncountersPending, "사람 나무는 그대로 남았다");
            Assert.AreEqual(ProgramCatalog.PeopleTree, t.Head.Def.Id, "순서 밖");
        }

        [Test]
        public void 소년은_교실_화분을_보고하는_순간_나타난다()
        {
            DirectorFixture f = Sequential(ProgramCatalog.BoyBang);
            TensionDirector t = f.Director;
            t.GazeTargetReportMode = true;
            f.Minute = 60f;
            f.Enter(SpaceId.Classroom).Wait(8f);
            Assert.AreEqual(EncounterRunState.Waiting, t.Runs[0].State, "교실에 있어도 화분 보고 전에는 아직");
            t.NoteReported("C-2");
            f.Wait(0.2f);
            Assert.AreEqual(EncounterRunState.Waiting, t.Runs[0].State, "다른 항목은 아님");
            t.NoteReported("C-1");
            f.Wait(0.1f);
            Assert.AreNotEqual(EncounterRunState.Waiting, t.Runs[0].State, "화분 보고 — 곧바로");
            Assert.AreNotEqual(EncounterRunState.Foreshadow, t.Runs[0].State, "전조 없이");
        }

        [Test]
        public void 노란_얼굴은_biology_책을_집는_순간_서서_응시를_기다린다()
        {
            DirectorFixture f = Sequential(ProgramCatalog.YellowFace);
            TensionDirector t = f.Director;
            t.GazeTargetReportMode = true;
            t.SightGated = true;
            f.Minute = 60f;
            f.Enter(SpaceId.Library).Wait(10f);
            Assert.AreEqual(EncounterRunState.Waiting, t.Runs[0].State, "도서관 깊이 있어도 책을 집기 전에는 아직");
            t.NoteReported(DutyCatalog.LibraryBookTarget);
            f.Wait(0.1f);
            Assert.AreEqual(EncounterRunState.Presenting, t.Runs[0].State, "집는 순간 문간에 선다");
            f.Wait(TensionDirector.PresentMaxSeconds + 5f);
            Assert.AreEqual(EncounterRunState.Presenting, t.Runs[0].State, "볼 때까지 서 있다");
            Assert.IsTrue(t.NotifySeen(ProgramCatalog.YellowFace));
            f.Wait(0.1f);
            Assert.AreEqual(EncounterRunState.Active, t.Runs[0].State, "응시하는 순간 대면(걸어오기 시작)");
        }

        [Test]
        public void 소녀는_3번_칸_지시가_나가는_순간_선다()
        {
            DirectorFixture f = Sequential(ProgramCatalog.ToiletGirl);
            TensionDirector t = f.Director;
            t.GazeTargetReportMode = true;
            bool ordered = false;
            t.ItemOrdered = id => ordered && id == InspectionCatalog.ReverseReportItem;
            f.Minute = 60f;
            f.Enter(SpaceId.Corridor).Wait(3f);
            Assert.AreEqual(EncounterRunState.Waiting, t.Runs[0].State);
            ordered = true;
            f.Wait(0.1f);
            Assert.AreNotEqual(EncounterRunState.Waiting, t.Runs[0].State, "화장실 밖이어도 지시가 나가면 곧바로");
        }

        [Test]
        public void 서_있는_사람_나무는_남은_조우로_세지_않는다()
        {
            DirectorFixture f = Sequential(ProgramCatalog.PeopleTree);
            TensionDirector t = f.Director;
            Assert.IsTrue(t.EncountersPending);
            t.Runs[0].State = EncounterRunState.Active;
            Assert.IsFalse(t.EncountersPending);
        }
    }
}

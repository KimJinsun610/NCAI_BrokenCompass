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
            CollectionAssert.AreEquivalent(new[] { ProgramCatalog.BoyBang, ProgramCatalog.ToiletGirl, ProgramCatalog.PeopleTree, ProgramCatalog.HallEndFigure }, day4.Sequence);
            Assert.AreEqual(ProgramCatalog.BoyBang, day4.Sequence[3]);
            foreach (string id in new[] { "C2", "C3", "T4", "H1", "S5" }) Assert.IsTrue(day4.Has(id), id);
            Assert.LessOrEqual(day4.Deck.Count, System.Math.Max(ProgramDirector.RulesFor(4), 6), "장수는 그대로 — 조우 수칙 말고 나머지를 뺀다");
            for (int day = 2; day <= 5; day++)
            {
                NightProgram p = new ProgramDirector(new System.Random(day)).Build(Request(day, Band.Band0));
                int locked = 0;
                foreach (RuleDef r in p.Deck) if (!r.IsStandalone || r.Id == "K4") locked++;
                Assert.LessOrEqual(p.Deck.Count, System.Math.Max(ProgramDirector.RulesFor(day), locked), day + "일차 장수");
            }

            NightProgram day5 = d.Build(Request(5, Band.Band0));
            CollectionAssert.AreEquivalent(new[] { ProgramCatalog.PeopleTree, ProgramCatalog.CallingVoice, ProgramCatalog.YellowFace, ProgramCatalog.ModelRush }, day5.Sequence);
            Assert.AreEqual(ProgramCatalog.YellowFace, day5.Sequence[3], "5일차 클라이맥스는 도서관 노란 얼굴");
            Assert.AreEqual(EncounterSlot.C, day5.WindowOf(ProgramCatalog.YellowFace), "클라이맥스는 가장 늦은 창");
            foreach (string id in new[] { "K4", "H1", "H4", "L3", "S5" }) Assert.IsTrue(day5.Has(id), id);
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
            NightProgram p = new NightProgram(2, slots, new[] { ProgramCatalog.Rule("S4") }, "test", null, new[] { ProgramCatalog.ToiletBlackout }, null, new[] { EncounterSlot.B });
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

            Assert.GreaterOrEqual(firsts.Count, 3, "4일차 첫 조우가 회차마다 달라진다");
        }

        [Test]
        public void 위반한_조우는_다음_날_후속으로_보장_없이_온다()
        {
            ProgramDirector d = Director(ProgramCatalog.ScienceBlackout, 4);
            d.Reserve(ProgramCatalog.Footsteps);   // 3일차 H3 위반
            NightProgram day4 = d.Build(Request(4, Band.Band0));
            Assert.IsTrue(day4.Sequence.Contains(ProgramCatalog.Footsteps), "후속");
            Assert.IsTrue(day4.IsOptional(ProgramCatalog.Footsteps));
            Assert.IsFalse(day4.IsOptional(ProgramCatalog.BoyBang));
            Assert.IsTrue(day4.Has("H3"));
            Assert.AreEqual(ProgramCatalog.BoyBang, day4.Sequence[day4.Sequence.Count - 1], "후속은 클라이맥스 앞");

            NightProgram day5 = d.Build(Request(5, Band.Band0));
            Assert.IsFalse(day5.Sequence.Contains(ProgramCatalog.Footsteps), "지키면(예약 없음) 조용");

            ProgramDirector r = Director(ProgramCatalog.ScienceBlackout, 4);
            r.Reserve(ProgramCatalog.ModelRush);   // S3 위반(옛 예약)
            NightProgram day3 = r.Build(Request(3, Band.Band0));
            Assert.IsTrue(day3.IsOptional(ProgramCatalog.HallEndFigure), "모형이 앞당겨 복도 끝에");
            Assert.IsFalse(day3.Sequence.Contains(ProgramCatalog.ModelRush));
        }

        [Test]
        public void 후속은_오래_기다리지_않고_남은_조우로_세지_않는다()
        {
            List<SlotEncounter> slots = new List<SlotEncounter> { new SlotEncounter(EncounterSlot.A, ProgramCatalog.Encounter(ProgramCatalog.Footsteps), false), new SlotEncounter(EncounterSlot.B, ProgramCatalog.Encounter(ProgramCatalog.ToiletBlackout), false) };
            NightProgram p = new NightProgram(4, slots, new[] { ProgramCatalog.Rule("H3"), ProgramCatalog.Rule("S4") }, "test", null,
                new[] { ProgramCatalog.Footsteps, ProgramCatalog.ToiletBlackout }, new[] { ProgramCatalog.Footsteps });
            DirectorFixture f = new DirectorFixture(4, 0.5, new string[0]);
            TensionDirector t = new TensionDirector(p, 4, 0, new FixedRandom(0.5));
            typeof(DirectorFixture).GetField("Director").SetValue(f, t);
            f.Minute = 60f;
            f.Enter(SpaceId.Toilet).Wait(1f);
            Assert.IsTrue(t.Runs[0].Optional);
            Assert.AreEqual(ProgramCatalog.Footsteps, t.Head.Def.Id);
            Assert.IsTrue(t.EncountersPending, "보장 조우(소등)는 남았다");
            f.Wait(TensionDirector.OptionalWaitSeconds + 1f);
            Assert.AreEqual(EncounterRunState.Missed, t.Runs[0].State, "복도에 없으니 지나감");
            Assert.AreEqual(ProgramCatalog.ToiletBlackout, t.Head.Def.Id);
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
    }
}

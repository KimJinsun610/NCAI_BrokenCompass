using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace NightDuty.Tests
{
    /// <summary>NextDouble이 늘 같은 값을 돌려주는 난수(헛예고·전조 길이를 고정).</summary>
    internal sealed class FixedRandom : System.Random
    {
        private readonly double _v;

        public FixedRandom(double v)
        {
            _v = v;
        }

        protected override double Sample()
        {
            return _v;
        }

        public override int Next(int maxValue)
        {
            return (int)(_v * maxValue);
        }

        public override double NextDouble()
        {
            return _v;
        }
    }

    /// <summary>디렉터 조립 — 편성을 손으로 만든다.</summary>
    internal sealed class DirectorFixture
    {
        public readonly TensionDirector Director;
        public readonly List<DirectionEvent> Events = new List<DirectionEvent>();
        public readonly List<JudgeSignal> Out = new List<JudgeSignal>();
        public float Minute = 30f;
        public int Highest = 30;

        public DirectorFixture(int day, double rng, string[] rules, params KeyValuePair<EncounterSlot, string>[] slots)
        {
            List<SlotEncounter> s = new List<SlotEncounter>();
            foreach (KeyValuePair<EncounterSlot, string> kv in slots) s.Add(new SlotEncounter(kv.Key, ProgramCatalog.Encounter(kv.Value), false));
            List<RuleDef> deck = new List<RuleDef>();
            foreach (string id in rules ?? new string[0]) deck.Add(ProgramCatalog.Rule(id));
            Director = new TensionDirector(new NightProgram(day, s, deck, "test"), day, 0, new FixedRandom(rng));
            Director.Emitted += Events.Add;
        }

        public static KeyValuePair<EncounterSlot, string> Slot(EncounterSlot slot, string id)
        {
            return new KeyValuePair<EncounterSlot, string>(slot, id);
        }

        public DirectorFixture Send(JudgeSignal s)
        {
            Director.Observe(s);
            return this;
        }

        public DirectorFixture Enter(SpaceId space)
        {
            return Send(JudgeSignal.OfSpace(SignalKind.SpaceEntered, space));
        }

        public DirectorFixture Exit(SpaceId space)
        {
            return Send(JudgeSignal.OfSpace(SignalKind.SpaceExited, space));
        }

        public DirectorFixture Pose(float x, float z, float yaw = 0f)
        {
            return Send(JudgeSignal.Pose(new Vector3(x, 0f, z), yaw));
        }

        /// <summary>0.1초씩 흘리고 나온 단서를 모은다.</summary>
        public DirectorFixture Wait(float seconds)
        {
            int n = Mathf.RoundToInt(seconds * 10f);
            for (int i = 0; i < n; i++)
            {
                Director.Tick(Minute, 0.1f, Highest, Band.Band0, false);
                JudgeSignal s;
                while (Director.TryDequeue(out s)) Out.Add(s);
            }

            return this;
        }

        public bool HasPhase(string source, DirectionPhase phase)
        {
            foreach (DirectionEvent e in Events)
            {
                if (e.SourceId == source && e.Phase == phase) return true;
            }

            return false;
        }

        public int CueCount(SignalKind kind, string cue)
        {
            int n = 0;
            foreach (JudgeSignal s in Out)
            {
                string id = s.TargetId;
                int at = id.IndexOf('@');
                if (at >= 0) id = id.Substring(0, at);
                if (s.Kind == kind && id == cue) n++;
            }

            return n;
        }

        public EncounterRun Run(string id)
        {
            foreach (EncounterRun r in Director.Runs)
            {
                if (r.Def.Id == id) return r;
            }

            return null;
        }
    }

    /// <summary>대본 표·예산·강도 단계.</summary>
    public sealed class DirectionTableTests
    {
        [Test]
        public void 조우_15개_모두_대본이_있다()
        {
            foreach (EncounterDef e in ProgramCatalog.AllEncounters)
            {
                Assert.IsNotNull(EncounterScripts.Find(e.Id), e.Id);
            }
        }

        [Test]
        public void 대본의_공간은_조우_공간과_같다()
        {
            foreach (EncounterDef e in ProgramCatalog.AllEncounters)
            {
                EncounterScript s = EncounterScripts.Find(e.Id);
                if (e.Id == ProgramCatalog.CctvPerson) continue;
                if (e.Id == ProgramCatalog.HallEndFigure)
                {
                    Assert.AreEqual(SpaceId.Corridor, s.Space, "70차: 복도 끝 모형은 복도에서 마주친다(수칙 S5는 과학실 카드)");
                    continue;
                }

                Assert.AreEqual(SpaceIds.Canonical(e.Space), s.Space, e.Id);
            }
        }

        [Test]
        public void 수칙_단서_대본은_혼자_서는_수칙만()
        {
            foreach (RuleTriggerScript s in RuleTriggers.All)
            {
                RuleDef r = ProgramCatalog.Rule(s.RuleId);
                Assert.IsNotNull(r, s.RuleId);
                Assert.IsTrue(r.IsStandalone, s.RuleId + "는 조우에 묶인 수칙이라 디렉터가 조우로 울린다");
            }
        }

        [Test]
        public void 놀람_예산_일차별_상한()
        {
            SurpriseBudget d1 = new SurpriseBudget(1);
            Assert.AreEqual(3, d1.Cap3);
            Assert.AreEqual(1, d1.Cap4);
            Assert.AreEqual(0, d1.Cap5(true));
            SurpriseBudget d3 = new SurpriseBudget(3);
            Assert.AreEqual(4, d3.Cap3);
            Assert.AreEqual(2, d3.Cap4);
            Assert.AreEqual(0, d3.Cap5(false));
            Assert.AreEqual(1, d3.Cap5(true));
            Assert.AreEqual(1, new SurpriseBudget(5).Cap5(false));
        }

        [Test]
        public void 놀람_예산_간격과_휴지()
        {
            SurpriseBudget b = new SurpriseBudget(3);
            string why;
            Assert.IsTrue(b.CanStart(3, false, 0f, out why));
            b.Commit(3, 0f);
            Assert.IsFalse(b.CanStart(3, false, 59f, out why));   // 59차: 밤 10분 — 간격 90 → 60, 120 → 80
            Assert.IsTrue(b.CanStart(2, false, 10f, out why), "강도 2는 간격에 걸리지 않는다");
            Assert.IsTrue(b.CanStart(3, false, 61f, out why));
            b.Commit(4, 100f);
            Assert.IsFalse(b.CanStart(3, false, 170f, out why), "강도 4 뒤 80초");
            Assert.IsTrue(b.CanStart(3, false, 181f, out why));
            b.EndEncounter(230f, 60f);
            Assert.IsFalse(b.CanStart(1, false, 280f, out why), "조우 뒤 휴지는 강도와 무관");
            Assert.IsTrue(b.CanStart(1, false, 291f, out why));
        }

        [Test]
        public void 강도_단계는_생존_수치와_재시작으로()
        {
            Assert.AreEqual(DirectorMood.Easy, DirectorMoods.Of(49, 0));
            Assert.AreEqual(DirectorMood.Normal, DirectorMoods.Of(50, 1));
            Assert.AreEqual(DirectorMood.Danger, DirectorMoods.Of(75, 0));
            Assert.AreEqual(DirectorMood.Danger, DirectorMoods.Of(10, 2));
            Assert.AreEqual(0.8f, DirectorMoods.WindowScale(DirectorMood.Easy));
            Assert.AreEqual(1.3f, DirectorMoods.WindowScale(DirectorMood.Danger));
        }
    }

    /// <summary>디렉터의 조우 진행.</summary>
    public sealed class TensionDirectorTests
    {
        [Test]
        public void 슬롯_시간창_밖에서는_조우가_없다()
        {
            DirectorFixture f = new DirectorFixture(1, 0.99, null, DirectorFixture.Slot(EncounterSlot.A, ProgramCatalog.ToiletGirl));
            f.Minute = 40f;
            f.Enter(SpaceId.Toilet).Wait(5f);
            Assert.AreEqual(0, f.CueCount(SignalKind.CueStarted, FinalCues.GirlStall));
            Assert.AreEqual(EncounterRunState.Waiting, f.Run(ProgramCatalog.ToiletGirl).State);
        }

        [Test]
        public void 강도2는_전조_없이_대면하고_창이_닫히면_끝_단서()
        {
            DirectorFixture f = new DirectorFixture(1, 0.99, null, DirectorFixture.Slot(EncounterSlot.A, ProgramCatalog.ToiletGirl));
            f.Minute = NightClock.Call1 + 10f;
            f.Enter(SpaceId.Toilet).Wait(0.1f);
            Assert.IsTrue(f.HasPhase(ProgramCatalog.ToiletGirl, DirectionPhase.Confront));
            Assert.IsFalse(f.HasPhase(ProgramCatalog.ToiletGirl, DirectionPhase.Foreshadow));
            Assert.AreEqual(1, f.CueCount(SignalKind.CueStarted, FinalCues.GirlStall));

            f.Wait(6f * 0.8f + 0.2f);   // 생존 30 = 쉬움 → 창 −20%(51차: 창 4 → 6초, 걸음 끝까지)
            Assert.AreEqual(1, f.CueCount(SignalKind.SequenceEnded, FinalCues.GirlStall));
            f.Wait(0.2f);
            Assert.AreEqual(EncounterRunState.Done, f.Run(ProgramCatalog.ToiletGirl).State);
            Assert.IsTrue(f.HasPhase(ProgramCatalog.ToiletGirl, DirectionPhase.Result));
        }

        [Test]
        public void 발소리는_복도_10m를_걸으면_전조_뒤_뒤쪽에서_온다()
        {
            DirectorFixture f = new DirectorFixture(1, 0.99, null, DirectorFixture.Slot(EncounterSlot.A, ProgramCatalog.Footsteps));
            f.Minute = NightClock.Call1 + 10f;
            f.Enter(SpaceId.Corridor).Pose(10f, 43f, 90f).Wait(0.1f);
            Assert.IsFalse(f.HasPhase(ProgramCatalog.Footsteps, DirectionPhase.Foreshadow), "아직 안 걸었다");
            for (int i = 1; i <= 11; i++) f.Pose(10f + i, 43f, 90f);
            f.Wait(0.1f);
            Assert.IsTrue(f.HasPhase(ProgramCatalog.Footsteps, DirectionPhase.Foreshadow));
            Assert.AreEqual(0, f.CueCount(SignalKind.CueStarted, FinalCues.Footsteps), "전조 중에는 단서가 없다");

            f.Wait(8f);   // 전조 1 + 0.99×7 ≈ 7.9초
            Assert.AreEqual(1, f.CueCount(SignalKind.CueStarted, FinalCues.Footsteps));
            JudgeSignal cue = f.Out.Find(s => s.Kind == SignalKind.CueStarted);
            Assert.Less(cue.Point.x, 21f - 10f, "발소리는 플레이어 뒤(−X)에서 난다");
        }

        [Test]
        public void 헛예고는_단서를_보내지_않고_다시_기다린다()
        {
            DirectorFixture f = new DirectorFixture(1, 0.0, null, DirectorFixture.Slot(EncounterSlot.A, ProgramCatalog.Footsteps));
            f.Minute = NightClock.Call1 + 10f;
            f.Enter(SpaceId.Corridor).Pose(0f, 43f, 90f);
            for (int i = 1; i <= 11; i++) f.Pose(i, 43f, 90f);
            f.Wait(3f);
            Assert.IsTrue(f.HasPhase(ProgramCatalog.Footsteps, DirectionPhase.FalseForeshadow));
            Assert.AreEqual(0, f.CueCount(SignalKind.CueStarted, FinalCues.Footsteps));
            Assert.AreEqual(EncounterRunState.Waiting, f.Run(ProgramCatalog.Footsteps).State);
        }

        [Test]
        public void 헛예고는_조우마다_한_번까지_다음은_진짜다()
        {
            DirectorFixture f = new DirectorFixture(1, 0.0, null, DirectorFixture.Slot(EncounterSlot.A, ProgramCatalog.Footsteps));
            f.Minute = NightClock.Call1 + 10f;
            f.Enter(SpaceId.Corridor).Pose(0f, 43f, 90f);
            for (int i = 1; i <= 11; i++) f.Pose(i, 43f, 90f);
            f.Wait(30f);   // 헛예고 + 재시도 대기(FalseRetry 20초)
            for (int i = 1; i <= 11; i++) f.Pose(11f + i, 43f, 90f);
            f.Wait(0.5f);
            int falseCount = 0;
            foreach (DirectionEvent e in f.Events) if (e.SourceId == ProgramCatalog.Footsteps && e.Phase == DirectionPhase.FalseForeshadow) falseCount++;
            Assert.AreEqual(1, falseCount, "rng가 늘 0이어도 헛예고는 한 번뿐");
            Assert.IsTrue(f.HasPhase(ProgramCatalog.Footsteps, DirectionPhase.Foreshadow), "두 번째는 진짜 전조");
        }

        [Test]
        public void 슬롯이_끝날_때까지_방아쇠가_없으면_판정_끝까지_넘기고_그래도_없으면_놓침()
        {
            // 57차(민: 2일차 수칙은 나왔는데 조우를 못 만났다): 슬롯 A·B에서 못 걸린 조우는 03:30까지 넘기고, 끝내 없으면 Missed를 알린다(수칙을 거두는 신호).
            DirectorFixture f = new DirectorFixture(1, 0.99, null, DirectorFixture.Slot(EncounterSlot.A, ProgramCatalog.ToiletGirl));
            List<string> missed = new List<string>();
            f.Director.Missed += missed.Add;
            f.Minute = NightClock.Call1 + 10f;
            f.Enter(SpaceId.Library).Wait(1f);
            f.Minute = NightClock.RelaxStart;
            f.Wait(0.1f);
            EncounterRun run = f.Run(ProgramCatalog.ToiletGirl);
            Assert.AreEqual(EncounterRunState.Waiting, run.State, "슬롯 A가 끝나도 아직 기다린다");
            Assert.IsTrue(run.CarriedOver);
            Assert.AreEqual(0, missed.Count);

            f.Minute = NightClock.JudgingEnd;
            f.Wait(0.1f);
            Assert.AreEqual(EncounterRunState.Missed, run.State);
            CollectionAssert.AreEqual(new[] { ProgramCatalog.ToiletGirl }, missed);
        }

        [Test]
        public void 넘긴_조우는_다른_슬롯_시간에도_그_공간에_들어가면_헛예고_없이_시작한다()
        {
            DirectorFixture f = new DirectorFixture(1, 0.0, null, DirectorFixture.Slot(EncounterSlot.A, ProgramCatalog.ToiletBlackout));
            f.Minute = NightClock.Call1 + 10f;
            f.Enter(SpaceId.Library).Wait(1f);
            f.Minute = NightClock.RelaxStart + 1f;
            f.Wait(0.1f);
            Assert.IsTrue(f.Run(ProgramCatalog.ToiletBlackout).CarriedOver);

            f.Minute = NightClock.Call2 + 5f;
            f.Exit(SpaceId.Library).Enter(SpaceId.Toilet).Wait(0.5f);
            Assert.IsFalse(f.HasPhase(ProgramCatalog.ToiletBlackout, DirectionPhase.FalseForeshadow), "넘긴 조우는 헛예고로 기회를 쓰지 않는다(rng 0이어도)");
            Assert.IsTrue(f.HasPhase(ProgramCatalog.ToiletBlackout, DirectionPhase.Foreshadow), "들어가기만 해도(머무름 없이) 시작");
        }

        [Test]
        public void 슬롯_끝_10분_전에는_머무름_없이_그_공간에_있기만_하면()
        {
            DirectorFixture f = new DirectorFixture(1, 0.99, null, DirectorFixture.Slot(EncounterSlot.A, ProgramCatalog.ToiletBlackout));
            f.Minute = NightClock.RelaxStart - 5f;
            f.Enter(SpaceId.Toilet).Wait(0.5f);
            Assert.IsTrue(f.HasPhase(ProgramCatalog.ToiletBlackout, DirectionPhase.Foreshadow));
        }

        [Test]
        public void 위험_단계면_슬롯_C를_끈다()
        {
            DirectorFixture f = new DirectorFixture(3, 0.99, null, DirectorFixture.Slot(EncounterSlot.C, ProgramCatalog.ToiletGirl));
            f.Highest = 80;
            f.Minute = NightClock.SlotCStart + 7f;
            f.Enter(SpaceId.Toilet).Wait(1f);
            Assert.AreEqual(0, f.CueCount(SignalKind.CueStarted, FinalCues.GirlStall));
            f.Highest = 60;
            f.Wait(0.2f);
            Assert.AreEqual(1, f.CueCount(SignalKind.CueStarted, FinalCues.GirlStall));
        }

        [Test]
        public void 존재형은_슬롯이_끝날_때까지_머문다()
        {
            DirectorFixture f = new DirectorFixture(2, 0.99, null, DirectorFixture.Slot(EncounterSlot.A, ProgramCatalog.PeopleTree));
            f.Minute = NightClock.Call1 + 10f;
            f.Enter(SpaceId.Corridor).Pose(0f, 43f).Wait(8.1f);
            Assert.IsTrue(f.HasPhase(ProgramCatalog.PeopleTree, DirectionPhase.Confront), "강도 3이라 전조 뒤 대면");
            f.Wait(60f);
            Assert.AreEqual(EncounterRunState.Active, f.Run(ProgramCatalog.PeopleTree).State, "슬롯 끝(01:52)까지 머문다");
            Assert.IsFalse(f.Director.Busy, "존재형의 머무름은 다른 연출을 막지 않는다");
            f.Wait(100f);
            Assert.AreNotEqual(EncounterRunState.Active, f.Run(ProgramCatalog.PeopleTree).State);
        }

        [Test]
        public void 재시작하면_시작_분_뒤에_일어난_조우는_다시_기다린다()
        {
            DirectorFixture f = new DirectorFixture(1, 0.99, null, DirectorFixture.Slot(EncounterSlot.A, ProgramCatalog.ToiletGirl));
            f.Minute = NightClock.Call1 + 10f;
            f.Enter(SpaceId.Toilet).Wait(7f);
            Assert.AreEqual(EncounterRunState.Done, f.Run(ProgramCatalog.ToiletGirl).State);

            f.Director.ResetToRest(1, NightClock.Call2);
            Assert.AreEqual(EncounterRunState.Done, f.Run(ProgramCatalog.ToiletGirl).State, "체크포인트(02:16) 전에 끝난 조우는 그대로");

            f.Director.ResetToRest(2, 0);
            Assert.AreEqual(EncounterRunState.Waiting, f.Run(ProgramCatalog.ToiletGirl).State, "밤 시작 재시작은 다시 기다린다");
        }

        [Test]
        public void 붙잡히면_끝_단서_없이_중단()
        {
            DirectorFixture f = new DirectorFixture(1, 0.99, null, DirectorFixture.Slot(EncounterSlot.A, ProgramCatalog.ToiletGirl));
            f.Minute = NightClock.Call1 + 10f;
            f.Enter(SpaceId.Toilet).Wait(0.1f);
            f.Director.Tick(NightClock.Call1 + 10f, 0.1f, 30, Band.Band0, true);
            JudgeSignal s;
            Assert.IsFalse(f.Director.TryDequeue(out s));
            Assert.IsTrue(f.HasPhase(ProgramCatalog.ToiletGirl, DirectionPhase.Aborted));
            Assert.AreEqual(0, f.CueCount(SignalKind.SequenceEnded, FinalCues.GirlStall));
        }

        [Test]
        public void 디버그_강제_실행은_슬롯과_예산을_건너뛴다()
        {
            DirectorFixture f = new DirectorFixture(1, 0.0, null);
            f.Minute = 10f;   // 출근 구간
            Assert.IsTrue(f.Director.ForceEncounter(ProgramCatalog.ModelRush));
            f.Wait(2.2f);
            Assert.AreEqual(1, f.CueCount(SignalKind.CueStarted, FinalCues.HallEnd));
            Assert.IsFalse(f.HasPhase(ProgramCatalog.ModelRush, DirectionPhase.FalseForeshadow));
            f.Director.SkipPhase();
            f.Wait(0.1f);
            Assert.AreEqual(1, f.CueCount(SignalKind.SequenceEnded, FinalCues.HallEnd));
        }
    }

    /// <summary>디렉터의 수칙 단서·가짜 놀람.</summary>
    public sealed class DirectorRuleTriggerTests
    {
        [Test]
        public void 물_내림은_화장실에_머물면_한_번_울리고_8초_뒤_끝난다()
        {
            DirectorFixture f = new DirectorFixture(1, 0.0, new[] { "T1" });
            f.Minute = 30f;
            f.Enter(SpaceId.Toilet).Wait(3.2f);
            Assert.AreEqual(1, f.CueCount(SignalKind.CueStarted, FinalCues.Flush));
            f.Wait(8.1f);
            Assert.AreEqual(1, f.CueCount(SignalKind.SequenceEnded, FinalCues.Flush));
            f.Exit(SpaceId.Toilet).Enter(SpaceId.Toilet).Wait(30f);
            Assert.AreEqual(1, f.CueCount(SignalKind.CueStarted, FinalCues.Flush), "밤에 한 번");
        }

        [Test]
        public void 이완_구간에는_수칙_단서가_없다()
        {
            DirectorFixture f = new DirectorFixture(1, 0.0, new[] { "T1" });
            f.Minute = NightClock.RelaxStart + 8f;
            f.Enter(SpaceId.Toilet).Wait(10f);
            Assert.AreEqual(0, f.CueCount(SignalKind.CueStarted, FinalCues.Flush));
        }

        [Test]
        public void 불켜진_칸은_화장실을_나갈_때_끝난다()
        {
            DirectorFixture f = new DirectorFixture(1, 0.0, new[] { "T5" });
            f.Minute = 30f;
            f.Enter(SpaceId.Toilet).Wait(5f);
            Assert.AreEqual(1, f.CueCount(SignalKind.CueStarted, FinalCues.StallLit));
            f.Wait(30f);
            Assert.AreEqual(0, f.CueCount(SignalKind.SequenceEnded, FinalCues.StallLit));
            f.Exit(SpaceId.Toilet);
            JudgeSignal s;
            while (f.Director.TryDequeue(out s)) f.Out.Add(s);
            Assert.AreEqual(1, f.CueCount(SignalKind.SequenceEnded, FinalCues.StallLit));
        }

        [Test]
        public void 분필은_교실_문_밖_구역에서()
        {
            DirectorFixture f = new DirectorFixture(1, 0.0, new[] { "C1" });
            f.Minute = 30f;
            f.Enter(SpaceId.Corridor).Wait(5f);
            Assert.AreEqual(0, f.CueCount(SignalKind.CueStarted, FinalCues.Chalk));
            f.Send(JudgeSignal.Target(SignalKind.ZoneEntered, "cls13.door.outside")).Wait(1f);   // 1-1 미사용 → 1-3 출입구 앞
            Assert.AreEqual(1, f.CueCount(SignalKind.CueStarted, FinalCues.Chalk));
        }

        [Test]
        public void 빈_방은_지금_보는_채널이_아닌_다른_채널()
        {
            DirectorFixture f = new DirectorFixture(1, 0.0, new[] { "K2" });
            f.Minute = 30f;
            f.Enter(SpaceId.SecurityRoom);
            for (int i = 0; i < 30; i++)
            {
                f.Send(JudgeSignal.CctvView("cctv.ch0", 0.1f)).Wait(0.1f);
            }

            JudgeSignal cue = f.Out.Find(s => s.Kind == SignalKind.CueStarted);
            StringAssert.StartsWith(FinalCues.EmptyRoom + "@cctv.ch", cue.TargetId);
            Assert.AreNotEqual(FinalCues.EmptyRoom + "@cctv.ch0", cue.TargetId);
        }

        [Test]
        public void 조우_중에는_수칙_단서를_쉰다()
        {
            DirectorFixture f = new DirectorFixture(1, 0.99, new[] { "T1" }, DirectorFixture.Slot(EncounterSlot.A, ProgramCatalog.ToiletBlackout));
            f.Minute = NightClock.Call1 + 10f;
            f.Enter(SpaceId.Toilet).Wait(4.2f);
            Assert.IsTrue(f.Director.Busy);
            int before = f.CueCount(SignalKind.CueStarted, FinalCues.Flush);
            f.Wait(5f);
            Assert.AreEqual(before, f.CueCount(SignalKind.CueStarted, FinalCues.Flush));
        }

        [Test]
        public void 가짜_놀람은_같은_것을_밤에_두_번까지()
        {
            DirectorFixture f = new DirectorFixture(2, 0.0, null);
            f.Minute = 30f;
            f.Wait(600f);
            Dictionary<string, int> n = new Dictionary<string, int>();
            foreach (DirectionEvent e in f.Events)
            {
                if (e.Kind != DirectionEventKind.FakeScare) continue;
                int c;
                n.TryGetValue(e.SourceId, out c);
                n[e.SourceId] = c + 1;
            }

            Assert.Greater(n.Count, 0);
            foreach (KeyValuePair<string, int> kv in n) Assert.LessOrEqual(kv.Value, TensionDirector.FakePerId, kv.Key);
            Assert.LessOrEqual(f.Director.FakesUsed, TensionPacer.FakeCap(2) + DirectorMoods.ExtraFakes(DirectorMood.Easy), "2일차 상한 4(+쉬움 1)까지");
        }

        [Test]
        public void 출근_동안은_조용하고_판정이_열리면_곧_첫_가짜_놀람()
        {
            DirectorFixture arrival = new DirectorFixture(2, 0.5, null);
            arrival.Minute = NightClock.JudgingStart - 6f;
            arrival.Wait(200f);
            Assert.AreEqual(0, arrival.Director.FakesUsed, "출근(00:16 전)에는 가짜 놀람이 없다");

            DirectorFixture early = new DirectorFixture(2, 0.0, null);
            early.Minute = NightClock.JudgingStart + 1f;
            early.Wait(NightClock.RealSecondsAt(NightClock.JudgingStart) + TensionDirector.FirstFakeMin - 1f);
            Assert.AreEqual(0, early.Director.FakesUsed, "50차: 판정이 열리자마자 놀래지 않는다(25초 전에는 없음)");

            DirectorFixture open = new DirectorFixture(2, 0.99, null);
            open.Minute = NightClock.JudgingStart + 1f;
            open.Wait(NightClock.RealSecondsAt(NightClock.JudgingStart) + TensionDirector.FirstFakeMax + 0.5f);
            Assert.AreEqual(1, open.Director.FakesUsed, "판정이 열린 뒤 50초 안에 첫 가짜 놀람");
        }

        [Test]
        public void 일차1은_첫_조우_결과_전에는_가짜_놀람이_없다()
        {
            DirectorFixture f = new DirectorFixture(1, 0.0, null);
            f.Minute = 30f;
            f.Wait(600f);
            Assert.AreEqual(0, f.Director.FakesUsed, "1일차 01:00 전");

            DirectorFixture a = new DirectorFixture(1, 0.0, null, DirectorFixture.Slot(EncounterSlot.A, ProgramCatalog.BoySeated));
            a.Minute = NightClock.Call1 + 5f;
            a.Wait(300f);
            Assert.AreEqual(0, a.Director.FakesUsed, "슬롯 A 조우가 있으면 그 결과(또는 호출 2) 전");

            f.Minute = NightClock.Call1 + 1f;
            f.Wait(200f);
            Assert.Greater(f.Director.FakesUsed, 0, "슬롯 A 조우가 없는 1일차는 01:00 뒤에 낸다");
            Assert.LessOrEqual(f.Director.FakesUsed, TensionPacer.FakeCap(1) + DirectorMoods.ExtraFakes(DirectorMood.Easy));
        }

        [Test]
        public void 긴장_조절기가_뜨거우면_가짜_놀람을_쉰다()
        {
            DirectorFixture f = new DirectorFixture(2, 0.0, null);   // 첫 가짜는 40 + 17 = 57초부터(59차: 밤 10분)
            f.Minute = 30f;
            f.Wait(56f);
            Assert.AreEqual(0, f.Director.FakesUsed);
            f.Director.Pacer.Impulse(PacerImpulse.Startle);
            f.Director.Pacer.Impulse(PacerImpulse.Startle);
            f.Director.Pacer.Impulse(PacerImpulse.Startle);
            f.Director.Pacer.Impulse(PacerImpulse.Startle);
            f.Director.Pacer.Impulse(PacerImpulse.Startle);
            Assert.AreEqual(75f, f.Director.Pacer.Intensity, 0.01f);
            f.Wait(5f);
            Assert.AreEqual(PacerState.Peak, f.Director.Pacer.State);
            f.Wait(40f);   // 101초 — 첫 가짜 차례(57초)가 지났지만 해소·휴식 중
            Assert.AreEqual(0, f.Director.FakesUsed, "절정 → 해소 → 휴식 동안은 없다");
            f.Wait(60f);   // 휴식 최소 33초 + 긴장도 25 아래 → 축적
            Assert.GreaterOrEqual(f.Director.FakesUsed, 1, "휴식이 끝나면 미뤄 둔 가짜 놀람(59차: 간격 30~75초라 60초 안에 둘째가 올 수도 있다)");
        }

        [Test]
        public void 벌레_떼는_창고가_있는_1_3_교실_화장실_도서관에서만()
        {
            TensionDirector.FakeBugsEnabled = true;   // 61차부터 꺼져 있다 — 켰을 때의 자리 규칙만 본다
            try
            {
                BugRooms();
            }
            finally
            {
                TensionDirector.FakeBugsEnabled = false;
            }
        }

        [Test]
        public void 벌레_떼는_가짜_놀람으로_나오지_않는다()
        {
            Assert.IsFalse(TensionDirector.FakeBugsEnabled);
            foreach (SpaceId room in TensionDirector.BugSpaces)
            {
                DirectorFixture inside = new DirectorFixture(2, 0.99, null);
                inside.Minute = 30f;
                inside.Enter(room).Wait(600f);
                Assert.AreEqual(0, Count(inside, TensionDirector.FakeBugs), room + " — 바퀴벌레는 시체 낙하와만");
                Assert.Greater(inside.Director.FakesUsed, 0, "다른 가짜 놀람은 그대로");
            }
        }

        private static void BugRooms()
        {
            Assert.Contains(TensionDirector.FakeBugs, TensionDirector.FakeScares);
            CollectionAssert.AreEquivalent(new[] { SpaceId.Classroom_1_3, SpaceId.Toilet, SpaceId.Library }, TensionDirector.BugSpaces);

            DirectorFixture outside = new DirectorFixture(2, 0.99, null);   // 0.99 = 열린 목록의 마지막(벌레 떼)을 고른다
            outside.Minute = 30f;
            outside.Enter(SpaceId.Corridor).Wait(600f);
            Assert.AreEqual(0, Count(outside, TensionDirector.FakeBugs), "복도에서는 벌레 떼가 없다");
            Assert.Greater(outside.Director.FakesUsed, 0, "다른 가짜 놀람은 그대로");

            foreach (SpaceId room in TensionDirector.BugSpaces)
            {
                DirectorFixture inside = new DirectorFixture(2, 0.99, null);
                inside.Minute = 30f;
                inside.Enter(room).Wait(600f);
                int bugs = Count(inside, TensionDirector.FakeBugs);
                Assert.Greater(bugs, 0, room.ToString());
                Assert.LessOrEqual(bugs, TensionDirector.FakePerId, room.ToString());
            }
        }

        [Test]
        public void 디버그_가짜_놀람은_횟수를_쓰지_않는다()
        {
            DirectorFixture f = new DirectorFixture(1, 0.0, null);
            Assert.IsTrue(f.Director.ForceFake(TensionDirector.FakeBugs));
            Assert.IsFalse(f.Director.ForceFake("fake.none"));
            Assert.AreEqual(0, f.Director.FakesUsed);
        }

        private static int Count(DirectorFixture f, string fakeId)
        {
            int n = 0;
            foreach (DirectionEvent e in f.Events)
            {
                if (e.Kind == DirectionEventKind.FakeScare && e.SourceId == fakeId) n++;
            }

            return n;
        }
    }

    /// <summary>NightRun 연결 — 디렉터 단서가 실제 판정까지.</summary>
    public sealed class NightRunDirectionTests
    {
        private int _minute = 30;

        [TearDown]
        public void TearDown()
        {
            NightRun.ProgramEnabled = false;
            NightRun.StartNewRun();
            EventBus.ClearAll();
        }

        [Test]
        public void 소년_착석_단서가_C3_판정까지_간다()
        {
            NightRun.StartNewRun();
            NightRun.ProgramEnabled = true;
            _minute = 70;
            NightRun.BeginNight(1, () => _minute);
            Assert.IsNotNull(NightRun.Tension);
            Assert.IsFalse(NightRun.Program.HasEncounter(ProgramCatalog.BoySeated), "1일차엔 몹 없음");
            Assert.IsTrue(NightRun.DebugAddFinalRule("C3"));

            List<DirectionEvent> seen = new List<DirectionEvent>();
            EventBus.DirectionEmitted += seen.Add;

            NightRun.Send(JudgeSignal.OfSpace(SignalKind.SpaceEntered, SpaceId.Classroom_1_3));
            NightRun.Send(JudgeSignal.Pose(new Vector3(44f, 1.5f, 36f), 0f));
            Assert.IsTrue(NightRun.DebugForceEncounter(ProgramCatalog.BoySeated));
            for (int i = 0; i < 35; i++) NightRun.Tick(0.1f);

            Assert.IsTrue(seen.Exists(e => e.SourceId == ProgramCatalog.BoySeated && e.Phase == DirectionPhase.Confront));
            Assert.IsTrue(NightRun.FinalRules.Judge("C3").Triggered, "대면 단서가 판정 책까지 갔다");

            NightRun.Send(JudgeSignal.Pose(new Vector3(45f, 1.5f, 36f), 0f));
            Assert.AreEqual(Deltas.ThreatFailure, NightRun.Axes.GetValue(FearAxis.Auditory));
        }

        [Test]
        public void 디버그로_조우와_수칙_단서를_바로_울린다()
        {
            NightRun.StartNewRun();
            NightRun.ProgramEnabled = true;
            _minute = 30;
            NightRun.BeginNight(1, () => _minute);

            Assert.IsTrue(NightRun.DebugAddFinalRule("T1"));
            Assert.IsTrue(NightRun.DebugFireRuleCue("T1"));
            NightRun.Send(JudgeSignal.OfSpace(SignalKind.SpaceEntered, SpaceId.Toilet));
            Assert.IsTrue(NightRun.DebugFireRuleCue("T1"));
            for (int i = 0; i < 82; i++) NightRun.Tick(0.1f);
            Assert.AreEqual(Deltas.RuleViolation, NightRun.Axes.GetValue(FearAxis.Auditory), "8초 안에 나오지 않음");

            Assert.IsTrue(NightRun.DebugForceEncounter(ProgramCatalog.Footsteps));
            Assert.IsFalse(NightRun.DebugForceEncounter("E.None"));
        }
    }

    public class DayOneAndBindingTests
    {
        [Test]
        public void 일차1_복도는_H2_문_자동열림_단서다()
        {
            RuleTriggerScript h2 = RuleTriggers.Find("H2");
            Assert.IsNotNull(h2, "H2는 혼자 서는 수칙 — 단서 대본이 있어야 1일차 복도가 산다");
            Assert.AreEqual(SpaceId.Corridor, h2.Space);
            Assert.AreEqual("cue.door.autoopen", h2.Cue);
        }

        [Test]
        public void 머리박기_조우는_앉은_소년_하나로_C2_C3를_묶는다()
        {
            // 52차 민: C2 「교실의 _? 는 무시하십시오.」 + C3 + 앉은 소년 — 둘째 대역(천장 다리·시체)은 없다.
            EncounterScript bang = EncounterScripts.Find(ProgramCatalog.BoyBang);
            Assert.IsNotNull(bang);
            Assert.AreEqual(FinalCues.BoySeated, bang.Cue);
            Assert.AreEqual(FinalCues.BoyTarget, bang.AnchorId, "C2 응시 기준점 = 소년");
            Assert.IsTrue(string.IsNullOrEmpty(bang.ExtraStandIn));
            Assert.AreEqual(ProgramCatalog.BoyBang, ProgramCatalog.Rule("C2").BoundEncounter);
            EncounterDef e = ProgramCatalog.Encounter(ProgramCatalog.BoyBang);
            CollectionAssert.AreEquivalent(new[] { "C2", "C3" }, new[] { e.ResponseRule, e.SecondRule });
        }

        [Test]
        public void H1_C2는_조우에_묶여_있다()
        {
            Assert.IsFalse(ProgramCatalog.Rule("H1").IsStandalone, "인체나무 없이 H1 금지");
            Assert.IsFalse(ProgramCatalog.Rule("C2").IsStandalone, "천장 다리 없이 C2 금지");
            Assert.IsTrue(ProgramCatalog.Rule("H2").IsStandalone);
        }
    }

    public class StageAnchorTests
    {
        [TearDown]
        public void TearDown()
        {
            StagePoints.Clear();
            NightRun.ProgramEnabled = false;
        }

        [Test]
        public void 고정_자리가_있으면_대면_점이_그_자리다()
        {
            StagePoints.Set(StageAnchors.BoySeat, new Vector3(46.3f, 1.5f, 36.3f));
            NightRun.StartNewRun();
            NightRun.ProgramEnabled = true;
            NightRun.BeginNight(1, () => 70);

            List<DirectionEvent> seen = new List<DirectionEvent>();
            EventBus.DirectionEmitted += seen.Add;
            try
            {
                NightRun.Send(JudgeSignal.Pose(new Vector3(40f, 1.5f, 36f), 90f));
                Assert.IsTrue(NightRun.DebugForceEncounter(ProgramCatalog.BoySeated));
                for (int i = 0; i < 30; i++) NightRun.Tick(0.1f);
            }
            finally
            {
                EventBus.DirectionEmitted -= seen.Add;
            }

            int at = seen.FindIndex(e => e.SourceId == ProgramCatalog.BoySeated && e.Phase == DirectionPhase.Confront);
            Assert.GreaterOrEqual(at, 0, "대면이 나와야 한다");
            DirectionEvent confront = seen[at];
            Assert.AreEqual(new Vector3(46.3f, 1.5f, 36.3f), confront.Point, "플레이어 앞이 아니라 씬이 정한 책상 자리");
        }

        [Test]
        public void 민_지정_자리와_방()
        {
            Assert.AreEqual(StageAnchors.BoySeat, EncounterScripts.Find(ProgramCatalog.BoySeated).StageAnchor);
            Assert.AreEqual(SpaceId.Classroom_1_3, EncounterScripts.Find(ProgramCatalog.BoySeated).ExactSpace, "소년 책상이 1-3 교실에 있으니 1-1에서는 걸리지 않는다");
            Assert.AreEqual(string.Empty, EncounterScripts.Find(ProgramCatalog.CeilingLegs).StageAnchor, "51차: 시체는 플레이어 바로 앞에 떨어진다(고정 자리 없음)");
            Assert.AreEqual(EncounterScripts.CorpseStandIn, EncounterScripts.Find(ProgramCatalog.CeilingLegs).StandIn);
            Assert.AreEqual(StageAnchors.WindowMan, EncounterScripts.Find(ProgramCatalog.SuitMan).StageAnchor);
            Assert.AreEqual("mob.windowman", EncounterScripts.Find(ProgramCatalog.SuitMan).StandIn, "창밖 남자 = DUCK 프리팹");
            Assert.AreEqual(StageAnchors.YellowDoor, EncounterScripts.Find(ProgramCatalog.YellowFace).StageAnchor, "노란 남자 둘째 연출 = 도서관 정문 앞");
            Assert.AreEqual(StageAnchors.GirlWalk, EncounterScripts.Find(ProgramCatalog.ToiletGirl).StageAnchor, "소녀는 옆으로 걸어 칸으로");
        }
    }
}

using System.Collections.Generic;
using NUnit.Framework;

namespace NightDuty.Tests
{
    /// <summary>
    /// 103차(2026-10-12) 편성 균형 — 민: 「역설/변조가 적절히 나오고, 너무 중복되지 않으며, 축이 밸런스 있게 나오고,
    /// 플레이어가 날이 갈수록 변화를 명확히 인지할 수 있도록, 그리고 모든 조우 연출을 목격할 수 있도록」.
    /// </summary>
    public sealed class OneHundredThirdBalanceTests
    {
        private static readonly FearAxis[] Sensory = { FearAxis.Auditory, FearAxis.Illuminance, FearAxis.Layout };

        private static bool HasAxis(NightProgram p, FearAxis axis)
        {
            foreach (RuleDef r in p.Deck) if (r.HasAxis && r.Axis == axis) return true;
            return false;
        }

        private static NightProgram Night(ProgramDirector d, AnomalyAssigner a, int day)
        {
            IFearAxisReader shown = FixedBands.All(DayFloor.Of(day));
            return d.Build(new ProgramRequest { Day = day, Shown = shown, Survival = FixedBands.All(Band.Band1), Inspections = a.Build(day, shown) });
        }

        [Test]
        public void 일차1_덱도_감각_세_축을_모두_둔다()
        {
            for (int seed = 0; seed < 200; seed++)
            {
                NightProgram p = Night(new ProgramDirector(new System.Random(seed)), new AnomalyAssigner(new System.Random(seed + 7)), 1);
                foreach (FearAxis axis in Sensory) Assert.IsTrue(HasAxis(p, axis), axis + " — seed " + seed + ": " + p);
            }
        }

        [Test]
        public void 이에서_사일차는_감각_세_축을_모두_둔다()
        {
            for (int seed = 0; seed < 200; seed++)
            {
                ProgramDirector d = new ProgramDirector(new System.Random(seed));
                AnomalyAssigner a = new AnomalyAssigner(new System.Random(seed + 7));
                for (int day = 1; day <= 4; day++)
                {
                    NightProgram p = Night(d, a, day);
                    foreach (FearAxis axis in Sensory) Assert.IsTrue(HasAxis(p, axis), day + "일차 " + axis + " — seed " + seed + ": " + p.Report);
                }
            }
        }

        [Test]
        public void CCTV_사람은_3_4일차_하루에_겹침_조우로()
        {
            for (int seed = 0; seed < 100; seed++)
            {
                ProgramDirector d = new ProgramDirector(new System.Random(seed));
                AnomalyAssigner a = new AnomalyAssigner(new System.Random(seed + 7));
                Assert.That(d.CctvDay, Is.InRange(ProgramDirector.CctvDayFrom, ProgramDirector.CctvDayTo));
                int shown = 0;
                for (int day = 1; day <= 5; day++)
                {
                    NightProgram p = Night(d, a, day);
                    foreach (SlotEncounter s in p.Slots) Assert.AreNotEqual(ProgramCatalog.CctvPerson, s.Encounter.Id, "슬롯을 차지하지 않는다");
                    bool here = p.HasEncounter(ProgramCatalog.CctvPerson);
                    Assert.AreEqual(here, p.Has("K1"), "K1과 함께 — " + p.Report);
                    if (here) shown++;
                    Assert.AreEqual(day == d.CctvDay, here, day + "일차 — " + p.Report);
                }

                Assert.AreEqual(1, shown, "회차에 한 번");
            }
        }

        [Test]
        public void 회차에서_조우가_되풀이되지_않고_거의_다_나온다()
        {
            double distinct = 0, repeats = 0;
            const int runs = 200;
            HashSet<string> everSeen = new HashSet<string>();
            for (int seed = 0; seed < runs; seed++)
            {
                ProgramDirector d = new ProgramDirector(new System.Random(seed));
                AnomalyAssigner a = new AnomalyAssigner(new System.Random(seed + 7));
                HashSet<string> seen = new HashSet<string>();
                for (int day = 1; day <= 5; day++)
                {
                    NightProgram p = Night(d, a, day);
                    foreach (SlotEncounter s in p.Slots) if (!seen.Add(s.Encounter.Id)) repeats++;
                    foreach (EncounterDef e in p.Extras) if (!seen.Add(e.Id)) repeats++;
                }

                distinct += seen.Count;
                everSeen.UnionWith(seen);
            }

            Assert.GreaterOrEqual(distinct / runs, 12.0, "조우 15개 중 12개 이상(옛: 약 11개 · 과학실·화장실 소등은 한 번도 안 나옴)");
            Assert.Less(repeats / runs, 0.3, "같은 조우가 회차에 두 번 나오는 일은 드물다");
            Assert.IsTrue(everSeen.Contains(ProgramCatalog.ScienceBlackout) && everSeen.Contains(ProgramCatalog.ToiletBlackout), "소등 조우도 나온다");
        }

        [Test]
        public void 변조는_3일차부터_신뢰_구간_1에서도()
        {
            Assert.IsFalse(ParadoxPlanner.TamperOpen(2, 1));
            Assert.IsTrue(ParadoxPlanner.TamperOpen(2, 2));
            Assert.IsTrue(ParadoxPlanner.TamperOpen(3, 1));
            Assert.IsFalse(ParadoxPlanner.TamperOpen(5, 0));

            ParadoxPlanner p = new ParadoxPlanner(new System.Random(3));
            List<RuleDef> deck = new List<RuleDef> { ProgramCatalog.Rule("H2"), ProgramCatalog.Rule("G1") };
            p.Plan(1, Band.Band0, deck);
            ParadoxPlan day3 = p.Plan(3, Band.Band1, deck);
            Assert.AreEqual("H2", day3.Tampered);
            Assert.AreEqual(ParadoxCatalog.Subtle(ProgramCatalog.Rule("H2").Text), day3.TamperText, "구간 1이면 미세 변조");
        }

        [Test]
        public void 변조는_회차에_이미_건_수칙을_피한다()
        {
            int repeats = 0;
            for (int seed = 0; seed < 100; seed++)
            {
                ParadoxPlanner p = new ParadoxPlanner(new System.Random(seed));
                List<RuleDef> deck = new List<RuleDef> { ProgramCatalog.Rule("H2"), ProgramCatalog.Rule("C4"), ProgramCatalog.Rule("S5") };
                p.Plan(1, Band.Band0, deck);
                string first = p.Plan(4, Band.Band2, deck).Tampered;
                string second = p.Plan(5, Band.Band2, deck).Tampered;
                if (first == second) repeats++;
            }

            Assert.Less(repeats, 25, "다른 후보가 있으면 대개 다른 수칙을 변조한다");
        }

        [Test]
        public void 지난_밤_지시는_다른_지시가_남아_있으면_다시_내지_않는다()
        {
            for (int seed = 0; seed < 50; seed++)
            {
                DutyDispatcher d = new DutyDispatcher(3, null, 5, null, new System.Random(seed));
                d.RecentlyIssued = id => id == "W17" || id == "W2";
                DutyDef pick = d.Pick();
                Assert.IsNotNull(pick);
                Assert.AreNotEqual("W17", pick.Id);
                Assert.AreNotEqual("W2", pick.Id);
            }

            DutyDispatcher all = new DutyDispatcher(3, null, 5, null, new System.Random(1));
            all.RecentlyIssued = id => true;
            Assert.IsNotNull(all.Pick(), "모두 지난 밤 지시면 그래도 낸다");
        }

        [Test]
        public void 소년_착석과_머리_박기는_한_조우로_편성한다()
        {
            Assert.IsFalse(UnavoidableCatalog.Feasible(UnavoidableCatalog.Find("C2xC-3"), 5, null), "소년을 다시 부르는 가짜 회피 불가는 3일차부터 걸지 않는다");
            Assert.AreEqual(ProgramCatalog.BoyBang, ProgramDirector.MergedInto(ProgramCatalog.BoySeated));
            Assert.AreEqual(FinalCues.BoySeated, EncounterScripts.Find(ProgramCatalog.BoyBang).Cue, "머리 박기는 앉는 데서 시작한다");
            for (int seed = 0; seed < 200; seed++)
            {
                ProgramDirector d = new ProgramDirector(new System.Random(seed));
                AnomalyAssigner a = new AnomalyAssigner(new System.Random(seed + 7));
                int boy = 0;
                for (int day = 1; day <= 5; day++)
                {
                    NightProgram p = Night(d, a, day);
                    Assert.IsFalse(p.HasEncounter(ProgramCatalog.BoySeated), "착석만 따로 나오지 않는다 — " + p.Report);
                    if (p.HasEncounter(ProgramCatalog.BoyBang)) boy++;
                }

                Assert.AreEqual(1, boy, "소년은 회차에 한 번(2일차 첫 역설 C2)");
            }

            ProgramDirector r = new ProgramDirector(new System.Random(5));
            AnomalyAssigner ra = new AnomalyAssigner(new System.Random(5));
            Night(r, ra, 1);
            r.Reserve(ProgramCatalog.BoySeated);
            NightProgram next = Night(r, ra, 2);
            Assert.IsTrue(next.HasEncounter(ProgramCatalog.BoyBang), next.Report);
            Assert.IsFalse(next.HasEncounter(ProgramCatalog.BoySeated), next.Report);
        }

        [Test]
        public void 날마다_조우_수_상한과_가벼운_여분_칸()
        {
            Assert.AreEqual(3, ProgramDirector.EncounterCap(2));
            Assert.AreEqual(3, ProgramDirector.EncounterCap(3));
            Assert.AreEqual(4, ProgramDirector.EncounterCap(4));
            for (int seed = 0; seed < 200; seed++)
            {
                ProgramDirector d = new ProgramDirector(new System.Random(seed));
                AnomalyAssigner a = new AnomalyAssigner(new System.Random(seed + 7));
                for (int day = 1; day <= 5; day++)
                {
                    NightProgram p = Night(d, a, day);
                    int counted = p.Extras.Count;
                    foreach (SlotEncounter s in p.Slots) if (s.Encounter.Id != ProgramCatalog.SuitMan) counted++;
                    Assert.LessOrEqual(counted, ProgramDirector.EncounterCap(day), day + "일차 — " + p.Report);
                    Assert.LessOrEqual(p.Slots.Count, ProgramDirector.SlotLetters, p.Report);
                    if (day == 2) Assert.IsFalse(p.HasEncounter(ProgramCatalog.SuitMan), "103차 후속: 창밖 노란 얼굴은 3일차 — 2일차가 가장 무거운 밤이 되지 않게");
                }
            }
        }

        [Test]
        public void C6_교실_24분_수칙은_실제_1분()
        {
            RuleDef c6 = ProgramCatalog.Rule("C6");
            Assert.IsNotNull(c6);
            Assert.AreEqual("교실에 24분 넘게 머물지 마십시오.", c6.Text);
            Assert.AreEqual(FearAxis.Layout, c6.Axis);
            Assert.IsTrue(c6.IsStandalone, "혼자 서는 수칙 — 채움에 쓴다");
            Assert.AreEqual(60f, ProgramCatalog.ClassroomDwellRealSeconds, 0.001f, "게임 24분 = 실제 60초");

            FinalFixture f = new FinalFixture("C6");
            f.Enter(SpaceId.Classroom).Wait(30f).Exit(SpaceId.Classroom).Enter(SpaceId.Classroom).Wait(59f);
            Assert.AreEqual(0, f.Value(FearAxis.Layout), "나갔다 들어오면 다시 센다");
            f.Wait(1.1f);
            Assert.AreEqual(Deltas.RuleViolation, f.Value(FearAxis.Layout));
        }

        [Test]
        public void 회피_불가_사슬_조우는_그날_하한으로_나올_때만_같은_쌍은_회차에_한_번()
        {
            UnavoidableDef h4 = UnavoidableCatalog.Find("H4xT1");
            Assert.IsFalse(UnavoidableCatalog.Feasible(h4, 3, null), "부르는 목소리(청각 구간 3)는 3일차 하한으로 못 나온다");
            Assert.IsFalse(UnavoidableCatalog.Feasible(h4, 4, null), "4일차에 목소리를 끌어와 사람 나무 자리를 빼앗던 것");
            Assert.IsTrue(UnavoidableCatalog.Feasible(h4, 5, null));

            ParadoxPlanner p = new ParadoxPlanner(new System.Random(2));
            Assert.AreEqual("H4xT1", p.PlanUnavoidable(5, Band.Band3, null).Id, "편성만 하고 걸지 않았으면 다시 고를 수 있다");
            Assert.AreEqual("H4xT1", p.PlanUnavoidable(5, Band.Band3, null).Id);
            p.MarkUnavoidable(h4);
            UnavoidableDef again = p.PlanUnavoidable(5, Band.Band4, null);
            Assert.IsTrue(again == null || again.Id != "H4xT1", "건 쌍은 회차에 다시 고르지 않는다");
        }

        [Test]
        public void 사일차_경비실은_K1을_다시_점검해_가짜_회피_불가를_건다()
        {
            int secDays = 0;
            for (int seed = 0; seed < 200; seed++)
            {
                ProgramDirector d = new ProgramDirector(new System.Random(seed));
                AnomalyAssigner a = new AnomalyAssigner(new System.Random(seed + 7));
                for (int day = 1; day <= 3; day++) Night(d, a, day);
                IFearAxisReader shown = FixedBands.All(DayFloor.Of(4));
                InspectionPlan plan = a.Build(4, shown);
                if (!plan.Spaces.Contains(SpaceId.SecurityRoom)) continue;
                secDays++;
                InspectionAssignment k1 = plan.Find(InspectionCatalog.FirstInspection);
                Assert.IsNotNull(k1, "4일차 경비실은 CCTV 전 채널을 다시 — seed " + seed + ": " + plan);
                Assert.IsFalse(k1.IsAnomaly);

                ParadoxPlanner pp = new ParadoxPlanner(new System.Random(seed));
                UnavoidableDef fake = pp.PlanUnavoidable(4, Band.Band3, plan, id => d.CanForce(id, 4));
                Assert.IsNotNull(fake, "4일차 신뢰 3이면 가짜 회피 불가 — seed " + seed);
                Assert.IsFalse(fake.Real);
                Assert.AreEqual(d.CctvDay == 4 ? "K1xK-1" : "K2xK-1", fake.Id, "CCTV 날은 K1, 아니면 K2(CCTV 사람은 회차에 한 번)");
                NightProgram p = d.Build(new ProgramRequest { Day = 4, Shown = shown, Survival = FixedBands.All(Band.Band1), Inspections = plan, ForcedRules = fake.Rules });
                Assert.IsTrue(UnavoidableCatalog.Placed(fake, p), p.Report);
                Assert.AreEqual(d.CctvDay == 4, p.HasEncounter(ProgramCatalog.CctvPerson), p.Report);

                pp.MarkUnavoidable(fake);
                UnavoidableDef real = pp.PlanUnavoidable(5, Band.Band4, a.Build(5, FixedBands.All(DayFloor.Of(5))), id => d.CanForce(id, 5));
                Assert.IsTrue(real == null || real.Real, "가짜를 겪은 뒤 5일차 신뢰 4면 진짜");
            }

            Assert.Greater(secDays, 150, "4일차는 거의 늘 경비실을 점검한다");
        }

        [Test]
        public void 조도_구간_3부터_일부_등만_붉어지거나_깜빡인다()
        {
            Assert.AreEqual(Band.Band3, IlluminanceTint.AccentFrom);
            Assert.AreEqual(0, IlluminanceTint.AccentCount(8, Band.Band2), "구간 2까지는 없다");
            Assert.AreEqual(2, IlluminanceTint.AccentCount(8, Band.Band3), "구간 3 — 4분의 1");
            Assert.AreEqual(3, IlluminanceTint.AccentCount(8, Band.Band4), "구간 4 — 5분의 2");
            Assert.AreEqual(1, IlluminanceTint.AccentCount(2, Band.Band3), "등이 적어도 하나는");
            Assert.AreEqual(0, IlluminanceTint.AccentCount(0, Band.Band4));
            Assert.IsFalse(IlluminanceTint.AccentRed(0), "첫째는 깜빡임");
            Assert.IsTrue(IlluminanceTint.AccentRed(1), "둘째는 붉음");
            Assert.AreEqual(IlluminanceTint.AccentRank(12345, 3), IlluminanceTint.AccentRank(12345, 3), "같은 밤에는 같은 등");
            int differ = 0;
            for (int key = 0; key < 50; key++) if (IlluminanceTint.AccentRank(key, 3) < IlluminanceTint.AccentRank(key + 1, 3) != IlluminanceTint.AccentRank(key, 4) < IlluminanceTint.AccentRank(key + 1, 4)) differ++;
            Assert.Greater(differ, 5, "밤마다 다른 등 순서");
        }

        [Test]
        public void 이일차부터_경비실_수칙은_날마다_한_장()
        {
            for (int seed = 0; seed < 200; seed++)
            {
                ProgramDirector d = new ProgramDirector(new System.Random(seed));
                AnomalyAssigner a = new AnomalyAssigner(new System.Random(seed + 7));
                for (int day = 1; day <= 5; day++)
                {
                    NightProgram p = Night(d, a, day);
                    if (day == 1) continue;
                    int security = 0;
                    foreach (RuleDef r in p.Deck) if (SpaceIds.Canonical(r.Space) == SpaceId.SecurityRoom) security++;
                    Assert.AreEqual(1, security, day + "일차 — G4보다 경비실을 먼저 빼던 것(2일차 27%) — " + p.Report);
                }
            }
        }

        [Test]
        public void 경비실_공실_채널_수칙은_조도()
        {
            Assert.AreEqual(FearAxis.Illuminance, ProgramCatalog.Rule("K2").Axis, "화면 불빛 — 경비실 수칙이 모두 청각이던 것");
            Assert.AreEqual(FearAxis.Auditory, ProgramCatalog.Rule("K3").Axis, "기획서 축 분류 규칙 그대로");
        }

        [Test]
        public void C6_머문_시간은_판정을_쉬는_동안의_드나듦을_건너_이어지지_않는다()
        {
            float limit = ProgramCatalog.ClassroomDwellRealSeconds;
            FinalFixture f = new FinalFixture("C6");
            f.Enter(SpaceId.Classroom).Wait(limit - 10f);
            f.Book.Dispatch(JudgeSignal.OfSpace(SignalKind.SpaceExited, SpaceId.Classroom), false);   // 이완 구간에 복도로 나갔다가
            f.Book.Dispatch(JudgeSignal.OfSpace(SignalKind.SpaceEntered, SpaceId.Classroom), false);  // 다시 들어옴
            f.Wait(15f);
            Assert.AreEqual(0, f.Value(FearAxis.Layout), "옛: 쉬기 전 " + (limit - 10f) + "초에 이어 붙어 10초 만에 위반");
            f.Wait(limit - 14f);
            Assert.AreEqual(Deltas.RuleViolation, f.Value(FearAxis.Layout), "다시 들어온 뒤로 " + limit + "초");
        }

        [Test]
        public void K3_판정을_쉬는_동안_머물기만_했으면_이어서_센다()
        {
            FinalFixture f = new FinalFixture("K3");
            f.Enter(SpaceId.SecurityRoom).Wait(40f);
            f.Book.Dispatch(JudgeSignal.Tick(30f), false);   // 나가지 않고 쉼
            f.Wait(5.1f);
            Assert.AreEqual(Deltas.RuleViolation, f.Value(FearAxis.Auditory), "나가지 않았으면 연속 — 판정 시간 45초");
        }
    }

    /// <summary>103차 QA 2회: 재시작·결근과 밤 동안 쌓인 조우 예약.</summary>
    public sealed class OneHundredThirdQaTests
    {
        [TearDown]
        public void TearDown()
        {
            NightRun.ProgramEnabled = false;
            NightRun.InspectionsEnabled = false;
            NightRun.StartNewRun();
            EventBus.ClearAll();
        }

        [Test]
        public void 회피_불가_재시작_뒤에는_단서가_다시_울려야_나가기_금지()
        {
            UnavoidableRun r = new UnavoidableRun(UnavoidableCatalog.Find("S2xScience"));
            System.Func<SpaceId, bool> pending = s => true;
            Assert.AreEqual(UnavoidableStep.Staged, r.Observe(JudgeSignal.Cue(FinalCues.Glass, default(UnityEngine.Vector3)), SpaceId.ScienceRoom, pending, null));
            r.ResetEpisode();   // 붙잡혀 재시작
            Assert.AreEqual(UnavoidableStep.None, r.Observe(JudgeSignal.OfSpace(SignalKind.SpaceExited, SpaceId.ScienceRoom), SpaceId.ScienceRoom, pending, null), "옛: 단서 없이 잠깐 나가기만 해도 금지");
            Assert.AreEqual(SpaceId.None, r.Banned);
            r.Observe(JudgeSignal.Cue(FinalCues.Glass, default(UnityEngine.Vector3)), SpaceId.ScienceRoom, pending, null);
            Assert.AreEqual(UnavoidableStep.Banned, r.Observe(JudgeSignal.OfSpace(SignalKind.SpaceExited, SpaceId.ScienceRoom), SpaceId.ScienceRoom, pending, null), "단서가 다시 울린 뒤 나가면 금지");
        }

        [Test]
        public void 회피_불가_체크포인트_전에_울린_단서는_재시작_뒤에도_남는다()
        {
            UnavoidableRun r = new UnavoidableRun(UnavoidableCatalog.Find("S2xScience"));
            System.Func<SpaceId, bool> pending = s => true;
            r.Observe(JudgeSignal.Cue(FinalCues.Glass, default(UnityEngine.Vector3)), SpaceId.ScienceRoom, pending, null);
            Assert.IsTrue(r.ExitArmed);
            r.ResetEpisode(true);   // 단서가 02:16 체크포인트 전에 울렸다 — TensionDirector는 그 단서를 다시 울리지 않는다
            Assert.AreEqual(UnavoidableStep.Banned, r.Observe(JudgeSignal.OfSpace(SignalKind.SpaceExited, SpaceId.ScienceRoom), SpaceId.ScienceRoom, pending, null));
        }

        [Test]
        public void 회피_불가_둘째_조우는_재시작해도_제_슬롯으로_돌아가지_않는다()
        {
            NightProgram program = new NightProgram(3, new[] { new SlotEncounter(EncounterSlot.A, ProgramCatalog.Encounter(ProgramCatalog.Footsteps), false) }, new List<RuleDef> { ProgramCatalog.Rule("H3") }, string.Empty);
            TensionDirector d = new TensionDirector(program, 3, 0, new System.Random(1));
            d.SetUnavoidable(UnavoidableCatalog.Find("S5xH3"));
            d.Observe(JudgeSignal.OfSpace(SignalKind.SpaceEntered, SpaceId.Corridor));
            d.Tick(70f, 0.5f, 0, Band.Band0, false);
            Assert.IsTrue(d.ChainEncounter(ProgramCatalog.Footsteps));
            d.ResetToRest(1, 0f);
            foreach (EncounterRun r in d.Runs)
            {
                if (r.Def.Id == ProgramCatalog.Footsteps) Assert.AreNotEqual(EncounterRunState.Waiting, r.State, "옛: 재시작이 제 슬롯 몫을 되살려 발소리가 한 번 더 나왔다");
            }
        }

        [Test]
        public void 재입실_불가_공간의_항목은_조기_퇴근을_막는_남은_점검에_세지_않는다()
        {
            InspectionBoard b = new InspectionBoard();
            b.Begin(new InspectionPlan(2, new[]
            {
                new InspectionAssignment(InspectionCatalog.Find("K-1"), false, Band.Band0, false),
                new InspectionAssignment(InspectionCatalog.Find("S-1"), false, Band.Band0, false),
            }, SpaceId.None, string.Empty), true);
            Assert.AreEqual(2, b.RemainingCount);
            Assert.AreEqual(1, b.RemainingCountExcept(SpaceId.ScienceRoom));
            Assert.AreEqual(2, b.RemainingCountExcept(SpaceId.None));
        }

        [Test]
        public void G1과_G3은_역설_변조_검은_줄이_겨누지_않는다()
        {
            Assert.IsTrue(ParadoxPlanner.Excluded("G1"), "103차 QA 4회(사용자 결정)");
            Assert.IsTrue(ParadoxPlanner.Excluded(ProgramCatalog.FinaleBlankRule));
            Assert.IsTrue(ParadoxPlanner.Excluded(ProgramCatalog.ReverseReportRule));
            Assert.IsTrue(ParadoxPlanner.Excluded(ProgramCatalog.FinaleRule));
            Assert.IsFalse(ParadoxPlanner.Excluded("G4"));
        }

        [Test]
        public void 가짜_수칙_자리는_회차마다_바뀌고_두_줄이_붙지_않는다()
        {
            int keep = FakeRuleCatalog.SlotOffset;
            try
            {
                HashSet<int> firsts = new HashSet<int>();
                for (int off = 0; off < 300; off++)
                {
                    FakeRuleCatalog.SlotOffset = off;
                    List<string> deck = new List<string> { "a", "b", "c", "d" };   // 1일차 진짜 수칙 4장
                    int p0 = FakeRuleCatalog.SlotFor(1, deck.Count, 0);
                    deck.Insert(p0, "F0");
                    int p1 = FakeRuleCatalog.SlotFor(1, deck.Count, 1);
                    Assert.That(p1, Is.InRange(0, deck.Count - 1));
                    deck.Insert(p1, "F1");
                    int i0 = deck.IndexOf("F0"), i1 = deck.IndexOf("F1");
                    Assert.Greater(System.Math.Abs(i0 - i1), 1, "off " + off + ": " + string.Join(",", deck));
                    firsts.Add(p0);
                }

                Assert.AreEqual(4, firsts.Count, "옛: 첫 줄이 늘 가짜");
            }
            finally
            {
                FakeRuleCatalog.SlotOffset = keep;
            }
        }

        [Test]
        public void 다른_지시가_열려_있어도_이완_구간에_근무일지_안내가_나간다()
        {
            DutyDispatcher d = new DutyDispatcher(3, s => 0f, 5);
            DutyEvent ev;
            Assert.IsTrue(d.Force("W4", out ev));
            Assert.IsTrue(d.Tick(new DutyInput { Minute = NightClock.RelaxStart + 1f, Dt = 0.1f, SinceInspection = 100f }, out ev), "옛: W4가 열려 있으면 W5가 아예 안 나갔다");
            Assert.AreEqual(DutyCatalog.Find("W5"), ev.Def);
            Assert.AreEqual(DutyOutcome.Issued, ev.Outcome);
            Assert.AreEqual("W4", d.Active.Id, "진행 중이던 지시는 그대로");
            Assert.IsTrue(d.LogOpen);
            Assert.IsFalse(d.Tick(new DutyInput { Minute = NightClock.RelaxStart + 2f, Dt = 0.1f, SinceInspection = 100f }, out ev), "한 번만");
            Assert.IsTrue(d.NoteSigned(out ev));
            Assert.AreEqual(DutyOutcome.Done, ev.Outcome);
            Assert.AreEqual("W5", ev.Def.Id);
            Assert.AreEqual("W4", d.Active.Id);

            DutyDispatcher ended = new DutyDispatcher(3, s => 0f, 5);
            Assert.IsTrue(ended.Force("W15", out ev));
            Assert.IsTrue(ended.Tick(new DutyInput { Minute = NightClock.RelaxStart + 1f, Dt = 0.1f, SinceInspection = 100f }, out ev), "따로 연 W5");
            Assert.IsTrue(ended.NotePicked(DutyCatalog.LibraryBookTarget, out ev), "진행 중이던 줍기 지시를 이완 구간에 끝냄");
            Assert.IsNull(ended.Active);
            Assert.IsFalse(ended.Tick(new DutyInput { Minute = NightClock.RelaxStart + 2f, Dt = 0.1f, SinceInspection = 100f }, out ev), "103차 QA 4회: 서명 안내를 두 번 내지 않는다");

            DutyDispatcher late = new DutyDispatcher(3, s => 0f, 5);
            Assert.IsTrue(late.Force("W4", out ev));
            late.Tick(new DutyInput { Minute = NightClock.RelaxStart + 1f, Dt = 0.1f, SinceInspection = 100f }, out ev);
            Assert.IsFalse(late.Tick(new DutyInput { Minute = NightClock.Call2, Dt = 0.1f, SinceInspection = 100f }, out ev), "서명 못 하면 호출 2에 조용히 닫힌다");
            Assert.IsTrue(late.Finished("W5"));
            Assert.IsFalse(late.LogOpen);
            Assert.AreEqual("W4", late.Active.Id);
        }

        [Test]
        public void 밤_안에서_20_오르면_그_밤만_연출이_하한보다_한_구간_위()
        {
            // 103차 후속(사용자 결정, 피드백 1번): 잘하든 못하든 그날 하한 연출은 같고, 그 밤 실수가 쌓인 사람만 그 밤 동안 한 구간 더.
            FearAxisSystem axes = new FearAxisSystem();
            BandResolver bands = new BandResolver(axes);
            axes.ValueChanged += bands.OnValueChanged;
            axes.Restore(10, 10, 10, 0);
            bands.SetDayFloor(DayFloor.Of(4));   // 하한 구간 2
            bands.BeginNight();
            Band floor = DayFloor.Of(4);
            Assert.AreEqual(floor, bands.GetShown(SpaceId.Corridor, FearAxis.Auditory));

            axes.Apply(FearAxis.Auditory, BandResolver.NightRiseBoost - 1, "test", SpaceId.Corridor);
            Assert.AreEqual(floor, bands.GetShown(SpaceId.Corridor, FearAxis.Auditory), "19 — 그대로");
            axes.Apply(FearAxis.Auditory, 1, "test", SpaceId.Corridor);
            Assert.AreEqual(floor + 1, bands.GetShown(SpaceId.Corridor, FearAxis.Auditory), "20 — 한 구간 위");
            Assert.AreEqual(floor, bands.GetShown(SpaceId.Corridor, FearAxis.Layout), "오른 축만");
            axes.Lower(FearAxis.Auditory, 15, "duty");
            Assert.AreEqual(floor + 1, bands.GetShown(SpaceId.Corridor, FearAxis.Auditory), "그 밤에는 내려도 유지");

            bands.SetDayFloor(DayFloor.Of(4));
            bands.BeginNight();   // 다음 밤(또는 재시작)
            Assert.AreEqual(floor, bands.GetShown(SpaceId.Corridor, FearAxis.Auditory), "다음 밤은 다시 그날 하한");
        }

        [Test]
        public void 편성기_예약은_스냅샷으로_되돌린다()
        {
            ProgramDirector d = new ProgramDirector(new System.Random(1));
            object before = d.CaptureState();
            d.Reserve(ProgramCatalog.ModelRush);
            Assert.AreEqual(1, d.Reserved.Count);
            d.RestoreState(before);
            Assert.AreEqual(0, d.Reserved.Count);
        }

        [Test]
        public void 재시작하면_S3_위반_예약도_밤_시작으로_돌아간다()
        {
            NightRun.StartNewRun();
            NightRun.ProgramEnabled = true;
            NightRun.BeginNight(1, () => 30);
            NightRun.ReserveEncounter(ProgramCatalog.ModelRush);
            Assert.AreEqual(1, NightRun.Programs.Reserved.Count);

            NightRun.DebugForceCapture(FearAxis.Layout);
            NightRun.RestartAfterCapture();
            Assert.AreEqual(0, NightRun.Programs.Reserved.Count, "재시작해서 S3를 지켜도 급습이 당겨지던 것");
        }
    }
}

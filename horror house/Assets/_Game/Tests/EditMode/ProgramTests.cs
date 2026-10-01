using System.Collections.Generic;
using NUnit.Framework;

namespace NightDuty.Tests
{
    /// <summary>새 수칙·조우 카탈로그(최종 기획서 「공간별 설계」·「조우와 대응 수칙」).</summary>
    public sealed class ProgramCatalogTests
    {
        private static HashSet<string> Ids(System.Predicate<RuleDef> pick)
        {
            HashSet<string> set = new HashSet<string>();
            foreach (RuleDef r in ProgramCatalog.AllRules)
            {
                if (pick(r)) set.Add(r.Id);
            }

            return set;
        }

        [Test]
        public void 공간수칙_28장과_공통_G1_G2_G3()
        {
            Assert.AreEqual(31, ProgramCatalog.AllRules.Count);
            Assert.AreEqual(4, ProgramCatalog.RulesIn(SpaceId.Corridor).Count);
            Assert.AreEqual(5, ProgramCatalog.RulesIn(SpaceId.Classroom).Count);
            Assert.AreEqual(5, ProgramCatalog.RulesIn(SpaceId.ScienceRoom).Count);
            Assert.AreEqual(5, ProgramCatalog.RulesIn(SpaceId.Toilet).Count);
            Assert.AreEqual(5, ProgramCatalog.RulesIn(SpaceId.Library).Count);
            Assert.AreEqual(4, ProgramCatalog.RulesIn(SpaceId.SecurityRoom).Count);
        }

        [Test]
        public void 위협_손전등_대기형_목록이_기획서와_같다()
        {
            CollectionAssert.AreEquivalent(new[] { "H3", "H4", "C3", "S5", "T3", "L3", "L5" }, Ids(r => r.IsThreat));
            CollectionAssert.AreEquivalent(new[] { "C4", "S3", "S4", "S5", "T3", "T5", "L3" }, Ids(r => r.UsesFlashlight));
            CollectionAssert.AreEquivalent(new[] { "C1", "C3", "T3", "H3", "S5", "L2" }, Ids(r => r.IsWaitType));
        }

        [Test]
        public void 축_분류_규칙()
        {
            // 방아쇠나 금지 행동이 소리면 청각, 손전등·조명이면 조도(기획서 「축 분류 규칙」).
            foreach (string id in new[] { "C1", "S2", "T1", "L2", "L5", "H3", "H4", "G1", "G2", "C3", "K3" })
            {
                Assert.AreEqual(FearAxis.Auditory, ProgramCatalog.Rule(id).Axis, id);
            }

            foreach (string id in new[] { "C4", "S3", "S4", "S5", "T3", "T5", "L3" })
            {
                Assert.AreEqual(FearAxis.Illuminance, ProgramCatalog.Rule(id).Axis, id);
            }

            Assert.IsFalse(ProgramCatalog.Rule("K4").HasAxis);
            Assert.IsFalse(ProgramCatalog.Rule("G3").HasAxis);
        }

        [Test]
        public void 조우마다_대응수칙이_있고_공간이_같다()
        {
            foreach (EncounterDef e in ProgramCatalog.AllEncounters)
            {
                RuleDef r = ProgramCatalog.Rule(e.ResponseRule);
                Assert.IsNotNull(r, e.Id);
                Assert.AreEqual(e.Space, r.Space, e.Id);
                if (e.SecondRule.Length > 0) Assert.AreEqual(e.Space, ProgramCatalog.Rule(e.SecondRule).Space, e.Id);
            }

            Assert.AreEqual(15, ProgramCatalog.AllEncounters.Count);
            Assert.AreEqual("boy", ProgramCatalog.Encounter(ProgramCatalog.CeilingLegs).Mob, "천장 다리는 소년 모델");
        }

        [Test]
        public void 조우에_묶인_수칙은_그_조우가_있어야_한다()
        {
            foreach (RuleDef r in ProgramCatalog.AllRules)
            {
                if (r.IsStandalone) continue;
                EncounterDef e = ProgramCatalog.Encounter(r.BoundEncounter);
                Assert.IsNotNull(e, r.Id);
                Assert.IsTrue(e.ResponseRule == r.Id || e.SecondRule == r.Id, r.Id);
            }
        }

        [Test]
        public void 놀람_예산()
        {
            EncounterDef rush = ProgramCatalog.Encounter(ProgramCatalog.ModelRush);
            EncounterDef bang = ProgramCatalog.Encounter(ProgramCatalog.BoyBang);
            EncounterDef tree = ProgramCatalog.Encounter(ProgramCatalog.PeopleTree);

            Assert.IsFalse(ProgramDirector.WithinBudget(new EncounterDef[0], rush, 2), "1~2일차 강도 5 없음");
            Assert.IsTrue(ProgramDirector.WithinBudget(new EncounterDef[0], rush, 3), "교차라 3일차부터 1회");
            Assert.IsFalse(ProgramDirector.WithinBudget(new[] { bang }, rush, 2), "1~2일차 강도 4 이상 1회");
            Assert.IsTrue(ProgramDirector.WithinBudget(new[] { bang }, rush, 5), "5일차 강도 4 이상 2회");
            Assert.IsFalse(ProgramDirector.WithinBudget(new[] { tree, tree, tree }, tree, 2), "1~2일차 강도 3 이상 3회");
        }
    }

    /// <summary>밤 편성기 — 배정 순서 1~7.</summary>
    public sealed class ProgramDirectorTests
    {
        private static ProgramRequest Request(int day, IFearAxisReader shown, IFearAxisReader survival, InspectionPlan inspections)
        {
            return new ProgramRequest { Day = day, Shown = shown, Survival = survival, Inspections = inspections };
        }

        private static int CountIn(NightProgram p, SpaceId space)
        {
            int n = 0;
            foreach (RuleDef r in p.Deck)
            {
                if (r.Space == space) n++;
            }

            return n;
        }

        [Test]
        public void 일차1은_고정덱이다()
        {
            for (int seed = 0; seed < 30; seed++)
            {
                ProgramDirector d = new ProgramDirector(new System.Random(seed));
                NightProgram p = d.Build(Request(1, FixedBands.All(Band.Band0), FixedBands.All(Band.Band0), null));

                Assert.AreEqual(5, p.Deck.Count, p.ToString());
                Assert.AreEqual("H2", p.Deck[0].Id, "1일차 복도 = 문 자동 열림(인체나무 없이 H1 금지)");
                Assert.AreEqual("C4", p.Deck[1].Id);
                Assert.IsTrue(p.Has("S1") ^ p.Has("S3"), p.ToString());
                Assert.AreEqual("G1", p.Deck[3].Id);
                Assert.AreEqual("G2", p.Deck[4].Id);
                Assert.IsFalse(p.Has("H1") || p.Has("C2") || p.Has("C3"), "조우 묶인 수칙 없음");
                Assert.AreEqual(0, p.Slots.Count, "1일차는 몹 없음(2일차부터)");
            }
        }

        [Test]
        public void 이일차부터_배정_규칙을_지킨다()
        {
            for (int seed = 0; seed < 200; seed++)
            {
                ProgramDirector d = new ProgramDirector(new System.Random(seed));
                AnomalyAssigner a = new AnomalyAssigner(new System.Random(seed + 1000));
                IFearAxisReader survival = FixedBands.All(Band.Band1);
                d.Build(Request(1, FixedBands.All(Band.Band0), survival, a.Build(1, FixedBands.All(Band.Band0))));

                for (int day = 2; day <= 5; day++)
                {
                    IFearAxisReader shown = FixedBands.All(DayFloor.Of(day));
                    NightProgram p = d.Build(Request(day, shown, survival, a.Build(day, shown)));
                    string why = "seed " + seed + ": " + p.Report;

                    bool bang = p.HasEncounter(ProgramCatalog.BoyBang);
                    foreach (SpaceId s in ProgramCatalog.RuleSpaces)
                    {
                        int expected = s == SpaceId.Classroom && bang ? 2 : 1;
                        Assert.AreEqual(expected, CountIn(p, s), s + " — " + why);
                    }

                    Assert.AreEqual(1, CountIn(p, SpaceId.SecurityRoom), why);
                    Assert.AreEqual(day == 5, p.Has("K4"), "5일차 경비실은 K4 — " + why);
                    Assert.AreEqual(p.HasEncounter(ProgramCatalog.CctvPerson), p.Has("K1"), why);
                    Assert.AreEqual("G2", p.Deck[p.Deck.Count - 1].Id, why);
                    Assert.AreEqual("G1", p.Deck[p.Deck.Count - 2].Id, why);
                    if (day == 2) Assert.IsTrue(p.Has(ProgramCatalog.FirstParadoxRule), "2일차 첫 역설 C2 — " + why);

                    int flash = 0;
                    foreach (RuleDef r in p.Deck)
                    {
                        if (r.UsesFlashlight) flash++;
                        if (!r.IsStandalone)
                        {
                            bool bound = false;
                            foreach (SlotEncounter se in p.Slots)
                            {
                                if (se.Encounter.ResponseRule == r.Id || se.Encounter.SecondRule == r.Id) bound = true;
                            }

                            Assert.IsTrue(bound, r.Id + "는 조우 없이 들어가면 헛자리 — " + why);
                        }
                    }

                    Assert.LessOrEqual(flash, ProgramCatalog.FlashlightPerDay, why);

                    int expectSlots = day == 2 ? 2 : 3;
                    Assert.LessOrEqual(p.Slots.Count, expectSlots, why);
                    Assert.GreaterOrEqual(p.Slots.Count, 1, why);

                    List<EncounterDef> chosen = new List<EncounterDef>();
                    for (int i = 0; i < p.Slots.Count; i++)
                    {
                        EncounterDef e = p.Slots[i].Encounter;
                        Assert.IsTrue(p.Has(e.ResponseRule), "조우의 대응 수칙이 덱에 — " + why);
                        if (!p.Slots[i].Reserved) Assert.IsTrue(e.Satisfied(shown), e.Id + " 발동 조건 — " + why);
                        if (i > 0) Assert.AreNotEqual(p.Slots[i - 1].Encounter.Mob, e.Mob, "직전 슬롯과 같은 몹 — " + why);
                        Assert.AreEqual((EncounterSlot)i, p.Slots[i].Slot);
                        chosen.Add(e);
                    }

                    // 예산: 전체가 하루 예산 안.
                    for (int i = 0; i < chosen.Count; i++)
                    {
                        List<EncounterDef> others = new List<EncounterDef>(chosen);
                        others.RemoveAt(i);
                        Assert.IsTrue(ProgramDirector.WithinBudget(others, chosen[i], day), "놀람 예산 — " + why);
                    }
                }

                Assert.LessOrEqual(d.ReverseReportAssigned, ProgramCatalog.ReverseReportPerRun, "역보고 T4 회차 2번까지");
            }
        }

        [Test]
        public void 보너스_슬롯C는_잘하는_중일_때만()
        {
            int threeWhenCalm = 0;
            for (int seed = 0; seed < 50; seed++)
            {
                IFearAxisReader shown = FixedBands.All(Band.Band2);
                ProgramDirector calm = new ProgramDirector(new System.Random(seed));
                calm.Build(Request(1, FixedBands.All(Band.Band0), FixedBands.All(Band.Band0), null));
                if (calm.Build(Request(3, shown, FixedBands.All(Band.Band1), null)).Slots.Count == 3) threeWhenCalm++;

                ProgramDirector tense = new ProgramDirector(new System.Random(seed));
                tense.Build(Request(1, FixedBands.All(Band.Band0), FixedBands.All(Band.Band0), null));
                Assert.LessOrEqual(tense.Build(Request(3, shown, FixedBands.All(Band.Band3), null)).Slots.Count, 2, "위험(75~)이면 C를 끈다");
            }

            Assert.Greater(threeWhenCalm, 40, "잘하는 중이면 대개 C까지 건다");
            Assert.IsTrue(NightProgram.SlotActive(EncounterSlot.C, 1));
            Assert.IsFalse(NightProgram.SlotActive(EncounterSlot.C, 2), "재시작 k≥2면 C를 끈다(장치 1)");
        }

        [Test]
        public void 예약된_조우는_다음_밤에_강제로_들어간다()
        {
            ProgramDirector d = new ProgramDirector(new System.Random(7));
            d.Build(Request(1, FixedBands.All(Band.Band0), FixedBands.All(Band.Band0), null));

            d.Reserve(ProgramCatalog.ModelRush);
            NightProgram day2 = d.Build(Request(2, FixedBands.All(Band.Band1), FixedBands.All(Band.Band1), null));
            Assert.IsFalse(day2.HasEncounter(ProgramCatalog.ModelRush), "모형 급습 예약은 3일차부터");
            Assert.AreEqual(0, d.Reserved.Count, "예약은 한 번 쓰고 비운다");

            d.Reserve(ProgramCatalog.ModelRush);
            d.Reserve(ProgramCatalog.SuitMan);
            NightProgram day3 = d.Build(Request(3, FixedBands.All(Band.Band1), FixedBands.All(Band.Band1), null));

            Assert.IsTrue(day3.HasEncounter(ProgramCatalog.ModelRush), day3.Report);
            Assert.IsTrue(day3.HasEncounter(ProgramCatalog.SuitMan), day3.Report);
            Assert.IsTrue(day3.Has("S5") && day3.Has("L5"), day3.Report);
            Assert.IsTrue(day3.At(EncounterSlot.A).Reserved, "예약이 앞 슬롯을 먼저 차지한다");
        }

        [Test]
        public void 강제_수칙은_그_공간에_들어간다()
        {
            ProgramDirector d = new ProgramDirector(new System.Random(3));
            d.Build(Request(1, FixedBands.All(Band.Band0), FixedBands.All(Band.Band0), null));
            ProgramRequest req = Request(3, FixedBands.All(Band.Band1), FixedBands.All(Band.Band1), null);
            req.ForcedRules = new[] { "T5", "L4" };

            NightProgram p = d.Build(req);

            Assert.IsTrue(p.Has("T5") && p.Has("L4"), p.Report);
        }

        [Test]
        public void 소년_머리박기는_교실에_두장()
        {
            bool seen = false;
            for (int seed = 0; seed < 200 && !seen; seed++)
            {
                ProgramDirector d = new ProgramDirector(new System.Random(seed));
                d.Build(Request(1, FixedBands.All(Band.Band0), FixedBands.All(Band.Band0), null));
                NightProgram p = d.Build(Request(3, new FixedBands(Band.Band2, Band.Band0, Band.Band2), FixedBands.All(Band.Band1), null));
                if (!p.HasEncounter(ProgramCatalog.BoyBang)) continue;

                seen = true;
                Assert.IsTrue(p.Has("C2") && p.Has("C3"), p.Report);
                Assert.AreEqual(2, CountIn(p, SpaceId.Classroom));
            }

            Assert.IsTrue(seen, "청각 2 + 배치 2면 교차 조우(점수 +2)가 나와야 한다");
        }

        [Test]
        public void 가장_낮은_축_수칙이_최소_한장()
        {
            for (int seed = 0; seed < 100; seed++)
            {
                ProgramDirector d = new ProgramDirector(new System.Random(seed));
                d.Build(Request(1, FixedBands.All(Band.Band0), FixedBands.All(Band.Band0), null));
                FixedBands shown = new FixedBands(Band.Band3, Band.Band1, Band.Band3);
                NightProgram p = d.Build(Request(3, shown, FixedBands.All(Band.Band1), null));

                bool any = false;
                foreach (RuleDef r in p.Deck)
                {
                    if (r.Space != SpaceId.None && r.HasAxis && r.Axis == FearAxis.Illuminance) any = true;
                }

                Assert.IsTrue(any, p.Report);
            }
        }
    }

    /// <summary>NightRun에 붙은 밤 편성.</summary>
    public sealed class NightRunProgramTests
    {
        [TearDown]
        public void TearDown()
        {
            NightRun.DeckOverride = null;
            NightRun.ProgramEnabled = false;
            NightRun.InspectionsEnabled = false;
            NightRun.StartNewRun();
            EventBus.ClearAll();
        }

        [Test]
        public void 켜면_밤시작에_편성하고_재시작해도_그대로다()
        {
            NightRun.StartNewRun();
            NightRun.RegisteredTargets = null;
            NightRun.DeckOverride = day => new List<RuleSO>();
            NightRun.ProgramEnabled = true;
            NightRun.InspectionsEnabled = true;

            NightRun.BeginNight(1, () => 30);
            NightProgram first = NightRun.Program;
            Assert.AreEqual(1, first.Day);
            Assert.IsTrue(first.Has("H2"));
            Assert.AreEqual(0, first.Slots.Count);

            NightRun.DebugForceCapture(FearAxis.Layout);
            NightRun.RestartAfterCapture();
            Assert.AreSame(first, NightRun.Program);

            NightRun.RequestEndNight();
            NightRun.BeginNight(2, () => 30);
            Assert.AreEqual(2, NightRun.Program.Day);
            Assert.IsTrue(NightRun.Program.Has(ProgramCatalog.FirstParadoxRule));
        }

        [Test]
        public void 끄면_빈_편성이다()
        {
            NightRun.StartNewRun();
            NightRun.DeckOverride = day => new List<RuleSO>();
            NightRun.BeginNight(1, () => 30);
            Assert.AreEqual(0, NightRun.Program.Deck.Count);
        }
    }
}

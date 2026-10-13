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
        public void 공간수칙_28장과_공통_G1_G3()
        {
            Assert.AreEqual(36, ProgramCatalog.AllRules.Count);   // 79차: T7·L6·K5·G4 추가(T3·T5·L5는 폐기 — 정의는 남김) · 80차: L7 추가 · 103차: C6 추가
            Assert.IsNull(ProgramCatalog.Rule("G2"), "G2는 2026-10-04 폐기");
            Assert.AreEqual(4, ProgramCatalog.RulesIn(SpaceId.Corridor).Count);
            Assert.AreEqual(6, ProgramCatalog.RulesIn(SpaceId.Classroom).Count);   // 103차: C6
            Assert.AreEqual(5, ProgramCatalog.RulesIn(SpaceId.ScienceRoom).Count);
            Assert.AreEqual(6, ProgramCatalog.RulesIn(SpaceId.Toilet).Count);
            Assert.AreEqual(7, ProgramCatalog.RulesIn(SpaceId.Library).Count);   // 80차: L7
            Assert.AreEqual(5, ProgramCatalog.RulesIn(SpaceId.SecurityRoom).Count);
        }

        [Test]
        public void 위협_손전등_대기형_목록이_기획서와_같다()
        {
            CollectionAssert.AreEquivalent(new[] { "H3", "H4", "C3", "S5", "T3", "L3", "L5" }, Ids(r => r.IsThreat));
            CollectionAssert.AreEquivalent(new[] { "C4", "S3", "S4", "S5", "T3", "T5", "L3", "L6" }, Ids(r => r.UsesFlashlight));
            CollectionAssert.AreEquivalent(new[] { "C1", "C3", "T3", "H3", "S5", "L2" }, Ids(r => r.IsWaitType));
        }

        [Test]
        public void 축_분류_규칙()
        {
            // 방아쇠나 금지 행동이 소리면 청각, 손전등·조명이면 조도(기획서 「축 분류 규칙」).
            foreach (string id in new[] { "C1", "S2", "T1", "L2", "L5", "H3", "H4", "G1", "C3", "K3" })
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
                if (e.Id == ProgramCatalog.CeilingLegs)
                {
                    Assert.AreEqual(string.Empty, e.ResponseRule, "52차: 시체 낙하는 수칙 없이 놀람만");
                    continue;
                }

                if (e.Id == ProgramCatalog.SuitMan)
                {
                    Assert.AreEqual(string.Empty, e.ResponseRule, "85차: 창밖 노란 얼굴은 수칙 없이(L3는 문간 노란 얼굴만) — 2초 응시하면 달아남");
                    continue;
                }

                RuleDef r = ProgramCatalog.Rule(e.ResponseRule);
                Assert.IsNotNull(r, e.Id);
                if (e.Id == ProgramCatalog.ToiletBlackout) Assert.AreEqual("S4", r.Id, "79차: 화장실 소등도 S4(정전 중 손전등 금지)");
                else Assert.AreEqual(e.Space, r.Space, e.Id);
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
                if (r.IsStandalone || ProgramCatalog.IsRetired(r.Id)) continue;   // 79차: T3(S4에 합침)
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
        public void 일차1은_S1_G1에_후보에서_둘을_더한_네_장이다()
        {
            // 57차(민: 「1~2일차 수칙이 너무 풀이 좁아」): S1·G1 + Day1Pool. 74차(민: 「1일차 4개, 2~4일차 5개, 5일차 6개」): 후보에서 둘.
            HashSet<string> decks = new HashSet<string>();
            for (int seed = 0; seed < 60; seed++)
            {
                ProgramDirector d = new ProgramDirector(new System.Random(seed));
                NightProgram p = d.Build(Request(1, FixedBands.All(Band.Band0), FixedBands.All(Band.Band0), null));
                string why = "seed " + seed + ": " + p;

                Assert.AreEqual(4, ProgramDirector.RulesFor(1));
                Assert.AreEqual(ProgramDirector.RulesFor(1), p.Deck.Count, why);
                Assert.IsTrue(p.Has("S1"), "1일차는 모형 위치를 익히는 날 — " + why);
                Assert.IsFalse(p.Has("S3"), why);
                Assert.AreEqual("G1", p.Deck[p.Deck.Count - 1].Id, why);
                Assert.IsFalse(p.Has("H1") || p.Has("C2") || p.Has("C3"), "조우 묶인 수칙 없음 — " + why);
                Assert.AreEqual(0, p.Slots.Count, "1일차는 몹 없음(2일차부터)");

                int flash = 0;
                foreach (RuleDef r in p.Deck)
                {
                    Assert.IsTrue(r.Id == "S1" || r.Id == "G1" || System.Array.IndexOf(ProgramDirector.Day1Pool, r.Id) >= 0, r.Id + " — " + why);
                    Assert.IsTrue(r.IsStandalone, r.Id + "는 혼자 서는 수칙 — " + why);
                    if (r.UsesFlashlight) flash++;
                }

                foreach (SpaceId s in ProgramCatalog.RuleSpaces) Assert.LessOrEqual(CountIn(p, s), 2, s + " — " + why);
                Assert.LessOrEqual(CountIn(p, SpaceId.SecurityRoom), 1, why);
                Assert.LessOrEqual(flash, ProgramCatalog.FlashlightPerDay, why);
                decks.Add(p.ToString());
            }

            // 104차(사용자: 「K3 수칙을 고정으로 모든 밤에」): 1일차 네 장 중 S1·G1·K3가 고정이고 감각 세 축 보장으로 조도 C4가 들어가 1일차 덱은 하나로 정해진다(옛: K2/K3만 갈렸다).
            Assert.AreEqual(1, decks.Count, "1일차 덱은 S1·C4·K3·G1 — " + string.Join(" / ", decks));
            Assert.IsTrue(decks.Count == 1 && new List<string>(decks)[0].Contains("K3"));
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

                    // 57차: 하루 여섯 장(민: 「수칙은 5~6개로 고정」) — 공간마다 많아야 한 장(소년 쾅이면 교실 둘), 점검이 없는 공간의 채움부터 뺀다.
                    Assert.AreEqual(ProgramDirector.RulesFor(day), p.Deck.Count, why);   // 74차: 2~4일차 5장, 5일차 6장
                    bool bang = p.HasEncounter(ProgramCatalog.BoyBang);
                    foreach (SpaceId s in ProgramCatalog.RuleSpaces)
                    {
                        int expected = s == SpaceId.Classroom && bang ? 2 : 1;
                        Assert.LessOrEqual(CountIn(p, s), expected, s + " — " + why);
                    }

                    // 104차: 경비실 버티기 수칙 K3는 매일 — 경비실이 조우·피날레 수칙(K1·K4)으로 차 있으면 한 장 더.
                    Assert.IsTrue(p.Has(ProgramDirector.CampRule), "K3 매일 — " + why);
                    Assert.LessOrEqual(CountIn(p, SpaceId.SecurityRoom), 2, why);
                    Assert.IsFalse(day != 5 && p.Has("K4"), "K4는 5일차만 — " + why);
                    Assert.AreEqual(p.HasEncounter(ProgramCatalog.CctvPerson), p.Has("K1"), why);
                    Assert.IsTrue(p.Has("G1"), why);
                    Assert.AreEqual(p.Has("G4") ? "G4" : "G1", p.Deck[p.Deck.Count - 1].Id, why);   // 79차: G4는 짝수 날 맨 끝(자리가 있을 때)
                    Assert.IsTrue(!p.Has("G4") || ProgramDirector.HasTabletRule(day), "G4는 짝수 날만 — " + why);
                    foreach (string retired in ProgramCatalog.Retired) Assert.IsFalse(p.Has(retired), retired + " 폐기 — " + why);
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

                            foreach (EncounterDef ex in p.Extras) if (ex.ResponseRule == r.Id) bound = true;   // 103차: CCTV 사람(K1)은 겹침 조우
                            Assert.IsTrue(bound, r.Id + "는 조우 없이 들어가면 헛자리 — " + why);
                        }
                    }

                    Assert.LessOrEqual(flash, ProgramCatalog.FlashlightPerDay, why);

                    int expectSlots = day == 2 ? 2 : 3;
                    int counted = 0;   // 103차: 창밖 노란 얼굴(수칙 없는 빌드업)은 슬롯 수에 세지 않는다 — 글자는 셋까지
                    foreach (SlotEncounter se in p.Slots) if (se.Encounter.Id != ProgramCatalog.SuitMan) counted++;
                    Assert.LessOrEqual(counted, expectSlots, why);
                    Assert.LessOrEqual(p.Slots.Count, ProgramDirector.SlotLetters, why);
                    Assert.GreaterOrEqual(p.Slots.Count, 1, why);

                    List<EncounterDef> chosen = new List<EncounterDef>();
                    for (int i = 0; i < p.Slots.Count; i++)
                    {
                        EncounterDef e = p.Slots[i].Encounter;
                        Assert.IsTrue(e.ResponseRule.Length == 0 || p.Has(e.ResponseRule), "조우의 대응 수칙이 덱에 — " + why);
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
                // 103차: 조우 수 = 슬롯(창밖 노란 얼굴 제외) + 겹침 조우, 3일차 상한 3(EncounterCap) — 잘하는 중이면 상한까지 채운다.
                NightProgram calmNight = calm.Build(Request(3, shown, FixedBands.All(Band.Band1), null));
                int calmCount = calmNight.Extras.Count;
                foreach (SlotEncounter se in calmNight.Slots) if (se.Encounter.Id != ProgramCatalog.SuitMan) calmCount++;
                // 103차 후속: 3일차는 창밖 노란 얼굴 고정 — 그 조우가 슬롯 글자 하나를 차지하므로 글자 셋이 다 차도 「C까지 건」 것으로 본다.
                if (calmCount >= ProgramDirector.EncounterCap(3) || calmNight.Slots.Count >= ProgramDirector.SlotLetters) threeWhenCalm++;

                ProgramDirector tense = new ProgramDirector(new System.Random(seed));
                tense.Build(Request(1, FixedBands.All(Band.Band0), FixedBands.All(Band.Band0), null));
                int tenseCounted = 0;   // 103차: 창밖 노란 얼굴은 세지 않는다
                foreach (SlotEncounter se in tense.Build(Request(3, shown, FixedBands.All(Band.Band3), null)).Slots) if (se.Encounter.Id != ProgramCatalog.SuitMan) tenseCounted++;
                Assert.LessOrEqual(tenseCounted, 2, "위험(75~)이면 C를 끈다");
            }

            Assert.Greater(threeWhenCalm, 35, "잘하는 중이면 대개 C까지 건다(103차: 같은 수칙 조우는 한 밤에 하나 · CCTV 겹침 K1이 장수를 차지하는 밤은 C를 못 채울 수 있다)");
            Assert.IsTrue(NightProgram.SlotActive(EncounterSlot.C, 1));
            Assert.IsFalse(NightProgram.SlotActive(EncounterSlot.C, 2), "재시작 k≥2면 C를 끈다(장치 1)");
        }

        [Test]
        public void 예약된_조우는_다음_밤에_강제로_들어간다()
        {
            ProgramDirector d = new ProgramDirector(new System.Random(7));
            d.Build(Request(1, FixedBands.All(Band.Band0), FixedBands.All(Band.Band0), null));

            // 70차: 모형 급습 예약(S3 위반)은 사슬을 앞당긴다 — 다음 밤 복도 끝에 선 자, 그다음 밤 모형 급습.
            d.Reserve(ProgramCatalog.ModelRush);
            NightProgram day2 = d.Build(Request(2, FixedBands.All(Band.Band1), FixedBands.All(Band.Band1), null));
            Assert.IsFalse(day2.HasEncounter(ProgramCatalog.ModelRush), "급습 전날에는 복도 끝에 선 자가 먼저");
            Assert.IsTrue(day2.HasEncounter(ProgramCatalog.HallEndFigure), day2.Report);
            Assert.AreEqual(0, d.Reserved.Count, "예약은 한 번 쓰고 비운다");

            d.Reserve(ProgramCatalog.SuitMan);
            NightProgram day3 = d.Build(Request(3, FixedBands.All(Band.Band1), FixedBands.All(Band.Band1), null));

            Assert.IsTrue(day3.HasEncounter(ProgramCatalog.ModelRush), day3.Report);
            Assert.AreEqual(d.IsWindowFaceDay(3), day3.HasEncounter(ProgramCatalog.SuitMan), "84·103차: 창밖 노란 얼굴은 날짜 사슬(2·3일차 중 하루)로만 — 예약은 무시한다. " + day3.Report);
            Assert.AreEqual(ProgramCatalog.ModelRush, day3.At(EncounterSlot.A).Encounter.Id, day3.Report);
            Assert.IsTrue(day3.Has("S5") && !day3.Has("L5"), day3.Report);
            Assert.IsTrue(day3.At(EncounterSlot.A).Reserved, "예약이 앞 슬롯을 먼저 차지한다");
        }

        [Test]
        public void 강제_수칙은_그_공간에_들어간다()
        {
            ProgramDirector d = new ProgramDirector(new System.Random(3));
            d.Build(Request(1, FixedBands.All(Band.Band0), FixedBands.All(Band.Band0), null));
            ProgramRequest req = Request(4, FixedBands.All(Band.Band1), FixedBands.All(Band.Band1), null);   // 103차: 노란 얼굴 사슬(창밖 2·3일차 중 하루 · 문간 4·5일차 중 하루)이 도서관을 쓸 수 있다 — 도서관 밖 수칙으로
            req.ForcedRules = new[] { "T7", "C4" };   // 79차: T5 폐기

            NightProgram p = d.Build(req);

            Assert.IsTrue(p.Has("T7") && p.Has("C4"), p.Report);
        }

        [Test]
        public void 소년_머리박기는_교실에_두장()
        {
            bool seen = false;
            for (int seed = 0; seed < 200 && !seen; seed++)
            {
                ProgramDirector d = new ProgramDirector(new System.Random(seed));
                d.Build(Request(1, FixedBands.All(Band.Band0), FixedBands.All(Band.Band0), null));
                NightProgram p = d.Build(Request(5, new FixedBands(Band.Band2, Band.Band0, Band.Band2), FixedBands.All(Band.Band1), null));   // 103차: 소년 머리 박기는 4일차부터(3일차 불가) · 4일차는 복도 끝(+노란 얼굴일 수 있음)이 앞 슬롯을 쓴다 — 5일차로
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

                // 79차: 조우·고정 수칙만으로 하루 장수가 꽉 찬 날은 채움이 한 장도 없다(하루 장수가 우선).
                bool onlyBound = true;
                foreach (RuleDef r in p.Deck)
                {
                    if (r.Space != SpaceId.None && r.Space != SpaceId.SecurityRoom && r.IsStandalone) onlyBound = false;
                }

                Assert.IsTrue(any || onlyBound, p.Report);
            }
        }
    }

    /// <summary>NightRun에 붙은 밤 편성.</summary>
    public sealed class NightRunProgramTests
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
        public void 켜면_밤시작에_편성하고_재시작해도_그대로다()
        {
            NightRun.StartNewRun();
            NightRun.ProgramEnabled = true;
            NightRun.InspectionsEnabled = true;

            NightRun.BeginNight(1, () => 30);
            NightProgram first = NightRun.Program;
            Assert.AreEqual(1, first.Day);
            Assert.IsTrue(first.Has("S1") && first.Has("G1"), "1일차 고정 S1·G1(H2는 57차부터 후보에서 무작위 — 옛 단언은 우연히 통과했다)");
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
            NightRun.BeginNight(1, () => 30);
            Assert.AreEqual(0, NightRun.Program.Deck.Count);
        }
    }
}

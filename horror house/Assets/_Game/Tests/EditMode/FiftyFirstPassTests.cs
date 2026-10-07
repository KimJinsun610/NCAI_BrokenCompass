using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace NightDuty.Tests
{
    /// <summary>51차 — 시체 낙하(C-3·C2) · T4 목격 사슬 · K1 CCTV · 역설 형식 · 일차 분배.</summary>
    public sealed class FiftyFirstPassTests
    {
        private static InspectionAssignment Row(string id, bool anomaly = false, bool late = false)
        {
            return new InspectionAssignment(InspectionCatalog.Find(id), anomaly, Band.Band0, late);
        }

        private static NightProgram ProgramWith(int day, string[] rules, params string[] encounters)
        {
            List<SlotEncounter> slots = new List<SlotEncounter>();
            for (int i = 0; i < encounters.Length; i++) slots.Add(new SlotEncounter((EncounterSlot)i, ProgramCatalog.Encounter(encounters[i]), false));
            List<RuleDef> deck = new List<RuleDef>();
            foreach (string id in rules) deck.Add(ProgramCatalog.Rule(id));
            return new NightProgram(day, slots, deck, "test");
        }

        // ── 문구 ───────────────────────────────────────────────

        [Test]
        public void 문구_C2_C4_K1_C3()
        {
            // 52차 민 문구.
            Assert.AreEqual("교실의 _? 는 무시하십시오.", ProgramCatalog.Rule("C2").Text);
            Assert.AreEqual("붉은 불빛 아래에서는 손전등을 끄십시오.", ProgramCatalog.Rule("C4").Text);
            Assert.AreEqual("화면 속 !_ 이 지나갈 때까지 채널을 넘기지 마십시오.", ProgramCatalog.Rule("K1").Text, "원래대로");
            InspectionItem c3 = InspectionCatalog.Find("C-3");
            Assert.AreEqual("교실 안쪽에 작은 사다리가 있는지 확인하십시오.", c3.TabletLine);
            Assert.AreEqual(FearAxis.Layout, c3.Axis, "이상 = 사다리 없음(배치)");
            Assert.AreEqual(AnomalyTemplate.Move, c3.Template);
        }

        [Test]
        public void 역설_문자는_모두_지금_또는_즉시이고_C2는_소년_C4는_켜라()
        {
            foreach (ParadoxEntry e in ParadoxCatalog.All)
            {
                if (!e.HasMessage) continue;
                Assert.IsTrue(e.Message.Contains("지금") || e.Message.Contains("즉시"), e.RuleId + ": " + e.Message);
                StringAssert.DoesNotStartWith("[", e.Message, "역설 문자는 머리(「[점검 지시]」 등)가 없다 — " + e.RuleId);
            }

            ParadoxEntry c2 = ParadoxCatalog.Find("C2");
            Assert.AreEqual("교실에 남아 있는 학생이 있습니다. 지금 확인하십시오.", c2.Message);
            Assert.AreEqual(FinalCues.BoySeated, c2.Cue, "소년이 앉은 뒤에 온다 — 몹을 예고하지 않는다");
            Assert.AreEqual(FinalCues.BoyTarget, c2.Target);
            ParadoxEntry c4 = ParadoxCatalog.Find("C4");
            Assert.AreEqual("붉은 등 아래가 어둡습니다. 지금 손전등으로 비추십시오.", c4.Message, "따르면(켜면) C4를 어긴다");
            Assert.AreEqual(SafeReadPattern.StandStill, c4.Pattern, "꺼진 채 붉은 등 아래를 지나가면 안전한 읽기");
            Assert.AreEqual("확인되었습니다.", NightRun.ParadoxAck);
        }

        // ── 시체 낙하 ───────────────────────────────────────────

        [Test]
        public void 시체_낙하는_사다리를_0_5초_보면_1_2초_전조_뒤_대면()
        {
            DirectorFixture f = new DirectorFixture(2, 0.99, null, DirectorFixture.Slot(EncounterSlot.A, ProgramCatalog.CeilingLegs));
            f.Minute = 30f;   // 슬롯 A(01:00) 전이어도 — 응시 방아쇠는 점검이 지시받은 때를 따른다
            f.Enter(SpaceId.Classroom_1_3).Wait(0.1f);
            string ladder = InspectionCatalog.TargetPrefix + "C-3";
            for (int i = 0; i < 4; i++) f.Send(JudgeSignal.Gaze(ladder, 0.1f)).Wait(0.1f);
            Assert.IsFalse(f.HasPhase(ProgramCatalog.CeilingLegs, DirectionPhase.Foreshadow), "0.4초");
            f.Send(JudgeSignal.Gaze(ladder, 0.1f)).Wait(0.1f);
            Assert.IsTrue(f.HasPhase(ProgramCatalog.CeilingLegs, DirectionPhase.Foreshadow), "0.5초");
            Assert.IsFalse(f.HasPhase(ProgramCatalog.CeilingLegs, DirectionPhase.FalseForeshadow), "헛예고 없음");
            f.Wait(1.0f);
            Assert.IsFalse(f.HasPhase(ProgramCatalog.CeilingLegs, DirectionPhase.Confront));
            f.Wait(0.3f);
            Assert.IsTrue(f.HasPhase(ProgramCatalog.CeilingLegs, DirectionPhase.Confront), "전조 1.2초 고정");
            Assert.AreEqual(0, f.CueCount(SignalKind.CueStarted, FinalCues.Legs), "52차: 수칙 없는 놀람 — 판정 단서가 없다");
            Assert.AreEqual(string.Empty, ProgramCatalog.Encounter(ProgramCatalog.CeilingLegs).ResponseRule);
        }

        [Test]
        public void 시체_낙하는_사다리가_점검_중이_아니면_오지_않는다()
        {
            DirectorFixture f = new DirectorFixture(2, 0.99, null, DirectorFixture.Slot(EncounterSlot.A, ProgramCatalog.CeilingLegs));
            f.Director.GazeTargetReady = id => false;
            f.Minute = 70f;
            f.Enter(SpaceId.Classroom_1_3);
            for (int i = 0; i < 30; i++) f.Send(JudgeSignal.Gaze(InspectionCatalog.TargetPrefix + "C-3", 0.1f)).Wait(0.1f);
            Assert.IsFalse(f.HasPhase(ProgramCatalog.CeilingLegs, DirectionPhase.Foreshadow));

            f.Director.GazeTargetReady = id => id == InspectionCatalog.TargetPrefix + "C-3";
            f.Send(JudgeSignal.Gaze("other", 0.1f)).Wait(0.1f);
            for (int i = 0; i < 5; i++) f.Send(JudgeSignal.Gaze(InspectionCatalog.TargetPrefix + "C-3", 0.1f)).Wait(0.1f);
            Assert.IsTrue(f.HasPhase(ProgramCatalog.CeilingLegs, DirectionPhase.Foreshadow), "지시받은 사다리를 보면");
        }

        [Test]
        public void C2_떨어진_시체는_판정하지_않는다()
        {
            // 52차: C2는 앉은 소년 — 시체(옛 rule.C2.legs)를 오래 봐도 위반이 아니다.
            FinalFixture f = new FinalFixture("C2");
            f.Cue(FinalCues.Legs);
            f.Samples(() => JudgeSignal.Gaze(FinalCues.LegsTarget, 0.1f), 5f);
            Assert.AreNotEqual(FinalOutcome.Violated, f.Last("C2"));
        }

        [Test]
        public void 소년_머리_박기는_2일차_첫_역설과_함께()
        {
            Assert.AreEqual("C2", ProgramCatalog.FirstParadoxRule);
            Assert.IsTrue(ProgramDirector.IsDayFocus(2, ProgramCatalog.BoyBang));
            Assert.IsFalse(ProgramDirector.IsDayFocus(4, ProgramCatalog.BoyBang));
            for (int seed = 0; seed < 20; seed++)
            {
                ProgramDirector d = new ProgramDirector(new System.Random(seed));
                NightProgram p = d.Build(new ProgramRequest { Day = 2, Shown = FixedBands.All(Band.Band1) });
                Assert.IsTrue(p.HasEncounter(ProgramCatalog.BoyBang), "seed " + seed + ": " + p);
                Assert.IsTrue(p.Has("C2") && p.Has("C3"), "C2·C3·소년 셋이 하나 — seed " + seed);
            }
        }

        [Test]
        public void 시체_낙하_날에는_사다리_점검을_더한다()
        {
            InspectionPlan plan = new InspectionPlan(2, new[] { Row("K-1"), Row("C-1", true), Row("S-1") }, SpaceId.ScienceRoom, "C-1");
            InspectionPlan patched = NightRun.PatchPlanForProgram(plan, ProgramWith(2, new[] { "C2" }, ProgramCatalog.CeilingLegs));
            Assert.AreNotSame(plan, patched);
            Assert.IsNotNull(patched.Find("C-3"));
            Assert.IsFalse(patched.Find("C-3").IsAnomaly);
            Assert.AreEqual(plan.AnomalyCount, patched.AnomalyCount);

            InspectionPlan same = NightRun.PatchPlanForProgram(plan, ProgramWith(2, new[] { "S1" }));
            Assert.AreSame(plan, same, "고칠 것이 없으면 그대로");
        }

        // ── T4 목격 사슬 ─────────────────────────────────────────

        [Test]
        public void T4_날_변기는_정상_이른_항목이고_T2_점검은_빠진다()
        {
            InspectionPlan plan = new InspectionPlan(3, new[] { Row("K-1"), Row("T-1", true, true), Row("T-2", false, true), Row("L-1") }, SpaceId.Toilet, "L-1");
            InspectionPlan patched = NightRun.PatchPlanForProgram(plan, ProgramWith(3, new[] { "T4" }, ProgramCatalog.ToiletGirl));
            InspectionAssignment t1 = patched.Find("T-1");
            Assert.IsNotNull(t1);
            Assert.IsFalse(t1.IsAnomaly, "「변기는 정상입니다」");
            Assert.IsFalse(t1.IsLate, "목격 뒤 바로 지시");
            Assert.IsNull(patched.Find("T-2"));
        }

        [Test]
        public void T4_날_화장실_항목이_변기뿐이면_거울_T3을_더해_화장실로_부른다()
        {
            // 53차 플레이 점검: T-1이 묶이면 화장실로 부르는 지시가 없어 여자아이(화장실 입장 방아쇠)가 나오지 않았다.
            InspectionPlan plan = new InspectionPlan(2, new[] { Row("K-1"), Row("T-1", false, true), Row("C-1") }, SpaceId.Toilet, "K-1");
            InspectionPlan patched = NightRun.PatchPlanForProgram(plan, ProgramWith(2, new[] { "T4" }, ProgramCatalog.Footsteps, ProgramCatalog.ToiletGirl));   // 여자아이 = 슬롯 B
            InspectionAssignment t3 = patched.Find("T-3");
            Assert.IsNotNull(t3);
            Assert.IsFalse(t3.IsAnomaly);
            Assert.IsTrue(t3.IsLate, "화장실이 늦은 공간이면 호출 2로(슬롯 B는 호출 2와 함께 열린다)");
            Assert.AreEqual(plan.AnomalyCount, patched.AnomalyCount);

            InspectionPlan early = NightRun.PatchPlanForProgram(new InspectionPlan(2, new[] { Row("K-1"), Row("T-1") }, SpaceId.ScienceRoom, "K-1"), ProgramWith(2, new[] { "T4" }, ProgramCatalog.ToiletGirl));
            Assert.IsFalse(early.Find("T-3").IsLate);

            InspectionPlan mirror = new InspectionPlan(2, new[] { Row("K-1"), Row("T-1"), Row("T-3", true) }, SpaceId.None, "K-1");
            InspectionPlan kept = NightRun.PatchPlanForProgram(mirror, ProgramWith(2, new[] { "T4" }, ProgramCatalog.ToiletGirl));
            Assert.IsTrue(kept.Find("T-3").IsAnomaly, "이미 화장실 항목이 있으면 건드리지 않는다");
        }

        [Test]
        public void 슬롯_A_C_조우_방이_늦은_공간이면_이른_항목을_둔다()
        {
            // 54차 QA: 3일차 화장실 여자아이가 슬롯 A(01:00~01:52)인데 화장실이 늦은 공간이라 화장실 점검이 02:16에야 나와 조우가 열리지 않았다.
            InspectionPlan t4 = new InspectionPlan(3, new[] { Row("K-1"), Row("T-1", false, true), Row("C-1") }, SpaceId.Toilet, "K-1");
            InspectionPlan a = NightRun.PatchPlanForProgram(t4, ProgramWith(3, new[] { "T4" }, ProgramCatalog.ToiletGirl));
            Assert.IsFalse(a.Find("T-3").IsLate, "T4 날은 더할 화장실 항목이 없다 — 거울을 이르게(호출 1 몫)");
            Assert.IsFalse(a.Find("T-1").IsLate);
            Assert.IsNull(a.Find("T-2"));
            Assert.AreEqual(t4.AnomalyCount, a.AnomalyCount);

            InspectionPlan lib = new InspectionPlan(3, new[] { Row("K-1"), Row("L-1", true, true), Row("S-1") }, SpaceId.Library, "S-1");
            InspectionPlan c = NightRun.PatchPlanForProgram(lib, ProgramWith(3, new[] { "S1" }, ProgramCatalog.Footsteps, ProgramCatalog.ToiletGirl, ProgramCatalog.YellowFace));
            int early = 0, late = 0;
            foreach (InspectionAssignment row in c.Assignments)
            {
                if (SpaceIds.Canonical(row.Item.Space) != SpaceId.Library) continue;
                if (row.IsLate) late++;
                else
                {
                    early++;
                    Assert.IsFalse(row.IsAnomaly, "더한 항목은 정상");
                }
            }

            Assert.AreEqual(1, early, "슬롯 C 도서관 — 03:08까지 아껴 둘 이른 항목");
            Assert.AreEqual(1, late, "호출 2의 늦은 항목은 그대로");
            Assert.IsTrue(c.Find("T-3") != null || c.Find("T-1") != null || c.Find("T-2") != null, "슬롯 B 화장실 항목");

            InspectionDispatcher d = new InspectionDispatcher(BoardOf(c), 3, new[]
            {
                new KeyValuePair<EncounterSlot, SpaceId>(EncounterSlot.C, SpaceId.Library)
            });
            Assert.AreEqual(NightClock.SlotCStart, d.ReleaseOf(SpaceId.Library), 0.01f, "슬롯 C까지 아껴 둔다");

            InspectionPlan same = NightRun.PatchPlanForProgram(lib, ProgramWith(3, new[] { "S1" }, ProgramCatalog.Footsteps, ProgramCatalog.YellowFace));
            Assert.AreSame(lib, same, "슬롯 B 방은 늦은 항목으로 충분하다");
        }

        private static InspectionBoard BoardOf(InspectionPlan plan)
        {
            InspectionBoard b = new InspectionBoard();
            b.Begin(plan, true);
            return b;
        }

        [Test]
        public void 방에서_터지는_조우는_그_방에_점검_하나를_더한다()
        {
            // 53차 플레이 점검: 2일차 소년(교실) 날 편성에 교실 점검이 없어 아무도 교실에 가지 않았다.
            InspectionPlan plan = new InspectionPlan(2, new[] { Row("K-1"), Row("S-1", true), Row("L-1") }, SpaceId.None, "L-1");
            InspectionPlan patched = NightRun.PatchPlanForProgram(plan, ProgramWith(2, new[] { "C2", "C3" }, ProgramCatalog.BoyBang));
            Assert.AreNotSame(plan, patched);
            int classroom = 0;
            foreach (InspectionAssignment a in patched.Assignments)
            {
                if (SpaceIds.Canonical(a.Item.Space) == SpaceId.Classroom)
                {
                    classroom++;
                    Assert.IsFalse(a.IsAnomaly);
                }
            }

            Assert.AreEqual(1, classroom);
            Assert.AreEqual(plan.AnomalyCount, patched.AnomalyCount);
        }

        [Test]
        public void 묶은_항목은_총량_보고_정산에서_빠진다()
        {
            InspectionBoard b = new InspectionBoard();
            b.Begin(new InspectionPlan(3, new[] { Row("K-1"), Row("T-1") }, SpaceId.None, string.Empty), true);
            Assert.IsTrue(b.Hold("T-1"));
            Assert.AreEqual(1, b.Total);
            b.MarkIssued("K-1");
            FearAxisSystem axes = new FearAxisSystem();
            Assert.AreEqual(ReportRejection.NotOpenYet, b.Report("T-1", false, 30, axes, SpaceId.Toilet).Rejection);
            Assert.IsTrue(b.Release("T-1"));
            Assert.AreEqual(2, b.Total);
            Assert.IsFalse(b.IsHeld("T-1"));
        }

        [Test]
        public void 목격하면_3초_뒤_그_변기만_단독_지시()
        {
            InspectionBoard b = new InspectionBoard();
            b.Begin(new InspectionPlan(3, new[] { Row("K-1"), Row("H-1"), Row("T-1"), Row("L-1") }, SpaceId.None, string.Empty), true);
            b.Hold("T-1");
            InspectionDispatcher d = new InspectionDispatcher(b, 3, null);
            List<InspectionOrder> orders = new List<InspectionOrder>();
            FearAxisSystem axes = new FearAxisSystem();
            for (int i = 0; i < 6000; i++)
            {
                InspectionOrder o = d.Tick(new DispatchInput { Minute = 20f + i * 0.04f, Dt = 0.1f });
                if (o == null) continue;
                orders.Add(o);
                foreach (string id in o.ItemIds) b.Report(id, false, 30, axes, SpaceId.None);
                d.NoteReport();
            }

            Assert.IsFalse(b.IsIssued("T-1"), "목격 전에는 어떤 지시에도 나오지 않는다");

            int before = orders.Count;
            Assert.IsTrue(d.QueueWitness("T-1"));
            for (int i = 0; i < 29; i++)
            {
                InspectionOrder o = d.Tick(new DispatchInput { Minute = 300f, Dt = 0.1f, DirectorBusy = true });
                if (o != null) orders.Add(o);
            }

            Assert.AreEqual(before, orders.Count, "3초 전");
            for (int i = 0; i < 3; i++)
            {
                InspectionOrder o = d.Tick(new DispatchInput { Minute = 300f, Dt = 0.1f, DirectorBusy = true });
                if (o != null) orders.Add(o);
            }

            Assert.AreEqual(before + 1, orders.Count, "조우 중이어도 낸다");
            InspectionOrder w = orders[orders.Count - 1];
            Assert.AreEqual(OrderKind.Witness, w.Kind);
            CollectionAssert.AreEqual(new[] { "T-1" }, w.ItemIds);
            Assert.IsTrue(b.IsIssued("T-1"));
        }

        // ── K1 ──────────────────────────────────────────────────

        [Test]
        public void K1_조우_단서는_지금_채널을_단다()
        {
            DirectorFixture f = new DirectorFixture(3, 0.99, null, DirectorFixture.Slot(EncounterSlot.A, ProgramCatalog.CctvPerson));
            f.Minute = 70f;
            f.Enter(SpaceId.SecurityRoom);
            f.Send(JudgeSignal.Channel("cctv.ch2"));
            for (int i = 0; i < 40; i++) f.Send(JudgeSignal.CctvView("cctv.ch2", 0.1f)).Wait(0.1f);
            bool found = false;
            foreach (JudgeSignal s in f.Out)
            {
                if (s.Kind == SignalKind.CueStarted && s.TargetId == FinalCues.CctvPerson + "@cctv.ch2") found = true;
            }

            Assert.IsTrue(found, "cue.cctvperson@cctv.ch2");
        }

        // ── 분배 ───────────────────────────────────────────────

        [Test]
        public void 일차별로_무게를_둘_조우()
        {
            Assert.IsTrue(ProgramDirector.IsDayFocus(2, ProgramCatalog.CeilingLegs));
            Assert.IsTrue(ProgramDirector.IsDayFocus(3, ProgramCatalog.ToiletGirl));
            Assert.IsTrue(ProgramDirector.IsDayFocus(3, ProgramCatalog.CctvPerson));
            Assert.IsTrue(ProgramDirector.IsDayFocus(4, ProgramCatalog.PeopleTree));
            Assert.IsTrue(ProgramDirector.IsDayFocus(5, ProgramCatalog.ModelRush));
            Assert.IsFalse(ProgramDirector.IsDayFocus(1, ProgramCatalog.BoySeated), "1일차는 몹이 없다(2026-10-01 민 결정)");
        }

        [Test]
        public void CCTV_사람은_3일차부터()
        {
            for (int seed = 0; seed < 40; seed++)
            {
                ProgramDirector d = new ProgramDirector(new System.Random(seed));
                NightProgram p = d.Build(new ProgramRequest { Day = 2, Shown = FixedBands.All(Band.Band3) });
                Assert.IsFalse(p.HasEncounter(ProgramCatalog.CctvPerson), "seed " + seed);
            }
        }
    }
}

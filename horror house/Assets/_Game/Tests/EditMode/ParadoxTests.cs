using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace NightDuty.Tests
{
    /// <summary>역설·변조(10단계): 편성표 · 안전한 읽기 세 패턴 · 보상 · 재시작 · 태블릿 표시.</summary>
    public sealed class ParadoxTests
    {
        private int _clock;

        [SetUp]
        public void SetUp()
        {
            NightRun.StartNewRun();
            _clock = 0;
        }

        [TearDown]
        public void TearDown()
        {
            NightRun.ProgramEnabled = false;
            NightRun.ParadoxSeed = null;
            NightRun.StartNewRun();
            EventBus.ClearAll();
        }

        private static List<RuleDef> Deck(params string[] ids)
        {
            List<RuleDef> deck = new List<RuleDef>();
            foreach (string id in ids) deck.Add(ProgramCatalog.Rule(id));
            return deck;
        }

        private static ParadoxPlanner Sent(int seed)
        {
            ParadoxPlanner p = new ParadoxPlanner(new Random(seed));
            p.MarkSent();
            return p;
        }

        // ── 편성표 ─────────────────────────────────────────────

        [Test]
        public void 신뢰_구간_0이면_역설이_없다()
        {
            ParadoxPlan plan = new ParadoxPlanner(new Random(1)).Plan(3, Band.Band0, Deck("H2", "C1", "G1"));
            Assert.IsNull(plan.Ambiguous);
            Assert.IsNull(plan.Tampered);
            Assert.IsNull(plan.Blacked);
        }

        [Test]
        public void 이일차_회차_첫_역설은_C2이고_반드시_보낸다()
        {
            ParadoxPlan plan = new ParadoxPlanner(new Random(1)).Plan(2, Band.Band0, Deck("H2", "C2", "G1"));
            Assert.AreEqual("C2", plan.Ambiguous);
            Assert.IsTrue(plan.FirstOfRun);
            Assert.IsTrue(plan.WillSend);
            Assert.AreEqual(SafeReadPattern.EyesOnly, plan.AmbiguousEntry.Pattern);
        }

        [Test]
        public void 회차_첫_역설은_눈으로만_패턴만_고른다()
        {
            for (int seed = 0; seed < 30; seed++)
            {
                ParadoxPlan plan = new ParadoxPlanner(new Random(seed)).Plan(3, Band.Band1, Deck("C1", "C3", "H2", "K3"));
                Assert.AreEqual("H2", plan.Ambiguous, "씨앗 " + seed);
                Assert.IsTrue(plan.WillSend, "첫 역설은 굴리지 않는다");
            }
        }

        [Test]
        public void 신뢰_구간_3_이상이면_CCTV_패턴을_보내지_않는다()
        {
            for (int seed = 0; seed < 30; seed++)
            {
                ParadoxPlan plan = Sent(seed).Plan(3, Band.Band3, Deck("K3", "C1"));
                Assert.AreEqual("C1", plan.Ambiguous, "씨앗 " + seed);
            }
        }

        [Test]
        public void T4_K4는_겨누지도_변조하지도_않는다()
        {
            Assert.IsTrue(ParadoxPlanner.Excluded(ProgramCatalog.ReverseReportRule));
            Assert.IsTrue(ParadoxPlanner.Excluded(ProgramCatalog.FinaleRule));
            Assert.IsFalse(ParadoxPlanner.Excluded("K2"), "K2는 그날 빈 방 채널로 겨눈다");
            for (int seed = 0; seed < 20; seed++)
            {
                ParadoxPlanner p = Sent(seed);
                List<RuleDef> deck = Deck("T4", "G1");
                p.Plan(2, Band.Band4, deck);
                ParadoxPlan plan = p.Plan(3, Band.Band4, deck);
                Assert.IsNull(plan.Ambiguous);
                Assert.IsNull(plan.Tampered);
                Assert.AreNotEqual("T4", plan.Blacked);
            }
        }

        [Test]
        public void 변조는_이전_밤에_본_수칙만_고른다()
        {
            ParadoxPlanner p = Sent(3);
            ParadoxPlan first = p.Plan(2, Band.Band3, Deck("H2", "C1", "G1"));
            Assert.IsNull(first.Tampered, "처음 보는 수칙은 변조하지 않는다");

            ParadoxPlan second = p.Plan(3, Band.Band3, Deck("H2", "C1", "G1"));
            Assert.IsNotNull(second.Tampered);
            Assert.AreNotEqual(second.Ambiguous, second.Tampered, "같은 수칙에 역설과 변조를 함께 걸지 않는다");
            Assert.AreEqual(ParadoxCatalog.Subtle(ProgramCatalog.Rule(second.Tampered).Text), second.TamperText, "구간 3은 미세 변조");
            StringAssert.EndsWith("?.", second.TamperText);
            Assert.IsNull(second.Blacked, "검은 줄은 구간 4만");
        }

        [Test]
        public void 신뢰_구간_4는_붕괴형_변조와_검은_줄()
        {
            ParadoxPlanner p = Sent(5);
            p.Plan(2, Band.Band4, Deck("H2", "C1", "L4", "G1"));
            ParadoxPlan plan = p.Plan(3, Band.Band4, Deck("H2", "C1", "L4", "G1"));
            Assert.IsNotNull(plan.Tampered);
            Assert.AreEqual(ParadoxCatalog.Find(plan.Tampered).Variant, plan.TamperText);
            Assert.IsNotNull(plan.Blacked);
            Assert.AreNotEqual(plan.Tampered, plan.Blacked);
            Assert.AreNotEqual(plan.Ambiguous, plan.Blacked);
            StringAssert.Contains("████", plan.BlackedText);
            StringAssert.StartsWith("<color=" + ParadoxCatalog.BlackedColor, plan.BlackedText, "먹색 막대");
            Assert.AreEqual(plan.TamperText, plan.DisplayTextOf(plan.Tampered));
            Assert.AreEqual(plan.BlackedText, plan.DisplayTextOf(plan.Blacked));
        }

        [Test]
        public void 발송_확률은_이상이_있으면_높고_없으면_낮다()
        {
            int withAnomaly = 0, without = 0;
            for (int seed = 0; seed < 400; seed++)
            {
                if (Sent(seed).Plan(3, Band.Band1, Deck("H2"), s => true).WillSend) withAnomaly++;
                if (Sent(seed).Plan(3, Band.Band1, Deck("H2"), s => false).WillSend) without++;
            }

            Assert.That(withAnomaly, Is.InRange(260, 340), "75%");
            Assert.That(without, Is.InRange(60, 140), "25%");
        }

        // ── 안전한 읽기 ─────────────────────────────────────────

        private static ParadoxRun Run(string ruleId)
        {
            return new ParadoxRun(new ParadoxPlan(Band.Band1, ruleId, false, true, null, null, null, null, "시험"));
        }

        [Test]
        public void 눈으로만_대상을_1초_보면_안전한_읽기()
        {
            ParadoxRun r = Run("H1");
            Assert.AreEqual(ParadoxStep.None, r.Observe(JudgeSignal.Gaze(FinalCues.H1Object, 0.5f), SpaceId.Corridor, false), "문자 전에는 세지 않는다");
            Assert.AreEqual(ParadoxStep.Sent, r.Observe(JudgeSignal.OfSpace(SignalKind.SpaceEntered, SpaceId.Corridor), SpaceId.Corridor, false));
            for (int i = 0; i < 9; i++) Assert.AreEqual(ParadoxStep.None, r.Observe(JudgeSignal.Gaze(FinalCues.H1Object, 0.1f), SpaceId.Corridor, false));
            Assert.AreEqual(ParadoxStep.SafeRead, r.Observe(JudgeSignal.Gaze(FinalCues.H1Object, 0.1f), SpaceId.Corridor, false));
            Assert.IsTrue(r.SafeRead);
        }

        [Test]
        public void 대상이_없는_눈으로만은_그_공간에_1초_머물면_된다()
        {
            ParadoxRun r = Run("H2");
            r.Observe(JudgeSignal.OfSpace(SignalKind.SpaceEntered, SpaceId.Corridor), SpaceId.Corridor, false);
            Assert.IsTrue(r.Sent);
            r.Observe(JudgeSignal.Tick(0.5f), SpaceId.Classroom, false);
            Assert.IsFalse(r.SafeRead, "다른 공간");
            r.Observe(JudgeSignal.Tick(0.5f), SpaceId.Corridor, false);
            Assert.AreEqual(ParadoxStep.SafeRead, r.Observe(JudgeSignal.Tick(0.5f), SpaceId.Corridor, false));
        }

        [Test]
        public void 멈춰서는_단서가_끝날_때까지_지키면_된다()
        {
            ParadoxRun r = Run("C1");
            Assert.AreEqual(ParadoxStep.None, r.Observe(JudgeSignal.OfSpace(SignalKind.SpaceEntered, SpaceId.Classroom), SpaceId.Classroom, false), "단서가 있는 수칙은 단서에 보낸다");
            Assert.AreEqual(ParadoxStep.Sent, r.Observe(JudgeSignal.Cue(FinalCues.Chalk + "@x", default), SpaceId.Corridor, false));
            Assert.AreEqual(ParadoxStep.None, r.Observe(JudgeSignal.CueEnd(FinalCues.Chalk), SpaceId.Corridor, true), "어겼으면 아니다");
            Assert.AreEqual(ParadoxStep.SafeRead, r.Observe(JudgeSignal.CueEnd(FinalCues.Chalk), SpaceId.Corridor, false));
        }

        [Test]
        public void 멈춰서는_위협_대응_성공으로도_된다()
        {
            ParadoxRun r = Run("H3");
            r.Observe(JudgeSignal.Cue(FinalCues.Footsteps, default), SpaceId.Corridor, false);
            Assert.AreEqual(ParadoxStep.None, r.NoteThreatKept("C3"));
            Assert.AreEqual(ParadoxStep.SafeRead, r.NoteThreatKept("H3"));
        }

        [Test]
        public void CCTV로는_그_공간_채널을_2초_보면_된다()
        {
            ParadoxRun r = Run("K3");
            Assert.AreEqual(SpaceId.Corridor, r.Space);
            Assert.AreEqual(ParadoxStep.Sent, r.Observe(JudgeSignal.OfSpace(SignalKind.SpaceEntered, SpaceId.SecurityRoom), SpaceId.SecurityRoom, false));
            for (int i = 0; i < 30; i++) r.Observe(JudgeSignal.CctvView("cctv.ch1", 0.1f), SpaceId.SecurityRoom, false);
            Assert.IsFalse(r.SafeRead, "다른 채널");
            for (int i = 0; i < 19; i++) r.Observe(JudgeSignal.CctvView("cctv.ch0", 0.1f), SpaceId.SecurityRoom, false);
            Assert.IsFalse(r.SafeRead, "1.9초");
            Assert.AreEqual(ParadoxStep.SafeRead, r.Observe(JudgeSignal.CctvView("cctv.ch0", 0.1f), SpaceId.SecurityRoom, false));
        }

        [Test]
        public void 보류된_역설은_보내지_않는다()
        {
            ParadoxRun r = new ParadoxRun(new ParadoxPlan(Band.Band1, "H2", false, false, null, null, null, null, "보류"));
            Assert.AreEqual(ParadoxStep.None, r.Observe(JudgeSignal.OfSpace(SignalKind.SpaceEntered, SpaceId.Corridor), SpaceId.Corridor, false));
            Assert.IsFalse(r.Sent);
        }

        [Test]
        public void 이미_어긴_수칙에는_문자를_보내지_않는다()
        {
            ParadoxRun r = Run("H2");
            Assert.AreEqual(ParadoxStep.None, r.Observe(JudgeSignal.OfSpace(SignalKind.SpaceEntered, SpaceId.Corridor), SpaceId.Corridor, true));
            Assert.IsFalse(r.Sent);
        }

        // ── NightRun 연결 ────────────────────────────────────────

        private static string FirstMessageRule(bool nonThreat)
        {
            foreach (RuleDef def in NightRun.Program.Deck)
            {
                ParadoxEntry e = ParadoxCatalog.Find(def.Id);
                if (e == null || !e.HasMessage || NightRun.FinalRules.Judge(def.Id) == null) continue;
                if (nonThreat && def.IsThreat) continue;
                return def.Id;
            }

            return null;
        }

        private string OpenWithMessage()
        {
            for (int attempt = 0; attempt < 20; attempt++)
            {
                TestKit.BeginProgramNight(3, () => _clock);
                string id = FirstMessageRule(true);
                if (id != null) return id;
                NightRun.StartNewRun();
            }

            Assert.Inconclusive("20번 편성해도 문자가 있는 일반 수칙이 덱에 없음");
            return null;
        }

        [Test]
        public void 문자를_받으면_방아쇠가_오고_지키면_신뢰_3()
        {
            string id = OpenWithMessage();
            int messages = 0;
            ParadoxMessage got = default;
            EventBus.MessageSent += m =>
            {
                messages++;
                got = m;
            };

            Assert.IsTrue(NightRun.DebugSendParadox(id));
            Assert.AreEqual(1, messages);
            Assert.AreEqual(id, got.CardId);
            Assert.AreEqual(ParadoxCatalog.Find(id).Message, got.Text);
            Assert.IsTrue(NightRun.FinalRules.Judge(id).Triggered);
            Assert.IsTrue(NightRun.Paradoxes.SentInRun);

            NightRun.RequestEndNight();
            bool found = false;
            foreach (FinalRuleResult r in NightRun.FinalResults)
            {
                if (r.RuleId != id) continue;
                Assert.AreEqual(FinalOutcome.Complied, r.Outcome);
                Assert.AreEqual(Deltas.TrustParadoxKept, r.Delta);
                found = true;
            }

            Assert.IsTrue(found, "밤 종료 준수가 정산된다");
            foreach (DutyLogEntry e in NightRun.LastSummary.DutyLog)
            {
                if (e.RuleId == id) Assert.IsTrue(e.Instructed);
                else Assert.IsFalse(e.Instructed);
            }
        }

        [Test]
        public void 안전한_읽기는_그_공간_점검줄에_이상_여부를_적는다()
        {
            bool was = NightRun.InspectionsEnabled;
            NightRun.InspectionsEnabled = true;
            try
            {
                SafeReadCore();
            }
            finally
            {
                NightRun.InspectionsEnabled = was;
            }
        }

        private void SafeReadCore()
        {
            string id = OpenWithMessage();
            SafeReadReveal? got = null;
            EventBus.SafeReadConfirmed += r => got = r;

            Assert.IsTrue(NightRun.DebugSendParadox(id));
            Assert.IsTrue(NightRun.DebugSafeRead());
            Assert.IsTrue(got.HasValue);
            Assert.AreEqual(NightRun.Paradox.Space, got.Value.Space);

            InspectionPlan plan = NightRun.Inspections.Plan;
            Assert.Greater(plan.Assignments.Count, 0, "점검 편성이 있어야 시험이 된다");
            int inSpace = 0;
            for (int i = 0; i < plan.Assignments.Count; i++)
            {
                if (SpaceIds.Canonical(plan.Assignments[i].Item.Space) == got.Value.Space) inSpace++;
            }

            string text = NightRun.ChecklistMessage;
            Assert.AreEqual(inSpace, Count(text, "· " + ParadoxRun.RevealAnomaly) + Count(text, "· " + ParadoxRun.RevealClear), "그 공간 줄에만 붙는다");
            if (inSpace > 0) StringAssert.Contains("· " + got.Value.Label, text);
        }

        private static int Count(string text, string part)
        {
            int n = 0;
            for (int i = text.IndexOf(part, StringComparison.Ordinal); i >= 0; i = text.IndexOf(part, i + part.Length, StringComparison.Ordinal)) n++;
            return n;
        }

        [Test]
        public void 점검표는_수칙_탭이_아니라_점검_지시_문자로_간다()
        {
            bool was = NightRun.InspectionsEnabled;
            NightRun.InspectionsEnabled = true;
            try
            {
                TestKit.BeginProgramNight(2, () => _clock);
                foreach (RuleSO card in NightRun.TodayDeck)
                {
                    StringAssert.DoesNotStartWith(InspectionCatalog.TargetPrefix, card.CardId, "수칙 탭에는 수칙만");
                }

                InspectionPlan plan = NightRun.Inspections.Plan;
                string text = NightRun.ChecklistMessage;
                StringAssert.StartsWith("[점검 지시]", text);
                StringAssert.Contains(NightRun.InspectionHowTo, text);
                for (int i = 0; i < plan.Assignments.Count; i++)
                {
                    StringAssert.Contains((i + 1) + ". ", text);
                    StringAssert.Contains(plan.Assignments[i].Item.TabletLine, text);
                }

                InspectionAssignment first = plan.Assignments[0];
                NightRun.ReportInspection(first.Id, false);
                StringAssert.Contains(first.Item.TabletLine + (first.IsLate ? " (02:16부터)" : string.Empty) + " · 보고함", NightRun.ChecklistMessage);
            }
            finally
            {
                NightRun.InspectionsEnabled = was;
            }
        }

        [Test]
        public void 재시작해도_받은_문자는_남고_방아쇠도_남는다()
        {
            string id = OpenWithMessage();
            Assert.IsTrue(NightRun.DebugSendParadox(id));
            NightRun.DebugForceCapture(FearAxis.Auditory);
            Assert.AreNotEqual(RestartKind.None, NightRun.RestartAfterCapture().Kind);

            Assert.IsTrue(NightRun.Paradox.Sent);
            Assert.IsTrue(NightRun.FinalRules.Judge(id).Triggered, "밤 시작 스냅샷이 되돌려도 다시 표시");
        }

        [Test]
        public void 태블릿에는_변조본과_검은_줄이_보이고_판정은_원본()
        {
            NightRun.DebugAddAxis(FearAxis.Trust, 70);
            for (int day = 1; day <= 4; day++)
            {
                TestKit.BeginProgramNight(day, () => _clock);
                ParadoxPlan plan = NightRun.Paradox.Plan;
                Assert.AreEqual(Band.Band4, plan.TrustBand);
                foreach (RuleSO card in NightRun.TodayDeck)
                {
                    RuleDef def = ProgramCatalog.Rule(card.CardId);
                    if (def == null) continue;
                    string shown = plan.DisplayTextOf(def.Id);
                    Assert.AreEqual(shown ?? def.Text, card.PlayerText, def.Id);
                }

                if (plan.Tampered != null) Assert.AreEqual(Deltas.TrustVariantKept, NightRun.FinalRules.KeepRewardOf(plan.Tampered));
                NightRun.RequestEndNight();
            }
        }

        // ── K2 역설 · CCTV 패턴의 빈 방 채널 ─────────────────────

        [Test]
        public void K2_역설은_그날_빈_방_채널을_부르고_잠깐_보고_넘기면_안전한_읽기()
        {
            ParadoxRun r = new ParadoxRun(new ParadoxPlan(Band.Band1, "K2", false, true, null, null, null, null, "시험", null, 2));
            Assert.AreEqual("공실 CAM03에 움직임이 있습니다. 즉시 확인하십시오.", r.Message);
            Assert.AreEqual(SpaceId.ScienceRoom, r.Space, "채널 2 = 과학실");
            Assert.AreEqual(ParadoxStep.Sent, r.Observe(JudgeSignal.Cue(FinalCues.EmptyRoom + "@cctv.ch2", default), SpaceId.SecurityRoom, false));
            r.Observe(JudgeSignal.CctvView("cctv.ch2", 0.3f), SpaceId.SecurityRoom, false);
            Assert.AreEqual(ParadoxStep.None, r.Observe(JudgeSignal.Channel("cctv.ch1"), SpaceId.SecurityRoom, false), "0.3초는 아직 못 봤다");
            r.Observe(JudgeSignal.CctvView("cctv.ch2", 0.3f), SpaceId.SecurityRoom, false);
            Assert.AreEqual(ParadoxStep.SafeRead, r.Observe(JudgeSignal.Channel("cctv.ch3"), SpaceId.SecurityRoom, false));
        }

        [Test]
        public void CCTV로_역설은_그날_빈_방_채널_공간을_겨누지_않는다()
        {
            for (int seed = 0; seed < 30; seed++)
            {
                ParadoxPlan plan = Sent(seed).Plan(3, Band.Band1, Deck("K3", "C1"), null, null, 0);   // K3은 복도 채널(0)을 본다
                Assert.AreEqual("C1", plan.Ambiguous, "씨앗 " + seed);
            }
        }

        // ── 회피 불가 역설: 편성 ───────────────────────────────────

        [Test]
        public void 회피_불가는_신뢰_3이면_가짜_4면_진짜_회차_첫_회피_불가는_가짜()
        {
            ParadoxPlanner p = new ParadoxPlanner(new Random(1));
            Assert.IsNull(p.PlanUnavoidable(3, Band.Band2, null), "구간 2 이하는 없음");
            UnavoidableDef fake = p.PlanUnavoidable(3, Band.Band3, null);
            Assert.IsNotNull(fake);
            Assert.IsFalse(fake.Real);
            Assert.IsFalse(p.PlanUnavoidable(3, Band.Band4, null).Real, "회차의 첫 회피 불가는 가짜");
            p.MarkUnavoidable();
            Assert.IsTrue(p.PlanUnavoidable(3, Band.Band4, null).Real);
        }

        [Test]
        public void 점검이_없으면_점검과_맞물린_쌍은_고르지_않는다()
        {
            Assert.IsFalse(UnavoidableCatalog.Feasible(UnavoidableCatalog.Find("S2xScience"), 3, null));
            Assert.IsFalse(UnavoidableCatalog.Feasible(UnavoidableCatalog.Find("T2xT-1"), 3, null));
            Assert.IsTrue(UnavoidableCatalog.Feasible(UnavoidableCatalog.Find("S5xH3"), 3, null));
            Assert.IsFalse(UnavoidableCatalog.Feasible(UnavoidableCatalog.Find("K1xK-1"), 5, null), "5일차 경비실은 K4");
            for (int seed = 0; seed < 20; seed++)
            {
                ParadoxPlanner p = new ParadoxPlanner(new Random(seed));
                Assert.AreEqual("H4xT1", p.PlanUnavoidable(3, Band.Band3, null).Id, "점검이 없으면 가짜는 나서며 쌍뿐");
                p.MarkUnavoidable();
                Assert.AreEqual("S5xH3", p.PlanUnavoidable(3, Band.Band4, null).Id, "점검이 없으면 진짜는 겹침 쌍뿐");
            }
        }

        [Test]
        public void 회피_불가_쌍의_수칙에는_역설_변조_검은_줄을_걸지_않는다()
        {
            UnavoidableDef def = UnavoidableCatalog.Find("H4xT1");
            for (int seed = 0; seed < 30; seed++)
            {
                ParadoxPlanner p = Sent(seed);
                p.Plan(2, Band.Band4, Deck("T1", "H2", "G1", "L4"));
                ParadoxPlan plan = p.Plan(3, Band.Band4, Deck("T1", "H2", "G1", "L4"), null, def);
                Assert.AreNotEqual("T1", plan.Ambiguous);
                Assert.AreNotEqual("T1", plan.Tampered);
                Assert.AreNotEqual("T1", plan.Blacked);
                Assert.AreSame(def, plan.Unavoidable);
            }
        }

        // ── 회피 불가 역설: 진행 ───────────────────────────────────

        [Test]
        public void 나가라_쌍은_점검이_남았을_때만_걸리고_진짜에서_나가면_재입실_금지()
        {
            UnavoidableRun r = new UnavoidableRun(UnavoidableCatalog.Find("S2xScience"));
            bool pending = false;
            Func<SpaceId, bool> space = s => pending;
            Assert.AreEqual(UnavoidableStep.None, r.Observe(JudgeSignal.Cue(FinalCues.Glass, default), SpaceId.ScienceRoom, space, null), "점검이 끝났으면 평소 S2");
            pending = true;
            Assert.AreEqual(UnavoidableStep.None, r.Observe(JudgeSignal.Cue(FinalCues.Glass, default), SpaceId.Corridor, space, null), "그 공간 밖");
            Assert.AreEqual(UnavoidableStep.Staged, r.Observe(JudgeSignal.Cue(FinalCues.Glass, default), SpaceId.ScienceRoom, space, null));
            StringAssert.EndsWith(UnavoidableCatalog.NoReentry, r.Def.Message);
            Assert.AreEqual(UnavoidableStep.Banned, r.Observe(JudgeSignal.OfSpace(SignalKind.SpaceExited, SpaceId.ScienceRoom), SpaceId.ScienceRoom, space, null));
            Assert.AreEqual(SpaceId.ScienceRoom, r.Banned);
        }

        [Test]
        public void 점검을_마치고_나가면_재입실_금지가_아니다()
        {
            UnavoidableRun r = new UnavoidableRun(UnavoidableCatalog.Find("L2xLibrary"));
            bool pending = true;
            r.Observe(JudgeSignal.Cue(FinalCues.Pages, default), SpaceId.Library, s => pending, null);
            Assert.IsTrue(r.Staged);
            pending = false;   // 청각 +12를 내주고 점검을 끝냈다
            Assert.AreEqual(UnavoidableStep.None, r.Observe(JudgeSignal.OfSpace(SignalKind.SpaceExited, SpaceId.Library), SpaceId.Library, s => pending, null));
            Assert.AreEqual(SpaceId.None, r.Banned);
        }

        [Test]
        public void 항목_쌍은_그_항목이_남았을_때만_걸린다()
        {
            UnavoidableRun r = new UnavoidableRun(UnavoidableCatalog.Find("K1xK-1"));
            Assert.AreEqual(UnavoidableStep.None, r.Observe(JudgeSignal.Cue(FinalCues.CctvPerson, default), SpaceId.SecurityRoom, null, id => false));
            Assert.AreEqual(UnavoidableStep.Staged, r.Observe(JudgeSignal.Cue(FinalCues.CctvPerson, default), SpaceId.SecurityRoom, null, id => id == "K-1"));
            Assert.IsFalse(r.Def.Real);
        }

        [Test]
        public void 나서며_쌍은_물_내림_뒤_15초_안에_복도로_나설_때_걸린다()
        {
            UnavoidableRun r = new UnavoidableRun(UnavoidableCatalog.Find("H4xT1"));
            r.Observe(JudgeSignal.Cue(FinalCues.Flush, default), SpaceId.Toilet, null, null);
            r.Observe(JudgeSignal.Tick(5f), SpaceId.Toilet, null, null);
            Assert.AreEqual(UnavoidableStep.Staged, r.Observe(JudgeSignal.OfSpace(SignalKind.SpaceEntered, SpaceId.Corridor), SpaceId.Corridor, null, null));

            UnavoidableRun late = new UnavoidableRun(UnavoidableCatalog.Find("H4xT1"));
            late.Observe(JudgeSignal.Cue(FinalCues.Flush, default), SpaceId.Toilet, null, null);
            late.Observe(JudgeSignal.Tick(16f), SpaceId.Toilet, null, null);
            Assert.AreEqual(UnavoidableStep.None, late.Observe(JudgeSignal.OfSpace(SignalKind.SpaceEntered, SpaceId.Corridor), SpaceId.Corridor, null, null));
        }

        [Test]
        public void 겹침_쌍은_복도_끝_단서에_걸린다()
        {
            UnavoidableRun r = new UnavoidableRun(UnavoidableCatalog.Find("S5xH3"));
            Assert.AreEqual(UnavoidableStep.Staged, r.Observe(JudgeSignal.Cue(FinalCues.HallEnd, default), SpaceId.ScienceRoom, null, null));
            Assert.AreEqual(ProgramCatalog.Footsteps, r.Def.ChainEncounter);
        }

        // ── 긴장 디렉터 ───────────────────────────────────────────

        private static int RuleCues(TensionDirector d, string cue)
        {
            int n = 0;
            JudgeSignal s;
            while (d.TryDequeue(out s))
            {
                if (s.Kind == SignalKind.CueStarted && ParadoxRun.CueId(s.TargetId) == cue) n++;
            }

            return n;
        }

        private static void Dwell(TensionDirector d, SpaceId space, float seconds)
        {
            d.Observe(JudgeSignal.OfSpace(SignalKind.SpaceEntered, space));
            for (float t = 0f; t < seconds; t += 0.5f) d.Tick(70f, 0.5f, 0, Band.Band0, false);
        }

        [Test]
        public void 나가라_신호는_평소_그_공간_점검이_남았으면_울리지_않는다()
        {
            NightProgram program = new NightProgram(3, null, Deck("S2"), string.Empty);
            TensionDirector d = new TensionDirector(program, 3, 0, new Random(1));
            d.InspectionPendingIn = s => true;
            Dwell(d, SpaceId.ScienceRoom, 20f);
            Assert.AreEqual(0, RuleCues(d, FinalCues.Glass));

            TensionDirector u = new TensionDirector(program, 3, 0, new Random(1));
            u.InspectionPendingIn = s => true;
            u.SetUnavoidable(UnavoidableCatalog.Find("S2xScience"));
            Dwell(u, SpaceId.ScienceRoom, 20f);
            Assert.AreEqual(1, RuleCues(u, FinalCues.Glass), "회피 불가 밤에는 점검이 남아도 울린다");
        }

        [Test]
        public void 둘째_조우는_제_슬롯에서_걸지_않고_겹칠_때_곧장_대면한다()
        {
            NightProgram program = new NightProgram(3, new[] { new SlotEncounter(EncounterSlot.A, ProgramCatalog.Encounter(ProgramCatalog.Footsteps), false) }, Deck("H3"), string.Empty);
            TensionDirector d = new TensionDirector(program, 3, 0, new Random(1));
            d.SetUnavoidable(UnavoidableCatalog.Find("S5xH3"));
            d.Observe(JudgeSignal.OfSpace(SignalKind.SpaceEntered, SpaceId.Corridor));
            for (int i = 0; i < 200; i++)
            {
                d.Observe(JudgeSignal.Pose(new UnityEngine.Vector3(i * 0.2f, 0f, 0f), 90f));
                d.Tick(70f, 0.5f, 0, Band.Band0, false);
            }

            Assert.AreEqual(0, RuleCues(d, FinalCues.Footsteps), "제 슬롯에서는 걸지 않는다");
            Assert.IsTrue(d.ChainEncounter(ProgramCatalog.Footsteps));
            Assert.AreEqual(1, RuleCues(d, FinalCues.Footsteps));
        }

        [Test]
        public void K2_단서는_그날_빈_방_채널을_가리킨다()
        {
            NightProgram program = new NightProgram(3, null, Deck("K2"), string.Empty);
            TensionDirector d = new TensionDirector(program, 3, 0, new Random(1));
            d.EmptyRoomChannel = "cctv.ch4";
            d.Observe(JudgeSignal.OfSpace(SignalKind.SpaceEntered, SpaceId.SecurityRoom));
            string got = null;
            for (int i = 0; i < 40 && got == null; i++)
            {
                d.Observe(JudgeSignal.CctvView("cctv.ch1", 0.5f));
                d.Tick(70f, 0.5f, 0, Band.Band0, false);
                JudgeSignal s;
                while (d.TryDequeue(out s)) if (s.Kind == SignalKind.CueStarted) got = s.TargetId;
            }

            Assert.AreEqual(FinalCues.EmptyRoom + "@cctv.ch4", got);
        }

        // ── NightRun 연결 ────────────────────────────────────────

        [Test]
        public void 진짜_나가라_쌍에서_나가면_그_공간_보고를_받지_않고_점검_지시에_재입실_불가()
        {
            bool was = NightRun.InspectionsEnabled;
            NightRun.InspectionsEnabled = true;
            try
            {
                UnavoidableDef def = null;
                for (int attempt = 0; attempt < 40 && def == null; attempt++)
                {
                    NightRun.StartNewRun();
                    NightRun.ParadoxSeed = attempt;
                    NightRun.StartNewRun();
                    NightRun.DebugAddAxis(FearAxis.Trust, 70);
                    NightRun.Paradoxes.MarkUnavoidable();
                    TestKit.BeginProgramNight(3, () => _clock);
                    UnavoidableDef d = NightRun.Unavoidable.Def;
                    if (d != null && d.Real && d.Kind == UnavoidableKind.ExitBeforeInspection) def = d;
                }

                if (def == null) Assert.Inconclusive("40번 편성해도 진짜 「나가라」 쌍이 걸리지 않음");
                StringAssert.Contains(def.Rules[0], NightRun.Program.ToString(), "쌍의 수칙을 강제 편성");

                int messages = 0;
                EventBus.MessageSent += m => { if (m.Text == def.Message) messages++; };
                NightRun.Send(JudgeSignal.OfSpace(SignalKind.SpaceEntered, def.Space));
                NightRun.Send(JudgeSignal.Cue(def.Cue, default));
                Assert.AreEqual(1, messages, "점검이 남은 채 단서 → 문자");
                NightRun.Send(JudgeSignal.OfSpace(SignalKind.SpaceExited, def.Space));
                Assert.AreEqual(def.Space, NightRun.BannedSpace);

                InspectionAssignment pending = null;
                foreach (InspectionAssignment a in NightRun.Inspections.Plan.Assignments)
                {
                    if (SpaceIds.Canonical(a.Item.Space) == def.Space && NightRun.Inspections.StateOf(a.Id) == InspectionState.Pending) pending = a;
                }

                Assert.IsNotNull(pending);
                InspectionReport report = NightRun.ReportInspection(pending.Id, false);
                Assert.IsFalse(report.Accepted);
                Assert.AreEqual(ReportRejection.SpaceClosed, report.Rejection);
                StringAssert.Contains(pending.Item.TabletLine + (pending.IsLate ? " (02:16부터)" : string.Empty) + " · 재입실 불가", NightRun.ChecklistMessage);
            }
            finally
            {
                NightRun.InspectionsEnabled = was;
            }
        }

        [Test]
        public void 어긴_수칙_줄에는_얼룩이_번지고_내용은_그대로()
        {
            TestKit.BeginProgramNight(2, () => _clock);
            TestKit.ViolateRunning();
            RuleSO g1 = null;
            foreach (RuleSO card in NightRun.TodayDeck) if (card.CardId == "G1") g1 = card;
            Assert.IsNotNull(g1);
            // 51차 민: 「태블릿에 수칙 어긴 것을 표시하지 말 것」 — 기본은 얼룩 없음(스위치를 켜면 옛 표시).
            Assert.IsFalse(NightRun.ShowViolationStain);
            Assert.AreEqual(ProgramCatalog.Rule("G1").Text, g1.PlayerText, "위반해도 수칙 줄은 그대로");
        }

        [Test]
        public void 근무일지_표시는_불가피가_지시를_따름보다_앞선다()
        {
            DutyLogEntry e = new DutyLogEntry(1, "S2", SpaceId.ScienceRoom, "x", RuleVerdict.Violated, true, string.Empty, true);
            Assert.AreEqual(DutyMark.Unavoidable, e.Mark);
            Assert.AreEqual(DutyMark.Instructed, new DutyLogEntry(1, "S2", SpaceId.ScienceRoom, "x", RuleVerdict.Violated, true).Mark);
            Assert.AreEqual(DutyMark.None, new DutyLogEntry(1, "S2", SpaceId.ScienceRoom, "x", RuleVerdict.Complied, true, string.Empty, true).Mark);
        }

        [Test]
        public void 민_수정_몹을_예고하던_역설은_폐기되고_S2는_눈으로만()
        {
            foreach (string id in new[] { "C3", "C5", "L2", "L5", "K1" }) Assert.IsFalse(ParadoxCatalog.Find(id).HasMessage, id);
            Assert.AreEqual(SafeReadPattern.EyesOnly, ParadoxCatalog.Find("S2").Pattern);
            Assert.IsFalse(ParadoxCatalog.Find("S3").HasVariant);
            Assert.AreEqual("인체 모형에는 빛을 비추지 마십시오.", ProgramCatalog.Rule("S3").Text, "51차 민: 미션처럼 읽히던 「3초간 비추라」 폐기");
            Assert.AreEqual("근무 종료 후에는 경비실을 나가지 마십시오.", ProgramCatalog.Rule("K4").Text);
        }
    }
}

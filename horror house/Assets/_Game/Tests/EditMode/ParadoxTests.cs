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
                ParadoxPlan plan = new ParadoxPlanner(new Random(seed)).Plan(3, Band.Band1, Deck("C1", "C3", "H2", "S2"));
                Assert.AreEqual("H2", plan.Ambiguous, "씨앗 " + seed);
                Assert.IsTrue(plan.WillSend, "첫 역설은 굴리지 않는다");
            }
        }

        [Test]
        public void 신뢰_구간_3_이상이면_CCTV_패턴을_보내지_않는다()
        {
            for (int seed = 0; seed < 30; seed++)
            {
                ParadoxPlan plan = Sent(seed).Plan(3, Band.Band3, Deck("S2", "L2", "K3", "C1"));
                Assert.AreEqual("C1", plan.Ambiguous, "씨앗 " + seed);
            }
        }

        [Test]
        public void T4_K4_K2는_겨누지도_변조하지도_않는다()
        {
            Assert.IsTrue(ParadoxPlanner.Excluded(ProgramCatalog.ReverseReportRule));
            Assert.IsTrue(ParadoxPlanner.Excluded(ProgramCatalog.FinaleRule));
            Assert.IsTrue(ParadoxPlanner.Excluded("K2"));
            for (int seed = 0; seed < 20; seed++)
            {
                ParadoxPlanner p = Sent(seed);
                List<RuleDef> deck = Deck("T4", "K2", "G1");
                p.Plan(2, Band.Band4, deck);
                ParadoxPlan plan = p.Plan(3, Band.Band4, deck);
                Assert.IsNull(plan.Ambiguous);
                Assert.IsNull(plan.Tampered);
                Assert.AreNotEqual("T4", plan.Blacked);
                Assert.AreNotEqual("K2", plan.Blacked);
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
            StringAssert.StartsWith("████", plan.BlackedText);
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
            ParadoxRun r = Run("C3");
            Assert.AreEqual(ParadoxStep.None, r.Observe(JudgeSignal.OfSpace(SignalKind.SpaceEntered, SpaceId.Classroom), SpaceId.Classroom, false), "단서가 있는 수칙은 단서에 보낸다");
            Assert.AreEqual(ParadoxStep.Sent, r.Observe(JudgeSignal.Cue(FinalCues.BoySeated + "@x", default), SpaceId.Classroom, false));
            Assert.AreEqual(ParadoxStep.None, r.Observe(JudgeSignal.CueEnd(FinalCues.BoySeated), SpaceId.Classroom, true), "어겼으면 아니다");
            Assert.AreEqual(ParadoxStep.SafeRead, r.Observe(JudgeSignal.CueEnd(FinalCues.BoySeated), SpaceId.Classroom, false));
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
            for (int i = 0; i < plan.Assignments.Count; i++)
            {
                InspectionAssignment a = plan.Assignments[i];
                RuleSO card = null;
                foreach (RuleSO c in NightRun.TodayDeck) if (c != null && c.CardId == a.Item.TargetId) card = c;
                Assert.IsNotNull(card);
                bool inSpace = SpaceIds.Canonical(a.Item.Space) == got.Value.Space;
                if (inSpace) StringAssert.EndsWith(got.Value.Label, card.PlayerText);
                else
                {
                    StringAssert.DoesNotContain(ParadoxRun.RevealAnomaly, card.PlayerText);
                    StringAssert.DoesNotContain(ParadoxRun.RevealClear, card.PlayerText);
                }
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
    }
}

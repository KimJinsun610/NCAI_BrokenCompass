using NUnit.Framework;

namespace NightDuty.Tests
{
    /// <summary>5일차 피날레 판정(11단계): K4(경비실을 나가지 않기) · 「봤다」(창밖 남자 2초) · CCTV 채널 · G3 빈칸 · 결말 정산.</summary>
    public sealed class FinaleTests
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
            NightRun.StartNewRun();
            EventBus.ClearAll();
        }

        private void OpenFinale()
        {
            TestKit.BeginProgramNight(FinaleWatch.Day, () => _clock);
            NightRun.Send(JudgeSignal.OfSpace(SignalKind.SpaceEntered, SpaceId.SecurityRoom));
            Assert.IsTrue(NightRun.BeginFinale());
        }

        private static void Gaze(string target, int samples)
        {
            for (int i = 0; i < samples; i++) NightRun.Send(JudgeSignal.Gaze(target, 0.1f));
        }

        [Test]
        public void 오일차가_아니면_피날레를_열지_않는다()
        {
            TestKit.BeginProgramNight(4, () => _clock);
            Assert.IsFalse(NightRun.BeginFinale());
            Assert.IsFalse(NightRun.Finale.Active);
        }

        [Test]
        public void 피날레는_한번만_열린다()
        {
            OpenFinale();
            Assert.IsFalse(NightRun.BeginFinale());
            Assert.AreEqual(1, NightRun.Finale.Attempt);
        }

        [Test]
        public void 경비실을_나가면_K4_위반이고_축은_오르지_않는다()
        {
            OpenFinale();
            int before = NightRun.Axes.GetValue(FearAxis.Auditory) + NightRun.Axes.GetValue(FearAxis.Illuminance) + NightRun.Axes.GetValue(FearAxis.Layout);
            int fired = 0;
            NightRun.Finale.Violation += () => fired++;

            NightRun.Send(JudgeSignal.OfSpace(SignalKind.SpaceExited, SpaceId.SecurityRoom));
            NightRun.Send(JudgeSignal.OfSpace(SignalKind.SpaceEntered, SpaceId.Corridor));

            Assert.IsTrue(NightRun.Finale.Violated);
            Assert.AreEqual(1, fired, "한 시도에 한 번만");
            int after = NightRun.Axes.GetValue(FearAxis.Auditory) + NightRun.Axes.GetValue(FearAxis.Illuminance) + NightRun.Axes.GetValue(FearAxis.Layout);
            Assert.AreEqual(before, after, "K4에는 축이 없다");
            Assert.IsTrue(NightRun.IsNightActive, "피날레 처음부터 — 밤은 닫히지 않는다");
        }

        [Test]
        public void 다시_하면_위반_봤다_채널을_지우고_시도가_는다()
        {
            OpenFinale();
            NightRun.Send(JudgeSignal.Beam(FinaleWatch.WindowTarget, 0.1f));
            NightRun.Send(JudgeSignal.Channel("cctv.ch1"));
            NightRun.Send(JudgeSignal.OfSpace(SignalKind.SpaceExited, SpaceId.SecurityRoom));

            NightRun.RestartFinale();

            Assert.IsFalse(NightRun.Finale.Violated);
            Assert.IsFalse(NightRun.Finale.Seen);
            Assert.AreEqual(0, NightRun.Finale.ChannelsSeen);
            Assert.AreEqual(2, NightRun.Finale.Attempt);
        }

        [Test]
        public void 창밖_남자를_2초_이어서_보면_봤다()
        {
            OpenFinale();
            Gaze(FinaleWatch.WindowTarget, 19);
            Assert.IsFalse(NightRun.Finale.Seen, "1.9초");
            Gaze(FinaleWatch.WindowTarget, 1);
            Assert.IsTrue(NightRun.Finale.Seen, "2.0초");
        }

        [Test]
        public void 응시가_끊기면_처음부터_센다()
        {
            OpenFinale();
            Gaze(FinaleWatch.WindowTarget, 15);
            Gaze(string.Empty, 3);   // 0.3초 틈(허용 0.2초)
            Gaze(FinaleWatch.WindowTarget, 15);
            Assert.IsFalse(NightRun.Finale.Seen);
        }

        [Test]
        public void 비추면_바로_봤다()
        {
            OpenFinale();
            NightRun.Send(JudgeSignal.Beam(FinaleWatch.WindowTarget, 0.1f));
            Assert.IsTrue(NightRun.Finale.Seen);
        }

        [Test]
        public void 넘겨_본_CCTV_채널을_센다()
        {
            OpenFinale();
            NightRun.Send(JudgeSignal.Channel("cctv.ch1"));
            NightRun.Send(JudgeSignal.CctvView("cctv.ch2", 0.1f));
            NightRun.Send(JudgeSignal.Channel("cctv.ch1"));
            Assert.AreEqual(2, NightRun.Finale.ChannelsSeen);
        }

        [Test]
        public void 오일차_태블릿에는_G3이_있고_빈칸을_당신으로_채운다()
        {
            TestKit.BeginProgramNight(FinaleWatch.Day, () => _clock);
            RuleSO g3 = null;
            foreach (RuleSO card in NightRun.TodayDeck) if (card != null && card.CardId == ProgramCatalog.FinaleBlankRule) g3 = card;
            Assert.IsNotNull(g3, "5일차 표시 카드에 G3");
            StringAssert.Contains("[　　]", g3.PlayerText);

            Assert.IsTrue(NightRun.FillFinaleBlank());
            Assert.AreEqual(ProgramCatalog.FinaleBlankFilled, g3.PlayerText);
            StringAssert.Contains("당신", g3.PlayerText);
        }

        [Test]
        public void 사일차_태블릿에는_G3이_없다()
        {
            TestKit.BeginProgramNight(4, () => _clock);
            foreach (RuleSO card in NightRun.TodayDeck) Assert.AreNotEqual(ProgramCatalog.FinaleBlankRule, card.CardId);
            Assert.IsFalse(NightRun.FillFinaleBlank());
        }

        [Test]
        public void 결말은_K4를_지킴으로_적고_근무를_정산한다()
        {
            OpenFinale();
            int ended = 0;
            DaySummary got = default;
            EventBus.DayEnded += s =>
            {
                ended++;
                got = s;
            };

            NightRun.Send(JudgeSignal.Beam(FinaleWatch.WindowTarget, 0.1f));
            Assert.IsTrue(NightRun.EndFinale());

            Assert.AreEqual(1, ended);
            Assert.IsFalse(NightRun.IsNightActive);
            Assert.AreEqual(FinaleEnding.ShiftChange, NightRun.LastFinaleEnding);
            bool k4 = false;
            foreach (DutyLogEntry e in got.DutyLog)
            {
                if (e.RuleId != ProgramCatalog.FinaleRule) continue;
                k4 = true;
                Assert.AreEqual(RuleVerdict.Complied, e.Verdict);
            }

            Assert.IsTrue(k4, "근무일지에 K4 줄");
            Assert.IsFalse(NightRun.EndFinale(), "두 번 닫지 않는다");
        }

        [Test]
        public void 창을_보지_않으면_근무_종료_결말()
        {
            OpenFinale();
            Assert.IsTrue(NightRun.EndFinale());
            Assert.AreEqual(FinaleEnding.ShiftOver, NightRun.LastFinaleEnding);
        }
    }
}

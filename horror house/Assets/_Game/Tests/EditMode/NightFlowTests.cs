using System.Collections.Generic;
using NUnit.Framework;

namespace NightDuty.Tests
{
    /// <summary>신뢰 전용 구간(2026-09-30 최종 기획서 15/30/45/65).</summary>
    public sealed class TrustBandTests
    {
        [TestCase(0, Band.Band0)]
        [TestCase(14, Band.Band0)]
        [TestCase(15, Band.Band1)]
        [TestCase(29, Band.Band1)]
        [TestCase(30, Band.Band2)]
        [TestCase(44, Band.Band2)]
        [TestCase(45, Band.Band3)]
        [TestCase(64, Band.Band3)]
        [TestCase(65, Band.Band4)]
        [TestCase(100, Band.Band4)]
        public void 신뢰는_전용경계로_구간을_읽는다(int trust, Band expected)
        {
            Assert.AreEqual(expected, Bands.OfTrust(trust));
        }

        [Test]
        public void 축시스템과_연출구간_모두_신뢰에_전용경계를_쓴다()
        {
            FearAxisSystem axes = new FearAxisSystem();
            BandResolver bands = new BandResolver(axes);
            axes.ValueChanged += bands.OnValueChanged;
            axes.Apply(FearAxis.Trust, 30, "준수", SpaceId.None);

            Assert.AreEqual(Band.Band2, axes.GetBand(FearAxis.Trust));
            Assert.AreEqual(Band.Band2, bands.Shown.GetBand(FearAxis.Trust));
            Assert.AreEqual(Band.Band2, bands.Target(FearAxis.Trust));

            // 감각 축은 그대로 25/50/75/90이다.
            axes.Apply(FearAxis.Auditory, 30, "위반", SpaceId.None);
            Assert.AreEqual(Band.Band1, axes.GetBand(FearAxis.Auditory));
        }
    }

    /// <summary>밤 시계 표(실시간 15분 = 00:00~04:00).</summary>
    public sealed class NightClockTests
    {
        [Test]
        public void 실시간15분이_게임240분이다()
        {
            Assert.AreEqual(16f, NightClock.GameSecondsPerRealSecond, 0.0001f);
            Assert.AreEqual(900f, NightClock.RealSecondsAt(NightClock.ShiftEnd), 0.001f);
            Assert.AreEqual(60f, NightClock.RealSecondsAt(NightClock.JudgingStart), 0.001f, "출근은 실시간 1분");
            Assert.AreEqual(225f, NightClock.RealSecondsAt(NightClock.Call1), 0.001f, "호출 1은 실시간 3:45");
            Assert.AreEqual(510f, NightClock.RealSecondsAt(NightClock.Call2), 0.001f, "호출 2는 실시간 8:30");
        }

        [TestCase(0f, NightPhase.Arrival)]
        [TestCase(15.9f, NightPhase.Arrival)]
        [TestCase(16f, NightPhase.LowTension)]
        [TestCase(60f, NightPhase.SlotA)]
        [TestCase(111.9f, NightPhase.SlotA)]
        [TestCase(112f, NightPhase.Relax)]
        [TestCase(136f, NightPhase.SlotB)]
        [TestCase(188f, NightPhase.SlotC)]
        [TestCase(210f, NightPhase.Wrap)]
        [TestCase(240f, NightPhase.Ended)]
        public void 시각별_구간(float minute, NightPhase expected)
        {
            Assert.AreEqual(expected, NightClock.PhaseAt(minute));
        }

        [TestCase(0f, false)]
        [TestCase(16f, true)]
        [TestCase(100f, true)]
        [TestCase(120f, false)]
        [TestCase(136f, true)]
        [TestCase(209f, true)]
        [TestCase(210f, false)]
        public void 판정은_출근과_이완과_03시30분_이후에_멈춘다(float minute, bool judging)
        {
            Assert.AreEqual(judging, NightClock.IsJudging(minute));
        }

        [Test]
        public void 중간서명은_이완구간에만_된다()
        {
            Assert.IsFalse(NightClock.CanSignCheckpoint(100f));
            Assert.IsTrue(NightClock.CanSignCheckpoint(120f));
            Assert.IsFalse(NightClock.CanSignCheckpoint(136f));
        }

        [Test]
        public void 조우는_슬롯에서만_새로_건다()
        {
            Assert.IsFalse(NightClock.CanStartEncounter(40f), "저긴장");
            Assert.IsTrue(NightClock.CanStartEncounter(70f), "슬롯 A");
            Assert.IsFalse(NightClock.CanStartEncounter(120f), "이완");
            Assert.IsTrue(NightClock.CanStartEncounter(200f), "슬롯 C");
            Assert.IsFalse(NightClock.CanStartEncounter(215f), "03:30 이후");
        }

        [Test]
        public void 추적기는_건너뛴_경계를_순서대로_알린다()
        {
            NightClockTracker tracker = new NightClockTracker();
            List<string> log = new List<string>();
            tracker.Call1 += () => log.Add("호출1");
            tracker.Call2 += () => log.Add("호출2");
            tracker.ResidualAlert += () => log.Add("잔여");
            tracker.ShiftEnded += () => log.Add("종료");
            tracker.PhaseChanged += (a, b) => log.Add(a + ">" + b);

            tracker.Reset(0f);
            tracker.Advance(150f);

            CollectionAssert.AreEqual(
                new[] { "Arrival>LowTension", "LowTension>SlotA", "호출1", "SlotA>Relax", "Relax>SlotB", "호출2" },
                log);

            log.Clear();
            tracker.Advance(140f);   // 뒤로 가면 무시한다
            Assert.AreEqual(0, log.Count);

            tracker.Advance(240f);
            CollectionAssert.AreEqual(new[] { "SlotB>SlotC", "SlotC>Wrap", "잔여", "Wrap>Ended", "종료" }, log);
        }

        [Test]
        public void 되돌리기는_지난_경계를_다시_알리지_않는다()
        {
            NightClockTracker tracker = new NightClockTracker();
            int call2 = 0;
            tracker.Call2 += () => call2++;

            tracker.Reset(0f);
            tracker.Advance(170f);
            tracker.Reset(NightClock.Call2);   // 체크포인트 재시작
            tracker.Advance(160f);

            Assert.AreEqual(1, call2);
            Assert.AreEqual(NightPhase.SlotB, tracker.Phase);
        }
    }

    /// <summary>경고 장부와 재시작 규칙(순수 계산).</summary>
    public sealed class WarningAndRestartPolicyTests
    {
        [Test]
        public void 세번째_경고에_처벌이_대기하고_도장은_비워진다()
        {
            WarningLedger ledger = new WarningLedger();
            int changes = 0;
            ledger.Changed += (c, p) => changes++;

            Assert.AreEqual(0, ledger.Add());
            Assert.AreEqual(0, ledger.Add());
            Assert.AreEqual(1, ledger.Add());

            Assert.AreEqual(0, ledger.Count);
            Assert.AreEqual(1, ledger.PendingPunishments);
            Assert.AreEqual(3, changes);

            Assert.IsTrue(ledger.TryTakePunishment());
            Assert.IsFalse(ledger.TryTakePunishment());
            Assert.AreEqual(1, ledger.TotalTaken);
        }

        [Test]
        public void 한번에_여러_경고도_처벌로_바뀐다()
        {
            WarningLedger ledger = new WarningLedger();
            Assert.AreEqual(2, ledger.Add(7));
            Assert.AreEqual(1, ledger.Count);
            Assert.AreEqual(2, ledger.PendingPunishments);
        }

        [TestCase(80, 1, 70)]
        [TestCase(80, 3, 50)]
        [TestCase(80, 5, 40)]
        [TestCase(45, 1, 40)]
        [TestCase(30, 2, 30)]
        [TestCase(0, 4, 0)]
        public void 재시작_보정은_10k이고_하한은_min_스냅샷_40(int snapshot, int k, int expected)
        {
            Assert.AreEqual(expected, RestartPolicy.RestoredValue(snapshot, k));
        }

        [Test]
        public void 장치_문턱()
        {
            Assert.IsTrue(RestartPolicy.SlotCAllowed(1));
            Assert.IsFalse(RestartPolicy.SlotCAllowed(2));
            Assert.IsFalse(RestartPolicy.SwapCapturedRule(2));
            Assert.IsTrue(RestartPolicy.SwapCapturedRule(3));
            Assert.IsFalse(RestartPolicy.CanLeaveVoluntarily(3));
            Assert.IsTrue(RestartPolicy.CanLeaveVoluntarily(4));
            Assert.IsFalse(RestartPolicy.IsAbsence(4));
            Assert.IsTrue(RestartPolicy.IsAbsence(5));
            Assert.AreEqual(60, RestartPolicy.AbsenceValue(100));
            Assert.AreEqual(35, RestartPolicy.AbsenceValue(35));
        }

        [Test]
        public void 상한이_걸린_델타로는_붙잡히지_않는다()
        {
            FearAxisSystem axes = new FearAxisSystem();
            axes.Apply(FearAxis.Layout, 90, "준비", SpaceId.None);
            axes.SoftCap = Deltas.SoftCap;

            Assert.IsTrue(axes.Apply(FearAxis.Layout, Deltas.Punishment, "처벌", SpaceId.None));
            Assert.AreEqual(95, axes.GetValue(FearAxis.Layout));
            Assert.IsFalse(axes.Apply(FearAxis.Layout, 12, "정산", SpaceId.None), "이미 95면 더 오르지 않는다");
            Assert.IsFalse(axes.IsLocked);

            axes.Apply(FearAxis.Trust, 99, "신뢰는 상한과 무관", SpaceId.None);
            Assert.AreEqual(99, axes.GetValue(FearAxis.Trust));

            axes.SoftCap = null;
            axes.Apply(FearAxis.Layout, 5, "위반", SpaceId.None);
            Assert.IsTrue(axes.IsLocked);
        }

        [Test]
        public void 도달값_복원은_연출구간을_내린다()
        {
            FearAxisSystem axes = new FearAxisSystem();
            BandResolver bands = new BandResolver(axes);
            axes.ValueChanged += bands.OnValueChanged;
            int[] snapshot = bands.ReachedCopy();

            List<Band> seen = new List<Band>();
            EventBus.BandChanged += (s, a, from, to) =>
            {
                if (s == SpaceId.Corridor && a == FearAxis.Auditory)
                {
                    seen.Add(to);
                }
            };

            try
            {
                axes.Apply(FearAxis.Auditory, 100, "붙잡힘", SpaceId.Corridor);
                Assert.AreEqual(Band.Band4, bands.GetShown(SpaceId.Corridor, FearAxis.Auditory));

                axes.Restore(0, 0, 0, 0);
                bands.RestoreReached(snapshot);

                Assert.AreEqual(Band.Band0, bands.GetShown(SpaceId.Corridor, FearAxis.Auditory));
                Assert.AreEqual(0, bands.Peak(FearAxis.Auditory));
                Assert.AreEqual(Band.Band0, seen[seen.Count - 1], "내려간 구간도 방송한다");
            }
            finally
            {
                EventBus.ClearAll();
            }
        }
    }

    /// <summary>NightRun의 판정 시간창·경고·처벌·재시작·체크포인트·결근.</summary>
    public sealed class NightRunRestartTests
    {
        private int _clock;

        [SetUp]
        public void SetUp()
        {
            NightRun.StartNewRun();
            NightRun.ProgramEnabled = true;   // 수칙 위반은 새 편성의 G1(청각)으로 만든다(TestKit)
            NightRun.JudgingWindowEnabled = false;
            _clock = 30;
        }

        [TearDown]
        public void TearDown()
        {
            NightRun.InspectionPlanOverride = null;
            NightRun.JudgingWindowEnabled = false;
            NightRun.ProgramEnabled = false;
            NightRun.StartNewRun();
            EventBus.ClearAll();
        }

        private static void Violate()
        {
            TestKit.ViolateRunning();
        }

        [Test]
        public void 판정시간창이_켜지면_출근과_이완에는_판정하지_않는다()
        {
            NightRun.JudgingWindowEnabled = true;
            _clock = 5;   // 00:05 출근
            NightRun.BeginNight(1, () => _clock);
            Assert.IsFalse(NightRun.IsJudgingNow);

            Violate();
            Assert.AreEqual(0, NightRun.Axes.GetValue(FearAxis.Auditory), "출근 중에는 판정하지 않는다");

            _clock = 120;   // 이완
            Violate();
            Assert.AreEqual(0, NightRun.Axes.GetValue(FearAxis.Auditory), "이완 중에는 판정하지 않는다");

            _clock = 40;
            Assert.IsTrue(NightRun.IsJudgingNow);
            Violate();
            Assert.AreEqual(Deltas.RuleViolation, NightRun.Axes.GetValue(FearAxis.Auditory));
        }

        [Test]
        public void 판정정지_중에도_현재공간은_갱신한다()
        {
            NightRun.JudgingWindowEnabled = true;
            _clock = 5;
            NightRun.BeginNight(1, () => _clock);
            NightRun.Send(JudgeSignal.OfSpace(SignalKind.SpaceEntered, SpaceId.Toilet));
            Assert.AreEqual(SpaceId.Toilet, NightRun.CurrentSpace);
        }

        [Test]
        public void 세번째_경고의_처벌은_다음_공간진입에서_가장높은_축에_95까지()
        {
            FearAxis punished = FearAxis.Trust;
            EventBus.Punished += a => punished = a;

            NightRun.BeginNight(1, () => _clock);
            NightRun.DebugAddAxis(FearAxis.Illuminance, 88);
            NightRun.DebugAddAxis(FearAxis.Layout, 30);

            Assert.AreEqual(1, NightRun.AddWarning(3, "미완료"));
            Assert.AreEqual(1, NightRun.Warnings.PendingPunishments);
            Assert.AreEqual(88, NightRun.Axes.GetValue(FearAxis.Illuminance), "처벌은 곧바로 나오지 않는다");

            NightRun.Send(JudgeSignal.OfSpace(SignalKind.SpaceEntered, SpaceId.Corridor));

            Assert.AreEqual(FearAxis.Illuminance, punished);
            Assert.AreEqual(Deltas.SoftCap, NightRun.Axes.GetValue(FearAxis.Illuminance), "처벌로는 붙잡히지 않는다");
            Assert.IsFalse(NightRun.IsCaptured);
            Assert.AreEqual(0, NightRun.Warnings.PendingPunishments);
        }

        [Test]
        public void 처벌_동점은_청각이_먼저다()
        {
            NightRun.BeginNight(1, () => _clock);
            NightRun.DebugAddAxis(FearAxis.Layout, 20);
            NightRun.DebugAddAxis(FearAxis.Auditory, 20);
            Assert.AreEqual(FearAxis.Auditory, NightRun.HighestSensory());
        }

        [Test]
        public void 정산_델타로는_붙잡히지_않는다()
        {
            int dayEnded = 0;
            EventBus.DayEnded += s => dayEnded++;
            NightRun.InspectionPlanOverride = (day, shown) => new InspectionPlan(day,
                new[] { new InspectionAssignment(InspectionCatalog.Find("H-4"), true, Band.Band1, false) },
                SpaceId.None, string.Empty);

            NightRun.BeginNight(1, () => _clock);
            NightRun.DebugAddAxis(FearAxis.Layout, 90);

            Assert.IsTrue(NightRun.RequestEndNight(), "미완료 이상 +8이 붙어도 95에서 멈춘다");
            Assert.AreEqual(Deltas.SoftCap, NightRun.Axes.GetValue(FearAxis.Layout));
            Assert.AreEqual(1, NightRun.Warnings.Count, "미완료 점검은 경고 1");
            Assert.AreEqual(1, dayEnded);
        }

        [Test]
        public void 붙잡히면_밤시작_스냅샷에서_10k_보정으로_다시_한다()
        {
            RestartResult notified = default;
            EventBus.NightRestarted += r => notified = r;

            NightRun.DebugAddAxis(FearAxis.Auditory, 80);
            NightRun.DebugAddAxis(FearAxis.Illuminance, 30);
            NightRun.DebugAddAxis(FearAxis.Trust, 20);
            NightRun.AddWarning(1, "전날 미완료");   // 밤 시작 전에 찍힌 도장은 스냅샷에 들어간다
            NightRun.BeginNight(2, () => _clock);
            RuleSO firstLine = NightRun.TodayDeck[0];

            NightRun.DebugAddAxis(FearAxis.Trust, 10);   // 이번 시도에서 번 신뢰는 사라진다
            NightRun.AddWarning(1, "미완료");            // 이번 시도에서 받은 경고도 사라진다
            NightRun.DebugForceCapture(FearAxis.Auditory);
            Assert.IsTrue(NightRun.IsCaptured);
            Assert.IsFalse(NightRun.IsNightActive);

            RestartResult result = NightRun.RestartAfterCapture();

            Assert.AreEqual(RestartKind.FromNightStart, result.Kind);
            Assert.AreEqual(1, result.K);
            Assert.AreEqual(0, result.StartMinute);
            Assert.AreEqual(FearAxis.Auditory, result.CapturedAxis);
            Assert.AreEqual(result.Kind, notified.Kind);

            Assert.IsFalse(NightRun.IsCaptured);
            Assert.IsTrue(NightRun.IsNightActive);
            Assert.AreEqual(2, NightRun.Day);
            Assert.AreEqual(1, NightRun.RestartsTonight);
            Assert.AreEqual(70, NightRun.Axes.GetValue(FearAxis.Auditory), "80 − 10×1");
            Assert.AreEqual(30, NightRun.Axes.GetValue(FearAxis.Illuminance), "40 이하에서 시작한 축은 깎지 않는다");
            Assert.AreEqual(20, NightRun.Axes.GetValue(FearAxis.Trust), "신뢰는 스냅샷 그대로");
            Assert.AreEqual(1, NightRun.Warnings.Count, "경고도 스냅샷 그대로");
            Assert.AreEqual(Band.Band3, NightRun.Shown.GetBand(FearAxis.Auditory), "붙잡힌 100이 연출을 구간 4로 묶지 않는다");
            Assert.AreSame(firstLine, NightRun.TodayDeck[0], "덱은 다시 뽑지 않는다");

            NightRun.DebugForceCapture(FearAxis.Auditory);
            result = NightRun.RestartAfterCapture();
            Assert.AreEqual(2, result.K);
            Assert.AreEqual(60, NightRun.Axes.GetValue(FearAxis.Auditory), "80 − 10×2");
        }

        [Test]
        public void 여섯번째_시도도_붙잡히면_결근이다()
        {
            List<DaySummary> ended = new List<DaySummary>();
            EventBus.DayEnded += s => ended.Add(s);

            NightRun.DebugAddAxis(FearAxis.Layout, 30);
            NightRun.BeginNight(3, () => _clock);

            for (int k = 1; k <= RestartPolicy.AbsenceAfterRestarts; k++)
            {
                NightRun.DebugForceCapture(FearAxis.Auditory);
                RestartResult r = NightRun.RestartAfterCapture();
                Assert.AreEqual(k, r.K);
                Assert.AreNotEqual(RestartKind.Absent, r.Kind);
            }

            NightRun.DebugForceCapture(FearAxis.Auditory);
            RestartResult absent = NightRun.RestartAfterCapture();

            Assert.AreEqual(RestartKind.Absent, absent.Kind);
            Assert.IsFalse(NightRun.IsNightActive);
            Assert.IsFalse(NightRun.IsCaptured);
            Assert.AreEqual(1, ended.Count);
            Assert.AreEqual(NightOutcome.Absent, ended[0].Outcome);
            Assert.AreEqual(RestartPolicy.AbsenceNextNightCap, NightRun.Axes.GetValue(FearAxis.Auditory), "min(현재, 60)");
            Assert.AreEqual(30, NightRun.Axes.GetValue(FearAxis.Layout));

            NightRun.BeginNight(4, () => _clock);
            Assert.AreEqual(0, NightRun.RestartsTonight, "새 밤은 k를 비운다");
        }

        [Test]
        public void 체크포인트는_이완구간에_한번_찍히고_재시작은_거기서_한다()
        {
            NightRun.JudgingWindowEnabled = true;
            _clock = 40;
            NightRun.BeginNight(1, () => _clock);
            Assert.IsFalse(NightRun.SignCheckpoint(), "이완 구간이 아니면 서명할 수 없다");

            Violate();   // 청각 12
            _clock = 120;
            Assert.IsTrue(NightRun.SignCheckpoint());
            Assert.IsFalse(NightRun.SignCheckpoint(), "밤당 한 번");
            Assert.AreEqual(Deltas.RuleViolation, NightRun.Checkpoint.Value(FearAxis.Auditory));

            _clock = 150;
            NightRun.DebugForceCapture(FearAxis.Auditory);
            RestartResult r = NightRun.RestartAfterCapture();

            Assert.AreEqual(RestartKind.FromCheckpoint, r.Kind);
            Assert.AreEqual(NightClock.Call2, r.StartMinute);
            Assert.AreEqual(Deltas.RuleViolation, NightRun.Axes.GetValue(FearAxis.Auditory), "40 이하라 보정하지 않는다");
        }

        [Test]
        public void 조퇴는_k4부터_결근과_같다()
        {
            NightRun.BeginNight(1, () => _clock);
            Assert.AreEqual(RestartKind.None, NightRun.LeaveVoluntarily().Kind, "처음부터 조퇴할 수는 없다");

            for (int k = 0; k < RestartPolicy.VoluntaryLeaveFrom; k++)
            {
                NightRun.DebugForceCapture(FearAxis.Layout);
                NightRun.RestartAfterCapture();
            }

            Assert.IsTrue(NightRun.IsNightActive);
            RestartResult leave = NightRun.LeaveVoluntarily();
            Assert.AreEqual(RestartKind.Absent, leave.Kind);
            Assert.IsFalse(NightRun.IsNightActive);
            Assert.AreEqual(NightOutcome.Absent, NightRun.LastSummary.Outcome);
        }

        [Test]
        public void 붙잡히지_않았으면_재시작하지_않는다()
        {
            NightRun.BeginNight(1, () => _clock);
            Assert.AreEqual(RestartKind.None, NightRun.RestartAfterCapture().Kind);
            Assert.AreEqual(0, NightRun.RestartsTonight);
        }
    }
}

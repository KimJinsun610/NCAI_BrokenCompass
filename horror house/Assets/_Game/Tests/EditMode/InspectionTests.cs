using System.Collections.Generic;
using NUnit.Framework;

namespace NightDuty.Tests
{
    /// <summary>고정 구간을 돌려주는 읽기 창구(연출 구간 대역).</summary>
    internal sealed class FixedBands : IFearAxisReader
    {
        private readonly Band[] _bands = new Band[4];

        public FixedBands(Band auditory, Band illuminance, Band layout, Band trust = Band.Band0)
        {
            _bands[0] = auditory;
            _bands[1] = illuminance;
            _bands[2] = layout;
            _bands[3] = trust;
        }

        public static FixedBands All(Band band)
        {
            return new FixedBands(band, band, band);
        }

        public int GetValue(FearAxis axis)
        {
            return Bands.LowerBound(_bands[(int)axis]);
        }

        public Band GetBand(FearAxis axis)
        {
            return _bands[(int)axis];
        }
    }

    /// <summary>점검 항목 카탈로그와 일차별 수(최종 기획서 「공간별 설계」·「이상 배정」).</summary>
    public sealed class InspectionCatalogTests
    {
        [Test]
        public void 점검_항목은_17개다_복도4_교실3_과학실3_화장실3_도서관3_경비실1()
        {
            Assert.AreEqual(17, InspectionCatalog.All.Count);
            Assert.AreEqual(4, InspectionCatalog.InSpace(SpaceId.Corridor).Count);
            Assert.AreEqual(3, InspectionCatalog.InSpace(SpaceId.Classroom).Count);
            Assert.AreEqual(3, InspectionCatalog.InSpace(SpaceId.ScienceRoom).Count);
            Assert.AreEqual(3, InspectionCatalog.InSpace(SpaceId.Toilet).Count);
            Assert.AreEqual(3, InspectionCatalog.InSpace(SpaceId.Library).Count);
            Assert.AreEqual(1, InspectionCatalog.InSpace(SpaceId.SecurityRoom).Count);
            Assert.AreEqual(3, InspectionCatalog.InSpace(SpaceId.Classroom_1_3).Count, "옛 교실 값도 새 교실로 읽는다");
        }

        [Test]
        public void 항목에는_신뢰축이_없고_문구는_정상을_단정한다()
        {
            HashSet<string> ids = new HashSet<string>();
            foreach (InspectionItem item in InspectionCatalog.All)
            {
                Assert.IsTrue(ids.Add(item.Id), "ID 중복: " + item.Id);
                Assert.AreNotEqual(FearAxis.Trust, item.Axis, item.Id);
                StringAssert.EndsWith("니다.", item.TabletLine, item.Id + " 문구는 공문 말투의 단정문");
                Assert.AreSame(item, InspectionCatalog.FindByTarget(item.TargetId));
                Assert.AreSame(item, InspectionCatalog.FindByTarget(item.Id));
            }

            Assert.IsNull(InspectionCatalog.Find("X-9"));
        }

        [TestCase(1, 5, 2)]
        [TestCase(2, 5, 2)]
        [TestCase(3, 6, 3)]
        [TestCase(4, 6, 3)]
        [TestCase(5, 7, 3)]
        public void 일차별_점검수와_이상수(int day, int items, int anomalies)
        {
            Assert.AreEqual(items, InspectionQuota.Items(day));
            Assert.AreEqual(anomalies, InspectionQuota.Anomalies(day));
        }

        [Test]
        public void 좌우_동()
        {
            Assert.AreEqual(Wing.Left, SpaceIds.WingOf(SpaceId.Toilet));
            Assert.AreEqual(Wing.Left, SpaceIds.WingOf(SpaceId.Library));
            Assert.AreEqual(Wing.Right, SpaceIds.WingOf(SpaceId.Classroom));
            Assert.AreEqual(Wing.Right, SpaceIds.WingOf(SpaceId.ScienceRoom));
            Assert.AreEqual(Wing.Right, SpaceIds.WingOf(SpaceId.SecurityRoom));
            Assert.AreEqual(Wing.Center, SpaceIds.WingOf(SpaceId.Corridor));
        }
    }

    /// <summary>이상 배정기. 시드 여럿으로 규칙이 늘 지켜지는지 본다.</summary>
    public sealed class AnomalyAssignerTests
    {
        [Test]
        public void 일차1은_튜토리얼_고정이다()
        {
            for (int seed = 0; seed < 50; seed++)
            {
                AnomalyAssigner a = new AnomalyAssigner(new System.Random(seed));
                InspectionPlan plan = a.Build(1, FixedBands.All(Band.Band0));

                Assert.AreEqual(5, plan.Count, plan.ToString());
                Assert.AreEqual(2, plan.AnomalyCount, plan.ToString());
                Assert.IsFalse(plan.Find("K-1").IsAnomaly, "첫 점검 K-1은 정상");
                Assert.IsTrue(plan.Find("H-4").IsAnomaly, "사물함(옮김)");
                Assert.IsTrue(plan.Find("S-2").IsAnomaly, "현미경 불(빛)");
                Assert.AreEqual(Band.Band1, plan.Find("S-2").Intensity, "구간 0이어도 강도는 1부터");
                Assert.AreEqual(SpaceId.Classroom, plan.LateSpace);
                Assert.IsTrue(plan.Spaces.Contains(SpaceId.Classroom));
                foreach (SpaceId s in plan.Spaces)
                {
                    Assert.IsTrue(s == SpaceId.Corridor || s == SpaceId.Classroom || s == SpaceId.ScienceRoom || s == SpaceId.SecurityRoom, plan.ToString());
                }

                Assert.AreNotEqual("K-1", plan.Call1ItemId);
                Assert.IsFalse(plan.Find(plan.Call1ItemId).IsLate, "호출 1은 먼저 열린 항목");
            }
        }

        [Test]
        public void 이일차부터_규칙을_지킨다()
        {
            for (int seed = 0; seed < 200; seed++)
            {
                AnomalyAssigner a = new AnomalyAssigner(new System.Random(seed));
                a.Build(1, FixedBands.All(Band.Band0));
                for (int day = 2; day <= 5; day++)
                {
                    HashSet<SpaceId> seenBefore = new HashSet<SpaceId>(a.SeenSpaces);
                    InspectionPlan plan = a.Build(day, FixedBands.All(DayFloor.Of(day)));
                    string why = "seed " + seed + ": " + a.LastReport;

                    Assert.AreEqual(InspectionQuota.Items(day), plan.Count, why);
                    Assert.LessOrEqual(plan.Spaces.Count, InspectionQuota.MaxSpaces, why);

                    bool left = false, right = false;
                    foreach (SpaceId s in plan.Spaces)
                    {
                        if (SpaceIds.WingOf(s) == Wing.Left) left = true;
                        if (SpaceIds.WingOf(s) == Wing.Right) right = true;
                    }

                    Assert.IsTrue(left && right, "좌·우 동에 각각 하나 이상 — " + why);
                    Assert.AreNotEqual(SpaceId.SecurityRoom, plan.LateSpace, why);
                    Assert.IsTrue(plan.Spaces.Contains(plan.LateSpace), why);
                    Assert.IsFalse(plan.Find(plan.Call1ItemId).IsLate, why);

                    int anomalies = 0;
                    foreach (InspectionAssignment row in plan.Assignments)
                    {
                        Assert.AreEqual(row.Item.Space == plan.LateSpace, row.IsLate, why);
                        if (!row.IsAnomaly) continue;
                        anomalies++;
                        Assert.IsTrue(seenBefore.Contains(row.Item.Space), "처음 점검하는 공간에 이상 — " + why);
                    }

                    Assert.LessOrEqual(anomalies, InspectionQuota.Anomalies(day), why);
                    if (seenBefore.Count >= 4)
                    {
                        Assert.AreEqual(InspectionQuota.Anomalies(day), anomalies, "후보가 넉넉하면 이상 수를 채운다 — " + why);
                    }
                }
            }
        }

        [Test]
        public void 구간0인_축에는_이상을_두지_않는다()
        {
            for (int seed = 0; seed < 100; seed++)
            {
                AnomalyAssigner a = new AnomalyAssigner(new System.Random(seed));
                a.Build(1, FixedBands.All(Band.Band0));
                InspectionPlan plan = a.Build(3, new FixedBands(Band.Band0, Band.Band2, Band.Band2));

                foreach (InspectionAssignment row in plan.Assignments)
                {
                    if (row.IsAnomaly) Assert.AreNotEqual(FearAxis.Auditory, row.Item.Axis, a.LastReport);
                }
            }
        }

        [Test]
        public void 구간이_높은_축이_더_자주_뽑힌다()
        {
            int layout = 0, auditory = 0;
            for (int seed = 0; seed < 400; seed++)
            {
                AnomalyAssigner a = new AnomalyAssigner(new System.Random(seed));
                a.MarkSeen(SpaceIds.Final);
                InspectionPlan plan = a.Build(3, new FixedBands(Band.Band1, Band.Band1, Band.Band4));
                foreach (InspectionAssignment row in plan.Assignments)
                {
                    if (!row.IsAnomaly) continue;
                    if (row.Item.Axis == FearAxis.Layout) layout++;
                    if (row.Item.Axis == FearAxis.Auditory) auditory++;
                    Assert.AreEqual(row.Item.Axis == FearAxis.Layout ? Band.Band4 : Band.Band1, row.Intensity);
                }
            }

            Assert.Greater(layout, auditory * 2, "배치 구간 4(가중치 4) 대 청각 구간 1(가중치 1)");
        }

    }

    /// <summary>점검판 — 보고 경제·가까이·04:00 정산·스냅샷.</summary>
    public sealed class InspectionBoardTests
    {
        private static InspectionAssignment Row(string id, bool anomaly, bool late = false)
        {
            return new InspectionAssignment(InspectionCatalog.Find(id), anomaly, Band.Band2, late);
        }

        private static InspectionBoard Board(params InspectionAssignment[] rows)
        {
            InspectionBoard board = new InspectionBoard();
            board.Begin(new InspectionPlan(3, rows, SpaceId.Toilet, string.Empty));
            return board;
        }

        [Test]
        public void 정확한_이상보고는_5씩_축마다_밤당_10까지_뺀다()
        {
            InspectionBoard board = Board(Row("H-2", true), Row("H-4", true), Row("C-1", true));
            FearAxisSystem axes = new FearAxisSystem();
            axes.Apply(FearAxis.Layout, 30, "준비", SpaceId.None);

            Assert.AreEqual(ReportOutcome.CorrectAnomaly, board.Report("H-2", true, -1, axes, SpaceId.Corridor).Outcome);
            Assert.AreEqual(25, axes.GetValue(FearAxis.Layout));
            Assert.AreEqual(-5, board.Report("H-4", true, -1, axes, SpaceId.Corridor).Change);
            InspectionReport third = board.Report("C-1", true, -1, axes, SpaceId.Classroom);

            Assert.AreEqual(ReportOutcome.CorrectAnomaly, third.Outcome);
            Assert.AreEqual(0, third.Change, "한도 −10을 다 썼다");
            Assert.AreEqual(20, axes.GetValue(FearAxis.Layout));
            Assert.AreEqual(Deltas.ReliefCapPerAxisPerNight, board.ReliefUsed(FearAxis.Layout));
        }

        [Test]
        public void 놓침_8_오보_7_정상은_0()
        {
            InspectionBoard board = Board(Row("H-1", true), Row("H-3", false), Row("S-2", false));
            FearAxisSystem axes = new FearAxisSystem();

            Assert.AreEqual(ReportOutcome.Missed, board.Report("H-1", false, -1, axes, SpaceId.Corridor).Outcome);
            Assert.AreEqual(Deltas.MissedAnomaly, axes.GetValue(FearAxis.Illuminance));

            Assert.AreEqual(ReportOutcome.FalseReport, board.Report("H-3", true, -1, axes, SpaceId.Corridor).Outcome);
            Assert.AreEqual(Deltas.FalseReport, axes.GetValue(FearAxis.Auditory), "오보는 그 항목의 축");

            Assert.AreEqual(ReportOutcome.CorrectNormal, board.Report("S-2", false, -1, axes, SpaceId.ScienceRoom).Outcome);
            Assert.AreEqual(Deltas.MissedAnomaly, axes.GetValue(FearAxis.Illuminance), "정상을 정상 — 변화 없음");
        }

        [Test]
        public void T4가_걸린_T1은_T4로만_판정한다()
        {
            InspectionBoard board = Board(Row("T-1", true), Row("T-2", false));
            FearAxisSystem axes = new FearAxisSystem();
            board.SetReverseReport("T-1", true);

            InspectionReport kept = board.Report("T-1", false, -1, axes, SpaceId.Toilet);
            Assert.AreEqual(ReportOutcome.ReverseKept, kept.Outcome);
            Assert.AreEqual(Deltas.TrustReverseReport, axes.GetValue(FearAxis.Trust));
            Assert.AreEqual(0, axes.GetValue(FearAxis.Layout), "이상이었어도 놓침이 없다");

            InspectionBoard other = Board(Row("T-1", false));
            FearAxisSystem axes2 = new FearAxisSystem();
            other.SetReverseReport("T-1", true);
            InspectionReport broken = other.Report("T-1", true, -1, axes2, SpaceId.Toilet);
            Assert.AreEqual(ReportOutcome.ReverseViolated, broken.Outcome);
            Assert.AreEqual(Deltas.RuleViolation, axes2.GetValue(FearAxis.Layout), "T4 위반 배치 +12, 오보를 따로 더하지 않음");
        }

        [Test]
        public void 보고는_항목당_한번이고_편성밖과_열리기전은_받지_않는다()
        {
            InspectionBoard board = Board(Row("H-2", false), Row("T-3", false, true));
            FearAxisSystem axes = new FearAxisSystem();

            Assert.IsTrue(board.Report("H-2", false, 30, axes, SpaceId.Corridor).Accepted);
            Assert.AreEqual(ReportRejection.AlreadyReported, board.Report("H-2", true, 30, axes, SpaceId.Corridor).Rejection);
            Assert.AreEqual(ReportRejection.NotInPlan, board.Report("L-1", true, 30, axes, SpaceId.Library).Rejection);
            Assert.AreEqual(ReportRejection.NotOpenYet, board.Report("T-3", false, NightClock.Call2 - 1, axes, SpaceId.Toilet).Rejection,
                "그날 마지막 점검 공간은 호출 2 전에 비활성");
            Assert.IsTrue(board.Report("T-3", false, NightClock.Call2, axes, SpaceId.Toilet).Accepted);
            Assert.AreEqual(2, board.ReportedCount);
            Assert.AreEqual(0, board.RemainingCount);
        }

        [Test]
        public void 가까이는_항목마다_한번_6()
        {
            InspectionBoard board = Board(Row("C-2", false));
            FearAxisSystem axes = new FearAxisSystem();

            Assert.IsTrue(board.Startle("C-2", axes, SpaceId.Classroom));
            Assert.IsFalse(board.Startle("C-2", axes, SpaceId.Classroom));
            Assert.AreEqual(Deltas.InspectionRuleViolation, axes.GetValue(FearAxis.Illuminance));
        }

        [Test]
        public void 정산은_미완료마다_경고1_이상이면_8()
        {
            InspectionBoard board = Board(Row("H-1", true), Row("H-2", false), Row("H-3", false));
            FearAxisSystem axes = new FearAxisSystem();
            board.Report("H-3", false, -1, axes, SpaceId.Corridor);

            InspectionSettlement s = board.Settle(axes, true);

            Assert.AreEqual(2, s.Unfinished);
            Assert.AreEqual(1, s.UnfinishedAnomalies);
            Assert.AreEqual(Deltas.MissedAnomaly, axes.GetValue(FearAxis.Illuminance));

            InspectionSettlement absent = Board(Row("H-1", true)).Settle(axes, false);
            Assert.AreEqual(0, absent.Unfinished, "결근은 경고 없는 미완료");
        }

        [Test]
        public void 스냅샷은_한도와_가까이를_되돌리고_보고는_남긴다()
        {
            InspectionBoard board = Board(Row("H-2", true), Row("H-4", true), Row("C-1", false));
            FearAxisSystem axes = new FearAxisSystem();
            axes.Apply(FearAxis.Layout, 40, "준비", SpaceId.None);
            object saved = board.CaptureState();

            board.Report("H-2", true, -1, axes, SpaceId.Corridor);
            board.Startle("C-1", axes, SpaceId.Classroom);
            Assert.AreEqual(5, board.ReliefUsed(FearAxis.Layout));

            board.RestoreState(saved);

            Assert.AreEqual(0, board.ReliefUsed(FearAxis.Layout), "한도 사용량은 스냅샷으로");
            Assert.IsFalse(board.WasStartled("C-1"));
            Assert.AreEqual(InspectionState.ReportedAnomaly, board.StateOf("H-2"), "이미 한 보고는 「보고됨」으로 남는다");
        }
    }

    /// <summary>NightRun에 붙은 점검 — 편성·보고·정산·재시작.</summary>
    public sealed class NightRunInspectionTests
    {
        private int _clock;

        [SetUp]
        public void SetUp()
        {
            NightRun.StartNewRun();
            _clock = 30;
        }

        [TearDown]
        public void TearDown()
        {
            NightRun.InspectionPlanOverride = null;
            NightRun.InspectionsEnabled = false;
            NightRun.JudgingWindowEnabled = false;
            NightRun.StartNewRun();
            EventBus.ClearAll();
        }

        private static void UsePlan(params InspectionAssignment[] rows)
        {
            NightRun.InspectionPlanOverride = (day, shown) => new InspectionPlan(day, rows, SpaceId.None, string.Empty);
        }

        private static InspectionAssignment Row(string id, bool anomaly)
        {
            return new InspectionAssignment(InspectionCatalog.Find(id), anomaly, Band.Band1, false);
        }

        [Test]
        public void 켜면_밤시작에_편성하고_알린다()
        {
            InspectionPlan planned = null;
            EventBus.InspectionPlanned += p => planned = p;
            NightRun.InspectionsEnabled = true;

            NightRun.BeginNight(1, () => _clock);

            Assert.IsNotNull(planned);
            Assert.AreSame(planned, NightRun.Inspections.Plan);
            Assert.AreEqual(InspectionQuota.Items(1), planned.Count);
        }

        [Test]
        public void 점검을_다_보고해야_전화로_근무를_끝낼_수_있다()
        {
            UsePlan(Row("H-2", false), Row("C-1", true));
            NightRun.BeginNight(2, () => _clock);
            Assert.IsFalse(NightRun.CanEndShiftEarly, "보고 0건");

            NightRun.ReportInspection("H-2", false);
            Assert.IsFalse(NightRun.CanEndShiftEarly, "1건 남음");

            NightRun.ReportInspection("C-1", true);
            Assert.IsTrue(NightRun.CanEndShiftEarly);

            Assert.IsTrue(NightRun.RequestEndNight());
            Assert.AreEqual(0, NightRun.Warnings.Count, "미완료가 없으니 경고도 없다");
            Assert.IsFalse(NightRun.CanEndShiftEarly, "끝난 밤은 다시 끝내지 않는다");
        }

        [Test]
        public void 점검표가_없거나_붙잡히면_전화로_끝낼_수_없다()
        {
            NightRun.BeginNight(1, () => _clock);
            Assert.AreEqual(0, NightRun.Inspections.Total);
            Assert.IsFalse(NightRun.CanEndShiftEarly, "빈 점검표는 「다 했다」가 아니다");

            UsePlan(Row("H-2", false));
            NightRun.StartNewRun();
            NightRun.BeginNight(2, () => _clock);
            NightRun.ReportInspection("H-2", false);
            NightRun.DebugForceCapture(FearAxis.Auditory);
            Assert.IsFalse(NightRun.CanEndShiftEarly);
        }

        [Test]
        public void 끄면_점검표가_비어있다()
        {
            NightRun.BeginNight(1, () => _clock);
            Assert.AreEqual(0, NightRun.Inspections.Total);
        }

        [Test]
        public void 판정정지_중_보고의_델타는_95에서_멈춘다()
        {
            UsePlan(Row("H-2", true));
            NightRun.JudgingWindowEnabled = true;
            _clock = 220;   // 03:40 — 판정은 끝났지만 보고는 04:00까지
            NightRun.BeginNight(2, () => _clock);
            NightRun.DebugAddAxis(FearAxis.Layout, 93);

            InspectionReport r = NightRun.ReportInspection("inspect.H-2", false);

            Assert.AreEqual(ReportOutcome.Missed, r.Outcome);
            Assert.AreEqual(Deltas.SoftCap, NightRun.Axes.GetValue(FearAxis.Layout));
            Assert.IsFalse(NightRun.IsCaptured);
        }

        [Test]
        public void 판정구간의_놓침으로는_붙잡힐_수_있다()
        {
            UsePlan(Row("H-2", true));
            NightRun.BeginNight(2, () => _clock);
            NightRun.DebugAddAxis(FearAxis.Layout, 93);

            NightRun.ReportInspection("H-2", false);

            Assert.IsTrue(NightRun.IsCaptured);
            Assert.AreEqual(FearAxis.Layout, NightRun.Cause.Axis);
        }

        [Test]
        public void 가까이는_판정시간창_밖에서는_판정하지_않는다()
        {
            List<string> startled = new List<string>();
            EventBus.InspectionStartled += (id, axis) => startled.Add(id);
            UsePlan(Row("C-2", false));
            NightRun.JudgingWindowEnabled = true;
            _clock = 120;   // 이완
            NightRun.BeginNight(2, () => _clock);

            Assert.IsFalse(NightRun.InspectionStartle("C-2"));
            _clock = 140;
            Assert.IsTrue(NightRun.InspectionStartle("C-2"));
            CollectionAssert.AreEqual(new[] { "C-2" }, startled);
            Assert.AreEqual(Deltas.InspectionRuleViolation, NightRun.Axes.GetValue(FearAxis.Illuminance));
        }

        [Test]
        public void 정산의_미완료는_경고장부로_가고_셋이면_처벌이_대기한다()
        {
            UsePlan(Row("H-1", false), Row("H-2", false), Row("H-3", false));
            NightRun.BeginNight(2, () => _clock);

            Assert.IsTrue(NightRun.RequestEndNight());

            Assert.AreEqual(0, NightRun.Warnings.Count);
            Assert.AreEqual(1, NightRun.Warnings.PendingPunishments, "04:00에 3회면 다음 밤 첫 이동에서 처벌");
        }

        [Test]
        public void 재시작해도_보고는_남고_한도는_스냅샷으로_돌아간다()
        {
            UsePlan(Row("H-2", true), Row("H-4", true), Row("C-1", true));
            NightRun.DebugAddAxis(FearAxis.Layout, 45);
            NightRun.BeginNight(2, () => _clock);

            NightRun.ReportInspection("H-2", true);
            NightRun.ReportInspection("H-4", true);
            Assert.AreEqual(35, NightRun.Axes.GetValue(FearAxis.Layout));

            NightRun.DebugForceCapture(FearAxis.Auditory);
            NightRun.RestartAfterCapture();

            Assert.AreEqual(40, NightRun.Axes.GetValue(FearAxis.Layout), "스냅샷 45 → max(min(45,40), 45−10)");
            Assert.AreEqual(InspectionState.ReportedAnomaly, NightRun.Inspections.StateOf("H-2"));
            Assert.AreEqual(ReportRejection.AlreadyReported, NightRun.ReportInspection("H-2", true).Rejection, "델타를 다시 적용하지 않는다");
            Assert.AreEqual(0, NightRun.Inspections.ReliefUsed(FearAxis.Layout), "한도 사용량은 밤 시작 스냅샷으로");

            NightRun.ReportInspection("C-1", true);
            Assert.AreEqual(35, NightRun.Axes.GetValue(FearAxis.Layout));
        }

        [Test]
        public void 편성은_재시작해도_다시_뽑지_않는다()
        {
            int planned = 0;
            EventBus.InspectionPlanned += p => planned++;
            NightRun.InspectionsEnabled = true;
            NightRun.BeginNight(1, () => _clock);
            InspectionPlan first = NightRun.Inspections.Plan;

            NightRun.DebugForceCapture(FearAxis.Layout);
            NightRun.RestartAfterCapture();

            Assert.AreSame(first, NightRun.Inspections.Plan);
            Assert.AreEqual(1, planned);
        }
    }

    /// <summary>경계 사례(최종 기획서 「경계 사례」)와 재시작 카드 재료.</summary>
    public sealed class EdgeCaseTests
    {
        private int _clock;

        [SetUp]
        public void SetUp()
        {
            NightRun.StartNewRun();
            _clock = 30;
        }

        [TearDown]
        public void TearDown()
        {
            NightRun.InspectionPlanOverride = null;
            NightRun.ProgramEnabled = false;
            NightRun.StartNewRun();
            EventBus.ClearAll();
        }

        [Test]
        public void 한프레임에_두축이_100이면_초과량이_큰_축으로_붙잡힌다()
        {
            FearAxisSystem axes = new FearAxisSystem();
            axes.Apply(FearAxis.Auditory, 95, "준비", SpaceId.None);
            axes.Apply(FearAxis.Layout, 90, "준비", SpaceId.None);

            axes.BeginFrame();
            axes.Apply(FearAxis.Auditory, 12, "A", SpaceId.Corridor);   // 초과 7
            Assert.IsFalse(axes.IsLocked, "묶음 안에서는 아직 잠그지 않는다");
            axes.Apply(FearAxis.Layout, 20, "L", SpaceId.Corridor);     // 초과 10
            Assert.IsTrue(axes.EndFrame());

            Assert.AreEqual(FearAxis.Layout, axes.Cause.Axis);
            Assert.AreEqual("L", axes.Cause.SourceId);
        }

        [Test]
        public void 초과량이_같으면_청각_조도_배치_순이다()
        {
            FearAxisSystem axes = new FearAxisSystem();
            axes.Apply(FearAxis.Layout, 90, "준비", SpaceId.None);
            axes.Apply(FearAxis.Illuminance, 90, "준비", SpaceId.None);

            axes.BeginFrame();
            axes.Apply(FearAxis.Layout, 12, "L", SpaceId.None);
            axes.Apply(FearAxis.Illuminance, 12, "I", SpaceId.None);
            axes.EndFrame();

            Assert.AreEqual(FearAxis.Illuminance, axes.Cause.Axis);
        }

        [Test]
        public void 묶음_밖에서는_예전처럼_바로_잠근다()
        {
            FearAxisSystem axes = new FearAxisSystem();
            int critical = 0;
            EventBus.AxisCritical += a => critical++;

            axes.Apply(FearAxis.Auditory, 100, "즉시", SpaceId.None);

            Assert.IsTrue(axes.IsLocked);
            Assert.AreEqual(1, critical);
        }

        [Test]
        public void 이미_나온_처벌은_재시작해도_되풀이하지_않는다()
        {
            NightRun.AddWarning(3, "전날 미완료");   // 밤 시작 스냅샷에 대기 1
            NightRun.BeginNight(2, () => _clock);
            Assert.AreEqual(1, NightRun.NightStartSnapshot.PendingPunishments);

            NightRun.Send(JudgeSignal.OfSpace(SignalKind.SpaceEntered, SpaceId.Corridor));   // 처벌 실행
            Assert.AreEqual(0, NightRun.Warnings.PendingPunishments);

            NightRun.DebugForceCapture(FearAxis.Layout);
            NightRun.RestartAfterCapture();

            Assert.AreEqual(0, NightRun.Warnings.PendingPunishments, "스냅샷의 대기 1은 이미 나왔다");
        }

        [Test]
        public void 재시작_카드는_그_축을_올린_수칙_이름을_받는다()
        {
            NightRun.InspectionPlanOverride = (day, shown) => new InspectionPlan(day,
                new[] { new InspectionAssignment(InspectionCatalog.Find("H-2"), false, Band.Band0, false) },
                SpaceId.None, string.Empty);
            TestKit.BeginProgramNight(2, () => _clock);

            TestKit.ViolateRunning();                  // G1 청각 +12
            NightRun.ReportInspection("H-2", true);   // 오보 +7(배치)
            NightRun.DebugForceCapture(FearAxis.Auditory);

            NightRun.RestartAfterCapture();

            CollectionAssert.AreEqual(new[] { "G1" }, NightRun.LastCaptureSources, "붙잡힌 축(청각)을 올린 것만 — 배치를 올린 H-2는 빠진다");
            Assert.AreEqual(0, NightRun.RaisedSources(FearAxis.Auditory).Count, "새 시도는 비어서 시작한다");
        }

        [TestCase("T4:T-1[정상]", "T4")]
        [TestCase("H-2[이상]", "H-2")]
        [TestCase("H-1(가까이)", "H-1")]
        [TestCase("C3", "C3")]
        public void 출처_이름을_줄인다(string source, string expected)
        {
            Assert.AreEqual(expected, NightRun.DisplaySource(source));
        }
    }
}

using NUnit.Framework;

namespace NightDuty.Tests
{
    /// <summary>60차 — C1 판서 폐기 · 보고한 점검 정정.</summary>
    public sealed class SixtiethPassTests
    {
        private static InspectionAssignment Row(string id, bool anomaly = false)
        {
            return new InspectionAssignment(InspectionCatalog.Find(id), anomaly, Band.Band0, false);
        }

        [Test]
        public void C1_판서는_어느_날에도_편성하지_않는다()
        {
            Assert.IsTrue(ProgramCatalog.IsRetired("C1"));
            CollectionAssert.DoesNotContain(ProgramDirector.Day1Pool, "C1");
            for (int seed = 0; seed < 30; seed++)
            {
                ProgramDirector d = new ProgramDirector(new System.Random(seed));
                for (int day = 1; day <= 5; day++)
                {
                    NightProgram p = d.Build(new ProgramRequest { Day = day, Shown = FixedBands.All(Band.Band2), Survival = FixedBands.All(Band.Band2) });
                    Assert.IsFalse(p.Has("C1"), "seed " + seed + " day " + day + " — " + p);
                }
            }
        }

        [Test]
        public void 보고한_판정을_바꾸면_지난_몫을_되돌리고_다시_판정한다()
        {
            InspectionBoard b = new InspectionBoard();
            b.Begin(new InspectionPlan(3, new[] { Row("H-2", true), Row("S-1") }, SpaceId.None, string.Empty));
            FearAxisSystem axes = new FearAxisSystem();
            FearAxis ax = InspectionCatalog.Find("H-2").Axis;
            axes.Apply(ax, 30, "준비", SpaceId.None);

            Assert.AreEqual(ReportRejection.NotReported, b.Revise("H-2", true, -1, axes, SpaceId.Corridor).Rejection, "보고 전에는 정정이 아니다");
            Assert.AreEqual(ReportOutcome.Missed, b.Report("H-2", false, -1, axes, SpaceId.Corridor).Outcome);
            Assert.AreEqual(30 + Deltas.MissedAnomaly, axes.GetValue(ax));
            Assert.AreEqual(ReportRejection.CannotRevise, b.Revise("H-2", false, -1, axes, SpaceId.Corridor).Rejection, "같은 판정");

            InspectionReport fixedUp = b.Revise("H-2", true, -1, axes, SpaceId.Corridor);
            Assert.AreEqual(ReportOutcome.CorrectAnomaly, fixedUp.Outcome);
            Assert.AreEqual(30 - Deltas.CorrectReportRelief, axes.GetValue(ax), "놓침 +8을 되돌리고 정확 보고 −5");
            Assert.AreEqual(InspectionState.ReportedAnomaly, b.StateOf("H-2"));
            Assert.AreEqual(Deltas.CorrectReportRelief, b.ReliefUsed(ax));

            InspectionReport back = b.Revise("H-2", false, -1, axes, SpaceId.Corridor);
            Assert.AreEqual(ReportOutcome.Missed, back.Outcome);
            Assert.AreEqual(30 + Deltas.MissedAnomaly, axes.GetValue(ax), "몇 번이든 — 정확 보고 몫과 그 한도도 돌려준다");
            Assert.AreEqual(0, b.ReliefUsed(ax));
            Assert.AreEqual(1, b.ReportedCount, "정정은 보고 수를 늘리지 않는다");
        }

        [Test]
        public void 사다리_방_모드면_사다리_가까이에서만_시체가_떨어진다()
        {
            DirectorFixture f = new DirectorFixture(2, 0.99, null, DirectorFixture.Slot(EncounterSlot.A, ProgramCatalog.CeilingLegs));
            f.Director.GazeTargetPosition = id => new UnityEngine.Vector3(10f, 0f, 0f);
            f.Director.GazeTargetNearRadius = NightRun.CorpseLadderRoomRadius;
            f.Minute = 30f;
            string ladder = InspectionCatalog.TargetPrefix + "C-3";
            f.Enter(SpaceId.Classroom_1_3).Pose(4f, 0f).Wait(0.1f);
            for (int i = 0; i < 10; i++) f.Send(JudgeSignal.Gaze(ladder, 0.1f)).Wait(0.1f);
            Assert.IsFalse(f.HasPhase(ProgramCatalog.CeilingLegs, DirectionPhase.Foreshadow), "교실 입구(6m)에서 봐도 아직");
            f.Pose(8f, 0.5f).Wait(0.1f);
            for (int i = 0; i < 6; i++) f.Send(JudgeSignal.Gaze(ladder, 0.1f)).Wait(0.1f);
            Assert.IsTrue(f.HasPhase(ProgramCatalog.CeilingLegs, DirectionPhase.Foreshadow), "사다리 방 안(2m)에서 보면");
        }

        [Test]
        public void T4_역보고가_걸린_변기는_바꿀_수_없다()
        {
            InspectionBoard b = new InspectionBoard();
            b.Begin(new InspectionPlan(3, new[] { Row("T-1") }, SpaceId.None, string.Empty));
            FearAxisSystem axes = new FearAxisSystem();
            b.SetReverseReport("T-1", true);
            Assert.AreEqual(ReportOutcome.ReverseKept, b.Report("T-1", false, -1, axes, SpaceId.Toilet).Outcome);
            Assert.IsFalse(b.CanRevise("T-1", -1));
            Assert.AreEqual(ReportRejection.CannotRevise, b.Revise("T-1", true, -1, axes, SpaceId.Toilet).Rejection);
        }
    }
}

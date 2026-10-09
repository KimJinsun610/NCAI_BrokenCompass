using NUnit.Framework;

namespace NightDuty.Tests
{
    /// <summary>
    /// 70차 — 민: 「복도 급습은 과학실 옆 복도 비상등에서 플레이어 방향으로 뛰어와서 놀래키는 연출」.
    /// 급습은 복도에서 마주치므로 S5(「복도 끝에 _?이 서 있으면 빛을 끄고 기다리십시오」)가 복도에서도 판정된다.
    /// 70차 ③ — 「복도 끝에 서 있는 자는 인체 모형으로 · 모형 급습 전날 · 같은 자리 · 가만히 서 있기만 · 목 꺾임·오래 보면 사망 연출을 이 모형에게」.
    /// </summary>
    public sealed class SeventiethPassTests
    {
        [Test]
        public void S5는_복도에서_마주쳐도_판정한다()
        {
            FinalFixture f = new FinalFixture("S5");
            f.Enter(SpaceId.Corridor).Light(true).Pose(40f, 46f).Cue(FinalCues.HallEnd).Wait(1f).Light(false).Wait(4f);
            f.End(FinalCues.HallEnd);
            Assert.AreEqual(FinalOutcome.ThreatKept, f.Last("S5"));

            FinalFixture g = new FinalFixture("S5");
            g.Enter(SpaceId.Corridor).Light(true).Pose(40f, 46f).Cue(FinalCues.HallEnd).Wait(3.1f);
            Assert.AreEqual(Deltas.ThreatFailure, g.Value(FearAxis.Illuminance));
        }

        [Test]
        public void S5는_과학실과_복도_밖에서는_해당하지_않는다()
        {
            FinalFixture f = new FinalFixture("S5");
            f.Enter(SpaceId.Toilet).Light(true).Pose(0f, 34f).Cue(FinalCues.HallEnd).Wait(4f);
            Assert.AreEqual(0, f.Value(FearAxis.Illuminance));
        }

        private static ProgramRequest Request(int day, Band band)
        {
            return new ProgramRequest { Day = day, Shown = FixedBands.All(band), Survival = FixedBands.All(band) };
        }

        [Test]
        public void 복도_끝에_선_자는_모형_급습_전날에만_나온다()
        {
            Band[] floors = { Band.Band0, Band.Band1, Band.Band1, Band.Band2, Band.Band3 };
            for (int seed = 0; seed < 40; seed++)
            {
                ProgramDirector d = new ProgramDirector(new System.Random(seed));
                for (int day = 1; day <= 5; day++)
                {
                    NightProgram p = d.Build(Request(day, floors[day - 1]));
                    Assert.AreEqual(day == 4, p.HasEncounter(ProgramCatalog.HallEndFigure), "시드 " + seed + " · " + day + "일차 복도 끝\n" + p.Report);
                    Assert.AreEqual(day == 5, p.HasEncounter(ProgramCatalog.ModelRush), "시드 " + seed + " · " + day + "일차 급습\n" + p.Report);
                }

                Assert.AreEqual(4, d.HallFigureDay);
                Assert.AreEqual(5, d.RushDay);
            }
        }

        [Test]
        public void S3_위반_예약은_다음_밤_복도_끝_그다음_밤_급습으로_앞당긴다()
        {
            ProgramDirector d = new ProgramDirector(new System.Random(11));
            d.Build(Request(1, Band.Band0));
            d.Build(Request(2, Band.Band1));
            d.Reserve(ProgramCatalog.ModelRush);
            NightProgram day3 = d.Build(Request(3, Band.Band1));
            NightProgram day4 = d.Build(Request(4, Band.Band2));
            NightProgram day5 = d.Build(Request(5, Band.Band3));

            Assert.IsTrue(day3.HasEncounter(ProgramCatalog.HallEndFigure), day3.Report);
            Assert.IsFalse(day3.HasEncounter(ProgramCatalog.ModelRush), day3.Report);
            Assert.IsTrue(day4.HasEncounter(ProgramCatalog.ModelRush), day4.Report);
            Assert.IsFalse(day4.HasEncounter(ProgramCatalog.HallEndFigure), day4.Report);
            Assert.IsFalse(day5.HasEncounter(ProgramCatalog.ModelRush) || day5.HasEncounter(ProgramCatalog.HallEndFigure), "회차에 한 번: " + day5.Report);

            // 복도 끝이 이미 선 날(급습 전날)의 예약은 사슬을 바꾸지 않는다.
            ProgramDirector e = new ProgramDirector(new System.Random(12));
            for (int day = 1; day <= 3; day++) e.Build(Request(day, Band.Band1));
            e.Build(Request(4, Band.Band2));
            e.Reserve(ProgramCatalog.ModelRush);
            Assert.IsTrue(e.Build(Request(5, Band.Band3)).HasEncounter(ProgramCatalog.ModelRush));
        }

        [Test]
        public void 응시_붙잡힘과_목_꺾임의_대상은_복도_끝_모형이다()
        {
            Assert.IsTrue(FixedMobStare.IsFixedMob(FinalCues.HallFigureTarget));
            Assert.IsFalse(FixedMobStare.IsFixedMob(FinalCues.ModelTarget));
            StringAssert.Contains("복도 끝 인체 모형", FixedMobStare.SourceName(FixedMobStare.SourcePrefix + FinalCues.HallFigureTarget));

            EncounterScript s = EncounterScripts.Find(ProgramCatalog.HallEndFigure);
            Assert.AreEqual(StageAnchors.RushHall, s.StageAnchor, "모형 급습이 시작되는 자리와 같다");
            Assert.AreEqual("mob.dummy.stand", s.StandIn);
            Assert.AreEqual("dummy", ProgramCatalog.Encounter(ProgramCatalog.HallEndFigure).Mob);
        }

        [Test]
        public void 모형_급습은_복도_끝_비상등_자리에_선다()
        {
            EncounterScript s = EncounterScripts.Find(ProgramCatalog.ModelRush);
            Assert.IsNotNull(s);
            Assert.AreEqual(StageAnchors.RushHall, s.StageAnchor);
            Assert.AreEqual("mob.dummy", s.StandIn);
        }
    }
}

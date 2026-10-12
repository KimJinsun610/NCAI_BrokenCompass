using NUnit.Framework;

namespace NightDuty.Tests
{
    /// <summary>
    /// 84차(2026-10-10) — 민: 「현재 복도 조우가 복도 급습의 빌드업이 되는 것처럼, 창밖의 노란 얼굴 조우가 도서관 노란 얼굴 조우의 빌드업이 되면 좋겠어」.
    /// 창밖 노란 얼굴(SuitMan 되살림) → 다음 날 도서관 노란 얼굴 — 날짜 사슬. 85차: 대응 수칙 L3는 문간 노란 얼굴만.
    /// </summary>
    public sealed class EightyFourthPassTests
    {
        private static ProgramRequest Request(int day, Band band)
        {
            return new ProgramRequest { Day = day, Shown = FixedBands.All(band), Survival = FixedBands.All(band) };
        }

        [Test]
        public void 창밖_노란_얼굴은_3일차_도서관_노란_얼굴은_4_5일차_중_하루()
        {
            Band[] floors = { Band.Band0, Band.Band1, Band.Band1, Band.Band2, Band.Band3 };
            bool[] windowSeen = new bool[6];
            bool[] faceSeen = new bool[6];
            for (int seed = 0; seed < 40; seed++)
            {
                ProgramDirector d = new ProgramDirector(new System.Random(seed));
                Assert.That(d.WindowFaceDay, Is.InRange(ProgramDirector.WindowFaceFrom, ProgramDirector.WindowFaceTo));
                Assert.That(d.FaceDay, Is.InRange(ProgramDirector.DefaultFaceDay, ProgramDirector.FaceTo));
                windowSeen[d.WindowFaceDay] = true;
                faceSeen[d.FaceDay] = true;
                for (int day = 1; day <= 5; day++)
                {
                    NightProgram p = d.Build(Request(day, floors[day - 1]));
                    bool window = day == d.WindowFaceDay;   // 103차(민): 2·3일차 중 하루 창밖 · 103차 후속: 3일차
                    bool face = day == d.FaceDay;           // 103차(민): 4·5일차 중 하루 도서관
                    Assert.AreEqual(window, d.IsWindowFaceDay(day));
                    Assert.AreEqual(face, d.IsFaceDay(day));
                    Assert.AreEqual(window, p.HasEncounter(ProgramCatalog.SuitMan), "시드 " + seed + " · " + day + "일차 창밖\n" + p.Report);
                    Assert.AreEqual(face, p.HasEncounter(ProgramCatalog.YellowFace), "시드 " + seed + " · " + day + "일차 문간\n" + p.Report);
                    if (face) Assert.IsTrue(p.Has("L3"), "문간 노란 얼굴 밤 L3 — " + p.Report);
                    if (window) Assert.IsFalse(p.Has("L3"), "85차: 창밖 노란 얼굴 밤에는 L3 없음 — " + p.Report);
                }
            }

            Assert.IsTrue(windowSeen[3] && !windowSeen[2], "103차 후속(사용자 결정): 창밖 날은 3일차 고정(옛 2·3일차 중 하루 — 2일차가 회차에서 가장 무거운 밤이 됐다)");
            Assert.IsTrue(faceSeen[4] && faceSeen[5], "도서관 날은 회차마다 4·5일차 중에서 고른다");
        }

        [Test]
        public void 노란_얼굴_날짜는_시험용으로_정할_수_있고_구간_안으로_잘린다()
        {
            ProgramDirector d = new ProgramDirector(new System.Random(1));
            d.SetFaceDays(3, 5);
            Assert.AreEqual(3, d.WindowFaceDay);
            Assert.AreEqual(5, d.FaceDay);
            d.SetFaceDays(9, 1);
            Assert.AreEqual(ProgramDirector.WindowFaceTo, d.WindowFaceDay);
            Assert.AreEqual(ProgramDirector.DefaultFaceDay, d.FaceDay);
        }

        [Test]
        public void 창밖_노란_얼굴의_정의와_대본()
        {
            EncounterDef e = ProgramCatalog.Encounter(ProgramCatalog.SuitMan);
            Assert.AreEqual(string.Empty, e.ResponseRule, "85차: L3는 문간 노란 얼굴만");
            Assert.AreEqual("창밖 노란 얼굴", e.Name);
            Assert.IsFalse(ProgramCatalog.IsEncounterRetired(ProgramCatalog.SuitMan));
            Assert.IsTrue(ProgramDirector.IsChained(ProgramCatalog.SuitMan));
            Assert.IsTrue(ProgramDirector.IsChained(ProgramCatalog.YellowFace));

            EncounterScript s = EncounterScripts.Find(ProgramCatalog.SuitMan);
            EncounterScript door = EncounterScripts.Find(ProgramCatalog.YellowFace);
            Assert.AreEqual(FinalCues.YellowFace, door.Cue, "문간 = L3 단서");
            Assert.AreEqual(FinalCues.WindowKnock, s.Cue, "85차: 창밖은 창 두드림만(L3 판정 없음)");
            Assert.AreEqual(FinalCues.WindowFaceTarget, s.AnchorId, "85차: 창밖은 제 응시 대상(2초 응시 → 달아남)");
            Assert.AreEqual("mob.windowman", s.StandIn);
            Assert.AreEqual(StageAnchors.WindowMan, s.StageAnchor);
        }

        [Test]
        public void 예약으로는_창밖_노란_얼굴을_걸지_않는다()
        {
            ProgramDirector d = new ProgramDirector(new System.Random(5));
            d.Build(Request(1, Band.Band0));
            d.Build(Request(2, Band.Band1));
            d.Build(Request(3, Band.Band1));
            d.Reserve(ProgramCatalog.SuitMan);
            NightProgram day4 = d.Build(Request(4, Band.Band2));
            Assert.IsFalse(day4.HasEncounter(ProgramCatalog.SuitMan), day4.Report);
        }
    }
}

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace NightDuty.Tests
{
    /// <summary>새 수칙 판정 책 조립(판정기 없이 신호만 넣는다).</summary>
    internal sealed class FinalFixture
    {
        public readonly FearAxisSystem Axes = new FearAxisSystem();
        public readonly FinalRuleBook Book;
        public readonly Dictionary<string, Vector3> Anchors = new Dictionary<string, Vector3>();
        public readonly List<string> Requested = new List<string>();
        public readonly List<string> Armed = new List<string>();

        public FinalFixture(params string[] ids)
        {
            List<RuleDef> deck = new List<RuleDef>();
            foreach (string id in ids) deck.Add(ProgramCatalog.Rule(id));
            Book = new FinalRuleBook(deck, Axes);
            Book.Anchor = id =>
            {
                Vector3 v;
                return Anchors.TryGetValue(id, out v) ? v : (Vector3?)null;
            };
            Book.EncounterRequested += Requested.Add;
            Book.ReverseReportArmed += Armed.Add;
        }

        public FinalFixture Send(JudgeSignal s)
        {
            Book.Dispatch(s, true);
            return this;
        }

        public FinalFixture Enter(SpaceId space)
        {
            return Send(JudgeSignal.OfSpace(SignalKind.SpaceEntered, space));
        }

        public FinalFixture Exit(SpaceId space)
        {
            return Send(JudgeSignal.OfSpace(SignalKind.SpaceExited, space));
        }

        public FinalFixture Pose(float x, float z, float yaw = 0f)
        {
            return Send(JudgeSignal.Pose(new Vector3(x, 0f, z), yaw));
        }

        public FinalFixture Light(bool on)
        {
            return Send(JudgeSignal.Flashlight(on));
        }

        public FinalFixture Cue(string id, Vector3 at = default(Vector3))
        {
            return Send(JudgeSignal.Cue(id, at));
        }

        public FinalFixture End(string id)
        {
            return Send(JudgeSignal.CueEnd(id));
        }

        /// <summary>0.1초 간격으로 판정 시간을 흘린다.</summary>
        public FinalFixture Wait(float seconds)
        {
            int n = Mathf.RoundToInt(seconds * 10f);
            for (int i = 0; i < n; i++) Book.Dispatch(JudgeSignal.Tick(0.1f), true);
            return this;
        }

        /// <summary>0.1초 샘플을 n초 동안 보낸다.</summary>
        public FinalFixture Samples(System.Func<JudgeSignal> make, float seconds)
        {
            int n = Mathf.RoundToInt(seconds * 10f);
            for (int i = 0; i < n; i++)
            {
                Book.Dispatch(make(), true);
                Book.Dispatch(JudgeSignal.Tick(0.1f), true);
            }

            return this;
        }

        public int Value(FearAxis axis)
        {
            return Axes.GetValue(axis);
        }

        public FinalOutcome? Last(string id)
        {
            for (int i = Book.Results.Count - 1; i >= 0; i--)
            {
                if (Book.Results[i].RuleId == id) return Book.Results[i].Outcome;
            }

            return null;
        }

        public int Count(string id)
        {
            int n = 0;
            foreach (FinalRuleResult r in Book.Results)
            {
                if (r.RuleId == id) n++;
            }

            return n;
        }
    }

    /// <summary>새 수칙 판정 책의 공통 규칙(일반 +12 한 번·밤 종료 준수 +2·위협 에피소드 +20/+3·판정 정지·스냅샷).</summary>
    public sealed class FinalRuleBookTests
    {
        [Test]
        public void 카탈로그_31장_모두_판정기가_있다()
        {
            foreach (RuleDef def in ProgramCatalog.AllRules)
            {
                Assert.IsNotNull(FinalJudges.Create(def), def.Id);
            }
        }

        [Test]
        public void 일반수칙은_첫_위반에서_한번만_12()
        {
            FinalFixture f = new FinalFixture("H1");
            f.Anchors[FinalCues.H1Object] = Vector3.zero;
            f.Pose(3f, 0f).Pose(1f, 0f).Pose(0.5f, 0f).Pose(3f, 0f).Pose(1f, 0f);
            Assert.AreEqual(Deltas.RuleViolation, f.Value(FearAxis.Layout));
            Assert.AreEqual(1, f.Count("H1"));
        }

        [Test]
        public void 방아쇠가_왔고_지켰으면_밤종료에_신뢰_2()
        {
            FinalFixture f = new FinalFixture("H1", "L4");
            f.Anchors[FinalCues.H1Object] = Vector3.zero;
            f.Pose(3f, 0f);   // 반경 3배 안 — 방아쇠.
            int trust = f.Value(FearAxis.Trust);
            f.Book.EndNight();
            Assert.AreEqual(trust + Deltas.TrustComply, f.Value(FearAxis.Trust));
            Assert.AreEqual(FinalOutcome.Complied, f.Last("H1"));
            Assert.IsNull(f.Last("L4"), "방아쇠가 오지 않은 수칙은 준수로 치지 않는다");
        }

        [Test]
        public void 위반한_수칙은_밤종료_준수가_없다()
        {
            FinalFixture f = new FinalFixture("H1");
            f.Anchors[FinalCues.H1Object] = Vector3.zero;
            f.Pose(0.5f, 0f);
            int trust = f.Value(FearAxis.Trust);
            f.Book.EndNight();
            Assert.AreEqual(trust, f.Value(FearAxis.Trust));
        }

        [Test]
        public void 판정정지_중에는_상태만_갱신한다()
        {
            FinalFixture f = new FinalFixture("G1");
            f.Book.Dispatch(JudgeSignal.OfSpace(SignalKind.SpaceEntered, SpaceId.Corridor), false);
            f.Book.Dispatch(JudgeSignal.Run(true), false);
            for (int i = 0; i < 20; i++) f.Book.Dispatch(JudgeSignal.Tick(0.1f), false);
            Assert.AreEqual(0, f.Value(FearAxis.Auditory));
            Assert.AreEqual(SpaceId.Corridor, f.Book.World.Space);
            Assert.IsTrue(f.Book.World.Running);

            f.Wait(1.1f);   // 판정 구간이 되면 판정한다.
            Assert.AreEqual(Deltas.RuleViolation, f.Value(FearAxis.Auditory));
        }

        [Test]
        public void 스냅샷으로_되돌리면_그뒤_위반이_사라진다()
        {
            FinalFixture f = new FinalFixture("G1", "H1");
            f.Anchors[FinalCues.H1Object] = Vector3.zero;
            f.Pose(0.5f, 0f);   // H1 위반(스냅샷 전).
            object state = f.Book.CaptureState();

            f.Enter(SpaceId.Corridor).Send(JudgeSignal.Run(true)).Wait(1.2f);
            Assert.IsTrue(f.Book.Judge("G1").Violated);
            Assert.AreEqual(2, f.Book.Results.Count);

            f.Book.RestoreState(state);
            Assert.IsFalse(f.Book.Judge("G1").Violated);
            Assert.IsTrue(f.Book.Judge("H1").Violated);
            Assert.AreEqual(1, f.Book.Results.Count);
            Assert.IsFalse(f.Book.World.Running, "재시작하면 달리기 상태를 버린다");
        }

        [Test]
        public void 붙잡히면_더_판정하지_않는다()
        {
            FinalFixture f = new FinalFixture("G1");
            f.Axes.Apply(FearAxis.Auditory, 100, "test", SpaceId.None);
            Assert.IsTrue(f.Axes.IsLocked);
            int before = f.Book.Results.Count;
            f.Enter(SpaceId.Corridor).Send(JudgeSignal.Run(true)).Wait(2f);
            Assert.AreEqual(before, f.Book.Results.Count);
        }
    }

    /// <summary>복도·교실 판정기.</summary>
    public sealed class FinalCorridorClassroomTests
    {
        [Test]
        public void H2_연출이_연_문을_닫으면_위반()
        {
            FinalFixture f = new FinalFixture("H2");
            f.Send(JudgeSignal.DoorCommand("door.a", true, ActionSource.Player));
            Assert.AreEqual(0, f.Value(FearAxis.Layout), "연출이 연 문이 아니면 묻지 않는다");
            f.Send(JudgeSignal.Target(SignalKind.DoorAutoOpenObserved, "door.a"));
            f.Send(JudgeSignal.DoorCommand("door.a", true, ActionSource.Direction));
            Assert.AreEqual(0, f.Value(FearAxis.Layout), "연출이 닫은 것은 플레이어 조작이 아니다");
            f.Send(JudgeSignal.DoorCommand("door.a", true, ActionSource.Player));
            Assert.AreEqual(Deltas.RuleViolation, f.Value(FearAxis.Layout));
        }

        [Test]
        public void H3_6초_안에_방으로_대피해_끝나고_2초_머물면_신뢰_3()
        {
            FinalFixture f = new FinalFixture("H3");
            f.Enter(SpaceId.Corridor).Cue(FinalCues.Footsteps).Wait(3f);
            f.Exit(SpaceId.Corridor).Enter(SpaceId.Classroom_1_1).Wait(4f);
            f.End(FinalCues.Footsteps).Wait(2.1f);
            Assert.AreEqual(FinalOutcome.ThreatKept, f.Last("H3"));
            Assert.AreEqual(Deltas.TrustThreatSuccess, f.Value(FearAxis.Trust));
        }

        [Test]
        public void H3_복도에_6초_남으면_20()
        {
            FinalFixture f = new FinalFixture("H3");
            f.Enter(SpaceId.Corridor).Cue(FinalCues.Footsteps).Wait(6.1f);
            Assert.AreEqual(Deltas.ThreatFailure, f.Value(FearAxis.Auditory));
        }

        [Test]
        public void H3_끝나기_전에_복도로_나오면_20()
        {
            FinalFixture f = new FinalFixture("H3");
            f.Enter(SpaceId.Library).Cue(FinalCues.Footsteps).Wait(2f);
            f.Exit(SpaceId.Library).Enter(SpaceId.Corridor);
            Assert.AreEqual(FinalOutcome.Violated, f.Last("H3"));
        }

        [Test]
        public void H3_위협은_조우마다_정산한다()
        {
            FinalFixture f = new FinalFixture("H3");
            f.Enter(SpaceId.Corridor).Cue(FinalCues.Footsteps).Wait(6.1f);
            f.End(FinalCues.Footsteps);
            f.Cue(FinalCues.Footsteps).Wait(6.1f);
            Assert.AreEqual(2 * Deltas.ThreatFailure, f.Value(FearAxis.Auditory));
        }

        [Test]
        public void H4_등뒤_목소리로_돌아보면_20()
        {
            FinalFixture f = new FinalFixture("H4");
            f.Pose(0f, 0f, 0f).Cue(FinalCues.Voice, new Vector3(0f, 0f, -5f));
            f.Pose(0f, 0f, 60f);
            Assert.IsNull(f.Last("H4"), "60°는 아직 응답이 아니다");
            f.Pose(0f, 0f, 170f);
            Assert.AreEqual(FinalOutcome.Violated, f.Last("H4"));
        }

        [Test]
        public void H4_돌아보지_않고_끝나면_신뢰_3()
        {
            FinalFixture f = new FinalFixture("H4");
            f.Pose(0f, 0f, 0f).Cue(FinalCues.Voice, new Vector3(0f, 0f, -5f));
            f.Pose(0f, 1f, 20f).Wait(3f).End(FinalCues.Voice);
            Assert.AreEqual(FinalOutcome.ThreatKept, f.Last("H4"));
        }

        [Test]
        public void C1_판서_중과_끝난_직후_1초는_입실_금지()
        {
            FinalFixture f = new FinalFixture("C1");
            f.Enter(SpaceId.Corridor).Cue(FinalCues.Chalk).Wait(2f).End(FinalCues.Chalk).Wait(0.5f);
            f.Enter(SpaceId.Classroom_1_3);
            Assert.AreEqual(Deltas.RuleViolation, f.Value(FearAxis.Auditory));

            FinalFixture g = new FinalFixture("C1");
            g.Enter(SpaceId.Corridor).Cue(FinalCues.Chalk).Wait(2f).End(FinalCues.Chalk).Wait(1.2f);
            g.Enter(SpaceId.Classroom_1_3);
            Assert.AreEqual(0, g.Value(FearAxis.Auditory));
        }

        [Test]
        public void C2_다리를_3초_응시하면_위반()
        {
            FinalFixture f = new FinalFixture("C2");
            f.Samples(() => JudgeSignal.Gaze(FinalCues.LegsTarget, 0.1f), 2.9f);
            Assert.AreEqual(0, f.Value(FearAxis.Auditory));
            f.Samples(() => JudgeSignal.Gaze(FinalCues.LegsTarget, 0.1f), 0.2f);
            Assert.AreEqual(Deltas.RuleViolation, f.Value(FearAxis.Auditory));
        }

        [Test]
        public void C3_수업_중_움직이면_20_종까지_버티면_3()
        {
            FinalFixture f = new FinalFixture("C3");
            f.Enter(SpaceId.Classroom_1_1).Pose(0f, 0f).Cue(FinalCues.BoySeated);
            f.Pose(0.1f, 0f).Wait(5f);
            Assert.IsNull(f.Last("C3"), "0.2m 안은 정지");
            f.Cue(FinalCues.Bell);
            Assert.AreEqual(FinalOutcome.ThreatKept, f.Last("C3"));

            FinalFixture g = new FinalFixture("C3");
            g.Enter(SpaceId.Classroom_1_1).Pose(0f, 0f).Cue(FinalCues.BoySeated).Pose(0.5f, 0f);
            Assert.AreEqual(Deltas.ThreatFailure, g.Value(FearAxis.Auditory));
        }

        [Test]
        public void C3_교실_밖이면_해당없음()
        {
            FinalFixture f = new FinalFixture("C3");
            f.Enter(SpaceId.Corridor).Pose(0f, 0f).Cue(FinalCues.BoySeated).Pose(3f, 0f).Cue(FinalCues.Bell);
            Assert.AreEqual(0, f.Book.Results.Count);
            Assert.IsFalse(f.Book.Judge("C3").Triggered);
        }

        [Test]
        public void C4_붉은빛_아래서_손전등을_끄면_위반()
        {
            FinalFixture f = new FinalFixture("C4");
            f.Enter(SpaceId.Classroom_1_1).Light(true).Cue(FinalCues.RedLight).Wait(1f).Light(false);
            Assert.AreEqual(Deltas.RuleViolation, f.Value(FearAxis.Illuminance));
        }

        [Test]
        public void C5_없던_문_구역에_들어가면_위반()
        {
            FinalFixture f = new FinalFixture("C5");
            f.Send(JudgeSignal.Target(SignalKind.ZoneEntered, FinalCues.PhantomDoorZone));
            Assert.AreEqual(0, f.Value(FearAxis.Layout), "단서 전에는 문이 없다");
            f.Cue(FinalCues.PhantomDoor).Send(JudgeSignal.Target(SignalKind.ZoneEntered, FinalCues.PhantomDoorZone));
            Assert.AreEqual(Deltas.RuleViolation, f.Value(FearAxis.Layout));
        }

        [Test]
        public void G1_복도에서_1초_뛰면_위반_교실은_괜찮다()
        {
            FinalFixture f = new FinalFixture("G1");
            f.Enter(SpaceId.Classroom_1_1).Send(JudgeSignal.Run(true)).Wait(3f);
            Assert.AreEqual(0, f.Value(FearAxis.Auditory));
            f.Exit(SpaceId.Classroom_1_1).Enter(SpaceId.Corridor).Wait(0.5f);
            f.Send(JudgeSignal.Run(false)).Send(JudgeSignal.Run(true)).Wait(0.8f);
            Assert.AreEqual(0, f.Value(FearAxis.Auditory), "짧은 발걸음은 봐준다");
            f.Wait(0.3f);
            Assert.AreEqual(Deltas.RuleViolation, f.Value(FearAxis.Auditory));
        }
    }

    /// <summary>과학실·화장실·도서관·경비실 판정기.</summary>
    public sealed class FinalRoomTests
    {
        [Test]
        public void S1_반대편으로_나가면_통로로_쓴_것()
        {
            FinalFixture f = new FinalFixture("S1");
            f.Anchors[FinalCues.S1Center] = new Vector3(48f, 0f, 42f);
            f.Pose(43f, 42f).Enter(SpaceId.ScienceRoom).Pose(47f, 42f).Pose(43f, 42f).Exit(SpaceId.ScienceRoom);
            Assert.AreEqual(0, f.Value(FearAxis.Layout));
            f.Pose(43f, 42f).Enter(SpaceId.ScienceRoom).Pose(53f, 42f).Exit(SpaceId.ScienceRoom);
            Assert.AreEqual(Deltas.RuleViolation, f.Value(FearAxis.Layout));
        }

        [Test]
        public void S2_깨지는_소리_10초_안에_나오고_다시_들어가면_위반()
        {
            FinalFixture f = new FinalFixture("S2");
            f.Enter(SpaceId.ScienceRoom).Cue(FinalCues.Glass).Wait(5f).Exit(SpaceId.ScienceRoom);
            Assert.AreEqual(0, f.Value(FearAxis.Auditory));
            f.Wait(10f).Enter(SpaceId.ScienceRoom);
            Assert.AreEqual(Deltas.RuleViolation, f.Value(FearAxis.Auditory));

            FinalFixture g = new FinalFixture("S2");
            g.Enter(SpaceId.ScienceRoom).Cue(FinalCues.Glass).Wait(10.1f);
            Assert.AreEqual(Deltas.RuleViolation, g.Value(FearAxis.Auditory));
        }

        [Test]
        public void S3_테이프_밖에서_2초_비추면_위반하고_모형급습_예약()
        {
            FinalFixture f = new FinalFixture("S3");
            f.Send(JudgeSignal.Target(SignalKind.ZoneEntered, FinalCues.TapeZone));
            f.Samples(() => JudgeSignal.Beam(FinalCues.ModelTarget, 0.1f), 3f);
            Assert.AreEqual(0, f.Value(FearAxis.Illuminance), "테이프 안은 괜찮다");
            f.Send(JudgeSignal.Target(SignalKind.ZoneExited, FinalCues.TapeZone));
            f.Samples(() => JudgeSignal.Beam(FinalCues.ModelTarget, 0.1f), 2f);
            Assert.AreEqual(Deltas.RuleViolation, f.Value(FearAxis.Illuminance));
            CollectionAssert.AreEqual(new[] { ProgramCatalog.ModelRush }, f.Requested);
        }

        [Test]
        public void S4_소등_중_어두운_구역에서_불이_꺼져있으면_위반()
        {
            FinalFixture f = new FinalFixture("S4");
            f.Enter(SpaceId.ScienceRoom).Light(false).Cue(FinalCues.ScienceBlackout).Wait(3f);
            Assert.AreEqual(0, f.Value(FearAxis.Illuminance), "구역 밖");
            f.Send(JudgeSignal.Target(SignalKind.ZoneEntered, FinalCues.ScienceDarkZone)).Wait(1.2f);
            Assert.AreEqual(Deltas.RuleViolation, f.Value(FearAxis.Illuminance));
        }

        [Test]
        public void S5_3초_안에_불을_끄고_가만히_기다리면_신뢰_3()
        {
            FinalFixture f = new FinalFixture("S5");
            f.Enter(SpaceId.ScienceRoom).Light(true).Pose(45f, 42f).Cue(FinalCues.HallEnd).Wait(1f).Light(false).Wait(4f);
            f.Pose(45.1f, 42f).Wait(2f).End(FinalCues.HallEnd);
            Assert.AreEqual(FinalOutcome.ThreatKept, f.Last("S5"));
        }

        [Test]
        public void S5_불을_켜둔_채면_20_기다리다_켜도_20()
        {
            FinalFixture f = new FinalFixture("S5");
            f.Enter(SpaceId.ScienceRoom).Light(true).Pose(45f, 42f).Cue(FinalCues.HallEnd).Wait(3.1f);
            Assert.AreEqual(Deltas.ThreatFailure, f.Value(FearAxis.Illuminance));

            FinalFixture g = new FinalFixture("S5");
            g.Enter(SpaceId.ScienceRoom).Light(false).Pose(45f, 42f).Cue(FinalCues.HallEnd).Wait(3.1f).Light(true);
            Assert.AreEqual(Deltas.ThreatFailure, g.Value(FearAxis.Illuminance));
        }

        [Test]
        public void T3_정전에_움직이면_20()
        {
            FinalFixture f = new FinalFixture("T3");
            f.Enter(SpaceId.Toilet).Light(false).Pose(0f, 34f).Cue(FinalCues.ToiletBlackout).Wait(3.1f).Pose(1f, 34f);
            Assert.AreEqual(Deltas.ThreatFailure, f.Value(FearAxis.Illuminance));
        }

        [Test]
        public void T1_물_내려가기_8초_안에_나온다()
        {
            FinalFixture f = new FinalFixture("T1");
            f.Enter(SpaceId.Toilet).Cue(FinalCues.Flush).Wait(8.1f);
            Assert.AreEqual(Deltas.RuleViolation, f.Value(FearAxis.Auditory));

            FinalFixture g = new FinalFixture("T1");
            g.Enter(SpaceId.Toilet).Cue(FinalCues.Flush).Wait(4f).Exit(SpaceId.Toilet).Wait(8f);
            Assert.AreEqual(0, g.Value(FearAxis.Auditory));
            Assert.IsTrue(g.Book.Judge("T1").Triggered);
        }

        [Test]
        public void T2_사용중_칸_구역에_들어가면_위반()
        {
            FinalFixture f = new FinalFixture("T2");
            f.Cue(FinalCues.StallOccupied + "@toilet.stall2");
            f.Send(JudgeSignal.Target(SignalKind.ZoneEntered, "toilet.stall1"));
            Assert.AreEqual(0, f.Value(FearAxis.Layout));
            f.Send(JudgeSignal.Target(SignalKind.ZoneEntered, "toilet.stall2"));
            Assert.AreEqual(Deltas.RuleViolation, f.Value(FearAxis.Layout));
        }

        [Test]
        public void T4_여자아이_단서가_역보고를_건다()
        {
            FinalFixture f = new FinalFixture("T4");
            f.Cue(FinalCues.GirlStall);
            CollectionAssert.AreEqual(new[] { InspectionCatalog.ReverseReportItem }, f.Armed);
            f.Book.NoteExternal("T4", false, "역보고 준수");
            Assert.AreEqual(FinalOutcome.Noted, f.Last("T4"));
            f.Book.EndNight();
            Assert.AreEqual(0, f.Value(FearAxis.Trust), "T4의 신뢰는 점검판이 준다");
        }

        [Test]
        public void T5_불켜진_칸이_있으면_3초_안에_켜고_나갈때까지_유지()
        {
            FinalFixture f = new FinalFixture("T5");
            f.Enter(SpaceId.Toilet).Light(false).Cue(FinalCues.StallLit).Wait(1f).Light(true).Wait(5f);
            f.End(FinalCues.StallLit).Light(false).Wait(4f);
            Assert.AreEqual(Deltas.RuleViolation, f.Value(FearAxis.Illuminance), "단서가 끝나도 화장실을 나갈 때까지");

            FinalFixture g = new FinalFixture("T5");
            g.Enter(SpaceId.Toilet).Light(true).Cue(FinalCues.StallLit).Wait(2f).Exit(SpaceId.Toilet).Light(false).Wait(5f);
            Assert.AreEqual(0, g.Value(FearAxis.Illuminance));
        }

        [Test]
        public void L1_쓰러진_책장_곁_3초()
        {
            FinalFixture f = new FinalFixture("L1");
            f.Anchors[FinalCues.L1Shelf] = new Vector3(10f, 0f, 50f);
            f.Pose(10f, 51f).Wait(2f).Pose(10f, 53f).Pose(10f, 51f).Wait(2f);
            Assert.AreEqual(0, f.Value(FearAxis.Auditory), "나갔다 오면 다시 잰다");
            f.Wait(1.1f);
            Assert.AreEqual(Deltas.RuleViolation, f.Value(FearAxis.Auditory));
        }

        [Test]
        public void L3_노란얼굴에서_빛이_떨어지면_20과_정장남자_예약()
        {
            FinalFixture f = new FinalFixture("L3");
            f.Cue(FinalCues.YellowFace).Wait(1f);
            f.Samples(() => JudgeSignal.Beam(FinalCues.FaceTarget, 0.1f), 3f);
            Assert.IsNull(f.Last("L3"));
            f.Samples(() => JudgeSignal.Beam(string.Empty, 0.1f), 0.7f);
            Assert.AreEqual(FinalOutcome.Violated, f.Last("L3"));
            CollectionAssert.AreEqual(new[] { ProgramCatalog.SuitMan }, f.Requested);
        }

        [Test]
        public void L3_끝까지_비추면_신뢰_3()
        {
            FinalFixture f = new FinalFixture("L3");
            f.Cue(FinalCues.YellowFace).Wait(1f);
            f.Samples(() => JudgeSignal.Beam(FinalCues.FaceTarget, 0.1f), 5f);
            f.End(FinalCues.YellowFace);
            Assert.AreEqual(FinalOutcome.ThreatKept, f.Last("L3"));
            Assert.AreEqual(0, f.Requested.Count);
        }

        [Test]
        public void L5_창밖_남자를_비추면_20()
        {
            FinalFixture f = new FinalFixture("L5");
            f.Cue(FinalCues.WindowKnock).Samples(() => JudgeSignal.Gaze(FinalCues.ManTarget, 0.1f), 1.5f);
            Assert.IsNull(f.Last("L5"), "잠깐 보는 건 인사가 아니다");
            f.Send(JudgeSignal.Beam(FinalCues.ManTarget, 0.1f));
            Assert.AreEqual(Deltas.ThreatFailure, f.Value(FearAxis.Auditory));
        }

        [Test]
        public void K1_사람이_지나가기_전_채널을_넘기면_위반()
        {
            FinalFixture f = new FinalFixture("K1");
            f.Send(JudgeSignal.Channel("cctv.ch1"));
            Assert.AreEqual(0, f.Value(FearAxis.Layout));
            f.Cue(FinalCues.CctvPerson).Wait(2f).Send(JudgeSignal.Channel("cctv.ch2"));
            Assert.AreEqual(Deltas.RuleViolation, f.Value(FearAxis.Layout));
        }

        [Test]
        public void K2_빈방_채널을_3초_보면_위반()
        {
            FinalFixture f = new FinalFixture("K2");
            f.Cue(FinalCues.EmptyRoom + "@cctv.ch3");
            f.Samples(() => JudgeSignal.CctvView("cctv.ch1", 0.1f), 5f);
            Assert.AreEqual(0, f.Value(FearAxis.Auditory));
            f.Samples(() => JudgeSignal.CctvView("cctv.ch3", 0.1f), 3.1f);
            Assert.AreEqual(Deltas.RuleViolation, f.Value(FearAxis.Auditory));
        }

        [Test]
        public void K3_경비실_45초()
        {
            FinalFixture f = new FinalFixture("K3");
            f.Enter(SpaceId.SecurityRoom).Wait(44f).Exit(SpaceId.SecurityRoom).Enter(SpaceId.SecurityRoom).Wait(44f);
            Assert.AreEqual(0, f.Value(FearAxis.Auditory));
            f.Wait(1.1f);
            Assert.AreEqual(Deltas.RuleViolation, f.Value(FearAxis.Auditory));
        }
    }

    /// <summary>NightRun 연결(편성 켜짐 → 새 판정 책·재시작 스냅샷·이벤트).</summary>
    public sealed class NightRunFinalRuleTests
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
        public void 편성이_켜지면_그날_덱으로_판정하고_옛덱은_쉰다()
        {
            NightRun.StartNewRun();
            NightRun.ProgramEnabled = true;
            NightRun.BeginNight(1, () => 30);

            Assert.IsNotNull(NightRun.FinalRules);
            Assert.Greater(NightRun.TodayDeck.Count, 0, "태블릿에는 새 수칙 + 점검표가 나간다");
            foreach (RuleSO card in NightRun.TodayDeck)
            {
                Assert.IsTrue(NightRun.Program.Has(card.CardId) || card.CardId.StartsWith(InspectionCatalog.TargetPrefix), "태블릿 표시 카드: " + card.CardId);
            }
            Assert.IsNotNull(NightRun.FinalRules.Judge("G1"), "1일차 공통 G1");

            List<FinalRuleResult> raised = new List<FinalRuleResult>();
            EventBus.FinalRuleSettled += raised.Add;

            NightRun.Send(JudgeSignal.OfSpace(SignalKind.SpaceEntered, SpaceId.Corridor));
            NightRun.Send(JudgeSignal.Run(true));
            for (int i = 0; i < 12; i++) NightRun.Tick(0.1f);

            Assert.AreEqual(1, raised.Count);
            Assert.AreEqual("G1", raised[0].RuleId);
            Assert.AreEqual(Deltas.RuleViolation, NightRun.Axes.GetValue(FearAxis.Auditory));
        }

        [Test]
        public void 재시작하면_새_판정책을_만들지_않고_스냅샷으로_되돌린다()
        {
            NightRun.StartNewRun();
            NightRun.ProgramEnabled = true;
            NightRun.BeginNight(1, () => 30);
            FinalRuleBook book = NightRun.FinalRules;

            NightRun.Send(JudgeSignal.OfSpace(SignalKind.SpaceEntered, SpaceId.Corridor));
            NightRun.Send(JudgeSignal.Run(true));
            for (int i = 0; i < 12; i++) NightRun.Tick(0.1f);
            Assert.AreEqual(1, NightRun.FinalResults.Count);

            NightRun.DebugForceCapture(FearAxis.Layout);
            NightRun.RestartAfterCapture();
            Assert.AreSame(book, NightRun.FinalRules);
            Assert.AreEqual(0, NightRun.FinalResults.Count);
            Assert.IsFalse(book.Judge("G1").Violated);
        }

        [Test]
        public void 편성이_꺼지면_새_판정책이_없다()
        {
            NightRun.StartNewRun();
            NightRun.BeginNight(1, () => 30);
            Assert.IsNull(NightRun.FinalRules);
            Assert.AreEqual(0, NightRun.FinalResults.Count);
        }
    }
}

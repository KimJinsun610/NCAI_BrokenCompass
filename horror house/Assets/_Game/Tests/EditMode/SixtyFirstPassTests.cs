using NUnit.Framework;
using UnityEngine;

namespace NightDuty.Tests
{
    /// <summary>61차(2026-10-07 플레이테스트 피드백 1단계) — 몸(키·벽 뚫림·점프), 재시작 배터리.</summary>
    public sealed class SixtyFirstPassTests
    {
        [TearDown]
        public void TearDown()
        {
            NightRun.BatteryEnabled = false;
            NightRun.BatterySeed = null;
            NightRun.StartNewRun();
            EventBus.ClearAll();
        }

        [Test]
        public void 키는_1점95미터_눈은_김진선님_컷신_기준_1점7미터()
        {
            Assert.AreEqual(1.95f, PlayerBodyRules.Height, 1e-5f);
            Assert.AreEqual(1.70f, PlayerBodyRules.EyeHeight, 1e-5f);
            Assert.AreEqual(0.25f, PlayerBodyRules.GrowFrom(1.7f), 1e-5f, "옛 몸 1.7m에서 0.25m 늘어난다");
            Assert.AreEqual(0f, PlayerBodyRules.GrowFrom(1.95f), "이미 늘었으면 다시 늘리지 않는다");
        }

        [Test]
        public void 벽을_파고드는_이동은_벽면을_따라_미끄러진다()
        {
            Vector3 wall = new Vector3(-1f, 0f, 0f);   // +x로 가다 벽에 닿음
            Assert.AreEqual(Vector3.zero, PlayerBodyRules.SlideAlong(new Vector3(1f, 0f, 0f), wall), "정면으로 박으면 멈춘다");

            Vector3 slide = PlayerBodyRules.SlideAlong(new Vector3(1f, 0f, 1f), wall);
            Assert.AreEqual(0f, slide.x, 1e-5f, "벽 안쪽 성분은 깎는다");
            Assert.AreEqual(1f, slide.z, 1e-5f, "벽면 방향 성분은 남는다");

            Vector3 away = new Vector3(-0.5f, 0f, 0.3f);
            Assert.AreEqual(away, PlayerBodyRules.SlideAlong(away, wall), "벽에서 멀어지는 이동은 그대로");

            Vector3 floorish = PlayerBodyRules.SlideAlong(new Vector3(1f, 0f, 0f), new Vector3(0f, 1f, 0f));
            Assert.AreEqual(Vector3.zero, floorish, "수평 성분이 없는 법선(바닥·천장)은 멈춤으로 본다");
        }

        [Test]
        public void 위로_튀는_속도는_묶이고_떨어지는_속도는_그대로()
        {
            Assert.AreEqual(PlayerBodyRules.MaxRiseSpeed, PlayerBodyRules.ClampRise(4f), 1e-5f, "점프·끼임 튐 금지");
            Assert.AreEqual(0.2f, PlayerBodyRules.ClampRise(0.2f), 1e-5f, "문지방 넘기는 작은 솟음은 둔다");
            Assert.AreEqual(-9f, PlayerBodyRules.ClampRise(-9f), 1e-5f);
        }

        [Test]
        public void 한_프레임_0점75미터를_넘는_이동은_순간이동으로_본다()
        {
            Assert.IsFalse(PlayerBodyRules.IsTeleport(new Vector3(0.06f, 0f, 0.02f)), "뛰기 한 프레임");
            Assert.IsTrue(PlayerBodyRules.IsTeleport(new Vector3(3f, 0f, 0f)), "재시작·연출 순간이동");
            Assert.IsFalse(PlayerBodyRules.IsTeleport(new Vector3(0f, 5f, 0f)), "수직은 세지 않는다");
        }

        [Test]
        public void 체크포인트에서_재시작해도_손전등은_가득_예비는_그대로()
        {
            NightRun.StartNewRun();
            NightRun.JudgingWindowEnabled = false;
            NightRun.BatteryEnabled = true;
            NightRun.BatterySeed = 11;
            NightRun.BeginNight(2, () => 30);
            NightRun.Battery.DebugSet(0.6f, 1);
            Assert.IsTrue(NightRun.SignCheckpoint());

            NightRun.Battery.Drain(500f);
            Assert.AreEqual(0f, NightRun.Battery.Charge, 1e-5f);

            NightRun.DebugForceCapture(FearAxis.Auditory);
            Assert.AreEqual(RestartKind.FromCheckpoint, NightRun.RestartAfterCapture().Kind);
            Assert.AreEqual(1f, NightRun.Battery.Charge, 1e-5f, "밤이 초기화되면 배터리가 이어지지 않는다");
            Assert.AreEqual(1, NightRun.Battery.Spare, "체크포인트 때 주머니의 예비는 남는다");
        }

        // ── 2단계: 태블릿 들기 제한 ──

        [Test]
        public void 태블릿은_5초_뒤_내려가고_2초_뒤에_다시_들_수_있다()
        {
            TabletLimit l = new TabletLimit();
            Assert.IsFalse(l.Tick(0.1f, true, false), "막 들었다");
            for (int k = 0; k < 47; k++) Assert.IsFalse(l.Tick(0.1f, true, false), "5초 전");
            Assert.IsTrue(l.Tick(0.3f, true, false), "5초 — 내린다");
            Assert.IsFalse(l.Tick(0.1f, false, false), "내려감");
            Assert.IsTrue(l.Locked);
            Assert.IsTrue(l.Tick(0.5f, true, false), "쉬는 중에 들면 곧바로 내린다");
            Assert.IsFalse(l.Tick(1.6f, false, false));
            Assert.IsFalse(l.Locked, "2초가 지났다");
            Assert.IsFalse(l.Tick(0.1f, true, false), "다시 들 수 있다");
        }

        [Test]
        public void 주요_연출_중에는_태블릿을_들_수_없다()
        {
            TabletLimit l = new TabletLimit();
            Assert.IsFalse(l.Tick(0.1f, true, false));
            Assert.IsTrue(l.Tick(0.1f, true, true), "연출이 나오면 강제로 내린다");
            Assert.IsFalse(l.Tick(0.1f, false, true));
            for (int k = 0; k < 30; k++) Assert.IsTrue(l.Tick(0.1f, true, true), "끝날 때까지 못 든다");
            Assert.IsFalse(l.Tick(0.1f, false, false));
            Assert.IsFalse(l.Tick(2.1f, false, false));
            Assert.IsFalse(l.Tick(0.1f, true, false), "연출이 끝나고 쉬는 시간이 지나면 든다");
        }

        // ── 2단계: 몹은 세워 두고, 본 뒤에 대면 ──

        [Test]
        public void 몹이_있는_조우는_세워_두고_본_뒤에_대면한다()
        {
            DirectorFixture f = new DirectorFixture(2, 0.99, null, DirectorFixture.Slot(EncounterSlot.A, ProgramCatalog.ToiletGirl));
            f.Director.SightGated = true;
            f.Minute = 61f;
            f.Enter(SpaceId.Toilet).Wait(1f);
            Assert.IsTrue(f.HasPhase(ProgramCatalog.ToiletGirl, DirectionPhase.Present), "소녀를 세운다");
            Assert.IsFalse(f.HasPhase(ProgramCatalog.ToiletGirl, DirectionPhase.Confront), "아직 못 봤다");
            Assert.IsTrue(f.Director.Busy, "세워 둔 동안은 연출 중(태블릿 잠김)");
            Assert.AreEqual(0, f.CueCount(SignalKind.CueStarted, FinalCues.GirlStall), "대응 수칙 단서도 아직");

            Assert.IsTrue(f.Director.NotifySeen(ProgramCatalog.ToiletGirl));
            f.Wait(0.2f);
            Assert.IsTrue(f.HasPhase(ProgramCatalog.ToiletGirl, DirectionPhase.Confront), "알아본 뒤 대면");
        }

        [Test]
        public void 세운_몹을_40초_못_보면_거두고_다시_기다린다()
        {
            DirectorFixture f = new DirectorFixture(2, 0.99, null, DirectorFixture.Slot(EncounterSlot.A, ProgramCatalog.ToiletGirl));
            f.Director.SightGated = true;
            f.Minute = 61f;
            f.Enter(SpaceId.Toilet).Wait(TensionDirector.PresentMaxSeconds + 1f);
            Assert.IsTrue(f.HasPhase(ProgramCatalog.ToiletGirl, DirectionPhase.Aborted), "거둔다");
            Assert.IsFalse(f.HasPhase(ProgramCatalog.ToiletGirl, DirectionPhase.Confront));
            Assert.AreEqual(EncounterRunState.Waiting, f.Run(ProgramCatalog.ToiletGirl).State);
        }

        [Test]
        public void 몹이_없는_조우와_옛_흐름은_곧바로_대면한다()
        {
            Assert.IsFalse(EncounterScripts.Find(ProgramCatalog.Footsteps).NeedsSight);
            Assert.IsFalse(EncounterScripts.Find(ProgramCatalog.CeilingLegs).NeedsSight, "시체는 사다리를 볼 때 이미 본다");
            Assert.IsFalse(EncounterScripts.Find(ProgramCatalog.PeopleTree).NeedsSight, "존재형");
            Assert.IsTrue(EncounterScripts.Find(ProgramCatalog.BoyBang).NeedsSight);

            DirectorFixture f = new DirectorFixture(2, 0.99, null, DirectorFixture.Slot(EncounterSlot.A, ProgramCatalog.ToiletGirl));
            f.Minute = 61f;   // SightGated 꺼짐(코어 기본)
            f.Enter(SpaceId.Toilet).Wait(1f);
            Assert.IsTrue(f.HasPhase(ProgramCatalog.ToiletGirl, DirectionPhase.Confront));
        }

        // ── 2단계: 방아쇠 ──

        [Test]
        public void 노란_얼굴은_도서관_안쪽_깊이_들어와_4초()
        {
            bool deep = false;
            DirectorFixture f = new DirectorFixture(2, 0.99, null, DirectorFixture.Slot(EncounterSlot.A, ProgramCatalog.YellowFace));
            f.Director.DeepInSpace = (space, margin) => deep;
            f.Minute = 61f;
            f.Enter(SpaceId.Library).Wait(12f);
            Assert.IsFalse(f.HasPhase(ProgramCatalog.YellowFace, DirectionPhase.Foreshadow), "문간·가장자리에 오래 있어도 안 온다");
            deep = true;
            f.Wait(3.5f);
            Assert.IsFalse(f.HasPhase(ProgramCatalog.YellowFace, DirectionPhase.Foreshadow), "깊이 들어와 4초 전");
            f.Wait(1f);
            Assert.IsTrue(f.HasPhase(ProgramCatalog.YellowFace, DirectionPhase.Foreshadow), "깊이 들어와 4초");
        }

        [Test]
        public void 창밖_남자는_창에서_6미터_안에서만()
        {
            StagePoints.Set(StageAnchors.WindowMan, new Vector3(0f, 0f, 20f));
            try
            {
                DirectorFixture f = new DirectorFixture(2, 0.99, null, DirectorFixture.Slot(EncounterSlot.A, ProgramCatalog.SuitMan));
                f.Minute = 61f;
                f.Enter(SpaceId.Library).Pose(0f, 5f).Wait(10f);
                Assert.IsFalse(f.HasPhase(ProgramCatalog.SuitMan, DirectionPhase.Foreshadow), "창에서 15m — 안 온다");
                f.Pose(0f, 15f).Wait(7f);
                Assert.IsTrue(f.HasPhase(ProgramCatalog.SuitMan, DirectionPhase.Foreshadow), "창에서 5m");
            }
            finally
            {
                StagePoints.Remove(StageAnchors.WindowMan);
            }
        }

        [Test]
        public void 나가는_길_시체는_사다리_곁에_들어갔다_나올_때()
        {
            DirectorFixture f = new DirectorFixture(2, 0.99, null, DirectorFixture.Slot(EncounterSlot.A, ProgramCatalog.CeilingLegs));
            f.Director.GazeTargetExitMode = true;
            f.Director.GazeTargetPosition = id => new Vector3(0f, 0f, 0f);
            f.Minute = 61f;
            f.Enter(SpaceId.Classroom).Pose(6f, 0f).Wait(1f);
            Assert.IsFalse(f.HasPhase(ProgramCatalog.CeilingLegs, DirectionPhase.Foreshadow), "사다리 곁에 간 적 없음");
            f.Pose(1.5f, 0f).Wait(1f);
            Assert.IsFalse(f.HasPhase(ProgramCatalog.CeilingLegs, DirectionPhase.Foreshadow), "곁에 있는 동안은 아니다");
            f.Pose(3f, 0f).Wait(0.5f);
            Assert.IsFalse(f.HasPhase(ProgramCatalog.CeilingLegs, DirectionPhase.Foreshadow), "3.8m 전");
            f.Pose(4.2f, 0f).Wait(0.5f);
            Assert.IsTrue(f.HasPhase(ProgramCatalog.CeilingLegs, DirectionPhase.Foreshadow), "나오는 길 — 떨어진다");
        }
    }
}

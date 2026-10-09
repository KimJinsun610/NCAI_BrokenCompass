using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace NightDuty.Tests
{
    /// <summary>54차 — [근무 지시](3차 회의 1차 묶음 W1~W6) 지시기와 NightRun 연결.</summary>
    public sealed class DutyTests
    {
        private sealed class Rig
        {
            public readonly DutyDispatcher D;
            public readonly List<DutyEvent> Events = new List<DutyEvent>();
            public float Minute = 30f;
            public bool Busy;
            public int Backlog;
            public float Since = 100f;

            public Rig(int day, SpaceId near = SpaceId.SecurityRoom)
            {
                D = new DutyDispatcher(day, s => s == near ? 0f : 10f + (int)s, 5);
            }

            public Rig Wait(float seconds)
            {
                int n = Mathf.RoundToInt(seconds * 10f);
                for (int i = 0; i < n; i++)
                {
                    DutyEvent ev;
                    if (D.Tick(new DutyInput { Minute = Minute, Dt = 0.1f, DirectorBusy = Busy, InspectionBacklog = Backlog, SinceInspection = Since }, out ev)) Events.Add(ev);
                }

                return this;
            }

            public bool Send(JudgeSignal s)
            {
                DutyEvent ev;
                if (!D.Observe(s, out ev)) return false;
                Events.Add(ev);
                return true;
            }

            public DutyEvent Last
            {
                get { return Events[Events.Count - 1]; }
            }

            public void Gaze(string id, float seconds)
            {
                for (int i = 0; i < Mathf.RoundToInt(seconds * 10f); i++) Send(JudgeSignal.Gaze(id, 0.1f));
            }
        }

        [Test]
        public void 점검_공백이_30초_이어지면_가까운_지시가_나간다()
        {
            Rig r = new Rig(1);
            r.Since = DutyDispatcher.GapSeconds - 1f;
            r.Wait(0.5f);
            Assert.AreEqual(0, r.Events.Count, "공백 30초 전");
            r.Since = DutyDispatcher.GapSeconds + 1f;
            r.Wait(0.2f);
            Assert.AreEqual(1, r.Events.Count);
            Assert.AreEqual(DutyOutcome.Issued, r.Last.Outcome);
            Assert.AreEqual("W1", r.Last.Def.Id, "1일차 · 경비실에서 가장 가까운 CCTV 순회");
            StringAssert.StartsWith(DutyCatalog.Header, DutyCatalog.OrderText(r.Last.Def, 1));
            StringAssert.EndsWith("바랍니다.", DutyCatalog.OrderText(r.Last.Def, 1));
        }

        [Test]
        public void 조우_중_점검이_밀린_동안_판정_구간_밖에는_내지_않는다()
        {
            Rig r = new Rig(1);
            r.Busy = true;
            r.Wait(1f);
            r.Busy = false;
            r.Backlog = 1;
            r.Wait(1f);
            r.Backlog = 0;
            r.Minute = 10f;   // 출근
            r.Wait(1f);
            r.Minute = NightClock.JudgingEnd + 5f;  // 판정 끝(67차 04:25) 뒤
            r.Wait(1f);
            Assert.AreEqual(0, r.Events.Count);
        }

        [Test]
        public void W1_CCTV_다섯_채널을_2초씩_보면_완료()
        {
            Rig r = new Rig(1);
            r.Wait(0.2f);
            Assert.AreEqual("W1", r.D.Active.Id);
            for (int ch = 0; ch < 4; ch++)
            {
                for (int i = 0; i < 21; i++) r.Send(JudgeSignal.CctvView("cctv.ch" + ch, 0.1f));
            }

            Assert.IsNotNull(r.D.Active, "네 채널로는 아직");
            for (int i = 0; i < 21; i++) r.Send(JudgeSignal.CctvView("cctv.ch4", 0.1f));
            Assert.AreEqual(DutyOutcome.Done, r.Last.Outcome);
            Assert.IsNull(r.D.Active);
        }

        [Test]
        public void W2_도서관_네_지점_중_셋을_지나_나오면_완료()
        {
            // 57차(민: 「도서관 순찰이 완료가 잘 안 되는 것 같아」): 넷 중 셋, 지시를 받을 때 이미 서 있던 지점도 센다.
            Rig r = new Rig(1, SpaceId.Library);
            r.Send(JudgeSignal.Target(SignalKind.ZoneEntered, DutyCatalog.LibraryZonePrefix + 2));   // 지시 전부터 서 있던 곳
            r.Wait(0.2f);
            Assert.AreEqual("W2", r.D.Active.Id);
            Assert.AreEqual(3, DutyCatalog.LibraryZones);
            r.Send(JudgeSignal.Target(SignalKind.ZoneExited, DutyCatalog.LibraryZonePrefix + 2));
            r.Send(JudgeSignal.Target(SignalKind.ZoneEntered, DutyCatalog.LibraryZonePrefix + 1));
            Assert.IsFalse(r.Send(JudgeSignal.OfSpace(SignalKind.SpaceExited, SpaceId.Library)), "두 곳만 돌고 나오면 아직");
            r.Send(JudgeSignal.Target(SignalKind.ZoneEntered, DutyCatalog.LibraryZonePrefix + 4));
            Assert.IsTrue(r.Send(JudgeSignal.OfSpace(SignalKind.SpaceEntered, SpaceId.Corridor)));
            Assert.AreEqual(DutyOutcome.Done, r.Last.Outcome);
            Assert.AreEqual("도서관 순찰이 기록되었습니다. 00:42", DutyCatalog.DoneText(r.Last.Def, 1, 42));
        }

        [Test]
        public void W3_문간에서_천장_등을_1초_이어_봐야_완료()
        {
            Rig r = new Rig(2, SpaceId.Classroom_1_3);
            r.Wait(0.2f);
            Assert.AreEqual("W3", r.D.Active.Id);
            r.Gaze(DutyCatalog.ClassroomLightTarget, 2f);
            Assert.IsNotNull(r.D.Active, "문간 전에 본 것은 세지 않는다");
            r.Send(JudgeSignal.Target(SignalKind.ZoneEntered, DutyCatalog.ClassroomDoorZone));
            r.Gaze(DutyCatalog.ClassroomLightTarget, 0.6f);
            r.Gaze("rule.C2.boy", 0.3f);   // 소년에게 시선이 끌렸다 — 처음부터
            r.Gaze(DutyCatalog.ClassroomLightTarget, 0.6f);
            Assert.IsNotNull(r.D.Active);
            r.Gaze(DutyCatalog.ClassroomLightTarget, 0.5f);
            Assert.AreEqual(DutyOutcome.Done, r.Last.Outcome);
            Assert.AreEqual("[근무 지시] 교실 소등 확인\n1-3 교실은 소등되어 있습니다.", DutyCatalog.OrderText(r.Last.Def, 2));   // 67차 ② 민 문구
        }

        [Test]
        public void W4_플레이어가_연_문이_있을_때만_나가고_다_닫으면_완료()
        {
            Rig r = new Rig(2, SpaceId.Corridor);
            r.Send(JudgeSignal.DoorCommand("corridor.door.auto", false, ActionSource.Direction));   // 저절로 열린 문(H2)은 세지 않는다
            Assert.AreNotEqual("W4", r.D.Pick().Id);
            r.Send(JudgeSignal.DoorCommand("corridor.door.11", false, ActionSource.Player));
            Assert.AreEqual("W4", r.D.Pick().Id);
            r.Wait(0.2f);
            Assert.AreEqual("W4", r.D.Active.Id);
            Assert.IsTrue(r.Send(JudgeSignal.DoorCommand("corridor.door.11", true, ActionSource.Player)));
            Assert.AreEqual(DutyOutcome.Done, r.Last.Outcome);
        }

        [Test]
        public void W5_이완_구간이_열리면_근무일지_서명하면_완료_못하면_조용히_닫힌다()
        {
            Rig r = new Rig(3);
            r.Minute = NightClock.RelaxStart + 1f;
            r.Since = 0f;   // 공백과 무관하게
            r.Wait(0.2f);
            Assert.AreEqual("W5", r.D.Active.Id);
            DutyEvent ev;
            Assert.IsTrue(r.D.NoteSigned(out ev));
            Assert.AreEqual(DutyOutcome.Done, ev.Outcome);

            Rig late = new Rig(3);
            late.Since = 0f;   // 닫힌 뒤 다른 지시가 바로 나오지 않게
            late.Minute = NightClock.RelaxStart + 1f;
            late.Wait(0.2f);
            late.Minute = NightClock.Call2;
            late.Wait(0.2f);
            // 67차 ②(민: 「W5는 시간 제한 없음 — 시간 초과 처리도 하지 않게」): 미완료 답장·경고 없이 조용히 닫힌다.
            Assert.AreEqual(1, late.Events.Count, "지시 한 통뿐 — 미완료 사건 없음");
            Assert.IsNull(late.D.Active);
            Assert.IsTrue(late.D.Finished("W5"));
        }

        [Test]
        public void W6_복도_끝에서_유도등을_보고_경비실로_돌아오면_완료()
        {
            Rig r = new Rig(2, SpaceId.Corridor);
            r.Wait(0.2f);
            Assert.AreEqual("W6", r.D.Active.Id);
            Assert.IsFalse(r.Send(JudgeSignal.OfSpace(SignalKind.SpaceEntered, SpaceId.SecurityRoom)), "유도등 전");
            r.Send(JudgeSignal.Target(SignalKind.ZoneEntered, DutyCatalog.ExitZone));
            r.Gaze(DutyCatalog.ExitSignTarget, 1.1f);
            Assert.IsTrue(r.Send(JudgeSignal.OfSpace(SignalKind.SpaceEntered, SpaceId.SecurityRoom)));
            Assert.AreEqual(DutyOutcome.Done, r.Last.Outcome);
        }

        [Test]
        public void 기한을_넘기면_미완료이고_조우_중에도_기한은_흐른다()
        {
            // 67차 ②: 기한은 태블릿에 「HH:MM까지」로 적히므로 조우 중에도 흐른다(전에는 멈췄다).
            Rig r = new Rig(1);
            r.Wait(0.2f);
            DutyDef w1 = r.D.Active;
            Assert.AreEqual(DutyCatalog.LimitSeconds, w1.Seconds);
            r.Busy = true;
            r.Wait(w1.Seconds - 5f);
            Assert.IsNotNull(r.D.Active);
            r.Wait(6f);
            Assert.AreEqual(DutyOutcome.Missed, r.Last.Outcome);
        }

        [Test]
        public void 제한_없는_지시는_기한이_지나도_미완료가_아니다()
        {
            foreach (string id in new[] { "W2", "W4", "W5", "W15" }) Assert.AreEqual(0f, DutyCatalog.Find(id).Seconds, id);
            Rig r = new Rig(1, SpaceId.Library);
            r.Wait(0.2f);
            Assert.AreEqual(0f, r.D.Active.Seconds, r.D.Active.Id + " — 도서관 가까이(W2 또는 W15)");
            r.Wait(400f);
            Assert.IsNotNull(r.D.Active, "제한 없음");
            Assert.AreEqual(1, r.Events.Count);
            r.Minute = NightClock.JudgingEnd;
            r.Wait(0.2f);
            Assert.IsNull(r.D.Active, "판정 구간 끝에 조용히 닫힌다");
            Assert.AreEqual(1, r.Events.Count);
        }

        [Test]
        public void 하루_상한과_지시_사이_간격을_지키고_수칙_위반이면_완료해도_표시된다()
        {
            Rig r = new Rig(1, SpaceId.Library);
            r.Wait(0.2f);
            r.D.NoteViolation();
            for (int i = 1; i <= 4; i++) r.Send(JudgeSignal.Target(SignalKind.ZoneEntered, DutyCatalog.LibraryZonePrefix + i));
            r.Send(JudgeSignal.OfSpace(SignalKind.SpaceExited, SpaceId.Library));
            Assert.IsTrue(r.Last.Tainted, "지시는 수칙 위반을 면책하지 않는다");

            Assert.AreEqual(2, r.D.Allowed(r.Minute), "00:30에는 둘(67차: 호출 1 전 둘 — 점검 지시와 번갈아)");
            r.Minute = NightClock.Call1;
            r.Wait(DutyDispatcher.AfterDuty - 1f);
            Assert.AreEqual(2, r.Events.Count, "지시 사이 20초");
            r.Wait(2f);
            Assert.AreEqual(3, r.Events.Count);
            Assert.AreNotEqual("W2", r.Last.Def.Id, "한 번 한 것은 다시 내지 않는다");
            r.Wait(r.D.Active.Seconds + 1f);   // 미완료
            r.Wait(DutyDispatcher.AfterDuty + 5f);
            for (int k = 0; k < 6 && r.D.Active != null; k++)   // 67차: 1일차 상한 셋
            {
                if (r.D.Active.Seconds <= 0f) break;   // 67차 ②: 제한 없는 지시(W2·W4·W5·W15)는 기한으로 끝나지 않는다 — 무한 대기 금지
                r.Wait(r.D.Active.Seconds + 1f);
                r.Wait(DutyDispatcher.AfterDuty + 5f);
            }

            Assert.LessOrEqual(r.D.IssuedToday, DutyCatalog.DailyCap(1), "1일차 상한 셋");
            r.Minute = 200f;
            r.Wait(DutyDispatcher.AfterDuty + 5f);
            Assert.LessOrEqual(r.D.IssuedToday, DutyCatalog.DailyCap(1), "상한을 넘겨 내지 않는다");
        }

        [Test]
        public void 지시는_판정_구간에_고르게_퍼진다()
        {
            // 57차(민: 「근무 지시가 많아서 귀찮고」): 하루 상한 1~2일차 둘·3일차부터 셋, 01:00 전 하나 → 02:16 전 둘 → 그 뒤 상한까지.
            // 67차(민: 「점검과 지시 비중을 균일하게」): 상한 1·2일차 셋 · 3일차부터 넷, 호출 1 전 둘 → 호출 2 전 셋 → 그 뒤 상한까지.
            Assert.AreEqual(3, DutyCatalog.DailyCap(1));
            Assert.AreEqual(3, DutyCatalog.DailyCap(2));
            Assert.AreEqual(4, DutyCatalog.DailyCap(3));
            DutyDispatcher d = new DutyDispatcher(2, null, 5);
            Assert.AreEqual(2, d.Allowed(30f), "호출 1 전 둘");
            Assert.AreEqual(3, d.Allowed(NightClock.Call1), "호출 1~2 사이 하나 더");
            Assert.AreEqual(3, d.Allowed(NightClock.Call2), "2일차 상한 셋");
            Assert.AreEqual(DutyCatalog.DailyCap(2), d.Allowed(NightClock.JudgingEnd - 1f));
            DutyDispatcher d3 = new DutyDispatcher(3, null, 5);
            Assert.AreEqual(3, d3.Allowed(NightClock.Call2 - 1f));
            Assert.AreEqual(4, d3.Allowed(NightClock.Call2), "3일차 호출 2 뒤 넷째");
        }

        [Test]
        public void 비품_확인은_그_대상을_1초_보면_완료이고_그날_점검_대상은_내지_않는다()
        {
            Rig r = new Rig(1, SpaceId.ScienceRoom);
            r.Wait(0.2f);
            Assert.AreEqual("W8", r.D.Active.Id, "과학실 가까이 — 현미경 전원 확인");
            Assert.AreEqual(DutyKind.GazeCheck, r.D.Active.Kind);
            string target = InspectionCatalog.TargetPrefix + "S-2";
            Assert.AreEqual(target, r.D.Active.Target);
            r.Gaze(target, 0.6f);
            r.Gaze("inspect.S-1", 0.2f);   // 눈을 돌리면 처음부터
            r.Gaze(target, 0.6f);
            Assert.IsNotNull(r.D.Active);
            r.Gaze(target, 0.5f);
            Assert.AreEqual(DutyOutcome.Done, r.Last.Outcome);

            DutyDispatcher planned = new DutyDispatcher(1, s => s == SpaceId.ScienceRoom ? 0f : 10f + (int)s, 5, item => item == "S-2");
            Assert.AreNotEqual("W8", planned.Pick().Id, "그날 점검에 있는 현미경은 비품 확인으로 내지 않는다");
            foreach (DutyDef def in DutyCatalog.All)
            {
                if (def.Kind != DutyKind.GazeCheck) continue;
                Assert.IsNotNull(InspectionCatalog.Find(def.Item), def.Id);
                StringAssert.EndsWith("바랍니다.", DutyCatalog.OrderText(def, 3), def.Id);
            }
        }

        [Test]
        public void 넷째_날_도서관_순찰은_다른_근무자_변주()
        {
            DutyDef w2 = DutyCatalog.Find("W2");
            Assert.AreEqual("[근무 지시] 복도 순찰은 다른 근무자가 진행 중입니다. 도서관 순찰 바랍니다.", DutyCatalog.OrderText(w2, 4));
            Assert.AreEqual("순찰이 기록되었습니다. 근무자 2명. 01:20", DutyCatalog.DoneText(w2, 4, 80));
            foreach (DutyDef d in DutyCatalog.All)
            {
                StringAssert.DoesNotContain("확인되었습니다", DutyCatalog.DoneText(d, 1, 10), "역설 답장 전용 말");
                StringAssert.DoesNotContain("지금", DutyCatalog.OrderText(d, 5), "역설 말투 금지");
            }
        }

        // ── NightRun 연결 ─────────────────────────────────────────

        private int _clock;

        [SetUp]
        public void SetUp()
        {
            NightRun.StartNewRun();
            NightRun.JudgingWindowEnabled = false;
            NightRun.DutiesEnabled = true;
            _clock = 30;
        }

        [TearDown]
        public void TearDown()
        {
            NightRun.DutiesEnabled = false;
            NightRun.StartNewRun();
            EventBus.ClearAll();
        }

        [Test]
        public void 지시는_태블릿_문자로_가고_완료하면_축_3이_내려가고_미완료면_경고()
        {
            List<string> texts = new List<string>();
            EventBus.DutySent += m => texts.Add(m.Text);
            NightRun.BeginNight(1, () => _clock);
            Assert.IsNotNull(NightRun.Duties);
            NightRun.DebugAddAxis(FearAxis.Auditory, 20);

            Assert.IsTrue(NightRun.DebugIssueDuty("W2"));
            Assert.AreEqual("[근무 지시] 도서관 순찰 바랍니다.", texts[0]);
            for (int i = 1; i <= 4; i++) NightRun.Send(JudgeSignal.Target(SignalKind.ZoneEntered, DutyCatalog.LibraryZonePrefix + i));
            NightRun.Send(JudgeSignal.OfSpace(SignalKind.SpaceEntered, SpaceId.Corridor));
            Assert.AreEqual("도서관 순찰이 기록되었습니다. 00:30", texts[1]);
            Assert.AreEqual(20 - DutyCatalog.Relief, NightRun.Axes.GetValue(FearAxis.Auditory));

            int before = NightRun.Warnings.Count + NightRun.Warnings.PendingPunishments * Deltas.WarningsForPunishment;
            Assert.IsTrue(NightRun.DebugIssueDuty("W1"));
            NightRun.Tick(DutyCatalog.Find("W1").Seconds + 1f);
            Assert.AreEqual("CCTV 순회 기록이 없습니다.", texts[texts.Count - 1]);
            int after = NightRun.Warnings.Count + NightRun.Warnings.PendingPunishments * Deltas.WarningsForPunishment;
            Assert.AreEqual(before + 1, after, "미완료는 경고 1");
        }

        [Test]
        public void 재시작은_진행_중이던_지시와_되돌린_기록을_남기지_않는다()
        {
            // 54차 QA: 태블릿(TabletBridge)은 이것으로 되돌린 [근무 지시] 문자를 지운다.
            NightRun.BeginNight(3, () => _clock);
            Assert.IsTrue(NightRun.DebugIssueDuty("W2"));
            for (int i = 1; i <= 4; i++) NightRun.Send(JudgeSignal.Target(SignalKind.ZoneEntered, DutyCatalog.LibraryZonePrefix + i));
            NightRun.Send(JudgeSignal.OfSpace(SignalKind.SpaceEntered, SpaceId.Corridor));
            Assert.IsTrue(NightRun.DutyRecordStands("W2"), "끝난 지시");
            Assert.IsTrue(NightRun.DebugIssueDuty("W1"));
            Assert.IsFalse(NightRun.DutyRecordStands("W1"), "진행 중");

            NightRun.DebugForceCapture(FearAxis.Auditory);
            Assert.AreEqual(RestartKind.FromNightStart, NightRun.RestartAfterCapture().Kind);
            Assert.IsNull(NightRun.Duties.Active, "진행 중이던 지시는 되돌려 사라진다");
            Assert.IsFalse(NightRun.DutyRecordStands("W1"));
            Assert.IsFalse(NightRun.DutyRecordStands("W2"), "밤 시작으로 돌아가면 완료 기록도 되돌린다 — 다시 나올 수 있다");
            Assert.AreEqual(0, NightRun.Duties.IssuedToday);
        }

        [Test]
        public void 근무일지_서명은_이완_구간에_한_번이고_재시작은_02_16에서()
        {
            // 54차 QA: 경비실 근무일지(DutyLogBook)는 CanSignCheckpointNow로 [E] 안내를 띄운다.
            NightRun.JudgingWindowEnabled = true;
            try
            {
                List<string> texts = new List<string>();
                EventBus.DutySent += m => texts.Add(m.Text);
                NightRun.BeginNight(3, () => _clock);
                _clock = (int)NightClock.RelaxStart - 1;
                Assert.IsFalse(NightRun.CanSignCheckpointNow, "01:51 — 이완 전");
                Assert.IsFalse(NightRun.SignCheckpoint());

                _clock = (int)NightClock.RelaxStart + 1;
                NightRun.Tick(0.1f);   // W5 「근무일지 서명 바랍니다」
                Assert.AreEqual("W5", NightRun.Duties.Active.Id);
                Assert.IsTrue(NightRun.CanSignCheckpointNow, "01:53");
                Assert.IsTrue(NightRun.SignCheckpoint());
                Assert.IsNotNull(NightRun.Checkpoint);
                Assert.AreEqual("근무일지 서명이 기록되었습니다. " + NightClock.Clock(NightClock.RelaxStart + 1), texts[texts.Count - 1]);
                Assert.IsFalse(NightRun.CanSignCheckpointNow, "한 번만");
                Assert.IsFalse(NightRun.SignCheckpoint());

                NightRun.DebugForceCapture(FearAxis.Auditory);
                Assert.IsFalse(NightRun.CanSignCheckpointNow, "붙잡힌 동안");
                RestartResult r = NightRun.RestartAfterCapture();
                Assert.AreEqual(RestartKind.FromCheckpoint, r.Kind);
                Assert.AreEqual((int)NightClock.Call2, (int)r.StartMinute);
                Assert.IsFalse(NightRun.CanSignCheckpointNow, "재시작한 밤에도 서명은 남는다");

                _clock = (int)NightClock.Call2 + 1;
                Assert.IsFalse(NightRun.CanSignCheckpointNow, "02:17 — 이완 뒤");
            }
            finally
            {
                NightRun.JudgingWindowEnabled = false;
            }
        }

        [Test]
        public void 체크포인트_재시작은_서명_전에_끝난_지시만_남긴다()
        {
            NightRun.BeginNight(3, () => _clock);
            Assert.IsTrue(NightRun.DebugIssueDuty("W5"));
            Assert.IsTrue(NightRun.SignCheckpoint());
            Assert.IsTrue(NightRun.DutyRecordStands("W5"));
            Assert.IsTrue(NightRun.DebugIssueDuty("W6"));

            NightRun.DebugForceCapture(FearAxis.Auditory);
            Assert.AreEqual(RestartKind.FromCheckpoint, NightRun.RestartAfterCapture().Kind);
            Assert.IsTrue(NightRun.DutyRecordStands("W5"), "서명은 체크포인트에 담겼다");
            Assert.IsFalse(NightRun.DutyRecordStands("W6"), "서명 뒤에 낸 지시는 되돌린다");
            Assert.IsNull(NightRun.Duties.Active);
        }
    }
}

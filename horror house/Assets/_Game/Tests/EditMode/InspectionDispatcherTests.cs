using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace NightDuty.Tests
{
    /// <summary>긴장 조절기(50차).</summary>
    public sealed class TensionPacerTests
    {
        private static void Run(TensionPacer p, float minute, float seconds, bool guard = false, int highest = 0)
        {
            int n = Mathf.RoundToInt(seconds * 10f);
            for (int i = 0; i < n; i++) p.Tick(minute, 0.1f, guard, highest);
        }

        [Test]
        public void 사건으로_오르고_4초_뒤부터_초당_1_2씩_빠진다()
        {
            TensionPacer p = new TensionPacer(2);
            Run(p, 30f, 1f);
            p.Impulse(PacerImpulse.Confront, 2);
            Assert.AreEqual(30f, p.Intensity, 0.01f, "대면 = 10 + 10×강도");
            Run(p, 30f, 4f);
            Assert.AreEqual(30f, p.Intensity, 0.2f, "4초 유예");
            Run(p, 30f, 10f);
            Assert.AreEqual(18f, p.Intensity, 0.3f, "그 뒤 초당 1.2");
        }

        [Test]
        public void 경비실은_두_배로_식고_바닥값_아래로는_내려가지_않는다()
        {
            TensionPacer guard = new TensionPacer(2);
            Run(guard, 30f, 1f);
            guard.Impulse(PacerImpulse.Confront, 2);
            Run(guard, 30f, 14f, true);
            Assert.AreEqual(6f, guard.Intensity, 0.5f, "30 − 2.4×10");

            TensionPacer cornered = new TensionPacer(2);
            Run(cornered, 30f, 1f, false, 80);
            Assert.AreEqual(TensionPacer.FloorMax, cornered.Floor, 0.01f, "생존 수치 80 → 바닥 min(20, 25)");
            Run(cornered, 30f, 30f, false, 80);
            Assert.AreEqual(TensionPacer.FloorMax, cornered.Intensity, 0.01f);
        }

        [Test]
        public void 절정_해소_휴식_축적_순서()
        {
            TensionPacer p = new TensionPacer(1);
            Run(p, 30f, 1f);
            Assert.AreEqual(PacerState.BuildUp, p.State);

            p.Impulse(PacerImpulse.Confront, 5);   // 60
            p.Impulse(PacerImpulse.Startle);        // 75
            Run(p, 30f, 0.2f);
            Assert.AreEqual(PacerState.Peak, p.State);

            Run(p, 30f, 9f);
            Assert.AreEqual(PacerState.Fade, p.State, "70 아래");

            Run(p, 30f, 30f);
            Assert.AreEqual(PacerState.Relax, p.State, "35 아래");
            float relaxAt = p.Now - p.InState;

            Run(p, 30f, 20f);
            Assert.AreEqual(PacerState.Relax, p.State, "긴장도는 이미 낮아도 1일차 최소 휴식 40초");
            Run(p, 30f, 25f);
            Assert.AreEqual(PacerState.BuildUp, p.State);
            Assert.GreaterOrEqual(p.Now - p.InState - relaxAt, TensionPacer.RelaxMin(1) - 0.2f);
        }

        [Test]
        public void 압력이_70이면_45초_강제_휴식()
        {
            TensionPacer p = new TensionPacer(3);
            Run(p, 30f, 1f);
            for (int i = 0; i < 80; i++)
            {
                p.Impulse(PacerImpulse.Fake);   // 계속 60 근처를 유지한다
                Run(p, 30f, 1f);
                if (p.State == PacerState.Relax) break;
                if (p.Intensity > 62f) Run(p, 30f, 0f);
            }

            Assert.AreEqual(PacerState.Relax, p.State);
            Assert.AreEqual(TensionPacer.PressureAfterRelax, p.Pressure, 1.5f);
        }

        [Test]
        public void 출근은_조용하고_이완은_휴식이고_03_30_뒤는_앰비언스만()
        {
            TensionPacer p = new TensionPacer(2);
            Run(p, 5f, 1f);
            Assert.AreEqual(PacerState.Quiet, p.State);
            Run(p, NightClock.RelaxStart + 1f, 1f);
            Assert.AreEqual(PacerState.Relax, p.State);
            Run(p, NightClock.Call2 + 1f, 0.2f);
            Assert.AreEqual(PacerState.BuildUp, p.State, "이완이 끝나면 곧 축적");
            Run(p, NightClock.JudgingEnd + 1f, 1f);
            Assert.AreEqual(PacerState.AmbientOnly, p.State);
        }

        [Test]
        public void 가짜_놀람은_축적_중_긴장도_50_미만_지시_직후가_아닐_때만()
        {
            TensionPacer p = new TensionPacer(2);
            string why;
            Run(p, 5f, 1f);
            Assert.IsFalse(p.AllowsFake(out why), "출근");

            Run(p, 30f, 1f);
            Assert.IsTrue(p.AllowsFake(out why), why);

            p.Impulse(PacerImpulse.Order);
            Assert.IsFalse(p.AllowsFake(out why), "지시 직후");
            Run(p, 30f, TensionPacer.OrderQuiet(2) + 0.2f);
            Assert.IsTrue(p.AllowsFake(out why), why);

            p.Impulse(PacerImpulse.Confront, 4);   // 50
            Assert.IsFalse(p.AllowsFake(out why), "긴장도 50");
        }

        [Test]
        public void 일차_상한과_휴식_길이()
        {
            CollectionAssert.AreEqual(new[] { 3, 5, 6, 7, 8 }, new[] { TensionPacer.FakeCap(1), TensionPacer.FakeCap(2), TensionPacer.FakeCap(3), TensionPacer.FakeCap(4), TensionPacer.FakeCap(5) });
            Assert.AreEqual(40f, TensionPacer.RelaxMin(1));
            Assert.AreEqual(25f, TensionPacer.RelaxMin(5));
        }
    }

    /// <summary>점검 순차 지시기(50차).</summary>
    public sealed class InspectionDispatcherTests
    {
        private sealed class Rig
        {
            public readonly InspectionBoard Board = new InspectionBoard();
            public readonly InspectionDispatcher D;
            public readonly FearAxisSystem Axes = new FearAxisSystem();
            public readonly List<InspectionOrder> Orders = new List<InspectionOrder>();
            public float Minute = 20f;
            public bool Busy;
            public TensionPacer Pacer;
            public bool AutoReport;

            public Rig(int day, IEnumerable<KeyValuePair<EncounterSlot, SpaceId>> encounters, params InspectionAssignment[] rows)
                : this(day, string.Empty, encounters, rows)
            {
            }

            public Rig(int day, string call1ItemId, IEnumerable<KeyValuePair<EncounterSlot, SpaceId>> encounters, params InspectionAssignment[] rows)
            {
                Board.Begin(new InspectionPlan(day, rows, SpaceId.None, call1ItemId), true);
                D = new InspectionDispatcher(Board, day, encounters);
                At(SpaceId.SecurityRoom, 34f, 46f);
            }

            public Rig At(SpaceId space, float x, float z)
            {
                D.Observe(JudgeSignal.OfSpace(SignalKind.SpaceEntered, space));
                D.Observe(JudgeSignal.Pose(new Vector3(x, 0f, z), 0f));
                return this;
            }

            public Rig Wait(float seconds)
            {
                int n = Mathf.RoundToInt(seconds * 10f);
                for (int i = 0; i < n; i++)
                {
                    InspectionOrder o = D.Tick(new DispatchInput { Minute = Minute, Dt = 0.1f, DirectorBusy = Busy, Pacer = Pacer });
                    if (o == null) continue;
                    Orders.Add(o);
                    if (!AutoReport) continue;
                    foreach (string id in o.ItemIds) Report(id);
                }

                return this;
            }

            public void Report(string id)
            {
                Assert.IsTrue(Board.Report(id, false, (int)Minute, Axes, SpaceId.None).Accepted, id);
                D.NoteReport();
            }
        }

        private static InspectionAssignment Row(string id, bool late = false)
        {
            return new InspectionAssignment(InspectionCatalog.Find(id), false, Band.Band0, late);
        }

        private static KeyValuePair<EncounterSlot, SpaceId> Enc(EncounterSlot slot, SpaceId space)
        {
            return new KeyValuePair<EncounterSlot, SpaceId>(slot, space);
        }

        [Test]
        public void 첫_지시는_8초_뒤_경비실이고_지시받은_항목만_열린다()
        {
            Rig r = new Rig(2, null, Row("K-1"), Row("H-1"), Row("S-1"), Row("L-1", true));
            Assert.IsFalse(r.Board.IsOpen("K-1", 2));
            r.Minute = 2f;
            r.Wait(7.8f);
            Assert.AreEqual(0, r.Orders.Count, "8초 전");
            r.Wait(0.5f);
            Assert.AreEqual(1, r.Orders.Count);
            Assert.AreEqual(OrderKind.Opening, r.Orders[0].Kind);
            Assert.AreEqual(SpaceId.SecurityRoom, r.Orders[0].Space);
            CollectionAssert.AreEqual(new[] { "K-1" }, r.Orders[0].ItemIds);
            Assert.IsTrue(r.Board.IsOpen("K-1", 2));
            Assert.IsFalse(r.Board.IsOpen("H-1", 200), "지시 전에는 시각과 무관하게 닫혀 있다");
            Assert.AreEqual(ReportRejection.NotOpenYet, r.Board.Report("H-1", false, 30, r.Axes, SpaceId.Corridor).Rejection);
        }

        [Test]
        public void 보고하면_몇_초_뒤_가까운_공간을_다음으로()
        {
            Rig r = new Rig(2, null, Row("K-1"), Row("H-1"), Row("S-1"), Row("L-1", true));
            r.Minute = 20f;   // 53차: 시간표가 아직 허락하지 않아도 보고 뒤 바로
            r.Wait(8.2f);
            r.Report("K-1");
            r.Wait(InspectionDispatcher.NextAfterReport - 0.5f);
            Assert.AreEqual(1, r.Orders.Count, "보고 직후에는 아직");
            r.Wait(1f);
            Assert.AreEqual(2, r.Orders.Count);
            Assert.AreEqual(OrderKind.Regular, r.Orders[1].Kind);
            Assert.AreEqual(SpaceId.Corridor, r.Orders[1].Space, "경비실에서 가장 가까운 곳");
        }

        [Test]
        public void 밀린_지시가_있으면_숨_돌릴_틈을_주지_않는다()
        {
            Rig r = new Rig(2, null, Row("K-1"), Row("S-1"), Row("T-1"), Row("L-1", true));
            r.At(SpaceId.Corridor, 28f, 44f);
            r.Minute = 20f;
            r.Wait(InspectionDispatcher.LullGap - 5f);   // 57차: 잔잔함 간격 60 → 40초
            Assert.AreEqual(1, r.Orders.Count, "K-1을 보고하지 않았고 다음 공간은 20m 밖");
        }

        [Test]
        public void 조우_중_문자_직후에는_미룬다()
        {
            Rig r = new Rig(2, null, Row("K-1"), Row("H-1"), Row("S-1"));
            r.Minute = 55f;
            r.Wait(8.2f);
            r.Report("K-1");
            r.Busy = true;
            r.Wait(40f);
            Assert.AreEqual(1, r.Orders.Count, "조우 중");
            r.Busy = false;
            r.Wait(InspectionDispatcher.AfterBusy - 0.5f);
            Assert.AreEqual(1, r.Orders.Count, "조우 끝나고 10초");
            r.D.NoteMessage();
            r.Wait(InspectionDispatcher.AfterMessage - 0.5f);
            Assert.AreEqual(1, r.Orders.Count, "문자 뒤 12초");
            r.Wait(1f);
            Assert.AreEqual(2, r.Orders.Count);
        }

        [Test]
        public void 긴장이_뜨거우면_최대_20초_미룬다()
        {
            // 53차: 보고 뒤 바로 오는 지시는 긴장을 보지 않으므로, 보고하지 않은 채 「시간표보다 늦음」으로 나오는 지시로 잰다.
            Rig r = new Rig(2, null, Row("K-1"), Row("H-1"), Row("S-1"), Row("C-2"), Row("L-1"), Row("T-1"));
            r.Pacer = new TensionPacer(2);
            r.Minute = 55f;
            r.Wait(8.2f);
            Assert.AreEqual(1, r.Orders.Count);
            r.Wait(InspectionDispatcher.LullGap - 0.5f);
            for (int i = 0; i < 5; i++) r.Pacer.Impulse(PacerImpulse.Startle);
            Assert.IsTrue(r.Pacer.IsHot);
            r.Wait(InspectionDispatcher.HotHoldMax - 0.5f);
            Assert.AreEqual(1, r.Orders.Count, "뜨거운 동안");
            r.Wait(2f);
            Assert.AreEqual(2, r.Orders.Count, "20초를 넘기면 그래도 낸다");
        }

        [Test]
        public void 조우가_없는_밤은_편성의_호출_1_항목_공간을_01시까지_아낀다()
        {
            // 53차 플레이 점검: 1일차(조우 없음)에 보고 뒤 바로 다음 지시가 오자 00:15에 다 끝나고 01:00 호출이 비었다.
            Rig r = new Rig(1, "S-1", null, Row("K-1"), Row("C-1"), Row("S-1"), Row("S-2"), Row("H-3", true));
            Assert.AreEqual(SpaceId.ScienceRoom, r.D.Reserved);
            r.AutoReport = true;
            r.Minute = 20f;
            r.Wait(300f);
            Assert.IsFalse(r.Board.IsIssued("S-1"), "과학실은 호출 1 몫");
            Assert.IsTrue(r.Board.IsIssued("C-1"));

            r.Minute = NightClock.Call1;
            r.Wait(10f);
            InspectionOrder last = r.Orders[r.Orders.Count - 1];
            Assert.AreEqual(OrderKind.Call1, last.Kind);
            Assert.AreEqual(SpaceId.ScienceRoom, last.Space);
            Assert.IsFalse(r.Board.IsIssued("H-3"), "늦은 공간은 호출 2");
        }

        [Test]
        public void 슬롯_C_조우_방은_그_슬롯이_열릴_때까지_아낀다()
        {
            // 53차 플레이 점검: 보고 뒤 바로 다음 지시가 오자 B·C 조우 방이 00:10쯤 끝나 조우 시간대에 아무도 그 방에 가지 않았다.
            Rig r = new Rig(3, new[] { Enc(EncounterSlot.C, SpaceId.Library) }, Row("K-1"), Row("S-1"), Row("L-1"), Row("T-1", true));
            float from, to;
            TensionDirector.SlotWindow(EncounterSlot.C, out from, out to);
            Assert.AreEqual(from, r.D.ReleaseOf(SpaceId.Library));
            r.AutoReport = true;
            r.Minute = 20f;
            r.Wait(200f);
            Assert.IsFalse(r.Board.IsIssued("L-1"), "슬롯 C 전");
            r.Minute = 110f;   // 01:40 따라잡기도 아껴 둔 방은 건드리지 않는다
            r.Wait(60f);
            Assert.IsFalse(r.Board.IsIssued("L-1"), "따라잡기에도 남는다");
            r.Minute = from + 0.5f;
            r.Wait(10f);
            Assert.IsTrue(r.Board.IsIssued("L-1"), "슬롯 C가 열리면 바로");
        }

        [Test]
        public void 경비실_CCTV_조우_날은_첫_지시를_다른_공간으로_하고_CCTV는_슬롯까지_아낀다()
        {
            // 53차 플레이 점검: K-1을 첫 지시로 써 버려 CCTV에만 보이는 사람(슬롯 B) 시간대에 아무도 CCTV를 보지 않았다.
            Rig r = new Rig(3, new[] { Enc(EncounterSlot.B, SpaceId.SecurityRoom) }, Row("K-1"), Row("H-1"), Row("T-1", true));
            r.AutoReport = true;
            r.Minute = 2f;
            r.Wait(9f);
            Assert.AreEqual(1, r.Orders.Count);
            Assert.AreEqual(OrderKind.Opening, r.Orders[0].Kind);
            Assert.AreEqual(SpaceId.Corridor, r.Orders[0].Space, "경비실은 슬롯 B까지");
            r.Wait(120f);
            Assert.IsFalse(r.Board.IsIssued("K-1"));
            float from, to;
            TensionDirector.SlotWindow(EncounterSlot.B, out from, out to);
            r.Minute = from + 0.5f;
            r.Wait(20f);
            Assert.IsTrue(r.Board.IsIssued("K-1"), "슬롯 B가 열리면 CCTV 점검");
        }

        [Test]
        public void 먼저_열릴_공간을_모두_아끼게_되면_하나는_남겨_첫_지시를_낸다()
        {
            // 54차 4일차 실측: 화장실(호출 1 몫) · 과학실(슬롯 B) · 교실(슬롯 C)을 모두 아껴 00:00~01:00에 점검이 하나도 없었다.
            Rig r = new Rig(4, "T-1", new[] { Enc(EncounterSlot.A, SpaceId.Corridor), Enc(EncounterSlot.B, SpaceId.ScienceRoom), Enc(EncounterSlot.C, SpaceId.Classroom) },
                Row("C-3"), Row("T-3"), Row("T-1"), Row("S-1"), Row("L-2", true), Row("C-2"));
            Assert.AreEqual(SpaceId.None, r.D.Reserved, "조우 없는 호출 1 몫부터 푼다");
            Assert.Greater(r.D.ReleaseOf(SpaceId.ScienceRoom), 0f, "슬롯 B 방은 그대로 아낀다");
            r.Minute = 2f;
            r.Wait(9f);
            Assert.AreEqual(1, r.Orders.Count);
            Assert.AreEqual(SpaceId.Toilet, r.Orders[0].Space);
        }

        [Test]
        public void 보고_뒤_바로_오는_지시는_긴장으로_미루지_않는다()
        {
            Rig r = new Rig(2, null, Row("K-1"), Row("H-1"), Row("S-1"));
            r.Pacer = new TensionPacer(2);
            r.Minute = 55f;
            r.Wait(8.2f);
            r.Report("K-1");
            for (int i = 0; i < 5; i++) r.Pacer.Impulse(PacerImpulse.Startle);
            Assert.IsTrue(r.Pacer.IsHot);
            r.Wait(InspectionDispatcher.NextAfterReport + 0.5f);
            Assert.AreEqual(2, r.Orders.Count, "53차 민: 한번 점검하면 몇 초 뒤 바로 다음");
        }

        [Test]
        public void 같은_동을_먼저()
        {
            Rig r = new Rig(2, null, Row("S-1"), Row("T-1"), Row("H-1", true));
            r.At(SpaceId.Library, 8f, 48f);
            r.Wait(8.2f);
            Assert.AreEqual(SpaceId.Toilet, r.Orders[0].Space, "도서관 옆 화장실이 과학실보다 먼저");
        }

        [Test]
        public void 호출_1은_슬롯_A_조우_공간을_아껴_두었다가_낸다()
        {
            Rig r = new Rig(2, new[] { Enc(EncounterSlot.A, SpaceId.Classroom_1_3) }, Row("K-1"), Row("H-1"), Row("C-1"), Row("C-2"), Row("L-1", true));
            Assert.AreEqual(SpaceId.Classroom, r.D.Reserved);
            r.AutoReport = true;
            r.Minute = 20f;
            r.Wait(200f);
            foreach (InspectionOrder o in r.Orders) Assert.AreNotEqual(SpaceId.Classroom, o.Space, o.ToString());
            Assert.IsFalse(r.Board.IsIssued("C-1"));

            r.Minute = NightClock.Call1;
            r.Wait(0.2f);
            InspectionOrder call = r.Orders[r.Orders.Count - 1];
            Assert.AreEqual(OrderKind.Call1, call.Kind);
            Assert.AreEqual(SpaceId.Classroom, call.Space);
            CollectionAssert.AreEquivalent(new[] { "C-1", "C-2" }, call.ItemIds);
        }

        [Test]
        public void 빨리_끝내면_먼저_열릴_공간이_잇달아_오지만_늦은_공간은_호출_2까지_남는다()
        {
            Rig r = new Rig(3, null, Row("K-1"), Row("H-1"), Row("S-1"), Row("C-2"), Row("T-1", true));
            r.AutoReport = true;
            r.Minute = 20f;
            r.Wait(300f);
            Assert.AreEqual(4, r.Board.IssuedCount, "53차: 보고하면 다음 — 먼저 열릴 네 공간이 다 나왔다");
            Assert.AreEqual(4, r.Orders.Count, "한 공간씩 따로따로");

            r.Minute = NightClock.Call2 + 14f;
            r.Wait(300f);
            Assert.AreEqual(5, r.Board.IssuedCount, "02:30이면 시간표가 거의 다 허락하고 호출 2도 나갔다");
        }

        [Test]
        public void 호출_1은_아껴_둔_공간이_없고_이미_앞서_있으면_쉰다()
        {
            // 51차: 시간표 끝이 01:40이라 01:00 허용이 커졌다 — 앞선 지시 둘(S-1·S-2)을 미리 냈다고 놓고 본다.
            Rig r = new Rig(3, null, Row("H-1"), Row("H-2"), Row("H-3"), Row("S-1"), Row("S-2"), Row("L-1"), Row("T-1", true));
            r.Minute = 2f;
            r.Wait(9f);
            Assert.AreEqual(3, r.Board.IssuedCount, "첫 지시 = 복도 셋");
            r.Board.MarkIssued("S-1");
            r.Board.MarkIssued("S-2");
            Assert.GreaterOrEqual(r.Board.IssuedCount, r.D.Allowed(NightClock.Call1) + 1, "01:00 시간표보다 한 공간 앞섬");
            r.Minute = NightClock.Call1;
            r.Wait(10f);
            foreach (InspectionOrder o in r.Orders) Assert.AreNotEqual(OrderKind.Call1, o.Kind, o.ToString());
        }

        [Test]
        public void 이완_구간에는_평소_지시가_없고_호출_2에_마지막_공간()
        {
            Rig r = new Rig(2, null, Row("K-1"), Row("H-1"), Row("L-1", true), Row("L-2", true));
            r.AutoReport = true;
            r.Minute = 30f;
            r.Wait(120f);
            Assert.IsTrue(r.Board.IsIssued("H-1"));
            r.Minute = NightClock.RelaxStart + 2f;
            r.Wait(60f);
            Assert.IsFalse(r.Board.IsIssued("L-1"), "이완 구간");
            r.Minute = NightClock.Call2;
            r.Wait(0.2f);
            InspectionOrder call = r.Orders[r.Orders.Count - 1];
            Assert.AreEqual(OrderKind.Call2, call.Kind);
            Assert.AreEqual(SpaceId.Library, call.Space);
            Assert.AreEqual(2, call.ItemIds.Count);
        }

        [Test]
        public void 오전_3시까지_먼저_열릴_항목은_모두_나간다()
        {
            Rig r = new Rig(4, null, Row("K-1"), Row("H-1"), Row("S-1"), Row("T-1"), Row("L-1", true));
            r.Minute = 30f;
            r.Wait(30f);
            Assert.Less(r.Board.IssuedCount, 4);
            r.Minute = InspectionDispatcher.EarlyDeadline;
            r.Wait(InspectionDispatcher.ForcedGap + 1f);
            // 51차: 시간표 끝(01:40)이 호출 2(02:16)보다 앞이다 — 호출 2 전에 먼저 열릴 항목이 모두 나간다.
            foreach (InspectionOrder o in r.Orders) Assert.AreNotEqual(OrderKind.Call2, o.Kind, o.ToString());
            InspectionOrder last = r.Orders[r.Orders.Count - 1];
            Assert.AreEqual(OrderKind.Call1, last.Kind, "호출 1이 아직이면 호출 1이 따라잡기를 겸한다");
            Assert.AreEqual(SpaceId.None, last.Space, "여러 공간을 한 통으로");
            foreach (string id in new[] { "K-1", "H-1", "S-1", "T-1" }) Assert.IsTrue(r.Board.IsIssued(id), id);
        }

        [Test]
        public void 여유가_없으면_일찍_따라잡는다()
        {
            Rig r = new Rig(5, null, Row("K-1"), Row("H-1"), Row("S-1"), Row("S-2"), Row("T-1"), Row("T-2"), Row("L-1", true));
            r.Minute = 2f;
            r.Wait(8.2f);
            Assert.AreEqual(1, r.Orders.Count, "첫 지시");
            r.Minute = NightClock.Call1 + 1f;
            r.Wait(9f);
            Assert.AreEqual(OrderKind.Call1, r.Orders[r.Orders.Count - 1].Kind);
            r.Wait(10f);

            float tight = InspectionDispatcher.WorkDeadline - 65f;   // 67차: 마감까지 게임 65분(실시간 약 160초) — 일곱 개를 돌기에 빠듯하다
            Assert.Greater(tight, NightClock.Call2);
            r.Minute = tight;
            Assert.Less(r.D.Slack(tight, SpaceId.None), InspectionDispatcher.SlackMin);
            r.Wait(0.2f);
            Assert.AreEqual(OrderKind.Call2, r.Orders[r.Orders.Count - 1].Kind);
            int before = r.Orders.Count;
            r.Wait(InspectionDispatcher.ForcedGap - 1f);
            Assert.AreEqual(before, r.Orders.Count, "따라잡기도 직전 지시 뒤 8초는 띄운다");
            r.Wait(1.5f);
            InspectionOrder last = r.Orders[r.Orders.Count - 1];
            Assert.AreEqual(OrderKind.CatchUp, last.Kind);
            Assert.AreEqual(7, r.Board.IssuedCount);
        }

        [Test]
        public void 재입실_불가_공간은_지시하지_않는다()
        {
            Rig r = new Rig(2, null, Row("H-1"), Row("S-1"));
            r.Wait(20f);
            Assert.AreEqual(1, r.Orders.Count);
            InspectionOrder o = r.D.Tick(new DispatchInput { Minute = InspectionDispatcher.EarlyDeadline, Dt = 0.1f, Banned = SpaceId.ScienceRoom });
            Assert.IsNull(o);
            Assert.IsFalse(r.Board.IsIssued("S-1"));
        }

        [Test]
        public void 재시작하면_지시는_스냅샷으로_돌아가고_보고한_항목은_열린_채()
        {
            Rig r = new Rig(2, null, Row("K-1"), Row("H-1"), Row("S-1"));
            r.Minute = 55f;
            object board0 = r.Board.CaptureState();
            object orders0 = r.D.CaptureState();
            r.Wait(8.2f);
            r.Report("K-1");
            r.Wait(25f);
            Assert.AreEqual(2, r.Orders.Count);

            r.Board.RestoreState(board0);
            r.D.RestoreState(orders0);
            Assert.AreEqual(0, r.D.Orders.Count);
            Assert.IsTrue(r.Board.IsIssued("K-1"), "보고한 항목");
            Assert.IsFalse(r.Board.IsIssued("H-1"), "지시가 되돌려졌다");

            r.At(SpaceId.SecurityRoom, 34f, 46f);
            r.Wait(8.2f);
            Assert.AreEqual(1, r.D.Orders.Count);
            Assert.AreEqual(SpaceId.Corridor, r.D.Orders[0].Space, "K-1은 이미 보고했으니 다음 공간부터");
            Assert.AreEqual(1, r.D.Orders[0].Index);
        }

        [Test]
        public void 정산은_지시받은_항목만_센다()
        {
            Rig r = new Rig(2, null, Row("K-1"), Row("H-1"), Row("S-1"));
            r.Wait(8.2f);
            InspectionSettlement s = r.Board.Settle(r.Axes, true);
            Assert.AreEqual(1, s.Unfinished);
        }
    }

    /// <summary>NightRun에 붙은 순차 지시(50차).</summary>
    public sealed class NightRunOrderTests
    {
        private int _clock;

        [SetUp]
        public void SetUp()
        {
            NightRun.StartNewRun();
            NightRun.InspectionDripEnabled = true;
            _clock = 2;
        }

        [TearDown]
        public void TearDown()
        {
            NightRun.InspectionPlanOverride = null;
            NightRun.InspectionDripEnabled = false;
            NightRun.StartNewRun();
            EventBus.ClearAll();
        }

        private static void UsePlan(params string[] ids)
        {
            List<InspectionAssignment> rows = new List<InspectionAssignment>();
            foreach (string id in ids) rows.Add(new InspectionAssignment(InspectionCatalog.Find(id), false, Band.Band0, false));
            NightRun.InspectionPlanOverride = (day, shown) => new InspectionPlan(day, rows, SpaceId.None, string.Empty);
        }

        private static void Run(float seconds)
        {
            int n = Mathf.RoundToInt(seconds * 10f);
            for (int i = 0; i < n; i++) NightRun.Tick(0.1f);
        }

        [Test]
        public void 지시가_나가면_알리고_문자가_점검표를_대신한다()
        {
            UsePlan("K-1", "H-1");
            List<InspectionOrder> sent = new List<InspectionOrder>();
            EventBus.InspectionOrdered += sent.Add;
            NightRun.BeginNight(2, () => _clock);

            Assert.IsNotNull(NightRun.Orders);
            Assert.AreEqual(string.Empty, NightRun.ChecklistMessage, "순차 지시면 점검표 한 통은 없다");
            Assert.AreEqual(ReportRejection.NotOpenYet, NightRun.ReportInspection("K-1", false).Rejection, "지시 전");

            Run(8.3f);
            Assert.AreEqual(1, sent.Count);
            string text = NightRun.OrderMessage(sent[0]);
            StringAssert.StartsWith("[점검 지시] 경비실", text);
            StringAssert.Contains(InspectionCatalog.Find("K-1").TabletLine, text);
            StringAssert.Contains(NightRun.InspectionHowTo, text, "첫 지시에만 조작 안내");

            Assert.IsTrue(NightRun.ReportInspection("K-1", false).Accepted);
            StringAssert.Contains(" 보고함", NightRun.OrderMessage(sent[0]));   // 60차: 「· [정상] 보고함」처럼 판정이 붙는다
        }

        [Test]
        public void 지시받지_않은_물품에_가까이_가도_놀라지_않는다()
        {
            UsePlan("K-1", "H-1");
            NightRun.BeginNight(2, () => _clock);
            Assert.IsFalse(NightRun.InspectionStartle("H-1"));
        }

        [Test]
        public void 재시작하면_지시가_밤_시작으로_돌아간다()
        {
            UsePlan("K-1", "H-1", "S-1");
            NightRun.BeginNight(2, () => _clock);
            Run(8.3f);
            NightRun.ReportInspection("K-1", false);
            Assert.AreEqual(1, NightRun.Orders.Orders.Count);

            NightRun.DebugForceCapture(FearAxis.Layout);
            NightRun.RestartAfterCapture();
            Assert.AreEqual(0, NightRun.Orders.Orders.Count);
            Assert.IsTrue(NightRun.Inspections.IsIssued("K-1"));
            Assert.IsFalse(NightRun.Inspections.IsIssued("H-1"));

            Run(8.3f);
            Assert.AreEqual(1, NightRun.Orders.Orders.Count);
            Assert.AreNotEqual(SpaceId.SecurityRoom, NightRun.Orders.Orders[0].Space);
        }
    }
}

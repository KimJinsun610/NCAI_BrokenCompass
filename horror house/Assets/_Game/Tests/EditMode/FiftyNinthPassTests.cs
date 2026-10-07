using System.Collections.Generic;
using NUnit.Framework;

namespace NightDuty.Tests
{
    /// <summary>59차 — 밤 10분 · 시체 낙하 겹침 조우(앉은 소년과 함께) · 조기 퇴근 문구 · 마지막 지시 안내.</summary>
    public sealed class FiftyNinthPassTests
    {
        private sealed class Rig
        {
            public readonly TensionDirector Director;
            public readonly List<DirectionEvent> Events = new List<DirectionEvent>();
            public float Minute = 70f;

            public Rig(params string[] slotEncounters)
            {
                List<SlotEncounter> slots = new List<SlotEncounter>();
                for (int i = 0; i < slotEncounters.Length; i++) slots.Add(new SlotEncounter((EncounterSlot)i, ProgramCatalog.Encounter(slotEncounters[i]), false));
                NightProgram program = new NightProgram(2, slots, new List<RuleDef>(), "test", new[] { ProgramCatalog.Encounter(ProgramCatalog.CeilingLegs) });
                Director = new TensionDirector(program, 2, 0, new FixedRandom(0.99));
                Director.Emitted += Events.Add;
            }

            public Rig Send(JudgeSignal s)
            {
                Director.Observe(s);
                return this;
            }

            public Rig Wait(float seconds)
            {
                int n = UnityEngine.Mathf.RoundToInt(seconds * 10f);
                for (int i = 0; i < n; i++)
                {
                    Director.Tick(Minute, 0.1f, 30, Band.Band0, false);
                    JudgeSignal s;
                    while (Director.TryDequeue(out s)) { }
                }

                return this;
            }

            public bool Has(string id, DirectionPhase phase)
            {
                foreach (DirectionEvent e in Events) if (e.SourceId == id && e.Phase == phase) return true;
                return false;
            }

            public EncounterRun Run(string id)
            {
                foreach (EncounterRun r in Director.Runs) if (r.Def.Id == id) return r;
                return null;
            }

            public Rig GazeLadder(float seconds)
            {
                string ladder = InspectionCatalog.TargetPrefix + "C-3";
                int n = UnityEngine.Mathf.RoundToInt(seconds * 10f);
                for (int i = 0; i < n; i++) Send(JudgeSignal.Gaze(ladder, 0.1f)).Wait(0.1f);
                return this;
            }
        }

        [Test]
        public void 시체_낙하는_회차에_한_번_2일차나_3일차에_슬롯_밖_겹침_조우로()
        {
            // 59차 민: 「무서운 연출은 똑같은 게 반복되면 재미없다 — 2~3일차에 한 번 딱」.
            HashSet<int> days = new HashSet<int>();
            for (int seed = 0; seed < 40; seed++)
            {
                ProgramDirector d = new ProgramDirector(new System.Random(seed));
                int count = 0;
                for (int day = 1; day <= 5; day++)
                {
                    NightProgram p = d.Build(new ProgramRequest { Day = day, Shown = FixedBands.All(Band.Band2), Survival = FixedBands.All(Band.Band2) });
                    foreach (SlotEncounter s in p.Slots) Assert.AreNotEqual(ProgramCatalog.CeilingLegs, s.Encounter.Id, "슬롯을 차지하지 않는다 — " + p);
                    bool extra = false;
                    foreach (EncounterDef e in p.Extras) if (e.Id == ProgramCatalog.CeilingLegs) extra = true;
                    Assert.AreEqual(extra, p.HasEncounter(ProgramCatalog.CeilingLegs), "사다리 점검(C-3)을 더하는 근거");
                    if (!extra) continue;
                    count++;
                    days.Add(day);
                    Assert.AreEqual(d.CorpseDay, day);
                }

                Assert.AreEqual(1, count, "seed " + seed + " — 회차에 딱 한 번");
            }

            CollectionAssert.AreEquivalent(new[] { 2, 3 }, days, "2일차·3일차 둘 다 나올 수 있고 그 밖의 날은 없다");
        }

        [Test]
        public void 소년이_앉아_있어도_시체가_떨어진다()
        {
            Rig r = new Rig(ProgramCatalog.BoySeated);
            r.Send(JudgeSignal.OfSpace(SignalKind.SpaceEntered, SpaceId.Classroom_1_3)).Wait(3.5f);
            Assert.IsTrue(r.Has(ProgramCatalog.BoySeated, DirectionPhase.Confront), "소년 착석");
            Assert.AreEqual(EncounterRunState.Active, r.Run(ProgramCatalog.BoySeated).State);
            r.GazeLadder(0.6f).Wait(1.4f);
            Assert.IsTrue(r.Has(ProgramCatalog.CeilingLegs, DirectionPhase.Confront), "소년이 앉아 있는 동안에도 시체가 떨어진다");
            Assert.AreEqual(EncounterRunState.Active, r.Run(ProgramCatalog.BoySeated).State, "소년은 그대로 앉아 있다");
        }

        [Test]
        public void 시체가_떨어진_뒤에도_소년_머리_박기가_곧_온다()
        {
            Rig r = new Rig(ProgramCatalog.BoyBang);
            r.Send(JudgeSignal.OfSpace(SignalKind.SpaceEntered, SpaceId.Classroom_1_3)).Wait(0.1f);
            r.GazeLadder(0.6f);
            Assert.IsTrue(r.Has(ProgramCatalog.CeilingLegs, DirectionPhase.Foreshadow));
            r.Wait(3f);
            Assert.IsTrue(r.Has(ProgramCatalog.BoyBang, DirectionPhase.Foreshadow) || r.Has(ProgramCatalog.BoyBang, DirectionPhase.Confront),
                "시체가 떨어지는 중에도 소년 조우가 시작된다(예산·휴지에 시체를 넣지 않는다) — " + r.Run(ProgramCatalog.BoyBang));
            Assert.AreEqual(EncounterRunState.Active, r.Run(ProgramCatalog.CeilingLegs).State, "시체는 아직 대면 중");
            Assert.IsTrue(r.Director.Busy, "겹침 조우도 Busy — 가짜 놀람·수칙 단서·점검 지시는 쉰다");
        }

        [Test]
        public void 밤은_실시간_10분()
        {
            Assert.AreEqual(600f, NightClock.RealSecondsPerNight);
            Assert.AreEqual(600f, NightClock.RealSecondsAt(NightClock.ShiftEnd), 0.001f);
        }

        [Test]
        public void 조기_퇴근_문구와_마지막_지시_안내()
        {
            System.Type phone = System.Type.GetType("ShiftEndPhone, Assembly-CSharp");   // Flow는 시험 어셈블리가 참조하지 않는다
            Assert.IsNotNull(phone);
            Assert.AreEqual("지시가 아직 끝나지 않았습니다.", phone.GetField("LockedLine").GetValue(null));
            StringAssert.Contains("마지막 지시", NightRun.FinalOrderNotice);
            StringAssert.Contains("퇴근", NightRun.FinalOrderNotice);
            Assert.IsFalse(NightRun.IsFinalOrder(null));
        }
    }
}

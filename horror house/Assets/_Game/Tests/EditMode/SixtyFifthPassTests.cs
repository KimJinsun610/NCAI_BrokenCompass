using NUnit.Framework;

namespace NightDuty.Tests
{
    /// <summary>65차(2026-10-08 플레이테스트 피드백 5단계 ①) — 반납 상자 → biology 책 줍기(W15) · 쓰레기통 점검(H-4).</summary>
    public sealed class SixtyFifthPassTests
    {
        [Test]
        public void 쓰레기통은_복도_배치_켬_항목이고_문구는_민_그대로()
        {
            InspectionItem item = InspectionCatalog.Find("H-4");
            Assert.IsNotNull(item);
            Assert.AreEqual(SpaceId.Corridor, item.Space);
            Assert.AreEqual(FearAxis.Layout, item.Axis);
            Assert.AreEqual(AnomalyTemplate.Switch, item.Template, "걷어차이는 사건 — [옮김] 자리 풀이 아니다");
            Assert.AreEqual("쓰레기통이 정상인지 보고하십시오.", item.TabletLine);
            Assert.IsTrue(AnomalyLook.HasLook(item));
        }

        [Test]
        public void 반납_상자는_점검에서_빠지고_biology_책_줍기_지시가_된다()
        {
            Assert.IsNull(InspectionCatalog.Find("L-3"));
            DutyDef d = DutyCatalog.Find("W15");
            Assert.IsNotNull(d);
            Assert.AreEqual(DutyKind.Pickup, d.Kind);
            Assert.AreEqual(DutyCatalog.LibraryBookTarget, d.Target);
            Assert.AreEqual(SpaceId.Library, d.Space);
            Assert.AreEqual("[근무 지시] 도서관 안쪽 책상에 있는 biology 책을 습득하세요.", DutyCatalog.OrderText(d, 1));
            for (int day = 1; day <= 5; day++) Assert.IsTrue(d.OnDay(day), day + "일차");
        }

        [Test]
        public void 줍기_지시는_그_물건을_주우면_끝난다()
        {
            DutyDispatcher disp = new DutyDispatcher(1, null, 5);
            DutyEvent ev;
            Assert.IsFalse(disp.Wants(DutyCatalog.LibraryBookTarget), "지시 전에는 줍기를 바라지 않는다");
            Assert.IsFalse(disp.NotePicked(DutyCatalog.LibraryBookTarget, out ev));

            Assert.IsTrue(disp.Force("W15", out ev));
            Assert.AreEqual(DutyOutcome.Issued, ev.Outcome);
            Assert.IsTrue(disp.Wants(DutyCatalog.LibraryBookTarget));
            Assert.IsFalse(disp.Wants("duty.other"));

            Assert.IsFalse(disp.NotePicked("duty.other", out ev), "다른 물건은 아니다");
            Assert.IsTrue(disp.NotePicked(DutyCatalog.LibraryBookTarget, out ev));
            Assert.AreEqual(DutyOutcome.Done, ev.Outcome);
            Assert.AreEqual("W15", ev.Def.Id);
            Assert.IsTrue(disp.Finished("W15"));
            Assert.IsFalse(disp.Wants(DutyCatalog.LibraryBookTarget));
        }

        [Test]
        public void 닷새_점검은_덜_나온_공간과_항목부터_골라_세_번을_넘지_않는다()
        {
            FearAxisSystem axes = new FearAxisSystem();
            axes.BeginFrame();
            foreach (FearAxis ax in new[] { FearAxis.Auditory, FearAxis.Illuminance, FearAxis.Layout }) axes.Apply(ax, 55, "t", SpaceId.Corridor);
            axes.EndFrame();
            for (int seed = 1; seed <= 60; seed++)
            {
                AnomalyAssigner a = new AnomalyAssigner(new System.Random(seed));
                System.Collections.Generic.Dictionary<string, int> n = new System.Collections.Generic.Dictionary<string, int>();
                for (int day = 1; day <= 5; day++)
                {
                    foreach (InspectionAssignment r in a.Build(day, axes).Assignments)
                    {
                        int c;
                        n.TryGetValue(r.Id, out c);
                        n[r.Id] = c + 1;
                    }
                }

                foreach (System.Collections.Generic.KeyValuePair<string, int> kv in n)
                {
                    if (kv.Key == "K-1") continue;   // 경비실은 항목이 하나뿐
                    Assert.LessOrEqual(kv.Value, 3, "시드 " + seed + " " + kv.Key);
                }

                Assert.GreaterOrEqual(n.Count, 14, "시드 " + seed + " — 닷새 동안 16개 중 14개 이상이 나온다");
            }
        }

        [Test]
        public void 사다리는_있음_없음으로_보고하고_나머지는_정상_이상()
        {
            Assert.AreEqual("없음", InspectionCatalog.ReportWord("C-3", true));
            Assert.AreEqual("있음", InspectionCatalog.ReportWord("C-3", false));
            Assert.AreEqual("이상", InspectionCatalog.ReportWord("H-1", true));
            Assert.AreEqual("정상", InspectionCatalog.ReportWord("H-1", false));
        }

        [Test]
        public void 블라인드_문구는_어느_블라인드인지_말한다()
        {
            StringAssert.Contains("입구 맞은편", InspectionCatalog.Find("L-2").TabletLine);
        }

        [Test]
        public void 다른_지시가_진행_중이면_주워도_끝나지_않는다()
        {
            DutyDispatcher disp = new DutyDispatcher(1, null, 5);
            DutyEvent ev;
            Assert.IsTrue(disp.Force("W7", out ev));
            Assert.IsFalse(disp.NotePicked(DutyCatalog.LibraryBookTarget, out ev));
            Assert.AreEqual("W7", disp.Active.Id);
        }
    }
}

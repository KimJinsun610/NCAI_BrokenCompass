using System.Collections.Generic;
using NUnit.Framework;

namespace NightDuty.Tests
{
    /// <summary>
    /// 104차(2026-10-12) 점검 문구 정리 — 사용자: 「문구 변경은 k-1, s-5, h-2을 제외하고 개선안처럼 바꿔줘」 · 「3번 w3은 교실을 순찰해주십시오로 바꿔」 · 「T-2는 그냥 폐기해줘」.
    /// </summary>
    public sealed class OneHundredFourthInspectionTextTests
    {
        private const string Retired = "T-2";

        [Test]
        public void T2는_폐기되어_정의만_남고_공간_목록에서_빠진다()
        {
            Assert.IsTrue(InspectionCatalog.IsRetired(Retired));
            Assert.IsNotNull(InspectionCatalog.Find(Retired), "정의는 되살릴 수 있게 남긴다");
            foreach (InspectionItem item in InspectionCatalog.InSpace(SpaceId.Toilet)) Assert.AreNotEqual(Retired, item.Id);
            foreach (InspectionItem item in InspectionCatalog.All)
            {
                if (item.Id != Retired) Assert.IsFalse(InspectionCatalog.IsRetired(item.Id), item.Id + "는 폐기하지 않았다");
            }

            Assert.IsFalse(InspectionCatalog.IsRetired(null));
        }

        [Test]
        public void 배정기는_1에서_5일차_어느_시드에서도_T2를_내지_않는다()
        {
            for (int seed = 0; seed < 200; seed++)
            {
                AnomalyAssigner a = new AnomalyAssigner(new System.Random(seed));
                for (int day = 1; day <= 5; day++)
                {
                    InspectionPlan plan = a.Build(day, FixedBands.All(DayFloor.Of(day)));
                    Assert.IsNull(plan.Find(Retired), day + "일차 seed " + seed + ": " + plan);
                }
            }

            // 청각 구간이 가장 높아 [소리] T-2가 이상 후보로 가장 잘 뽑힐 조건에서도.
            for (int seed = 0; seed < 200; seed++)
            {
                AnomalyAssigner a = new AnomalyAssigner(new System.Random(seed));
                a.MarkSeen(SpaceIds.Final);
                for (int day = 2; day <= 5; day++)
                {
                    InspectionPlan plan = a.Build(day, new FixedBands(Band.Band4, Band.Band1, Band.Band1));
                    Assert.IsNull(plan.Find(Retired), day + "일차 seed " + seed + ": " + plan);
                }
            }
        }

        [Test]
        public void 조우에_맞춘_편성_보충도_T2를_더하지_않는다()
        {
            for (int seed = 0; seed < 100; seed++)
            {
                ProgramDirector d = new ProgramDirector(new System.Random(seed));
                AnomalyAssigner a = new AnomalyAssigner(new System.Random(seed + 7));
                for (int day = 1; day <= 5; day++)
                {
                    IFearAxisReader shown = FixedBands.All(DayFloor.Of(day));
                    InspectionPlan plan = a.Build(day, shown);
                    NightProgram program = d.Build(new ProgramRequest { Day = day, Shown = shown, Survival = FixedBands.All(Band.Band1), Inspections = plan });
                    InspectionPlan patched = NightRun.PatchPlanForProgram(plan, program);
                    Assert.IsNull(patched.Find(Retired), day + "일차 seed " + seed + ": " + patched + " / " + program.Report);
                }
            }
        }

        [Test]
        public void 같던_이름_넷은_공간을_붙여_서로_다르다()
        {
            Assert.AreEqual("복도 쓰레기통", InspectionCatalog.Find("H-4").Name);
            Assert.AreEqual("도서관 쓰레기통", InspectionCatalog.Find("L-5").Name);
            Assert.AreEqual("교실 화분", InspectionCatalog.Find("C-1").Name);
            Assert.AreEqual("경비실 화분", InspectionCatalog.Find("K-3").Name);
            Assert.AreEqual("도서관 의자", InspectionCatalog.Find("L-1").Name);
            Assert.AreEqual("세계 지도", InspectionCatalog.Find("S-6").Name);

            HashSet<string> names = new HashSet<string>();
            foreach (InspectionItem item in InspectionCatalog.All) Assert.IsTrue(names.Add(item.Name), "이름 중복: " + item.Id + " " + item.Name);
        }

        [Test]
        public void 그대로_두기로_한_문구는_바뀌지_않았다()
        {
            Assert.AreEqual("CCTV 모든 채널이 정상인지 확인해주십시오.", InspectionCatalog.Find("K-1").TabletLine);
            Assert.AreEqual("과학실 선반의 플라스크의 상태를 보고해주십시오.", InspectionCatalog.Find("S-5").TabletLine);
            Assert.AreEqual("과학실 쪽 복도 식수대의 상태를 보고해주십시오.", InspectionCatalog.Find("H-2").TabletLine);
        }

        [Test]
        public void 목격_지시의_변기_줄에_칸을_두_번_붙이지_않는다()
        {
            InspectionAssignment t1 = new InspectionAssignment(InspectionCatalog.Find(InspectionCatalog.ReverseReportItem), false, Band.Band0, false);
            InspectionOrder witness = new InspectionOrder(4, OrderKind.Witness, 200, new List<string> { "T-1" }, SpaceId.Toilet);
            string line = NightRun.OrderLine(witness, t1);
            Assert.AreEqual("화장실 3번 칸 변기의 상태를 보고해주십시오.", line);
            Assert.AreEqual(line.IndexOf(NightRun.WitnessStallLabel), line.LastIndexOf(NightRun.WitnessStallLabel), "「3번 칸」은 한 번만");
        }

        [Test]
        public void W3은_교실_순찰이고_답장도_W2와_같은_꼴이다()
        {
            DutyDef w3 = DutyCatalog.Find("W3");
            Assert.IsNotNull(w3);
            Assert.AreEqual("교실을 순찰해주십시오.", w3.Order);
            Assert.AreEqual("교실을 순찰해주십시오.", DutyCatalog.OrderText(w3, 2));
            Assert.AreEqual("교실 순찰이 기록되었습니다.", w3.Done);
            Assert.AreEqual("교실 순찰 기록이 없습니다.", DutyCatalog.MissedText(w3));
            StringAssert.DoesNotContain("확인해주십시오", w3.Order, "확인·보고 지시는 점검 지시만 쓴다");

            DutyDef w2 = DutyCatalog.Find("W2");
            Assert.AreEqual("도서관을 순찰해주십시오.", w2.Order);
            Assert.AreEqual("도서관 순찰이 기록되었습니다.", w2.Done);
            Assert.AreEqual("도서관 순찰 기록이 없습니다.", w2.Missed);
        }
    }
}

using System.Collections.Generic;
using NUnit.Framework;

namespace NightDuty.Tests
{
    /// <summary>66차(민: 「점검은 안 겹칠수록 좋아. 점검 항목을 추가해도 돼, 필요하면 하루 점검을 줄여도 돼」).</summary>
    public sealed class SixtySixthPassTests
    {
        [TestCase("H-5", SpaceId.Corridor, AnomalyTemplate.Sound)]
        [TestCase("H-6", SpaceId.Corridor, AnomalyTemplate.Move)]
        [TestCase("C-4", SpaceId.Classroom, AnomalyTemplate.Move)]
        [TestCase("C-5", SpaceId.Classroom, AnomalyTemplate.Move)]
        [TestCase("S-4", SpaceId.ScienceRoom, AnomalyTemplate.Switch)]
        [TestCase("S-5", SpaceId.ScienceRoom, AnomalyTemplate.Light)]
        [TestCase("S-6", SpaceId.ScienceRoom, AnomalyTemplate.Move)]
        [TestCase("T-4", SpaceId.Toilet, AnomalyTemplate.Switch)]
        [TestCase("T-5", SpaceId.Toilet, AnomalyTemplate.Move)]
        [TestCase("L-4", SpaceId.Library, AnomalyTemplate.Switch)]
        [TestCase("L-5", SpaceId.Library, AnomalyTemplate.Move)]
        [TestCase("K-2", SpaceId.SecurityRoom, AnomalyTemplate.Move)]
        [TestCase("K-3", SpaceId.SecurityRoom, AnomalyTemplate.Move)]
        public void 새_점검_항목(string id, SpaceId space, AnomalyTemplate template)
        {
            InspectionItem item = InspectionCatalog.Find(id);
            Assert.IsNotNull(item, id);
            Assert.AreEqual(space, item.Space);
            Assert.AreEqual(template, item.Template);
            Assert.AreEqual("정상", InspectionCatalog.ReportWord(id, false));
        }

        [Test]
        public void 빠진_ID는_되살리지_않는다()
        {
            Assert.IsNull(InspectionCatalog.Find("L-3"), "65차에 뺀 반납 상자");
            Assert.AreEqual("쓰레기통", InspectionCatalog.Find("H-4").Name, "49차에 뺀 복도 사물함이 아니다");
        }

        [Test]
        public void 도서관_쓰레기통은_원래_쓰러져_있다()
        {
            StringAssert.Contains("쓰러져", InspectionCatalog.Find("L-5").TabletLine, "씬의 도서관 쓰레기통은 누워 있다 — 이상이면 누가 세워 놓았다");
            StringAssert.Contains("벽에 걸려", InspectionCatalog.Find("C-4").TabletLine, "둥근 시계는 돌려도 티가 안 나 — 이상이면 바닥에 떨어져 있다");
        }

        [Test]
        public void 조도가_오르면_천장_등이_미세하게_붉어진다()
        {
            Assert.AreEqual(0f, IlluminanceTint.Amount(Band.Band0));
            float prev = 0f;
            for (int b = 1; b <= 4; b++)
            {
                float k = IlluminanceTint.Amount((Band)b);
                Assert.Greater(k, prev, "구간이 오를수록 더 붉다");
                prev = k;
            }

            Assert.LessOrEqual(prev, 0.4f, "「미세하게」 — 4구간도 절반 아래");
            Assert.Greater(IlluminanceTint.R, IlluminanceTint.G);
        }

        [Test]
        public void C4_수칙_불빛은_초록()
        {
            Assert.AreEqual("초록 불빛 아래에서는 손전등을 끄십시오.", ProgramCatalog.Rule("C4").Text);
            StringAssert.Contains("초록", ParadoxCatalog.Find("C4").Message);
            StringAssert.DoesNotContain("붉은", ParadoxCatalog.Find("C4").Message);
        }

        [Test]
        public void 손전등과_배터리는_1일차에만_책상_위에_있다()
        {
            DeskKit day1 = new DeskKit(1);
            Assert.IsTrue(day1.FlashlightOnDesk);
            Assert.IsTrue(day1.BatteryOnDesk);
            DeskKit day2 = new DeskKit(2);
            Assert.IsFalse(day2.FlashlightOnDesk, "2일차부터는 손전등을 든 채 시작");
            Assert.IsFalse(day2.BatteryOnDesk);

            object before = day1.CaptureState();
            Assert.IsTrue(day1.TakeFlashlight());
            Assert.IsFalse(day1.TakeFlashlight(), "한 번만");
            Assert.IsTrue(day1.TakeBattery());
            day1.RestoreState(before);
            Assert.IsTrue(day1.FlashlightOnDesk, "줍기 전 체크포인트로 돌아가면 책상에 다시");
            Assert.IsTrue(day1.BatteryOnDesk);
        }

        [Test]
        public void 처음_손전등은_다_닳은_채로_예비로_갈아야_켜진다()
        {
            FlashlightBattery b = new FlashlightBattery();
            b.StartEmpty();
            Assert.IsTrue(b.IsEmpty, "민: 「처음 손전등은 배터리 수치가 0이게」");
            Assert.IsFalse(b.Swap(), "예비가 없으면 못 간다");
            Assert.IsTrue(b.TryPocket(), "책상 배터리");
            Assert.IsTrue(b.Swap());
            Assert.AreEqual(1f, b.Charge);
        }

        [Test]
        public void 닷새_점검표에서_같은_항목은_두_번까지()
        {
            FearAxisSystem axes = new FearAxisSystem();
            axes.BeginFrame();
            foreach (FearAxis ax in new[] { FearAxis.Auditory, FearAxis.Illuminance, FearAxis.Layout }) axes.Apply(ax, 55, "t", SpaceId.Corridor);
            axes.EndFrame();
            for (int seed = 1; seed <= 80; seed++)
            {
                AnomalyAssigner a = new AnomalyAssigner(new System.Random(seed));
                Dictionary<string, int> n = new Dictionary<string, int>();
                for (int day = 1; day <= 5; day++)
                {
                    foreach (InspectionAssignment r in a.Build(day, axes).Assignments)
                    {
                        int c;
                        n.TryGetValue(r.Id, out c);
                        n[r.Id] = c + 1;
                    }
                }

                int repeats = 0;
                foreach (KeyValuePair<string, int> kv in n)
                {
                    if (kv.Key == InspectionCatalog.FirstInspection) continue;
                    Assert.LessOrEqual(kv.Value, 2, "시드 " + seed + " " + kv.Key);
                    repeats += kv.Value - 1;
                }

                Assert.LessOrEqual(repeats, 3, "시드 " + seed + " — 31칸에 항목 29개(실측 평균 2)");
                Assert.GreaterOrEqual(n.Count, 27, "시드 " + seed);
            }
        }
    }
}

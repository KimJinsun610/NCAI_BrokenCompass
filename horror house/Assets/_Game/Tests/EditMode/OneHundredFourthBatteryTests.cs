using System.Collections.Generic;
using NUnit.Framework;

namespace NightDuty.Tests
{
    /// <summary>
    /// 104차 배터리(사용자: 「1일~5일 배터리 배치 갯수를 4 4 4 3 3으로 · 도서관으로 배터리가 쏠리는 현상은 개선」).
    /// 공간을 먼저 고르고 그 안의 칸을 고른다 — 도서관은 몫 절반 · 한 밤 1개까지 · 1일차 0.
    /// </summary>
    public sealed class OneHundredFourthBatteryTests
    {
        private static List<BatteryCache> Rooms(float libraryWeight)
        {
            List<BatteryCache> list = new List<BatteryCache>();
            for (int i = 0; i < 6; i++)
            {
                list.Add(new BatteryCache("Interior/Corridors/LockerA (" + i + ")", 1.8f, false, false, SpaceId.Corridor));
                list.Add(new BatteryCache("Interior/Classroom02/Bookcase (" + i + ")#open", 2.0f, false, true, SpaceId.Classroom_1_3));
                list.Add(new BatteryCache("Interior/science classroom/TeacherTable (" + i + ")", 2.1f, false, true, SpaceId.ScienceRoom));
                list.Add(new BatteryCache("Interior/Library/LibraryTable (" + i + ")#open", libraryWeight, false, false, SpaceId.Library));
            }

            return list;
        }

        private static int CountIn(BatteryPlan plan, string part)
        {
            int n = 0;
            for (int i = 0; i < plan.Placed.Count; i++)
            {
                if (plan.Placed[i].Contains(part)) n++;
            }

            return n;
        }

        [Test]
        public void 놓는_수는_4_4_4_3_3()
        {
            int[] want = { 4, 4, 4, 3, 3 };
            for (int day = 1; day <= 5; day++) Assert.AreEqual(want[day - 1], BatteryRules.PlacedOn(day), day + "일차");
        }

        [Test]
        public void 도서관은_칸_무게가_커도_한_밤_하나까지이고_몫이_작다()
        {
            int library = 0;
            int total = 0;
            for (int seed = 0; seed < 300; seed++)
            {
                BatteryPlan plan = new BatteryPlan(2, null, new System.Random(seed));
                plan.Fill(Rooms(3.0f));
                Assert.AreEqual(BatteryRules.PlacedOn(2), plan.Placed.Count);
                int here = CountIn(plan, "/Library/");
                Assert.LessOrEqual(here, BatteryRules.MaxPerSpace(SpaceId.Library), "씨앗 " + seed);
                library += here;
                total += plan.Placed.Count;
            }

            Assert.Less((float)library / total, 0.22f, "도서관 몫 — 공간 넷 중 절반 몫(약 17%)");
        }

        [Test]
        public void 한_공간에_몰리지_않는다()
        {
            for (int seed = 0; seed < 300; seed++)
            {
                BatteryPlan plan = new BatteryPlan(3, null, new System.Random(seed));
                plan.Fill(Rooms(3.0f));
                foreach (string part in new[] { "/Corridors/", "/Classroom02/", "/science classroom/" }) Assert.LessOrEqual(CountIn(plan, part), 2, part + " 씨앗 " + seed);
            }
        }

        [Test]
        public void 일차_1은_도서관에_두지_않는다()
        {
            for (int seed = 0; seed < 100; seed++)
            {
                BatteryPlan plan = new BatteryPlan(1, null, new System.Random(seed));
                plan.Fill(Rooms(3.0f));
                Assert.AreEqual(4, plan.Placed.Count);
                Assert.AreEqual(0, CountIn(plan, "/Library/"), "씨앗 " + seed);
            }
        }

        [Test]
        public void 공간이_하나뿐이면_상한을_넘겨서라도_수를_채운다()
        {
            List<BatteryCache> caches = new List<BatteryCache>();
            for (int i = 0; i < 6; i++) caches.Add(new BatteryCache("Interior/Library/LibraryTable (" + i + ")#open", 2f, false, false, SpaceId.Library));
            BatteryPlan plan = new BatteryPlan(2, null, new System.Random(1));
            plan.Fill(caches);
            Assert.AreEqual(BatteryRules.PlacedOn(2), plan.Placed.Count, "몫이 0이 되면 공간 없이 칸 무게로 뽑는다");
        }
    }
}

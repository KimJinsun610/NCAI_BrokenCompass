using System.Collections.Generic;
using InstructionDT;
using NUnit.Framework;

namespace NightDuty.Tests
{
    /// <summary>3단계 Rule·Day_Rule: 코드 카탈로그가 Rule_Table에서 오는지, 날짜 편성이 Day_Rule 표와 어긋나지 않는지(대조만 — 편성은 코드 알고리즘).</summary>
    public class DesignDataRuleTests
    {
        bool _fixed;

        [SetUp]
        public void SetUp()
        {
            _fixed = ProgramDirector.FixedSchedule;
            ProgramDirector.FixedSchedule = true;   // 실제 게임 편성(날짜표)
        }

        [TearDown]
        public void TearDown()
        {
            ProgramDirector.FixedSchedule = _fixed;
        }

        static Dictionary<int, List<DayRuleRow>> DayRows()
        {
            DesignData.Ensure();
            var byDay = new Dictionary<int, List<DayRuleRow>>();
            foreach (var r in DT.DayRule.All)
            {
                int day = (int)DT.Day.Get(r.Day_ID).Day_No;
                if (!byDay.ContainsKey(day)) byDay[day] = new List<DayRuleRow>();
                byDay[day].Add(r);
            }

            return byDay;
        }

        [Test]
        public void 수칙_카탈로그는_표의_판정_수칙과_같다()
        {
            DesignData.Ensure();
            int n = 0;
            foreach (var r in DT.Rule.All)
            {
                if (r.Kind_ID == 520205) continue;   // 장식 문구는 가짜 수칙(FakeRuleCatalog)
                RuleDef def = ProgramCatalog.Rule(r.ID.ToString());
                Assert.IsNotNull(def, "Rule " + r.Code);
                Assert.AreEqual(r.Text, def.Text, r.Code);
                n++;
            }

            Assert.AreEqual(n, ProgramCatalog.AllRules.Count);
            Assert.AreEqual(5, FakeRuleCatalog.Lines.Length);
        }

        [Test]
        public void 날짜_편성은_Day_Rule_표를_벗어나지_않는다()
        {
            var byDay = DayRows();
            var miss = new SortedDictionary<string, int>();
            var extra = new SortedDictionary<string, int>();
            for (int seed = 1; seed <= 40; seed++)
            for (int b = 0; b <= 3; b++)
            {
                var d = new ProgramDirector(new System.Random(seed));
                for (int day = 1; day <= 5; day++)
                {
                    var prog = d.Build(new ProgramRequest { Day = day, Shown = FixedBands.All((Band)b), Survival = FixedBands.All(Band.Band0) });
                    var allowed = new HashSet<string>();
                    foreach (var r in byDay[day])
                    {
                        allowed.Add(r.Rule_ID.ToString());
                        if ((r.Slot_Type_ID == 570101 || r.Slot_Type_ID == 570102) && !prog.Has(r.Rule_ID.ToString()))
                        {
                            string k = "D" + day + " " + DesignData.Label(r.Rule_ID) + " slot" + r.Slot_Type_ID % 10 + " band" + b;
                            miss[k] = miss.TryGetValue(k, out var c) ? c + 1 : 1;
                        }
                    }

                    foreach (var rd in prog.Deck)
                        if (!allowed.Contains(rd.Id))
                        {
                            string k = "D" + day + " " + DesignData.Label(uint.Parse(rd.Id)) + " band" + b;
                            extra[k] = extra.TryGetValue(k, out var c) ? c + 1 : 1;
                        }
                }
            }

            // 고정(570101)·묶임(570102) 칸은 늘 덱에 있고, 덱의 수칙은 모두 그날 표의 행(후보 570103 포함)이다. 시드 40 × 띠 4.
            Assert.IsEmpty(miss, "Day_Rule 고정·묶임 수칙이 빠짐: " + string.Join(", ", miss.Keys));
            Assert.IsEmpty(extra, "Day_Rule에 없는 수칙이 편성됨: " + string.Join(", ", extra.Keys));
        }
    }
}

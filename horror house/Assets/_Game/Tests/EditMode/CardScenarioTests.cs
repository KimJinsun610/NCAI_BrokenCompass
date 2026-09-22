using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;

namespace NightDuty.Tests
{
    /// <summary>
    /// 판정 디버그 패널의 「지키기 / 어기기」 시나리오가 실제 카드 에셋에서 기대대로 정산되는지 확인한다.
    /// 패널 버튼이 거짓말을 하지 않게 하는 안전망이다.
    /// </summary>
    public sealed class CardScenarioTests
    {
        private static IEnumerable<string> Ids()
        {
            IReadOnlyList<CardScenario> all = CardScenarios.All;
            for (int i = 0; i < all.Count; i++)
            {
                yield return all[i].CardId;
            }
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.ClearAll();
        }

        [Test]
        public void 카드24장_모두_시나리오가_있다()
        {
            string[] expected =
            {
                "H1", "H2", "H3", "H4", "H5", "H6", "C1", "C2", "C3", "C4", "C5", "C6",
                "S1", "S2", "S3", "S4", "S5", "S6", "T1", "T2", "T3", "T4", "T5", "T6"
            };
            List<string> actual = new List<string>(Ids());
            CollectionAssert.AreEqual(expected, actual);
        }

        [TestCaseSource(nameof(Ids))]
        public void 지키기는_준수로_정산된다(string id)
        {
            CardScenario s = CardScenarios.Find(id);
            RuleBook book = Run(s, true, out FearAxisSystem axes);

            Assert.AreEqual(CardState.Complied, book.Watchers[0].State, id + " 지키기");
            Assert.AreEqual(book.Watchers[0].Card.SuccessDelta, axes.GetValue(FearAxis.Trust), id + " 신뢰");
        }

        [TestCaseSource(nameof(Ids))]
        public void 어기기는_기대상태로_정산된다(string id)
        {
            CardScenario s = CardScenarios.Find(id);
            RuleBook book = Run(s, false, out FearAxisSystem axes);

            Assert.AreEqual(s.ViolateExpect, book.Watchers[0].State, id + " 어기기");
            Assert.AreEqual(0, axes.GetValue(FearAxis.Trust), id + " 신뢰는 오르지 않음");
            if (s.ViolateExpect == CardState.Violated)
            {
                RuleSO card = book.Watchers[0].Card;
                // 2026-09-21 재설계: 위반 축에는 시나리오 셋업뿐 아니라 자격 선행값도 얹혀 있다.
                int before = Baseline(s, card.FailureAxis);
                int expected = System.Math.Min(Bands.Max, before + card.FailureDelta);
                Assert.AreEqual(expected, axes.GetValue(card.FailureAxis), id + " 위반 축");
            }
        }

        /// <summary>
        /// 카드를 시작시키기 전에 올려 두어야 하는 선행 축 값 한 줄.
        /// <see cref="Value"/>가 0이면 선행 조건이 없다.
        /// </summary>
        private struct AxisSetup
        {
            public readonly FearAxis Axis;
            public readonly int Value;

            public AxisSetup(FearAxis axis, int value)
            {
                Axis = axis;
                Value = value;
            }
        }

        /// <summary>
        /// 2026-09-21 재설계로 <b>발동 자격이 새로 걸린 카드</b>의 선행 축 값 표.
        /// 시나리오를 돌리기 전에 이 축을 이 값까지 올려 두지 않으면 카드가 아예 시작하지 않아
        /// 지키기·어기기 양쪽 모두 미판정(Undetermined)으로 끝난다.
        /// 표에 없는 20장은 선행 값이 0이어도 자격을 통과한다.
        /// </summary>
        private static readonly Dictionary<string, AxisSetup> Prerequisites = new Dictionary<string, AxisSetup>
        {
            { "C2", new AxisSetup(FearAxis.Layout, 24) },   // 배치 Band1~Band4
            { "S5", new AxisSetup(FearAxis.Layout, 24) },   // 배치 Band1~Band4
            { "T3", new AxisSetup(FearAxis.Layout, 48) },   // 배치 Band2~Band4
            { "T6", new AxisSetup(FearAxis.Layout, 72) }    // 배치 Band3~Band4
        };

        /// <summary>카드 ID의 선행 축 값. 표에 없으면 값 0(선행 조건 없음)을 돌려준다.</summary>
        private static AxisSetup Prerequisite(string cardId)
        {
            AxisSetup setup;
            if (Prerequisites.TryGetValue(cardId, out setup))
            {
                return setup;
            }

            return new AxisSetup(FearAxis.Layout, 0);
        }

        /// <summary>시나리오가 시작되기 전에 그 축에 쌓여 있는 값(시나리오 셋업 + 자격 선행값).</summary>
        private static int Baseline(CardScenario s, FearAxis axis)
        {
            int value = 0;
            if (s.SetupValue > 0 && s.SetupAxis == axis)
            {
                value += s.SetupValue;
            }

            AxisSetup pre = Prerequisite(s.CardId);
            if (pre.Value > 0 && pre.Axis == axis)
            {
                value += pre.Value;
            }

            return value;
        }

        private static RuleBook Run(CardScenario s, bool comply, out FearAxisSystem axes)
        {
            RuleSO card = Load(s.CardId);
            axes = new FearAxisSystem();
            if (s.SetupValue > 0)
            {
                axes.Apply(s.SetupAxis, s.SetupValue, "setup", SpaceId.None);
            }

            // 2026-09-21 재설계: 자격이 새로 걸린 카드(C2·S5·T3·T6)는 축을 먼저 올려야 시작한다.
            AxisSetup prerequisite = Prerequisite(s.CardId);
            if (prerequisite.Value > 0)
            {
                axes.Apply(prerequisite.Axis, prerequisite.Value, "eligible", SpaceId.None);
            }

            RuleBook book = new RuleBook(new[] { card }, axes, null, null);
            book.BeginNight();
            s.Play(comply, sig => book.Dispatch(sig), (sec, gaze) => TestKit.Advance(book, sec, gaze), book.EndNight);
            return book;
        }

        private static RuleSO Load(string id)
        {
            string folder;
            switch (id[0])
            {
                case 'H': folder = "Corridor"; break;
                case 'C': folder = "Classroom"; break;
                case 'S': folder = "Science"; break;
                default: folder = "Toilet"; break;
            }

            string path = "Assets/_Game/ScriptableObjects/Rules/" + folder + "/" + id + ".asset";
            RuleSO card = AssetDatabase.LoadAssetAtPath<RuleSO>(path);
            Assert.IsNotNull(card, path + " 없음. NightDuty ▸ 모든 공간 카드 에셋 생성을 실행하세요.");
            return card;
        }
    }
}

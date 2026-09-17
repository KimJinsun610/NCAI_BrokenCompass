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
                int before = card.FailureAxis == s.SetupAxis ? s.SetupValue : 0;
                int expected = System.Math.Min(Bands.Max, before + card.FailureDelta);
                Assert.AreEqual(expected, axes.GetValue(card.FailureAxis), id + " 위반 축");
            }
        }

        private static RuleBook Run(CardScenario s, bool comply, out FearAxisSystem axes)
        {
            RuleSO card = Load(s.CardId);
            axes = new FearAxisSystem();
            if (s.SetupValue > 0)
            {
                axes.Apply(s.SetupAxis, s.SetupValue, "setup", SpaceId.None);
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

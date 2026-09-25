using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace NightDuty.Tests
{
    /// <summary>
    /// 역설 데이터(2026-09-19 기획서 12절): 수칙 24장 중 23장에 역설이 1:1로 붙는다. S1만 없다.
    /// 따를 때 델타는 짝 수칙의 위반 델타를 그대로 쓰므로 문자 쪽에 별도 델타를 두지 않는다.
    /// </summary>
    public sealed class ParadoxDataTests
    {
        private static List<RuleSO> LoadCards()
        {
            List<RuleSO> cards = new List<RuleSO>();
            foreach (string guid in AssetDatabase.FindAssets("t:RuleSO"))
            {
                RuleSO card = AssetDatabase.LoadAssetAtPath<RuleSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (card != null)
                {
                    cards.Add(card);
                }
            }

            return cards;
        }

        [Test]
        public void 카드24장중_23장에_역설이_붙는다_S1만없다()
        {
            List<RuleSO> cards = LoadCards();
            Assert.AreEqual(24, cards.Count, "수칙 카드는 24장이다");

            int withParadox = 0;
            foreach (RuleSO card in cards)
            {
                if (card.CardId == "S1")
                {
                    Assert.IsFalse(card.HasParadox, "S1은 1일차 고정 카드라 역설이 없다");
                    continue;
                }

                Assert.IsTrue(card.HasParadox, card.CardId + "에 역설 문자가 없다");
                withParadox++;
            }

            Assert.AreEqual(23, withParadox);
        }

        [Test]
        public void 역설ID는_P1부터_P23까지_겹치지않는다()
        {
            HashSet<string> ids = new HashSet<string>();
            foreach (RuleSO card in LoadCards())
            {
                if (!card.HasParadox)
                {
                    continue;
                }

                Assert.IsTrue(ids.Add(card.ParadoxId), card.ParadoxId + "가 두 장에 붙어 있다");
            }

            for (int i = 1; i <= 23; i++)
            {
                Assert.IsTrue(ids.Contains("P" + i), "P" + i + "가 없다");
            }
        }

        [Test]
        public void 역설을_따르면_짝수칙의_위반델타가_적용된다()
        {
            // 문자 쪽에 별도 델타 필드를 두지 않는다는 규약을 카드 데이터로 확인한다.
            // 따라서 역설이 붙은 카드는 모두 위반 축·델타가 살아 있어야 한다(준수 전용 카드에는 역설을 붙이지 않는다).
            foreach (RuleSO card in LoadCards())
            {
                if (!card.HasParadox)
                {
                    continue;
                }

                Assert.AreNotEqual(FearAxis.Trust, card.FailureAxis, card.CardId + ": 위반 축이 신뢰다");
                Assert.Greater(card.FailureDelta, 0, card.CardId + ": 위반 델타가 없다");
                Assert.Greater(card.SuccessDelta, 0, card.CardId + ": 거절 시 오를 신뢰 델타가 없다");
            }
        }
    }
}

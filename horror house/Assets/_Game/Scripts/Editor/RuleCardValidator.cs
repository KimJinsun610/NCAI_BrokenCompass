using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace NightDuty.Editor
{
    /// <summary>
    /// 프로젝트의 모든 근무수칙 카드(RuleSO)를 검사한다.
    /// 데이터 규칙 위반과 카드 ID 중복을 콘솔에 보고한다. 씬 대상 참조 검사는 씬 등록부가 생긴 뒤 추가한다.
    /// </summary>
    public static class RuleCardValidator
    {
        /// <summary>메뉴: 모든 카드 검사.</summary>
        [MenuItem("NightDuty/근무수칙 카드 검사")]
        public static void ValidateAll()
        {
            List<RuleSO> cards = LoadAll();
            List<string> errors = new List<string>();
            Dictionary<string, string> seen = new Dictionary<string, string>();

            for (int i = 0; i < cards.Count; i++)
            {
                RuleSO card = cards[i];
                card.Validate(errors);

                string path = AssetDatabase.GetAssetPath(card);
                if (!string.IsNullOrEmpty(card.CardId))
                {
                    if (seen.TryGetValue(card.CardId, out string other))
                    {
                        errors.Add(card.CardId + ": 카드 ID 중복 — " + other + " / " + path);
                    }
                    else
                    {
                        seen.Add(card.CardId, path);
                    }
                }
            }

            if (errors.Count == 0)
            {
                Debug.Log("[근무수칙 카드 검사] " + cards.Count + "장 통과.");
                return;
            }

            Debug.LogWarning("[근무수칙 카드 검사] " + cards.Count + "장 중 문제 " + errors.Count + "건\n- " + string.Join("\n- ", errors));
        }

        private static List<RuleSO> LoadAll()
        {
            List<RuleSO> list = new List<RuleSO>();
            string[] guids = AssetDatabase.FindAssets("t:RuleSO");
            for (int i = 0; i < guids.Length; i++)
            {
                RuleSO card = AssetDatabase.LoadAssetAtPath<RuleSO>(AssetDatabase.GUIDToAssetPath(guids[i]));
                if (card != null)
                {
                    list.Add(card);
                }
            }

            return list;
        }
    }
}

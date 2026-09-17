using System.Collections.Generic;

namespace NightDuty
{
    /// <summary>
    /// 카드가 요구하는 씬 대상이 등록돼 있는지 시작 전에 검사한다.
    /// 기획서 공통 명세 5절: 「모든 카드에 대상 참조가 연결됐는지 시작 전 검사한다」.
    /// 누락은 크래시가 아니라 해당 카드의 미판정으로 처리한다.
    /// </summary>
    public static class RuleReferenceCheck
    {
        /// <summary>한 카드에서 누락된 대상 ID를 모은다(중복 제거).</summary>
        public static void FindMissing(RuleSO card, ICollection<string> registeredIds, List<string> missing)
        {
            if (card == null || registeredIds == null)
            {
                return;
            }

            List<string> required = new List<string>();
            card.CollectReferences(required);

            for (int i = 0; i < required.Count; i++)
            {
                string id = required[i];
                if (!registeredIds.Contains(id) && !missing.Contains(id))
                {
                    missing.Add(id);
                }
            }
        }

        /// <summary>여러 카드를 한꺼번에 검사해 "카드: 누락 ID" 형식의 줄을 돌려준다. 비어 있으면 통과.</summary>
        public static List<string> Report(IEnumerable<RuleSO> cards, ICollection<string> registeredIds)
        {
            List<string> lines = new List<string>();
            List<string> missing = new List<string>();

            foreach (RuleSO card in cards)
            {
                if (card == null)
                {
                    continue;
                }

                missing.Clear();
                FindMissing(card, registeredIds, missing);
                if (missing.Count > 0)
                {
                    lines.Add(card.CardId + ": " + string.Join(", ", missing));
                }
            }

            return lines;
        }
    }
}

using System.Collections.Generic;

namespace NightDuty
{
    /// <summary>
    /// 조건의 대상 ID를 카드의 대상 목록과 맞춰 보는 공통 규칙.
    /// <list type="bullet">
    /// <item>조건에 대상 ID가 있으면 그 ID와 정확히 일치해야 한다.</item>
    /// <item>비어 있으면 카드의 <see cref="RuleSO.TargetIds"/> 중 하나와 일치해야 한다.</item>
    /// <item>카드에도 대상이 없으면 아무 대상이나 허용한다.</item>
    /// <item><c>*</c>는 항상 아무 대상이나 허용한다.</item>
    /// </list>
    /// </summary>
    internal static class TargetMatch
    {
        public const string Any = "*";

        public static bool Matches(string conditionTarget, RuleSO card, string signalTarget)
        {
            if (conditionTarget == Any)
            {
                return true;
            }

            string actual = signalTarget ?? string.Empty;

            if (!string.IsNullOrEmpty(conditionTarget))
            {
                return conditionTarget == actual;
            }

            IReadOnlyList<string> targets = card != null ? card.TargetIds : null;
            if (targets == null || targets.Count == 0)
            {
                return true;
            }

            for (int i = 0; i < targets.Count; i++)
            {
                if (targets[i] == actual)
                {
                    return actual.Length > 0;
                }
            }

            return false;
        }

        public static void Collect(string conditionTarget, RuleSO card, List<string> ids)
        {
            if (conditionTarget == Any)
            {
                return;
            }

            if (!string.IsNullOrEmpty(conditionTarget))
            {
                ids.Add(conditionTarget);
                return;
            }

            IReadOnlyList<string> targets = card != null ? card.TargetIds : null;
            if (targets == null)
            {
                return;
            }

            for (int i = 0; i < targets.Count; i++)
            {
                if (!string.IsNullOrEmpty(targets[i]))
                {
                    ids.Add(targets[i]);
                }
            }
        }

        public static string Label(string conditionTarget)
        {
            if (conditionTarget == Any)
            {
                return "아무 대상";
            }

            return string.IsNullOrEmpty(conditionTarget) ? "카드 대상" : "'" + conditionTarget + "'";
        }
    }
}

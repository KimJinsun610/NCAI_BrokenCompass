using System;
using System.Collections.Generic;

namespace NightDuty
{
    /// <summary>
    /// 80차(민: 「안전한 읽기 보상은 수칙 빈칸 해독으로」 · 해독: C2 _? → 소녀 · S5 _? → 선임 · L3 ?_? → 담당자 · K1 !_ → 사망자).
    /// 가려진 글자가 있는 수칙과 그 해독. 안전한 읽기를 마치면 <see cref="NightRun"/>이 그날 덱의 빈칸 수칙 하나를 해독하고,
    /// 해독은 회차 내내 남는다(태블릿 수칙 카드·변조본 글에 함께 적용). 판정은 그대로다 — 글만 바뀐다.
    /// 조사가 받침에 맞게 바뀌도록 가림표와 그 뒤 조사를 함께 갈아 끼운다(「?_? 을」 → 「담당자를」).
    /// </summary>
    public static class BlankDecoding
    {
        private sealed class Entry
        {
            public readonly string RuleId;
            public readonly string Word;
            public readonly KeyValuePair<string, string>[] Swaps;

            public Entry(string ruleId, string word, params string[] pairs)
            {
                RuleId = ruleId;
                Word = word;
                Swaps = new KeyValuePair<string, string>[pairs.Length / 2];
                for (int i = 0; i < Swaps.Length; i++) Swaps[i] = new KeyValuePair<string, string>(pairs[i * 2], pairs[i * 2 + 1]);
            }
        }

        // 순서 = 같은 공간 후보가 없을 때 고르는 순서(덱 순서가 먼저다 — NightRun.DecodeBlank).
        private static readonly Entry[] Entries =
        {
            new Entry("C2", "소녀", "_? 는", "소녀는", "_?", "소녀"),
            new Entry("S5", "선임", "_?이", "선임이", "_?", "선임"),
            new Entry("L3", "담당자", "?_? 을", "담당자를", "?_?", "담당자"),
            new Entry("K1", "사망자", "!_ 이", "사망자가", "!_", "사망자"),
        };

        /// <summary>빈칸이 있는 수칙 ID(정해진 순서).</summary>
        public static IEnumerable<string> RuleIds
        {
            get
            {
                foreach (Entry e in Entries) yield return e.RuleId;
            }
        }

        /// <summary>그 수칙에 해독할 빈칸이 있는지.</summary>
        public static bool HasBlank(string ruleId)
        {
            return Find(ruleId) != null;
        }

        /// <summary>그 수칙 빈칸의 해독 낱말(없으면 null).</summary>
        public static string Word(string ruleId)
        {
            Entry e = Find(ruleId);
            return e != null ? e.Word : null;
        }

        /// <summary>그 수칙 글의 빈칸을 해독한 글. 빈칸 수칙이 아니거나 글이 비면 그대로.</summary>
        public static string Decode(string ruleId, string text)
        {
            Entry e = Find(ruleId);
            if (e == null || string.IsNullOrEmpty(text)) return text;
            foreach (KeyValuePair<string, string> swap in e.Swaps)
            {
                if (text.IndexOf(swap.Key, StringComparison.Ordinal) >= 0) return text.Replace(swap.Key, swap.Value);
            }

            return text;
        }

        private static Entry Find(string ruleId)
        {
            if (ruleId == null) return null;
            foreach (Entry e in Entries)
            {
                if (e.RuleId == ruleId) return e;
            }

            return null;
        }
    }
}

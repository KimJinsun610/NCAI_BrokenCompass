using System;
using System.Collections.Generic;
using UnityEngine;

namespace NightDuty
{
    /// <summary>
    /// 근무수칙 카드 24장의 <b>공급원</b>. 표의 칸은 일차별로 나뉘어 있지만 읽는 쪽은 그렇게 쓰지 않는다.
    /// <para>
    /// <b>이 표는 「일차별 덱」이 아니다</b>(2026-09-21 재설계). <see cref="NightRun"/>의 <c>CollectPool</c>이
    /// 1일차부터 <see cref="DayFloor.LastDay"/>까지의 모든 칸을 합쳐 <b>중복 없는 카드 풀 24장</b>으로 만들고,
    /// 그날 실제로 나갈 6장은 <see cref="DayDirector"/>가 고른다.
    /// 칸을 일차별로 남겨 둔 것은 기획팀이 쓰던 에셋을 그대로 살리기 위해서다 — 어느 칸에 넣든 풀에는 똑같이 들어간다.
    /// 일차가 표보다 크면 마지막 항목을 쓴다.
    /// </para>
    /// <para>
    /// <see cref="NightRun"/>이 <c>Resources/NightDeckTable</c>에서 찾는다. 이름과 위치를 바꾸지 말 것.
    /// </para>
    /// </summary>
    [CreateAssetMenu(menuName = "NightDuty/Night Deck Table", fileName = "NightDeckTable")]
    public sealed class NightDeckTableSO : ScriptableObject
    {
        /// <summary><see cref="Resources.Load(string)"/> 경로.</summary>
        public const string ResourcePath = "NightDeckTable";

        /// <summary>하루치 덱.</summary>
        [Serializable]
        public sealed class DayDeck
        {
            [Tooltip("덱 표시 순서대로. 같은 시각에 여러 카드가 반응하면 이 순서로 처리한다")]
            public RuleSO[] Cards = new RuleSO[0];
        }

        [SerializeField, Tooltip("0번 = 1일차")]
        private DayDeck[] _days = new DayDeck[0];

        /// <summary>일차 수.</summary>
        public int DayCount
        {
            get { return _days != null ? _days.Length : 0; }
        }

        /// <summary>
        /// 해당 일차의 덱. 일차가 표보다 크면 마지막 항목, 표가 비어 있으면 빈 목록.
        /// 비어 있는 칸(null)은 건너뛴다.
        /// </summary>
        public IReadOnlyList<RuleSO> DeckFor(int day)
        {
            List<RuleSO> result = new List<RuleSO>();
            if (_days == null || _days.Length == 0)
            {
                return result;
            }

            int index = Mathf.Clamp(day - 1, 0, _days.Length - 1);
            DayDeck deck = _days[index];
            if (deck == null || deck.Cards == null)
            {
                return result;
            }

            for (int i = 0; i < deck.Cards.Length; i++)
            {
                if (deck.Cards[i] != null)
                {
                    result.Add(deck.Cards[i]);
                }
            }

            return result;
        }

        /// <summary>에디터 도구용: 일차 칸의 카드 배열 복사본(빈 칸 포함). 없으면 빈 배열.</summary>
        internal RuleSO[] RawCardsOf(int dayIndex)
        {
            if (_days == null || dayIndex < 0 || dayIndex >= _days.Length || _days[dayIndex] == null || _days[dayIndex].Cards == null)
            {
                return new RuleSO[0];
            }

            return (RuleSO[])_days[dayIndex].Cards.Clone();
        }

        /// <summary>에디터 도구가 표를 채울 때 쓴다.</summary>
        internal void SetDays(DayDeck[] days)
        {
            _days = days ?? new DayDeck[0];
        }
    }
}

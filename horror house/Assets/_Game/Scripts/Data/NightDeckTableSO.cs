using System;
using System.Collections.Generic;
using UnityEngine;

namespace NightDuty
{
    /// <summary>
    /// 일차별로 배정할 근무수칙 카드 목록(덱 표시 순서 그대로).
    /// <para>
    /// <b>임시 편성표다.</b> 기획서의 덱 배정 규칙(DayDirector)이 생기기 전까지, 복도 한 공간으로 하룻밤을
    /// 끝까지 돌려 보기 위해 쓴다. 일차가 표보다 크면 마지막 항목을 쓴다.
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

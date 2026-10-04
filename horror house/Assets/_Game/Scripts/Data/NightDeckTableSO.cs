using System;
using System.Collections.Generic;
using UnityEngine;

namespace NightDuty
{
    /// <summary>
    /// 옛 근무수칙 카드 편성표. <b>2026-10-03에 옛 24장 카드를 폐기하면서 비웠다</b> — 판정 코어는 더 읽지 않는다.
    /// <para>
    /// 타입을 남긴 이유: 김진선님의 <c>TabletDocument</c>가 밤이 시작되기 전 폴백으로 <c>Resources/NightDeckTable</c>을 읽는다.
    /// 밤 중의 태블릿 수칙은 <see cref="NightRun.TodayDeck"/>(새 수칙)가 정본이다 — 점검표는 메시지로 간다.
    /// 표에 옛 카드를 다시 채우지 마십시오 — 되살리지 마십시오.
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

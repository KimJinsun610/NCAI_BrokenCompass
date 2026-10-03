using UnityEngine;

namespace NightDuty
{
    /// <summary>
    /// 태블릿에 실리는 수칙 한 줄(<b>표시 전용</b>). <see cref="NightRun.TodayDeck"/>이 그날 새 수칙·점검표로 런타임에 만든다(NightRun.Final.cs).
    /// <para>
    /// 김진선님 코드가 이 타입을 읽는다 — <c>TabletDocument</c>(본문·조작 안내), <c>GuardRoomLoiterTrigger</c>(ID), <see cref="NightDeckTableSO"/>(밤 전 폴백).
    /// 그래서 이름과 네 필드는 그대로 둔다.
    /// </para>
    /// <para>
    /// 2026-10-03: 옛 24장 카드의 판정 데이터(발동 자격·시작 신호·준수/위반/취소 조건·델타·역설 문구·반경·유예)를 지웠다.
    /// 판정은 새 수칙(<see cref="FinalRuleBook"/> · <see cref="ProgramCatalog"/>)이 코드로 한다. 되살리지 마십시오.
    /// </para>
    /// </summary>
    public sealed class RuleSO : ScriptableObject
    {
        [SerializeField, Tooltip("수칙·점검 ID (예: H1, inspect.H-2). 화면에 표시하지 않는다")]
        private string _cardId = string.Empty;

        [SerializeField, Tooltip("수칙이 속한 공간")]
        private SpaceId _space;

        [SerializeField, TextArea(3, 8), Tooltip("태블릿에 노출하는 본문")]
        private string _playerText = string.Empty;

        [SerializeField, TextArea(1, 4), Tooltip("조작 안내. 본문과 구분해 표시한다(점검표 첫 줄에만)")]
        private string _howTo = string.Empty;

        /// <summary>수칙·점검 ID.</summary>
        public string CardId { get { return _cardId; } }

        /// <summary>공간.</summary>
        public SpaceId Space { get { return _space; } }

        /// <summary>태블릿 노출 본문.</summary>
        public string PlayerText { get { return _playerText; } }

        /// <summary>조작 안내. 본문과 같은 문단에 섞지 않는다. 대부분 비어 있다.</summary>
        public string HowTo { get { return _howTo ?? string.Empty; } }

        /// <summary>코드로 값을 채운다.</summary>
        internal void Configure(Config config)
        {
            _cardId = config.CardId ?? string.Empty;
            _space = config.Space;
            _playerText = config.PlayerText ?? string.Empty;
            _howTo = config.HowTo ?? string.Empty;
        }

        /// <summary><see cref="Configure"/>용 값 묶음.</summary>
        internal sealed class Config
        {
            public string CardId = string.Empty;
            public SpaceId Space;
            public string PlayerText = string.Empty;
            public string HowTo = string.Empty;
        }
    }
}

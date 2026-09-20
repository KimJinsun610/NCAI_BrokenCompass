using System.Collections.Generic;

namespace NightDuty
{
    /// <summary>
    /// 태블릿으로 보낼 역설 문자를 고른다(2026-09-19 기획서 10-5·12절).
    /// <list type="bullet">
    /// <item>역설은 <b>짝 수칙 카드에 1:1로 붙어 있다</b>(<see cref="RuleSO.ParadoxText"/>). 그날 배정된 카드가 곧 그날의 후보다.</item>
    /// <item>짝 카드의 판정이 <b>진행 중</b>일 때만 보낸다 — 기획서의 발송 시점(단서 직후)이 곧 카드가 시작되는 시점이다.</item>
    /// <item>신뢰 25 미만이면 한 건도 보내지 않는다. 구간이 오를수록 하루 상한이 1 → 4쌍으로 늘어난다.</item>
    /// <item>한 카드에 하루 한 번. 문자 쪽에는 벌점이 없다 — 따르면 짝 카드가 위반으로, 거절하면 준수로 정산된다.</item>
    /// </list>
    /// <para>
    /// 하루 상한의 구간 경계는 <b>시험값</b>이다(기획서 10-5절 원문 미확인). <see cref="DailyQuota"/>만 고치면 된다.
    /// </para>
    /// </summary>
    public sealed class ParadoxDirector
    {
        /// <summary>역설이 실리기 시작하는 신뢰 값.</summary>
        public const int MinTrust = 25;

        private readonly HashSet<string> _sentToday = new HashSet<string>();
        private readonly List<ParadoxMessage> _today = new List<ParadoxMessage>();

        /// <summary>오늘 보낸 문자(발송 순서).</summary>
        public IReadOnlyList<ParadoxMessage> Today { get { return _today; } }

        /// <summary>새 밤. 하루 기록을 비운다.</summary>
        public void BeginNight()
        {
            _sentToday.Clear();
            _today.Clear();
        }

        /// <summary>
        /// 신뢰 구간이 허용하는 하루 최대 쌍 수. 25 미만 0, 이후 1 → 4.
        /// </summary>
        public static int DailyQuota(int trust)
        {
            if (trust < MinTrust) return 0;
            if (trust < 50) return 1;
            if (trust < 75) return 2;
            if (trust < 90) return 3;
            return 4;
        }

        /// <summary>
        /// 지금 보낼 수 있는 문자가 있으면 보낸다. 판정 신호를 처리한 뒤마다 부르면 된다.
        /// </summary>
        /// <param name="book">그날 판정. 진행 중인 카드를 본다.</param>
        /// <param name="axes">회차 축(신뢰를 읽는다).</param>
        /// <param name="minute">발송 시각(0:00 기준 분). 모르면 -1.</param>
        /// <returns>이번 호출에서 보낸 문자 수.</returns>
        public int Poll(RuleBook book, IFearAxisReader axes, int minute)
        {
            if (book == null || axes == null)
            {
                return 0;
            }

            int quota = DailyQuota(axes.GetValue(FearAxis.Trust)) - _today.Count;
            if (quota <= 0)
            {
                return 0;
            }

            int sent = 0;
            IReadOnlyList<RuleWatcher> watchers = book.Watchers;
            for (int i = 0; i < watchers.Count && sent < quota; i++)
            {
                RuleWatcher w = watchers[i];
                if (w.State != CardState.Active)
                {
                    continue;
                }

                RuleSO card = w.Card;
                if (card == null || !card.HasParadox || _sentToday.Contains(card.CardId))
                {
                    continue;
                }

                _sentToday.Add(card.CardId);
                ParadoxMessage message = new ParadoxMessage(card.ParadoxId, card.CardId, card.Space, card.ParadoxText, minute);
                _today.Add(message);
                EventBus.RaiseMessageSent(message);
                sent++;
            }

            return sent;
        }
    }

    /// <summary>태블릿 메시지 탭에 실리는 문자 한 건. 발신자는 표시하지 않는다(기획서 8-3).</summary>
    public readonly struct ParadoxMessage
    {
        /// <summary>역설 ID(P1~P23). 화면에 표시하지 않는다.</summary>
        public readonly string ParadoxId;

        /// <summary>짝 수칙 ID. 화면에 표시하지 않는다.</summary>
        public readonly string CardId;

        /// <summary>문자가 부르는 공간.</summary>
        public readonly SpaceId Space;

        /// <summary>문자 본문.</summary>
        public readonly string Text;

        /// <summary>수신 시각(0:00 기준 분). 모르면 -1.</summary>
        public readonly int Minute;

        /// <summary>문자를 만든다.</summary>
        public ParadoxMessage(string paradoxId, string cardId, SpaceId space, string text, int minute)
        {
            ParadoxId = paradoxId ?? string.Empty;
            CardId = cardId ?? string.Empty;
            Space = space;
            Text = text ?? string.Empty;
            Minute = minute;
        }
    }
}

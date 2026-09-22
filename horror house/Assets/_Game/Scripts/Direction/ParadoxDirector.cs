using System.Collections.Generic;

namespace NightDuty
{
    /// <summary>
    /// 태블릿으로 보낼 역설 문자를 고른다(2026-09-19 기획서 10-5·12절).
    /// <list type="bullet">
    /// <item>역설은 <b>짝 수칙 카드에 1:1로 붙어 있다</b>(<see cref="RuleSO.ParadoxText"/>). 그날 배정된 카드가 곧 그날의 후보다.</item>
    /// <item>짝 카드의 판정이 <b>진행 중</b>일 때만 보낸다 — 기획서의 발송 시점(단서 직후)이 곧 카드가 시작되는 시점이다.</item>
    /// <item>신뢰 24 미만이면 한 건도 보내지 않는다. 구간이 오를수록 하루 상한이 1 → 4쌍으로 늘어난다.</item>
    /// <item>한 카드에 하루 한 번. 문자 쪽에는 벌점이 없다 — 따르면 짝 카드가 위반으로, 거절하면 준수로 정산된다.</item>
    /// </list>
    /// <para>
    /// 하루 상한은 <b>근무 시작 시점의 신뢰</b>로 정해지고 그날은 고정된다(2026-09-20 기획서 C절·10-5절).
    /// 밤 도중에 준수 정산으로 신뢰가 올라도 그날 상한은 늘어나지 않는다.
    /// </para>
    /// </summary>
    public sealed class ParadoxDirector
    {
        /// <summary>
        /// 역설이 실리기 시작하는 신뢰 값. <b>Band1의 하한과 같은 값이어야 한다</b>(2026-09-21 재설계로 25 → 24).
        /// <see cref="DailyQuota"/>가 <see cref="Bands.Of"/>로 구간을 읽으므로, 이 값이 경계와 어긋나면
        /// 신뢰 24~에서 「구간은 1인데 상한은 0」이 되어 한 칸이 비어 버린다.
        /// </summary>
        public const int MinTrust = 24;

        private readonly HashSet<string> _sentToday = new HashSet<string>();
        private readonly List<ParadoxMessage> _today = new List<ParadoxMessage>();

        /// <summary>그날 상한. <see cref="BeginNight"/>에서 한 번 정하고 밤 내내 바뀌지 않는다.</summary>
        private int _quotaToday;

        /// <summary>오늘 보낸 문자(발송 순서).</summary>
        public IReadOnlyList<ParadoxMessage> Today { get { return _today; } }

        /// <summary>
        /// 오늘 이 카드에 역설 문자를 보냈는지. <b>결산의 3구분 표기가 이것을 읽는다.</b>
        /// <para>
        /// 문자를 받은 카드를 어긴 것은 「그냥 어김」이 아니라 「지시를 따르다 어김」이다.
        /// 둘을 같은 빨간 줄로 그리면 플레이어는 이틀이면 「문자는 함정, 무시가 정답」을 배우고
        /// 그 뒤로는 역설을 고민하지 않는다 — 23쌍을 준비해 놓고 2일차부터 전부 버리는 셈이다.
        /// 수치 손해는 똑같이 주되 <b>종이가 부르는 이름만</b> 다르게 해서 고민을 살려 둔다.
        /// </para>
        /// <para>따랐는지 거절했는지는 묻지 않는다 — 그건 짝 카드의 판정 결과가 말해 준다.</para>
        /// </summary>
        /// <param name="cardId">짝 수칙 ID.</param>
        public bool WasSentToday(string cardId)
        {
            return !string.IsNullOrEmpty(cardId) && _sentToday.Contains(cardId);
        }

        /// <summary>그날 실릴 수 있는 역설 최대 쌍 수(근무 시작 시 신뢰로 고정).</summary>
        public int QuotaToday { get { return _quotaToday; } }

        /// <summary>
        /// 새 밤. 하루 기록을 비우고 <b>그날 상한을 근무 시작 시점의 신뢰로 고정한다.</b>
        /// <para>
        /// 호출 위치는 덱을 구성한 뒤·<c>RuleBook.BeginNight</c> <b>앞</b>이어야 한다 —
        /// 밤 시작 장기 카드(C6)가 시작되기 전에 상한이 먼저 정해져야 한다.
        /// </para>
        /// </summary>
        /// <param name="axes">회차 축. null이면 상한 0(그날은 한 건도 보내지 않는다).</param>
        public void BeginNight(IFearAxisReader axes)
        {
            _sentToday.Clear();
            _today.Clear();
            _quotaToday = axes == null ? 0 : DailyQuota(axes.GetValue(FearAxis.Trust));
        }

        /// <summary>
        /// 신뢰 구간이 허용하는 하루 최대 쌍 수. 24 미만(<see cref="MinTrust"/>) 0, 이후 1 → 4.
        /// <para>
        /// 기획서 C절의 경계(재설계 후 0~23 / 24~47 / 48~71 / 72~89 / 90~99)는 <see cref="Bands"/>의 구간과 같은 눈금이라
        /// 구간 번호가 곧 상한이다. CLAUDE.md §2.3의 「균등 분할 계산 금지」를 지키려고 구간표를 그대로 쓴다.
        /// </para>
        /// </summary>
        public static int DailyQuota(int trust)
        {
            if (trust < MinTrust) return 0;
            return (int)Bands.Of(trust);
        }

        /// <summary>
        /// 지금 보낼 수 있는 문자가 있으면 보낸다. 판정 신호를 처리한 뒤마다 부르면 된다.
        /// </summary>
        /// <param name="book">그날 판정. 진행 중인 카드를 본다.</param>
        /// <param name="axes">회차 축. <b>여기서 신뢰를 다시 읽지 않는다</b> — 상한은 <see cref="BeginNight"/>에서 고정됐다.</param>
        /// <param name="minute">발송 시각(0:00 기준 분). 모르면 -1.</param>
        /// <returns>이번 호출에서 보낸 문자 수.</returns>
        public int Poll(RuleBook book, IFearAxisReader axes, int minute)
        {
            if (book == null || axes == null)
            {
                return 0;
            }

            // 상한은 밤 시작에 고정된 값이다. 지금 신뢰를 다시 읽으면
            // 밤 도중 준수 정산으로 신뢰가 오를 때 그날 상한이 함께 늘어난다(기획서 C절 위반).
            int quota = _quotaToday - _today.Count;
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

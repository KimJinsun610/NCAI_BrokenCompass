namespace NightDuty
{
    /// <summary>
    /// 태블릿 문자함에 실리는 문자 한 건. 발신자는 표시하지 않는다. <see cref="EventBus.MessageSent"/>로 나가고 <c>TabletBridge</c>가 태블릿에 넣는다.
    /// <para>옛 역설 연출기(ParadoxDirector, 옛 24장 짝 역설)는 2026-10-03에 폐기했다. 새 역설·변조 발송기는 10단계에서 이 문자 형식을 그대로 쓴다.</para>
    /// </summary>
    public readonly struct ParadoxMessage
    {
        /// <summary>문자 ID. 화면에 표시하지 않는다.</summary>
        public readonly string ParadoxId;

        /// <summary>겨누는 수칙 ID. 화면에 표시하지 않는다.</summary>
        public readonly string CardId;

        /// <summary>문자가 부르는 공간.</summary>
        public readonly SpaceId Space;

        /// <summary>문자 본문.</summary>
        public readonly string Text;

        /// <summary>수신 시각(근무 시작부터의 분). 모르면 -1.</summary>
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

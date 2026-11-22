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

        /// <summary>
        /// 2026-11-22(로컬라이징): 본문을 지금 언어로 다시 만드는 함수(없으면 null — <see cref="Text"/> 그대로).
        /// 태블릿은 받은 문자를 언어가 바뀔 때 이것으로 다시 그린다. <see cref="Text"/>는 보낸 순간의 글이다.
        /// </summary>
        public readonly System.Func<string> Source;

        /// <summary>문자를 만든다.</summary>
        public ParadoxMessage(string paradoxId, string cardId, SpaceId space, string text, int minute)
            : this(paradoxId, cardId, space, text, minute, null)
        {
        }

        /// <summary>2026-11-22: 다시 만드는 함수와 함께 문자를 만든다. <paramref name="text"/>는 보낸 순간의 글(보통 <c>source()</c>).</summary>
        public ParadoxMessage(string paradoxId, string cardId, SpaceId space, string text, int minute, System.Func<string> source)
        {
            ParadoxId = paradoxId ?? string.Empty;
            CardId = cardId ?? string.Empty;
            Space = space;
            Text = text ?? string.Empty;
            Minute = minute;
            Source = source;
        }

        /// <summary>2026-11-22: 지금 언어의 본문(<see cref="Source"/>가 없으면 <see cref="Text"/>).</summary>
        public string CurrentText
        {
            get
            {
                if (Source == null) return Text;
                string s = Source();
                return string.IsNullOrEmpty(s) ? Text : s;
            }
        }
    }
}

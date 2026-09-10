namespace NightDuty
{
    /// <summary>
    /// 지침록 맨 아래에 인쇄되는 §0 조항의 형태.
    /// 전화 지시와 문서 수칙이 충돌할 때 무엇이 우선인지를 정하는 한 줄짜리 조항이며,
    /// 본문보다 눈에 띄게 작게 인쇄되어 대부분의 플레이어는 Day 2의 첫 충돌 이후에야 존재를 알아차린다.
    /// </summary>
    public enum ClauseZeroType
    {
        /// <summary>
        /// A형 — 전화 지시 우선.
        /// 문서에 적힌 수칙과 전화로 내려온 지시가 충돌하면 전화 지시를 따르는 것이 준수로 판정된다.
        /// </summary>
        TypeA_PhoneFirst = 0,

        /// <summary>
        /// B형 — 문서 우선.
        /// 충돌 시 인쇄된 수칙을 따르는 것이 준수로 판정되며, 전화 지시를 따르면 위반으로 기록된다.
        /// </summary>
        TypeB_DocumentFirst = 1,

        /// <summary>
        /// 미인쇄 — Day 5에는 §0 조항 자체가 인쇄되지 않는다.
        /// 어느 쪽도 정답이 아니며, 플레이어가 스스로 고른 쪽이 그대로 결과가 된다.
        /// </summary>
        NotPrinted = 2
    }

    /// <summary>
    /// 지침록의 한 줄이 어떤 서체로 인쇄되는지를 나타낸다.
    /// 신뢰(Trust) 축이 오를수록 <see cref="Variant1"/>·<see cref="Variant2"/>로 인쇄되는 줄이 늘어난다.
    /// <para>
    /// 주의: 서체 차이는 반드시 <b>미묘</b>해야 한다. "뭔가 이상하다" 정도로만 느껴져야 하며,
    /// 명조↔고딕처럼 한눈에 다른 서체로 바꾸면 안 된다.
    /// 자간·굵기·글자 높이를 아주 조금 다르게 한 동일 계열 서체를 쓴다.
    /// 이 서체 차이는 신뢰 축이 월드가 아닌 문서로 드러나는 <b>유일한 시각 신호</b>이므로,
    /// 너무 뚜렷하면 플레이어가 곧바로 규칙을 학습해 버리고 너무 약하면 신뢰 축이 아예 보이지 않는다.
    /// </para>
    /// </summary>
    public enum FontVariant
    {
        /// <summary>기본 서체. 신뢰 축이 낮을 때 모든 줄이 이 서체로 인쇄된다.</summary>
        Normal = 0,

        /// <summary>1차 변형 서체. 기본 서체와 거의 구별되지 않을 정도의 차이만 준다.</summary>
        Variant1 = 1,

        /// <summary>2차 변형 서체. 신뢰 축 상위 구간에서만 등장하며, 그래도 한눈에 알아보게 만들지 않는다.</summary>
        Variant2 = 2
    }

    /// <summary>
    /// 지침록에 인쇄되는 근무수칙 한 줄의 표시 정보.
    /// 판정에 쓰이는 규칙 데이터(RuleSO)와 달리, 이 구조체는 "그날 문서에 어떻게 보이는가"만 담는다.
    /// </summary>
    public readonly struct RuleEntry
    {
        /// <summary>수칙의 문구. 문서에 인쇄되는 그대로의 텍스트다.</summary>
        public readonly string Text;

        /// <summary>이 줄이 인쇄되는 서체. 신뢰 축이 높을수록 변형 서체가 섞인다.</summary>
        public readonly FontVariant Font;

        /// <summary>
        /// 소거된 줄인지 여부. 축이 70 이상이면 하루에 한 줄이 문서에서 사라진다.
        /// <para>
        /// 이 줄은 인쇄되지 않지만 <b>판정은 그대로 살아 있다</b>. 플레이어는 존재조차 모르는 수칙을 어기게 된다.
        /// 또한 주변 줄들은 원래의 번호를 그대로 유지해야 한다 — 「3번 다음이 5번」처럼 번호가 비어 보여야
        /// 무언가 빠졌다는 사실만 감지할 수 있다. 절대 번호를 다시 매기지 말 것.
        /// </para>
        /// </summary>
        public readonly bool IsErased;

        /// <summary>지침록 한 줄을 구성한다.</summary>
        /// <param name="text">수칙 문구 그대로의 텍스트.</param>
        /// <param name="font">인쇄에 사용할 서체 변형.</param>
        /// <param name="isErased">true면 인쇄되지 않지만 판정은 계속된다.</param>
        public RuleEntry(string text, FontVariant font, bool isErased)
        {
            Text = text;
            Font = font;
            IsErased = isErased;
        }
    }
}

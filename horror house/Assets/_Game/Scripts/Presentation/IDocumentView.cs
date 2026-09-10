namespace NightDuty
{
    /// <summary>
    /// 태블릿 지침록 화면. 신뢰(Trust) 축이 밖으로 드러나는 <b>유일한 출력 표면</b>이다.
    /// <para>
    /// 신뢰 축이 오를수록 그날 수칙들 사이의 모순이 늘고(Band0 0개 · Band2 2개 · Band3 3개 · Band4 5개),
    /// 일부 줄이 미묘하게 다른 서체(<see cref="FontVariant"/>)로 인쇄된다. 월드는 전혀 바뀌지 않는다.
    /// </para>
    /// <para>
    /// 지침록을 열어도 <b>게임은 멈추지 않는다</b>. 근무 시계는 계속 흐르므로, 문서를 오래 들여다보는 것 자체가
    /// 순찰 시간을 깎는 선택이 된다. 어떤 경우에도 일시정지를 걸지 말 것.
    /// </para>
    /// </summary>
    public interface IDocumentView
    {
        /// <summary>지침록이 현재 열려 있는지 여부.</summary>
        bool IsOpen { get; }

        /// <summary>
        /// 그날의 지침록을 그린다.
        /// <para>
        /// 소거된 줄(<see cref="RuleEntry.IsErased"/>)은 인쇄하지 않되 주변 줄의 원래 번호는 유지한다
        /// (「3번 다음이 5번」). 번호를 다시 매기면 무엇이 사라졌다는 단서 자체가 없어진다.
        /// </para>
        /// <para>
        /// §0 조항(<paramref name="clause"/>)은 스크롤 <b>맨 아래</b>에, 본문보다 눈에 띄게 작은 크기로 그린다.
        /// 대부분의 플레이어는 Day 2의 첫 충돌을 겪고 나서야 이 조항을 찾아내야 하기 때문이다.
        /// 다만 이 크기 비율은 인스펙터에 노출된 값으로 두어 플레이테스트로 조정할 수 있게 한다.
        /// <see cref="ClauseZeroType.NotPrinted"/>(Day 5)일 때는 조항 영역 자체를 그리지 않는다.
        /// </para>
        /// </summary>
        /// <param name="rules">그날 인쇄할 수칙 목록. 인덱스는 원래 번호 순서를 그대로 유지한다.</param>
        /// <param name="clause">문서 하단 §0 조항의 형태.</param>
        /// <param name="trustValue">신뢰 축 현재값(0~100). 서체 변형 비율 등 표시 강도의 참고값이다.</param>
        void Render(System.Collections.Generic.IReadOnlyList<RuleEntry> rules, ClauseZeroType clause, int trustValue);

        /// <summary>지침록을 연다. 근무 시계는 계속 흐른다.</summary>
        void Open();

        /// <summary>지침록을 닫는다.</summary>
        void Close();

        /// <summary>
        /// ComplianceMode가 Attempt인 수칙이 충족되었을 때 호출된다.
        /// <para>
        /// 해당 줄에 <b>극히 미묘한</b> 변화만 준다(예: 자간을 아주 조금 좁힘).
        /// 체크 표시·색 변화·애니메이션처럼 확인 신호로 읽히는 연출은 금지다 —
        /// 플레이어에게 준수 여부를 알려 주지 않는 것이 설계 원칙이며,
        /// 이 표시는 반복 플레이어가 뒤늦게 알아차리라고만 존재한다.
        /// </para>
        /// </summary>
        /// <param name="ruleIndex">충족된 수칙의 인덱스. <see cref="Render"/>에 넘긴 목록 기준이다.</param>
        void MarkAttemptSatisfied(int ruleIndex);
    }
}

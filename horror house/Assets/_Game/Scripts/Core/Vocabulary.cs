namespace NightDuty
{
    /// <summary>
    /// 순찰 대상 공간 식별자.
    /// 근무수칙(RuleSO)과 공간 프로필(SpaceProfileSO)이 모두 이 값으로 공간을 지목한다.
    /// 명시적 숫자를 박아 두는 이유: 이 값이 .asset(ScriptableObject)에 직렬화되므로
    /// 열거자 순서를 바꾸면 기존 데이터의 의미가 통째로 어긋난다. <b>새 값은 빈 번호에 덧붙인다.</b>
    /// <para>
    /// 2026-09-30 최종 기획서의 공간은 여섯 곳이다 — 복도·교실(Classroom02 하나)·과학실·화장실·도서관·경비실.
    /// 교실은 <see cref="Classroom"/>(8)이고, 옛 24장 카드가 쓰는 <see cref="Classroom_1_1"/>·<see cref="Classroom_1_3"/>은
    /// 6단계(공간·수칙 교체)에서 정리한다. 그 전까지 <see cref="SpaceIds.Canonical"/>이 셋을 같은 교실로 읽는다.
    /// </para>
    /// </summary>
    public enum SpaceId
    {
        /// <summary>미지정. 기본값이자 "아직 어떤 공간도 아니다"라는 뜻. 유효한 순찰 지점이 아니다.</summary>
        None = 0,

        /// <summary>
        /// 복도. 자기보고(self-reporting) 공간 — 플레이어가 공포 지표를 읽는 법을 배우는 곳이다.
        /// 점등된 형광등 개수(8개 중) = 조도 축, 밖에 나와 있는 집기 = 배치 축, 문 소리 = 청각 축.
        /// </summary>
        Corridor = 1,

        /// <summary>화장실(좌측 동). 칸막이 4칸. 각 칸의 문은 HingedDoor 컴포넌트를 쓴다.</summary>
        Toilet = 2,

        /// <summary>옛 교실 1-1(옛 24장 카드 전용). 새 코드는 <see cref="Classroom"/>을 쓴다.</summary>
        Classroom_1_1 = 3,

        /// <summary>옛 교실 1-3(옛 24장 카드 전용). 새 코드는 <see cref="Classroom"/>을 쓴다.</summary>
        Classroom_1_3 = 4,

        /// <summary>
        /// 과학실(우측 동). 등 4개. 조도 Band4(90~99)에서는 등 0개에 붉은 잔광만 남는다(공통 색온도표).
        /// </summary>
        ScienceRoom = 5,

        /// <summary>도서관(좌측 동). 조명이 하나도 없는 방 — 모든 점검을 손전등에 기댄다.</summary>
        Library = 6,

        /// <summary>경비실(우측 동). 근무 시작 지점이자 CCTV·근무일지가 있는 거점. 완전히 안전하지는 않다(K3).</summary>
        SecurityRoom = 7,

        /// <summary>교실(Classroom02, 우측 동). 문짝 없이 문틀만 두 개 남은 창고형 교실. 최종 기획서의 교실은 이것 하나다.</summary>
        Classroom = 8
    }

    /// <summary>건물의 어느 쪽인지. 점검 공간은 3~5일차에 좌·우 두 동에 각각 하나 이상을 둔다(최종 기획서 「동선」).</summary>
    public enum Wing
    {
        /// <summary>양쪽을 잇는 복도.</summary>
        Center = 0,

        /// <summary>우측 동 — 교실·과학실·경비실.</summary>
        Right = 1,

        /// <summary>좌측 동 — 도서관·화장실.</summary>
        Left = 2
    }

    /// <summary>공간 식별자 도우미.</summary>
    public static class SpaceIds
    {
        /// <summary>최종 기획서의 공간 여섯 곳(점검·수칙 편성 순서).</summary>
        public static readonly SpaceId[] Final =
        {
            SpaceId.Corridor, SpaceId.Classroom, SpaceId.ScienceRoom, SpaceId.Toilet, SpaceId.Library, SpaceId.SecurityRoom
        };

        /// <summary>옛 교실 두 값을 새 교실 하나로 읽는다. 나머지는 그대로.</summary>
        public static SpaceId Canonical(SpaceId space)
        {
            return space == SpaceId.Classroom_1_1 || space == SpaceId.Classroom_1_3 ? SpaceId.Classroom : space;
        }

        /// <summary>그 공간이 건물의 어느 쪽인지.</summary>
        public static Wing WingOf(SpaceId space)
        {
            switch (Canonical(space))
            {
                case SpaceId.Toilet:
                case SpaceId.Library:
                    return Wing.Left;
                case SpaceId.Classroom:
                case SpaceId.ScienceRoom:
                case SpaceId.SecurityRoom:
                    return Wing.Right;
                default:
                    return Wing.Center;
            }
        }
    }

    /// <summary>
    /// 공포 축. 각 축은 0~100 범위의 정수 값을 가지며 5개 구간(<see cref="Band"/>)으로 나뉜다.
    /// 청각·조도·배치 세 축은 월드의 연출을 직접 구동하고,
    /// 신뢰 축만은 월드가 아니라 인게임 지침록(문서)을 구동한다.
    /// </summary>
    public enum FearAxis
    {
        /// <summary>
        /// 청각(廳覺). 문 여닫는 소리, 발소리, 정체 불명의 소음 등 사운드 연출 밀도를 결정한다.
        /// </summary>
        Auditory = 0,

        /// <summary>
        /// 조도(照度). 점등된 형광등 개수와 색온도를 결정한다.
        /// </summary>
        Illuminance = 1,

        /// <summary>
        /// 배치(配置). 집기·소품이 제자리에 있는지, 문이 열려 있는지 등 오브젝트 상태를 결정한다.
        /// </summary>
        Layout = 2,

        /// <summary>
        /// 신뢰(信賴). 수칙을 <b>준수</b>할 때 오른다. 다른 세 축과 달리 월드에 절대 그려지지 않는다.
        /// 신뢰 구간(전용 경계 15/30/45/65)이 오를수록 <b>태블릿 문자와 근무수칙의 충돌(역설)이 늘어난다</b>.
        /// <b>신뢰는 게임오버를 일으키지 않는다.</b> 100에서 멈출 뿐 종료 잠금은 청각·조도·배치만 건다.
        /// 연출 코드가 이 축을 보고 월드를 바꾸려 한다면 그건 설계 위반이다.
        /// </summary>
        Trust = 3
    }

    /// <summary>
    /// 공포 축 값이 속하는 구간. 폭이 균일하지 않다는 점에 주의할 것.
    /// 감각 축: Band0 = 0–24, Band1 = 25–49, Band2 = 50–74, Band3 = 75–89, Band4 = 90–99, 100 = 붙잡힘.
    /// 신뢰: 0–14 / 15–29 / 30–44 / 45–64 / 65~(<see cref="Bands.OfTrust"/>).
    /// 연출이 보는 구간은 <see cref="BandResolver"/>의 연출 구간이다(내려가지 않음, 일차 하한 적용).
    /// 실제 범위 표는 <see cref="Bands"/>에 한 곳으로 모아 두었다.
    /// </summary>
    public enum Band
    {
        /// <summary>평상. 이상 징후가 사실상 없다.</summary>
        Band0 = 0,

        /// <summary>미약한 위화감.</summary>
        Band1 = 1,

        /// <summary>명백한 이상.</summary>
        Band2 = 2,

        /// <summary>고조.</summary>
        Band3 = 3,

        /// <summary>붙잡힘 직전 경고 표현. 청각·조도·배치가 100에 도달하면 구간이 아니라 종료 잠금이다(<c>EventBus.AxisCritical</c>). 신뢰 100은 Band4로 취급한다.</summary>
        Band4 = 4
    }
}

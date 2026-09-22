namespace NightDuty
{
    /// <summary>
    /// 태블릿의 <b>업무 상태 문구</b>. 판정도 델타도 없는 안내문이며, 시나리오 기획서 v6 §3-3·§4가 정본이다.
    /// <para>
    /// <b>왜 수칙 카드가 아닌가.</b> 이 문장들은 역설(P)·발견유도(G)·재방문(N) 어느 덱에도 속하지 않는다.
    /// 읽지 않았다는 이유로 델타를 부과하지 않고, 수락·거절·미도달 벌점도 없다(기획서 §3-3).
    /// 카드로 만들면 그 순간 판정 대상이 되므로 <b>여기 상수로 둔다</b>.
    /// </para>
    /// <para>
    /// <b>DAY는 바깥 날짜가 아니라 회사가 붙인 관측 회차다</b>(기획서 §4). 04:00에 근무자는 의식을 잃고
    /// 다시 00:00의 경비실에서 깨어나지만 DAY 숫자와 완료 기록은 올라간다. 그래서 2일차 문구가
    /// 「1회차 관측 기록이 접수되었습니다」로 시작한다 — 직전 밤이 1회차였다는 뜻이다.
    /// </para>
    /// <para>
    /// <b>특정 공간을 지목하지 않는다.</b> 조우가 어느 공간에서 일어나는지는 회차마다 달라지므로
    /// (기획서 §4 「날짜별 공간을 고정하지 않는다」), 업무 상태 문구도 「이미 어디를 봤다」고 단정하지 않는다.
    /// </para>
    /// <para>표시 주체는 태블릿 UI다. 아직 없으므로 지금은 아무도 읽지 않는다.</para>
    /// </summary>
    public static class DayBriefText
    {
        /// <summary>업무 상태 문구가 있는 첫 일차. 1일차는 대신 습득 안내 세 장을 순서대로 본다.</summary>
        public const int FirstBriefDay = 2;

        /// <summary>
        /// 1일차, 태블릿을 주운 직후 맨 처음. 실제 물건을 인수하거나 목록을 체크하는 시스템이 아니라
        /// 판정 없는 안내 화면이다(기획서 §3-3).
        /// </summary>
        public const string HandoverNotice =
            "이전 근무자의 퇴실 기록이 확인되지 않았습니다.\n현장 근무 지침을 확인한 뒤 업무를 시작하십시오.";

        /// <summary>
        /// 1일차, 확인 입력 뒤 수칙 첫머리. 회사가 사고를 알고 있었다는 흔적과,
        /// 수칙을 따를 생존상의 이유만 전달한다. 새 행동 카드·역설 문자·준수 보상 대상이 아니다.
        /// </summary>
        public const string SafetyNotice =
            "아래 수칙은 이전 근무 중 발생한 사고를 기준으로 작성되었습니다.\n" +
            "평소의 시설관리 방식과 다르더라도 임의로 변경하지 마십시오. 귀하의 안전은 해당 절차를 전제로 안내됩니다.";

        /// <summary>
        /// S1의 조작 안내. 수칙 본문(<c>RuleSO.PlayerText</c>)과 <b>구분해</b> 표시한다(기획서 §3-3).
        /// 첫 방문 전에 확인 대상을 알 수 있어야 하므로 본문과 같은 화면에 두되 같은 문단에 섞지 않는다.
        /// </summary>
        public const string FirstCardHowTo =
            "확인 방법: 보관 위치의 인체모형을 화면 중앙에 1초간 두십시오.";

        /// <summary>
        /// <b>점검</b>이 무엇인지 알려 주는 한 줄. 수칙 여덟 장(C1·C2·C5·C6·S3·S4·T5·T6)이 성공 조건으로 점검을 요구하는데,
        /// 「점검하십시오」라는 말만으로는 무엇을 하라는 것인지 알 수 없습니다(2026-09-23 사용자 보고).
        /// <para>실제 규칙은 <c>SpaceZones</c>에 있습니다 — 그 공간의 <b>지정 점검 상자</b> 안에
        /// <c>inspectionDwellSeconds</c>(현재 1초)만큼 머물면 체류가 차고, <b>그 공간을 나갈 때</b>
        /// <c>InspectionCompleted</c>가 나갑니다. 방 안에서는 아무 일도 일어나지 않는 것이 정상입니다.</para>
        /// <para><b>[기획 확인 대기]</b> 아래 문구는 구현에서 읽어 쓴 잠정안입니다. 기획서에 대응 문장이 없습니다.
        /// 승인 담당(민)이 문구를 확정하면 여기만 고치면 됩니다.</para>
        /// </summary>
        public const string InspectionHowTo =
            "점검하는 법: 그 방 안쪽에 1초 머문 뒤 방을 나오면 점검한 것이 됩니다.";

        /// <summary>두 교실을 <b>각각</b> 점검해야 하는 카드(C6)용. 한쪽만 해서는 성립하지 않습니다.</summary>
        public const string InspectionHowToBothRooms =
            "점검하는 법: 1-1과 1-3에 각각 1초씩 머문 뒤 각각 나와야 합니다. 한쪽만으로는 안 됩니다.";

        /// <summary>
        /// 최초 조우 관찰 직후 한 줄. 인체모형을 연락 두절된 경비원이라고 <b>설명하지 않는다</b> —
        /// 플레이어가 둘의 관계를 의심하게만 한다(기획서 §3-4).
        /// </summary>
        public const string FirstEncounterNotice =
            "최초 인계 대상이 확인되었습니다. 해당 대상은 반출 목록에 없습니다.";

        /// <summary>
        /// 엔딩 E04의 마지막 지시. 순찰 중 문자도 근무수칙 카드도 아니고,
        /// 모든 마감 정산 후 표시하는 종료 절차다(기획서 §5-2).
        /// </summary>
        public const string ExitInstruction =
            "퇴실 시 출입문을 닫아 주십시오. 현장에 남아 있는 것은 인계 대상입니다.";

        /// <summary>엔딩 E08. 생환은 분명하게 표시한다.</summary>
        public const string ExitConfirmed = "퇴실 확인 완료";

        /// <summary>엔딩 E08의 작은 상태 줄. 방금 닫은 문 안쪽에 남긴 존재를 가리킨다.</summary>
        public const string ExitRemaining = "현장 잔류 대상: 1";

        /// <summary>
        /// 일차별 업무 상태 문구(0번 칸 = 2일차). 1일차는 여기 없다 —
        /// 같은 안내를 별도 문자로 중복 발송하지 않는다(기획서 §4).
        /// </summary>
        private static readonly string[] Briefs =
        {
            "1회차 관측 기록이 접수되었습니다. 남은 관측 횟수는 4회입니다.",
            "이전 근무자의 관측 기록은 2건입니다. 퇴실 기록은 없습니다.",
            "현장 반응이 이전 기록과 일치하지 않습니다. 관측을 계속하십시오.",
            "최종 관측입니다. 04:00 이후 출입구에서 퇴실 절차가 진행됩니다."
        };

        /// <summary>
        /// 그날 시작 태블릿에 띄울 한 줄. 1일차이거나 범위를 벗어나면 빈 문자열이다
        /// (「문구가 없다」를 빈 문자열로 말하는 것은 이 코드베이스의 다른 표와 같은 약속이다).
        /// </summary>
        public static string BriefFor(int day)
        {
            int index = day - FirstBriefDay;
            if (index < 0 || index >= Briefs.Length)
            {
                return string.Empty;
            }

            return Briefs[index];
        }

        /// <summary>업무 상태 문구가 있는 마지막 일차.</summary>
        public static int LastBriefDay
        {
            get { return FirstBriefDay + Briefs.Length - 1; }
        }
    }
}

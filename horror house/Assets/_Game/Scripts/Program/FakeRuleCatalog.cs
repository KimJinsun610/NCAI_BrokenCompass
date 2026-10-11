namespace NightDuty
{
    /// <summary>
    /// 가짜 수칙(79차, 민: 「의미는 없지만 긴장감을 유도하는 문구 수칙들 — 2일차부터 하나씩」 · 「1, 9 채택」 · 「따로 더해」).
    /// 판정기·축·역설이 없는 <b>표시용 한 줄</b>이다 — 하루 수칙 수(<see cref="ProgramDirector.RulesFor"/>)에 세지 않고 태블릿 「근무 수칙」에만 끼운다.
    /// 2일차부터 5일차까지 하루에 하나. 80차(민: 가짜 수칙 4 추가 채택 — 다섯 줄에 나흘): 회차마다 시작 줄(<see cref="RunOffset"/>)을 돌려
    /// 채택 순서대로 이어 쓴다 — 회차마다 한 줄씩 빠지고 모든 줄이 고루 나온다. 시작 줄 0이면 채택 순서 그대로(시험).
    /// </summary>
    public static class FakeRuleCatalog
    {
        /// <summary>표시 카드 ID 접두어(판정 수칙 ID와 겹치지 않게).</summary>
        public const string IdPrefix = "F";

        /// <summary>처음 나오는 날.</summary>
        public const int FirstDay = 2;

        /// <summary>채택한 문구(민 2026-10-10: 후보 1·9 → 2·3일차, 80차 추가 3·7 → 4·5일차).</summary>
        public static readonly string[] Lines =
        {
            "근무는 개인입니다. 변수는 없습니다.",
            "퇴근 후 이 수칙서를 기억할 필요는 없습니다.",
            "어제의 근무자와 오늘의 근무자는 같은 사람입니다.",   // 80차 민 추가 채택(후보 3)
            "오늘 밤은 평소와 같습니다.",   // 80차 민 추가 채택(후보 7)
            "당신의 이름은 출근부에 이미 적혀 있습니다."   // 80차 민 추가 채택(후보 4)
        };

        /// <summary>마지막으로 나오는 날.</summary>
        public const int LastDay = 5;

        /// <summary>80차: 이번 회차의 시작 줄(0 ≤ 값 &lt; 줄 수). 새 회차마다 <c>NightRun.StartNewRun</c>이 고른다.</summary>
        public static int RunOffset { get; set; }

        private static int IndexFor(int day)
        {
            if (day < FirstDay || day > LastDay || Lines.Length == 0) return -1;
            int off = ((RunOffset % Lines.Length) + Lines.Length) % Lines.Length;
            return (off + day - FirstDay) % Lines.Length;
        }

        /// <summary>그날의 가짜 수칙. 없으면 null.</summary>
        public static string ForDay(int day)
        {
            int i = IndexFor(day);
            return i >= 0 ? Lines[i] : null;
        }

        /// <summary>그날 가짜 수칙의 표시 ID(「F1」…). 없으면 null.</summary>
        public static string IdForDay(int day)
        {
            int i = IndexFor(day);
            return i >= 0 ? IdPrefix + (i + 1) : null;   // 80차: 줄마다 고정 ID(F1~F5)
        }

        /// <summary>
        /// 태블릿 목록에서 끼울 자리(0 = 맨 앞). 맨 끝에만 두면 가짜라는 게 티가 나므로 날마다 다른 자리 — 공통 수칙(맨 끝 묶음) 앞까지.
        /// </summary>
        public static int SlotFor(int day, int cardCount)
        {
            if (cardCount <= 1) return 0;
            return (day * 3 + 1) % cardCount;
        }
    }
}

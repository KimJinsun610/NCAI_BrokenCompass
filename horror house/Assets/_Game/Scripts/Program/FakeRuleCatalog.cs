namespace NightDuty
{
    /// <summary>
    /// 가짜 수칙(79차, 민: 「의미는 없지만 긴장감을 유도하는 문구 수칙들」 · 「따로 더해」).
    /// 판정기·축·역설이 없는 <b>표시용 줄</b>이다 — 하루 수칙 수(<see cref="ProgramDirector.RulesFor"/>)에 세지 않고 태블릿 「근무 수칙」에만 끼운다.
    /// 103차(사용자: 「가짜 수칙은 첫날에 원래 나오던 수칙 4개 + 가짜 2개로 첫날에만」): 옛 79·80차는 2~5일차 하루 한 줄씩이었다 —
    /// 이제 1일차에만 두 줄(<see cref="PerDay"/>), 2~5일차의 그 자리는 진짜 수칙 한 장이 된다(<see cref="ProgramDirector.RulesFor"/> 6·6·6·7).
    /// 다섯 줄 중 회차마다 시작 줄(<see cref="RunOffset"/>)부터 두 줄을 이어 쓴다 — 회차마다 다른 두 줄이 고루 나온다. 시작 줄 0이면 채택 순서 그대로(시험).
    /// </summary>
    public static class FakeRuleCatalog
    {
        /// <summary>표시 카드 ID 접두어(판정 수칙 ID와 겹치지 않게).</summary>
        public const string IdPrefix = "115";   // DT 장식 문구 ID 대(115001~). 판정 수칙(110xxx)과 겹치지 않는다

        /// <summary>처음 나오는 날(103차: 2 → 1).</summary>
        public const int FirstDay = 1;

        /// <summary>마지막으로 나오는 날(103차: 5 → 1).</summary>
        public const int LastDay = 1;

        /// <summary>103차: 나오는 날 하루의 줄 수.</summary>
        public const int PerDay = 2;

        /// <summary>채택한 문구(민 2026-10-10: 후보 1·9, 80차 추가 3·7·4).</summary>
        public static readonly string[] Lines = LinesFromDesignData();

        /// <summary>DT 3단계: Rule_Table 장식 문구(115001~, 표 순서). 정본은 xlsx.</summary>
        private static string[] LinesFromDesignData()
        {
            var rows = ProgramCatalog.DecorRows();
            var lines = new string[rows.Count];
            _ids = new string[rows.Count];
            for (int i = 0; i < rows.Count; i++) { lines[i] = rows[i].Text; _ids[i] = rows[i].ID.ToString(); }
            return lines;
        }

        private static string[] _ids;

        /// <summary>80차: 이번 회차의 시작 줄(0 ≤ 값 &lt; 줄 수). 새 회차마다 <c>NightRun.StartNewRun</c>이 고른다.</summary>
        public static int RunOffset { get; set; }

        /// <summary>103차 QA 4회(사용자 결정): 이번 회차에 가짜 줄을 끼울 자리를 돌리는 값(0이면 옛 자리). 새 회차마다 <c>NightRun.StartNewRun</c>이 고른다.</summary>
        public static int SlotOffset { get; set; }

        /// <summary>그날 가짜 수칙 줄 수(나오지 않는 날은 0).</summary>
        public static int CountFor(int day)
        {
            var row = global::NightDuty.DesignData.DayExact(day);   // DT Day_Table.Fake_Rule_Count(1일차 2)
            if (row == null || Lines.Length == 0) return 0;
            return System.Math.Min((int)row.Fake_Rule_Count, Lines.Length);
        }

        private static int IndexFor(int day, int k)
        {
            if (k < 0 || k >= CountFor(day)) return -1;
            int off = ((RunOffset % Lines.Length) + Lines.Length) % Lines.Length;
            return (off + (day - FirstDay) * PerDay + k) % Lines.Length;
        }

        /// <summary>그날 k번째 가짜 수칙. 없으면 null.</summary>
        public static string ForDay(int day, int k = 0)
        {
            int i = IndexFor(day, k);
            return i >= 0 ? Lines[i] : null;
        }

        /// <summary>그날 k번째 가짜 수칙의 표시 ID(「F1」…). 없으면 null.</summary>
        public static string IdForDay(int day, int k = 0)
        {
            int i = IndexFor(day, k);
            return i >= 0 ? _ids[i] : null;   // 80차: 줄마다 고정 ID — DT 3단계: 표 ID(115001~, 옛 F1~F5)
        }

        /// <summary>
        /// 태블릿 목록에서 k번째 가짜 줄을 끼울 자리(0 = 맨 앞). 맨 끝에만 두면 가짜라는 게 티가 나므로 줄마다 다른 자리 — 지금 목록 길이 <paramref name="cardCount"/> 안.
        /// </summary>
        public static int SlotFor(int day, int cardCount, int k = 0)
        {
            if (cardCount <= 1) return 0;
            int off = ((SlotOffset % 997) + 997) % 997;
            if (k <= 0) return (day * 3 + 1 + off) % cardCount;   // 103차 QA 4회: 옛 — 1일차 덱이 늘 4장이라 첫 줄이 늘 가짜였다

            // 둘째 줄부터: 앞 줄이 들어간 자리에서 2칸 이상 떨어지게(앞뒤로 붙으면 가짜라는 게 티가 난다).
            int prev = SlotFor(day, cardCount - 1, k - 1);
            int step = 2 + off % System.Math.Max(1, cardCount - 3);
            return (prev + step) % cardCount;
        }
    }
}

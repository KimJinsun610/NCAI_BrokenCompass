using System.Collections.Generic;
using UnityEngine;

namespace NightDuty
{
    public static partial class NightRun
    {
        private static FinalRuleBook _finalBook;
        private static readonly List<RuleSO> _displayDeck = new List<RuleSO>();

        /// <summary>
        /// 오늘 새 수칙 판정 책(<see cref="ProgramEnabled"/>일 때 그날 편성 덱으로 만든다). 꺼져 있거나 밤 전이면 null.
        /// 재시작해도 새로 만들지 않고 스냅샷으로 되돌린다(방아쇠·위반 여부·결과 수).
        /// </summary>
        public static FinalRuleBook FinalRules
        {
            get { return _finalBook; }
        }

        /// <summary>오늘 새 수칙 정산 결과(없으면 빈 목록).</summary>
        public static IReadOnlyList<FinalRuleResult> FinalResults
        {
            get { return _finalBook != null ? _finalBook.Results : (IReadOnlyList<FinalRuleResult>)new FinalRuleResult[0]; }
        }

        /// <summary>그날 편성이 정해진 직후(재시작 제외) 새 판정 책을 만든다.</summary>
        private static void BeginFinalRules()
        {
            DisposeFinalRules();
            if (!ProgramEnabled || _program == null)
            {
                PlanCctvSpots();   // 71차: K-1 이상 사람 자리는 편성 없이도
                return;
            }

            _finalBook = new FinalRuleBook(_program.Deck, _axes);
            _finalBook.Settled += OnFinalSettled;
            _finalBook.ReverseReportArmed += OnReverseReportArmed;
            _finalBook.EncounterRequested += ReserveEncounter;
            BeginDirection();
            PlanCctvSpots();   // 71차: CCTV 사람 자리 — 디렉터가 그 채널을 볼 때 조우를 건다
            PlanParadox();
            BuildDisplayDeck();
        }

        private static void DisposeFinalRules()
        {
            DisposeDirection();
            DisposeParadox();
            ClearDisplayDeck();
            if (_finalBook == null) return;
            _finalBook.Settled -= OnFinalSettled;
            _finalBook.ReverseReportArmed -= OnReverseReportArmed;
            _finalBook.EncounterRequested -= ReserveEncounter;
            _finalBook = null;
        }

        private static void FinalDispatch(in JudgeSignal signal, bool judging)
        {
            if (_finalBook == null) return;
            _finalBook.Dispatch(signal, judging);
        }

        private static void FinalEndNight()
        {
            if (_finalBook == null) return;
            _finalBook.EndNight();
        }

        private static void OnFinalSettled(FinalRuleResult result)
        {
            if (result.Outcome == FinalOutcome.Violated)
            {
                ViolationMinutesToday.Add(CurrentMinute());   // 결과창 위반 시각
            }

            EventBus.RaiseFinalRuleSettled(result);
            ParadoxSettled(result);
            DutiesRuleSettled(result);   // 54차: 지시는 수칙 위반을 면책하지 않는다
        }

        private static void OnReverseReportArmed(string itemId)
        {
            SetReverseReport(itemId, true);

            // 51차: 목격한 칸의 변기는 그때 처음 점검에 오른다 — 순차 지시면 3초 뒤 단독 지시, 아니면 바로 푼다.
            InspectionItem item = InspectionCatalog.FindByTarget(itemId);
            string id = item != null ? item.Id : itemId;
            if (!Board.IsHeld(id)) return;
            if (_orders == null || !_orders.QueueWitness(id)) Board.Release(id);
        }

        /// <summary>점검판이 이미 델타를 준 결과를 새 수칙 기록에 남긴다(T4 역보고).</summary>
        private static void OnFinalInspectionReported(InspectionReport report)
        {
            if (_finalBook == null) return;
            switch (report.Outcome)
            {
                case ReportOutcome.ReverseKept:
                    _finalBook.NoteExternal(ProgramCatalog.ReverseReportRule, false, report.ItemId + " 역보고 준수");
                    break;
                case ReportOutcome.ReverseViolated:
                    _finalBook.NoteExternal(ProgramCatalog.ReverseReportRule, true, report.ItemId + " 역보고 위반");
                    ViolationMinutesToday.Add(CurrentMinute());
                    break;
            }
        }
        // ── 태블릿 표시용 카드 ──────────────────────────────────
        //
        // 태블릿(TabletDocument, 김진선님 코드)은 NightRun.TodayDeck의 RuleSO.PlayerText를 그대로 읽는다.
        // 그날 새 수칙을 「표시만 하는」 RuleSO로 만들어 TodayDeck으로 내보낸다. 태블릿 코드는 건드리지 않는다. 점검표는 메시지로 간다(32차, 아래).

        /// <summary>태블릿 「근무 수칙」에 내보내는 표시용 카드(새 수칙만 — 점검표는 메시지, <see cref="ChecklistMessage"/>). 새 편성이 꺼져 있으면 비어 있다.</summary>
        public static IReadOnlyList<RuleSO> DisplayDeck
        {
            get { return _displayDeck; }
        }

        private static void BuildDisplayDeck()
        {
            ClearDisplayDeck();
            if (!ProgramEnabled || _program == null) return;

            // 변조·검은 줄(10단계)은 태블릿에 보이는 글만 바꾼다 — 판정은 원본(def.Text)으로 한다.
            ParadoxPlan paradox = _paradox.Plan;
            foreach (RuleDef def in _program.Deck)
            {
                _displayDeck.Add(DisplayCard(def.Id, def.Space, RuleCardText(def, paradox), string.Empty));
            }

            // 5일차: 판정 없는 G3(빈칸)을 공통 수칙 뒤에 보인다 — 피날레에서 「당신」으로 채운다(FillFinaleBlank).
            if (Day >= FinaleWatch.Day)
            {
                RuleDef g3 = ProgramCatalog.Rule(ProgramCatalog.FinaleBlankRule);
                if (g3 != null) _displayDeck.Add(DisplayCard(g3.Id, g3.Space, g3.Text, string.Empty));
            }
        }

        // ── 점검 지시 문자 ──────────────────────────────────────
        //
        // 2026-10-04(32차, 민 요청): 점검표는 수칙 탭이 아니라 태블릿 <b>메시지</b>에 한 통으로 뜬다.
        // 코어는 본문만 만든다(<see cref="ChecklistMessage"/>) — 태블릿 메시지함에 넣고 고치는 것은 TabletBridge.

        /// <summary>점검 지시 문자의 ID(태블릿 메시지함). 같은 ID로 다시 넣으면 본문만 바뀌고 알람은 울리지 않는다.</summary>
        public const string ChecklistMessageId = "inspect.checklist";

        /// <summary>점검 조작 안내(점검 지시 문자 맨 아래).</summary>
        public const string InspectionHowTo = "가까이서 1초 본 뒤 길게 — Z 정상 / X 이상";   // 67차(문장은 짧게)

        /// <summary>조작 안내 색(김진선님 태블릿의 수칙 안내 색과 같다).</summary>
        public const string ChecklistHowToColor = "#7FA6B8";

        /// <summary>보고한 줄의 색(흐리게).</summary>
        public const string ChecklistDoneColor = "#5E6E77";

        /// <summary>
        /// 오늘 점검 지시 문자 본문(여러 줄, TMP 서식). 점검 편성이 없으면 빈 문자열.
        /// 보고하면 그 줄에 「· 보고함」(흐리게), 안전한 읽기로 드러나면 그 공간 줄에 「· 확인 필요」/「· 이상 없음」.
        /// </summary>
        public static string ChecklistMessage
        {
            get
            {
                InspectionPlan plan = Board.Plan;
                if (plan == null || plan.Assignments.Count == 0) return string.Empty;
                if (Board.DripMode) return string.Empty;   // 50차: 순차 지시면 지시 문자(OrderMessage)가 점검표를 대신한다

                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                sb.Append("[점검 지시] 오늘 확인할 항목 ").Append(plan.Assignments.Count).Append("개");
                for (int i = 0; i < plan.Assignments.Count; i++)
                {
                    InspectionAssignment a = plan.Assignments[i];
                    bool done = Board.StateOf(a.Id) != InspectionState.Pending;
                    sb.Append('\n');
                    if (done) sb.Append("<color=").Append(ChecklistDoneColor).Append('>');
                    sb.Append(i + 1).Append(". ").Append(InspectionLine(a));
                    if (done) sb.Append("</color>");
                }

                sb.Append("\n<color=").Append(ChecklistHowToColor).Append('>').Append(InspectionHowTo).Append("</color>");
                return sb.ToString();
            }
        }

        private static string InspectionLine(InspectionAssignment a)
        {
            string line = SpaceLabel(a.Item.Space) + " " + a.Item.Name + " — " + a.Item.TabletLine;
            if (a.IsLate) line += " (" + NightClock.Clock(NightClock.Call2) + "부터)";
            if (Board.StateOf(a.Id) != InspectionState.Pending) line += " · 보고함";
            else if (_unavoidable.Banned != SpaceId.None && SpaceIds.Canonical(a.Item.Space) == _unavoidable.Banned) line += " · 재입실 불가";
            else
            {
                SafeReadReveal? reveal = RevealOf(a.Item.Space);   // 안전한 읽기(10단계) — 공간 단위로 드러난다.
                if (reveal.HasValue) line += " · " + reveal.Value.Label;
            }

            return line;
        }

        private static string SpaceLabel(SpaceId s)
        {
            switch (SpaceIds.Canonical(s))
            {
                case SpaceId.Corridor: return "복도";
                case SpaceId.Classroom: return "교실";
                case SpaceId.ScienceRoom: return "과학실";
                case SpaceId.Toilet: return "화장실";
                case SpaceId.Library: return "도서관";
                case SpaceId.SecurityRoom: return "경비실";
                default: return string.Empty;
            }
        }

        private static RuleSO DisplayCard(string id, SpaceId space, string text, string howTo)
        {
            RuleSO card = ScriptableObject.CreateInstance<RuleSO>();
            card.name = "표시 " + id;
            card.Configure(new RuleSO.Config { CardId = id, Space = space, PlayerText = text, HowTo = howTo });
            return card;
        }

        private static void ClearDisplayDeck()
        {
            for (int i = 0; i < _displayDeck.Count; i++)
            {
                if (_displayDeck[i] == null) continue;
                if (Application.isPlaying) Object.Destroy(_displayDeck[i]);
                else Object.DestroyImmediate(_displayDeck[i]);
            }

            _displayDeck.Clear();
        }
    }
}

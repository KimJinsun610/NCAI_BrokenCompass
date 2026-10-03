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
            if (!ProgramEnabled || _program == null) return;

            _finalBook = new FinalRuleBook(_program.Deck, _axes);
            _finalBook.Settled += OnFinalSettled;
            _finalBook.ReverseReportArmed += OnReverseReportArmed;
            _finalBook.EncounterRequested += ReserveEncounter;
            BeginDirection();
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
        }

        private static void OnReverseReportArmed(string itemId)
        {
            SetReverseReport(itemId, true);
        }

        /// <summary>점검판이 이미 델타를 준 결과를 새 수칙 기록에 남긴다(G2 환청 기록, T4 역보고).</summary>
        private static void OnFinalInspectionReported(InspectionReport report)
        {
            RefreshDisplayDeck();
            if (_finalBook == null) return;
            switch (report.Outcome)
            {
                case ReportOutcome.HallucinationRecorded:
                    _finalBook.NoteExternal("G2", true, report.ItemId + " 환청 기록");
                    ViolationMinutesToday.Add(CurrentMinute());
                    break;
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
        // 그날 새 수칙 + 점검표를 「표시만 하는」 RuleSO로 만들어 TodayDeck으로 내보낸다. 태블릿 코드는 건드리지 않는다.

        /// <summary>태블릿에 내보내는 표시용 카드(새 수칙 → 점검). 새 편성이 꺼져 있으면 비어 있다.</summary>
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
                _displayDeck.Add(DisplayCard(def.Id, def.Space, paradox.DisplayTextOf(def.Id) ?? def.Text, string.Empty));
            }

            // 5일차: 판정 없는 G3(빈칸)을 공통 수칙 뒤에 보인다 — 피날레에서 「당신」으로 채운다(FillFinaleBlank).
            if (Day >= FinaleWatch.Day)
            {
                RuleDef g3 = ProgramCatalog.Rule(ProgramCatalog.FinaleBlankRule);
                if (g3 != null) _displayDeck.Add(DisplayCard(g3.Id, g3.Space, g3.Text, string.Empty));
            }

            InspectionPlan plan = Board.Plan;
            if (plan == null) return;
            for (int i = 0; i < plan.Assignments.Count; i++)
            {
                InspectionAssignment a = plan.Assignments[i];
                _displayDeck.Add(DisplayCard(a.Item.TargetId, a.Item.Space, InspectionLine(a), i == 0 ? InspectionHowTo : string.Empty));
            }
        }

        /// <summary>점검 줄의 「보고함」 표시를 다시 쓴다(보고·재시작 뒤). 태블릿은 열 때마다 다시 읽는다.</summary>
        private static void RefreshDisplayDeck()
        {
            InspectionPlan plan = Board.Plan;
            if (plan == null || _displayDeck.Count == 0) return;
            for (int i = 0; i < _displayDeck.Count; i++)
            {
                RuleSO card = _displayDeck[i];
                if (card == null) continue;
                InspectionItem item = InspectionCatalog.FindByTarget(card.CardId);
                if (item == null) continue;
                InspectionAssignment a = plan.Find(item.Id);
                if (a == null) continue;
                card.Configure(new RuleSO.Config { CardId = card.CardId, Space = item.Space, PlayerText = InspectionLine(a), HowTo = card.HowTo });
            }
        }

        /// <summary>점검 조작 안내(첫 점검 줄 아래에 한 번).</summary>
        public const string InspectionHowTo = "항목 2m 안에서 1초 바라본 뒤 길게 누르기 — Z 정상 / X 이상";

        private static string InspectionLine(InspectionAssignment a)
        {
            string line = "[점검] " + SpaceLabel(a.Item.Space) + " " + a.Item.Name + " — " + a.Item.TabletLine;
            if (a.IsLate) line += " (02:16부터)";
            if (Board.StateOf(a.Id) != InspectionState.Pending) line += " · 보고함";
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

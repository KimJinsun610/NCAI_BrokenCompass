using System;
using System.Collections.Generic;
using UnityEngine;

namespace NightDuty
{
    // 10단계 — 회피 불가 역설(최종 기획서 「회피 불가 역설」)과 위반 얼룩. 역설(NightRun.Paradox.cs)과 같은 편성기가 고른다.
    public static partial class NightRun
    {
        private static UnavoidableDef _plannedUnavoidable;
        private static int _emptyRoomChannel = -1;
        private static UnavoidableRun _unavoidable = new UnavoidableRun(null);
        private static readonly HashSet<string> _unavoidableBroken = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>오늘 회피 불가 역설 진행. 없으면 빈 진행(<see cref="UnavoidableRun.Def"/> = null).</summary>
        public static UnavoidableRun Unavoidable
        {
            get { return _unavoidable; }
        }

        /// <summary>그날의 빈 방 채널(0~4, K2). 밤 전이면 -1.</summary>
        public static int EmptyRoomChannel
        {
            get { return _emptyRoomChannel; }
        }

        /// <summary>진짜 회피 불가 역설로 당일 재입실이 금지된 공간. 없으면 None.</summary>
        public static SpaceId BannedSpace
        {
            get { return _unavoidable.Banned; }
        }

        /// <summary>
        /// 덱 편성 <b>전</b>(점검 편성 뒤) — 그날의 빈 방 채널과 회피 불가 역설을 고르고 쌍의 수칙을 강제 편성 목록으로 돌려준다.
        /// </summary>
        private static IEnumerable<string> PlanUnavoidable(InspectionPlan inspections)
        {
            _emptyRoomChannel = _paradoxPlanner.PickEmptyRoomChannel();
            _plannedUnavoidable = _paradoxPlanner.PlanUnavoidable(Day, _bands.Shown.GetBand(FearAxis.Trust), inspections);
            return _plannedUnavoidable != null ? _plannedUnavoidable.Rules : null;
        }

        /// <summary>덱 편성 뒤 — 쌍이 실제로 걸렸는지 확인한다(공간 충돌·조우 예산으로 빠졌으면 그날은 없음).</summary>
        private static void ConfirmUnavoidable()
        {
            if (_plannedUnavoidable == null) return;
            if (UnavoidableCatalog.Placed(_plannedUnavoidable, _program)) return;
            Debug.Log("[NightRun] 회피 불가 역설 " + _plannedUnavoidable + " 취소 — 쌍을 덱에 편성하지 못함");
            _plannedUnavoidable = null;
        }

        /// <summary>역설 편성(<see cref="PlanParadox"/>) 뒤 — 진행을 만들고 긴장 디렉터에 문을 건다.</summary>
        private static void BeginUnavoidable(ParadoxPlan plan)
        {
            _unavoidable = new UnavoidableRun(plan.Unavoidable);
            _unavoidableBroken.Clear();
            if (_tension == null) return;
            _tension.InspectionPendingIn = SpacePending;
            _tension.EmptyRoomChannel = plan.EmptyRoomChannel >= 0 ? "cctv.ch" + plan.EmptyRoomChannel : string.Empty;
            _tension.SetUnavoidable(plan.Unavoidable);
        }

        private static void DisposeUnavoidable()
        {
            _unavoidable = new UnavoidableRun(null);
            _unavoidableBroken.Clear();
        }

        private static void ResetUnavoidable()
        {
            _plannedUnavoidable = null;
            _emptyRoomChannel = -1;
            DisposeUnavoidable();
        }

        /// <summary>판정 신호 하나(역설 뒤). 판정 구간에만.</summary>
        private static void UnavoidableObserve(in JudgeSignal signal, bool judging)
        {
            if (!judging || IsCaptured || _unavoidable.Def == null) return;
            if (_sandbox && !_unavoidable.Staged) return;   // 71차: 흐름 정지 중에는 스스로 걸지 않는다
            UnavoidableApply(_unavoidable.Observe(signal, _currentSpace, IssuedSpacePending, IssuedItemPending));   // 50차: 지시받은 점검만
        }

        /// <summary>새 수칙 정산 — 진짜 회피 불가가 걸린 뒤 쌍의 수칙을 어기면 근무일지 「불가피」.</summary>
        private static void UnavoidableSettled(FinalRuleResult result)
        {
            UnavoidableDef def = _unavoidable.Def;
            if (def == null || !def.Real || !_unavoidable.Staged || result.Outcome != FinalOutcome.Violated || !def.Has(result.RuleId)) return;
            _unavoidableBroken.Add(result.RuleId);
        }

        private static void UnavoidableApply(UnavoidableStep step)
        {
            UnavoidableDef def = _unavoidable.Def;
            switch (step)
            {
                case UnavoidableStep.Staged:
                    _paradoxPlanner.MarkUnavoidable();
                    Debug.Log("[NightRun] 회피 불가 역설 " + def + ": " + def.Message);
                    EventBus.RaiseMessageSent(new ParadoxMessage("unavoidable." + def.Id, def.Rules[0], def.Space, def.Message, CurrentMinute()));
                    OrdersNoteMessage();
                    if (!string.IsNullOrEmpty(def.ChainEncounter) && _tension != null) _tension.ChainEncounter(def.ChainEncounter);
                    break;

                case UnavoidableStep.Banned:
                    Debug.Log("[NightRun] 회피 불가 역설 " + def + " — " + _unavoidable.Banned + " 당일 재입실 불가");
                    EventBus.RaiseTabletTextChanged();
                    break;
            }
        }

        /// <summary>그 공간에 아직 보고하지 않은 점검이 있는지.</summary>
        private static bool SpacePending(SpaceId space)
        {
            InspectionPlan plan = Board.Plan;
            if (plan == null) return false;
            SpaceId s = SpaceIds.Canonical(space);
            for (int i = 0; i < plan.Assignments.Count; i++)
            {
                InspectionAssignment a = plan.Assignments[i];
                if (SpaceIds.Canonical(a.Item.Space) == s && Board.StateOf(a.Id) == InspectionState.Pending) return true;
            }

            return false;
        }

        /// <summary>그 점검 항목이 오늘 배정됐고 아직 보고하지 않았는지.</summary>
        private static bool ItemPending(string itemId)
        {
            InspectionPlan plan = Board.Plan;
            InspectionAssignment a = plan != null && itemId != null ? plan.Find(itemId) : null;
            return a != null && Board.StateOf(a.Id) == InspectionState.Pending;
        }

        /// <summary>오늘 그 수칙을 진짜 회피 불가 역설 속에서 어겼는지(근무일지 「불가피」).</summary>
        public static bool UnavoidableBrokeFor(string ruleId)
        {
            return ruleId != null && _unavoidableBroken.Contains(ruleId);
        }

        /// <summary>디버그: 오늘 회피 불가 역설을 지금 건다(조건 무시). 편성이 없으면 false.</summary>
        public static bool DebugStageUnavoidable()
        {
            if (!_nightOpen || _unavoidable.Def == null || _unavoidable.Staged) return false;
            UnavoidableApply(_unavoidable.ForceStage());
            return true;
        }

        // ── 위반 얼룩 ──────────────────────────────────────────
        //
        // 최종 기획서 「위반 피드백」: 태블릿이 한 번 진동하고 어긴 수칙 줄에 얼룩이 번진다(내용은 가리지 않음).
        // 진동은 TabletBridge(FinalRuleSettled), 얼룩은 여기 — 표시 카드 글 뒤에 붉은 반투명 배경(TMP mark).

        /// <summary>위반 얼룩 색(TMP mark — 글 뒤에 깔린다).</summary>
        public const string ViolationStain = "#7A1E1E66";

        /// <summary>위반 얼룩을 보일지. 51차부터 끔(되살리려면 이 값만).</summary>
        public static bool ShowViolationStain = false;

        /// <summary>표시 카드 글을 다시 쓴다 — 변조·검은 줄 + 오늘 어긴 수칙의 얼룩. 판정은 그대로.</summary>
        private static void RefreshRuleCards()
        {
            if (_program == null || _displayDeck.Count == 0) return;
            ParadoxPlan paradox = _paradox.Plan;
            for (int i = 0; i < _displayDeck.Count; i++)
            {
                RuleSO card = _displayDeck[i];
                if (card == null) continue;
                RuleDef def = ProgramCatalog.Rule(card.CardId);
                if (def == null || !_program.Has(def.Id)) continue;
                string text = RuleCardText(def, paradox);
                if (card.PlayerText == text) continue;
                card.Configure(new RuleSO.Config { CardId = card.CardId, Space = def.Space, PlayerText = text, HowTo = card.HowTo });
            }
        }

        private static string RuleCardText(RuleDef def, ParadoxPlan paradox)
        {
            if (_voidRules.Contains(def.Id)) return string.Empty;   // 57차: 조우가 끝내 오지 않은 수칙은 태블릿에서 뺀다(빈 글 = 카드 숨김)
            string text = paradox.DisplayTextOf(def.Id) ?? def.Text;
            FinalJudge judge = _finalBook != null ? _finalBook.Judge(def.Id) : null;
            // 51차(민: 「태블릿에 수칙을 어긴 게 표시되지 않았으면」): 위반 얼룩을 쓰지 않는다. 위반은 현장 반응·몸·근무일지로만 안다.
            if (ShowViolationStain && judge != null && judge.Violated) text = "<mark=" + ViolationStain + ">" + text + "</mark>";
            return text;
        }
    }
}

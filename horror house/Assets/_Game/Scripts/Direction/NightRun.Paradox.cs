using System;
using System.Collections.Generic;
using UnityEngine;

namespace NightDuty
{
    // 10단계 — 역설과 변조(최종 기획서 「역설과 변조」). 편성은 밤 시작에 확정(재시작해도 같다), 판정은 언제나 수칙서 원본.
    public static partial class NightRun
    {
        private static ParadoxPlanner _paradoxPlanner = new ParadoxPlanner();
        private static ParadoxRun _paradox = new ParadoxRun(ParadoxPlan.None);
        private static readonly List<SafeReadReveal> _reveals = new List<SafeReadReveal>();

        /// <summary>
        /// 역설 편성기 씨앗(시험·재현용). 값이 있으면 회차 시작마다 그 씨앗으로 편성기를 만든다. 스위치를 비울 때 함께 비운다.
        /// </summary>
        public static int? ParadoxSeed { get; set; }

        /// <summary>회차 역설 편성기(본 수칙·회차 첫 역설 여부).</summary>
        public static ParadoxPlanner Paradoxes
        {
            get { return _paradoxPlanner; }
        }

        /// <summary>오늘 역설 진행. 편성이 꺼져 있거나 밤 전이면 빈 진행.</summary>
        public static ParadoxRun Paradox
        {
            get { return _paradox; }
        }

        /// <summary>오늘 안전한 읽기로 드러난 것(순서대로). 재시작해도 남는다.</summary>
        public static IReadOnlyList<SafeReadReveal> Reveals
        {
            get { return _reveals; }
        }

        /// <summary>그 공간에서 오늘 드러난 것. 없으면 null.</summary>
        public static SafeReadReveal? RevealOf(SpaceId space)
        {
            SpaceId s = SpaceIds.Canonical(space);
            for (int i = 0; i < _reveals.Count; i++)
            {
                if (_reveals[i].Space == s) return _reveals[i];
            }

            return null;
        }

        /// <summary>그날 덱이 정해진 직후(판정 책을 만든 뒤, 태블릿 카드를 만들기 전) 역설·변조를 편성한다.</summary>
        private static void PlanParadox()
        {
            _reveals.Clear();
            if (_finalBook == null || _program == null)
            {
                _paradox = new ParadoxRun(ParadoxPlan.None);
                DisposeUnavoidable();
                return;
            }

            InspectionPlan inspections = Board.Plan;
            Band trust = _bands.Shown.GetBand(FearAxis.Trust);
            ParadoxPlan plan = _paradoxPlanner.Plan(Day, trust, _program.Deck, s => SpaceHasAnomaly(inspections, s), _plannedUnavoidable, _emptyRoomChannel);
            _paradox = new ParadoxRun(plan);
            _paradoxAcked = false;
            BeginUnavoidable(plan);
            if (plan.Tampered != null)
            {
                _finalBook.SetKeepReward(plan.Tampered, Deltas.TrustVariantKept, "변조본을 보고도 원본대로 지킴");
            }

            Debug.Log("[NightRun] 역설 편성 — " + plan.Report);
        }

        /// <summary>역설 문자를 따라 그 수칙을 어겼을 때 오는 답장(51차).</summary>
        public const string ParadoxAck = "확인되었습니다.";

        private static bool _paradoxAcked;

        private static void DisposeParadox()
        {
            _paradoxAcked = false;
            _paradox = new ParadoxRun(ParadoxPlan.None);
            _reveals.Clear();
            DisposeUnavoidable();
        }

        private static void ResetParadox(bool clearSwitches)
        {
            if (clearSwitches) ParadoxSeed = null;
            _paradoxPlanner = ParadoxSeed.HasValue ? new ParadoxPlanner(new System.Random(ParadoxSeed.Value)) : new ParadoxPlanner();
            DisposeParadox();
        }

        /// <summary>판정 신호 하나(새 수칙 판정 뒤). 판정 구간에만 진행한다.</summary>
        private static void ParadoxObserve(in JudgeSignal signal, bool judging)
        {
            if (!judging || IsCaptured || _finalBook == null || _paradox.RuleId == null) return;
            if (_sandbox && !_paradox.Sent) return;   // 71차: 흐름 정지 중에는 스스로 보내지 않는다(보낸 뒤의 안전한 읽기는 센다)
            FinalJudge judge = _finalBook.Judge(_paradox.RuleId);
            ParadoxApply(_paradox.Observe(signal, _currentSpace, judge != null && judge.Violated));
        }

        /// <summary>새 수칙 정산 한 건(위협 대응 성공 = 「멈춰서」 안전한 읽기).</summary>
        private static void ParadoxSettled(FinalRuleResult result)
        {
            UnavoidableSettled(result);
            if (result.Outcome == FinalOutcome.Violated)
            {
                // 51차: 역설 문자를 따라 수칙을 어기면 짧은 답장 하나 — 「따르면 어기게 되는 문자」였다는 것을 늦게 깨닫게(위반 표시는 하지 않는다).
                if (_paradox.Sent && result.RuleId == _paradox.RuleId && !_paradoxAcked)
                {
                    _paradoxAcked = true;
                    EventBus.RaiseMessageSent(new ParadoxMessage("paradox.ack." + result.RuleId, result.RuleId, _paradox.Space, ParadoxAck, CurrentMinute()));
                }

                RefreshRuleCards();   // 위반 얼룩
                EventBus.RaiseTabletTextChanged();
                return;
            }

            if (result.Outcome != FinalOutcome.ThreatKept) return;
            ParadoxApply(_paradox.NoteThreatKept(result.RuleId));
        }

        /// <summary>재시작 — 판정 책 스냅샷이 방아쇠를 되돌려도 받은 문자는 남으므로 다시 표시한다.</summary>
        private static void ParadoxAfterRestore()
        {
            _paradox.ResetEpisode();
            _unavoidable.ResetEpisode();
            if (_paradox.Sent && _finalBook != null) _finalBook.MarkTriggered(_paradox.RuleId);
            RefreshRuleCards();   // 되돌린 위반의 얼룩을 지운다
        }

        private static void ParadoxApply(ParadoxStep step)
        {
            string id = _paradox.RuleId;
            switch (step)
            {
                case ParadoxStep.Sent:
                    _paradoxPlanner.MarkSent();
                    if (_finalBook != null)
                    {
                        _finalBook.MarkTriggered(id);
                        _finalBook.SetKeepReward(id, Deltas.TrustParadoxKept, "역설 문자를 받고도 지킴");
                    }

                    Debug.Log("[NightRun] 역설 문자 " + id + " (" + _paradox.Pattern + "): " + _paradox.Message);
                    EventBus.RaiseMessageSent(new ParadoxMessage("paradox." + id, id, _paradox.Space, _paradox.Message, CurrentMinute()));
            OrdersNoteMessage();
                    break;

                case ParadoxStep.SafeRead:
                    SafeReadReveal reveal = new SafeReadReveal(id, _paradox.Space, SpaceHasAnomaly(Board.Plan, _paradox.Space));
                    _reveals.Add(reveal);
                    Debug.Log("[NightRun] 안전한 읽기 " + id + " → " + reveal.Space + " " + reveal.Label);
                    EventBus.RaiseSafeReadConfirmed(reveal);
                    break;
            }
        }

        /// <summary>오늘 그 수칙을 겨눈 역설 문자를 받았는지(근무일지 「지시를 따름」).</summary>
        public static bool ParadoxSentFor(string ruleId)
        {
            return ruleId != null && _paradox.Sent && _paradox.RuleId == ruleId;
        }

        /// <summary>그 공간에 오늘 이상이 배정됐는지.</summary>
        private static bool SpaceHasAnomaly(InspectionPlan plan, SpaceId space)
        {
            if (plan == null) return false;
            SpaceId s = SpaceIds.Canonical(space);
            for (int i = 0; i < plan.Assignments.Count; i++)
            {
                InspectionAssignment a = plan.Assignments[i];
                if (a.IsAnomaly && SpaceIds.Canonical(a.Item.Space) == s) return true;
            }

            return false;
        }

        /// <summary>
        /// 디버그: 오늘 모호 역설을 그 수칙으로 바꾸고 곧바로 문자를 보낸다(발송 확률 무시). 덱에 없거나 문구가 없으면 false.
        /// 변조·검은 줄은 그대로 둔다. 에디터·디버그 빌드에서만 쓴다.
        /// </summary>
        public static bool DebugSendParadox(string ruleId)
        {
            ParadoxEntry e = ParadoxCatalog.Find(ruleId);
            if (!_nightOpen || _finalBook == null || e == null || !e.HasMessage || _finalBook.Judge(ruleId) == null) return false;
            ParadoxPlan old = _paradox.Plan;
            string tampered = old.Tampered == ruleId ? null : old.Tampered;
            string blacked = old.Blacked == ruleId ? null : old.Blacked;
            _paradox = new ParadoxRun(new ParadoxPlan(old.TrustBand, ruleId, false, true, tampered, tampered != null ? old.TamperText : null,
                blacked, blacked != null ? old.BlackedText : null, old.Report + " · 디버그 " + ruleId));
            ParadoxApply(_paradox.ForceSend());
            return true;
        }

        /// <summary>디버그: 지금 역설의 안전한 읽기를 마친 것으로 한다. 보낸 문자가 없으면 false.</summary>
        public static bool DebugSafeRead()
        {
            if (!_nightOpen || !_paradox.Sent || _paradox.SafeRead) return false;
            ParadoxApply(_paradox.ForceRead());
            return true;
        }
    }
}

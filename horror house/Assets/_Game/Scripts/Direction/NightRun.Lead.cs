using System;
using System.Collections.Generic;

namespace NightDuty
{
    // 85차 — 조우 앞 지시(민: 「L3는 문쪽 조우일 때만 · 도서관 입구 맞은편 블라인드 점검 지시 → 창문 조우(플레이어 쪽을 바라보다가, 2초 넘게 응시하면 도망감)
    // → 다음 날 biology 책 습득 or 도서관 순찰 → 도서관 문 조우」).
    // <list type="bullet">
    // <item><see cref="EncounterScript.AfterOrder"/>: 그 점검 항목을 편성에 넣고(없으면 정상으로, 늦은 항목이면 이르게) 묶어 두었다가 슬롯이 열리기 <see cref="LeadMinutes"/>분 앞에 단독 지시로 낸다.
    // 그 지시가 나가기 전에는 조우가 방아쇠를 받지 않는다(창밖 노란 얼굴 ← L-2 블라인드).</item>
    // <item><see cref="EncounterScript.AfterDuty"/>: 후보 중 하나를 그날 [근무 지시]로 정해(평소 지시에서 뺌) 같은 때에 낸다. 그 지시가 나가기 전에는 방아쇠를 받지 않는다(문간 노란 얼굴 ← W15·W2).</item>
    // <item>슬롯이 지나 판정 끝까지 넘어간(이월) 조우는 기다리지 않는다 — 앞선 지시가 끝내 못 나가도 조우는 온다.</item>
    // <item>창밖 노란 얼굴 응시: <see cref="FinalCues.WindowFaceTarget"/>을 이어서 <see cref="WindowFleeSeconds"/>초 보면 <see cref="WindowFaceFled"/> — 연출(WindowFace)이 달아나게 한다.</item>
    // </list>
    public static partial class NightRun
    {
        /// <summary>조우 앞 지시가 슬롯보다 이만큼(게임 분) 먼저 나간다(변기 지시와 같은 간격).</summary>
        public const float LeadMinutes = ToiletOrderLeadMinutes;

        /// <summary>창밖 노란 얼굴을 이만큼(초) 이어서 보면 달아난다(민: 「응시를 2초 이상 하면 도망감」).</summary>
        public const float WindowFleeSeconds = 2f;

        private static string _leadDuty;
        private static float _windowGaze;
        private static bool _windowFled;
        private static readonly Random s_leadRoll = new Random();

        /// <summary>오늘 문간 노란 얼굴 앞에 낼 [근무 지시](W15 또는 W2). 없으면 null.</summary>
        public static string LeadDuty
        {
            get { return _leadDuty; }
        }

        /// <summary>창밖 노란 얼굴을 2초 넘게 봐서 달아났는지(그 밤 — 재시작하면 다시).</summary>
        public static bool WindowFaceFled
        {
            get { return _windowFled; }
        }

        /// <summary>창밖 노란 얼굴을 이어서 본 초.</summary>
        public static float WindowGazeSeconds
        {
            get { return _windowGaze; }
        }

        // ── 점검 지시 ───────────────────────────────────────────

        /// <summary>편성 보정 — 조우 앞 점검 항목이 없으면 정상으로 더하고, 늦은 항목이면 이르게 한다. 바꿨으면 true.</summary>
        private static bool PatchLeadOrders(List<InspectionAssignment> rows, NightProgram program)
        {
            bool changed = false;
            foreach (SlotEncounter slot in program.Slots)
            {
                EncounterScript script = EncounterScripts.Find(slot.Encounter.Id);
                if (script == null || script.AfterOrder.Length == 0) continue;
                InspectionItem item = InspectionCatalog.Find(script.AfterOrder);
                if (item == null) continue;
                int at = rows.FindIndex(r => r.Id == item.Id);
                if (at < 0)
                {
                    rows.Add(new InspectionAssignment(item, false, Band.Band0, false));
                    changed = true;
                }
                else if (rows[at].IsLate)
                {
                    InspectionAssignment was = rows[at];
                    rows[at] = new InspectionAssignment(was.Item, was.IsAnomaly, was.Intensity, false);
                    changed = true;
                }
            }

            return changed;
        }

        /// <summary>조우 앞 점검 항목을 묶어 둔다(순차 지시일 때만 — 점검표 한 통이면 묶지 않는다).</summary>
        private static void HoldLeadOrders(InspectionPlan plan, NightProgram program)
        {
            if (!DripFor(plan)) return;
            foreach (SlotEncounter slot in program.Slots)
            {
                EncounterScript script = EncounterScripts.Find(slot.Encounter.Id);
                if (script != null && script.AfterOrder.Length > 0) Board.Hold(script.AfterOrder);
            }
        }

        /// <summary>묶어 둔 조우 앞 점검 항목을 슬롯 <see cref="LeadMinutes"/>분 앞에 단독 지시로 낸다(한 번).</summary>
        private static void ReleaseLeadOrders(float minute)
        {
            if (_orders == null || minute < 0f) return;
            if (_tension == null)
            {
                // 연출이 없는 밤(코어 시험) — 조우가 없으니 기다릴 까닭이 없다.
                if (_program == null) return;
                foreach (SlotEncounter slot in _program.Slots)
                {
                    EncounterScript script = EncounterScripts.Find(slot.Encounter.Id);
                    if (script != null && script.AfterOrder.Length > 0 && Board.IsHeld(script.AfterOrder)) _orders.QueueWitness(script.AfterOrder, 0f);
                }

                return;
            }

            foreach (EncounterRun r in _tension.Runs)
            {
                string item = r.Script != null ? r.Script.AfterOrder : string.Empty;
                if (item.Length == 0 || !Board.IsHeld(item)) continue;
                if (LeadDue(r, minute)) _orders.QueueWitness(item, 0f);
            }
        }

        private static bool LeadDue(EncounterRun r, float minute)
        {
            float from, to;
            TensionDirector.SlotWindow(r.Slot, out from, out to);
            return r.State != EncounterRunState.Waiting || r.CarriedOver || minute >= from - LeadMinutes;
        }

        // ── 근무 지시 ───────────────────────────────────────────

        /// <summary>밤 시작(재시작 제외) — 조우 앞 [근무 지시]를 하나 정하고 후보를 평소 지시에서 뺀다.</summary>
        private static void ChooseLeadDuty()
        {
            _leadDuty = null;
            if (_duties == null || _program == null) return;
            foreach (SlotEncounter slot in _program.Slots)
            {
                EncounterScript script = EncounterScripts.Find(slot.Encounter.Id);
                if (script == null || script.AfterDuty.Length == 0) continue;
                List<string> ok = new List<string>();
                for (int i = 0; i < script.AfterDuty.Length; i++)
                {
                    if (DutyCatalog.Find(script.AfterDuty[i]) == null) continue;
                    ok.Add(script.AfterDuty[i]);
                    _duties.Reserve(script.AfterDuty[i]);
                }

                if (ok.Count == 0) continue;
                _leadDuty = ok[s_leadRoll.Next(ok.Count)];
                UnityEngine.Debug.Log("[NightRun] 조우 앞 근무 지시 — " + slot.Encounter.Id + " ← " + _leadDuty);
                return;
            }
        }

        /// <summary>조우 앞 [근무 지시]를 슬롯 <see cref="LeadMinutes"/>분 앞에 낸다(진행 중인 다른 지시가 있으면 끝날 때까지 기다린다 — 그동안 새 평소 지시는 쉰다).</summary>
        private static void ReleaseLeadDuty(float minute)
        {
            if (!LeadDutyDue(minute) || _finale.Active || _duties.Active != null) return;
            DutyEvent ev;
            if (_duties.Force(_leadDuty, out ev)) AnnounceDuty(ev);
        }

        /// <summary>조우 앞 [근무 지시]를 낼 때가 됐고 아직 나가지 않았는지.</summary>
        private static bool LeadDutyDue(float minute)
        {
            if (_duties == null || _leadDuty == null || minute < 0f || _duties.WasIssued(_leadDuty)) return false;
            if (_tension == null) return true;
            foreach (EncounterRun r in _tension.Runs)
            {
                if (r.Script == null || r.Script.AfterDuty.Length == 0) continue;
                return LeadDue(r, minute);
            }

            return true;
        }

        /// <summary>조우 앞 [근무 지시]가 아직 나가지 않았고 판정 끝 전인지(조기 퇴근을 막는다).</summary>
        private static bool LeadDutyPending(float minute)
        {
            return _duties != null && _leadDuty != null && !_duties.WasIssued(_leadDuty) && minute >= 0f && minute < NightClock.JudgingEnd;
        }

        // ── 방아쇠 ──────────────────────────────────────────────

        /// <summary>그 조우의 앞선 지시가 나갔는지(<see cref="TensionDirector.EncounterGate"/>).</summary>
        private static bool LeadGateOpen(EncounterRun r)
        {
            EncounterScript s = r != null ? r.Script : null;
            if (s == null || r.CarriedOver) return true;
            if (s.AfterOrder.Length > 0 && _orders != null && Board.Plan != null && Board.Plan.Find(s.AfterOrder) != null && !Board.IsIssued(s.AfterOrder)) return false;
            if (s.AfterDuty.Length > 0 && _duties != null && _leadDuty != null && !_duties.WasIssued(_leadDuty)) return false;
            return true;
        }

        // ── 창밖 노란 얼굴 응시 ─────────────────────────────────

        private static void WindowGazeObserve(in JudgeSignal s)
        {
            if (s.Kind != SignalKind.GazeSample || _windowFled) return;
            if (s.TargetId != FinalCues.WindowFaceTarget)
            {
                _windowGaze = 0f;
                return;
            }

            _windowGaze += Math.Max(0f, s.Value);
            if (_windowGaze < WindowFleeSeconds) return;
            _windowFled = true;
            UnityEngine.Debug.Log("[NightRun] 창밖 노란 얼굴을 " + WindowFleeSeconds + "초 응시 — 달아남");
        }

        private static void ResetWindowGaze()
        {
            _windowGaze = 0f;
            _windowFled = false;
        }
    }
}

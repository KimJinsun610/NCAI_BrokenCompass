using System.Collections.Generic;

namespace NightDuty
{
    // 104차(사용자: 「이월은 없어. 의도한 날짜에 무조건 목격해야 해」 → 「유도 지시 + 시계 멈춤」) — 고정 편성 밤(TensionDirector.Sequential)의 조우 유도.
    // <list type="bullet">
    // <item>차례가 온 조우(TensionDirector.Head, 쉬는 시간 뒤)가 제 앞선 지시(변기 T-1 · 블라인드 L-2 · 사다리 C-3 · biology 책/도서관 순찰)를 갖고 있으면 그것을 먼저 낸다.
    // 그래도 <see cref="GuideGraceSeconds"/>초 안에 시작하지 않거나 앞선 지시가 없으면 [근무] 유도 문자(<see cref="GuideText"/>)를 보낸다 — 그때부터 앞선 지시 문은 열린 것으로 보고,
    // 디렉터는 <see cref="TensionDirector.SeqLenientSeconds"/>초 뒤 방아쇠를 「그 공간에 있기만 하면」으로 푼다.</item>
    // <item>조우가 시작되면 유도 문자에 취소선(완료). 유도 문자는 한 번만 보낸다(104차 후속 — 재촉 없음).</item>
    // <item>조우가 남은 채 03:30(<see cref="HoldMinute"/>)에 닿으면 밤 시계를 붙든다(<see cref="ClockHeld"/> — 구동기가 GameTime을 멈춘다). 조기 퇴근도 막혀 있다(<see cref="EncountersPending"/>).</item>
    // <item>104차 후속(사용자: 「처음 경비실에서 시작할 때 초기 수칙들을 읽을 시간이 필요하다」 → 「경비실 밖으로 나갈 때, 모든 밤에」): 밤마다 00:00에서
    // 경비실을 나설 때까지(최대 <see cref="StartHoldMaxSeconds"/>초) 밤 시계를 붙든다(<see cref="StartHeld"/>). 판정 시작(00:16)도 그만큼 늦어진다.</item>
    // </list>
    public static partial class NightRun
    {
        /// <summary>조우가 남았으면 이 시각(게임 분, 03:30)에서 밤 시계를 붙든다 — 판정 끝(03:32) 전이라 지시·판정은 그대로 돈다.</summary>
        public const float HoldMinute = NightClock.JudgingEnd - 2f;

        /// <summary>앞선 지시(그 공간 점검 등)가 있는 조우가 그 지시 뒤 이만큼(실제 초) 시작하지 않으면 유도 문자를 보낸다(마지막 수단).</summary>
        public const float GuideGraceSeconds = 45f;

        /// <summary>유도 문자의 카드 ID 머리(「guide.3」). 태블릿은 같은 카드를 다시 받으면 옛 문자를 지운다.</summary>
        public const string GuideCardPrefix = "guide.";

        private sealed class GuideState
        {
            public float OpenedAt = -1f;
            public float SentAt = -1f;
            public string CardId;
            public bool Struck;
        }

        private static readonly Dictionary<string, GuideState> s_guides = new Dictionary<string, GuideState>();

        /// <summary>
        /// 104차(사용자: 「해당 공간의 점검으로 자연스럽게 지시되도록 해줘 — 원래도 그러지 않았나?」): 순서대로 여는 밤에 조우마다 그 공간의 점검 항목 하나를 묶어 두었다가
        /// 그 조우 차례에 단독 지시로 낸다(옛 53차 「조우 방은 그 슬롯까지 아낀다」와 같은 생각 — 시간창 대신 차례). 유도 문자는 그래도 안 올 때의 마지막 수단.
        /// 조우 ID → 묶어 둔 점검 항목.
        /// </summary>
        private static readonly Dictionary<string, string> s_leadItems = new Dictionary<string, string>();

        /// <summary>그 조우가 제 앞선 지시(블라인드 L-2 · 변기 T-1 · 사다리 C-3 · biology 책/도서관 순찰)를 이미 갖는지 — 그러면 공간 점검을 따로 묶지 않는다.</summary>
        public static bool HasBuiltInLead(string encounterId, NightProgram program)
        {
            EncounterScript s = EncounterScripts.Find(encounterId);
            if (s == null) return false;
            if (s.AfterOrder.Length > 0 || s.AfterDuty.Length > 0) return true;
            if (encounterId == ProgramCatalog.ToiletGirl && program != null && program.Has(ProgramCatalog.ReverseReportRule)) return true;
            return encounterId == ProgramCatalog.CeilingLegs;
        }

        /// <summary>그 조우를 부를 점검 공간(CCTV 사람 = 경비실).</summary>
        private static SpaceId LeadSpaceOf(string encounterId)
        {
            EncounterScript s = EncounterScripts.Find(encounterId);
            return s != null ? SpaceIds.Canonical(s.Space) : SpaceId.None;
        }

        /// <summary>
        /// 편성 보정(순서대로 여는 밤) — 제 앞선 지시가 없는 조우마다 그 공간 점검이 하나씩 있도록 정상 항목을 더한다(복도 포함 — 같은 공간 조우가 여럿이면 여럿).
        /// </summary>
        private static bool PatchSequentialLeads(List<InspectionAssignment> rows, NightProgram program, bool reverse)
        {
            bool changed = false;
            Dictionary<SpaceId, int> need = new Dictionary<SpaceId, int>();
            foreach (string id in program.Sequence)
            {
                if (HasBuiltInLead(id, program)) continue;
                SpaceId space = LeadSpaceOf(id);
                if (space == SpaceId.None) continue;
                int n;
                need.TryGetValue(space, out n);
                need[space] = n + 1;
            }

            // 104차: 보고로 시작하는 조우(소년 = 교실 화분 C-1)는 그 항목이 꼭 있어야 한다.
            foreach (string id in program.Sequence)
            {
                EncounterScript es = EncounterScripts.Find(id);
                if (es == null || es.ReportTriggerItem.Length == 0 || rows.Exists(r => r.Id == es.ReportTriggerItem)) continue;
                InspectionItem item = InspectionCatalog.Find(es.ReportTriggerItem);
                if (item == null) continue;
                rows.Add(new InspectionAssignment(item, false, Band.Band0, false));
                changed = true;
            }

            foreach (KeyValuePair<SpaceId, int> kv in need)
            {
                int have = rows.FindAll(r => SpaceIds.Canonical(r.Item.Space) == kv.Key && !IsBuiltInLeadItem(r.Id, reverse)).Count;
                foreach (InspectionItem item in InspectionCatalog.InSpace(kv.Key))
                {
                    if (have >= kv.Value) break;
                    if (rows.Exists(r => r.Id == item.Id) || IsBuiltInLeadItem(item.Id, reverse) || (reverse && item.Id == "T-2")) continue;
                    rows.Add(new InspectionAssignment(item, false, Band.Band0, false));
                    have++;
                    changed = true;
                }
            }

            return changed;
        }

        private static bool IsBuiltInLeadItem(string itemId, bool reverse)
        {
            return itemId == "C-3" || itemId == "L-2" || (reverse && itemId == InspectionCatalog.ReverseReportItem);
        }

        /// <summary>밤 시작 — 순서대로 여는 밤에 조우마다 그 공간 점검 하나를 묶는다(CCTV 사람은 K-1을 먼저).</summary>
        private static void HoldSequentialLeads(InspectionPlan plan, NightProgram program)
        {
            s_leadItems.Clear();
            if (plan == null || program == null || !program.Sequential || !DripFor(plan)) return;
            bool reverse = program.Has(ProgramCatalog.ReverseReportRule);
            HashSet<string> used = new HashSet<string>();
            foreach (string id in program.Sequence)
            {
                if (HasBuiltInLead(id, program)) continue;
                SpaceId space = LeadSpaceOf(id);
                string pick = null;
                if (id == ProgramCatalog.CctvPerson && plan.Find(InspectionCatalog.FirstInspection) != null && !Board.IsHeld(InspectionCatalog.FirstInspection)) pick = InspectionCatalog.FirstInspection;
                EncounterScript es = EncounterScripts.Find(id);
                if (pick == null && es != null && es.ReportTriggerItem.Length > 0 && plan.Find(es.ReportTriggerItem) != null && !Board.IsHeld(es.ReportTriggerItem) && !used.Contains(es.ReportTriggerItem)) pick = es.ReportTriggerItem;   // 104차: 소년 ← 교실 화분
                for (int i = 0; pick == null && i < plan.Assignments.Count; i++)
                {
                    InspectionAssignment a = plan.Assignments[i];
                    if (SpaceIds.Canonical(a.Item.Space) != space || used.Contains(a.Id) || Board.IsHeld(a.Id) || IsBuiltInLeadItem(a.Id, reverse)) continue;
                    pick = a.Id;
                }

                if (pick == null || !Board.Hold(pick)) continue;
                used.Add(pick);
                s_leadItems[id] = pick;
            }

            if (s_leadItems.Count > 0)
            {
                List<string> shown = new List<string>();
                foreach (KeyValuePair<string, string> kv in s_leadItems) shown.Add(kv.Key + "←" + kv.Value);
                UnityEngine.Debug.Log("[NightRun] 조우 차례에 낼 점검 — " + string.Join(" · ", shown.ToArray()));
            }
        }
        private static int s_guideSerial;

        /// <summary>
        /// 고정 편성 밤에 밤 시계를 붙들어야 하는지 — 조우가 남았고 <see cref="HoldMinute"/>에 닿았다. 구동기(NightRunDriver)가 읽어 게임 시계를 멈춘다.
        /// </summary>
        public static bool ClockHeld
        {
            get
            {
                if (!IsNightActive || IsCaptured) return false;
                if (StartHeld) return true;
                if (_tension == null || !_tension.Sequential) return false;
                // 104차 검토: 자동 연출을 끈 개발자 모드·샌드박스·피날레에서는 조우가 새로 시작하지 않는다 — 붙들면 영영 안 풀린다.
                if (!DirectorAutoRun || _sandbox || _finale.Active) return false;
                int minute = CurrentMinute();
                return minute >= HoldMinute && EncountersPending;
            }
        }

        /// <summary>밤 시작 시계 붙듦의 상한(실제 초) — 경비실을 나서지 않아도 이만큼 지나면 근무가 시작된다.</summary>
        public const float StartHoldMaxSeconds = 90f;

        private static bool s_shiftStarted;
        private static float s_startHoldSeconds;

        /// <summary>
        /// 104차 후속: 밤 시작(00:00) 뒤 경비실을 아직 나서지 않았다 — 구동기가 밤 시계를 붙든다(수칙을 읽을 시간). 모든 밤에 건다.
        /// 체크포인트(00:00이 아닌 시각)로 재시작한 밤과 샌드박스에는 걸지 않는다.
        /// </summary>
        public static bool StartHeld
        {
            get { return IsNightActive && !IsCaptured && !s_shiftStarted && !_sandbox && CurrentMinute() < 1; }
        }

        /// <summary>근무가 시작됐는지(경비실을 나섰거나 상한이 지났다). 테스트·개발자 모드용.</summary>
        public static bool ShiftStarted { get { return s_shiftStarted; } }

        private static void ResetStartHold()
        {
            s_shiftStarted = false;
            s_startHoldSeconds = 0f;
        }

        /// <summary>경비실을 나서면(경비실 공간을 나가거나 다른 공간에 들어서면) 근무가 시작된다.</summary>
        private static void ObserveShiftStart(in JudgeSignal signal)
        {
            if (s_shiftStarted) return;
            bool left = (signal.Kind == SignalKind.SpaceExited && SpaceIds.Canonical(signal.Space) == SpaceId.SecurityRoom)
                     || (signal.Kind == SignalKind.SpaceEntered && signal.Space != SpaceId.None && SpaceIds.Canonical(signal.Space) != SpaceId.SecurityRoom);
            if (!left) return;
            s_shiftStarted = true;
            UnityEngine.Debug.Log("[NightRun] 경비실을 나섬 — 밤 시계 시작(" + s_startHoldSeconds.ToString("0.0") + "초 붙듦)");
        }

        private static void StartHoldTick(float realSeconds)
        {
            if (s_shiftStarted) return;
            s_startHoldSeconds += realSeconds;
            if (s_startHoldSeconds < StartHoldMaxSeconds) return;
            s_shiftStarted = true;
            UnityEngine.Debug.Log("[NightRun] 경비실에서 " + StartHoldMaxSeconds + "초가 지나 밤 시계 시작");
        }

        /// <summary>그 조우의 유도 문자(공간 이름만 — 무엇이 나오는지는 말하지 않는다).</summary>
        public static string GuideText(string encounterId)
        {
            switch (encounterId)
            {
                case ProgramCatalog.CeilingLegs: return "교실 안쪽 사다리를 다시 확인해주십시오.";
                case ProgramCatalog.ScienceBlackout: return "과학실을 순찰해주십시오.";
                case ProgramCatalog.ToiletBlackout: return "화장실을 순찰해주십시오.";
                case ProgramCatalog.ToiletGirl: return "화장실을 순찰해주십시오.";
                case ProgramCatalog.SuitMan: return "도서관 입구 맞은편 창의 블라인드를 확인해주십시오.";
                case ProgramCatalog.Footsteps: return "복도를 한 바퀴 순찰해주십시오.";
                case ProgramCatalog.CallingVoice: return "복도를 순찰해주십시오.";
                case ProgramCatalog.PeopleTree: return "복도를 순찰해주십시오.";
                case ProgramCatalog.CctvPerson: return "경비실 CCTV 화면을 확인해주십시오.";
                case ProgramCatalog.BoyBang: return "1-3 교실을 순찰해주십시오.";
                case ProgramCatalog.HallEndFigure: return "복도 끝 비상구 쪽을 확인해주십시오.";
                case ProgramCatalog.ModelRush: return "복도 끝 비상구 쪽을 확인해주십시오.";
                case ProgramCatalog.YellowFace: return "도서관을 순찰해주십시오.";
                default: return "순찰을 계속해주십시오.";
            }
        }

        /// <summary>그 조우에 앞선 지시(점검 항목·근무 지시)가 있는지 — 있으면 그것을 먼저 내고 유도 문자는 늦춘다.</summary>
        private static bool HasOwnLead(EncounterRun r)
        {
            if (r == null || r.Script == null) return false;
            if (r.Script.AfterOrder.Length > 0 || r.Script.AfterDuty.Length > 0) return true;
            if (s_leadItems.ContainsKey(r.Def.Id)) return true;   // 104차: 그 공간 점검
            if (r.Def.Id == ProgramCatalog.ToiletGirl && _program != null && _program.Has(ProgramCatalog.ReverseReportRule)) return true;
            return r.Def.Id == ProgramCatalog.CeilingLegs && SequentialCorpseLead;
        }

        /// <summary>
        /// 2026-10-29 김진선님(「공포 조우 이전에 CCTV 확인하라는 지시가 나와 중복」): CCTV 사람 차례에는 앞선 지시 K-1(「CCTV 모든 채널이 정상인지 확인」)이
        /// 이미 같은 말을 한다 — 그 지시가 나가 있고 아직 보고 전이면 「경비실 CCTV 화면을 확인」 유도 문자를 따로 내지 않는다.
        /// 보고를 마쳤는데도 조우가 오지 않았으면 그때 유도한다.
        /// </summary>
        private static bool LeadSaysSame(EncounterRun r)
        {
            if (r == null || r.Def.Id != ProgramCatalog.CctvPerson || Board == null) return false;
            string lead = LeadItemOf(r);
            if (string.IsNullOrEmpty(lead)) return false;
            return Board.IsIssued(lead) && !Board.IsHeld(lead) && Board.StateOf(lead) == InspectionState.Pending;
        }

        /// <summary>104차: 고정 편성 밤의 시체 낙하는 사다리 점검(C-3)을 묶어 두었다가 차례에 낸다.</summary>
        private static bool SequentialCorpseLead
        {
            get { return _program != null && _program.Sequential && _program.HasEncounter(ProgramCatalog.CeilingLegs); }
        }

        /// <summary>그 조우의 앞선 점검 항목(묶어 두었다가 차례에 내는 것). 없으면 빈 문자열.</summary>
        private static string LeadItemOf(EncounterRun r)
        {
            if (r == null || r.Script == null) return string.Empty;
            if (r.Script.AfterOrder.Length > 0) return r.Script.AfterOrder;
            if (r.Def.Id == ProgramCatalog.CeilingLegs && SequentialCorpseLead) return "C-3";
            string item;
            return s_leadItems.TryGetValue(r.Def.Id, out item) ? item : string.Empty;   // 104차: 그 공간 점검
        }

        private static void ResetGuides()
        {
            s_guides.Clear();
        }

        /// <summary>매 틱 — 차례가 온 조우를 유도한다(고정 편성 밤만).</summary>
        private static void GuideTick()
        {
            if (_tension == null || !_tension.Sequential) return;
            if (!DirectorAutoRun || _sandbox) return;   // 104차 검토: 자동 연출이 꺼지면 유도도 하지 않는다(GuidedAt이 먼저 서 방아쇠가 풀리지 않게)
            float now = _tension.Now;
            int minute = CurrentMinute();

            // 시작한 조우의 유도 문자는 지운다(취소선).
            foreach (EncounterRun r in _tension.Runs)
            {
                GuideState g;
                if (!s_guides.TryGetValue(r.Def.Id, out g) || g.Struck || g.CardId == null) continue;
                if (r.State == EncounterRunState.Waiting) continue;
                g.Struck = true;
                EventBus.RaiseDutySent(new ParadoxMessage(g.CardId + "." + DutyOutcome.Done, g.CardId, r.Script.Space, "-", minute));
            }

            EncounterRun head = _tension.Head;
            if (head == null || head.State != EncounterRunState.Waiting || !_tension.HeadOpen(head, minute)) return;

            GuideState state;
            if (!s_guides.TryGetValue(head.Def.Id, out state))
            {
                state = new GuideState();
                s_guides[head.Def.Id] = state;
            }

            if (state.OpenedAt < 0f)
            {
                state.OpenedAt = now;
                UnityEngine.Debug.Log("[NightRun] 조우 차례 — " + head.Def.Id + (HasOwnLead(head) ? " (앞선 지시 먼저)" : string.Empty));
            }

            bool ownLead = HasOwnLead(head);
            if (state.SentAt < 0f)
            {
                if (ownLead && now - state.OpenedAt < GuideGraceSeconds) return;
                if (LeadSaysSame(head)) return;
                SendGuide(head, state, minute);
                return;
            }

            // 104차(사용자: 「재촉 제거 — 재촉하는 걸 다 없애줘」): 유도 문자는 한 번만(옛: 75초마다 다시 보냈다).
        }

        private static void SendGuide(EncounterRun r, GuideState state, int minute)
        {
            // 다시 보낼 때는 같은 카드로 다시 낸다 — 태블릿이 옛 문자를 지우고 새로 넣는다(알람이 다시 울린다).
            // 104차 검토: 옛은 옛 문자에 취소선을 그어 끝난 지시처럼 보였다.
            if (state.CardId == null || state.Struck)
            {
                s_guideSerial++;
                state.CardId = GuideCardPrefix + s_guideSerial;
            }
            state.Struck = false;
            state.SentAt = _tension.Now;
            if (r.GuidedAt < 0f) r.GuidedAt = _tension.Now;
            string text = GuideText(r.Def.Id);
            UnityEngine.Debug.Log("[NightRun] 조우 유도 — " + r.Def.Id + " · " + text + " (기다림: " + r.Waiting + ")");
            EventBus.RaiseDutySent(new ParadoxMessage(state.CardId + "." + DutyOutcome.Issued, state.CardId, r.Script.Space, text, minute));
            if (_tension != null) _tension.Pacer.Impulse(PacerImpulse.Order);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Text;

namespace NightDuty
{
    // 50차 — 점검 순차 지시(InspectionDispatcher)와 긴장 조절기(TensionPacer)를 밤에 잇는다.
    public static partial class NightRun
    {
        private static InspectionDispatcher _orders;

        /// <summary>
        /// 점검을 순차 지시로 낼지(50차). 게임 구동기가 켠다. 꺼 두면(기본값·테스트) 점검표 한 통이 밤 시작에 전부 뜨고
        /// 늦은 공간만 호출 2에 열린다(옛 동작).
        /// </summary>
        public static bool InspectionDripEnabled { get; set; }

        /// <summary>오늘 점검 지시기. 순차 지시가 꺼져 있거나 점검이 없으면 null.</summary>
        public static InspectionDispatcher Orders
        {
            get { return _orders; }
        }

        /// <summary>점검 지시 문자 ID 앞부분(태블릿 메시지함). 뒤에 지시 번호가 붙는다.</summary>
        public const string OrderMessagePrefix = "inspect.order.";

        /// <summary>그 지시의 태블릿 메시지 ID.</summary>
        public static string OrderMessageId(int index)
        {
            return OrderMessagePrefix + index;
        }

        /// <summary>목격 뒤 지시(T4)의 칸 표시 — 여자아이가 들어가는 변기 점검 칸.</summary>
        public const string WitnessStallLabel = "3번 칸";

        /// <summary>
        /// 51차 — 편성된 조우·수칙이 요구하는 점검을 맞춘 편성을 돌려준다(바꿀 것이 없으면 같은 객체).
        /// <list type="bullet">
        /// <item>시체 낙하(<see cref="ProgramCatalog.CeilingLegs"/>): 사다리 C-3이 없으면 정상 항목으로 더한다(사다리 점검 중에 떨어진다).</item>
        /// <item>T4: 변기 T-1을 정상·이른 항목으로(없으면 더한다, 이상이었으면 정상으로 — 「변기는 정상입니다」), T-2는 뺀다.
        /// 다른 화장실 항목이 없으면 T-3(정상)을 더한다 — 화장실로 부르는 지시가 있어야 여자아이가 나온다(53차).</item>
        /// <item>방에서 터지는 조우: 그 방에 점검이 없으면 정상 항목 하나를 더한다(53차). 슬롯 A·C 조우 방은 <b>이른</b> 항목이 있어야 한다 —
        /// 늦은 항목뿐이면 이른 정상 항목을 더하고, 더할 것이 없으면 늦은 항목 하나를 이르게 한다(54차 QA).</item>
        /// </list>
        /// </summary>
        public static InspectionPlan PatchPlanForProgram(InspectionPlan plan, NightProgram program)
        {
            if (plan == null || program == null || plan.Count == 0) return plan;
            bool corpse = program.HasEncounter(ProgramCatalog.CeilingLegs);
            bool reverse = program.Has(ProgramCatalog.ReverseReportRule);

            List<InspectionAssignment> rows = new List<InspectionAssignment>(plan.Assignments);
            bool changed = false;
            if (corpse && plan.Find("C-3") == null)
            {
                rows.Add(new InspectionAssignment(InspectionCatalog.Find("C-3"), false, Band.Band0, false));
                changed = true;
            }

            if (reverse)
            {
                string t1 = InspectionCatalog.ReverseReportItem;
                int at = rows.FindIndex(r => r.Id == t1);
                if (at < 0)
                {
                    rows.Add(new InspectionAssignment(InspectionCatalog.Find(t1), false, Band.Band0, false));
                    changed = true;
                }
                else if (rows[at].IsAnomaly || rows[at].IsLate)
                {
                    rows[at] = new InspectionAssignment(rows[at].Item, false, Band.Band0, false);
                    changed = true;
                }

                if (rows.RemoveAll(r => r.Id == "T-2") > 0) changed = true;

                // 61차(민: 「변기 수칙 → 변기 지시 → 화장실 입장 → 소녀가 칸에 들어가는 것 목격 → 핏물·머리카락 → 정상 보고」):
                // 화장실로 부르는 것은 변기 지시(T-1) 자신이다 — 여자아이 슬롯이 열릴 때 단독 지시로 나간다(<see cref="ReleaseToiletOrder"/>).
                // 옛 53차의 「묶이지 않는 화장실 항목(T-3)」은 소녀보다 먼저 화장실로 불러 순서를 흐트러뜨려 뺐다.
                // 그날 다른 이유로 이미 있는 화장실 항목은 그대로 둔다.
            }

            // 53차 플레이 점검: 방에서 터지는 조우(소년·노란 얼굴·모형 …)는 그 방에 점검 지시가 하나도 없으면 플레이어가 갈 까닭이 없어
            // 조우가 열리지 않았다 — 그 방의 점검 하나(정상)를 더한다. 그 방이 늦은 공간이면 호출 2로. 복도는 늘 지나다니므로 뺀다.
            foreach (SlotEncounter slot in program.Slots)
            {
                EncounterScript script = EncounterScripts.Find(slot.Encounter.Id);
                if (script == null) continue;
                SpaceId room = SpaceIds.Canonical(script.Space);
                if (room == SpaceId.None || room == SpaceId.Corridor) continue;   // 경비실(CCTV 조우)은 K-1이 있어야 CCTV를 보러 온다
                if (reverse && slot.Encounter.Id == ProgramCatalog.ToiletGirl) continue;   // 61차: 변기 지시(T-1)가 소녀 슬롯에 맞춰 부른다

                // 54차 QA: 슬롯 A·C 조우 방이 늦은 공간이면 그 방 점검이 호출 2(02:16)에만 나와, A(01:00~01:52)에는 아무도 그 방에 가지 않고
                // C(03:08~)에는 이미 보고를 끝낸 뒤였다(3일차 화장실 여자아이가 슬롯 A · 화장실이 늦은 공간 — 조우가 열리지 않음).
                // A·C 방에는 이른 항목이 하나 있어야 지시기가 호출 1·슬롯 C까지 아껴 둔다. B는 호출 2와 함께 열리므로 늦은 항목으로 충분하다.
                bool needEarly = slot.Slot != EncounterSlot.B;
                bool lateRoom = SpaceIds.Canonical(plan.LateSpace) == room;
                if (rows.Exists(r => SpaceIds.Canonical(r.Item.Space) == room && !(reverse && r.Id == InspectionCatalog.ReverseReportItem) && !(needEarly && r.IsLate))) continue;
                bool added = false;
                foreach (InspectionItem item in InspectionCatalog.InSpace(room))
                {
                    if (rows.Exists(r => r.Id == item.Id) || (reverse && (item.Id == "T-2" || item.Id == InspectionCatalog.ReverseReportItem))) continue;
                    rows.Add(new InspectionAssignment(item, false, Band.Band0, lateRoom && !needEarly));
                    changed = true;
                    added = true;
                    break;
                }

                if (added || !needEarly) continue;

                // 더할 항목이 없으면(T4 날 화장실 — T-1 묶음 · T-2 뺌) 그 방의 늦은 항목 하나를 이르게 한다.
                int lateAt = rows.FindIndex(r => SpaceIds.Canonical(r.Item.Space) == room && r.IsLate && !(reverse && r.Id == InspectionCatalog.ReverseReportItem));
                if (lateAt < 0) continue;
                InspectionAssignment was = rows[lateAt];
                rows[lateAt] = new InspectionAssignment(was.Item, was.IsAnomaly, was.Intensity, false);
                changed = true;
            }

            return changed ? new InspectionPlan(plan.Day, rows, plan.LateSpace, plan.Call1ItemId) : plan;
        }

        /// <summary>
        /// T4 날 변기(T-1)는 묶어 둔다(지시·정산·총량에서 뺌). 61차: 목격이 아니라 여자아이 슬롯이 열릴 때(<see cref="ToiletOrderLeadMinutes"/>분 앞) 단독 지시로 푼다 —
        /// 「변기 지시 → 화장실 입장 → 소녀 목격 → 핏물·머리카락 → 정상 보고」(민). 소녀를 본 뒤의 역보고·핏물은 그대로(<c>ReverseReportJudge</c>).
        /// </summary>
        private static void HoldForProgram(InspectionPlan plan, NightProgram program)
        {
            if (plan == null || program == null || !program.Has(ProgramCatalog.ReverseReportRule)) return;
            Board.Hold(InspectionCatalog.ReverseReportItem);
        }

        private static bool DripFor(InspectionPlan plan)
        {
            return InspectionDripEnabled && plan != null && plan.Count > 0;
        }

        /// <summary>밤 편성 뒤(재시작 제외) 지시기를 만든다.</summary>
        private static void BeginOrders(InspectionPlan plan)
        {
            _orders = null;
            if (!DripFor(plan)) return;

            List<KeyValuePair<EncounterSlot, SpaceId>> encounters = new List<KeyValuePair<EncounterSlot, SpaceId>>();
            if (_program != null)
            {
                foreach (SlotEncounter s in _program.Slots)
                {
                    EncounterScript script = EncounterScripts.Find(s.Encounter.Id);
                    SpaceId space = script != null ? script.Space : SpaceId.None;
                    encounters.Add(new KeyValuePair<EncounterSlot, SpaceId>(s.Slot, space));
                }
            }

            _orders = new InspectionDispatcher(Board, Day, encounters);
        }

        private static void OrdersObserve(in JudgeSignal signal)
        {
            if (_orders != null) _orders.Observe(signal);
        }

        private static void OrdersTick(float realSeconds)
        {
            TickInstructionClock(realSeconds);
            if (_orders == null || IsCaptured || _sandbox) return;   // 71차: 흐름 정지 중에는 지시를 스스로 내지 않는다(버튼으로만)

            bool busy = _tension != null && _tension.Busy;
            float minute = CurrentMinute();
            DispatchInput input = new DispatchInput
            {
                Minute = minute,
                Dt = realSeconds,
                DirectorBusy = busy,
                Pacer = _tension != null ? _tension.Pacer : null,
                Banned = _unavoidable.Banned,
                DutyActive = _duties != null && _duties.Active != null && _duties.Active.Seconds > 0f,   // 제한 없는 지시(W2·W4·W5·W15)는 점검을 막지 않는다
                DutyTurn = DutyTurnNow(minute)
            };

            ReleaseToiletOrder(input.Minute);
            InspectionOrder order = _orders.Tick(input);
            if (order != null) AnnounceOrder(order);
            TickOrderLimits(realSeconds, busy);
        }

        // ── 67차: 점검 지시와 근무 지시를 번갈아(민: 「물품 점검이 비중이 더 높은데, 지시 사항과 비중이 균일했으면」) ──

        /// <summary>지난 지시가 무엇이었는지.</summary>
        private enum LastInstruction
        {
            None = 0,
            Inspection = 1,
            Duty = 2
        }

        private static LastInstruction _lastInstruction;
        private static float _lastInstructionAt;
        private static float _instructionClock;

        /// <summary>한쪽 차례를 이만큼(실제 초) 넘게 기다리면 다른 쪽도 낼 수 있다 — 한쪽이 낼 것이 없을 때 밤이 비지 않게.</summary>
        public const float TurnWaitMax = 45f;

        /// <summary>근무 지시 차례인지 — 지난 지시가 점검이었고, 근무 지시를 곧 낼 수 있고, 그 뒤 오래 기다리지 않았다.</summary>
        private static bool DutyTurnNow(float minute)
        {
            if (_duties == null || _lastInstruction != LastInstruction.Inspection) return false;
            if (_instructionClock - _lastInstructionAt > TurnWaitMax + DutyDispatcher.GapSeconds) return false;
            return _duties.CanIssueSoon(minute);
        }

        /// <summary>점검 지시 차례인지 — 지난 지시가 근무 지시였고 그 뒤 오래 기다리지 않았다(근무 지시기가 쉰다).</summary>
        private static bool InspectionTurnNow()
        {
            if (_orders == null || _lastInstruction != LastInstruction.Duty) return false;
            if (_instructionClock - _lastInstructionAt > TurnWaitMax) return false;
            return !_orders.AllIssued;
        }

        private static void NoteInstruction(bool duty)
        {
            _lastInstruction = duty ? LastInstruction.Duty : LastInstruction.Inspection;
            _lastInstructionAt = _instructionClock;
        }

        private static void TickInstructionClock(float realSeconds)
        {
            if (realSeconds > 0f) _instructionClock += realSeconds;
        }

        // ── 67차: 점검 지시 제한시간(민: 「제한시간이 존재하고, 명시되면 좋겠어」) ──

        private sealed class OrderLimit
        {
            public int Index;
            public float Seconds;
            public float IssuedMinute;
            public float Left;
            public bool Expired;
            public bool Stopped;
        }

        private static readonly Dictionary<int, OrderLimit> _orderLimits = new Dictionary<int, OrderLimit>();

        /// <summary>
        /// 그 지시의 제한시간(실제 초) — 67차 ②(민: 「시간 제한이 있는 모든 점검 지시는 120초로 고정」): 늘 <see cref="DutyCatalog.LimitSeconds"/>.
        /// 목격 뒤 변기 지시(T4)는 연출에 묶여 있어 제한이 없다(0). 조우 중에도 흐른다(태블릿 마감 시각과 맞게).
        /// </summary>
        public static float OrderLimitSeconds(InspectionOrder order)
        {
            if (order == null || order.Kind == OrderKind.Witness) return 0f;
            return DutyCatalog.LimitSeconds;
        }

        /// <summary>그 지시의 마감 「HH:MM까지」 — 제한이 없거나 모르면 빈 문자열.</summary>
        public static string OrderDeadlineText(int index)
        {
            OrderLimit l;
            if (!_orderLimits.TryGetValue(index, out l) || l.Seconds <= 0f || l.IssuedMinute < 0f) return string.Empty;
            return DutyCatalog.DeadlineText(l.IssuedMinute, l.Seconds);
        }

        /// <summary>그 지시의 제한시간(초) — 모르면 0.</summary>
        public static float OrderLimitOf(int index)
        {
            OrderLimit l;
            return _orderLimits.TryGetValue(index, out l) ? l.Seconds : 0f;
        }

        /// <summary>그 지시가 기한을 넘겼는지.</summary>
        public static bool OrderExpired(int index)
        {
            OrderLimit l;
            return _orderLimits.TryGetValue(index, out l) && l.Expired;
        }

        /// <summary>남은 제한시간(초) — 없거나 끝났으면 0.</summary>
        public static float OrderTimeLeft(int index)
        {
            OrderLimit l;
            return _orderLimits.TryGetValue(index, out l) && !l.Expired && !l.Stopped ? Math.Max(0f, l.Left) : 0f;
        }

        private static void StartOrderLimit(InspectionOrder order)
        {
            float seconds = OrderLimitSeconds(order);
            if (seconds <= 0f) return;   // 목격 뒤 변기 지시는 연출에 묶여 있어 기한을 두지 않는다
            _orderLimits[order.Index] = new OrderLimit { Index = order.Index, Seconds = seconds, Left = seconds, IssuedMinute = CurrentMinute() };
        }

        private static void TickOrderLimits(float realSeconds, bool busy)
        {
            if (_orderLimits.Count == 0 || realSeconds <= 0f || _orders == null) return;   // 67차 ②: 조우 중에도 흐른다(마감 시각과 맞게)
            bool changed = false;
            foreach (OrderLimit l in _orderLimits.Values)
            {
                if (l.Expired || l.Stopped) continue;
                InspectionOrder order = FindOrder(l.Index);
                if (order == null || !OrderPending(order))
                {
                    l.Stopped = true;
                    continue;   // 다 보고했다 — 기한은 그대로 멈춘다
                }

                l.Left -= realSeconds;
                if (l.Left > 0f) continue;
                l.Expired = true;
                changed = true;
                AddWarning(1, "order.late." + l.Index);
                UnityEngine.Debug.Log("[NightRun] 점검 지시 #" + l.Index + " 기한 지남(" + DutyCatalog.DeadlineText(l.IssuedMinute, l.Seconds) + ") — 경고 1");
            }

            if (changed) EventBus.RaiseTabletTextChanged();
        }

        private static InspectionOrder FindOrder(int index)
        {
            if (_orders == null) return null;
            for (int i = 0; i < _orders.Orders.Count; i++)
            {
                if (_orders.Orders[i].Index == index) return _orders.Orders[i];
            }

            return null;
        }

        private static bool OrderPending(InspectionOrder order)
        {
            for (int i = 0; i < order.ItemIds.Count; i++)
            {
                if (Board.StateOf(order.ItemIds[i]) == InspectionState.Pending) return true;
            }

            return false;
        }

        /// <summary>재시작 — 진행 중이던 기한은 멈추고(벌하지 않음) 번갈아 낼 차례도 비운다. 이미 넘긴 기한·경고는 스냅샷이 되돌린다.</summary>
        private static void OrdersAfterRestore()
        {
            List<int> drop = new List<int>();
            foreach (OrderLimit l in _orderLimits.Values)
            {
                if (FindOrder(l.Index) == null) drop.Add(l.Index);
                else if (!l.Expired) l.Stopped = true;
            }

            for (int i = 0; i < drop.Count; i++) _orderLimits.Remove(drop[i]);
            _lastInstruction = LastInstruction.None;
        }

        /// <summary>61차: 변기 지시가 여자아이 슬롯보다 이만큼(게임 분) 먼저 나간다 — 지시를 읽고 화장실에 들어서면 슬롯이 열려 있다.</summary>
        public const float ToiletOrderLeadMinutes = 5f;   // 67차(밤 05:00): 4 → 5

        /// <summary>61차: T4 날 묶어 둔 변기(T-1)를 여자아이 슬롯에 맞춰 단독 지시로 낸다(한 번).</summary>
        private static void ReleaseToiletOrder(float minute)
        {
            string t1 = InspectionCatalog.ReverseReportItem;
            if (_orders == null || minute < 0f || !Board.IsHeld(t1) || _program == null || !_program.Has(ProgramCatalog.ReverseReportRule)) return;

            bool due = true;   // 소녀가 편성에 없으면(있을 수 없지만) 곧바로
            if (_tension != null)
            {
                foreach (EncounterRun r in _tension.Runs)
                {
                    if (r.Def.Id != ProgramCatalog.ToiletGirl) continue;
                    float from, to;
                    TensionDirector.SlotWindow(r.Slot, out from, out to);
                    due = r.State != EncounterRunState.Waiting || r.CarriedOver || minute >= from - ToiletOrderLeadMinutes;
                    break;
                }
            }

            if (due) _orders.QueueWitness(t1, 0f);
        }

        private static void AnnounceOrder(InspectionOrder order)
        {
            StartOrderLimit(order);
            NoteInstruction(false);
            if (_tension != null) _tension.Pacer.Impulse(PacerImpulse.Order);
            UnityEngine.Debug.Log("[NightRun] 점검 지시 " + order + " — " + (_orders != null ? _orders.Waiting : string.Empty));
            EventBus.RaiseInspectionOrdered(order);
        }

        private static void OrdersNoteReport()
        {
            if (_orders != null) _orders.NoteReport();
        }

        private static void OrdersNoteMessage()
        {
            if (_orders != null) _orders.NoteMessage();
            DutiesNoteMessage();
        }

        private static void ResetOrders()
        {
            _orders = null;
            _orderLimits.Clear();
            _lastInstruction = LastInstruction.None;
            _lastInstructionAt = 0f;
            _instructionClock = 0f;
        }

        /// <summary>디버그: 다음 점검 지시를 지금 낸다(조건 무시). 낼 것이 없으면 false.</summary>
        public static bool DebugIssueOrder()
        {
            if (!_nightOpen || _orders == null) return false;
            InspectionOrder order = _orders.ForceNext(CurrentMinute(), _unavoidable.Banned);
            if (order == null) return false;
            AnnounceOrder(order);
            return true;
        }

        /// <summary>
        /// 점검 지시 문자 본문(TMP 서식). 머리 한 줄 + 항목마다 「· 이름 — 문구」. 보고하면 그 줄이 흐려지고 「· 보고함」,
        /// 재입실 불가·안전한 읽기 표시는 점검표와 같다. 첫 지시에만 조작 안내를 붙인다.
        /// </summary>
        public static string OrderMessage(InspectionOrder order)
        {
            if (order == null) return string.Empty;

            StringBuilder sb = new StringBuilder();
            string space = order.Space != SpaceId.None ? SpaceLabel(order.Space) : string.Empty;
            switch (order.Kind)
            {
                case OrderKind.Call1:
                case OrderKind.Call2:
                    // 51차: 점검 지시는 늘 「[점검 지시]」 — 역설 문자(머리 없음·「지금」)와 한눈에 갈리게.
                    sb.Append("[점검 지시] ").Append(space.Length > 0 ? space : "점검");
                    break;
                case OrderKind.CatchUp:
                    // 53차: 한 공간뿐이면(조우 슬롯까지 아껴 둔 방이 풀린 경우) 그 공간 이름 — 「남은 점검」은 여러 공간일 때만.
                    sb.Append("[점검 지시] ").Append(space.Length > 0 ? space : "남은 점검");
                    break;
                default:
                    sb.Append("[점검 지시] ").Append(space.Length > 0 ? space : "점검");
                    break;
            }

            // 67차 ②: 마감 시각을 머리 줄에 적는다(「02:08까지」). 넘기면 「기한 지남」.
            string due = OrderDeadlineText(order.Index);
            if (OrderExpired(order.Index)) sb.Append(" · <color=").Append(ChecklistLateColor).Append(">기한 지남</color>");
            else if (due.Length > 0) sb.Append(" · ").Append(due);

            for (int i = 0; i < order.ItemIds.Count; i++)
            {
                InspectionAssignment a = Board.Plan.Find(order.ItemIds[i]);
                if (a == null) continue;
                bool done = Board.StateOf(a.Id) != InspectionState.Pending;
                sb.Append('\n');
                if (done) sb.Append("<color=").Append(ChecklistDoneColor).Append('>');
                sb.Append("· ");
                if (order.Space == SpaceId.None) sb.Append(SpaceLabel(a.Item.Space)).Append(' ');
                sb.Append(a.Item.Name);
                if (order.Kind == OrderKind.Witness) sb.Append('(').Append(WitnessStallLabel).Append(')');
                sb.Append(" — ").Append(a.Item.TabletLine).Append(ItemStatus(a));
                if (done) sb.Append("</color>");
            }

            if (order.Index == 1)
            {
                sb.Append("\n<color=").Append(ChecklistHowToColor).Append('>').Append(InspectionHowTo).Append("</color>");
            }

            if (IsFinalOrder(order))
            {
                sb.Append("\n<color=").Append(ChecklistHowToColor).Append('>').Append(FinalOrderNotice).Append("</color>");
            }

            return sb.ToString();
        }

        /// <summary>마지막 지시에 붙는 안내(59차, 민: 「마지막 지시가 나오면 퇴근해도 된다는 안내문」). 67차(문장은 짧게): 줄임.</summary>
        public const string FinalOrderNotice = "마지막 지시입니다. 마치면 경비실 전화로 퇴근하십시오.";

        /// <summary>67차: 기한을 넘긴 지시의 「기한 지남」 색.</summary>
        public const string ChecklistLateColor = "#C8643C";

        /// <summary>
        /// 그 지시가 그날 마지막 지시인지 — 지시기가 낸 가장 최근 지시이고, 편성의 모든 항목이 지시받았다(묶어 둔 T-1이 남았으면 아직 아니다).
        /// </summary>
        public static bool IsFinalOrder(InspectionOrder order)
        {
            if (order == null || _orders == null || _orders.Orders.Count == 0) return false;
            if (!ReferenceEquals(_orders.Orders[_orders.Orders.Count - 1], order) && _orders.Orders[_orders.Orders.Count - 1].Index != order.Index) return false;
            InspectionPlan plan = Board.Plan;
            if (plan == null || plan.Count == 0) return false;
            for (int i = 0; i < plan.Assignments.Count; i++)
            {
                if (!Board.IsIssued(plan.Assignments[i].Id)) return false;
            }

            // 68차(민: 「점검이 끝나면 퇴근할 수 있는 상태가 되는데 이후에 추가 지시가 발생」): [근무 지시]가 남았으면 마지막 지시가 아니다 —
            // 모든 지시를 마치면 따로 「모든 지시를 마쳤습니다」 문자가 간다(ShiftReadyText).
            return !DutiesPending;
        }

        /// <summary>항목 줄 뒤 표시: 보고함 / 재입실 불가 / 안전한 읽기 결과.</summary>
        private static string ItemStatus(InspectionAssignment a)
        {
            if (Board.StateOf(a.Id) != InspectionState.Pending) return Board.StateOf(a.Id) == InspectionState.ReportedAnomaly ? " · [이상] 보고함" : " · [정상] 보고함";   // 60차: 정정할 수 있으니 무엇으로 보고했는지 보인다
            if (_unavoidable.Banned != SpaceId.None && SpaceIds.Canonical(a.Item.Space) == _unavoidable.Banned) return " · 재입실 불가";
            SafeReadReveal? reveal = RevealOf(a.Item.Space);
            return reveal.HasValue ? " · " + reveal.Value.Label : string.Empty;
        }

        /// <summary>그 공간에 지시받았고 아직 보고하지 않은 점검이 있는지(회피 불가 역설은 지시받은 점검만 걸고 넘어진다).</summary>
        private static bool IssuedSpacePending(SpaceId space)
        {
            InspectionPlan plan = Board.Plan;
            if (plan == null) return false;
            SpaceId s = SpaceIds.Canonical(space);
            for (int i = 0; i < plan.Assignments.Count; i++)
            {
                InspectionAssignment a = plan.Assignments[i];
                if (SpaceIds.Canonical(a.Item.Space) == s && Board.IsIssued(a.Id) && Board.StateOf(a.Id) == InspectionState.Pending) return true;
            }

            return false;
        }

        /// <summary>그 항목이 지시받았고 아직 보고하지 않았는지.</summary>
        private static bool IssuedItemPending(string itemId)
        {
            return ItemPending(itemId) && Board.IsIssued(itemId);
        }
    }
}

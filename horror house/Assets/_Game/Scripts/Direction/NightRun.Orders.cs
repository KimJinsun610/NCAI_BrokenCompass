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
        /// <item>T4: 변기 T-1을 정상·이른 항목으로(없으면 더한다, 이상이었으면 정상으로 — 「변기는 정상입니다」), T-2는 뺀다.</item>
        /// </list>
        /// </summary>
        public static InspectionPlan PatchPlanForProgram(InspectionPlan plan, NightProgram program)
        {
            if (plan == null || program == null || plan.Count == 0) return plan;
            bool corpse = program.HasEncounter(ProgramCatalog.CeilingLegs);
            bool reverse = program.Has(ProgramCatalog.ReverseReportRule);
            if (!corpse && !reverse) return plan;

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
            }

            return changed ? new InspectionPlan(plan.Day, rows, plan.LateSpace, plan.Call1ItemId) : plan;
        }

        /// <summary>T4 날 변기(T-1)는 여자아이를 목격할 때까지 묶어 둔다(지시·정산·총량에서 뺌).</summary>
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
            if (_orders == null || IsCaptured) return;

            DispatchInput input = new DispatchInput
            {
                Minute = CurrentMinute(),
                Dt = realSeconds,
                DirectorBusy = _tension != null && _tension.Busy,
                Pacer = _tension != null ? _tension.Pacer : null,
                Banned = _unavoidable.Banned
            };

            InspectionOrder order = _orders.Tick(input);
            if (order != null) AnnounceOrder(order);
        }

        private static void AnnounceOrder(InspectionOrder order)
        {
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
        }

        private static void ResetOrders()
        {
            _orders = null;
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
                    sb.Append("[점검 지시] 남은 점검");
                    break;
                default:
                    sb.Append("[점검 지시] ").Append(space.Length > 0 ? space : "점검");
                    break;
            }

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

            return sb.ToString();
        }

        /// <summary>항목 줄 뒤 표시: 보고함 / 재입실 불가 / 안전한 읽기 결과.</summary>
        private static string ItemStatus(InspectionAssignment a)
        {
            if (Board.StateOf(a.Id) != InspectionState.Pending) return " · 보고함";
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

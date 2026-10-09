using UnityEngine;

namespace NightDuty
{
    // 54차 — [근무 지시](3차 회의 제안서 1차 묶음). 점검(보고 판단한다) 사이에 걷고·닫고·끄고·적는 업무를 섞는다.
    public static partial class NightRun
    {
        private static DutyDispatcher _duties;

        /// <summary>[근무 지시]를 낼지. 구동기(<c>NightRunDriver</c>)가 켠다. 옛 테스트를 위해 코어 기본값은 꺼짐.</summary>
        public static bool DutiesEnabled { get; set; }

        /// <summary>CCTV 채널 수(W1 순회). 연출 쪽이 씬의 CCTV에서 맞춘다.</summary>
        public static int DutyCctvChannels { get; set; } = 5;

        /// <summary>오늘 [근무 지시] 지시기. 꺼져 있거나 밤 전이면 null.</summary>
        public static DutyDispatcher Duties
        {
            get { return _duties; }
        }

        /// <summary>68차: 오늘 처리할 [근무 지시]가 남았는지(<see cref="DutyDispatcher.Pending"/>). [근무 지시]가 꺼져 있으면 false.</summary>
        public static bool DutiesPending
        {
            get { return _duties != null && _duties.Pending(CurrentMinute()); }
        }

        /// <summary>68차: 모든 지시를 마쳐 조기 퇴근할 수 있게 된 순간 한 번 보내는 문자.</summary>
        public const string ShiftReadyText = "[근무] 모든 지시를 마쳤습니다. 경비실 전화로 퇴근하십시오.";

        /// <summary>68차: <see cref="ShiftReadyText"/> 문자의 카드 ID(재시작이 되돌리면 태블릿에서 지운다 — 끝난 지시가 아니므로).</summary>
        public const string ShiftReadyId = "shift.ready";

        private static bool _shiftReadyAnnounced;

        /// <summary>68차: 조기 퇴근이 막 가능해졌으면 한 번 알린다. 다시 막히면(새 지시가 나오면) 다음에 또 알린다.</summary>
        private static void ShiftReadyTick()
        {
            bool ready = CanEndShiftEarly;
            if (!ready)
            {
                _shiftReadyAnnounced = false;
                return;
            }

            if (_shiftReadyAnnounced) return;
            _shiftReadyAnnounced = true;
            if (_duties == null) return;   // [근무 지시]가 없는 밤(옛 시험)은 점검 지시의 「마지막 지시」 안내로 충분하다
            EventBus.RaiseDutySent(new ParadoxMessage(ShiftReadyId, ShiftReadyId, SpaceId.SecurityRoom, ShiftReadyText, CurrentMinute()));
        }

        /// <summary>밤 편성 뒤(재시작 제외) 지시기를 만든다.</summary>
        private static void BeginDuties()
        {
            _shiftReadyAnnounced = false;
            // 57차: 그날 점검 대상은 비품 확인 지시로 내지 않고, 가까운 셋 중 하나를 고른다.
            _duties = DutiesEnabled ? new DutyDispatcher(Day, DutyCost, DutyCctvChannels, item => Board.Plan != null && Board.Plan.Find(item) != null, new System.Random()) : null;
        }

        private static float DutyCost(SpaceId space)
        {
            return _orders != null ? _orders.Cost(space) : 0f;
        }

        private static void DutiesTick(float realSeconds)
        {
            if (_duties == null || IsCaptured || _sandbox) return;   // 71차: 흐름 정지 — 스스로 내지 않고 시간 초과도 멈춘다
            DutyInput input = new DutyInput
            {
                Minute = CurrentMinute(),
                Dt = realSeconds,
                DirectorBusy = _tension != null && _tension.Busy,
                InspectionBacklog = Board.IssuedPendingCount,
                SinceInspection = _orders != null ? _orders.SinceActivity : float.PositiveInfinity,
                Finale = _finale.Active,
                InspectionTurn = InspectionTurnNow()
            };

            DutyEvent ev;
            if (_duties.Tick(input, out ev)) AnnounceDuty(ev);
        }

        private static void DutiesObserve(in JudgeSignal signal)
        {
            if (_duties == null || IsCaptured) return;
            DutyEvent ev;
            if (_duties.Observe(signal, out ev)) AnnounceDuty(ev);
        }

        private static void DutiesNoteSigned()
        {
            if (_duties == null) return;
            DutyEvent ev;
            if (_duties.NoteSigned(out ev)) AnnounceDuty(ev);
        }

        /// <summary>65차: 줍기 지시의 물건을 주웠다. 그 지시를 끝냈으면 true(완료 답장·축 −3).</summary>
        public static bool PickUpDutyItem(string target)
        {
            if (!_nightOpen || _duties == null || IsCaptured) return false;
            DutyEvent ev;
            if (!_duties.NotePicked(target, out ev)) return false;
            AnnounceDuty(ev);
            return true;
        }

        /// <summary>65차: 지금 그 물건을 줍기를 바라는 지시가 진행 중인지.</summary>
        public static bool DutyWants(string target)
        {
            return _nightOpen && _duties != null && !IsCaptured && _duties.Wants(target);
        }

        private static void DutiesRuleSettled(FinalRuleResult result)
        {
            if (_duties != null && result.Outcome == FinalOutcome.Violated) _duties.NoteViolation();
        }

        private static void DutiesNoteMessage()
        {
            if (_duties != null) _duties.NoteMessage();
        }

        /// <summary>디버그: 그 지시를 지금 낸다. 진행 중인 지시가 있거나 꺼져 있으면 false.</summary>
        public static bool DebugIssueDuty(string id)
        {
            if (!_nightOpen || _duties == null) return false;
            DutyEvent ev;
            if (!_duties.Force(id, out ev)) return false;
            AnnounceDuty(ev);
            return true;
        }

        /// <summary>
        /// 재시작 뒤에도 그 지시의 문자(지시·답장)가 태블릿에 남아야 하는지 — 되돌린 스냅샷에서 끝난(완료·미완료) 지시만 남는다.
        /// 54차 QA: 붙잡혀 밤 시작으로 돌아가도 진행 중이던 지시(「도서관 순찰 바랍니다」 — 되돌려 사라짐)와
        /// 되돌린 완료 답장(「문 정리가 기록되었습니다」 — 그 지시가 다시 나올 수 있음)이 태블릿에 남았다. 점검 지시 문자를 되돌리는 것과 같게 한다.
        /// </summary>
        public static bool DutyRecordStands(string dutyId)
        {
            return _duties != null && !string.IsNullOrEmpty(dutyId) && _duties.Finished(dutyId);
        }

        /// <summary>
        /// 사건 처리 — 문자(<see cref="EventBus.DutySent"/>)·완료 축 −3·미완료 경고 1. 지시가 나가면 긴장 조절기에 점검 지시와 같은 자극을 준다.
        /// </summary>
        private static void AnnounceDuty(DutyEvent ev)
        {
            DutyDef d = ev.Def;
            if (d == null) return;
            int minute = CurrentMinute();
            string text;
            switch (ev.Outcome)
            {
                case DutyOutcome.Issued:
                    text = DutyCatalog.MessageText(d, Day, minute);   // 67차: 마감 「HH:MM까지」
                    if (d.Kind != DutyKind.LogSign) NoteInstruction(true);   // 67차: 점검 지시와 번갈아
                    if (_tension != null) _tension.Pacer.Impulse(PacerImpulse.Order);
                    break;
                case DutyOutcome.Done:
                    if (!ev.Tainted && _axes != null) _axes.Lower(d.Axis, DutyCatalog.Relief, "duty." + d.Id);
                    text = DutyCatalog.DoneText(d, Day, minute);
                    break;
                default:
                    AddWarning(1, "duty." + d.Id);
                    text = DutyCatalog.MissedText(d);
                    break;
            }

            Debug.Log("[NightRun] 근무 지시 " + ev + " — " + text);
            EventBus.RaiseDutySent(new ParadoxMessage("duty." + d.Id + "." + ev.Outcome, d.Id, d.Space, text, minute));
        }

        private static void ResetDuties()
        {
            _duties = null;
            DutiesEnabled = false;
            DutyCctvChannels = 5;
        }
    }
}

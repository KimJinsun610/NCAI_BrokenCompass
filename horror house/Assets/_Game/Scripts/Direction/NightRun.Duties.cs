using UnityEngine;

namespace NightDuty
{
    // 54차 — [근무 지시](3차 회의 제안서 1차 묶음). 점검(보고 판단한다) 사이에 걷고·닫고·끄고·적는 업무를 섞는다.
    public static partial class NightRun
    {
        private static DutyDispatcher _duties;
        private static readonly System.Collections.Generic.HashSet<string> _lastNightDuties = new System.Collections.Generic.HashSet<string>();   // 103차
        private static bool _dutyCarry;   // 103차: 이 회차에 지난 밤 지시기가 있는지(새 회차면 거르지 않는다)

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
            get { return _duties != null && (_duties.Pending(CurrentMinute()) || LeadDutyPending(CurrentMinute())); }   // 85차: 조우 앞 지시가 아직이면 남은 것
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
            _sinceStallNag = float.PositiveInfinity;   // 103차 후속
            _dutyTrustTonight = 0;   // 103차
            System.Array.Clear(_dutyReliefTonight, 0, _dutyReliefTonight.Length);
            // 103차: 지난 밤에 낸 지시는 오늘 다른 지시가 남아 있으면 내지 않는다.
            _lastNightDuties.Clear();
            if (_dutyCarry && _duties != null) foreach (string id in _duties.IssuedIds) _lastNightDuties.Add(id);
            // 57차: 그날 점검 대상은 비품 확인 지시로 내지 않고, 가까운 셋 중 하나를 고른다.
            _duties = DutiesEnabled ? new DutyDispatcher(Day, DutyCost, DutyCctvChannels, item => Board.Plan != null && Board.Plan.Find(item) != null, new System.Random()) : null;
            if (_duties != null) _duties.RecentlyIssued = id => _lastNightDuties.Contains(id);
            _dutyCarry = true;
            ChooseLeadDuty();   // 85차: 문간 노란 얼굴 앞 지시(W15·W2 중 하나)
        }

        private static float DutyCost(SpaceId space)
        {
            return _orders != null ? _orders.Cost(space) : 0f;
        }

        private static void DutiesTick(float realSeconds)
        {
            if (_duties == null || IsCaptured || _sandbox) return;   // 71차: 흐름 정지 — 스스로 내지 않고 시간 초과도 멈춘다
            int backlog = Board.IssuedPendingCountExcept(_unavoidable.Banned);
            bool stalled = backlog > 0 && !_finale.Active && InspectionsStalled;   // 103차 후속(사용자 7번): 지시받은 점검을 너무 미루면 독촉 문자 + 다음 근무 지시
            NagStalledInspections(realSeconds, stalled, backlog);
            DutyInput input = new DutyInput
            {
                Minute = CurrentMinute(),
                Dt = realSeconds,
                DirectorBusy = _tension != null && _tension.Busy,
                InspectionBacklog = stalled ? 0 : backlog,   // 103차 QA: 재입실 불가 공간의 항목은 밀린 점검이 아니다 · 103차 후속: 오래 미루면 기다리지 않는다
                SinceInspection = stalled ? float.PositiveInfinity : (_orders != null ? _orders.SinceActivity : float.PositiveInfinity),
                Finale = _finale.Active,
                InspectionTurn = !stalled && InspectionTurnNow(),   // 103차 후속: 오래 미루면 점검 차례를 기다리지 않는다
                HoldNew = LeadDutyDue(CurrentMinute()) || (!stalled && _orders != null && _orders.SpaceUnfinished)   // 85차 · 103차: 점검 공간을 다 끝낼 때까지 · 103차 후속: 오래 미루면 기다리지 않는다
            };

            ReleaseLeadDuty(input.Minute);   // 85차
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
            if (_tension != null) _tension.NoteReported(target);   // 104차: biology 책을 집는 순간 노란 얼굴이 문간에 선다
            return true;
        }

        /// <summary>80차: 출입 카드 지시(W16·W17)의 카드를 집었다. 그 카드를 바라는 지시가 진행 중이면 true.</summary>
        public static bool TakeDutyCard(string card)
        {
            return _nightOpen && _duties != null && !IsCaptured && _duties.NoteCardTaken(card);
        }

        /// <summary>80차: 들고 있던 출입 카드를 경비실 열쇠함에 넣었다. 지시를 끝냈으면 true(완료 답장·축 −3).</summary>
        public static bool DeliverDutyCard()
        {
            if (!_nightOpen || _duties == null || IsCaptured) return false;
            DutyEvent ev;
            if (!_duties.NoteCardDelivered(out ev)) return false;
            AnnounceDuty(ev);
            return true;
        }

        /// <summary>80차: 지금 그 출입 카드를 집기를 바라는지.</summary>
        public static bool DutyWantsCard(string card)
        {
            return _nightOpen && _duties != null && !IsCaptured && _duties.WantsCard(card);
        }

        /// <summary>80차: 들고 있는 출입 카드(없으면 null).</summary>
        public static string DutyCardCarried
        {
            get { return _nightOpen && _duties != null && !IsCaptured ? _duties.CarriedCard : null; }
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
                    CampNoteDone();   // 104차: 지시를 하나 끝내면 경비실 체류 0으로
                    if (!ev.Tainted && _axes != null) LowerByDuty(d);   // 103차: 축마다 밤당 −6까지
                    text = DutyCatalog.DoneText(d, Day, minute);
                    if (!ev.Tainted && GrantDutyTrust(d) > 0 && TakeTrustLine()) text += "\n" + DutyCatalog.TrustLine;   // 103차 · 103차 후속(사용자: 「매번 뜨면 정신없다 — 중간에 한 2회 정도」)
                    break;
                default:
                    if (DutyCatalog.TimeLimitsEnabled) AddWarning(1, "duty." + d.Id);   // 103차: 다시 건 제한(W19·W21)은 넘겨도 경고 없음 — 신뢰 보상만 없다
                    text = DutyCatalog.MissedText(d);
                    break;
            }

            Debug.Log("[NightRun] 근무 지시 " + ev + " — " + text);
            EventBus.RaiseDutySent(new ParadoxMessage("duty." + d.Id + "." + ev.Outcome, d.Id, d.Space, text, minute));
        }

        /// <summary>103차(축 균형): 그 밤 지시로 축마다 내려간 양(상한 <see cref="DutyCatalog.ReliefCapPerAxis"/>).</summary>
        private static readonly int[] _dutyReliefTonight = new int[3];

        private static void LowerByDuty(DutyDef d)
        {
            int i = (int)d.Axis;
            if (i < 0 || i >= _dutyReliefTonight.Length) return;
            int amount = Mathf.Min(DutyCatalog.Relief, DutyCatalog.ReliefCapPerAxis - _dutyReliefTonight[i]);
            if (amount <= 0) return;
            _axes.Lower(d.Axis, amount, "duty." + d.Id);
            _dutyReliefTonight[i] += amount;
        }

        /// <summary>103차: 그 밤 지시로 오른 신뢰(상한 <see cref="DutyCatalog.TrustCapPerNight"/>).</summary>
        private static int _dutyTrustTonight;

        private static readonly NightTallies s_tallies = new NightTallies();

        /// <summary>103차 후속(사용자: 「근무 평가 반영 메시지는 매번 뜨면 정신없다 — 중간에 한번씩, 한 2회 정도」): 회차에 「근무 평가에 반영되었습니다.」 줄을 보이는 횟수.</summary>
        public const int TrustLinesPerRun = 2;

        /// <summary>103차 후속: 둘째 줄은 이 날부터(첫 줄은 회차의 첫 신뢰 보상 — 그 뒤로는 한동안 조용히).</summary>
        public const int TrustLineSecondDay = 3;

        private static int _trustLinesShown;

        private static bool TakeTrustLine()
        {
            if (_trustLinesShown >= TrustLinesPerRun) return false;
            if (_trustLinesShown >= 1 && Day < TrustLineSecondDay) return false;
            _trustLinesShown++;
            return true;
        }

        /// <summary>103차 후속(사용자 7번: 「너무 미루면 독촉 문자와 함께 다음 지시가 진행되도록」): 지시받은 점검을 이만큼(실제 초) 보고하지 않으면 독촉한다.</summary>
        public const float InspectionStallSeconds = 90f;

        /// <summary>103차 후속: 독촉 문자를 다시 보내는 간격(실제 초).</summary>
        public const float StallNagRepeatSeconds = 120f;

        /// <summary>103차 후속: 독촉 문자 본문({0} = 보고하지 않은 점검 수).</summary>
        public const string StallNagText = "지시한 점검 {0}건의 보고가 늦어지고 있습니다. 확인 후 보고 바랍니다.";

        private static float _sinceStallNag = float.PositiveInfinity;
        private static int _stallNagSerial;

        /// <summary>103차 후속: 지금 지시받은 점검을 오래 미루고 있는지(근무 지시가 밀린 점검을 더는 기다리지 않는다).</summary>
        public static bool InspectionsStalled
        {
            get { return _orders != null && _orders.OldestPendingSeconds(_unavoidable.Banned) >= InspectionStallSeconds; }
        }

        /// <summary>
        /// 104차(사용자: 「재촉 제거 필요 — 원래 유도 문자와 겹침. 재촉하는 걸 다 없애줘」): 점검 독촉 문자를 보내는지 — 기본 끔.
        /// 미룬 점검을 더 기다리지 않고 근무 지시를 내는 것(<see cref="InspectionsStalled"/>)은 그대로다.
        /// </summary>
        public static bool StallNagEnabled { get; set; }

        private static void NagStalledInspections(float realSeconds, bool stalled, int backlog)
        {
            if (_sinceStallNag < float.PositiveInfinity) _sinceStallNag += realSeconds;
            if (!StallNagEnabled || !stalled || backlog <= 0) return;
            if (_sinceStallNag < StallNagRepeatSeconds) return;
            _sinceStallNag = 0f;
            _stallNagSerial++;
            string nag = string.Format(StallNagText, backlog);
            Debug.Log("[NightRun] 점검 독촉 — " + nag);
            EventBus.RaiseDutySent(new ParadoxMessage("inspection.nag." + _stallNagSerial, null, SpaceId.None, nag, CurrentMinute()));
            if (_tension != null) _tension.Pacer.Impulse(PacerImpulse.Order);
        }

        /// <summary>
        /// 103차 최종 QA: 축·장부와 함께 되돌려야 하는 밤 집계 — 지시 보상 상한(신뢰·축별 감소), 처벌 축 집계(<c>_warnByAxis</c>), 결과창 위반 시각.
        /// 옛: 재시작이 축·신뢰·경고 장부는 스냅샷으로 되돌리는데 이 값들은 그대로라, 되감긴 시도에서 받은 보상 때문에 다시 해도 신뢰·감소가 0이었고
        /// 처벌이 엉뚱한 축으로 갔으며, 체크포인트 전 위반이 결과창에서 빠졌다.
        /// </summary>
        private sealed class NightTallies : ISnapshotable
        {
            public string SnapshotKey
            {
                get { return "night.tallies"; }
            }

            public object CaptureState()
            {
                return new State
                {
                    TrustLines = _trustLinesShown,
                    DutyTrust = _dutyTrustTonight,
                    DutyRelief = (int[])_dutyReliefTonight.Clone(),
                    WarnByAxis = (int[])_warnByAxis.Clone(),
                    Violations = new System.Collections.Generic.List<int>(ViolationMinutesToday),
                };
            }

            public void RestoreState(object state)
            {
                State s = state as State;
                if (s == null) return;
                _dutyTrustTonight = s.DutyTrust;
                _trustLinesShown = s.TrustLines;
                _sinceStallNag = float.PositiveInfinity;   // 되돌린 시도의 독촉 문자는 태블릿이 지운다 — 새로 센다
                System.Array.Copy(s.DutyRelief, _dutyReliefTonight, Mathf.Min(s.DutyRelief.Length, _dutyReliefTonight.Length));
                System.Array.Copy(s.WarnByAxis, _warnByAxis, Mathf.Min(s.WarnByAxis.Length, _warnByAxis.Length));
                ViolationMinutesToday.Clear();
                ViolationMinutesToday.AddRange(s.Violations);
            }

            private sealed class State
            {
                public int TrustLines;
                public int DutyTrust;
                public int[] DutyRelief;
                public int[] WarnByAxis;
                public System.Collections.Generic.List<int> Violations;
            }
        }

        /// <summary>103차: 지시 완료 신뢰를 준다(밤 상한 안에서). 준 양.</summary>
        private static int GrantDutyTrust(DutyDef d)
        {
            if (_axes == null) return 0;
            int want = Mathf.Min(DutyCatalog.TrustFor(d), DutyCatalog.TrustCapPerNight - _dutyTrustTonight);
            if (want <= 0) return 0;
            if (!_axes.Apply(FearAxis.Trust, want, "duty." + d.Id, d.Space)) return 0;
            _dutyTrustTonight += want;
            return want;
        }

        /// <summary>103차: 그 밤 지시로 오른 신뢰(검수·시험용).</summary>
        public static int DutyTrustTonight
        {
            get { return _dutyTrustTonight; }
        }

        /// <summary>103차: 새 회차 — 지난 회차의 지시를 거르지 않는다.</summary>
        private static void ForgetLastNightDuties()
        {
            _lastNightDuties.Clear();
            _dutyCarry = false;
        }

        private static void ResetDuties()
        {
            ForgetLastNightDuties();
            _duties = null;
            _leadDuty = null;
            DutiesEnabled = false;
            DutyCctvChannels = 5;
        }
    }
}

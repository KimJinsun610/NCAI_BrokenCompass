using UnityEngine;

namespace NightDuty
{
    public static partial class NightRun
    {
        private static TensionDirector _tension;
        private static bool _flushingDirection;

        /// <summary>
        /// 오늘 긴장 디렉터(<see cref="ProgramEnabled"/>일 때 밤 편성과 함께 만든다). 없으면 null.
        /// 재시작해도 새로 만들지 않고 <see cref="TensionDirector.ResetToRest"/>로 되감는다.
        /// </summary>
        public static TensionDirector Tension
        {
            get { return _tension; }
        }

        /// <summary>
        /// 디렉터가 스스로 조우·수칙 단서·가짜 놀람을 거는지(기본 켬). 디버그 콘솔에서 끄면 디버그 버튼으로만 돈다.
        /// 끄더라도 이미 진행 중인 조우의 단계는 흐른다.
        /// </summary>
        public static bool DirectorAutoRun { get; set; } = true;

        private static void BeginDirection()
        {
            DisposeDirection();
            if (!ProgramEnabled || _program == null) return;

            _tension = new TensionDirector(_program, Day, RestartsTonight);
            _tension.Emitted += OnDirectionEmitted;
            _tension.GazeTargetReady = GazeTargetReady;   // 51차: 사다리(C-3)를 「점검 중」일 때만 시체가 떨어진다
            _tension.EncounterGate = LeadGateOpen;         // 85차: 블라인드 지시 → 창밖 노란 얼굴 · biology 책/도서관 순찰 지시 → 문간 노란 얼굴
            // 60차: 시체가 떨어지는 곳 반반 — 사다리 방 안에 들어와서(사다리 2.8m 안) 사다리를 볼 때 /
            // 61차(민: 「교실 안쪽에서 떨어지는 건, 플레이어가 그 안쪽에서 나오면서 플레이어 쪽으로」): 사다리 곁에 들어갔다가 나오는 길에(옛: 교실 입구에서 사다리를 볼 때).
            _tension.GazeTargetPosition = TargetPosition;
            // 104차(사용자: 「시체가 등장해도 못 보는 경우가 많음 — 사다리 이상 보고 도중에 낙하」): 사다리(C-3) 보고 키를 누르기 시작하면 떨어진다(사다리를 보는 중이라 화면 안).
            // 옛 반반(사다리 방 안에서 볼 때 / 나오는 길에)은 코어 시험용으로 남긴다(CorpseOnReport = false).
            if (CorpseOnReport)
            {
                _tension.GazeTargetReportMode = true;   // 104차: 시체(사다리 보고 도중) · 소년(화분 보고) · 소녀(3번 칸 지시)
                _tension.ItemOrdered = id => Board.IsIssued(id) && !Board.IsHeld(id);
                if (_program.HasEncounter(ProgramCatalog.CeilingLegs)) Debug.Log("[NightRun] 시체 낙하 — 사다리 보고 도중");
            }
            else
            {
                bool ladderRoom = s_corpseRoll.NextDouble() < 0.5;
                _tension.GazeTargetNearRadius = ladderRoom ? CorpseLadderRoomRadius : 0f;
                _tension.GazeTargetExitMode = !ladderRoom;
                if (_program.HasEncounter(ProgramCatalog.CeilingLegs)) Debug.Log("[NightRun] 시체 낙하 — " + (ladderRoom ? "사다리 방 안에서 사다리를 볼 때" : "사다리 곁에서 나오는 길에"));
            }
            // 61차: 몹은 먼저 세우고 플레이어가 본 뒤 대면 · 안쪽 깊이 머무름 — 연출 쪽이 있을 때만(코어 시험은 옛 흐름).
            _tension.SightGated = EncounterSightGating;
            _tension.DeepInSpace = (space, margin) => DeepInSpaceProbe == null || DeepInSpaceProbe(space, margin);
            _tension.Missed += VoidEncounterRules;          // 57차: 끝내 오지 않은 조우의 수칙은 태블릿에서 거둔다
            _tension.FakeBlocked = FakeBlockedAt;            // 57차: 점검 대상 가까이에서는 가짜 놀람을 미룬다
            _voidRules.Clear();
            PlanWildcard();   // 104차: 그날의 빈 칸
        }

        private static readonly System.Random s_wildcardRoll = new System.Random();

        /// <summary>104차: 그날 빈 칸 후보 — 가장 높은 감각 축마다(청각: 먼 사물함 덜컹 · 사물함 줄 / 조도: 손전등 깜빡임 · 멀리 스치는 몹 / 배치: 멀리 스치는 몹 · 사물함 줄).</summary>
        public static string[] WildcardPool(FearAxis axis)
        {
            switch (axis)
            {
                case FearAxis.Auditory: return new[] { "fake.locker.rattle", "fake.locker.row" };
                case FearAxis.Illuminance: return new[] { "fake.flashlight.flicker", TensionDirector.FakeGlimpse };
                default: return new[] { TensionDirector.FakeGlimpse, "fake.locker.row" };
            }
        }

        /// <summary>
        /// 104차(사용자: 「날마다 빈 칸 하나 — 그날 가장 높은 축에 맞춰」): 고정 편성 밤(2일차부터)에 가장 높은 감각 축의 후보 중 하나를 01:00~03:00 사이 한 번.
        /// </summary>
        private static void PlanWildcard()
        {
            if (_tension == null || !_tension.Sequential || Day < 2) return;
            FearAxis axis = HighestSensory();
            string[] pool = WildcardPool(axis);
            string pick = pool[s_wildcardRoll.Next(pool.Length)];
            float at = NightClock.Call1 + (float)s_wildcardRoll.NextDouble() * 120f;
            _tension.SetWildcard(pick, at);
            Debug.Log("[NightRun] 그날의 빈 칸 — " + pick + " (가장 높은 축 " + axis + ", " + NightClock.Clock(at) + " 뒤)");
        }

        /// <summary>
        /// 61차: 몹 대역이 있는 조우를 「세워 두고 → 플레이어가 본 뒤 대면」으로 할지(<see cref="TensionDirector.SightGated"/>). 연출 쪽(DirectionStage)이 켠다.
        /// </summary>
        public static bool EncounterSightGating { get; set; }

        /// <summary>61차: 플레이어가 그 공간 상자 안쪽으로 그만큼(m) 들어와 있는지(연출 쪽이 넣는다). null이면 참.</summary>
        public static System.Func<SpaceId, float, bool> DeepInSpaceProbe { get; set; }

        /// <summary>61차: 플레이어가 세워 둔 몹을 봤다(연출 쪽이 부른다). 받았으면 true.</summary>
        public static bool EncounterSeen(string encounterId)
        {
            if (_tension == null) return false;
            bool ok = _tension.NotifySeen(encounterId);
            if (ok) Debug.Log("[NightRun] 몹을 봄 — " + encounterId + " 대면");
            return ok;
        }

        /// <summary>60차: 「사다리 방 안」으로 보는 사다리(C-3)와의 거리(m, 수평).</summary>
        public const float CorpseLadderRoomRadius = 2.8f;

        private static readonly System.Collections.Generic.HashSet<string> s_standingFired = new System.Collections.Generic.HashSet<string>();

        /// <summary>104차: 그날 밤새 서 있는 것인지(4일차 복도 끝 모형 · 5일차 모형 급습 — <see cref="NightProgram.Standing"/>).</summary>
        public static bool IsStandingTonight(string encounterId)
        {
            return _nightOpen && _program != null && _program.IsStanding(encounterId);
        }

        /// <summary>104차: 밤새 서 있던 조우를 이미 걸었는지(그 시도 안에서).</summary>
        public static bool StandingFired(string encounterId)
        {
            return encounterId != null && s_standingFired.Contains(encounterId);
        }

        /// <summary>
        /// 104차(사용자: 「인체 모형 급습은 상시 대기, 빛을 받는 순간 바로 급습」): 밤새 서 있던 조우를 곧바로 건다(연출 쪽 — 복도 끝 모형이 손전등을 받으면).
        /// 그날 서 있는 것이 아니거나 이미 걸었거나 다른 조우가 진행 중이면 false.
        /// </summary>
        public static bool TriggerStanding(string encounterId)
        {
            if (!_nightOpen || IsCaptured || _tension == null || _finale.Active || !IsStandingTonight(encounterId) || s_standingFired.Contains(encounterId)) return false;
            if (!_tension.StartNow(encounterId)) return false;
            s_standingFired.Add(encounterId);
            FlushDirection();
            Debug.Log("[NightRun] 밤새 서 있던 " + encounterId + " — 곧바로");
            return true;
        }

        private static void ResetStanding()
        {
            s_standingFired.Clear();
        }

        /// <summary>104차: 시체 낙하를 사다리 보고 도중에 건다(연출 쪽이 켠다 — 코어 시험은 옛 반반).</summary>
        public static bool CorpseOnReport { get; set; }

        /// <summary>
        /// 104차: 플레이어가 점검 항목의 보고 키를 누르기 시작했다(InspectionSensor가 부른다). 시체 낙하(사다리 C-3)의 방아쇠.
        /// </summary>
        public static void NoteReportHold(string itemId)
        {
            if (_tension == null || string.IsNullOrEmpty(itemId)) return;
            InspectionItem item = InspectionCatalog.FindByTarget(itemId);
            _tension.NoteReportHold(InspectionCatalog.TargetPrefix + (item != null ? item.Id : itemId));
        }

        /// <summary>104차: 보고 도중 시체가 떨어져 보고를 잠깐 멈춰야 하는지(InspectionSensor가 읽는다).</summary>
        public static bool ReportInterrupted
        {
            get { return _tension != null && _tension.ReportInterrupted; }
        }

        private static readonly System.Random s_corpseRoll = new System.Random();

        private static Vector3? TargetPosition(string targetId)
        {
            JudgeTarget t;
            if (string.IsNullOrEmpty(targetId) || !JudgeTargetRegistry.TryGet(targetId, out t) || t == null) return null;
            return t.AnchorPosition;
        }

        /// <summary>가짜 놀람을 미룰 거리(m) — 지시받은 점검 대상에서 이만큼 안이면(57차).</summary>
        public const float FakeQuietRadius = 2.5f;

        private static readonly System.Collections.Generic.HashSet<string> _voidRules = new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal);

        /// <summary>끝내 조우가 오지 않아 태블릿에서 거둔 수칙(57차, 민: 「조우가 등장하지 않으면 관련 수칙이 안 나오게」). 재시작하면 다시 보인다.</summary>
        public static System.Collections.Generic.IEnumerable<string> VoidedRules
        {
            get { return _voidRules; }
        }

        private static void VoidEncounterRules(string encounterId)
        {
            EncounterDef e = ProgramCatalog.Encounter(encounterId);
            if (e == null) return;
            bool changed = false;
            foreach (string id in new[] { e.ResponseRule, e.SecondRule })
            {
                if (string.IsNullOrEmpty(id) || RuleUsedByLiveEncounter(id, encounterId)) continue;
                changed |= _voidRules.Add(id);
            }

            if (!changed) return;
            Debug.Log("[NightRun] 조우 " + encounterId + "가 오지 않아 태블릿에서 수칙을 거둠 — " + string.Join(" · ", _voidRules));
            RefreshRuleCards();
            EventBus.RaiseTabletTextChanged();
        }

        /// <summary>그 수칙을 쓰는 다른 조우가 아직 남아 있는지(놓치지 않은).</summary>
        private static bool RuleUsedByLiveEncounter(string ruleId, string exceptEncounter)
        {
            if (_tension == null) return false;
            foreach (EncounterRun r in _tension.Runs)
            {
                if (r.Def.Id == exceptEncounter || r.State == EncounterRunState.Missed) continue;
                if (r.Def.ResponseRule == ruleId || r.Def.SecondRule == ruleId) return true;
            }

            return false;
        }

        /// <summary>발밑이 지시받은 점검 대상 가까이인지(57차).</summary>
        private static bool FakeBlockedAt(Vector3 feet)
        {
            InspectionPlan plan = Board.Plan;
            if (plan == null) return false;
            foreach (InspectionAssignment a in plan.Assignments)
            {
                if (!IssuedItemPending(a.Id)) continue;
                JudgeTarget t;
                if (!JudgeTargetRegistry.TryGet(InspectionCatalog.TargetPrefix + a.Id, out t) || t == null) continue;
                if (SensingRules.HorizontalDistance(feet, t.AnchorPosition) <= FakeQuietRadius) return true;
            }

            return false;
        }

        /// <summary>응시 방아쇠 대상(<c>inspect.&lt;항목&gt;</c>)이 지시받았고 아직 보고 전인지. 순차 지시가 꺼져 있으면 편성에 있고 보고 전이면 참.</summary>
        private static bool GazeTargetReady(string targetId)
        {
            if (string.IsNullOrEmpty(targetId)) return false;
            string item = targetId.StartsWith(InspectionCatalog.TargetPrefix, System.StringComparison.Ordinal) ? targetId.Substring(InspectionCatalog.TargetPrefix.Length) : targetId;
            return IssuedItemPending(item);
        }

        private static void DisposeDirection()
        {
            if (_tension == null) return;
            _tension.Emitted -= OnDirectionEmitted;
            _tension = null;
        }

        private static void OnDirectionEmitted(DirectionEvent e)
        {
            EventBus.RaiseDirectionEmitted(e);
            ArmToiletAfterGirl(e);
        }

        /// <summary>
        /// 104차(사용자: 「조우 끝나고 변기 이상해지지 않음 — 무조건 이상해지게」 → 「조우가 시작되면 바로 변기가 머리카락 상태로」): 화장실 여자아이 조우가 시작되면
        /// (대역이 서는 순간 · 곧바로 대면이면 대면 · 놓쳐도 창 닫힘·결과) 그 칸 변기(T-1)를 늘 역보고 상태로 —
        /// 옛: 소녀를 0.1초 응시했거나 끝날 때 화장실 안에 있어야만(T4 판정) 변기가 이상해졌다. 역보고 수칙(T4)이 있는 밤만.
        /// </summary>
        private static void ArmToiletAfterGirl(DirectionEvent e)
        {
            if (e.Kind != DirectionEventKind.Encounter || e.SourceId != ProgramCatalog.ToiletGirl) return;
            if (e.Phase != DirectionPhase.Present && e.Phase != DirectionPhase.Confront && e.Phase != DirectionPhase.WindowClose && e.Phase != DirectionPhase.Result) return;
            if (_program == null || !_program.Has(ProgramCatalog.ReverseReportRule)) return;
            string t1 = InspectionCatalog.ReverseReportItem;
            if (Board.Plan == null || Board.Plan.Find(t1) == null || Board.IsReverse(t1)) return;
            Debug.Log("[NightRun] 여자아이 조우 시작 — 3번 칸 변기를 이상으로(역보고, " + e.Phase + ")");
            OnReverseReportArmed(t1);
        }

        private static void DirectionObserve(in JudgeSignal signal)
        {
            if (_tension == null) return;
            _tension.Observe(signal);
        }

        /// <summary>디렉터 시간 경과. 판정 정지 구간에도 흐른다(슬롯·단서는 디렉터가 시각으로 거른다).</summary>
        private static void DirectionTick(float realSeconds)
        {
            if (_tension == null) return;
            _tension.SightGated = EncounterSightGating;   // 61차: 연출 쪽이 늦게 서도 따라간다

            int highest = _axes.GetValue(HighestSensory());
            Band auditory = _bands.Shown.GetBand(FearAxis.Auditory);
            // 71차: 개발자 모드 흐름 정지 — 새 조우·단서·가짜 놀람을 스스로 걸지 않는다. 피날레 중(2026-10-11, 조기 퇴근으로 일찍 열려도)도 같다.
            if (DirectorAutoRun && !_sandbox && !_finale.Active)
            {
                _tension.Tick(CurrentMinute(), realSeconds, highest, auditory, IsCaptured);
            }
            else
            {
                // 자동 연출을 꺼도 진행 중인 단계는 끝까지 흐르게 한다 — 시각을 「판정 밖」(-1)으로 넘겨 새 조우·단서만 막는다.
                _tension.Tick(-1f, realSeconds, highest, auditory, IsCaptured);
            }

            FlushDirection();
        }

        /// <summary>디렉터가 낸 판정 단서를 판정 책에 넣는다(재진입 방지).</summary>
        private static void FlushDirection()
        {
            if (_tension == null || _flushingDirection) return;
            _flushingDirection = true;
            try
            {
                JudgeSignal s;
                while (_tension != null && _tension.TryDequeue(out s))
                {
                    Send(s);
                }
            }
            finally
            {
                _flushingDirection = false;
            }
        }

        private static void DirectionAbort(string reason)
        {
            if (_tension == null) return;
            _tension.AbortAll(reason);
        }

        private static void DirectionRestart(int restarts, int startMinute)
        {
            if (_tension == null) return;
            _tension.ResetToRest(restarts, startMinute);
            if (_voidRules.Count == 0) return;
            _voidRules.Clear();   // 놓친 조우가 다시 기다리므로 수칙도 다시 보인다
            RefreshRuleCards();
            EventBus.RaiseTabletTextChanged();
        }

        // ── 디버그 ─────────────────────────────────────────────

        /// <summary>디버그: 조우를 지금 바로 시작한다(전조 2초 → 대면). 디렉터가 없거나 대본이 없으면 false.</summary>
        public static bool DebugForceEncounter(string encounterId)
        {
            if (_tension == null) return false;
            bool ok = _tension.ForceEncounter(encounterId);
            FlushDirection();
            return ok;
        }

        /// <summary>디버그: 가짜 놀람을 지금 건다(예산을 쓰지 않음). 디렉터가 없거나 목록에 없으면 false.</summary>
        public static bool DebugForceFake(string fakeId)
        {
            if (_tension == null) return false;
            bool ok = _tension.ForceFake(fakeId);
            FlushDirection();
            return ok;
        }

        /// <summary>디버그: 수칙 단서를 지금 울린다(분필·물 내림 등). 대본이 없으면 false.</summary>
        public static bool DebugFireRuleCue(string ruleId)
        {
            if (_tension == null) return false;
            bool ok = _tension.FireRuleNow(ruleId);
            FlushDirection();
            return ok;
        }

        /// <summary>디버그: 울리는 중인 수칙 단서를 끝낸다.</summary>
        public static bool DebugEndRuleCue(string ruleId)
        {
            if (_tension == null) return false;
            bool ok = _tension.EndRuleNow(ruleId);
            FlushDirection();
            return ok;
        }

        /// <summary>103차: 연출이 먼저 끝난 조우의 대면 창을 닫는다(<see cref="TensionDirector.CloseWindowNow"/>). 대면 중이 아니면 false.</summary>
        public static bool EndEncounterWindow(string encounterId)
        {
            return _tension != null && !string.IsNullOrEmpty(encounterId) && _tension.CloseWindowNow(encounterId);
        }

        /// <summary>디버그: 진행 중인 조우를 다음 단계로 넘긴다.</summary>
        public static void DebugSkipDirectionPhase()
        {
            if (_tension == null) return;
            _tension.SkipPhase();
            _tension.Tick(CurrentMinute(), 0f, _axes.GetValue(HighestSensory()), _bands.Shown.GetBand(FearAxis.Auditory), IsCaptured);
            FlushDirection();
        }

        /// <summary>디버그: 생존 수치를 내린다(신뢰는 무시).</summary>
        public static void DebugLowerAxis(FearAxis axis, int amount)
        {
            if (_axes == null || amount <= 0) return;
            _axes.Lower(axis, amount, "debug");
        }

        /// <summary>
        /// 디버그: 오늘 새 수칙 덱에 수칙 하나를 더한다(판정 책을 다시 만들지 않고 판정기만 붙인다). 이미 있거나 모르는 ID면 false.
        /// </summary>
        public static bool DebugAddFinalRule(string ruleId)
        {
            if (_finalBook == null) return false;
            RuleDef def = ProgramCatalog.Rule(ruleId);
            if (def == null || _finalBook.Judge(ruleId) != null) return false;
            bool ok = _finalBook.AddJudge(def);
            if (ok) Debug.Log("[NightRun] 디버그: 오늘 덱에 " + ruleId + " 추가");
            return ok;
        }
    }
}

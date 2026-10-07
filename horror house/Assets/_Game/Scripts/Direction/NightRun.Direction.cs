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
            // 60차: 시체가 떨어지는 곳 반반 — 교실 입구에서 사다리를 볼 때(거리 무관) / 사다리 방 안에 들어와서(사다리 2.8m 안).
            _tension.GazeTargetPosition = TargetPosition;
            _tension.GazeTargetNearRadius = s_corpseRoll.NextDouble() < 0.5 ? CorpseLadderRoomRadius : 0f;
            if (_program.HasEncounter(ProgramCatalog.CeilingLegs)) Debug.Log("[NightRun] 시체 낙하 — " + (_tension.GazeTargetNearRadius > 0f ? "사다리 방 안에서" : "교실 입구에서 사다리를 볼 때"));
            _tension.Missed += VoidEncounterRules;          // 57차: 끝내 오지 않은 조우의 수칙은 태블릿에서 거둔다
            _tension.FakeBlocked = FakeBlockedAt;            // 57차: 점검 대상 가까이에서는 가짜 놀람을 미룬다
            _voidRules.Clear();
        }

        /// <summary>60차: 「사다리 방 안」으로 보는 사다리(C-3)와의 거리(m, 수평).</summary>
        public const float CorpseLadderRoomRadius = 2.8f;

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

            int highest = _axes.GetValue(HighestSensory());
            Band auditory = _bands.Shown.GetBand(FearAxis.Auditory);
            if (DirectorAutoRun)
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

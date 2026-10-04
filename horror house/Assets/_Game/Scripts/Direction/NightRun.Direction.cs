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

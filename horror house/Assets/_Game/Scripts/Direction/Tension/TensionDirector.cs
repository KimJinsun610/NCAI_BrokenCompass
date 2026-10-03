using System;
using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;

namespace NightDuty
{
    /// <summary>조우 한 번의 진행 상태.</summary>
    public enum EncounterRunState
    {
        /// <summary>슬롯 시간창과 방아쇠를 기다린다.</summary>
        Waiting = 0,

        /// <summary>전조 중.</summary>
        Foreshadow = 1,

        /// <summary>대면·대응 창 중(존재형은 슬롯 끝까지).</summary>
        Active = 2,

        /// <summary>끝 단서를 보낸 뒤 결과까지.</summary>
        Releasing = 3,

        /// <summary>끝났다.</summary>
        Done = 4,

        /// <summary>슬롯이 끝날 때까지 방아쇠가 오지 않았다.</summary>
        Missed = 5
    }

    /// <summary>그날 슬롯 하나의 조우 진행.</summary>
    public sealed class EncounterRun
    {
        /// <summary>슬롯.</summary>
        public EncounterSlot Slot;

        /// <summary>조우 정의.</summary>
        public EncounterDef Def;

        /// <summary>대본.</summary>
        public EncounterScript Script;

        /// <summary>상태.</summary>
        public EncounterRunState State;

        /// <summary>지금 단계가 끝나는 디렉터 시각(초).</summary>
        public float PhaseEnds;

        /// <summary>헛예고 횟수.</summary>
        public int FalseCount;

        /// <summary>다시 시도할 수 있는 시각(헛예고 뒤).</summary>
        public float RetryAt;

        /// <summary>마지막으로 시작하지 못한 사유(디버그).</summary>
        public string Waiting = string.Empty;

        /// <summary>디버그로 강제한 실행.</summary>
        public bool Forced;

        /// <summary>대역·소리 자리.</summary>
        public Vector3 Point;

        /// <summary>대면을 시작한 밤 분(재시작 되감기에 쓴다).</summary>
        public float ConfrontMinute = -1f;

        /// <inheritdoc/>
        public override string ToString()
        {
            return Slot + " " + Def.Id + " " + State + (Waiting.Length > 0 && State == EncounterRunState.Waiting ? " (" + Waiting + ")" : string.Empty);
        }
    }

    /// <summary>수칙 단서 하나의 진행.</summary>
    public sealed class RuleTriggerRun
    {
        /// <summary>대본.</summary>
        public RuleTriggerScript Script;

        /// <summary>이번 밤에 필요한 머무름(초).</summary>
        public float NeedDwell;

        /// <summary>울리는 중.</summary>
        public bool Running;

        /// <summary>끝났다(밤에 한 번).</summary>
        public bool Done;

        /// <summary>끝나는 시각.</summary>
        public float EndsAt;

        /// <summary>실제로 보낸 단서 ID(@대상 포함).</summary>
        public string SentCue = string.Empty;

        /// <summary>울린 밤 분.</summary>
        public float FiredMinute = -1f;
    }

    /// <summary>
    /// 긴장 디렉터(최종 기획서 「연출과 긴장 디렉터」, 2026-10-01 7단계). 돌아다니는 AI가 아니라 밤의 리듬을 관리하는 규칙 묶음이다.
    /// <list type="bullet">
    /// <item>조우는 밤 시작에 편성기(<see cref="ProgramDirector"/>)가 슬롯 A/B/C에 정해 둔다. 디렉터는 <b>시점과 자리만</b> 고른다.</item>
    /// <item>슬롯 시간창: A 01:00–01:52 · B 02:16–03:08 · C 03:08–03:30(강도 단계가 위험이면 끔). 03:30 뒤에는 새 조우를 걸지 않는다.</item>
    /// <item>조우 = 방아쇠(<see cref="EncounterScript.Trigger"/>) → 전조(강도 3↑만 1~8초, 헛예고 30% / 청각 구간 3↑ 40%) → 대면(대응 수칙의 단서를 판정 책에) → 대응 창 → 끝 단서 → 결과.</item>
    /// <item><see cref="SurpriseBudget"/>이 강도별 횟수·간격·조우 뒤 휴지를 막는다.</item>
    /// <item>조우에 묶이지 않은 수칙(<see cref="RuleTriggers"/>)의 단서를 그 공간에 머물 때 밤마다 한 번 울린다(조우 중엔 쉼, 20초 간격).</item>
    /// <item>가짜 놀람: 진짜 1에 3까지, 같은 것은 밤에 2번, 60~150초 간격.</item>
    /// </list>
    /// 코어 안에서 돌고(엔진 객체를 모름), 판정 단서는 <see cref="TryDequeue"/>로 내보낸다 — <c>NightRun</c>이 꺼내 판정 책에 넣는다.
    /// 보이고 들리는 일은 <see cref="Emitted"/>를 받은 연출 쪽이 한다.
    /// </summary>
    public sealed class TensionDirector
    {
        /// <summary>헛예고 뒤 다시 시도까지(초).</summary>
        public const float FalseRetry = 20f;

        /// <summary>수칙 단서 사이 최소 간격(초).</summary>
        public const float RuleGap = 20f;

        /// <summary>가짜 놀람 간격 최소~최대(초).</summary>
        public const float FakeGapMin = 60f;

        /// <summary>가짜 놀람 간격 최대(초).</summary>
        public const float FakeGapMax = 150f;

        /// <summary>같은 가짜 놀람의 밤당 상한.</summary>
        public const int FakePerId = 2;

        /// <summary>슬롯 끝 몇 분 전부터 방아쇠를 「그 공간에 있기만 하면」으로 완화하는지(게임 분).</summary>
        public const float LastCallMinutes = 10f;

        /// <summary>디버그 강제 실행에서 존재형 조우의 길이(초).</summary>
        public const float ForcedPresenceSeconds = 60f;

        /// <summary>가짜 놀람 목록.</summary>
        public static readonly string[] FakeScares = { "fake.locker.rattle", "fake.locker.row", "fake.flashlight.flicker", FakeBugs };

        /// <summary>벌레 떼(김진선님 BugSwarm, 2026-10-02 민 추가) — 천장에서 쏟아진다. <see cref="BugSpaces"/>에서만.</summary>
        public const string FakeBugs = "fake.bugs";

        /// <summary>벌레 떼가 떨어질 수 있는 방(정규화 전): 1-3 교실(뒤 창고 포함) · 화장실 · 도서관.</summary>
        public static readonly SpaceId[] BugSpaces = { SpaceId.Classroom_1_3, SpaceId.Toilet, SpaceId.Library };

        /// <summary>그 방에 이만큼(초) 머문 뒤에만 벌레 떼를 건다 — 들어서자마자 쏟아지지 않게.</summary>
        public const float BugDwellSeconds = 6f;

        private readonly NightProgram _program;
        private readonly int _day;
        private readonly Random _rng;
        private readonly List<EncounterRun> _runs = new List<EncounterRun>();
        private readonly List<RuleTriggerRun> _rules = new List<RuleTriggerRun>();
        private readonly Queue<JudgeSignal> _out = new Queue<JudgeSignal>();
        private readonly HashSet<string> _zones = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, float> _zoneSince = new Dictionary<string, float>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _fakeCount = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly HashSet<string> _seenHigh = new HashSet<string>(StringComparer.Ordinal);

        private SurpriseBudget _budget;
        private int _restarts;
        private float _minute;
        private SpaceId _space = SpaceId.None;
        private SpaceId _exact = SpaceId.None;   // 정규화 전 방(1-1/1-3 교실 구분). 고정 자리 조우의 방아쇠용.
        private float _spaceSince;
        private bool _hasPose;
        private Vector3 _feet;
        private float _yaw;
        private float _walk;
        private float _cctvSince = -1f;
        private float _lastCctvView = float.NegativeInfinity;
        private string _channel = string.Empty;
        private float _lastRuleCue = float.NegativeInfinity;
        private float _nextFake;
        private int _fakesUsed;
        private int _encountersDone;
        private string _lastNote = string.Empty;

        /// <summary>그날 편성으로 만든다.</summary>
        public TensionDirector(NightProgram program, int day, int restarts = 0, Random rng = null)
        {
            _program = program ?? NightProgram.Empty(day);
            _day = Math.Max(1, day);
            _restarts = restarts;
            _rng = rng ?? new Random();
            _budget = new SurpriseBudget(_day);

            foreach (SlotEncounter s in _program.Slots)
            {
                EncounterScript script = EncounterScripts.Find(s.Encounter.Id);
                if (script == null) continue;
                _runs.Add(new EncounterRun { Slot = s.Slot, Def = s.Encounter, Script = script });
            }

            foreach (RuleDef r in _program.Deck)
            {
                RuleTriggerScript script = RuleTriggers.Find(r.Id);
                if (script == null) continue;
                _rules.Add(new RuleTriggerRun { Script = script, NeedDwell = Range(script.DwellMin, script.DwellMax) });
            }

            _nextFake = Range(FakeGapMin * 0.5f, FakeGapMax * 0.5f);
        }

        /// <summary>연출 알림(대역·소등·소리·로그).</summary>
        public event Action<DirectionEvent> Emitted;

        /// <summary>디렉터 시각(실제 초, Tick의 누적).</summary>
        public float Now { get; private set; }

        /// <summary>지금 강도 단계.</summary>
        public DirectorMood Mood { get; private set; } = DirectorMood.Normal;

        /// <summary>놀람 예산.</summary>
        public SurpriseBudget Budget
        {
            get { return _budget; }
        }

        /// <summary>그날 조우 진행(슬롯 순).</summary>
        public IReadOnlyList<EncounterRun> Runs
        {
            get { return _runs; }
        }

        /// <summary>수칙 단서 진행.</summary>
        public IReadOnlyList<RuleTriggerRun> RuleRuns
        {
            get { return _rules; }
        }

        /// <summary>지금 디렉터가 아는 플레이어 공간.</summary>
        public SpaceId Space
        {
            get { return _space; }
        }

        /// <summary>정규화 전 방(1-1/1-3 교실 구분).</summary>
        public SpaceId ExactSpace
        {
            get { return _exact; }
        }

        /// <summary>쓴 가짜 놀람 수.</summary>
        public int FakesUsed
        {
            get { return _fakesUsed; }
        }

        /// <summary>조우가 전조·대면·마무리 중인지(존재형의 머무름은 세지 않는다).</summary>
        public bool Busy
        {
            get
            {
                for (int i = 0; i < _runs.Count; i++)
                {
                    EncounterRun r = _runs[i];
                    if (r.State == EncounterRunState.Foreshadow || r.State == EncounterRunState.Releasing) return true;
                    if (r.State == EncounterRunState.Active && !r.Script.IsPresence) return true;
                }

                return false;
            }
        }

        /// <summary>판정 단서 하나를 꺼낸다(NightRun이 판정 책에 넣는다).</summary>
        public bool TryDequeue(out JudgeSignal signal)
        {
            if (_out.Count == 0)
            {
                signal = default(JudgeSignal);
                return false;
            }

            signal = _out.Dequeue();
            return true;
        }

        /// <summary>슬롯의 시간창(밤 분). C는 03:08–03:30.</summary>
        public static void SlotWindow(EncounterSlot slot, out float from, out float to)
        {
            switch (slot)
            {
                case EncounterSlot.A:
                    from = NightClock.Call1;
                    to = NightClock.RelaxStart;
                    break;
                case EncounterSlot.B:
                    from = NightClock.Call2;
                    to = NightClock.SlotCStart;
                    break;
                default:
                    from = NightClock.SlotCStart;
                    to = NightClock.JudgingEnd;
                    break;
            }
        }

        // ── 입력 ───────────────────────────────────────────────

        /// <summary>플레이어 신호(판정 정지 중에도 모두 받는다).</summary>
        public void Observe(in JudgeSignal s)
        {
            switch (s.Kind)
            {
                case SignalKind.SpaceEntered:
                    _space = SpaceIds.Canonical(s.Space);
                    _exact = s.Space;
                    _spaceSince = Now;
                    _walk = 0f;
                    break;
                case SignalKind.SpaceExited:
                    SpaceId left = SpaceIds.Canonical(s.Space);
                    if (_space == left)
                    {
                        _space = SpaceId.None;
                        _exact = SpaceId.None;
                        _spaceSince = Now;
                    }

                    EndOnExit(left);
                    break;
                case SignalKind.ZoneEntered:
                    if (_zones.Add(s.TargetId)) _zoneSince[s.TargetId] = Now;
                    break;
                case SignalKind.ZoneExited:
                    _zones.Remove(s.TargetId);
                    break;
                case SignalKind.PlayerPose:
                    if (_hasPose && _space == SpaceId.Corridor) _walk += SensingRules.HorizontalDistance(_feet, s.Point);
                    _feet = s.Point;
                    _yaw = s.Value;
                    _hasPose = true;
                    break;
                case SignalKind.CctvViewSample:
                    if (Now - _lastCctvView > 0.5f) _cctvSince = Now;
                    _lastCctvView = Now;
                    _channel = s.TargetId;
                    break;
                case SignalKind.CctvChannel:
                    _channel = s.TargetId;
                    break;
            }
        }

        /// <summary>
        /// 시간 경과. <paramref name="minute"/> = 밤 시계(0~240), <paramref name="dt"/> = 실제 초.
        /// 생존 수치와 청각 연출 구간으로 강도 단계와 헛예고 확률을 정한다. 붙잡혔으면 모두 중단한다.
        /// </summary>
        public void Tick(float minute, float dt, int highestSensory, Band auditoryShown, bool captured)
        {
            if (dt > 0f) Now += dt;
            _minute = minute;
            Mood = DirectorMoods.Of(highestSensory, _restarts);

            if (captured)
            {
                AbortAll("붙잡힘");
                return;
            }

            AdvanceRuns(minute);
            AdvanceRules();

            if (!Busy) TryStartEncounter(minute, auditoryShown);
            if (!Busy) TryRuleTriggers(minute);
            if (!Busy) TryFakeScare(minute);
        }

        // ── 조우 ───────────────────────────────────────────────

        private bool SlotOpen(EncounterRun r, float minute)
        {
            if (r.Forced) return true;
            if (!NightClock.CanStartEncounter(minute)) return false;
            if (!NightProgram.SlotActive(r.Slot, _restarts)) return false;
            if (r.Slot == EncounterSlot.C && Mood == DirectorMood.Danger) return false;

            float from, to;
            SlotWindow(r.Slot, out from, out to);
            return minute >= from && minute < to;
        }

        private bool Triggered(EncounterRun r, float minute)
        {
            EncounterScript s = r.Script;
            float from, to;
            SlotWindow(r.Slot, out from, out to);
            bool lastCall = minute >= to - LastCallMinutes;

            switch (s.Trigger)
            {
                case EncounterTrigger.EnterSpace:
                    return InScriptSpace(s);
                case EncounterTrigger.DwellInSpace:
                    return InScriptSpace(s) && (lastCall || Now - _spaceSince >= s.Dwell);
                case EncounterTrigger.CorridorWalk:
                    return _space == SpaceId.Corridor && (lastCall || _walk >= s.Dwell);
                case EncounterTrigger.ViewingCctv:
                    return ViewingCctv && Now - _cctvSince >= s.Dwell;
            }

            return false;
        }

        private bool ViewingCctv
        {
            get { return _cctvSince >= 0f && Now - _lastCctvView <= 0.5f; }
        }

        private void TryStartEncounter(float minute, Band auditoryShown)
        {
            for (int i = 0; i < _runs.Count; i++)
            {
                EncounterRun r = _runs[i];
                if (r.State != EncounterRunState.Waiting || Now < r.RetryAt || !SlotOpen(r, minute)) continue;

                if (!Triggered(r, minute))
                {
                    r.Waiting = "방아쇠 대기";
                    continue;
                }

                string why;
                if (!_budget.CanStart(r.Def.Intensity, r.Def.IsCross, Now, out why))
                {
                    if (r.Waiting != why) Note(r.Def.Id, "예산: " + why);
                    r.Waiting = why;
                    continue;
                }

                Begin(r, auditoryShown);
                return;
            }
        }

        private void Begin(EncounterRun r, Band auditoryShown)
        {
            r.Waiting = string.Empty;
            r.Point = PointFor(r.Script);

            if (r.Def.Intensity < 3)
            {
                Confront(r);
                return;
            }

            float scale = DirectorMoods.ForeshadowScale(Mood);
            if (!r.Forced)
            {
                float pFalse = (auditoryShown >= Band.Band3 ? 0.4f : 0.3f) * DirectorMoods.FalseScale(Mood);
                if (_rng.NextDouble() < pFalse)
                {
                    float d = Range(1f, 8f) * scale;
                    r.FalseCount++;
                    r.RetryAt = Now + d + FalseRetry;
                    Emit(DirectionEventKind.Encounter, DirectionPhase.FalseForeshadow, r.Def.Id, string.Empty, r.Script.Space, r.Def.Intensity, r.Point, d, "헛예고");
                    return;
                }
            }

            float length = r.Forced ? 2f : Range(1f, 8f) * scale;
            if (_restarts > 0 && r.Def.Intensity >= 4 && _seenHigh.Contains(r.Def.Id)) length *= 0.5f;   // 이미 본 강도 4↑의 전조는 절반.
            r.State = EncounterRunState.Foreshadow;
            r.PhaseEnds = Now + length;
            Emit(DirectionEventKind.Encounter, DirectionPhase.Foreshadow, r.Def.Id, string.Empty, r.Script.Space, r.Def.Intensity, r.Point, length, string.Empty);
        }

        private void Confront(EncounterRun r)
        {
            EncounterScript s = r.Script;
            if (!r.Forced) _budget.Commit(r.Def.Intensity, Now);
            if (r.Def.Intensity >= 4) _seenHigh.Add(r.Def.Id);

            float window;
            if (s.IsPresence)
            {
                if (r.Forced) window = ForcedPresenceSeconds;
                else
                {
                    float from, to;
                    SlotWindow(r.Slot, out from, out to);
                    window = NightClock.RealSecondsAt(to) - NightClock.RealSecondsAt(_minute);
                    if (window < 1f) window = 1f;
                }
            }
            else
            {
                window = s.Window * DirectionWindowScale();
            }

            r.State = EncounterRunState.Active;
            r.PhaseEnds = Now + window;
            r.ConfrontMinute = _minute;
            if (s.Placement != CuePlacement.None && r.Point == Vector3.zero) r.Point = PointFor(s);

            Emit(DirectionEventKind.Encounter, DirectionPhase.Confront, r.Def.Id, s.Cue, s.Space, r.Def.Intensity, r.Point, window, string.Empty);
            if (s.Cue.Length > 0) _out.Enqueue(JudgeSignal.Cue(s.Cue, r.Point));
            if (s.ExtraCue.Length > 0) _out.Enqueue(JudgeSignal.Cue(s.ExtraCue, r.Point));
        }

        private float DirectionWindowScale()
        {
            return DirectorMoods.WindowScale(Mood);
        }

        private void Release(EncounterRun r)
        {
            EncounterScript s = r.Script;
            if (s.ReleaseCue.Length > 0) _out.Enqueue(JudgeSignal.Cue(s.ReleaseCue, r.Point));
            if (s.Cue.Length > 0) _out.Enqueue(JudgeSignal.CueEnd(s.Cue));
            if (s.ExtraCue.Length > 0) _out.Enqueue(JudgeSignal.CueEnd(s.ExtraCue));
            r.State = EncounterRunState.Releasing;
            r.PhaseEnds = Now + s.After;
            Emit(DirectionEventKind.Encounter, DirectionPhase.WindowClose, r.Def.Id, s.Cue, s.Space, r.Def.Intensity, r.Point, s.After, string.Empty);
        }

        private void Finish(EncounterRun r)
        {
            r.State = EncounterRunState.Done;
            _encountersDone++;
            if (!r.Forced) _budget.EndEncounter(Now, Range(SurpriseBudget.QuietMin, SurpriseBudget.QuietMax));
            Emit(DirectionEventKind.Encounter, DirectionPhase.Result, r.Def.Id, r.Script.Cue, r.Script.Space, r.Def.Intensity, r.Point, 0f, string.Empty);
        }

        private void AdvanceRuns(float minute)
        {
            for (int i = 0; i < _runs.Count; i++)
            {
                EncounterRun r = _runs[i];
                switch (r.State)
                {
                    case EncounterRunState.Foreshadow:
                        if (Now >= r.PhaseEnds) Confront(r);
                        break;
                    case EncounterRunState.Active:
                        if (Now >= r.PhaseEnds) Release(r);
                        break;
                    case EncounterRunState.Releasing:
                        if (Now >= r.PhaseEnds) Finish(r);
                        break;
                    case EncounterRunState.Waiting:
                        if (!r.Forced && PastSlot(r, minute))
                        {
                            r.State = EncounterRunState.Missed;
                            Note(r.Def.Id, "슬롯 " + r.Slot + "이 끝날 때까지 방아쇠가 오지 않음(" + r.Waiting + ")");
                        }

                        break;
                }
            }
        }

        private static bool PastSlot(EncounterRun r, float minute)
        {
            float from, to;
            SlotWindow(r.Slot, out from, out to);
            return minute >= to;
        }

        /// <summary>대본의 방에 있는가. <see cref="EncounterScript.ExactSpace"/>가 있으면 그 방(정규화 전)이어야 한다.</summary>
        private bool InScriptSpace(EncounterScript s)
        {
            if (_space != s.Space) return false;
            return s.ExactSpace == SpaceId.None || _exact == s.ExactSpace;
        }

        private Vector3 PointFor(EncounterScript s)
        {
            Vector3 fixedPoint;
            if (s.StageAnchor.Length > 0 && StagePoints.TryGet(s.StageAnchor, out fixedPoint)) return fixedPoint;   // 씬이 정한 고정 자리.
            if (!_hasPose) return Vector3.zero;
            float rad = _yaw * Mathf.Deg2Rad;
            Vector3 fwd = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));
            switch (s.Placement)
            {
                case CuePlacement.BehindPlayer:
                    return _feet - fwd * s.Distance;
                case CuePlacement.AheadOfPlayer:
                    return _feet + fwd * s.Distance;
                case CuePlacement.CeilingAhead:
                    return _feet + fwd * s.Distance + Vector3.up * 2.4f;
            }

            return _feet;
        }

        // ── 수칙 단서 ───────────────────────────────────────────

        private void TryRuleTriggers(float minute)
        {
            if (!NightClock.IsJudging(minute) || Now - _lastRuleCue < RuleGap) return;

            for (int i = 0; i < _rules.Count; i++)
            {
                RuleTriggerRun r = _rules[i];
                if (r.Done || r.Running) continue;
                if (!RuleReady(r)) continue;
                FireRule(r);
                return;
            }
        }

        private bool RuleReady(RuleTriggerRun r)
        {
            RuleTriggerScript s = r.Script;
            if (s.WhileViewingCctv) return ViewingCctv && Now - _cctvSince >= r.NeedDwell;

            if (s.Zone.Length > 0)
            {
                float since;
                return _zones.Contains(s.Zone) && _zoneSince.TryGetValue(s.Zone, out since) && Now - since >= r.NeedDwell;
            }

            return _space == s.Space && Now - _spaceSince >= r.NeedDwell;
        }

        private void FireRule(RuleTriggerRun r)
        {
            RuleTriggerScript s = r.Script;
            string target = s.Target;
            if (target == "cctv") target = OtherChannel();
            string cue = target.Length > 0 ? s.Cue + "@" + target : s.Cue;

            r.SentCue = cue;
            r.FiredMinute = _minute;
            _lastRuleCue = Now;
            _out.Enqueue(JudgeSignal.Cue(cue, _feet));
            Emit(DirectionEventKind.RuleCue, DirectionPhase.None, s.RuleId, cue, s.Space, 0, _feet, s.Duration, string.Empty);

            if (s.Duration <= 0f && !s.EndOnExit)
            {
                EndRule(r);
                return;
            }

            r.Running = true;
            r.EndsAt = s.EndOnExit ? float.PositiveInfinity : Now + s.Duration;
        }

        private void EndRule(RuleTriggerRun r)
        {
            _out.Enqueue(JudgeSignal.CueEnd(r.SentCue));
            r.Running = false;
            r.Done = true;
            Emit(DirectionEventKind.RuleCueEnd, DirectionPhase.None, r.Script.RuleId, r.SentCue, r.Script.Space, 0, _feet, 0f, string.Empty);
        }

        private void AdvanceRules()
        {
            for (int i = 0; i < _rules.Count; i++)
            {
                RuleTriggerRun r = _rules[i];
                if (r.Running && Now >= r.EndsAt) EndRule(r);
            }
        }

        private void EndOnExit(SpaceId left)
        {
            for (int i = 0; i < _rules.Count; i++)
            {
                RuleTriggerRun r = _rules[i];
                if (r.Running && r.Script.EndOnExit && r.Script.Space == left) EndRule(r);
            }
        }

        private string OtherChannel()
        {
            int current = -1;
            if (_channel.StartsWith("cctv.ch")) int.TryParse(_channel.Substring(7), out current);
            int pick = _rng.Next(RuleTriggers.CctvChannels - 1);
            if (pick >= current && current >= 0) pick++;
            return "cctv.ch" + pick;
        }

        // ── 가짜 놀람 ───────────────────────────────────────────

        private void TryFakeScare(float minute)
        {
            if (!NightClock.IsJudging(minute) || Now < _nextFake) return;

            int cap = 2 + 3 * _encountersDone + DirectorMoods.ExtraFakes(Mood);
            if (_fakesUsed >= cap)
            {
                _nextFake = Now + FakeGapMin;
                return;
            }

            List<string> open = new List<string>();
            foreach (string id in FakeScares)
            {
                if (!FakeAllowedHere(id)) continue;
                int n;
                _fakeCount.TryGetValue(id, out n);
                if (n < FakePerId) open.Add(id);
            }

            _nextFake = Now + Range(FakeGapMin, FakeGapMax);
            if (open.Count == 0) return;

            string pick = open[_rng.Next(open.Count)];
            int used;
            _fakeCount.TryGetValue(pick, out used);
            _fakeCount[pick] = used + 1;
            _fakesUsed++;
            Emit(DirectionEventKind.FakeScare, DirectionPhase.None, pick, string.Empty, _space, 0, _feet, 0f, string.Empty);
        }

        /// <summary>
        /// 이 가짜 놀람을 지금 자리에서 걸 수 있는가. 벌레 떼만 조건이 있다: <see cref="BugSpaces"/>의 방에 <see cref="BugDwellSeconds"/>초 이상 있고,
        /// 그 방에 조우(존재형 포함)가 서 있지 않을 것 — 천장 다리·소녀·노란 얼굴 옆에 벌레가 쏟아져 시선을 끌면 응시 판정이 억울해진다.
        /// </summary>
        private bool FakeAllowedHere(string id)
        {
            if (id != FakeBugs) return true;
            if (Array.IndexOf(BugSpaces, _exact) < 0 || Now - _spaceSince < BugDwellSeconds) return false;
            for (int i = 0; i < _runs.Count; i++)
            {
                EncounterRun r = _runs[i];
                if (r.State == EncounterRunState.Waiting || r.State == EncounterRunState.Done || r.State == EncounterRunState.Missed) continue;
                if (SpaceIds.Canonical(r.Script.Space) == _space) return false;
            }

            return true;
        }

        // ── 중단·재시작·디버그 ─────────────────────────────────

        /// <summary>진행 중인 조우·단서를 끝 단서 없이 즉시 거둔다(붙잡힘·04:00).</summary>
        public void AbortAll(string reason)
        {
            for (int i = 0; i < _runs.Count; i++)
            {
                EncounterRun r = _runs[i];
                if (r.State != EncounterRunState.Foreshadow && r.State != EncounterRunState.Active && r.State != EncounterRunState.Releasing) continue;
                r.State = EncounterRunState.Done;
                Emit(DirectionEventKind.Encounter, DirectionPhase.Aborted, r.Def.Id, r.Script.Cue, r.Script.Space, r.Def.Intensity, r.Point, 0f, reason);
            }

            for (int i = 0; i < _rules.Count; i++)
            {
                RuleTriggerRun r = _rules[i];
                if (!r.Running) continue;
                r.Running = false;
                r.Done = true;
                Emit(DirectionEventKind.RuleCueEnd, DirectionPhase.Aborted, r.Script.RuleId, r.SentCue, r.Script.Space, 0, _feet, 0f, reason);
            }

            _out.Clear();
        }

        /// <summary>
        /// 밤 재시작(같은 밤을 다시) — 조우와 수칙은 같고 시점·위치만 바뀔 수 있다.
        /// <paramref name="startMinute"/>(00:00 또는 02:16) 이후에 일어난 조우·단서는 다시 기다리고, 그 전에 끝난 것은 그대로 둔다.
        /// 예산은 남은 「끝난 조우」로 다시 센다.
        /// </summary>
        public void ResetToRest(int restarts, float startMinute)
        {
            AbortAll("재시작");
            _restarts = restarts;
            _budget = new SurpriseBudget(_day);

            for (int i = 0; i < _runs.Count; i++)
            {
                EncounterRun r = _runs[i];
                bool before = r.ConfrontMinute >= 0f && r.ConfrontMinute < startMinute;
                if (before && !r.Forced)
                {
                    r.State = EncounterRunState.Done;
                    _budget.Commit(r.Def.Intensity, float.NegativeInfinity);
                    continue;
                }

                if (r.Forced) continue;
                r.State = EncounterRunState.Waiting;
                r.RetryAt = 0f;
                r.Waiting = string.Empty;
                r.ConfrontMinute = -1f;
                r.Point = Vector3.zero;
            }

            for (int i = 0; i < _rules.Count; i++)
            {
                RuleTriggerRun r = _rules[i];
                if (r.FiredMinute >= 0f && r.FiredMinute < startMinute) continue;
                r.Done = false;
                r.Running = false;
                r.FiredMinute = -1f;
                r.NeedDwell = Range(r.Script.DwellMin, r.Script.DwellMax);
            }

            _fakeCount.Clear();
            _fakesUsed = 0;
            _encountersDone = 0;
            _lastRuleCue = float.NegativeInfinity;
            _nextFake = Now + Range(FakeGapMin * 0.5f, FakeGapMax * 0.5f);
            _space = SpaceId.None;
            _exact = SpaceId.None;
            _zones.Clear();
            _walk = 0f;
            _hasPose = false;
        }

        /// <summary>
        /// 디버그: 그 조우를 지금 바로 시작한다(슬롯·방아쇠·예산·헛예고를 건너뜀, 전조 2초). 진행 중인 조우는 먼저 중단한다.
        /// 편성에 없던 조우면 새로 만든다. 존재형은 60초 머문다. 대본이 없으면 false.
        /// </summary>
        public bool ForceEncounter(string encounterId)
        {
            EncounterScript script = EncounterScripts.Find(encounterId);
            EncounterDef def = ProgramCatalog.Encounter(encounterId);
            if (script == null || def == null) return false;

            AbortAll("디버그 강제 실행");
            EncounterRun run = new EncounterRun { Slot = EncounterSlot.A, Def = def, Script = script, Forced = true };
            _runs.Add(run);
            Note(encounterId, "디버그 강제 실행");
            Begin(run, Band.Band0);
            return true;
        }

        /// <summary>디버그: 그 가짜 놀람을 지금 건다(예산·간격·방 조건을 건너뜀, 쓴 횟수에도 넣지 않음). 목록에 없으면 false.</summary>
        public bool ForceFake(string fakeId)
        {
            if (Array.IndexOf(FakeScares, fakeId) < 0) return false;
            Note(fakeId, "디버그 강제 실행");
            Emit(DirectionEventKind.FakeScare, DirectionPhase.None, fakeId, string.Empty, _space, 0, _feet, 0f, string.Empty);
            return true;
        }

        /// <summary>디버그: 그 수칙의 단서를 지금 울린다(조건·간격·「밤에 한 번」 무시). 대본이 없으면 false.</summary>
        public bool FireRuleNow(string ruleId)
        {
            RuleTriggerScript script = RuleTriggers.Find(ruleId);
            if (script == null) return false;

            RuleTriggerRun run = null;
            for (int i = 0; i < _rules.Count; i++)
            {
                if (_rules[i].Script.RuleId == ruleId) run = _rules[i];
            }

            if (run == null)
            {
                run = new RuleTriggerRun { Script = script };
                _rules.Add(run);
            }

            if (run.Running) EndRule(run);
            run.Done = false;
            FireRule(run);
            return true;
        }

        /// <summary>디버그: 울리는 중인 수칙 단서를 지금 끝낸다.</summary>
        public bool EndRuleNow(string ruleId)
        {
            for (int i = 0; i < _rules.Count; i++)
            {
                if (_rules[i].Script.RuleId == ruleId && _rules[i].Running)
                {
                    EndRule(_rules[i]);
                    return true;
                }
            }

            return false;
        }

        /// <summary>디버그: 진행 중인 조우를 지금 다음 단계로 넘긴다(전조 → 대면 → 끝 → 결과).</summary>
        public void SkipPhase()
        {
            for (int i = 0; i < _runs.Count; i++)
            {
                EncounterRun r = _runs[i];
                if (r.State == EncounterRunState.Foreshadow || r.State == EncounterRunState.Active || r.State == EncounterRunState.Releasing)
                {
                    r.PhaseEnds = Now;
                }
            }
        }

        // ── 도구 ───────────────────────────────────────────────

        private void Note(string source, string text)
        {
            string key = source + "|" + text;
            if (key == _lastNote) return;
            _lastNote = key;
            Emit(DirectionEventKind.Note, DirectionPhase.None, source, string.Empty, _space, 0, Vector3.zero, 0f, text);
        }

        private void Emit(DirectionEventKind kind, DirectionPhase phase, string source, string cue, SpaceId space, int intensity, Vector3 point, float duration, string text)
        {
            Action<DirectionEvent> h = Emitted;
            if (h == null) return;
            DirectionEvent e = new DirectionEvent(kind, phase, source, cue, space, intensity, point, duration, text);
            foreach (Delegate d in h.GetInvocationList())
            {
                try { ((Action<DirectionEvent>)d)(e); }
                catch (Exception ex) { Debug.LogException(ex); }
            }
        }

        private float Range(float min, float max)
        {
            return min + (float)_rng.NextDouble() * (max - min);
        }
    }
}

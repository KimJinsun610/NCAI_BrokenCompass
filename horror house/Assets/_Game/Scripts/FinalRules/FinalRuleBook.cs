using System;
using System.Collections.Generic;
using UnityEngine;

namespace NightDuty
{
    /// <summary>새 수칙 한 건의 정산.</summary>
    public enum FinalOutcome
    {
        /// <summary>위반(일반 +12 — 밤당 한 번, 위협 +20 — 조우마다).</summary>
        Violated = 0,

        /// <summary>위협 대응 성공(신뢰 +3, 조우마다).</summary>
        ThreatKept = 1,

        /// <summary>밤 종료 준수(방아쇠가 한 번 이상 왔고 위반이 없는 일반 수칙, 신뢰 +2).</summary>
        Complied = 2,

        /// <summary>델타 없는 기록(G2·T4처럼 점검판이 이미 델타를 준 경우).</summary>
        Noted = 3
    }

    /// <summary>새 수칙 정산 결과 한 건. 근무일지·재시작 카드·위반 피드백이 읽는다(수치는 화면에 내지 않는다).</summary>
    public readonly struct FinalRuleResult
    {
        /// <summary>수칙 ID.</summary>
        public readonly string RuleId;

        /// <summary>정산 종류.</summary>
        public readonly FinalOutcome Outcome;

        /// <summary>델타가 걸린 축.</summary>
        public readonly FearAxis Axis;

        /// <summary>델타(없으면 0).</summary>
        public readonly int Delta;

        /// <summary>사유(개발 로그).</summary>
        public readonly string Reason;

        /// <summary>만든다.</summary>
        public FinalRuleResult(string ruleId, FinalOutcome outcome, FearAxis axis, int delta, string reason)
        {
            RuleId = ruleId;
            Outcome = outcome;
            Axis = axis;
            Delta = delta;
            Reason = reason ?? string.Empty;
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return RuleId + " " + Outcome + " " + Axis + " +" + Delta + " (" + Reason + ")";
        }
    }

    /// <summary>새 수칙 판정이 공유하는 현재 상태. <see cref="FinalRuleBook"/>이 모든 신호로 갱신한다(판정 정지 중에도).</summary>
    public sealed class FinalWorld
    {
        /// <summary>현재 공간(옛 교실 값은 교실로 읽는다).</summary>
        public SpaceId Space { get; internal set; }

        /// <summary>손전등이 켜져 있는지.</summary>
        public bool Flashlight { get; internal set; }

        /// <summary>발밑 위치(자세 샘플을 한 번이라도 받았으면 유효).</summary>
        public Vector3 Feet { get; internal set; }

        /// <summary>바라보는 수평 방향(도).</summary>
        public float Yaw { get; internal set; }

        /// <summary>자세 샘플을 받았는지.</summary>
        public bool HasPose { get; internal set; }

        /// <summary>달리는 중인지.</summary>
        public bool Running { get; internal set; }

        /// <summary>판정 시간(ms) — 판정 구간의 Tick 누적. 수칙의 제한 시간은 이 시계로 잰다.</summary>
        public int NowMs { get; internal set; }

        /// <summary>초를 밀리초 정수로 바꾼다. 판정 시간은 ms 정수로 잰다(부동소수 누적 오차를 피한다).</summary>
        public static int ToMs(float seconds)
        {
            return Mathf.RoundToInt(seconds * 1000f);
        }

        /// <summary>바라보는 수평 방향 단위 벡터.</summary>
        public Vector3 Forward
        {
            get
            {
                float r = Yaw * Mathf.Deg2Rad;
                return new Vector3(Mathf.Sin(r), 0f, Mathf.Cos(r));
            }
        }

        internal void Apply(in JudgeSignal s)
        {
            switch (s.Kind)
            {
                case SignalKind.SpaceEntered:
                    Space = SpaceIds.Canonical(s.Space);
                    break;
                case SignalKind.SpaceExited:
                    if (Space == SpaceIds.Canonical(s.Space)) Space = SpaceId.None;
                    break;
                case SignalKind.FlashlightChanged:
                    Flashlight = s.Flag;
                    break;
                case SignalKind.PlayerPose:
                    Feet = s.Point;
                    Yaw = s.Value;
                    HasPose = true;
                    break;
                case SignalKind.Running:
                    Running = s.Flag;
                    break;
            }
        }
    }

    /// <summary>
    /// 새 수칙 한 장의 판정기. 수칙마다 하나(<see cref="FinalJudges"/>). 판정 구간의 신호만 받는다(상태는 <see cref="FinalWorld"/>가 늘 갱신).
    /// <para>
    /// 에피소드(단서 하나에 대한 대응)는 판정기 필드에 둔다 — 조건 에셋과 달리 판정기는 밤마다 새로 만드는 인스턴스라 값이 새지 않는다.
    /// 재시작 때는 <see cref="ResetEpisode"/>로 진행 중이던 에피소드만 버리고 「방아쇠가 왔는지·이미 어겼는지」는 스냅샷으로 되돌린다.
    /// </para>
    /// </summary>
    public abstract class FinalJudge
    {
        /// <summary>수칙 정의.</summary>
        public RuleDef Def { get; internal set; }

        /// <summary>판정 책.</summary>
        protected internal FinalRuleBook Book { get; internal set; }

        /// <summary>그 밤 방아쇠가 한 번 이상 왔는지.</summary>
        public bool Triggered { get; internal set; }

        /// <summary>그 밤 위반했는지(일반 수칙은 첫 위반에서 한 번만 물린다).</summary>
        public bool Violated { get; internal set; }

        /// <summary>지금 상태 한 줄(디버그 콘솔). 진행 중인 에피소드가 없으면 빈 문자열.</summary>
        public virtual string Status
        {
            get { return string.Empty; }
        }

        /// <summary>판정 구간의 신호 하나.</summary>
        internal abstract void Observe(in JudgeSignal s, FinalWorld w);

        /// <summary>진행 중이던 에피소드를 버린다(재시작).</summary>
        internal virtual void ResetEpisode()
        {
        }

        /// <summary>방아쇠 표시.</summary>
        protected void Trigger()
        {
            Triggered = true;
        }

        /// <summary>위반을 알린다.</summary>
        protected void Violate(string reason)
        {
            Book.ReportViolation(this, reason);
        }

        /// <summary>위협 대응 성공을 알린다.</summary>
        protected void Keep(string reason)
        {
            Book.ReportThreatKept(this, reason);
        }

        /// <summary>단서 ID가 맞는지(<c>@</c> 뒤 대상은 무시).</summary>
        protected static bool IsCue(in JudgeSignal s, SignalKind kind, string cueId)
        {
            if (s.Kind != kind) return false;
            string id = s.TargetId;
            int at = id.IndexOf('@');
            return (at < 0 ? id : id.Substring(0, at)) == cueId;
        }

        /// <summary>단서 ID의 <c>@</c> 뒤 대상. 없으면 빈 문자열.</summary>
        protected static string CueTarget(in JudgeSignal s)
        {
            int at = s.TargetId.IndexOf('@');
            return at < 0 ? string.Empty : s.TargetId.Substring(at + 1);
        }
    }

    /// <summary>
    /// 새 수칙 판정 책(2026-09-30 최종 기획서 「공간별 설계」·「위협 수칙과 신뢰」). 그날 편성(<see cref="NightProgram"/>)의 덱을 판정한다.
    /// <list type="bullet">
    /// <item>일반 수칙: 첫 위반에서 그 축 +12 한 번. 방아쇠가 한 번 이상 왔고 위반이 없으면 밤 종료에 신뢰 +2.</item>
    /// <item>위협 수칙(H3·H4·C3·S5·T3·L3·L5): 조우마다 실패 +20 / 성공 신뢰 +3(+12·+2를 더하지 않음).</item>
    /// <item>G2·T4는 점검판이 델타를 준다 — 여기서는 기록만(<see cref="NoteExternal"/>).</item>
    /// </list>
    /// 판정 구간 밖에서는 상태만 갱신하고 판정하지 않는다(부르는 쪽이 <c>judging</c>으로 알린다). 델타 상한(95)은 부르는 쪽이 건다.
    /// </summary>
    public sealed class FinalRuleBook : ISnapshotable
    {
        /// <summary>스냅샷 키.</summary>
        public const string Key = "final-rules";

        private readonly List<FinalJudge> _judges = new List<FinalJudge>();
        private readonly List<FinalRuleResult> _results = new List<FinalRuleResult>();
        private readonly FearAxisSystem _axes;
        private bool _ended;

        /// <summary>현재 상태.</summary>
        public FinalWorld World { get; } = new FinalWorld();

        /// <summary>씬 기준점 위치를 찾는다(기본: <see cref="JudgeTargetRegistry"/>). 테스트가 바꾼다.</summary>
        public Func<string, Vector3?> Anchor { get; set; }

        /// <summary>정산 한 건이 났다.</summary>
        public event Action<FinalRuleResult> Settled;

        /// <summary>T4 역보고를 걸어야 할 때(여자아이가 칸으로 들어가는 것을 봤다). 인자: 점검 항목 ID.</summary>
        public event Action<string> ReverseReportArmed;

        /// <summary>판정기 목록(덱 순).</summary>
        public IReadOnlyList<FinalJudge> Judges
        {
            get { return _judges; }
        }

        /// <summary>정산 결과(순서대로).</summary>
        public IReadOnlyList<FinalRuleResult> Results
        {
            get { return _results; }
        }

        /// <inheritdoc/>
        public string SnapshotKey
        {
            get { return Key; }
        }

        /// <summary>그날 덱으로 만든다.</summary>
        public FinalRuleBook(IEnumerable<RuleDef> deck, FearAxisSystem axes)
        {
            _axes = axes ?? throw new ArgumentNullException(nameof(axes));
            Anchor = RegistryAnchor;
            if (deck == null) return;

            foreach (RuleDef def in deck)
            {
                FinalJudge j = FinalJudges.Create(def);
                if (j == null) continue;
                j.Def = def;
                j.Book = this;
                _judges.Add(j);
            }
        }

        /// <summary>판정기를 하나 더 붙인다(디버그 — 덱에 수칙 추가). 판정기가 없거나 이미 있으면 false.</summary>
        public bool AddJudge(RuleDef def)
        {
            if (def == null || Judge(def.Id) != null) return false;
            FinalJudge j = FinalJudges.Create(def);
            if (j == null) return false;
            j.Def = def;
            j.Book = this;
            _judges.Add(j);
            return true;
        }

        /// <summary>그 수칙의 판정기. 없으면 null.</summary>
        public FinalJudge Judge(string ruleId)
        {
            for (int i = 0; i < _judges.Count; i++)
            {
                if (_judges[i].Def.Id == ruleId) return _judges[i];
            }

            return null;
        }

        /// <summary>신호 하나. <paramref name="judging"/>이 false면 상태만 갱신한다.</summary>
        public void Dispatch(in JudgeSignal s, bool judging)
        {
            if (_ended) return;

            World.Apply(s);
            if (!judging || _axes.IsLocked) return;

            if (s.Kind == SignalKind.Tick)
            {
                World.NowMs += FinalWorld.ToMs(s.Value);
            }

            for (int i = 0; i < _judges.Count; i++)
            {
                try
                {
                    _judges[i].Observe(s, World);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }

                if (_axes.IsLocked) return;
            }
        }

        /// <summary>밤 종료 정산 — 방아쇠가 왔고 위반이 없는 일반 수칙에 신뢰 +2.</summary>
        public void EndNight()
        {
            if (_ended) return;
            _ended = true;

            for (int i = 0; i < _judges.Count; i++)
            {
                FinalJudge j = _judges[i];
                if (j.Def.IsThreat || !j.Def.HasAxis || !j.Triggered || j.Violated) continue;
                if (j.Def.Id == "G2" || j.Def.Id == ProgramCatalog.ReverseReportRule) continue;   // 점검판 몫.
                Commit(new FinalRuleResult(j.Def.Id, FinalOutcome.Complied, FearAxis.Trust, Deltas.TrustComply, "밤 종료 준수"));
            }
        }

        /// <summary>
        /// 점검판이 델타를 이미 준 결과를 수칙 기록에 남긴다(G2 환청 [이상] → 위반, T4 역보고 → 준수/위반).
        /// </summary>
        public void NoteExternal(string ruleId, bool violated, string reason)
        {
            FinalJudge j = Judge(ruleId);
            if (j == null) return;
            j.Triggered = true;
            if (violated) j.Violated = true;
            AddResult(new FinalRuleResult(ruleId, FinalOutcome.Noted, FearAxis.Trust, 0, reason));
        }

        internal void ReportViolation(FinalJudge j, string reason)
        {
            j.Triggered = true;
            if (j.Def.IsThreat)
            {
                j.Violated = true;
                Commit(new FinalRuleResult(j.Def.Id, FinalOutcome.Violated, j.Def.Axis, Deltas.ThreatFailure, reason));
                return;
            }

            if (j.Violated) return;   // 일반 수칙은 첫 위반에서 한 번만.
            j.Violated = true;
            Commit(new FinalRuleResult(j.Def.Id, FinalOutcome.Violated, j.Def.Axis, Deltas.RuleViolation, reason));
        }

        internal void ReportThreatKept(FinalJudge j, string reason)
        {
            j.Triggered = true;
            Commit(new FinalRuleResult(j.Def.Id, FinalOutcome.ThreatKept, FearAxis.Trust, Deltas.TrustThreatSuccess, reason));
        }

        internal void ArmReverseReport(string itemId)
        {
            Action<string> h = ReverseReportArmed;
            if (h == null) return;
            try { h(itemId); }
            catch (Exception e) { Debug.LogException(e); }
        }

        /// <summary>판정이 다음 밤 조우를 요청했다(S3 위반 → 모형 급습, L3 실패 → 정장 남자). 인자: 조우 ID.</summary>
        public event Action<string> EncounterRequested;

        internal void RequestEncounter(string encounterId)
        {
            Action<string> h = EncounterRequested;
            if (h == null) return;
            try { h(encounterId); }
            catch (Exception e) { Debug.LogException(e); }
        }

        internal Vector3? AnchorOf(string id)
        {
            return Anchor != null ? Anchor(id) : null;
        }

        private void Commit(FinalRuleResult r)
        {
            if (r.Delta > 0)
            {
                _axes.Apply(r.Axis, r.Delta, r.RuleId, World.Space);
            }

            AddResult(r);
        }

        private void AddResult(FinalRuleResult r)
        {
            _results.Add(r);
            Action<FinalRuleResult> h = Settled;
            if (h == null) return;
            foreach (Delegate d in h.GetInvocationList())
            {
                try { ((Action<FinalRuleResult>)d)(r); }
                catch (Exception e) { Debug.LogException(e); }
            }
        }

        private static Vector3? RegistryAnchor(string id)
        {
            JudgeTarget t;
            if (JudgeTargetRegistry.TryGet(id, out t) && t != null) return t.AnchorPosition;
            return null;
        }

        // ── 스냅샷 ─────────────────────────────────────────────

        /// <inheritdoc/>
        public object CaptureState()
        {
            State s = new State { ResultCount = _results.Count, Triggered = new bool[_judges.Count], Violated = new bool[_judges.Count] };
            for (int i = 0; i < _judges.Count; i++)
            {
                s.Triggered[i] = _judges[i].Triggered;
                s.Violated[i] = _judges[i].Violated;
            }

            return s;
        }

        /// <inheritdoc/>
        public void RestoreState(object state)
        {
            State s = state as State;
            if (s == null) return;

            for (int i = 0; i < _judges.Count && i < s.Triggered.Length; i++)
            {
                _judges[i].Triggered = s.Triggered[i];
                _judges[i].Violated = s.Violated[i];
                _judges[i].ResetEpisode();
            }

            if (_results.Count > s.ResultCount) _results.RemoveRange(s.ResultCount, _results.Count - s.ResultCount);
            _ended = false;
            World.Running = false;
        }

        private sealed class State
        {
            public int ResultCount;
            public bool[] Triggered;
            public bool[] Violated;
        }
    }
}

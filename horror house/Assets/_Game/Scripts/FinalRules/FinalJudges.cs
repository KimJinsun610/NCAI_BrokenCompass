using UnityEngine;

namespace NightDuty
{
    /// <summary>
    /// 새 수칙 판정기의 단서·기준점 계약(연출·씬이 지키는 이름). 최종 기획서 「공간별 설계」의 수칙 문장을 판정 가능한 사건으로 옮긴 것이다.
    /// <para>단서는 <see cref="JudgeSignal.Cue"/>로 시작하고 같은 ID의 <see cref="JudgeSignal.CueEnd"/>로 끝난다. 기준점은 <see cref="JudgeTarget"/> ID다.</para>
    /// </summary>
    public static class FinalCues
    {
        // 복도
        public const string H1Object = "rule.H1.object";
        public const string Footsteps = "cue.footsteps";
        public const string Voice = "cue.voice";

        // 교실
        public const string Chalk = "cue.chalk";
        public const string Legs = "cue.legs";
        public const string LegsTarget = "rule.C2.legs";
        public const string BoySeated = "cue.boy.seated";
        public const string Bell = "cue.bell";
        public const string RedLight = "cue.redlight";
        public const string PhantomDoor = "cue.phantomdoor";
        public const string PhantomDoorZone = "classroom.phantomdoor";

        // 과학실
        public const string S1Center = "rule.S1.center";
        public const string Glass = "cue.glass";
        public const string ModelTarget = "rule.S3.model";
        public const string TapeZone = "science.tape";
        public const string ScienceBlackout = "cue.blackout.science";
        public const string ScienceDarkZone = "science.dark";
        public const string HallEnd = "cue.hallend";

        // 화장실
        public const string Flush = "cue.flush";
        public const string StallOccupied = "cue.stall.occupied";
        public const string ToiletBlackout = "cue.blackout.toilet";
        public const string GirlStall = "cue.girl.stall";
        public const string StallLit = "cue.stall.lit";

        // 도서관
        public const string L1Shelf = "rule.L1.shelf";
        public const string Pages = "cue.pages";
        public const string YellowFace = "cue.yellowface";
        public const string FaceTarget = "rule.L3.face";
        public const string L4Box = "rule.L4.box";
        public const string WindowKnock = "cue.windowknock";
        public const string ManTarget = "rule.L5.man";

        // 경비실
        public const string CctvPerson = "cue.cctvperson";
        public const string EmptyRoom = "cue.emptyroom";
    }

    /// <summary>수칙 ID → 판정기. 판정 조건이 없는 수칙(G3·K4)은 기록만 하는 판정기를 받는다.</summary>
    public static class FinalJudges
    {
        /// <summary>그 수칙의 새 판정기. 모르는 ID면 null.</summary>
        public static FinalJudge Create(RuleDef def)
        {
            if (def == null) return null;
            switch (def.Id)
            {
                case "H1": return new ProximityAvoidJudge(FinalCues.H1Object, 1.2f);
                case "H2": return new DoorKeepOpenJudge();
                case "H3": return new TakeShelterJudge(FinalCues.Footsteps, 6f, 2f);
                case "H4": return new DontAnswerJudge(FinalCues.Voice);
                case "C1": return new NoEntryDuringCueJudge(FinalCues.Chalk, SpaceId.Classroom, 1f);
                case "C2": return new DontStareJudge(FinalCues.Legs, FinalCues.LegsTarget, 3f);
                case "C3": return new StayStillJudge(FinalCues.BoySeated, FinalCues.Bell);
                case "C4": return new LightKeepJudge(FinalCues.RedLight, null, -1f, false);
                case "C5": return new ZoneForbiddenJudge(FinalCues.PhantomDoor, FinalCues.PhantomDoorZone);
                case "S1": return new NoPassageJudge(FinalCues.S1Center);
                case "S2": return new ExitWithinJudge(FinalCues.Glass, 10f, true);
                case "S3": return new LitGazeJudge(FinalCues.ModelTarget, 2f, ProgramCatalog.ModelRush);   // 2026-10-04 민: 「인체 모형을 빛으로 확인하십시오.」
                case "S4": return new LightKeepJudge(FinalCues.ScienceBlackout, FinalCues.ScienceDarkZone, 1f, false);
                case "S5": return new WaitInDarkJudge(FinalCues.HallEnd, 3f);
                case "T1": return new ExitWithinJudge(FinalCues.Flush, 8f, false);
                case "T2": return new ZoneForbiddenJudge(FinalCues.StallOccupied, null);
                case "T3": return new WaitInDarkJudge(FinalCues.ToiletBlackout, 3f);
                case "T4": return new ReverseReportJudge();
                case "T5": return new LightKeepJudge(FinalCues.StallLit, null, 3f, true);
                case "L1": return new DwellNearJudge(FinalCues.L1Shelf, 1.5f, 3f);
                case "L2": return new ExitWithinJudge(FinalCues.Pages, 12f, false);
                case "L3": return new KeepBeamJudge(FinalCues.YellowFace, FinalCues.FaceTarget, 2f, 0.5f, ProgramCatalog.SuitMan);
                case "L4": return new ProximityAvoidJudge(FinalCues.L4Box, 1.5f);
                case "L5": return new DontGreetJudge(FinalCues.WindowKnock, FinalCues.ManTarget, 2f);
                case "K1": return new NoChannelChangeJudge(FinalCues.CctvPerson);
                case "K2": return new DontWatchJudge(FinalCues.EmptyRoom, 3f);
                case "K3": return new DwellSpaceJudge(SpaceId.SecurityRoom, 45f);
                case "G1": return new NoRunningJudge(1f);
                case "G3": return new RecordOnlyJudge();
                case "K4": return new RecordOnlyJudge();
            }

            return null;
        }
    }

    // ── 공통 도구 ──────────────────────────────────────────

    /// <summary>샘플 연속 시간(틈 허용). 응시·비춤·시청 샘플을 이어 붙인다.</summary>
    internal struct SampleStreak
    {
        private float _run;
        private float _gap;

        /// <summary>이어진 시간(초).</summary>
        public float Seconds
        {
            get { return _run; }
        }

        /// <summary>샘플 하나. <paramref name="hit"/>이면 이어 붙이고, 아니면 틈이 <paramref name="gapTolerance"/>를 넘을 때 끊는다.</summary>
        public float Feed(bool hit, float dt, float gapTolerance)
        {
            if (hit)
            {
                _run += dt;
                _gap = 0f;
            }
            else
            {
                _gap += dt;
                if (_gap > gapTolerance) _run = 0f;
            }

            return _run;
        }

        /// <summary>비운다.</summary>
        public void Clear()
        {
            _run = 0f;
            _gap = 0f;
        }
    }

    /// <summary>단서 하나를 에피소드로 다루는 판정기의 바탕(시작·끝·진행 여부).</summary>
    public abstract class CueEpisodeJudge : FinalJudge
    {
        /// <summary>방아쇠 단서 ID.</summary>
        protected readonly string CueId;

        /// <summary>에피소드 진행 중.</summary>
        protected bool Active;

        /// <summary>이 에피소드에서 이미 정산했는지(위협 수칙은 에피소드마다 한 번).</summary>
        protected bool Settled;

        /// <summary>시작 시각(판정 ms).</summary>
        protected int StartMs;

        /// <inheritdoc/>
        public override string Status
        {
            get { return Active ? "단서 진행 중 " : string.Empty; }
        }

        /// <summary>만든다.</summary>
        protected CueEpisodeJudge(string cueId)
        {
            CueId = cueId;
        }

        internal override void Observe(in JudgeSignal s, FinalWorld w)
        {
            if (IsCue(s, SignalKind.CueStarted, CueId))
            {
                Active = true;
                Settled = false;
                StartMs = w.NowMs;
                if (OnStart(s, w)) Trigger();
                else Active = false;
                return;
            }

            if (!Active) return;

            if (IsCue(s, SignalKind.SequenceEnded, CueId))
            {
                OnEnd(w);
                return;
            }

            OnSignal(s, w);
        }

        internal override void ResetEpisode()
        {
            Active = false;
            Settled = false;
        }

        /// <summary>시작. false면 이 단서는 이 플레이어에게 해당하지 않는다(방아쇠 아님).</summary>
        protected virtual bool OnStart(in JudgeSignal s, FinalWorld w)
        {
            return true;
        }

        /// <summary>단서 끝.</summary>
        protected virtual void OnEnd(FinalWorld w)
        {
            Active = false;
        }

        /// <summary>진행 중 신호.</summary>
        protected abstract void OnSignal(in JudgeSignal s, FinalWorld w);

        /// <summary>에피소드 실패로 끝낸다.</summary>
        protected void Fail(string reason)
        {
            if (Settled) return;
            Settled = true;
            Active = false;
            Violate(reason);
        }

        /// <summary>에피소드 성공으로 끝낸다(위협 수칙만 신뢰 +3).</summary>
        protected void Pass(string reason)
        {
            if (Settled) return;
            Settled = true;
            Active = false;
            if (Def.IsThreat) Keep(reason);
        }

        /// <summary>경과(초).</summary>
        protected float Elapsed(FinalWorld w)
        {
            return (w.NowMs - StartMs) / 1000f;
        }
    }

    // ── 판정기 ────────────────────────────────────────────

    /// <summary>H1·L4: 기준점 반경 안으로 들어가면 위반. 반경의 3배 안으로 오면 방아쇠(피할 기회가 있었다).</summary>
    public sealed class ProximityAvoidJudge : FinalJudge
    {
        private readonly string _anchor;
        private readonly float _radius;

        /// <summary>만든다.</summary>
        public ProximityAvoidJudge(string anchor, float radius)
        {
            _anchor = anchor;
            _radius = radius;
        }

        internal override void Observe(in JudgeSignal s, FinalWorld w)
        {
            if (s.Kind != SignalKind.PlayerPose) return;
            Vector3? a = Book.AnchorOf(_anchor);
            if (!a.HasValue) return;
            float d = SensingRules.HorizontalDistance(s.Point, a.Value);

            if (d < _radius * 3f) Trigger();
            if (d < _radius) Violate(_anchor + " 반경 " + _radius + "m 진입");
        }
    }

    /// <summary>L1: 기준점 반경 안에 연속으로 오래 서 있으면 위반.</summary>
    public sealed class DwellNearJudge : FinalJudge
    {
        private readonly string _anchor;
        private readonly float _radius;
        private readonly float _limit;
        private bool _inside;
        private float _dwell;

        /// <summary>만든다.</summary>
        public DwellNearJudge(string anchor, float radius, float limitSeconds)
        {
            _anchor = anchor;
            _radius = radius;
            _limit = limitSeconds;
        }

        internal override void Observe(in JudgeSignal s, FinalWorld w)
        {
            if (s.Kind == SignalKind.PlayerPose)
            {
                Vector3? a = Book.AnchorOf(_anchor);
                if (!a.HasValue) return;
                float d = SensingRules.HorizontalDistance(s.Point, a.Value);

                if (d < _radius * 2f) Trigger();
                _inside = d < _radius;
                if (!_inside) _dwell = 0f;
                return;
            }

            if (s.Kind == SignalKind.Tick && _inside)
            {
                _dwell += s.Value;
                if (_dwell >= _limit) Violate(_anchor + " 곁 " + _limit + "초");
            }
        }

        internal override void ResetEpisode()
        {
            _inside = false;
            _dwell = 0f;
        }
    }

    /// <summary>H2: 연출이 연 문(플레이어가 관찰)을 플레이어가 닫으면 위반.</summary>
    public sealed class DoorKeepOpenJudge : FinalJudge
    {
        private readonly System.Collections.Generic.HashSet<string> _opened = new System.Collections.Generic.HashSet<string>();

        internal override void Observe(in JudgeSignal s, FinalWorld w)
        {
            if (s.Kind == SignalKind.DoorAutoOpenObserved)
            {
                _opened.Add(s.TargetId);
                Trigger();
                return;
            }

            if (s.Kind == SignalKind.DoorCommandAccepted && s.Flag && s.Source == ActionSource.Player && _opened.Contains(s.TargetId))
            {
                Violate(s.TargetId + " 닫음");
            }
        }

        internal override void ResetEpisode()
        {
            _opened.Clear();
        }
    }

    /// <summary>H3(위협): 발소리가 시작되면 제한 시간 안에 방(복도 밖 공간)으로 들어가 끝난 뒤 여유 시간까지 머문다.</summary>
    public sealed class TakeShelterJudge : CueEpisodeJudge
    {
        private readonly float _limit;
        private readonly float _after;
        private bool _sheltered;
        private int _endMs = -1;

        /// <summary>만든다.</summary>
        public TakeShelterJudge(string cueId, float limitSeconds, float afterSeconds) : base(cueId)
        {
            _limit = limitSeconds;
            _after = afterSeconds;
        }

        private static bool InRoom(FinalWorld w)
        {
            return w.Space != SpaceId.None && w.Space != SpaceId.Corridor;
        }

        protected override bool OnStart(in JudgeSignal s, FinalWorld w)
        {
            _sheltered = InRoom(w);
            _endMs = -1;
            return true;
        }

        protected override void OnEnd(FinalWorld w)
        {
            if (!_sheltered)
            {
                Fail("발소리가 끝날 때까지 대피하지 않음");
                return;
            }

            _endMs = w.NowMs;   // 여유 시간 동안 계속 본다(Active 유지).
        }

        protected override void OnSignal(in JudgeSignal s, FinalWorld w)
        {
            if (s.Kind == SignalKind.SpaceEntered || s.Kind == SignalKind.SpaceExited)
            {
                bool inRoom = InRoom(w);
                if (_sheltered && !inRoom)
                {
                    Fail("대피한 방에서 너무 일찍 나옴");
                    return;
                }

                if (inRoom) _sheltered = true;
                return;
            }

            if (s.Kind != SignalKind.Tick) return;

            if (!_sheltered && Elapsed(w) >= _limit)
            {
                Fail(_limit + "초 안에 대피하지 않음");
                return;
            }

            if (_endMs >= 0 && w.NowMs - _endMs >= FinalWorld.ToMs(_after))
            {
                Pass("발소리 대피");
            }
        }

        internal override void ResetEpisode()
        {
            base.ResetEpisode();
            _endMs = -1;
            _sheltered = false;
        }
    }

    /// <summary>
    /// H4(위협): 부르는 목소리에 응답하지 않는다. 소리 쪽으로 90° 이상 몸을 돌리거나(시작 때 등진 각도에서 90° 이상 줄어듦),
    /// 처음에 옆·뒤에 있던 소리를 정면 30° 안에 두거나, 소리 쪽으로 2m 넘게 다가가면 응답으로 본다.
    /// </summary>
    public sealed class DontAnswerJudge : CueEpisodeJudge
    {
        private Vector3 _source;
        private float _startAngle = -1f;
        private float _startDistance = -1f;

        /// <summary>만든다.</summary>
        public DontAnswerJudge(string cueId) : base(cueId)
        {
        }

        protected override bool OnStart(in JudgeSignal s, FinalWorld w)
        {
            _source = s.Point;
            _startAngle = -1f;
            _startDistance = -1f;
            if (w.HasPose) Baseline(w);
            return true;
        }

        private void Baseline(FinalWorld w)
        {
            _startAngle = AngleTo(w);
            _startDistance = SensingRules.HorizontalDistance(w.Feet, _source);
        }

        private float AngleTo(FinalWorld w)
        {
            Vector3 to = _source - w.Feet;
            to.y = 0f;
            if (to.sqrMagnitude < 0.0001f) return 0f;
            return Vector3.Angle(w.Forward, to);
        }

        protected override void OnEnd(FinalWorld w)
        {
            Pass("목소리에 응답하지 않음");
        }

        protected override void OnSignal(in JudgeSignal s, FinalWorld w)
        {
            if (s.Kind != SignalKind.PlayerPose) return;
            if (_startAngle < 0f)
            {
                Baseline(w);
                return;
            }

            float angle = AngleTo(w);
            if (_startAngle - angle >= 90f)
            {
                Fail("목소리 쪽으로 돌아봄");
                return;
            }

            if (_startAngle > 60f && angle <= 30f)
            {
                Fail("목소리를 정면에 둠");
                return;
            }

            if (_startDistance - SensingRules.HorizontalDistance(w.Feet, _source) > 2f)
            {
                Fail("목소리 쪽으로 다가감");
            }
        }
    }

    /// <summary>C1: 단서가 이어지는 동안과 끝난 뒤 여유 시간 안에 그 공간에 들어가면 위반.</summary>
    public sealed class NoEntryDuringCueJudge : CueEpisodeJudge
    {
        private readonly SpaceId _space;
        private readonly float _grace;
        private int _endMs = -1;

        /// <summary>만든다.</summary>
        public NoEntryDuringCueJudge(string cueId, SpaceId space, float graceSeconds) : base(cueId)
        {
            _space = space;
            _grace = graceSeconds;
        }

        protected override bool OnStart(in JudgeSignal s, FinalWorld w)
        {
            _endMs = -1;
            return true;
        }

        protected override void OnEnd(FinalWorld w)
        {
            _endMs = w.NowMs;
        }

        protected override void OnSignal(in JudgeSignal s, FinalWorld w)
        {
            if (s.Kind == SignalKind.SpaceEntered && SpaceIds.Canonical(s.Space) == _space)
            {
                Fail(_endMs < 0 ? "판서 중 입실" : "판서 직후 입실");
                return;
            }

            if (s.Kind == SignalKind.Tick && _endMs >= 0 && w.NowMs - _endMs > FinalWorld.ToMs(_grace))
            {
                Active = false;
            }
        }
    }

    /// <summary>C2: 대상을 연속으로 오래 응시하면 위반(응시 틈 0.2초 허용).</summary>
    public sealed class DontStareJudge : FinalJudge
    {
        private readonly string _cue;
        private readonly string _target;
        private readonly float _limit;
        private SampleStreak _streak;

        /// <summary>만든다.</summary>
        public DontStareJudge(string cueId, string target, float limitSeconds)
        {
            _cue = cueId;
            _target = target;
            _limit = limitSeconds;
        }

        internal override void Observe(in JudgeSignal s, FinalWorld w)
        {
            if (IsCue(s, SignalKind.CueStarted, _cue))
            {
                Trigger();
                return;
            }

            if (s.Kind != SignalKind.GazeSample) return;
            bool hit = s.TargetId == _target;
            if (hit) Trigger();
            if (_streak.Feed(hit, s.Value, SensingRules.GazeGapSeconds) >= _limit)
            {
                _streak.Clear();
                Violate(_target + " " + _limit + "초 응시");
            }
        }

        internal override void ResetEpisode()
        {
            _streak.Clear();
        }
    }

    /// <summary>C3(위협): 단서부터 끝 단서(종)까지 제자리(0.2m)를 지킨다. 시작 때 그 공간에 있어야 해당.</summary>
    public sealed class StayStillJudge : CueEpisodeJudge
    {
        private readonly string _endCue;
        private bool _hasAnchor;
        private Vector3 _anchor;

        /// <summary>만든다.</summary>
        public StayStillJudge(string cueId, string endCueId) : base(cueId)
        {
            _endCue = endCueId;
        }

        protected override bool OnStart(in JudgeSignal s, FinalWorld w)
        {
            if (Def != null && Def.Space != SpaceId.None && w.Space != Def.Space) return false;
            _hasAnchor = w.HasPose;
            _anchor = w.Feet;
            return true;
        }

        protected override void OnEnd(FinalWorld w)
        {
            Pass("수업 중 정지");
        }

        protected override void OnSignal(in JudgeSignal s, FinalWorld w)
        {
            if (IsCue(s, SignalKind.CueStarted, _endCue))
            {
                Pass("종이 울릴 때까지 정지");
                return;
            }

            if (s.Kind != SignalKind.PlayerPose) return;
            if (!_hasAnchor)
            {
                _hasAnchor = true;
                _anchor = s.Point;
                return;
            }

            if (!SensingRules.IsStill(_anchor, s.Point)) Fail("수업 중 움직임");
        }
    }

    /// <summary>
    /// C4·S4·T5: 단서가 이어지는 동안 손전등을 켜 둔다.
    /// <list type="bullet">
    /// <item><c>onWithin</c> &lt; 0(C4): 노출 중 끄는 순간 위반(이미 꺼져 있던 것은 묻지 않는다).</item>
    /// <item><c>onWithin</c> ≥ 0(S4·T5): 노출 중 그 시간보다 오래 꺼져 있으면 위반.</item>
    /// <item>구역을 주면(S4 <c>science.dark</c>) 그 구역 안에서만 노출. <c>endOnExit</c>면(T5) 단서 끝 대신 공간을 나갈 때 끝난다.</item>
    /// </list>
    /// </summary>
    public sealed class LightKeepJudge : CueEpisodeJudge
    {
        private readonly string _zone;
        private readonly float _onWithin;
        private readonly bool _endOnExit;
        private bool _inZone;
        private float _offFor;

        /// <summary>만든다.</summary>
        public LightKeepJudge(string cueId, string zone, float onWithinSeconds, bool endOnExit) : base(cueId)
        {
            _zone = zone;
            _onWithin = onWithinSeconds;
            _endOnExit = endOnExit;
        }

        private bool Exposed(FinalWorld w)
        {
            if (_zone != null) return _inZone;
            return Def == null || Def.Space == SpaceId.None || w.Space == Def.Space;
        }

        internal override void Observe(in JudgeSignal s, FinalWorld w)
        {
            if (_zone != null && (s.Kind == SignalKind.ZoneEntered || s.Kind == SignalKind.ZoneExited) && s.TargetId == _zone)
            {
                _inZone = s.Kind == SignalKind.ZoneEntered;
            }

            base.Observe(s, w);
        }

        protected override bool OnStart(in JudgeSignal s, FinalWorld w)
        {
            _offFor = 0f;
            return true;
        }

        protected override void OnEnd(FinalWorld w)
        {
            if (_endOnExit) return;   // T5는 단서 끝이 아니라 공간 이탈로 끝난다.
            Active = false;
        }

        protected override void OnSignal(in JudgeSignal s, FinalWorld w)
        {
            if (_endOnExit && s.Kind == SignalKind.SpaceExited && Def != null && SpaceIds.Canonical(s.Space) == Def.Space)
            {
                Active = false;
                return;
            }

            if (_onWithin < 0f)
            {
                if (s.Kind == SignalKind.FlashlightChanged && !s.Flag && Exposed(w)) Fail("손전등을 끔");
                return;
            }

            if (s.Kind != SignalKind.Tick) return;
            if (!Exposed(w) || w.Flashlight)
            {
                _offFor = 0f;
                return;
            }

            _offFor += s.Value;
            if (_offFor > _onWithin) Fail("불을 켜지 않음");
        }

        internal override void ResetEpisode()
        {
            base.ResetEpisode();
            _offFor = 0f;
            _inZone = false;
        }
    }

    /// <summary>
    /// S5·T3(위협): 단서가 시작되면 여유 시간 안에 손전등을 끄고, 그 뒤 끝날 때까지 제자리(0.2m)에서 불을 끈 채 기다린다.
    /// 시작 때 그 공간에 있어야 해당.
    /// </summary>
    public sealed class WaitInDarkJudge : CueEpisodeJudge
    {
        private readonly float _grace;
        private bool _settledIn;
        private Vector3 _anchor;

        /// <summary>만든다.</summary>
        public WaitInDarkJudge(string cueId, float graceSeconds) : base(cueId)
        {
            _grace = graceSeconds;
        }

        protected override bool OnStart(in JudgeSignal s, FinalWorld w)
        {
            if (Def != null && Def.Space != SpaceId.None && w.Space != Def.Space) return false;
            _settledIn = false;
            return true;
        }

        protected override void OnEnd(FinalWorld w)
        {
            if (!_settledIn && w.Flashlight)
            {
                Fail("불을 끄지 않음");
                return;
            }

            Pass("불을 끄고 기다림");
        }

        protected override void OnSignal(in JudgeSignal s, FinalWorld w)
        {
            if (s.Kind == SignalKind.Tick && !_settledIn && Elapsed(w) >= _grace)
            {
                if (w.Flashlight)
                {
                    Fail(_grace + "초 안에 불을 끄지 않음");
                    return;
                }

                _settledIn = true;
                _anchor = w.Feet;
                return;
            }

            if (!_settledIn) return;

            if (s.Kind == SignalKind.FlashlightChanged && s.Flag)
            {
                Fail("기다리는 중 불을 켬");
                return;
            }

            if (s.Kind == SignalKind.PlayerPose && w.HasPose && !SensingRules.IsStill(_anchor, s.Point))
            {
                Fail("기다리는 중 움직임");
            }
        }
    }

    /// <summary>
    /// T1·L2·S2: 단서 때 그 공간에 있었다면 제한 시간 안에 나온다. <c>noReentry</c>면(S2) 단서 뒤 그 밤 다시 들어가도 위반.
    /// </summary>
    public sealed class ExitWithinJudge : CueEpisodeJudge
    {
        private readonly float _limit;
        private readonly bool _noReentry;
        private bool _inside;
        private bool _closed;

        /// <summary>만든다.</summary>
        public ExitWithinJudge(string cueId, float limitSeconds, bool noReentry) : base(cueId)
        {
            _limit = limitSeconds;
            _noReentry = noReentry;
        }

        internal override void Observe(in JudgeSignal s, FinalWorld w)
        {
            if (_closed && s.Kind == SignalKind.SpaceEntered && Def != null && SpaceIds.Canonical(s.Space) == Def.Space)
            {
                Violate("종료된 공간에 다시 들어감");
                return;
            }

            base.Observe(s, w);
        }

        protected override bool OnStart(in JudgeSignal s, FinalWorld w)
        {
            _inside = Def == null || w.Space == Def.Space;
            if (_noReentry) _closed = true;
            return _inside || _noReentry;
        }

        protected override void OnEnd(FinalWorld w)
        {
            // 소리가 끝나도 제한 시간은 흐른다.
        }

        protected override void OnSignal(in JudgeSignal s, FinalWorld w)
        {
            if (!_inside)
            {
                Active = false;
                return;
            }

            if (s.Kind == SignalKind.SpaceExited && Def != null && SpaceIds.Canonical(s.Space) == Def.Space)
            {
                _inside = false;
                Pass("제한 시간 안에 나옴");
                return;
            }

            if (s.Kind == SignalKind.Tick && Elapsed(w) >= _limit)
            {
                Fail(_limit + "초 안에 나오지 않음");
            }
        }

        internal override void ResetEpisode()
        {
            base.ResetEpisode();
            _inside = false;
            // _closed(과학실 종료)는 재시작 스냅샷의 Triggered로 되살리지 못하므로 방아쇠가 났으면 유지한다.
            _closed = _noReentry && Triggered;
        }
    }

    /// <summary>S1: 들어온 쪽(기준점의 X 기준)과 나간 쪽이 다르면 통로로 쓴 것.</summary>
    public sealed class NoPassageJudge : FinalJudge
    {
        private readonly string _center;
        private int _entrySide;

        /// <summary>만든다.</summary>
        public NoPassageJudge(string center)
        {
            _center = center;
        }

        private int Side(FinalWorld w)
        {
            if (!w.HasPose) return 0;
            Vector3? c = Book.AnchorOf(_center);
            if (!c.HasValue) return 0;
            return w.Feet.x >= c.Value.x ? 1 : -1;
        }

        internal override void Observe(in JudgeSignal s, FinalWorld w)
        {
            if (Def == null) return;
            if (s.Kind == SignalKind.SpaceEntered && SpaceIds.Canonical(s.Space) == Def.Space)
            {
                _entrySide = Side(w);
                if (_entrySide != 0) Trigger();
                return;
            }

            if (s.Kind == SignalKind.SpaceExited && SpaceIds.Canonical(s.Space) == Def.Space)
            {
                int exit = Side(w);
                if (_entrySide != 0 && exit != 0 && exit != _entrySide) Violate("과학실을 가로질러 나감");
                _entrySide = 0;
            }
        }

        internal override void ResetEpisode()
        {
            _entrySide = 0;
        }
    }

    /// <summary>
    /// S3(2026-10-04 민 수정 — 「인체 모형을 빛으로 확인하십시오.」): 대상을 <b>비추지 않은 채</b> 연속으로 오래 바라보면 위반.
    /// 비추는 동안(마지막 비춤 샘플에서 <see cref="LitWindowMs"/> 안)의 응시는 괜찮다. 위반하면 다음 밤 조우(모형 급습)를 예약한다.
    /// 과학실에는 점검용 인체 모형(S-1)과 몬스터 모형이 함께 있어 「무엇을 비춰 확인해야 하는지」가 헷갈리게 둔다(의도).
    /// </summary>
    public sealed class LitGazeJudge : FinalJudge
    {
        /// <summary>비춤이 응시를 덮어 주는 시간(ms) — 비춤 샘플 사이 틈.</summary>
        public const int LitWindowMs = 300;

        private readonly string _target;
        private readonly float _limit;
        private readonly string _reserve;
        private int _lastLitMs = int.MinValue / 2;
        private SampleStreak _streak;

        /// <summary>만든다.</summary>
        public LitGazeJudge(string target, float limitSeconds, string reserveOnViolation)
        {
            _target = target;
            _limit = limitSeconds;
            _reserve = reserveOnViolation;
        }

        internal override void Observe(in JudgeSignal s, FinalWorld w)
        {
            if (s.Kind == SignalKind.SpaceEntered && Def != null && SpaceIds.Canonical(s.Space) == Def.Space)
            {
                Trigger();
                return;
            }

            if (s.Kind == SignalKind.BeamSample)
            {
                if (s.TargetId == _target) _lastLitMs = w.NowMs;
                return;
            }

            if (s.Kind != SignalKind.GazeSample) return;
            bool lit = w.NowMs - _lastLitMs <= LitWindowMs;
            bool hit = s.TargetId == _target && !lit;
            if (_streak.Feed(hit, s.Value, SensingRules.GazeGapSeconds) >= _limit)
            {
                _streak.Clear();
                bool first = !Violated;
                Violate("빛 없이 모형을 봄");
                if (first && !string.IsNullOrEmpty(_reserve)) Book.RequestEncounter(_reserve);
            }
        }

        internal override void ResetEpisode()
        {
            _streak.Clear();
            _lastLitMs = int.MinValue / 2;
        }
    }

    /// <summary>
    /// L3(위협): 단서 동안 대상을 계속 비춘다. 시작 뒤 여유 시간 안에 비추지 못하거나, 한 번 비춘 뒤 빛이 틈 허용보다 오래 떨어지면 실패.
    /// 실패하면 다음 밤 조우를 예약한다.
    /// </summary>
    public sealed class KeepBeamJudge : CueEpisodeJudge
    {
        private readonly string _target;
        private readonly float _acquire;
        private readonly float _gap;
        private readonly string _reserve;
        private int _lastLitMs = -1;

        /// <summary>만든다.</summary>
        public KeepBeamJudge(string cueId, string target, float acquireSeconds, float gapSeconds, string reserveOnFailure) : base(cueId)
        {
            _target = target;
            _acquire = acquireSeconds;
            _gap = gapSeconds;
            _reserve = reserveOnFailure;
        }

        protected override bool OnStart(in JudgeSignal s, FinalWorld w)
        {
            _lastLitMs = -1;
            return true;
        }

        protected override void OnEnd(FinalWorld w)
        {
            if (_lastLitMs < 0) FailAndReserve("끝까지 비추지 못함");
            else Pass("끝까지 비춤");
        }

        private void FailAndReserve(string reason)
        {
            bool was = Settled;
            Fail(reason);
            if (!was && !string.IsNullOrEmpty(_reserve)) Book.RequestEncounter(_reserve);
        }

        protected override void OnSignal(in JudgeSignal s, FinalWorld w)
        {
            if (s.Kind == SignalKind.BeamSample && s.TargetId == _target)
            {
                _lastLitMs = w.NowMs;
                return;
            }

            if (s.Kind != SignalKind.Tick) return;
            if (_lastLitMs < 0)
            {
                if (Elapsed(w) >= _acquire) FailAndReserve(_acquire + "초 안에 비추지 못함");
                return;
            }

            if (w.NowMs - _lastLitMs > FinalWorld.ToMs(_gap)) FailAndReserve("빛이 떨어짐");
        }
    }

    /// <summary>L5(위협): 창밖 남자를 비추거나 연속으로 오래 보면 먼저 인사한 것.</summary>
    public sealed class DontGreetJudge : CueEpisodeJudge
    {
        private readonly string _target;
        private readonly float _gazeLimit;
        private SampleStreak _streak;

        /// <summary>만든다.</summary>
        public DontGreetJudge(string cueId, string target, float gazeLimitSeconds) : base(cueId)
        {
            _target = target;
            _gazeLimit = gazeLimitSeconds;
        }

        protected override bool OnStart(in JudgeSignal s, FinalWorld w)
        {
            _streak.Clear();
            return true;
        }

        protected override void OnEnd(FinalWorld w)
        {
            Pass("먼저 인사하지 않음");
        }

        protected override void OnSignal(in JudgeSignal s, FinalWorld w)
        {
            if (s.Kind == SignalKind.BeamSample && s.TargetId == _target)
            {
                Fail("창밖 남자를 비춤");
                return;
            }

            if (s.Kind == SignalKind.GazeSample && _streak.Feed(s.TargetId == _target, s.Value, SensingRules.GazeGapSeconds) >= _gazeLimit)
            {
                Fail("창밖 남자를 오래 봄");
            }
        }
    }

    /// <summary>C5·T2: 단서 동안 구역에 들어가면 위반. 구역을 null로 주면 단서 ID의 <c>@</c> 뒤가 구역(T2 <c>cue.stall.occupied@toilet.stall2</c>).</summary>
    public sealed class ZoneForbiddenJudge : CueEpisodeJudge
    {
        private readonly string _fixedZone;
        private string _zone;

        /// <summary>만든다.</summary>
        public ZoneForbiddenJudge(string cueId, string zone) : base(cueId)
        {
            _fixedZone = zone;
        }

        protected override bool OnStart(in JudgeSignal s, FinalWorld w)
        {
            _zone = _fixedZone ?? CueTarget(s);
            return !string.IsNullOrEmpty(_zone);
        }

        protected override void OnSignal(in JudgeSignal s, FinalWorld w)
        {
            if (s.Kind == SignalKind.ZoneEntered && s.TargetId == _zone) Fail(_zone + " 진입");
        }
    }

    /// <summary>K1: 단서 동안 채널을 넘기면 위반.</summary>
    public sealed class NoChannelChangeJudge : CueEpisodeJudge
    {
        /// <summary>만든다.</summary>
        public NoChannelChangeJudge(string cueId) : base(cueId)
        {
        }

        protected override void OnSignal(in JudgeSignal s, FinalWorld w)
        {
            if (s.Kind == SignalKind.CctvChannel) Fail("사람이 지나가기 전 채널을 넘김");
        }
    }

    /// <summary>K2: 단서가 가리킨 채널(<c>cue.emptyroom@cctv.ch2</c>)을 연속으로 오래 보면 위반.</summary>
    public sealed class DontWatchJudge : CueEpisodeJudge
    {
        private readonly float _limit;
        private string _channel;
        private SampleStreak _streak;

        /// <summary>만든다.</summary>
        public DontWatchJudge(string cueId, float limitSeconds) : base(cueId)
        {
            _limit = limitSeconds;
        }

        protected override bool OnStart(in JudgeSignal s, FinalWorld w)
        {
            _channel = CueTarget(s);
            _streak.Clear();
            return !string.IsNullOrEmpty(_channel);
        }

        protected override void OnSignal(in JudgeSignal s, FinalWorld w)
        {
            if (s.Kind != SignalKind.CctvViewSample) return;
            if (_streak.Feed(s.TargetId == _channel, s.Value, SensingRules.GazeGapSeconds) >= _limit) Fail("빈 방을 오래 봄");
        }
    }

    /// <summary>K3: 판정 시간 동안 그 공간에 연속으로 오래 머물면 위반(한 번 들어가면 방아쇠).</summary>
    public sealed class DwellSpaceJudge : FinalJudge
    {
        private readonly SpaceId _space;
        private readonly float _limit;
        private float _dwell;

        /// <summary>만든다.</summary>
        public DwellSpaceJudge(SpaceId space, float limitSeconds)
        {
            _space = space;
            _limit = limitSeconds;
        }

        internal override void Observe(in JudgeSignal s, FinalWorld w)
        {
            if (w.Space != _space)
            {
                _dwell = 0f;
                return;
            }

            if (s.Kind != SignalKind.Tick) return;
            Trigger();
            _dwell += s.Value;
            if (_dwell >= _limit)
            {
                _dwell = 0f;
                Violate("경비실에 " + _limit + "초 머묾");
            }
        }

        internal override void ResetEpisode()
        {
            _dwell = 0f;
        }
    }

    /// <summary>G1: 복도에서 연속으로 뛰면 위반(짧은 발걸음은 봐준다).</summary>
    public sealed class NoRunningJudge : FinalJudge
    {
        private readonly float _limit;
        private float _run;

        /// <summary>만든다.</summary>
        public NoRunningJudge(float limitSeconds)
        {
            _limit = limitSeconds;
        }

        internal override void Observe(in JudgeSignal s, FinalWorld w)
        {
            if (w.Space != SpaceId.Corridor)
            {
                _run = 0f;
                return;
            }

            if (!w.Running)
            {
                _run = 0f;
            }

            if (s.Kind != SignalKind.Tick) return;
            Trigger();
            if (!w.Running) return;

            _run += s.Value;
            if (_run >= _limit) Violate("복도에서 뜀");
        }

        internal override void ResetEpisode()
        {
            _run = 0f;
        }
    }

    /// <summary>T4: 여자아이가 칸에 들어가는 단서가 오면 역보고(T-1)를 건다. 델타는 점검판이 준다.</summary>
    public sealed class ReverseReportJudge : FinalJudge
    {
        internal override void Observe(in JudgeSignal s, FinalWorld w)
        {
            if (!IsCue(s, SignalKind.CueStarted, FinalCues.GirlStall)) return;
            Trigger();
            Book.ArmReverseReport(InspectionCatalog.ReverseReportItem);
        }
    }

    /// <summary>G3·K4: 판정 조건 없이 기록만(K4는 결말, G3은 피날레 표시).</summary>
    public sealed class RecordOnlyJudge : FinalJudge
    {
        internal override void Observe(in JudgeSignal s, FinalWorld w)
        {
        }
    }
}

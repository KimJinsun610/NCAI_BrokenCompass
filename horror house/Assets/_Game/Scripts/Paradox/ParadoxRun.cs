using System;

namespace NightDuty
{
    /// <summary>역설 진행 한 걸음.</summary>
    public enum ParadoxStep
    {
        /// <summary>아무 일 없음.</summary>
        None = 0,

        /// <summary>역설 문자를 보냈다.</summary>
        Sent = 1,

        /// <summary>안전한 읽기를 마쳤다(그 공간의 이상 여부가 점검표에 드러난다).</summary>
        SafeRead = 2
    }

    /// <summary>안전한 읽기로 드러난 것 — 그 공간의 오늘 이상 여부. <see cref="EventBus.SafeReadConfirmed"/>로 나간다.</summary>
    public readonly struct SafeReadReveal
    {
        /// <summary>역설이 겨눈 수칙.</summary>
        public readonly string RuleId;

        /// <summary>드러난 공간.</summary>
        public readonly SpaceId Space;

        /// <summary>그 공간에 오늘 이상이 있는지(점검표에 「확인 필요」 / 「이상 없음」).</summary>
        public readonly bool HasAnomaly;

        /// <summary>만든다.</summary>
        public SafeReadReveal(string ruleId, SpaceId space, bool hasAnomaly)
        {
            RuleId = ruleId ?? string.Empty;
            Space = space;
            HasAnomaly = hasAnomaly;
        }

        /// <summary>점검표·근무일지에 붙는 말.</summary>
        public string Label
        {
            get { return HasAnomaly ? ParadoxRun.RevealAnomaly : ParadoxRun.RevealClear; }
        }
    }

    /// <summary>
    /// 그날 모호 역설 하나의 진행(최종 기획서 「모호 역설」). 순수 상태기계 — 문자를 보내고 표시를 바꾸는 일은 <see cref="NightRun"/>이 한다.
    /// <list type="bullet">
    /// <item>발송: 수칙에 방아쇠 단서가 있으면 그 단서가 시작될 때, 없으면 수칙의 공간에 들어갈 때. 판정 구간에만, 그 수칙을 아직 어기지 않았을 때만.</item>
    /// <item>안전한 읽기(문자를 받은 뒤, 그 수칙을 어기지 않은 채):
    /// 눈으로만 — 대상을 1초 이상 시야에 둔다(대상이 없으면 그 공간에 1초 머문다).
    /// 멈춰서 — 수칙의 정지 조건을 끝까지 지킨다(단서가 끝남 · 위협 대응 성공).
    /// CCTV로 — 그 공간 채널을 2초 이상 본다.</item>
    /// </list>
    /// <b>재시작해도 되돌리지 않는다</b> — 받은 문자는 태블릿에 남고, 한 번 드러난 것은 플레이어가 이미 안다. 진행 중이던 누적만 버린다(<see cref="ResetEpisode"/>).
    /// </summary>
    public sealed class ParadoxRun
    {
        /// <summary>「눈으로만」 — 대상을 시야에 두는 시간(초).</summary>
        public const float EyesOnlySeconds = 1f;

        /// <summary>「CCTV로」 — 채널을 보는 시간(초).</summary>
        public const float CctvSeconds = 2f;

        /// <summary>점검표에 붙는 말 — 이상 있음.</summary>
        public const string RevealAnomaly = "확인 필요";

        /// <summary>점검표에 붙는 말 — 이상 없음.</summary>
        public const string RevealClear = "이상 없음";

        private readonly ParadoxPlan _plan;
        private readonly ParadoxEntry _entry;
        private readonly SpaceId _space;
        private readonly string _channel;
        private float _seen;

        /// <summary>그날 편성으로 만든다.</summary>
        public ParadoxRun(ParadoxPlan plan)
        {
            _plan = plan ?? ParadoxPlan.None;
            _entry = _plan.AmbiguousEntry;
            _space = _plan.Ambiguous != null ? SpaceOf(_plan.Ambiguous, _plan.EmptyRoomChannel) : SpaceId.None;
            _channel = _plan.EmptyRoomChannel >= 0 ? "cctv.ch" + _plan.EmptyRoomChannel : string.Empty;
        }

        /// <summary>그날 편성.</summary>
        public ParadoxPlan Plan
        {
            get { return _plan; }
        }

        /// <summary>겨눈 수칙. 없으면 null.</summary>
        public string RuleId
        {
            get { return _plan.Ambiguous; }
        }

        /// <summary>안전한 읽기 패턴.</summary>
        public SafeReadPattern Pattern
        {
            get { return _entry != null ? _entry.Pattern : SafeReadPattern.None; }
        }

        /// <summary>안전한 읽기가 드러내는 공간(「CCTV로」는 채널 공간, 그 밖에는 수칙의 공간).</summary>
        public SpaceId Space
        {
            get { return _space; }
        }

        /// <summary>오늘 보낼 문자가 있는지.</summary>
        public bool Armed
        {
            get { return _entry != null && _entry.HasMessage && _plan.WillSend; }
        }

        /// <summary>문자를 보냈는지.</summary>
        public bool Sent { get; private set; }

        /// <summary>안전한 읽기를 마쳤는지.</summary>
        public bool SafeRead { get; private set; }

        /// <summary>안전한 읽기 진행(0~1, 디버그).</summary>
        public float Progress
        {
            get
            {
                if (SafeRead) return 1f;
                if (!Sent) return 0f;
                switch (Pattern)
                {
                    case SafeReadPattern.EyesOnly: return Math.Min(1f, _seen / EyesOnlySeconds);
                    case SafeReadPattern.Cctv: return Math.Min(1f, _seen / CctvSeconds);
                    default: return 0f;
                }
            }
        }

        /// <summary>문자 본문.</summary>
        public string Message
        {
            get
            {
                if (_entry == null) return string.Empty;
                if (_entry.RuleId == "K2") return string.Format(_entry.Message, (_plan.EmptyRoomChannel + 1).ToString("00"));
                return _entry.Message;
            }
        }

        /// <summary>
        /// 판정 구간의 신호 하나. <paramref name="current"/>는 지금 공간, <paramref name="violated"/>는 그 수칙을 오늘 이미 어겼는지.
        /// </summary>
        public ParadoxStep Observe(in JudgeSignal s, SpaceId current, bool violated)
        {
            if (!Armed || SafeRead || violated) return ParadoxStep.None;

            if (!Sent)
            {
                if (!IsSendTrigger(s)) return ParadoxStep.None;
                Sent = true;
                _seen = 0f;
                return ParadoxStep.Sent;
            }

            if (_entry.RuleId == "K2") return ObserveEmptyRoom(s);

            switch (Pattern)
            {
                case SafeReadPattern.EyesOnly:
                    if (_entry.Target != null)
                    {
                        if (s.Kind == SignalKind.GazeSample && s.TargetId == _entry.Target) _seen += s.Value;
                    }
                    else if (s.Kind == SignalKind.Tick && SpaceIds.Canonical(current) == _space)
                    {
                        _seen += s.Value;
                    }

                    return _seen + 1e-4f >= EyesOnlySeconds ? Read() : ParadoxStep.None;

                case SafeReadPattern.StandStill:
                    return _entry.Cue != null && s.Kind == SignalKind.SequenceEnded && CueId(s.TargetId) == _entry.Cue ? Read() : ParadoxStep.None;

                case SafeReadPattern.Cctv:
                    if (s.Kind == SignalKind.CctvViewSample && s.TargetId == ChannelOf(_space)) _seen += s.Value;
                    return _seen + 1e-4f >= CctvSeconds ? Read() : ParadoxStep.None;

                default:
                    return ParadoxStep.None;
            }
        }

        /// <summary>K2 「눈으로만」 — 빈 방 채널을 잠깐(0.5초) 본 뒤 다른 채널로 넘긴다(3초 넘게 보면 K2 위반이라 거기서 끝난다).</summary>
        public const float EmptyRoomGlanceSeconds = 0.5f;

        private ParadoxStep ObserveEmptyRoom(in JudgeSignal s)
        {
            if (s.Kind == SignalKind.CctvViewSample)
            {
                if (s.TargetId == _channel) _seen += s.Value;
                else if (_seen + 1e-4f >= EmptyRoomGlanceSeconds) return Read();
                return ParadoxStep.None;
            }

            if (s.Kind == SignalKind.CctvChannel && s.TargetId != _channel && _seen + 1e-4f >= EmptyRoomGlanceSeconds) return Read();
            return ParadoxStep.None;
        }

        /// <summary>위협 수칙 대응 성공(「멈춰서」 — 정지 조건을 끝까지 지켰다).</summary>
        public ParadoxStep NoteThreatKept(string ruleId)
        {
            if (!Armed || !Sent || SafeRead || ruleId != _plan.Ambiguous || Pattern != SafeReadPattern.StandStill) return ParadoxStep.None;
            return Read();
        }

        /// <summary>디버그: 방아쇠 없이 보낸다.</summary>
        public ParadoxStep ForceSend()
        {
            if (Sent || _entry == null || !_entry.HasMessage) return ParadoxStep.None;
            Sent = true;
            _seen = 0f;
            return ParadoxStep.Sent;
        }

        /// <summary>디버그: 안전한 읽기를 마친 것으로 한다.</summary>
        public ParadoxStep ForceRead()
        {
            return Sent && !SafeRead ? Read() : ParadoxStep.None;
        }

        /// <summary>진행 중이던 누적을 버린다(재시작). 보낸 문자·드러난 것은 남는다.</summary>
        public void ResetEpisode()
        {
            _seen = 0f;
        }

        private ParadoxStep Read()
        {
            SafeRead = true;
            return ParadoxStep.SafeRead;
        }

        private bool IsSendTrigger(in JudgeSignal s)
        {
            if (_entry.Cue != null) return s.Kind == SignalKind.CueStarted && CueId(s.TargetId) == _entry.Cue;
            RuleDef def = ProgramCatalog.Rule(_plan.Ambiguous);
            SpaceId ruleSpace = def != null ? SpaceIds.Canonical(def.Space) : SpaceId.None;
            return s.Kind == SignalKind.SpaceEntered && ruleSpace != SpaceId.None && SpaceIds.Canonical(s.Space) == ruleSpace;
        }

        /// <summary>단서 ID의 <c>@</c> 앞부분.</summary>
        public static string CueId(string targetId)
        {
            if (string.IsNullOrEmpty(targetId)) return string.Empty;
            int at = targetId.IndexOf('@');
            return at < 0 ? targetId : targetId.Substring(0, at);
        }

        /// <summary>그 수칙의 역설이 드러내는 공간(「CCTV로」는 채널 공간, K2는 그날 빈 방 채널의 공간).</summary>
        public static SpaceId SpaceOf(string ruleId, int emptyRoomChannel = -1)
        {
            if (ruleId == "K2") return SpaceOfChannel(emptyRoomChannel);
            ParadoxEntry e = ParadoxCatalog.Find(ruleId);
            if (e != null && e.Pattern == SafeReadPattern.Cctv) return SpaceIds.Canonical(e.CctvSpace);
            RuleDef def = ProgramCatalog.Rule(ruleId);
            return def != null ? SpaceIds.Canonical(def.Space) : SpaceId.None;
        }

        /// <summary>채널 번호(0~4)의 공간. 모르면 None.</summary>
        public static SpaceId SpaceOfChannel(int channel)
        {
            switch (channel)
            {
                case 0: return SpaceId.Corridor;
                case 1: return SpaceId.Classroom;
                case 2: return SpaceId.ScienceRoom;
                case 3: return SpaceId.Toilet;
                case 4: return SpaceId.Library;
                default: return SpaceId.None;
            }
        }

        /// <summary>공간의 CCTV 채널 ID(<c>cctv.ch0</c> 복도 · 1 교실 · 2 과학실 · 3 화장실 · 4 도서관). 채널이 없으면 빈 문자열.</summary>
        public static string ChannelOf(SpaceId space)
        {
            switch (SpaceIds.Canonical(space))
            {
                case SpaceId.Corridor: return "cctv.ch0";
                case SpaceId.Classroom: return "cctv.ch1";
                case SpaceId.ScienceRoom: return "cctv.ch2";
                case SpaceId.Toilet: return "cctv.ch3";
                case SpaceId.Library: return "cctv.ch4";
                default: return string.Empty;
            }
        }
    }
}

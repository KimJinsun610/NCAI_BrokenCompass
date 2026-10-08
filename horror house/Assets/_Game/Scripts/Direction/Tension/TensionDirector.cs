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
        Missed = 5,

        /// <summary>61차: 대역을 세워 두고 플레이어가 보기를 기다린다(<see cref="DirectionPhase.Present"/>).</summary>
        Presenting = 6
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

        /// <summary>57차: 헛예고 뒤 — 그 방에 있기만 하면 곧 진짜가 온다(머무름을 다시 세지 않는다).</summary>
        public bool Rearmed;

        /// <summary>57차: 제 슬롯에 방아쇠가 오지 않아 판정 끝(03:30)까지 넘어온 실행.</summary>
        public bool CarriedOver;

        /// <summary>
        /// 59차 겹침 조우(<see cref="NightProgram.Extras"/> — 시체 낙하): 슬롯 조우가 진행 중이어도 시작하고, 진행 중에도 슬롯 조우를 막지 않으며,
        /// 놀람 예산(횟수·간격·조우 뒤 휴지)에 넣지 않는다(민: 「소년이 앉아 있어도 시체가 나오고, 시체를 맞이해도 소년이 머리를 박도록」).
        /// </summary>
        public bool Overlay;

        /// <summary>61차: 깊이 머무름(<see cref="EncounterScript.DeepMargin"/>)을 센 시작 시각(-1 = 지금 깊이 안에 없음).</summary>
        public float DeepSince = -1f;

        /// <summary>61차: 깊이를 마지막으로 잰 시각(오래 안 쟀으면 다시 센다).</summary>
        public float DeepCheckedAt = float.NegativeInfinity;

        /// <summary>61차: 대역을 세운 시각(<see cref="EncounterRunState.Presenting"/>).</summary>
        public float PresentSince;

        /// <summary>61차: 플레이어가 세워 둔 대역을 봤다.</summary>
        public bool Seen;

        /// <summary>61차: 「나가는 길」 시체 — 사다리 곁(<see cref="TensionDirector.ExitInner"/>)에 들어갔었다.</summary>
        public bool ExitArmed;

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
    /// <item>가짜 놀람: 긴장 조절기(<see cref="TensionPacer"/>, 50차)가 거른다 — 일차 상한 2/4/5/6/7, 같은 것은 밤에 2번, 60~150초 간격, 1일차는 첫 조우 결과 뒤.</item>
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
        public const float FakeGapMin = 30f;   // 57차: 60~150 → 45~110 · 59차(밤 15분 → 10분): × 2/3 → 30~75

        /// <summary>가짜 놀람 간격 최대(초).</summary>
        public const float FakeGapMax = 75f;

        /// <summary>판정이 열린(00:16) 뒤 첫 가짜 놀람까지 최소(초, 2일차부터). 50차: 4~14초 → 25~50초(「첫날부터 도배」).</summary>
        public const float FirstFakeMin = 17f;   // 59차: 25~50 → 17~34(밤 10분)

        /// <summary>판정이 열린 뒤 첫 가짜 놀람까지 최대(초).</summary>
        public const float FirstFakeMax = 34f;

        /// <summary>같은 가짜 놀람의 밤당 상한.</summary>
        public const int FakePerId = 2;

        /// <summary>슬롯 끝 몇 분 전부터 방아쇠를 「그 공간에 있기만 하면」으로 완화하는지(게임 분).</summary>
        public const float LastCallMinutes = 12f;   // 67차(밤 05:00): 10 → 12(게임 분 ×1.25)

        /// <summary>디버그 강제 실행에서 존재형 조우의 길이(초).</summary>
        public const float ForcedPresenceSeconds = 60f;

        /// <summary>가짜 놀람 목록.</summary>
        public static readonly string[] FakeScares = { "fake.locker.rattle", "fake.locker.row", "fake.flashlight.flicker", FakeBugs, FakeGlimpse };

        /// <summary>
        /// 57차(민: 「몹을 살짝씩 멀리서 등장시킨다던가」): 멀리(12~22m) 몹 하나가 잠깐 서 있다가 바라보거나 몇 초 지나면 사라진다. 수칙·판정 없음, 2일차부터.
        /// 자리·모습은 연출 쪽(<c>DistantGlimpse</c>)이 고른다.
        /// </summary>
        public const string FakeGlimpse = "fake.glimpse";

        /// <summary>벌레 떼(김진선님 BugSwarm, 2026-10-02 민 추가) — 천장에서 쏟아진다. <see cref="BugSpaces"/>에서만.</summary>
        public const string FakeBugs = "fake.bugs";

        /// <summary>
        /// 61차(민: 「1일차에 사다리 근무 지시와 함께 바퀴벌레가 등장 — 바퀴벌레는 교실에서, 시체 연출이 등장할 때만 시체와 함께」):
        /// 벌레 떼 가짜 놀람을 끈다. 바퀴벌레는 시체 낙하(<c>DirectionStage.CorpseRoaches</c>)에만 나온다. 디버그 강제는 그대로.
        /// </summary>
        public static bool FakeBugsEnabled = false;

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
        private readonly TensionPacer _pacer;
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
        private float _channelSince = -1f;   // 71차: 지금 채널을 본 지(초) — CCTV 사람 자리 채널을 2초 보면 조우
        private float _lastRuleCue = float.NegativeInfinity;
        private float _nextFake;
        private int _fakesUsed;
        private int _encountersDone;
        private string _lastNote = string.Empty;
        private readonly HashSet<string> _held = new HashSet<string>(StringComparer.Ordinal);
        private string _gateRule = string.Empty;
        private string _gazeId = string.Empty;
        private float _gazeRun;

        /// <summary>
        /// 응시 방아쇠(<see cref="EncounterTrigger.GazeTarget"/>)의 대상이 지금 「점검 중」인지 — 지시받았고 아직 보고 전(NightRun이 넣는다). null이면 늘 참.
        /// </summary>
        public Func<string, bool> GazeTargetReady { get; set; }

        /// <summary>
        /// 60차(민: 「교실 들어설 때 바로 떨어지는 것도, 원래 기획대로 사다리 방 안에서도 — 랜덤으로」): 0보다 크면 응시 방아쇠를 그 대상에서 이 거리(m, 수평) 안에서만 받는다
        /// (사다리 방 안). 0이면 거리 무관(교실 입구에서 사다리를 봐도). 대상 자리는 <see cref="GazeTargetPosition"/>이 준다.
        /// </summary>
        public float GazeTargetNearRadius { get; set; }

        /// <summary>응시 방아쇠 대상의 자리(없으면 null — 거리 조건을 보지 않는다).</summary>
        public Func<string, Vector3?> GazeTargetPosition { get; set; }

        /// <summary>
        /// 61차(민: 「도서관 안에 완전히 들어오고 몇 초 · 교실 안에 확실하게」): 플레이어가 그 공간 상자 안쪽으로 그만큼(m) 들어와 있는지. 연출 쪽이 넣는다.
        /// null이면 그 공간 안이면 참(코어 시험).
        /// </summary>
        public Func<SpaceId, float, bool> DeepInSpace { get; set; }

        /// <summary>
        /// 61차(민: 「몹은 나타나 있되, 플레이어가 몹을 시야에 넣고 인지한 뒤에 연출이 시작되도록」): 켜면 몹 대역이 있는 조우(<see cref="EncounterScript.NeedsSight"/>)는
        /// 전조 뒤 대역만 세우고(<see cref="DirectionPhase.Present"/>) <see cref="NotifySeen"/>가 오면 대면한다. 연출 쪽이 있을 때만 켠다(코어 시험은 옛 흐름).
        /// </summary>
        public bool SightGated { get; set; }

        /// <summary>61차: 세운 대역을 이만큼(초) 못 보면 거두고 다시 기다린다.</summary>
        public const float PresentMaxSeconds = 40f;

        /// <summary>61차: 거둔 뒤 다시 시도까지(초).</summary>
        public const float PresentRetrySeconds = 15f;

        /// <summary>
        /// 61차(민: 「교실 안쪽에서 시체 떨어지는 건, 플레이어가 그 안쪽에서 나오면서 플레이어 쪽으로 떨어지게」): 켜면 시체 낙하(응시 방아쇠)를
        /// 사다리 곁(<see cref="ExitInner"/>m 안)에 들어갔다가 <see cref="ExitOuter"/>m 밖으로 나오는 순간 건다(응시는 보지 않는다). 대상 자리는 <see cref="GazeTargetPosition"/>.
        /// </summary>
        public bool GazeTargetExitMode { get; set; }

        /// <summary>61차 「나가는 길」 시체: 사다리 곁(m).</summary>
        public const float ExitInner = 2.8f;

        /// <summary>61차 「나가는 길」 시체: 여기(m)를 넘어 나오면 떨어진다.</summary>
        public const float ExitOuter = 3.8f;

        private bool ExitTriggered(EncounterRun r, EncounterScript s)
        {
            if (GazeTargetPosition == null || !_hasPose) return false;
            Vector3? at = GazeTargetPosition(s.GazeTargetId);
            if (!at.HasValue) return false;
            Vector3 d = at.Value - _feet;
            d.y = 0f;
            float dist = d.magnitude;
            if (dist <= ExitInner)
            {
                r.ExitArmed = true;
                return false;
            }

            return r.ExitArmed && dist >= ExitOuter;
        }

        private bool NearStage(EncounterScript s)
        {
            Vector3 at;
            if (s.NearAnchor <= 0f || !_hasPose || s.StageAnchor.Length == 0 || !StagePoints.TryGet(s.StageAnchor, out at)) return true;
            return SensingRules.HorizontalDistance(_feet, at) <= s.NearAnchor;
        }

        private bool DeepDwell(EncounterRun r, EncounterScript s, bool lastCall)
        {
            bool deep = InScriptSpace(s) && (DeepInSpace == null || DeepInSpace(s.ExactSpace != SpaceId.None ? s.ExactSpace : s.Space, s.DeepMargin));
            bool stale = Now - r.DeepCheckedAt > 0.5f;
            r.DeepCheckedAt = Now;
            if (!deep)
            {
                r.DeepSince = -1f;
                return false;
            }

            if (stale || r.DeepSince < 0f) r.DeepSince = Now;
            return r.Rearmed || lastCall || Now - r.DeepSince >= s.Dwell;
        }

        /// <summary>
        /// 61차: 플레이어가 세워 둔 대역을 봤다(연출 쪽이 부른다). 그 조우가 <see cref="EncounterRunState.Presenting"/>이면 다음 틱에 대면한다. 받았으면 true.
        /// </summary>
        public bool NotifySeen(string encounterId)
        {
            for (int i = 0; i < _runs.Count; i++)
            {
                EncounterRun r = _runs[i];
                if (r.State != EncounterRunState.Presenting || r.Def.Id != encounterId) continue;
                r.Seen = true;
                return true;
            }

            return false;
        }

        private bool GazeTargetNear(string targetId)
        {
            if (GazeTargetNearRadius <= 0f || GazeTargetPosition == null) return true;
            Vector3? at = GazeTargetPosition(targetId);
            if (!at.HasValue || !_hasPose) return true;
            Vector3 d = at.Value - _feet;
            d.y = 0f;
            return d.magnitude <= GazeTargetNearRadius;
        }

        /// <summary>그날 편성으로 만든다.</summary>
        public TensionDirector(NightProgram program, int day, int restarts = 0, Random rng = null)
        {
            _program = program ?? NightProgram.Empty(day);
            _day = Math.Max(1, day);
            _restarts = restarts;
            _rng = rng ?? new Random();
            _budget = new SurpriseBudget(_day);
            _pacer = new TensionPacer(_day);

            foreach (SlotEncounter s in _program.Slots)
            {
                EncounterScript script = EncounterScripts.Find(s.Encounter.Id);
                if (script == null) continue;
                _runs.Add(new EncounterRun { Slot = s.Slot, Def = s.Encounter, Script = script });
            }

            foreach (EncounterDef extra in _program.Extras)
            {
                EncounterScript script = EncounterScripts.Find(extra.Id);
                if (script == null) continue;
                _runs.Add(new EncounterRun { Slot = EncounterSlot.A, Def = extra, Script = script, Overlay = true });
            }

            foreach (RuleDef r in _program.Deck)
            {
                RuleTriggerScript script = RuleTriggers.Find(r.Id);
                if (script == null) continue;
                _rules.Add(new RuleTriggerRun { Script = script, NeedDwell = Range(script.DwellMin, script.DwellMax) });
            }

            // 44차: 출근(00:00~00:16, 실시간 1분)은 조용히 둔다. 50차: 판정이 열린 뒤 25~50초(2일차부터) — 1일차는 첫 조우 결과 뒤(TryFakeScare).
            _nextFake = NightClock.RealSecondsAt(NightClock.JudgingStart) + Range(FirstFakeMin, FirstFakeMax);
        }

        /// <summary>연출 알림(대역·소등·소리·로그).</summary>
        public event Action<DirectionEvent> Emitted;

        /// <summary>57차: 편성된 조우가 끝내 오지 않았다(판정 끝까지 이월해도). 인자: 조우 ID — NightRun이 그 조우의 수칙을 태블릿에서 거둔다.</summary>
        public event Action<string> Missed;

        /// <summary>57차(민: 「반납 상자를 확인하는데 쾅 — 헷갈려 오보」): 참이면 가짜 놀람을 미룬다(점검 대상 가까이·보고 직전). NightRun이 넣는다.</summary>
        public Func<Vector3, bool> FakeBlocked { get; set; }

        // ── 10단계: 점검과 맞물린 단서 · 회피 불가 역설 ──────────────────

        /// <summary>「나가라」 신호형 수칙(S2·L2·T1) — 평소에는 그 공간 점검을 끝냈거나 그날 그 공간 점검이 없을 때만 울린다(최종 기획서 「밤중 발동」).</summary>
        public static readonly string[] ExitSignalRules = { "S2", "L2", "T1" };

        /// <summary>그 공간에 아직 보고하지 않은 점검이 있는지(NightRun이 넣는다). null이면 「나가라」 문을 걸지 않는다(옛 동작·시험).</summary>
        public Func<SpaceId, bool> InspectionPendingIn { get; set; }

        /// <summary>71차: 그 밤 CCTV 사람(K1 조우)이 나타날 자리의 채널(<c>cctv.chN</c>). 비면 아무 채널(옛 동작).</summary>
        public string CctvPersonChannel { get; set; } = string.Empty;

        /// <summary>그날의 빈 방 채널(<c>cctv.chN</c>) — K2 단서가 늘 이 채널을 가리킨다. 비면 지금 보지 않는 채널 하나를 고른다(옛 동작).</summary>
        public string EmptyRoomChannel { get; set; } = string.Empty;

        /// <summary>
        /// 그날 회피 불가 역설을 건다(밤 시작에 한 번). 쌍의 「나가라」 수칙은 점검이 남아도 울리고(문을 연다),
        /// 둘째 조우(<see cref="UnavoidableDef.ChainEncounter"/>)는 제 슬롯에서 따로 걸지 않는다 — <see cref="ChainEncounter"/>로만.
        /// </summary>
        public void SetUnavoidable(UnavoidableDef def)
        {
            _held.Clear();
            _gateRule = string.Empty;
            if (def == null) return;
            if (!string.IsNullOrEmpty(def.ChainEncounter)) _held.Add(def.ChainEncounter);
            if (!string.IsNullOrEmpty(def.GateRule)) _gateRule = def.GateRule;
            Note(def.Id, "회피 불가 역설 편성(" + (def.Real ? "진짜" : "가짜") + ")");
        }

        /// <summary>
        /// 그 조우를 지금 곧장 대면으로 건다(회피 불가 역설의 둘째 조우). 전조·헛예고·예산 없이, <b>진행 중인 다른 조우를 끊지 않는다</b>.
        /// 대본이 없으면 false.
        /// </summary>
        public bool ChainEncounter(string encounterId)
        {
            EncounterScript script = EncounterScripts.Find(encounterId);
            EncounterDef def = ProgramCatalog.Encounter(encounterId);
            if (script == null || def == null) return false;

            for (int i = 0; i < _runs.Count; i++)
            {
                EncounterRun held = _runs[i];
                if (held.Def.Id == encounterId && held.State == EncounterRunState.Waiting) held.State = EncounterRunState.Done;   // 제 슬롯 몫은 이것으로 갈음
            }

            EncounterRun run = new EncounterRun { Slot = EncounterSlot.A, Def = def, Script = script, Forced = true };
            _runs.Add(run);
            _held.Remove(encounterId);
            Note(encounterId, "회피 불가 역설 — 겹쳐 건다");
            run.Point = PointFor(script);
            Confront(run);
            return true;
        }

        private bool ExitGateBlocks(RuleTriggerScript s)
        {
            if (InspectionPendingIn == null || Array.IndexOf(ExitSignalRules, s.RuleId) < 0) return false;
            if (s.RuleId == _gateRule) return false;   // 회피 불가 밤: 점검이 남아도 울린다
            return InspectionPendingIn(s.Space);
        }

        /// <summary>디렉터 시각(실제 초, Tick의 누적).</summary>
        public float Now { get; private set; }

        /// <summary>지금 강도 단계.</summary>
        public DirectorMood Mood { get; private set; } = DirectorMood.Normal;

        /// <summary>긴장 조절기(50차) — 가짜 놀람을 거르고 점검 지시가 읽는다.</summary>
        public TensionPacer Pacer
        {
            get { return _pacer; }
        }

        /// <summary>결과까지 간 조우 수(이번 시도).</summary>
        public int EncountersDone
        {
            get { return _encountersDone; }
        }

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

        /// <summary>조우가 전조·대면·마무리 중인지(존재형의 머무름은 세지 않는다). 겹침 조우도 센다 — 가짜 놀람·수칙 단서·점검 지시는 그동안 쉰다.</summary>
        public bool Busy
        {
            get { return BusyOf(false) || BusyOf(true); }
        }

        /// <summary>겹침 조우(<paramref name="overlay"/>) 또는 슬롯 조우가 진행 중인지.</summary>
        private bool BusyOf(bool overlay)
        {
            for (int i = 0; i < _runs.Count; i++)
            {
                EncounterRun r = _runs[i];
                if (r.Overlay != overlay) continue;
                if (r.State == EncounterRunState.Foreshadow || r.State == EncounterRunState.Releasing || r.State == EncounterRunState.Presenting) return true;
                if (r.State == EncounterRunState.Active && !r.Script.IsPresence) return true;
            }

            return false;
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
                    if (s.TargetId != _channel) _channelSince = Now;
                    _channel = s.TargetId;
                    break;
                case SignalKind.CctvChannel:
                    if (s.TargetId != _channel) _channelSince = Now;
                    _channel = s.TargetId;
                    break;
                case SignalKind.GazeSample:
                    string id = s.TargetId ?? string.Empty;
                    if (id.Length > 0 && id == _gazeId) _gazeRun += s.Value;
                    else
                    {
                        _gazeId = id;
                        _gazeRun = id.Length > 0 ? s.Value : 0f;
                    }

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
            _pacer.Tick(minute, dt > 0f ? dt : 0f, _space == SpaceId.SecurityRoom, highestSensory);

            if (captured)
            {
                AbortAll("붙잡힘");
                return;
            }

            AdvanceRuns(minute);
            AdvanceRules();

            // 59차: 슬롯 조우와 겹침 조우(시체 낙하)는 서로를 막지 않는다 — 각자 자기 쪽이 비었을 때만 새로 시작한다.
            TryStartEncounter(minute, auditoryShown, BusyOf(false), BusyOf(true));
            if (!Busy) TryRuleTriggers(minute);
            if (!Busy) TryFakeScare(minute);
        }

        // ── 조우 ───────────────────────────────────────────────

        private bool SlotOpen(EncounterRun r, float minute)
        {
            if (r.Forced) return true;

            // 51차: 응시 방아쇠(사다리 점검 중 시체 낙하)는 그 점검이 지시받은 때를 따른다 — 슬롯 시간창을 보지 않는다(판정 구간, 이완 구간 제외).
            if (r.Script.Trigger == EncounterTrigger.GazeTarget)
            {
                return NightClock.IsJudging(minute) && !(minute >= NightClock.RelaxStart && minute < NightClock.Call2);
            }

            if (!NightClock.CanStartEncounter(minute)) return false;
            if (!NightProgram.SlotActive(r.Slot, _restarts)) return false;
            if (r.Slot == EncounterSlot.C && Mood == DirectorMood.Danger) return false;

            float from, to;
            RunWindow(r, out from, out to);
            return minute >= from && minute < to;
        }

        /// <summary>그 실행의 시간창 — 이월된 실행은 제 슬롯 끝부터 판정 끝(03:30)까지(57차).</summary>
        private static void RunWindow(EncounterRun r, out float from, out float to)
        {
            SlotWindow(r.Slot, out from, out to);
            if (!r.CarriedOver) return;
            from = to;
            to = NightClock.JudgingEnd;
        }

        private bool Triggered(EncounterRun r, float minute)
        {
            EncounterScript s = r.Script;
            float from, to;
            RunWindow(r, out from, out to);
            bool lastCall = minute >= to - LastCallMinutes;

            // 61차: 고정 자리에서 가까워야 하는 조우(창밖 남자)·안쪽 깊이 머물러야 하는 조우(소년·노란 얼굴).
            if (!NearStage(s)) return false;
            if (s.DeepMargin > 0f && s.Trigger == EncounterTrigger.DwellInSpace) return DeepDwell(r, s, lastCall);

            // 57차(민: 2일차 교실·수업 수칙이 나왔는데 소년을 못 봄 — 헛예고가 하나뿐인 방문을 써 버렸다): 헛예고 뒤에는 그 방에 있기만 하면 된다.
            if (r.Rearmed && (s.Trigger == EncounterTrigger.DwellInSpace || s.Trigger == EncounterTrigger.EnterSpace)) return InScriptSpace(s);

            switch (s.Trigger)
            {
                case EncounterTrigger.EnterSpace:
                    return InScriptSpace(s);
                case EncounterTrigger.DwellInSpace:
                    return InScriptSpace(s) && (lastCall || Now - _spaceSince >= s.Dwell);
                case EncounterTrigger.CorridorWalk:
                    return _space == SpaceId.Corridor && (lastCall || _walk >= s.Dwell);
                case EncounterTrigger.ViewingCctv:
                    if (!ViewingCctv || Now - _cctvSince < s.Dwell) return false;
                    // 71차(민: 「CCTV 등장 장소 다양화 — 이벤트가 결정되면 장소도 함께」): 밤 시작에 정한 자리의 채널을 보고 있어야 한다(그 채널에 머문 지 Dwell).
                    // 슬롯 끝 10분 전부터는 어느 채널이든 — 그때는 연출이 보고 있는 채널의 자리로 바꾼다.
                    if (CctvPersonChannel.Length == 0 || lastCall) return true;
                    return _channel == CctvPersonChannel && Now - Mathf.Max(_cctvSince, _channelSince) >= s.Dwell;
                case EncounterTrigger.GazeTarget:
                    if (GazeTargetReady != null && !GazeTargetReady(s.GazeTargetId)) return false;
                    if (GazeTargetExitMode) return ExitTriggered(r, s);   // 61차: 사다리 곁에서 나오는 길
                    return _gazeId == s.GazeTargetId && _gazeRun >= s.Dwell && GazeTargetNear(s.GazeTargetId);
            }

            return false;
        }

        private bool ViewingCctv
        {
            get { return _cctvSince >= 0f && Now - _lastCctvView <= 0.5f; }
        }

        private void TryStartEncounter(float minute, Band auditoryShown, bool slotBusy, bool overlayBusy)
        {
            for (int i = 0; i < _runs.Count; i++)
            {
                EncounterRun r = _runs[i];
                if (r.Overlay ? overlayBusy : slotBusy) continue;
                if (r.State != EncounterRunState.Waiting || Now < r.RetryAt || !SlotOpen(r, minute)) continue;
                if (_held.Contains(r.Def.Id))
                {
                    r.Waiting = "회피 불가 역설의 둘째 조우 — 겹칠 때만";
                    continue;
                }


                if (!Triggered(r, minute))
                {
                    r.Waiting = "방아쇠 대기";
                    continue;
                }

                // 51차: 응시 방아쇠는 플레이어가 그것을 보는 그 순간뿐이다 — 긴장·예산으로 미루면 영영 오지 않는다.
                if (r.Script.Trigger == EncounterTrigger.GazeTarget)
                {
                    Begin(r, auditoryShown);
                    return;
                }

                // 50차: 긴장 절정 중에는 조우를 미룬다 — 단 슬롯 끝 10분 전부터는 그대로 건다(조우는 수칙과 묶여 있다).
                float slotFrom, slotTo;
                RunWindow(r, out slotFrom, out slotTo);
                if (!r.Forced && _pacer.State == PacerState.Peak && minute < slotTo - LastCallMinutes)
                {
                    r.Waiting = "긴장 절정 — 잠시 미룸";
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

            if (r.Def.Intensity < 3 && r.Script.FixedForeshadow <= 0f)
            {
                Engage(r);
                return;
            }

            float scale = DirectorMoods.ForeshadowScale(Mood);
            if (r.Script.FixedForeshadow > 0f)
            {
                r.State = EncounterRunState.Foreshadow;
                r.PhaseEnds = Now + r.Script.FixedForeshadow;
                _pacer.Impulse(PacerImpulse.Foreshadow);
                Emit(DirectionEventKind.Encounter, DirectionPhase.Foreshadow, r.Def.Id, string.Empty, r.Script.Space, r.Def.Intensity, r.Point, r.Script.FixedForeshadow, string.Empty);
                return;
            }

            // 헛예고는 조우마다 한 번까지 — 한 번 속인 뒤에는 진짜가 온다(43차 시뮬: 같은 조우의 헛예고가 26초 사이로 두 번 연달아 나와 김이 샜다).
            float winFrom, winTo;
            RunWindow(r, out winFrom, out winTo);
            // 57차: 슬롯이 얼마 안 남았거나 이월된 실행은 헛예고로 속이지 않는다(진짜를 놓치게 된다).
            if (!r.Forced && r.FalseCount == 0 && !r.CarriedOver && _minute < winTo - LastCallMinutes * 2f)
            {
                float pFalse = (auditoryShown >= Band.Band3 ? 0.4f : 0.3f) * DirectorMoods.FalseScale(Mood);
                if (_rng.NextDouble() < pFalse)
                {
                    float d = Range(1f, 8f) * scale;
                    r.FalseCount++;
                    r.RetryAt = Now + d + FalseRetry;
                    r.Rearmed = true;   // 57차: 다시 시도할 때는 그 방에 있기만 하면
                    _pacer.Impulse(PacerImpulse.FalseForeshadow);
                    Emit(DirectionEventKind.Encounter, DirectionPhase.FalseForeshadow, r.Def.Id, string.Empty, r.Script.Space, r.Def.Intensity, r.Point, d, "헛예고");
                    return;
                }
            }

            float length = r.Forced ? 2f : Range(1f, 8f) * scale;
            if (_restarts > 0 && r.Def.Intensity >= 4 && _seenHigh.Contains(r.Def.Id)) length *= 0.5f;   // 이미 본 강도 4↑의 전조는 절반.
            r.State = EncounterRunState.Foreshadow;
            r.PhaseEnds = Now + length;
            _pacer.Impulse(PacerImpulse.Foreshadow);
            Emit(DirectionEventKind.Encounter, DirectionPhase.Foreshadow, r.Def.Id, string.Empty, r.Script.Space, r.Def.Intensity, r.Point, length, string.Empty);
        }

        /// <summary>61차: 전조 뒤 — 몹 대역이 있는 조우는 대역부터 세우고 플레이어가 보기를 기다린다(<see cref="SightGated"/>), 아니면 곧바로 대면.</summary>
        private void Engage(EncounterRun r)
        {
            if (!SightGated || !r.Script.NeedsSight)
            {
                Confront(r);
                return;
            }

            if (r.Point == Vector3.zero) r.Point = PointFor(r.Script);
            r.State = EncounterRunState.Presenting;
            r.PresentSince = Now;
            r.Seen = false;
            Note(r.Def.Id, "대역을 세움 — 플레이어가 보기를 기다림");
            Emit(DirectionEventKind.Encounter, DirectionPhase.Present, r.Def.Id, r.Script.Cue, r.Script.Space, r.Def.Intensity, r.Point, PresentMaxSeconds, string.Empty);
        }

        /// <summary>61차: 세운 대역을 오래 못 봤다 — 거두고 그 방에 있으면 곧 다시(슬롯이 지나면 대기의 넘김·놓침 규칙을 따른다).</summary>
        private void Withdraw(EncounterRun r)
        {
            Emit(DirectionEventKind.Encounter, DirectionPhase.Aborted, r.Def.Id, r.Script.Cue, r.Script.Space, r.Def.Intensity, r.Point, 0f, "못 보고 지나침");
            Note(r.Def.Id, "대역을 " + PresentMaxSeconds + "초 동안 못 봄 — 거두고 다시 기다림");
            r.State = EncounterRunState.Waiting;
            r.RetryAt = Now + PresentRetrySeconds;
            r.Rearmed = true;
            r.Point = Vector3.zero;
            r.Seen = false;
        }

        private void Confront(EncounterRun r)
        {
            EncounterScript s = r.Script;
            if (!r.Forced && !r.Overlay) _budget.Commit(r.Def.Intensity, Now);
            if (r.Def.Intensity >= 4) _seenHigh.Add(r.Def.Id);

            float window;
            if (s.IsPresence)
            {
                if (r.Forced) window = ForcedPresenceSeconds;
                else
                {
                    float from, to;
                    RunWindow(r, out from, out to);
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
            _pacer.Impulse(PacerImpulse.Confront, r.Def.Intensity);
            if (s.Placement != CuePlacement.None && r.Point == Vector3.zero) r.Point = PointFor(s);

            Emit(DirectionEventKind.Encounter, DirectionPhase.Confront, r.Def.Id, s.Cue, s.Space, r.Def.Intensity, r.Point, window, string.Empty);
            // 51차 K1: CCTV를 보다 걸린 조우의 단서는 지금 채널을 단다(「그 채널을 오래 보면」 판정).
            string cue = s.Trigger == EncounterTrigger.ViewingCctv && s.Cue.Length > 0 && !string.IsNullOrEmpty(_channel) ? s.Cue + "@" + _channel : s.Cue;
            if (s.Cue.Length > 0) _out.Enqueue(JudgeSignal.Cue(cue, r.Point));
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
            if (!r.Forced && !r.Overlay) _budget.EndEncounter(Now, Range(SurpriseBudget.QuietMin, SurpriseBudget.QuietMax));
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
                        if (Now >= r.PhaseEnds) Engage(r);
                        break;
                    case EncounterRunState.Presenting:
                        if (r.Seen) Confront(r);
                        else if (Now - r.PresentSince >= PresentMaxSeconds || (!r.Forced && PastSlot(r, minute))) Withdraw(r);
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
                            // 57차: 슬롯 A·B에서 못 걸린 조우는 판정 끝(03:30)까지 넘긴다 — 그 수칙이 이미 태블릿에 있다.
                            if (!r.CarriedOver && r.Slot != EncounterSlot.C && r.Script.Trigger != EncounterTrigger.GazeTarget && minute < NightClock.JudgingEnd)
                            {
                                r.CarriedOver = true;
                                r.Rearmed = true;
                                Note(r.Def.Id, "슬롯 " + r.Slot + "에 방아쇠가 오지 않음(" + r.Waiting + ") — 04:25까지 넘김");
                                break;
                            }

                            r.State = EncounterRunState.Missed;
                            Note(r.Def.Id, "방아쇠가 끝내 오지 않음(" + r.Waiting + ") — 그 수칙을 거둔다");
                            Action<string> missed = Missed;
                            if (missed != null) missed(r.Def.Id);
                        }

                        break;
                }
            }
        }

        private static bool PastSlot(EncounterRun r, float minute)
        {
            if (r.Script.Trigger == EncounterTrigger.GazeTarget) return minute >= NightClock.JudgingEnd;
            float from, to;
            RunWindow(r, out from, out to);
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
            if (ExitGateBlocks(s)) return false;
            if (s.WhileViewingCctv)
            {
                if (EmptyRoomChannel.Length > 0 && _channel == EmptyRoomChannel) return false;   // 빈 방 채널을 보는 중에는 그 채널을 가리키지 않는다
                return ViewingCctv && Now - _cctvSince >= r.NeedDwell;
            }

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
            if (target == "cctv") target = EmptyRoomChannel.Length > 0 ? EmptyRoomChannel : OtherChannel();
            string cue = target.Length > 0 ? s.Cue + "@" + target : s.Cue;

            r.SentCue = cue;
            r.FiredMinute = _minute;
            _lastRuleCue = Now;
            _pacer.Impulse(PacerImpulse.RuleCue);
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

            // 50차(민: 「첫날부터 도배된 느낌」): 1일차는 첫 조우의 결과를 본 뒤(또는 호출 2 뒤)에만.
            // 슬롯 A에 조우가 없는 1일차(지금 편성)는 호출 1(01:00) 뒤부터.
            if (_day <= 1 && _encountersDone == 0 && minute < (HasSlotA() ? NightClock.Call2 : NightClock.Call1)) return;

            // 일차 상한 2/4/5/6/7(+강도 단계). 옛 「진짜 1에 가짜 3」은 조절기 상한으로 갈음했다(되살리지 말 것).
            int cap = TensionPacer.FakeCap(_day) + DirectorMoods.ExtraFakes(Mood);
            if (_fakesUsed >= cap)
            {
                _nextFake = Now + FakeGapMin;
                return;
            }

            // 조절기: 축적 상태 · 긴장도 50 미만 · 점검 지시 직후가 아닐 때만. 아니면 다음 틱에 다시 본다.
            string why;
            if (!_pacer.AllowsFake(out why)) return;

            // 57차: 점검 대상 가까이·보고 직전에는 미룬다(놀람을 이상 소리로 착각해 오보). 예산을 쓰지 않고 다음 틱에 다시 본다.
            if (FakeBlocked != null && FakeBlocked(_feet)) return;

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
            _pacer.Impulse(PacerImpulse.Fake);
            Emit(DirectionEventKind.FakeScare, DirectionPhase.None, pick, string.Empty, _space, 0, _feet, 0f, string.Empty);
        }

        private bool HasSlotA()
        {
            for (int i = 0; i < _runs.Count; i++)
            {
                if (_runs[i].Slot == EncounterSlot.A && !_runs[i].Forced) return true;
            }

            return false;
        }

        /// <summary>
        /// 이 가짜 놀람을 지금 자리에서 걸 수 있는가. 벌레 떼만 조건이 있다: <see cref="BugSpaces"/>의 방에 <see cref="BugDwellSeconds"/>초 이상 있고,
        /// 그 방에 조우(존재형 포함)가 서 있지 않을 것 — 천장 다리·소녀·노란 얼굴 옆에 벌레가 쏟아져 시선을 끌면 응시 판정이 억울해진다.
        /// </summary>
        private bool FakeAllowedHere(string id)
        {
            if (id == FakeGlimpse) return _day >= 2 && !Busy;
            if (id != FakeBugs) return true;
            if (!FakeBugsEnabled) return false;   // 61차: 벌레 떼는 가짜 놀람으로 내지 않는다
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
                if (r.State != EncounterRunState.Foreshadow && r.State != EncounterRunState.Active && r.State != EncounterRunState.Releasing && r.State != EncounterRunState.Presenting) continue;
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
            _pacer.Reset();

            for (int i = 0; i < _runs.Count; i++)
            {
                EncounterRun r = _runs[i];
                bool before = r.ConfrontMinute >= 0f && r.ConfrontMinute < startMinute;
                if (before && !r.Forced)
                {
                    r.State = EncounterRunState.Done;
                    if (!r.Overlay) _budget.Commit(r.Def.Intensity, float.NegativeInfinity);
                    continue;
                }

                if (r.Forced) continue;
                r.State = EncounterRunState.Waiting;
                r.RetryAt = 0f;
                r.Rearmed = false;
                r.CarriedOver = false;
                r.Waiting = string.Empty;
                r.ConfrontMinute = -1f;
                r.Point = Vector3.zero;
                r.Seen = false;
                r.ExitArmed = false;
                r.DeepSince = -1f;
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

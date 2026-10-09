using System;
using System.Collections.Generic;
using System.Text;

namespace NightDuty
{
    /// <summary>점검 지시 한 통의 종류.</summary>
    public enum OrderKind
    {
        /// <summary>출근 직후 첫 지시(경비실 CCTV가 있으면 그것).</summary>
        Opening = 0,

        /// <summary>평소 지시 — 플레이어의 동선·긴장도·시간을 보고 고른 때.</summary>
        Regular = 1,

        /// <summary>호출 1(01:00).</summary>
        Call1 = 2,

        /// <summary>호출 2(02:16) — 그날 마지막 점검 공간.</summary>
        Call2 = 3,

        /// <summary>남은 점검을 한꺼번에(03:00 또는 시간이 빠듯할 때).</summary>
        CatchUp = 4,

        /// <summary>목격 뒤 지시(51차 T4 — 여자아이가 들어간 칸의 변기). 그 항목 하나만.</summary>
        Witness = 5
    }

    /// <summary>점검 지시 한 통 — 한 공간(또는 따라잡기면 여러 공간)의 항목들.</summary>
    public sealed class InspectionOrder
    {
        /// <summary>그 밤 몇 번째 지시인지(1부터). 태블릿 메시지 ID에 쓴다.</summary>
        public readonly int Index;

        /// <summary>종류.</summary>
        public readonly OrderKind Kind;

        /// <summary>낸 밤 분(모르면 -1).</summary>
        public readonly int Minute;

        /// <summary>지시한 항목(편성 순).</summary>
        public readonly IReadOnlyList<string> ItemIds;

        /// <summary>한 공간이면 그 공간, 여러 공간이면 None.</summary>
        public readonly SpaceId Space;

        /// <summary>만든다.</summary>
        public InspectionOrder(int index, OrderKind kind, int minute, IList<string> itemIds, SpaceId space)
        {
            Index = index;
            Kind = kind;
            Minute = minute;
            ItemIds = new List<string>(itemIds ?? new string[0]).AsReadOnly();
            Space = space;
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return "#" + Index + " " + Kind + " " + Space + " [" + string.Join(", ", ItemIds) + "] @" + Minute;
        }
    }

    /// <summary>지시기에 매 틱 넘기는 바깥 사정.</summary>
    public struct DispatchInput
    {
        /// <summary>밤 시계(모르면 음수).</summary>
        public float Minute;

        /// <summary>흐른 실제 초.</summary>
        public float Dt;

        /// <summary>조우가 전조·대면·마무리 중인지.</summary>
        public bool DirectorBusy;

        /// <summary>긴장 조절기(없으면 null — 긴장으로 미루지 않는다).</summary>
        public TensionPacer Pacer;

        /// <summary>회피 불가 역설로 당일 재입실이 금지된 공간.</summary>
        public SpaceId Banned;
    }

    /// <summary>
    /// 점검 순차 지시기(2026-10-06 50차, 민 요청: 「점검이 한번에 떠 있는 게 아니라, 조절해서 메시지로 계속 업데이트 —
    /// 플레이어의 동선이나 위치, 긴장도를 계속 계산해서 적절한 타이밍에」 · 전문가 회의 4인 종합).
    /// <para>
    /// 그날의 총량·이상 수·편성은 그대로(<see cref="AnomalyAssigner"/>), <b>언제 어느 공간을 알려 줄지</b>만 정한다. 지시는 공간 단위 묶음이다.
    /// 지시받은 항목만 열린다(<see cref="InspectionBoard.MarkIssued"/>, <see cref="InspectionBoard.DripMode"/>).
    /// </para>
    /// <list type="bullet">
    /// <item><b>첫 지시</b>: 밤 시작 8초 뒤 — 경비실(CCTV)이 편성에 있으면 그것, 없으면 가장 가까운 공간.</item>
    /// <item><b>시간표</b>: 먼저 열릴 항목(늦은 공간 제외)을 00:00~03:00에 고르게 — 허용 수 = ⌈항목 수 × 흐른 몫⌉(이완 구간은 시간에서 뺀다).
    /// 빨리 끝낸 플레이어라도 시간표를 한 공간 넘게 앞서 받지 않는다 — 한 밤 내내 지시가 「계속 업데이트」되게.</item>
    /// <item><b>호출 1(01:00)</b>: 슬롯 A 조우가 걸린 공간을 아껴 두었다가 낸다(1일차 = 교실 → 소년 착석). 아껴 둔 공간이 없으면 시간표가 허락할 때만.</item>
    /// <item><b>호출 2(02:16)</b>: 그날 마지막 점검 공간.</item>
    /// <item><b>따라잡기</b>: 03:00이 지났거나, 03:45까지의 여유(남은 실제 초 − 이완 남은 초 − 남은 일의 예상 시간)가 60초 아래면 남은 것을 한 통으로.</item>
    /// <item><b>평소 지시</b>(밀린 지시 0일 때) — 시간표가 허락하고 마지막 보고(또는 지시) 뒤 숨 돌릴 틈(1일차 30초 · 2일차 22초 · 그 뒤 15초) /
    /// 시간표가 허락하고 경비실에 10초 이상 / 한가함(1일차 90초 · 2일차 75초 · 그 뒤 60초)이면 시간표보다 한 공간 앞서.
    /// 밀린 지시 1이면 잔잔하고 60초 지났고 다음 공간이 20m 안일 때만. 시간표보다 두 항목 넘게 늦으면 밀린 지시가 있어도 낸다.</item>
    /// <item><b>막는 때</b>: 조우 중(끝나고 10초), 역설 문자 뒤 12초, 지시 뒤 20초, 이완 구간(01:52~02:16).
    /// 긴장이 뜨거우면(<see cref="TensionPacer.IsHot"/>) 최대 20초 미룬다. 호출·따라잡기는 긴장을 무시하고 직전 지시 뒤 8초·조우는 최대 30초만 기다린다(빠듯하면 호출 1이 따라잡기를 겸한다).</item>
    /// <item><b>공간 고르기</b>: 점수 = −거리/10 + 1.5(지금·곧 조우가 걸린 공간) + 0.8(같은 동) − 1.0(제 동 지시가 밀렸는데 건너편 동).
    /// 거리는 복도(z=44)를 따라 문 위치(화장실 4 · 도서관 12 · 경비실 34 · 교실 40 · 과학실 44)까지 + 방 깊이.</item>
    /// </list>
    /// 재시작(<see cref="ISnapshotable"/>): 낸 지시 수·호출 여부를 스냅샷으로 되돌린다(점검판이 지시받은 항목을 되돌린다).
    /// 코어 안의 순수 계산이다 — 태블릿 문자는 <see cref="EventBus.InspectionOrdered"/>를 받은 TabletBridge가 넣는다.
    /// </summary>
    public sealed class InspectionDispatcher : ISnapshotable
    {
        /// <summary>스냅샷 키.</summary>
        public const string Key = "inspection.orders";

        /// <summary>밤 시작 뒤 첫 지시까지(실제 초).</summary>
        public const float OpeningDelay = 8f;

        /// <summary>지시 사이 최소 간격(초).</summary>
        public const float MinGap = 14f;   // 57차: 20 → 14

        /// <summary>조우가 끝난 뒤 지시를 쉬는 초.</summary>
        public const float AfterBusy = 10f;

        /// <summary>역설 문자 뒤 지시를 쉬는 초.</summary>
        public const float AfterMessage = 12f;

        /// <summary>긴장이 뜨거울 때 최대로 미루는 초.</summary>
        public const float HotHoldMax = 20f;

        /// <summary>호출·따라잡기도 직전 지시 뒤 이만큼은 띄운다(알람 두 번이 겹치지 않게).</summary>
        public const float ForcedGap = 8f;

        /// <summary>호출·따라잡기가 조우를 기다리는 최대 초.</summary>
        public const float ForcedBusyWaitMax = 30f;

        /// <summary>경비실에 이만큼 머물고 밀린 지시가 없으면 다음 지시.</summary>
        public const float GuardRoomIdle = 10f;

        /// <summary>잔잔할 때 연속 지시 — 지난 지시 뒤 최소 초.</summary>
        public const float LullGap = 27f;   // 57차: 60 → 40 · 59차(밤 10분): 40 → 27

        /// <summary>잔잔할 때 연속 지시 — 다음 공간까지 최대 거리(m).</summary>
        public const float LullRange = 20f;

        /// <summary>먼저 열릴 항목을 모두 낼 시각(01:40) — 시간표의 끝. 51차(민: 「초반 3개는 괜찮은데 이후 2개가 너무 늦다」): 03:00 → 01:40.</summary>
        public const int EarlyDeadline = 100;

        /// <summary>남은 지시가 있으면 지난 지시 뒤 이 시간(실제 초, 게임 30분) 안에 다음 지시를 낸다(밀린 지시 1 이하일 때).</summary>
        public const float MaxQuiet = 47f;   // 57차: 112 → 70 · 59차(밤 10분): 70 → 47(게임 약 19분)

        /// <summary>
        /// 받은 지시를 다 보고하면 이만큼 뒤 다음 지시(53차 민: 「점검을 기다리는 과정이 루즈하다 — 한번 점검하면 몇 초 뒤에 바로 다음」).
        /// 시간표·지시 간격·긴장 미룸을 보지 않는다(호출 1 몫·늦은 공간·이완 구간·조우 중·문자 직후는 그대로 지킨다).
        /// </summary>
        public const float NextAfterReport = 4f;

        /// <summary>여유를 재는 끝(03:45) — 04:00 전에 다 끝낼 수 있어야 한다.</summary>
        public const int WorkDeadline = 225;

        /// <summary>따라잡기 여유 기준(초).</summary>
        public const float SlackMin = 60f;

        /// <summary>예상 걷기 속도(m/s, 멈춤·둘러봄 포함).</summary>
        public const float WalkSpeed = 1.2f;

        /// <summary>항목당 예상 작업(초) — 찾기·1초 응시·길게 누르기·판단.</summary>
        public const float SecondsPerItem = 27f;

        /// <summary>조우가 곧 열린다고 보는 게임 분.</summary>
        public const float ArmedLeadMinutes = 8f;

        /// <summary>복도 중심선 z.</summary>
        public const float CorridorZ = 44f;

        private readonly InspectionBoard _board;
        private readonly int _day;
        private readonly List<KeyValuePair<EncounterSlot, SpaceId>> _encounters = new List<KeyValuePair<EncounterSlot, SpaceId>>();
        private readonly List<InspectionOrder> _orders = new List<InspectionOrder>();
        private SpaceId _reserved;
        private readonly List<KeyValuePair<SpaceId, float>> _later = new List<KeyValuePair<SpaceId, float>>();

        private readonly List<KeyValuePair<string, float>> _witness = new List<KeyValuePair<string, float>>();
        private bool _call1Done;
        private bool _call2Done;
        private float _begunAt;
        private float _lastIssue = float.NegativeInfinity;
        private float _lastReport = float.NegativeInfinity;
        private float _lastMessage = float.NegativeInfinity;
        private float _busyEndedAt = float.NegativeInfinity;
        private float _hotSince = -1f;
        private float _forcedSince = -1f;
        private float _guardSince = -1f;
        private SpaceId _space = SpaceId.None;
        private bool _hasPose;
        private float _x;
        private float _z;

        /// <summary>
        /// 지시기를 만든다. <paramref name="encounters"/> = 그날 슬롯별 조우 공간(정규화한 공간) — 호출 1에 아껴 둘 공간과 「조우가 걸린 공간」 가산점에 쓴다.
        /// </summary>
        public InspectionDispatcher(InspectionBoard board, int day, IEnumerable<KeyValuePair<EncounterSlot, SpaceId>> encounters)
        {
            _board = board ?? throw new ArgumentNullException(nameof(board));
            _day = Math.Max(1, day);
            if (encounters != null)
            {
                foreach (KeyValuePair<EncounterSlot, SpaceId> kv in encounters) _encounters.Add(new KeyValuePair<EncounterSlot, SpaceId>(kv.Key, SpaceIds.Canonical(kv.Value)));
            }

            bool fallback;
            _reserved = PickReserved(out fallback);
            PickLater();
            KeepOneEarlySpace(fallback);
        }

        /// <summary>지시기 시각(누적 실제 초).</summary>
        public float Now { get; private set; }

        /// <summary>마지막 지시·보고 뒤 흐른 실제 초(둘 다 없으면 밤 시작부터) — [근무 지시]의 공백 규칙(54차).</summary>
        public float SinceActivity
        {
            get
            {
                float last = Math.Max(_lastReport, _lastIssue);
                return Now - (float.IsNegativeInfinity(last) ? _begunAt : last);
            }
        }

        /// <summary>낸 지시(순서대로).</summary>
        public IReadOnlyList<InspectionOrder> Orders
        {
            get { return _orders; }
        }

        /// <summary>호출 1에 아껴 둔 공간(없으면 None).</summary>
        public SpaceId Reserved
        {
            get { return _reserved; }
        }

        /// <summary>
        /// 슬롯 B·C 조우가 걸린 방이 풀리는 밤 분(없으면 -1). 53차 플레이 점검: 보고 뒤 바로 다음 지시가 오자 B·C 조우 방의 점검이 00:10쯤 끝나
        /// 조우 시간대(02:16~ · 03:08~)에 아무도 그 방에 가지 않았다 — 그 방은 슬롯이 열릴 때까지 아껴 둔다(호출 1 몫과 같은 생각).
        /// </summary>
        public float ReleaseOf(SpaceId space)
        {
            space = SpaceIds.Canonical(space);
            for (int i = 0; i < _later.Count; i++)
            {
                if (_later[i].Key == space) return _later[i].Value;
            }

            return -1f;
        }

        /// <summary>마지막으로 지시를 미룬 이유(디버그 콘솔).</summary>
        public string Waiting { get; private set; } = string.Empty;

        /// <inheritdoc/>
        public string SnapshotKey
        {
            get { return Key; }
        }

        /// <summary>일차별 한가함(초): 45 · 40 · 35 — 밀린 지시 없이 이만큼 지나면 시간표보다 한 공간 앞서 낸다(51차: 90·75·60에서 줄임).</summary>
        public static float IdleMax(int day)
        {
            if (day <= 1) return 45f;
            if (day == 2) return 40f;
            return 35f;
        }

        /// <summary>시간표의 「흐른 시간」(게임 분) — 이완 구간(01:52~02:16)은 뺀다.</summary>
        public static float ActiveMinutes(float minute)
        {
            if (minute <= 0f) return 0f;
            return Math.Min(minute, NightClock.RelaxStart) + Math.Max(0f, minute - NightClock.Call2);
        }

        /// <summary>그 시각까지 지시해도 되는 먼저 열릴 항목 수(시간표).</summary>
        public int Allowed(float minute)
        {
            float end = ActiveMinutes(EarlyDeadline);
            float g = Math.Max(0f, Math.Min(1f, ActiveMinutes(minute) / end));
            return (int)Math.Ceiling(EarlyTotal() * g - 0.0001f);
        }

        /// <summary>일차별 숨 돌릴 틈(초): 30 · 22 · 15.</summary>
        public static float ReliefGap(int day)
        {
            if (day <= 1) return 30f;
            if (day == 2) return 22f;
            return 15f;
        }

        // ── 입력 ───────────────────────────────────────────────

        /// <summary>플레이어 신호(공간·자세).</summary>
        public void Observe(in JudgeSignal s)
        {
            switch (s.Kind)
            {
                case SignalKind.SpaceEntered:
                    _space = SpaceIds.Canonical(s.Space);
                    _guardSince = _space == SpaceId.SecurityRoom ? Now : -1f;
                    break;
                case SignalKind.SpaceExited:
                    if (_space == SpaceIds.Canonical(s.Space))
                    {
                        _space = SpaceId.None;
                        _guardSince = -1f;
                    }

                    break;
                case SignalKind.PlayerPose:
                    _x = s.Point.x;
                    _z = s.Point.z;
                    _hasPose = true;
                    break;
            }
        }

        /// <summary>보고를 받았다(숨 돌릴 틈의 기준).</summary>
        public void NoteReport()
        {
            _lastReport = Now;
        }

        /// <summary>역설·회피 불가 문자가 왔다(12초 쉰다).</summary>
        public void NoteMessage()
        {
            _lastMessage = Now;
        }

        /// <summary>목격 뒤 지시까지(초) — 51차 T4: 여자아이가 칸에 들어가는 것을 보고 3초 뒤 그 칸 변기.</summary>
        public const float WitnessDelay = 3f;

        /// <summary>
        /// 묶어 둔 항목(<see cref="InspectionBoard.Hold"/>)을 <paramref name="delay"/>초 뒤 단독 지시로 낸다(51차 T4 목격).
        /// 조우·긴장·간격을 기다리지 않는다(직전 지시 뒤 <see cref="ForcedGap"/>만). 이미 냈거나 묶여 있지 않으면 false.
        /// </summary>
        public bool QueueWitness(string itemId, float delay = WitnessDelay)
        {
            if (itemId == null || !_board.IsHeld(itemId)) return false;
            for (int i = 0; i < _witness.Count; i++)
            {
                if (_witness[i].Key == itemId) return false;
            }

            _witness.Add(new KeyValuePair<string, float>(itemId, Now + Math.Max(0f, delay)));
            return true;
        }

        // ── 시간 ───────────────────────────────────────────────

        /// <summary>시간 경과. 지시를 냈으면 그 지시(점검판에 이미 표시함), 아니면 null.</summary>
        public InspectionOrder Tick(DispatchInput input)
        {
            if (input.Dt > 0f) Now += input.Dt;
            if (input.DirectorBusy) _busyEndedAt = Now;

            float minute = input.Minute;
            int whole = minute < 0f ? -1 : (int)Math.Floor(minute);
            bool known = minute >= 0f;

            // 0) 목격 뒤 지시(51차 T4) — 다른 무엇보다 먼저.
            for (int i = 0; i < _witness.Count; i++)
            {
                if (Now < _witness[i].Value || Now - _lastIssue < ForcedGap) continue;
                string id = _witness[i].Key;
                _witness.RemoveAt(i);
                if (!_board.Release(id)) return null;
                return Issue(OrderKind.Witness, whole, new List<string> { id });
            }

            // 1) 첫 지시.
            if (_orders.Count == 0 && !AllIssuedOrReported())
            {
                if (Now - _begunAt < OpeningDelay)
                {
                    Waiting = "첫 지시 대기";
                    return null;
                }

                if (input.DirectorBusy) return Hold("조우 중");
                List<string> first = OpeningBatch(minute, input.Banned);
                if (first.Count > 0) return Issue(OrderKind.Opening, whole, first);
            }

            // 2) 호출 2 — 마지막 점검 공간.
            if (!_call2Done && known && minute >= NightClock.Call2)
            {
                List<string> late = LateBatch(input.Banned);
                if (late.Count == 0)
                {
                    _call2Done = true;
                }
                else if (ForcedReady(input.DirectorBusy))
                {
                    _call2Done = true;
                    return Issue(OrderKind.Call2, whole, late);
                }
                else
                {
                    return Hold("호출 2 — 조우 끝을 기다림");
                }
            }

            // 3) 호출 1.
            if (!_call1Done && known && minute >= NightClock.Call1)
            {
                List<string> call = Call1Batch(minute, input.Banned);
                if (_reserved == SpaceId.None && EarlyIssued() >= Allowed(minute) + 1) call.Clear();   // 아껴 둔 공간이 없고 이미 앞서 있다 — 호출은 쉰다

                // 빠듯하면 호출 1이 따라잡기를 겸한다(문자 두 통이 잇달아 오지 않게).
                if (call.Count > 0 && (minute >= EarlyDeadline || Slack(minute, input.Banned) < SlackMin))
                {
                    foreach (string id in EarlyUnissued(minute, input.Banned, true))
                    {
                        if (!call.Contains(id)) call.Add(id);
                    }
                }

                if (call.Count == 0)
                {
                    _call1Done = true;
                }
                else if (ForcedReady(input.DirectorBusy))
                {
                    _call1Done = true;
                    return Issue(OrderKind.Call1, whole, call);
                }
                else
                {
                    return Hold("호출 1 — 조우 끝을 기다림");
                }
            }

            // 4) 따라잡기 — 03:00이 지났거나 03:45까지 빠듯하다(이완 구간에는 걸지 않는다).
            bool relax = known && minute >= NightClock.RelaxStart && minute < NightClock.Call2;
            if (known && !relax && HasEarlyUnissued(minute, input.Banned, true))
            {
                bool deadline = minute >= EarlyDeadline;
                bool tight = Slack(minute, input.Banned) < SlackMin;
                if (deadline || tight)
                {
                    if (!ForcedReady(input.DirectorBusy)) return Hold("따라잡기 — 조우 끝을 기다림");
                    if (!_call1Done && minute >= NightClock.Call1) _call1Done = true;
                    List<string> rest = EarlyUnissued(minute, input.Banned, true);
                    return Issue(OrderKind.CatchUp, whole, rest);
                }
            }

            _forcedSince = -1f;

            // 5) 평소 지시.
            if (relax) return Hold("이완 구간");

            bool beforeCall1 = !_call1Done;
            string space;
            SpaceId pick = BestSpace(minute, input.Banned, beforeCall1, out space);
            if (pick == SpaceId.None)
            {
                Waiting = "낼 지시 없음";
                _hotSince = -1f;
                return null;
            }

            string reason = DueReason(minute, pick, beforeCall1, input);
            if (reason.Length == 0)
            {
                Waiting = "때를 기다림";
                _hotSince = -1f;
                return null;
            }

            if (input.DirectorBusy) return Hold("조우 중");
            if (Now - _busyEndedAt < AfterBusy) return Hold("조우 직후");
            if (Now - _lastMessage < AfterMessage) return Hold("문자 직후");
            bool quick = reason == QuickReason;   // 53차: 보고 뒤 바로 — 지시 간격·긴장 미룸을 건너뛴다
            if (!quick && Now - _lastIssue < MinGap) return Hold("지시 간격");
            if (!quick && input.Pacer != null && input.Pacer.IsHot)
            {
                if (_hotSince < 0f) _hotSince = Now;
                if (Now - _hotSince < HotHoldMax) return Hold("긴장 " + input.Pacer.State);
            }

            _hotSince = -1f;
            return Issue(OrderKind.Regular, whole, UnissuedIn(pick, input.Banned));
        }

        private InspectionOrder Hold(string why)
        {
            Waiting = why;
            return null;
        }

        private bool ForcedReady(bool busy)
        {
            if (_forcedSince < 0f) _forcedSince = Now;
            if (Now - _lastIssue < ForcedGap) return false;
            return !busy || Now - _forcedSince >= ForcedBusyWaitMax;
        }

        private InspectionOrder Issue(OrderKind kind, int minute, List<string> ids)
        {
            List<string> issued = new List<string>();
            for (int i = 0; i < ids.Count; i++)
            {
                if (_board.MarkIssued(ids[i])) issued.Add(ids[i]);
            }

            _forcedSince = -1f;
            _hotSince = -1f;
            if (issued.Count == 0) return null;

            SpaceId space = SpaceId.None;
            for (int i = 0; i < issued.Count; i++)
            {
                SpaceId s = SpaceOf(issued[i]);
                if (i == 0) space = s;
                else if (s != space)
                {
                    space = SpaceId.None;
                    break;
                }
            }

            InspectionOrder order = new InspectionOrder(_orders.Count + 1, kind, minute, issued, space);
            _orders.Add(order);
            _lastIssue = Now;
            Waiting = string.Empty;
            return order;
        }

        // ── 무엇을 낼까 ─────────────────────────────────────────

        /// <summary>호출 1에 아껴 둘 공간: 슬롯 A 조우가 걸린 공간 중 먼저 열릴 항목이 있는 곳(복도 제외 — 53차부터 경비실 CCTV 조우도 아낀다). 없으면 편성의 호출 1 항목 공간(53차).</summary>
        private SpaceId PickReserved(out bool fallback)
        {
            fallback = false;
            for (int i = 0; i < _encounters.Count; i++)
            {
                if (_encounters[i].Key != EncounterSlot.A) continue;
                SpaceId s = _encounters[i].Value;
                if (s == SpaceId.None) continue;
                if (s == SpaceId.Corridor) continue;   // 복도는 늘 지나다니므로 아낄 까닭이 없다
                // 53차: 경비실 조우(CCTV에만 보이는 사람)는 CCTV를 볼 때 열린다 — K-1을 첫 지시로 써 버리면 조우 시간대에 아무도 CCTV를 보지 않았다.
                foreach (InspectionAssignment a in _board.Plan.Assignments)
                {
                    if (!a.IsLate && SpaceIds.Canonical(a.Item.Space) == s) return s;
                }
            }

            // 53차: 슬롯 A 조우가 없으면(1일차) 편성이 정한 호출 1 항목의 공간을 아낀다 — 보고 뒤 바로 다음 지시가 와서
            // 먼저 열릴 공간이 00:15쯤 바닥나면 01:00 호출이 빈 채로 지나가고 02:16까지 실제 7분 넘게 아무것도 오지 않았다.
            string callItem = _board.Plan.Call1ItemId;
            InspectionAssignment call = string.IsNullOrEmpty(callItem) ? null : _board.Plan.Find(callItem);
            if (call != null && !call.IsLate)
            {
                SpaceId cs = SpaceIds.Canonical(call.Item.Space);
                if (cs != SpaceId.SecurityRoom && cs != SpaceId.Corridor && cs != SpaceId.None)
                {
                    fallback = true;
                    return cs;
                }
            }

            return SpaceId.None;
        }

        /// <summary>슬롯 B·C 조우 방(복도·호출 1 몫 제외, 먼저 열릴 항목이 있는 곳 — 경비실 CCTV 조우 포함) → 그 슬롯이 열리는 분.</summary>
        private void PickLater()
        {
            for (int i = 0; i < _encounters.Count; i++)
            {
                EncounterSlot slot = _encounters[i].Key;
                SpaceId s = _encounters[i].Value;
                if (slot == EncounterSlot.A || s == SpaceId.None || s == SpaceId.Corridor || s == _reserved) continue;
                bool early = false;
                foreach (InspectionAssignment a in _board.Plan.Assignments)
                {
                    if (!a.IsLate && SpaceIds.Canonical(a.Item.Space) == s) early = true;
                }

                if (!early) continue;
                float from, to;
                TensionDirector.SlotWindow(slot, out from, out to);
                int at = _later.FindIndex(kv => kv.Key == s);
                if (at < 0) _later.Add(new KeyValuePair<SpaceId, float>(s, from));
                else if (from < _later[at].Value) _later[at] = new KeyValuePair<SpaceId, float>(s, from);
            }
        }

        /// <summary>
        /// 먼저 열릴 공간을 모두 아끼면 출근부터 01:00까지 점검이 하나도 없다(54차 4일차 실측 — 화장실 = 호출 1 몫, 과학실 = 슬롯 B, 교실 = 슬롯 C).
        /// 하나는 남도록 덜 중요한 것부터 푼다: 조우 없는 밤의 호출 1 몫 → 슬롯 C 방 → 슬롯 B 방.
        /// </summary>
        private void KeepOneEarlySpace(bool fallback)
        {
            for (int guard = 0; guard < 8 && FreeEarlySpaces() == 0; guard++)
            {
                if (fallback && _reserved != SpaceId.None)
                {
                    _reserved = SpaceId.None;
                    fallback = false;
                    continue;
                }

                if (_later.Count == 0) break;
                int last = 0;
                for (int i = 1; i < _later.Count; i++)
                {
                    if (_later[i].Value > _later[last].Value) last = i;
                }

                _later.RemoveAt(last);
            }
        }

        private int FreeEarlySpaces()
        {
            List<SpaceId> free = new List<SpaceId>();
            foreach (InspectionAssignment a in _board.Plan.Assignments)
            {
                if (a.IsLate || _board.IsHeld(a.Id)) continue;
                SpaceId s = SpaceIds.Canonical(a.Item.Space);
                if (s == _reserved || ReleaseOf(s) >= 0f || free.Contains(s)) continue;
                free.Add(s);
            }

            return free.Count;
        }

        /// <summary>그 방이 아직 슬롯을 기다리는지(밤 시계를 모르면 기다리지 않는다).</summary>
        private bool Waits(SpaceId space, float minute)
        {
            if (minute < 0f) return false;
            float at = ReleaseOf(space);
            return at >= 0f && minute < at;
        }

        private List<string> OpeningBatch(float minute, SpaceId banned)
        {
            List<string> guard = _reserved == SpaceId.SecurityRoom || Waits(SpaceId.SecurityRoom, minute) ? new List<string>() : UnissuedIn(SpaceId.SecurityRoom, banned);
            if (guard.Count > 0) return guard;
            string unused;
            SpaceId s = BestSpace(minute, banned, true, out unused);
            return s == SpaceId.None ? guard : UnissuedIn(s, banned);
        }

        private List<string> LateBatch(SpaceId banned)
        {
            List<string> ids = new List<string>();
            foreach (InspectionAssignment a in _board.Plan.Assignments)
            {
                if (!a.IsLate || !Eligible(a, banned)) continue;
                ids.Add(a.Id);
            }

            return ids;
        }

        private List<string> Call1Batch(float minute, SpaceId banned)
        {
            List<string> ids = new List<string>();
            if (_reserved != SpaceId.None) ids.AddRange(UnissuedIn(_reserved, banned));

            if (ids.Count == 0)
            {
                string unused;
                SpaceId s = BestSpace(minute, banned, false, out unused);
                if (s != SpaceId.None) ids.AddRange(UnissuedIn(s, banned));
            }

            return ids;
        }

        /// <summary>지금 낼 만한 가장 좋은 공간. 호출 1 전에는 아껴 둔 공간을 뺀다. 늦은 공간은 호출 2에만.</summary>
        private SpaceId BestSpace(float minute, SpaceId banned, bool keepReserved, out string why)
        {
            SpaceId best = SpaceId.None;
            float bestScore = float.NegativeInfinity;
            why = string.Empty;
            foreach (SpaceId s in CandidateSpaces(minute, banned, keepReserved))
            {
                float score = Score(s, minute);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = s;
                }
            }

            return best;
        }

        private List<SpaceId> CandidateSpaces(float minute, SpaceId banned, bool keepReserved)
        {
            List<SpaceId> list = new List<SpaceId>();
            foreach (InspectionAssignment a in _board.Plan.Assignments)
            {
                if (a.IsLate || !Eligible(a, banned)) continue;
                SpaceId s = SpaceIds.Canonical(a.Item.Space);
                if (keepReserved && s == _reserved) continue;
                if (Waits(s, minute)) continue;   // 53차: 슬롯 B·C 조우 방은 그 슬롯이 열릴 때까지
                if (!list.Contains(s)) list.Add(s);
            }

            return list;
        }

        /// <summary>공간 점수 = −거리/10 + 1.5(조우가 걸린 공간) + 0.8(같은 동) − 1.0(제 동 지시가 밀렸는데 건너편).</summary>
        public float Score(SpaceId space, float minute)
        {
            float score = -Cost(space) / 10f;
            if (Armed(space, minute)) score += 1.5f;

            Wing here = SpaceIds.WingOf(PlayerSpaceForWing());
            Wing there = SpaceIds.WingOf(space);
            if (here != Wing.Center && there == here) score += 0.8f;
            if (here != Wing.Center && there != Wing.Center && there != here && BacklogInWing(here)) score -= 1.0f;
            return score;
        }

        private SpaceId PlayerSpaceForWing()
        {
            if (_space != SpaceId.None) return _space;
            if (!_hasPose) return SpaceId.SecurityRoom;
            return _x < 24f ? SpaceId.Library : SpaceId.ScienceRoom;
        }

        private bool BacklogInWing(Wing wing)
        {
            foreach (InspectionAssignment a in _board.Plan.Assignments)
            {
                if (SpaceIds.WingOf(a.Item.Space) != wing) continue;
                if (_board.IsIssued(a.Id) && _board.StateOf(a.Id) == InspectionState.Pending) return true;
            }

            return false;
        }

        private bool Armed(SpaceId space, float minute)
        {
            if (minute < 0f) return false;
            for (int i = 0; i < _encounters.Count; i++)
            {
                if (_encounters[i].Value != space) continue;
                float from, to;
                TensionDirector.SlotWindow(_encounters[i].Key, out from, out to);
                if (minute >= from - ArmedLeadMinutes && minute < to) return true;
            }

            return false;
        }

        /// <summary>플레이어 자리에서 그 공간까지의 대략 거리(m) — 복도를 따라 문까지 + 방 깊이.</summary>
        public float Cost(SpaceId space)
        {
            space = SpaceIds.Canonical(space);
            if (space == _space && space != SpaceId.None) return 0f;

            float px = _hasPose ? _x : 34f;
            float pz = _hasPose ? _z : 46f;
            if (space == SpaceId.Corridor) return Math.Abs(pz - CorridorZ);
            return Math.Abs(px - DoorX(space)) + Math.Abs(pz - CorridorZ) + Depth(space);
        }

        /// <summary>그 공간 문의 복도 x(월드 m).</summary>
        public static float DoorX(SpaceId space)
        {
            switch (SpaceIds.Canonical(space))
            {
                case SpaceId.Toilet: return 4f;
                case SpaceId.Library: return 12f;
                case SpaceId.SecurityRoom: return 34f;
                case SpaceId.Classroom: return 40f;
                case SpaceId.ScienceRoom: return 44f;
                default: return 28f;
            }
        }

        /// <summary>문에서 점검 물품까지의 대략 깊이(m).</summary>
        public static float Depth(SpaceId space)
        {
            switch (SpaceIds.Canonical(space))
            {
                case SpaceId.Toilet: return 10f;
                case SpaceId.Corridor: return 0f;
                case SpaceId.SecurityRoom: return 2f;
                default: return 4f;
            }
        }

        private const string QuickReason = "보고 뒤 바로";

        /// <summary>평소 지시를 낼 까닭(없으면 빈 문자열).</summary>
        private string DueReason(float minute, SpaceId pick, bool beforeCall1, DispatchInput input)
        {
            int backlog = _board.IssuedPendingCount;
            float since = Math.Max(_lastReport, _lastIssue);
            int issued = EarlyIssued();
            int allowed = minute < 0f ? int.MaxValue / 2 : Allowed(minute);

            // 53차: 받은 지시를 다 보고했으면 몇 초 뒤 바로 다음 공간(시간표를 보지 않는다).
            if (backlog == 0 && _lastReport >= _lastIssue && _lastReport > float.NegativeInfinity && Now - _lastReport >= NextAfterReport) return QuickReason;

            if (backlog == 0)
            {
                if (issued < allowed && Now - since >= ReliefGap(_day)) return "시간표 · 숨 돌림";
                if (issued < allowed && _guardSince >= 0f && _space == SpaceId.SecurityRoom && Now - _guardSince >= GuardRoomIdle) return "시간표 · 경비실";
                if (issued < allowed + 1 && Now - since >= IdleMax(_day)) return "한가함";
            }

            if (backlog == 1 && issued < allowed + 1 && (input.Pacer == null || input.Pacer.IsCalm) && Now - _lastIssue >= LullGap && Cost(pick) <= LullRange) return "잔잔함 · 가까움";
            if (issued + 1 < allowed && Now - _lastIssue >= LullGap) return "시간표보다 늦음";
            if (backlog <= 1 && issued < allowed + 1 && Now - _lastIssue >= MaxQuiet) return "너무 오래 조용함";   // 시간표보다 한 공간 넘게 앞서지는 않는다
            return string.Empty;
        }

        // ── 남은 시간 ───────────────────────────────────────────

        /// <summary>03:45까지 남은 실제 초 − 남은 이완 구간 − 남은 일(보고 안 한 모든 항목, 늦은 공간 포함)의 예상 시간.</summary>
        public float Slack(float minute, SpaceId banned)
        {
            float m = Math.Max(0f, minute);
            float left = NightClock.RealSecondsAt(WorkDeadline) - NightClock.RealSecondsAt(m);
            if (m < NightClock.Call2) left -= NightClock.RealSecondsAt(NightClock.Call2 - Math.Max(m, NightClock.RelaxStart));
            return left - EstimateWork(banned);
        }

        /// <summary>남은 일의 예상 실제 초 — 가까운 공간부터 차례로 도는 경로 / 걷기 속도 + 항목당 27초.</summary>
        public float EstimateWork(SpaceId banned)
        {
            List<SpaceId> spaces = new List<SpaceId>();
            int items = 0;
            foreach (InspectionAssignment a in _board.Plan.Assignments)
            {
                if (!Eligible(a, banned, true)) continue;
                items++;
                SpaceId s = SpaceIds.Canonical(a.Item.Space);
                if (!spaces.Contains(s)) spaces.Add(s);
            }

            float x = _hasPose ? _x : 34f;
            float route = 0f;
            SpaceId at = _space;
            while (spaces.Count > 0)
            {
                int bestI = 0;
                float bestD = float.PositiveInfinity;
                for (int i = 0; i < spaces.Count; i++)
                {
                    float d = spaces[i] == at ? 0f : Math.Abs(x - DoorX(spaces[i])) + Depth(spaces[i]) * 2f;
                    if (d < bestD)
                    {
                        bestD = d;
                        bestI = i;
                    }
                }

                route += bestD;
                x = DoorX(spaces[bestI]);
                at = spaces[bestI];
                spaces.RemoveAt(bestI);
            }

            return route / WalkSpeed + items * SecondsPerItem;
        }

        // ── 셈 ────────────────────────────────────────────────

        private bool Eligible(InspectionAssignment a, SpaceId banned, bool includeIssuedPending = false)
        {
            if (_board.StateOf(a.Id) != InspectionState.Pending) return false;
            if (_board.IsHeld(a.Id)) return false;   // 51차: 목격 전 T4 변기
            if (banned != SpaceId.None && SpaceIds.Canonical(a.Item.Space) == banned) return false;
            return includeIssuedPending || !_board.IsIssued(a.Id);
        }

        private List<string> UnissuedIn(SpaceId space, SpaceId banned)
        {
            List<string> ids = new List<string>();
            foreach (InspectionAssignment a in _board.Plan.Assignments)
            {
                if (SpaceIds.Canonical(a.Item.Space) != space || !Eligible(a, banned)) continue;
                if (a.IsLate && !_call2Done) continue;
                ids.Add(a.Id);
            }

            return ids;
        }

        private List<string> EarlyUnissued(float minute, SpaceId banned, bool includeReserved)
        {
            List<string> ids = new List<string>();
            foreach (InspectionAssignment a in _board.Plan.Assignments)
            {
                if (a.IsLate || !Eligible(a, banned)) continue;
                if (!includeReserved && SpaceIds.Canonical(a.Item.Space) == _reserved) continue;
                if (Waits(SpaceIds.Canonical(a.Item.Space), minute)) continue;
                ids.Add(a.Id);
            }

            return ids;
        }

        private bool HasEarlyUnissued(float minute, SpaceId banned, bool includeReserved)
        {
            return EarlyUnissued(minute, banned, includeReserved).Count > 0;
        }

        private bool AllIssuedOrReported()
        {
            foreach (InspectionAssignment a in _board.Plan.Assignments)
            {
                if (_board.StateOf(a.Id) == InspectionState.Pending && !_board.IsIssued(a.Id)) return false;
            }

            return true;
        }

        private int EarlyTotal()
        {
            int n = 0;
            foreach (InspectionAssignment a in _board.Plan.Assignments)
            {
                if (!a.IsLate && !_board.IsHeld(a.Id)) n++;
            }

            return n;
        }

        private int EarlyIssued()
        {
            int n = 0;
            foreach (InspectionAssignment a in _board.Plan.Assignments)
            {
                if (!a.IsLate && (_board.IsIssued(a.Id) || _board.StateOf(a.Id) != InspectionState.Pending)) n++;
            }

            return n;
        }

        private SpaceId SpaceOf(string itemId)
        {
            InspectionAssignment a = _board.Plan.Find(itemId);
            return a != null ? SpaceIds.Canonical(a.Item.Space) : SpaceId.None;
        }

        // ── 재시작 ───────────────────────────────────────────────

        /// <inheritdoc/>
        public object CaptureState()
        {
            return new State { Orders = _orders.Count, Call1 = _call1Done, Call2 = _call2Done, Witness = new List<KeyValuePair<string, float>>(_witness) };
        }

        /// <inheritdoc/>
        public void RestoreState(object state)
        {
            State s = state as State;
            if (s == null) return;
            if (s.Orders < _orders.Count) _orders.RemoveRange(s.Orders, _orders.Count - s.Orders);
            _call1Done = s.Call1;
            _call2Done = s.Call2;
            _witness.Clear();
            if (s.Witness != null)
            {
                foreach (KeyValuePair<string, float> w in s.Witness) _witness.Add(new KeyValuePair<string, float>(w.Key, Now + WitnessDelay));
            }

            _begunAt = Now;
            _lastIssue = Now;
            _lastReport = float.NegativeInfinity;
            _lastMessage = float.NegativeInfinity;
            _busyEndedAt = float.NegativeInfinity;
            _hotSince = -1f;
            _forcedSince = -1f;
            _guardSince = -1f;
            _space = SpaceId.None;
            _hasPose = false;
            Waiting = string.Empty;
        }

        /// <summary>디버그: 지금 가장 좋은 공간 하나를 조건 없이 지시한다(호출 1 몫은 남긴다). 낼 것이 없으면 null.</summary>
        public InspectionOrder ForceNext(float minute, SpaceId banned)
        {
            string unused;
            SpaceId s = BestSpace(minute, banned, !_call1Done, out unused);
            if (s == SpaceId.None) s = BestSpace(minute, banned, false, out unused);
            if (s == SpaceId.None) return null;
            return Issue(OrderKind.Regular, minute < 0f ? -1 : (int)Math.Floor(minute), UnissuedIn(s, banned));
        }

        /// <summary>지금 상태 한 줄(디버그 콘솔).</summary>
        public string Describe(float minute, SpaceId banned)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("지시 ").Append(_orders.Count).Append("통 · 시간표 ").Append(EarlyIssued()).Append('/').Append(Allowed(minute))
                .Append(" · 밀림 ").Append(_board.IssuedPendingCount)
                .Append(" · 아껴둠 ").Append(_reserved)
                .Append(" · 여유 ").Append((int)Slack(minute, banned)).Append("초");
            if (Waiting.Length > 0) sb.Append(" · ").Append(Waiting);
            return sb.ToString();
        }

        private sealed class State
        {
            public int Orders;
            public bool Call1;
            public bool Call2;
            public List<KeyValuePair<string, float>> Witness;
        }
    }
}

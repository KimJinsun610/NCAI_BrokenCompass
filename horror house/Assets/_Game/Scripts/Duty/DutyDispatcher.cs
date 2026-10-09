using System;
using System.Collections.Generic;

namespace NightDuty
{
    /// <summary>[근무 지시] 한 건에 일어난 일.</summary>
    public enum DutyOutcome
    {
        /// <summary>지시가 나갔다.</summary>
        Issued = 0,

        /// <summary>해냈다(답장 「~이 기록되었습니다. 시각」).</summary>
        Done = 1,

        /// <summary>기한을 넘겼다(답장 「~ 기록이 없습니다.」, 경고 1).</summary>
        Missed = 2
    }

    /// <summary>지시기가 돌려주는 사건.</summary>
    public readonly struct DutyEvent
    {
        /// <summary>지시 정의.</summary>
        public readonly DutyDef Def;

        /// <summary>무슨 일인지.</summary>
        public readonly DutyOutcome Outcome;

        /// <summary>하는 동안 수칙을 어겼는지(완료해도 축을 빼 주지 않는다 — 지시는 수칙 위반을 면책하지 않는다).</summary>
        public readonly bool Tainted;

        /// <summary>만든다.</summary>
        public DutyEvent(DutyDef def, DutyOutcome outcome, bool tainted)
        {
            Def = def;
            Outcome = outcome;
            Tainted = tainted;
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return (Def != null ? Def.Id : "?") + " " + Outcome + (Tainted ? "(위반 있음)" : string.Empty);
        }
    }

    /// <summary>지시기에 매 틱 넘기는 바깥 사정.</summary>
    public struct DutyInput
    {
        /// <summary>밤 시계(모르면 음수).</summary>
        public float Minute;

        /// <summary>흐른 실제 초.</summary>
        public float Dt;

        /// <summary>조우가 전조·대면·마무리 중인지(새 지시를 쉬고 기한 시계를 멈춘다).</summary>
        public bool DirectorBusy;

        /// <summary>받았지만 아직 보고하지 않은 점검 수.</summary>
        public int InspectionBacklog;

        /// <summary>마지막 점검 지시·보고 뒤 흐른 실제 초.</summary>
        public float SinceInspection;

        /// <summary>피날레 중인지.</summary>
        public bool Finale;

        /// <summary>67차: 지난 지시가 근무 지시였고 점검 지시 차례다 — 새 근무 지시를 쉰다(번갈아 낸다).</summary>
        public bool InspectionTurn;
    }

    /// <summary>
    /// [근무 지시] 지시기(2026-10-07 54차 — 3차 회의 제안서 「시스템 규칙」, 민: 「물품만 조사하니까 틀린그림찾기 같다」).
    /// <list type="bullet">
    /// <item><b>공백 규칙</b>: 받은 점검을 다 보고하고 다음 점검 지시가 <see cref="GapSeconds"/> 동안 오지 않으면 그날 낼 수 있는 지시 하나(지금 자리에서 가까운 공간 먼저).
    /// 53차 「보고 뒤 바로 다음 점검」 뒤에 남는 실제 3~5분의 공백을 메운다.</item>
    /// <item><b>마디</b>: 하루 상한(<see cref="DutyCatalog.DailyCap"/>)을 호출 마디마다 나눈다(<see cref="Allowed"/> — 01:00 전 2 · 02:16 전 3 · 그 뒤 4).</item>
    /// <item><b>막는 때</b>: 판정 구간(00:16~03:30) 밖, 조우 중, 역설 문자 뒤 <see cref="AfterMessage"/>초, 지시가 끝난 뒤 <see cref="AfterDuty"/>초, 이완 구간, 피날레. 진행 중인 지시는 하나뿐.</item>
    /// <item><b>근무일지(W5)</b>: 이완 구간(01:52)이 열리면 낸다. 이완 구간이 끝날 때까지 서명하면 완료.</item>
    /// <item><b>기한</b>: 지시마다 실제 초(<see cref="DutyDef.Seconds"/>), 조우 중에는 멈춘다. 문구에는 쓰지 않는다(카운트다운 미션처럼 읽히지 않게).</item>
    /// <item><b>결과</b>: 완료 = 맞물린 수칙의 축 −3(수행 중 수칙 위반이 있으면 없음), 미완료 = 경고 1. 신뢰는 이 게임에서 줄지 않으므로 제안서의 「신뢰 −2」 대신 경고를 쓴다.</item>
    /// <item><b>문 정리(W4)</b>: 플레이어가 연 문(연출이 연 문 제외 — H2 「열린 문은 열린 채로」와 맞물린다)이 하나라도 열려 있을 때만 낸다.</item>
    /// </list>
    /// 코어 안의 순수 계산이다 — 문자·축·경고는 <see cref="NightRun"/>이 사건을 받아 처리한다.
    /// </summary>
    public sealed class DutyDispatcher : ISnapshotable
    {
        /// <summary>스냅샷 키.</summary>
        public const string Key = "duty.orders";

        /// <summary>점검 공백이 이만큼 이어지면 지시(실제 초 — 제안서 「게임 8분」).</summary>
        public const float GapSeconds = 10f;   // 57차: 30 → 40(지시가 점검을 덮지 않게) · 67차(민: 「점검과 지시 비중을 균일하게」): 40 → 10 — 점검 지시와 번갈아 낸다

        /// <summary>역설 문자 뒤 쉬는 초.</summary>
        public const float AfterMessage = 12f;

        /// <summary>지시가 끝난 뒤 다음 지시까지 최소 초.</summary>
        public const float AfterDuty = 12f;   // 67차: 20 → 12

        private readonly int _day;
        private readonly Func<SpaceId, float> _cost;
        private readonly int _channels;
        private readonly HashSet<string> _finished = new HashSet<string>();
        private readonly HashSet<string> _openDoors = new HashSet<string>();
        private readonly Dictionary<string, float> _channelSeconds = new Dictionary<string, float>();
        private readonly HashSet<string> _zones = new HashSet<string>();
        private readonly HashSet<string> _insideNow = new HashSet<string>();   // 57차: 지시 전부터 서 있던 구역(들어간 순간만 신호가 온다)
        private readonly Random _rng;
        private readonly Func<string, bool> _planned;

        private DutyDef _active;
        private float _left;
        private bool _tainted;
        private int _issued;
        private bool _signed;
        private float _gaze;
        private bool _inZone;
        private bool _stepDone;
        private float _lastMessage = float.NegativeInfinity;
        private float _lastEnd = float.NegativeInfinity;

        /// <summary>
        /// 만든다. <paramref name="cost"/> = 플레이어 자리에서 그 공간까지의 대략 거리(m, 점검 지시기의 계산을 빌린다 — 없으면 0),
        /// <paramref name="cctvChannels"/> = CCTV 채널 수(W1).
        /// </summary>
        public DutyDispatcher(int day, Func<SpaceId, float> cost, int cctvChannels, Func<string, bool> itemPlanned = null, Random rng = null)
        {
            _day = Math.Max(1, day);
            _cost = cost;
            _channels = Math.Max(1, cctvChannels);
            _planned = itemPlanned;
            _rng = rng;
        }

        /// <summary>지시기 시각(누적 실제 초).</summary>
        public float Now { get; private set; }

        /// <summary>진행 중인 지시(없으면 null).</summary>
        public DutyDef Active
        {
            get { return _active; }
        }

        /// <summary>진행 중인 지시의 남은 기한(초).</summary>
        public float SecondsLeft
        {
            get { return _active != null ? _left : 0f; }
        }

        /// <summary>오늘 낸 지시 수(W5 제외).</summary>
        public int IssuedToday
        {
            get { return _issued; }
        }

        /// <summary>플레이어가 열어 두고 아직 닫지 않은 문.</summary>
        public IReadOnlyCollection<string> OpenDoors
        {
            get { return _openDoors; }
        }

        /// <summary>오늘 끝난(완료·미완료) 지시인지.</summary>
        public bool Finished(string id)
        {
            return _finished.Contains(id);
        }

        /// <inheritdoc/>
        public string SnapshotKey
        {
            get { return Key; }
        }

        // ── 입력 ───────────────────────────────────────────────

        /// <summary>역설·회피 불가 문자가 왔다.</summary>
        public void NoteMessage()
        {
            _lastMessage = Now;
        }

        /// <summary>수칙을 어겼다 — 진행 중인 지시는 완료해도 축을 빼 주지 않는다.</summary>
        public void NoteViolation()
        {
            if (_active != null) _tainted = true;
        }

        /// <summary>근무일지에 서명했다. W5가 진행 중이면 완료 사건.</summary>
        public bool NoteSigned(out DutyEvent ev)
        {
            _signed = true;
            ev = default;
            if (_active == null || _active.Kind != DutyKind.LogSign) return false;
            ev = Finish(DutyOutcome.Done);
            return true;
        }

        /// <summary>65차: 물건 하나를 주웠다. 진행 중인 줍기 지시의 대상이면 완료 사건.</summary>
        public bool NotePicked(string target, out DutyEvent ev)
        {
            ev = default;
            if (_active == null || _active.Kind != DutyKind.Pickup || string.IsNullOrEmpty(target) || _active.Target != target) return false;
            ev = Finish(DutyOutcome.Done);
            return true;
        }

        /// <summary>65차: 지금 그 물건을 줍기를 바라는 지시가 진행 중인지(연출 쪽이 [E] 줍기를 켤 때 본다).</summary>
        public bool Wants(string target)
        {
            return _active != null && _active.Kind == DutyKind.Pickup && _active.Target == target;
        }

        /// <summary>판정 신호 하나. 진행 중인 지시를 끝냈으면 완료 사건.</summary>
        public bool Observe(in JudgeSignal s, out DutyEvent ev)
        {
            ev = default;
            if (s.Kind == SignalKind.DoorCommandAccepted && s.Source == ActionSource.Player && !string.IsNullOrEmpty(s.TargetId))
            {
                if (s.Flag) _openDoors.Remove(s.TargetId);
                else _openDoors.Add(s.TargetId);
            }

            if (s.Kind == SignalKind.ZoneEntered && s.TargetId != null) _insideNow.Add(s.TargetId);
            else if (s.Kind == SignalKind.ZoneExited && s.TargetId != null) _insideNow.Remove(s.TargetId);

            if (_active == null) return false;
            bool done = false;
            switch (_active.Kind)
            {
                case DutyKind.CctvSweep:
                    if (s.Kind == SignalKind.CctvViewSample && !string.IsNullOrEmpty(s.TargetId))
                    {
                        float t;
                        _channelSeconds.TryGetValue(s.TargetId, out t);
                        _channelSeconds[s.TargetId] = t + Math.Max(0f, s.Value);
                        int seen = 0;
                        foreach (float v in _channelSeconds.Values)
                        {
                            if (v >= DutyCatalog.ChannelSeconds) seen++;
                        }

                        done = seen >= _channels;
                    }

                    break;
                case DutyKind.Patrol:
                    if (s.Kind == SignalKind.ZoneEntered && s.TargetId != null && s.TargetId.StartsWith(DutyCatalog.LibraryZonePrefix, StringComparison.Ordinal))
                    {
                        _zones.Add(s.TargetId);
                    }
                    else if (_zones.Count >= DutyCatalog.LibraryZones && LeftSpace(s, SpaceId.Library))
                    {
                        done = true;
                    }

                    break;
                case DutyKind.LightsOutCheck:
                    if (s.Kind == SignalKind.ZoneEntered && s.TargetId == DutyCatalog.ClassroomDoorZone) _inZone = true;
                    else if (s.Kind == SignalKind.GazeSample) done = _inZone && Gazed(s, DutyCatalog.ClassroomLightTarget);
                    break;
                case DutyKind.DoorTidy:
                    done = s.Kind == SignalKind.DoorCommandAccepted && _openDoors.Count == 0;
                    break;
                case DutyKind.ExitSignCheck:
                    if (!_stepDone)
                    {
                        if (s.Kind == SignalKind.ZoneEntered && s.TargetId == DutyCatalog.ExitZone) _inZone = true;
                        else if (s.Kind == SignalKind.ZoneExited && s.TargetId == DutyCatalog.ExitZone)
                        {
                            _inZone = false;
                            _gaze = 0f;
                        }
                        else if (s.Kind == SignalKind.GazeSample && _inZone && Gazed(s, DutyCatalog.ExitSignTarget)) _stepDone = true;
                    }
                    else
                    {
                        done = s.Kind == SignalKind.SpaceEntered && SpaceIds.Canonical(s.Space) == SpaceId.SecurityRoom;
                    }

                    break;
                case DutyKind.GazeCheck:
                    if (s.Kind == SignalKind.GazeSample) done = Gazed(s, _active.Target);
                    break;
            }

            if (!done) return false;
            ev = Finish(DutyOutcome.Done);
            return true;
        }

        private static bool LeftSpace(in JudgeSignal s, SpaceId space)
        {
            if (s.Kind == SignalKind.SpaceExited) return SpaceIds.Canonical(s.Space) == space;
            return s.Kind == SignalKind.SpaceEntered && s.Space != SpaceId.None && SpaceIds.Canonical(s.Space) != space;
        }

        /// <summary>연속 응시 누적 — 다른 것을 보면 처음부터.</summary>
        private bool Gazed(in JudgeSignal s, string target)
        {
            if (s.TargetId == target) _gaze += Math.Max(0f, s.Value);
            else _gaze = 0f;
            return _gaze >= DutyCatalog.GazeSeconds;
        }

        // ── 시간 ───────────────────────────────────────────────

        /// <summary>시간 경과. 지시를 냈거나 기한이 지났으면 그 사건.</summary>
        public bool Tick(DutyInput input, out DutyEvent ev)
        {
            ev = default;
            if (input.Dt > 0f) Now += input.Dt;
            float minute = input.Minute;
            bool known = minute >= 0f;

            if (_active != null)
            {
                if (_active.Seconds <= 0f)
                {
                    // 67차 ②(민: 「W2·W4·W5·W15는 시간 제한 없음 — 시간 초과 처리도 하지 않게」): 미완료 답장·경고 없이 조용히 닫는 때만 있다 —
                    // 근무일지(W5)는 서명할 수 없게 되는 이완 끝(호출 2), 나머지는 판정 구간 끝.
                    float closeAt = _active.Kind == DutyKind.LogSign ? NightClock.Call2 : NightClock.JudgingEnd;
                    if (known && minute >= closeAt) CloseSilently();
                    return false;
                }

                if (input.Dt > 0f) _left -= input.Dt;   // 67차 ②: 조우 중에도 흐른다 — 태블릿에 적힌 마감 시각(「02:08까지」)과 맞게
                if (_left > 0f) return false;
                ev = Finish(DutyOutcome.Missed);
                return true;
            }

            if (!known || input.Finale) return false;

            // 근무일지 — 이완 구간이 열리면.
            bool relax = minute >= NightClock.RelaxStart && minute < NightClock.Call2;
            if (relax)
            {
                DutyDef log = DutyCatalog.Find("W5");
                if (log == null || _signed || _finished.Contains(log.Id) || !log.OnDay(_day)) return false;
                ev = Issue(log);
                return true;
            }

            if (minute < NightClock.JudgingStart || minute >= NightClock.JudgingEnd) return false;
            if (input.DirectorBusy || input.InspectionBacklog > 0 || input.SinceInspection < GapSeconds || input.InspectionTurn) return false;
            if (Now - _lastMessage < AfterMessage || Now - _lastEnd < AfterDuty) return false;
            if (_issued >= Allowed(minute)) return false;

            DutyDef pick = Pick();
            if (pick == null) return false;
            ev = Issue(pick);
            return true;
        }

        /// <summary>
        /// 그 시각까지 낼 수 있는 지시 수 — 밤의 마디(호출)마다 나눈다: 호출 1(01:00) 전 2 · 호출 2(02:16) 전 3 · 그 뒤 4(하루 상한까지).
        /// 54차 플레이 점검: 상한만 두면 지시가 00:20~01:15에 몰리고 02:16 뒤가 다시 비었다. 점검이 00:10쯤 바닥나는 첫 마디에 둘, 01:00~01:52에 하나, 02:16~03:30에 하나.
        /// </summary>
        public int Allowed(float minute)
        {
            int n = minute < NightClock.Call1 ? 2 : minute < NightClock.Call2 ? 3 : 5;   // 57차: 2·3·4 → 1·2·상한 · 67차: 2·3·상한(점검 지시와 번갈아)
            return Math.Min(DutyCatalog.DailyCap(_day), n);
        }

        /// <summary>
        /// 67차: 지금 새 지시를 낼 수 있는지(점검 지시기가 「근무 지시 차례」를 가늠할 때) — 진행 중인 지시 없음 · 판정 구간 · 이완 아님 · 마디 상한 · 낼 지시 있음.
        /// 점검 공백·간격은 보지 않는다(그것은 차례가 오면 Tick이 지킨다). 난수를 쓰지 않는다.
        /// </summary>
        public bool CanIssueSoon(float minute)
        {
            if (_active != null || minute < NightClock.JudgingStart || minute >= NightClock.JudgingEnd) return false;
            if (minute >= NightClock.RelaxStart && minute < NightClock.Call2) return false;
            if (_issued >= Allowed(minute)) return false;
            IReadOnlyList<DutyDef> all = DutyCatalog.All;
            for (int i = 0; i < all.Count; i++)
            {
                DutyDef d = all[i];
                if (d.Kind == DutyKind.LogSign || !d.OnDay(_day) || _finished.Contains(d.Id)) continue;
                if (d.Kind == DutyKind.DoorTidy && _openDoors.Count == 0) continue;
                if (d.Kind == DutyKind.GazeCheck && _planned != null && _planned(d.Item)) continue;
                return true;
            }

            return false;
        }

        /// <summary>
        /// 68차(민: 「조기퇴근은 모든 지시가 완료됐을 때만 — 점검이 끝난 뒤에 추가 지시가 오는 문제」): 오늘 처리할 [근무 지시]가 아직 남았는지.
        /// <list type="bullet">
        /// <item>진행 중인 지시가 있다(제한 없는 W2·W4·W15도 끝내기 전까지는 남은 것이다).</item>
        /// <item>근무일지(W5)를 아직 안 했고 그 시간(호출 2 전)이 지나지 않았다 — 이완 구간이 열리면 나온다.</item>
        /// <item>판정 구간이 끝나기 전이고, 하루 상한(<see cref="DutyCatalog.DailyCap"/>)까지 덜 냈고, 낼 수 있는 지시가 남았다 — 마디 상한(<see cref="Allowed"/>)에 막혀 있어도 다음 마디에 나온다.</item>
        /// </list>
        /// 기한을 넘겨 놓친 지시는 끝난 것으로 본다(다시 할 수 없다). 난수를 쓰지 않는다.
        /// </summary>
        public bool Pending(float minute)
        {
            if (_active != null) return true;
            if (minute < 0f) return false;

            DutyDef log = DutyCatalog.Find("W5");
            if (log != null && log.OnDay(_day) && !_signed && !_finished.Contains(log.Id) && minute < NightClock.Call2) return true;

            if (minute >= NightClock.JudgingEnd || _issued >= DutyCatalog.DailyCap(_day)) return false;
            IReadOnlyList<DutyDef> all = DutyCatalog.All;
            for (int i = 0; i < all.Count; i++)
            {
                DutyDef d = all[i];
                if (d.Kind == DutyKind.LogSign || !d.OnDay(_day) || _finished.Contains(d.Id)) continue;
                if (d.Kind == DutyKind.DoorTidy && _openDoors.Count == 0) continue;   // 열어 둔 문이 없으면 정리할 것도 없다
                if (d.Kind == DutyKind.GazeCheck && _planned != null && _planned(d.Item)) continue;
                return true;
            }

            return false;
        }

        /// <summary>그날 낼 수 있고 아직 하지 않은 지시 중 지금 자리에서 가장 가까운 것.</summary>
        public DutyDef Pick()
        {
            List<KeyValuePair<float, DutyDef>> open = new List<KeyValuePair<float, DutyDef>>();
            IReadOnlyList<DutyDef> all = DutyCatalog.All;
            for (int i = 0; i < all.Count; i++)
            {
                DutyDef d = all[i];
                if (d.Kind == DutyKind.LogSign || !d.OnDay(_day) || _finished.Contains(d.Id)) continue;
                if (d.Kind == DutyKind.DoorTidy && _openDoors.Count == 0) continue;
                if (d.Kind == DutyKind.GazeCheck && _planned != null && _planned(d.Item)) continue;   // 57차: 그날 점검 대상은 비품 확인으로 내지 않는다
                open.Add(new KeyValuePair<float, DutyDef>(_cost != null ? _cost(d.Space) : 0f, d));
            }

            if (open.Count == 0) return null;
            open.Sort((a, b) => a.Key.CompareTo(b.Key));   // 안정 정렬이 아니어도 같은 거리면 카탈로그 순이 앞서게 아래에서 고른다
            if (_rng == null)
            {
                DutyDef best = open[0].Value;
                float bestCost = open[0].Key;
                for (int i = 1; i < open.Count; i++)
                {
                    if (open[i].Key < bestCost) { bestCost = open[i].Key; best = open[i].Value; }
                }

                foreach (DutyDef d in DutyCatalog.All)
                {
                    for (int i = 0; i < open.Count; i++) if (open[i].Value == d && open[i].Key == bestCost) return d;
                }

                return best;
            }

            // 57차: 가까운 셋 중 하나 — 늘 같은 지시가 먼저 나오지 않게.
            return open[_rng.Next(Math.Min(3, open.Count))].Value;
        }

        /// <summary>디버그·연출: 그 지시를 지금 낸다(진행 중인 지시가 있으면 false).</summary>
        public bool Force(string id, out DutyEvent ev)
        {
            ev = default;
            DutyDef d = DutyCatalog.Find(id);
            if (d == null || _active != null) return false;
            ev = Issue(d);
            return true;
        }

        private DutyEvent Issue(DutyDef d)
        {
            _active = d;
            _left = d.Seconds > 0f ? d.Seconds : float.PositiveInfinity;
            _tainted = false;
            _gaze = 0f;
            _inZone = false;
            _stepDone = false;
            _zones.Clear();
            _channelSeconds.Clear();
            // 57차(도서관 순찰이 잘 끝나지 않음): 지시가 올 때 이미 서 있던 구역도 센다 — 구역 신호는 들어가는 순간에만 온다.
            foreach (string z in _insideNow)
            {
                if (z.StartsWith(DutyCatalog.LibraryZonePrefix, StringComparison.Ordinal)) _zones.Add(z);
            }

            if (d.Kind == DutyKind.LightsOutCheck) _inZone = _insideNow.Contains(DutyCatalog.ClassroomDoorZone);
            else if (d.Kind == DutyKind.ExitSignCheck) _inZone = _insideNow.Contains(DutyCatalog.ExitZone);
            if (d.Kind != DutyKind.LogSign) _issued++;
            return new DutyEvent(d, DutyOutcome.Issued, false);
        }

        /// <summary>제한 없는 지시를 사건 없이 닫는다(67차 ②).</summary>
        private void CloseSilently()
        {
            _finished.Add(_active.Id);
            _active = null;
            _lastEnd = Now;
        }

        private DutyEvent Finish(DutyOutcome outcome)
        {
            DutyDef d = _active;
            bool tainted = _tainted;
            _finished.Add(d.Id);
            _active = null;
            _lastEnd = Now;
            return new DutyEvent(d, outcome, tainted);
        }

        // ── 재시작 ───────────────────────────────────────────────

        /// <inheritdoc/>
        public object CaptureState()
        {
            return new State { Finished = new List<string>(_finished), Issued = _issued, Signed = _signed };
        }

        /// <inheritdoc/>
        public void RestoreState(object state)
        {
            State s = state as State;
            if (s == null) return;
            _finished.Clear();
            foreach (string id in s.Finished) _finished.Add(id);
            _issued = s.Issued;
            _signed = s.Signed;
            _active = null;
            _openDoors.Clear();
            _lastEnd = Now;
            _lastMessage = float.NegativeInfinity;
        }

        private sealed class State
        {
            public List<string> Finished;
            public int Issued;
            public bool Signed;
        }
    }
}

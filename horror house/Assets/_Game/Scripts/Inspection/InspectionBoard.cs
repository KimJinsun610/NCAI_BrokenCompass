using System;
using System.Collections.Generic;

namespace NightDuty
{
    /// <summary>점검 항목의 보고 상태. <b>보고는 되돌릴 수 없다</b> — 항목당 1회, 재시작해도 「보고됨」으로 남는다.</summary>
    public enum InspectionState
    {
        /// <summary>아직 보고하지 않음.</summary>
        Pending = 0,

        /// <summary>[정상]으로 보고함.</summary>
        ReportedNormal = 1,

        /// <summary>[이상]으로 보고함.</summary>
        ReportedAnomaly = 2
    }

    /// <summary>보고 한 건의 판정(최종 기획서 「보고 경제」).</summary>
    public enum ReportOutcome
    {
        /// <summary>받지 않은 보고(편성에 없음·이미 보고함·아직 열리지 않음·밤이 아님·붙잡힘).</summary>
        Rejected = 0,

        /// <summary>이상을 정확히 [이상] — 그 축 −5(축마다 밤당 −10까지).</summary>
        CorrectAnomaly = 1,

        /// <summary>정상을 정확히 [정상] — 변화 없음.</summary>
        CorrectNormal = 2,

        /// <summary>이상을 [정상] — 놓침, 그 축 +8.</summary>
        Missed = 3,

        /// <summary>정상을 [이상] — 오보, 그 항목의 축 +7.</summary>
        FalseReport = 4,

        /// <summary>T4가 걸린 T-1을 [정상] — 역보고 준수, 신뢰 +3(놓침 없음).</summary>
        ReverseKept = 6,

        /// <summary>T4가 걸린 T-1을 [이상] — T4 위반, 배치 +12(오보를 따로 더하지 않음).</summary>
        ReverseViolated = 7
    }

    /// <summary>보고를 받지 않은 이유.</summary>
    public enum ReportRejection
    {
        /// <summary>받았다.</summary>
        None = 0,

        /// <summary>밤이 진행 중이 아니다.</summary>
        NoNight = 1,

        /// <summary>오늘 편성에 없는 항목이다.</summary>
        NotInPlan = 2,

        /// <summary>이미 보고했다.</summary>
        AlreadyReported = 3,

        /// <summary>그날 마지막 점검 공간이라 호출 2(02:16) 전에는 비활성이다.</summary>
        NotOpenYet = 4,

        /// <summary>붙잡힌 뒤다.</summary>
        Captured = 5,

        /// <summary>회피 불가 역설로 그 공간이 당일 재입실 불가다(10단계).</summary>
        SpaceClosed = 6
    }

    /// <summary>보고 한 건의 결과. 태블릿·연출·근무일지가 읽는다(수치는 화면에 내지 않는다).</summary>
    public readonly struct InspectionReport
    {
        /// <summary>항목 ID.</summary>
        public readonly string ItemId;

        /// <summary>[이상]을 눌렀는지.</summary>
        public readonly bool SaidAnomaly;

        /// <summary>판정.</summary>
        public readonly ReportOutcome Outcome;

        /// <summary>받지 않았으면 그 이유.</summary>
        public readonly ReportRejection Rejection;

        /// <summary>변화가 걸린 축(신뢰 포함).</summary>
        public readonly FearAxis Axis;

        /// <summary>실제로 바뀐 양(뺀 것은 음수). 상한·하한에 걸리면 표보다 작다.</summary>
        public readonly int Change;

        /// <summary>결과를 만든다.</summary>
        public InspectionReport(string itemId, bool saidAnomaly, ReportOutcome outcome, ReportRejection rejection, FearAxis axis, int change)
        {
            ItemId = itemId ?? string.Empty;
            SaidAnomaly = saidAnomaly;
            Outcome = outcome;
            Rejection = rejection;
            Axis = axis;
            Change = change;
        }

        /// <summary>받은 보고인지.</summary>
        public bool Accepted
        {
            get { return Outcome != ReportOutcome.Rejected; }
        }

        /// <summary>받지 않은 보고를 만든다.</summary>
        public static InspectionReport Reject(string itemId, bool saidAnomaly, ReportRejection why)
        {
            return new InspectionReport(itemId, saidAnomaly, ReportOutcome.Rejected, why, default, 0);
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return ItemId + (SaidAnomaly ? " [이상] " : " [정상] ") + (Accepted ? Outcome + " " + Axis + " " + Change : "거절 " + Rejection);
        }
    }

    /// <summary>04:00 정산 결과.</summary>
    public readonly struct InspectionSettlement
    {
        /// <summary>미완료 항목 수(= 새로 준 경고 수, 결근이면 경고 없음).</summary>
        public readonly int Unfinished;

        /// <summary>미완료 중 이상이 있던 항목 수(각 +8).</summary>
        public readonly int UnfinishedAnomalies;

        /// <summary>결과를 만든다.</summary>
        public InspectionSettlement(int unfinished, int unfinishedAnomalies)
        {
            Unfinished = unfinished;
            UnfinishedAnomalies = unfinishedAnomalies;
        }
    }

    /// <summary>
    /// 하룻밤 점검판 — 편성(<see cref="InspectionPlan"/>)과 보고 상태, 보고 경제의 델타 적용.
    /// <list type="bullet">
    /// <item>보고는 항목당 1회, 되돌릴 수 없다. 거리(2m)·응시(1초)·길게 누르기(0.5초)는 센서와 태블릿이 거른 뒤 부른다.</item>
    /// <item>그날 마지막 점검 공간의 항목은 호출 2(02:16) 전에 비활성이다(그 공간 수칙은 판정한다).</item>
    /// <item>정확 보고 −5는 축마다 밤당 −10까지. 한도 사용량은 재시작 스냅샷에 들어간다.</item>
    /// <item>「가까이」(0.8m 들여다보기·뒤로 돌아가기·건드리기)는 항목마다 한 번 그 축 +6(<see cref="Startle"/>).</item>
    /// <item>04:00 정산(<see cref="Settle"/>): 미완료마다 경고 1, 이상이 있던 항목은 그 축 +8.</item>
    /// </list>
    /// 재시작 때 되돌리는 것(<see cref="ISnapshotable"/>): 한도 사용량·「가까이」 기록·역보고 표시. 이미 한 보고는 되돌리지 않는다.
    /// </summary>
    public sealed class InspectionBoard : ISnapshotable
    {
        /// <summary>스냅샷 키.</summary>
        public const string Key = "inspection";

        private const int SensoryAxes = 3;

        private InspectionPlan _plan = InspectionPlan.Empty(0);
        private readonly Dictionary<string, InspectionState> _states = new Dictionary<string, InspectionState>(StringComparer.Ordinal);
        private readonly int[] _reliefUsed = new int[SensoryAxes];
        private readonly HashSet<string> _startled = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _reverse = new HashSet<string>(StringComparer.Ordinal);

        /// <inheritdoc/>
        public string SnapshotKey
        {
            get { return Key; }
        }

        /// <summary>오늘 편성. 밤 전에는 빈 편성.</summary>
        public InspectionPlan Plan
        {
            get { return _plan; }
        }

        /// <summary>점검 항목 수.</summary>
        public int Total
        {
            get { return _plan.Count; }
        }

        /// <summary>보고한 항목 수.</summary>
        public int ReportedCount
        {
            get
            {
                int n = 0;
                foreach (KeyValuePair<string, InspectionState> kv in _states)
                {
                    if (kv.Value != InspectionState.Pending) n++;
                }

                return n;
            }
        }

        /// <summary>남은 항목 수(03:30 「잔여 점검 n건」 호출이 읽는다).</summary>
        public int RemainingCount
        {
            get { return Total - ReportedCount; }
        }

        /// <summary>새 밤의 편성으로 시작한다. 보고 상태·한도·표시를 모두 비운다.</summary>
        public void Begin(InspectionPlan plan)
        {
            _plan = plan ?? InspectionPlan.Empty(0);
            _states.Clear();
            for (int i = 0; i < _plan.Assignments.Count; i++)
            {
                _states[_plan.Assignments[i].Id] = InspectionState.Pending;
            }

            Array.Clear(_reliefUsed, 0, _reliefUsed.Length);
            _startled.Clear();
            _reverse.Clear();
        }

        /// <summary>항목의 보고 상태. 편성에 없으면 Pending.</summary>
        public InspectionState StateOf(string itemId)
        {
            InspectionState state;
            return itemId != null && _states.TryGetValue(itemId, out state) ? state : InspectionState.Pending;
        }

        /// <summary>
        /// 지금 보고할 수 있는 항목인지(편성에 있고, 열렸고, 아직 보고하지 않음).
        /// </summary>
        /// <param name="itemId">항목 ID.</param>
        /// <param name="minute">밤 시계(근무 시작부터의 분). 모르면 -1 — 이때는 늦은 공간도 열린 것으로 본다.</param>
        public bool CanReport(string itemId, int minute)
        {
            return Check(itemId, minute) == ReportRejection.None;
        }

        /// <summary>항목이 열렸는지. 그날 마지막 점검 공간은 호출 2(02:16)부터.</summary>
        public bool IsOpen(string itemId, int minute)
        {
            InspectionAssignment a = _plan.Find(itemId);
            return a != null && (!a.IsLate || minute < 0 || minute >= NightClock.Call2);
        }

        /// <summary>그 감각 축에서 이번 밤 정확 보고로 뺀 양(0~10).</summary>
        public int ReliefUsed(FearAxis axis)
        {
            int i = (int)axis;
            return i >= 0 && i < SensoryAxes ? _reliefUsed[i] : 0;
        }

        /// <summary>
        /// T4(「변기는 정상입니다.」)가 이 항목에 걸렸는지 켠다 — 여자아이가 칸으로 들어가는 것을 봤을 때 수칙 쪽이 부른다.
        /// 켜져 있으면 [정상] = 역보고 준수, [이상] = T4 위반으로만 판정한다.
        /// </summary>
        public void SetReverseReport(string itemId, bool active)
        {
            if (itemId == null) return;
            if (active) _reverse.Add(itemId);
            else _reverse.Remove(itemId);
        }

        /// <summary>역보고가 걸렸는지.</summary>
        public bool IsReverse(string itemId)
        {
            return itemId != null && _reverse.Contains(itemId);
        }

        /// <summary>그 항목의 「가까이」를 이미 어겼는지.</summary>
        public bool WasStartled(string itemId)
        {
            return itemId != null && _startled.Contains(itemId);
        }

        /// <summary>보고를 판정만 한다(부작용 없음). 받을 수 없는 보고면 Rejected.</summary>
        public ReportOutcome Classify(string itemId, bool saysAnomaly)
        {
            InspectionAssignment a = _plan.Find(itemId);
            if (a == null) return ReportOutcome.Rejected;

            if (IsReverse(itemId))
            {
                return saysAnomaly ? ReportOutcome.ReverseViolated : ReportOutcome.ReverseKept;
            }

            if (a.IsAnomaly)
            {
                return saysAnomaly ? ReportOutcome.CorrectAnomaly : ReportOutcome.Missed;
            }

            return saysAnomaly ? ReportOutcome.FalseReport : ReportOutcome.CorrectNormal;
        }

        /// <summary>
        /// 보고를 받아 판정하고 델타를 적용한다. 상한(95 등)은 부르는 쪽이 <see cref="FearAxisSystem.SoftCap"/>으로 건다.
        /// </summary>
        /// <param name="itemId">항목 ID.</param>
        /// <param name="saysAnomaly">[이상]이면 true.</param>
        /// <param name="minute">밤 시계. 모르면 -1.</param>
        /// <param name="axes">생존 수치.</param>
        /// <param name="space">지금 공간(붙잡힘 원인 기록용).</param>
        public InspectionReport Report(string itemId, bool saysAnomaly, int minute, FearAxisSystem axes, SpaceId space)
        {
            if (axes == null) throw new ArgumentNullException(nameof(axes));
            if (axes.IsLocked) return InspectionReport.Reject(itemId, saysAnomaly, ReportRejection.Captured);

            ReportRejection why = Check(itemId, minute);
            if (why != ReportRejection.None) return InspectionReport.Reject(itemId, saysAnomaly, why);

            InspectionAssignment a = _plan.Find(itemId);
            ReportOutcome outcome = Classify(itemId, saysAnomaly);
            _states[itemId] = saysAnomaly ? InspectionState.ReportedAnomaly : InspectionState.ReportedNormal;

            string source = itemId + (saysAnomaly ? "[이상]" : "[정상]");
            FearAxis axis = a.Item.Axis;
            int change = 0;
            switch (outcome)
            {
                case ReportOutcome.CorrectAnomaly:
                    change = -Relieve(axes, axis, source);
                    break;
                case ReportOutcome.Missed:
                    change = Raise(axes, axis, Deltas.MissedAnomaly, source, space);
                    break;
                case ReportOutcome.FalseReport:
                    change = Raise(axes, axis, Deltas.FalseReport, source, space);
                    break;
                case ReportOutcome.ReverseKept:
                    axis = FearAxis.Trust;
                    change = Raise(axes, axis, Deltas.TrustReverseReport, "T4:" + source, space);
                    break;
                case ReportOutcome.ReverseViolated:
                    axis = FearAxis.Layout;
                    change = Raise(axes, axis, Deltas.RuleViolation, "T4:" + source, space);
                    break;
            }

            return new InspectionReport(itemId, saysAnomaly, outcome, ReportRejection.None, axis, change);
        }

        /// <summary>
        /// 점검 수칙 「가까이」 위반 — 그 항목의 축 +6(항목마다 한 번). 실제로 적용했으면 true.
        /// 1초짜리 놀람 연출은 부르는 쪽이 이벤트로 낸다.
        /// </summary>
        public bool Startle(string itemId, FearAxisSystem axes, SpaceId space)
        {
            if (axes == null || axes.IsLocked) return false;
            InspectionAssignment a = _plan.Find(itemId);
            if (a == null || !_startled.Add(itemId)) return false;

            axes.Apply(a.Item.Axis, Deltas.InspectionRuleViolation, itemId + "(가까이)", space);
            return true;
        }

        /// <summary>
        /// 04:00 정산. 미완료 항목마다 경고 1(수를 돌려준다 — 경고 장부에는 부르는 쪽이 넣는다), 이상이 있던 항목은 그 축 +8.
        /// <paramref name="penalize"/>가 false면(결근) 경고도 델타도 없는 미완료로 끝낸다.
        /// </summary>
        public InspectionSettlement Settle(FearAxisSystem axes, bool penalize)
        {
            int unfinished = 0;
            int anomalies = 0;
            for (int i = 0; i < _plan.Assignments.Count; i++)
            {
                InspectionAssignment a = _plan.Assignments[i];
                if (StateOf(a.Id) != InspectionState.Pending) continue;

                unfinished++;
                if (!a.IsAnomaly) continue;

                anomalies++;
                if (penalize && axes != null)
                {
                    axes.Apply(a.Item.Axis, Deltas.MissedAnomaly, a.Id + "(미완료)", a.Item.Space);
                }
            }

            return new InspectionSettlement(penalize ? unfinished : 0, anomalies);
        }

        /// <inheritdoc/>
        public object CaptureState()
        {
            return new State
            {
                ReliefUsed = (int[])_reliefUsed.Clone(),
                Startled = new List<string>(_startled),
                Reverse = new List<string>(_reverse)
            };
        }

        /// <inheritdoc/>
        public void RestoreState(object state)
        {
            State s = state as State;
            if (s == null) return;

            Array.Copy(s.ReliefUsed, _reliefUsed, Math.Min(s.ReliefUsed.Length, _reliefUsed.Length));
            _startled.Clear();
            _startled.UnionWith(s.Startled);
            _reverse.Clear();
            _reverse.UnionWith(s.Reverse);
        }

        private ReportRejection Check(string itemId, int minute)
        {
            if (_plan.Find(itemId) == null) return ReportRejection.NotInPlan;
            if (StateOf(itemId) != InspectionState.Pending) return ReportRejection.AlreadyReported;
            if (!IsOpen(itemId, minute)) return ReportRejection.NotOpenYet;
            return ReportRejection.None;
        }

        /// <summary>정확 보고 −5, 축마다 밤당 −10까지. 실제로 뺀 양을 돌려준다.</summary>
        private int Relieve(FearAxisSystem axes, FearAxis axis, string source)
        {
            int slot = (int)axis;
            if (slot < 0 || slot >= SensoryAxes) return 0;

            int room = Deltas.ReliefCapPerAxisPerNight - _reliefUsed[slot];
            int amount = Math.Min(Deltas.CorrectReportRelief, Math.Max(0, room));
            if (amount <= 0) return 0;

            int before = axes.GetValue(axis);
            axes.Lower(axis, amount, source);
            int lowered = before - axes.GetValue(axis);
            _reliefUsed[slot] += amount;   // 0에 붙어 덜 빠졌어도 한도는 쓴 것으로 본다 — 정확 보고의 몫은 한 번이다.
            return lowered;
        }

        private static int Raise(FearAxisSystem axes, FearAxis axis, int delta, string source, SpaceId space)
        {
            int before = axes.GetValue(axis);
            axes.Apply(axis, delta, source, space);
            return axes.GetValue(axis) - before;
        }

        private sealed class State
        {
            public int[] ReliefUsed;
            public List<string> Startled;
            public List<string> Reverse;
        }
    }
}

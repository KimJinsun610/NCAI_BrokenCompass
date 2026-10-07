using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace NightDuty
{
    /// <summary>그날 점검표의 한 줄 — 항목, 이상인지, 이상의 강도, 호출 2(02:16)에 열리는지.</summary>
    public sealed class InspectionAssignment
    {
        /// <summary>항목 정의.</summary>
        public readonly InspectionItem Item;

        /// <summary>이상인지. 정답은 이상이면 [이상], 아니면 [정상].</summary>
        public readonly bool IsAnomaly;

        /// <summary>이상의 강도(그 축의 연출 구간, 최소 구간 1). 정상이면 Band0. 연출이 이 값으로 뚜렷함을 고른다.</summary>
        public readonly Band Intensity;

        /// <summary>그날 마지막 점검 공간이라 호출 2(02:16)에 열리는지.</summary>
        public readonly bool IsLate;

        /// <summary>줄을 만든다.</summary>
        public InspectionAssignment(InspectionItem item, bool isAnomaly, Band intensity, bool isLate)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            Item = item;
            IsAnomaly = isAnomaly;
            Intensity = isAnomaly ? intensity : Band.Band0;
            IsLate = isLate;
        }

        /// <summary>항목 ID.</summary>
        public string Id
        {
            get { return Item.Id; }
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return Item.Id + (IsAnomaly ? "[이상 " + (int)Intensity + "]" : "[정상]") + (IsLate ? "(늦게)" : string.Empty);
        }
    }

    /// <summary>
    /// 하룻밤 점검 편성. 밤 시작에 확정되고 <b>재시작해도 바뀌지 않는다</b>(장치 2만 예외 — 수칙 교체는 점검과 무관).
    /// </summary>
    public sealed class InspectionPlan
    {
        private readonly List<InspectionAssignment> _assignments;
        private readonly List<SpaceId> _spaces;
        private readonly ReadOnlyCollection<SpaceId> _spacesView;

        /// <summary>일차.</summary>
        public readonly int Day;

        /// <summary>그날 마지막 점검 공간(호출 2에 열림). 없으면 None.</summary>
        public readonly SpaceId LateSpace;

        /// <summary>호출 1(01:00)이 가리키는 항목 ID. 없으면 빈 문자열.</summary>
        public readonly string Call1ItemId;

        /// <summary>편성을 만든다.</summary>
        public InspectionPlan(int day, IEnumerable<InspectionAssignment> assignments, SpaceId lateSpace,
            string call1ItemId)
        {
            Day = day;
            LateSpace = lateSpace;
            Call1ItemId = call1ItemId ?? string.Empty;
            _assignments = assignments != null ? new List<InspectionAssignment>(assignments) : new List<InspectionAssignment>();
            _spaces = new List<SpaceId>();
            for (int i = 0; i < _assignments.Count; i++)
            {
                if (!_spaces.Contains(_assignments[i].Item.Space))
                {
                    _spaces.Add(_assignments[i].Item.Space);
                }
            }

            _spacesView = _spaces.AsReadOnly();
        }

        /// <summary>빈 편성(점검을 쓰지 않는 밤·테스트).</summary>
        public static InspectionPlan Empty(int day)
        {
            return new InspectionPlan(day, null, SpaceId.None, string.Empty);
        }

        /// <summary>점검표 줄(편성 순).</summary>
        public IReadOnlyList<InspectionAssignment> Assignments
        {
            get { return _assignments; }
        }

        /// <summary>점검 공간(편성에 처음 나온 순).</summary>
        public ReadOnlyCollection<SpaceId> Spaces
        {
            get { return _spacesView; }
        }

        /// <summary>점검 항목 수.</summary>
        public int Count
        {
            get { return _assignments.Count; }
        }

        /// <summary>이상 수.</summary>
        public int AnomalyCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _assignments.Count; i++)
                {
                    if (_assignments[i].IsAnomaly) n++;
                }

                return n;
            }
        }

        /// <summary>항목 ID로 찾는다. 편성에 없으면 null.</summary>
        public InspectionAssignment Find(string itemId)
        {
            for (int i = 0; i < _assignments.Count; i++)
            {
                if (_assignments[i].Item.Id == itemId)
                {
                    return _assignments[i];
                }
            }

            return null;
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(Day).Append("일차 점검 ").Append(Count).Append('(').Append(AnomalyCount).Append(") ");
            for (int i = 0; i < _assignments.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(_assignments[i]);
            }

            sb.Append(" · 늦게 ").Append(LateSpace).Append(" · 호출1 ").Append(Call1ItemId);
            return sb.ToString();
        }
    }

    /// <summary>일차별 점검 수(최종 기획서 「이상 배정」).</summary>
    public static class InspectionQuota
    {
        /// <summary>점검 공간 상한(경비실 포함).</summary>
        public const int MaxSpaces = 4;

        private static readonly int[] ItemsByDay = { 5, 5, 6, 6, 7 };
        private static readonly int[] AnomaliesByDay = { 2, 2, 3, 3, 3 };

        /// <summary>그날 점검 항목 수.</summary>
        public static int Items(int day)
        {
            return ItemsByDay[Index(day)];
        }

        /// <summary>그날 이상 수.</summary>
        public static int Anomalies(int day)
        {
            return AnomaliesByDay[Index(day)];
        }

        private static int Index(int day)
        {
            return Math.Max(0, Math.Min(ItemsByDay.Length - 1, day - 1));
        }
    }

    /// <summary>
    /// 이상 배정기(최종 기획서 「이상 배정」). 회차 동안 살아 있으며 공간을 처음 점검한 날을 기억한다.
    /// <list type="bullet">
    /// <item>점검 수(그중 이상): 5(2) · 5(2) · 6(3) · 6(3) · 7(3). 점검 공간은 경비실 포함 4곳 이하이고, 2일차부터는 좌·우 동에 각각 하나 이상.</item>
    /// <item>1일차는 튜토리얼 고정: 복도·교실·과학실 + 경비실, 이상은 S-2 현미경 불(빛, 과학실이라 호출 2에 열림)과 C-1 화분(옮김), 첫 점검 K-1은 정상. 복도는 정상 항목 하나.</item>
    /// <item>이상의 축은 그 축의 <b>연출 구간</b>이 1 이상일 때만 고르고, 구간이 높을수록 가중치가 크다(가중치 = 구간 번호).</item>
    /// <item>공간을 처음 점검하는 날에는 그 공간에 이상을 두지 않는다(1일차 튜토리얼 제외).</item>
    /// <item>그날 마지막 점검 공간 하나는 호출 2(02:16)에 열린다(경비실 제외, 좌측 동 우선). 호출 1은 먼저 열린 항목 하나를 가리킨다.</item>
    /// </list>
    /// 조건을 다 만족하는 편성이 없으면(예: 이상 후보 부족) 이상 수를 줄이고 <see cref="LastReport"/>에 남긴다.
    /// </summary>
    public sealed class AnomalyAssigner
    {
        private const int Attempts = 40;

        private static readonly SpaceId[] Day1Spaces = { SpaceId.Corridor, SpaceId.Classroom, SpaceId.ScienceRoom, SpaceId.SecurityRoom };

        /// <summary>1일차 마지막 점검 공간(호출 2). 50차: 교실 → 과학실. 51차: 과학실 → 복도 정상 1항목(민: 「이후 2개가 너무 늦다」 — 과학실은 01:00 전후에).</summary>
        public const SpaceId Day1LateSpace = SpaceId.Corridor;

        /// <summary>2일차부터 늦은 공간(호출 2)에 묶는 항목 수 상한. 나머지는 먼저 열린다.</summary>
        public const int LateItemsMax = 2;

        private readonly Random _rng;
        private readonly HashSet<SpaceId> _seen = new HashSet<SpaceId>();

        /// <summary>배정기를 만든다. 테스트는 시드를 고정한 난수를 넘긴다.</summary>
        public AnomalyAssigner(Random rng = null)
        {
            _rng = rng ?? new Random();
        }

        /// <summary>지금까지 점검한 적이 있는 공간.</summary>
        public IReadOnlyCollection<SpaceId> SeenSpaces
        {
            get { return _seen; }
        }

        /// <summary>마지막 편성의 설명(줄인 것이 있으면 이유 포함).</summary>
        public string LastReport { get; private set; }

        /// <summary>회차를 새로 시작한다.</summary>
        public void Reset()
        {
            _seen.Clear();
            LastReport = null;
        }

        /// <summary>점검한 적이 있는 공간을 직접 적는다(저장 복원·디버그 시작).</summary>
        public void MarkSeen(IEnumerable<SpaceId> spaces)
        {
            if (spaces == null) return;
            foreach (SpaceId s in spaces)
            {
                _seen.Add(SpaceIds.Canonical(s));
            }
        }

        /// <summary>
        /// 그날 편성을 만든다. <paramref name="shown"/>은 <b>연출 구간</b>을 돌려주는 읽기 창구다(<c>BandResolver.Shown</c>).
        /// 만든 편성의 공간은 「점검한 적이 있는 공간」에 더해진다.
        /// </summary>
        public InspectionPlan Build(int day, IFearAxisReader shown)
        {
            if (shown == null) throw new ArgumentNullException(nameof(shown));
            day = Math.Max(1, day);

            // 2일차 이후인데 기록이 없으면(디버그로 중간 일차부터 시작 등) 1일차 공간은 본 것으로 친다.
            if (day > 1 && _seen.Count == 0)
            {
                MarkSeen(Day1Spaces);
            }

            InspectionPlan plan = day == 1 ? BuildTutorial(shown) : BuildRegular(day, shown);
            for (int i = 0; i < plan.Spaces.Count; i++)
            {
                _seen.Add(plan.Spaces[i]);
            }

            if (LastReport == null) LastReport = plan.ToString();
            return plan;
        }

        // ── 1일차 ──────────────────────────────────────────────

        private InspectionPlan BuildTutorial(IFearAxisReader shown)
        {
            LastReport = null;
            List<InspectionAssignment> list = new List<InspectionAssignment>();
            InspectionItem first = InspectionCatalog.Find(InspectionCatalog.FirstInspection);
            InspectionItem move = InspectionCatalog.Find(InspectionCatalog.TutorialMove);
            InspectionItem light = InspectionCatalog.Find(InspectionCatalog.TutorialLight);

            // 51차: 1일차 호출 2(02:16)는 복도 정상 1항목(경비실 앞 재방문)만 — 과학실·교실은 앞쪽에 열려 1시대에 다 나온다.
            list.Add(new InspectionAssignment(first, false, Band.Band0, false));
            list.Add(new InspectionAssignment(move, true, Intensity(shown, move.Axis), move.Space == Day1LateSpace));
            list.Add(new InspectionAssignment(light, true, Intensity(shown, light.Axis), light.Space == Day1LateSpace));

            // 복도는 정상 항목 하나(49차: 복도 사물함 H-4를 뺀 뒤에도 1일차 공간 셋을 다 돈다).
            InspectionItem hall = Pick(InspectionCatalog.InSpace(SpaceId.Corridor));
            list.Add(new InspectionAssignment(hall, false, Band.Band0, hall.Space == Day1LateSpace));

            List<InspectionItem> rest = new List<InspectionItem>();
            SpaceId[] tutorialSpaces = { SpaceId.Corridor, SpaceId.Classroom, SpaceId.ScienceRoom };
            for (int s = 0; s < tutorialSpaces.Length; s++)
            {
                List<InspectionItem> inSpace = InspectionCatalog.InSpace(tutorialSpaces[s]);
                for (int i = 0; i < inSpace.Count; i++)
                {
                    if (!Contains(list, inSpace[i].Id)) rest.Add(inSpace[i]);
                }
            }

            rest.RemoveAll(x => x.Space == Day1LateSpace);   // 늦은 항목은 하나만
            InspectionItem extra = Pick(rest);
            list.Add(new InspectionAssignment(extra, false, Band.Band0, false));

            string call1 = PickCall1(list, InspectionCatalog.FirstInspection);
            return new InspectionPlan(1, list, Day1LateSpace, call1);
        }

        // ── 2일차부터 ──────────────────────────────────────────

        private InspectionPlan BuildRegular(int day, IFearAxisReader shown)
        {
            LastReport = null;
            int total = InspectionQuota.Items(day);
            int wanted = InspectionQuota.Anomalies(day);

            List<SpaceId> spaces = null;
            List<InspectionItem> eligible = null;
            for (int attempt = 0; attempt < Attempts; attempt++)
            {
                spaces = PickSpaces();
                eligible = AnomalyCandidates(spaces, shown);
                if (eligible.Count >= wanted) break;
            }

            int anomalies = Math.Min(wanted, eligible.Count);
            StringBuilder note = new StringBuilder();
            if (anomalies < wanted)
            {
                note.Append("이상 후보가 ").Append(eligible.Count).Append("개뿐이라 이상을 ")
                    .Append(wanted).Append(" → ").Append(anomalies).Append("개로 줄였습니다. ");
            }

            List<InspectionAssignment> list = new List<InspectionAssignment>();

            // 1) 이상 — 연출 구간 가중치로 뽑는다.
            for (int n = 0; n < anomalies; n++)
            {
                InspectionItem item = PickWeighted(eligible, shown);
                eligible.Remove(item);
                list.Add(new InspectionAssignment(item, true, Intensity(shown, item.Axis), false));
            }

            // 2) 고른 공간마다 적어도 한 항목.
            for (int s = 0; s < spaces.Count; s++)
            {
                if (HasSpace(list, spaces[s])) continue;
                InspectionItem item = Pick(InspectionCatalog.InSpace(spaces[s]));
                list.Add(new InspectionAssignment(item, false, Band.Band0, false));
            }

            // 3) 나머지는 고른 공간의 남은 항목에서 정상으로.
            List<InspectionItem> rest = new List<InspectionItem>();
            for (int s = 0; s < spaces.Count; s++)
            {
                List<InspectionItem> inSpace = InspectionCatalog.InSpace(spaces[s]);
                for (int i = 0; i < inSpace.Count; i++)
                {
                    if (!Contains(list, inSpace[i].Id)) rest.Add(inSpace[i]);
                }
            }

            while (list.Count < total && rest.Count > 0)
            {
                InspectionItem item = Pick(rest);
                rest.Remove(item);
                list.Add(new InspectionAssignment(item, false, Band.Band0, false));
            }

            if (list.Count < total)
            {
                note.Append("고른 공간의 항목이 모자라 점검을 ").Append(list.Count).Append("개로 줄였습니다. ");
            }

            // 4) 그날 마지막 점검 공간(호출 2) — 경비실 제외, 좌측 동 우선.
            SpaceId late = PickLateSpace(spaces);
            List<InspectionAssignment> final = new List<InspectionAssignment>(list.Count);
            int lateCount = 0;
            for (int i = 0; i < list.Count; i++)
            {
                InspectionAssignment a = list[i];
                bool isLate = a.Item.Space == late && lateCount < LateItemsMax;   // 51차: 늦은 공간은 2항목까지
                if (isLate) lateCount++;
                final.Add(new InspectionAssignment(a.Item, a.IsAnomaly, a.Intensity, isLate));
            }

            string call1 = PickCall1(final, null);
            InspectionPlan plan = new InspectionPlan(day, final, late, call1);
            LastReport = note.Length > 0 ? note + plan.ToString() : plan.ToString();
            return plan;
        }

        /// <summary>좌·우 동 하나씩 + 나머지 둘을 무작위로(경비실 포함 4곳).</summary>
        private List<SpaceId> PickSpaces()
        {
            List<SpaceId> left = new List<SpaceId>();
            List<SpaceId> right = new List<SpaceId>();
            List<SpaceId> all = new List<SpaceId>(SpaceIds.Final);
            for (int i = 0; i < all.Count; i++)
            {
                Wing wing = SpaceIds.WingOf(all[i]);
                if (wing == Wing.Left) left.Add(all[i]);
                else if (wing == Wing.Right) right.Add(all[i]);
            }

            List<SpaceId> chosen = new List<SpaceId>();
            chosen.Add(Pick(left));
            chosen.Add(Pick(right));

            List<SpaceId> rest = new List<SpaceId>();
            for (int i = 0; i < all.Count; i++)
            {
                if (!chosen.Contains(all[i])) rest.Add(all[i]);
            }

            while (chosen.Count < InspectionQuota.MaxSpaces && rest.Count > 0)
            {
                SpaceId s = Pick(rest);
                rest.Remove(s);
                chosen.Add(s);
            }

            chosen.Sort((a, b) => Array.IndexOf(SpaceIds.Final, a).CompareTo(Array.IndexOf(SpaceIds.Final, b)));
            return chosen;
        }

        /// <summary>이상을 둘 수 있는 항목: 점검한 적 있는 공간 + 그 축의 연출 구간 1 이상.</summary>
        private List<InspectionItem> AnomalyCandidates(List<SpaceId> spaces, IFearAxisReader shown)
        {
            List<InspectionItem> list = new List<InspectionItem>();
            for (int s = 0; s < spaces.Count; s++)
            {
                if (!_seen.Contains(spaces[s])) continue;
                List<InspectionItem> inSpace = InspectionCatalog.InSpace(spaces[s]);
                for (int i = 0; i < inSpace.Count; i++)
                {
                    if (shown.GetBand(inSpace[i].Axis) >= Band.Band1) list.Add(inSpace[i]);
                }
            }

            return list;
        }

        private InspectionItem PickWeighted(List<InspectionItem> items, IFearAxisReader shown)
        {
            int sum = 0;
            for (int i = 0; i < items.Count; i++)
            {
                sum += Weight(shown, items[i].Axis);
            }

            int roll = _rng.Next(sum);
            for (int i = 0; i < items.Count; i++)
            {
                roll -= Weight(shown, items[i].Axis);
                if (roll < 0) return items[i];
            }

            return items[items.Count - 1];
        }

        private static int Weight(IFearAxisReader shown, FearAxis axis)
        {
            return Math.Max(1, (int)shown.GetBand(axis));
        }

        private static Band Intensity(IFearAxisReader shown, FearAxis axis)
        {
            Band band = shown.GetBand(axis);
            return band < Band.Band1 ? Band.Band1 : band;
        }

        private SpaceId PickLateSpace(List<SpaceId> spaces)
        {
            List<SpaceId> left = new List<SpaceId>();
            List<SpaceId> any = new List<SpaceId>();
            for (int i = 0; i < spaces.Count; i++)
            {
                if (spaces[i] == SpaceId.SecurityRoom) continue;
                any.Add(spaces[i]);
                if (SpaceIds.WingOf(spaces[i]) == Wing.Left) left.Add(spaces[i]);
            }

            if (left.Count > 0) return Pick(left);
            return any.Count > 0 ? Pick(any) : SpaceId.None;
        }

        private string PickCall1(List<InspectionAssignment> list, string exclude)
        {
            List<string> ids = new List<string>();
            for (int i = 0; i < list.Count; i++)
            {
                if (!list[i].IsLate && list[i].Id != exclude) ids.Add(list[i].Id);
            }

            return ids.Count > 0 ? Pick(ids) : string.Empty;
        }

        private T Pick<T>(List<T> list)
        {
            return list[_rng.Next(list.Count)];
        }

        private static bool Contains(List<InspectionAssignment> list, string id)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Id == id) return true;
            }

            return false;
        }

        private static bool HasSpace(List<InspectionAssignment> list, SpaceId space)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Item.Space == space) return true;
            }

            return false;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Text;

namespace NightDuty
{
    /// <summary>조우 슬롯(최종 기획서 「밤 시계」). A 01:00~01:52, B 02:16~03:08(그날의 메인), C 03:08~03:30(보너스).</summary>
    public enum EncounterSlot
    {
        /// <summary>슬롯 A.</summary>
        A = 0,

        /// <summary>슬롯 B — 그날의 메인.</summary>
        B = 1,

        /// <summary>보너스 슬롯 C. 재시작 k≥2면 끈다(장치 1).</summary>
        C = 2
    }

    /// <summary>그날 슬롯에 편성된 조우 하나.</summary>
    public sealed class SlotEncounter
    {
        /// <summary>슬롯.</summary>
        public readonly EncounterSlot Slot;

        /// <summary>조우.</summary>
        public readonly EncounterDef Encounter;

        /// <summary>지난 밤에 예약돼 강제로 들어왔는지(S3 위반 → 모형 급습, 노란 얼굴에서 빛을 뗌 → 정장 남자).</summary>
        public readonly bool Reserved;

        /// <summary>만든다.</summary>
        public SlotEncounter(EncounterSlot slot, EncounterDef encounter, bool reserved)
        {
            Slot = slot;
            Encounter = encounter;
            Reserved = reserved;
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return Slot + ":" + Encounter.Id + (Reserved ? "(예약)" : string.Empty);
        }
    }

    /// <summary>
    /// 하룻밤 편성 — 조우 슬롯과 수칙 덱. 밤 시작에 확정되고 재시작해도 바뀌지 않는다(장치 2의 수칙 교체만 예외, 런타임 몫).
    /// 덱 순서: 공간 수칙(복도·교실·과학실·화장실·도서관) → 경비실 → G1.
    /// </summary>
    public sealed class NightProgram
    {
        private readonly List<SlotEncounter> _slots;
        private readonly List<RuleDef> _deck;
        private readonly List<EncounterDef> _extras;

        /// <summary>일차.</summary>
        public readonly int Day;

        /// <summary>편성 설명(줄인 것·강제한 것 포함).</summary>
        public readonly string Report;

        /// <summary>만든다.</summary>
        public NightProgram(int day, IEnumerable<SlotEncounter> slots, IEnumerable<RuleDef> deck, string report, IEnumerable<EncounterDef> extras = null)
        {
            Day = day;
            _slots = slots != null ? new List<SlotEncounter>(slots) : new List<SlotEncounter>();
            _extras = extras != null ? new List<EncounterDef>(extras) : new List<EncounterDef>();
            _deck = deck != null ? new List<RuleDef>(deck) : new List<RuleDef>();
            Report = report ?? string.Empty;
        }

        /// <summary>빈 편성.</summary>
        public static NightProgram Empty(int day)
        {
            return new NightProgram(day, null, null, string.Empty);
        }

        /// <summary>슬롯 조우(A → B → C).</summary>
        public IReadOnlyList<SlotEncounter> Slots
        {
            get { return _slots; }
        }

        /// <summary>
        /// 59차 — 슬롯 밖에서 거는 조우(겹침 조우). 지금은 시체 낙하(<see cref="ProgramCatalog.CeilingLegs"/>) 하나: 회차에 한 번, 2·3일차 중 하루(<see cref="ProgramDirector.CorpseDay"/>)
        /// 사다리 점검(C-3) 중에 떨어진다. 슬롯·예산·같은 몹 제외를 보지 않고 다른 조우(앉은 소년 등)와 겹쳐도 된다.
        /// </summary>
        public IReadOnlyList<EncounterDef> Extras
        {
            get { return _extras; }
        }

        /// <summary>수칙 덱(태블릿 표시 순).</summary>
        public IReadOnlyList<RuleDef> Deck
        {
            get { return _deck; }
        }

        /// <summary>그 수칙이 덱에 있는지.</summary>
        public bool Has(string ruleId)
        {
            for (int i = 0; i < _deck.Count; i++)
            {
                if (_deck[i].Id == ruleId) return true;
            }

            return false;
        }

        /// <summary>그 조우가 편성됐는지.</summary>
        public bool HasEncounter(string encounterId)
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i].Encounter.Id == encounterId) return true;
            }

            for (int i = 0; i < _extras.Count; i++)
            {
                if (_extras[i].Id == encounterId) return true;
            }

            return false;
        }

        /// <summary>그 슬롯의 조우. 없으면 null.</summary>
        public SlotEncounter At(EncounterSlot slot)
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i].Slot == slot) return _slots[i];
            }

            return null;
        }

        /// <summary>재시작 k에서 그 슬롯을 걸어도 되는지 — 보너스 슬롯 C는 k≥2면 끈다(장치 1).</summary>
        public static bool SlotActive(EncounterSlot slot, int restarts)
        {
            return slot != EncounterSlot.C || RestartPolicy.SlotCAllowed(restarts);
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(Day).Append("일차 조우 [");
            for (int i = 0; i < _slots.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(_slots[i]);
            }

            for (int i = 0; i < _extras.Count; i++) sb.Append(" + 겹침:").Append(_extras[i].Id);
            sb.Append("] 덱 [");
            for (int i = 0; i < _deck.Count; i++)
            {
                if (i > 0) sb.Append(' ');
                sb.Append(_deck[i].Id);
            }

            sb.Append(']');
            return sb.ToString();
        }
    }

    /// <summary>편성 요청 — 편성기가 밤 시작에 받는 재료.</summary>
    public sealed class ProgramRequest
    {
        /// <summary>일차(1부터).</summary>
        public int Day = 1;

        /// <summary>연출 구간(조우 발동 조건·가중치).</summary>
        public IFearAxisReader Shown;

        /// <summary>생존 수치(보너스 슬롯 C — 가장 높은 감각 축이 49 이하일 때만 켠다). 없으면 C를 켜지 않는다.</summary>
        public IFearAxisReader Survival;

        /// <summary>그날 점검 편성(소년 착석 = 교실 점검, 노란 얼굴 = 도서관 점검 2개). 없으면 그 조건을 만족하지 않는 것으로 본다.</summary>
        public InspectionPlan Inspections;

        /// <summary>강제 편성할 수칙(회피 불가 역설의 쌍 — 10단계가 넣는다).</summary>
        public IEnumerable<string> ForcedRules;

        /// <summary>지난 밤에 예약된 조우(<see cref="ProgramDirector.Reserve"/>).</summary>
        public IEnumerable<string> ReservedEncounters;
    }

    /// <summary>
    /// 밤 편성기(최종 기획서 「수칙과 덱 — 배정 순서」). 조우를 먼저 뽑고 대응 수칙을 고정해 덱과 조우가 서로를 정하는 순환을 끊는다.
    /// 규칙이 부딪히면 앞 번호가 이긴다.
    /// <list type="number">
    /// <item>1일차 고정 덱: H2 · C4 · S1 · G1(조우 없음 — 2026-10-01 민 결정 「몹은 2일차부터」). S3는 2일차부터, S-1 점검이 있는 날에는 채우지 않는다.</item>
    /// <item>강제 수칙: 회피 불가 역설의 쌍(요청), 2일차 첫 역설 C2.</item>
    /// <item>지난 밤 예약 조우: S3 위반 → 모형 급습(S5, 3일차부터), 노란 얼굴에서 빛을 뗌 → 정장 남자(L5).</item>
    /// <item>슬롯 조우: 발동 조건·대응 수칙 배정 가능·놀람 예산을 통과한 후보 중 점수(주축 구간×2 · 회차에 아직 안 봄 +3 · 교차 +2 ·
    /// 직전 슬롯과 같은 축 −2, 직전 슬롯과 같은 몹 제외)가 가장 높은 것. 뽑은 조우의 대응 수칙이 그 공간의 수칙이 된다
    /// (소년 머리 박기만 교실 C2·C3 두 장).</item>
    /// <item>(런타임) 재시작 k≥3이면 붙잡힌 축의 공간 수칙 1장을 다른 축으로 — 편성기 밖.</item>
    /// <item>손전등 수칙 하루 3장, 역보고 T4 회차 2번.</item>
    /// <item>남은 칸: 혼자 서는 수칙에서, 가장 높은 감각 축(연출 구간) ×2, 가장 낮은 축 최소 1장.</item>
    /// </list>
    /// 경비실은 2일차부터 하루 1장(CCTV 사람이 편성되면 K1, 5일차는 K4, 아니면 K2·K3), 공통 G1은 매일(G2는 2026-10-04 폐기).
    /// </summary>
    public sealed class ProgramDirector
    {
        /// <summary>하루 수칙 수(57차, 민: 「수칙은 5~6개로 고정하는 게 좋을 듯」 — 전에는 1일차 4장, 2일차부터 7~8장).</summary>
        public const int RulesPerDay = 6;

        /// <summary>1일차 수칙 후보(57차, 민: 「1~2일차 수칙이 너무 풀이 좁다」) — 조우에 묶이지 않은 수칙 중 1일차 점검 공간(복도·교실·과학실·경비실)의 것. S1·G1은 고정.</summary>
        public static readonly string[] Day1Pool = { "H2", "C4", "S2", "K2", "K3" };   // 60차: C1(판서) 폐기

        private readonly Random _rng;
        private readonly HashSet<string> _seen = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _reserved = new HashSet<string>(StringComparer.Ordinal);
        private int _reverseAssigned;
        private InspectionPlan _planForFill;
        private int _corpseDay;

        private static bool PlanHas(InspectionPlan plan, string itemId)
        {
            return plan != null && plan.Find(itemId) != null;
        }

        /// <summary>편성기를 만든다. 테스트는 시드를 고정한 난수를 넘긴다.</summary>
        public ProgramDirector(Random rng = null)
        {
            _rng = rng ?? new Random();
            _corpseDay = RollCorpseDay();
            ResetRushChain();
        }

        // ── 70차: 복도 끝에 선 자 → 다음 날 모형 급습 ─────────────

        /// <summary>
        /// 70차(민: 「복도 끝에 서 있는 자는 인체 모형으로, 모형 급습 전날에 배치」) — 모형 급습이 나오는 날(기본 5일차).
        /// 그 전날(<see cref="HallFigureDay"/>)에는 복도 끝에 선 자가 같은 자리에 서 있다. 둘은 슬롯 뽑기에서 빠지고 이 날짜로만 들어간다.
        /// </summary>
        public const int DefaultRushDay = 5;

        private int _rushDay;
        private int _hallDay;

        /// <summary>그 회차에 모형 급습이 나오는 날.</summary>
        public int RushDay
        {
            get { return _rushDay; }
        }

        /// <summary>그 회차에 복도 끝에 선 자가 서는 날(모형 급습 전날).</summary>
        public int HallFigureDay
        {
            get { return _hallDay; }
        }

        private void ResetRushChain()
        {
            _rushDay = DefaultRushDay;
            _hallDay = DefaultRushDay - 1;
        }

        /// <summary>
        /// 모형 급습 예약(S3 위반)은 사슬을 앞당긴다 — 아직 복도 끝에 선 자가 나오지 않았으면 오늘 세우고 급습은 내일.
        /// 이미 섰으면(오늘이 급습 날이거나 그 뒤) 그대로 둔다. 예약 목록에서는 뺀다.
        /// </summary>
        private void ApplyRushReservation(int day, List<string> reserved)
        {
            if (!reserved.Remove(ProgramCatalog.ModelRush)) return;
            while (reserved.Remove(ProgramCatalog.ModelRush)) { }
            if (day < _hallDay)
            {
                _hallDay = day;
                _rushDay = day + 1;
            }
        }

        /// <summary>
        /// 시체 낙하(겹침 조우)가 나오는 그 회차의 하루 — 2일차 또는 3일차 중 하나, 회차에 딱 한 번(59차 민: 「무서운 연출은 똑같은 게 반복되면 재미없다 — 2~3일차에 한 번만」).
        /// </summary>
        public int CorpseDay
        {
            get { return _corpseDay; }
        }

        /// <summary>시체 낙하가 나올 수 있는 날(포함).</summary>
        public const int CorpseDayFrom = 2;

        /// <summary>시체 낙하가 나올 수 있는 마지막 날(포함).</summary>
        public const int CorpseDayTo = 3;

        private int RollCorpseDay()
        {
            return CorpseDayFrom + _rng.Next(CorpseDayTo - CorpseDayFrom + 1);
        }

        /// <summary>회차에 이미 편성한 조우.</summary>
        public IReadOnlyCollection<string> SeenEncounters
        {
            get { return _seen; }
        }

        /// <summary>다음 밤을 위해 예약된 조우.</summary>
        public IReadOnlyCollection<string> Reserved
        {
            get { return _reserved; }
        }

        /// <summary>회차에서 역보고 T4를 배정한 수.</summary>
        public int ReverseReportAssigned
        {
            get { return _reverseAssigned; }
        }

        /// <summary>회차를 새로 시작한다.</summary>
        public void Reset()
        {
            _seen.Clear();
            _reserved.Clear();
            _reverseAssigned = 0;
            _corpseDay = RollCorpseDay();
            ResetRushChain();
        }

        /// <summary>
        /// 다음 밤 조우를 예약한다 — S3 위반이면 <see cref="ProgramCatalog.ModelRush"/>, 노란 얼굴에서 빛을 떼면 <see cref="ProgramCatalog.SuitMan"/>.
        /// 다음 <see cref="Build"/>가 쓰고 비운다.
        /// </summary>
        public void Reserve(string encounterId)
        {
            if (ProgramCatalog.Encounter(encounterId) != null) _reserved.Add(encounterId);
        }

        /// <summary>그날 편성을 만든다. 요청에 예약 조우를 따로 넣지 않으면 <see cref="Reserve"/>로 쌓인 것을 쓴다.</summary>
        public NightProgram Build(ProgramRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (request.Shown == null) throw new ArgumentNullException("request.Shown");

            int day = Math.Max(1, request.Day);
            List<string> reserved = new List<string>();
            if (request.ReservedEncounters != null) reserved.AddRange(request.ReservedEncounters);
            else reserved.AddRange(_reserved);
            _reserved.Clear();

            _planForFill = request.Inspections;
            NightProgram program = day == 1 ? BuildDay1(request) : BuildRegular(day, request, reserved);

            for (int i = 0; i < program.Slots.Count; i++) _seen.Add(program.Slots[i].Encounter.Id);
            if (program.Has(ProgramCatalog.ReverseReportRule)) _reverseAssigned++;
            return program;
        }

        // ── 1일차 ──────────────────────────────────────────────

        private NightProgram BuildDay1(ProgramRequest request)
        {
            // 2026-10-01 사용자 결정: 몹은 2일차부터 — 1일차에는 조우가 없다(긴장의 기승전결).
            // 그래서 조우에 묶인 수칙(H1 사람 나무·C3 소년)도 쓰지 않고 혼자 서는 수칙만 둔다.
            // 57차: 고정 4장(H2·C4·S1·G1) → S1·G1 고정 + 후보(Day1Pool)에서 넷, 모두 여섯 장. 한 공간 두 장까지, 경비실 한 장, 손전등 한도.
            List<RuleDef> picked = new List<RuleDef> { ProgramCatalog.Rule("S1") };   // 51차: S3(모형을 비추지 마)는 2일차부터 — 1일차는 모형 위치를 익히는 날
            List<string> pool = new List<string>(Day1Pool);
            while (picked.Count < RulesPerDay - 1 && pool.Count > 0)
            {
                string id = pool[_rng.Next(pool.Count)];
                pool.Remove(id);
                RuleDef r = ProgramCatalog.Rule(id);
                if (r == null) continue;
                int sameSpace = 0, flash = 0;
                for (int i = 0; i < picked.Count; i++)
                {
                    if (picked[i].Space == r.Space) sameSpace++;
                    if (picked[i].UsesFlashlight) flash++;
                }

                if (sameSpace >= (r.Space == SpaceId.SecurityRoom ? 1 : 2)) continue;
                if (r.UsesFlashlight && flash >= ProgramCatalog.FlashlightPerDay) continue;
                picked.Add(r);
            }

            List<RuleDef> deck = new List<RuleDef>();
            for (int s = 0; s < ProgramCatalog.RuleSpaces.Length; s++)
            {
                for (int i = 0; i < picked.Count; i++) if (picked[i].Space == ProgramCatalog.RuleSpaces[s]) deck.Add(picked[i]);
            }

            for (int i = 0; i < picked.Count; i++) if (picked[i].Space == SpaceId.SecurityRoom) deck.Add(picked[i]);
            deck.Add(ProgramCatalog.Rule("G1"));
            return new NightProgram(1, new List<SlotEncounter>(), deck, "1일차 덱(조우 없음)");
        }

        // ── 2일차부터 ──────────────────────────────────────────

        private sealed class Draft
        {
            public readonly Dictionary<SpaceId, List<RuleDef>> Spaces = new Dictionary<SpaceId, List<RuleDef>>();
            public readonly HashSet<string> Locked = new HashSet<string>(StringComparer.Ordinal);   // 강제·조우 수칙(바꾸지 않음)
            public readonly List<SlotEncounter> Slots = new List<SlotEncounter>();
            public readonly StringBuilder Note = new StringBuilder();

            public List<RuleDef> In(SpaceId s)
            {
                List<RuleDef> list;
                if (!Spaces.TryGetValue(s, out list))
                {
                    list = new List<RuleDef>();
                    Spaces[s] = list;
                }

                return list;
            }

            public bool Has(string id)
            {
                foreach (KeyValuePair<SpaceId, List<RuleDef>> kv in Spaces)
                {
                    for (int i = 0; i < kv.Value.Count; i++)
                    {
                        if (kv.Value[i].Id == id) return true;
                    }
                }

                return false;
            }

            public int Flashlights()
            {
                int n = 0;
                foreach (KeyValuePair<SpaceId, List<RuleDef>> kv in Spaces)
                {
                    for (int i = 0; i < kv.Value.Count; i++)
                    {
                        if (kv.Value[i].UsesFlashlight) n++;
                    }
                }

                return n;
            }
        }

        private NightProgram BuildRegular(int day, ProgramRequest request, List<string> reserved)
        {
            Draft d = new Draft();
            IFearAxisReader shown = request.Shown;

            // 5일차 경비실은 K4 고정.
            if (day >= 5) Place(d, ProgramCatalog.Rule(ProgramCatalog.FinaleRule), true);

            // 70차: 복도 끝에 선 자(급습 전날) · 모형 급습 — 날짜로만 들어간다(예약·슬롯 뽑기보다 먼저 자리를 잡는다).
            ApplyRushReservation(day, reserved);
            if (day == _hallDay) PlaceChain(d, ProgramCatalog.HallEndFigure);
            if (day == _rushDay) PlaceChain(d, ProgramCatalog.ModelRush);

            // 2) 강제 수칙.
            List<string> forced = new List<string>();
            if (request.ForcedRules != null) forced.AddRange(request.ForcedRules);
            if (day == 2) forced.Add(ProgramCatalog.FirstParadoxRule);
            for (int i = 0; i < forced.Count; i++)
            {
                RuleDef r = ProgramCatalog.Rule(forced[i]);
                if (r == null) continue;

                // 조우에 묶인 수칙은 혼자 들어가면 방아쇠가 오지 않는다 — 그 조우를 함께 건다(2일차 C2 → 천장 다리).
                if (!r.IsStandalone)
                {
                    if (d.Has(r.Id)) continue;   // 70차: 이미 조우가 그 수칙을 들여놓았다(S5 = 복도 끝·급습 날)
                    EncounterDef bound = ProgramCatalog.Encounter(r.BoundEncounter);
                    if (bound != null && IsChained(bound.Id))
                    {
                        d.Note.Append(r.Id).Append(" 강제 편성 실패(복도 끝·급습은 정해진 날에만). ");
                        continue;
                    }

                    if (bound != null && !Chosen(d, bound.Id) && CanPlaceEncounter(d, bound))
                    {
                        PlaceEncounter(d, bound, true);
                        continue;
                    }

                    d.Note.Append(r.Id).Append(" 강제 편성 실패(묶인 조우를 걸 수 없음). ");
                    continue;
                }

                if (!CanPlace(d, r)) { d.Note.Append(r.Id).Append(" 강제 편성 실패(공간 충돌). "); continue; }
                Place(d, r, true);
            }

            // 3) 예약 조우.
            for (int i = 0; i < reserved.Count; i++)
            {
                EncounterDef e = ProgramCatalog.Encounter(reserved[i]);
                if (e == null) continue;
                if (IsChained(e.Id)) continue;   // 70차: 날짜 사슬로만
                if (!CanPlaceEncounter(d, e)) { d.Note.Append(e.Id).Append(" 예약 편성 실패(공간 충돌). "); continue; }
                PlaceEncounter(d, e, true);
            }

            // 4) 슬롯 조우.
            int slotCount = day == 2 ? 2 : (SlotCAtStart(request) ? 3 : 2);
            while (d.Slots.Count < slotCount)
            {
                EncounterDef pick = PickEncounter(d, day, request);
                if (pick == null) { d.Note.Append("걸 수 있는 조우가 없어 슬롯 ").Append(d.Slots.Count).Append("개. "); break; }
                PlaceEncounter(d, pick, false);
            }

            // 경비실.
            if (d.In(SpaceId.SecurityRoom).Count == 0)
            {
                Place(d, ProgramCatalog.Rule(_rng.Next(2) == 0 ? "K2" : "K3"), false);
            }

            // 7) 남은 칸.
            FearAxis highest = HighestShown(shown);
            for (int s = 0; s < ProgramCatalog.RuleSpaces.Length; s++)
            {
                SpaceId space = ProgramCatalog.RuleSpaces[s];
                if (d.In(space).Count > 0) continue;
                RuleDef r = PickFiller(d, space, highest, null);
                if (r != null) Place(d, r, false);
                else d.Note.Append(space).Append(" 수칙을 채우지 못했습니다. ");
            }

            EnsureLowestAxis(d, shown);

            List<RuleDef> deck = new List<RuleDef>();
            for (int s = 0; s < ProgramCatalog.RuleSpaces.Length; s++) deck.AddRange(d.In(ProgramCatalog.RuleSpaces[s]));
            deck.AddRange(d.In(SpaceId.SecurityRoom));
            deck.Add(ProgramCatalog.Rule("G1"));
            TrimDeck(d, deck, LowestShown(shown));

            d.Slots.Sort((a, b) => a.Slot.CompareTo(b.Slot));
            List<EncounterDef> extras = new List<EncounterDef>();
            if (day == _corpseDay) extras.Add(ProgramCatalog.Encounter(ProgramCatalog.CeilingLegs));   // 59차: 시체 낙하는 회차에 한 번 — 2·3일차 중 고른 하루(겹침 조우)
            NightProgram program = new NightProgram(day, d.Slots, deck, null, extras);
            return new NightProgram(day, d.Slots, deck, d.Note.Length > 0 ? d.Note + program.ToString() : program.ToString(), extras);
        }

        /// <summary>
        /// 57차: 하루 <see cref="RulesPerDay"/>장으로 줄인다 — 그날 점검이 없는 공간의 채움 → 다른 채움 → 묶이지 않은 경비실 순으로 뺀다(G1은 남긴다).
        /// 조우·강제 수칙(<c>Locked</c>)과 가장 낮은 축의 마지막 한 장은 빼지 않는다.
        /// </summary>
        private void TrimDeck(Draft d, List<RuleDef> deck, FearAxis lowest)
        {
            while (deck.Count > RulesPerDay)
            {
                RuleDef drop = null;   // G1(복도에서 뛰지 마십시오)은 매일 남긴다 — 달리기 판정·시험이 기대는 공통 수칙
                for (int pass = 0; pass < 2 && drop == null; pass++)
                {
                    for (int i = deck.Count - 1; i >= 0 && drop == null; i--)
                    {
                        RuleDef r = deck[i];
                        if (d.Locked.Contains(r.Id) || r.Space == SpaceId.SecurityRoom || r.Space == SpaceId.None) continue;
                        if (r.HasAxis && r.Axis == lowest && deck.FindAll(x => x.HasAxis && x.Axis == lowest).Count <= 1) continue;
                        if (pass == 0 && CountIn(_planForFill, r.Space) > 0) continue;   // 먼저 그날 가지 않는 공간의 수칙
                        drop = r;
                    }
                }

                if (drop == null)
                {
                    RuleDef k = deck.Find(r => r.Space == SpaceId.SecurityRoom && !d.Locked.Contains(r.Id));
                    drop = k;
                }

                if (drop == null) break;
                deck.Remove(drop);
                d.Note.Append(drop.Id).Append(" 뺌(하루 ").Append(RulesPerDay).Append("장). ");
            }
        }

        /// <summary>보너스 슬롯 C: 3일차부터, 밤 시작에 가장 높은 감각 축의 생존 수치가 49 이하일 때(긴장 디렉터 「잘하는 중」).</summary>
        private static bool SlotCAtStart(ProgramRequest request)
        {
            if (request.Day < 3 || request.Survival == null) return false;
            int max = Math.Max(request.Survival.GetValue(FearAxis.Auditory),
                Math.Max(request.Survival.GetValue(FearAxis.Illuminance), request.Survival.GetValue(FearAxis.Layout)));
            return max <= 49;
        }

        private EncounterDef PickEncounter(Draft d, int day, ProgramRequest request)
        {
            SlotEncounter prev = d.Slots.Count > 0 ? d.Slots[d.Slots.Count - 1] : null;
            List<EncounterDef> best = new List<EncounterDef>();
            int bestScore = int.MinValue;

            IReadOnlyList<EncounterDef> all = ProgramCatalog.AllEncounters;
            for (int i = 0; i < all.Count; i++)
            {
                EncounterDef e = all[i];
                if (Chosen(d, e.Id)) continue;
                if (e.Id == ProgramCatalog.CeilingLegs) continue;   // 59차: 시체 낙하는 슬롯 밖 겹침 조우(Extras)로 늘 건다
                if (IsChained(e.Id)) continue;   // 70차: 복도 끝에 선 자·모형 급습은 날짜 사슬로만
                if (!e.Satisfied(request.Shown)) continue;
                if (!SpecialOk(e, day, request)) continue;
                if (prev != null && prev.Encounter.Mob == e.Mob) continue;
                if (!CanPlaceEncounter(d, e)) continue;
                if (!WithinBudget(d, e, day)) continue;

                int score = 2 * (int)request.Shown.GetBand(e.Axis)
                            + (_seen.Contains(e.Id) ? 0 : 3)
                            + (e.IsCross ? 2 : 0)
                            - (prev != null && prev.Encounter.Axis == e.Axis ? 2 : 0)
                            + (IsDayFocus(day, e.Id) ? DayFocusBonus : 0);

                if (score > bestScore)
                {
                    bestScore = score;
                    best.Clear();
                    best.Add(e);
                }
                else if (score == bestScore)
                {
                    best.Add(e);
                }
            }

            return best.Count == 0 ? null : best[_rng.Next(best.Count)];
        }

        /// <summary>그날 무게를 둘 조우에 더하는 점수(51차 분배).</summary>
        public const int DayFocusBonus = 4;

        /// <summary>
        /// 51차(민: 「1~5일차에 밸런스 있게, 점점 무서워지도록」) — 일차별로 무게를 둘 조우.
        /// 2일차 소년(C2·C3)·시체 낙하·노란 얼굴 · 3일차 화장실 여자아이(T4)·CCTV 얼굴·소등 · 4일차 사람 나무·발소리·복도 끝 · 5일차 모형 급습·목소리.
        /// 점수만 더한다 — 발동 조건·예산·공간 충돌은 그대로다.
        /// </summary>
        public static bool IsDayFocus(int day, string encounterId)
        {
            switch (day)
            {
                case 2:
                    return encounterId == ProgramCatalog.CeilingLegs || encounterId == ProgramCatalog.YellowFace || encounterId == ProgramCatalog.BoyBang;
                case 3:
                    return encounterId == ProgramCatalog.ToiletGirl || encounterId == ProgramCatalog.CctvPerson
                           || encounterId == ProgramCatalog.ScienceBlackout || encounterId == ProgramCatalog.ToiletBlackout;
                case 4:
                    return encounterId == ProgramCatalog.PeopleTree || encounterId == ProgramCatalog.Footsteps
                           || encounterId == ProgramCatalog.HallEndFigure;
                case 5:
                    return encounterId == ProgramCatalog.ModelRush || encounterId == ProgramCatalog.CallingVoice;
                default:
                    return false;
            }
        }

        /// <summary>조우별 추가 조건.</summary>
        private static bool SpecialOk(EncounterDef e, int day, ProgramRequest request)
        {
            switch (e.Id)
            {
                case ProgramCatalog.BoySeated:
                    return CountIn(request.Inspections, SpaceId.Classroom) > 0;   // 교실 점검 항목을 처음 비출 때.
                case ProgramCatalog.YellowFace:
                    return CountIn(request.Inspections, SpaceId.Library) >= 2;    // 도서관 점검 2개를 끝내고.
                case ProgramCatalog.CctvPerson:
                    return day >= 3;   // 51차 분배: CCTV 얼굴 점프스케어는 3일차부터
                default:
                    return true;
            }
        }

        private static int CountIn(InspectionPlan plan, SpaceId space)
        {
            if (plan == null) return 0;
            int n = 0;
            for (int i = 0; i < plan.Assignments.Count; i++)
            {
                if (plan.Assignments[i].Item.Space == space) n++;
            }

            return n;
        }

        /// <summary>놀람 예산(최종 기획서 「놀람 예산」) — 강도 3 이상·4 이상·5의 하룻밤 상한.</summary>
        public static bool WithinBudget(IEnumerable<EncounterDef> chosen, EncounterDef candidate, int day)
        {
            int b3 = day <= 2 ? 3 : 4;
            int b4 = day <= 2 ? 1 : 2;
            int b5 = day <= 2 ? 0 : 1;

            int n3 = 0, n4 = 0, n5 = 0;
            List<EncounterDef> all = new List<EncounterDef>(chosen);
            all.Add(candidate);
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].Intensity >= 3) n3++;
                if (all[i].Intensity >= 4) n4++;
                if (all[i].Intensity >= 5) n5++;
            }

            // 3~4일차의 강도 5는 교차 조건을 채운 경우만.
            if (candidate.Intensity >= 5 && day <= 4 && !candidate.IsCross) return false;
            return n3 <= b3 && n4 <= b4 && n5 <= b5;
        }

        private static bool WithinBudget(Draft d, EncounterDef e, int day)
        {
            List<EncounterDef> chosen = new List<EncounterDef>();
            for (int i = 0; i < d.Slots.Count; i++) chosen.Add(d.Slots[i].Encounter);
            return WithinBudget(chosen, e, day);
        }

        private static bool Chosen(Draft d, string id)
        {
            for (int i = 0; i < d.Slots.Count; i++)
            {
                if (d.Slots[i].Encounter.Id == id) return true;
            }

            return false;
        }

        private List<RuleDef> RulesOf(EncounterDef e)
        {
            List<RuleDef> list = new List<RuleDef>();
            if (e.ResponseRule.Length > 0) list.Add(ProgramCatalog.Rule(e.ResponseRule));   // 52차: 수칙 없는 조우(시체 낙하)
            if (e.SecondRule.Length > 0) list.Add(ProgramCatalog.Rule(e.SecondRule));
            return list;
        }

        private bool CanPlaceEncounter(Draft d, EncounterDef e)
        {
            List<RuleDef> rules = RulesOf(e);
            int extraFlash = 0;
            for (int i = 0; i < rules.Count; i++)
            {
                RuleDef r = rules[i];
                if (d.Has(r.Id)) continue;
                if (!CanPlace(d, r, rules)) return false;
                if (r.UsesFlashlight) extraFlash++;
                if (r.Id == ProgramCatalog.ReverseReportRule && _reverseAssigned >= ProgramCatalog.ReverseReportPerRun) return false;
            }

            return d.Flashlights() + extraFlash <= ProgramCatalog.FlashlightPerDay;
        }

        /// <summary>
        /// 그 공간에 수칙을 둘 수 있는지 — 공간당 하루 1장. 교차 조우가 한 공간에 두 장을 함께 둘 때(<paramref name="together"/>)만 예외.
        /// </summary>
        private static bool CanPlace(Draft d, RuleDef r, List<RuleDef> together = null)
        {
            List<RuleDef> here = d.In(r.Space);
            for (int i = 0; i < here.Count; i++)
            {
                if (here[i].Id == r.Id) return true;
                if (together == null || !together.Contains(here[i])) return false;
            }

            return true;
        }

        /// <summary>70차: 날짜 사슬로만 들어가는 조우(복도 끝에 선 자 → 다음 날 모형 급습).</summary>
        public static bool IsChained(string encounterId)
        {
            return encounterId == ProgramCatalog.HallEndFigure || encounterId == ProgramCatalog.ModelRush;
        }

        private void PlaceChain(Draft d, string encounterId)
        {
            EncounterDef e = ProgramCatalog.Encounter(encounterId);
            if (e == null || Chosen(d, e.Id)) return;
            if (!CanPlaceEncounter(d, e)) { d.Note.Append(e.Id).Append(" 편성 실패(공간 충돌). "); return; }
            PlaceEncounter(d, e, true);
        }

        private void PlaceEncounter(Draft d, EncounterDef e, bool reserved)
        {
            List<RuleDef> rules = RulesOf(e);
            for (int i = 0; i < rules.Count; i++)
            {
                if (!d.Has(rules[i].Id)) Place(d, rules[i], true);
                else d.Locked.Add(rules[i].Id);
            }

            EncounterSlot slot = (EncounterSlot)Math.Min((int)EncounterSlot.C, d.Slots.Count);
            d.Slots.Add(new SlotEncounter(slot, e, reserved));
        }

        private static void Place(Draft d, RuleDef r, bool locked)
        {
            if (r == null) return;
            d.In(r.Space).Add(r);
            if (locked) d.Locked.Add(r.Id);
        }

        /// <summary>남은 칸 채움: 혼자 서는 수칙, 가장 높은 감각 축 ×2, 손전등 3장 한도. <paramref name="onlyAxis"/>가 있으면 그 축만.</summary>
        private RuleDef PickFiller(Draft d, SpaceId space, FearAxis highest, FearAxis? onlyAxis)
        {
            List<RuleDef> candidates = new List<RuleDef>();
            List<int> weights = new List<int>();
            List<RuleDef> inSpace = ProgramCatalog.RulesIn(space);
            int flash = d.Flashlights();
            for (int i = 0; i < inSpace.Count; i++)
            {
                RuleDef r = inSpace[i];
                if (!r.IsStandalone || !r.HasAxis) continue;
                if (ProgramCatalog.IsRetired(r.Id)) continue;   // 60차: 폐기한 수칙(C1 판서)
                if (r.UsesFlashlight && flash >= ProgramCatalog.FlashlightPerDay) continue;
                if (r.Id == "S3" && PlanHas(_planForFill, "S-1")) continue;   // 51차: 모형을 비추지 말라는 날에 모형 점검은 없다
                if (onlyAxis.HasValue && r.Axis != onlyAxis.Value) continue;
                candidates.Add(r);
                weights.Add(r.Axis == highest ? 2 : 1);
            }

            if (candidates.Count == 0) return null;
            int sum = 0;
            for (int i = 0; i < weights.Count; i++) sum += weights[i];
            int roll = _rng.Next(sum);
            for (int i = 0; i < candidates.Count; i++)
            {
                roll -= weights[i];
                if (roll < 0) return candidates[i];
            }

            return candidates[candidates.Count - 1];
        }

        /// <summary>가장 낮은 감각 축(연출 구간, 동점이면 생존 수치가 낮은 쪽)의 수칙이 한 장도 없으면 채움 한 장을 그 축으로 바꾼다.</summary>
        private void EnsureLowestAxis(Draft d, IFearAxisReader shown)
        {
            FearAxis lowest = LowestShown(shown);
            foreach (KeyValuePair<SpaceId, List<RuleDef>> kv in d.Spaces)
            {
                for (int i = 0; i < kv.Value.Count; i++)
                {
                    if (kv.Value[i].HasAxis && kv.Value[i].Axis == lowest) return;
                }
            }

            FearAxis highest = HighestShown(shown);
            for (int s = 0; s < ProgramCatalog.RuleSpaces.Length; s++)
            {
                SpaceId space = ProgramCatalog.RuleSpaces[s];
                List<RuleDef> here = d.In(space);
                if (here.Count != 1 || d.Locked.Contains(here[0].Id)) continue;

                RuleDef old = here[0];
                here.Clear();
                RuleDef swap = PickFiller(d, space, highest, lowest);
                if (swap != null)
                {
                    here.Add(swap);
                    return;
                }

                here.Add(old);
            }

            d.Note.Append("가장 낮은 축(").Append(lowest).Append(") 수칙을 넣지 못했습니다. ");
        }

        private static readonly FearAxis[] Sensory = { FearAxis.Auditory, FearAxis.Illuminance, FearAxis.Layout };

        /// <summary>가장 높은 감각 축(연출 구간). 동점이면 청각 &gt; 조도 &gt; 배치.</summary>
        public static FearAxis HighestShown(IFearAxisReader shown)
        {
            FearAxis best = Sensory[0];
            for (int i = 1; i < Sensory.Length; i++)
            {
                if (shown.GetBand(Sensory[i]) > shown.GetBand(best)) best = Sensory[i];
            }

            return best;
        }

        /// <summary>가장 낮은 감각 축(연출 구간, 동점이면 생존 수치가 낮은 쪽, 그래도 같으면 배치 &gt; 조도 &gt; 청각).</summary>
        public static FearAxis LowestShown(IFearAxisReader shown)
        {
            FearAxis best = Sensory[Sensory.Length - 1];
            for (int i = Sensory.Length - 2; i >= 0; i--)
            {
                FearAxis a = Sensory[i];
                Band ba = shown.GetBand(a);
                Band bb = shown.GetBand(best);
                if (ba < bb || (ba == bb && shown.GetValue(a) < shown.GetValue(best))) best = a;
            }

            return best;
        }
    }
}

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
        C = 2,

        /// <summary>104차 고정 편성(<see cref="ProgramDirector.FixedSchedule"/>): 넷째 조우 — 순서대로 여는 밤(<see cref="NightProgram.Sequential"/>)에만 쓴다. 시간창은 C와 같다(옛 경로).</summary>
        D = 3,

        /// <summary>104차 고정 편성: 다섯째 조우.</summary>
        E = 4,

        /// <summary>104차 고정 편성: 여섯째 조우.</summary>
        F = 5
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
        private readonly List<string> _sequence;
        private readonly List<EncounterSlot> _windows;
        private readonly List<string> _standing;

        /// <summary>일차.</summary>
        public readonly int Day;

        /// <summary>편성 설명(줄인 것·강제한 것 포함).</summary>
        public readonly string Report;

        /// <summary>만든다.</summary>
        public NightProgram(int day, IEnumerable<SlotEncounter> slots, IEnumerable<RuleDef> deck, string report, IEnumerable<EncounterDef> extras = null, IEnumerable<string> sequence = null, IEnumerable<EncounterSlot> windows = null, IEnumerable<string> standing = null)
        {
            _standing = standing != null ? new List<string>(standing) : new List<string>();
            _windows = windows != null ? new List<EncounterSlot>(windows) : new List<EncounterSlot>();
            Day = day;
            _slots = slots != null ? new List<SlotEncounter>(slots) : new List<SlotEncounter>();
            _extras = extras != null ? new List<EncounterDef>(extras) : new List<EncounterDef>();
            _deck = deck != null ? new List<RuleDef>(deck) : new List<RuleDef>();
            _sequence = sequence != null ? new List<string>(sequence) : new List<string>();
            Report = report ?? string.Empty;
        }

        /// <summary>
        /// 104차(사용자: 「이월은 없어. 의도한 날짜에 무조건 목격해야 해」): 고정 편성의 조우 순서(슬롯·겹침 조우 모두). 비어 있지 않으면
        /// 긴장 디렉터가 시간창 대신 이 순서대로 하나씩 연다(<see cref="Sequential"/>) — 놓침 없이 끝날 때까지 기다리고, 그동안 유도 지시를 낸다.
        /// </summary>
        public IReadOnlyList<string> Sequence
        {
            get { return _sequence; }
        }

        /// <summary>104차: 순서대로 여는 밤인지(고정 편성).</summary>
        public bool Sequential
        {
            get { return _sequence.Count > 0; }
        }

        /// <summary>
        /// 104차(사용자: 「너무 조우 위주로 굴러가는 거 아니야? 원래 짜여져 있던 흐름이 있지 않았어?」): 순서대로 여는 밤에도 조우는 원래 시간 뼈대 안에서만 열린다 —
        /// 그 조우가 열릴 수 있는 창(A 01:00~ · B 02:16~ · C 03:08~). 출근~01:00은 점검·근무 지시만, 01:52~02:16 이완(근무일지)에는 새 조우를 열지 않는다.
        /// </summary>
        public EncounterSlot WindowOf(string encounterId)
        {
            int at = _sequence.IndexOf(encounterId);
            return at >= 0 && at < _windows.Count ? _windows[at] : EncounterSlot.A;
        }

        /// <summary>104차: 순서대로 여는 밤의 창 나누기 — 조우 n개를 A·B·C에 앞에서부터(클라이맥스인 마지막은 늘 가장 늦은 창).</summary>
        public static List<EncounterSlot> SplitWindows(int n)
        {
            List<EncounterSlot> list = new List<EncounterSlot>();
            if (n <= 0) return list;
            if (n == 1)
            {
                list.Add(EncounterSlot.B);
                return list;
            }

            if (n == 2)
            {
                list.Add(EncounterSlot.A);
                list.Add(EncounterSlot.B);
                return list;
            }

            // 셋 이상: C는 클라이맥스 하나(여섯 이상이면 둘), 나머지를 A·B에 앞이 많게.
            int c = n >= 6 ? 2 : 1;
            int rest = n - c;
            int a = (rest + 1) / 2;
            for (int i = 0; i < a; i++) list.Add(EncounterSlot.A);
            for (int i = 0; i < rest - a; i++) list.Add(EncounterSlot.B);
            for (int i = 0; i < c; i++) list.Add(EncounterSlot.C);
            return list;
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

        /// <summary>
        /// 104차(사용자: 「day 4 복도 끝에 선 자는 그냥 서 있는 거로 고정, 조우 취급 X · day 5 인체 모형 급습은 상시 대기, 빛을 받는 순간 바로 급습」):
        /// 그날 밤새 서 있는 것(순서·시계 붙듦·유도 밖). 4일차 복도 끝 모형(<see cref="ProgramCatalog.HallEndFigure"/> — 그냥 서 있음) ·
        /// 5일차 모형 급습(<see cref="ProgramCatalog.ModelRush"/> — 빛을 받으면 곧바로, <see cref="NightRun.TriggerStanding"/>).
        /// </summary>
        public IReadOnlyList<string> Standing
        {
            get { return _standing; }
        }

        /// <summary>104차: 그날 밤새 서 있는 것인지(<see cref="Standing"/>).</summary>
        public bool IsStanding(string encounterId)
        {
            return encounterId != null && _standing.Contains(encounterId);
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
    public sealed class ProgramDirector : ISnapshotable
    {
        /// <summary>
        /// 그날 수칙 수 — 74차(민: 「1일차에 등장하는 수칙들은 4개, 2일~4일차 5개, 5일차 6개」). 옛 57차는 매일 6장. 103차: 2~4일차 6 · 5일차 7(가짜 수칙 자리).
        /// </summary>
        public static int RulesFor(int day)
        {
            // 103차(사용자: 「가짜 수칙은 첫날에 원래 수칙 4개 + 가짜 2개로 첫날에만」): 2~5일차에 한 줄씩 끼던 가짜 수칙 자리를 진짜 수칙으로 — 2~4일차 5 → 6, 5일차 6 → 7.
            // 태블릿 줄 수는 그대로(1일차 4 + 가짜 2 = 6). 잘 안 나오던 수칙(도서관 L1·L2 13 → 37%)·변조(회차 1.5 → 2.5번)가 는다.
            if (day <= 1) return 4;
            if (day >= 5) return 7;
            return 6;
        }

        /// <summary>1일차 수칙 후보(57차, 민: 「1~2일차 수칙이 너무 풀이 좁다」) — 조우에 묶이지 않은 수칙 중 1일차 점검 공간(복도·교실·과학실·경비실)의 것. S1·G1은 고정.</summary>
        public static readonly string[] Day1Pool = { "H2", "C4", "S2", "K2", "K3" };   // 60차: C1(판서) 폐기

        private readonly Random _rng;
        private readonly HashSet<string> _seen = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _reserved = new HashSet<string>(StringComparer.Ordinal);
        private int _reverseAssigned;
        private InspectionPlan _planForFill;
        private int _corpseDay;
        private readonly HashSet<string> _lastDeck = new HashSet<string>(StringComparer.Ordinal);   // 103차(편성 다양성): 지난 밤 덱 — 채움에서 덜 뽑는다
        private readonly HashSet<string> _runRules = new HashSet<string>(StringComparer.Ordinal);   // 103차: 회차에 덱에 올랐던 수칙(변조 후보 — ParadoxPlanner.TamperLateDay부터 조금 더 뽑는다)
        private int _buildDay;

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
            _cctvDay = RollCctvDay();
            RollBlackouts();
        }

        // ── 104차: 고정 날짜 편성 ─────────────────────────────

        /// <summary>
        /// 104차(사용자: 「모든 연출에서 축 조건 제거 · 날짜 편성 — 이월은 없어. 의도한 날짜에 무조건 목격해야 해」): 참이면 조우를 날짜표(<see cref="FixedEncounters"/>)대로만 건다 —
        /// 축 구간·점수 뽑기·예약·재등장 막기를 보지 않는다. 그 밤 편성에는 순서(<see cref="NightProgram.Sequence"/>)가 붙어 디렉터가 하나씩 연다.
        /// 거짓이면 옛 뽑기(103차까지 — 옛 시험이 쓴다).
        /// <list type="bullet">
        /// <item>2일차: 시체 낙하 · 과학실 소등</item>
        /// <item>3일차: 창밖 노란 얼굴 · 달려오는 발소리 · CCTV 사람 · 화장실 소등(사용자: 「화장실 소등은 3일차로」)</item>
        /// <item>4일차: 소년 머리 박기(첫 역설 C2) · 화장실 여자아이 · 사람 나무 · 복도 끝에 선 자</item>
        /// <item>5일차: 사람 나무 · 부르는 목소리 · 도서관 노란 얼굴 · 모형 급습</item>
        /// </list>
        /// </summary>
        public static bool FixedSchedule = true;

        /// <summary>104차: 고정 편성에서 첫 역설(C2 — 소년)이 나오는 날(소년 머리 박기 날). 옛 편성은 2일차.</summary>
        public const int FixedFirstParadoxDay = 4;

        /// <summary>104차: 고정 편성에서 남은 소등이 나올 수 있는 날(4·5일차 중 하루).</summary>
        public const int LateBlackoutFrom = 4;

        /// <inheritdoc cref="LateBlackoutFrom"/>
        public const int LateBlackoutTo = 5;

        private string _earlyBlackout = ProgramCatalog.ScienceBlackout;
        private int _lateBlackoutDay = LateBlackoutFrom;

        /// <summary>104차: 2일차에 나오는 소등(과학실/화장실). 나머지 하나는 <see cref="LateBlackoutDay"/>에.</summary>
        public string EarlyBlackout
        {
            get { return _earlyBlackout; }
        }

        /// <summary>104차: 4·5일차에 나오는 남은 소등.</summary>
        public string LateBlackout
        {
            get { return _earlyBlackout == ProgramCatalog.ScienceBlackout ? ProgramCatalog.ToiletBlackout : ProgramCatalog.ScienceBlackout; }
        }

        /// <summary>104차: 남은 소등이 나오는 날(4 또는 5).</summary>
        public int LateBlackoutDay
        {
            get { return _lateBlackoutDay; }
        }

        /// <summary>시험 · 개발용: 소등 배치를 정한다.</summary>
        public void SetBlackouts(string early, int lateDay)
        {
            _earlyBlackout = early == ProgramCatalog.ToiletBlackout ? ProgramCatalog.ToiletBlackout : ProgramCatalog.ScienceBlackout;
            _lateBlackoutDay = Math.Max(LateBlackoutFrom, Math.Min(LateBlackoutTo, lateDay));
        }

        private void RollBlackouts()
        {
            // 104차(사용자: 「2일/3일차엔 과학실 정전, 4일/5일차엔 화장실 정전으로」): 2일차는 늘 과학실 소등, 화장실 소등은 4·5일차 중 하루.
            _earlyBlackout = ProgramCatalog.ScienceBlackout;
            _lateBlackoutDay = LateBlackoutFrom + _rng.Next(LateBlackoutTo - LateBlackoutFrom + 1);
        }

        /// <summary>104차: 그날 고정 편성의 조우(여는 순서). 1일차는 없다.</summary>
        public List<string> FixedEncounters(int day)
        {
            List<string> list = new List<string>();
            switch (day)
            {
                case 2:
                    list.Add(ProgramCatalog.CeilingLegs);
                    list.Add(_earlyBlackout);
                    break;
                case 3:
                    list.Add(ProgramCatalog.SuitMan);
                    list.Add(ProgramCatalog.Footsteps);
                    list.Add(ProgramCatalog.CctvPerson);
                    list.Add(ProgramCatalog.ToiletBlackout);   // 104차(사용자: 「화장실 소등은 3일차로」)
                    break;
                case 4:
                    list.Add(ProgramCatalog.BoyBang);
                    list.Add(ProgramCatalog.ToiletGirl);
                    list.Add(ProgramCatalog.PeopleTree);
                    break;
                case 5:
                    list.Add(ProgramCatalog.PeopleTree);
                    list.Add(ProgramCatalog.CallingVoice);
                    list.Add(ProgramCatalog.YellowFace);
                    break;
            }

            return list;
        }

        /// <summary>
        /// 104차(사용자: 「복도 끝에 선 자는 그냥 서 있는 거로 고정, 조우 취급 X · 인체 모형 급습은 상시 대기, 빛을 받는 순간 바로 급습」): 그날 밤새 서 있는 것 —
        /// 4일차 복도 끝 모형(수칙 없음) · 5일차 모형 급습(S5). 순서대로 여는 조우가 아니다(<see cref="NightProgram.Standing"/>).
        /// </summary>
        public static List<string> FixedStanding(int day)
        {
            List<string> list = new List<string>();
            if (day == 4) list.Add(ProgramCatalog.HallEndFigure);
            if (day == 5) list.Add(ProgramCatalog.ModelRush);
            return list;
        }

        /// <summary>104차: 그 조우가 그날 고정 편성에 있는지.</summary>
        public bool FixedOn(string encounterId, int day)
        {
            string id = MergedInto(encounterId);
            return FixedEncounters(day).Contains(id) || (id == ProgramCatalog.ModelRush && FixedStanding(day).Contains(id));
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
            RollFaceDays();
        }

        /// <summary>103차: 창밖 노란 얼굴은 2·3일차 중 하루, 도서관 노란 얼굴은 4·5일차 중 하루 — 난수 한 번으로 둘 다 고른다.</summary>
        private void RollFaceDays()
        {
            int roll = _rng.Next(4);
            _windowDay = Math.Min(WindowFaceTo, WindowFaceFrom + (roll & 1));   // 103차 후속: 창밖은 3일차 고정(난수는 그대로 한 번 — 도서관 쪽 날짜 분포를 바꾸지 않게)
            _faceDay = DefaultFaceDay + (roll >> 1);
        }

        /// <summary>시험 · 개발용: 이 회차의 노란 얼굴 날짜를 정한다(각 구간 안으로 자른다).</summary>
        public void SetFaceDays(int windowDay, int faceDay)
        {
            _windowDay = Math.Max(WindowFaceFrom, Math.Min(WindowFaceTo, windowDay));
            _faceDay = Math.Max(DefaultFaceDay, Math.Min(FaceTo, faceDay));
        }

        // ── 84차: 창밖 노란 얼굴 → 다음 날 도서관 노란 얼굴 ─────────────

        /// <summary>
        /// 84차(민: 「현재 복도 조우가 복도 급습의 빌드업이 되는 것처럼, 창밖의 노란 얼굴 조우가 도서관 노란 얼굴 조우의 빌드업이 되면 좋겠어」) —
        /// 도서관 노란 얼굴(문간)이 나오는 첫날. 둘은 슬롯 뽑기에서 빠지고 이 날짜로만 들어간다(복도 끝 → 모형 급습과 같은 규칙).
        /// 103차(민: 「2일 3일 중 하루 창문, 4일 5일 중 하루 노란 얼굴」): 회차마다 창밖 노란 얼굴은 <see cref="WindowFaceFrom"/>~<see cref="WindowFaceTo"/>일차 중 하루,
        /// 도서관 노란 얼굴은 <see cref="DefaultFaceDay"/>~<see cref="FaceTo"/>일차 중 하루(<see cref="RollFaceDays"/>). <see cref="DefaultFaceDay"/>는 도서관 쪽 첫 후보일.
        /// </summary>
        public const int DefaultFaceDay = 4;

        /// <summary>103차: 도서관 노란 얼굴이 나올 수 있는 마지막 날(포함).</summary>
        public const int FaceTo = 5;

        /// <summary>
        /// 103차: 창밖 노란 얼굴이 설 수 있는 첫날·마지막 날(포함).
        /// 103차 후속(사용자 결정, 조우 강도 곡선): 2 → 3 — 2일차에 걸리면 그 밤이 회차에서 가장 무거웠다(강도 합 11.0: 머리 박기 4 · 시체 낙하 2 · 창밖 3 · 여분 2).
        /// 3일차 고정이면 일차별 강도 합 9.0 → 9.1 → 9.5 → 9.5(옛 10.0 → 8.1 → 9.5 → 9.5), 조우 13개 목격은 그대로.
        /// </summary>
        public const int WindowFaceFrom = 3;

        /// <inheritdoc cref="WindowFaceFrom"/>
        public const int WindowFaceTo = 3;

        private int _faceDay;
        private int _windowDay;

        /// <summary>그 회차에 도서관 노란 얼굴이 나오는 날(4·5일차 중 하루).</summary>
        public int FaceDay
        {
            get { return _faceDay; }
        }

        /// <summary>그 회차에 창밖 노란 얼굴이 서는 날(2·3일차 중 하루).</summary>
        public int WindowFaceDay
        {
            get { return _windowDay; }
        }

        /// <summary>103차: 그날 밤 창밖 노란 얼굴이 서는지.</summary>
        public bool IsWindowFaceDay(int day)
        {
            return day == _windowDay;
        }

        /// <summary>103차: 그날 밤 도서관 노란 얼굴이 나오는지.</summary>
        public bool IsFaceDay(int day)
        {
            return day == _faceDay;
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
        /// 시체 낙하(겹침 조우)가 나오는 그 회차의 하루 — 회차에 딱 한 번(59차 민: 「무서운 연출은 똑같은 게 반복되면 재미없다 — 2~3일차에 한 번만」).
        /// 103차(사용자: 「시체 낙하의 강도를 낮춰서 조금 더 일찍」): 늘 2일차(옛: 2·3일차 반반), 강도 3 → 2.
        /// </summary>
        public int CorpseDay
        {
            get { return _corpseDay; }
        }

        /// <summary>시체 낙하가 나올 수 있는 날(포함).</summary>
        public const int CorpseDayFrom = 2;

        /// <summary>시체 낙하가 나올 수 있는 마지막 날(포함).</summary>
        public const int CorpseDayTo = 2;   // 103차: 3 → 2(늘 2일차)

        private int RollCorpseDay()
        {
            return CorpseDayFrom + _rng.Next(CorpseDayTo - CorpseDayFrom + 1);
        }

        // ── 103차: CCTV에만 보이는 사람 → 겹침 조우 ─────────────

        /// <summary>
        /// 103차(민: 「모든 조우 연출을 목격할 수 있도록」) — CCTV에만 보이는 사람은 화면 속에서만 지나가 공간·슬롯을 차지할 까닭이 없다.
        /// 회차에 한 번, <see cref="CctvDayFrom"/>~<see cref="CctvDayTo"/>일차 중 하루에 겹침 조우(<see cref="NightProgram.Extras"/>)로 걸고 경비실 수칙은 K1로 둔다.
        /// 옛: 슬롯 하나를 먹어 회차당 조우 15개 중 11~12개만 나왔다.
        /// </summary>
        public const int CctvDayFrom = 3;

        /// <inheritdoc cref="CctvDayFrom"/>
        public const int CctvDayTo = 4;   // 5일차 경비실은 K4

        private int _cctvDay;

        /// <summary>그 회차에 CCTV에만 보이는 사람이 나오는 날.</summary>
        public int CctvDay
        {
            get { return _cctvDay; }
        }

        /// <summary>
        /// 103차: 그날 그 수칙을 강제 편성(회피 불가 쌍)할 수 있는지 — 공간당 하루 한 장 · 회차에 한 번 나오는 조우를 지킨다(편성에서 떨어져 그날 회피 불가가 취소되던 것).
        /// 날짜 사슬 조우가 그 공간을 차지하는 날은 안 된다(5일차 과학실 = 급습 S5). K1(CCTV 사람)은 CCTV 날 이전·당일에만, 다른 경비실 수칙은 CCTV 날이 아닐 때만.
        /// 조우에 묶인 수칙은 그 조우가 회차에 이미 나왔으면 안 된다(S5×H3이 발소리를 다시 부르던 것).
        /// </summary>
        /// <param name="ruleId">수칙 ID.</param>
        /// <param name="day">편성할 일차.</param>
        public bool CanForce(string ruleId, int day)
        {
            RuleDef r = ProgramCatalog.Rule(ruleId);
            if (r == null) return false;
            SpaceId space = SpaceIds.Canonical(r.Space);
            if (FixedSchedule) return CanForceFixed(r, day);

            // 날짜 사슬 조우(복도 끝·급습 S5 · 도서관 노란 얼굴 L3 …)가 그날 그 공간을 먼저 차지한다 — 그 수칙이면 이미 들어온다.
            List<RuleDef> chained = ChainRulesOn(day);
            for (int i = 0; i < chained.Count; i++) if (chained[i].Id == r.Id) return true;
            for (int i = 0; i < chained.Count; i++) if (space != SpaceId.None && SpaceIds.Canonical(chained[i].Space) == space) return false;

            if (space == SpaceId.SecurityRoom)
            {
                if (r.BoundEncounter == ProgramCatalog.CctvPerson) return day <= _cctvDay && !_seen.Contains(ProgramCatalog.CctvPerson);
                return day != _cctvDay;
            }

            // 조우에 묶인 수칙은 그 조우를 함께 부른다 — 회차에 이미 나온 조우(발소리·부르는 목소리 …)는 다시 부르지 않는다.
            if (!r.IsStandalone && !string.IsNullOrEmpty(r.BoundEncounter))
            {
                string bound = MergedInto(r.BoundEncounter);
                if (IsChained(bound) || _seen.Contains(bound)) return false;
            }

            return true;
        }

        private List<RuleDef> ChainRulesOn(int day)
        {
            List<RuleDef> list = new List<RuleDef>();
            // 103차 QA: 모형 급습 예약(S3 위반)이 있으면 편성 때 복도 끝 날이 오늘로 당겨진다(ApplyRushReservation) — 미리 그렇게 본다.
            bool pulled = _reserved.Contains(ProgramCatalog.ModelRush) && day < _hallDay;
            int hallDay = pulled ? day : _hallDay;
            int rushDay = pulled ? day + 1 : _rushDay;
            if (day == hallDay) AddRulesOf(list, ProgramCatalog.HallEndFigure);
            if (day == rushDay) AddRulesOf(list, ProgramCatalog.ModelRush);
            if (IsWindowFaceDay(day)) AddRulesOf(list, ProgramCatalog.SuitMan);
            if (IsFaceDay(day)) AddRulesOf(list, ProgramCatalog.YellowFace);
            return list;
        }

        private void AddRulesOf(List<RuleDef> list, string encounterId)
        {
            EncounterDef e = ProgramCatalog.Encounter(encounterId);
            if (e == null) return;
            List<RuleDef> rules = RulesOf(e);
            for (int i = 0; i < rules.Count; i++) if (rules[i] != null) list.Add(rules[i]);
        }

        private int RollCctvDay()
        {
            return CctvDayFrom + _rng.Next(CctvDayTo - CctvDayFrom + 1);
        }

        private static bool PlaceCctv(Draft d)
        {
            RuleDef k1 = ProgramCatalog.Rule("K1");
            if (k1 == null) return false;
            if (d.Has(k1.Id)) return true;
            if (d.In(SpaceId.SecurityRoom).Count > 0) return false;
            Place(d, k1, true);
            return true;
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
            _lastDeck.Clear();
            _runRules.Clear();
            _corpseDay = RollCorpseDay();
            ResetRushChain();
            _cctvDay = RollCctvDay();
            RollBlackouts();
        }

        /// <summary>
        /// 다음 밤 조우를 예약한다 — S3 위반이면 <see cref="ProgramCatalog.ModelRush"/>(84차: 노란 얼굴에서 빛을 떼도 예약하지 않는다 — 창밖 노란 얼굴은 날짜 사슬).
        /// 다음 <see cref="Build"/>가 쓰고 비운다.
        /// </summary>
        public void Reserve(string encounterId)
        {
            if (FixedSchedule) return;   // 104차(사용자: 「후속 개념은 없애줘」): 고정 편성은 날짜표뿐 — 위반으로 다음 날 조우가 더해지지 않는다
            if (ProgramCatalog.Encounter(encounterId) != null) _reserved.Add(encounterId);
        }

        /// <inheritdoc/>
        public string SnapshotKey
        {
            get { return "ProgramReserved"; }
        }

        /// <summary>103차 QA: 밤 동안 쌓인 조우 예약(S3 위반 → 모형 급습)도 재시작·결근에 되돌린다 — 옛: 재시작해서 S3를 지켜도 예약이 남아 급습이 당겨졌다.</summary>
        public object CaptureState()
        {
            return new List<string>(_reserved);
        }

        /// <inheritdoc/>
        public void RestoreState(object state)
        {
            List<string> s = state as List<string>;
            if (s == null) return;
            _reserved.Clear();
            for (int i = 0; i < s.Count; i++) _reserved.Add(s[i]);
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
            _buildDay = day;
            NightProgram program = day == 1 ? BuildDay1(request) : FixedSchedule ? BuildFixed(day, request, reserved) : BuildRegular(day, request, reserved);

            for (int i = 0; i < program.Slots.Count; i++) _seen.Add(program.Slots[i].Encounter.Id);
            for (int i = 0; i < program.Extras.Count; i++) _seen.Add(program.Extras[i].Id);
            if (program.Has(ProgramCatalog.ReverseReportRule)) _reverseAssigned++;
            _lastDeck.Clear();
            for (int i = 0; i < program.Deck.Count; i++)
            {
                _lastDeck.Add(program.Deck[i].Id);
                _runRules.Add(program.Deck[i].Id);
            }
            return program;
        }

        // ── 104차: 고정 편성 ─────────────────────────────────────

        /// <summary>
        /// 104차 고정 편성에서 강제(회피 불가 쌍)할 수 있는지 — 조우에 묶인 수칙은 그 조우가 그날 날짜표에 있을 때만(다른 날 조우를 끌어오지 않는다).
        /// 경비실: CCTV 사람(K1)은 CCTV 날(3일차)에만, 다른 경비실 수칙은 그날이 아닐 때만(경비실은 한 장).
        /// </summary>
        private bool CanForceFixed(RuleDef r, int day)
        {
            if (SpaceIds.Canonical(r.Space) == SpaceId.SecurityRoom)
            {
                bool cctvDay = FixedOn(ProgramCatalog.CctvPerson, day);
                if (r.BoundEncounter == ProgramCatalog.CctvPerson) return cctvDay;
                return !cctvDay;
            }

            if (!r.IsStandalone && !string.IsNullOrEmpty(r.BoundEncounter)) return FixedOn(r.BoundEncounter, day);
            return true;
        }

        /// <summary>
        /// 104차 고정 편성 — 날짜표의 조우를 순서대로 걸고(축·예산·공간 충돌 무시, 묶인 수칙은 같은 공간에 둘이어도 둔다) 남은 공간을 채운다.
        /// 하루 장수(<see cref="RulesFor"/>)는 그대로 — 넘치면 조우 수칙 말고 나머지(채움 → 경비실 → G4 → G1)부터 뺀다(사용자: 「수칙 장수는 조우 말고 나머지를 빼는 방안」).
        /// 조우·강제 수칙만으로 넘치면 그만큼은 둔다.
        /// </summary>
        private NightProgram BuildFixed(int day, ProgramRequest request, List<string> reserved)
        {
            Draft d = new Draft();
            IFearAxisReader shown = request.Shown;
            if (day >= 5) Place(d, ProgramCatalog.Rule(ProgramCatalog.FinaleRule), true);   // 5일차 경비실은 K4

            List<string> order = ShuffledOrder(day);   // 104차(사용자: 「후속 개념은 없애줘」): 예약(reserved)은 보지 않는다 — 날짜표 그대로
            List<EncounterDef> extras = new List<EncounterDef>();
            for (int i = 0; i < order.Count; i++)
            {
                EncounterDef e = ProgramCatalog.Encounter(order[i]);
                if (e == null) continue;
                if (e.Id == ProgramCatalog.CeilingLegs)
                {
                    extras.Add(e);   // 수칙 없음 — 사다리 점검(C-3) 중
                    continue;
                }

                if (e.Id == ProgramCatalog.CctvPerson)
                {
                    RuleDef k1 = ProgramCatalog.Rule("K1");
                    if (k1 != null && !d.Has(k1.Id)) Place(d, k1, true);
                    extras.Add(e);
                    continue;
                }

                List<RuleDef> rules = RulesOf(e);
                for (int k = 0; k < rules.Count; k++)
                {
                    if (rules[k] == null) continue;
                    if (!d.Has(rules[k].Id)) Place(d, rules[k], true);
                    else d.Locked.Add(rules[k].Id);
                }

                EncounterSlot slot = (EncounterSlot)Math.Min((int)EncounterSlot.F, d.Slots.Count);
                d.Slots.Add(new SlotEncounter(slot, e, false));
            }

            // 104차: 밤새 서 있는 모형 급습(5일차)의 수칙(S5)도 둔다 — 복도 끝 모형(4일차)은 그냥 서 있을 뿐이라 수칙이 없다.
            List<string> standing = FixedStanding(day);
            if (standing.Contains(ProgramCatalog.ModelRush))
            {
                List<RuleDef> rush = RulesOf(ProgramCatalog.Encounter(ProgramCatalog.ModelRush));
                for (int k = 0; k < rush.Count; k++)
                {
                    if (rush[k] == null) continue;
                    if (!d.Has(rush[k].Id)) Place(d, rush[k], true);
                    else d.Locked.Add(rush[k].Id);
                }
            }

            // 강제 수칙(회피 불가 쌍) — 묶인 수칙은 그날 조우가 이미 들여놓았을 때만(CanForceFixed가 미리 거른다).
            if (request.ForcedRules != null)
            {
                foreach (string id in request.ForcedRules)
                {
                    RuleDef r = ProgramCatalog.Rule(id);
                    if (r == null) continue;
                    if (ProgramCatalog.IsRetired(r.Id)) { d.Note.Append(r.Id).Append(" 강제 편성 실패(폐기한 수칙). "); continue; }
                    if (d.Has(r.Id)) { d.Locked.Add(r.Id); continue; }
                    if (!r.IsStandalone) { d.Note.Append(r.Id).Append(" 강제 편성 실패(묶인 조우가 그날 없음). "); continue; }
                    if (SpaceIds.Canonical(r.Space) == SpaceId.SecurityRoom && d.In(SpaceId.SecurityRoom).Count > 0) { d.Note.Append(r.Id).Append(" 강제 편성 실패(경비실 충돌). "); continue; }
                    Place(d, r, true);
                }
            }

            // 남은 공간 채움(옛과 같다).
            FearAxis highest = HighestShown(shown);
            for (int s = 0; s < ProgramCatalog.RuleSpaces.Length; s++)
            {
                SpaceId space = ProgramCatalog.RuleSpaces[s];
                if (d.In(space).Count > 0) continue;
                RuleDef r = PickFiller(d, space, highest, null);
                if (r != null) Place(d, r, false);
            }

            if (d.In(SpaceId.SecurityRoom).Count == 0) Place(d, PickSecurity(d), false);
            EnsureLowestAxis(d, shown);
            EnsureEverySensoryAxis(d, shown);

            List<RuleDef> deck = new List<RuleDef>();
            for (int s = 0; s < ProgramCatalog.RuleSpaces.Length; s++) deck.AddRange(d.In(ProgramCatalog.RuleSpaces[s]));
            deck.AddRange(d.In(SpaceId.SecurityRoom));
            deck.Add(ProgramCatalog.Rule("G1"));
            if (HasTabletRule(day) && !_runRules.Contains(TabletRule)) deck.Add(ProgramCatalog.Rule(TabletRule));
            int target = RulesFor(day);
            TrimDeck(d, deck, LowestShown(shown), target);
            while (deck.Count > target)
            {
                // TrimDeck은 공통 수칙(G1)·축의 마지막 한 장을 남긴다 — 고정 편성은 조우 수칙이 우선이라 남은 채움·G1도 뺀다.
                int at = deck.FindLastIndex(r => !d.Locked.Contains(r.Id) && r.Id != "G1");
                if (at < 0) at = deck.FindIndex(r => !d.Locked.Contains(r.Id));
                if (at < 0) break;
                d.Note.Append(deck[at].Id).Append(" 뺌(조우 수칙 우선). ");
                deck.RemoveAt(at);
            }

            List<string> sequence = new List<string>();
            List<string> shown2 = new List<string>();
            for (int i = 0; i < order.Count; i++)
            {
                if (ProgramCatalog.Encounter(order[i]) == null) continue;
                sequence.Add(order[i]);
                shown2.Add(order[i]);
            }

            List<EncounterSlot> windows = NightProgram.SplitWindows(sequence.Count);
            for (int i = 0; i < shown2.Count && i < windows.Count; i++) shown2[i] = windows[i] + ":" + shown2[i];
            NightProgram program = new NightProgram(day, d.Slots, deck, null, extras, sequence, windows, standing);
            string stand = standing.Count > 0 ? " 밤새 서 있음 [" + string.Join(", ", standing.ToArray()) + "]" : string.Empty;
            string report = "고정 편성 [" + string.Join(" → ", shown2.ToArray()) + "]" + stand + " " + (d.Note.Length > 0 ? d.Note.ToString() : string.Empty) + program;
            return new NightProgram(day, d.Slots, deck, report, extras, sequence, windows, standing);
        }

        /// <summary>
        /// 104차(사용자: 「같은 날 안에서 순서를 섞기 — 각 날의 마지막(클라이맥스)만 고정」): 그날 날짜표를 섞되 클라이맥스(<see cref="ClimaxOf"/>)는 맨 뒤.
        /// </summary>
        private List<string> ShuffledOrder(int day)
        {
            List<string> list = FixedEncounters(day);
            string climax = ClimaxOf(day);
            bool hasClimax = climax != null && list.Remove(climax);
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = _rng.Next(i + 1);
                string t = list[i];
                list[i] = list[j];
                list[j] = t;
            }

            if (hasClimax) list.Add(climax);
            return list;
        }

        /// <summary>104차: 그날의 클라이맥스(맨 뒤에 고정) — 2일차 시체 낙하 · 3일차 발소리 · 4일차 소년 머리 박기 · 5일차 도서관 노란 얼굴.</summary>
        public static string ClimaxOf(int day)
        {
            switch (day)
            {
                case 2: return ProgramCatalog.CeilingLegs;
                case 3: return ProgramCatalog.Footsteps;
                case 4: return ProgramCatalog.BoyBang;
                case 5: return ProgramCatalog.YellowFace;   // 104차(사용자: 「5일차 클라이맥스는 도서관 노란 얼굴 조우로」)
                default: return null;
            }
        }

        // ── 1일차 ──────────────────────────────────────────────

        private NightProgram BuildDay1(ProgramRequest request)
        {
            // 2026-10-01 사용자 결정: 몹은 2일차부터 — 1일차에는 조우가 없다(긴장의 기승전결).
            // 그래서 조우에 묶인 수칙(H1 사람 나무·C3 소년)도 쓰지 않고 혼자 서는 수칙만 둔다.
            // 57차: 고정 4장(H2·C4·S1·G1) → S1·G1 고정 + 후보(Day1Pool)에서 넷, 모두 여섯 장. 한 공간 두 장까지, 경비실 한 장, 손전등 한도.
            List<RuleDef> picked = new List<RuleDef> { ProgramCatalog.Rule("S1") };   // 51차: S3(모형을 비추지 마)는 2일차부터 — 1일차는 모형 위치를 익히는 날
            List<string> pool = new List<string>(Day1Pool);
            // 103차(편성 균형): 1일차도 감각 세 축을 한 장씩 — S1(배치)·G1(청각)에 없는 축(조도)부터 후보에서 고른다.
            FearAxis[] need = { FearAxis.Illuminance, FearAxis.Auditory, FearAxis.Layout };
            for (int a = 0; a < need.Length && picked.Count < RulesFor(1) - 1; a++)
            {
                bool has = need[a] == FearAxis.Auditory;   // G1
                for (int i = 0; i < picked.Count; i++) if (picked[i].HasAxis && picked[i].Axis == need[a]) has = true;
                if (has) continue;
                List<string> withAxis = pool.FindAll(id => { RuleDef x = ProgramCatalog.Rule(id); return x != null && x.HasAxis && x.Axis == need[a]; });
                if (withAxis.Count == 0) continue;
                string pickId = withAxis[_rng.Next(withAxis.Count)];
                pool.Remove(pickId);
                if (Day1Fits(picked, ProgramCatalog.Rule(pickId))) picked.Add(ProgramCatalog.Rule(pickId));
            }

            while (picked.Count < RulesFor(1) - 1 && pool.Count > 0)
            {
                string id = pool[_rng.Next(pool.Count)];
                pool.Remove(id);
                RuleDef r = ProgramCatalog.Rule(id);
                if (r == null || !Day1Fits(picked, r)) continue;
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

        private static bool Day1Fits(List<RuleDef> picked, RuleDef r)
        {
            if (r == null) return false;
            int sameSpace = 0, flash = 0;
            for (int i = 0; i < picked.Count; i++)
            {
                if (picked[i].Space == r.Space) sameSpace++;
                if (picked[i].UsesFlashlight) flash++;
            }

            if (sameSpace >= (r.Space == SpaceId.SecurityRoom ? 1 : 2)) return false;
            return !(r.UsesFlashlight && flash >= ProgramCatalog.FlashlightPerDay);
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
            // 84차: 창밖 노란 얼굴 → 도서관 노란 얼굴 — 날짜 사슬. 103차: 2·3일차 중 하루 창밖 · 4·5일차 중 하루 도서관.
            if (IsWindowFaceDay(day)) PlaceChain(d, ProgramCatalog.SuitMan);
            if (IsFaceDay(day)) PlaceChain(d, ProgramCatalog.YellowFace);
            bool cctv = day == _cctvDay && PlaceCctv(d);   // 103차: 겹침 조우

            // 2) 강제 수칙.
            List<string> forced = new List<string>();
            if (request.ForcedRules != null) forced.AddRange(request.ForcedRules);
            if (day == 2) forced.Add(ProgramCatalog.FirstParadoxRule);
            for (int i = 0; i < forced.Count; i++)
            {
                RuleDef r = ProgramCatalog.Rule(forced[i]);
                if (r == null) continue;
                if (ProgramCatalog.IsRetired(r.Id)) { d.Note.Append(r.Id).Append(" 강제 편성 실패(폐기한 수칙). "); continue; }   // 79차

                // 조우에 묶인 수칙은 혼자 들어가면 방아쇠가 오지 않는다 — 그 조우를 함께 건다(2일차 C2 → 천장 다리).
                if (!r.IsStandalone)
                {
                    if (r.BoundEncounter == ProgramCatalog.CctvPerson)   // 103차: CCTV 사람은 겹침 조우로(회피 불가 K1·K2×K-1)
                    {
                        if (!cctv) cctv = PlaceCctv(d);
                        if (cctv && day < _cctvDay) _cctvDay = day;
                        if (!cctv) d.Note.Append(r.Id).Append(" 강제 편성 실패(경비실 충돌). ");
                        continue;
                    }

                    if (d.Has(r.Id)) continue;   // 70차: 이미 조우가 그 수칙을 들여놓았다(S5 = 복도 끝·급습 날)
                    EncounterDef bound = ProgramCatalog.Encounter(MergedInto(r.BoundEncounter));   // 103차: C3(소년 착석) → 머리 박기
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
                EncounterDef e = ProgramCatalog.Encounter(MergedInto(reserved[i]));
                if (e == null || Chosen(d, e.Id)) continue;
                if (IsChained(e.Id)) continue;   // 70차: 날짜 사슬로만
                if (e.Id == ProgramCatalog.CctvPerson) continue;   // 103차: 겹침 조우 날짜로만
                if (ProgramCatalog.IsEncounterRetired(e.Id)) continue;   // 79차: 대응 수칙을 폐기한 조우(L5 창밖 정장 남자)
                if (!CanPlaceEncounter(d, e)) { d.Note.Append(e.Id).Append(" 예약 편성 실패(공간 충돌). "); continue; }
                PlaceEncounter(d, e, true);
            }

            // 4) 슬롯 조우.
            int slotCount = day == 2 ? 2 : (SlotCAtStart(request) ? 3 : 2);
            int extrasTonight = (day == _corpseDay ? 1 : 0) + (cctv ? 1 : 0);   // 103차: 겹침 조우도 그날 조우 수에 센다
            // 103차: 창밖 노란 얼굴(수칙 없는 짧은 빌드업)은 슬롯 수·조우 수에 세지 않는다 — 슬롯 글자(A·B·C)는 셋까지.
            while (CountedSlots(d) < slotCount && d.Slots.Count < SlotLetters && CountedSlots(d) + extrasTonight < EncounterCap(day))
            {
                // 103차: 창밖 노란 얼굴 덕에 생긴 여분 칸은 가벼운 조우(강도 ≤ LightIntensity)만 — 2일차가 가장 무거운 밤이 되지 않게.
                EncounterDef pick = PickEncounter(d, day, request, d.Slots.Count >= slotCount ? LightIntensity : int.MaxValue);
                if (pick == null) { d.Note.Append("걸 수 있는 조우가 없어 슬롯 ").Append(d.Slots.Count).Append("개. "); break; }
                PlaceEncounter(d, pick, false);
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

            // 경비실 — 79차: K5(전화) 추가. 103차(편성 균형): 채움 뒤에, 덱에 가장 적은 축의 경비실 수칙을 먼저(K2 조도 · K3 배치 · K5 청각).
            if (d.In(SpaceId.SecurityRoom).Count == 0) Place(d, PickSecurity(d), false);

            EnsureLowestAxis(d, shown);
            EnsureEverySensoryAxis(d, shown);   // 103차(축 균형): 조도 단독 수칙이 덱에서 사라지던 것 — 감각 축마다 한 장씩

            List<RuleDef> deck = new List<RuleDef>();
            for (int s = 0; s < ProgramCatalog.RuleSpaces.Length; s++) deck.AddRange(d.In(ProgramCatalog.RuleSpaces[s]));
            deck.AddRange(d.In(SpaceId.SecurityRoom));
            deck.Add(ProgramCatalog.Rule("G1"));
            // 79차: G4는 짝수 날. 103차(편성 균형): 줄이기 전에 넣어 축 세기에 들어가게 한다(옛: 줄인 뒤 끝에 붙여 조도만 둘·배치 0장인 날이 생김).
            // 조우 수칙만으로 꽉 찬 날은 줄이기에서 G4가 먼저 빠진다.
            // 103차(사용자: 「앞선 날 수칙 반복 개선」): G4는 회차에 한 번 — 2일차에 나왔으면 4일차엔 넣지 않는다(2일차에 빠졌으면 4일차에).
            if (HasTabletRule(day) && !_runRules.Contains(TabletRule)) deck.Add(ProgramCatalog.Rule(TabletRule));
            TrimDeck(d, deck, LowestShown(shown), RulesFor(day));

            d.Slots.Sort((a, b) => a.Slot.CompareTo(b.Slot));
            List<EncounterDef> extras = new List<EncounterDef>();
            if (day == _corpseDay) extras.Add(ProgramCatalog.Encounter(ProgramCatalog.CeilingLegs));   // 59차: 시체 낙하는 회차에 한 번 — 2·3일차 중 고른 하루(겹침 조우)
            if (cctv) extras.Add(ProgramCatalog.Encounter(ProgramCatalog.CctvPerson));   // 103차
            NightProgram program = new NightProgram(day, d.Slots, deck, null, extras);
            return new NightProgram(day, d.Slots, deck, d.Note.Length > 0 ? d.Note + program.ToString() : program.ToString(), extras);
        }

        /// <summary>79차: 경비실 수칙 후보(그날 경비실에 조우 수칙이 없을 때 하나).</summary>
        public static readonly string[] SecurityPool = { "K2", "K3", "K5" };

        /// <summary>79차(민 채택 「태블릿을 보면서 걷지 마십시오.」): 공통 수칙 G4.</summary>
        public const string TabletRule = "G4";

        /// <summary>79차: G4가 드는 날 — 2·4일차(짝수 날). 하루 장수(<see cref="RulesFor"/>) 안에서 한 장을 차지한다.</summary>
        public static bool HasTabletRule(int day)
        {
            return day >= 2 && day % 2 == 0;
        }

        /// <summary>
        /// 57차: 하루 <see cref="RulesFor"/>장으로 줄인다 — 그날 점검이 없는 공간의 채움 → 다른 채움 → 묶이지 않은 경비실 순으로 뺀다(G1은 남긴다).
        /// 조우·강제 수칙(<c>Locked</c>)과 가장 낮은 축의 마지막 한 장은 빼지 않는다.
        /// </summary>
        private void TrimDeck(Draft d, List<RuleDef> deck, FearAxis lowest, int target)
        {
            while (deck.Count > target)
            {
                RuleDef drop = null;   // G1(복도에서 뛰지 마십시오)은 매일 남긴다 — 달리기 판정·시험이 기대는 공통 수칙
                List<RuleDef> can = new List<RuleDef>();
                for (int pass = 0; pass < 2 && drop == null; pass++)
                {
                    can.Clear();
                    for (int i = deck.Count - 1; i >= 0; i--)
                    {
                        RuleDef r = deck[i];
                        if (d.Locked.Contains(r.Id) || r.Space == SpaceId.SecurityRoom || r.Space == SpaceId.None) continue;
                        if (r.HasAxis && r.Axis == lowest && deck.FindAll(x => x.HasAxis && x.Axis == lowest).Count <= 1) continue;
                        if (r.HasAxis && r.Axis != FearAxis.Trust && deck.FindAll(x => x.HasAxis && x.Axis == r.Axis).Count <= 1) continue;   // 103차(축 균형): 감각 축마다 마지막 한 장은 남긴다
                        if (pass == 0 && CountIn(_planForFill, r.Space) > 0) continue;   // 먼저 그날 가지 않는 공간의 수칙
                        can.Add(r);
                    }

                    // 103차(민: 「특정 수칙들이 동시에 세트로 나오는 현상」): 옛날엔 늘 덱 끝(도서관·과학실·화장실 쪽)부터 빼서
                    // 복도·교실 채움(H2·C4 …)만 남아 같은 조합이 되풀이됐다 — 뺄 수 있는 것 중에서 고르게 뽑는다.
                    // 103차(민: 「회차에 아직 안 나온 수칙을 남기도록」): 이번 회차에 이미 나온 수칙부터 뺀다 — S1·L7처럼 5일 내내 한 번도 안 보이던 수칙이 한 번씩은 나오게.
                    List<RuleDef> seenBefore = can.FindAll(x => _runRules.Contains(x.Id));
                    if (seenBefore.Count > 0) drop = seenBefore[_rng.Next(seenBefore.Count)];
                    else if (can.Count > 0) drop = can[_rng.Next(can.Count)];
                }

                // 103차 QA: 경비실 수칙(2일차부터 하루 1장)보다 G4(회차에 한 번 · 짝수 날)를 먼저 뺀다 — 빠진 G4는 4일차에 나온다.
                // 옛 순서(경비실 먼저)로는 2일차 덱의 27%에 경비실 수칙이 없었다(소년 C2·C3 + G1·G4 + 창밖 노란 얼굴 날의 화장실 여자아이 T4).
                if (drop == null) drop = deck.Find(r => r.Id == TabletRule && !d.Locked.Contains(r.Id) && !LastOfAxis(deck, r));
                if (drop == null)
                {
                    // 103차(편성 균형): 경비실 수칙도 그 축의 마지막 한 장이면 먼저 피한다(K3 배치·K2 조도 — 옛: 경비실을 빼서 배치 0장인 날이 생김).
                    RuleDef k = deck.Find(r => r.Space == SpaceId.SecurityRoom && !d.Locked.Contains(r.Id) && !LastOfAxis(deck, r));
                    drop = k;
                }

                // 79차: 조우·고정 수칙만으로 장수가 꽉 찼으면 가장 낮은 축의 마지막 채움도 뺀다(하루 장수가 우선).
                // 103차(축 균형): 마지막 대체 경로도 감각 축마다 마지막 한 장은 먼저 피한다 — 그래도 없으면 옛 규칙.
                if (drop == null) drop = deck.FindLast(r => !d.Locked.Contains(r.Id) && r.Space != SpaceId.SecurityRoom && r.Space != SpaceId.None
                                                     && !(r.HasAxis && r.Axis != FearAxis.Trust && deck.FindAll(x => x.HasAxis && x.Axis == r.Axis).Count <= 1));
                if (drop == null) drop = deck.Find(r => r.Id == TabletRule && !LastOfAxis(deck, r));   // 103차: G4는 축 보호 다음, 마지막 대체 경로 앞
                if (drop == null) drop = deck.Find(r => r.Space == SpaceId.SecurityRoom && !d.Locked.Contains(r.Id));
                if (drop == null) drop = deck.Find(r => r.Id == TabletRule);
                if (drop == null) drop = deck.FindLast(r => !d.Locked.Contains(r.Id) && r.Space != SpaceId.SecurityRoom && r.Space != SpaceId.None);

                if (drop == null) break;
                deck.Remove(drop);
                d.Note.Append(drop.Id).Append(" 뺌(하루 ").Append(target).Append("장). ");
            }
        }

        private static bool LastOfAxis(List<RuleDef> deck, RuleDef r)
        {
            return r.HasAxis && r.Axis != FearAxis.Trust && deck.FindAll(x => x.HasAxis && x.Axis == r.Axis).Count <= 1;
        }

        /// <summary>보너스 슬롯 C: 3일차부터, 밤 시작에 가장 높은 감각 축의 생존 수치가 49 이하일 때(긴장 디렉터 「잘하는 중」).</summary>
        private static bool SlotCAtStart(ProgramRequest request)
        {
            if (request.Day < 3 || request.Survival == null) return false;
            int max = Math.Max(request.Survival.GetValue(FearAxis.Auditory),
                Math.Max(request.Survival.GetValue(FearAxis.Illuminance), request.Survival.GetValue(FearAxis.Layout)));
            return max <= (request.Day >= LateSlotCDay ? LateSlotCMax : 49);
        }

        /// <summary>
        /// 103차(편성 상승감·미숙 플레이어 조우 목격): 이 날부터는 보너스 슬롯 C를 「위험」(75 이상)만 아니면 편성한다(3일차는 그대로 49 이하).
        /// 런타임이 다시 거른다 — 위험 분위기면 C를 걸지 않고(<c>TensionDirector.SlotOpen</c>), 재시작 k≥2면 끈다(장치 1).
        /// 옛: 미숙 플레이어는 4·5일차에 C가 자주 꺼져 조우 13갈래를 다 보는 회차가 21%, 5일차 조우가 2.2개로 3일차(4.0)보다 적었다.
        /// </summary>
        public const int LateSlotCDay = 4;

        /// <inheritdoc cref="LateSlotCDay"/>
        public const int LateSlotCMax = 74;

        /// <summary>
        /// 103차(민: 「날이 갈수록 변화를 명확히 인지」): 그날 조우 수 상한(슬롯 + 겹침 조우). 2·3일차 3 · 4·5일차 4.
        /// 옛: 겹침 조우(시체 낙하·창밖 노란 얼굴·CCTV 사람)가 2·3일차에 몰려 3일차 조우(3.9)가 5일차(2.9)보다 많았다.
        /// </summary>
        public static int EncounterCap(int day)
        {
            return day >= LateSlotCDay ? 4 : 3;
        }

        /// <summary>슬롯 글자 수(A·B·C).</summary>
        public const int SlotLetters = 3;

        /// <summary>103차: 조우 수 상한에 세는 슬롯 — 창밖 노란 얼굴(수칙 없는 짧은 빌드업)은 세지 않는다.</summary>
        private static int CountedSlots(Draft d)
        {
            int n = 0;
            for (int i = 0; i < d.Slots.Count; i++) if (d.Slots[i].Encounter.Id != ProgramCatalog.SuitMan) n++;
            return n;
        }

        /// <summary>103차: 이 날부터 조우 점수에 강도를 더한다 — 끝으로 갈수록 센 조우가 남는다.</summary>
        public const int ClimaxFromDay = 4;

        /// <summary>103차: 창밖 노란 얼굴이 낸 여분 칸에 들일 수 있는 조우 강도 상한.</summary>
        public const int LightIntensity = 2;

        private EncounterDef PickEncounter(Draft d, int day, ProgramRequest request, int maxIntensity = int.MaxValue)
        {
            SlotEncounter prev = d.Slots.Count > 0 ? d.Slots[d.Slots.Count - 1] : null;
            List<EncounterDef> best = new List<EncounterDef>();
            int bestScore = int.MinValue;

            IReadOnlyList<EncounterDef> all = ProgramCatalog.AllEncounters;
            for (int i = 0; i < all.Count; i++)
            {
                EncounterDef e = all[i];
                if (Chosen(d, e.Id)) continue;
                if (e.Id == ProgramCatalog.CeilingLegs || e.Id == ProgramCatalog.CctvPerson) continue;
                if (MergedInto(e.Id) != e.Id) continue;   // 103차: 소년 착석은 머리 박기의 앞부분 — 따로 뽑지 않는다
                if (e.Intensity > maxIntensity) continue;
                if (FamilySeen(e)) continue;   // 103차(민: 「하나의 연출로」): 회차에 본 조우(같은 갈래 포함)는 평소 뽑기에서 다시 걸지 않는다 — 강제·예약만 예외   // 59차: 시체 낙하는 슬롯 밖 겹침 조우(Extras)로 늘 건다 · 103차: CCTV 사람도
                if (IsChained(e.Id)) continue;   // 70차: 복도 끝에 선 자·모형 급습은 날짜 사슬로만
                if (ProgramCatalog.IsEncounterRetired(e.Id)) continue;   // 79차: L5 폐기 → 창밖 정장 남자도 걸지 않는다
                if (!e.Satisfied(request.Shown)) continue;
                if (!SpecialOk(e, day, request)) continue;
                if (prev != null && prev.Encounter.Mob == e.Mob) continue;
                if (!CanPlaceEncounter(d, e)) continue;
                if (!WithinBudget(d, e, day)) continue;
                if (!FitsDeck(d, e, day)) continue;   // 103차: 고정 수칙(조우·강제·CCTV 겹침 K1)이 하루 장수를 넘지 않게(G1 한 장 자리)
                if (SharesRule(d, e)) continue;   // 103차: 같은 수칙을 쓰는 조우(과학실·화장실 소등 · 소년 착석·머리 박기)는 한 밤에 하나

                int score = 2 * (int)request.Shown.GetBand(e.Axis)
                            + (FamilySeen(e) ? 0 : UnseenBonus)
                            + (AxisGap(d, e) ? AxisGapBonus : 0)
                            + (e.IsCross ? 2 : 0)
                            - (prev != null && prev.Encounter.Axis == e.Axis ? 2 : 0)
                            + (IsDayFocus(day, e.Id) ? DayFocusBonus : 0)
                            + (day >= ClimaxFromDay ? e.Intensity : 0)
                            + _rng.Next(EncounterJitter + 1);   // 103차: 회차마다 같은 날 같은 조우만 나오지 않게

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

        /// <summary>그날 무게를 둘 조우에 더하는 점수(51차 분배). 103차(편성 다양성): 4 → 3.</summary>
        public const int DayFocusBonus = 3;

        /// <summary>103차(민: 「너무 중복되지 않으며」): 조우 점수에 더하는 흔들림(0~이 값) — 옛: 3일차는 화장실 여자아이·발소리가 거의 늘 같이 나왔다.</summary>
        public const int EncounterJitter = 2;

        /// <summary>
        /// 회차에 아직 안 본 조우에 더하는 점수. 103차(민: 「모든 조우 연출을 목격할 수 있도록」): 3 → 9 — 구간·날짜 무게(최대 8+4)보다 앞서
        /// 같은 조우가 되풀이되는 일을 막는다(옛: 회차당 조우 15개 중 약 11개, 화장실 여자아이·소년이 이틀씩 나옴).
        /// </summary>
        public const int UnseenBonus = 9;

        /// <summary>103차(편성 균형): 대응 수칙의 축이 아직 그날 덱에 없는 조우에 더하는 점수(5일차 배치 0장 → 없던 문).</summary>
        public const int AxisGapBonus = 3;

        private static bool AxisGap(Draft d, EncounterDef e)
        {
            string id = e.ResponseRule.Length > 0 ? e.ResponseRule : e.SecondRule;
            RuleDef r = ProgramCatalog.Rule(id);
            return r != null && r.HasAxis && r.Axis != FearAxis.Trust && CountAxis(d, r.Axis) == 0;
        }

        /// <summary>103차: 회차에 그 조우나 같은 수칙을 쓰는 조우(과학실·화장실 소등 · 소년 착석·머리 박기)를 이미 봤는지.</summary>
        private bool FamilySeen(EncounterDef e)
        {
            if (_seen.Contains(e.Id)) return true;
            foreach (string id in _seen)
            {
                EncounterDef o = ProgramCatalog.Encounter(id);
                if (o == null) continue;
                if (Same(e.ResponseRule, o.ResponseRule) || Same(e.ResponseRule, o.SecondRule) || Same(e.SecondRule, o.ResponseRule) || Same(e.SecondRule, o.SecondRule)) return true;
            }

            return false;
        }

        private bool FitsDeck(Draft d, EncounterDef e, int day)
        {
            int locked = 0;
            foreach (KeyValuePair<SpaceId, List<RuleDef>> kv in d.Spaces)
            {
                for (int i = 0; i < kv.Value.Count; i++) if (d.Locked.Contains(kv.Value[i].Id)) locked++;
            }

            List<RuleDef> rules = RulesOf(e);
            for (int i = 0; i < rules.Count; i++) if (!d.Has(rules[i].Id)) locked++;
            return locked <= RulesFor(day) - 1;
        }

        private static bool SharesRule(Draft d, EncounterDef e)
        {
            for (int i = 0; i < d.Slots.Count; i++)
            {
                EncounterDef o = d.Slots[i].Encounter;
                if (Same(e.ResponseRule, o.ResponseRule) || Same(e.ResponseRule, o.SecondRule) || Same(e.SecondRule, o.ResponseRule) || Same(e.SecondRule, o.SecondRule)) return true;
            }

            return false;
        }

        private static bool Same(string a, string b)
        {
            return a.Length > 0 && a == b;
        }

        private RuleDef PickSecurity(Draft d)
        {
            int min = int.MaxValue;
            for (int i = 0; i < SecurityPool.Length; i++)
            {
                RuleDef r = ProgramCatalog.Rule(SecurityPool[i]);
                if (r != null && r.HasAxis) min = Math.Min(min, CountAxis(d, r.Axis));
            }

            List<RuleDef> c = new List<RuleDef>();
            List<int> w = new List<int>();
            for (int i = 0; i < SecurityPool.Length; i++)
            {
                RuleDef r = ProgramCatalog.Rule(SecurityPool[i]);
                if (r == null) continue;
                c.Add(r);
                w.Add((r.HasAxis && CountAxis(d, r.Axis) == min ? 3 : 1) * (_lastDeck.Contains(r.Id) ? 1 : 2));
            }

            return Weighted(c, w);
        }

        /// <summary>
        /// 103차(편성 다양성·변조): 지난 밤에 있던 채움은 덜(1), 처음 보는 채움은 보통(3), <see cref="ParadoxPlanner.TamperLateDay"/>(103차 3일차)부터는 이틀 전 이전에 본 변조본 있는 채움을 조금 더(4) —
        /// 변조는 「이전 밤에 원본을 본 수칙」만 거는데, 덱이 고르게 바뀌면서 4일차 변조 후보가 모자랐다.
        /// </summary>
        private int RecencyWeight(RuleDef r)
        {
            if (_lastDeck.Contains(r.Id)) return 1;
            if (_buildDay >= ParadoxPlanner.TamperLateDay && _runRules.Contains(r.Id))
            {
                ParadoxEntry e = ParadoxCatalog.Find(r.Id);
                if (e != null && e.HasVariant) return 4;
            }

            return 3;
        }

        private RuleDef Weighted(List<RuleDef> c, List<int> w)
        {
            if (c.Count == 0) return null;
            int sum = 0;
            for (int i = 0; i < w.Count; i++) sum += w[i];
            int roll = _rng.Next(sum);
            for (int i = 0; i < c.Count; i++)
            {
                roll -= w[i];
                if (roll < 0) return c[i];
            }

            return c[c.Count - 1];
        }

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
                    return encounterId == ProgramCatalog.CeilingLegs || encounterId == ProgramCatalog.SuitMan || encounterId == ProgramCatalog.BoyBang;   // 84차: 노란 얼굴은 3일차(사슬) — 2일차는 창밖
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

        /// <summary>
        /// 103차(민: 「소년 착석과 머리 박기는 합쳐진 조우야. 소년이 착석하고, 머리를 박는 걸로 하나의 연출로」) — 편성에서 그 조우를 대신하는 조우.
        /// 소년 착석은 머리 박기(<see cref="ProgramCatalog.BoyBang"/>, 앉음 → 바라보면 박기 · C2·C3 두 장)로만 들어간다.
        /// 착석 정의·대본은 남겨 둔다(디버그 강제·연출 시험).
        /// </summary>
        public static string MergedInto(string encounterId)
        {
            return encounterId == ProgramCatalog.BoySeated ? ProgramCatalog.BoyBang : encounterId;
        }

        /// <summary>70차: 날짜 사슬로만 들어가는 조우(복도 끝에 선 자 → 다음 날 모형 급습).</summary>
        public static bool IsChained(string encounterId)
        {
            return encounterId == ProgramCatalog.HallEndFigure || encounterId == ProgramCatalog.ModelRush
                   || encounterId == ProgramCatalog.SuitMan || encounterId == ProgramCatalog.YellowFace;   // 84차
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
                weights.Add((r.Axis == highest ? 2 : 1) * RecencyWeight(r));
            }

            return Weighted(candidates, weights);
        }

        /// <summary>가장 낮은 감각 축(연출 구간, 동점이면 생존 수치가 낮은 쪽)의 수칙이 한 장도 없으면 채움 한 장을 그 축으로 바꾼다.</summary>
        /// <summary>
        /// 103차(축 균형): 감각 축(청각·조도·배치)마다 수칙이 한 장도 없으면, 같은 축이 둘 이상인 채움 한 장을 그 축 채움으로 바꾼다(조우·강제 수칙은 그대로).
        /// 옛: 조우 수칙·G1·G4로 덱이 차서 조도 단독 수칙(C4·S3·L6)이 거의 남지 않았다 — 조도가 1~3일차에 움직이지 않다 4·5일차에 몰아서 올랐다.
        /// </summary>
        private void EnsureEverySensoryAxis(Draft d, IFearAxisReader shown)
        {
            FearAxis highest = HighestShown(shown);
            FearAxis[] axes = { FearAxis.Illuminance, FearAxis.Auditory, FearAxis.Layout };
            for (int a = 0; a < axes.Length; a++)
            {
                FearAxis want = axes[a];
                if (CountAxisInDeck(d, want) > 0) continue;   // 103차 QA: 공통 G1(청각)·짝수 날 G4(배치)도 센다 — 옛: 청각이 있는데도 채움 한 장을 청각으로 바꿨다
                bool fixedIt = false;
                for (int s = 0; s < ProgramCatalog.RuleSpaces.Length && !fixedIt; s++)
                {
                    SpaceId space = ProgramCatalog.RuleSpaces[s];
                    List<RuleDef> here = d.In(space);
                    if (here.Count != 1 || d.Locked.Contains(here[0].Id)) continue;
                    RuleDef old = here[0];
                    if (old.HasAxis && old.Axis != FearAxis.Trust && CountAxisInDeck(d, old.Axis) <= 1) continue;   // 다른 축의 마지막 한 장은 건드리지 않는다
                    here.Clear();
                    RuleDef swap = PickFiller(d, space, highest, want);
                    if (swap != null)
                    {
                        here.Add(swap);
                        fixedIt = true;
                        break;
                    }

                    here.Add(old);
                }
            }
        }

        /// <summary>103차 QA: 덱에 들어갈 공통 수칙(G1 · 그날 들어갈 G4)까지 센 축 장수.</summary>
        private int CountAxisInDeck(Draft d, FearAxis axis)
        {
            int n = CountAxis(d, axis);
            RuleDef g1 = ProgramCatalog.Rule("G1");
            if (g1 != null && g1.HasAxis && g1.Axis == axis) n++;
            if (HasTabletRule(_buildDay) && !_runRules.Contains(TabletRule))
            {
                RuleDef g4 = ProgramCatalog.Rule(TabletRule);
                if (g4 != null && g4.HasAxis && g4.Axis == axis) n++;
            }

            return n;
        }

        private static int CountAxis(Draft d, FearAxis axis)
        {
            int n = 0;
            foreach (KeyValuePair<SpaceId, List<RuleDef>> kv in d.Spaces)
            {
                for (int i = 0; i < kv.Value.Count; i++) if (kv.Value[i].HasAxis && kv.Value[i].Axis == axis) n++;
            }

            return n;
        }

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

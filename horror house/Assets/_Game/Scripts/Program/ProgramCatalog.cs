using System;
using System.Collections.Generic;

namespace NightDuty
{
    /// <summary>
    /// 새 근무수칙 한 장의 정의(2026-09-30 최종 기획서 「공간별 설계」·「수칙과 덱」). <b>편성용 데이터</b>다 —
    /// 판정 조건(<see cref="RuleSO"/>의 조건 조합)은 6단계 후반에 수칙마다 붙인다.
    /// </summary>
    public sealed class RuleDef
    {
        /// <summary>수칙 ID(「H1」·「G1」·「K4」).</summary>
        public readonly string Id;

        /// <summary>공간. 공통 수칙은 None.</summary>
        public readonly SpaceId Space;

        /// <summary>위반 때 오르는 축. K4·G3처럼 축이 없으면 <see cref="HasAxis"/>가 false.</summary>
        public readonly FearAxis Axis;

        /// <summary>축이 있는지(K4·G3은 없음).</summary>
        public readonly bool HasAxis;

        /// <summary>태블릿 문구(공문 말투 한 문장).</summary>
        public readonly string Text;

        /// <summary>2026-11-22: 태블릿에 보일 글 — <see cref="Text"/>(한국어 정본)의 지금 언어 글. 판정·기록은 <see cref="Text"/>를 쓴다.</summary>
        public string ShownText
        {
            get { return Loc.Dt(Id, "Text", Text); }
        }

        /// <summary>위협 수칙(어기면 +20만, 지키면 신뢰 +3만).</summary>
        public readonly bool IsThreat;

        /// <summary>손전등 수칙(하루 3장까지).</summary>
        public readonly bool UsesFlashlight;

        /// <summary>대기형 수칙(1일차 채움에서 빠진다).</summary>
        public readonly bool IsWaitType;

        /// <summary>
        /// 이 수칙을 방아쇠로 쓰는 조우 ID. 비어 있으면 <b>혼자 서는 수칙</b>(조우 없이도 방아쇠가 온다)이라 빈칸 채움에 쓸 수 있다.
        /// 값이 있으면 그 조우가 편성된 날에만 덱에 든다 — 조우 없이 들어가면 방아쇠가 오지 않는 헛자리다.
        /// </summary>
        public readonly string BoundEncounter;

        /// <summary>정의를 만든다.</summary>
        public RuleDef(string id, SpaceId space, FearAxis axis, bool hasAxis, string text,
            bool isThreat = false, bool usesFlashlight = false, bool isWaitType = false, string boundEncounter = "")
        {
            Id = id;
            Space = space;
            Axis = axis;
            HasAxis = hasAxis;
            Text = text;
            IsThreat = isThreat;
            UsesFlashlight = usesFlashlight;
            IsWaitType = isWaitType;
            BoundEncounter = boundEncounter ?? string.Empty;
        }

        /// <summary>혼자 서는 수칙인지.</summary>
        public bool IsStandalone
        {
            get { return BoundEncounter.Length == 0; }
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return Id;
        }
    }

    /// <summary>조우 한 개의 정의(최종 기획서 「조우와 대응 수칙」). 모든 조우는 대응 수칙이 하나씩 정해져 있다.</summary>
    public sealed class EncounterDef
    {
        /// <summary>조우 ID.</summary>
        public readonly string Id;

        /// <summary>이름(제작용).</summary>
        public readonly string Name;

        /// <summary>주축.</summary>
        public readonly FearAxis Axis;

        /// <summary>대응 수칙의 공간(「과학실 앞 복도」의 S5는 과학실).</summary>
        public readonly SpaceId Space;

        /// <summary>대응 수칙 ID. 빈 문자열이면 수칙 없이 놀람만 주는 조우(52차 시체 낙하).</summary>
        public readonly string ResponseRule;

        /// <summary>교차 조우의 두 번째 대응 수칙(소년 머리 박기 = C2 + C3). 없으면 빈 문자열.</summary>
        public readonly string SecondRule;

        /// <summary>강도 1~5(놀람 예산).</summary>
        public readonly int Intensity;

        /// <summary>교차 조우인지(두 축 조건).</summary>
        public readonly bool IsCross;

        /// <summary>몹(직전 슬롯과 같은 몹은 제외). 천장 다리도 소년 모델이라 「boy」.</summary>
        public readonly string Mob;

        /// <summary>발동 조건 — 축별 연출 구간 하한. 모두 만족해야 한다.</summary>
        public readonly KeyValuePair<FearAxis, Band>[] Requires;

        /// <summary>정의를 만든다.</summary>
        public EncounterDef(string id, string name, FearAxis axis, SpaceId space, string responseRule, int intensity,
            string mob, KeyValuePair<FearAxis, Band>[] requires, bool isCross = false, string secondRule = "")
        {
            Id = id;
            Name = name;
            Axis = axis;
            Space = space;
            ResponseRule = responseRule ?? string.Empty;
            Intensity = intensity;
            Mob = mob;
            Requires = requires ?? new KeyValuePair<FearAxis, Band>[0];
            IsCross = isCross;
            SecondRule = secondRule ?? string.Empty;
        }

        /// <summary>연출 구간이 발동 조건을 만족하는지.</summary>
        public bool Satisfied(IFearAxisReader shown)
        {
            for (int i = 0; i < Requires.Length; i++)
            {
                if (shown.GetBand(Requires[i].Key) < Requires[i].Value) return false;
            }

            return true;
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return Id;
        }
    }

    /// <summary>
    /// 새 수칙 28장 + 공통 G1·G3(G2 「들린 것만 기록하십시오.」는 2026-10-04 민 결정으로 폐기), 조우 15개. <b>편성 데이터의 구현값 정본</b>이다 — 기획 정본은 최종 기획서이고,
    /// 문구·축·위협 여부가 바뀌면 여기를 고친다. 옛 24장(<see cref="RuleSO"/> 에셋)은 6단계 후반에 교체한다.
    /// </summary>
    public static class ProgramCatalog
    {
        // ── 조우 ID ─────────────────────────────────────────────
        public const string BoySeated = "120001";
        public const string BoyBang = "120002";
        public const string ToiletGirl = "120003";
        public const string Footsteps = "120004";

        /// <summary>104차(사용자): 뒤에서 따라오는 발소리(2일차 청각 조우) — 수칙 H5. 뒤돌아봄 2번이면 위반, 10초 무시하거나 멈추면 소리가 사라진다.</summary>
        public const string FollowSteps = "120016";
        public const string CallingVoice = "120005";
        public const string YellowFace = "120006";
        public const string HallEndFigure = "120007";
        public const string ModelRush = "120008";
        public const string ScienceBlackout = "120009";
        public const string ToiletBlackout = "120010";
        public const string PeopleTree = "120011";
        public const string CeilingLegs = "120012";
        public const string PhantomDoor = "120013";
        public const string SuitMan = "120014";
        public const string CctvPerson = "120015";

        /// <summary>2일차 첫 역설이 겨누는 수칙(C2 — 소년이 앉은 뒤 「교실에 남은 학생이 있습니다. 지금 확인하십시오.」, 눈으로만).</summary>
        public const string FirstParadoxRule = "110006";

        /// <summary>104차: 첫 역설(C2)이 나오는 날 — 고정 편성(<see cref="ProgramDirector.FixedSchedule"/>)은 소년 머리 박기 날(4일차), 옛 편성은 2일차.</summary>
        public static int FirstParadoxDay
        {
            get { return ProgramDirector.FixedSchedule ? ProgramDirector.FixedFirstParadoxDay : 2; }
        }

        /// <summary>
        /// 편성하지 않는 수칙(정의는 남겨 둔다 — 판정기·역설 표·시험이 참조). 60차 민: 「교실의 판서 근무 수칙은 폐기해줘. 별로인 것 같아」 → C1.
        /// </summary>
        public static readonly string[] Retired = RuleIdsWhere(r => r.State_ID != ActiveState);   // DT 상태 「폐기」(C1·C5·T3·T5·L5)   // 79차 민: 「S4 T3 합침」·「T5 폐기」·「L5 폐기」 · 104차(사용자: 「C5는 폐기 — 너무 애매해」) → 없던 문 조우도 함께 빠짐(IsEncounterRetired)

        /// <summary>편성하지 않는 수칙인지.</summary>
        public static bool IsRetired(string ruleId)
        {
            return ruleId != null && System.Array.IndexOf(Retired, ruleId) >= 0;
        }

        /// <summary>
        /// 79차: 대응 수칙이 폐기된 조우(창밖 정장 남자 — L5 폐기)는 편성하지 않는다. 대응 수칙이 없는 조우(시체 낙하)는 해당하지 않는다.
        /// </summary>
        public static bool IsEncounterRetired(string encounterId)
        {
            EncounterDef e = Encounter(encounterId);
            return e != null && e.ResponseRule.Length > 0 && IsRetired(e.ResponseRule);
        }

        /// <summary>5일차 경비실 고정 수칙(04:00 뒤에만 판정).</summary>
        public const string FinaleRule = "110032";

        /// <summary>5일차 태블릿에만 보이는 공통 수칙(판정 없음). 피날레 「들어왔다」에서 빈칸이 채워진다.</summary>
        public const string FinaleBlankRule = "110036";

        /// <summary>빈칸이 채워진 G3 문구(최종 기획서: 빈칸은 「당신」).</summary>
        public const string FinaleBlankFilled = "이 수칙서와 태블릿 공지가 다를 경우 당신을 따르십시오.";

        /// <summary>2026-11-22: 채워진 G3의 지금 언어 글 — DT G3 행 Blank_Decoded의 번역(한국어면 <see cref="FinaleBlankFilled"/>).</summary>
        public static string FinaleBlankFilledText
        {
            get { return Loc.Dt(FinaleBlankRule, "Blank_Decoded", FinaleBlankFilled); }
        }

        /// <summary>역보고 수칙(회차당 최대 2번).</summary>
        public const string ReverseReportRule = "110019";

        /// <summary>회차당 역보고 수칙 배정 한도.</summary>
        public const int ReverseReportPerRun = 2;

        /// <summary>
        /// 103차(사용자: 「교실에 1분 넘게 머물지 마십시오 — 게임 시간으로 표기해서 실제 1분」): C6 문구의 게임 분. 실제 1초 = 게임 24초(<see cref="NightClock.GameSecondsPerRealSecond"/>)라 24분 = 실제 60초.
        /// 판정 시간은 <see cref="ClassroomDwellRealSeconds"/>.
        /// </summary>
        public const int ClassroomDwellGameMinutes = 24;

        /// <summary>C6 판정 시간(실제 초) — 문구의 게임 분을 실제 초로 바꾼 값(60초).</summary>
        public static float ClassroomDwellRealSeconds => ClassroomDwellGameMinutes * 60f / NightClock.GameSecondsPerRealSecond;

        /// <summary>2026-11-06(1·2일차 밤이 길다): 그날 배속으로 바꾼 C6 판정 시간(1일차 90 · 2일차 75 · 그 뒤 60초) — 문구 「24분」은 그대로 맞는다.</summary>
        public static float ClassroomDwellRealSecondsFor(int day)
        {
            return ClassroomDwellGameMinutes * 60f / NightClock.GameSecondsPerRealSecondFor(day);
        }

        /// <summary>하루 손전등 수칙 한도.</summary>
        public static int FlashlightPerDay => global::NightDuty.DesignData.SettingInt(390063);   // DT 390063 손전등 수칙 하루 한도

        private static KeyValuePair<FearAxis, Band> Need(FearAxis axis, Band band)
        {
            return new KeyValuePair<FearAxis, Band>(axis, band);
        }

        private const FearAxis A = FearAxis.Auditory;
        private const FearAxis I = FearAxis.Illuminance;
        private const FearAxis L = FearAxis.Layout;
        private const FearAxis T = FearAxis.Trust;

        /// <summary>DT 수칙 종류 — 위협(520202) · 장식 문구(520205, 가짜 수칙 — <see cref="FakeRuleCatalog"/>).</summary>
        private const uint ThreatKind = 520202, DecorKind = 520205;

        /// <summary>DT 수칙 상태 — 사용(510201). 그 밖(폐기)은 <see cref="Retired"/>.</summary>
        private const uint ActiveState = 510201;

        /// <summary>수칙 → 묶인 조우. 조우 키는 아직 코드(Encounter 단계에서 표로 옮김).</summary>
        private static readonly Dictionary<string, string> BoundOf = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "110001", PeopleTree },      // H1 — 2026-10-01: 사람 나무가 없으면 피할 물체가 없다
            { "110003", Footsteps },       // H3
            { "110004", CallingVoice },    // H4
            { "110037", FollowSteps },     // H5
            { "110006", BoyBang },         // C2 — 52차 민: 앉은 소년과 묶음(C2 + C3 + 소년)
            { "110007", BoySeated },       // C3
            { "110009", PhantomDoor },     // C5
            { "110014", ScienceBlackout }, // S4
            { "110015", HallEndFigure },   // S5
            { "110018", ToiletBlackout },  // T3
            { "110019", ToiletGirl },      // T4
            { "110024", YellowFace },      // L3
            { "110026", SuitMan },         // L5
            { "110029", CctvPerson },      // K1
        };

        /// <summary>DT 3단계: 수칙은 Rule_Table(장식 문구 제외)에서 읽는다 — 문구·공간·축·위협·손전등·대기형. 정본은 xlsx.</summary>
        private static readonly RuleDef[] Rules = BuildRules();

        private static RuleDef[] BuildRules()
        {
            List<InstructionDT.RuleRow> rows = RuleRows();
            RuleDef[] list = new RuleDef[rows.Count];
            for (int i = 0; i < rows.Count; i++)
            {
                InstructionDT.RuleRow r = rows[i];
                string id = r.ID.ToString();
                string bound;
                BoundOf.TryGetValue(id, out bound);
                list[i] = new RuleDef(id, global::NightDuty.DesignData.SpaceOf(r.Space_ID), r.Axis_ID == 0 ? L : global::NightDuty.DesignData.AxisOf(r.Axis_ID), r.Axis_ID != 0, r.Text,
                    isThreat: r.Kind_ID == ThreatKind, usesFlashlight: r.Is_Flashlight, isWaitType: r.Is_Wait, boundEncounter: bound ?? string.Empty);
            }

            return list;
        }

        /// <summary>판정 수칙 행(표 순서, 장식 문구 제외). H5(110037, 표 끝에 추가)는 옛 순서대로 H4 뒤.</summary>
        internal static List<InstructionDT.RuleRow> RuleRows()
        {
            global::NightDuty.DesignData.Ensure();
            List<InstructionDT.RuleRow> rows = new List<InstructionDT.RuleRow>();
            foreach (InstructionDT.RuleRow r in InstructionDT.DT.Rule.All)
                if (r.Kind_ID != DecorKind) rows.Add(r);
            int h5 = rows.FindIndex(r => r.ID == 110037), h4 = rows.FindIndex(r => r.ID == 110004);
            if (h5 >= 0 && h4 >= 0 && h5 != h4 + 1)
            {
                InstructionDT.RuleRow t = rows[h5];
                rows.RemoveAt(h5);
                rows.Insert(rows.FindIndex(r => r.ID == 110004) + 1, t);
            }

            return rows;
        }

        /// <summary>장식 문구 행(가짜 수칙, 표 순서).</summary>
        internal static List<InstructionDT.RuleRow> DecorRows()
        {
            global::NightDuty.DesignData.Ensure();
            List<InstructionDT.RuleRow> rows = new List<InstructionDT.RuleRow>();
            foreach (InstructionDT.RuleRow r in InstructionDT.DT.Rule.All)
                if (r.Kind_ID == DecorKind) rows.Add(r);
            return rows;
        }

        /// <summary>표의 칸이 참인 수칙 ID(표 순서).</summary>
        internal static string[] RuleIdsWhere(Func<InstructionDT.RuleRow, bool> pick)
        {
            List<string> ids = new List<string>();
            foreach (InstructionDT.RuleRow r in RuleRows())
                if (pick(r)) ids.Add(r.ID.ToString());
            return ids.ToArray();
        }

        /// <summary>조우의 몹(대역) 이름과 편성 조건 띠 — 표에 칸이 없어 코드에 둔다.</summary>
        private static readonly Dictionary<string, KeyValuePair<string, KeyValuePair<FearAxis, Band>[]>> CastOf = new Dictionary<string, KeyValuePair<string, KeyValuePair<FearAxis, Band>[]>>(StringComparer.Ordinal)
        {
            { BoySeated, Cast("boy", Need(A, Band.Band0)) },
            { BoyBang, Cast("boy", Need(A, Band.Band2), Need(L, Band.Band2)) },
            { ToiletGirl, Cast("girl", Need(A, Band.Band1)) },
            { Footsteps, Cast("footsteps", Need(A, Band.Band1)) },   // 103차: 청각 2 → 1
            { FollowSteps, Cast("footsteps", Need(A, Band.Band0)) },
            { CallingVoice, Cast("voice", Need(A, Band.Band3)) },
            { YellowFace, Cast("duck", Need(I, Band.Band1)) },
            { HallEndFigure, Cast("dummy", Need(I, Band.Band2)) },
            { ModelRush, Cast("dummy", Need(I, Band.Band3), Need(L, Band.Band2)) },
            { ScienceBlackout, Cast("science.dark", Need(I, Band.Band1)) },
            { ToiletBlackout, Cast("toilet.dark", Need(I, Band.Band1)) },
            { PeopleTree, Cast("tree", Need(L, Band.Band2)) },
            { CeilingLegs, Cast("boy", Need(L, Band.Band1)) },
            { PhantomDoor, Cast("door", Need(L, Band.Band2)) },
            { SuitMan, Cast("duck", Need(I, Band.Band1)) },
            { CctvPerson, Cast("blackman") },
        };

        private static KeyValuePair<string, KeyValuePair<FearAxis, Band>[]> Cast(string mob, params KeyValuePair<FearAxis, Band>[] needs)
        {
            return new KeyValuePair<string, KeyValuePair<FearAxis, Band>[]>(mob, needs);
        }

        /// <summary>DT 3단계: 조우는 Encounter_Table에서 읽는다 — 이름·축·수칙 공간·대응 수칙 1·2·강도·겹침. 정본은 xlsx.</summary>
        private static readonly EncounterDef[] Encounters = BuildEncounters();

        private static EncounterDef[] BuildEncounters()
        {
            List<InstructionDT.EncounterRow> rows = EncounterRows();
            EncounterDef[] list = new EncounterDef[rows.Count];
            for (int i = 0; i < rows.Count; i++)
            {
                InstructionDT.EncounterRow r = rows[i];
                string id = r.ID.ToString();
                KeyValuePair<string, KeyValuePair<FearAxis, Band>[]> cast;
                if (!CastOf.TryGetValue(id, out cast)) throw new InvalidOperationException("DT Encounter " + id + " 몹·조건 띠가 코드(CastOf)에 없음");
                list[i] = new EncounterDef(id, r.Name, global::NightDuty.DesignData.AxisOf(r.Main_Axis_ID), global::NightDuty.DesignData.SpaceOf(r.Rule_Space_ID),
                    r.Rule_1_ID == 0 ? string.Empty : r.Rule_1_ID.ToString(), (int)r.Intensity, cast.Key, cast.Value, r.Is_Cross, r.Rule_2_ID == 0 ? string.Empty : r.Rule_2_ID.ToString());
            }

            return list;
        }

        /// <summary>조우 행(표 순서). 따라오는 발소리(120016, 표 끝에 추가)는 옛 순서대로 달려오는 발소리 뒤.</summary>
        internal static List<InstructionDT.EncounterRow> EncounterRows()
        {
            global::NightDuty.DesignData.Ensure();
            List<InstructionDT.EncounterRow> rows = new List<InstructionDT.EncounterRow>(InstructionDT.DT.Encounter.All);
            int f = rows.FindIndex(r => r.ID == 120016);
            if (f >= 0)
            {
                InstructionDT.EncounterRow t = rows[f];
                rows.RemoveAt(f);
                rows.Insert(rows.FindIndex(r => r.ID == 120004) + 1, t);
            }

            return rows;
        }

        private static readonly Dictionary<string, RuleDef> RuleIndex = IndexRules();
        private static readonly Dictionary<string, EncounterDef> EncounterIndex = IndexEncounters();

        /// <summary>수칙 전부(공간 순, 공통은 끝).</summary>
        public static IReadOnlyList<RuleDef> AllRules
        {
            get { return Rules; }
        }

        /// <summary>조우 전부.</summary>
        public static IReadOnlyList<EncounterDef> AllEncounters
        {
            get { return Encounters; }
        }

        /// <summary>공간 수칙을 두는 다섯 공간(경비실 제외, 편성 순).</summary>
        public static readonly SpaceId[] RuleSpaces =
        {
            SpaceId.Corridor, SpaceId.Classroom, SpaceId.ScienceRoom, SpaceId.Toilet, SpaceId.Library
        };

        /// <summary>수칙을 ID로 찾는다. 없으면 null.</summary>
        public static RuleDef Rule(string id)
        {
            RuleDef r;
            return id != null && RuleIndex.TryGetValue(id, out r) ? r : null;
        }

        /// <summary>조우를 ID로 찾는다. 없으면 null.</summary>
        public static EncounterDef Encounter(string id)
        {
            EncounterDef e;
            return id != null && EncounterIndex.TryGetValue(id, out e) ? e : null;
        }

        /// <summary>그 공간의 수칙(카탈로그 순).</summary>
        public static List<RuleDef> RulesIn(SpaceId space)
        {
            List<RuleDef> list = new List<RuleDef>();
            SpaceId s = SpaceIds.Canonical(space);
            for (int i = 0; i < Rules.Length; i++)
            {
                if (Rules[i].Space == s) list.Add(Rules[i]);
            }

            return list;
        }

        private static Dictionary<string, RuleDef> IndexRules()
        {
            Dictionary<string, RuleDef> d = new Dictionary<string, RuleDef>(StringComparer.Ordinal);
            for (int i = 0; i < Rules.Length; i++) d.Add(Rules[i].Id, Rules[i]);
            return d;
        }

        private static Dictionary<string, EncounterDef> IndexEncounters()
        {
            Dictionary<string, EncounterDef> d = new Dictionary<string, EncounterDef>(StringComparer.Ordinal);
            for (int i = 0; i < Encounters.Length; i++) d.Add(Encounters[i].Id, Encounters[i]);
            return d;
        }
    }
}

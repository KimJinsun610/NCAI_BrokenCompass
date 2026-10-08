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
        public const string BoySeated = "E.BoySeated";
        public const string BoyBang = "E.BoyBang";
        public const string ToiletGirl = "E.ToiletGirl";
        public const string Footsteps = "E.Footsteps";
        public const string CallingVoice = "E.CallingVoice";
        public const string YellowFace = "E.YellowFace";
        public const string HallEndFigure = "E.HallEndFigure";
        public const string ModelRush = "E.ModelRush";
        public const string ScienceBlackout = "E.ScienceBlackout";
        public const string ToiletBlackout = "E.ToiletBlackout";
        public const string PeopleTree = "E.PeopleTree";
        public const string CeilingLegs = "E.CeilingLegs";
        public const string PhantomDoor = "E.PhantomDoor";
        public const string SuitMan = "E.SuitMan";
        public const string CctvPerson = "E.CctvPerson";

        /// <summary>2일차 첫 역설이 겨누는 수칙(C2 — 소년이 앉은 뒤 「교실에 남은 학생이 있습니다. 지금 확인하십시오.」, 눈으로만).</summary>
        public const string FirstParadoxRule = "C2";

        /// <summary>
        /// 편성하지 않는 수칙(정의는 남겨 둔다 — 판정기·역설 표·시험이 참조). 60차 민: 「교실의 판서 근무 수칙은 폐기해줘. 별로인 것 같아」 → C1.
        /// </summary>
        public static readonly string[] Retired = { "C1" };

        /// <summary>편성하지 않는 수칙인지.</summary>
        public static bool IsRetired(string ruleId)
        {
            return ruleId != null && System.Array.IndexOf(Retired, ruleId) >= 0;
        }

        /// <summary>5일차 경비실 고정 수칙(04:00 뒤에만 판정).</summary>
        public const string FinaleRule = "K4";

        /// <summary>5일차 태블릿에만 보이는 공통 수칙(판정 없음). 피날레 「들어왔다」에서 빈칸이 채워진다.</summary>
        public const string FinaleBlankRule = "G3";

        /// <summary>빈칸이 채워진 G3 문구(최종 기획서: 빈칸은 「당신」).</summary>
        public const string FinaleBlankFilled = "이 수칙서와 태블릿 공지가 다를 경우 당신을 따르십시오.";

        /// <summary>역보고 수칙(회차당 최대 2번).</summary>
        public const string ReverseReportRule = "T4";

        /// <summary>회차당 역보고 수칙 배정 한도.</summary>
        public const int ReverseReportPerRun = 2;

        /// <summary>하루 손전등 수칙 한도.</summary>
        public const int FlashlightPerDay = 3;

        private static KeyValuePair<FearAxis, Band> Need(FearAxis axis, Band band)
        {
            return new KeyValuePair<FearAxis, Band>(axis, band);
        }

        private const FearAxis A = FearAxis.Auditory;
        private const FearAxis I = FearAxis.Illuminance;
        private const FearAxis L = FearAxis.Layout;
        private const FearAxis T = FearAxis.Trust;

        private static readonly RuleDef[] Rules =
        {
            new RuleDef("H1", SpaceId.Corridor, L, true, "복도의 미분류 물체에 다가가지 마십시오.", boundEncounter: PeopleTree),   // 2026-10-01: 사람 나무가 없으면 피할 물체가 없다
            new RuleDef("H2", SpaceId.Corridor, L, true, "열린 문은 열린 채로 두십시오."),
            new RuleDef("H3", SpaceId.Corridor, A, true, "발소리가 들리면 가까운 방으로 대피하십시오.", isThreat: true, isWaitType: true, boundEncounter: Footsteps),
            new RuleDef("H4", SpaceId.Corridor, A, true, "뒤에서 부르면 돌아보지 마십시오.", isThreat: true, boundEncounter: CallingVoice),

            new RuleDef("C1", SpaceId.Classroom, A, true, "교실은 판서가 끝난 뒤에 들어가십시오.", isWaitType: true),   // 57차 민: 앞에 「교실」을 밝힘
            new RuleDef("C2", SpaceId.Classroom, A, true, "교실의 _? 는 무시하십시오.", boundEncounter: BoyBang),   // 52차 민: 앉은 소년과 묶음(C2 + C3 + 소년) — 3초 바라보면 책상에 머리를 박는다
            new RuleDef("C3", SpaceId.Classroom, A, true, "수업 중에 움직이지 마십시오.", isThreat: true, isWaitType: true, boundEncounter: BoySeated),
            new RuleDef("C4", SpaceId.Classroom, I, true, "초록 불빛 아래에서는 손전등을 끄십시오.", usesFlashlight: true),   // 52차 민: 늘 켜고 다니니 「끄라」로 — 등은 맵 어딘가 하나(RedLightSpot). 66차 민: 「조도 축이 오르면 전체 조명이 붉어지니 수칙의 빨간 불빛은 초록으로」
            new RuleDef("C5", SpaceId.Classroom, L, true, "교실의 문은 모두 부서져 있습니다. 성한 문으로는 나가지 마십시오.", boundEncounter: PhantomDoor),

            new RuleDef("S1", SpaceId.ScienceRoom, L, true, "과학실은 통로가 아닙니다. 통로로 사용하지 마십시오."),
            new RuleDef("S2", SpaceId.ScienceRoom, A, true, "깨지는 소리가 나면 과학실을 나가 다시 오지 마십시오."),
            new RuleDef("S3", SpaceId.ScienceRoom, I, true, "인체 모형에는 빛을 비추지 마십시오.", usesFlashlight: true),
            new RuleDef("S4", SpaceId.ScienceRoom, I, true, "소등 중에는 손전등을 끄지 마십시오.", usesFlashlight: true, boundEncounter: ScienceBlackout),
            new RuleDef("S5", SpaceId.ScienceRoom, I, true, "복도 끝에 _?이 서 있으면 빛을 끄고 기다리십시오.", isThreat: true, usesFlashlight: true, isWaitType: true, boundEncounter: HallEndFigure),

            new RuleDef("T1", SpaceId.Toilet, A, true, "물이 다 내려가기 전에 나오십시오."),
            new RuleDef("T2", SpaceId.Toilet, L, true, "사용 중인 칸은 열지 마십시오."),
            new RuleDef("T3", SpaceId.Toilet, I, true, "정전 시에는 기다리십시오.", isThreat: true, usesFlashlight: true, isWaitType: true, boundEncounter: ToiletBlackout),
            new RuleDef("T4", SpaceId.Toilet, L, true, "변기는 정상입니다.", boundEncounter: ToiletGirl),
            new RuleDef("T5", SpaceId.Toilet, I, true, "불이 켜진 칸이 있으면 당신도 불을 켜십시오.", usesFlashlight: true),

            new RuleDef("L1", SpaceId.Library, A, true, "쓰러진 책장 곁에 서지 마십시오."),
            new RuleDef("L2", SpaceId.Library, A, true, "책장 넘기는 소리가 들리면, 도서관 점검 시간은 끝났습니다. 나가십시오.", isWaitType: true),
            new RuleDef("L3", SpaceId.Library, I, true, "도서관 ?_? 을 계속 비추십시오.", isThreat: true, usesFlashlight: true, boundEncounter: YellowFace),
            new RuleDef("L4", SpaceId.Library, L, true, "상자에서 떨어져 걸으십시오."),
            new RuleDef("L5", SpaceId.Library, A, true, "창밖에 먼저 인사하지 마십시오. 눈길도, 빛도 인사입니다.", isThreat: true, boundEncounter: SuitMan),

            new RuleDef("K1", SpaceId.SecurityRoom, L, true, "화면 속 !_ 이 지나갈 때까지 채널을 넘기지 마십시오.", boundEncounter: CctvPerson),   // 52차 민: 원래대로(점프스케어를 예고하는 수칙은 쓰지 않는다)
            new RuleDef("K2", SpaceId.SecurityRoom, A, true, "공실 채널은 오래 보지 마십시오."),
            new RuleDef("K3", SpaceId.SecurityRoom, A, true, "서명 시간 외에는 경비실에 오래 머물지 마십시오."),
            new RuleDef("K4", SpaceId.SecurityRoom, L, false, "근무 종료 후에는 경비실을 나가지 마십시오."),

            new RuleDef("G1", SpaceId.None, A, true, "복도에서는 뛰지 마십시오."),
            new RuleDef("G3", SpaceId.None, L, false, "이 수칙서와 태블릿 공지가 다를 경우 [　　]을 따르십시오.")
        };

        private static readonly EncounterDef[] Encounters =
        {
            new EncounterDef(BoySeated, "소년 착석", A, SpaceId.Classroom, "C3", 1, "boy", new[] { Need(A, Band.Band0) }),
            new EncounterDef(BoyBang, "소년 머리 박기", A, SpaceId.Classroom, "C2", 4, "boy", new[] { Need(A, Band.Band2), Need(L, Band.Band2) }, true, "C3"),
            new EncounterDef(ToiletGirl, "화장실 여자아이", A, SpaceId.Toilet, "T4", 2, "girl", new[] { Need(A, Band.Band1) }),
            new EncounterDef(Footsteps, "달려오는 발소리", A, SpaceId.Corridor, "H3", 3, "footsteps", new[] { Need(A, Band.Band2) }),
            new EncounterDef(CallingVoice, "부르는 목소리", A, SpaceId.Corridor, "H4", 2, "voice", new[] { Need(A, Band.Band3) }),
            new EncounterDef(YellowFace, "노란 얼굴", I, SpaceId.Library, "L3", 3, "duck", new[] { Need(I, Band.Band1) }),
            new EncounterDef(HallEndFigure, "복도 끝에 선 자", I, SpaceId.ScienceRoom, "S5", 3, "meatman", new[] { Need(I, Band.Band2) }),
            new EncounterDef(ModelRush, "모형 급습", I, SpaceId.ScienceRoom, "S5", 5, "dummy", new[] { Need(I, Band.Band3), Need(L, Band.Band2) }, true),
            new EncounterDef(ScienceBlackout, "과학실 소등", I, SpaceId.ScienceRoom, "S4", 3, "science.dark", new[] { Need(I, Band.Band2) }),
            new EncounterDef(ToiletBlackout, "화장실 소등", I, SpaceId.Toilet, "T3", 3, "toilet.dark", new[] { Need(I, Band.Band2) }),
            new EncounterDef(PeopleTree, "사람 나무", L, SpaceId.Corridor, "H1", 3, "tree", new[] { Need(L, Band.Band2) }),
            new EncounterDef(CeilingLegs, "시체 낙하", L, SpaceId.Classroom, string.Empty, 3, "boy", new[] { Need(L, Band.Band2) }),   // 52차: 대응 수칙 없음(사다리 점검 중 놀람만 — 예고하는 수칙을 두지 않는다)
            new EncounterDef(PhantomDoor, "없던 문", L, SpaceId.Classroom, "C5", 2, "door", new[] { Need(L, Band.Band2) }),
            new EncounterDef(SuitMan, "창밖 정장 남자", T, SpaceId.Library, "L5", 3, "glitchman", new[] { Need(T, Band.Band3) }),
            new EncounterDef(CctvPerson, "CCTV에만 보이는 사람", L, SpaceId.SecurityRoom, "K1", 2, "blackman", new KeyValuePair<FearAxis, Band>[0])
        };

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

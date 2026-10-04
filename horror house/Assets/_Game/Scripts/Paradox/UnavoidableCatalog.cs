using System;
using System.Collections.Generic;

namespace NightDuty
{
    /// <summary>회피 불가 역설이 어떻게 걸리는지(긴장 디렉터가 거는 상황).</summary>
    public enum UnavoidableKind
    {
        /// <summary>두 수칙의 방아쇠를 한자리에 겹친다 — 첫 단서에 맞춰 둘째 조우를 바로 건다(S5 × H3).</summary>
        CrossCue = 0,

        /// <summary>「나가라」 신호를 그 공간 점검이 끝나기 전에 울린다(S2·L2·T1). 진짜에서 나가면 그 공간은 당일 재입실 불가.</summary>
        ExitBeforeInspection = 1,

        /// <summary>점검 항목이 남은 채 그 수칙의 단서를 울린다(T2 × T-1 · K1·K2 × K-1 · C2 × C-3).</summary>
        CueOnPendingItem = 2,

        /// <summary>단서로 그 공간을 나서는 순간 등 뒤에 둘째 조우를 건다(T1 물 내림 → 복도에서 H4 「Hey」).</summary>
        ChainOnExit = 3
    }

    /// <summary>회피 불가 역설 한 쌍(최종 기획서 「회피 불가 역설」 표).</summary>
    public sealed class UnavoidableDef
    {
        /// <summary>쌍 ID(「S2xScience」).</summary>
        public readonly string Id;

        /// <summary>진짜인지(빠져나갈 길이 없다). 가짜는 수칙을 글자 그대로 읽으면 길이 있다.</summary>
        public readonly bool Real;

        /// <summary>거는 방법.</summary>
        public readonly UnavoidableKind Kind;

        /// <summary>강제 편성할 수칙(쌍의 수칙 — 둘 다 판정하는 수칙이면 둘).</summary>
        public readonly string[] Rules;

        /// <summary>상황을 거는 단서(이 단서가 시작될 때 문자를 보낸다 · 둘째 조우를 건다).</summary>
        public readonly string Cue;

        /// <summary>점검이 남아야 하는 공간(<see cref="UnavoidableKind.ExitBeforeInspection"/>). 없으면 None.</summary>
        public readonly SpaceId Space;

        /// <summary>남아 있어야 하는 점검 항목(<see cref="UnavoidableKind.CueOnPendingItem"/>, 「T-1」). 없으면 null.</summary>
        public readonly string Item;

        /// <summary>둘째로 거는 조우(<see cref="UnavoidableKind.CrossCue"/>·<see cref="UnavoidableKind.ChainOnExit"/>). 그날은 제 슬롯에서 따로 걸지 않는다.</summary>
        public readonly string ChainEncounter;

        /// <summary>그날 「점검이 남았을 때만」 울릴 수칙 단서(디렉터 문). 없으면 null.</summary>
        public readonly string GateRule;

        /// <summary>태블릿 문자(지시하지 않고 사실만 — 기획서 그대로).</summary>
        public readonly string Message;

        /// <summary>만든다.</summary>
        public UnavoidableDef(string id, bool real, UnavoidableKind kind, string[] rules, string cue, SpaceId space, string item, string chainEncounter, string gateRule, string message)
        {
            Id = id;
            Real = real;
            Kind = kind;
            Rules = rules ?? new string[0];
            Cue = cue;
            Space = space;
            Item = item;
            ChainEncounter = chainEncounter;
            GateRule = gateRule;
            Message = message;
        }

        /// <summary>쌍의 수칙인지.</summary>
        public bool Has(string ruleId)
        {
            return ruleId != null && Array.IndexOf(Rules, ruleId) >= 0;
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return (Real ? "진짜 " : "가짜 ") + Id;
        }
    }

    /// <summary>
    /// 회피 불가 역설 표(최종 기획서 2026-09-30 정본, 10단계). <b>문자는 기획서 그대로</b>.
    /// 공간당 하루 1장 규칙을 지키는 쌍만 있다(같은 공간 두 장이 필요한 쌍은 기획서에서 폐기).
    /// </summary>
    public static class UnavoidableCatalog
    {
        /// <summary>재입실 금지를 알리는 문장(진짜 「나가라」 쌍의 문자 끝).</summary>
        public const string NoReentry = "금일 재입실은 불가합니다.";

        private static readonly List<UnavoidableDef> s_all = new List<UnavoidableDef>
        {
            new UnavoidableDef("S5xH3", true, UnavoidableKind.CrossCue, new[] { "S5", "H3" }, FinalCues.HallEnd, SpaceId.None, null, ProgramCatalog.Footsteps, null,
                "복도에 인원이 둘입니다."),
            new UnavoidableDef("S2xScience", true, UnavoidableKind.ExitBeforeInspection, new[] { "S2" }, FinalCues.Glass, SpaceId.ScienceRoom, null, null, "S2",
                "과학실 점검이 끝나지 않았습니다. " + NoReentry),
            new UnavoidableDef("L2xLibrary", true, UnavoidableKind.ExitBeforeInspection, new[] { "L2" }, FinalCues.Pages, SpaceId.Library, null, null, "L2",
                "도서관 점검이 끝나지 않았습니다. " + NoReentry),
            new UnavoidableDef("T1xToilet", true, UnavoidableKind.ExitBeforeInspection, new[] { "T1" }, FinalCues.Flush, SpaceId.Toilet, null, null, "T1",
                "화장실 점검이 끝나지 않았습니다. " + NoReentry),
            new UnavoidableDef("T2xT-1", true, UnavoidableKind.CueOnPendingItem, new[] { "T2" }, FinalCues.StallOccupied, SpaceId.Toilet, "T-1", null, "T2",
                "변기 점검 칸이 사용 중입니다."),
            new UnavoidableDef("K1xK-1", false, UnavoidableKind.CueOnPendingItem, new[] { "K1" }, FinalCues.CctvPerson, SpaceId.SecurityRoom, "K-1", null, null,
                "화면에 인원이 잡혔습니다."),
            new UnavoidableDef("K2xK-1", false, UnavoidableKind.CueOnPendingItem, new[] { "K2" }, FinalCues.EmptyRoom, SpaceId.SecurityRoom, "K-1", null, "K2",
                "CCTV 점검 시간입니다."),
            new UnavoidableDef("C2xC-3", false, UnavoidableKind.CueOnPendingItem, new[] { "C2" }, FinalCues.Legs, SpaceId.Classroom, "C-3", null, null,
                "교실 천장 점검 요청이 있습니다."),
            new UnavoidableDef("H4xT1", false, UnavoidableKind.ChainOnExit, new[] { "H4", "T1" }, FinalCues.Flush, SpaceId.Toilet, null, ProgramCatalog.CallingVoice, null,
                "복도에 육성이 감지되었습니다.")
        };

        /// <summary>표 전체.</summary>
        public static IReadOnlyList<UnavoidableDef> All
        {
            get { return s_all; }
        }

        /// <summary>ID로 찾는다. 없으면 null.</summary>
        public static UnavoidableDef Find(string id)
        {
            for (int i = 0; i < s_all.Count; i++)
            {
                if (s_all[i].Id == id) return s_all[i];
            }

            return null;
        }

        /// <summary>
        /// 그날 점검 편성으로 걸 수 있는지 — 남아야 할 항목이 배정됐는가(「나가라」 쌍은 그 공간에 점검이 하나라도, 항목 쌍은 그 항목).
        /// 경비실 수칙(K1·K2)은 2일차부터, 5일차는 경비실이 K4라 걸지 않는다.
        /// </summary>
        public static bool Feasible(UnavoidableDef def, int day, InspectionPlan inspections)
        {
            if (def == null) return false;
            for (int i = 0; i < def.Rules.Length; i++)
            {
                RuleDef r = ProgramCatalog.Rule(def.Rules[i]);
                if (r == null) return false;
                if (SpaceIds.Canonical(r.Space) == SpaceId.SecurityRoom && (day < 2 || day >= FinaleWatch.Day)) return false;
            }

            switch (def.Kind)
            {
                case UnavoidableKind.ExitBeforeInspection:
                    return CountIn(inspections, def.Space) > 0;
                case UnavoidableKind.CueOnPendingItem:
                    return inspections != null && inspections.Find(def.Item) != null;
                default:
                    return day >= 2;
            }
        }

        private static int CountIn(InspectionPlan plan, SpaceId space)
        {
            if (plan == null) return 0;
            int n = 0;
            for (int i = 0; i < plan.Assignments.Count; i++)
            {
                if (SpaceIds.Canonical(plan.Assignments[i].Item.Space) == space) n++;
            }

            return n;
        }

        /// <summary>강제 편성 뒤 실제로 걸렸는지 — 쌍의 수칙이 모두 덱에 있고, 둘째 조우가 편성됐는가.</summary>
        public static bool Placed(UnavoidableDef def, NightProgram program)
        {
            if (def == null || program == null) return false;
            for (int i = 0; i < def.Rules.Length; i++)
            {
                if (!program.Has(def.Rules[i])) return false;
            }

            return string.IsNullOrEmpty(def.ChainEncounter) || program.HasEncounter(def.ChainEncounter);
        }
    }
}

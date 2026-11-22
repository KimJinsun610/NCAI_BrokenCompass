using System;
using System.Collections.Generic;

namespace NightDuty
{
    /// <summary>안전한 읽기 패턴(최종 기획서 「모호 역설」). 하나면 곧 규칙이 되어 청기백기가 되므로 셋이다.</summary>
    public enum SafeReadPattern
    {
        /// <summary>역설 문자가 없는 수칙.</summary>
        None = 0,

        /// <summary>눈으로만 — 다가가거나 오래 보지 않고 본다. 판정: 대상을 1초 이상 시야에 두고 그 수칙을 어기지 않음.</summary>
        EyesOnly = 1,

        /// <summary>멈춰서 — 움직이지 않고 그 자리(또는 방 안)에서 한다. 판정: 수칙의 정지 조건을 끝까지 지킴.</summary>
        StandStill = 2,

        /// <summary>CCTV로 — 경비실에서 한다. 판정: 그 공간 채널을 2초 이상 봄. 신뢰 구간 3 이상(CCTV 고장)에서는 보내지 않는다.</summary>
        Cctv = 3,
    }

    /// <summary>역설·변조 문구표 한 줄(최종 기획서 「역설·변조 문구표」).</summary>
    public sealed class ParadoxEntry
    {
        /// <summary>수칙 ID.</summary>
        public readonly string RuleId;

        /// <summary>역설 문자. 없으면 null(그 수칙은 겨누지 않는다).</summary>
        public readonly string Message;

        /// <summary>안전한 읽기 패턴.</summary>
        public readonly SafeReadPattern Pattern;

        /// <summary>그 수칙의 방아쇠 단서 ID(문자를 보내는 때 · 「멈춰서」가 끝나는 때). 없으면 null — 공간에 들어간 직후만.</summary>
        public readonly string Cue;

        /// <summary>「눈으로만」이 바라볼 대상 ID(판정 기준점). 없으면 null — 그 공간에 1초 머무는 것으로 본다.</summary>
        public readonly string Target;

        /// <summary>「CCTV로」가 볼 공간(대개 수칙의 공간, K3은 복도).</summary>
        public readonly SpaceId CctvSpace;

        /// <summary>변조본(붕괴형). 없으면 null(변조하지 않는다).</summary>
        public readonly string Variant;

        /// <summary>만든다.</summary>
        public ParadoxEntry(string ruleId, string message, SafeReadPattern pattern, string cue, string target, SpaceId cctvSpace, string variant)
        {
            RuleId = ruleId;
            Message = message;
            Pattern = message == null ? SafeReadPattern.None : pattern;
            Cue = cue;
            Target = target;
            CctvSpace = cctvSpace;
            Variant = variant;
        }

        /// <summary>역설 문자가 있는지.</summary>
        public bool HasMessage
        {
            get { return !string.IsNullOrEmpty(Message); }
        }

        /// <summary>변조본이 있는지.</summary>
        public bool HasVariant
        {
            get { return !string.IsNullOrEmpty(Variant); }
        }

        /// <summary>2026-11-22: 역설 문자의 지금 언어 글(DT Rule Paradox_Text). 한국어면 <see cref="Message"/>.</summary>
        public string ShownMessage
        {
            get { return Loc.Dt(RuleId, "Paradox_Text", Message); }
        }

        /// <summary>2026-11-22: 변조본의 지금 언어 글(DT Rule Variant_Text). 한국어면 <see cref="Variant"/>.</summary>
        public string ShownVariant
        {
            get { return Loc.Dt(RuleId, "Variant_Text", Variant); }
        }
    }

    /// <summary>
    /// 역설·변조 문구표(최종 기획서 2026-09-30 정본, 10단계). <b>문구는 기획서 그대로</b> — 고칠 때는 기획서와 함께.
    /// 2026-10-04 민 수정(텍스트 목록 문서): C3·C5·L2·L5·K1 역설 폐기(몬스터를 미리 알려 긴장을 깎는다), S2 안전한 읽기 CCTV로 → 눈으로만, S3 변조본 폐기(수칙 문구 변경).
    /// 「없음」인 칸은 null. T4·K4·G1·G3은 겨누지도 변조하지도 않는다.
    /// K2(「CAM0n에 신호가 잡힙니다.」)의 n은 그날의 빈 방 채널(<see cref="ParadoxPlan.EmptyRoomChannel"/>) — 문자의 {0}을 채운다.
    /// K2의 「눈으로만」은 그 채널을 잠깐(0.5초) 보고 다른 채널로 넘기는 것이다(3초 넘게 보면 K2 위반).
    /// </summary>
    public static class ParadoxCatalog
    {
        private static readonly Dictionary<string, ParadoxEntry> s_byId = new Dictionary<string, ParadoxEntry>(StringComparer.Ordinal);

        static ParadoxCatalog()
        {
            // DT 3단계: Rule_Table 역설 칸(Paradox_Text · Paradox_Safe_ID · Paradox_Cue_ID · Paradox_Target_ID · Paradox_Cctv_No · Variant_Text).
            // Excluded_From_Paradox(T4·K4·G1·G3·H5)는 표에 줄이 없다. 문구 정본은 xlsx.
            foreach (InstructionDT.RuleRow r in ProgramCatalog.RuleRows())
            {
                if (r.Excluded_From_Paradox) continue;
                string message = string.IsNullOrEmpty(r.Paradox_Text) ? null : r.Paradox_Text;
                string cue = r.Paradox_Cue_ID == 0 ? null : global::NightDuty.DesignData.Label(r.Paradox_Cue_ID);
                string target = r.Paradox_Target_ID == 0 ? null : global::NightDuty.DesignData.Label(r.Paradox_Target_ID);
                string variant = string.IsNullOrEmpty(r.Variant_Text) ? null : r.Variant_Text;
                SpaceId cctv = r.Paradox_Cctv_No == 0 ? SpaceId.None : ParadoxRun.SpaceOfChannel((int)r.Paradox_Cctv_No - 1);   // 표 CAM 번호는 1부터(1 = 복도 = 채널 0)
                Add(r.ID.ToString(), message, PatternOf(r.Paradox_Safe_ID), cue, target, variant, cctv);
            }
        }

        /// <summary>DT 안전한 읽기 ID → 패턴(520601 눈으로만 · 520602 멈춰서 · 520603 CCTV로).</summary>
        private static SafeReadPattern PatternOf(uint id)
        {
            switch (id)
            {
                case 520601: return SafeReadPattern.EyesOnly;
                case 520602: return SafeReadPattern.StandStill;
                case 520603: return SafeReadPattern.Cctv;
                default: return SafeReadPattern.None;
            }
        }

        private static void Add(string id, string message, SafeReadPattern pattern, string cue, string target, string variant, SpaceId cctvSpace = SpaceId.None)
        {
            RuleDef def = ProgramCatalog.Rule(id);
            SpaceId space = cctvSpace != SpaceId.None ? cctvSpace : def != null ? def.Space : SpaceId.None;
            s_byId[id] = new ParadoxEntry(id, message, pattern, cue, target, space, variant);
        }

        /// <summary>그 수칙의 줄. 표에 없으면 null.</summary>
        public static ParadoxEntry Find(string ruleId)
        {
            ParadoxEntry e;
            return ruleId != null && s_byId.TryGetValue(ruleId, out e) ? e : null;
        }

        /// <summary>표 전체.</summary>
        public static IEnumerable<ParadoxEntry> All
        {
            get { return s_byId.Values; }
        }

        /// <summary>
        /// 미세 변조(신뢰 구간 3) — 어미 하나만 흔든다: 끝의 「.」 앞에 「?」를 넣는다(「…마십시오.」 → 「…마십시오?.」).
        /// 뜻·방아쇠·대상은 그대로다(검수 기준: 원본을 기억하는 사람이 다른 행동을 할 여지가 없는가).
        /// </summary>
        public static string Subtle(string original)
        {
            if (string.IsNullOrEmpty(original)) return original;
            string t = original.TrimEnd();
            if (t.EndsWith(".", StringComparison.Ordinal)) return t.Substring(0, t.Length - 1) + "?.";
            return t + "?";
        }

        /// <summary>검은 막대 색 — 태블릿 화면(거의 검정)보다 한 단 밝은 먹색. 흰 █는 어두운 화면에서 밝은 막대로 보였다(31차).</summary>
        public const string BlackedColor = "#262D31";

        /// <summary>검게 지운 줄(신뢰 구간 4) — 원문 길이에 맞춘 먹색 막대(TMP 색 태그).</summary>
        public static string Blacked(string original)
        {
            int n = string.IsNullOrEmpty(original) ? 12 : Math.Max(global::NightDuty.DesignData.SettingInt(390339), (int)(original.Length * global::NightDuty.DesignData.Setting(390340)));   // DT 390339 최소 칸 · 390340 배율
            return "<color=" + BlackedColor + ">" + new string('█', n) + "</color>";
        }
    }
}

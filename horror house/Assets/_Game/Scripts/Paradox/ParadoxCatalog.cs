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
            Add("H1", "복도의 중앙을 지금 확인하십시오.", SafeReadPattern.EyesOnly, null, FinalCues.H1Object, "복도의 미분류 물체는 점검 하시겠습니까.? 아니오. 피하 십시오.");
            Add("H2", "저절로 열린 문이 있습니다. 지금 확인하십시오.", SafeReadPattern.EyesOnly, null, null, "열린 문은 열린 문이다.");
            Add("H3", "복도에 인원이 있습니다. 지금 확인하십시오.", SafeReadPattern.StandStill, FinalCues.Footsteps, null, "발소리 가 들리면? 가까운 방 으로. 대피 하십니까.? 대피하십시오.");
            Add("H4", null, SafeReadPattern.None, null, null, null);
            Add("C1", "교실에서 소리가 나면 즉시 점검하십시오.", SafeReadPattern.StandStill, FinalCues.Chalk, null, "판서 를 합류 하지 마시오.");
            Add("C2", "천장에서 물이 샙니다. 지금 바닥을 확인하십시오.", SafeReadPattern.EyesOnly, FinalCues.Legs, null, "교실의 ??은 무시 대상 입니 다.");
            Add("C3", null, SafeReadPattern.None, null, null, "수업 중 이동 은 금지입니 ㄷ ㅏ.");   // 민 2026-10-04: 역설 폐기(몬스터를 과하게 예고해 긴장 저하)
            Add("C4", "붉은 등이 켜졌습니다. 즉시 조명 상태를 확인하십시오.", SafeReadPattern.EyesOnly, FinalCues.RedLight, null, "붉은 불빛 아래 손전등 은 끄지 마. 끄지 마.");
            Add("C5", null, SafeReadPattern.None, null, null, "정상 문은? 교실이 아닙 니다. 교실 의 문은 고장.");   // 민 2026-10-04: 역설 폐기
            Add("S1", "반대편 문이 열렸습니다. 지금 점검하십시오.", SafeReadPattern.EyesOnly, null, null, "과학 실은 통로 입니까?. 아니오.");
            Add("S2", "지금 과학실의 훼손 물품을 점검하십시오.", SafeReadPattern.EyesOnly, FinalCues.Glass, null, "깨지는 소리 가 나면? 오늘 과학실 은 끝.");   // 민 2026-10-04: CCTV로 → 눈으로만
            Add("S3", null, SafeReadPattern.None, null, null, null);   // 민 2026-10-04: 수칙 문구를 「빛으로 확인」으로 바꾸며 변조본 폐기
            Add("S4", null, SafeReadPattern.None, null, null, "어둠을 지양하십시오?.");
            Add("S5", "복도 끝 출구를 즉시 확인하십시오.", SafeReadPattern.StandStill, FinalCues.HallEnd, null, "복도 _?에 눈 부십니까?. 끄고 기다리 십시오.");
            Add("T1", "물이 내려가고 있습니다. 지금 칸을 확인하십시오.", SafeReadPattern.EyesOnly, FinalCues.Flush, null, null);
            Add("T2", null, SafeReadPattern.None, null, null, "사용 중 인 칸은? 열지 마 십시오.");
            Add("T3", "정전입니다. 즉시 비상등을 찾으십시오.", SafeReadPattern.StandStill, FinalCues.ToiletBlackout, null, "어둠 에서 대기 하라.");
            Add("T5", "불이 켜진 칸이 있습니다. 그 불빛이면 충분합니다.", SafeReadPattern.EyesOnly, FinalCues.StallLit, null, "빛? 에는 빛?으로 대처 하세요.");
            Add("L1", "쓰러진 책장 밑에 책이 깔렸습니다. 지금 비추십시오.", SafeReadPattern.EyesOnly, null, FinalCues.L1Shelf, "쓰러진 책장 곁 에 서 지 마 십 시 오");
            Add("L2", null, SafeReadPattern.None, null, null, "종이 넘기 는 소리 가 들리면? 열람 끝.");   // 민 2026-10-04: 역설 폐기(몹 예고)
            Add("L3", null, SafeReadPattern.None, null, null, "도서관 ?_? 을 계속 비추 십니까? 예.");
            Add("L4", "바닥에 상자가 쏟아져 있습니다. 지금 확인하십시오.", SafeReadPattern.EyesOnly, null, FinalCues.L4Box, "상자 에서 떨어 져 걸으 십시오?");
            Add("L5", null, SafeReadPattern.None, null, null, "창밖에 먼저 인사 하지 마시오. 먼저.");   // 민 2026-10-04: 역설 폐기(몹 예고)
            Add("K1", null, SafeReadPattern.None, null, null, null);   // 민 2026-10-04: 역설 폐기(몹 예고)
            Add("K2", "CAM{0}에 신호가 잡힙니다. 즉시 확인하십시오.", SafeReadPattern.EyesOnly, FinalCues.EmptyRoom, null, null);   // {0} = 그날 빈 방 채널(CAM01~05)
            Add("K3", "복도에 위험 신고가 있습니다. 즉시 CCTV로 확인하십시오.", SafeReadPattern.Cctv, null, null, null, SpaceId.Corridor);
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
            int n = string.IsNullOrEmpty(original) ? 12 : Math.Max(8, (int)(original.Length * 0.8f));
            return "<color=" + BlackedColor + ">" + new string('█', n) + "</color>";
        }
    }
}

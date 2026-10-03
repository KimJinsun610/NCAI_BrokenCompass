using System;
using System.Collections.Generic;
using System.Text;

namespace NightDuty
{
    /// <summary>
    /// 하룻밤의 역설·변조 편성(최종 기획서 「역설과 변조 — 편성」). 밤 시작에 확정되고 재시작해도 바뀌지 않는다.
    /// 판정은 바꾸지 않는다 — 판정은 언제나 수칙서 원본이다. 바뀌는 것은 태블릿 문자와 태블릿에 보이는 글뿐이다.
    /// </summary>
    public sealed class ParadoxPlan
    {
        /// <summary>빈 편성.</summary>
        public static readonly ParadoxPlan None = new ParadoxPlan(Band.Band0, null, false, false, null, null, null, null, "역설 없음");

        /// <summary>밤 시작 신뢰 연출 구간.</summary>
        public readonly Band TrustBand;

        /// <summary>모호 역설이 겨누는 수칙. 없으면 null.</summary>
        public readonly string Ambiguous;

        /// <summary>회차의 첫 역설인지(반드시 보낸다 — 가르치기).</summary>
        public readonly bool FirstOfRun;

        /// <summary>
        /// 모호 역설을 실제로 보낼지(밤 시작에 굴린다 — 재시작해도 같다). 대상 공간에 이상이 있으면 75%, 없으면 25%, 회차 첫 역설은 100%.
        /// 확률이 공간의 이상 여부에 기우는 것이 「안전한 읽기」의 보상이다 — 문자가 왔다는 것 자체가 약한 단서다.
        /// </summary>
        public readonly bool WillSend;

        /// <summary>변조된 수칙. 없으면 null.</summary>
        public readonly string Tampered;

        /// <summary>변조본(태블릿에 보이는 글).</summary>
        public readonly string TamperText;

        /// <summary>검게 지운 수칙(신뢰 구간 4). 없으면 null.</summary>
        public readonly string Blacked;

        /// <summary>검게 지운 줄의 글.</summary>
        public readonly string BlackedText;

        /// <summary>편성 설명(디버그·로그).</summary>
        public readonly string Report;

        /// <summary>만든다.</summary>
        public ParadoxPlan(Band trustBand, string ambiguous, bool firstOfRun, bool willSend, string tampered, string tamperText, string blacked, string blackedText, string report)
        {
            TrustBand = trustBand;
            Ambiguous = ambiguous;
            FirstOfRun = firstOfRun;
            WillSend = ambiguous != null && willSend;
            Tampered = tampered;
            TamperText = tamperText;
            Blacked = blacked;
            BlackedText = blackedText;
            Report = report ?? string.Empty;
        }

        /// <summary>모호 역설의 문구표 줄. 없으면 null.</summary>
        public ParadoxEntry AmbiguousEntry
        {
            get { return ParadoxCatalog.Find(Ambiguous); }
        }

        /// <summary>그 수칙이 태블릿에 다르게 보이면 그 글, 아니면 null.</summary>
        public string DisplayTextOf(string ruleId)
        {
            if (ruleId == null) return null;
            if (ruleId == Tampered) return TamperText;
            if (ruleId == Blacked) return BlackedText;
            return null;
        }
    }

    /// <summary>
    /// 역설·변조 편성기(회차 하나에 하나). 최종 기획서 표:
    /// <list type="bullet">
    /// <item>신뢰 구간 0 — 없음(단 2일차는 회차 첫 역설 C2 「천장에서 물이 샙니다.」 — 눈으로만).</item>
    /// <item>1–2 — 모호 역설 1.</item>
    /// <item>3 — 모호 역설 1 + 변조 1장(미세 변조). (회피 불가 1 — 항상 가짜 — 는 아직 없다.)</item>
    /// <item>4 — 모호 역설 1 + 변조 1장(붕괴형) + 다른 수칙 한 줄이 검게 지워짐. (진짜 회피 불가는 아직 없다.)</item>
    /// </list>
    /// 모호 역설 대상: 그날 덱 중 문구가 있는 수칙(T4·K4·같은 밤 변조된 수칙 제외). 회차의 첫 역설은 「눈으로만」 패턴만.
    /// 신뢰 구간 3 이상이면 「CCTV로」 패턴은 보내지 않는다(CCTV 고장).
    /// 변조 대상: 그날 덱 중 <b>이전 밤에 원본을 본</b> 수칙, 변조본이 있는 것(T4·K4 제외).
    /// </summary>
    public sealed class ParadoxPlanner
    {
        private readonly Random _rng;
        private readonly HashSet<string> _seen = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>만든다. 시험은 씨앗을 준다.</summary>
        public ParadoxPlanner(Random rng = null)
        {
            _rng = rng ?? new Random();
        }

        /// <summary>회차에서 역설 문자를 한 번이라도 보냈는지(첫 역설 = 눈으로만·반드시 보냄).</summary>
        public bool SentInRun { get; private set; }

        /// <summary>이전 밤들에 태블릿에서 원본을 본 수칙(변조 대상 후보).</summary>
        public IReadOnlyCollection<string> Seen
        {
            get { return _seen; }
        }

        /// <summary>역설 문자를 실제로 보냈다(편성이 아니라 발송 — 확률에서 떨어지면 다음 역설이 여전히 첫 역설이다).</summary>
        public void MarkSent()
        {
            SentInRun = true;
        }

        /// <summary>이상이 있는 공간이면 모호 역설을 보낼 확률(%).</summary>
        public const int SendPercentAnomaly = 75;

        /// <summary>이상이 없는 공간이면 모호 역설을 보낼 확률(%).</summary>
        public const int SendPercentClear = 25;

        /// <summary>
        /// 그날 편성. 끝나면 그날 덱을 「본 수칙」에 더한다(다음 밤부터 변조 후보).
        /// </summary>
        /// <param name="spaceHasAnomaly">그 공간에 오늘 이상이 배정됐는지(발송 확률). null이면 모두 이상 없음.</param>
        public ParadoxPlan Plan(int day, Band trustBand, IReadOnlyList<RuleDef> deck, Func<SpaceId, bool> spaceHasAnomaly = null)
        {
            if (deck == null || deck.Count == 0) return ParadoxPlan.None;
            int band = (int)trustBand;
            StringBuilder note = new StringBuilder();
            note.Append("신뢰 구간 ").Append(band).Append(": ");

            // 1) 변조(신뢰 3+) — 모호 역설과 같은 수칙에 걸지 않으므로 먼저 고른다.
            string tampered = null;
            string tamperText = null;
            if (band >= 3)
            {
                List<string> pool = new List<string>();
                for (int i = 0; i < deck.Count; i++)
                {
                    string id = deck[i].Id;
                    ParadoxEntry e = ParadoxCatalog.Find(id);
                    if (e == null || !e.HasVariant || Excluded(id) || !_seen.Contains(id)) continue;
                    if (id == ProgramCatalog.FirstParadoxRule && day == 2) continue;
                    pool.Add(id);
                }

                if (pool.Count > 0)
                {
                    tampered = pool[_rng.Next(pool.Count)];
                    RuleDef def = ProgramCatalog.Rule(tampered);
                    tamperText = band >= 4 ? ParadoxCatalog.Find(tampered).Variant : ParadoxCatalog.Subtle(def != null ? def.Text : string.Empty);
                    note.Append("변조 ").Append(tampered).Append(band >= 4 ? "(붕괴형) " : "(미세) ");
                }
                else
                {
                    note.Append("변조 후보 없음(이전 밤에 본 수칙 중 변조본이 있는 것이 없음) ");
                }
            }

            // 2) 모호 역설.
            string ambiguous = null;
            bool first = !SentInRun;
            bool dayTwoFirst = day == 2 && first && Has(deck, ProgramCatalog.FirstParadoxRule);
            if (dayTwoFirst)
            {
                ambiguous = ProgramCatalog.FirstParadoxRule;
            }
            else if (band >= 1)
            {
                List<string> pool = new List<string>();
                for (int i = 0; i < deck.Count; i++)
                {
                    string id = deck[i].Id;
                    ParadoxEntry e = ParadoxCatalog.Find(id);
                    if (e == null || !e.HasMessage || Excluded(id) || id == tampered) continue;
                    if (first && e.Pattern != SafeReadPattern.EyesOnly) continue;      // 회차의 첫 역설은 「눈으로만」
                    if (band >= 3 && e.Pattern == SafeReadPattern.Cctv) continue;     // CCTV 고장
                    pool.Add(id);
                }

                if (pool.Count > 0) ambiguous = pool[_rng.Next(pool.Count)];
            }

            bool willSend = false;
            if (ambiguous != null)
            {
                SpaceId space = ParadoxRun.SpaceOf(ambiguous);
                bool anomaly = spaceHasAnomaly != null && spaceHasAnomaly(space);
                int roll = _rng.Next(100);
                willSend = first || roll < (anomaly ? SendPercentAnomaly : SendPercentClear);
                note.Append("모호 역설 ").Append(ambiguous).Append(first ? "(회차 첫 역설) " : " ")
                    .Append(willSend ? "발송" : "보류").Append("(").Append(space).Append(anomaly ? " 이상 있음" : " 이상 없음")
                    .Append(first ? string.Empty : ", 굴림 " + roll).Append(") ");
            }
            else if (band >= 1 || day == 2) note.Append("모호 역설 대상 없음 ");

            // 3) 검게 지운 줄(신뢰 4).
            string blacked = null;
            string blackedText = null;
            if (band >= 4)
            {
                List<string> pool = new List<string>();
                for (int i = 0; i < deck.Count; i++)
                {
                    string id = deck[i].Id;
                    if (id == ambiguous || id == tampered || Excluded(id)) continue;
                    pool.Add(id);
                }

                if (pool.Count > 0)
                {
                    blacked = pool[_rng.Next(pool.Count)];
                    RuleDef def = ProgramCatalog.Rule(blacked);
                    blackedText = ParadoxCatalog.Blacked(def != null ? def.Text : string.Empty);
                    note.Append("검은 줄 ").Append(blacked).Append(' ');
                }
            }

            for (int i = 0; i < deck.Count; i++) _seen.Add(deck[i].Id);
            if (ambiguous == null && tampered == null && blacked == null) return new ParadoxPlan(trustBand, null, false, false, null, null, null, null, note.Append("없음").ToString());
            return new ParadoxPlan(trustBand, ambiguous, ambiguous != null && first, willSend, tampered, tamperText, blacked, blackedText, note.ToString().TrimEnd());
        }

        /// <summary>겨누지도 변조하지도 않는 수칙.</summary>
        public static bool Excluded(string id)
        {
            return id == ProgramCatalog.ReverseReportRule || id == ProgramCatalog.FinaleRule || id == "K2";
        }

        private static bool Has(IReadOnlyList<RuleDef> deck, string id)
        {
            for (int i = 0; i < deck.Count; i++)
            {
                if (deck[i].Id == id) return true;
            }

            return false;
        }
    }
}

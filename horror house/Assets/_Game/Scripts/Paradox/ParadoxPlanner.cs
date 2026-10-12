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

        /// <summary>그날 회피 불가 역설(신뢰 구간 3 가짜 · 4 진짜). 없으면 null.</summary>
        public readonly UnavoidableDef Unavoidable;

        /// <summary>그날의 빈 방 채널(0~4, K2). 모르면 -1.</summary>
        public readonly int EmptyRoomChannel;

        /// <summary>만든다.</summary>
        public ParadoxPlan(Band trustBand, string ambiguous, bool firstOfRun, bool willSend, string tampered, string tamperText, string blacked, string blackedText, string report,
            UnavoidableDef unavoidable = null, int emptyRoomChannel = -1)
        {
            Unavoidable = unavoidable;
            EmptyRoomChannel = emptyRoomChannel;
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
    /// <item>3 — 모호 역설 1 + 가짜 회피 불가 1 + 변조 1장(미세 변조).</item>
    /// <item>4 — 모호 역설 1 + 진짜 회피 불가 1(회차의 첫 회피 불가면 가짜) + 변조 1장(붕괴형) + 다른 수칙 한 줄이 검게 지워짐.</item>
    /// </list>
    /// 모호 역설 대상: 그날 덱 중 문구가 있는 수칙(T4·K4·같은 밤 변조된 수칙 제외). 회차의 첫 역설은 「눈으로만」 패턴만.
    /// 신뢰 구간 3 이상이면 「CCTV로」 패턴은 보내지 않는다(CCTV 고장). 그날의 빈 방 채널(K2) 공간도 「CCTV로」 대상에서 뺀다.
    /// 회피 불가 역설(<see cref="PlanUnavoidable"/>)은 덱 편성 전에 골라 쌍의 수칙을 강제 편성하고, 그 수칙에는 역설·변조·검은 줄을 걸지 않는다.
    /// 변조 대상: 그날 덱 중 <b>이전 밤에 원본을 본</b> 수칙, 변조본이 있는 것(T4·K4 제외).
    /// </summary>
    public sealed class ParadoxPlanner
    {
        private readonly Random _rng;
        private readonly HashSet<string> _seen = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _tamperedInRun = new HashSet<string>(StringComparer.Ordinal);   // 103차: 회차에 이미 변조한 수칙
        private readonly HashSet<string> _unavoidableInRun = new HashSet<string>(StringComparer.Ordinal);   // 103차: 회차에 이미 건(문자를 보낸) 회피 불가 쌍(같은 쌍은 한 번)

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

        /// <summary>회차에서 회피 불가 역설이 한 번이라도 걸렸는지(회차의 첫 회피 불가는 반드시 가짜).</summary>
        public bool UnavoidableInRun { get; private set; }

        /// <summary>회피 불가 역설이 실제로 걸렸다(문자를 보냈다). 걸린 쌍은 회차에 다시 고르지 않는다(103차).</summary>
        /// <param name="def">걸린 쌍(없으면 기록만 하지 않는다).</param>
        public void MarkUnavoidable(UnavoidableDef def = null)
        {
            UnavoidableInRun = true;
            if (def != null) _unavoidableInRun.Add(def.Id);
        }

        /// <summary>그날의 빈 방 채널(0~4)을 고른다 — K2 단서·K2 역설의 「CAM0n」.</summary>
        public int PickEmptyRoomChannel()
        {
            return _rng.Next(RuleTriggers.CctvChannels);
        }

        /// <summary>
        /// 그날 회피 불가 역설을 고른다(<b>덱 편성 전</b> — 쌍의 수칙을 강제 편성한다). 신뢰 구간 3이면 가짜, 4면 진짜(회차의 첫 회피 불가면 가짜).
        /// 그날 점검 편성으로 걸 수 없는 쌍은 뺀다. 없으면 null.
        /// </summary>
        /// <param name="canForce">그날 그 수칙을 강제 편성할 수 있는지(103차 — 경비실·CCTV 날짜). null이면 묻지 않는다.</param>
        public UnavoidableDef PlanUnavoidable(int day, Band trustBand, InspectionPlan inspections, Func<string, bool> canForce = null)
        {
            if ((int)trustBand < 3) return null;
            bool real = (int)trustBand >= 4 && UnavoidableInRun;
            List<UnavoidableDef> pool = new List<UnavoidableDef>();
            IReadOnlyList<UnavoidableDef> all = UnavoidableCatalog.All;
            for (int i = 0; i < all.Count; i++)
            {
                if (_unavoidableInRun.Contains(all[i].Id)) continue;   // 103차: 같은 쌍은 회차에 한 번
                if (canForce != null && !Array.TrueForAll(all[i].Rules, canForce.Invoke)) continue;   // 103차: 경비실 자리·CCTV 사람 한 번
                if (all[i].Real == real && UnavoidableCatalog.Feasible(all[i], day, inspections)) pool.Add(all[i]);
            }

            return pool.Count == 0 ? null : pool[_rng.Next(pool.Count)];
        }

        /// <summary>역설 문자를 실제로 보냈다(편성이 아니라 발송 — 확률에서 떨어지면 다음 역설이 여전히 첫 역설이다).</summary>
        public void MarkSent()
        {
            SentInRun = true;
        }

        /// <summary>이상이 있는 공간이면 모호 역설을 보낼 확률(%). 103차(민: 「변조/역설 수칙들이 잘 나오도록」): 75 → 90.</summary>
        public const int SendPercentAnomaly = 90;

        /// <summary>이상이 없는 공간이면 모호 역설을 보낼 확률(%). 103차: 25 → 50(「문자 = 약한 단서」 기울기는 남긴다).</summary>
        public const int SendPercentClear = 50;

        /// <summary>103차: 미세 변조가 열리는 신뢰 구간(옛 3).</summary>
        public const int TamperMinBand = 2;

        /// <summary>103차: 붕괴형 변조가 열리는 신뢰 구간(옛 4). 검은 줄은 4 그대로.</summary>
        public const int CollapseMinBand = 3;

        /// <summary>
        /// 103차(민: 「역설/변조가 적절히 나오고」): 이 날부터는 신뢰 구간 1에서도 미세 변조를 건다 — 신뢰가 낮은 플레이어도 회차 후반에 변조를 본다
        /// (옛: 미숙 플레이어 약 40%가 회차 내내 변조를 못 봄).
        /// </summary>
        public const int TamperLateDay = 3;   // 103차: 4 → 3 — 2일차 첫 역설 → 3일차 첫 변조 → 5일차 붕괴형·회피 불가로 날마다 새것이 하나씩

        /// <summary>
        /// 103차 후속(사용자: 「변조는 수칙이 처음 등장해도 신뢰가 높다면 변조돼서 처음 등장할 수 있는 게 원래 의도」): 이 신뢰 구간부터는 처음 보는 수칙도 변조 후보.
        /// 그 아래(미세 변조)는 원본을 본 적 있는 수칙만 — 원본을 모르면 「?.」 미세 변조는 알아챌 수 없다. 붕괴형 구간(<see cref="CollapseMinBand"/>)과 같게 둔다.
        /// </summary>
        public const int UnseenTamperMinBand = CollapseMinBand;

        /// <summary>그날 그 신뢰 구간에서 변조를 거는지.</summary>
        public static bool TamperOpen(int day, int band)
        {
            return band >= TamperMinBand || (day >= TamperLateDay && band >= 1);
        }

        /// <summary>
        /// 그날 편성. 끝나면 그날 덱을 「본 수칙」에 더한다(다음 밤부터 변조 후보).
        /// </summary>
        /// <param name="spaceHasAnomaly">그 공간에 오늘 이상이 배정됐는지(발송 확률). null이면 모두 이상 없음.</param>
        /// <param name="unavoidable">그날 회피 불가 역설(쌍의 수칙은 역설·변조·검은 줄을 걸지 않는다).</param>
        /// <param name="emptyRoomChannel">그날의 빈 방 채널(K2) — 「CCTV로」 역설은 그 공간을 겨누지 않는다.</param>
        public ParadoxPlan Plan(int day, Band trustBand, IReadOnlyList<RuleDef> deck, Func<SpaceId, bool> spaceHasAnomaly = null,
            UnavoidableDef unavoidable = null, int emptyRoomChannel = -1)
        {
            if (deck == null || deck.Count == 0) return ParadoxPlan.None;
            int band = (int)trustBand;
            StringBuilder note = new StringBuilder();
            note.Append("신뢰 구간 ").Append(band).Append(": ");
            if (unavoidable != null) note.Append("회피 불가 ").Append(unavoidable).Append(' ');
            SpaceId emptySpace = ParadoxRun.SpaceOfChannel(emptyRoomChannel);

            // 1) 변조(신뢰 구간 TamperMinBand+, 103차: 3 → 2) — 모호 역설과 같은 수칙에 걸지 않으므로 먼저 고른다.
            string tampered = null;
            string tamperText = null;
            if (TamperOpen(day, band))
            {
                List<string> pool = new List<string>();
                List<int> weights = new List<int>();
                for (int i = 0; i < deck.Count; i++)
                {
                    string id = deck[i].Id;
                    ParadoxEntry e = ParadoxCatalog.Find(id);
                    if (e == null || !e.HasVariant || Excluded(id)) continue;
                    if (!_seen.Contains(id) && band < UnseenTamperMinBand) continue;   // 103차 후속(사용자: 「신뢰가 높다면 처음 등장하는 수칙도 변조돼서 나올 수 있는 게 원래 의도」)
                    if (unavoidable != null && unavoidable.Has(id)) continue;
                    if (id == ProgramCatalog.FirstParadoxRule && day == ProgramCatalog.FirstParadoxDay) continue;   // 104차: 첫 역설 날(고정 편성 4일차)
                    pool.Add(id);
                    // 103차: 회차에 이미 변조한 수칙은 거의 다시 고르지 않고, 위협 수칙(S5 등)보다 평소 수칙을 앞세운다(옛: 5일차 변조의 60%가 S5).
                    RuleDef def0 = ProgramCatalog.Rule(id);
                    weights.Add((_tamperedInRun.Contains(id) ? 1 : 6) * (def0 != null && (def0.IsThreat || def0.Space == SpaceId.None) ? 1 : 2));   // 공통 수칙(G4)도 한 단 아래
                }

                if (pool.Count > 0)
                {
                    int sum = 0;
                    for (int i = 0; i < weights.Count; i++) sum += weights[i];
                    int roll = _rng.Next(sum);
                    tampered = pool[pool.Count - 1];
                    for (int i = 0; i < pool.Count; i++)
                    {
                        roll -= weights[i];
                        if (roll < 0) { tampered = pool[i]; break; }
                    }

                    _tamperedInRun.Add(tampered);
                    RuleDef def = ProgramCatalog.Rule(tampered);
                    tamperText = band >= CollapseMinBand ? ParadoxCatalog.Find(tampered).Variant : ParadoxCatalog.Subtle(def != null ? def.Text : string.Empty);
                    note.Append("변조 ").Append(tampered).Append(band >= CollapseMinBand ? "(붕괴형) " : "(미세) ");
                }
                else
                {
                    note.Append(band >= UnseenTamperMinBand ? "변조 후보 없음(변조본이 있는 수칙이 없음) " : "변조 후보 없음(이전 밤에 본 수칙 중 변조본이 있는 것이 없음) ");
                }
            }

            // 2) 모호 역설.
            string ambiguous = null;
            bool first = !SentInRun;
            bool dayTwoFirst = day == ProgramCatalog.FirstParadoxDay && (first || ProgramDirector.FixedSchedule) && Has(deck, ProgramCatalog.FirstParadoxRule);   // 104차: 고정 편성은 4일차(소년 날) — 2·3일차에 다른 역설이 먼저 나왔어도 소년 날엔 C2
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
                    if (unavoidable != null && unavoidable.Has(id)) continue;
                    if (e.Pattern == SafeReadPattern.Cctv && emptySpace != SpaceId.None && SpaceIds.Canonical(e.CctvSpace) == emptySpace) continue;   // 그날 빈 방 채널 공간
                    if (id == "K2" && emptyRoomChannel < 0) continue;
                    if (first && e.Pattern != SafeReadPattern.EyesOnly) continue;      // 회차의 첫 역설은 「눈으로만」
                    if (band >= 3 && e.Pattern == SafeReadPattern.Cctv) continue;     // CCTV 고장
                    pool.Add(id);
                }

                if (pool.Count > 0) ambiguous = pool[_rng.Next(pool.Count)];
            }

            bool willSend = false;
            if (ambiguous != null)
            {
                SpaceId space = ParadoxRun.SpaceOf(ambiguous, emptyRoomChannel);
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
                    if (unavoidable != null && unavoidable.Has(id)) continue;
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
            if (ambiguous == null && tampered == null && blacked == null && unavoidable == null)
            {
                return new ParadoxPlan(trustBand, null, false, false, null, null, null, null, note.Append("없음").ToString(), null, emptyRoomChannel);
            }

            return new ParadoxPlan(trustBand, ambiguous, ambiguous != null && first, willSend, tampered, tamperText, blacked, blackedText, note.ToString().TrimEnd(),
                unavoidable, emptyRoomChannel);
        }

        /// <summary>
        /// 겨누지도 변조하지도 않는 수칙 — T4·K4·G1·G3(<see cref="ParadoxCatalog"/> 주석 그대로).
        /// 103차 QA 4회(사용자 결정): G1(복도에서 뛰지 마십시오)은 매일 고정인 기본 수칙이라 검은 줄로 가리면 억울한 위반이 된다 — 옛: T4·K4만 빠져 신뢰 구간 4 밤에 G1이 가려졌다(2000밤 중 126번).
        /// </summary>
        public static bool Excluded(string id)
        {
            return id == ProgramCatalog.ReverseReportRule || id == ProgramCatalog.FinaleRule || id == CommonRunRule || id == ProgramCatalog.FinaleBlankRule;
        }

        /// <summary>공통 수칙 G1(복도에서 뛰지 마십시오).</summary>
        public const string CommonRunRule = "G1";

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

using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace NightDuty
{
    /// <summary>
    /// 하루치 근무수칙 덱을 24장 풀에서 고른다(2026-09-21 밸런스 재설계안 7절 「모델 A」).
    /// <list type="bullet">
    /// <item><b>축 쿼터로 뽑는다</b> — 배치 2 · 청각 2 · 조도 2. 고정 편성표(<see cref="NightDeckTableSO"/>)가
    /// 하루를 한 공간으로 몰아 주던 것을 대체한다. 축을 골고루 배정해야 세 감각축이 같은 속도로 오르고,
    /// 조도·청각이 영영 Band0에 묶이던 데드락이 재발하지 않는다.</item>
    /// <item><b>자격 미달 카드는 후보에서 뺀다</b>(<see cref="RuleSO.IsEligible"/>). 배정은 <b>밤 시작 한 번</b>이므로,
    /// 밤 도중 축이 올라 자격을 새로 얻는 카드가 있어도 그날 덱에는 들어오지 않는다.</item>
    /// <item><b>가중 비복원 추첨</b>이다. 그날 새로 열린 카드를 크게 띄우는 것이 핵심이다 —
    /// 축·델타·게이지를 화면에 못 그리는 이 게임에서 「축이 올랐다」를 플레이어에게 알릴 수 있는 창구가
    /// 다음 날 수칙 목록뿐이기 때문이다(CLAUDE.md §2.8 표시 금기).</item>
    /// <item><b>하드 제약은 재시도로 지킨다.</b> 가중치로 달래는 것이 아니라, 어긴 조합은 통째로 버리고 다시 뽑는다.
    /// 가중치는 확률만 기울일 뿐 「절대 같이 나오면 안 되는」 조합을 막지 못한다.</item>
    /// </list>
    /// <para>
    /// <b>회차 내내 살아 있어야 한다.</b> 카드별 마지막 등장 일차·누적 등장 횟수·어제 자격 통과 여부를 들고 있어서
    /// 「어제 나왔다」 「사흘째 안 나왔다」 「오늘 새로 열렸다」를 판단한다. 새 회차는 <see cref="NightRun.StartNewRun"/>에서
    /// 새 인스턴스를 만들거나 <see cref="Reset"/>으로 시작한다.
    /// </para>
    /// <para>
    /// 난수는 생성자에서 <b>주입</b>한다. 테스트가 시드를 고정해 같은 덱을 재현할 수 있어야 하고,
    /// 회차 시드를 세이브에 넣으면 덱 자체를 직렬화하지 않아도 되기 때문이다(재설계안 7절 「시드 2단 계층」).
    /// </para>
    /// </summary>
    public sealed class DayDirector
    {
        /// <summary>하루 덱 크기. 2일차부터는 축 쿼터 합(2+2+2)과 같고, 1일차만 S1이 한 자리를 가져간다.</summary>
        public const int DeckSize = 6;

        /// <summary>배치(<see cref="FearAxis.Layout"/>) 쿼터.</summary>
        public const int QuotaLayout = 2;

        /// <summary>청각(<see cref="FearAxis.Auditory"/>) 쿼터.</summary>
        public const int QuotaAuditory = 2;

        /// <summary>
        /// 조도(<see cref="FearAxis.Illuminance"/>) 쿼터.
        /// <b>1일차만 1장</b>이다 — S1(최초 조우를 겸하는 고정 카드)이 여섯 자리 중 하나를 가져가므로,
        /// 어느 축에서 한 자리를 빼야 한다. 조도에서 빼는 이유는 조도 카드가 5장으로 가장 적어
        /// 1일차에 두 장을 쓰면 나머지 나흘의 조도 슬롯 9개를 3장으로 메워야 하기 때문이다.
        /// </summary>
        public const int QuotaIlluminance = 2;

        /// <summary>1일차 조도 쿼터. <see cref="FirstDayFixedCardId"/>가 한 자리를 가져간 몫이다.</summary>
        public const int QuotaIlluminanceFirstDay = 1;

        /// <summary>1일차에만 고정 배정하는 카드. 최초 조우(S-A)를 여는 카드라 자리를 따로 받는다.</summary>
        public const string FirstDayFixedCardId = "S1";

        /// <summary>
        /// 회차 재등장 한도 — 배치. 배치 카드가 10장으로 가장 많아 한도를 낮게 둬도 슬롯(회차 14칸)이 돈다.
        /// 한도를 넘긴 카드는 후보에서 아예 뺀다.
        /// </summary>
        public const int CapLayout = 2;

        /// <summary>회차 재등장 한도 — 청각(카드 8장).</summary>
        public const int CapAuditory = 2;

        /// <summary>
        /// 회차 재등장 한도 — 조도. 조도만 3회인 것은 의도다. 카드가 5장뿐인데 회차 슬롯은 9칸이라
        /// 2회로 묶으면 5일차에 뽑을 카드가 남지 않는다.
        /// <para>
        /// 「같은 카드 = 같은 경험」이 아니다. H3는 1일차 6500K, 3일차 4500K, 5일차 2000K에서 나온다 —
        /// 반복감을 정하는 것은 카드가 아니라 구간이다.
        /// </para>
        /// </summary>
        public const int CapIlluminance = 3;

        /// <summary>그날 새로 열린 카드 가중치. 「축이 올랐다」를 수칙 목록으로 체감시키는 유일한 창구라 가장 크다.</summary>
        public const double WeightNewlyOpened = 3.0;

        /// <summary>연속 미등장 1일마다 더해지는 가중치 기울기. 방치된 카드가 회차 끝까지 안 나오는 것을 막는다.</summary>
        public const double WeightMissStep = 0.5;

        /// <summary>직전 날 등장한 카드에 곱하는 감쇠. 이틀 연속 같은 수칙이 뜨면 편성이 굳어 보인다.</summary>
        public const double WeightPlayedYesterday = 0.3;

        /// <summary>
        /// 하드 제약 재시도 상한. 재설계안의 1000회차 시뮬레이션에서 후퇴가 회차당 0.24회였으므로
        /// 실제로는 두세 번이면 통과한다. 24는 「풀이 구조적으로 조합을 못 만드는 날」에 무한 루프를 막는 안전장치다.
        /// </summary>
        public const int MaxAttempts = 24;

        /// <summary>하루 덱이 덮어야 하는 최소 공간 수. 4곳 중 3곳.</summary>
        public const int MinSpaceCount = 3;

        /// <summary>같은 날 함께 배정하면 안 되는 카드 쌍의 한쪽(T1 「수동 개폐 금지」).</summary>
        public const string ConflictCardA = "T1";

        /// <summary>같은 날 함께 배정하면 안 되는 카드 쌍의 다른 쪽(T3 「닫고 나가라」). T1과 정면으로 부딪힌다.</summary>
        public const string ConflictCardB = "T3";

        /// <summary>
        /// 공간 확산을 세는 대상 4곳. <see cref="SpaceId.Classroom_1_3"/>은 카드가 한 장도 없어(교실 카드는 전부 1-1)
        /// 여기에 넣지 않는다 — 넣으면 절대 못 채우는 칸이 하나 생긴다.
        /// </summary>
        private static readonly SpaceId[] CountedSpaces =
        {
            SpaceId.Corridor,
            SpaceId.Classroom_1_1,
            SpaceId.ScienceRoom,
            SpaceId.Toilet
        };

        private readonly List<RuleSO> _pool = new List<RuleSO>();
        private readonly System.Random _rng;

        // 회차 이력 — 풀 인덱스와 1:1. Dictionary 대신 배열인 이유는 풀이 생성 시점에 고정되기 때문이다.
        private readonly int[] _lastDay;          // 마지막 등장 일차. 한 번도 안 나왔으면 0.
        private readonly int[] _timesUsed;        // 회차 누적 등장 횟수.
        private readonly bool[] _eligibleLastBuild;   // 직전 BuildDeck 시점의 자격 통과 여부.

        // 하루 계산용 작업 버퍼. 밤마다 새로 할당하지 않으려고 들고 있는다.
        private readonly bool[] _eligibleToday;
        private readonly bool[] _newlyOpened;
        private readonly double[] _weight;
        private readonly bool[] _taken;
        private readonly List<int> _candidates = new List<int>();
        private readonly List<int> _attempt = new List<int>();
        private readonly List<int> _best = new List<int>();
        private readonly int[] _need = new int[3];
        private readonly int[] _attemptShort = new int[3];
        private readonly int[] _bestShort = new int[3];

        private int _lastBuildDay;
        private int _lastAttempts;
        private List<RuleSO> _lastDeck = new List<RuleSO>();
        private string _lastReport = string.Empty;

        /// <summary>
        /// 회차용 편성기를 만든다.
        /// </summary>
        /// <param name="pool">카드 풀(보통 24장). null 항목과 <see cref="FearAxis.Trust"/> 위반축 카드는 버린다 —
        /// 신뢰는 준수로만 오르므로 축 쿼터에 넣을 자리가 없다.</param>
        /// <param name="rng">난수. null이면 시드 없는 인스턴스를 만든다. 테스트는 시드를 고정한 것을 넣는다.</param>
        public DayDirector(IReadOnlyList<RuleSO> pool, System.Random rng = null)
        {
            _rng = rng ?? new System.Random();

            if (pool == null || pool.Count == 0)
            {
                Debug.LogWarning("[DayDirector] 카드 풀이 비어 있습니다. 덱은 항상 빈 목록이 됩니다.");
            }
            else
            {
                for (int i = 0; i < pool.Count; i++)
                {
                    RuleSO card = pool[i];
                    if (card == null)
                    {
                        continue;
                    }

                    if (AxisSlotOf(card) < 0)
                    {
                        Debug.LogWarning("[DayDirector] " + IdOf(card) + ": 위반축이 청각·조도·배치가 아니라 편성에서 제외합니다.");
                        continue;
                    }

                    _pool.Add(card);
                }
            }

            int n = _pool.Count;
            _lastDay = new int[n];
            _timesUsed = new int[n];
            _eligibleLastBuild = new bool[n];
            _eligibleToday = new bool[n];
            _newlyOpened = new bool[n];
            _weight = new double[n];
            _taken = new bool[n];
        }

        /// <summary>
        /// 디버그용 한 줄 요약. 「왜 이 조합이 나왔는지」 — 뽑힌 카드 · 축 분포 · 후보 수 · 신규 개방 · 시도 횟수 ·
        /// 제약 통과 여부 · 쿼터 미달이 들어 있다. 편성이 이상해 보일 때 가장 먼저 볼 문자열이다.
        /// </summary>
        public string LastReport
        {
            get { return _lastReport; }
        }

        /// <summary>
        /// 회차를 초기화한다. 등장 이력·자격 스냅숏·직전 결과를 전부 지운다.
        /// <b>난수는 초기화하지 않는다</b> — 시드를 다시 심는 것은 호출자의 몫이고,
        /// 여기서 새 <see cref="System.Random"/>을 만들면 주입한 시드가 조용히 버려진다.
        /// </summary>
        public void Reset()
        {
            for (int i = 0; i < _pool.Count; i++)
            {
                _lastDay[i] = 0;
                _timesUsed[i] = 0;
                _eligibleLastBuild[i] = false;
            }

            _lastBuildDay = 0;
            _lastAttempts = 0;
            _lastDeck = new List<RuleSO>();
            _lastReport = string.Empty;
        }

        /// <summary>
        /// 그날 덱을 만든다(표시 순서대로). <b>부작용으로 등장 이력을 갱신한다.</b>
        /// <para>
        /// 같은 일차로 두 번 부르면 이력이 두 번 올라 재등장 한도가 어긋나므로, 경고를 남기고 직전 결과를 그대로 돌려준다.
        /// 다시 뽑고 싶으면 <see cref="Reset"/>으로 회차를 새로 시작해야 한다.
        /// </para>
        /// </summary>
        /// <param name="day">일차(1부터).</param>
        /// <param name="axes">밤 시작 시점의 회차 축. 자격 검사에 쓴다. null이면 구간 자격이 있는 카드가 전부 떨어진다.</param>
        public IReadOnlyList<RuleSO> BuildDeck(int day, IFearAxisReader axes)
        {
            if (day < 1)
            {
                Debug.LogWarning("[DayDirector] 일차 " + day + "는 1보다 작습니다. 1일차로 취급합니다.");
                day = 1;
            }

            if (day == _lastBuildDay)
            {
                Debug.LogWarning("[DayDirector] " + day + "일차 덱을 두 번 요청했습니다. 이력이 두 번 오르지 않도록 직전 결과를 그대로 돌려줍니다.");
                return _lastDeck;
            }

            if (axes == null)
            {
                Debug.LogWarning("[DayDirector] 축이 null이라 구간 자격이 걸린 카드는 모두 후보에서 빠집니다.");
            }

            SnapshotEligibility(day, axes);
            ComputeWeights(day);
            CollectCandidates(day);

            _lastAttempts = DrawWithConstraints(day);

            // 이력 갱신은 최종 조합이 정해진 뒤 한 번만. 재시도 중에 갱신하면 버린 조합이 가중치를 오염시킨다.
            Commit(day);

            return _lastDeck;
        }

        /// <summary>
        /// 오늘의 자격과 「오늘 새로 열렸는가」를 기록한다.
        /// <para>
        /// 새로 열린 카드 = <b>직전 편성 때는 자격 미달이었는데 오늘은 통과</b>한 카드다.
        /// 1일차에는 비교할 어제가 없으므로 한 장도 새로 열리지 않은 것으로 본다.
        /// </para>
        /// </summary>
        private void SnapshotEligibility(int day, IFearAxisReader axes)
        {
            bool hasYesterday = _lastBuildDay > 0;

            for (int i = 0; i < _pool.Count; i++)
            {
                bool eligible = _pool[i].IsEligible(axes);
                _eligibleToday[i] = eligible;
                _newlyOpened[i] = hasYesterday && eligible && !_eligibleLastBuild[i];
            }
        }

        /// <summary>
        /// 후보별 가중치를 곱으로 계산한다. 자격 미달은 0.
        /// <list type="bullet">
        /// <item>그날 새로 열린 카드 ×3.0</item>
        /// <item>연속 미등장 일수 ×(1 + 0.5 × 일수)</item>
        /// <item>직전 날 등장 ×0.3</item>
        /// </list>
        /// <para>
        /// 가중치는 하루 동안 고정이다(이력이 밤 중에 바뀌지 않으므로). 그래서 재시도마다 다시 계산하지 않는다.
        /// 직전 날 등장한 카드는 미등장 일수가 0이라 두 번째 항이 1.0이고, 결과적으로 ×0.3만 남는다.
        /// </para>
        /// </summary>
        private void ComputeWeights(int day)
        {
            for (int i = 0; i < _pool.Count; i++)
            {
                if (!_eligibleToday[i])
                {
                    _weight[i] = 0.0;
                    continue;
                }

                double w = 1.0;

                if (_newlyOpened[i])
                {
                    w *= WeightNewlyOpened;
                }

                // 마지막 등장이 0(미등장)이면 어제까지의 날 수가 통째로 미등장 일수가 된다.
                int missed = (day - 1) - _lastDay[i];
                if (missed < 0)
                {
                    missed = 0;
                }

                w *= 1.0 + WeightMissStep * missed;

                if (_lastDay[i] > 0 && _lastDay[i] == day - 1)
                {
                    w *= WeightPlayedYesterday;
                }

                _weight[i] = w;
            }
        }

        /// <summary>
        /// 오늘 뽑을 수 있는 카드를 모은다. 여기서 빠지는 것은 추첨 자체에 못 들어온다.
        /// <list type="bullet">
        /// <item>자격 미달(하드 제약 1)</item>
        /// <item>회차 재등장 한도 초과</item>
        /// <item><see cref="FirstDayFixedCardId"/> — 1일차에는 고정 자리로 따로 넣고, 2일차부터는 하드 제약 2로 제외</item>
        /// </list>
        /// </summary>
        private void CollectCandidates(int day)
        {
            _candidates.Clear();

            for (int i = 0; i < _pool.Count; i++)
            {
                if (!_eligibleToday[i])
                {
                    continue;
                }

                if (IsFirstDayFixed(_pool[i]))
                {
                    continue;
                }

                if (_timesUsed[i] >= CapOf(AxisSlotOf(_pool[i])))
                {
                    continue;
                }

                _candidates.Add(i);
            }
        }

        /// <summary>
        /// 하드 제약을 만족할 때까지 다시 뽑는다.
        /// <para>
        /// 상한을 넘기면 <b>위반이 가장 적었던 조합</b>을 쓰고 경고를 남긴다. 덱 없이 밤을 시작하면
        /// 그날 판정이 통째로 사라지므로, 제약을 못 지키는 것보다 나쁜 결과다.
        /// </para>
        /// </summary>
        /// <returns>실제로 돌린 시도 횟수.</returns>
        private int DrawWithConstraints(int day)
        {
            int bestViolations = int.MaxValue;
            _best.Clear();

            int attempt = 0;
            while (attempt < MaxAttempts)
            {
                attempt++;
                DrawOnce(day);

                int violations = CountViolations(_attempt, day, null);
                if (violations < bestViolations)
                {
                    bestViolations = violations;
                    CopyTo(_attempt, _best);
                    CopyShortfall(_attemptShort, _bestShort);
                }

                if (violations == 0)
                {
                    break;
                }
            }

            if (bestViolations > 0)
            {
                StringBuilder reason = new StringBuilder();
                CountViolations(_best, day, reason);
                Debug.LogWarning("[DayDirector] " + day + "일차: " + MaxAttempts + "번 다시 뽑아도 하드 제약을 지키지 못했습니다(" +
                                 reason + "). 위반이 가장 적은 조합으로 진행합니다.");
            }

            WarnShortfall(day);
            return attempt;
        }

        /// <summary>
        /// 한 번 뽑는다. 순서가 중요하다.
        /// <list type="bullet">
        /// <item><b>1일차 고정 S1</b> — 최초 조우를 여는 카드라 추첨 대상이 아니다.</item>
        /// <item><b>새로 열린 카드 1장 강제</b> — 가중치로는 「운이 나쁘면 한 장도 안 나오는」 날이 생긴다.
        /// 축이 오른 것을 알릴 창구가 여기뿐이라 확률이 아니라 규칙으로 못박는다. 이 카드는 자기 축의 쿼터를 한 칸 쓴다.</item>
        /// <item><b>축 쿼터</b> — 배치 → 청각 → 조도 순. 후보가 모자라면 <b>다른 축에서 메우지 않고 덜 뽑는다.</b>
        /// 억지로 채우면 축 균형이라는 하드 제약이 깨지고, 그게 처음의 데드락을 만든 원인이다.</item>
        /// </list>
        /// </summary>
        private void DrawOnce(int day)
        {
            _attempt.Clear();
            for (int i = 0; i < _taken.Length; i++)
            {
                _taken[i] = false;
            }

            _need[0] = QuotaLayout;
            _need[1] = QuotaAuditory;
            _need[2] = day == 1 ? QuotaIlluminanceFirstDay : QuotaIlluminance;

            if (day == 1)
            {
                int fixedIndex = FindFirstDayFixed();
                if (fixedIndex >= 0)
                {
                    Take(fixedIndex);
                }
                else
                {
                    Debug.LogWarning("[DayDirector] 1일차 고정 카드 " + FirstDayFixedCardId + "가 풀에 없거나 자격 미달입니다. 5장으로 시작합니다.");
                }
            }

            int newOne = PickWeighted(-1, true);
            if (newOne >= 0)
            {
                Take(newOne);
                int slot = AxisSlotOf(_pool[newOne]);
                if (_need[slot] > 0)
                {
                    _need[slot]--;
                }
            }

            for (int slot = 0; slot < 3; slot++)
            {
                _attemptShort[slot] = 0;

                for (int k = 0; k < _need[slot]; k++)
                {
                    int picked = PickWeighted(slot, false);
                    if (picked < 0)
                    {
                        // 이 축은 여기까지. 남은 자리는 비운다.
                        _attemptShort[slot] = _need[slot] - k;
                        break;
                    }

                    Take(picked);
                }
            }

            // TODO(조우 시스템): EncounterDirector가 생기면 여기에 두 가지를 더한다.
            //   ① 그날 조우 장면이 쓰는 공간의 카드를 최소 1장 강제(쿼터 밖 추가 슬롯).
            //   ② S2가 활성인 동안에는 S-B 장면을 금지 — 장면 잠금을 덱 제약으로 옮긴 것이라 여기서 검사해야 한다.
        }

        /// <summary>카드를 덱에 넣고 중복 추첨을 막는다.</summary>
        private void Take(int index)
        {
            _taken[index] = true;
            _attempt.Add(index);
        }

        /// <summary>
        /// 가중 비복원 추첨 한 장.
        /// </summary>
        /// <param name="axisSlot">뽑을 축(0 배치 · 1 청각 · 2 조도). -1이면 축을 가리지 않는다.</param>
        /// <param name="newlyOnly">true면 「오늘 새로 열린 카드」만 본다.</param>
        /// <returns>뽑힌 풀 인덱스. 후보가 없으면 -1.</returns>
        private int PickWeighted(int axisSlot, bool newlyOnly)
        {
            double total = 0.0;
            for (int c = 0; c < _candidates.Count; c++)
            {
                int i = _candidates[c];
                if (!Accepts(i, axisSlot, newlyOnly))
                {
                    continue;
                }

                total += _weight[i];
            }

            if (total <= 0.0)
            {
                return -1;
            }

            double roll = _rng.NextDouble() * total;
            int last = -1;

            for (int c = 0; c < _candidates.Count; c++)
            {
                int i = _candidates[c];
                if (!Accepts(i, axisSlot, newlyOnly))
                {
                    continue;
                }

                last = i;
                roll -= _weight[i];
                if (roll <= 0.0)
                {
                    return i;
                }
            }

            // 부동소수 누적 오차로 마지막 칸을 넘어갈 수 있다. 그때는 마지막 후보를 쓴다.
            return last;
        }

        /// <summary>추첨 후보로 받을 수 있는 카드인지(이미 뽑힘 · 축 · 신규 개방 필터).</summary>
        private bool Accepts(int index, int axisSlot, bool newlyOnly)
        {
            if (_taken[index] || _weight[index] <= 0.0)
            {
                return false;
            }

            if (newlyOnly && !_newlyOpened[index])
            {
                return false;
            }

            if (axisSlot >= 0 && AxisSlotOf(_pool[index]) != axisSlot)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// 하드 제약 위반 수를 센다. 0이면 통과.
        /// <list type="bullet">
        /// <item><b>T1·T3 동일일 금지</b> — 「수동으로 열지 마라」와 「닫고 나가라」가 정면으로 부딪혀,
        /// 같은 날 받으면 어느 쪽을 지켜도 다른 쪽을 어기게 된다.</item>
        /// <item><b>S1은 1일차에만</b> — 최초 조우를 여는 카드라 둘째 날 이후에 나오면 서사가 뒤집힌다.</item>
        /// <item><b>4공간 중 최소 3곳</b> — 두 공간으로 하루가 끝나면 순찰이 왕복 두 번으로 줄어 하루가 비어 버린다.</item>
        /// </list>
        /// </summary>
        /// <param name="reason">null이 아니면 어긴 항목을 적는다.</param>
        private int CountViolations(List<int> deck, int day, StringBuilder reason)
        {
            int violations = 0;

            bool hasA = false;
            bool hasB = false;
            bool hasFixed = false;

            for (int k = 0; k < deck.Count; k++)
            {
                string id = IdOf(_pool[deck[k]]);
                if (id == ConflictCardA)
                {
                    hasA = true;
                }

                if (id == ConflictCardB)
                {
                    hasB = true;
                }

                if (id == FirstDayFixedCardId)
                {
                    hasFixed = true;
                }
            }

            if (hasA && hasB)
            {
                violations++;
                Append(reason, ConflictCardA + "·" + ConflictCardB + " 동일일");
            }

            if (hasFixed && day != 1)
            {
                violations++;
                Append(reason, FirstDayFixedCardId + "가 " + day + "일차에 배정됨");
            }

            int spaces = CountSpaces(deck);
            if (spaces < MinSpaceCount)
            {
                violations++;
                Append(reason, "공간 " + spaces + "곳(최소 " + MinSpaceCount + ")");
            }

            return violations;
        }

        /// <summary>덱이 덮는 공간 수(<see cref="CountedSpaces"/> 기준).</summary>
        private int CountSpaces(List<int> deck)
        {
            int count = 0;

            for (int s = 0; s < CountedSpaces.Length; s++)
            {
                for (int k = 0; k < deck.Count; k++)
                {
                    if (_pool[deck[k]].Space == CountedSpaces[s])
                    {
                        count++;
                        break;
                    }
                }
            }

            return count;
        }

        /// <summary>쿼터를 못 채운 축이 있으면 개발 로그에 남긴다. 덜 뽑는 것 자체는 설계상 정상 동작이다.</summary>
        private void WarnShortfall(int day)
        {
            int total = _bestShort[0] + _bestShort[1] + _bestShort[2];
            if (total <= 0)
            {
                return;
            }

            Debug.LogWarning("[DayDirector] " + day + "일차: 축 쿼터를 " + total + "자리 못 채웠습니다(" +
                             ShortfallText() + "). 다른 축에서 메우지 않고 덱을 " + (_best.Count) + "장으로 둡니다 — " +
                             "자격을 통과한 카드가 그 축에 남아 있지 않다는 뜻입니다.");
        }

        /// <summary>최종 조합을 이력에 반영하고 결과 목록과 보고 문자열을 만든다.</summary>
        private void Commit(int day)
        {
            List<RuleSO> deck = new List<RuleSO>(_best.Count);
            OrderDeck(_best, deck);

            for (int k = 0; k < _best.Count; k++)
            {
                int i = _best[k];
                _lastDay[i] = day;
                _timesUsed[i]++;
            }

            for (int i = 0; i < _pool.Count; i++)
            {
                _eligibleLastBuild[i] = _eligibleToday[i];
            }

            _lastBuildDay = day;
            _lastDeck = deck;
            _lastReport = BuildReport(day, deck);
        }

        /// <summary>
        /// 표시 순서를 정한다. <b>이 순서가 곧 동시 성립 우선순위</b>이므로(공통 명세 1절 7·8항),
        /// 다시 나올 기회가 적은 카드를 앞에 둔다.
        /// <list type="bullet">
        /// <item>1일차 고정 S1이 맨 앞 — 그날의 척추다.</item>
        /// <item>장기 카드가 그다음 — 밤 시작(<c>RuleBook.BeginNight</c>)에 이미 무장돼 있어야 한다.</item>
        /// <item>나머지는 뽑힌 순서 그대로(축 쿼터 순: 배치 → 청각 → 조도).</item>
        /// </list>
        /// <para>
        /// TODO: 재설계안 7절의 전체 순서 점수(조우 연결 · 상한형/창형 · 자격 하한 구간)는
        /// 조우 시스템과 함께 별도 비교기로 들어온다. 지금은 그중 확실한 두 항목만 쓴다.
        /// </para>
        /// </summary>
        private void OrderDeck(List<int> picked, List<RuleSO> into)
        {
            for (int k = 0; k < picked.Count; k++)
            {
                if (IsFirstDayFixed(_pool[picked[k]]))
                {
                    into.Add(_pool[picked[k]]);
                }
            }

            for (int k = 0; k < picked.Count; k++)
            {
                RuleSO card = _pool[picked[k]];
                if (!IsFirstDayFixed(card) && card.IsLongTerm)
                {
                    into.Add(card);
                }
            }

            for (int k = 0; k < picked.Count; k++)
            {
                RuleSO card = _pool[picked[k]];
                if (!IsFirstDayFixed(card) && !card.IsLongTerm)
                {
                    into.Add(card);
                }
            }
        }

        /// <summary>디버그 한 줄 요약을 만든다.</summary>
        private string BuildReport(int day, List<RuleSO> deck)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("[Day ").Append(day).Append("] ");

            for (int k = 0; k < deck.Count; k++)
            {
                if (k > 0)
                {
                    sb.Append(',');
                }

                sb.Append(IdOf(deck[k]));
            }

            int[] byAxis = new int[3];
            for (int k = 0; k < deck.Count; k++)
            {
                int slot = AxisSlotOf(deck[k]);
                if (slot >= 0)
                {
                    byAxis[slot]++;
                }
            }

            sb.Append(" | 축 배치").Append(byAxis[0]).Append("/청각").Append(byAxis[1]).Append("/조도").Append(byAxis[2]);
            sb.Append(" | 공간 ").Append(CountSpaces(_best)).Append('/').Append(CountedSpaces.Length);
            sb.Append(" | 후보 ").Append(_candidates.Count);

            int newCount = 0;
            for (int i = 0; i < _pool.Count; i++)
            {
                if (_newlyOpened[i])
                {
                    newCount++;
                }
            }

            int newInDeck = 0;
            for (int k = 0; k < deck.Count; k++)
            {
                for (int i = 0; i < _pool.Count; i++)
                {
                    if (_pool[i] == deck[k] && _newlyOpened[i])
                    {
                        newInDeck++;
                        break;
                    }
                }
            }

            sb.Append(" | 신규개방 ").Append(newCount).Append("(덱 ").Append(newInDeck).Append(')');
            sb.Append(" | 시도 ").Append(_lastAttempts).Append('회');

            if (_bestShort[0] + _bestShort[1] + _bestShort[2] > 0)
            {
                sb.Append(" | 쿼터미달 ").Append(ShortfallText());
            }

            return sb.ToString();
        }

        /// <summary>미달 자리 수를 축 이름과 함께 적는다.</summary>
        private string ShortfallText()
        {
            StringBuilder sb = new StringBuilder();

            for (int slot = 0; slot < 3; slot++)
            {
                if (_bestShort[slot] <= 0)
                {
                    continue;
                }

                if (sb.Length > 0)
                {
                    sb.Append(' ');
                }

                sb.Append(AxisNameOf(slot)).Append(_bestShort[slot]);
            }

            return sb.ToString();
        }

        /// <summary>풀에서 1일차 고정 카드를 찾는다. 자격 미달이거나 한도를 넘었으면 -1.</summary>
        private int FindFirstDayFixed()
        {
            for (int i = 0; i < _pool.Count; i++)
            {
                if (!IsFirstDayFixed(_pool[i]))
                {
                    continue;
                }

                if (!_eligibleToday[i] || _timesUsed[i] >= CapOf(AxisSlotOf(_pool[i])))
                {
                    return -1;
                }

                return i;
            }

            return -1;
        }

        private static bool IsFirstDayFixed(RuleSO card)
        {
            return IdOf(card) == FirstDayFixedCardId;
        }

        private static string IdOf(RuleSO card)
        {
            if (card == null)
            {
                return string.Empty;
            }

            return card.CardId ?? string.Empty;
        }

        /// <summary>
        /// 카드가 속한 축 칸(0 배치 · 1 청각 · 2 조도). 신뢰나 그 밖의 값은 -1.
        /// <para>
        /// 소속 축을 <see cref="RuleSO.FailureAxis"/>로 정하는 이유: 이 카드를 어겼을 때 실제로 오르는 축이
        /// 곧 그 카드가 밀어 올리는 축이기 때문이다. 자격 축(<see cref="RuleSO.EligibleAxis"/>)은
        /// 「언제 열리는가」이지 「무엇을 올리는가」가 아니라 쿼터의 기준이 될 수 없다.
        /// </para>
        /// <para>
        /// 주의: 위반이 없는 준수 전용 카드(S1)는 위반축이 기본값(배치)으로 남아 있다. S1은 어차피
        /// 1일차 고정 자리로만 들어가므로 쿼터 계산에 끼어들지 않는다.
        /// </para>
        /// </summary>
        private static int AxisSlotOf(RuleSO card)
        {
            if (card == null)
            {
                return -1;
            }

            switch (card.FailureAxis)
            {
                case FearAxis.Layout:
                    return 0;
                case FearAxis.Auditory:
                    return 1;
                case FearAxis.Illuminance:
                    return 2;
                default:
                    return -1;
            }
        }

        /// <summary>축 칸의 회차 재등장 한도.</summary>
        private static int CapOf(int axisSlot)
        {
            switch (axisSlot)
            {
                case 0:
                    return CapLayout;
                case 1:
                    return CapAuditory;
                case 2:
                    return CapIlluminance;
                default:
                    return 0;
            }
        }

        /// <summary>로그용 축 이름.</summary>
        private static string AxisNameOf(int axisSlot)
        {
            switch (axisSlot)
            {
                case 0:
                    return "배치";
                case 1:
                    return "청각";
                case 2:
                    return "조도";
                default:
                    return "기타";
            }
        }

        private static void CopyTo(List<int> from, List<int> to)
        {
            to.Clear();
            for (int i = 0; i < from.Count; i++)
            {
                to.Add(from[i]);
            }
        }

        private static void CopyShortfall(int[] from, int[] to)
        {
            for (int i = 0; i < to.Length; i++)
            {
                to[i] = from[i];
            }
        }

        private static void Append(StringBuilder sb, string text)
        {
            if (sb == null)
            {
                return;
            }

            if (sb.Length > 0)
            {
                sb.Append(", ");
            }

            sb.Append(text);
        }
    }
}

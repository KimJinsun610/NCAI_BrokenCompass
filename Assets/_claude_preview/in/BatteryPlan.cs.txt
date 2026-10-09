using System;
using System.Collections.Generic;
using System.Text;

namespace NightDuty
{
    /// <summary>배터리를 둘 수 있는 칸 하나(화면 쪽이 씬의 여닫는 수납에서 모아 넘긴다).</summary>
    public sealed class BatteryCache
    {
        /// <summary>칸 ID(씬 계층 경로 — 밤마다 같다).</summary>
        public readonly string Id;

        /// <summary>뽑힐 무게(경비실에서 멀수록 크게).</summary>
        public readonly float Weight;

        /// <summary>잠긴 사물함인지(밤마다 몇 개만 풀어 후보로 쓴다).</summary>
        public readonly bool IsLocker;

        /// <summary>1일차에 하나를 확정으로 두는 칸인지(순찰 공간 안 — 배우기).</summary>
        public readonly bool Starter;

        /// <summary>칸을 만든다.</summary>
        public BatteryCache(string id, float weight, bool isLocker = false, bool starter = false)
        {
            Id = id ?? string.Empty;
            Weight = weight > 0f ? weight : 0.01f;
            IsLocker = isLocker;
            Starter = starter;
        }
    }

    /// <summary>
    /// 그 밤 배터리가 놓이는 칸(56차). 밤 시작에 빈 채로 만들고, 화면 쪽이 씬의 칸을 모아 <see cref="Fill"/>로 한 번 채운다.
    /// <list type="bullet">
    /// <item>잠긴 사물함 중 <see cref="BatteryRules.UnlockedLockers"/>개를 풀어 후보에 넣는다(<see cref="Unlocked"/>) — 어느 것이 풀렸는지는 열어 봐야 안다.</item>
    /// <item>후보 중 <see cref="BatteryRules.PlacedOn"/>개를 무게대로 뽑는다(경비실에서 먼 칸일수록 잘 뽑힌다). 전날 놓였던 칸은 피한다(모자라면 쓴다).</item>
    /// <item>1일차는 순찰 공간 칸(<see cref="BatteryCache.Starter"/>) 하나를 먼저 확정한다.</item>
    /// <item>재시작 스냅샷에는 <b>주운 칸</b>만 싣는다 — 놓인 자리는 밤 시작에 정해져 재시작해도 같다.</item>
    /// </list>
    /// </summary>
    public sealed class BatteryPlan : ISnapshotable
    {
        /// <summary>스냅샷 키.</summary>
        public const string Key = "battery.plan";

        private readonly int _day;
        private readonly Random _rng;
        private readonly List<string> _placed = new List<string>();
        private readonly HashSet<string> _taken = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<string> _unlocked = new List<string>();
        private readonly HashSet<string> _avoid;

        /// <summary>그 밤의 빈 계획을 만든다. <paramref name="avoid"/>는 전날 놓였던 칸.</summary>
        public BatteryPlan(int day, IEnumerable<string> avoid, Random rng)
        {
            _day = day;
            _rng = rng ?? new Random();
            _avoid = new HashSet<string>(avoid ?? new string[0], StringComparer.Ordinal);
        }

        /// <summary>칸을 채웠는지.</summary>
        public bool Filled { get; private set; }

        /// <summary>배터리가 놓인 칸(주웠어도 남는다).</summary>
        public IReadOnlyList<string> Placed
        {
            get { return _placed; }
        }

        /// <summary>그 밤 풀어 둔 사물함.</summary>
        public IReadOnlyList<string> Unlocked
        {
            get { return _unlocked; }
        }

        /// <summary>아직 줍지 않은 배터리 수.</summary>
        public int Remaining
        {
            get { return _placed.Count - _taken.Count; }
        }

        /// <summary>그 칸에 아직 배터리가 있는지.</summary>
        public bool Holds(string id)
        {
            return id != null && _placed.Contains(id) && !_taken.Contains(id);
        }

        /// <summary>그 칸의 배터리를 집는다. 없으면 false.</summary>
        public bool Take(string id)
        {
            if (!Holds(id)) return false;
            _taken.Add(id);
            return true;
        }

        /// <summary>씬의 칸으로 그 밤 자리를 정한다(한 번만 — 두 번째부터는 무시).</summary>
        public void Fill(IList<BatteryCache> caches)
        {
            if (Filled) return;
            Filled = true;
            if (caches == null || caches.Count == 0) return;

            List<BatteryCache> pool = new List<BatteryCache>();
            List<BatteryCache> lockers = new List<BatteryCache>();
            for (int i = 0; i < caches.Count; i++)
            {
                if (caches[i] == null || caches[i].Id.Length == 0) continue;
                if (caches[i].IsLocker) lockers.Add(caches[i]);
                else pool.Add(caches[i]);
            }

            for (int i = 0; i < BatteryRules.UnlockedLockers && lockers.Count > 0; i++)
            {
                int k = _rng.Next(lockers.Count);
                _unlocked.Add(lockers[k].Id);
                pool.Add(lockers[k]);
                lockers.RemoveAt(k);
            }

            int want = Math.Min(BatteryRules.PlacedOn(_day), pool.Count);
            if (_day <= 1 && want > 0)
            {
                List<BatteryCache> starters = pool.FindAll(c => c.Starter);
                if (starters.Count > 0) PickFrom(starters, pool);
            }

            while (_placed.Count < want)
            {
                List<BatteryCache> fresh = pool.FindAll(c => !_avoid.Contains(c.Id));
                PickFrom(fresh.Count > 0 ? fresh : pool, pool);
            }
        }

        private void PickFrom(List<BatteryCache> from, List<BatteryCache> pool)
        {
            float total = 0f;
            for (int i = 0; i < from.Count; i++) total += from[i].Weight;
            double roll = _rng.NextDouble() * total;
            BatteryCache chosen = from[from.Count - 1];
            for (int i = 0; i < from.Count; i++)
            {
                roll -= from[i].Weight;
                if (roll <= 0d)
                {
                    chosen = from[i];
                    break;
                }
            }

            _placed.Add(chosen.Id);
            pool.Remove(chosen);
        }

        /// <inheritdoc/>
        public string SnapshotKey
        {
            get { return Key; }
        }

        /// <inheritdoc/>
        public object CaptureState()
        {
            return new List<string>(_taken);
        }

        /// <inheritdoc/>
        public void RestoreState(object state)
        {
            List<string> taken = state as List<string>;
            if (taken == null) return;
            _taken.Clear();
            for (int i = 0; i < taken.Count; i++) _taken.Add(taken[i]);
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(_day).Append("일차 배터리 ").Append(_placed.Count).Append("개(남은 ").Append(Remaining).Append(")");
            for (int i = 0; i < _placed.Count; i++) sb.Append(i == 0 ? " — " : " · ").Append(Short(_placed[i]));
            if (_unlocked.Count > 0)
            {
                sb.Append(" / 푼 사물함 ");
                for (int i = 0; i < _unlocked.Count; i++) sb.Append(i == 0 ? "" : " · ").Append(Short(_unlocked[i]));
            }

            return sb.ToString();
        }

        private static string Short(string id)
        {
            int slash = id.LastIndexOf('/');
            return slash >= 0 ? id.Substring(slash + 1) : id;
        }
    }
}

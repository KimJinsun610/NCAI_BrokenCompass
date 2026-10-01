using System;
using System.Collections.Generic;

namespace NightDuty
{
    /// <summary>
    /// 재시작 스냅샷에 자기 상태를 싣는 부품(스냅샷 레지스트리, 2026-09-30 최종 기획서 제작 계획 3단계).
    /// <para>
    /// 밤 시작·체크포인트 때 <see cref="CaptureState"/>로 상태를 떠 두고, 붙잡혀 다시 할 때 <see cref="RestoreState"/>로 되돌린다.
    /// 상태 객체는 부품이 알아서 만들고 알아서 읽는다 — 스냅샷은 들고만 있다. 되돌리지 않을 값(이미 한 보고, 본 연출 기록)은 싣지 않는다.
    /// </para>
    /// </summary>
    public interface ISnapshotable
    {
        /// <summary>스냅샷 안에서 이 부품을 가리키는 이름. 부품마다 달라야 한다.</summary>
        string SnapshotKey { get; }

        /// <summary>지금 상태를 떠서 돌려준다(이후 부품이 바뀌어도 변하지 않는 복사본).</summary>
        object CaptureState();

        /// <summary><see cref="CaptureState"/>가 돌려준 상태로 되돌린다.</summary>
        void RestoreState(object state);
    }

    /// <summary>
    /// 재시작이 돌아가는 지점(2026-09-30 최종 기획서 「되돌리는 것」). 밤 시작과 02:16 중간 서명(체크포인트)에서 찍는다.
    /// <list type="bullet">
    /// <item>생존 수치 4축(감각 축은 재시작 때 <see cref="RestartPolicy"/>로 보정해서 쓴다, 신뢰는 그대로 복원).</item>
    /// <item>연출 도달값(감각 축 셋) — 붙잡힌 순간의 100이 연출을 영원히 구간 4로 묶지 않게.</item>
    /// <item>경고 도장과 대기 중인 처벌(스냅샷 뒤에 이미 나온 처벌은 되풀이하지 않는다 — <see cref="WarningLedger.RestoreFromSnapshot"/>).</item>
    /// <item>등록된 부품(<see cref="ISnapshotable"/>)의 상태 — 점검판의 정확 보고 한도 사용량 등.</item>
    /// </list>
    /// <para>
    /// 이상 배정·덱·조우·역설 편성은 밤 시작에 확정되어 재시작해도 바뀌지 않으므로 여기에 담지 않는다.
    /// </para>
    /// </summary>
    public sealed class NightSnapshot
    {
        private readonly int[] _values = new int[FearAxisSystem.AxisCount];
        private readonly int[] _reached = new int[BandResolver.WorldAxisCount];
        private readonly Dictionary<string, object> _parts = new Dictionary<string, object>(StringComparer.Ordinal);

        /// <summary>찍은 일차.</summary>
        public int Day { get; private set; }

        /// <summary>재시작이 시작되는 게임 시각(근무 시작부터의 분). 밤 시작이면 0, 체크포인트면 02:16.</summary>
        public int StartMinute { get; private set; }

        /// <summary>체크포인트(중간 서명)에서 찍었는지.</summary>
        public bool IsCheckpoint { get; private set; }

        /// <summary>경고 도장 수.</summary>
        public int Warnings { get; private set; }

        /// <summary>대기 중이던 처벌 수.</summary>
        public int PendingPunishments { get; private set; }

        /// <summary>찍은 순간까지 꺼내 쓴 처벌 누계(<see cref="WarningLedger.TotalTaken"/>).</summary>
        public int PunishmentsTaken { get; private set; }

        /// <summary>그 축의 생존 수치.</summary>
        public int Value(FearAxis axis)
        {
            int i = (int)axis;
            return i >= 0 && i < _values.Length ? _values[i] : 0;
        }

        /// <summary>감각 축의 연출 도달값(청각·조도·배치 순).</summary>
        public int Reached(int worldAxisSlot)
        {
            return worldAxisSlot >= 0 && worldAxisSlot < _reached.Length ? _reached[worldAxisSlot] : 0;
        }

        /// <summary>감각 축 연출 도달값의 복사본(청각·조도·배치 순).</summary>
        public int[] ReachedCopy()
        {
            return (int[])_reached.Clone();
        }

        /// <summary>그 부품의 상태를 실었는지.</summary>
        public bool HasPart(string key)
        {
            return key != null && _parts.ContainsKey(key);
        }

        /// <summary>
        /// 실어 둔 부품 상태를 되돌린다. 스냅샷에 없는 부품은 건드리지 않는다. 한 부품이 실패해도 나머지는 계속한다.
        /// </summary>
        public void RestoreParts(IEnumerable<ISnapshotable> parts)
        {
            if (parts == null) return;
            foreach (ISnapshotable part in parts)
            {
                object state;
                if (part == null || !_parts.TryGetValue(part.SnapshotKey, out state))
                {
                    continue;
                }

                try
                {
                    part.RestoreState(state);
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogException(e);
                }
            }
        }

        /// <summary>지금 상태를 찍는다.</summary>
        public static NightSnapshot Take(int day, int startMinute, bool isCheckpoint,
            IFearAxisReader axes, BandResolver bands, WarningLedger ledger, IEnumerable<ISnapshotable> parts = null)
        {
            if (axes == null) throw new ArgumentNullException(nameof(axes));
            if (bands == null) throw new ArgumentNullException(nameof(bands));

            NightSnapshot s = new NightSnapshot();
            s.Day = day;
            s.StartMinute = startMinute;
            s.IsCheckpoint = isCheckpoint;
            for (int i = 0; i < s._values.Length; i++)
            {
                s._values[i] = axes.GetValue((FearAxis)i);
            }

            int[] reached = bands.ReachedCopy();
            Array.Copy(reached, s._reached, Math.Min(reached.Length, s._reached.Length));

            if (ledger != null)
            {
                s.Warnings = ledger.Count;
                s.PendingPunishments = ledger.PendingPunishments;
                s.PunishmentsTaken = ledger.TotalTaken;
            }

            if (parts != null)
            {
                foreach (ISnapshotable part in parts)
                {
                    if (part == null) continue;
                    s._parts[part.SnapshotKey] = part.CaptureState();
                }
            }

            return s;
        }
    }
}

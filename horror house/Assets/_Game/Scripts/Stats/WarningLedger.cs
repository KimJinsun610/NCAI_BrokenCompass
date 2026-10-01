using System;

namespace NightDuty
{
    /// <summary>
    /// 경고 장부(2026-09-30 최종 기획서 「경고와 처벌」). 회차 안에서 누적되고 태블릿 상단에 도장 세 칸으로 보인다.
    /// <list type="bullet">
    /// <item>점검을 마치지 못하면 경고 1. <see cref="Deltas.WarningsForPunishment"/>(3)에 닿으면 처벌이 <b>대기</b>에 들어가고 경고는 초기화된다.</item>
    /// <item>처벌은 곧바로 주지 않는다 — 판정 구간에 다음 공간 경계를 넘을 때 <see cref="NightRun"/>이 꺼내 쓴다.
    /// 04:00 정산에서 3회가 되면 다음 밤 출근이 끝난 뒤 첫 이동에서 나온다(대기가 밤을 건너 남는다).</item>
    /// <item>경고·대기는 재시작 스냅샷에 들어간다(<see cref="NightSnapshot"/>). 단 <b>이미 실행된 처벌은 되풀이하지 않는다</b> —
    /// <see cref="TotalTaken"/>(되돌리지 않는 누계)로 스냅샷 뒤에 꺼낸 수를 빼고 복원한다(<see cref="RestoreFromSnapshot"/>).</item>
    /// </list>
    /// </summary>
    public sealed class WarningLedger
    {
        private int _count;
        private int _pending;

        /// <summary>경고나 대기가 바뀌었다. 인자: (도장 수 0~2, 대기 중인 처벌 수).</summary>
        public event Action<int, int> Changed;

        /// <summary>지금 찍혀 있는 경고 도장 수(0~2).</summary>
        public int Count
        {
            get { return _count; }
        }

        /// <summary>아직 나오지 않은 처벌 수.</summary>
        public int PendingPunishments
        {
            get { return _pending; }
        }

        /// <summary>회차 동안 실제로 꺼내 쓴 처벌의 누계. 재시작해도 되돌리지 않는다.</summary>
        public int TotalTaken { get; private set; }

        /// <summary>
        /// 경고를 더한다. 세 번째 도장이 찍히면 처벌 하나가 대기에 들어가고 도장은 비워진다.
        /// </summary>
        /// <param name="amount">더할 경고 수(양수). 0 이하는 무시한다.</param>
        /// <returns>이번에 새로 대기에 들어간 처벌 수.</returns>
        public int Add(int amount = 1)
        {
            if (amount <= 0)
            {
                return 0;
            }

            _count += amount;
            int added = 0;
            while (_count >= Deltas.WarningsForPunishment)
            {
                _count -= Deltas.WarningsForPunishment;
                _pending++;
                added++;
            }

            RaiseChanged();
            return added;
        }

        /// <summary>대기 중인 처벌 하나를 꺼낸다. 없으면 false.</summary>
        public bool TryTakePunishment()
        {
            if (_pending <= 0)
            {
                return false;
            }

            _pending--;
            TotalTaken++;
            RaiseChanged();
            return true;
        }

        /// <summary>스냅샷 값으로 그대로 되돌린다(결근 등). 이벤트를 보낸다.</summary>
        public void Restore(int count, int pending)
        {
            _count = Math.Max(0, Math.Min(Deltas.WarningsForPunishment - 1, count));
            _pending = Math.Max(0, pending);
            RaiseChanged();
        }

        /// <summary>
        /// 재시작 복원. 스냅샷 뒤에 이미 꺼내 쓴 처벌은 대기에서 뺀다 — 같은 처벌을 두 번 보지 않는다(경계 사례 「처벌 대기 중 재시작」).
        /// </summary>
        public void RestoreFromSnapshot(NightSnapshot snapshot)
        {
            if (snapshot == null) return;
            int takenSince = Math.Max(0, TotalTaken - snapshot.PunishmentsTaken);
            Restore(snapshot.Warnings, snapshot.PendingPunishments - takenSince);
        }

        private void RaiseChanged()
        {
            Action<int, int> handler = Changed;
            if (handler == null)
            {
                return;
            }

            foreach (Delegate d in handler.GetInvocationList())
            {
                try
                {
                    ((Action<int, int>)d)(_count, _pending);
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogException(e);
                }
            }
        }
    }
}

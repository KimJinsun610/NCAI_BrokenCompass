using System;

namespace NightDuty
{
    /// <summary>
    /// 손전등 배터리 상태(56차) — 충전량과 주머니의 예비. 규칙 값은 <see cref="BatteryRules"/>.
    /// 점검판처럼 재시작 스냅샷에 실린다(붙잡힌 스냅샷 — 밤 시작 또는 02:16 서명 — 의 충전량·예비로 돌아간다).
    /// </summary>
    public sealed class FlashlightBattery : ISnapshotable
    {
        /// <summary>스냅샷 키.</summary>
        public const string Key = "flashlight.battery";

        private sealed class State
        {
            public float Charge;
            public int Spare;
        }

        /// <summary>충전량(0~1).</summary>
        public float Charge { get; private set; } = 1f;

        /// <summary>주머니의 예비 수.</summary>
        public int Spare { get; private set; }

        /// <summary>다 닳았는지.</summary>
        public bool IsEmpty
        {
            get { return Charge <= 0f; }
        }

        /// <summary>주머니가 찼는지.</summary>
        public bool PocketFull
        {
            get { return Spare >= BatteryRules.PocketMax; }
        }

        /// <summary>
        /// 켜 둔 시간만큼 닳게 한다. 이번에 막 다 닳았으면 true(손전등을 끌 때다).
        /// </summary>
        public bool Drain(float seconds)
        {
            if (seconds <= 0f || Charge <= 0f) return false;
            Charge -= seconds / BatteryRules.FullSeconds;
            if (Charge > 0f) return false;
            Charge = 0f;
            return true;
        }

        /// <summary>예비 하나를 주머니에 넣는다. 가득 찼으면 false.</summary>
        public bool TryPocket()
        {
            if (PocketFull) return false;
            Spare++;
            return true;
        }

        /// <summary>예비로 갈아 끼운다(남은 양은 버린다). 예비가 없으면 false.</summary>
        public bool Swap()
        {
            if (Spare <= 0) return false;
            Spare--;
            Charge = 1f;
            return true;
        }

        /// <summary>디버그: 충전량(0~1)과 예비 수를 정한다.</summary>
        public void DebugSet(float charge, int spare)
        {
            Charge = Math.Max(0f, Math.Min(1f, charge));
            Spare = Math.Max(0, Math.Min(BatteryRules.PocketMax, spare));
        }

        /// <inheritdoc/>
        public string SnapshotKey
        {
            get { return Key; }
        }

        /// <inheritdoc/>
        public object CaptureState()
        {
            return new State { Charge = Charge, Spare = Spare };
        }

        /// <inheritdoc/>
        public void RestoreState(object state)
        {
            State s = state as State;
            if (s == null) return;
            Charge = s.Charge;
            Spare = s.Spare;
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return "배터리 " + Math.Round(Charge * 100f) + "% · 예비 " + Spare;
        }
    }
}

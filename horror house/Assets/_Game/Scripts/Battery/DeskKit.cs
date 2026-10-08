namespace NightDuty
{
    /// <summary>
    /// 66차(민: 「책상 위 배터리는 1일차에만, 손전등도 같이 책상에 놓고, 손전등을 먹으면 그때부터 F로 켤 수 있도록」) — 경비실 책상 위 시작 물품.
    /// 1일차 밤은 손전등 없이 시작하고, 책상에 손전등과 예비 배터리 하나가 놓인다. 2일차부터는 손전등을 든 채 시작하고 책상은 비어 있다.
    /// 재시작 스냅샷에 실린다 — 줍기 전 체크포인트로 돌아가면 책상에 다시 놓인다.
    /// </summary>
    public sealed class DeskKit : ISnapshotable
    {
        /// <summary>스냅샷 키.</summary>
        public const string Key = "desk.kit";

        private sealed class State
        {
            public bool Flashlight;
            public bool Battery;
        }

        /// <summary>그날 책상에 물품을 놓는지(1일차만).</summary>
        public static bool ForDay(int day)
        {
            return day <= 1;
        }

        /// <summary>그날 책상을 만든다.</summary>
        public DeskKit(int day)
        {
            FlashlightOnDesk = ForDay(day);
            BatteryOnDesk = ForDay(day);
        }

        /// <summary>손전등이 아직 책상에 있는지(있으면 플레이어는 손전등을 켤 수 없다).</summary>
        public bool FlashlightOnDesk { get; private set; }

        /// <summary>예비 배터리가 아직 책상에 있는지.</summary>
        public bool BatteryOnDesk { get; private set; }

        /// <summary>손전등을 집는다. 이미 없으면 false.</summary>
        public bool TakeFlashlight()
        {
            if (!FlashlightOnDesk) return false;
            FlashlightOnDesk = false;
            return true;
        }

        /// <summary>배터리를 집는다. 이미 없으면 false.</summary>
        public bool TakeBattery()
        {
            if (!BatteryOnDesk) return false;
            BatteryOnDesk = false;
            return true;
        }

        /// <inheritdoc/>
        public string SnapshotKey
        {
            get { return Key; }
        }

        /// <inheritdoc/>
        public object CaptureState()
        {
            return new State { Flashlight = FlashlightOnDesk, Battery = BatteryOnDesk };
        }

        /// <inheritdoc/>
        public void RestoreState(object state)
        {
            State s = state as State;
            if (s == null) return;
            FlashlightOnDesk = s.Flashlight;
            BatteryOnDesk = s.Battery;
        }
    }
}

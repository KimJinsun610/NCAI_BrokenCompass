using System;

namespace NightDuty
{
    /// <summary>
    /// 생존 수치 → 공간별 <b>연출 구간</b>. <see cref="EventBus.BandChanged"/>를 보내는 곳은 여기 하나다.
    /// <para>
    /// 축마다 값이 둘이다(2026-09-30 새 기획서).
    /// <list type="bullet">
    /// <item><b>생존 수치</b> — <see cref="FearAxisSystem"/>의 값. 붙잡힘(100)에만 쓰고, 정확한 보고로 내려갈 수 있다.</item>
    /// <item><b>연출 구간</b> — 여기서 정한다. max(도달 구간, 일차 하한)이고 <b>절대 내려가지 않는다(래칫).</b>
    /// 도달 구간은 이번 회차에 생존 수치가 닿았던 가장 높은 값(<see cref="Peak"/>)의 구간이다.
    /// 그래서 보고로 수치를 깎아도, 밤을 다시 시작해 수치가 줄어도 학교는 나빠진 그대로다.</item>
    /// </list>
    /// </para>
    /// <list type="bullet">
    /// <item><b>사건 중 고정:</b> 공간에 진행 중인 단기 사건이 있으면(<see cref="SetHold"/>) 그 공간의 구간 반영을 미루고,
    /// 사건이 끝나면 한 번에 반영한다. 다른 공간은 계속 따른다.</item>
    /// <item><b>100 도달은 미루지 않는다</b> — 그 신호는 <see cref="FearAxisSystem"/>이 별도로 보낸다.</item>
    /// <item>신뢰 축은 월드에 그리지 않으므로 방송하지 않는다. 신뢰의 구간은 원시 구간 그대로다(신뢰는 줄지 않는다).</item>
    /// </list>
    /// </summary>
    public sealed class BandResolver
    {
        private static readonly SpaceId[] PatrolSpaces =
        {
            SpaceId.Corridor, SpaceId.Toilet, SpaceId.Classroom_1_1, SpaceId.Classroom_1_3, SpaceId.ScienceRoom
        };

        private static readonly FearAxis[] WorldAxes =
        {
            FearAxis.Auditory, FearAxis.Illuminance, FearAxis.Layout
        };

        private readonly IFearAxisReader _reader;
        private readonly Band[,] _shown;
        private readonly bool[] _hold;
        private readonly int[] _peak = new int[WorldAxes.Length];
        private Band _dayFloor = Band.Band0;

        /// <summary>해석기를 만든다. 초기 도달값은 현재 생존 수치다.</summary>
        public BandResolver(IFearAxisReader reader)
        {
            _reader = reader ?? throw new ArgumentNullException(nameof(reader));
            int spaceSlots = MaxSpaceIndex() + 1;
            _shown = new Band[spaceSlots, WorldAxes.Length];
            _hold = new bool[spaceSlots];
            Shown = new ShownReader(this);

            for (int a = 0; a < WorldAxes.Length; a++)
            {
                _peak[a] = _reader.GetValue(WorldAxes[a]);
            }

            for (int s = 0; s < PatrolSpaces.Length; s++)
            {
                for (int a = 0; a < WorldAxes.Length; a++)
                {
                    _shown[(int)PatrolSpaces[s], a] = Target(WorldAxes[a]);
                }
            }
        }

        /// <summary>
        /// 연출 구간으로 읽는 창구. <c>GetValue</c>는 생존 수치 그대로, <c>GetBand</c>는 연출 구간(<see cref="Target"/>)이다.
        /// 수칙 자격·하루 덱·역설 상한처럼 「지금 학교가 얼마나 나빠졌나」를 보는 곳은 이것을 읽는다.
        /// </summary>
        public IFearAxisReader Shown { get; }

        /// <summary>현재 걸려 있는 일차 하한.</summary>
        public Band DayFloorBand
        {
            get { return _dayFloor; }
        }

        /// <summary>
        /// 그날의 일차 하한을 건다. <b>덱을 짜기 전에</b> 부른다 — 카드 자격이 연출 구간을 보기 때문이다.
        /// 보류 중이 아닌 공간은 바로 올라간다. 하한이 내려가도 이미 올라간 연출 구간은 내려가지 않는다.
        /// </summary>
        public void SetDayFloor(Band floor)
        {
            _dayFloor = floor;
            for (int s = 0; s < PatrolSpaces.Length; s++)
            {
                SpaceId space = PatrolSpaces[s];
                if (!_hold[(int)space])
                {
                    RefreshSpace(space);
                }
            }
        }

        /// <summary>이번 회차에 그 축의 생존 수치가 닿았던 가장 높은 값. 감각 축이 아니면 현재 값.</summary>
        public int Peak(FearAxis axis)
        {
            int a = AxisSlot(axis);
            return a < 0 ? _reader.GetValue(axis) : _peak[a];
        }

        /// <summary>
        /// 축의 연출 구간(공간 보류와 무관한 목표치) = max(도달 구간, 일차 하한).
        /// 신뢰는 원시 구간 그대로다.
        /// </summary>
        public Band Target(FearAxis axis)
        {
            int a = AxisSlot(axis);
            if (a < 0)
            {
                return Bands.Of(_reader.GetValue(axis));
            }

            return Bands.Higher(Bands.Of(_peak[a]), _dayFloor);
        }

        /// <summary>공간에 표시 중인 구간. 순찰 공간이 아니면 <see cref="Target"/>.</summary>
        public Band GetShown(SpaceId space, FearAxis axis)
        {
            int a = AxisSlot(axis);
            if (a < 0 || !IsPatrol(space))
            {
                return Target(axis);
            }

            return _shown[(int)space, a];
        }

        /// <summary>공간의 구간 반영을 멈추거나 재개한다. 재개하면 밀린 변화를 즉시 반영한다.</summary>
        public void SetHold(SpaceId space, bool hold)
        {
            if (!IsPatrol(space))
            {
                return;
            }

            bool was = _hold[(int)space];
            _hold[(int)space] = hold;
            if (was && !hold)
            {
                RefreshSpace(space);
            }
        }

        /// <summary>공간이 반영 보류 중인지.</summary>
        public bool IsHeld(SpaceId space)
        {
            return IsPatrol(space) && _hold[(int)space];
        }

        /// <summary>축 값이 바뀌었을 때 호출한다. 도달값을 올리고, 보류 중이 아닌 공간만 갱신한다.</summary>
        public void OnValueChanged(FearAxis axis)
        {
            int a = AxisSlot(axis);
            if (a < 0)
            {
                return;
            }

            int value = _reader.GetValue(axis);
            if (value > _peak[a])
            {
                _peak[a] = value;
            }

            for (int s = 0; s < PatrolSpaces.Length; s++)
            {
                SpaceId space = PatrolSpaces[s];
                if (!_hold[(int)space])
                {
                    Update(space, a);
                }
            }
        }

        /// <summary><see cref="FearAxisSystem.ValueChanged"/>에 바로 연결할 수 있는 형태.</summary>
        public void OnValueChanged(FearAxis axis, int before, int after)
        {
            OnValueChanged(axis);
        }

        /// <summary>
        /// 모든 공간·축의 현재 구간을 <b>from == to</b>로 다시 방송한다(씬 로드·복원 직후 기준값 송출).
        /// 연출은 멱등해야 한다.
        /// </summary>
        public void BroadcastAll()
        {
            for (int s = 0; s < PatrolSpaces.Length; s++)
            {
                for (int a = 0; a < WorldAxes.Length; a++)
                {
                    SpaceId space = PatrolSpaces[s];
                    Band band = _shown[(int)space, a];
                    EventBus.RaiseBandChanged(space, WorldAxes[a], band, band);
                    EventBus.RaiseBandProgress(space, WorldAxes[a], Bands.Progress(_peak[a], band));
                }
            }
        }

        private void RefreshSpace(SpaceId space)
        {
            for (int a = 0; a < WorldAxes.Length; a++)
            {
                Update(space, a);
            }
        }

        private void Update(SpaceId space, int axisSlot)
        {
            FearAxis axis = WorldAxes[axisSlot];
            Band from = _shown[(int)space, axisSlot];
            Band to = Bands.Higher(from, Target(axis));   // 래칫: 내려가지 않는다.

            if (to != from)
            {
                _shown[(int)space, axisSlot] = to;
                EventBus.RaiseBandChanged(space, axis, from, to);
            }

            // 진행도도 도달값 기준이라 수치가 깎여도 뒤로 가지 않는다.
            EventBus.RaiseBandProgress(space, axis, Bands.Progress(_peak[axisSlot], to));
        }

        private static int AxisSlot(FearAxis axis)
        {
            for (int i = 0; i < WorldAxes.Length; i++)
            {
                if (WorldAxes[i] == axis)
                {
                    return i;
                }
            }

            return -1;
        }

        private static bool IsPatrol(SpaceId space)
        {
            return Array.IndexOf(PatrolSpaces, space) >= 0;
        }

        private static int MaxSpaceIndex()
        {
            int max = 0;
            for (int i = 0; i < PatrolSpaces.Length; i++)
            {
                max = Math.Max(max, (int)PatrolSpaces[i]);
            }

            return max;
        }

        /// <summary>값은 생존 수치, 구간은 연출 구간으로 읽는 어댑터.</summary>
        private sealed class ShownReader : IFearAxisReader
        {
            private readonly BandResolver _owner;

            public ShownReader(BandResolver owner)
            {
                _owner = owner;
            }

            public int GetValue(FearAxis axis)
            {
                return _owner._reader.GetValue(axis);
            }

            public Band GetBand(FearAxis axis)
            {
                return _owner.Target(axis);
            }
        }
    }
}

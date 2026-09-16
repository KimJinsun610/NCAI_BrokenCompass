using System;

namespace NightDuty
{
    /// <summary>
    /// 축 값 → 공간별 표시 구간. <see cref="EventBus.BandChanged"/>를 보내는 곳은 여기 하나다.
    /// <list type="bullet">
    /// <item><b>히스테리시스:</b> 상승은 임계값에서, 하강은 임계값 − <see cref="FallMargin"/>에서 전환한다.
    /// 축은 줄지 않지만 저장 복원 등으로 값이 내려갈 수 있어 남겨 둔다.</item>
    /// <item><b>사건 중 고정:</b> 공간에 진행 중인 단기 사건이 있으면(<see cref="SetHold"/>) 그 공간의 구간 반영을 미루고,
    /// 사건이 끝나면 한 번에 반영한다. 다른 공간은 계속 수치를 따른다.</item>
    /// <item><b>100 도달은 미루지 않는다</b> — 그 신호는 <see cref="FearAxisSystem"/>이 별도로 보낸다.</item>
    /// <item>신뢰 축은 월드에 그리지 않으므로 방송하지 않는다.</item>
    /// </list>
    /// </summary>
    public sealed class BandResolver
    {
        /// <summary>하강 전환 여유.</summary>
        public const int FallMargin = 5;

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

        /// <summary>해석기를 만든다. 초기 표시 구간은 현재 값의 원시 구간이다.</summary>
        public BandResolver(IFearAxisReader reader)
        {
            _reader = reader ?? throw new ArgumentNullException(nameof(reader));
            int spaceSlots = MaxSpaceIndex() + 1;
            _shown = new Band[spaceSlots, WorldAxes.Length];
            _hold = new bool[spaceSlots];

            for (int s = 0; s < PatrolSpaces.Length; s++)
            {
                for (int a = 0; a < WorldAxes.Length; a++)
                {
                    _shown[(int)PatrolSpaces[s], a] = Bands.Of(_reader.GetValue(WorldAxes[a]));
                }
            }
        }

        /// <summary>
        /// 히스테리시스를 적용한 다음 구간. 상승은 즉시, 하강은 하한 − 여유 이하일 때만.
        /// </summary>
        public static Band Resolve(Band current, int value)
        {
            Band raw = Bands.Of(value);
            if (raw >= current)
            {
                return raw;
            }

            Band target = current;
            while (target > Band.Band0 && value <= Bands.LowerBound(target) - FallMargin)
            {
                target = (Band)((int)target - 1);
            }

            return target;
        }

        /// <summary>공간에 표시 중인 구간.</summary>
        public Band GetShown(SpaceId space, FearAxis axis)
        {
            int a = AxisSlot(axis);
            if (a < 0 || !IsPatrol(space))
            {
                return Bands.Of(_reader.GetValue(axis));
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

        /// <summary>축 값이 바뀌었을 때 호출한다. 보류 중이 아닌 공간만 갱신한다.</summary>
        public void OnValueChanged(FearAxis axis)
        {
            int a = AxisSlot(axis);
            if (a < 0)
            {
                return;
            }

            int value = _reader.GetValue(axis);
            for (int s = 0; s < PatrolSpaces.Length; s++)
            {
                SpaceId space = PatrolSpaces[s];
                if (!_hold[(int)space])
                {
                    Update(space, a, value);
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
                    EventBus.RaiseBandProgress(space, WorldAxes[a], Bands.Progress(_reader.GetValue(WorldAxes[a]), band));
                }
            }
        }

        private void RefreshSpace(SpaceId space)
        {
            for (int a = 0; a < WorldAxes.Length; a++)
            {
                Update(space, a, _reader.GetValue(WorldAxes[a]));
            }
        }

        private void Update(SpaceId space, int axisSlot, int value)
        {
            FearAxis axis = WorldAxes[axisSlot];
            Band from = _shown[(int)space, axisSlot];
            Band to = Resolve(from, value);

            if (to != from)
            {
                _shown[(int)space, axisSlot] = to;
                EventBus.RaiseBandChanged(space, axis, from, to);
            }

            EventBus.RaiseBandProgress(space, axis, Bands.Progress(value, to));
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
    }
}

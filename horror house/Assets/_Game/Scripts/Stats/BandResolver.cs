using System;

namespace NightDuty
{
    /// <summary>
    /// 생존 수치 → 공간별 <b>연출 구간</b>. <see cref="EventBus.BandChanged"/>를 보내는 곳은 여기 하나다.
    /// <para>
    /// 축마다 값이 둘이다(2026-09-30 새 기획서).
    /// <list type="bullet">
    /// <item><b>생존 수치</b> — <see cref="FearAxisSystem"/>의 값. 붙잡힘(100)에만 쓰고, 정확한 보고로 내려갈 수 있다.</item>
    /// <item><b>연출 구간</b> — 여기서 정한다. max(도달 구간, 일차 하한)이고 <b>밤 안에서는 내려가지 않는다(래칫).</b>
    /// 도달 구간은 이번 회차에 생존 수치가 닿았던 가장 높은 값(<see cref="Peak"/>)의 구간이다.
    /// 그래서 보고로 수치를 깎아도 학교는 나빠진 그대로다.
    /// <b>예외는 붙잡힌 뒤의 재시작 하나다</b> — <see cref="RestoreReached"/>가 도달값을 스냅샷으로 되돌린다.
    /// 붙잡힌 순간의 100이 연출을 영원히 구간 4로 묶지 않게 하기 위해서다(2026-09-30 최종 기획서).</item>
    /// </list>
    /// </para>
    /// <list type="bullet">
    /// <item><b>100 도달은 미루지 않는다</b> — 그 신호는 <see cref="FearAxisSystem"/>이 별도로 보낸다.</item>
    /// <item>신뢰 축은 월드에 그리지 않으므로 방송하지 않는다. 신뢰의 구간은 <b>신뢰 전용 경계</b>(<see cref="Bands.OfTrust"/>)로 읽는다(신뢰는 줄지 않는다).</item>
    /// </list>
    /// </summary>
    public sealed class BandResolver
    {
        /// <summary>감각 축 수(청각·조도·배치). 도달값 배열의 길이다.</summary>
        public const int WorldAxisCount = 3;

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
        private readonly int[] _peak = new int[WorldAxisCount];
        private Band _dayFloor = Band.Band0;

        /// <summary>해석기를 만든다. 초기 도달값은 현재 생존 수치다.</summary>
        public BandResolver(IFearAxisReader reader)
        {
            _reader = reader ?? throw new ArgumentNullException(nameof(reader));
            int spaceSlots = MaxSpaceIndex() + 1;
            _shown = new Band[spaceSlots, WorldAxes.Length];
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
        /// 하한이 내려가도 이미 올라간 연출 구간은 내려가지 않는다.
        /// </summary>
        public void SetDayFloor(Band floor)
        {
            _dayFloor = floor;
            for (int s = 0; s < PatrolSpaces.Length; s++)
            {
                RefreshSpace(PatrolSpaces[s]);
            }
        }

        /// <summary>이번 회차에 그 축의 생존 수치가 닿았던 가장 높은 값. 감각 축이 아니면 현재 값.</summary>
        public int Peak(FearAxis axis)
        {
            int a = AxisSlot(axis);
            return a < 0 ? _reader.GetValue(axis) : _peak[a];
        }

        /// <summary>감각 축 도달값의 복사본(청각·조도·배치 순). 재시작 스냅샷이 찍는다.</summary>
        public int[] ReachedCopy()
        {
            return (int[])_peak.Clone();
        }

        /// <summary>
        /// 도달값을 스냅샷으로 되돌리고 모든 공간의 연출 구간을 다시 계산한다(붙잡힌 뒤 재시작 전용).
        /// <b>연출 구간이 내려갈 수 있는 유일한 길이다.</b> 바뀐 공간에만 <see cref="EventBus.BandChanged"/>를 보낸다.
        /// </summary>
        /// <param name="reached">청각·조도·배치 순 도달값. 짧거나 null이면 남는 축은 현재 생존 수치로 둔다.</param>
        public void RestoreReached(int[] reached)
        {
            for (int a = 0; a < WorldAxes.Length; a++)
            {
                int current = _reader.GetValue(WorldAxes[a]);
                int restored = reached != null && a < reached.Length ? reached[a] : current;
                // 도달값은 적어도 지금 생존 수치 이상이어야 한다(재시작 보정이 스냅샷보다 높게 시작할 일은 없지만 방어).
                _peak[a] = Math.Max(restored, current);
            }

            for (int s = 0; s < PatrolSpaces.Length; s++)
            {
                SpaceId space = PatrolSpaces[s];
                for (int a = 0; a < WorldAxes.Length; a++)
                {
                    FearAxis axis = WorldAxes[a];
                    Band from = _shown[(int)space, a];
                    Band to = Target(axis);
                    _shown[(int)space, a] = to;
                    if (to != from)
                    {
                        EventBus.RaiseBandChanged(space, axis, from, to);
                    }

                    EventBus.RaiseBandProgress(space, axis, Bands.Progress(_peak[a], to));
                }
            }
        }

        /// <summary>
        /// 축의 연출 구간 = max(도달 구간, 일차 하한).
        /// 신뢰는 신뢰 전용 경계의 원시 구간 그대로다.
        /// </summary>
        public Band Target(FearAxis axis)
        {
            int a = AxisSlot(axis);
            if (a < 0)
            {
                return Bands.OfAxis(axis, _reader.GetValue(axis));
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

        /// <summary>축 값이 바뀌었을 때 호출한다. 도달값을 올리고 모든 순찰 공간을 갱신한다.</summary>
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
                Update(PatrolSpaces[s], a);
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

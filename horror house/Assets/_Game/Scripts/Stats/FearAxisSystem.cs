using System;
using UnityEngine;

namespace NightDuty
{
    /// <summary>
    /// 종료 잠금의 최초 원인. 기획서: 「종료 원인 축·카드/문자 ID·현재 공간을 기록한다」.
    /// </summary>
    public readonly struct TerminationCause
    {
        /// <summary>100에 도달한 축.</summary>
        public readonly FearAxis Axis;

        /// <summary>마지막 델타를 만든 카드·문자 ID.</summary>
        public readonly string SourceId;

        /// <summary>도달 당시 공간.</summary>
        public readonly SpaceId Space;

        /// <summary>원인을 만든다.</summary>
        public TerminationCause(FearAxis axis, string sourceId, SpaceId space)
        {
            Axis = axis;
            SourceId = sourceId ?? string.Empty;
            Space = space;
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return Axis + " 100 by " + SourceId + " @" + Space;
        }
    }

    /// <summary>
    /// 4축 누적 값의 유일한 소유자. <see cref="IFearAxisReader"/>의 실제 공급원이다
    /// (<c>DebugAxisDriver</c>와 같은 포트에 꽂힌다).
    /// <list type="bullet">
    /// <item>값은 0~100 누적. <b>감쇠·상시 증가·감소 없음.</b> 새 값 = min(100, 기존 + 델타).</item>
    /// <item>어느 축이든 100에 도달하면 <b>종료 잠금</b>. 최초 원인 1개를 기록하고 <see cref="EventBus.AxisCritical"/>을 <b>한 번만</b> 보낸다.</item>
    /// <item>잠금 이후의 델타는 모두 무시한다.</item>
    /// </list>
    /// 회차 기록이므로 다음 날에 초기화하지 않는다.
    /// </summary>
    public sealed class FearAxisSystem : IFearAxisReader
    {
        /// <summary>축 개수.</summary>
        public const int AxisCount = 4;

        private readonly int[] _values = new int[AxisCount];
        private TerminationCause _cause;

        /// <summary>종료 잠금 상태인지.</summary>
        public bool IsLocked { get; private set; }

        /// <summary>종료 잠금의 최초 원인. <see cref="IsLocked"/>가 false면 의미 없음.</summary>
        public TerminationCause Cause { get { return _cause; } }

        /// <summary>값이 바뀌었다. 인자: (축, 이전 값, 새 값). 연출 연결은 <see cref="BandResolver"/>가 맡는다.</summary>
        public event Action<FearAxis, int, int> ValueChanged;

        /// <summary>종료 잠금이 걸렸다. 한 번만 발생한다.</summary>
        public event Action<TerminationCause> Terminated;

        /// <inheritdoc/>
        public int GetValue(FearAxis axis)
        {
            int i = (int)axis;
            return i >= 0 && i < AxisCount ? _values[i] : 0;
        }

        /// <inheritdoc/>
        public Band GetBand(FearAxis axis)
        {
            return Bands.Of(GetValue(axis));
        }

        /// <summary>
        /// 델타를 적용한다. 실제로 값이 바뀌었으면 true.
        /// 잠금 상태, 0 이하 델타(축은 줄지 않는다), 이미 100인 축은 무시한다.
        /// </summary>
        /// <param name="axis">대상 축.</param>
        /// <param name="delta">양수 델타.</param>
        /// <param name="sourceId">카드·문자 ID(종료 원인 기록용).</param>
        /// <param name="space">현재 공간(종료 원인 기록용).</param>
        public bool Apply(FearAxis axis, int delta, string sourceId, SpaceId space)
        {
            if (IsLocked)
            {
                return false;
            }

            int i = (int)axis;
            if (i < 0 || i >= AxisCount)
            {
                Debug.LogWarning("[FearAxisSystem] 알 수 없는 축: " + axis);
                return false;
            }

            if (delta <= 0)
            {
                if (delta < 0)
                {
                    Debug.LogWarning("[FearAxisSystem] 음수 델타는 허용되지 않습니다(감쇠 없음): " + sourceId + " " + delta);
                }

                return false;
            }

            int before = _values[i];
            int after = Math.Min(Bands.Max, before + delta);
            if (after == before)
            {
                return false;
            }

            _values[i] = after;

            // 잠금과 원인을 이벤트보다 먼저 확정한다. 구독자가 Apply를 다시 불러도
            // 100 이후의 델타가 끼어들거나 최초 원인이 덮이지 않게 하기 위해서다.
            bool reachedEnd = after >= Bands.Max;
            if (reachedEnd)
            {
                IsLocked = true;
                _cause = new TerminationCause(axis, sourceId, space);
            }

            RaiseValueChanged(axis, before, after);

            if (reachedEnd)
            {
                RaiseTerminated(_cause);
                EventBus.RaiseAxisCritical(axis);
            }

            return true;
        }

        /// <summary>
        /// 저장된 회차 값을 복원한다(저장 기능이 있을 때만 쓴다). 이벤트는 보내지 않는다 —
        /// 복원 뒤 <see cref="BandResolver.BroadcastAll"/>로 기준값을 다시 보내면 된다.
        /// </summary>
        public void Restore(int auditory, int illuminance, int layout, int trust)
        {
            _values[(int)FearAxis.Auditory] = Clamp(auditory);
            _values[(int)FearAxis.Illuminance] = Clamp(illuminance);
            _values[(int)FearAxis.Layout] = Clamp(layout);
            _values[(int)FearAxis.Trust] = Clamp(trust);
            IsLocked = false;
            _cause = default;

            for (int i = 0; i < AxisCount; i++)
            {
                if (_values[i] >= Bands.Max)
                {
                    IsLocked = true;
                    _cause = new TerminationCause((FearAxis)i, "복원", SpaceId.None);
                    break;
                }
            }
        }

        private static int Clamp(int value)
        {
            return Math.Max(Bands.Min, Math.Min(Bands.Max, value));
        }

        private void RaiseValueChanged(FearAxis axis, int before, int after)
        {
            Action<FearAxis, int, int> handler = ValueChanged;
            if (handler == null)
            {
                return;
            }

            Delegate[] targets = handler.GetInvocationList();
            for (int i = 0; i < targets.Length; i++)
            {
                try
                {
                    ((Action<FearAxis, int, int>)targets[i])(axis, before, after);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }

        private void RaiseTerminated(TerminationCause cause)
        {
            Action<TerminationCause> handler = Terminated;
            if (handler == null)
            {
                return;
            }

            Delegate[] targets = handler.GetInvocationList();
            for (int i = 0; i < targets.Length; i++)
            {
                try
                {
                    ((Action<TerminationCause>)targets[i])(cause);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }
    }
}

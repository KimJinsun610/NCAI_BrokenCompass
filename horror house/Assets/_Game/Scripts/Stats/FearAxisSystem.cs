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
    /// 4축 누적 값의 유일한 소유자. <see cref="IFearAxisReader"/>의 실제 공급원이다.
    /// <list type="bullet">
    /// <item>값은 0~100 누적(<b>생존 수치</b>). 감쇠·상시 증가 없음. 새 값 = min(100, 기존 + 델타).
    /// 감소는 <see cref="Lower"/>(정확한 보고)와 <see cref="Restore"/>(밤 재시작)로만 일어나고 신뢰는 줄지 않는다(2026-09-30 새 기획서).
    /// 연출 구간은 이 값이 아니라 <see cref="BandResolver"/>가 정한다.</item>
    /// <item><b>청각·조도·배치</b> 중 하나가 100에 도달하면 <b>종료 잠금</b>. 최초 원인 1개를 기록하고 붙잡힘(<see cref="EventBus.Captured"/>, 구독자가 없으면 옛 <see cref="EventBus.AxisCritical"/>)을 <b>한 번만</b> 보낸다.</item>
    /// <item><b>한 프레임(한 신호)에 두 축이 100</b>이 되면 초과량이 큰 축 하나로 붙잡힌다(동점은 청각 &gt; 조도 &gt; 배치) —
    /// <see cref="BeginFrame"/>/<see cref="EndFrame"/> 사이에서는 잠금 판단을 미룬다(최종 기획서 「경계 사례」).</item>
    /// <item><see cref="SoftCap"/>이 걸려 있으면 감각 축은 그 값에서 멈추고 붙잡히지 않는다 —
    /// 처벌·04:00 정산·판정 정지 구간의 델타(무한 루프 방지 장치 3·4).</item>
    /// <item><b>신뢰는 100에서 멈출 뿐 잠그지 않는다</b>(2026-09-17 결정). 신뢰는 태블릿 문자와 근무수칙의 충돌을 늘리는 데에만 쓴다.</item>
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

        // 한 프레임 묶음: 100을 넘긴 초과량(원시 합 − 100)과 그 원인. -1이면 넘기지 않음.
        private int _frameDepth;
        private readonly int[] _overflow = { -1, -1, -1, -1 };
        private readonly string[] _overflowSource = new string[AxisCount];
        private readonly SpaceId[] _overflowSpace = new SpaceId[AxisCount];

        /// <summary>
        /// 100 도달이 종료 잠금(포획)으로 이어지는 축인지. 청각·조도·배치만 true, 신뢰는 false.
        /// </summary>
        public static bool IsTerminal(FearAxis axis)
        {
            return axis == FearAxis.Auditory || axis == FearAxis.Illuminance || axis == FearAxis.Layout;
        }

        /// <summary>종료 잠금 상태인지.</summary>
        public bool IsLocked { get; private set; }

        /// <summary>종료 잠금의 최초 원인. <see cref="IsLocked"/>가 false면 의미 없음.</summary>
        public TerminationCause Cause { get { return _cause; } }

        /// <summary>
        /// 감각 축 델타의 임시 상한. 값이 있으면 <see cref="Apply"/>가 감각 축을 이 값에서 멈추고 붙잡힘을 일으키지 않는다
        /// (이미 이 값 이상이면 올리지 않는다). 신뢰에는 걸리지 않는다. null이면 상한 없음(100 = 붙잡힘).
        /// <see cref="NightRun"/>이 판정 정지 구간·처벌·정산 동안 <see cref="Deltas.SoftCap"/>으로 건다.
        /// </summary>
        public int? SoftCap { get; set; }

        /// <summary>
        /// 71차: 지워지지 않는 상한(개발자 모드 흐름 정지의 「붙잡힘 막기」). <see cref="SoftCap"/>과 같이 감각 축을 이 값에서 멈춘다 —
        /// <see cref="SoftCap"/>은 신호마다 다시 걸고 지우지만 이것은 직접 지울 때까지 남는다. null이면 없음.
        /// </summary>
        public int? HardCap { get; set; }

        /// <summary>값이 바뀌었다. 인자: (축, 이전 값, 새 값). 연출 연결은 <see cref="BandResolver"/>가 맡는다.</summary>
        public event Action<FearAxis, int, int> ValueChanged;

        /// <summary>
        /// <see cref="Apply"/>로 값이 올랐다. 인자: (축, 오른 양, 출처 ID). 재시작 카드의 「그 밤 그 축을 올린 수칙·점검 항목」이 읽는다.
        /// </summary>
        public event Action<FearAxis, int, string> Raised;

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
            return Bands.OfAxis(axis, GetValue(axis));
        }

        /// <summary>
        /// 한 프레임 묶음을 연다. 닫을 때(<see cref="EndFrame"/>)까지 100에 닿은 감각 축이 있어도 잠그지 않고 초과량만 기록한다.
        /// 중첩해도 된다(가장 바깥의 <see cref="EndFrame"/>에서 판단).
        /// </summary>
        public void BeginFrame()
        {
            if (_frameDepth == 0)
            {
                for (int i = 0; i < AxisCount; i++)
                {
                    _overflow[i] = -1;
                    _overflowSource[i] = null;
                    _overflowSpace[i] = SpaceId.None;
                }
            }

            _frameDepth++;
        }

        /// <summary>
        /// 한 프레임 묶음을 닫는다. 그 사이 100에 닿은 감각 축이 있으면 초과량이 가장 큰 축 하나로 잠근다(동점은 청각 &gt; 조도 &gt; 배치).
        /// </summary>
        /// <returns>이번에 잠갔으면 true.</returns>
        public bool EndFrame()
        {
            if (_frameDepth <= 0)
            {
                return false;
            }

            _frameDepth--;
            if (_frameDepth > 0 || IsLocked)
            {
                return false;
            }

            int best = -1;
            FearAxis[] order = { FearAxis.Auditory, FearAxis.Illuminance, FearAxis.Layout };
            for (int n = 0; n < order.Length; n++)
            {
                int i = (int)order[n];
                if (_overflow[i] < 0) continue;
                if (best < 0 || _overflow[i] > _overflow[best]) best = i;
            }

            if (best < 0)
            {
                return false;
            }

            Lock(new TerminationCause((FearAxis)best, _overflowSource[best], _overflowSpace[best]));
            return true;
        }

        /// <summary>
        /// 델타를 적용한다. 실제로 값이 바뀌었으면 true.
        /// 잠금 상태, 0 이하 델타(빼기는 <see cref="Lower"/>로), 이미 100인 축은 무시한다.
        /// 신뢰가 100에 닿아도 잠그지 않는다(<see cref="IsTerminal"/>).
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
                    Debug.LogWarning("[FearAxisSystem] 음수 델타는 Apply로 받지 않습니다. Lower를 쓰십시오: " + sourceId + " " + delta);
                }

                return false;
            }

            int before = _values[i];
            int raw = before + delta;
            int after = Math.Min(Bands.Max, raw);
            int? capAt = SoftCap;
            if (HardCap.HasValue) capAt = capAt.HasValue ? Math.Min(capAt.Value, HardCap.Value) : HardCap;
            bool capped = capAt.HasValue && IsTerminal(axis);
            if (capped)
            {
                int cap = Math.Min(Bands.Max - 1, capAt.Value);
                after = before >= cap ? before : Math.Min(after, cap);
            }

            bool reachedEnd = !capped && raw >= Bands.Max && IsTerminal(axis);

            // 묶음 중에는 초과량을 기록만 한다(이미 100이어도 더 넘긴 양을 센다).
            if (reachedEnd && _frameDepth > 0)
            {
                int over = raw - Bands.Max;
                if (over > _overflow[i])
                {
                    _overflow[i] = over;
                    _overflowSource[i] = sourceId;
                    _overflowSpace[i] = space;
                }
            }

            if (after == before)
            {
                return false;
            }

            _values[i] = after;

            // 잠금과 원인을 이벤트보다 먼저 확정한다. 구독자가 Apply를 다시 불러도
            // 100 이후의 델타가 끼어들거나 최초 원인이 덮이지 않게 하기 위해서다.
            bool lockNow = reachedEnd && _frameDepth == 0;
            if (lockNow)
            {
                IsLocked = true;
                _cause = new TerminationCause(axis, sourceId, space);
            }

            RaiseValueChanged(axis, before, after);
            RaiseRaised(axis, after - before, sourceId);

            if (lockNow)
            {
                RaiseTerminated(_cause);
                EventBus.RaiseCapturedOrCritical(axis);
            }

            return true;
        }

        /// <summary>
        /// 생존 수치를 뺀다(정확한 보고, 2026-09-30 새 기획서). 실제로 값이 바뀌었으면 true.
        /// <list type="bullet">
        /// <item><b>신뢰는 줄지 않는다</b> — 신뢰 축이면 아무것도 하지 않는다.</item>
        /// <item>붙잡힘(잠금) 상태에서는 무시한다.</item>
        /// <item>0 밑으로는 내려가지 않는다. <see cref="ValueChanged"/>를 보낸다 —
        /// <see cref="BandResolver"/>는 도달값을 기억하므로 연출 구간은 내려가지 않는다.</item>
        /// </list>
        /// </summary>
        /// <param name="axis">감각 축.</param>
        /// <param name="amount">뺄 양(양수).</param>
        /// <param name="sourceId">출처 ID(로그용).</param>
        public bool Lower(FearAxis axis, int amount, string sourceId)
        {
            if (IsLocked || !IsTerminal(axis) || amount <= 0)
            {
                return false;
            }

            int i = (int)axis;
            int before = _values[i];
            int after = Math.Max(Bands.Min, before - amount);
            if (after == before)
            {
                return false;
            }

            _values[i] = after;
            RaiseValueChanged(axis, before, after);
            return true;
        }

        /// <summary>
        /// 값을 통째로 되돌린다(밤 재시작·결근·저장 복원). 잠금을 풀고, 이벤트는 보내지 않는다 —
        /// 복원 뒤 <see cref="BandResolver.RestoreReached"/>나 <see cref="BandResolver.BroadcastAll"/>로 연출을 맞춘다.
        /// 100인 감각 축이 있으면 다시 잠근다.
        /// </summary>
        public void Restore(int auditory, int illuminance, int layout, int trust)
        {
            _values[(int)FearAxis.Auditory] = Clamp(auditory);
            _values[(int)FearAxis.Illuminance] = Clamp(illuminance);
            _values[(int)FearAxis.Layout] = Clamp(layout);
            _values[(int)FearAxis.Trust] = Clamp(trust);
            IsLocked = false;
            _cause = default;
            _frameDepth = 0;

            for (int i = 0; i < AxisCount; i++)
            {
                if (_values[i] >= Bands.Max && IsTerminal((FearAxis)i))
                {
                    IsLocked = true;
                    _cause = new TerminationCause((FearAxis)i, "복원", SpaceId.None);
                    break;
                }
            }
        }

        private void Lock(TerminationCause cause)
        {
            IsLocked = true;
            _cause = cause;
            RaiseTerminated(_cause);
            EventBus.RaiseCapturedOrCritical(cause.Axis);
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

            foreach (Delegate d in handler.GetInvocationList())
            {
                try
                {
                    ((Action<FearAxis, int, int>)d)(axis, before, after);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }

        private void RaiseRaised(FearAxis axis, int amount, string sourceId)
        {
            Action<FearAxis, int, string> handler = Raised;
            if (handler == null)
            {
                return;
            }

            foreach (Delegate d in handler.GetInvocationList())
            {
                try
                {
                    ((Action<FearAxis, int, string>)d)(axis, amount, sourceId);
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

            foreach (Delegate d in handler.GetInvocationList())
            {
                try
                {
                    ((Action<TerminationCause>)d)(cause);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }
    }
}

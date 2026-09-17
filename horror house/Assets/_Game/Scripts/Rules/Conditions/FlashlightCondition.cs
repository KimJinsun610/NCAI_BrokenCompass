using System;
using System.Collections.Generic;
using UnityEngine;

namespace NightDuty
{
    /// <summary>
    /// 유예 시간이 지난 뒤 손전등이 지정 상태이면 성립한다.
    /// 유예가 끝나는 순간 그 상태이거나, 이후 그 상태로 바꾸면 성립한다(H3: 복도 진입 2초 뒤 On이면 위반).
    /// </summary>
    [Serializable]
    [ConditionMenu("손전등/유예 후 상태")]
    public sealed class FlashlightCondition : ICondition
    {
        [SerializeField, Tooltip("true면 켜져 있을 때, false면 꺼져 있을 때 성립")]
        private bool _whenOn = true;

        [SerializeField, Tooltip("사건 시작 후 전환 유예(초). 음수면 카드의 유예 시간")]
        private float _graceSeconds = -1f;

        /// <summary>직렬화용 기본 생성자.</summary>
        public FlashlightCondition()
        {
        }

        /// <summary>코드·테스트에서 조건을 만든다.</summary>
        public FlashlightCondition(bool whenOn, float graceSeconds = -1f)
        {
            _whenOn = whenOn;
            _graceSeconds = graceSeconds;
        }

        /// <inheritdoc/>
        public void OnStart(RuleSO card, JudgeWorld world, ConditionState state)
        {
            state.ElapsedMs = 0;
        }

        /// <inheritdoc/>
        public bool Observe(in JudgeSignal signal, RuleSO card, JudgeWorld world, ConditionState state)
        {
            int graceMs = ConditionState.ToMs(ResolveGrace(card));

            if (signal.Kind == SignalKind.Tick)
            {
                state.ElapsedMs += ConditionState.ToMs(signal.Value);
                return state.ElapsedMs >= graceMs && world.FlashlightOn == _whenOn;
            }

            if (signal.Kind == SignalKind.FlashlightChanged)
            {
                return state.ElapsedMs >= graceMs && signal.Flag == _whenOn;
            }

            return false;
        }

        /// <inheritdoc/>
        public void CollectReferences(RuleSO card, List<string> ids)
        {
        }

        /// <inheritdoc/>
        public string Describe()
        {
            return "손전등 " + (_whenOn ? "On" : "Off") + " (유예 " + (_graceSeconds >= 0f ? _graceSeconds + "초" : "카드값") + " 후)";
        }

        private float ResolveGrace(RuleSO card)
        {
            if (_graceSeconds >= 0f)
            {
                return _graceSeconds;
            }

            return card != null ? card.GraceSeconds : 0f;
        }
    }
}

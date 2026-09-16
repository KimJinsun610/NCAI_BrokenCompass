using System;
using System.Collections.Generic;
using UnityEngine;

namespace NightDuty
{
    /// <summary>
    /// 플레이어 발밑 기준점과 바닥 기준점의 수평 거리가 반경 <b>미만</b>이 되면 성립한다.
    /// 정확히 경계(거리 == 반경)는 바깥이다. 기획서 검증: 1.49m 실패, 1.50m 통과.
    /// </summary>
    [Serializable]
    [ConditionMenu("근접/반경 안 진입")]
    public sealed class ProximityCondition : ICondition
    {
        [SerializeField, Tooltip("바닥 기준점 ID. 비우면 카드의 대상 목록, @trigger면 시작 대상")]
        private string _anchorId = string.Empty;

        [SerializeField, Tooltip("금지 반경(m). 0 이하면 카드의 반경을 쓴다")]
        private float _radius;

        /// <summary>직렬화용 기본 생성자.</summary>
        public ProximityCondition()
        {
        }

        /// <summary>코드·테스트에서 조건을 만든다.</summary>
        public ProximityCondition(string anchorId, float radius = 0f)
        {
            _anchorId = anchorId ?? string.Empty;
            _radius = radius;
        }

        /// <inheritdoc/>
        public void OnStart(RuleSO card, JudgeWorld world, ConditionState state)
        {
        }

        /// <inheritdoc/>
        public bool Observe(in JudgeSignal signal, RuleSO card, JudgeWorld world, ConditionState state)
        {
            if (signal.Kind != SignalKind.ProximitySample)
            {
                return false;
            }

            if (!TargetMatch.Matches(_anchorId, card, signal.TargetId, state))
            {
                return false;
            }

            // 밀리미터 정수로 비교해 1.49 / 1.50 경계가 부동소수 오차로 뒤집히지 않게 한다.
            int distanceMm = ConditionState.ToMs(signal.Value);
            int radiusMm = ConditionState.ToMs(ResolveRadius(card));
            return radiusMm > 0 && distanceMm < radiusMm;
        }

        /// <inheritdoc/>
        public void CollectReferences(RuleSO card, List<string> ids)
        {
            TargetMatch.Collect(_anchorId, card, ids);
        }

        /// <inheritdoc/>
        public string Describe()
        {
            return "근접 " + TargetMatch.Label(_anchorId) + " < " + (_radius > 0f ? _radius + "m" : "카드 반경");
        }

        private float ResolveRadius(RuleSO card)
        {
            if (_radius > 0f)
            {
                return _radius;
            }

            return card != null ? card.Radius : 0f;
        }
    }
}

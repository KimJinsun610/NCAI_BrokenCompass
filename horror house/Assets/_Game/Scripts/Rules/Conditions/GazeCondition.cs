using System;
using System.Collections.Generic;
using UnityEngine;

namespace NightDuty
{
    /// <summary>
    /// 대상을 지정 시간 이상 <b>연속</b> 응시하면 성립한다.
    /// <list type="bullet">
    /// <item>사건 시작 후 유예 시간 동안은 응시 시간을 쌓지 않는다(H2·C3의 2초).
    /// 유예 시계는 응시 샘플 자체로 흐른다 — 추적기는 대상이 없을 때도 빈 ID 샘플을 계속 보내야 한다.
    /// 유예 구간에 걸친 샘플은 세지 않는다.</item>
    /// <item>대상이 바뀌거나 가려지면(다른 ID·빈 ID 샘플) 연속 시간은 0이 된다.</item>
    /// <item>기획서 검증: 2.9초 응시는 통과, 3.0초는 위반, 2초씩 두 번 끊어 보기는 위반 없음.</item>
    /// </list>
    /// 판정 원뿔의 폭 같은 「무엇을 응시로 볼지」는 클라이언트의 응시 추적기가 정한다. 여기서는 샘플의 대상 ID만 본다.
    /// </summary>
    [Serializable]
    [ConditionMenu("응시/대상 연속 응시")]
    public sealed class GazeCondition : ICondition
    {
        [SerializeField, Tooltip("응시 대상 ID. 비우면 카드의 대상 목록, @trigger면 시작 대상")]
        private string _targetId = string.Empty;

        [SerializeField, Tooltip("필요한 연속 응시 시간(초). 0 이하면 카드의 응시 시간")]
        private float _seconds;

        [SerializeField, Tooltip("사건 시작 후 응시를 세지 않는 유예(초). 음수면 카드의 유예 시간")]
        private float _graceSeconds = -1f;

        /// <summary>직렬화용 기본 생성자.</summary>
        public GazeCondition()
        {
        }

        /// <summary>코드·테스트에서 조건을 만든다.</summary>
        public GazeCondition(string targetId, float seconds = 0f, float graceSeconds = -1f)
        {
            _targetId = targetId ?? string.Empty;
            _seconds = seconds;
            _graceSeconds = graceSeconds;
        }

        /// <inheritdoc/>
        public void OnStart(RuleSO card, JudgeWorld world, ConditionState state)
        {
            state.TimerMs = 0;
            state.ElapsedMs = 0;
        }

        /// <inheritdoc/>
        public bool Observe(in JudgeSignal signal, RuleSO card, JudgeWorld world, ConditionState state)
        {
            if (signal.Kind != SignalKind.GazeSample)
            {
                return false;
            }

            // 샘플은 (이전 경과, 이전 경과 + 샘플 시간] 구간을 대표한다. 시작점이 유예 안이면 세지 않는다.
            int sampleMs = ConditionState.ToMs(signal.Value);
            int sampleStartMs = state.ElapsedMs;
            state.ElapsedMs += sampleMs;

            if (sampleStartMs < ConditionState.ToMs(ResolveGrace(card)))
            {
                state.TimerMs = 0;
                return false;
            }

            bool onTarget = !string.IsNullOrEmpty(signal.TargetId) && TargetMatch.Matches(_targetId, card, signal.TargetId, state);
            if (!onTarget)
            {
                state.TimerMs = 0;
                return false;
            }

            state.TimerMs += sampleMs;
            int needMs = ConditionState.ToMs(ResolveSeconds(card));
            return needMs > 0 && state.TimerMs >= needMs;
        }

        /// <inheritdoc/>
        public void CollectReferences(RuleSO card, List<string> ids)
        {
            TargetMatch.Collect(_targetId, card, ids);
        }

        /// <inheritdoc/>
        public string Describe()
        {
            return "응시 " + TargetMatch.Label(_targetId) + " " + (_seconds > 0f ? _seconds + "초" : "카드 응시 시간") + " 연속";
        }

        private float ResolveSeconds(RuleSO card)
        {
            if (_seconds > 0f)
            {
                return _seconds;
            }

            return card != null ? card.GazeSeconds : 0f;
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

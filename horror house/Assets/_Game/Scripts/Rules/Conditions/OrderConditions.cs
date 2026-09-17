using System;
using System.Collections.Generic;
using UnityEngine;

namespace NightDuty
{
    /// <summary>
    /// 「가드보다 먼저 일어남」. 가드 조건이 한 번도 성립하지 않은 상태에서 사건 조건이 성립하면 성립한다.
    /// 같은 신호에서 둘 다 성립하면 가드가 먼저인 것으로 본다(불성립).
    /// <para>
    /// 예: T3 — 닫힘 완료(가드) <b>전에</b> 화장실을 나감(사건).
    /// C6 — 두 교실 점검(가드) <b>전에</b> 밤 종료(사건).
    /// </para>
    /// </summary>
    [Serializable]
    [ConditionMenu("순서/가드보다 먼저 발생")]
    public sealed class BeforeCondition : ICondition
    {
        [SerializeReference, SubclassSelector, Tooltip("먼저 일어나면 안 되는 사건")]
        private ICondition _event;

        [SerializeReference, SubclassSelector, Tooltip("이것이 먼저 성립했으면 사건은 문제가 되지 않는다")]
        private ICondition _guard;

        /// <summary>직렬화용 기본 생성자.</summary>
        public BeforeCondition()
        {
        }

        /// <summary>코드·테스트에서 조건을 만든다.</summary>
        public BeforeCondition(ICondition happened, ICondition guard)
        {
            _event = happened;
            _guard = guard;
        }

        /// <inheritdoc/>
        public void OnStart(RuleSO card, JudgeWorld world, ConditionState state)
        {
            ConditionState e = state.Child(0);
            ConditionState g = state.Child(1);
            e.Latched = false;
            g.Latched = false;

            if (_event != null)
            {
                _event.OnStart(card, world, e);
            }

            if (_guard != null)
            {
                _guard.OnStart(card, world, g);
            }
        }

        /// <inheritdoc/>
        public bool Observe(in JudgeSignal signal, RuleSO card, JudgeWorld world, ConditionState state)
        {
            ConditionState e = state.Child(0);
            ConditionState g = state.Child(1);

            if (!g.Latched && _guard != null && _guard.Observe(signal, card, world, g))
            {
                g.Latched = true;
            }

            // 사건 쪽 타이머가 신호를 놓치지 않도록 가드 성립 여부와 무관하게 평가한다.
            bool happened = _event != null && _event.Observe(signal, card, world, e);
            return happened && !g.Latched;
        }

        /// <inheritdoc/>
        public void CollectReferences(RuleSO card, List<string> ids)
        {
            if (_event != null)
            {
                _event.CollectReferences(card, ids);
            }

            if (_guard != null)
            {
                _guard.CollectReferences(card, ids);
            }
        }

        /// <inheritdoc/>
        public string Describe()
        {
            string a = _event != null ? _event.Describe() : "(비어 있음)";
            string b = _guard != null ? _guard.Describe() : "(비어 있음)";
            return "[" + b + "] 전에 [" + a + "]";
        }
    }

    /// <summary>
    /// 문별 수동 개방 의무 장부. 대상 문에 <b>플레이어의 열기 명령</b>이 수락되면 의무가 생기고,
    /// 닫기 명령이 수락되면 출처와 무관하게 지워진다(문이 실제로 닫혔으므로). 다시 열면 다시 생긴다.
    /// 확인 신호(기본 <see cref="SignalKind.NightEndAccepted"/>)가 왔을 때 의무가 하나라도 남았으면 성립한다.
    /// 남은 문 개수와 무관하게 한 번이다. 연출이 움직인 문(자동 개방 등)은 의무를 만들지 않는다.
    /// <para>예: C6 — 직접 열어 둔 교실 문을 근무 종료 전까지 닫지 않음.</para>
    /// </summary>
    [Serializable]
    [ConditionMenu("문/수동 개방 의무 남음")]
    public sealed class DoorObligationCondition : ICondition
    {
        [SerializeField, Tooltip("문 ID. 비우면 카드의 대상 목록, *이면 아무 문")]
        private string _doorId = string.Empty;

        [SerializeField, Tooltip("이 신호가 왔을 때 남은 의무를 확인한다")]
        private SignalKind _checkAt = SignalKind.NightEndAccepted;

        /// <summary>직렬화용 기본 생성자.</summary>
        public DoorObligationCondition()
        {
        }

        /// <summary>코드·테스트에서 조건을 만든다.</summary>
        public DoorObligationCondition(string doorId, SignalKind checkAt = SignalKind.NightEndAccepted)
        {
            _doorId = doorId ?? string.Empty;
            _checkAt = checkAt;
        }

        /// <inheritdoc/>
        public void OnStart(RuleSO card, JudgeWorld world, ConditionState state)
        {
            state.Ids.Clear();
        }

        /// <inheritdoc/>
        public bool Observe(in JudgeSignal signal, RuleSO card, JudgeWorld world, ConditionState state)
        {
            if (signal.Kind == SignalKind.DoorCommandAccepted)
            {
                if (string.IsNullOrEmpty(signal.TargetId) || !TargetMatch.Matches(_doorId, card, signal.TargetId, state))
                {
                    return false;
                }

                if (signal.Flag)
                {
                    // 닫힘: 누가 닫았든 문은 닫혔다(연출이 닫은 문에 플레이어가 다시 닫기 명령을 낼 수는 없다).
                    state.Ids.Remove(signal.TargetId);
                }
                else if (signal.Source == ActionSource.Player)
                {
                    // 열림: 플레이어가 직접 연 경우만 의무. 저절로 열린 문은 그대로 둔다.
                    state.Ids.Add(signal.TargetId);
                }

                return false;
            }

            return signal.Kind == _checkAt && state.Ids.Count > 0;
        }

        /// <inheritdoc/>
        public void CollectReferences(RuleSO card, List<string> ids)
        {
            TargetMatch.Collect(_doorId, card, ids);
        }

        /// <inheritdoc/>
        public string Describe()
        {
            return _checkAt + " 때 직접 연 문 " + TargetMatch.Label(_doorId) + " 이(가) 열린 채 남음";
        }
    }
}

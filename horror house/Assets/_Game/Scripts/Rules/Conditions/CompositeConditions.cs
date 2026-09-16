using System;
using System.Collections.Generic;
using UnityEngine;

namespace NightDuty
{
    /// <summary>
    /// 자식 조건이 <b>모두</b> 한 번씩 성립하면 성립한다(순서 무관, 성립한 자식은 유지).
    /// 예: 「점검 구역 체류」와 「손전등 Off 유지」를 함께 요구하는 카드.
    /// </summary>
    [Serializable]
    [ConditionMenu("조합/모두 성립")]
    public sealed class AllOfCondition : ICondition
    {
        [SerializeReference, SubclassSelector]
        private List<ICondition> _conditions = new List<ICondition>();

        /// <summary>직렬화용 기본 생성자.</summary>
        public AllOfCondition()
        {
        }

        /// <summary>코드·테스트에서 조건을 만든다.</summary>
        public AllOfCondition(params ICondition[] conditions)
        {
            _conditions = new List<ICondition>(conditions);
        }

        /// <inheritdoc/>
        public void OnStart(RuleSO card, JudgeWorld world, ConditionState state)
        {
            EnsureList();
            for (int i = 0; i < _conditions.Count; i++)
            {
                ConditionState child = state.Child(i);
                child.Latched = false;
                if (_conditions[i] != null)
                {
                    _conditions[i].OnStart(card, world, child);
                }
            }
        }

        /// <inheritdoc/>
        public bool Observe(in JudgeSignal signal, RuleSO card, JudgeWorld world, ConditionState state)
        {
            EnsureList();
            if (_conditions.Count == 0)
            {
                return false;
            }

            bool all = true;
            for (int i = 0; i < _conditions.Count; i++)
            {
                ConditionState child = state.Child(i);
                if (!child.Latched && _conditions[i] != null && _conditions[i].Observe(signal, card, world, child))
                {
                    child.Latched = true;
                }

                all &= child.Latched;
            }

            return all;
        }

        /// <inheritdoc/>
        public void CollectReferences(RuleSO card, List<string> ids)
        {
            EnsureList();
            for (int i = 0; i < _conditions.Count; i++)
            {
                if (_conditions[i] != null)
                {
                    _conditions[i].CollectReferences(card, ids);
                }
            }
        }

        /// <inheritdoc/>
        public string Describe()
        {
            EnsureList();
            return "모두(" + CompositeText.Join(_conditions) + ")";
        }

        private void EnsureList()
        {
            if (_conditions == null)
            {
                _conditions = new List<ICondition>();
            }
        }
    }

    /// <summary>자식 조건 중 <b>하나라도</b> 성립하면 성립한다.</summary>
    [Serializable]
    [ConditionMenu("조합/하나라도 성립")]
    public sealed class AnyOfCondition : ICondition
    {
        [SerializeReference, SubclassSelector]
        private List<ICondition> _conditions = new List<ICondition>();

        /// <summary>직렬화용 기본 생성자.</summary>
        public AnyOfCondition()
        {
        }

        /// <summary>코드·테스트에서 조건을 만든다.</summary>
        public AnyOfCondition(params ICondition[] conditions)
        {
            _conditions = new List<ICondition>(conditions);
        }

        /// <inheritdoc/>
        public void OnStart(RuleSO card, JudgeWorld world, ConditionState state)
        {
            EnsureList();
            for (int i = 0; i < _conditions.Count; i++)
            {
                if (_conditions[i] != null)
                {
                    _conditions[i].OnStart(card, world, state.Child(i));
                }
            }
        }

        /// <inheritdoc/>
        public bool Observe(in JudgeSignal signal, RuleSO card, JudgeWorld world, ConditionState state)
        {
            EnsureList();
            bool any = false;

            // 한 자식이 성립해도 나머지 자식의 타이머가 같은 신호를 놓치지 않도록 끝까지 돈다.
            for (int i = 0; i < _conditions.Count; i++)
            {
                if (_conditions[i] != null && _conditions[i].Observe(signal, card, world, state.Child(i)))
                {
                    any = true;
                }
            }

            return any;
        }

        /// <inheritdoc/>
        public void CollectReferences(RuleSO card, List<string> ids)
        {
            EnsureList();
            for (int i = 0; i < _conditions.Count; i++)
            {
                if (_conditions[i] != null)
                {
                    _conditions[i].CollectReferences(card, ids);
                }
            }
        }

        /// <inheritdoc/>
        public string Describe()
        {
            EnsureList();
            return "하나라도(" + CompositeText.Join(_conditions) + ")";
        }

        private void EnsureList()
        {
            if (_conditions == null)
            {
                _conditions = new List<ICondition>();
            }
        }
    }

    /// <summary>사건 시작 후 지정 시간이 지나면 성립한다(시퀀스 제한시간 등). 시간은 Tick 신호로만 흐른다.</summary>
    [Serializable]
    [ConditionMenu("시간/경과")]
    public sealed class ElapsedCondition : ICondition
    {
        [SerializeField, Tooltip("경과 시간(초)")]
        private float _seconds;

        /// <summary>직렬화용 기본 생성자.</summary>
        public ElapsedCondition()
        {
        }

        /// <summary>코드·테스트에서 조건을 만든다.</summary>
        public ElapsedCondition(float seconds)
        {
            _seconds = seconds;
        }

        /// <inheritdoc/>
        public void OnStart(RuleSO card, JudgeWorld world, ConditionState state)
        {
            state.ElapsedMs = 0;
        }

        /// <inheritdoc/>
        public bool Observe(in JudgeSignal signal, RuleSO card, JudgeWorld world, ConditionState state)
        {
            if (signal.Kind != SignalKind.Tick)
            {
                return false;
            }

            state.ElapsedMs += ConditionState.ToMs(signal.Value);
            return state.ElapsedMs >= ConditionState.ToMs(_seconds);
        }

        /// <inheritdoc/>
        public void CollectReferences(RuleSO card, List<string> ids)
        {
        }

        /// <inheritdoc/>
        public string Describe()
        {
            return _seconds + "초 경과";
        }
    }

    internal static class CompositeText
    {
        public static string Join(List<ICondition> conditions)
        {
            List<string> parts = new List<string>();
            for (int i = 0; i < conditions.Count; i++)
            {
                parts.Add(conditions[i] != null ? conditions[i].Describe() : "(비어 있음)");
            }

            return string.Join(", ", parts);
        }
    }
}

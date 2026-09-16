using System;
using System.Collections.Generic;
using UnityEngine;

namespace NightDuty.Tests
{
    /// <summary>테스트용 카드·판정 구성 도우미. 만든 에셋은 <see cref="Dispose"/>에서 지운다.</summary>
    internal sealed class TestKit : IDisposable
    {
        public const float Step = 0.1f;

        private readonly List<RuleSO> _created = new List<RuleSO>();

        public FearAxisSystem Axes { get; } = new FearAxisSystem();

        public RuleSO Card(Action<RuleSO.Config> setup)
        {
            RuleSO.Config config = new RuleSO.Config();
            setup(config);
            RuleSO card = ScriptableObject.CreateInstance<RuleSO>();
            card.Configure(config);
            _created.Add(card);
            return card;
        }

        public RuleBook Book(params RuleSO[] deck)
        {
            return new RuleBook(deck, Axes, null, null);
        }

        /// <summary>0.1초 단위로 시간을 흘리며, 매 단계 응시 샘플을 함께 보낸다.</summary>
        public static void Advance(RuleBook book, float seconds, string gazeTarget = "")
        {
            int steps = Mathf.RoundToInt(seconds / Step);
            for (int i = 0; i < steps; i++)
            {
                book.Dispatch(JudgeSignal.Tick(Step));
                book.Dispatch(JudgeSignal.Gaze(gazeTarget, Step));
            }
        }

        public void Dispose()
        {
            for (int i = 0; i < _created.Count; i++)
            {
                if (_created[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(_created[i]);
                }
            }

            _created.Clear();
            EventBus.ClearAll();
        }
    }
}

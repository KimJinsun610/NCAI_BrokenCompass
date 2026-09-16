using System;
using System.Collections.Generic;
using UnityEngine;

namespace NightDuty
{
    /// <summary>불리언 값(<see cref="JudgeSignal.Flag"/>) 필터.</summary>
    public enum FlagFilter
    {
        /// <summary>상관없음.</summary>
        Any = 0,

        /// <summary>Flag가 true일 때만(닫기 명령, 켜짐, 열림).</summary>
        True = 1,

        /// <summary>Flag가 false일 때만.</summary>
        False = 2
    }

    /// <summary>
    /// 지정한 종류의 신호가 오면 성립한다. 문 조작, 구역 진입·이탈, 통행·점검 완료, 시퀀스 종료, 모형 관찰 등
    /// 「무엇이 일어났다」로 표현되는 조건은 모두 이것으로 만든다.
    /// </summary>
    [Serializable]
    [ConditionMenu("신호/지정 신호 발생")]
    public sealed class SignalCondition : ICondition
    {
        [SerializeField, Tooltip("기다릴 신호 종류")]
        private SignalKind _kind;

        [SerializeField, Tooltip("대상 ID. 비우면 카드의 대상 목록, *이면 아무 대상")]
        private string _targetId = string.Empty;

        [SerializeField, Tooltip("공간 신호(진입·이탈·점검 완료)의 공간. None이면 아무 공간")]
        private SpaceId _space;

        [SerializeField, Tooltip("Flag 필터 — 문 명령이면 True = 닫기")]
        private FlagFilter _flag;

        [SerializeField, Tooltip("문 신호에서 플레이어 조작만 셀지. 연출이 움직인 문은 조작이 아니다")]
        private bool _playerOnly = true;

        /// <summary>직렬화용 기본 생성자.</summary>
        public SignalCondition()
        {
        }

        /// <summary>코드·테스트에서 조건을 만든다.</summary>
        public SignalCondition(SignalKind kind, string targetId = "", SpaceId space = SpaceId.None, FlagFilter flag = FlagFilter.Any, bool playerOnly = true)
        {
            _kind = kind;
            _targetId = targetId ?? string.Empty;
            _space = space;
            _flag = flag;
            _playerOnly = playerOnly;
        }

        /// <inheritdoc/>
        public void OnStart(RuleSO card, JudgeWorld world, ConditionState state)
        {
        }

        /// <inheritdoc/>
        public bool Observe(in JudgeSignal signal, RuleSO card, JudgeWorld world, ConditionState state)
        {
            if (signal.Kind != _kind)
            {
                return false;
            }

            if (_space != SpaceId.None && signal.Space != _space)
            {
                return false;
            }

            if (_flag == FlagFilter.True && !signal.Flag)
            {
                return false;
            }

            if (_flag == FlagFilter.False && signal.Flag)
            {
                return false;
            }

            if (_playerOnly && IsDoorKind(_kind) && signal.Source != ActionSource.Player)
            {
                return false;
            }

            if (!UsesTarget(_kind))
            {
                return true;
            }

            return TargetMatch.Matches(_targetId, card, signal.TargetId);
        }

        /// <inheritdoc/>
        public void CollectReferences(RuleSO card, List<string> ids)
        {
            if (UsesTarget(_kind))
            {
                TargetMatch.Collect(_targetId, card, ids);
            }
        }

        /// <inheritdoc/>
        public string Describe()
        {
            string text = _kind.ToString();
            if (UsesTarget(_kind))
            {
                text += " " + TargetMatch.Label(_targetId);
            }

            if (_space != SpaceId.None)
            {
                text += " @" + _space;
            }

            if (_flag != FlagFilter.Any)
            {
                text += " flag=" + _flag;
            }

            return text;
        }

        private static bool IsDoorKind(SignalKind kind)
        {
            return kind == SignalKind.DoorCommandAccepted || kind == SignalKind.DoorCloseCompleted;
        }

        /// <summary>대상 ID가 의미 있는 신호인지. 공간·시간·손전등·Tab 신호는 대상이 없다.</summary>
        internal static bool UsesTarget(SignalKind kind)
        {
            switch (kind)
            {
                case SignalKind.None:
                case SignalKind.Tick:
                case SignalKind.SpaceEntered:
                case SignalKind.SpaceExited:
                case SignalKind.InspectionCompleted:
                case SignalKind.FlashlightChanged:
                case SignalKind.TabChanged:
                case SignalKind.NightEndAccepted:
                    return false;
                default:
                    return true;
            }
        }
    }
}

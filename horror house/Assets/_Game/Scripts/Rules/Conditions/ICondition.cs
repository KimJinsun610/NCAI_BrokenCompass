using System;
using System.Collections.Generic;
using UnityEngine;

namespace NightDuty
{
    /// <summary>
    /// 카드의 준수·위반·취소 조건 하나. <see cref="RuleSO"/>에 <c>[SerializeReference]</c>로 들어가며,
    /// 기획팀이 인스펙터에서 종류를 골라 조합한다.
    /// <para>
    /// <b>구현체는 상태를 필드에 저장하면 안 된다.</b> 조건 인스턴스는 에셋에 직렬화된 공유 데이터라서,
    /// 필드에 타이머를 쓰면 여러 판·에디터 세션에 값이 새어 나간다(ScriptableObject 값은 플레이 종료 후에도 남는다).
    /// 실행 중 값은 매 사건마다 새로 만드는 <see cref="ConditionState"/>에 둔다.
    /// </para>
    /// </summary>
    public interface ICondition
    {
        /// <summary>사건이 진행 중으로 바뀔 때 한 번 호출된다. 타이머를 초기화한다.</summary>
        void OnStart(RuleSO card, JudgeWorld world, ConditionState state);

        /// <summary>신호 하나를 관찰하고, 이 신호로 조건이 성립했으면 true를 돌려준다.</summary>
        bool Observe(in JudgeSignal signal, RuleSO card, JudgeWorld world, ConditionState state);

        /// <summary>이 조건이 씬에 등록돼 있어야 하는 대상 ID를 모은다(시작 전 참조 검사용).</summary>
        void CollectReferences(RuleSO card, List<string> ids);

        /// <summary>개발 로그용 한 줄 설명.</summary>
        string Describe();
    }

    /// <summary>
    /// 조건 하나의 실행 중 상태. 카드 사건이 시작될 때마다 새로 만든다.
    /// 시간은 부동소수 누적 오차(2.9초와 3.0초 구분)를 피하려고 <b>밀리초 정수</b>로 센다.
    /// </summary>
    public sealed class ConditionState
    {
        /// <summary>조건별 누적 시간(ms). 응시 연속 시간 등.</summary>
        public int TimerMs;

        /// <summary>사건 시작 후 경과 시간(ms). 유예 판정에 쓴다.</summary>
        public int ElapsedMs;

        /// <summary>한 번 성립하면 유지되는 표시(복합 조건용).</summary>
        public bool Latched;

        /// <summary>이 사건을 시작시킨 신호의 대상 ID. 조건 대상 <c>@trigger</c>가 이 값과 비교된다.</summary>
        public string TriggerTargetId;

        private List<ConditionState> _children;
        private HashSet<string> _ids;

        /// <summary>ID 집합(문별 의무 장부 등). 처음 쓸 때 만든다.</summary>
        public HashSet<string> Ids
        {
            get
            {
                if (_ids == null)
                {
                    _ids = new HashSet<string>();
                }

                return _ids;
            }
        }

        /// <summary>복합 조건의 i번째 자식 상태. 없으면 만든다.</summary>
        public ConditionState Child(int index)
        {
            if (_children == null)
            {
                _children = new List<ConditionState>();
            }

            while (_children.Count <= index)
            {
                _children.Add(new ConditionState());
            }

            ConditionState child = _children[index];
            child.TriggerTargetId = TriggerTargetId;
            return child;
        }

        /// <summary>초 단위를 밀리초 정수로 바꾼다.</summary>
        public static int ToMs(float seconds)
        {
            return Mathf.RoundToInt(seconds * 1000f);
        }
    }

    /// <summary>
    /// 인스펙터의 조건 종류 선택 메뉴에 보일 이름. 기획팀이 읽는 이름이므로 한국어로 짓는다.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class ConditionMenuAttribute : Attribute
    {
        /// <summary>메뉴 경로(예: "근접/반경 안 진입").</summary>
        public string Path { get; }

        /// <summary>메뉴 경로를 지정한다.</summary>
        public ConditionMenuAttribute(string path)
        {
            Path = path;
        }
    }

    /// <summary>
    /// <c>[SerializeReference]</c> 필드에 종류 선택 드롭다운을 붙인다.
    /// Unity 기본 인스펙터는 SerializeReference에 타입 선택 UI를 주지 않는다. 그리는 코드는 Editor 어셈블리에 있다.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class SubclassSelectorAttribute : PropertyAttribute
    {
    }
}

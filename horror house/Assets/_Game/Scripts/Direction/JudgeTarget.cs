using System.Collections.Generic;
using UnityEngine;

namespace NightDuty
{
    /// <summary>
    /// 씬 오브젝트에 판정 대상 ID를 붙이는 표식. 켜지면 <see cref="JudgeTargetRegistry"/>에 올라간다.
    /// <para>
    /// ID 규칙(연결 약속 §4.4): <c>공간접두어.대상</c> — 예 <c>corridor.door.13</c>, <c>corridor.box</c>.
    /// 한 오브젝트가 여러 ID를 가질 수 있다(예: 문 = 문 ID + 응시 대상 ID). 첫 번째가 <see cref="PrimaryId"/>다.
    /// </para>
    /// <para>
    /// 클라이언트 사용 예: 응시 레이캐스트가 맞힌 콜라이더에서 <see cref="IdOf"/>로 ID를 얻어
    /// <c>JudgeSignal.Gaze(id, 0.1f)</c>를 보낸다. 근접 샘플의 기준점 위치는 이 오브젝트의 transform이다.
    /// </para>
    /// </summary>
    [AddComponentMenu("NightDuty/Judge Target")]
    [DisallowMultipleComponent]
    public sealed class JudgeTarget : MonoBehaviour
    {
        [Tooltip("판정 대상 ID. 카드의 시작 ID·대상 ID와 글자까지 같아야 한다. 첫 번째가 대표 ID.")]
        [SerializeField] private string[] _ids = new string[0];

        private readonly List<string> _registered = new List<string>();

        /// <summary>이 표식의 ID들.</summary>
        public IReadOnlyList<string> Ids
        {
            get { return _ids ?? new string[0]; }
        }

        /// <summary>대표 ID. 없으면 빈 문자열.</summary>
        public string PrimaryId
        {
            get { return _ids != null && _ids.Length > 0 && _ids[0] != null ? _ids[0].Trim() : string.Empty; }
        }

        /// <summary>근접 판정 기준점(발밑 높이와 무관하게 수평 거리만 쓴다).</summary>
        public Vector3 AnchorPosition
        {
            get { return transform.position; }
        }

        /// <summary>콜라이더 등에서 가장 가까운 상위 표식의 대표 ID를 찾는다. 없으면 빈 문자열.</summary>
        public static string IdOf(Component hit)
        {
            if (hit == null)
            {
                return string.Empty;
            }

            JudgeTarget target = hit.GetComponentInParent<JudgeTarget>();
            return target != null ? target.PrimaryId : string.Empty;
        }

        /// <summary>코드에서 ID를 지정한다(에디터 도구·테스트). 켜져 있으면 다시 등록한다.</summary>
        public void SetIds(params string[] ids)
        {
            bool wasRegistered = _registered.Count > 0;
            if (wasRegistered)
            {
                UnregisterAll();
            }

            _ids = ids ?? new string[0];

            if (wasRegistered || (Application.isPlaying && isActiveAndEnabled))
            {
                RegisterAll();
            }
        }

        private void OnEnable()
        {
            RegisterAll();
        }

        private void OnDisable()
        {
            UnregisterAll();
        }

        private void RegisterAll()
        {
            if (_ids == null)
            {
                return;
            }

            for (int i = 0; i < _ids.Length; i++)
            {
                string id = _ids[i] != null ? _ids[i].Trim() : string.Empty;
                if (id.Length == 0 || _registered.Contains(id))
                {
                    continue;
                }

                JudgeTargetRegistry.Register(id, this);
                _registered.Add(id);
            }
        }

        private void UnregisterAll()
        {
            for (int i = 0; i < _registered.Count; i++)
            {
                JudgeTargetRegistry.Unregister(_registered[i], this);
            }

            _registered.Clear();
        }
    }
}

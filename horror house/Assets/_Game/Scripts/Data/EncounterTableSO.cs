using System;
using System.Collections.Generic;
using UnityEngine;

namespace NightDuty
{
    /// <summary>
    /// 조우 8장면(H-A · H-B · C-A · C-B · S-A · S-B · T-A · T-B)의 데이터.
    /// <para>
    /// <b>이 표는 좌표를 갖지 않는다.</b> 장면이 가리키는 것은 전부 씬의 <see cref="JudgeTarget"/> ID 문자열이고,
    /// 실제 위치·회전은 씬 쪽이 그 ID를 단 빈 오브젝트로 잡는다. 판정 코어가 위치·물리·카메라를 모른다는
    /// 원칙(<see cref="JudgeSignal"/> 주석)을 조우에도 그대로 적용한 것이다 —
    /// 여기에 <c>Vector3</c>를 한 번 넣으면 <see cref="EncounterDirector"/>가 씬 좌표계를 알게 되고,
    /// 그때부터 레벨을 손볼 때마다 코어 에셋을 같이 고쳐야 한다.
    /// </para>
    /// <para>
    /// <b>접근 3지점</b>(<see cref="Scene.ApproachTargetIds"/>)이 이 표의 핵심이다. 모형은 하루 안에서
    /// 최대 세 번 자리를 옮기는데, 어느 지점이 「동선 3m 이내」이고 어느 지점이 「문 옆」인지는
    /// 레벨을 본 사람만 알 수 있다. 그래서 단계 수(3)만 코드가 정하고 지점은 전부 데이터로 둔다.
    /// </para>
    /// <para>
    /// <see cref="EncounterDirector"/>가 <c>Resources/EncounterTable</c>에서 찾는다. 이름과 위치를 바꾸지 말 것.
    /// </para>
    /// </summary>
    [CreateAssetMenu(menuName = "NightDuty/Encounter Table", fileName = "EncounterTable")]
    public sealed class EncounterTableSO : ScriptableObject
    {
        /// <summary><see cref="Resources.Load(string)"/> 경로.</summary>
        public const string ResourcePath = "EncounterTable";

        /// <summary>
        /// 장면마다 가져야 하는 접근 지점 수. <see cref="EncounterDirector.ApproachStepCount"/>와 같은 값이며
        /// 여기서 한 번 더 상수로 두는 이유는 <see cref="Validate"/>가 에디터에서 데이터만 보고 검사할 수 있어야 하기 때문이다.
        /// </summary>
        public const int ApproachCount = EncounterDirector.ApproachStepCount;

        private static readonly string[] NoIds = new string[0];
        private static readonly Scene[] NoScenes = new Scene[0];

        /// <summary>조우 장면 한 개.</summary>
        [Serializable]
        public sealed class Scene
        {
            [Tooltip("장면 ID. scene.ha · scene.hb · scene.ca · scene.cb · scene.sa · scene.sb · scene.ta · scene.tb")]
            public string SceneId = string.Empty;

            [Tooltip("장면이 벌어지는 공간. H=복도 C=Classroom_1_1 S=ScienceRoom T=Toilet")]
            public SpaceId Space;

            [Tooltip("발견형인가. 발견형(H-B · C-B · T-B)은 두 선택 모두 델타 0이라 새 판정을 만들지 않는다")]
            public bool IsDiscovery;

            [Tooltip("이 장면이 소유한 발견유도 문자 ID (G-H1 등). 비우면 유도 문자가 없다 — S-A만 비운다")]
            public string MessageId = string.Empty;

            [TextArea(2, 5), Tooltip("유도 문자 본문. 태블릿에 이 문구만 노출한다(발신자 표시 없음)")]
            public string MessageText = string.Empty;

            [Tooltip("접근 3지점의 대상 ID. 0=원래 장면 지점 1=동선 3m 이내 2=동선 위·문 옆. 순서가 곧 접근 순서다")]
            public string[] ApproachTargetIds = new string[ApproachCount];

            [Tooltip("퇴실 게이트 지점의 대상 ID. 출입구와 플레이어 사이. 게이트를 쓰지 않는 장면은 비운다")]
            public string ExitGateTargetId = string.Empty;

            /// <summary>
            /// 접근 단계(1~<see cref="ApproachCount"/>)의 대상 ID. 범위를 벗어나거나 칸이 비어 있으면 빈 문자열.
            /// <para>
            /// 단계를 1부터 세는 것은 <see cref="EncounterDirector"/>의 「아직 한 번도 안 놓음 = 0」과 맞추기 위해서다.
            /// 배열 인덱스와 한 칸 어긋나므로 직접 인덱싱하지 말고 이 메서드를 쓸 것.
            /// </para>
            /// </summary>
            public string ApproachOf(int step)
            {
                if (ApproachTargetIds == null || step < 1 || step > ApproachTargetIds.Length)
                {
                    return string.Empty;
                }

                return ApproachTargetIds[step - 1] ?? string.Empty;
            }
        }

        [Header("조우 8장면")]
        [SerializeField, Tooltip("8장면 전부. 순서는 상관없다 — 회차 배분은 EncounterDirector가 일차별로 들고 있다")]
        private Scene[] _scenes = new Scene[0];

        [Header("재방문 문자 (N1) — 장면에 속하지 않는 단 하나의 문자")]
        [SerializeField, Tooltip("재방문 문자 ID. 보통 N1")]
        private string _noticeMessageId = EncounterDirector.NoticeMessageId;

        [SerializeField, TextArea(2, 5), Tooltip("재방문 문자 본문. 새 이상현상 없이 목적지 도착만 요구한다")]
        private string _noticeText = string.Empty;

        [SerializeField, Tooltip("재방문 문자가 부르는 공간")]
        private SpaceId _noticeSpace = SpaceId.Corridor;

        /// <summary>표에 실린 장면 전부.</summary>
        public IReadOnlyList<Scene> Scenes
        {
            get { return _scenes ?? NoScenes; }
        }

        /// <summary>재방문 문자 ID.</summary>
        public string NoticeMessageId
        {
            get { return _noticeMessageId ?? string.Empty; }
        }

        /// <summary>재방문 문자 본문.</summary>
        public string NoticeText
        {
            get { return _noticeText ?? string.Empty; }
        }

        /// <summary>재방문 문자가 부르는 공간.</summary>
        public SpaceId NoticeSpace
        {
            get { return _noticeSpace; }
        }

        /// <summary>장면 ID로 찾는다. 없으면 null.</summary>
        public Scene Find(string sceneId)
        {
            if (_scenes == null || string.IsNullOrEmpty(sceneId))
            {
                return null;
            }

            for (int i = 0; i < _scenes.Length; i++)
            {
                Scene s = _scenes[i];
                if (s != null && s.SceneId == sceneId)
                {
                    return s;
                }
            }

            return null;
        }

        /// <summary>장면 ID의 표 인덱스. 없으면 -1. <see cref="EncounterDirector"/>가 상태 배열을 표와 1:1로 잡는 데 쓴다.</summary>
        public int IndexOf(string sceneId)
        {
            if (_scenes == null || string.IsNullOrEmpty(sceneId))
            {
                return -1;
            }

            for (int i = 0; i < _scenes.Length; i++)
            {
                Scene s = _scenes[i];
                if (s != null && s.SceneId == sceneId)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// 이 표가 씬에 요구하는 대상 ID를 모은다(밤 시작 전 참조 검사용).
        /// 접근 3지점과 퇴실 게이트 지점이 전부 <see cref="JudgeTarget"/>로 씬에 있어야 모형을 놓을 자리가 생긴다.
        /// </summary>
        public void CollectReferences(List<string> ids)
        {
            if (ids == null || _scenes == null)
            {
                return;
            }

            for (int i = 0; i < _scenes.Length; i++)
            {
                Scene s = _scenes[i];
                if (s == null)
                {
                    continue;
                }

                if (s.ApproachTargetIds != null)
                {
                    for (int k = 0; k < s.ApproachTargetIds.Length; k++)
                    {
                        if (!string.IsNullOrEmpty(s.ApproachTargetIds[k]))
                        {
                            ids.Add(s.ApproachTargetIds[k]);
                        }
                    }
                }

                if (!string.IsNullOrEmpty(s.ExitGateTargetId))
                {
                    ids.Add(s.ExitGateTargetId);
                }
            }
        }

        /// <summary>
        /// 데이터 규칙 위반을 모은다. 비어 있으면 통과.
        /// <para>
        /// 검사하는 것은 「뒤집히면 설계가 무너지는 것」뿐이다(<see cref="RuleSO.Validate"/>와 같은 기준).
        /// 문구나 좌표의 좋고 나쁨은 여기서 따지지 않는다.
        /// </para>
        /// <list type="bullet">
        /// <item><b>8장면이 전부 있어야 한다</b> — 회차 배분이 8칸을 정확히 채우는 고정표라, 한 장면이라도 빠지면
        /// 그날 아무것도 깔리지 않는 밤이 조용히 생긴다.</item>
        /// <item><b>접근 지점이 3개여야 한다</b> — 필연 조우 1단계가 통째로 데이터에 있다. 한 칸이 비면
        /// 그 장면은 「몰라볼 수도 있는 시야 주변부」에서 더 나아가지 못한다.</item>
        /// <item><b>S-A에만 유도 문자가 없어야 한다</b> — G 7통의 주인이 장면이므로, 여기가 어긋나면
        /// 7통 중 몇 통은 회차 내내 발송 기회를 못 얻는다.</item>
        /// <item><b>퇴실 게이트 지점은 게이트 장면 셋만</b> 갖는다. 전부에 붙이면 패턴이 읽힌다는 것이
        /// 2026-09-21 결정의 이유라, 데이터가 조용히 넷째를 늘리는 것을 막는다.</item>
        /// </list>
        /// </summary>
        public void Validate(List<string> errors)
        {
            if (errors == null)
            {
                return;
            }

            IReadOnlyList<string> all = EncounterDirector.AllSceneIds;

            int count = _scenes != null ? _scenes.Length : 0;
            if (count != all.Count)
            {
                errors.Add(name + ": 장면이 " + count + "개입니다. " + all.Count + "개여야 합니다.");
            }

            // 8장면이 하나씩, 중복 없이 있는가.
            for (int i = 0; i < all.Count; i++)
            {
                if (Find(all[i]) == null)
                {
                    errors.Add(name + ": 장면 " + all[i] + "이(가) 없습니다.");
                }
            }

            if (_scenes == null)
            {
                return;
            }

            for (int i = 0; i < _scenes.Length; i++)
            {
                Scene s = _scenes[i];
                if (s == null)
                {
                    errors.Add(name + ": " + i + "번 칸이 비어 있습니다.");
                    continue;
                }

                string id = string.IsNullOrEmpty(s.SceneId) ? "(" + i + "번 칸)" : s.SceneId;

                if (string.IsNullOrEmpty(s.SceneId) || !EncounterDirector.IsSceneId(s.SceneId))
                {
                    errors.Add(name + ": " + id + "은(는) 조우 장면 ID가 아닙니다.");
                    continue;
                }

                if (IndexOf(s.SceneId) != i)
                {
                    errors.Add(name + ": " + id + "이(가) 두 번 실려 있습니다.");
                }

                if (s.Space == SpaceId.None)
                {
                    errors.Add(name + ": " + id + "의 공간이 지정되지 않았습니다.");
                }

                int approaches = s.ApproachTargetIds != null ? s.ApproachTargetIds.Length : 0;
                if (approaches != ApproachCount)
                {
                    errors.Add(name + ": " + id + "의 접근 지점이 " + approaches + "개입니다. " + ApproachCount + "개여야 합니다.");
                }
                else
                {
                    for (int k = 0; k < approaches; k++)
                    {
                        if (string.IsNullOrEmpty(s.ApproachTargetIds[k]))
                        {
                            errors.Add(name + ": " + id + "의 " + (k + 1) + "번 접근 지점이 비어 있습니다.");
                        }
                    }
                }

                bool wantsMessage = s.SceneId != EncounterDirector.FirstDaySceneId;
                bool hasMessage = !string.IsNullOrEmpty(s.MessageId);

                if (wantsMessage && !hasMessage)
                {
                    errors.Add(name + ": " + id + "에 유도 문자 ID가 없습니다(문자가 없는 장면은 " + EncounterDirector.FirstDaySceneId + "뿐입니다).");
                }

                if (!wantsMessage && hasMessage)
                {
                    errors.Add(name + ": " + id + "은(는) 1일차 고정 장면이라 유도 문자를 가질 수 없습니다.");
                }

                if (hasMessage && string.IsNullOrEmpty(s.MessageText))
                {
                    errors.Add(name + ": " + id + "의 유도 문자 본문이 비어 있습니다.");
                }

                bool wantsGate = EncounterDirector.UsesExitGate(s.SceneId);
                bool hasGate = !string.IsNullOrEmpty(s.ExitGateTargetId);

                if (wantsGate && !hasGate)
                {
                    errors.Add(name + ": " + id + "은(는) 퇴실 게이트 장면인데 게이트 지점이 없습니다.");
                }

                if (!wantsGate && hasGate)
                {
                    errors.Add(name + ": " + id + "에 퇴실 게이트 지점이 있습니다. 게이트는 " +
                               EncounterDirector.ExitGateSceneList() + " 셋뿐입니다(전부에 붙이면 패턴이 읽힙니다).");
                }

                if (s.IsDiscovery != EncounterDirector.IsDiscovery(s.SceneId))
                {
                    errors.Add(name + ": " + id + "의 발견형 표시가 설계와 다릅니다(발견형은 " +
                               EncounterDirector.DiscoverySceneList() + " 셋입니다).");
                }
            }

            if (string.IsNullOrEmpty(_noticeMessageId))
            {
                errors.Add(name + ": 재방문 문자 ID가 비어 있습니다.");
            }

            if (string.IsNullOrEmpty(_noticeText))
            {
                errors.Add(name + ": 재방문 문자 본문이 비어 있습니다.");
            }

            // 재방문 문자(N1)의 목적지는 **그날 정해진다** — 새 이상현상 없이 「다시 가 보라」고만 부르므로
            // 표에 고정 공간을 적을 수 없다. SpaceId.None이 정상이며 오류가 아니다.
            // 표에 공간을 적어 두면 그 값이 그날 목적지를 덮어써서 기획 의도가 깨진다.
        }

        /// <summary>에디터 도구·테스트가 코드로 표를 채울 때 쓴다.</summary>
        internal void SetScenes(Scene[] scenes)
        {
            _scenes = scenes ?? new Scene[0];
        }

        /// <summary>에디터 도구·테스트가 재방문 문자를 채울 때 쓴다.</summary>
        internal void SetNotice(string messageId, string text, SpaceId space)
        {
            _noticeMessageId = messageId ?? string.Empty;
            _noticeText = text ?? string.Empty;
            _noticeSpace = space;
        }

        /// <summary>에디터 도구용: 장면 배열 복사본(빈 칸 포함). 없으면 빈 배열.</summary>
        internal Scene[] RawScenes()
        {
            if (_scenes == null)
            {
                return new Scene[0];
            }

            return (Scene[])_scenes.Clone();
        }

        /// <summary>표가 비어 있을 때 돌려줄 빈 ID 목록(할당을 줄이려고 한 번만 만든다).</summary>
        internal static IReadOnlyList<string> EmptyIds
        {
            get { return NoIds; }
        }
    }
}

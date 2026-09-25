using System;
using System.Collections.Generic;
using UnityEngine;

namespace NightDuty
{
    /// <summary>
    /// 큐(연출 재생 단위) → 판정 신호 바인딩 표.
    /// <para>
    /// <see cref="SpaceAnomalyTableSO"/>의 큐 ID와 <see cref="RuleSO"/>가 기다리는 판정 ID는 <b>다른 이름 체계</b>다.
    /// 예: 표의 <c>chalk.3</c> ↔ C1의 <c>ClueDelivered("cls11.chalk3")</c>.
    /// </para>
    /// <para>
    /// <b>이름을 합쳐 개명하면 안 된다.</b> 합치는 순간 「모형 효과음·분위기 효과음은 카드 단서 ID를 보내지 않는다」
    /// (CLAUDE.md §2.7)를 구조적으로 표현할 수 없게 된다. 소리가 났다는 사실과 카드가 시작됐다는 사실은
    /// 서로 다른 사건이며(공통 명세 1절-3), 그 사이를 잇는 것이 <b>이 표</b>다.
    /// 그래서 큐는 「무엇을 재생·표시하는가」만 말하고, 판정 ID는 이 표에서만 붙는다.
    /// <c>Send == <see cref="CueSend.None"/></c>인 줄은 「연출만 하고 판정은 건드리지 않는다」는 <b>명시적 선언</b>이다.
    /// </para>
    /// <para>
    /// 이 표는 데이터일 뿐이다. 실제 발신은 <c>AnomalyCueDirector</c>(Assembly-CSharp)가 한다.
    /// Core는 연출을 참조하지 않는다(CLAUDE.md §4.1).
    /// </para>
    /// <para>
    /// <b>음원은 아직 없다.</b> <see cref="Binding.ShotCount"/>·<see cref="Binding.ShotIntervalSeconds"/>·
    /// <see cref="Binding.SequenceSeconds"/>는 나중에 클립이 들어올 자리를 미리 비워 둔 것이다.
    /// 클립이 오면 <c>PlayOneShot</c>과 클립 길이가 이 값을 대신하고 <b>판정은 그대로</b>다.
    /// </para>
    /// </summary>
    [CreateAssetMenu(menuName = "NightDuty/Cue Binding Table", fileName = "CueBindingTable")]
    public sealed class CueBindingTableSO : ScriptableObject
    {
        /// <summary>표 한 줄.</summary>
        [Serializable]
        public sealed class Binding
        {
            [Tooltip("큐 ID. 표(SpaceAnomalyTable)의 Cues 값과 글자까지 같아야 한다. " +
                     "표에 없는 자체 키는 'bind.'로 시작한다(조도 칸에는 큐가 없다).")]
            public string CueId = string.Empty;

            [Tooltip("어느 공간 표의 큐인가. 교실은 Classroom_1_1로 적는다(1-3은 같은 표를 쓴다).")]
            public SpaceId Space = SpaceId.None;

            [Tooltip("표의 어느 축 칸에서 온 큐인가(청각·조도·배치).")]
            public FearAxis Axis = FearAxis.Auditory;

            [Tooltip("보낼 판정 신호. None이면 연출 전용이며 카드 단서 ID를 보내지 않는다(§2.7).")]
            public CueSend Send = CueSend.None;

            [Tooltip("보낼 신호의 대상 ID(= 카드가 기다리는 판정 ID). Send가 None이면 비워 둔다.")]
            public string JudgeId = string.Empty;

            [Tooltip("언제 발동하는가. 기획서 H절의 문장을 열거형으로 옮긴 것이다.")]
            public CueMoment Moment = CueMoment.None;

            [Tooltip("어떤 방문에서 발동하는가. SeparateVisit = 「별도 방문」(해석은 README 참조).")]
            public CueVisit Visit = CueVisit.AnyVisit;

            [Tooltip("같은 큐를 몇 번까지 발동하는가.")]
            public CueRepeat Repeat = CueRepeat.OncePerVisit;

            [Tooltip("전달 조건. 청각은 청취 구역 안, 시각은 식별 0.2초.")]
            public CueDelivery Delivery = CueDelivery.Immediate;

            [Tooltip("청취 구역 / 안전 관찰 구역 ID(SpaceZones의 신호 구역). 비우면 공간 안이기만 하면 된다.")]
            public string ZoneId = string.Empty;

            [Tooltip("식별 대상 ID. Delivery가 GazeIdentify*일 때만 쓴다. " +
                     "판정 ID(JudgeId)와 다를 수 있다 — T6이 그 예다.")]
            public string[] IdentifyTargetIds = new string[0];

            [Tooltip("식별 연속 응시 시간(초). 공통 명세 2절의 시험값 0.2초.")]
            [Min(0.05f)] public float IdentifySeconds = 0.2f;

            [Tooltip("켜면 ClueIdentified의 대상 ID로 JudgeId 대신 '실제로 식별한 대상'을 보낸다. " +
                     "한 카드가 공간마다 다른 대상을 쓸 때 필요하다(C5: 교실 1-1은 cls11.lights, 1-3은 cls13.lights).")]
            public bool SendIdentifiedTargetId;

            [Tooltip("이번 방문의 큐 목록에 이 큐들이 모두 있어야 무장한다(T6: 칸 두 개가 모두 열린 구간).")]
            public string[] RequiresCues = new string[0];

            [Tooltip("켜면 이 큐는 표(SpaceAnomalyTable)에서 찾는다. 끄면 표에 큐가 없는 줄이며 " +
                     "아래 구간 범위로만 무장한다(조도 칸).")]
            public bool FromAnomalyTable = true;

            [Tooltip("FromAnomalyTable이 꺼져 있을 때의 무장 구간(시작).")]
            public Band BandFrom = Band.Band0;

            [Tooltip("FromAnomalyTable이 꺼져 있을 때의 무장 구간(끝).")]
            public Band BandTo = Band.Band4;

            [Tooltip("발동 시점에서 이만큼 늦춰 발신한다(초). 「앞쪽 닫힘 뒤」 같은 순서 표현에 쓴다.")]
            [Min(0f)] public float DelaySeconds;

            [Tooltip("단발 클립을 몇 번 재생하는가. C1의 「정확히 세 번」은 3이다. " +
                     "루프 클립이면 횟수를 셀 수 없다(CLAUDE.md §5.4-15).")]
            [Min(1)] public int ShotCount = 1;

            [Tooltip("단발 사이 간격(초). ShotCount가 2 이상일 때만 쓴다. 음원이 오면 발주값으로 교체한다.")]
            [Min(0f)] public float ShotIntervalSeconds;

            [Tooltip("연출 시퀀스 전체 길이(초). 음원 발주값과 같은 값을 쓴다(CLAUDE.md §5.4-16). " +
                     "0이면 미정이며 SequenceEnded를 보내지 않는다.")]
            [Min(0f)] public float SequenceSeconds;

            [Tooltip("시퀀스가 끝날 때 보낼 SequenceEnded의 대상 ID. 비우면 보내지 않는다.")]
            public string SequenceId = string.Empty;

            [Tooltip("켜면 조도값(구간)과 실제로 켜진 등 개수가 어긋날 때 사건을 시작하지 않고 개발 로그에 남긴다(CLAUDE.md §2.4).")]
            public bool RequireLitCountMatch;

            [Tooltip("이 줄이 트리거를 열어 주는 카드(확인용. 판정에는 쓰지 않는다).")]
            public string[] Cards = new string[0];

            [TextArea(1, 4), Tooltip("근거와 미확정 사항. 빈칸으로 둔 값의 이유를 여기 적는다.")]
            public string Notes = string.Empty;

            /// <summary>판정 신호를 보내는 줄인지.</summary>
            public bool Sends
            {
                get { return Send != CueSend.None && !string.IsNullOrEmpty(JudgeId); }
            }

            /// <summary>식별 0.2초로 전달되는 줄인지.</summary>
            public bool UsesGaze
            {
                get { return Delivery == CueDelivery.GazeIdentifyAny || Delivery == CueDelivery.GazeIdentifyAll; }
            }
        }

        private static readonly Binding[] NoBindings = new Binding[0];

        [SerializeField]
        private Binding[] _bindings = new Binding[0];

        /// <summary>전체 줄.</summary>
        public IReadOnlyList<Binding> Bindings
        {
            get { return _bindings ?? NoBindings; }
        }

        /// <summary>한 공간의 줄을 모은다(교실 1-3은 1-1 표를 쓴다).</summary>
        public void CollectFor(SpaceId space, List<Binding> into)
        {
            if (into == null || _bindings == null)
            {
                return;
            }

            SpaceId group = SpaceAnomalyTableSO.Group(space);
            for (int i = 0; i < _bindings.Length; i++)
            {
                Binding b = _bindings[i];
                if (b != null && b.Space == group)
                {
                    into.Add(b);
                }
            }
        }

        /// <summary>큐 ID와 공간으로 한 줄을 찾는다. 없으면 null.</summary>
        public Binding Find(string cueId, SpaceId space)
        {
            if (_bindings == null || string.IsNullOrEmpty(cueId))
            {
                return null;
            }

            SpaceId group = SpaceAnomalyTableSO.Group(space);
            for (int i = 0; i < _bindings.Length; i++)
            {
                Binding b = _bindings[i];
                if (b != null && b.Space == group && b.CueId == cueId)
                {
                    return b;
                }
            }

            return null;
        }

        /// <summary>
        /// 표 자체의 모순을 모은다(에디터 검수용). 크래시를 내지 않는다.
        /// <paramref name="anomaly"/>를 주면 큐 ID가 이상현상 표에 실제로 있는지도 본다.
        /// </summary>
        public void Validate(List<string> errors, SpaceAnomalyTableSO anomaly)
        {
            if (errors == null)
            {
                return;
            }

            if (_bindings == null)
            {
                return;
            }

            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < _bindings.Length; i++)
            {
                Binding b = _bindings[i];
                if (b == null)
                {
                    errors.Add("[" + i + "] 빈 줄");
                    continue;
                }

                string key = b.Space + "|" + b.CueId;
                if (!seen.Add(key))
                {
                    errors.Add(key + " 중복");
                }

                if (string.IsNullOrEmpty(b.CueId))
                {
                    errors.Add("[" + i + "] 큐 ID 없음");
                }

                if (b.Space == SpaceId.None)
                {
                    errors.Add(b.CueId + " 공간 없음");
                }

                if (b.Send != CueSend.None && string.IsNullOrEmpty(b.JudgeId))
                {
                    errors.Add(b.CueId + " 판정 ID 없음");
                }

                if (b.Send == CueSend.None && !string.IsNullOrEmpty(b.JudgeId))
                {
                    errors.Add(b.CueId + " 신호는 None인데 판정 ID가 있음");
                }

                if (b.Sends && b.Moment == CueMoment.None)
                {
                    errors.Add(b.CueId + " 발동 시점이 None인데 신호를 보내려 함");
                }

                if (b.UsesGaze && (b.IdentifyTargetIds == null || b.IdentifyTargetIds.Length == 0))
                {
                    errors.Add(b.CueId + " 식별 전달인데 식별 대상이 없음");
                }

                if (b.Delivery == CueDelivery.GazeIdentifyAll && b.IdentifyTargetIds != null && b.IdentifyTargetIds.Length < 2)
                {
                    errors.Add(b.CueId + " GazeIdentifyAll인데 대상이 둘 미만");
                }

                if (!string.IsNullOrEmpty(b.SequenceId) && b.SequenceSeconds <= 0f)
                {
                    errors.Add(b.CueId + " SequenceId가 있는데 길이가 0(음원 발주 대기)");
                }

                if (b.ShotCount > 1 && b.ShotIntervalSeconds <= 0f)
                {
                    errors.Add(b.CueId + " 단발 " + b.ShotCount + "회인데 간격이 0");
                }

                if (b.Moment == CueMoment.OnZoneEntered && string.IsNullOrEmpty(b.ZoneId))
                {
                    errors.Add(b.CueId + " 구역 진입 시점인데 구역 ID가 비어 있음(씬 작업 대기)");
                }

                if (b.FromAnomalyTable && anomaly != null && !CueExistsIn(anomaly, b))
                {
                    errors.Add(b.CueId + " 이상현상 표의 " + b.Space + "/" + b.Axis + " 칸 어디에도 없음");
                }

                if (!b.FromAnomalyTable && !b.CueId.StartsWith("bind.", StringComparison.Ordinal))
                {
                    errors.Add(b.CueId + " 표 밖의 자체 키는 'bind.'로 시작해야 한다");
                }
            }
        }

        private static bool CueExistsIn(SpaceAnomalyTableSO anomaly, Binding b)
        {
            for (int band = 0; band <= (int)Band.Band4; band++)
            {
                IReadOnlyList<string> cues = anomaly.CuesFor(b.Space, b.Axis, (Band)band);
                for (int i = 0; i < cues.Count; i++)
                {
                    if (cues[i] == b.CueId)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

#if UNITY_EDITOR
        /// <summary>
        /// 에디터 도구가 표를 채울 때만 쓴다. 런타임에서 부르지 말 것.
        /// (<c>internal</c>이 아닌 이유: 표를 만드는 도구가 <c>Assembly-CSharp-Editor</c>에 있어
        /// <c>NightDuty.Core</c>의 <c>InternalsVisibleTo</c> 밖이다. CLAUDE.md §5.1-3.)
        /// </summary>
        public void SetBindings(Binding[] bindings)
        {
            _bindings = bindings ?? new Binding[0];
        }
#endif
    }

    /// <summary>
    /// 큐가 보낼 판정 신호. 숫자는 <see cref="SignalKind"/>와 일부러 같게 두었다(에셋 직렬화값이므로 바꾸지 말 것).
    /// <b>여기에 문·모형·구역 신호는 넣지 않는다</b> — 그것들은 각자의 발신기가 보낸다.
    /// </summary>
    public enum CueSend
    {
        /// <summary>보내지 않는다. 분위기·모형·상태 연출 전용 큐(§2.7).</summary>
        None = 0,

        /// <summary>청각 단서 전달. 청취 구역 안에서 음원이 정상 재생됐다.</summary>
        ClueDelivered = 30,

        /// <summary>시각 단서 식별. 안전 관찰 지점에서 대상을 중앙에 0.2초.</summary>
        ClueIdentified = 31,

        /// <summary>지정 오디오·연출 시퀀스가 끝났다.</summary>
        SequenceEnded = 32
    }

    /// <summary>
    /// 큐의 발동 시점. 기획서 H절이 문장으로만 적어 둔 조건을 열거형으로 옮긴 것이다.
    /// <b>여기 없는 시점을 코드에서 지어내지 말 것</b> — 기획서에 문장이 없으면 표에 빈칸(<see cref="None"/>)으로 둔다.
    /// </summary>
    public enum CueMoment
    {
        /// <summary>미지정. 발신하지 않는다.</summary>
        None = 0,

        /// <summary>공간에 들어온 직후. 기획서 「입장 직후」·「입구에서」.</summary>
        OnSpaceEntered = 1,

        /// <summary>지정 구역에 들어갔을 때. 「문밖에서」처럼 공간 밖의 청취 자리에 쓴다.</summary>
        OnZoneEntered = 2,

        /// <summary>통행 구역에 들어갔을 때. 기획서 「지정 통행에서」·「통행 중」.</summary>
        OnPassageEntered = 3,

        /// <summary>통행을 마쳤을 때.</summary>
        OnPassageCompleted = 4,

        /// <summary>일반 점검이 완료됐을 때(= 퇴실 직전). 기획서 「점검 후 퇴실할 때」·「유효 점검 후 퇴실할 때」.</summary>
        OnInspectionCompleted = 5,

        /// <summary>공간을 나갔을 때. 청취 구역 밖이라 청각 단서에는 쓰지 않는다.</summary>
        OnSpaceExited = 6,

        /// <summary>방문 내내 상시. 배치 상태 큐의 식별(0.2초)에 쓴다.</summary>
        WhileInSpace = 7
    }

    /// <summary>어떤 방문에서 발동하는가.</summary>
    public enum CueVisit
    {
        /// <summary>아무 방문.</summary>
        AnyVisit = 0,

        /// <summary>그날 그 공간의 첫 방문.</summary>
        FirstVisitOfNight = 1,

        /// <summary>
        /// 기획서 「별도 방문」. <b>잠정 해석:</b> 그날 그 공간에서 일반 점검을 한 번 이상 마친 뒤 다시 들어온 방문.
        /// 근거와 미확정 사항은 README 참조. 기획 확인 대기.
        /// </summary>
        SeparateVisit = 2
    }

    /// <summary>같은 큐를 몇 번까지 발동하는가.</summary>
    public enum CueRepeat
    {
        /// <summary>한 방문에 한 번.</summary>
        OncePerVisit = 0,

        /// <summary>하룻밤에 한 번(기획서 「하루 1회」).</summary>
        OncePerNight = 1
    }

    /// <summary>전달 조건.</summary>
    public enum CueDelivery
    {
        /// <summary>발동 시점에 바로 전달된 것으로 본다.</summary>
        Immediate = 0,

        /// <summary>청취 구역 안에 있어야 전달이다. 구역을 벗어나면 취소한다(공통 명세 2절 「청각 단서 전달」).</summary>
        InListenZone = 1,

        /// <summary>식별 대상 중 <b>하나</b>를 0.2초 연속 응시하면 전달.</summary>
        GazeIdentifyAny = 2,

        /// <summary>식별 대상을 <b>모두</b> 각각 0.2초 연속 응시하면 전달(T6).</summary>
        GazeIdentifyAll = 3
    }
}

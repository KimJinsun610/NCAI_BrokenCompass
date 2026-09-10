#if UNITY_EDITOR || NIGHTDUTY_DEBUG
using UnityEngine;

namespace NightDuty
{
    /// <summary>
    /// 네 개의 공포 축(청각/조도/배치/신뢰)을 인스펙터 슬라이더로 직접 밀어 넣는 임시 축 공급원입니다.
    /// </summary>
    /// <remarks>
    /// 이 컴포넌트가 판정 시스템(<c>FearAxisSystem</c>)보다 먼저 완성된 것은 의도된 것입니다. 삭제하지 마십시오.
    /// 클라이언트 쪽 약 40개의 공간 연출 상태는 전부 <c>EventBus.BandChanged</c> 하나로 구동되는데,
    /// 그 이벤트를 낼 수 있는 것이 완성된 판정 시스템뿐이라면 연출 담당은 조명 하나를 확인하려고
    /// 실제로 근무수칙을 어겨야 하고, 판정 시스템이 끝나기 전에는 아예 작업을 시작할 수 없습니다.
    /// 이 클래스는 실제 <c>FearAxisSystem</c>과 똑같이 <see cref="IFearAxisReader"/>를 구현하므로,
    /// 이것을 상대로 작성한 클라이언트 코드는 나중에 그대로 둔 채 공급원만 교체하면 됩니다.
    ///
    /// 빌드에는 절대 포함되지 않습니다. 플레이어 빌드는 UNITY_EDITOR도 NIGHTDUTY_DEBUG도 정의하지 않으므로
    /// 이 타입 자체가 존재하지 않습니다. 따라서 클라이언트 코드는 이 타입을 직접 하드 참조해서는 안 되며,
    /// 반드시 <see cref="IFearAxisReader"/> 인터페이스를 통해서만 사용해야 합니다.
    /// </remarks>
    [AddComponentMenu("NightDuty/Debug/Debug Axis Driver")]
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public sealed class DebugAxisDriver : MonoBehaviour, IFearAxisReader
    {
        [Header("공포 축 값 (0-100)")]

        [Tooltip("청각(Auditory). 문소리·발소리·정체불명의 소음 시퀀스가 얼마나 자주, 얼마나 가까이서 재생되는지를 결정합니다.")]
        [Range(Bands.Min, Bands.Max)] [SerializeField] private int _auditory;

        [Tooltip("조도(Illuminance). 공간의 형광등 점등 개수와 색온도를 결정합니다. 값이 오를수록 6500K 백색에서 2000K 붉은색 쪽으로 내려가며, 75 이상이면 규칙상 '붉게 보이는' 상태로 취급합니다.")]
        [Range(Bands.Min, Bands.Max)] [SerializeField] private int _illuminance;

        [Tooltip("배치(Layout). 소품이 원래 자리에서 얼마나 어긋나 있는지 — 열린 문, 꺼내진 물건, 옮겨진 책상 등 — 를 결정합니다.")]
        [Range(Bands.Min, Bands.Max)] [SerializeField] private int _layout;

        [Tooltip("신뢰(Trust). 월드에는 절대 그려지지 않습니다. 지침록(문서 UI)의 문구가 어긋나거나 사라지는 정도만 바꿉니다.")]
        [Range(Bands.Min, Bands.Max)] [SerializeField] private int _trust;

        [Header("방송 설정")]

        [Tooltip("이벤트를 내보낼 공간 목록입니다. 교실은 순찰 지점이 둘(1-1, 1-3)이라 실제 공간 넷에 SpaceId 값은 다섯 개입니다. SpaceId.None은 넣지 마십시오.")]
        [SerializeField] private SpaceId[] _broadcastTo;

        [Tooltip("밴드 전환뿐 아니라 밴드 내부 진행도(BandProgress)도 함께 방송할지 여부입니다. 색온도 보간처럼 연속적인 연출에 필요합니다.")]
        [SerializeField] private bool _emitProgress = true;

        [Tooltip("OnEnable 시 한 줄짜리 로그를 남깁니다. 매 프레임 로그는 남기지 않습니다.")]
        [SerializeField] private bool _verbose;

        private readonly Band[] _lastBands = new Band[4];
        private readonly int[] _lastValues = new int[4] { -1, -1, -1, -1 };
        private bool _dirty;

        /// <summary>기본 방송 대상을 실제 공간 다섯 개(순찰 지점 기준)로 채웁니다.</summary>
        private void Reset()
        {
            _broadcastTo = new SpaceId[]
            {
                SpaceId.Corridor,
                SpaceId.Toilet,
                SpaceId.Classroom_1_1,
                SpaceId.Classroom_1_3,
                SpaceId.ScienceRoom
            };
            _emitProgress = true;
            _verbose = false;
        }

        private void OnEnable()
        {
            if (_verbose)
            {
                Debug.Log("[DebugAxisDriver] 활성화 — 임시 축 공급원이 이벤트를 방송합니다. 빌드에는 포함되지 않습니다.", this);
            }
            PushAll();
        }

        private void OnValidate()
        {
            // OnValidate 중에는 호출이 금지된 Unity API가 많으므로 여기서는 플래그만 세우고
            // 실제 이벤트 방송은 Update로 미룹니다. 에디트 모드에서는 Update가 곧바로
            // 돌지 않을 수 있어 delayCall로도 한 번 밀어 줍니다.
            _dirty = true;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall += () =>
            {
                // delayCall이 실행될 때쯤 오브젝트가 파괴되었을 수 있습니다.
                if (this == null) return;
                if (!isActiveAndEnabled) return;
                Flush();
            };
#endif
        }

        private void Update()
        {
            if (_dirty) Flush();
        }

        /// <summary>더티 플래그가 서 있으면 변경된 축만 골라 방송합니다.</summary>
        private void Flush()
        {
            _dirty = false;
            for (int i = 0; i < 4; i++)
            {
                FearAxis axis = (FearAxis)i;
                int value = GetValue(axis);
                if (value == _lastValues[i]) continue;
                Broadcast(axis, value, _lastBands[i]);
            }
        }

        /// <summary>현재 네 축의 상태를 조건 없이 한 번 다시 방송합니다. 막 깨어난 연출 담당이 기준값을 받도록 합니다.</summary>
        [ContextMenu("전체 다시 방송")]
        public void PushAll()
        {
            _dirty = false;
            for (int i = 0; i < 4; i++)
            {
                FearAxis axis = (FearAxis)i;
                Broadcast(axis, GetValue(axis), _lastBands[i], true);
            }
        }

        /// <summary>네 축을 모두 0으로 되돌리고 방송합니다.</summary>
        [ContextMenu("전 축 0으로")]
        public void ResetAllAxes()
        {
            _auditory = 0;
            _illuminance = 0;
            _layout = 0;
            _trust = 0;
            PushAll();
        }

        /// <summary>축 값을 0..100으로 클램프해 적용하고 슬라이더를 끈 것과 동일하게 이벤트를 방송합니다. 테스트 씬용입니다.</summary>
        public void SetAxis(FearAxis axis, int value)
        {
            value = Mathf.Clamp(value, Bands.Min, Bands.Max);
            switch (axis)
            {
                case FearAxis.Auditory: _auditory = value; break;
                case FearAxis.Illuminance: _illuminance = value; break;
                case FearAxis.Layout: _layout = value; break;
                case FearAxis.Trust: _trust = value; break;
                default: return;
            }
            int index = (int)axis;
            Broadcast(axis, value, _lastBands[index]);
        }

        /// <summary>한 축의 값 변화를 캐시에 반영하고 방송 대상 공간마다 이벤트를 냅니다. force가 참이면 밴드가 같아도 BandChanged를 냅니다(기준값 재방송용).</summary>
        private void Broadcast(FearAxis axis, int value, Band from, bool force = false)
        {
            int index = (int)axis;
            Band to = Bands.Of(value);
            _lastValues[index] = value;
            _lastBands[index] = to;

            if (_broadcastTo == null) return;

            float t01 = Bands.Progress(value, to);
            for (int s = 0; s < _broadcastTo.Length; s++)
            {
                SpaceId space = _broadcastTo[s];
                if (space == SpaceId.None) continue;
                if (force || from != to) EventBus.RaiseBandChanged(space, axis, from, to);
                if (_emitProgress) EventBus.RaiseBandProgress(space, axis, t01);
            }
        }

        /// <summary>축의 현재 원시값(0-100)입니다. 범위를 벗어난 열거값은 0을 돌려줍니다.</summary>
        public int GetValue(FearAxis axis)
        {
            switch (axis)
            {
                case FearAxis.Auditory: return _auditory;
                case FearAxis.Illuminance: return _illuminance;
                case FearAxis.Layout: return _layout;
                case FearAxis.Trust: return _trust;
                default: return 0;
            }
        }

        /// <summary>축의 현재 밴드입니다. 범위를 벗어난 열거값은 Band0을 돌려줍니다.</summary>
        public Band GetBand(FearAxis axis)
        {
            return Bands.Of(GetValue(axis));
        }
    }
}
#endif

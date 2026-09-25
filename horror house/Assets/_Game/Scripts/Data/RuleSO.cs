using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace NightDuty
{
    /// <summary>
    /// 근무수칙 카드 한 장(H1~H6 · C1~C6 · S1~S6 · T1~T6).
    /// 필드 구성은 확정 기획서 공통 명세 5절의 「수칙 데이터」를 따른다.
    /// <para>
    /// <b>판정 상세의 정본은 기획서 HTML이다.</b> 이 에셋은 그 내용을 조건 조합으로 옮긴 것이며,
    /// 카드 설명 문구(개발 판정 상세)를 여기에 복사하지 않는다. 태블릿에는 <see cref="PlayerText"/>만 노출한다.
    /// </para>
    /// <para>
    /// 조건은 <c>[SerializeReference]</c>라서 종류를 인스펙터 드롭다운으로 고른다.
    /// 조건 인스턴스는 공유 데이터이므로 실행 중 값은 <see cref="RuleWatcher"/>가 따로 가진다.
    /// </para>
    /// </summary>
    [CreateAssetMenu(menuName = "NightDuty/Rule Card", fileName = "Rule_")]
    public sealed class RuleSO : ScriptableObject
    {
        /// <summary>카드 ID 형식: 공간 글자(H·C·S·T) + 1~6.</summary>
        public static readonly Regex CardIdPattern = new Regex("^[HCST][1-6]$");

        private static readonly string[] NoTargets = new string[0];

        [Header("식별")]
        [SerializeField, Tooltip("카드 ID (예: H1)")]
        private string _cardId = string.Empty;

        [SerializeField, Tooltip("카드가 속한 공간")]
        private SpaceId _space;

        [SerializeField, TextArea(3, 8), Tooltip("태블릿에 노출하는 수칙 본문. 기획서 문구를 그대로 옮긴다")]
        private string _playerText = string.Empty;

        [SerializeField, TextArea(1, 4), Tooltip("조작 안내. 수칙 본문과 구분해 표시한다(기획서 §3-3). 대부분 비어 있다 — S1만 있다")]
        private string _howTo = string.Empty;

        [SerializeField, Tooltip("밤 전체 금지처럼 방문을 넘어 감시하는 장기 의무인지. 단기 사건은 한 방문에 하나만 시작한다")]
        private bool _isLongTerm;

        [Header("발생 자격·시작")]
        [SerializeField, Tooltip("구간 자격을 검사할지. 끄면 구간과 무관하게 시작한다")]
        private bool _useEligibleBand;

        [SerializeField]
        private FearAxis _eligibleAxis;

        [SerializeField, Tooltip("자격 구간 하한(포함)")]
        private Band _eligibleFrom = Band.Band0;

        [SerializeField, Tooltip("자격 구간 상한(포함)")]
        private Band _eligibleTo = Band.Band4;

        [SerializeField, Tooltip("사건을 시작하는 신호 종류")]
        private SignalKind _triggerKind;

        [SerializeField, Tooltip("시작 신호의 대상 ID(단서·문·구역). 공간 신호면 비운다")]
        private string _triggerId = string.Empty;

        [SerializeField, Tooltip("시작 신호의 공간(공간 진입 등). None이면 카드 공간을 쓴다")]
        private SpaceId _triggerSpace;

        [SerializeField, Tooltip("씬에 등록돼 있어야 하는 대상 ID. 조건에서 대상 ID를 비우면 이 목록을 쓴다")]
        private string[] _targetIds = new string[0];

        [Header("판정")]
        [SerializeReference, SubclassSelector, Tooltip("준수 조건")]
        private ICondition _successCondition;

        [SerializeReference, SubclassSelector, Tooltip("위반 조건. 같은 신호에서 준수와 동시에 성립하면 위반이 우선한다")]
        private ICondition _failureCondition;

        [SerializeReference, SubclassSelector, Tooltip("선택: 유효하지 않은 시도로 보고 대기로 되돌리는 조건(예: 유예 안에 입구로 복귀)")]
        private ICondition _cancelCondition;

        [SerializeField, Tooltip("준수 정산 시점")]
        private SettleAt _settleAt;

        [Header("델타 — 준수는 신뢰, 위반은 감각축")]
        [SerializeField, Tooltip("준수 시 신뢰 델타 (+4 / +5 / +8)")]
        private int _successDelta = 2;

        [SerializeField, Tooltip("위반 시 오르는 축 (청각·조도·배치)")]
        private FearAxis _failureAxis = FearAxis.Layout;

        [SerializeField, Tooltip("위반 델타 (+12~+25)")]
        private int _failureDelta = 12;

        [Header("역설 문자 — 이 카드와 정면으로 부딪히는 태블릿 문자 (S1은 없음)")]
        [SerializeField, Tooltip("역설 ID (예: P1). 비우면 이 카드에는 역설이 없다")]
        private string _paradoxId = string.Empty;

        [SerializeField, TextArea(2, 5), Tooltip("태블릿에 노출하는 역설 문자 전문. 따르면 이 카드의 위반 델타가 그대로 적용된다")]
        private string _paradoxText = string.Empty;

        [Header("설정값 — 조건이 0/음수로 두면 이 값을 쓴다")]
        [SerializeField, Tooltip("금지 반경(m). 기본 1.5, H6만 2")]
        private float _radius = 1.5f;

        [SerializeField, Tooltip("유예 시간(초). 손전등 2초, 응시 유예 2초 등")]
        private float _graceSeconds;

        [SerializeField, Tooltip("금지 응시 시간(초). H2·C3 3초, S4 2초")]
        private float _gazeSeconds;

        /// <summary>카드 ID.</summary>
        public string CardId { get { return _cardId; } }

        /// <summary>카드 공간.</summary>
        public SpaceId Space { get { return _space; } }

        /// <summary>태블릿 노출 본문.</summary>
        public string PlayerText { get { return _playerText; } }

        /// <summary>
        /// 조작 안내. <b>수칙 본문과 같은 문단에 섞지 않는다</b> — 기획서 §3-3이 구분 표시를 요구한다.
        /// 대부분의 카드는 비어 있다. 지금은 S1(「화면 중앙에 1초간」)만 채워져 있다.
        /// </summary>
        public string HowTo { get { return _howTo ?? string.Empty; } }

        /// <summary>장기 의무 여부.</summary>
        public bool IsLongTerm { get { return _isLongTerm; } }

        /// <summary>구간 자격 검사 여부.</summary>
        public bool UseEligibleBand { get { return _useEligibleBand; } }

        /// <summary>자격 검사 축.</summary>
        public FearAxis EligibleAxis { get { return _eligibleAxis; } }

        /// <summary>자격 구간 하한.</summary>
        public Band EligibleFrom { get { return _eligibleFrom; } }

        /// <summary>자격 구간 상한.</summary>
        public Band EligibleTo { get { return _eligibleTo; } }

        /// <summary>시작 신호 종류.</summary>
        public SignalKind TriggerKind { get { return _triggerKind; } }

        /// <summary>시작 신호 대상 ID.</summary>
        public string TriggerId { get { return _triggerId ?? string.Empty; } }

        /// <summary>시작 신호 공간(None이면 카드 공간).</summary>
        public SpaceId TriggerSpace { get { return _triggerSpace != SpaceId.None ? _triggerSpace : _space; } }

        /// <summary>대상 ID 목록.</summary>
        public IReadOnlyList<string> TargetIds { get { return _targetIds ?? NoTargets; } }

        /// <summary>준수 조건.</summary>
        public ICondition SuccessCondition { get { return _successCondition; } }

        /// <summary>위반 조건.</summary>
        public ICondition FailureCondition { get { return _failureCondition; } }

        /// <summary>취소 조건(없을 수 있음).</summary>
        public ICondition CancelCondition { get { return _cancelCondition; } }

        /// <summary>준수 정산 시점.</summary>
        public SettleAt SettleAt { get { return _settleAt; } }

        /// <summary>준수 신뢰 델타.</summary>
        public int SuccessDelta { get { return _successDelta; } }

        /// <summary>위반 축.</summary>
        public FearAxis FailureAxis { get { return _failureAxis; } }

        /// <summary>위반 델타.</summary>
        public int FailureDelta { get { return _failureDelta; } }

        /// <summary>역설 ID(P1~P23). 비어 있으면 이 카드에는 역설이 없다(S1).</summary>
        public string ParadoxId { get { return _paradoxId ?? string.Empty; } }

        /// <summary>역설 문자 전문. 따르면 이 카드의 위반 델타가 적용되고, 거절하면 이 카드의 준수 델타(신뢰)가 오른다.</summary>
        public string ParadoxText { get { return _paradoxText ?? string.Empty; } }

        /// <summary>이 카드에 역설 문자가 붙어 있는지.</summary>
        public bool HasParadox { get { return !string.IsNullOrEmpty(_paradoxId) && !string.IsNullOrEmpty(_paradoxText); } }

        /// <summary>기본 금지 반경(m).</summary>
        public float Radius { get { return _radius; } }

        /// <summary>기본 유예(초).</summary>
        public float GraceSeconds { get { return _graceSeconds; } }

        /// <summary>기본 금지 응시 시간(초).</summary>
        public float GazeSeconds { get { return _gazeSeconds; } }

        /// <summary>
        /// 현재 축 값이 이 카드의 발동 자격을 만족하는지.
        /// </summary>
        public bool IsEligible(IFearAxisReader axes)
        {
            if (!_useEligibleBand)
            {
                return true;
            }

            if (axes == null)
            {
                return false;
            }

            Band band = axes.GetBand(_eligibleAxis);
            return band >= _eligibleFrom && band <= _eligibleTo;
        }

        /// <summary>
        /// 이 카드가 씬에 요구하는 대상 ID를 모은다(시작 전 참조 검사용).
        /// 공간 신호처럼 대상이 없는 시작 신호는 넣지 않는다.
        /// </summary>
        public void CollectReferences(List<string> ids)
        {
            if (SignalCondition.UsesTarget(_triggerKind) && !string.IsNullOrEmpty(_triggerId))
            {
                ids.Add(_triggerId);
            }

            if (_targetIds != null)
            {
                for (int i = 0; i < _targetIds.Length; i++)
                {
                    if (!string.IsNullOrEmpty(_targetIds[i]))
                    {
                        ids.Add(_targetIds[i]);
                    }
                }
            }

            if (_successCondition != null)
            {
                _successCondition.CollectReferences(this, ids);
            }

            if (_failureCondition != null)
            {
                _failureCondition.CollectReferences(this, ids);
            }

            if (_cancelCondition != null)
            {
                _cancelCondition.CollectReferences(this, ids);
            }
        }

        /// <summary>
        /// 데이터 규칙 위반을 모은다. 비어 있으면 통과.
        /// 기획서 수치를 강제하지는 않고, 뒤집히면 설계가 무너지는 것만 검사한다.
        /// </summary>
        public void Validate(List<string> errors)
        {
            string id = string.IsNullOrEmpty(_cardId) ? name : _cardId;

            if (string.IsNullOrEmpty(_cardId) || !CardIdPattern.IsMatch(_cardId))
            {
                errors.Add(id + ": 카드 ID는 H·C·S·T + 1~6 형식이어야 합니다.");
            }

            if (_space == SpaceId.None)
            {
                errors.Add(id + ": 공간이 지정되지 않았습니다.");
            }

            if (string.IsNullOrEmpty(_playerText))
            {
                errors.Add(id + ": 태블릿 본문이 비어 있습니다.");
            }

            if (_triggerKind == SignalKind.None)
            {
                errors.Add(id + ": 시작 신호 종류가 없습니다.");
            }
            else if (!CanTrigger(_triggerKind))
            {
                errors.Add(id + ": " + _triggerKind + "는 대상·공간이 없어 사건을 시작할 수 없습니다.");
            }

            if (_successCondition == null)
            {
                errors.Add(id + ": 준수 조건이 없습니다.");
            }

            // 준수 전용 카드(S1 — 위반 없음, 미완료는 업무 미완료): 위반 조건 없음 + 위반 델타 0.
            bool complianceOnly = _failureCondition == null && _failureDelta == 0;

            if (_failureCondition == null && !complianceOnly)
            {
                errors.Add(id + ": 위반 조건이 없습니다(위반이 없는 카드는 위반 델타를 0으로 두십시오).");
            }

            if (_failureCondition != null && _failureAxis == FearAxis.Trust)
            {
                errors.Add(id + ": 위반 축은 신뢰일 수 없습니다(신뢰는 준수로만 오릅니다).");
            }

            if (_successDelta <= 0 || (!complianceOnly && _failureDelta <= 0))
            {
                errors.Add(id + ": 델타는 양수여야 합니다(축은 줄지 않습니다).");
            }

            if (_triggerKind == SignalKind.NightBegan)
            {
                if (!_isLongTerm)
                {
                    errors.Add(id + ": 밤 시작 신호로 시작하는 카드는 장기여야 합니다(방문 몫 규칙과 섞이지 않게).");
                }

                // 밤 시작 카드는 시작 기회가 밤 시작 한 번뿐이다. 자격 구간이나 취소 조건이 있으면 조용히 미판정이 된다.
                if (_useEligibleBand)
                {
                    errors.Add(id + ": 밤 시작 카드에는 자격 구간을 쓸 수 없습니다(시작 기회가 한 번뿐입니다).");
                }

                if (_cancelCondition != null)
                {
                    errors.Add(id + ": 밤 시작 카드에는 취소 조건을 쓸 수 없습니다(대기로 돌아가면 다시 시작하지 않습니다).");
                }
            }

            if (_useEligibleBand && _eligibleFrom > _eligibleTo)
            {
                errors.Add(id + ": 자격 구간 하한이 상한보다 큽니다.");
            }
        }

        /// <summary>사건 시작 신호로 쓸 수 있는 종류인지. 시간·손전등·Tab·밤 종료는 시작 신호가 아니다.</summary>
        public static bool CanTrigger(SignalKind kind)
        {
            switch (kind)
            {
                case SignalKind.None:
                case SignalKind.Tick:
                case SignalKind.FlashlightChanged:
                case SignalKind.TabChanged:
                case SignalKind.NightEndAccepted:
                case SignalKind.GazeSample:
                case SignalKind.ProximitySample:
                    return false;
                default:
                    return true;
            }
        }

        /// <summary>테스트·에디터 도구가 코드로 카드를 구성할 때 쓴다.</summary>
        internal void Configure(Config config)
        {
            _cardId = config.CardId;
            _space = config.Space;
            _playerText = config.PlayerText;
            _howTo = config.HowTo;
            _isLongTerm = config.IsLongTerm;
            _useEligibleBand = config.UseEligibleBand;
            _eligibleAxis = config.EligibleAxis;
            _eligibleFrom = config.EligibleFrom;
            _eligibleTo = config.EligibleTo;
            _triggerKind = config.TriggerKind;
            _triggerId = config.TriggerId;
            _triggerSpace = config.TriggerSpace;
            _targetIds = config.TargetIds ?? new string[0];
            _successCondition = config.Success;
            _failureCondition = config.Failure;
            _cancelCondition = config.Cancel;
            _settleAt = config.SettleAt;
            _successDelta = config.SuccessDelta;
            _failureAxis = config.FailureAxis;
            _failureDelta = config.FailureDelta;
            _paradoxId = config.ParadoxId;
            _paradoxText = config.ParadoxText;
            _radius = config.Radius;
            _graceSeconds = config.GraceSeconds;
            _gazeSeconds = config.GazeSeconds;
        }

        /// <summary><see cref="Configure"/>용 값 묶음.</summary>
        internal sealed class Config
        {
            public string CardId = string.Empty;
            public SpaceId Space;
            public string PlayerText = "테스트 본문";
            public string HowTo = string.Empty;
            public bool IsLongTerm;
            public bool UseEligibleBand;
            public FearAxis EligibleAxis;
            public Band EligibleFrom = Band.Band0;
            public Band EligibleTo = Band.Band4;
            public SignalKind TriggerKind;
            public string TriggerId = string.Empty;
            public SpaceId TriggerSpace;
            public string[] TargetIds;
            public ICondition Success;
            public ICondition Failure;
            public ICondition Cancel;
            public SettleAt SettleAt;
            public int SuccessDelta = 2;
            public FearAxis FailureAxis = FearAxis.Layout;
            public int FailureDelta = 12;
            public string ParadoxId = string.Empty;
            public string ParadoxText = string.Empty;
            public float Radius = 1.5f;
            public float GraceSeconds;
            public float GazeSeconds;
        }
    }
}

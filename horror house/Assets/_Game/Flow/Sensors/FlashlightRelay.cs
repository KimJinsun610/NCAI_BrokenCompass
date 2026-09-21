using NightDuty;
using UnityEngine;

/// <summary>
/// 손전등 발신기. On/Off 토글 입력을 받아 실제 라이트를 켜고 끄고 <see cref="SignalKind.FlashlightChanged"/>를 보낸다.
///
/// <para><b>밤 시작 직후 현재 상태를 1회 반드시 보낸다.</b> <c>JudgeWorld.FlashlightOn</c>은 매 밤 <c>false</c>로 시작한다.
/// 씬의 <c>FPController/Camera/Flashlight_ON_FirstPerson</c>은 지금 <b>켜진 채로 저장돼 있어서</b>,
/// 초기 1회를 보내지 않으면 플레이어 화면은 밝은데 코어는 꺼진 것으로 안다 → H3는 통과, C5는 실패가 된다.</para>
///
/// <para><b>유예 2초 타이머를 여기서 만들지 말 것.</b> <c>FlashlightCondition</c>이 카드의 <c>GraceSeconds</c>로 직접 센다.
/// 발신기는 「바뀌었다」만 보낸다.</para>
///
/// <para><b>입력 시스템.</b> 이 프로젝트는 Active Input Handling이 <i>Both</i>라 레거시와 New Input System이 둘 다 켜져 있다.
/// <c>FPController</c>(WASD·Tab·Esc)와 벤더 <c>DoorScript</c>(E)가 전부 <c>KeyCode</c> 레거시 입력이므로
/// 손전등만 New Input System으로 가면 플레이어 입력 경로가 둘로 갈라진다. 그래서 레거시로 맞추고,
/// <c>#if ENABLE_LEGACY_INPUT_MANAGER</c>로 감싸 레거시를 끈 설정에서도 컴파일되게 한다.
/// New Input System으로 옮길 때는 <see cref="Toggle"/>·<see cref="SetOn"/>을 액션에 연결하면 된다.</para>
///
/// <para><b>기본 키는 F.</b> 기획서 9절은 「F 또는 우클릭, 미정」이다. 우클릭은 조준·집기 같은 다른 상호작용에
/// 쓰일 여지가 남아 있고 <c>FPController</c>의 기존 키(WASD·Shift·Space·Tab·Esc)와도 겹치지 않아 F를 기본값으로 둔다.
/// <see cref="alsoRightMouse"/>를 켜면 우클릭도 함께 받는다. 기획이 확정되면 인스펙터에서 바꾸면 된다.</para>
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(50)]
public sealed class FlashlightRelay : MonoBehaviour
{
    [Header("라이트")]
    [Tooltip("켜고 끌 손전등 오브젝트(예: FPController/Camera/Flashlight_ON_FirstPerson). 비우면 자식 Light를 쓴다.")]
    [SerializeField] private GameObject flashlightRoot;

    [Tooltip("밤 시작 때의 손전등 상태. 기획 정본은 복도 Off·교실 On이므로 경비실 출발은 Off가 기본이다.")]
    [SerializeField] private bool startOn;

    [Header("입력")]
    [Tooltip("토글 키. 기획 미정값의 기본은 F.")]
    [SerializeField] private KeyCode toggleKey = KeyCode.F;

    [Tooltip("켜면 마우스 우클릭으로도 토글한다.")]
    [SerializeField] private bool alsoRightMouse;

    private Light[] _lights;
    private bool _isOn;
    private int _sentForDay = -1;

    /// <summary>지금 켜져 있는지.</summary>
    public bool IsOn
    {
        get { return _isOn; }
    }

    /// <summary>씬에 하나뿐인 발신기(있으면). 다른 코드가 상태를 읽을 때 쓴다.</summary>
    public static FlashlightRelay Active { get; private set; }

    private void OnEnable()
    {
        Active = this;
        CacheLights();
        ApplyToWorld(startOn);
        _isOn = startOn;
        _sentForDay = -1;   // 다음 밤에 초기 1회를 다시 보내게 한다.
    }

    private void OnDisable()
    {
        // EventBus는 구독하지 않지만, 정적 참조는 반드시 되돌린다(씬을 바꿔도 static은 살아남는다).
        if (Active == this)
        {
            Active = null;
        }
    }

    private void Update()
    {
        if (!NightRun.IsNightActive || NightRun.IsCaptured)
        {
            return;
        }

        // Tab 중에는 손전등 조작이 멈춘다(기획 정본 공통 명세 2절). 신호도 보내지 않는다.
        if (PlayerSensors.TabOpen)
        {
            return;
        }

        // 밤 시작 직후 현재 상태 1회. 밤마다 다시 보낸다.
        if (_sentForDay != NightRun.Day)
        {
            _sentForDay = NightRun.Day;
            NightRun.Send(JudgeSignal.Flashlight(_isOn));
        }

        if (ReadToggleInput())
        {
            Toggle();
        }
    }

    /// <summary>손전등을 뒤집는다(플레이어 조작과 같은 경로).</summary>
    public void Toggle()
    {
        SetOn(!_isOn);
    }

    /// <summary>
    /// 상태를 지정한다. 바뀌었을 때만 신호를 보낸다.
    /// 연출이 강제로 끄는 경우에도 이 메서드를 쓴다 — 손전등 신호에는 출처 구분이 없다(<c>JudgeSignal.Flashlight</c>는 Flag만 쓴다).
    /// </summary>
    public void SetOn(bool on)
    {
        if (_isOn == on)
        {
            return;
        }

        _isOn = on;
        ApplyToWorld(on);

        if (NightRun.IsNightActive && !NightRun.IsCaptured && !PlayerSensors.TabOpen)
        {
            NightRun.Send(JudgeSignal.Flashlight(on));
            _sentForDay = NightRun.Day;
            return;
        }

        // 보낼 수 없는 시점(Tab 중·밤 밖·포획 뒤)에 연출이 상태를 바꿨다.
        // 초기 1회를 다시 무장해 두면 보낼 수 있게 되는 첫 프레임에 현재 상태가 나간다.
        _sentForDay = -1;
    }

    private bool ReadToggleInput()
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(toggleKey))
        {
            return true;
        }

        return alsoRightMouse && Input.GetMouseButtonDown(1);
#else
        // 레거시 입력이 꺼진 설정. New Input System 액션이 Toggle()을 부르게 연결한다.
        return false;
#endif
    }

    private void CacheLights()
    {
        if (flashlightRoot != null)
        {
            _lights = flashlightRoot.GetComponentsInChildren<Light>(true);
            return;
        }

        _lights = GetComponentsInChildren<Light>(true);
    }

    private void ApplyToWorld(bool on)
    {
        if (flashlightRoot != null)
        {
            flashlightRoot.SetActive(on);
            return;
        }

        if (_lights == null)
        {
            return;
        }

        for (int i = 0; i < _lights.Length; i++)
        {
            if (_lights[i] != null)
            {
                _lights[i].enabled = on;
            }
        }
    }
}

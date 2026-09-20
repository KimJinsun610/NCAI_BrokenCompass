using UnityEngine;

/// <summary>
/// 1인칭 손 + 태블릿 리그.
/// 카메라 자식으로 붙어서, Tab을 누르면 화면 아래에서 올라오고 다시 누르면 내려간다.
///
/// 위치·각도는 모두 인스펙터 값이며 <b>카메라 기준 로컬 좌표</b>다.
/// 손 모양은 이 스크립트가 건드리지 않는다. 프리팹에 저장된 포즈가 그대로 쓰인다.
/// </summary>
[DisallowMultipleComponent]
public class PlayerTablet : MonoBehaviour
{
    [Header("입력")]
    [Tooltip("이 키로 태블릿을 들었다 내린다.")]
    public KeyCode toggleKey = KeyCode.Tab;
    [Tooltip("켜면 시작하자마자 들고 있는 상태로 시작한다.")]
    public bool startOpened = false;
    [Tooltip("끄면 키 입력을 직접 받지 않는다. 다른 스크립트에서 Toggle()을 부를 때 사용.")]
    public bool readInput = true;

    [Header("붙일 대상")]
    [Tooltip("비워 두면 실행할 때 MainCamera를 찾아서 그 자식으로 들어간다.")]
    public Transform attachTo;

    [Header("올린 자세 (카메라 기준)")]
    public Vector3 openedPosition = new Vector3(0f, -0.08f, 0.38f);
    public Vector3 openedRotation = new Vector3(12f, 0f, 0f);

    [Header("내린 자세 (카메라 기준)")]
    public Vector3 closedPosition = new Vector3(0f, -0.60f, 0.30f);
    public Vector3 closedRotation = new Vector3(65f, 0f, 0f);

    [Header("연출")]
    [Tooltip("올리고 내리는 데 걸리는 시간(초). 일시정지 중에도 흐르는 실제 시간이다.")]
    public float moveSeconds = 0.25f;
    [Tooltip("0에 가까울수록 올라오기 시작할 때, 1에 가까울수록 다 올라온 뒤 화면이 켜진다.")]
    [Range(0f, 1f)] public float screenOnAt = 0.55f;

    [Header("연결")]
    [Tooltip("태블릿 화면(월드 캔버스). 태블릿이 올라오는 도중에 켜진다.")]
    public GameObject screenRoot;

    /// <summary>태블릿이 올라와 있는가.</summary>
    public bool IsOpened { get { return _opened; } }

    /// <summary>연출(흔들림)을 얹기 전의 기준 위치. ViewmodelSway가 여기에 더한다.</summary>
    public Vector3 PosePosition { get; private set; }

    /// <summary>연출을 얹기 전의 기준 회전.</summary>
    public Quaternion PoseRotation { get; private set; }

    /// <summary>0 = 완전히 내린 상태, 1 = 완전히 올린 상태. 연출 세기를 조절할 때 쓴다.</summary>
    public float OpenAmount { get { return _t; } }

    private bool _opened;
    // 0 = 내린 자세, 1 = 올린 자세
    private float _t;
    private float _tVelocity;

    private void Start()
    {
        Transform target = attachTo != null ? attachTo : (Camera.main != null ? Camera.main.transform : null);
        if (target != null && transform.parent != target)
        {
            transform.SetParent(target, false);
        }

        _opened = startOpened;
        _t = _opened ? 1f : 0f;
        ApplyPose(_t);
        SetScreenActive(_t >= screenOnAt);
    }

    private void Update()
    {
        if (readInput && Input.GetKeyDown(toggleKey))
        {
            Toggle();
        }

        float target = _opened ? 1f : 0f;
        if (!Mathf.Approximately(_t, target) || Mathf.Abs(_tVelocity) > 0.0001f)
        {
            // 일정 속도로 움직이면 시작·끝이 딱 끊긴다. 스프링처럼 감속하며 붙게 한다.
            // 올라오는 도중에 다시 누르면 속도를 이어받아 부드럽게 방향을 튼다.
            // 태블릿을 여는 동안 시간이 멈출 수 있으므로 unscaled를 쓴다.
            float smoothTime = Mathf.Max(0.02f, moveSeconds * 0.45f);
            _t = Mathf.SmoothDamp(_t, target, ref _tVelocity, smoothTime, Mathf.Infinity, Time.unscaledDeltaTime);

            if (Mathf.Abs(_t - target) < 0.0015f)
            {
                _t = target;
                _tVelocity = 0f;
            }

            ApplyPose(_t);
            SetScreenActive(_t >= screenOnAt);
        }
    }

    public void Toggle()
    {
        if (_opened) Close();
        else Open();
    }

    public void Open()
    {
        _opened = true;
    }

    public void Close()
    {
        _opened = false;
    }

    /// <summary>연출 없이 즉시 그 자세로 맞춘다.</summary>
    public void SetOpenedImmediate(bool opened)
    {
        _opened = opened;
        _t = opened ? 1f : 0f;
        _tVelocity = 0f;
        ApplyPose(_t);
        SetScreenActive(_t >= screenOnAt);
    }

    /// <summary>
    /// 에디터 미리보기와 실행 중 모두 쓰는 자세 적용.
    /// 결과를 PosePosition·PoseRotation에도 남겨서, ViewmodelSway가 그 위에 흔들림을 얹을 수 있게 한다.
    /// </summary>
    public void ApplyPose(float t)
    {
        // 부드럽게 들어올리기 위해 가속·감속을 넣는다.
        float e = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
        PosePosition = Vector3.Lerp(closedPosition, openedPosition, e);
        PoseRotation = Quaternion.Slerp(Quaternion.Euler(closedRotation), Quaternion.Euler(openedRotation), e);

        transform.localPosition = PosePosition;
        transform.localRotation = PoseRotation;
    }

    private void SetScreenActive(bool active)
    {
        if (screenRoot != null && screenRoot.activeSelf != active)
        {
            screenRoot.SetActive(active);
        }
    }
}

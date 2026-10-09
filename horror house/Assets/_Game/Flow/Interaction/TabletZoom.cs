using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 태블릿 확대(2026-10-04 민 요청). <b>태블릿은 늘 손에 들려 있고(내린 상태 없음), Tab으로 「든 상태 ↔ 화면 정중앙에 꽉 차게 확대」를 오간다.</b>
/// <para><b>「읽는 중」 = 확대 상태</b>다. Tab 판정 신호·센서 정지(<see cref="TabletBridge"/>), CCTV·전화기 사용 금지, 알람 확인 같은 「태블릿을 펼쳤나」 판단은
/// 모두 <see cref="Reading"/>을 읽는다 — 든 상태만으로는 판정이 멈추지 않는다. CCTV를 들여다보는 동안은 모니터를 가리지 않게 잠깐 내린다.</para>
/// <para><b>61차 들기 제한</b>(플레이테스트 「플레이어가 태블릿을 너무 올리고 다닌다」 → 민: 「주요 연출이 나오면 강제로 내리고 끝날 때까지 못 올리고, 한 번 올리면 5초 뒤에 내려가고,
/// 2초 후에 다시 올릴 수 있도록」, 규칙은 코어 <see cref="NightDuty.TabletLimit"/>): 「올림」 = 확대. 푼 뒤 2초 동안 다시 못 한다(69차: 「5초 뒤 저절로 풀림」은 폐기 — 민).
/// 주요 연출(조우의 전조·몹을 세워 둔 동안·대면·마무리 — <c>TensionDirector.Busy</c>) 동안은 확대를 풀고 손의 태블릿까지 시야 밖으로 내린다 — 끝나면 다시 든다.</para>
/// <list type="bullet">
/// <item>김진선님 <see cref="PlayerTablet"/>은 고치지 않는다. 실행 중에만 그 인스턴스의 <c>readInput</c>을 끄고 같은 키를 여기서 받아
/// <c>Open</c>/<c>Close</c>를 부르며, 확대는 <c>openedPosition</c>·<c>openedRotation</c>(올린 자세)을 확대 자세 쪽으로 옮긴 뒤 <c>ApplyPose</c>로 기준 자세를 다시 잡는다.
/// 흔들림(<see cref="ViewmodelSway"/>)은 그 기준 자세 위에 얹히므로 그대로 따라온다.</item>
/// <item>확대 자세는 화면 판(<c>Screen/BG</c>)의 크기·자리를 재서 계산한다 — 화면이 카메라 정면을 보고, 그 중심이 시선 한가운데, 세로(또는 가로)가
/// 뷰모델 카메라 화면의 <see cref="fill"/>만큼을 채우는 거리. 판을 못 찾으면 확대하지 않고 바로 내린다(옛 동작).</item>
/// <item>확대 중에는 호흡·시선 지연·걸음 흔들림을 끈다(가까이서 흔들리면 글이 읽히지 않는다). 내리면 되돌린다.</item>
/// <item>판정과 무관하다. Tab 신호(<see cref="TabletBridge"/>)는 들었는지만 보므로 확대 여부는 신호를 바꾸지 않는다.</item>
/// </list>
/// 근무 씬에 자동으로 선다.
/// </summary>
[DisallowMultipleComponent]
public sealed class TabletZoom : MonoBehaviour
{
    [Tooltip("확대했을 때 화면 판이 차지하는 비율(세로 또는 가로 중 먼저 닿는 쪽).")]
    [SerializeField, Range(0.5f, 1f)] private float fill = 0.9f;

    [Tooltip("켜면 태블릿을 늘 들고 있고 Tab은 확대만 오간다(민 2026-10-04). 끄면 옛 동작: 들기 → 확대 → 내리기.")]
    [SerializeField] private bool alwaysHeld = true;

    [Tooltip("늘 드는 방식일 때 든 자세(카메라 기준). 김진선님 올린 자세(-0.16, -0.05, 0.30)보다 왼쪽 아래로 조금 멀리 — 글은 읽히되 왼쪽 시야를 덜 가리게(민 2026-10-04).")]
    [SerializeField] private Vector3 heldPosition = new Vector3(-0.218f, -0.104f, 0.36f);

    [SerializeField] private Vector3 heldRotation = new Vector3(8f, -9f, -4f);

    [Tooltip("확대·축소에 걸리는 시간(초, 일시정지 중에는 멈춤).")]
    [SerializeField, Min(0.02f)] private float zoomSeconds = 0.22f;

    private PlayerTablet _tablet;
    private ViewmodelSway _sway;
    private bool _zoomTarget;
    private float _blend;
    private float _blendVelocity;
    private Vector3 _basePos;
    private Vector3 _restorePos;
    private Vector3 _restoreRot;
    private Quaternion _baseRot;
    private bool _haveZoomPose;
    private Vector3 _zoomPos;
    private Quaternion _zoomRot;
    private bool _swayOff;
    private bool _savedBreathing;
    private bool _savedLook;
    private bool _savedWalk;
    private readonly NightDuty.TabletLimit _limit = new NightDuty.TabletLimit();
    private NightDuty.InspectionPlan _plan;

    /// <summary>61차 들기 제한 규칙 상태(검수용).</summary>
    public NightDuty.TabletLimit Limit
    {
        get { return _limit; }
    }

    /// <summary>61차: 주요 연출로 태블릿을 시야 밖으로 내려 둔 중인지(검수용).</summary>
    public bool LoweredForDirection { get; private set; }

    /// <summary>지금 근무 씬의 것. 없으면 null.</summary>
    public static TabletZoom Active { get; private set; }

    /// <summary>
    /// 그 태블릿을 「읽는 중」인지 — 늘 드는 방식이면 확대 상태, 아니면(또는 이 컴포넌트가 없으면) 들고 있는지.
    /// Tab 신호·CCTV·전화기·붙잡힘 소리가 이것을 본다.
    /// </summary>
    public static bool Reading(PlayerTablet tablet)
    {
        if (tablet == null) return false;
        TabletZoom z = Active;
        if (z != null && z.alwaysHeld && ReferenceEquals(z._tablet, tablet)) return z._zoomTarget;
        return tablet.IsOpened;
    }

    /// <summary>확대 중인지(넘어가는 중이면 절반을 넘었을 때).</summary>
    public bool IsZoomed
    {
        get { return _blend > 0.5f; }
    }

    // ── 자동 설치 ─────────────────────────────────────────────

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Install()
    {
        Active = null;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InstallForFirstScene()
    {
        EnsureFor(SceneManager.GetActiveScene());
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureFor(scene);
    }

    private static void EnsureFor(Scene scene)
    {
        if (!FlowAutoInstall.IsDutyScene(scene) || FlowAutoInstall.Exists<TabletZoom>(scene)) return;
        FlowAutoInstall.CreateHost<TabletZoom>(scene, "TabletZoom (auto)");
    }

    private void OnEnable()
    {
        Active = this;
    }

    private void OnDisable()
    {
        Restore();
        if (Active == this) Active = null;
    }

    // ── 매 프레임 ─────────────────────────────────────────────

    private void Update()
    {
        if (!Bind()) return;
        if (ViewmodelTime.Paused) return;   // 일시정지 중에는 키도 받지 않고 움직이지도 않는다(PlayerTablet과 같다)

        bool cctv = CctvSystem.Active != null && CctvSystem.Active.IsViewing;
        bool live = NightDuty.NightRun.IsNightActive && !NightDuty.NightRun.IsCaptured;
        NightDuty.InspectionPlan plan = NightDuty.NightRun.Inspections != null ? NightDuty.NightRun.Inspections.Plan : null;
        if (!ReferenceEquals(plan, _plan))
        {
            _plan = plan;
            _limit.Reset();   // 새 밤
        }

        // 61차: 주요 연출 중 — 확대를 풀고 손의 태블릿도 내린다(끝날 때까지 못 든다).
        bool busy = live && NightDuty.NightRun.Tension != null && NightDuty.NightRun.Tension.Busy;
        LoweredForDirection = busy;
        if (alwaysHeld)
        {
            // 늘 든다 — CCTV를 들여다보는 동안·주요 연출 중에만 내린다.
            if (cctv || busy)
            {
                _zoomTarget = false;
                if (_tablet.IsOpened) _tablet.Close();
            }
            else if (!_tablet.IsOpened)
            {
                _tablet.Open();
            }
        }

        if (!cctv && Input.GetKeyDown(_tablet.toggleKey)) Press();
        if (!_tablet.IsOpened) _zoomTarget = false;   // 다른 쪽(알람 등)이 내렸다

        // 61차: 푼 뒤 2초는 다시 못 한다(일시정지·CCTV 중에는 흐르지 않는다). 69차: 「5초 뒤 풀림」은 폐기 — 연출이 나올 때만 풀린다.
        if (live && !cctv && _limit.Tick(Time.deltaTime, _zoomTarget, busy))
        {
            if (_zoomTarget && DirectionStage.Verbose) Debug.Log("[TabletZoom] 확대를 풀었다 — " + (busy ? "연출 중" : "쉬는 중"));
            _zoomTarget = false;
        }

        float target = _zoomTarget ? 1f : 0f;
        if (Mathf.Approximately(_blend, target) && Mathf.Abs(_blendVelocity) < 0.0001f) return;
        _blend = Mathf.SmoothDamp(_blend, target, ref _blendVelocity, Mathf.Max(0.02f, zoomSeconds * 0.45f), Mathf.Infinity, ViewmodelTime.Delta);
        if (Mathf.Abs(_blend - target) < 0.002f)
        {
            _blend = target;
            _blendVelocity = 0f;
        }

        ApplyBlend();
        SetSway(_blend > 0.01f);
    }

    /// <summary>Tab 한 번 — 늘 드는 방식이면 확대 ↔ 든 상태. 아니면 내려 있으면 들고, 들고 있으면 확대하고, 확대돼 있으면 내린다.</summary>
    public void Press()
    {
        if (!Bind()) return;
        if (alwaysHeld)
        {
            if (_limit.Locked && !_zoomTarget) return;   // 61차: 연출 중·쉬는 중에는 확대하지 않는다
            if (!_tablet.IsOpened) _tablet.Open();
            _zoomTarget = !_zoomTarget && EnsureZoomPose();
            return;
        }

        if (!_tablet.IsOpened)
        {
            _zoomTarget = false;
            _tablet.Open();
            return;
        }

        if (!_zoomTarget && EnsureZoomPose())
        {
            _zoomTarget = true;
            return;
        }

        _zoomTarget = false;
        _tablet.Close();
    }

    private bool Bind()
    {
        if (_tablet != null) return true;
        _tablet = FindAnyObjectByType<PlayerTablet>();
        if (_tablet == null) return false;
        _sway = _tablet.GetComponent<ViewmodelSway>();
        _tablet.readInput = false;   // 키는 여기서 받는다(실행 중 인스턴스 값만 — 프리팹은 그대로)
        _basePos = alwaysHeld ? heldPosition : _tablet.openedPosition;
        _baseRot = Quaternion.Euler(alwaysHeld ? heldRotation : _tablet.openedRotation);
        _restorePos = _tablet.openedPosition;
        _restoreRot = _tablet.openedRotation;
        if (alwaysHeld)
        {
            _tablet.openedPosition = _basePos;   // 든 자세를 이 값으로(실행 중 인스턴스 값만)
            _tablet.openedRotation = heldRotation;
            _tablet.Open();   // 기본(내린) 상태는 없다
        }
        return true;
    }

    private void ApplyBlend()
    {
        float e = Mathf.SmoothStep(0f, 1f, _blend);
        _tablet.openedPosition = Vector3.Lerp(_basePos, _haveZoomPose ? _zoomPos : _basePos, e);
        _tablet.openedRotation = Quaternion.Slerp(_baseRot, _haveZoomPose ? _zoomRot : _baseRot, e).eulerAngles;
        if (_tablet.OpenAmount > 0f) _tablet.ApplyPose(_tablet.OpenAmount);
    }

    private void SetSway(bool off)
    {
        if (_sway == null || off == _swayOff) return;
        _swayOff = off;
        if (off)
        {
            _savedBreathing = _sway.breathing;
            _savedLook = _sway.lookSway;
            _savedWalk = _sway.walkBob;
            _sway.breathing = false;
            _sway.lookSway = false;
            _sway.walkBob = false;
        }
        else
        {
            _sway.breathing = _savedBreathing;
            _sway.lookSway = _savedLook;
            _sway.walkBob = _savedWalk;
        }
    }

    private void Restore()
    {
        if (_tablet == null) return;
        SetSway(false);
        _tablet.openedPosition = _restorePos;
        _tablet.openedRotation = _restoreRot;
        _tablet.readInput = true;
        _blend = 0f;
        _zoomTarget = false;
    }

    // ── 확대 자세 ─────────────────────────────────────────────

    /// <summary>
    /// 화면 판을 재서 확대 자세를 구한다(처음 한 번). 판의 앞면이 카메라를 보고(판 법선 = 카메라 뒤쪽), 판의 위 = 카메라 위,
    /// 판 중심이 시선 축 위에서 세로·가로가 화면의 <see cref="fill"/>에 맞는 거리.
    /// </summary>
    private bool EnsureZoomPose()
    {
        if (_haveZoomPose) return true;
        Transform plate = FindPlate();
        Camera cam = ViewmodelCamera();
        Transform t = _tablet.transform;
        if (plate == null || cam == null || t.parent == null) return false;

        MeshFilter mf = plate.GetComponent<MeshFilter>();
        Bounds b = mf.sharedMesh.bounds;

        // 판의 면(가장 얇은 축이 법선)과 위 방향을 태블릿 기준으로.
        Vector3 ext = b.extents;
        Vector3 nLocal = ext.z <= ext.x && ext.z <= ext.y ? Vector3.forward : (ext.y <= ext.x ? Vector3.up : Vector3.right);
        Vector3[] normals = mf.sharedMesh.normals;
        if (normals != null && normals.Length > 0) nLocal = normals[0];
        Vector3 faceInTablet = t.InverseTransformDirection(plate.TransformDirection(nLocal)).normalized;   // 판 앞면이 향하는 쪽
        Vector3 upInTablet = t.InverseTransformDirection(plate.up).normalized;

        // 태블릿 회전 R(카메라 기준): R·face = −z(카메라 쪽), R·up = +y.
        Quaternion faceToCamera = Quaternion.LookRotation(-faceInTablet, upInTablet);   // 태블릿 공간에서 「판 뒤쪽 = +z, 판 위 = +y」인 틀
        Quaternion r = Quaternion.Inverse(faceToCamera);

        // 판 크기(카메라 공간, 태블릿 크기 반영)와 중심.
        Vector3 scaleT = t.localScale;
        Vector3 center = Vector3.Scale(scaleT, t.InverseTransformPoint(plate.TransformPoint(b.center)));
        float h = (Vector3.Scale(scaleT, t.InverseTransformVector(plate.TransformVector(new Vector3(0f, b.size.y, 0f))))).magnitude;
        float w = (Vector3.Scale(scaleT, t.InverseTransformVector(plate.TransformVector(new Vector3(b.size.x, 0f, 0f))))).magnitude;
        if (h <= 0f || w <= 0f) return false;

        float halfV = cam.fieldOfView * 0.5f * Mathf.Deg2Rad;
        float halfH = Mathf.Atan(Mathf.Tan(halfV) * cam.aspect);
        float d = Mathf.Max((h * 0.5f) / Mathf.Tan(halfV), (w * 0.5f) / Mathf.Tan(halfH)) / fill;
        d = Mathf.Max(d, cam.nearClipPlane * 3f);

        _zoomRot = r;
        _zoomPos = new Vector3(0f, 0f, d) - r * center;
        _haveZoomPose = true;
        if (DirectionStage.Verbose) Debug.Log("[TabletZoom] 판 " + w.ToString("0.000") + "×" + h.ToString("0.000") + "m, 거리 " + d.ToString("0.000") + "m, 자세 " + _zoomPos.ToString("F3") + " " + r.eulerAngles.ToString("F1"));
        return true;
    }

    /// <summary>화면 판 — 태블릿 화면 아래의 <c>BG</c>, 없으면 화면 아래에서 가장 넓은 메시.</summary>
    private Transform FindPlate()
    {
        Transform screen = _tablet.screenRoot != null ? _tablet.screenRoot.transform : _tablet.transform;
        Transform bg = screen.Find("BG");
        if (bg != null && bg.GetComponent<MeshFilter>() != null && bg.GetComponent<MeshFilter>().sharedMesh != null) return bg;

        Transform best = null;
        float bestArea = 0f;
        foreach (MeshFilter mf in screen.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh == null || mf.GetComponent<TMPro.TMP_Text>() != null) continue;
            Vector3 s = Vector3.Scale(mf.sharedMesh.bounds.size, mf.transform.lossyScale);
            float area = Mathf.Max(s.x * s.y, Mathf.Max(s.x * s.z, s.y * s.z));
            if (area > bestArea)
            {
                bestArea = area;
                best = mf.transform;
            }
        }

        return best;
    }

    /// <summary>태블릿을 그리는 카메라(뷰모델 카메라). 없으면 주 카메라.</summary>
    private Camera ViewmodelCamera()
    {
        int bit = 1 << _tablet.gameObject.layer;
        foreach (Camera c in Camera.allCameras)
        {
            if ((c.cullingMask & bit) != 0) return c;
        }

        return Camera.main;
    }
}

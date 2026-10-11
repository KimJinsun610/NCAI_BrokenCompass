using System.Collections.Generic;
using NightDuty;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 창밖 노란 얼굴(84차 빌드업 · 85차 흐름 — 민: 「도서관 입구 맞은편 블라인드 점검 지시 → 창문 조우(플레이어 쪽을 바라보다가, 플레이어가 응시를 2초 이상 하면 도망감)
/// → 다음 날 biology 책 습득 or 다른 도서관 순찰 → 도서관 문 조우」).
/// <list type="bullet">
/// <item>그 밤 편성에 <see cref="ProgramCatalog.SuitMan"/>(창밖 노란 얼굴 — 노란 얼굴 전날, 날짜 사슬)이 있으면, 조우가 대역을 세울 때(<see cref="Show"/> —
/// 블라인드 L-2 단독 지시 뒤 도서관 6초) <b>그 블라인드 창</b>(도서관 입구 맞은편 서쪽 창 둘 중 플레이어에게 가까운 쪽) 밖에 노란 얼굴(<c>mob.windowman</c>)이 서서
/// 블라인드 아래 유리 칸으로 들여다본다. 밤 내내 서 있지는 않는다. 블라인드 창을 못 찾으면 옛 북쪽 창 자리(<c>stage.window.man</c>).</item>
/// <item>서 있는 동안 플레이어 쪽으로 몸을 천천히 돌려 바라본다(창 정면에서 ±<see cref="MaxTurnDegrees"/>°까지).</item>
/// <item>플레이어가 <see cref="FinalCues.WindowFaceTarget"/>을 2초 넘게 이어서 보면(<see cref="NightRun.WindowFaceFled"/>) 창 정면으로 돌아서 창 앞을 가로질러 달아난다
/// (<see cref="StandInExit.Leave"/> — 기괴하게 빠른 걸음, 화면 밖이 되면 사라짐). 그 밤에는 다시 서지 않는다(재시작이면 다시).</item>
/// <item>보지 않으면 조우가 끝나도 그 밤 내내 창밖에서 바라보고 있다. 다음 날(문간 노란 얼굴 밤)에는 창밖에 서지 않는다.</item>
/// <item>수칙과 무관(85차: L3는 문간 노란 얼굴만). 사망 컷신이 도는 동안은 숨는다.</item>
/// </list>
/// 근무 씬에 자동으로 선다. 씬은 고치지 않는다.
/// </summary>
[DisallowMultipleComponent]
public sealed class WindowFace : MonoBehaviour
{
    /// <summary>대역 ID(문간 노란 얼굴과 같은 business duck — 창밖용 프리팹).</summary>
    public const string StandInId = "mob.windowman";

    /// <summary>플레이어 쪽으로 도는 속도(°/초) — 천천히.</summary>
    public const float TurnDegreesPerSecond = 70f;

    /// <summary>창 정면에서 이만큼(°)까지만 돈다(벽 쪽을 보며 돌아서지 않게).</summary>
    public const float MaxTurnDegrees = 75f;

    private GameObject _figure;
    private Quaternion _baseRotation;
    private Transform _aim;
    private Vector3 _aimAt;
    private object _program;
    private bool _debug;
    private bool _fled;
    private bool _follow;
    private Vector3 _pane;
    private Vector3 _outward;
    private Vector3 _tangent;
    private static Vector3 s_pane, s_outward, s_tangent;

    /// <summary>94차: 눈길을 따라 옆으로 비켜 서는 빠르기(m/초).</summary>
    public const float FollowSpeed = 1.2f;

    /// <summary>지금 근무 씬의 것. 없으면 null.</summary>
    public static WindowFace Active { get; private set; }

    /// <summary>창밖에 세워 둔 노란 얼굴(서 있지 않으면 null).</summary>
    public GameObject Figure
    {
        get { return _figure; }
    }

    /// <summary>그 밤 달아났는지(시험·검수).</summary>
    public bool Fled
    {
        get { return _fled; }
    }

    /// <summary>이 모형인지(연출 실행기가 지우거나 걸어 나가게 하지 않게).</summary>
    public static bool Owns(GameObject go)
    {
        return go != null && Active != null && Active._figure == go;
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
        if (!FlowAutoInstall.IsDutyScene(scene) || FlowAutoInstall.Exists<WindowFace>(scene)) return;
        FlowAutoInstall.CreateHost<WindowFace>(scene, "WindowFace (auto)");
    }

    private void OnEnable()
    {
        Active = this;
        EventBus.NightRestarted += OnRestarted;
    }

    private void OnDisable()
    {
        EventBus.NightRestarted -= OnRestarted;
        Remove();
        if (Active == this) Active = null;
    }

    private void OnRestarted(RestartResult result)
    {
        // 재시작하면 조우를 다시 기다린다 — 블라인드 지시 뒤 다시 선다.
        Remove();
        _fled = false;
        _debug = false;
    }

    // ── 조우가 부른다 ────────────────────────────────────────

    /// <summary>
    /// 조우가 대역을 세운다(<c>DirectionStage.Present</c>) — 창밖에 세우고 그 모형을 돌려준다. 그 밤 이미 달아났거나 창밖 노란 얼굴 밤이 아니면 null(연출이 제 대역을 세운다).
    /// </summary>
    public GameObject Show()
    {
        if (_fled || NightRun.WindowFaceFled) return null;
        if (_figure == null) Place();
        return _figure;
    }

    // ── 밤 따라가기 ──────────────────────────────────────────

    private void Update()
    {
        NightProgram program = NightRun.IsNightActive ? NightRun.Program : null;
        if (!ReferenceEquals(program, _program))
        {
            _program = program;
            _debug = false;
            _fled = false;
            Remove();
            if (program != null && program.HasEncounter(ProgramCatalog.SuitMan)) MoveSoundPoint();
            return;
        }

        if (_figure == null) return;
        bool windowNight = program != null && program.HasEncounter(ProgramCatalog.SuitMan);
        if (!windowNight && !_debug)
        {
            Remove();
            return;
        }

        bool show = DeathCutscene.Playing == null;   // 사망 컷신과 겹치지 않게
        if (_figure.activeSelf != show) _figure.SetActive(show);
        if (!show) return;

        if (NightRun.WindowFaceFled && !_fled)
        {
            Flee();
            return;
        }

        FacePlayer();
        FollowEye();
    }

    /// <summary>
    /// 94차(민: 「창밖에 노란 얼굴이 확인이 안 되네」): 서쪽 벽이 2m 두께라 유리 칸으로 보이는 각이 좁다 — 세울 때의 눈 한 번으로 정한 자리는
    /// 플레이어가 움직이면 문설주·벽 뒤로 숨었다. 이제 늘 지금 눈에서 그 칸 가운데를 지나는 눈길 끝으로 천천히 비켜 선다(±<see cref="MaxSlide"/>m).
    /// </summary>
    private void FollowEye()
    {
        if (!_follow) return;
        Camera cam = Camera.main;
        if (cam == null) return;
        Vector3 look = _pane - cam.transform.position;
        look.y = 0f;
        if (look.sqrMagnitude < 0.01f) return;
        look.Normalize();
        float dot = Vector3.Dot(look, _outward);
        if (dot < 0.2f) return;   // 창 바깥쪽에 있다 — 그대로
        float side = Mathf.Clamp(Vector3.Dot(look * (OutsideDistance / Mathf.Max(0.35f, dot)), _tangent), -MaxSlide, MaxSlide);
        Vector3 want = _pane + _outward * OutsideDistance + _tangent * side;
        Vector3 now = _figure.transform.position;
        want.y = now.y;
        Vector3 next = Vector3.MoveTowards(now, want, FollowSpeed * Time.deltaTime);
        Vector3 delta = next - now;
        if (delta.sqrMagnitude < 1e-8f) return;
        _figure.transform.position = next;
        _aimAt += delta;
        if (_aim != null) _aim.position = _aimAt;
    }

    /// <summary>플레이어 쪽으로 몸을 천천히 돌린다(수평, 창 정면에서 ±<see cref="MaxTurnDegrees"/>°).</summary>
    private void FacePlayer()
    {
        Camera cam = Camera.main;
        if (cam == null) return;
        Vector3 to = cam.transform.position - _figure.transform.position;
        to.y = 0f;
        if (to.sqrMagnitude < 0.01f) return;
        Vector3 baseFwd = _baseRotation * Vector3.forward;
        baseFwd.y = 0f;
        float angle = Vector3.SignedAngle(baseFwd, to, Vector3.up);
        angle = Mathf.Clamp(angle, -MaxTurnDegrees, MaxTurnDegrees);
        Quaternion want = Quaternion.AngleAxis(angle, Vector3.up) * _baseRotation;
        _figure.transform.rotation = Quaternion.RotateTowards(_figure.transform.rotation, want, TurnDegreesPerSecond * Time.deltaTime);
        if (_aim != null) _aim.SetPositionAndRotation(_aimAt, _baseRotation);   // 응시 상자는 유리 안쪽 그 자리에(몸을 돌려도 따라 돌지 않게)
    }

    /// <summary>창 정면으로 돌아서 창 앞을 가로질러 달아난다(<see cref="StandInExit"/>). 걸음이 없는 대역이면 그냥 지운다.</summary>
    private void Flee()
    {
        _fled = true;
        GameObject go = _figure;
        _figure = null;   // 이제 이 모형은 연출 실행기의 것이 아니다 — 스스로 걸어 나가 지워진다
        if (go == null) return;
        go.transform.rotation = _baseRotation;   // 창과 나란히 옆으로 달아나게
        StandInExit exit = go.GetComponent<StandInExit>();
        if (DirectionStage.Verbose) Debug.Log("[WindowFace] 2초 응시 — 창밖 노란 얼굴이 달아남");
        if (exit != null) exit.Leave();
        else Destroy(go);
    }

    /// <summary>
    /// 85차: 블라인드 L-2(도서관 입구 맞은편 서쪽 창 둘)의 블라인드 오브젝트. 노란 얼굴은 이 창의 블라인드 아래 유리 칸으로 들여다본다 —
    /// 북쪽 창(<c>stage.window.man</c>)은 블라인드 앞에서 책장·장에 가려 보이지 않았다(PlayScene 확인).
    /// </summary>
    public static readonly string[] BlindWindows = { "Interior/Library/JalousieE (6)", "Interior/Library/JalousieE (7)" };

    /// <summary>창(블라인드)에서 바깥으로 떨어져 서는 거리(m) — 서쪽 벽은 두께가 2m라(창틀 안쪽 x≈1.0) 그 밖 유리 너머에 선다.</summary>
    public const float OutsideDistance = 1.45f;

    /// <summary>블라인드 높이에서 발까지(m, 처음 자리 — 세운 뒤 머리 꼭대기를 블라인드 아랫단에 맞춘다).</summary>
    public const float FeetBelowBlind = 3.47f;

    /// <summary>머리 꼭대기가 블라인드 아랫단보다 이만큼(m) 위(음수 = 아래) — 블라인드 앞에 선 눈높이에서 블라인드 아래 유리 칸 가운데에 얼굴이 들어온다.</summary>
    public const float HeadAboveBlindBottom = -0.02f;   // 94차: −0.2는 얼굴이 유리 칸 아랫단(창턱)에 걸려 반쯤 가렸다 — 칸 가운데로

    /// <summary>창 가운데에서 유리 칸 가운데까지(m). 94차: 0 — PlayScene 실측으로 블라인드마다 아래 유리 칸은 하나(폭 0.8m, 블라인드 가운데)였다. 옛 0.33은 칸 가장자리(창틀 뒤)에 세워 얼굴이 거의 보이지 않았다.</summary>
    public const float PaneOffset = 0f;

    /// <summary>눈길을 따라 옆으로 비켜 서는 최대(m) — 벽 속으로 들어가지 않게.</summary>
    public const float MaxSlide = 0.6f;

    private static float s_blindBottom = float.NaN;

    /// <summary>94차(민: 「창밖에 노란 얼굴이 확인이 안 되네」): 원본 색(<see cref="TrueColor"/>, 노출을 덜어 낸 색)에 곱하는 밝기 — 창 너머 어둠에서도 얼굴을 알아보게.</summary>
    public const float Brightness = 2.4f;

    /// <summary>그 아래 렌더러의 기본 색을 <paramref name="k"/>배로(이 인스턴스만 — 재질 블록).</summary>
    private static void Brighten(GameObject go, float k)
    {
        MaterialPropertyBlock mpb = new MaterialPropertyBlock();
        foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
        {
            Material[] mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                Material m = mats[i];
                if (m == null || !m.HasProperty("_BaseColor")) continue;
                Color c = m.GetColor("_BaseColor");
                r.GetPropertyBlock(mpb, i);
                mpb.SetColor("_BaseColor", new Color(c.r * k, c.g * k, c.b * k, c.a));
                r.SetPropertyBlock(mpb, i);
            }
        }
    }

    private void Place()
    {
        StageAnchor at = StageAnchor.Find(StageAnchors.WindowMan);
        Vector3 pos;
        Quaternion rot;
        if (!BlindWindowPose(out pos, out rot))
        {
            if (at == null)
            {
                Debug.LogWarning("[WindowFace] 블라인드 창도 창밖 자리(stage.window.man)도 찾지 못했습니다.");
                return;
            }

            pos = at.transform.position;
            rot = at.transform.rotation;
        }

        bool blind = !float.IsNaN(s_blindBottom);
        _baseRotation = rot;
        _follow = blind;
        _pane = s_pane;
        _outward = s_outward;
        _tangent = s_tangent;
        _figure = StandInFactory.Create(StandInId, pos, rot, FinalCues.WindowFaceTarget);
        if (_figure == null) return;
        _aim = null;
        if (blind)
        {
            _aim = FitToBlindPane(_figure);
            if (_aim != null) _aimAt = _aim.position;
        }

        else if (at != null && at.GazeProxy != null) StandInFactory.ApplyGazeProxy(_figure, at.GazeProxy);
        TrueColor.Apply(_figure);   // 어두운 창 밖에서도 노란 얼굴이 원래 색으로(사망·놀람 얼굴과 같은 처리)
        Brighten(_figure, Brightness);   // 94차: 창 너머 어둠 속이라 놀람 얼굴보다 밝게 — 알아볼 수 있게
        _figure.name = "창밖 노란 얼굴";
        if (DirectionStage.Verbose) Debug.Log("[WindowFace] 창밖에 노란 얼굴을 세움 @" + pos.ToString("F1"));
    }

    /// <summary>
    /// 블라인드 창 둘 중 플레이어에게서 가까운 쪽 바깥 자리 — 창에서 바깥으로 <see cref="OutsideDistance"/>, 안을 보고 선다.
    /// 바깥 방향은 블라인드의 앞(±)축 중 플레이어(도서관 안)에서 먼 쪽.
    /// </summary>
    private static bool BlindWindowPose(out Vector3 pos, out Quaternion rot)
    {
        return BlindWindowPose(true, out pos, out rot);
    }

    /// <summary>
    /// 머리 꼭대기를 블라인드 아랫단에 맞추고, 응시 상자를 그 칸 앞(방 쪽 벽면 바로 안쪽)에 둔다(다른 콜라이더는 끈다 — 응시는 상자 하나의 중심을 본다).
    /// 바깥벽 메시 콜라이더가 유리까지 막아 얼굴에 둔 상자에는 응시 레이가 닿지 않는다(PlayScene 확인) — 북쪽 창 고정 자리의 응시 대리 상자와 같은 까닭.
    /// </summary>
    private static Transform FitToBlindPane(GameObject go)
    {
        Bounds b = Bounds(go);
        go.transform.position += Vector3.up * (s_blindBottom + HeadAboveBlindBottom - b.max.y);
        b = Bounds(go);

        Transform aim = StandInFactory.Aim(go);
        foreach (Collider c in go.GetComponentsInChildren<Collider>(true))
        {
            if (c.transform != aim) c.enabled = false;
        }

        Vector3 face = new Vector3(b.center.x, b.max.y - 0.3f, b.center.z);
        Vector3 inward = go.transform.forward;   // 안(플레이어 쪽)을 보고 선다
        inward.y = 0f;
        inward.Normalize();
        aim.position = face;
        // 눈(세울 때의 카메라)에서 얼굴로 가는 눈길이 방 쪽 벽면(블라인드 면)을 지나는 자리 바로 안쪽에 둔다 —
        // 칸 가운데 앞이라 책장에 가리지 않고, 두꺼운 창(유리 바깥면) 속에 묻히지 않는다.
        Camera cam = Camera.main;
        if (cam != null)
        {
            Vector3 toEye = cam.transform.position - face;
            float depth = Vector3.Dot(toEye, inward);
            float want = OutsideDistance + 0.12f;
            if (depth > want) aim.position = face + toEye * (want / depth);
        }

        aim.rotation = go.transform.rotation;
        BoxCollider box = aim.GetComponent<BoxCollider>();
        if (box == null) box = aim.gameObject.AddComponent<BoxCollider>();
        Vector3 s = aim.lossyScale;
        box.center = Vector3.zero;
        box.size = new Vector3(0.5f / Mathf.Max(0.0001f, Mathf.Abs(s.x)), 0.45f / Mathf.Max(0.0001f, Mathf.Abs(s.y)), 0.5f / Mathf.Max(0.0001f, Mathf.Abs(s.z)));
        box.isTrigger = false;
        box.enabled = true;
        JudgeTarget target = aim.GetComponent<JudgeTarget>();
        if (target == null) target = aim.gameObject.AddComponent<JudgeTarget>();
        target.SetIds(FinalCues.WindowFaceTarget);
        return aim;
    }

    private static Bounds Bounds(GameObject go)
    {
        Bounds b = new Bounds(go.transform.position, Vector3.zero);
        foreach (Renderer r in go.GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
        return b;
    }

    /// <summary>
    /// <paramref name="nearest"/>: 블라인드 창 두 개 × 칸 두 개(가운데 문설주) 중 플레이어 눈에서 가려지지 않은(책장 등) 칸, 그중 가까운 칸 /
    /// 아니면 두 창 가운데(소리 자리).
    /// </summary>
    private static bool BlindWindowPose(bool nearest, out Vector3 pos, out Quaternion rot)
    {
        pos = Vector3.zero;
        rot = Quaternion.identity;
        Camera cam = Camera.main;
        Vector3 inside = cam != null ? cam.transform.position : Vector3.zero;
        s_blindBottom = float.NaN;
        List<Transform> windows = new List<Transform>();
        Vector3 sum = Vector3.zero;
        for (int i = 0; i < BlindWindows.Length; i++)
        {
            GameObject go = GameObject.Find(BlindWindows[i]);
            if (go == null) continue;
            windows.Add(go.transform);
            sum += go.transform.position;
        }

        if (windows.Count == 0) return false;
        Transform first = windows[0];
        Vector3 fwd = first.forward;
        fwd.y = 0f;
        if (fwd.sqrMagnitude < 1e-4f) return false;
        fwd.Normalize();
        Vector3 center = sum / windows.Count;
        Vector3 away = center - inside;
        away.y = 0f;
        Vector3 outward = Vector3.Dot(fwd, away) >= 0f ? fwd : -fwd;
        Vector3 tangent = Vector3.Cross(Vector3.up, outward);

        float bottom = float.MaxValue;
        foreach (Renderer r in first.GetComponentsInChildren<Renderer>()) bottom = Mathf.Min(bottom, r.bounds.min.y);
        s_blindBottom = bottom < float.MaxValue ? bottom : first.position.y - 1.58f;

        if (!nearest)
        {
            pos = center + outward * OutsideDistance;
            pos.y = center.y - FeetBelowBlind;
            rot = Quaternion.LookRotation(-outward, Vector3.up);
            return true;
        }

        // 칸 고르기 — 블라인드 아래 칸 가운데(창 면, 얼굴 높이)까지 눈길이 트였는지.
        Vector3 bestPane = Vector3.zero;
        float bestScore = float.MaxValue;
        for (int w = 0; w < windows.Count; w++)
        {
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 pane = windows[w].position + tangent * (s * PaneOffset);
                Vector3 probe = pane;
                probe.y = s_blindBottom - 0.35f;
                float score = (probe - inside).magnitude;
                if (!LineClear(inside, probe)) score += 100f;   // 가려진 칸은 다른 칸이 모두 가려졌을 때만
                if (score < bestScore)
                {
                    bestScore = score;
                    bestPane = pane;
                }
            }
        }

        // 그 칸 가운데를 지나는 눈길 끝에 선다(문설주 뒤에 숨지 않게).
        s_pane = bestPane;
        s_outward = outward;
        s_tangent = tangent;
        pos = bestPane + outward * OutsideDistance;
        Vector3 look = bestPane - inside;
        look.y = 0f;
        if (look.sqrMagnitude > 0.01f)
        {
            look.Normalize();
            Vector3 along = look * (OutsideDistance / Mathf.Max(0.35f, Vector3.Dot(look, outward)));
            float side = Mathf.Clamp(Vector3.Dot(along, tangent), -MaxSlide, MaxSlide);
            pos += tangent * side;
        }

        pos.y = bestPane.y - FeetBelowBlind;
        rot = Quaternion.LookRotation(-outward, Vector3.up);
        return true;
    }

    /// <summary>눈에서 그 점(창 면)까지 가리는 것이 없는지 — 창 면 바로 앞 5cm는 보지 않는다(벽·블라인드 자체).</summary>
    private static bool LineClear(Vector3 eye, Vector3 at)
    {
        Vector3 d = at - eye;
        float dist = d.magnitude;
        if (dist < 0.1f) return true;
        RaycastHit hit;
        if (!Physics.Raycast(eye, d / dist, out hit, dist, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return true;
        if (hit.distance >= dist - 0.25f) return true;
        Transform p = PlayerSensors.Active != null ? PlayerSensors.Active.PlayerRoot : null;
        return p != null && hit.collider.transform.IsChildOf(p);
    }

    /// <summary>창밖 노란 얼굴 밤 — 조우의 소리 자리(전조)를 블라인드 창 밖으로 옮긴다(씬의 북쪽 창 고정 자리는 그대로 두고 런타임 자리만).</summary>
    private static void MoveSoundPoint()
    {
        Vector3 pos;
        Quaternion rot;
        if (BlindWindowPose(false, out pos, out rot)) StagePoints.Set(StageAnchors.WindowMan, pos);
    }

    private void Remove()
    {
        if (_figure != null) Destroy(_figure);
        _figure = null;
    }

    // ── 디버그 ───────────────────────────────────────────────

    /// <summary>디버그: 편성과 무관하게 이 밤에 세운다(개발자 모드·시연).</summary>
    public void DebugPlace()
    {
        Remove();
        _fled = false;
        Place();
        _debug = _figure != null;
    }
}

using System.Collections.Generic;
using NightDuty;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// 창밖 노란 얼굴(84차 빌드업 · 85차 흐름 — 민: 「도서관 입구 맞은편 블라인드 점검 지시 → 창문 조우(플레이어 쪽을 바라보다가, 플레이어가 응시를 2초 이상 하면 도망감)
/// → 다음 날 biology 책 습득 or 다른 도서관 순찰 → 도서관 문 조우」).
/// <list type="bullet">
/// <item>그 밤 편성에 <see cref="ProgramCatalog.SuitMan"/>(창밖 노란 얼굴 — 노란 얼굴 전날, 날짜 사슬)이 있으면, 조우가 대역을 세울 때(<see cref="Show"/> —
/// 블라인드 L-2 단독 지시 뒤 도서관 6초) <b>그 블라인드 창</b>(도서관 입구 맞은편 서쪽 창 둘 중 플레이어에게 가까운 쪽) 밖에 노란 얼굴(<c>mob.windowman</c>)이 서서
/// 블라인드 아래 유리 칸으로 들여다본다. 밤 내내 서 있지는 않는다. 블라인드 창을 못 찾으면 옛 북쪽 창 자리(<c>stage.window.man</c>).</item>
/// <item>서 있는 동안 몸은 창 정면 그대로, <b>고개만</b> 플레이어(카메라)를 따라 돌려 바라본다(<see cref="DoorDuck"/> 고개 돌림 — 103차 후속, 민: 「눈길을 따라 비켜서는 게 아니라 목만 움직이게」).
/// 세울 때 자리를 정하면 옮기지 않는다.</item>
/// <item>플레이어가 <see cref="FinalCues.WindowFaceTarget"/>을 2초 넘게 이어서 보면(<see cref="NightRun.WindowFaceFled"/>) 창 정면으로 돌아서 창 앞을 가로질러 달아난다
/// (<see cref="StandInExit.Leave"/> — 기괴하게 빠른 걸음, 화면 밖이 되면 사라짐). 그 밤에는 다시 서지 않는다(재시작이면 다시).</item>
/// <item>보지 않으면 조우가 끝나도 그 밤 내내 창밖에서 바라보고 있다. 다음 날(문간 노란 얼굴 밤)에는 창밖에 서지 않는다.</item>
/// <item>수칙과 무관(85차: L3는 문간 노란 얼굴만). 사망 컷신이 도는 동안은 숨는다.</item>
/// <item>103차 후속(민: 「작고 평면적이고 라이팅이 안 먹어 이질감」): 원본 색(Unlit, <see cref="TrueColor"/>)을 그만두고 빛을 받는 재질 그대로 둔다 —
/// 얼굴 전용 조명(<see cref="FaceLightLayer"/> 렌더링 레이어만 비춤)이 옆 위에서 비추고, 손전등도 그대로 먹는다. <see cref="Scale"/>배로 키워
/// 가운데 문설주를 피해 아래 유리 칸 하나(<see cref="SashOffset"/>)에 얼굴이 꽉 차게 유리 바로 뒤(<see cref="GlassInset"/>)에 붙어 선다.</item>
/// </list>
/// 근무 씬에 자동으로 선다. 씬은 고치지 않는다.
/// </summary>
[DisallowMultipleComponent]
public sealed class WindowFace : MonoBehaviour
{
    /// <summary>대역 ID(문간 노란 얼굴과 같은 business duck — 창밖용 프리팹).</summary>
    public const string StandInId = "mob.windowman";

    private GameObject _figure;
    private Quaternion _baseRotation;
    private Transform _aim;
    private object _program;
    private bool _debug;
    private bool _fled;

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

        if (NightRun.WindowFaceFled && !_fled) Flee();
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

    /// <summary>창(블라인드)에서 바깥으로 떨어져 서는 거리(m, 발 자리) — 서쪽 벽은 두께가 2m라 그 밖 유리 너머에 선다.
    /// 103차 후속: 1.45 → 1.25 — 얼굴이 유리 바로 뒤(약 0.2m)에 붙게.</summary>
    public const float OutsideDistance = 1.25f;

    /// <summary>103차 후속: 블라인드 면에서 창틀·유리 면까지(m, 바깥쪽). PlayScene 실측 — 블라인드 x 2.07, 창틀 x 1.29.
    /// 보이는 구멍은 이 면의 유리 칸이라 눈길은 여기를 지난다(옛 계산은 블라인드 면을 지나게 해 비스듬히 보면 얼굴이 창틀 뒤로 숨었다).</summary>
    public const float GlassInset = 0.78f;

    /// <summary>103차 후속: 창 가운데(문설주)에서 아래 유리 칸 가운데까지(m). PlayScene 실측 — 칸 폭 약 0.28m, 문설주 0.07m.</summary>
    public const float SashOffset = 0.175f;

    /// <summary>103차 후속: 모형 크기 배율 — 얼굴이 유리 칸(0.28×0.4m)을 거의 채우게.</summary>
    public const float Scale = 1.3f;

    /// <summary>발 자리에서 얼굴 앞면까지(m, 키운 뒤 대략).</summary>
    public const float FaceAhead = 0.25f;

    /// <summary>유리 면에서 얼굴까지(m) — 세울 때 눈길을 따라 옆으로 비켜 서는 양을 정한다.</summary>
    public const float FaceDepth = OutsideDistance - GlassInset - FaceAhead;

    /// <summary>블라인드 높이에서 발까지(m, 처음 자리 — 세운 뒤 머리 꼭대기를 블라인드 아랫단에 맞춘다).</summary>
    public const float FeetBelowBlind = 3.47f;

    /// <summary>머리 꼭대기가 블라인드 아랫단보다 이만큼(m) 위(음수 = 아래) — 블라인드 앞에 선 눈높이에서 블라인드 아래 유리 칸 가운데에 얼굴이 들어온다.</summary>
    public const float HeadAboveBlindBottom = 0.04f;   // 94차: −0.2는 얼굴이 유리 칸 아랫단(창턱)에 걸려 반쯤 가렸다 · 103차 후속: 1.3배라 정수리를 칸 위 가로살 뒤로 — 눈이 칸 위쪽, 턱이 창턱 위

    /// <summary>세울 때 그때 눈길을 따라 옆으로 비켜 서는 최대(m) — 칸 밖 창틀 뒤로 가지 않게(103차 후속: 0.6 → 0.25). 세운 뒤에는 옮기지 않는다.</summary>
    public const float MaxSlide = 0.25f;

    private static float s_blindBottom = float.NaN;

    /// <summary>103차 후속: 얼굴 전용 조명이 비추는 렌더링 레이어(이 모형만 이 비트를 더 가진다 — 창틀·벽은 비추지 않는다).</summary>
    public const int FaceLightLayer = 7;

    /// <summary>얼굴 전용 조명 세기 — 창 너머 어둠에서도 알아보되 입체가 살게(옆 위에서 비추는 차가운 빛).</summary>
    public const float FaceLightIntensity = 3f;

    /// <summary>
    /// 103차 후속(민: 「너무 평면적이고 라이팅 적용이 안 되는 것 같아 이질감」): 94차의 원본 색(Unlit × 2.4)은 빛·그림자가 없어 스티커처럼 떠 보였다 —
    /// 빛을 받는 재질 그대로 두고, 얼굴 앞 옆 위에 이 모형만 비추는 스포트 조명을 단다(그림자 없음). 모형의 자식이라 몸을 돌려도·달아나도 같이 간다.
    /// </summary>
    private static Light AddFaceLight(GameObject go)
    {
        uint bit = 1u << FaceLightLayer;
        foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true)) r.renderingLayerMask |= bit;
        Bounds b = Bounds(go);
        Vector3 face = new Vector3(b.center.x, b.max.y - 0.22f * Scale, b.center.z);
        GameObject host = new GameObject("창밖 얼굴 조명");
        host.transform.SetParent(go.transform, false);
        host.transform.position = face + go.transform.right * 0.4f + Vector3.up * 0.4f + go.transform.forward * 0.9f;
        host.transform.rotation = Quaternion.LookRotation(face - host.transform.position, Vector3.up);
        Light light = host.AddComponent<Light>();
        light.type = LightType.Spot;
        light.color = new Color(0.78f, 0.85f, 1f);
        light.intensity = FaceLightIntensity;
        light.range = 3f;
        light.spotAngle = 45f;
        light.innerSpotAngle = 18f;
        light.shadows = LightShadows.None;
        UniversalAdditionalLightData data = host.GetComponent<UniversalAdditionalLightData>();
        if (data == null) data = host.AddComponent<UniversalAdditionalLightData>();
        data.renderingLayers = bit;
        return light;
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
        _figure = StandInFactory.Create(StandInId, pos, rot, FinalCues.WindowFaceTarget);
        if (_figure == null) return;
        _figure.transform.localScale *= Scale;   // 103차 후속: 맞추기(머리 꼭대기·응시 상자) 전에 키운다
        _aim = null;
        if (blind) _aim = FitToBlindPane(_figure);
        else if (at != null && at.GazeProxy != null) StandInFactory.ApplyGazeProxy(_figure, at.GazeProxy);
        AddFaceLight(_figure);   // 103차 후속: 원본 색(Unlit) 대신 빛을 받는 재질 + 얼굴 전용 조명
        DoorDuck.Attach(_figure, false);   // 103차 후속: 몸은 그대로, 고개만 플레이어를 따라 돈다(문간 노란 얼굴과 같은 고개 돌림)
        StandInExit exit = _figure.GetComponent<StandInExit>();
        if (exit != null) exit.RunFar = false;   // 달아날 때는 옛날처럼 창 앞을 가로질러(멀리 트인 쪽으로 뛰지 않게)
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

        Vector3 face = new Vector3(b.center.x, b.max.y - 0.25f * Scale, b.center.z);
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
                Vector3 pane = windows[w].position + tangent * (s * SashOffset);   // 103차 후속: 문설주 양옆 유리 칸 둘
                Vector3 probe = pane + outward * GlassInset;   // 그 칸 유리 면 가운데
                probe.y = s_blindBottom - 0.22f;
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
        pos = bestPane + outward * OutsideDistance;
        Vector3 look = bestPane + outward * GlassInset - inside;
        look.y = 0f;
        if (look.sqrMagnitude > 0.01f)
        {
            look.Normalize();
            float side = Mathf.Clamp(Vector3.Dot(look, tangent) / Mathf.Max(0.35f, Vector3.Dot(look, outward)) * FaceDepth, -MaxSlide, MaxSlide);
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

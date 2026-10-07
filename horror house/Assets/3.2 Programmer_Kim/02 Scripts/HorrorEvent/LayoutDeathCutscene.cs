using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

/// <summary>
/// 배치 축 붙잡힘(사망) 컷신 — 「발소리 에코가 길어지고, 사람 나무. 화면이 가지 사이로 끌려 들어감」.
///
/// <para><b>흐름</b> — <see cref="Play"/>
/// ① 조작 잠금 · 다른 소리 끊김(컷신 소리만)
/// ② 플레이어와 <see cref="LayoutDeathSpot"/>(사람 나무 자리) 사이 구역만 충돌체로 즉석 NavMesh를 구워,
///    벽·사물을 피하는 길이 있는 가장 가까운 자리를 고른다(금지 반경 안의 자리는 뺀다 — 없으면 재생 안 함)
/// ③ 그 자리에 사람 나무(<see cref="treePrefab"/>)를 세운다 — 흔들림·바라보면 화면이 물드는 효과는 프리팹 그대로
/// ④ 비틀거리며 길을 따라 걷는다. 발소리 에코·잔향이 점점 길어지고, 속삭임·웅얼거림이 차오르고, 화면이 점점 일렁인다
/// ⑤ 나무에 <see cref="pullDistance"/>까지 다가가면 확 끌려 들어가며 화면이 먹물 얼룩처럼 검게 덮인다
/// ⑥ 끝 — 디버그 재생이면 모든 것을 원래대로(위치까지) 돌린다. 사망으로 쓸 때는 검은 화면을 사망 화면이 이어받는다</para>
///
/// <para>걷는 거리가 매번 달라 Timeline 대신 코드로 진행한다. 소리·나무는 빌더 메뉴
/// 「Tools ▸ Programmer_Kim ▸ Horror ▸ Build Death Cutscene (Layout)」가 프리팹에 연결한다.</para>
///
/// <para><b>ver2(피 비)</b>는 <c>bloodRain</c>을 켠 프리팹(<see cref="ResourceNameV2"/>)이다 — 앞에 떨어지는 핏방울 → 천장의 피 얼룩 →
/// 비처럼 쏟아지는 피·가득 선 사람 나무 → 가까운 나무로 걸어감. 걷기부터는 ver1과 같다. 코드는 <c>LayoutDeathCutscene.BloodRain.cs</c>.</para>
/// </summary>
[DisallowMultipleComponent]
public partial class LayoutDeathCutscene : MonoBehaviour
{
    public const string ResourceName = "DeathCutscene_Layout";
    public const float DefaultBlockRadius = 2f;

    /// <summary>계속 도는 소리 한 겹. 걸어간 정도(0~1)에 따라 차오른다.</summary>
    [Serializable]
    public class Layer
    {
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 0.6f;
        [Tooltip("이 진행도(0 = 출발, 1 = 끌려 들어가기 직전)부터 들리기 시작")]
        [Range(0f, 1f)] public float fromProgress;
        [Tooltip("이 진행도에서 최대 볼륨")]
        [Range(0f, 1f)] public float fullProgress = 1f;
        [Tooltip("이 진행도부터 줄어들기 시작(1 이상이면 줄지 않음) — 다음 겹으로 넘겨줄 때(심장 박동이 빨라지는 판으로 바뀔 때)")]
        [Range(0f, 1.01f)] public float fadeOutFrom = 1.01f;
        [Tooltip("이 진행도에서 완전히 사라짐")]
        [Range(0f, 1.01f)] public float fadeOutTo = 1.01f;
        [Tooltip("켜면 나무 자리에서 나는 3D 소리(가까워질수록 커짐)")]
        public bool atTree;
        [Tooltip("같은 소리를 몇 겹 겹쳐 틀지 — 원본이 작게 녹음돼 볼륨 1로도 묻힐 때(볼륨은 1을 넘길 수 없다). 2겹 ≈ 2배, 3겹 ≈ 3배.")]
        [Range(1, 4)] public int copies = 1;
        [Tooltip("재생 시작 지점(초)")]
        public float clipStart;
    }

    /// <summary>한 번 터지는 소리. clipStart를 가장 큰 지점 바로 앞으로 두면 그 순간 터진다(빌더가 잰다).</summary>
    [Serializable]
    public class Hit
    {
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 1f;
        public float clipStart;
        public float delay;
    }

    [Header("사람 나무")]
    [SerializeField] private GameObject treePrefab;
    [Tooltip("끌려 들어갈 가지 사이 높이(바닥에서, m). 사람 나무는 약 3.9m.")]
    [SerializeField] private float branchHeight = 2.2f;
    [Tooltip("바라볼 높이 — 다가가며 이 높이를 본다.")]
    [SerializeField] private float lookHeight = 2.0f;

    [Header("자리 고르기")]
    [Tooltip("플레이어가 자리에서 이 반경(수평 m) 안에 있으면 그 자리는 쓰지 않는다.")]
    [SerializeField, Min(0f)] private float blockRadius = DefaultBlockRadius;
    [Tooltip("이보다 먼(직선 m) 자리는 찾지 않는다.")]
    [SerializeField, Min(1f)] private float searchRange = 35f;
    [Tooltip("걸어갈 길이 이보다 길면(m) 그 자리는 쓰지 않는다.")]
    [SerializeField, Min(1f)] private float maxPathLength = 40f;

    [Header("걸음")]
    [SerializeField, Min(0f)] private float freezeSeconds = 0.6f;
    [SerializeField, Min(0.1f)] private float walkSpeed = 1.45f;
    [SerializeField, Min(0.1f)] private float stepLength = 0.72f;
    [Tooltip("나무에서 이 거리(수평 m)까지 다가가면 끌려 들어간다.")]
    [SerializeField, Min(0.5f)] private float pullDistance = 2.3f;
    [Tooltip("비틀거림 = 눕힌 8자(∞). 한 바퀴 시간(초) — 그 사이 좌우 한 번, 위아래 두 번.")]
    [SerializeField, Min(0.5f)] private float swayPeriod = 3.2f;
    [Tooltip("8자의 좌우 폭(°)")]
    [SerializeField, Range(0f, 15f)] private float swayYawDeg = 4f;
    [Tooltip("8자의 위아래 폭(°)")]
    [SerializeField, Range(0f, 15f)] private float swayPitchDeg = 3f;
    [Tooltip("좌우로 흔들릴 때 함께 기우는 각(°)")]
    [SerializeField, Range(0f, 15f)] private float staggerRoll = 4f;
    [SerializeField, Range(30f, 90f)] private float approachFov = 46f;

    [Header("끌려 들어감 · 먹물")]
    [SerializeField, Min(0.05f)] private float pullSeconds = 0.38f;
    [SerializeField, Range(30f, 120f)] private float pullFov = 85f;
    [SerializeField, Min(0f)] private float blotDelay = 0.08f;
    [SerializeField, Min(0.05f)] private float blotSeconds = 0.32f;
    [SerializeField, Min(0f)] private float blackHold = 1.0f;

    [Header("손전등")]
    [Tooltip("켜면 컷신 동안 플레이어 손전등을 켠다 — 어두운 복도에서 나무가 손전등 빛에 드러난다. 끝나면 원래 상태로.")]
    [SerializeField] private bool forceFlashlight = true;

    [Header("다가갈수록 어두워짐")]
    [Tooltip("나무에서 이 거리(수평 m) 안으로 들어오면 어두워지기 시작한다 — 끌려 드는 거리에서 최대.")]
    [SerializeField, Min(0.5f)] private float darkenFrom = 5f;
    [Tooltip("최대일 때 가장자리 어둠(비네트) 불투명도")]
    [SerializeField, Range(0f, 1f)] private float darkenVignette = 0.95f;
    [Tooltip("최대일 때 화면 전체 어둠 불투명도")]
    [SerializeField, Range(0f, 1f)] private float darkenOverall = 0.45f;

    [Header("화면 일렁임")]
    [SerializeField, Range(0f, 1f)] private float wobbleMax = 0.85f;

    [Header("소리")]
    [SerializeField] private AudioClip[] stepClips = new AudioClip[0];
    [SerializeField, Range(0f, 1f)] private float stepVolume = 0.45f;
    [Tooltip("발소리 되울림 간격(ms) — 고정. 재생 중에 바꾸면 딸깍거린다.")]
    [SerializeField, Range(50f, 1000f)] private float echoDelayMs = 320f;
    [Tooltip("갑자기 비틀거릴 때")]
    [SerializeField] private Hit[] startHits = new Hit[0];
    [Tooltip("걸어가는 동안 차오르는 소리")]
    [SerializeField] private Layer[] layers = new Layer[0];
    [Tooltip("끌려 들어가기 직전부터 차오르는 빨려 듦 — clipStart부터 틀어 가장 큰 지점이 끌려 드는 순간에 오게 미리 건다")]
    [SerializeField] private Hit suckIn = new Hit();
    [Tooltip("이 소리의 가장 큰 지점(clipStart 기준 초). 빌더가 잰다.")]
    [SerializeField] private float suckInPeak = 2.8f;
    [Tooltip("끌려 드는 순간")]
    [SerializeField] private Hit[] pullHits = new Hit[0];
    [Tooltip("완전히 검어진 뒤")]
    [SerializeField] private Hit[] endHits = new Hit[0];

    [Header("디버그")]
    [Tooltip("켜면 아래 디버그 키로 이 컷신을 재생할 수 있다(에디터 · 개발 빌드만). 끄면 키를 눌러도 반응하지 않는다.\n" +
             "Resources에서 키로 불러오는 감시자(DeathCutsceneDebugSpawner)도 이 프리팹의 값을 따른다.")]
    [SerializeField] private bool useDebugKey = false;
    [SerializeField] private KeyCode debugKey = KeyCode.F6;

    /// <summary>끝까지 재생됐을 때.</summary>
    public event Action Finished;

    public static LayoutDeathCutscene Playing { get; private set; }
    public bool IsPlaying { get { return Playing == this; } }
    public KeyCode DebugKey { get { return debugKey; } }
    /// <summary>디버그 키로 재생할 수 있는지.</summary>
    public bool UseDebugKey { get { return useDebugKey; } }

    /// <summary>마지막으로 재생하지 못한 이유(디버그·로그용).</summary>
    public string LastRefusal { get; private set; }

    /// <summary>지금 단계 — Freeze · Walk · Pull · Ink · Black(디버그·검증용).</summary>
    public string Phase { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatic()
    {
        Playing = null;
    }

    // ── 플레이어 · 복구 ──
    private Transform player;
    private Camera cam;
    private Behaviour controller;
    private Rigidbody body;
    private bool bodyWasKinematic;
    private GameObject viewmodel;
    private bool viewmodelWasActive;
    private Vector3 playerPos;
    private Quaternion playerRot;
    private Vector3 camLocalPos;
    private Quaternion camLocalRot;
    private float camFov;
    private CursorLockMode cursorWasLock;
    private bool pausedAudio;
    private bool restoreOnEnd;
    private readonly List<Canvas> hiddenCanvases = new List<Canvas>();
    private GameObject lamp;
    private bool lampWasActive;
    private Light[] lampLights = new Light[0];
    private bool[] lampEnabled = new bool[0];

    // ── 연출 ──
    private NavMeshDataInstance navInstance;
    private int agentTypeId = -1;
    private GameObject tree;
    private Vector3 treeBase;
    private ScreenWobbleSource wobble;
    private AudioSource stepSource;
    private AudioEchoFilter stepEcho;
    private AudioReverbFilter stepReverb;
    private readonly List<AudioSource> layerSources = new List<AudioSource>();
    private readonly List<int> layerOf = new List<int>();   // layerSources[j]가 몇 번째 겹인지
    private readonly List<AudioSource> hitSources = new List<AudioSource>();
    private Canvas inkCanvas;
    private RawImage inkFull;
    private RawImage darkOverall;
    private RawImage darkVignette;
    private readonly List<RawImage> blots = new List<RawImage>();
    private Coroutine running;
    private int startedFrame = -1;

    // 카메라(LateUpdate에서 적용)
    private bool camDriven;
    private Vector3 camPos;
    private Quaternion camRot;
    private float camFovNow;
    private float blendIn;
    private Vector3 blendFromPos;
    private Quaternion blendFromRot;

    // ─────────────────────────────── 재생 ───────────────────────────────

    /// <summary>
    /// 재생한다. 갈 수 있는 자리가 없거나(금지 반경 안·길 없음) 다른 컷신이 재생 중이면 거짓 — 이유는 <see cref="LastRefusal"/>.
    /// <paramref name="restoreAfter"/>가 참이면 끝난 뒤 위치·조작·카메라·소리를 원래대로 돌린다(디버그).
    /// </summary>
    public bool Play(bool restoreAfter)
    {
        LastRefusal = null;
        if (Playing != null || DeathCutscene.Playing != null) return Refuse("다른 사망 컷신이 재생 중입니다.");
        if (bloodRain) return PlayBloodRain(restoreAfter);
        baseWobble = 0f;
        baseVignette = 0f;

        FPController fp = FindAnyObjectByType<FPController>();
        if (fp == null) return Refuse("플레이어(FPController)가 없습니다.");
        Camera c = fp.GetComponentInChildren<Camera>();
        if (c == null) return Refuse("플레이어 카메라가 없습니다.");
        if (LayoutDeathSpot.All.Count == 0) return Refuse("씬에 LayoutDeathSpot(사람 나무 자리)이 없습니다.");

        player = fp.transform;
        cam = c;
        Vector3 feet = FindFeet(fp);

        // 길 찾기 — 실패하면 아무것도 바꾸지 않은 채 거절
        List<Vector3> path;
        LayoutDeathSpot spot = ChooseSpot(feet, out path);
        if (spot == null)
        {
            RemoveNavMesh();
            return false;   // LastRefusal은 ChooseSpot이 채움
        }

        // ── 잠금 ──
        restoreOnEnd = restoreAfter;
        RememberPose();
        LockPlayer(fp);

        if (forceFlashlight) GrabFlashlight();
        BuildRuntimeParts();
        SpawnTree(spot, path);

        Playing = this;
        startedFrame = Time.frameCount;
        gameObject.SetActive(true);
        running = StartCoroutine(Run(path, feet, freezeSeconds, 0f));
        return true;
    }

    /// <summary>끝난 뒤 되돌릴 위치·카메라를 적어 둔다(디버그 재생).</summary>
    private void RememberPose()
    {
        playerPos = player.position;
        playerRot = player.rotation;
        camLocalPos = cam.transform.localPosition;
        camLocalRot = cam.transform.localRotation;
        camFov = cam.fieldOfView;
    }

    /// <summary>조작을 잠근다 — 이동·커서·화면 UI를 막고 다른 소리를 끊는다(컷신 소리만 들린다).</summary>
    private void LockPlayer(FPController fp)
    {
        controller = fp;
        controller.enabled = false;
        body = fp.GetComponent<Rigidbody>();
        if (body != null)
        {
            bodyWasKinematic = body.isKinematic;
            if (!body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
            body.isKinematic = true;   // 길을 따라 직접 옮긴다(길이 이미 벽을 피한다)
        }
        cursorWasLock = Cursor.lockState;
        HideCursor();
        // 태블릿(손)은 끌려 들어가며 화면이 일그러지기 직전까지 그대로 둔다 — 치우는 것은 Run의 Pull 단계
        PlayerTablet tablet = cam.GetComponentInChildren<PlayerTablet>(true);
        viewmodel = tablet != null ? tablet.gameObject : null;
        if (viewmodel != null) viewmodelWasActive = viewmodel.activeSelf;
        HideScreenUi();
        if (!AudioListener.pause)
        {
            AudioListener.pause = true;
            pausedAudio = true;
        }
    }

    private bool Refuse(string why)
    {
        LastRefusal = why;
        Debug.Log("[LayoutDeathCutscene] 재생 안 함 — " + why);
        return false;
    }

    /// <summary>
    /// 플레이어 손전등을 켠다 — 카메라 아래 이름에 Flashlight가 들어가고 Light를 가진 가지(FlashlightRelay와 같은 규칙).
    /// 판정 쪽 켜짐 상태(FlashlightRelay)는 건드리지 않는다 — 연출용 빛일 뿐이다.
    /// </summary>
    private void GrabFlashlight()
    {
        lamp = null;
        foreach (Transform t in cam.GetComponentsInChildren<Transform>(true))
        {
            if (t == cam.transform || t.name.IndexOf("Flashlight", StringComparison.OrdinalIgnoreCase) < 0) continue;
            if (t.GetComponentInChildren<Light>(true) == null) continue;
            lamp = t.gameObject;
            break;
        }
        if (lamp == null) return;
        lampWasActive = lamp.activeSelf;
        lampLights = lamp.GetComponentsInChildren<Light>(true);
        lampEnabled = new bool[lampLights.Length];
        for (int i = 0; i < lampLights.Length; i++)
        {
            lampEnabled[i] = lampLights[i].enabled;
            lampLights[i].enabled = true;
        }
        lamp.SetActive(true);
    }

    private void ReleaseFlashlight()
    {
        for (int i = 0; i < lampLights.Length; i++)
        {
            if (lampLights[i] != null) lampLights[i].enabled = lampEnabled[i];
        }
        if (lamp != null) lamp.SetActive(lampWasActive);
        lamp = null;
        lampLights = new Light[0];
    }

    /// <summary>
    /// 화면 UI(조작 안내·시계·조준선 등 화면에 붙는 캔버스)를 숨긴다. 태블릿 화면은 월드 공간이라 그대로 보인다.
    /// 끈 것만 기억했다가 복구 때 다시 켠다.
    /// </summary>
    private void HideScreenUi()
    {
        hiddenCanvases.Clear();
        foreach (Canvas c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (!c.isRootCanvas || !c.enabled || c.renderMode == RenderMode.WorldSpace) continue;
            if (c.transform.IsChildOf(transform)) continue;   // 이 컷신의 먹물·어둠
            c.enabled = false;
            hiddenCanvases.Add(c);
        }
    }

    private void ShowScreenUi()
    {
        foreach (Canvas c in hiddenCanvases) if (c != null) c.enabled = true;
        hiddenCanvases.Clear();
    }

    private static void HideCursor()
    {
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }

    /// <summary>발밑 바닥 — 눈에서 아래로 레이(플레이어 캡슐은 시작점이 안이라 맞지 않는다), 못 찾으면 캡슐 바닥.</summary>
    private Vector3 FindFeet(FPController fp)
    {
        RaycastHit hit;
        if (Physics.Raycast(cam.transform.position, Vector3.down, out hit, 4f, ~0, QueryTriggerInteraction.Ignore))
        {
            return new Vector3(player.position.x, hit.point.y, player.position.z);
        }
        CapsuleCollider capsule = fp.GetComponent<CapsuleCollider>();
        if (capsule == null) return player.position;
        return player.TransformPoint(capsule.center) - Vector3.up * capsule.height * 0.5f * player.lossyScale.y;
    }

    // ─────────────────────────────── 길 ───────────────────────────────

    /// <summary>
    /// 금지 반경 밖·탐색 범위 안의 자리들을 담는 구역만 충돌체로 NavMesh를 굽고, 길이 끝까지 이어지는 가장 짧은 자리를 고른다.
    /// 씬에 NavMesh를 미리 구워 둘 필요가 없다(씬 파일을 고치지 않는다).
    /// </summary>
    private LayoutDeathSpot ChooseSpot(Vector3 feet, out List<Vector3> bestPath)
    {
        bestPath = null;
        var candidates = new List<LayoutDeathSpot>();
        int blocked = 0;
        foreach (LayoutDeathSpot s in LayoutDeathSpot.All)
        {
            float d = Flat(s.transform.position - feet).magnitude;
            if (d < blockRadius) { blocked++; continue; }
            if (d > searchRange) continue;
            candidates.Add(s);
        }
        if (candidates.Count == 0)
        {
            LastRefusal = blocked > 0 ? "플레이어가 사람 나무 자리의 금지 반경(" + blockRadius + "m) 안에 있습니다." : "탐색 범위(" + searchRange + "m) 안에 사람 나무 자리가 없습니다.";
            Debug.Log("[LayoutDeathCutscene] 재생 안 함 — " + LastRefusal);
            return null;
        }

        var bounds = new Bounds(feet, Vector3.zero);
        foreach (LayoutDeathSpot s in candidates) bounds.Encapsulate(s.transform.position);
        bounds.Expand(new Vector3(16f, 0f, 16f));
        bounds.SetMinMax(new Vector3(bounds.min.x, Mathf.Min(feet.y, bounds.min.y) - 1.5f, bounds.min.z),
                         new Vector3(bounds.max.x, Mathf.Max(feet.y, bounds.max.y) + 3.5f, bounds.max.z));

        var watch = System.Diagnostics.Stopwatch.StartNew();
        if (!BuildNavMesh(bounds))
        {
            LastRefusal = "NavMesh를 만들지 못했습니다.";
            Debug.LogWarning("[LayoutDeathCutscene] " + LastRefusal);
            return null;
        }

        var filter = new NavMeshQueryFilter { agentTypeID = agentTypeId, areaMask = NavMesh.AllAreas };
        NavMeshHit from;
        if (!NavMesh.SamplePosition(feet, out from, 1.5f, filter))
        {
            LastRefusal = "플레이어 발밑에서 걸을 수 있는 바닥을 찾지 못했습니다.";
            Debug.LogWarning("[LayoutDeathCutscene] " + LastRefusal);
            return null;
        }

        LayoutDeathSpot best = null;
        float bestLength = float.MaxValue;
        var navPath = new NavMeshPath();
        foreach (LayoutDeathSpot s in candidates)
        {
            NavMeshHit to;
            if (!NavMesh.SamplePosition(s.transform.position, out to, 2f, filter)) continue;
            if (!NavMesh.CalculatePath(from.position, to.position, filter, navPath)) continue;
            if (navPath.status != NavMeshPathStatus.PathComplete) continue;
            float length = PathLength(navPath.corners);
            if (length > maxPathLength || length >= bestLength) continue;
            best = s;
            bestLength = length;
            bestPath = new List<Vector3>(navPath.corners);
        }
        Debug.Log("[LayoutDeathCutscene] NavMesh " + watch.ElapsedMilliseconds + "ms · 후보 " + candidates.Count + " · 고른 자리 " +
                  (best != null ? best.name + " (길 " + bestLength.ToString("0.0") + "m, 꺾임 " + bestPath.Count + ")" : "없음"));
        if (best == null)
        {
            LastRefusal = "사람 나무 자리까지 걸어갈 길이 없습니다(문이 닫혔거나 너무 멉니다).";
        }
        return best;
    }

    private bool BuildNavMesh(Bounds bounds)
    {
        var sources = new List<NavMeshBuildSource>();
        NavMeshBuilder.CollectSources(bounds, ~0, NavMeshCollectGeometry.PhysicsColliders, 0, new List<NavMeshBuildMarkup>(), sources);
        sources.RemoveAll(s =>
        {
            Component comp = s.component;
            if (comp == null) return false;
            if (comp.transform.IsChildOf(player)) return true;               // 플레이어 자신
            Collider col = comp as Collider;
            return col != null && (col.isTrigger || !col.enabled);            // 문 앞 감지 상자 등
        });

        NavMeshBuildSettings settings = NavMesh.CreateSettings();
        settings.agentRadius = 0.28f;
        settings.agentHeight = 1.6f;
        settings.agentClimb = 0.3f;
        settings.agentSlope = 40f;
        settings.overrideVoxelSize = true;
        settings.voxelSize = 0.09f;
        agentTypeId = settings.agentTypeID;

        NavMeshData data = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);
        if (data == null) return false;
        navInstance = NavMesh.AddNavMeshData(data);
        return navInstance.valid;
    }

    private void RemoveNavMesh()
    {
        if (navInstance.valid) NavMesh.RemoveNavMeshData(navInstance);
        navInstance = default(NavMeshDataInstance);
        if (agentTypeId != -1) NavMesh.RemoveSettings(agentTypeId);
        agentTypeId = -1;
    }

    private static float PathLength(IList<Vector3> pts)
    {
        float sum = 0f;
        for (int i = 1; i < pts.Count; i++) sum += Vector3.Distance(pts[i - 1], pts[i]);
        return sum;
    }

    private static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v;
    }

    // ─────────────────────────────── 무대 ───────────────────────────────

    private void SpawnTree(LayoutDeathSpot spot, List<Vector3> path)
    {
        treeBase = spot.transform.position;
        Vector3 face = spot.transform.forward;
        if (spot.faceArrival && path.Count >= 2)
        {
            // 마지막 꺾임에서 나무 쪽으로 오는 방향의 반대 = 다가오는 플레이어를 본다
            Vector3 arrive = Flat(path[path.Count - 1] - path[Mathf.Max(0, path.Count - 2)]);
            if (arrive.sqrMagnitude > 0.01f) face = -arrive.normalized;
        }
        face = Flat(face);
        if (face.sqrMagnitude < 0.01f) face = Vector3.forward;

        if (treePrefab == null)
        {
            Debug.LogWarning("[LayoutDeathCutscene] 사람 나무 프리팹이 비어 있습니다 — 나무 없이 진행합니다.");
            return;
        }
        tree = NewTree(treeBase, Quaternion.LookRotation(face.normalized, Vector3.up), "LayoutDeath HumanTree", true);
    }

    /// <summary>
    /// 사람 나무 하나를 세운다. 흔들림(HorrorTreeSway)은 프리팹 그대로 쓰고, 바라보면 화면이 물드는 효과(HorrorGazeHold)는
    /// <paramref name="keepGaze"/>일 때만 남긴다(나무가 여럿이면 서로 같은 화면 효과를 다툰다).
    /// </summary>
    private GameObject NewTree(Vector3 pos, Quaternion rot, string name, bool keepGaze)
    {
        GameObject t = Instantiate(treePrefab, pos, rot);
        t.name = name;
        // 같은 볼륨을 Timeline으로 덮어쓰는 응시 트리거·이벤트는 끈다(둘이 다투면 세기가 튄다 — 2026-09-29).
        foreach (HorrorGazeTrigger g in t.GetComponentsInChildren<HorrorGazeTrigger>(true)) g.enabled = false;
        foreach (HorrorEvent e in t.GetComponentsInChildren<HorrorEvent>(true)) e.enabled = false;
        foreach (Collider col in t.GetComponentsInChildren<Collider>(true)) col.enabled = false;
        if (!keepGaze)
        {
            foreach (HorrorGazeHold h in t.GetComponentsInChildren<HorrorGazeHold>(true)) h.enabled = false;
            // 여럿 세울 때 같은 동작을 맞춰 하면 인형처럼 보인다 — 애니메이션 시작 지점을 흩는다(0 = 지금 상태의 시간만 바꿈)
            foreach (Animator a in t.GetComponentsInChildren<Animator>())
            {
                if (a.runtimeAnimatorController != null) a.Play(0, 0, UnityEngine.Random.value);   // Timeline만 쓰는 Animator(컨트롤러 없음)는 건너뜀
            }
        }
        return t;
    }

    /// <summary>소리 소스·화면 일렁임·먹물 캔버스 — 프리팹을 단순하게 두려고 재생할 때 만든다(처음 한 번).</summary>
    private void BuildRuntimeParts()
    {
        if (stepSource == null)
        {
            var go = new GameObject("Steps");
            go.transform.SetParent(transform, false);
            stepSource = NewSource(go);
            // 에코(되울림)와 잔향 — 둘 다 걸어갈수록 길어진다
            stepEcho = go.AddComponent<AudioEchoFilter>();
            stepEcho.delay = echoDelayMs;   // 지연은 고정 — 바꾸면 지연 버퍼가 다시 잡혀 딸깍거린다
            stepEcho.dryMix = 1f;
            stepReverb = go.AddComponent<AudioReverbFilter>();
            stepReverb.reverbPreset = AudioReverbPreset.User;
            stepReverb.dryLevel = 0f;
            // 소스가 멈추면 필터도 멈춰 에코·잔향 꼬리가 뚝 잘린다(발소리 한 번 = 0.5초) — 무음 루프로 소스를 계속 돌린다
            stepSource.clip = SilentClip();
            stepSource.loop = true;
        }
        SetEcho(0f);
        stepSource.volume = 1f;
        stepSource.Play();

        if (wobble == null)
        {
            var go = new GameObject("Wobble");
            go.transform.SetParent(transform, false);
            go.SetActive(false);
            wobble = go.AddComponent<ScreenWobbleSource>();
            wobble.weight = 0f;
        }
        wobble.weight = 0f;
        wobble.gameObject.SetActive(true);
        if (FindAnyObjectByType<ScreenWobble>() == null) Debug.LogWarning("[LayoutDeathCutscene] 씬에 ScreenWobble이 없어 화면 일렁임이 보이지 않습니다(PlaySystems에 있어야 함).");

        if (inkCanvas == null)
        {
            var go = new GameObject("InkCanvas");
            go.transform.SetParent(transform, false);
            inkCanvas = go.AddComponent<Canvas>();
            inkCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            inkCanvas.sortingOrder = 1001;   // HUD까지 덮는다
            Texture2D[] tex = InkTextures();
            for (int i = 0; i < 16; i++)
            {
                var b = NewImage(go.transform, "Blot " + i, tex[i % tex.Length]);
                blots.Add(b);
            }
            inkFull = NewImage(go.transform, "Black", Texture2D.whiteTexture);
            Stretch(inkFull.rectTransform);

            // 다가갈수록 어두워짐 — 먹물보다 아래(먼저 그림)
            darkOverall = NewImage(go.transform, "Darken", Texture2D.whiteTexture);
            Stretch(darkOverall.rectTransform);
            darkOverall.transform.SetAsFirstSibling();
            darkVignette = NewImage(go.transform, "Vignette", VignetteTexture());
            Stretch(darkVignette.rectTransform);
            darkVignette.transform.SetSiblingIndex(1);
        }
        inkCanvas.gameObject.SetActive(true);
        foreach (RawImage b in blots) b.enabled = false;
        SetAlpha(inkFull, 0f);
        SetAlpha(darkOverall, 0f);
        SetAlpha(darkVignette, 0f);
    }

    private static void Stretch(RectTransform r)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = Vector2.zero;
        r.offsetMax = Vector2.zero;
    }

    /// <summary>나무까지 거리로 어둠을 짙게 한다 — darkenFrom에서 0, 끌려 드는 거리에서 최대(가장자리부터 조여 옴).</summary>
    private void UpdateDarken(float treeDistance)
    {
        float k = 1f - Edge(pullDistance, Mathf.Max(pullDistance + 0.1f, darkenFrom), treeDistance);
        SetAlpha(darkVignette, Mathf.Max(baseVignette, k * darkenVignette));   // ver2는 이미 어두워진 가장자리에서 시작
        SetAlpha(darkOverall, k * k * darkenOverall);
    }

    private static Texture2D vignetteTexture;

    /// <summary>가운데는 투명, 가장자리로 갈수록 검은 비네트 — 코드로 한 번 만든다.</summary>
    private static Texture2D VignetteTexture()
    {
        if (vignetteTexture != null) return vignetteTexture;
        const int N = 128;
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "LayoutVignette" };
        var px = new Color32[N * N];
        for (int y = 0; y < N; y++)
        {
            for (int x = 0; x < N; x++)
            {
                float u = (x + 0.5f) / N - 0.5f, v = (y + 0.5f) / N - 0.5f;
                float r = Mathf.Sqrt(u * u + v * v) / 0.7071f;   // 모서리 = 1
                px[y * N + x] = new Color32(255, 255, 255, (byte)(Edge(0.25f, 0.95f, r) * 255f));
            }
        }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        vignetteTexture = tex;
        return tex;
    }

    private static RawImage NewImage(Transform parent, string name, Texture tex)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<RawImage>();
        img.texture = tex;
        img.color = Color.black;
        img.raycastTarget = false;
        img.enabled = false;
        return img;
    }

    private static void SetAlpha(Graphic g, float a)
    {
        Color c = g.color;
        c.a = a;
        g.color = c;
        g.enabled = a > 0.001f;
    }

    private AudioSource NewSource(GameObject go)
    {
        var src = go.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.spatialBlend = 0f;
        src.ignoreListenerPause = true;   // 다른 소리를 끊은 뒤에도 컷신 소리는 들린다
        return src;
    }

    private AudioSource PlayHit(Hit h, Vector3? at = null)
    {
        if (h == null || h.clip == null) return null;
        var go = new GameObject("Hit " + h.clip.name);
        go.transform.SetParent(transform, false);
        AudioSource src = NewSource(go);
        src.clip = h.clip;
        src.volume = h.volume;
        if (at.HasValue)
        {
            go.transform.position = at.Value;
            src.spatialBlend = 1f;
            src.minDistance = 1.5f;
            src.maxDistance = 20f;
        }
        src.time = Mathf.Clamp(h.clipStart, 0f, Mathf.Max(0f, h.clip.length - 0.05f));
        if (h.delay > 0f) src.PlayDelayed(h.delay);
        else src.Play();
        hitSources.Add(src);
        return src;
    }

    // ─────────────────────────────── 진행 ───────────────────────────────

    /// <summary>굳음 → 걷기 → 끌려 들어감 → 먹물 → 끝. ver2는 굳음 없이, 내려다본 고개(<paramref name="startPitch"/>)에서 이어 걷는다.</summary>
    private IEnumerator Run(List<Vector3> path, Vector3 startFeet, float freeze, float startPitch)
    {
        Vector3 bodyOffset = player.position - startFeet;   // 발밑 → 플레이어 루트
        float totalLength = PathLength(path);
        float walkLength = Mathf.Max(0.1f, totalLength - pullDistance);

        // 처음 카메라 → 연출 카메라로 섞어 들어감
        blendFromPos = cam.transform.position;
        blendFromRot = cam.transform.rotation;
        blendIn = 0f;
        camDriven = true;
        camFovNow = camFov;

        float yaw = cam.transform.eulerAngles.y;
        float pitch = startPitch;
        Vector3 eyeLocal = Vector3.up * (cam.transform.position.y - startFeet.y);

        StartLayers();
        Phase = "Freeze";
        float t = 0f;

        // ① 굳음 — 소리가 끊긴 채 잠깐
        Vector3 feet = path[0];
        while (t < freeze)
        {
            t += Time.deltaTime;
            SetCamera(feet + bodyOffset, yaw, pitch, 0f, 0f);
            yield return null;
        }

        // ② 갑자기 비틀 — 걷기 시작
        Phase = "Walk";
        foreach (Hit h in startHits) PlayHit(h);
        // 비틀거림은 눕힌 8자(∞)로 — 시선이 좌우로 크게, 위아래로 그 두 배 빠르게 오가는 리사주 곡선.
        // 예전엔 이따금 한 프레임에 롤을 확 꺾고(쏠림) 걸음마다 |sin|으로 까딱여 꼭짓점에서 뚜둑 끊겼다(2026-10-05 사용자 지적).
        float swayPhase = 0f;
        float swayIn = 0f;   // 비틀거림이 0에서 차오름(시작할 때 툭 튀지 않게)
        float pullRollSign = 1f;   // 끌려 들 때 기우는 쪽 = 마지막으로 흔들리던 쪽
        float towardTree = 0f;     // 시선이 나무로 끌려간 정도(0 = 앞길)
        float yawVel = 0f, pitchVel = 0f;

        float walked = 0f;
        float stepAcc = 0f;
        int seg = 1;
        bool suckStarted = false;

        while (walked < walkLength)
        {
            float dt = Time.deltaTime;
            float p = Mathf.Clamp01(walked / walkLength);

            // 비틀거림 위상 — 8자 한 바퀴(swayPeriod초)에 걸음이 늦췄다 쏠렸다를 두 번
            swayIn = Mathf.MoveTowards(swayIn, 1f, dt / 0.8f);
            swayPhase += dt * Mathf.PI * 2f / swayPeriod;
            float sx = Mathf.Sin(swayPhase);                 // 좌우(한 바퀴에 한 번)
            float sy = Mathf.Sin(swayPhase * 2f) * 0.5f;     // 위아래(한 바퀴에 두 번) — 합치면 눕힌 8자
            float sway = swayIn * Mathf.Lerp(1f, 1.5f, p);   // 다가갈수록 크게

            // 속도 — 좌우 끝에서 늦추고 가운데를 지날 때 쏠린다(매끈한 코사인)
            float gait = 0.7f + 0.3f * Mathf.Cos(swayPhase * 2f);
            float speed = walkSpeed * gait;
            float move = speed * dt;
            walked += move;
            stepAcc += move;

            // 길 위 위치
            while (seg < path.Count && move > 0f)
            {
                float left = Vector3.Distance(feet, path[seg]);
                if (move < left) { feet = Vector3.MoveTowards(feet, path[seg], move); move = 0f; }
                else { feet = path[seg]; move -= left; seg++; }
            }

            // 바라볼 곳 — 앞길, 나무가 가까우면 나무
            Vector3 ahead = LookAhead(path, feet, seg, 1.4f);
            Vector3 treeLook = treeBase + Vector3.up * lookHeight;
            float treeDist = Flat(treeBase - feet).magnitude;
            // 가림 판정은 프레임마다 켜졌다 꺼질 수 있어 바로 쓰면 시선 목표가 툭 바뀐다 — 천천히 따라가게
            float wantToward = Mathf.InverseLerp(9f, 5f, treeDist) * (HasLineOfSight(feet + eyeLocal, treeLook) ? 1f : 0.3f);
            towardTree = Mathf.MoveTowards(towardTree, wantToward, dt * 0.8f);
            Vector3 eye = feet + eyeLocal;
            Vector3 dirPath = (ahead + Vector3.up * (eyeLocal.y - 0.15f)) - eye;
            Vector3 dirTree = treeLook - eye;
            Vector3 dir = Vector3.Slerp(dirPath.normalized, dirTree.normalized, towardTree);
            float wantYaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            float wantPitch = -Mathf.Asin(Mathf.Clamp(dir.normalized.y, -1f, 1f)) * Mathf.Rad2Deg;
            // 몸이 향하는 방향은 천천히 따라가고, 8자 흔들림은 그 위에 얹는다(누적되지 않게 따로 더함)
            // SmoothDamp — 목표가 바뀌어도(길 꺾임) 회전 속도가 이어져 꺾이지 않는다. Lerp는 목표가 바뀌는 순간 속도가 툭 바뀌었다
            yaw = Mathf.SmoothDampAngle(yaw, wantYaw, ref yawVel, 0.55f);
            pitch = Mathf.SmoothDamp(pitch, wantPitch + 4f * (1f - towardTree), ref pitchVel, 0.6f);
            float swayYaw = sx * swayYawDeg * sway;
            float swayPitch = sy * swayPitchDeg * sway;
            float roll = sx * staggerRoll * sway;
            pullRollSign = sx >= 0f ? 1f : -1f;

            // 걸음 — 발소리, 고개 까딱(매끈한 코사인 — 바닥을 디딜 때 가장 낮음)
            if (stepAcc >= stepLength)
            {
                stepAcc -= stepLength;
                Step(p);
            }
            float bob = (Mathf.Cos(walked / stepLength * Mathf.PI * 2f) - 1f) * 0.5f * 0.03f;

            SetCamera(feet + bodyOffset, yaw, pitch, roll, bob, swayYaw, swayPitch);
            UpdateDarken(treeDist);
            UpdateLayers(p);
            SetEcho(p);
            if (wobble != null) wobble.weight = Mathf.Max(baseWobble, Mathf.Pow(p, 1.3f) * wobbleMax);
            camFovNow = Mathf.Lerp(camFov, approachFov, Mathf.SmoothStep(0f, 1f, p));

            // 끌려 드는 순간에 빨려 듦의 꼭대기가 오게 미리 건다(남은 거리 ÷ 지금 속도로 어림)
            if (!suckStarted && suckIn != null && suckIn.clip != null)
            {
                float remain = walkLength - walked;
                if (remain / Mathf.Max(0.3f, walkSpeed * 0.8f) <= suckInPeak)
                {
                    PlayHit(suckIn);
                    suckStarted = true;
                }
            }
            yield return null;
        }

        // ③ 확 끌려 들어감 — 걷던 소리는 뚝. 화면이 일그러지는 이 순간부터 태블릿(손)도 치운다
        Phase = "Pull";
        if (viewmodel != null) viewmodel.SetActive(false);
        stepSource.Stop();
        foreach (AudioSource s in layerSources) if (s != null) s.Stop();
        StopBloodRain();   // ver2 — 피 비·물소리·웅성거림도 뚝
        foreach (Hit h in pullHits) PlayHit(h);

        Vector3 fromPos = cam.transform.position;
        Quaternion fromRot = cam.transform.rotation;
        Vector3 into = treeBase + Vector3.up * branchHeight;
        Vector3 toCam = Flat(fromPos - into).normalized;
        Vector3 endPos = into + toCam * 0.12f;   // 가지 사이 바로 안쪽
        Quaternion endRot = Quaternion.LookRotation((into - toCam * 0.5f) - endPos, Vector3.up) * Quaternion.Euler(0f, 0f, pullRollSign * 24f);
        float fov0 = camFovNow;
        float k = 0f;
        StartInk();
        while (k < 1f)
        {
            k = Mathf.Min(1f, k + Time.deltaTime / pullSeconds);
            float e = k * k * k;   // 확 — 처음엔 느리다가 빨라짐
            camPos = Vector3.Lerp(fromPos, endPos, e);
            camRot = Quaternion.Slerp(fromRot, endRot, Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, k * 1.6f)));
            camFovNow = Mathf.Lerp(fov0, pullFov, e);
            if (wobble != null) wobble.weight = Mathf.Lerp(wobbleMax, 1f, k);
            UpdateInk(k * pullSeconds);
            yield return null;
        }

        // ④ 먹물이 마저 덮음
        Phase = "Ink";
        float inkT = pullSeconds;
        while (inkT < blotDelay + blotSeconds + 0.05f)
        {
            inkT += Time.deltaTime;
            UpdateInk(inkT);
            yield return null;
        }
        SetAlpha(inkFull, 1f);
        if (wobble != null) wobble.weight = 0f;
        Phase = "Black";
        foreach (Hit h in endHits) PlayHit(h);

        float hold = 0f;
        while (hold < blackHold)
        {
            hold += Time.deltaTime;
            yield return null;
        }

        running = null;
        EndCutscene(restoreOnEnd);
        if (Finished != null) Finished();
    }

    private static Vector3 LookAhead(List<Vector3> path, Vector3 feet, int seg, float distance)
    {
        Vector3 p = feet;
        for (int i = seg; i < path.Count; i++)
        {
            float d = Vector3.Distance(p, path[i]);
            if (d >= distance) return Vector3.MoveTowards(p, path[i], distance);
            distance -= d;
            p = path[i];
        }
        return path[path.Count - 1] + (path.Count >= 2 ? (path[path.Count - 1] - path[path.Count - 2]).normalized * distance : Vector3.zero);
    }

    private bool HasLineOfSight(Vector3 from, Vector3 to)
    {
        RaycastHit hit;
        if (!Physics.Linecast(from, to, out hit, ~0, QueryTriggerInteraction.Ignore)) return true;
        return hit.transform.IsChildOf(player) || (tree != null && hit.transform.IsChildOf(tree.transform));
    }

    /// <summary>
    /// 몸을 옮기고(좌우 방향만 돌림) 카메라 목표를 정한다. 눈 위치 = 처음 카메라의 부모 기준 로컬 위치 + 고개 까딱임.
    /// 실제로 카메라에 붙이는 것은 <see cref="LateUpdate"/>.
    /// </summary>
    private void SetCamera(Vector3 bodyPos, float yaw, float pitch, float roll, float bob, float swayYaw = 0f, float swayPitch = 0f)
    {
        player.SetPositionAndRotation(bodyPos, Quaternion.Euler(0f, yaw, 0f));
        Transform parent = cam.transform.parent;
        camPos = (parent != null ? parent.TransformPoint(camLocalPos) : bodyPos) + Vector3.up * bob;
        camRot = Quaternion.Euler(pitch + swayPitch, yaw + swayYaw, roll);
    }

    private void LateUpdate()
    {
        if (!IsPlaying || !camDriven || cam == null) return;
        if (Cursor.visible) HideCursor();

        blendIn += Time.deltaTime;
        float w = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(blendIn / 0.35f));
        cam.transform.SetPositionAndRotation(Vector3.Lerp(blendFromPos, camPos, w), Quaternion.Slerp(blendFromRot, camRot, w));
        cam.fieldOfView = camFovNow;
    }

    // ─────────────────────────────── 소리 ───────────────────────────────

    private void Step(float p)
    {
        if (stepClips.Length == 0) return;
        AudioClip clip = stepClips[UnityEngine.Random.Range(0, stepClips.Length)];
        // 음높이는 바꾸지 않는다 — 소스 pitch를 걸음마다 바꾸면 아직 울리는 앞 걸음 꼬리까지 음이 흔들렸다. 세기만 조금씩 다르게
        stepSource.PlayOneShot(clip, stepVolume * UnityEngine.Random.Range(0.85f, 1f));
    }

    /// <summary>
    /// 에코가 점점 길어진다 — 매 프레임 진행도로 「남는 정도·젖은 비율·잔향 길이」만 조금씩 바꾼다(지연은 고정).
    /// 걸음마다 한 번에 바꾸면 울리던 꼬리가 툭 달라졌다.
    /// </summary>
    private void SetEcho(float p)
    {
        if (stepEcho == null) return;
        stepEcho.decayRatio = Mathf.Lerp(0.2f, 0.62f, p);
        stepEcho.wetMix = Mathf.Lerp(0.25f, 0.8f, p);
        stepReverb.room = Mathf.Lerp(-1800f, -250f, p);
        stepReverb.decayTime = Mathf.Lerp(0.8f, 5f, p);
        stepReverb.reverbLevel = Mathf.Lerp(-800f, 100f, p);
    }

    private static AudioClip silentClip;

    private static AudioClip SilentClip()
    {
        if (silentClip == null) silentClip = AudioClip.Create("LayoutSilence", 48000, 1, 48000, false);   // 1초, 0으로 채워짐
        return silentClip;
    }

    private void StartLayers()
    {
        foreach (AudioSource s in layerSources) if (s != null) Destroy(s.gameObject);
        layerSources.Clear();
        layerOf.Clear();
        // 겹을 여럿 낼 때는 같은 DSP 시각에 걸어 샘플까지 맞춘다 — 어긋나면 두 번 들리거나 소리가 상쇄된다
        double startAt = AudioSettings.dspTime + 0.06;
        for (int i = 0; i < layers.Length; i++)
        {
            Layer l = layers[i];
            if (l == null || l.clip == null) continue;
            for (int c = 0; c < Mathf.Max(1, l.copies); c++)
            {
                var go = new GameObject("Layer " + l.clip.name + (c > 0 ? " +" + c : ""));
                go.transform.SetParent(transform, false);
                AudioSource src = NewSource(go);
                src.clip = l.clip;
                src.loop = true;
                src.volume = 0f;
                if (l.atTree)
                {
                    go.transform.position = treeBase + Vector3.up * lookHeight;
                    src.spatialBlend = 1f;
                    src.minDistance = 1.5f;
                    src.maxDistance = 22f;
                    src.rolloffMode = AudioRolloffMode.Logarithmic;
                }
                src.time = Mathf.Clamp(l.clipStart, 0f, Mathf.Max(0f, l.clip.length - 0.05f));
                src.PlayScheduled(startAt);
                layerSources.Add(src);
                layerOf.Add(i);
            }
        }
    }

    private void UpdateLayers(float p)
    {
        for (int j = 0; j < layerSources.Count; j++)
        {
            AudioSource src = layerSources[j];
            if (src == null) continue;
            Layer l = layers[layerOf[j]];
            float k = l.fullProgress <= l.fromProgress ? (p >= l.fromProgress ? 1f : 0f) : Mathf.Clamp01((p - l.fromProgress) / (l.fullProgress - l.fromProgress));
            float o = l.fadeOutFrom > 1f ? 0f : l.fadeOutTo <= l.fadeOutFrom ? (p >= l.fadeOutFrom ? 1f : 0f) : Mathf.Clamp01((p - l.fadeOutFrom) / (l.fadeOutTo - l.fadeOutFrom));
            src.volume = l.volume * k * k * (1f - o * o);
        }
    }

    // ─────────────────────────────── 먹물 ───────────────────────────────

    private float[] blotStart;
    private Vector2[] blotSize;

    private void StartInk()
    {
        Vector2 screen = inkCanvas.pixelRect.size;
        if (screen.x < 1f) screen = new Vector2(Screen.width, Screen.height);
        float diag = screen.magnitude;
        blotStart = new float[blots.Count];
        blotSize = new Vector2[blots.Count];
        for (int i = 0; i < blots.Count; i++)
        {
            RectTransform r = blots[i].rectTransform;
            r.anchorMin = r.anchorMax = Vector2.zero;
            // 가장자리부터 번져 들어오게 — 앞쪽 얼룩은 화면 가장자리, 뒤쪽일수록 가운데
            float edge = 1f - (float)i / blots.Count;
            Vector2 c = new Vector2(0.5f, 0.5f) + UnityEngine.Random.insideUnitCircle.normalized * 0.55f * edge + UnityEngine.Random.insideUnitCircle * 0.2f;
            r.anchoredPosition = new Vector2(c.x * screen.x, c.y * screen.y);
            r.localRotation = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(0f, 360f));
            float size = diag * UnityEngine.Random.Range(0.35f, 0.7f);
            blotSize[i] = new Vector2(size, size * UnityEngine.Random.Range(0.75f, 1.25f));
            r.sizeDelta = Vector2.zero;
            blotStart[i] = blotDelay + blotSeconds * 0.65f * ((float)i / blots.Count) + UnityEngine.Random.Range(0f, 0.04f);
            blots[i].enabled = false;
        }
    }

    private void UpdateInk(float t)
    {
        if (blotStart == null) return;
        for (int i = 0; i < blots.Count; i++)
        {
            float k = Mathf.Clamp01((t - blotStart[i]) / 0.16f);
            if (k <= 0f) continue;
            float e = 1f - (1f - k) * (1f - k);   // 확 번졌다가 멈춤
            blots[i].enabled = true;
            SetAlpha(blots[i], 1f);
            blots[i].rectTransform.sizeDelta = blotSize[i] * e;
        }
        float full = Mathf.Clamp01((t - (blotDelay + blotSeconds * 0.75f)) / (blotSeconds * 0.35f));
        SetAlpha(inkFull, full);
    }

    private static Texture2D[] inkTextures;

    /// <summary>셰이더의 smoothstep — x가 a 이하면 0, b 이상이면 1. (Mathf.SmoothStep은 「from~to 사이를 t로 보간」이라 다르다 — 그걸 쓰면 얼룩이 네모가 됐다.)</summary>
    private static float Edge(float a, float b, float x)
    {
        float t = Mathf.Clamp01((x - a) / (b - a));
        return t * t * (3f - 2f * t);
    }

    /// <summary>먹물 얼룩 텍스처(가장자리가 들쭉날쭉한 덩어리 + 흩뿌린 방울) — 코드로 한 번 만든다.</summary>
    private static Texture2D[] InkTextures()
    {
        if (inkTextures != null && inkTextures[0] != null) return inkTextures;
        inkTextures = new Texture2D[3];
        for (int v = 0; v < inkTextures.Length; v++)
        {
            const int N = 192;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "InkBlot" + v };
            var rng = new System.Random(31 + v * 17);
            float ph1 = (float)rng.NextDouble() * 6.28f, ph2 = (float)rng.NextDouble() * 6.28f, ph3 = (float)rng.NextDouble() * 6.28f;
            var drops = new Vector3[10];
            for (int d = 0; d < drops.Length; d++)
            {
                float a = (float)rng.NextDouble() * 6.28f, r = 0.36f + (float)rng.NextDouble() * 0.12f;
                drops[d] = new Vector3(0.5f + Mathf.Cos(a) * r, 0.5f + Mathf.Sin(a) * r, 0.015f + (float)rng.NextDouble() * 0.035f);
            }
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++)
            {
                for (int x = 0; x < N; x++)
                {
                    float u = (x + 0.5f) / N, w = (y + 0.5f) / N;
                    float dx = u - 0.5f, dy = w - 0.5f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Atan2(dy, dx);
                    float edge = 0.30f + 0.06f * Mathf.Sin(3f * a + ph1) + 0.035f * Mathf.Sin(7f * a + ph2) + 0.02f * Mathf.Sin(15f * a + ph3);
                    float alpha = 1f - Edge(edge - 0.015f, edge + 0.01f, r);
                    foreach (Vector3 d in drops)
                    {
                        float dd = Mathf.Sqrt((u - d.x) * (u - d.x) + (w - d.y) * (w - d.y));
                        alpha = Mathf.Max(alpha, 1f - Edge(d.z - 0.006f, d.z + 0.004f, dd));
                    }
                    byte ab = (byte)(Mathf.Clamp01(alpha) * 255f);
                    px[y * N + x] = new Color32(255, 255, 255, ab);
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            inkTextures[v] = tex;
        }
        return inkTextures;
    }

    // ─────────────────────────────── 끝 ───────────────────────────────

    /// <summary>재생 중이면 멈추고 원래대로 돌린다(디버그용).</summary>
    public void StopAndRestore()
    {
        if (!IsPlaying) return;
        if (running != null) StopCoroutine(running);
        running = null;
        EndCutscene(true);
    }

    private void EndCutscene(bool restore)
    {
        Playing = null;
        camDriven = false;
        RemoveNavMesh();
        if (!restore) return;   // 사망: 검은 화면을 사망 화면이 이어받는다

        if (tree != null) Destroy(tree);
        tree = null;
        CleanupBloodRain();
        foreach (AudioSource s in layerSources) if (s != null) Destroy(s.gameObject);
        layerSources.Clear();
        layerOf.Clear();
        foreach (AudioSource s in hitSources) if (s != null) Destroy(s.gameObject);
        hitSources.Clear();
        if (stepSource != null) stepSource.Stop();
        if (wobble != null)
        {
            wobble.weight = 0f;
            wobble.gameObject.SetActive(false);
        }
        if (inkCanvas != null) inkCanvas.gameObject.SetActive(false);

        if (player != null) player.SetPositionAndRotation(playerPos, playerRot);
        if (cam != null)
        {
            cam.transform.localPosition = camLocalPos;
            cam.transform.localRotation = camLocalRot;
            cam.fieldOfView = camFov;
        }
        ReleaseFlashlight();
        ShowScreenUi();
        if (body != null) body.isKinematic = bodyWasKinematic;
        if (controller != null) controller.enabled = true;
        Cursor.visible = false;
        Cursor.lockState = cursorWasLock;
        if (viewmodel != null) viewmodel.SetActive(viewmodelWasActive);
        if (pausedAudio && !GamePause.IsPaused) AudioListener.pause = false;
        pausedAudio = false;
    }

    private void OnDestroy()
    {
        if (IsPlaying) EndCutscene(true);
        RemoveNavMesh();
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private void Update()
    {
        if (Time.frameCount == startedFrame) return;
        if (useDebugKey && debugKey != KeyCode.None && Input.GetKeyDown(debugKey))
        {
            if (IsPlaying) StopAndRestore();
            else Play(true);
        }
    }
#endif
}

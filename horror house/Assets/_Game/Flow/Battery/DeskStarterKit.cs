using NightDuty;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 66차(민: 「책상 위 배터리는 1일차에만, 손전등도 같이 책상에 놓고, 손전등을 먹으면 그때부터 F로 켤 수 있도록」) — 경비실 책상 위 시작 물품의 화면 쪽.
/// 상태는 코어 <see cref="NightRun.DeskKit"/>(스냅샷에 실린다).
/// <list type="bullet">
/// <item>1일차 밤: CCTV 모니터 옆 교탁(<c>TeacherTable02_static</c>) 위에 손전등(씬의 손전등 소품 복제)과 예비 배터리 하나.</item>
/// <item><see cref="Reach"/> 안에서 겨누면 외곽선과 「[E] 손전등 줍기」·「[E] 배터리 줍기」. 3m 안이면 손전등에 옅은 외곽선(어디 있는지 보이게).</item>
/// <item>손전등을 줍기 전에는 F가 듣지 않는다(<see cref="FlashlightPower.CanTurnOn"/>) — 누르면 「손전등이 없습니다」 안내. 주우면 「[F] 손전등 켜기」 안내.</item>
/// </list>
/// 근무 씬에 스스로 선다. 씬은 고치지 않는다.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(40)]   // PlayerInteractor(50)보다 먼저 — 겨눈 프레임에 문 조작을 막는다
public sealed class DeskStarterKit : MonoBehaviour
{
    /// <summary>손이 닿는 거리(m).</summary>
    public const float Reach = 2.1f;

    /// <summary>손전등 옅은 외곽선이 보이는 거리(m).</summary>
    public const float HintRange = 6f;   // 66차 민: 「눈에 띄게」 — 경비실 어디서든

    /// <summary>
    /// 손전등 소품 원본(씬 경로 — 복제해 책상에 둔다). 둘 다 벤더 프리팹 <c>Flashlight_ON</c>의 씬 인스턴스.
    /// 72차(민: 「손전등 모델을 도형이 아니라 씬에 있는 손전등 프리펩으로」): 옛 경로 「Interior/Corridors/Flashlight」는 씬에 없어(실제 이름 「Flashlight (1)」)
    /// 늘 원통 도형으로 물러났다. 경로가 다 빗나가도 <see cref="FindTemplate"/>가 씬에서 이름으로 찾는다.
    /// </summary>
    public static readonly string[] FlashlightTemplatePaths = { "Interior/Corridors/Flashlight (1)", "Interior/Corridors/Flashlight" };

    /// <summary>책상 위 손전등 자리(가로).</summary>
    public static readonly Vector3 FlashlightSpot = new Vector3(32.86f, 0f, 46.92f);   // 66차 민 스크린샷: 모니터 앞 빈 상판(TeacherTable_static (1)) 한가운데

    /// <summary>책상 위 배터리 자리(가로).</summary>
    public static readonly Vector3 BatterySpot = new Vector3(33.04f, 0f, 46.62f);

    private const float DeskY = 2.11f;
    private const float AimSlack = 0.12f;
    private const float NoticeSeconds = 2.8f;

    private GameObject _flashlight;
    private GameObject _battery;
    private Material _bodyMat;
    private Material _capMat;
    private bool _ownsPrompt;
    private static string s_notice;
    private static float s_noticeUntil = -1f;
    private bool _ownsNotice;

    /// <summary>지금 근무 씬의 것. 없으면 null.</summary>
    public static DeskStarterKit Active { get; private set; }

    /// <summary>지금 겨눈 것(「flashlight」·「battery」, 없으면 빈 문자열). 시험용.</summary>
    public string Aimed { get; private set; } = string.Empty;

    /// <summary>책상 위 손전등 모형(없으면 null). 시험·검수용.</summary>
    public GameObject FlashlightModel
    {
        get { return _flashlight; }
    }

    /// <summary>책상 위 배터리 모형(없으면 null). 시험·검수용.</summary>
    public GameObject BatteryModel
    {
        get { return _battery; }
    }

    /// <summary>손전등 없이 F를 눌렀다 — 잠깐 안내.</summary>
    public static void ShowNoFlashlight()
    {
        Notice("손전등이 없습니다 — 경비실 책상을 확인하십시오");
    }

    /// <summary>104차: 예비도 책상 배터리도 없을 때 — 그 밤 남은 배터리가 있으면.</summary>
    public const string EmptySearchText = "손전등 배터리가 비었습니다 — 서랍·사물함·책장·탁자에서 배터리를 찾으십시오 (경비실 지도에 표시)";

    /// <summary>104차: 예비도 책상 배터리도 없고 그 밤 놓인 배터리도 다 주웠을 때.</summary>
    public const string EmptyNoneLeftText = "손전등 배터리가 비었습니다 — 오늘 밤 남은 배터리가 없습니다";

    /// <summary>
    /// 66차: 다 닳은 손전등 — 예비가 있으면 R, 없으면 책상 배터리를 안내. 104차(사용자: 「빈 손전등 안내도 추가해줘」): 둘 다 없으면
    /// 그 밤 남은 배터리를 찾으라고(<see cref="EmptySearchText"/>), 남은 것도 없으면 그렇다고(<see cref="EmptyNoneLeftText"/>) — 전에는 2일차부터 아무 말이 없었다.
    /// </summary>
    public static void ShowEmptyHint()
    {
        string text = EmptyHintText();
        if (text != null) Notice(text);
    }

    /// <summary>104차: 지금 보일 빈 손전등 안내(다 닳지 않았으면 null). 시험용으로 열어 둔다.</summary>
    public static string EmptyHintText()
    {
        FlashlightBattery b = NightRun.Battery;
        if (b == null || !b.IsEmpty) return null;
        if (b.Spare > 0) return "[R] 배터리 갈아 끼우기";
        if (NightRun.DeskKit != null && NightRun.DeskKit.BatteryOnDesk) return "손전등 배터리가 비어 있습니다 — 책상 위 배터리를 주우십시오";
        BatteryPlan plan = NightRun.BatteryPlan;
        return plan != null && plan.Remaining <= 0 ? EmptyNoneLeftText : EmptySearchText;
    }

    /// <summary>102차: 충전량이 넉넉한데 R을 눌렀다 — 예비를 쓰지 않고 잠깐 안내.</summary>
    public static void ShowStillCharged()
    {
        FlashlightBattery b = NightRun.Battery;
        Notice("배터리가 아직 충분합니다" + (b != null ? " (" + Mathf.RoundToInt(b.Charge * 100f) + "%)" : string.Empty));
    }

    private static void Notice(string text)
    {
        s_notice = text;
        s_noticeUntil = Time.unscaledTime + NoticeSeconds;
    }

    // ── 자동 설치 ─────────────────────────────────────────────

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Install()
    {
        Active = null;
        s_notice = null;
        s_noticeUntil = -1f;
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
        if (!FlowAutoInstall.IsDutyScene(scene) || FlowAutoInstall.Exists<DeskStarterKit>(scene)) return;
        FlowAutoInstall.CreateHost<DeskStarterKit>(scene, "DeskStarterKit (auto)");
    }

    private void OnEnable()
    {
        Active = this;
    }

    private void OnDisable()
    {
        ReleasePrompt();
        if (_flashlight != null) Destroy(_flashlight);
        if (_battery != null) Destroy(_battery);
        if (_bodyMat != null) Destroy(_bodyMat);
        if (_capMat != null) Destroy(_capMat);
        if (Active == this) Active = null;
    }

    // ── 매 프레임 ─────────────────────────────────────────────

    private void Update()
    {
        Aimed = string.Empty;
        DeskKit kit = NightRun.DeskKit;
        bool live = kit != null && NightRun.IsNightActive && !NightRun.IsCaptured;
        bool showLight = live && kit.FlashlightOnDesk;
        bool showBattery = live && kit.BatteryOnDesk;
        if ((showLight || showBattery) && (_flashlight == null || _battery == null)) Build();
        if (_flashlight != null && _flashlight.activeSelf != showLight) _flashlight.SetActive(showLight);
        if (_battery != null && _battery.activeSelf != showBattery) _battery.SetActive(showBattery);

        // 손전등이 없는데 켜져 있으면(밤 시작에 켜진 채 들어온 경우) 끈다.
        if (!NightRun.HasFlashlight)
        {
            FlashlightRelay relay = FlashlightRelay.Active;
            if (relay != null && relay.IsOn) relay.SetOn(false);
        }

        GameObject aimed = null;
        if (showLight || showBattery)
        {
            Camera cam = Camera.main;
            if (cam != null && showLight && Vector3.Distance(cam.transform.position, _flashlight.transform.position) <= HintRange) InteractionOutline.Request(_flashlight.transform, 0.65f);
            if (cam != null && showBattery && Vector3.Distance(cam.transform.position, _battery.transform.position) <= HintRange) InteractionOutline.Request(_battery.transform, 0.65f);
            if (CanInteract()) aimed = Aim(showLight, showBattery);
        }

        if (aimed == null)
        {
            ReleasePrompt();
            TickNotice();
            return;
        }

        bool isLight = aimed == _flashlight;
        Aimed = isLight ? "flashlight" : "battery";
        PlayerInteractor.SuppressThisFrame();
        InteractionOutline.Request(aimed.transform);
        bool full = !isLight && NightRun.Battery != null && NightRun.Battery.PocketFull;
        ClaimPrompt(isLight ? "[E] 손전등 줍기" : (full ? BatteryStash.FullText : "[E] 배터리 줍기"), !full);

#if ENABLE_LEGACY_INPUT_MANAGER
        if (!full && Input.GetKeyDown(KeyCode.E)) Pick(isLight);
#endif
    }

    /// <summary>줍는다([E]와 같은 처리 — 조준 검사 없이). 디버그·시험이 부른다.</summary>
    public bool Pick(bool flashlight)
    {
        bool ok = flashlight ? NightRun.PickUpFlashlight() : NightRun.TakeDeskBattery();
        if (!ok) return false;
        ReleasePrompt();
        if (FlashlightPower.Active != null) FlashlightPower.Active.PlayPickup();
        if (NightRun.Battery != null && NightRun.Battery.IsEmpty) ShowEmptyHint();   // 66차: 처음 손전등은 0%
        else if (flashlight) Notice("[좌클릭] 손전등 켜기");   // 90차: 옛 [F]
        return true;
    }

    private void TickNotice()
    {
        bool want = s_notice != null && Time.unscaledTime < s_noticeUntil;
        if (want)
        {
            if (_ownsNotice || string.IsNullOrEmpty(InteractionHud.ExternalPrompt))
            {
                _ownsNotice = true;
                InteractionHud.ExternalPrompt = s_notice;
                InteractionHud.ExternalHot = false;
            }

            return;
        }

        if (!_ownsNotice) return;
        _ownsNotice = false;
        if (InteractionHud.ExternalPrompt == s_notice) InteractionHud.ExternalPrompt = string.Empty;
        s_notice = null;
    }

    // ── 모형 ─────────────────────────────────────────────────

    private void Build()
    {
        float y = DeskTop(FlashlightSpot);
        if (_flashlight == null) _flashlight = MakeFlashlight(new Vector3(FlashlightSpot.x, y, FlashlightSpot.z));
        if (_battery == null) _battery = MakeBattery(new Vector3(BatterySpot.x, DeskTop(BatterySpot), BatterySpot.z));
    }

    private static float DeskTop(Vector3 spot)
    {
        RaycastHit hit;
        if (Physics.Raycast(new Vector3(spot.x, DeskY + 0.6f, spot.z), Vector3.down, out hit, 1.2f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return hit.point.y;
        return DeskY;
    }

    private GameObject MakeFlashlight(Vector3 at)
    {
        GameObject template = FindTemplate();
        GameObject go;
        if (template != null)
        {
            go = Instantiate(template);
            go.SetActive(true);   // 소품 손전등은 FlashlightRelay가 끌 수 있다(Flashlight_ON) — 복제본은 켠다
            foreach (Behaviour b in go.GetComponentsInChildren<Behaviour>(true))
            {
                // 66차 플레이 점검: Light는 URP 추가 데이터가 붙들고 있어 지울 수 없다(「Can't remove Light」) — 끄기만 한다.
                if (b is Light)
                {
                    b.enabled = false;
                    continue;
                }

                if (b.GetType().Name.StartsWith("UniversalAdditional")) continue;
                if (b is MonoBehaviour || b is AudioSource) Destroy(b);
            }

            foreach (Collider c in go.GetComponentsInChildren<Collider>(true)) Destroy(c);
            foreach (Rigidbody rb in go.GetComponentsInChildren<Rigidbody>(true)) Destroy(rb);
        }
        else
        {
            go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(go.GetComponent<Collider>());
            go.transform.localScale = new Vector3(0.045f, 0.12f, 0.045f);
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        }

        go.name = "책상 손전등";
        go.transform.SetParent(transform, true);
        // 프리팹 손전등은 길이가 로컬 Z — 소품 자리의 기울기는 버리고 상판에 눕힌다(도형은 위에서 세운 회전을 그대로).
        go.transform.rotation = template != null ? Quaternion.Euler(0f, 70f, 0f) : Quaternion.Euler(0f, 70f, 0f) * go.transform.rotation;
        go.transform.position = at;
        Bounds b2;
        if (Bounds(go, out b2)) go.transform.position += new Vector3(at.x - b2.center.x, at.y + 0.002f - b2.min.y, at.z - b2.center.z);
        return go;
    }

    /// <summary>
    /// 복제할 손전등 소품. 경로 → 없으면 씬에서 이름이 Flashlight로 시작하고 LODGroup이 있는 것(플레이어 손 · 정적 배칭 소품은 뺀다 —
    /// 정적 배칭된 복제는 합친 메시를 끌고 와 엉뚱하게 그려진다). 꺼져 있어도 찾는다. 없으면 null(원통 도형으로 물러남).
    /// </summary>
    private GameObject FindTemplate()
    {
        for (int i = 0; i < FlashlightTemplatePaths.Length; i++)
        {
            GameObject byPath = GameObject.Find("/" + FlashlightTemplatePaths[i]);
            if (byPath != null && Usable(byPath)) return byPath;
        }

        Scene scene = gameObject.scene;
        GameObject best = null;
        foreach (LODGroup lod in FindObjectsByType<LODGroup>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            GameObject g = lod.gameObject;
            if (g.scene != scene || !g.name.StartsWith("Flashlight", System.StringComparison.OrdinalIgnoreCase)) continue;
            if (g.GetComponentInParent<FPController>(true) != null || g.GetComponentInParent<Camera>(true) != null) continue;
            if (!Usable(g)) continue;
            if (best == null || g.name.IndexOf("_ON", System.StringComparison.OrdinalIgnoreCase) < 0) best = g;
        }

        return best;
    }

    private static bool Usable(GameObject g)
    {
        Renderer[] rs = g.GetComponentsInChildren<Renderer>(true);
        if (rs.Length == 0) return false;
        for (int i = 0; i < rs.Length; i++)
        {
            if (rs[i].isPartOfStaticBatch) return false;
        }

        return true;
    }

    private GameObject MakeBattery(Vector3 at)
    {
        if (_bodyMat == null)
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            _bodyMat = new Material(lit) { name = "desk battery body (runtime)" };
            _bodyMat.color = new Color(0.42f, 0.16f, 0.07f);
            _capMat = new Material(lit) { name = "desk battery cap (runtime)" };
            _capMat.color = new Color(0.78f, 0.62f, 0.28f);
            if (_capMat.HasProperty("_Metallic")) _capMat.SetFloat("_Metallic", 0.8f);
        }

        GameObject root = new GameObject("책상 배터리");
        root.transform.SetParent(transform, false);
        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(body.GetComponent<Collider>());
        body.name = "body";
        body.transform.SetParent(root.transform, false);
        body.transform.localScale = new Vector3(0.036f, 0.04f, 0.036f);
        body.GetComponent<Renderer>().sharedMaterial = _bodyMat;
        GameObject cap = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(cap.GetComponent<Collider>());
        cap.name = "cap";
        cap.transform.SetParent(root.transform, false);
        cap.transform.localPosition = new Vector3(0f, 0.042f, 0f);
        cap.transform.localScale = new Vector3(0.028f, 0.004f, 0.028f);
        cap.GetComponent<Renderer>().sharedMaterial = _capMat;
        root.transform.rotation = Quaternion.Euler(0f, 20f, 90f);   // 눕혀서
        root.transform.position = at + Vector3.up * 0.019f;
        return root;
    }

    private static bool Bounds(GameObject go, out Bounds bounds)
    {
        bounds = new Bounds();
        bool any = false;
        foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
        {
            if (!any)
            {
                bounds = r.bounds;
                any = true;
            }
            else
            {
                bounds.Encapsulate(r.bounds);
            }
        }

        return any;
    }

    // ── 겨눔 ─────────────────────────────────────────────────

    private GameObject Aim(bool light, bool battery)
    {
        Camera cam = Camera.main;
        if (cam == null) return null;
        Vector3 o = cam.transform.position;
        Vector3 f = cam.transform.forward;
        GameObject best = null;
        float bestT = float.MaxValue;
        GameObject[] candidates = { light ? _flashlight : null, battery ? _battery : null };
        for (int i = 0; i < candidates.Length; i++)
        {
            GameObject g = candidates[i];
            if (g == null || !g.activeSelf) continue;
            Bounds b;
            Vector3 p = Bounds(g, out b) ? b.center : g.transform.position;
            float t = Vector3.Dot(p - o, f);
            if (t <= 0f || t > Reach || t >= bestT) continue;
            float off = Vector3.Distance(p, o + f * t);
            if (off > AimSlack + 0.03f * t) continue;
            if (!Clear(o, p + Vector3.up * 0.03f)) continue;
            best = g;
            bestT = t;
        }

        return best;
    }

    private static bool Clear(Vector3 from, Vector3 to)
    {
        foreach (RaycastHit h in Physics.RaycastAll(from, (to - from).normalized, Vector3.Distance(from, to), Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (h.collider.name.StartsWith("Inspect ")) continue;   // 점검 대상 상자(모니터 K-1)는 보이는 벽이 아니다
            if (Vector3.Distance(h.point, to) < 0.12f) continue;   // 책상 윗면 가장자리
            PlayerSensors hb = PlayerSensors.Active;
            if (hb != null && hb.PlayerRoot != null && h.collider.transform.IsChildOf(hb.PlayerRoot)) continue;
            return false;
        }

        return true;
    }

    private static bool CanInteract()
    {
        if (Time.timeScale <= 0f || NightRun.IsCaptured) return false;
        CctvSystem cctv = CctvSystem.Active;
        return cctv == null || !cctv.IsViewing;
    }

    private void ClaimPrompt(string text, bool hot)
    {
        _ownsPrompt = true;
        _ownsNotice = false;
        InteractionHud.ExternalPrompt = text;
        InteractionHud.ExternalHot = hot;
    }

    private void ReleasePrompt()
    {
        if (!_ownsPrompt) return;
        _ownsPrompt = false;
        InteractionHud.ExternalPrompt = string.Empty;
        InteractionHud.ExternalHot = false;
    }
}

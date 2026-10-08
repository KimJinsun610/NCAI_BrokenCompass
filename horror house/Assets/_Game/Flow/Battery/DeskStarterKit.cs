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
    public const float HintRange = 3f;

    /// <summary>손전등 소품 원본(씬 경로 — 복제해 책상에 둔다).</summary>
    public const string FlashlightTemplatePath = "Interior/Corridors/Flashlight";

    /// <summary>책상 위 손전등 자리(가로).</summary>
    public static readonly Vector3 FlashlightSpot = new Vector3(32.42f, 0f, 47.33f);

    /// <summary>책상 위 배터리 자리(가로).</summary>
    public static readonly Vector3 BatterySpot = new Vector3(32.40f, 0f, 47.78f);

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
        if (flashlight) Notice("[F] 손전등 켜기");
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
        GameObject template = GameObject.Find("/" + FlashlightTemplatePath);
        GameObject go;
        if (template != null)
        {
            go = Instantiate(template);
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
        go.transform.rotation = Quaternion.Euler(0f, 70f, 0f) * go.transform.rotation;
        go.transform.position = at;
        Bounds b2;
        if (Bounds(go, out b2)) go.transform.position += new Vector3(at.x - b2.center.x, at.y + 0.002f - b2.min.y, at.z - b2.center.z);
        return go;
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
        RaycastHit hit;
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

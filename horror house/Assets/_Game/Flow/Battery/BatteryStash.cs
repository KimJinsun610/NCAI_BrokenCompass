using System.Collections.Generic;
using NightDuty;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 배터리가 놓이는 칸과 줍기(56차). 씬의 여닫는 수납(<see cref="DoorPolicySO.Kind.Storage"/> — 교탁·서랍·책장 아래 수납장·사물함)을 모아
/// 코어 계획(<see cref="NightRun.FillBatteryPlan"/>)에 넘기고, 뽑힌 칸 안에 작은 배터리를 둔다. 씬 파일은 고치지 않는다.
/// <list type="bullet">
/// <item><b>칸 무게</b>: 경비실에서 멀수록 크게(1 + 거리/15). 1일차 확정 칸 = 순찰 공간(교실 1-1·과학실) 안 수납.
/// 60차: 복도 관물대 10개는 늘 열리고 늘 후보다(<see cref="PlayerInteractor.IsCorridorLocker"/>). 라커룸 사물함만 잠겨 있고 밤마다 <see cref="BatteryRules.UnlockedLockers"/>개를 풀어 후보로 쓴다.</item>
/// <item><b>보임</b>: 칸이 열려 있고 아직 줍지 않았을 때만. 서랍은 서랍째 따라 나온다. 재시작하면 스냅샷의 「주운 칸」으로 돌아가 다시 보일 수 있다.</item>
/// <item><b>줍기</b>: <see cref="Reach"/> 안에서 배터리를 겨누면(조준선에서 10cm 남짓 — 서랍 콜라이더 안에 있어 레이로는 맞지 않는다) 외곽선과 「[E] 배터리 줍기」.
/// 그 프레임에는 <see cref="PlayerInteractor"/>가 [E]를 받지 않는다(서랍이 같이 닫히지 않게). 주머니가 차 있으면 「예비 배터리가 가득 찼습니다」.</item>
/// </list>
/// 배터리가 꺼져 있으면(<see cref="NightRun.BatteryEnabled"/> 거짓) 아무것도 하지 않는다. 근무 씬에 자동으로 선다.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(40)]   // PlayerInteractor(50)보다 먼저 — 배터리를 겨눈 프레임에 문 조작을 막는다
public sealed class BatteryStash : MonoBehaviour
{
    /// <summary>손이 닿는 거리(m).</summary>
    public const float Reach = 1.7f;

    /// <summary>안내 줄.</summary>
    public const string PromptText = "[E] 배터리 줍기";

    /// <summary>주머니가 찼을 때의 안내 줄.</summary>
    public const string FullText = "예비 배터리가 가득 찼습니다";

    private const float AimSlack = 0.1f;
    private static readonly Vector3 GuardRoom = new Vector3(34f, 0f, 46.1f);

    private sealed class Cache
    {
        public string Id;
        public DoorHandle Door;
        public Transform Follow;
        public Vector3 LocalPos;
        public Quaternion LocalRot;
        public GameObject Model;
    }

    [SerializeField] private KeyCode interactKey = KeyCode.E;

    private readonly Dictionary<string, DoorHandle> _doors = new Dictionary<string, DoorHandle>();
    private readonly List<Cache> _placed = new List<Cache>();
    private readonly List<Component> _unlocked = new List<Component>();
    private BatteryPlan _plan;
    private bool _ownsPrompt;
    private Material _bodyMat;
    private Material _capMat;

    /// <summary>지금 근무 씬의 것. 없으면 null.</summary>
    public static BatteryStash Active { get; private set; }

    /// <summary>지금 겨눈 칸 ID(없으면 빈 문자열). 시험용.</summary>
    public string AimedId { get; private set; } = string.Empty;

    /// <summary>지금 보이는(열린 칸 안의) 배터리 수. 시험용.</summary>
    public int VisibleCount { get; private set; }

    /// <summary>그 칸의 배터리 모형 위치(없으면 false). 시험·디버그용.</summary>
    public bool TryGetBatteryPosition(string id, out Vector3 position)
    {
        for (int i = 0; i < _placed.Count; i++)
        {
            if (_placed[i].Id != id || _placed[i].Model == null) continue;
            position = _placed[i].Model.transform.position;
            return true;
        }

        position = Vector3.zero;
        return false;
    }

    /// <summary>그 칸의 문(서랍·사물함)을 연다. 디버그·시험용.</summary>
    public bool DebugOpen(string id)
    {
        DoorHandle d;
        if (!_doors.TryGetValue(id, out d) || !d.IsValid) return false;
        if (PlayerInteractor.IsLockedLocker(d))
        {
            // 56차 QA: 디버그로 푼 사물함도 밤이 바뀌면 다시 잠근다(전에는 플레이가 끝날 때까지 풀려 있었다).
            PlayerInteractor.SetLockerUnlocked(d.Owner, true);
            _unlocked.Add(d.Owner);
        }

        d.Open();
        return true;
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
        if (!FlowAutoInstall.IsDutyScene(scene) || FlowAutoInstall.Exists<BatteryStash>(scene)) return;
        FlowAutoInstall.CreateHost<BatteryStash>(scene, "BatteryStash (auto)");
    }

    private void OnEnable()
    {
        Active = this;
    }

    private void OnDisable()
    {
        ReleasePrompt();
        Clear();
        if (_bodyMat != null) Destroy(_bodyMat);
        if (_capMat != null) Destroy(_capMat);
        if (Active == this) Active = null;
    }

    // ── 매 프레임 ─────────────────────────────────────────────

    private void Update()
    {
        AimedId = string.Empty;
        BatteryPlan plan = NightRun.BatteryPlan;
        if (!ReferenceEquals(plan, _plan))
        {
            Clear();
            _plan = plan;
        }

        if (plan == null)
        {
            ReleasePrompt();
            return;
        }

        if (!plan.Filled)
        {
            NightRun.FillBatteryPlan(Collect());
            Unlock(plan);
            Build(plan);
        }

        bool live = NightRun.IsNightActive && !NightRun.IsCaptured;
        int visible = 0;
        for (int i = 0; i < _placed.Count; i++)
        {
            Cache c = _placed[i];
            if (c.Model == null) continue;
            bool show = live && plan.Holds(c.Id) && c.Door.IsValid && c.Door.IsOpen;
            if (c.Model.activeSelf != show) c.Model.SetActive(show);
            if (!show) continue;
            visible++;
            if (c.Follow != null) c.Model.transform.SetPositionAndRotation(c.Follow.TransformPoint(c.LocalPos), c.Follow.rotation * c.LocalRot);
        }

        VisibleCount = visible;
        Cache aimed = live && visible > 0 && CanInteract() ? Aim() : null;
        if (aimed == null)
        {
            ReleasePrompt();
            return;
        }

        AimedId = aimed.Id;
        PlayerInteractor.SuppressThisFrame();
        bool full = NightRun.Battery != null && NightRun.Battery.PocketFull;
        InteractionOutline.Request(aimed.Model.transform);
        ClaimPrompt(full ? FullText : PromptText, !full);

#if ENABLE_LEGACY_INPUT_MANAGER
        if (!full && Input.GetKeyDown(interactKey)) Pick(aimed.Id);
#endif
    }

    /// <summary>그 칸의 배터리를 줍는다([E]와 같은 처리 — 조준 검사 없이). 디버그·시험이 부른다.</summary>
    public bool Pick(string id)
    {
        if (!NightRun.TakeBattery(id)) return false;
        for (int i = 0; i < _placed.Count; i++)
        {
            if (_placed[i].Id == id && _placed[i].Model != null) _placed[i].Model.SetActive(false);
        }

        ReleasePrompt();
        if (FlashlightPower.Active != null) FlashlightPower.Active.PlayPickup();
        return true;
    }

    // ── 칸 모으기 ─────────────────────────────────────────────

    private List<BatteryCache> Collect()
    {
        _doors.Clear();
        List<BatteryCache> list = new List<BatteryCache>();
        foreach (DoorHandle d in DoorHandle.All())
        {
            if (!d.IsValid || d.Owner == null) continue;
            if (PlayerInteractor.Classify(d) != DoorPolicySO.Kind.Storage) continue;
            if (!BatteryRules.Upright(d.Owner.transform.up.y)) continue;   // 56차 QA: 문을 막은 판자·엎어 기댄 책장(이름만 Bookcase)은 칸이 아니다
            string id = PathOf(d.Owner.transform);
            if (_doors.ContainsKey(id)) continue;
            _doors[id] = d;
            Vector3 p = d.Owner.transform.position;
            p.y = 0f;
            float weight = 1f + Vector3.Distance(p, GuardRoom) / 15f;
            bool locker = d.Owner.name.StartsWith("Locker") && !PlayerInteractor.IsCorridorLocker(d.Owner);   // 60차: 복도 관물대는 늘 열린 칸(라커룸 사물함만 밤마다 몇 개 풀림)
            bool starter = id.Contains("/Classroom01/") || id.Contains("/science classroom/");
            list.Add(new BatteryCache(id, weight, locker, starter));
        }

        return list;
    }

    private void Unlock(BatteryPlan plan)
    {
        for (int i = 0; i < plan.Unlocked.Count; i++)
        {
            DoorHandle d;
            if (!_doors.TryGetValue(plan.Unlocked[i], out d) || !d.IsValid) continue;
            PlayerInteractor.SetLockerUnlocked(d.Owner, true);
            _unlocked.Add(d.Owner);
        }
    }

    private void Build(BatteryPlan plan)
    {
        for (int i = 0; i < plan.Placed.Count; i++)
        {
            DoorHandle d;
            if (!_doors.TryGetValue(plan.Placed[i], out d) || !d.IsValid) continue;
            Cache c = new Cache { Id = plan.Placed[i], Door = d };
            Place(c);
            c.Model = MakeModel();
            c.Model.SetActive(false);
            _placed.Add(c);
        }
    }

    /// <summary>
    /// 칸 안 자리. 서랍(교탁 아래 <c>Drawer</c>·서랍장 <c>DrawerA</c>)은 움직이는 서랍의 바닥, 그 밖(책장 아래 수납장·사물함)은 몸통 가운데 · 문짝 아래 끝 높이의 바닥.
    /// </summary>
    private static void Place(Cache c)
    {
        Transform owner = c.Door.Owner.transform;
        Transform drawer = owner.name.Contains("Drawer") ? owner : FindChild(owner, "Drawer");
        Vector3 at;
        if (drawer != null)
        {
            Renderer r = drawer.GetComponent<Renderer>();
            Bounds b = r != null ? r.bounds : new Bounds(drawer.position, Vector3.one * 0.2f);
            at = new Vector3(b.center.x, b.min.y + 0.038f, b.center.z);
            c.Follow = drawer;
        }
        else
        {
            Renderer r = owner.GetComponent<Renderer>();
            Bounds b = r != null ? r.bounds : new Bounds(owner.position + Vector3.up, Vector3.one * 0.3f);
            float floor = b.min.y;
            float doorMin = float.MaxValue;
            foreach (Transform t in owner.GetComponentsInChildren<Transform>(true))
            {
                if (t == owner || t.name.IndexOf("Door", System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                Renderer dr = t.GetComponent<Renderer>();
                if (dr != null) doorMin = Mathf.Min(doorMin, dr.bounds.min.y);
            }

            if (doorMin < float.MaxValue) floor = Mathf.Max(floor, doorMin);

            // 56차 QA: 벽에 기댄 사물함(12°)은 AABB 가운데가 바닥 가운데가 아니다 — 몸통 축(up)을 따라 바닥 높이로 내린다(전에는 옆벽에 반쯤 묻혔다).
            Vector3 up = owner.up;
            Vector3 bottom = b.center + up * ((floor + 0.038f - b.center.y) / Mathf.Max(0.5f, up.y));

            // 56차 QA: 책장 아래 수납장은 위 칸 판자가 가려, 선 채로는 바닥 가운데가 손 닿는 거리(1.7m)에서 보이지 않는다 —
            // 문 쪽으로 붙인다. 앞 = 문짝 경첩(피벗)이 있는 쪽(피벗은 열어도 제자리라 열린 채 모아도 같다).
            float front = FrontOffset(owner, bottom);
            at = bottom + owner.forward * (Mathf.Sign(front) * Mathf.Max(0f, Mathf.Abs(front) - FrontInset));
            c.Follow = owner;
        }

        c.LocalPos = c.Follow.InverseTransformPoint(at);
        // 눕혀서, 칸의 옆 방향으로.
        c.LocalRot = Quaternion.Inverse(c.Follow.rotation) * Quaternion.LookRotation(c.Follow.right, c.Follow.up) * Quaternion.Euler(90f, 0f, 0f);
    }

    /// <summary>문 앞면에서 배터리 가운데까지(m) — 문짝에 닿지 않고 위 칸 판자 밑이 들여다보이는 깊이.</summary>
    private const float FrontInset = 0.07f;

    /// <summary>
    /// <paramref name="from"/>에서 문짝 경첩(이름에 Door가 든 렌더러 가진 자식의 피벗) 평균까지의 앞(<c>owner.forward</c>) 방향 거리. 문짝이 없으면 0.
    /// </summary>
    private static float FrontOffset(Transform owner, Vector3 from)
    {
        float sum = 0f;
        int n = 0;
        foreach (Transform t in owner.GetComponentsInChildren<Transform>(true))
        {
            if (t == owner || t.name.IndexOf("Door", System.StringComparison.OrdinalIgnoreCase) < 0 || t.GetComponent<Renderer>() == null) continue;
            sum += Vector3.Dot(t.position - from, owner.forward);
            n++;
        }

        return n > 0 ? sum / n : 0f;
    }

    private static Transform FindChild(Transform root, string part)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t != root && t.name.Contains(part)) return t;
        }

        return null;
    }

    private GameObject MakeModel()
    {
        if (_bodyMat == null)
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            _bodyMat = new Material(lit) { name = "battery body (runtime)" };
            _bodyMat.color = new Color(0.42f, 0.16f, 0.07f);   // 바랜 적갈색 겉종이 — 손전등 빛에 서랍 바닥과 갈린다
            _capMat = new Material(lit) { name = "battery cap (runtime)" };
            _capMat.color = new Color(0.78f, 0.62f, 0.28f);
            if (_capMat.HasProperty("_Metallic")) _capMat.SetFloat("_Metallic", 0.8f);
        }

        GameObject root = new GameObject("손전등 배터리");
        root.transform.SetParent(transform, false);
        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(body.GetComponent<Collider>());
        body.name = "body";
        body.transform.SetParent(root.transform, false);
        body.transform.localScale = new Vector3(0.036f, 0.04f, 0.036f);   // 지름 3.6cm · 길이 8cm
        body.GetComponent<Renderer>().sharedMaterial = _bodyMat;
        GameObject cap = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(cap.GetComponent<Collider>());
        cap.name = "cap";
        cap.transform.SetParent(root.transform, false);
        cap.transform.localPosition = new Vector3(0f, 0.042f, 0f);
        cap.transform.localScale = new Vector3(0.028f, 0.004f, 0.028f);
        cap.GetComponent<Renderer>().sharedMaterial = _capMat;
        return root;
    }

    private static string PathOf(Transform t)
    {
        string path = t.name;
        for (Transform p = t.parent; p != null; p = p.parent) path = p.name + "/" + path;
        return path;
    }

    private void Clear()
    {
        for (int i = 0; i < _placed.Count; i++)
        {
            if (_placed[i].Model != null) Destroy(_placed[i].Model);
        }

        _placed.Clear();
        for (int i = 0; i < _unlocked.Count; i++) PlayerInteractor.SetLockerUnlocked(_unlocked[i], false);
        _unlocked.Clear();
        _plan = null;
    }

    // ── 겨눔 ─────────────────────────────────────────────────

    private Cache Aim()
    {
        Camera cam = Camera.main;
        if (cam == null) return null;
        Vector3 o = cam.transform.position;
        Vector3 f = cam.transform.forward;
        Cache best = null;
        float bestT = float.MaxValue;
        for (int i = 0; i < _placed.Count; i++)
        {
            Cache c = _placed[i];
            if (c.Model == null || !c.Model.activeSelf) continue;
            Vector3 p = c.Model.transform.position;
            float t = Vector3.Dot(p - o, f);
            if (t <= 0f || t > Reach || t >= bestT) continue;
            float off = Vector3.Distance(p, o + f * t);
            if (off > AimSlack + 0.03f * t) continue;
            if (!Visible(o, p, c.Door.Owner.transform)) continue;
            best = c;
            bestT = t;
        }

        return best;
    }

    private static bool Visible(Vector3 from, Vector3 to, Transform owner)
    {
        RaycastHit hit;
        if (!Physics.Linecast(from, to, out hit, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return true;
        Transform h = hit.collider.transform;
        if (h == owner || h.IsChildOf(owner)) return true;   // 열린 서랍·사물함 자기 몸
        PlayerSensors hub = PlayerSensors.Active;
        return hub != null && hub.PlayerRoot != null && h.IsChildOf(hub.PlayerRoot);
    }

    private static bool CanInteract()
    {
        if (Time.timeScale <= 0f || PlayerSensors.TabletRaised || NightRun.IsCaptured) return false;
        CctvSystem cctv = CctvSystem.Active;
        return cctv == null || !cctv.IsViewing;
    }

    private void ClaimPrompt(string text, bool hot)
    {
        _ownsPrompt = true;
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

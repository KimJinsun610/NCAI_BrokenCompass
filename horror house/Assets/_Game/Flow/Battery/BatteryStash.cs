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
    /// <summary>
    /// 손이 닿는 거리(m, 눈에서 배터리까지 조준선 방향). 61차(민 스크린샷: 열린 관물대 바닥의 배터리가 주워지지 않음):
    /// 키를 1.95m(눈 1.70m)로 올리자 관물대 바닥(바닥 위 12cm)까지 세로만 1.58m — 문짝을 피해 1m 떨어져 서면 1.87m라 1.7에 걸렸다. 1.7 → 2.1.
    /// </summary>
    public const float Reach = 2.1f;

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
        public bool Open;            // 67차: 열린 선반 자리(문 없음 — 늘 보인다)
        public Transform Owner;
        public Transform Follow;
        public Vector3 LocalPos;
        public Quaternion LocalRot;
        public GameObject Model;
    }

    [SerializeField] private KeyCode interactKey = KeyCode.E;

    private readonly Dictionary<string, DoorHandle> _doors = new Dictionary<string, DoorHandle>();
    private readonly Dictionary<string, ShelfSpot> _shelves = new Dictionary<string, ShelfSpot>();

    /// <summary>67차: 열린 선반 자리 하나 — 책장·선반의 칸 위.</summary>
    private struct ShelfSpot
    {
        public Transform Owner;
        public Vector3 At;
        public Vector3 Side;
    }

    /// <summary>
    /// 67차(민: 「배터리가 책장이나 선반 같은 곳에도 놓여 있게」): 이름이 이것으로 시작하거나 이것을 품은 소품의 칸 위도 배터리 자리다(문이 없는 열린 칸).
    /// 문이 달린 책장 아래 수납장(Storage)은 따로 — 그 몸통이 아니라 위 칸 판자 위만 쓴다.
    /// </summary>
    public static readonly string[] ShelfNames = { "Bookcase", "BookShelving", "Shelf", "Shelves", "Rack", "Cupboard" };

    /// <summary>열린 칸으로 칠 높이(바닥에서, m) — 허리 아래 무릎 위부터 눈높이 조금 위까지.</summary>
    public const float ShelfMinHeight = 0.45f;

    /// <inheritdoc cref="ShelfMinHeight"/>
    public const float ShelfMaxHeight = 1.75f;

    /// <summary>소품 하나에서 뽑는 열린 자리 수 상한.</summary>
    public const int SpotsPerShelf = 1;
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

    /// <summary>67차: 열린 선반 자리 수(그 밤 후보). 시험·디버그용.</summary>
    public int ShelfSpotCount
    {
        get { return _shelves.Count; }
    }

    /// <summary>67차: 그 밤 배터리가 놓인 열린 선반 자리 ID. 시험·디버그용.</summary>
    public List<string> PlacedShelfIds()
    {
        List<string> ids = new List<string>();
        for (int i = 0; i < _placed.Count; i++)
        {
            if (_placed[i].Open) ids.Add(_placed[i].Id);
        }

        return ids;
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
            bool show = live && plan.Holds(c.Id) && (c.Open || (c.Door.IsValid && c.Door.IsOpen));
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
        // 67차 플레이 점검: 쓰지 않는 1-1 교실(문 잠금 · 선반이 막음, 21차)의 교탁·책장·서랍장 셋도 칸으로 잡혀 — 1일차 「순찰 공간」 몫이라 자주 — 배터리를 주울 수 없었다.
        SpaceZones zones = FindAnyObjectByType<SpaceZones>();
        Bounds unused = default;
        bool hasUnused = zones != null && zones.TryGetSpaceBox(SpaceId.Classroom_1_1, out unused);
        foreach (DoorHandle d in DoorHandle.All())
        {
            if (!d.IsValid || d.Owner == null) continue;
            if (hasUnused)
            {
                Vector3 at = d.Owner.transform.position;
                at.y = unused.center.y;
                if (unused.Contains(at)) continue;
            }

            if (PlayerInteractor.Classify(d) != DoorPolicySO.Kind.Storage) continue;
            if (!BatteryRules.Upright(d.Owner.transform.up.y)) continue;   // 56차 QA: 문을 막은 판자·엎어 기댄 책장(이름만 Bookcase)은 칸이 아니다
            string id = PathOf(d.Owner.transform);
            if (_doors.ContainsKey(id)) continue;
            _doors[id] = d;
            Vector3 p = d.Owner.transform.position;
            p.y = 0f;
            float weight = 1f + Vector3.Distance(p, GuardRoom) / 15f;
            bool locker = d.Owner.name.StartsWith("Locker") && !PlayerInteractor.IsCorridorLocker(d.Owner);   // 60차: 복도 관물대는 늘 열린 칸(라커룸 사물함만 밤마다 몇 개 풀림)
            bool starter = id.Contains("/Classroom02/") || id.Contains("/science classroom/");   // 67차: 1-1(Classroom01 묶음)은 쓰지 않는다 — 1-3 교실 · 과학실
            list.Add(new BatteryCache(id, weight, locker, starter));
        }

        CollectShelves(list);
        return list;
    }

    /// <summary>
    /// 67차: 열린 선반 자리를 모은다 — 근무 공간 상자 안, 이름이 <see cref="ShelfNames"/>인 서 있는 소품마다 칸 윗면 하나.
    /// 칸 찾기: 소품 경계 안 격자 점에서 아래로 쏴 그 소품 자신의 면에 맞은 곳 중 바닥 위 <see cref="ShelfMinHeight"/>~<see cref="ShelfMaxHeight"/>,
    /// 배터리가 들어갈 틈(위로 10cm 빔)이 있고 앞(소품 밖 0.9m · 눈높이)에서 보이는 곳.
    /// </summary>
    private void CollectShelves(List<BatteryCache> list)
    {
        _shelves.Clear();
        SpaceZones zones = FindAnyObjectByType<SpaceZones>();
        HashSet<Transform> done = new HashSet<Transform>();
        foreach (Renderer r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
        {
            Transform owner = ShelfOwner(r.transform);
            if (owner == null || !done.Add(owner)) continue;
            if (!BatteryRules.Upright(owner.up.y)) continue;
            Bounds b = BoundsOf(owner);
            if (b.size.y < 0.6f || b.size.x * b.size.z < 0.08f) continue;
            if (zones != null && !InDutySpace(zones, b.center)) continue;

            ShelfSpot spot;
            if (!FindShelfSpot(owner, b, out spot)) continue;
            string id = PathOf(owner) + "#open";
            if (_doors.ContainsKey(id) || _shelves.ContainsKey(id)) continue;
            _shelves[id] = spot;
            Vector3 p = spot.At;
            p.y = 0f;
            float weight = 1f + Vector3.Distance(p, GuardRoom) / 15f;
            bool starter = id.Contains("/Classroom02/") || id.Contains("/science classroom/");   // 1일차 순찰 공간(1-3 교실 · 과학실)
            list.Add(new BatteryCache(id, weight, false, starter));
        }
    }

    private static Transform ShelfOwner(Transform t)
    {
        for (Transform p = t; p != null; p = p.parent)
        {
            for (int i = 0; i < ShelfNames.Length; i++)
            {
                if (p.name.IndexOf(ShelfNames[i], System.StringComparison.OrdinalIgnoreCase) >= 0) return p;
            }
        }

        return null;
    }

    private static Bounds BoundsOf(Transform owner)
    {
        Renderer[] rs = owner.GetComponentsInChildren<Renderer>();
        Bounds b = rs.Length > 0 ? rs[0].bounds : new Bounds(owner.position, Vector3.zero);
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        return b;
    }

    private static bool InDutySpace(SpaceZones zones, Vector3 at)
    {
        // 67차 플레이 점검: SpaceId.Classroom 상자는 쓰지 않는 1-1 교실이었다(1-1 책장에 배터리 자리가 잡히고 1-3 책장은 빠졌다) — 1-3을 직접 묻고 1-1은 뺀다.
        Bounds unused;
        if (zones.TryGetSpaceBox(SpaceId.Classroom_1_1, out unused) && unused.Contains(at)) return false;
        SpaceId[] spaces = { SpaceId.Corridor, SpaceId.Classroom_1_3, SpaceId.ScienceRoom, SpaceId.Library, SpaceId.Toilet, SpaceId.SecurityRoom };
        for (int i = 0; i < spaces.Length; i++)
        {
            Bounds box;
            if (zones.TryGetSpaceBox(spaces[i], out box))
            {
                box.Expand(new Vector3(0.6f, 2f, 0.6f));
                if (box.Contains(at)) return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 칸 판자 높이(소품 뿌리에서, m) — 정적 배칭된 책장은 칸 판자에 콜라이더가 없고(몸통 상자 하나) 플레이 중에는 메시도 합쳐져 읽을 수 없다.
    /// 그래서 에디터에서 메시의 윗면을 재어 적어 둔다(67차 실측). 이름 앞부분으로 찾는다 — 더 긴 이름을 먼저.
    /// </summary>
    private static readonly KeyValuePair<string, float[]>[] ShelfHeights =
    {
        new KeyValuePair<string, float[]>("BookShelving", new[] { 0.74f, 1.06f, 1.38f, 1.70f }),            // BookShelvingSingle/Double(_booksA·B): 0.10 · 0.42 · 0.74 · 1.06 · 1.38 · 1.70 · 2.22(꼭대기)
        new KeyValuePair<string, float[]>("Bookcase", new[] { 0.56f, 0.81f, 1.06f, 1.30f, 1.55f }),          // Bookcase · BookcaseBroken A/B/C · Bookcase_static: 0.06 · 0.31 · 0.56 · 0.81 · 1.06 · 1.30 · 1.55 · 1.80
    };

    private static float[] HeightsOf(Transform owner)
    {
        for (int i = 0; i < ShelfHeights.Length; i++)
        {
            if (owner.name.StartsWith(ShelfHeights[i].Key, System.StringComparison.OrdinalIgnoreCase)) return ShelfHeights[i].Value;
        }

        return null;
    }

    /// <summary>책장 몸통을 통째로 감싼 상자 콜라이더(칸·책이 아니다) — 자리 검사에서 뺀다.</summary>
    private static bool IsShell(Collider c, Transform owner, Bounds b)
    {
        if (!c.transform.IsChildOf(owner)) return false;
        Bounds cb = c.bounds;
        return cb.size.y > b.size.y * 0.6f && cb.size.x * cb.size.z > b.size.x * b.size.z * 0.4f;
    }

    private static bool FindShelfSpot(Transform owner, Bounds b, out ShelfSpot spot)
    {
        spot = default;
        float floor = b.min.y;
        RaycastHit ground;
        if (Physics.Raycast(b.center + Vector3.up * 0.1f, Vector3.down, out ground, b.extents.y + 3f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (!ground.collider.transform.IsChildOf(owner)) floor = ground.point.y;
        }

        // 열린 면 = 경계 상자의 수평 두 축 중 짧은 쪽이 깊이. 앞은 그 축의 두 방향 중 앞에서 보이는 쪽.
        Vector3 depthAxis = b.size.x < b.size.z ? Vector3.right : Vector3.forward;
        Vector3 widthAxis = b.size.x < b.size.z ? Vector3.forward : Vector3.right;
        float depth = Mathf.Min(b.size.x, b.size.z);
        float width = Mathf.Max(b.size.x, b.size.z);

        // 칸 높이 후보: 표에 있으면 표(정적 배칭 책장), 없으면 소품 자신의 콜라이더 윗면(옛 방식).
        List<float> levels = new List<float>();
        float[] table = HeightsOf(owner);
        if (table != null)
        {
            for (int i = 0; i < table.Length; i++) levels.Add(owner.position.y + table[i]);
        }

        float bestScore = float.MinValue;
        bool found = false;
        float[] widths = { -0.34f, -0.12f, 0.12f, 0.34f };
        for (int side = -1; side <= 1; side += 2)
        {
            Vector3 front = depthAxis * side;
            Vector3 eyeBase = b.center + front * (depth * 0.5f + 0.9f);
            for (int wi = 0; wi < widths.Length; wi++)
            {
                Vector3 col = b.center + widthAxis * (widths[wi] * width) + front * (depth * 0.22f);
                List<float> here = new List<float>(levels);
                if (table == null)
                {
                    RaycastHit[] hits = Physics.RaycastAll(new Vector3(col.x, b.max.y + 0.05f, col.z), Vector3.down, b.size.y + 0.1f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                    for (int h = 0; h < hits.Length; h++)
                    {
                        if (hits[h].collider.transform.IsChildOf(owner) && hits[h].normal.y >= 0.85f && !IsShell(hits[h].collider, owner, b)) here.Add(hits[h].point.y);
                    }
                }

                for (int l = 0; l < here.Count; l++)
                {
                    float height = here[l] - floor;
                    if (height < ShelfMinHeight || height > ShelfMaxHeight) continue;
                    Vector3 at = new Vector3(col.x, here[l] + 0.022f, col.z);
                    if (Blocked(Physics.OverlapSphere(at + Vector3.up * 0.06f, 0.04f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore), owner, b)) continue;   // 책이 꽉 찬 칸
                    Vector3 eye = new Vector3(eyeBase.x + widthAxis.x * (at.x - b.center.x) * widthAxis.x, floor + 1.7f, eyeBase.z + widthAxis.z * (at.z - b.center.z) * widthAxis.z);
                    if (!Visible(eye, at + Vector3.up * 0.03f, owner, b)) continue;   // 앞에서 안 보인다(벽에 붙은 뒷면·가린 칸·문 달린 칸)
                    float score = -Mathf.Abs(height - 1.15f) - 0.05f * Mathf.Abs(widths[wi]);   // 허리~가슴 높이를 먼저
                    if (score <= bestScore) continue;
                    bestScore = score;
                    spot = new ShelfSpot { Owner = owner, At = at, Side = widthAxis };
                    found = true;
                }
            }
        }

        return found;
    }

    private static bool Blocked(Collider[] cs, Transform owner, Bounds b)
    {
        for (int i = 0; i < cs.Length; i++)
        {
            if (!IsShell(cs[i], owner, b)) return true;
        }

        return false;
    }

    private static bool Visible(Vector3 eye, Vector3 at, Transform owner, Bounds b)
    {
        Vector3 d = at - eye;
        float len = d.magnitude;
        RaycastHit[] hits = Physics.RaycastAll(eye, d / len, len - 0.02f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits.Length; i++)
        {
            if (!IsShell(hits[i].collider, owner, b)) return false;
        }

        return true;
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
            ShelfSpot shelf;
            if (_shelves.TryGetValue(plan.Placed[i], out shelf))
            {
                Cache open = new Cache { Id = plan.Placed[i], Open = true, Owner = shelf.Owner, Follow = shelf.Owner };
                open.LocalPos = shelf.Owner.InverseTransformPoint(shelf.At);
                open.LocalRot = Quaternion.Inverse(shelf.Owner.rotation) * Quaternion.LookRotation(shelf.Side, Vector3.up) * Quaternion.Euler(90f, 0f, 0f);
                open.Model = MakeModel();
                open.Model.SetActive(false);
                _placed.Add(open);
                continue;
            }

            DoorHandle d;
            if (!_doors.TryGetValue(plan.Placed[i], out d) || !d.IsValid) continue;
            Cache c = new Cache { Id = plan.Placed[i], Door = d, Owner = d.Owner.transform };
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
            if (!Visible(o, p, c.Owner)) continue;
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
        // 61차: 태블릿을 든 채로도 줍는다 — 문([E] 닫기)은 태블릿을 들어도 되는데 배터리만 안 돼 「배터리가 안 주워진다」로 보였다(민 스크린샷).
        if (Time.timeScale <= 0f || NightRun.IsCaptured) return false;
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

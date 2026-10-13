using NightDuty;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// 80차 [근무 지시] 출입 카드(민: W16 「과학실에 두고 간 출입 카드를 경비실 열쇠함에 넣어주십시오.」 · W17 「교실 교탁 서랍의 출입 카드를 가져와 주십시오.」 ·
/// 「열쇠함은 경비실의 LibraryCardCatalogB에 열려있는 서랍에 두는 걸로 · 카드키 모델로 결정」).
/// <list type="bullet">
/// <item><b>카드</b>: 지시가 나가면 카드키(얇은 플라스틱 판 · 파란 띠 · 금색 칩, 신규 에셋 없이 만든다)를 세운다 — W16은 과학실 교탁 위(그날마다 다른 교탁),
/// W17은 교실 교탁 서랍 안(서랍에 붙어 서랍과 함께 열린다). 플레이어 눈에 보이는 자리면 보이지 않을 때까지 기다렸다가 세운다.</item>
/// <item><b>92차 교실 교탁 서랍</b>(민: 「교탁 서랍 수칙이 교탁 서랍이 구현이 안되어 있어서 깰 수가 없어」): 80차의 1-1 교실은 쓰지 않는 잠긴 교실(21차)이라 닿을 수 없었고,
/// 쓰는 교실(1-3, <c>Classroom02</c>)의 교탁은 서랍 없는 정적 모델이다. 근무 씬이 열리면 그 정적 교탁을 숨기고(렌더러·콜라이더만 끔) 같은 자리에
/// 서랍 달린 교탁(벤더 <c>TeacherTable</c> 프리팹, 대역 <see cref="DrawerTablePrefab"/> — 서랍 열기 동작 포함)을 세운다 — 서랍은 교사 쪽(칠판 쪽)으로 열린다. 카드는 이 서랍 안.</item>
/// <item><b>집기</b>: 겨누고 [E] — 「[E] 출입 카드 줍기」. 카드는 사라지고 손에 든 것으로 친다(<see cref="NightRun.TakeDutyCard"/>).</item>
/// <item><b>넣기</b>: 들고 있으면 경비실 LibraryCardCatalogB에서 가장 많이 열린 서랍이 겨눌 수 있게 된다 — 「[E] 출입 카드 넣기」.
/// 넣으면 카드가 그 서랍 위에 놓이고 완료 답장(<see cref="NightRun.DeliverDutyCard"/>). 놓인 카드는 그 밤 내내 남는다.</item>
/// <item><b>되돌림</b>: 기한을 넘기거나 재시작으로 지시가 사라지면 카드를 거둔다(다시 지시가 나오면 다시 선다). 새 밤이면 모두 거둔다.</item>
/// </list>
/// 씬은 고치지 않는다. 근무 씬에 자동으로 선다.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(30)]   // 102차: BatteryStash(40)·PlayerInteractor(50)보다 먼저 — 카드를 겨눈 프레임에 배터리 안내·서랍 닫기를 막는다
public sealed class DutyCards : MonoBehaviour
{
    /// <summary>경비실 열쇠함(카드 목록함) — 씬 경로.</summary>
    public const string KeyBoxPath = "Interior/Corridors/FloorCorridors_1st/LibraryCardCatalogB";

    /// <summary>서랍 달린 교탁 대역(Resources) — 벤더 <c>TeacherTable</c> 프리팹(서랍 열기 동작 포함)을 감쌈. 92차: 씬의 1-1 교탁은 정적 배칭이라 복제하면 메시가 엉뚱한 데 그려진다.</summary>
    public const string DrawerTablePrefab = "StandIns/prop.teachertable.drawer";

    /// <summary>쓰는 교실(1-3)의 서랍 없는 정적 교탁 — 씬 경로. 92차: 숨기고 그 자리에 서랍 교탁.</summary>
    public const string ClassroomStaticTablePath = "Interior/Classroom02/TeacherTable02_static (1)";

    /// <summary>쓰는 교실에 세운 서랍 교탁의 서랍(없으면 null).</summary>
    public static Transform ClassroomDrawer { get; private set; }

    private GameObject _classroomTable;
    private bool _classroomTableTried;

    /// <summary>W16 카드를 두는 과학실 교탁들의 부모.</summary>
    public const string ScienceRoomPath = "Interior/science classroom";

    /// <summary>카드 크기(가로·두께·세로, m) — 신용카드 규격의 1.25배(어두운 데서 알아보게).</summary>
    public static readonly Vector3 CardSize = new Vector3(0.107f, 0.003f, 0.068f);

    /// <summary>손이 닿는 거리(m).</summary>
    public const float Reach = 2.1f;

    /// <summary>집는·넣는 소리(연출 소리표).</summary>
    public const string SoundKey = "duty.pickup";

    private const float AimRadius = 0.06f;

    /// <summary>
    /// 102차(민: 「교실 서랍 열고 카드키 잘 못꺼내는 거」): 서랍 안 카드는 서랍 메시 콜라이더 안에 누워 있어 구(0.06m)를 쏘면 서랍 옆판·앞판에 먼저 맞았다.
    /// 배터리 줍기(<see cref="BatteryStash"/>)처럼 조준선에서 떨어진 거리로 겨눈다 — 이 값 + 거리×3%.
    /// </summary>
    public const float CardAimSlack = 0.09f;

    /// <summary>102차: 서랍이 이만큼(m) 나와 있어야 서랍 안 카드를 집는다(교탁 서랍은 0.26m 열린다) — 닫힌 서랍 앞판 너머로 집히지 않게.</summary>
    public const float DrawerOpenMin = 0.12f;

    /// <summary>102차: 서랍 안 카드 가운데를 서랍 앞판 안쪽에서 이만큼(m) 들인다 — 서랍 가운데에 두면 열어도 교탁 윗판 밑에 반쯤 가렸다.</summary>
    public const float CardFrontInset = 0.08f;

    private static int s_cardAimFrame = -1;
    private static Vector3 s_drawerClosedLocal;

    /// <summary>102차: 이번 프레임에 카드를 겨누고 있는지 — <see cref="BatteryStash"/>가 보고 배터리 안내를 내지 않는다(같은 서랍에 배터리가 있어도 [E]가 카드로 간다).</summary>
    public static bool AimingCardThisFrame
    {
        get { return s_cardAimFrame == Time.frameCount; }
    }

    /// <summary>열쇠함 서랍 바닥 두께(m) — 서랍 상자 아랫면에서.</summary>
    private const float DrawerFloor = 0.018f;
    private const float SeenDistance = 16f;

    private GameObject _card;
    private string _cardId;
    private GameObject _slot;
    private Bounds _drawer;
    private bool _drawerFound;
    private bool _drawerSearched;
    private Transform _box;   // 104차: 찾은 열쇠함(배치 연출이 꺼 두어도 그대로 쓴다)
    private float _nextSearch;
    private GameObject _placed;
    private string _placedDuty;
    private DutyDispatcher _seen;
    private bool _ownsPrompt;

    /// <summary>지금 근무 씬의 것. 없으면 null.</summary>
    public static DutyCards Active { get; private set; }

    /// <summary>지금 세워 둔 (집기 전) 카드. 시험·디버그용.</summary>
    public GameObject Card
    {
        get { return _card; }
    }

    /// <summary>열쇠함에 넣어 둔 카드. 시험·디버그용.</summary>
    public GameObject Placed
    {
        get { return _placed; }
    }

    /// <summary>열쇠함 서랍(찾았으면).</summary>
    public bool TryGetKeyBoxDrawer(out Bounds drawer)
    {
        FindDrawer();
        drawer = _drawer;
        return _drawerFound;
    }

    // ── 자동 설치 ─────────────────────────────────────────────

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Install()
    {
        Active = null;
        s_cardAimFrame = -1;
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
        if (!FlowAutoInstall.IsDutyScene(scene) || FlowAutoInstall.Exists<DutyCards>(scene)) return;
        FlowAutoInstall.CreateHost<DutyCards>(scene, "DutyCards (auto)");
    }

    private void OnEnable()
    {
        Active = this;
    }

    private void OnDisable()
    {
        ReleasePrompt();
        ClearAll();
        if (_slot != null) Destroy(_slot);
        _slot = null;
        if (_classroomTable != null) Destroy(_classroomTable);
        _classroomTable = null;
        ClassroomDrawer = null;
        if (_hiddenTable != null)
        {
            foreach (Renderer r in _hiddenTable.GetComponentsInChildren<Renderer>(true)) r.enabled = true;
            foreach (Collider c in _hiddenTable.GetComponentsInChildren<Collider>(true)) c.enabled = true;
        }

        _hiddenTable = null;
        _classroomTableTried = false;
        if (Active == this) Active = null;
    }

    // ── 매 프레임 ─────────────────────────────────────────────

    private void Update()
    {
        if (!_classroomTableTried) EnsureClassroomTable();
        DutyDispatcher duties = NightRun.IsNightActive ? NightRun.Duties : null;
        if (!ReferenceEquals(duties, _seen))
        {
            _seen = duties;
            ClearAll();
        }

        if (duties == null)
        {
            ReleasePrompt();
            return;
        }

        // 넣어 둔 카드는 그 지시가 끝난 동안만(재시작으로 되돌리면 거둔다).
        if (_placed != null && !duties.Finished(_placedDuty)) RemovePlaced();

        string want = NightRun.DutyWantsCard(DutyCatalog.ScienceCardTarget) ? DutyCatalog.ScienceCardTarget
            : NightRun.DutyWantsCard(DutyCatalog.ClassroomCardTarget) ? DutyCatalog.ClassroomCardTarget : null;
        if (want == null)
        {
            if (_card != null) RemoveCard();
        }
        else if (_card == null || _cardId != want)
        {
            RemoveCard();
            TrySpawn(want);
        }

        string carried = NightRun.DutyCardCarried;
        EnsureSlot();
        if (_slot != null)
        {
            Collider c = _slot.GetComponent<Collider>();
            if (c != null && c.enabled != (carried != null)) c.enabled = carried != null;
        }

        if (!CanInteract())
        {
            ReleasePrompt();
            return;
        }

        if (carried != null && _slot != null && AimedSlot())
        {
            Transform outline = KeyBoxOutline();
            if (outline != null) InteractionOutline.Request(outline);
            ClaimPrompt("[E] 출입 카드 넣기");
            if (Pressed()) Deliver();
            return;
        }

        if (_card != null && want != null && AimedCard())
        {
            s_cardAimFrame = Time.frameCount;
            PlayerInteractor.SuppressThisFrame();   // 102차: [E]가 서랍을 같이 닫지 않게
            InteractionOutline.Request(_card.transform);
            ClaimPrompt("[E] 출입 카드 줍기");
            if (Pressed()) Take();
            return;
        }

        ReleasePrompt();
    }

    /// <summary>집는다([E]와 같다 — 조준 검사 없이). 디버그·시험이 부른다.</summary>
    public bool Take()
    {
        if (_card == null || !NightRun.TakeDutyCard(_cardId)) return false;
        PlaySound(_card.transform.position);
        Debug.Log("[근무 지시] 출입 카드를 집음 — " + _cardId, this);
        RemoveCard();
        ReleasePrompt();
        return true;
    }

    /// <summary>열쇠함에 넣는다([E]와 같다 — 조준 검사 없이). 디버그·시험이 부른다.</summary>
    public bool Deliver()
    {
        DutyDispatcher duties = NightRun.Duties;
        string dutyId = duties != null && duties.Active != null ? duties.Active.Id : null;
        if (!NightRun.DeliverDutyCard()) return false;
        FindDrawer();
        RemovePlaced();
        if (_drawerFound)
        {
            // 열린 서랍 안 바닥, 밖으로 나온 쪽(서랍 상자는 대부분 함 밖으로 나와 있다 — 위에서 들여다보면 보인다).
            Vector3 at = new Vector3(_drawer.center.x, _drawer.min.y + DrawerFloor + CardSize.y * 0.5f, _drawer.center.z) - KeyBoxOutward() * (_drawer.extents.z * 0.1f);   // 앞판에 가리지 않게 살짝 안쪽
            _placed = BuildCard("출입 카드 (열쇠함)", at, Quaternion.Euler(0f, Random.Range(-25f, 25f), 0f), null, false);
            _placedDuty = dutyId;
            PlaySound(at);
        }

        Debug.Log("[근무 지시] 출입 카드를 열쇠함에 넣음 — " + dutyId, this);
        ReleasePrompt();
        return true;
    }

    // ── 카드 세우기 ───────────────────────────────────────────

    private void TrySpawn(string card)
    {
        Vector3 at;
        Quaternion rot;
        Transform parent;
        if (!SpotFor(card, out at, out rot, out parent)) return;
        if (parent == null && SeenNow(at)) return;   // 눈앞에서 생기지 않게 — 안 볼 때까지 기다린다
        _card = BuildCard("출입 카드", at, rot, parent, true);
        _cardId = card;
        if (DirectionStage.Verbose) Debug.Log("[DutyCards] 출입 카드를 세움 " + card + " @ " + at.ToString("F2"));
    }

    private static bool SpotFor(string card, out Vector3 at, out Quaternion rot, out Transform parent)
    {
        at = Vector3.zero;
        rot = Quaternion.identity;
        parent = null;
        if (card == DutyCatalog.ClassroomCardTarget)
        {
            GameObject drawer = ClassroomDrawer != null ? ClassroomDrawer.gameObject : null;
            if (drawer == null) return false;
            Renderer r = drawer.GetComponentInChildren<Renderer>();
            Bounds b = r != null ? r.bounds : new Bounds(drawer.transform.position, new Vector3(0.4f, 0.12f, 0.3f));
            // 102차: 서랍 가운데가 아니라 앞판 쪽 — 서랍이 나오는 쪽(교탁 forward, 열기 동작이 Drawer를 로컬 +z로 민다)으로 들인다.
            Vector3 outward = drawer.transform.parent != null ? drawer.transform.parent.forward : drawer.transform.forward;
            outward.y = 0f;
            outward = outward.sqrMagnitude > 1e-4f ? outward.normalized : Vector3.forward;
            float half = Mathf.Abs(outward.x) * b.extents.x + Mathf.Abs(outward.z) * b.extents.z;
            Vector3 spot = b.center + outward * Mathf.Max(0f, half - CardFrontInset);
            float bottom = b.min.y + 0.012f;
            RaycastHit hit;
            if (Physics.Raycast(new Vector3(spot.x, b.max.y - 0.004f, spot.z), Vector3.down, out hit, b.size.y + 0.05f, ~0, QueryTriggerInteraction.Ignore)
                && hit.collider.transform.IsChildOf(drawer.transform))
            {
                bottom = hit.point.y;
            }

            at = new Vector3(spot.x, bottom + CardSize.y * 0.5f + 0.002f, spot.z);
            rot = drawer.transform.rotation * Quaternion.Euler(0f, 12f, 0f);
            parent = drawer.transform;
            return true;
        }

        if (card == DutyCatalog.ScienceCardTarget)
        {
            GameObject room = GameObject.Find("/" + ScienceRoomPath);
            if (room == null) return false;
            System.Collections.Generic.List<Transform> tables = new System.Collections.Generic.List<Transform>();
            foreach (Transform t in room.transform)
            {
                if (t.name.StartsWith("TeacherTable") && t.gameObject.activeInHierarchy) tables.Add(t);
            }

            if (tables.Count == 0) return false;
            tables.Sort((x, y) => string.CompareOrdinal(x.name, y.name));
            int day = Mathf.Max(1, NightRun.Day);
            for (int k = 0; k < tables.Count; k++)
            {
                Transform table = tables[(day + k) % tables.Count];
                Bounds b = new Bounds(table.position, Vector3.zero);
                bool any = false;
                foreach (Renderer r in table.GetComponentsInChildren<Renderer>())
                {
                    if (r.transform.name == "Drawer") continue;
                    if (!any) b = r.bounds;
                    else b.Encapsulate(r.bounds);
                    any = true;
                }

                if (!any) continue;
                // 교탁 윗면에서 다른 소품(저울·책 등)과 겹치지 않는 빈자리를 고른다 — 날마다 시작 칸을 바꾼다.
                bool longX = b.size.x > b.size.z;
                Vector3 along = longX ? Vector3.right : Vector3.forward;
                Vector3 across = longX ? Vector3.forward : Vector3.right;
                float la = (longX ? b.extents.x : b.extents.z) * 0.7f;
                float lc = (longX ? b.extents.z : b.extents.x) * 0.55f;
                Quaternion yaw = Quaternion.Euler(0f, 30f + day * 47f, 0f);
                const int Cells = 15;
                for (int c = 0; c < Cells; c++)
                {
                    int cell = (c + day * 4) % Cells;
                    float u = (cell % 5) / 4f * 2f - 1f;
                    float v = (cell / 5) / 2f * 2f - 1f;
                    Vector3 probe = b.center + along * (u * la) + across * (v * lc);
                    RaycastHit hit;
                    if (!Physics.Raycast(new Vector3(probe.x, b.max.y + 0.4f, probe.z), Vector3.down, out hit, 1f, ~0, QueryTriggerInteraction.Ignore)) continue;
                    if (hit.normal.y < 0.8f || !hit.collider.transform.IsChildOf(table)) continue;
                    if (hit.point.y < b.max.y - 0.12f) continue;   // 윗면이 아니다
                    Vector3 half = new Vector3(CardSize.x * 0.8f, 0.03f, CardSize.z * 0.8f);
                    if (Physics.CheckBox(hit.point + Vector3.up * 0.04f, half, yaw, ~0, QueryTriggerInteraction.Ignore)) continue;   // 소품이 있다
                    at = hit.point + Vector3.up * (CardSize.y * 0.5f + 0.001f);
                    rot = yaw;
                    return true;
                }
            }
        }

        return false;
    }

    private static bool SeenNow(Vector3 at)
    {
        Camera cam = Camera.main;
        if (cam == null) return false;
        Vector3 v = cam.WorldToViewportPoint(at);
        if (v.z <= 0f || v.z > SeenDistance || v.x < -0.05f || v.x > 1.05f || v.y < -0.05f || v.y > 1.05f) return false;
        Vector3 from = cam.transform.position;
        Vector3 to = at + Vector3.up * 0.03f;
        return !Physics.Linecast(from, to, ~0, QueryTriggerInteraction.Ignore);
    }

    /// <summary>카드키 하나 — 흰 플라스틱 판 · 파란 띠 · 금색 칩(손전등에 보이게 아주 약하게 스스로 빛난다).</summary>
    private static GameObject BuildCard(string name, Vector3 at, Quaternion rot, Transform parent, bool collider)
    {
        GameObject root = new GameObject(name);
        root.transform.SetPositionAndRotation(at, rot);
        if (parent != null) root.transform.SetParent(parent, true);

        Part(root.transform, "판", Vector3.zero, CardSize, new Color(0.86f, 0.88f, 0.9f), 0.04f);
        Part(root.transform, "띠", new Vector3(0f, CardSize.y * 0.5f + 0.0004f, CardSize.z * 0.28f), new Vector3(CardSize.x, 0.0008f, CardSize.z * 0.3f), new Color(0.12f, 0.3f, 0.62f), 0.05f);
        Part(root.transform, "칩", new Vector3(-CardSize.x * 0.28f, CardSize.y * 0.5f + 0.0004f, -CardSize.z * 0.08f), new Vector3(0.016f, 0.0008f, 0.013f), new Color(0.78f, 0.62f, 0.25f), 0.04f);
        Part(root.transform, "사진", new Vector3(CardSize.x * 0.3f, CardSize.y * 0.5f + 0.0004f, -CardSize.z * 0.12f), new Vector3(0.022f, 0.0008f, 0.027f), new Color(0.35f, 0.36f, 0.38f), 0.02f);

        if (collider)
        {
            BoxCollider box = root.AddComponent<BoxCollider>();
            box.size = new Vector3(CardSize.x * 1.3f, 0.04f, CardSize.z * 1.4f);
            box.center = new Vector3(0f, 0.012f, 0f);
        }

        return root;
    }

    private static void Part(Transform root, string name, Vector3 local, Vector3 size, Color color, float glow)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        Object.Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(root, false);
        go.transform.localPosition = local;
        go.transform.localScale = size;
        Renderer r = go.GetComponent<Renderer>();
        r.shadowCastingMode = ShadowCastingMode.Off;
        Shader sh = Shader.Find("Universal Render Pipeline/Lit");
        Material m = new Material(sh != null ? sh : r.sharedMaterial.shader);
        m.color = color;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.65f);
        if (m.HasProperty("_EmissionColor"))
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", color * glow);
        }

        r.sharedMaterial = m;
    }

    // ── 쓰는 교실의 서랍 교탁(92차) ─────────────────────────────

    /// <summary>쓰는 교실(1-3)의 정적 교탁을 숨기고 그 자리에 서랍 교탁을 세운다(한 번). 디버그·시험도 부른다.</summary>
    public bool EnsureClassroomTable()
    {
        _classroomTableTried = true;
        if (_classroomTable != null) return true;
        GameObject source = Resources.Load<GameObject>(DrawerTablePrefab);
        GameObject old = GameObject.Find("/" + ClassroomStaticTablePath);
        if (source == null || old == null)
        {
            Debug.LogWarning("[DutyCards] 교실 서랍 교탁을 세우지 못했습니다(원본 " + (source != null) + " · 정적 교탁 " + (old != null) + ").");
            return false;
        }

        Bounds ob;
        if (!TryBounds(old, out ob)) return false;
        // 정적 교탁의 앞(학생 쪽) 반대가 교사 쪽 — 원본 교탁은 앞(forward)으로 서랍이 열린다. 정적 교탁은 forward가 교사 쪽이 아니면 뒤집는다.
        Vector3 teacher = TeacherSide(old.transform, ob.center);
        Quaternion rot = Quaternion.LookRotation(teacher, Vector3.up);
        GameObject table = Instantiate(source, ob.center, rot, old.transform.parent);
        table.name = "교탁(서랍) 1-3";   // 안의 벤더 교탁 이름은 TeacherTable 그대로 — 여닫는 수납(DoorPolicySO Storage)으로 친다
        Bounds nb;
        if (TryBounds(table, out nb)) table.transform.position += new Vector3(ob.center.x - nb.center.x, ob.min.y - nb.min.y, ob.center.z - nb.center.z);

        foreach (Renderer r in old.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
        foreach (Collider c in old.GetComponentsInChildren<Collider>(true)) c.enabled = false;
        _classroomTable = table;
        _hiddenTable = old;
        Transform inner = table.transform.Find("TeacherTable");
        Transform drawer = inner != null ? inner.Find("Drawer") : null;
        ClassroomDrawer = drawer;
        if (drawer != null) s_drawerClosedLocal = drawer.localPosition;   // 102차: 막 세운 교탁의 서랍은 닫혀 있다 — 열림 판단의 기준
        if (DirectionStage.Verbose) Debug.Log("[DutyCards] 교실(1-3) 교탁을 서랍 교탁으로 바꿈 @ " + table.transform.position.ToString("F2") + " 서랍 쪽 " + teacher.ToString("F1"));
        return drawer != null;
    }

    private GameObject _hiddenTable;

    /// <summary>교사 쪽(수평 단위 벡터) — 교탁에서 교실 안 학생 책상이 적은 쪽(칠판 쪽). 판단이 안 서면 정적 교탁의 forward.</summary>
    private static Vector3 TeacherSide(Transform table, Vector3 center)
    {
        Vector3 f = table.forward;
        f.y = 0f;
        f = f.sqrMagnitude > 1e-4f ? f.normalized : Vector3.forward;
        // 학생 책상(이름에 Desk)이 많은 쪽의 반대.
        int front = 0, back = 0;
        foreach (Collider c in Physics.OverlapSphere(center, 6f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (c.name.IndexOf("Desk", System.StringComparison.OrdinalIgnoreCase) < 0) continue;
            if (Vector3.Dot(c.bounds.center - center, f) > 0f) front++;
            else back++;
        }

        return front > back ? -f : f;
    }

    private static bool TryBounds(GameObject go, out Bounds b)
    {
        b = new Bounds();
        bool any = false;
        foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
        {
            if (!any) b = r.bounds;
            else b.Encapsulate(r.bounds);
            any = true;
        }

        return any;
    }

    // ── 열쇠함 ───────────────────────────────────────────────

    /// <summary>
    /// 104차(피드백: 「보안카드 회수 지시에서 열쇠함이 흔들리는 연출이 걸리면 회수가 안 된다」): 경비실 배치 구간 3 이상인 밤에는
    /// <see cref="LayoutCabinets"/>가 밤 첫 프레임(실행 순서 0 — 여기 30보다 먼저)에 원래 열쇠함을 꺼 두고 흔들릴 대역으로 바꾼다.
    /// 전에는 <c>GameObject.Find</c>(꺼진 것은 못 찾음)로 찾고 한 번 실패하면 그 밤 내내 다시 찾지 않아 넣을 자리(<c>duty.keybox</c>)가 생기지 않았다 →
    /// 꺼져 있어도 씬 뿌리에서 경로로 찾고, 서랍 상자는 콜라이더 bounds(꺼지면 0) 대신 변환 행렬로 잰다. 못 찾으면 1초마다 다시.
    /// </summary>
    private void FindDrawer()
    {
        if (_drawerSearched || Time.unscaledTime < _nextSearch) return;
        _nextSearch = Time.unscaledTime + 1f;
        Transform box = FindKeyBox();
        if (box == null)
        {
            Debug.LogWarning("[DutyCards] 경비실 열쇠함(LibraryCardCatalogB)을 찾지 못했습니다: " + KeyBoxPath);
            return;
        }

        _drawerSearched = true;
        _box = box;

        // 서랍 상자 콜라이더(작고 납작한 것) 중 가로 면적이 가장 큰 것 = 가장 많이 열린 서랍.
        float best = 0f;
        foreach (BoxCollider c in box.GetComponentsInChildren<BoxCollider>(true))
        {
            Bounds b = WorldBox(c);
            if (b.size.y > 0.2f || b.size.y < 0.06f || b.size.x > 0.35f) continue;
            float area = b.size.x * b.size.z;
            if (area <= best) continue;
            best = area;
            _drawer = b;
            _drawerFound = true;
        }

        if (!_drawerFound) Debug.LogWarning("[DutyCards] 열쇠함의 열린 서랍을 찾지 못했습니다.");
    }

    private void EnsureSlot()
    {
        if (_slot != null) return;
        FindDrawer();
        if (!_drawerFound) return;
        _slot = new GameObject("duty.keybox");
        _slot.transform.position = _drawer.center + Vector3.up * 0.02f;
        BoxCollider c = _slot.AddComponent<BoxCollider>();
        c.size = new Vector3(_drawer.size.x * 1.35f, _drawer.size.y + 0.06f, _drawer.size.z * 1.35f);
        c.enabled = false;
    }

    /// <summary>104차: 열쇠함 — 켜져 있으면 <c>GameObject.Find</c>, 꺼져 있으면(배치 연출 대역으로 바뀜) 씬 뿌리에서 경로로.</summary>
    private Transform FindKeyBox()
    {
        GameObject on = GameObject.Find("/" + KeyBoxPath);
        if (on != null) return on.transform;
        int cut = KeyBoxPath.IndexOf('/');
        string rootName = KeyBoxPath.Substring(0, cut);
        foreach (GameObject root in gameObject.scene.GetRootGameObjects())
        {
            if (root.name != rootName) continue;
            Transform t = root.transform.Find(KeyBoxPath.Substring(cut + 1));
            if (t != null) return t;
        }

        return null;
    }

    /// <summary>104차: 상자 콜라이더의 월드 상자 — 꺼져 있어도(<c>Collider.bounds</c>는 0) 변환 행렬로 잰다.</summary>
    private static Bounds WorldBox(BoxCollider c)
    {
        Matrix4x4 m = c.transform.localToWorldMatrix;
        Vector3 e = c.size * 0.5f;
        Bounds b = new Bounds(m.MultiplyPoint3x4(c.center), Vector3.zero);
        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = c.center + new Vector3((i & 1) == 0 ? -e.x : e.x, (i & 2) == 0 ? -e.y : e.y, (i & 4) == 0 ? -e.z : e.z);
            b.Encapsulate(m.MultiplyPoint3x4(corner));
        }

        return b;
    }

    /// <summary>열쇠함 몸통에서 서랍이 나온 쪽(수평 단위 벡터, 없으면 +z).</summary>
    private Vector3 KeyBoxOutward()
    {
        Transform box = _box;
        if (box == null) return Vector3.forward;
        Vector3 d = _drawer.center - box.position;
        d.y = 0f;
        Vector3 f = box.forward;
        f.y = 0f;
        if (f.sqrMagnitude < 1e-4f) return Vector3.forward;
        f.Normalize();
        return Vector3.Dot(f, d) >= 0f ? f : -f;
    }

    /// <summary>테두리 빛낼 열쇠함 — 104차: 원래 것이 꺼져 있으면(배치 연출) 대역.</summary>
    private Transform KeyBoxOutline()
    {
        if (_box != null && _box.gameObject.activeInHierarchy) return _box;
        return LayoutCabinets.Active != null ? LayoutCabinets.Active.DrawerInstance : null;
    }

    /// <summary>
    /// 104차: 넣을 자리(<c>duty.keybox</c>)를 겨눴는지 — 조준선에 걸린 것 중 열쇠함 자신(원래 것 · 흔들림 대역의 튀어나온 서랍 · 경비실 보충 상자)은
    /// 건너뛰고 자리를 찾는다(벽 등 다른 것이 먼저 가리면 아님). 전에는 처음 맞은 것 하나만 봐서 덜컹 연출로 나온 서랍이 자리를 가릴 수 있었다.
    /// </summary>
    private bool AimedSlot()
    {
        Camera cam = Camera.main;
        if (cam == null || _slot == null) return false;
        Transform slot = _slot.transform;
        Transform standIn = LayoutCabinets.Active != null ? LayoutCabinets.Active.DrawerInstance : null;
        RaycastHit[] hits = Physics.SphereCastAll(cam.transform.position, AimRadius, cam.transform.forward, Reach, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (RaycastHit h in hits)
        {
            Transform t = h.collider.transform;
            if (t == slot || t.IsChildOf(slot)) return true;
            if (_box != null && t.IsChildOf(_box)) continue;
            if (standIn != null && t.IsChildOf(standIn)) continue;
            if (t.name.EndsWith("LibraryCardCatalogB")) continue;   // GuardRoomColliders 보충 상자
            if (PlayerSensors.Active != null && PlayerSensors.Active.PlayerRoot != null && t.IsChildOf(PlayerSensors.Active.PlayerRoot)) continue;
            return false;
        }

        return false;
    }

    // ── 정리 ────────────────────────────────────────────────

    private void RemoveCard()
    {
        if (_card != null) Destroy(_card);
        _card = null;
        _cardId = null;
    }

    private void RemovePlaced()
    {
        if (_placed != null) Destroy(_placed);
        _placed = null;
        _placedDuty = null;
    }

    private void ClearAll()
    {
        RemoveCard();
        RemovePlaced();
    }

    // ── 조작 ────────────────────────────────────────────────

    private static bool Aimed(Transform target)
    {
        Camera cam = Camera.main;
        if (cam == null || target == null) return false;
        RaycastHit hit;
        if (!Physics.SphereCast(cam.transform.position, AimRadius, cam.transform.forward, out hit, Reach, ~0, QueryTriggerInteraction.Ignore)) return false;
        return hit.transform == target || hit.transform.IsChildOf(target);
    }

    /// <summary>
    /// 102차: 카드를 겨눴는지 — 조준선에서 카드까지의 거리(<see cref="CardAimSlack"/> + 거리×3%) 안이고, 눈에서 카드까지 카드·서랍·플레이어 말고는 가린 것이 없을 때.
    /// 서랍 안 카드는 서랍이 <see cref="DrawerOpenMin"/> 넘게 나와 있어야 한다.
    /// </summary>
    private bool AimedCard()
    {
        Camera cam = Camera.main;
        if (cam == null || _card == null) return false;
        Transform drawer = _card.transform.parent;
        if (drawer != null)
        {
            if (drawer != ClassroomDrawer || !ClassroomDrawerOpen()) return false;
        }

        Vector3 o = cam.transform.position;
        Vector3 f = cam.transform.forward;
        Vector3 p = _card.transform.position + Vector3.up * 0.008f;
        float t = Vector3.Dot(p - o, f);
        if (t <= 0f || t > Reach) return false;
        if (Vector3.Distance(p, o + f * t) > CardAimSlack + 0.03f * t) return false;
        Vector3 d = p - o;
        float len = d.magnitude;
        if (len < 1e-3f) return true;
        PlayerSensors hub = PlayerSensors.Active;
        foreach (RaycastHit h in Physics.RaycastAll(o, d / len, len - 0.005f, ~0, QueryTriggerInteraction.Ignore))
        {
            Transform ht = h.collider.transform;
            if (ht == _card.transform || ht.IsChildOf(_card.transform)) continue;
            if (drawer != null && (ht == drawer || ht.IsChildOf(drawer))) continue;   // 열린 서랍 자기 몸(앞판·옆판)
            if (hub != null && hub.PlayerRoot != null && ht.IsChildOf(hub.PlayerRoot)) continue;
            return false;
        }

        return true;
    }

    /// <summary>102차: 교실 교탁 서랍이 열려 있는지(닫힌 자리에서 <see cref="DrawerOpenMin"/> 넘게 나왔는지).</summary>
    public static bool ClassroomDrawerOpen()
    {
        Transform drawer = ClassroomDrawer;
        return drawer != null && (drawer.localPosition - s_drawerClosedLocal).magnitude >= DrawerOpenMin;
    }

    private static bool Pressed()
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.E);
#else
        return false;
#endif
    }

    private static bool CanInteract()
    {
        if (Time.timeScale <= 0f || PlayerSensors.TabletRaised || NightRun.IsCaptured || ViolationScare.Busy) return false;
        CctvSystem cctv = CctvSystem.Active;
        if (cctv != null && cctv.IsViewing) return false;
        PlayerTablet[] tablets = FindObjectsByType<PlayerTablet>(FindObjectsSortMode.None);
        for (int i = 0; i < tablets.Length; i++)
        {
            if (TabletZoom.Reading(tablets[i])) return false;
        }

        return true;
    }

    private void ClaimPrompt(string text)
    {
        _ownsPrompt = true;
        InteractionHud.ExternalPrompt = text;
        InteractionHud.ExternalHot = true;
    }

    private void ReleasePrompt()
    {
        if (!_ownsPrompt) return;
        _ownsPrompt = false;
        InteractionHud.ExternalPrompt = string.Empty;
        InteractionHud.ExternalHot = false;
    }

    private static void PlaySound(Vector3 at)
    {
        float volume;
        AudioClip clip = DirectionSoundTableSO.FindExact(SoundKey, out volume);
        if (clip == null) return;
        GameObject go = new GameObject("sfx " + SoundKey);
        go.transform.position = at;
        AudioSource s = go.AddComponent<AudioSource>();
        s.playOnAwake = false;
        s.clip = clip;
        s.volume = volume;
        s.spatialBlend = 1f;
        s.dopplerLevel = 0f;
        s.minDistance = 1f;
        s.maxDistance = 12f;
        s.Play();
        Destroy(go, clip.length + 0.2f);
    }
}

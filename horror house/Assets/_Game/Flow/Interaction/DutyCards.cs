using NightDuty;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// 80차 [근무 지시] 출입 카드(민: W16 「과학실에 두고 간 출입 카드를 경비실 열쇠함에 넣어주십시오.」 · W17 「교실 교탁 서랍의 출입 카드를 가져와 주십시오.」 ·
/// 「열쇠함은 경비실의 LibraryCardCatalogB에 열려있는 서랍에 두는 걸로 · 카드키 모델로 결정」).
/// <list type="bullet">
/// <item><b>카드</b>: 지시가 나가면 카드키(얇은 플라스틱 판 · 파란 띠 · 금색 칩, 신규 에셋 없이 만든다)를 세운다 — W16은 과학실 교탁 위(그날마다 다른 교탁),
/// W17은 교실(1-1) 교탁 서랍 안(서랍에 붙어 서랍과 함께 열린다). 플레이어 눈에 보이는 자리면 보이지 않을 때까지 기다렸다가 세운다.</item>
/// <item><b>집기</b>: 겨누고 [E] — 「[E] 출입 카드 줍기」. 카드는 사라지고 손에 든 것으로 친다(<see cref="NightRun.TakeDutyCard"/>).</item>
/// <item><b>넣기</b>: 들고 있으면 경비실 LibraryCardCatalogB에서 가장 많이 열린 서랍이 겨눌 수 있게 된다 — 「[E] 출입 카드 넣기」.
/// 넣으면 카드가 그 서랍 위에 놓이고 완료 답장(<see cref="NightRun.DeliverDutyCard"/>). 놓인 카드는 그 밤 내내 남는다.</item>
/// <item><b>되돌림</b>: 기한을 넘기거나 재시작으로 지시가 사라지면 카드를 거둔다(다시 지시가 나오면 다시 선다). 새 밤이면 모두 거둔다.</item>
/// </list>
/// 씬은 고치지 않는다. 근무 씬에 자동으로 선다.
/// </summary>
[DisallowMultipleComponent]
public sealed class DutyCards : MonoBehaviour
{
    /// <summary>경비실 열쇠함(카드 목록함) — 씬 경로.</summary>
    public const string KeyBoxPath = "Interior/Corridors/FloorCorridors_1st/LibraryCardCatalogB";

    /// <summary>W17 교실 교탁 서랍 — 씬 경로.</summary>
    public const string ClassroomDrawerPath = "Interior/Classroom01/TeacherTable/Drawer";

    /// <summary>W16 카드를 두는 과학실 교탁들의 부모.</summary>
    public const string ScienceRoomPath = "Interior/science classroom";

    /// <summary>카드 크기(가로·두께·세로, m) — 신용카드 규격의 1.25배(어두운 데서 알아보게).</summary>
    public static readonly Vector3 CardSize = new Vector3(0.107f, 0.003f, 0.068f);

    /// <summary>손이 닿는 거리(m).</summary>
    public const float Reach = 2.1f;

    /// <summary>집는·넣는 소리(연출 소리표).</summary>
    public const string SoundKey = "duty.pickup";

    private const float AimRadius = 0.06f;

    /// <summary>열쇠함 서랍 바닥 두께(m) — 서랍 상자 아랫면에서.</summary>
    private const float DrawerFloor = 0.018f;
    private const float SeenDistance = 16f;

    private GameObject _card;
    private string _cardId;
    private GameObject _slot;
    private Bounds _drawer;
    private bool _drawerFound;
    private bool _drawerSearched;
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
        if (Active == this) Active = null;
    }

    // ── 매 프레임 ─────────────────────────────────────────────

    private void Update()
    {
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

        if (carried != null && _slot != null && Aimed(_slot.transform))
        {
            Transform outline = KeyBoxOutline();
            if (outline != null) InteractionOutline.Request(outline);
            ClaimPrompt("[E] 출입 카드 넣기");
            if (Pressed()) Deliver();
            return;
        }

        if (_card != null && want != null && Aimed(_card.transform))
        {
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
            GameObject drawer = GameObject.Find("/" + ClassroomDrawerPath);
            if (drawer == null) return false;
            Renderer r = drawer.GetComponentInChildren<Renderer>();
            Bounds b = r != null ? r.bounds : new Bounds(drawer.transform.position, new Vector3(0.4f, 0.12f, 0.3f));
            float bottom = b.min.y + 0.012f;
            RaycastHit hit;
            if (Physics.Raycast(new Vector3(b.center.x, b.max.y - 0.004f, b.center.z), Vector3.down, out hit, b.size.y + 0.05f, ~0, QueryTriggerInteraction.Ignore)
                && hit.collider.transform.IsChildOf(drawer.transform))
            {
                bottom = hit.point.y;
            }

            at = new Vector3(b.center.x, bottom + CardSize.y * 0.5f + 0.002f, b.center.z);
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

    // ── 열쇠함 ───────────────────────────────────────────────

    private void FindDrawer()
    {
        if (_drawerSearched) return;
        _drawerSearched = true;
        GameObject box = GameObject.Find("/" + KeyBoxPath);
        if (box == null)
        {
            Debug.LogWarning("[DutyCards] 경비실 열쇠함(LibraryCardCatalogB)을 찾지 못했습니다: " + KeyBoxPath);
            return;
        }

        // 서랍 상자 콜라이더(작고 납작한 것) 중 가로 면적이 가장 큰 것 = 가장 많이 열린 서랍.
        float best = 0f;
        foreach (BoxCollider c in box.GetComponentsInChildren<BoxCollider>(true))
        {
            Bounds b = c.bounds;
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

    /// <summary>열쇠함 몸통에서 서랍이 나온 쪽(수평 단위 벡터, 없으면 +z).</summary>
    private Vector3 KeyBoxOutward()
    {
        GameObject box = GameObject.Find("/" + KeyBoxPath);
        if (box == null) return Vector3.forward;
        Vector3 d = _drawer.center - box.transform.position;
        d.y = 0f;
        Vector3 f = box.transform.forward;
        f.y = 0f;
        if (f.sqrMagnitude < 1e-4f) return Vector3.forward;
        f.Normalize();
        return Vector3.Dot(f, d) >= 0f ? f : -f;
    }

    private static Transform KeyBoxOutline()
    {
        GameObject box = GameObject.Find("/" + KeyBoxPath);
        return box != null ? box.transform : null;
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

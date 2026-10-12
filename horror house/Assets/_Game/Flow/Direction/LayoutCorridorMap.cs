using System.Collections.Generic;
using NightDuty;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 104차 배치 지도 — 복도(사용자: 「축이 오를 때마다 벽이나 천장에 오브젝트가 생긴다던가, 복도에 가짜 문들이 늘어난다던가 하는 기괴한 변화 · 핏방울도 구간 밸런스에 맞춰」).
/// 복도 배치 구간(<see cref="NightRun.ShownBand"/>)이 오를수록 빈 벽에 <b>없던 문</b>이 늘고, 천장에 핏방울(김진선님 <c>HorrorProp_CeilingBloodDrip</c>)이 번지고, 끝에는 천장에 의자가 거꾸로 붙는다.
/// <list type="bullet">
/// <item>구간별 수(<see cref="Doors"/>·<see cref="Walled"/>·<see cref="Ajar"/>·<see cref="Drips"/>·<see cref="Chairs"/>): 구간 1 문 2+핏방울 2 · 구간 2 문 4(하나는 열린 문 너머가 벽돌로 막힘)+핏방울 3 ·
/// 구간 3 문 7(막힘 2 · 살짝 열려 안이 새카만 문 1)+핏방울 6+천장 의자 2 · 구간 4 문 12(막힘 3·열림 2)+핏방울 9+천장 의자 5. 아래 구간 것은 그대로 남는다(같은 자리·같은 모양).</item>
/// <item>104차(사용자: 「도서관 옆 화장실로 이어가는 복도에도 변화들을」): 본 복도 상자 밖 <see cref="HallBox"/>(x 6~24 · z 28~40 — 도서관 옆에서 화장실로 내려가는 복도·홀, 방 상자 밖)도 훑는다.
/// 본 복도 자리와 이 복도 자리를 번갈아 써서(본 복도 먼저) 구간마다 두 곳에 고르게 생긴다. 구간은 둘 다 복도 배치 구간.</item>
/// <item>104차(사용자: 「벽돌이 지금 벽밖에 없는데 원래 문이 열리고 벽돌 벽이 보여야 하는 거 아니야?」): 막힌 문 = 없던 문이 활짝 열려(<see cref="WalledOpenDegrees"/>°) 문틀 안이 벽돌로 막힌 모습.
/// 벽돌 판은 NOT_Lonely 테셀레이션 셰이더가 이 자리(빛 탐침)에서 혼자 하얗게 떠서 같은 그림의 URP Lit으로 바꿔 그린다.</item>
/// <item>하나씩 <b>안 볼 때</b> 생긴다(그 자리의 몸 여러 점이 모두 화면 밖이거나 가려짐). 밤 시작(경비실)에는 그 구간 것이 다 서 있다.</item>
/// <item>자리: 복도 상자를 0.5m 간격으로 훑어 네 방향의 벽면(천장은 복도 단면 <see cref="TreeSpots.Sites"/>의 가운데) — 이름에 Wall이 든 충돌체가 문 너비·키만큼 평평하게 이어지고, 앞 0.6m가 비었고, 천장 2.5m 이상,
/// 판정 표식(문·점검 대상)·진짜 문·소화기·음수대(그림 상자 가운데)에서 <see cref="AvoidRadius"/>m 밖, 현관·방 문간 밖. 문끼리 <see cref="DoorSpacing"/>m · 천장끼리 <see cref="CeilingSpacing"/>m 띄운다.
/// 어느 자리를 쓸지는 일차로 섞어 밤마다 다르다.</item>
/// <item>가짜 문은 열리지 않는다(상호작용 없음 — 판정과 무관). 문에는 몸 충돌체만(지나가지 못하게, 벽에 붙어 있어 동선은 그대로). 의자·핏방울은 충돌체 없음.</item>
/// <item>재시작·새 밤이면 모두 거두고 그 구간에 맞게 다시 세운다. 씬·원본 프리팹은 고치지 않는다(<c>Resources/LayoutProps</c> 사본 · 없던 문 대역).</item>
/// </list>
/// </summary>
[DisallowMultipleComponent]
public sealed class LayoutCorridorMap : MonoBehaviour
{
    public const string DoorPath = "StandIns/prop.phantomdoor";
    public const string WalledDoorPath = "LayoutProps/walled_door";
    public const string ChairPath = "LayoutProps/ceiling_chair";
    public const string DripPath = "LayoutProps/blood_drip";

    /// <summary>구간(0~4)별 없던 문 수(막힌 문·열린 문 포함).</summary>
    public static readonly int[] Doors = { 0, 2, 4, 7, 12 };

    /// <summary>그중 벽돌로 막힌 문.</summary>
    public static readonly int[] Walled = { 0, 0, 1, 2, 3 };

    /// <summary>그중 살짝 열려 안이 새카만 문.</summary>
    public static readonly int[] Ajar = { 0, 0, 0, 1, 2 };

    /// <summary>천장 핏방울.</summary>
    public static readonly int[] Drips = { 0, 2, 3, 6, 9 };

    /// <summary>천장에 거꾸로 붙은 의자.</summary>
    public static readonly int[] Chairs = { 0, 0, 0, 2, 5 };

    /// <summary>문 순서 중 벽돌 문·열린 문 자리(아래 구간 것이 위 구간에서도 같은 모양이게 순서로 정한다).</summary>
    private static readonly int[] WalledSlots = { 3, 5, 9 };
    private static readonly int[] AjarSlots = { 4, 10 };

    /// <summary>104차: 도서관 옆에서 화장실로 이어지는 복도·홀(본 복도 상자 밖 — PlayScene 실측, 방 상자 안은 뺀다).</summary>
    public static readonly Bounds HallBox = new Bounds(new Vector3(15f, 3.4f, 34.1f), new Vector3(18f, 4f, 12.2f));

    /// <summary>104차: 벽돌로 막힌 문의 문짝을 여는 각도.</summary>
    public const float WalledOpenDegrees = 100f;

    /// <summary>104차: 이 복도·홀에서 벽을 찾는 거리(m) — 본 복도보다 넓다.</summary>
    public const float HallReach = 4f;

    public const float DoorSpacing = 2.0f;
    public const float CeilingSpacing = 3.5f;
    public const float AvoidRadius = 1.5f;
    public const float AjarDegrees = 28f;
    public const float CheckEvery = 0.25f;

    /// <summary>검수용 — 0~4로 구간을 강제(−1이면 실제 연출 구간).</summary>
    public static int DebugBand = -1;

    public static LayoutCorridorMap Active { get; private set; }

    private enum Kind
    {
        Door,
        Walled,
        Ajar,
        Drip,
        Chair,
    }

    private sealed class Item
    {
        public Kind Kind;
        public Vector3 At;
        public Quaternion Rot;
        public Vector3[] Probe;
        public GameObject Go;
    }

    private struct Spot
    {
        public Vector3 At;       // 벽: 벽면 바닥 점 / 천장: 천장 점
        public Vector3 Normal;   // 벽: 복도 쪽 / 천장: 아래
        public bool Hall;        // 104차: 화장실로 이어지는 복도·홀 자리
    }

    private static readonly List<Spot> s_walls = new List<Spot>();
    private static readonly List<Spot> s_ceilings = new List<Spot>();
    private static int s_scene = -1;

    private readonly List<Item> _doors = new List<Item>();
    private readonly List<Item> _drips = new List<Item>();
    private readonly List<Item> _chairs = new List<Item>();
    private object _plan;
    private float _next;
    private int _band;
    private Material _void;
    private Material _brick;

    /// <summary>지금 쓰는 구간(검수용).</summary>
    public int Band
    {
        get { return _band; }
    }

    /// <summary>서 있는 것 수(문, 핏방울, 의자 — 검수용).</summary>
    public Vector3Int ShownCounts
    {
        get { return new Vector3Int(Count(_doors), Count(_drips), Count(_chairs)); }
    }

    /// <summary>찾은 자리 수(벽, 천장 — 검수용).</summary>
    public static Vector2Int SpotCounts
    {
        get { return new Vector2Int(s_walls.Count, s_ceilings.Count); }
    }

    private static int Count(List<Item> list)
    {
        int n = 0;
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i].Go != null) n++;
        }

        return n;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Install()
    {
        Active = null;
        DebugBand = -1;
        s_scene = -1;
        s_walls.Clear();
        s_ceilings.Clear();
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
        if (!FlowAutoInstall.IsDutyScene(scene) || FlowAutoInstall.Exists<LayoutCorridorMap>(scene)) return;
        FlowAutoInstall.CreateHost<LayoutCorridorMap>(scene, "LayoutCorridorMap (auto)");
    }

    private void OnEnable()
    {
        Active = this;
        EventBus.NightRestarted += OnRestarted;
    }

    private void OnDisable()
    {
        EventBus.NightRestarted -= OnRestarted;
        ClearAll();
        if (Active == this) Active = null;
    }

    private void OnRestarted(RestartResult result)
    {
        ClearAll();
        _plan = null;   // 다음 프레임에 그 밤에 맞게 다시 고른다
    }

    private void Update()
    {
        if (!NightRun.IsNightActive) return;
        object plan = NightRun.Inspections != null ? NightRun.Inspections.Plan : null;
        if (!ReferenceEquals(plan, _plan))
        {
            _plan = plan;
            NewNight();
        }

        if (Time.unscaledTime < _next) return;
        _next = Time.unscaledTime + CheckEvery;
        _band = DebugBand >= 0 ? Mathf.Clamp(DebugBand, 0, 4) : Mathf.Clamp((int)NightRun.ShownBand(SpaceId.Corridor, FearAxis.Layout), 0, 4);
        Camera cam = Camera.main;
        Sync(_doors, Doors[_band], cam);
        Sync(_drips, Drips[_band], cam);
        Sync(_chairs, Chairs[_band], cam);
    }

    /// <summary>검수용 — 지금 구간의 것을 보든 말든 바로 세운다.</summary>
    public void DebugShowNow()
    {
        if (_doors.Count == 0 && _drips.Count == 0) NewNight();
        _band = DebugBand >= 0 ? Mathf.Clamp(DebugBand, 0, 4) : Mathf.Clamp((int)NightRun.ShownBand(SpaceId.Corridor, FearAxis.Layout), 0, 4);
        Sync(_doors, Doors[_band], null);
        Sync(_drips, Drips[_band], null);
        Sync(_chairs, Chairs[_band], null);
    }

    private void Sync(List<Item> list, int want, Camera cam)
    {
        for (int i = 0; i < list.Count; i++)
        {
            Item it = list[i];
            if (i < want)
            {
                if (it.Go == null && (cam == null || Hidden(it.Probe, cam))) Spawn(it);
            }
            else if (it.Go != null)
            {
                Destroy(it.Go);   // 재시작으로 구간이 내려갔다
                it.Go = null;
            }
        }
    }

    private void ClearAll()
    {
        foreach (List<Item> list in new[] { _doors, _drips, _chairs })
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Go != null) Destroy(list[i].Go);
            }

            list.Clear();
        }
    }

    private void NewNight()
    {
        ClearAll();
        Scan();
        System.Random rng = new System.Random(NightRun.Day * 7919 + 104);
        List<Spot> walls = Alternate(Shuffled(s_walls, rng));
        int doorCount = Mathf.Min(Doors[4], walls.Count);
        for (int i = 0; i < doorCount; i++)
        {
            Spot s = walls[i];
            Kind k = System.Array.IndexOf(WalledSlots, i) >= 0 ? Kind.Walled : System.Array.IndexOf(AjarSlots, i) >= 0 ? Kind.Ajar : Kind.Door;
            Quaternion rot = Quaternion.LookRotation(s.Normal, Vector3.up);
            Vector3 t = Vector3.Cross(Vector3.up, s.Normal);
            Vector3 c = s.At + s.Normal * 0.1f;
            _doors.Add(new Item
            {
                Kind = k,
                At = s.At + s.Normal * 0.05f,
                Rot = rot,
                Probe = new[] { c + Vector3.up * 0.3f, c + Vector3.up * 1.1f, c + Vector3.up * 2.0f, c + t * 0.5f + Vector3.up * 1.1f, c - t * 0.5f + Vector3.up * 1.1f, c + t * 0.5f + Vector3.up * 2.0f, c - t * 0.5f + Vector3.up * 0.3f },
            });
        }

        List<Spot> ceilings = Alternate(Shuffled(s_ceilings, rng));
        int k2 = 0;
        for (int i = 0; i < Drips[4] && k2 < ceilings.Count; i++, k2++) _drips.Add(CeilingItem(Kind.Drip, ceilings[k2], rng));
        for (int i = 0; i < Chairs[4] && k2 < ceilings.Count; i++, k2++) _chairs.Add(CeilingItem(Kind.Chair, ceilings[k2], rng));
        if (DirectionStage.Verbose) Debug.Log("[LayoutCorridorMap] " + NightRun.Day + "일차 — 벽 자리 " + s_walls.Count + " · 천장 자리 " + s_ceilings.Count);
    }

    private static Item CeilingItem(Kind kind, Spot s, System.Random rng)
    {
        Vector3 c = s.At;
        return new Item
        {
            Kind = kind,
            At = c,
            Rot = kind == Kind.Chair ? Quaternion.Euler(180f, (float)rng.NextDouble() * 360f, 0f) : Quaternion.identity,
            Probe = new[] { c + Vector3.down * 0.1f, c + Vector3.down * 0.9f, c + new Vector3(0.5f, -0.1f, 0f), c + new Vector3(-0.5f, -0.1f, 0f), c + new Vector3(0f, -0.1f, 0.5f), c + new Vector3(0f, -0.1f, -0.5f) },
        };
    }

    /// <summary>104차: 본 복도 자리와 화장실 쪽 복도 자리를 번갈아(본 복도 먼저) — 한쪽이 바닥나면 남은 쪽을 잇는다.</summary>
    private static List<Spot> Alternate(List<Spot> shuffled)
    {
        List<Spot> main = shuffled.FindAll(s => !s.Hall);
        List<Spot> hall = shuffled.FindAll(s => s.Hall);
        List<Spot> list = new List<Spot>(shuffled.Count);
        for (int i = 0; i < Mathf.Max(main.Count, hall.Count); i++)
        {
            if (i < main.Count) list.Add(main[i]);
            if (i < hall.Count) list.Add(hall[i]);
        }

        return list;
    }

    private static List<Spot> Shuffled(List<Spot> src, System.Random rng)
    {
        List<Spot> list = new List<Spot>(src);
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            Spot t = list[i];
            list[i] = list[j];
            list[j] = t;
        }

        return list;
    }

    private void Spawn(Item it)
    {
        string path = it.Kind == Kind.Drip ? DripPath : it.Kind == Kind.Chair ? ChairPath : DoorPath;   // 104차: 막힌 문도 없던 문(열어서 벽돌 판을 댄다)
        GameObject prefab = Resources.Load<GameObject>(path);
        if (prefab == null)
        {
            Debug.LogWarning("[LayoutCorridorMap] 소품이 없습니다: Resources/" + path + " (야간근무/연출/배치 지도 소품 다시 만들기)");
            return;
        }

        GameObject go = Instantiate(prefab, it.At, it.Rot);
        go.name = "배치 지도 · " + it.Kind;
        foreach (Collider c in go.GetComponentsInChildren<Collider>(true)) c.enabled = false;
        if (it.Kind == Kind.Door || it.Kind == Kind.Ajar || it.Kind == Kind.Walled)
        {
            BoxCollider body = go.AddComponent<BoxCollider>();   // 벽에 붙은 문틀을 뚫고 지나가지 않게
            body.center = new Vector3(0f, 1.1f, 0.02f);
            body.size = new Vector3(1.2f, 2.2f, 0.22f);
        }

        if (it.Kind == Kind.Ajar) OpenAjar(go);
        if (it.Kind == Kind.Walled) OpenWalled(go);
        if (it.Kind == Kind.Drip)
        {
            foreach (AudioSource a in go.GetComponentsInChildren<AudioSource>(true)) NightDutyMixer.Route(a, NightDutyMixer.Bus.World);
        }

        it.Go = go;
    }

    private static Transform JointOf(GameObject go)
    {
        foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == "Joint") return t;
        }

        return null;
    }

    /// <summary>
    /// 104차: 문짝을 활짝 열고 문틀 안에 벽돌 판(<see cref="WalledDoorPath"/> — 김진선님 NOT_Lonely <c>WalledDoor</c> 사본)을 댄다.
    /// 벽돌 판은 같은 그림·노멀의 URP Lit으로 다시 칠한다(원래 셰이더가 정적이 아닌 물체에서 빛을 너무 받아 혼자 하얗게 떴다).
    /// </summary>
    private void OpenWalled(GameObject go)
    {
        Transform joint = JointOf(go);
        if (joint != null) joint.localRotation = Quaternion.Euler(0f, -WalledOpenDegrees, 0f) * joint.localRotation;
        GameObject prefab = Resources.Load<GameObject>(WalledDoorPath);
        if (prefab == null) return;
        GameObject brick = Instantiate(prefab, go.transform, false);
        brick.name = "문 안 벽돌";
        brick.transform.localPosition = new Vector3(0f, 0.01f, -0.03f);
        brick.transform.localRotation = Quaternion.identity;
        brick.transform.localScale = new Vector3(0.84f, 0.94f, 1f);   // 문틀 안(약 0.98 × 2.08m)에 맞춘다 — 원래 판은 1.17 × 2.21m
        foreach (Collider c in brick.GetComponentsInChildren<Collider>(true)) c.enabled = false;
        foreach (Renderer r in brick.GetComponentsInChildren<Renderer>(true))
        {
            if (_brick == null) _brick = Relit(r.sharedMaterial);
            if (_brick != null) r.sharedMaterial = _brick;
        }
    }

    private static Material Relit(Material src)
    {
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        if (src == null || lit == null) return null;
        Material m = new Material(lit) { name = "배치 지도 · 벽돌(URP Lit)" };
        Texture albedo = Tex(src, "_Albedo", "_MainTex", "_BaseMap");
        Texture normal = Tex(src, "_Normal", "_BumpMap");
        if (albedo != null) m.SetTexture("_BaseMap", albedo);
        if (normal != null)
        {
            m.SetTexture("_BumpMap", normal);
            m.EnableKeyword("_NORMALMAP");
        }

        m.SetFloat("_Smoothness", 0.15f);
        m.SetFloat("_Metallic", 0f);
        return m;
    }

    private static Texture Tex(Material m, params string[] names)
    {
        for (int i = 0; i < names.Length; i++)
        {
            if (m.HasProperty(names[i]) && m.GetTexture(names[i]) != null) return m.GetTexture(names[i]);
        }

        return null;
    }

    /// <summary>문짝 경첩(Joint)을 복도 쪽으로 열고, 문틀 안에 새카만 판을 댄다.</summary>
    private void OpenAjar(GameObject go)
    {
        Transform joint = JointOf(go);
        if (joint != null) joint.localRotation = Quaternion.Euler(0f, -AjarDegrees, 0f) * joint.localRotation;
        if (_void == null)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Unlit");
            _void = new Material(sh != null ? sh : Shader.Find("Universal Render Pipeline/Lit")) { name = "배치 지도 · 문 안 어둠" };
            _void.color = Color.black;
            if (_void.HasProperty("_BaseColor")) _void.SetColor("_BaseColor", Color.black);
        }

        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(quad.GetComponent<Collider>());
        quad.name = "문 안 어둠";
        quad.transform.SetParent(go.transform, false);
        quad.transform.localPosition = new Vector3(0f, 1.05f, -0.03f);
        quad.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);   // 기본 사각판은 −z 쪽에서 보인다 — 복도(+z) 쪽으로
        quad.transform.localScale = new Vector3(0.98f, 2.08f, 1f);
        Renderer r = quad.GetComponent<Renderer>();
        r.sharedMaterial = _void;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    /// <summary>그 점들이 모두 화면 밖이거나 가려졌는지.</summary>
    private static bool Hidden(Vector3[] pts, Camera cam)
    {
        Vector3 eye = cam.transform.position;
        PlayerSensors hub = PlayerSensors.Active;
        Transform player = hub != null ? hub.PlayerRoot : null;
        for (int i = 0; i < pts.Length; i++)
        {
            Vector3 v = cam.WorldToViewportPoint(pts[i]);
            if (v.z <= 0f || v.x < -0.1f || v.x > 1.1f || v.y < -0.1f || v.y > 1.1f) continue;
            Vector3 d = pts[i] - eye;
            float len = d.magnitude;
            if (len < 0.05f) return false;
            bool blocked = false;
            foreach (RaycastHit h in Physics.RaycastAll(eye, d / len, len - 0.15f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (player != null && h.collider.transform.IsChildOf(player)) continue;
                blocked = true;
                break;
            }

            if (!blocked) return false;
        }

        return true;
    }

    // ── 자리 훑기(씬마다 한 번) ─────────────────────────────

    private static void Scan()
    {
        int scene = SceneManager.GetActiveScene().handle;
        if (s_scene == scene) return;
        s_scene = scene;
        s_walls.Clear();
        s_ceilings.Clear();
        List<Vector3> avoid = new List<Vector3>();
        foreach (Transform t in FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            string n = t.name;
            if (!(n.StartsWith("Door") || n.StartsWith("Inspect ") || n.StartsWith("ToiletCabin") || n.StartsWith("WalledDoor") || n.StartsWith("FireExtinguisher") || n.StartsWith("DrinkingFountain"))) continue;   // 사물함은 문 앞 상자 검사가 겹침만 막는다
            Renderer r = t.GetComponentInChildren<Renderer>();
            if (r != null && (r.bounds.size.x > 3.5f || r.bounds.size.z > 3.5f)) continue;   // 문 묶음 상자(통째 그룹)는 건너뛴다 — 낱개 문이 따로 잡힌다
            avoid.Add(r != null ? r.bounds.center : t.position);   // 피벗이 경첩 끝이라 그림 가운데로 잰다
        }

        List<JudgeTarget> marks = new List<JudgeTarget>();
        JudgeTargetRegistry.CollectOwners(marks);
        for (int i = 0; i < marks.Count; i++)
        {
            if (marks[i] != null) avoid.Add(marks[i].AnchorPosition);
        }

        // 벽: 복도 상자를 0.5m 간격으로 훑어 네 방향의 벽면을 모은다(단면만 보면 출입구·갈림길 근처 벽이 빠진다).
        SpaceZones zones = FindAnyObjectByType<SpaceZones>();
        Bounds box;
        if (zones != null && zones.TryGetSpaceBox(SpaceId.Corridor, out box))
        {
            Vector3[] dirs = { Vector3.right, Vector3.left, Vector3.forward, Vector3.back };
            for (float x = box.min.x; x <= box.max.x; x += 0.5f)
            {
                for (float z = box.min.z; z <= box.max.z; z += 0.5f)
                {
                    RaycastHit fh;
                    if (!Physics.Raycast(new Vector3(x, box.min.y + 1.6f, z), Vector3.down, out fh, 2.2f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
                    if (fh.normal.y < 0.8f || Physics.CheckSphere(fh.point + Vector3.up, 0.25f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
                    if (!TreeSpots.InCorridor(fh.point, fh.point.y)) continue;
                    for (int d = 0; d < dirs.Length; d++) TryWall(fh.point, dirs[d], avoid, false);
                }
            }
        }

        // 104차: 도서관 옆 → 화장실 복도·홀 — 벽과 천장을 같은 격자로.
        List<Bounds> rooms = Rooms(zones);
        Vector3[] four = { Vector3.right, Vector3.left, Vector3.forward, Vector3.back };
        for (float x = HallBox.min.x; x <= HallBox.max.x; x += 0.5f)
        {
            for (float z = HallBox.min.z; z <= HallBox.max.z; z += 0.5f)
            {
                RaycastHit fh;
                if (!Physics.Raycast(new Vector3(x, HallBox.min.y + 1.6f, z), Vector3.down, out fh, 2.2f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
                if (fh.normal.y < 0.8f || fh.point.y > HallBox.min.y + 0.5f || Physics.CheckSphere(fh.point + Vector3.up, 0.25f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
                if (!InHall(rooms, fh.point)) continue;
                TryHallCeiling(fh.point);
                for (int d = 0; d < four.Length; d++) TryWall(fh.point, four[d], avoid, true, rooms);
            }
        }

        foreach (TreeSpots.Site site in TreeSpots.Sites()) TryCeiling(site);
    }

    private static bool Near(List<Vector3> pts, Vector3 p, float r)
    {
        for (int i = 0; i < pts.Count; i++)
        {
            Vector3 d = pts[i] - p;
            if (Mathf.Abs(d.y) > 3f) continue;
            d.y = 0f;
            if (d.magnitude < r) return true;
        }

        return false;
    }

    private static bool WallHit(Vector3 from, Vector3 dir, float max, out RaycastHit hit)
    {
        RaycastHit[] hits = Physics.RaycastAll(from, dir, max, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        PlayerSensors hub = PlayerSensors.Active;
        Transform player = hub != null ? hub.PlayerRoot : null;
        for (int i = 0; i < hits.Length; i++)
        {
            if (player != null && hits[i].collider.transform.IsChildOf(player)) continue;
            hit = hits[i];
            return hit.collider.name.IndexOf("Wall", System.StringComparison.OrdinalIgnoreCase) >= 0 && Mathf.Abs(hit.normal.y) < 0.2f;
        }

        hit = new RaycastHit();
        return false;
    }

    /// <summary>104차: 방 상자(조금 넓혀) — 화장실 쪽 복도·홀 자리는 이 밖이어야.</summary>
    private static List<Bounds> Rooms(SpaceZones zones)
    {
        List<Bounds> list = new List<Bounds>();
        if (zones == null) return list;
        SpaceId[] rooms = { SpaceId.Toilet, SpaceId.Library, SpaceId.Classroom_1_1, SpaceId.Classroom_1_3, SpaceId.ScienceRoom, SpaceId.SecurityRoom };
        for (int i = 0; i < rooms.Length; i++)
        {
            Bounds b;
            if (!zones.TryGetSpaceBox(rooms[i], out b)) continue;
            b.Expand(new Vector3(0.6f, 10f, 0.6f));
            list.Add(b);
        }

        return list;
    }

    private static bool InHall(List<Bounds> rooms, Vector3 p)
    {
        if (p.x < HallBox.min.x || p.x > HallBox.max.x || p.z < HallBox.min.z || p.z > HallBox.max.z) return false;
        for (int i = 0; i < rooms.Count; i++)
        {
            if (rooms[i].Contains(p)) return false;
        }

        return true;
    }

    /// <summary>104차: 화장실 쪽 복도·홀 천장 — 바닥 점 위, 아래를 보는 천장 2.5m 이상, 등 아래가 아니고, 벽에서 0.9m 떨어진 곳.</summary>
    private static void TryHallCeiling(Vector3 floor)
    {
        RaycastHit hit;
        if (!Physics.Raycast(floor + Vector3.up * 0.5f, Vector3.up, out hit, 4.5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return;
        if (hit.normal.y > -0.8f || hit.distance + 0.5f < 2.5f) return;
        if (Physics.CheckSphere(hit.point + Vector3.down * 0.55f, 0.4f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return;
        Vector3[] four = { Vector3.right, Vector3.left, Vector3.forward, Vector3.back };
        for (int d = 0; d < four.Length; d++)
        {
            if (Physics.Raycast(floor + Vector3.up * 1.5f, four[d], 0.9f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return;
        }

        for (int i = 0; i < s_ceilings.Count; i++)
        {
            if ((s_ceilings[i].At - hit.point).magnitude < CeilingSpacing) return;
        }

        s_ceilings.Add(new Spot { At = hit.point, Normal = Vector3.down, Hall = true });
    }

    private static void TryWall(Vector3 floor, Vector3 dir, List<Vector3> avoid, bool hall, List<Bounds> rooms = null)
    {
        RaycastHit hit;
        if (!WallHit(floor + Vector3.up * 1.2f, dir, hall ? HallReach : TreeSpots.MaxHalfWidth + 0.5f, out hit)) return;
        Vector3 n = hit.normal;
        n.y = 0f;
        if (n.sqrMagnitude < 0.5f) return;
        n.Normalize();
        if (Vector3.Dot(n, -dir) < 0.95f) return;
        Vector3 basePt = new Vector3(hit.point.x, floor.y, hit.point.z);
        for (int i = 0; i < s_walls.Count; i++)
        {
            if ((s_walls[i].At - basePt).magnitude < DoorSpacing) return;
        }

        Vector3 t = Vector3.Cross(Vector3.up, n);
        float[] along = { -0.62f, 0f, 0.62f };
        float[] heights = { 1.2f, 2.0f };   // 문 키(2.2m) 바로 아래까지 — 그 위 창·몰딩은 문틀이, 아래 걸레받이는 문짝이 가린다(앞 상자 검사가 낮은 물건을 막는다)
        for (int a = 0; a < along.Length; a++)
        {
            for (int h = 0; h < heights.Length; h++)
            {
                RaycastHit w;
                Vector3 o = basePt + t * along[a] + Vector3.up * heights[h] + n * 0.6f;
                if (!WallHit(o, -n, 0.9f, out w) || Mathf.Abs(w.distance - 0.6f) > 0.1f) return;   // 평평한 벽이 문 너비·키만큼 이어져야
            }
        }

        RaycastHit ceil;
        if (!Physics.Raycast(basePt + n * 0.3f + Vector3.up * 0.3f, Vector3.up, out ceil, 5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) || ceil.distance + 0.3f < 2.5f) return;
        if (Physics.CheckBox(basePt + n * 0.36f + Vector3.up * 1.2f, new Vector3(0.62f, 1.05f, 0.28f), Quaternion.LookRotation(n, Vector3.up), Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return;   // 문 앞이 비어 있어야
        if (hall ? !InHall(rooms, basePt + n * 0.4f) : !TreeSpots.InCorridor(basePt + n * 0.4f, floor.y)) return;
        if (Near(avoid, basePt, AvoidRadius)) return;
        s_walls.Add(new Spot { At = basePt, Normal = n, Hall = hall });
    }

    private static void TryCeiling(TreeSpots.Site site)
    {
        Vector3 c = site.Floor + site.WallDir * (site.ToWall - site.Width * 0.5f);
        if (!TreeSpots.InCorridor(c, site.Floor.y)) return;
        RaycastHit hit;
        if (!Physics.Raycast(c + Vector3.up * 0.5f, Vector3.up, out hit, 4.5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return;
        if (hit.normal.y > -0.8f || hit.distance + 0.5f < 2.5f) return;
        if (Physics.CheckSphere(hit.point + Vector3.down * 0.55f, 0.4f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return;   // 등·배관 아래가 아니게
        for (int i = 0; i < s_ceilings.Count; i++)
        {
            if ((s_ceilings[i].At - hit.point).magnitude < CeilingSpacing) return;
        }

        s_ceilings.Add(new Spot { At = hit.point, Normal = Vector3.down });
    }
}

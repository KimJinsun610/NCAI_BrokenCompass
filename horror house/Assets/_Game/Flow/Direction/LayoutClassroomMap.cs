using System.Collections.Generic;
using NightDuty;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 104차 배치 지도 — 교실(사용자: 「교실도 복도랑 같이 기획 · 변화는 그냥 천장에 책상과 의자, 벽시계 복제, 칠판 복제, 핏물 정도」).
/// 교실(1-3) 배치 구간(<see cref="NightRun.ShownBand"/>)이 오를수록 천장에 책상·의자가 거꾸로 붙고, 벽에 벽시계·칠판이 하나씩 더 걸리고, 천장에서 핏물이 떨어져 바닥에 고인다.
/// <list type="bullet">
/// <item>구간(0~4)별 수: 벽시계 <see cref="Clocks"/> · 칠판 <see cref="Boards"/> · 천장 책상 <see cref="Desks"/> · 천장 의자 <see cref="Chairs"/> · 핏물 <see cref="Drips"/>(천장 핏방울 + 그 아래 바닥 핏물 웅덩이).
/// 아래 구간 것은 같은 자리·모양으로 남는다. 하나씩 <b>안 볼 때</b> 생긴다(<see cref="LayoutCorridorMap"/>와 같은 규칙).</item>
/// <item>기존 물건은 옮기지 않는다 — 교실 점검(C-1 화분 · C-3 사다리 · C-4 시계 · C-5 교사 의자)과 헷갈리지 않게. 원래 시계·칠판·점검 표식·문에서 <see cref="AvoidRadius"/>m 밖에만 건다.</item>
/// <item>자리: 교실 상자를 0.5m 격자로 훑어 네 방향의 벽(이름에 Wall · 걸 넓이만큼 평평 · 앞이 비었음), 천장(아래를 보는 2.5m 이상 · 등 아래 아님 · 벽에서 0.8m). 일차로 섞어 밤마다 다르다.</item>
/// <item>소품은 <c>Resources/LayoutProps</c> 사본(ceiling_desk·ceiling_chair·wall_clock·blackboard·blood_drip·blood_pool — <c>LayoutPropsBuilder</c>). 충돌체 없음 — 동선은 그대로.</item>
/// </list>
/// </summary>
[DisallowMultipleComponent]
public sealed class LayoutClassroomMap : MonoBehaviour
{
    public const string DeskPath = "LayoutProps/ceiling_desk";
    public const string ChairPath = "LayoutProps/ceiling_chair";
    public const string ClockPath = "LayoutProps/wall_clock";
    public const string BoardPath = "LayoutProps/blackboard";
    public const string DripPath = "LayoutProps/blood_drip";
    public const string PoolPath = "LayoutProps/blood_pool";

    public static readonly int[] Clocks = { 0, 1, 2, 4, 6 };
    public static readonly int[] Boards = { 0, 0, 1, 2, 3 };
    public static readonly int[] Desks = { 0, 0, 1, 2, 4 };
    public static readonly int[] Chairs = { 0, 1, 2, 3, 5 };
    public static readonly int[] Drips = { 0, 1, 2, 3, 5 };

    public const float AvoidRadius = 1.5f;
    public const float BoardSpacing = 2.4f;
    public const float ClockSpacing = 1.4f;
    public const float CeilingSpacing = 2.2f;
    public const float CheckEvery = 0.25f;

    /// <summary>칠판 사본 배율(원래 가로 2m · 세로 1.26m → 1.6 × 1.0m)과 반폭·가운데 높이.</summary>
    private const float BoardScale = 0.8f;
    private const float BoardHalf = 0.85f;
    private const float BoardCenter = 2.85f;   // 벽 아래 타일 턱·책장(1.8m) 위 — 교실 벽은 1.6m까지 타일이라 그 아래는 평평하지 않다

    /// <summary>벽시계 높이(바닥에서, m — 원래 시계 3.0m 언저리).</summary>
    private const float ClockMin = 2.6f;
    private const float ClockMax = 3.3f;

    /// <summary>검수용 — 0~4로 구간을 강제(−1이면 실제 연출 구간).</summary>
    public static int DebugBand = -1;

    public static LayoutClassroomMap Active { get; private set; }

    private enum Kind
    {
        Clock,
        Board,
        Desk,
        Chair,
        Drip,
    }

    private sealed class Item
    {
        public Kind Kind;
        public Vector3 At;
        public Quaternion Rot;
        public Vector3 Floor;
        public Vector3[] Probe;
        public GameObject Go;
        public GameObject Pool;
    }

    private struct Spot
    {
        public Vector3 At;       // 벽: 벽면 바닥 점 / 천장: 천장 점
        public Vector3 Normal;
        public Vector3 Floor;    // 천장: 아래 바닥 점
    }

    private static readonly List<Spot> s_boards = new List<Spot>();
    private static readonly List<Spot> s_clocks = new List<Spot>();
    private static readonly List<Spot> s_ceilings = new List<Spot>();
    private static readonly List<Bounds> s_keepOut = new List<Bounds>();
    private static int s_scene = -1;

    private readonly List<Item> _clocks = new List<Item>();
    private readonly List<Item> _boards = new List<Item>();
    private readonly List<Item> _desks = new List<Item>();
    private readonly List<Item> _chairs = new List<Item>();
    private readonly List<Item> _drips = new List<Item>();
    private object _plan;
    private float _next;
    private int _band;

    public int Band
    {
        get { return _band; }
    }

    /// <summary>서 있는 수(시계, 칠판, 책상, 의자, 핏물 — 검수용).</summary>
    public string ShownCounts
    {
        get { return Count(_clocks) + "," + Count(_boards) + "," + Count(_desks) + "," + Count(_chairs) + "," + Count(_drips); }
    }

    /// <summary>찾은 자리 수(칠판, 시계, 천장 — 검수용).</summary>
    public static Vector3Int SpotCounts
    {
        get { return new Vector3Int(s_boards.Count, s_clocks.Count, s_ceilings.Count); }
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
        s_boards.Clear();
        s_clocks.Clear();
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
        if (!FlowAutoInstall.IsDutyScene(scene) || FlowAutoInstall.Exists<LayoutClassroomMap>(scene)) return;
        FlowAutoInstall.CreateHost<LayoutClassroomMap>(scene, "LayoutClassroomMap (auto)");
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
        _plan = null;
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
        SyncAll(Camera.main);
    }

    /// <summary>검수용 — 지금 구간의 것을 보든 말든 바로 세운다.</summary>
    public void DebugShowNow()
    {
        if (_clocks.Count == 0 && _drips.Count == 0 && _desks.Count == 0) NewNight();
        SyncAll(null);
    }

    private void SyncAll(Camera cam)
    {
        _band = DebugBand >= 0 ? Mathf.Clamp(DebugBand, 0, 4) : Mathf.Clamp((int)NightRun.ShownBand(SpaceId.Classroom, FearAxis.Layout), 0, 4);
        Sync(_clocks, Clocks[_band], cam);
        Sync(_boards, Boards[_band], cam);
        Sync(_desks, Desks[_band], cam);
        Sync(_chairs, Chairs[_band], cam);
        Sync(_drips, Drips[_band], cam);
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
                Destroy(it.Go);
                if (it.Pool != null) Destroy(it.Pool);
                it.Go = null;
                it.Pool = null;
            }
        }
    }

    private void ClearAll()
    {
        foreach (List<Item> list in new[] { _clocks, _boards, _desks, _chairs, _drips })
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Go != null) Destroy(list[i].Go);
                if (list[i].Pool != null) Destroy(list[i].Pool);
            }

            list.Clear();
        }
    }

    private void NewNight()
    {
        ClearAll();
        Scan();
        System.Random rng = new System.Random(NightRun.Day * 6151 + 1043);
        List<Spot> clocks = Shuffled(s_clocks, rng);
        for (int i = 0; i < Mathf.Min(Clocks[4], clocks.Count); i++)
        {
            Spot s = clocks[i];
            float h = ClockMin + (float)rng.NextDouble() * (ClockMax - ClockMin);
            Vector3 at = s.At + Vector3.up * h + s.Normal * 0.04f;
            _clocks.Add(new Item { Kind = Kind.Clock, At = at, Rot = Quaternion.LookRotation(s.Normal, Vector3.up), Probe = Around(at, s.Normal, 0.2f, 0.2f) });
        }

        List<Spot> boards = Shuffled(s_boards, rng);
        for (int i = 0; i < Mathf.Min(Boards[4], boards.Count); i++)
        {
            Spot s = boards[i];
            Vector3 at = s.At + Vector3.up * BoardCenter + s.Normal * 0.05f;
            _boards.Add(new Item { Kind = Kind.Board, At = at, Rot = Quaternion.LookRotation(s.Normal, Vector3.up), Probe = Around(at, s.Normal, 0.95f, 0.6f) });
        }

        List<Spot> ceilings = Shuffled(s_ceilings, rng);
        int k = 0;
        for (int i = 0; i < Desks[4] && k < ceilings.Count; i++, k++) _desks.Add(CeilingItem(Kind.Desk, ceilings[k], rng));
        for (int i = 0; i < Chairs[4] && k < ceilings.Count; i++, k++) _chairs.Add(CeilingItem(Kind.Chair, ceilings[k], rng));
        for (int i = 0; i < Drips[4] && k < ceilings.Count; i++, k++) _drips.Add(CeilingItem(Kind.Drip, ceilings[k], rng));
        if (DirectionStage.Verbose) Debug.Log("[LayoutClassroomMap] " + NightRun.Day + "일차 — 칠판 자리 " + s_boards.Count + " · 시계 자리 " + s_clocks.Count + " · 천장 자리 " + s_ceilings.Count);
    }

    private static Vector3[] Around(Vector3 c, Vector3 n, float halfW, float halfH)
    {
        Vector3 t = Vector3.Cross(Vector3.up, n);
        Vector3 f = c + n * 0.05f;
        return new[] { f, f + t * halfW, f - t * halfW, f + Vector3.up * halfH, f - Vector3.up * halfH };
    }

    private static Item CeilingItem(Kind kind, Spot s, System.Random rng)
    {
        Vector3 c = s.At;
        Quaternion rot = kind == Kind.Drip ? Quaternion.identity : Quaternion.Euler(180f, (float)rng.NextDouble() * 360f, 0f);
        return new Item
        {
            Kind = kind,
            At = c,
            Rot = rot,
            Floor = s.Floor,
            Probe = new[] { c + Vector3.down * 0.1f, c + Vector3.down * 0.8f, c + new Vector3(0.5f, -0.1f, 0f), c + new Vector3(-0.5f, -0.1f, 0f), c + new Vector3(0f, -0.1f, 0.5f), c + new Vector3(0f, -0.1f, -0.5f), s.Floor + Vector3.up * 0.05f },
        };
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
        string path = it.Kind == Kind.Clock ? ClockPath : it.Kind == Kind.Board ? BoardPath : it.Kind == Kind.Desk ? DeskPath : it.Kind == Kind.Chair ? ChairPath : DripPath;
        GameObject prefab = Resources.Load<GameObject>(path);
        if (prefab == null)
        {
            Debug.LogWarning("[LayoutClassroomMap] 소품이 없습니다: Resources/" + path + " (야간근무/연출/배치 지도 소품 다시 만들기)");
            return;
        }

        GameObject go = Instantiate(prefab, it.At, it.Rot);
        go.name = "배치 지도 교실 · " + it.Kind;
        if (it.Kind == Kind.Board) go.transform.localScale = Vector3.one * BoardScale;
        foreach (Collider c in go.GetComponentsInChildren<Collider>(true)) c.enabled = false;
        foreach (AudioSource a in go.GetComponentsInChildren<AudioSource>(true)) NightDutyMixer.Route(a, NightDutyMixer.Bus.World);
        it.Go = go;
        if (it.Kind == Kind.Drip)
        {
            GameObject pool = Resources.Load<GameObject>(PoolPath);
            if (pool != null)
            {
                it.Pool = Instantiate(pool, it.Floor + Vector3.up * 0.02f, Quaternion.Euler(90f, (it.At.x * 37f + it.At.z * 11f) % 360f, 0f));
                it.Pool.name = "배치 지도 교실 · 핏물 웅덩이";
                foreach (AudioSource a in it.Pool.GetComponentsInChildren<AudioSource>(true)) NightDutyMixer.Route(a, NightDutyMixer.Bus.World);
            }
        }
    }

    /// <summary>그 점들이 모두 화면 밖이거나 가려졌는지(<see cref="LayoutCorridorMap"/>와 같은 규칙).</summary>
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
        s_boards.Clear();
        s_clocks.Clear();
        s_ceilings.Clear();
        SpaceZones zones = FindAnyObjectByType<SpaceZones>();
        Bounds box;
        if (zones == null || !zones.TryGetSpaceBox(SpaceId.Classroom_1_3, out box)) return;
        Bounds inner = box;
        inner.Expand(new Vector3(-0.2f, 10f, -0.2f));

        List<Vector3> avoid = new List<Vector3>();
        foreach (Transform t in FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            string n = t.name;
            if (!(n.StartsWith("Door") || n.StartsWith("Inspect ") || n.StartsWith("Clock") || n.StartsWith("Blackboard") || n.StartsWith("FireExtinguisher") || n.StartsWith("LightSwitch") || n.StartsWith("Sign_"))) continue;
            Renderer r = t.GetComponentInChildren<Renderer>();
            if (r != null && (r.bounds.size.x > 6f || r.bounds.size.z > 6f)) continue;
            avoid.Add(r != null ? r.bounds.center : t.position);
        }

        List<JudgeTarget> marks = new List<JudgeTarget>();
        JudgeTargetRegistry.CollectOwners(marks);
        for (int i = 0; i < marks.Count; i++)
        {
            if (marks[i] != null) avoid.Add(marks[i].AnchorPosition);
        }

        // 원래 칠판·시계·문·창·표지판의 그림 상자 — 큰 칠판(5m)은 가운데 거리로는 못 막는다.
        s_keepOut.Clear();
        Bounds near = box;
        near.Expand(new Vector3(1f, 10f, 1f));
        foreach (Renderer r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
        {
            Transform p = r.transform;
            bool hit = false;
            for (int up = 0; up < 3 && p != null && !hit; up++, p = p.parent)
            {
                string pn = p.name;
                hit = pn.StartsWith("Blackboard") || pn.StartsWith("Clock") || pn.StartsWith("Door") || pn.StartsWith("Jalousie") || pn.StartsWith("Window") || pn.StartsWith("Sign_") || pn.StartsWith("LightSwitch") || pn.StartsWith("FireExtinguisher");
            }

            if (!hit || !near.Intersects(r.bounds)) continue;
            Bounds b = r.bounds;
            b.Expand(0.4f);
            s_keepOut.Add(b);
        }

        Vector3[] dirs = { Vector3.right, Vector3.left, Vector3.forward, Vector3.back };
        for (float x = box.min.x; x <= box.max.x; x += 0.5f)
        {
            for (float z = box.min.z; z <= box.max.z; z += 0.5f)
            {
                RaycastHit fh;
                if (!Physics.Raycast(new Vector3(x, box.min.y + 1.6f, z), Vector3.down, out fh, 2.2f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
                if (fh.normal.y < 0.8f || fh.point.y > box.min.y + 0.5f) continue;
                for (int d = 0; d < dirs.Length; d++) TryWall(fh.point, dirs[d], avoid, inner);
                TryCeiling(fh.point, inner);
            }
        }

        s_clocks.RemoveAll(c => NearSpot(s_boards, c.At, BoardHalf + 0.6f));   // 훑는 순서상 칠판보다 먼저 잡힌 시계 자리도 칠판 옆이면 뺀다
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

    private static bool Near(List<Vector3> pts, Vector3 p, float r)
    {
        for (int i = 0; i < pts.Count; i++)
        {
            Vector3 d = pts[i] - p;
            if (Mathf.Abs(d.y) > 4f) continue;
            d.y = 0f;
            if (d.magnitude < r) return true;
        }

        return false;
    }

    /// <summary>벽면 <paramref name="basePt"/>(바닥 높이)에서 가로 ±<paramref name="half"/> × 그 높이들이 0.4m 앞에서 평평한 벽(±0.08)인지.</summary>
    private static bool Flat(Vector3 basePt, Vector3 n, float half, float[] heights)
    {
        Vector3 t = Vector3.Cross(Vector3.up, n);
        float[] along = { -half, 0f, half };
        for (int a = 0; a < along.Length; a++)
        {
            for (int h = 0; h < heights.Length; h++)
            {
                RaycastHit w;
                Vector3 o = basePt + t * along[a] + Vector3.up * heights[h] + n * 0.4f;
                if (!WallHit(o, -n, 0.7f, out w) || Mathf.Abs(w.distance - 0.4f) > 0.08f) return false;
            }
        }

        return true;
    }

    private static void TryWall(Vector3 floor, Vector3 dir, List<Vector3> avoid, Bounds inner)
    {
        RaycastHit hit;
        if (!WallHit(floor + Vector3.up * 1.9f, dir, 4f, out hit)) return;
        Vector3 n = hit.normal;
        n.y = 0f;
        if (n.sqrMagnitude < 0.5f) return;
        n.Normalize();
        if (Vector3.Dot(n, -dir) < 0.95f) return;
        Vector3 basePt = new Vector3(hit.point.x, floor.y, hit.point.z);
        if (!inner.Contains(basePt + n * 0.4f + Vector3.up)) return;   // 교실 안쪽 벽면만(복도 쪽 면이 아니게)
        RaycastHit ff;
        if (!Physics.Raycast(basePt + n * 0.6f + Vector3.up * 1.5f, Vector3.down, out ff, 2.0f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) || Mathf.Abs(ff.point.y - floor.y) > 0.3f) return;   // 벽 앞이 교실 바닥(옆 칸의 꺼진 바닥·딴 방이 아니게)
        RaycastHit ceil;
        if (!Physics.Raycast(basePt + n * 0.3f + Vector3.up * 2.0f, Vector3.up, out ceil, 4f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return;   // 책장(1.8m) 위에서 쏜다
        float room = ceil.distance + 2.0f;
        Quaternion face = Quaternion.LookRotation(n, Vector3.up);

        bool boardOk = room >= BoardCenter + 0.6f && Flat(basePt, n, BoardHalf, new[] { BoardCenter - 0.45f, BoardCenter, BoardCenter + 0.45f })
            && !Physics.CheckBox(basePt + n * 0.4f + Vector3.up * BoardCenter, new Vector3(BoardHalf, 0.5f, 0.33f), face, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)   // 앞 0.07~0.73m가 비어야(책장 뒤에 걸리지 않게)
            && !Near(avoid, basePt, 1.0f) && !NearSpot(s_boards, basePt, BoardSpacing)   /* 칠판은 책장 위 높이라 점검 표식과 1m만 띄운다 */ && !KeptOut(basePt + Vector3.up * BoardCenter, Vector3.Cross(Vector3.up, n), BoardHalf, 0.5f);
        if (boardOk) s_boards.Add(new Spot { At = basePt, Normal = n });

        bool clockOk = room >= ClockMax + 0.3f && Flat(basePt, n, 0.25f, new[] { ClockMin, (ClockMin + ClockMax) * 0.5f, ClockMax })
            && !Physics.CheckBox(basePt + n * 0.35f + Vector3.up * ((ClockMin + ClockMax) * 0.5f), new Vector3(0.25f, (ClockMax - ClockMin) * 0.5f + 0.2f, 0.3f), face, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
            && !Near(avoid, basePt, AvoidRadius) && !NearSpot(s_clocks, basePt, ClockSpacing) && !NearSpot(s_boards, basePt, BoardHalf + 0.6f) && !KeptOut(basePt + Vector3.up * (ClockMin + ClockMax) * 0.5f, Vector3.Cross(Vector3.up, n), 0.25f, (ClockMax - ClockMin) * 0.5f + 0.2f);
        if (clockOk) s_clocks.Add(new Spot { At = basePt, Normal = n });
    }

    /// <summary>벽면 위 사각형(가운데 <paramref name="c"/> · 가로 <paramref name="t"/> 방향 ±<paramref name="halfW"/> · 세로 ±<paramref name="halfH"/>)이 원래 칠판·시계·문·창 그림 상자에 닿는지.</summary>
    private static bool KeptOut(Vector3 c, Vector3 t, float halfW, float halfH)
    {
        Vector3 ext = new Vector3(Mathf.Abs(t.x) * halfW + 0.1f, halfH, Mathf.Abs(t.z) * halfW + 0.1f);
        Bounds me = new Bounds(c, ext * 2f);
        for (int i = 0; i < s_keepOut.Count; i++)
        {
            if (s_keepOut[i].Intersects(me)) return true;
        }

        return false;
    }

    private static bool NearSpot(List<Spot> list, Vector3 p, float r)
    {
        for (int i = 0; i < list.Count; i++)
        {
            if ((list[i].At - p).magnitude < r) return true;
        }

        return false;
    }

    private static void TryCeiling(Vector3 floor, Bounds inner)
    {
        if (!inner.Contains(floor + Vector3.up)) return;
        RaycastHit hit;
        if (!Physics.Raycast(floor + Vector3.up * 0.5f, Vector3.up, out hit, 4.5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return;
        if (hit.normal.y > -0.8f || hit.distance + 0.5f < 2.5f || hit.distance + 0.5f > 4.4f) return;   // 교실 천장 높이(약 4m)만 — 뚫린 천장 위가 아니게
        if (Physics.CheckSphere(hit.point + Vector3.down * 0.55f, 0.4f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return;   // 등·배관 아래가 아니게
        Vector3[] four = { Vector3.right, Vector3.left, Vector3.forward, Vector3.back };
        for (int d = 0; d < four.Length; d++)
        {
            if (Physics.Raycast(hit.point + Vector3.down * 0.5f, four[d], 0.8f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return;   // 벽에서 떨어진 곳
        }

        for (int i = 0; i < s_ceilings.Count; i++)
        {
            if ((s_ceilings[i].At - hit.point).magnitude < CeilingSpacing) return;
        }

        s_ceilings.Add(new Spot { At = hit.point, Normal = Vector3.down, Floor = floor });
    }
}

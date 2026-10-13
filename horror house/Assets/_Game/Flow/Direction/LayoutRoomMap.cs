using System.Collections.Generic;
using NightDuty;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 104차 배치 지도 — 방(교실 1-3 · 과학실) 공용 바탕. 방 배치 구간이 오를수록 벽시계(·칠판)가 하나씩 더 걸리고, 천장에 책상·의자가 거꾸로 붙거나 인체 모형 머리가 박히고,
/// 천장에서 붉은 방울이 떨어져 바닥에 고인다. 하나씩 <b>안 볼 때</b> 생긴다(<see cref="LayoutCorridorMap"/>와 같은 규칙) · 아래 구간 것은 같은 자리·모양으로 남는다 · 재시작·새 밤이면 거둔다.
/// 기존 물건은 옮기지 않는다(점검 대상과 헷갈리지 않게). 방마다 상자·구간 공간·종류별 수는 하위 클래스(<see cref="LayoutClassroomMap"/>·<see cref="LayoutScienceMap"/>)가 정한다.
/// <list type="bullet">
/// <item>벽: 방 상자를 0.5m 격자로 훑어 네 방향의 벽(이름에 Wall · 걸 넓이만큼 평평 · 앞이 비었음 · 앞 0.6m가 방 바닥) — 원래 칠판·시계·문·창·표지판 그림 상자(+0.4m)와 겹치지 않고 점검 표식에서 떨어진 곳.</item>
/// <item>천장: 아래를 보는 2.5~4.4m · 등 아래 아님 · 벽에서 0.8m · <see cref="CeilingSpacing"/>m 간격.</item>
/// </list>
/// </summary>
public abstract class LayoutRoomMap : MonoBehaviour
{
    public const string DeskPath = "LayoutProps/ceiling_desk";
    public const string ChairPath = "LayoutProps/ceiling_chair";
    public const string ClockPath = "LayoutProps/wall_clock";
    public const string BoardPath = "LayoutProps/blackboard";
    public const string DripPath = "LayoutProps/blood_drip";
    public const string TorsoPath = "LayoutProps/ceiling_torso";
    public const string ShelfPath = "LayoutProps/ceiling_shelf";
    public const string BooksPath = "LayoutProps/book_heap";

    /// <summary>천장 책장 배율(2.22m → 약 1.55m — 천장 4m에서 아래 끝이 바닥 위 2.4m 남짓).</summary>
    public const float ShelfScale = 0.7f;

    /// <summary>바닥 책 더미끼리 띄우는 거리(m).</summary>
    public const float BooksSpacing = 1.6f;

    /// <summary>인체 모형 머리를 천장에 박는 깊이(m).</summary>
    public const float TorsoEmbed = 0.35f;
    public const string PoolPath = "LayoutProps/blood_pool";

    private static readonly int[] None = { 0, 0, 0, 0, 0 };

    public const float AvoidRadius = 1.5f;
    public const float BoardSpacing = 2.4f;
    public const float ClockSpacing = 1.4f;
    /// <summary>천장 자리끼리 띄우는 거리(m).</summary>
    protected virtual float CeilingSpacing
    {
        get { return 2.2f; }
    }
    public const float CheckEvery = 0.25f;

    /// <summary>칠판 사본 배율(원래 가로 2m · 세로 1.26m → 1.6 × 1.0m)과 반폭·가운데 높이.</summary>
    private const float BoardScale = 0.8f;
    private const float BoardHalf = 0.85f;
    private const float BoardCenter = 2.85f;   // 벽 아래 타일 턱·책장(1.8m) 위 — 교실 벽은 1.6m까지 타일이라 그 아래는 평평하지 않다

    /// <summary>벽시계 높이(바닥에서, m — 원래 시계 3.0m 언저리).</summary>
    /// <summary>벽시계(·화장실 거울) 높이 아래·위 끝(바닥에서, m)과 반폭 — 화장실은 거울이라 낮게 건다.</summary>
    protected virtual float ClockMin
    {
        get { return 2.6f; }
    }

    protected virtual float ClockMax
    {
        get { return 3.3f; }
    }

    protected virtual float ClockHalf
    {
        get { return 0.25f; }
    }

    /// <summary>벽시계(·거울)를 점검 표식·문·소화기에서 띄우는 거리(m).</summary>
    protected virtual float ClockAvoid
    {
        get { return AvoidRadius; }
    }

    /// <summary>천장 자리를 벽에서 띄우는 거리(m).</summary>
    protected virtual float CeilingWallGap
    {
        get { return 0.8f; }
    }

    /// <summary>방 상자(<see cref="SpaceZones"/>).</summary>
    protected abstract SpaceId BoxSpace { get; }

    /// <summary>배치 구간을 읽는 공간.</summary>
    protected abstract SpaceId BandSpace { get; }

    /// <summary>검수용 강제 구간(−1이면 실제 연출 구간).</summary>
    protected abstract int ForcedBand { get; }

    /// <summary>이름 앞에 붙는 말(「배치 지도 교실 · 」).</summary>
    protected abstract string Label { get; }

    /// <summary>자리 섞기 씨앗(일차와 섞는다).</summary>
    protected abstract int Seed { get; }

    /// <summary>구간(0~4)별 그 종류의 수(쓰지 않으면 null).</summary>
    protected abstract int[] CountsFor(Kind kind);

    private int[] Counts(Kind kind)
    {
        return CountsFor(kind) ?? None;
    }

    protected enum Kind
    {
        Clock,
        Board,
        Desk,
        Chair,
        Drip,
        Torso,
        Shelf,
        Books,
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

    private readonly List<Spot> s_boards = new List<Spot>();
    private readonly List<Spot> s_clocks = new List<Spot>();
    private readonly List<Spot> s_ceilings = new List<Spot>();
    private readonly List<Bounds> s_keepOut = new List<Bounds>();
    private int s_scene = -1;

    private readonly List<Item> _clocks = new List<Item>();
    private readonly List<Item> _boards = new List<Item>();
    private readonly List<Item> _desks = new List<Item>();
    private readonly List<Item> _chairs = new List<Item>();
    private readonly List<Item> _drips = new List<Item>();
    private readonly List<Item> _torsos = new List<Item>();
    private readonly List<Item> _shelves = new List<Item>();
    private readonly List<Item> _books = new List<Item>();
    private readonly List<Spot> s_floors = new List<Spot>();
    private object _plan;
    private float _next;
    private int _band;

    public int Band
    {
        get { return _band; }
    }

    /// <summary>서 있는 수(시계, 칠판, 책상, 의자, 붉은 방울, 인체 모형 — 검수용).</summary>
    public string ShownCounts
    {
        get { return Count(_clocks) + "," + Count(_boards) + "," + Count(_desks) + "," + Count(_chairs) + "," + Count(_drips) + "," + Count(_torsos) + "," + Count(_shelves) + "," + Count(_books); }
    }

    /// <summary>찾은 바닥 자리 수(검수용).</summary>
    public int FloorSpotCount
    {
        get { return s_floors.Count; }
    }

    /// <summary>찾은 자리 수(칠판, 시계, 천장 — 검수용).</summary>
    public Vector3Int SpotCounts
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

    protected virtual void OnEnable()
    {
        EventBus.NightRestarted += OnRestarted;
    }

    protected virtual void OnDisable()
    {
        EventBus.NightRestarted -= OnRestarted;
        ClearAll();
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
        if (_clocks.Count == 0 && _drips.Count == 0 && _desks.Count == 0 && _torsos.Count == 0 && _books.Count == 0 && _shelves.Count == 0) NewNight();
        SyncAll(null);
    }

    private void SyncAll(Camera cam)
    {
        _band = ForcedBand >= 0 ? Mathf.Clamp(ForcedBand, 0, 4) : Mathf.Clamp((int)NightRun.ShownBand(BandSpace, FearAxis.Layout), 0, 4);
        Sync(_clocks, Counts(Kind.Clock)[_band], cam);
        Sync(_boards, Counts(Kind.Board)[_band], cam);
        Sync(_desks, Counts(Kind.Desk)[_band], cam);
        Sync(_chairs, Counts(Kind.Chair)[_band], cam);
        Sync(_drips, Counts(Kind.Drip)[_band], cam);
        Sync(_torsos, Counts(Kind.Torso)[_band], cam);
        Sync(_shelves, Counts(Kind.Shelf)[_band], cam);
        Sync(_books, Counts(Kind.Books)[_band], cam);
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
        foreach (List<Item> list in new[] { _clocks, _boards, _desks, _chairs, _drips, _torsos, _shelves, _books })
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
        System.Random rng = new System.Random(NightRun.Day * 6151 + Seed);
        List<Spot> clocks = Shuffled(s_clocks, rng);
        for (int i = 0; i < Mathf.Min(Counts(Kind.Clock)[4], clocks.Count); i++)
        {
            Spot s = clocks[i];
            float h = ClockMin + (float)rng.NextDouble() * (ClockMax - ClockMin);
            Vector3 at = s.At + Vector3.up * h + s.Normal * 0.04f;
            _clocks.Add(new Item { Kind = Kind.Clock, At = at, Rot = Quaternion.LookRotation(s.Normal, Vector3.up), Probe = Around(at, s.Normal, 0.2f, 0.2f) });
        }

        List<Spot> boards = Shuffled(s_boards, rng);
        for (int i = 0; i < Mathf.Min(Counts(Kind.Board)[4], boards.Count); i++)
        {
            Spot s = boards[i];
            Vector3 at = s.At + Vector3.up * BoardCenter + s.Normal * 0.05f;
            _boards.Add(new Item { Kind = Kind.Board, At = at, Rot = Quaternion.LookRotation(s.Normal, Vector3.up), Probe = Around(at, s.Normal, 0.95f, 0.6f) });
        }

        List<Spot> floors = Shuffled(s_floors, rng);
        for (int i = 0; i < Mathf.Min(Counts(Kind.Books)[4], floors.Count); i++)
        {
            Vector3 f = floors[i].At;
            _books.Add(new Item { Kind = Kind.Books, At = f, Rot = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f), Probe = new[] { f + Vector3.up * 0.15f, f + new Vector3(0.6f, 0.1f, 0f), f + new Vector3(-0.6f, 0.1f, 0f), f + new Vector3(0f, 0.1f, 0.6f), f + new Vector3(0f, 0.1f, -0.6f) } });
        }

        List<Spot> ceilings = Shuffled(s_ceilings, rng);
        int k = 0;
        for (int i = 0; i < Counts(Kind.Shelf)[4] && k < ceilings.Count; i++, k++) _shelves.Add(CeilingItem(Kind.Shelf, ceilings[k], rng));
        for (int i = 0; i < Counts(Kind.Torso)[4] && k < ceilings.Count; i++, k++) _torsos.Add(CeilingItem(Kind.Torso, ceilings[k], rng));
        for (int i = 0; i < Counts(Kind.Desk)[4] && k < ceilings.Count; i++, k++) _desks.Add(CeilingItem(Kind.Desk, ceilings[k], rng));
        for (int i = 0; i < Counts(Kind.Chair)[4] && k < ceilings.Count; i++, k++) _chairs.Add(CeilingItem(Kind.Chair, ceilings[k], rng));
        for (int i = 0; i < Counts(Kind.Drip)[4] && k < ceilings.Count; i++, k++) _drips.Add(CeilingItem(Kind.Drip, ceilings[k], rng));
        if (DirectionStage.Verbose) Debug.Log("[" + GetType().Name + "] " + NightRun.Day + "일차 — 칠판 자리 " + s_boards.Count + " · 시계 자리 " + s_clocks.Count + " · 천장 자리 " + s_ceilings.Count);
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
        float yaw = (float)rng.NextDouble() * 360f;
        if (kind == Kind.Shelf) yaw = Mathf.Round(yaw / 90f) * 90f;   // 책장은 방 축에 맞춘다
        Quaternion rot = kind == Kind.Drip ? Quaternion.identity : kind == Kind.Torso ? Quaternion.Euler(270f, yaw, 0f) : Quaternion.Euler(180f, yaw, 0f);   // 인체 모형 원본은 Z가 위(씬 270·270·0) — 똑바로 세워 머리를 천장에 박는다
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
        string path = PathFor(it.Kind);
        GameObject prefab = Resources.Load<GameObject>(path);
        if (prefab == null)
        {
            Debug.LogWarning("[" + GetType().Name + "] 소품이 없습니다: Resources/" + path + " (야간근무/연출/배치 지도 소품 다시 만들기)");
            return;
        }

        GameObject go = Instantiate(prefab, it.At, it.Rot);
        go.name = Label + it.Kind;
        if (it.Kind == Kind.Board) go.transform.localScale = Vector3.one * BoardScale;
        if (it.Kind == Kind.Torso) EmbedHead(go, it.At.y);
        if (it.Kind == Kind.Shelf) go.transform.localScale = Vector3.one * ShelfScale;
        foreach (Collider c in go.GetComponentsInChildren<Collider>(true)) c.enabled = false;
        foreach (AudioSource a in go.GetComponentsInChildren<AudioSource>(true)) NightDutyMixer.Route(a, NightDutyMixer.Bus.World);
        it.Go = go;
        if (it.Kind == Kind.Drip)
        {
            GameObject pool = Resources.Load<GameObject>(PoolPath);
            if (pool != null)
            {
                it.Pool = Instantiate(pool, it.Floor + Vector3.up * 0.02f, Quaternion.Euler(90f, (it.At.x * 37f + it.At.z * 11f) % 360f, 0f));
                it.Pool.name = Label + "웅덩이";
                foreach (AudioSource a in it.Pool.GetComponentsInChildren<AudioSource>(true)) NightDutyMixer.Route(a, NightDutyMixer.Bus.World);
            }
        }
    }

    /// <summary>방 상자 안에서도 실제 방인지(상자가 이웃 공간을 덮는 방이 거른다 — 도서관 상자는 동쪽 복도 띠를 덮는다).</summary>
    protected virtual bool InRoom(Vector3 p)
    {
        return true;
    }

    /// <summary>그 종류의 소품 경로(방마다 바꿀 수 있다 — 도서관은 책상·의자를 도서관 것으로).</summary>
    protected virtual string PathFor(Kind kind)
    {
        switch (kind)
        {
            case Kind.Clock: return ClockPath;
            case Kind.Board: return BoardPath;
            case Kind.Desk: return DeskPath;
            case Kind.Chair: return ChairPath;
            case Kind.Torso: return TorsoPath;
            case Kind.Shelf: return ShelfPath;
            case Kind.Books: return BooksPath;
            default: return DripPath;
        }
    }

    /// <summary>104차(과학실 — 사용자: 「Anatomical Human Torso를 천장에 머리가 꽂힌 채 몇 개」): 모형 꼭대기(머리)가 천장 위로 <see cref="TorsoEmbed"/>m 들어가게 내린다.</summary>
    private static void EmbedHead(GameObject go, float ceilingY)
    {
        Renderer[] rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return;
        Bounds b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        go.transform.position += Vector3.up * (ceilingY + TorsoEmbed - b.max.y);
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

    private void Scan()
    {
        int scene = SceneManager.GetActiveScene().handle;
        if (s_scene == scene) return;
        s_scene = scene;
        s_boards.Clear();
        s_clocks.Clear();
        s_ceilings.Clear();
        s_floors.Clear();
        SpaceZones zones = FindAnyObjectByType<SpaceZones>();
        Bounds box;
        if (zones == null || !zones.TryGetSpaceBox(BoxSpace, out box)) return;
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
                hit = pn.StartsWith("Blackboard") || pn.StartsWith("Clock") || pn.StartsWith("Door") || pn.StartsWith("Jalousie") || pn.StartsWith("Window") || pn.StartsWith("Sign_") || pn.StartsWith("LightSwitch") || pn.StartsWith("FireExtinguisher") || pn.StartsWith("Toilet_Mirror");
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
                if (fh.normal.y < 0.8f || fh.point.y > box.min.y + 0.3f) continue;   // 방 바닥만(낮은 상자·발판 위 웅덩이가 아니게)
                for (int d = 0; d < dirs.Length; d++) TryWall(fh.point, dirs[d], avoid, inner);
                TryCeiling(fh.point, inner);
                TryFloor(fh.point, avoid, inner);
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

    private void TryWall(Vector3 floor, Vector3 dir, List<Vector3> avoid, Bounds inner)
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

        bool boardOk = Counts(Kind.Board)[4] > 0 && room >= BoardCenter + 0.6f && Flat(basePt, n, BoardHalf, new[] { BoardCenter - 0.45f, BoardCenter, BoardCenter + 0.45f })
            && !Physics.CheckBox(basePt + n * 0.4f + Vector3.up * BoardCenter, new Vector3(BoardHalf, 0.5f, 0.33f), face, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)   // 앞 0.07~0.73m가 비어야(책장 뒤에 걸리지 않게)
            && !Near(avoid, basePt, 1.0f) && !NearSpot(s_boards, basePt, BoardSpacing)   /* 칠판은 책장 위 높이라 점검 표식과 1m만 띄운다 */ && !KeptOut(basePt + Vector3.up * BoardCenter, Vector3.Cross(Vector3.up, n), BoardHalf, 0.5f);
        if (boardOk) s_boards.Add(new Spot { At = basePt, Normal = n });

        bool clockOk = Counts(Kind.Clock)[4] > 0 && room >= ClockMax + 0.3f && Flat(basePt, n, ClockHalf, new[] { ClockMin, (ClockMin + ClockMax) * 0.5f, ClockMax })
            && !Physics.CheckBox(basePt + n * 0.35f + Vector3.up * ((ClockMin + ClockMax) * 0.5f), new Vector3(ClockHalf, (ClockMax - ClockMin) * 0.5f + 0.2f, 0.3f), face, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
            && !Near(avoid, basePt, ClockAvoid) && !NearSpot(s_clocks, basePt, ClockSpacing) && !NearSpot(s_boards, basePt, BoardHalf + 0.6f) && !KeptOut(basePt + Vector3.up * (ClockMin + ClockMax) * 0.5f, Vector3.Cross(Vector3.up, n), ClockHalf, (ClockMax - ClockMin) * 0.5f + 0.2f);
        if (clockOk) s_clocks.Add(new Spot { At = basePt, Normal = n });
    }

    /// <summary>벽면 위 사각형(가운데 <paramref name="c"/> · 가로 <paramref name="t"/> 방향 ±<paramref name="halfW"/> · 세로 ±<paramref name="halfH"/>)이 원래 칠판·시계·문·창 그림 상자에 닿는지.</summary>
    private bool KeptOut(Vector3 c, Vector3 t, float halfW, float halfH)
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

    /// <summary>104차(도서관 — 책 더미): 바닥 위 1.3 × 0.7 × 1.3m가 비고 점검 표식·문에서 1.2m 떨어진 방 바닥.</summary>
    private void TryFloor(Vector3 floor, List<Vector3> avoid, Bounds inner)
    {
        if (Counts(Kind.Books)[4] <= 0 || !inner.Contains(floor + Vector3.up) || !InRoom(floor)) return;
        if (Physics.CheckBox(floor + Vector3.up * 0.4f, new Vector3(0.65f, 0.35f, 0.65f), Quaternion.identity, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return;
        if (Near(avoid, floor, 1.2f) || NearSpot(s_floors, floor, BooksSpacing)) return;
        s_floors.Add(new Spot { At = floor, Normal = Vector3.up, Floor = floor });
    }

    private void TryCeiling(Vector3 floor, Bounds inner)
    {
        if (!InRoom(floor)) return;
        if (!inner.Contains(floor + Vector3.up)) return;
        RaycastHit hit;
        if (!Physics.Raycast(floor + Vector3.up * 0.5f, Vector3.up, out hit, 4.5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return;
        if (hit.normal.y > -0.8f || hit.distance + 0.5f < 2.5f || hit.distance + 0.5f > 4.4f) return;   // 교실 천장 높이(약 4m)만 — 뚫린 천장 위가 아니게
        if (Physics.CheckSphere(hit.point + Vector3.down * 0.55f, 0.4f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return;   // 등·배관 아래가 아니게
        Vector3[] four = { Vector3.right, Vector3.left, Vector3.forward, Vector3.back };
        for (int d = 0; d < four.Length; d++)
        {
            if (Physics.Raycast(hit.point + Vector3.down * 0.5f, four[d], CeilingWallGap, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return;   // 벽에서 떨어진 곳
        }

        for (int i = 0; i < s_ceilings.Count; i++)
        {
            if ((s_ceilings[i].At - hit.point).magnitude < CeilingSpacing) return;
        }

        s_ceilings.Add(new Spot { At = hit.point, Normal = Vector3.down, Floor = floor });
    }
}

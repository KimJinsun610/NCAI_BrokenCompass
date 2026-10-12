using System.Collections.Generic;
using NightDuty;
using UnityEngine;

/// <summary>
/// 104차(민: 「현관 나무와 사람 나무 조우를 합쳐줘 · 너무 복도 동선을 막거나, 벽이나 장애물 뒤에 어딘가 잘린 채로 등장해서 몰입을 깨는 경우를 확인」):
/// 현관 인체 나무(<see cref="HumanTreeGrowth"/>)가 사람 나무 조우 때 복도에 나올 자리를 고른다. 옛 자리 고르기(DirectionStage.TreeSpot)는 줄기 한 점만 봐서
/// 가지가 벽을 뚫거나(모델 폭 1.76m) 문 앞·좁은 곳에 서서 H1(1.2m 안 접근 금지) 때문에 복도가 통째로 막혔다.
/// <list type="bullet">
/// <item>복도 단면(양옆 벽이 모두 <see cref="MaxHalfWidth"/>m 안 — 출입구·갈림길이 아님) · 방 상자 밖 · 현관(<see cref="LobbyMin"/>~<see cref="LobbyMax"/>) 밖 · 플레이어와 같은 층.</item>
/// <item>한쪽 벽에 붙되, 모델의 <b>실제 폭</b>(키에 비례, 회전 반영)이 벽에서 <see cref="WallMargin"/>m 떨어지게. 앞면은 반대쪽 벽을 보다 플레이어 쪽으로 최대 <see cref="Yaws"/>°.</item>
/// <item>H1 원(<see cref="H1Radius"/>m) 밖으로 반대편 벽까지 <see cref="MinPassage"/>m 넘게 트여야 하고, 그 통로(앞뒤 1m)에 걸리는 것이 없어야 한다.</item>
/// <item>몸 전체 상자(회전 반영)가 어떤 콜라이더와도 겹치지 않고, 천장이 키 + <see cref="CeilingMargin"/>m 위 — 키는 천장에 맞춰 줄인다.</item>
/// <item>문·점검 대상(판정 표식)에서 <see cref="AvoidRadius"/>m, 다른 고정 몹에서 <see cref="MobRadius"/>m, 관물대에서 <see cref="LockerRadius"/>m 떨어진다(문짝·관물대 문이 나무를 뚫지 않게).
/// 등 뒤 벽이 앞뒤 <see cref="WallSpan"/>m 이어져야 한다(문간·갈림길 어귀·모퉁이 끝 아님).</item>
/// <item>복도 단면은 씬마다 한 번 훑어 둔다(<see cref="Sites"/>, 0.5m 간격). 고를 때(<see cref="Pick"/>) 플레이어에게서 4.5~13m · 같은 층 · 앞쪽·7m 근처부터, 모든 검사를 다시 한다.</item>
/// <item>플레이어 눈에서 몸통까지 가림이 없어야 한다(벽 너머에 서서 영영 못 보는 일 없게 — 오래 못 고르면 부르는 쪽이 이 조건을 푼다). 나타나는 순간은 <see cref="Hidden"/> — 화면 밖일 때만.</item>
/// </list>
/// </summary>
public static class TreeSpots
{
    /// <summary>모델(New_eggman 나무) 실측 — 키 2.8m일 때 반폭(좌우 x) · 반깊이(앞뒤 z).</summary>
    public const float ModelHeight = 2.8f;
    public const float ModelHalfWidth = 0.88f;
    public const float ModelHalfDepth = 0.57f;

    /// <summary>H1 「미분류 물체에 다가가지 마십시오」 근접 위반 반지름(FinalJudges, m).</summary>
    public const float H1Radius = 1.2f;

    /// <summary>H1 원 밖으로 남아야 하는 통로 폭(m) — 플레이어 캡슐 지름 0.6 + 여유.</summary>
    public const float MinPassage = 0.85f;

    /// <summary>가지 끝과 벽 사이(m).</summary>
    public const float WallMargin = 0.08f;

    /// <summary>가지 끝과 천장 사이(m).</summary>
    public const float CeilingMargin = 0.15f;

    /// <summary>이 반지름(m) 안에 문·점검 대상이 있으면 서지 않는다.</summary>
    public const float AvoidRadius = 2.0f;

    /// <summary>이 반지름(m) 안에 관물대가 있으면 서지 않는다(배치 연출이 문을 연다).</summary>
    public const float LockerRadius = 1.2f;

    /// <summary>복도 단면 — 양옆 벽이 이 거리(m) 안이어야 한다.</summary>
    public const float MaxHalfWidth = 3.5f;

    /// <summary>앞면이 반대쪽 벽에서 플레이어 쪽으로 도는 각(°) — 앞쪽이 먼저.</summary>
    public static readonly float[] Yaws = { 35f, 20f, 0f };

    /// <summary>현관(나무의 집) 수평 범위 — 복도 상자에 들어 있어 따로 뺀다(PlayScene 실측 x 25.3~29.8 · z 44~48, 여유 0.5m).</summary>
    public static readonly Vector2 LobbyMin = new Vector2(24.8f, 43.5f);
    public static readonly Vector2 LobbyMax = new Vector2(30.3f, 48.5f);

    /// <summary>고른 자리.</summary>
    public struct Spot
    {
        /// <summary>뿌리(바닥 점).</summary>
        public Vector3 Floor;
        /// <summary>나무 앞면 방향.</summary>
        public Quaternion Rotation;
        /// <summary>그 자리에서의 키(m) — 천장·벽에 맞춘 것.</summary>
        public float Height;
        /// <summary>H1 원 밖 통로 폭(m).</summary>
        public float Passage;
        /// <summary>플레이어가 보는 쪽(앞)인지.</summary>
        public bool Ahead;
        /// <summary>플레이어와의 수평 거리(m).</summary>
        public float Distance;
    }

    /// <summary>키 <paramref name="height"/>m · 앞면 <paramref name="forward"/>일 때 <paramref name="dir"/> 쪽으로 뻗은 길이(m).</summary>
    public static float Extent(float height, Vector3 forward, Vector3 dir)
    {
        float s = height / ModelHeight;
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        return s * (ModelHalfWidth * Mathf.Abs(Vector3.Dot(right, dir)) + ModelHalfDepth * Mathf.Abs(Vector3.Dot(forward, dir)));
    }

    /// <summary>미리 훑어 둔 복도 단면 하나 — 벽 쪽 바닥 점과 양옆 벽 거리(나무 키와 무관한 것만).</summary>
    public struct Site
    {
        /// <summary>단면 가운데 바닥 점.</summary>
        public Vector3 Floor;
        /// <summary>복도 길이 방향(±x 또는 ±z).</summary>
        public Vector3 Axis;
        /// <summary>붙을 벽 쪽(단위).</summary>
        public Vector3 WallDir;
        /// <summary>그 벽까지(m).</summary>
        public float ToWall;
        /// <summary>양옆 벽 사이(m).</summary>
        public float Width;
        /// <summary>천장까지(m).</summary>
        public float Room;
    }

    /// <summary>훑는 간격(m).</summary>
    public const float ScanStep = 0.5f;

    private static readonly List<Site> s_sites = new List<Site>();
    private static int s_sitesScene = -1;

    /// <summary>근무 씬의 복도 단면들(처음 부를 때 한 번 훑는다 — 벽·천장만 보므로 밤마다 같다).</summary>
    public static List<Site> Sites()
    {
        int scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().handle;
        if (s_sitesScene == scene && s_sites.Count > 0) return s_sites;
        s_sitesScene = scene;
        s_sites.Clear();
        if (s_zones == null) s_zones = Object.FindAnyObjectByType<SpaceZones>();
        Bounds box;
        if (s_zones == null || !s_zones.TryGetSpaceBox(SpaceId.Corridor, out box)) return s_sites;
        HashSet<Vector3Int> seen = new HashSet<Vector3Int>();
        Vector3[] axes = { Vector3.right, Vector3.forward };
        for (float x = box.min.x; x <= box.max.x; x += ScanStep)
        {
            for (float z = box.min.z; z <= box.max.z; z += ScanStep)
            {
                RaycastHit hit;
                if (!Physics.Raycast(new Vector3(x, box.min.y + 1.6f, z), Vector3.down, out hit, 2.2f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
                if (hit.normal.y < 0.8f) continue;
                Vector3 floor = hit.point;
                if (Physics.CheckSphere(floor + Vector3.up * 1.0f, 0.25f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;   // 가구 위·벽 속
                if (!InCorridor(floor, floor.y)) continue;
                float room = Ceiling(floor, null, null);
                for (int a = 0; a < axes.Length; a++)
                {
                    Vector3 across = Vector3.Cross(Vector3.up, axes[a]);
                    float left = Wall(floor, across, null, null);
                    float right = Wall(floor, -across, null, null);
                    if (left < 0f || right < 0f) continue;
                    float width = left + right;
                    if (width < H1Radius + MinPassage + 0.45f) continue;   // 아무리 작아도 지나갈 수 없다
                    for (int side = -1; side <= 1; side += 2)
                    {
                        Vector3 wallDir = across * side;
                        float toWall = side > 0 ? left : right;
                        Vector3 wallPoint = floor + wallDir * toWall;
                        Vector3Int key = new Vector3Int(Mathf.RoundToInt(wallPoint.x / ScanStep), Mathf.RoundToInt(wallPoint.z / ScanStep), a * 2 + (side > 0 ? 1 : 0));
                        if (!seen.Add(key)) continue;   // 같은 벽 자리(단면의 다른 점에서 본 것)
                        s_sites.Add(new Site { Floor = floor, Axis = axes[a], WallDir = wallDir, ToWall = toWall, Width = width, Room = room });
                    }
                }
            }
        }

        return s_sites;
    }

    /// <summary>
    /// 플레이어(루트 <paramref name="playerRoot"/> — 캡슐 가운데)에게서 <see cref="MinDistance"/>~<see cref="MaxDistance"/>m, 같은 층의 복도 단면 가운데
    /// <paramref name="accept"/>(예: 화면 밖)를 만족하는 첫 자리. <paramref name="aheadOnly"/>면 보는 쪽만, <paramref name="requireSight"/>면 플레이어 눈에서 가림 없이 보이는 자리만.
    /// 가까운 7m 근처부터. <paramref name="tree"/>는 무시할 나무(옮기는 중인 자기 자신).
    /// </summary>
    public static bool Pick(Transform playerRoot, Vector3 forward, float height, Transform tree, bool aheadOnly, bool requireSight, System.Func<Spot, bool> accept, out Spot spot, out int valid)
    {
        spot = new Spot();
        valid = 0;
        if (playerRoot == null) return false;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
        forward.Normalize();
        float ground = GroundY(playerRoot.position, playerRoot);
        Vector3 feet = new Vector3(playerRoot.position.x, ground, playerRoot.position.z);
        List<Site> sites = Sites();
        s_order.Clear();
        for (int i = 0; i < sites.Count; i++)
        {
            Vector3 d = sites[i].Floor - feet;
            if (Mathf.Abs(d.y) > 0.3f) continue;
            d.y = 0f;
            float dist = d.magnitude;
            if (dist < MinDistance || dist > MaxDistance) continue;
            bool ahead = Vector3.Dot(d / dist, forward) > 0.3f;
            if (aheadOnly && !ahead) continue;
            s_order.Add(new KeyValuePair<float, int>((ahead ? 0f : 100f) + Mathf.Abs(dist - PreferDistance) + Random.value * 0.6f, i));
        }

        s_order.Sort((p, q) => p.Key.CompareTo(q.Key));
        for (int k = 0; k < s_order.Count; k++)
        {
            Site site = sites[s_order[k].Value];
            Spot s;
            string why;
            if (!Fit(site, feet, playerRoot, height, tree, requireSight, out s, out why)) continue;
            if (s.Distance < MinDistance) continue;   // 단면 가운데가 아니라 벽에 붙인 뿌리로 다시 잰다
            valid++;
            s.Ahead = s_order[k].Key < 100f;
            if (accept != null && !accept(s)) continue;
            spot = s;
            return true;
        }

        return false;
    }

    /// <summary>자리를 고를 플레이어와의 거리(m) — 이보다 가깝거나 멀면 보지 않는다.</summary>
    public const float MinDistance = 4.5f;
    public const float MaxDistance = 13f;

    /// <summary>이 거리(m) 근처부터 고른다.</summary>
    public const float PreferDistance = 7f;

    private static readonly List<KeyValuePair<float, int>> s_order = new List<KeyValuePair<float, int>>();

    /// <summary>
    /// 그 단면 벽에 키 <paramref name="height"/>m 나무를 붙여 세울 수 있는지 — 앞면은 반대쪽 벽을 보다 플레이어 쪽으로 <see cref="Yaws"/>°(넓은 각부터).
    /// 모든 검사(통로·몸 겹침·통로 장애물·문/점검 대상/관물대·시야)를 지금 다시 한다(문·관물대가 열고 닫히므로). 실패하면 <paramref name="why"/>.
    /// </summary>
    public static bool Fit(Site site, Vector3 feet, Transform player, float height, Transform tree, bool requireSight, out Spot spot, out string why)
    {
        spot = new Spot();
        why = string.Empty;
        float h = Mathf.Min(height, site.Room - CeilingMargin);
        if (h < HumanTreeGrowth.MinLength * 0.9f)
        {
            why = "천장 낮음 " + site.Room.ToString("F2");
            return false;
        }

        Vector3 toPlayer = feet - site.Floor;
        toPlayer.y = 0f;
        toPlayer = Vector3.Dot(toPlayer, site.Axis) >= 0f ? site.Axis : -site.Axis;   // 복도를 따라 플레이어 쪽
        for (int y = 0; y < Yaws.Length; y++)
        {
            float a = Yaws[y] * Mathf.Deg2Rad;
            Vector3 f = (-site.WallDir * Mathf.Cos(a) + toPlayer * Mathf.Sin(a)).normalized;
            float gap = Extent(h, f, site.WallDir) + WallMargin;
            float passage = site.Width - gap - H1Radius;
            if (passage < MinPassage)
            {
                why = "통로 좁음 " + passage.ToString("F2") + "(폭 " + site.Width.ToString("F2") + ")";
                continue;
            }

            Vector3 pivot = site.Floor + site.WallDir * (site.ToWall - gap);
            Quaternion rot = Quaternion.LookRotation(f, Vector3.up);
            if (!InCorridor(pivot, site.Floor.y))
            {
                why = "복도 밖/현관";
                return false;
            }

            if (Blocked(pivot, rot, h, player, tree))
            {
                why = "몸이 겹침(벽·장애물)";
                continue;
            }

            if (!WallBehind(pivot, site.WallDir, site.Axis, gap, player, tree))
            {
                why = "등 뒤 벽이 끊김(문간·모퉁이)";
                return false;
            }

            if (PassageBlocked(pivot, site.WallDir, site.Axis, site.Width - gap, player, tree))
            {
                why = "통로에 장애물";
                continue;
            }

            if (NearAvoid(pivot, out why)) return false;
            if (requireSight && player != null && !InSight(player, pivot, h, tree))
            {
                why = "플레이어에게서 가려짐";
                return false;
            }

            spot.Floor = pivot;
            spot.Rotation = rot;
            spot.Height = h;
            spot.Passage = passage;
            Vector3 flat = pivot - feet;
            flat.y = 0f;
            spot.Distance = flat.magnitude;
            return true;
        }

        return false;
    }

    /// <summary>
    /// 그 자리에 선 키 <paramref name="height"/>m 나무가 지금 화면에 보이지 않는지 — 몸 여러 점(아래·가운데·위, 양옆)이 모두 화면 밖(가장자리 15% 여유)이거나 가려짐.
    /// </summary>
    public static bool Hidden(Vector3 floor, Quaternion rot, float height, Camera cam, Transform ignore)
    {
        if (cam == null) return true;
        Vector3 right = rot * Vector3.right;
        float s = height / ModelHeight;
        Vector3 eye = cam.transform.position;
        float[] hs = { 0.15f, 0.5f, 0.95f };
        float[] xs = { -1f, 0f, 1f };
        for (int i = 0; i < hs.Length; i++)
        {
            for (int j = 0; j < xs.Length; j++)
            {
                Vector3 p = floor + Vector3.up * (height * hs[i]) + right * (ModelHalfWidth * s * xs[j] * 0.8f);
                Vector3 v = cam.WorldToViewportPoint(p);
                if (v.z <= 0f || v.x < -0.15f || v.x > 1.15f || v.y < -0.15f || v.y > 1.15f) continue;
                Vector3 d = p - eye;
                float len = d.magnitude;
                if (len < 0.05f) return false;
                if (Occluded(eye, d / len, len - 0.1f, ignore)) continue;
                return false;
            }
        }

        return true;
    }

    // ── 안쪽 ─────────────────────────────────────

    private static bool Skip(Collider c, Transform player, Transform tree)
    {
        if (c == null || c.isTrigger) return true;
        Transform t = c.transform;
        return (player != null && t.IsChildOf(player)) || (tree != null && t.IsChildOf(tree));
    }

    /// <summary>플레이어가 선 바닥 높이.</summary>
    public static float GroundY(Vector3 root, Transform player)
    {
        foreach (RaycastHit h in Sorted(Physics.RaycastAll(root + Vector3.up * 0.1f, Vector3.down, 3f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)))
        {
            if (Skip(h.collider, player, null) || h.normal.y < 0.5f) continue;
            return h.point.y;
        }

        return root.y - 0.85f;
    }

    private static RaycastHit[] Sorted(RaycastHit[] hits)
    {
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        return hits;
    }

    /// <summary>
    /// 옆 벽까지 거리 — 무릎·허리 높이에 모두 수직면이 있어야 하고(없으면 문간·갈림길 → −1), 어깨 높이(2m)는 맞으면 더 가까운 쪽으로 친다
    /// (낮은 장 위로 트인 곳·창은 막힘이 아니다 — 가지가 그 위로 뻗을 수 있으니 거리만 줄인다).
    /// </summary>
    private static float Wall(Vector3 floor, Vector3 dir, Transform player, Transform tree)
    {
        float best = -1f;
        float[] heights = { 0.4f, 1.2f, 2.0f };
        for (int k = 0; k < heights.Length; k++)
        {
            float here = -1f;
            foreach (RaycastHit h in Physics.RaycastAll(floor + Vector3.up * heights[k], dir, MaxHalfWidth, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (Skip(h.collider, player, tree) || Mathf.Abs(h.normal.y) > 0.3f) continue;
                if (here < 0f || h.distance < here) here = h.distance;
            }

            if (here < 0f)
            {
                if (k < 2) return -1f;   // 무릎·허리에 벽이 없다 — 문간·갈림길
                continue;
            }

            if (best < 0f || here < best) best = here;
        }

        return best;
    }

    private static float Ceiling(Vector3 floor, Transform player, Transform tree)
    {
        float best = 5f;
        foreach (RaycastHit h in Physics.RaycastAll(floor + Vector3.up * 0.3f, Vector3.up, 4.7f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (Skip(h.collider, player, tree)) continue;
            if (h.distance + 0.3f < best) best = h.distance + 0.3f;
        }

        return best;
    }

    private static readonly Collider[] s_hits = new Collider[32];

    private static bool AnyOverlap(Vector3 center, Vector3 half, Quaternion rot, Transform player, Transform tree)
    {
        int n = Physics.OverlapBoxNonAlloc(center, half, s_hits, rot, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            if (!Skip(s_hits[i], player, tree)) return true;
        }

        return false;
    }

    /// <summary>몸 전체 상자(바닥 위 0.06m부터 키까지)가 무엇과 겹치는지.</summary>
    public static bool Blocked(Vector3 pivot, Quaternion rot, float height, Transform player, Transform tree)
    {
        float s = height / ModelHeight;
        Vector3 half = new Vector3(ModelHalfWidth * s + 0.02f, (height - 0.06f) * 0.5f, ModelHalfDepth * s + 0.02f);
        return AnyOverlap(pivot + Vector3.up * (0.06f + half.y), half, rot, player, tree);
    }

    /// <summary>등 뒤(붙은 벽)가 앞뒤 <see cref="WallSpan"/>m 동안 이어지는지 — 문간·갈림길 어귀·모퉁이 끝에 서지 않게(무릎·허리 높이 모두).</summary>
    private static bool WallBehind(Vector3 pivot, Vector3 wallDir, Vector3 axis, float gap, Transform player, Transform tree)
    {
        float[] along = { -WallSpan, 0f, WallSpan };
        float[] heights = { 0.5f, 1.3f };
        for (int i = 0; i < along.Length; i++)
        {
            for (int k = 0; k < heights.Length; k++)
            {
                Vector3 o = pivot + axis * along[i] + Vector3.up * heights[k];
                bool hit = false;
                foreach (RaycastHit h in Physics.RaycastAll(o, wallDir, gap + 0.35f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                {
                    if (Skip(h.collider, player, tree)) continue;
                    hit = true;
                    break;
                }

                if (!hit) return false;
            }
        }

        return true;
    }

    /// <summary>등 뒤 벽이 이어져야 하는 앞뒤 길이(m).</summary>
    public const float WallSpan = 1.0f;

    /// <summary>다른 고정 몹(복도 끝 인체 모형 등)에서 이만큼(m) 떨어진다 — 한 화면에 겹쳐 보이지 않게.</summary>
    public const float MobRadius = 3.5f;

    /// <summary>H1 원 바로 밖 통로 띠(폭 <see cref="MinPassage"/>, 바닥 0.2~1.9m, 앞뒤 1m)에 걸리는 것이 있는지.</summary>
    private static bool PassageBlocked(Vector3 pivot, Vector3 wallDir, Vector3 axis, float toOtherWall, Transform player, Transform tree)
    {
        float from = H1Radius;
        float to = Mathf.Min(from + MinPassage, toOtherWall - 0.03f);   // H1 원 바로 바깥 띠(지나갈 폭)만 — 맞은편 벽의 기둥·몰딩까지 막힘으로 치지 않는다
        if (to - from < MinPassage * 0.9f) return true;
        Vector3 center = pivot - wallDir * ((from + to) * 0.5f) + Vector3.up * 1.05f;
        Vector3 half = new Vector3((to - from) * 0.5f, 0.85f, 1.0f);
        Quaternion rot = Quaternion.LookRotation(axis, Vector3.up);   // 상자 x = 가로(벽 쪽), z = 복도 길이 방향
        return AnyOverlap(center, half, rot, player, tree);
    }

    private static SpaceZones s_zones;

    /// <summary>복도 상자 안 · 방 상자(+0.6m) 밖 · 현관 밖인지(104차: 배치 지도도 쓴다).</summary>
    public static bool InCorridor(Vector3 p, float floorY)
    {
        if (p.x >= LobbyMin.x && p.x <= LobbyMax.x && p.z >= LobbyMin.y && p.z <= LobbyMax.y) return false;
        if (s_zones == null) s_zones = Object.FindAnyObjectByType<SpaceZones>();
        if (s_zones == null) return true;
        Vector3 q = new Vector3(p.x, floorY + 1f, p.z);
        Bounds box;
        if (s_zones.TryGetSpaceBox(SpaceId.Corridor, out box) && !box.Contains(q)) return false;
        SpaceId[] rooms = { SpaceId.Classroom_1_1, SpaceId.Classroom_1_3, SpaceId.ScienceRoom, SpaceId.Toilet, SpaceId.Library, SpaceId.SecurityRoom };
        for (int i = 0; i < rooms.Length; i++)
        {
            if (s_zones.TryGetSpaceBox(rooms[i], out box))
            {
                box.Expand(new Vector3(0.6f, 0f, 0.6f));   // 방 문간 바로 앞도 피한다
                if (box.Contains(q)) return false;
            }
        }

        return true;
    }

    private static readonly List<JudgeTarget> s_targets = new List<JudgeTarget>();
    private static readonly List<Transform> s_lockers = new List<Transform>();
    private static int s_lockerScene = -1;

    /// <summary>문·점검 대상(판정 표식, H1 자신 제외) 또는 관물대가 가까운지.</summary>
    private static bool NearAvoid(Vector3 pivot, out string why)
    {
        why = string.Empty;
        s_targets.Clear();
        JudgeTargetRegistry.CollectOwners(s_targets);
        for (int i = 0; i < s_targets.Count; i++)
        {
            JudgeTarget t = s_targets[i];
            if (t == null || t.PrimaryId == FinalCues.H1Object) continue;
            Vector3 d = t.AnchorPosition - pivot;
            if (Mathf.Abs(d.y) > 3f) continue;
            d.y = 0f;
            if (d.magnitude < (FixedMobStare.IsFixedMob(t.PrimaryId) ? MobRadius : AvoidRadius))
            {
                why = "판정 표식 가까움 " + t.PrimaryId;
                return true;
            }
        }

        int scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().handle;
        if (s_lockerScene != scene)
        {
            s_lockerScene = scene;
            s_lockers.Clear();
            foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t.name.StartsWith(LayoutCabinets.LockerPrefix, System.StringComparison.Ordinal)) s_lockers.Add(t);
            }
        }

        for (int i = 0; i < s_lockers.Count; i++)
        {
            Transform l = s_lockers[i];
            if (l == null) continue;
            Vector3 d = l.position - pivot;
            if (Mathf.Abs(d.y) > 3f) continue;
            d.y = 0f;
            if (d.magnitude < LockerRadius)
            {
                why = "관물대 가까움";
                return true;
            }
        }

        return false;
    }

    private static bool Occluded(Vector3 from, Vector3 dir, float dist, Transform ignore)
    {
        PlayerSensors hub = PlayerSensors.Active;
        Transform player = hub != null ? hub.PlayerRoot : null;
        foreach (RaycastHit h in Physics.RaycastAll(from, dir, Mathf.Max(0f, dist), Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (Skip(h.collider, player, ignore)) continue;
            return true;
        }

        return false;
    }

    /// <summary>플레이어 눈 높이에서 나무 몸통(키의 절반)까지 가림이 없는지.</summary>
    private static bool InSight(Transform player, Vector3 pivot, float height, Transform tree)
    {
        float ground = GroundY(player.position, player);
        Vector3 eye = new Vector3(player.position.x, ground + 1.6f, player.position.z);
        Vector3 body = pivot + Vector3.up * Mathf.Clamp(height * 0.5f, 0.6f, 1.4f);
        Vector3 d = body - eye;
        float len = d.magnitude;
        foreach (RaycastHit h in Physics.RaycastAll(eye, d / len, len - 0.7f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (Skip(h.collider, player, tree)) continue;
            return false;
        }

        return true;
    }
}

using System.Collections.Generic;
using NightDuty;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// 74차(민: 「경비실의 mapboard에 경비실 부분에 나의 위치를 표시하는 빨간 점 하나가 있으면 좋겠네」) — 경비실 벽 지도(<c>MapBoard/MapPaper</c>)의
/// 경비실 칸에 「현재 위치」 빨간 점 하나를 세운다.
/// <list type="bullet">
/// <item>씬·김진선님 프리팹(MapBoard.prefab)은 고치지 않는다 — 근무 씬이 열리면 지도 종이를 찾아 그 자식 옆(MapBoard 아래)에 점을 만든다.</item>
/// <item>자리는 지도 그림(<c>Map_KR_Aged.png</c>, 1208×546)의 경비실 방 안 「경비실」 글씨 바로 위(<see cref="SecurityRoomUv"/>).
/// 지도 종이는 기본 Quad(로컬 −0.5~0.5, 앞면 −Z)라 UV가 곧 로컬 좌표다.</item>
/// <item>점은 납작한 원기둥(지름 <see cref="Diameter"/>m) + 조명 없는 빨강(<c>Resources/MapYouAreHere.mat</c>) — 어두운 경비실에서도 보인다. 콜라이더·그림자 없음.</item>
/// </list>
/// </summary>
[DisallowMultipleComponent]
public sealed class MapBoardYouAreHere : MonoBehaviour
{
    /// <summary>지도 종이 오브젝트 이름(김진선님 MapBoard 프리팹의 자식).</summary>
    public const string PaperName = "MapPaper";

    /// <summary>세운 점의 이름.</summary>
    public const string DotName = "MapYouAreHere";

    /// <summary>지도 그림에서 경비실 방 안, 「경비실」 글씨 바로 위(가로 0.344 · 아래에서 0.481).</summary>
    public static readonly Vector2 SecurityRoomUv = new Vector2(0.344f, 0.481f);

    /// <summary>점 지름(m). 지도 세로 0.82m에 그림 546px — 약 15px.</summary>
    public const float Diameter = 0.022f;

    /// <summary>종이 앞으로 띄우는 거리(m) — 겹쳐 깜빡이지 않게.</summary>
    public const float Lift = 0.0015f;

    /// <summary>점 재질(Resources).</summary>
    public const string MaterialPath = "MapYouAreHere";

    /// <summary>재질이 없을 때의 색.</summary>
    public static readonly Color DotColor = new Color(0.86f, 0.06f, 0.05f);

    private bool _done;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Install()
    {
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
        if (!FlowAutoInstall.IsDutyScene(scene) || FlowAutoInstall.Exists<MapBoardYouAreHere>(scene)) return;
        FlowAutoInstall.CreateHost<MapBoardYouAreHere>(scene, "MapBoardYouAreHere (auto)");
    }

    private void Update()
    {
        if (!_done)
        {
            _done = Place(gameObject.scene) != null;
            if (_done) Brighten(FindPaper(gameObject.scene));
            return;
        }

        if (Time.unscaledTime < _nextSync) return;
        _nextSync = Time.unscaledTime + SyncSeconds;
        SyncBatteries(FindPaper(gameObject.scene));
    }

    // ── 80차: 손전등 없이도 보이는 지도 · 배터리 자리 ─────────────────────────

    /// <summary>80차(민: 「경비실 지도가 손전등으로 비추지 않으면 잘 안 보여」): 지도 종이가 스스로 이만큼 빛난다(그림 × 세기). 김진선님 재질은 고치지 않고 이 인스턴스만.</summary>
    public const float PaperGlow = 0.42f;

    /// <summary>80차(민: 「배터리 위치를 경비실 맵보드에 표기 — 매일 밤 상시로」): 배터리 점 이름 머리.</summary>
    public const string BatteryDotPrefix = "MapBattery ";

    /// <summary>배터리 점 지름(m) — 현재 위치 점보다 조금 작게. 90차부터 배터리는 그림(<see cref="BatteryIcon"/>)이라 쓰지 않는다.</summary>
    public const float BatteryDiameter = 0.016f;

    /// <summary>배터리 점 색(호박색 — 빨간 「현재 위치」와 갈린다).</summary>
    public static readonly Color BatteryColor = new Color(1f, 0.72f, 0.12f);

    /// <summary>점을 맞추는 간격(초) — 주운 배터리 점은 다음 맞춤에서 사라진다.</summary>
    public const float SyncSeconds = 0.5f;

    /// <summary>지도 그림 크기(px) — <c>Map_KR_Aged.png</c>.</summary>
    public static readonly Vector2 MapPixels = new Vector2(1208f, 546f);

    /// <summary>
    /// 방마다 지도 그림 속 방 안쪽 사각형(px, 왼쪽 위 원점)과 판정 공간 상자(<see cref="SpaceZones"/>)를 짝짓는다.
    /// 지도는 위에서 내려다본 그림으로 오른쪽 = 월드 −x, 아래쪽 = 월드 +z(80차 실측).
    /// </summary>
    private static readonly KeyValuePair<SpaceId, Rect>[] RoomRects =
    {
        new KeyValuePair<SpaceId, Rect>(SpaceId.Classroom_1_3, Rect.MinMaxRect(133f, 50f, 372f, 188f)),
        new KeyValuePair<SpaceId, Rect>(SpaceId.ScienceRoom, Rect.MinMaxRect(135f, 192f, 292f, 272f)),
        new KeyValuePair<SpaceId, Rect>(SpaceId.SecurityRoom, Rect.MinMaxRect(352f, 252f, 480f, 355f)),
        new KeyValuePair<SpaceId, Rect>(SpaceId.Toilet, Rect.MinMaxRect(933f, 124f, 1033f, 213f)),
        new KeyValuePair<SpaceId, Rect>(SpaceId.Library, Rect.MinMaxRect(697f, 277f, 940f, 503f))
    };

    private readonly Dictionary<string, GameObject> _batteryDots = new Dictionary<string, GameObject>();
    private float _nextSync;
    private Material _batteryMat;
    private bool _fitReady;
    private float _ua, _ub, _uc, _va, _vb, _vc;   // 복도 등 방 밖: u = ua + ub·x + uc·z, v = va + vb·x + vc·z (px)

    /// <summary>지도 종이 재질을 이 인스턴스만 스스로 빛나게(그림을 발광 맵으로).</summary>
    public static void Brighten(Transform paper)
    {
        if (paper == null) return;
        MeshRenderer r = paper.GetComponent<MeshRenderer>();
        if (r == null || r.sharedMaterial == null) return;
        Material m = r.material;   // 인스턴스
        Texture tex = m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : m.mainTexture;
        if (m.HasProperty("_EmissionMap")) m.SetTexture("_EmissionMap", tex);
        if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", Color.white * PaperGlow);
        m.EnableKeyword("_EMISSION");
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
    }

    /// <summary>월드 점 → 지도 그림 px(왼쪽 위 원점). 방 안이면 그 방 사각형 비례, 아니면 방 사각형 모서리로 맞춘 근사.</summary>
    public bool WorldToMapPixel(Vector3 world, out Vector2 px)
    {
        px = Vector2.zero;
        SpaceZones zones = FindFirstObjectByType<SpaceZones>();
        if (zones == null) return false;
        for (int i = 0; i < RoomRects.Length; i++)
        {
            Bounds box;
            if (!zones.TryGetSpaceBox(RoomRects[i].Key, out box)) continue;
            if (world.x < box.min.x || world.x > box.max.x || world.z < box.min.z || world.z > box.max.z) continue;
            Rect r = RoomRects[i].Value;
            float u = Mathf.InverseLerp(box.max.x, box.min.x, world.x);   // 오른쪽 = −x
            float v = Mathf.InverseLerp(box.min.z, box.max.z, world.z);   // 아래 = +z
            px = new Vector2(Mathf.Lerp(r.xMin, r.xMax, u), Mathf.Lerp(r.yMin, r.yMax, v));
            return true;
        }

        if (!_fitReady) Fit(zones);
        if (!_fitReady) return false;
        px = new Vector2(_ua + _ub * world.x + _uc * world.z, _va + _vb * world.x + _vc * world.z);
        return true;
    }

    /// <summary>방 사각형 네 모서리 ↔ 공간 상자 네 모서리로 최소제곱 아핀 맞춤.</summary>
    private void Fit(SpaceZones zones)
    {
        List<Vector2> w = new List<Vector2>();
        List<Vector2> p = new List<Vector2>();
        for (int i = 0; i < RoomRects.Length; i++)
        {
            Bounds box;
            if (!zones.TryGetSpaceBox(RoomRects[i].Key, out box)) continue;
            Rect r = RoomRects[i].Value;
            w.Add(new Vector2(box.max.x, box.min.z)); p.Add(new Vector2(r.xMin, r.yMin));
            w.Add(new Vector2(box.min.x, box.min.z)); p.Add(new Vector2(r.xMax, r.yMin));
            w.Add(new Vector2(box.max.x, box.max.z)); p.Add(new Vector2(r.xMin, r.yMax));
            w.Add(new Vector2(box.min.x, box.max.z)); p.Add(new Vector2(r.xMax, r.yMax));
        }

        if (w.Count < 3) return;
        double[] su = Solve(w, p, true);
        double[] sv = Solve(w, p, false);
        if (su == null || sv == null) return;
        _ua = (float)su[0]; _ub = (float)su[1]; _uc = (float)su[2];
        _va = (float)sv[0]; _vb = (float)sv[1]; _vc = (float)sv[2];
        _fitReady = true;
    }

    private static double[] Solve(List<Vector2> w, List<Vector2> p, bool u)
    {
        double[,] a = new double[3, 4];
        for (int i = 0; i < w.Count; i++)
        {
            double[] row = { 1.0, w[i].x, w[i].y };
            double t = u ? p[i].x : p[i].y;
            for (int r = 0; r < 3; r++)
            {
                for (int c = 0; c < 3; c++) a[r, c] += row[r] * row[c];
                a[r, 3] += row[r] * t;
            }
        }

        for (int col = 0; col < 3; col++)
        {
            int piv = col;
            for (int r = col + 1; r < 3; r++) if (System.Math.Abs(a[r, col]) > System.Math.Abs(a[piv, col])) piv = r;
            if (System.Math.Abs(a[piv, col]) < 1e-9) return null;
            for (int c = 0; c < 4; c++) { double tmp = a[col, c]; a[col, c] = a[piv, c]; a[piv, c] = tmp; }
            for (int r = 0; r < 3; r++)
            {
                if (r == col) continue;
                double f = a[r, col] / a[col, col];
                for (int c = col; c < 4; c++) a[r, c] -= f * a[col, c];
            }
        }

        return new[] { a[0, 3] / a[0, 0], a[1, 3] / a[1, 1], a[2, 3] / a[2, 2] };
    }

    /// <summary>오늘 놓인(아직 줍지 않은) 배터리마다 지도에 호박색 점. 주웠거나 밤이 아니면 지운다.</summary>
    private void SyncBatteries(Transform paper)
    {
        if (paper == null) return;
        BatteryPlan plan = NightRun.BatteryEnabled ? NightRun.BatteryPlan : null;
        BatteryStash stash = BatteryStash.Active;
        HashSet<string> want = new HashSet<string>();
        if (plan != null && stash != null && NightRun.IsNightActive)
        {
            foreach (string id in plan.Placed)
            {
                if (!plan.Holds(id)) continue;
                Vector3 at;
                Vector2 px;
                if (!stash.TryGetCachePosition(id, out at) || !WorldToMapPixel(at, out px)) continue;
                want.Add(id);
                if (!_batteryDots.ContainsKey(id)) _batteryDots[id] = BatteryIcon(paper, BatteryDotPrefix + id, new Vector2(px.x / MapPixels.x, 1f - px.y / MapPixels.y), BatteryMaterial(), PlateMaterial());   // 90차: 원 → 배터리 그림
            }
        }

        List<string> drop = new List<string>();
        foreach (KeyValuePair<string, GameObject> kv in _batteryDots)
        {
            if (!want.Contains(kv.Key)) drop.Add(kv.Key);
        }

        for (int i = 0; i < drop.Count; i++)
        {
            if (_batteryDots[drop[i]] != null) Destroy(_batteryDots[drop[i]]);
            _batteryDots.Remove(drop[i]);
        }
    }

    private Material BatteryMaterial()
    {
        if (_batteryMat == null) _batteryMat = UnlitMaterial(BatteryIconColor);   // 90차: 그림 색(옛 점 BatteryColor는 노출에 하얗게 날았다)
        return _batteryMat;
    }

    private Material _plateMat;

    private Material PlateMaterial()
    {
        if (_plateMat == null) _plateMat = UnlitMaterial(BatteryPlateColor);
        return _plateMat;
    }

    private static Material UnlitMaterial(Color color)
    {
        Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
        Material m = new Material(unlit != null ? unlit : Shader.Find("Unlit/Color"));
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
        m.color = color;
        return m;
    }

    /// <summary>90차(민: 「배터리 맵 표기 아이콘을 원 대신 인터페이스와 같은 아이콘으로」): 배터리 그림 가로(m, 어두운 바탕판 포함).</summary>
    public const float BatteryIconWidth = 0.05f;

    /// <summary>90차: 배터리 그림 색 — 초록(민: 「배터리 아이콘과 인터페이스는 초록색」, 화면 배터리 표시와 같은 색). 실내 노출(+2.5EV)에서 하얗게 날지 않게 어둡게 잡았다.</summary>
    public static readonly Color BatteryIconColor = new Color(0.12f, 0.5f, 0.17f);

    /// <summary>90차: 배터리 그림 바탕판 색(HUD처럼 어두운 바탕 위 밝은 그림).</summary>
    public static readonly Color BatteryPlateColor = new Color(0.035f, 0.035f, 0.035f);

    /// <summary>
    /// 90차: 지도 위 배터리 그림 — 화면 배터리 표시(<see cref="BatteryHud"/>)와 같은 모양: 테두리 본체 50×22 + 오른쪽 꼭지 4×10 + 안쪽 충전 막대(가득).
    /// 테두리는 HUD(2)보다 굵게 3 — 벽 지도를 떨어져 볼 때도 보이게. 조각은 조명 없는 호박색 사각 판(Quad), 종이 앞면(−Z)을 본다. 콜라이더·그림자 없음.
    /// </summary>
    public static GameObject BatteryIcon(Transform paper, string name, Vector2 uv, Material mat, Material plate = null)
    {
        GameObject root = new GameObject(name);
        root.transform.SetParent(paper, false);
        Vector3 normal = paper.TransformDirection(Vector3.back).normalized;
        root.transform.position = paper.TransformPoint(new Vector3(uv.x - 0.5f, uv.y - 0.5f, 0f)) + normal * Lift;
        root.transform.rotation = paper.rotation;
        Vector3 s = paper.lossyScale;
        root.transform.localScale = new Vector3(1f / NonZero(s.x), 1f / NonZero(s.y), 1f / NonZero(s.z));   // 조각은 미터 단위로

        float u = BatteryIconWidth / 62f;   // HUD 그림 전체 폭 54(본체 50 + 꼭지 4) + 바탕판 여백 4씩
        if (plate != null) Piece(root.transform, plate, -31f * u, -15f * u, 62f * u, 30f * u, 0.0005f);   // 바탕판(종이 쪽으로 0.5mm)
        float bw = 50f * u, bh = 22f * u, line = 3f * u, pad = 3f * u;
        float x0 = -27f * u, y0 = -bh * 0.5f;
        Piece(root.transform, mat, x0, y0 + bh - line, bw, line);           // 위
        Piece(root.transform, mat, x0, y0, bw, line);                       // 아래
        Piece(root.transform, mat, x0, y0 + line, line, bh - line * 2f);    // 왼쪽
        Piece(root.transform, mat, x0 + bw - line, y0 + line, line, bh - line * 2f);   // 오른쪽
        Piece(root.transform, mat, x0 + bw, -5f * u, 4f * u, 10f * u);     // 꼭지
        float inset = line + pad;
        Piece(root.transform, mat, x0 + inset, y0 + inset, bw - inset * 2f, bh - inset * 2f);   // 충전 막대
        return root;
    }

    private static float NonZero(float v)
    {
        return Mathf.Abs(v) < 1e-4f ? (v < 0f ? -1e-4f : 1e-4f) : v;
    }

    /// <summary>배터리 그림 조각 — 왼쪽 아래(x, y)에서 가로 w·세로 h(m)인 사각 판.</summary>
    private static void Piece(Transform parent, Material mat, float x, float y, float w, float h, float z = 0f)
    {
        GameObject q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        q.name = "piece";
        Collider c = q.GetComponent<Collider>();
        if (c != null) Destroy(c);
        q.transform.SetParent(parent, false);
        q.transform.localPosition = new Vector3(x + w * 0.5f, y + h * 0.5f, z);
        q.transform.localRotation = Quaternion.identity;
        q.transform.localScale = new Vector3(w, h, 1f);
        MeshRenderer r = q.GetComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
    }

    /// <summary>지도 종이 위 UV 자리에 납작한 점 하나(MapBoard 아래).</summary>
    private static GameObject Dot(Transform paper, string name, Vector2 uv, float diameter, Material mat)
    {
        Transform holder = paper.parent != null ? paper.parent : paper;
        Vector3 surface = paper.TransformPoint(new Vector3(uv.x - 0.5f, uv.y - 0.5f, 0f));
        Vector3 normal = paper.TransformDirection(Vector3.back).normalized;
        GameObject dot = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        dot.name = name;
        Collider c = dot.GetComponent<Collider>();
        if (c != null) Destroy(c);
        dot.transform.SetParent(holder, true);
        dot.transform.position = surface + normal * Lift;
        dot.transform.rotation = Quaternion.FromToRotation(Vector3.up, normal);
        Vector3 s = holder.lossyScale;
        dot.transform.localScale = new Vector3(diameter / Mathf.Max(1e-4f, Mathf.Abs(s.x)), 0.0008f / Mathf.Max(1e-4f, Mathf.Abs(s.y)), diameter / Mathf.Max(1e-4f, Mathf.Abs(s.z)));
        MeshRenderer renderer = dot.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = mat;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return dot;
    }

    /// <summary>그 씬의 지도 종이. 없으면 null.</summary>
    public static Transform FindPaper(Scene scene)
    {
        foreach (MeshRenderer r in FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (r.gameObject.scene == scene && r.name == PaperName) return r.transform;
        }

        return null;
    }

    /// <summary>점을 세운다(이미 있으면 그것). 지도가 없으면 null.</summary>
    public static GameObject Place(Scene scene)
    {
        Transform paper = FindPaper(scene);
        if (paper == null) return null;
        Transform holder = paper.parent != null ? paper.parent : paper;
        Transform existing = holder.Find(DotName);
        if (existing != null) return existing.gameObject;

        Vector3 surface = paper.TransformPoint(new Vector3(SecurityRoomUv.x - 0.5f, SecurityRoomUv.y - 0.5f, 0f));
        Vector3 normal = paper.TransformDirection(Vector3.back).normalized;   // Quad 앞면은 −Z

        GameObject dot = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        dot.name = DotName;
        Collider c = dot.GetComponent<Collider>();
        if (c != null) Destroy(c);
        dot.transform.SetParent(holder, true);
        dot.transform.position = surface + normal * Lift;
        dot.transform.rotation = Quaternion.FromToRotation(Vector3.up, normal);
        Vector3 s = holder.lossyScale;
        dot.transform.localScale = new Vector3(Diameter / Mathf.Max(1e-4f, Mathf.Abs(s.x)), 0.0008f / Mathf.Max(1e-4f, Mathf.Abs(s.y)), Diameter / Mathf.Max(1e-4f, Mathf.Abs(s.z)));

        MeshRenderer renderer = dot.GetComponent<MeshRenderer>();
        Material m = Resources.Load<Material>(MaterialPath);
        if (m != null) renderer.sharedMaterial = m;
        else renderer.material.color = DotColor;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return dot;
    }
}

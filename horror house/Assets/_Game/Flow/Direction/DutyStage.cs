using System.Collections.Generic;
using NightDuty;
using UnityEngine;

/// <summary>
/// 54차 [근무 지시]의 무대 — 코어 지시기(<see cref="DutyDispatcher"/>)가 듣는 판정 지점을 런타임에만 세운다(씬은 고치지 않는다).
/// <list type="bullet">
/// <item>W2 도서관 순찰: 숨은 지점 네 곳(입구 · 열람석 · 서가 안 · 창가)을 런타임 신호 구역 <c>duty.library.1~4</c>로(<see cref="FinalRuleRelay.SetRuntimeZone"/>).
/// 자리는 53차 바닥 측량으로 고른 통로 위(PlayScene 좌표).</item>
/// <item>W3 1-3 교실 소등 확인: 문간 구역 <c>duty.c13.door</c>(교실 안 비상구 표지 밑) + 교실 가운데에 가장 가까운 천장 형광등에 응시 대상 <c>duty.c13.light</c>.</item>
/// <item>W6 유도등: 복도 동쪽 끝 구역 <c>duty.exit.end</c> + 그 끝의 <c>Sign_Exit</c>에 응시 대상 <c>duty.exit.sign</c>. 돌아오는 곳은 경비실(공간 신호).</item>
/// <item>W1 CCTV 순회: 씬 CCTV의 채널 수를 코어에 알린다.</item>
/// <item>W5 근무일지(54차 QA): 경비 책상 위 펼친 장부(<c>BookOpen*</c>, 없으면 <see cref="LogBookFallback"/>)에 서명 조준 상자 + <see cref="DutyLogBook"/>.
/// [근무 지시]가 꺼져 있어도 세운다 — 02:16 체크포인트 서명은 지시와 별개의 장치다.</item>
/// </list>
/// 응시 대상은 응시 원뿔이 보도록 트리거가 아닌 상자 콜라이더를 등보다 조금 크게 붙인다(등 자체 콜라이더가 먼저 맞아 가려지지 않게). DirectionStage가 붙인다.
/// </summary>
[DisallowMultipleComponent]
public sealed class DutyStage : MonoBehaviour
{
    /// <summary>도서관 순찰 지점(바닥 기준 x, z).</summary>
    public static readonly Vector2[] LibraryPoints =
    {
        // 57차: 서가 안(6.5, 53)은 폭 1m 통로 안에만, 창가(2.8, 45)는 책장 위(바닥 레이가 y 2.12 책장에 닿음)였다 — 트인 바닥으로 옮기고 구역을 키웠다.
        new Vector2(12.0f, 42f),   // 입구
        new Vector2(8.5f, 47f),    // 열람석
        new Vector2(6.0f, 51f),    // 서가 통로 끝(트인 줄 z 50~51)
        new Vector2(5.0f, 46.5f),  // 창가 쪽(트인 곳 x 4.5~7)
    };

    /// <summary>순찰 지점 구역 크기(m).</summary>
    public static readonly Vector3 PointSize = new Vector3(4f, 4f, 4f);   // 57차: 2.6 → 4

    /// <summary>1-3 교실 문간(안쪽 문턱 — 비상구 표지 Sign_Exit (4) 밑).</summary>
    public static readonly Bounds ClassroomDoor = new Bounds(new Vector3(40f, 2.5f, 39.4f), new Vector3(2.6f, 4f, 2.6f));

    /// <summary>복도 동쪽 끝.</summary>
    public static readonly Bounds ExitEnd = new Bounds(new Vector3(52f, 2.5f, 45.5f), new Vector3(3.2f, 4f, 5f));

    /// <summary>근무일지 자리(경비 책상 위 펼친 장부 — PlayScene 실측). 장부를 찾지 못하면 여기에 세운다.</summary>
    public static readonly Vector3 LogBookFallback = new Vector3(31.82f, 2.12f, 47.47f);

    private readonly List<GameObject> _targets = new List<GameObject>();
    private DutyPickupItem _book;

    /// <summary>65차 W15 줍기 대상 — 도서관 안쪽 책상 위 biology 책.</summary>
    public const string LibraryBookPath = "Interior/Library/Book16 (2)";
    private bool _built;
    private bool _bookBuilt;

    private void OnDisable()
    {
        for (int i = 1; i <= LibraryPoints.Length; i++) FinalRuleRelay.ClearRuntimeZone(DutyCatalog.LibraryZonePrefix + i);
        FinalRuleRelay.ClearRuntimeZone(DutyCatalog.ClassroomDoorZone);
        FinalRuleRelay.ClearRuntimeZone(DutyCatalog.ExitZone);
        for (int i = 0; i < _targets.Count; i++)
        {
            if (_targets[i] != null) Destroy(_targets[i]);
        }

        _targets.Clear();
        if (_book != null) Destroy(_book);
        _book = null;
        _built = false;
        _bookBuilt = false;
    }

    private void Update()
    {
        if (!NightRun.IsNightActive) return;
        if (!_bookBuilt)
        {
            _bookBuilt = true;
            BuildLogBook();
        }

        if (NightRun.Duties == null) return;

        CctvSystem cctv = CctvSystem.Active;
        if (cctv != null && cctv.ChannelCount > 0) NightRun.DutyCctvChannels = cctv.ChannelCount;

        if (_built) return;
        _built = true;
        Build();
    }

    private void Build()
    {
        for (int i = 0; i < LibraryPoints.Length; i++)
        {
            Vector2 p = LibraryPoints[i];
            FinalRuleRelay.SetRuntimeZone(DutyCatalog.LibraryZonePrefix + (i + 1), new Bounds(new Vector3(p.x, 2.5f, p.y), PointSize));
        }

        FinalRuleRelay.SetRuntimeZone(DutyCatalog.ClassroomDoorZone, ClassroomDoor);
        FinalRuleRelay.SetRuntimeZone(DutyCatalog.ExitZone, ExitEnd);

        GameObject book = GameObject.Find("/" + LibraryBookPath);
        if (book != null)
        {
            _book = book.GetComponent<DutyPickupItem>();
            if (_book == null) _book = book.AddComponent<DutyPickupItem>();
            _book.Setup(DutyCatalog.LibraryBookTarget, "W15", "[E] biology 책 줍기");
        }
        else Debug.LogWarning("[DutyStage] 도서관 biology 책을 찾지 못했습니다(W15): " + LibraryBookPath);

        Renderer lamp = ClassroomLamp();
        if (lamp != null) AddTarget(DutyCatalog.ClassroomLightTarget, lamp.bounds, 1.15f);
        else Debug.LogWarning("[DutyStage] 1-3 교실 천장 등을 찾지 못했습니다(W3).");

        Renderer sign = ExitSign();
        if (sign != null)
        {
            AddTarget(DutyCatalog.ExitSignTarget, sign.bounds, 1.3f);
            ExitGlow(sign.bounds);
        }
        else Debug.LogWarning("[DutyStage] 복도 끝 유도등을 찾지 못했습니다(W6).");

        Debug.Log("[DutyStage] 근무 지시 지점 — 도서관 " + LibraryPoints.Length + " · 1-3 문간 · 복도 끝 / 등 " + (lamp != null ? lamp.name : "없음") + " · 유도등 " + (sign != null ? sign.name : "없음"));
    }

    /// <summary>경비 책상 위 근무일지 — 서명 조준 상자(평소엔 꺼짐, 이완 구간에만 <see cref="DutyLogBook"/>이 켠다).</summary>
    private void BuildLogBook()
    {
        Renderer book = GuardLogBook();
        Bounds b = book != null ? book.bounds : new Bounds(LogBookFallback, new Vector3(0.3f, 0.03f, 0.24f));
        GameObject go = new GameObject("duty.log.book");
        go.transform.position = b.center + Vector3.up * 0.03f;
        BoxCollider box = go.AddComponent<BoxCollider>();
        box.size = new Vector3(Mathf.Max(0.38f, b.size.x * 1.25f), 0.1f, Mathf.Max(0.32f, b.size.z * 1.25f));
        box.enabled = false;
        DutyLogBook log = go.AddComponent<DutyLogBook>();
        log.SetOutlineTarget(book != null ? book.transform : null);
        _targets.Add(go);
        Debug.Log("[DutyStage] 근무일지 — " + (book != null ? book.name : "장부 없음(고정 자리)") + " @ " + go.transform.position);
    }

    /// <summary>경비실 상자 안 펼친 책(BookOpen*) 중 경비 책상 자리에 가장 가까운 것.</summary>
    private static Renderer GuardLogBook()
    {
        SpaceZones zones = FindAnyObjectByType<SpaceZones>();
        Bounds room;
        if (zones == null || !zones.TryGetSpaceBox(SpaceId.SecurityRoom, out room)) return null;

        Renderer best = null;
        float bestD = float.MaxValue;
        foreach (MeshRenderer r in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
        {
            if (!r.name.StartsWith("BookOpen") || !r.gameObject.activeInHierarchy) continue;
            Vector3 c = r.bounds.center;
            if (c.x < room.min.x || c.x > room.max.x || c.z < room.min.z || c.z > room.max.z || c.y < room.min.y || c.y > room.max.y) continue;
            float d = (c - LogBookFallback).sqrMagnitude;
            if (d < bestD)
            {
                bestD = d;
                best = r;
            }
        }

        return best;
    }

    /// <summary>유도등이 꺼진 채라 손전등 없이는 찾을 수 없었다 — 표지 앞에 희미한 초록 점광(늘 켜진 비상등, 지시와 무관).</summary>
    private void ExitGlow(Bounds b)
    {
        GameObject go = new GameObject("유도등 빛");
        go.transform.position = b.center + new Vector3(-0.25f, -0.1f, 0f);
        Light l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = new Color(0.35f, 1f, 0.5f);
        l.range = 1.8f;
        l.intensity = 0.045f;
        l.shadows = LightShadows.None;
        _targets.Add(go);
    }

    private void AddTarget(string id, Bounds b, float grow)
    {
        GameObject go = new GameObject(id);
        go.transform.position = b.center;
        BoxCollider box = go.AddComponent<BoxCollider>();
        Vector3 size = b.size * grow;
        box.size = new Vector3(Mathf.Max(0.3f, size.x), Mathf.Max(0.2f, size.y), Mathf.Max(0.3f, size.z));
        JudgeTarget t = go.AddComponent<JudgeTarget>();
        t.SetIds(id);
        _targets.Add(go);
    }

    /// <summary>1-3 교실 상자 안 천장 형광등 중 교실 가운데에 가장 가까운 것.</summary>
    private static Renderer ClassroomLamp()
    {
        SpaceZones zones = FindAnyObjectByType<SpaceZones>();
        Bounds room;
        if (zones == null || !zones.TryGetSpaceBox(SpaceId.Classroom_1_3, out room)) return null;

        Renderer best = null;
        float bestD = float.MaxValue;
        foreach (MeshRenderer r in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
        {
            string n = r.name;
            if (!n.StartsWith("LampFluo") || n.Contains("_LOD1") || n.Contains("_LOD2") || !r.gameObject.activeInHierarchy) continue;
            Vector3 c = r.bounds.center;
            if (c.x < room.min.x || c.x > room.max.x || c.z < room.min.z || c.z > room.max.z) continue;
            float d = new Vector2(c.x - room.center.x, c.z - room.center.z).sqrMagnitude;
            if (d < bestD)
            {
                bestD = d;
                best = r;
            }
        }

        return best;
    }

    /// <summary>복도 동쪽 끝 구역에 가장 가까운 비상구 표지(Sign_Exit*).</summary>
    private static Renderer ExitSign()
    {
        Renderer best = null;
        float bestD = float.MaxValue;
        foreach (Renderer r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
        {
            Transform t = r.transform;
            bool sign = false;
            for (Transform p = t; p != null && !sign; p = p.parent) sign = p.name.StartsWith("Sign_Exit");
            if (!sign || !r.gameObject.activeInHierarchy) continue;
            Vector3 c = r.bounds.center;
            if (c.y > 6f) continue;   // 위층 표지 제외
            float d = new Vector2(c.x - ExitEnd.center.x, c.z - ExitEnd.center.z).sqrMagnitude;
            if (d < bestD)
            {
                bestD = d;
                best = r;
            }
        }

        return best;
    }
}

using System.Collections.Generic;
using NightDuty;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 65차 H-4 쓰레기통(민: 「쓰레기통이 정상인지 보고하십시오. 정상이면 아무것도 안 일어나고, 이상이 있으면 물리효과를 일으키며 누가 발로 찬 것처럼
/// 깡통 효과음과 같이 멀리 날아가는 작은 놀람」).
/// <list type="bullet">
/// <item><b>점검 대상</b>: 복도 서쪽(도서관 쪽) 큰 쓰레기통 <see cref="PropPath"/>에 런타임 점검 대상 <c>inspect.H-4</c>(소품을 감싸는 상자)를 세운다 — 씬은 고치지 않는다.
/// 씬에 이미 있으면(점검 대상 배치 메뉴를 다시 돌렸으면) 그것을 쓴다. 옛 L-3 반납 상자의 씬 대상(<c>inspect.L-3</c>)은 근무 중 끈다.</item>
/// <item><b>이상</b>(<see cref="InspectionAnomalies"/>가 <see cref="Arm"/>): 플레이어가 복도에서 쓰레기통을 <see cref="TriggerDistance"/>m 안에서 0.25초 보면
/// 원본(정적 배칭이라 움직일 수 없다)을 숨기고 같은 프리팹 복제(<c>StandIns/prop.trashcan</c>)를 그 자리에 세워 걷어찬다 — 방향은 15°마다 잰 트인 거리와
/// 「플레이어 반대쪽」을 함께 본다(<see cref="OpenDirection"/> — 이 쓰레기통은 도서관 문 옆 구석이라 반대쪽은 벽, 실측으로는 복도를 따라 6.7m 굴러 플레이어 옆을 지나감).
/// 수평 최대 <see cref="KickSpeed"/>m/s · 위로 <see cref="LiftSpeed"/>m/s · 구르는 회전, 깡통 소리(<see cref="SoundKey"/>)가 깡통을 따라간다.
/// 점검 대상은 복제를 따라가 떨어진 자리에서 [이상]을 보고한다. 편성이 바뀌면(<see cref="Disarm"/>) 제자리로.</item>
/// <item>정상이면 아무 일도 없다.</item>
/// </list>
/// 근무 씬에 자동으로 선다.
/// </summary>
[DisallowMultipleComponent]
public sealed class TrashCanKick : MonoBehaviour
{
    public const string ItemId = "H-4";
    public const string PropPath = "Interior/Corridors/TrashCanBig_A (1)";
    public const string StandInId = "prop.trashcan";
    public const string SoundKey = "inspect.H-4.kick";

    /// <summary>이 거리(수평 m) 안에서 보면 걷어차인다.</summary>
    public const float TriggerDistance = 7f;

    /// <summary>이만큼(초) 이어서 보면.</summary>
    public const float SeeSeconds = 0.25f;

    public const float KickSpeed = 7.5f;
    public const float LiftSpeed = 2.6f;

    private const string ChildName = "Inspect H-4";
    private const float Inflate = 0.04f;

    private Transform _prop;
    private Transform _target;
    private bool _ownTarget;
    private Vector3 _targetLocalPos;
    private Quaternion _targetLocalRot;
    private bool _armed;
    private bool _kicked;
    private float _seenFor;
    private GameObject _copy;
    private readonly List<Renderer> _hiddenRenderers = new List<Renderer>();
    private readonly List<Collider> _hiddenColliders = new List<Collider>();
    private readonly List<GameObject> _staleTargets = new List<GameObject>();

    /// <summary>지금 근무 씬의 것. 없으면 null.</summary>
    public static TrashCanKick Active { get; private set; }

    /// <summary>이상이 걸려 있는지(걷어차이기 전 포함).</summary>
    public bool Armed
    {
        get { return _armed; }
    }

    /// <summary>걷어차였는지.</summary>
    public bool Kicked
    {
        get { return _kicked; }
    }

    /// <summary>날아간 복제. 없으면 null.</summary>
    public GameObject Copy
    {
        get { return _copy; }
    }

    /// <summary>씬 쓰레기통. 없으면 null.</summary>
    public Transform Prop
    {
        get { return _prop; }
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
        if (!FlowAutoInstall.IsDutyScene(scene) || FlowAutoInstall.Exists<TrashCanKick>(scene)) return;
        FlowAutoInstall.CreateHost<TrashCanKick>(scene, "TrashCanKick (auto)");
    }

    private void OnEnable()
    {
        Active = this;
        EnsureTarget();
        HideStaleTargets();
    }

    private void OnDisable()
    {
        Disarm();
        for (int i = 0; i < _staleTargets.Count; i++)
        {
            if (_staleTargets[i] != null) _staleTargets[i].SetActive(true);
        }

        _staleTargets.Clear();
        if (_ownTarget && _target != null) Destroy(_target.gameObject);
        _target = null;
        if (Active == this) Active = null;
    }

    // ── 점검 대상 ───────────────────────────────────────────

    private void EnsureTarget()
    {
        GameObject host = GameObject.Find("/" + PropPath);
        if (host == null)
        {
            Debug.LogWarning("[TrashCanKick] 씬에 쓰레기통이 없습니다: " + PropPath);
            return;
        }

        _prop = host.transform;
        JudgeTarget jt;
        string id = InspectionCatalog.TargetPrefix + ItemId;
        if (JudgeTargetRegistry.TryGet(id, out jt) && jt != null)
        {
            _target = jt.transform;
            _ownTarget = false;
        }
        else
        {
            Bounds b;
            if (!PropBounds(out b)) return;
            GameObject child = new GameObject(ChildName);
            child.layer = host.layer;
            child.transform.SetParent(_prop, false);
            child.transform.SetPositionAndRotation(b.center, Quaternion.identity);
            BoxCollider box = child.AddComponent<BoxCollider>();
            Vector3 s = child.transform.lossyScale;
            Vector3 size = b.size + Vector3.one * Inflate;
            box.size = new Vector3(size.x / Mathf.Max(0.0001f, Mathf.Abs(s.x)), size.y / Mathf.Max(0.0001f, Mathf.Abs(s.y)), size.z / Mathf.Max(0.0001f, Mathf.Abs(s.z)));
            JudgeTarget t = child.AddComponent<JudgeTarget>();
            t.SetIds(id);
            _target = child.transform;
            _ownTarget = true;
        }

        _targetLocalPos = _target.localPosition;
        _targetLocalRot = _target.localRotation;
    }

    /// <summary>옛 L-3 반납 상자 씬 대상은 카탈로그에서 빠졌다 — 응시가 엉뚱한 ID를 내지 않게 근무 중 끈다.</summary>
    private void HideStaleTargets()
    {
        foreach (JudgeTarget t in FindObjectsByType<JudgeTarget>(FindObjectsSortMode.None))
        {
            IReadOnlyList<string> ids = t.Ids;
            for (int i = 0; i < ids.Count; i++)
            {
                if (ids[i] == null || ids[i].Trim() != InspectionCatalog.TargetPrefix + "L-3") continue;
                t.gameObject.SetActive(false);
                _staleTargets.Add(t.gameObject);
                break;
            }
        }
    }

    /// <summary>쓰레기통 LOD0 렌더러를 감싸는 월드 상자.</summary>
    private bool PropBounds(out Bounds bounds)
    {
        bounds = new Bounds();
        if (_prop == null) return false;
        bool any = false;
        LODGroup lod = _prop.GetComponent<LODGroup>();
        Renderer[] first = null;
        if (lod != null)
        {
            LOD[] lods = lod.GetLODs();
            if (lods.Length > 0) first = lods[0].renderers;
        }

        Renderer[] all = first ?? _prop.GetComponentsInChildren<Renderer>();
        for (int i = 0; i < all.Length; i++)
        {
            Renderer r = all[i];
            if (r == null || r.name.StartsWith("Inspect ")) continue;
            if (!any)
            {
                bounds = r.bounds;
                any = true;
            }
            else
            {
                bounds.Encapsulate(r.bounds);
            }
        }

        return any;
    }

    // ── 이상 ─────────────────────────────────────────────────

    /// <summary>이상을 건다 — 이제 보면 걷어차인다. 쓰레기통이 없으면 false.</summary>
    public bool Arm()
    {
        if (_prop == null || _target == null) return false;
        Disarm();
        _armed = true;
        _kicked = false;
        _seenFor = 0f;
        return true;
    }

    /// <summary>이상을 거두고 쓰레기통을 제자리로(복제를 지우고 원본을 다시 보인다).</summary>
    public void Disarm()
    {
        _armed = false;
        _kicked = false;
        _seenFor = 0f;
        if (_target != null && _prop != null && _target.parent != _prop)
        {
            _target.SetParent(_prop, false);
            _target.localPosition = _targetLocalPos;
            _target.localRotation = _targetLocalRot;
        }

        if (_copy != null) Destroy(_copy);
        _copy = null;
        for (int i = 0; i < _hiddenRenderers.Count; i++)
        {
            if (_hiddenRenderers[i] != null) _hiddenRenderers[i].enabled = true;
        }

        for (int i = 0; i < _hiddenColliders.Count; i++)
        {
            if (_hiddenColliders[i] != null) _hiddenColliders[i].enabled = true;
        }

        _hiddenRenderers.Clear();
        _hiddenColliders.Clear();
    }

    private void Update()
    {
        if (!_armed || _kicked || _prop == null) return;
        if (!NightRun.IsNightActive || NightRun.IsCaptured)
        {
            _seenFor = 0f;
            return;
        }

        Camera cam = Camera.main;
        Transform player = PlayerSensors.Active != null ? PlayerSensors.Active.PlayerRoot : null;
        if (cam == null || player == null || SpaceIds.Canonical(NightRun.CurrentSpace) != SpaceId.Corridor)
        {
            _seenFor = 0f;
            return;
        }

        Vector3 away = _prop.position - player.position;
        away.y = 0f;
        if (away.magnitude > TriggerDistance || !UnseenDespawn.VisibleTo(_prop.gameObject, cam))
        {
            _seenFor = 0f;
            return;
        }

        _seenFor += Time.deltaTime;
        if (_seenFor < SeeSeconds) return;
        if (away.sqrMagnitude < 1.4f)
        {
            away = cam.transform.forward;
            away.y = 0f;
        }

        Kick(away.sqrMagnitude > 0.0001f ? away.normalized : Vector3.left);
    }

    /// <summary>지금 걷어찬다(디버그·시험이 부른다). 걸려 있지 않거나 이미 찼으면 false.</summary>
    public bool Kick(Vector3 direction)
    {
        if (!_armed || _kicked || _prop == null) return false;
        _kicked = true;

        Bounds b;
        PropBounds(out b);
        GameObject prefab = Resources.Load<GameObject>("StandIns/" + StandInId);
        if (prefab == null)
        {
            Debug.LogWarning("[TrashCanKick] StandIns/" + StandInId + "가 없어 소리만 냅니다(대역 빌더를 다시 돌리십시오).");
            PlaySound(_prop.gameObject, b.center);
            return true;
        }

        _copy = Instantiate(prefab, _prop.position, _prop.rotation);
        _copy.name = "쓰레기통 (걷어차임)";
        FitTo(_copy, b);

        foreach (Collider c in _copy.GetComponentsInChildren<Collider>(true)) c.enabled = false;
        foreach (LODGroup g in _copy.GetComponentsInChildren<LODGroup>(true)) g.ForceLOD(0);
        Bounds cb = RenderBounds(_copy);
        CapsuleCollider cap = _copy.AddComponent<CapsuleCollider>();
        float scale = Mathf.Max(0.0001f, _copy.transform.lossyScale.x);
        cap.direction = 1;
        cap.center = _copy.transform.InverseTransformPoint(cb.center);
        cap.radius = Mathf.Max(cb.extents.x, cb.extents.z) / scale;
        cap.height = Mathf.Max(cap.radius * 2f, cb.size.y / scale);

        // 원본(정적 배칭)은 숨기고, 점검 대상은 복제를 따라간다.
        foreach (Renderer r in _prop.GetComponentsInChildren<Renderer>())
        {
            if (!r.enabled) continue;
            r.enabled = false;
            _hiddenRenderers.Add(r);
        }

        foreach (Collider c in _prop.GetComponentsInChildren<Collider>())
        {
            if (_target != null && c.transform == _target) continue;
            if (!c.enabled) continue;
            c.enabled = false;
            _hiddenColliders.Add(c);
        }

        if (_target != null) _target.SetParent(_copy.transform, true);

        Rigidbody rb = _copy.AddComponent<Rigidbody>();
        rb.mass = 4f;
        rb.linearDamping = 0.15f;
        rb.angularDamping = 0.4f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        Transform player = PlayerSensors.Active != null ? PlayerSensors.Active.PlayerRoot : null;
        if (player != null)
        {
            foreach (Collider pc in player.GetComponentsInChildren<Collider>())
            {
                foreach (Collider cc in _copy.GetComponentsInChildren<Collider>()) Physics.IgnoreCollision(pc, cc, true);
            }
        }

        // 65차 실측: 복도 쓰레기통은 도서관 문 옆 구석(서쪽 벽 0.25m · 문 0.4m)이라 「플레이어 반대쪽」은 벽이다 — 트인 쪽을 고른다.
        float free;
        Vector3 dir = OpenDirection(cb.center, direction, player, out free);
        float speed = Mathf.Clamp(free * 1.5f, 4f, KickSpeed);
        rb.linearVelocity = dir * speed + Vector3.up * LiftSpeed;
        rb.angularVelocity = Vector3.Cross(Vector3.up, dir) * Random.Range(7f, 11f) + Random.insideUnitSphere * 3f;

        PlaySound(_copy, cb.center);
        if (DirectionStage.Verbose) Debug.Log("[TrashCanKick] H-4 쓰레기통이 걷어차였다 → " + dir.ToString("F2") + " 트인 " + free.ToString("F1") + "m · " + speed.ToString("F1") + "m/s");
        return true;
    }

    /// <summary>
    /// 날릴 방향 — 15°마다 반지름 0.22m 구를 쏘아 트인 거리(최대 7m)를 재고, 「트인 거리 + 1.5 × 플레이어 반대쪽 정도」가 가장 큰 쪽.
    /// 플레이어 몸을 정면으로 지나가는 방향은 뺀다(깡통이 몸을 뚫고 지나가 보이지 않게). <paramref name="free"/> = 그 방향의 트인 거리.
    /// </summary>
    private Vector3 OpenDirection(Vector3 from, Vector3 away, Transform player, out float free)
    {
        Vector3 origin = new Vector3(from.x, _prop.position.y + 0.35f, from.z);
        Vector3 toPlayer = player != null ? player.position - origin : -away;
        toPlayer.y = 0f;
        float playerDist = toPlayer.magnitude;
        Vector3 pDir = playerDist > 0.01f ? toPlayer / playerDist : -away;
        Vector3 best = away;
        float bestScore = float.NegativeInfinity;
        free = 0f;
        for (int a = 0; a < 360; a += 15)
        {
            Vector3 d = Quaternion.Euler(0f, a, 0f) * Vector3.forward;
            float f = 7f;
            foreach (RaycastHit h in Physics.SphereCastAll(origin, 0.22f, d, 7f, ~0, QueryTriggerInteraction.Ignore))
            {
                Transform ht = h.collider.transform;
                if (h.distance <= 0f || ht.IsChildOf(_prop) || (_copy != null && ht.IsChildOf(_copy.transform))) continue;
                if (player != null && ht.IsChildOf(player)) continue;
                f = Mathf.Min(f, h.distance);
            }

            float score = Mathf.Min(f, 6f) + 1.5f * Vector3.Dot(d, away);
            if (Vector3.Dot(d, pDir) > 0.92f && playerDist < f + 0.5f) score -= 4f;
            if (score > bestScore)
            {
                bestScore = score;
                best = d;
                free = f;
            }
        }

        return Quaternion.Euler(0f, Random.Range(-6f, 6f), 0f) * best;
    }

    /// <summary>복제의 렌더 상자를 원본 상자에 맞춘다(크기·가운데).</summary>
    private static void FitTo(GameObject copy, Bounds want)
    {
        Bounds have = RenderBounds(copy);
        if (have.size.y > 0.001f && want.size.y > 0.001f)
        {
            float k = want.size.y / have.size.y;
            if (k > 0.2f && k < 5f) copy.transform.localScale *= k;
        }

        have = RenderBounds(copy);
        copy.transform.position += want.center - have.center;
    }

    private static Bounds RenderBounds(GameObject go)
    {
        Bounds b = new Bounds(go.transform.position, Vector3.zero);
        bool any = false;
        foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
        {
            LODGroup g = r.GetComponentInParent<LODGroup>();
            if (g != null)
            {
                LOD[] lods = g.GetLODs();
                if (lods.Length > 0 && System.Array.IndexOf(lods[0].renderers, r) < 0) continue;
            }

            if (!any)
            {
                b = r.bounds;
                any = true;
            }
            else
            {
                b.Encapsulate(r.bounds);
            }
        }

        return b;
    }

    private static void PlaySound(GameObject follow, Vector3 at)
    {
        float volume;
        AudioClip clip = DirectionSoundTableSO.FindExact(SoundKey, out volume);
        if (clip == null) return;
        GameObject go = new GameObject("sfx " + SoundKey);
        go.transform.position = at;
        if (follow != null && follow.GetComponent<Rigidbody>() != null) go.transform.SetParent(follow.transform, true);
        AudioSource s = go.AddComponent<AudioSource>();
        s.playOnAwake = false;
        s.clip = clip;
        s.volume = volume;
        s.spatialBlend = 1f;
        s.dopplerLevel = 0f;
        s.minDistance = 3f;
        s.maxDistance = 35f;
        s.priority = 40;
        s.Play();
        Destroy(go, clip.length + 0.2f);
    }
}

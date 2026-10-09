using NightDuty;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 플레이어 몸(61차, 2026-10-07 플레이테스트). 근무 씬의 플레이어 루트(<see cref="FPController"/>)에 자동으로 붙는다.
/// 규칙 값은 코어 <see cref="PlayerBodyRules"/>. <b>FPController(김진선님 폴더)는 고치지 않는다</b> — 그 이동 뒤에서 결과만 바로잡는다.
/// <list type="bullet">
/// <item><b>키 2.2m</b>: 캡슐 높이를 늘리고(발 위치 그대로) 카메라를 같은 만큼 올린다. 프리팹·씬 파일은 그대로다.</item>
/// <item><b>벽 뚫림</b>: FPController는 <c>transform.Translate</c>로 움직여 물리를 거치지 않는다 — 벽에 비비며 뛰면 한 프레임에
/// 벽 두께 절반을 넘어 반대편으로 밀려 나갔다. 이 프레임 이동(Update 앞뒤 위치 차)을 캡슐로 쓸어 보고, 막히면 벽면을 따라 미끄러지게 한다.
/// 플레이어 캡슐에는 마찰 0 재질을 씌워 벽에 달라붙지 않게 한다.</item>
/// <item><b>점프 금지</b>: 위로 솟는 속도를 묶고, 수평 속도는 지운다(이동은 전부 Translate라 물리 속도가 쌓이면 미끄러짐만 남는다).</item>
/// <item>FPController가 꺼져 있으면(CCTV·컷신·사망 연출) 아무것도 하지 않는다. 한 프레임 0.75m 넘는 이동은 순간이동으로 보고 두지 않는다.</item>
/// </list>
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-900)]
public sealed class PlayerBody : MonoBehaviour
{
    private FPController _fp;
    private Rigidbody _rb;
    private CapsuleCollider _cap;
    private Vector3 _before;
    private bool _armed;
    private readonly RaycastHit[] _hits = new RaycastHit[24];

    /// <summary>지금 근무 씬의 것. 없으면 null.</summary>
    public static PlayerBody Active { get; private set; }

    /// <summary>벽에 막혀 이동을 바로잡은 횟수(검수용).</summary>
    public int Blocks { get; private set; }

    /// <summary>위로 튀는 속도를 깎은 횟수(검수용).</summary>
    public int RiseClamps { get; private set; }

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
        if (!FlowAutoInstall.IsDutyScene(scene) || FlowAutoInstall.Exists<PlayerBody>(scene)) return;
        foreach (FPController c in FindObjectsByType<FPController>(FindObjectsSortMode.None))
        {
            if (c.gameObject.scene == scene)
            {
                c.gameObject.AddComponent<PlayerBody>();
                return;
            }
        }
    }

    private void Awake()
    {
        _fp = GetComponent<FPController>();
        _rb = GetComponent<Rigidbody>();
        _cap = GetComponent<CapsuleCollider>();
        ApplyHeight();

        if (_cap != null)
        {
            PhysicsMaterial slick = new PhysicsMaterial("player slick (61차)");
            slick.dynamicFriction = 0f;
            slick.staticFriction = 0f;
            slick.frictionCombine = PhysicsMaterialCombine.Minimum;
            slick.bounciness = 0f;
            slick.bounceCombine = PhysicsMaterialCombine.Minimum;
            _cap.sharedMaterial = slick;
        }
    }

    private void OnEnable()
    {
        Active = this;
    }

    private void OnDisable()
    {
        if (Active == this) Active = null;
    }

    /// <summary>캡슐을 <see cref="PlayerBodyRules.Height"/>로 늘리고(발 위치 그대로) 카메라를 같은 만큼 올린다. 한 번만.</summary>
    private void ApplyHeight()
    {
        if (_cap == null) return;
        float grow = PlayerBodyRules.GrowFrom(_cap.height);
        if (grow <= 0f) return;
        _cap.height += grow;
        _cap.center += Vector3.up * (grow * 0.5f);

        Camera cam = GetComponentInChildren<Camera>(true);
        if (cam != null && cam.transform.parent == transform)
        {
            cam.transform.localPosition += Vector3.up * grow;
        }

        Debug.Log("[PlayerBody] 키 " + (_cap.height - grow).ToString("F2") + " → " + _cap.height.ToString("F2") + "m (눈 +" + grow.ToString("F2") + "m)");
    }

    private void Update()
    {
        _before = transform.position;
        _armed = true;
    }

    private void LateUpdate()
    {
        if (!_armed) return;
        _armed = false;
        if (_fp == null || !_fp.enabled || _cap == null || Time.deltaTime <= 0f) return;

        Vector3 after = transform.position;
        Vector3 delta = after - _before;
        delta.y = 0f;
        if (delta.sqrMagnitude < 1e-8f || PlayerBodyRules.IsTeleport(delta)) return;

        Vector3 resolved = Sweep(_before, delta);
        resolved.y = after.y;
        if ((resolved - after).sqrMagnitude > 1e-8f)
        {
            transform.position = resolved;
            Blocks++;
        }
    }

    private void FixedUpdate()
    {
        if (_rb == null || _rb.isKinematic || _fp == null || !_fp.enabled) return;
        Vector3 v = _rb.linearVelocity;
        float rise = PlayerBodyRules.ClampRise(v.y);
        if (rise < v.y) RiseClamps++;
        _rb.linearVelocity = new Vector3(0f, rise, 0f);
    }

    /// <summary>시작 위치에서 수평 이동을 캡슐로 쓸어 보고, 막히면 벽면을 따라 미끄러진 끝 위치(최대 3번 꺾음).</summary>
    private Vector3 Sweep(Vector3 start, Vector3 move)
    {
        Vector3 pos = start;
        for (int i = 0; i < 3; i++)
        {
            float dist = move.magnitude;
            if (dist < 1e-5f) break;
            Vector3 dir = move / dist;
            RaycastHit hit;
            if (!Cast(pos, dir, dist + PlayerBodyRules.Skin, out hit))
            {
                pos += move;
                break;
            }

            float travel = Mathf.Max(0f, hit.distance - PlayerBodyRules.Skin);
            pos += dir * travel;
            move = PlayerBodyRules.SlideAlong(dir * (dist - travel), hit.normal);
        }

        return pos;
    }

    /// <summary>그 자리의 몸 캡슐(턱 높이 위만)을 쏜다. 나 자신·트리거·처음부터 겹친 것은 뺀다.</summary>
    private bool Cast(Vector3 rootPos, Vector3 dir, float dist, out RaycastHit best)
    {
        best = default;
        float scale = Mathf.Max(transform.lossyScale.x, transform.lossyScale.z);
        float radius = Mathf.Max(0.05f, _cap.radius * scale - 0.01f);
        float half = _cap.height * 0.5f * transform.lossyScale.y;
        Vector3 center = rootPos + transform.rotation * Vector3.Scale(_cap.center, transform.lossyScale);
        Vector3 top = center + Vector3.up * (half - radius);
        Vector3 bottom = center - Vector3.up * (half - radius) + Vector3.up * PlayerBodyRules.StepHeight;
        if (bottom.y > top.y) bottom = top;

        int n = Physics.CapsuleCastNonAlloc(bottom, top, radius, dir, _hits, dist, ~0, QueryTriggerInteraction.Ignore);
        float nearest = float.MaxValue;
        bool found = false;
        for (int i = 0; i < n; i++)
        {
            RaycastHit h = _hits[i];
            if (h.collider == null || h.distance <= 0f) continue;
            if (h.collider.attachedRigidbody == _rb || h.collider.transform.IsChildOf(transform)) continue;
            if (h.distance < nearest)
            {
                nearest = h.distance;
                best = h;
                found = true;
            }
        }

        return found;
    }
}

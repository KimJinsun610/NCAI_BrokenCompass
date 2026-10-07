using NightDuty;
using UnityEngine;

/// <summary>
/// 멀리서 잠깐 보이는 몹(57차, 민: 「3일차까지 놀람 요소가 별로 없었다 — 몹을 살짝씩 멀리서 등장시킨다던가」).
/// 가짜 놀람 <see cref="TensionDirector.FakeGlimpse"/>(2일차부터, 가짜 놀람 상한·간격 안)가 부른다. 수칙·판정·소리 없음.
/// <list type="bullet">
/// <item><b>자리</b>: 플레이어 시선에서 7~25° 비낀 곳(정면이 아니라 곁눈), 12~22m. 그 자리 바닥이 플레이어 발 높이와 같고, 눈에서 몸까지 가리는 것이 없어야 한다.
/// 40번 찾아 없으면 아무것도 하지 않는다.</item>
/// <item><b>사라짐</b>: 똑바로 바라보면(4° 안 0.3초) · 8m 안으로 다가오면 · 2.5~4초가 지나면. 화면 밖이면 그 자리에서 사라지고,
/// 화면 안이면(59차, 민: 「사라질 때에도 사각지대로 뛰어가는 애니메이션 — 사각지대로 가면 사라지게」) 눈에서 가려지는 가장 가까운 자리(모퉁이·문간·기둥 뒤, 1.5~7m)를 찾아
/// 기괴하게 빠른 걸음(<see cref="StandInExit.RunTo"/>, 4.6m/s)으로 달려가 가려지는 순간 사라진다. 그런 자리가 없으면 플레이어 반대쪽으로 달아나다 화면 밖·가려짐에서 사라진다.</item>
/// <item><b>모습</b>: 2일차 소년·여자아이(선 채 숨쉬기), 3일차부터 노란 얼굴·검은 남자도. 플레이어를 바라보고 선다. 뒤 1m에 약한 차가운 빛(실루엣) — 몹과 함께 사라진다.</item>
/// </list>
/// </summary>
public sealed class DistantGlimpse : MonoBehaviour
{
    /// <summary>가장 가까운·먼 거리(m).</summary>
    public const float MinDistance = 12f;
    public const float MaxDistance = 22f;

    /// <summary>「똑바로 봄」 — 시선과 이 각도 안으로 이만큼 이어 보면 깜빡 사라진다.</summary>
    public const float StareDegrees = 4f;
    public const float StareSeconds = 0.3f;

    /// <summary>몹 뒤 실루엣 빛의 세기(57차).</summary>
    public const float BacklightIntensity = 1.1f;

    // 58차: 선 채 숨쉬는 새 소년·소녀(옛 mob.boy는 앉은 자세라 복도에 앉은 채 떠 있었다), 3일차부터 노란 얼굴(새 business duck)·검은 남자도.
    private static readonly string[] Early = { "mob.boy.stand", "mob.girl.stand" };
    private static readonly string[] Later = { "mob.boy.stand", "mob.girl.stand", "mob.duck", "mob.blackman.glimpse" };   // 59차: 달리기가 있는 검은 남자

    private static DistantGlimpse s_active;

    private GameObject _mob;
    private Vector3 _aim;
    private float _born;
    private float _life;
    private float _stare;
    private bool _seen;
    private bool _fleeing;
    private float _fleeAt;
    private Light _backlight;
    private Vector3 _lightOffset;

    /// <summary>사각지대를 찾는 거리(m).</summary>
    public const float BlindMin = 1.5f;
    public const float BlindMax = 7f;

    /// <summary>달아나는 최대 시간(초) — 그 뒤에는 어디서든 사라진다.</summary>
    public const float FleeMaxSeconds = 3f;

    /// <summary>지금 달아나는 중인지. 시험용.</summary>
    public static bool Fleeing
    {
        get { return s_active != null && s_active._fleeing; }
    }

    /// <summary>마지막으로 고른 사각지대(없으면 0). 시험용.</summary>
    public static Vector3 LastBlindSpot { get; private set; }

    /// <summary>지금 보이는 중인지. 시험용.</summary>
    public static bool Showing
    {
        get { return s_active != null; }
    }

    /// <summary>마지막으로 세운 자리(없으면 0). 시험용.</summary>
    public static Vector3 LastPoint { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        s_active = null;
        LastPoint = Vector3.zero;
        LastBlindSpot = Vector3.zero;
    }

    /// <summary>멀리 몹 하나를 세운다. 이미 보이는 중이거나 자리가 없으면 false.</summary>
    public static bool Play()
    {
        if (s_active != null || !NightRun.IsNightActive || NightRun.IsCaptured) return false;
        Camera cam = Camera.main;
        if (cam == null) return false;

        Vector3 at;
        if (!FindSpot(cam, out at)) return false;
        string[] pool = NightRun.Day >= 3 ? Later : Early;
        string id = pool[Random.Range(0, pool.Length)];
        GameObject mob = StandInFactory.Create(id, at, cam.transform.position, string.Empty);
        if (mob == null) return false;
        foreach (Collider c in mob.GetComponentsInChildren<Collider>()) c.enabled = false;   // 길을 막거나 조준을 가리지 않게

        GameObject host = new GameObject("멀리 보이는 몹 " + id);
        DistantGlimpse g = host.AddComponent<DistantGlimpse>();

        // 57차 플레이 점검: 어두운 복도 15m 밖의 몹은 손전등을 켜도 보이지 않았다 — 몹 뒤 1m에 약한 차가운 빛을 둬 실루엣으로 읽히게(앞은 어둡게).
        Vector3 away = at - cam.transform.position;
        away.y = 0f;
        if (away.sqrMagnitude > 0.01f) away.Normalize();
        GameObject back = new GameObject("뒤 빛");
        back.transform.SetParent(host.transform, false);
        back.transform.position = at + away * 1f + Vector3.up * 1.5f;
        Light light = back.AddComponent<Light>();
        light.type = LightType.Point;
        light.range = 3.2f;
        light.intensity = BacklightIntensity;
        light.color = new Color(0.6f, 0.7f, 0.85f);
        light.shadows = LightShadows.None;
        g._mob = mob;
        g._backlight = light;
        g._lightOffset = back.transform.position - at;
        g._aim = at + Vector3.up * 1.2f;
        g._born = Time.time;
        g._life = Random.Range(2.5f, 4f);
        s_active = g;
        LastPoint = at;
        if (DirectionStage.Verbose) Debug.Log("[Glimpse] " + id + " @" + at.ToString("F1") + " (" + Vector3.Distance(cam.transform.position, at).ToString("F1") + "m)");
        return true;
    }

    private static bool FindSpot(Camera cam, out Vector3 at)
    {
        at = Vector3.zero;
        Vector3 eye = cam.transform.position;
        Vector3 fwd = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up);
        if (fwd.sqrMagnitude < 0.01f) return false;
        fwd.Normalize();
        Transform player = PlayerSensors.Active != null ? PlayerSensors.Active.PlayerRoot : null;
        float feet = player != null ? DirectionStage.PlayerGroundY() : eye.y - 1.4f;   // 57차 플레이 점검: 루트는 캡슐 가운데(+0.85m)

        for (int i = 0; i < 40; i++)
        {
            float side = Random.value < 0.5f ? -1f : 1f;
            // 57차 플레이 점검: 12~35°는 폭 3m 복도에서 늘 벽 속이었다 — 7~25°(13m에서 1.6~6m 옆). 「똑바로 봄」(4°)보다는 늘 바깥.
            Vector3 dir = Quaternion.Euler(0f, side * Random.Range(7f, 25f), 0f) * fwd;
            float dist = Random.Range(MinDistance, MaxDistance);
            Vector3 probe = new Vector3(eye.x, eye.y, eye.z) + dir * dist;

            RaycastHit floor;
            if (!Physics.Raycast(probe, Vector3.down, out floor, 3.5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
            if (floor.normal.y < 0.7f || Mathf.Abs(floor.point.y - feet) > 0.4f) continue;
            Vector3 body = floor.point + Vector3.up * 1.2f;
            Vector3 head = floor.point + Vector3.up * 1.6f;
            if (Blocked(eye, body, player) || Blocked(eye, head, player)) continue;

            // 몸이 들어갈 틈(벽에 박히지 않게).
            if (Physics.CheckCapsule(floor.point + Vector3.up * 0.45f, floor.point + Vector3.up * 1.5f, 0.3f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;

            Vector3 vp = cam.WorldToViewportPoint(body);
            if (vp.z <= 0f || vp.x < 0.08f || vp.x > 0.92f || vp.y < 0.1f || vp.y > 0.9f) continue;
            at = floor.point;
            return true;
        }

        return false;
    }

    private static bool Blocked(Vector3 from, Vector3 to, Transform player)
    {
        RaycastHit hit;
        if (!Physics.Linecast(from, to, out hit, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return false;
        return player == null || !hit.collider.transform.IsChildOf(player);
    }

    private void Update()
    {
        Camera cam = Camera.main;
        if (_mob == null || cam == null || !NightRun.IsNightActive || NightRun.IsCaptured)
        {
            Vanish();
            return;
        }

        Vector3 to = _aim - cam.transform.position;
        float angle = Vector3.Angle(cam.transform.forward, to);
        Vector3 vp = cam.WorldToViewportPoint(_aim);
        bool onScreen = vp.z > 0f && vp.x > -0.05f && vp.x < 1.05f && vp.y > -0.05f && vp.y < 1.05f;

        _stare = angle < StareDegrees ? _stare + Time.deltaTime : 0f;
        if (_stare >= StareSeconds) _seen = true;

        if (_fleeing)
        {
            // 뒤 빛은 몹을 따라간다(실루엣 유지). 달리기는 StandInExit가 하고, 가려지면 몹을 지운다 → 위에서 Vanish.
            if (_backlight != null) _backlight.transform.position = _mob.transform.position + _lightOffset;
            if (Time.time - _fleeAt > FleeMaxSeconds) Vanish();
            return;
        }

        bool tooClose = to.magnitude < 8f;
        bool expired = Time.time - _born >= _life;
        bool leave = _stare >= StareSeconds || _seen || expired || tooClose || Time.time - _born > 12f;
        if (!leave) return;

        // 화면 밖이면 조용히, 화면 안이면 사각지대로 달려간다(59차) — 눈앞에서 꺼지지 않는다.
        if (!onScreen) Vanish();
        else Flee(cam);
    }

    private void Flee(Camera cam)
    {
        _fleeing = true;
        _fleeAt = Time.time;
        Vector3 eye = cam.transform.position;
        Vector3 from = _mob.transform.position;
        Transform player = PlayerSensors.Active != null ? PlayerSensors.Active.PlayerRoot : null;

        Vector3 spot;
        bool found = FindBlindSpot(from, eye, player, out spot);
        if (!found)
        {
            // 가려지는 자리가 없다 — 플레이어 반대쪽으로 트인 만큼 달아난다(화면 밖이 되거나 가려지면 사라진다).
            Vector3 away = from - eye;
            away.y = 0f;
            away = away.sqrMagnitude > 0.01f ? away.normalized : _mob.transform.forward;
            spot = from + away * Mathf.Max(1f, Clear(from, away, BlindMax) - 0.4f);
        }

        LastBlindSpot = spot;
        StandInExit exit = _mob.GetComponent<StandInExit>();
        if (exit == null) exit = _mob.AddComponent<StandInExit>();   // 달리기 동작이 없는 대역 — 미끄러지듯
        GameObject mob = _mob;
        exit.RunTo(spot, FleeMaxSeconds, () => HiddenFrom(mob, player));
        if (DirectionStage.Verbose) Debug.Log("[Glimpse] 사각지대로 " + (found ? "" : "(없음 — 반대쪽) ") + spot.ToString("F1") + " " + Vector3.Distance(from, spot).ToString("F1") + "m");
    }

    /// <summary>
    /// 플레이어 눈에서 몸(1.2m)·머리(1.6m)가 모두 가려지는 바닥 점 중 가장 가까운 것(<see cref="BlindMin"/>~<see cref="BlindMax"/>).
    /// 그 자리까지 허리 높이로 막힘 없이 달려갈 수 있고, 플레이어 쪽으로 1m 넘게 다가가지 않아야 한다.
    /// </summary>
    public static bool FindBlindSpot(Vector3 from, Vector3 eye, Transform player, out Vector3 spot)
    {
        spot = Vector3.zero;
        float best = float.MaxValue;
        float baseDist = FlatDistance(from, eye);
        float start = Random.Range(0f, 360f);
        for (float dist = BlindMin; dist <= BlindMax + 0.01f; dist += 0.75f)
        {
            for (int k = 0; k < 24; k++)
            {
                Vector3 dir = Quaternion.Euler(0f, start + k * 15f, 0f) * Vector3.forward;
                Vector3 p = from + dir * dist;
                RaycastHit floor;
                if (!Physics.Raycast(p + Vector3.up * 1.5f, Vector3.down, out floor, 3f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
                if (floor.normal.y < 0.7f || Mathf.Abs(floor.point.y - from.y) > 0.4f) continue;
                if (FlatDistance(floor.point, eye) < baseDist - 1f) continue;
                if (Clear(from, dir, dist) < dist) continue;
                if (Physics.CheckCapsule(floor.point + Vector3.up * 0.45f, floor.point + Vector3.up * 1.5f, 0.3f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
                if (!Blocked(eye, floor.point + Vector3.up * 1.2f, player) || !Blocked(eye, floor.point + Vector3.up * 1.6f, player)) continue;
                if (dist < best)
                {
                    best = dist;
                    spot = floor.point;
                }
            }

            if (best < float.MaxValue) return true;   // 가장 가까운 고리에서 찾았으면 그만
        }

        return false;
    }

    private static float Clear(Vector3 from, Vector3 dir, float max)
    {
        Vector3 waist = from + Vector3.up * 0.9f;
        float dist = max;
        foreach (RaycastHit h in Physics.SphereCastAll(waist, 0.25f, dir, max, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (h.collider.GetComponentInParent<CharacterController>() != null) continue;
            if (h.distance <= 0f) continue;
            dist = Mathf.Min(dist, h.distance);
        }

        return dist;
    }

    private static float FlatDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    private static bool HiddenFrom(GameObject mob, Transform player)
    {
        Camera cam = Camera.main;
        if (mob == null || cam == null) return true;
        Vector3 eye = cam.transform.position;
        Vector3 p = mob.transform.position;
        return Blocked(eye, p + Vector3.up * 1.2f, player) && Blocked(eye, p + Vector3.up * 1.6f, player);
    }

    private void Vanish()
    {
        if (_mob != null) Destroy(_mob);
        _mob = null;
        if (s_active == this) s_active = null;
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (_mob != null) Destroy(_mob);
        if (s_active == this) s_active = null;
    }
}

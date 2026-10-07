using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 배치 사망 컷신 <b>ver2 — 피 비</b>. <c>bloodRain</c>을 켠 프리팹(<see cref="ResourceNameV2"/>)에서만 돈다.
///
/// <para><b>흐름</b> — <see cref="PlayBloodRain"/>
/// ① 눈앞 천장에서 핏방울이 3~4개 떨어진다 — 이때까지는 조작할 수 있다
/// ② 조작 잠금 · 다른 소리 끊김 → 천천히 천장을 올려다보고 피 얼룩을 발견한다(얼룩이 번진다)
/// ③ 고개를 내리는 동안 — 여기저기서 비처럼 피가 쏟아지고, 복도·방에 사람 나무가 가득 서 있다.
///    화면이 일렁이고 가장자리가 어두워지며, 핏방울·물소리·웅성거림이 차오른다
/// ④ 가까운 사람 나무로 비틀비틀 걸어간다 — 여기부터는 ver1과 같다(걷기 → 끌려 들어감 → 먹물)</para>
///
/// <para>핏방울은 <c>HorrorProp_CeilingBloodDrip</c>의 방울을 빌려 쓴다 — 바닥에 닿으면 튐·젖은 자국은 그 프리팹이 낸다.
/// 나무 자리는 그 순간 플레이어 둘레를 즉석 NavMesh로 구워 고르므로 <see cref="LayoutDeathSpot"/>이 필요 없다.</para>
/// </summary>
public partial class LayoutDeathCutscene
{
    public const string ResourceNameV2 = "DeathCutscene_Layout2";
    /// <summary>ver2와 같고 나무만 사람 나무(HumanTree) — ver2 프리팹의 배리언트(F4).</summary>
    public const string ResourceNameV2HumanTree = "DeathCutscene_Layout_ver2_HumanTree";

    /// <summary>방울이 나오는 높이 — 천장 아래(m). 키운 방울의 충돌 반지름보다 커야 한다.</summary>
    private const float DripBelowCeiling = 0.1f;

    /// <summary>③부터 차오르는 소리 한 겹(물소리·웅성거림 등). 끌려 드는 순간 끊긴다.</summary>
    [Serializable]
    public class RainSound
    {
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 0.6f;
        [Tooltip("고개를 내리기 시작해서 최대 볼륨까지(초)")]
        [Min(0f)] public float fadeIn = 2f;
        [Tooltip("0 = 화면 소리(2D). 1 이상 = 그 수만큼 사람 나무에 나눠 단 3D 소리 — 웅성거림이 여기저기서 들린다(시작 지점은 겹마다 다르게).")]
        [Range(0, 8)] public int atTrees;
    }

    [Header("ver2 — 피 비 (끄면 ver1)")]
    [Tooltip("켜면 ver2 흐름(눈앞 핏방울 → 천장 → 피 비·사람 나무 → 걸어감). 사람 나무 자리(LayoutDeathSpot)는 쓰지 않는다.")]
    [SerializeField] private bool bloodRain;
    [Tooltip("천장 핏방울 프리팹(HorrorProp_CeilingBloodDrip) — 얼룩·방울(바닥에 튐·젖은 자국)을 여기서 빌린다.")]
    [SerializeField] private GameObject bloodDripPrefab;

    [Header("ver2 ① 눈앞 핏방울 (아직 조작 가능)")]
    [Tooltip("눈앞 이만큼(수평 m) 위 천장에서 떨어진다. 앞이 벽이면 벽 앞까지.")]
    [SerializeField, Min(0.5f)] private float firstDropDistance = 1.4f;
    [SerializeField, Range(1, 8)] private int firstDropsMin = 3;
    [SerializeField, Range(1, 8)] private int firstDropsMax = 4;
    [Tooltip("재생 뒤 첫 방울까지(초)")]
    [SerializeField, Min(0f)] private float firstDropDelay = 0.4f;
    [Tooltip("방울 사이 간격(초) — 최소~최대에서 무작위")]
    [SerializeField] private Vector2 firstDropInterval = new Vector2(0.8f, 1.15f);
    [Tooltip("마지막 방울이 바닥에 닿고 조작이 잠기기까지(초)")]
    [SerializeField, Min(0f)] private float lockAfterLastDrop = 0.5f;
    [Tooltip("방울이 바닥에 닿는 소리(무작위 하나, 그 자리 3D)")]
    [SerializeField] private AudioClip[] dropClips = new AudioClip[0];
    [SerializeField, Range(0f, 1f)] private float dropVolume = 0.85f;
    [Tooltip("방울 크기 배수 — 프리팹의 2~3cm 방울은 조금만 떨어져도 안 보인다(비처럼 보이게 키운다)")]
    [SerializeField, Range(1f, 4f)] private float dropSizeScale = 2f;

    [Header("ver2 ② 천장을 올려다봄")]
    [SerializeField, Min(0.3f)] private float lookUpSeconds = 2.6f;
    [Tooltip("얼룩을 바라보는 시간(초) — 그동안 방울이 계속 떨어진다. 얼룩 크기는 그대로(2026-10-05 사용자: 커짐 제거)")]
    [SerializeField, Min(0f)] private float stainHoldSeconds = 2.8f;
    [Tooltip("올려다보는 최대 각(° — 음수가 위)")]
    [SerializeField, Range(-89f, -20f)] private float maxLookUpPitch = -75f;
    [Tooltip("올려다볼 때 고개가 떨리는 정도(°)")]
    [SerializeField, Range(0f, 3f)] private float lookTremble = 0.6f;

    [Header("ver2 ③ 고개를 내리며 — 피 비 · 사람 나무")]
    [SerializeField, Min(0.3f)] private float lookDownSeconds = 1.6f;
    [Tooltip("다 내린 뒤 걷기 시작까지 바라보는 시간(초)")]
    [SerializeField, Min(0f)] private float revealHoldSeconds = 2.0f;
    [Tooltip("피가 떨어지는 천장 자리 수")]
    [SerializeField, Range(0, 80)] private int rainPoints = 40;
    [SerializeField, Min(2f)] private float rainRadius = 10f;
    [Tooltip("고개를 내리기 시작할 때 초당 방울 수")]
    [SerializeField, Min(0f)] private float rainRateStart = 4f;
    [Tooltip("다 차올랐을 때 초당 방울 수")]
    [SerializeField, Min(0f)] private float rainRateFull = 26f;
    [SerializeField, Min(0.1f)] private float rainRampSeconds = 5f;
    [Tooltip("피 자리마다 천장 얼룩을 둘 확률")]
    [SerializeField, Range(0f, 1f)] private float rainStainChance = 0.6f;
    [Tooltip("얼룩이 투명 → 불투명으로 나타나는 시간(초). 얼룩마다 처음 비가 떨어질 때부터 나타나기 시작한다.")]
    [SerializeField, Min(0.05f)] private float rainStainFadeSeconds = 1.5f;
    [Tooltip("피 방울마다 닿는 소리를 낼 확률(전부 내면 소리가 뭉개진다)")]
    [SerializeField, Range(0f, 1f)] private float rainSoundChance = 0.4f;
    [Tooltip("걸어갈 나무 말고 둘레에 더 세우는 사람 나무 수")]
    [SerializeField, Range(0, 40)] private int crowdTrees = 14;
    [SerializeField, Min(3f)] private float crowdRadius = 12f;
    [Tooltip("나무끼리 최소 간격(수평 m)")]
    [SerializeField, Min(1f)] private float crowdSpacing = 2.2f;
    [Tooltip("플레이어에게서 최소 거리(수평 m)")]
    [SerializeField, Min(1f)] private float crowdMinDistance = 2.6f;
    [Tooltip("걸어갈 나무까지 길이(m) — 이 범위에서 가장 가까운 나무, 앞쪽을 먼저")]
    [SerializeField] private Vector2 targetPathLength = new Vector2(6f, 11f);
    [Tooltip("걸어갈 길에서 다른 나무가 떨어져 있어야 하는 거리(m) — 걸어가며 나무를 뚫지 않게")]
    [SerializeField, Min(0f)] private float pathClearance = 1.2f;
    [Tooltip("고개를 내린 뒤 화면 일렁임 — 걸으며 ver1 값(wobbleMax)까지 더 오른다")]
    [SerializeField, Range(0f, 1f)] private float revealWobble = 0.8f;
    [Tooltip("고개를 내린 뒤 가장자리 어둠")]
    [SerializeField, Range(0f, 1f)] private float revealVignette = 0.4f;
    [Tooltip("일렁임·어둠이 위 값까지 차오르는 시간(초)")]
    [SerializeField, Min(0.1f)] private float revealFadeSeconds = 2.5f;
    [Tooltip("③부터 차오르는 소리 — 핏방울 고이는 소리·물소리·웅성거림")]
    [SerializeField] private RainSound[] rainSounds = new RainSound[0];

    // ── ver2 상태 ──
    private float baseWobble;     // 걷기 중에도 이 아래로 내려가지 않는다(ver1은 0)
    private float baseVignette;
    private GameObject firstProp;
    private ParticleSystem[] firstDrips = new ParticleSystem[0];
    private DecalProjector firstStain;
    private Vector3 firstStainSize;
    private Vector3 firstCeiling;
    private float firstFloorY;
    private float dropGravity = 9.81f * 0.4f;
    private readonly List<GameObject> rainObjects = new List<GameObject>();
    private readonly List<ParticleSystem> rainEmitters = new List<ParticleSystem>();
    private readonly List<float> rainFloorY = new List<float>();
    private readonly List<float> rainActiveAt = new List<float>();             // 피가 떨어지기 시작하는 때(③ 시작부터 초)
    private readonly List<DecalProjector> rainStains = new List<DecalProjector>(); // 그 자리 천장 얼룩(없으면 null)
    private readonly List<int> rainActive = new List<int>();
    private readonly List<GameObject> crowd = new List<GameObject>();
    private readonly List<AudioSource> rainSources = new List<AudioSource>();
    private readonly List<RainSound> rainSourceOf = new List<RainSound>();
    private readonly List<AudioSource> dropPool = new List<AudioSource>();
    private int dropNext;
    private readonly List<KeyValuePair<float, Vector3>> pendingDrops = new List<KeyValuePair<float, Vector3>>();
    private float rainStartTime = -1f;
    private float rainAcc;
    private Coroutine bloodTick;

    // ─────────────────────────────── 재생 ───────────────────────────────

    private bool PlayBloodRain(bool restoreAfter)
    {
        FPController fp = FindAnyObjectByType<FPController>();
        if (fp == null) return Refuse("플레이어(FPController)가 없습니다.");
        Camera c = fp.GetComponentInChildren<Camera>();
        if (c == null) return Refuse("플레이어 카메라가 없습니다.");
        if (bloodDripPrefab == null) return Refuse("천장 핏방울 프리팹(bloodDripPrefab)이 비어 있습니다.");

        player = fp.transform;
        cam = c;
        // 지난 재생의 흔적 — ①에서 멈추면(아직 잠그지 않음) 되돌릴 것이 없어야 한다
        controller = null;
        body = null;
        viewmodel = null;
        cursorWasLock = Cursor.lockState;
        restoreOnEnd = restoreAfter;
        RememberPose();
        baseWobble = 0f;
        baseVignette = 0f;

        if (forceFlashlight) GrabFlashlight();   // 어두운 복도에서도 눈앞 핏방울이 보이게
        BuildRuntimeParts();
        PlaceFirstDrop();

        Playing = this;
        startedFrame = Time.frameCount;
        gameObject.SetActive(true);
        bloodTick = StartCoroutine(BloodTick());
        running = StartCoroutine(RunBloodRain(fp));
        return true;
    }

    /// <summary>눈앞 천장에 핏방울 프리팹을 하나 붙인다 — 저절로 떨어지지 않게 하고(방울은 컷신이 떨어뜨린다) 고리 소리도 끈다.</summary>
    private void PlaceFirstDrop()
    {
        Vector3 eye = cam.transform.position;
        Vector3 fwd = Flat(cam.transform.forward);
        if (fwd.sqrMagnitude < 0.01f) fwd = Flat(player.forward);
        fwd.Normalize();
        float d = firstDropDistance;
        RaycastHit hit;
        if (Physics.Raycast(eye, fwd, out hit, d + 0.5f, ~0, QueryTriggerInteraction.Ignore) && !hit.transform.IsChildOf(player))
        {
            d = Mathf.Max(0.6f, hit.distance - 0.5f);
        }
        CeilingAt(eye + fwd * d, out firstCeiling, out firstFloorY);

        firstProp = Instantiate(bloodDripPrefab, firstCeiling, Quaternion.AngleAxis(UnityEngine.Random.Range(0f, 360f), Vector3.up));
        firstProp.name = "LayoutDeath BloodDrip";
        foreach (AudioSource a in firstProp.GetComponentsInChildren<AudioSource>(true))
        {
            a.Stop();
            a.enabled = false;
        }
        firstDrips = DripsOf(firstProp);
        foreach (ParticleSystem ps in firstDrips)
        {
            ParticleSystem.EmissionModule em = ps.emission;
            em.enabled = false;
            // 프리팹은 천장 2cm 아래 — 방울을 키우면 충돌 반지름이 천장에 닿아 나오자마자 천장에서 터진다(2026-10-05 실측)
            Vector3 lp = ps.transform.localPosition;
            ps.transform.localPosition = new Vector3(lp.x, -DripBelowCeiling, lp.z);
        }
        if (firstDrips.Length > 0) dropGravity = Mathf.Max(0.1f, firstDrips[0].main.gravityModifier.constant * -Physics.gravity.y);

        firstStain = null;
        foreach (DecalProjector p in firstProp.GetComponentsInChildren<DecalProjector>(true))
        {
            if (p.transform.parent != firstProp.transform) continue;   // 방울 속 젖은 자국 말고 천장 얼룩
            firstStain = p;
            break;
        }
        if (firstStain != null) firstStainSize = firstStain.size;
    }

    /// <summary>프리팹 바로 밑의 방울(Drip_*) — 떨어지면 바닥에 튐·젖은 자국을 낸다.</summary>
    private static ParticleSystem[] DripsOf(GameObject root)
    {
        var list = new List<ParticleSystem>();
        foreach (ParticleSystem ps in root.GetComponentsInChildren<ParticleSystem>(true))
        {
            if (ps.transform.parent == root.transform) list.Add(ps);
        }
        return list.ToArray();
    }

    /// <summary><paramref name="at"/> 위 천장과 그 아래 바닥 높이. 천장이 없으면(바깥) 2.2m 위를 천장으로 친다.</summary>
    private void CeilingAt(Vector3 at, out Vector3 ceiling, out float floorY)
    {
        RaycastHit hit;
        ceiling = Physics.Raycast(at, Vector3.up, out hit, 8f, ~0, QueryTriggerInteraction.Ignore) ? hit.point : at + Vector3.up * 2.2f;
        floorY = Physics.Raycast(ceiling - Vector3.up * 0.05f, Vector3.down, out hit, 12f, ~0, QueryTriggerInteraction.Ignore) ? hit.point.y : at.y - 1.6f;
    }

    private IEnumerator RunBloodRain(FPController fp)
    {
        // ① 눈앞 핏방울 — 아직 조작할 수 있다
        Phase = "Prelude";
        yield return Wait(firstDropDelay);
        int count = UnityEngine.Random.Range(firstDropsMin, Mathf.Max(firstDropsMin, firstDropsMax) + 1);
        float lastImpact = Time.time;
        for (int i = 0; i < count; i++)
        {
            lastImpact = DropFirst(true);
            if (i < count - 1) yield return Wait(UnityEngine.Random.Range(firstDropInterval.x, Mathf.Max(firstDropInterval.x, firstDropInterval.y)));
        }
        while (Time.time < lastImpact + lockAfterLastDrop) yield return null;

        // ② 잠금 — 그 자리에서 천장을 올려다본다
        Phase = "LookUp";
        LockPlayer(fp);
        Vector3 feet = FindFeet(fp);
        List<Vector3> path, crowdSpots, rainSpots;
        Vector3 target;
        PlanReveal(feet, out target, out path, out crowdSpots, out rainSpots);

        Vector3 bodyPos = player.position;
        float yaw = cam.transform.eulerAngles.y;
        float pitch = NormPitch(cam.transform.eulerAngles.x);
        blendFromPos = cam.transform.position;
        blendFromRot = cam.transform.rotation;
        blendIn = 0f;
        camFovNow = cam.fieldOfView;
        camDriven = true;

        Vector3 eye = cam.transform.position;
        Vector3 toStain = firstCeiling - eye;
        float upYaw = Flat(toStain).magnitude > 0.3f ? Mathf.Atan2(toStain.x, toStain.z) * Mathf.Rad2Deg : yaw;
        float upPitch = Mathf.Max(maxLookUpPitch, -Mathf.Asin(Mathf.Clamp(toStain.normalized.y, -1f, 1f)) * Mathf.Rad2Deg);
        float y0 = yaw, p0 = pitch;
        float t = 0f;
        float nextDrop = Time.time + 0.9f;
        while (t < lookUpSeconds)
        {
            t += Time.deltaTime;
            float e = Mathf.SmoothStep(0f, 1f, t / lookUpSeconds);
            e = e * e * (3f - 2f * e);   // 두 번 감아 처음·끝이 더 느리다 — 「천천히」
            yaw = Mathf.LerpAngle(y0, upYaw, e);
            pitch = Mathf.Lerp(p0, upPitch, e);
            LookTremble(bodyPos, yaw, pitch, e);
            nextDrop = KeepDripping(nextDrop);
            yield return null;
        }

        // 피 얼룩을 바라본다(크기 그대로). 끝날 즈음(시선이 위에 있을 때) 아래에 나무를 세운다
        t = 0f;
        while (t < stainHoldSeconds)
        {
            t += Time.deltaTime;
            LookTremble(bodyPos, yaw, pitch, 1f);
            nextDrop = KeepDripping(nextDrop);
            yield return null;
        }
        SpawnCrowd(feet, target, crowdSpots);
        SpawnRain(rainSpots);

        // ③ 고개를 내리며 — 피 비·사람 나무·일렁임·소리가 한꺼번에 차오른다
        Phase = "LookDown";
        rainStartTime = Time.time;
        rainAcc = 0f;
        StartRainSounds(target);

        Vector3 treeLook = target + Vector3.up * lookHeight;
        Vector3 lookAt = HasLineOfSight(eye, treeLook) ? treeLook : LookAhead(path, feet, 1, 2f) + Vector3.up * (eye.y - feet.y - 0.15f);
        Vector3 toLook = lookAt - eye;
        float downYaw = Mathf.Atan2(toLook.x, toLook.z) * Mathf.Rad2Deg;
        float downPitch = -Mathf.Asin(Mathf.Clamp(toLook.normalized.y, -1f, 1f)) * Mathf.Rad2Deg;
        y0 = yaw;
        p0 = pitch;
        t = 0f;
        while (t < lookDownSeconds)
        {
            t += Time.deltaTime;
            float e = Mathf.SmoothStep(0f, 1f, t / lookDownSeconds);
            yaw = Mathf.LerpAngle(y0, downYaw, e);
            pitch = Mathf.Lerp(p0, downPitch, e);
            LookTremble(bodyPos, yaw, pitch, 1f);
            yield return null;
        }
        Phase = "Reveal";
        t = 0f;
        while (t < revealHoldSeconds)
        {
            t += Time.deltaTime;
            LookTremble(bodyPos, yaw, pitch, 1f);
            yield return null;
        }

        // ④ 가까운 나무로 — 여기부터 ver1과 같다(굳음 없이 이어서)
        yield return Run(path, feet, 0f, pitch);
    }

    private static IEnumerator Wait(float seconds)
    {
        float t = 0f;
        while (t < seconds)
        {
            t += Time.deltaTime;
            yield return null;
        }
    }

    private static float NormPitch(float x)
    {
        return x > 180f ? x - 360f : x;
    }

    /// <summary>고개를 미세하게 떨며 카메라를 둔다(겁에 질려 굳은 시선).</summary>
    private void LookTremble(Vector3 bodyPos, float yaw, float pitch, float amount)
    {
        float k = lookTremble * amount;
        float ty = (Mathf.PerlinNoise(Time.time * 2.3f, 0.17f) - 0.5f) * 2f * k;
        float tp = (Mathf.PerlinNoise(0.53f, Time.time * 2.9f) - 0.5f) * 2f * k;
        SetCamera(bodyPos, yaw, pitch, 0f, 0f, ty, tp);
    }

    /// <summary>얼룩에서 방울이 계속 떨어진다(약 0.9초마다). 다음 방울 시각을 돌려준다.</summary>
    private float KeepDripping(float nextDrop)
    {
        if (Time.time < nextDrop) return nextDrop;
        DropFirst(true);
        return Time.time + UnityEngine.Random.Range(0.7f, 1.1f);
    }

    /// <summary>눈앞 얼룩에서 한 방울. 바닥에 닿는 시각을 돌려준다.</summary>
    private float DropFirst(bool sound)
    {
        if (firstDrips.Length == 0) return Time.time;
        return EmitDrop(firstDrips[UnityEngine.Random.Range(0, firstDrips.Length)], firstFloorY, sound);
    }

    /// <summary>방울 하나를 떨어뜨리고 바닥에 닿는 순간 소리를 예약한다. 닿는 시각을 돌려준다.</summary>
    private float EmitDrop(ParticleSystem ps, float floorY, bool sound)
    {
        if (ps == null) return Time.time;
        ParticleSystem.MinMaxCurve size = ps.main.startSize;
        var p = new ParticleSystem.EmitParams { startSize = UnityEngine.Random.Range(size.constantMin, size.constantMax) * dropSizeScale };
        ps.Emit(p, 1);
        Vector3 from = ps.transform.position;
        float h = Mathf.Max(0.1f, from.y - floorY);
        float at = Time.time + Mathf.Sqrt(2f * h / dropGravity);
        if (sound) pendingDrops.Add(new KeyValuePair<float, Vector3>(at, new Vector3(from.x, floorY + 0.02f, from.z)));
        return at;
    }

    // ─────────────────────────────── 자리 고르기 ───────────────────────────────

    /// <summary>
    /// 플레이어 둘레를 즉석 NavMesh로 굽고 — 걸어갈 나무(앞쪽·가까운 것), 둘레 나무 자리, 피가 떨어질 자리를 고른다.
    /// 길을 못 찾으면 눈앞 3.5m(벽이면 그 앞)에 나무를 두고 곧장 걷는다.
    /// </summary>
    private void PlanReveal(Vector3 feet, out Vector3 target, out List<Vector3> path, out List<Vector3> crowdSpots, out List<Vector3> rainSpots)
    {
        crowdSpots = new List<Vector3>();
        rainSpots = new List<Vector3>();
        path = null;
        target = feet;
        Vector3 fwd = Flat(cam.transform.forward);
        if (fwd.sqrMagnitude < 0.01f) fwd = Flat(player.forward);
        fwd.Normalize();

        float reach = Mathf.Max(crowdRadius, rainRadius) + 3f;
        var bounds = new Bounds(feet, Vector3.zero);
        bounds.SetMinMax(new Vector3(feet.x - reach, feet.y - 1.5f, feet.z - reach), new Vector3(feet.x + reach, feet.y + 3.5f, feet.z + reach));
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var samples = new List<Vector3>();
        NavMeshHit from = default(NavMeshHit);
        bool ok = BuildNavMesh(bounds);
        var filter = new NavMeshQueryFilter { agentTypeID = agentTypeId, areaMask = NavMesh.AllAreas };
        if (ok) ok = NavMesh.SamplePosition(feet, out from, 1.5f, filter);
        if (ok)
        {
            for (int i = 0; i < 260; i++)
            {
                Vector2 r = UnityEngine.Random.insideUnitCircle * reach;
                NavMeshHit h;
                if (!NavMesh.SamplePosition(feet + new Vector3(r.x, 0f, r.y), out h, 1.2f, filter)) continue;
                if (Mathf.Abs(h.position.y - feet.y) > 1.2f) continue;   // 같은 층만
                samples.Add(h.position);
            }
        }

        // 걸어갈 나무 — 길이 범위 안에서 가장 가까운 것, 뒤쪽이면 벌점
        if (ok)
        {
            var near = new List<Vector3>();
            foreach (Vector3 s in samples)
            {
                float d = Flat(s - feet).magnitude;
                if (d >= crowdMinDistance && d <= targetPathLength.y) near.Add(s);
            }
            near.Sort((a, b) => Flat(a - feet).sqrMagnitude.CompareTo(Flat(b - feet).sqrMagnitude));
            var navPath = new NavMeshPath();
            float bestScore = float.MaxValue;
            for (int i = 0; i < near.Count && i < 40; i++)
            {
                if (!NavMesh.CalculatePath(from.position, near[i], filter, navPath) || navPath.status != NavMeshPathStatus.PathComplete) continue;
                float length = PathLength(navPath.corners);
                if (length > targetPathLength.y + 2f) continue;
                float angle = Vector3.Angle(fwd, Flat(near[i] - feet));
                float score = length + (length < targetPathLength.x ? (targetPathLength.x - length) * 3f : 0f) + (angle > 70f ? 6f : angle / 70f);
                if (score >= bestScore) continue;
                bestScore = score;
                target = near[i];
                path = new List<Vector3>(navPath.corners);
            }
        }
        if (path == null)
        {
            // 길 없음 — 눈앞에 곧장
            float d = 3.5f;
            RaycastHit hit;
            if (Physics.Raycast(feet + Vector3.up * 1f, fwd, out hit, d + 0.8f, ~0, QueryTriggerInteraction.Ignore) && !hit.transform.IsChildOf(player))
            {
                d = Mathf.Max(pullDistance + 0.3f, hit.distance - 0.8f);
            }
            target = feet + fwd * d;
            path = new List<Vector3> { feet, target };
            Debug.LogWarning("[LayoutDeathCutscene] ver2 — 걸어갈 길을 찾지 못해 눈앞 " + d.ToString("0.0") + "m에 나무를 둡니다.");
        }

        // 둘레 나무 — 앞쪽 먼저, 서로·플레이어·걸어갈 길에서 떨어지게
        Shuffle(samples);
        var placed = new List<Vector3> { target };
        for (int pass = 0; pass < 2; pass++)
        {
            foreach (Vector3 s in samples)
            {
                if (crowdSpots.Count >= crowdTrees) break;
                Vector3 flat = Flat(s - feet);
                float d = flat.magnitude;
                if (d < crowdMinDistance || d > crowdRadius) continue;
                bool front = Vector3.Angle(fwd, flat) < 65f;
                if (pass == 0 && !front) continue;
                if (pass == 0 && crowdSpots.Count >= crowdTrees * 2 / 3) break;   // 앞쪽은 2/3까지, 나머지는 둘레에
                if (TooClose(s, placed, crowdSpacing)) continue;
                if (DistanceToPath(s, path) < pathClearance) continue;
                placed.Add(s);
                crowdSpots.Add(s);
            }
        }

        // 피가 떨어질 자리 — 절반은 눈앞(2~7m), 나머지는 둘레
        for (int pass = 0; pass < 2; pass++)
        {
            foreach (Vector3 s in samples)
            {
                if (rainSpots.Count >= rainPoints) break;
                Vector3 flat = Flat(s - feet);
                float d = flat.magnitude;
                if (d < 0.8f || d > rainRadius) continue;
                bool front = d >= 2f && d <= 7f && Vector3.Angle(fwd, flat) < 50f;
                if (pass == 0 && (!front || rainSpots.Count >= rainPoints / 2)) continue;
                if (TooClose(s, rainSpots, 0.9f)) continue;
                rainSpots.Add(s);
            }
        }
        if (samples.Count == 0)
        {
            for (int i = 0; i < rainPoints; i++)
            {
                Vector2 r = UnityEngine.Random.insideUnitCircle * rainRadius;
                rainSpots.Add(feet + new Vector3(r.x, 0f, r.y));
            }
        }
        Debug.Log("[LayoutDeathCutscene] ver2 자리 " + watch.ElapsedMilliseconds + "ms · 표본 " + samples.Count + " · 걸어갈 길 " + PathLength(path).ToString("0.0") +
                  "m · 둘레 나무 " + crowdSpots.Count + " · 피 자리 " + rainSpots.Count);
    }

    private static bool TooClose(Vector3 p, List<Vector3> others, float min)
    {
        foreach (Vector3 o in others) if (Flat(o - p).sqrMagnitude < min * min) return true;
        return false;
    }

    /// <summary>점에서 꺾인 길까지 수평 거리.</summary>
    private static float DistanceToPath(Vector3 p, List<Vector3> path)
    {
        float best = float.MaxValue;
        for (int i = 1; i < path.Count; i++)
        {
            Vector3 a = Flat(path[i - 1]), b = Flat(path[i]), q = Flat(p);
            Vector3 ab = b - a;
            float k = ab.sqrMagnitude < 1e-4f ? 0f : Mathf.Clamp01(Vector3.Dot(q - a, ab) / ab.sqrMagnitude);
            best = Mathf.Min(best, Vector3.Distance(q, a + ab * k));
        }
        return best;
    }

    private static void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            T tmp = list[i];
            list[i] = list[j];
            list[j] = tmp;
        }
    }

    // ─────────────────────────────── 무대 ───────────────────────────────

    /// <summary>걸어갈 나무(<see cref="tree"/>)와 둘레 나무를 세운다 — 모두 플레이어를 본다.</summary>
    private void SpawnCrowd(Vector3 feet, Vector3 target, List<Vector3> spots)
    {
        treeBase = target;
        if (treePrefab == null)
        {
            Debug.LogWarning("[LayoutDeathCutscene] 사람 나무 프리팹이 비어 있습니다 — 나무 없이 진행합니다.");
            return;
        }
        tree = NewTree(target, FaceTo(target, feet, 0f), "LayoutDeath HumanTree", true);
        foreach (Vector3 s in spots)
        {
            crowd.Add(NewTree(s, FaceTo(s, feet, 25f), "LayoutDeath Crowd Tree", false));
        }
    }

    private static Quaternion FaceTo(Vector3 from, Vector3 to, float jitter)
    {
        Vector3 d = Flat(to - from);
        if (d.sqrMagnitude < 0.01f) d = Vector3.forward;
        return Quaternion.LookRotation(d.normalized, Vector3.up) * Quaternion.Euler(0f, UnityEngine.Random.Range(-jitter, jitter), 0f);
    }

    /// <summary>피 자리마다 천장에 방울(얼룩은 확률로)을 붙인다. 방울은 <see cref="BloodTick"/>이 떨어뜨린다.</summary>
    private void SpawnRain(List<Vector3> spots)
    {
        if (firstDrips.Length == 0) return;
        ParticleSystem template = firstDrips[0];
        foreach (Vector3 s in spots)
        {
            Vector3 ceil;
            float floorY;
            CeilingAt(s + Vector3.up * 1f, out ceil, out floorY);
            if (ceil.y - floorY > 7f) continue;   // 계단참·뚫린 곳

            GameObject drip = Instantiate(template.gameObject, ceil - Vector3.up * DripBelowCeiling, Quaternion.identity);
            drip.name = "LayoutDeath Rain Drip";
            // 방울 속 맺힘 표시(초당 150개)는 자리마다 두면 너무 무겁다 — 떨어지는 방울만 남긴다
            foreach (ParticleSystem child in drip.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (child.gameObject != drip) Destroy(child.gameObject);
            }
            ParticleSystem ps = drip.GetComponent<ParticleSystem>();
            ParticleSystem.EmissionModule em = ps.emission;
            em.enabled = false;
            ps.Play();
            rainObjects.Add(drip);
            rainEmitters.Add(ps);
            rainFloorY.Add(floorY);
            // 자리가 한꺼번에 열리지 않고 차례로 늘어난다 — 앞쪽(목록 앞) 몇 곳은 바로, 나머지는 빗줄기가 굵어지는 동안
            int n = rainActiveAt.Count;
            rainActiveAt.Add(n < 4 ? UnityEngine.Random.Range(0f, 0.3f) : UnityEngine.Random.Range(0.3f, rainRampSeconds));

            DecalProjector stainProjector = null;
            if (firstStain != null && UnityEngine.Random.value < rainStainChance)
            {
                GameObject stain = Instantiate(firstStain.gameObject, ceil, Quaternion.AngleAxis(UnityEngine.Random.Range(0f, 360f), Vector3.up) * firstStain.transform.localRotation);
                stain.name = "LayoutDeath Rain Stain";
                stainProjector = stain.GetComponent<DecalProjector>();
                float k = UnityEngine.Random.Range(0.45f, 1.05f);
                stainProjector.size = new Vector3(firstStainSize.x * k, firstStainSize.y * k, firstStainSize.z);
                stainProjector.fadeFactor = 0f;   // 그 자리에서 피가 떨어지기 시작하면 투명 → 불투명(BloodTick)
                rainObjects.Add(stain);
            }
            rainStains.Add(stainProjector);
        }
    }

    // ─────────────────────────────── 소리 · 진행 ───────────────────────────────

    private void StartRainSounds(Vector3 target)
    {
        var treeSpots = new List<Vector3> { target };
        foreach (GameObject g in crowd) if (g != null) treeSpots.Add(g.transform.position);
        foreach (RainSound rs in rainSounds)
        {
            if (rs == null || rs.clip == null) continue;
            int copies = rs.atTrees <= 0 ? 1 : Mathf.Min(rs.atTrees, treeSpots.Count);
            for (int c = 0; c < copies; c++)
            {
                var go = new GameObject("Rain " + rs.clip.name + (c > 0 ? " +" + c : ""));
                go.transform.SetParent(transform, false);
                AudioSource src = NewSource(go);
                src.clip = rs.clip;
                src.loop = true;
                src.volume = 0f;
                if (rs.atTrees > 0)
                {
                    // 가까운 나무부터가 아니라 섞어서 — 앞뒤·좌우에서 들린다
                    go.transform.position = treeSpots[(c * 5 + 1) % treeSpots.Count] + Vector3.up * 1.8f;
                    src.spatialBlend = 1f;
                    src.minDistance = 1.5f;
                    src.maxDistance = 22f;
                    src.rolloffMode = AudioRolloffMode.Logarithmic;
                    src.time = UnityEngine.Random.Range(0f, Mathf.Max(0f, rs.clip.length - 0.1f));
                    src.pitch = UnityEngine.Random.Range(0.9f, 1.08f);   // 같은 웅얼거림이 여러 사람처럼
                }
                src.Play();
                rainSources.Add(src);
                rainSourceOf.Add(rs);
            }
        }
    }

    /// <summary>방울이 닿는 소리 · 피 비 · 차오르는 화면/소리 — ①부터 끌려 들 때까지 매 프레임.</summary>
    private IEnumerator BloodTick()
    {
        while (true)
        {
            float now = Time.time;
            for (int i = pendingDrops.Count - 1; i >= 0; i--)
            {
                if (pendingDrops[i].Key > now) continue;
                PlayDropSound(pendingDrops[i].Value);
                pendingDrops.RemoveAt(i);
            }

            if (rainStartTime >= 0f)
            {
                float since = now - rainStartTime;
                float k = Mathf.Clamp01(since / rainRampSeconds);
                // 열린 자리 — 열리는 순간부터 그 자리 얼룩이 투명 → 불투명
                rainActive.Clear();
                for (int i = 0; i < rainEmitters.Count; i++)
                {
                    float open = since - rainActiveAt[i];
                    if (open < 0f) continue;
                    rainActive.Add(i);
                    DecalProjector stain = rainStains[i];
                    if (stain != null && stain.fadeFactor < 1f) stain.fadeFactor = Mathf.Clamp01(open / rainStainFadeSeconds);
                }
                rainAcc += Mathf.Lerp(rainRateStart, rainRateFull, k * k) * Time.deltaTime;
                while (rainAcc >= 1f && rainActive.Count > 0)
                {
                    rainAcc -= 1f;
                    int i = rainActive[UnityEngine.Random.Range(0, rainActive.Count)];
                    EmitDrop(rainEmitters[i], rainFloorY[i], UnityEngine.Random.value < rainSoundChance);
                }
                if (rainActive.Count == 0) rainAcc = 0f;

                float f = Mathf.Clamp01(since / revealFadeSeconds);
                f = f * f * (3f - 2f * f);
                baseWobble = revealWobble * f;
                baseVignette = revealVignette * f;
                if (Phase == "LookDown" || Phase == "Reveal")   // 걷는 동안은 Run이 이 값을 바닥으로 쓴다
                {
                    if (wobble != null) wobble.weight = baseWobble;
                    SetAlpha(darkVignette, baseVignette);
                }

                for (int j = 0; j < rainSources.Count; j++)
                {
                    RainSound rs = rainSourceOf[j];
                    float v = rs.fadeIn <= 0f ? 1f : Mathf.Clamp01(since / rs.fadeIn);
                    if (rainSources[j] != null) rainSources[j].volume = rs.volume * v * v;
                }
            }
            yield return null;
        }
    }

    private void PlayDropSound(Vector3 at)
    {
        if (dropClips.Length == 0) return;
        if (dropPool.Count == 0)
        {
            for (int i = 0; i < 12; i++)
            {
                var go = new GameObject("Drop " + i);
                go.transform.SetParent(transform, false);
                AudioSource src = NewSource(go);
                src.spatialBlend = 1f;
                src.minDistance = 1f;
                src.maxDistance = 16f;
                src.rolloffMode = AudioRolloffMode.Logarithmic;
                dropPool.Add(src);
            }
        }
        AudioSource s = dropPool[dropNext];
        dropNext = (dropNext + 1) % dropPool.Count;
        s.transform.position = at;
        s.clip = dropClips[UnityEngine.Random.Range(0, dropClips.Length)];
        s.volume = dropVolume * UnityEngine.Random.Range(0.6f, 1f);
        s.pitch = UnityEngine.Random.Range(0.9f, 1.1f);
        s.Play();
    }

    /// <summary>끌려 드는 순간 — 피 비와 그 소리를 멈춘다(이미 떨어지는 방울은 그대로 떨어진다).</summary>
    private void StopBloodRain()
    {
        if (bloodTick != null) StopCoroutine(bloodTick);
        bloodTick = null;
        rainStartTime = -1f;
        pendingDrops.Clear();
        foreach (AudioSource s in rainSources) if (s != null) s.Stop();
        foreach (AudioSource s in dropPool) if (s != null) s.Stop();
    }

    /// <summary>디버그 재생이 끝날 때 — ver2가 세운 것을 모두 거둔다(바닥의 젖은 자국은 몇 초 뒤 스스로 사라진다).</summary>
    private void CleanupBloodRain()
    {
        StopBloodRain();
        if (firstProp != null) Destroy(firstProp);
        firstProp = null;
        firstDrips = new ParticleSystem[0];
        firstStain = null;
        foreach (GameObject g in rainObjects) if (g != null) Destroy(g);
        rainObjects.Clear();
        rainEmitters.Clear();
        rainFloorY.Clear();
        rainActiveAt.Clear();
        rainStains.Clear();
        foreach (GameObject g in crowd) if (g != null) Destroy(g);
        crowd.Clear();
        foreach (AudioSource s in rainSources) if (s != null) Destroy(s.gameObject);
        rainSources.Clear();
        rainSourceOf.Clear();
        baseWobble = 0f;
        baseVignette = 0f;
        if (darkVignette != null) SetAlpha(darkVignette, 0f);
    }
}

using System.Collections.Generic;
using NightDuty;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

/// <summary>
/// 104차 배치 연출 1단계(민: 「배치 축의 연출 부족 — 김진선님의 흔들리는 관물대 연출 활용」) — 김진선님 관물대·서랍 프리셋을 배치 구간에 묶는다.
/// 배치 축(「있던 것이 다른 자리에」)은 맵 변화가 소리 둘(천장 낙하·소품 떨림)뿐이었다. 김진선님 프리셋 중 쓰지 않던 흔들리는 관물대(<see cref="RampagePreset"/>)·
/// 서랍장(<see cref="DrawerPreset"/>)과 삐걱 열린 사물함(<see cref="CreakPreset"/>)을 런타임에 씬 사물함·서랍장 자리에 세운다(<see cref="ProximityDoors"/>와 같은 짜임 — 씬 파일·프리팹은 고치지 않는다).
/// <list type="bullet">
/// <item><b>복도 배치 구간 <see cref="ShutBand"/>+</b>: 늘 문이 열려 있던 복도 사물함 하나가 <b>안 보는 사이</b> 닫혀 있다(삐걱 열림의 첫 자세 — 소리 없이).
/// 복도의 움직이지 않는 사물함(<see cref="LockerPrefix"/>)은 씬에서 모두 문이 열린 채로 놓여 있다(PlayScene 실측 19개 모두 27~133° 열림).</item>
/// <item><b>복도 배치 구간 <see cref="RampageBand"/>+</b>: 다른 사물함 하나가 플레이어 <b>등 뒤·옆</b>(<see cref="RampageNear"/>~<see cref="RampageFar"/>m, 시선에서 <see cref="RampageBehindDegrees"/>° 밖)에서
/// 한 번 마구 흔들린다(김진선님 타임라인 3.8초 · 가구 끄는 소리). 밤마다 한 번.</item>
/// <item><b>경비실 서랍장(남쪽 벽 카드 목록함) 배치 구간 <see cref="DrawerBand"/>+</b>: <see cref="DrawerNear"/>~<see cref="DrawerFar"/>m 안에 오면 서랍들이 덜컹거린다(9.7초). 밤마다 한 번.</item>
/// <item>바꿔 세운 프리셋은 원래 사물함의 재질(빨강·초록 칠)과 문 각도를 그대로 받는다 — 흔들리기 전에는 바뀐 티가 나지 않게.</item>
/// <item>고르는 사물함: 복도의 움직이지 않는 사물함(<see cref="LockerPrefix"/> — 여닫는 사물함은 배터리 칸이라 뺀다) 중 바로 선 것,
/// 점검 기준점(<c>Inspect *</c>)·이미 놓인 김진선님 연출(가짜 놀람 사물함 둘)에서 <see cref="AvoidRadius"/>m 떨어진 것. 밤마다 새로 고른다.</item>
/// <item>조우·놀람·붙잡힘·사망 컷신·피날레 중에는 흔들지 않는다. 밤 시작 뒤 <see cref="QuietAfterStart"/>초는 쉰다. 재시작하면 원래 사물함으로 돌려 두고 다시 센다.</item>
/// <item>프리셋의 자체 시동(구역 트리거 · <c>HorrorAxisLink</c> — 배치 구간 1만 넘어도 어디서든 재생)은 끈다. 판정과 무관(읽기만).</item>
/// </list>
/// 근무 씬에 스스로 선다.
/// </summary>
[DisallowMultipleComponent]
public sealed class LayoutCabinets : MonoBehaviour
{
    /// <summary>삐걱 열려 멈추는 사물함(끝 자세 = 문 열림).</summary>
    public const string CreakPreset = "HorrorEvent_CabinetCreak";

    /// <summary>마구 흔들리는 관물대.</summary>
    public const string RampagePreset = "HorrorEvent_CabinetRampage";

    /// <summary>서랍이 덜컹거리는 카드 목록함.</summary>
    public const string DrawerPreset = "HorrorEvent_DrawerRampage";

    /// <summary>복도 배치 이 구간부터 열려 있던 사물함 하나가 닫혀 있다.</summary>
    public const int ShutBand = 2;

    /// <summary>복도 배치 이 구간부터 등 뒤 사물함이 흔들린다.</summary>
    public const int RampageBand = 3;

    /// <summary>서랍장 공간 배치 이 구간부터 서랍이 덜컹거린다.</summary>
    public const int DrawerBand = 3;

    /// <summary>흔들림을 거는 거리(m, 수평) — 가까운 쪽.</summary>
    public const float RampageNear = 3.5f;

    /// <summary>흔들림을 거는 거리(m, 수평) — 먼 쪽(소리 14m 안).</summary>
    public const float RampageFar = 9f;

    /// <summary>시선에서 이만큼(°) 밖(등 뒤·옆)일 때만 흔든다 — 소리에 돌아보면 흔들리고 있다.</summary>
    public const float RampageBehindDegrees = 70f;

    /// <summary>서랍장 덜컹 거리(m, 수평).</summary>
    public const float DrawerNear = 2.5f;

    /// <summary>서랍장 덜컹 거리(m, 수평) — 먼 쪽.</summary>
    public const float DrawerFar = 7f;

    /// <summary>밤 시작(재시작 포함) 뒤 이만큼(초)은 흔들지 않는다.</summary>
    public const float QuietAfterStart = 45f;

    /// <summary>움직이지 않는 사물함 이름 머리(여닫는 LockerA·B·C·D는 배터리 칸).</summary>
    public const string LockerPrefix = "LockerB_static";

    /// <summary>복도 사물함 묶음.</summary>
    public const string CorridorRoot = "Interior/Corridors";

    /// <summary>경비실 남쪽 벽의 카드 목록함(서랍장, 공간은 경비실 — PlayScene 실측 x 31.1 z 44.5).</summary>
    public const string DrawerPath = "Interior/Corridors/FloorCorridors_1st/LibraryCardCatalogB";

    /// <summary>점검 기준점·기존 김진선님 연출에서 이만큼(m) 떨어진 사물함만.</summary>
    public const float AvoidRadius = 1.6f;

    private sealed class Spot
    {
        public Transform Original;
        public string Preset;
        public GameObject Instance;
        public HorrorEvent Event;
        public bool Played;
        public bool Looping;         // 104차: 응시할 때까지 되풀이
        public float GazedAt = -1f;  // 104차: 응시를 시작한 때(Time.time)
    }

    /// <summary>
    /// 104차(사용자: 「관물대/열쇠함 요동치는 것도 응시하기 전까지 계속 흔들리다가 응시 2초 후 멈추게」): 흔들림이 시작되면 응시할 때까지 되풀이하고,
    /// 응시(화면 가운데 <see cref="GazeDegrees"/>° 안 · 가림 없음)를 시작한 뒤 이만큼(초) 지나면 멈춘다(첫 자세로).
    /// </summary>
    public const float StopAfterGazeSeconds = 2f;

    /// <summary>104차: 응시로 치는 시선 각(°).</summary>
    public const float GazeDegrees = 25f;

    /// <summary>104차: 응시할 때까지 흔들리는 중인지(검수용).</summary>
    public bool RampageLooping { get { return _rampage.Looping; } }

    /// <summary>104차: 응시할 때까지 덜컹거리는 중인지(검수용).</summary>
    public bool DrawerLooping { get { return _drawer.Looping; } }

    private static readonly List<Transform> s_candidates = new List<Transform>();
    private readonly Spot _shut = new Spot { Preset = CreakPreset };
    private readonly Spot _rampage = new Spot { Preset = RampagePreset };
    private readonly Spot _drawer = new Spot { Preset = DrawerPreset };
    private HorrorPresetsSO _presets;
    private object _plan;
    private float _since;
    private SpaceId _drawerSpace = SpaceId.None;
    private Transform _player;

    /// <summary>지금 근무 씬의 것. 없으면 null.</summary>
    public static LayoutCabinets Active { get; private set; }

    /// <summary>닫혀 있을 사물함 자리(검수용, 없으면 null).</summary>
    public Transform ShutSpot { get { return _shut.Original; } }

    /// <summary>흔들릴 사물함 자리(검수용).</summary>
    public Transform RampageSpot { get { return _rampage.Original; } }

    /// <summary>서랍장 자리(검수용).</summary>
    public Transform DrawerSpot { get { return _drawer.Original; } }

    /// <summary>104차: 서랍장 대역(바꿔 세웠으면 — 원래 것은 꺼져 있다). <see cref="DutyCards"/>가 열쇠함 조준·테두리에 쓴다.</summary>
    public Transform DrawerInstance { get { return _drawer.Instance != null ? _drawer.Instance.transform : null; } }

    /// <summary>이번 밤(시도) 흔들렸는지(검수용).</summary>
    public bool RampagePlayed { get { return _rampage.Played; } }

    /// <summary>이번 밤(시도) 서랍이 덜컹거렸는지(검수용).</summary>
    public bool DrawerPlayed { get { return _drawer.Played; } }

    /// <summary>닫힌 사물함이 서 있는지(검수용).</summary>
    public bool ShutShown { get { return _shut.Instance != null; } }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Install()
    {
        Active = null;
        s_candidates.Clear();
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
        if (!FlowAutoInstall.IsDutyScene(scene) || FlowAutoInstall.Exists<LayoutCabinets>(scene)) return;
        FlowAutoInstall.CreateHost<LayoutCabinets>(scene, "LayoutCabinets (auto)");
    }

    private void OnEnable()
    {
        Active = this;
        EventBus.NightRestarted += OnRestarted;
    }

    private void OnDisable()
    {
        EventBus.NightRestarted -= OnRestarted;
        RestoreAll();
        if (Active == this) Active = null;
    }

    private void OnRestarted(RestartResult result)
    {
        // 재시작 — 원래 사물함으로 돌려 두고 다시 센다(그 시점 구간에 맞게 다시 선다).
        RestoreAll();
        _since = 0f;
    }

    private void Update()
    {
        if (!NightRun.IsNightActive)
        {
            return;
        }

        object plan = NightRun.Inspections != null ? NightRun.Inspections.Plan : null;
        if (!ReferenceEquals(plan, _plan))
        {
            _plan = plan;
            NewNight();
        }

        _since += Time.deltaTime;
        Camera cam = Camera.main;
        if (cam == null) return;
        KeepShaking(_rampage, cam);   // 104차: 응시할 때까지
        KeepShaking(_drawer, cam);

        int corridor = (int)NightRun.ShownBand(SpaceId.Corridor, FearAxis.Layout);
        // 닫힌 사물함 — 구간에 닿으면 안 보일 때 바꿔 둔다(바뀌는 순간을 보지 않게).
        if (corridor >= ShutBand && _shut.Instance == null && _shut.Original != null && !Seen(_shut.Original, cam))
        {
            if (Swap(_shut))
            {
                ShowStart(_shut);
                if (DirectionStage.Verbose) Debug.Log("[LayoutCabinets] 열려 있던 사물함이 닫혀 있음 — " + Name(_shut.Original));
            }
        }

        // 흔들릴 사물함 — 미리 바꿔 두고(닫힌 첫 자세) 등 뒤에 오면 흔든다.
        if (corridor >= RampageBand && !_rampage.Played && _rampage.Original != null)
        {
            if (_rampage.Instance == null)
            {
                if (!Seen(_rampage.Original, cam) && Swap(_rampage))
                {
                    ShowStart(_rampage);
                    MatchDoor(_rampage);   // 흔들리기 전에는 원래 문 각도 그대로
                }
            }
            else if (_since >= QuietAfterStart && !Busy() && Behind(_rampage.Original, cam, RampageNear, RampageFar))
            {
                Fire(_rampage, "등 뒤 사물함 흔들림");
            }
        }

        if (_drawer.Original != null && !_drawer.Played && (int)NightRun.ShownBand(_drawerSpace, FearAxis.Layout) >= DrawerBand)
        {
            if (_drawer.Instance == null)
            {
                if (!Seen(_drawer.Original, cam) && Swap(_drawer)) ShowStart(_drawer);
            }
            else if (_since >= QuietAfterStart && !Busy() && Near(_drawer.Original, DrawerNear, DrawerFar))
            {
                Fire(_drawer, "서랍장 덜컹");
            }
        }
    }

    // ── 밤 ────────────────────────────────────────────────

    private void NewNight()
    {
        RestoreAll();
        _since = 0f;
        Collect();
        int n = s_candidates.Count;
        _shut.Original = null;
        _rampage.Original = null;
        if (n > 0)
        {
            System.Random roll = new System.Random(NightRun.Day * 7919 + Random.Range(0, 100000));
            int a = roll.Next(n);
            _shut.Original = s_candidates[a];
            if (n > 1)
            {
                int b = roll.Next(n - 1);
                if (b >= a) b++;
                _rampage.Original = s_candidates[b];
            }
        }

        GameObject drawer = GameObject.Find(DrawerPath);
        _drawer.Original = drawer != null && drawer.activeInHierarchy ? drawer.transform : null;
        SpaceZones zones = FindAnyObjectByType<SpaceZones>();
        _drawerSpace = _drawer.Original != null && zones != null ? SpaceIds.Canonical(zones.SpaceAt(_drawer.Original.position)) : SpaceId.None;
        if (_drawerSpace == SpaceId.None) _drawerSpace = SpaceId.Corridor;
        if (DirectionStage.Verbose)
        {
            Debug.Log("[LayoutCabinets] " + NightRun.Day + "일차 — 후보 사물함 " + n + " · 닫힘 " + Name(_shut.Original) + " · 흔들림 " + Name(_rampage.Original)
                      + " · 서랍장 " + Name(_drawer.Original) + "(" + _drawerSpace + ")");
        }
    }

    /// <summary>복도의 움직이지 않는 바로 선 사물함 중 점검 기준점·기존 연출에서 떨어진 것.</summary>
    private static void Collect()
    {
        s_candidates.Clear();
        GameObject root = GameObject.Find(CorridorRoot);
        if (root == null) return;
        List<Vector3> avoid = new List<Vector3>();
        foreach (Transform t in FindObjectsByType<Transform>(FindObjectsSortMode.None))
        {
            if (t.name.StartsWith("Inspect ")) avoid.Add(t.position);
        }

        foreach (HorrorEvent e in FindObjectsByType<HorrorEvent>(FindObjectsSortMode.None)) avoid.Add(e.transform.position);
        foreach (Transform t in root.GetComponentsInChildren<Transform>(false))
        {
            if (!t.name.StartsWith(LockerPrefix) || !IsCandidate(t.position, t.eulerAngles)) continue;
            bool near = false;
            for (int i = 0; i < avoid.Count && !near; i++)
            {
                Vector3 d = avoid[i] - t.position;
                d.y = 0f;
                near = d.magnitude < AvoidRadius;
            }

            if (!near) s_candidates.Add(t);
        }
    }

    /// <summary>바닥(1층)에 바로 선 사물함인지 — 넘어지거나 기운 것·위층 것은 뺀다.</summary>
    public static bool IsCandidate(Vector3 position, Vector3 euler)
    {
        if (position.y < 1.3f || position.y > 1.7f) return false;
        if (Mathf.Abs(Mathf.DeltaAngle(euler.x, 0f)) > 3f || Mathf.Abs(Mathf.DeltaAngle(euler.z, 0f)) > 3f) return false;
        float y = Mathf.Repeat(euler.y, 90f);
        return y < 3f || y > 87f;
    }

    // ── 바꿔 세우기 ───────────────────────────────────────

    private bool Swap(Spot s)
    {
        if (_presets == null) _presets = Resources.Load<HorrorPresetsSO>(HorrorPresetsSO.ResourcePath);
        GameObject prefab = _presets != null ? _presets.Find(s.Preset) : null;
        if (prefab == null || s.Original == null)
        {
            s.Original = null;   // 다시 시도하지 않는다
            return false;
        }

        // 꺼 둔 그릇에서 만들어 자체 시동(구역 트리거·축 연결)을 켜지기 전에 끈다.
        GameObject holder = new GameObject("배치 연출 준비");
        holder.SetActive(false);
        GameObject inst = Instantiate(prefab, s.Original.position, s.Original.rotation, holder.transform);
        foreach (HorrorTriggerZone z in inst.GetComponentsInChildren<HorrorTriggerZone>(true))
        {
            z.enabled = false;
            Collider c = z.GetComponent<Collider>();
            if (c != null) c.enabled = false;
        }

        foreach (HorrorAxisLink l in inst.GetComponentsInChildren<HorrorAxisLink>(true)) l.enabled = false;
        inst.name = s.Preset + " (배치)";
        inst.transform.SetParent(transform, true);
        Destroy(holder);
        s.Instance = inst;
        s.Event = inst.GetComponent<HorrorEvent>();
        CopyLook(s.Original, inst.transform);
        s.Original.gameObject.SetActive(false);
        return true;
    }

    /// <summary>
    /// 원래 물건의 재질을 같은 자리 렌더러에 입힌다 — 사물함 몸통(원래 루트 ↔ 프리셋 <c>Cabinet</c>)·문(<c>LockerDoor</c>), 그 밖에는 같은 이름 렌더러끼리.
    /// 복도 사물함은 빨강(Lockers02)·초록(Lockers) 칠이 섞여 있는데 프리셋은 초록 하나라, 그대로 두면 바꾼 사물함만 색이 달랐다(PlayScene 실측).
    /// </summary>
    private static void CopyLook(Transform original, Transform inst)
    {
        Dictionary<string, Renderer> from = new Dictionary<string, Renderer>();
        foreach (Renderer r in original.GetComponentsInChildren<Renderer>(true))
        {
            string key = r.transform == original ? "Cabinet" : r.name;
            if (!from.ContainsKey(key)) from[key] = r;
        }

        foreach (Renderer r in inst.GetComponentsInChildren<Renderer>(true))
        {
            Renderer src;
            if (!from.TryGetValue(r.name, out src) || src == null) continue;
            Material[] mats = src.sharedMaterials;
            if (mats.Length == r.sharedMaterials.Length) r.sharedMaterials = mats;
        }
    }

    /// <summary>프리셋 문짝을 원래 사물함의 문 각도로.</summary>
    private static void MatchDoor(Spot s)
    {
        if (s.Instance == null || s.Original == null) return;
        Transform from = s.Original.Find("LockerDoor");
        Transform to = FindChild(s.Instance.transform, "LockerDoor");
        if (from != null && to != null) to.localRotation = from.localRotation;
    }

    private static Transform FindChild(Transform t, string name)
    {
        if (t.name == name) return t;
        foreach (Transform c in t)
        {
            Transform f = FindChild(c, name);
            if (f != null) return f;
        }

        return null;
    }

    /// <summary>첫 자세(닫힘)로.</summary>
    private static void ShowStart(Spot s)
    {
        PlayableDirector d = s.Instance != null ? s.Instance.GetComponentInChildren<PlayableDirector>() : null;
        if (d == null || d.playableAsset == null) return;
        d.time = 0;
        d.Evaluate();
    }

    private static void Fire(Spot s, string what)
    {
        s.Played = true;
        s.Looping = true;   // 104차: 응시할 때까지 되풀이
        s.GazedAt = -1f;
        if (s.Event != null) s.Event.Play();
        if (DirectionStage.Verbose) Debug.Log("[LayoutCabinets] " + what + " — " + Name(s.Original));
    }

    private void RestoreAll()
    {
        Restore(_shut);
        Restore(_rampage);
        Restore(_drawer);
    }

    /// <summary>
    /// 104차: 흔들림이 끝나면 다시 처음부터(응시할 때까지) — 응시를 시작하고 <see cref="StopAfterGazeSeconds"/>초 뒤 첫 자세로 멈춘다.
    /// 붙잡힘·사망 컷신 중에는 되풀이하지 않는다(멈추지도 않음 — 끝나면 이어서).
    /// </summary>
    private static void KeepShaking(Spot s, Camera cam)
    {
        if (!s.Looping || s.Instance == null) return;
        PlayableDirector d = s.Instance.GetComponentInChildren<PlayableDirector>();
        if (d == null || d.playableAsset == null)
        {
            s.Looping = false;
            return;
        }

        if (s.GazedAt < 0f && Gazed(s.Instance.transform, cam)) s.GazedAt = Time.time;
        if (s.GazedAt >= 0f && Time.time - s.GazedAt >= StopAfterGazeSeconds)
        {
            s.Looping = false;
            d.Stop();
            d.time = 0;
            d.Evaluate();   // 첫 자세(닫힘)
            if (DirectionStage.Verbose) Debug.Log("[LayoutCabinets] 응시 " + StopAfterGazeSeconds + "초 — 흔들림 멈춤 · " + s.Instance.name);
            return;
        }

        if (NightRun.IsCaptured || DeathCutscene.Playing != null) return;
        bool playing = d.state == PlayState.Playing && d.time < d.duration - 0.02;
        if (playing || (s.Event != null && s.Event.IsPlaying)) return;
        d.time = 0;
        d.Play();
        d.Evaluate();
    }

    /// <summary>
    /// 104차: 화면 가운데 <see cref="GazeDegrees"/>° 안에 가림 없이 보이는지. 경비실 서랍장은 따로 둔 충돌체(GuardRoomColliders)가 몸을 덮고 있어
    /// 화면 판정(<see cref="UnseenDespawn.VisibleTo"/>)이 늘 가려진 것으로 봤다 — 시선이 닿은 점이 물건 곁(<see cref="GazeHitSlack"/>m)이면 보인 것으로 친다.
    /// </summary>
    private static bool Gazed(Transform t, Camera cam)
    {
        if (t == null || cam == null) return false;
        Vector3 target = t.position + Vector3.up * 1f;
        Vector3 from = cam.transform.position;
        Vector3 to = target - from;
        if (Vector3.Angle(cam.transform.forward, to) > GazeDegrees) return false;
        RaycastHit hit;
        if (!Physics.Linecast(from, target, out hit, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return true;
        if (hit.collider.transform.IsChildOf(t)) return true;
        PlayerSensors hub = PlayerSensors.Active;
        if (hub != null && hub.PlayerRoot != null && hit.collider.transform.IsChildOf(hub.PlayerRoot)) return true;
        Vector3 d = hit.point - t.position;
        d.y = 0f;
        return d.magnitude <= GazeHitSlack;
    }

    /// <summary>104차: 시선이 닿은 점이 물건(바닥 기준)에서 이만큼(m, 수평) 안이면 그 물건을 본 것.</summary>
    public const float GazeHitSlack = 1.0f;

    private static void Restore(Spot s)
    {
        if (s.Instance != null) Destroy(s.Instance);
        s.Instance = null;
        s.Event = null;
        s.Played = false;
        s.Looping = false;
        s.GazedAt = -1f;
        if (s.Original != null && !s.Original.gameObject.activeSelf) s.Original.gameObject.SetActive(true);
    }

    // ── 조건 ─────────────────────────────────────────────

    /// <summary>다른 연출과 겹칠 때 — 조우(전조·세움·대면·마무리) · 놀람 · 붙잡힘 · 사망 컷신 · 피날레.</summary>
    private static bool Busy()
    {
        if (!NightRun.IsNightActive || NightRun.IsCaptured || ViolationScare.Busy || DeathCutscene.Playing != null) return true;
        if (FinaleDirector.Active != null && FinaleDirector.Active.IsRunning) return true;
        if (NightRun.Tension == null) return false;
        foreach (EncounterRun r in NightRun.Tension.Runs)
        {
            if (r == null) continue;
            if (r.State == EncounterRunState.Foreshadow || r.State == EncounterRunState.Presenting || r.State == EncounterRunState.Active || r.State == EncounterRunState.Releasing) return true;
        }

        return false;
    }

    private static bool Seen(Transform t, Camera cam)
    {
        return t != null && UnseenDespawn.VisibleTo(t.gameObject, cam);
    }

    private Transform Player()
    {
        if (_player == null)
        {
            PlayerSensors hub = PlayerSensors.Active;
            if (hub != null) _player = hub.PlayerRoot;
            if (_player == null)
            {
                FPController fp = FindAnyObjectByType<FPController>();
                if (fp != null) _player = fp.transform;
            }
        }

        return _player;
    }

    private bool Near(Transform t, float near, float far)
    {
        Transform p = Player();
        if (p == null) return false;
        Vector3 d = t.position - p.position;
        d.y = 0f;
        float m = d.magnitude;
        return m >= near && m <= far && Mathf.Abs(t.position.y - p.position.y) < 2.5f;
    }

    /// <summary>그 거리 안이고 시선에서 <see cref="RampageBehindDegrees"/>° 밖(등 뒤·옆)인지.</summary>
    private bool Behind(Transform t, Camera cam, float near, float far)
    {
        if (!Near(t, near, far)) return false;
        Vector3 to = t.position + Vector3.up * 1f - cam.transform.position;
        Vector3 fwd = cam.transform.forward;
        to.y = 0f;
        fwd.y = 0f;
        if (to.sqrMagnitude < 0.01f || fwd.sqrMagnitude < 0.01f) return false;
        return Vector3.Angle(fwd, to) > RampageBehindDegrees;
    }

    private static string Name(Transform t)
    {
        return t != null ? t.name : "없음";
    }
}

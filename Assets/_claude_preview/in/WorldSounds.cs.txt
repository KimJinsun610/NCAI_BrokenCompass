using System;
using System.Collections;
using System.Collections.Generic;
using NightDuty;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 맵 소리 · 교차 연출 소리 · 수칙 위반 현장 반응(최종 기획서 「구간별 맵·감각 변화」·「교차 연출」·「위반 피드백 — 세 단계」의 1단계, 2026-10-04 37차).
/// 소리는 연출 소리 표의 키(<c>map.*</c> · <c>cross.*</c> · <c>trust.*</c> · <c>react.*</c>)로 찾고 없으면 조용하다. <b>판정 신호를 보내지 않는다</b>(§2.7).
/// <list type="bullet">
/// <item><b>청각 맵 소리</b>: 그 공간의 청각 연출 구간 n이면 1..n 구간의 소리가 <b>하루 한 번씩</b>(재시작해도 다시 나지 않음), 플레이어가 그 공간에 머물 때 6~18초 뒤부터 18~40초 간격으로 하나씩.
/// 복도 1 앞쪽 문 닫힘·멀리서 사물함 덜컹 / 2 지나온 문 닫힘 / 3 손잡이 놓는 소리 + 뒤쪽 문 닫힘 / 4 뒤에서 따라오다 멈추는 발소리(H3가 편성된 날은 뺌).
/// 교실 1 지우개 / 2 나갈 때 뒷줄 책상 타격(교실을 나서는 순간) / 3 칠판 긁기 + 교탁 의자 / 4 교탁 의자 한 구절 더.
/// 과학실 1 유리 기구 / 2 책상 긁힘 / 3 부딪힘이 책상 아래로 / 4 무거운 물체. 화장실 2 세면대 배관.
/// (교실 0 「문밖 분필 세 획」과 화장실 1 「안쪽 칸 물 내림」은 C1·T1 방아쇠와 같은 소리라 넣지 않았다 — 맵 소리가 수칙 신호처럼 들리면 안 된다.)</item>
/// <item><b>배치</b>: 3 천장 조각 떨어지는 소리(하루 한 번) · 4 소품이 미세하게 떨림(그 공간 배치 4인 동안 작게 계속).</item>
/// <item><b>조도</b>: 구간 때문에 등이 꺼지는 순간 그 등에서 형광등이 지직이다 꺼지는 소리(<see cref="IlluminanceMap.LampWentOff"/>, 밤 시작 5초 뒤부터).</item>
/// <item><b>교차·신뢰</b>: 청각 2 + 조도 2 = 화장실 소등 동안 물소리가 칸 사이를 옮겨 다님 · 청각 2 + 신뢰 3 = 경비실 문 밖 기괴한 숨소리 · 신뢰 4 = 경비실 소품 요동.</item>
/// <item><b>위반 현장 반응</b>(어긴 축의 언어 — 청각이면 그 대상의 소리): C1 분필 부러짐 · L1 책장 끼익 · L2 책 덮임 · L5 창에 손바닥 · S2 유리 밟음 · G1 멈추면 뒤에서 따라오다 멈추는 구두 · K2 CCTV 너머 의자 삐걱.</item>
/// </list>
/// 근무 씬에 자동으로 선다.
/// </summary>
[DisallowMultipleComponent]
public sealed class WorldSounds : MonoBehaviour
{
    private enum Place
    {
        Around,
        Ahead,
        Behind,
        Far,
        Low,
        GuardDoor,
    }

    private sealed class MapItem
    {
        public SpaceId Space;
        public FearAxis Axis;
        public int Level;
        public string Key;
        public Place Where;
        public float FollowDelay;   // 키 + "+"를 이만큼 뒤에 같은 자리에서
        public Func<bool> Allowed;
    }

    private static readonly MapItem[] Items =
    {
        Item(SpaceId.Corridor, FearAxis.Auditory, 1, "map.hall.1", Place.Ahead),
        Item(SpaceId.Corridor, FearAxis.Auditory, 1, "map.hall.1b", Place.Far),
        Item(SpaceId.Corridor, FearAxis.Auditory, 2, "map.hall.2", Place.Behind),
        Item(SpaceId.Corridor, FearAxis.Auditory, 3, "map.hall.3", Place.Behind, 0.9f),
        Item(SpaceId.Corridor, FearAxis.Auditory, 4, "map.hall.4", Place.Behind, 0f, () => NightRun.Program == null || !NightRun.Program.Has("H3")),
        Item(SpaceId.Classroom_1_3, FearAxis.Auditory, 1, "map.class.1", Place.Around),
        Item(SpaceId.Classroom_1_3, FearAxis.Auditory, 3, "map.class.3", Place.Around),
        Item(SpaceId.Classroom_1_3, FearAxis.Auditory, 4, "map.class.4", Place.Around),
        Item(SpaceId.ScienceRoom, FearAxis.Auditory, 1, "map.lab.1", Place.Around),
        Item(SpaceId.ScienceRoom, FearAxis.Auditory, 2, "map.lab.2", Place.Around),
        Item(SpaceId.ScienceRoom, FearAxis.Auditory, 3, "map.lab.3", Place.Low),
        Item(SpaceId.ScienceRoom, FearAxis.Auditory, 4, "map.lab.4", Place.Around),
        Item(SpaceId.Toilet, FearAxis.Auditory, 2, "map.toilet.2", Place.Around),
        Item(SpaceId.Corridor, FearAxis.Layout, 3, "map.layout.3", Place.Around),
        Item(SpaceId.Classroom_1_3, FearAxis.Layout, 3, "map.layout.3", Place.Around),
        Item(SpaceId.SecurityRoom, FearAxis.Trust, 3, "cross.guard.breath", Place.GuardDoor, 0f, () => GlobalBand(FearAxis.Auditory) >= 2),
        Item(SpaceId.SecurityRoom, FearAxis.Trust, 4, "trust.guard.rattle", Place.Around),
    };

    private const string ClassExitKey = "map.class.2";
    private const string TremoloKey = "map.layout.4";

    private readonly HashSet<string> _played = new HashSet<string>(StringComparer.Ordinal);
    private readonly Dictionary<string, Vector3> _points = new Dictionary<string, Vector3>(StringComparer.Ordinal);
    private InspectionPlan _plan;
    private float _nightStart;
    private SpaceId _space = SpaceId.None;
    private float _nextAt;
    private Vector3 _lastClassroomPos;
    private float _poll;
    private AudioSource _tremble;
    private Coroutine _water;

    /// <summary>지금 근무 씬의 것. 없으면 null.</summary>
    public static WorldSounds Active { get; private set; }

    /// <summary>그 밤 이미 낸 하루 한 번 소리 키(디버그·검수).</summary>
    public IEnumerable<string> Played
    {
        get { return _played; }
    }

    private static MapItem Item(SpaceId space, FearAxis axis, int level, string key, Place where, float follow = 0f, Func<bool> allowed = null)
    {
        return new MapItem { Space = space, Axis = axis, Level = level, Key = key, Where = where, FollowDelay = follow, Allowed = allowed };
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
        if (!FlowAutoInstall.IsDutyScene(scene) || FlowAutoInstall.Exists<WorldSounds>(scene)) return;
        FlowAutoInstall.CreateHost<WorldSounds>(scene, "WorldSounds (auto)");
    }

    private void OnEnable()
    {
        Active = this;
        EventBus.FinalRuleSettled += OnRuleSettled;
        EventBus.DirectionEmitted += OnDirection;
        IlluminanceMap.LampWentOff += OnLampOff;
    }

    private void OnDisable()
    {
        EventBus.FinalRuleSettled -= OnRuleSettled;
        EventBus.DirectionEmitted -= OnDirection;
        IlluminanceMap.LampWentOff -= OnLampOff;
        if (Active == this) Active = null;
    }

    // ── 맵 소리 ─────────────────────────────────────────────

    private void Update()
    {
        bool live = NightRun.IsNightActive && !NightRun.IsCaptured;
        InspectionPlan plan = NightRun.Inspections != null ? NightRun.Inspections.Plan : null;
        if (!ReferenceEquals(plan, _plan))
        {
            _plan = plan;
            _played.Clear();   // 새 밤
            _points.Clear();
            _nightStart = Time.time;
        }

        Tremble(live);
        if (!live) return;

        _poll -= Time.deltaTime;
        if (_poll > 0f) return;
        _poll = 0.25f;

        SpaceId space = NightRun.CurrentSpace;
        if (space != _space)
        {
            if (_space == SpaceId.Classroom_1_3) LeftClassroom();
            _space = space;
            _nextAt = Time.time + UnityEngine.Random.Range(6f, 18f);
        }

        Camera cam = Camera.main;
        if (space == SpaceId.Classroom_1_3 && cam != null) _lastClassroomPos = cam.transform.position;
        if (Time.time < _nextAt) return;

        for (int i = 0; i < Items.Length; i++)
        {
            MapItem it = Items[i];
            if (it.Space != space || _played.Contains(it.Key)) continue;
            int band = it.Axis == FearAxis.Trust ? GlobalBand(FearAxis.Trust) : (int)NightRun.ShownBand(space, it.Axis);
            if (band < it.Level || (it.Allowed != null && !it.Allowed())) continue;
            if (!PlayMap(it)) continue;
            _played.Add(it.Key);
            _nextAt = Time.time + UnityEngine.Random.Range(18f, 40f);
            return;
        }
    }

    private bool PlayMap(MapItem it)
    {
        Vector3 at = PlaceFor(it.Where);
        if (!PlayAt(it.Key, at, 0f)) return false;
        if (it.FollowDelay > 0f) PlayAt(it.Key + "+", at, it.FollowDelay);
        else PlayAt(it.Key + "+", at, 0f);
        if (DirectionStage.Verbose) Debug.Log("[World] 맵 소리 " + it.Key + " (" + it.Space + " " + it.Axis + " " + it.Level + ")");
        return true;
    }

    /// <summary>교실 청각 2 — 교실을 나서는 순간 뒷줄 책상 타격(하루 한 번).</summary>
    private void LeftClassroom()
    {
        if (_played.Contains(ClassExitKey) || (int)NightRun.ShownBand(SpaceId.Classroom_1_3, FearAxis.Auditory) < 2) return;
        if (PlayAt(ClassExitKey, _lastClassroomPos + Vector3.down * 0.8f, 0.15f)) _played.Add(ClassExitKey);
    }

    /// <summary>배치 4 — 그 공간 소품이 미세하게 떠는 소리(작게, 계속).</summary>
    private void Tremble(bool live)
    {
        bool want = live && (int)NightRun.ShownBand(NightRun.CurrentSpace, FearAxis.Layout) >= 4;
        if (_tremble == null)
        {
            if (!want) return;
            float volume;
            AudioClip clip = DirectionSoundTableSO.FindExact(TremoloKey, out volume);
            if (clip == null) return;
            GameObject go = new GameObject("world tremble");
            go.transform.SetParent(transform, false);
            _tremble = go.AddComponent<AudioSource>();
            _tremble.clip = clip;
            _tremble.loop = true;
            _tremble.spatialBlend = 0.7f;
            _tremble.volume = 0f;
            _tremble.minDistance = 1.5f;
            _tremble.maxDistance = 12f;
        }

        Camera cam = Camera.main;
        if (cam != null) _tremble.transform.position = cam.transform.position + cam.transform.right * 2f + Vector3.up * 0.6f;
        float full;
        DirectionSoundTableSO.FindExact(TremoloKey, out full);
        _tremble.volume = Mathf.MoveTowards(_tremble.volume, want ? full : 0f, Time.deltaTime * 0.3f);
        if (_tremble.volume > 0.001f)
        {
            if (!_tremble.isPlaying) _tremble.Play();
        }
        else if (_tremble.isPlaying)
        {
            _tremble.Stop();
        }
    }

    private void OnLampOff(Vector3 at)
    {
        if (!NightRun.IsNightActive || Time.time - _nightStart < 5f) return;   // 밤 시작에 한꺼번에 꺼지는 등은 조용히
        Camera cam = Camera.main;
        if (cam != null && (cam.transform.position - at).sqrMagnitude > 35f * 35f) return;
        PlayAt("map.lamp.off", at, 0f);
    }

    // ── 연출 사건 ───────────────────────────────────────────

    private void OnDirection(DirectionEvent e)
    {
        if (e.Point != Vector3.zero && (e.Kind == DirectionEventKind.RuleCue || (e.Kind == DirectionEventKind.Encounter && e.Phase == DirectionPhase.Confront)))
        {
            _points[e.SourceId] = e.Point;
        }

        if (e.Kind != DirectionEventKind.Encounter || e.SourceId != "E.ToiletBlackout") return;
        if (e.Phase == DirectionPhase.Confront)
        {
            bool cross = (int)NightRun.ShownBand(SpaceId.Toilet, FearAxis.Auditory) >= 2 && (int)NightRun.ShownBand(SpaceId.Toilet, FearAxis.Illuminance) >= 2;
            if (cross && _water == null) _water = StartCoroutine(WaterMoves());
        }
        else if (e.Phase == DirectionPhase.WindowClose || e.Phase == DirectionPhase.Result || e.Phase == DirectionPhase.Aborted)
        {
            if (_water != null) StopCoroutine(_water);
            _water = null;
        }
    }

    /// <summary>청각 2 + 조도 2 — 화장실 소등 동안 물소리가 칸 사이를 옮겨 다닌다.</summary>
    private IEnumerator WaterMoves()
    {
        string[] stalls = { "toilet.stall.inner", "toilet.stall.outer" };
        int i = 0;
        float until = Time.time + 40f;
        while (Time.time < until)
        {
            Vector3 at;
            JudgeTarget t;
            if (JudgeTargetRegistry.TryGet(stalls[i % stalls.Length], out t) && t != null) at = t.AnchorPosition + Vector3.up * 0.3f;
            else at = PlaceFor(Place.Around);
            PlayAt("cross.toilet.water", at, 0f);
            i++;
            yield return new WaitForSeconds(UnityEngine.Random.Range(2.2f, 3.6f));
        }

        _water = null;
    }

    // ── 위반 현장 반응 ───────────────────────────────────────

    private void OnRuleSettled(FinalRuleResult r)
    {
        if (r.Outcome != FinalOutcome.Violated) return;
        string key = "react." + r.RuleId;
        Vector3 at;
        switch (r.RuleId)
        {
            case "C1":
            case "L2":
                at = PointOr(r.RuleId, Place.Around);
                break;
            case "L1":
                at = TargetOr("rule.L1.shelf", Place.Around);
                break;
            case "L5":
                at = PointOr("E.SuitMan", Place.Ahead);
                break;
            case "S2":
                at = Feet();
                break;
            case "K2":
                at = CctvSystem.Active != null ? CctvSystem.Active.ScreenCenter : PlaceFor(Place.Ahead);
                break;
            case "G1":
                StartCoroutine(FollowWhenStopped(key));
                return;
            default:
                return;
        }

        PlayAt(key, at, 0.1f);
    }

    /// <summary>G1 — 달리다 멈추면 뒤에서 몇 걸음이 따라오다 멈춘다.</summary>
    private IEnumerator FollowWhenStopped(string key)
    {
        Camera cam = Camera.main;
        if (cam == null) yield break;
        Vector3 last = cam.transform.position;
        float still = 0f;
        float waited = 0f;
        while (waited < 12f)
        {
            yield return null;
            if (cam == null) yield break;
            waited += Time.deltaTime;
            Vector3 now = cam.transform.position;
            Vector3 d = now - last;
            d.y = 0f;
            last = now;
            still = d.magnitude / Mathf.Max(0.0001f, Time.deltaTime) < 0.3f ? still + Time.deltaTime : 0f;
            if (still >= 0.35f) break;
        }

        PlayAt(key, PlaceFor(Place.Behind), 0f);
    }

    // ── 도우미 ───────────────────────────────────────────────

    private static int GlobalBand(FearAxis axis)
    {
        return NightRun.Shown != null ? (int)NightRun.Shown.GetBand(axis) : 0;
    }

    private Vector3 PointOr(string id, Place fallback)
    {
        Vector3 p;
        return _points.TryGetValue(id, out p) ? p : PlaceFor(fallback);
    }

    private Vector3 TargetOr(string targetId, Place fallback)
    {
        JudgeTarget t;
        return JudgeTargetRegistry.TryGet(targetId, out t) && t != null ? t.AnchorPosition : PlaceFor(fallback);
    }

    private static Vector3 Feet()
    {
        Camera cam = Camera.main;
        return cam != null ? cam.transform.position + Vector3.down * 1.4f : Vector3.zero;
    }

    private static Vector3 PlaceFor(Place where)
    {
        Camera cam = Camera.main;
        if (cam == null) return Vector3.zero;
        Vector3 eye = cam.transform.position;
        Vector3 fwd = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up);
        if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward;
        fwd.Normalize();
        Vector2 r = UnityEngine.Random.insideUnitCircle.normalized;
        Vector3 rand = new Vector3(r.x, 0f, r.y);
        switch (where)
        {
            case Place.Ahead: return eye + fwd * 7f;
            case Place.Behind: return eye - fwd * 6f;
            case Place.Far: return eye + rand * 12f;
            case Place.Low: return eye + rand * 3f + Vector3.down * 1.2f;
            case Place.GuardDoor: return GuardDoor(eye, fwd);
            default: return eye + rand * 4f;
        }
    }

    /// <summary>경비실에서 가장 가까운 문(6m 안). 없으면 등 뒤.</summary>
    private static Vector3 GuardDoor(Vector3 eye, Vector3 fwd)
    {
        float best = 36f;
        Vector3 at = eye - fwd * 3f;
        foreach (DoorHandle d in DoorHandle.All())
        {
            if (!d.IsValid || d.Owner == null) continue;
            Vector3 p = d.Owner.transform.position;
            float sq = (p - eye).sqrMagnitude;
            if (sq >= best) continue;
            best = sq;
            at = p + Vector3.up * 1.4f;
        }

        return at;
    }

    private bool PlayAt(string key, Vector3 at, float delay)
    {
        float volume;
        AudioClip clip = DirectionSoundTableSO.FindExact(key, out volume);
        if (clip == null) return false;
        GameObject go = new GameObject("world " + key);
        go.transform.SetParent(transform, false);
        go.transform.position = at;
        AudioSource s = go.AddComponent<AudioSource>();
        s.playOnAwake = false;
        s.clip = clip;
        s.volume = volume;
        s.spatialBlend = 1f;
        s.dopplerLevel = 0f;
        s.rolloffMode = AudioRolloffMode.Logarithmic;
        s.minDistance = 2f;
        s.maxDistance = 30f;
        if (delay > 0f) s.PlayDelayed(delay);
        else s.Play();
        Destroy(go, delay + clip.length + 0.3f);
        if (DirectionStage.Verbose) Debug.Log("[World] " + key + " ← " + clip.name + " @" + at.ToString("F1"));
        return true;
    }
}

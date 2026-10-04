using System.Collections.Generic;
using NightDuty;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 근무 중 피드백·점검 소리(2026-10-04 사운드 전달본). 근무 씬에 자동으로 선다(<see cref="FlowAutoInstall"/>).
/// 소리는 전부 연출 소리 표(<see cref="DirectionSoundTableSO"/>)의 정확한 키로 찾고, 없으면 조용히 넘어간다. <b>판정과 무관하다</b> — 어떤 신호도 보내지 않는다.
/// <list type="bullet">
/// <item>보고: 보고 가능으로 켜질 때 <c>ui.ready</c>, 보고가 받아들여질 때 <c>ui.confirm</c>(2D).</item>
/// <item>긴장: 강도 3 이상 조우의 대면에 떨리는 현(<c>tension.confront</c>)을 작게 깐다.</item>
/// <item>경고·처벌: 경고 도장이 늘면 <c>punish.stamp</c>, 세 번째 도장으로 처벌이 대기에 들어가면 <c>tablet.buzz</c>.
/// 처벌이 나오면 축별 <c>punish.auditory</c>(속삭임이 귀를 스침) · <c>punish.illuminance</c>(차단기 툭·딸깍) · <c>punish.layout</c>(가지가 어깨를 스침) + <c>punish.hit</c> + 「뚝」 끊김(<c>punish.cut</c>).</item>
/// <item>점검 「가까이」: 항목 자리에서 <c>inspect.&lt;항목&gt;.near</c>(3D, <c>+</c>는 0.6초 뒤 이어서) + 공용 충격음 <c>inspect.near</c>(2D).</item>
/// <item>점검 이상: 이상이 배정된 항목은 보고할 때까지 그 자리에서 <c>inspect.&lt;항목&gt;.loop</c>가 3D로 계속 난다(C-3 천장 사각거림 · H-2 식수대 · H-3 종 웅— · L-3 종이 넘김 · S-3 물방울 · T-2 휴지 뜯기).
/// 이상의 강도(구간)가 높을수록 조금 크다.</item>
/// </list>
/// </summary>
[DisallowMultipleComponent]
public sealed class NightDutySfx : MonoBehaviour
{
    private const float LoopMinDistance = 1.2f;
    private const float LoopMaxDistance = 14f;
    private const float FollowUpSeconds = 0.6f;

    /// <summary>대면 긴장음을 까는 조우 강도(이상).</summary>
    public const int TensionIntensity = 3;

    private sealed class Loop
    {
        public string ItemId;
        public AudioSource Source;
    }

    private readonly Dictionary<string, Loop> _loops = new Dictionary<string, Loop>();
    private InspectionPlan _plan;
    private int _stamps = -1;
    private int _pending = -1;
    private AudioSource _ui;

    /// <summary>지금 근무 씬의 것. 없으면 null.</summary>
    public static NightDutySfx Active { get; private set; }

    /// <summary>지금 울리는 점검 이상 루프의 항목 ID들(디버그·검수).</summary>
    public IEnumerable<string> LoopingItems
    {
        get { return _loops.Keys; }
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
        if (!FlowAutoInstall.IsDutyScene(scene) || FlowAutoInstall.Exists<NightDutySfx>(scene)) return;
        FlowAutoInstall.CreateHost<NightDutySfx>(scene, "NightDutySfx (auto)");
    }

    private void OnEnable()
    {
        Active = this;
        InspectionSensor.ReadyTicked += OnReadyTicked;
        EventBus.InspectionReported += OnReported;
        EventBus.InspectionStartled += OnStartled;
        EventBus.WarningsChanged += OnWarningsChanged;
        EventBus.Punished += OnPunished;
        EventBus.DirectionEmitted += OnDirection;
    }

    private void OnDisable()
    {
        InspectionSensor.ReadyTicked -= OnReadyTicked;
        EventBus.InspectionReported -= OnReported;
        EventBus.InspectionStartled -= OnStartled;
        EventBus.WarningsChanged -= OnWarningsChanged;
        EventBus.Punished -= OnPunished;
        EventBus.DirectionEmitted -= OnDirection;
        StopLoops();
        if (Active == this) Active = null;
    }

    // ── 보고 ─────────────────────────────────────────────────

    private void OnReadyTicked(string itemId)
    {
        Play2D("ui.ready");
    }

    private void OnReported(InspectionReport report)
    {
        if (!report.Accepted) return;
        Play2D("ui.confirm");
        StopLoop(report.ItemId);
    }

    // ── 「가까이」 ───────────────────────────────────────────

    private void OnStartled(string itemId, FearAxis axis)
    {
        Vector3 at;
        if (!ItemPosition(itemId, out at)) at = Ear() + Vector3.forward * 0.6f;
        string key = "inspect." + itemId + ".near";
        PlayAt(key, at, 0f);
        PlayAt(key + "+", at, FollowUpSeconds);
        Play2D("inspect.near");
    }

    // ── 경고·처벌 ───────────────────────────────────────────

    private void OnWarningsChanged(int stamps, int pending)
    {
        bool first = _stamps < 0;
        int oldStamps = _stamps;
        int oldPending = _pending;
        _stamps = stamps;
        _pending = pending;
        if (first || !NightRun.IsNightActive) return;   // 처음 받는 값·재시작 복원은 소리 없이

        if (pending > oldPending) Play2D("tablet.buzz");   // 세 번째 도장 — 도장이 0으로 돌아가며 처벌이 대기에 든다
        else if (stamps > oldStamps) Play2D("punish.stamp");
    }

    private void OnPunished(FearAxis axis)
    {
        Play2D("punish." + CaptureDirector.AxisKey(axis));
        Play2D("punish.hit");
        Play2D("punish.cut");
    }

    // ── 긴장 ─────────────────────────────────────────────────

    /// <summary>강도 3 이상 조우의 대면 — 떨리는 현을 작게 깐다(긴장 클라이맥스, CLX-29).</summary>
    private void OnDirection(DirectionEvent e)
    {
        if (e.Kind != DirectionEventKind.Encounter || e.Phase != DirectionPhase.Confront || e.Intensity < TensionIntensity) return;
        Play2D("tension.confront");
    }

    // ── 점검 이상 루프 ───────────────────────────────────────

    private void Update()
    {
        if (!NightRun.IsNightActive)
        {
            if (_loops.Count > 0) StopLoops();
            _plan = null;
            return;
        }

        InspectionBoard board = NightRun.Inspections;
        if (!ReferenceEquals(board.Plan, _plan))
        {
            StopLoops();
            _plan = board.Plan;
            _stamps = -1;
            _pending = -1;
        }

        if (_plan == null) return;
        IReadOnlyList<InspectionAssignment> rows = _plan.Assignments;
        for (int i = 0; i < rows.Count; i++)
        {
            InspectionAssignment row = rows[i];
            bool want = row.IsAnomaly && board.StateOf(row.Id) == InspectionState.Pending;
            Loop loop;
            bool has = _loops.TryGetValue(row.Id, out loop);
            if (want && !has) StartLoop(row);
            else if (!want && has) StopLoop(row.Id);
        }
    }

    private void StartLoop(InspectionAssignment row)
    {
        float volume;
        AudioClip clip = DirectionSoundTableSO.FindExact("inspect." + row.Id + ".loop", out volume);
        Vector3 at;
        if (clip == null || !ItemPosition(row.Id, out at))
        {
            _loops[row.Id] = new Loop { ItemId = row.Id };   // 소리 없음 — 매 프레임 다시 찾지 않게 빈 칸으로 둔다
            return;
        }

        GameObject go = new GameObject("inspect loop " + row.Id);
        go.transform.SetParent(transform, false);
        go.transform.position = at;
        AudioSource s = go.AddComponent<AudioSource>();
        s.playOnAwake = false;
        s.loop = true;
        s.clip = clip;
        s.spatialBlend = 1f;
        s.dopplerLevel = 0f;
        s.rolloffMode = AudioRolloffMode.Linear;
        s.minDistance = LoopMinDistance;
        s.maxDistance = LoopMaxDistance;
        s.priority = 64;
        s.volume = volume * Mathf.Clamp01(0.55f + 0.15f * (int)row.Intensity);
        s.timeSamples = Random.Range(0, Mathf.Max(1, clip.samples - 1));
        s.Play();
        _loops[row.Id] = new Loop { ItemId = row.Id, Source = s };
        if (DirectionStage.Verbose) Debug.Log("[Sfx] 점검 이상 소리 " + row.Id + " ← " + clip.name + " @" + at.ToString("F1"));
    }

    private void StopLoop(string itemId)
    {
        Loop loop;
        if (string.IsNullOrEmpty(itemId) || !_loops.TryGetValue(itemId, out loop)) return;
        if (loop.Source != null) Destroy(loop.Source.gameObject);
        _loops.Remove(itemId);
    }

    private void StopLoops()
    {
        foreach (Loop loop in _loops.Values)
        {
            if (loop.Source != null) Destroy(loop.Source.gameObject);
        }

        _loops.Clear();
    }

    // ── 도우미 ───────────────────────────────────────────────

    private static bool ItemPosition(string itemId, out Vector3 at)
    {
        at = Vector3.zero;
        InspectionItem item = InspectionCatalog.Find(itemId);
        JudgeTarget target;
        if (item == null || !JudgeTargetRegistry.TryGet(item.TargetId, out target) || target == null) return false;
        at = target.transform.position;
        return true;
    }

    private static Vector3 Ear()
    {
        Camera cam = Camera.main;
        return cam != null ? cam.transform.position : Vector3.zero;
    }

    private void PlayAt(string key, Vector3 at, float delay)
    {
        float volume;
        AudioClip clip = DirectionSoundTableSO.FindExact(key, out volume);
        if (clip == null) return;
        GameObject go = new GameObject("sfx " + key);
        go.transform.SetParent(transform, false);
        go.transform.position = at;
        AudioSource s = go.AddComponent<AudioSource>();
        s.playOnAwake = false;
        s.clip = clip;
        s.volume = volume;
        s.spatialBlend = 1f;
        s.dopplerLevel = 0f;
        s.minDistance = 1.5f;
        s.maxDistance = 20f;
        if (delay > 0f) s.PlayDelayed(delay);
        else s.Play();
        Destroy(go, delay + clip.length + 0.3f);
    }

    /// <summary>몸 가까이에서 나는 소리(태블릿·UI·처벌).</summary>
    public void Play2D(string key)
    {
        float volume;
        AudioClip clip = DirectionSoundTableSO.FindExact(key, out volume);
        if (clip == null) return;
        if (_ui == null)
        {
            _ui = gameObject.AddComponent<AudioSource>();
            _ui.playOnAwake = false;
            _ui.spatialBlend = 0f;
            _ui.priority = 16;
        }

        _ui.PlayOneShot(clip, volume);
        if (DirectionStage.Verbose) Debug.Log("[Sfx] " + key + " ← " + clip.name);
    }
}

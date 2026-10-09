using System.Collections.Generic;
using NightDuty;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 근무 중 피드백·점검 소리(2026-10-04 사운드 전달본). 근무 씬에 자동으로 선다(<see cref="FlowAutoInstall"/>).
/// 소리는 전부 연출 소리 표(<see cref="DirectionSoundTableSO"/>)의 정확한 키로 찾고, 없으면 조용히 넘어간다. <b>판정과 무관하다</b> — 어떤 신호도 보내지 않는다.
/// <list type="bullet">
/// <item>보고: 보고 가능으로 켜질 때 <c>ui.ready</c>, 보고가 받아들여질 때 <c>ui.confirm</c>(2D).</item>
/// <item>58차(민: 「조우 효과음이 아직도 현악기」): 강도 3 이상 조우의 대면에 깔던 떨리는 현(<c>tension.confront</c>, CLX-29 8초)을 뺐다 — 51차에 스팅어만 바꾸고 이 겹을 남겨 두었다. 조우 소리는 <see cref="EncounterImpact"/>의 점프스케어 스팅어뿐.</item>
/// <item>경고·처벌: 경고 도장이 늘면 <c>punish.stamp</c>, 세 번째 도장으로 처벌이 대기에 들어가면 <c>tablet.buzz</c>.
/// 처벌이 나오면 축별 <c>punish.auditory</c>(속삭임이 귀를 스침) · <c>punish.illuminance</c>(차단기 툭·딸깍) · <c>punish.layout</c>(가지가 어깨를 스침) + <c>punish.hit</c> + 「뚝」 끊김(<c>punish.cut</c>).</item>
/// <item>점검 「가까이」: 항목 자리에서 <c>inspect.&lt;항목&gt;.near</c>(3D, <c>+</c>는 0.6초 뒤 이어서) + 공용 충격음 <c>inspect.near</c>.
/// 61차(민: 「쾅 소리가 1일차부터 너무 가까이서 들려 혼란 — 멀리서, 1~2일차엔 배정 안 했으면」): 공용 충격음은 <see cref="StartleImpactFromDay"/>일차부터만,
/// 2D가 아니라 등 뒤 <see cref="StartleImpactDistance"/>m에서 저역만 남겨 낸다(<see cref="PlayFar"/>). 「가까이」 판정(축 +6)과 항목 소리는 그대로.</item>
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
    }

    private void OnDisable()
    {
        InspectionSensor.ReadyTicked -= OnReadyTicked;
        EventBus.InspectionReported -= OnReported;
        EventBus.InspectionStartled -= OnStartled;
        EventBus.WarningsChanged -= OnWarningsChanged;
        EventBus.Punished -= OnPunished;
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
        // 50차(민: scanner beep): 정상은 한 번, 이상은 조금 낮게 두 번(「삐삑」). 보고가 맞았는지는 소리로 알리지 않는다.
        if (report.SaidAnomaly)
        {
            PlayReportBeep(AnomalyBeepPitch, 0f);
            PlayReportBeep(AnomalyBeepPitch, AnomalyBeepGap);
        }
        else
        {
            PlayReportBeep(UnityEngine.Random.Range(0.97f, 1.03f), 0f);
        }

        StopLoop(report.ItemId);
    }

    private const float AnomalyBeepPitch = 0.84f;
    private const float AnomalyBeepGap = 0.13f;

    private void PlayReportBeep(float pitch, float delay)
    {
        float volume;
        AudioClip clip = DirectionSoundTableSO.FindExact("ui.confirm", out volume);
        if (clip == null) return;
        GameObject go = new GameObject("sfx ui.confirm");
        go.transform.SetParent(transform, false);
        AudioSource s = go.AddComponent<AudioSource>();
        s.playOnAwake = false;
        s.clip = clip;
        s.volume = volume;
        s.pitch = pitch;
        s.spatialBlend = 0f;
        s.priority = 16;
        if (delay > 0f) s.PlayDelayed(delay);
        else s.Play();
        Destroy(go, delay + clip.length / Mathf.Max(0.1f, pitch) + 0.2f);
    }

    // ── 「가까이」 ───────────────────────────────────────────

    private void OnStartled(string itemId, FearAxis axis)
    {
        Vector3 at;
        if (!ItemPosition(itemId, out at)) at = Ear() + Vector3.forward * 0.6f;
        string key = "inspect." + itemId + ".near";
        PlayAt(key, at, 0f);
        PlayAt(key + "+", at, FollowUpSeconds);
        if (NightRun.Day >= StartleImpactFromDay) PlayFar("inspect.near", StartleImpactDistance);
    }

    /// <summary>61차: 「가까이」 공용 충격음(쾅)이 나기 시작하는 날.</summary>
    public const int StartleImpactFromDay = 3;

    /// <summary>61차: 「가까이」 공용 충격음이 나는 거리(m, 등 뒤).</summary>
    public const float StartleImpactDistance = 16f;

    /// <summary>61차: 등 뒤(±70°) 먼 곳에서, 벽 너머처럼 저역만 남긴 3D 소리.</summary>
    private void PlayFar(string key, float distance)
    {
        float volume;
        AudioClip clip = DirectionSoundTableSO.FindExact(key, out volume);
        if (clip == null) return;
        Camera cam = Camera.main;
        Vector3 ear = Ear();
        Vector3 back = cam != null ? -Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up) : Vector3.back;
        if (back.sqrMagnitude < 0.01f) back = Vector3.back;
        back = Quaternion.Euler(0f, Random.Range(-70f, 70f), 0f) * back.normalized;

        GameObject go = new GameObject("sfx(먼) " + key);
        go.transform.SetParent(transform, false);
        go.transform.position = ear + back * distance;
        AudioSource s = go.AddComponent<AudioSource>();
        s.playOnAwake = false;
        s.clip = clip;
        s.volume = volume;
        s.spatialBlend = 1f;
        s.dopplerLevel = 0f;
        s.rolloffMode = AudioRolloffMode.Logarithmic;
        s.minDistance = 4f;
        s.maxDistance = 40f;
        go.AddComponent<AudioLowPassFilter>().cutoffFrequency = 1100f;
        s.Play();
        Destroy(go, clip.length + 0.3f);
        if (DirectionStage.Verbose) Debug.Log("[Sfx] " + key + " ← " + clip.name + " (먼 쾅 " + distance + "m)");
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

using System.Collections.Generic;
using NightDuty;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 66차 근접 자동문(민 피드백 7단계 「근접 자동문 — 진선님 에셋」, 2026-10-08 민이 알려 준 김진선님 연출 프리셋).
/// 김진선님 문 프리셋을 씬의 닫힌 문(DoorNarrowSolid) 자리에 밤마다 세운다 — 같은 모형·같은 피벗이라 그대로 겹친다.
/// <list type="bullet">
/// <item><c>HorrorEvent_OpenDoorSlam</c>: 반쯤 열린 문 — 앞 구역에 들어서면 쾅 닫힌다.</item>
/// <item><c>HorrorEvent_DoorSlam</c>: 닫힌 문 — 바라보면 스르르 열리고, 다가가면 쾅 닫힌다.</item>
/// <item>발동은 김진선님 프리셋 그대로(<c>HorrorTriggerZone</c>·<c>HorrorGazeTrigger</c>, 한 번만). 구역은 모두 복도 쪽이다(66차 실측).</item>
/// <item>66차 ③(민: 「과학실의 오른쪽 문이 잘 안 열린다」): 세운 동안 씬 문은 <b>그림과 몸(트리거 아닌 콜라이더)을 함께</b> 숨긴다 —
/// 몸을 남겼더니 쾅 닫힌 뒤 [E] 조준이 진선님 문짝에 먼저 맞아 안내가 안 뜨고, 어쩌다 숨은 씬 문이 맞으면 안 보이는 문만 열려 보이는 문이 길을 막았다.</item>
/// <item>쾅이 끝나면 곧바로 <b>씬 문에 자리를 돌려준다</b>(<see cref="HandBack"/> — 같은 모형·같은 피벗의 닫힌 자세라 티가 나지 않는다). 그때부터 [E]로 평소처럼 연다.
/// 아직 아무 연출도 안 돌았는데 플레이어가 문 앞(<see cref="NearDoor"/>m)에 오면: 닫힌 문(바라보면 열림, 아직 안 봄)은 바로 돌려주고, 열린 문은 쾅을 친다.</item>
/// <item>세운 동안의 문은 H2 「저절로 열림」이 고르지 않는다(<see cref="Covers"/>) — 숨은 씬 문만 열리면 돌려줄 때 어긋난다.</item>
/// <item>1일차는 없음. 2일차부터 하루 한두 곳(<see cref="ScheduleFor"/>). 밤이 바뀌면 거두고 새로 세운다(재시작은 그대로 — 이미 돌려준 문은 씬 문 그대로).</item>
/// </list>
/// 판정과 무관하다(문 판정 신호를 내지 않는다). 근무 씬에 스스로 선다. 씬 파일은 고치지 않는다.
/// </summary>
[DisallowMultipleComponent]
public sealed class ProximityDoors : MonoBehaviour
{
    /// <summary>반쯤 열린 문 → 다가가면 쾅.</summary>
    public const string OpenSlam = "HorrorEvent_OpenDoorSlam";

    /// <summary>바라보면 열림 → 다가가면 쾅.</summary>
    public const string GazeSlam = "HorrorEvent_DoorSlam";

    /// <summary>과학실 앞 복도(식수대 옆) 문.</summary>
    public const string ScienceFront = "Interior/Corridors/DoorNarrowSolid (8)";

    /// <summary>복도 동쪽 끝 과학실 쪽 문.</summary>
    public const string EastScience = "Interior/Corridors/DoorNarrowSolid (9)";

    /// <summary>복도 동쪽 끝 맞은편 문.</summary>
    public const string EastToilet = "Interior/Toilet01/DoorNarrowSolid";

    /// <summary>복도 서쪽 끝(화장실 쪽) 문.</summary>
    public const string WestA = "Interior/Corridors/DoorNarrowSolid (5)";

    /// <summary>복도 서쪽 끝 바깥 문.</summary>
    public const string WestB = "Interior/Corridors/DoorNarrowSolid (2)";

    /// <summary>끄면 세우지 않는다.</summary>
    public static bool Enabled = true;

    /// <summary>66차 ③: 연출이 하나도 안 돈 채 플레이어가 이만큼(m, 수평) 다가오면 정리한다 — [E]가 닿는 거리(1.8m)와 같다.</summary>
    public const float NearDoor = 1.8f;

    /// <summary>66차 ③: 쾅이 끝나고 씬 문에 돌려주기까지(초) — 끝 자세가 한 번은 그려지게.</summary>
    public const float HandBackDelay = 0.3f;

    private sealed class Placed
    {
        public string Preset;
        public string Path;
        public GameObject Instance;
        public Component Door;
        public HorrorEvent Open;
        public HorrorEvent Slam;
        public float SlamEndedAt = -1f;
        public bool Returned;
        public List<Renderer> Hidden = new List<Renderer>();
        public List<Collider> Disabled = new List<Collider>();
    }

    private readonly List<Placed> _placed = new List<Placed>();
    private object _night;
    private HorrorPresetsSO _presets;

    /// <summary>지금 근무 씬의 것. 없으면 null.</summary>
    public static ProximityDoors Active { get; private set; }

    /// <summary>지금 세운 것(「HorrorEvent_OpenDoorSlam@…(8)」). 디버그·검수.</summary>
    public string Summary
    {
        get
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int i = 0; i < _placed.Count; i++)
            {
                if (i > 0) sb.Append(" · ");
                sb.Append(_placed[i].Preset).Append('@').Append(_placed[i].Path);
                if (_placed[i].Returned) sb.Append("(돌려줌)");
            }

            return sb.ToString();
        }
    }

    /// <summary>그날 세울 것(프리셋, 씬 문 경로). 1일차는 비어 있다.</summary>
    public static List<KeyValuePair<string, string>> ScheduleFor(int day)
    {
        List<KeyValuePair<string, string>> list = new List<KeyValuePair<string, string>>();
        switch (day)
        {
            case 1:
                break;
            case 2:
                list.Add(Pair(OpenSlam, ScienceFront));
                break;
            case 3:
                list.Add(Pair(GazeSlam, EastToilet));
                list.Add(Pair(OpenSlam, WestA));
                break;
            case 4:
                list.Add(Pair(GazeSlam, ScienceFront));
                list.Add(Pair(OpenSlam, EastScience));
                break;
            default:
                list.Add(Pair(OpenSlam, EastToilet));
                list.Add(Pair(GazeSlam, WestB));
                break;
        }

        return list;
    }

    private static KeyValuePair<string, string> Pair(string preset, string path)
    {
        return new KeyValuePair<string, string>(preset, path);
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
        if (!FlowAutoInstall.IsDutyScene(scene) || FlowAutoInstall.Exists<ProximityDoors>(scene)) return;
        FlowAutoInstall.CreateHost<ProximityDoors>(scene, "ProximityDoors (auto)");
    }

    private void OnEnable()
    {
        Active = this;
    }

    private void OnDisable()
    {
        Clear();
        if (Active == this) Active = null;
    }

    private void Update()
    {
        object night = NightRun.IsNightActive && NightRun.Inspections != null ? NightRun.Inspections.Plan : null;
        if (!ReferenceEquals(night, _night))
        {
            _night = night;
            Clear();
            if (night != null && Enabled)
            {
                List<KeyValuePair<string, string>> schedule = ScheduleFor(NightRun.Day);
                for (int i = 0; i < schedule.Count; i++) Place(schedule[i].Key, schedule[i].Value);
                if (_placed.Count > 0) Debug.Log("[ProximityDoors] " + NightRun.Day + "일차 근접 문: " + Summary);
            }
        }

        for (int i = 0; i < _placed.Count; i++) Watch(_placed[i]);
    }

    /// <summary>66차 ③: 그 씬 문(벤더 문 컴포넌트가 붙은 오브젝트) 자리에 지금 진선님 문이 서 있는가. 서 있는 동안은 연출이 그 문을 건드리지 않는다.</summary>
    public static bool Covers(Component door)
    {
        if (Active == null || door == null) return false;
        for (int i = 0; i < Active._placed.Count; i++)
        {
            Placed p = Active._placed[i];
            if (!p.Returned && p.Door == door) return true;
        }

        return false;
    }

    /// <summary>66차 ③: 디버그·검수 — 그 문 자리를 지금 돌려준다. 돌려줬으면 true.</summary>
    public bool DebugHandBack(string path)
    {
        for (int i = 0; i < _placed.Count; i++)
        {
            if (_placed[i].Path == path && !_placed[i].Returned)
            {
                HandBack(_placed[i]);
                return true;
            }
        }

        return false;
    }

    private void Watch(Placed p)
    {
        if (p.Returned) return;
        if (p.Instance == null || p.Slam == null)
        {
            HandBack(p);
            return;
        }

        bool openBusy = p.Open != null && p.Open.IsPlaying;
        if (p.Slam.HasPlayed && !p.Slam.IsPlaying)
        {
            if (p.SlamEndedAt < 0f) p.SlamEndedAt = Time.time;
            if (Time.time - p.SlamEndedAt >= HandBackDelay) HandBack(p);
            return;
        }

        if (p.Slam.IsPlaying || openBusy || !PlayerNear(p)) return;

        // 연출이 하나도 돌지 않은 채 문 앞에 왔다(구역을 비켜 옆에서 붙음·바라보지 않고 다가옴).
        if (p.Open != null && !p.Open.HasPlayed) HandBack(p);   // 닫힌 문 그대로 — 씬 문이 이어받는다
        else p.Slam.Play();                                     // 열린 문 — 쾅(끝나면 위에서 돌려준다)
    }

    private static bool PlayerNear(Placed p)
    {
        Camera cam = Camera.main;
        if (cam == null || p.Door == null) return false;
        Vector3 d = cam.transform.position - p.Door.transform.position;
        d.y = 0f;
        return d.sqrMagnitude <= NearDoor * NearDoor;
    }

    /// <summary>66차 ③: 진선님 문을 걷고 씬 문(닫힌 자세)을 되살린다 — 그림·몸 모두.</summary>
    private void HandBack(Placed p)
    {
        if (p.Returned) return;
        p.Returned = true;
        if (p.Instance != null) Destroy(p.Instance);
        p.Instance = null;
        Restore(p);
        Debug.Log("[ProximityDoors] 씬 문에 돌려줌 — " + p.Path);
    }

    private static void Restore(Placed p)
    {
        if (p.Door != null)
        {
            DoorHandle h = DoorHandle.Of(p.Door);
            if (h.IsValid) h.SnapClosed();
        }

        for (int k = 0; k < p.Hidden.Count; k++)
        {
            if (p.Hidden[k] != null) p.Hidden[k].enabled = true;
        }

        for (int k = 0; k < p.Disabled.Count; k++)
        {
            if (p.Disabled[k] != null) p.Disabled[k].enabled = true;
        }

        p.Hidden.Clear();
        p.Disabled.Clear();
    }

    /// <summary>디버그: 그 프리셋을 그 문 자리에 지금 세운다. 세웠으면 true.</summary>
    public bool Place(string preset, string path)
    {
        if (_presets == null) _presets = Resources.Load<HorrorPresetsSO>(HorrorPresetsSO.ResourcePath);
        GameObject prefab = _presets != null ? _presets.Find(preset) : null;
        GameObject door = GameObject.Find("/" + path);
        if (prefab == null || door == null)
        {
            Debug.LogWarning("[ProximityDoors] " + preset + " @" + path + " — " + (prefab == null ? "프리셋 표에 없음(야간근무/연출/진선님 연출 프리셋 표 다시 만들기)" : "씬에 문 없음"));
            return false;
        }

        for (int i = 0; i < _placed.Count; i++)
        {
            if (_placed[i].Path == path) return false;   // 한 문에 하나
        }

        DoorHandle handle = DoorHandle.Of(door.transform);
        Placed p = new Placed { Preset = preset, Path = path, Door = handle.IsValid ? handle.Owner : null };
        if (handle.IsValid) handle.SnapClosed();   // 돌려줄 때의 닫힌 자세와 맞춘다(반쯤 열린 채 시작하는 문 포함)
        foreach (Renderer r in door.GetComponentsInChildren<Renderer>())
        {
            if (!r.enabled) continue;
            r.enabled = false;
            p.Hidden.Add(r);
        }

        // 66차 ③: 동선의 문(정책 「열리는 문」)은 몸도 숨긴다 — [E] 조준과 길막음은 진선님 문짝이 맡는다. 벤더 트리거 상자는 둔다(조준이 무시한다).
        // 쓰지 않는 문(Sealed)은 몸을 남긴다 — 열린 진선님 문으로 막힌 방에 들어가지 못하게(돌려준 뒤에도 어차피 「잠겨 있습니다」).
        bool openable = handle.IsValid && PlayerInteractor.Classify(handle) == DoorPolicySO.Kind.Openable;
        foreach (Collider c in openable ? door.GetComponentsInChildren<Collider>() : new Collider[0])
        {
            if (!c.enabled || c.isTrigger) continue;
            c.enabled = false;
            p.Disabled.Add(c);
        }

        p.Instance = Instantiate(prefab, door.transform.position, door.transform.rotation, transform);
        p.Instance.name = "근접 문 " + preset + " @" + door.name;
        foreach (HorrorEvent e in p.Instance.GetComponentsInChildren<HorrorEvent>(true))
        {
            if (e.name == "Slam") p.Slam = e;
            else if (e.name == "Open") p.Open = e;
        }

        _placed.Add(p);
        return true;
    }

    private void Clear()
    {
        for (int i = 0; i < _placed.Count; i++)
        {
            Placed p = _placed[i];
            if (p.Instance != null) Destroy(p.Instance);
            if (!p.Returned) Restore(p);
        }

        _placed.Clear();
    }
}

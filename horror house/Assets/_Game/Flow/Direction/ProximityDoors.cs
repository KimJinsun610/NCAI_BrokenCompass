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
/// <item>씬 문은 그림만 숨기고 콜라이더는 남긴다 — 열린 문으로 막힌 방에 들어가지 못하게.</item>
/// <item>1일차는 없음. 2일차부터 하루 한두 곳(<see cref="ScheduleFor"/>). 밤이 바뀌면 거두고 새로 세운다(재시작은 그대로 — 이미 쾅 닫혔으면 닫힌 채).</item>
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

    private sealed class Placed
    {
        public string Preset;
        public string Path;
        public GameObject Instance;
        public List<Renderer> Hidden = new List<Renderer>();
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
        if (ReferenceEquals(night, _night)) return;
        _night = night;
        Clear();
        if (night == null || !Enabled) return;
        List<KeyValuePair<string, string>> schedule = ScheduleFor(NightRun.Day);
        for (int i = 0; i < schedule.Count; i++) Place(schedule[i].Key, schedule[i].Value);
        if (_placed.Count > 0) Debug.Log("[ProximityDoors] " + NightRun.Day + "일차 근접 문: " + Summary);
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

        Placed p = new Placed { Preset = preset, Path = path };
        foreach (Renderer r in door.GetComponentsInChildren<Renderer>())
        {
            if (!r.enabled) continue;
            r.enabled = false;
            p.Hidden.Add(r);
        }

        p.Instance = Instantiate(prefab, door.transform.position, door.transform.rotation, transform);
        p.Instance.name = "근접 문 " + preset + " @" + door.name;
        _placed.Add(p);
        return true;
    }

    private void Clear()
    {
        for (int i = 0; i < _placed.Count; i++)
        {
            Placed p = _placed[i];
            if (p.Instance != null) Destroy(p.Instance);
            for (int k = 0; k < p.Hidden.Count; k++)
            {
                if (p.Hidden[k] != null) p.Hidden[k].enabled = true;
            }
        }

        _placed.Clear();
    }
}

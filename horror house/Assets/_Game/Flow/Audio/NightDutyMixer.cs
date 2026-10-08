using System.Collections.Generic;
using NightDuty;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

/// <summary>
/// 67차 소리 믹서(민: 「효과음들의 방향을 정확하게 — 태블릿은 확실하게 왼쪽, 다른 효과음도 확실하게 공간적으로, 특정 연출이 나오면 연출 사운드가 다른 효과음보다
/// 더 강조되게 — 사운드 믹서 기능을 써서 조절하는 방법을 모색」).
/// <list type="bullet">
/// <item><b>버스 다섯</b>(<c>Resources/NightDutyMixer.mixer</c>, 빌더 <c>야간근무/소리/믹서 만들기</c>): 엠비언트 · 세상(문·발소리 밖 효과음·CCTV) · 연출(조우·스팅어·붙잡힘·진선님 연출) · 태블릿(알람·진동·UI) · 몸(심박·숨·발소리).
/// 각 버스 볼륨은 노출 파라미터 <c>Vol_&lt;버스&gt;</c>(dB).</item>
/// <item><b>길 찾기</b>: 0.5초마다 씬의 AudioSource 중 아직 버스가 없는 것을 이름·부모로 갈래 지어 보낸다(<see cref="Classify"/>) — 소리를 만드는 코드 30여 곳을 고치지 않는다.
/// 우리 코드가 직접 정하고 싶으면 <see cref="Route"/>.</item>
/// <item><b>공간감</b>: 세상·연출 버스에서 반쯤 3D(0.01~0.99)인 소리는 완전 3D로(<see cref="SpatialFix"/>) — 「연출 소리 3D 75%」(44차)가 방향을 흐렸다. 도플러는 끈다. 2D로 일부러 낸 스팅어·몸 소리는 그대로.</item>
/// <item><b>강조(덕킹)</b>: 조우가 진행 중이면(<c>NightRun.Tension.Busy</c> — 전조·몹을 세워 둔 동안·대면·마무리) 엠비언트 −8dB · 세상 −6dB · 태블릿 −4dB · 몸 −2dB,
/// 덮치기·스팅어 순간(<see cref="Spotlight"/>)에는 엠비언트 −16 · 세상 −12 · 태블릿 −10 · 몸 −4dB까지. 연출 버스는 0dB 그대로 — 남는 것이 연출 소리다.
/// 내려갈 때 0.12초, 돌아올 때 1.6초.</item>
/// </list>
/// 믹서 에셋이 없으면(빌더를 아직 안 돌림) 길 찾기·덕킹은 쉬고 공간감 바로잡기만 한다. 근무 씬에 자동 설치.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(80)]
public sealed class NightDutyMixer : MonoBehaviour
{
    /// <summary>버스.</summary>
    public enum Bus
    {
        World = 0,
        Ambience = 1,
        Direction = 2,
        Tablet = 3,
        Body = 4
    }

    /// <summary>믹서 에셋 경로(Resources).</summary>
    public const string ResourcePath = "NightDutyMixer";

    /// <summary>버스 이름(믹서 그룹 이름 = 노출 파라미터 접미).</summary>
    public static readonly string[] BusNames = { "World", "Ambience", "Direction", "Tablet", "Body" };

    /// <summary>조우 진행 중 버스별 내림(dB).</summary>
    public static readonly float[] BusyDuck = { -6f, -8f, 0f, -4f, -2f };

    /// <summary>덮치기·스팅어 순간 버스별 내림(dB).</summary>
    public static readonly float[] SpotlightDuck = { -12f, -16f, 0f, -10f, -4f };

    /// <summary>내려갈 때 걸리는 초.</summary>
    public const float AttackSeconds = 0.12f;

    /// <summary>돌아올 때 걸리는 초.</summary>
    public const float ReleaseSeconds = 1.6f;

    /// <summary>길 찾기 간격(초).</summary>
    public const float ScanInterval = 0.5f;

    private static NightDutyMixer s_active;
    private static AudioMixer s_mixer;
    private static AudioMixerGroup[] s_groups;
    private static bool s_loaded;

    private readonly float[] _current = new float[5];
    private readonly HashSet<int> _seen = new HashSet<int>();
    private float _spotlightUntil;
    private float _scanAt;

    /// <summary>지금 근무 씬의 것. 없으면 null.</summary>
    public static NightDutyMixer Active
    {
        get { return s_active; }
    }

    /// <summary>믹서 에셋을 찾았는지.</summary>
    public static bool HasMixer
    {
        get
        {
            Load();
            return s_mixer != null;
        }
    }

    /// <summary>버스 그룹(없으면 null).</summary>
    public static AudioMixerGroup GroupOf(Bus bus)
    {
        Load();
        return s_groups != null ? s_groups[(int)bus] : null;
    }

    /// <summary>그 소스를 그 버스로 보낸다(믹서가 없으면 아무 일 없음). 공간감 바로잡기도 한다.</summary>
    public static void Route(AudioSource source, Bus bus)
    {
        if (source == null) return;
        AudioMixerGroup g = GroupOf(bus);
        if (g != null) source.outputAudioMixerGroup = g;
        SpatialFix(source, bus);
        if (s_active != null) s_active._seen.Add(source.GetInstanceID());
    }

    /// <summary>덮치기·스팅어 순간 — 연출 버스만 남기고 나머지를 깊게 내린다(<paramref name="seconds"/>초 동안).</summary>
    public static void Spotlight(float seconds)
    {
        if (s_active == null) return;
        s_active._spotlightUntil = Mathf.Max(s_active._spotlightUntil, Time.unscaledTime + Mathf.Max(0.1f, seconds));
    }

    /// <summary>
    /// 이름·부모로 버스를 고른다. 순서가 중요하다 — 태블릿·몸·연출을 먼저 보고, 엠비언트 재생기 아래라도 「Stinger」는 연출이다.
    /// </summary>
    public static Bus Classify(AudioSource source)
    {
        if (source == null) return Bus.World;
        Transform t = source.transform;
        string n = t.name;

        if (Starts(n, "body ") || Starts(n, "footstep") || source.GetComponent<PlayerFootsteps>() != null || source.GetComponent<BodyMeter>() != null) return Bus.Body;
        if (source.GetComponentInParent<PlayerTablet>(true) != null || source.GetComponent<TabletBridge>() != null || n == "sfx ui.confirm") return Bus.Tablet;
        if (n == "Stinger" || Starts(n, "연출 소리") || Starts(n, "capture") || Starts(n, "시체 낙하") || Starts(n, "sfx 모형")) return Bus.Direction;

        for (Transform p = t; p != null; p = p.parent)
        {
            if (p.GetComponent<HorrorEvent>() != null || p.GetComponent<UnityEngine.Playables.PlayableDirector>() != null) return Bus.Direction;
            if (p.GetComponent<StandInExit>() != null || p.GetComponent<DirectionStage>() != null || p.GetComponent<CaptureDirector>() != null) return Bus.Direction;
            if (p.GetComponent<AmbiencePlayer>() != null) return Bus.Ambience;
        }

        return Bus.World;
    }

    /// <summary>세상·연출 버스의 반쯤 3D 소리를 완전 3D로, 도플러 끔. 2D(0)는 일부러 낸 것이라 그대로.</summary>
    public static void SpatialFix(AudioSource source, Bus bus)
    {
        if (source == null || (bus != Bus.World && bus != Bus.Direction)) return;
        if (source.spatialBlend > 0.01f && source.spatialBlend < 0.99f) source.spatialBlend = 1f;
        if (source.spatialBlend >= 0.99f)
        {
            source.dopplerLevel = 0f;
            source.spread = 0f;
        }
    }

    /// <summary>그 버스가 지금 낼 목표 dB(조우·강조에 따라).</summary>
    public static float TargetDb(Bus bus, bool busy, bool spotlight)
    {
        if (spotlight) return SpotlightDuck[(int)bus];
        if (busy) return BusyDuck[(int)bus];
        return 0f;
    }

    private static bool Starts(string s, string prefix)
    {
        return s != null && s.StartsWith(prefix, System.StringComparison.Ordinal);
    }

    private static void Load()
    {
        if (s_loaded) return;
        s_loaded = true;
        s_mixer = Resources.Load<AudioMixer>(ResourcePath);
        if (s_mixer == null) return;
        s_groups = new AudioMixerGroup[BusNames.Length];
        for (int i = 0; i < BusNames.Length; i++)
        {
            AudioMixerGroup[] found = s_mixer.FindMatchingGroups("Master/" + BusNames[i]);
            if (found == null || found.Length == 0) found = s_mixer.FindMatchingGroups(BusNames[i]);
            s_groups[i] = found != null && found.Length > 0 ? found[0] : null;
        }
    }

    // ── 설치 ─────────────────────────────────────────────

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Install()
    {
        s_active = null;
        s_loaded = false;
        s_mixer = null;
        s_groups = null;
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
        if (!FlowAutoInstall.IsDutyScene(scene) || FlowAutoInstall.Exists<NightDutyMixer>(scene)) return;
        FlowAutoInstall.CreateHost<NightDutyMixer>(scene, "NightDutyMixer (auto)");
    }

    private void OnEnable()
    {
        s_active = this;
        Load();
    }

    private void OnDisable()
    {
        if (s_active == this) s_active = null;
        if (s_mixer == null) return;
        for (int i = 0; i < BusNames.Length; i++) s_mixer.SetFloat("Vol_" + BusNames[i], 0f);
    }

    private void Update()
    {
        float now = Time.unscaledTime;
        if (now >= _scanAt)
        {
            _scanAt = now + ScanInterval;
            Scan();
        }

        if (s_mixer == null) return;
        bool busy = NightRun.IsNightActive && NightRun.Tension != null && NightRun.Tension.Busy;
        bool spot = now < _spotlightUntil;
        float dt = Time.unscaledDeltaTime;
        for (int i = 0; i < BusNames.Length; i++)
        {
            float target = TargetDb((Bus)i, busy, spot);
            float span = Mathf.Abs(SpotlightDuck[i]) + 0.01f;
            float rate = target < _current[i] ? span / AttackSeconds : span / ReleaseSeconds;
            _current[i] = Mathf.MoveTowards(_current[i], target, rate * dt);
            s_mixer.SetFloat("Vol_" + BusNames[i], _current[i]);
        }
    }

    private void Scan()
    {
        AudioSource[] all = FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            AudioSource s = all[i];
            if (s == null || !_seen.Add(s.GetInstanceID())) continue;
            Bus bus = Classify(s);
            if (s_groups != null && s.outputAudioMixerGroup == null)
            {
                AudioMixerGroup g = s_groups[(int)bus];
                if (g != null) s.outputAudioMixerGroup = g;
            }

            SpatialFix(s, bus);
        }

        if (_seen.Count > 4096) _seen.Clear();   // 파괴된 소스의 ID가 쌓이지 않게(다시 훑어도 같은 결과라 괜찮다)
    }
}

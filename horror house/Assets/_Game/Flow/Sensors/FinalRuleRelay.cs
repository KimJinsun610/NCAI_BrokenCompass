using System.Collections.Generic;
using NightDuty;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 새 수칙 판정(<see cref="FinalRuleBook"/>)이 쓰는 플레이어 상태 발신기(2026-10-01). 센서 허브의 0.1초 샘플마다:
/// <list type="number">
/// <item>자세 — 발밑 위치와 수평 방향(<see cref="JudgeSignal.Pose"/>). H1·H4·C3·S1·S5·T3·L1·L4가 쓴다.</item>
/// <item>달리기 — 수평 속도가 <see cref="RunSpeed"/>를 넘나드는 순간만(<see cref="JudgeSignal.Run"/>, G1). 걷기 2·달리기 5 m/s 사이로 잡았다.</item>
/// <item>비춤 — 손전등이 켜져 있으면 원뿔 15°·8m 안에서 가려지지 않은 <c>rule.</c> 기준점(<see cref="JudgeSignal.Beam"/>, S3·L3·L5).
///   빈 샘플도 보낸다(연속 비춤의 끊김을 재야 한다).</item>
/// <item>CCTV 시청 — 모니터를 보는 동안 현재 채널(<see cref="JudgeSignal.CctvView"/>, K2).</item>
/// </list>
/// 채널을 넘기면 <see cref="JudgeSignal.Channel"/>(K1)을 보낸다. 채널 ID는 <c>cctv.ch&lt;번호&gt;</c>.
/// <para>새 편성이 꺼져 있으면(<see cref="NightRun.FinalRules"/> == null) 아무것도 보내지 않는다. 씬에 놓지 않아도 근무 씬에서 플레이어 루트에 붙는다.</para>
/// </summary>
[DisallowMultipleComponent]
public sealed class FinalRuleRelay : MonoBehaviour
{
    /// <summary>달리기로 보는 수평 속도(m/s).</summary>
    public const float RunSpeed = 3.5f;

    private const string RulePrefix = "rule.";

    // 연출이 잠깐 여는 신호 구역(소등한 과학실 전체 = science.dark, 없던 문 앞 = classroom.phantomdoor). 씬의 SpaceZones에 없는 구역.
    private static readonly Dictionary<string, Bounds> s_runtimeZones = new Dictionary<string, Bounds>();
    private readonly HashSet<string> _insideRuntime = new HashSet<string>();
    private readonly List<string> _scratch = new List<string>();

    /// <summary>연출이 신호 구역을 연다(같은 ID면 상자를 바꾼다). 플레이어가 안에 있으면 다음 샘플에 ZoneEntered가 간다.</summary>
    public static void SetRuntimeZone(string id, Bounds box)
    {
        if (string.IsNullOrEmpty(id)) return;
        s_runtimeZones[id] = box;
    }

    /// <summary>연출이 신호 구역을 닫는다. 안에 있었으면 다음 샘플에 ZoneExited가 간다.</summary>
    public static void ClearRuntimeZone(string id)
    {
        if (string.IsNullOrEmpty(id)) return;
        s_runtimeZones.Remove(id);
    }

    /// <summary>지금 열린 런타임 구역 ID들(디버그).</summary>
    public static IEnumerable<string> RuntimeZoneIds
    {
        get { return s_runtimeZones.Keys; }
    }

    private readonly List<JudgeTarget> _owners = new List<JudgeTarget>();
    private readonly List<KeyValuePair<string, JudgeTarget>> _beamTargets = new List<KeyValuePair<string, JudgeTarget>>();
    private bool _targetsDirty = true;
    private bool _running;
    private bool _hasLast;
    private Vector3 _last;
    private Camera _cam;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Install()
    {
        s_runtimeZones.Clear();
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
        if (!FlowAutoInstall.IsDutyScene(scene)) return;
        if (FlowAutoInstall.Exists<FinalRuleRelay>(scene)) return;

        Camera cam = FlowAutoInstall.FindCamera(scene);
        if (cam == null) return;

        FinalRuleRelay relay = cam.transform.root.gameObject.AddComponent<FinalRuleRelay>();
        relay._cam = cam;
    }

    private void OnEnable()
    {
        PlayerSensors.Sampled += OnSampled;
        CctvSystem.ChannelChanged += OnChannelChanged;
        JudgeTargetRegistry.Changed += OnTargetsChanged;
        _targetsDirty = true;
        _hasLast = false;
    }

    private void OnDisable()
    {
        PlayerSensors.Sampled -= OnSampled;
        CctvSystem.ChannelChanged -= OnChannelChanged;
        JudgeTargetRegistry.Changed -= OnTargetsChanged;
    }

    private void OnTargetsChanged()
    {
        _targetsDirty = true;
    }

    private static bool Live
    {
        get { return NightRun.IsNightActive && !NightRun.IsCaptured && NightRun.FinalRules != null; }
    }

    private void OnSampled(float step)
    {
        if (!Live)
        {
            _hasLast = false;
            if (_running) _running = false;
            return;
        }

        PlayerSensors hub = PlayerSensors.Active;
        Transform root = hub != null && hub.PlayerRoot != null ? hub.PlayerRoot : transform;

        // ① 자세
        Vector3 feet = root.position;
        NightRun.Send(JudgeSignal.Pose(feet, root.eulerAngles.y));

        // ①′ 런타임 신호 구역
        UpdateRuntimeZones(feet);

        // ② 달리기(바뀔 때만)
        if (_hasLast && step > 0f)
        {
            float speed = SensingRules.HorizontalDistance(feet, _last) / step;
            bool running = speed > RunSpeed;
            if (running != _running)
            {
                _running = running;
                NightRun.Send(JudgeSignal.Run(running));
            }
        }

        _last = feet;
        _hasLast = true;

        // ③ 비춤
        NightRun.Send(JudgeSignal.Beam(LitTarget(root), step));

        // ④ CCTV 시청
        CctvSystem cctv = CctvSystem.Active;
        if (cctv != null && cctv.IsViewing)
        {
            NightRun.Send(JudgeSignal.CctvView(ChannelId(cctv.CurrentChannel), step));
        }
    }

    private void UpdateRuntimeZones(Vector3 feet)
    {
        foreach (KeyValuePair<string, Bounds> kv in s_runtimeZones)
        {
            Bounds b = kv.Value;
            bool inside = feet.x >= b.min.x && feet.x <= b.max.x && feet.z >= b.min.z && feet.z <= b.max.z
                          && feet.y >= b.min.y - 1f && feet.y <= b.max.y + 1f;
            if (inside && _insideRuntime.Add(kv.Key)) NightRun.Send(JudgeSignal.Target(SignalKind.ZoneEntered, kv.Key));
            else if (!inside && _insideRuntime.Remove(kv.Key)) NightRun.Send(JudgeSignal.Target(SignalKind.ZoneExited, kv.Key));
        }

        _scratch.Clear();
        foreach (string id in _insideRuntime)
        {
            if (!s_runtimeZones.ContainsKey(id)) _scratch.Add(id);
        }

        for (int i = 0; i < _scratch.Count; i++)
        {
            _insideRuntime.Remove(_scratch[i]);
            NightRun.Send(JudgeSignal.Target(SignalKind.ZoneExited, _scratch[i]));
        }
    }

    private void OnChannelChanged(int channel)
    {
        if (!Live) return;
        NightRun.Send(JudgeSignal.Channel(ChannelId(channel)));
    }

    /// <summary>CCTV 채널 ID.</summary>
    public static string ChannelId(int channel)
    {
        return "cctv.ch" + channel;
    }

    private string LitTarget(Transform root)
    {
        FlashlightRelay light = FlashlightRelay.Active;
        if (light == null || !light.IsOn) return string.Empty;

        Camera cam = ResolveCamera();
        if (cam == null) return string.Empty;

        if (_targetsDirty) RebuildTargets();

        Vector3 origin = cam.transform.position;
        Vector3 forward = cam.transform.forward;
        float range = NightRun.BeamRange;   // 56차: 배터리 10% 아래면 5m
        string best = string.Empty;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < _beamTargets.Count; i++)
        {
            JudgeTarget t = _beamTargets[i].Value;
            if (t == null || !t.isActiveAndEnabled) continue;

            Vector3 point = t.transform.position;
            if (!SensingRules.InBeam(origin, forward, point, range)) continue;

            float d = Vector3.Distance(origin, point);
            if (d >= bestDistance || !Visible(origin, point, t.transform, root)) continue;

            best = _beamTargets[i].Key;
            bestDistance = d;
        }

        return best;
    }

    /// <summary>
    /// 가림 검사. 첫 충돌이 기준점의 소품(기준점의 부모 아래) 또는 플레이어 자신이면 보이는 것으로 본다 —
    /// 기준점에는 콜라이더가 없어 선분이 소품 콜라이더에 먼저 닿는 것이 정상이다.
    /// </summary>
    private static bool Visible(Vector3 origin, Vector3 point, Transform target, Transform player)
    {
        RaycastHit hit;
        if (!Physics.Linecast(origin, point, out hit, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return true;

        Transform h = hit.collider.transform;
        Transform prop = target.parent != null ? target.parent : target;
        return h.IsChildOf(prop) || (player != null && h.IsChildOf(player));
    }

    private void RebuildTargets()
    {
        _targetsDirty = false;
        _owners.Clear();
        _beamTargets.Clear();
        JudgeTargetRegistry.CollectOwners(_owners);
        for (int i = 0; i < _owners.Count; i++)
        {
            JudgeTarget t = _owners[i];
            if (t == null) continue;
            IReadOnlyList<string> ids = t.Ids;
            for (int k = 0; k < ids.Count; k++)
            {
                if (ids[k].StartsWith(RulePrefix)) _beamTargets.Add(new KeyValuePair<string, JudgeTarget>(ids[k], t));
            }
        }
    }

    private Camera ResolveCamera()
    {
        if (_cam != null) return _cam;
        _cam = GetComponentInChildren<Camera>(true);
        if (_cam == null) _cam = Camera.main;
        return _cam;
    }
}

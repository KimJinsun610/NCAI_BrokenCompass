using System.Collections.Generic;
using NightDuty;
using UnityEngine;

/// <summary>
/// 달려오는 발소리(E.Footsteps) 연출 — 소리가 <b>지나가는 길</b>을 따라 복도 형광등이 순서대로 깜빡인다(사용자: 「소리만 나는 조우를 보이게」).
/// <list type="bullet">
/// <item>발소리 원본(<c>E.Footsteps.confront</c>)을 한 점에서 틀지 않고 움직이는 소리 소스로 틀어, 소리가 플레이어 뒤에서 앞(그리고 지나쳐 6m)까지 달린다.</item>
/// <item>길 가까이(수평 <see cref="LampReach"/>m)의 복도 천장 등(<c>LampFluo*</c>의 라이트·발광)을 소리 머리가 지나가는 순간에 짧게 두 번 끈다. 방 안 등·이미 꺼진 등은 건드리지 않는다.</item>
/// <item>판정과 무관한 순수 연출이다. 광과민 옵션(<see cref="InspectionAnomalies.PhotosensitiveSafe"/>)이면 등은 건드리지 않고 소리만 움직인다.</item>
/// </list>
/// 끝나거나 조우가 거둬지면(<see cref="Cancel"/>) 등은 원래 상태로 돌아간다.
/// </summary>
[DefaultExecutionOrder(1000)]
public sealed class FootstepLampRun : MonoBehaviour
{
    /// <summary>길에서 이만큼(m, 수평) 안의 등을 깜빡인다.</summary>
    public const float LampReach = 4.5f;

    /// <summary>소리가 플레이어를 지나쳐 더 달리는 거리(m).</summary>
    public const float PassBeyond = 6f;

    /// <summary>소리 출발점이 플레이어 뒤로 떨어진 거리(m).</summary>
    public const float RunUp = 15f;

    /// <summary>한 등이 꺼져 있는 시간(초) — 두 번 끈다.</summary>
    public const float OffSeconds = 0.14f;
    public const float OnGapSeconds = 0.07f;

    /// <summary>달리는 시간 범위(초) — 발소리 길이에 맞추되 이 안으로.</summary>
    public const float MinRunSeconds = 2.6f;
    public const float MaxRunSeconds = 5.5f;

    private sealed class Lamp
    {
        public Light Light;
        public Renderer[] Glow;
        public MaterialPropertyBlock[] Saved;
        public float At;        // 소리 머리가 이 등을 지나는 시각(초)
        public bool Done;
        public bool Off;
        public bool WasEnabled;
        public float PhaseStart;
        public int Phase;       // 0 대기 · 1 꺼짐1 · 2 켜짐 · 3 꺼짐2
    }

    private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

    private readonly List<Lamp> _lamps = new List<Lamp>();
    private MaterialPropertyBlock _black;
    private AudioSource _source;
    private Vector3 _from;
    private Vector3 _to;
    private float _duration;
    private float _t;
    private bool _ended;

    /// <summary>
    /// 발소리 연출을 시작한다. <paramref name="from"/> = 소리가 출발하는 점(플레이어 뒤), <paramref name="feet"/> = 플레이어 발밑.
    /// 소리 원본이 없으면 null(부르는 쪽이 옛 한 점 소리로 물러난다).
    /// </summary>
    public static FootstepLampRun Begin(string soundName, Vector3 from, Vector3 feet, SpaceZones zones)
    {
        float volume = 1f;
        AudioClip clip = Resources.Load<AudioClip>("Direction/" + soundName);
        if (clip == null) clip = DirectionSoundTableSO.Find(soundName, out volume);
        if (clip == null) return null;

        Vector3 dir = feet - from;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.25f) return null;
        dir.Normalize();

        GameObject go = new GameObject("발소리 달림");
        FootstepLampRun run = go.AddComponent<FootstepLampRun>();
        run._duration = Mathf.Clamp(clip.length, MinRunSeconds, MaxRunSeconds);

        // 복도는 축 방향(x 또는 z)이다 — 소리가 달려올 길을 네 축 방향 중 복도 천장 등이 가장 많이 놓인 쪽으로 고른다(동률이면 원래 방향에 가까운 쪽).
        // 출발점 = 그 축 위 플레이어 뒤 RunUp m, 끝 = 플레이어 앞 PassBeyond m.
        if (!InspectionAnomalies.PhotosensitiveSafe)
        {
            Vector3[] cands = { Vector3.right, Vector3.left, Vector3.forward, Vector3.back };
            Vector3 best = cands[0];
            float bestScore = -1f;
            for (int i = 0; i < cands.Length; i++)
            {
                run.SetPath(feet, cands[i]);
                run._lamps.Clear();
                run.CollectLamps(zones, feet.y, Vector3.Distance(run._from, run._to));
                float score = run._lamps.Count + Vector3.Dot(cands[i], dir) * 0.5f;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = cands[i];
                }
            }

            run._lamps.Clear();
            run.SetPath(feet, best);
            run.CollectLamps(zones, feet.y, Vector3.Distance(run._from, run._to));
        }
        else
        {
            run.SetPath(feet, dir);
        }

        run.StartSound(clip, volume);
        return run;
    }

    private void SetPath(Vector3 feet, Vector3 dir)
    {
        _from = new Vector3(feet.x, feet.y + 0.1f, feet.z) - dir * RunUp;
        _to = new Vector3(feet.x, feet.y + 0.1f, feet.z) + dir * PassBeyond;
    }

    private void StartSound(AudioClip clip, float volume)
    {
        transform.position = _from;
        _source = gameObject.AddComponent<AudioSource>();
        _source.clip = clip;
        _source.volume = volume;
        _source.spatialBlend = 0.85f;
        _source.rolloffMode = AudioRolloffMode.Logarithmic;
        _source.minDistance = 4f;
        _source.maxDistance = 40f;
        _source.dopplerLevel = 0f;
        _source.priority = 8;
        _source.Play();
    }

    private void CollectLamps(SpaceZones zones, float floorY, float length)
    {
        List<Bounds> rooms = new List<Bounds>();
        if (zones != null)
        {
            SpaceId[] ids = { SpaceId.Classroom_1_1, SpaceId.Classroom_1_3, SpaceId.ScienceRoom, SpaceId.Toilet, SpaceId.Library, SpaceId.SecurityRoom };
            for (int i = 0; i < ids.Length; i++)
            {
                Bounds b;
                if (zones.TryGetSpaceBox(ids[i], out b)) rooms.Add(new Bounds(b.center + Vector3.up, new Vector3(b.size.x + 0.5f, b.size.y + 4f, b.size.z + 0.5f)));
            }
        }

        Vector3 axis = _to - _from;
        axis.y = 0f;
        float axisLen = axis.magnitude;
        if (axisLen < 0.1f) return;
        axis /= axisLen;

        // 복도 천장 등은 실시간 빛이 달린 것이 드물고(약 5개), 대부분은 구운 빛 + 형광관 발광뿐이다 — 그래서 라이트가 아니라 형광등 몸체(렌더러)를 기준으로 모은다.
        // 같은 등의 LOD 렌더러는 한 묶음(루트 = 부모가 LampFluo면 그 부모, 아니면 자기 자신). 실시간 라이트가 있으면 같이 끈다.
        Dictionary<Transform, List<Renderer>> byRoot = new Dictionary<Transform, List<Renderer>>();
        foreach (Renderer r in FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (!r.name.StartsWith("LampFluo") || r is ParticleSystemRenderer) continue;
            Transform root = r.transform.parent != null && r.transform.parent.name.StartsWith("LampFluo") ? r.transform.parent : r.transform;
            if (root.name.Contains("_OFF")) continue;   // 꺼 둔 등
            List<Renderer> list;
            if (!byRoot.TryGetValue(root, out list)) byRoot[root] = list = new List<Renderer>();
            list.Add(r);
        }

        foreach (KeyValuePair<Transform, List<Renderer>> kv in byRoot)
        {
            Transform lamp = kv.Key;
            if (lamp.GetComponentInParent<FlashlightRelay>() != null || lamp.GetComponentInParent<CctvSystem>() != null) continue;
            Vector3 p = kv.Value[0].bounds.center;
            if (p.y < floorY + 2.5f || p.y > floorY + 5.5f) continue;   // 이 층의 천장 등만(위층 매달린 등 제외)

            bool inRoom = false;
            for (int i = 0; i < rooms.Count && !inRoom; i++) inRoom = rooms[i].Contains(p);
            if (inRoom) continue;

            Vector3 rel = p - _from;
            rel.y = 0f;
            float along = Vector3.Dot(rel, axis);
            if (along < -1f || along > axisLen + 1f) continue;
            float side = (rel - axis * along).magnitude;
            if (side > LampReach) continue;

            Light light = null;
            foreach (Light l in lamp.GetComponentsInChildren<Light>(true))
            {
                if (l.enabled && l.type != LightType.Directional && !l.bakingOutput.isBaked) { light = l; break; }
            }

            Renderer[] glow = kv.Value.ToArray();
            _lamps.Add(new Lamp
            {
                Light = light,
                Glow = glow,
                Saved = new MaterialPropertyBlock[glow.Length],
                At = Mathf.Clamp01(along / axisLen) * _duration,
            });
        }

        _lamps.Sort((a, b) => a.At.CompareTo(b.At));
        _black = new MaterialPropertyBlock();
        _black.SetColor(EmissionId, Color.black);
    }

    /// <summary>찾은 등 수(시험·검수).</summary>
    public int LampCount
    {
        get { return _lamps.Count; }
    }

    /// <summary>끝났는지(시험·검수).</summary>
    public bool Ended
    {
        get { return _ended; }
    }

    private void Update()
    {
        if (_ended) return;
        if (!NightRun.IsNightActive || NightRun.IsCaptured)
        {
            Cancel();
            return;
        }

        _t += Time.deltaTime;
        float w = Mathf.Clamp01(_t / _duration);
        transform.position = Vector3.Lerp(_from, _to, w);
        if (_t >= _duration && !_source.isPlaying) Cancel();
    }

    private void LateUpdate()
    {
        if (_ended) return;
        for (int i = 0; i < _lamps.Count; i++)
        {
            Lamp l = _lamps[i];
            if (l.Done) continue;
            if (l.Phase == 0)
            {
                if (_t >= l.At) Step(l, 1, true);
            }
            else if (l.Phase == 1 && _t - l.PhaseStart >= OffSeconds) Step(l, 2, false);
            else if (l.Phase == 2 && _t - l.PhaseStart >= OnGapSeconds) Step(l, 3, true);
            else if (l.Phase == 3 && _t - l.PhaseStart >= OffSeconds)
            {
                Step(l, 4, false);
                l.Done = true;
            }
        }
    }

    private void Step(Lamp l, int phase, bool off)
    {
        if (off && !l.Off) SetOff(l);
        else if (!off && l.Off) SetOn(l);
        l.Phase = phase;
        l.PhaseStart = _t;
    }

    private void SetOff(Lamp l)
    {
        l.Off = true;
        if (l.Light != null)
        {
            l.WasEnabled = l.Light.enabled;
            l.Light.enabled = false;
        }
        for (int i = 0; i < l.Glow.Length; i++)
        {
            Renderer r = l.Glow[i];
            if (r == null) continue;
            MaterialPropertyBlock prev = new MaterialPropertyBlock();
            r.GetPropertyBlock(prev);
            l.Saved[i] = prev;
            r.SetPropertyBlock(_black);
        }
    }

    private void SetOn(Lamp l)
    {
        l.Off = false;
        if (l.Light != null) l.Light.enabled = l.WasEnabled;
        for (int i = 0; i < l.Glow.Length; i++)
        {
            Renderer r = l.Glow[i];
            if (r == null) continue;
            MaterialPropertyBlock prev = l.Saved[i];
            r.SetPropertyBlock(prev != null && !prev.isEmpty ? prev : null);
            l.Saved[i] = null;
        }
    }

    /// <summary>중단·끝 — 등을 모두 원래대로, 소리를 멈추고 스스로 지운다.</summary>
    public void Cancel()
    {
        if (_ended) return;
        _ended = true;
        for (int i = 0; i < _lamps.Count; i++)
        {
            if (_lamps[i].Off) SetOn(_lamps[i]);
        }

        if (_source != null) _source.Stop();
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        // 씬이 먼저 내려가도 등이 꺼진 채 남지 않게.
        if (_ended) return;
        _ended = true;
        for (int i = 0; i < _lamps.Count; i++)
        {
            if (_lamps[i].Off) SetOn(_lamps[i]);
        }
    }
}

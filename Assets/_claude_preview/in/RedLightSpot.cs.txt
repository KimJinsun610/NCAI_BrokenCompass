using System.Collections.Generic;
using NightDuty;
using UnityEngine;

/// <summary>
/// 52차 C4 붉은 등(민: 「유저들은 손전등을 무조건 켜고 다니니 끄라고 정해 두고, 빨간 등에 변칙성을 더해서 맵 곳곳에 랜덤하게 하나를 정하고 나오게」).
/// <b>66차: 초록 등</b>(민: 「조도 축이 오르면 전체 조명이 미세하게 붉어지고, 수칙의 빨간 불빛은 초록으로」) — 색은 <see cref="CueColor"/>. 클래스·단서 이름(cue.redlight)은 그대로 둔다.
/// <list type="bullet">
/// <item>C4(「붉은 불빛 아래에서는 손전등을 끄십시오.」)가 덱에 있는 밤, 근무 공간(복도·교실 둘·과학실·도서관·화장실)의 형광등 하나를 무작위로 골라 붉게 켠다(밤 시작에 정함, 재시작해도 같은 자리).</item>
/// <item>그 등 아래 구역(가로세로 4m)에 들어서면 판정 단서 <c>cue.redlight</c>를 보내고, 나서면 끝 단서 — 판정(<c>LightOffJudge</c>)은 그동안 손전등이 1.5초 넘게 켜져 있으면 위반.</item>
/// <item>처음 들어설 때 그 자리에서 <c>C4.cue</c> 소리. 붉은 빛은 느리게 숨 쉬듯 흔들린다.</item>
/// </list>
/// DirectionStage가 붙인다. 씬은 고치지 않는다(런타임에만 세운다).
/// </summary>
[DisallowMultipleComponent]
public sealed class RedLightSpot : MonoBehaviour
{
    /// <summary>판정 구역 크기(m).</summary>
    public static readonly Vector3 ZoneSize = new Vector3(4f, 3.2f, 4f);

    private static readonly SpaceId[] Spaces =
    {
        SpaceId.Corridor, SpaceId.Classroom_1_1, SpaceId.Classroom_1_3, SpaceId.ScienceRoom, SpaceId.Library, SpaceId.Toilet
    };

    private static RedLightSpot s_active;

    /// <summary>빛이 닿는 거리(m).</summary>
    public const float LightRange = 4.2f;

    /// <summary>66차: C4 수칙 등의 색(초록). 교실 단서 물들임(DirectionStage C4)도 이 색.</summary>
    public static readonly Color CueColor = new Color(0.12f, 1f, 0.3f);

    /// <summary>66차: 등 재질 바탕색(어두운 초록).</summary>
    private static readonly Color CueBase = new Color(0.04f, 0.3f, 0.08f);

    private object _night;
    private GameObject _root;
    private Light _light;
    private Bounds _zone;
    private bool _inside;
    private bool _sounded;
    private float _base;

    /// <summary>지금 살아 있는 것.</summary>
    public static RedLightSpot Active
    {
        get { return s_active; }
    }

    /// <summary>오늘 붉은 등 자리(없으면 null).</summary>
    public Vector3? Spot
    {
        get { return _root != null ? _root.transform.position : (Vector3?)null; }
    }

    private void OnEnable()
    {
        s_active = this;
        EventBus.NightRestarted += OnRestarted;
        EventBus.DayEnded += OnDayEnded;
    }

    private void OnDisable()
    {
        EventBus.NightRestarted -= OnRestarted;
        EventBus.DayEnded -= OnDayEnded;
        Clear();
        if (s_active == this) s_active = null;
    }

    private void OnRestarted(RestartResult r)
    {
        _inside = false;   // 판정 책이 에피소드를 되돌렸다 — 다시 들어서면 단서를 새로 보낸다
    }

    private void OnDayEnded(DaySummary s)
    {
        Clear();
        _night = null;
    }

    private void Update()
    {
        bool want = NightRun.ProgramEnabled && NightRun.IsNightActive && NightRun.Program != null && NightRun.Program.Has("C4");
        if (!want)
        {
            if (_root != null) Clear();
            return;
        }

        object night = NightRun.Inspections != null ? NightRun.Inspections.Plan : null;
        if (!ReferenceEquals(night, _night))
        {
            Clear();
            _night = night;
            Pick();
        }

        if (_light == null) return;
        _light.intensity = _base * (0.82f + 0.18f * Mathf.Sin(Time.time * 1.7f) + 0.06f * Mathf.PerlinNoise(Time.time * 5f, 0.3f));

        PlayerSensors hub = PlayerSensors.Active;
        if (hub == null || hub.PlayerRoot == null) return;
        // 판정 구간에만 단서를 낸다 — 출근(00:00~00:16)·이완에 들어서 있던 채 판정이 열리면 그때 단서를 보낸다.
        bool inside = NightRun.IsJudgingNow && _zone.Contains(hub.PlayerRoot.position + Vector3.up * 0.3f);
        if (inside == _inside) return;
        _inside = inside;
        if (inside)
        {
            NightRun.Send(JudgeSignal.Cue(FinalCues.RedLight, _root.transform.position));
            if (!_sounded)
            {
                _sounded = true;
                DirectionStage.PlaySound("C4.cue", _root.transform.position);
            }
        }
        else
        {
            NightRun.Send(JudgeSignal.CueEnd(FinalCues.RedLight));
        }
    }

    private void Pick()
    {
        SpaceZones zones = FindAnyObjectByType<SpaceZones>();
        if (zones == null) return;

        List<Bounds> boxes = new List<Bounds>();
        for (int i = 0; i < Spaces.Length; i++)
        {
            Bounds b;
            if (zones.TryGetSpaceBox(Spaces[i], out b)) boxes.Add(b);
        }

        List<Renderer> lamps = new List<Renderer>();
        foreach (MeshRenderer r in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
        {
            string n = r.name;
            if (!n.StartsWith("LampFluo") || n.Contains("Broken") || n.Contains("_LOD1") || n.Contains("_LOD2") || !r.gameObject.activeInHierarchy) continue;
            Vector3 c = r.bounds.center;
            // 61차(민 스크린샷: 복도에 붉은 등이 서면 경비실 벽·천장이 붉게 물들었다): 빛이 닿는 거리 안에 경비실이 있는 등은 쓰지 않는다.
            if (GuardRoomColliders.Room.SqrDistance(c) < (LightRange + 0.5f) * (LightRange + 0.5f)) continue;
            for (int i = 0; i < boxes.Count; i++)
            {
                if (!boxes[i].Contains(c)) continue;
                lamps.Add(r);
                break;
            }
        }

        if (lamps.Count == 0)
        {
            Debug.LogWarning("[RedLightSpot] 초록 등으로 쓸 형광등을 찾지 못했습니다.");
            return;
        }

        Renderer lamp = lamps[Random.Range(0, lamps.Count)];
        Bounds lb = lamp.bounds;
        Vector3 under = new Vector3(lb.center.x, lb.min.y - 0.05f, lb.center.z);

        _root = new GameObject("C4 초록 등");
        _root.transform.position = under;

        GameObject lightGo = new GameObject("초록 빛");
        lightGo.transform.SetParent(_root.transform, false);
        lightGo.transform.localPosition = Vector3.down * 0.25f;
        _light = lightGo.AddComponent<Light>();
        _light.type = LightType.Point;
        _light.color = CueColor;
        // 53차 플레이 점검: 6.5m · 3.2는 복도 한 토막을 통째로 물들여 「붉은 불빛 아래」가 어디인지 흐려졌다 — 등 밑 웅덩이로 줄인다.
        _light.range = LightRange;
        _base = 0.85f;   // 57차(민: 「붉은 조명은 밝기를 좀 줄여야」): 1.5 → 0.85
        _light.intensity = _base;
        // 61차: 그림자를 켠다 — 그림자 없는 점광은 벽을 뚫고 옆방(경비실)을 물들였다.
        _light.shadows = LightShadows.Soft;
        _light.shadowNearPlane = 0.1f;
        _light.renderMode = LightRenderMode.ForcePixel;

        // 53차(민 스크린샷: 매달린 등 아래 붉은 판이 허공에 떠 있었다): 판을 따로 세우지 않고 등 자체(LOD 형제 포함)의 재질을 붉게 빛나게 바꾼다.
        TintLamp(lamp);

        // 바닥: 등 바로 밑에서 아래로(천장 판을 바닥으로 잡지 않게 — 등 아래 0.3m에서 쏜다).
        float floor = under.y - 3f;
        Transform player = PlayerSensors.Active != null ? PlayerSensors.Active.PlayerRoot : null;
        float best = float.MaxValue;
        foreach (RaycastHit fh in Physics.RaycastAll(under + Vector3.down * 0.3f, Vector3.down, 8f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (player != null && fh.collider.transform.IsChildOf(player)) continue;
            if (fh.distance < best)
            {
                best = fh.distance;
                floor = fh.point.y;
            }
        }
        _zone = new Bounds(new Vector3(under.x, floor + ZoneSize.y * 0.5f, under.z), ZoneSize);
        _inside = false;
        _sounded = false;
        Debug.Log("[RedLightSpot] 오늘 초록 등 — " + lamp.name + " " + under.ToString("F1") + " (후보 " + lamps.Count + ")");
    }

    private readonly List<KeyValuePair<Renderer, Material[]>> _tinted = new List<KeyValuePair<Renderer, Material[]>>();

    private void TintLamp(Renderer lamp)
    {
        List<Renderer> rs = new List<Renderer>();
        LODGroup lod = lamp.GetComponentInParent<LODGroup>();
        if (lod != null)
        {
            foreach (LOD l in lod.GetLODs())
            {
                foreach (Renderer r in l.renderers)
                {
                    if (r != null && !rs.Contains(r)) rs.Add(r);
                }
            }
        }

        if (!rs.Contains(lamp)) rs.Add(lamp);
        Color red = CueColor;
        foreach (Renderer r in rs)
        {
            Material[] old = r.sharedMaterials;
            Material[] neo = new Material[old.Length];
            for (int i = 0; i < old.Length; i++)
            {
                if (old[i] == null) continue;
                Material m = new Material(old[i]);
                m.name = old[i].name + " (C4 초록 등)";
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", CueBase);
                if (m.HasProperty("_EmissionColor"))
                {
                    m.EnableKeyword("_EMISSION");
                    m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                    m.SetColor("_EmissionColor", red * 1.4f);   // 57차: 3 → 1.4
                }

                neo[i] = m;
            }

            _tinted.Add(new KeyValuePair<Renderer, Material[]>(r, old));
            r.sharedMaterials = neo;
        }
    }

    private void Clear()
    {
        for (int i = 0; i < _tinted.Count; i++)
        {
            if (_tinted[i].Key != null) _tinted[i].Key.sharedMaterials = _tinted[i].Value;
        }

        _tinted.Clear();
        if (_inside) NightRun.Send(JudgeSignal.CueEnd(FinalCues.RedLight));
        _inside = false;
        if (_root != null) Destroy(_root);
        _root = null;
        _light = null;
    }
}

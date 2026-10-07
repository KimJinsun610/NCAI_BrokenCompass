using System.Collections.Generic;
using NightDuty;
using UnityEngine;

/// <summary>
/// 52차 C4 붉은 등(민: 「유저들은 손전등을 무조건 켜고 다니니 끄라고 정해 두고, 빨간 등에 변칙성을 더해서 맵 곳곳에 랜덤하게 하나를 정하고 나오게」).
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
            for (int i = 0; i < boxes.Count; i++)
            {
                if (!boxes[i].Contains(c)) continue;
                lamps.Add(r);
                break;
            }
        }

        if (lamps.Count == 0)
        {
            Debug.LogWarning("[RedLightSpot] 붉은 등으로 쓸 형광등을 찾지 못했습니다.");
            return;
        }

        Renderer lamp = lamps[Random.Range(0, lamps.Count)];
        Bounds lb = lamp.bounds;
        Vector3 under = new Vector3(lb.center.x, lb.min.y - 0.05f, lb.center.z);

        _root = new GameObject("C4 붉은 등");
        _root.transform.position = under;

        GameObject lightGo = new GameObject("붉은 빛");
        lightGo.transform.SetParent(_root.transform, false);
        lightGo.transform.localPosition = Vector3.down * 0.25f;
        _light = lightGo.AddComponent<Light>();
        _light.type = LightType.Point;
        _light.color = new Color(1f, 0.1f, 0.06f);
        _light.range = 6.5f;
        _base = 3.2f;
        _light.intensity = _base;
        _light.shadows = LightShadows.None;
        _light.renderMode = LightRenderMode.ForcePixel;

        // 등 아랫면을 붉게 덮는 얇은 판(멀리서도 「붉은 등」으로 보이게).
        GameObject glow = GameObject.CreatePrimitive(PrimitiveType.Cube);
        glow.name = "붉은 등 판";
        Collider gc = glow.GetComponent<Collider>();
        if (gc != null) Destroy(gc);
        glow.transform.SetParent(_root.transform, false);
        glow.transform.localScale = new Vector3(Mathf.Max(0.2f, lb.size.x * 0.95f), 0.02f, Mathf.Max(0.2f, lb.size.z * 0.95f));
        Shader sh = Shader.Find("Universal Render Pipeline/Unlit");
        if (sh == null) sh = Shader.Find("Unlit/Color");
        Material m = new Material(sh);
        Color red = new Color(1f, 0.12f, 0.08f);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", red);
        if (m.HasProperty("_Color")) m.SetColor("_Color", red);
        Renderer gr = glow.GetComponent<Renderer>();
        gr.sharedMaterial = m;
        gr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

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
        Debug.Log("[RedLightSpot] 오늘 붉은 등 — " + lamp.name + " " + under.ToString("F1") + " (후보 " + lamps.Count + ")");
    }

    private void Clear()
    {
        if (_inside) NightRun.Send(JudgeSignal.CueEnd(FinalCues.RedLight));
        _inside = false;
        if (_root != null) Destroy(_root);
        _root = null;
        _light = null;
    }
}

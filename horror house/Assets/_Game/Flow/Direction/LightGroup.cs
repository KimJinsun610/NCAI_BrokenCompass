using System.Collections.Generic;
using NightDuty;
using UnityEngine;

/// <summary>
/// 공간 하나의 실시간 조명 묶음(최종 기획서 「조명」 — JudgeLights 그룹이 비어 있어 런타임에 모은다, 2026-10-01).
/// 그 공간의 <see cref="SpaceZones"/> 상자 안에 있는 <b>실시간</b> 라이트(베이크·혼합 제외)와 같은 자리의 <c>LampFluo</c> 발광 렌더러를 모은다.
/// 씬 파일은 고치지 않는다. 발광은 공유 머티리얼 대신 <see cref="MaterialPropertyBlock"/>으로 끈다.
/// </summary>
public sealed class LightGroup
{
    private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

    private readonly List<Light> _lights = new List<Light>();
    private readonly List<bool> _wasEnabled = new List<bool>();
    private readonly List<Color> _color = new List<Color>();
    private readonly List<float> _intensity = new List<float>();
    private readonly List<Renderer> _lamps = new List<Renderer>();
    private MaterialPropertyBlock _block;

    /// <summary>공간.</summary>
    public SpaceId Space { get; private set; }

    /// <summary>꺼져 있는지.</summary>
    public bool IsOff { get; private set; }

    /// <summary>붉게 물들어 있는지.</summary>
    public bool IsTinted { get; private set; }

    /// <summary>모은 라이트 수.</summary>
    public int LightCount
    {
        get { return _lights.Count; }
    }

    /// <summary>그 공간의 조명을 모은다. 공간 상자를 모르면 빈 묶음.</summary>
    public static LightGroup Collect(SpaceId space, SpaceZones zones)
    {
        LightGroup g = new LightGroup { Space = SpaceIds.Canonical(space) };
        Bounds box;
        if (zones == null || !zones.TryGetSpaceBox(space, out box)) return g;

        // 천장 등은 상자 위(천장)에 있으므로 높이는 넉넉히 본다.
        Bounds wide = new Bounds(box.center + Vector3.up * 1f, new Vector3(box.size.x + 0.5f, box.size.y + 4f, box.size.z + 0.5f));

        foreach (Light l in Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (l.type == LightType.Directional || l.bakingOutput.isBaked) continue;
            if (l.GetComponentInParent<FlashlightRelay>() != null) continue;
            if (l.GetComponentInParent<CctvSystem>() != null) continue;
            if (!wide.Contains(l.transform.position)) continue;
            g._lights.Add(l);
        }

        foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (!r.name.StartsWith("LampFluo")) continue;
            if (!wide.Contains(r.bounds.center)) continue;
            g._lamps.Add(r);
        }

        return g;
    }

    /// <summary>끈다(이미 꺼져 있으면 아무 일 없음).</summary>
    public void TurnOff()
    {
        if (IsOff) return;
        IsOff = true;
        _wasEnabled.Clear();
        for (int i = 0; i < _lights.Count; i++)
        {
            Light l = _lights[i];
            _wasEnabled.Add(l != null && l.enabled);
            if (l != null) l.enabled = false;
        }

        if (_block == null) _block = new MaterialPropertyBlock();
        for (int i = 0; i < _lamps.Count; i++)
        {
            Renderer r = _lamps[i];
            if (r == null) continue;
            r.GetPropertyBlock(_block);
            _block.SetColor(EmissionId, Color.black);
            r.SetPropertyBlock(_block);
        }
    }

    /// <summary>다시 켠다.</summary>
    public void TurnOn()
    {
        if (!IsOff) return;
        IsOff = false;
        for (int i = 0; i < _lights.Count && i < _wasEnabled.Count; i++)
        {
            if (_lights[i] != null) _lights[i].enabled = _wasEnabled[i];
        }

        for (int i = 0; i < _lamps.Count; i++)
        {
            if (_lamps[i] != null) _lamps[i].SetPropertyBlock(null);
        }
    }

    /// <summary>붉게 물들인다(C4 「붉은 불빛」).</summary>
    public void Tint(Color color, float intensityScale)
    {
        if (IsTinted) return;
        IsTinted = true;
        _color.Clear();
        _intensity.Clear();
        for (int i = 0; i < _lights.Count; i++)
        {
            Light l = _lights[i];
            _color.Add(l != null ? l.color : Color.white);
            _intensity.Add(l != null ? l.intensity : 1f);
            if (l == null) continue;
            l.color = color;
            l.intensity *= intensityScale;
        }
    }

    /// <summary>물들인 것을 되돌린다.</summary>
    public void Untint()
    {
        if (!IsTinted) return;
        IsTinted = false;
        for (int i = 0; i < _lights.Count && i < _color.Count; i++)
        {
            if (_lights[i] == null) continue;
            _lights[i].color = _color[i];
            _lights[i].intensity = _intensity[i];
        }
    }

    /// <summary>모두 되돌린다.</summary>
    public void Restore()
    {
        Untint();
        TurnOn();
    }
}

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 66차: 외곽선 묶음 — 이 오브젝트를 외곽선 뿌리로 요청하면(<see cref="InteractionOutline.Request"/>) 자식 대신 <see cref="Members"/>를 그린다.
/// 씬 소품 여러 개를 점검 대상 하나로 묶을 때(<see cref="RuntimeInspectTargets"/>) 쓴다. 소품을 옮기거나 부모를 바꾸지 않는다.
/// </summary>
[DisallowMultipleComponent]
public sealed class OutlineGroup : MonoBehaviour
{
    private readonly List<Renderer> _members = new List<Renderer>();
    private Renderer[] _array = new Renderer[0];

    /// <summary>그릴 렌더러(LOD0만).</summary>
    public Renderer[] Members
    {
        get { return _array; }
    }

    /// <summary>소품 하나의 LOD0 렌더러를 더한다.</summary>
    public void AddProp(Transform prop)
    {
        if (prop == null) return;
        HashSet<Renderer> skip = new HashSet<Renderer>();
        foreach (LODGroup lod in prop.GetComponentsInChildren<LODGroup>(true))
        {
            LOD[] lods = lod.GetLODs();
            for (int i = 1; i < lods.Length; i++)
            {
                foreach (Renderer r in lods[i].renderers)
                {
                    if (r != null) skip.Add(r);
                }
            }
        }

        foreach (Renderer r in prop.GetComponentsInChildren<Renderer>(true))
        {
            if (skip.Contains(r) || r.name.StartsWith("Inspect ")) continue;
            if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue;
            if (!_members.Contains(r)) _members.Add(r);
        }

        _array = _members.ToArray();
    }

    /// <summary>렌더러 하나를 바꾼다(이상 연출이 원본을 숨기고 대역을 세울 때). 없으면 더한다.</summary>
    public void Replace(Renderer from, Renderer to)
    {
        int i = from != null ? _members.IndexOf(from) : -1;
        if (i >= 0)
        {
            if (to != null) _members[i] = to;
            else _members.RemoveAt(i);
        }
        else if (to != null && !_members.Contains(to))
        {
            _members.Add(to);
        }

        _array = _members.ToArray();
    }
}

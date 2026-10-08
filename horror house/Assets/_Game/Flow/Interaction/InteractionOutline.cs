using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 상호작용 외곽선(2026-10-03 민 요청). <b>지금 상호작용할 수 있는 것</b>에만 얇은 외곽선을 그린다 —
/// 보고 가능해진 점검 대상(<see cref="InspectionSensor.Focus"/>), 겨눈 CCTV 모니터, 겨눈 경비실 전화기(끝낼 수 있으면 진하게, 점검이 남았으면 흐리게).
/// <list type="bullet">
/// <item><b>요청은 매 프레임.</b> 그리고 싶은 쪽이 <c>Update</c>에서 <see cref="Request"/>를 부른다. 부르지 않은 프레임부터 사라진다(0.15초 페이드).</item>
/// <item><b>씬을 건드리지 않는다.</b> 렌더러의 머티리얼을 바꾸지 않고, 대상의 메시를 <see cref="Graphics.RenderMesh"/>로
/// <b>플레이어 카메라에만</b> 두 번 더 그린다 — 먼저 마스크(<c>NightDuty/InteractionOutlineMask</c>, 스텐실만 찍음), 그다음 뒤집은 껍질(<c>NightDuty/InteractionOutline</c>)을
/// 그 스텐실 바깥에만. 그래서 다이얼·버튼 같은 안쪽 모서리 없이 실루엣 선만 남는다. CCTV·태블릿 화면에는 나오지 않는다.</item>
/// <item>LOD가 있으면 LOD0만, 꺼진 렌더러·파티클은 건너뛴다. 스킨 메시는 그 프레임 자세를 굽는다.</item>
/// </list>
/// 판정과 무관하다 — 이 컴포넌트가 없어도 상호작용은 그대로 된다.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(200)]
public sealed class InteractionOutline : MonoBehaviour
{
    private const string MaterialPath = "InteractionOutline";
    private const string MaskMaterialPath = "InteractionOutlineMask";
    private const float FadeSeconds = 0.15f;

    private static InteractionOutline s_active;
    private static readonly Dictionary<Transform, float> s_requested = new Dictionary<Transform, float>();

    private readonly Dictionary<Transform, float> _alpha = new Dictionary<Transform, float>();
    private readonly Dictionary<Transform, Renderer[]> _renderers = new Dictionary<Transform, Renderer[]>();
    private readonly List<Transform> _scratch = new List<Transform>();
    private readonly Dictionary<SkinnedMeshRenderer, Mesh> _baked = new Dictionary<SkinnedMeshRenderer, Mesh>();
    private readonly Dictionary<MeshRenderer, int[]> _submeshes = new Dictionary<MeshRenderer, int[]>();
    private MaterialPropertyBlock _block;
    private Material _material;
    private Material _mask;
    private bool _missingWarned;

    private static readonly int AlphaId = Shader.PropertyToID("_Alpha");

    /// <summary>
    /// 이 프레임에 <paramref name="target"/>(과 그 자식)의 외곽선을 그려 달라고 한다. null이면 무시.
    /// <paramref name="strength"/>(0~1)는 선의 진하기 — 겨누었지만 아직 쓸 수 없는 것(점검이 남은 전화기)은 흐리게.
    /// 같은 프레임에 여러 번 부르면 가장 진한 것.
    /// </summary>
    public static void Request(Transform target, float strength = 1f)
    {
        if (target == null || !Application.isPlaying)
        {
            return;
        }

        float prev;
        strength = Mathf.Clamp01(strength);
        if (!s_requested.TryGetValue(target, out prev) || strength > prev) s_requested[target] = strength;
        if (s_active == null)
        {
            // hideFlags를 붙이지 않는다 — DontSave는 플레이를 꺼도 오브젝트를 남긴다(CLAUDE.md §5.5-23).
            s_active = new GameObject("InteractionOutline (auto)").AddComponent<InteractionOutline>();
        }
    }

    /// <summary>지금 외곽선이 (조금이라도) 그려지는 대상 수. 시험용.</summary>
    public static int VisibleCount
    {
        get { return s_active != null ? s_active._alpha.Count : 0; }
    }

    /// <summary>그 대상 외곽선의 지금 진하기(0 = 안 그림). 시험용.</summary>
    public static float StrengthOf(Transform target)
    {
        float a;
        return s_active != null && target != null && s_active._alpha.TryGetValue(target, out a) ? a : 0f;
    }

    /// <summary>그 대상의 외곽선이 지금 그려지고 있는가(페이드 중 포함). 시험용.</summary>
    public static bool IsShown(Transform target)
    {
        return s_active != null && target != null && s_active._alpha.ContainsKey(target);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        s_active = null;
        s_requested.Clear();
    }

    private void OnDestroy()
    {
        if (s_active == this)
        {
            s_active = null;
        }

        foreach (Mesh m in _baked.Values)
        {
            if (m != null) Destroy(m);
        }

        _baked.Clear();
    }

    private void LateUpdate()
    {
        float step = Time.unscaledDeltaTime / FadeSeconds;

        // 페이드: 요청된 것은 1로, 아닌 것은 0으로.
        foreach (Transform t in s_requested.Keys)
        {
            if (t != null && !_alpha.ContainsKey(t)) _alpha[t] = 0f;
        }

        _scratch.Clear();
        _scratch.AddRange(_alpha.Keys);
        for (int i = 0; i < _scratch.Count; i++)
        {
            Transform t = _scratch[i];
            if (t == null)
            {
                _alpha.Remove(t);
                _renderers.Remove(t);
                continue;
            }

            float want;
            if (!s_requested.TryGetValue(t, out want)) want = 0f;
            float a = Mathf.MoveTowards(_alpha[t], want, step);
            if (a <= 0f)
            {
                _alpha.Remove(t);
                continue;
            }

            _alpha[t] = a;
        }

        s_requested.Clear();

        if (_alpha.Count == 0)
        {
            return;
        }

        Camera cam = Camera.main;
        Material mat = ResolveMaterial();
        if (cam == null || mat == null || _mask == null)
        {
            return;
        }

        RenderParams maskRp = new RenderParams(_mask)
        {
            camera = cam,
            layer = 0,
            shadowCastingMode = ShadowCastingMode.Off,
            receiveShadows = false,
        };

        if (_block == null) _block = new MaterialPropertyBlock();

        foreach (KeyValuePair<Transform, float> kv in _alpha)
        {
            if (!kv.Key.gameObject.activeInHierarchy) continue;
            _block.SetFloat(AlphaId, kv.Value);
            RenderParams rp = new RenderParams(mat)
            {
                camera = cam,
                layer = 0,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                matProps = _block,
            };

            Renderer[] rends = RenderersOf(kv.Key);
            for (int i = 0; i < rends.Length; i++)
            {
                Draw(maskRp, rends[i], true);   // 스킨 메시는 여기서 한 번 굽는다. 렌더 큐가 앞(+49)이라 모든 껍질보다 먼저 그려진다.
                Draw(rp, rends[i], false);
            }
        }
    }

    private void Draw(RenderParams rp, Renderer r, bool bake)
    {
        if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) return;

        MeshRenderer mr = r as MeshRenderer;
        if (mr != null)
        {
            MeshFilter mf = mr.GetComponent<MeshFilter>();
            Mesh mesh = mf != null ? mf.sharedMesh : null;
            if (mesh == null) return;
            Matrix4x4 m = r.localToWorldMatrix;
            int[] subs = SubmeshesOf(mr, mesh);
            for (int i = 0; i < subs.Length; i++) Graphics.RenderMesh(rp, mesh, subs[i], m);
            return;
        }

        SkinnedMeshRenderer sr = r as SkinnedMeshRenderer;
        if (sr != null && sr.sharedMesh != null)
        {
            Mesh baked;
            if (!_baked.TryGetValue(sr, out baked) || baked == null)
            {
                baked = new Mesh { name = sr.name + " (outline)" };
                _baked[sr] = baked;
            }

            // 6000.3에서 BakeMesh(mesh, true)는 스케일이 빠진 정점을 준다(CLAUDE.md 21차 ⓔ) — false로 굽고 회전·위치만 곱한다.
            if (bake) sr.BakeMesh(baked, false);
            Matrix4x4 m = Matrix4x4.TRS(sr.transform.position, sr.transform.rotation, Vector3.one);
            int count = DrawnSubmeshes(r, baked);
            for (int s = 0; s < count; s++) Graphics.RenderMesh(rp, baked, s, m);
        }
    }

    /// <summary>
    /// 이 메시 렌더러가 실제로 그리는 서브메시들. <b>정적 배칭</b>이면 <c>sharedMesh</c>가 씬 전체를 합친 메시(「Combined Mesh (root: scene)」,
    /// 서브메시 수백 개, 정점은 월드 좌표, <c>localToWorldMatrix</c> = 단위)라 다 그리면 근처 다른 물체까지 선이 생긴다(2026-10-03 실측: 소화기 → 젖은 바닥 표지판).
    /// 그 렌더러 몫의 시작 서브메시는 공개 API가 없으므로, <b>렌더러 경계 안에 드는 연속 서브메시 묶음</b>(머티리얼 수만큼) 중 합친 경계가 렌더러 경계와 가장 닮은 것을 고른다.
    /// </summary>
    private int[] SubmeshesOf(MeshRenderer r, Mesh mesh)
    {
        int[] cached;
        if (_submeshes.TryGetValue(r, out cached)) return cached;

        int mats = Mathf.Max(1, r.sharedMaterials.Length);
        if (!r.isPartOfStaticBatch)
        {
            cached = new int[Mathf.Min(mesh.subMeshCount, mats)];
            for (int i = 0; i < cached.Length; i++) cached[i] = i;
        }
        else
        {
            Bounds want = r.bounds;
            Bounds box = want;
            box.Expand(0.05f);
            int best = -1;
            float bestScore = float.MaxValue;
            for (int start = 0; start + mats <= mesh.subMeshCount; start++)
            {
                bool inside = true;
                Bounds union = mesh.GetSubMesh(start).bounds;
                for (int k = 0; k < mats && inside; k++)
                {
                    Bounds sb = mesh.GetSubMesh(start + k).bounds;
                    inside = box.Contains(sb.min) && box.Contains(sb.max);
                    union.Encapsulate(sb);
                }

                if (!inside) continue;
                float score = (union.center - want.center).sqrMagnitude + (union.size - want.size).sqrMagnitude;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = start;
                }
            }

            cached = new int[best < 0 ? 0 : mats];
            for (int i = 0; i < cached.Length; i++) cached[i] = best + i;
        }

        _submeshes[r] = cached;
        return cached;
    }

    /// <summary>
    /// 스킨 메시가 그리는 서브메시 수 = min(서브메시, 머티리얼).
    /// </summary>
    private static int DrawnSubmeshes(Renderer r, Mesh mesh)
    {
        int mats = r.sharedMaterials.Length;
        return Mathf.Min(mesh.subMeshCount, Mathf.Max(1, mats));
    }

    private Renderer[] RenderersOf(Transform root)
    {
        // 66차: 소품 여러 개를 한 점검 대상으로 묶은 것(세면대 넷·의자 둘·플라스크 선반) — 묶음이 정한 렌더러. 이상 연출이 대역으로 바꿀 수 있어 캐시하지 않는다.
        OutlineGroup group = root.GetComponent<OutlineGroup>();
        if (group != null) return group.Members;

        Renderer[] cached;
        if (_renderers.TryGetValue(root, out cached)) return cached;

        HashSet<Renderer> skip = new HashSet<Renderer>();
        foreach (LODGroup g in root.GetComponentsInChildren<LODGroup>(true))
        {
            LOD[] lods = g.GetLODs();
            for (int i = 1; i < lods.Length; i++)
            {
                foreach (Renderer lr in lods[i].renderers)
                {
                    if (lr != null) skip.Add(lr);
                }
            }
        }

        List<Renderer> list = new List<Renderer>();
        foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
        {
            if (skip.Contains(r)) continue;
            if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue;
            list.Add(r);
        }

        cached = list.ToArray();
        _renderers[root] = cached;
        return cached;
    }

    private Material ResolveMaterial()
    {
        if (_material != null && _mask != null) return _material;
        _material = Resources.Load<Material>(MaterialPath);
        _mask = Resources.Load<Material>(MaskMaterialPath);
        if ((_material == null || _mask == null) && !_missingWarned)
        {
            _missingWarned = true;
            Debug.LogWarning("[외곽선] Resources/" + MaterialPath + "·" + MaskMaterialPath + " 머티리얼이 없습니다. 외곽선을 그리지 않습니다(메뉴 「야간근무/경비실/전화기 놓기」가 만든다).", this);
        }

        return _material;
    }
}

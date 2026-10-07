using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// S-3 「수도는 잠겨 있습니다」의 눈에 보이는 이상(57차 민: 「물 이펙트도 필요해 보여. 시각적으로 확인할 수 있게」).
/// 수도꼭지 입에서 개수대 바닥까지 — 실 같은 물줄기(구간이 오를수록 굵고 물방울·튐이 잦다).
/// 바닥에 닿는 자리에 작은 파문이 번진다. 물은 손전등에 반짝이게 매끈한 반투명이고 아주 약하게만 스스로 빛난다(어두운 과학실에서 가까이 가야 보이게).
/// 판정과 무관 — <see cref="InspectionAnomalies"/>가 이상 편성일 때 세우고 거둘 때 통째로 지운다.
/// </summary>
[DisallowMultipleComponent]
public sealed class FaucetWater : MonoBehaviour
{
    private const float Gravity = 9.81f;
    private const float DropSize = 0.011f;

    private sealed class Drop
    {
        public Transform T;
        public float Speed;
        public bool Live;
    }

    private sealed class Ripple
    {
        public Transform T;
        public Material Mat;
        public float Age;
        public bool Live;
    }

    private readonly List<Drop> _drops = new List<Drop>();
    private readonly List<Ripple> _ripples = new List<Ripple>();
    private readonly List<Material> _materials = new List<Material>();
    private Material _water;
    private Vector3 _mouth;
    private float _basinY;
    private int _band;
    private float _nextDrop;
    private float _nextRipple;
    private Transform _stream;
    private float _streamWidth;
    private Texture2D _ring;
    private Texture2D _soft;

    /// <summary>입 <paramref name="mouth"/>에서 <paramref name="basinY"/> 높이까지 물을 세운다. 거둘 때는 돌려준 오브젝트를 지운다.</summary>
    public static GameObject Create(string name, Vector3 mouth, float basinY, int band, Texture2D ring, Texture2D soft)
    {
        GameObject root = new GameObject(name);
        root.transform.position = mouth;
        FaucetWater w = root.AddComponent<FaucetWater>();
        w._mouth = mouth;
        w._basinY = Mathf.Min(basinY, mouth.y - 0.05f);
        w._band = Mathf.Clamp(band, 1, 4);
        w._ring = ring;
        w._soft = soft;
        w.Build();
        return root;
    }

    private void Build()
    {
        _water = Water(new Color(0.62f, 0.72f, 0.82f, 0.55f), 0.06f, null);
        // 57차 플레이 점검: 구간 1의 물방울만으로는 1cm라 잘 안 보였다 — 구간 1도 실 같은 줄기를 둔다.
        {
            GameObject s = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            s.name = "물줄기";
            Object.DestroyImmediate(s.GetComponent<Collider>());
            Renderer r = s.GetComponent<Renderer>();
            r.sharedMaterial = _water;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            s.transform.SetParent(transform, false);
            float len = _mouth.y - _basinY;
            s.transform.position = new Vector3(_mouth.x, _basinY + len * 0.5f, _mouth.z);
            _streamWidth = _band == 1 ? 0.0035f : _band == 2 ? 0.007f : _band == 3 ? 0.011f : 0.016f;
            s.transform.localScale = new Vector3(_streamWidth, len * 0.5f, _streamWidth);
            _stream = s.transform;
        }

        // 바닥에 고인 얇은 물막 — 물이 닿는 자리를 손전등에 번들거리게(가장자리가 흐린 둥근 판 — 57차 첫 확인에서 네모 판이 그대로 보였다).
        GameObject film = GameObject.CreatePrimitive(PrimitiveType.Quad);
        film.name = "물막";
        Object.DestroyImmediate(film.GetComponent<Collider>());
        Renderer fr = film.GetComponent<Renderer>();
        fr.sharedMaterial = Water(new Color(0.5f, 0.58f, 0.66f, 0.3f), 0.02f, _soft);
        fr.shadowCastingMode = ShadowCastingMode.Off;
        film.transform.SetParent(transform, false);
        film.transform.SetPositionAndRotation(new Vector3(_mouth.x, _basinY + 0.004f, _mouth.z), Quaternion.Euler(90f, 0f, 0f));
        float fw = 0.1f + 0.04f * _band;
        film.transform.localScale = new Vector3(fw, fw, 1f);
    }

    private void Update()
    {
        float now = Time.time;
        float dropGap = _band == 1 ? 0.85f : _band == 2 ? 0.35f : 0.18f;
        if (now >= _nextDrop)
        {
            _nextDrop = now + dropGap * Random.Range(0.75f, 1.25f);
            SpawnDrop();
        }

        if (_stream != null)
        {
            // 물줄기는 가늘게 떨린다 — 멈춘 막대가 아니라 흐르는 것으로 읽히게.
            float wob = 1f + 0.18f * (Mathf.PerlinNoise(now * 9f, 0.3f) - 0.5f);
            Vector3 sc = _stream.localScale;
            sc.x = _streamWidth * wob;
            sc.z = _streamWidth * (2f - wob);
            _stream.localScale = sc;
            if (now >= _nextRipple)
            {
                _nextRipple = now + (_band >= 3 ? 0.12f : 0.22f);
                SpawnRipple();
            }
        }

        float dt = Time.deltaTime;
        for (int i = 0; i < _drops.Count; i++)
        {
            Drop d = _drops[i];
            if (!d.Live) continue;
            d.Speed += Gravity * dt;
            Vector3 p = d.T.position;
            p.y -= d.Speed * dt;
            if (p.y <= _basinY + DropSize * 0.5f)
            {
                d.Live = false;
                d.T.gameObject.SetActive(false);
                SpawnRipple();
                continue;
            }

            d.T.position = p;
            // 떨어질수록 길쭉하게(빠른 물방울).
            d.T.localScale = new Vector3(DropSize, DropSize * (1f + Mathf.Min(2.5f, d.Speed * 0.9f)), DropSize);
        }

        for (int i = 0; i < _ripples.Count; i++)
        {
            Ripple r = _ripples[i];
            if (!r.Live) continue;
            r.Age += dt;
            float t = r.Age / 0.55f;
            if (t >= 1f)
            {
                r.Live = false;
                r.T.gameObject.SetActive(false);
                continue;
            }

            float size = Mathf.Lerp(0.02f, 0.11f + 0.02f * _band, t);
            r.T.localScale = new Vector3(size, size, 1f);
            Color c = r.Mat.color;
            c.a = 0.5f * (1f - t);
            r.Mat.color = c;
        }
    }

    private void SpawnDrop()
    {
        Drop d = null;
        for (int i = 0; i < _drops.Count; i++)
        {
            if (!_drops[i].Live)
            {
                d = _drops[i];
                break;
            }
        }

        if (d == null)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "물방울";
            Object.DestroyImmediate(go.GetComponent<Collider>());
            Renderer r = go.GetComponent<Renderer>();
            r.sharedMaterial = _water;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            go.transform.SetParent(transform, false);
            d = new Drop { T = go.transform };
            _drops.Add(d);
        }

        d.Live = true;
        d.Speed = 0f;
        float jitter = _band >= 2 ? _streamWidth * 0.8f : 0.002f;
        d.T.position = _mouth + new Vector3(Random.Range(-jitter, jitter), 0f, Random.Range(-jitter, jitter));
        d.T.localScale = Vector3.one * DropSize;
        d.T.gameObject.SetActive(true);
    }

    private void SpawnRipple()
    {
        if (_ring == null) return;
        Ripple r = null;
        for (int i = 0; i < _ripples.Count; i++)
        {
            if (!_ripples[i].Live)
            {
                r = _ripples[i];
                break;
            }
        }

        if (r == null)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "파문";
            Object.DestroyImmediate(go.GetComponent<Collider>());
            Renderer rd = go.GetComponent<Renderer>();
            Material m = new Material(Shader.Find("Sprites/Default"));
            m.mainTexture = _ring;
            m.color = new Color(0.7f, 0.8f, 0.9f, 0.5f);
            _materials.Add(m);
            rd.sharedMaterial = m;
            rd.shadowCastingMode = ShadowCastingMode.Off;
            rd.receiveShadows = false;
            go.transform.SetParent(transform, false);
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            r = new Ripple { T = go.transform, Mat = m };
            _ripples.Add(r);
        }

        r.Live = true;
        r.Age = 0f;
        r.T.position = new Vector3(_mouth.x + Random.Range(-0.004f, 0.004f), _basinY + 0.006f, _mouth.z + Random.Range(-0.004f, 0.004f));
        r.T.gameObject.SetActive(true);
    }

    /// <summary>매끈한 반투명 물(premultiply) — 손전등 반사광은 그대로 남는다. 아주 약한 자체 발광.</summary>
    private Material Water(Color color, float glow, Texture2D tex)
    {
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        Material m = new Material(lit != null ? lit : Shader.Find("Standard"));
        if (tex != null) m.SetTexture("_BaseMap", tex);
        m.SetColor("_BaseColor", color);
        m.SetFloat("_Smoothness", 0.97f);
        m.SetFloat("_Metallic", 0f);
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 1f);
        m.SetFloat("_SrcBlend", (float)BlendMode.One);
        m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0f);
        m.SetOverrideTag("RenderType", "Transparent");
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.EnableKeyword("_ALPHAPREMULTIPLY_ON");
        m.EnableKeyword("_EMISSION");
        m.SetColor("_EmissionColor", new Color(0.55f, 0.65f, 0.75f) * glow);
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        m.renderQueue = (int)RenderQueue.Transparent;
        _materials.Add(m);
        return m;
    }

    private void OnDestroy()
    {
        for (int i = 0; i < _materials.Count; i++)
        {
            if (_materials[i] != null) Destroy(_materials[i]);
        }

        _materials.Clear();
    }
}

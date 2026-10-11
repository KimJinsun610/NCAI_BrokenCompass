using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 95차 놀람 얼굴 연출(민: 「노란 얼굴의 놀람 연출이 얼굴이 제대로 안 나오네 · 놀람 연출이 전부 평면적인 걸 개선하고 싶네 — 진짜 얼굴을 코앞까지 들이민 것 같은 느낌」).
/// <see cref="ViolationScare"/>가 쓴다. 옛 방식(머리뼈 + 9cm를 눈앞 0.34m에 · 빛 없는 원본 색 · 짧은 직선 이동)이 평면적이던 까닭과 바꾼 것:
/// <list type="bullet">
/// <item><b>얼굴 맞춤</b>: 머리뼈 기준 고정 거리라 머리가 큰 노란 얼굴은 코만 화면에 찼다. 이제 실제 메시(목 위 꼭짓점)로 머리 크기·얼굴 앞면을 재어
/// 머리가 화면 높이의 <see cref="Fill"/>만큼 차는 거리에 둔다(몹마다 다름). 얼굴 가운데를 화면 가운데에.</item>
/// <item><b>들이밈</b>: 몹 자리에서 점점 빨라지며(가속) 날아와 <see cref="Overshoot"/>배까지 더 파고들었다가(화면 밖으로 넘침) 튕기듯 제자리로,
/// 머무는 동안에도 조금씩 더 다가오고(<see cref="Creep"/>) 고개가 천천히 기운다 — 멈춘 그림이 아니라 계속 움직이는 입체.</item>
/// <item><b>원근</b>: 덮치는 순간 시야각을 넓혔다(<see cref="FovPunch"/>) 천천히 되돌린다 — 코앞 물체의 원근 왜곡이 커져 「코앞」이 된다. 카메라는 움찔 뒤로·위로.</item>
/// <item><b>빛</b>: 빛을 받지 않는 원본 색은 그림자가 없어 종이처럼 납작했다. 이제 원래 재질 그대로 빛을 받되, 0.3m 앞 손전등에 하얗게 날지 않게
/// 손전등을 잠시 끄고(<see cref="FlashlightDim"/>), 아래 옆에서 비추는 작은 빛(<see cref="KeyIntensity"/>)으로 이목구비에 그늘을 만든다.</item>
/// <item><b>심도</b>: 얼굴 뒤 배경을 흐리게(가우시안 피사계 심도) + 비네트·색수차를 잠깐 — 얼굴만 또렷하게 떠오른다.</item>
/// </list>
/// 에셋·씬은 고치지 않는다(런타임에 만들고 지운다).
/// </summary>
public sealed class ScareFace
{
    /// <summary>머리가 화면 높이에서 차지하는 몫(머무를 때).</summary>
    public const float Fill = 0.98f;   // PlayScene 실측: 얼굴 앞면 평균으로 가운데를 잡은 뒤 — 0.86에서 노란 얼굴·소녀가 화면의 3/4, 머무는 동안 0.86배까지 다가오며 화면을 넘친다

    /// <summary>얼굴 앞면으로 치는 몫 — 머리 묶음 가운데에서 앞면까지의 이 몫보다 앞에 있는 꼭짓점의 높이 평균이 얼굴 가운데(눈·코·입). 머리털·정수리에 끌려 올라가지 않게.</summary>
    public const float FrontShare = 0.4f;

    /// <summary>덮칠 때 이 배율 거리까지 더 파고든다(1보다 작을수록 가깝게).</summary>
    public const float Overshoot = 0.62f;

    /// <summary>머무는 동안 이 배율 거리까지 더 다가온다.</summary>
    public const float Creep = 0.86f;

    /// <summary>날아오는 시간(초) — 가속.</summary>
    public const float LungeSeconds = 0.13f;

    /// <summary>파고들었다 제자리로 튕기는 시간(초).</summary>
    public const float SettleSeconds = 0.14f;

    /// <summary>덮치는 순간 넓히는 시야각(°, 원래 시야각에 더함).</summary>
    public const float FovPunch = 16f;

    /// <summary>머무는 동안 남기는 시야각(°, 원래에 더함).</summary>
    public const float FovHold = 7f;

    /// <summary>움찔 — 카메라가 뒤로 물러나는 거리(m).</summary>
    public const float Recoil = 0.06f;

    /// <summary>손전등을 이 배율로 줄인다(얼굴이 하얗게 날지 않게 — 0.3m 앞 손전등은 실내 노출 +2.5EV에서 노란 얼굴을 흰 판으로 만들었다). 손전등 스크립트가 매 프레임 세기를 다시 쓰므로 매 프레임 덮는다.</summary>
    public const float FlashlightDim = 0f;

    /// <summary>아래 옆 빛의 세기(PlayScene 실측 — 0.12에서 이목구비에 그늘이 지고 원래 색이 산다).</summary>
    public const float KeyIntensity = 0.12f;

    /// <summary>머무는 동안 고개가 기우는 최대 각(°).</summary>
    public const float TiltDegrees = 7f;

    private readonly GameObject _go;
    private readonly Transform _head;
    private readonly Vector3 _centerLocal;   // 머리뼈 기준 얼굴 가운데
    private readonly float _frontDepth;      // 얼굴 가운데에서 앞면까지(m)
    private readonly float _headHeight;
    private readonly Camera _cam;
    private readonly float _fov0;
    private readonly Vector3 _camLocal0;
    private Light _key;
    private readonly List<KeyValuePair<Light, float>> _dimmed = new List<KeyValuePair<Light, float>>();
    private Volume _volume;
    private VolumeProfile _profile;

    /// <summary>놀람 모델.</summary>
    public GameObject Go
    {
        get { return _go; }
    }

    /// <summary>머무를 때 카메라에서 얼굴 가운데까지(m).</summary>
    public float HoldDistance { get; private set; }

    private ScareFace(GameObject go, Transform head, Vector3 centerLocal, float frontDepth, float headHeight, Camera cam)
    {
        _go = go;
        _head = head;
        _centerLocal = centerLocal;
        _frontDepth = frontDepth;
        _headHeight = headHeight;
        _cam = cam;
        _fov0 = cam.fieldOfView;
        _camLocal0 = cam.transform.localPosition;
        float fov = (_fov0 + FovHold) * Mathf.Deg2Rad;
        float d = _headHeight / (2f * Mathf.Tan(fov * 0.5f) * Fill);
        HoldDistance = Mathf.Max(d, _frontDepth + 0.16f);
    }

    /// <summary>놀람 모델을 만든다(콜라이더·연출 스크립트는 뗌). 못 만들면 null.</summary>
    public static ScareFace Create(string standIn, Camera cam, Vector3 at, Quaternion rot)
    {
        if (string.IsNullOrEmpty(standIn) || cam == null) return null;
        GameObject go = StandInFactory.Create(standIn, at, rot, null);
        if (go == null) return null;
        go.name = "놀람 얼굴 · " + standIn;
        foreach (Collider c in go.GetComponentsInChildren<Collider>(true)) Object.Destroy(c);
        foreach (MonoBehaviour b in go.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (b != null) b.enabled = false;
        }

        foreach (Animator a in go.GetComponentsInChildren<Animator>(true)) a.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
        {
            r.shadowCastingMode = ShadowCastingMode.Off;   // 코앞 얼굴의 그림자가 화면을 덮지 않게
            if (r is SkinnedMeshRenderer smr) smr.updateWhenOffscreen = true;
        }

        Transform head = Find(go.transform, "head");
        Transform neck = Find(go.transform, "neck");
        Vector3 center;
        float front, height;
        Measure(go, head, neck, out center, out front, out height);
        Vector3 local = head != null ? head.InverseTransformPoint(center) : go.transform.InverseTransformPoint(center);
        if (head != null) Stare.Attach(go, head, cam);   // 고개를 숙인 동작이어도 얼굴이 카메라를 똑바로 본다
        return new ScareFace(go, head, local, front, height, cam);
    }

    /// <summary>목 위 꼭짓점으로 머리 크기·얼굴 가운데·앞면 깊이를 잰다(모델 앞 = +z).</summary>
    internal static void Measure(GameObject go, Transform head, Transform neck, out Vector3 center, out float front, out float height)
    {
        float cut = neck != null ? neck.position.y : head != null ? head.position.y - 0.08f : go.transform.position.y + 1.4f;
        Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
        Mesh baked = new Mesh();
        Transform root = go.transform;
        bool any = false;
        List<Vector3> pts = new List<Vector3>();
        foreach (SkinnedMeshRenderer r in go.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            r.BakeMesh(baked, true);   // 렌더러 변환(크기 포함)으로 월드에 — PlayScene 실측: 이 조합이 렌더러 묶음 상자와 맞는다(원본 FBX ×95 배율)
            Vector3[] v = baked.vertices;
            Matrix4x4 toWorld = r.transform.localToWorldMatrix;
            for (int i = 0; i < v.Length; i++)
            {
                Vector3 w = toWorld.MultiplyPoint3x4(v[i]);
                if (w.y < cut) continue;
                Vector3 l = root.InverseTransformPoint(w);
                min = Vector3.Min(min, l);
                max = Vector3.Max(max, l);
                pts.Add(l);
                any = true;
            }
        }

        Object.Destroy(baked);
        if (!any)
        {
            Vector3 h = head != null ? head.position : root.position + Vector3.up * 1.6f;
            center = h + Vector3.up * 0.09f;
            front = 0.1f;
            height = 0.3f;
            return;
        }

        float scale = root.lossyScale.y;
        Vector3 c = (min + max) * 0.5f;
        // 높이는 앞면 꼭짓점 평균(눈·코·입) — 묶음 가운데는 머리털이 큰 소년에서 얼굴보다 높았다.
        float frontZ = c.z + (max.z - c.z) * FrontShare;
        float sumY = 0f;
        int n = 0;
        for (int i = 0; i < pts.Count; i++)
        {
            if (pts[i].z < frontZ) continue;
            sumY += pts[i].y;
            n++;
        }

        if (n > 20) c.y = sumY / n;
        center = root.TransformPoint(c);
        front = (max.z - c.z) * scale;
        height = Mathf.Max(0.15f, (max.y - min.y) * scale);
    }

    /// <summary>
    /// 머리를 카메라 쪽으로 — 동작(애니메이터) 위에 덧붙여 얼굴 앞이 카메라를 보게 한다(<see cref="Weight"/>만큼). 소년은 고개를 숙이는 숨쉬기 동작이라 정수리를 들이밀었다.
    /// 얼굴 앞 = 만들 때(저장된 자세) 몸 앞쪽(+z)을 머리뼈 기준으로 적어 둔 방향.
    /// </summary>
    internal sealed class Stare : MonoBehaviour
    {
        public const float Weight = 0.85f;
        private Transform _head;
        private Camera _cam;
        private Vector3 _axisLocal;

        private float _weight = Weight;

        public static void Attach(GameObject go, Transform head, Camera cam)
        {
            Attach(go, head, cam, Weight, head.InverseTransformDirection(go.transform.forward));
        }

        /// <summary>
        /// 99차: 얼굴 앞을 머리뼈 기준 축 <paramref name="faceAxisLocal"/>로 직접 준다(배치 붙잡힘 인체 모형 — 선 자세의 고개가 몸 앞에서 17° 돌아가 있어
        /// 「몸 앞 = 얼굴 앞」으로 적으면 늘 그만큼 옆을 봤다. 모형 머리뼈의 +z가 얼굴 앞). <paramref name="weight"/> 1 = 정확히 카메라를 본다.
        /// </summary>
        public static void Attach(GameObject go, Transform head, Camera cam, float weight, Vector3 faceAxisLocal)
        {
            Stare s = go.AddComponent<Stare>();
            s._head = head;
            s._cam = cam;
            s._weight = Mathf.Clamp01(weight);
            s._axisLocal = faceAxisLocal.normalized;
        }

        private void LateUpdate()
        {
            if (_head == null || _cam == null) return;
            Vector3 cur = _head.TransformDirection(_axisLocal);
            Vector3 want = _cam.transform.position - _head.position;
            if (want.sqrMagnitude < 1e-6f || cur.sqrMagnitude < 1e-6f) return;
            Quaternion q = Quaternion.FromToRotation(cur, want.normalized);
            _head.rotation = Quaternion.Slerp(Quaternion.identity, q, _weight) * _head.rotation;
        }
    }

    internal static Transform Find(Transform root, string bone)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            string n = t.name.ToLowerInvariant();
            if (n == bone || n.EndsWith(":" + bone) || n.EndsWith("_" + bone)) return t;
        }

        return null;
    }

    /// <summary>
    /// 얼굴 가운데를 카메라 앞 <paramref name="distance"/>m(가운데까지)에 두고 카메라를 보게. <paramref name="tilt"/>°만큼 고개를 기울인다(시선 축).
    /// <paramref name="from"/>·<paramref name="w"/>: 덮쳐 오는 중이면 몹 자리(from)에서 w(0~1)만큼 온 자리.
    /// </summary>
    public void Pose(float distance, float tilt, Vector3 jitter, Vector3 from, Quaternion fromRot, float w)
    {
        if (_go == null) return;
        Transform ct = _cam.transform;
        Vector3 fwd = ct.forward;
        Quaternion faceCam = Quaternion.LookRotation(-fwd, ct.up) * Quaternion.Euler(0f, 0f, tilt) * Quaternion.Euler(jitter);
        Quaternion rot = Quaternion.Slerp(fromRot, faceCam, Mathf.Clamp01(w));
        _go.transform.rotation = rot;
        Vector3 target = ct.position + fwd * distance;
        Vector3 c = Center();
        Vector3 posAtTarget = _go.transform.position + (target - c);
        Vector3 posAtHome = from;
        _go.transform.position = Vector3.LerpUnclamped(posAtHome, posAtTarget, w);
    }

    /// <summary>지금 얼굴 가운데(월드).</summary>
    public Vector3 Center()
    {
        return _head != null ? _head.TransformPoint(_centerLocal) : _go.transform.TransformPoint(_centerLocal);
    }

    // ── 카메라 · 빛 · 화면 ────────────────────────────────────

    /// <summary>덮치기 시작 — 손전등 줄임 · 아래 옆 빛 · 심도/비네트 볼륨.</summary>
    public void BeginFx()
    {
        foreach (Light l in _cam.GetComponentsInChildren<Light>(true))
        {
            if (l.type != LightType.Spot) continue;
            _dimmed.Add(new KeyValuePair<Light, float>(l, l.intensity));
        }

        DimFlashlight();

        GameObject key = new GameObject("놀람 얼굴 빛");
        key.transform.SetParent(_cam.transform, false);
        key.transform.localPosition = new Vector3(0.22f, -0.32f, 0.05f);   // 아래 오른쪽 — 턱 밑·눈두덩에 그늘
        _key = key.AddComponent<Light>();
        _key.type = LightType.Point;
        _key.range = 1.6f;
        _key.intensity = KeyIntensity;
        _key.color = new Color(1f, 0.9f, 0.78f);
        _key.shadows = LightShadows.None;

        _profile = ScriptableObject.CreateInstance<VolumeProfile>();
        DepthOfField dof = _profile.Add<DepthOfField>(true);
        dof.mode.Override(DepthOfFieldMode.Gaussian);
        dof.gaussianStart.Override(HoldDistance + 0.25f);
        dof.gaussianEnd.Override(HoldDistance + 2.2f);
        dof.gaussianMaxRadius.Override(1.5f);
        dof.highQualitySampling.Override(true);
        Vignette vig = _profile.Add<Vignette>(true);
        vig.intensity.Override(0.5f);
        vig.smoothness.Override(0.6f);
        vig.color.Override(Color.black);
        ChromaticAberration ca = _profile.Add<ChromaticAberration>(true);
        ca.intensity.Override(0.7f);
        GameObject vgo = new GameObject("놀람 얼굴 화면");
        _volume = vgo.AddComponent<Volume>();
        _volume.isGlobal = true;
        _volume.priority = 400f;
        _volume.weight = 0f;
        _volume.sharedProfile = _profile;
    }

    /// <summary>카메라 원근·움찔·볼륨 세기. <paramref name="punch"/> 0~1(덮치는 순간 1), <paramref name="fx"/> 볼륨 세기 0~1.</summary>
    public void CameraFx(float punch, float hold, float fx)
    {
        if (_cam == null) return;
        _cam.fieldOfView = _fov0 + FovHold * hold + (FovPunch - FovHold) * punch;
        _cam.transform.localPosition = _camLocal0 + new Vector3(0f, 0.015f, -Recoil) * Mathf.Max(punch, hold * 0.6f);
        if (_volume != null) _volume.weight = Mathf.Clamp01(fx);
        _dim = Mathf.Lerp(1f, FlashlightDim, Mathf.Clamp01(fx));   // 다가오는 동안 손전등이 죽어 간다
        DimFlashlight();
    }

    private float _dim = 1f;

    private void DimFlashlight()
    {
        for (int i = 0; i < _dimmed.Count; i++)
        {
            if (_dimmed[i].Key != null) _dimmed[i].Key.intensity = _dimmed[i].Value * _dim;
        }
    }

    /// <summary>끝 — 시야각·카메라 자리·손전등 되돌리고 빛·볼륨을 지운다. 모델은 지우지 않는다.</summary>
    public void EndFx()
    {
        if (_cam != null)
        {
            _cam.fieldOfView = _fov0;
            _cam.transform.localPosition = _camLocal0;
        }

        for (int i = 0; i < _dimmed.Count; i++)
        {
            if (_dimmed[i].Key != null) _dimmed[i].Key.intensity = _dimmed[i].Value;
        }

        _dimmed.Clear();
        if (_key != null) Object.Destroy(_key.gameObject);
        _key = null;
        if (_volume != null) Object.Destroy(_volume.gameObject);
        _volume = null;
        if (_profile != null) Object.Destroy(_profile);
        _profile = null;
    }
}

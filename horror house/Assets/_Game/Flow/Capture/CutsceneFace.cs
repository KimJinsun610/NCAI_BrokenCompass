using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 96차 사망 컷신 얼굴(민: 「같은 방식으로 한번 바꿔봐. 대신 연출 구도는 바꾸지 말고. 모델도 소년 → 화장실 소녀, 인체모형 → 노란 얼굴로」).
/// 김진선님 컷신 프리팹·Timeline은 그대로 두고 <b>이번 인스턴스만</b> 바꾼다 — <see cref="CaptureDirector"/>가 <c>Play</c> 전에 붙인다.
/// <list type="bullet">
/// <item><b>모델</b>: 청각 컷신의 소년(Boy) 아래에 화장실 소녀(<see cref="AuditoryStandIn"/>), 조도 컷신의 인체 모형(Dummy/Model) 자리에 노란 얼굴(<see cref="IlluminanceStandIn"/>).
/// 원래 몸은 그리지만 않는다(Timeline 바인딩·활성 트랙·비명 이동은 원래 것을 그대로 따른다).
/// 원래 얼굴과 <b>머리 크기</b>를 같게 하고, 매 프레임 <b>얼굴 가운데를 원래 얼굴(움직이는 뼈) 자리</b>에 붙인다 — 카메라 키는 손대지 않고
/// 소년이 고개 숙이고 비명 지르며 덮쳐 오는 움직임도 그대로 따르므로 화면 구도가 그대로다.
/// 고개만 카메라 쪽으로(<see cref="ScareFace.Stare"/>).</item>
/// <item><b>빛·심도</b>: 95차 놀람 얼굴과 같은 방식 — 원본 색(빛 무시) 대신 재질이 빛을 받고, 코앞 손전등에 하얗게 날지 않게 얼굴이 보이는 동안 손전등을 줄이며(<see cref="AuditoryDim"/>·<see cref="IlluminanceDim"/>),
/// 아래 옆 작은 빛(<see cref="KeyIntensity"/>)이 이목구비에 그늘을 만들고, 얼굴 뒤를 흐린다(가우시안 피사계 심도, 초점 = 얼굴까지 거리 매 프레임).</item>
/// </list>
/// 컷신이 끝나거나 지워지면 빛·볼륨을 지우고 손전등을 되돌린다. 못 찾으면(구조가 바뀐 프리팹) 아무것도 바꾸지 않는다.
/// </summary>
[DefaultExecutionOrder(10000)]   // 컷신(DeathCutscene)·손전등(FlashlightPower)이 세기를 쓴 뒤에 덮는다
[DisallowMultipleComponent]
public sealed class CutsceneFace : MonoBehaviour
{
    /// <summary>청각 컷신 — 소년 대신.</summary>
    public const string AuditoryStandIn = "mob.girl.stand";

    /// <summary>조도 컷신 — 인체 모형 대신.</summary>
    public const string IlluminanceStandIn = "mob.duck";

    /// <summary>아래 옆 빛의 세기.</summary>
    public const float KeyIntensity = 0.12f;

    /// <summary>청각 컷신: 얼굴이 보이는 동안 플레이어 손전등 배율(0.3m 앞 손전등은 얼굴을 흰 판으로 만든다).</summary>
    public const float AuditoryDim = 0f;

    /// <summary>조도 컷신: 컷신이 움직이는 손전등 밝기에 더 곱하는 배율(깜빡임·꺼짐은 그대로).
    /// 97차 PlayScene 실측: 얼굴이 0.16m 앞이라 ×0.3(세기 0.11)에도 하얗게 날았다 — ×0.02(0.008)에서 원래 색과 굴곡이 산다.</summary>
    public const float IlluminanceDim = 0.02f;

    /// <summary>조도 컷신의 아래 옆 빛 세기 — 얼굴이 청각 컷신보다 훨씬 가깝다(0.4m 안).</summary>
    public const float IlluminanceKey = 0.07f;

    /// <summary>소년 얼굴 가운데(코) = Head 뼈(턱 이음매)에서 위로 이만큼(m). 김진선님 컷신의 조준 보정(aimHeightOffset 0.09 — 눈 높이)보다 조금 아래.</summary>
    public const float BoyFaceLift = 0.05f;

    /// <summary>인체 모형 얼굴 가운데(코) = head ~ head_end의 이 몫(FitDummyFace의 눈 높이 0.6보다 조금 아래).</summary>
    public const float DummyFaceAt = 0.45f;

    /// <summary>얼굴이 켜진 뒤 빛·심도가 다 걸리기까지(초).</summary>
    public const float FadeSeconds = 0.25f;

    /// <summary>예전 대역 얼굴 id를 바뀐 모델로(연출표 기본 얼굴 장면 — 컷신을 틀지 못했을 때).</summary>
    public static string Replace(string id)
    {
        if (id == "mob.boy" || id == "mob.boy.stand") return AuditoryStandIn;
        if (id == "mob.dummy" || id == "mob.dummy.stand") return IlluminanceStandIn;
        return id;
    }

    private DeathCutscene _cs;
    private UnityEngine.Playables.PlayableDirector _director;
    private GameObject _model;
    private Transform _head;
    private Vector3 _centerLocal;
    // 원래 얼굴 가운데 = Lerp(_refA, _refB, _refT) + 위로 _refLift — 원래 몸은 그리지 않지만 Timeline·비명 동작으로 뼈는 계속 움직인다.
    // 메시로 재지 않는다: 소년은 고개를 숙여 카메라에 가장 가까운 꼭짓점이 정수리 머리털이라 얼굴 가운데가 10cm쯤 위로 잡혔다(PlayScene 실측).
    private Transform _refA;
    private Transform _refB;
    private float _refT;
    private float _refLift;
    private bool _grabbed;   // 손전등을 컷신이 붙잡아 매 프레임 다시 쓴다(조도) — 곱하기만
    private float _dim;
    private float _fx;
    private Camera _cam;
    private Light _key;
    private Volume _volume;
    private VolumeProfile _profile;
    private DepthOfField _dof;
    private readonly List<KeyValuePair<Light, float>> _lamps = new List<KeyValuePair<Light, float>>();

    /// <summary>바꾼 모델(없으면 null).</summary>
    public GameObject Model
    {
        get { return _model; }
    }

    /// <summary>컷신 시각(초). 검수용.</summary>
    public double CutsceneTime
    {
        get { return _director != null ? _director.time : 0d; }
    }

    /// <summary>지금 빛·심도 세기(0~1). 검수용.</summary>
    public float Fx
    {
        get { return _fx; }
    }

    /// <summary>
    /// 컷신 인스턴스의 몹을 바꾸고 빛·심도를 붙인다. <c>Play</c> 전에, <see cref="CaptureDirector.FitDummyFace"/> 뒤에 부른다.
    /// 소년(Boy)이 있으면 화장실 소녀, 인체 모형(Dummy/Model)이 있으면 노란 얼굴. 바꿨으면 그 컴포넌트, 못 바꿨으면 null.
    /// </summary>
    public static CutsceneFace Attach(DeathCutscene cs)
    {
        if (cs == null) return null;
        Transform boy = cs.transform.Find("Boy");
        if (boy != null) return Build(cs, boy, boy, AuditoryStandIn, false);

        Transform dummy = Deep(cs.transform, "Dummy");
        Transform model = dummy != null ? dummy.Find("Model") : null;
        if (model != null) return Build(cs, dummy, model, IlluminanceStandIn, true);
        return null;
    }

    /// <param name="parent">새 모델을 둘 자리(원래 몸의 부모 또는 몸 자체 — 그 변환이 Timeline·비명 이동을 받는다).</param>
    /// <param name="body">원래 몸(그리지 않게 하고 머리를 잰다).</param>
    private static CutsceneFace Build(DeathCutscene cs, Transform parent, Transform body, string standIn, bool grabbed)
    {
        // 원래 얼굴 — 꺼진 가지면 잠깐 켜서 잰다(꺼진 렌더러는 GetComponentsInChildren에 안 잡힌다).
        bool wasActive = body.gameObject.activeSelf;
        bool parentActive = parent.gameObject.activeSelf;
        parent.gameObject.SetActive(true);
        body.gameObject.SetActive(true);
        Transform refHead = ScareFace.Find(body, "head");
        Transform refNeck = ScareFace.Find(body, "neck");
        Vector3 refCenter;
        float refFront, refHeight;
        ScareFace.Measure(body.gameObject, refHead, refNeck, out refCenter, out refFront, out refHeight);
        body.gameObject.SetActive(wasActive);
        parent.gameObject.SetActive(parentActive);
        if (refHead == null) return null;
        Transform headEnd = ScareFace.Find(body, "head_end");

        GameObject go = StandInFactory.Create(standIn, parent.position, parent.rotation, null);
        if (go == null) return null;
        go.name = "컷신 얼굴 · " + standIn;
        foreach (Collider c in go.GetComponentsInChildren<Collider>(true)) Destroy(c);
        foreach (MonoBehaviour b in go.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (b != null) b.enabled = false;
        }

        foreach (Animator a in go.GetComponentsInChildren<Animator>(true)) a.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        foreach (SkinnedMeshRenderer smr in go.GetComponentsInChildren<SkinnedMeshRenderer>(true)) smr.updateWhenOffscreen = true;

        Transform head = ScareFace.Find(go.transform, "head");
        Transform neck = ScareFace.Find(go.transform, "neck");
        Vector3 center;
        float front, height;
        ScareFace.Measure(go, head, neck, out center, out front, out height);

        // 머리 크기를 원래 얼굴과 같게 — 구도(카메라 키)는 원래 얼굴 크기로 잡혀 있다.
        float scale = Mathf.Clamp(refHeight / Mathf.Max(0.05f, height), 0.4f, 2.5f);
        Vector3 centerInRoot = go.transform.InverseTransformPoint(center);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one * scale;
        // 원래 몸은 그리지 않는다(바인딩·트랙은 그대로 돈다). 안 그려도 뼈는 계속 움직이게 — 새 얼굴이 그 머리를 따라간다.
        foreach (Renderer r in body.GetComponentsInChildren<Renderer>(true))
        {
            if (!r.transform.IsChildOf(go.transform)) r.enabled = false;
        }

        // 원래 몸에 붙은 얼굴 빛(소년 FaceLight — 원본 색 시절의 차가운 빛)은 끈다: 빛을 받는 소녀의 머리털을 하얗게 날렸다.
        foreach (Light l in parent.GetComponentsInChildren<Light>(true))
        {
            if (!l.transform.IsChildOf(go.transform)) l.enabled = false;
        }

        foreach (Animator a in body.GetComponentsInChildren<Animator>(true))
        {
            if (!a.transform.IsChildOf(go.transform)) a.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }

        CutsceneFace f = cs.gameObject.AddComponent<CutsceneFace>();
        f._cs = cs;
        f._director = cs.GetComponent<UnityEngine.Playables.PlayableDirector>();
        f._model = go;
        f._head = head;
        f._centerLocal = head != null ? head.InverseTransformPoint(go.transform.TransformPoint(centerInRoot)) : centerInRoot;
        f._grabbed = grabbed;
        f.SetRef(refHead, headEnd);
        f.Follow();
        Camera cam = Camera.main;
        if (head != null && cam != null) ScareFace.Stare.Attach(go, head, cam);
        return f;
    }

    private void SetRef(Transform head, Transform headEnd)
    {
        _refA = head;
        _refB = headEnd != null ? headEnd : head;
        _refT = headEnd != null ? DummyFaceAt : 0f;
        _refLift = headEnd != null ? 0f : BoyFaceLift;   // 노란 얼굴은 0 — 8cm 내리면 눈이 모형 눈 자리에 오지만 긴 얼굴의 입이 화면 밖으로 나갔다(PlayScene 실측)
    }

    private Vector3 RefCenter()
    {
        return Vector3.Lerp(_refA.position, _refB.position, _refT) + Vector3.up * _refLift;
    }

    private static Transform Deep(Transform root, string name)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == name) return t;
        }

        return null;
    }

    private Vector3 FaceCenter()
    {
        return _head != null ? _head.TransformPoint(_centerLocal) : _model.transform.position + Vector3.up * 1.5f;
    }

    /// <summary>새 얼굴 가운데를 원래 얼굴 가운데(지금 뼈 자리)에.</summary>
    private void Follow()
    {
        if (_model == null || _refA == null) return;
        _model.transform.position += RefCenter() - FaceCenter();
    }

    private void LateUpdate()
    {
        Follow();
        bool on = _cs != null && DeathCutscene.Playing == _cs && _model != null && _model.activeInHierarchy;
        if (on && _cam == null) Begin();
        if (_cam == null) return;

        float step = Time.unscaledDeltaTime / Mathf.Max(0.01f, FadeSeconds);
        _fx = Mathf.Clamp01(_fx + (on ? step : -step * 2f));
        if (!on && _fx <= 0f)
        {
            End();
            return;
        }

        float d = Vector3.Distance(_cam.transform.position, FaceCenter());
        if (_dof != null)
        {
            _dof.gaussianStart.Override(d + 0.25f);
            _dof.gaussianEnd.Override(d + 2.2f);
        }

        if (_volume != null) _volume.weight = _fx;
        if (_key != null) _key.intensity = (_grabbed ? IlluminanceKey : KeyIntensity) * _fx;
        Dim();
    }

    private void Begin()
    {
        _cam = Camera.main;
        if (_cam == null) return;
        _lamps.Clear();
        foreach (Light l in _cam.GetComponentsInChildren<Light>(true))
        {
            if (l.type == LightType.Spot) _lamps.Add(new KeyValuePair<Light, float>(l, l.intensity));
        }

        GameObject key = new GameObject("컷신 얼굴 빛");
        key.transform.SetParent(_cam.transform, false);
        key.transform.localPosition = new Vector3(0.22f, -0.32f, 0.05f);   // 아래 오른쪽 — 턱 밑·눈두덩에 그늘(95차와 같은 자리)
        _key = key.AddComponent<Light>();
        _key.type = LightType.Point;
        _key.range = 1.6f;
        _key.intensity = 0f;
        _key.color = new Color(1f, 0.9f, 0.78f);
        _key.shadows = LightShadows.None;

        _profile = ScriptableObject.CreateInstance<VolumeProfile>();
        _dof = _profile.Add<DepthOfField>(true);
        _dof.mode.Override(DepthOfFieldMode.Gaussian);
        _dof.gaussianStart.Override(0.6f);
        _dof.gaussianEnd.Override(2.5f);
        _dof.gaussianMaxRadius.Override(1.5f);
        _dof.highQualitySampling.Override(true);
        GameObject vgo = new GameObject("컷신 얼굴 화면");
        _volume = vgo.AddComponent<Volume>();
        _volume.isGlobal = true;
        _volume.priority = 400f;
        _volume.weight = 0f;
        _volume.sharedProfile = _profile;
    }

    private void Dim()
    {
        // 조도: 컷신이 끝나 손전등을 놓은 뒤에는 곱하지 않는다(아무도 다시 쓰지 않으면 곱이 쌓여 꺼져 간다).
        if (_grabbed && DeathCutscene.Playing != _cs) return;
        float target = _grabbed ? IlluminanceDim : AuditoryDim;
        _dim = Mathf.Lerp(1f, target, _fx);
        for (int i = 0; i < _lamps.Count; i++)
        {
            Light l = _lamps[i].Key;
            if (l == null) continue;
            // 조도: 컷신이 매 프레임 「원래 세기 × 밝기」를 쓴 뒤라 곱하기만. 청각: 처음 세기 기준.
            l.intensity = _grabbed ? l.intensity * _dim : _lamps[i].Value * _dim;
        }
    }

    private void End()
    {
        if (!_grabbed)
        {
            for (int i = 0; i < _lamps.Count; i++)
            {
                if (_lamps[i].Key != null) _lamps[i].Key.intensity = _lamps[i].Value;
            }
        }

        _lamps.Clear();
        if (_key != null) Destroy(_key.gameObject);
        _key = null;
        if (_volume != null) Destroy(_volume.gameObject);
        _volume = null;
        if (_profile != null) Destroy(_profile);
        _profile = null;
        _dof = null;
        _cam = null;
        _fx = 0f;
    }

    private void OnDestroy()
    {
        End();
    }
}

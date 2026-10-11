using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 101차(민: 「손과 태블릿에도 라이팅을 적용할 수 있어? 대신 태블릿 화면은 적용 없이 늘 잘 보이게」).
/// 김진선님 <c>ViewmodelRig</c>은 손·태블릿을 씬 조명·후처리에서 떼어 내고 카메라에 붙은 전용 조명(<c>ViewmodelLight</c>, 방향광 1.1) 하나로만 비춘다 —
/// 어두운 복도에서도 손은 늘 같은 밝기로 떠 보였다. 그 파일은 고치지 않고, 그 전용 조명의 <b>세기·색·방향</b>을 매 프레임 지금 자리의 빛으로 맞춘다:
/// <list type="bullet">
/// <item>손 자리(카메라 앞 아래)에 닿는 씬의 실시간 점·스포트 조명을 URP와 같은 감쇠(거리 제곱 · 범위 끝 부드럽게 · 스포트 원뿔)로 더하고,
/// 그 자리의 화면 노출(실내·실외 보정 + 밝기 설정 + 안개 걷기 — <see cref="BrightnessSettings"/>)을 곱한다.
/// 뷰모델 카메라는 후처리(노출)를 받지 않으므로 이렇게 곱해야 세상과 같은 밝기가 된다.</item>
/// <item>손전등이 켜져 있으면 손에 되비치는 빛(<see cref="FlashlightFill"/>)을 더한다(손전등 빛 자체는 앞을 비춘다).</item>
/// <item>빛이 오는 쪽으로 방향광을 돌려 그늘이 맞게 진다. 가장 어두워도 <see cref="MinIntensity"/> — 손 윤곽은 남는다.</item>
/// </list>
/// 태블릿 화면(<c>Custom/TabletScreen</c>)과 글자(TextMeshPro)는 빛을 받지 않는 재질이라 그대로 늘 잘 보인다. 씬·에셋은 고치지 않는다(런타임에 스스로 선다).
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(9000)]
public sealed class ViewmodelLighting : MonoBehaviour
{
    /// <summary>계산한 빛 → 뷰모델 조명 세기 배율(PlayScene 실측 — 손 피부·태블릿 몸체가 옆 벽과 비슷한 밝기).</summary>
    public const float Gain = 1f;

    /// <summary>가장 어두울 때의 세기(완전한 어둠에서도 손 윤곽).</summary>
    public const float MinIntensity = 0.12f;

    /// <summary>가장 밝을 때의 세기.</summary>
    public const float MaxIntensity = 1.6f;

    /// <summary>손전등을 켜면 손에 되비치는 빛.</summary>
    public const float FlashlightFill = 0.25f;

    /// <summary>바뀐 빛을 따라가는 빠르기(1/초).</summary>
    public const float Follow = 4f;

    /// <summary>손 자리 — 카메라 기준(앞 0.35m · 아래 0.2m).</summary>
    public static readonly Vector3 HandOffset = new Vector3(0f, -0.2f, 0.35f);

    private const string LightName = "ViewmodelLight";
    private const float Rescan = 1f;

    private Light _vm;
    private Camera _cam;
    private float _baseIntensity = -1f;
    private Quaternion _baseRot;
    private Color _baseColor;
    private float _intensity = -1f;
    private Vector3 _dir;
    private float _rescanAt;
    private readonly List<Light> _lights = new List<Light>();

    /// <summary>지금 뷰모델 조명 세기(검수용).</summary>
    public float Intensity
    {
        get { return _intensity; }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Ensure();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Ensure();
    }

    private static void Ensure()
    {
        if (FindAnyObjectByType<ViewmodelLighting>() != null) return;
        if (FindAnyObjectByType<ViewmodelRig>() == null) return;   // 뷰모델이 없는 씬(타이틀 등)
        new GameObject("ViewmodelLighting (auto)").AddComponent<ViewmodelLighting>();
    }

    private bool Bind()
    {
        if (_vm != null && _cam != null) return true;
        _cam = Camera.main;
        if (_cam == null) return false;
        Transform t = _cam.transform.Find(LightName);
        if (t == null) return false;   // ViewmodelRig.Start 전
        _vm = t.GetComponent<Light>();
        if (_vm == null) return false;
        _baseIntensity = _vm.intensity;
        _baseRot = t.localRotation;
        _baseColor = _vm.color;
        _dir = Vector3.zero;
        _rescanAt = 0f;
        return true;
    }

    private void Scan()
    {
        _lights.Clear();
        foreach (Light l in FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (l == null || (l.type != LightType.Point && l.type != LightType.Spot)) continue;
            if (l.bakingOutput.isBaked && l.bakingOutput.lightmapBakeType == LightmapBakeType.Baked) continue;   // 구운 빛은 움직이는 물체를 비추지 않는다
            if (_cam != null && l.transform.IsChildOf(_cam.transform)) continue;   // 손전등·연출용 카메라 빛은 따로
            _lights.Add(l);
        }
    }

    private void LateUpdate()
    {
        if (!Bind()) return;
        if (Time.unscaledTime >= _rescanAt)
        {
            Scan();
            _rescanAt = Time.unscaledTime + Rescan;
        }

        Vector3 p = _cam.transform.TransformPoint(HandOffset);
        Vector3 sum = Vector3.zero;   // 빛이 오는 방향 × 세기
        float r = 0f, g = 0f, b = 0f;
        for (int i = 0; i < _lights.Count; i++)
        {
            Light l = _lights[i];
            if (l == null || !l.isActiveAndEnabled || l.intensity <= 0f) continue;
            float a = Attenuation(l, p);
            if (a <= 0f) continue;
            Color c = l.color * (l.intensity * a);
            r += c.r;
            g += c.g;
            b += c.b;
            Vector3 from = (p - l.transform.position).normalized;   // 빛이 나아가는 방향
            sum += from * (0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b);
        }

        float ev = TrueColor.ExposureAt(_cam.transform.position) + BrightnessSettings.Offset + BrightnessSettings.DehazeExposure;
        float gain = Mathf.Pow(2f, ev) * Gain;
        float lum = (0.2126f * r + 0.7152f * g + 0.0722f * b) * gain;

        bool torch = FlashlightRelay.Active != null && FlashlightRelay.Active.IsOn;
        float want = Mathf.Clamp(lum + (torch ? FlashlightFill : 0f), MinIntensity, MaxIntensity);
        float k = 1f - Mathf.Exp(-Follow * Time.unscaledDeltaTime);
        _intensity = _intensity < 0f ? want : Mathf.Lerp(_intensity, want, k);
        _vm.intensity = _intensity;

        // 색: 씬 빛의 색(세기로 나눠 정규화)을 원래 색에 조금 섞는다 — 밝을 때만.
        float total = r + g + b;
        Color tint = total > 1e-4f ? new Color(r / total * 3f, g / total * 3f, b / total * 3f) : Color.white;
        _vm.color = Color.Lerp(_baseColor, _baseColor * tint, Mathf.Clamp01(lum / MaxIntensity) * 0.5f);

        // 방향: 빛이 오는 쪽으로(약하면 원래 각도)
        if (sum.sqrMagnitude > 1e-8f)
        {
            Vector3 d = sum.normalized;
            _dir = _dir == Vector3.zero ? d : Vector3.Slerp(_dir, d, k);
            Quaternion world = Quaternion.LookRotation(_dir, Vector3.up);
            Quaternion baseWorld = _cam.transform.rotation * _baseRot;
            float w = Mathf.Clamp01(lum / 0.6f) * 0.7f;
            _vm.transform.rotation = Quaternion.Slerp(baseWorld, world, w);
        }
        else
        {
            _vm.transform.localRotation = _baseRot;
        }
    }

    /// <summary>URP 점·스포트 조명 감쇠(거리 제곱 · 범위 끝 부드럽게 · 스포트 원뿔).</summary>
    public static float Attenuation(Light l, Vector3 p)
    {
        Vector3 to = p - l.transform.position;
        float d2 = to.sqrMagnitude;
        float r2 = l.range * l.range;
        if (d2 >= r2 || r2 <= 0f) return 0f;
        float f = d2 / r2;
        float smooth = Mathf.Clamp01(1f - f * f);
        float a = smooth * smooth / Mathf.Max(d2, 0.0001f);
        if (l.type == LightType.Spot)
        {
            float cosOuter = Mathf.Cos(l.spotAngle * 0.5f * Mathf.Deg2Rad);
            float cosInner = Mathf.Cos(Mathf.Max(0.01f, l.innerSpotAngle) * 0.5f * Mathf.Deg2Rad);
            float cd = Vector3.Dot(l.transform.forward, to / Mathf.Sqrt(d2));
            float s = Mathf.Clamp01((cd - cosOuter) / Mathf.Max(0.0001f, cosInner - cosOuter));
            a *= s * s;
        }

        return a;
    }

    private void OnDestroy()
    {
        if (_vm != null && _baseIntensity >= 0f)
        {
            _vm.intensity = _baseIntensity;
            _vm.color = _baseColor;
            _vm.transform.localRotation = _baseRot;
        }
    }
}

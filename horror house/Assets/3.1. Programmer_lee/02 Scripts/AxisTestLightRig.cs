using UnityEngine;
using NightDuty;

/// <summary>
/// 조도 축이 오르면 조명이 실제로 꺼지고 색이 변하는지 눈으로 확인하는 임시 검증용 컴포넌트입니다.
/// </summary>
/// <remarks>
/// 이것은 <b>버릴 실험용 코드</b>입니다. 개인 폴더에 있고 Assembly-CSharp에 들어갑니다.
/// 다만 <c>ISpacePresenter</c>를 정식으로 구현해 두었으므로,
/// 클라이언트 담당이 <c>Assets/_Game/Scripts/Presenters/</c> 아래에 실제 프리젠터를 만들 때
/// 이 파일을 그대로 참고할 수 있습니다.
///
/// 수치(구간별 등 개수 · 색온도 · 밝기 배수 · 틴트)는 이 컴포넌트가 들고 있지 않고
/// <see cref="BandTableSO"/> 에셋에 있습니다. MonoBehaviour 인스펙터 값은 Play 모드에서 바꿔도
/// Stop을 누르면 되돌아가지만, ScriptableObject 에셋의 값은 Play 중 수정해도 남습니다.
/// 「등 4개 3200K가 진짜 불안한가」는 플레이하면서 만져 봐야 판단할 수 있으므로 표를 에셋으로 뺐습니다.
///
/// 구간 범위(비균등): Band0=0~24, Band1=25~49, Band2=50~74, Band3=75~89, Band4=90~100.
///
/// 사용법
/// 1. 조명들을 자식으로 가진 오브젝트에 이 컴포넌트를 붙입니다(또는 조명 루트를 지정).
/// 2. <c>BandTableSO</c> 에셋을 만들어 <c>조도 표</c> 칸에 물립니다.
/// 3. 씬 어딘가에 DebugAxisDriver를 붙입니다.
/// 4. 조도 슬라이더를 0에서 100까지 끌면 등이 순서대로 꺼지고 색온도가 내려갑니다.
///    플레이 모드에 들어가지 않아도 됩니다.
/// </remarks>
[AddComponentMenu("NightDuty/Debug/Axis Test Light Rig")]
[DisallowMultipleComponent]
[ExecuteAlways]
public sealed class AxisTestLightRig : MonoBehaviour, ISpacePresenter
{
    [Header("어느 공간으로 취급할지")]
    [Tooltip("DebugAxisDriver가 이 공간으로 방송할 때만 반응합니다.")]
    [SerializeField] private SpaceId _space = SpaceId.Corridor;

    [Header("구간 표")]
    [Tooltip("구간별 등 개수와 색온도. Play 중 수정해도 값이 유지됩니다.")]
    [SerializeField] private BandTableSO _bandTable;

    [Header("조명")]
    [Tooltip("비워 두면 이 오브젝트 자신을 루트로 씁니다. 이 아래의 Light를 전부 모읍니다.")]
    [SerializeField] private Transform _lightRoot;

    [Tooltip("너무 많이 잡히면 여기서 개수를 제한합니다. 0이면 무제한.")]
    [SerializeField] private int _maxLights = 8;

    [Tooltip("수집된 조명. '조명 다시 수집'으로 채웁니다.")]
    [SerializeField] private Light[] _lights;

    /// <remarks>
    /// 조명의 <b>원래</b> intensity입니다. 표의 Intensity는 배수라서 이 원본 값에 곱합니다.
    /// 이 배열이 없어서 light.intensity에 직접 곱하면, 적용이 일어날 때마다 이전 결과에 또 곱해져
    /// 조명이 프레임마다 점점 어두워지는 버그가 납니다(BandProgress는 매 프레임 올 수 있습니다).
    /// 그래서 곱셈의 기준값을 따로 보관합니다.
    /// </remarks>
    [Tooltip("수집 당시의 원본 intensity. 표의 Intensity를 여기에 곱합니다. 직접 만지지 마십시오.")]
    [SerializeField] private float[] _baseIntensities;

    [SerializeField] private bool _verbose = true;

    private Band _currentBand = Band.Band0;
    private float _currentProgress;

    /// <summary>이 프리젠터가 담당하는 공간입니다.</summary>
    public SpaceId Space { get { return _space; } }

    private void OnEnable()
    {
        if (_lights == null || _lights.Length == 0) CollectLights();
        EnsureBaseIntensities();

        // 표가 없으면 조명을 건드리지 않습니다. 경고는 여기서 한 번만 냅니다.
        // Apply()에서 매번 로그를 내면 BandProgress 때문에 콘솔이 프레임마다 채워집니다.
        if (_bandTable == null)
        {
            Debug.LogWarning(
                "[AxisTestLightRig] 조도 표가 비어 있어 조명을 제어하지 않습니다. " +
                "BandTableSO 에셋을 지정하십시오. 메뉴 NightDuty > 조도 표 에셋 생성 으로 만들 수 있습니다.",
                this);
        }

        EventBus.BandChanged += HandleBandChanged;
        EventBus.BandProgress += HandleBandProgress;

        Apply(_currentBand, _currentProgress);
    }

    private void OnDisable()
    {
        // 반드시 해제할 것. EventBus는 static이라 씬을 바꾸거나 오브젝트를 지워도 구독이 살아남고,
        // 파괴된 오브젝트로 이벤트가 날아가면 MissingReferenceException이 납니다.
        EventBus.BandChanged -= HandleBandChanged;
        EventBus.BandProgress -= HandleBandProgress;
    }

    /// <summary>루트 아래의 Light를 모아 이름순으로 정렬하고, 각 조명의 원본 intensity를 함께 기록합니다.</summary>
    /// <remarks>정렬 순서가 매번 같아야 같은 등이 꺼집니다.</remarks>
    [ContextMenu("조명 다시 수집")]
    public void CollectLights()
    {
        Transform root = _lightRoot != null ? _lightRoot : transform;
        Light[] found = root.GetComponentsInChildren<Light>(true);

        System.Array.Sort(found, delegate (Light a, Light b)
        {
            return string.CompareOrdinal(a.name, b.name);
        });

        if (_maxLights > 0 && found.Length > _maxLights)
        {
            Light[] trimmed = new Light[_maxLights];
            System.Array.Copy(found, trimmed, _maxLights);
            found = trimmed;
        }

        _lights = found;

        // 원본 intensity는 반드시 여기서, 아직 아무것도 곱하지 않은 상태로 기록합니다.
        _baseIntensities = new float[_lights.Length];
        for (int i = 0; i < _lights.Length; i++)
        {
            _baseIntensities[i] = _lights[i] != null ? _lights[i].intensity : 0f;
        }

        if (_verbose)
        {
            Debug.Log("[AxisTestLightRig] 조명 " + _lights.Length + "개 수집 — " + _space, this);
        }
    }

    /// <summary>지금 기억하고 있는 구간·진행도를 조명에 다시 적용합니다.</summary>
    [ContextMenu("현재 상태 다시 적용")]
    public void ReapplyCurrent()
    {
        EnsureBaseIntensities();
        Apply(_currentBand, _currentProgress);
    }

    /// <summary>구간이 바뀌었을 때 호출됩니다. 조도 축만 처리합니다.</summary>
    public void OnBandChanged(FearAxis axis, Band from, Band to)
    {
        if (axis != FearAxis.Illuminance) return;

        // from == to 로 오는 경우가 있습니다(DebugAxisDriver의 「전체 다시 방송」, OnEnable 기준값 송출).
        // 같은 상태를 다시 적용해도 결과가 같아야 하므로 Apply는 멱등하게 만들어져 있습니다.
        _currentBand = to;
        _currentProgress = 0f;
        Apply(_currentBand, _currentProgress);
    }

    /// <summary>구간 안에서의 진행도(0~1)가 갱신될 때 호출됩니다.</summary>
    public void OnBandProgress(FearAxis axis, float t01)
    {
        if (axis != FearAxis.Illuminance) return;

        _currentProgress = Mathf.Clamp01(t01);
        Apply(_currentBand, _currentProgress);
    }

    /// <summary>새 날 시작 시 가장 낮은 구간으로 되돌립니다.</summary>
    public void ResetForNewDay()
    {
        _currentBand = Band.Band0;
        _currentProgress = 0f;
        Apply(_currentBand, _currentProgress);
    }

    private void HandleBandChanged(SpaceId space, FearAxis axis, Band from, Band to)
    {
        // EventBus는 전 공간에 방송하므로 자기 공간이 아니면 반드시 무시해야 합니다.
        if (space != _space) return;
        OnBandChanged(axis, from, to);
    }

    private void HandleBandProgress(SpaceId space, FearAxis axis, float t01)
    {
        if (space != _space) return;
        OnBandProgress(axis, t01);
    }

    /// <summary>표에서 읽은 값을 조명에 적용합니다. 몇 번을 호출해도 결과가 같습니다(멱등).</summary>
    private void Apply(Band band, float t01)
    {
        if (_bandTable == null) return;           // 경고는 OnEnable에서 한 번만.
        if (_lights == null || _lights.Length == 0) return;

        EnsureBaseIntensities();

        float progress = Mathf.Clamp01(t01);
        int lit = _bandTable.LitCountFor(_space, band);
        float kelvin = _bandTable.KelvinFor(band, progress);
        float multiplier = _bandTable.IntensityFor(band, progress);
        Color tint = _bandTable.TintFor(band, progress);

        for (int i = 0; i < _lights.Length; i++)
        {
            Light light = _lights[i];
            if (light == null) continue;

            light.enabled = i < lit;
            if (!light.enabled) continue;

            // 색온도는 useColorTemperature가 켜져 있어야 적용됩니다.
            light.useColorTemperature = true;
            light.colorTemperature = kelvin;
            light.color = tint;

            // 원본 값에 배수를 곱합니다. 현재 intensity에 곱하면 호출할 때마다 어두워집니다.
            float baseIntensity = (i < _baseIntensities.Length) ? _baseIntensities[i] : light.intensity;
            light.intensity = baseIntensity * multiplier;
        }
    }

    /// <summary>_baseIntensities가 없거나 _lights와 길이가 어긋나면 다시 채웁니다.</summary>
    /// <remarks>
    /// 길이가 어긋난 상태에서 곱하면 엉뚱한 조명의 기준값을 쓰게 되므로 먼저 맞춥니다.
    /// 이미 배수가 곱해진 뒤라면 그 값이 새 기준이 되지만, 실험용 도구이므로 여기서는 감수합니다.
    /// 정확한 기준이 필요하면 '조명 다시 수집'을 조명이 원래 밝기일 때 실행하십시오.
    /// </remarks>
    private void EnsureBaseIntensities()
    {
        if (_lights == null)
        {
            _baseIntensities = new float[0];
            return;
        }

        if (_baseIntensities != null && _baseIntensities.Length == _lights.Length) return;

        float[] rebuilt = new float[_lights.Length];
        for (int i = 0; i < _lights.Length; i++)
        {
            if (_baseIntensities != null && i < _baseIntensities.Length)
            {
                rebuilt[i] = _baseIntensities[i];
            }
            else
            {
                rebuilt[i] = _lights[i] != null ? _lights[i].intensity : 0f;
            }
        }

        _baseIntensities = rebuilt;
    }
}

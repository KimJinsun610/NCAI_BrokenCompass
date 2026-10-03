using NightDuty;
using UnityEngine;

/// <summary>
/// <b>2026-10-03부터 아무것도 하지 않는다 — <see cref="IlluminanceMap"/>이 대신한다.</b>
/// 이 컴포넌트는 구간마다 등을 8/6/4/2/0개로 끄고 등 색온도를 6500K→1200K로 내렸는데, 9.20V가 「켜진 등 개수」를 지웠고
/// 최종 기획서는 색을 Volume White Balance로, 소등을 「좌측 복도 끝부터 구간 수만큼 · 3구간 과학실 앞 복도」로 정했다.
/// 씬(JudgeLights 아래 5개)과 옛 단서 조건(<c>AnomalyCueDirector</c>, 새 편성에서는 꺼짐)이 아직 참조하므로 클래스는 남긴다.
/// <para>옛 설명: 한 공간의 조도 연출을 맡는 등 그룹. 구간이 바뀌면 켜 둘 등 개수와 색온도를 바꾼다.</para>
/// <list type="bullet">
/// <item>켤 개수·색온도는 <see cref="BandTableSO"/>(전 공간 공통 표)가 정한다. 여기서 수치를 다시 쓰지 않는다.</item>
/// <item><b>실시간 라이트만</b> 켜고 끌 수 있다. Mixed·Baked는 꺼도 화면이 어두워지지 않는다 — 에디터 메뉴로 변환한다.</item>
/// <item>같은 구간을 여러 번 받아도 결과가 같아야 한다(<c>from == to</c> 재방송이 온다).</item>
/// </list>
/// <b>판정은 등 개수를 보지 않는다.</b> 9.20V가 "실제로 켜진 등 개수"를 판정 조건에서 없앴고,
/// 한때 그렇게 쓸 것으로 적혀 있던 C5는 <c>FlashlightCondition</c>, S4는 <c>GazeCondition</c>으로 판정한다.
/// 이 컴포넌트는 표가 정한 개수를 <b>그리기만</b> 한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class SpaceLights : MonoBehaviour, ISpacePresenter
{
    [SerializeField] private SpaceId space = SpaceId.Corridor;

    [Tooltip("이 공간의 등. 앞쪽부터 꺼진다(구간이 오를수록 뒤쪽만 남는다).")]
    [SerializeField] private Light[] lights = new Light[0];

    [Tooltip("비우면 Resources의 BandTable을 찾는다.")]
    [SerializeField] private BandTableSO bandTable;

    private float[] _baseIntensity;
    private int _lit;

    /// <inheritdoc/>
    public SpaceId Space => space;

    /// <summary>
    /// 지금 켜져 있는 등 개수. <b>연출 확인·디버그용이다.</b>
    /// <para>
    /// <b>2026-09-21 기준 읽는 코드는 프로젝트 전체에 0곳.</b> 9.20V가 등 개수를 판정 조건에서 없앴으므로
    /// <b>새 코드에서 판정에 쓰지 마십시오</b> — 판정은 카드의 조건 컴포넌트(손전등·응시 등)가 맡는다.
    /// 지우지 않고 남겨 둔 것은 연출·툴 쪽에서 현재 상태를 읽어 볼 여지가 있어서다.
    /// </para>
    /// </summary>
    public int LitCount => _lit;

    /// <summary>옛 동작을 끈다(늘 true). 시험이 옛 동작을 확인해야 하면 false로 바꿔 쓴다.</summary>
    public static bool Retired { get; set; } = true;

    private void OnEnable()
    {
        CacheIntensity();
        if (bandTable == null) bandTable = Resources.Load<BandTableSO>("BandTable");
        _lit = lights.Length;   // 끄지 않으므로 늘 전부 켜져 있다.
        // 구간 이벤트를 받지 않는다(2026-10-03) — 받으면 IlluminanceMap과 등을 두고 다툰다.
    }

    private void CacheIntensity()
    {
        // 원본 세기를 따로 둔다 — 현재 값에 배수를 곱하면 매 프레임 지수적으로 어두워진다.
        if (_baseIntensity != null && _baseIntensity.Length == lights.Length) return;
        _baseIntensity = new float[lights.Length];
        for (int i = 0; i < lights.Length; i++)
        {
            _baseIntensity[i] = lights[i] != null ? lights[i].intensity : 0f;
        }
    }

    /// <inheritdoc/>
    public void OnBandChanged(FearAxis axis, Band from, Band to)
    {
        // 2026-10-03: IlluminanceMap이 대신한다. 아래 옛 동작(등 개수·색온도)은 되살리지 마십시오.
        if (Retired || axis != FearAxis.Illuminance || bandTable == null) return;

        CacheIntensity();
        int lit = bandTable.LitCountFor(space, to);
        float kelvin = bandTable.KelvinFor(to, 0f);
        float scale = bandTable.IntensityFor(to, 0f);
        Color tint = bandTable.TintFor(to, 0f);

        _lit = 0;
        for (int i = 0; i < lights.Length; i++)
        {
            Light l = lights[i];
            if (l == null) continue;

            bool on = i >= lights.Length - lit;   // 뒤쪽 등이 남는다
            l.enabled = on;
            if (!on) continue;

            _lit++;
            l.useColorTemperature = true;
            l.colorTemperature = kelvin;
            l.color = tint;
            l.intensity = _baseIntensity[i] * scale;
        }
    }

    /// <inheritdoc/>
    public void OnBandProgress(FearAxis axis, float progress01)
    {
        // 구간 안 보간(색온도 서서히 이동)은 아직 쓰지 않는다. 필요해지면 여기서 KelvinFor(band, progress01)을 쓴다.
    }

    /// <inheritdoc/>
    public void ResetForNewDay()
    {
        OnBandChanged(FearAxis.Illuminance, Band.Band0, Band.Band0);
    }
}

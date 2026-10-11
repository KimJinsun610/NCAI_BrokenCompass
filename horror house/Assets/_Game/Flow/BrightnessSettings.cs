using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// 화면 밝기 조절. <b>설정 하나로 씬의 모든 포스트 볼륨을 함께 올리고 내린다.</b>
///
/// <para>
/// 조절하는 것은 <see cref="ColorAdjustments.postExposure"/>다. 라이트맵이나 조명을 건드리지 않으므로
/// 재베이크가 필요 없고, 밤의 분위기(안개·블룸·색)는 그대로 둔 채 눈에 보이는 밝기만 바뀐다.
/// </para>
///
/// <para>
/// <b>에셋을 고치지 않는다.</b> <see cref="Volume.profile"/>(런타임 사본)에만 쓴다 —
/// <c>sharedProfile</c>에 쓰면 플레이를 꺼도 프로필 에셋에 값이 남아 버린다(2026-09-23 실측).
/// 그래서 프로필에 적힌 값이 늘 <b>기준</b>이고, 이 설정은 거기에 얹는 <b>보정</b>이다.
/// </para>
///
/// <para>
/// 씬에 놓지 않아도 된다 — 씬이 로드될 때 스스로 선다. 값은 <see cref="PrefsKey"/>로 저장된다.
/// </para>
///
/// <para>
/// 99차(민: 「설정에서 라이팅 수치를 변경하면 화면이 뿌예지는 버그가 아직 있어」): 실내·실외 보정 프로필의 Lift(리프트·감마·게인)가
/// 검은 바닥을 청록 회색(약 34/255)으로 띄워 둔다. 노출만 내리면 장면은 어두워지는데 이 바닥은 그대로라, 어두운 끝(옛 −1.5EV)에서 화면 전체가
/// 그 회색 한 장으로 뭉개졌다(보안실 PlayScene 재현). 이제 어두운 쪽은 노출을 덜 내리고(<see cref="MinOffset"/> −1) 내린 만큼 리프트도 함께 내려
/// (<see cref="LiftPerEv"/>) 검정이 검정으로 남는다.
/// </para>
///
/// <para>
/// 100차(민: 「화면이 좀 덜 뿌옜으면 좋겠어 · 밝기를 올리면 좀 뿌예지는 거」): 밝기 값과 상관없이 늘 <b>안개 걷기</b>를 얹는다 — 리프트 바닥을 내리고
/// (<see cref="DehazeLift"/>) 대비를 올리고(<see cref="DehazeContrast"/>) 그만큼 어두워진 중간 톤을 노출로 조금 되돌린다(<see cref="DehazeExposure"/>).
/// 보안실 실측(손전등 켬, 밝기 0.5): 검정 바닥 34 → 0, 중간 45 → 28, 밝은 쪽 88 → 93/255 · 표준편차 24.5 → 37. 밝은 쪽 끝에서도 리프트를 조금 더 내리고 대비를 더해
/// (<see cref="LiftPerEvBright"/>·<see cref="ContrastPerEvBright"/>) 노출을 올려도 바닥이 뜨지 않는다(옛 최대 바닥 34 → 2).
/// </para>
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-900)]
public sealed class BrightnessSettings : MonoBehaviour
{
    /// <summary>저장 키.</summary>
    public const string PrefsKey = "nightduty.brightness";

    /// <summary>슬라이더 0일 때의 보정(EV). 어두운 쪽 끝(99차 −1.5 → −1).</summary>
    public const float MinOffset = -1f;

    /// <summary>99차: 노출을 1EV 내릴 때 리프트(밝기 축 w)를 함께 내리는 양 — 어두운 쪽(100차: 안개 걷기가 이미 바닥을 내려 0.025 → 0.011).</summary>
    public const float LiftPerEv = 0.011f;

    /// <summary>100차: 늘 얹는 리프트 보정(w) — 프로필의 청록 회색 바닥(+0.01)을 거의 검정으로.</summary>
    public const float DehazeLift = -0.014f;

    /// <summary>100차: 늘 얹는 대비(색 조정 Contrast, 프로필 값에 더함).</summary>
    public const float DehazeContrast = 8f;

    /// <summary>100차: 늘 얹는 노출(EV) — 바닥을 내리고 대비를 올려 어두워진 중간 톤을 되돌린다.</summary>
    public const float DehazeExposure = 0.3f;

    /// <summary>100차: 밝은 쪽에서 1EV 올릴 때마다 리프트를 더 내리는 양.</summary>
    public const float LiftPerEvBright = 0.006f;

    /// <summary>100차: 밝은 쪽에서 1EV 올릴 때마다 더하는 대비.</summary>
    public const float ContrastPerEvBright = 3f;

    /// <summary>슬라이더 1일 때의 보정(EV). 밝은 쪽 끝(100차 1.5 → 1.2 — 안개 걷기 노출 +0.3을 늘 얹어 끝의 총량은 옛 +1.5 그대로).</summary>
    public const float MaxOffset = 1.2f;

    /// <summary>기본값. <b>0.5가 「제작자가 맞춰 둔 그대로」</b>다.</summary>
    public const float Default = 0.5f;

    private static float s_value = Default;
    private static bool s_loaded;

    // 프로필마다 «원래 적혀 있던 노출»을 기억한다. 이걸 안 하면 조절할 때마다 값이 누적된다.
    private static readonly Dictionary<ColorAdjustments, float> s_baseline = new Dictionary<ColorAdjustments, float>();
    private static readonly Dictionary<LiftGammaGain, Vector4> s_liftBaseline = new Dictionary<LiftGammaGain, Vector4>();
    private static readonly Dictionary<ColorAdjustments, float> s_contrastBaseline = new Dictionary<ColorAdjustments, float>();

    // 86차: 밝기를 얹는 볼륨 — 씬에 처음부터 있고 프로필 에셋이 노출(postExposure)을 정하는 것만(실내·실외 보정).
    // 옛 Apply는 씬의 모든 볼륨을 훑어, 조도 톤(IlluminanceMap)·조우 화면 효과처럼 런타임에 만든 볼륨까지
    // ① v.profile로 사본을 만들어 그 연출이 더는 화면에 닿지 않게 하고(톤이 굳음) ② 노출을 「0 + 보정」으로 덮어
    // 실내 보정(2.5)을 지웠다 — 설정에서 밝기를 움직이면 화면이 뿌옇게 바랬다(민 제보, PlayScene 재현: 보안실 노출 4 → 1.2).
    private static readonly HashSet<Volume> s_targets = new HashSet<Volume>();

    /// <summary>지금 밝기(0~1). 0.5가 기본이다.</summary>
    public static float Value
    {
        get
        {
            Load();
            return s_value;
        }
    }

    /// <summary>지금 보정값(EV). 0.5 = 0(제작 그대로), 0 = <see cref="MinOffset"/>, 1 = <see cref="MaxOffset"/>(양쪽 범위가 달라 반씩 나눠 잇는다).</summary>
    public static float Offset { get { return OffsetFor(Value); } }

    /// <summary>슬라이더 값(0~1)의 보정값(EV).</summary>
    public static float OffsetFor(float value)
    {
        value = Mathf.Clamp01(value);
        return value < Default
            ? Mathf.Lerp(MinOffset, 0f, value / Default)
            : Mathf.Lerp(0f, MaxOffset, (value - Default) / (1f - Default));
    }

    /// <summary>값이 바뀔 때. 화면의 숫자 표시가 듣는다.</summary>
    public static event System.Action<float> Changed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        // 플레이를 껐다 켜면 볼륨 인스턴스가 새로 생긴다. 옛 기준값을 들고 있으면 안 된다.
        s_baseline.Clear();
        s_liftBaseline.Clear();
        s_contrastBaseline.Clear();
        s_targets.Clear();
        s_loaded = false;
        Changed = null;

        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InstallForFirstScene()
    {
        EnsureFor(SceneManager.GetActiveScene());
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureFor(scene);
    }

    private static void EnsureFor(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded) return;

        foreach (BrightnessSettings existing in FindObjectsByType<BrightnessSettings>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (existing.gameObject.scene == scene) return;
        }

        // 포스트 볼륨이 없는 씬(로딩·결과 등)에는 세우지 않는다.
        bool hasVolume = false;
        foreach (Volume v in FindObjectsByType<Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (v.gameObject.scene == scene) { hasVolume = true; break; }
        }

        if (!hasVolume) return;

        GameObject go = new GameObject("BrightnessSettings (auto)");
        SceneManager.MoveGameObjectToScene(go, scene);
        go.AddComponent<BrightnessSettings>();
    }

    private void Start()
    {
        CollectTargets(gameObject.scene);
        Apply();
    }

    /// <summary>
    /// 86차: 그 씬에 지금 있는 볼륨 중 프로필 에셋(<see cref="Volume.sharedProfile"/>)이 노출을 켜 둔 것만 밝기 대상으로 기억한다.
    /// 이 뒤에 런타임으로 생기는 볼륨(조도 톤·조우 화면 효과·컷신)은 건드리지 않는다.
    /// </summary>
    private static void CollectTargets(Scene scene)
    {
        foreach (Volume v in FindObjectsByType<Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (v.gameObject.scene != scene || s_targets.Contains(v)) continue;
            VolumeProfile source = v.sharedProfile;
            ColorAdjustments color;
            if (source == null || !source.TryGet<ColorAdjustments>(out color) || !color.postExposure.overrideState) continue;
            s_targets.Add(v);
        }
    }

    /// <summary>86차: 밝기를 얹는 볼륨 수(시험·검수).</summary>
    public static int TargetCount
    {
        get
        {
            s_targets.RemoveWhere(v => v == null);
            return s_targets.Count;
        }
    }

    /// <summary>밝기를 바꾸고 저장한다. 0~1.</summary>
    public static void Set(float value)
    {
        Load();

        float next = Mathf.Clamp01(value);
        if (Mathf.Abs(next - s_value) < 0.0005f) return;

        s_value = next;
        PlayerPrefs.SetFloat(PrefsKey, s_value);
        PlayerPrefs.Save();

        Apply();

        if (Changed != null) Changed(s_value);
    }

    /// <summary>기본값으로 되돌린다.</summary>
    public static void Reset()
    {
        Set(Default);
    }

    /// <summary>지금 값을 씬의 모든 포스트 볼륨에 바른다.</summary>
    public static void Apply()
    {
        Load();

        float offset = Offset;
        s_targets.RemoveWhere(v => v == null);
        foreach (Volume v in s_targets)
        {
            // profile(사본)을 쓴다. sharedProfile에 쓰면 에셋이 더럽혀진다.
            VolumeProfile profile = v.HasInstantiatedProfile() || v.sharedProfile != null ? v.profile : null;
            if (profile == null) continue;

            ColorAdjustments color;
            if (!profile.TryGet<ColorAdjustments>(out color)) continue;

            float baseline;
            if (!s_baseline.TryGetValue(color, out baseline))
            {
                baseline = color.postExposure.value;
                s_baseline[color] = baseline;
            }

            color.postExposure.overrideState = true;
            color.postExposure.value = baseline + DehazeExposure + offset;

            // 100차: 안개 걷기 — 대비(프로필 값 + 늘 + 밝은 쪽 더).
            float contrast;
            if (!s_contrastBaseline.TryGetValue(color, out contrast))
            {
                contrast = color.contrast.overrideState ? color.contrast.value : 0f;
                s_contrastBaseline[color] = contrast;
            }

            color.contrast.overrideState = true;
            color.contrast.value = Mathf.Clamp(contrast + DehazeContrast + ContrastPerEvBright * Mathf.Max(0f, offset), -100f, 100f);

            // 99차: 어두운 쪽은 리프트도 함께 내린다(청록 회색 바닥에 화면이 뭉개지지 않게). 100차: 늘 바닥을 내리고 밝은 쪽에서도 조금 더. 리프트를 안 쓰는 프로필은 그대로.
            LiftGammaGain lgg;
            if (!profile.TryGet<LiftGammaGain>(out lgg) || !lgg.lift.overrideState) continue;
            Vector4 lift;
            if (!s_liftBaseline.TryGetValue(lgg, out lift))
            {
                lift = lgg.lift.value;
                s_liftBaseline[lgg] = lift;
            }

            lift.w += DehazeLift + LiftPerEv * Mathf.Min(0f, offset) - LiftPerEvBright * Mathf.Max(0f, offset);
            lgg.lift.value = lift;
        }
    }

    private static void Load()
    {
        if (s_loaded) return;

        s_loaded = true;
        s_value = Mathf.Clamp01(PlayerPrefs.GetFloat(PrefsKey, Default));
    }
}

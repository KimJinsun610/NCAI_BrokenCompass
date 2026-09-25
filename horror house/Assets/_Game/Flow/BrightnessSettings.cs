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
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-900)]
public sealed class BrightnessSettings : MonoBehaviour
{
    /// <summary>저장 키.</summary>
    public const string PrefsKey = "nightduty.brightness";

    /// <summary>슬라이더 0일 때의 보정(EV). 어두운 쪽 끝.</summary>
    public const float MinOffset = -1.5f;

    /// <summary>슬라이더 1일 때의 보정(EV). 밝은 쪽 끝.</summary>
    public const float MaxOffset = 1.5f;

    /// <summary>기본값. <b>0.5가 「제작자가 맞춰 둔 그대로」</b>다.</summary>
    public const float Default = 0.5f;

    private static float s_value = Default;
    private static bool s_loaded;

    // 프로필마다 «원래 적혀 있던 노출»을 기억한다. 이걸 안 하면 조절할 때마다 값이 누적된다.
    private static readonly Dictionary<ColorAdjustments, float> s_baseline = new Dictionary<ColorAdjustments, float>();

    /// <summary>지금 밝기(0~1). 0.5가 기본이다.</summary>
    public static float Value
    {
        get
        {
            Load();
            return s_value;
        }
    }

    /// <summary>지금 보정값(EV). 화면에 숫자로 보여 줄 때 쓴다.</summary>
    public static float Offset { get { return Mathf.Lerp(MinOffset, MaxOffset, Value); } }

    /// <summary>값이 바뀔 때. 화면의 숫자 표시가 듣는다.</summary>
    public static event System.Action<float> Changed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        // 플레이를 껐다 켜면 볼륨 인스턴스가 새로 생긴다. 옛 기준값을 들고 있으면 안 된다.
        s_baseline.Clear();
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
        Apply();
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
        foreach (Volume v in FindObjectsByType<Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None))
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
            color.postExposure.value = baseline + offset;
        }
    }

    private static void Load()
    {
        if (s_loaded) return;

        s_loaded = true;
        s_value = Mathf.Clamp01(PlayerPrefs.GetFloat(PrefsKey, Default));
    }
}

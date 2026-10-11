using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 86차(민: 「마우스 감도 조절, 볼륨을 esc 누르면 지금 라이팅처럼 조절이 가능한 인터페이스를 추가해줘」) — 일시정지 화면에서 바꾸는 플레이어 설정 둘.
/// <list type="bullet">
/// <item><b>마우스 감도</b>: 0~1(0.5 = 제작자가 맞춘 그대로). 김진선님 <c>FPController.mouseSensitivity</c>(인스펙터 값)를 기준으로 <see cref="MouseMultiplier"/>배 —
/// 0이면 1/3배, 1이면 3배(가운데에서 양쪽으로 같은 비율). 김진선님 스크립트는 고치지 않고 값만 바꾼다(기준은 처음 본 값을 기억).</item>
/// <item><b>볼륨</b>: 0~1 전체 음량(<see cref="AudioListener.volume"/>, 기본 1). 일시정지·컷신의 <c>AudioListener.pause</c>와 겹치지 않는다.</item>
/// </list>
/// 값은 PlayerPrefs에 저장되고 씬이 바뀌어도 이어진다. 화면 밝기는 <see cref="BrightnessSettings"/>.
/// </summary>
public static class PlayerOptions
{
    /// <summary>마우스 감도 저장 키.</summary>
    public const string MouseKey = "nightduty.mouse";

    /// <summary>볼륨 저장 키.</summary>
    public const string VolumeKey = "nightduty.volume";

    /// <summary>마우스 감도 기본값(제작자 값 그대로).</summary>
    public const float MouseDefault = 0.5f;

    /// <summary>볼륨 기본값.</summary>
    public const float VolumeDefault = 1f;

    /// <summary>감도 슬라이더 끝에서의 배수(양쪽 대칭 — 0이면 1/이 값).</summary>
    public const float MouseRange = 3f;

    private static float s_mouse = MouseDefault;
    private static float s_volume = VolumeDefault;
    private static bool s_loaded;
    private static readonly Dictionary<FPController, Vector2> s_base = new Dictionary<FPController, Vector2>();

    /// <summary>마우스 감도(0~1).</summary>
    public static float Mouse
    {
        get
        {
            Load();
            return s_mouse;
        }
    }

    /// <summary>볼륨(0~1).</summary>
    public static float Volume
    {
        get
        {
            Load();
            return s_volume;
        }
    }

    /// <summary>감도 배수 — 0.5에서 1, 0에서 1/<see cref="MouseRange"/>, 1에서 <see cref="MouseRange"/>.</summary>
    public static float MouseMultiplier
    {
        get { return MultiplierOf(Mouse); }
    }

    /// <summary>슬라이더 값의 감도 배수.</summary>
    public static float MultiplierOf(float value)
    {
        return Mathf.Pow(MouseRange, (Mathf.Clamp01(value) - 0.5f) * 2f);
    }

    /// <summary>값이 바뀔 때(슬라이더 숫자 표시가 듣는다).</summary>
    public static event System.Action Changed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        s_loaded = false;
        s_base.Clear();
        Changed = null;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void ApplyForFirstScene()
    {
        ApplyVolume();
        PauseMenuExtras.EnsureWatcher();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyVolume();
    }

    /// <summary>마우스 감도를 바꾸고 저장한다(0~1).</summary>
    public static void SetMouse(float value)
    {
        Load();
        float next = Mathf.Clamp01(value);
        if (Mathf.Abs(next - s_mouse) < 0.0005f) return;
        s_mouse = next;
        PlayerPrefs.SetFloat(MouseKey, s_mouse);
        PlayerPrefs.Save();
        ApplyMouse();
        if (Changed != null) Changed();
    }

    /// <summary>볼륨을 바꾸고 저장한다(0~1).</summary>
    public static void SetVolume(float value)
    {
        Load();
        float next = Mathf.Clamp01(value);
        if (Mathf.Abs(next - s_volume) < 0.0005f) return;
        s_volume = next;
        PlayerPrefs.SetFloat(VolumeKey, s_volume);
        PlayerPrefs.Save();
        ApplyVolume();
        if (Changed != null) Changed();
    }

    /// <summary>지금 감도를 씬의 모든 1인칭 조작기에 바른다(처음 본 조작기는 인스펙터 값을 기준으로 기억).</summary>
    public static void ApplyMouse()
    {
        float k = MouseMultiplier;
        List<FPController> gone = null;
        foreach (KeyValuePair<FPController, Vector2> kv in s_base)
        {
            if (kv.Key != null) continue;
            if (gone == null) gone = new List<FPController>();
            gone.Add(kv.Key);
        }

        if (gone != null) for (int i = 0; i < gone.Count; i++) s_base.Remove(gone[i]);

        foreach (FPController fp in Object.FindObjectsByType<FPController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            Vector2 baseline;
            if (!s_base.TryGetValue(fp, out baseline))
            {
                baseline = fp.mouseSensitivity;
                s_base[fp] = baseline;
            }

            Vector2 want = baseline * k;
            if (fp.mouseSensitivity != want) fp.mouseSensitivity = want;
        }
    }

    /// <summary>새로 생긴 조작기가 있으면 감도를 바른다(감시자가 가끔 부른다).</summary>
    public static void ApplyMouseToNew()
    {
        foreach (FPController fp in Object.FindObjectsByType<FPController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!s_base.ContainsKey(fp))
            {
                ApplyMouse();
                return;
            }
        }
    }

    /// <summary>지금 볼륨을 바른다.</summary>
    public static void ApplyVolume()
    {
        AudioListener.volume = Volume;
    }

    private static void Load()
    {
        if (s_loaded) return;
        s_loaded = true;
        s_mouse = Mathf.Clamp01(PlayerPrefs.GetFloat(MouseKey, MouseDefault));
        s_volume = Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey, VolumeDefault));
    }
}

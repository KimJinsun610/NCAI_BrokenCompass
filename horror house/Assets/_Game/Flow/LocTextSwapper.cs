using NightDuty;
using TMPro;
using UnityEngine;

/// <summary>
/// 화면 글자 바꿈 장치(2026-11-22, 로컬라이징) — 씬·프리팹에 저장된 라벨, 한 번만 만드는 안내처럼 「누가 언제 썼는지 모르는」 TMP 글을 지금 언어로 바꾼다.
/// <list type="bullet">
/// <item>번역표(<see cref="Loc.Swap"/>)에 있는 글만 바꾼다 — 한국어면 영어로, 언어를 되돌리면 그 영어를 다시 한국어로. 표에 없는 글(시계·숫자·조합 문장)은 건드리지 않는다.</item>
/// <item>씬을 고치지 않는다 — 실행 중 글자만 바꾼다(씬·프리팹 파일은 한국어 그대로).</item>
/// <item>도는 때: 씬이 열린 직후 · 언어가 바뀐 순간 · 그 뒤로 <see cref="Interval"/>초마다(늦게 생기는 UI). 계속 바뀌는 글(자막·태블릿)은 쓰는 코드가 직접 <c>Loc</c>를 거친다 — 여기에만 맡기면 한 틈 한국어가 보인다.</item>
/// <item>한글 글꼴이 없는 TMP에 한국어를 되돌려 쓸 때는 <see cref="GameLanguage.FitFont"/>로 글꼴을 맞춘다.</item>
/// </list>
/// 씬에 놓지 않아도 된다 — 첫 씬 뒤 스스로 하나 생기고 씬을 넘어 산다(DontDestroyOnLoad, DontSave 아님).
/// </summary>
[DisallowMultipleComponent]
public sealed class LocTextSwapper : MonoBehaviour
{
    /// <summary>늦게 생기는 UI를 잡는 주기(실제 초).</summary>
    public const float Interval = 0.25f;

    private static LocTextSwapper s_instance;
    private float _next;

    /// <summary>마지막 한 바퀴에서 바꾼 글 수(검수).</summary>
    public static int LastSwapped { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (s_instance != null) return;
        GameObject go = new GameObject("LocTextSwapper (auto)");
        DontDestroyOnLoad(go);
        s_instance = go.AddComponent<LocTextSwapper>();
    }

    private void OnEnable()
    {
        Loc.Changed += SwapNow;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        _next = 0f;
    }

    private void OnDisable()
    {
        Loc.Changed -= SwapNow;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        if (s_instance == this) s_instance = null;
    }

    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        _next = 0f;   // 다음 프레임에 바로 한 바퀴(그 씬의 Awake·Start가 글을 쓴 뒤)
    }

    private void LateUpdate()
    {
        if (Time.unscaledTime < _next) return;
        _next = Time.unscaledTime + Interval;
        SwapNow();
    }

    /// <summary>지금 열린 모든 TMP 글을 한 바퀴 바꾼다.</summary>
    public static void SwapNow()
    {
        int n = 0;
        foreach (TMP_Text t in FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t == null) continue;
            string cur = t.text;
            string swapped = Loc.Swap(cur);
            if (swapped == null || swapped == cur) continue;
            GameLanguage.FitFont(t, swapped);
            t.text = swapped;
            n++;
        }

        LastSwapped = n;
    }
}

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 메인 화면(HUD_Main) 버튼은 글자가 다 나타난 뒤에야 누를 수 있다.
/// <para>
/// START·QUIT 글자는 프리팹의 DOTween(From Fade)으로 서서히 나타나는데, 버튼은 처음부터 켜져 있어
/// 글자가 보이기도 전에 눌렸다. 버튼마다 자식 글자(TMP)의 알파가 <see cref="ShownAlpha"/> 이상이 될 때까지
/// <c>interactable</c>을 꺼 둔다(꺼져 있으면 호버·클릭 모두 받지 않는다). 한 번 켜지면 다시 끄지 않는다.
/// </para>
/// 프리팹·씬은 고치지 않는다 — 씬이 열릴 때 이름이 「HUD_Main」으로 시작하는 루트에 스스로 붙는다.
/// </summary>
[DisallowMultipleComponent]
public sealed class MainMenuRevealGate : MonoBehaviour
{
    /// <summary>이 알파 이상이면 다 나타난 것으로 본다(프리팹 끝 값이 0.99998).</summary>
    private const float ShownAlpha = 0.99f;

    /// <summary>
    /// QUIT은 이 알파면 켠다 — 프리팹 페이드가 길어(3초 대기 + 4초, OutQuad) 끝까지 기다리면 7초 가까이 걸렸다.
    /// 0.8이면 시작 약 5.2초(끝까지보다 1.4초쯤 빠름), 글자는 거의 다 보인다.
    /// </summary>
    private const float QuitShownAlpha = 0.8f;

    private const string QuitButtonName = "Btn_Quit";

    /// <summary>시작 직후 몇 초는 무조건 막는다(트윈이 시작 값을 쓰기 전 한 프레임 대비).</summary>
    private const float MinLockSeconds = 0.2f;

    /// <summary>글자가 끝내 다 안 나타나도(트윈이 빠졌을 때) 이 시간이 지나면 켠다.</summary>
    private const float MaxLockSeconds = 12f;

    private const string HudPrefix = "HUD_Main";

    private sealed class Entry
    {
        public Button Button;
        public TMP_Text Label;
        public Selectable.Transition Transition;
        public float NeedAlpha;
    }

    private readonly List<Entry> _pending = new List<Entry>();
    private float _elapsed;

    // ── 자동 설치 ─────────────────────────────────────────────

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Install()
    {
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
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (!root.name.StartsWith(HudPrefix)) continue;
            if (root.GetComponent<MainMenuRevealGate>() == null) root.AddComponent<MainMenuRevealGate>();
        }
    }

    // ── 동작 ──────────────────────────────────────────────────

    private void OnEnable()
    {
        _pending.Clear();
        _elapsed = 0f;
        foreach (Button button in GetComponentsInChildren<Button>(true))
        {
            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            if (label == null) continue;   // 글자 없는 버튼은 그대로

            // 잠긴 동안 비활성 모양(씬 버튼은 비활성 스프라이트가 ui_select라 「선택됨」처럼 보였다)이
            // 나오지 않게, 상태 전환을 잠시 끄고 평소 그림으로 둔다. 풀 때 원래 전환으로 되돌린다.
            var entry = new Entry
            {
                Button = button,
                Label = label,
                Transition = button.transition,
                NeedAlpha = button.name == QuitButtonName ? QuitShownAlpha : ShownAlpha,
            };
            button.transition = Selectable.Transition.None;
            if (button.targetGraphic is Image image) image.overrideSprite = null;
            button.interactable = false;
            _pending.Add(entry);
        }
    }

    private void Update()
    {
        if (_pending.Count == 0)
        {
            enabled = false;
            return;
        }

        // 일시정지·timeScale과 무관하게 센다(메인 화면은 timeScale 1이지만 안전하게)
        _elapsed += Time.unscaledDeltaTime;
        if (_elapsed < MinLockSeconds) return;

        bool timeUp = _elapsed >= MaxLockSeconds;
        for (int i = _pending.Count - 1; i >= 0; i--)
        {
            Entry e = _pending[i];
            if (e.Button == null)
            {
                _pending.RemoveAt(i);
                continue;
            }

            if (timeUp || Shown(e.Label, e.NeedAlpha))
            {
                e.Button.transition = e.Transition;
                e.Button.interactable = true;
                _pending.RemoveAt(i);
            }
        }
    }

    private static bool Shown(TMP_Text label, float need)
    {
        return label == null || (label.color.a >= need && label.alpha >= need);
    }
}

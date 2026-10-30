using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 메인 화면(HUD_Main) OPTIONS 버튼 — 2026-10-29, 김진선님: 「그 옵션을 메인 화면에서도 조작 가능하고 인게임 적용 되게」.
/// <list type="bullet">
/// <item>START · <b>OPTIONS</b> · QUIT. OPTIONS는 QUIT 버튼을 복제해 QUIT 자리에 두고, QUIT은 START↔QUIT 간격만큼 아래로 옮긴다(모양·글꼴·나타나는 연출이 같다).</item>
/// <item>누르면 일시정지 화면 프리팹(<see cref="MainMenuOptionsLook"/>)을 한 벌 세워 <see cref="PauseMenuExtras.MakeStandalone"/> — 설정 화면(LIGHT · MOUSE · VOLUME · BACK)만.
/// BACK·Esc로 닫는다. 열려 있는 동안 메인 화면 버튼은 감추고 눌리지 않는다.</item>
/// <item>값은 일시정지 화면과 같은 저장소(<see cref="BrightnessSettings"/>·<see cref="PlayerOptions"/>, PlayerPrefs)라 근무 씬이 열릴 때 그대로 적용된다
/// (밝기 = 씬 보정 볼륨, 감도 = 새 1인칭 조작기, 볼륨 = 씬마다 <c>AudioListener.volume</c>).</item>
/// <item>OPTIONS도 QUIT처럼 글자가 나타난 뒤에야 눌린다 — QUIT이 눌릴 수 있게 된 순간 함께 풀린다(<c>MainMenuRevealGate</c>가 먼저 잠근 상태를 복제했을 수 있어서).</item>
/// </list>
/// 프리팹·씬은 고치지 않는다 — 씬이 열릴 때 이름이 「HUD_Main」으로 시작하는 루트에 스스로 붙는다.
/// </summary>
[DisallowMultipleComponent]
public sealed class MainMenuOptions : MonoBehaviour
{
    private const string HudPrefix = "HUD_Main";
    private const string StartButtonName = "Btn_Start";
    private const string QuitButtonName = "Btn_Quit";

    /// <summary>버튼 글자.</summary>
    public const string Label = "OPTIONS";

    /// <summary>복제한 버튼 폭에 더하는 양(px) — QUIT보다 글자가 길어 누르는 자리를 넓힌다.</summary>
    public const float ExtraWidth = 60f;

    private Button _quit;
    private Button _options;
    private bool _released;
    private GameObject _screen;
    private GraphicRaycaster _raycaster;
    private TMP_Text _quitLabel;
    private TMP_Text _label;
    private bool _fading = true;

    /// <summary>덧붙인 OPTIONS 버튼(검수).</summary>
    public Button OptionsButton
    {
        get { return _options; }
    }

    /// <summary>세운 설정 화면(한 번도 안 열었으면 null, 검수).</summary>
    public GameObject Screen
    {
        get { return _screen; }
    }

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
            if (root.GetComponent<MainMenuOptions>() != null) continue;
            MainMenuOptions options = root.AddComponent<MainMenuOptions>();
            options.Build();
        }
    }

    // ── 버튼 ─────────────────────────────────────────────────

    private void Build()
    {
        MainMenuOptionsLook look = MainMenuOptionsLook.Load();
        if (look == null || look.pauseScreen == null || look.pauseScreen.GetComponent<PauseMenu>() == null)
        {
            Debug.LogWarning("[MainMenuOptions] Resources/MainMenuOptionsLook의 일시정지 화면 프리팹이 비어 있습니다 — 메인 화면에 OPTIONS를 붙이지 않습니다.");
            return;
        }

        Button start = FindButton(StartButtonName);
        _quit = FindButton(QuitButtonName);
        if (start == null || _quit == null)
        {
            Debug.LogWarning("[MainMenuOptions] 메인 화면에서 Btn_Start·Btn_Quit을 찾지 못했습니다 — OPTIONS를 붙이지 않습니다.");
            return;
        }

        RectTransform quitRt = (RectTransform)_quit.transform;
        float step = quitRt.localPosition.y - ((RectTransform)start.transform).localPosition.y;   // START → QUIT 간격(아래로 음수)

        GameObject clone = Instantiate(_quit.gameObject, quitRt.parent, false);
        clone.name = "Btn_Options";
        clone.transform.SetSiblingIndex(quitRt.GetSiblingIndex());
        RectTransform rt = (RectTransform)clone.transform;
        rt.sizeDelta += new Vector2(ExtraWidth, 0f);
        quitRt.anchoredPosition += new Vector2(0f, step);   // QUIT은 한 줄 아래로, OPTIONS가 QUIT 자리

        // 글자 나타남: 복제한 「From Fade」는 QUIT 글자가 이미 투명해진 뒤를 복제해 0 → 0으로 돌아 끝내 안 보였다.
        // 복제 연출은 끄고 QUIT 글자의 알파를 그대로 따라간다(같은 박자로 나타남).
        foreach (DG.Tweening.DOTweenAnimation anim in clone.GetComponentsInChildren<DG.Tweening.DOTweenAnimation>(true))
        {
            DG.Tweening.DOTween.Kill(anim.gameObject);
            if (anim.target != null) DG.Tweening.DOTween.Kill(anim.target);
            Destroy(anim);
        }

        _quitLabel = _quit.GetComponentInChildren<TMP_Text>(true);
        _label = clone.GetComponentInChildren<TMP_Text>(true);
        if (_label != null)
        {
            _label.text = Label;
            _label.textWrappingMode = TextWrappingModes.NoWrap;
            if (_quitLabel != null) _label.alpha = _quitLabel.alpha;
        }

        _options = clone.GetComponent<Button>();
        _options.onClick = new Button.ButtonClickedEvent();
        _options.onClick.AddListener(Open);
        _released = _quit.interactable;
        if (!_released) _options.interactable = false;

        _raycaster = GetComponent<GraphicRaycaster>();
    }

    private Button FindButton(string name)
    {
        foreach (Button b in GetComponentsInChildren<Button>(true))
        {
            if (b.name == name) return b;
        }

        return null;
    }

    private void Update()
    {
        if (_options == null) return;

        if (_fading && _label != null && _quitLabel != null)
        {
            _label.alpha = _quitLabel.alpha;
            if (_quitLabel.alpha >= 0.999f && _released) _fading = false;
        }

        if (_released) return;

        // QUIT이 풀릴 때 같이 푼다(잠긴 동안 MainMenuRevealGate가 꺼 둔 전환 모양도 QUIT 것을 따른다).
        if (_quit == null || _quit.interactable)
        {
            if (_quit != null) _options.transition = _quit.transition;
            _options.interactable = true;
            _released = true;
        }
        else if (_options.interactable)
        {
            _options.interactable = false;
        }
    }

    // ── 설정 화면 ────────────────────────────────────────────

    /// <summary>설정 화면을 연다(처음이면 세운다).</summary>
    public void Open()
    {
        if (_screen == null)
        {
            MainMenuOptionsLook look = MainMenuOptionsLook.Load();
            if (look == null || look.pauseScreen == null) return;
            // FPController와 같게 세우자마자 끈다 — 처음 켜질 때는 자식 DOTween이 아직 없어 PauseMenu가 연출을 다시 틀지 못한다(글자가 투명한 채 굳음).
            _screen = Instantiate(look.pauseScreen);
            _screen.name = "HUD_Options (main menu)";   // 「HUD_Main」으로 시작하면 메인 화면 자동 설치(이것·MainMenuRevealGate)에 걸린다
            _screen.SetActive(false);
            PauseMenuExtras extras = PauseMenuExtras.Install(_screen.GetComponent<PauseMenu>());
            if (extras != null) extras.MakeStandalone(OnClosed);
        }

        _screen.SetActive(true);

        if (_raycaster != null) _raycaster.enabled = false;   // 뒤의 START·QUIT이 눌리지 않게
        SetButtonsShown(false);   // 설정 화면 바탕이 반투명이라 흐린 START·OPTIONS·QUIT이 비쳐 헷갈렸다
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
    }

    private void SetButtonsShown(bool shown)
    {
        foreach (Button b in GetComponentsInChildren<Button>(true))
        {
            CanvasGroup group = b.GetComponent<CanvasGroup>();
            if (group == null)
            {
                if (shown) continue;
                group = b.gameObject.AddComponent<CanvasGroup>();
            }

            group.alpha = shown ? 1f : 0f;
        }
    }

    private void OnClosed()
    {
        if (_raycaster != null) _raycaster.enabled = true;
        SetButtonsShown(true);
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
    }

    private void OnDestroy()
    {
        if (_screen != null) Destroy(_screen);
    }
}

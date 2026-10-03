using System.Collections;
using System.Collections.Generic;
using NightDuty;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 붙잡힘 연출과 재시작(최종 기획서 「붙잡힘과 재시작」, 8단계). 근무 씬에 자동으로 선다(<see cref="FlowAutoInstall"/>).
/// <para>
/// <b>공용 틀 + 축별 장면</b>: 소리 끊김 → 정적·암전 → <b>축별 장면</b> → 암전 →
/// 「n일차 · 00:00」(체크포인트면 02:16) 카드 + 사망 화면의 「YOU DIED」(<see cref="CaptureCardLook"/>, 없으면 축 상징 귀·눈·발자국) + 그 밤 그 축을 올린 수칙·점검 항목 이름 + 「마지막 서명」.
/// 무엇을 했는지·어떻게 했어야 하는지는 적지 않는다. 카드를 넘기면 <see cref="NightRun.RestartAfterCapture"/> → 출근 자리에서 다시 한다.
/// </para>
/// <para>
/// <b>축별 장면은 프리팹으로 끼운다</b>(<see cref="CaptureCastSO"/>, <c>Resources/CaptureCast.asset</c>, 축마다 한 칸 — 규약은 그 클래스 설명).
/// 칸이 비면 기본 장면: 청각 = 모든 소리가 끊기고 돌아보면 소년의 얼굴 · 조도 = 손전등이 꺼졌다 켜지는 순간 해부 모형의 얼굴 ·
/// 배치 = 돌아보면 사람 나무, 화면이 가지 사이로 끌려 들어간다. 같은 축으로 두 번째면 짧게(기본 얼굴 절반, 프리팹은 재생 속도 ×2), 세 번째부터 아무 키로 건너뛴다.
/// 장면만 미리 보기: <see cref="Preview"/>(F3 콘솔).
/// </para>
/// <para>
/// <b>사망 화면과의 관계.</b> 코어는 <see cref="EventBus.Captured"/> 구독자가 있으면 옛 「사망」(<see cref="EventBus.AxisCritical"/>)을 보내지 않는다 —
/// 그래서 근무 씬에서는 김진선님 <c>PlayResultRouter</c>의 사망 화면이 뜨지 않고 이 연출이 맡는다(김진선님 코드는 고치지 않았다).
/// 결근(6번째 붙잡힘)은 카드 없이 암전을 유지한 채 <see cref="EventBus.DayEnded"/> → 결과창으로 넘어간다.
/// </para>
/// <para>
/// <b>아직 안 하는 것</b>: 씬을 다시 불러오지 않는다(문·소품은 그대로, 플레이어만 출근 자리로). 게임 시계 표시는 시 단위로만 되돌린다
/// (<c>GameTime.JumpToHour</c> — 판정 시계는 구동기가 분 단위로 맞춘다).
/// </para>
/// </summary>
[DisallowMultipleComponent]
public sealed partial class CaptureDirector : MonoBehaviour
{
    [Header("카드·페이드(초, 실제 시간)")]
    [SerializeField, Min(0f)] private float fadeSeconds = 0.6f;
    [SerializeField, Min(0f)] private float cardMinSeconds = 1.5f;
    [SerializeField, Min(0f)] private float cardAutoSeconds = 8f;

    [Header("기본 장면의 얼굴 구도")]
    [Tooltip("카메라에서 얼굴(조준점)까지 최소 거리(m). 튀어나온 팔·가지가 근평면을 뚫으면 그만큼 더 띄운다.")]
    [SerializeField, Min(0.1f)] private float faceDistance = 0.42f;
    [Tooltip("화면 세로의 절반을 채울 얼굴 크기(m). 거리가 멀어지면 시야각을 좁혀 이만큼이 화면을 채우게 한다.")]
    [SerializeField, Min(0.05f)] private float faceHalfHeight = 0.2f;

    private Canvas _canvas;
    private Image _black;
    private CanvasGroup _card;
    private TMP_Text _title;
    private TMP_Text _symbol;
    private TMP_Text _sources;
    private TMP_Text _sign;
    private TMP_Text _hint;

    private Vector3 _spawnPos;
    private Quaternion _spawnRot;
    private bool _spawnKnown;
    private readonly Dictionary<FearAxis, int> _counts = new Dictionary<FearAxis, int>();
    private readonly List<Canvas> _hidden = new List<Canvas>();
    private CaptureCardLook _look;
    private readonly List<GameObject> _lookParts = new List<GameObject>();
    private bool _running;

    /// <summary>지금 근무 씬의 연출기. 없으면 null.</summary>
    public static CaptureDirector Active { get; private set; }

    /// <summary>붙잡힘 연출·재시작 카드(또는 미리 보기)가 진행 중인지.</summary>
    public bool IsPlaying
    {
        get { return _running; }
    }

    /// <summary>마지막으로 띄운 카드 문구(디버그·검수).</summary>
    public string LastCard { get; private set; }

    /// <summary>마지막 붙잡힘에서 보여 준 얼굴 대역 ID(기본 장면일 때, 디버그·검수). 프리팹 장면이거나 건너뛰었으면 빈 문자열.</summary>
    public string LastFaceId { get; private set; }

    // ── 자동 설치 ─────────────────────────────────────────────

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Install()
    {
        Active = null;
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
        if (!FlowAutoInstall.IsDutyScene(scene) || FlowAutoInstall.Exists<CaptureDirector>(scene)) return;
        FlowAutoInstall.CreateHost<CaptureDirector>(scene, "CaptureDirector (auto)");
    }

    private void OnEnable()
    {
        Active = this;
        EventBus.Captured += OnCaptured;
    }

    private void OnDisable()
    {
        EventBus.Captured -= OnCaptured;
        if (Active == this) Active = null;
        if (_running) AudioListener.pause = false;
        _mount = null;
        RestoreOtherHud();
    }

    private void Start()
    {
        FPController player = FindAnyObjectByType<FPController>();
        if (player != null)
        {
            _spawnPos = player.transform.position;
            _spawnRot = player.transform.rotation;
            _spawnKnown = true;
        }
    }

    private void OnCaptured(FearAxis axis)
    {
        if (_running) return;
        StartCoroutine(Run(axis));
    }

    // ── 붙잡힘 ───────────────────────────────────────────────

    private IEnumerator Run(FearAxis axis)
    {
        _running = true;
        LastFaceId = string.Empty;

        if (NightRun.RestartsTonight == 0) _counts.Clear();   // 새 밤의 첫 붙잡힘
        int count;
        _counts.TryGetValue(axis, out count);
        count++;
        _counts[axis] = count;

        FPController player = FindAnyObjectByType<FPController>();
        GameTime clock = FindAnyObjectByType<GameTime>();
        if (clock != null) clock.Hold(this);
        if (player != null) player.enabled = false;

        EnsureUi();
        HideOtherHud();

        // ① 소리 끊김 — 리스너를 멈춘다(장면 프리팹의 AudioSource와 장면 소리만 예외).
        AudioListener.pause = true;

        // ②③ 정적·암전 → 축별 장면 → 암전(끝나면 화면은 검다)
        yield return PlayScene(axis, count, player);

        // ④ 재시작(연출이 가려 주는 동안 스냅샷으로 돌린다). 콘솔이 먼저 재시작했으면 건너뛴다.
        RestartResult result = new RestartResult(RestartKind.None, NightRun.RestartsTonight, -1, axis);
        if (NightRun.IsCaptured)
        {
            if (!RestartPolicy.IsAbsence(NightRun.RestartsTonight)) RewindClock(clock, NightRun.Checkpoint != null);
            result = NightRun.RestartAfterCapture();
        }

        if (result.Kind == RestartKind.Absent)
        {
            // 결근: 결과창(PlayResultRouter)이 DayEnded로 이어받는다. 암전은 그대로 둔다.
            AudioListener.pause = false;
            RestoreOtherHud();
            if (clock != null) clock.Release(this);
            _running = false;
            yield break;
        }

        // ⑤ 재시작 카드
        if (result.Kind != RestartKind.None)
        {
            FillCard(axis, result);
            yield return FadeCard(1f);
            float shown = 0f;
            while (shown < cardAutoSeconds)
            {
                shown += Time.unscaledDeltaTime;
                if (shown >= cardMinSeconds && Input.anyKeyDown) break;
                yield return null;
            }

            MovePlayerToStart(player);
            yield return FadeCard(0f);
            ClearDeathLook();
        }

        // ⑥ 다시 근무
        AudioListener.pause = false;
        yield return FadeFromBlack();
        RestoreOtherHud();
        if (player != null) player.enabled = true;
        if (clock != null) clock.Release(this);
        _running = false;
    }

    private IEnumerator FadeFromBlack()
    {
        float f = 0f;
        while (f < fadeSeconds)
        {
            f += Time.unscaledDeltaTime;
            SetBlack(1f - Mathf.Clamp01(f / Mathf.Max(0.01f, fadeSeconds)));
            yield return null;
        }

        SetBlack(0f);
    }

    /// <summary>장면 동안 다른 화면 UI(조작 안내·시계)를 감춘다. 컴포넌트를 끄기만 하고 끝나면 되돌린다.</summary>
    private void HideOtherHud()
    {
        _hidden.Clear();
        Canvas[] all = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            Canvas c = all[i];
            if (c == _canvas || !c.enabled || !c.isRootCanvas || c.renderMode != RenderMode.ScreenSpaceOverlay) continue;
            c.enabled = false;
            _hidden.Add(c);
        }
    }

    private void RestoreOtherHud()
    {
        for (int i = 0; i < _hidden.Count; i++)
        {
            if (_hidden[i] != null) _hidden[i].enabled = true;
        }

        _hidden.Clear();
    }

    /// <summary>게임 시계 표시를 재시작 지점 쪽으로 돌린다(시 단위). 판정 시계(분)는 구동기가 NightRestarted로 맞춘다.</summary>
    private static void RewindClock(GameTime clock, bool fromCheckpoint)
    {
        if (clock == null) return;
        int span = clock.EndMinutes - clock.StartMinutes;
        int nightMinute = fromCheckpoint ? NightClock.Call2 : 0;
        int gameMinute = clock.StartMinutes + (span > 0 ? Mathf.RoundToInt(nightMinute * span / (float)NightClock.ShiftEnd) : 0);
        clock.JumpToHour(gameMinute / 60);
    }

    private void MovePlayerToStart(FPController player)
    {
        if (player == null || !_spawnKnown) return;
        player.transform.SetPositionAndRotation(_spawnPos, _spawnRot);
        Rigidbody body = player.GetComponent<Rigidbody>();
        if (body != null)
        {
            body.position = _spawnPos;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }
    }

    private IEnumerator Wait(float seconds, bool skippable, System.Action onSkip)
    {
        float t = 0f;
        while (t < seconds)
        {
            t += Time.unscaledDeltaTime;
            if (skippable && Input.anyKeyDown)
            {
                onSkip();
                yield break;
            }

            yield return null;
        }
    }

    // ── 재시작 카드 ───────────────────────────────────────────

    private void FillCard(FearAxis axis, RestartResult result)
    {
        bool fromCheckpoint = result.Kind == RestartKind.FromCheckpoint;
        int start = fromCheckpoint ? NightClock.Call2 : 0;
        _title.text = NightRun.Day + "일차 · " + (start / 60).ToString("00") + ":" + (start % 60).ToString("00");
        bool died = ShowDeathLook();
        _symbol.text = Symbol(axis);
        _symbol.gameObject.SetActive(!died);

        List<string> names = new List<string>();
        IReadOnlyList<string> sources = NightRun.LastCaptureSources;
        for (int i = 0; i < sources.Count; i++) names.Add(SourceName(sources[i]));
        _sources.text = string.Join("\n", names);   // 출처가 없으면(디버그 강제 붙잡힘) 비워 둔다

        _sign.text = fromCheckpoint ? "마지막 서명: 02:16" : "마지막 서명: 없음 → 00:00";
        _hint.text = "아무 키나 눌러 계속";
        LastCard = _title.text + " | " + (died ? "YOU DIED" : _symbol.text) + " | " + (names.Count > 0 ? _sources.text.Replace("\n", " / ") : "—") + " | " + _sign.text;
    }

    /// <summary>
    /// 카드 뒤에 사망 화면(<see cref="CaptureCardLook"/>)의 배경·「YOU DIED」 로고를 깐다. 카드마다 새로 복제하므로
    /// 로고의 페이드 인(디자인 프리팹의 DOTween)도 매번 처음부터 돈다. 깔았으면 true — 그때는 축 상징을 숨긴다.
    /// </summary>
    private bool ShowDeathLook()
    {
        ClearDeathLook();
        if (_look == null) _look = CaptureCardLook.Load();
        if (_look == null) return false;

        GameObject logo = _look.Part(_look.logoChild);
        if (logo == null) return false;

        GameObject background = _look.Part(_look.backgroundChild);
        if (background != null)
        {
            GameObject bg = Instantiate(background, _card.transform, false);
            bg.transform.SetAsFirstSibling();
            _lookParts.Add(bg);
        }

        GameObject mark = Instantiate(logo, _card.transform, false);
        mark.transform.SetSiblingIndex(background != null ? 1 : 0);
        _lookParts.Add(mark);

        foreach (GameObject part in _lookParts)
        {
            foreach (Graphic g in part.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;
        }

        return true;
    }

    private void ClearDeathLook()
    {
        for (int i = 0; i < _lookParts.Count; i++)
        {
            if (_lookParts[i] != null) Destroy(_lookParts[i]);
        }

        _lookParts.Clear();
    }

    /// <summary>붙잡힌 축의 상징(사망 화면 모습이 없을 때의 폴백).</summary>
    public static string Symbol(FearAxis axis)
    {
        switch (axis)
        {
            case FearAxis.Auditory: return "귀";
            case FearAxis.Illuminance: return "눈";
            default: return "발자국";
        }
    }

    /// <summary>출처 ID(수칙 「G1」, 점검 「H-2」)를 카드에 쓸 이름으로. 모르는 ID는 그대로.</summary>
    public static string SourceName(string id)
    {
        if (string.IsNullOrEmpty(id)) return string.Empty;

        IReadOnlyList<RuleDef> rules = ProgramCatalog.AllRules;
        for (int i = 0; i < rules.Count; i++)
        {
            if (rules[i].Id == id) return rules[i].Text;
        }

        InspectionItem item = InspectionCatalog.Find(id);
        if (item != null) return "[점검] " + item.Name;
        return id;
    }

    private IEnumerator FadeCard(float to)
    {
        float from = _card.alpha;
        float t = 0f;
        while (t < fadeSeconds)
        {
            t += Time.unscaledDeltaTime;
            _card.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / Mathf.Max(0.01f, fadeSeconds)));
            yield return null;
        }

        _card.alpha = to;
    }

    // ── 화면 ─────────────────────────────────────────────────

    private void SetBlack(float alpha)
    {
        Color c = _black.color;
        c.a = alpha;
        _black.color = c;
        _black.raycastTarget = alpha > 0.001f;
    }

    private void EnsureUi()
    {
        if (_canvas != null) return;

        GameObject root = new GameObject("CaptureCanvas");
        root.transform.SetParent(transform, false);
        _canvas = root.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 500;   // 김진선님 페이드(200)·사망 화면 위
        CanvasScaler scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject black = new GameObject("Black", typeof(RectTransform));
        black.transform.SetParent(root.transform, false);
        Stretch((RectTransform)black.transform);
        _black = black.AddComponent<Image>();
        _black.color = new Color(0f, 0f, 0f, 0f);
        _black.raycastTarget = false;

        GameObject card = new GameObject("Card", typeof(RectTransform));
        card.transform.SetParent(root.transform, false);
        Stretch((RectTransform)card.transform);
        _card = card.AddComponent<CanvasGroup>();
        _card.alpha = 0f;
        _card.blocksRaycasts = false;

        TMP_FontAsset font = DonorFont();
        _title = Text(card.transform, "Title", font, 56f, new Vector2(0.5f, 0.78f), new Color(0.82f, 0.82f, 0.78f));
        _symbol = Text(card.transform, "Symbol", font, 150f, new Vector2(0.5f, 0.58f), new Color(0.75f, 0.2f, 0.18f));
        _sources = Text(card.transform, "Sources", font, 30f, new Vector2(0.5f, 0.36f), new Color(0.78f, 0.78f, 0.74f));
        _sign = Text(card.transform, "Sign", font, 28f, new Vector2(0.5f, 0.2f), new Color(0.6f, 0.62f, 0.6f));
        _hint = Text(card.transform, "Hint", font, 22f, new Vector2(0.5f, 0.08f), new Color(0.45f, 0.47f, 0.47f));
    }

    private static TMP_Text Text(Transform parent, string name, TMP_FontAsset font, float size, Vector2 anchor, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.sizeDelta = new Vector2(1500f, size * 5f);
        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.fontSize = size;
        text.alignment = TextAlignmentOptions.Center;
        text.color = color;
        text.raycastTarget = false;
        return text;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    /// <summary>씬의 한글이 들어 있는 TMP 글꼴을 빌린다(김진선님 HUD 글꼴). 없으면 TMP 기본.</summary>
    private static TMP_FontAsset DonorFont()
    {
        TMP_Text[] all = FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].font != null && all[i].font.HasCharacter('일')) return all[i].font;
        }

        return TMP_Settings.defaultFontAsset;
    }
}

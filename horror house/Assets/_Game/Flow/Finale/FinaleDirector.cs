using System.Collections;
using System.Collections.Generic;
using NightDuty;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 5일차 피날레 연출(최종 기획서 「5일차 피날레」, 11단계 — 2026-10-10 기획 가이드로 3번 이후를 다시 짬). 근무 씬에 자동으로 선다.
/// 판정은 코어(<see cref="FinaleWatch"/>)가 하고, 이것은 순서·화면·소리만 맡는다.
/// <list type="number">
/// <item>05:00 — 구동기가 정산 대신 <see cref="Begin"/>을 부른다(5일차 경비실 전화 조기 퇴근도 — <see cref="ShiftEndPhone"/>). 경비실 밖이면 강제 복귀 컷(암전 + 「근무 시간이 종료되었습니다.」). 경비실 문은 닫고 잠근다(나갈 수 없다).</item>
/// <item>문자 공지 「창밖에..?..?//..ㅁ....ㅜ/ㅅ../??......」 → 플레이어가 메시지란을 확인(<see cref="MessagesChecked"/>) → 3초.</item>
/// <item>「들어가게 해줘」 + 창밖 정장 남자 나타남 → 창 두드림(피날레 배역표 <see cref="FinaleCastSO"/>).</item>
/// <item>괴이문자가 2초 동안 「들여보내줘」로 바뀌고, 이어 2.5초 동안 「들여보내줘」만 온다. 이 4.5초 동안 창밖 남자가 화면에 보인 시간이 합쳐서 1.5초면 「봤다」 — 결말은 4.5초가 끝난 뒤.
/// 두 결말 모두 문자를 전부 지우며 태블릿 G3 빈칸이 「당신」으로 채워진다.</item>
/// <item><b>봤다</b>: 문자 전부 지움 → 0.5초 → 창밖 남자 사라짐 · 메시지란·확대 고정 → 「들어왔다」(붉게) · 「내가 보여?」 · 「이제 보이는 구나」(0.5초 간격) → 1초 → 손 떨림 · 수신음 연타 ·
/// 문자 몰아치기(이상현상 확인 · 확인됨 · 조치 예정 · 계약 해지 · 인수인계, 묶음 사이 0.4초 · 마지막 무작위 2초는 0.2초 간격) → 소리·화면 정지 → 0.2초 → 검은 화면 무음 2초 →
/// 「근무 종료, 철거가 예정대로 진행됩니다.」 4초 → 암전 → 엔딩 크레딧(「엔딩 1」) → 메인(<see cref="FinaleEnding.ShiftChange"/>).</item>
/// <item><b>안 봤다</b>: 문자가 멈추고 전부 지움 + 창밖 남자 제거 → 「근무가 종료 되었습니다. 전화를 통해 퇴근하십시오」 → 경비실 전화로 퇴근(<see cref="Checkout"/>)
/// → 성공 엔딩 씬(<see cref="FinaleEnding.ShiftOver"/>). 이때부터 경비실 문은 상호작용도 받지 않는다.</item>
/// </list>
/// 어느 결말이든 <see cref="NightRun.EndFinale"/>로 근무를 정산하고, 어디로 갈지는 결과 라우터(<c>PlayResultRouter</c>)가 결말을 보고 정한다(근무일지 결과창은 건너뜀).
/// 시험: 개발자 모드 ① 「피날레 (5일차)」 [5일차로 다시 열기] → [피날레 시작] → [응시 1.5초].
/// </summary>
[DisallowMultipleComponent]
public sealed class FinaleDirector : MonoBehaviour
{
    [Header("문구")]
    [SerializeField] private string shiftOverNotice = "근무 시간이 종료되었습니다.";
    [Tooltip("강제 복귀 뒤 태블릿에 오는 첫 문자(창밖 남자가 나타나기 3초 전).")]
    [SerializeField] private string openingNotice = "창밖에..?..?//..ㅁ....ㅜ/ㅅ../??......";
    [Tooltip("괴이문자가 바뀌어 가는 끝 문구.")]
    [SerializeField] private string letInLine = "들여보내줘";
    [Tooltip("괴이문자 한 통의 처음 글자 수(끝으로 갈수록 「들여보내줘」 길이로 줄어든다).")]
    [SerializeField, Min(4)] private int weirdLength = 14;
    [SerializeField] private string checkoutNotice = "근무가 종료 되었습니다. 전화를 통해 퇴근하십시오";
    [SerializeField] private string cameIn = "들어왔다";
    [SerializeField] private string cameInColor = "#D11F27";
    [SerializeField] private string[] seenLines = { "내가 보여?", "이제 보이는 구나" };
    [SerializeField] private string incidentLine = "현 폐교 내 확인되지 않는 이상현상 확인 됨";
    [SerializeField] private string confirmedLine = "확인됨";
    [SerializeField] private string actionLine = "조치 예정";
    [SerializeField] private string terminatedLine = "계약 해지";
    [SerializeField] private string handoverLine = "인수인계가 진행됩니다.";
    [SerializeField] private string[] chaosLines = { "인수인계", "진행", "인수인계가 진행됩니다.", "진행됩니다", "근무지 이탈 불가" };
    [SerializeField] private string finalLine = "근무 종료, 철거가 예정대로 진행됩니다.";

    [Header("시간(초)")]
    [SerializeField, Min(0f)] private float fadeSeconds = 0.8f;
    [SerializeField, Min(0f)] private float noticeSeconds = 2.5f;
    [SerializeField, Min(0f)] private float k4Lead = 3f;
    [Tooltip("괴이문자가 「들여보내줘」로 바뀌어 가는 시간.")]
    [SerializeField, Min(0.5f)] private float letInMorphSeconds = 2f;
    [Tooltip("바뀐 뒤 온전한 「들여보내줘」만 계속 받는 시간. 응시 판정 창 = 바뀌는 시간 + 이 시간(4.5초 — 2026-10-10 김진선님 3.5 → 4.5).")]
    [SerializeField, Min(0f)] private float letInHoldSeconds = 2.5f;
    [Tooltip("괴이문자·「들여보내줘」 문자 사이 간격.")]
    [SerializeField, Min(0.02f)] private float letInInterval = 0.2f;
    [Tooltip("「봤다」: 문자를 지운 뒤 창밖 남자가 사라지고 태블릿이 메시지란·확대로 고정되기까지.")]
    [SerializeField, Min(0f)] private float seenClearDelay = 0.5f;
    [Tooltip("「봤다」: 고정 뒤 「들어왔다」 · 「내가 보여?」 · 「이제 보이는 구나」를 한 줄씩 받는 간격(고정 → 첫 줄도 이 간격).")]
    [SerializeField, Min(0f)] private float cameInLineGap = 0.5f;
    [Tooltip("「봤다」: 마지막 줄(이제 보이는 구나) 뒤 몰아치기가 시작되기까지.")]
    [SerializeField, Min(0f)] private float beforeBurstSeconds = 1f;
    [Tooltip("「봤다」 몰아치기: 묶음(번호) 사이 간격.")]
    [SerializeField, Min(0f)] private float seenLineGap = 0.4f;
    [Tooltip("한 묶음 안에서 「연달아」 받는 간격(확인됨·조치 예정).")]
    [SerializeField, Min(0.02f)] private float burstInterval = 0.06f;
    [Tooltip("마지막 몰아치기(인수인계·진행·… 무작위)의 문자 간격.")]
    [SerializeField, Min(0.02f)] private float chaosInterval = 0.2f;
    [SerializeField, Min(0f)] private float confirmedSeconds = 1f;
    [SerializeField, Min(0f)] private float actionSeconds = 1f;
    [SerializeField, Min(0f)] private float chaosSeconds = 2f;
    [Tooltip("소리·화면이 멈춘 뒤 검은 화면까지.")]
    [SerializeField, Min(0f)] private float freezeSeconds = 0.2f;
    [SerializeField, Min(0f)] private float blackSilenceSeconds = 2f;
    [SerializeField, Min(0f)] private float finalLineSeconds = 4f;
    [Tooltip("마지막 문구가 사라진 뒤 메인으로 가기까지의 암전.")]
    [SerializeField, Min(0f)] private float finalBlackSeconds = 0.5f;

    [Header("손 떨림(TabletZoom.SetShake) — 몰아치기 동안 처음 → 끝")]
    [SerializeField, Min(0f)] private float shakeStart = 0.6f;
    [SerializeField, Min(0f)] private float shakeEnd = 1.8f;

    [Header("소리 이름(Resources/Direction 또는 연출 소리 표, 없으면 조용히)")]
    [SerializeField] private string letMeInSound = "finale.letmein";
    [SerializeField] private string wipeSound = "finale.wipe";

    [Header("경비실 문")]
    [Tooltip("이 거리 안에서 겨눈 경비실 문은 상호작용을 막는다(퇴근 안내·「봤다」 결말부터).")]
    [SerializeField, Min(0.5f)] private float doorBlockRange = 2.5f;

    private sealed class GuardDoor
    {
        public DoorHandle Handle;
        public bool WasLocked;
        public bool WasOpen;
    }

    private readonly List<GuardDoor> _doors = new List<GuardDoor>();
    private Coroutine _run;
    private bool _running;
    private bool _violating;
    private bool _doorsNoInteract;
    private bool _awaitingCheckout;
    private bool _checkoutRequested;
    private bool _frozen;
    private GameTime _clock;
    private int _messageSerial;
    private readonly System.Random _rng = new System.Random();

    private Canvas _canvas;
    private Image _black;
    private RawImage _frozenImage;
    private Texture2D _frozenShot;
    private TMP_Text _text;

    /// <summary>지금 근무 씬의 피날레 연출기. 없으면 null.</summary>
    public static FinaleDirector Active { get; private set; }

    /// <summary>피날레가 진행 중인지.</summary>
    public bool IsRunning
    {
        get { return _running; }
    }

    /// <summary>「창을 보지 않음」 결말에서 전화 퇴근을 기다리는 중인지(<see cref="ShiftEndPhone"/>가 본다).</summary>
    public bool AwaitingCheckout
    {
        get { return _running && _awaitingCheckout && !_checkoutRequested; }
    }

    /// <summary>지금 단계(디버그).</summary>
    public string Phase { get; private set; }

    // ── 자동 설치 ─────────────────────────────────────────────

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Install()
    {
        SceneManager.sceneLoaded -= RestoreListenerVolume;
        if (s_restoreVolume >= 0f) AudioListener.volume = s_restoreVolume;   // 지난 플레이에서 눌러 둔 볼륨이 남지 않게
        s_restoreVolume = -1f;
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
        if (!FlowAutoInstall.IsDutyScene(scene) || FlowAutoInstall.Exists<FinaleDirector>(scene)) return;
        FlowAutoInstall.CreateHost<FinaleDirector>(scene, "FinaleDirector (auto)");
    }

    private void OnEnable()
    {
        Active = this;
        Phase = "대기";
    }

    private void OnDisable()
    {
        NightRun.Finale.Violation -= OnViolation;
        NightRun.Finale.CloseGazeWindow();
        DisarmDoors();
        ReleaseTablet();
        if (_frozen) Time.timeScale = 1f;
        _frozen = false;
        if (_frozenShot != null) Destroy(_frozenShot);
        _frozenShot = null;
        if (_running) AudioListener.pause = false;
        if (_clock != null) _clock.Release(this);
        if (Active == this) Active = null;
    }

    // ── 시작 ─────────────────────────────────────────────────

    /// <summary>
    /// 5일차 05:00 — 구동기가 정산 대신 부른다(개발자 모드도). 5일차 밤이 열려 있고 붙잡히지 않았으면 시작하고 true.
    /// </summary>
    public bool Begin()
    {
        if (_running || !NightRun.IsNightActive || NightRun.IsCaptured || NightRun.Day < FinaleWatch.Day || NightRun.Finale.Active) return false;
        _running = true;
        _awaitingCheckout = false;
        _checkoutRequested = false;
        _clock = FindAnyObjectByType<GameTime>();
        if (_clock != null) _clock.Hold(this);
        NightRun.Finale.Violation -= OnViolation;
        NightRun.Finale.Violation += OnViolation;
        _run = StartCoroutine(Run(true));
        return true;
    }

    /// <summary>
    /// 「창을 보지 않음」 결말 — 경비실 전화로 퇴근한다(<see cref="ShiftEndPhone"/>가 부른다). 퇴근 안내 전이면 false.
    /// </summary>
    public bool Checkout()
    {
        if (!AwaitingCheckout) return false;
        _checkoutRequested = true;
        return true;
    }

    private IEnumerator Run(bool first)
    {
        EnsureUi();
        if (first)
        {
            Phase = "강제 복귀";
            yield return ForcedReturn();
            if (!NightRun.BeginFinale())
            {
                // 열 수 없으면(밤이 닫힘 등) 평소처럼 정산한다.
                Finish(false, false);
                yield break;
            }
        }

        SealDoors();   // 2026-10-10: 경비실을 나갈 수 없다(잠김 — 겨누면 「잠겨 있습니다」)
        FinaleCastSO.Load();
        DirectionStage stage = DirectionStage.Active;

        Phase = "문자 공지";
        Message(openingNotice);   // 2026-10-11 김진선님: K4 수칙 문구 대신 깨진 문자(K4 판정·문 잠금은 그대로)
        yield return null;        // 문자가 메시지함에 들어간 뒤부터 본다

        // 2026-10-11 김진선님: 플레이어가 메시지란을 확인해야 그 뒤 연출(3초 → 창밖 남자)이 나온다. 그때까지 태블릿 알람이 울린다.
        Phase = "문자 공지 — 메시지 확인 대기";
        while (!MessagesChecked()) yield return null;
        Phase = "문자 공지 — 확인함";
        yield return new WaitForSeconds(k4Lead);

        Phase = "들어가게 해줘 · 창 두드림";
        FinaleMob window = stage != null ? stage.StageFinale(FinaleRole.WindowMan) : null;
        if (window != null)
        {
            PlaySound(letMeInSound, window.transform.position + Vector3.up * 1.5f);
            yield return window.PlayAndWait(FinaleBeat.Appear);
            if (window != null && window.CurrentBeat == FinaleBeat.Appear) window.Play(FinaleBeat.Knock);
        }

        Phase = "들여보내줘 — 응시 판정";
        yield return LetMeIn(window);

        if (NightRun.Finale.Seen)
        {
            yield return SeenEnding(stage);
        }
        else
        {
            yield return CheckoutEnding(stage);
        }
    }

    // ── 3. 괴이문자 → 「들여보내줘」 (응시 판정 창) ─────────────────────────

    /// <summary>
    /// <see cref="letInMorphSeconds"/>(2초) 동안 괴이문자를 보내며 「들여보내줘」로 바꿔 가고, 이어서 <see cref="letInHoldSeconds"/>(2.5초) 동안 온전한 「들여보내줘」만 보낸다.
    /// 이 4.5초 내내 응시 창을 열어 둔다. 「봤다」가 되어도 끝까지 진행한 뒤에 결말로 간다(2026-10-10 수정).
    /// 「봤다」는 창밖 남자가 <b>화면에 보인 시간</b>을 합쳐 1.5초(B안 — <see cref="WindowManOnScreen"/>).
    /// </summary>
    private IEnumerator LetMeIn(FinaleMob window)
    {
        FinaleWatch watch = NightRun.Finale;
        watch.OpenGazeWindow();
        TMP_FontAsset font = TabletFont();
        Renderer[] windowRenderers = window != null ? window.GetComponentsInChildren<Renderer>(true) : null;
        float total = letInMorphSeconds + letInHoldSeconds;
        float t = 0f;
        float next = 0f;
        while (t < total)
        {
            if (t >= next)
            {
                Message(t < letInMorphSeconds ? MorphLine(t / letInMorphSeconds, font) : letInLine);
                next += letInInterval;
            }

            float dt = Time.deltaTime;
            watch.FeedSight(WindowManOnScreen(window, windowRenderers), dt);
            t += dt;
            yield return null;
        }

        watch.CloseGazeWindow();
        Debug.Log("[Finale] 응시 판정 끝 — " + (watch.Seen ? "봤다" : "안 봤다") + " (합 " + watch.GazeTotal.ToString("0.0") + "초)");
    }

    /// <summary>문자를 전부 지우고 G3 빈칸을 「당신」으로 채운다(두 결말 공통 — 옛 「들어왔다」 연출에서 되살림).</summary>
    private void WipeAndFillBlank()
    {
        ClearTabletMessages();
        PlaySound(wipeSound, EarPoint());
        NightRun.FillFinaleBlank();
        ReloadTablet();
    }

    private static void ReloadTablet()
    {
        foreach (TabletDocument doc in FindObjectsByType<TabletDocument>(FindObjectsInactive.Include, FindObjectsSortMode.None)) doc.Reload();
    }

    /// <summary>
    /// 창밖 남자가 지금 화면에 보이는지(B안 — 2026-10-10 김진선님): 조우 연출이 「몹을 봤다」를 재는 것과 같은 기준
    /// (<see cref="SightProbe.VisibleNow"/> — 몸통·머리 중 하나가 화면 가장자리 12% 안쪽, 30m 안, 가림 없음, CCTV 보는 중 제외).
    /// 태블릿을 확대(TAB)해 화면을 덮고 있으면 보이지 않는 것으로 친다.
    /// </summary>
    private static bool WindowManOnScreen(FinaleMob window, Renderer[] renderers)
    {
        if (window == null || renderers == null) return false;
        TabletZoom zoom = TabletZoom.Active;
        if (zoom != null && zoom.IsZoomed) return false;
        return SightProbe.VisibleNow(window.transform, renderers);
    }

    /// <summary>진행도 <paramref name="p"/>(0~1)의 괴이문자 한 줄 — 앞쪽 글자부터 「들여보내줘」로 맞춰지고 길이도 그쪽으로 줄어든다.</summary>
    private string MorphLine(float p, TMP_FontAsset font)
    {
        p = Mathf.Clamp01(p);
        float settle = Mathf.SmoothStep(0f, 1f, p);
        int length = Mathf.Max(letInLine.Length, Mathf.RoundToInt(Mathf.Lerp(weirdLength, letInLine.Length, settle)));
        System.Text.StringBuilder sb = new System.Text.StringBuilder(length);
        for (int i = 0; i < length; i++)
        {
            bool fixedChar = i < letInLine.Length && _rng.NextDouble() < settle;
            sb.Append(fixedChar ? letInLine[i] : WeirdChar(font));
        }

        return sb.ToString();
    }

    // ── 결말: 봤다 ────────────────────────────────────────────

    private IEnumerator SeenEnding(DirectionStage stage)
    {
        Phase = "결말(봤다) — 들어왔다";
        _doorsNoInteract = true;   // 경비실 문 잠김 + 상호작용 불가
        WipeAndFillBlank();
        yield return new WaitForSeconds(seenClearDelay);

        // 0.5초 후 — 창밖 남자가 사라지고, 태블릿이 메시지란·확대로 고정된다(TAB 불가). 2026-10-10 김진선님 수정안.
        if (stage != null) stage.ClearFinale(FinaleRole.WindowMan);   // 들어왔다 — 창밖에서 사라진다
        TabletDocument doc = Document();
        if (doc != null) doc.LockTab(TabletDocument.Tab.Messages);
        TabletZoom zoom = TabletZoom.Active;
        if (zoom != null) zoom.ForceZoom(true);

        // 0.5초 간격으로 한 줄씩 — 「들어왔다」만 붉게.
        yield return new WaitForSeconds(cameInLineGap);
        AddColored(cameIn, cameInColor);
        for (int i = 0; i < seenLines.Length; i++)
        {
            yield return new WaitForSeconds(cameInLineGap);
            Message(seenLines[i]);
        }

        yield return new WaitForSeconds(beforeBurstSeconds);

        Phase = "결말(봤다) — 인수인계";
        if (zoom != null) zoom.SetShake(shakeStart);   // 손 떨림은 몰아치기와 함께 시작해 점점 세진다

        // 묶음 사이 0.4초, 묶음 안에서는 연달아. 손 떨림은 몰아치기 전체 진행도를 따라 세진다.
        float total = confirmedSeconds + actionSeconds + chaosSeconds + seenLineGap * 5f;
        float elapsed = 0f;

        Message(incidentLine);
        elapsed += seenLineGap;
        Shake(zoom, elapsed, total);
        yield return new WaitForSeconds(seenLineGap);

        yield return Repeat(confirmedLine, null, confirmedSeconds, burstInterval, total, elapsed, zoom);
        elapsed += confirmedSeconds + seenLineGap;
        Shake(zoom, elapsed, total);
        yield return new WaitForSeconds(seenLineGap);

        yield return Repeat(actionLine, null, actionSeconds, burstInterval, total, elapsed, zoom);
        elapsed += actionSeconds + seenLineGap;
        Shake(zoom, elapsed, total);
        yield return new WaitForSeconds(seenLineGap);

        Message(terminatedLine);
        elapsed += seenLineGap;
        Shake(zoom, elapsed, total);
        yield return new WaitForSeconds(seenLineGap);

        Message(handoverLine);
        elapsed += seenLineGap;
        Shake(zoom, elapsed, total);
        yield return new WaitForSeconds(seenLineGap);

        yield return Repeat(null, chaosLines, chaosSeconds, chaosInterval, total, elapsed, zoom);

        Phase = "결말(봤다) — 정지";
        yield return FreezeAndBlack(zoom);

        ShowText(finalLine);
        yield return new WaitForSecondsRealtime(finalLineSeconds);
        ShowText(string.Empty);
        yield return new WaitForSecondsRealtime(finalBlackSeconds);

        Phase = "결말(봤다) — 엔딩 크레딧";
        yield return ShowEndingCredits(SeenEndingNumber);

        SilenceUntilNextScene();   // 씬 전환이 일시정지를 풀어도 남은 알람이 들리지 않게
        Finish(true, true);   // → 결과 라우터가 메인으로(근무일지 건너뜀)
    }

    /// <summary>「봤다」 결말 = 엔딩 1(「안 봤다」 = 엔딩 2 — 성공 엔딩 씬의 <c>SuccessEndingFlow</c>).</summary>
    public const int SeenEndingNumber = 1;

    /// <summary>
    /// 엔딩 크레딧(<see cref="FinaleCastSO.CreditsPrefab"/>, 「엔딩 N」) — 2026-10-10 김진선님: 응시 엔딩도 메인으로 가기 전에 크레딧.
    /// 크레딧 프리팹의 DOTween이 게임 시간으로 돌므로 멈춘 시간을 되돌리고, 소리는 일시정지 그대로(무음).
    /// 피날레의 검은 판(정렬 450)은 크레딧(300) 아래로 내려 크레딧이 보이게 한다 — 크레딧 배경이 화면을 다 덮는다.
    /// </summary>
    private IEnumerator ShowEndingCredits(int ending)
    {
        GameObject prefab = FinaleCastSO.Load().CreditsPrefab;
        if (prefab == null) yield break;

        if (_frozen) Time.timeScale = 1f;
        _frozen = false;
        if (_canvas != null) _canvas.sortingOrder = 250;   // 크레딧(300) 아래, 김진선님 페이드(200) 위

        EndingCredits credits = new EndingCredits();
        yield return credits.Play(prefab, ending);   // 넘기면 크레딧의 검은 덮개가 다시 덮인 채 끝난다
        if (_canvas != null) _canvas.sortingOrder = 450;
    }

    /// <summary>
    /// 문자 하나를 <paramref name="interval"/>마다 연달아 보낸다(<paramref name="seconds"/> 동안). <paramref name="pool"/>이 있으면 그중 무작위.
    /// 손 떨림은 몰아치기 전체 진행도(<paramref name="startElapsed"/>/<paramref name="total"/>)를 따라 세진다.
    /// </summary>
    private IEnumerator Repeat(string line, string[] pool, float seconds, float interval, float total, float startElapsed, TabletZoom zoom)
    {
        float t = 0f;
        float next = 0f;
        while (t < seconds)
        {
            if (t >= next)
            {
                string text = pool != null && pool.Length > 0 ? pool[_rng.Next(pool.Length)] : line;
                Message(text);
                next += interval;
            }

            t += Time.deltaTime;
            Shake(zoom, startElapsed + t, total);
            yield return null;
        }
    }

    private void Shake(TabletZoom zoom, float elapsed, float total)
    {
        if (zoom == null) return;
        zoom.SetShake(Mathf.Lerp(shakeStart, shakeEnd, total > 0f ? Mathf.Clamp01(elapsed / total) : 1f));
    }

    /// <summary>소리와 화면을 같은 프레임에 멈추고(지금 화면을 찍어 덮음), <see cref="freezeSeconds"/> 뒤 검은 화면 무음 <see cref="blackSilenceSeconds"/>.</summary>
    private IEnumerator FreezeAndBlack(TabletZoom zoom)
    {
        yield return new WaitForEndOfFrame();
        Texture2D shot = ScreenCapture.CaptureScreenshotAsTexture();
        EnsureUi();
        if (_frozenShot != null) Destroy(_frozenShot);
        _frozenShot = shot;
        _frozenImage.texture = shot;
        _frozenImage.enabled = shot != null;

        AudioListener.pause = true;
        Time.timeScale = 0f;
        _frozen = true;
        if (zoom != null) zoom.SetShake(0f);
        FPController player = FindAnyObjectByType<FPController>();
        if (player != null) player.enabled = false;

        yield return new WaitForSecondsRealtime(freezeSeconds);
        SetBlack(1f);
        _frozenImage.enabled = false;
        yield return new WaitForSecondsRealtime(blackSilenceSeconds);
    }

    // ── 결말: 안 봤다 ─────────────────────────────────────────

    private IEnumerator CheckoutEnding(DirectionStage stage)
    {
        Phase = "결말(안 봤다) — 퇴근 안내";
        WipeAndFillBlank();
        if (stage != null) stage.ClearFinale(FinaleRole.WindowMan);   // 배치된 몬스터 제거
        _doorsNoInteract = true;   // 이제 경비실 문은 상호작용도 받지 않는다
        Message(checkoutNotice);
        ShowText(checkoutNotice);
        _awaitingCheckout = true;

        float shown = 0f;
        while (!_checkoutRequested)
        {
            shown += Time.unscaledDeltaTime;
            if (shown >= noticeSeconds && _text != null && _text.text.Length > 0) ShowText(string.Empty);
            yield return null;
        }

        Phase = "결말(안 봤다) — 퇴근";
        ShowText(string.Empty);
        FPController player = FindAnyObjectByType<FPController>();
        if (player != null) player.enabled = false;
        yield return Fade(1f);
        AudioListener.pause = true;
        SilenceUntilNextScene();
        Finish(true, true);   // → 결과 라우터가 성공 엔딩 씬으로
    }

    private static float s_restoreVolume = -1f;

    /// <summary>
    /// 결말 끝 — 다음 씬이 열릴 때까지 소리를 완전히 끈다. 씬 전환(SceneFlow.GoTo → GamePause.Clear)이 리스너 일시정지를 풀어
    /// 멈춰 있던 태블릿 알람 루프 등이 새 씬이 뜨기 전에 다시 들렸다(2026-10-10 김진선님 「근무 종료 뒤 처리 안 된 알람 소리」).
    /// 알람은 확인 처리하고, 리스너 볼륨을 0으로 눌렀다가 다음 씬이 열리면 되돌린다.
    /// </summary>
    private static void SilenceUntilNextScene()
    {
        foreach (TabletAlarm alarm in FindObjectsByType<TabletAlarm>(FindObjectsInactive.Include, FindObjectsSortMode.None)) alarm.Acknowledge();
        if (s_restoreVolume < 0f)
        {
            s_restoreVolume = AudioListener.volume;
            SceneManager.sceneLoaded -= RestoreListenerVolume;
            SceneManager.sceneLoaded += RestoreListenerVolume;
        }

        AudioListener.volume = 0f;
    }

    private static void RestoreListenerVolume(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single) return;
        SceneManager.sceneLoaded -= RestoreListenerVolume;
        if (s_restoreVolume >= 0f) AudioListener.volume = s_restoreVolume;
        s_restoreVolume = -1f;
    }

    /// <summary>
    /// 끝 — 근무를 정산한다. 피날레로 끝나면 <see cref="NightRun.EndFinale"/>(결말 기록 → DayEnded → 결과 라우터가 결말별 씬으로).
    /// <paramref name="keepBlack"/>면 화면·소리를 그대로 둔다(씬이 바뀌며 풀린다 — SceneFlow가 일시정지·소리를 되돌린다).
    /// </summary>
    private void Finish(bool viaFinale, bool keepBlack)
    {
        Phase = "끝";
        NightRun.Finale.Violation -= OnViolation;
        NightRun.Finale.CloseGazeWindow();
        _awaitingCheckout = false;
        if (_frozen) Time.timeScale = 1f;   // 씬을 옮기기 전에 시간은 되돌린다(화면은 검다)
        _frozen = false;
        ReleaseTablet();
        DisarmDoors();
        if (!keepBlack) AudioListener.pause = false;
        if (_clock != null) _clock.Release(this);
        _running = false;
        _run = null;
        if (!viaFinale || !NightRun.EndFinale()) NightRun.RequestEndNight();
    }

    // ── 강제 복귀 ─────────────────────────────────────────────

    private IEnumerator ForcedReturn()
    {
        FPController player = FindAnyObjectByType<FPController>();
        bool outside = NightRun.CurrentSpace != SpaceId.SecurityRoom;
        if (player != null) player.enabled = false;
        yield return Fade(1f);
        if (outside && CaptureDirector.Active != null) CaptureDirector.Active.TeleportPlayerToStart();
        if (DirectionStage.Active != null) DirectionStage.Active.ClearAllExceptFinale();   // 조기 퇴근(5일차 전화)이면 서 있던 몹·소등이 남아 있을 수 있다
        ShowText(shiftOverNotice);
        Message(shiftOverNotice);
        yield return new WaitForSeconds(noticeSeconds);
        ShowText(string.Empty);
        yield return Fade(0f);
        if (player != null) player.enabled = true;
    }

    // ── K4 위반 → 붙잡힘 → 처음부터 (문이 잠겨 있어 보통은 오지 않는다 — 벽을 뚫는 등 예외용) ─────

    private void OnViolation()
    {
        if (!_running || _violating || _awaitingCheckout) return;
        _violating = true;
        if (_run != null) StopCoroutine(_run);
        _run = null;
        StartCoroutine(ViolationRun());
    }

    private IEnumerator ViolationRun()
    {
        Phase = "K4 위반 — " + NightRun.Finale.ViolationReason;
        Debug.Log("[Finale] K4 위반(" + NightRun.Finale.ViolationReason + ") — 붙잡힘 뒤 피날레 처음부터");
        NightRun.Finale.CloseGazeWindow();
        ReleaseTablet();
        ShowText(string.Empty);
        SetBlack(0f);
        AudioListener.pause = false;

        if (CaptureDirector.Active != null) yield return CaptureDirector.Active.PlayFinaleCapture(HighestSensoryAxis());

        DirectionStage stage = DirectionStage.Active;
        if (stage != null)
        {
            stage.ClearFinale(FinaleRole.WindowMan);
            stage.ClearFinale(FinaleRole.SeatFigure);
        }

        _doorsNoInteract = false;
        NightRun.RestartFinale();
        _violating = false;
        _run = StartCoroutine(Run(false));
    }

    /// <summary>가장 높은 감각축(같으면 청각 → 조도 → 배치).</summary>
    public static FearAxis HighestSensoryAxis()
    {
        IFearAxisReader axes = NightRun.Axes;
        FearAxis best = FearAxis.Auditory;
        if (axes == null) return best;
        int v = axes.GetValue(FearAxis.Auditory);
        if (axes.GetValue(FearAxis.Illuminance) > v)
        {
            best = FearAxis.Illuminance;
            v = axes.GetValue(FearAxis.Illuminance);
        }

        if (axes.GetValue(FearAxis.Layout) > v) best = FearAxis.Layout;
        return best;
    }

    // ── 경비실 문: 닫고 잠근다 · 결말부터는 상호작용도 막는다 ────────────────

    private void SealDoors()
    {
        if (_doors.Count > 0) return;   // 처음부터 다시 할 때는 이미 잠겨 있다
        SpaceZones zones = FindAnyObjectByType<SpaceZones>();
        Bounds box;
        if (zones == null || !zones.TryGetSpaceBox(SpaceId.SecurityRoom, out box)) return;
        box.Expand(new Vector3(1.5f, 0f, 1.5f));

        foreach (DoorHandle h in DoorHandle.All())
        {
            if (!h.IsValid || PlayerInteractor.IsSealed(h)) continue;   // 쓰지 않는 문(판자로 막힌 문 등)은 원래 잠겨 있다
            Vector3 p = h.Owner.transform.position;
            if (!box.Contains(new Vector3(p.x, box.center.y, p.z))) continue;
            _doors.Add(new GuardDoor { Handle = h, WasLocked = h.IsLocked, WasOpen = h.IsOpen });
            if (h.IsOpen) h.Close();
            h.ForceLock();
        }

        Debug.Log("[Finale] 경비실 문 " + _doors.Count + "개 잠금 — 경비실을 나갈 수 없다");
    }

    private void DisarmDoors()
    {
        for (int i = 0; i < _doors.Count; i++)
        {
            DoorHandle h = _doors[i].Handle;
            if (!h.IsValid) continue;
            if (!_doors[i].WasLocked && h.IsLocked) h.ForceUnlock();
            if (_doors[i].WasOpen && !h.IsOpen) h.Open();
        }

        _doors.Clear();
        _doorsNoInteract = false;
    }

    private void Update()
    {
        if (!_running || !_doorsNoInteract || _doors.Count == 0) return;
        Camera cam = Camera.main;
        if (cam == null) return;

        // 상호작용 불가 — 경비실 문을 겨눈 프레임은 문 조작(안내·[E])을 통째로 쉰다(PlayerInteractor는 실행 순서 50, 이것보다 뒤).
        RaycastHit hit;
        if (!Physics.SphereCast(cam.transform.position, 0.06f, cam.transform.forward, out hit, doorBlockRange, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return;
        DoorHandle looked = DoorHandle.Of(hit.collider);
        if (!looked.IsValid) return;
        for (int i = 0; i < _doors.Count; i++)
        {
            if (_doors[i].Handle.IsValid && _doors[i].Handle.Owner == looked.Owner)
            {
                PlayerInteractor.SuppressThisFrame();
                return;
            }
        }
    }

    // ── 태블릿 ─────────────────────────────────────────────────

    private static TabletDocument Document()
    {
        return FindAnyObjectByType<TabletDocument>(FindObjectsInactive.Include);
    }

    /// <summary>
    /// 플레이어가 메시지란을 보고 있는지 — 태블릿 알람(<c>TabletAlarm</c>)이 「확인」으로 치는 것과 같다(태블릿을 들고 메시지 탭이 떠 있음).
    /// CCTV를 들여다보는 동안은 태블릿이 내려가 있어 보지 않은 것으로 본다. 태블릿이 없으면 확인한 것으로(연출이 멈추지 않게).
    /// </summary>
    private static bool MessagesChecked()
    {
        TabletDocument doc = Document();
        if (doc == null) return true;
        if (!doc.isActiveAndEnabled) return false;
        PlayerTablet tablet = doc.GetComponentInParent<PlayerTablet>(true);
        if (tablet != null && !tablet.IsOpened) return false;
        CctvSystem cctv = CctvSystem.Active;
        if (cctv != null && cctv.IsViewing) return false;
        return doc.CurrentTab == TabletDocument.Tab.Messages;
    }

    /// <summary>메시지란·확대 고정과 손 떨림을 푼다.</summary>
    private static void ReleaseTablet()
    {
        TabletDocument doc = Document();
        if (doc != null) doc.UnlockTab();
        TabletZoom zoom = TabletZoom.Active;
        if (zoom != null)
        {
            zoom.SetShake(0f);
            zoom.ForceZoom(false);
        }
    }

    // ── 괴이문자 ────────────────────────────────────────
    //
    // 된소리·거센소리 초성 + 겹모음 + 겹받침을 엮은 낯선 음절(「꿻」「뷁」 같은)만 골라 쓴다.
    // 태블릿 글꼴에 없는 음절은 □로 보이므로 글꼴이 가진 글자만 쓴다(동적 글꼴이면 그 자리에서 채운다).

    private static readonly int[] WeirdInitials = { 1, 4, 8, 10, 13, 15, 16, 17, 18 };          // ㄲ ㄸ ㅃ ㅆ ㅉ ㅋ ㅌ ㅍ ㅎ
    private static readonly int[] WeirdMedials = { 3, 7, 10, 11, 14, 15, 16, 19 };              // ㅒ ㅖ ㅙ ㅚ ㅝ ㅞ ㅟ ㅢ
    private static readonly int[] WeirdFinals = { 3, 5, 6, 9, 10, 11, 12, 13, 14, 15, 18, 27 }; // ㄳ ㄵ ㄶ ㄺ ㄻ ㄼ ㄽ ㄾ ㄿ ㅀ ㅄ ㅎ

    private char WeirdChar(TMP_FontAsset font)
    {
        for (int guard = 0; guard < 40; guard++)
        {
            int ini = WeirdInitials[_rng.Next(WeirdInitials.Length)];
            int med = WeirdMedials[_rng.Next(WeirdMedials.Length)];
            int fin = WeirdFinals[_rng.Next(WeirdFinals.Length)];
            char c = (char)(0xAC00 + (ini * 21 + med) * 28 + fin);
            if (font == null || font.HasCharacter(c, true, true)) return c;
        }

        return '꿻';   // 글꼴에서 못 찾았을 때
    }

    private static TMP_FontAsset TabletFont()
    {
        TabletDocument doc = Document();
        return doc != null && doc.bodyText != null ? doc.bodyText.font : null;
    }

    /// <summary>태블릿 문자 한 통(역설 문자와 같은 통로 — 글리치·알림음이 함께 난다).</summary>
    private void Message(string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        _messageSerial++;
        EventBus.RaiseMessageSent(new ParadoxMessage("finale." + _messageSerial, ProgramCatalog.FinaleRule, SpaceId.SecurityRoom, text, -1));
    }

    /// <summary>글씨 색을 준 태블릿 문자 한 통(메시지함에 바로 — 「들어왔다」만 붉게).</summary>
    private void AddColored(string text, string color)
    {
        TabletMessageList list = FindAnyObjectByType<TabletMessageList>(FindObjectsInactive.Include);
        if (list == null)
        {
            Message(text);
            return;
        }

        _messageSerial++;
        list.Add("finale.colored." + _messageSerial, text, color);
    }

    private static void ClearTabletMessages()
    {
        foreach (TabletMessageList list in FindObjectsByType<TabletMessageList>(FindObjectsInactive.Include, FindObjectsSortMode.None)) list.Clear();
    }

    /// <summary>플레이어 귀(주 카메라) 자리. 태블릿 소리처럼 몸 가까이에서 나는 소리용.</summary>
    private static Vector3 EarPoint()
    {
        Camera cam = Camera.main;
        return cam != null ? cam.transform.position : Vector3.zero;
    }

    private static void PlaySound(string soundName, Vector3 at)
    {
        if (string.IsNullOrEmpty(soundName)) return;
        float v;
        if (Resources.Load<AudioClip>("Direction/" + soundName) == null && DirectionSoundTableSO.FindExact(soundName, out v) == null) return;
        DirectionStage.PlaySound(soundName, at);
    }

    // ── 화면 ─────────────────────────────────────────────────

    private IEnumerator Fade(float to)
    {
        EnsureUi();
        float from = _black.color.a;
        float t = 0f;
        while (t < fadeSeconds)
        {
            t += Time.unscaledDeltaTime;
            SetBlack(Mathf.Lerp(from, to, Mathf.Clamp01(t / Mathf.Max(0.01f, fadeSeconds))));
            yield return null;
        }

        SetBlack(to);
    }

    private void SetBlack(float a)
    {
        if (_black == null) return;
        Color c = _black.color;
        c.a = a;
        _black.color = c;
    }

    private void ShowText(string line)
    {
        EnsureUi();
        _text.text = line ?? string.Empty;
    }

    private void EnsureUi()
    {
        if (_canvas != null) return;
        GameObject root = new GameObject("FinaleCanvas");
        root.transform.SetParent(transform, false);
        _canvas = root.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 450;   // 붙잡힘(500) 아래, 김진선님 페이드(200) 위
        CanvasScaler scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // 멈춘 화면(정지 연출 — 그 프레임을 찍어 덮는다).
        GameObject frozen = new GameObject("Frozen", typeof(RectTransform));
        frozen.transform.SetParent(root.transform, false);
        Stretch((RectTransform)frozen.transform);
        _frozenImage = frozen.AddComponent<RawImage>();
        _frozenImage.raycastTarget = false;
        _frozenImage.enabled = false;

        GameObject black = new GameObject("Black", typeof(RectTransform));
        black.transform.SetParent(root.transform, false);
        Stretch((RectTransform)black.transform);
        _black = black.AddComponent<Image>();
        _black.color = new Color(0f, 0f, 0f, 0f);
        _black.raycastTarget = false;

        GameObject text = new GameObject("Text", typeof(RectTransform));
        text.transform.SetParent(root.transform, false);
        RectTransform tr = (RectTransform)text.transform;
        tr.anchorMin = new Vector2(0.5f, 0.5f);
        tr.anchorMax = new Vector2(0.5f, 0.5f);
        tr.sizeDelta = new Vector2(1600f, 200f);
        TextMeshProUGUI tmp = text.AddComponent<TextMeshProUGUI>();
        FinaleCastSO cast = FinaleCastSO.Load();
        TMP_FontAsset font = cast.ScreenFont != null ? cast.ScreenFont : DonorFont();   // 2026-10-10: Freesentation 5 Medium 36(배역표에서)
        if (font != null) tmp.font = font;
        tmp.fontSize = cast.ScreenFontSize;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(0.82f, 0.82f, 0.78f);
        tmp.raycastTarget = false;
        _text = tmp;
    }

    private static void Stretch(RectTransform r)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = Vector2.zero;
        r.offsetMax = Vector2.zero;
    }

    private static TMP_FontAsset DonorFont()
    {
        foreach (TMP_Text t in FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t != null && t.font != null && t.font.HasCharacter('근')) return t.font;
        }

        return TMP_Settings.defaultFontAsset;
    }
}

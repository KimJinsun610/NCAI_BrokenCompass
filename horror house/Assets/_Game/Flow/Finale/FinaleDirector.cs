using System.Collections;
using System.Collections.Generic;
using NightDuty;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 5일차 피날레 연출(최종 기획서 「5일차 피날레」, 11단계). 근무 씬에 자동으로 선다. 판정은 코어(<see cref="FinaleWatch"/>)가 하고, 이것은 순서·화면·소리만 맡는다.
/// <list type="number">
/// <item>04:00 — 구동기가 정산 대신 <see cref="Begin"/>을 부른다. 경비실 밖이면 강제 복귀 컷(암전 + 「근무 시간이 종료되었습니다.」).</item>
/// <item>K4 공지. 경비실 문은 「응시하면 열리고 다가가면 닫히는」 상태. 나가면 K4 위반 → 가장 높은 감각축의 붙잡힘 장면 → 피날레 처음부터.</item>
/// <item>「들어가게 해줘」 + 창밖 정장 남자 나타남 → 창 두드림(L5와 같은 소리, 피날레 배역표 <see cref="FinaleCastSO"/>).</item>
/// <item>CCTV 다섯 채널을 다 넘겨 보면 모든 몹이 제자리에서 사라져 있다(오래 안 보면 <see cref="cctvTimeout"/> 뒤 넘어감).</item>
/// <item>문자가 전부 지워지고 「들어왔다」. 태블릿 G3의 빈칸이 「당신」으로 채워진다.</item>
/// <item>CRT 험이 멈추고 한 프레임 웃는 얼굴이 비친다.</item>
/// <item>결말: 창을 보지 않았으면 1.5초 정적 뒤 「근무 종료. 수고하셨습니다.」 · 봤으면 창밖 남자가 사라지고, 꺼진 CRT에 비친 내 자리에 무언가가 서 있으며
/// 「근무 교대. 수고하셨습니다.」(그 무언가는 비친 모습에만 보인다). 그다음 <see cref="NightRun.EndFinale"/> → 결과창.</item>
/// </list>
/// 시험: F3 콘솔 「개요」 탭 [5일차로 다시 열기] → [피날레 시작].
/// </summary>
[DisallowMultipleComponent]
public sealed class FinaleDirector : MonoBehaviour
{
    [Header("문구")]
    [SerializeField] private string shiftOverNotice = "근무 시간이 종료되었습니다.";
    [SerializeField] private string letMeIn = "들어가게 해줘";
    [SerializeField] private string cameIn = "들어왔다";
    [SerializeField] private string endingShiftOver = "근무 종료. 수고하셨습니다.";
    [SerializeField] private string endingShiftChange = "근무 교대. 수고하셨습니다.";

    [Header("시간(초)")]
    [SerializeField, Min(0f)] private float fadeSeconds = 0.8f;
    [SerializeField, Min(0f)] private float noticeSeconds = 2.5f;
    [SerializeField, Min(0f)] private float k4Lead = 3f;
    [Tooltip("CCTV 다섯 채널을 다 보지 않아도 이만큼 지나면 다음으로 넘어간다.")]
    [SerializeField, Min(1f)] private float cctvTimeout = 150f;
    [SerializeField, Min(0f)] private float mobsGoneToMessage = 2f;
    [SerializeField, Min(0f)] private float messageToHum = 4f;
    [SerializeField, Min(0f)] private float humToSmile = 1.2f;
    [Tooltip("웃는 얼굴이 CRT에 비치는 시간 — 「한 프레임」(몇 프레임).")]
    [SerializeField, Min(0.01f)] private float smileSeconds = 0.06f;
    [SerializeField, Min(0f)] private float smileToEnding = 2f;
    [SerializeField, Min(0f)] private float silenceSeconds = 1.5f;
    [SerializeField, Min(0f)] private float endingTextSeconds = 4f;
    [Tooltip("「봤다」 결말: 플레이어가 CRT를 볼 때까지 기다리는 최대 시간.")]
    [SerializeField, Min(0f)] private float lookAtCrtTimeout = 10f;
    [SerializeField, Min(0f)] private float reflectionHold = 2.5f;

    [Header("경비실 문 — 응시하면 열리고 다가가면 닫힌다")]
    [SerializeField, Min(0.05f)] private float doorGazeSeconds = 0.5f;
    [SerializeField, Min(0.5f)] private float doorCloseDistance = 1.6f;
    [SerializeField, Min(1f)] private float doorGazeRange = 7f;

    [Header("소리 이름(Resources/Direction 또는 연출 소리 표, 없으면 조용히)")]
    [SerializeField] private string letMeInSound = "finale.letmein";
    [SerializeField] private string smileSound = "finale.smile";

    private sealed class GuardDoor
    {
        public DoorHandle Handle;
        public bool WasLocked;
        public bool WasOpen;
        public float Gaze;
    }

    private readonly List<GuardDoor> _doors = new List<GuardDoor>();
    private Coroutine _run;
    private bool _running;
    private bool _violating;
    private bool _skipCctv;
    private GameTime _clock;
    private int _messageSerial;

    private Camera _reflectionCam;
    private RenderTexture _reflectionRt;
    private Camera _mainCam;
    private int _mainMaskSaved;
    private bool _maskChanged;

    private Canvas _canvas;
    private Image _black;
    private TMP_Text _text;

    /// <summary>지금 근무 씬의 피날레 연출기. 없으면 null.</summary>
    public static FinaleDirector Active { get; private set; }

    /// <summary>피날레가 진행 중인지.</summary>
    public bool IsRunning
    {
        get { return _running; }
    }

    /// <summary>지금 단계(디버그).</summary>
    public string Phase { get; private set; }

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
        NightRun.Finale.SeenNow -= OnSeen;
        DisarmDoor();
        ClearReflection();
        if (_running) AudioListener.pause = false;
        if (_clock != null) _clock.Release(this);
        if (Active == this) Active = null;
    }

    // ── 시작 ─────────────────────────────────────────────────

    /// <summary>
    /// 5일차 04:00 — 구동기가 정산 대신 부른다(디버그 콘솔도). 5일차 밤이 열려 있고 붙잡히지 않았으면 시작하고 true.
    /// </summary>
    public bool Begin()
    {
        if (_running || !NightRun.IsNightActive || NightRun.IsCaptured || NightRun.Day < FinaleWatch.Day || NightRun.Finale.Active) return false;
        _running = true;
        _skipCctv = false;
        _clock = FindAnyObjectByType<GameTime>();
        if (_clock != null) _clock.Hold(this);
        NightRun.Finale.Violation -= OnViolation;
        NightRun.Finale.Violation += OnViolation;
        NightRun.Finale.SeenNow -= OnSeen;
        NightRun.Finale.SeenNow += OnSeen;
        _run = StartCoroutine(Run(true));
        return true;
    }

    /// <summary>디버그: CCTV 다섯 채널을 다 본 것으로 치고 넘어간다.</summary>
    public void DebugSkipCctv()
    {
        _skipCctv = true;
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
                Finish(false);
                yield break;
            }
        }

        FinaleCastSO.Load();
        DirectionStage stage = DirectionStage.Active;
        CctvSystem cctv = CctvSystem.Active;

        Phase = "K4 공지";
        RuleDef k4 = ProgramCatalog.Rule(ProgramCatalog.FinaleRule);
        Message(k4 != null ? k4.Text : "근무 종료 후에는 경비실을 나가지 마십시오.");
        ArmDoor();
        yield return new WaitForSeconds(k4Lead);

        Phase = "들어가게 해줘 · 창 두드림";
        Message(letMeIn);
        FinaleMob window = stage != null ? stage.StageFinale(FinaleRole.WindowMan) : null;
        if (window != null) PlaySound(letMeInSound, window.transform.position + Vector3.up * 1.5f);
        if (window != null)
        {
            yield return window.PlayAndWait(FinaleBeat.Appear);
            if (window != null && window.CurrentBeat == FinaleBeat.Appear) window.Play(FinaleBeat.Knock);
        }

        Phase = "CCTV 다섯 채널";
        int need = cctv != null ? Mathf.Max(1, cctv.ChannelCount) : 5;
        float waited = 0f;
        while (!_skipCctv && NightRun.Finale.ChannelsSeen < need && waited < cctvTimeout)
        {
            waited += Time.deltaTime;
            yield return null;
        }

        Phase = "몹이 사라짐";
        if (stage != null) stage.ClearAllExceptFinale();
        yield return new WaitForSeconds(mobsGoneToMessage);

        Phase = "들어왔다";
        if (window != null) window.Play(FinaleBeat.Idle);   // 두드림이 멎는다 — 들어왔다
        ClearTabletMessages();
        Message(cameIn);
        NightRun.FillFinaleBlank();
        ReloadTablet();
        yield return new WaitForSeconds(messageToHum);

        Phase = "험 멈춤 · 웃는 얼굴";
        if (cctv != null) cctv.SetHum(false);
        yield return new WaitForSeconds(humToSmile);
        yield return FlashSmile(cctv);
        yield return new WaitForSeconds(smileToEnding);

        DisarmDoor();
        if (NightRun.Finale.Seen)
        {
            Phase = "결말 — 근무 교대";
            yield return SeenEnding(stage, cctv, window);
            yield return EndingText(endingShiftChange);
        }
        else
        {
            Phase = "결말 — 근무 종료";
            AudioListener.pause = true;
            yield return new WaitForSecondsRealtime(silenceSeconds);
            yield return EndingText(endingShiftOver);
        }

        Finish(true);
    }

    /// <summary>「봤다」가 된 순간 창밖 남자가 반응한다(Seen) — 두드리던 중이면 다시 두드린다.</summary>
    private void OnSeen()
    {
        if (!_running || _violating || DirectionStage.Active == null) return;
        FinaleMob window = DirectionStage.Active.FinaleOf(FinaleRole.WindowMan);
        if (window == null || window.CurrentBeat == FinaleBeat.Vanish) return;
        FinaleBeat resume = window.CurrentBeat == FinaleBeat.Appear ? FinaleBeat.Knock : window.CurrentBeat;   // 나타나는 중이었으면 반응 뒤 두드린다
        StartCoroutine(SeenReaction(window, resume));
    }

    private IEnumerator SeenReaction(FinaleMob window, FinaleBeat resume)
    {
        yield return window.PlayAndWait(FinaleBeat.Seen);
        if (window != null && window.CurrentBeat == FinaleBeat.Seen && (resume == FinaleBeat.Knock || resume == FinaleBeat.Idle)) window.Play(resume);
    }

    private void Finish(bool viaFinale)
    {
        Phase = "끝";
        NightRun.Finale.Violation -= OnViolation;
        NightRun.Finale.SeenNow -= OnSeen;
        DisarmDoor();
        AudioListener.pause = false;
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
        ShowText(shiftOverNotice);
        Message(shiftOverNotice);
        yield return new WaitForSeconds(noticeSeconds);
        ShowText(string.Empty);
        yield return Fade(0f);
        if (player != null) player.enabled = true;
    }

    // ── K4 위반 → 붙잡힘 → 처음부터 ─────────────────────────────

    private void OnViolation()
    {
        if (!_running || _violating) return;
        _violating = true;
        if (_run != null) StopCoroutine(_run);
        _run = null;
        StartCoroutine(ViolationRun());
    }

    private IEnumerator ViolationRun()
    {
        Phase = "K4 위반 — " + NightRun.Finale.ViolationReason;
        Debug.Log("[Finale] K4 위반(" + NightRun.Finale.ViolationReason + ") — 붙잡힘 뒤 피날레 처음부터");
        DisarmDoor();
        ClearReflection();
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

        if (CctvSystem.Active != null)
        {
            CctvSystem.Active.SetHum(true);
            CctvSystem.Active.SetScreenOverride(null);
        }

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

    // ── 경비실 문: 응시하면 열리고 다가가면 닫힌다 ────────────────────

    private void ArmDoor()
    {
        DisarmDoor();
        SpaceZones zones = FindAnyObjectByType<SpaceZones>();
        Bounds box;
        if (zones == null || !zones.TryGetSpaceBox(SpaceId.SecurityRoom, out box)) return;
        box.Expand(new Vector3(1.5f, 0f, 1.5f));

        foreach (DoorHandle h in DoorHandle.All())
        {
            if (!h.IsValid) continue;
            Vector3 p = h.Owner.transform.position;
            if (!box.Contains(new Vector3(p.x, box.center.y, p.z))) continue;
            GuardDoor d = new GuardDoor { Handle = h, WasLocked = h.IsLocked, WasOpen = h.IsOpen };
            if (h.IsLocked) h.ForceUnlock();
            if (h.IsOpen) h.Close();
            _doors.Add(d);
        }

        Debug.Log("[Finale] 경비실 문 " + _doors.Count + "개 — 응시하면 열리고 다가가면 닫힌다");
    }

    private void DisarmDoor()
    {
        for (int i = 0; i < _doors.Count; i++)
        {
            DoorHandle h = _doors[i].Handle;
            if (!h.IsValid) continue;
            if (h.IsOpen && !_doors[i].WasOpen) h.Close();
            if (_doors[i].WasLocked && !h.IsLocked) h.ForceLock();
        }

        _doors.Clear();
    }

    private void Update()
    {
        if (!_running || _violating || _doors.Count == 0) return;
        FPController player = FindAnyObjectByType<FPController>();
        Camera cam = Camera.main;
        if (player == null || cam == null || !player.enabled) return;

        RaycastHit hit;
        DoorHandle looked = default(DoorHandle);
        if (Physics.Raycast(cam.transform.position, cam.transform.forward, out hit, doorGazeRange, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            looked = DoorHandle.Of(hit.collider);
        }

        for (int i = 0; i < _doors.Count; i++)
        {
            GuardDoor d = _doors[i];
            if (!d.Handle.IsValid) continue;
            Vector3 a = player.transform.position;
            Vector3 b = d.Handle.Owner.transform.position;
            a.y = 0f;
            b.y = 0f;
            if (Vector3.Distance(a, b) < doorCloseDistance)
            {
                d.Gaze = 0f;
                if (d.Handle.IsOpen) d.Handle.Close();
                continue;
            }

            bool gazed = looked.IsValid && looked.Owner == d.Handle.Owner;
            d.Gaze = gazed ? d.Gaze + Time.deltaTime : 0f;
            if (d.Gaze >= doorGazeSeconds && !d.Handle.IsOpen) d.Handle.Open();
        }
    }

    // ── CRT 웃는 얼굴 ─────────────────────────────────────────

    private IEnumerator FlashSmile(CctvSystem cctv)
    {
        if (cctv == null) yield break;
        Camera ch = cctv.ChannelCamera(cctv.CurrentChannel);
        if (ch == null) yield break;

        GameObject prefab = FinaleCastSO.Load().PrefabFor(FinaleRole.WindowMan);
        Vector3 fwd = ch.transform.forward;
        Vector3 flat = Vector3.ProjectOnPlane(fwd, Vector3.up);
        if (flat.sqrMagnitude < 0.0001f) flat = Vector3.forward;
        Quaternion face = Quaternion.LookRotation(-flat.normalized, Vector3.up);
        GameObject go = prefab != null ? Instantiate(prefab, ch.transform.position, face) : StandInFactory.Create("mob.finale", ch.transform.position, face, null);
        if (go == null) yield break;
        go.name = "finale smile";
        foreach (Collider col in go.GetComponentsInChildren<Collider>(true)) col.enabled = false;
        Transform aim = StandInFactory.Aim(go);
        go.transform.position += ch.transform.position + fwd * 0.55f - aim.position;

        PlaySound(smileSound, cctv.ScreenCenter);
        cctv.ForceRenderFor(smileSeconds + 0.05f);
        yield return new WaitForSeconds(smileSeconds);
        Destroy(go);
        Debug.Log("[Finale] CRT에 웃는 얼굴 " + smileSeconds.ToString("0.00") + "초(채널 " + cctv.CurrentChannel + ")");
    }

    // ── 「봤다」 결말: 꺼진 CRT에 비친 내 자리 ─────────────────────────

    private IEnumerator SeenEnding(DirectionStage stage, CctvSystem cctv, FinaleMob window)
    {
        if (window != null) yield return window.PlayAndWait(FinaleBeat.Vanish);

        int layer = FreeLayer();
        FinaleMob seat = stage != null ? stage.StageFinale(FinaleRole.SeatFigure) : null;
        _mainCam = Camera.main;
        if (seat != null && layer >= 0 && _mainCam != null)
        {
            // 그 무언가는 비친 모습에만 보인다 — 플레이어 카메라에서는 그 층을 뺀다.
            foreach (Transform t in seat.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
            _mainMaskSaved = _mainCam.cullingMask;
            _mainCam.cullingMask &= ~(1 << layer);
            _maskChanged = true;
            seat.Play(FinaleBeat.Stand);
        }

        if (cctv != null && cctv.ScreenNormal != Vector3.zero)
        {
            _reflectionRt = new RenderTexture(320, 240, 16);
            _reflectionRt.name = "Finale CRT reflection";
            GameObject camGo = new GameObject("Finale CRT reflection");
            _reflectionCam = camGo.AddComponent<Camera>();
            // 화면 높이(바닥 위 약 0.9m)에서 앉은 눈높이 쪽으로 조금 올려다본다 — 내 자리 뒤에 선 것의 얼굴까지 비치게.
            Quaternion look = Quaternion.LookRotation(cctv.ScreenNormal, Vector3.up) * Quaternion.Euler(-14f, 0f, 0f);
            _reflectionCam.transform.SetPositionAndRotation(cctv.ScreenCenter + cctv.ScreenNormal * 0.03f, look);
            _reflectionCam.fieldOfView = 82f;
            _reflectionCam.nearClipPlane = 0.05f;
            _reflectionCam.farClipPlane = 12f;
            _reflectionCam.targetTexture = _reflectionRt;
            _reflectionCam.cullingMask = (_maskChanged ? _mainMaskSaved : ~0) | (layer >= 0 ? 1 << layer : 0);
            _reflectionCam.clearFlags = CameraClearFlags.SolidColor;
            _reflectionCam.backgroundColor = Color.black;
            cctv.SetHum(false);
            cctv.SetScreenOverride(_reflectionRt, 0.4f);
        }

        float t0 = 0f;
        while (t0 < lookAtCrtTimeout && !LookingAtCrt(cctv))
        {
            t0 += Time.deltaTime;
            yield return null;
        }

        yield return new WaitForSeconds(reflectionHold);
    }

    private static bool LookingAtCrt(CctvSystem cctv)
    {
        Camera cam = Camera.main;
        if (cctv == null || cam == null) return false;
        Vector3 to = cctv.ScreenCenter - cam.transform.position;
        return to.magnitude < 3.5f && Vector3.Dot(cam.transform.forward, to.normalized) > 0.93f;
    }

    private void ClearReflection()
    {
        if (_reflectionCam != null) Destroy(_reflectionCam.gameObject);
        _reflectionCam = null;
        if (_reflectionRt != null)
        {
            _reflectionRt.Release();
            Destroy(_reflectionRt);
        }

        _reflectionRt = null;
        if (_maskChanged && _mainCam != null) _mainCam.cullingMask = _mainMaskSaved;
        _maskChanged = false;
    }

    private static int FreeLayer()
    {
        for (int i = 30; i >= 8; i--)
        {
            if (string.IsNullOrEmpty(LayerMask.LayerToName(i))) return i;   // 31은 붙잡힘 장면이 쓴다
        }

        return -1;
    }

    // ── 태블릿 ─────────────────────────────────────────────────

    private void Message(string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        _messageSerial++;
        EventBus.RaiseMessageSent(new ParadoxMessage("finale." + _messageSerial, ProgramCatalog.FinaleRule, SpaceId.SecurityRoom, text, -1));
    }

    private static void ClearTabletMessages()
    {
        foreach (TabletMessageList list in FindObjectsByType<TabletMessageList>(FindObjectsInactive.Include, FindObjectsSortMode.None)) list.Clear();
    }

    private static void ReloadTablet()
    {
        foreach (TabletDocument doc in FindObjectsByType<TabletDocument>(FindObjectsInactive.Include, FindObjectsSortMode.None)) doc.Reload();
    }

    private static void PlaySound(string soundName, Vector3 at)
    {
        if (string.IsNullOrEmpty(soundName)) return;
        float v;
        if (Resources.Load<AudioClip>("Direction/" + soundName) == null && DirectionSoundTableSO.FindExact(soundName, out v) == null) return;
        DirectionStage.PlaySound(soundName, at);
    }

    // ── 화면 ─────────────────────────────────────────────────

    private IEnumerator EndingText(string line)
    {
        yield return Fade(1f);
        ShowText(line);
        yield return new WaitForSecondsRealtime(endingTextSeconds);
        AudioListener.pause = false;
    }

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

        GameObject black = new GameObject("Black", typeof(RectTransform));
        black.transform.SetParent(root.transform, false);
        RectTransform br = (RectTransform)black.transform;
        br.anchorMin = Vector2.zero;
        br.anchorMax = Vector2.one;
        br.offsetMin = Vector2.zero;
        br.offsetMax = Vector2.zero;
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
        TMP_FontAsset font = DonorFont();
        if (font != null) tmp.font = font;
        tmp.fontSize = 44f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(0.82f, 0.82f, 0.78f);
        tmp.raycastTarget = false;
        _text = tmp;
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

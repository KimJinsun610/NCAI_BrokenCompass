using System.Collections;
using System.Collections.Generic;
using NightDuty;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 놓친 이상을 다음 날 출근 CCTV 한 컷으로 공개(최종 기획서 「근무일지 정산 화면」 — 「어제 02:40, CAM03」, 2026-10-04 39차).
/// 정답을 디에제틱하게 알려 다음 날 판별의 기준이 되게 한다. <b>판정과 무관하다.</b>
/// <list type="bullet">
/// <item>밤 동안 이상을 [정상]으로 보고하면(놓침) 그 시각을, 밤이 끝날 때 이상이 있던 항목을 끝내 보고하지 않았으면(미완료) 그럴듯한 시각을 적어 둔다.</item>
/// <item>다음 날 근무가 시작되고 1.5초 뒤, 경비실 CRT가 그 항목이 있는 공간의 채널 카메라를 <b>그 항목 쪽으로 돌려 한 장 찍은 정지 화면</b>을 띄우고
/// 화면 아래에 「어제 02:40 · CAM03」을 적는다(비디오가 철컥 멈추는 소리 <c>cctv.replay</c>). 10초 동안, 또는 플레이어가 모니터를 들여다보면 그 뒤 4초 더 둔다.</item>
/// <item>놓친 것이 여럿이면 가장 늦은 것 한 컷만. 첫날·놓친 것이 없는 날은 아무 일도 없다.</item>
/// </list>
/// 한 컷을 찍는 순간에만 그 이상의 모습(<see cref="InspectionAnomalies.Preview"/> — 옮김·빛·켬, 그날의 강도)을 잠깐 세웠다가 거둔다(41차). [소리] 항목은 자리만 보여 준다.
/// 근무 씬에 자동으로 선다. 기록은 씬을 넘어(결과 → 다음 날) 정적 필드로 남는다.
/// </summary>
[DisallowMultipleComponent]
public sealed class CctvReplay : MonoBehaviour
{
    /// <summary>놓친 이상 한 건.</summary>
    public readonly struct Missed
    {
        public readonly string ItemId;
        public readonly SpaceId Space;
        public readonly int Minute;
        public readonly bool Unfinished;
        public readonly Band Intensity;

        public Missed(string itemId, SpaceId space, int minute, bool unfinished, Band intensity)
        {
            ItemId = itemId;
            Space = space;
            Minute = minute;
            Unfinished = unfinished;
            Intensity = intensity < Band.Band1 ? Band.Band1 : intensity;
        }
    }

    private const float StartDelay = 1.5f;
    private const float HoldSeconds = 10f;
    private const float AfterViewSeconds = 4f;

    private static readonly List<Missed> s_tonight = new List<Missed>();
    private static readonly List<Missed> s_yesterday = new List<Missed>();
    private static int s_forDay = -1;

    private InspectionPlan _plan;
    private float _nightStart;
    private bool _shown;
    private bool _playing;
    private bool _viewed;
    private RenderTexture _still;
    private GameObject _label;

    /// <summary>지금 근무 씬의 것. 없으면 null.</summary>
    public static CctvReplay Active { get; private set; }

    /// <summary>오늘(그 날) 띄울 어제의 놓친 이상.</summary>
    public static IReadOnlyList<Missed> Yesterday
    {
        get { return s_yesterday; }
    }

    /// <summary><see cref="Yesterday"/>가 어느 날을 위한 것인지.</summary>
    public static int ForDay
    {
        get { return s_forDay; }
    }

    /// <summary>마지막으로 띄운 한 컷의 글(디버그·검수). 아직 없으면 빈 문자열.</summary>
    public string LastCaption { get; private set; }

    // ── 자동 설치 ─────────────────────────────────────────────

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Install()
    {
        Active = null;
        s_tonight.Clear();
        s_yesterday.Clear();
        s_forDay = -1;
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
        if (!FlowAutoInstall.IsDutyScene(scene) || FlowAutoInstall.Exists<CctvReplay>(scene)) return;
        FlowAutoInstall.CreateHost<CctvReplay>(scene, "CctvReplay (auto)");
    }

    private void OnEnable()
    {
        Active = this;
        EventBus.InspectionReported += OnReported;
        EventBus.DayEnded += OnDayEnded;
        CctvSystem.ViewEntered += OnViewEntered;
    }

    private void OnDisable()
    {
        EventBus.InspectionReported -= OnReported;
        EventBus.DayEnded -= OnDayEnded;
        CctvSystem.ViewEntered -= OnViewEntered;
        EndShow();
        if (Active == this) Active = null;
    }

    // ── 기록 ─────────────────────────────────────────────────

    private void OnReported(InspectionReport report)
    {
        s_tonight.RemoveAll(m => m.ItemId == report.ItemId && !m.Unfinished);   // 60차: 판정을 바꾸면(정정) 지난 놓침은 지운다
        if (report.Outcome != ReportOutcome.Missed) return;
        InspectionItem item = InspectionCatalog.Find(report.ItemId);
        InspectionAssignment row = NightRun.Inspections != null && NightRun.Inspections.Plan != null ? NightRun.Inspections.Plan.Find(report.ItemId) : null;
        s_tonight.Add(new Missed(report.ItemId, item != null ? item.Space : SpaceId.None, NightRun.NightMinute, false, row != null ? row.Intensity : Band.Band1));
    }

    private void OnDayEnded(DaySummary summary)
    {
        InspectionBoard board = NightRun.Inspections;
        if (board != null && board.Plan != null)
        {
            IReadOnlyList<InspectionAssignment> rows = board.Plan.Assignments;
            for (int i = 0; i < rows.Count; i++)
            {
                InspectionAssignment a = rows[i];
                if (!a.IsAnomaly || board.StateOf(a.Id) != InspectionState.Pending) continue;
                s_tonight.Add(new Missed(a.Id, a.Item.Space, PlausibleMinute(a.Id), true, a.Intensity));
            }
        }

        s_yesterday.Clear();
        s_yesterday.AddRange(s_tonight);
        s_tonight.Clear();
        s_forDay = summary.Day + 1;
    }

    /// <summary>끝내 보고하지 않은 이상의 「그때」 — 01:00~03:30 사이에서 항목마다 늘 같은 분.</summary>
    public static int PlausibleMinute(string itemId)
    {
        int h = 17;
        foreach (char ch in itemId ?? string.Empty) h = h * 31 + ch;
        return 60 + (int)((uint)h % 151u);
    }

    /// <summary>「어제 02:40 · CAM03」.</summary>
    public static string Caption(int minute, int channel)
    {
        int m = Mathf.Max(0, minute);
        return "어제 " + (m / 60).ToString("00") + ":" + (m % 60).ToString("00") + " · CAM" + (channel + 1).ToString("00");
    }

    // ── 한 컷 ────────────────────────────────────────────────

    private void Update()
    {
        InspectionPlan plan = NightRun.Inspections != null ? NightRun.Inspections.Plan : null;
        if (!ReferenceEquals(plan, _plan))
        {
            _plan = plan;
            _nightStart = Time.time;
            _shown = false;
            s_tonight.Clear();
        }

        if (_shown || !NightRun.IsNightActive || NightRun.IsCaptured || NightRun.Day != s_forDay || s_yesterday.Count == 0) return;
        if (Time.time - _nightStart < StartDelay || CctvSystem.Active == null) return;
        _shown = true;
        StartCoroutine(Show(Latest()));
    }

    private static Missed Latest()
    {
        Missed best = s_yesterday[0];
        for (int i = 1; i < s_yesterday.Count; i++)
        {
            if (s_yesterday[i].Minute > best.Minute) best = s_yesterday[i];
        }

        return best;
    }

    /// <summary>디버그: 지금 이 항목을 놓친 것처럼 한 컷을 띄운다.</summary>
    public bool DebugShow(string itemId, int minute, Band intensity = Band.Band2)
    {
        InspectionItem item = InspectionCatalog.Find(itemId);
        if (item == null || _playing || CctvSystem.Active == null) return false;
        StartCoroutine(Show(new Missed(itemId, item.Space, minute, false, intensity)));
        return true;
    }

    private IEnumerator Show(Missed m)
    {
        CctvSystem cctv = CctvSystem.Active;
        int channel = ChannelFor(cctv, m.Space);
        Camera cam = channel >= 0 ? cctv.ChannelCamera(channel) : null;
        if (cam == null) yield break;

        _playing = true;
        _viewed = false;
        Snap(cam, m.ItemId, m.Intensity);
        cctv.SetScreenOverride(_still, 1f);
        cctv.ForceRenderFor(0.2f);
        LastCaption = Caption(m.Minute, channel);
        MakeLabel(cctv, LastCaption);
        DirectionStage.PlaySound("cctv.replay", cctv.ScreenCenter);
        if (DirectionStage.Verbose) Debug.Log("[CctvReplay] " + LastCaption + " ← " + m.ItemId + (m.Unfinished ? " (미완료)" : " (놓침)"));

        float t = 0f;
        float viewedAt = -1f;
        while (t < HoldSeconds || (viewedAt >= 0f && t < viewedAt + AfterViewSeconds))
        {
            if (_viewed && viewedAt < 0f) viewedAt = t;
            if (NightRun.IsCaptured || !NightRun.IsNightActive) break;
            t += Time.deltaTime;
            yield return null;
        }

        EndShow();
    }

    private void OnViewEntered()
    {
        _viewed = true;
    }

    private void EndShow()
    {
        if (_playing && CctvSystem.Active != null && !CctvSystemOverriddenByOthers()) CctvSystem.Active.SetScreenOverride(null);
        _playing = false;
        if (_label != null) Destroy(_label);
        _label = null;
    }

    private bool CctvSystemOverriddenByOthers()
    {
        return NightRun.Finale.Active;   // 피날레가 화면을 쓰고 있으면 건드리지 않는다
    }

    /// <summary>그 공간을 비추는 채널. 없으면 −1.</summary>
    private static int ChannelFor(CctvSystem cctv, SpaceId space)
    {
        SpaceId want = SpaceIds.Canonical(space);
        for (int i = 0; i < cctv.ChannelCount; i++)
        {
            if (SpaceIds.Canonical(ParadoxRun.SpaceOfChannel(i)) == want) return i;
        }

        return -1;
    }

    /// <summary>채널 카메라를 잠깐 그 항목 쪽으로 돌려 한 장 찍는다(적외선 조명은 CCTV가 그 카메라를 그릴 때 켠다).</summary>
    private void Snap(Camera cam, string itemId, Band intensity)
    {
        if (_still == null)
        {
            RenderTexture live = cam.targetTexture;
            _still = new RenderTexture(live != null ? live.width : 320, live != null ? live.height : 240, 16);
            _still.name = "CCTV replay still";
        }

        Quaternion rot = cam.transform.rotation;
        float fov = cam.fieldOfView;
        RenderTexture target = cam.targetTexture;

        InspectionItem item = InspectionCatalog.Find(itemId);
        JudgeTarget jt;
        if (item != null && JudgeTargetRegistry.TryGet(item.TargetId, out jt) && jt != null)
        {
            Vector3 to = jt.AnchorPosition - cam.transform.position;
            if (to.sqrMagnitude > 0.01f)
            {
                cam.transform.rotation = Quaternion.LookRotation(to.normalized, Vector3.up);
                cam.fieldOfView = Mathf.Clamp(2f * Mathf.Atan(1.6f / to.magnitude) * Mathf.Rad2Deg, 18f, fov);   // 항목 둘레 약 3m가 화면에
            }
        }

        // 어제 그때의 모습 — 이상 연출을 잠깐 세워 찍고 바로 거둔다(오늘 이미 서 있으면 그대로).
        System.Action undo = InspectionAnomalies.Active != null ? InspectionAnomalies.Active.Preview(itemId, intensity) : null;
        cam.targetTexture = _still;
        cam.Render();
        if (undo != null) undo();
        cam.targetTexture = target;
        cam.transform.rotation = rot;
        cam.fieldOfView = fov;
    }

    /// <summary>CRT 화면 아래에 「어제 02:40 · CAM03」.</summary>
    private void MakeLabel(CctvSystem cctv, string text)
    {
        if (_label != null) Destroy(_label);
        Vector3 n = cctv.ScreenNormal;
        Vector2 size = cctv.ScreenSize;
        if (n == Vector3.zero || size.x <= 0f) return;

        _label = new GameObject("CCTV replay label");
        Vector3 up = Vector3.ProjectOnPlane(Vector3.up, n).normalized;
        _label.transform.SetPositionAndRotation(cctv.ScreenCenter + n * 0.004f - up * (size.y * 0.38f), Quaternion.LookRotation(-n, up));
        TextMeshPro tmp = _label.AddComponent<TextMeshPro>();
        TMP_FontAsset font = HangulFont();
        if (font != null) tmp.font = font;
        tmp.text = "<color=#E04A3A>●</color> " + text;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(0.86f, 0.9f, 0.86f, 0.92f);
        tmp.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
        tmp.rectTransform.sizeDelta = new Vector2(size.x, size.y * 0.2f);

        // 글자 크기는 재서 맞춘다 — 화면 가로의 80% 또는 세로의 11% 중 먼저 닿는 쪽.
        tmp.fontSize = 1f;
        tmp.ForceMeshUpdate();
        Vector3 b = tmp.textBounds.size;
        if (b.x > 0f && b.y > 0f) tmp.fontSize = Mathf.Min(size.x * 0.8f / b.x, size.y * 0.11f / b.y);
    }

    private static TMP_FontAsset HangulFont()
    {
        foreach (TMP_Text t in FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t != null && t.font != null && t.font.HasCharacter('어')) return t.font;
        }

        return TMP_Settings.defaultFontAsset;
    }
}

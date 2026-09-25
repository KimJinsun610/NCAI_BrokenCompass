using System.Collections.Generic;
using NightDuty;
using UnityEngine;

/// <summary>
/// <b>임시 태블릿</b>입니다. 오늘 받은 근무 지침을 사람이 읽는 문장으로 보여 주고, 공포 4축을 막대로 보여 줍니다.
///
/// <para><b>왜 만들었나.</b> 하네스 상태판(F3)은 <c>C1</c>·<c>H4</c> 같은 카드 번호만 띄웠습니다.
/// 만든 사람은 알아도 <b>걸어 보는 사람은 무엇을 지켜야 하는지 알 수가 없습니다</b>(2026-09-22 사용자 보고).
/// 지침 문장은 <c>RuleSO.PlayerText</c>에 이미 24장 전부 한국어로 들어 있습니다 — 읽는 쪽이 없었을 뿐입니다.</para>
///
/// <para><b>이것은 진짜 태블릿이 아닙니다.</b> 기획서의 태블릿 UI는 따로 만들어야 합니다(습득·탭 전환·역설 문자 도착 연출).
/// 여기는 그 전까지 <b>걸어서 시험할 수 있게</b> 하는 IMGUI 덮개이고, 버릴 코드입니다.</para>
///
/// <list type="bullet">
/// <item><b>F1</b> — 지침 태블릿 여닫기(기본 켜짐).</item>
/// <item><b>F3</b> — 하네스 상태판(개발용 내부 값).</item>
/// <item>공포 4축 막대는 화면 아래에 늘 떠 있습니다. 값이 오르면 그 자리에 <b>올라간 양</b>이 잠깐 뜹니다.</item>
/// </list>
///
/// <para>읽기만 합니다. 버튼이 없습니다 — 진행 중인 밤을 건드릴 수 있는 것은 하나도 두지 않았습니다.</para>
/// </summary>
[AddComponentMenu("NightDuty/Debug/Duty Tablet Panel")]
[DisallowMultipleComponent]
[DefaultExecutionOrder(-900)]
public sealed class DutyTabletPanel : MonoBehaviour
{
    private const float TabletWidth = 560f;
    private const float BarsWidth = 620f;

    /// <summary>오른 양을 띄워 두는 시간(초).</summary>
    private const float DeltaHoldSeconds = 2.0f;

    /// <summary>축 100이면 게임 오버입니다(신뢰만 예외). 막대의 끝은 그래서 100입니다.</summary>
    private const int AxisMax = 100;

    private static readonly string[] AxisNames = { "청각", "조도", "배치", "신뢰" };

    /// <summary>구간 경계. 폭이 균일하지 않습니다(CLAUDE.md §2.3).</summary>
    private static readonly int[] BandEdges = { 24, 48, 72, 90 };

    private static readonly string[] StateNames =
    {
        "아직", "지금 보는 중", "지켰습니다", "어겼습니다", "판정 못 함", "잠김"
    };

    private static DutyTabletPanel s_instance;

    [SerializeField] private bool showTablet = true;

    private readonly int[] _prev = { -1, -1, -1, -1 };
    private readonly int[] _delta = new int[4];
    private readonly float[] _deltaAt = { -99f, -99f, -99f, -99f };

    private Vector2 _scroll;
    private GUIStyle _title;
    private GUIStyle _rule;
    private GUIStyle _meta;
    private GUIStyle _state;
    private GUIStyle _axis;
    private GUIStyle _delta2;
    private Texture2D _px;

    // ─────────────────────────────── 설치 ───────────────────────────────

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (s_instance != null || FindAnyObjectByType<SpaceZones>() == null)
        {
            return;
        }

        // hideFlags를 붙이지 않는다 — DontSave는 플레이를 꺼도 오브젝트를 남긴다(CLAUDE.md §5.5-23).
        GameObject go = new GameObject("__DutyTablet (runtime)");
        go.AddComponent<DutyTabletPanel>();
    }

    private void Awake()
    {
        s_instance = this;
    }

    private void OnDestroy()
    {
        if (s_instance == this)
        {
            s_instance = null;
        }

        if (_px != null)
        {
            Destroy(_px);
            _px = null;
        }
    }

    private void Update()
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.F1))
        {
            showTablet = !showTablet;
        }
#endif

        TrackAxes();
    }

    /// <summary>축이 움직이면 그 양을 기억해 둔다. 숫자만 보면 「방금 뭐가 올랐지」를 놓친다.</summary>
    private void TrackAxes()
    {
        IFearAxisReader axes = NightRun.Axes;
        if (axes == null)
        {
            return;
        }

        for (int i = 0; i < 4; i++)
        {
            int now = axes.GetValue((FearAxis)i);
            if (_prev[i] < 0)
            {
                _prev[i] = now;
                continue;
            }

            if (now == _prev[i])
            {
                continue;
            }

            _delta[i] = now - _prev[i];
            _deltaAt[i] = Time.unscaledTime;
            _prev[i] = now;
        }
    }

    // ─────────────────────────────── 화면 ───────────────────────────────

    private void OnGUI()
    {
        EnsureStyles();

        float scale = Mathf.Clamp(Screen.width / 1920f, 0.55f, 1.2f);
        Matrix4x4 saved = GUI.matrix;
        GUIUtility.ScaleAroundPivot(new Vector2(scale, scale), Vector2.zero);

        try
        {
            float w = Screen.width / scale;
            float h = Screen.height / scale;

            DrawAxisBars(w, h);

            if (showTablet)
            {
                DrawTablet(w, h);
            }
            else
            {
                GUI.Label(new Rect(w - 220f, 12f, 200f, 22f), "F1 — 근무 지침", _meta);
            }
        }
        finally
        {
            GUI.matrix = saved;
        }
    }

    // ── 지침 태블릿 ──

    private void DrawTablet(float w, float h)
    {
        Rect area = new Rect(w - TabletWidth - 16f, 16f, TabletWidth, h - 150f);
        GUILayout.BeginArea(area, GUI.skin.box);

        GUILayout.Label(NightRun.Day + "일차 근무 지침", _title);
        GUILayout.Label("F1로 닫습니다 · 이 지침을 어기면 해당 감각이 오릅니다", _meta);
        GUILayout.Space(6);

        RuleBook book = NightRun.CurrentBook;
        if (book == null || book.Watchers.Count == 0)
        {
            GUILayout.Label("아직 오늘 지침을 받지 못했습니다.", _rule);
            GUILayout.EndArea();
            return;
        }

        _scroll = GUILayout.BeginScrollView(_scroll);

        DrawBrief();

        for (int i = 0; i < book.Watchers.Count; i++)
        {
            DrawOneRule(i, book.Watchers[i]);
        }

        DrawMessages();

        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    private void DrawOneRule(int index, RuleWatcher watcher)
    {
        RuleSO card = watcher.Card;
        if (card == null)
        {
            return;
        }

        GUILayout.BeginVertical(GUI.skin.box);

        GUILayout.BeginHorizontal();
        GUILayout.Label(Circled(index + 1), _title, GUILayout.Width(30f));

        Color saved = GUI.color;
        GUI.color = StateColor(watcher.State);
        GUILayout.Label(StateName(watcher.State), _state);
        GUI.color = saved;

        GUILayout.FlexibleSpace();
        GUILayout.Label(card.CardId + (card.IsLongTerm ? " · 밤 끝에 정산" : string.Empty), _meta);
        GUILayout.EndHorizontal();

        string text = card.PlayerText;
        GUILayout.Label(string.IsNullOrEmpty(text) ? "(지침 문구가 비어 있습니다)" : text, _rule);

        // 조작 안내는 수칙 본문과 <b>같은 문단에 섞지 않는다</b>(기획서 §3-3). 색과 들여쓰기로 가른다.
        string howTo = card.HowTo;
        if (howTo.Length > 0)
        {
            GUI.color = new Color(0.65f, 0.85f, 1f);
            GUILayout.Label("    " + howTo, _rule);
            GUI.color = saved;
        }

        GUILayout.Label("지키면 신뢰 +" + card.SuccessDelta +
                        "   ·   어기면 " + AxisNames[(int)card.FailureAxis] + " +" + card.FailureDelta, _meta);

        if (watcher.State == CardState.Violated && !string.IsNullOrEmpty(watcher.Reason))
        {
            GUI.color = new Color(1f, 0.6f, 0.55f);
            GUILayout.Label("어긴 까닭: " + watcher.Reason, _meta);
            GUI.color = saved;
        }

        GUILayout.EndVertical();
        GUILayout.Space(4);
    }

    /// <summary>
    /// 수칙 위에 붙는 <b>업무 안내</b>. 판정도 델타도 없는 문구이고 <see cref="DayBriefText"/>가 정본입니다
    /// (기획서 v6 §3-3·§4). 읽지 않았다고 벌점이 붙지 않습니다 — 그래서 카드가 아니라 상수입니다.
    /// <para>1일차는 인계 안내 → 안전 안내 두 장이고, 2일차부터는 그날의 관측 상태 한 줄이 앞에 붙습니다.
    /// <b>DAY는 바깥 날짜가 아니라 회사가 붙인 관측 회차</b>라서 2일차 문구가 「1회차 관측 기록이」로 시작합니다.</para>
    /// </summary>
    private void DrawBrief()
    {
        Color saved = GUI.color;
        GUILayout.BeginVertical(GUI.skin.box);
        GUILayout.Label("업무 안내", _title);

        int day = NightRun.Day;

        if (day <= 1)
        {
            GUI.color = new Color(1f, 0.85f, 0.6f);
            GUILayout.Label(DayBriefText.HandoverNotice, _rule);
            GUI.color = saved;
        }
        else
        {
            string brief = DayBriefText.BriefFor(day);
            if (brief.Length > 0)
            {
                GUI.color = new Color(1f, 0.85f, 0.6f);
                GUILayout.Label(brief, _rule);
                GUI.color = saved;
            }
        }

        GUILayout.Space(4);
        GUILayout.Label(DayBriefText.SafetyNotice, _meta);

        // 퇴실 안내는 마감 정산 뒤에 뜨는 종료 절차입니다(기획서 §5-2). 밤이 끝난 동안만 보입니다.
        if (!NightRun.IsNightActive && !NightRun.IsCaptured)
        {
            GUILayout.Space(4);
            GUI.color = new Color(0.75f, 0.95f, 0.8f);
            GUILayout.Label(DayBriefText.ExitInstruction, _rule);
            GUI.color = saved;
        }

        GUILayout.EndVertical();
        GUILayout.Space(6);
        GUILayout.Label("오늘의 근무수칙", _title);
        GUILayout.Space(2);
    }

    /// <summary>오늘 온 역설 문자. 「지침을 뒤집는 문자」라 태블릿에 같이 떠야 뜻이 있습니다.</summary>
    private void DrawMessages()
    {
        IReadOnlyList<ParadoxMessage> messages = NightRun.MessagesToday;
        if (messages == null || messages.Count == 0)
        {
            return;
        }

        GUILayout.Space(8);
        GUILayout.Label("받은 문자 " + messages.Count + "통", _title);

        for (int i = 0; i < messages.Count; i++)
        {
            ParadoxMessage m = messages[i];
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label(Minute(m.Minute) + "   " + m.ParadoxId + " (" + m.CardId + "번 지침에 대하여)", _meta);
            GUILayout.Label(string.IsNullOrEmpty(m.Text) ? "(문구 없음)" : m.Text, _rule);
            GUILayout.EndVertical();
            GUILayout.Space(3);
        }
    }

    // ── 공포 4축 막대 ──

    private void DrawAxisBars(float w, float h)
    {
        IFearAxisReader axes = NightRun.Axes;
        if (axes == null)
        {
            return;
        }

        const float rowH = 24f;
        const float pad = 10f;
        float boxH = rowH * 4f + pad * 2f + 20f;
        Rect box = new Rect((w - BarsWidth) * 0.5f, h - boxH - 14f, BarsWidth, boxH);

        Color saved = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.55f);
        GUI.DrawTexture(box, Pixel());
        GUI.color = saved;

        GUI.Label(new Rect(box.x + pad, box.y + 2f, 300f, 18f),
            NightRun.IsCaptured ? "포획됨 — " + NightRun.Cause : (NightRun.IsNightActive ? "근무 중" : "밤 아님"), _meta);

        for (int i = 0; i < 4; i++)
        {
            FearAxis axis = (FearAxis)i;
            float y = box.y + 20f + pad + rowH * i;
            DrawOneBar(new Rect(box.x + pad, y, BarsWidth - pad * 2f, rowH - 4f),
                AxisNames[i], axes.GetValue(axis), (int)axes.GetBand(axis), i);
        }
    }

    private void DrawOneBar(Rect r, string name, int value, int band, int index)
    {
        Color saved = GUI.color;

        GUI.Label(new Rect(r.x, r.y, 42f, r.height), name, _axis);

        Rect track = new Rect(r.x + 46f, r.y + 3f, r.width - 220f, r.height - 6f);
        GUI.color = new Color(1f, 1f, 1f, 0.12f);
        GUI.DrawTexture(track, Pixel());

        float fill = Mathf.Clamp01(value / (float)AxisMax);
        GUI.color = BandColor(band, index == (int)FearAxis.Trust);
        GUI.DrawTexture(new Rect(track.x, track.y, track.width * fill, track.height), Pixel());

        // 구간 경계 눈금. 폭이 균일하지 않으므로 눈으로 보이는 편이 낫다.
        GUI.color = new Color(0f, 0f, 0f, 0.55f);
        for (int i = 0; i < BandEdges.Length; i++)
        {
            float x = track.x + track.width * (BandEdges[i] / (float)AxisMax);
            GUI.DrawTexture(new Rect(x, track.y, 1f, track.height), Pixel());
        }

        GUI.color = saved;
        GUI.Label(new Rect(track.xMax + 8f, r.y, 110f, r.height), value + " / 구간 " + band, _axis);

        if (Time.unscaledTime - _deltaAt[index] > DeltaHoldSeconds || _delta[index] == 0)
        {
            return;
        }

        bool up = _delta[index] > 0;
        GUI.color = index == (int)FearAxis.Trust
            ? new Color(0.55f, 0.95f, 0.65f)
            : (up ? new Color(1f, 0.45f, 0.4f) : new Color(0.7f, 0.8f, 1f));
        GUI.Label(new Rect(track.xMax + 118f, r.y, 60f, r.height), (up ? "+" : string.Empty) + _delta[index], _delta2);
        GUI.color = saved;
    }

    // ─────────────────────────────── 거들기 ───────────────────────────────

    private static string StateName(CardState state)
    {
        int i = (int)state;
        return i >= 0 && i < StateNames.Length ? StateNames[i] : state.ToString();
    }

    private static Color StateColor(CardState state)
    {
        switch (state)
        {
            case CardState.Complied: return new Color(0.55f, 0.95f, 0.65f);
            case CardState.Violated: return new Color(1f, 0.45f, 0.4f);
            case CardState.Active: return new Color(1f, 0.88f, 0.45f);
            case CardState.Undetermined: return new Color(1f, 0.72f, 0.4f);
            case CardState.Locked: return new Color(0.55f, 0.55f, 0.55f);
            default: return new Color(0.75f, 0.75f, 0.75f);
        }
    }

    /// <summary>구간이 오를수록 붉어진다. 신뢰는 반대로 푸르게 — 올라서 좋은 축이다.</summary>
    private static Color BandColor(int band, bool trust)
    {
        if (trust)
        {
            return new Color(0.45f, 0.75f, 1f, 0.95f);
        }

        switch (band)
        {
            case 0: return new Color(0.55f, 0.85f, 0.6f, 0.95f);
            case 1: return new Color(0.85f, 0.9f, 0.5f, 0.95f);
            case 2: return new Color(0.98f, 0.75f, 0.35f, 0.95f);
            case 3: return new Color(1f, 0.5f, 0.3f, 0.95f);
            default: return new Color(1f, 0.28f, 0.28f, 0.95f);
        }
    }

    private static string Circled(int n)
    {
        return n >= 1 && n <= 9 ? ((char)('①' + n - 1)).ToString() : n + ".";
    }

    /// <summary>근무 시작(00:00)부터 몇 분인지를 시각으로.</summary>
    private static string Minute(int minute)
    {
        int hh = minute / 60;
        int mm = minute % 60;
        return hh.ToString("00") + ":" + mm.ToString("00");
    }

    private Texture2D Pixel()
    {
        if (_px == null)
        {
            _px = new Texture2D(1, 1);
            _px.SetPixel(0, 0, Color.white);
            _px.Apply();
        }

        return _px;
    }

    private void EnsureStyles()
    {
        if (_title != null)
        {
            return;
        }

        _title = new GUIStyle(GUI.skin.label);
        _title.fontSize = 17;
        _title.fontStyle = FontStyle.Bold;

        _rule = new GUIStyle(GUI.skin.label);
        _rule.fontSize = 15;
        _rule.wordWrap = true;

        _meta = new GUIStyle(GUI.skin.label);
        _meta.fontSize = 12;
        _meta.wordWrap = true;
        _meta.normal.textColor = new Color(0.72f, 0.72f, 0.72f);

        _state = new GUIStyle(GUI.skin.label);
        _state.fontSize = 14;
        _state.fontStyle = FontStyle.Bold;

        _axis = new GUIStyle(GUI.skin.label);
        _axis.fontSize = 14;

        _delta2 = new GUIStyle(GUI.skin.label);
        _delta2.fontSize = 16;
        _delta2.fontStyle = FontStyle.Bold;
    }
}

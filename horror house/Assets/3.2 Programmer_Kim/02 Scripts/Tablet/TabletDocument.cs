using System.Collections.Generic;
using NightDuty;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 태블릿 화면. 기획안의 문서 서식을 그대로 따른다.
///
/// <code>
///   상단 상태바   신호·배터리 / 현재 시각(GameTimeView가 따로 갱신)
///   문서 헤더     발신 · 문서번호 · 발행시각
///   근무 정보 바  근무일 · 근무 시간 범위 · 순찰 지점 수
///   탭            근무 수칙 / 메시지(안 읽은 것이 있으면 점)
///   본문          섹션 제목 + 항목 (세로 스크롤)
///   하단          항목 개수 · 조작 안내
///   하단바        고정 면책 문구
/// </code>
///
/// 수칙 본문은 <b>카드 데이터(RuleSO)에서 읽어 온다.</b> 이 스크립트에 옮겨 적지 않는다.
/// 기획이 본문을 고치면 카드 에셋만 고치면 되고, 여기는 건드릴 필요가 없다.
///
/// 표시 규칙(기획 정본):
/// - 태블릿에는 <b>수칙 본문만</b> 보여 준다. 카드 ID·판정 상세·수치는 보여 주지 않는다.
/// - 그날 덱에 들어 있는 수칙만 보여 준다(하루 6장).
/// - 메시지는 <b>발신자 없이</b> 수신 시각 + 본문만, <b>최신순</b>으로 보여 준다.
/// </summary>
[DisallowMultipleComponent]
public class TabletDocument : MonoBehaviour
{
    [Header("상단 — 상태바 · 문서 헤더")]
    [Tooltip("신호·배터리 아이콘 자리. 실제 아이콘 이미지가 오면 이 줄 대신 써도 된다.")]
    public TMP_Text statusIconText;
    [Tooltip("발신 줄. 예: 발신 시설관리팀")]
    public TMP_Text senderText;
    [Tooltip("문서번호와 발행 시각 줄. 예: NS-217-01 · 발행 01:40")]
    public TMP_Text documentText;

    [Header("근무 정보 바")]
    [Tooltip("근무일 · 근무 시간 · 순찰 지점 수를 한 줄에 적는다.")]
    [FormerlySerializedAs("headerText")]
    public TMP_Text dutyBarText;

    [Header("본문")]
    [Tooltip("탭 이름을 보여 주는 줄. 보고 있는 쪽이 밝게 표시된다.")]
    public TMP_Text tabText;
    [Tooltip("섹션 제목. 예: 근무 수칙 / 수신 메시지")]
    public TMP_Text sectionTitleText;
    [Tooltip("항목 본문. 이 글상자 안에서 세로로 스크롤한다.")]
    public TMP_Text bodyText;
    [Tooltip("항목 개수. 예: 6 항목")]
    [FormerlySerializedAs("pageText")]
    public TMP_Text countText;
    [Tooltip("조작 안내 줄. 지금 화면에서 쓸 수 있는 키만 적힌다. 비워 두면 표시하지 않는다.")]
    public TMP_Text hintText;

    [Header("하단바")]
    [Tooltip("항상 같은 문구가 붙는 면책 줄.")]
    public TMP_Text disclaimerText;

    [Header("고정 문구")]
    [Tooltip("상태바에 적을 신호·배터리 표시. 실제 아이콘이 오면 비우고 이미지로 바꾸면 된다.")]
    public string statusIcons = "●●○  ■■□";
    [Tooltip("문서를 보낸 곳. 기획이 정한 고정값.")]
    public string senderName = "시설관리팀";
    [Tooltip("문서번호 앞부분. 뒤에 일차 두 자리가 붙는다. 예: NS-217 → NS-217-01")]
    public string documentPrefix = "NS-217";
    [Tooltip("근무 시작 몇 분 전에 발행된 문서로 적을지. 예: 20이면 02:00 근무의 발행 시각은 01:40")]
    [Min(0)] public int issueMinutesBeforeShift = 20;
    [Tooltip("순찰 지점 수. 기획 스코프는 4곳(복도·교실·과학실·화장실).")]
    [Min(0)] public int patrolPointCount = 4;
    [Tooltip("하단 고정 안내 문구.")]
    [TextArea(2, 3)] public string disclaimer = "본 지침 미준수로 발생한 사고 및 심리적 손상에 대해 회사는 책임지지 않습니다.";

    [Header("안전 안내")]
    [Tooltip("태블릿을 켜면 가장 먼저 보이는 안내문. 날짜와 상관없이 늘 같다.\n" +
             "근무수칙(RuleSO)과 달리 판정 대상이 아니다. 여기 적은 문장으로 플레이어를 채점하지 않는다.")]
    [TextArea(1, 3)] public string[] safetyNotices =
    {
        "근무 중에는 지급된 태블릿을 항상 휴대하십시오.",
        "정전 시 손전등을 사용하고, 배전반을 임의로 조작하지 마십시오.",
        "시설 내 이상을 발견하면 근무 수칙에 따라 조치하십시오.",
        "비상 상황에는 경비실로 복귀하십시오.",
        "본 안내는 근무 수칙에 우선하지 않습니다."
    };

    [Header("내용")]
    [Tooltip("비워 두면 Resources에서 편성표를 찾는다.")]
    public NightDeckTableSO deckTable;
    [Tooltip("켜면 GameSession의 현재 일차를 쓴다. 끄면 아래 값을 쓴다(씬 단독 확인용).")]
    public bool useGameSessionDay = true;
    [Min(1)] public int previewDay = 1;
    [Tooltip("비워 두면 씬에서 찾는다. 근무 시간 범위와 발행 시각을 여기서 읽는다.")]
    public GameTime gameTime;

    [Header("메시지")]
    [Tooltip("메시지 목록. 비워 두면 같은 오브젝트에서 찾는다.")]
    [FormerlySerializedAs("taskList")]
    public TabletMessageList messageList;
    [Tooltip("메시지 시각을 적을 색(16진수).")]
    public string messageTimeColor = "#738C99";

    [Header("조작")]
    [Tooltip("탭을 오가는 키.")]
    public KeyCode switchTabKey = KeyCode.Q;
    [Tooltip("아래로 스크롤. 마우스 휠도 함께 동작한다.")]
    public KeyCode scrollDownKey = KeyCode.DownArrow;
    public KeyCode scrollUpKey = KeyCode.UpArrow;
    [Tooltip("한 번에 몇 줄씩 스크롤할지.")]
    [Min(1)] public int linesPerScroll = 2;
    [Tooltip("본문에 한 번에 보일 줄 수. 글상자 높이에 맞춰 정한다.")]
    [Min(1)] public int visibleLines = 9;
    [Tooltip("태블릿을 들고 있을 때만 입력을 받는다. 비어 있으면 같은 오브젝트 위에서 찾는다.")]
    public PlayerTablet tablet;

    [Header("나타나는 연출")]
    [Tooltip("태블릿이 올라오는 동안 글자를 서서히 띄운다. 끄면 화면이 켜지는 순간 바로 보인다.")]
    public bool fadeTextWithOpen = true;
    [Tooltip("몇 %쯤 올라왔을 때부터 글자가 보이기 시작할지(0~1).")]
    [Range(0f, 1f)] public float fadeStartAt = 0.5f;

    /// <summary>태블릿에 띄울 수 있는 화면 종류. <b>적힌 순서대로</b> 탭 줄에 놓이고 Q로 돌아간다.</summary>
    public enum Tab
    {
        Safety,    // 안전 안내 — 태블릿을 처음 켜면 이 화면이 먼저 보인다
        Rules,     // 근무 수칙
        Messages   // 수신 메시지
    }

    private static readonly Tab[] TabOrder = { Tab.Safety, Tab.Rules, Tab.Messages };

    private readonly List<RuleSO> _rules = new List<RuleSO>();
    private int _loadedDay = -1;
    private TabletGlitch _glitch;
    private Tab _tab = Tab.Safety;

    // 스크롤: 지금 맨 위에 보이는 줄 번호와, 마지막으로 잰 전체 줄 수
    private int _scrollLine;
    private int _totalLines = 1;

    /// <summary>지금 보고 있는 화면.</summary>
    public Tab CurrentTab { get { return _tab; } }

    /// <summary>지금 화면에 들어 있는 항목 수.</summary>
    public int ItemCount
    {
        get
        {
            if (_tab == Tab.Safety) return safetyNotices != null ? safetyNotices.Length : 0;
            if (_tab == Tab.Rules) return _rules.Count;
            return messageList != null ? messageList.Count : 0;
        }
    }

    /// <summary>맨 위에 보이는 줄 번호(0부터).</summary>
    public int ScrollLine { get { return _scrollLine; } }

    private void Awake()
    {
        if (tablet == null) tablet = GetComponentInParent<PlayerTablet>();
        if (messageList == null) messageList = GetComponent<TabletMessageList>();
        if (messageList == null) messageList = GetComponentInParent<TabletMessageList>();
        if (gameTime == null) gameTime = FindAnyObjectByType<GameTime>();
    }

    private void OnEnable()
    {
        // 화면을 켤 때마다 첫 탭(안전 안내)으로 되돌린다.
        // 태블릿을 들 때마다 화면이 꺼졌다 켜지므로, 열 때는 언제나 안전 안내가 먼저 보인다.
        _tab = TabOrder[0];
        _scrollLine = 0;

        // 태블릿을 열 때마다 화면이 켜지므로, 그때 일차가 바뀌었으면 다시 읽는다.
        if (messageList != null) messageList.Changed += Render;
        Reload();
    }

    private void OnDisable()
    {
        if (messageList != null) messageList.Changed -= Render;
    }

    private void Update()
    {
        FadeWithOpenAmount();

        if (ViewmodelTime.Paused) return;          // 일시정지 중에는 스크롤도 막는다
        if (tablet != null && !tablet.IsOpened) return;

        if (Input.GetKeyDown(switchTabKey))
        {
            SwitchTab();
            return;
        }

        float wheel = Input.mouseScrollDelta.y;
        if (Input.GetKeyDown(scrollDownKey) || wheel < -0.01f) ScrollBy(linesPerScroll);
        else if (Input.GetKeyDown(scrollUpKey) || wheel > 0.01f) ScrollBy(-linesPerScroll);
    }

    /// <summary>안전 안내 → 근무 수칙 → 메시지 순서로 돌아간다.</summary>
    public void SwitchTab()
    {
        SetTab(NextTab(_tab));
    }

    private static Tab NextTab(Tab from)
    {
        for (int i = 0; i < TabOrder.Length; i++)
        {
            if (TabOrder[i] == from) return TabOrder[(i + 1) % TabOrder.Length];
        }
        return TabOrder[0];
    }

    /// <summary>탭 이름. 탭 줄과 조작 안내가 같은 말을 쓰도록 한 곳에서만 적는다.</summary>
    private static string TabName(Tab tab)
    {
        switch (tab)
        {
            case Tab.Safety: return "안전 안내";
            case Tab.Rules: return "근무 수칙";
            default: return "메시지";
        }
    }

    public void SetTab(Tab value)
    {
        if (_tab == value) return;

        _tab = value;
        _scrollLine = 0;   // 화면을 바꾸면 맨 위부터

        // 메시지를 펼친 순간 안 읽음 표시를 지운다(탭 옆 점이 사라진다).
        if (_tab == Tab.Messages && messageList != null) messageList.MarkAllRead();

        Render();
    }

    /// <summary>본문을 위아래로 굴린다. 끝을 넘지 않는다.</summary>
    public void ScrollBy(int lines)
    {
        int maxLine = Mathf.Max(0, _totalLines - visibleLines);
        int next = Mathf.Clamp(_scrollLine + lines, 0, maxLine);
        if (next == _scrollLine) return;

        _scrollLine = next;
        ApplyScroll();
    }

    /// <summary>편성표에서 그날 수칙을 다시 읽어 처음부터 보여 준다.</summary>
    public void Reload()
    {
        int day = useGameSessionDay ? GameSession.CurrentDay : previewDay;
        LoadRules(day);
        _scrollLine = 0;
        Render();
    }

    /// <summary>
    /// 태블릿이 올라오는 동안 글자가 서서히 나타나게 한다.
    /// 화면 오브젝트는 켜졌다 꺼졌다 하지만, 글자 투명도를 같이 움직여서 툭 나타나 보이지 않게 한다.
    /// </summary>
    private void FadeWithOpenAmount()
    {
        if (tablet == null || !fadeTextWithOpen) return;

        // 화면이 켜지는 지점부터 완전히 올라올 때까지 0 → 1
        float open = tablet.OpenAmount;
        float from = Mathf.Clamp01(fadeStartAt);
        float alpha = from >= 0.999f ? 1f : Mathf.Clamp01((open - from) / (1f - from));
        alpha = Mathf.SmoothStep(0f, 1f, alpha);

        SetAlpha(statusIconText, alpha);
        SetAlpha(senderText, alpha);
        SetAlpha(documentText, alpha);
        SetAlpha(dutyBarText, alpha);
        SetAlpha(sectionTitleText, alpha);
        SetAlpha(bodyText, alpha);
        SetAlpha(countText, alpha);
        SetAlpha(hintText, alpha);
        SetAlpha(disclaimerText, alpha);
    }

    private static void SetAlpha(TMP_Text text, float alpha)
    {
        if (text == null) return;
        if (Mathf.Abs(text.alpha - alpha) < 0.003f) return;
        text.alpha = alpha;
    }

    private void LoadRules(int day)
    {
        _rules.Clear();
        _loadedDay = day;

        NightDeckTableSO table = deckTable;
        if (table == null) table = Resources.Load<NightDeckTableSO>(NightDeckTableSO.ResourcePath);
        if (table == null)
        {
            Debug.LogWarning("[TabletDocument] 편성표를 찾지 못했습니다: Resources/" + NightDeckTableSO.ResourcePath);
            return;
        }

        IReadOnlyList<RuleSO> deck = table.DeckFor(day);
        for (int i = 0; i < deck.Count; i++)
        {
            if (deck[i] != null && !string.IsNullOrEmpty(deck[i].PlayerText))
            {
                _rules.Add(deck[i]);
            }
        }
    }

    private void Render()
    {
        // 글리치가 글자를 깨진 기호로 바꿔 놓았을 수 있다.
        // 새 내용을 쓰기 <b>전에</b> 원본을 되돌려야 한다. 쓰고 나서 되돌리면 방금 쓴 내용이 옛 글자로 덮인다
        // (탭을 바꿔도 화면이 그대로인 것처럼 보였던 원인).
        if (_glitch == null) _glitch = GetComponent<TabletGlitch>();
        if (_glitch != null) _glitch.RefreshCleanText();

        RenderStatusBar();
        RenderDocumentHeader();
        RenderDutyBar();
        RenderTabLine();

        if (sectionTitleText != null)
        {
            sectionTitleText.text = _tab == Tab.Messages ? "수신 메시지" : TabName(_tab);
        }

        if (bodyText != null)
        {
            if (_tab == Tab.Safety) bodyText.text = BuildSafety();
            else if (_tab == Tab.Rules) bodyText.text = BuildRules();
            else bodyText.text = BuildMessages();

            ApplyScroll();
        }

        if (countText != null)
        {
            int count = ItemCount;
            countText.text = count > 0 ? count + " 항목" : string.Empty;
        }

        if (disclaimerText != null) disclaimerText.text = disclaimer;

        RenderHintLine();
    }

    /// <summary>신호·배터리 표시. 현재 시각은 GameTimeView가 따로 갱신하므로 여기서 건드리지 않는다.</summary>
    private void RenderStatusBar()
    {
        if (statusIconText != null) statusIconText.text = statusIcons;
    }

    /// <summary>발신 · 문서번호 · 발행 시각.</summary>
    private void RenderDocumentHeader()
    {
        if (senderText != null) senderText.text = "발신 " + senderName;

        if (documentText == null) return;

        // 문서번호는 일차를 두 자리로 붙인다(1일차 → NS-217-01).
        string number = documentPrefix + "-" + _loadedDay.ToString("00");
        documentText.text = number + " · 발행 " + IssueTimeText();
    }

    /// <summary>근무일 · 근무 시간 범위 · 순찰 지점 수.</summary>
    private void RenderDutyBar()
    {
        if (dutyBarText == null) return;

        string day = "근무일 " + _loadedDay + " / " + GameSession.FinalDay;
        string range = ShiftRangeText();
        string points = "순찰 지점 " + patrolPointCount + "곳";

        dutyBarText.text = string.IsNullOrEmpty(range)
            ? day + "   " + points
            : day + "   " + range + "   " + points;
    }

    /// <summary>보고 있는 탭을 밝게, 나머지는 흐리게 적는다. 안 읽은 메시지가 있으면 점을 붙인다.</summary>
    private void RenderTabLine()
    {
        if (tabText == null) return;

        bool unread = messageList != null && messageList.HasUnread;

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        for (int i = 0; i < TabOrder.Length; i++)
        {
            Tab tab = TabOrder[i];
            string label = TabName(tab);
            if (tab == Tab.Messages && unread) label += " ●";

            if (sb.Length > 0) sb.Append("  ");
            if (tab == _tab) sb.Append("<b>").Append(label).Append("</b>");
            else sb.Append("<color=#4C5D66>").Append(label).Append("</color>");
        }
        tabText.text = sb.ToString();
    }

    /// <summary>
    /// 화면 아래에 조작 키를 적는다.
    /// <b>지금 화면에서 실제로 쓸 수 있는 키만</b> 적는다. 넘칠 내용이 없으면 스크롤 안내를 빼는 식이다.
    /// 키를 인스펙터에서 바꾸면 안내 문구도 따라 바뀐다(문구에 키 이름을 박아 두지 않는다).
    /// </summary>
    private void RenderHintLine()
    {
        if (hintText == null) return;

        string tabHint = KeyLabel(switchTabKey) + " : " + TabName(NextTab(_tab));
        bool canScroll = _totalLines > visibleLines;

        hintText.text = canScroll ? "휠 : 스크롤   " + tabHint : tabHint;
    }

    /// <summary>KeyCode를 화면에 적기 좋은 짧은 이름으로 바꾼다.</summary>
    private static string KeyLabel(KeyCode key)
    {
        switch (key)
        {
            case KeyCode.RightArrow: return "→";
            case KeyCode.LeftArrow: return "←";
            case KeyCode.UpArrow: return "↑";
            case KeyCode.DownArrow: return "↓";
            case KeyCode.Return: return "Enter";
            case KeyCode.Escape: return "Esc";
            default: return key.ToString();
        }
    }

    /// <summary>근무 시간 범위. 시계가 없으면 빈 문자열이라 그 칸이 빠진다.</summary>
    private string ShiftRangeText()
    {
        if (gameTime == null) gameTime = FindAnyObjectByType<GameTime>();
        if (gameTime == null) return string.Empty;

        return gameTime.StartTimeText + " - " + gameTime.EndTimeText;
    }

    /// <summary>발행 시각. 근무 시작 시각보다 지정한 만큼 이르게 적는다.</summary>
    private string IssueTimeText()
    {
        if (gameTime == null) gameTime = FindAnyObjectByType<GameTime>();
        if (gameTime == null) return "--:--";

        // 자정을 넘겨 빼면 음수가 되므로 하루를 더해서 돌린다(01:40 → 23:40).
        int minutes = gameTime.StartMinutes - issueMinutesBeforeShift;
        minutes = ((minutes % 1440) + 1440) % 1440;
        return gameTime.FormatTime(minutes);
    }

    /// <summary>
    /// 안전 안내. 날마다 바뀌는 수칙과 달리 늘 같은 문장이라 인스펙터 값에서 그대로 읽는다.
    /// <b>판정 대상이 아니다.</b> 여기 적힌 문장으로 플레이어를 채점하지 않는다.
    /// </summary>
    private string BuildSafety()
    {
        if (safetyNotices == null || safetyNotices.Length == 0) return "안내 사항이 없습니다.";

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        for (int i = 0; i < safetyNotices.Length; i++)
        {
            if (string.IsNullOrEmpty(safetyNotices[i])) continue;

            if (sb.Length > 0) sb.Append('\n');
            // 수칙은 번호, 안내는 점으로 구분해서 서로 다른 문서라는 걸 알게 한다.
            sb.Append("· ").Append(safetyNotices[i]).Append('\n');
        }
        return sb.ToString();
    }

    private string BuildRules()
    {
        if (_rules.Count == 0) return "오늘 배정된 수칙이 없습니다.";

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        for (int i = 0; i < _rules.Count; i++)
        {
            if (sb.Length > 0) sb.Append('\n');
            // 카드 ID는 제작자용이라 보여 주지 않고, 덱 순서대로 번호만 붙인다.
            sb.Append(i + 1).Append(". ").Append(_rules[i].PlayerText).Append('\n');
        }
        return sb.ToString();
    }

    /// <summary>수신 시각 + 본문. 발신자는 적지 않고 최신순으로 쌓는다.</summary>
    private string BuildMessages()
    {
        if (messageList == null || messageList.Count == 0) return "수신된 메시지가 없습니다.";

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        IReadOnlyList<TabletMessageList.Message> list = messageList.Messages;

        // 목록은 도착 순서대로 쌓이므로 뒤에서부터 읽어 최신순으로 만든다.
        for (int i = list.Count - 1; i >= 0; i--)
        {
            TabletMessageList.Message message = list[i];
            if (sb.Length > 0) sb.Append('\n');

            string time = MessageTimeText(message);
            if (!string.IsNullOrEmpty(time))
            {
                sb.Append("<color=").Append(messageTimeColor).Append('>').Append(time).Append("</color>  ");
            }

            sb.Append(message.text).Append('\n');
        }
        return sb.ToString();
    }

    private string MessageTimeText(TabletMessageList.Message message)
    {
        if (message.receivedMinutes < 0) return string.Empty;
        if (gameTime == null) gameTime = FindAnyObjectByType<GameTime>();
        if (gameTime == null) return string.Empty;

        return gameTime.FormatTime(message.receivedMinutes);
    }

    /// <summary>
    /// 본문을 줄 단위로 굴린다.
    /// TMP는 글상자를 잘라 주지 않으므로, <b>첫 글자와 보일 줄 수</b>를 지정해 창처럼 보여 준다.
    /// 전체 줄 수를 재려면 제한을 풀고 한 번 계산해야 한다.
    /// </summary>
    private void ApplyScroll()
    {
        if (bodyText == null) return;

        // ① 제한을 풀고 전체 줄 수를 잰다.
        bodyText.firstVisibleCharacter = 0;
        bodyText.maxVisibleLines = int.MaxValue;
        bodyText.ForceMeshUpdate();

        TMP_TextInfo info = bodyText.textInfo;
        _totalLines = Mathf.Max(1, info.lineCount);

        int maxLine = Mathf.Max(0, _totalLines - visibleLines);
        _scrollLine = Mathf.Clamp(_scrollLine, 0, maxLine);

        // ② 맨 위에 보일 줄의 첫 글자를 찾아 거기서부터 다시 흘린다.
        int firstChar = 0;
        if (_scrollLine > 0 && _scrollLine < info.lineCount)
        {
            firstChar = info.lineInfo[_scrollLine].firstCharacterIndex;
        }

        bodyText.firstVisibleCharacter = firstChar;
        bodyText.maxVisibleLines = visibleLines;
    }
}

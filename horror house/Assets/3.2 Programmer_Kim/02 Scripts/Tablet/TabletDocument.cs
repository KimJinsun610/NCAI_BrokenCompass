using System.Collections.Generic;
using NightDuty;
using TMPro;
using UnityEngine;

/// <summary>
/// 태블릿 화면에 그날의 근무수칙을 한 장씩 보여 준다.
///
/// 수칙 본문은 <b>카드 데이터(RuleSO)에서 읽어 온다.</b> 이 스크립트에 옮겨 적지 않는다.
/// 기획이 본문을 고치면 카드 에셋만 고치면 되고, 여기는 건드릴 필요가 없다.
///
/// 표시 규칙(기획 정본):
/// - 태블릿에는 <b>수칙 본문만</b> 보여 준다. 카드 ID·판정 상세·수치는 보여 주지 않는다.
/// - 그날 덱에 들어 있는 수칙만 보여 준다(하루 6장).
/// </summary>
[DisallowMultipleComponent]
public class TabletDocument : MonoBehaviour
{
    [Header("표시할 곳")]
    [Tooltip("제목 줄. 예: 근무수칙 · 1일차")]
    public TMP_Text headerText;
    [Tooltip("수칙 본문.")]
    public TMP_Text bodyText;
    [Tooltip("쪽 번호. 예: 2 / 6")]
    public TMP_Text pageText;

    [Header("내용")]
    [Tooltip("한 쪽에 보여 줄 수칙 개수.")]
    [Min(1)] public int rulesPerPage = 1;
    [Tooltip("비워 두면 Resources에서 편성표를 찾는다.")]
    public NightDeckTableSO deckTable;
    [Tooltip("켜면 GameSession의 현재 일차를 쓴다. 끄면 아래 값을 쓴다(씬 단독 확인용).")]
    public bool useGameSessionDay = true;
    [Min(1)] public int previewDay = 1;

    [Header("넘기기")]
    [Tooltip("이 키로 다음 쪽. 마우스 휠도 함께 동작한다.")]
    public KeyCode nextKey = KeyCode.RightArrow;
    public KeyCode previousKey = KeyCode.LeftArrow;
    [Tooltip("태블릿을 들고 있을 때만 입력을 받는다. 비어 있으면 같은 오브젝트 위에서 찾는다.")]
    public PlayerTablet tablet;

    [Header("나타나는 연출")]
    [Tooltip("태블릿이 올라오는 동안 글자를 서서히 띄운다. 끄면 화면이 켜지는 순간 바로 보인다.")]
    public bool fadeTextWithOpen = true;
    [Tooltip("몇 %쯤 올라왔을 때부터 글자가 보이기 시작할지(0~1).")]
    [Range(0f, 1f)] public float fadeStartAt = 0.5f;

    private readonly List<RuleSO> _rules = new List<RuleSO>();
    private int _page;
    private int _loadedDay = -1;

    /// <summary>현재 쪽(0부터).</summary>
    public int Page { get { return _page; } }

    /// <summary>전체 쪽 수.</summary>
    public int PageCount
    {
        get
        {
            if (_rules.Count == 0) return 1;
            return Mathf.CeilToInt(_rules.Count / (float)Mathf.Max(1, rulesPerPage));
        }
    }

    private void Awake()
    {
        if (tablet == null) tablet = GetComponentInParent<PlayerTablet>();
    }

    private void OnEnable()
    {
        // 태블릿을 열 때마다 화면이 켜지므로, 그때 일차가 바뀌었으면 다시 읽는다.
        Reload();
    }

    private void Update()
    {
        FadeWithOpenAmount();

        if (tablet != null && !tablet.IsOpened) return;

        float wheel = Input.mouseScrollDelta.y;
        if (Input.GetKeyDown(nextKey) || wheel < -0.01f) NextPage();
        else if (Input.GetKeyDown(previousKey) || wheel > 0.01f) PreviousPage();
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

        SetAlpha(headerText, alpha);
        SetAlpha(bodyText, alpha);
        SetAlpha(pageText, alpha);
    }

    private static void SetAlpha(TMP_Text text, float alpha)
    {
        if (text == null) return;
        if (Mathf.Abs(text.alpha - alpha) < 0.003f) return;
        text.alpha = alpha;
    }

    /// <summary>편성표에서 그날 수칙을 다시 읽어 첫 쪽부터 보여 준다.</summary>
    public void Reload()
    {
        int day = useGameSessionDay ? GameSession.CurrentDay : previewDay;
        LoadRules(day);
        _page = 0;
        Render();
    }

    public void NextPage()
    {
        if (_page >= PageCount - 1) return;
        _page++;
        Render();
    }

    public void PreviousPage()
    {
        if (_page <= 0) return;
        _page--;
        Render();
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
        if (headerText != null)
        {
            headerText.text = "근무수칙 · " + _loadedDay + "일차";
        }

        if (bodyText != null)
        {
            if (_rules.Count == 0)
            {
                bodyText.text = "오늘 배정된 수칙이 없습니다.";
            }
            else
            {
                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                int first = _page * rulesPerPage;
                for (int i = first; i < first + rulesPerPage && i < _rules.Count; i++)
                {
                    if (sb.Length > 0) sb.Append("\n\n");
                    // 카드 ID는 제작자용이라 보여 주지 않고, 덱 순서대로 번호만 붙인다.
                    sb.Append(i + 1).Append(". ").Append(_rules[i].PlayerText);
                }
                bodyText.text = sb.ToString();
            }
        }

        if (pageText != null)
        {
            pageText.text = _rules.Count == 0 ? string.Empty : (_page + 1) + " / " + PageCount;
        }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

/// <summary>
/// 태블릿 <b>메시지 연속 수신 연출</b>. 엑셀(CSV)에 적어 둔 이벤트를 이름으로 실행하면,
/// 그 이벤트의 메시지들이 적어 둔 간격으로 하나씩 태블릿에 들어온다(들어올 때마다 알람이 울린다).
///
/// <para><b>데이터</b> — 엑셀에서 「CSV UTF-8」로 저장해 <c>Resources/TabletMessageEvents.csv</c>에 둔다.</para>
/// <code>
///   A열 이벤트 이름 | B열 간격(초) | C열 메시지 (여러 개는 | 로 구분) | D열 글씨 색 (선택)
///   E열 반복(인게임 분, 선택) | F열 확인 대기(선택) | G열 끝나면(선택)
///   첫 줄은 제목 줄이라 읽지 않는다. 이벤트 이름이 빈 줄과 #으로 시작하는 줄도 건너뛴다.
///   D열: hex 코드(#FF3030, FF3030, #FF303080). 하나만 적으면 모든 메시지에, | 로 여러 개 적으면
///        메시지 순서대로 적용하고 모자라면 마지막 색을 이어 쓴다. 빈 칸은 기본색.
///   E열: 빈 칸 · 0이면 메시지를 한 번씩만 보낸다. 값이 있으면 그 인게임 시간(분) 동안 메시지를 처음부터 다시 돌려 보낸다.
///   F열: 빈 칸이면 앞 메시지를 확인해야 다음이 온다(기존 동작). N이면 확인과 상관없이 간격마다 보낸다.
///   G열: 끝까지 간 뒤 할 일. clear = 메시지 전부 지움 · 이벤트 이름 = 그 이벤트 실행 · clear>이벤트 이름 = 둘 다.
///        중간에 멈추면(<see cref="Stop"/>) 하지 않는다.
/// </code>
/// <para>
/// 첫 메시지는 실행 즉시 보낸다. 그 뒤로는 <b>플레이어가 앞 메시지를 확인해야</b> 간격(B열)을 세기 시작하고,
/// 간격이 지나면 다음 메시지를 보낸다. 확인하지 않으면 다음 메시지는 오지 않는다(F열이 N이면 기다리지 않는다).
/// 「확인」은 알람이 꺼지는 조건과 같다 — 태블릿을 들고 메시지 탭을 보고 있거나, 그 메시지가 읽음 처리됐을 때.
/// 간격(B열)은 현실 초, 반복(E열)은 인게임 분이다. 둘 다 일시정지 중에는 멈춘다.
/// </para>
///
/// <para><b>실행</b></para>
/// <list type="bullet">
/// <item>코드: <c>TabletMessageEvents.Play("이벤트이름");</c></item>
/// <item>Timeline: Signal Receiver → <see cref="TabletMessageEventSender.Send(string)"/></item>
/// <item>개발자 모드('-') ③ 탭의 이벤트 버튼</item>
/// </list>
///
/// <para>
/// 판정 시스템의 문자(<c>EventBus.MessageSent</c>)와는 별개다. 연출 전용이며 판정에 아무 영향도 주지 않는다.
/// </para>
/// </summary>
[DisallowMultipleComponent]
public class TabletMessageEvents : MonoBehaviour
{
    /// <summary>Resources 안의 CSV 이름(확장자 없이).</summary>
    public const string ResourceName = "TabletMessageEvents";

    /// <summary>한 칸에 적은 메시지들을 나누는 기호.</summary>
    public const char MessageSeparator = '|';

    /// <summary>G열에서 「메시지 전부 지움」을 뜻하는 말.</summary>
    public const string ClearCommand = "clear";

    /// <summary>G열에서 지우기와 다음 이벤트를 잇는 기호(clear>이벤트).</summary>
    public const char ThenSeparator = '>';

    /// <summary>반복(E열) 중에는 간격이 이보다 짧을 수 없다. 0이면 한 프레임에 메시지가 쏟아진다.</summary>
    public const float MinRepeatInterval = 0.2f;

    /// <summary>CSV 한 줄 = 이벤트 하나.</summary>
    public sealed class EventData
    {
        public string Name;
        public float Interval;
        public string[] Messages;
        /// <summary>메시지별 글씨 색(#RRGGBBAA). Messages와 길이가 같고, 빈 문자열은 기본색.</summary>
        public string[] Colors;
        /// <summary>반복할 인게임 시간(분). 0이면 한 번씩만 보낸다.</summary>
        public float RepeatMinutes;
        /// <summary>앞 메시지를 확인해야 다음을 보내는가.</summary>
        public bool WaitForRead = true;
        /// <summary>끝까지 간 뒤 메시지를 전부 지우는가.</summary>
        public bool ClearOnEnd;
        /// <summary>끝까지 간 뒤 이어서 실행할 이벤트. 없으면 빈 문자열.</summary>
        public string Next = string.Empty;
    }

    /// <summary>
    /// 이벤트가 <b>끝까지</b> 갔을 때(G열 처리 직전) 이벤트 이름과 함께 발생한다. <see cref="Stop"/>으로 멈춘 경우는 오지 않는다.
    /// </summary>
    public static event Action<string> Finished;

    private static Dictionary<string, EventData> table;
    private static List<string> order;

    private readonly Dictionary<string, Coroutine> running = new Dictionary<string, Coroutine>();
    private TabletMessageList list;
    private PlayerTablet tablet;
    private TabletDocument document;
    private int sendCounter;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatic()
    {
        // 에디터에서 CSV를 고치고 다시 플레이하면 새로 읽도록 비운다.
        table = null;
        order = null;
        Finished = null;
    }

    // ─────────────────────────────── 실행 ───────────────────────────────

    /// <summary>이벤트를 실행한다. 태블릿이 없는 씬이거나 이름이 없으면 경고만 남긴다.</summary>
    public static bool Play(string eventName)
    {
        TabletMessageEvents player = Find();
        return player != null && player.PlayEvent(eventName);
    }

    /// <summary>진행 중인 이벤트를 멈춘다. 이미 보낸 메시지는 그대로 남는다.</summary>
    public static void Stop(string eventName)
    {
        TabletMessageEvents player = Find();
        if (player != null) player.StopEvent(eventName);
    }

    /// <summary>진행 중인 이벤트를 모두 멈춘다.</summary>
    public static void StopAll()
    {
        TabletMessageEvents player = Find();
        if (player != null) player.StopAllEvents();
    }

    /// <summary>지금 진행 중인가.</summary>
    public static bool IsPlaying(string eventName)
    {
        TabletMessageEvents player = Find(false);
        return player != null && player.running.ContainsKey(Key(eventName));
    }

    /// <summary>CSV에 적힌 이벤트 이름들(적힌 순서).</summary>
    public static IReadOnlyList<string> EventNames
    {
        get { Load(); return order; }
    }

    /// <summary>이름으로 이벤트 데이터를 찾는다. 없으면 null.</summary>
    public static EventData Get(string eventName)
    {
        Load();
        table.TryGetValue(Key(eventName), out EventData data);
        return data;
    }

    /// <summary>CSV를 다시 읽는다. 플레이 중에 CSV를 고쳤을 때 쓴다.</summary>
    public static void Reload()
    {
        table = null;
        order = null;
        Load();
    }

    /// <summary>
    /// 태블릿 메시지 목록이 있는 오브젝트에 붙은 재생기를 찾는다. 없으면 그 자리에 붙인다
    /// (씬 · 프리팹을 고치지 않아도 되게, 런타임에만 붙는다).
    /// </summary>
    private static TabletMessageEvents Find(bool create = true)
    {
        TabletMessageList target = FindAnyObjectByType<TabletMessageList>(FindObjectsInactive.Include);
        if (target == null)
        {
            if (create) Debug.LogWarning("[TabletMessageEvents] 이 씬에는 태블릿(TabletMessageList)이 없습니다.");
            return null;
        }

        TabletMessageEvents player = target.GetComponent<TabletMessageEvents>();
        if (player == null && create) player = target.gameObject.AddComponent<TabletMessageEvents>();
        return player;
    }

    // ─────────────────────────────── 인스턴스 ───────────────────────────────

    private void Awake()
    {
        list = GetComponent<TabletMessageList>();
        tablet = GetComponentInParent<PlayerTablet>();
        document = GetComponentInChildren<TabletDocument>(true);
    }

    private void OnDisable()
    {
        // 태블릿 오브젝트가 꺼지면 코루틴도 끊긴다. 기록을 맞춰 둔다.
        running.Clear();
    }

    /// <summary>이벤트를 실행한다. 같은 이벤트가 이미 진행 중이면 무시한다(서로 다른 이벤트는 동시에 돈다).</summary>
    public bool PlayEvent(string eventName)
    {
        EventData data = Get(eventName);
        if (data == null)
        {
            Debug.LogWarning($"[TabletMessageEvents] '{eventName}' 이벤트가 CSV에 없습니다.");
            return false;
        }
        if (data.Messages.Length == 0)
        {
            Debug.LogWarning($"[TabletMessageEvents] '{eventName}' 이벤트에 메시지가 없습니다.");
            return false;
        }
        if (list == null)
        {
            Debug.LogWarning("[TabletMessageEvents] 메시지 목록(TabletMessageList)을 찾지 못했습니다.");
            return false;
        }
        if (!isActiveAndEnabled)
        {
            Debug.LogWarning("[TabletMessageEvents] 태블릿이 꺼져 있어 실행할 수 없습니다.");
            return false;
        }

        string key = Key(eventName);
        if (running.ContainsKey(key)) return false;

        running[key] = StartCoroutine(Run(key, data));
        return true;
    }

    public void StopEvent(string eventName)
    {
        string key = Key(eventName);
        if (!running.TryGetValue(key, out Coroutine routine)) return;

        if (routine != null) StopCoroutine(routine);
        running.Remove(key);
    }

    public void StopAllEvents()
    {
        foreach (Coroutine routine in running.Values)
        {
            if (routine != null) StopCoroutine(routine);
        }
        running.Clear();
    }

    private IEnumerator Run(string key, EventData data)
    {
        bool repeat = data.RepeatMinutes > 0f;
        float elapsedMinutes = 0f;   // 반복(E열)은 첫 메시지를 보낸 순간부터 센다
        string lastId = null;

        for (int i = 0; repeat || i < data.Messages.Length; i++)
        {
            if (lastId != null)
            {
                // 앞 메시지를 확인할 때까지 기다린 뒤(F열), 거기서부터 간격을 센다.
                // 기다리는 동안에도 반복 시간은 흐르므로, 매 프레임 끝났는지 본다.
                while (data.WaitForRead && !IsRead(lastId))
                {
                    yield return null;
                    elapsedMinutes += GameMinutes.Delta;
                    if (repeat && elapsedMinutes >= data.RepeatMinutes) break;
                }

                // deltaTime은 게임 시간이라 일시정지 중에는 멈춘다.
                float waited = 0f;
                while (waited < data.Interval && !(repeat && elapsedMinutes >= data.RepeatMinutes))
                {
                    yield return null;
                    waited += Time.deltaTime;
                    elapsedMinutes += GameMinutes.Delta;
                }

                if (repeat && elapsedMinutes >= data.RepeatMinutes) break;
            }

            // 같은 id면 알람이 울리지 않으므로 보낼 때마다 새 id를 만든다.
            int index = i % data.Messages.Length;
            sendCounter++;
            lastId = "event." + data.Name + "." + sendCounter;
            list.Add(lastId, data.Messages[index], data.Colors[index]);
        }

        running.Remove(key);

        // 받는 쪽이 터져도 G열(지우기 · 다음 이벤트)은 그대로 한다.
        try { Finished?.Invoke(data.Name); }
        catch (Exception e) { Debug.LogException(e); }

        if (data.ClearOnEnd) list.Clear();
        if (data.Next.Length > 0) PlayEvent(data.Next);
    }

    /// <summary>
    /// 플레이어가 그 메시지를 확인했는가. 알람이 꺼지는 조건(<see cref="TabletAlarm"/>)과 같게 본다 —
    /// 태블릿을 들고 메시지 탭을 보고 있으면 확인한 것이다. 이미 메시지 탭을 보는 중에 들어온 메시지는
    /// 읽음 표시가 안 붙으므로(탭을 펼칠 때만 지워진다) 읽음 표시만으로는 판단하지 않는다.
    /// 메시지가 목록에서 지워졌으면(목록 비우기 등) 기다리지 않는다.
    /// </summary>
    private bool IsRead(string id)
    {
        TabletMessageList.Message message = null;
        IReadOnlyList<TabletMessageList.Message> messages = list.Messages;
        for (int i = 0; i < messages.Count; i++)
        {
            if (messages[i].id == id) { message = messages[i]; break; }
        }

        if (message == null || !message.unread) return true;

        return tablet != null && tablet.IsOpened
               && document != null && document.CurrentTab == TabletDocument.Tab.Messages;
    }

    // ─────────────────────────────── CSV ───────────────────────────────

    private static string Key(string eventName)
    {
        return (eventName ?? string.Empty).Trim();
    }

    private static void Load()
    {
        if (table != null) return;

        table = new Dictionary<string, EventData>();
        order = new List<string>();

        TextAsset csv = Resources.Load<TextAsset>(ResourceName);
        if (csv == null)
        {
            Debug.LogWarning($"[TabletMessageEvents] Resources/{ResourceName}.csv 를 찾지 못했습니다.");
            return;
        }

        List<List<string>> rows = ParseCsv(csv.text);

        // 첫 줄은 제목 줄
        for (int r = 1; r < rows.Count; r++)
        {
            List<string> row = rows[r];
            string name = Cell(row, 0);
            if (name.Length == 0 || name.StartsWith("#")) continue;

            if (!float.TryParse(Cell(row, 1), NumberStyles.Float, CultureInfo.InvariantCulture, out float interval))
            {
                Debug.LogWarning($"[TabletMessageEvents] {r + 1}행 '{name}': 간격 '{Cell(row, 1)}'을(를) 숫자로 읽지 못해 1초로 씁니다.");
                interval = 1f;
            }

            var messages = new List<string>();
            foreach (string part in Cell(row, 2).Split(MessageSeparator))
            {
                string text = part.Trim();
                if (text.Length > 0) messages.Add(text);
            }

            if (table.ContainsKey(name))
            {
                Debug.LogWarning($"[TabletMessageEvents] {r + 1}행 '{name}': 같은 이름이 이미 있어 이 줄은 건너뜁니다.");
                continue;
            }

            var data = new EventData
            {
                Name = name,
                Interval = Mathf.Max(0f, interval),
                Messages = messages.ToArray(),
                Colors = ParseColors(Cell(row, 3), messages.Count, r + 1, name),
                RepeatMinutes = ParseRepeat(Cell(row, 4), r + 1, name),
                WaitForRead = ParseWaitForRead(Cell(row, 5))
            };
            ParseThen(Cell(row, 6), data, r + 1);

            if (data.RepeatMinutes > 0f && data.Interval < MinRepeatInterval)
            {
                Debug.LogWarning($"[TabletMessageEvents] {r + 1}행 '{name}': 반복 중에는 간격이 {MinRepeatInterval}초보다 짧을 수 없어 {MinRepeatInterval}초로 씁니다.");
                data.Interval = MinRepeatInterval;
            }

            table[name] = data;
            order.Add(name);
        }
    }

    /// <summary>E열 반복 시간(인게임 분). 빈 칸은 0(반복 안 함).</summary>
    private static float ParseRepeat(string cell, int line, string name)
    {
        if (cell.Length == 0) return 0f;
        if (float.TryParse(cell, NumberStyles.Float, CultureInfo.InvariantCulture, out float minutes)) return Mathf.Max(0f, minutes);

        Debug.LogWarning($"[TabletMessageEvents] {line}행 '{name}': 반복 '{cell}'을(를) 숫자로 읽지 못해 반복하지 않습니다.");
        return 0f;
    }

    /// <summary>F열 확인 대기. N · 아니오 · 0 · false면 기다리지 않는다. 빈 칸은 기다린다(기존 동작).</summary>
    private static bool ParseWaitForRead(string cell)
    {
        switch (cell.ToLowerInvariant())
        {
            case "n":
            case "no":
            case "false":
            case "0":
            case "아니오":
            case "x":
                return false;
            default:
                return true;
        }
    }

    /// <summary>G열 「끝나면」. clear · 이벤트 이름 · clear>이벤트 이름.</summary>
    private static void ParseThen(string cell, EventData data, int line)
    {
        if (cell.Length == 0) return;

        foreach (string part in cell.Split(ThenSeparator))
        {
            string word = part.Trim();
            if (word.Length == 0) continue;

            if (string.Equals(word, ClearCommand, StringComparison.OrdinalIgnoreCase)) data.ClearOnEnd = true;
            else if (data.Next.Length == 0) data.Next = word;
            else Debug.LogWarning($"[TabletMessageEvents] {line}행 '{data.Name}': 다음 이벤트는 하나만 적을 수 있어 '{word}'은(는) 무시합니다.");
        }

        if (string.Equals(data.Next, data.Name, StringComparison.Ordinal))
        {
            Debug.LogWarning($"[TabletMessageEvents] {line}행 '{data.Name}': 자기 자신을 다음 이벤트로 적으면 끝나지 않아 무시합니다. 계속 돌리려면 E열 반복을 쓰십시오.");
            data.Next = string.Empty;
        }
    }

    /// <summary>
    /// D열의 글씨 색을 메시지 수만큼 펼친다. 하나면 전부에, 여러 개면 순서대로, 모자라면 마지막 색을 이어 쓴다.
    /// 읽지 못한 값은 경고를 남기고 기본색(빈 문자열)으로 둔다.
    /// </summary>
    private static string[] ParseColors(string cell, int count, int line, string name)
    {
        var result = new string[count];
        for (int i = 0; i < count; i++) result[i] = string.Empty;
        if (cell.Length == 0 || count == 0) return result;

        string[] parts = cell.Split(MessageSeparator);
        string last = string.Empty;
        for (int i = 0; i < count; i++)
        {
            if (i < parts.Length) last = NormalizeColor(parts[i].Trim(), line, name);
            result[i] = last;
        }
        return result;
    }

    /// <summary>「FF3030」「#ff3030」「#FF303080」을 TMP가 읽는 「#FF3030FF」 꼴로 맞춘다. 빈 칸은 기본색.</summary>
    private static string NormalizeColor(string value, int line, string name)
    {
        if (value.Length == 0) return string.Empty;

        string hex = value.StartsWith("#") ? value : "#" + value;
        if (ColorUtility.TryParseHtmlString(hex, out Color color))
        {
            return "#" + ColorUtility.ToHtmlStringRGBA(color);
        }

        Debug.LogWarning($"[TabletMessageEvents] {line}행 '{name}': 글씨 색 '{value}'을(를) 읽지 못해 기본색으로 씁니다. #RRGGBB 형식으로 적어 주십시오.");
        return string.Empty;
    }

    private static string Cell(List<string> row, int index)
    {
        return index < row.Count ? row[index].Trim() : string.Empty;
    }

    /// <summary>
    /// 엑셀이 저장한 CSV를 읽는다. 쉼표·줄바꿈이 들어간 칸은 큰따옴표로 감싸이고,
    /// 칸 안의 큰따옴표는 두 번("") 적힌다. 파일 맨 앞의 BOM은 버린다.
    /// </summary>
    private static List<List<string>> ParseCsv(string text)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var cell = new StringBuilder();
        bool quoted = false;

        int start = text.Length > 0 && text[0] == '﻿' ? 1 : 0;
        for (int i = start; i < text.Length; i++)
        {
            char c = text[i];

            if (quoted)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i++; }
                    else quoted = false;
                }
                else cell.Append(c);
                continue;
            }

            switch (c)
            {
                case '"': quoted = true; break;
                case ',': row.Add(cell.ToString()); cell.Length = 0; break;
                case '\r': break;
                case '\n':
                    row.Add(cell.ToString()); cell.Length = 0;
                    rows.Add(row); row = new List<string>();
                    break;
                default: cell.Append(c); break;
            }
        }

        if (cell.Length > 0 || row.Count > 0)
        {
            row.Add(cell.ToString());
            rows.Add(row);
        }
        return rows;
    }
}

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
///   A열 이벤트 이름 | B열 간격(초) | C열 메시지 (여러 개는 | 로 구분)
///   첫 줄은 제목 줄이라 읽지 않는다. 이벤트 이름이 빈 줄과 #으로 시작하는 줄도 건너뛴다.
/// </code>
/// <para>
/// 첫 메시지는 실행 즉시 보낸다. 그 뒤로는 <b>플레이어가 앞 메시지를 확인해야</b> 간격(B열)을 세기 시작하고,
/// 간격이 지나면 다음 메시지를 보낸다. 확인하지 않으면 다음 메시지는 오지 않는다.
/// 「확인」은 알람이 꺼지는 조건과 같다 — 태블릿을 들고 메시지 탭을 보고 있거나, 그 메시지가 읽음 처리됐을 때.
/// 간격은 게임 시간이라 일시정지 중에는 멈춘다.
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

    /// <summary>CSV 한 줄 = 이벤트 하나.</summary>
    public sealed class EventData
    {
        public string Name;
        public float Interval;
        public string[] Messages;
    }

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
        string lastId = null;

        for (int i = 0; i < data.Messages.Length; i++)
        {
            if (lastId != null)
            {
                // 앞 메시지를 확인할 때까지 기다린 뒤, 거기서부터 간격을 센다.
                // WaitForSeconds는 게임 시간이라 일시정지 중에는 멈춘다.
                while (!IsRead(lastId)) yield return null;
                if (data.Interval > 0f) yield return new WaitForSeconds(data.Interval);
            }

            // 같은 id면 알람이 울리지 않으므로 보낼 때마다 새 id를 만든다.
            sendCounter++;
            lastId = "event." + data.Name + "." + sendCounter;
            list.Add(lastId, data.Messages[i]);
        }

        running.Remove(key);
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

            table[name] = new EventData { Name = name, Interval = Mathf.Max(0f, interval), Messages = messages.ToArray() };
            order.Add(name);
        }
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

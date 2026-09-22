using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 태블릿의 <b>수신 메시지</b> 목록.
///
/// 지금은 인스펙터에 넣은 임시 항목을 보여 주지만, 실제로는 다른 시스템이
/// 아래 API로 넣는 것을 전제로 만들었다.
///   messages.Add("patrol.corridor", "복도를 점검하십시오");   // 수신 시각은 게임 시계에서 자동
///
/// <b>수칙(RuleSO)과는 다른 것이다.</b> 수칙은 지켜야 할 규칙이고, 여기는 지시가 담긴 문자다.
/// 수칙 본문을 여기에 옮겨 적지 않는다.
///
/// 표시 규칙(기획):
/// - 발신자는 적지 않는다. <b>수신 시각 + 지시 본문</b>만 보여 준다.
/// - <b>최신순</b>으로 보여 준다(정렬은 화면을 그리는 TabletDocument가 한다. 목록 자체는 도착 순서를 지킨다).
/// - 안 읽은 메시지가 있으면 탭 옆에 점이 붙고, 메시지 탭을 열면 사라진다.
/// </summary>
[DisallowMultipleComponent]
public class TabletMessageList : MonoBehaviour
{
    [Serializable]
    public class Message
    {
        [Tooltip("코드에서 찾을 때 쓰는 이름. 표시되지 않는다.")]
        public string id = string.Empty;
        [Tooltip("화면에 보이는 지시 본문.")]
        [TextArea(1, 3)] public string text = string.Empty;
        [Tooltip("수신 시각(자정 기준 누적 분). -1이면 시각을 적지 않는다.")]
        public int receivedMinutes = -1;
        [Tooltip("아직 읽지 않았는가. 메시지 탭을 열면 모두 읽음이 된다.")]
        public bool unread = true;
    }

    [Header("임시 항목")]
    [Tooltip("실제 메시지가 정해지기 전까지 모양을 확인하려고 넣어 둔 것. 기획이 정해지면 코드에서 넣는다.")]
    [FormerlySerializedAs("tasks")]
    public List<Message> messages = new List<Message>();

    /// <summary>목록이 바뀌면 알린다. 화면이 이 신호를 받아 다시 그린다.</summary>
    public event Action Changed;

    /// <summary>새 메시지가 들어왔을 때만 알린다. 알람이 이 신호를 듣고 울린다.</summary>
    public event Action<Message> MessageReceived;

    public IReadOnlyList<Message> Messages { get { return messages; } }

    /// <summary>전체 메시지 수.</summary>
    public int Count { get { return messages.Count; } }

    /// <summary>아직 읽지 않은 메시지 수.</summary>
    public int UnreadCount
    {
        get
        {
            int n = 0;
            for (int i = 0; i < messages.Count; i++) if (messages[i].unread) n++;
            return n;
        }
    }

    /// <summary>탭에 점(뱃지)을 붙여야 하는가.</summary>
    public bool HasUnread { get { return UnreadCount > 0; } }

    /// <summary>메시지를 받는다. 수신 시각은 게임 시계에서 읽는다.</summary>
    public void Add(string id, string text)
    {
        Add(id, text, CurrentGameMinutes());
    }

    /// <summary>메시지를 받는다. 같은 id가 이미 있으면 본문만 고치고 알람은 울리지 않는다.</summary>
    public void Add(string id, string text, int receivedMinutes)
    {
        Message found = Find(id);
        if (found != null)
        {
            found.text = text;
            Raise();
            return;
        }

        Message message = new Message
        {
            id = id,
            text = text,
            receivedMinutes = receivedMinutes,
            unread = true
        };
        messages.Add(message);

        Raise();
        if (MessageReceived != null) MessageReceived(message);
    }

    /// <summary>메시지 탭을 열었을 때 부른다. 안 읽음 표시를 모두 지운다.</summary>
    public void MarkAllRead()
    {
        bool changed = false;
        for (int i = 0; i < messages.Count; i++)
        {
            if (!messages[i].unread) continue;
            messages[i].unread = false;
            changed = true;
        }

        if (changed) Raise();
    }

    /// <summary>목록에서 아예 지운다.</summary>
    public void Remove(string id)
    {
        Message found = Find(id);
        if (found == null) return;

        messages.Remove(found);
        Raise();
    }

    /// <summary>하루가 끝나거나 새 날이 시작될 때 비운다.</summary>
    public void Clear()
    {
        if (messages.Count == 0) return;

        messages.Clear();
        Raise();
    }

    private Message Find(string id)
    {
        for (int i = 0; i < messages.Count; i++)
        {
            if (messages[i].id == id) return messages[i];
        }
        return null;
    }

    /// <summary>
    /// 지금 게임 시각(자정 기준 누적 분). 시계가 없으면 -1이라 시각 없이 본문만 나온다.
    /// 씬마다 GameTime이 하나라 찾아서 들고 있는다.
    /// </summary>
    private int CurrentGameMinutes()
    {
        if (_gameTime == null) _gameTime = UnityEngine.Object.FindAnyObjectByType<GameTime>();
        return _gameTime != null ? _gameTime.CurrentMinutes : -1;
    }

    private GameTime _gameTime;

    private void Raise()
    {
        if (Changed != null) Changed();
    }
}

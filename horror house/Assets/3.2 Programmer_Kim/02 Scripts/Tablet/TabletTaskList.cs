using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 태블릿의 <b>수행 지시</b> 목록. 오늘 해야 할 일을 담는다.
///
/// 지금은 인스펙터에 넣은 임시 항목을 보여 주지만, 실제로는 다른 시스템이
/// 아래 API로 넣고 빼는 것을 전제로 만들었다. 나중에 알람 기능이 생기면 그쪽에서 부르면 된다.
///   Add("patrol.corridor", "복도를 점검하십시오");
///   Complete("patrol.corridor");
///
/// <b>수칙(RuleSO)과는 다른 것이다.</b> 수칙은 지켜야 할 규칙이고, 여기는 해야 할 일이다.
/// 수칙 본문을 여기에 옮겨 적지 않는다.
/// </summary>
[DisallowMultipleComponent]
public class TabletTaskList : MonoBehaviour
{
    [Serializable]
    public class Task
    {
        [Tooltip("코드에서 찾을 때 쓰는 이름. 표시되지 않는다.")]
        public string id = string.Empty;
        [Tooltip("화면에 보이는 문장.")]
        public string text = string.Empty;
        public bool done;
    }

    [Header("임시 항목")]
    [Tooltip("실제 지시가 정해지기 전까지 모양을 확인하려고 넣어 둔 것. 기획이 정해지면 코드에서 넣는다.")]
    public List<Task> tasks = new List<Task>();

    [Header("표시")]
    [Tooltip("아직 안 한 일 앞에 붙는 기호.")]
    public string pendingMark = "□";
    [Tooltip("끝낸 일 앞에 붙는 기호.")]
    public string doneMark = "■";
    [Tooltip("끝낸 일을 흐리게 표시한다.")]
    public bool dimDone = true;
    [Tooltip("흐리게 표시할 때 쓰는 색(16진수).")]
    public string doneColor = "#5A6B72";

    /// <summary>목록이 바뀌면 알린다. 화면이 이 신호를 받아 다시 그린다.</summary>
    public event Action Changed;

    /// <summary>새 지시가 들어왔을 때만 알린다. 알람이 이 신호를 듣고 울린다.</summary>
    public event Action<Task> TaskAdded;

    public IReadOnlyList<Task> Tasks { get { return tasks; } }

    /// <summary>아직 끝내지 않은 일의 수.</summary>
    public int PendingCount
    {
        get
        {
            int n = 0;
            for (int i = 0; i < tasks.Count; i++) if (!tasks[i].done) n++;
            return n;
        }
    }

    /// <summary>할 일을 추가한다. 같은 id가 이미 있으면 문장만 바꾼다.</summary>
    public void Add(string id, string text)
    {
        Task found = Find(id);
        if (found != null)
        {
            // 이미 있는 지시는 문구만 고친다. 알람은 울리지 않는다.
            found.text = text;
            Raise();
            return;
        }

        Task task = new Task { id = id, text = text, done = false };
        tasks.Add(task);

        Raise();
        if (TaskAdded != null) TaskAdded(task);
    }

    /// <summary>끝낸 것으로 표시한다.</summary>
    public void Complete(string id)
    {
        Task found = Find(id);
        if (found == null || found.done) return;

        found.done = true;
        Raise();
    }

    /// <summary>목록에서 아예 지운다.</summary>
    public void Remove(string id)
    {
        Task found = Find(id);
        if (found == null) return;

        tasks.Remove(found);
        Raise();
    }

    /// <summary>하루가 끝나거나 새 날이 시작될 때 비운다.</summary>
    public void Clear()
    {
        if (tasks.Count == 0) return;

        tasks.Clear();
        Raise();
    }

    /// <summary>화면에 그릴 문장을 만든다.</summary>
    public string BuildText()
    {
        if (tasks.Count == 0) return "지시받은 일이 없습니다.";

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        for (int i = 0; i < tasks.Count; i++)
        {
            Task task = tasks[i];
            if (sb.Length > 0) sb.Append('\n');

            bool dim = task.done && dimDone;
            if (dim) sb.Append("<color=").Append(doneColor).Append('>');

            sb.Append(task.done ? doneMark : pendingMark).Append(' ').Append(task.text);

            if (dim) sb.Append("</color>");
        }

        return sb.ToString();
    }

    private Task Find(string id)
    {
        for (int i = 0; i < tasks.Count; i++)
        {
            if (tasks[i].id == id) return tasks[i];
        }
        return null;
    }

    private void Raise()
    {
        if (Changed != null) Changed();
    }
}

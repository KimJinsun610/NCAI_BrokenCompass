using UnityEngine;

/// <summary>
/// 인스펙터 · Timeline에서 <see cref="TabletMessageEvents"/>를 실행하는 연결부.
/// <para>
/// Timeline에서 쓰려면: Signal Track에 Signal을 놓고, Director 오브젝트의 <b>Signal Receiver</b>에서
/// 이 컴포넌트의 <see cref="Send(string)"/>을 고른 뒤 이벤트 이름을 적는다.
/// 이름 칸을 비워 두고 <see cref="Send()"/>를 고르면 아래 <see cref="eventName"/>을 쓴다.
/// </para>
/// </summary>
public class TabletMessageEventSender : MonoBehaviour
{
    [Tooltip("Send()로 실행할 이벤트 이름. CSV의 A열과 글자까지 같아야 한다.")]
    [SerializeField] private string eventName = string.Empty;

    /// <summary>인스펙터에 적은 이벤트를 실행한다.</summary>
    public void Send()
    {
        Send(eventName);
    }

    /// <summary>이름을 받아 실행한다. Signal Receiver에서 이름을 직접 적을 때 쓴다.</summary>
    public void Send(string name)
    {
        TabletMessageEvents.Play(name);
    }

    /// <summary>인스펙터에 적은 이벤트를 멈춘다.</summary>
    public void Stop()
    {
        TabletMessageEvents.Stop(eventName);
    }
}

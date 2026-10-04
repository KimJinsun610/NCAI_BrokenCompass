using UnityEngine;

/// <summary>
/// 피날레 몹 Animator의 애니메이션 이벤트 받이(<see cref="FinaleMob"/>이 런타임에 붙인다 — 프리팹에 넣지 않아도 된다).
/// 클립에 적을 함수 이름: <c>FinaleBeatDone</c>(한 번짜리 비트 끝) · <c>FinaleKnock</c>(손이 유리에 닿음) · <c>FinaleCue</c>(문자열, 그 밖의 타이밍).
/// </summary>
[DisallowMultipleComponent]
public sealed class FinaleAnimEvents : MonoBehaviour
{
    /// <summary>한 번짜리 비트 끝 이벤트 이름.</summary>
    public const string BeatDoneEvent = "FinaleBeatDone";

    /// <summary>두드림 이벤트 이름.</summary>
    public const string KnockEvent = "FinaleKnock";

    /// <summary>그 밖의 타이밍 이벤트 이름.</summary>
    public const string CueEvent = "FinaleCue";

    /// <summary>이벤트를 넘길 몹.</summary>
    public FinaleMob Owner { get; set; }

    /// <summary>애니메이션 이벤트: 한 번짜리 비트가 끝났다.</summary>
    public void FinaleBeatDone()
    {
        if (Owner != null) Owner.OnBeatDoneEvent();
    }

    /// <summary>애니메이션 이벤트: 두드렸다(소리는 몹이 낸다).</summary>
    public void FinaleKnock()
    {
        if (Owner != null) Owner.OnKnockEvent();
    }

    /// <summary>애니메이션 이벤트: 이름 붙은 타이밍(예: 「smile」).</summary>
    public void FinaleCue(string cue)
    {
        if (Owner != null) Owner.OnCueEvent(cue);
    }
}

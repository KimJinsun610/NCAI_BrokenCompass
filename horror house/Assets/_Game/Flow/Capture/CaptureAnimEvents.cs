using UnityEngine;

/// <summary>
/// 붙잡힘 연출 프리팹의 애니메이션 이벤트 받이(<see cref="CaptureDirector"/>가 런타임에 붙인다 — 프리팹에 넣지 않아도 된다).
/// 클립에 적을 함수 이름: <c>CaptureDone</c>(연출 끝 — 이 프레임에 암전) · <c>CaptureSound</c>(문자열: 소리 이름) ·
/// <c>CaptureFlashlight</c>(정수: 1 켬, 0 끔) · <c>CaptureCue</c>(문자열: 그 밖의 타이밍).
/// </summary>
[DisallowMultipleComponent]
public sealed class CaptureAnimEvents : MonoBehaviour
{
    /// <summary>연출 끝 이벤트 이름.</summary>
    public const string DoneEvent = "CaptureDone";

    /// <summary>소리 이벤트 이름.</summary>
    public const string SoundEvent = "CaptureSound";

    /// <summary>손전등 이벤트 이름.</summary>
    public const string FlashlightEvent = "CaptureFlashlight";

    /// <summary>그 밖의 타이밍 이벤트 이름.</summary>
    public const string CueEvent = "CaptureCue";

    /// <summary>이벤트를 넘길 연출기.</summary>
    public CaptureDirector Owner { get; set; }

    /// <summary>애니메이션 이벤트: 연출 끝.</summary>
    public void CaptureDone()
    {
        if (Owner != null) Owner.OnSceneDone();
    }

    /// <summary>애니메이션 이벤트: 소리(이름 = Resources/Direction 또는 연출 소리 표). 몹 자리에서 3D로, 정적 중에도 들린다.</summary>
    public void CaptureSound(string soundName)
    {
        if (Owner != null) Owner.OnSceneSound(soundName, transform.position);
    }

    /// <summary>애니메이션 이벤트: 손전등(1 켬, 0 끔).</summary>
    public void CaptureFlashlight(int on)
    {
        if (Owner != null) Owner.OnSceneFlashlight(on != 0);
    }

    /// <summary>애니메이션 이벤트: 이름 붙은 타이밍.</summary>
    public void CaptureCue(string cue)
    {
        if (Owner != null) Owner.OnSceneCue(cue);
    }
}

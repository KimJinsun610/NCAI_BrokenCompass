using UnityEngine;

/// <summary>
/// 손·태블릿 연출이 공통으로 쓰는 시간.
///
/// 왜 따로 두는가:
/// - <see cref="Time.deltaTime"/>을 쓰면 시간이 느려지는 연출에서 손까지 같이 느려진다.
/// - <see cref="Time.unscaledDeltaTime"/>만 쓰면 <b>Esc로 일시정지해도 손이 계속 움직인다.</b>
///
/// 그래서 "일시정지 중인가"를 따로 판단하고, 멈춰 있는 동안에는 0을 돌려준다.
///
/// 나중에 Tab(태블릿)이 게임 시간을 멈추게 되면(기획 Q6), Tab으로 멈춘 것은
/// 일시정지가 아니므로 <see cref="ignoreTimeScale"/>을 잠깐 켜서 구분하면 된다.
/// </summary>
public static class ViewmodelTime
{
    /// <summary>켜 두면 Time.timeScale이 0이어도 연출이 계속 움직인다(Tab으로 시간을 멈출 때 사용).</summary>
    public static bool ignoreTimeScale;

    /// <summary>다른 곳에서 강제로 멈추고 싶을 때 켠다.</summary>
    public static bool forcePaused;

    /// <summary>지금 연출이 멈춰 있어야 하는가.</summary>
    public static bool Paused
    {
        get
        {
            if (forcePaused) return true;
            return !ignoreTimeScale && Time.timeScale <= 0.0001f;
        }
    }

    /// <summary>이번 프레임에 흘려도 되는 시간. 일시정지 중에는 0이다.</summary>
    public static float Delta
    {
        get { return Paused ? 0f : Time.unscaledDeltaTime; }
    }
}

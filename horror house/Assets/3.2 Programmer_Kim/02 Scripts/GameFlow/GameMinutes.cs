using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 이번 프레임에 흐른 <b>인게임 시간(분)</b>. 「인게임 N분 동안」을 세는 연출이 쓴다.
/// <para>
/// <see cref="GameTime"/>과 같은 규칙으로 흐른다 — 일시정지(timeScale 0) 중이거나, DayIntro 같은 연출이
/// 시계를 멈춰 두었거나, 근무가 끝났으면 0이다. <see cref="GameTime.CurrentMinutes"/>는 분 단위로 잘려 있어
/// 짧은 구간을 세면 최대 1분이 어긋나므로, 매 프레임 흐른 양을 더해 쓴다.
/// </para>
/// <para>씬에 GameTime이 없으면(시험 씬) 기본 배속 <see cref="FallbackMultiplier"/>로 흐른다고 본다.</para>
/// </summary>
public static class GameMinutes
{
    /// <summary>GameTime이 없을 때 쓰는 배속. GameTime의 기본값과 같다.</summary>
    public const float FallbackMultiplier = 30f;

    private static GameTime clock;
    private static bool searched;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatic()
    {
        clock = null;
        searched = false;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    // 씬이 바뀌면 시계도 바뀐다. 없는 씬에서 매 프레임 찾지 않도록, 찾기는 씬마다 한 번만 한다.
    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        clock = null;
        searched = false;
    }

    /// <summary>이번 프레임에 흐른 인게임 분. 같은 프레임에 여러 번 불러도 같은 값이다.</summary>
    public static float Delta
    {
        get
        {
            if (!searched)
            {
                clock = Object.FindAnyObjectByType<GameTime>();
                searched = true;
            }

            if (clock == null) return Time.deltaTime * FallbackMultiplier / 60f;
            if (!clock.IsRunning || clock.IsEnded) return 0f;
            return Time.deltaTime * clock.TimeMultiplier / 60f;
        }
    }
}

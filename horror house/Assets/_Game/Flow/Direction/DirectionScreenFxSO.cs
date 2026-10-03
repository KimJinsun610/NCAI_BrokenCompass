using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 연출 화면 효과가 쓰는 김진선님의 공포 화면 톤 볼륨 프로필 참조(<c>3.2 Programmer_Kim/04 Materials/HorrorVolume/</c>).
/// <see cref="DirectionStage"/>는 씬 오브젝트가 아니라 스스로 생기므로 참조를 이 에셋(<c>Resources/DirectionScreenFx.asset</c>)으로 받는다.
/// 채우는 것은 메뉴 「야간근무/연출/몹 대역·소리 연결」.
/// </summary>
[CreateAssetMenu(menuName = "NightDuty/Direction Screen Fx", fileName = "DirectionScreenFx")]
public sealed class DirectionScreenFxSO : ScriptableObject
{
    /// <summary>Resources 경로.</summary>
    public const string ResourcePath = "DirectionScreenFx";

    [Tooltip("숨막힘 — 비네팅·채도 빠짐·그레인. 모형이 나타날 때.")]
    public VolumeProfile Suffocate;

    [Tooltip("순간 암전 — 노출 -3. 0.1초 두 번(정전·캐비닛 쾅).")]
    public VolumeProfile Blackout;

    [Tooltip("이질감 — 청록 그림자·색수차. 노란 남자.")]
    public VolumeProfile Wrongness;

    [Tooltip("서서히 잠식 — 아직 쓰는 연출 없음.")]
    public VolumeProfile Creep;

    private static DirectionScreenFxSO s_loaded;
    private static bool s_looked;

    /// <summary>에셋. 없으면 null(화면 효과 없이 돈다).</summary>
    public static DirectionScreenFxSO Load()
    {
        if (!s_looked)
        {
            s_looked = true;
            s_loaded = Resources.Load<DirectionScreenFxSO>(ResourcePath);
        }

        return s_loaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        s_loaded = null;
        s_looked = false;
    }
}

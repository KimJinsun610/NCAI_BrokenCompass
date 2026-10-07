using UnityEngine;

/// <summary>
/// 청각 붙잡힘의 「비명」(58차, 민: 「scream을 붙잡힘 장면으로 쓰자」). 김진선님 사망 컷신(<c>DeathCutscene_Auditory</c>)은 고치지 않고,
/// 그 인스턴스에서 소년의 숨쉬기 트랙만 떼어 내고 이 컨트롤러(숨쉬기 → 비명)를 소년 애니메이터에 붙인다(<see cref="CutsceneScream"/>).
/// 빌더(<c>StandInPrefabBuilder</c>)가 <c>Resources/CutsceneScream.asset</c>으로 만든다.
/// </summary>
public sealed class CutsceneScreamSO : ScriptableObject
{
    /// <summary>Resources 이름.</summary>
    public const string ResourceName = "CutsceneScream";

    /// <summary>비명 상태 이름.</summary>
    public const string ScreamState = "Scream";

    [Tooltip("숨쉬기(기본, 반복) → 비명(한 번) 컨트롤러. 경로는 컷신 소년(Boy) 기준(SkirtBoy_Rig/...).")]
    [SerializeField] private RuntimeAnimatorController controller;

    [Tooltip("비명 동작에서 소년이 가장 앞으로 덮쳐 오는 순간(초). 컷신의 Hit 소리(SFX Hit) 시작에 이 순간을 맞춘다.")]
    [SerializeField] private float peakSeconds = 0.8f;

    [Tooltip("비명 정점의 머리를 숨쉬기 때 머리 자리로 옮기는 이동(소년 로컬, m). 덮쳐 오며 몸을 낮춰 얼굴이 화면 아래로 빠지지 않게.")]
    [SerializeField] private Vector3 headShift;

    [Tooltip("비명 정점에서 소년의 가슴이 숨쉬기 때와 같은 쪽(카메라)을 보게 돌리는 각(°, 위축).")]
    [SerializeField] private float turnDegrees;

    [Tooltip("떼어 낼 컷신 트랙(소년 숨쉬기).")]
    [SerializeField] private string idleTrack = "Boy idle";

    [Tooltip("맞출 소리 트랙 이름 앞부분(가장 이른 시작이 「Hit」).")]
    [SerializeField] private string hitTrackPrefix = "SFX Hit";

    public RuntimeAnimatorController Controller
    {
        get { return controller; }
    }

    public float PeakSeconds
    {
        get { return peakSeconds; }
    }

    public Vector3 HeadShift
    {
        get { return headShift; }
    }

    public float TurnDegrees
    {
        get { return turnDegrees; }
    }

    public string IdleTrack
    {
        get { return idleTrack; }
    }

    public string HitTrackPrefix
    {
        get { return hitTrackPrefix; }
    }

    /// <summary>빌더가 쓴다.</summary>
    public void Configure(RuntimeAnimatorController ctrl, float peak, Vector3 shift, float turn)
    {
        controller = ctrl;
        peakSeconds = Mathf.Max(0f, peak);
        headShift = shift;
        turnDegrees = turn;
    }
}

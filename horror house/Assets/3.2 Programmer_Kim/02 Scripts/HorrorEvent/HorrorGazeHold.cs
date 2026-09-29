using System;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 플레이어가 바라보는 <b>동안</b> 화면 볼륨을 켜고, 시선을 떼면 끈다.
/// <para>
/// <see cref="HorrorGazeTrigger"/>는 바라보면 연출을 <b>한 번</b> 재생하고, 이 컴포넌트는 보는 내내 유지한다.
/// 세기(<see cref="Weight"/>, 0~1)는 켜질 때 <see cref="fadeInSeconds"/>, 꺼질 때 <see cref="fadeOutSeconds"/>에 걸쳐 따라간다.
/// </para>
/// <para>
/// 볼륨의 weight를 <b>이 컴포넌트가 직접 쓴다.</b> 같은 볼륨을 Timeline(Animation 트랙)으로도 움직이면 서로 덮어쓴다 — 한쪽만 쓸 것.
/// 게임 시간 기준이라 일시정지 중에는 그대로 멈춘다.
/// </para>
/// </summary>
public class HorrorGazeHold : MonoBehaviour
{
    [Serializable]
    public class VolumeTarget
    {
        [Tooltip("켜고 끌 볼륨. 레이어는 Viewmodel이어야 플레이어 카메라에 보인다.")]
        public Volume volume;
        [Tooltip("완전히 바라볼 때의 weight. 여러 볼륨을 섞을 때 비율이 된다.")]
        [Range(0f, 1f)] public float maxWeight = 1f;
    }

    [Header("바라보기")]
    [Tooltip("바라봐야 하는 지점.")]
    [SerializeField] private Transform lookPoint;
    [Tooltip("여기(와 자식)에 시선이 막히는 것은 가린 것으로 치지 않는다. 비워 두면 Look Point의 부모.")]
    [SerializeField] private Transform lookObject;
    [Tooltip("화면 가운데에서 이 각도(도) 안에 들어와야 바라본 것으로 친다.")]
    [SerializeField, Range(1f, 45f)] private float maxAngle = 12f;
    [Tooltip("이 거리(m)보다 멀면 바라봐도 켜지지 않는다.")]
    [SerializeField, Min(0.5f)] private float maxDistance = 12f;
    [Tooltip("시선이 이 시간(초) 이어져야 켜지기 시작한다. 스쳐 지나가는 시선에 반응하지 않게 한다.")]
    [SerializeField, Min(0f)] private float startDelay = 0.3f;

    [Header("세기 변화")]
    [Tooltip("0에서 최대까지 올라가는 시간(초).")]
    [SerializeField, Min(0.01f)] private float fadeInSeconds = 1.2f;
    [Tooltip("최대에서 0까지 내려가는 시간(초).")]
    [SerializeField, Min(0.01f)] private float fadeOutSeconds = 0.8f;

    [Header("화면 볼륨")]
    [SerializeField] private VolumeTarget[] volumes = new VolumeTarget[0];

    private float lookTime;

    /// <summary>현재 세기(0~1, 선형). 다른 연출(나무 흔들림 등)이 읽는다.</summary>
    public float Weight { get; private set; }

    /// <summary>이번 프레임에 바라보고 있는지.</summary>
    public bool IsLooking { get; private set; }

    private void Awake()
    {
        if (lookObject == null && lookPoint != null) lookObject = lookPoint.parent;
        if (lookPoint == null) Debug.LogWarning($"[HorrorGazeHold] {name}: Look Point가 비어 있습니다.", this);
        Apply(0f);
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;   // 일시정지

        IsLooking = HorrorGaze.IsLooking(lookPoint, lookObject, maxAngle, maxDistance);
        lookTime = IsLooking ? lookTime + dt : 0f;

        bool on = IsLooking && lookTime >= startDelay;
        float step = on ? dt / fadeInSeconds : -dt / fadeOutSeconds;
        Weight = Mathf.Clamp01(Weight + step);

        // 선형으로 오르내리면 끝이 뚝 끊겨 보인다. 양끝을 부드럽게 한다.
        Apply(Weight * Weight * (3f - 2f * Weight));
    }

    private void OnDisable()
    {
        Weight = 0f;
        lookTime = 0f;
        Apply(0f);
    }

    private void Apply(float eased)
    {
        foreach (VolumeTarget target in volumes)
        {
            if (target != null && target.volume != null) target.volume.weight = eased * target.maxWeight;
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (lookPoint == null) return;
        Gizmos.color = new Color(0.6f, 0.2f, 1f);
        Gizmos.DrawWireSphere(lookPoint.position, 0.15f);
    }
}

using UnityEngine;

/// <summary>
/// 대역 프리팹에 실린 한 번짜리 동작 클립(58차, 아트 새 몹). 빌더(<c>StandInPrefabBuilder</c>)가 채운다.
/// 상시 동작(숨쉬기·흔들림)은 프리팹 애니메이터의 반복 상태가 맡고, 여기의 클립은 연출 코드가 필요할 때 직접 굴린다 —
/// 예: 앉은 소년의 머리 박기(<see cref="BoyHeadBang"/>)는 아트의 「kung」 동작에서 척추·목·머리 곡선만 떼어 낸 <see cref="Bang"/>을 쓴다(앉은 다리는 그대로).
/// </summary>
[DisallowMultipleComponent]
public sealed class StandInClips : MonoBehaviour
{
    [Tooltip("머리 박기 — 척추·가슴·목·머리 곡선만 든 클립(경로는 Model 기준).")]
    [SerializeField] private AnimationClip bang;

    [Tooltip("머리 박기에서 머리가 책상에 닿는 순간(초) — 소리를 낸다.")]
    [SerializeField] private float[] bangThuds = new float[0];

    [Tooltip("클립 경로의 기준(대개 Model 자식).")]
    [SerializeField] private Transform clipRoot;

    /// <summary>머리 박기 클립. 없으면 null.</summary>
    public AnimationClip Bang
    {
        get { return bang; }
    }

    /// <summary>머리가 닿는 순간들(초, 오름차순).</summary>
    public float[] BangThuds
    {
        get { return bangThuds ?? new float[0]; }
    }

    /// <summary>클립을 샘플할 대상.</summary>
    public GameObject ClipRoot
    {
        get { return clipRoot != null ? clipRoot.gameObject : gameObject; }
    }

    /// <summary>빌더가 쓴다.</summary>
    public void Configure(AnimationClip bangClip, float[] thuds, Transform root)
    {
        bang = bangClip;
        bangThuds = thuds ?? new float[0];
        clipRoot = root;
    }
}

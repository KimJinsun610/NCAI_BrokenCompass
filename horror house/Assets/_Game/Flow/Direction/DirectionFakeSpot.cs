using UnityEngine;

/// <summary>
/// 가짜 놀람을 맡길 씬의 연출 자리(2026-10-02). 김진선님의 <see cref="HorrorEvent"/> 프리팹 인스턴스(캐비닛 쾅·삐걱, 벌레 떼)에 붙인다.
/// 디렉터가 그 가짜 놀람을 고르면 <see cref="DirectionStage"/>가 플레이어 근처(<see cref="MaxDistance"/>)의 자리를 찾아
/// <see cref="HorrorEvent.Play"/>를 부른다. 없으면 소리만 낸다.
/// <para>이 인스턴스들의 자체 구역 트리거(<c>HorrorTriggerZone</c>)는 씬에서 꺼 둔다 — 가짜 놀람은 놀람 예산·간격을 지키는
/// 디렉터만 건다(같은 것 밤 2회까지). 같은 ID의 자리가 여럿이면 시야 안의 자리를 먼저 고른다.</para>
/// </summary>
[DisallowMultipleComponent]
public sealed class DirectionFakeSpot : MonoBehaviour
{
    [Tooltip("가짜 놀람 ID(fake.locker.rattle · fake.locker.row · fake.bugs).")]
    [SerializeField] private string fakeId = string.Empty;

    [Tooltip("이 거리(m, 수평) 안에 플레이어가 있을 때만 쓴다.")]
    [SerializeField, Min(1f)] private float maxDistance = 14f;

    [Tooltip("이미 재생한 뒤에도 다시 쓸지(끝 자세가 남는 연출 — 열린 채 남는 캐비닛 — 은 끔).")]
    [SerializeField] private bool replayable = true;

    /// <summary>가짜 놀람 ID.</summary>
    public string FakeId
    {
        get { return fakeId; }
    }

    /// <summary>쓸 수 있는 거리.</summary>
    public float MaxDistance
    {
        get { return maxDistance; }
    }

    /// <summary>다시 쓸 수 있는가.</summary>
    public bool Replayable
    {
        get { return replayable; }
    }

    /// <summary>에디터 도구가 쓴다.</summary>
    public void Configure(string id, float distance, bool canReplay)
    {
        fakeId = id ?? string.Empty;
        maxDistance = Mathf.Max(1f, distance);
        replayable = canReplay;
    }
}

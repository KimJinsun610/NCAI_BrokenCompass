using System.Collections.Generic;
using NightDuty;
using UnityEngine;

/// <summary>
/// 씬에 놓는 고정 연출 자리(2026-10-01, 7단계 후속). 몹을 「플레이어 앞 몇 m」가 아니라 <b>정해 둔 자리·방향</b>에 세운다.
/// <list type="bullet">
/// <item>위치 = 몹 프리팹의 피벗(발바닥 중앙, 앉은 몹은 엉덩이 아래 바닥, 천장 다리는 천장에 붙는 점), 앞(+Z) = 몹이 바라볼 방향.</item>
/// <item>켜질 때 <see cref="StagePoints"/>에 자리를 적는다 — 디렉터가 소리·판정 단서의 점을 같은 자리로 잡는다.</item>
/// <item><see cref="gazeProxy"/>가 있으면 대역의 응시·비춤 판정 상자를 그 자리로 옮긴다. 창밖 남자처럼 창틀·블라인드 콜라이더에
/// 가려 레이가 몸에 닿지 않는 몹은 창 안쪽 면에 상자를 둔다(크기 = 그 트랜스폼의 스케일).</item>
/// </list>
/// 씬 파일에는 이 컴포넌트와 빈 트랜스폼만 남는다. 프리팹은 <c>Resources/StandIns/&lt;ID&gt;</c>.
/// </summary>
[DisallowMultipleComponent]
public sealed class StageAnchor : MonoBehaviour
{
    private static readonly Dictionary<string, StageAnchor> s_byId = new Dictionary<string, StageAnchor>(System.StringComparer.Ordinal);

    [Tooltip("자리 ID(stage.boy.seat · stage.legs.ceiling · stage.window.man). 대본의 StageAnchor와 같아야 한다.")]
    [SerializeField] private string anchorId = string.Empty;

    [Tooltip("응시·비춤 판정 상자 자리(선택). 위치·회전 = 상자 중심·방향, 스케일 = 상자 크기.")]
    [SerializeField] private Transform gazeProxy;

    [Tooltip("디버그 콘솔 [이동+실행]이 플레이어를 세울 거리(자리 앞 m).")]
    [SerializeField, Min(0.5f)] private float debugViewDistance = 4f;

    /// <summary>자리 ID.</summary>
    public string AnchorId
    {
        get { return anchorId; }
    }

    /// <summary>응시 상자 자리. 없으면 null.</summary>
    public Transform GazeProxy
    {
        get { return gazeProxy; }
    }

    /// <summary>디버그 콘솔이 플레이어를 세울 거리.</summary>
    public float DebugViewDistance
    {
        get { return debugViewDistance; }
    }

    /// <summary>그 ID의 자리. 없으면 null.</summary>
    public static StageAnchor Find(string id)
    {
        StageAnchor a;
        if (string.IsNullOrEmpty(id) || !s_byId.TryGetValue(id, out a)) return null;
        return a != null && a.isActiveAndEnabled ? a : null;
    }

    /// <summary>에디터 도구가 만들 때 쓴다.</summary>
    public void Configure(string id, Transform proxy, float viewDistance)
    {
        anchorId = id ?? string.Empty;
        gazeProxy = proxy;
        debugViewDistance = Mathf.Max(0.5f, viewDistance);
    }

    private void OnEnable()
    {
        if (anchorId.Length == 0) return;
        s_byId[anchorId] = this;
        StagePoints.Set(anchorId, transform.position);
    }

    private void OnDisable()
    {
        if (anchorId.Length == 0) return;
        StageAnchor current;
        if (s_byId.TryGetValue(anchorId, out current) && current == this)
        {
            s_byId.Remove(anchorId);
            StagePoints.Remove(anchorId);
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        s_byId.Clear();
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.4f, 0.1f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, 0.12f);
        Gizmos.DrawLine(transform.position, transform.position + transform.forward * 0.6f);
        if (gazeProxy != null)
        {
            Gizmos.color = new Color(0.2f, 1f, 0.9f, 0.8f);
            Gizmos.matrix = Matrix4x4.TRS(gazeProxy.position, gazeProxy.rotation, Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, gazeProxy.lossyScale);
            Gizmos.matrix = Matrix4x4.identity;
        }
    }
}

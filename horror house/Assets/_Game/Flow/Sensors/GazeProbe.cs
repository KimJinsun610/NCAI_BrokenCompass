using System;
using NightDuty;
using UnityEngine;

/// <summary>
/// 응시 발신기. 카메라 중앙 레이의 <b>첫 가시 충돌체</b> 대상 ID를 구해
/// <see cref="SignalKind.GazeSample"/>을 0.1초마다 보낸다.
/// <list type="bullet">
/// <item><b>대상이 없어도 빈 ID로 보낸다.</b> <c>GazeCondition</c>의 유예 시계(H2·C3의 2초)와 응시 시계는
/// <c>Tick</c>이 아니라 <b>이 샘플의 Value</b>로만 흐른다. 안 보내면 유예가 영원히 끝나지 않는다.</item>
/// <item><b>벽·닫힌 문을 관통하지 않는다.</b> <see cref="Physics.Raycast"/> 한 번의 첫 충돌만 본다.
/// <c>RaycastAll</c>로 뒤엣것을 골라내면 관통이 되므로 쓰지 않는다.</item>
/// <item>트리거 콜라이더는 무시한다. 씬의 <c>Physics.queriesHitTriggers</c>가 true이고
/// 벤더 문 프리팹이 문 앞에 감지용 트리거 상자를 두고 있어서, 무시하지 않으면 문 대신 그 상자를 맞힌다.</item>
/// </list>
/// <para>
/// 발신은 <see cref="PlayerSensors"/>가 누산기로 부른다. 이 클래스는 <see cref="MonoBehaviour"/>가 아니다 —
/// 자기 <c>Update</c>를 가지면 허브가 정한 고정 순서(CLAUDE.md §4.4.1)를 지킬 수 없기 때문이다.
/// </para>
/// <para>
/// <see cref="CurrentId"/>는 다른 발신기가 읽는다(<c>DoorRelay</c>의 「자동 개방을 <b>보았다</b>」 판단,
/// 앞으로 만들 식별 0.2초·<c>ModelObserved</c> 발신기).
/// </para>
/// </summary>
[Serializable]
public sealed class GazeProbe
{
    [Tooltip("레이 최대 거리(m). 이보다 먼 대상은 응시로 보지 않는다.")]
    [SerializeField, Min(1f)] private float maxDistance = 30f;

    [Tooltip("레이가 맞힐 레이어. 벽·문을 반드시 포함해야 관통이 막힌다. 기본은 전부.")]
    [SerializeField] private LayerMask blockingMask = ~0;

    [Tooltip("켜면 매 샘플의 대상 ID를 콘솔에 남긴다(시험용).")]
    [SerializeField] private bool logSamples;

    // 플레이어 자신의 콜라이더를 건너뛰는 최대 횟수. 카메라가 캡슐 안에 있어도 안전하게 한다.
    private const int MaxSelfSkips = 4;

    private string _currentId = string.Empty;
    private bool _hasCollider;
    private Vector3 _lastPoint;
    private Transform _lastHit;

    /// <summary>가장 최근 샘플의 대상 ID. 아무것도 안 보고 있으면 빈 문자열.</summary>
    public string CurrentId
    {
        get { return _currentId; }
    }

    /// <summary>가장 최근 샘플이 무언가에 맞았는지(대상 ID가 없는 벽도 true).</summary>
    public bool HasCollider
    {
        get { return _hasCollider; }
    }

    /// <summary>가장 최근 샘플이 맞힌 지점(디버그 기즈모용).</summary>
    public Vector3 LastPoint
    {
        get { return _lastPoint; }
    }

    /// <summary>가장 최근 샘플이 맞힌 콜라이더의 트랜스폼. 없으면 null.</summary>
    public Transform LastHit
    {
        get { return _lastHit; }
    }

    /// <summary>밤이 바뀔 때 상태를 비운다.</summary>
    public void Reset()
    {
        _currentId = string.Empty;
        _hasCollider = false;
        _lastHit = null;
    }

    /// <summary>
    /// 카메라 중앙을 한 번 재서 <see cref="CurrentId"/>를 갱신한다. 신호는 보내지 않는다.
    /// 허브가 샘플 직전에 부른다.
    /// </summary>
    public void Probe(Camera camera, Transform playerRoot)
    {
        _currentId = string.Empty;
        _hasCollider = false;
        _lastHit = null;

        if (camera == null)
        {
            return;
        }

        Vector3 origin = camera.transform.position;
        Vector3 direction = camera.transform.forward;
        float remaining = maxDistance;

        for (int i = 0; i < MaxSelfSkips; i++)
        {
            RaycastHit hit;
            // QueryTriggerInteraction.Ignore: 트리거는 「가시 충돌체」가 아니다.
            if (!Physics.Raycast(origin, direction, out hit, remaining, blockingMask, QueryTriggerInteraction.Ignore))
            {
                return;
            }

            // 플레이어 자신의 캡슐을 맞혔으면 그 너머부터 다시 잰다.
            if (playerRoot != null && hit.collider.transform.IsChildOf(playerRoot))
            {
                float step = hit.distance + 0.01f;
                remaining -= step;
                if (remaining <= 0f)
                {
                    return;
                }

                origin += direction * step;
                continue;
            }

            _hasCollider = true;
            _lastPoint = hit.point;
            _lastHit = hit.collider.transform;
            _currentId = JudgeTarget.IdOf(hit.collider);   // 표식이 없으면 빈 문자열 = 벽을 본 것과 같다.

            if (logSamples)
            {
                Debug.Log("[GazeProbe] " + (_currentId.Length == 0 ? "(대상 없음) " + hit.collider.name : _currentId));
            }

            return;
        }
    }

    /// <summary>
    /// 이 샘플이 대표하는 시간만큼 응시 샘플을 보낸다. <b>대상이 없어도 빈 ID로 반드시 보낸다.</b>
    /// <see cref="Probe"/>를 먼저 불러 둬야 한다.
    /// </summary>
    public void Send(float sampleSeconds)
    {
        NightRun.Send(JudgeSignal.Gaze(_currentId, sampleSeconds));
    }
}

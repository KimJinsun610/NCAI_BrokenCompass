using System;
using System.Collections.Generic;
using NightDuty;
using UnityEngine;

/// <summary>
/// 응시 발신기. 응시 기준점에서 본 대상 ID를 구해 <see cref="SignalKind.GazeSample"/>을 0.1초마다 보낸다.
/// <list type="bullet">
/// <item><b>응시 정의(2026-09-30 최종 기획서 판정 공통 정의)</b>: 대상의 판정 콜라이더 중심이 응시 기준점에서
/// <see cref="SensingRules.GazeConeDegrees"/>(10°) 안에 있고 레이로 가려지지 않으면 본 것이다. 후보가 여럿이면 각도가 가장 작은 것.
/// <b>판정 콜라이더(트리거가 아닌 콜라이더)가 있는 대상만</b> 후보다 — 바닥 구역·통행 구역 같은 트리거 대상은 보지 않는다.
/// 원뿔 안에 대상이 없으면 기준점 레이의 첫 가시 충돌체를 쓴다(옛 방식 — 벽을 보고 있으면 빈 ID).</item>
/// <item><b>응시 기준점</b>: 평소 화면 중심, 태블릿을 든 동안은 태블릿 위 가운데(화면 높이 78%) —
/// 태블릿 뒤에 숨어 대상을 마음껏 볼 수 없다. <see cref="Probe"/>의 <c>viewportY</c>로 받는다.</item>
/// <item><b>대상이 없어도 빈 ID로 보낸다.</b> <c>GazeCondition</c>의 유예 시계와 응시 시계는
/// <c>Tick</c>이 아니라 <b>이 샘플의 Value</b>로만 흐른다. 안 보내면 유예가 영원히 끝나지 않는다.</item>
/// <item><b>벽·닫힌 문을 관통하지 않는다.</b> 가림 검사는 대상 중심까지의 레이 한 번의 첫 충돌로 본다.</item>
/// <item>트리거 콜라이더는 무시한다(벤더 문 프리팹의 감지용 트리거 상자).</item>
/// </list>
/// <para>
/// 발신은 <see cref="PlayerSensors"/>가 누산기로 부른다. 이 클래스는 <see cref="MonoBehaviour"/>가 아니다 —
/// 자기 <c>Update</c>를 가지면 허브가 정한 고정 순서(CLAUDE.md §4.4.1)를 지킬 수 없기 때문이다.
/// </para>
/// </summary>
[Serializable]
public sealed class GazeProbe
{
    [Tooltip("레이 최대 거리(m). 이보다 먼 대상은 응시로 보지 않는다.")]
    [SerializeField, Min(1f)] private float maxDistance = 30f;

    [Tooltip("레이가 맞힐 레이어. 벽·문을 반드시 포함해야 관통이 막힌다. 기본은 전부.")]
    [SerializeField] private LayerMask blockingMask = ~0;

    [Tooltip("켜면 응시 원뿔(10°)로 대상을 찾는다(최종 기획서). 끄면 기준점 레이의 첫 충돌만 본다(옛 방식).")]
    [SerializeField] private bool useCone = true;

    [Tooltip("켜면 매 샘플의 대상 ID를 콘솔에 남긴다(시험용).")]
    [SerializeField] private bool logSamples;

    // 플레이어 자신의 콜라이더를 건너뛰는 최대 횟수. 카메라가 캡슐 안에 있어도 안전하게 한다.
    private const int MaxSelfSkips = 4;

    private static readonly List<JudgeTarget> Candidates = new List<JudgeTarget>();

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
    /// 응시 기준점에서 한 번 재서 <see cref="CurrentId"/>를 갱신한다. 신호는 보내지 않는다. 허브가 샘플 직전에 부른다.
    /// </summary>
    /// <param name="camera">플레이어 카메라.</param>
    /// <param name="playerRoot">플레이어 루트(자기 콜라이더 건너뛰기).</param>
    /// <param name="viewportY">응시 기준점 높이(뷰포트 0~1). 평소 0.5, 태블릿을 든 동안 0.78.</param>
    public void Probe(Camera camera, Transform playerRoot, float viewportY = 0.5f)
    {
        _currentId = string.Empty;
        _hasCollider = false;
        _lastHit = null;

        if (camera == null)
        {
            return;
        }

        Ray ray = camera.ViewportPointToRay(new Vector3(0.5f, viewportY, 0f));

        if (useCone && TryCone(ray, playerRoot))
        {
            return;
        }

        CenterRay(ray, playerRoot);
    }

    /// <summary>원뿔 10° 안에서 가려지지 않은 대상 중 각도가 가장 작은 것.</summary>
    private bool TryCone(Ray ray, Transform playerRoot)
    {
        JudgeTargetRegistry.CollectOwners(Candidates);
        JudgeTarget best = null;
        float bestAngle = float.MaxValue;
        Vector3 bestPoint = Vector3.zero;

        for (int i = 0; i < Candidates.Count; i++)
        {
            JudgeTarget t = Candidates[i];
            if (t == null || !t.isActiveAndEnabled || t.PrimaryId.Length == 0) continue;

            Vector3 center;
            if (!TrySolidCenter(t, out center)) continue;

            Vector3 to = center - ray.origin;
            float dist = to.magnitude;
            if (dist > maxDistance || dist < 0.01f) continue;

            float angle = Vector3.Angle(ray.direction, to);
            if (angle > SensingRules.GazeConeDegrees || angle >= bestAngle) continue;
            if (!Visible(ray.origin, center, t, playerRoot)) continue;

            best = t;
            bestAngle = angle;
            bestPoint = center;
        }

        if (best == null) return false;

        _currentId = best.PrimaryId;
        _hasCollider = true;
        _lastPoint = bestPoint;
        _lastHit = best.transform;
        if (logSamples) Debug.Log("[GazeProbe] 원뿔 " + _currentId + " " + bestAngle.ToString("F1") + "°");
        return true;
    }

    /// <summary>대상의 판정 콜라이더(트리거가 아닌 켜진 콜라이더) 중심. 없으면 false — 구역 대상은 응시 후보가 아니다.</summary>
    private static bool TrySolidCenter(JudgeTarget t, out Vector3 center)
    {
        Collider[] colliders = t.GetComponentsInChildren<Collider>();
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null && colliders[i].enabled && !colliders[i].isTrigger)
            {
                center = colliders[i].bounds.center;
                return true;
            }
        }

        center = t.transform.position;
        return false;
    }

    /// <summary>기준점에서 대상 중심까지 가리는 것이 없는지(첫 충돌이 그 대상이거나, 대상 중심 앞에서 아무것도 안 맞음).</summary>
    private bool Visible(Vector3 origin, Vector3 center, JudgeTarget target, Transform playerRoot)
    {
        Vector3 dir = center - origin;
        float remaining = dir.magnitude;
        dir /= remaining;

        for (int i = 0; i < MaxSelfSkips; i++)
        {
            RaycastHit hit;
            if (!Physics.Raycast(origin, dir, out hit, remaining, blockingMask, QueryTriggerInteraction.Ignore))
            {
                return true;
            }

            if (playerRoot != null && hit.collider.transform.IsChildOf(playerRoot))
            {
                float step = hit.distance + 0.01f;
                remaining -= step;
                if (remaining <= 0f) return true;
                origin += dir * step;
                continue;
            }

            JudgeTarget owner = hit.collider.GetComponentInParent<JudgeTarget>();
            return owner == target;
        }

        return false;
    }

    /// <summary>기준점 레이의 첫 가시 충돌체(옛 방식).</summary>
    private void CenterRay(Ray ray, Transform playerRoot)
    {
        Vector3 origin = ray.origin;
        Vector3 direction = ray.direction;
        float remaining = maxDistance;

        for (int i = 0; i < MaxSelfSkips; i++)
        {
            RaycastHit hit;
            if (!Physics.Raycast(origin, direction, out hit, remaining, blockingMask, QueryTriggerInteraction.Ignore))
            {
                return;
            }

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

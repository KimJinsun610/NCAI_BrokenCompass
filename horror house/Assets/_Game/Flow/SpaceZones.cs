using System;
using NightDuty;
using UnityEngine;

/// <summary>
/// 플레이어 위치로 <b>공간 · 구역</b> 신호를 만들어 <see cref="NightRun"/>에 보낸다.
/// <list type="bullet">
/// <item><b>공간</b>: 방마다 트리거를 다는 대신 축 정렬 상자 목록으로 검사한다. 복도가 ㄱ자라 상자 하나로 감쌀 수 없어,
/// 방 상자 어디에도 없으면 복도로 본다(건물 밖이면 None). 발밑 기준점만 쓴다.</item>
/// <item><b>구역</b>: 칸 안·금지 구역처럼 ID를 가진 상자. 들어가고 나갈 때 신호를 보낸다.</item>
/// </list>
/// <para>2026-10-03: 옛 「점검 상자 1초 체류 → 퇴실 때 점검 완료」와 「통행 구역 → 통행 완료」를 지웠다. 점검은 새 점검표(InspectionSensor·InspectionBoard)가 한다.</para>
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(40)]
public sealed class SpaceZones : MonoBehaviour
{
    /// <summary>방 하나. y는 층 구분에 쓴다(도서관은 2층이라 y가 다르다).</summary>
    [Serializable]
    public struct Zone
    {
        public SpaceId Space;
        [Tooltip("월드 상자. 바닥 타일 범위를 쓴다")] public Bounds Box;
        [Tooltip("켜면 순찰 공간이 아니다(스코프 밖)")] public bool OutOfScope;
    }

    /// <summary>ID를 가진 구역(칸 안·금지 등).</summary>
    [Serializable]
    public struct SignalZone
    {
        [Tooltip("대상 ID (예: toilet.stall.inner.inside)")] public string Id;
        public Bounds Box;
    }

    [Tooltip("비워 두면 FPController를 찾는다.")]
    [SerializeField] private Transform player;

    [SerializeField] private Zone[] zones = new Zone[0];

    [SerializeField] private SignalZone[] signalZones = new SignalZone[0];

    [Tooltip("건물 전체 범위. 이 밖이면 공간 없음(None).")]
    [SerializeField] private Bounds building = new Bounds(new Vector3(20f, 3.4f, 42f), new Vector3(70f, 4f, 30f));

    [Tooltip("위치 검사 간격(초).")]
    [SerializeField, Min(0.02f)] private float sampleSeconds = 0.1f;

    private SpaceId _current = SpaceId.None;
    private float _acc;
    private bool[] _inZone;

    /// <summary>지금 판정된 공간.</summary>
    public SpaceId Current => _current;

    /// <summary>그 공간의 방 상자(교실 두 값은 같은 교실로 본다). 없으면 false. 연출(소등한 방 전체 구역 등)이 쓴다.</summary>
    public bool TryGetSpaceBox(SpaceId space, out Bounds box)
    {
        for (int i = 0; i < zones.Length; i++)
        {
            if (zones[i].OutOfScope || zones[i].Space != space) continue;   // 정확히 같은 값 먼저(교실 1-3을 달라면 1-3)
            box = zones[i].Box;
            return true;
        }

        SpaceId want = SpaceIds.Canonical(space);
        for (int i = 0; i < zones.Length; i++)
        {
            if (zones[i].OutOfScope || SpaceIds.Canonical(zones[i].Space) != want) continue;
            box = zones[i].Box;
            return true;
        }

        box = default(Bounds);
        return false;
    }

    /// <summary>그 ID의 신호 구역 상자. 없으면 false.</summary>
    public bool TryGetSignalZone(string id, out Bounds box)
    {
        for (int i = 0; i < signalZones.Length; i++)
        {
            if (signalZones[i].Id != id) continue;
            box = signalZones[i].Box;
            return true;
        }

        box = default(Bounds);
        return false;
    }

    /// <summary>신호 한 건을 코어로 보낸다(옛 큐 발신기 AnomalyCueDirector는 2026-10-03 폐기).</summary>
    private static void Send(in JudgeSignal s)
    {
        NightRun.Send(s);
    }

    private void Update()
    {
        // 밤이 아니거나 포획됐으면 샘플하지 않는다(태블릿을 든 동안에도 샘플한다 — 최종 기획서).
        if (!NightRun.IsNightActive || NightRun.IsCaptured)
        {
            _acc = 0f;
            return;
        }

        if (player == null)
        {
            FPController fp = FindAnyObjectByType<FPController>();
            if (fp == null) return;
            player = fp.transform;
        }

        float step = sampleSeconds;

        // 고정 간격 누산. Time.time 게이트는 프레임 지터만큼 매 샘플이 뒤로 밀려,
        // 같은 방식으로 응시를 보내면 H2·C3의 3초가 실제 3.4초가 된다(CLAUDE.md §5.5-18).
        _acc += Time.deltaTime;

        int guard = 0;
        while (_acc >= step)
        {
            _acc -= step;

            if (++guard > 50)
            {
                _acc = 0f;
                break;
            }

            Vector3 p = player.position;
            UpdateSignalZones(p);
            UpdateSpace(p);
        }
    }

    private void UpdateSpace(Vector3 p)
    {
        SpaceId space = SpaceAt(p);
        if (space == _current)
        {
            return;
        }

        if (_current != SpaceId.None)
        {
            Send(JudgeSignal.OfSpace(SignalKind.SpaceExited, _current));
        }

        _current = space;

        if (_current != SpaceId.None)
        {
            Send(JudgeSignal.OfSpace(SignalKind.SpaceEntered, _current));
        }
    }

    private void UpdateSignalZones(Vector3 p)
    {
        if (_inZone == null || _inZone.Length != signalZones.Length)
        {
            _inZone = new bool[signalZones.Length];
        }

        for (int i = 0; i < signalZones.Length; i++)
        {
            bool inside = signalZones[i].Box.Contains(p);
            if (inside == _inZone[i]) continue;
            _inZone[i] = inside;

            Send(JudgeSignal.Target(inside ? SignalKind.ZoneEntered : SignalKind.ZoneExited, signalZones[i].Id));
        }
    }

    /// <summary>한 점이 속한 공간. 방 상자 우선, 없으면 건물 안이면 복도.</summary>
    public SpaceId SpaceAt(Vector3 point)
    {
        for (int i = 0; i < zones.Length; i++)
        {
            if (zones[i].Box.Contains(point))
            {
                return zones[i].OutOfScope ? SpaceId.None : zones[i].Space;
            }
        }

        return building.Contains(point) ? SpaceId.Corridor : SpaceId.None;
    }

    private void OnDrawGizmosSelected()
    {
        for (int i = 0; i < zones.Length; i++)
        {
            Gizmos.color = zones[i].OutOfScope ? new Color(1f, 0.3f, 0.3f, 0.2f) : new Color(0.3f, 1f, 0.6f, 0.2f);
            Gizmos.DrawCube(zones[i].Box.center, zones[i].Box.size);
        }

        for (int i = 0; i < signalZones.Length; i++)
        {
            Gizmos.color = new Color(1f, 0.9f, 0.3f, 0.2f);
            Gizmos.DrawCube(signalZones[i].Box.center, signalZones[i].Box.size);
        }

        Gizmos.color = new Color(1f, 1f, 1f, 0.15f);
        Gizmos.DrawWireCube(building.center, building.size);
    }
}

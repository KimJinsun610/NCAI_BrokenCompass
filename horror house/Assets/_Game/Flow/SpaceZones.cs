using System;
using NightDuty;
using UnityEngine;

/// <summary>
/// 플레이어 위치로 <b>공간 · 점검 · 구역</b> 신호를 만들어 <see cref="NightRun"/>에 보낸다.
/// <list type="bullet">
/// <item><b>공간</b>: 방마다 트리거를 다는 대신 축 정렬 상자 목록으로 검사한다. 복도가 ㄱ자라 상자 하나로 감쌀 수 없어,
/// 방 상자 어디에도 없으면 복도로 본다(건물 밖이면 None). 발밑 기준점만 쓴다.</item>
/// <item><b>점검</b>: 공간마다 지정 점검 상자에 1초 이상 머문 뒤 그 공간을 나가면 점검 완료다(기획서: 단순 통행은 점검이 아니다).
/// 신호 순서는 점검 완료 → 공간 이탈.</item>
/// <item><b>구역</b>: 통행 구역·금지 구역처럼 ID를 가진 상자. 들어가고 나갈 때 신호를 보내고, 통행 구역은 나갈 때 통행 완료를 함께 보낸다.</item>
/// </list>
/// <para>중립 구역(복도↔교실 손전등 전환)은 아직 없다. 손전등 카드(H3·C5)를 시험할 때 추가한다.</para>
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
        [Tooltip("켜면 순찰 공간이 아니다(스코프 밖·경비실)")] public bool OutOfScope;
        [Tooltip("지정 점검 상자. 크기가 0이면 점검 판정을 하지 않는다")] public Bounds InspectionBox;
    }

    /// <summary>ID를 가진 구역(통행·금지·청취).</summary>
    [Serializable]
    public struct SignalZone
    {
        [Tooltip("대상 ID (예: corridor.passage)")] public string Id;
        public Bounds Box;
        [Tooltip("켜면 나갈 때 통행 완료도 함께 보낸다")] public bool IsPassage;
    }

    [Tooltip("비워 두면 FPController를 찾는다.")]
    [SerializeField] private Transform player;

    [SerializeField] private Zone[] zones = new Zone[0];

    [SerializeField] private SignalZone[] signalZones = new SignalZone[0];

    [Tooltip("건물 전체 범위. 이 밖이면 공간 없음(None).")]
    [SerializeField] private Bounds building = new Bounds(new Vector3(20f, 3.4f, 42f), new Vector3(70f, 4f, 30f));

    [Tooltip("점검으로 인정하는 최소 체류 시간(초).")]
    [SerializeField, Min(0.1f)] private float inspectionDwellSeconds = 1f;

    [Tooltip("위치 검사 간격(초).")]
    [SerializeField, Min(0.02f)] private float sampleSeconds = 0.1f;

    private SpaceId _current = SpaceId.None;
    private float _dwell;
    private bool _inspectedHere;
    private float _acc;
    private bool[] _inZone;

    /// <summary>지금 판정된 공간.</summary>
    public SpaceId Current => _current;

    /// <summary>
    /// 신호 한 건을 코어와 큐 발신기에 함께 보낸다.
    /// <para>
    /// <see cref="AnomalyCueDirector"/>는 공간 진입·구역 진입·통행·점검 완료 시점을 알아야 큐를 무장할 수 있는데,
    /// <see cref="NightRun"/>은 받은 신호를 되돌려 주지 않는다. 그래서 보내는 쪽에서 한 번 더 알린다.
    /// </para>
    /// </summary>
    private static void Send(in JudgeSignal s)
    {
        NightRun.Send(s);
        AnomalyCueDirector.Observe(s);
    }

    private void Update()
    {
        // 밤이 아니거나 포획됐거나 Tab이 열려 있으면 샘플하지 않는다.
        // Tab 중에 _current를 갱신하면 코어는 그 신호를 버리므로 「나간 적 없는 공간에서 나감」 상태가 남는다.
        if (!NightRun.IsNightActive || NightRun.IsCaptured || PlayerSensors.TabOpen)
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
            UpdateSpace(p, step);
        }
    }

    private void UpdateSpace(Vector3 p, float step)
    {
        SpaceId space = SpaceAt(p);

        if (space == _current)
        {
            if (_current != SpaceId.None && !_inspectedHere && InInspectionBox(_current, p))
            {
                _dwell += step;
                if (_dwell >= inspectionDwellSeconds)
                {
                    _inspectedHere = true;   // 체류는 채웠다. 공간을 나갈 때 점검 완료를 보낸다.
                }
            }

            return;
        }

        if (_current != SpaceId.None)
        {
            if (_inspectedHere)
            {
                Send(JudgeSignal.OfSpace(SignalKind.InspectionCompleted, _current));
            }

            Send(JudgeSignal.OfSpace(SignalKind.SpaceExited, _current));
        }

        _current = space;
        _dwell = 0f;
        _inspectedHere = false;

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

            string id = signalZones[i].Id;
            if (inside)
            {
                Send(JudgeSignal.Target(SignalKind.ZoneEntered, id));
                continue;
            }

            if (signalZones[i].IsPassage)
            {
                Send(JudgeSignal.Target(SignalKind.PassageCompleted, id));
            }

            Send(JudgeSignal.Target(SignalKind.ZoneExited, id));
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

    private bool InInspectionBox(SpaceId space, Vector3 point)
    {
        for (int i = 0; i < zones.Length; i++)
        {
            if (zones[i].Space != space || zones[i].OutOfScope) continue;
            Bounds box = zones[i].InspectionBox;
            return box.size.sqrMagnitude > 0f && box.Contains(point);
        }

        return false;
    }

    private void OnDrawGizmosSelected()
    {
        for (int i = 0; i < zones.Length; i++)
        {
            Gizmos.color = zones[i].OutOfScope ? new Color(1f, 0.3f, 0.3f, 0.2f) : new Color(0.3f, 1f, 0.6f, 0.2f);
            Gizmos.DrawCube(zones[i].Box.center, zones[i].Box.size);

            if (zones[i].InspectionBox.size.sqrMagnitude > 0f)
            {
                Gizmos.color = new Color(0.2f, 0.7f, 1f, 0.45f);
                Gizmos.DrawCube(zones[i].InspectionBox.center, zones[i].InspectionBox.size);
            }
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

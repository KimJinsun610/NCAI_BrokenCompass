using System;
using System.Collections.Generic;
using NightDuty;
using UnityEngine;

/// <summary>
/// 근접 발신기. 그날 쓰이는 바닥 기준점과 플레이어 발밑 기준점의 <b>수평(XZ) 거리</b>를
/// 0.1초마다 <see cref="SignalKind.ProximitySample"/>로 보낸다.
/// <list type="bullet">
/// <item><b>y는 완전히 무시한다.</b> 기준점을 천장 높이에 놓아도 결과가 같아야 한다(기획 정본 공통 명세 2절).</item>
/// <item><b>반올림하지 않는다.</b> float 원값을 그대로 넘긴다. <c>ProximityCondition</c>이 mm 정수로 바꿔
/// <c>거리 &lt; 반경</c>을 비교하고 「정확히 경계는 바깥」을 보장한다. 여기서 반올림하면 1.4996이 1.5가 되어
/// 그 보장이 깨진다.</item>
/// <item><b>반경을 모른다.</b> 1.5m(H6만 2m)는 카드 데이터에 있다. 발신기는 거리만 보낸다.</item>
/// </list>
///
/// <para><b>대상 수집.</b> <c>NightRun</c>에 「그날 쓰는 근접 기준점」만 돌려주는 API는 없다.
/// 대신 <see cref="NightRun.TodayDeck"/>(그날 덱)의 각 카드에 <c>RuleSO.CollectReferences</c>를 돌려
/// <b>그날 카드가 실제로 참조하는 ID만</b> 모은다. 덱이 비어 있으면(시험 씬) <see cref="JudgeTargetRegistry.Snapshot"/> 전체로 물러선다.
/// 근접이 아닌 ID(응시·단서·구역)도 섞이지만, <c>ProximityCondition</c>은 자기 앵커 ID가 아닌 샘플을 그냥 무시하므로 결과에 영향이 없다.</para>
///
/// <para><b>거리 컬링이 판정 결과를 바꾸지 않는 이유.</b>
/// <c>ProximityCondition.Observe</c>는 <c>거리 &lt; 반경</c>일 때만 true를 돌려주고, 그 밖의 샘플로는 아무 상태도 바뀌지 않는다
/// (타이머도 래치도 없다). 카드 반경의 최대값은 H6의 2m다. 따라서 컬링 반경을 그보다 크게(기본 4m) 두면
/// 잘려 나간 샘플은 전부 「반경 밖」이고, 보냈어도 false였을 것들이다.
/// 그래도 만약을 위해 <b>컬링 경계를 벗어나는 순간 마지막 한 번</b>은 실제 거리로 보낸다 —
/// 앞으로 「밖으로 나갔다」를 읽는 조건이 생겨도 그 전이를 놓치지 않게 하기 위해서다.</para>
///
/// <para>발신은 <see cref="PlayerSensors"/>가 누산기로 부른다. 자기 <c>Update</c>를 가지지 않는 이유는 <see cref="GazeProbe"/>와 같다.</para>
/// </summary>
[Serializable]
public sealed class ProximityProbe
{
    [Tooltip("이 거리(m) 밖의 기준점은 발신하지 않는다. 카드 최대 반경(H6 = 2m)보다 충분히 커야 한다.")]
    [SerializeField, Min(3f)] private float cullRadius = 4f;

    [Tooltip("대상 목록을 다시 모으는 주기(초). 0이면 매 샘플 다시 모은다.")]
    [SerializeField, Min(0f)] private float rebuildSeconds = 2f;

    [Tooltip("켜면 발신한 거리를 콘솔에 남긴다(시험용).")]
    [SerializeField] private bool logSamples;

    // 해석된 기준점. id와 표식을 같이 들고 있다(표식이 파괴되면 다시 모은다).
    private readonly List<string> _ids = new List<string>();
    private readonly List<JudgeTarget> _anchors = new List<JudgeTarget>();
    private readonly List<bool> _wasInRange = new List<bool>();

    private readonly List<string> _scratch = new List<string>();
    private readonly HashSet<string> _seen = new HashSet<string>(StringComparer.Ordinal);

    private float _sinceRebuild = float.MaxValue;
    private int _builtForDay = -1;
    private bool _dirty = true;

    /// <summary>지금 감시 중인 기준점 수(디버그).</summary>
    public int AnchorCount
    {
        get { return _anchors.Count; }
    }

    /// <summary>등록부가 바뀌었으니 다음 샘플에 다시 모으라고 표시한다.</summary>
    public void MarkDirty()
    {
        _dirty = true;
    }

    /// <summary>밤이 바뀔 때 상태를 비운다.</summary>
    public void Reset()
    {
        _ids.Clear();
        _anchors.Clear();
        _wasInRange.Clear();
        _builtForDay = -1;
        _dirty = true;
        _sinceRebuild = float.MaxValue;
    }

    /// <summary>이 샘플에서 기준점들과의 수평 거리를 보낸다.</summary>
    public void Sample(Transform player, float stepSeconds)
    {
        if (player == null)
        {
            return;
        }

        _sinceRebuild += stepSeconds;
        if (_dirty || _builtForDay != NightRun.Day || _sinceRebuild >= rebuildSeconds)
        {
            Rebuild();
        }

        Vector3 p = player.position;

        for (int i = 0; i < _anchors.Count; i++)
        {
            JudgeTarget anchor = _anchors[i];
            if (anchor == null)
            {
                _dirty = true;   // 표식이 사라졌다. 다음 샘플에 다시 모은다.
                continue;
            }

            Vector3 a = anchor.AnchorPosition;

            // 수평 거리. y는 쓰지 않는다.
            float dx = a.x - p.x;
            float dz = a.z - p.z;
            float distance = Mathf.Sqrt(dx * dx + dz * dz);

            bool inRange = distance <= cullRadius;
            bool wasInRange = _wasInRange[i];

            if (!inRange && !wasInRange)
            {
                continue;   // 계속 멀다. 보내도 반드시 「반경 밖」이므로 생략한다.
            }

            _wasInRange[i] = inRange;

            // 반올림 금지: 원값 그대로.
            NightRun.Send(JudgeSignal.Proximity(_ids[i], distance));

            if (logSamples)
            {
                Debug.Log("[ProximityProbe] " + _ids[i] + " = " + distance.ToString("F3") + "m");
            }
        }
    }

    /// <summary>그날 덱이 참조하는 ID를 모아 씬 표식으로 해석한다.</summary>
    private void Rebuild()
    {
        _sinceRebuild = 0f;
        _dirty = false;
        _builtForDay = NightRun.Day;

        _scratch.Clear();
        _seen.Clear();

        IReadOnlyList<RuleSO> deck = NightRun.TodayDeck;
        if (deck != null && deck.Count > 0)
        {
            for (int i = 0; i < deck.Count; i++)
            {
                if (deck[i] != null)
                {
                    deck[i].CollectReferences(_scratch);
                }
            }
        }
        else
        {
            // 덱이 없다(시험 씬·밤 시작 전). 등록부 전체로 물러선다.
            foreach (string id in JudgeTargetRegistry.Snapshot())
            {
                _scratch.Add(id);
            }
        }

        _ids.Clear();
        _anchors.Clear();
        _wasInRange.Clear();

        for (int i = 0; i < _scratch.Count; i++)
        {
            string id = _scratch[i];
            if (string.IsNullOrEmpty(id) || !_seen.Add(id))
            {
                continue;
            }

            JudgeTarget target;
            if (!JudgeTargetRegistry.TryGet(id, out target) || target == null)
            {
                continue;   // 씬에 없는 ID는 코어가 이미 「미판정 + 경고」로 처리한다.
            }

            _ids.Add(id);
            _anchors.Add(target);
            _wasInRange.Add(false);
        }
    }
}

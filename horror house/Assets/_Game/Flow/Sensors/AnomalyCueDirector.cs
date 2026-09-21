using System;
using System.Collections.Generic;
using NightDuty;
using UnityEngine;

/// <summary>
/// 이상현상 큐 발신기. <see cref="SpaceAnomalyTableSO"/>(공간 × 축 × 구간)를 읽어 이번 방문에 재생할 큐 목록을 정하고,
/// <see cref="CueBindingTableSO"/>가 지정한 시점에 <b>음원 없이 판정 신호만</b> 보낸다.
///
/// <para><b>지금 이 컴포넌트가 하는 일은 「소리 없는 단서」다.</b> 오디오는 아직 없다(<see cref="cueAudio"/> 슬롯만 비워 둔다).
/// 나중에 클립이 들어오면 <c>PlayOneShot</c>을 이 파일의 표시한 자리에 넣고, 클립 길이를 바인딩의
/// <c>SequenceSeconds</c>에 그대로 적으면 된다(CLAUDE.md §5.4-16). <b>판정은 그대로, 소리만 나중에 꽂힌다.</b></para>
///
/// <list type="bullet">
/// <item><b>큐와 판정 ID를 합치지 않는다.</b> 표의 <c>chalk.3</c>은 카드의 <c>cls11.chalk3</c>이 아니다.
/// 둘을 잇는 것은 <see cref="CueBindingTableSO"/>뿐이고, <c>Send == None</c>인 줄은 소리는 나되 카드는 건드리지 않는다(§2.7).</item>
/// <item><b>이번 방문의 큐 목록은 입장 때 얼려 둔다</b>(§2.5-6). 방문 중에 구간이 바뀌어도 목록을 바꾸지 않고,
/// 바뀐 구간은 <b>다음 방문</b>부터 쓴다.</item>
/// <item><b>같은 틱에 두 개 이상 보내지 않는다.</b> 여러 큐가 같은 순간에 걸리면 표의 <c>Cues</c> 배열 순서
/// (= 기획서가 적은 재생 순서)대로 줄을 세워 한 틱에 하나씩 보낸다. 한꺼번에 쏘면 「한 방문에 신규 단기 사건 하나」
/// (§2.5-7)의 주인을 <b>덱 순서</b>가 정해 버려, 플레이어가 실제로 들은 단서와 판정된 카드가 어긋난다.</item>
/// <item><b>레이를 다시 쏘지 않는다.</b> 식별 0.2초는 <c>PlayerSensors.Active.Gaze.CurrentId</c>를 읽어서 센다.</item>
/// <item><b>자체 <c>Update</c>로 0.1초를 세지 않는다.</b> 틱은 <c>PlayerSensors</c> 허브에서 받는다
/// (같은 순간의 신호 순서를 보장하기 위해서다. CLAUDE.md §4.4.1).</item>
/// <item><b>문은 건드리지 않는다.</b> 복도 <c>door.auto_open</c>·화장실 <c>stall.entry.auto_open</c>의
/// <c>DoorAutoOpenObserved</c>는 <c>DoorRelay</c>가 보낸다. 이 발신기는 연출 개방을 <b>예약</b>할 때
/// <c>DoorRelay.BeginDirectionMove()</c>를 먼저 불러 출처가 <c>Direction</c>으로 기록되게만 한다.</item>
/// </list>
///
/// <para><b>구간을 어디서 읽는가.</b> <c>NightRun</c>은 「공간별 표시 구간」을 공개하지 않는다
/// (<c>BandResolver.GetShown</c>은 <c>NightRun</c> 내부에 있다). 공개된 유일한 통로가 <see cref="EventBus.BandChanged"/>이고,
/// 이 이벤트는 <c>BandResolver</c>의 <b>보류</b>(진행 중 단기 사건이 있는 공간은 구간 반영을 미룬다)가 이미 반영된 값이라
/// §2.5-6이 요구하는 값과 정확히 같다. 그래서 구독한다. <c>from == to</c> 재방송이 오므로 처리는 멱등하다(§5.2-5).
/// <b>구독은 <c>OnDisable</c>에서 반드시 푼다</b>(§5.2-6).</para>
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(70)]
public sealed class AnomalyCueDirector : MonoBehaviour
{
    [Header("표")]
    [Tooltip("공간별 이상현상 표. 비우면 Resources의 SpaceAnomalyTable을 찾는다.")]
    [SerializeField] private SpaceAnomalyTableSO anomalyTable;

    [Tooltip("큐 → 판정 신호 바인딩 표. 비우면 Resources의 CueBindingTable을 찾는다.")]
    [SerializeField] private CueBindingTableSO bindingTable;

    [Tooltip("조도 표. 조도 단서의 「켜진 등 개수」 정합 검사에 쓴다(§2.4). 비우면 Resources의 BandTable을 찾는다.")]
    [SerializeField] private BandTableSO bandTable;

    [Header("오디오 — 지금은 비워 둔다")]
    [Tooltip("큐 음원 재생기. 음원이 발주되면 여기에 꽂는다. 비어 있어도 판정은 그대로 동작한다.")]
    [SerializeField] private AudioSource cueAudio;

    [Header("디버그")]
    [Tooltip("켜면 큐 목록·발신·보류를 콘솔에 남긴다. 플레이 화면에는 아무것도 표시하지 않는다(§2.8).")]
    [SerializeField] private bool logCues;

    /// <summary>축 개수(청각·조도·배치·신뢰).</summary>
    private const int AxisCount = 4;

    /// <summary>공간 열거자의 최대값 + 1.</summary>
    private const int SpaceSlots = 6;

    // 공간 × 축의 현재 표시 구간. BandChanged로만 채운다.
    private readonly Band[,] _shownBand = new Band[SpaceSlots, AxisCount];
    private readonly bool[,] _bandKnown = new bool[SpaceSlots, AxisCount];

    // 이번 방문에 얼려 둔 것들.
    private SpaceId _visitSpace = SpaceId.None;
    private readonly Band[] _visitBand = new Band[AxisCount];
    private readonly List<string> _visitCues = new List<string>();
    private readonly List<CueBindingTableSO.Binding> _armed = new List<CueBindingTableSO.Binding>();
    private readonly HashSet<string> _firedThisVisit = new HashSet<string>(StringComparer.Ordinal);
    private readonly HashSet<string> _firedThisNight = new HashSet<string>(StringComparer.Ordinal);

    // 그날 공간별 방문·점검 횟수. 「별도 방문」 판정에 쓴다.
    private readonly int[] _visitCount = new int[SpaceSlots];
    private readonly int[] _inspectCount = new int[SpaceSlots];

    // 발신 대기열과 시퀀스.
    private readonly List<Pending> _pending = new List<Pending>();
    private readonly List<CueBindingTableSO.Binding> _scratch = new List<CueBindingTableSO.Binding>();
    private readonly List<string> _crossCues = new List<string>();

    // 식별 누적(대상 ID → 연속 응시 시간). 바인딩마다 따로 센다.
    private readonly List<GazeWatch> _gazeWatches = new List<GazeWatch>();

    private readonly List<SpaceLights> _lights = new List<SpaceLights>();

    private int _lastDay = -1;
    private int _immediateThisMoment;
    private bool _tapSeen;
    private bool _tapWarned;
    private SpaceId _polledSpace = SpaceId.None;

    private static AnomalyCueDirector s_active;

    /// <summary>지금 살아 있는 발신기. 없으면 null.</summary>
    public static AnomalyCueDirector Active
    {
        get { return s_active; }
    }

    /// <summary>이번 방문에 재생하기로 얼려 둔 큐 목록(디버그·검수용).</summary>
    public IReadOnlyList<string> VisitCues
    {
        get { return _visitCues; }
    }

    // ────────────────────────────────────────────────────────────────────────
    // 수명
    // ────────────────────────────────────────────────────────────────────────

    private void OnEnable()
    {
        s_active = this;

        if (anomalyTable == null) anomalyTable = Resources.Load<SpaceAnomalyTableSO>("SpaceAnomalyTable");
        if (bindingTable == null) bindingTable = Resources.Load<CueBindingTableSO>("CueBindingTable");
        if (bandTable == null) bandTable = Resources.Load<BandTableSO>("BandTable");

        EventBus.BandChanged += OnBandChanged;

        // 허브의 0.1초 누산 틱. 자체 Update로 세지 않는다(§4.4.1의 「같은 순간 순서」).
        PlayerSensors.Sampled += OnSensorTick;

        CacheLights();
    }

    private void OnDisable()
    {
        // static 이벤트는 씬을 바꿔도 살아남는다. 반드시 푼다(CLAUDE.md §5.2-6).
        EventBus.BandChanged -= OnBandChanged;
        PlayerSensors.Sampled -= OnSensorTick;

        if (s_active == this)
        {
            s_active = null;
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // 들어오는 것 — 구간 · 신호 · 틱
    // ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 구간 변화. <b>이번 방문의 큐 목록은 바꾸지 않는다</b>(§2.5-6). 다음 방문부터 반영된다.
    /// <c>from == to</c> 재방송이 와도 결과가 같다(§5.2-5).
    /// </summary>
    private void OnBandChanged(SpaceId space, FearAxis axis, Band from, Band to)
    {
        int s = (int)space;
        int a = (int)axis;
        if (s < 0 || s >= SpaceSlots || a < 0 || a >= AxisCount)
        {
            return;
        }

        _shownBand[s, a] = to;
        _bandKnown[s, a] = true;

        if (logCues && space == _visitSpace && from != to)
        {
            Debug.Log("[Cue] " + space + "/" + axis + " 구간 " + from + "→" + to +
                      " — 이번 방문의 큐 목록은 그대로 둔다(§2.5-6).", this);
        }
    }

    /// <summary>
    /// 신호 탭. <c>SpaceZones</c>가 보내는 공간·구역·점검 신호를 그대로 넘겨받는다.
    /// <b>배선은 README 1절 참조</b> — 아직 연결되지 않았으면 공간 진입·퇴실만 폴링으로 대신한다.
    /// </summary>
    public static void Observe(in JudgeSignal signal)
    {
        AnomalyCueDirector d = s_active;
        if (d != null)
        {
            d.OnSignal(signal);
        }
    }

    private void OnSignal(in JudgeSignal signal)
    {
        if (!CanSend())
        {
            return;
        }

        _tapSeen = true;

        switch (signal.Kind)
        {
            case SignalKind.SpaceEntered:
                BeginVisit(signal.Space);
                FireMoment(CueMoment.OnSpaceEntered, string.Empty);
                break;

            case SignalKind.SpaceExited:
                FireMoment(CueMoment.OnSpaceExited, string.Empty);
                EndVisit();
                break;

            case SignalKind.InspectionCompleted:
                if (signal.Space != SpaceId.None)
                {
                    _inspectCount[(int)signal.Space]++;
                }

                // 신호 순서는 점검 완료 → 공간 이탈이다(§4.4.1). 「점검 후 퇴실할 때」는
                // 아직 공간·청취 구역 안인 이 시점에 쏜다. SpaceExited 뒤에 쏘면 전달이 성립하지 않는다.
                FireMoment(CueMoment.OnInspectionCompleted, string.Empty);
                break;

            case SignalKind.ZoneEntered:
                FireMoment(CueMoment.OnZoneEntered, signal.TargetId);
                FireMoment(CueMoment.OnPassageEntered, signal.TargetId);
                FireZoneCuesOfOtherSpaces(signal.TargetId);
                break;

            case SignalKind.PassageCompleted:
                FireMoment(CueMoment.OnPassageCompleted, signal.TargetId);
                break;

            case SignalKind.ZoneExited:
                CancelZone(signal.TargetId);
                break;
        }
    }

    /// <summary>
    /// 허브의 0.1초 샘플. 대기열·시퀀스·식별을 한 걸음 진행한다.
    /// <b>이 메서드에서 프레임 시간을 다시 재지 않는다</b> — 허브가 넘겨준 고정 간격만 쓴다.
    /// </summary>
    private void OnSensorTick(float stepSeconds)
    {
        if (!CanSend())
        {
            _pending.Clear();
            _gazeWatches.Clear();
            return;
        }

        if (_lastDay != NightRun.Day)
        {
            ResetForNewNight();
        }

        // 탭이 아직 배선되지 않았으면 코어가 보는 현재 공간으로 진입·퇴실만 대신 잡는다.
        PollSpaceIfNoTap();

        StepPending(stepSeconds);
        StepGaze(stepSeconds);
    }

    // ────────────────────────────────────────────────────────────────────────
    // 방문
    // ────────────────────────────────────────────────────────────────────────

    private void BeginVisit(SpaceId space)
    {
        if (space == SpaceId.None)
        {
            return;
        }

        EndVisit();

        _visitSpace = space;
        _visitCount[(int)space]++;
        _firedThisVisit.Clear();
        _visitCues.Clear();
        _armed.Clear();

        // ① 이번 방문의 구간을 얼린다.
        for (int a = 0; a < AxisCount; a++)
        {
            _visitBand[a] = _bandKnown[(int)space, a] ? _shownBand[(int)space, a] : Band.Band0;
            if (!_bandKnown[(int)space, a] && logCues)
            {
                Debug.LogWarning("[Cue] " + space + "/" + (FearAxis)a +
                                 " 구간을 아직 받지 못해 Band0으로 본다. BandChanged 재방송 전에 입장했는가?", this);
            }
        }

        // ② 이번 방문의 큐 목록을 얼린다. 표의 배열 순서 = 기획서가 적은 재생 순서다.
        AppendCues(space, FearAxis.Auditory, _visitBand[(int)FearAxis.Auditory], _visitCues);
        AppendCues(space, FearAxis.Layout, _visitBand[(int)FearAxis.Layout], _visitCues);

        // ③ 이번 방문에 무장할 바인딩을 고른다.
        if (bindingTable != null)
        {
            _scratch.Clear();
            bindingTable.CollectFor(space, _scratch);
            for (int i = 0; i < _scratch.Count; i++)
            {
                CueBindingTableSO.Binding b = _scratch[i];
                if (IsArmedFor(b, _visitCues, _visitBand[(int)b.Axis], space))
                {
                    _armed.Add(b);
                }
            }

            _armed.Sort(CompareByCueOrder);
        }

        if (logCues)
        {
            Debug.Log("[Cue] " + space + " 방문 " + _visitCount[(int)space] + "회차 · 구간 청각=" +
                      _visitBand[(int)FearAxis.Auditory] + " 조도=" + _visitBand[(int)FearAxis.Illuminance] +
                      " 배치=" + _visitBand[(int)FearAxis.Layout] + " · 큐 [" + string.Join(", ", _visitCues) +
                      "] · 무장 " + _armed.Count + "줄", this);
        }

        // ④ 상시 식별 큐를 건다.
        for (int i = 0; i < _armed.Count; i++)
        {
            if (_armed[i].Moment == CueMoment.WhileInSpace)
            {
                TryFire(_armed[i], string.Empty);
            }
        }
    }

    private void EndVisit()
    {
        // 나가는 공간의 대기분만 버린다. 「문밖에서 듣는 다른 공간의 단서」(chalk.3)는 복도를 나가도 살아 있어야 한다 —
        // 그쪽은 청취 구역 이탈(ZoneExited)이 취소한다.
        // 다만 이미 때가 된 것은 버리기 전에 내보낸다. 「점검 후 퇴실할 때」(C3의 desk.hit, S2의 glass.break)는
        // 점검 완료와 공간 이탈이 <b>같은 프레임</b>에 연달아 오므로(§4.4.1의 신호 순서),
        // 그냥 버리면 그 두 카드의 단서가 영원히 전달되지 않는다.
        SpaceId leaving = SpaceAnomalyTableSO.Group(_visitSpace);
        for (int i = 0; i < _pending.Count; i++)
        {
            if (_pending[i].Binding.Space == leaving && _pending[i].Remaining <= 0f)
            {
                Fire(_pending[i]);
            }
        }

        for (int i = _pending.Count - 1; i >= 0; i--)
        {
            if (_pending[i].Binding.Space == leaving)
            {
                _pending.RemoveAt(i);
            }
        }

        // 식별은 그 공간 안에서만 성립한다. 나가면 전부 버린다.
        _gazeWatches.Clear();

        _visitSpace = SpaceId.None;
        _visitCues.Clear();
        _armed.Clear();
    }

    private void AppendCues(SpaceId space, FearAxis axis, Band band, List<string> into)
    {
        if (anomalyTable == null)
        {
            return;
        }

        IReadOnlyList<string> cues = anomalyTable.CuesFor(space, axis, band);
        for (int i = 0; i < cues.Count; i++)
        {
            if (!string.IsNullOrEmpty(cues[i]) && !into.Contains(cues[i]))
            {
                into.Add(cues[i]);
            }
        }
    }

    /// <summary>지금 이 공간·축의 표시 구간. 아직 받지 못했으면 Band0.</summary>
    private Band BandOf(SpaceId space, FearAxis axis)
    {
        int s = (int)space;
        int a = (int)axis;
        return s >= 0 && s < SpaceSlots && _bandKnown[s, a] ? _shownBand[s, a] : Band.Band0;
    }

    /// <summary>
    /// 이 바인딩이 무장하는가.
    /// <paramref name="cues"/>는 대상 공간의 큐 목록, <paramref name="axisBand"/>는 그 공간·축의 구간,
    /// <paramref name="space"/>는 방문 횟수를 셀 <b>실제</b> 공간이다(교실은 1-1과 1-3을 따로 센다).
    /// </summary>
    private bool IsArmedFor(CueBindingTableSO.Binding b, List<string> cues, Band axisBand, SpaceId space)
    {
        if (b == null || b.Moment == CueMoment.None || !b.Sends || space == SpaceId.None)
        {
            return false;   // 연출 전용 줄은 신호를 보내지 않는다(§2.7).
        }

        // 구간 자격.
        if (b.FromAnomalyTable)
        {
            if (!cues.Contains(b.CueId))
            {
                return false;
            }
        }
        else
        {
            if (axisBand < b.BandFrom || axisBand > b.BandTo)
            {
                return false;
            }
        }

        // 함께 있어야 하는 큐(T6: 칸 두 개가 모두 열린 구간).
        if (b.RequiresCues != null)
        {
            for (int i = 0; i < b.RequiresCues.Length; i++)
            {
                if (!string.IsNullOrEmpty(b.RequiresCues[i]) && !cues.Contains(b.RequiresCues[i]))
                {
                    return false;
                }
            }
        }

        // 방문 조건.
        switch (b.Visit)
        {
            case CueVisit.FirstVisitOfNight:
                if (_visitCount[(int)space] != 1) return false;
                break;

            case CueVisit.SeparateVisit:
                // 「별도 방문」 잠정 해석: 그날 그 공간의 일반 점검을 한 번 이상 마친 뒤의 새 방문.
                // 근거와 미확정 사항은 README 4절. 기획 확인 대기.
                if (_inspectCount[(int)space] < 1) return false;
                break;
        }

        // 반복 제한.
        if (b.Repeat == CueRepeat.OncePerNight && _firedThisNight.Contains(Key(b)))
        {
            return false;
        }

        return true;
    }

    private int CompareByCueOrder(CueBindingTableSO.Binding a, CueBindingTableSO.Binding b)
    {
        int ia = _visitCues.IndexOf(a.CueId);
        int ib = _visitCues.IndexOf(b.CueId);
        if (ia < 0) ia = int.MaxValue - 1;   // 표 밖의 자체 키(조도)는 뒤로.
        if (ib < 0) ib = int.MaxValue - 1;
        return ia != ib ? ia.CompareTo(ib) : string.CompareOrdinal(a.CueId, b.CueId);
    }

    // ────────────────────────────────────────────────────────────────────────
    // 발동
    // ────────────────────────────────────────────────────────────────────────

    private void FireMoment(CueMoment moment, string zoneId)
    {
        _immediateThisMoment = 0;

        for (int i = 0; i < _armed.Count; i++)
        {
            CueBindingTableSO.Binding b = _armed[i];
            if (b.Moment != moment)
            {
                continue;
            }

            if (!string.IsNullOrEmpty(b.ZoneId) && b.ZoneId != zoneId &&
                (moment == CueMoment.OnZoneEntered || moment == CueMoment.OnPassageEntered ||
                 moment == CueMoment.OnPassageCompleted))
            {
                continue;
            }

            TryFire(b, zoneId);
        }

        if (_immediateThisMoment > 1)
        {
            // 같은 순간에 단서가 둘 이상 나가면 「한 방문에 신규 단기 사건 하나」(§2.5-7)의 주인을
            // 플레이어가 들은 순서가 아니라 <b>그날 덱 순서</b>가 정하게 된다.
            Debug.LogWarning("[Cue] " + moment + "에 단서 " + _immediateThisMoment +
                             "개가 같은 순간에 나갔습니다. 어느 카드가 시작될지는 덱 순서가 정합니다 — " +
                             "바인딩 표에서 한쪽에 DelaySeconds를 주어 순서를 명시하십시오(§2.5-7).", this);
        }
    }

    /// <summary>
    /// <b>문밖에서 듣는 다른 공간의 단서.</b> 기획서 H절 「1-1 문밖에서 분필 3획」이 그것이고,
    /// 공통 명세도 「카드 공간과 현재 공간이 다른 경우」를 인정한다(§2.5-7).
    /// 현재 방문 공간의 무장 목록(<c>_armed</c>)에는 들어 있지 않으므로 구역 진입 때 따로 본다.
    /// 이때 구간은 얼린 값이 아니라 <b>그 공간의 현재 표시 구간</b>을 쓴다(그 공간을 방문 중이 아니므로 얼릴 것이 없다).
    /// </summary>
    private void FireZoneCuesOfOtherSpaces(string zoneId)
    {
        if (bindingTable == null || string.IsNullOrEmpty(zoneId))
        {
            return;
        }

        IReadOnlyList<CueBindingTableSO.Binding> all = bindingTable.Bindings;
        for (int i = 0; i < all.Count; i++)
        {
            CueBindingTableSO.Binding b = all[i];
            if (b == null || b.Moment != CueMoment.OnZoneEntered || b.ZoneId != zoneId || !b.Sends)
            {
                continue;
            }

            if (b.Space == SpaceAnomalyTableSO.Group(_visitSpace))
            {
                continue;   // 현재 방문 공간의 줄은 FireMoment가 이미 봤다.
            }

            _crossCues.Clear();
            AppendCues(b.Space, FearAxis.Auditory, BandOf(b.Space, FearAxis.Auditory), _crossCues);
            AppendCues(b.Space, FearAxis.Layout, BandOf(b.Space, FearAxis.Layout), _crossCues);

            if (IsArmedFor(b, _crossCues, BandOf(b.Space, b.Axis), b.Space))
            {
                TryFire(b, zoneId);
            }
        }
    }

    private void TryFire(CueBindingTableSO.Binding b, string zoneId)
    {
        string key = Key(b);
        if (_firedThisVisit.Contains(key))
        {
            return;
        }

        if (b.Repeat == CueRepeat.OncePerNight && _firedThisNight.Contains(key))
        {
            return;
        }

        if (b.Moment == CueMoment.OnZoneEntered && string.IsNullOrEmpty(b.ZoneId))
        {
            WarnOnce(b, "구역 진입 시점인데 구역 ID가 비어 있다. 씬에 청취 구역을 추가할 때까지 발신하지 않는다.");
            return;
        }

        // 조도값과 실제로 켜진 등 개수가 어긋나면 사건을 시작하지 않고 개발 로그에 남긴다(CLAUDE.md §2.4).
        if (b.RequireLitCountMatch && !LitCountMatches(b))
        {
            return;
        }

        _firedThisVisit.Add(key);
        _firedThisNight.Add(key);

        if (b.UsesGaze)
        {
            ArmGaze(b);
            return;
        }

        float wait = Mathf.Max(0f, b.DelaySeconds) + ShotSpan(b);
        Pending p = new Pending(b, wait, zoneId);

        if (wait <= 0f)
        {
            // 지연이 없는 줄은 <b>그 자리에서</b> 보낸다. 「점검 후 퇴실할 때」는 점검 완료와 공간 이탈이
            // 같은 프레임에 연달아 오므로(§4.4.1), 한 틱이라도 미루면 단서가 SpaceExited <b>뒤</b>에 도착해
            // C3·S2의 성공 조건(퇴실)이 이미 지나가 버린다.
            _immediateThisMoment++;
            Fire(p);
            return;
        }

        // 지연이 있는 줄은 대기열로. 대기열은 한 틱에 하나만 내보낸다(§2.5-7의 덱 순서 문제).
        _pending.Add(p);

        // ── 음원이 오면 여기서 PlayOneShot. 단발 ShotCount회를 ShotIntervalSeconds 간격으로 예약하고,
        //    마지막 획이 끝난 뒤에 아래 Send가 일어나게만 하면 된다. 판정은 바뀌지 않는다. ──
        if (logCues)
        {
            Debug.Log("[Cue] 예약 " + b.CueId + " → " + b.Send + "('" + b.JudgeId + "') " +
                      (b.ShotCount > 1 ? "단발 " + b.ShotCount + "회 " : string.Empty) + "지연 " +
                      (Mathf.Max(0f, b.DelaySeconds) + ShotSpan(b)).ToString("F2") + "초", this);
        }
    }

    /// <summary>단발 N회를 다 재생하는 데 걸리는 시간. C1은 세 획 <b>전체</b>가 전달돼야 활성화된다(§5.4-15).</summary>
    private static float ShotSpan(CueBindingTableSO.Binding b)
    {
        return b.ShotCount > 1 ? (b.ShotCount - 1) * Mathf.Max(0f, b.ShotIntervalSeconds) : 0f;
    }

    private void StepPending(float step)
    {
        for (int i = _pending.Count - 1; i >= 0; i--)
        {
            Pending p = _pending[i];
            p.Remaining -= step;
            _pending[i] = p;

            if (p.Remaining > 0f)
            {
                continue;
            }

            _pending.RemoveAt(i);
            Fire(p);

            // 한 틱에 하나만 내보낸다(§2.5-7의 덱 순서 문제).
            break;
        }
    }

    /// <summary>대기분 하나를 실제로 내보낸다.</summary>
    private void Fire(Pending p)
    {
        if (p.IsSequenceEnd)
        {
            Send(SignalKind.SequenceEnded, p.Binding.SequenceId, p.Binding);
            return;
        }

        Send(ToSignalKind(p.Binding.Send), p.Binding.JudgeId, p.Binding);

        // 시퀀스 끝을 따로 보내야 하는 줄(C4의 cls11.lectern.noise)만 예약한다.
        if (!string.IsNullOrEmpty(p.Binding.SequenceId) && p.Binding.SequenceSeconds > 0f)
        {
            _pending.Add(new Pending(p.Binding, p.Binding.SequenceSeconds, p.ZoneId) { IsSequenceEnd = true });
        }
        else if (!string.IsNullOrEmpty(p.Binding.SequenceId))
        {
            WarnOnce(p.Binding, "시퀀스 길이가 0이라 SequenceEnded를 보내지 않는다(음원 발주 대기).");
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // 식별 0.2초 — 레이는 GazeProbe가 이미 쏜다. 우리는 그 결과만 센다.
    // ────────────────────────────────────────────────────────────────────────

    private void ArmGaze(CueBindingTableSO.Binding b)
    {
        if (b.IdentifyTargetIds == null || b.IdentifyTargetIds.Length == 0)
        {
            WarnOnce(b, "식별 전달인데 식별 대상이 비어 있다.");
            return;
        }

        _gazeWatches.Add(new GazeWatch(b));

        if (logCues)
        {
            Debug.Log("[Cue] 식별 감시 " + b.CueId + " → ClueIdentified('" + b.JudgeId + "') 대상 [" +
                      string.Join(", ", b.IdentifyTargetIds) + "] " + b.IdentifySeconds.ToString("F2") + "초", this);
        }
    }

    private void StepGaze(float step)
    {
        if (_gazeWatches.Count == 0)
        {
            return;
        }

        PlayerSensors hub = PlayerSensors.Active;
        string looking = hub != null && hub.Gaze != null ? hub.Gaze.CurrentId : string.Empty;

        for (int i = _gazeWatches.Count - 1; i >= 0; i--)
        {
            GazeWatch w = _gazeWatches[i];
            CueBindingTableSO.Binding b = w.Binding;

            for (int t = 0; t < w.Held.Length; t++)
            {
                // 대상이 바뀌거나 가려지면 연속 시간은 0이다(공통 명세 2절).
                w.Held[t] = b.IdentifyTargetIds[t] == looking && looking.Length > 0 ? w.Held[t] + step : 0f;
            }

            int met;
            if (!w.IsMet(b, out met))
            {
                continue;
            }

            // 조도 단서는 발신 직전에 한 번 더 등 개수를 본다(응시하는 동안 구간이 바뀔 수 있다).
            if (b.RequireLitCountMatch && !LitCountMatches(b))
            {
                _gazeWatches.RemoveAt(i);
                continue;
            }

            _gazeWatches.RemoveAt(i);

            // 한 카드가 공간마다 다른 대상을 쓰면(C5: cls11.lights / cls13.lights)
            // 실제로 식별한 대상 ID를 그대로 보낸다.
            string sendId = b.SendIdentifiedTargetId && met >= 0 ? b.IdentifyTargetIds[met] : b.JudgeId;
            Send(SignalKind.ClueIdentified, sendId, b);
            break;   // 한 틱에 하나만.
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // 발신과 검사
    // ────────────────────────────────────────────────────────────────────────

    private void Send(SignalKind kind, string targetId, CueBindingTableSO.Binding b)
    {
        if (kind == SignalKind.None || string.IsNullOrEmpty(targetId) || !CanSend())
        {
            return;
        }

        NightRun.Send(JudgeSignal.Target(kind, targetId));

        if (logCues)
        {
            Debug.Log("[Cue] 발신 " + kind + "('" + targetId + "')  ← 큐 " + b.CueId +
                      (b.Cards != null && b.Cards.Length > 0 ? "  (카드 " + string.Join("·", b.Cards) + ")" : string.Empty), this);
        }
    }

    /// <summary>포획 이후·Tab 중·밤이 아닐 때는 아무것도 보내지 않는다.</summary>
    private static bool CanSend()
    {
        return NightRun.IsNightActive && !NightRun.IsCaptured && !PlayerSensors.TabOpen;
    }

    /// <summary>
    /// 조도값(구간)과 <b>실제로 켜진 등 개수</b>가 맞는지 본다. 어긋나면 사건을 시작하지 않고 개발 로그에 남긴다(§2.4).
    /// </summary>
    private bool LitCountMatches(CueBindingTableSO.Binding b)
    {
        if (bandTable == null)
        {
            WarnOnce(b, "조도 표가 없어 등 개수를 검사할 수 없다.");
            return false;
        }

        // 조도 단서는 전부 WhileInSpace라 방문 중 공간이 곧 대상 공간이다. 방문 밖이면 바인딩의 공간으로 물러선다.
        SpaceId space = _visitSpace != SpaceId.None ? _visitSpace : b.Space;
        Band band = _visitSpace != SpaceId.None
            ? _visitBand[(int)FearAxis.Illuminance]
            : BandOf(space, FearAxis.Illuminance);

        SpaceLights group = FindLights(space);
        if (group == null)
        {
            WarnOnce(b, space + "의 SpaceLights를 찾지 못해 등 개수를 검사할 수 없다.");
            return false;
        }

        int expected = bandTable.LitCountFor(space, band);
        if (group.LitCount == expected)
        {
            return true;
        }

        Debug.LogWarning("[Cue] " + space + " 조도 " + band + "의 기대 등 개수 " + expected +
                         "와 실제 " + group.LitCount + "가 어긋나 큐 " + b.CueId +
                         "를 시작하지 않는다(CLAUDE.md §2.4).", this);
        return false;
    }

    private SpaceLights FindLights(SpaceId space)
    {
        for (int i = 0; i < _lights.Count; i++)
        {
            if (_lights[i] != null && _lights[i].Space == space)
            {
                return _lights[i];
            }
        }

        CacheLights();

        for (int i = 0; i < _lights.Count; i++)
        {
            if (_lights[i] != null && _lights[i].Space == space)
            {
                return _lights[i];
            }
        }

        return null;
    }

    private void CacheLights()
    {
        _lights.Clear();
        _lights.AddRange(FindObjectsByType<SpaceLights>(FindObjectsInactive.Exclude, FindObjectsSortMode.None));
    }

    private void CancelZone(string zoneId)
    {
        if (string.IsNullOrEmpty(zoneId))
        {
            return;
        }

        // 청취 구역을 벗어나면 그 구역에 매인 전달은 취소한다(공통 명세 2절 「청각 단서 전달」).
        for (int i = _pending.Count - 1; i >= 0; i--)
        {
            if (_pending[i].Binding.Delivery == CueDelivery.InListenZone && _pending[i].ZoneId == zoneId)
            {
                if (logCues)
                {
                    Debug.Log("[Cue] 청취 구역 " + zoneId + " 이탈 — " + _pending[i].Binding.CueId + " 전달 취소", this);
                }

                _pending.RemoveAt(i);
            }
        }
    }

    private void PollSpaceIfNoTap()
    {
        if (_tapSeen)
        {
            return;
        }

        RuleBook book = NightRun.CurrentBook;
        SpaceId now = book != null ? book.World.CurrentSpace : SpaceId.None;
        if (now == _polledSpace)
        {
            return;
        }

        if (!_tapWarned)
        {
            _tapWarned = true;
            Debug.LogWarning("[Cue] 신호 탭이 배선되지 않았습니다. 공간 진입·퇴실만 폴링으로 대신합니다 — " +
                             "점검 완료·구역 진입·통행 시점의 큐는 발신되지 않습니다. README 1절을 보십시오.", this);
        }

        if (_polledSpace != SpaceId.None)
        {
            EndVisit();
        }

        _polledSpace = now;

        if (now != SpaceId.None)
        {
            BeginVisit(now);
            FireMoment(CueMoment.OnSpaceEntered, string.Empty);
        }
    }

    private void ResetForNewNight()
    {
        _lastDay = NightRun.Day;
        _firedThisNight.Clear();
        Array.Clear(_visitCount, 0, _visitCount.Length);
        Array.Clear(_inspectCount, 0, _inspectCount.Length);
        _polledSpace = SpaceId.None;
        _tapSeen = false;
        _tapWarned = false;
        EndVisit();
        CacheLights();
    }

    private static SignalKind ToSignalKind(CueSend send)
    {
        switch (send)
        {
            case CueSend.ClueDelivered: return SignalKind.ClueDelivered;
            case CueSend.ClueIdentified: return SignalKind.ClueIdentified;
            case CueSend.SequenceEnded: return SignalKind.SequenceEnded;
            default: return SignalKind.None;
        }
    }

    /// <summary>
    /// 반복 제한의 열쇠. <b>현재 공간이 아니라 바인딩의 공간</b>을 쓴다 —
    /// 교실 단서는 복도에서 듣는 일이 있어서(§2.5-7의 「카드 공간과 현재 공간이 다른 경우」)
    /// 현재 공간으로 세면 「하루 1회」가 방마다 한 번씩으로 늘어난다.
    /// </summary>
    private static string Key(CueBindingTableSO.Binding b)
    {
        return b.Space + "|" + b.CueId;
    }

    private void WarnOnce(CueBindingTableSO.Binding b, string message)
    {
        string key = "warn|" + Key(b) + "|" + message;
        if (_firedThisNight.Add(key))
        {
            Debug.LogWarning("[Cue] " + b.CueId + ": " + message, this);
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // 내부 자료형
    // ────────────────────────────────────────────────────────────────────────

    private struct Pending
    {
        public readonly CueBindingTableSO.Binding Binding;
        public readonly string ZoneId;
        public float Remaining;
        public bool IsSequenceEnd;

        public Pending(CueBindingTableSO.Binding binding, float remaining, string zoneId)
        {
            Binding = binding;
            Remaining = remaining;
            ZoneId = zoneId ?? string.Empty;
            IsSequenceEnd = false;
        }
    }

    private sealed class GazeWatch
    {
        public readonly CueBindingTableSO.Binding Binding;
        public readonly float[] Held;

        public GazeWatch(CueBindingTableSO.Binding binding)
        {
            Binding = binding;
            Held = new float[binding.IdentifyTargetIds.Length];
        }

        /// <summary>성립했는지. <paramref name="metIndex"/>는 성립시킨 대상의 자리(모두 조건이면 마지막 자리).</summary>
        public bool IsMet(CueBindingTableSO.Binding b, out int metIndex)
        {
            metIndex = -1;
            bool all = true;
            for (int i = 0; i < Held.Length; i++)
            {
                bool one = Held[i] >= b.IdentifySeconds;
                if (one && b.Delivery == CueDelivery.GazeIdentifyAny)
                {
                    metIndex = i;
                    return true;
                }

                all &= one;
            }

            if (b.Delivery == CueDelivery.GazeIdentifyAll && all && Held.Length > 0)
            {
                metIndex = Held.Length - 1;
                return true;
            }

            return false;
        }
    }
}

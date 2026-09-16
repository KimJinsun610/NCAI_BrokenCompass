using System;
using System.Collections.Generic;
using UnityEngine;

namespace NightDuty
{
    /// <summary>
    /// 하룻밤의 근무수칙 판정 진입점. 그날 배정된 카드(덱 순서)를 감시하고, 결과를 축에 적용한다.
    /// <para>
    /// 처리 순서(신호 한 건):
    /// <list type="number">
    /// <item>종료 잠금이면 아무것도 하지 않는다.</item>
    /// <item>현재 상태(공간·손전등·Tab)를 갱신한다. Tab이 열려 있는 동안에는 다른 신호를 무시한다.</item>
    /// <item>진행 중 카드가 덱 순서대로 신호를 관찰하고, 정산 결과를 곧바로 축에 적용한다. 100에 닿으면 즉시 멈춘다.</item>
    /// <item>대기 중 카드 중 이 신호로 시작할 수 있는 것을 덱 순서대로 시작한다.
    /// 자격 구간, Tab, 「한 방문에 신규 단기 사건 하나」를 지킨다.</item>
    /// <item>진행 중 단기 사건이 있는 공간은 구간 반영을 보류한다.</item>
    /// </list>
    /// 같은 신호로 시작된 카드는 그 신호를 관찰하지 않고, 같은 신호로 취소된 카드는 그 신호로 다시 시작하지 않는다.
    /// </para>
    /// <para>
    /// 「한 방문에 신규 단기 사건 하나」는 플레이어의 방문 단위다(<see cref="SignalKind.SpaceEntered"/>에서 초기화).
    /// 문밖에서 듣는 교실 단서처럼 카드 공간과 현재 공간이 다른 경우도 같은 방문의 몫을 쓴다.
    /// </para>
    /// <para>
    /// 구간 보류(<see cref="BandResolver"/>)를 쓰려면 호출자가 <c>axes.ValueChanged += bands.OnValueChanged</c>도 연결해야 한다.
    /// </para>
    /// <para>
    /// 아직 없는 것(다음 단계): 조우·문자·역설(P형) 정산, 덱 배정. 밤 종료 순서의 P형 미도달 정산 자리는
    /// <see cref="EndNight"/>에 표시해 두었다.
    /// </para>
    /// </summary>
    public sealed class RuleBook
    {
        private readonly List<RuleWatcher> _watchers = new List<RuleWatcher>();
        private static readonly SpaceId[] PatrolSpaces =
        {
            SpaceId.Corridor, SpaceId.Toilet, SpaceId.Classroom_1_1, SpaceId.Classroom_1_3, SpaceId.ScienceRoom
        };

        private readonly List<RuleResult> _results = new List<RuleResult>();
        private readonly HashSet<RuleWatcher> _observedThisSignal = new HashSet<RuleWatcher>();
        private readonly FearAxisSystem _axes;
        private readonly BandResolver _bands;
        private bool _shortTermStartedThisVisit;
        private bool _nightEnded;

        /// <summary>그날 덱 순서의 감시 목록.</summary>
        public IReadOnlyList<RuleWatcher> Watchers { get { return _watchers; } }

        /// <summary>그날 정산 결과(델타 0인 미판정 포함). 개발 로그·근무 종료 리뷰용.</summary>
        public IReadOnlyList<RuleResult> Results { get { return _results; } }

        /// <summary>판정 공유 상태.</summary>
        public JudgeWorld World { get; }

        /// <summary>카드 하나가 정산됐다(플레이 중 화면에 표시하지 말 것).</summary>
        public event Action<RuleResult> Settled;

        /// <summary>
        /// 하룻밤 판정을 만든다.
        /// </summary>
        /// <param name="deck">그날 배정된 카드, 덱 표시 순서대로.</param>
        /// <param name="axes">회차 누적 축.</param>
        /// <param name="bands">구간 해석기. 없으면 구간 보류를 하지 않는다.</param>
        /// <param name="registeredIds">씬에 등록된 대상 ID. null이면 참조 검사를 건너뛴다.
        /// 누락된 카드는 <b>미판정</b>으로 시작한다(크래시가 아니라).</param>
        public RuleBook(IReadOnlyList<RuleSO> deck, FearAxisSystem axes, BandResolver bands, ICollection<string> registeredIds)
        {
            _axes = axes ?? throw new ArgumentNullException(nameof(axes));
            _bands = bands;
            World = new JudgeWorld(axes);

            if (deck == null)
            {
                return;
            }

            List<string> missing = new List<string>();
            for (int i = 0; i < deck.Count; i++)
            {
                RuleSO card = deck[i];
                if (card == null)
                {
                    continue;
                }

                RuleWatcher watcher = new RuleWatcher(card, _watchers.Count);
                _watchers.Add(watcher);

                if (registeredIds != null)
                {
                    missing.Clear();
                    RuleReferenceCheck.FindMissing(card, registeredIds, missing);
                    if (missing.Count > 0)
                    {
                        string reason = "대상 참조 누락: " + string.Join(", ", missing);
                        watcher.MarkUndetermined(reason);
                        _results.Add(new RuleResult(card.CardId, CardState.Undetermined, FearAxis.Trust, 0, card.Space, reason));
                        Debug.LogWarning("[RuleBook] " + card.CardId + " " + reason);
                    }
                }
            }
        }

        /// <summary>판정 입력 신호 하나를 처리한다.</summary>
        public void Dispatch(in JudgeSignal signal)
        {
            if (_nightEnded)
            {
                return;
            }

            if (_axes.IsLocked)
            {
                LockAll();
                return;
            }

            if (signal.Kind == SignalKind.TabChanged)
            {
                World.Apply(signal);
                return;
            }

            if (World.TabOpen)
            {
                // Tab 중에는 이동·조작·시계·판정 시간이 모두 멈춘다. 들어온 신호는 상태에도 판정에도 쓰지 않는다.
                return;
            }

            World.Apply(signal);

            if (signal.Kind == SignalKind.SpaceEntered)
            {
                _shortTermStartedThisVisit = false;
            }

            // 1) 진행 중 카드 관찰. 이 신호를 본 카드는 표시해 두고 2)에서 같은 신호로 다시 시작하지 않는다.
            _observedThisSignal.Clear();
            for (int i = 0; i < _watchers.Count; i++)
            {
                RuleWatcher w = _watchers[i];
                if (w.State != CardState.Active)
                {
                    continue;
                }

                _observedThisSignal.Add(w);
                if (w.Observe(signal, World, out RuleResult result))
                {
                    if (!Commit(result))
                    {
                        return;
                    }
                }
            }

            // 2) 대기 카드 시작.
            for (int i = 0; i < _watchers.Count; i++)
            {
                RuleWatcher w = _watchers[i];
                if (_observedThisSignal.Contains(w) || !w.IsTrigger(signal))
                {
                    continue;
                }

                if (!w.Card.IsEligible(_axes))
                {
                    continue;
                }

                if (!w.Card.IsLongTerm)
                {
                    if (_shortTermStartedThisVisit || HasActiveShortTermIn(w.Card.Space))
                    {
                        continue;
                    }

                    _shortTermStartedThisVisit = true;
                }

                w.Start(World);
            }

            // 3) 구간 반영 보류 갱신.
            UpdateHolds();
        }

        /// <summary>
        /// 근무 종료 요청이 수락된 뒤 호출한다(수락 조건 — 필수 점검과 오늘 조우 완료 — 은 호출자가 확인).
        /// 남은 수칙을 덱 순서로 정산한다. 도중에 100에 닿으면 멈춘다.
        /// </summary>
        public void EndNight()
        {
            if (_nightEnded)
            {
                return;
            }

            _nightEnded = true;

            if (_axes.IsLocked)
            {
                LockAll();
                return;
            }

            for (int i = 0; i < _watchers.Count; i++)
            {
                if (_watchers[i].SettleAtNightEnd(out RuleResult result))
                {
                    if (!Commit(result))
                    {
                        return;
                    }
                }
            }

            // TODO(다음 단계): P형 미도달 정산은 여기, 수칙 정산 뒤에 온다(기획서 공통 명세 1절·4절).

            if (_bands != null)
            {
                ReleaseAllHolds();
            }
        }

        /// <summary>결과를 기록하고 축에 적용한다. 종료 잠금이 걸렸으면 false.</summary>
        private bool Commit(RuleResult result)
        {
            _results.Add(result);

            if (result.HasDelta)
            {
                _axes.Apply(result.Axis, result.Delta, result.CardId, World.CurrentSpace != SpaceId.None ? World.CurrentSpace : result.Space);
            }

            RaiseSettled(result);

            if (_axes.IsLocked)
            {
                LockAll();
                return false;
            }

            return true;
        }

        private bool HasActiveShortTermIn(SpaceId space)
        {
            for (int i = 0; i < _watchers.Count; i++)
            {
                if (_watchers[i].IsActiveShortTerm && _watchers[i].Card.Space == space)
                {
                    return true;
                }
            }

            return false;
        }

        private void UpdateHolds()
        {
            if (_bands == null)
            {
                return;
            }

            for (int i = 0; i < PatrolSpaces.Length; i++)
            {
                _bands.SetHold(PatrolSpaces[i], HasActiveShortTermIn(PatrolSpaces[i]));
            }
        }

        private void ReleaseAllHolds()
        {
            for (int i = 0; i < PatrolSpaces.Length; i++)
            {
                _bands.SetHold(PatrolSpaces[i], false);
            }
        }

        private void LockAll()
        {
            for (int i = 0; i < _watchers.Count; i++)
            {
                _watchers[i].Lock();
            }
        }

        private void RaiseSettled(RuleResult result)
        {
            Action<RuleResult> handler = Settled;
            if (handler == null)
            {
                return;
            }

            Delegate[] targets = handler.GetInvocationList();
            for (int i = 0; i < targets.Length; i++)
            {
                try
                {
                    ((Action<RuleResult>)targets[i])(result);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }
    }
}

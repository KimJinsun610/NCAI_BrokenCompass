using System;
using UnityEngine;

namespace NightDuty
{
    /// <summary>
    /// 판정/스탯 쪽에서 연출 쪽으로 건너가는 <b>유일한</b> 통로인 정적 이벤트 허브.
    /// <para>
    /// <c>NightDuty.Core</c>는 연출을 절대 참조하지 않는다. 축이 바뀌면 Core는 여기서 이벤트를 쏠 뿐이고,
    /// 무엇을 그릴지는 반대편(<c>NightDuty.Client</c>)이 정한다. 이 방향을 뒤집으면 단방향 의존이 깨진다.
    /// </para>
    /// <para>
    /// <b>ClearAll / SubsystemRegistration 훅을 지우지 말 것 — 죽은 코드가 아니다.</b>
    /// Unity의 "Enter Play Mode Options"에서 Domain Reload를 끄면 정적 필드가 플레이 모드 종료 후에도 살아남는다.
    /// 그러면 이전 플레이 세션의 구독자가 그대로 남아(구독 누출) 이벤트가 두 번, 세 번 발생하고,
    /// 이미 파괴된 오브젝트를 건드리는 <c>MissingReferenceException</c>이 뒤따른다.
    /// <see cref="ClearAll"/>을 <c>RuntimeInitializeLoadType.SubsystemRegistration</c> 시점에 호출해
    /// 매 플레이 시작마다 구독 목록을 비우는 것으로 이를 막는다.
    /// </para>
    /// <para>
    /// 모든 Raise 메서드는 구독자를 하나씩 개별 try/catch로 호출한다.
    /// 구독자 하나가 예외를 던져도 나머지 구독자에게 이벤트가 도달해야 하기 때문이다
    /// (기본 델리게이트 호출은 첫 예외에서 그대로 중단된다).
    /// </para>
    /// </summary>
    public static class EventBus
    {
        /// <summary>
        /// 어떤 공간의 어떤 축이 구간을 넘었다. 인자: (공간, 축, 이전 구간, 새 구간).
        /// 판정 쪽과 연출 쪽을 잇는 핵심 이벤트다.
        /// </summary>
        public static event Action<SpaceId, FearAxis, Band, Band> BandChanged;

        /// <summary>
        /// 구간은 그대로인 채 구간 내 진행도만 움직였다. 인자: (공간, 축, 0..1 진행도).
        /// 색온도 보간처럼 연속적인 연출에 쓴다.
        /// </summary>
        public static event Action<SpaceId, FearAxis, float> BandProgress;

        /// <summary>
        /// 하루가 시작됐다. 인자: (일차 1~5, 지침록 §0 조항의 형태).
        /// </summary>
        public static event Action<int, ClauseZeroType> DayStarted;

        /// <summary>
        /// 하루가 끝났다. 인자: 그날의 결산 정보.
        /// </summary>
        public static event Action<DaySummary> DayEnded;

        /// <summary>
        /// 축이 100에 도달했다. 인자: 임계에 다다른 축.
        /// </summary>
        public static event Action<FearAxis> AxisCritical;

        /// <summary><see cref="BandChanged"/>를 발생시킨다.</summary>
        public static void RaiseBandChanged(SpaceId space, FearAxis axis, Band from, Band to)
        {
            Action<SpaceId, FearAxis, Band, Band> handler = BandChanged;
            if (handler == null)
            {
                return;
            }

            Delegate[] targets = handler.GetInvocationList();
            for (int i = 0; i < targets.Length; i++)
            {
                Action<SpaceId, FearAxis, Band, Band> one = targets[i] as Action<SpaceId, FearAxis, Band, Band>;
                if (one == null)
                {
                    continue;
                }

                try
                {
                    one(space, axis, from, to);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }

        /// <summary><see cref="BandProgress"/>를 발생시킨다.</summary>
        public static void RaiseBandProgress(SpaceId space, FearAxis axis, float progress01)
        {
            Action<SpaceId, FearAxis, float> handler = BandProgress;
            if (handler == null)
            {
                return;
            }

            Delegate[] targets = handler.GetInvocationList();
            for (int i = 0; i < targets.Length; i++)
            {
                Action<SpaceId, FearAxis, float> one = targets[i] as Action<SpaceId, FearAxis, float>;
                if (one == null)
                {
                    continue;
                }

                try
                {
                    one(space, axis, progress01);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }

        /// <summary><see cref="DayStarted"/>를 발생시킨다.</summary>
        public static void RaiseDayStarted(int day, ClauseZeroType clauseZero)
        {
            Action<int, ClauseZeroType> handler = DayStarted;
            if (handler == null)
            {
                return;
            }

            Delegate[] targets = handler.GetInvocationList();
            for (int i = 0; i < targets.Length; i++)
            {
                Action<int, ClauseZeroType> one = targets[i] as Action<int, ClauseZeroType>;
                if (one == null)
                {
                    continue;
                }

                try
                {
                    one(day, clauseZero);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }

        /// <summary><see cref="DayEnded"/>를 발생시킨다.</summary>
        public static void RaiseDayEnded(DaySummary summary)
        {
            Action<DaySummary> handler = DayEnded;
            if (handler == null)
            {
                return;
            }

            Delegate[] targets = handler.GetInvocationList();
            for (int i = 0; i < targets.Length; i++)
            {
                Action<DaySummary> one = targets[i] as Action<DaySummary>;
                if (one == null)
                {
                    continue;
                }

                try
                {
                    one(summary);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }

        /// <summary><see cref="AxisCritical"/>을 발생시킨다.</summary>
        public static void RaiseAxisCritical(FearAxis axis)
        {
            Action<FearAxis> handler = AxisCritical;
            if (handler == null)
            {
                return;
            }

            Delegate[] targets = handler.GetInvocationList();
            for (int i = 0; i < targets.Length; i++)
            {
                Action<FearAxis> one = targets[i] as Action<FearAxis>;
                if (one == null)
                {
                    continue;
                }

                try
                {
                    one(axis);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }

        /// <summary>
        /// 모든 이벤트의 구독자를 비운다.
        /// Domain Reload를 끈 상태에서 이전 플레이 세션의 구독이 남는 것을 막기 위해
        /// 플레이 시작마다 자동으로 호출된다. 테스트 코드에서 수동으로 불러도 된다.
        /// </summary>
        public static void ClearAll()
        {
            BandChanged = null;
            BandProgress = null;
            DayStarted = null;
            DayEnded = null;
            AxisCritical = null;
        }

        /// <summary>
        /// 플레이 모드 진입 시 가장 이른 시점에 구독 목록을 초기화한다.
        /// Domain Reload가 꺼져 있어도 정적 상태가 깨끗한 채로 시작하도록 보장한다.
        /// <b>참조하는 곳이 없어 보여도 지우지 말 것</b> — Unity 런타임이 리플렉션으로 호출한다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            ClearAll();
        }
    }
}

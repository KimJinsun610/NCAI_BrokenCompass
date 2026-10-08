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
        /// 판정 쪽과 연출 쪽을 잇는 핵심 이벤트다. 재시작 복원 때는 구간이 <b>내려갈 수도</b> 있다.
        /// </summary>
        public static event Action<SpaceId, FearAxis, Band, Band> BandChanged;

        /// <summary>
        /// 구간은 그대로인 채 구간 내 진행도만 움직였다. 인자: (공간, 축, 0..1 진행도).
        /// 색온도 보간처럼 연속적인 연출에 쓴다.
        /// </summary>
        public static event Action<SpaceId, FearAxis, float> BandProgress;

        /// <summary>
        /// 하루가 끝났다(정상 종료·결근). 인자: 그날의 결산 정보.
        /// </summary>
        public static event Action<DaySummary> DayEnded;

        /// <summary>
        /// 축이 100에 도달했다 — <b>옛 「사망」 통로</b>. 인자: 임계에 다다른 축.
        /// <see cref="Captured"/> 구독자가 있으면(근무 씬의 붙잡힘 연출) 보내지 않는다. 김진선님 <c>PlayResultRouter</c>의 사망 화면이 이것을 듣는다.
        /// </summary>
        public static event Action<FearAxis> AxisCritical;

        /// <summary>
        /// 붙잡혔다(감각 축 100, 최종 기획서 「붙잡힘과 재시작」). 인자: 붙잡힌 축.
        /// 구독자(<c>CaptureDirector</c>)가 붙잡힘 연출 → 재시작 카드 → <see cref="NightRun.RestartAfterCapture"/>를 맡는다.
        /// 붙잡힘은 게임 오버가 아니라 그 밤을 다시 하는 것이라, 구독자가 있으면 <see cref="AxisCritical"/>(사망)은 보내지 않는다.
        /// </summary>
        public static event Action<FearAxis> Captured;

        /// <summary>
        /// 태블릿으로 문자가 왔다. 태블릿 UI가 메시지 탭에 실으면 된다(발신자 표시 없음).
        /// </summary>
        public static event Action<ParadoxMessage> MessageSent;

        /// <summary>
        /// [근무 지시] 문자(54차) — 지시·완료 답장·미완료 답장. 역설 문자와 달리 글리치 없이 태블릿 문자함에 실린다(<c>TabletBridge</c>).
        /// </summary>
        public static event Action<ParadoxMessage> DutySent;

        /// <summary>
        /// 역설 문자의 안전한 읽기를 마쳤다(10단계). 점검표에 그 공간의 이상 여부가 드러난다 — 태블릿이 짧게 떨고 「틱」 한 번, 태블릿을 다시 읽는다.
        /// </summary>
        public static event Action<SafeReadReveal> SafeReadConfirmed;

        /// <summary>태블릿에 보이는 글(수칙 얼룩·점검 지시)이 바뀌었다. 태블릿은 다시 읽는다(10단계 위반 얼룩·재입실 불가).</summary>
        public static event Action TabletTextChanged;

        /// <summary>67차(민: 「1시간마다 알림」): 밤 시계가 정시(01:00 · 02:00 · 03:00 · 04:00)를 지났다. 인자: 시. 태블릿 알림 한 통(<c>TabletBridge</c>).</summary>
        public static event Action<int> HourStruck;

        /// <summary><see cref="HourStruck"/>를 발생시킨다.</summary>
        public static void RaiseHourStruck(int hour)
        {
            Invoke(HourStruck, hour);
        }

        /// <summary>
        /// 경고 도장이나 대기 중인 처벌이 바뀌었다. 인자: (도장 수 0~2, 대기 중인 처벌 수).
        /// 태블릿 상단 바의 도장 세 칸이 구독한다. 세 번째 도장이 찍히는 순간은 대기 수가 늘어난 것으로 안다.
        /// </summary>
        public static event Action<int, int> WarningsChanged;

        /// <summary>
        /// 경고 누적 처벌이 나왔다. 인자: 처벌을 받은 감각 축(그 축의 몹이 그 축의 언어로 짧게 나타난다).
        /// </summary>
        public static event Action<FearAxis> Punished;

        /// <summary>
        /// 붙잡힌 밤을 다시 시작했다(또는 결근으로 넘겼다). 인자: 재시작 결과. 구동기가 씬·시계를 되돌린다.
        /// </summary>
        public static event Action<RestartResult> NightRestarted;

        /// <summary>
        /// 그날 점검 편성이 확정됐다(밤 시작, 재시작 때는 다시 보내지 않는다). 점검표 UI와 이상 연출이 구독한다.
        /// 이상 연출은 <see cref="InspectionAssignment.IsAnomaly"/>·<see cref="InspectionAssignment.Intensity"/>로 대상을 바꿔 둔다.
        /// </summary>
        public static event Action<InspectionPlan> InspectionPlanned;

        /// <summary>점검 보고가 판정됐다(받지 않은 보고는 보내지 않는다). 점검표 UI·근무일지가 구독한다. 수치는 화면에 내지 않는다.</summary>
        public static event Action<InspectionReport> InspectionReported;

        /// <summary>점검 지시 한 통이 나갔다(50차 순차 지시). TabletBridge가 태블릿 메시지로 넣는다(알람 한 번).</summary>
        public static event Action<InspectionOrder> InspectionOrdered;

        /// <summary>새 수칙 한 건이 정산됐다(위반·위협 성공·밤 종료 준수·기록). 위반 피드백·근무일지가 구독한다. 수치는 화면에 내지 않는다.</summary>
        public static event Action<FinalRuleResult> FinalRuleSettled;

        /// <summary>긴장 디렉터의 연출 알림(조우 단계·수칙 단서·가짜 놀람·메모). 대역·소등·소리·디버그 콘솔이 구독한다.</summary>
        public static event Action<DirectionEvent> DirectionEmitted;

        /// <summary>점검 수칙 「가까이」를 어겼다. 인자: (항목 ID, 그 항목의 축). 1초짜리 놀람 연출이 구독한다.</summary>
        public static event Action<string, FearAxis> InspectionStartled;

        /// <summary><see cref="BandChanged"/>를 발생시킨다.</summary>
        public static void RaiseBandChanged(SpaceId space, FearAxis axis, Band from, Band to)
        {
            Action<SpaceId, FearAxis, Band, Band> handler = BandChanged;
            if (handler == null) return;
            foreach (Delegate d in handler.GetInvocationList())
            {
                try { ((Action<SpaceId, FearAxis, Band, Band>)d)(space, axis, from, to); }
                catch (Exception e) { Debug.LogException(e); }
            }
        }

        /// <summary><see cref="BandProgress"/>를 발생시킨다.</summary>
        public static void RaiseBandProgress(SpaceId space, FearAxis axis, float progress01)
        {
            Action<SpaceId, FearAxis, float> handler = BandProgress;
            if (handler == null) return;
            foreach (Delegate d in handler.GetInvocationList())
            {
                try { ((Action<SpaceId, FearAxis, float>)d)(space, axis, progress01); }
                catch (Exception e) { Debug.LogException(e); }
            }
        }

        /// <summary><see cref="DayEnded"/>를 발생시킨다.</summary>
        public static void RaiseDayEnded(DaySummary summary)
        {
            Invoke(DayEnded, summary);
        }

        /// <summary><see cref="MessageSent"/>를 발생시킨다.</summary>
        public static void RaiseMessageSent(ParadoxMessage message)
        {
            Invoke(MessageSent, message);
        }

        /// <summary><see cref="DutySent"/>를 발생시킨다.</summary>
        public static void RaiseDutySent(ParadoxMessage message)
        {
            Invoke(DutySent, message);
        }

        /// <summary><see cref="TabletTextChanged"/>를 발생시킨다.</summary>
        public static void RaiseTabletTextChanged()
        {
            Action h = TabletTextChanged;
            if (h == null) return;
            foreach (Delegate d in h.GetInvocationList())
            {
                try { ((Action)d)(); }
                catch (Exception e) { Debug.LogException(e); }
            }
        }

        /// <summary><see cref="SafeReadConfirmed"/>를 발생시킨다.</summary>
        public static void RaiseSafeReadConfirmed(SafeReadReveal reveal)
        {
            Invoke(SafeReadConfirmed, reveal);
        }

        /// <summary><see cref="AxisCritical"/>을 발생시킨다.</summary>
        public static void RaiseAxisCritical(FearAxis axis)
        {
            Invoke(AxisCritical, axis);
        }

        /// <summary>붙잡힘을 알린다 — <see cref="Captured"/> 구독자가 있으면 그쪽, 없으면 옛 <see cref="AxisCritical"/>.</summary>
        public static void RaiseCapturedOrCritical(FearAxis axis)
        {
            if (Captured != null)
            {
                Invoke(Captured, axis);
                return;
            }

            Invoke(AxisCritical, axis);
        }

        /// <summary><see cref="WarningsChanged"/>를 발생시킨다.</summary>
        public static void RaiseWarningsChanged(int stamps, int pending)
        {
            Invoke(WarningsChanged, stamps, pending);
        }

        /// <summary><see cref="Punished"/>를 발생시킨다.</summary>
        public static void RaisePunished(FearAxis axis)
        {
            Invoke(Punished, axis);
        }

        /// <summary><see cref="NightRestarted"/>를 발생시킨다.</summary>
        public static void RaiseNightRestarted(RestartResult result)
        {
            Invoke(NightRestarted, result);
        }

        /// <summary><see cref="InspectionPlanned"/>을 발생시킨다.</summary>
        public static void RaiseInspectionPlanned(InspectionPlan plan)
        {
            Invoke(InspectionPlanned, plan);
        }

        /// <summary><see cref="InspectionReported"/>를 발생시킨다.</summary>
        public static void RaiseInspectionReported(InspectionReport report)
        {
            Invoke(InspectionReported, report);
        }

        /// <summary><see cref="InspectionOrdered"/>를 발생시킨다.</summary>
        public static void RaiseInspectionOrdered(InspectionOrder order)
        {
            Invoke(InspectionOrdered, order);
        }

        /// <summary><see cref="FinalRuleSettled"/>를 발생시킨다.</summary>
        public static void RaiseFinalRuleSettled(FinalRuleResult result)
        {
            Invoke(FinalRuleSettled, result);
        }

        /// <summary><see cref="DirectionEmitted"/>를 발생시킨다.</summary>
        public static void RaiseDirectionEmitted(DirectionEvent e)
        {
            Invoke(DirectionEmitted, e);
        }

        /// <summary><see cref="InspectionStartled"/>를 발생시킨다.</summary>
        public static void RaiseInspectionStartled(string itemId, FearAxis axis)
        {
            Invoke(InspectionStartled, itemId, axis);
        }

        private static void Invoke<T>(Action<T> handler, T arg)
        {
            if (handler == null) return;
            foreach (Delegate d in handler.GetInvocationList())
            {
                try { ((Action<T>)d)(arg); }
                catch (Exception e) { Debug.LogException(e); }
            }
        }

        private static void Invoke<T1, T2>(Action<T1, T2> handler, T1 a, T2 b)
        {
            if (handler == null) return;
            foreach (Delegate d in handler.GetInvocationList())
            {
                try { ((Action<T1, T2>)d)(a, b); }
                catch (Exception e) { Debug.LogException(e); }
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
            DayEnded = null;
            AxisCritical = null;
            Captured = null;
            MessageSent = null;
            DutySent = null;
            SafeReadConfirmed = null;
            TabletTextChanged = null;
            HourStruck = null;
            WarningsChanged = null;
            Punished = null;
            NightRestarted = null;
            InspectionPlanned = null;
            InspectionReported = null;
            InspectionOrdered = null;
            FinalRuleSettled = null;
            DirectionEmitted = null;
            InspectionStartled = null;
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

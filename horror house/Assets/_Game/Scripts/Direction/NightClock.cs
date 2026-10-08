using System;

namespace NightDuty
{
    /// <summary>하룻밤의 구간(2026-09-30 최종 기획서 「밤 시계」).</summary>
    public enum NightPhase
    {
        /// <summary>00:00–00:20. 경비실 출근. 판정 정지(무한 루프 방지 장치 4).</summary>
        Arrival = 0,

        /// <summary>00:20–01:15. 자유 점검, 가짜 놀람 1~2개.</summary>
        LowTension = 1,

        /// <summary>01:15–02:20. 호출 1 뒤 조우 슬롯 A.</summary>
        SlotA = 2,

        /// <summary>02:20–02:50. 이완. 판정 정지, 경비실 근무일지 중간 서명(체크포인트).</summary>
        Relax = 3,

        /// <summary>02:50–03:55. 호출 2 뒤 조우 슬롯 B(그날의 메인). 마지막 점검 공간이 열린다.</summary>
        SlotB = 4,

        /// <summary>03:55–04:25. 남은 점검, 보너스 슬롯 C.</summary>
        SlotC = 5,

        /// <summary>04:25–05:00. 잔여 점검 알림 뒤 복귀. 수칙 판정 종료, 새 조우 없음, 보고는 가능.</summary>
        Wrap = 6,

        /// <summary>05:00. 근무 종료.</summary>
        Ended = 7
    }

    /// <summary>
    /// 밤 시계 표 — <b>실시간 12분 30초 = 게임 00:00~05:00</b>(실시간 1분 = 게임 24분 그대로). 59차(민: 「하룻밤이 너무 길다 — 2/3 정도로」): 15분 → 10분.
    /// 67차(민: 「하루 시간도 5시까지 늘려줘」 · 「실제 길이도 늘릴 거야」): 04:00 → 05:00, 10분 → 12분 30초 — 구간 경계를 모두 ×1.25(분 단위로 반올림).
    /// 시각은 모두 <b>근무 시작(00:00)부터의 게임 분</b>이다. 시각 숫자는 여기 한 곳에만 둔다.
    /// <para>
    /// 판정 시간창은 00:20~04:25이고 이완 구간(02:20~02:50)에는 멈춘다. 퇴실 여부와는 무관하다 —
    /// 경비실에 머무르는 사람에게도 K3가 걸려야 하기 때문이다. 예외는 03:30에 진행 중이던 조우의 대응 창과
    /// 5일차 05:00 뒤의 K4다(둘 다 이 표가 아니라 호출하는 쪽이 판단한다).
    /// </para>
    /// </summary>
    public static class NightClock
    {
        /// <summary>하룻밤의 실시간 길이(초). 12분 30초(67차, 59차 10분 · 그 전 15분).</summary>
        public const float RealSecondsPerNight = 750f;

        /// <summary>하룻밤의 게임 길이(분). 00:00~05:00(67차, 전에는 04:00).</summary>
        public const int NightMinutes = 300;

        /// <summary>실시간 1초에 흐르는 게임 초. 300분 × 60 / 750초 = 24.</summary>
        public const float GameSecondsPerRealSecond = NightMinutes * 60f / RealSecondsPerNight;

        /// <summary>출근이 끝나고 수칙 판정이 시작되는 시각(00:20, 67차 — 전에는 00:16).</summary>
        public const int JudgingStart = 20;

        /// <summary>호출 1 · 조우 슬롯 A 시작(01:15, 67차 — 전에는 01:00).</summary>
        public const int Call1 = 75;

        /// <summary>이완 구간 시작(02:20, 67차 — 전에는 01:52). 판정 정지.</summary>
        public const int RelaxStart = 140;

        /// <summary>호출 2 · 이완 끝 · 체크포인트 시각(02:50, 67차 — 전에는 02:16). 그날 마지막 점검 공간이 열린다.</summary>
        public const int Call2 = 170;

        /// <summary>보너스 슬롯 C 시작(03:55, 67차 — 전에는 03:08).</summary>
        public const int SlotCStart = 235;

        /// <summary>잔여 점검 알림 · 수칙 판정 종료(04:25, 67차 — 전에는 03:30).</summary>
        public const int JudgingEnd = 265;

        /// <summary>근무 종료(05:00, 67차 — 전에는 04:00).</summary>
        public const int ShiftEnd = 300;

        /// <summary>67차(민: 「1시간마다 알림」): 정시 알림을 보내는 시각(01:00 · 02:00 · 03:00 · 04:00). 05:00은 근무 종료라 따로 없다.</summary>
        public static readonly int[] HourMarks = { 60, 120, 180, 240 };

        /// <summary>그 게임 분의 「HH:MM」(근무 시작 00:00 기준).</summary>
        public static string Clock(float minute)
        {
            int m = Math.Max(0, (int)Math.Floor(minute));
            return (m / 60).ToString("00") + ":" + (m % 60).ToString("00");
        }

        /// <summary>그 시각의 구간.</summary>
        /// <param name="minute">근무 시작부터의 게임 분.</param>
        public static NightPhase PhaseAt(float minute)
        {
            if (minute < JudgingStart) return NightPhase.Arrival;
            if (minute < Call1) return NightPhase.LowTension;
            if (minute < RelaxStart) return NightPhase.SlotA;
            if (minute < Call2) return NightPhase.Relax;
            if (minute < SlotCStart) return NightPhase.SlotB;
            if (minute < JudgingEnd) return NightPhase.SlotC;
            if (minute < ShiftEnd) return NightPhase.Wrap;
            return NightPhase.Ended;
        }

        /// <summary>수칙을 판정하는 시각인지(00:16~03:30, 이완 구간 제외).</summary>
        public static bool IsJudging(float minute)
        {
            NightPhase phase = PhaseAt(minute);
            return phase == NightPhase.LowTension || phase == NightPhase.SlotA
                || phase == NightPhase.SlotB || phase == NightPhase.SlotC;
        }

        /// <summary>새 조우를 걸 수 있는 시각인지(슬롯 A·B·C). 03:30 이후에는 새 조우가 없다.</summary>
        public static bool CanStartEncounter(float minute)
        {
            NightPhase phase = PhaseAt(minute);
            return phase == NightPhase.SlotA || phase == NightPhase.SlotB || phase == NightPhase.SlotC;
        }

        /// <summary>근무일지 중간 서명(체크포인트)을 할 수 있는 시각인지 — 이완 구간.</summary>
        public static bool CanSignCheckpoint(float minute)
        {
            return PhaseAt(minute) == NightPhase.Relax;
        }

        /// <summary>그 게임 시각까지 흐른 실시간(초).</summary>
        public static float RealSecondsAt(float minute)
        {
            return minute * 60f / GameSecondsPerRealSecond;
        }

        /// <summary>그 실시간(초)이 흐른 뒤의 게임 시각(분).</summary>
        public static float MinuteAtRealSeconds(float realSeconds)
        {
            return realSeconds * GameSecondsPerRealSecond / 60f;
        }
    }

    /// <summary>
    /// 밤 시계의 경계를 지나는 순간을 알려 주는 추적기. 시각은 바깥(게임 시계)에서 받아 온다 — 스스로 흐르지 않는다.
    /// <para>
    /// 한 번에 여러 경계를 건너뛰면(프레임 멈춤·배속) 건너뛴 경계를 <b>순서대로 모두</b> 알린다.
    /// 체크포인트 복원처럼 시각을 되돌릴 때는 <see cref="Reset"/>을 쓴다 — 이미 지난 경계를 다시 알리지 않는다.
    /// </para>
    /// </summary>
    public sealed class NightClockTracker
    {
        private float _minute;
        private bool _started;

        /// <summary>구간이 바뀌었다. 인자: (이전, 새 구간).</summary>
        public event Action<NightPhase, NightPhase> PhaseChanged;

        /// <summary>호출 1(01:00).</summary>
        public event Action Call1;

        /// <summary>호출 2(02:16) — 그날 마지막 점검 공간이 열린다.</summary>
        public event Action Call2;

        /// <summary>잔여 점검 알림(03:30). 수칙 판정이 끝난다.</summary>
        public event Action ResidualAlert;

        /// <summary>근무 종료(05:00). 한 번만.</summary>
        public event Action ShiftEnded;

        /// <summary>67차: 정시(01:00 · 02:00 · 03:00 · 04:00)를 지났다. 인자: 시(1~4).</summary>
        public event Action<int> HourStruck;

        /// <summary>마지막으로 받은 시각(근무 시작부터의 게임 분).</summary>
        public float Minute
        {
            get { return _minute; }
        }

        /// <summary>현재 구간.</summary>
        public NightPhase Phase
        {
            get { return NightClock.PhaseAt(_minute); }
        }

        /// <summary>
        /// 시각을 옮기되 지나간 경계를 알리지 않는다. 밤 시작(0)과 체크포인트 복원(02:16)에서 부른다.
        /// </summary>
        public void Reset(float minute)
        {
            _minute = Math.Max(0f, minute);
            _started = true;
        }

        /// <summary>
        /// 새 시각을 알려 준다. 앞으로 간 경우에만 그 사이의 경계를 순서대로 알린다. 뒤로 가면 무시한다.
        /// </summary>
        public void Advance(float minute)
        {
            if (!_started)
            {
                Reset(0f);
            }

            if (minute <= _minute)
            {
                return;
            }

            float from = _minute;
            _minute = minute;

            Cross(from, minute, NightClock.JudgingStart, null);
            Cross(from, minute, NightClock.Call1, Call1);
            Cross(from, minute, NightClock.RelaxStart, null);
            Cross(from, minute, NightClock.Call2, Call2);
            Cross(from, minute, NightClock.SlotCStart, null);
            Cross(from, minute, NightClock.JudgingEnd, ResidualAlert);
            Cross(from, minute, NightClock.ShiftEnd, ShiftEnded);

            for (int i = 0; i < NightClock.HourMarks.Length; i++)
            {
                int mark = NightClock.HourMarks[i];
                if (from < mark && minute >= mark) RaiseHour(mark / 60);
            }
        }

        private void RaiseHour(int hour)
        {
            Action<int> handler = HourStruck;
            if (handler == null) return;
            Delegate[] targets = handler.GetInvocationList();
            for (int i = 0; i < targets.Length; i++)
            {
                try
                {
                    ((Action<int>)targets[i])(hour);
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogException(e);
                }
            }
        }

        private void Cross(float from, float to, int boundary, Action named)
        {
            if (from >= boundary || to < boundary)
            {
                return;
            }

            NightPhase before = NightClock.PhaseAt(boundary - 0.001f);
            NightPhase after = NightClock.PhaseAt(boundary);
            Raise(PhaseChanged, before, after);
            Raise(named);
        }

        private static void Raise(Action handler)
        {
            if (handler == null)
            {
                return;
            }

            Delegate[] targets = handler.GetInvocationList();
            for (int i = 0; i < targets.Length; i++)
            {
                try
                {
                    ((Action)targets[i])();
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogException(e);
                }
            }
        }

        private static void Raise(Action<NightPhase, NightPhase> handler, NightPhase before, NightPhase after)
        {
            if (handler == null)
            {
                return;
            }

            Delegate[] targets = handler.GetInvocationList();
            for (int i = 0; i < targets.Length; i++)
            {
                try
                {
                    ((Action<NightPhase, NightPhase>)targets[i])(before, after);
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogException(e);
                }
            }
        }
    }
}

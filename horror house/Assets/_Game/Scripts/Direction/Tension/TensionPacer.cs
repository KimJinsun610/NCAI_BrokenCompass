using System;

namespace NightDuty
{
    /// <summary>긴장 조절기의 상태(Left 4 Dead 디렉터의 축적 → 절정 → 해소 → 휴식).</summary>
    public enum PacerState
    {
        /// <summary>출근(00:00~00:16). 아무것도 내지 않는다.</summary>
        Quiet = 0,

        /// <summary>축적 — 가짜 놀람·점검 지시를 낼 수 있다.</summary>
        BuildUp = 1,

        /// <summary>절정 — 긴장도가 <see cref="TensionPacer.PeakAt"/> 이상. 새 비트 없음.</summary>
        Peak = 2,

        /// <summary>절정 해소 — 긴장도가 <see cref="TensionPacer.FadeTo"/> 아래로 내려갈 때까지.</summary>
        Fade = 3,

        /// <summary>휴식 — 최소 시간과 긴장도 바닥을 채울 때까지 가짜 놀람 없음. 이완 구간(01:52~02:16)도 휴식이다.</summary>
        Relax = 4,

        /// <summary>03:30 뒤 — 앰비언스만.</summary>
        AmbientOnly = 5
    }

    /// <summary>긴장도를 올리는 사건.</summary>
    public enum PacerImpulse
    {
        /// <summary>조우 전조(+8).</summary>
        Foreshadow = 0,

        /// <summary>헛예고(+8).</summary>
        FalseForeshadow = 1,

        /// <summary>조우 대면(+10 + 10×강도).</summary>
        Confront = 2,

        /// <summary>가짜 놀람(+10).</summary>
        Fake = 3,

        /// <summary>점검 「가까이」 놀람(+15).</summary>
        Startle = 4,

        /// <summary>수칙 위반·감각 축 상승(+5).</summary>
        Violation = 5,

        /// <summary>점검 지시 문자(+3).</summary>
        Order = 6,

        /// <summary>수칙 단서(+6).</summary>
        RuleCue = 7
    }

    /// <summary>
    /// 플레이어 긴장 조절기(2026-10-06 50차, 민 요청 「레포데2의 좀비 컨트롤처럼」 · 전문가 회의 4인 종합).
    /// <para>
    /// 두 겹이다 — 빠른 <see cref="Intensity"/>(사건마다 튀고 4초 뒤 초당 1.2씩 빠진다, 경비실은 두 배)와
    /// 느린 <see cref="Pressure"/>(긴장도가 50을 넘는 동안 쌓이고 70이면 45초 강제 휴식 — 엘리언 아이솔레이션의 메나스 게이지).
    /// 바닥값 = min(20, (최고 감각 생존 수치 − 30) × 0.5) — 이미 몰린 플레이어는 완전히 식지 않는다.
    /// </para>
    /// <para>
    /// 조절기는 <b>무엇을 내지 않을지</b>만 정한다. 조우·수칙 단서는 편성과 수칙에 묶여 있으므로 여기서 막지 않는다(조우는 절정 중에만 슬롯 끝 10분 전까지 미룬다 — 디렉터).
    /// 가짜 놀람은 축적 상태 · 긴장도 50 미만 · 일차 상한(2/4/5/6/7, 강도 단계 보정) · 점검 지시 뒤 10/8/6초 · 1일차는 첫 조우 결과 뒤에만.
    /// 점검 지시(<see cref="InspectionDispatcher"/>)는 <see cref="IsHot"/>이면 최대 20초 미룬다.
    /// </para>
    /// 코어 안의 순수 계산이다(엔진·씬을 모른다). 시각은 <see cref="Tick"/>의 누적 실제 초.
    /// </summary>
    public sealed class TensionPacer
    {
        /// <summary>절정으로 넘어가는 긴장도.</summary>
        public const float PeakAt = 70f;

        /// <summary>해소가 끝나 휴식으로 넘어가는 긴장도.</summary>
        public const float FadeTo = 35f;

        /// <summary>휴식을 끝낼 수 있는 긴장도.</summary>
        public const float RelaxUntil = 25f;

        /// <summary>점검 지시를 미루는 긴장도.</summary>
        public const float HotAt = 60f;

        /// <summary>가짜 놀람을 낼 수 있는 긴장도 상한(이 값 미만에서만).</summary>
        public const float FakeRoomBelow = 50f;

        /// <summary>사건 뒤 감쇠를 시작하기까지(초).</summary>
        public const float Grace = 4f;

        /// <summary>초당 감쇠.</summary>
        public const float Decay = 1.2f;

        /// <summary>경비실 감쇠 배수.</summary>
        public const float GuardRoomDecayScale = 2f;

        /// <summary>압력이 쌓이기 시작하는 긴장도.</summary>
        public const float PressureFrom = 50f;

        /// <summary>압력 초당 상승·하강.</summary>
        public const float PressureRise = 1f;

        /// <summary>압력 초당 하강.</summary>
        public const float PressureFall = 0.3f;

        /// <summary>강제 휴식을 거는 압력.</summary>
        public const float PressureLimit = 70f;

        /// <summary>강제 휴식 길이(초).</summary>
        public const float ForcedRelaxSeconds = 45f;

        /// <summary>강제 휴식 뒤 압력.</summary>
        public const float PressureAfterRelax = 30f;

        /// <summary>휴식 최대 길이(초).</summary>
        public const float RelaxMax = 75f;

        /// <summary>바닥값 상한.</summary>
        public const float FloorMax = 20f;

        private static readonly int[] FakeCaps = { 3, 5, 6, 7, 8 };   // 57차(민: 「3일차까지 놀람이 별로 없다」): 2/4/5/6/7 → +1, 멀리서 보이는 몹(fake.glimpse) 몫

        private readonly int _day;
        private float _lastImpulse = float.NegativeInfinity;
        private float _lastOrder = float.NegativeInfinity;
        private float _stateSince;
        private float _forcedRelaxUntil = float.NegativeInfinity;
        private bool _calendarRelax;

        /// <summary>그날 조절기를 만든다.</summary>
        public TensionPacer(int day)
        {
            _day = Math.Max(1, day);
        }

        /// <summary>빠른 긴장도(0~100).</summary>
        public float Intensity { get; private set; }

        /// <summary>느린 압력(0~100).</summary>
        public float Pressure { get; private set; }

        /// <summary>지금 상태.</summary>
        public PacerState State { get; private set; } = PacerState.Quiet;

        /// <summary>조절기 시각(누적 실제 초).</summary>
        public float Now { get; private set; }

        /// <summary>지금 바닥값.</summary>
        public float Floor { get; private set; }

        /// <summary>지금 상태에 들어온 뒤 흐른 초.</summary>
        public float InState
        {
            get { return Now - _stateSince; }
        }

        /// <summary>마지막 점검 지시 뒤 흐른 초(없으면 무한).</summary>
        public float SinceOrder
        {
            get { return Now - _lastOrder; }
        }

        /// <summary>뜨거운가(절정·해소 또는 긴장도 60 이상) — 점검 지시를 미룬다.</summary>
        public bool IsHot
        {
            get { return State == PacerState.Peak || State == PacerState.Fade || Intensity >= HotAt; }
        }

        /// <summary>잔잔한가(축적·출근 상태이고 긴장도 35 미만) — 짧은 연속 지시를 허락한다.</summary>
        public bool IsCalm
        {
            get { return (State == PacerState.BuildUp || State == PacerState.Quiet || State == PacerState.Relax) && Intensity < FadeTo; }
        }

        /// <summary>일차별 가짜 놀람 상한(강도 단계 보정 전): 2 · 4 · 5 · 6 · 7.</summary>
        public static int FakeCap(int day)
        {
            return FakeCaps[Math.Max(0, Math.Min(FakeCaps.Length - 1, day - 1))];
        }

        /// <summary>일차별 최소 휴식(초): 40 · 33 · 28 · 25 …</summary>
        public static float RelaxMin(int day)
        {
            if (day <= 1) return 40f;
            if (day == 2) return 33f;
            if (day == 3) return 28f;
            return 25f;
        }

        /// <summary>점검 지시 뒤 가짜 놀람을 쉬는 초: 10 · 8 · 6.</summary>
        public static float OrderQuiet(int day)
        {
            if (day <= 1) return 10f;
            if (day == 2) return 8f;
            return 6f;
        }

        /// <summary>사건 하나의 긴장도 몫. <paramref name="strength"/>는 조우 강도(1~5).</summary>
        public static float AmountOf(PacerImpulse kind, int strength)
        {
            switch (kind)
            {
                case PacerImpulse.Foreshadow:
                case PacerImpulse.FalseForeshadow:
                    return 8f;
                case PacerImpulse.Confront:
                    return 10f + 10f * Math.Max(1, Math.Min(5, strength));
                case PacerImpulse.Fake:
                    return 10f;
                case PacerImpulse.Startle:
                    return 15f;
                case PacerImpulse.Violation:
                    return 5f;
                case PacerImpulse.Order:
                    return 3f;
                case PacerImpulse.RuleCue:
                    return 6f;
                default:
                    return 0f;
            }
        }

        /// <summary>사건을 넣는다.</summary>
        public void Impulse(PacerImpulse kind, int strength = 0)
        {
            Intensity = Math.Min(100f, Intensity + AmountOf(kind, strength));
            _lastImpulse = Now;
            if (kind == PacerImpulse.Order) _lastOrder = Now;
        }

        /// <summary>
        /// 시간 경과. <paramref name="minute"/> = 밤 시계(모르면 음수 — 판정 구간으로 본다), <paramref name="dt"/> = 실제 초.
        /// </summary>
        public void Tick(float minute, float dt, bool inGuardRoom, int highestSensory)
        {
            if (dt < 0f) dt = 0f;
            Now += dt;

            Floor = Math.Max(0f, Math.Min(FloorMax, (highestSensory - 30) * 0.5f));
            if (Now - _lastImpulse > Grace)
            {
                Intensity -= Decay * (inGuardRoom ? GuardRoomDecayScale : 1f) * dt;
            }

            if (Intensity < Floor) Intensity = Floor;
            if (Intensity > 100f) Intensity = 100f;

            Pressure += (Intensity > PressureFrom ? PressureRise : -PressureFall) * dt;
            if (Pressure < 0f) Pressure = 0f;
            if (Pressure > 100f) Pressure = 100f;

            Step(minute);
        }

        private void Step(float minute)
        {
            if (minute >= 0f && minute < NightClock.JudgingStart)
            {
                Enter(PacerState.Quiet);
                return;
            }

            if (minute >= NightClock.JudgingEnd)
            {
                Enter(PacerState.AmbientOnly);
                return;
            }

            if (minute >= 0f && NightClock.PhaseAt(minute) == NightPhase.Relax)
            {
                _calendarRelax = true;
                Enter(PacerState.Relax);
                return;
            }

            if (_calendarRelax)
            {
                // 이완 구간이 끝났다(02:16) — 휴식의 최소 시간은 이미 채웠다.
                _calendarRelax = false;
                Enter(PacerState.BuildUp);
            }

            if (Pressure >= PressureLimit && State != PacerState.Relax)
            {
                _forcedRelaxUntil = Now + ForcedRelaxSeconds;
                Pressure = PressureAfterRelax;
                Enter(PacerState.Relax);
                return;
            }

            switch (State)
            {
                case PacerState.Quiet:
                case PacerState.AmbientOnly:
                    Enter(PacerState.BuildUp);
                    if (Intensity >= PeakAt) Enter(PacerState.Peak);
                    break;
                case PacerState.BuildUp:
                    if (Intensity >= PeakAt) Enter(PacerState.Peak);
                    break;
                case PacerState.Peak:
                    if (Intensity < PeakAt) Enter(PacerState.Fade);
                    break;
                case PacerState.Fade:
                    if (Intensity >= PeakAt) Enter(PacerState.Peak);
                    else if (Intensity < FadeTo) Enter(PacerState.Relax);
                    break;
                case PacerState.Relax:
                    bool rested = InState >= RelaxMin(_day) && Intensity < RelaxUntil && Now >= _forcedRelaxUntil;
                    if (rested || InState >= Math.Max(RelaxMax, _forcedRelaxUntil - _stateSince)) Enter(PacerState.BuildUp);
                    break;
            }
        }

        private void Enter(PacerState next)
        {
            if (State == next) return;
            State = next;
            _stateSince = Now;
        }

        /// <summary>
        /// 가짜 놀람을 지금 낼 수 있는가(일차 상한·1일차 조건은 디렉터가 본다). 안 되면 이유.
        /// </summary>
        public bool AllowsFake(out string why)
        {
            if (State != PacerState.BuildUp)
            {
                why = "조절기 " + State;
                return false;
            }

            if (Intensity >= FakeRoomBelow)
            {
                why = "긴장도 " + (int)Intensity;
                return false;
            }

            if (SinceOrder < OrderQuiet(_day))
            {
                why = "점검 지시 직후";
                return false;
            }

            why = string.Empty;
            return true;
        }

        /// <summary>재시작 — 같은 밤을 다시 한다. 긴장·압력·상태를 비운다(시각은 이어진다).</summary>
        public void Reset()
        {
            Intensity = 0f;
            Pressure = 0f;
            State = PacerState.Quiet;
            _stateSince = Now;
            _lastImpulse = float.NegativeInfinity;
            _lastOrder = float.NegativeInfinity;
            _forcedRelaxUntil = float.NegativeInfinity;
            _calendarRelax = false;
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return State + " I" + (int)Intensity + " P" + (int)Pressure + (Floor > 0f ? " 바닥" + (int)Floor : string.Empty);
        }
    }
}

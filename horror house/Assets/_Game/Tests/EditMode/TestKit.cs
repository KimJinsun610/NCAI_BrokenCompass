using System;

namespace NightDuty.Tests
{
    /// <summary>
    /// NightRun 테스트 도우미. 2026-10-03 옛 판정 책(RuleBook)과 함께 옛 카드 만들기(Card·Book)를 지웠다 —
    /// 이제 수칙 위반은 새 편성의 공통 수칙 G1(「복도에서는 뛰지 마십시오」, 청각, 밤당 한 번 · 매일 편성)으로 만든다.
    /// </summary>
    internal static class TestKit
    {
        /// <summary>새 편성을 켜고 밤을 연다. TearDown에서 <see cref="NightRun.ProgramEnabled"/>를 꺼야 한다.</summary>
        public static void BeginProgramNight(int day, Func<int> clock)
        {
            NightRun.ProgramEnabled = true;
            NightRun.BeginNight(day, clock);
        }

        /// <summary>G1 위반 한 번: 복도에 들어가 1.2초 달린 뒤 멈춘다(판정 구간이면 청각 +12 — 밤당 한 번만).</summary>
        public static void ViolateRunning()
        {
            NightRun.Send(JudgeSignal.OfSpace(SignalKind.SpaceEntered, SpaceId.Corridor));
            NightRun.Send(JudgeSignal.Run(true));
            for (int i = 0; i < 12; i++)
            {
                NightRun.Tick(0.1f);
            }

            NightRun.Send(JudgeSignal.Run(false));
        }
    }
}

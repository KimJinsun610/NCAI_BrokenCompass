using NUnit.Framework;

namespace NightDuty.Tests
{
    /// <summary>
    /// 104차: 게임은 고정 날짜 편성(<see cref="ProgramDirector.FixedSchedule"/> = 참)을 쓴다. 103차까지의 편성 시험(점수 뽑기·슬롯 시간창·이월)은
    /// 옛 편성을 시험하므로 이 이름공간의 시험 전체는 거짓으로 돌리고, 고정 편성 시험(<see cref="OneHundredFourthScheduleTests"/>)만 제 안에서 켠다.
    /// </summary>
    [SetUpFixture]
    public sealed class LegacyScheduleFixture
    {
        [OneTimeSetUp]
        public void Legacy()
        {
            ProgramDirector.FixedSchedule = false;
        }

        [OneTimeTearDown]
        public void Restore()
        {
            ProgramDirector.FixedSchedule = true;
        }
    }
}

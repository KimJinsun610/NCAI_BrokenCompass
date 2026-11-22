using InstructionDT;
using NUnit.Framework;

namespace NightDuty.Tests
{
    /// <summary>3단계 Message: 코드가 읽는 Message_Table 행이 있고, 코드가 기대하는 모양(자리표시·나누기)을 지키는지.</summary>
    public class DesignDataMessageTests
    {
        static readonly uint[] Used =
        {
            140001, 140002, 140003, 140004, 140005, 140006, 140007, 140008, 140009, 140010, 140011, 140012, 140013,
            140014, 140015, 140016, 140017, 140018, 140019, 140020, 140021, 140022, 140023, 140024, 140025, 140026,
            140027, 140028, 140029, 140030, 140031, 140032, 140033, 140034, 140035, 140036, 140037, 140038,
        };

        [Test]
        public void 코드가_읽는_문자_행이_모두_있고_문구가_비지_않음()
        {
            foreach (var id in Used)
                Assert.IsFalse(string.IsNullOrEmpty(DesignData.Msg(id)), "Message " + id + " 문구 빈칸");
        }

        [Test]
        public void 코드가_기대하는_모양()
        {
            StringAssert.Contains("{0}", DesignData.Msg(140005), "독촉 문자는 건수 자리표시 {0}");
            Assert.AreEqual(2, DesignData.Msg(140037).Split(new[] { " → " }, System.StringSplitOptions.None).Length, "조기 퇴근 안내 = 첫 안내 → 한 번 더");
            Assert.AreEqual(2, DesignData.Msg(140038).Split(new[] { " → " }, System.StringSplitOptions.None).Length, "피날레 퇴근 안내 = 첫 안내 → 한 번 더");
            Assert.Greater(DesignData.MsgParts(140032).Length, 1, "마지막 몰아치기는 「a | b | …」 중 무작위");
        }

        [Test]
        public void 피날레_시간_칸()
        {
            var letIn = DesignData.MessageRow(140023);
            Assert.Greater(letIn.Repeat_Duration_Sec, 2f, "들여보내줘 응시 창 = 바뀌는 2초 + 온전한 시간");
            Assert.Greater(letIn.Repeat_Interval_Sec, 0f);
            Assert.GreaterOrEqual(DesignData.MessageRow(140033).Delay_Sec, 0.2f, "마지막 문구 지연 = 정지 0.2초 + 검은 화면");
            Assert.Greater(DesignData.MessageRow(140021).Show_Sec, 0f);
            Assert.Greater(DesignData.MessageRow(140032).Repeat_Interval_Sec, 0f);
        }
    }
}

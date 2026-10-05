using System;
using NUnit.Framework;

namespace NightDuty.Tests
{
    /// <summary>붙잡힘 통로(8단계): 붙잡힘 연출(<c>CaptureDirector</c>)이 있으면 옛 사망 신호를 보내지 않는다.</summary>
    public class CaptureRoutingTests
    {
        [SetUp]
        public void SetUp()
        {
            EventBus.ClearAll();
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.ClearAll();
        }

        [Test]
        public void 붙잡힘_구독자가_있으면_사망_신호는_보내지_않는다()
        {
            FearAxis? captured = null;
            bool critical = false;
            Action<FearAxis> onCaptured = a => captured = a;
            Action<FearAxis> onCritical = a => critical = true;
            EventBus.Captured += onCaptured;
            EventBus.AxisCritical += onCritical;

            EventBus.RaiseCapturedOrCritical(FearAxis.Illuminance);

            Assert.AreEqual(FearAxis.Illuminance, captured);
            Assert.IsFalse(critical);
        }

        [Test]
        public void 붙잡힘_구독자가_없으면_옛_사망_신호로_간다()
        {
            FearAxis? critical = null;
            Action<FearAxis> onCritical = a => critical = a;
            EventBus.AxisCritical += onCritical;

            EventBus.RaiseCapturedOrCritical(FearAxis.Layout);

            Assert.AreEqual(FearAxis.Layout, critical);
        }

        [Test]
        public void ClearAll은_붙잡힘_구독도_지운다()
        {
            bool captured = false;
            bool critical = false;
            Action<FearAxis> onCaptured = a => captured = true;
            EventBus.Captured += onCaptured;
            EventBus.ClearAll();
            Action<FearAxis> onCritical = a => critical = true;
            EventBus.AxisCritical += onCritical;

            EventBus.RaiseCapturedOrCritical(FearAxis.Auditory);

            Assert.IsFalse(captured);
            Assert.IsTrue(critical);
        }
    }
}

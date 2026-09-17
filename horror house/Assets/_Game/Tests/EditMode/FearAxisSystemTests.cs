using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace NightDuty.Tests
{
    /// <summary>기획서 공통 명세 6절 「100 도달」 및 축 규약.</summary>
    public sealed class FearAxisSystemTests
    {
        [TearDown]
        public void TearDown()
        {
            EventBus.ClearAll();
        }

        [TestCase(99, 2, FearAxis.Auditory)]
        [TestCase(88, 12, FearAxis.Illuminance)]
        [TestCase(90, 25, FearAxis.Layout)]
        public void 백도달_값은100_종료신호는한번(int start, int delta, FearAxis axis)
        {
            FearAxisSystem axes = new FearAxisSystem();
            int critical = 0;
            EventBus.AxisCritical += a => critical++;

            axes.Apply(axis, start, "setup", SpaceId.None);
            axes.Apply(axis, delta, "X1", SpaceId.Corridor);

            Assert.AreEqual(100, axes.GetValue(axis));
            Assert.IsTrue(axes.IsLocked);
            Assert.AreEqual(1, critical);
            Assert.AreEqual(axis, axes.Cause.Axis);
            Assert.AreEqual("X1", axes.Cause.SourceId);
            Assert.AreEqual(SpaceId.Corridor, axes.Cause.Space);
        }

        [Test]
        public void 신뢰100은_포획이아니다_값만100에서멈춘다()
        {
            FearAxisSystem axes = new FearAxisSystem();
            int critical = 0;
            int terminated = 0;
            EventBus.AxisCritical += a => critical++;
            axes.Terminated += c => terminated++;

            axes.Apply(FearAxis.Trust, 99, "setup", SpaceId.None);
            Assert.IsTrue(axes.Apply(FearAxis.Trust, 2, "H1", SpaceId.Corridor));
            Assert.IsFalse(axes.Apply(FearAxis.Trust, 4, "H6", SpaceId.Corridor), "100 이후 신뢰는 더 오르지 않는다");

            Assert.AreEqual(100, axes.GetValue(FearAxis.Trust));
            Assert.IsFalse(axes.IsLocked);
            Assert.AreEqual(0, critical);
            Assert.AreEqual(0, terminated);
            Assert.AreEqual(Band.Band4, axes.GetBand(FearAxis.Trust));
        }

        [Test]
        public void 신뢰100뒤에도_감각축델타와_포획은_정상동작한다()
        {
            FearAxisSystem axes = new FearAxisSystem();
            int critical = 0;
            EventBus.AxisCritical += a => critical++;

            axes.Apply(FearAxis.Trust, 100, "setup", SpaceId.None);
            axes.Apply(FearAxis.Layout, 90, "setup", SpaceId.None);
            axes.Apply(FearAxis.Layout, 25, "H6", SpaceId.Corridor);

            Assert.AreEqual(100, axes.GetValue(FearAxis.Layout));
            Assert.IsTrue(axes.IsLocked);
            Assert.AreEqual(FearAxis.Layout, axes.Cause.Axis);
            Assert.AreEqual(1, critical);
        }

        [Test]
        public void 복원_신뢰100만으로는_잠그지않는다()
        {
            FearAxisSystem axes = new FearAxisSystem();
            axes.Restore(10, 20, 30, 100);
            Assert.IsFalse(axes.IsLocked);
            Assert.AreEqual(100, axes.GetValue(FearAxis.Trust));

            axes.Restore(10, 100, 30, 100);
            Assert.IsTrue(axes.IsLocked);
            Assert.AreEqual(FearAxis.Illuminance, axes.Cause.Axis);
        }

        [TestCase(FearAxis.Auditory, true)]
        [TestCase(FearAxis.Illuminance, true)]
        [TestCase(FearAxis.Layout, true)]
        [TestCase(FearAxis.Trust, false)]
        public void 포획축은_청각조도배치뿐이다(FearAxis axis, bool expected)
        {
            Assert.AreEqual(expected, FearAxisSystem.IsTerminal(axis));
        }

        [Test]
        public void 잠금뒤_다른축델타는_적용되지않는다()
        {
            FearAxisSystem axes = new FearAxisSystem();
            int critical = 0;
            EventBus.AxisCritical += a => critical++;

            axes.Apply(FearAxis.Layout, 100, "A", SpaceId.None);
            bool applied = axes.Apply(FearAxis.Trust, 2, "B", SpaceId.None);

            Assert.IsFalse(applied);
            Assert.AreEqual(0, axes.GetValue(FearAxis.Trust));
            Assert.AreEqual(1, critical);
        }

        [Test]
        public void 음수델타는_무시한다_감쇠없음()
        {
            FearAxisSystem axes = new FearAxisSystem();
            axes.Apply(FearAxis.Auditory, 30, "A", SpaceId.None);

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("음수 델타"));
            bool applied = axes.Apply(FearAxis.Auditory, -10, "B", SpaceId.None);

            Assert.IsFalse(applied);
            Assert.AreEqual(30, axes.GetValue(FearAxis.Auditory));
        }

        [TestCase(0, Band.Band0)]
        [TestCase(24, Band.Band0)]
        [TestCase(25, Band.Band1)]
        [TestCase(74, Band.Band2)]
        [TestCase(75, Band.Band3)]
        [TestCase(89, Band.Band3)]
        [TestCase(90, Band.Band4)]
        [TestCase(99, Band.Band4)]
        public void 구간표는_불균일하다(int value, Band expected)
        {
            Assert.AreEqual(expected, Bands.Of(value));
        }

        [Test]
        public void Band4상한은_99다_100은종료()
        {
            Assert.AreEqual(90, Bands.LowerBound(Band.Band4));
            Assert.AreEqual(99, Bands.UpperBound(Band.Band4));
            Assert.AreEqual(1f, Bands.Progress(99, Band.Band4));
        }

        [TestCase(Band.Band2, 46, Band.Band2)]
        [TestCase(Band.Band2, 45, Band.Band1)]
        [TestCase(Band.Band2, 20, Band.Band0)]
        [TestCase(Band.Band1, 50, Band.Band2)]
        public void 히스테리시스_하강은_하한빼기5(Band current, int value, Band expected)
        {
            Assert.AreEqual(expected, BandResolver.Resolve(current, value));
        }

        [Test]
        public void 보류중인공간은_구간변화를_미뤘다가_해제시반영한다()
        {
            FearAxisSystem axes = new FearAxisSystem();
            BandResolver resolver = new BandResolver(axes);
            axes.ValueChanged += resolver.OnValueChanged;

            int corridorChanges = 0;
            int toiletChanges = 0;
            EventBus.BandChanged += (space, axis, from, to) =>
            {
                if (from == to)
                {
                    return;
                }

                if (space == SpaceId.Corridor)
                {
                    corridorChanges++;
                }

                if (space == SpaceId.Toilet)
                {
                    toiletChanges++;
                }
            };

            resolver.SetHold(SpaceId.Corridor, true);
            axes.Apply(FearAxis.Layout, 25, "H4", SpaceId.Corridor);

            Assert.AreEqual(0, corridorChanges);
            Assert.AreEqual(1, toiletChanges);
            Assert.AreEqual(Band.Band0, resolver.GetShown(SpaceId.Corridor, FearAxis.Layout));

            resolver.SetHold(SpaceId.Corridor, false);

            Assert.AreEqual(1, corridorChanges);
            Assert.AreEqual(Band.Band1, resolver.GetShown(SpaceId.Corridor, FearAxis.Layout));
        }

        [Test]
        public void 신뢰축은_월드에_방송하지않는다()
        {
            FearAxisSystem axes = new FearAxisSystem();
            BandResolver resolver = new BandResolver(axes);
            axes.ValueChanged += resolver.OnValueChanged;

            int trustEvents = 0;
            EventBus.BandChanged += (space, axis, from, to) =>
            {
                if (axis == FearAxis.Trust)
                {
                    trustEvents++;
                }
            };

            axes.Apply(FearAxis.Trust, 30, "H1", SpaceId.Corridor);
            resolver.BroadcastAll();

            Assert.AreEqual(0, trustEvents);
        }
    }
}

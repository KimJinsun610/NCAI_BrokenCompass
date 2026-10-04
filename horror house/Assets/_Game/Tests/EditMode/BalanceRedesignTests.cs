using System.Collections.Generic;
using NUnit.Framework;

namespace NightDuty.Tests
{
    /// <summary>
    /// 일차 하한과 연출 구간(2026-09-30 새 기획서). 하한은 <b>연출 구간에만</b> 걸리고 생존 수치는 올리지 않는다.
    /// 연출 구간 = max(도달 구간, 일차 하한)이고 내려가지 않는다.
    /// </summary>
    public sealed class DayFloorTests
    {
        [TearDown]
        public void TearDown()
        {
            EventBus.ClearAll();
        }

        /// <summary>곡선은 Band0/Band0/Band1/Band1/Band2/Band3(0일차 포함). 값을 바꾸려면 DayFloor.Floors 배열 한 곳만 고친다.</summary>
        [TestCase(0, Band.Band0)]
        [TestCase(1, Band.Band0)]
        [TestCase(2, Band.Band1)]
        [TestCase(3, Band.Band1)]
        [TestCase(4, Band.Band2)]
        [TestCase(5, Band.Band3)]
        [TestCase(7, Band.Band3)]
        public void 일차별_연출하한은_0_0_1_1_2_3이다(int day, Band expected)
        {
            Assert.AreEqual(expected, DayFloor.Of(day));
        }

        [Test]
        public void 하한은_생존수치를_올리지_않는다()
        {
            FearAxisSystem axes = new FearAxisSystem();
            BandResolver bands = new BandResolver(axes);

            bands.SetDayFloor(DayFloor.Of(5));

            Assert.AreEqual(0, axes.GetValue(FearAxis.Layout));
            Assert.AreEqual(0, axes.GetValue(FearAxis.Auditory));
            Assert.AreEqual(0, axes.GetValue(FearAxis.Illuminance));
            Assert.AreEqual(Band.Band0, axes.GetBand(FearAxis.Layout), "생존 수치의 원시 구간은 그대로다");
            Assert.AreEqual(Band.Band3, bands.Shown.GetBand(FearAxis.Layout), "연출 구간은 하한까지 오른다");
            Assert.AreEqual(Band.Band3, bands.GetShown(SpaceId.Corridor, FearAxis.Auditory));
        }

        [Test]
        public void 신뢰는_하한_대상이_아니다()
        {
            FearAxisSystem axes = new FearAxisSystem();
            BandResolver bands = new BandResolver(axes);

            bands.SetDayFloor(DayFloor.Of(5));

            // 신뢰는 준수로만 오른다. 하한이 공짜로 신뢰를 올리면 역설 배급량이 저절로 늘어난다.
            Assert.AreEqual(Band.Band0, bands.Shown.GetBand(FearAxis.Trust));
        }

        [Test]
        public void 연출구간은_도달구간과_하한중_높은쪽이다()
        {
            FearAxisSystem axes = new FearAxisSystem();
            BandResolver bands = new BandResolver(axes);
            axes.ValueChanged += bands.OnValueChanged;

            axes.Apply(FearAxis.Layout, Bands.LowerBound(Band.Band2), "준비", SpaceId.None);
            bands.SetDayFloor(DayFloor.Of(2));

            Assert.AreEqual(Band.Band2, bands.Shown.GetBand(FearAxis.Layout), "도달 Band2 > 하한 Band1");
            Assert.AreEqual(Band.Band1, bands.Shown.GetBand(FearAxis.Auditory), "도달 Band0 < 하한 Band1");
        }

        [Test]
        public void 생존수치가_내려가도_연출구간은_내려가지_않는다()
        {
            FearAxisSystem axes = new FearAxisSystem();
            BandResolver bands = new BandResolver(axes);
            axes.ValueChanged += bands.OnValueChanged;

            int down = 0;
            EventBus.BandChanged += (space, axis, from, to) =>
            {
                if (to < from)
                {
                    down++;
                }
            };

            axes.Apply(FearAxis.Auditory, Bands.LowerBound(Band.Band1) + 2, "위반", SpaceId.None);
            Assert.IsTrue(axes.Lower(FearAxis.Auditory, Deltas.CorrectReportRelief, "보고"));

            Assert.AreEqual(Band.Band0, axes.GetBand(FearAxis.Auditory), "생존 수치는 Band0으로 내려갔다");
            Assert.AreEqual(Band.Band1, bands.Shown.GetBand(FearAxis.Auditory), "연출 구간은 남는다");
            Assert.AreEqual(Bands.LowerBound(Band.Band1) + 2, bands.Peak(FearAxis.Auditory));
            Assert.AreEqual(0, down, "내려가는 BandChanged는 한 번도 나가지 않는다");
        }

        [Test]
        public void 하한이_낮아져도_연출구간은_내려가지_않는다()
        {
            FearAxisSystem axes = new FearAxisSystem();
            BandResolver bands = new BandResolver(axes);

            bands.SetDayFloor(Band.Band2);
            bands.SetDayFloor(Band.Band0);

            Assert.AreEqual(Band.Band2, bands.GetShown(SpaceId.Toilet, FearAxis.Illuminance));
        }

        [Test]
        public void 신뢰는_Lower로_줄지_않는다()
        {
            FearAxisSystem axes = new FearAxisSystem();
            axes.Apply(FearAxis.Trust, 30, "준수", SpaceId.None);

            Assert.IsFalse(axes.Lower(FearAxis.Trust, 10, "시험"));
            Assert.AreEqual(30, axes.GetValue(FearAxis.Trust));
        }

        [Test]
        public void Lower는_0밑으로_내려가지_않고_잠금중엔_무시한다()
        {
            FearAxisSystem axes = new FearAxisSystem();
            axes.Apply(FearAxis.Layout, 3, "위반", SpaceId.None);
            Assert.IsTrue(axes.Lower(FearAxis.Layout, 10, "보고"));
            Assert.AreEqual(0, axes.GetValue(FearAxis.Layout));
            Assert.IsFalse(axes.Lower(FearAxis.Layout, 10, "보고"), "이미 0이면 바뀐 것이 없다");

            axes.Apply(FearAxis.Auditory, 100, "포획", SpaceId.None);
            Assert.IsTrue(axes.IsLocked);
            Assert.IsFalse(axes.Lower(FearAxis.Auditory, 10, "보고"));
            Assert.AreEqual(100, axes.GetValue(FearAxis.Auditory));
        }
    }

    // 2026-10-03: 옛 「하루 6장 배정」 테스트(DayDirectorTests)를 지웠다. 옛 24장 카드와 DayDirector를 함께 폐기했다 — 되살리지 마십시오.

    // 2026-10-01: 옛 「미방문 벌점 +9」 테스트(UnvisitedPenaltyTests)를 지웠다.
    // 최종 기획서에서 점검 미완료 경고가 그 역할을 넘겨받았다 — InspectionTests의 04:00 정산 테스트가 대신한다.

    /// <summary>
    /// 근무일지 줄 표시(최종 기획서 「근무일지 정산 화면」). <see cref="DutyLogEntry"/>는 순수 구조체라 직접 만들어 표시만 본다.
    /// 2026-10-03: 「가지 않은 공간도 어김」(옛 24장 규칙)을 없앴다 — 그 테스트들도 함께 지웠다.
    /// </summary>
    public sealed class DutyLogMarkTests
    {
        private static DutyLogEntry Entry(RuleVerdict verdict, bool instructed)
        {
            return new DutyLogEntry(1, "H1", SpaceId.Corridor, "테스트 본문", verdict, instructed);
        }

        [Test]
        public void 지킨_줄은_표시가_없다()
        {
            DutyLogEntry entry = Entry(RuleVerdict.Complied, false);

            Assert.IsFalse(entry.Struck);
            Assert.AreEqual(DutyMark.None, entry.Mark);
        }

        [Test]
        public void 방아쇠가_오지_않은_줄도_표시가_없다()
        {
            Assert.AreEqual(DutyMark.None, Entry(RuleVerdict.NotTriggered, false).Mark);
            Assert.AreEqual(DutyMark.None, Entry(RuleVerdict.NotTriggered, true).Mark, "문자를 받았어도 어기지 않았으면 표시 없음");
        }

        [Test]
        public void 그냥_어긴_줄은_어김이다()
        {
            DutyLogEntry entry = Entry(RuleVerdict.Violated, false);

            Assert.IsTrue(entry.Struck);
            Assert.AreEqual(DutyMark.Struck, entry.Mark);
        }

        [Test]
        public void 역설문자를_받고_어긴_줄은_지시를따름이다()
        {
            DutyLogEntry entry = Entry(RuleVerdict.Violated, true);

            // 수치 손해는 「어김」과 똑같다. 다른 것은 종이가 부르는 이름뿐이다.
            Assert.IsTrue(entry.Struck);
            Assert.AreEqual(DutyMark.Instructed, entry.Mark);
        }

        [Test]
        public void 문자를_받았어도_지켰으면_표시가_없다()
        {
            Assert.AreEqual(DutyMark.None, Entry(RuleVerdict.Complied, true).Mark);
        }
    }
}

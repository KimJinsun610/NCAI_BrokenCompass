using System.Collections.Generic;
using NUnit.Framework;

namespace NightDuty.Tests
{
    /// <summary>56차 손전등 배터리(설계 문서 「야간근무 손전등 배터리 설계안」).</summary>
    public sealed class BatteryTests
    {
        [SetUp]
        public void SetUp()
        {
            NightRun.StartNewRun();
            NightRun.JudgingWindowEnabled = false;
            NightRun.BatteryEnabled = true;
            NightRun.BatterySeed = 11;
        }

        [TearDown]
        public void TearDown()
        {
            NightRun.BatteryEnabled = false;
            NightRun.BatterySeed = null;
            NightRun.StartNewRun();
            EventBus.ClearAll();
        }

        private static List<BatteryCache> Caches(int plain, int lockers, int starters = 0)
        {
            List<BatteryCache> list = new List<BatteryCache>();
            for (int i = 0; i < plain; i++) list.Add(new BatteryCache("Interior/Corridors/Bookcase (" + i + ")", 1f + i * 0.1f));
            for (int i = 0; i < lockers; i++) list.Add(new BatteryCache("Interior/Corridors/LockerA (" + i + ")", 1.5f, true));
            for (int i = 0; i < starters; i++) list.Add(new BatteryCache("Interior/Classroom01/Drawer (" + i + ")", 1f, false, true));
            return list;
        }

        [Test]
        public void 완충은_켜_둔_채_160초이고_다_닳는_순간을_한_번_알린다()
        {
            FlashlightBattery b = new FlashlightBattery();
            Assert.AreEqual(160f, BatteryRules.FullSeconds, "59차: 밤 10분에 맞춰 240 → 160");
            Assert.IsFalse(b.Drain(BatteryRules.FullSeconds - 1f));
            Assert.Greater(b.Charge, 0f);
            Assert.IsTrue(b.Drain(1.5f), "이번에 다 닳았다");
            Assert.IsTrue(b.IsEmpty);
            Assert.IsFalse(b.Drain(5f), "이미 다 닳았으면 다시 알리지 않는다");
        }

        [Test]
        public void 예비는_둘까지_갈면_100퍼센트이고_남은_양은_버린다()
        {
            FlashlightBattery b = new FlashlightBattery();
            Assert.IsFalse(b.Swap(), "예비가 없으면 갈 수 없다");
            Assert.IsTrue(b.TryPocket());
            Assert.IsTrue(b.TryPocket());
            Assert.IsFalse(b.TryPocket(), "주머니는 둘까지");
            b.Drain(BatteryRules.FullSeconds * 0.7f);
            Assert.IsTrue(b.Swap());
            Assert.AreEqual(1f, b.Charge, 1e-5f);
            Assert.AreEqual(1, b.Spare);
        }

        [Test]
        public void 빛은_30퍼센트_아래부터_약해지고_10퍼센트_아래면_비춤_판정이_5m()
        {
            Assert.AreEqual(1f, BatteryRules.IntensityScale(0.5f));
            Assert.Less(BatteryRules.IntensityScale(0.15f), 1f);
            Assert.AreEqual(BatteryRules.DimIntensity, BatteryRules.IntensityScale(0f), 1e-5f);
            Assert.AreEqual(BatteryRules.DimRange, BatteryRules.RangeScale(0f), 1e-5f);
            Assert.AreEqual(SensingRules.BeamRange, BatteryRules.BeamRange(0.11f));
            Assert.AreEqual(5f, BatteryRules.BeamRange(0.09f));

            float min, max;
            BatteryRules.FlickerGap(0.5f, out min, out max);
            Assert.AreEqual(0f, max, "30% 위는 깜빡이지 않는다");
            BatteryRules.FlickerGap(0.2f, out min, out max);
            Assert.AreEqual(10f, min);   // 59차: 15~30 → 10~20
            BatteryRules.FlickerGap(0.05f, out min, out max);
            Assert.AreEqual(3f, min);
            BatteryRules.FlickerGap(0f, out min, out max);
            Assert.AreEqual(0f, max, "꺼진 손전등은 깜빡이지 않는다");

            UnityEngine.Vector3 eye = UnityEngine.Vector3.zero;
            Assert.IsTrue(SensingRules.InBeam(eye, UnityEngine.Vector3.forward, new UnityEngine.Vector3(0f, 0f, 7f)));
            Assert.IsFalse(SensingRules.InBeam(eye, UnityEngine.Vector3.forward, new UnityEngine.Vector3(0f, 0f, 7f), BatteryRules.LowBeamRange));
        }

        [Test]
        public void 태블릿_상태바는_세_칸과_예비_수()
        {
            Assert.AreEqual("●●○  ■■■ +2", BatteryRules.StatusText("●●○", 1f, 2));
            Assert.AreEqual("●●○  ■■□", BatteryRules.StatusText("●●○", 0.5f, 0));
            Assert.AreEqual("●●○  ■□□", BatteryRules.StatusText("●●○", 0.05f, 0));
            Assert.AreEqual("●●○  □□□ +1", BatteryRules.StatusText("●●○", 0f, 1));
        }

        [Test]
        public void 일차가_지날수록_놓이는_배터리가_준다()
        {
            int[] want = { 3, 3, 2, 2, 1 };
            for (int day = 1; day <= 5; day++) Assert.AreEqual(want[day - 1], BatteryRules.PlacedOn(day), day + "일차");
        }

        [Test]
        public void 일차_1은_순찰_공간_칸에_하나를_확정한다()
        {
            for (int seed = 0; seed < 20; seed++)
            {
                BatteryPlan plan = new BatteryPlan(1, null, new System.Random(seed));
                plan.Fill(Caches(12, 0, 1));
                Assert.AreEqual(3, plan.Placed.Count);
                Assert.Contains("Interior/Classroom01/Drawer (0)", new List<string>(plan.Placed), "씨앗 " + seed);
            }
        }

        [Test]
        public void 전날_놓였던_칸에는_다시_두지_않는다()
        {
            List<BatteryCache> caches = Caches(8, 0);
            BatteryPlan day2 = new BatteryPlan(2, null, new System.Random(3));
            day2.Fill(caches);
            BatteryPlan day3 = new BatteryPlan(3, day2.Placed, new System.Random(4));
            day3.Fill(caches);
            for (int i = 0; i < day3.Placed.Count; i++) CollectionAssert.DoesNotContain(day2.Placed, day3.Placed[i]);
        }

        [Test]
        public void 사물함은_밤마다_푼_세_개만_후보가_된다()
        {
            BatteryPlan plan = new BatteryPlan(2, null, new System.Random(5));
            plan.Fill(Caches(0, 10));
            Assert.AreEqual(BatteryRules.UnlockedLockers, plan.Unlocked.Count);
            for (int i = 0; i < plan.Placed.Count; i++) CollectionAssert.Contains(plan.Unlocked, plan.Placed[i]);
        }

        [Test]
        public void 재시작은_충전량_예비_주운_칸을_스냅샷대로_되돌린다()
        {
            NightRun.BeginNight(2, () => 30);
            Assert.IsNotNull(NightRun.Battery);
            NightRun.FillBatteryPlan(Caches(6, 0));
            BatteryPlan plan = NightRun.BatteryPlan;
            Assert.AreEqual(3, plan.Placed.Count);
            string first = plan.Placed[0];

            Assert.IsTrue(NightRun.TakeBattery(first));
            Assert.IsFalse(NightRun.TakeBattery(first), "같은 칸은 한 번");
            Assert.AreEqual(1, NightRun.Battery.Spare);
            NightRun.Battery.Drain(100f);

            NightRun.DebugForceCapture(FearAxis.Illuminance);
            Assert.AreEqual(RestartKind.FromNightStart, NightRun.RestartAfterCapture().Kind);
            Assert.AreSame(plan, NightRun.BatteryPlan, "재시작해도 자리는 다시 뽑지 않는다");
            Assert.IsTrue(plan.Holds(first), "밤 시작으로 — 주운 배터리가 칸에 돌아온다");
            Assert.AreEqual(0, NightRun.Battery.Spare);
            Assert.AreEqual(1f, NightRun.Battery.Charge, 1e-5f);
        }

        [Test]
        public void 다음_밤은_전날_충전량과_예비를_이어받는다()
        {
            // 66차(민: 「배터리는 일차가 바뀔 때 100%가 되지 않아 — 이전 배터리 상태가 계속 이월」). 전에는 밤마다 100%·예비 0.
            NightRun.BeginNight(1, () => 30);
            Assert.AreEqual(0f, NightRun.Battery.Charge, 1e-5f, "1일차 책상 손전등은 0%");
            NightRun.FillBatteryPlan(Caches(6, 0, 1));
            NightRun.TakeBattery(NightRun.BatteryPlan.Placed[0]);
            NightRun.Battery.DebugSet(0.4f, 1);
            NightRun.AbandonNight();

            NightRun.BeginNight(2, () => 30);
            Assert.AreEqual(0.4f, NightRun.Battery.Charge, 1e-5f);
            Assert.AreEqual(1, NightRun.Battery.Spare);
            Assert.AreEqual(0.4f, NightRun.NightStartCharge, 1e-5f);

            NightRun.Battery.Drain(500f);
            NightRun.DebugForceCapture(FearAxis.Illuminance);
            NightRun.RestartAfterCapture();
            Assert.AreEqual(0.4f, NightRun.Battery.Charge, 1e-5f, "재시작은 밤 시작 충전량까지 — 가득 채우지 않는다");
            Assert.IsFalse(NightRun.BatteryPlan.Filled, "칸은 화면 쪽이 그 밤 씬으로 채운다");
        }

        [Test]
        public void 꺼져_있으면_배터리가_없고_비춤_판정은_8m()
        {
            NightRun.BatteryEnabled = false;
            NightRun.BeginNight(1, () => 30);
            Assert.IsNull(NightRun.Battery);
            Assert.AreEqual(SensingRules.BeamRange, NightRun.BeamRange);
        }

        [Test]
        public void 눕힌_판자와_엎어_기댄_책장은_칸이_아니다()
        {
            // 56차 QA: 씬 실측 회전 — 복도 「Bookcase」 판자(눕혀 문에 박음)·문에 엎어 기댄 책장 / 벽에 기댄 사물함·교탁.
            UnityEngine.Vector3[] notCaches = { new UnityEngine.Vector3(0f, 180f, 93f), new UnityEngine.Vector3(1f, 270f, 96f), new UnityEngine.Vector3(19f, 270f, 0f) };
            UnityEngine.Vector3[] caches = { new UnityEngine.Vector3(0f, 0f, 12f), new UnityEngine.Vector3(0f, 270f, 0f), new UnityEngine.Vector3(360f, 94f, 1f) };
            foreach (UnityEngine.Vector3 e in notCaches) Assert.IsFalse(BatteryRules.Upright((UnityEngine.Quaternion.Euler(e) * UnityEngine.Vector3.up).y), e.ToString());
            foreach (UnityEngine.Vector3 e in caches) Assert.IsTrue(BatteryRules.Upright((UnityEngine.Quaternion.Euler(e) * UnityEngine.Vector3.up).y), e.ToString());
        }
    }

    /// <summary>56차 축 컨셉 재구성(민: 「배치에 어울리는 변화들이 조도에 가 있다」) — 몸 계기 조도 = 눈.</summary>
    public sealed class FiftySixthPassTests
    {
        [Test]
        public void 조도_몸_계기는_층이_오를수록_잦아지는_눈_깜빡임()
        {
            float min, max;
            BodyRules.BlinkGap(0, out min, out max);
            Assert.AreEqual(0f, max, "층 0은 깜빡이지 않는다");
            float lastMax = float.MaxValue;
            for (int tier = 1; tier <= 4; tier++)
            {
                BodyRules.BlinkGap(tier, out min, out max);
                Assert.Greater(max, 0f);
                Assert.Less(max, lastMax, "층 " + tier);
                lastMax = max;
            }

            Assert.AreEqual(0f, BodyRules.BlinkCover(0f));
            Assert.AreEqual(1f, BodyRules.BlinkCover(0.5f));
            Assert.AreEqual(0f, BodyRules.BlinkCover(1f));
            Assert.Greater(BodyRules.SlowBlinkSeconds, BodyRules.BlinkSeconds);
        }
    }
}

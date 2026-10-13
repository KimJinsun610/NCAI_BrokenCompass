using NUnit.Framework;

namespace NightDuty.Tests
{
    /// <summary>104차: 경비실 버티기 판단(GuardRoomLoiter).</summary>
    public sealed class GuardRoomLoiterTests
    {
        private static void Stay(GuardRoomLoiter g, float seconds, bool inside = true, bool far = false, bool counting = true, bool exempt = false, int day = 2)
        {
            for (float t = 0f; t < seconds - 1e-4f; t += 0.5f) g.Tick(0.5f, inside, far, counting, exempt, day);
        }

        [Test]
        public void 머문_시간에_따라_깜빡임_CCTV_얼굴_정전으로_간다()
        {
            GuardRoomLoiter g = new GuardRoomLoiter();
            Stay(g, 29f);
            Assert.AreEqual(LoiterStage.None, g.Stage);
            Stay(g, 2f);
            Assert.AreEqual(LoiterStage.Flicker, g.Stage);
            Stay(g, 30f);
            Assert.AreEqual(LoiterStage.CctvFace, g.Stage);
            Stay(g, 30f);
            Assert.AreEqual(LoiterStage.Blackout, g.Stage);
            Assert.IsFalse(g.ConsumeCapture());
            Stay(g, GuardRoomLoiter.CaptureGraceSeconds);
            Assert.IsTrue(g.ConsumeCapture(), "정전 뒤에도 남으면 붙잡힘");
            Assert.IsFalse(g.ConsumeCapture(), "한 번만");
        }

        [Test]
        public void 일일차는_정전까지만_붙잡지_않는다()
        {
            GuardRoomLoiter g = new GuardRoomLoiter();
            Stay(g, 200f, day: 1);
            Assert.AreEqual(LoiterStage.Blackout, g.Stage);
            Assert.IsFalse(g.ConsumeCapture());
        }

        [Test]
        public void 세지_않는_때와_면제_중에는_시간이_멈춘다()
        {
            GuardRoomLoiter g = new GuardRoomLoiter();
            Stay(g, 20f);
            Stay(g, 100f, counting: false);
            Assert.AreEqual(20f, g.Seconds, 0.01f, "세지 않는 때(출근 전·피날레·남은 일 없음)");
            Assert.AreEqual(LoiterStage.None, g.Stage);
            Stay(g, 50f, exempt: true);
            Assert.AreEqual(20f, g.Seconds, 0.01f, "전화·서명·경비실 점검 중");
        }

        [Test]
        public void 면제도_한도가_있어_일을_남겨_두고_버틸_수_없다()
        {
            GuardRoomLoiter g = new GuardRoomLoiter();
            Stay(g, GuardRoomLoiter.ExemptBudgetSeconds + 40f, exempt: true);
            Assert.AreEqual(40f, g.Seconds, 0.01f, "한도를 넘긴 만큼은 센다");
            g.NoteDutyDone();
            Stay(g, 10f, exempt: true);
            Assert.AreEqual(0f, g.Seconds, 0.01f, "지시를 끝내면 한도가 다시 찬다");
        }

        [Test]
        public void 문턱을_오가도_나간_것이_아니고_시간은_멈출_뿐이다()
        {
            GuardRoomLoiter g = new GuardRoomLoiter();
            Stay(g, 70f);
            for (int i = 0; i < 10; i++)
            {
                Stay(g, 2f, inside: false, far: true);   // 3초 안 되게 나갔다
                Stay(g, 1f);
            }

            Assert.AreEqual(80f, g.Seconds, 0.01f, "안에 있던 10초만 늘고 0이 되지 않는다");
            Assert.AreEqual(LoiterStage.CctvFace, g.Stage, "나간 것이 아니라 연출도 그대로");
        }

        [Test]
        public void 떨어진_밖은_이어서_3초여야_나간_것이다()
        {
            // 104차 QA: 문턱 띠(안도 떨어진 밖도 아님)를 거치면 「떨어진 밖」 시간은 처음부터 — 옛: 2초 + 문턱 + 1초가 나간 것으로 쌓였다.
            GuardRoomLoiter g = new GuardRoomLoiter();
            Stay(g, 40f);
            Stay(g, 2f, inside: false, far: true);
            Stay(g, 1f, inside: false, far: false);
            Stay(g, 2f, inside: false, far: true);
            Assert.AreEqual(LoiterStage.Flicker, g.Stage, "끊긴 2초 + 2초는 나간 것이 아니다");
            Stay(g, 1f, inside: false, far: true);
            Assert.AreEqual(LoiterStage.None, g.Stage, "이어서 3초면 나간 것");
        }

        [Test]
        public void 잠깐_나갔다_와도_0이_되지_않고_이어진다()
        {
            GuardRoomLoiter g = new GuardRoomLoiter();
            Stay(g, 50f);
            Stay(g, 20f, inside: false, far: true);
            Assert.AreEqual(LoiterStage.None, g.Stage, "나가면 연출은 꺼진다");
            Assert.AreEqual(50f, g.Seconds, 0.01f, "시간은 멈춤");
            Stay(g, 10f);
            Assert.AreEqual(LoiterStage.CctvFace, g.Stage, "돌아오면 이어서 — 5초 나갔다 오기로 얻는 게 없다");
        }

        [Test]
        public void 다른_공간에_가거나_지시를_끝내면_0이_된다()
        {
            GuardRoomLoiter g = new GuardRoomLoiter();
            Stay(g, 50f);
            g.NoteOtherSpace();
            Assert.AreEqual(0f, g.Seconds);
            Stay(g, 50f);
            g.NoteDutyDone();
            Assert.AreEqual(0f, g.Seconds);
        }

        [Test]
        public void 정전까지_간_뒤_금방_돌아오면_60초_단계부터()
        {
            GuardRoomLoiter g = new GuardRoomLoiter();
            Stay(g, 95f);
            Stay(g, 10f, inside: false, far: true);
            g.NoteOtherSpace();
            Assert.AreEqual(GuardRoomLoiter.FaceSeconds, g.Seconds, 0.01f, "기억 안 — 60초부터");
            Stay(g, 0.5f);
            Assert.AreEqual(LoiterStage.CctvFace, g.Stage);

            GuardRoomLoiter late = new GuardRoomLoiter();
            Stay(late, 95f);
            Stay(late, GuardRoomLoiter.MemorySeconds + 10f, inside: false, far: true);
            late.NoteOtherSpace();
            Assert.AreEqual(0f, late.Seconds, 0.01f, "기억이 지나면 처음부터");
        }

        [Test]
        public void 붙잡지_못하면_미루고_다시_판단한다()
        {
            GuardRoomLoiter g = new GuardRoomLoiter();
            Stay(g, GuardRoomLoiter.BlackoutSeconds + GuardRoomLoiter.CaptureGraceSeconds + 1f);
            Assert.IsTrue(g.ConsumeCapture());
            g.DeferCapture();
            Stay(g, 0.5f);
            Assert.IsTrue(g.ConsumeCapture(), "다른 연출 중이라 못 잡았으면 다음 프레임에 다시");
        }

        [Test]
        public void 밤이_새로_시작하면_처음으로()
        {
            GuardRoomLoiter g = new GuardRoomLoiter();
            Stay(g, 100f);
            g.Reset();
            Assert.AreEqual(0f, g.Seconds);
            Assert.AreEqual(LoiterStage.None, g.Stage);
            g.NoteOtherSpace();
            Assert.AreEqual(0f, g.Seconds, "재시작 뒤엔 정전 기억도 없다");
        }
    }
}

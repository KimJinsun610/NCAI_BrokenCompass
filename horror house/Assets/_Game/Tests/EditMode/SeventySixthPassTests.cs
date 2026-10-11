using System.Collections.Generic;
using NUnit.Framework;

namespace NightDuty.Tests
{
    /// <summary>
    /// 76차(2026-10-09) — 민: 「점검 지시 문구 변경」 · 「지시 사항들의 제한 시간도 폐기해줘」.
    /// </summary>
    public sealed class SeventySixthPassTests
    {
        [Test]
        public void 제한_시간은_기본으로_꺼져_있고_지시_문자에_마감_시각이_없다()
        {
            Assert.IsFalse(DutyCatalog.TimeLimitsEnabled, "76차: 게임 기본은 제한 없음");
            foreach (DutyDef d in DutyCatalog.All)
            {
                Assert.AreEqual(0f, DutyCatalog.EffectiveSeconds(d), d.Id);
                StringAssert.DoesNotContain("까지", DutyCatalog.MessageText(d, 1, 30f), d.Id);
            }

            InspectionOrder one = new InspectionOrder(1, OrderKind.Regular, 30, new List<string> { "H-1" }, SpaceId.Corridor);
            Assert.AreEqual(0f, NightRun.OrderLimitSeconds(one), "점검 지시도 제한 없음");
        }

        [Test]
        public void 제한이_없으면_오래_두어도_미완료가_되지_않고_판정_끝에_조용히_닫힌다()
        {
            DutyDispatcher d = new DutyDispatcher(1, s => 0f, 5);
            DutyEvent ev;
            Assert.IsTrue(d.Force("W3", out ev));   // 77차: W1 폐기
            Assert.AreEqual(DutyOutcome.Issued, ev.Outcome);
            Assert.IsFalse(d.Tick(new DutyInput { Minute = 40f, Dt = DutyCatalog.LimitSeconds * 3f }, out ev), "옛 120초를 훨씬 넘겨도 미완료 사건이 없다");
            Assert.IsNotNull(d.Active, "아직 진행 중");
            Assert.IsFalse(d.Tick(new DutyInput { Minute = NightClock.JudgingEnd + 1f, Dt = 0.1f }, out ev), "판정 끝 — 답장·경고 없이");
            Assert.IsNull(d.Active, "조용히 닫혔다");
        }

        [Test]
        public void 점검_문구는_민_문구다()
        {
            Dictionary<string, string> want = new Dictionary<string, string>
            {
                { "H-1", "화장실 쪽 복도 소화기의 상태를 보고해주십시오." },
                { "H-2", "과학실 쪽 복도 식수대의 상태를 보고해주십시오." },
                { "H-3", "경비실 쪽 복도 위 알림종이 꺼져 있는지 확인해주십시오." },
                { "H-4", "복도 쓰레기통의 상태를 보고해주십시오." },
                { "H-5", "도서관 옆 복도의 스피커가 꺼져 있는지 확인해주십시오." },
                { "H-6", "화장실 쪽 복도 벤치가 벽에 붙어 있는지 확인해주십시오." },
                { "C-1", "교실 화분이 제자리에 있는지 확인해주십시오." },
                { "C-2", "교실 책상 램프가 꺼져 있는지 확인해주십시오." },
                { "C-3", "교실 안쪽에 작은 사다리가 있는지 확인해주십시오." },
                { "C-4", "교실 시계가 제자리에 있는지 확인해주십시오." },
                { "C-5", "교사 의자가 교탁 안에 들어가 있는지 확인해주십시오." },
                { "S-1", "과학실 인체 모형이 정면을 향하는지 확인해주십시오." },
                { "S-2", "과학실 현미경이 꺼져 있는지 확인해주십시오." },
                { "S-3", "과학실 개수대가 잠겨 있는지 확인해주십시오." },
                { "S-4", "과학실 지구본의 상태를 보고해주십시오." },
                { "S-5", "과학실 선반의 플라스크의 상태를 보고해주십시오." },
                { "S-6", "과학실 세계 지도가 반듯한지 확인해주십시오." },
                { "T-1", "화장실 변기의 상태를 보고해주십시오." },
                { "T-2", "화장실 빈 칸의 상태를 보고해주십시오." },
                { "T-3", "화장실 거울 쪽 조명의 상태를 보고해주십시오." },
                { "T-5", "화장실 세면대 옆 수건이 칸에 걸려 있는지 확인해주십시오." },
                { "L-1", "도서관 의자가 책상 안에 들어가 있는지 확인해주십시오." },
                { "L-2", "도서관 입구 맞은편 창 블라인드 둘이 내려져 있는지 확인해주십시오." },
                { "L-5", "도서관 쓰레기통이 세워져 있는지 확인해주십시오." },
                { "K-1", "CCTV 모든 채널이 정상인지 확인해주십시오." },
                { "K-2", "접이식 의자 둘이 같은 쪽을 보는지 확인해주십시오." },
                { "K-3", "경비실 화분이 제자리인지 확인해주십시오." }
            };
            Assert.AreEqual(want.Count, InspectionCatalog.All.Count);
            foreach (InspectionItem item in InspectionCatalog.All) Assert.AreEqual(want[item.Id], item.TabletLine, item.Id);
        }
    }
}

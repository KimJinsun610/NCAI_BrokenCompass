using System.Collections.Generic;
using InstructionDT;
using NUnit.Framework;

namespace NightDuty.Tests
{
    /// <summary>
    /// 3단계 Fx: 코드가 이름으로 읽는 Fx 칸이 모두 있고 비어 있지 않은지, 바꾸기 전 코드 상수와 값이 같은지(차이 0),
    /// SO 에셋이 표와 같은지(굽기 대조).
    /// </summary>
    public class DesignDataFxTests
    {
        static readonly (uint id, string name)[] Used =
        {
            (420201u, "깜빡이는 등 비율"),
            (420201u, "좌측 복도 소등 개수"),
            (420201u, "천장 등 붉은 섞음"),
            (420202u, "깜빡이는 등 비율"),
            (420202u, "좌측 복도 소등 개수"),
            (420202u, "천장 등 붉은 섞음"),
            (420203u, "깜빡이는 등 비율"),
            (420203u, "좌측 복도 소등 개수"),
            (420203u, "천장 등 붉은 섞음"),
            (420204u, "깜빡이는 등 비율"),
            (420204u, "좌측 복도 소등 개수"),
            (420204u, "천장 등 붉은 섞음"),
            (420205u, "깜빡이는 등 비율"),
            (420205u, "좌측 복도 소등 개수"),
            (420205u, "천장 등 붉은 섞음"),
            (420501u, "밤당 횟수"),
            (420502u, "밤당 횟수"),
            (420503u, "밤당 횟수"),
            (420504u, "밤당 횟수"),
            (420505u, "밤당 횟수"),
            (420601u, "광과민 모드 깜빡임(Hz)"),
            (420601u, "깜빡임(Hz)"),
            (420601u, "먼 책상(비율)"),
            (420601u, "문 각도(°)"),
            (420601u, "번짐(m)"),
            (420601u, "빛 세기(배)"),
            (420601u, "의자 빠짐(m)"),
            (420601u, "회전(°)"),
            (420602u, "광과민 모드 깜빡임(Hz)"),
            (420602u, "깜빡임(Hz)"),
            (420602u, "먼 책상(비율)"),
            (420602u, "문 각도(°)"),
            (420602u, "번짐(m)"),
            (420602u, "빛 세기(배)"),
            (420602u, "의자 빠짐(m)"),
            (420602u, "회전(°)"),
            (420603u, "광과민 모드 깜빡임(Hz)"),
            (420603u, "깜빡임(Hz)"),
            (420603u, "먼 책상(비율)"),
            (420603u, "문 각도(°)"),
            (420603u, "번짐(m)"),
            (420603u, "빛 세기(배)"),
            (420603u, "의자 빠짐(m)"),
            (420603u, "회전(°)"),
            (420604u, "광과민 모드 깜빡임(Hz)"),
            (420604u, "깜빡임(Hz)"),
            (420604u, "먼 책상(비율)"),
            (420604u, "문 각도(°)"),
            (420604u, "번짐(m)"),
            (420604u, "빛 세기(배)"),
            (420604u, "의자 빠짐(m)"),
            (420604u, "회전(°)"),
            (420605u, "광과민 모드 깜빡임(Hz)"),
            (420605u, "깜빡임(Hz)"),
            (420605u, "먼 책상(비율)"),
            (420605u, "문 각도(°)"),
            (420605u, "번짐(m)"),
            (420605u, "빛 세기(배)"),
            (420605u, "의자 빠짐(m)"),
            (420605u, "회전(°)"),
            (420606u, "[빛] 이상 세기(배)"),
            (420606u, "[빛] 이상 후광(1=켬)"),
            (420606u, "[옮김] 최소 회전(°)"),
            (420801u, "층 하한(생존 수치)"),
            (420802u, "층 하한(생존 수치)"),
            (420803u, "층 하한(생존 수치)"),
            (420804u, "층 하한(생존 수치)"),
            (420805u, "[배치] 에코가 한 박 늦는 층"),
            (420805u, "[청각] 귀 먹먹함 시작 층"),
            (420805u, "[청각] 이명 시작 층"),
            (420805u, "[청각] 화면 박동 흔들림 시작 층"),
            (420805u, "경계 신호 1(생존 수치)"),
            (420805u, "경계 신호 2(생존 수치)"),
            (420805u, "경계 신호 느린 깜빡임(초)"),
            (420805u, "눈 깜빡임 한 번(초)"),
            (420805u, "재시작 뒤 약하게(초)"),
            (420902u, "꺼짐 1 끝(초)"),
            (420902u, "꺼짐 1 시작(초)"),
            (420902u, "꺼짐 2 끝(초)"),
            (420902u, "꺼짐 2 시작(초)"),
            (420902u, "꺼짐 3 끝(초)"),
            (420902u, "꺼짐 3 시작(초)"),
            (421001u, "0% 근처 빛 거리 배율"),
            (421001u, "0% 근처 빛 세기 배율"),
            (421001u, "0~10% 깜빡임 간격 최대(초)"),
            (421001u, "0~10% 깜빡임 간격 최소(초)"),
            (421001u, "10~30% 깜빡임 간격 최대(초)"),
            (421001u, "10~30% 깜빡임 간격 최소(초)"),
            (421001u, "가짜 끊김 놀람 최소 충전량"),
            (421001u, "누런빛 색 B"),
            (421001u, "누런빛 색 G"),
            (421001u, "누런빛 색 R"),
            (421001u, "다 닳았을 때 켜기 시도 빛(초)"),
            (421001u, "태블릿 상태바 칸 수"),
            (421002u, "근접 외곽선 거리(m)"),
            (421002u, "근접 외곽선 진하기(0~1)"),
            (421002u, "반짝임 길이(초)"),
            (421002u, "반짝임 보이는 거리(m)"),
            (421002u, "반짝임 주기(초)"),
            (421002u, "손전등 비출 때 반짝임 거리(m)"),
            (421002u, "책상 손전등 외곽선 거리(m)"),
            (421002u, "책상 안내 문구 표시(초)"),
            (421101u, "걸음 폭 걷기(m)"),
            (421101u, "걸음 폭 뛰기(m)"),
            (421101u, "메아리 지연(초)"),
            (421101u, "볼륨 걷기(0~1)"),
            (421101u, "볼륨 뛰기(0~1)"),
            (421201u, "사용(1=예)"),
            (421201u, "시작 일차"),
            (421202u, "사용(1=예)"),
            (421202u, "시작 일차"),
            (421203u, "사용(1=예)"),
            (421203u, "시작 일차"),
            (421204u, "사용(1=예)"),
            (421204u, "시작 일차"),
            (421205u, "사용(1=예)"),
            (421401u, "끝 X"),
            (421401u, "끝 Y"),
            (421401u, "끝 Z"),
            (421401u, "시작 X"),
            (421401u, "시작 Y"),
            (421401u, "시작 Z"),
            (421401u, "카메라 번호"),
            (421402u, "끝 X"),
            (421402u, "끝 Y"),
            (421402u, "끝 Z"),
            (421402u, "시작 X"),
            (421402u, "시작 Y"),
            (421402u, "시작 Z"),
            (421402u, "카메라 번호"),
            (421403u, "끝 X"),
            (421403u, "끝 Y"),
            (421403u, "끝 Z"),
            (421403u, "시작 X"),
            (421403u, "시작 Y"),
            (421403u, "시작 Z"),
            (421403u, "카메라 번호"),
            (421404u, "끝 X"),
            (421404u, "끝 Y"),
            (421404u, "끝 Z"),
            (421404u, "시작 X"),
            (421404u, "시작 Y"),
            (421404u, "시작 Z"),
            (421404u, "카메라 번호"),
            (421405u, "끝 X"),
            (421405u, "끝 Y"),
            (421405u, "끝 Z"),
            (421405u, "시작 X"),
            (421405u, "시작 Y"),
            (421405u, "시작 Z"),
            (421405u, "카메라 번호"),
            (421406u, "끝 X"),
            (421406u, "끝 Y"),
            (421406u, "끝 Z"),
            (421406u, "시작 X"),
            (421406u, "시작 Y"),
            (421406u, "시작 Z"),
            (421406u, "카메라 번호"),
            (421603u, "괴이문자→「들여보내줘」 바뀜(초)"),
            (421801u, "등장 시 키(m)"),
            (421801u, "배치 100일 때 키(m)"),
            (421901u, "표시(1=켬)")
        };

        [Test]
        public void 코드가_읽는_Fx_칸은_모두_있고_비어_있지_않다()
        {
            var missing = new List<string>();
            foreach (var (id, name) in Used)
            {
                try { DesignData.Fx(id, name); }
                catch (System.Exception e) { missing.Add(e.Message); }
            }

            Assert.IsEmpty(missing, string.Join("\n", missing));
        }

        [Test]
        public void 바꾸기_전_코드_상수와_같다()
        {
            // 조도 조명
            CollectionAssert.AreEqual(new[] { 0f, 0.08f, 0.16f, 0.26f, 0.36f }, DesignData.FxSeries(420201, 5, "천장 등 붉은 섞음"));
            Assert.AreEqual(0.36f, IlluminanceTint.Amount(Band.Band4), 1e-5f);
            Assert.AreEqual(4, IlluminanceTint.AccentCount(10, Band.Band4));   // 10 × 0.4
            Assert.AreEqual(0, IlluminanceTint.AccentCount(10, Band.Band2));
            // 점검 이상 세기
            float[] door = { 0f, 20f, 35f, 55f, 85f }, turn = { 0f, 20f, 45f, 100f, 180f }, pull = { 0f, 0.35f, 0.45f, 0.55f, 0.7f };
            for (int b = 0; b < 5; b++)
            {
                Assert.AreEqual(door[b], AnomalyLook.DoorDegrees((Band)b), 1e-5f);
                Assert.AreEqual(turn[b], AnomalyLook.TurnDegrees((Band)b), 1e-5f);
                Assert.AreEqual(pull[b], AnomalyLook.PullMeters((Band)b), 1e-5f);
            }

            Assert.AreEqual(15f, AnomalyLook.MinMoveDegrees, 1e-5f);
            // 신체 신호
            CollectionAssert.AreEqual(new[] { 25, 50, 75, 90 }, BodyRules.TierFloors);
            CollectionAssert.AreEqual(new[] { 70, 85 }, BodyRules.Boundaries);
            Assert.AreEqual(30f, BodyRules.RestartWeakSeconds, 1e-5f);
            int[] heart = { 0, 62, 72, 86, 104 }, echo = { 0, 1, 2, 3, 3 };
            float[] breath = { 0f, 10f, 8f, 6f, 4.5f }, spread = { 1f, 1f, 1.15f, 1.3f, 1.3f }, gapMin = { 0f, 70f, 40f, 22f, 10f }, gapMax = { 0f, 110f, 70f, 40f, 20f };
            for (int t = 0; t <= 4; t++)
            {
                Assert.AreEqual(heart[t], BodyRules.HeartRate(t), "심박 " + t);
                Assert.AreEqual(echo[t], BodyRules.EchoCount(t), "에코 " + t);
                Assert.AreEqual(breath[t], BodyRules.BreathInterval(t), 1e-5f, "숨 " + t);
                Assert.AreEqual(spread[t], BodyRules.EchoSpread(t), 1e-5f, "에코 간격 " + t);
                BodyRules.BlinkGap(t, out float mn, out float mx);
                Assert.AreEqual(gapMin[t], mn, 1e-5f);
                Assert.AreEqual(gapMax[t], mx, 1e-5f);
                Assert.AreEqual(t >= 2, BodyRules.EarPressure(t));
                Assert.AreEqual(t >= 3, BodyRules.Tinnitus(t));
                Assert.AreEqual(t >= 4, BodyRules.PulseShake(t));
                Assert.AreEqual(t >= 4, BodyRules.EchoLate(t));
            }

            Assert.AreEqual(0.12f, BodyRules.BlinkSeconds, 1e-5f);
            Assert.AreEqual(0.6f, BodyRules.SlowBlinkSeconds, 1e-5f);
            // 배터리
            Assert.AreEqual(0.45f, BatteryRules.DimIntensity, 1e-5f);
            Assert.AreEqual(0.6f, BatteryRules.DimRange, 1e-5f);
            Assert.AreEqual(0.5f, BatteryRules.FakeFlickerMinCharge, 1e-5f);
            Assert.AreEqual(3, BatteryRules.Cells(1f));
            Assert.AreEqual(1, BatteryRules.Cells(0.2f));
            // 가짜 놀람
            Assert.AreEqual(2, TensionDirector.FakeStartDay(TensionDirector.FakeGlimpse));
            Assert.AreEqual(1, TensionDirector.FakeStartDay("fake.locker.rattle"));
            // CCTV 인물 경로
            CctvSpot far = null;
            foreach (CctvSpot s in CctvSpots.All) if (s.Id == "cctv.hall.far") far = s;
            Assert.IsNotNull(far);
            Assert.AreEqual(0, far.Channel);
            Assert.AreEqual(31.4f, far.From.x, 1e-4f);
            Assert.AreEqual(42.4f, far.To.z, 1e-4f);
            // 피날레 바뀜 시간, 엔딩 연출 행과 문자 시간이 같다
            Assert.AreEqual(2f, DesignData.Fx(421603, "괴이문자→「들여보내줘」 바뀜(초)"), 1e-5f);
            Assert.AreEqual(DesignData.MessageRow(140021).Delay_Sec, DesignData.Fx(421603, "강제 복귀 암전(초)"), 1e-5f);
            Assert.AreEqual(DesignData.MessageRow(140023).Repeat_Duration_Sec - 2f, DesignData.Fx(421603, "「들여보내줘」 유지(초)"), 1e-5f);
        }

        [Test]
        public void SO_에셋은_표와_같다()
        {
            System.Type baker = System.Type.GetType("DTFxBaker, Assembly-CSharp-Editor");
            Assert.IsNotNull(baker, "DTFxBaker");
            var log = new System.Text.StringBuilder();
            int n = (int)baker.GetMethod("Run").Invoke(null, new object[] { false, log });
            Assert.AreEqual(0, n, "Tools/DT/Fx 굽기 (Bake)를 다시 누르십시오\n" + log);
        }
    }
}

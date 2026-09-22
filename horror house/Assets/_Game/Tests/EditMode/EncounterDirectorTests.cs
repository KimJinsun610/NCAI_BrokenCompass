using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace NightDuty.Tests
{
    /// <summary>
    /// 테스트용 조우 표를 코드로 만든다. <b>Resources를 쓰지 않는다</b> —
    /// 에셋이 아직 없는 동안에도 조우 규칙이 굳어 있어야 하고, 에셋 유무로 테스트가 흔들리면
    /// 「규칙이 틀린 것」과 「에셋이 없는 것」을 구분할 수 없게 된다.
    /// </summary>
    internal static class EncounterTestTable
    {
        /// <summary>접근 지점 대상 ID. 단계는 1부터 센다(표의 ApproachOf와 같은 약속).</summary>
        internal static string ApproachTargetId(string sceneId, int step)
        {
            return sceneId + ".p" + step;
        }

        /// <summary>퇴실 게이트 지점 대상 ID.</summary>
        internal static string GateTargetId(string sceneId)
        {
            return sceneId + ".gate";
        }

        /// <summary>장면 ID가 벌어지는 공간. H=복도 C=교실1-1 S=과학실 T=화장실.</summary>
        internal static SpaceId SpaceOf(string sceneId)
        {
            if (sceneId == EncounterDirector.SceneHA || sceneId == EncounterDirector.SceneHB)
            {
                return SpaceId.Corridor;
            }

            if (sceneId == EncounterDirector.SceneCA || sceneId == EncounterDirector.SceneCB)
            {
                return SpaceId.Classroom_1_1;
            }

            if (sceneId == EncounterDirector.SceneSA || sceneId == EncounterDirector.SceneSB)
            {
                return SpaceId.ScienceRoom;
            }

            return SpaceId.Toilet;
        }

        /// <summary>설계를 그대로 지킨 8장면 표. 유도 문자는 S-A만 비우고, 게이트는 게이트 장면 셋에만 붙인다.</summary>
        internal static EncounterTableSO Create()
        {
            EncounterTableSO table = ScriptableObject.CreateInstance<EncounterTableSO>();
            table.name = "테스트조우표";

            EncounterTableSO.Scene[] scenes = new EncounterTableSO.Scene[EncounterDirector.AllSceneIds.Length];
            for (int i = 0; i < EncounterDirector.AllSceneIds.Length; i++)
            {
                scenes[i] = MakeScene(EncounterDirector.AllSceneIds[i]);
            }

            table.SetScenes(scenes);
            table.SetNotice(EncounterDirector.NoticeMessageId, "복도를 한 번 더 지나 주십시오.", SpaceId.Corridor);
            return table;
        }

        private static EncounterTableSO.Scene MakeScene(string sceneId)
        {
            EncounterTableSO.Scene scene = new EncounterTableSO.Scene();
            scene.SceneId = sceneId;
            scene.Space = SpaceOf(sceneId);
            scene.IsDiscovery = EncounterDirector.IsDiscovery(sceneId);

            if (sceneId != EncounterDirector.FirstDaySceneId)
            {
                scene.MessageId = "G-" + sceneId;
                scene.MessageText = sceneId + " 쪽을 한 번 보고 오십시오.";
            }

            string[] approach = new string[EncounterTableSO.ApproachCount];
            for (int k = 0; k < approach.Length; k++)
            {
                approach[k] = ApproachTargetId(sceneId, k + 1);
            }

            scene.ApproachTargetIds = approach;

            if (EncounterDirector.UsesExitGate(sceneId))
            {
                scene.ExitGateTargetId = GateTargetId(sceneId);
            }

            return scene;
        }
    }

    /// <summary>
    /// 조우 연출기 한 벌(표 · 연출기 · 배치 기록 · 문자 기록). <see cref="Dispose"/>에서
    /// <b>EventBus 구독과 에셋을 되돌린다</b> — 구독이 남으면 다음 테스트 파일이 이 목록에 계속 적는다.
    /// </summary>
    internal sealed class EncounterFixture : IDisposable
    {
        /// <summary>고정 시드. 배분 자체는 난수를 쓰지 않지만 하루 안의 순서가 매번 같아야 진단이 쉽다.</summary>
        internal const int Seed = 20260922;

        private readonly Action<ParadoxMessage> _onMessage;

        internal EncounterTableSO Table { get; private set; }

        internal EncounterDirector Director { get; private set; }

        /// <summary>PlaceModel이 불린 장면 ID(호출 순서).</summary>
        internal List<string> PlacedScenes { get; private set; }

        /// <summary>PlaceModel이 불린 대상 ID(호출 순서). <see cref="PlacedScenes"/>와 같은 인덱스다.</summary>
        internal List<string> PlacedTargets { get; private set; }

        /// <summary>EventBus로 나간 문자 전부.</summary>
        internal List<ParadoxMessage> Messages { get; private set; }

        internal EncounterFixture() : this(Seed)
        {
        }

        /// <summary>시드를 지정해 만든다. 회차 배분이 회차마다 달라지므로, 배분 검사는 여러 시드로 돌려야 한다.</summary>
        internal EncounterFixture(int seed)
        {
            Table = EncounterTestTable.Create();
            Director = new EncounterDirector(Table, new System.Random(seed));

            PlacedScenes = new List<string>();
            PlacedTargets = new List<string>();
            Messages = new List<ParadoxMessage>();

            Director.PlaceModel = delegate (string sceneId, string targetId)
            {
                PlacedScenes.Add(sceneId);
                PlacedTargets.Add(targetId);
            };

            // 기본은 「안 보임」이다. 시야 규약 테스트만 이것을 바꾼다.
            Director.IsVisible = delegate (string sceneId) { return false; };

            _onMessage = delegate (ParadoxMessage message) { Messages.Add(message); };
            EventBus.MessageSent += _onMessage;
        }

        /// <summary>일반 점검 완료.</summary>
        internal void Inspect(SpaceId space)
        {
            Director.Observe(JudgeSignal.OfSpace(SignalKind.InspectionCompleted, space));
        }

        /// <summary>공간 퇴실.</summary>
        internal void Exit(SpaceId space)
        {
            Director.Observe(JudgeSignal.OfSpace(SignalKind.SpaceExited, space));
        }

        /// <summary>모형 1초 관찰.</summary>
        internal void ObserveModel(string sceneId)
        {
            Director.Observe(JudgeSignal.Target(SignalKind.ModelObserved, sceneId));
        }

        /// <summary>아무 일도 안 일어난 시간 경과. 대기 중인 배치를 다시 밀어 보는 용도다.</summary>
        internal void Tick()
        {
            Director.Observe(JudgeSignal.Tick(0.1f));
        }

        /// <summary>그 문자 ID가 나간 횟수.</summary>
        internal int CountOf(string messageId)
        {
            int n = 0;
            for (int i = 0; i < Messages.Count; i++)
            {
                if (Messages[i].ParadoxId == messageId)
                {
                    n++;
                }
            }

            return n;
        }

        public void Dispose()
        {
            EventBus.MessageSent -= _onMessage;
            EventBus.ClearAll();

            if (Director != null)
            {
                Director.PlaceModel = null;
                Director.IsVisible = null;
                Director.ParadoxSentToday = null;
                Director.ClockMinutes = null;
                Director = null;
            }

            if (Table != null)
            {
                UnityEngine.Object.DestroyImmediate(Table);
                Table = null;
            }
        }
    }

    /// <summary>
    /// A. 배분표 — 8장면이 5일에 전부, 한 번씩 깔리는가(재설계안 8절 「조우 8장면 전소진」).
    /// </summary>
    public sealed class EncounterPlanTests
    {
        /// <summary>배분 검사에 쓸 회차 수. 배분이 무작위라 한 시드만으로는 제약이 지켜지는지 알 수 없다.</summary>
        private const int RunSamples = 40;

        private EncounterFixture _fx;

        /// <summary>표본 회차의 시드. 실패하면 이 수를 그대로 재현에 쓸 수 있게 단순한 식으로 둔다.</summary>
        private static int SeedOf(int run)
        {
            return EncounterFixture.Seed + run;
        }

        [SetUp]
        public void SetUp()
        {
            _fx = new EncounterFixture();
        }

        [TearDown]
        public void TearDown()
        {
            _fx.Dispose();
            _fx = null;
        }

        /// <summary>
        /// 배분이 특정 장면을 빠뜨리거나 두 번 싣는 것을 막는다 — 안 나온 장면은 그 회차에 영영 못 본 콘텐츠다.
        /// <para>배분이 회차마다 달라지므로 <b>여러 시드</b>로 돌린다. 한 시드만 보면 우연히 통과한다.</para>
        /// </summary>
        [Test]
        public void 여덟_장면이_다섯_일차에_중복_없이_전부_배분된다()
        {
            for (int run = 0; run < RunSamples; run++)
            {
                using (EncounterFixture fx = new EncounterFixture(SeedOf(run)))
                {
                    fx.Director.BeginRun();

                    List<string> seen = new List<string>();

                    for (int day = 1; day <= EncounterDirector.LastDay; day++)
                    {
                        IReadOnlyList<string> plan = fx.Director.PlanFor(day);

                        Assert.AreEqual(EncounterDirector.SlotsOf(day), plan.Count,
                                        "시드 " + SeedOf(run) + "의 " + day + "일차 장면 수가 " + EncounterDirector.SlotsOf(day) + "이 아니다");

                        for (int i = 0; i < plan.Count; i++)
                        {
                            Assert.IsTrue(EncounterDirector.IsSceneId(plan[i]),
                                          day + "일차에 조우 장면이 아닌 ID가 있다: " + plan[i]);
                            Assert.IsFalse(seen.Contains(plan[i]), plan[i] + "이(가) 두 번 배분됐다(시드 " + SeedOf(run) + ")");
                            seen.Add(plan[i]);
                        }
                    }

                    Assert.AreEqual(EncounterDirector.SceneCount, seen.Count,
                                    "5일에 깔리는 장면 수가 8이 아니다(전소진이 깨진다. 시드 " + SeedOf(run) + ")");

                    for (int i = 0; i < EncounterDirector.AllSceneIds.Length; i++)
                    {
                        Assert.IsTrue(seen.Contains(EncounterDirector.AllSceneIds[i]),
                                      EncounterDirector.AllSceneIds[i] + "이(가) 회차 어느 날에도 배분되지 않았다(시드 " + SeedOf(run) + ")");
                    }
                }
            }
        }

        /// <summary>1일차에 장면이 둘 이상 깔려 최초 조우가 흐려지는 것을 막는다.</summary>
        [Test]
        public void 일일차는_S_A_하나뿐이다()
        {
            Assert.AreEqual(EncounterDirector.SceneSA, EncounterDirector.FirstDaySceneId, "1일차 고정 장면은 과학실 A다");

            for (int run = 0; run < RunSamples; run++)
            {
                using (EncounterFixture fx = new EncounterFixture(SeedOf(run)))
                {
                    fx.Director.BeginRun();
                    IReadOnlyList<string> plan = fx.Director.PlanFor(1);

                    Assert.AreEqual(1, plan.Count, "1일차는 「모형이라는 것이 있다」만 가르치는 밤이라 한 장면뿐이다");
                    Assert.AreEqual(EncounterDirector.FirstDaySceneId, plan[0],
                                    "시드 " + SeedOf(run) + "에서 1일차가 과학실 A가 아니다 — 기획서 §3-3이 못박은 자리다");
                }
            }
        }

        /// <summary>발견형 3장이 앞쪽으로 당겨지는 것을 막는다 — 잘하는 플레이어가 수치 손해 없이 겪는 유일한 공포가 후반에 남아야 한다.</summary>
        [Test]
        public void 발견형_세_장면은_삼사오일차에_한_장씩_심는다()
        {
            Assert.AreEqual(3, EncounterDirector.DiscoverySceneIds.Length, "발견형은 셋이다");

            // 어느 셋인지도 못박는다. 셋의 구성이 바뀌면 「델타 0인 공포」의 총량 자체가 달라진다.
            Assert.IsTrue(EncounterDirector.IsDiscovery(EncounterDirector.SceneHB));
            Assert.IsTrue(EncounterDirector.IsDiscovery(EncounterDirector.SceneCB));
            Assert.IsTrue(EncounterDirector.IsDiscovery(EncounterDirector.SceneTB));

            for (int run = 0; run < RunSamples; run++)
            {
                using (EncounterFixture fx = new EncounterFixture(SeedOf(run)))
                {
                    fx.Director.BeginRun();

                    List<int> days = new List<int>();

                    for (int i = 0; i < EncounterDirector.DiscoverySceneIds.Length; i++)
                    {
                        string sceneId = EncounterDirector.DiscoverySceneIds[i];
                        int day = DayOf(fx.Director, sceneId);

                        Assert.AreNotEqual(-1, day, sceneId + "이(가) 배분에 없다(시드 " + SeedOf(run) + ")");
                        Assert.GreaterOrEqual(day, 3, sceneId + "이(가) " + day + "일차에 있다. 발견형은 3일차부터다");
                        Assert.IsFalse(days.Contains(day), day + "일차에 발견형이 둘이다. 3·4·5일에 한 장씩이어야 한다");

                        days.Add(day);
                    }

                    Assert.AreEqual(3, days.Count);
                    Assert.IsTrue(days.Contains(3) && days.Contains(4) && days.Contains(5),
                                  "발견형은 3·4·5일에 하나씩 있어야 한다(시드 " + SeedOf(run) + ")");
                }
            }
        }

        /// <summary>
        /// <b>날짜별 공간이 고정되지 않는다</b>(시나리오 기획서 v6 §4). 2026-09-22까지는 고정표였고,
        /// 그때는 「3일차는 화장실」을 두 번째 회차에 이미 알 수 있었다.
        /// <para>
        /// 2일차는 칸이 하나뿐이라 가장 좁은 자리다 — 여기가 갈리면 나머지도 갈린다.
        /// </para>
        /// </summary>
        [Test]
        public void 날짜별_장면이_회차마다_달라진다()
        {
            List<string> secondDay = new List<string>();

            for (int run = 0; run < RunSamples; run++)
            {
                using (EncounterFixture fx = new EncounterFixture(SeedOf(run)))
                {
                    fx.Director.BeginRun();
                    IReadOnlyList<string> plan = fx.Director.PlanFor(2);

                    Assert.AreEqual(1, plan.Count);
                    if (!secondDay.Contains(plan[0]))
                    {
                        secondDay.Add(plan[0]);
                    }
                }
            }

            Assert.Greater(secondDay.Count, 1,
                           "시드를 " + RunSamples + "개 돌려도 2일차 장면이 " + string.Join(", ", secondDay.ToArray()) +
                           " 하나뿐이다. 배분이 사실상 고정됐다는 뜻이다");
        }

        /// <summary>이월이 겹쳐 하룻밤에 모형을 네 번 이상 마주치는 것을 막는다 — 그 이상은 공포가 아니라 소란이다.</summary>
        [Test]
        public void 하루_장면_수는_이월이_쌓여도_상한을_넘지_않는다()
        {
            _fx.Director.BeginRun();

            // 아무것도 관찰하지 않는다 = 매일 전부 이월된다. 상한이 없으면 여기서 오늘 목록이 계속 불어난다.
            for (int day = 1; day <= EncounterDirector.LastDay + 2; day++)
            {
                _fx.Director.BeginNight(day);

                Assert.LessOrEqual(_fx.Director.TodayScenes.Count, EncounterDirector.MaxScenesPerNight,
                                   day + "일차에 장면이 " + _fx.Director.TodayScenes.Count + "개 깔렸다. 상한은 " +
                                   EncounterDirector.MaxScenesPerNight + "이다");

                _fx.Inspect(SpaceId.Corridor);
                _fx.Director.EndNight();
            }
        }

        /// <summary>그 장면이 배분된 일차. 없으면 -1.</summary>
        /// <summary>그 회차에서 장면이 배분된 일차. 없으면 -1.</summary>
        private static int DayOf(EncounterDirector director, string sceneId)
        {
            for (int day = 1; day <= EncounterDirector.LastDay; day++)
            {
                IReadOnlyList<string> plan = director.PlanFor(day);
                for (int i = 0; i < plan.Count; i++)
                {
                    if (plan[i] == sceneId)
                    {
                        return day;
                    }
                }
            }

            return -1;
        }
    }

    /// <summary>
    /// B. 관찰 상태 기계 — 「관찰 + 그 방문의 퇴실」이 한 묶음인가. 반만 끝난 것은 안 끝난 것이다.
    /// </summary>
    public sealed class EncounterObservationTests
    {
        private EncounterFixture _fx;

        [SetUp]
        public void SetUp()
        {
            _fx = new EncounterFixture();
            _fx.Director.BeginRun();
            _fx.Director.BeginNight(1);
            _fx.Inspect(SpaceId.ScienceRoom);
        }

        [TearDown]
        public void TearDown()
        {
            _fx.Dispose();
            _fx = null;
        }

        /// <summary>모형을 보기만 하고 방을 안 나갔는데 장면이 소비되는 것을 막는다.</summary>
        [Test]
        public void 관찰만으로는_장면이_끝나지_않는다()
        {
            _fx.ObserveModel(EncounterDirector.SceneSA);

            Assert.AreEqual(EncounterState.Observed, _fx.Director.StateOf(EncounterDirector.SceneSA));
            Assert.AreNotEqual(EncounterState.Done, _fx.Director.StateOf(EncounterDirector.SceneSA),
                               "퇴실 전에 소비되면 「그 방문의 퇴실」이라는 한 묶음이 깨진다");
            Assert.IsTrue(_fx.Director.IsActive(EncounterDirector.SceneSA), "관찰만 한 장면은 아직 오늘 살아 있다");
        }

        /// <summary>관찰과 퇴실 중 하나만으로 장면이 끝나는 것을 막는다.</summary>
        [Test]
        public void 관찰과_그_방문의_퇴실을_모두_마쳐야_끝난다()
        {
            _fx.ObserveModel(EncounterDirector.SceneSA);
            _fx.Exit(SpaceId.ScienceRoom);

            Assert.AreEqual(EncounterState.Done, _fx.Director.StateOf(EncounterDirector.SceneSA));
            Assert.IsFalse(_fx.Director.IsActive(EncounterDirector.SceneSA), "소비된 장면은 그 회차에 다시 깔리지 않는다");
        }

        /// <summary>관찰만 하고 밤이 끝난 장면을 「봤다」로 처리해 조용히 소비하는 것을 막는다.</summary>
        [Test]
        public void 관찰만_한_채_밤이_끝나면_이월된다()
        {
            _fx.ObserveModel(EncounterDirector.SceneSA);
            Assert.AreEqual(EncounterState.Observed, _fx.Director.StateOf(EncounterDirector.SceneSA));

            _fx.Director.EndNight();

            Assert.IsTrue(Contains(_fx.Director.CarriedOver, EncounterDirector.SceneSA),
                          "반만 끝난 장면은 안 끝난 것이라 다음 날로 넘어가야 한다");
            Assert.AreEqual(EncounterState.Idle, _fx.Director.StateOf(EncounterDirector.SceneSA));
        }

        private static bool Contains(IReadOnlyList<string> list, string value)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == value)
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// C. 필연 조우 3단계 — 못 보고 물러날수록 모형이 다가오고, 게이트는 셋뿐이며, 못 본 장면은 이월된다.
    /// </summary>
    public sealed class EncounterApproachTests
    {
        private EncounterFixture _fx;

        [SetUp]
        public void SetUp()
        {
            _fx = new EncounterFixture();
        }

        [TearDown]
        public void TearDown()
        {
            _fx.Dispose();
            _fx = null;
        }

        /// <summary>접근이 멈추거나(조우가 영영 안 일어남) 3단계를 넘어 문 앞을 막는 것(출구 봉쇄 금기)을 막는다.</summary>
        [Test]
        public void 접근_단계는_못_보고_퇴실할_때마다_오르고_삼단계에서_멈춘다()
        {
            _fx.Director.BeginRun();
            _fx.Director.BeginNight(1);

            _fx.Inspect(SpaceId.ScienceRoom);
            Assert.AreEqual(1, _fx.Director.StepOf(EncounterDirector.SceneSA), "장면 확정 직후가 1단계다");

            _fx.Exit(SpaceId.ScienceRoom);
            Assert.AreEqual(2, _fx.Director.StepOf(EncounterDirector.SceneSA));

            _fx.Exit(SpaceId.ScienceRoom);
            Assert.AreEqual(EncounterDirector.ApproachStepCount, _fx.Director.StepOf(EncounterDirector.SceneSA));

            // 네 번째 퇴실. 여기서 더 오르면 남는 자리는 문 앞뿐이고 그건 출구 봉쇄다.
            _fx.Exit(SpaceId.ScienceRoom);
            _fx.Exit(SpaceId.ScienceRoom);
            Assert.AreEqual(EncounterDirector.ApproachStepCount, _fx.Director.StepOf(EncounterDirector.SceneSA),
                            "접근 단계가 " + EncounterDirector.ApproachStepCount + "를 넘었다");

            Assert.AreEqual(EncounterDirector.ApproachStepCount, _fx.PlacedTargets.Count, "단계마다 한 번씩만 놓아야 한다");
            for (int step = 1; step <= EncounterDirector.ApproachStepCount; step++)
            {
                Assert.AreEqual(EncounterTestTable.ApproachTargetId(EncounterDirector.SceneSA, step), _fx.PlacedTargets[step - 1]);
            }
        }

        /// <summary>게이트가 넷째 장면으로 번지는 것을 막는다 — 전부에 붙으면 「안 보고 나가면 문 앞에 선다」가 조작법으로 읽힌다.</summary>
        [Test]
        public void 퇴실_게이트는_지정된_세_장면에만_놓인다()
        {
            Assert.AreEqual(3, EncounterDirector.ExitGateSceneIds.Length, "게이트 장면은 셋이다");
            Assert.IsFalse(EncounterDirector.UsesExitGate(EncounterDirector.SceneCB));
            Assert.IsTrue(EncounterDirector.UsesExitGate(EncounterDirector.SceneTA));

            // 2026-09-22: 배분이 회차마다 달라지므로(기획서 v6 §4) 일차를 적지 않고
            // 그 장면이 실제로 깔린 날을 찾아 그 밤에 시험한다.
            _fx.Director.BeginRun();
            int gateDay = DayOf(_fx.Director, EncounterDirector.SceneTA);
            int plainDay = DayOf(_fx.Director, EncounterDirector.SceneCB);
            Assert.AreNotEqual(-1, gateDay, "T-A가 배분에 없다");
            Assert.AreNotEqual(-1, plainDay, "C-B가 배분에 없다");

            // ① 게이트 아닌 장면(C-B)이 깔린 밤 — 못 보고 나가도 게이트는 서지 않는다.
            _fx.Director.BeginNight(plainDay);
            _fx.Inspect(SpaceId.Corridor);
            _fx.Exit(SpaceId.Classroom_1_1);
            _fx.Exit(SpaceId.Classroom_1_1);
            Assert.IsFalse(_fx.Director.IsExitGatePlaced(EncounterDirector.SceneCB),
                           "C-B에 게이트가 섰다. 게이트는 " + EncounterDirector.ExitGateSceneList() + " 셋뿐이다");
            Assert.IsFalse(WasPlacedAt(EncounterTestTable.GateTargetId(EncounterDirector.SceneCB)));

            // ② 게이트 장면(T-A)이 깔린 밤 — 못 보고 나가면 문과 플레이어 사이에 선다.
            if (gateDay != plainDay)
            {
                _fx.Director.EndNight();
                _fx.Director.BeginNight(gateDay);
                _fx.Inspect(SpaceId.Corridor);
            }

            _fx.Exit(SpaceId.Toilet);
            Assert.IsTrue(_fx.Director.IsExitGatePlaced(EncounterDirector.SceneTA), "T-A는 게이트 장면인데 게이트가 서지 않았다");
            Assert.IsTrue(WasPlacedAt(EncounterTestTable.GateTargetId(EncounterDirector.SceneTA)));

            // 게이트는 하룻밤 1회다. 두 번째 퇴실에 또 서면 「나갈 때마다 막힌다」가 된다.
            int gatePlacements = CountPlacedAt(EncounterTestTable.GateTargetId(EncounterDirector.SceneTA));
            _fx.Exit(SpaceId.Toilet);
            Assert.AreEqual(gatePlacements, CountPlacedAt(EncounterTestTable.GateTargetId(EncounterDirector.SceneTA)),
                            "게이트가 하룻밤에 두 번 섰다");
        }

        /// <summary>그 회차에서 장면이 배분된 일차. 없으면 -1.</summary>
        private static int DayOf(EncounterDirector director, string sceneId)
        {
            for (int day = 1; day <= EncounterDirector.LastDay; day++)
            {
                IReadOnlyList<string> plan = director.PlanFor(day);
                for (int i = 0; i < plan.Count; i++)
                {
                    if (plan[i] == sceneId)
                    {
                        return day;
                    }
                }
            }

            return -1;
        }

        /// <summary>못 본 장면이 조용히 소비돼 그 회차에서 사라지는 것을 막는다.</summary>
        [Test]
        public void 못_본_장면은_다음_날로_이월된다()
        {
            _fx.Director.BeginRun();
            _fx.Director.BeginNight(1);
            _fx.Inspect(SpaceId.ScienceRoom);
            _fx.Exit(SpaceId.ScienceRoom);

            _fx.Director.EndNight();

            Assert.AreEqual(1, _fx.Director.CarriedOver.Count);
            Assert.AreEqual(EncounterDirector.SceneSA, _fx.Director.CarriedOver[0]);
            Assert.AreEqual(EncounterState.Idle, _fx.Director.StateOf(EncounterDirector.SceneSA), "이월된 장면은 다시 깔릴 수 있어야 한다");

            // 2일차 배분은 회차마다 다르므로(기획서 v6 §4) 장면 ID를 적지 않고 배분에서 읽는다.
            string todaysOwn = _fx.Director.PlanFor(2)[0];

            _fx.Director.BeginNight(2);

            // 2026-09-22: 우선순위를 뒤집었다. **그날 배분이 먼저**고 이월분은 남는 자리에 들어간다.
            // 이월을 먼저 넣으면 상한 3장에 걸려 새 배분이 계속 밀리고, 회차 뒤쪽 장면이
            // 한 번도 안 깔린다 — 실측으로 6/8에 그쳤다. 8장면 전소진이 이 설계의 목적이다.
            Assert.AreEqual(todaysOwn, _fx.Director.TodayScenes[0],
                            "오늘 처음 나오는 장면이 자리를 먼저 가진다");
            Assert.IsTrue(Contains(_fx.Director.TodayScenes, EncounterDirector.SceneSA),
                          "이월분은 남는 자리에 들어간다 — 사라지면 안 된다");
        }

        /// <summary>이월이 새 배분을 밀어내 회차 뒤쪽 장면이 한 번도 안 깔리는 것을 막는다.</summary>
        [Test]
        public void 아무것도_관찰하지_않아도_여덟_장면이_전부_깔린다()
        {
            _fx.Director.BeginRun();
            List<string> laid = new List<string>();

            for (int day = 1; day <= EncounterDirector.LastDay; day++)
            {
                _fx.Director.BeginNight(day);
                IReadOnlyList<string> today = _fx.Director.TodayScenes;
                for (int i = 0; i < today.Count; i++)
                {
                    if (!laid.Contains(today[i]))
                    {
                        laid.Add(today[i]);
                    }
                }

                // 점검만 하고 아무것도 보지 않은 채 나간다.
                _fx.Inspect(SpaceId.Corridor);
                _fx.Exit(SpaceId.Corridor);
                _fx.Director.EndNight();
            }

            IReadOnlyList<string> all = EncounterDirector.AllSceneIds;
            List<string> missing = new List<string>();
            for (int i = 0; i < all.Count; i++)
            {
                if (!laid.Contains(all[i]))
                {
                    missing.Add(all[i]);
                }
            }

            Assert.AreEqual(0, missing.Count,
                            "한 번도 깔리지 않은 장면: " + string.Join(" ", missing.ToArray()));
            Assert.AreEqual(EncounterDirector.SceneCount, laid.Count);
        }

        private static bool Contains(IReadOnlyList<string> list, string value)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == value)
                {
                    return true;
                }
            }

            return false;
        }

        private bool WasPlacedAt(string targetId)
        {
            return CountPlacedAt(targetId) > 0;
        }

        private int CountPlacedAt(string targetId)
        {
            int n = 0;
            for (int i = 0; i < _fx.PlacedTargets.Count; i++)
            {
                if (_fx.PlacedTargets[i] == targetId)
                {
                    n++;
                }
            }

            return n;
        }
    }

    /// <summary>
    /// D. 시야 규약 — <b>모형은 시야 밖에 있을 때만 움직인다</b>(CLAUDE.md §2.7 금기).
    /// 한 번이라도 눈앞에서 움직이면 플레이어는 그것을 연출로 분류하고 그 뒤로는 무서워하지 않는다.
    /// </summary>
    public sealed class EncounterVisibilityTests
    {
        private EncounterFixture _fx;
        private bool _visible;

        [SetUp]
        public void SetUp()
        {
            _fx = new EncounterFixture();
            _visible = true;
            _fx.Director.IsVisible = delegate (string sceneId) { return _visible; };
        }

        [TearDown]
        public void TearDown()
        {
            // 예외 로그 무시 설정은 정적 상태다. 되돌리지 않으면 뒤 테스트가 진짜 예외를 놓친다.
            LogAssert.ignoreFailingMessages = false;
            _fx.Dispose();
            _fx = null;
        }

        /// <summary>플레이어가 보고 있는 동안 모형이 움직이는 것을 막는다. 이 게임의 공포가 통째로 걸린 한 줄이다.</summary>
        [Test]
        public void 시야_안이면_배치가_한_번도_일어나지_않는다()
        {
            _visible = true;

            _fx.Director.BeginRun();
            _fx.Director.BeginNight(1);

            _fx.Inspect(SpaceId.ScienceRoom);
            _fx.Exit(SpaceId.ScienceRoom);
            _fx.Tick();
            _fx.Tick();

            Assert.AreEqual(0, _fx.PlacedScenes.Count, "시야 안에서 모형이 움직였다");
            Assert.AreEqual(0, _fx.Director.StepOf(EncounterDirector.SceneSA), "실제로 놓인 단계는 0이어야 한다");
        }

        /// <summary>시야에 걸린 배치가 「취소」돼 모형이 영영 다가오지 않는 것을 막는다 — 대기지 포기가 아니다.</summary>
        [Test]
        public void 시야가_풀리면_그때_배치된다()
        {
            _visible = true;

            _fx.Director.BeginRun();
            _fx.Director.BeginNight(1);
            _fx.Inspect(SpaceId.ScienceRoom);
            Assert.AreEqual(0, _fx.PlacedScenes.Count);

            _visible = false;
            _fx.Tick();

            Assert.AreEqual(1, _fx.PlacedScenes.Count, "시야가 풀렸는데도 대기 중이던 배치가 실행되지 않았다");
            Assert.AreEqual(EncounterDirector.SceneSA, _fx.PlacedScenes[0]);
            Assert.AreEqual(EncounterTestTable.ApproachTargetId(EncounterDirector.SceneSA, 1), _fx.PlacedTargets[0]);
            Assert.AreEqual(1, _fx.Director.StepOf(EncounterDirector.SceneSA));
        }

        /// <summary>시야 판정이 터졌을 때 「안 보인다」로 넘겨 눈앞에서 모형을 옮기는 것을 막는다.</summary>
        [Test]
        public void 시야_판정이_예외를_던지면_보인다로_본다()
        {
            // 델리게이트가 던진 예외는 Debug.LogException으로 남는다. 그 로그 자체는 이 테스트의 관심사가 아니다.
            LogAssert.ignoreFailingMessages = true;

            _fx.Director.IsVisible = delegate (string sceneId)
            {
                throw new InvalidOperationException("시야 판정 실패");
            };

            _fx.Director.BeginRun();
            _fx.Director.BeginNight(1);

            _fx.Inspect(SpaceId.ScienceRoom);
            _fx.Exit(SpaceId.ScienceRoom);
            _fx.Tick();

            Assert.AreEqual(0, _fx.PlacedScenes.Count, "판단 불가일 때 움직였다. 안전한 쪽은 안 움직이는 쪽이다");
            Assert.AreEqual(0, _fx.Director.StepOf(EncounterDirector.SceneSA));
        }

        /// <summary>시야 델리게이트 미주입을 「항상 보임」으로 처리해 조우가 통째로 사라지는 것을 막는다 — 그건 버그가 아니라 빈 게임으로 보인다.</summary>
        [Test]
        public void 시야_미주입이면_항상_안_보임으로_본다()
        {
            _fx.Director.IsVisible = null;

            _fx.Director.BeginRun();
            _fx.Director.BeginNight(1);

            _fx.Inspect(SpaceId.ScienceRoom);

            Assert.AreEqual(1, _fx.PlacedScenes.Count, "미주입인데 배치가 멈췄다. 조우가 통째로 사라진다");
            Assert.AreEqual(EncounterDirector.SceneSA, _fx.PlacedScenes[0]);
            Assert.AreEqual(EncounterTestTable.ApproachTargetId(EncounterDirector.SceneSA, 1), _fx.PlacedTargets[0]);
        }
    }

    /// <summary>
    /// E. 문자 — 유도 문자 G와 재방문 문자 N1의 발송 조건(기획서 10-3 · 10-4).
    /// 발송은 <see cref="EventBus.MessageSent"/>를 거치므로 구독해서 센다.
    /// </summary>
    public sealed class EncounterMessageTests
    {
        private EncounterFixture _fx;

        [SetUp]
        public void SetUp()
        {
            _fx = new EncounterFixture();
        }

        [TearDown]
        public void TearDown()
        {
            _fx.Dispose();
            _fx = null;
        }

        /// <summary>한 방에서 들락거리는 것만으로 유도 문자가 다 나가는 것을 막는다.</summary>
        [Test]
        public void 서로_다른_점검_두_곳마다_문자_한_통이_나간다()
        {
            _fx.Director.BeginRun();
            _fx.Director.BeginNight(3);   // 3일차 = 하루 2장면

            _fx.Inspect(SpaceId.Toilet);
            Assert.AreEqual(0, _fx.Messages.Count, "점검 한 곳으로는 문자가 열리지 않는다");

            // 같은 공간을 또 점검한다. 새로운 곳이 아니므로 크레딧이 열리면 안 된다.
            _fx.Inspect(SpaceId.Toilet);
            Assert.AreEqual(0, _fx.Messages.Count, "같은 공간을 두 번 점검했는데 문자가 나갔다");

            _fx.Inspect(SpaceId.Corridor);
            Assert.AreEqual(1, _fx.Messages.Count, "서로 다른 두 곳을 점검했는데 문자가 안 나갔다");

            // 세 곳째는 아직 두 곳이 안 모였다(3/2 = 1).
            _fx.Inspect(SpaceId.ScienceRoom);
            Assert.AreEqual(1, _fx.Messages.Count, "세 곳째에 문자가 한 통 더 나갔다");

            _fx.Inspect(SpaceId.Classroom_1_1);
            Assert.AreEqual(2, _fx.Messages.Count, "네 곳째(두 번째 2곳)에 문자가 안 나갔다");
        }

        /// <summary>1일차 고정 장면에 유도 문자가 붙어 「가 보라」는 안내가 최초 조우를 미리 설명하는 것을 막는다.</summary>
        [Test]
        public void S_A에는_유도_문자가_없다()
        {
            EncounterTableSO.Scene scene = _fx.Table.Find(EncounterDirector.SceneSA);
            Assert.IsNotNull(scene);
            Assert.IsTrue(string.IsNullOrEmpty(scene.MessageId), "S-A는 유도 문자를 가질 수 없는 유일한 장면이다");

            _fx.Director.BeginRun();
            _fx.Director.BeginNight(1);   // 오늘 장면은 S-A 하나뿐이다.

            _fx.Inspect(SpaceId.Toilet);
            _fx.Inspect(SpaceId.Corridor);   // 크레딧 1통이 열린다.

            Assert.AreEqual(0, _fx.Messages.Count, "S-A를 안내하는 문자가 나갔다");
            Assert.IsFalse(_fx.Director.WasMessageSent(EncounterDirector.SceneSA));
        }

        /// <summary>재방문 문자가 1일차나 후반으로 새거나 회차에 두 번 나가는 것을 막는다 — 빈 자리는 2일차뿐이다.</summary>
        [Test]
        public void N1은_이일차에만_회차_한_번_나간다()
        {
            _fx.Director.BeginRun();

            _fx.Director.BeginNight(1);
            _fx.Inspect(SpaceId.Toilet);
            Assert.AreEqual(0, _fx.CountOf(EncounterDirector.NoticeMessageId), "1일차에 N1이 나갔다");
            Assert.AreEqual(0, _fx.Director.NoticeSentCount);
            _fx.Director.EndNight();

            _fx.Director.BeginNight(EncounterDirector.NoticeDay);
            _fx.Inspect(SpaceId.Toilet);
            Assert.AreEqual(1, _fx.CountOf(EncounterDirector.NoticeMessageId), "2일차에 N1이 안 나갔다");
            Assert.AreEqual(1, _fx.Director.NoticeSentCount);

            // 같은 밤에 점검을 더 해도 N1은 다시 나가지 않는다.
            _fx.Inspect(SpaceId.Corridor);
            _fx.Inspect(SpaceId.ScienceRoom);
            Assert.AreEqual(1, _fx.CountOf(EncounterDirector.NoticeMessageId), "같은 밤에 N1이 두 번 나갔다");
            _fx.Director.EndNight();

            // 회차 한도. 2일차가 다시 와도 한 번 보낸 뒤에는 나가지 않는다.
            _fx.Director.BeginNight(EncounterDirector.NoticeDay);
            _fx.Inspect(SpaceId.Toilet);
            Assert.AreEqual(EncounterDirector.NoticeLimitPerRun, _fx.CountOf(EncounterDirector.NoticeMessageId),
                            "N1이 회차 한도를 넘었다");
            Assert.AreEqual(EncounterDirector.NoticeLimitPerRun, _fx.Director.NoticeSentCount);
        }

        /// <summary>역설 문자가 나간 밤에 N1을 겹쳐 보내 태블릿이 같은 날 두 통으로 붐비는 것을 막는다.</summary>
        [Test]
        public void 역설을_보낸_날에는_N1이_나가지_않는다()
        {
            _fx.Director.ParadoxSentToday = delegate { return true; };

            _fx.Director.BeginRun();
            _fx.Director.BeginNight(EncounterDirector.NoticeDay);
            _fx.Inspect(SpaceId.Toilet);

            Assert.AreEqual(0, _fx.CountOf(EncounterDirector.NoticeMessageId), "역설을 보낸 날에 N1이 나갔다");
            Assert.AreEqual(0, _fx.Director.NoticeSentCount);
        }
    }
}

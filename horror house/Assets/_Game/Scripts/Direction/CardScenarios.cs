using System;
using System.Collections.Generic;

namespace NightDuty
{
    /// <summary>시나리오 한 걸음의 종류.</summary>
    public enum ScenarioStepKind
    {
        /// <summary>판정 신호 하나를 보낸다.</summary>
        Signal = 0,

        /// <summary>판정 시간을 흘린다(0.1초마다 응시 샘플 동반).</summary>
        Wait = 1,

        /// <summary>근무 종료를 요청한다.</summary>
        EndNight = 2
    }

    /// <summary>시나리오 한 걸음. 화면에 보일 설명을 함께 가진다.</summary>
    public readonly struct ScenarioStep
    {
        /// <summary>종류.</summary>
        public readonly ScenarioStepKind Kind;

        /// <summary>보낼 신호(<see cref="ScenarioStepKind.Signal"/>).</summary>
        public readonly JudgeSignal Signal;

        /// <summary>흘릴 시간(초, <see cref="ScenarioStepKind.Wait"/>).</summary>
        public readonly float Seconds;

        /// <summary>시간이 흐르는 동안 바라보는 대상 ID. 빈 문자열이면 아무것도 보지 않음.</summary>
        public readonly string GazeTarget;

        /// <summary>사람이 읽는 설명.</summary>
        public readonly string Label;

        /// <summary>걸음을 만든다.</summary>
        public ScenarioStep(ScenarioStepKind kind, JudgeSignal signal, float seconds, string gazeTarget, string label)
        {
            Kind = kind;
            Signal = signal;
            Seconds = seconds;
            GazeTarget = gazeTarget ?? string.Empty;
            Label = label ?? string.Empty;
        }
    }

    /// <summary>
    /// 카드 한 장을 「지키는」 행동과 「어기는」 행동을 신호 순서로 적은 시험 시나리오.
    /// <para>
    /// 판정 디버그 패널이 버튼 하나로 재생하고, EditMode 테스트가 모든 카드에 대해 기대 결과를 확인한다.
    /// 기획서 각 카드의 「검증 절차·기대 결과」에서 대표 경로 하나씩을 골랐다. 정본은 기획서다.
    /// </para>
    /// </summary>
    public sealed class CardScenario
    {
        /// <summary>카드 ID.</summary>
        public string CardId;

        /// <summary>짧은 이름(화면용).</summary>
        public string Title;

        /// <summary>시작 전에 맞출 축(자격 구간). <see cref="SetupValue"/>가 0이면 없음.</summary>
        public FearAxis SetupAxis;

        /// <summary>시작 전에 올릴 축 값.</summary>
        public int SetupValue;

        /// <summary>지키는 행동 설명.</summary>
        public string ComplyText;

        /// <summary>어기는 행동 설명.</summary>
        public string ViolateText;

        /// <summary>어기기 버튼 이름. 위반이 없는 카드(S1)는 「안 하기」.</summary>
        public string ViolateButton = "어기기";

        /// <summary>지키는 행동.</summary>
        public ScenarioStep[] Comply;

        /// <summary>어기는 행동.</summary>
        public ScenarioStep[] Violate;

        /// <summary>어기는 행동의 기대 상태(보통 위반).</summary>
        public CardState ViolateExpect = CardState.Violated;

        /// <summary>
        /// 시나리오를 재생한다. 끝에 항상 근무 종료를 요청한다(밤 종료 정산 카드용).
        /// </summary>
        /// <param name="comply">true면 지키기, false면 어기기.</param>
        /// <param name="send">신호 보내기.</param>
        /// <param name="wait">시간 흘리기(초, 응시 대상).</param>
        /// <param name="endNight">근무 종료 요청.</param>
        public void Play(bool comply, Action<JudgeSignal> send, Action<float, string> wait, Action endNight)
        {
            ScenarioStep[] steps = comply ? Comply : Violate;
            for (int i = 0; i < steps.Length; i++)
            {
                PlayStep(steps[i], send, wait, endNight);
            }

            endNight();
        }

        /// <summary>걸음 하나를 실행한다.</summary>
        public static void PlayStep(in ScenarioStep step, Action<JudgeSignal> send, Action<float, string> wait, Action endNight)
        {
            switch (step.Kind)
            {
                case ScenarioStepKind.Signal:
                    send(step.Signal);
                    break;
                case ScenarioStepKind.Wait:
                    wait(step.Seconds, step.GazeTarget);
                    break;
                case ScenarioStepKind.EndNight:
                    endNight();
                    break;
            }
        }
    }

    /// <summary>카드 24장의 시험 시나리오 모음.</summary>
    public static class CardScenarios
    {
        private static List<CardScenario> _all;

        /// <summary>전체(H1~T6 순서).</summary>
        public static IReadOnlyList<CardScenario> All
        {
            get
            {
                if (_all == null)
                {
                    _all = Build();
                }

                return _all;
            }
        }

        /// <summary>카드 ID로 찾는다. 없으면 null.</summary>
        public static CardScenario Find(string cardId)
        {
            IReadOnlyList<CardScenario> all = All;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].CardId == cardId)
                {
                    return all[i];
                }
            }

            return null;
        }

        // ── 걸음 만들기 ──

        private static ScenarioStep Sig(JudgeSignal s, string label)
        {
            return new ScenarioStep(ScenarioStepKind.Signal, s, 0f, string.Empty, label);
        }

        private static ScenarioStep Enter(SpaceId space, string name)
        {
            return Sig(JudgeSignal.OfSpace(SignalKind.SpaceEntered, space), name + " 들어감");
        }

        private static ScenarioStep Exit(SpaceId space, string name)
        {
            return Sig(JudgeSignal.OfSpace(SignalKind.SpaceExited, space), name + "에서 나옴");
        }

        private static ScenarioStep Inspect(SpaceId space, string name)
        {
            return Sig(JudgeSignal.OfSpace(SignalKind.InspectionCompleted, space), name + " 점검 완료");
        }

        private static ScenarioStep T(SignalKind kind, string id, string label)
        {
            return Sig(JudgeSignal.Target(kind, id), label);
        }

        private static ScenarioStep Door(string id, bool close, string label)
        {
            return Sig(JudgeSignal.DoorCommand(id, close, ActionSource.Player), label);
        }

        private static ScenarioStep Near(string id, float meters, string what)
        {
            return Sig(JudgeSignal.Proximity(id, meters), what + "에서 " + meters.ToString("0.0") + "m");
        }

        private static ScenarioStep Light(bool on)
        {
            return Sig(JudgeSignal.Flashlight(on), on ? "손전등 켬" : "손전등 끔");
        }

        private static ScenarioStep Wait(float seconds, string gaze = "", string what = "")
        {
            string label = gaze.Length == 0
                ? seconds.ToString("0.0") + "초 기다림"
                : what + " " + seconds.ToString("0.0") + "초 바라봄";
            return new ScenarioStep(ScenarioStepKind.Wait, default(JudgeSignal), seconds, gaze, label);
        }

        private static ScenarioStep EndNight()
        {
            return new ScenarioStep(ScenarioStepKind.EndNight, default(JudgeSignal), 0f, string.Empty, "근무 종료");
        }

        private static ScenarioStep[] S(params ScenarioStep[] steps)
        {
            return steps;
        }

        private const string Passage = "corridor.passage";

        private static List<CardScenario> Build()
        {
            const SpaceId Cor = SpaceId.Corridor;
            const SpaceId C11 = SpaceId.Classroom_1_1;
            const SpaceId C13 = SpaceId.Classroom_1_3;
            const SpaceId Sci = SpaceId.ScienceRoom;
            const SpaceId Toi = SpaceId.Toilet;
            ScenarioStep passage = T(SignalKind.PassageCompleted, Passage, "복도 통행 마침");

            List<CardScenario> list = new List<CardScenario>();

            // ───── 복도 ─────
            list.Add(new CardScenario
            {
                CardId = "H1", Title = "저절로 열린 문",
                ComplyText = "문이 저절로 열리는 걸 보고, 그대로 지나간다.",
                ViolateText = "문이 저절로 열리는 걸 보고, 그 문을 닫는다.",
                Comply = S(Enter(Cor, "복도"), T(SignalKind.DoorAutoOpenObserved, "corridor.door.auto", "문이 저절로 열림"), passage),
                Violate = S(Enter(Cor, "복도"), T(SignalKind.DoorAutoOpenObserved, "corridor.door.auto", "문이 저절로 열림"),
                            Door("corridor.door.auto", true, "그 문을 닫음"))
            });
            list.Add(new CardScenario
            {
                CardId = "H2", Title = "등 뒤의 문소리", SetupAxis = FearAxis.Auditory, SetupValue = 50,
                ComplyText = "등 뒤 문소리 뒤 2초 지나 그 문을 2.9초만 보고 고개를 돌린다.",
                ViolateText = "등 뒤 문소리 뒤 2초 지나 그 문을 3초 이상 본다.",
                Comply = S(Enter(Cor, "복도"), T(SignalKind.ClueDelivered, "corridor.door.back", "등 뒤에서 문소리"),
                           Wait(2f), Wait(2.9f, "corridor.door.back", "소리 난 문"), Wait(0.1f), passage),
                Violate = S(Enter(Cor, "복도"), T(SignalKind.ClueDelivered, "corridor.door.back", "등 뒤에서 문소리"),
                            Wait(2f), Wait(3f, "corridor.door.back", "소리 난 문"))
            });
            list.Add(new CardScenario
            {
                CardId = "H3", Title = "복도에선 손전등 끄기",
                ComplyText = "손전등을 켠 채 들어왔다가 2초 안에 끄고 지나간다.",
                ViolateText = "손전등을 켠 채 복도를 2초 넘게 걷는다.",
                Comply = S(Enter(Cor, "복도"), Light(true), T(SignalKind.ZoneEntered, Passage, "통행 구역 진입"),
                           Wait(1f), Light(false), Wait(2f), passage),
                Violate = S(Enter(Cor, "복도"), Light(true), T(SignalKind.ZoneEntered, Passage, "통행 구역 진입"), Wait(2f))
            });
            list.Add(new CardScenario
            {
                CardId = "H4", Title = "복도 가운데 상자", SetupAxis = FearAxis.Layout, SetupValue = 25,
                ComplyText = "상자를 보고 2m 떨어져 지나간 뒤 근무를 마친다.",
                ViolateText = "상자를 보고 1.4m까지 다가간다.",
                Comply = S(Enter(Cor, "복도"), T(SignalKind.ClueIdentified, "corridor.box", "상자 발견"),
                           Near("corridor.box", 2f, "상자"), passage, EndNight()),
                Violate = S(Enter(Cor, "복도"), T(SignalKind.ClueIdentified, "corridor.box", "상자 발견"),
                            Near("corridor.box", 1.4f, "상자"))
            });
            list.Add(new CardScenario
            {
                CardId = "H5", Title = "떨어진 천장 조각", SetupAxis = FearAxis.Layout, SetupValue = 75,
                ComplyText = "떨어진 조각을 보고 1.6m 떨어져 돌아간다.",
                ViolateText = "떨어진 조각에 1.4m까지 다가간다.",
                Comply = S(Enter(Cor, "복도"), T(SignalKind.ClueIdentified, "corridor.debris", "조각 발견"),
                           Near("corridor.debris", 1.6f, "조각"), passage),
                Violate = S(Enter(Cor, "복도"), T(SignalKind.ClueIdentified, "corridor.debris", "조각 발견"),
                            Near("corridor.debris", 1.4f, "조각"))
            });
            list.Add(new CardScenario
            {
                CardId = "H6", Title = "복도의 나무", SetupAxis = FearAxis.Layout, SetupValue = 90,
                ComplyText = "나무를 보고 풀(2m) 밖으로 지나간다.",
                ViolateText = "나무 1.5m 안(풀)을 밟는다. 배치 90에서 +25라 바로 포획된다.",
                Comply = S(Enter(Cor, "복도"), T(SignalKind.ClueIdentified, "corridor.tree", "나무 발견"),
                           Near("corridor.tree", 2.5f, "나무"), passage),
                Violate = S(Enter(Cor, "복도"), T(SignalKind.ClueIdentified, "corridor.tree", "나무 발견"),
                            Near("corridor.tree", 1.5f, "나무"))
            });

            // ───── 교실 ─────
            list.Add(new CardScenario
            {
                CardId = "C1", Title = "분필 세 획",
                ComplyText = "분필 소리를 듣고 1-3부터 점검한 뒤 1-1에 들어간다.",
                ViolateText = "분필 소리를 듣고 바로 1-1에 들어간다.",
                Comply = S(T(SignalKind.ClueDelivered, "cls11.chalk3", "1-1 문밖에서 분필 세 획"),
                           Enter(C13, "1-3"), Inspect(C13, "1-3"), Exit(C13, "1-3"), Enter(C11, "1-1")),
                Violate = S(T(SignalKind.ClueDelivered, "cls11.chalk3", "1-1 문밖에서 분필 세 획"), Enter(C11, "1-1"))
            });
            list.Add(new CardScenario
            {
                CardId = "C2", Title = "문을 향한 좌석",
                ComplyText = "돌아앉은 책상을 보고 1.5m 떨어진 채 점검하고 나온다.",
                ViolateText = "돌아앉은 책상에 1.4m까지 다가간다.",
                Comply = S(Enter(C11, "1-1"), T(SignalKind.ClueIdentified, "cls11.desk.turned", "돌아앉은 책상 발견"),
                           Near("cls11.desk.turned", 1.5f, "책상"), Inspect(C11, "1-1"), Exit(C11, "1-1")),
                Violate = S(Enter(C11, "1-1"), T(SignalKind.ClueIdentified, "cls11.desk.turned", "돌아앉은 책상 발견"),
                            Near("cls11.desk.turned", 1.4f, "책상"))
            });
            list.Add(new CardScenario
            {
                CardId = "C3", Title = "뒷줄 책상 타격음", SetupAxis = FearAxis.Auditory, SetupValue = 50,
                ComplyText = "나가려는데 책상이 울린다. 2초 뒤 그 책상을 2.9초만 보고 나온다.",
                ViolateText = "나가려는데 책상이 울린다. 2초 뒤 그 책상을 3초 이상 본다.",
                Comply = S(Enter(C11, "1-1"), T(SignalKind.ClueDelivered, "cls11.desk.back", "뒷줄 책상이 울림"),
                           Wait(2f), Wait(2.9f, "cls11.desk.back", "뒷줄 책상"), Wait(0.1f), Exit(C11, "1-1")),
                Violate = S(Enter(C11, "1-1"), T(SignalKind.ClueDelivered, "cls11.desk.back", "뒷줄 책상이 울림"),
                            Wait(2f), Wait(3f, "cls11.desk.back", "뒷줄 책상"))
            });
            list.Add(new CardScenario
            {
                CardId = "C4", Title = "칠판과 의자 소리", SetupAxis = FearAxis.Auditory, SetupValue = 75,
                ComplyText = "두 소리가 겹치는 동안 교탁에서 떨어져 있다가, 소리가 모두 멎는다.",
                ViolateText = "두 소리가 겹치는 동안 교탁 1.4m까지 다가간다.",
                Comply = S(Enter(C11, "1-1"), T(SignalKind.ClueDelivered, "cls11.lectern.noise", "칠판·의자 소리가 겹침"),
                           Near("cls11.lectern", 2f, "교탁"), T(SignalKind.SequenceEnded, "cls11.lectern.noise", "두 소리 모두 멎음")),
                Violate = S(Enter(C11, "1-1"), T(SignalKind.ClueDelivered, "cls11.lectern.noise", "칠판·의자 소리가 겹침"),
                            Near("cls11.lectern", 1.4f, "교탁"))
            });
            list.Add(new CardScenario
            {
                CardId = "C5", Title = "어두운 교실엔 손전등", SetupAxis = FearAxis.Illuminance, SetupValue = 50,
                ComplyText = "등이 4개 이하인 걸 보고 1초 안에 손전등을 켜고, 켠 채 점검하고 나온다.",
                ViolateText = "등이 4개 이하인 걸 보고도 손전등을 2초 넘게 켜지 않는다.",
                Comply = S(Enter(C11, "1-1"), T(SignalKind.ClueIdentified, "cls11.lights", "등 4개 이하 확인"),
                           Wait(1f), Light(true), Wait(3f), Inspect(C11, "1-1"), Exit(C11, "1-1")),
                Violate = S(Enter(C11, "1-1"), T(SignalKind.ClueIdentified, "cls11.lights", "등 4개 이하 확인"), Wait(2f))
            });
            list.Add(new CardScenario
            {
                CardId = "C6", Title = "두 교실 점검·문 닫기",
                ComplyText = "1-1 문을 직접 열고 점검한 뒤 닫고, 1-3도 점검하고 근무를 마친다.",
                ViolateText = "1-1 문을 직접 열고 점검만 한 뒤 문을 연 채, 1-3은 건너뛰고 근무를 마친다.",
                Comply = S(Door("corridor.door.11", false, "1-1 문을 직접 엶"), Enter(C11, "1-1"), Inspect(C11, "1-1"), Exit(C11, "1-1"),
                           Door("corridor.door.11", true, "1-1 문을 닫음"), Enter(C13, "1-3"), Inspect(C13, "1-3"), Exit(C13, "1-3"),
                           EndNight()),
                Violate = S(Door("corridor.door.11", false, "1-1 문을 직접 엶"), Enter(C11, "1-1"), Inspect(C11, "1-1"),
                            Exit(C11, "1-1"), EndNight())
            });

            // ───── 과학실 ─────
            list.Add(new CardScenario
            {
                CardId = "S1", Title = "첫날 모형과 눈 맞추기", ViolateButton = "안 하기", ViolateExpect = CardState.Undetermined,
                ComplyText = "과학실에 들어가 모형 얼굴을 1초 동안 본다.",
                ViolateText = "모형 얼굴을 0.9초만 보고 나온다. 위반이 없는 카드라 벌점 없이 미판정이 된다.",
                Comply = S(Enter(Sci, "과학실"), Wait(1f, "science.model.sa.face", "모형 얼굴")),
                Violate = S(Enter(Sci, "과학실"), Wait(0.9f, "science.model.sa.face", "모형 얼굴"), Wait(0.1f), Exit(Sci, "과학실"))
            });
            list.Add(new CardScenario
            {
                CardId = "S2", Title = "퇴실 후 유리 깨지는 소리",
                ComplyText = "나온 뒤 유리 소리를 듣고, 문만 열어 보고 들어가지 않은 채 근무를 마친다.",
                ViolateText = "나온 뒤 유리 소리를 듣고 다시 들어간다.",
                Comply = S(T(SignalKind.ClueDelivered, "science.glass.break", "과학실에서 유리 깨지는 소리"),
                           Door("science.door", false, "과학실 문만 엶"), EndNight()),
                Violate = S(T(SignalKind.ClueDelivered, "science.glass.break", "과학실에서 유리 깨지는 소리"), Enter(Sci, "과학실"))
            });
            list.Add(new CardScenario
            {
                CardId = "S3", Title = "떨어진 유리 기구 접촉음", SetupAxis = FearAxis.Auditory, SetupValue = 25,
                ComplyText = "간격을 확인하고 접촉음을 들은 뒤, 실험대에서 떨어져 점검하고 나온다.",
                ViolateText = "간격을 확인하고 접촉음을 들은 뒤, 실험대 1.4m까지 다가간다.",
                Comply = S(Enter(Sci, "과학실"), T(SignalKind.ClueIdentified, "science.bench.glass", "기구 간격 확인"),
                           T(SignalKind.ClueDelivered, "science.bench.glass.clink", "유리 접촉음"),
                           Near("science.bench.glass", 2f, "실험대"), Inspect(Sci, "과학실"), Exit(Sci, "과학실")),
                Violate = S(Enter(Sci, "과학실"), T(SignalKind.ClueIdentified, "science.bench.glass", "기구 간격 확인"),
                            T(SignalKind.ClueDelivered, "science.bench.glass.clink", "유리 접촉음"),
                            Near("science.bench.glass", 1.4f, "실험대"))
            });
            list.Add(new CardScenario
            {
                CardId = "S4", Title = "마지막 천장등", SetupAxis = FearAxis.Illuminance, SetupValue = 75,
                ComplyText = "하나 남은 등을 1.9초만 보고 눈을 돌린 뒤 점검하고 나온다.",
                ViolateText = "하나 남은 등을 2초 이상 올려다본다.",
                Comply = S(Enter(Sci, "과학실"), T(SignalKind.ClueIdentified, "science.light.last", "등 하나만 남음"),
                           Wait(1.9f, "science.light.last", "남은 등"), Wait(0.1f), Inspect(Sci, "과학실"), Exit(Sci, "과학실")),
                Violate = S(Enter(Sci, "과학실"), T(SignalKind.ClueIdentified, "science.light.last", "등 하나만 남음"),
                            Wait(2f, "science.light.last", "남은 등"))
            });
            list.Add(new CardScenario
            {
                CardId = "S5", Title = "자리를 벗어난 모형",
                ComplyText = "보관 위치 밖의 모형을 보고 1.5m 떨어진 채 나온다.",
                ViolateText = "보관 위치 밖의 모형에 1.4m까지 다가간다.",
                Comply = S(Enter(Sci, "과학실"), T(SignalKind.ModelObserved, "scene.sb", "모형을 1초 봄"),
                           Near("science.model.sb", 1.5f, "모형"), Exit(Sci, "과학실")),
                Violate = S(Enter(Sci, "과학실"), T(SignalKind.ModelObserved, "scene.sb", "모형을 1초 봄"),
                            Near("science.model.sb", 1.4f, "모형"))
            });
            list.Add(new CardScenario
            {
                CardId = "S6", Title = "유리 구역에선 손전등 끄기",
                ComplyText = "손전등을 켠 채 구역에 들어갔다가 1초 만에 끄고, 점검한 뒤 구역을 나온다.",
                ViolateText = "손전등을 켠 채 유리 구역에 2초 넘게 있는다.",
                Comply = S(Enter(Sci, "과학실"), Light(true), T(SignalKind.ZoneEntered, "science.zone.glass", "유리 구역 진입"),
                           Wait(1f), Light(false), Wait(2f), T(SignalKind.ZoneExited, "science.zone.glass", "유리 구역에서 나옴")),
                Violate = S(Enter(Sci, "과학실"), Light(true), T(SignalKind.ZoneEntered, "science.zone.glass", "유리 구역 진입"),
                            Wait(2f))
            });

            // ───── 화장실 ─────
            list.Add(new CardScenario
            {
                CardId = "T1", Title = "저절로 움직인 칸 문",
                ComplyText = "칸 문이 저절로 열리는 걸 보고, 출입문만 쓰고 칸 문은 건드리지 않는다.",
                ViolateText = "칸 문이 저절로 열리는 걸 보고, 칸 문을 닫는다.",
                Comply = S(Enter(Toi, "화장실"), T(SignalKind.DoorAutoOpenObserved, "toilet.stall.outer", "칸 문이 저절로 열림"),
                           Door("toilet.door", false, "화장실 출입문을 엶"), EndNight()),
                Violate = S(Enter(Toi, "화장실"), T(SignalKind.DoorAutoOpenObserved, "toilet.stall.outer", "칸 문이 저절로 열림"),
                            Door("toilet.stall.inner", true, "칸 문을 닫음"))
            });
            list.Add(new CardScenario
            {
                CardId = "T2", Title = "물 내림 12초", SetupAxis = FearAxis.Auditory, SetupValue = 25,
                ComplyText = "물 내리는 소리가 나고 11.9초 만에 화장실을 나온다.",
                ViolateText = "물 내리는 소리가 나고 12초가 지나도록 안에 있는다.",
                Comply = S(Enter(Toi, "화장실"), T(SignalKind.ClueDelivered, "toilet.flush", "물 내리는 소리 시작"),
                           Wait(11.9f), Exit(Toi, "화장실")),
                Violate = S(Enter(Toi, "화장실"), T(SignalKind.ClueDelivered, "toilet.flush", "물 내리는 소리 시작"), Wait(12f))
            });
            list.Add(new CardScenario
            {
                CardId = "T3", Title = "안쪽 칸의 이용자",
                ComplyText = "안쪽 칸의 무언가를 보고, 칸 문을 닫고(닫힘 끝까지) 나온다.",
                ViolateText = "안쪽 칸의 무언가를 보고, 문을 닫지 않은 채 나온다.",
                Comply = S(Enter(Toi, "화장실"), T(SignalKind.ModelObserved, "scene.ta", "안쪽 칸의 무언가를 1초 봄"),
                           Door("toilet.stall.inner", true, "칸 문 닫기"),
                           Sig(new JudgeSignal(SignalKind.DoorCloseCompleted, SpaceId.None, "toilet.stall.inner", ActionSource.Player, true, 0f), "칸 문이 다 닫힘"),
                           Exit(Toi, "화장실")),
                Violate = S(Enter(Toi, "화장실"), T(SignalKind.ModelObserved, "scene.ta", "안쪽 칸의 무언가를 1초 봄"),
                            Exit(Toi, "화장실"))
            });
            list.Add(new CardScenario
            {
                CardId = "T4", Title = "잠긴 세면대의 호출", SetupAxis = FearAxis.Auditory, SetupValue = 50,
                ComplyText = "세면대 쪽에서 부르는 소리를 듣고, 다가가지 않고 나온다.",
                ViolateText = "세면대 쪽에서 부르는 소리를 듣고 1.4m까지 다가간다.",
                Comply = S(Enter(Toi, "화장실"), T(SignalKind.ClueDelivered, "toilet.sink.call", "'근무자님' 부르는 소리"),
                           Near("toilet.sink", 2f, "세면대"), Exit(Toi, "화장실")),
                Violate = S(Enter(Toi, "화장실"), T(SignalKind.ClueDelivered, "toilet.sink.call", "'근무자님' 부르는 소리"),
                            Near("toilet.sink", 1.4f, "세면대"))
            });
            list.Add(new CardScenario
            {
                CardId = "T5", Title = "닫힌 칸 아래 빛", SetupAxis = FearAxis.Illuminance, SetupValue = 50,
                ComplyText = "칸 아래 빛을 보고 손전등을 끈 채 공용부를 점검하고 나온다.",
                ViolateText = "칸 아래 빛을 보고도 손전등을 2초 넘게 켜 둔다.",
                Comply = S(Enter(Toi, "화장실"), T(SignalKind.ClueIdentified, "toilet.stall.light", "칸 아래 새는 빛"),
                           Wait(3f), Inspect(Toi, "화장실"), Exit(Toi, "화장실")),
                Violate = S(Enter(Toi, "화장실"), Light(true), T(SignalKind.ClueIdentified, "toilet.stall.light", "칸 아래 새는 빛"),
                            Wait(2f))
            });
            list.Add(new CardScenario
            {
                CardId = "T6", Title = "두 칸이 모두 열림",
                ComplyText = "두 칸이 열린 걸 보고, 칸 안에 들어가지 않고 점검한 뒤 근무를 마친다.",
                ViolateText = "두 칸이 열린 걸 보고, 안쪽 칸 문턱 안으로 들어간다.",
                Comply = S(Enter(Toi, "화장실"), T(SignalKind.ClueIdentified, "toilet.stalls.bothopen", "두 칸 모두 열림"),
                           Inspect(Toi, "화장실"), Exit(Toi, "화장실"), EndNight()),
                Violate = S(Enter(Toi, "화장실"), T(SignalKind.ClueIdentified, "toilet.stalls.bothopen", "두 칸 모두 열림"),
                            T(SignalKind.ZoneEntered, "toilet.stall.inner.inside", "안쪽 칸 안으로 들어감"))
            });

            return list;
        }
    }
}

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace NightDuty.Editor
{
    /// <summary>
    /// 교실(C1~C6) · 과학실(S1~S6) · 화장실(T1~T6) 근무수칙 카드 에셋을 만들고, 임시 편성표에 2~4일차를 채운다.
    /// <para>
    /// 판정 상세의 정본은 기획서(2026-09-12)다. 여기서는 그 내용을 조건 조합으로 옮긴 <b>초기값</b>만 만든다
    /// (근거: 교실·과학실·화장실 카드 표현 가능성 분석 문서 §2).
    /// <see cref="CorridorCardBuilder"/>와 같은 규칙을 따른다: <b>이미 있는 에셋은 건드리지 않는다</b>,
    /// 확인 대화상자를 띄우지 않는다(Unity CLI/MCP 실행용), 끝에 데이터 검사 결과를 콘솔에 요약한다.
    /// 초기값으로 되돌리려면 해당 에셋을 지우고 메뉴를 다시 실행한다.
    /// </para>
    /// <para>
    /// 대상 ID는 연결 약속 문서 규칙을 따른다: 교실 접두어 <c>cls11</c>·<c>cls13</c>, 교실 문 <c>corridor.door.11</c>·<c>corridor.door.13</c>
    /// (복도 카드와 같은 오브젝트는 같은 ID), 화장실 칸 문 <c>toilet.stall.outer</c>·<c>toilet.stall.inner</c>,
    /// 칸 문턱 안 구역 <c>toilet.stall.outer.inside</c>·<c>toilet.stall.inner.inside</c>, 모형 장면 <c>scene.xx</c>.
    /// </para>
    /// </summary>
    public static class RoomCardBuilder
    {
        /// <summary>교실 카드 폴더.</summary>
        public const string ClassroomFolder = "Assets/_Game/ScriptableObjects/Rules/Classroom";

        /// <summary>과학실 카드 폴더.</summary>
        public const string ScienceFolder = "Assets/_Game/ScriptableObjects/Rules/Science";

        /// <summary>화장실 카드 폴더.</summary>
        public const string ToiletFolder = "Assets/_Game/ScriptableObjects/Rules/Toilet";

        /// <summary>디버그 편성표가 채우는 마지막 일차(1일차 복도 + 교실·과학실·화장실).</summary>
        private const int DebugDayCount = 4;

        private static readonly string[] ClassroomIds = { "C1", "C2", "C3", "C4", "C5", "C6" };
        private static readonly string[] ScienceIds = { "S1", "S2", "S3", "S4", "S5", "S6" };
        private static readonly string[] ToiletIds = { "T1", "T2", "T3", "T4", "T5", "T6" };

        /// <summary>메뉴: 없는 교실 카드를 만들고 편성표 2일차를 채운다.</summary>
        [MenuItem("NightDuty/교실 카드 에셋 생성 (C1~C6)", false, 122)]
        public static void BuildClassroom()
        {
            BuildSpace("교실", ClassroomFolder, ClassroomIds);
            ExtendDeckTable();
        }

        /// <summary>메뉴: 없는 과학실 카드를 만들고 편성표 3일차를 채운다.</summary>
        [MenuItem("NightDuty/과학실 카드 에셋 생성 (S1~S6)", false, 123)]
        public static void BuildScience()
        {
            BuildSpace("과학실", ScienceFolder, ScienceIds);
            ExtendDeckTable();
        }

        /// <summary>메뉴: 없는 화장실 카드를 만들고 편성표 4일차를 채운다.</summary>
        [MenuItem("NightDuty/화장실 카드 에셋 생성 (T1~T6)", false, 124)]
        public static void BuildToilet()
        {
            BuildSpace("화장실", ToiletFolder, ToiletIds);
            ExtendDeckTable();
        }

        /// <summary>메뉴: 복도(편성표 포함)부터 네 공간의 없는 카드를 모두 만들고 편성표 2~4일차를 채운다.</summary>
        [MenuItem("NightDuty/모든 공간 카드 에셋 생성", false, 125)]
        public static void BuildAll()
        {
            CorridorCardBuilder.BuildMissing();
            BuildSpace("교실", ClassroomFolder, ClassroomIds);
            BuildSpace("과학실", ScienceFolder, ScienceIds);
            BuildSpace("화장실", ToiletFolder, ToiletIds);
            ExtendDeckTable();
        }

        /// <summary>한 공간의 없는 카드를 만들고 검사 결과를 요약한다. 있는 에셋은 그대로 둔다.</summary>
        private static void BuildSpace(string label, string folder, string[] ids)
        {
            EnsureFolder(folder);

            List<string> created = new List<string>();
            List<string> kept = new List<string>();
            List<RuleSO> cards = new List<RuleSO>();

            for (int i = 0; i < ids.Length; i++)
            {
                string id = ids[i];
                string path = folder + "/" + id + ".asset";
                RuleSO existing = AssetDatabase.LoadAssetAtPath<RuleSO>(path);
                if (existing != null)
                {
                    kept.Add(id);
                    cards.Add(existing);
                    continue;
                }

                RuleSO card = ScriptableObject.CreateInstance<RuleSO>();
                card.Configure(Define(id));
                AssetDatabase.CreateAsset(card, path);
                created.Add(id);
                cards.Add(card);
            }

            AssetDatabase.SaveAssets();

            List<string> errors = new List<string>();
            for (int i = 0; i < cards.Count; i++)
            {
                cards[i].Validate(errors);
            }

            string summary = "[" + label + " 카드 생성] 새로 만듦: " + (created.Count > 0 ? string.Join(", ", created) : "없음")
                             + " / 유지: " + (kept.Count > 0 ? string.Join(", ", kept) : "없음");
            if (errors.Count == 0)
            {
                Debug.Log(summary);
            }
            else
            {
                Debug.LogWarning(summary + "\n검사 문제:\n- " + string.Join("\n- ", errors));
            }
        }

        /// <summary>
        /// 임시 편성표(<see cref="CorridorCardBuilder.DeckPath"/>)를 2일차 = C1~C6, 3일차 = S1~S6, 4일차 = T1~T6으로 늘린다.
        /// <para>
        /// 디버그용 임시 편성: 기획서의 S1 1일차 고정·T1/T3 같은 날 금지 규칙은 DayDirector가 맡는다.
        /// (그래서 3일차에 S1이, 4일차에 T1·T3가 함께 들어가는 것은 디버그 편성이라 허용한다.)
        /// </para>
        /// <para>
        /// 규칙: 1일차는 그대로 둔다. 2~4일차는 <b>표에 없거나 비어 있는 날만</b> 채우며, 그 공간의 여섯 장이
        /// 모두 에셋으로 있을 때만 채운다(없는 공간은 그 공간 메뉴가 만든 뒤에 채워진다).
        /// 앞 공간이 아직 없어 중간 날이 비면 빈 덱으로 두고, 나중에 그 공간 메뉴를 실행하면 채워진다.
        /// 5일차 이후와 이미 카드가 있는 날은 건드리지 않는다. 일차 수가 4 이상이고 2~4일차가 모두 차 있으면 아무것도 하지 않는다.
        /// </para>
        /// </summary>
        private static void ExtendDeckTable()
        {
            string deckPath = CorridorCardBuilder.DeckPath;
            NightDeckTableSO table = AssetDatabase.LoadAssetAtPath<NightDeckTableSO>(deckPath);
            if (table == null)
            {
                Debug.LogWarning("[공간 카드 편성] 편성표가 없습니다(" + deckPath + "). "
                                 + "'NightDuty/복도 카드 에셋 생성 (H1~H6)' 메뉴가 만든 뒤 다시 실행하십시오. 편성표는 건드리지 않았습니다.");
                return;
            }

            string[] labels = { "교실", "과학실", "화장실" };
            string[] folders = { ClassroomFolder, ScienceFolder, ToiletFolder };
            string[][] idSets = { ClassroomIds, ScienceIds, ToiletIds };

            int oldCount = table.DayCount;
            List<RuleSO[]> days = new List<RuleSO[]>();
            for (int day = 1; day <= oldCount; day++)
            {
                // 기존 날은 원본 배열 그대로 둔다(빈 칸도 유지 — 1일차를 바꾸지 않기 위해).
                days.Add(table.RawCardsOf(day - 1));
            }

            if (days.Count == 0)
            {
                // 1일차 칸이 아예 없으면 빈 1일차를 둔다(1일차 내용은 복도 메뉴 몫).
                days.Add(new RuleSO[0]);
            }

            List<string> filled = new List<string>();
            List<string> waiting = new List<string>();
            for (int s = 0; s < folders.Length; s++)
            {
                int dayIndex = s + 1;   // 0번 = 1일차 → 교실은 2일차(인덱스 1)
                bool hasCards = dayIndex < days.Count && table.DeckFor(dayIndex + 1).Count > 0 && dayIndex < oldCount;
                if (hasCards)
                {
                    continue;
                }

                RuleSO[] spaceCards = LoadAll(folders[s], idSets[s]);
                if (spaceCards == null)
                {
                    waiting.Add((dayIndex + 1) + "일차(" + labels[s] + ")");
                    continue;
                }

                while (days.Count <= dayIndex)
                {
                    days.Add(new RuleSO[0]);
                }

                days[dayIndex] = spaceCards;
                filled.Add((dayIndex + 1) + "일차(" + labels[s] + ")");
            }

            if (filled.Count == 0)
            {
                string reason = oldCount >= DebugDayCount && waiting.Count == 0 ? "2~4일차가 이미 차 있음" : "채울 공간 카드가 아직 없음";
                Debug.Log("[공간 카드 편성] 편성표 유지(" + reason + ")"
                          + (waiting.Count > 0 ? " / 대기: " + string.Join(", ", waiting) : string.Empty));
                return;
            }

            NightDeckTableSO.DayDeck[] result = new NightDeckTableSO.DayDeck[days.Count];
            for (int i = 0; i < days.Count; i++)
            {
                result[i] = new NightDeckTableSO.DayDeck { Cards = days[i] };
            }

            table.SetDays(result);
            EditorUtility.SetDirty(table);
            AssetDatabase.SaveAssets();

            Debug.Log("[공간 카드 편성] 디버그 임시 편성 채움: " + string.Join(", ", filled)
                      + " / 일차 수 " + oldCount + " → " + result.Length
                      + (waiting.Count > 0 ? " / 대기(그 공간 메뉴 실행 뒤 채움): " + string.Join(", ", waiting) : string.Empty));
        }

        /// <summary>폴더에서 카드 여섯 장을 순서대로 읽는다. 하나라도 없으면 null.</summary>
        private static RuleSO[] LoadAll(string folder, string[] ids)
        {
            RuleSO[] cards = new RuleSO[ids.Length];
            for (int i = 0; i < ids.Length; i++)
            {
                cards[i] = AssetDatabase.LoadAssetAtPath<RuleSO>(folder + "/" + ids[i] + ".asset");
                if (cards[i] == null)
                {
                    return null;
                }
            }

            return cards;
        }

        /// <summary>카드 ID별 초기값.</summary>
        private static RuleSO.Config Define(string id)
        {
            RuleSO.Config c = new RuleSO.Config
            {
                CardId = id,
                SettleAt = SettleAt.WhenSuccessMet,
                SuccessDelta = 2,
                FailureAxis = FearAxis.Layout,
                FailureDelta = 12,
                Radius = 1.5f
            };

            switch (id)
            {
                // ───────────── 교실 ─────────────

                case "C1":
                    // 청각 0~49 · 1-1 문밖 분필 3획 끝에 시작(장기) · 1-3 점검 완료 → 신뢰 +2 · 그 전에 1-1 실내 진입 → 청각 +12.
                    // 장기로 둔 이유: 복도 → 1-3 → 1-1로 방문을 넘는 순서 의무이고, 단서가 하루 1회라 방문 몫에 막히면 다시 오지 않는다.
                    // 「그날 1-3 점검을 마친 뒤면 시작 안 함」은 단서 재생기가 맡는다(점검 뒤에는 분필 단서를 전달하지 않음).
                    c.Space = SpaceId.Classroom_1_1;
                    c.PlayerText = "1-1 문밖에서 분필 소리가 정확히 세 번 들린다면, 점검 순서를 바꾸어 1-3부터 완료하십시오. 순서를 지키지 않고 들어온 사람은, 학생으로 처리됩니다.";
                    c.IsLongTerm = true;
                    c.UseEligibleBand = true;
                    c.EligibleAxis = FearAxis.Auditory;
                    c.EligibleFrom = Band.Band0;
                    c.EligibleTo = Band.Band1;
                    c.TriggerKind = SignalKind.ClueDelivered;
                    c.TriggerId = "cls11.chalk3";
                    c.Failure = new SignalCondition(SignalKind.SpaceEntered, string.Empty, SpaceId.Classroom_1_1);   // 문 E는 무관
                    c.Success = new SignalCondition(SignalKind.InspectionCompleted, string.Empty, SpaceId.Classroom_1_3);
                    c.FailureAxis = FearAxis.Auditory;
                    c.FailureDelta = 12;
                    c.Radius = 0f;
                    break;

                case "C2":
                    // 배치 25~99 또는 C-A(→ 구간 검사 끄고 회전 좌석 식별 신호의 존재로 대신) · 반경 밖 식별로 시작 ·
                    // 점검 완료 → 신뢰 +2 · 좌석 1.5m 미만 → 배치 +12 · 점검 없이 퇴실 → 대기(다음 방문에 기회).
                    c.Space = SpaceId.Classroom_1_1;
                    c.PlayerText = "출입문을 향해 돌아앉은 책상 근처로는 가지 마십시오. 그 자리는 수업을 듣기 위한 자리가 아닙니다. 앉아 있는 쪽에서는 문이 잘 보입니다.";
                    c.UseEligibleBand = false;
                    c.TriggerKind = SignalKind.ClueIdentified;
                    c.TargetIds = new[] { "cls11.desk.turned" };
                    c.Failure = new ProximityCondition(string.Empty);
                    c.Success = new SignalCondition(SignalKind.InspectionCompleted, string.Empty, SpaceId.Classroom_1_1);
                    c.Cancel = new SignalCondition(SignalKind.SpaceExited, string.Empty, SpaceId.Classroom_1_1);
                    c.FailureAxis = FearAxis.Layout;
                    c.FailureDelta = 12;
                    c.Radius = 1.5f;
                    break;

                case "C3":
                    // 청각 50~99 · 퇴실 준비 중 뒷줄 책상 타격음으로 시작 · 실내 경계 이탈 → 신뢰 +2 ·
                    // 유예 2초 뒤 그 책상 3초 연속 응시 → 청각 +15. (타격음 단서 ID = 응시 대상 ID)
                    c.Space = SpaceId.Classroom_1_1;
                    c.PlayerText = "나가려는 순간 뒤쪽 책상이 울린다면, 그 책상을 오래 바라보지 말고 교실 밖으로 나오십시오. 당신의 이름은, 아직 부르지 않았습니다.";
                    c.UseEligibleBand = true;
                    c.EligibleAxis = FearAxis.Auditory;
                    c.EligibleFrom = Band.Band2;
                    c.EligibleTo = Band.Band4;
                    c.TriggerKind = SignalKind.ClueDelivered;
                    c.TargetIds = new[] { "cls11.desk.back" };
                    c.Failure = new GazeCondition(TargetMatchIds.Trigger);
                    c.Success = new SignalCondition(SignalKind.SpaceExited, string.Empty, SpaceId.Classroom_1_1);
                    c.GraceSeconds = 2f;
                    c.GazeSeconds = 3f;
                    c.FailureAxis = FearAxis.Auditory;
                    c.FailureDelta = 15;
                    c.Radius = 0f;
                    break;

                case "C4":
                    // 청각 75~99 · 칠판 긁기와 의자 마찰이 겹쳐 들리기 시작할 때 시작 ·
                    // 전체 시퀀스 종료 또는 그 전 퇴실 → 신뢰 +2 · 교탁 1.5m 미만 → 청각 +15.
                    c.Space = SpaceId.Classroom_1_1;
                    c.PlayerText = "칠판 긁는 소리와 의자 끄는 소리가 겹친다면, 교탁 쪽으로는 발을 들이지 마십시오. 두 소리가 모두 멎으면 점검을 계속하셔도 됩니다. 두 소리를 함께 내려면, 손이 네 개 필요합니다.";
                    c.UseEligibleBand = true;
                    c.EligibleAxis = FearAxis.Auditory;
                    c.EligibleFrom = Band.Band3;
                    c.EligibleTo = Band.Band4;
                    c.TriggerKind = SignalKind.ClueDelivered;
                    c.TriggerId = "cls11.lectern.noise";
                    c.TargetIds = new[] { "cls11.lectern" };
                    c.Failure = new ProximityCondition(string.Empty);
                    c.Success = new AnyOfCondition(
                        new SignalCondition(SignalKind.SequenceEnded, "cls11.lectern.noise"),   // 90~99 추가 마찰음까지 포함한 전체 종료
                        new SignalCondition(SignalKind.SpaceExited, string.Empty, SpaceId.Classroom_1_1));
                    c.FailureAxis = FearAxis.Auditory;
                    c.FailureDelta = 15;
                    c.Radius = 1.5f;
                    break;

                case "C5":
                    // 조도 50~99(실제 켜진 등 4개 이하) · 두 교실 공통, 진입 지점에서 등 상태 식별로 시작 ·
                    // 유예 2초 뒤 On 유지로 점검 완료 → 신뢰 +3 · 유예 뒤 Off 또는 이후 Off 전환 → 조도 +12 · 짧은 입구 왕복 → 대기.
                    // 공간은 대표값 1-1(두 교실 공통 카드). 두 방 조건은 AnyOf로 모두 받는다.
                    c.Space = SpaceId.Classroom_1_1;
                    c.PlayerText = "형광등이 네 개 이하로 켜져 있다면, 손전등을 켜고 교실 밖으로 나올 때까지 유지하십시오. 어두운 교실에서는 퇴실이 확인되지 않습니다. 아직 퇴실이 확인되지 않은 근무자가 한 명 있습니다.";
                    c.UseEligibleBand = true;
                    c.EligibleAxis = FearAxis.Illuminance;
                    c.EligibleFrom = Band.Band2;
                    c.EligibleTo = Band.Band4;
                    c.TriggerKind = SignalKind.ClueIdentified;
                    c.TargetIds = new[] { "cls11.lights", "cls13.lights" };
                    c.Failure = new FlashlightCondition(false);
                    c.Success = new AnyOfCondition(
                        new SignalCondition(SignalKind.InspectionCompleted, string.Empty, SpaceId.Classroom_1_1),
                        new SignalCondition(SignalKind.InspectionCompleted, string.Empty, SpaceId.Classroom_1_3));
                    c.Cancel = new AnyOfCondition(
                        new SignalCondition(SignalKind.SpaceExited, string.Empty, SpaceId.Classroom_1_1),
                        new SignalCondition(SignalKind.SpaceExited, string.Empty, SpaceId.Classroom_1_3));
                    c.GraceSeconds = 2f;
                    c.SuccessDelta = 3;
                    c.FailureAxis = FearAxis.Illuminance;
                    c.FailureDelta = 12;
                    c.Radius = 0f;
                    break;

                case "C6":
                    // 자격 없음 · 덱에 들어오면 밤 시작부터 감시(장기) · 밤 종료에 두 교실 점검 완료·직접 연 문 모두 닫힘 → 신뢰 +3 ·
                    // 밤 종료에 점검 누락 또는 수동 개방 문이 열린 채 남음 → 배치 +12 한 번(같은 밤 종료 신호에서 위반 우선).
                    // 자동 개방 문(DoorAutoOpenObserved·연출 출처 명령)은 의무를 만들지 않는다.
                    c.Space = SpaceId.Classroom_1_1;
                    c.PlayerText = "1-1과 1-3을 각각 점검하고, 직접 열어 둔 문은 근무 종료 전까지 닫으십시오. 저절로 열린 문은 그대로 두십시오. 뒤따라 닫는 사람이 따로 있습니다.";
                    c.IsLongTerm = true;
                    c.TriggerKind = SignalKind.NightBegan;
                    c.TargetIds = new[] { "corridor.door.11", "corridor.door.13" };
                    c.Success = new SignalCondition(SignalKind.NightEndAccepted);
                    c.Failure = new AnyOfCondition(
                        new BeforeCondition(
                            new SignalCondition(SignalKind.NightEndAccepted),
                            new AllOfCondition(
                                new SignalCondition(SignalKind.InspectionCompleted, string.Empty, SpaceId.Classroom_1_1),
                                new SignalCondition(SignalKind.InspectionCompleted, string.Empty, SpaceId.Classroom_1_3))),
                        new DoorObligationCondition(string.Empty));
                    c.SuccessDelta = 3;
                    c.FailureAxis = FearAxis.Layout;
                    c.FailureDelta = 12;
                    c.Radius = 0f;
                    break;

                // ───────────── 과학실 ─────────────

                case "S1":
                    // 1일차 고정(편성) · 과학실 진입으로 시작 · 보관 모형 얼굴 1초 연속 응시 → 신뢰 +3 ·
                    // 위반 없음(미관찰은 업무 미완료 — 준수 전용 카드) · 미관찰 퇴실 → 대기.
                    c.Space = SpaceId.ScienceRoom;
                    c.PlayerText = "첫 근무일에는 과학실 보관 위치의 인체 모형과 눈을 맞춘 뒤 순찰을 시작하십시오. 모형도 같은 순간에 당신을 확인합니다.";
                    c.TriggerKind = SignalKind.SpaceEntered;
                    c.TargetIds = new[] { "science.model.sa.face" };
                    c.Success = new GazeCondition(string.Empty, 1f, 0f);
                    c.Cancel = new SignalCondition(SignalKind.SpaceExited, string.Empty, SpaceId.ScienceRoom);
                    c.Failure = null;
                    c.FailureDelta = 0;
                    c.SuccessDelta = 3;
                    c.Radius = 0f;
                    break;

                case "S2":
                    // 자격 구간 없음 · 점검 후 퇴실 바깥에서 유리 파손음 전달로 시작(장기) ·
                    // 밤 종료까지 재진입 없음 → 신뢰 +2 · 과학실 실내 재진입 → 청각 +12(문 E만은 무관).
                    c.Space = SpaceId.ScienceRoom;
                    c.PlayerText = "과학실에서 나온 뒤 유리 깨지는 소리가 들린다면, 그날은 다시 들어가지 마십시오. 퇴실 기록 이후의 파손은 안에 남아 있는 사람의 책임입니다. 당신은 이미 나오셨고, 그 사람은 아직입니다.";
                    c.IsLongTerm = true;
                    c.TriggerKind = SignalKind.ClueDelivered;
                    c.TriggerId = "science.glass.break";
                    c.Failure = new SignalCondition(SignalKind.SpaceEntered, string.Empty, SpaceId.ScienceRoom);
                    c.Success = new SignalCondition(SignalKind.NightEndAccepted);
                    c.FailureAxis = FearAxis.Auditory;
                    c.FailureDelta = 12;
                    c.Radius = 0f;
                    break;

                case "S3":
                    // 청각 25~99 · 간격 식별 뒤 접촉음 전달로 시작(식별 선행은 재생기 몫) ·
                    // 접근 없이 점검 완료 → 신뢰 +2 · 실험대 1.5m 미만 → 청각 +12 · 점검 없이 퇴실 → 대기.
                    c.Space = SpaceId.ScienceRoom;
                    c.PlayerText = "유리 기구끼리 부딪히는 소리가 나는 실험대가 있는데, 그 기구들은 서로 닿을 수 없는 간격으로 고정되어 있다면, 다가가지 마십시오. 내용물은 모두 비웠습니다. 비운 뒤에도 안에서 무언가 움직입니다.";
                    c.UseEligibleBand = true;
                    c.EligibleAxis = FearAxis.Auditory;
                    c.EligibleFrom = Band.Band1;
                    c.EligibleTo = Band.Band4;
                    c.TriggerKind = SignalKind.ClueDelivered;
                    c.TriggerId = "science.bench.glass.clink";
                    c.TargetIds = new[] { "science.bench.glass" };
                    c.Failure = new ProximityCondition(string.Empty);
                    c.Success = new SignalCondition(SignalKind.InspectionCompleted, string.Empty, SpaceId.ScienceRoom);
                    c.Cancel = new SignalCondition(SignalKind.SpaceExited, string.Empty, SpaceId.ScienceRoom);
                    c.FailureAxis = FearAxis.Auditory;
                    c.FailureDelta = 12;
                    c.Radius = 1.5f;
                    break;

                case "S4":
                    // 조도 75~89(등 1개) · 마지막 등 식별로 시작(식별 0.2초는 응시에 넣지 않음) ·
                    // 금지 응시 없이 점검 완료 → 신뢰 +2 · 그 등 2초 연속 응시 → 조도 +12 · 점검 없이 퇴실 → 대기.
                    c.Space = SpaceId.ScienceRoom;
                    c.PlayerText = "천장등이 하나만 남았다면, 그 등을 오래 올려다보지 마십시오. 그 안에서도 이쪽을 봅니다.";
                    c.UseEligibleBand = true;
                    c.EligibleAxis = FearAxis.Illuminance;
                    c.EligibleFrom = Band.Band3;
                    c.EligibleTo = Band.Band3;
                    c.TriggerKind = SignalKind.ClueIdentified;
                    c.TargetIds = new[] { "science.light.last" };
                    c.Failure = new GazeCondition(TargetMatchIds.Trigger, 0f, 0f);   // 유예 없음, 시간은 카드 응시 2초
                    c.Success = new SignalCondition(SignalKind.InspectionCompleted, string.Empty, SpaceId.ScienceRoom);
                    c.Cancel = new SignalCondition(SignalKind.SpaceExited, string.Empty, SpaceId.ScienceRoom);
                    c.GazeSeconds = 2f;
                    c.GraceSeconds = 0f;
                    c.FailureAxis = FearAxis.Illuminance;
                    c.FailureDelta = 12;
                    c.Radius = 0f;
                    break;

                case "S5":
                    // 구간 없음(일반 S-B) · 반경 밖 S-B 1초 관찰로 시작 · 모형 반경 밖으로 퇴실 → 신뢰 +3 · 모형 1.5m 미만 → 배치 +15.
                    // P1 경유 S-B는 장면 ID를 scene.sb.p1로 따로 보내 S5를 시작하지 않는다.
                    c.Space = SpaceId.ScienceRoom;
                    c.PlayerText = "인체 모형이 보관 위치를 벗어난 곳에 서 있다면, 그 모형에 다가가지 말고 과학실에서 나오십시오. 이전 근무자가 시도한 방법은 지침에서 삭제했습니다.";
                    c.TriggerKind = SignalKind.ModelObserved;
                    c.TriggerId = "scene.sb";
                    c.TargetIds = new[] { "science.model.sb" };
                    c.Failure = new ProximityCondition(string.Empty);
                    c.Success = new SignalCondition(SignalKind.SpaceExited, string.Empty, SpaceId.ScienceRoom);
                    c.SuccessDelta = 3;
                    c.FailureAxis = FearAxis.Layout;
                    c.FailureDelta = 15;
                    c.Radius = 1.5f;
                    break;

                case "S6":
                    // 구간 없음 · 유리 기구 구역 진입으로 시작 · 유예 2초 뒤 Off 유지로 구역 안 1초 점검 후 이탈 → 신뢰 +2 ·
                    // 유예 뒤 On 또는 이후 On 전환 → 조도 +12 · 점검 없이 이탈 → 대기.
                    // 체류 3초 = 유예 2초 + 점검 1초(구역 점검 완료 신호가 없어 경과 시간으로 표현).
                    c.Space = SpaceId.ScienceRoom;
                    c.PlayerText = "유리 기구 점검 구역에서는 손전등을 끄고, 그 구역을 나갈 때까지 유지하십시오. 빛을 비추면 목록에 없던 것이 하나씩 늘어납니다.";
                    c.TriggerKind = SignalKind.ZoneEntered;
                    c.TriggerId = "science.zone.glass";
                    c.TargetIds = new[] { "science.zone.glass" };
                    c.Failure = new FlashlightCondition(true);
                    c.Success = new AllOfCondition(
                        new ElapsedCondition(3f),
                        new SignalCondition(SignalKind.ZoneExited, "science.zone.glass"));
                    c.Cancel = new SignalCondition(SignalKind.ZoneExited, "science.zone.glass");
                    c.GraceSeconds = 2f;
                    c.FailureAxis = FearAxis.Illuminance;
                    c.FailureDelta = 12;
                    c.Radius = 0f;
                    break;

                // ───────────── 화장실 ─────────────

                case "T1":
                    // 배치 0~74 · 지정 칸 문(입구 쪽)의 자동 움직임 관찰로 시작(장기) ·
                    // 밤 종료까지 두 점검칸 수동 개폐 없음 → 신뢰 +2 · 어느 점검칸이든 E 열기/닫기 수락 → 배치 +12(출입문 제외).
                    c.Space = SpaceId.Toilet;
                    c.PlayerText = "칸 문이 눈앞에서 저절로 움직이는 것을 보았다면, 그날은 어떤 칸도 여닫지 마십시오. 안에서 열어 준 문을 다시 열면 두 번째 방문으로 기록됩니다. 첫 번째 방문자가 누구였는지는 확인하지 않습니다.";
                    c.IsLongTerm = true;
                    c.UseEligibleBand = true;
                    c.EligibleAxis = FearAxis.Layout;
                    c.EligibleFrom = Band.Band0;
                    c.EligibleTo = Band.Band2;
                    c.TriggerKind = SignalKind.DoorAutoOpenObserved;
                    c.TriggerId = "toilet.stall.outer";
                    c.TargetIds = new[] { "toilet.stall.outer", "toilet.stall.inner" };
                    c.Failure = new SignalCondition(SignalKind.DoorCommandAccepted, string.Empty);   // 열기·닫기 모두, 플레이어만
                    c.Success = new SignalCondition(SignalKind.NightEndAccepted);
                    c.FailureAxis = FearAxis.Layout;
                    c.FailureDelta = 12;
                    c.Radius = 0f;
                    break;

                case "T2":
                    // 청각 25~99 · 물 내림 시퀀스 시작 전달로 시작 · 12초 미만에 화장실 이탈 → 신뢰 +3 ·
                    // 12초가 되도록 실내 → 청각 +15(같은 프레임은 Tick이 먼저). 칸→공용부는 퇴실 아님.
                    c.Space = SpaceId.Toilet;
                    c.PlayerText = "칸 안에서 물 내리는 소리가 들린다면, 소리가 멎기 전에 나오십시오. 안에 계신 분이 먼저 나오도록 양보하지 마십시오. 소리가 멎었다는 것은, 나올 준비가 끝났다는 뜻입니다.";
                    c.UseEligibleBand = true;
                    c.EligibleAxis = FearAxis.Auditory;
                    c.EligibleFrom = Band.Band1;
                    c.EligibleTo = Band.Band4;
                    c.TriggerKind = SignalKind.ClueDelivered;
                    c.TriggerId = "toilet.flush";
                    c.Failure = new ElapsedCondition(12f);
                    c.Success = new SignalCondition(SignalKind.SpaceExited, string.Empty, SpaceId.Toilet);
                    c.SuccessDelta = 3;
                    c.FailureAxis = FearAxis.Auditory;
                    c.FailureDelta = 15;
                    c.Radius = 0f;
                    break;

                case "T3":
                    // 구간 없음(T-A) · 안쪽 칸 T-A 1초 관찰로 시작 · 닫힘 완료 뒤 재개방 없이 화장실 이탈 → 신뢰 +3 ·
                    // 닫힘 완료 전 이탈 또는 관찰 뒤 열기 명령 수락 → 배치 +15 한 번. 시간 제한 없음.
                    c.Space = SpaceId.Toilet;
                    c.PlayerText = "안쪽 점검칸에 무언가 서 있다면, 칸 문을 닫고 다시 열지 않은 채 화장실 밖으로 나오십시오. 비품이 들어 있는 칸도 사용 중인 칸으로 분류합니다. 사용이 끝나면 저희가 회수하겠습니다.";
                    c.TriggerKind = SignalKind.ModelObserved;
                    c.TriggerId = "scene.ta";
                    c.TargetIds = new[] { "toilet.stall.inner" };
                    c.Failure = new AnyOfCondition(
                        new SignalCondition(SignalKind.DoorCommandAccepted, string.Empty, SpaceId.None, FlagFilter.False),   // 다시 열기
                        new BeforeCondition(
                            new SignalCondition(SignalKind.SpaceExited, string.Empty, SpaceId.Toilet),
                            new SignalCondition(SignalKind.DoorCloseCompleted, string.Empty)));
                    c.Success = new AllOfCondition(
                        new SignalCondition(SignalKind.DoorCloseCompleted, string.Empty),
                        new SignalCondition(SignalKind.SpaceExited, string.Empty, SpaceId.Toilet));
                    c.SuccessDelta = 3;
                    c.FailureAxis = FearAxis.Layout;
                    c.FailureDelta = 15;
                    c.Radius = 0f;
                    break;

                case "T4":
                    // 청각 50~99 · 잠긴 세면대의 '근무자님' 호출음 전달로 시작 · 접근 없이 화장실 이탈 → 신뢰 +2 · 세면대 1.5m 미만 → 청각 +12.
                    c.Space = SpaceId.Toilet;
                    c.PlayerText = "세면대 쪽에서 당신을 부르는 소리가 나는데, 수도꼭지는 잠긴 채 그대로라면, 그쪽으로 발을 떼지 마십시오. 시설관리팀은 배수구로 연락하지 않습니다. 이름을 안다고 해서, 사람인 것은 아닙니다.";
                    c.UseEligibleBand = true;
                    c.EligibleAxis = FearAxis.Auditory;
                    c.EligibleFrom = Band.Band2;
                    c.EligibleTo = Band.Band4;
                    c.TriggerKind = SignalKind.ClueDelivered;
                    c.TriggerId = "toilet.sink.call";
                    c.TargetIds = new[] { "toilet.sink" };
                    c.Failure = new ProximityCondition(string.Empty);
                    c.Success = new SignalCondition(SignalKind.SpaceExited, string.Empty, SpaceId.Toilet);
                    c.FailureAxis = FearAxis.Auditory;
                    c.FailureDelta = 12;
                    c.Radius = 1.5f;
                    break;

                case "T5":
                    // 조도 50~99(닫힌 칸 아래 빛 실재) · 공용부에서 새는 빛 식별로 시작 · 유예 2초 뒤 Off 유지로 공용부 점검 완료 → 신뢰 +2 ·
                    // 유예 뒤 On 또는 이후 On 전환 → 조도 +12 · 점검 없이 퇴실 → 대기. 문 열기 자체는 위반 아님.
                    c.Space = SpaceId.Toilet;
                    c.PlayerText = "닫힌 칸 아래로 빛이 새어나온다면, 손전등을 끄고 공용부 점검을 마치십시오. 문을 열어 확인하실 필요는 없습니다. 그 안에서는 바깥이 어두운 편이 낫다고 합니다.";
                    c.UseEligibleBand = true;
                    c.EligibleAxis = FearAxis.Illuminance;
                    c.EligibleFrom = Band.Band2;
                    c.EligibleTo = Band.Band4;
                    c.TriggerKind = SignalKind.ClueIdentified;
                    c.TargetIds = new[] { "toilet.stall.light" };
                    c.Failure = new FlashlightCondition(true);
                    c.Success = new SignalCondition(SignalKind.InspectionCompleted, string.Empty, SpaceId.Toilet);
                    c.Cancel = new SignalCondition(SignalKind.SpaceExited, string.Empty, SpaceId.Toilet);
                    c.GraceSeconds = 2f;
                    c.FailureAxis = FearAxis.Illuminance;
                    c.FailureDelta = 12;
                    c.Radius = 0f;
                    break;

                case "T6":
                    // 배치 75~99 또는 별도 두 칸 개방(→ 구간 검사 끄고 두 칸 개방 식별 신호의 존재로 대신, 수치만으로는 시작 안 함) ·
                    // 공용부에서 두 칸 개방 식별로 시작(장기) · 공용부 점검 완료 + 밤 종료까지 칸 내부 진입 없음 → 신뢰 +2 ·
                    // 어느 칸이든 문턱 안 구역 진입 → 배치 +15.
                    c.Space = SpaceId.Toilet;
                    c.PlayerText = "점검칸이 모두 열린 것을 보았다면, 그날은 어느 칸도 문턱 안으로 들어가지 마십시오. 두 칸이 동시에 열리는 날은 흔치 않습니다. 그런 날 안을 들여다본 근무자가 무엇을 보았는지는, 기록에 없습니다.";
                    c.IsLongTerm = true;
                    c.UseEligibleBand = false;
                    c.TriggerKind = SignalKind.ClueIdentified;
                    c.TriggerId = "toilet.stalls.bothopen";
                    c.TargetIds = new[] { "toilet.stall.outer.inside", "toilet.stall.inner.inside" };
                    c.Failure = new SignalCondition(SignalKind.ZoneEntered, string.Empty);
                    c.Success = new SignalCondition(SignalKind.InspectionCompleted, string.Empty, SpaceId.Toilet);
                    c.SettleAt = SettleAt.AtNightEnd;
                    c.FailureAxis = FearAxis.Layout;
                    c.FailureDelta = 15;
                    c.Radius = 0f;
                    break;

                default:
                    Debug.LogError("[공간 카드 생성] 알 수 없는 카드 ID: " + id);
                    break;
            }

            return c;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            string leaf = System.IO.Path.GetFileName(path);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}

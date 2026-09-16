using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace NightDuty.Editor
{
    /// <summary>
    /// 복도 근무수칙 H1~H6 카드 에셋과 임시 편성표를 만든다.
    /// <para>
    /// 판정 상세의 정본은 기획서(2026-09-12)다. 여기서는 그 내용을 조건 조합으로 옮긴 <b>초기값</b>만 만든다.
    /// <b>이미 있는 에셋은 건드리지 않는다</b> — 기획팀이 인스펙터에서 고친 값을 덮어쓰지 않기 위해서다.
    /// 초기값으로 되돌리려면 해당 에셋을 지우고 메뉴를 다시 실행한다.
    /// </para>
    /// <para>
    /// 대상 ID는 연결 약속 문서 §4.4의 제안 규칙(&lt;공간&gt;.&lt;종류&gt;.&lt;구분&gt;)을 따른다.
    /// 확인 대화상자를 띄우지 않으므로 Unity CLI/MCP로도 실행할 수 있다.
    /// </para>
    /// </summary>
    public static class CorridorCardBuilder
    {
        /// <summary>카드 에셋 폴더.</summary>
        public const string CardFolder = "Assets/_Game/ScriptableObjects/Rules/Corridor";

        /// <summary>편성표 경로(Resources).</summary>
        public const string DeckPath = "Assets/_Game/Resources/NightDeckTable.asset";

        /// <summary>복도 통행 구역 ID.</summary>
        public const string Passage = "corridor.passage";

        private static readonly string[] Order = { "H1", "H2", "H3", "H4", "H5", "H6" };

        /// <summary>메뉴: 없는 복도 카드와 편성표를 만든다.</summary>
        [MenuItem("NightDuty/복도 카드 에셋 생성 (H1~H6)", false, 120)]
        public static void BuildMissing()
        {
            EnsureFolder(CardFolder);
            EnsureFolder("Assets/_Game/Resources");

            List<string> created = new List<string>();
            List<string> kept = new List<string>();
            List<RuleSO> cards = new List<RuleSO>();

            for (int i = 0; i < Order.Length; i++)
            {
                string id = Order[i];
                string path = CardFolder + "/" + id + ".asset";
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

            bool deckCreated = false;
            if (AssetDatabase.LoadAssetAtPath<NightDeckTableSO>(DeckPath) == null)
            {
                NightDeckTableSO deck = ScriptableObject.CreateInstance<NightDeckTableSO>();
                NightDeckTableSO.DayDeck day = new NightDeckTableSO.DayDeck { Cards = cards.ToArray() };
                deck.SetDays(new[] { day });
                AssetDatabase.CreateAsset(deck, DeckPath);
                deckCreated = true;
            }

            AssetDatabase.SaveAssets();

            List<string> errors = new List<string>();
            for (int i = 0; i < cards.Count; i++)
            {
                cards[i].Validate(errors);
            }

            string summary = "[복도 카드 생성] 새로 만듦: " + (created.Count > 0 ? string.Join(", ", created) : "없음")
                             + " / 유지: " + (kept.Count > 0 ? string.Join(", ", kept) : "없음")
                             + " / 편성표: " + (deckCreated ? "새로 만듦" : "유지");
            if (errors.Count == 0)
            {
                Debug.Log(summary);
            }
            else
            {
                Debug.LogWarning(summary + "\n검사 문제:\n- " + string.Join("\n- ", errors));
            }
        }

        /// <summary>카드 ID별 초기값. 테스트도 이 정의와 에셋이 같은지 확인한다.</summary>
        internal static RuleSO.Config Define(string id)
        {
            RuleSO.Config c = new RuleSO.Config
            {
                CardId = id,
                Space = SpaceId.Corridor,
                SettleAt = SettleAt.WhenSuccessMet,
                Success = new SignalCondition(SignalKind.PassageCompleted, Passage),
                SuccessDelta = 2,
                FailureAxis = FearAxis.Layout,
                FailureDelta = 12,
                Radius = 1.5f
            };

            switch (id)
            {
                case "H1":
                    // 자동 개방을 실제 관찰한 문만 대상. 그 문에 E 닫기가 수락되면 즉시 위반.
                    c.PlayerText = "복도를 지나던 중 문이 저절로 열린다면, 닫지 말고 그대로 지나가십시오. 그 문을 연 것은 아직 나오지 않았습니다.";
                    c.TriggerKind = SignalKind.DoorAutoOpenObserved;
                    c.TargetIds = new[] { "corridor.door.auto", "corridor.door.13" };
                    c.Failure = new SignalCondition(SignalKind.DoorCommandAccepted, TargetMatchIds.Trigger, SpaceId.None, FlagFilter.True);
                    // 준수: 문을 닫지 않고 통행 구역을 벗어나면. H-A처럼 1-3으로 바로 들어가 복도를 떠나는 경우도 포함한다.
                    c.Success = new AnyOfCondition(
                        new SignalCondition(SignalKind.PassageCompleted, Passage),
                        new SignalCondition(SignalKind.SpaceExited, string.Empty, SpaceId.Corridor));
                    c.Radius = 0f;
                    break;

                case "H2":
                    // 청각 50~99. 대상 문과 음원은 같은 ID. 유예 2초 뒤 그 문을 3초 연속 응시하면 위반.
                    c.PlayerText = "등 뒤에서 문 닫히는 소리가 난다면, 소리가 난 문을 오래 바라보지 마십시오. 돌아본 근무자들은 모두, 문이 열려 있었다고 보고했습니다.";
                    c.UseEligibleBand = true;
                    c.EligibleAxis = FearAxis.Auditory;
                    c.EligibleFrom = Band.Band2;
                    c.EligibleTo = Band.Band4;
                    c.TriggerKind = SignalKind.ClueDelivered;
                    c.TargetIds = new[] { "corridor.door.back" };
                    c.Failure = new GazeCondition(TargetMatchIds.Trigger);
                    c.GraceSeconds = 2f;
                    c.GazeSeconds = 3f;
                    c.Radius = 0f;
                    c.FailureAxis = FearAxis.Auditory;
                    c.FailureDelta = 15;
                    break;

                case "H3":
                    // 통행 구역 진입 후 2초 유예, 이후 On이면 위반. 유예 안에 되돌아가면 대기로 복귀.
                    c.PlayerText = "복도를 지날 때는 손전등을 꺼 주십시오. 불빛은 근무자보다 앞서 걷습니다.";
                    c.TriggerKind = SignalKind.ZoneEntered;
                    c.TriggerId = Passage;
                    c.TargetIds = new[] { Passage };
                    c.Failure = new FlashlightCondition(true);
                    c.Cancel = new SignalCondition(SignalKind.ZoneExited, Passage);
                    c.GraceSeconds = 2f;
                    c.Radius = 0f;
                    c.FailureAxis = FearAxis.Illuminance;
                    c.FailureDelta = 12;
                    break;

                case "H4":
                    // 배치 25~99. 상자 식별 후 밤 종료까지 1.5m 미만 금지. 안전 통행 1회 이상이면 밤 종료에 준수.
                    c.PlayerText = "복도 가운데 상자가 있다면, 그날 근무가 끝날 때까지 다가가지 말고 벽을 따라 지나가십시오. 반입 목록에 없는 물건입니다. 안에 있는 것도 그렇습니다.";
                    c.IsLongTerm = true;
                    c.UseEligibleBand = true;
                    c.EligibleAxis = FearAxis.Layout;
                    c.EligibleFrom = Band.Band1;
                    c.EligibleTo = Band.Band4;
                    c.TriggerKind = SignalKind.ClueIdentified;
                    c.TargetIds = new[] { "corridor.box" };
                    c.Failure = new ProximityCondition(string.Empty);
                    c.SettleAt = SettleAt.AtNightEnd;
                    break;

                case "H5":
                    // 배치 75~99. 떨어진 조각 식별 후 1.5m 미만 금지. 우회해 통행을 마치면 준수.
                    c.PlayerText = "천장에서 무언가 떨어졌다면, 그 자리를 피해 지나가십시오. 떨어뜨린 것은 아직 천장에 있습니다.";
                    c.UseEligibleBand = true;
                    c.EligibleAxis = FearAxis.Layout;
                    c.EligibleFrom = Band.Band3;
                    c.EligibleTo = Band.Band4;
                    c.TriggerKind = SignalKind.ClueIdentified;
                    c.TargetIds = new[] { "corridor.debris" };
                    c.Failure = new ProximityCondition(string.Empty);
                    break;

                case "H6":
                    // 배치 90~99. 나무 중심 2m 미만(풀 영역) 진입 시 배치 +25 — 90~99에서는 즉시 포획.
                    c.PlayerText = "복도에 나무가 서 있다면, 풀을 밟지 말고 지나가십시오. 본교 복도에는 나무가 없습니다. 그 아래에서 기다리는 것도 없습니다.";
                    c.UseEligibleBand = true;
                    c.EligibleAxis = FearAxis.Layout;
                    c.EligibleFrom = Band.Band4;
                    c.EligibleTo = Band.Band4;
                    c.TriggerKind = SignalKind.ClueIdentified;
                    c.TargetIds = new[] { "corridor.tree" };
                    c.Failure = new ProximityCondition(string.Empty);
                    c.Radius = 2f;
                    c.SuccessDelta = 4;
                    c.FailureDelta = 25;
                    break;

                default:
                    Debug.LogError("[복도 카드 생성] 알 수 없는 카드 ID: " + id);
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

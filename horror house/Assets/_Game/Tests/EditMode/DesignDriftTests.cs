using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace NightDuty.Tests
{
    /// <summary>
    /// 「두 곳이 같은 말을 하는지」만 보는 검사들(2026-09-21 신설).
    /// <para>
    /// 다른 테스트는 「값이 맞는지」를 본다. 여기 있는 것은 <b>값이 두 군데 적혀 있을 때 둘이 어긋났는지</b>만 본다.
    /// 한쪽만 고치고 다른 쪽을 두면 아무도 모르게 어긋나고, 에셋을 지우고 메뉴를 다시 돌릴 때까지 드러나지 않는다.
    /// 사람의 주의력 대신 이 파일이 막는다.
    /// </para>
    /// </summary>
    internal static class DesignDriftKit
    {
        /// <summary>수칙 카드 24장. 순서는 공간별 H·C·S·T.</summary>
        internal static readonly string[] AllCardIds =
        {
            "H1", "H2", "H3", "H4", "H5", "H6",
            "C1", "C2", "C3", "C4", "C5", "C6",
            "S1", "S2", "S3", "S4", "S5", "S6",
            "T1", "T2", "T3", "T4", "T5", "T6"
        };

        /// <summary>감각 3축. 신뢰는 위반축이 될 수 없어 빠진다.</summary>
        internal static readonly FearAxis[] SensoryAxes =
        {
            FearAxis.Auditory,
            FearAxis.Illuminance,
            FearAxis.Layout
        };

        /// <summary>프로젝트 안의 모든 <see cref="RuleSO"/>를 카드 ID로 묶는다. 폴더 경로에 기대지 않는다.</summary>
        internal static Dictionary<string, RuleSO> LoadCardsById(List<string> problems)
        {
            Dictionary<string, RuleSO> map = new Dictionary<string, RuleSO>();
            string[] guids = AssetDatabase.FindAssets("t:RuleSO");
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                RuleSO card = AssetDatabase.LoadAssetAtPath<RuleSO>(path);
                if (card == null || string.IsNullOrEmpty(card.CardId))
                {
                    continue;
                }

                if (map.ContainsKey(card.CardId))
                {
                    problems.Add(card.CardId + ": 같은 카드 ID를 가진 에셋이 둘 이상입니다(" + path + ").");
                    continue;
                }

                map.Add(card.CardId, card);
            }

            for (int i = 0; i < AllCardIds.Length; i++)
            {
                if (!map.ContainsKey(AllCardIds[i]))
                {
                    problems.Add(AllCardIds[i] + ": 에셋이 없습니다. NightDuty ▸ 모든 공간 카드 에셋 생성 메뉴를 실행하세요.");
                }
            }

            return map;
        }

        /// <summary>대상 ID 목록을 메시지에 찍을 수 있는 한 줄로 만든다. 순서도 비교 대상이다.</summary>
        internal static string Join(IReadOnlyList<string> ids)
        {
            if (ids == null || ids.Count == 0)
            {
                return "[]";
            }

            List<string> parts = new List<string>();
            for (int i = 0; i < ids.Count; i++)
            {
                parts.Add(ids[i] ?? "(null)");
            }

            return "[" + string.Join(", ", parts) + "]";
        }

        /// <summary>대상 ID 배열을 메시지용 한 줄로 만든다.</summary>
        internal static string JoinArray(string[] ids)
        {
            if (ids == null || ids.Length == 0)
            {
                return "[]";
            }

            List<string> parts = new List<string>();
            for (int i = 0; i < ids.Length; i++)
            {
                parts.Add(ids[i] ?? "(null)");
            }

            return "[" + string.Join(", ", parts) + "]";
        }
    }

    /// <summary>
    /// A. 에셋 ↔ 빌더 드리프트.
    /// <para>
    /// <see cref="RuleSO"/> 에셋의 초기값은 <c>RoomCardBuilder.Define</c>·<c>CorridorCardBuilder.Define</c>이 만든다.
    /// 그런데 두 빌더는 <b>이미 있는 에셋을 건드리지 않는다</b>(기획팀이 인스펙터에서 고친 값을 지키려고 일부러 그렇게 만들었다).
    /// 그래서 빌더만 고치면 에셋은 옛 값 그대로 남고, 에셋을 지우고 메뉴를 다시 돌릴 때까지 아무도 모른다.
    /// </para>
    /// <para>
    /// <b>두 빌더는 테스트 어셈블리에서 직접 부를 수 없다.</b> 이 프로젝트에는 어셈블리 정의 파일(<c>.asmdef</c>)이 하나도 없어
    /// 빌더는 <c>Assembly-CSharp-Editor</c>에, 이 테스트는 <c>Assembly-CSharp</c>에 들어간다 — 참조 방향이 반대라 컴파일로는 닿지 않는다.
    /// 게다가 <c>CorridorCardBuilder.Define</c>은 <c>internal</c>, <c>RoomCardBuilder.Define</c>은 <c>private</c>이다.
    /// 그래서 에디터 도메인에 이미 올라와 있는 어셈블리를 <b>리플렉션</b>으로 찾아 부른다.
    /// 돌려받는 <see cref="RuleSO.Config"/>는 <c>Assembly-CSharp</c>에 정의된 타입이라 그대로 캐스팅해 읽을 수 있다.
    /// 빌더를 못 찾으면 조용히 통과시키지 않고 <c>Assert.Inconclusive</c>로 「검사하지 못했다」를 남긴다.
    /// </para>
    /// </summary>
    public sealed class AssetBuilderDriftTests
    {
        private const string CorridorBuilderType = "NightDuty.Editor.CorridorCardBuilder";
        private const string RoomBuilderType = "NightDuty.Editor.RoomCardBuilder";

        private const float FloatTolerance = 0.0001f;

        private MethodInfo _corridorDefine;
        private MethodInfo _roomDefine;

        [SetUp]
        public void SetUp()
        {
            _corridorDefine = FindDefine(CorridorBuilderType);
            _roomDefine = FindDefine(RoomBuilderType);
        }

        [TearDown]
        public void TearDown()
        {
            _corridorDefine = null;
            _roomDefine = null;
            EventBus.ClearAll();
        }

        /// <summary>로드된 어셈블리 전부를 훑어 빌더의 <c>Define(string)</c>을 찾는다. 없으면 null.</summary>
        private static MethodInfo FindDefine(string typeName)
        {
            System.Reflection.Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Type type;
                try
                {
                    type = assemblies[i].GetType(typeName, false);
                }
                catch (Exception)
                {
                    continue;
                }

                if (type == null)
                {
                    continue;
                }

                return type.GetMethod(
                    "Define",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                    null,
                    new[] { typeof(string) },
                    null);
            }

            return null;
        }

        /// <summary>카드 ID에 맞는 빌더를 골라 초기값을 만든다.</summary>
        private RuleSO.Config Build(string cardId)
        {
            MethodInfo define = cardId[0] == 'H' ? _corridorDefine : _roomDefine;
            return (RuleSO.Config)define.Invoke(null, new object[] { cardId });
        }

        private static void Same(List<string> drifts, string cardId, string field, object asset, object builder)
        {
            if (object.Equals(asset, builder))
            {
                return;
            }

            drifts.Add(cardId + "." + field + ": 에셋 " + Show(asset) + " ≠ 빌더 " + Show(builder));
        }

        private static void SameFloat(List<string> drifts, string cardId, string field, float asset, float builder)
        {
            if (Mathf.Abs(asset - builder) <= FloatTolerance)
            {
                return;
            }

            drifts.Add(cardId + "." + field + ": 에셋 " + asset + " ≠ 빌더 " + builder);
        }

        private static string Show(object value)
        {
            return value == null ? "(null)" : value.ToString();
        }

        /// <summary>빌더만 고치고 에셋을 안 고치는(또는 그 반대의) 사고를 막는다.</summary>
        [Test]
        public void 카드24장_에셋값이_빌더_초기값과_같다()
        {
            if (_corridorDefine == null || _roomDefine == null)
            {
                Assert.Inconclusive(
                    "빌더의 Define(string)에 닿지 못했습니다(복도: " + (_corridorDefine == null ? "없음" : "찾음")
                    + " / 교실·과학실·화장실: " + (_roomDefine == null ? "없음" : "찾음") + "). "
                    + "이 프로젝트에는 .asmdef가 없어 빌더는 Assembly-CSharp-Editor, 테스트는 Assembly-CSharp에 들어가므로 "
                    + "컴파일 참조로는 닿을 수 없고 리플렉션으로만 부릅니다. 에디터 바깥에서 돌렸다면 이 검사는 성립하지 않습니다.");
            }

            List<string> drifts = new List<string>();
            Dictionary<string, RuleSO> cards = DesignDriftKit.LoadCardsById(drifts);

            for (int i = 0; i < DesignDriftKit.AllCardIds.Length; i++)
            {
                string id = DesignDriftKit.AllCardIds[i];
                RuleSO asset;
                if (!cards.TryGetValue(id, out asset))
                {
                    continue;
                }

                RuleSO.Config built = Build(id);
                Assert.IsNotNull(built, id + ": 빌더가 초기값을 돌려주지 않았습니다.");

                Same(drifts, id, "Space", asset.Space, built.Space);
                Same(drifts, id, "UseEligibleBand", asset.UseEligibleBand, built.UseEligibleBand);
                Same(drifts, id, "EligibleAxis", asset.EligibleAxis, built.EligibleAxis);
                Same(drifts, id, "EligibleFrom", asset.EligibleFrom, built.EligibleFrom);
                Same(drifts, id, "EligibleTo", asset.EligibleTo, built.EligibleTo);
                Same(drifts, id, "FailureAxis", asset.FailureAxis, built.FailureAxis);
                Same(drifts, id, "FailureDelta", asset.FailureDelta, built.FailureDelta);
                Same(drifts, id, "SuccessDelta", asset.SuccessDelta, built.SuccessDelta);
                Same(drifts, id, "TriggerKind", asset.TriggerKind, built.TriggerKind);
                Same(drifts, id, "TriggerId", asset.TriggerId, built.TriggerId ?? string.Empty);
                Same(drifts, id, "IsLongTerm", asset.IsLongTerm, built.IsLongTerm);
                Same(drifts, id, "SettleAt", asset.SettleAt, built.SettleAt);
                SameFloat(drifts, id, "Radius", asset.Radius, built.Radius);
                SameFloat(drifts, id, "GraceSeconds", asset.GraceSeconds, built.GraceSeconds);
                SameFloat(drifts, id, "GazeSeconds", asset.GazeSeconds, built.GazeSeconds);

                string assetTargets = DesignDriftKit.Join(asset.TargetIds);
                string builtTargets = DesignDriftKit.JoinArray(built.TargetIds);
                if (assetTargets != builtTargets)
                {
                    drifts.Add(id + ".TargetIds: 에셋 " + assetTargets + " ≠ 빌더 " + builtTargets);
                }
            }

            if (drifts.Count > 0)
            {
                Assert.Fail(
                    "에셋과 빌더가 어긋난 곳 " + drifts.Count + "건:\n- " + string.Join("\n- ", drifts)
                    + "\n\n빌더는 이미 있는 에셋을 건드리지 않습니다. 빌더 쪽이 정답이면 해당 에셋을 지우고 "
                    + "NightDuty ▸ 모든 공간 카드 에셋 생성 메뉴를 다시 실행하고, 에셋 쪽이 정답이면 빌더의 Define을 고치십시오.");
            }
        }
    }

    /// <summary>
    /// B. 불변식 — 하루 배정이 성립하는 조건.
    /// <para>
    /// <see cref="DayDirector"/>는 하루 6장을 배치 2·청각 2·조도 2의 축 쿼터로 뽑는다.
    /// 어느 구간에서든 축당 열린 카드가 쿼터보다 적으면 그날 덱이 조용히 줄어든다 — 예외도 경고도 나지 않는다.
    /// 카드 자격을 한 장 고치거나 구간 경계를 옮기면 바로 여기가 깨진다.
    /// </para>
    /// </summary>
    public sealed class DeckFeasibilityInvariantTests
    {
        /// <summary>축당 Band0에서 열려 있어야 하는 카드 수(CLAUDE.md §2.3′). 수를 세어서 확인하지, ID를 박아 두지 않는다.</summary>
        /// <summary>
        /// Band0에서 축을 올리는 카드가 있어야 할 최소 장수. <b>하루 쿼터</b>와 같다 —
        /// 그날 그 축에 배정할 장수만큼은 열려 있어야 덱이 조용히 줄지 않는다.
        /// <para>
        /// 2026-09-21에는 이 수가 <b>정확히 3장</b>이어야 한다는 불변식이었다. 데드락을 막으려고 둔 것인데,
        /// 실은 장수가 아니라 <b>위반 델타의 합</b>이 다음 게이트를 넘느냐가 조건이다. 그리고 「정확히 3장」은
        /// 1·2일차 덱을 사실상 고정시켰다 — 2026-09-22 실측으로 2일차 6장 중 평균 4장이 1일차와 같았다.
        /// 그래서 장수 상한을 풀고, 아래 두 가지를 대신 잠근다.
        /// </para>
        /// </summary>
        private const int Band0OpenMinPerAxis = 2;

        private RuleSO _defaults;

        [SetUp]
        public void SetUp()
        {
            // RuleSO의 직렬화 기본값을 코드에서 직접 읽으려고 빈 인스턴스를 하나 만든다.
            _defaults = ScriptableObject.CreateInstance<RuleSO>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_defaults != null)
            {
                UnityEngine.Object.DestroyImmediate(_defaults);
            }

            _defaults = null;
            EventBus.ClearAll();
        }

        /// <summary>24장을 다 읽고, 한 장이라도 없으면 그 자리에서 멈춘다.</summary>
        private static Dictionary<string, RuleSO> RequireAllCards()
        {
            List<string> problems = new List<string>();
            Dictionary<string, RuleSO> cards = DesignDriftKit.LoadCardsById(problems);
            if (problems.Count > 0)
            {
                Assert.Fail("카드 에셋을 읽지 못했습니다:\n- " + string.Join("\n- ", problems));
            }

            return cards;
        }

        /// <summary>감각 3축을 모두 그 구간의 하한으로 올린 회차 축을 만든다(일차 하한이 세 축을 함께 올리는 것과 같은 모양).</summary>
        private static FearAxisSystem AxesAt(Band band)
        {
            FearAxisSystem axes = new FearAxisSystem();
            int value = Bands.LowerBound(band);
            for (int i = 0; i < DesignDriftKit.SensoryAxes.Length; i++)
            {
                if (value > 0)
                {
                    axes.Apply(DesignDriftKit.SensoryAxes[i], value, "드리프트검사", SpaceId.None);
                }
            }

            return axes;
        }

        /// <summary>그 구간에서 열려 있으면서 축을 실제로 올리는 카드 ID 목록. 위반 델타 0(S1)은 축을 못 올리므로 뺀다.</summary>
        private static List<string> OpenRaisingCards(Dictionary<string, RuleSO> cards, FearAxis axis, FearAxisSystem axes)
        {
            List<string> open = new List<string>();
            for (int i = 0; i < DesignDriftKit.AllCardIds.Length; i++)
            {
                RuleSO card;
                if (!cards.TryGetValue(DesignDriftKit.AllCardIds[i], out card))
                {
                    continue;
                }

                if (card.FailureAxis != axis || card.FailureDelta <= 0)
                {
                    continue;
                }

                if (!card.IsEligible(axes))
                {
                    continue;
                }

                open.Add(card.CardId);
            }

            return open;
        }

        private static int QuotaOf(FearAxis axis)
        {
            if (axis == FearAxis.Layout)
            {
                return DayDirector.QuotaLayout;
            }

            if (axis == FearAxis.Auditory)
            {
                return DayDirector.QuotaAuditory;
            }

            return DayDirector.QuotaIlluminance;
        }

        /// <summary>
        /// 회차 첫날 축 하나가 영영 0에 묶이는 데드락을 막는다.
        /// <para>
        /// <b>진짜 조건은 장수가 아니라 델타의 합</b>이다. 하루에 그 축으로 배정되는 장수(쿼터)만큼 뽑았을 때
        /// 가장 불리한 조합 — 즉 <b>위반 델타가 가장 작은 쿼터 장</b> — 의 합이 다음 구간 하한(24)에 닿아야,
        /// 「하루를 통째로 어겨도 구간이 안 올라가는 밤」이 없다.
        /// </para>
        /// </summary>
        [Test]
        public void Band0에서_하루_쿼터만큼_어기면_다음_구간에_닿는다()
        {
            Dictionary<string, RuleSO> cards = RequireAllCards();
            FearAxisSystem axes = AxesAt(Band.Band0);
            int gate = Bands.LowerBound(Band.Band1);

            for (int a = 0; a < DesignDriftKit.SensoryAxes.Length; a++)
            {
                FearAxis axis = DesignDriftKit.SensoryAxes[a];
                List<string> open = OpenRaisingCards(cards, axis, axes);
                open.Sort(StringComparer.Ordinal);

                int quota = QuotaOf(axis);
                Assert.GreaterOrEqual(
                    open.Count,
                    Band0OpenMinPerAxis,
                    axis + ": Band0에서 열리며 축을 올리는 카드가 " + open.Count + "장뿐입니다. 실제 목록 "
                        + DesignDriftKit.JoinArray(open.ToArray()));

                // 가장 불리한 조합 = 델타가 작은 쪽부터 쿼터 장.
                List<int> deltas = new List<int>();
                for (int i = 0; i < open.Count; i++)
                {
                    deltas.Add(cards[open[i]].FailureDelta);
                }
                deltas.Sort();

                int worst = 0;
                for (int i = 0; i < quota && i < deltas.Count; i++)
                {
                    worst += deltas[i];
                }

                Assert.GreaterOrEqual(
                    worst,
                    gate,
                    axis + ": Band0에서 하루 쿼터 " + quota + "장을 전부 어겨도 " + worst + "밖에 안 올라 구간 하한 "
                        + gate + "에 못 닿습니다. 그 축은 영영 Band0에 묶입니다. 열린 카드 "
                        + DesignDriftKit.JoinArray(open.ToArray()));
            }
        }

        /// <summary>
        /// 조우 장면에 묶인 카드가 <b>그 장면이 깔릴 수 있는 어느 날에도</b> 덱에 들어갈 수 있게 한다.
        /// <para>
        /// S5·T3는 트리거 ID가 곧 조우 장면 ID(<c>scene.sb</c>·<c>scene.ta</c>)다. 2026-09-22 이전에는
        /// 배치 자격이 Band1·Band2여서, 일차 하한이 그 값에 닿는 3·4일차부터만 장면을 깔 수 있었다.
        /// 기획서 v6 §4가 「날짜별 공간을 고정하지 않는다」를 요구해 두 카드를 Band0으로 내렸다.
        /// <b>여기가 깨지면 <c>EncounterDirector.EarliestDayOf</c>에 일차 제약을 되살려야 한다</b> —
        /// 그러지 않으면 장면은 깔리는데 카드가 없는 밤이 생긴다.
        /// </para>
        /// </summary>
        [Test]
        public void 조우_장면에_묶인_카드는_일차하한_어디서나_열린다()
        {
            Dictionary<string, RuleSO> cards = RequireAllCards();
            string[] ids = { "S5", "T3" };

            for (int i = 0; i < ids.Length; i++)
            {
                RuleSO card;
                Assert.IsTrue(cards.TryGetValue(ids[i], out card), ids[i] + " 에셋이 없다");

                for (int day = 1; day <= DayFloor.LastDay; day++)
                {
                    FearAxisSystem axes = new FearAxisSystem();
                    DayFloor.Apply(axes, day);
                    Assert.IsTrue(
                        card.IsEligible(axes),
                        ids[i] + "(" + card.EligibleAxis + " " + card.EligibleFrom + "~" + card.EligibleTo + ")가 "
                            + day + "일차 하한 " + DayFloor.Of(day) + "에서 자격 밖입니다. "
                            + "트리거 ID가 조우 장면(" + card.TriggerId + ")이라, 그날 장면이 깔리면 카드 없는 밤이 됩니다.");
                }
            }
        }

        /// <summary>
        /// 어느 카드도 <b>죽은 카드</b>가 되지 않게 한다.
        /// <para>
        /// 준수만 하는 플레이어의 축은 일차 하한에 정확히 머문다. 그래서 자격 창이 그 다섯 값
        /// (0 · 12 · 24 · 48 · 72) 중 어느 하나도 품지 못하면, 그 카드는 <b>잘하는 플레이어에게 영영 안 나온다</b>.
        /// 2026-09-22 실측에서 H6이 정확히 그랬다 — 배치 Band4(90↑) 전용이라 회차당 0.00회였다.
        /// </para>
        /// </summary>
        [Test]
        public void 준수만_하는_플레이어도_스물네장을_모두_만날_수_있다()
        {
            Dictionary<string, RuleSO> cards = RequireAllCards();
            List<string> dead = new List<string>();

            for (int i = 0; i < DesignDriftKit.AllCardIds.Length; i++)
            {
                RuleSO card;
                if (!cards.TryGetValue(DesignDriftKit.AllCardIds[i], out card))
                {
                    continue;
                }

                if (!card.UseEligibleBand)
                {
                    continue;   // 자격이 없으면 언제나 열린다.
                }

                bool reachable = false;
                for (int day = 1; day <= DayFloor.LastDay; day++)
                {
                    FearAxisSystem axes = new FearAxisSystem();
                    DayFloor.Apply(axes, day);
                    if (card.IsEligible(axes))
                    {
                        reachable = true;
                        break;
                    }
                }

                if (!reachable)
                {
                    dead.Add(card.CardId + "(" + card.EligibleAxis + " " + card.EligibleFrom + "~" + card.EligibleTo + ")");
                }
            }

            Assert.AreEqual(
                0,
                dead.Count,
                "일차 하한(0·12·24·48·72) 어디에서도 열리지 않는 카드가 있습니다 — 준수만 하는 플레이어는 영영 못 만납니다: "
                    + DesignDriftKit.JoinArray(dead.ToArray()));
        }

        /// <summary>구간이 올라갈 때 축 쿼터를 못 채워 하루 덱이 조용히 줄어드는 것을 막는다.</summary>
        [Test]
        public void 모든_구간에서_축당_쿼터만큼은_열려있다()
        {
            Dictionary<string, RuleSO> cards = RequireAllCards();
            List<string> problems = new List<string>();

            for (int b = 0; b < Bands.Count; b++)
            {
                Band band = (Band)b;
                FearAxisSystem axes = AxesAt(band);

                for (int a = 0; a < DesignDriftKit.SensoryAxes.Length; a++)
                {
                    FearAxis axis = DesignDriftKit.SensoryAxes[a];
                    int quota = QuotaOf(axis);
                    List<string> open = OpenRaisingCards(cards, axis, axes);
                    if (open.Count >= quota)
                    {
                        continue;
                    }

                    open.Sort(StringComparer.Ordinal);
                    problems.Add(
                        band + " · " + axis + ": 열린 카드 " + open.Count + "장 < 쿼터 " + quota + "장. 실제 목록 "
                            + DesignDriftKit.JoinArray(open.ToArray()));
                }
            }

            if (problems.Count > 0)
            {
                Assert.Fail("축 쿼터를 못 채우는 구간이 있습니다(그날 덱이 6장보다 줄어듭니다):\n- " + string.Join("\n- ", problems));
            }
        }

        /// <summary>구간 경계를 옮겨 「같은 축을 두 번 어기면 다음 구간」이라는 재설계의 전제가 깨지는 것을 막는다.</summary>
        [Test]
        public void 구간경계가_기본_위반델타의_배수라_두번_어기면_구간이_오른다()
        {
            int delta = _defaults.FailureDelta;
            Assert.AreEqual(12, delta, "RuleSO의 기본 위반 델타가 바뀌었습니다. 구간 경계(24의 배수)도 함께 옮겨야 합니다.");

            // 핵심 주장: Band0에서 두 번 어기면 반드시 Band1 이상.
            Band afterTwo = Bands.Of(Bands.LowerBound(Band.Band0) + (delta * 2));
            Assert.GreaterOrEqual(
                (int)afterTwo,
                (int)Band.Band1,
                "Band0에서 " + delta + "를 두 번 맞으면 " + (delta * 2) + " — 구간이 " + afterTwo + "에 머뭅니다. Band1 하한은 "
                    + Bands.LowerBound(Band.Band1) + "입니다.");

            // 같은 성질이 마지막 구간 직전까지 이어진다.
            for (int b = 0; b < Bands.Count - 1; b++)
            {
                Band band = (Band)b;
                Band next = Bands.Of(Bands.LowerBound(band) + (delta * 2));
                Assert.Greater(
                    (int)next,
                    b,
                    band + " 하한 " + Bands.LowerBound(band) + "에서 두 번 어기면 " + (Bands.LowerBound(band) + (delta * 2))
                        + " — 그래도 " + next + "에 머뭅니다.");
            }

            // Band1~Band3 하한은 위반 두 번(24)의 배수다. Band4의 90은 일부러 배수가 아니다 —
            // 종료 직전 경고 구간을 좁히려고 Band3을 18칸으로 줄인 결과다(CLAUDE.md §2.3).
            for (int b = 1; b <= 3; b++)
            {
                int lower = Bands.LowerBound((Band)b);
                Assert.AreEqual(
                    0,
                    lower % (delta * 2),
                    "Band" + b + " 하한 " + lower + "이 " + (delta * 2) + "의 배수가 아닙니다.");
            }
        }

        /// <summary>일차 하한 곡선이 뒤집히거나 Band4까지 공짜로 올라가는 것을 막는다.</summary>
        [Test]
        public void 일차하한은_단조증가하고_Band4에_닿지_않는다()
        {
            for (int day = 1; day <= DayFloor.LastDay; day++)
            {
                Assert.GreaterOrEqual(
                    DayFloor.Of(day),
                    DayFloor.Of(day - 1),
                    day + "일차 하한 " + DayFloor.Of(day) + "이 " + (day - 1) + "일차 " + DayFloor.Of(day - 1) + "보다 낮습니다.");
            }

            // 2일차부터는 전날보다 반드시 높다(1일차는 0으로 시작한다).
            for (int day = 2; day <= DayFloor.LastDay; day++)
            {
                Assert.Greater(
                    DayFloor.Of(day),
                    DayFloor.Of(day - 1),
                    day + "일차 하한이 전날과 같습니다(" + DayFloor.Of(day) + "). 날이 갈수록 나빠지지 않습니다.");
            }

            int last = DayFloor.Of(DayFloor.LastDay);
            int band4 = Bands.LowerBound(Band.Band4);
            Assert.Less(
                last,
                band4,
                "마지막 일차 하한 " + last + "이 Band4 하한 " + band4 + " 이상입니다. Band4는 「당신이 어겨서 여기까지 왔다」는 구간이라 "
                    + "하한으로 공짜로 주면 경고로서의 뜻이 사라집니다.");
        }

        /// <summary>신뢰 게이트와 구간 경계가 따로 놀아 신뢰 한 칸이 비는 것을 막는다.</summary>
        [Test]
        public void 역설_최소신뢰가_Band1_하한과_같다()
        {
            Assert.AreEqual(
                Bands.LowerBound(Band.Band1),
                ParadoxDirector.MinTrust,
                "ParadoxDirector.MinTrust(" + ParadoxDirector.MinTrust + ")와 Band1 하한("
                    + Bands.LowerBound(Band.Band1) + ")이 다릅니다. 그 사이 신뢰 값은 Band1인데도 역설이 한 쌍도 안 나옵니다.");
        }
    }

    /// <summary>
    /// C. 문서 드리프트 — <c>CLAUDE.md</c>의 숫자가 코드와 같은지.
    /// <para>
    /// <c>CLAUDE.md</c>는 다음 세션이 정본으로 믿는 파일이다. 여기가 낡으면 없어진 규칙을 그대로 구현한다.
    /// 그래서 <b>특정 문자열이 본문에 있는지·없는지</b>만 본다 — 문장을 해석하지 않는다.
    /// </para>
    /// <para>
    /// 개정 이력(<c>&gt; </c>로 시작하는 인용 블록)과 「되살리지 마십시오」 줄에는 <b>일부러 옛 값을 적어 둔다.</b>
    /// 그 줄들은 본문에서 뺀다.
    /// </para>
    /// </summary>
    public sealed class ClaudeMdDriftTests
    {
        /// <summary>구간 표기에 쓰는 EN DASH(U+2013). 하이픈(-)과 눈으로 구별되지 않아 이스케이프로 적는다.</summary>
        private const string Dash = "–";

        private string _path;
        private string[] _lines;

        [SetUp]
        public void SetUp()
        {
            _path = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", "CLAUDE.md"));
            _lines = System.IO.File.Exists(_path) ? System.IO.File.ReadAllLines(_path) : null;
        }

        [TearDown]
        public void TearDown()
        {
            _lines = null;
            _path = null;
            EventBus.ClearAll();
        }

        /// <summary>파일이 없으면 실패가 아니라 건너뛴다. 빌드 환경에 따라 없을 수 있다.</summary>
        private void RequireFile()
        {
            if (_lines == null)
            {
                Assert.Inconclusive("CLAUDE.md가 없습니다(" + _path + "). 빌드 환경에 따라 없을 수 있어 실패로 세지 않습니다.");
            }
        }

        /// <summary>개정 이력과 「되살리지 마십시오」 줄은 옛 값을 일부러 적어 두는 자리라 본문에서 뺀다.</summary>
        private static bool IsExempt(string line)
        {
            if (line.TrimStart().StartsWith(">", StringComparison.Ordinal))
            {
                return true;
            }

            return line.Contains("되살리지");
        }

        /// <summary>코드가 말하는 현재 경계를 한 줄로 만든다. 실패 메시지의 「코드는 △△」 쪽이다.</summary>
        private static string CodeBounds()
        {
            List<string> parts = new List<string>();
            for (int b = 0; b < Bands.Count; b++)
            {
                Band band = (Band)b;
                parts.Add(band + " = " + BandText(band));
            }

            return string.Join(" · ", parts);
        }

        private static string BandText(Band band)
        {
            return Bands.LowerBound(band) + Dash + Bands.UpperBound(band);
        }

        /// <summary>그 문자열이 본문(면제 줄 제외)에 처음 나오는 줄 번호(1부터). 없으면 0.</summary>
        private int FindInBody(string needle)
        {
            for (int i = 0; i < _lines.Length; i++)
            {
                if (IsExempt(_lines[i]))
                {
                    continue;
                }

                if (_lines[i].Contains(needle))
                {
                    return i + 1;
                }
            }

            return 0;
        }

        /// <summary>구간 경계를 코드에서 옮긴 뒤 문서에 옛 숫자가 남는 것을 막는다.</summary>
        [Test]
        public void 옛_구간경계가_본문에_남아있지_않다()
        {
            RequireFile();

            string[] banned =
            {
                "25~49",
                "50~74",
                "Band3 = 75~89",
                "0" + Dash + "24",
                "25" + Dash + "49",
                "50" + Dash + "74"
            };

            string code = CodeBounds();
            List<string> hits = new List<string>();

            for (int i = 0; i < _lines.Length; i++)
            {
                if (IsExempt(_lines[i]))
                {
                    continue;
                }

                for (int b = 0; b < banned.Length; b++)
                {
                    if (_lines[i].Contains(banned[b]))
                    {
                        hits.Add("CLAUDE.md " + (i + 1) + "줄: " + banned[b] + " — 코드는 " + code);
                    }
                }
            }

            if (hits.Count > 0)
            {
                Assert.Fail(
                    "폐기된 구간 경계가 본문에 남아 있습니다 " + hits.Count + "건:\n- " + string.Join("\n- ", hits)
                    + "\n(개정 이력과 「되살리지 마십시오」 줄은 세지 않았습니다.)");
            }
        }

        /// <summary>코드의 경계를 고쳐 놓고 문서에 새 숫자를 안 적는 것을 막는다.</summary>
        [Test]
        public void 현재_구간경계가_본문에_그대로_적혀있다()
        {
            RequireFile();

            string code = CodeBounds();
            List<string> missing = new List<string>();

            for (int b = 0; b < Bands.Count; b++)
            {
                Band band = (Band)b;
                string want = BandText(band);
                if (FindInBody(want) > 0)
                {
                    continue;
                }

                missing.Add("CLAUDE.md 본문에 「" + want + "」(" + band + ")가 없습니다 — 코드는 " + code);
            }

            if (missing.Count > 0)
            {
                Assert.Fail(
                    "현재 구간 경계가 문서에 적혀 있지 않습니다 " + missing.Count + "건:\n- " + string.Join("\n- ", missing)
                    + "\n경계 숫자는 Bands.cs의 LowerBounds 배열이 정본입니다. 문서를 그 값으로 고치십시오.");
            }
        }

        /// <summary>일차 하한 곡선을 코드에서 바꾸고 문서를 안 고치는 것을 막는다.</summary>
        [Test]
        public void 일차하한_곡선이_본문에_적혀있다()
        {
            RequireFile();

            List<string> values = new List<string>();
            for (int day = 1; day <= DayFloor.LastDay; day++)
            {
                values.Add(DayFloor.Of(day).ToString());
            }

            string curve = string.Join("/", values);
            if (FindInBody(curve) > 0)
            {
                return;
            }

            // 곡선을 한 줄로 안 적고 표로 적은 경우도 받아 준다.
            // 최소 조건: 「하한」이라는 말과 함께 마지막 두 일차의 값이 같은 줄에 있어야 한다.
            string last = DayFloor.Of(DayFloor.LastDay).ToString();
            string secondLast = DayFloor.Of(DayFloor.LastDay - 1).ToString();

            for (int i = 0; i < _lines.Length; i++)
            {
                if (IsExempt(_lines[i]))
                {
                    continue;
                }

                string line = _lines[i];
                if (line.Contains("하한") && line.Contains(last) && line.Contains(secondLast))
                {
                    return;
                }
            }

            Assert.Fail(
                "CLAUDE.md 본문에 일차 하한 곡선이 없습니다 — 코드는 " + curve
                + " (DayFloor.cs의 Floors 배열). 한 줄 표기(" + curve + ")나, 「하한」과 함께 "
                + secondLast + "·" + last + "이 들어간 표 줄 가운데 하나는 있어야 합니다.");
        }
    }
}

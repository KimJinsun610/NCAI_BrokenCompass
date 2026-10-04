using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace NightDuty.Tests
{
    /// <summary>
    /// 점검 이상의 보이는 정도(41차, 최종 기획서 「공간별 설계」). 수치는 <see cref="AnomalyLook"/>, 씬 연출은 화면 쪽 <c>InspectionAnomalies</c>.
    /// 화면 쪽 어셈블리를 참조하지 않으므로 대역 소품 표는 직렬화 필드로, 연출 코드는 소스 글자로 확인한다.
    /// </summary>
    public sealed class AnomalyLookTests
    {
        private const string PropsPath = "Assets/_Game/Resources/InspectionAnomalyProps.asset";
        private const string ScenePath = "Assets/_Game/Flow/Presentation/InspectionAnomalies.cs";

        private static readonly Band[] Bands = { Band.Band1, Band.Band2, Band.Band3, Band.Band4 };

        [Test]
        public void 옮김은_1구간부터_15도_이상이고_구간이_오를수록_커진다()
        {
            float door = 0f;
            float turn = 0f;
            float pull = 0f;
            foreach (Band b in Bands)
            {
                Assert.GreaterOrEqual(AnomalyLook.DoorDegrees(b), AnomalyLook.MinMoveDegrees, "문 " + b);
                Assert.GreaterOrEqual(AnomalyLook.TurnDegrees(b), AnomalyLook.MinMoveDegrees, "모형 " + b);
                Assert.Greater(AnomalyLook.DoorDegrees(b), door, "문 " + b);
                Assert.Greater(AnomalyLook.TurnDegrees(b), turn, "모형 " + b);
                Assert.Greater(AnomalyLook.PullMeters(b), pull, "의자 " + b);
                door = AnomalyLook.DoorDegrees(b);
                turn = AnomalyLook.TurnDegrees(b);
                pull = AnomalyLook.PullMeters(b);
            }

            Assert.AreEqual(180f, AnomalyLook.TurnDegrees(Band.Band4), "4구간이면 모형이 완전히 등을 돌린다");
        }

        [Test]
        public void 켬과_빛은_구간이_오를수록_뚜렷해지고_구간0은_없다()
        {
            float spread = 0f;
            float glow = 0f;
            float far = 0f;
            float strength = 0f;
            foreach (Band b in Bands)
            {
                Assert.Greater(AnomalyLook.SpreadMeters(b), spread, "물 " + b);
                Assert.Greater(AnomalyLook.Glow(b), glow, "빛 " + b);
                Assert.Greater(AnomalyLook.FarPick(b), far, "화분 " + b);
                Assert.Greater(AnomalyLook.Strength(b), strength, "강도 " + b);
                spread = AnomalyLook.SpreadMeters(b);
                glow = AnomalyLook.Glow(b);
                far = AnomalyLook.FarPick(b);
                strength = AnomalyLook.Strength(b);
            }

            Assert.AreEqual(1f, AnomalyLook.Strength(Band.Band4));
            Assert.AreEqual(1f, AnomalyLook.FarPick(Band.Band4), "4구간이면 가장 먼 책상");
            Assert.AreEqual(0f, AnomalyLook.Strength(Band.Band0));
            Assert.AreEqual(0f, AnomalyLook.SpreadMeters(Band.Band0));
            Assert.AreEqual(0f, AnomalyLook.Glow(Band.Band0));
            Assert.AreEqual(0f, AnomalyLook.DoorDegrees(Band.Band0));
        }

        [Test]
        public void 광과민_옵션의_깜빡임은_2Hz_이하이고_평소보다_느리다()
        {
            foreach (Band b in Bands)
            {
                float slow = AnomalyLook.FlickerHz(b, true);
                Assert.Greater(slow, 0f, b.ToString());
                Assert.LessOrEqual(slow, AnomalyLook.SlowFlickerMaxHz, b.ToString());
                Assert.Less(slow, AnomalyLook.FlickerHz(b, false), b.ToString());
            }

            Assert.LessOrEqual(AnomalyLook.SlowFlickerMaxHz, 2f, "최종 기획서: 광과민 옵션은 2Hz 이하");
        }

        [Test]
        public void 소리_틀을_뺀_모든_항목에_보이는_이상이_있다()
        {
            int looks = 0;
            foreach (InspectionItem item in InspectionCatalog.All)
            {
                Assert.AreEqual(item.Template != AnomalyTemplate.Sound, AnomalyLook.HasLook(item), item.ToString());
                if (AnomalyLook.HasLook(item)) looks++;
            }

            Assert.AreEqual(12, looks, "17개 중 [소리] 5개를 뺀 12개");
            Assert.IsFalse(AnomalyLook.HasLook(null));
        }

        [Test]
        public void 이상_연출_코드가_보이는_항목을_모두_다룬다()
        {
            string code = System.IO.File.ReadAllText(ScenePath);
            foreach (InspectionItem item in InspectionCatalog.All)
            {
                if (!AnomalyLook.HasLook(item)) continue;
                bool handled = code.Contains("case \"" + item.Id + "\":") || code.Contains("itemId == \"" + item.Id + "\"");
                Assert.IsTrue(handled, item.Id + "의 이상 연출이 InspectionAnomalies에 없다");
            }
        }

        [Test]
        public void 옮김_대역_소품표에_옮김_항목의_프리팹이_있다()
        {
            Object asset = AssetDatabase.LoadMainAssetAtPath(PropsPath);
            Assert.IsNotNull(asset, PropsPath + " 없음 — 야간근무/연출/점검 이상 소품 표 다시 만들기");
            SerializedProperty entries = new SerializedObject(asset).FindProperty("entries");
            Assert.IsNotNull(entries);
            HashSet<string> ids = new HashSet<string>();
            for (int i = 0; i < entries.arraySize; i++)
            {
                SerializedProperty e = entries.GetArrayElementAtIndex(i);
                string id = e.FindPropertyRelative("itemId").stringValue;
                InspectionItem item = InspectionCatalog.Find(id);
                Assert.IsNotNull(item, id);
                Assert.AreEqual(AnomalyTemplate.Move, item.Template, id + "는 [옮김]이 아니다");
                Assert.IsNotNull(e.FindPropertyRelative("prefab").objectReferenceValue, id + " 프리팹 없음");
                ids.Add(id);
            }

            // 정적 배칭으로 묶여 원본을 옮길 수 없는 둘은 반드시 있어야 한다.
            Assert.IsTrue(ids.Contains("C-1"), "C-1 화분");
            Assert.IsTrue(ids.Contains("L-1"), "L-1 의자");
        }
    }
}

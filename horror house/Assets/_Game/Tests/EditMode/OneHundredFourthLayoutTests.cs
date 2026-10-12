using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace NightDuty.Tests
{
    /// <summary>
    /// 104차 배치 연출 1단계 — 김진선님 관물대·서랍 프리셋 연결(<c>LayoutCabinets</c>). 화면 쪽 어셈블리를 참조하지 않으므로 에셋을 직렬화 필드로 읽는다.
    /// 런타임이 이름으로 찾는 프리셋이 연출 프리셋 표에 프리팹과 함께 있는지 본다(빠지면 조용히 아무 연출도 안 선다).
    /// </summary>
    public sealed class OneHundredFourthLayoutTests
    {
        private const string PresetsPath = "Assets/_Game/Resources/HorrorPresets.asset";

        [TestCase("HorrorEvent_CabinetCreak")]
        [TestCase("HorrorEvent_CabinetRampage")]
        [TestCase("HorrorEvent_DrawerRampage")]
        public void 배치_연출이_쓰는_진선님_프리셋이_표에_있다(string preset)
        {
            Object asset = AssetDatabase.LoadAssetAtPath<Object>(PresetsPath);
            Assert.IsNotNull(asset, PresetsPath);
            SerializedProperty entries = new SerializedObject(asset).FindProperty("entries");
            Assert.IsNotNull(entries);
            for (int i = 0; i < entries.arraySize; i++)
            {
                SerializedProperty e = entries.GetArrayElementAtIndex(i);
                if (e.FindPropertyRelative("name").stringValue != preset) continue;
                Assert.IsNotNull(e.FindPropertyRelative("prefab").objectReferenceValue, preset + " 프리팹이 비었다");
                return;
            }

            Assert.Fail(preset + "가 표에 없다(야간근무/연출/진선님 연출 프리셋 표 다시 만들기)");
        }
    }
}

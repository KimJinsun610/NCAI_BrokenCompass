using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace NightDuty.Tests
{
    /// <summary>
    /// 104차 근무일지 서명 소리(사용자: 「약간 판서 소리 느낌나는 사운드로 변경하고, 적용해줘」).
    /// 소리 표 `duty.sign`이 판서 전용 클립을 가리키고, 1초 안의 짧은 소리라 `DutyLogBook`이 끊지 않는다(DutyLogBook은 Assembly-CSharp라 상수 대신 값으로 본다).
    /// </summary>
    public sealed class OneHundredFourthSignSoundTests
    {
        private const string TablePath = "Assets/_Game/Resources/DirectionSounds.asset";

        [Test]
        public void 서명_소리는_판서_전용_클립이다()
        {
            Object table = AssetDatabase.LoadMainAssetAtPath(TablePath);
            Assert.IsNotNull(table, TablePath + " 없음");
            SerializedProperty entries = new SerializedObject(table).FindProperty("entries");
            AudioClip clip = null;
            for (int i = 0; i < entries.arraySize; i++)
            {
                SerializedProperty e = entries.GetArrayElementAtIndex(i);
                if (e.FindPropertyRelative("key").stringValue != "duty.sign") continue;
                clip = e.FindPropertyRelative("clip").objectReferenceValue as AudioClip;
            }

            Assert.IsNotNull(clip, "소리 표에 duty.sign 클립이 없다 — 야간근무/연출/소리 표만 다시 만들기");
            Assert.AreEqual("SFX_CLASS_DutySign", clip.name);
            Assert.That(clip.length, Is.InRange(0.6f, 1.0f), "서명은 1초 안에 끝나는 짧은 소리(DutyLogBook.SoundSeconds 1초 — 끊지 않고 끝까지)");
        }
    }
}

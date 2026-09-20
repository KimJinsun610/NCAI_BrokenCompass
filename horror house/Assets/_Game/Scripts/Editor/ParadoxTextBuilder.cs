using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace NightDuty.EditorTools
{
    /// <summary>
    /// 역설 문자 23개를 기존 수칙 카드 에셋에 채운다(2026-09-19 기획서 11·12절).
    /// <para>
    /// 역설은 짝 수칙 1장에 1:1로 붙는다. 따르면 그 카드의 <b>위반 델타</b>가 그대로 적용되고,
    /// 거절하면 짝 수칙을 지킨 것이므로 <b>준수 델타(신뢰)</b>만 오른다. 문자 쪽에 붙는 별도 벌점은 없다.
    /// S1은 1일차 고정 카드라 역설이 없다 — 그래서 24장 중 23쌍이다.
    /// </para>
    /// <para>본문은 기획서 원문이다. 손으로 고치지 말고 기획서가 바뀌면 이 표를 고친다.</para>
    /// </summary>
    public static class ParadoxTextBuilder
    {
        private const string MenuPath = "NightDuty/역설 문자 채우기";

        // cardId, paradoxId, 문자 전문
        private static readonly string[,] Table =
        {
            { "H1", "P1", "개방 상태가 잘못 전달되었습니다. 저절로 열린 복도 문은 직접 닫고 지나가 주십시오. 먼저 나와야 할 분은 나오신 것으로 처리하겠습니다." },
            { "H2", "P2", "방금 등 뒤에서 닫힌 문을 다시 확인해 주십시오. 해당 문에 시선을 두고 세 초 이상 지켜보시면 됩니다. 이쪽에서는 아직 열린 것으로 보입니다." },
            { "H3", "P3", "이번 복도 통행에 한해 손전등을 켠 채 끝까지 이동하십시오. 불빛이 있어야 근무자 한 분이 지나가신 것으로 확인됩니다." },
            { "H4", "P4", "중앙 상자의 수령인이 도착한 것으로 확인됩니다. 상자 바로 앞까지 와 주십시오. 수령인 성함은 근무자 정보와 대조하지 않겠습니다." },
            { "H5", "P5", "낙하 지점 확인을 요청드립니다. 천장 조각 바로 옆으로 이동해 주십시오. 그 자리에 계셔야 위에서도 위치를 알 수 있습니다." },
            { "H6", "P6", "복도 식재의 현장 확인이 남아 있습니다. 나무 바로 아래까지 이동하십시오. 없는 것으로 처리하려면 가까이 계신 분의 확인이 필요합니다." },
            { "C1", "P7", "점검 순서를 정정합니다. 분필 소리가 세 번 들린 1-1을 먼저 방문하십시오. 1-3 점검 완료를 기다리지 마십시오. 출석 정정은 이번에 한해 가능하다는 연락을 받았습니다." },
            { "C2", "P8", "출입문을 향한 좌석 옆으로 와 주십시오. 좌석의 방향은 정상입니다. 자리를 비워 두는 시간이 길어지고 있어 확인을 요청드립니다." },
            { "C3", "P9", "퇴실 전, 방금 소리가 난 뒤쪽 책상을 세 초 이상 바라봐 주십시오. 눈을 떼지 않아서야 확인이 됩니다. 이름을 부르는 절차는 생략하겠습니다." },
            { "C4", "P10", "칠판과 의자 소리가 아직 들리는 동안 교탁 바로 앞으로 이동하십시오. 두 소리가 모두 멎으면 점검을 받는 쪽이 바뀝니다." },
            { "C5", "P11", "주황빛 교실의 점검 방법이 변경되었습니다. 붉은 잔광만 남은 경우도 포함하여, 퇴실할 때까지 손전등을 켜 두십시오. 빈 자리는 저희가 확인하겠습니다." },
            { "C6", "P12", "직접 여신 1-3 문은 이번 근무가 끝날 때까지 열어 두십시오. 뒤에 나올 분의 퇴실이 아직 끝나지 않았습니다. 문을 닫는 업무는 그분에게 전달했습니다." },
            { "S2", "P13", "과학실 내부에 근무자 한 분이 남아 있습니다. 이번 근무가 끝나기 전에 과학실 문 안쪽 점검 위치로 다시 이동하십시오. 이미 나오셨다는 답변은 안에서도 받았습니다." },
            { "S3", "P14", "유리 접촉음이 들린 책상 바로 옆으로 이동하십시오. 기구 사이 간격에는 이상이 없습니다. 소리가 날 수 없다는 사실까지 현장에서 확인해 주셔야 합니다." },
            { "S4", "P15", "짙은 주황빛의 과학실 지정 점검등을 두 초 이상 올려다봐 주십시오. 시선을 유지하시면 확인이 끝납니다. 위쪽 담당자는 근무자께서 먼저 보시기를 기다리고 있습니다." },
            { "S5", "P16", "보관 위치를 벗어난 모형 바로 옆으로 와 주십시오. 모형의 위치는 확인했습니다. 이번에는 그 옆에 계실 분을 확인하는 절차입니다." },
            { "S6", "P17", "유리 기구 점검 구역에서는 손전등을 켠 채 점검하고 나오십시오. 기구 안이 비어 있더라도 그대로 비춰 주십시오. 밝은 곳에서도 비어 있는지 확인이 필요합니다." },
            { "T1", "P18", "저절로 움직인 점검칸 문은 직접 닫아 주십시오. 안쪽 이용자가 닫아 달라는 뜻으로 열어 둔 것이라는 설명을 받았습니다." },
            { "T2", "P19", "물 내리는 소리가 완전히 멎을 때까지 화장실 안에서 기다려 주십시오. 먼저 나오실 필요는 없습니다. 다음 이용자는 이미 들어와 계십니다." },
            { "T3", "P20", "방금 닫으신 안쪽 점검칸 문을 다시 열어 주십시오. 이용이 끝난 것으로 정정되었습니다. 안에 계신 분도 두 번째 확인을 요청하셨습니다." },
            { "T4", "P21", "세면대 앞까지 이동해 주십시오. 근무자를 부르는 소리는 배관 소음으로 분류했습니다. 가까이서도 같은 호칭이 들리는지만 확인하면 됩니다." },
            { "T5", "P22", "칸 아래 빛이 보이는 동안에는 손전등을 켜고, 화장실에서 나오실 때까지 유지하십시오. 안쪽에서 외부 조명을 요청했습니다. 요청한 분의 성함은 받지 않았습니다." },
            { "T6", "P23", "열린 점검칸의 문턱 안으로 들어가 주십시오. 공용부에서는 빈 칸으로 보일 수 있습니다. 안에서도 비어 있는지 확인이 필요합니다." },
        };

        [MenuItem(MenuPath)]
        public static void Fill()
        {
            Dictionary<string, RuleSO> cards = new Dictionary<string, RuleSO>();
            foreach (string guid in AssetDatabase.FindAssets("t:RuleSO"))
            {
                RuleSO card = AssetDatabase.LoadAssetAtPath<RuleSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (card != null && !string.IsNullOrEmpty(card.CardId))
                {
                    cards[card.CardId] = card;
                }
            }

            int written = 0;
            List<string> missing = new List<string>();
            for (int i = 0; i < Table.GetLength(0); i++)
            {
                string cardId = Table[i, 0];
                if (!cards.TryGetValue(cardId, out RuleSO card))
                {
                    missing.Add(cardId);
                    continue;
                }

                SerializedObject so = new SerializedObject(card);
                so.FindProperty("_paradoxId").stringValue = Table[i, 1];
                so.FindProperty("_paradoxText").stringValue = Table[i, 2];
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(card);
                written++;
            }

            AssetDatabase.SaveAssets();

            string note = "[역설] " + written + "장에 문자를 채웠습니다(S1 제외 23장 기준).";
            if (missing.Count > 0)
            {
                note += " 카드 에셋을 못 찾음: " + string.Join(", ", missing);
                Debug.LogWarning(note);
                return;
            }

            Debug.Log(note);
        }
    }
}

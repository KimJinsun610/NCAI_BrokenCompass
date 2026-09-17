using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace NightDuty.Editor
{
    /// <summary>
    /// 열린 씬의 <see cref="JudgeTarget"/> ID와 편성표(<see cref="NightDeckTableSO"/>)의 카드 참조를 대조한다.
    /// 결과는 콘솔에만 남긴다(대화상자 없음 — CLI/MCP로 실행 가능).
    /// </summary>
    public static class SceneTargetValidator
    {
        /// <summary>메뉴: 씬 대상 검사.</summary>
        [MenuItem("NightDuty/씬 대상 검사 (열린 씬 × 편성표)", false, 130)]
        public static void Run()
        {
            HashSet<string> sceneIds = CollectSceneIds(out List<string> duplicates, out List<string> inactive);
            NightDeckTableSO table = Resources.Load<NightDeckTableSO>(NightDeckTableSO.ResourcePath);

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("[씬 대상 검사] 씬 ID " + sceneIds.Count + "개");

            if (inactive.Count > 0)
            {
                // 런타임 등록부는 켜진 표식만 담는다. 밤 시작 때 꺼져 있으면 그 ID를 쓰는 카드는 미판정이 된다.
                sb.AppendLine("  주의 — 꺼져 있는 표식(밤 시작 때 켜져 있어야 판정됨): " + string.Join(", ", inactive));
            }

            if (duplicates.Count > 0)
            {
                sb.AppendLine("  참고 — 여러 오브젝트가 같은 ID를 가짐(구역을 콜라이더 여럿으로 만든 경우면 정상): " + string.Join(", ", duplicates));
            }

            if (sceneIds.Count == 0)
            {
                sb.AppendLine("  씬에 JudgeTarget이 없습니다. 이 상태로 밤을 시작하면 참조 검사를 건너뜁니다(테스트 씬이면 정상).");
                Debug.Log(sb.ToString());
                return;
            }

            if (table == null)
            {
                sb.AppendLine("  편성표(Resources/" + NightDeckTableSO.ResourcePath + ")가 없어 카드 대조를 건너뜁니다.");
                Debug.LogWarning(sb.ToString());
                return;
            }

            HashSet<string> used = new HashSet<string>();
            List<string> refs = new List<string>();
            bool anyMissing = false;

            for (int day = 1; day <= Mathf.Max(1, table.DayCount); day++)
            {
                IReadOnlyList<RuleSO> deck = table.DeckFor(day);
                List<string> lines = RuleReferenceCheck.Report(deck, sceneIds);
                foreach (RuleSO card in deck)
                {
                    if (card == null)
                    {
                        continue;
                    }

                    refs.Clear();
                    card.CollectReferences(refs);
                    used.UnionWith(refs);
                }

                if (lines.Count == 0)
                {
                    sb.AppendLine("  " + day + "일차: 통과 (" + deck.Count + "장)");
                }
                else
                {
                    anyMissing = true;
                    sb.AppendLine("  " + day + "일차: 누락 — 이 카드들은 밤 시작 때 미판정이 됩니다.");
                    for (int i = 0; i < lines.Count; i++)
                    {
                        sb.AppendLine("    " + lines[i]);
                    }
                }
            }

            List<string> unused = new List<string>();
            foreach (string id in sceneIds)
            {
                if (!used.Contains(id))
                {
                    unused.Add(id);
                }
            }

            if (unused.Count > 0)
            {
                unused.Sort(System.StringComparer.Ordinal);
                sb.AppendLine("  참고 — 편성표 카드가 쓰지 않는 씬 ID(오타인지 확인): " + string.Join(", ", unused));
            }

            if (anyMissing)
            {
                Debug.LogWarning(sb.ToString());
            }
            else
            {
                Debug.Log(sb.ToString());
            }
        }

        /// <summary>열린 씬(비활성 포함)의 모든 표식 ID를 모은다. <paramref name="inactive"/>는 지금 꺼져 있는 표식의 ID.</summary>
        public static HashSet<string> CollectSceneIds(out List<string> duplicates, out List<string> inactive)
        {
            inactive = new List<string>();
            HashSet<string> ids = new HashSet<string>(System.StringComparer.Ordinal);
            Dictionary<string, JudgeTarget> owner = new Dictionary<string, JudgeTarget>(System.StringComparer.Ordinal);
            duplicates = new List<string>();

            JudgeTarget[] targets = Object.FindObjectsByType<JudgeTarget>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int t = 0; t < targets.Length; t++)
            {
                IReadOnlyList<string> list = targets[t].Ids;
                bool enabledNow = targets[t].enabled && targets[t].gameObject.activeInHierarchy;
                for (int i = 0; i < list.Count; i++)
                {
                    string id = list[i] != null ? list[i].Trim() : string.Empty;
                    if (id.Length == 0)
                    {
                        continue;
                    }

                    JudgeTarget first;
                    if (owner.TryGetValue(id, out first))
                    {
                        if (first != targets[t] && !duplicates.Contains(id))
                        {
                            duplicates.Add(id);
                        }
                    }
                    else
                    {
                        owner[id] = targets[t];
                    }

                    ids.Add(id);
                    if (!enabledNow && !inactive.Contains(id))
                    {
                        inactive.Add(id);
                    }
                }
            }

            return ids;
        }
    }
}

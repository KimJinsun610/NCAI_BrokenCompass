using System.Collections.Generic;
using NightDuty;
using UnityEditor;
using UnityEngine;

/// <summary>
/// <see cref="InspectionAnomalyPropsSO"/>를 채운다(2026-10-04 41차). 열린 씬의 점검 대상(<c>inspect.*</c>) 중 [옮김] 틀의 부모 소품이
/// 프리팹 인스턴스이면 그 원본 프리팹을 적는다. 씬 소품을 바꿨으면 근무 씬을 열고 다시 돌린다.
/// </summary>
public static class InspectionAnomalyPropsBuilder
{
    public const string AssetPath = "Assets/_Game/Resources/InspectionAnomalyProps.asset";

    /// <summary>항목별 이상 연출 프리팹(김진선님 폴더 — 경로가 바뀌면 여기만 고친다).</summary>
    private static readonly string[,] EventPrefabs =
    {
        { "H-2", "Assets/3.2 Programmer_Kim/03 Prefebs/04 Horror/Bloody/HorrorEvent_BloodyFountain.prefab" },
    };

    [MenuItem("야간근무/연출/점검 이상 소품 표 다시 만들기")]
    public static void BuildMenu()
    {
        Debug.Log("[InspectionAnomalyProps] " + Build());
    }

    /// <summary>표를 다시 만들고 요약을 돌려준다.</summary>
    public static string Build()
    {
        List<InspectionAnomalyPropsSO.Entry> list = new List<InspectionAnomalyPropsSO.Entry>();
        List<string> missing = new List<string>();
        JudgeTarget[] targets = Object.FindObjectsByType<JudgeTarget>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < InspectionCatalog.All.Count; i++)
        {
            InspectionItem item = InspectionCatalog.All[i];
            if (item.Template != AnomalyTemplate.Move) continue;
            GameObject prefab = null;
            for (int t = 0; t < targets.Length && prefab == null; t++)
            {
                if (!Has(targets[t], item.TargetId) || targets[t].transform.parent == null) continue;
                GameObject prop = targets[t].transform.parent.gameObject;
                if (!PrefabUtility.IsAnyPrefabInstanceRoot(prop)) continue;
                prefab = PrefabUtility.GetCorrespondingObjectFromOriginalSource(prop);
            }

            if (prefab == null)
            {
                missing.Add(item.Id);
                continue;
            }

            InspectionAnomalyPropsSO.Entry e;
            e.itemId = item.Id;
            e.prefab = prefab;
            list.Add(e);
        }

        InspectionAnomalyPropsSO so = AssetDatabase.LoadAssetAtPath<InspectionAnomalyPropsSO>(AssetPath);
        if (so == null)
        {
            so = ScriptableObject.CreateInstance<InspectionAnomalyPropsSO>();
            AssetDatabase.CreateAsset(so, AssetPath);
        }

        so.Entries = list.ToArray();
        List<InspectionAnomalyPropsSO.Entry> events = new List<InspectionAnomalyPropsSO.Entry>();
        for (int i = 0; i < EventPrefabs.GetLength(0); i++)
        {
            GameObject ev = AssetDatabase.LoadAssetAtPath<GameObject>(EventPrefabs[i, 1]);
            if (ev == null)
            {
                missing.Add(EventPrefabs[i, 0] + " 연출");
                continue;
            }

            InspectionAnomalyPropsSO.Entry e;
            e.itemId = EventPrefabs[i, 0];
            e.prefab = ev;
            events.Add(e);
        }

        so.Events = events.ToArray();
        EditorUtility.SetDirty(so);
        AssetDatabase.SaveAssets();

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        for (int i = 0; i < list.Count; i++) sb.Append(list[i].itemId).Append('=').Append(list[i].prefab.name).Append(' ');
        for (int i = 0; i < events.Count; i++) sb.Append(events[i].itemId).Append(" 연출=").Append(events[i].prefab.name).Append(' ');
        if (missing.Count > 0) sb.Append("· 프리팹 아님: ").Append(string.Join(", ", missing));
        return sb.ToString().Trim();
    }

    private static bool Has(JudgeTarget t, string id)
    {
        IReadOnlyList<string> ids = t.Ids;
        for (int i = 0; i < ids.Count; i++)
        {
            if (ids[i] != null && ids[i].Trim() == id) return true;
        }

        return false;
    }
}

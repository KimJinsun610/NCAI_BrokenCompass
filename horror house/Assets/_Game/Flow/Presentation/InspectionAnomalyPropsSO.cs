using System;
using UnityEngine;

/// <summary>
/// 점검 이상 [옮김]의 대역 소품 표(2026-10-04 41차). 정적 배칭(Batching Static)으로 묶인 씬 소품은 런타임에 옮겨도 그림이 따라오지 않으므로,
/// <see cref="InspectionAnomalies"/>는 원본을 숨기고 <b>같은 프리팹을 옮긴 자리에 하나 더 세운다</b>. 그 프리팹을 여기서 찾는다.
/// 에디터 메뉴 「야간근무/연출/점검 이상 소품 표 다시 만들기」(<c>InspectionAnomalyPropsBuilder</c>)가 열린 근무 씬의 점검 대상에서 채운다. 손으로 고치지 않는다.
/// </summary>
[CreateAssetMenu(fileName = "InspectionAnomalyProps", menuName = "NightDuty/Inspection Anomaly Props")]
public sealed class InspectionAnomalyPropsSO : ScriptableObject
{
    /// <summary>Resources 경로(확장자 없음).</summary>
    public const string ResourcePath = "InspectionAnomalyProps";

    /// <summary>항목 하나 — 점검 항목 ID와 그 대상 소품의 원본 프리팹.</summary>
    [Serializable]
    public struct Entry
    {
        public string itemId;
        public GameObject prefab;
    }

    [SerializeField] private Entry[] entries = new Entry[0];

    /// <summary>표 전체.</summary>
    public Entry[] Entries
    {
        get { return entries; }
        set { entries = value ?? new Entry[0]; }
    }

    /// <summary>그 항목의 소품 프리팹. 없으면 null.</summary>
    public GameObject Find(string itemId)
    {
        for (int i = 0; i < entries.Length; i++)
        {
            if (entries[i].itemId == itemId) return entries[i].prefab;
        }

        return null;
    }
}

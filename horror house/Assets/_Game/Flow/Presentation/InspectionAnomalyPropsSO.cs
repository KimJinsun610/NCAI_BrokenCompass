using System;
using UnityEngine;

/// <summary>
/// 점검 이상 [옮김]의 대역 소품 표(2026-10-04 41차). 정적 배칭(Batching Static)으로 묶인 씬 소품은 런타임에 옮겨도 그림이 따라오지 않으므로,
/// <see cref="InspectionAnomalies"/>는 원본을 숨기고 <b>같은 프리팹을 옮긴 자리에 하나 더 세운다</b>. 그 프리팹을 여기서 찾는다.
/// 에디터 메뉴 「야간근무/연출/점검 이상 소품 표 다시 만들기」(<c>InspectionAnomalyPropsBuilder</c>)가 열린 근무 씬의 점검 대상에서 채운다. 손으로 고치지 않는다.
/// 이상 연출 프리팹(<see cref="FindEvent"/>)도 같은 메뉴가 빌더의 고정 표에서 채운다.
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

    [Tooltip("항목별 이상 연출 프리팹(김진선님 HorrorEvent — 예: H-2 피 식수대). 있으면 절차 연출 대신 그 자리에 세운다.")]
    [SerializeField] private Entry[] events = new Entry[0];

    [Tooltip("66차 L-4 칠판 분필 글씨 글꼴(한글 손글씨). 빌더가 채운다. 없으면 TMP 기본 글꼴.")]
    [SerializeField] private TMPro.TMP_FontAsset chalkFont;

    /// <summary>66차: 칠판 분필 글씨 글꼴.</summary>
    public TMPro.TMP_FontAsset ChalkFont
    {
        get { return chalkFont; }
        set { chalkFont = value; }
    }

    /// <summary>표 전체.</summary>
    public Entry[] Entries
    {
        get { return entries; }
        set { entries = value ?? new Entry[0]; }
    }

    /// <summary>이상 연출 프리팹 표.</summary>
    public Entry[] Events
    {
        get { return events ?? new Entry[0]; }
        set { events = value ?? new Entry[0]; }
    }

    /// <summary>그 항목의 이상 연출 프리팹(44차 H-2 피 식수대). 없으면 null.</summary>
    public GameObject FindEvent(string itemId)
    {
        Entry[] list = Events;
        for (int i = 0; i < list.Length; i++)
        {
            if (list[i].itemId == itemId) return list[i].prefab;
        }

        return null;
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

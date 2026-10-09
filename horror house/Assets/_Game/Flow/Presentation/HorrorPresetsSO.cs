using System;
using UnityEngine;

/// <summary>
/// 66차: 김진선님 공포 연출 프리셋(HorrorEvent_*) 표 — 런타임에 이름으로 찾는다(<see cref="ProximityDoors"/>).
/// 에디터 메뉴 「야간근무/연출/진선님 연출 프리셋 표 다시 만들기」(<c>HorrorPresetsBuilder</c>)가 채운다. 손으로 고치지 않는다.
/// </summary>
[CreateAssetMenu(fileName = "HorrorPresets", menuName = "NightDuty/Horror Presets")]
public sealed class HorrorPresetsSO : ScriptableObject
{
    /// <summary>Resources 경로(확장자 없음).</summary>
    public const string ResourcePath = "HorrorPresets";

    /// <summary>프리셋 하나.</summary>
    [Serializable]
    public struct Entry
    {
        public string name;
        public GameObject prefab;
    }

    [SerializeField] private Entry[] entries = new Entry[0];

    /// <summary>표 전체.</summary>
    public Entry[] Entries
    {
        get { return entries ?? new Entry[0]; }
        set { entries = value ?? new Entry[0]; }
    }

    /// <summary>이름(HorrorEvent_DoorSlam 등)으로 찾는다. 없으면 null.</summary>
    public GameObject Find(string presetName)
    {
        Entry[] list = Entries;
        for (int i = 0; i < list.Length; i++)
        {
            if (list[i].name == presetName) return list[i].prefab;
        }

        return null;
    }
}

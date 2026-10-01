using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 연출 소리 표(2026-10-01). <see cref="DirectionStage"/>가 「<c>&lt;조우·수칙 ID&gt;.&lt;단계&gt;</c>」 이름으로 찾는다
/// (<c>E.BoySeated.foreshadow</c> · <c>E.SuitMan.confront</c> · <c>H2.cue</c> · <c>fake.knock</c> …).
/// <list type="bullet">
/// <item><c>Resources/Direction/&lt;이름&gt;</c> 클립이 있으면 그쪽이 먼저다(아트가 클립만 넣으면 바뀐다).</item>
/// <item>정확한 이름이 없으면 <c>*.&lt;단계&gt;</c>(모든 조우 공통)을 쓴다.</item>
/// <item>다른 폴더(김진선님 <c>99 Resources/04 Sound</c> 등)의 클립을 <b>복사하지 않고 참조만</b> 한다.</item>
/// </list>
/// 에셋은 <c>Resources/DirectionSounds.asset</c>, 채우는 것은 에디터 메뉴 「야간근무/연출/몹 대역·소리 연결」.
/// </summary>
[CreateAssetMenu(menuName = "NightDuty/Direction Sound Table", fileName = "DirectionSounds")]
public sealed class DirectionSoundTableSO : ScriptableObject
{
    /// <summary>Resources 경로.</summary>
    public const string ResourcePath = "DirectionSounds";

    [Serializable]
    public sealed class Entry
    {
        [Tooltip("이름(<조우·수칙 ID>.<단계>). *.<단계>는 공통.")]
        public string key = string.Empty;

        public AudioClip clip;

        [Range(0f, 1f)] public float volume = 1f;
    }

    [SerializeField] private List<Entry> entries = new List<Entry>();

    private Dictionary<string, Entry> _map;
    private static DirectionSoundTableSO s_loaded;
    private static bool s_looked;

    /// <summary>표 항목(에디터 빌더용).</summary>
    public List<Entry> Entries
    {
        get { return entries; }
    }

    /// <summary>그 이름의 클립. 없으면 <c>*.&lt;단계&gt;</c>, 그것도 없으면 null.</summary>
    public static AudioClip Find(string name, out float volume)
    {
        volume = 1f;
        DirectionSoundTableSO table = Table();
        if (table == null || string.IsNullOrEmpty(name)) return null;

        Entry e;
        if (table.Map().TryGetValue(name, out e) && e.clip != null)
        {
            volume = e.volume;
            return e.clip;
        }

        int dot = name.LastIndexOf('.');
        if (dot > 0 && table.Map().TryGetValue("*" + name.Substring(dot), out e) && e.clip != null)
        {
            volume = e.volume;
            return e.clip;
        }

        return null;
    }

    /// <summary>정확히 그 이름만(공통 대체 없음). 겹쳐 재생하는 둘째 소리(<c>&lt;이름&gt;+</c>)용.</summary>
    public static AudioClip FindExact(string name, out float volume)
    {
        volume = 1f;
        DirectionSoundTableSO table = Table();
        Entry e;
        if (table == null || string.IsNullOrEmpty(name) || !table.Map().TryGetValue(name, out e) || e.clip == null) return null;
        volume = e.volume;
        return e.clip;
    }

    private static DirectionSoundTableSO Table()
    {
        if (!s_looked)
        {
            s_looked = true;
            s_loaded = Resources.Load<DirectionSoundTableSO>(ResourcePath);
        }

        return s_loaded;
    }

    private Dictionary<string, Entry> Map()
    {
        if (_map != null) return _map;
        _map = new Dictionary<string, Entry>(StringComparer.Ordinal);
        foreach (Entry e in entries)
        {
            if (e != null && !string.IsNullOrEmpty(e.key)) _map[e.key] = e;
        }

        return _map;
    }

    private void OnValidate()
    {
        _map = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        s_loaded = null;
        s_looked = false;
    }
}

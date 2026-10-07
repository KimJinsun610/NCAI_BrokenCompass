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
/// 한 항목에 같은 소리의 여러 판(<c>_1~3</c>)을 넣으면 낼 때마다 무작위로 고른다(2026-10-04 사운드 전달본).
/// 에셋은 <c>Resources/DirectionSounds.asset</c>, 채우는 것은 <c>DirectionSoundTableBuilder</c>(에디터 메뉴 「야간근무/연출/몹 대역·소리 연결」 또는 「야간근무/연출/소리 표만 다시 만들기」).
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

        [Tooltip("같은 소리의 다른 판(_2·_3 …). 있으면 clip과 함께 무작위로 고른다.")]
        public AudioClip[] more = new AudioClip[0];

        [Range(0f, 1f)] public float volume = 1f;

        [Tooltip("판마다 가장 큰 지점(초). [0] = clip, [1..] = more. 빌더가 파형에서 잰다 — 충격음을 그 지점이 원하는 순간에 오도록 맞춰 튼다.")]
        public float[] hits = new float[0];

        /// <summary>clip과 more 중 하나(무작위). 다 비었으면 null.</summary>
        public AudioClip Pick()
        {
            float hit;
            return Pick(out hit);
        }

        /// <summary>clip과 more 중 하나(무작위)와 그 판의 가장 큰 지점(초, 모르면 0).</summary>
        public AudioClip Pick(out float hitAt)
        {
            hitAt = 0f;
            int count = (clip != null ? 1 : 0) + (more != null ? more.Length : 0);
            if (count == 0) return null;
            int i = UnityEngine.Random.Range(0, count);
            int slot = clip != null ? i : i + 1;
            AudioClip c = slot == 0 ? clip : more[slot - 1];
            if (c == null)
            {
                c = clip;
                slot = 0;
            }

            if (hits != null && slot < hits.Length) hitAt = hits[slot];
            return c;
        }
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
            return e.Pick();
        }

        int dot = name.LastIndexOf('.');
        if (dot > 0 && table.Map().TryGetValue("*" + name.Substring(dot), out e) && e.clip != null)
        {
            volume = e.volume;
            return e.Pick();
        }

        return null;
    }

    /// <summary>정확히 그 이름만(공통 대체 없음). 겹쳐 재생하는 둘째 소리(<c>&lt;이름&gt;+</c>)용.</summary>
    public static AudioClip FindExact(string name, out float volume)
    {
        float hit;
        return FindExact(name, out volume, out hit);
    }

    /// <summary>정확히 그 이름만 + 고른 판의 가장 큰 지점(초). 충격음을 박자에 맞출 때.</summary>
    public static AudioClip FindExact(string name, out float volume, out float hitAt)
    {
        volume = 1f;
        hitAt = 0f;
        DirectionSoundTableSO table = Table();
        Entry e;
        if (table == null || string.IsNullOrEmpty(name) || !table.Map().TryGetValue(name, out e) || e.clip == null) return null;
        volume = e.volume;
        return e.Pick(out hitAt);
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

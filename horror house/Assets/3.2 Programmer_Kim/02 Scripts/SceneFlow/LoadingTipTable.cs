using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// 로딩 화면에 표시할 안내 문구 · 팁 이미지 · 배경 이미지 목록.
/// <para>
/// <b>팁 문구와 팁 이미지는 CSV의 Id로 1:1 짝짓는다.</b> 문구는 CSV(<see cref="tipCsv"/>), 이미지는 이 에셋의
/// <see cref="tipImages"/>(Id → Sprite). 한 번 뽑은 줄(<see cref="PickTipEntry"/>)에서 문구와 이미지를 함께 꺼내므로
/// 둘이 어긋나지 않는다. 이미지 파일 이름을 Id와 같게(예: TIP_001.png) 지으면 인스펙터의 [자동 채우기]가 연결한다.
/// </para>
/// <para>
/// 문구는 두 갈래다. <see cref="tips"/>는 조작·규칙 안내라 언제 떠도 안전하고,
/// <see cref="unlockedTips"/>는 <b>현장에서 알게 되는 내용</b>이라 DAY 1에 뜨면 스포일러가 된다.
/// 시나리오 기획서 v6 검증항목 9: 「로딩 문구가 해금 전 내용을 누설하지 않는지 확인한다」.
/// </para>
/// <para>
/// 해금 기준은 <b>DAY 2 이상</b>이다. DAY 1의 로딩은 계약 화면에서 Play 씬으로 들어가는 길목이라
/// 아직 태블릿을 줍기 전이고, DAY 2부터의 로딩은 결과창을 거친 뒤라 이미 주운 뒤다.
/// 태블릿 습득 상태를 따로 들고 있게 되면 그 값으로 바꾸는 것이 더 정확하다.
/// </para>
/// </summary>
[CreateAssetMenu(fileName = "LoadingTipTable", menuName = "Programmer_Kim/Loading Tip Table")]
public class LoadingTipTable : ScriptableObject
{
    [Header("CSV (우선)")]
    [Tooltip("Id,UnlockDay,Text 형식의 CSV. 넣으면 아래 두 배열 대신 이것을 쓴다. 형식은 LoadingTipCsv 참고.")]
    public TextAsset tipCsv;

    [Header("CSV가 없을 때 쓰는 문구")]
    [Tooltip("언제 떠도 안전한 조작·규칙 안내. DAY 1 로딩에는 이것만 나온다.")]
    [TextArea(2, 4)] public string[] tips;

    [Tooltip("현장에서 알게 되는 내용. DAY 2부터만 나온다 — DAY 1에 뜨면 스포일러다.")]
    [TextArea(2, 4)] public string[] unlockedTips;

    [Tooltip("비워 두면 로딩 씬에 배치된 기본 배경을 그대로 쓴다.")]
    public Sprite[] backgrounds;

    /// <summary>팁 하나의 이미지(Id는 CSV의 Id와 같아야 한다).</summary>
    [Serializable]
    public class TipImage
    {
        public string id;
        public Sprite sprite;
    }

    [Header("팁 이미지 (CSV의 Id와 1:1)")]
    [Tooltip("[자동 채우기]가 이 폴더에서 Id와 이름이 같은 이미지를 찾는다.")]
    public string tipImageFolder = "Assets/0. Main/06 Data/LoadingTipImages";

    [Tooltip("Id마다 보여 줄 이미지. 비어 있는 팁은 이미지 칸을 숨기고 문구만 보여 준다.")]
    public List<TipImage> tipImages = new List<TipImage>();

    /// <summary>한 번에 뽑은 팁 한 줄 — 문구와 이미지가 늘 같은 줄에서 나온다.</summary>
    public readonly struct Tip
    {
        public readonly string Id;
        public readonly string Text;
        /// <summary>없으면 null.</summary>
        public readonly Sprite Image;

        public Tip(string id, string text, Sprite image)
        {
            Id = id;
            Text = text;
            Image = image;
        }
    }

    // 연속으로 같은 항목이 나오지 않도록 직전 인덱스를 기억한다 (씬이 바뀌어도 유지)
    private static int lastTipIndex = -1;
    private static int lastBackgroundIndex = -1;

    // 합친 목록을 매번 새로 만들지 않는다. 로딩은 프레임이 이미 빡빡한 구간이다.
    private string[] merged;

    // CSV는 처음 고를 때 한 번만 읽는다
    private List<LoadingTipRow> csvRows;
    private readonly List<LoadingTipRow> csvPool = new List<LoadingTipRow>();
    private Dictionary<string, Sprite> imageById;

    /// <summary>
    /// 지금 띄울 팁 한 줄(문구 + 같은 Id의 이미지). CSV가 있으면 CSV에서(UnlockDay ≤ 현재 DAY),
    /// 없으면 해금 전에는 <see cref="tips"/>에서만, 해금 뒤에는 두 목록을 합쳐서 고른다(이 경우 Id·이미지 없음).
    /// </summary>
    public Tip PickTipEntry()
    {
        if (tipCsv != null) return PickFromCsv();
        return new Tip(string.Empty, Pick(IsUnlocked ? Merged() : tips, ref lastTipIndex), null);
    }

    /// <summary>문구만 필요할 때.</summary>
    public string PickTip() => PickTipEntry().Text;

    public Sprite PickBackground() => Pick(backgrounds, ref lastBackgroundIndex);

    /// <summary>CSV의 모든 줄(에디터 검사 · 자동 채우기용).</summary>
    public IReadOnlyList<LoadingTipRow> CsvRows
    {
        get
        {
            if (tipCsv == null) return Array.Empty<LoadingTipRow>();
            if (csvRows == null) csvRows = LoadingTipCsv.Parse(tipCsv.text, tipCsv.name);
            return csvRows;
        }
    }

    /// <summary>그 Id의 팁 이미지(없으면 null). Id는 대소문자를 가리지 않는다.</summary>
    public Sprite ImageFor(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        if (imageById == null)
        {
            imageById = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
            foreach (TipImage t in tipImages)
            {
                if (t == null || string.IsNullOrWhiteSpace(t.id) || t.sprite == null) continue;
                imageById[t.id.Trim()] = t.sprite;
            }
        }
        return imageById.TryGetValue(id, out Sprite s) ? s : null;
    }

    private Tip PickFromCsv()
    {
        int day = Mathf.Max(1, GameSession.CurrentDay);
        csvPool.Clear();
        foreach (LoadingTipRow row in CsvRows)
        {
            if (row.UnlockDay <= day) csvPool.Add(row);
        }

        if (csvPool.Count == 0)
        {
            Debug.LogWarning($"[LoadingTipTable] {tipCsv.name}에 DAY {day}에 나올 문구가 없습니다.");
            return new Tip(string.Empty, string.Empty, null);
        }

        LoadingTipRow picked = Pick(csvPool.ToArray(), ref lastTipIndex);
        return new Tip(picked.Id, picked.Text, ImageFor(picked.Id));
    }

    // 인스펙터에서 CSV·배열·이미지를 바꾸면 다시 읽게 한다
    private void OnValidate()
    {
        csvRows = null;
        merged = null;
        imageById = null;
    }

    /// <summary>현장 내용을 띄워도 되는 시점인지.</summary>
    private static bool IsUnlocked => GameSession.CurrentDay >= 2;

    private string[] Merged()
    {
        if (unlockedTips == null || unlockedTips.Length == 0) return tips;
        if (tips == null || tips.Length == 0) return unlockedTips;

        if (merged == null || merged.Length != tips.Length + unlockedTips.Length)
        {
            merged = new string[tips.Length + unlockedTips.Length];
            tips.CopyTo(merged, 0);
            unlockedTips.CopyTo(merged, tips.Length);
        }

        return merged;
    }

    private static T Pick<T>(T[] items, ref int lastIndex)
    {
        if (items == null || items.Length == 0) return default;

        int index = Random.Range(0, items.Length);
        if (items.Length > 1 && index == lastIndex)
        {
            // 직전 항목을 제외한 나머지 중에서 다시 고른다
            index = (lastIndex + 1 + Random.Range(0, items.Length - 1)) % items.Length;
        }

        lastIndex = index;
        return items[index];
    }
}

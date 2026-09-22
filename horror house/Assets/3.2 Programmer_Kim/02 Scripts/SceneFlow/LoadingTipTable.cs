using UnityEngine;

/// <summary>
/// 로딩 화면에 표시할 안내 문구와 배경 이미지 목록.
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
    [Tooltip("언제 떠도 안전한 조작·규칙 안내. DAY 1 로딩에는 이것만 나온다.")]
    [TextArea(2, 4)] public string[] tips;

    [Tooltip("현장에서 알게 되는 내용. DAY 2부터만 나온다 — DAY 1에 뜨면 스포일러다.")]
    [TextArea(2, 4)] public string[] unlockedTips;

    [Tooltip("비워 두면 로딩 씬에 배치된 기본 배경을 그대로 쓴다.")]
    public Sprite[] backgrounds;

    // 연속으로 같은 항목이 나오지 않도록 직전 인덱스를 기억한다 (씬이 바뀌어도 유지)
    private static int lastTipIndex = -1;
    private static int lastBackgroundIndex = -1;

    // 합친 목록을 매번 새로 만들지 않는다. 로딩은 프레임이 이미 빡빡한 구간이다.
    private string[] merged;

    /// <summary>
    /// 지금 띄울 문구 하나. 해금 전에는 <see cref="tips"/>에서만, 해금 뒤에는 두 목록을 합쳐서 고른다.
    /// </summary>
    public string PickTip() => Pick(IsUnlocked ? Merged() : tips, ref lastTipIndex);

    public Sprite PickBackground() => Pick(backgrounds, ref lastBackgroundIndex);

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

using UnityEngine;

/// <summary>
/// 로딩 화면에 표시할 안내 문구와 배경 이미지 목록.
/// 문구에는 조작 안내만 넣는다 — 규칙 해석이나 수치 힌트는 게임 설계상 넣지 않는다.
/// </summary>
[CreateAssetMenu(fileName = "LoadingTipTable", menuName = "Programmer_Kim/Loading Tip Table")]
public class LoadingTipTable : ScriptableObject
{
    [TextArea(2, 4)] public string[] tips;
    [Tooltip("비워 두면 로딩 씬에 배치된 기본 배경을 그대로 쓴다.")]
    public Sprite[] backgrounds;

    // 연속으로 같은 항목이 나오지 않도록 직전 인덱스를 기억한다 (씬이 바뀌어도 유지)
    private static int lastTipIndex = -1;
    private static int lastBackgroundIndex = -1;

    public string PickTip() => Pick(tips, ref lastTipIndex);

    public Sprite PickBackground() => Pick(backgrounds, ref lastBackgroundIndex);

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

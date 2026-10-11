using UnityEngine;

/// <summary>
/// 오프닝이 끝났을 때 계약서로 넘기는 <b>하나뿐인 출구</b>.
///
/// <para>오프닝 씬은 무엇으로 만들어도 된다(슬라이드 · 타임라인 · 전화 연출 · 영상).
/// 어떤 방식이든 끝에서 이 출구만 부르면 흐름에 들어온다 —
/// 씬 이름을 코드에 적지 않으므로 오프닝을 통째로 갈아 끼워도 고칠 곳이 없다.</para>
///
/// 부르는 법 세 가지:
/// <list type="bullet">
/// <item>코드에서 <c>OpeningExit.ToContract()</c></item>
/// <item>버튼 OnClick()·Timeline Signal에서 이 컴포넌트의 <c>Finish()</c></item>
/// <item><c>finishAfterSeconds</c>를 양수로 두면 그 시간 뒤 저절로(연출 길이가 고정일 때)</item>
/// </list>
/// </summary>
public class OpeningExit : MonoBehaviour
{
    [Tooltip("0보다 크면 씬이 열린 뒤 이 시간(초)에 저절로 계약서로 넘어간다. 0이면 Finish()를 부를 때까지 기다린다.")]
    [Min(0f)] public float finishAfterSeconds;

    private static bool leaving;   // 두 번 불려도 한 번만 넘어간다

    private void OnEnable()
    {
        leaving = false;
        if (finishAfterSeconds > 0f) Invoke(nameof(Finish), finishAfterSeconds);
    }

    /// <summary>버튼·Timeline Signal에서 부를 수 있는 입구.</summary>
    public void Finish()
    {
        ToContract();
    }

    /// <summary>오프닝을 끝내고 계약서로 간다. 여러 번 불러도 한 번만 넘어간다.</summary>
    public static void ToContract()
    {
        if (leaving) return;
        leaving = true;

        // 오프닝은 커서를 숨겨 두므로 계약서가 다시 켠다(ContractController.Start).
        SceneFlow.GoTo(GameScene.Contract, false);
    }
}

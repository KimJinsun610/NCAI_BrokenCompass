using DG.Tweening;
using UnityEngine;

/// <summary>
/// 일시정지 메뉴(HUD_Pause_Design)의 화면 쪽 동작. 버튼의 실제 기능(계속하기·종료)은 <see cref="HUDActions"/>가 맡고,
/// 이 컴포넌트는 메뉴가 열릴 때의 연출과 밝기 패널 여닫기만 한다.
/// <para>
/// <b>연출은 메뉴를 열 때마다 처음부터 다시 재생한다.</b> 일시정지 중에는 <c>Time.timeScale</c>이 0이므로
/// 자식의 DOTweenAnimation은 <b>Independent Update</b>가 켜져 있어야 움직인다. 켜 두지 않으면 시작 자세에서 멈춘다
/// (투명에서 나타나는 버튼은 안 보이는 채로 굳는다).
/// 같은 이유로 Auto Kill은 끄고(다시 재생해야 하므로) Auto Play도 끈다(게임 시작 때 숨은 채로 한 번 재생되는 것을 막는다).
/// </para>
/// </summary>
[DisallowMultipleComponent]
public class PauseMenu : MonoBehaviour
{
    [Tooltip("LIGHT 버튼으로 여닫는 밝기 패널. 메뉴를 열 때마다 접힌 상태로 시작한다.")]
    [SerializeField] private GameObject brightnessPanel;

    private DOTweenAnimation[] tweens;

    private void Awake()
    {
        tweens = GetComponentsInChildren<DOTweenAnimation>(true);
    }

    private void OnEnable()
    {
        if (brightnessPanel != null)
        {
            brightnessPanel.SetActive(false);
        }

        if (tweens == null)
        {
            return;
        }

        // 처음 생성될 때는 자식 DOTweenAnimation의 Awake가 아직 돌지 않아 tween이 없다. 그때는 건너뛴다.
        // (FPController가 생성 직후 바로 숨기므로 보이지 않는다.) 그 뒤로 열 때마다 처음부터 재생한다.
        for (int i = 0; i < tweens.Length; i++)
        {
            if (tweens[i] != null && tweens[i].tween != null)
            {
                tweens[i].DORestart();
            }
        }
    }

    /// <summary>LIGHT 버튼의 OnClick에서 부른다. 밝기 패널을 펼치거나 접는다.</summary>
    public void ToggleBrightness()
    {
        if (brightnessPanel != null)
        {
            brightnessPanel.SetActive(!brightnessPanel.activeSelf);
        }
    }
}

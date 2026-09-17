using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 계약서 씬: 서명 칸에 서명해야 출근하기 버튼이 켜지고, 누르면 로딩을 거쳐 Play 씬(Day 1)으로 간다.
/// 메인의 시작 버튼(HUDActions.StartGame)이 회차를 초기화한 뒤 이 씬으로 보낸다.
/// </summary>
public class ContractController : MonoBehaviour
{
    [SerializeField] private SignaturePad signaturePad;
    [Tooltip("서명 전에만 보이는 안내 (예: \"여기에 서명\"). 비워 둬도 된다.")]
    [SerializeField] private GameObject signHint;
    [SerializeField] private Button startButton;

    private void OnEnable()
    {
        if (signaturePad != null) signaturePad.InkChanged += Refresh;
        if (startButton != null) startButton.onClick.AddListener(OnStartClicked);
    }

    private void OnDisable()
    {
        if (signaturePad != null) signaturePad.InkChanged -= Refresh;
        if (startButton != null) startButton.onClick.RemoveListener(OnStartClicked);
    }

    private void Start()
    {
        // 로딩 씬과 Day 인트로가 커서를 숨기므로 여기서 다시 보이게 한다
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        Refresh();
    }

    public void OnStartClicked()
    {
        if (signaturePad != null && !signaturePad.HasInk) return;
        SceneFlow.GoTo(GameScene.Play);
    }

    private void Refresh()
    {
        bool signed = signaturePad == null || signaturePad.HasInk;
        if (startButton != null) startButton.interactable = signed;
        if (signHint != null) signHint.SetActive(!signed);
    }
}

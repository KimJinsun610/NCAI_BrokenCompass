using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class FPController : MonoBehaviour
{
    [Header("MOUSE LOOK")]
    public Vector2 mouseSensitivity = new Vector2(80, 80);
    public Vector2 verticalLookLimit = new Vector2(-85, 85);

    private float xRot;
    private Camera cam;

    [Header("MOVEMENT")]
    public float walkSpeed = 1;
    public float runSpeed = 3;
    [Tooltip("태블릿을 Tab으로 확대해 읽는 동안의 이동 속도(Shift를 눌러도 이 속도). 플레이 중에 바꾸면 바로 반영된다.")]
    public float tabletZoomSpeed = 0.5f;
    private float speed = 1;

    [Header("CONTROLS")]
    public KeyCode forward = KeyCode.W;
    public KeyCode backward = KeyCode.S;
    public KeyCode strafeLeft = KeyCode.A;
    public KeyCode strafeRight = KeyCode.D;
    public KeyCode run = KeyCode.LeftShift;

    [Header("SIGHT")]
    public bool sight = true;
    public GameObject sightPrefab;

    [Header("DOCUMENT UI")]
    public GameObject documentUI;
    public KeyCode toggleDocumentUI = KeyCode.Tab;

    private GameObject documentUIInstance;

    [Header("PAUSE UI")]
    public GameObject pauseUI;
    public KeyCode togglePause = KeyCode.Escape;

    private GameObject pauseUIInstance;

    public bool hideCursor = false;

    private void OnEnable()
    {
        if (hideCursor)
        {
            Cursor.visible = false;
        }
        else
        {
            Cursor.visible = true;
        }
    }

    private void OnDisable()
    {
        Cursor.visible = true;
    }

    void Start()
    {
        cam = GetComponentInChildren<Camera>();

        if (hideCursor)
        {
            Cursor.visible = false;
        }
        else
        {
            Cursor.visible = true;
        }

        if (sight)
        {
            GameObject sightObj = Instantiate(sightPrefab);
            sightObj.transform.SetParent(transform.parent);
        }

        if (documentUI != null)
        {
            documentUIInstance = Instantiate(documentUI);
            documentUIInstance.SetActive(false);
        }

        if (pauseUI != null)
        {
            pauseUIInstance = Instantiate(pauseUI);
            pauseUIInstance.SetActive(false);

            HUDActions hudActions = pauseUIInstance.GetComponent<HUDActions>();
            if (hudActions != null)
            {
                hudActions.OnResumeClicked += ResumeGame;
            }
        }
    }

    void Update()
    {
        // 일시정지 토글이 먼저다. 멈춘 상태에서도 Esc로 풀 수 있어야 한다.
        TogglePause();

        // 일시정지 중에는 일시정지 메뉴 말고는 아무것도 받지 않는다.
        // (시점·이동은 Time.deltaTime이 0이라 저절로 멈추지만, 키 입력을 받는 Tab은 그렇지 않다)
        if (GamePause.IsPaused) return;

        CameraLook();

        PlayerMove();

        ToggleDocumentUI();
    }

    void ToggleDocumentUI()
    {
        if (documentUIInstance == null) return;

        if (Input.GetKeyDown(toggleDocumentUI))
        {
            documentUIInstance.SetActive(!documentUIInstance.activeSelf);

            Debug.Log("Tab ui출력");
            Cursor.visible = false;
        }
    }

    void TogglePause()
    {
        if (pauseUIInstance == null) return;

        if (Input.GetKeyDown(togglePause))
        {
            if (pauseUIInstance.activeSelf) ResumeGame();
            else PauseGame();
        }
    }

    public void PauseGame()
    {
        pauseUIInstance.SetActive(true);
        // 시간과 소리를 함께 멈춘다. timeScale만 0으로 두면 이미 재생 중인 소리는 계속 울린다.
        GamePause.Set(true);
        Cursor.visible = true;
    }

    public void ResumeGame()
    {
        pauseUIInstance.SetActive(false);
        GamePause.Set(false);
        Cursor.visible = false;
    }

    void CameraLook()
    {
        float mouseX = Input.GetAxis("Mouse X") * Time.deltaTime * mouseSensitivity.x * 10;
        float mouseY = Input.GetAxis("Mouse Y") * Time.deltaTime * mouseSensitivity.y * 10;

        xRot -= mouseY;
        xRot = Mathf.Clamp(xRot, verticalLookLimit.x, verticalLookLimit.y);
        cam.transform.localEulerAngles = new Vector3(xRot, 0, 0);

        transform.Rotate(Vector3.up * mouseX);
    }

    void PlayerMove()
    {
        if (Input.GetKey(run))
        {
            speed = runSpeed;
        }
        else
        {
            speed = walkSpeed;
        }

        // 태블릿 확대 중에는 확대 진행도(0~1)만큼 tabletZoomSpeed로 바뀐다
        float zoom = TabletZoom.Active != null ? TabletZoom.Active.ZoomAmount : 0f;
        speed = Mathf.Lerp(speed, tabletZoomSpeed, zoom);

        if (Input.GetKey(forward))
        {
            transform.Translate(Vector3.forward * speed * Time.deltaTime, Space.Self);
        }

        if (Input.GetKey(backward))
        {
            transform.Translate(Vector3.forward * -speed * Time.deltaTime, Space.Self);
        }

        if (Input.GetKey(strafeLeft))
        {
            transform.Translate(Vector3.right * -speed * Time.deltaTime, Space.Self);
        }

        if (Input.GetKey(strafeRight))
        {
            transform.Translate(Vector3.right * speed * Time.deltaTime, Space.Self);
        }
    }
}

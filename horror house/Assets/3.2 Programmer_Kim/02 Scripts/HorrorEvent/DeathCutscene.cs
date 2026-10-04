using System;
using UnityEngine;
using UnityEngine.Playables;

/// <summary>
/// 붙잡힘(사망) 컷신. <b>플레이어가 서 있는 자리·바라보는 방향</b>을 기준으로 Timeline을 재생한다.
///
/// <para><b>흐름</b> — <see cref="Play"/>
/// ① 컷신 루트를 플레이어 발밑으로 옮기고 플레이어의 좌우 방향(yaw)에 맞춘다
/// ② 플레이어 조작을 막는다(FPController · 태블릿 손 숨김)
/// ③ 모든 소리를 끊는다(<c>AudioListener.pause</c>) — 컷신 자신의 소리(<see cref="ownAudio"/>)만 예외
/// ④ Timeline 재생. 플레이어 카메라는 매 프레임 <see cref="camTarget"/>(Timeline이 움직임)을 따라간다
/// ⑤ 끝나면 <see cref="Finished"/> — 디버그 재생이면 모든 것을 원래대로 돌린다</para>
///
/// <para><b>카메라를 따로 만들지 않는다.</b> 플레이어 카메라를 그대로 움직여야 후처리 볼륨(Volume Mask)·손전등·
/// 오디오 리스너가 평소와 같다. 카메라 목표는 <see cref="camRig"/>(눈높이) 아래에 있고, 시작할 때 현재 시선에서
/// <see cref="blendInSeconds"/> 동안 섞어 들어가 화면이 튀지 않는다.</para>
///
/// <para>프리팹·Timeline은 메뉴 「Tools ▸ Programmer_Kim ▸ Horror ▸ Build Death Cutscene (Auditory)」가 만든다.
/// 키는 Timeline 창에서 직접 고친다. 씬에 놓지 않아도 디버그 키로 <c>Resources</c>에서 불러 재생한다.</para>
/// </summary>
[DisallowMultipleComponent]
public class DeathCutscene : MonoBehaviour
{
    /// <summary>씬에 없을 때 디버그 키로 불러올 프리팹 이름(어느 Resources 폴더든).</summary>
    public const string ResourceName = "DeathCutscene_Auditory";

    [Header("연결 (빌더가 채움)")]
    [SerializeField] private PlayableDirector director;
    [Tooltip("눈높이로 올라가는 카메라 받침. 플레이 시작 때 플레이어 눈높이로 맞춘다.")]
    [SerializeField] private Transform camRig;
    [Tooltip("Timeline이 움직이는 카메라 목표. 플레이어 카메라가 이 위치·회전을 따라간다.")]
    [SerializeField] private Transform camTarget;
    [Tooltip("모든 소리가 끊긴 동안에도 들려야 하는 컷신 자신의 소리(속삭임 등).")]
    [SerializeField] private AudioSource[] ownAudio = new AudioSource[0];
    [Tooltip("Timeline이 세기를 움직이는 화면 효과 볼륨. 평소·복구 때 세기를 0으로 둔다(화면 효과가 남지 않게).")]
    [SerializeField] private UnityEngine.Rendering.Volume[] volumes = new UnityEngine.Rendering.Volume[0];

    [Header("시선 보정 — 씬마다 플레이어 눈높이가 다르다")]
    [Tooltip("돌아본 끝에 봐야 하는 지점(소년의 Head 뼈). 비우면 보정하지 않는다.")]
    [SerializeField] private Transform aimPoint;
    [Tooltip("aimPoint에서 위로 이만큼(m) 올린 곳을 본다. Head 뼈는 목·머리 이음매(턱 높이)라 그대로 보면 입이 화면 가운데에 온다.")]
    [SerializeField] private float aimHeightOffset = 0.09f;
    [Tooltip("Timeline에서 돌아본 끝의 좌우 각도(°). 이만큼 돌았을 때 보정이 전부 걸린다.")]
    [SerializeField] private float designedYaw = 146f;
    [Tooltip("Timeline에서 돌아본 끝의 위아래 각도(°, +가 내려다봄). 눈높이 1.7m 기준으로 잡은 값.")]
    [SerializeField] private float designedPitch = 17f;

    [Header("연출")]
    [Tooltip("현재 시선에서 Timeline 카메라로 섞여 들어가는 시간(초). 0이면 바로 붙는다.")]
    [SerializeField, Min(0f)] private float blendInSeconds = 0.35f;
    [Tooltip("켜면 시작 순간 모든 소리를 끊는다(AudioListener.pause).")]
    [SerializeField] private bool cutAllSound = true;
    [Tooltip("켜면 컷신 동안 손·태블릿을 숨긴다.")]
    [SerializeField] private bool hideViewmodel = true;

    [Header("디버그")]
    [Tooltip("이 키로 컷신을 재생한다(에디터 · 개발 빌드만). 디버그 재생은 끝나면 원래대로 돌아간다.")]
    [SerializeField] private KeyCode debugKey = KeyCode.F8;

    /// <summary>컷신이 끝까지 재생됐을 때. 사망 화면으로 넘기는 쪽이 듣는다.</summary>
    public event Action Finished;

    /// <summary>지금 재생 중인 컷신(없으면 null).</summary>
    public static DeathCutscene Playing { get; private set; }

    public bool IsPlaying { get { return Playing == this; } }

    // 복구용
    private Transform player;
    private Camera playerCam;
    private Behaviour playerController;
    private GameObject viewmodel;
    private bool viewmodelWasActive;
    private Vector3 camLocalPos;
    private Quaternion camLocalRot;
    private Vector3 playerPos;
    private Quaternion playerRot;
    private bool restoreOnEnd;
    private bool pausedAudio;

    // 섞어 들어가기
    private Vector3 blendFromPos;
    private Quaternion blendFromRot;
    private float elapsed;
    private int startedFrame = -1;
    private float pitchCorrection;   // 실제 눈→얼굴 각도 − designedPitch
    private CursorLockMode cursorWasLock;

    private static void HideCursor()
    {
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatic()
    {
        Playing = null;
    }

    private void Awake()
    {
        if (director == null) director = GetComponent<PlayableDirector>();
        if (director != null)
        {
            director.playOnAwake = false;
            director.stopped += OnDirectorStopped;
        }
        foreach (AudioSource a in ownAudio)
        {
            if (a == null) continue;
            a.playOnAwake = false;
            a.ignoreListenerPause = true;   // AudioListener.pause로 다른 소리를 끊어도 이건 들린다
        }
        ResetVolumes();
    }

    private void ResetVolumes()
    {
        foreach (UnityEngine.Rendering.Volume v in volumes)
        {
            if (v != null) v.weight = 0f;
        }
    }

    private void OnDestroy()
    {
        if (director != null) director.stopped -= OnDirectorStopped;
        if (IsPlaying) EndCutscene(true);
    }

    // ─────────────────────────────── 재생 ───────────────────────────────

    /// <summary>
    /// 플레이어 위치를 기준으로 재생한다. <paramref name="restoreAfter"/>가 참이면 끝난 뒤 조작·카메라·소리를
    /// 원래대로 돌린다(디버그). 사망 컷신으로 쓸 때는 거짓 — 끝난 자리를 사망 화면이 이어받는다.
    /// </summary>
    public bool Play(bool restoreAfter)
    {
        if (Playing != null || director == null || camTarget == null) return false;

        FPController fp = FindAnyObjectByType<FPController>();
        if (fp == null)
        {
            Debug.LogWarning("[DeathCutscene] 플레이어(FPController)가 없어 재생하지 않습니다.");
            return false;
        }

        player = fp.transform;
        playerCam = fp.GetComponentInChildren<Camera>();
        if (playerCam == null) return false;

        playerController = fp;
        restoreOnEnd = restoreAfter;

        // 복구용 기록
        playerPos = player.position;
        playerRot = player.rotation;
        camLocalPos = playerCam.transform.localPosition;
        camLocalRot = playerCam.transform.localRotation;

        PlaceAtPlayer(fp);
        pitchCorrection = ComputePitchCorrection();

        // 조작 잠금 — FPController를 끄면 시점·이동·Esc(일시정지)가 막힌다.
        // 손전등(F)은 FlashlightRelay가 「플레이어 조작이 꺼져 있으면 받지 않음」으로 막는다.
        playerController.enabled = false;
        cursorWasLock = Cursor.lockState;
        HideCursor();   // FPController.OnDisable이 커서를 다시 보이게 하므로 그 뒤에 숨긴다
        if (hideViewmodel)
        {
            PlayerTablet tablet = playerCam.GetComponentInChildren<PlayerTablet>(true);
            viewmodel = tablet != null ? tablet.gameObject : null;
            if (viewmodel != null)
            {
                viewmodelWasActive = viewmodel.activeSelf;
                viewmodel.SetActive(false);
            }
        }

        // 소리 끊기 — 이미 일시정지 중이었으면(그럴 일은 거의 없지만) 되돌릴 때 건드리지 않는다
        if (cutAllSound && !AudioListener.pause)
        {
            AudioListener.pause = true;
            pausedAudio = true;
        }

        blendFromPos = playerCam.transform.position;
        blendFromRot = playerCam.transform.rotation;
        elapsed = 0f;

        Playing = this;
        startedFrame = Time.frameCount;
        gameObject.SetActive(true);
        director.time = 0;
        director.Play();
        return true;
    }

    /// <summary>
    /// 컷신 루트를 플레이어 발밑에 두고 좌우 방향만 맞춘다(위아래 시선은 섞어 들어가기로 처리).
    /// 카메라 받침은 플레이어 눈높이로 올린다.
    /// </summary>
    private void PlaceAtPlayer(FPController fp)
    {
        Vector3 feet = FindFeet(fp);

        Vector3 forward = Vector3.ProjectOnPlane(playerCam.transform.forward, Vector3.up);
        if (forward.sqrMagnitude < 0.0001f) forward = fp.transform.forward;

        transform.SetPositionAndRotation(feet, Quaternion.LookRotation(forward.normalized, Vector3.up));

        if (camRig != null)
        {
            Vector3 local = camRig.localPosition;
            local.y = playerCam.transform.position.y - feet.y;
            camRig.localPosition = local;
        }
    }

    /// <summary>
    /// 소년이 설 바닥. 눈 아래로 레이를 쏴 실제 바닥을 쓰고(플레이어 자신의 캡슐은 레이 시작점이 안에 있어 맞지 않는다),
    /// 못 찾으면 캡슐 바닥으로 계산한다. 씬마다 캡슐 높이·카메라 높이가 달라 캡슐 계산만으로는 어긋났다.
    /// </summary>
    private Vector3 FindFeet(FPController fp)
    {
        Vector3 eye = playerCam.transform.position;
        RaycastHit hit;
        if (Physics.Raycast(eye, Vector3.down, out hit, 4f, ~0, QueryTriggerInteraction.Ignore))
        {
            return new Vector3(fp.transform.position.x, hit.point.y, fp.transform.position.z);
        }

        Vector3 feet = fp.transform.position;
        CapsuleCollider capsule = fp.GetComponent<CapsuleCollider>();
        if (capsule != null)
        {
            float half = capsule.height * 0.5f * fp.transform.lossyScale.y;
            feet = fp.transform.TransformPoint(capsule.center) - Vector3.up * half;
        }
        return feet;
    }

    /// <summary>
    /// Timeline의 위아래 각도는 눈높이 1.7m 기준이다. 실제 눈 위치에서 얼굴까지의 각도와의 차이를 구해 둔다.
    /// (testScene 눈높이 1.70m · PlayScene_test 1.35m — 같은 17°로 내려다보면 뒤쪽은 허리가 보였다)
    /// </summary>
    private float ComputePitchCorrection()
    {
        if (aimPoint == null || camRig == null) return 0f;

        Vector3 eye = camRig.position;
        Vector3 to = aimPoint.position + Vector3.up * aimHeightOffset - eye;
        float horizontal = new Vector2(to.x, to.z).magnitude;
        if (horizontal < 0.05f) return 0f;

        float required = Mathf.Atan2(-to.y, horizontal) * Mathf.Rad2Deg;   // +면 내려다봄(Transform X 회전과 같은 부호)
        return required - designedPitch;
    }

    private void LateUpdate()
    {
        if (!IsPlaying || playerCam == null) return;

        // 다른 UI(개발자 모드 등)가 컷신 도중 커서를 켜도 다시 숨긴다.
        if (Cursor.visible) HideCursor();

        // 디렉터가 다 그린 뒤(LateUpdate) 카메라를 목표에 붙인다.
        elapsed += Time.deltaTime;
        float w = blendInSeconds <= 0f ? 1f : Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / blendInSeconds));

        // 시선 보정은 돌아본 만큼만 건다 — 정면을 보는 동안은 0, 소년을 마주했을 때 전부.
        Quaternion target = camTarget.rotation;
        if (pitchCorrection != 0f && designedYaw != 0f)
        {
            float turned = Mathf.Clamp01(Mathf.DeltaAngle(0f, camTarget.localEulerAngles.y) / designedYaw);
            target *= Quaternion.Euler(pitchCorrection * turned, 0f, 0f);
        }

        playerCam.transform.SetPositionAndRotation(
            Vector3.Lerp(blendFromPos, camTarget.position, w),
            Quaternion.Slerp(blendFromRot, target, w));
    }

    private void OnDirectorStopped(PlayableDirector d)
    {
        if (!IsPlaying) return;
        EndCutscene(restoreOnEnd);
        if (Finished != null) Finished();
    }

    /// <summary>재생 중이면 멈추고 원래대로 돌린다(디버그용).</summary>
    public void StopAndRestore()
    {
        if (!IsPlaying) return;
        restoreOnEnd = true;
        director.Stop();   // stopped 이벤트가 EndCutscene을 부른다
    }

    private void EndCutscene(bool restore)
    {
        Playing = null;
        if (!restore) return;   // 사망 컷신: 마지막 장면을 사망 화면이 이어받는다

        if (playerCam != null)
        {
            playerCam.transform.localPosition = camLocalPos;
            playerCam.transform.localRotation = camLocalRot;
        }
        if (player != null) player.SetPositionAndRotation(playerPos, playerRot);
        if (playerController != null) playerController.enabled = true;
        // 게임으로 돌아가면 커서는 항상 숨긴다. FPController.OnEnable은 hideCursor가 꺼진 씬에서 커서를 켜고,
        // 컷신 전 상태로 되돌리면 그때 보이던 커서가 그대로 남았다(2026-10-02 사용자 지적).
        // 잠금 상태만 컷신 전으로 돌린다 — 평소 게임은 커서를 잠그지 않는다.
        Cursor.visible = false;
        Cursor.lockState = cursorWasLock;
        ResetVolumes();
        if (viewmodel != null) viewmodel.SetActive(viewmodelWasActive);
        if (pausedAudio && !GamePause.IsPaused) AudioListener.pause = false;
        pausedAudio = false;

        // 디렉터를 처음으로 되돌려 다음 재생 전까지 소년이 보이지 않게 한다(Activation 트랙의 끝 상태).
        if (director != null) director.time = 0;
    }

    // ─────────────────────────────── 디버그 키 ───────────────────────────────

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private void Update()
    {
        // 디버그 감시자가 같은 키로 이 컷신을 막 불러와 재생했으면, 같은 프레임에 다시 받아 멈추지 않는다.
        if (Time.frameCount == startedFrame) return;
        if (debugKey != KeyCode.None && Input.GetKeyDown(debugKey)) ToggleDebug();
    }

    private void ToggleDebug()
    {
        if (IsPlaying) StopAndRestore();
        else Play(true);
    }

    /// <summary>
    /// 씬에 컷신이 없어도 디버그 키로 재생할 수 있게, 플레이어가 있는 씬에 작은 감시자를 둔다.
    /// 키를 누르는 순간 Resources에서 프리팹을 불러 재생한다(씬 파일은 바뀌지 않는다).
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InstallDebugSpawner()
    {
        if (FindAnyObjectByType<DeathCutsceneDebugSpawner>() != null) return;
        new GameObject("DeathCutscene DebugKey (auto)").AddComponent<DeathCutsceneDebugSpawner>();
    }
#endif
}

#if UNITY_EDITOR || DEVELOPMENT_BUILD
/// <summary>씬에 <see cref="DeathCutscene"/>가 없을 때만 F8로 Resources의 프리팹을 불러 재생한다.</summary>
public class DeathCutsceneDebugSpawner : MonoBehaviour
{
    private const KeyCode Key = KeyCode.F8;

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        if (!Input.GetKeyDown(Key)) return;
        if (FindAnyObjectByType<DeathCutscene>(FindObjectsInactive.Include) != null) return;   // 씬 것이 직접 받는다
        if (FindAnyObjectByType<FPController>() == null) return;

        DeathCutscene prefab = Resources.Load<DeathCutscene>(DeathCutscene.ResourceName);
        if (prefab == null)
        {
            Debug.LogWarning("[DeathCutscene] Resources/" + DeathCutscene.ResourceName + " 프리팹이 없습니다. 빌더 메뉴를 먼저 실행하십시오.");
            return;
        }

        DeathCutscene cutscene = Instantiate(prefab);
        cutscene.name = prefab.name;
        cutscene.Play(true);
    }
}
#endif

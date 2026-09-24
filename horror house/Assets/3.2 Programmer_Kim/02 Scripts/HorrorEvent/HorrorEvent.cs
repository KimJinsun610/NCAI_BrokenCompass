using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

/// <summary>
/// 공포 연출 하나. Timeline(PlayableDirector)을 재생하고, 끝나면 필요한 것을 되돌린다.
/// <para>
/// <b>무엇이 재생을 부르는지는 모른다.</b> 트리거(<see cref="HorrorTriggerZone"/>), 축 변화(<see cref="HorrorAxisLink"/>),
/// 이후의 지침 결과 연결이 모두 <see cref="Play"/> 하나만 부른다. 조건이 늘어도 이 파일은 그대로 둔다.
/// </para>
/// <para>
/// 연출은 여러 개가 동시에 재생될 수 있다(축 여러 개가 한꺼번에 오르는 경우).
/// Director의 Update Mode는 Game Time이어야 한다 — 그래야 일시정지(<see cref="GamePause"/>)에서 함께 멈춘다.
/// </para>
/// </summary>
public class HorrorEvent : MonoBehaviour
{
    [Header("재생")]
    [Tooltip("이 연출의 Timeline을 재생할 Director. Playable Asset은 인스펙터에서 직접 연결한다.")]
    [SerializeField] private PlayableDirector director;
    [Tooltip("켜면 한 번 재생한 뒤로는 다시 재생하지 않는다.")]
    [SerializeField] private bool playOnce = true;

    [Header("재생 조건")]
    [Tooltip("이 자리가 플레이어 화면에 보이지 않을 때까지 기다렸다가 재생한다.\n인체모형은 시야 밖에서만 나타나야 하므로 모형의 Transform(발밑)을 넣는다.")]
    [SerializeField] private Transform waitUntilUnseen;
    [Tooltip("시야 판정에 쓸 상자 크기(m). 발밑 기준으로 위로 세운다.")]
    [SerializeField] private Vector3 unseenBoxSize = new Vector3(0.6f, 1.8f, 0.6f);
    [Tooltip("이 시간(초) 동안 계속 보이면 이번 재생을 취소한다. 시야 안에서 나타나지 않게 하려는 것이다.")]
    [SerializeField, Min(0f)] private float maxWaitSeconds = 8f;

    [Header("플레이어")]
    [Tooltip("켜면 연출 동안 FPController를 꺼서 이동 · 시점 입력을 막는다.")]
    [SerializeField] private bool lockPlayer;

    [Header("테스트")]
    [Tooltip("켜면 아래 테스트 키로 재생할 수 있다. 끄면 키를 눌러도 반응하지 않는다.")]
    [SerializeField] private bool useDebugKey = true;
    [Tooltip("에디터 · 개발 빌드에서 이 키를 누르면 조건과 상관없이 재생한다. 같은 키를 여러 연출에 주면 함께 재생된다.")]
    [SerializeField] private KeyCode debugKey = KeyCode.None;

    // 보이지 않는 상태가 이만큼 이어져야 「안 보인다」로 본다. 한 프레임 가려진 것으로 튀어나오지 않게 한다.
    private const float UnseenHoldSeconds = 0.2f;

    private readonly List<Behaviour> lockedPlayers = new List<Behaviour>();
    private readonly Plane[] planes = new Plane[6];

    private bool waiting;
    private float waitTime;
    private float unseenTime;

    /// <summary>재생 중인지</summary>
    public bool IsPlaying { get; private set; }

    /// <summary>한 번이라도 재생됐는지</summary>
    public bool HasPlayed { get; private set; }

    private void Awake()
    {
        if (director == null) director = GetComponentInChildren<PlayableDirector>();

        if (director == null || director.playableAsset == null)
        {
            Debug.LogWarning($"[HorrorEvent] {name}: PlayableDirector 또는 Playable Asset이 연결되지 않았습니다.", this);
            return;
        }

        director.playOnAwake = false;
        director.timeUpdateMode = DirectorUpdateMode.GameTime;
    }

    /// <summary>연출을 재생한다. 이미 재생 중이거나 한 번 재생한 연출(Play Once)이면 무시한다.</summary>
    public void Play()
    {
        if (director == null || director.playableAsset == null) return;
        if (IsPlaying || waiting) return;
        if (playOnce && HasPlayed) return;

        if (waitUntilUnseen != null)
        {
            waiting = true;
            waitTime = 0f;
            unseenTime = 0f;
            return;
        }

        StartTimeline();
    }

    private void Update()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (useDebugKey && debugKey != KeyCode.None && Input.GetKeyDown(debugKey) && !GamePause.IsPaused) Play();
#endif

        if (waiting) UpdateWaiting();

        // Wrap Mode가 Hold면 끝나도 Director가 멈추지 않으므로 시간으로 끝을 잡는다.
        if (IsPlaying && (director.state != PlayState.Playing || director.time >= director.duration))
        {
            Finish();
        }
    }

    private void UpdateWaiting()
    {
        waitTime += Time.deltaTime;

        if (IsVisible(waitUntilUnseen)) unseenTime = 0f;
        else unseenTime += Time.deltaTime;

        if (unseenTime >= UnseenHoldSeconds)
        {
            waiting = false;
            StartTimeline();
        }
        else if (waitTime >= maxWaitSeconds)
        {
            // 끝내 시야 밖이 되지 않았다. 보는 앞에서 나타나느니 이번에는 넘긴다.
            waiting = false;
        }
    }

    private void StartTimeline()
    {
        IsPlaying = true;
        HasPlayed = true;

        if (lockPlayer) LockPlayers();

        director.time = 0;
        director.Play();
    }

    private void Finish()
    {
        IsPlaying = false;

        foreach (Behaviour player in lockedPlayers)
        {
            if (player != null) player.enabled = true;
        }
        lockedPlayers.Clear();
    }

    private void LockPlayers()
    {
        foreach (FPController player in FindObjectsByType<FPController>(FindObjectsSortMode.None))
        {
            if (!player.enabled) continue;
            player.enabled = false;
            lockedPlayers.Add(player);
        }

        // FPController를 끄면 OnDisable에서 커서가 보이므로 연출 동안은 숨긴다 (DayIntro와 같은 처리)
        Cursor.visible = false;
    }

    /// <summary>
    /// 그 자리가 지금 플레이어 화면에 보이는가. 시야(프러스텀) 안이고 가운데까지 가린 것이 없어야 보인다.
    /// 모형이 아직 꺼져 있어도 판단할 수 있도록 Renderer가 아니라 자리(발밑 + 상자)로 본다.
    /// </summary>
    private bool IsVisible(Transform spot)
    {
        Camera cam = Camera.main;
        if (cam == null) return false;

        Vector3 center = spot.position + Vector3.up * (unseenBoxSize.y * 0.5f);
        GeometryUtility.CalculateFrustumPlanes(cam, planes);
        if (!GeometryUtility.TestPlanesAABB(planes, new Bounds(center, unseenBoxSize))) return false;

        // 가운데까지 벽이나 문이 가리고 있으면 보이지 않는 것이다. 대상 자신에 맞은 것은 보이는 것으로 친다.
        if (Physics.Linecast(cam.transform.position, center, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore))
        {
            return hit.collider.transform.IsChildOf(spot);
        }

        return true;
    }
}

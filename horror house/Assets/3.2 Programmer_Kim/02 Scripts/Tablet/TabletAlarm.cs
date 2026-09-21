using TMPro;
using UnityEngine;

/// <summary>
/// 태블릿에 새 지시가 왔을 때 울리는 알람.
///
/// 흐름:
///   1) 다른 시스템이 <see cref="Raise"/>를 부른다 (수행 지침이 추가되면 자동으로도 울린다)
///   2) 태블릿이 <b>진동하듯 흔들리고</b>, 내려놓은 상태에서도 <b>알람 화면</b>이 뜨고, <b>소리가 반복</b>된다
///      (태블릿을 들고 있어도 수행 지침 탭에 들어가기 전까지는 안내 문구가 탭 줄 윗줄에 계속 보인다)
///   3) 플레이어가 태블릿을 들어 <b>수행 지침</b> 탭을 보면 꺼진다
///
/// 소리는 클립 칸만 만들어 두었다. 사운드가 나오면 <see cref="alarmClip"/>에 넣으면 된다.
/// </summary>
[DisallowMultipleComponent]
public class TabletAlarm : MonoBehaviour
{
    [Header("연결")]
    [Tooltip("비우면 부모에서 찾는다.")]
    public PlayerTablet tablet;
    [Tooltip("비우면 같은 오브젝트에서 찾는다.")]
    public TabletDocument document;
    [Tooltip("비우면 같은 오브젝트에서 찾는다. 지침이 추가되면 자동으로 알람이 울린다.")]
    public TabletTaskList taskList;
    [Tooltip("알람이 울릴 때 화면에 띄울 글자.")]
    public TMP_Text alarmText;
    [Tooltip("알람 글자와 자리가 겹치는 것이 있으면 그동안 감춘다. 지금은 알람 줄이 탭 줄 윗줄에 따로 있어 비워 둔다.")]
    public GameObject[] hideWhileAlarm = new GameObject[0];

    [Header("알람 화면")]
    [Tooltip("알람이 울릴 때 보여 줄 문구.")]
    public string alarmMessage = "새 지시 도착";
    [Tooltip("깜빡이는 주기(초).")]
    public float blinkSeconds = 0.7f;
    [Tooltip("알람 중에는 태블릿을 내려놔도 화면을 켜 둔다.")]
    public bool keepScreenOn = true;

    [Header("진동")]
    [Tooltip("흔들리는 거리(m).")]
    public float shakePosition = 0.006f;
    [Tooltip("흔들리는 각도.")]
    public float shakeRotation = 1.4f;
    [Tooltip("한 번 부르르 떠는 시간(초).")]
    public float buzzSeconds = 0.5f;
    [Tooltip("다음에 다시 떨기까지 쉬는 시간(초). 계속 떨면 성가시다.")]
    public float buzzInterval = 2.5f;
    [Tooltip("떨리는 빠르기.")]
    public float buzzFrequency = 38f;

    [Header("소리")]
    [Tooltip("알람 소리. 비어 있으면 소리 없이 진동·화면만 동작한다.")]
    public AudioClip alarmClip;
    [Tooltip("비우면 이 오브젝트에 AudioSource를 만들어 쓴다.")]
    public AudioSource audioSource;
    [Range(0f, 1f)] public float volume = 0.6f;
    [Tooltip("켜면 확인할 때까지 계속 반복한다. 끄면 한 번만 울린다.")]
    public bool loopSound = true;

    /// <summary>지금 알람이 울리고 있는가.</summary>
    public bool IsActive { get { return _active; } }

    private ViewmodelSway _sway;
    private bool _active;
    private float _clock;
    private float _buzzStartedAt;

    private void Awake()
    {
        if (tablet == null) tablet = GetComponentInParent<PlayerTablet>();
        if (document == null) document = GetComponentInChildren<TabletDocument>(true);
        if (taskList == null) taskList = GetComponent<TabletTaskList>();
        if (taskList == null) taskList = GetComponentInChildren<TabletTaskList>(true);
        if (tablet != null) _sway = tablet.GetComponent<ViewmodelSway>();

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        }

        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;   // 손에 든 기기 소리라 좌우로 갈리지 않게

        ShowAlarmText(false);
    }

    private void OnEnable()
    {
        if (taskList != null) taskList.TaskAdded += OnTaskAdded;
    }

    private void OnDisable()
    {
        if (taskList != null) taskList.TaskAdded -= OnTaskAdded;
        StopAll();
    }

    /// <summary>알람을 울린다. 이미 울리는 중이면 아무 일도 없다.</summary>
    public void Raise()
    {
        if (_active) return;

        _active = true;
        _buzzStartedAt = _clock;

        if (keepScreenOn && tablet != null) tablet.ForceScreenOn = true;

        if (alarmClip != null && audioSource != null)
        {
            audioSource.clip = alarmClip;
            audioSource.loop = loopSound;
            audioSource.volume = volume;
            audioSource.Play();
        }
    }

    /// <summary>알람을 끈다. 수행 지침을 확인하면 저절로 불린다.</summary>
    public void Acknowledge()
    {
        if (!_active) return;

        _active = false;
        StopAll();
    }

    private void OnTaskAdded(TabletTaskList.Task task)
    {
        Raise();
    }

    private void Update()
    {
        float dt = ViewmodelTime.Delta;
        if (dt <= 0f) return;
        _clock += dt;

        if (_active && IsReading())
        {
            Acknowledge();
            return;
        }

        if (!_active)
        {
            ShowAlarmText(false);
            return;
        }

        UpdateBlink();
        UpdateBuzz();
    }

    /// <summary>태블릿을 들고 수행 지침을 보고 있으면 확인한 것으로 친다.</summary>
    private bool IsReading()
    {
        if (tablet == null || !tablet.IsOpened) return false;
        if (document == null) return true;   // 문서가 없으면 여는 것만으로 확인

        return document.CurrentTab == TabletDocument.Tab.Tasks;
    }

    private void UpdateBlink()
    {
        if (alarmText == null) return;

        // 태블릿을 들고 있어도 수행 지침 탭에 들어가기 전까지는 알람이 그대로이므로,
        // 화면에서도 안내 문구가 계속 보여야 한다. (탭 줄 윗줄에 따로 자리를 잡아 두었다)
        bool on = blinkSeconds <= 0.01f || Mathf.Repeat(_clock, blinkSeconds) < blinkSeconds * 0.5f;
        ShowAlarmText(on);
    }

    private void UpdateBuzz()
    {
        if (_sway == null) return;

        float since = _clock - _buzzStartedAt;
        if (since > buzzSeconds)
        {
            _sway.extraPosition = Vector3.zero;
            _sway.extraRotation = Vector3.zero;

            // 쉬는 시간이 지나면 다시 한 번 부르르 떤다.
            if (since >= buzzSeconds + buzzInterval) _buzzStartedAt = _clock;
            return;
        }

        // 떨림은 끝으로 갈수록 잦아든다.
        float fade = 1f - Mathf.Clamp01(since / Mathf.Max(0.01f, buzzSeconds));
        float wave = Mathf.Sin(since * buzzFrequency);
        float wave2 = Mathf.Sin(since * buzzFrequency * 1.7f + 1.1f);

        _sway.extraPosition = new Vector3(wave * shakePosition, wave2 * shakePosition * 0.6f, 0f) * fade;
        _sway.extraRotation = new Vector3(wave2 * shakeRotation * 0.5f, 0f, wave * shakeRotation) * fade;
    }

    private void ShowAlarmText(bool show)
    {
        if (alarmText != null)
        {
            if (show && alarmText.text != alarmMessage) alarmText.text = alarmMessage;
            if (alarmText.gameObject.activeSelf != show) alarmText.gameObject.SetActive(show);
        }

        // 알람 글자와 같은 줄을 쓰는 것들은 잠시 감춘다.
        for (int i = 0; i < hideWhileAlarm.Length; i++)
        {
            GameObject go = hideWhileAlarm[i];
            if (go != null && go.activeSelf == show) go.SetActive(!show);
        }
    }

    private void StopAll()
    {
        ShowAlarmText(false);

        if (tablet != null) tablet.ForceScreenOn = false;

        if (_sway != null)
        {
            _sway.extraPosition = Vector3.zero;
            _sway.extraRotation = Vector3.zero;
        }

        if (audioSource != null && audioSource.isPlaying) audioSource.Stop();
    }
}

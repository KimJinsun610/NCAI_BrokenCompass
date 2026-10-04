using NightDuty;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 플레이어 발소리(2026-10-04 사운드 전달본 07_Footsteps). 근무 씬의 플레이어 루트에 자동으로 붙는다.
/// <list type="bullet">
/// <item>바닥은 엠비언트가 아는 지금 공간(<see cref="AmbiencePlayer.CurrentZone"/>)으로 고른다 —
/// 교실 = 나무 · 복도 = 타일 · 도서관 · 화장실 = 젖은 타일 · 과학실 = 리놀륨 · 경비실 = 콘크리트. 모르면 복도.</item>
/// <item>수평으로 움직인 거리를 세어 걸음 폭(걷기 <see cref="walkStride"/> · 뛰기 <see cref="runStride"/>)마다 한 걸음.
/// 뛰기 = 속도가 걷기와 뛰기 속도의 가운데를 넘을 때(<see cref="FPController"/> 값을 읽는다).</item>
/// <item>소리는 연출 소리 표의 <c>step.&lt;바닥&gt;.walk</c> · <c>.run</c>(여러 판 중 무작위). 표에 없으면 조용하다.</item>
/// <item><b>판정과 무관하다</b> — 신호를 보내지 않는다. 한 프레임에 2m 넘게 옮겨지면(재시작 순간이동) 세지 않는다.</item>
/// </list>
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerFootsteps : MonoBehaviour
{
    [SerializeField, Min(0.2f)] private float walkStride = 0.95f;
    [SerializeField, Min(0.2f)] private float runStride = 1.55f;
    [SerializeField, Range(0f, 1f)] private float walkVolume = 0.4f;
    [SerializeField, Range(0f, 1f)] private float runVolume = 0.6f;

    private FPController _fp;
    private AudioSource _source;
    private Vector3 _last;
    private bool _known;
    private float _travel;
    private float _speed;

    /// <summary>마지막으로 낸 걸음의 키(디버그·검수).</summary>
    public string LastStep { get; private set; }

    /// <summary>지금까지 낸 걸음 수(디버그·검수).</summary>
    public int Steps { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Install()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InstallForFirstScene()
    {
        EnsureFor(SceneManager.GetActiveScene());
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureFor(scene);
    }

    private static void EnsureFor(Scene scene)
    {
        if (!FlowAutoInstall.IsDutyScene(scene) || FlowAutoInstall.Exists<PlayerFootsteps>(scene)) return;
        FPController fp = null;
        foreach (FPController c in FindObjectsByType<FPController>(FindObjectsSortMode.None))
        {
            if (c.gameObject.scene == scene)
            {
                fp = c;
                break;
            }
        }

        if (fp != null) fp.gameObject.AddComponent<PlayerFootsteps>();
    }

    private void Awake()
    {
        _fp = GetComponent<FPController>();
        _source = gameObject.AddComponent<AudioSource>();
        _source.playOnAwake = false;
        _source.spatialBlend = 0f;
        _source.priority = 100;
    }

    private void Update()
    {
        Vector3 p = transform.position;
        float dt = Time.deltaTime;
        if (!_known || dt <= 0f)
        {
            _last = p;
            _known = true;
            return;
        }

        Vector3 d = p - _last;
        _last = p;
        float vertical = Mathf.Abs(d.y) / dt;
        d.y = 0f;
        float dist = d.magnitude;
        if (dist > 2f || NightRun.IsCaptured || (_fp != null && !_fp.enabled))
        {
            _travel = 0f;   // 순간이동·붙잡힘·조작 정지
            _speed = 0f;
            return;
        }

        _speed = Mathf.Lerp(_speed, dist / dt, 1f - Mathf.Exp(-dt * 10f));
        if (dist < 0.0005f)
        {
            _travel = Mathf.Max(0f, _travel - dt * 0.5f);   // 멈추면 반쯤 남은 걸음은 서서히 잊는다
            return;
        }

        if (vertical > 2.5f) return;   // 떨어지거나 뛰어오르는 중
        bool running = _speed > RunThreshold();
        _travel += dist;
        float stride = running ? runStride : walkStride;
        if (_travel < stride) return;
        _travel -= stride;
        Step(running);
    }

    private float RunThreshold()
    {
        if (_fp == null) return 3.5f;
        return (_fp.walkSpeed + _fp.runSpeed) * 0.5f;
    }

    private void Step(bool running)
    {
        string key = "step." + Surface() + (running ? ".run" : ".walk");
        float volume;
        AudioClip clip = DirectionSoundTableSO.FindExact(key, out volume);
        if (clip == null) return;
        _source.pitch = Random.Range(0.95f, 1.05f);
        _source.PlayOneShot(clip, volume * (running ? runVolume : walkVolume));
        LastStep = key;
        Steps++;
    }

    /// <summary>지금 바닥(소리 키의 가운데 이름).</summary>
    public static string Surface()
    {
        AmbiencePlayer amb = AmbiencePlayer.Active;
        string zone = amb != null ? amb.CurrentZone : string.Empty;
        switch (zone)
        {
            case "교실": return "CLASS";
            case "도서관": return "LIBRARY";
            case "화장실": return "TOILET";
            case "과학실": return "LAB";
            case "경비실": return "GUARD";
            default: return "HALL";
        }
    }
}

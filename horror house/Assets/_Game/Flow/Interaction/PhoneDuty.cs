using NightDuty;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 80차 W19 「경비실 전화가 울리면 받아주십시오.」 — 지시를 받으면 잠시 뒤(<see cref="FirstDelayMin"/>~<see cref="FirstDelayMax"/>초) 경비실 전화가 울린다.
/// <see cref="RingSeconds"/>초 동안 받지 않으면 그쳤다가 <see cref="RetrySeconds"/>초 뒤 다시 울린다. 받으면(<see cref="PhoneRinger"/> → <see cref="JudgeSignal.PhoneAnswer"/>) 지시 완료.
/// K5(세 번 울린 뒤에 받으십시오)가 있는 날엔 그 판정도 그대로 받는다 — 지시와 수칙이 맞물린다.
/// </summary>
[DisallowMultipleComponent]
public sealed class PhoneDuty : MonoBehaviour
{
    public const float FirstDelayMin = 8f;
    public const float FirstDelayMax = 18f;
    public const float RingSeconds = 30f;
    public const float RetrySeconds = 20f;

    private float _ringAt = -1f;
    private bool _wasRinging;

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
        if (!FlowAutoInstall.IsDutyScene(scene) || FlowAutoInstall.Exists<PhoneDuty>(scene)) return;
        FlowAutoInstall.CreateHost<PhoneDuty>(scene, "PhoneDuty (auto)");
    }

    private static bool Wanted
    {
        get
        {
            DutyDispatcher d = NightRun.Duties;
            return NightRun.IsNightActive && !NightRun.IsCaptured && d != null && d.Active != null && d.Active.Kind == DutyKind.PhoneAnswer;
        }
    }

    private void Update()
    {
        if (!Wanted)
        {
            _ringAt = -1f;
            _wasRinging = false;
            return;
        }

        bool ringing = PhoneRinger.IsRinging;
        if (_wasRinging && !ringing) _ringAt = Time.time + RetrySeconds;   // 받지 않고 그쳤다 — 다시
        _wasRinging = ringing;
        if (ringing) return;

        if (_ringAt < 0f) _ringAt = Time.time + Random.Range(FirstDelayMin, FirstDelayMax);
        if (Time.time < _ringAt) return;
        _ringAt = float.MaxValue;
        if (PhoneRinger.Ring(RingSeconds)) Debug.Log("[전화기] W19 — 전화가 울린다");
    }
}

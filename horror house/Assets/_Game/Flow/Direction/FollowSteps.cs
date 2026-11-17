using NightDuty;
using UnityEngine;

/// <summary>
/// 따라오는 발소리(E.FollowSteps, 수칙 H5 「인기척이 느껴질 때 무작정 뒤돌아 보시면 안됩니다.」) — 사용자 기획.
/// <list type="bullet">
/// <item>플레이어가 걷기 시작하고 <see cref="StartDelay"/>초 뒤부터, 플레이어 발걸음에 맞춰 <see cref="TrailSteps"/>발자국 뒤에서 플레이어 발소리(0.85배 속)가 3D로 따라온다.</item>
/// <item>플레이어가 멈추면 소리가 뒤로 2발자국 물러나는 소리를 내고 사라진다(위반 없음).</item>
/// <item>걷던 방향의 <b>뒤쪽 <see cref="TurnAngle"/>° 안</b>을 보면 뒤돌아본 것 — 들어가는 즉시 한 번으로 센다(<see cref="FinalCues.FollowTurn"/> 단서). 뒤돌아보면 10초 타이머가 처음으로.</item>
/// <item>3번째 뒤돌아봄은 위반(0→180→0처럼 돌아봤다 되돌아오는 것도 한 번씩 센다)(판정기 <see cref="FollowTurnJudge"/> — 청각 +15)이고 소리가 사라진다. 한 번도 안 봐도 10초가 지나면 사라진다.</item>
/// </list>
/// 사라질 때 <see cref="NightRun.EndEncounterWindow"/>로 조우 창을 닫아 판정(준수)을 낸다. 광과민과 무관한 순수 소리 연출이다.
/// </summary>
[DisallowMultipleComponent]
public sealed class FollowSteps : MonoBehaviour
{
    /// <summary>플레이어가 걷기 시작한 뒤 소리가 시작되기까지(초).</summary>
    public const float StartDelay = 0.6f;

    /// <summary>플레이어 뒤 발자국 수(걸음 간격 <see cref="Stride"/>).</summary>
    public const int TrailSteps = 3;

    /// <summary>한 발자국 간격(m) — 플레이어 걸음(<c>PlayerFootsteps</c>)과 같다.</summary>
    public const float Stride = 0.95f;

    /// <summary>소리 재생 속도(음높이·빠르기 0.85배).</summary>
    public const float SoundPitch = 0.85f;

    /// <summary>걷던 방향의 뒤쪽 이 각도(°) 안을 보면 뒤돌아본 것 — 들어가자마자 센다.</summary>
    public const float TurnAngle = 35f;

    /// <summary>다시 뒤돌아봄으로 세려면 먼저 이 각도(°) 밖으로 시선이 나가야 한다.</summary>
    public const float RearmAngle = 60f;

    /// <summary>뒤돌아봄이 없으면 소리가 사라지기까지(초). 뒤돌아볼 때마다 처음부터.</summary>
    public const float IgnoreSeconds = 10f;

    /// <summary>멈췄다고 보는 속도(m/s) 미만이 이 시간(초) 이어지면 물러난다.</summary>
    public const float StopSeconds = 0.3f;

    private const float MovingSpeed = 0.35f;

    /// <summary>뒤돌아본 직후 이 시간(초)은 멈춰도 소리가 물러나지 않는다(뒤돌아보려고 멈추는 것이 자연스럽다).</summary>
    public const float TurnGrace = 3f;

    private AudioSource _source;
    private Vector3 _lastPos;
    private bool _known;
    private float _speed;
    private Vector3 _moveDir = Vector3.forward;
    private bool _hasDir;
    private float _walkSince = -1f;
    private float _stillFor;
    private bool _following;
    private float _travel;
    private float _ignoreLeft = IgnoreSeconds;
    private int _turns;
    private bool _rearmed = true;
    private float _lastTurnAt = -99f;
    private bool _leaving;
    private bool _ended;
    private int _backSteps;
    private float _backAt;
    private string _id;

    /// <summary>지금까지 센 뒤돌아봄(시험·검수).</summary>
    public int Turns
    {
        get { return _turns; }
    }

    /// <summary>소리가 따라오는 중인지(시험·검수).</summary>
    public bool Following
    {
        get { return _following && !_leaving; }
    }

    /// <summary>끝났는지(시험·검수).</summary>
    public bool Ended
    {
        get { return _ended; }
    }

    /// <summary>시작한다. <paramref name="encounterId"/> = 끝날 때 닫을 조우 ID.</summary>
    public static FollowSteps Begin(string encounterId)
    {
        GameObject go = new GameObject("따라오는 발소리");
        FollowSteps f = go.AddComponent<FollowSteps>();
        f._id = encounterId;
        f._source = go.AddComponent<AudioSource>();
        f._source.playOnAwake = false;
        f._source.spatialBlend = 1f;
        f._source.rolloffMode = AudioRolloffMode.Linear;
        f._source.minDistance = 1.5f;
        f._source.maxDistance = 18f;
        f._source.dopplerLevel = 0f;
        f._source.priority = 90;
        return f;
    }

    /// <summary>조우가 거둬질 때(중단·밤 끝) 조용히 치운다.</summary>
    public void Cancel()
    {
        if (_ended) return;
        _ended = true;
        Destroy(gameObject);
    }

    private void Update()
    {
        if (_ended) return;
        if (!NightRun.IsNightActive || NightRun.IsCaptured)
        {
            Cancel();
            return;
        }

        Camera cam = Camera.main;
        float dt = Time.deltaTime;
        if (cam == null || dt <= 0f) return;

        Vector3 pos = cam.transform.position;
        pos.y = 0f;
        if (!_known)
        {
            _lastPos = pos;
            _known = true;
            return;
        }

        Vector3 d = pos - _lastPos;
        _lastPos = pos;
        float dist = d.magnitude;
        if (dist > 2f)
        {
            _speed = 0f;   // 순간이동
            return;
        }

        _speed = Mathf.Lerp(_speed, dist / dt, 1f - Mathf.Exp(-dt * 12f));
        bool moving = _speed > MovingSpeed;
        if (moving && dist > 0.0005f) _moveDir = Vector3.Slerp(_moveDir, d / dist, 1f - Mathf.Exp(-dt * 6f));
        if (moving) _hasDir = true;

        if (_leaving)
        {
            TickLeaving();
            return;
        }

        Vector3 forward = cam.transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude > 0.0001f) forward.Normalize();

        if (moving)
        {
            _stillFor = 0f;
            if (_walkSince < 0f) _walkSince = Time.time;
            if (!_following && Time.time - _walkSince >= StartDelay) StartFollowing();
        }
        else
        {
            _walkSince = -1f;
            _stillFor += dt;
            // 뒤돌아보려고 멈춘 것은 「멈춤」이 아니다 — 뒤돌아본 직후 TurnGrace초는 소리가 그 자리에서 기다린다.
            if (_following && _stillFor >= StopSeconds && Time.time - _lastTurnAt >= TurnGrace)
            {
                BeginLeave(pos);
                return;
            }
        }

        if (!_following) return;

        TickTurn(forward);
        if (_ended) return;

        // 플레이어 걸음에 맞춰 한 발자국씩.
        if (moving)
        {
            _travel += dist;
            if (_travel >= Stride)
            {
                _travel -= Stride;
                PlayStep(Behind(pos, TrailSteps), 1f);
            }
        }

        _ignoreLeft -= dt;
        if (_ignoreLeft <= 0f) Finish();   // 10초 무시 — 소리가 사라진다
    }

    private void StartFollowing()
    {
        _following = true;
        _ignoreLeft = IgnoreSeconds;
        _travel = 0f;
        if (_hasDir)
        {
            Camera cam = Camera.main;
            if (cam != null)
            {
                Vector3 p = cam.transform.position;
                p.y = 0f;
                PlayStep(Behind(p, TrailSteps), 1f);   // 첫 발자국
            }
        }
    }

    private void TickTurn(Vector3 forward)
    {
        if (!_hasDir || _moveDir.sqrMagnitude < 0.0001f) return;
        float angle = Vector3.Angle(forward, -_moveDir.normalized);
        if (angle > RearmAngle) _rearmed = true;
        if (!_rearmed || angle > TurnAngle) return;

        _rearmed = false;
        _turns++;
        _ignoreLeft = IgnoreSeconds;   // 뒤돌아봤으니 10초 타이머 초기화
        _lastTurnAt = Time.time;
        Camera cam = Camera.main;
        Vector3 at = cam != null ? cam.transform.position : Vector3.zero;
        NightRun.Send(JudgeSignal.Cue(FinalCues.FollowTurn, at));
        NightRun.Send(JudgeSignal.CueEnd(FinalCues.FollowTurn));
        if (_turns >= FollowTurnJudge.TurnLimit) Finish();   // 3번째 — 위반, 소리가 사라진다
    }

    private void BeginLeave(Vector3 pos)
    {
        _leaving = true;
        _backSteps = 0;
        _backAt = Time.time;
    }

    private void TickLeaving()
    {
        // 멈춘 플레이어 뒤에서 두 발자국 물러나는 소리 — 점점 멀어진다.
        if (Time.time < _backAt) return;
        if (_backSteps >= 2)
        {
            Finish();
            return;
        }

        Camera cam = Camera.main;
        if (cam != null)
        {
            Vector3 p = cam.transform.position;
            p.y = 0f;
            _backSteps++;
            PlayStep(Behind(p, TrailSteps + _backSteps * 2), 0.8f - 0.2f * _backSteps);
        }

        _backAt = Time.time + 0.6f;
    }

    /// <summary>걷던 방향 뒤 <paramref name="steps"/>발자국 자리(귀 높이 아래 바닥).</summary>
    private Vector3 Behind(Vector3 pos, int steps)
    {
        Vector3 back = _moveDir.sqrMagnitude > 0.0001f ? -_moveDir.normalized : Vector3.back;
        Vector3 p = pos + back * (Stride * steps);
        Camera cam = Camera.main;
        p.y = cam != null ? DirectionStage.FloorBelow(cam.transform.position).y + 0.1f : 0.1f;
        return p;
    }

    private void PlayStep(Vector3 at, float volumeScale)
    {
        float volume;
        AudioClip clip = DirectionSoundTableSO.FindExact("step." + PlayerFootsteps.Surface() + ".walk", out volume);
        if (clip == null || _source == null) return;
        transform.position = at;
        _source.pitch = SoundPitch * Random.Range(0.97f, 1.03f);
        _source.PlayOneShot(clip, Mathf.Clamp01(volume * 0.9f * volumeScale));
    }

    /// <summary>소리가 사라진다 — 조우 창을 닫아 판정(2번 뒤돌아봄이면 이미 위반)을 낸다.</summary>
    private void Finish()
    {
        if (_ended) return;
        _ended = true;
        NightRun.EndEncounterWindow(_id);
        Destroy(gameObject, 1.2f);   // 마지막 발자국 소리가 끝나게
    }
}

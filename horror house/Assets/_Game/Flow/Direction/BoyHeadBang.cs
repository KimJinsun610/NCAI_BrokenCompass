using UnityEngine;

/// <summary>
/// 52차 소년 머리 박기(민: 「C2 교실의 _? 는 무시하십시오 — 앉은 소년을 3초 이상 바라보면 머리를 책상에 박는 연출로」).
/// 앉은 소년 대역(<c>mob.boy</c>, 애니메이터 없음)의 척추·가슴·목 뼈를 앞으로 꺾어 책상에 세 번 박고, 마지막에는 엎드린 채 멈춘다.
/// 박을 때마다 머리 부딪는 소리(<c>E.BoyBang.headbang</c>, 복도까지 들리게 크게). 대역이 거둬지면 함께 사라진다.
/// 58차: 대역에 아트의 머리 박기 동작(<see cref="StandInClips.Bang"/> — 새 skirtboy 「kung」에서 척추·목·머리만)이 있으면 그것을 굴리고
/// 머리가 닿는 순간(<see cref="StandInClips.BangThuds"/>)마다 소리, 끝나면 마지막으로 닿은 자세로 엎드려 멈춘다. 없으면 옛 절차 꺾기.
/// 60차(민: 「머리 치는 게 조금 더 길고, 바라보면 점점 더 빨라지면 · 칠 때마다 약간의 흔들림」): 아트 동작을 되풀이해 최소 <see cref="MinSeconds"/>초,
/// 플레이어가 소년을 바라보는 동안은 박는 속도가 초당 <see cref="SpeedUpPerSecond"/>씩 빨라지고(최대 <see cref="MaxSpeed"/>배) 바라보는 한 계속한다(최대 <see cref="MaxSeconds"/>초).
/// 눈을 떼면 천천히 원래 속도로 돌아오고 <see cref="LookAwayGrace"/>초 뒤 다음 박기에서 엎드려 멈춘다. 박을 때마다 화면이 툭 흔들린다(가까울수록·빠를수록 세게, <see cref="BodyMeter.Jolt"/>).
/// 103차(민: 「소년이 나타나고 머리 박기를 시작하면, 플레이어가 교실을 나가거나 밖에 있어도 계속 박다가 사라지게」): 조우가 건 머리 박기(<see cref="UntilGone"/>)는
/// 눈을 떼도·교실을 나가도 멈추지 않고 대역이 거둬질 때(창이 끝나 시야 밖에서 사라질 때)까지 박는다. 3초 이어 바라보면 놀람은 그대로.
/// 104차(사용자: 「릴리스로 빌드하면 소년 머리박는게 적용이 안된다」): 대역에 애니메이터가 없어 빌드에서는 비레거시 클립의 <c>SampleAnimation</c>이
/// 「Non-Legacy animations cannot be sampled outside the Editor without an Animator」로 아무것도 하지 않았다(편집기에서만 됨) → 클립을 레거시로(<see cref="StandInClips"/>).
/// </summary>
[DisallowMultipleComponent]
public sealed class BoyHeadBang : MonoBehaviour
{
    /// <summary>박는 횟수.</summary>
    public const int Bangs = 3;

    /// <summary>앞으로 꺾는 최대 각(°, 척추·가슴·목 합).</summary>
    public const float DownDegrees = 48f;

    private const float DownTime = 0.09f;
    private const float HoldTime = 0.08f;
    private const float UpTime = 0.42f;
    private const float PauseTime = 0.22f;
    private const float RestDegrees = 8f;

    private static readonly string[] BoneNames = { "Spine", "Chest", "Neck" };
    private static readonly float[] Weights = { 0.45f, 0.3f, 0.25f };

    /// <summary>최소 길이(초) — 바라보지 않아도 이만큼은 박는다.</summary>
    public const float MinSeconds = 6f;

    /// <summary>바라보는 동안 이어 가는 최대 길이(초).</summary>
    public const float MaxSeconds = 14f;

    /// <summary>바라보는 동안 초당 빨라지는 배수.</summary>
    public const float SpeedUpPerSecond = 0.45f;

    /// <summary>최대 속도 배수.</summary>
    public const float MaxSpeed = 3.2f;

    /// <summary>눈을 뗀 뒤 멈추기까지(초).</summary>
    public const float LookAwayGrace = 1.5f;

    /// <summary>「바라봄」 시선 각(°).</summary>
    public const float WatchDegrees = 16f;

    /// <summary>박을 때 화면 흔들림 최대 각(°).</summary>
    public const float JoltDegrees = 1.3f;

    private const float WrapBlend = 0.15f;

    private Transform[] _bones;
    private StandInClips _clips;
    private Quaternion[] _base;
    private float _start;
    private int _played;
    private float _clipT;
    private float _speed = 1f;
    private float _lastWatched = -999f;
    private bool _stopping;
    private bool _stopped;
    private float _holdAt;
    private Transform[] _clipBones;
    private Quaternion[] _wrapPose;
    private Transform _head;
    private bool _watchedToEnd;
    private float _watchRun;
    private float _lastSeenAt = -999f;
    private bool _scared;

    /// <summary>
    /// 91차(민: 「소년 머리 박다가 놀람 연출 나오는 게 너무 오래 걸려. 머리 박는 걸 3초 쳐다보면 바로 나오게」): 박는 동안 이만큼(초) 이어 바라보면 곧바로 놀람(<see cref="WatchedToEnd"/>).
    /// 0.3초 미만 눈을 뗀 것은 이어서 센다. 옛: 끝(<see cref="MaxSeconds"/> 14초)까지 바라봐야 했다.
    /// </summary>
    public const float WatchToScare = 3f;

    /// <summary>80차(민: 「머리 부딪히는 걸 끝까지 목격한다면 덮치는 연출 후 증발」): 끝(<see cref="MaxSeconds"/>)까지 바라본 채 박기가 멈췄을 때.</summary>
    public static event System.Action<BoyHeadBang> WatchedToEnd;

    /// <summary>지금 속도 배수. 시험·디버그용.</summary>
    public float Speed
    {
        get { return _speed; }
    }

    /// <summary>박은 횟수. 시험·디버그용.</summary>
    public int Thuds
    {
        get { return _played; }
    }

    /// <summary>103차: 대역이 사라질 때까지 멈추지 않고 박는지(조우가 건 머리 박기).</summary>
    public bool UntilGone
    {
        get { return _untilGone; }
    }

    private bool _untilGone;

    /// <summary>엎드려 멈췄는지.</summary>
    public bool Stopped
    {
        get { return _stopped; }
    }

    /// <summary>머리 박기를 시작한다(이미 하고 있으면 그대로). <paramref name="untilGone"/>면 대역이 거둬질 때까지 멈추지 않는다(103차).</summary>
    public static BoyHeadBang Play(GameObject boy, bool untilGone = false)
    {
        if (boy == null) return null;
        BoyHeadBang b = boy.GetComponent<BoyHeadBang>();
        if (b == null) b = boy.AddComponent<BoyHeadBang>();
        if (untilGone) b._untilGone = true;
        return b;
    }

    private void Awake()
    {
        _bones = new Transform[BoneNames.Length];
        _base = new Quaternion[BoneNames.Length];
        foreach (Transform t in GetComponentsInChildren<Transform>(true))
        {
            for (int i = 0; i < BoneNames.Length; i++)
            {
                if (_bones[i] == null && t.name == BoneNames[i])
                {
                    _bones[i] = t;
                    _base[i] = t.localRotation;
                }
            }
        }

        _start = Time.time;
        _clips = GetComponent<StandInClips>();
        if (_clips != null && (_clips.Bang == null || _clips.BangThuds.Length == 0)) _clips = null;
        if (_clips != null)
        {
            // 동작이 움직이는 뼈(척추·가슴·목·머리) — 되풀이 이음매를 부드럽게 섞을 때 쓴다.
            string[] names = { "Spine", "Chest", "Neck", "Head" };
            _clipBones = new Transform[names.Length];
            foreach (Transform t in _clips.ClipRoot.GetComponentsInChildren<Transform>(true))
            {
                for (int i = 0; i < names.Length; i++) if (_clipBones[i] == null && t.name == names[i]) _clipBones[i] = t;
            }

            _head = _clipBones[3];
            _wrapPose = new Quaternion[names.Length];
            _clips.Bang.SampleAnimation(_clips.ClipRoot, 0f);
            for (int i = 0; i < _clipBones.Length; i++) if (_clipBones[i] != null) _wrapPose[i] = _clipBones[i].localRotation;
            if (_head != null) _faceAxisLocal = _head.InverseTransformDirection(transform.forward);   // 105차: 얼굴 앞 = 몸 앞(처음 자세 기준)
            _pauseAfter = Random.Range(3, 6);   // 세 번째~다섯 번째 박기 뒤에 한 번 멈춘다
        }
    }

    private float Cycle
    {
        get { return DownTime + HoldTime + UpTime + PauseTime; }
    }

    private void LateUpdate()
    {
        float t = Time.time - _start;
        if (!_scared && !_stopped)
        {
            // 91차: 박는 걸 3초 이어 바라보면 바로 놀람.
            bool seen = Watched();
            if (seen) _lastSeenAt = Time.time;
            if (seen) _watchRun += Time.deltaTime;
            else if (Time.time - _lastSeenAt > 0.3f) _watchRun = 0f;
            if (_watchRun >= WatchToScare)
            {
                _scared = true;
                if (_clips != null)
                {
                    _stopped = true;
                    _holdAt = _clipT;
                }

                if (WatchedToEnd != null) WatchedToEnd(this);
            }
        }

        if (_clips != null)
        {
            PlayClip(t);
            return;
        }

        int index = Mathf.FloorToInt(t / Cycle);
        float a;
        if (index >= Bangs && (!_untilGone || _scared))
        {
            a = DownDegrees * 0.85f;   // 엎드린 채 멈춤
        }
        else
        {
            float c = t - index * Cycle;
            if (c < DownTime) a = Mathf.Lerp(RestDegrees, DownDegrees, EaseIn(c / DownTime));
            else if (c < DownTime + HoldTime) a = DownDegrees;
            else if (c < DownTime + HoldTime + UpTime) a = Mathf.Lerp(DownDegrees, RestDegrees, EaseOut((c - DownTime - HoldTime) / UpTime));
            else a = RestDegrees;

            if (c >= DownTime && _played <= index)
            {
                _played = index + 1;
                Thud(index);
            }
        }

        for (int i = 0; i < _bones.Length; i++)
        {
            if (_bones[i] == null) continue;
            _bones[i].localRotation = _base[i] * Quaternion.Euler(a * Weights[i], 0f, 0f);
        }
    }

    /// <summary>
    /// 아트 동작으로 박는다(60차) — 머리 박기 구간을 되풀이하며, 바라보는 동안 빨라진다. 멈출 때는 다음으로 머리가 닿는 자세에서 엎드린다.
    /// </summary>
    private void PlayClip(float t)
    {
        float[] thuds = _clips.BangThuds;
        float cycle = Mathf.Min(_clips.Bang.length, thuds[thuds.Length - 1] + 0.3f);
        if (_stopped)
        {
            _clips.Bang.SampleAnimation(_clips.ClipRoot, _holdAt);
            return;
        }

        bool watched = Watched();
        if (watched) _lastWatched = Time.time;
        float dt = Time.deltaTime;
        _speed = watched ? Mathf.Min(MaxSpeed, _speed + SpeedUpPerSecond * dt) : Mathf.Max(1f, _speed - 0.6f * dt);
        if (!_stopping && !_untilGone && t >= MinSeconds && (t >= MaxSeconds || Time.time - _lastWatched > LookAwayGrace))
        {
            _stopping = true;
            _watchedToEnd = t >= MaxSeconds && Time.time - _lastWatched <= LookAwayGrace;   // 80차
        }


        // 105차(사용자: 「박는 박자를 불규칙하게, 한 번 멈춘 뒤 고개를 플레이어 쪽으로」): 멈춘 동안은 그 자세에서 고개만 플레이어 쪽으로 돌린다.
        if (Time.time < _pauseUntil)
        {
            _clips.Bang.SampleAnimation(_clips.ClipRoot, _pauseT);
            TurnHead(true, dt);
            return;
        }

        float prev = _clipT;
        _clipT += dt * _speed * _beatMul;
        // 머리가 닿는 순간을 지났는지(되풀이 경계 포함).
        for (int k = 0; k < thuds.Length; k++)
        {
            float th = thuds[k];
            bool crossed = prev < th && _clipT >= th;
            if (!crossed && _clipT >= cycle && prev < th + cycle && _clipT - cycle >= th && prev - cycle < th) crossed = true;
            if (!crossed) continue;
            Thud(_played);
            _played++;
            _beatMul = Random.Range(BeatMulMin, BeatMulMax);   // 105차: 다음 박기까지의 박자를 박을 때마다 다르게
            if (_stopping)
            {
                _stopped = true;
                _holdAt = th;
                _clips.Bang.SampleAnimation(_clips.ClipRoot, _holdAt);
                if (_watchedToEnd && !_scared && WatchedToEnd != null)   // 80차
                {
                    _scared = true;
                    WatchedToEnd(this);
                }
                return;
            }

            if (!_pauseDone && _played >= _pauseAfter && !_scared)
            {
                // 한 번 멈춘다 — 머리가 막 들리는 자세(닿은 뒤 짧게)에서, 고개를 플레이어 쪽으로.
                _pauseDone = true;
                _pauseUntil = Time.time + Random.Range(PauseMin, PauseMax);
                _pauseT = Mathf.Min(th + 0.14f, cycle - 0.02f);
            }
        }

        if (Time.time < _pauseUntil)
        {
            // 방금 멈추기로 했다 — 이번 프레임부터 그 자세로.
            _clips.Bang.SampleAnimation(_clips.ClipRoot, _pauseT);
            TurnHead(true, dt);
            return;
        }

        if (_clipT >= cycle) _clipT -= cycle;
        _clips.Bang.SampleAnimation(_clips.ClipRoot, _clipT);

        // 되풀이 이음매: 끝 무렵에는 처음 자세로 섞어 들어간다(덜컥 튀지 않게).
        float left = cycle - _clipT;
        if (left < WrapBlend && _clipBones != null)
        {
            float w = 1f - left / WrapBlend;
            for (int i = 0; i < _clipBones.Length; i++)
            {
                if (_clipBones[i] != null) _clipBones[i].localRotation = Quaternion.Slerp(_clipBones[i].localRotation, _wrapPose[i], w);
            }
        }

        TurnHead(false, dt);   // 멈춤이 끝나면 고개가 천천히 원래대로
    }

    // ── 105차: 박자 불규칙 · 한 번 멈춤 · 고개 돌림 ──────────────────────

    /// <summary>다음 박기까지의 박자 배수 범위 — 박을 때마다 다시 뽑는다.</summary>
    public const float BeatMulMin = 0.72f;
    public const float BeatMulMax = 1.4f;

    /// <summary>한 번 멈추는 길이(초) 범위. 멈추면 그동안 고개만 플레이어 쪽으로 돈다.</summary>
    public const float PauseMin = 0.9f;
    public const float PauseMax = 1.5f;

    /// <summary>고개를 돌리는 최대 각(°) — 목이 꺾여 보이지 않게.</summary>
    public const float HeadTurnMaxDegrees = 75f;

    /// <summary>고개가 돌아가고 돌아오는 빠르기(초당 비율, 4 = 0.25초).</summary>
    private const float HeadTurnRate = 4f;

    private float _beatMul = 1f;
    private float _pauseUntil = -1f;
    private float _pauseT;
    private bool _pauseDone;
    private int _pauseAfter = -1;
    private float _turn;
    private Vector3 _faceAxisLocal = Vector3.forward;

    /// <summary>
    /// 머리뼈를 카메라 쪽으로 돌린다(<paramref name="on"/>이면 돌리고, 아니면 원래대로 풀어 준다). 동작 자세를 먼저 표본한 뒤 그 위에 덧붙인다.
    /// 응시 판정(<see cref="Watched"/>)과는 무관하다.
    /// </summary>
    private void TurnHead(bool on, float dt)
    {
        _turn = Mathf.MoveTowards(_turn, on ? 1f : 0f, HeadTurnRate * dt);
        if (_turn < 0.001f || _head == null) return;
        Camera cam = Camera.main;
        if (cam == null) return;

        Vector3 cur = _head.TransformDirection(_faceAxisLocal);
        Vector3 want = cam.transform.position - _head.position;
        if (cur.sqrMagnitude < 1e-6f || want.sqrMagnitude < 1e-6f) return;
        Quaternion q = Quaternion.FromToRotation(cur, want.normalized);
        q = Quaternion.RotateTowards(Quaternion.identity, q, HeadTurnMaxDegrees);
        _head.rotation = Quaternion.Slerp(Quaternion.identity, q, _turn) * _head.rotation;
    }

    /// <summary>플레이어가 소년(머리)을 바라보는지 — 시선 <see cref="WatchDegrees"/>° 안, 25m 안, 가리는 것 없음.</summary>
    private bool Watched()
    {
        Camera cam = Camera.main;
        if (cam == null) return false;
        Vector3 head = _head != null ? _head.position : transform.position + Vector3.up;
        Vector3 to = head - cam.transform.position;
        if (to.magnitude > 25f || Vector3.Angle(cam.transform.forward, to) > WatchDegrees) return false;
        Transform player = cam.transform.root;
        foreach (RaycastHit h in Physics.RaycastAll(cam.transform.position, to.normalized, to.magnitude - 0.3f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (h.collider.transform.IsChildOf(transform) || h.collider.transform.IsChildOf(player)) continue;
            return false;
        }

        return true;
    }

    private void Thud(int index)
    {
        Vector3 at = _bones[_bones.Length - 1] != null ? _bones[_bones.Length - 1].position : transform.position + Vector3.up;
        // 복도까지 들리게 — 최소 거리 8m, 3D 60%.
        DirectionStage.PlaySound(NightDuty.ProgramCatalog.BoyBang + ".headbang", at, 8f, 0.6f);
        if (index == 0) EncounterImpact.PlayTier(EncounterImpact.Tier.Strong);

        // 60차: 박을 때마다 화면이 툭 — 가까울수록, 빨라질수록 세게.
        Camera cam = Camera.main;
        float d = cam != null ? Vector3.Distance(cam.transform.position, at) : 99f;
        float near = Mathf.Clamp01(1.25f - d / 10f);
        float fast = Mathf.InverseLerp(1f, MaxSpeed, _speed);
        if (near > 0f) BodyMeter.Jolt(JoltDegrees * near * (0.55f + 0.45f * fast));
    }

    private static float EaseIn(float x)
    {
        x = Mathf.Clamp01(x);
        return x * x;
    }

    private static float EaseOut(float x)
    {
        x = Mathf.Clamp01(x);
        return 1f - (1f - x) * (1f - x);
    }
}

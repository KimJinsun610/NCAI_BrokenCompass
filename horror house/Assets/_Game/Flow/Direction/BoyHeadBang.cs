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

    /// <summary>엎드려 멈췄는지.</summary>
    public bool Stopped
    {
        get { return _stopped; }
    }

    /// <summary>머리 박기를 시작한다(이미 하고 있으면 그대로).</summary>
    public static BoyHeadBang Play(GameObject boy)
    {
        if (boy == null) return null;
        BoyHeadBang b = boy.GetComponent<BoyHeadBang>();
        if (b == null) b = boy.AddComponent<BoyHeadBang>();
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
        }
    }

    private float Cycle
    {
        get { return DownTime + HoldTime + UpTime + PauseTime; }
    }

    private void LateUpdate()
    {
        float t = Time.time - _start;
        if (_clips != null)
        {
            PlayClip(t);
            return;
        }

        int index = Mathf.FloorToInt(t / Cycle);
        float a;
        if (index >= Bangs)
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
        if (!_stopping && t >= MinSeconds && (t >= MaxSeconds || Time.time - _lastWatched > LookAwayGrace)) _stopping = true;

        float prev = _clipT;
        _clipT += dt * _speed;
        // 머리가 닿는 순간을 지났는지(되풀이 경계 포함).
        for (int k = 0; k < thuds.Length; k++)
        {
            float th = thuds[k];
            bool crossed = prev < th && _clipT >= th;
            if (!crossed && _clipT >= cycle && prev < th + cycle && _clipT - cycle >= th && prev - cycle < th) crossed = true;
            if (!crossed) continue;
            Thud(_played);
            _played++;
            if (_stopping)
            {
                _stopped = true;
                _holdAt = th;
                _clips.Bang.SampleAnimation(_clips.ClipRoot, _holdAt);
                return;
            }
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

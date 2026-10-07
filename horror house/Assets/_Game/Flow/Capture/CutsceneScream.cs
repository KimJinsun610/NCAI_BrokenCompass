using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

/// <summary>
/// 청각 사망 컷신에 비명을 얹는다(58차). 김진선님 컷신 프리팹·Timeline 에셋은 그대로 두고 <b>이번 인스턴스의 바인딩만</b> 바꾼다:
/// 소년 숨쉬기 트랙(<see cref="CutsceneScreamSO.IdleTrack"/>)의 바인딩을 비우고, 소년 애니메이터에 숨쉬기 → 비명 컨트롤러를 붙인 뒤,
/// 컷신 시간이 「Hit 소리 시작 − 비명 정점」에 닿으면 비명을 튼다 — 소년이 덮쳐 오는 정점에 Hit(비명·서브) 소리가 터진다.
/// 덮쳐 오는 동안 몸은 비명 동작을 따르되 얼굴(머리 뼈)은 늘 카메라를 본다 — 컷신 카메라가 얼굴 바로 아래에 있어 고개를 숙이면 정수리만 보였다.
/// 소년이 켜지는 순간(숨쉬기 트랙 시작)보다 앞서지는 않는다 — 그만큼 비명 앞부분(움츠림)을 건너뛰어 정점은 늘 Hit에 맞는다. 못 찾으면 아무것도 바꾸지 않는다(원래 컷신 그대로).
/// </summary>
[DisallowMultipleComponent]
public sealed class CutsceneScream : MonoBehaviour
{
    private PlayableDirector _director;
    private Animator _boy;
    private double _at;
    private bool _played;
    private float _startNormalized;
    private Vector3 _shift;
    private Vector3 _base;
    private Quaternion _baseRot;
    private Transform _head;
    private Vector3 _faceLocal;
    private float _turn;
    private float _shiftFor;
    private float _shiftT;

    /// <summary>비명을 틀 컷신 시각(초). 시험·검수용.</summary>
    public double ScreamAt
    {
        get { return _at; }
    }

    /// <summary>비명을 틀었는지.</summary>
    public bool Played
    {
        get { return _played; }
    }

    /// <summary>
    /// 컷신 인스턴스에 붙인다. <c>DeathCutscene.Play</c> 전에 불러야 한다(재생 그래프가 그때 바인딩으로 만들어진다). 붙였으면 true.
    /// </summary>
    public static bool Attach(GameObject cutscene)
    {
        if (cutscene == null) return false;
        CutsceneScreamSO so = Resources.Load<CutsceneScreamSO>(CutsceneScreamSO.ResourceName);
        PlayableDirector director = cutscene.GetComponentInChildren<PlayableDirector>(true);
        TimelineAsset timeline = director != null ? director.playableAsset as TimelineAsset : null;
        if (so == null || so.Controller == null || timeline == null) return false;

        TrackAsset idle = null;
        double idleStart = 0;
        double hit = double.MaxValue;
        foreach (TrackAsset t in timeline.GetOutputTracks())
        {
            if (t == null) continue;
            if (t.name == so.IdleTrack)
            {
                idle = t;
                foreach (TimelineClip c in t.GetClips()) idleStart = c.start;
            }
            else if (t.name.StartsWith(so.HitTrackPrefix, System.StringComparison.Ordinal))
            {
                foreach (TimelineClip c in t.GetClips()) if (c.start < hit) hit = c.start;
            }
        }

        Animator boy = idle != null ? director.GetGenericBinding(idle) as Animator : null;
        if (boy == null || hit == double.MaxValue) return false;

        director.SetGenericBinding(idle, null);
        boy.runtimeAnimatorController = so.Controller;
        boy.applyRootMotion = false;
        boy.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        CutsceneScream s = cutscene.AddComponent<CutsceneScream>();
        s._director = director;
        s._boy = boy;
        s._at = System.Math.Max(idleStart + 0.1, hit - so.PeakSeconds);
        float length = 0f;
        foreach (AnimationClip c in so.Controller.animationClips)
        {
            if (c != null && c.name.EndsWith("scream", System.StringComparison.OrdinalIgnoreCase)) length = c.length;
        }

        float skip = so.PeakSeconds - (float)(hit - s._at);   // 정점이 Hit보다 늦어지는 만큼 앞을 건너뛴다
        s._startNormalized = length > 0.01f ? Mathf.Clamp01(Mathf.Max(0f, skip) / length) : 0f;
        s._shift = so.HeadShift;
        s._turn = so.TurnDegrees;
        s._shiftFor = Mathf.Max(0.05f, so.PeakSeconds - Mathf.Max(0f, skip));   // 정점까지 머리 자리로 옮겨 간다
        return true;
    }

    private void LateUpdate()
    {
        if (!_played || _head == null || _faceLocal == Vector3.zero) return;
        Camera cam = Camera.main;
        if (cam == null) return;
        Vector3 want = cam.transform.position - _head.position;
        Vector3 now = _head.TransformDirection(_faceLocal);
        if (want.sqrMagnitude < 1e-6f) return;
        _head.rotation = Quaternion.FromToRotation(now, want) * _head.rotation;
    }

    private void Update()
    {
        if (_director == null || _boy == null) return;
        if (_played)
        {
            if (_shiftT >= 1f) return;
            _shiftT = Mathf.Min(1f, _shiftT + Time.deltaTime / _shiftFor);
            float k = Mathf.SmoothStep(0f, 1f, _shiftT);
            _boy.transform.localRotation = _baseRot * Quaternion.Euler(0f, _turn * k, 0f);
            _boy.transform.localPosition = _base + _baseRot * (_shift * k);
            return;
        }

        if (_director.time < _at || !_boy.isActiveAndEnabled) return;
        _played = true;
        _base = _boy.transform.localPosition;
        _baseRot = _boy.transform.localRotation;
        // 숨쉬기 때 얼굴이 카메라를 보던 방향을 머리 뼈 기준으로 적어 둔다 — 비명 중에도 얼굴이 카메라를 보게(LateUpdate).
        Camera cam = Camera.main;
        foreach (Transform t in _boy.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == "Head")
            {
                _head = t;
                break;
            }
        }

        if (_head != null && cam != null) _faceLocal = _head.InverseTransformDirection(cam.transform.position - _head.position).normalized;
        _boy.Play(CutsceneScreamSO.ScreamState, 0, _startNormalized);
    }
}

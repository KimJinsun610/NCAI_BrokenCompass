using System;
using System.Collections;
using NightDuty;
using UnityEngine;

/// <summary>
/// 피날레 몹 한 마리(배역표 <see cref="FinaleCastSO"/>의 한 칸을 세운 것). 피날레 흐름(11단계)·디버그 콘솔은 이것만 부른다 —
/// <b>팀원 프리팹이 들어와도 부르는 쪽 코드는 그대로</b>다.
/// <list type="bullet">
/// <item>구조: 루트 <c>finale.&lt;배역&gt;</c>(이 컴포넌트, 자리·방향) ▸ 아트(팀원 프리팹 또는 대역). 아트에 조준점·응시 상자·판정 ID를 붙인다(<see cref="StandInFactory.Dress"/>).</item>
/// <item>비트: <see cref="Play"/> / <see cref="PlayAndWait"/>. Animator에 그 이름의 트리거가 있으면 SetTrigger, 상태가 있으면 CrossFade, 둘 다 없으면 건너뜀.</item>
/// <item>한 번짜리 비트의 끝: <c>FinaleBeatDone</c> 이벤트 → 그 상태가 끝나거나 빠져나감 → 최대 시간, 먼저 오는 것.</item>
/// <item>두드림: <c>FinaleKnock</c> 이벤트마다 두드림 소리(<c>finale.knock</c>, 없으면 L5와 같은 <c>E.SuitMan.confront</c>). 아트가 두드림 이벤트를 안 가지면 일정 간격으로 소리만 낸다.</item>
/// <item>Animator는 화면 밖에서도 돌게 한다(<c>AlwaysAnimate</c>) — 창밖·등 뒤 몹이 안 보일 때 멈추면 비트 끝을 영영 못 잡는다.</item>
/// </list>
/// </summary>
[DisallowMultipleComponent]
public sealed class FinaleMob : MonoBehaviour
{
    /// <summary>두드림 소리 이름(없으면 <see cref="KnockFallbackSound"/>).</summary>
    public const string KnockSound = "finale.knock";

    /// <summary>도서관 L5 창 두드림과 같은 소리(기획서 「L5와 같은 소리」).</summary>
    public const string KnockFallbackSound = "E.SuitMan.confront";

    /// <summary>두드림 이벤트가 없는 아트·대역의 두드림 간격(초).</summary>
    public const float FallbackKnockInterval = 2.4f;

    private FinaleCastSO.Role _role;
    private Animator _animator;
    private Coroutine _run;
    private Coroutine _knockTimer;
    private int _serial;
    private bool _done = true;
    private bool _doneEvent;
    private bool _artKnocks;

    /// <summary>배역.</summary>
    public FinaleRole Role { get; private set; }

    /// <summary>팀원 프리팹이 섰는지(false = 대역).</summary>
    public bool HasArt { get; private set; }

    /// <summary>아트 오브젝트(프리팹 인스턴스 또는 대역).</summary>
    public GameObject Art { get; private set; }

    /// <summary>판정·카메라 조준점.</summary>
    public Transform Aim { get; private set; }

    /// <summary>아트의 Animator(없으면 null).</summary>
    public Animator Animator
    {
        get { return _animator; }
    }

    /// <summary>마지막으로 튼 비트.</summary>
    public FinaleBeat CurrentBeat { get; private set; }

    /// <summary>한 번짜리 비트가 진행 중인지.</summary>
    public bool IsBusy
    {
        get { return !_done; }
    }

    /// <summary>마지막 비트를 어떻게 틀고 끝냈는지(디버그·검수).</summary>
    public string LastLog { get; private set; }

    /// <summary>한 번짜리 비트가 끝났다(또는 반복 비트가 시작됐다).</summary>
    public event Action<FinaleMob, FinaleBeat> BeatFinished;

    /// <summary>두드렸다(소리는 이미 냈다).</summary>
    public event Action<FinaleMob> Knocked;

    /// <summary>아트가 <c>FinaleCue(문자열)</c> 이벤트를 보냈다 — 피날레 흐름이 연출 타이밍을 맞출 때 쓴다.</summary>
    public event Action<FinaleMob, string> Cued;

    // ── 세우기 ─────────────────────────────────────────────

    /// <summary>배역을 씬의 고정 자리에 세운다. 자리가 없으면 경고하고 null.</summary>
    public static FinaleMob Spawn(FinaleRole role)
    {
        FinaleCastSO.Role r = FinaleCastSO.Load().Get(role);
        StageAnchor anchor = StageAnchor.Find(r.anchorId);
        if (anchor == null)
        {
            Debug.LogWarning("[Finale] 씬에 「" + FinaleBeats.Label(role) + "」 자리("+ r.anchorId + ")가 없습니다.");
            return null;
        }

        FinaleMob mob = Spawn(role, anchor.transform.position, anchor.transform.rotation);
        if (mob != null && anchor.GazeProxy != null && r.gazeCollider) StandInFactory.ApplyGazeProxy(mob.Art, anchor.GazeProxy);
        return mob;
    }

    /// <summary>배역을 그 자리(발바닥)·방향에 세운다.</summary>
    public static FinaleMob Spawn(FinaleRole role, Vector3 floor, Quaternion rotation)
    {
        FinaleCastSO cast = FinaleCastSO.Load();
        FinaleCastSO.Role r = cast.Get(role);

        GameObject rootGo = new GameObject("finale." + role);
        rootGo.transform.SetPositionAndRotation(floor, rotation);
        FinaleMob mob = rootGo.AddComponent<FinaleMob>();
        mob.Role = role;
        mob._role = r;

        GameObject prefab = cast.PrefabFor(role);
        GameObject art;
        if (prefab != null)
        {
            art = Instantiate(prefab, rootGo.transform, false);
            art.name = prefab.name;
            mob.HasArt = true;
        }
        else
        {
            art = StandInFactory.Create(r.fallbackStandIn, floor, rotation, null);
            art.transform.SetParent(rootGo.transform, true);
            mob.HasArt = false;
        }

        art.transform.localPosition = Vector3.zero;
        art.transform.localRotation = Quaternion.identity;
        mob.Art = art;
        mob.Aim = StandInFactory.Dress(art, r.gazeCollider, r.judgeId);

        // 몸 콜라이더는 끈다(길을 막지 않게) — 응시 판정 상자(조준점의 것)만 남긴다. 응시가 필요 없는 배역은 그것도 끈다.
        foreach (Collider col in art.GetComponentsInChildren<Collider>(true))
        {
            col.enabled = r.gazeCollider && col.transform == mob.Aim;
        }
        mob.Wire();
        mob.LastLog = (mob.HasArt ? "아트 " + art.name : "대역 " + r.fallbackStandIn) + " @ " + r.anchorId;
        return mob;
    }

    /// <summary>거둔다.</summary>
    public void Despawn()
    {
        Destroy(gameObject);
    }

    private void Wire()
    {
        _animator = Art.GetComponentInChildren<Animator>(true);
        if (_animator != null && _animator.runtimeAnimatorController == null) _animator = null;

        foreach (Animator a in Art.GetComponentsInChildren<Animator>(true))
        {
            a.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            FinaleAnimEvents relay = a.GetComponent<FinaleAnimEvents>();
            if (relay == null) relay = a.gameObject.AddComponent<FinaleAnimEvents>();
            relay.Owner = this;
        }

        _artKnocks = false;
        if (_animator != null)
        {
            foreach (AnimationClip clip in _animator.runtimeAnimatorController.animationClips)
            {
                if (clip == null) continue;
                foreach (AnimationEvent e in clip.events)
                {
                    if (e.functionName == FinaleAnimEvents.KnockEvent) _artKnocks = true;
                }
            }
        }
    }

    // ── 비트 ─────────────────────────────────────────────

    /// <summary>그 비트를 Animator가 가지고 있는지(트리거 또는 상태).</summary>
    public bool Supports(FinaleBeat beat)
    {
        if (_animator == null || !_animator.isActiveAndEnabled) return false;
        string name = _role.Binding(beat).NameOr(beat);
        return HasTrigger(name) || _animator.HasState(0, Animator.StringToHash(name));
    }

    /// <summary>비트를 튼다(앞 비트는 끊는다). 기다리려면 <see cref="PlayAndWait"/>.</summary>
    public void Play(FinaleBeat beat)
    {
        if (_run != null) StopCoroutine(_run);
        _serial++;
        _done = false;
        _run = StartCoroutine(Run(beat, _serial));
    }

    /// <summary>비트를 틀고, 한 번짜리면 끝날 때까지(반복이면 시작될 때까지) 기다린다. 다른 비트가 끼어들면 거기서 끝난다.</summary>
    public IEnumerator PlayAndWait(FinaleBeat beat)
    {
        Play(beat);
        int mine = _serial;
        while (_serial == mine && !_done) yield return null;
    }

    private IEnumerator Run(FinaleBeat beat, int serial)
    {
        CurrentBeat = beat;
        FinaleCastSO.BeatBinding binding = _role.Binding(beat);
        string name = binding.NameOr(beat);
        _doneEvent = false;
        StopKnockTimer();
        if (!Art.activeSelf && beat != FinaleBeat.Vanish) Art.SetActive(true);

        string how = Drive(name);
        bool driven = how != null;

        if (beat == FinaleBeat.Knock && !(driven && _artKnocks))
        {
            _knockTimer = StartCoroutine(KnockTimer());
        }

        float waited = 0f;
        string ended = "반복 — 시작";
        if (!FinaleBeats.IsLoop(beat))
        {
            ended = "건너뜀(Animator에 없음)";
            if (driven)
            {
                ended = "최대 시간 " + binding.maxSeconds.ToString("0.0") + "초";
                bool entered = false;
                int hash = Animator.StringToHash(name);
                while (waited < binding.maxSeconds)
                {
                    yield return null;
                    waited += Time.deltaTime;
                    if (_doneEvent)
                    {
                        ended = "FinaleBeatDone 이벤트";
                        break;
                    }

                    if (_animator.IsInTransition(0)) continue;
                    AnimatorStateInfo st = _animator.GetCurrentAnimatorStateInfo(0);
                    bool inBeat = st.shortNameHash == hash || st.IsTag(name);
                    if (inBeat) entered = true;
                    if (entered && !inBeat)
                    {
                        ended = "상태를 빠져나감";
                        break;
                    }

                    if (inBeat && !st.loop && st.normalizedTime >= 1f)
                    {
                        ended = "상태 끝";
                        break;
                    }
                }
            }

            if (beat == FinaleBeat.Vanish) Art.SetActive(false);
        }

        LastLog = FinaleBeats.Label(Role) + " · " + beat + " — " + (driven ? how : "Animator에 없음") + " → " + ended + (FinaleBeats.IsLoop(beat) ? string.Empty : " (" + waited.ToString("0.00") + "초)");
        if (serial != _serial) yield break;
        _done = true;
        _run = null;
        Action<FinaleMob, FinaleBeat> handler = BeatFinished;
        if (handler != null) handler(this, beat);
    }

    /// <summary>Animator에 비트를 건다. 건 방법(문구) 또는 null(못 걸었음).</summary>
    private string Drive(string name)
    {
        if (_animator == null || !_animator.isActiveAndEnabled) return null;
        if (HasTrigger(name))
        {
            _animator.SetTrigger(name);
            return "트리거 " + name;
        }

        if (_animator.HasState(0, Animator.StringToHash(name)))
        {
            _animator.CrossFadeInFixedTime(name, 0.15f, 0);
            return "상태 " + name;
        }

        return null;
    }

    private bool HasTrigger(string name)
    {
        if (_animator == null) return false;
        AnimatorControllerParameter[] ps = _animator.parameters;
        for (int i = 0; i < ps.Length; i++)
        {
            if (ps[i].type == AnimatorControllerParameterType.Trigger && ps[i].name == name) return true;
        }

        return false;
    }

    private IEnumerator KnockTimer()
    {
        while (true)
        {
            OnKnockEvent();
            yield return new WaitForSeconds(FallbackKnockInterval);
        }
    }

    private void StopKnockTimer()
    {
        if (_knockTimer == null) return;
        StopCoroutine(_knockTimer);
        _knockTimer = null;
    }

    // ── 애니메이션 이벤트(FinaleAnimEvents가 넘겨줌) ─────────────────

    internal void OnBeatDoneEvent()
    {
        _doneEvent = true;
    }

    internal void OnKnockEvent()
    {
        Vector3 at = Aim != null ? Aim.position : transform.position + Vector3.up * 1.5f;
        string sound = KnockSound;
        float volume;
        if (Resources.Load<AudioClip>("Direction/" + sound) == null && DirectionSoundTableSO.FindExact(sound, out volume) == null) sound = KnockFallbackSound;
        DirectionStage.PlaySound(sound, at);
        Action<FinaleMob> handler = Knocked;
        if (handler != null) handler(this);
    }

    internal void OnCueEvent(string cue)
    {
        Action<FinaleMob, string> handler = Cued;
        if (handler != null) handler(this, cue ?? string.Empty);
    }
}

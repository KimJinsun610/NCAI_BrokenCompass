using System;
using NightDuty;
using UnityEngine;

/// <summary>대면 때 연출에 넘기는 맥락(최종 기획서 「DirectionCue 계약」).</summary>
public struct CueContext
{
    /// <summary>강도 1~5.</summary>
    public int Intensity;

    /// <summary>자리.</summary>
    public Vector3 Anchor;

    /// <summary>조우 ID.</summary>
    public string EncounterId;
}

/// <summary>
/// 대역·완성 몹 프리팹의 루트에 붙는 연출 계약(2026-10-01 7단계). <see cref="DirectionStage"/>가 단계를 알려 준다.
/// 아트가 만든 Visual(애니메이터 Pose/Intensity, 소켓의 소리)은 <see cref="PhaseChanged"/>를 구독해 반응한다 — 코드는 대역과 완성 자산을 구분하지 않는다.
/// </summary>
[DisallowMultipleComponent]
public sealed class DirectionCue : MonoBehaviour
{
    private static readonly int PoseId = Animator.StringToHash("Pose");
    private static readonly int IntensityId = Animator.StringToHash("Intensity");

    /// <summary>지금 단계.</summary>
    public DirectionPhase Phase { get; private set; }

    /// <summary>마지막 맥락.</summary>
    public CueContext Context { get; private set; }

    /// <summary>단계가 바뀌었다(전조 / 대면 / 창 닫힘 / 결과 / 중단).</summary>
    public event Action<DirectionCue, DirectionPhase> PhaseChanged;

    /// <summary>대면 시작.</summary>
    public void Play(CueContext context)
    {
        Context = context;
        Animator a = GetComponentInChildren<Animator>();
        if (a != null && a.runtimeAnimatorController != null)
        {
            a.SetFloat(IntensityId, context.Intensity);
            a.SetInteger(PoseId, 0);
        }

        SetPhase(DirectionPhase.Confront);
    }

    /// <summary>단계 알림.</summary>
    public void SetPhase(DirectionPhase phase)
    {
        Phase = phase;
        Action<DirectionCue, DirectionPhase> h = PhaseChanged;
        if (h == null) return;
        try { h(this, phase); }
        catch (Exception e) { Debug.LogException(e, this); }
    }

    /// <summary>onEnd 없이 즉시 멈춘다(04:00·붙잡힘).</summary>
    public void Abort()
    {
        SetPhase(DirectionPhase.Aborted);
    }

    /// <summary>재시작 초기화.</summary>
    public void ResetToRest()
    {
        Phase = DirectionPhase.None;
    }
}

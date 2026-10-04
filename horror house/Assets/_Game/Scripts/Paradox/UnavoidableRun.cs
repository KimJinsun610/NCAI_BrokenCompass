using System;

namespace NightDuty
{
    /// <summary>회피 불가 역설 진행 한 걸음.</summary>
    public enum UnavoidableStep
    {
        /// <summary>아무 일 없음.</summary>
        None = 0,

        /// <summary>상황이 걸렸다 — 문자를 보낸다(<see cref="UnavoidableDef.ChainEncounter"/>가 있으면 그 조우도 지금 건다).</summary>
        Staged = 1,

        /// <summary>진짜 「나가라」 쌍에서 점검이 남은 채 그 공간을 나갔다 — 당일 재입실 불가(남은 항목은 보고를 받지 않는다).</summary>
        Banned = 2
    }

    /// <summary>
    /// 그날 회피 불가 역설 하나의 진행(최종 기획서 「회피 불가 역설」). 순수 상태기계 — 문자·조우·재입실 금지는 <see cref="NightRun"/>이 한다.
    /// <list type="bullet">
    /// <item>나가라(S2·L2·T1): 그 공간 점검이 남은 채 단서가 울리면 문자. 진짜에서 점검이 남은 채 나가면 재입실 금지.</item>
    /// <item>항목(T2×T-1·K1/K2×K-1·C2×C-3): 그 항목이 남은 채 단서가 울리면 문자.</item>
    /// <item>겹침(S5×H3): 복도 끝 단서에 문자 + 발소리 조우를 바로.</item>
    /// <item>나서며(H4×T1): 물 내림 뒤 <see cref="ChainWindowSeconds"/> 안에 화장실을 나서 복도에 들어서면 문자 + 등 뒤 「Hey」.</item>
    /// </list>
    /// 재시작해도 되돌리지 않는다(받은 문자는 남고, 고른 대가도 남는다) — 역설과 같다.
    /// </summary>
    public sealed class UnavoidableRun
    {
        /// <summary>나서며: 물 내림 단서 뒤 이 시간(판정 초) 안에 복도로 나서야 둘째 조우를 건다.</summary>
        public const float ChainWindowSeconds = 15f;

        private readonly UnavoidableDef _def;
        private float _armedFor = -1f;

        /// <summary>그날 쌍으로 만든다(null이면 아무것도 하지 않는다).</summary>
        public UnavoidableRun(UnavoidableDef def)
        {
            _def = def;
        }

        /// <summary>그날 쌍. 없으면 null.</summary>
        public UnavoidableDef Def
        {
            get { return _def; }
        }

        /// <summary>상황이 걸렸는지(문자를 보냈는지).</summary>
        public bool Staged { get; private set; }

        /// <summary>재입실 금지된 공간. 없으면 None.</summary>
        public SpaceId Banned { get; private set; }

        /// <summary>진행 한 줄(디버그).</summary>
        public string Status
        {
            get
            {
                if (_def == null) return "없음";
                if (Banned != SpaceId.None) return _def + " · 걸림 · " + Banned + " 재입실 불가";
                if (Staged) return _def + " · 걸림";
                return _def + (_armedFor >= 0f ? " · 물 내림 뒤 나서기 대기" : " · 대기");
            }
        }

        /// <summary>
        /// 판정 구간 신호 하나. <paramref name="spacePending"/>: 그 공간에 아직 보고하지 않은 점검이 있는가. <paramref name="itemPending"/>: 그 항목을 아직 보고하지 않았는가.
        /// </summary>
        public UnavoidableStep Observe(in JudgeSignal s, SpaceId current, Func<SpaceId, bool> spacePending, Func<string, bool> itemPending)
        {
            if (_def == null) return UnavoidableStep.None;

            if (Staged)
            {
                // 진짜 「나가라」: 점검이 남은 채 그 공간을 나가면(=경고를 고름) 당일 재입실 불가.
                if (_def.Real && _def.Kind == UnavoidableKind.ExitBeforeInspection && Banned == SpaceId.None
                    && s.Kind == SignalKind.SpaceExited && SpaceIds.Canonical(s.Space) == _def.Space
                    && spacePending != null && spacePending(_def.Space))
                {
                    Banned = _def.Space;
                    return UnavoidableStep.Banned;
                }

                return UnavoidableStep.None;
            }

            switch (_def.Kind)
            {
                case UnavoidableKind.ExitBeforeInspection:
                    if (IsCue(s) && SpaceIds.Canonical(current) == _def.Space && spacePending != null && spacePending(_def.Space)) return Stage();
                    return UnavoidableStep.None;

                case UnavoidableKind.CueOnPendingItem:
                    if (IsCue(s) && itemPending != null && itemPending(_def.Item)) return Stage();
                    return UnavoidableStep.None;

                case UnavoidableKind.CrossCue:
                    return IsCue(s) ? Stage() : UnavoidableStep.None;

                case UnavoidableKind.ChainOnExit:
                    if (IsCue(s) && SpaceIds.Canonical(current) == _def.Space)
                    {
                        _armedFor = ChainWindowSeconds;
                        return UnavoidableStep.None;
                    }

                    if (_armedFor < 0f) return UnavoidableStep.None;
                    if (s.Kind == SignalKind.Tick)
                    {
                        _armedFor -= s.Value;
                        if (_armedFor < 0f) _armedFor = -1f;
                        return UnavoidableStep.None;
                    }

                    if (s.Kind == SignalKind.SpaceEntered && SpaceIds.Canonical(s.Space) == SpaceId.Corridor) return Stage();
                    return UnavoidableStep.None;
            }

            return UnavoidableStep.None;
        }

        /// <summary>디버그: 지금 건다.</summary>
        public UnavoidableStep ForceStage()
        {
            return _def == null || Staged ? UnavoidableStep.None : Stage();
        }

        /// <summary>진행 중이던 누적을 버린다(재시작) — 걸린 것·재입실 금지는 남는다.</summary>
        public void ResetEpisode()
        {
            _armedFor = -1f;
        }

        private UnavoidableStep Stage()
        {
            Staged = true;
            _armedFor = -1f;
            return UnavoidableStep.Staged;
        }

        private bool IsCue(in JudgeSignal s)
        {
            return s.Kind == SignalKind.CueStarted && ParadoxRun.CueId(s.TargetId) == _def.Cue;
        }
    }
}

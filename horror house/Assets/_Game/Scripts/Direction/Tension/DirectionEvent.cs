using UnityEngine;

namespace NightDuty
{
    /// <summary>연출 알림의 종류.</summary>
    public enum DirectionEventKind
    {
        /// <summary>조우의 단계가 바뀌었다(<see cref="DirectionEvent.Phase"/>).</summary>
        Encounter = 0,

        /// <summary>조우에 묶이지 않은 수칙의 단서가 시작됐다(분필·물 내림 등).</summary>
        RuleCue = 1,

        /// <summary>그 단서가 끝났다.</summary>
        RuleCueEnd = 2,

        /// <summary>가짜 놀람(덜컹이는 사물함 등). 판정·예산과 무관.</summary>
        FakeScare = 3,

        /// <summary>디렉터 메모(놓친 슬롯·헛예고·예산 거절 등). 디버그 로그용.</summary>
        Note = 4
    }

    /// <summary>조우 한 번의 단계(기획서 「트리거 → 전조 → 대면 → 대응 창 → 결과」 + 헛예고·중단).</summary>
    public enum DirectionPhase
    {
        /// <summary>해당 없음.</summary>
        None = 0,

        /// <summary>전조(1~8초, 몹이 나타날 방향의 3D 음).</summary>
        Foreshadow = 1,

        /// <summary>헛예고 — 전조만 울리고 아무것도 오지 않는다.</summary>
        FalseForeshadow = 2,

        /// <summary>대면 — 대역이 나타나고 대응 수칙의 단서가 판정 책에 들어간다. 대응 창도 함께 열린다.</summary>
        Confront = 3,

        /// <summary>대응 창이 닫혔다 — 끝 단서가 들어간다.</summary>
        WindowClose = 4,

        /// <summary>결과 — 대역·소등을 거둔다. 이 뒤 60~90초는 위협을 걸지 않는다.</summary>
        Result = 5,

        /// <summary>중단(붙잡힘·04:00·재시작) — 끝 단서 없이 즉시 거둔다.</summary>
        Aborted = 6,

        /// <summary>
        /// 61차(민: 「몹은 나타나 있되, 플레이어가 몹을 시야에 넣고 인지한 뒤에 연출이 시작되도록」): 대역만 세운다(움직임·점프스케어·대응 수칙 단서 없음).
        /// 연출 쪽이 플레이어가 본 것을 알리면(<c>NightRun.EncounterSeen</c>) 대면(<see cref="Confront"/>)이 온다. 오래 못 보면 중단(<see cref="Aborted"/>)으로 거두고 다시 기다린다.
        /// </summary>
        Present = 7
    }

    /// <summary>대역·소리를 놓을 자리.</summary>
    public enum CuePlacement
    {
        /// <summary>자리 없음(소리만·화면만).</summary>
        None = 0,

        /// <summary>플레이어 뒤 <see cref="EncounterScript.Distance"/>m.</summary>
        BehindPlayer = 1,

        /// <summary>플레이어 앞 <see cref="EncounterScript.Distance"/>m.</summary>
        AheadOfPlayer = 2,

        /// <summary>플레이어 머리 위 앞쪽 천장(천장 다리).</summary>
        CeilingAhead = 3
    }

    /// <summary>
    /// 긴장 디렉터가 연출 쪽(대역·소등·소리·디버그 로그)에 보내는 알림 한 건. <see cref="EventBus.DirectionEmitted"/>.
    /// 판정 단서는 디렉터가 코어 안에서 직접 판정 책에 넣으므로, 연출 쪽은 이것으로 <b>보이고 들리는 것만</b> 맞춘다.
    /// </summary>
    public readonly struct DirectionEvent
    {
        /// <summary>종류.</summary>
        public readonly DirectionEventKind Kind;

        /// <summary>조우 단계(<see cref="DirectionEventKind.Encounter"/>일 때).</summary>
        public readonly DirectionPhase Phase;

        /// <summary>조우 ID(<c>E.BoySeated</c>) 또는 수칙 ID(<c>C1</c>) 또는 가짜 놀람 ID(<c>fake.locker</c>).</summary>
        public readonly string SourceId;

        /// <summary>판정 단서 ID(없으면 빈 문자열).</summary>
        public readonly string CueId;

        /// <summary>공간.</summary>
        public readonly SpaceId Space;

        /// <summary>강도 1~5(조우), 수칙 단서·가짜 놀람은 0.</summary>
        public readonly int Intensity;

        /// <summary>대역·소리 자리(플레이어 자세로 계산, 모르면 0).</summary>
        public readonly Vector3 Point;

        /// <summary>단계 길이(초) — 전조 길이, 대응 창 길이 등.</summary>
        public readonly float Duration;

        /// <summary>메모(디버그 로그용 한 줄).</summary>
        public readonly string Text;

        /// <summary>만든다.</summary>
        public DirectionEvent(DirectionEventKind kind, DirectionPhase phase, string sourceId, string cueId, SpaceId space,
            int intensity, Vector3 point, float duration, string text)
        {
            Kind = kind;
            Phase = phase;
            SourceId = sourceId ?? string.Empty;
            CueId = cueId ?? string.Empty;
            Space = space;
            Intensity = intensity;
            Point = point;
            Duration = duration;
            Text = text ?? string.Empty;
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            string head = Kind == DirectionEventKind.Encounter ? Phase.ToString() : Kind.ToString();
            return head + " " + SourceId + (CueId.Length > 0 ? " [" + CueId + "]" : string.Empty)
                   + (Duration > 0f ? " " + Duration.ToString("0.0") + "s" : string.Empty)
                   + (Text.Length > 0 ? " — " + Text : string.Empty);
        }
    }
}

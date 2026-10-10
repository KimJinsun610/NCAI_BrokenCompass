using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>피날레 배역(최종 기획서 「5일차 피날레」). 팀원이 만든 몹 프리팹이 들어갈 자리다.</summary>
public enum FinaleRole
{
    /// <summary>창밖의 웃는 정장 남자 — 04:00부터 경비실 창을 두드린다. 창을 2초 응시하면 「봤다」(L5와 같은 기준).</summary>
    WindowMan = 0,

    /// <summary>꺼진 CRT에 비친 내 자리에 선 무언가 — 「봤다」 결말(「근무 교대. 수고하셨습니다.」).</summary>
    SeatFigure = 1,
}

/// <summary>
/// 피날레 몹 비트. 프리팹 Animator에 <b>이 이름의 트리거 또는 상태</b>가 있으면 그 비트를 그것으로 튼다(이름은 배역표에서 바꿀 수 있다).
/// 없는 비트는 건너뛴다 — 애니메이션이 하나도 없어도 몹은 선다.
/// </summary>
public enum FinaleBeat
{
    /// <summary>기본 자세(반복). 처음 들어가 있는 상태 — 트리거가 없어도 된다.</summary>
    Idle = 0,

    /// <summary>나타남(한 번).</summary>
    Appear = 1,

    /// <summary>창 두드림(반복). 손이 유리에 닿는 프레임에 애니메이션 이벤트 <c>FinaleKnock</c> → 두드림 소리. 이벤트가 없으면 일정 간격으로 소리만 낸다.</summary>
    Knock = 2,

    /// <summary>플레이어가 창을 2초 응시한 순간의 반응(한 번).</summary>
    Seen = 3,

    /// <summary>사라짐(한 번). 끝나면 몹을 숨긴다.</summary>
    Vanish = 4,

    /// <summary>내 자리 뒤에 서 있음(반복).</summary>
    Stand = 5,
}

/// <summary>비트의 성질(반복인지, 기본 최대 시간).</summary>
public static class FinaleBeats
{
    /// <summary>반복 비트인지(들어가면 곧바로 「시작됨」으로 본다). 한 번짜리는 끝날 때까지 기다린다.</summary>
    public static bool IsLoop(FinaleBeat beat)
    {
        return beat == FinaleBeat.Idle || beat == FinaleBeat.Knock || beat == FinaleBeat.Stand;
    }

    /// <summary>배역이 쓰는 비트(검사 메뉴·디버그 콘솔이 보여 준다).</summary>
    public static FinaleBeat[] For(FinaleRole role)
    {
        return role == FinaleRole.WindowMan
            ? new[] { FinaleBeat.Idle, FinaleBeat.Appear, FinaleBeat.Knock, FinaleBeat.Seen, FinaleBeat.Vanish }
            : new[] { FinaleBeat.Idle, FinaleBeat.Appear, FinaleBeat.Stand, FinaleBeat.Vanish };
    }

    /// <summary>화면용 이름.</summary>
    public static string Label(FinaleBeat beat)
    {
        switch (beat)
        {
            case FinaleBeat.Idle: return "기본";
            case FinaleBeat.Appear: return "나타남";
            case FinaleBeat.Knock: return "두드림";
            case FinaleBeat.Seen: return "봤다 반응";
            case FinaleBeat.Vanish: return "사라짐";
            default: return "서 있음";
        }
    }

    /// <summary>화면용 배역 이름.</summary>
    public static string Label(FinaleRole role)
    {
        return role == FinaleRole.WindowMan ? "창밖 정장 남자" : "내 자리 무언가";
    }
}

/// <summary>
/// 피날레 배역표(<c>Resources/FinaleCast.asset</c>). <b>팀원 몹을 끼우는 곳은 여기 한 칸이다</b> — 배역의 <see cref="Role.prefab"/>에 프리팹을 넣으면 끝.
/// 코드·씬은 손대지 않는다. 비어 있으면 대역(<see cref="Role.fallbackStandIn"/>)이 선다.
/// <para>
/// <b>프리팹 규약</b>(검사: 메뉴 「야간근무 ▸ 연출 ▸ 피날레 몹 검사」):
/// 피벗 = 발바닥 중앙, 앞 = +Z(창 안·플레이어 쪽), 키 1.2~2.6m.
/// Animator가 있으면 트리거 또는 상태 이름 <c>Idle·Appear·Knock·Seen·Vanish·Stand</c>(<see cref="FinaleBeat"/>)로 비트를 튼다 — 없는 비트는 건너뛴다.
/// 한 번짜리 비트(Appear·Seen·Vanish)는 마지막 프레임에 애니메이션 이벤트 <c>FinaleBeatDone</c>를 두면 그 순간 다음으로 넘어간다
/// (없으면 상태가 끝나거나 빠져나갈 때, 그것도 없으면 <see cref="BeatBinding.maxSeconds"/>).
/// 두드림은 손이 닿는 프레임에 <c>FinaleKnock</c>, 그 밖의 타이밍은 <c>FinaleCue(문자열)</c>.
/// 이벤트를 받는 스크립트는 런타임에 붙으므로 프리팹에 스크립트를 넣을 필요가 없다. 머리에 <c>Aim</c> 자식을 두면 응시 판정·카메라가 그 점을 쓴다(없으면 머리 높이에 만든다).
/// 단단한 콜라이더는 넣지 않는다(응시 상자는 이쪽이 붙인다).
/// </para>
/// </summary>
[CreateAssetMenu(menuName = "야간근무/피날레 배역표", fileName = "FinaleCast")]
public sealed class FinaleCastSO : ScriptableObject
{
    /// <summary>Resources 이름.</summary>
    public const string ResourceName = "FinaleCast";

    /// <summary>비트 한 줄의 덮어쓰기.</summary>
    [Serializable]
    public sealed class BeatBinding
    {
        public FinaleBeat beat;

        [Tooltip("Animator 트리거 또는 상태 이름. 비우면 비트 이름 그대로(Appear·Knock…).")]
        public string animatorName = string.Empty;

        [Tooltip("한 번짜리 비트를 끝났다고 볼 최대 시간(초). FinaleBeatDone 이벤트나 상태 끝이 먼저 오면 그때 끝난다.")]
        [Min(0.1f)] public float maxSeconds = 8f;

        /// <summary>Animator에서 찾을 이름.</summary>
        public string NameOr(FinaleBeat b)
        {
            return string.IsNullOrEmpty(animatorName) ? b.ToString() : animatorName;
        }
    }

    /// <summary>배역 한 칸.</summary>
    [Serializable]
    public sealed class Role
    {
        public FinaleRole role;

        [Tooltip("팀원이 만든 몹 프리팹(모델 + Animator). 여기에 넣으면 끝 — 비우면 대역이 선다. 「내 자리 무언가」를 비우면 창밖 남자 프리팹을 쓴다.")]
        public GameObject prefab;

        [Tooltip("프리팹이 비었을 때 세울 대역 ID(Resources/StandIns).")]
        public string fallbackStandIn = "mob.finale";

        [Tooltip("씬의 고정 자리(StageAnchor) ID.")]
        public string anchorId = string.Empty;

        [Tooltip("응시 판정 ID(조준점 Aim의 JudgeTarget). 비우면 판정 없음.")]
        public string judgeId = string.Empty;

        [Tooltip("조준점에 몸을 감싸는 단단한 응시 상자를 둘지(창밖 남자 = 「봤다」 판정).")]
        public bool gazeCollider;

        [Tooltip("비트별 Animator 이름·최대 시간 덮어쓰기. 없는 비트는 기본값(비트 이름, 8초).")]
        public List<BeatBinding> beats = new List<BeatBinding>();

        /// <summary>그 비트의 설정(없으면 기본값).</summary>
        public BeatBinding Binding(FinaleBeat beat)
        {
            for (int i = 0; i < beats.Count; i++)
            {
                if (beats[i] != null && beats[i].beat == beat) return beats[i];
            }

            return new BeatBinding { beat = beat };
        }
    }

    [SerializeField] private List<Role> roles = new List<Role>();

    [Header("피날레 화면 문구(검은 화면 위 글자)")]
    [Tooltip("「근무 시간이 종료되었습니다.」 · 퇴근 안내 · 「근무 종료, 철거가 예정대로 진행됩니다.」의 글꼴. 비우면 씬의 한글 글꼴을 빌린다.")]
    [SerializeField] private TMPro.TMP_FontAsset screenFont;
    [Tooltip("화면 문구 글자 크기(1920×1080 기준).")]
    [SerializeField, Min(8f)] private float screenFontSize = 36f;

    [Header("엔딩 1 크레딧")]
    [Tooltip("「봤다」 결말(엔딩 1) 마지막 문구 뒤, 메인으로 가기 전에 띄울 엔딩 크레딧(HUD_EndingCredits_Design). 비우면 바로 메인.")]
    [SerializeField] private GameObject creditsPrefab;

    /// <summary>엔딩 1 크레딧 프리팹(없으면 null).</summary>
    public GameObject CreditsPrefab
    {
        get { return creditsPrefab; }
    }

    /// <summary>피날레 화면 문구 글꼴(없으면 null).</summary>
    public TMPro.TMP_FontAsset ScreenFont
    {
        get { return screenFont; }
    }

    /// <summary>피날레 화면 문구 글자 크기.</summary>
    public float ScreenFontSize
    {
        get { return screenFontSize; }
    }

    /// <summary>배역 목록(편집용).</summary>
    public List<Role> Roles
    {
        get { return roles; }
    }

    /// <summary>Resources에서 읽는다. 없으면 기본값만 든 임시 표.</summary>
    public static FinaleCastSO Load()
    {
        FinaleCastSO cast = Resources.Load<FinaleCastSO>(ResourceName);
        if (cast != null) return cast;

        cast = CreateInstance<FinaleCastSO>();
        cast.FillDefaults();
        return cast;
    }

    /// <summary>그 배역 칸. 표에 없으면 기본 칸.</summary>
    public Role Get(FinaleRole role)
    {
        for (int i = 0; i < roles.Count; i++)
        {
            if (roles[i] != null && roles[i].role == role) return roles[i];
        }

        return Default(role);
    }

    /// <summary>그 배역에 쓸 프리팹(내 자리 무언가가 비면 창밖 남자 것). 없으면 null = 대역.</summary>
    public GameObject PrefabFor(FinaleRole role)
    {
        GameObject p = Get(role).prefab;
        if (p == null && role == FinaleRole.SeatFigure) p = Get(FinaleRole.WindowMan).prefab;
        return p;
    }

    /// <summary>기본 칸.</summary>
    public static Role Default(FinaleRole role)
    {
        if (role == FinaleRole.WindowMan)
        {
            return new Role
            {
                role = role,
                fallbackStandIn = "mob.finale",
                anchorId = NightDuty.StageAnchors.FinaleWindow,
                judgeId = "rule.K4.window",
                gazeCollider = true,
            };
        }

        return new Role
        {
            role = role,
            fallbackStandIn = "mob.finale",
            anchorId = NightDuty.StageAnchors.FinaleSeat,
            judgeId = string.Empty,
            gazeCollider = false,
        };
    }

    /// <summary>없는 배역 칸을 기본값으로 채운다.</summary>
    public void FillDefaults()
    {
        foreach (FinaleRole r in new[] { FinaleRole.WindowMan, FinaleRole.SeatFigure })
        {
            bool found = false;
            for (int i = 0; i < roles.Count; i++)
            {
                if (roles[i] != null && roles[i].role == r) found = true;
            }

            if (!found) roles.Add(Default(r));
        }
    }
}

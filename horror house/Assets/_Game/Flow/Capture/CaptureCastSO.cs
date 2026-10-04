using System;
using System.Collections.Generic;
using NightDuty;
using UnityEngine;

/// <summary>
/// 붙잡힘 연출표(<c>Resources/CaptureCast.asset</c>). <b>팀원이 만든 축별 붙잡힘 연출을 끼우는 곳은 축마다 한 칸</b> — <see cref="Entry.prefab"/>에 넣으면 끝.
/// 비어 있으면 기본 연출(어둠 속 대역 얼굴: 청각 소년 · 조도 해부 모형 · 배치 사람 나무)이 돈다. 앞뒤 틀(소리 끊김·암전·재시작 카드·출근 자리)은 그대로다.
/// <para>
/// <b>연출 프리팹 규약</b>(검사: 메뉴 「야간근무 ▸ 연출 ▸ 붙잡힘 연출 검사」, 시험: F3 콘솔 「조우」 탭 [붙잡힘 연출 미리 보기]):
/// 원점 = 플레이어 발밑, +Z = 플레이어가 보는 방향(돌아보기가 켜져 있으면 돌아본 뒤의 방향) — 몹은 그 기준으로 프리팹 안에 놓는다.
/// 암전이 걷히는 순간 프리팹이 켜지며 Animator들이 기본 상태부터 돈다(트리거 불필요).
/// 자식 <c>CameraMount</c>가 있으면 그동안 플레이어 카메라가 매 프레임 그 위치·회전을 따른다(돌아보기·끌려가기 같은 카메라 움직임을 애니메이션으로).
/// <c>CameraMount</c>에 (꺼 둔) Camera가 있으면 그 시야각도 따른다. 없으면 카메라는 플레이어 눈에 그대로 있다.
/// 끝 = 애니메이션 이벤트 <c>CaptureDone</c>(권장) → 없으면 모든 Animator가 반복 아닌 상태를 끝냈을 때 → <see cref="Entry.maxSeconds"/>.
/// 그 밖의 이벤트: <c>CaptureSound(이름)</c> 연출 소리 · <c>CaptureFlashlight(0/1)</c> 손전등 · <c>CaptureCue(문자열)</c>.
/// 이벤트 받이는 런타임에 붙으므로 프리팹에 스크립트를 넣지 않는다. 콜라이더는 세울 때 끄고, 프리팹 안 AudioSource는 정적 중에도 들린다.
/// </para>
/// </summary>
[CreateAssetMenu(menuName = "야간근무/붙잡힘 연출표", fileName = "CaptureCast")]
public sealed class CaptureCastSO : ScriptableObject
{
    /// <summary>Resources 이름.</summary>
    public const string ResourceName = "CaptureCast";

    /// <summary>축 한 칸.</summary>
    [Serializable]
    public sealed class Entry
    {
        public FearAxis axis;

        [Tooltip("팀원이 만든 붙잡힘 연출 프리팹(몹 + Animator, 필요하면 CameraMount). 여기에 넣으면 끝 — 비우면 기본 연출(어둠 속 대역 얼굴).")]
        public GameObject prefab;

        [Header("앞 틀")]
        [Tooltip("소리가 끊긴 뒤 암전 정적(초).")]
        [Min(0f)] public float silenceSeconds = 0.5f;

        [Tooltip("정적 동안 플레이어를 180° 돌려 놓는다(「돌아보면」). 연출 프리팹의 +Z도 돌아본 뒤 방향.")]
        public bool turnAround = true;

        [Tooltip("붙잡히는 순간 손전등을 끈다(조도). 기본 연출은 얼굴이 나올 때 다시 켜고, 연출 프리팹은 CaptureFlashlight 이벤트로 켠다.")]
        public bool flashlightOff;

        [Tooltip("연출 동안 세상을 그리지 않고 연출 프리팹(얼굴)만 검은 배경에 그린다 — 좁은 방에서 벽·소품이 가리지 않게. 끄면 실제 방이 함께 보인다.")]
        public bool darkWorld = true;

        [Tooltip("암전이 걷히는 순간 낼 소리 이름(Resources/Direction 또는 연출 소리 표). 없으면 조용히 넘어간다.")]
        public string revealSound = string.Empty;

        [Header("연출 프리팹")]
        [Tooltip("끝을 못 잡았을 때 최대 길이(초).")]
        [Min(0.1f)] public float maxSeconds = 8f;

        [Tooltip("같은 축으로 두 번째 붙잡힘부터 Animator 재생 속도 배수(짧게). 세 번째부터는 아무 키로 건너뛴다.")]
        [Min(1f)] public float repeatSpeed = 2f;

        [Header("기본 연출(프리팹이 빌 때)")]
        [Tooltip("얼굴 대역 ID(Resources/StandIns).")]
        public string fallbackFace = "mob.boy";

        [Tooltip("얼굴이 보이는 시간(초). 두 번째부터는 절반.")]
        [Min(0.05f)] public float faceSeconds = 0.3f;

        [Tooltip("얼굴이 보이는 동안 카메라가 앞으로 끌려 들어가는 거리(m) — 배치 「가지 사이로」.")]
        [Min(0f)] public float pullDistance;
    }

    [SerializeField] private List<Entry> entries = new List<Entry>();

    /// <summary>칸 목록(편집용).</summary>
    public List<Entry> Entries
    {
        get { return entries; }
    }

    /// <summary>Resources에서 읽는다. 없으면 기본값만 든 임시 표.</summary>
    public static CaptureCastSO Load()
    {
        CaptureCastSO cast = Resources.Load<CaptureCastSO>(ResourceName);
        if (cast != null) return cast;

        cast = CreateInstance<CaptureCastSO>();
        cast.FillDefaults();
        return cast;
    }

    /// <summary>그 축의 칸. 표에 없으면 기본 칸.</summary>
    public Entry Get(FearAxis axis)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i] != null && entries[i].axis == axis) return entries[i];
        }

        return Default(axis);
    }

    /// <summary>축별 기본 칸(최종 기획서 「붙잡힘과 재시작」).</summary>
    public static Entry Default(FearAxis axis)
    {
        switch (axis)
        {
            case FearAxis.Auditory:
                // 모든 소리가 끊기고, 돌아보면 소년의 얼굴.
                return new Entry { axis = axis, fallbackFace = "mob.boy", turnAround = true, revealSound = "capture.auditory" };
            case FearAxis.Illuminance:
                // 손전등이 꺼져 완전한 암흑, 다시 켜지는 순간 해부 모형의 얼굴.
                return new Entry { axis = axis, fallbackFace = "mob.dummy.stand", turnAround = false, flashlightOff = true, revealSound = "capture.illuminance" };
            default:
                // 돌아보면 사람 나무, 화면이 가지 사이로 끌려 들어간다.
                return new Entry { axis = axis, fallbackFace = "mob.tree", turnAround = true, pullDistance = 0.3f, revealSound = "capture.layout" };
        }
    }

    /// <summary>없는 축 칸을 기본값으로 채운다.</summary>
    public void FillDefaults()
    {
        foreach (FearAxis a in new[] { FearAxis.Auditory, FearAxis.Illuminance, FearAxis.Layout })
        {
            bool found = false;
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i] != null && entries[i].axis == a) found = true;
            }

            if (!found) entries.Add(Default(a));
        }
    }

    /// <summary>화면용 축 이름.</summary>
    public static string Label(FearAxis axis)
    {
        switch (axis)
        {
            case FearAxis.Auditory: return "청각";
            case FearAxis.Illuminance: return "조도";
            case FearAxis.Layout: return "배치";
            default: return axis.ToString();
        }
    }
}

using System;
using NightDuty;
using UnityEngine;

/// <summary>
/// 조도축 연출 구간별 화면 톤(2026-10-03, 최종 기획서 「구간별 맵·감각 변화 — 조도」·「조명 — 씬 확인 결과」).
/// <c>Resources/IlluminanceTone</c>. 없으면 코드 기본값. 기획·아트가 인스펙터에서 바꾼다 — <see cref="IlluminanceMap"/>은 수치를 다시 쓰지 않는다.
/// <list type="bullet">
/// <item>0 = 6500K 백색(실내 보정 그대로) · 1 = 호박색이 돌기 시작 · 2 = 더 짙은 호박색 + 비네팅 · 3 = 채도 감소 · 4 = 붉은 잔광.</item>
/// <item>색온도는 라이트가 아니라 Volume <b>White Balance</b>(-100~100, +가 따뜻함), 채도는 <b>Color Adjustments</b>.</item>
/// <item>채도·컬러 필터·비네팅은 실내 보정(<c>PP_Night_Interior</c>) 값에 <b>얹는다</b> — 채도는 더하기, 필터는 곱하기, 비네팅은 큰 쪽.</item>
/// </list>
/// </summary>
[CreateAssetMenu(menuName = "야간근무/조도 구간 톤", fileName = "IlluminanceTone")]
public sealed class IlluminanceToneSO : ScriptableObject
{
    /// <summary>한 구간의 톤.</summary>
    [Serializable]
    public struct Row
    {
        [Tooltip("White Balance 색온도(-100~100). +가 호박색 쪽.")]
        [Range(-100f, 100f)] public float temperature;

        [Tooltip("White Balance 틴트(-100~100). +가 자홍 쪽.")]
        [Range(-100f, 100f)] public float tint;

        [Tooltip("채도 더하기(-100~100). 실내 보정 값에 더한다.")]
        [Range(-100f, 100f)] public float saturation;

        [Tooltip("컬러 필터(곱하기). 흰색이면 그대로.")]
        public Color colorFilter;

        [Tooltip("비네팅 세기. 실내 보정 값보다 클 때만 쓴다.")]
        [Range(0f, 1f)] public float vignette;

        [Tooltip("비네팅 색.")]
        public Color vignetteColor;
    }

    [Tooltip("구간 0~4. 정확히 5줄.")]
    public Row[] rows = Defaults();

    [Tooltip("구간이 바뀔 때 톤이 옮겨 가는 시간(초). 맵이 「서서히」 변한다.")]
    [Min(0f)] public float bandFadeSeconds = 4f;

    [Tooltip("공간을 옮길 때 톤이 바뀌는 시간(초).")]
    [Min(0.05f)] public float spaceBlendSeconds = 0.8f;

    [Tooltip("화장실 조명이 붉어지는 구간(기획서: 2).")]
    public Band toiletRedFrom = Band.Band2;

    [Tooltip("붉어진 화장실 조명 색.")]
    public Color toiletRed = new Color(1f, 0.32f, 0.26f, 1f);

    [Tooltip("붉어진 화장실 조명 세기 배수.")]
    [Range(0f, 2f)] public float toiletRedIntensity = 0.8f;

    [Tooltip("과학실 앞 복도 조명이 꺼지는 구간(기획서: 3).")]
    public Band scienceFrontOffFrom = Band.Band3;

    /// <summary>그 구간의 톤. 표가 짧으면 마지막 줄.</summary>
    public Row RowFor(Band band)
    {
        Row[] r = rows != null && rows.Length > 0 ? rows : Defaults();
        int i = Mathf.Clamp((int)band, 0, r.Length - 1);
        return r[i];
    }

    /// <summary>Resources에서 읽는다. 없으면 기본값 인스턴스(저장하지 않음).</summary>
    public static IlluminanceToneSO Load()
    {
        IlluminanceToneSO so = Resources.Load<IlluminanceToneSO>("IlluminanceTone");
        return so != null ? so : CreateInstance<IlluminanceToneSO>();
    }

    /// <summary>기본 톤 5줄(2026-10-03 첫 값 — 플레이로 맞출 것).</summary>
    public static Row[] Defaults()
    {
        return new[]
        {
            new Row { temperature = 0f, tint = 0f, saturation = 0f, colorFilter = Color.white, vignette = 0f, vignetteColor = Color.black },
            new Row { temperature = 16f, tint = 0f, saturation = -6f, colorFilter = new Color(1f, 0.97f, 0.9f), vignette = 0.12f, vignetteColor = Color.black },
            new Row { temperature = 32f, tint = 4f, saturation = -14f, colorFilter = new Color(1f, 0.92f, 0.8f), vignette = 0.32f, vignetteColor = Color.black },
            new Row { temperature = 30f, tint = 4f, saturation = -45f, colorFilter = new Color(0.98f, 0.92f, 0.84f), vignette = 0.36f, vignetteColor = Color.black },
            new Row { temperature = 24f, tint = 14f, saturation = -60f, colorFilter = new Color(1f, 0.58f, 0.52f), vignette = 0.42f, vignetteColor = new Color(0.16f, 0f, 0f) },
        };
    }
}

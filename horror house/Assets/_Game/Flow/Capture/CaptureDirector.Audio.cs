using System.Collections;
using System.Collections.Generic;
using NightDuty;
using UnityEngine;

/// <summary>
/// 붙잡힘 연출기의 소리 부분 — 플레이어 긴장의 클라이맥스(2026-10-04 클라이맥스_VARCO생성본 CLX + 잡힘연출 CAP).
/// 소리는 전부 연출 소리 표(<see cref="DirectionSoundTableSO"/>)의 정확한 키로 찾고, 없는 키는 조용히 넘어간다. 리스너가 멈춘 동안에도 들리게 2D·<c>ignoreListenerPause</c>.
/// <b>충격음·상승음은 그 판의 가장 큰 지점(<see cref="DirectionSoundTableSO.Entry.hits"/>)이 원하는 순간에 오게</b> 늦추거나 앞을 잘라 튼다.
/// <list type="number">
/// <item><b>흔들림 구간</b>(<see cref="riseSeconds"/> 2.5초, 같은 축 두 번째부터 <see cref="riseRepeatSeconds"/> 1.2초, 세 번째부터 아무 키로 건너뜀) —
/// 조작이 멈추고 화면이 점점 세게 떨린다. 축별 상승음(<c>capture.rise.&lt;축&gt;</c>, 청각은 심장 <c>capture.rise2.auditory</c>도)과 숨 들이켬(<c>capture.breath</c>)이 끊김 순간에 꼭대기에 닿고 거기서 끊긴다.</item>
/// <item><b>끊김</b>: <c>capture.cut</c>(「뚝」) · 조도는 손전등 딸깍(<c>capture.cut.illuminance</c>). 어둠: 조도 웅—·숨 막힘.</item>
/// <item><b>정적</b>: 빨려 드는 소리(<c>capture.swell</c>)와 얼굴 직전 소리(<c>capture.pre.&lt;축&gt;</c> — 청각 거대한 쿵 · 조도 관절 꺾임)가 얼굴 순간에 꼭대기.</item>
/// <item><b>얼굴</b>: 저음 <c>capture.face.low</c> + 중음 <c>capture.face.mid.&lt;축&gt;</c> + 고음 <c>capture.face.high.&lt;축&gt;</c> + 목소리 <c>capture.face.voice.&lt;축&gt;</c>
/// (각 판의 꼭대기부터 얼굴 프레임에) + 축별 등장 소리(장면 쪽, 3D) + 헉. 태블릿을 든 채면 <c>capture.tablet</c>.</item>
/// <item><b>암전</b>: 어둠 소리 끊고 낮은 웅—(<c>capture.tail</c>) · 청각은 이명. <b>카드</b>: <c>capture.precard.&lt;축&gt;</c> · YOU DIED · 종이 카드. <b>다시 근무</b>: 남은 꼬리를 줄이고 모니터 웅— → 숨.</item>
/// </list>
/// </summary>
public sealed partial class CaptureDirector
{
    [Header("흔들림 구간(붙잡히기 직전, 초)")]
    [Tooltip("그 축으로 처음 붙잡힐 때. 0이면 흔들림 없이 바로 끊긴다.")]
    [SerializeField, Min(0f)] private float riseSeconds = 2.5f;

    [Tooltip("같은 축으로 두 번째부터.")]
    [SerializeField, Min(0f)] private float riseRepeatSeconds = 1.2f;

    [Tooltip("흔들림 끝의 카메라 흔들림 세기(도).")]
    [SerializeField, Min(0f)] private float shakeDegrees = 2.2f;

    private readonly List<AudioSource> _sceneSfx = new List<AudioSource>();
    private readonly List<AudioSource> _riseSfx = new List<AudioSource>();
    private readonly List<AudioSource> _faceSfx = new List<AudioSource>();
    private readonly List<AudioSource> _tailSfx = new List<AudioSource>();

    /// <summary>축 이름(소리 키 뒤에 붙는 것).</summary>
    public static string AxisKey(FearAxis axis)
    {
        switch (axis)
        {
            case FearAxis.Auditory: return "auditory";
            case FearAxis.Illuminance: return "illuminance";
            default: return "layout";
        }
    }

    /// <summary>
    /// 붙잡힘 소리 하나. <paramref name="hitIn"/>(0 이상)을 주면 그 판의 가장 큰 지점이 지금부터 그 초 뒤에 오도록 늦추거나 앞을 자른다.
    /// 아니면 <paramref name="delay"/>초 뒤 처음부터. <paramref name="into"/>에 넣어 두면 나중에 함께 줄여 끊는다. 클립이 없으면 null.
    /// </summary>
    private AudioSource CaptureSfx(string key, float delay = 0f, float hitIn = -1f, List<AudioSource> into = null)
    {
        float volume;
        float hitAt;
        AudioClip clip = DirectionSoundTableSO.FindExact(key, out volume, out hitAt);
        if (clip == null) return null;

        GameObject go = new GameObject("capture sfx " + key);
        go.transform.SetParent(transform, false);
        AudioSource s = go.AddComponent<AudioSource>();
        s.playOnAwake = false;
        s.clip = clip;
        s.volume = volume;
        s.spatialBlend = 0f;
        s.priority = 4;
        s.ignoreListenerPause = true;

        float skip = 0f;
        if (hitIn >= 0f)
        {
            float start = hitIn - hitAt;
            if (start >= 0f) delay = start;
            else skip = Mathf.Min(-start, Mathf.Max(0f, clip.length - 0.05f));
        }

        if (skip > 0f) s.time = skip;
        if (delay > 0f) s.PlayDelayed(delay);
        else s.Play();
        Destroy(go, delay + clip.length - skip + 0.3f);
        if (into != null) into.Add(s);
        if (DirectionStage.Verbose) Debug.Log("[Capture] 소리 " + key + " ← " + clip.name + (hitIn >= 0f ? " (꼭대기 " + hitIn.ToString("0.00") + "초 뒤)" : string.Empty));
        return s;
    }

    /// <summary>목록의 소리를 <paramref name="seconds"/>초에 걸쳐 줄이고 지운다(뚝 끊기는 잡음을 막는다).</summary>
    private void FadeOut(List<AudioSource> list, float seconds)
    {
        if (list.Count == 0) return;
        List<AudioSource> copy = new List<AudioSource>(list);
        list.Clear();
        StartCoroutine(FadeOutRun(copy, seconds));
    }

    private static IEnumerator FadeOutRun(List<AudioSource> list, float seconds)
    {
        float[] from = new float[list.Count];
        for (int i = 0; i < list.Count; i++) from[i] = list[i] != null ? list[i].volume : 0f;
        float t = 0f;
        while (t < seconds)
        {
            t += Time.unscaledDeltaTime;
            float k = 1f - Mathf.Clamp01(t / Mathf.Max(0.001f, seconds));
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null) list[i].volume = from[i] * k;
            }

            yield return null;
        }

        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] != null) Destroy(list[i].gameObject);
        }
    }

    /// <summary>
    /// ⓪ 흔들림 구간 — 붙잡힌 것은 이미 정해졌고, 소리가 끊기기 전까지 긴장을 끝까지 끌어올린다. 조작은 멈춰 있다.
    /// 같은 축 세 번째부터는 아무 키로 건너뛴다. 끝나면 카메라 방향은 그대로 돌아와 있다.
    /// </summary>
    private IEnumerator PreCapture(FearAxis axis, int count)
    {
        float seconds = count <= 1 ? riseSeconds : riseRepeatSeconds;
        if (seconds <= 0f) yield break;

        string a = AxisKey(axis);
        CaptureSfx("capture.rise." + a, 0f, seconds, _riseSfx);
        if (axis == FearAxis.Auditory) CaptureSfx("capture.rise2.auditory", 0f, seconds, _riseSfx);
        CaptureSfx("capture.breath", 0f, seconds - 0.08f, _riseSfx);

        Camera cam = Camera.main;
        Transform t = cam != null ? cam.transform : null;
        Quaternion rest = t != null ? t.localRotation : Quaternion.identity;
        bool skippable = count >= 3;
        float seed = Random.value * 100f;
        float time = 0f;
        while (time < seconds)
        {
            time += Time.unscaledDeltaTime;
            if (t != null)
            {
                float k = Mathf.Clamp01(time / seconds);
                float amp = shakeDegrees * k * k;
                float f = 9f + 14f * k;   // 점점 빠르게
                float pitch = (Mathf.PerlinNoise(seed, time * f) - 0.5f) * 2f * amp;
                float yaw = (Mathf.PerlinNoise(seed + 7.3f, time * f) - 0.5f) * 2f * amp;
                float roll = (Mathf.PerlinNoise(seed + 13.1f, time * f) - 0.5f) * amp;
                t.localRotation = rest * Quaternion.Euler(pitch, yaw, roll);
            }

            if (skippable && Input.anyKeyDown) break;
            yield return null;
        }

        if (t != null) t.localRotation = rest;
        FadeOut(_riseSfx, 0.06f);
    }

    /// <summary>① 소리 끊김 ~ ② 정적. 얼굴은 <paramref name="silence"/>초 뒤에 뜬다 — 빨려 듦·얼굴 직전 소리만 정적 동안 차올라 그 순간에 꼭대기. 정적은 그 밖에는 비워 둔다.</summary>
    private void SceneSoundsCut(FearAxis axis, float silence)
    {
        string a = AxisKey(axis);
        CaptureSfx("capture.cut", 0f, 0f);
        CaptureSfx("capture.cut." + a);   // 손전등 딸깍은 파일 처음에 있다 — 맞추지 않고 바로
        CaptureSfx("capture.swell", 0f, silence, _faceSfx);
        CaptureSfx("capture.pre." + a, 0f, Mathf.Max(0f, silence - 0.03f), _faceSfx);
        if (axis == FearAxis.Illuminance)
        {
            CaptureSfx("capture.dark.illuminance", 0.2f, -1f, _sceneSfx);
            CaptureSfx("capture.choke.illuminance", 0.4f, -1f, _sceneSfx);
        }

        PlayerTablet tablet = FindAnyObjectByType<PlayerTablet>();
        _tabletAtCapture = tablet != null && tablet.IsOpened;
    }

    private bool _tabletAtCapture;

    /// <summary>얼굴 순간의 충격음 층 — 각 판의 꼭대기 바로 앞(<see cref="FaceLead"/>)부터 튼다(정적을 깨지 않고 그 프레임에 터지게).</summary>
    private const float FaceLead = 0.04f;

    /// <summary>정적을 건너뛰었다 — 얼굴에 맞춰 걸어 둔 소리를 거둔다.</summary>
    private void SceneSoundsSkipped()
    {
        FadeOut(_faceSfx, 0.05f);
    }

    /// <summary>③ 얼굴이 뜨는 순간 — 저음·중음·고음·목소리(태블릿을 들었으면 TV 잡음). 축별 등장 소리는 장면 쪽이 3D로 낸다.</summary>
    private void SceneSoundsReveal(FearAxis axis)
    {
        _faceSfx.Clear();   // 정적 동안 차오른 소리는 이제 끝까지 울린다
        string a = AxisKey(axis);
        CaptureSfx("capture.face.low", 0f, FaceLead);
        CaptureSfx("capture.face.mid." + a, 0f, FaceLead);
        CaptureSfx("capture.face.high." + a, 0f, FaceLead);
        CaptureSfx("capture.face.voice." + a, 0f, FaceLead);
        if (_tabletAtCapture) CaptureSfx("capture.tablet", 0f, FaceLead);
        CaptureSfx("capture.gasp", 0.05f);
        if (axis == FearAxis.Layout) CaptureSfx("capture.choke.layout", 0.3f, -1f, _sceneSfx);
    }

    /// <summary>④ 다시 암전.</summary>
    private void SceneSoundsBlack(FearAxis axis)
    {
        FadeOut(_sceneSfx, 0.08f);
        CaptureSfx("capture.tail", 0f, -1f, _tailSfx);
        if (axis == FearAxis.Auditory) CaptureSfx("capture.tail.auditory", 0f, -1f, _tailSfx);
    }

    /// <summary>⑤ 재시작 카드가 뜰 때.</summary>
    private void CardSounds(FearAxis axis, bool died)
    {
        CaptureSfx("capture.precard." + AxisKey(axis));
        if (died) CaptureSfx("capture.died", 0.1f);
        CaptureSfx("capture.card", 0.15f);
    }

    /// <summary>⑥ 다시 근무 — 남은 꼬리를 줄인다.</summary>
    private void EndCaptureSounds()
    {
        FadeOut(_tailSfx, 0.6f);
        FadeOut(_faceSfx, 0.2f);
    }
}

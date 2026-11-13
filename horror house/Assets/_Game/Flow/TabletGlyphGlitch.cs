using System.Collections.Generic;
using NightDuty;
using TMPro;
using UnityEngine;

/// <summary>
/// 태블릿 글자 깨짐 — 신뢰 구간 1 이상에서 가끔 글자 몇 개가 순간(0.3초) 다른 글자로 바뀌었다 돌아온다. 순수 표시 연출이다.
/// <list type="bullet">
/// <item>판정·원문·수칙 데이터는 건드리지 않는다. 화면의 TMP 글만 잠깐 바꿨다가, 그 글이 그대로일 때만 되돌린다(다른 코드가 글을 다시 쓰면 건드리지 않는다).</item>
/// <item>태블릿을 들었든 말든 상관없이 돈다(늘 들고 있는 태블릿 화면의 수칙·메시지 글).</item>
/// <item>구간이 오를수록 글자 수(1 → 2~3 → 3~4)와 빈도가 오른다. 같은 구간 안에서는 실시간 신뢰 수치가 오를수록 잦다.</item>
/// <item>소리는 기존 <c>tablet.corrupt</c>를 아주 작게(자세히 들어야 알 정도).</item>
/// </list>
/// </summary>
[DefaultExecutionOrder(500)]
public sealed class TabletGlyphGlitch : MonoBehaviour
{
    /// <summary>깨져 있는 시간(초).</summary>
    public const float GlitchSeconds = 1.0f;

    /// <summary>소리 크기 — 소리 표 값에 곱한다. 자세히 들어야 알 정도.</summary>
    public const float SoundGain = 0.12f;

    private const string SoundKey = "tablet.corrupt";
    private const float TabletPan = -0.75f;

    private static readonly string[] Pool = { "ㅁ", "ㅅ", "ㅇ", "ㅗ", "ㅓ", "ㅣ", "ㅠ", "?", "#", "잉", "뢰", "퀴", "꿻", "ㅂㅂ", "…" };

    /// <summary>한 번에 깨질 글자 수.</summary>
    public static int CountFor(Band band, float progress)
    {
        switch (band)
        {
            case Band.Band1: return 2;
            case Band.Band2: return progress < 0.5f ? 3 : 4;
            case Band.Band3: return progress < 0.5f ? 4 : 5;
            case Band.Band4: return 6 + Mathf.RoundToInt(2f * Mathf.Clamp01(progress));   // 6~8개
            default: return 0;
        }
    }

    /// <summary>다음 깨짐까지의 간격 범위(초). 구간 안 진행도가 오를수록 짧아진다.</summary>
    public static void GapFor(Band band, float progress, out float min, out float max)
    {
        float k = Mathf.Clamp01(progress);
        switch (band)
        {
            case Band.Band1: min = 20f - 4f * k; max = 40f - 8f * k; break;
            case Band.Band2: min = 12f - 3f * k; max = 25f - 6f * k; break;
            case Band.Band3: min = 8f - 2f * k; max = 16f - 4f * k; break;
            case Band.Band4: min = 4f - 1f * k; max = 8f - 2f * k; break;
            default: min = max = float.MaxValue; break;
        }
    }

    private sealed class Hit
    {
        public TMP_Text text;
        public string original;
        public string corrupted;
    }

    private readonly List<Hit> _hits = new List<Hit>();
    private readonly List<TMP_Text> _candidates = new List<TMP_Text>();
    private PlayerTablet _tablet;
    private AudioSource _source;
    private float _nextAt = -1f;
    private float _restoreAt;

    private void OnDisable()
    {
        RestoreAll(true);
    }

    private void Update()
    {
        if (_hits.Count > 0 && Time.unscaledTime >= _restoreAt)
        {
            RestoreAll(false);
        }

        if (!NightRun.IsNightActive || NightRun.IsCaptured || Time.timeScale <= 0.01f)
        {
            _nextAt = -1f;
            return;
        }

        int trust = NightRun.Axes != null ? NightRun.Axes.GetValue(FearAxis.Trust) : 0;
        Band band = Bands.OfTrust(trust);
        if (band == Band.Band0)
        {
            _nextAt = -1f;
            return;
        }

        float progress = TrustProgress(trust, band);

        if (_nextAt < 0f)
        {
            _nextAt = Time.unscaledTime + NextGap(band, progress);
            return;
        }

        if (Time.unscaledTime < _nextAt || _hits.Count > 0)
        {
            return;
        }

        _nextAt = Time.unscaledTime + NextGap(band, progress);
        Glitch(CountFor(band, progress));
    }

    private static float TrustProgress(int trust, Band band)
    {
        int lower = Bands.TrustLowerBound(band);
        int upper = band == Band.Band4 ? 100 : Bands.TrustLowerBound(band + 1);
        int width = Mathf.Max(1, upper - lower);
        return Mathf.Clamp01((trust - lower) / (float)width);
    }

    private static float NextGap(Band band, float progress)
    {
        float min, max;
        GapFor(band, progress, out min, out max);
        return Random.Range(min, max);
    }

    private void Glitch(int count)
    {
        if (_tablet == null)
        {
            _tablet = FindAnyObjectByType<PlayerTablet>();
            if (_tablet == null) return;
        }

        _candidates.Clear();
        TMP_Text[] all = _tablet.GetComponentsInChildren<TMP_Text>(false);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].isActiveAndEnabled && HangulCount(all[i].text) >= 4)
            {
                _candidates.Add(all[i]);
            }
        }

        if (_candidates.Count == 0) return;

        int done = 0;
        for (int n = 0; n < count * 4 && done < count; n++)
        {
            TMP_Text t = _candidates[Random.Range(0, _candidates.Count)];
            if (CorruptOne(t)) done++;
        }

        if (done == 0) return;

        _restoreAt = Time.unscaledTime + GlitchSeconds;
    }

    private bool CorruptOne(TMP_Text t)
    {
        Hit hit = null;
        for (int i = 0; i < _hits.Count; i++)
        {
            if (ReferenceEquals(_hits[i].text, t)) { hit = _hits[i]; break; }
        }

        string cur = t.text;
        if (hit != null && cur != hit.corrupted) return false;

        List<int> spots = new List<int>();
        bool inTag = false;
        for (int i = 0; i < cur.Length; i++)
        {
            char c = cur[i];
            if (c == '<') { inTag = true; continue; }
            if (c == '>') { inTag = false; continue; }
            if (!inTag && c >= '가' && c <= '힣') spots.Add(i);
        }

        if (spots.Count == 0) return false;

        int at = spots[Random.Range(0, spots.Count)];
        string next = cur.Substring(0, at) + Pool[Random.Range(0, Pool.Length)] + cur.Substring(at + 1);

        if (hit == null)
        {
            hit = new Hit();
            hit.text = t;
            hit.original = cur;
            _hits.Add(hit);
        }

        hit.corrupted = next;
        t.text = next;
        return true;
    }

    private void RestoreAll(bool force)
    {
        for (int i = 0; i < _hits.Count; i++)
        {
            Hit h = _hits[i];
            // 그 사이 다른 코드가 글을 다시 썼으면 그대로 둔다.
            if (h.text != null && (force || h.text.text == h.corrupted) && h.text.text == h.corrupted)
            {
                h.text.text = h.original;
            }
        }

        _hits.Clear();
    }

    private void PlaySound()
    {
        float volume;
        AudioClip clip = DirectionSoundTableSO.FindExact(SoundKey, out volume);
        if (clip == null) return;

        if (_source == null)
        {
            _source = gameObject.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.spatialBlend = 0f;
            _source.panStereo = TabletPan;
            NightDutyMixer.Route(_source, NightDutyMixer.Bus.Tablet);
        }

        _source.PlayOneShot(clip, volume * SoundGain);
    }

    private static int HangulCount(string s)
    {
        if (string.IsNullOrEmpty(s)) return 0;
        int n = 0;
        for (int i = 0; i < s.Length; i++)
        {
            if (s[i] >= '가' && s[i] <= '힣') n++;
        }

        return n;
    }
}

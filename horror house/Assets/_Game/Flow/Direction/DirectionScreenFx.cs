using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 연출 화면 효과(2026-10-02). 김진선님의 공포 화면 톤 볼륨 4종(<c>VP_Horror_*</c>)과 화면 울렁임(<see cref="ScreenWobble"/>)을
/// 긴장 디렉터의 조우에 붙인다 — 「화면 효과 가이드」 §3·§5의 조합을 코드로 돌린다(Timeline 없이 세기만 움직임).
/// <list type="bullet">
/// <item>볼륨은 이 오브젝트의 자식으로 한 번 만든다. <b>레이어 Viewmodel(8)</b> · Global · 우선순위 20~23(기본 밤 볼륨·인간나무 볼륨 10·11보다 위) · 처음 weight 0.</item>
/// <item>요청은 이름(key)으로 쌓고, 종류마다 가장 큰 세기를 쓴다(같은 볼륨을 둘이 덮어쓰지 않게 — 가이드 §3.5 주의).</item>
/// <item>시간은 <c>Time.deltaTime</c> — 일시정지면 함께 멈춘다.</item>
/// <item>울렁임은 <see cref="ScreenWobbleSource"/> 하나로 낸다. 씬의 <see cref="ScreenWobble"/>(PlaySystems)가 그린다. 없으면 하나 만든다.</item>
/// </list>
/// 판정에는 아무 영향이 없다. 위반 즉시 피드백 금기 때문에 <b>판정 결과에는 붙이지 않는다</b> — 조우의 대면·정전 같은 연출 순간에만.
/// </summary>
[DisallowMultipleComponent]
public sealed class DirectionScreenFx : MonoBehaviour
{
    /// <summary>효과 종류.</summary>
    public enum Kind
    {
        Suffocate = 0,
        Blackout = 1,
        Wrongness = 2,
        Creep = 3,
        Wobble = 4
    }

    private const int KindCount = 5;

    private sealed class Request
    {
        public Kind Kind;
        public float Peak;
        public float FadeIn;
        public float Hold;      // 음수면 Release까지
        public float FadeOut;
        public float T;
        public float ReleasedAt = -1f;
    }

    private readonly Dictionary<string, List<Request>> _requests = new Dictionary<string, List<Request>>(StringComparer.Ordinal);
    private readonly Volume[] _volumes = new Volume[4];
    private readonly float[] _weights = new float[KindCount];
    private ScreenWobbleSource _wobble;
    private bool _built;

    /// <summary>지금 세기(디버그·테스트).</summary>
    public float WeightOf(Kind kind)
    {
        return _weights[(int)kind];
    }

    /// <summary>
    /// 세기를 올린다. <paramref name="hold"/>가 음수면 <see cref="Release"/>까지 유지. 같은 key로 다시 부르면 그 key에 하나 더 쌓인다.
    /// </summary>
    public void Push(string key, Kind kind, float peak, float fadeIn, float hold, float fadeOut)
    {
        Build();
        List<Request> list;
        if (!_requests.TryGetValue(key, out list))
        {
            list = new List<Request>();
            _requests[key] = list;
        }

        list.Add(new Request { Kind = kind, Peak = Mathf.Clamp01(peak), FadeIn = Mathf.Max(0f, fadeIn), Hold = hold, FadeOut = Mathf.Max(0.01f, fadeOut) });
    }

    /// <summary>순간 암전 — 0.1초 동안 0 → 1 → 0을 <paramref name="times"/>번(가이드 §3.4 예시).</summary>
    public void Flash(string key, Kind kind, int times, float peak)
    {
        for (int i = 0; i < times; i++)
        {
            Build();
            List<Request> list;
            if (!_requests.TryGetValue(key, out list))
            {
                list = new List<Request>();
                _requests[key] = list;
            }

            // 0.15초 간격으로 겹치지 않게 미리 시작 시각을 당겨 둔다(T 음수 = 아직 시작 전).
            list.Add(new Request { Kind = kind, Peak = Mathf.Clamp01(peak), FadeIn = 0.05f, Hold = 0f, FadeOut = 0.05f, T = -0.2f * i });
        }
    }

    /// <summary>그 key의 유지 요청을 내린다.</summary>
    public void Release(string key)
    {
        List<Request> list;
        if (!_requests.TryGetValue(key, out list)) return;
        foreach (Request r in list)
        {
            if (r.ReleasedAt < 0f) r.ReleasedAt = r.T;
        }
    }

    /// <summary>모두 즉시 끈다(밤 종료·재시작).</summary>
    public void ClearAll()
    {
        _requests.Clear();
        for (int i = 0; i < KindCount; i++) _weights[i] = 0f;
        Apply();
    }

    private void Update()
    {
        for (int i = 0; i < KindCount; i++) _weights[i] = 0f;
        if (_requests.Count > 0)
        {
            float dt = Time.deltaTime;
            List<string> done = null;
            foreach (KeyValuePair<string, List<Request>> kv in _requests)
            {
                List<Request> list = kv.Value;
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    Request r = list[i];
                    r.T += dt;
                    float w;
                    if (!Evaluate(r, out w))
                    {
                        list.RemoveAt(i);
                        continue;
                    }

                    int k = (int)r.Kind;
                    if (w > _weights[k]) _weights[k] = w;
                }

                if (list.Count == 0)
                {
                    if (done == null) done = new List<string>();
                    done.Add(kv.Key);
                }
            }

            if (done != null)
            {
                foreach (string k in done) _requests.Remove(k);
            }
        }

        Apply();
    }

    /// <summary>요청 하나의 지금 세기. 끝났으면 false.</summary>
    private static bool Evaluate(Request r, out float w)
    {
        w = 0f;
        if (r.T < 0f) return true;   // 아직 시작 전

        float rise = r.FadeIn <= 0f ? 1f : Mathf.Clamp01(r.T / r.FadeIn);
        float level = r.Peak * Smooth(rise);

        float fallStart;
        if (r.ReleasedAt >= 0f) fallStart = Mathf.Max(r.ReleasedAt, r.FadeIn);
        else if (r.Hold >= 0f) fallStart = r.FadeIn + r.Hold;
        else
        {
            w = level;
            return true;
        }

        if (r.T < fallStart)
        {
            w = level;
            return true;
        }

        float fall = (r.T - fallStart) / r.FadeOut;
        if (fall >= 1f) return false;
        w = level * (1f - Smooth(fall));
        return true;
    }

    private static float Smooth(float x)
    {
        return x * x * (3f - 2f * x);
    }

    private void Apply()
    {
        for (int i = 0; i < _volumes.Length; i++)
        {
            if (_volumes[i] != null) _volumes[i].weight = _weights[i];
        }

        if (_wobble != null) _wobble.weight = _weights[(int)Kind.Wobble];
    }

    private void Build()
    {
        if (_built) return;
        _built = true;

        DirectionScreenFxSO so = DirectionScreenFxSO.Load();
        VolumeProfile[] profiles = so != null
            ? new[] { so.Suffocate, so.Blackout, so.Wrongness, so.Creep }
            : new VolumeProfile[4];
        string[] names = { "Fx_Suffocate", "Fx_Blackout", "Fx_Wrongness", "Fx_Creep" };
        int viewmodel = LayerMask.NameToLayer("Viewmodel");
        if (viewmodel < 0) viewmodel = 8;

        for (int i = 0; i < 4; i++)
        {
            if (profiles[i] == null) continue;
            GameObject go = new GameObject(names[i]);
            go.layer = viewmodel;   // ⚠ 플레이어 카메라 Volume Mask가 Viewmodel 하나뿐(가이드 §1.1)
            go.transform.SetParent(transform, false);
            Volume v = go.AddComponent<Volume>();
            v.isGlobal = true;
            v.priority = 20 + i;
            v.weight = 0f;
            v.sharedProfile = profiles[i];
            _volumes[i] = v;
        }

        if (FindAnyObjectByType<ScreenWobble>() == null)
        {
            gameObject.AddComponent<ScreenWobble>();   // 본 씬은 PlaySystems에 이미 있다 — 없을 때만(둘이면 서로 덮어씀)
        }

        GameObject w = new GameObject("Fx_Wobble");
        w.transform.SetParent(transform, false);
        _wobble = w.AddComponent<ScreenWobbleSource>();
        _wobble.weight = 0f;
    }

    private void OnDisable()
    {
        ClearAll();
    }
}

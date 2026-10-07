using System.Collections;
using System.Collections.Generic;
using NightDuty;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 51차 CCTV 얼굴 점프스케어(민: 「CCTV 사람이 걷는 애니 없이 서성거려 이상하다 — 그 채널에서 사람이 움직이는 걸 3초 이상 보면 CCTV 화면 코앞에 얼굴이 확대되게」).
/// <list type="bullet">
/// <item>화면 속 사람(K1 조우의 사람 · K-1 이상의 사람)을 등록받는다. 등록된 사람은 걷지 않고 <see cref="StepSeconds"/>초마다 <see cref="StepMeters"/>m씩 툭툭 카메라 쪽으로 다가온다(스톱모션).</item>
/// <item>그 채널을 <see cref="WatchSeconds"/>초 이어서 보면(틈 0.2초 허용) 얼굴이 카메라 코앞으로 — 3일차부터, 밤당 한 번.</item>
/// <item>회차 세기: 첫 번째 0.6초 + 강 스팅어 + 지직 소리 · 두 번째 0.25초 소리 없이 · 세 번째부터는 그 자리에서 카메라를 올려다보기만.</item>
/// </list>
/// K1(「화면 속 !_ 을 오래 보지 마십시오.」) 판정은 코어(<c>DontWatchJudge</c>)가 같은 3초로 따로 한다 — 이 컴포넌트는 보이고 들리는 것만.
/// </summary>
[DisallowMultipleComponent]
public sealed class CctvFaceScare : MonoBehaviour
{
    /// <summary>이만큼 이어서 보면 얼굴이 온다(초).</summary>
    public const float WatchSeconds = 3f;

    /// <summary>스톱모션 한 걸음 사이(초).</summary>
    public const float StepSeconds = 1.5f;

    /// <summary>스톱모션 한 걸음(m).</summary>
    public const float StepMeters = 0.7f;

    /// <summary>카메라에 이보다 가까이 다가오지 않는다(m, 바닥 거리).</summary>
    public const float NearestMeters = 2f;

    /// <summary>얼굴 점프스케어가 시작되는 일차.</summary>
    public const int FirstDay = 3;

    private sealed class Person
    {
        public GameObject Go;
        public int Channel;
        public bool Approach;
        public bool Cross;
        public Vector3 A;
        public Vector3 B;
        public float At;
        public float NextStep;
        public System.Action OnScared;
    }

    private static CctvFaceScare s_active;
    private static int s_scares;
    private static object s_night;

    private readonly List<Person> _people = new List<Person>();
    private float _watch;
    private float _gap;
    private bool _busy;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        s_active = null;
        s_scares = 0;
        s_night = null;
    }

    private static CctvFaceScare Ensure()
    {
        if (s_active != null) return s_active;
        GameObject go = new GameObject("CctvFaceScare (auto)");
        SceneManager.MoveGameObjectToScene(go, SceneManager.GetActiveScene());
        s_active = go.AddComponent<CctvFaceScare>();
        return s_active;
    }

    /// <summary>
    /// 화면 속 사람을 등록한다. <paramref name="approach"/>면 스톱모션으로 카메라 쪽으로 다가온다(K1 조우).
    /// <paramref name="onScared"/>는 얼굴 점프스케어가 끝난 뒤 불린다(사람을 거두거나 잠시 숨긴다).
    /// </summary>
    public static void Register(GameObject person, int channel, bool approach, System.Action onScared = null)
    {
        if (person == null) return;
        CctvFaceScare s = Ensure();
        s._people.RemoveAll(p => p.Go == null || p.Go == person);
        s._people.Add(new Person { Go = person, Channel = channel, Approach = approach, NextStep = Time.time + StepSeconds, OnScared = onScared });
    }

    /// <summary>
    /// 화면을 가로지르는 사람을 등록한다(52차 K1 「화면 속 !_ 이 지나갈 때까지 채널을 넘기지 마십시오.」) — 걷지 않고 <paramref name="from"/>에서 <paramref name="to"/>로 툭툭 옮겨 가고,
    /// 끝에 닿으면 화면에서 사라진다(<paramref name="onPassed"/>). 3초 이어서 보면 얼굴 점프스케어는 같다.
    /// </summary>
    public static void RegisterCrossing(GameObject person, int channel, Vector3 from, Vector3 to, System.Action onPassed)
    {
        if (person == null) return;
        CctvFaceScare s = Ensure();
        s._people.RemoveAll(p => p.Go == null || p.Go == person);
        s._people.Add(new Person { Go = person, Channel = channel, Cross = true, A = from, B = to, NextStep = Time.time + StepSeconds, OnScared = onPassed });
    }

    /// <summary>등록을 푼다.</summary>
    public static void Unregister(GameObject person)
    {
        if (s_active != null) s_active._people.RemoveAll(p => p.Go == null || p.Go == person);
    }

    private void OnDestroy()
    {
        if (s_active == this) s_active = null;
    }

    private void Update()
    {
        _people.RemoveAll(p => p.Go == null);
        CctvSystem cctv = CctvSystem.Active;
        if (cctv == null) return;

        for (int i = 0; i < _people.Count; i++)
        {
            Person p = _people[i];
            if ((!p.Approach && !p.Cross) || _busy || Time.time < p.NextStep) continue;
            p.NextStep = Time.time + StepSeconds;
            if (p.Cross) StepAcross(p, cctv);
            else Step(p, cctv);
        }

        if (_busy) return;
        Person seen = Watched(cctv);
        if (seen == null)
        {
            _gap += Time.deltaTime;
            if (_gap > SensingRules.GazeGapSeconds) _watch = 0f;
            return;
        }

        _gap = 0f;
        _watch += Time.deltaTime;
        if (_watch < WatchSeconds) return;
        _watch = 0f;

        object night = NightRun.Inspections != null ? NightRun.Inspections.Plan : null;
        if (NightRun.Day < FirstDay || ReferenceEquals(night, s_night)) return;
        s_night = night;
        StartCoroutine(Scare(seen, cctv, s_scares++));
    }

    private Person Watched(CctvSystem cctv)
    {
        if (!cctv.IsViewing) return null;
        for (int i = 0; i < _people.Count; i++)
        {
            Person p = _people[i];
            if (p.Channel != cctv.CurrentChannel || !p.Go.activeInHierarchy) continue;
            CctvOnlyVisible only = p.Go.GetComponent<CctvOnlyVisible>();
            if (only != null && !only.enabled) continue;
            return p;
        }

        return null;
    }

    private static void Step(Person p, CctvSystem cctv)
    {
        Camera cam = cctv.ChannelCamera(p.Channel);
        if (cam == null) return;
        Vector3 pos = p.Go.transform.position;
        Vector3 to = cam.transform.position - pos;
        to.y = 0f;
        float d = to.magnitude;
        if (d <= NearestMeters + 0.05f) return;
        Vector3 next = pos + to / d * Mathf.Min(StepMeters, d - NearestMeters);
        p.Go.transform.SetPositionAndRotation(next, Quaternion.LookRotation(to / d, Vector3.up));
        cctv.ForceRenderFor(0.15f);   // 툭 — 다음 프레임에 바로 보이게
    }

    private static void StepAcross(Person p, CctvSystem cctv)
    {
        if (p.At >= 1f) return;
        float span = Mathf.Max(0.5f, Vector3.Distance(p.A, p.B));
        p.At = Mathf.Min(1f, p.At + StepMeters / span);
        Vector3 pos = Vector3.Lerp(p.A, p.B, p.At);
        Camera cam = cctv.ChannelCamera(p.Channel);
        Vector3 look = cam != null ? cam.transform.position - pos : p.B - p.A;
        look.y = 0f;
        p.Go.transform.SetPositionAndRotation(pos, look.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(look.normalized, Vector3.up) : p.Go.transform.rotation);
        cctv.ForceRenderFor(0.15f);
        if (p.At >= 1f && p.OnScared != null)
        {
            // 다 지나갔다 — 화면에서 사라진다.
            System.Action done = p.OnScared;
            p.OnScared = null;
            done();
        }
    }

    private IEnumerator Scare(Person p, CctvSystem cctv, int count)
    {
        _busy = true;
        Camera cam = cctv.ChannelCamera(p.Channel);
        Transform t = p.Go != null ? p.Go.transform : null;
        if (cam == null || t == null)
        {
            _busy = false;
            yield break;
        }

        Vector3 oldPos = t.position;
        Quaternion oldRot = t.rotation;
        Vector3 toCam = cam.transform.position - t.position;
        toCam.y = 0f;
        Quaternion faceCam = toCam.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(toCam.normalized, Vector3.up) : t.rotation;

        if (count >= 2)
        {
            // 세 번째부터: 그 자리에서 고개를 들어 카메라를 본다(가장 조용한 것이 가장 오래 남는다).
            t.rotation = faceCam;
            cctv.ForceRenderFor(0.3f);
            _busy = false;
            yield break;
        }

        // 얼굴(조준점 = 머리 높이)이 카메라 0.45m 앞에 오게 몸을 옮긴다.
        Transform aim = StandInFactory.Aim(p.Go);
        Vector3 headOffset = aim.position - t.position;
        Vector3 faceAt = cam.transform.position + cam.transform.forward * 0.45f;
        Quaternion lookAtCam = Quaternion.LookRotation(Flat(-cam.transform.forward), Vector3.up);
        t.rotation = lookAtCam;
        headOffset = aim.position - t.position;
        t.position = faceAt - headOffset;

        float hold = count == 0 ? 0.6f : 0.25f;
        cctv.ForceRenderFor(hold + 0.1f);
        if (count == 0)
        {
            EncounterImpact.PlayTier(EncounterImpact.Tier.Strong);
            float v;
            AudioClip glitch = DirectionSoundTableSO.FindExact("cctv.face", out v);
            if (glitch != null)
            {
                AmbiencePlayer amb = AmbiencePlayer.Active;
                if (amb == null || !amb.PlayStingerClip(glitch, v)) AudioSource.PlayClipAtPoint(glitch, Camera.main != null ? Camera.main.transform.position : Vector3.zero, v);
            }

            BodyMeter.Startle();
        }

        Debug.Log("[CctvFaceScare] 얼굴 점프스케어 " + (count + 1) + "번째 · CAM" + (p.Channel + 1));
        yield return new WaitForSeconds(hold);

        if (t != null)
        {
            t.SetPositionAndRotation(oldPos, oldRot);
            cctv.ForceRenderFor(0.15f);
        }

        if (p.OnScared != null)
        {
            try { p.OnScared(); }
            catch (System.Exception e) { Debug.LogException(e); }
        }

        _busy = false;
    }

    private static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v.sqrMagnitude > 0.000001f ? v.normalized : Vector3.forward;
    }
}

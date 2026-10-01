using System;
using System.Collections;
using System.Collections.Generic;
using NightDuty;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 긴장 디렉터의 연출 실행기(2026-10-01 7단계). <see cref="EventBus.DirectionEmitted"/>를 받아 <b>보이고 들리는 것</b>을 맞춘다 —
/// 판정 단서는 디렉터가 코어 안에서 이미 판정 책에 넣었다.
/// <list type="bullet">
/// <item>대면: 대역(<see cref="StandInFactory"/>) · 방 소등(<see cref="LightGroup"/>) · 런타임 신호 구역(<c>science.dark</c> = 소등한 방 전체, <c>classroom.phantomdoor</c> = 없던 문 앞).</item>
/// <item>CCTV에만 보이는 사람: 지금 채널 카메라 앞 6m에 <see cref="CctvOnlyVisible"/> 대역.</item>
/// <item>수칙 단서: C4 붉은 불(교실 조명을 붉게), T5 불 켜진 칸(칸 안 작은 등).</item>
/// <item>가짜 놀람: 손전등 끊김은 손전등 라이트만 깜빡인다(판정 신호 없음). 나머지는 소리 자리.</item>
/// <item>결과·중단·밤 재시작: 대역을 거두고 조명·구역을 되돌린다.</item>
/// </list>
/// 소리는 <c>Resources/Direction/&lt;조우 ID&gt;.&lt;단계&gt;</c> 클립이 있으면 그 자리에서 3D로 재생한다(없으면 조용히 넘어간다).
/// 근무 씬이면 스스로 설치된다(씬 파일 수정 없음).
/// </summary>
[DisallowMultipleComponent]
public sealed class DirectionStage : MonoBehaviour
{
    private sealed class Staged
    {
        public readonly List<GameObject> Objects = new List<GameObject>();
        public readonly List<Action> Undo = new List<Action>();
    }

    private readonly Dictionary<string, Staged> _staged = new Dictionary<string, Staged>(StringComparer.Ordinal);
    private readonly Dictionary<SpaceId, LightGroup> _groups = new Dictionary<SpaceId, LightGroup>();
    private SpaceZones _zones;
    private static DirectionStage s_active;

    /// <summary>지금 살아 있는 실행기.</summary>
    public static DirectionStage Active
    {
        get { return s_active; }
    }

    /// <summary>지금 무대에 올라간 연출 ID들(디버그).</summary>
    public IEnumerable<string> StagedIds
    {
        get { return _staged.Keys; }
    }

    /// <summary>콘솔에 연출 알림을 찍을지.</summary>
    public static bool Verbose = true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Install()
    {
        s_active = null;
        Verbose = true;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InstallForFirstScene()
    {
        EnsureFor(SceneManager.GetActiveScene());
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureFor(scene);
    }

    private static void EnsureFor(Scene scene)
    {
        if (!FlowAutoInstall.IsDutyScene(scene)) return;
        if (FlowAutoInstall.Exists<DirectionStage>(scene)) return;
        GameObject go = new GameObject("DirectionStage (auto)");
        SceneManager.MoveGameObjectToScene(go, scene);
        go.AddComponent<DirectionStage>();
    }

    private void OnEnable()
    {
        s_active = this;
        EventBus.DirectionEmitted += OnDirection;
        EventBus.FinalRuleSettled += OnRuleSettled;
        EventBus.NightRestarted += OnRestarted;
        EventBus.DayEnded += OnDayEnded;
    }

    private void OnDisable()
    {
        EventBus.DirectionEmitted -= OnDirection;
        EventBus.FinalRuleSettled -= OnRuleSettled;
        EventBus.NightRestarted -= OnRestarted;
        EventBus.DayEnded -= OnDayEnded;
        ClearAll(DirectionPhase.Aborted);
        if (s_active == this) s_active = null;
    }

    private void OnRestarted(RestartResult r)
    {
        ClearAll(DirectionPhase.Aborted);
    }

    private void OnDayEnded(DaySummary s)
    {
        ClearAll(DirectionPhase.Aborted);
    }

    /// <summary>무대를 모두 거둔다(디버그 버튼·재시작).</summary>
    public void ClearAll(DirectionPhase phase)
    {
        List<string> ids = new List<string>(_staged.Keys);
        for (int i = 0; i < ids.Count; i++) Cleanup(ids[i], phase);
        foreach (LightGroup g in _groups.Values) g.Restore();
    }

    // ── 알림 처리 ───────────────────────────────────────────

    private void OnDirection(DirectionEvent e)
    {
        if (Verbose) Debug.Log("[Direction] " + e);

        try
        {
            switch (e.Kind)
            {
                case DirectionEventKind.Encounter:
                    OnEncounter(e);
                    break;
                case DirectionEventKind.RuleCue:
                    OnRuleCue(e);
                    break;
                case DirectionEventKind.RuleCueEnd:
                    Cleanup(e.SourceId, DirectionPhase.Result);
                    break;
                case DirectionEventKind.FakeScare:
                    OnFake(e);
                    break;
            }
        }
        catch (Exception ex)
        {
            Debug.LogException(ex, this);
        }
    }

    private void OnEncounter(DirectionEvent e)
    {
        switch (e.Phase)
        {
            case DirectionPhase.Foreshadow:
            case DirectionPhase.FalseForeshadow:
                PlaySound(e.SourceId + ".foreshadow", PointOr(e.Point, 6f));
                break;
            case DirectionPhase.Confront:
                Confront(e);
                break;
            case DirectionPhase.WindowClose:
                SetCuePhase(e.SourceId, DirectionPhase.WindowClose);
                PlaySound(e.SourceId + ".release", PointOr(e.Point, 3f));
                break;
            case DirectionPhase.Result:
            case DirectionPhase.Aborted:
                Cleanup(e.SourceId, e.Phase);
                break;
        }
    }

    private void Confront(DirectionEvent e)
    {
        EncounterScript script = EncounterScripts.Find(e.SourceId);
        if (script == null) return;

        Cleanup(e.SourceId, DirectionPhase.Aborted);
        Staged st = Stage(e.SourceId);
        Vector3 player = PlayerFeet();
        Vector3 point = PointOr(e.Point, script.Distance > 0f ? script.Distance : 3f);

        if (script.LightsOff != SpaceId.None)
        {
            LightGroup g = Group(script.LightsOff);
            g.TurnOff();
            st.Undo.Add(g.TurnOn);
        }

        if (script.Zone == FinalCues.ScienceDarkZone)
        {
            Bounds room;
            if (Zones() != null && Zones().TryGetSpaceBox(script.Space, out room))
            {
                FinalRuleRelay.SetRuntimeZone(script.Zone, room);
                string zid = script.Zone;
                st.Undo.Add(() => FinalRuleRelay.ClearRuntimeZone(zid));
            }
        }

        if (e.SourceId == ProgramCatalog.CctvPerson)
        {
            SpawnCctvPerson(st);
        }
        else if (script.StandIn.Length > 0)
        {
            Vector3 at = script.Placement == CuePlacement.CeilingAhead ? CeilingAbove(point) : FloorBelow(point);
            GameObject go = StandInFactory.Create(script.StandIn, at, player, script.AnchorId);
            DirectionCue cue = go.GetComponent<DirectionCue>();
            if (cue == null) cue = go.AddComponent<DirectionCue>();
            cue.Play(new CueContext { Intensity = e.Intensity, Anchor = at, EncounterId = e.SourceId });
            st.Objects.Add(go);

            if (script.ExtraStandIn.Length > 0)
            {
                Vector3 ceiling = CeilingAbove(PointOr(Vector3.zero, 3f));
                GameObject extra = StandInFactory.Create(script.ExtraStandIn, ceiling, player, script.ExtraAnchorId);
                if (extra.GetComponent<DirectionCue>() == null) extra.AddComponent<DirectionCue>().Play(new CueContext { Intensity = e.Intensity, Anchor = ceiling, EncounterId = e.SourceId });
                st.Objects.Add(extra);
            }

            if (script.Zone == FinalCues.PhantomDoorZone)
            {
                Vector3 toward = player - at;
                toward.y = 0f;
                Vector3 c = at + (toward.sqrMagnitude > 0.01f ? toward.normalized : Vector3.forward) * 0.8f + Vector3.up;
                FinalRuleRelay.SetRuntimeZone(script.Zone, new Bounds(c, new Vector3(1.4f, 3f, 1.2f)));
                string zid = script.Zone;
                st.Undo.Add(() => FinalRuleRelay.ClearRuntimeZone(zid));
            }
        }

        PlaySound(e.SourceId + ".confront", point);
    }

    private void SpawnCctvPerson(Staged st)
    {
        CctvSystem cctv = CctvSystem.Active;
        Camera cam = cctv != null ? cctv.ChannelCamera(cctv.CurrentChannel) : null;
        if (cam == null) return;

        Vector3 fwd = cam.transform.forward;
        fwd.y = 0f;
        Vector3 at = FloorBelow(cam.transform.position + (fwd.sqrMagnitude > 0.01f ? fwd.normalized : Vector3.forward) * 6f);
        GameObject go = StandInFactory.Create("mob.blackman", at, cam.transform.position, string.Empty);
        CctvOnlyVisible only = go.AddComponent<CctvOnlyVisible>();
        only.Channel = cctv.CurrentChannel;
        st.Objects.Add(go);
    }

    private void OnRuleCue(DirectionEvent e)
    {
        Staged st = Stage(e.SourceId);
        switch (e.SourceId)
        {
            case "H2":
                OpenDoorAhead();
                break;
            case "C4":
            {
                LightGroup g = Group(SpaceId.Classroom);
                g.Tint(new Color(1f, 0.12f, 0.08f), 0.7f);
                st.Undo.Add(g.Untint);
                break;
            }
            case "T5":
            {
                Bounds stall;
                if (Zones() != null && Zones().TryGetSignalZone("toilet.stall.inner.inside", out stall))
                {
                    GameObject go = new GameObject("T5 불 켜진 칸");
                    go.transform.position = new Vector3(stall.center.x, PlayerFeet().y + 1.6f, stall.center.z);
                    Light l = go.AddComponent<Light>();
                    l.type = LightType.Point;
                    l.range = 2.5f;
                    l.intensity = 2f;
                    l.color = new Color(1f, 0.85f, 0.6f);
                    st.Objects.Add(go);
                }

                break;
            }
        }

        PlaySound(e.SourceId + ".cue", PointOr(e.Point, 3f));
    }

    /// <summary>
    /// H2 「열린 문은 열린 채로」의 방아쇠 — 플레이어 앞 3~15m의 닫힌 문 하나를 연출로 연다.
    /// <see cref="DoorRelay.BeginDirectionMove"/>로 출처를 연출로 못 박으면, 그 문이 열리는 동안 플레이어가 보고 있을 때
    /// DoorRelay가 <c>DoorAutoOpenObserved</c>를 보낸다(보지 않았으면 H2의 방아쇠도 없다).
    /// </summary>
    /// <summary>H2 연출이 먼저 고르는 문 — 레벨에서 자동 개방용으로 지정한 복도 문.</summary>
    private const string AutoOpenDoorId = "corridor.door.auto";

    private void OpenDoorAhead()
    {
        Transform root = PlayerRoot();
        if (root == null) return;
        Vector3 fwd = root.forward;
        fwd.y = 0f;
        fwd.Normalize();

        DoorRelay best = null;
        float bestScore = float.MaxValue;
        foreach (DoorRelay relay in FindObjectsByType<DoorRelay>(FindObjectsSortMode.None))
        {
            DoorHandle h = DoorHandle.Of(relay);
            if (!h.IsValid || h.IsOpen) continue;

            // corridor.door.auto는 레벨에서 「저절로 열리는 문」으로 지정해 둔 문이다(DoorPolicyBuilder).
            // 평소엔 잠겨 있으므로 연출이 열 때만 런타임 잠금을 푼다. 다른 잠긴 문은 길막이므로 건드리지 않는다.
            bool designated = relay.DoorId == AutoOpenDoorId;
            if (h.IsLocked && !designated) continue;
            Vector3 to = relay.transform.position - root.position;
            to.y = 0f;
            float d = to.magnitude;
            if (d < 3f || d > (designated ? 25f : 18f)) continue;
            float ahead = Vector3.Dot(fwd, to / d);
            float score = d - ahead * 8f - (designated && ahead > 0.2f ? 10f : 0f);   // 보이는 지정 문 > 앞쪽·가까운 문(판정은 「본」 개방만 센다)
            if (score < bestScore)
            {
                bestScore = score;
                best = relay;
            }
        }

        if (best == null)
        {
            if (Verbose) Debug.Log("[Direction] H2: 근처에 열 수 있는 닫힌 문이 없습니다.");
            return;
        }

        DoorHandle chosen = DoorHandle.Of(best);
        if (chosen.IsLocked) chosen.ForceUnlock();
        best.BeginDirectionMove(2f);
        chosen.Open();
        if (Verbose) Debug.Log("[Direction] H2: 문 자동 개방 — " + best.DoorId);
    }

    private void OnFake(DirectionEvent e)
    {
        if (e.SourceId == "fake.flashlight.flicker")
        {
            StartCoroutine(FlickerFlashlight());
            return;
        }

        PlaySound(e.SourceId, PointOr(e.Point, 5f));
    }

    private IEnumerator FlickerFlashlight()
    {
        FlashlightRelay relay = FlashlightRelay.Active;
        if (relay == null || !relay.IsOn) yield break;
        Light light = null;
        foreach (Light l in relay.GetComponentsInChildren<Light>(true))
        {
            if (l.type == LightType.Spot && l.enabled)
            {
                light = l;
                break;
            }
        }

        if (light == null) yield break;
        for (int i = 0; i < 3; i++)
        {
            light.enabled = false;
            yield return new WaitForSeconds(0.07f + i * 0.03f);
            if (light == null) yield break;
            light.enabled = relay.IsOn;
            yield return new WaitForSeconds(0.12f);
        }
    }

    private void OnRuleSettled(FinalRuleResult r)
    {
        // 교차: 소년이 있는 동안 C2를 어기면 머리 박기(소리는 복도 어디서나).
        if (r.RuleId == "C2" && r.Outcome == FinalOutcome.Violated && _staged.ContainsKey(ProgramCatalog.BoyBang))
        {
            PlaySound(ProgramCatalog.BoyBang + ".headbang", PlayerFeet());
            if (Verbose) Debug.Log("[Direction] 교차 연출 — 소년 머리 박기(C2 위반)");
        }
    }

    // ── 무대 ───────────────────────────────────────────────

    private Staged Stage(string id)
    {
        Staged st;
        if (!_staged.TryGetValue(id, out st))
        {
            st = new Staged();
            _staged[id] = st;
        }

        return st;
    }

    private void SetCuePhase(string id, DirectionPhase phase)
    {
        Staged st;
        if (!_staged.TryGetValue(id, out st)) return;
        for (int i = 0; i < st.Objects.Count; i++)
        {
            DirectionCue cue = st.Objects[i] != null ? st.Objects[i].GetComponent<DirectionCue>() : null;
            if (cue != null) cue.SetPhase(phase);
        }
    }

    private void Cleanup(string id, DirectionPhase phase)
    {
        Staged st;
        if (!_staged.TryGetValue(id, out st)) return;
        _staged.Remove(id);

        for (int i = 0; i < st.Objects.Count; i++)
        {
            GameObject go = st.Objects[i];
            if (go == null) continue;
            DirectionCue cue = go.GetComponent<DirectionCue>();
            if (cue != null)
            {
                if (phase == DirectionPhase.Aborted) cue.Abort();
                else cue.SetPhase(phase);
            }

            Destroy(go);
        }

        for (int i = st.Undo.Count - 1; i >= 0; i--)
        {
            try { st.Undo[i](); }
            catch (Exception ex) { Debug.LogException(ex, this); }
        }
    }

    private LightGroup Group(SpaceId space)
    {
        SpaceId key = SpaceIds.Canonical(space);
        LightGroup g;
        if (!_groups.TryGetValue(key, out g))
        {
            g = LightGroup.Collect(key, Zones());
            _groups[key] = g;
            if (Verbose) Debug.Log("[Direction] 조명 묶음 " + key + ": 라이트 " + g.LightCount + "개");
        }

        return g;
    }

    private SpaceZones Zones()
    {
        if (_zones == null) _zones = FindAnyObjectByType<SpaceZones>();
        return _zones;
    }

    // ── 자리 ───────────────────────────────────────────────

    private static Transform PlayerRoot()
    {
        PlayerSensors hub = PlayerSensors.Active;
        return hub != null ? hub.PlayerRoot : null;
    }

    private static Vector3 PlayerFeet()
    {
        Transform root = PlayerRoot();
        return root != null ? root.position : Vector3.zero;
    }

    /// <summary>디렉터가 준 자리. 모르면(0) 플레이어 앞 <paramref name="ahead"/>m.</summary>
    private static Vector3 PointOr(Vector3 p, float ahead)
    {
        if (p != Vector3.zero) return p;
        Transform root = PlayerRoot();
        if (root == null) return Vector3.zero;
        Vector3 f = root.forward;
        f.y = 0f;
        return root.position + (f.sqrMagnitude > 0.01f ? f.normalized : Vector3.forward) * ahead;
    }

    /// <summary>그 점 아래 바닥(플레이어 콜라이더·트리거 무시). 못 찾으면 플레이어 발 높이에서 0.85m 아래.</summary>
    public static Vector3 FloorBelow(Vector3 p)
    {
        Transform player = PlayerRoot();
        RaycastHit[] hits = Physics.RaycastAll(p + Vector3.up * 1.5f, Vector3.down, 6f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        Vector3 found = Vector3.zero;
        bool any = false;
        for (int i = 0; i < hits.Length; i++)
        {
            if (player != null && hits[i].collider.transform.IsChildOf(player)) continue;
            if (hits[i].distance < best)
            {
                best = hits[i].distance;
                found = hits[i].point;
                any = true;
            }
        }

        if (any) return found;
        return new Vector3(p.x, PlayerFeet().y - 0.85f, p.z);
    }

    private static Vector3 CeilingAbove(Vector3 p)
    {
        Vector3 floor = FloorBelow(p);
        RaycastHit hit;
        if (Physics.Raycast(floor + Vector3.up * 0.5f, Vector3.up, out hit, 5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            return hit.point;
        }

        return floor + Vector3.up * 3f;
    }

    private static void PlaySound(string name, Vector3 at)
    {
        AudioClip clip = Resources.Load<AudioClip>("Direction/" + name);
        if (clip != null) AudioSource.PlayClipAtPoint(clip, at);
    }
}

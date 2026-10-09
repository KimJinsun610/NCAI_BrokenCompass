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

    /// <summary>61차: 세워 두고 플레이어가 보기를 기다리는 대역(<see cref="DirectionPhase.Present"/>). 대면 때 무대로 옮긴다.</summary>
    private sealed class Presented
    {
        public GameObject Go;
        public Vector3 At;
        public readonly Staged Holder = new Staged();
    }

    private readonly Dictionary<string, Presented> _presented = new Dictionary<string, Presented>(StringComparer.Ordinal);

    /// <summary>62차: 조우가 끝났지만 시야에서 벗어나기를 기다리는 대역(소년·시체). 재시작·하루 끝에는 곧바로 지운다.</summary>
    private readonly List<GameObject> _lingering = new List<GameObject>();
    private readonly Dictionary<SpaceId, LightGroup> _groups = new Dictionary<SpaceId, LightGroup>();
    private SpaceZones _zones;
    private DirectionScreenFx _fx;
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

    /// <summary>70차: 세워 두고 플레이어가 보기를 기다리는 연출 ID들.</summary>
    public IEnumerable<string> PresentedIds
    {
        get { return _presented.Keys; }
    }

    /// <summary>
    /// 70차 모형 급습 자리 — 과학실 옆 복도 동쪽 끝, 비상구 유도등 <c>Corridors/Sign_Exit</c>(54, 3.95, 46.39) 아래 바닥.
    /// 과학실 모형(<see cref="ScienceModel"/>)이 복도로 나왔을 때 서는 자리와 같다.
    /// </summary>
    public static readonly Vector3 RushHallSpot = new Vector3(53.3f, 1.5f, 46.4f);

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

    /// <summary>화면 효과(김진선님 공포 화면 톤·울렁임).</summary>
    public DirectionScreenFx Fx
    {
        get
        {
            if (_fx == null)
            {
                _fx = GetComponent<DirectionScreenFx>();
                if (_fx == null) _fx = gameObject.AddComponent<DirectionScreenFx>();
            }

            return _fx;
        }
    }

    private void OnEnable()
    {
        s_active = this;
        if (GetComponent<RedLightSpot>() == null) gameObject.AddComponent<RedLightSpot>();   // 52차 C4 붉은 등
        if (GetComponent<DutyStage>() == null) gameObject.AddComponent<DutyStage>();   // 54차 [근무 지시] 판정 지점
        EventBus.DirectionEmitted += OnDirection;
        EventBus.FinalRuleSettled += OnRuleSettled;
        EventBus.NightRestarted += OnRestarted;
        EventBus.DayEnded += OnDayEnded;
        // 61차: 몹은 먼저 세우고 플레이어가 알아본 뒤 대면 · 「안쪽 깊이」 머무름은 방 상자로 잰다.
        NightRun.EncounterSightGating = true;
        NightRun.DeepInSpaceProbe = DeepIn;
    }

    /// <summary>61차: 플레이어 발이 그 공간 상자 안쪽으로 <paramref name="margin"/>m 넘게(수평, 가장 가까운 벽까지) 들어와 있는지. 상자를 모르면 참.</summary>
    private static bool DeepIn(SpaceId space, float margin)
    {
        SpaceZones z = s_active != null ? s_active.Zones() : FindAnyObjectByType<SpaceZones>();
        Bounds box;
        if (z == null || !z.TryGetSpaceBox(space, out box)) return true;
        Vector3 f = PlayerFeet();
        float depth = Mathf.Min(Mathf.Min(f.x - box.min.x, box.max.x - f.x), Mathf.Min(f.z - box.min.z, box.max.z - f.z));
        return depth >= margin;
    }

    private void OnDisable()
    {
        EventBus.DirectionEmitted -= OnDirection;
        EventBus.FinalRuleSettled -= OnRuleSettled;
        EventBus.NightRestarted -= OnRestarted;
        EventBus.DayEnded -= OnDayEnded;
        NightRun.EncounterSightGating = false;
        NightRun.DeepInSpaceProbe = null;
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
        List<string> shown = new List<string>(_presented.Keys);
        for (int i = 0; i < shown.Count; i++) DropPresented(shown[i]);
        for (int i = 0; i < _lingering.Count; i++) if (_lingering[i] != null) Destroy(_lingering[i]);
        _lingering.Clear();
        foreach (LightGroup g in _groups.Values) g.Restore();
        if (_fx != null) _fx.ClearAll();
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
                if (Impact() != null) Impact().Foreshadow(e.Phase == DirectionPhase.FalseForeshadow);
                if (IsCorpse(e.SourceId))
                {
                    // 51차 시체 낙하 전조: 떨어질 자리 위 천장에서 먼지 + 삐걱(대면 때 같은 자리로 떨어진다).
                    // 60차: 어디서 떨어지든(교실 입구·사다리 방 안 — 방아쇠는 디렉터가 고른다) 늘 플레이어 바로 앞.
                    _corpseSpot = CorpseSpot(out _corpseCeiling);
                    _corpseSpotAt = Time.time;
                    CorpseDrop.Dust(new Vector3(_corpseSpot.x, _corpseCeiling, _corpseSpot.z), e.Duration);
                    PlaySound(e.SourceId + ".foreshadow", new Vector3(_corpseSpot.x, _corpseCeiling, _corpseSpot.z));
                    break;
                }

                PlaySound(e.SourceId + ".foreshadow", PointOr(e.Point, 6f));
                break;
            case DirectionPhase.Present:
                Present(e);
                break;
            case DirectionPhase.Confront:
                Confront(e);
                break;
            case DirectionPhase.WindowClose:
                SetCuePhase(e.SourceId, DirectionPhase.WindowClose);
                if (Impact() != null) Impact().Release();
                // 70차: 급습은 스쳐 갈 때 낸다 · 복도 끝 모형은 떠나지 않는다(옛 release = 사라지는 소리).
                if (e.SourceId != ProgramCatalog.ModelRush && e.SourceId != ProgramCatalog.HallEndFigure) PlaySound(e.SourceId + ".release", PointOr(e.Point, 3f));
                break;
            case DirectionPhase.Result:
            case DirectionPhase.Aborted:
                if (Impact() != null) Impact().Release();
                Cleanup(e.SourceId, e.Phase);
                break;
        }
    }

    /// <summary>
    /// 61차(민: 「몹은 나타나 있되, 플레이어가 몹이 시야에 들어오고 인지한 뒤에 연출이 시작되도록」): 대역만 세운다 — 걷기·움직임·점프스케어·소리·화면 효과 없음.
    /// 플레이어가 알아보면(<see cref="SightProbe"/>) 디렉터에 알리고, 대면(<see cref="Confront"/>)이 이 대역을 그대로 이어 쓴다.
    /// </summary>
    private void Present(DirectionEvent e)
    {
        EncounterScript script = EncounterScripts.Find(e.SourceId);
        if (script == null || script.StandIn.Length == 0) return;
        DropPresented(e.SourceId);
        Cleanup(e.SourceId, DirectionPhase.Aborted);

        Presented pre = new Presented();
        Vector3 player = PlayerFeet();
        Vector3 point = PointOr(e.Point, script.Distance > 0f ? script.Distance : 3f);
        HallFigure hall = HallFigure.Active;
        if (e.SourceId == ProgramCatalog.HallEndFigure && hall != null && hall.Figure != null)
        {
            // 70차: 복도 끝에 선 자 = 그 밤 내내 서 있는 모형 그대로(새로 세우지도, 지우지도 않는다).
            pre.Go = hall.Figure;
            pre.At = hall.Figure.transform.position;
        }
        else
        {
            pre.Go = SpawnAt(pre.Holder, script.StandIn, script.StageAnchor, script.Placement == CuePlacement.CeilingAhead, point, player, script.AnchorId, out pre.At, false);
        }
        _presented[e.SourceId] = pre;
        string id = e.SourceId;
        // 70차: 모형 급습은 복도에서 알아봐야 달려온다(과학실 동쪽 문 틈으로 본 것은 세지 않음 — 벽을 뚫고 달려오게 된다).
        Func<bool> gate = null;
        if (id == ProgramCatalog.ModelRush || id == ProgramCatalog.HallEndFigure) gate = () => SpaceIds.Canonical(NightRun.CurrentSpace) == SpaceId.Corridor;
        SightProbe.Attach(pre.Go, id, () => NightRun.EncounterSeen(id), gate);
        if (Verbose) Debug.Log("[Direction] 대역을 세움(보기를 기다림) — " + id + " @" + pre.At.ToString("F1"));
    }

    private Presented TakePresented(string id)
    {
        Presented pre;
        if (!_presented.TryGetValue(id, out pre)) return null;
        _presented.Remove(id);
        return pre;
    }

    private void DropPresented(string id)
    {
        Presented pre = TakePresented(id);
        if (pre == null) return;
        if (HallFigure.Owns(pre.Go)) Release(pre.Go);   // 70차: 복도 끝 모형은 그대로 둔다
        else if (pre.Go != null) Destroy(pre.Go);
        for (int i = pre.Holder.Undo.Count - 1; i >= 0; i--)
        {
            try { pre.Holder.Undo[i](); }
            catch (Exception ex) { Debug.LogException(ex, this); }
        }
    }

    /// <summary>70차: 계속 남는 대역(복도 끝 모형)에서 연출이 붙인 컴포넌트만 뗀다.</summary>
    private static void Release(GameObject go)
    {
        if (go == null) return;
        SightProbe probe = go.GetComponent<SightProbe>();
        if (probe != null) Destroy(probe);
        DirectionCue cue = go.GetComponent<DirectionCue>();
        if (cue != null) Destroy(cue);
    }

    /// <summary>61차: 세워 둔 대역의 고정 자리가 걷기(<c>WalkTo</c>)를 가지면 대면 때 걷기 시작.</summary>
    private static void StartWalk(string stageAnchor, GameObject placed, Vector3 at)
    {
        StageAnchor fixedAt = StageAnchor.Find(stageAnchor);
        if (fixedAt == null || fixedAt.WalkTo == null || placed == null) return;
        DirectionWalker walker = placed.GetComponent<DirectionWalker>();
        if (walker == null) walker = placed.AddComponent<DirectionWalker>();
        walker.Walk(at, fixedAt.WalkTo.position, fixedAt.WalkSpeed, true);
    }

    private void Confront(DirectionEvent e)
    {
        EncounterScript script = EncounterScripts.Find(e.SourceId);
        if (script == null) return;

        Presented pre = TakePresented(e.SourceId);   // 61차: 세워 둔 대역을 이어 쓴다
        Cleanup(e.SourceId, DirectionPhase.Aborted);
        Staged st = Stage(e.SourceId);
        if (pre != null) st.Undo.AddRange(pre.Holder.Undo);
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
        else if (script.StandIn == EncounterScripts.CorpseStandIn)
        {
            // 51차: 전조 때 고른 자리(플레이어가 1.5m 넘게 움직였으면 다시 고름)에 리지드바디 시체를 떨어뜨린다.
            float ceilingY;
            Vector3 spot = _corpseSpot;
            if (Time.time - _corpseSpotAt > 6f || (FlatDistance(spot, player) > 1.5f))
            {
                spot = CorpseSpot(out ceilingY);
            }
            else
            {
                ceilingY = _corpseCeiling;
            }

            CorpseDrop body = CorpseDrop.Spawn(spot, player, ceilingY, script.AnchorId);
            UnseenDespawn.Mark(body.gameObject);   // 62차
            st.Objects.Add(body.gameObject);
            point = spot;
            StartCoroutine(CorpseRoaches(spot, RoachDelay));   // 60차(민: 「시체 등장할 때 바닥에 바퀴벌레가 — 내가 올린 효과음과 함께」)
            if (Verbose) Debug.Log("[Direction] 시체 낙하 — 플레이어 앞 " + spot.ToString("F1"));
        }
        else if (script.StandIn.Length > 0)
        {
            Vector3 at;
            GameObject go;
            if (pre != null && pre.Go != null)
            {
                go = pre.Go;
                at = pre.At;
                StartWalk(script.StageAnchor, go, at);
            }
            else
            {
                go = SpawnAt(st, script.StandIn, script.StageAnchor, script.Placement == CuePlacement.CeilingAhead, point, player, script.AnchorId, out at);
            }
            if (script.StandIn == "mob.boy") UnseenDespawn.Mark(go);   // 62차: 소년은 시야에서 벗어나야 사라진다
            if (e.SourceId == ProgramCatalog.ModelRush)
            {
                // 70차(민: 「과학실 옆 복도 비상등에서 플레이어 방향으로 뛰어와서 놀래키는 연출」): 대면 = 달려오기 시작. 덮치는 순간 점프스케어.
                point = at;
                string rushId = e.SourceId;
                GameObject rusher = go;
                MannequinRush.Begin(go, lunge =>
                {
                    if (lunge)
                    {
                        if (Impact() != null) Impact().Hit(rushId);
                        BodyMeter.Jolt(1.6f);
                    }
                    else if (rusher != null)
                    {
                        PlaySound(rushId + ".release", rusher.transform.position + Vector3.up * 1.2f, ConfrontMinDistance, ConfrontSpatial);
                    }
                });
            }
            DirectionCue cue = go.GetComponent<DirectionCue>();
            if (cue == null) cue = go.AddComponent<DirectionCue>();
            cue.Play(new CueContext { Intensity = e.Intensity, Anchor = at, EncounterId = e.SourceId });
            st.Objects.Add(go);
            if (EncounterImpact.HitsWhenSeen(e.SourceId))
            {
                // 60차: 도서관 조우(노란 얼굴·창밖 남자)는 마주치는 순간 점프스케어.
                string hitId = e.SourceId;
                SeenStinger.Attach(go, () =>
                {
                    EncounterImpact impact = Impact();
                    if (impact != null) impact.Hit(hitId);
                    if (Verbose) Debug.Log("[Direction] 마주침 — " + hitId);
                });
            }

            if (script.ExtraStandIn == EncounterScripts.CorpseStandIn)
            {
                // 소년 머리 박기(C2 교차): 사다리 위 천장 구멍에서 시체가 떨어진다.
                StageAnchor hole = StageAnchor.Find(script.ExtraStageAnchor);
                Vector3 from = hole != null ? hole.transform.position : CeilingAbove(PointOr(Vector3.zero, 3f));
                Vector3 floor = FloorBelow(from + Vector3.down * 0.5f);
                CorpseDrop body = CorpseDrop.Spawn(floor, player, from.y, script.ExtraAnchorId);
                UnseenDespawn.Mark(body.gameObject);   // 62차
                st.Objects.Add(body.gameObject);
            }
            else if (script.ExtraStandIn.Length > 0)
            {
                Vector3 ceiling;
                GameObject extra = SpawnAt(st, script.ExtraStandIn, script.ExtraStageAnchor, true, PointOr(Vector3.zero, 3f), player, script.ExtraAnchorId, out ceiling);
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

        ApplyScreenFx(e.SourceId, st);
        if (Impact() != null) Impact().Confront(e.SourceId);
        // 대면 소리는 가깝고 크게(최소 거리 6m, 75%만 3D) — 4~8m 앞 몹의 소리가 PlayClipAtPoint(최소 1m)로 묻혔다(44차).
        PlaySound(e.SourceId + ".confront", point, ConfrontMinDistance, ConfrontSpatial);
    }

    private Vector3 _corpseSpot;
    private float _corpseCeiling;
    private float _corpseSpotAt = -100f;
    /// <summary>61차: 사다리 방(창고, 문간 x≈50.3 포함) 상자 — 2026-10-07 실측(사다리 C-3 (52.7, 33.2), 문간 z 33.6~34.4).</summary>
    public static readonly Bounds LadderRoom = new Bounds(new Vector3(52.3f, 2.5f, 33.9f), new Vector3(4.8f, 3f, 4.0f));

    /// <summary>61차: 사다리 방 시체 자리 — 문간에서 사다리를 볼 때 화면 가운데에 오는 바닥(민 스크린샷).</summary>
    public static readonly Vector3 LadderRoomCorpseSpot = new Vector3(51.8f, 1.5f, 33.9f);

    /// <summary>바퀴벌레 이펙트 대역(김진선님 벌레 떼를 감싼 것, 빌더가 만든다).</summary>
    public const string CorpseRoachesId = "fx.corpse.roaches";

    /// <summary>시체가 바닥에 닿을 즈음(초) 바퀴벌레가 기어 나온다.</summary>
    public const float RoachDelay = 0.55f;

    /// <summary>바퀴벌레가 쏟아지는 높이(m) — 바닥 바로 위.</summary>
    public const float RoachDropHeight = 0.45f;

    /// <summary>
    /// 60차: 시체가 떨어진 바닥에서 바퀴벌레가 사방으로 기어 나온다(김진선님 BugSwarm 타임라인 — 천장이 아니라 바닥 바로 위에서 쏟아져 기어간다).
    /// 시체 점프스케어 소리(민이 준 <c>stinger.corpse</c>)는 대면 때 이미 난다. 14초 뒤 스스로 지운다.
    /// </summary>
    private static IEnumerator CorpseRoaches(Vector3 floor, float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        GameObject prefab = Resources.Load<GameObject>("StandIns/" + CorpseRoachesId);
        if (prefab == null) yield break;
        GameObject fx = Instantiate(prefab, floor, Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f));
        fx.name = "시체 바퀴벌레";
        // 벌레 무리를 바닥 바로 위(0.45m)에 둔다 — 바닥에 쏟아져 닿자마자 사방으로 기어간다(기는 입자는 떨어진 벌레가 바닥에 닿을 때 생기는 하위 방출이라 떨어지는 줄기를 끌 수 없다).
        foreach (Transform t in fx.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == "Swarm") t.position = floor + Vector3.up * RoachDropHeight;
        }

        foreach (HorrorTriggerZone z in fx.GetComponentsInChildren<HorrorTriggerZone>(true))
        {
            z.enabled = false;
            Collider c = z.GetComponent<Collider>();
            if (c != null) c.enabled = false;
        }

        HorrorEvent he = fx.GetComponentInChildren<HorrorEvent>(true);
        if (he != null) he.Play();   // 타임라인: 벌레 무리 켜기 + 기어가는 소리
        Destroy(fx, 14f);
    }

    private static bool IsCorpse(string encounterId)
    {
        EncounterScript s = EncounterScripts.Find(encounterId);
        return s != null && s.StandIn == EncounterScripts.CorpseStandIn;
    }

    private static float FlatDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    /// <summary>
    /// 시체가 떨어질 바닥 점 — 카메라 정면에서 20° 옆(트인 쪽) 1.1m. 막혔으면 반대쪽·정면·0.9m 순. <paramref name="ceilingY"/> = 그 위 천장 높이.
    /// </summary>
    private static Vector3 CorpseSpot(out float ceilingY)
    {
        Vector3 feet = PlayerFeet();
        Transform me = PlayerRoot();

        // 61차(민: 「사다리 방은 너무 어두워 시체가 나와도 못 보는 경우가 있다 — 2번째 사진 조준점 자리에서 떨어지게」):
        // 사다리 방 안(문간 포함)이면 늘 정해 둔 자리 — 문간에서 사다리를 볼 때 화면 가운데 바닥. 플레이어가 그 자리에 서 있으면 평소대로.
        Vector3 flatFeet = new Vector3(feet.x, LadderRoom.center.y, feet.z);
        if (LadderRoom.Contains(flatFeet) && FlatDistance(feet, LadderRoomCorpseSpot) > 0.8f)
        {
            Vector3 fixedFloor = FloorBelow(LadderRoomCorpseSpot + Vector3.up * 0.3f);
            ceilingY = CeilingY(fixedFloor, me);
            return fixedFloor;
        }

        Camera cam = Camera.main;
        Vector3 fwd = cam != null ? cam.transform.forward : (PlayerRoot() != null ? PlayerRoot().forward : Vector3.forward);
        fwd.y = 0f;
        fwd = fwd.sqrMagnitude > 0.0001f ? fwd.normalized : Vector3.forward;
        float side = UnityEngine.Random.value < 0.5f ? 1f : -1f;
        float[] angles = { 20f * side, -20f * side, 0f, 35f * side, -35f * side };
        float[] dists = { 1.1f, 0.9f };
        Vector3 chest = feet + Vector3.up * 1.2f;
        Transform player = PlayerRoot();
        for (int d = 0; d < dists.Length; d++)
        {
            for (int a = 0; a < angles.Length; a++)
            {
                Vector3 dir = Quaternion.Euler(0f, angles[a], 0f) * fwd;
                if (Blocked(chest, dir, dists[d] + 0.35f, player)) continue;
                Vector3 floor = FloorBelow(feet + dir * dists[d] + Vector3.up * 0.3f);
                ceilingY = CeilingY(floor, player);
                return floor;
            }
        }

        Vector3 fallback = FloorBelow(feet + fwd * 0.9f + Vector3.up * 0.3f);
        ceilingY = CeilingY(fallback, player);
        return fallback;
    }

    private static bool Blocked(Vector3 from, Vector3 dir, float dist, Transform player)
    {
        foreach (RaycastHit h in Physics.RaycastAll(from, dir, dist, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (player != null && h.collider.transform.IsChildOf(player)) continue;
            return true;
        }

        return false;
    }

    private static float CeilingY(Vector3 floor, Transform player)
    {
        float best = float.MaxValue;
        foreach (RaycastHit h in Physics.RaycastAll(floor + Vector3.up * 0.5f, Vector3.up, 5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (player != null && h.collider.transform.IsChildOf(player)) continue;
            if (h.point.y < best) best = h.point.y;
        }

        return best < float.MaxValue ? best : floor.y + 3f;
    }

    private const float ConfrontMinDistance = 6f;
    private const float ConfrontSpatial = 0.75f;

    private EncounterImpact Impact()
    {
        EncounterImpact impact = EncounterImpact.Active;
        if (impact == null) impact = gameObject.AddComponent<EncounterImpact>();
        return impact;
    }

    /// <summary>
    /// 조우 대면에 화면 톤을 붙인다(「화면 효과 가이드」 §5 추천 조합). 조우가 끝나면(결과·중단) 내린다.
    /// 판정 결과(위반)에는 붙이지 않는다 — 위반 즉시 피드백 금기.
    /// </summary>
    private void ApplyScreenFx(string encounterId, Staged st)
    {
        DirectionScreenFx fx = Fx;
        string key = "enc." + encounterId;
        switch (encounterId)
        {
            case ProgramCatalog.ModelRush:
            case ProgramCatalog.HallEndFigure:
                // 인체모형이 나타남 — 숨막힘 1.5초에 0.8까지 → 조우 동안 유지 → 1.5초에 0.
                fx.Push(key, DirectionScreenFx.Kind.Suffocate, 0.8f, 1.5f, -1f, 1.5f);
                break;
            case ProgramCatalog.ScienceBlackout:
            case ProgramCatalog.ToiletBlackout:
                // 불이 나가는 순간 — 순간 암전 0.1초 두 번.
                fx.Flash(key, DirectionScreenFx.Kind.Blackout, 2, 1f);
                break;
            case ProgramCatalog.YellowFace:
            case ProgramCatalog.SuitMan:
                // 노란 남자(문간·창밖) — 이질감 0.6 + 울렁임 0.4.
                fx.Push(key, DirectionScreenFx.Kind.Wrongness, 0.6f, 1f, -1f, 1.5f);
                fx.Push(key, DirectionScreenFx.Kind.Wobble, 0.4f, 1f, -1f, 1.5f);
                break;
            default:
                return;
        }

        st.Undo.Add(() => fx.Release(key));
    }

    /// <summary>가짜 놀람을 씬의 연출 자리(김진선님 캐비닛 연출)로 낸다. 근처에 쓸 자리가 없으면 false.</summary>
    private bool PlayFakeSpot(string fakeId)
    {
        Transform player = PlayerRoot();
        if (player == null) return false;
        // 보이는 자리를 먼저, 그다음 가까운 자리(벌레 떼는 방마다 자리가 여럿 — 등 뒤에서 쏟아지면 소리만 남는다).
        Camera cam = Camera.main;
        DirectionFakeSpot best = null;
        float bestD = float.MaxValue;
        bool bestSeen = false;
        foreach (DirectionFakeSpot spot in FindObjectsByType<DirectionFakeSpot>(FindObjectsSortMode.None))
        {
            if (spot.FakeId != fakeId) continue;
            HorrorEvent he = spot.GetComponent<HorrorEvent>();
            if (he == null || he.IsPlaying || (!spot.Replayable && he.HasPlayed)) continue;
            Vector3 d = spot.transform.position - player.position;
            d.y = 0f;
            float dist = d.magnitude;
            if (dist > spot.MaxDistance) continue;
            bool seen = Seen(cam, spot.transform.position + Vector3.up * 2.2f, spot.transform);
            if (bestSeen && !seen) continue;
            if (seen == bestSeen && dist >= bestD) continue;
            best = spot;
            bestD = dist;
            bestSeen = seen;
        }

        if (best == null) return false;
        best.GetComponent<HorrorEvent>().Play();
        if (Verbose) Debug.Log("[Direction] 가짜 놀람 " + fakeId + " ← " + best.name + " (" + bestD.ToString("F1") + "m" + (bestSeen ? ", 시야 안" : ", 시야 밖") + ")");
        return true;
    }

    /// <summary>카메라 앞쪽(약 ±55°)이고 사이에 막는 것이 없는가(그 자리 자신의 충돌체는 무시).</summary>
    private static bool Seen(Camera cam, Vector3 point, Transform self)
    {
        if (cam == null) return false;
        Vector3 from = cam.transform.position;
        Vector3 to = point - from;
        if (Vector3.Dot(cam.transform.forward, to.normalized) < 0.57f) return false;
        RaycastHit[] hits = Physics.RaycastAll(from, to.normalized, to.magnitude, ~0, QueryTriggerInteraction.Ignore);
        foreach (RaycastHit h in hits)
        {
            if (h.collider.transform.IsChildOf(self)) continue;
            if (h.collider.GetComponentInParent<CharacterController>() != null) continue;
            return false;
        }

        return true;
    }

    /// <summary>
    /// 대역을 세운다. 고정 자리(<see cref="StageAnchor"/>)가 씬에 있으면 그 자리·방향(+응시 상자), 없으면 디렉터가 준 점의 바닥(천장)에서 플레이어를 보게.
    /// </summary>
    private static GameObject SpawnAt(Staged st, string standIn, string stageAnchor, bool ceiling, Vector3 point, Vector3 player, string anchorId, out Vector3 at, bool walk = true)
    {
        StageAnchor fixedAt = StageAnchor.Find(stageAnchor);
        if (fixedAt == null && stageAnchor == StageAnchors.RushHall)
        {
            // 70차: 씬에 자리를 두지 않은 코드 자리 — 복도 끝 비상등 아래에서 복도(서쪽)를 본다.
            at = FloorBelow(RushHallSpot);
            return StandInFactory.Create(standIn, at, Quaternion.LookRotation(Vector3.left, Vector3.up), anchorId);
        }

        if (fixedAt != null)
        {
            at = fixedAt.transform.position;
            GameObject placed = StandInFactory.Create(standIn, at, fixedAt.transform.rotation, anchorId);
            if (fixedAt.GazeProxy != null) StandInFactory.ApplyGazeProxy(placed, fixedAt.GazeProxy);
            if (fixedAt.RevealDoor != null) RevealDoor(st, fixedAt.RevealDoor);
            // 고정 천장 앵커도 구멍을 낸다 — 멀쩡한 천장 타일을 다리가 뚫고 나오면 소품처럼 보인다(43차 시뮬).
            if (standIn == "mob.legs") AddCeilingHole(placed, at);
            if (fixedAt.WalkTo != null && walk)   // 61차: 세워 둘 때(Present)는 걷지 않는다 — 대면 때 StartWalk
            {
                DirectionWalker walker = placed.GetComponent<DirectionWalker>();
                if (walker == null) walker = placed.AddComponent<DirectionWalker>();
                walker.Walk(at, fixedAt.WalkTo.position, fixedAt.WalkSpeed, true);
            }
            else if (fixedAt.WalkTo != null)
            {
                DirectionWalker.Hold(placed, at, fixedAt.WalkTo.position);   // 70차: 걸을 쪽을 향해 첫 자세로 서서 기다린다
            }

            return placed;
        }

        // 디렉터가 준 점(플레이어 앞 n m)이 벽 너머면 보이지 않는다 — 시야가 트인 쪽·거리로 당긴다(43차 시뮬: 과학실에서 모형 급습이 남쪽 벽 너머 교실에 섰다).
        point = InSight(point, player);
        if (standIn == "prop.phantomdoor")
        {
            // 없던 문은 벽에 붙어 있어야 한다(43차: 교실 한가운데 문짝만 서 있었다).
            Vector3 wallAt;
            Quaternion facing;
            if (OnWall(point, player, out wallAt, out facing))
            {
                at = wallAt;
                return StandInFactory.Create(standIn, at, facing, anchorId);
            }
        }

        // 사람 나무는 복도 한복판(길이 방향)에 한쪽 벽에 기대 선다(몹 연출 장면 「나무 조우」, 44차: 플레이어 앞 8m가 교실 출입구에 걸렸다).
        // 폭 한가운데면 H1(1.2m 안 접근 금지) 때문에 좁은 복도가 통째로 막혀 되돌아갈 수밖에 없다 — 벽 쪽 0.65m에 두어 반대편으로 1.2m 넘게 비켜 지나갈 수 있게.
        if (standIn == "mob.tree") point = TreeSpot(point, player);
        at = ceiling ? CeilingAbove(point) : FloorBelow(point);
        GameObject go = StandInFactory.Create(standIn, at, player, anchorId);
        if (standIn == "mob.legs") AddCeilingHole(go, at);
        if (standIn == "mob.tree") AddTrunk(go);
        return go;
    }

    /// <summary>
    /// 사람 나무 자리: 플레이어가 보는 쪽에 가까운 축(±x·±z) 가운데 5m 넘게 트인 쪽으로 4~9m, 그 단면이 복도(방 상자 밖·복도 상자 안)이고
    /// 양옆 벽이 3.5m 안이면 넓은 쪽 벽에서 <see cref="TreeWallGap"/>m 떨어진 자리. 못 찾으면 디렉터의 점 그대로.
    /// (44차: 플레이어 앞 8m 점이 교실 문을 지나 교실 안에 섰다.)
    /// </summary>
    private static Vector3 TreeSpot(Vector3 point, Vector3 feet)
    {
        Transform root = PlayerRoot();
        Vector3 fwd = root != null ? root.forward : point - feet;
        fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward;
        fwd.Normalize();
        List<Vector3> axes = new List<Vector3> { Vector3.right, Vector3.left, Vector3.forward, Vector3.back };
        axes.Sort((x, y) => Vector3.Dot(y, fwd).CompareTo(Vector3.Dot(x, fwd)));
        for (int i = 0; i < axes.Count; i++)
        {
            float free = Clearance(feet, axes[i], 14f);
            if (free < 5f) continue;
            Vector3 c = feet + axes[i] * Mathf.Clamp(free - 1.5f, 4f, 9f);
            Vector3 spot;
            if (TryCorridorSpot(c, feet, TreeWallGap, out spot)) return new Vector3(spot.x, point.y, spot.z);
        }

        return point;
    }

    /// <summary>복도 상자 안이고 어느 방 상자에도 들지 않는지(발 높이 +1m로 본다).</summary>
    private static bool InCorridor(Vector3 p, float feetY)
    {
        SpaceZones zones = FindAnyObjectByType<SpaceZones>();
        if (zones == null) return true;
        Vector3 q = new Vector3(p.x, feetY + 1f, p.z);
        Bounds box;
        if (zones.TryGetSpaceBox(SpaceId.Corridor, out box) && !box.Contains(q)) return false;
        SpaceId[] rooms = { SpaceId.Classroom_1_1, SpaceId.Classroom_1_3, SpaceId.ScienceRoom, SpaceId.Toilet, SpaceId.Library, SpaceId.SecurityRoom };
        for (int i = 0; i < rooms.Length; i++)
        {
            if (zones.TryGetSpaceBox(rooms[i], out box) && box.Contains(q)) return false;
        }

        return true;
    }

    private const float TreeWallGap = 0.65f;

    /// <summary>
    /// 그 점 근처(진행 방향으로 0·−1·+1·−2·+2·−3m)에서 양옆 벽이 모두 3.5m 안에 있는(출입구·갈림길이 아닌) 복도 단면을 찾아,
    /// 한쪽 벽에서 <paramref name="wallGap"/>m 떨어진 자리(0이면 한가운데). 플레이어에게서 그 자리가 트여 보여야 한다.
    /// </summary>
    private static bool TryCorridorSpot(Vector3 point, Vector3 feet, float wallGap, out Vector3 spot)
    {
        spot = point;
        Vector3 axis = point - feet;
        axis.y = 0f;
        float dist = axis.magnitude;
        if (dist < 1f) return false;
        axis /= dist;
        Vector3 across = Vector3.Cross(Vector3.up, axis);
        float[] shifts = { 0f, -1f, 1f, -2f, 2f, -3f };
        for (int i = 0; i < shifts.Length; i++)
        {
            float d = dist + shifts[i];
            if (d < 3f) continue;
            Vector3 c = feet + axis * d;
            float left = SideWall(c, across, 3.5f);
            float right = SideWall(c, -across, 3.5f);
            if (left < 0f || right < 0f) continue;
            Vector3 mid = c + across * ((left - right) * 0.5f);
            float half = (left + right) * 0.5f;
            if (wallGap > 0f && half > wallGap) mid += across * ((half - wallGap) * (UnityEngine.Random.value < 0.5f ? 1f : -1f));
            if (!InCorridor(mid, feet.y)) continue;
            Vector3 to = mid - feet;
            to.y = 0f;
            float want = to.magnitude;
            if (want < 2f || Clearance(feet, to / want, want) < want - 0.6f) continue;
            spot = mid;
            return true;
        }

        return false;
    }

    /// <summary>그 점에서 옆으로 허리 높이 수직면까지 거리. 못 찾으면 −1.</summary>
    private static float SideWall(Vector3 c, Vector3 dir, float max)
    {
        Transform player = PlayerRoot();
        RaycastHit[] hits = Physics.RaycastAll(new Vector3(c.x, c.y + 1.2f, c.z), dir, max, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        float best = -1f;
        for (int i = 0; i < hits.Length; i++)
        {
            if (player != null && hits[i].collider.transform.IsChildOf(player)) continue;
            if (Mathf.Abs(hits[i].normal.y) > 0.3f) continue;
            if (best < 0f || hits[i].distance < best) best = hits[i].distance;
        }

        return best;
    }

    /// <summary>
    /// 사람 나무 줄기에 단단한 충돌체(44차: 걸어서 통과됐다). 반지름 0.55m — 플레이어(0.3m)가 붙어 서면 H1 근접(1.2m) 안이라 「다가가지 않기」 판정은 그대로다.
    /// 김진선님 프리팹은 그대로, 세운 인스턴스에만 붙인다.
    /// </summary>
    private static void AddTrunk(GameObject tree)
    {
        if (tree == null) return;
        Bounds b = new Bounds(tree.transform.position + Vector3.up, Vector3.one * 0.2f);
        bool any = false;
        foreach (Renderer r in tree.GetComponentsInChildren<Renderer>())
        {
            if (!any)
            {
                b = r.bounds;
                any = true;
            }
            else
            {
                b.Encapsulate(r.bounds);
            }
        }

        GameObject trunk = new GameObject("사람 나무 줄기");
        trunk.transform.SetParent(tree.transform, false);
        trunk.transform.position = new Vector3(tree.transform.position.x, b.center.y, tree.transform.position.z);
        trunk.transform.rotation = Quaternion.identity;
        CapsuleCollider cap = trunk.AddComponent<CapsuleCollider>();
        float scale = Mathf.Max(0.0001f, tree.transform.lossyScale.x);
        cap.direction = 1;
        cap.radius = 0.55f / scale;
        cap.height = Mathf.Max(1.8f, b.size.y) / scale;
    }

    /// <summary>
    /// 플레이어 발(<paramref name="feet"/>)에서 그 점 쪽으로 허리·눈 높이가 트여 있으면 그대로, 막혔으면 조금씩 돌려(±25·50·80·120·180°) 트인 방향,
    /// 다 막혔으면 가장 멀리 트인 방향에 둔다. 높이(천장 다리의 +2.4m 등)는 그대로.
    /// </summary>
    private static Vector3 InSight(Vector3 point, Vector3 feet)
    {
        Vector3 to = point - feet;
        float dy = to.y;
        to.y = 0f;
        float want = to.magnitude;
        if (want < 0.5f) return point;
        Vector3 dir = to / want;
        float[] turns = { 0f, 25f, -25f, 50f, -50f, 80f, -80f, 120f, -120f, 180f };
        Vector3 best = point;
        float bestGot = -1f;
        for (int i = 0; i < turns.Length; i++)
        {
            Vector3 d = Quaternion.Euler(0f, turns[i], 0f) * dir;
            float got = Mathf.Min(want, Clearance(feet, d, want + 0.7f) - 0.7f);
            if (got >= want - 0.01f) return feet + d * want + Vector3.up * dy;
            if (got > bestGot)
            {
                bestGot = got;
                best = feet + d * Mathf.Max(1.2f, got) + Vector3.up * dy;
            }
        }

        return best;
    }

    private static float Clearance(Vector3 feet, Vector3 dir, float max)
    {
        Transform player = PlayerRoot();
        float free = max;
        float[] heights = { 1.1f, 1.7f };   // 책상 위·눈 높이 — 책상은 넘겨 보이므로 막힘으로 치지 않는다
        for (int k = 0; k < heights.Length; k++)
        {
            RaycastHit[] hits = Physics.RaycastAll(feet + Vector3.up * heights[k], dir, max, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hits.Length; i++)
            {
                if (player != null && hits[i].collider.transform.IsChildOf(player)) continue;
                if (hits[i].distance < free) free = hits[i].distance;
            }
        }

        return free;
    }

    /// <summary>그 점 쪽으로 보이는 첫 수직면이 벽이면 그 벽에 붙인 자리·방향(문 앞면이 방 쪽). 2~12m 안에서 못 찾으면 false.</summary>
    private static bool OnWall(Vector3 point, Vector3 feet, out Vector3 at, out Quaternion facing)
    {
        at = point;
        facing = Quaternion.identity;
        Transform player = PlayerRoot();
        Vector3 to = point - feet;
        to.y = 0f;
        Vector3 dir = to.sqrMagnitude > 0.01f ? to.normalized : (player != null ? Vector3.ProjectOnPlane(player.forward, Vector3.up).normalized : Vector3.forward);
        float[] turns = { 0f, 30f, -30f, 60f, -60f, 90f, -90f };
        for (int t = 0; t < turns.Length; t++)
        {
            Vector3 d = Quaternion.Euler(0f, turns[t], 0f) * dir;
            RaycastHit[] hits = Physics.RaycastAll(feet + Vector3.up * 1.2f, d, 12f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (x, y) => x.distance.CompareTo(y.distance));
            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit h = hits[i];
                if (player != null && h.collider.transform.IsChildOf(player)) continue;
                if (Mathf.Abs(h.normal.y) > 0.3f || h.distance < 2f) break;                  // 처음 막는 것이 바닥·너무 가까움
                if (h.collider.name.IndexOf("Wall", System.StringComparison.OrdinalIgnoreCase) < 0) break;   // 벽이 아닌 가구가 먼저 막는다
                Vector3 n = h.normal;
                n.y = 0f;
                n.Normalize();
                Vector3 p = h.point + n * 0.05f;
                at = new Vector3(p.x, feet.y, p.z);   // 같은 층 — 벽 앞 가구 윗면을 바닥으로 잡지 않게 플레이어 발 높이
                facing = Quaternion.LookRotation(n, Vector3.up);
                return true;
            }
        }

        return false;
    }

    private static Texture2D s_holeTex;

    /// <summary>천장 다리가 매달린 자리에 검은 구멍(43차: 멀쩡한 천장 판을 뚫고 다리가 나와 있었다). 다리와 함께 거둔다.</summary>
    private static void AddCeilingHole(GameObject legs, Vector3 ceiling)
    {
        if (legs == null) return;
        if (s_holeTex == null)
        {
            const int n = 64;
            s_holeTex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            s_holeTex.wrapMode = TextureWrapMode.Clamp;
            Color32[] px = new Color32[n * n];
            System.Random rng = new System.Random(77);
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n * 2f - 1f;
                    float v = (y + 0.5f) / n * 2f - 1f;
                    float ang = Mathf.Atan2(v, u);
                    float edge = 0.78f + 0.08f * Mathf.Sin(ang * 5f + 1.3f) + 0.05f * Mathf.Sin(ang * 9f + 0.4f);
                    float rad = Mathf.Sqrt(u * u + v * v);
                    float a = Mathf.Clamp01((edge - rad) / 0.12f);
                    px[y * n + x] = new Color32(0, 0, 0, (byte)(a * 255f));
                }
            }

            s_holeTex.SetPixels32(px);
            s_holeTex.Apply();
        }

        GameObject hole = GameObject.CreatePrimitive(PrimitiveType.Quad);
        hole.name = "천장 구멍";
        Collider c = hole.GetComponent<Collider>();
        if (c != null) UnityEngine.Object.Destroy(c);
        hole.transform.SetParent(legs.transform, true);
        hole.transform.SetPositionAndRotation(ceiling + Vector3.down * 0.015f, Quaternion.LookRotation(Vector3.up, Vector3.forward));
        hole.transform.localScale = new Vector3(0.95f, 0.95f, 1f) * (1f / Mathf.Max(0.0001f, legs.transform.lossyScale.x));
        Renderer r = hole.GetComponent<Renderer>();
        Material m = new Material(Shader.Find("Sprites/Default"));
        m.mainTexture = s_holeTex;
        m.color = Color.black;
        r.sharedMaterial = m;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    /// <summary>
    /// 대면 동안 그 문을 열어 둔다(문간의 노란 얼굴). 잠긴 문이면 그동안만 풀고, 끝나면 닫고 다시 잠근다.
    /// 문 발신기(DoorRelay)가 없는 문만 쓴다 — 있으면 연출 개방이 H2 방아쇠로 잡힌다.
    /// </summary>
    private static void RevealDoor(Staged st, Transform door)
    {
        DoorHandle h = DoorHandle.Of(door);
        if (!h.IsValid) return;
        if (PlayerInteractor.IsSealed(h))
        {
            Debug.LogWarning("[Direction] 쓰지 않는 문(판자로 막힌 문 등)은 연출로 열지 않습니다: " + door.name, door);
            return;
        }

        if (door.GetComponentInParent<DoorRelay>() != null || door.GetComponentInChildren<DoorRelay>() != null)
        {
            Debug.LogWarning("[Direction] 문 발신기가 달린 문은 연출로 열지 않습니다(H2 방아쇠가 됨): " + door.name, door);
            return;
        }

        bool wasLocked = h.IsLocked;
        bool wasOpen = h.IsOpen;
        if (wasLocked) h.ForceUnlock();
        if (!wasOpen) h.Open();
        st.Undo.Add(() =>
        {
            DoorHandle back = DoorHandle.Of(door);
            if (!back.IsValid) return;
            if (!wasOpen) back.Close();
            if (wasLocked) back.ForceLock();
        });
    }

    // ── 피날레 몹(배역표 FinaleCast — 팀원 프리팹이 들어가는 자리) ─────────────────

    private static string FinaleStageId(FinaleRole role)
    {
        return "finale." + role;
    }

    /// <summary>
    /// 피날레 배역을 고정 자리에 세운다(이미 서 있으면 그것). 무대 목록에 올리므로 밤 재시작·하루 끝·무대 정리 때 함께 거둔다.
    /// 피날레 흐름(11단계)과 디버그 콘솔이 같은 입구를 쓴다. 자리가 없으면 null.
    /// </summary>
    public FinaleMob StageFinale(FinaleRole role)
    {
        FinaleMob mob = FinaleOf(role);
        if (mob != null) return mob;

        mob = FinaleMob.Spawn(role);
        if (mob == null) return null;
        Staged st = Stage(FinaleStageId(role));
        st.Objects.Add(mob.gameObject);
        return mob;
    }

    /// <summary>피날레 배역만 남기고 무대를 거둔다(피날레 「모든 몹이 제자리에서 사라져 있다」). 조명·화면 효과도 되돌린다.</summary>
    public void ClearAllExceptFinale()
    {
        List<string> ids = new List<string>(_staged.Keys);
        for (int i = 0; i < ids.Count; i++)
        {
            if (ids[i].StartsWith("finale.", StringComparison.Ordinal)) continue;
            Cleanup(ids[i], DirectionPhase.Aborted);
        }

        foreach (LightGroup g in _groups.Values) g.Restore();
        if (_fx != null) _fx.ClearAll();
    }

    /// <summary>서 있는 피날레 배역. 없으면 null.</summary>
    public FinaleMob FinaleOf(FinaleRole role)
    {
        Staged st;
        if (!_staged.TryGetValue(FinaleStageId(role), out st)) return null;
        for (int i = 0; i < st.Objects.Count; i++)
        {
            if (st.Objects[i] == null) continue;
            FinaleMob mob = st.Objects[i].GetComponent<FinaleMob>();
            if (mob != null) return mob;
        }

        return null;
    }

    /// <summary>피날레 배역을 거둔다.</summary>
    public void ClearFinale(FinaleRole role)
    {
        Cleanup(FinaleStageId(role), DirectionPhase.Aborted);
    }

    /// <summary>경비실 창밖 정장 남자를 세우거나 치운다(디버그 미리 보기). 세우면 「나타남」 → 「두드림」.</summary>
    public bool ToggleFinaleWindowMan()
    {
        if (FinaleOf(FinaleRole.WindowMan) != null)
        {
            ClearFinale(FinaleRole.WindowMan);
            return false;
        }

        FinaleMob mob = StageFinale(FinaleRole.WindowMan);
        if (mob == null) return false;
        StartCoroutine(AppearThenKnock(mob));
        return true;
    }

    private static IEnumerator AppearThenKnock(FinaleMob mob)
    {
        yield return mob.PlayAndWait(FinaleBeat.Appear);
        if (mob != null && mob.CurrentBeat == FinaleBeat.Appear) mob.Play(FinaleBeat.Knock);
    }

    private void SpawnCctvPerson(Staged st)
    {
        CctvSystem cctv = CctvSystem.Active;
        int ch = cctv != null ? cctv.CurrentChannel : -1;
        Camera cam = cctv != null ? cctv.ChannelCamera(ch) : null;
        if (cam == null) return;

        // 71차(민: 「CCTV 등장 장소 다양화 — 이벤트가 결정되면 장소도 함께 결정 · 결정된 장소에서 실제로 등장·이동」):
        // 자리는 밤 시작에 정해 둔 것(NightRun.CctvPersonSpot — 디렉터는 그 채널을 볼 때 조우를 건다). 슬롯 끝 무렵 다른 채널에서 걸렸으면 그 채널의 자리 표에서.
        CctvSpot spot = NightRun.CctvPersonSpot;
        if (spot == null || spot.Channel != ch)
        {
            List<CctvSpot> here = CctvSpots.InChannel(ch);
            spot = here.Count > 0 ? here[UnityEngine.Random.Range(0, here.Count)] : null;
        }

        Vector3 from, to;
        if (!CctvWalker.TryResolve(cam, spot, out from, out to) && !CctvScreenPath(cam, out from, out to))
        {
            Debug.LogWarning("[Direction] CCTV 사람: 채널 " + ch + " 화면에 보이는 바닥을 찾지 못했다");
            return;
        }

        if (Verbose) Debug.Log("[Direction] CCTV 사람 " + (spot != null ? spot.ToString() : "채널 " + ch + " 화면 바닥") + " " + from.ToString("F1") + " → " + to.ToString("F1"));
        CctvWalker walker = CctvWalker.Create(from, to, ch, "CCTV 사람");
        if (walker == null) return;
        st.Objects.Add(walker.gameObject);
        // 52차 K1 「화면 속 !_ 이 지나갈 때까지 채널을 넘기지 마십시오」: 한 번 가로지르고 끝에서 사라진다.
        // 3초 이어서 보면 얼굴 점프스케어(3일차부터, 밤당 한 번) — 그 뒤에도 사라진다.
        CctvOnlyVisible only = walker.Only;
        System.Action vanish = () =>
        {
            if (only != null) only.enabled = false;
        };
        walker.Walk(from, to, false, 0f, vanish);
        CctvFaceScare.Register(walker.gameObject, ch, vanish);
    }

    /// <summary>자리 표가 맞지 않을 때 — 그 채널 카메라 화면 아래쪽 격자로 실제로 보이는 바닥 두 점(왼편 → 오른편)을 고른다(57차).</summary>
    private static bool CctvScreenPath(Camera cam, out Vector3 from, out Vector3 to)
    {
        from = Vector3.zero;
        to = Vector3.zero;
        bool hasFrom = false, hasTo = false;
        float[] rows = { 0.25f, 0.35f, 0.15f, 0.45f };
        float[] lefts = { 0.3f, 0.4f, 0.5f };
        float[] rights = { 0.7f, 0.6f, 0.5f };
        foreach (float vy in rows)
        {
            if (!hasFrom) foreach (float vx in lefts) if (CctvFloor(cam, vx, vy, out from)) { hasFrom = true; break; }
            if (!hasTo) foreach (float vx in rights) if (CctvFloor(cam, vx, vy, out to)) { hasTo = true; break; }
        }

        if (!hasFrom && !hasTo) return false;
        if (!hasFrom) from = to;
        if (!hasTo || (to - from).sqrMagnitude < 1f)
        {
            Vector3 side = cam.transform.right;   // 좁은 화면: 화면 가로로 1.5m
            side.y = 0f;
            to = from + side.normalized * 1.5f;
        }
        return true;
    }

    /// <summary>CCTV 채널 카메라 화면의 한 점(뷰포트)에서 레이를 쏴 플레이어 발 높이의 바닥을 찾는다(57차).</summary>
    private static bool CctvFloor(Camera cam, float vx, float vy, out Vector3 p)
    {
        p = Vector3.zero;
        RaycastHit h;
        if (!Physics.Raycast(cam.ViewportPointToRay(new Vector3(vx, vy, 0f)), out h, 25f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return false;
        // 57차 플레이 점검: 플레이어 루트는 캡슐 가운데(바닥 + 0.85m)라 루트 높이와 견주면 모든 바닥이 떨어졌다 — 발밑 바닥 높이와 견준다.
        if (h.normal.y < 0.7f || h.distance < 2.5f || Mathf.Abs(h.point.y - PlayerGroundY()) > 0.4f) return false;
        p = h.point;
        return true;
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
                // 44차: 정규화 공간(Classroom)의 묶음은 1-1 교실 상자였다 — 1-3 교실에 있어도 1-1 등이 붉어졌다. 플레이어가 선 교실의 등.
                SpaceId here = ClassroomHere();
                LightGroup g = here != SpaceId.None ? ExactGroup(here) : Group(SpaceId.Classroom);
                // 44차: 교실 등은 평소 꺼져 있다(RoomDarkness) — 어둠 속에 붉은 등이 확 들어오도록 세기 0.7 → 2.4배.
                g.Tint(RedLightSpot.CueColor, 0.6f);   // 67차: 1.5 → 0.6(초록은 같은 세기에서 붉은빛보다 훨씬 밝아 교실 전체가 빛나 보였다)   // 57차(민: 「붉은 조명 밝기 줄이기」): 2.4 → 1.5 · 66차: 초록(조도 축이 오르면 모든 등이 붉어지므로)
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
            if (!h.IsValid || h.IsOpen || h.IsLocked) continue;

            // 쓰지 않는 문(정책 Sealed)은 연출로도 열지 않는다(2026-10-01 민). 동선의 문은 시작할 때 잠금이 풀려 있다.
            if (PlayerInteractor.Classify(h) != DoorPolicySO.Kind.Openable) continue;
            if (ProximityDoors.Covers(h.Owner)) continue;   // 66차 ③: 진선님 문이 서 있는 자리(숨은 씬 문만 열리면 어긋난다)
            bool designated = relay.DoorId == AutoOpenDoorId;   // 과학실 둘째 문 — 레벨이 「저절로 열리는 문」으로 지정
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
        best.BeginDirectionMove(2f);
        chosen.Open();
        if (Verbose) Debug.Log("[Direction] H2: 문 자동 개방 — " + best.DoorId);
    }

    private void OnFake(DirectionEvent e)
    {
        // 57차: 점검 대상을 보고 있으면(보고 준비) 가짜 놀람을 내지 않는다 — 놀람을 이상 소리로 착각해 오보(코어 FakeBlocked의 뒷받침).
        if (InspectionSensor.Active != null && !string.IsNullOrEmpty(InspectionSensor.Active.Focus)) return;

        if (e.SourceId == TensionDirector.FakeGlimpse)
        {
            DistantGlimpse.Play();
            return;
        }

        // 57차(민: 「메탈 쾅은 굉장히 멀리서 들리는 듯이」): 사물함 쾅은 등 뒤 22m 남짓에서, 벽 너머처럼 먹먹하게 — 화면 암전도 없이.
        if (e.SourceId == "fake.locker.rattle")
        {
            PlayFar(e.SourceId, 22f);
            return;
        }

        if (e.SourceId == "fake.flashlight.flicker")
        {
            // 56차: 배터리가 반 아래면 가짜 끊김을 내지 않는다 — 배터리 탓으로 읽히면 놀라지 않는다(진짜 깜빡임은 30% 아래부터).
            if (NightRun.Battery != null && NightRun.Battery.Charge < BatteryRules.FakeFlickerMinCharge) return;
            StartCoroutine(FlickerFlashlight());
            PlaySound(e.SourceId, PlayerFeet() + Vector3.up * 1.2f);   // 손전등 지지직(2026-10-04 사운드)
            return;
        }

        if (PlayFakeSpot(e.SourceId))
        {
            // 캐비닛이 쾅 — 순간 암전 두 번(가이드 §5). 삐걱 열림은 화면을 건드리지 않는다.
            if (e.SourceId == "fake.locker.rattle") Fx.Flash("fake." + Time.frameCount, DirectionScreenFx.Kind.Blackout, 2, 0.7f);
            return;
        }

        PlaySound(e.SourceId, AwayFromPlayer(9f));   // 57차: 발밑(e.Point)에서 울리면 너무 가깝다
    }

    /// <summary>플레이어 등 뒤쪽(±70°) <paramref name="dist"/>m 지점.</summary>
    private static Vector3 AwayFromPlayer(float dist)
    {
        Transform root = PlayerRoot();
        if (root == null) return Vector3.zero;
        Vector3 back = -Vector3.ProjectOnPlane(root.forward, Vector3.up);
        if (back.sqrMagnitude < 0.01f) back = Vector3.back;
        Vector3 dir = Quaternion.Euler(0f, UnityEngine.Random.Range(-70f, 70f), 0f) * back.normalized;
        return root.position + dir * dist + Vector3.up * 1.2f;
    }

    /// <summary>먼 소리(57차) — 저역만 남기고 복도 잔향을 걸어 벽 너머에서 들리게.</summary>
    private static void PlayFar(string name, float dist)
    {
        float volume;
        AudioClip clip = DirectionSoundTableSO.Find(name, out volume);
        if (clip == null) return;
        GameObject go = new GameObject("연출 소리(먼) " + clip.name);
        go.transform.position = AwayFromPlayer(dist);
        AudioSource s = go.AddComponent<AudioSource>();
        s.playOnAwake = false;
        s.clip = clip;
        s.volume = volume;
        s.spatialBlend = 1f;
        s.rolloffMode = AudioRolloffMode.Logarithmic;
        s.minDistance = 3f;
        s.maxDistance = 45f;
        s.dopplerLevel = 0f;
        s.priority = 16;
        go.AddComponent<AudioLowPassFilter>().cutoffFrequency = 900f;
        go.AddComponent<AudioReverbFilter>().reverbPreset = AudioReverbPreset.Hallway;
        NightDutyMixer.Route(s, NightDutyMixer.Bus.Direction);   // 67차
        s.Play();
        Destroy(go, clip.length + 2.5f);
        if (Verbose) Debug.Log("[Direction] 먼 소리 " + name + " ← " + clip.name + " @" + go.transform.position.ToString("F1"));
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
        // 52차: 앉은 소년을 3초 바라보면(C2 위반) 책상에 머리를 세 번 박고 엎드린다(소리는 복도까지).
        Staged boy;
        if (r.RuleId == "C2" && r.Outcome == FinalOutcome.Violated && _staged.TryGetValue(ProgramCatalog.BoyBang, out boy))
        {
            for (int i = 0; i < boy.Objects.Count; i++)
            {
                if (boy.Objects[i] != null) BoyHeadBang.Play(boy.Objects[i]);
            }

            if (Verbose) Debug.Log("[Direction] 소년 머리 박기(C2 위반)");
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
        DropPresented(id);   // 61차: 못 보고 거둔(중단) 대역
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

            // 58차(민: 문·창문의 오리는 사라질 때 빠르게 걸어 나가며): 결과 단계면 걸어 나가고 스스로 지운다. 중단(붙잡힘·04:00)은 바로 지운다.
            StandInExit exit = phase == DirectionPhase.Result ? go.GetComponent<StandInExit>() : null;
            if (exit != null)
            {
                exit.Leave();
                continue;
            }

            // 70차: 복도 끝 모형은 그 밤 내내 서 있다 — 연출이 붙인 것만 떼고 그대로 둔다.
            if (HallFigure.Owns(go))
            {
                Release(go);
                continue;
            }

            // 70차: 걷는 중인 대역(화장실 소녀)은 끝점까지 마저 걷고 스스로 사라진다. 중단은 바로 지운다(_lingering).
            DirectionWalker walking = phase != DirectionPhase.Aborted ? go.GetComponent<DirectionWalker>() : null;
            if (walking != null && walking.FinishThenDestroy())
            {
                _lingering.RemoveAll(x => x == null);
                _lingering.Add(go);
                continue;
            }

            // 62차(민: 「소년, 시체의 디스폰은 플레이어 시야에서 완전히 벗어나면 사라지게」): 보이는 동안은 남겨 둔다. 중단(붙잡힘·재시작·04:00)은 바로 지운다.
            if (phase != DirectionPhase.Aborted && go.GetComponent<UnseenDespawn>() != null)
            {
                UnseenDespawn.Begin(go);
                _lingering.RemoveAll(x => x == null);
                _lingering.Add(go);
                continue;
            }

            Destroy(go);
        }

        for (int i = st.Undo.Count - 1; i >= 0; i--)
        {
            try { st.Undo[i](); }
            catch (Exception ex) { Debug.LogException(ex, this); }
        }
    }

    /// <summary>정규화하지 않은 그 방 하나의 조명 묶음(교실 두 곳을 가른다).</summary>
    private LightGroup ExactGroup(SpaceId exact)
    {
        LightGroup g;
        if (!_groups.TryGetValue(exact, out g))
        {
            g = LightGroup.Collect(exact, Zones());
            _groups[exact] = g;
        }

        return g;
    }

    /// <summary>플레이어 발이 든 교실(1-1·1-3). 어느 교실도 아니면 None.</summary>
    private SpaceId ClassroomHere()
    {
        SpaceZones zones = Zones();
        if (zones == null) return SpaceId.None;
        Vector3 feet = PlayerFeet() + Vector3.up * 0.5f;
        SpaceId[] rooms = { SpaceId.Classroom_1_1, SpaceId.Classroom_1_3 };
        for (int i = 0; i < rooms.Length; i++)
        {
            Bounds box;
            if (zones.TryGetSpaceBox(rooms[i], out box) && box.Contains(feet)) return rooms[i];
        }

        return SpaceId.None;
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

    /// <summary>
    /// 플레이어가 선 바닥의 높이(57차). <see cref="PlayerFeet"/>(루트)는 캡슐 가운데라 실제 발보다 0.85m 위다 — 루트에서 아래로 쏜 첫 바닥, 없으면 루트 − 0.85.
    /// </summary>
    public static float PlayerGroundY()
    {
        Transform root = PlayerRoot();
        if (root == null) return 0f;
        RaycastHit hit;
        if (Physics.Raycast(root.position + Vector3.up * 0.1f, Vector3.down, out hit, 3f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) && hit.normal.y > 0.5f) return hit.point.y;
        return root.position.y - 0.85f;
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

    /// <summary>이름으로 연출 소리를 그 자리에서 3D로 낸다(<c>Resources/Direction/이름</c> → 소리 표). 없으면 false.</summary>
    internal static bool PlaySound(string name, Vector3 at)
    {
        return PlaySound(name, at, SoundMinDistance, 1f);
    }

    // 연출 소리의 거리 감쇠: 최소 3m(전에는 PlayClipAtPoint 기본 1m — 6m 앞 소리가 1/6로 줄었다, 44차).
    private const float SoundMinDistance = 3f;
    private const float SoundMaxDistance = 40f;

    /// <summary>최소 거리·3D 비율을 정해 낸다(대면은 가깝고 크게).</summary>
    internal static bool PlaySound(string name, Vector3 at, float minDistance, float spatial)
    {
        float volume = 1f;
        AudioClip clip = Resources.Load<AudioClip>("Direction/" + name);
        if (clip == null) clip = DirectionSoundTableSO.Find(name, out volume);   // 표(다른 폴더의 클립을 이름으로 묶음).
        if (clip == null) return false;
        PlayClip(clip, at, volume, minDistance, spatial);
        if (Verbose) Debug.Log("[Direction] 소리 " + name + " ← " + clip.name);

        // 같은 순간 겹치는 둘째 소리(C4 = 칠판 긁기 + 교탁 의자). 공통(*) 대체는 쓰지 않는다.
        float v2;
        AudioClip layer = DirectionSoundTableSO.FindExact(name + "+", out v2);
        if (layer != null) PlayClip(layer, at, v2, minDistance, spatial);
        return true;
    }

    private static void PlayClip(AudioClip clip, Vector3 at, float volume, float minDistance, float spatial)
    {
        GameObject go = new GameObject("연출 소리 " + clip.name);
        go.transform.position = at;
        AudioSource s = go.AddComponent<AudioSource>();
        s.clip = clip;
        s.volume = volume;
        s.spatialBlend = spatial;
        s.rolloffMode = AudioRolloffMode.Logarithmic;
        s.minDistance = minDistance;
        s.maxDistance = SoundMaxDistance;
        s.dopplerLevel = 0f;
        s.priority = 16;
        NightDutyMixer.Route(s, NightDutyMixer.Bus.Direction);   // 67차: 연출 버스 + 반쯤 3D(0.75)는 완전 3D로 — 방향이 또렷하게
        s.Play();
        Destroy(go, clip.length + 0.2f);
    }
}

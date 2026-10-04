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
                PlaySound(e.SourceId + ".foreshadow", PointOr(e.Point, 6f));
                break;
            case DirectionPhase.Confront:
                Confront(e);
                break;
            case DirectionPhase.WindowClose:
                SetCuePhase(e.SourceId, DirectionPhase.WindowClose);
                if (Impact() != null) Impact().Release();
                PlaySound(e.SourceId + ".release", PointOr(e.Point, 3f));
                break;
            case DirectionPhase.Result:
            case DirectionPhase.Aborted:
                if (Impact() != null) Impact().Release();
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
            Vector3 at;
            GameObject go = SpawnAt(st, script.StandIn, script.StageAnchor, script.Placement == CuePlacement.CeilingAhead, point, player, script.AnchorId, out at);
            DirectionCue cue = go.GetComponent<DirectionCue>();
            if (cue == null) cue = go.AddComponent<DirectionCue>();
            cue.Play(new CueContext { Intensity = e.Intensity, Anchor = at, EncounterId = e.SourceId });
            st.Objects.Add(go);

            if (script.ExtraStandIn.Length > 0)
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
    private static GameObject SpawnAt(Staged st, string standIn, string stageAnchor, bool ceiling, Vector3 point, Vector3 player, string anchorId, out Vector3 at)
    {
        StageAnchor fixedAt = StageAnchor.Find(stageAnchor);
        if (fixedAt != null)
        {
            at = fixedAt.transform.position;
            GameObject placed = StandInFactory.Create(standIn, at, fixedAt.transform.rotation, anchorId);
            if (fixedAt.GazeProxy != null) StandInFactory.ApplyGazeProxy(placed, fixedAt.GazeProxy);
            if (fixedAt.RevealDoor != null) RevealDoor(st, fixedAt.RevealDoor);
            // 고정 천장 앵커도 구멍을 낸다 — 멀쩡한 천장 타일을 다리가 뚫고 나오면 소품처럼 보인다(43차 시뮬).
            if (standIn == "mob.legs") AddCeilingHole(placed, at);
            if (fixedAt.WalkTo != null)
            {
                DirectionWalker walker = placed.GetComponent<DirectionWalker>();
                if (walker == null) walker = placed.AddComponent<DirectionWalker>();
                walker.Walk(at, fixedAt.WalkTo.position, fixedAt.WalkSpeed, true);
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
                // 44차: 정규화 공간(Classroom)의 묶음은 1-1 교실 상자였다 — 1-3 교실에 있어도 1-1 등이 붉어졌다. 플레이어가 선 교실의 등.
                SpaceId here = ClassroomHere();
                LightGroup g = here != SpaceId.None ? ExactGroup(here) : Group(SpaceId.Classroom);
                // 44차: 교실 등은 평소 꺼져 있다(RoomDarkness) — 어둠 속에 붉은 등이 확 들어오도록 세기 0.7 → 2.4배.
                g.Tint(new Color(1f, 0.12f, 0.08f), 2.4f);
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
        if (e.SourceId == "fake.flashlight.flicker")
        {
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
        s.Play();
        Destroy(go, clip.length + 0.2f);
    }
}

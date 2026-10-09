using System;
using System.Collections.Generic;
using NightDuty;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// 점검 이상의 <b>눈에 보이는 모습</b>(최종 기획서 「공간별 설계」, 2026-10-04 41차). 밤 시작에 확정된 점검 편성(<see cref="InspectionPlan"/>)에서
/// 이상인 항목마다 [옮김]·[켬]·[빛] 연출을 그 밤 내내 세워 두고, 편성이 바뀌면(다음 날·새 밤) 모두 되돌린다. 재시작은 편성을 바꾸지 않으므로 그대로 둔다.
/// 강도는 <see cref="InspectionAssignment.Intensity"/>(구간 1~4)이고 수치는 <see cref="AnomalyLook"/>. <b>판정과 무관하다</b> — 판정은 보고가 한다.
/// <list type="bullet">
/// <item>[옮김] S-1 인체 모형이 돌아섬 · C-1 화분이 창가 반대편 책상 위로 · L-1 의자 하나가 빠져 출입구를 향함.
/// 정적 배칭으로 묶인 소품(C-1·L-1)은 원본을 숨기고 같은 프리팹을 옮긴 자리에 세운다(<see cref="InspectionAnomalyPropsSO"/>). 점검 기준점은 소품을 따라간다(42차).</item>
/// <item>[켬] H-2 식수대 앞 물(웅덩이 → 줄기 → 침수) · T-1 변기에 검은 머리카락, 넘친 물 · K-1 한 채널에 CCTV에만 보이는 사람이 천천히 지나감.</item>
/// <item>[빛] H-1 압력계 · C-2 책상 램프 · S-2 현미경 불 · T-3 거울 위 형광등 깜빡임(<see cref="PhotosensitiveSafe"/>면 2Hz 아래의 느린 맥동) · L-2 블라인드 하나가 올라가 달빛.
/// 빛 이상은 색과 함께 대상 둘레의 후광 링(정적인 모양 단서)을 같이 쓴다(<see cref="HaloRing"/>). 단 S-2 현미경은 링 없이 바닥 조명과 접안렌즈 빛점으로만 보인다(49차 민: 「UI스러운 원」).</item>
/// </list>
/// 「가까이」 연출과 [소리] 틀은 여기서 다루지 않는다. 근무 씬에 자동으로 선다.
/// </summary>
[DisallowMultipleComponent]
public sealed class InspectionAnomalies : MonoBehaviour
{
    /// <summary>광과민 옵션 — 켜면 T-3의 깜빡임이 2Hz 아래의 느린 맥동이 된다.</summary>
    public static bool PhotosensitiveSafe;

    /// <summary>빛 이상의 후광 링(최종 기획서 접근성: 색과 함께 정적인 모양 단서).</summary>
    public static bool HaloRing = false;   // 53차 민(C-2 램프 스크린샷): 「UI처럼 보인다」 — 빛 이상 후광 링을 모두 끈다(49차 S-2에 이어)

    /// <summary>
    /// 66차 실시간 조작(민 피드백 6단계 「실시간 조작」 — 원문이 남아 있지 않아 이렇게 읽었다): 그날 늦게 열리는 공간(호출 2)의 이상은 밤 시작에 세우지 않고,
    /// 그 점검이 열린 뒤 플레이어가 보지 않고 4m 넘게 떨어져 있을 때 세운다 — 앞서 지나가며 본 정상 모습이 나중에 바뀌어 있다. 판정(편성)은 그대로다. 끄면 옛 동작.
    /// </summary>
    public static bool LiveTamper = true;

    /// <summary>실시간 조작을 기다리는 동안 플레이어와 떨어져 있어야 하는 거리(m).</summary>
    public const float TamperMinDistance = 4f;

    /// <summary>[빛] 이상의 세기 배수(53차 민: 「빛과 관련된 점검 물품들은 빛이 아주 약하게」).</summary>
    public static float LightAnomalyScale = 0.3f;

    private static readonly float[] SpreadWidthByBand = { 0f, 0.6f, 0.45f, 1.2f, 2.4f };

    private sealed class Look
    {
        public string ItemId;
        public Band Band;
        public readonly List<GameObject> Objects = new List<GameObject>();
        public readonly List<Action> Undo = new List<Action>();
        public readonly List<Flicker> Flickers = new List<Flicker>();
        public Walker Walker;
        public Transform Spin;   // 66차 S-4: 세로축 둘레로 도는 것(원본 또는 대역)
        public Vector3 SpinPivot;
        public float SpinSpeed;
    }

    private sealed class Flicker
    {
        public Light Light;
        public float Base;
        public Renderer Glow;
        public Color GlowColor;
        public float Hz;
        public float Seed;
        public Material Emit;   // 66차 S-5: 발광 재질(사본) — 깜빡임에 맞춰 발광 세기
        public Color EmitColor;
    }

    private sealed class Walker
    {
        public GameObject Go;
        public CctvWalker Person;   // 71차: 걷기 동작으로 실제로 걷는다
        public int Channel = -1;
    }

    /// <summary>카메라를 보는 카드. 벽에 붙은 대상에서 링이 벽에 잘리지 않게 카메라 쪽으로 조금 띄운다.</summary>
    private struct Bill
    {
        public Transform T;
        public Vector3 Anchor;
        public float Push;
    }

    /// <summary>
    /// 후광 링의 거리 감쇠(44차: 멀리서도 또렷한 링이 게임 UI처럼 보였다). 2.5m 안은 다 보이고 6.5m 밖은 사라진다 —
    /// 점검하러 다가갔을 때 대상 둘레에 번진 빛으로 읽히게.
    /// </summary>
    private struct HaloFade
    {
        public Material Mat;
        public Vector3 Anchor;
        public float Alpha;
    }

    private const float HaloFullDistance = 2.5f;
    private const float HaloGoneDistance = 6.5f;

    private static Texture2D s_ring;
    private static Texture2D s_dot;
    private static Texture2D s_bar;
    private static Texture2D s_hair;
    private static Texture2D s_footprint;

    private readonly List<Look> _looks = new List<Look>();
    private readonly List<InspectionAssignment> _pending = new List<InspectionAssignment>();
    private readonly List<Bill> _billboards = new List<Bill>();
    private readonly List<HaloFade> _halos = new List<HaloFade>();
    private readonly List<Material> _materials = new List<Material>();
    private InspectionPlan _plan;
    private InspectionAnomalyPropsSO _props;

    /// <summary>지금 근무 씬의 것. 없으면 null.</summary>
    public static InspectionAnomalies Active { get; private set; }

    // ── 자동 설치 ─────────────────────────────────────────────

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Install()
    {
        Active = null;
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
        if (!FlowAutoInstall.IsDutyScene(scene) || FlowAutoInstall.Exists<InspectionAnomalies>(scene)) return;
        FlowAutoInstall.CreateHost<InspectionAnomalies>(scene, "InspectionAnomalies (auto)");
    }

    private void OnEnable()
    {
        Active = this;
    }

    private void OnDisable()
    {
        ClearAll();
        if (Active == this) Active = null;
    }

    private void OnDestroy()
    {
        for (int i = 0; i < _materials.Count; i++)
        {
            if (_materials[i] != null) Destroy(_materials[i]);
        }

        _materials.Clear();
    }

    // ── 공개 ─────────────────────────────────────────────────

    /// <summary>지금 세워 둔 이상(「S-2[2] H-4[1]」). 디버그·검수.</summary>
    public string Summary
    {
        get
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int i = 0; i < _looks.Count; i++)
            {
                if (i > 0) sb.Append(' ');
                sb.Append(_looks[i].ItemId).Append('[').Append((int)_looks[i].Band).Append(']');
            }

            return sb.ToString();
        }
    }

    /// <summary>그 항목의 이상이 세워져 있는지.</summary>
    public bool IsApplied(string itemId)
    {
        return FindLook(itemId) != null;
    }

    /// <summary>
    /// 잠깐만 세운다(CCTV 다시보기 한 컷). 되돌리는 함수를 돌려주고, 이미 서 있거나 세울 것이 없으면 null.
    /// 지운 것은 그 프레임이 그려지기 전에 사라진다.
    /// </summary>
    public Action Preview(string itemId, Band band)
    {
        if (IsApplied(itemId) || itemId == "K-1" || itemId == TrashCanKick.ItemId) return null;   // 65차: H-4는 다가갈 때 걷어차이는 사건이라 한 컷 미리 보기가 없다
        Look look = Build(itemId, band);
        if (look == null) return null;
        return () => Remove(look);
    }

    /// <summary>디버그: 그 항목의 이상을 지금 세운다(이미 있으면 다시). 세웠으면 true.</summary>
    public bool DebugApply(string itemId, Band band)
    {
        Look old = FindLook(itemId);
        if (old != null)
        {
            Remove(old);
            _looks.Remove(old);
        }

        Look look = Build(itemId, band < Band.Band1 ? Band.Band1 : band);
        if (look == null) return false;
        _looks.Add(look);
        return true;
    }

    /// <summary>디버그: 세워 둔 것을 모두 거둔다(같은 편성이면 다시 세우지 않는다).</summary>
    public void DebugClear()
    {
        ClearAll();
    }

    // ── 편성 따라가기 ────────────────────────────────────────

    private void Update()
    {
        InspectionPlan plan = NightRun.Inspections != null ? NightRun.Inspections.Plan : null;
        if (!ReferenceEquals(plan, _plan))
        {
            _plan = plan;
            ClearAll();
            if (plan != null)
            {
                IReadOnlyList<InspectionAssignment> rows = plan.Assignments;
                for (int i = 0; i < rows.Count; i++)
                {
                    if (!rows[i].IsAnomaly) continue;
                    if (LiveTamper && rows[i].IsLate && AnomalyLook.HasLook(rows[i].Item) && rows[i].Id != "K-1")
                    {
                        _pending.Add(rows[i]);   // 66차: 실시간 조작 — 점검이 열린 뒤 보지 않을 때 세운다
                        continue;
                    }

                    Look look = Build(rows[i].Id, rows[i].Intensity);
                    if (look != null) _looks.Add(look);
                }

                if (DirectionStage.Verbose && _looks.Count > 0) Debug.Log("[InspectionAnomalies] " + plan.Day + "일차 이상 연출: " + Summary);
            }
        }

        // 51차 T4: 여자아이가 들어간 칸의 변기 — 목격 뒤 지시가 나가면 머리카락이 쌓여 있다(정답은 [정상] = 역보고).
        if (plan != null && NightRun.Inspections != null)
        {
            string t1 = InspectionCatalog.ReverseReportItem;
            InspectionBoard board = NightRun.Inspections;
            if (board.IsReverse(t1) && board.IsIssued(t1) && FindLook(t1) == null)
            {
                InspectionAssignment row = plan.Find(t1);
                Look look = row != null ? Build(t1, Band.Band2) : null;
                if (look != null) _looks.Add(look);
            }
        }

        TickTamper();

        for (int i = 0; i < _looks.Count; i++)
        {
            Look look = _looks[i];
            for (int f = 0; f < look.Flickers.Count; f++) Tick(look.Flickers[f]);
            if (look.ItemId == "K-1") TickWalker(look);
            if (look.Spin != null) look.Spin.RotateAround(look.SpinPivot, Vector3.up, look.SpinSpeed * Time.deltaTime);
        }
    }

    private void LateUpdate()
    {
        Camera cam = Camera.main;
        if (cam == null) return;
        Vector3 eye = cam.transform.position;
        for (int i = _billboards.Count - 1; i >= 0; i--)
        {
            Bill b = _billboards[i];
            if (b.T == null)
            {
                _billboards.RemoveAt(i);
                continue;
            }

            Vector3 to = b.Anchor - eye;
            if (to.sqrMagnitude < 0.0001f) continue;
            float push = Mathf.Min(b.Push, to.magnitude * 0.5f);
            b.T.SetPositionAndRotation(b.Anchor - to.normalized * push, Quaternion.LookRotation(to, Vector3.up));
        }

        for (int i = _halos.Count - 1; i >= 0; i--)
        {
            HaloFade h = _halos[i];
            if (h.Mat == null)
            {
                _halos.RemoveAt(i);
                continue;
            }

            float near = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(HaloFullDistance, HaloGoneDistance, Vector3.Distance(eye, h.Anchor)));
            Color c = h.Mat.color;
            c.a = h.Alpha * near;
            h.Mat.color = c;
        }
    }

    /// <summary>66차: 실시간 조작을 기다리는 이상 수.</summary>
    public int PendingTamperCount
    {
        get { return _pending.Count; }
    }

    /// <summary>66차: 기다리는 이상 — 점검이 열렸고, 소품이 화면에 안 보이고, 플레이어가 4m 넘게 떨어져 있으면 그때 세운다.</summary>
    private void TickTamper()
    {
        if (_pending.Count == 0) return;
        InspectionBoard board = NightRun.Inspections;
        Camera cam = Camera.main;
        Transform player = PlayerSensors.Active != null ? PlayerSensors.Active.PlayerRoot : null;
        if (board == null || cam == null) return;
        for (int i = _pending.Count - 1; i >= 0; i--)
        {
            InspectionAssignment row = _pending[i];
            if (!board.IsIssued(row.Id)) continue;
            JudgeTarget jt;
            if (!JudgeTargetRegistry.TryGet(row.Item.TargetId, out jt) || jt == null || jt.transform.parent == null) continue;
            Transform prop = jt.transform.parent;
            if (player != null && SensingRules.HorizontalDistance(player.position, jt.transform.position) < TamperMinDistance) continue;
            if (SeenNow(prop, cam)) continue;
            _pending.RemoveAt(i);
            if (FindLook(row.Id) != null) continue;
            Look look = Build(row.Id, row.Intensity);
            if (look == null) continue;
            _looks.Add(look);
            if (DirectionStage.Verbose) Debug.Log("[InspectionAnomalies] 실시간 조작: " + row.Id + "[" + (int)row.Intensity + "]");
        }
    }

    private static bool SeenNow(Transform prop, Camera cam)
    {
        OutlineGroup group = prop.GetComponent<OutlineGroup>();
        if (group == null) return UnseenDespawn.VisibleTo(prop.gameObject, cam);
        Renderer[] members = group.Members;
        for (int i = 0; i < members.Length; i++)
        {
            if (members[i] != null && UnseenDespawn.VisibleTo(members[i].gameObject, cam)) return true;
        }

        return false;
    }

    private void ClearAll()
    {
        _pending.Clear();
        for (int i = _looks.Count - 1; i >= 0; i--) Remove(_looks[i]);
        _looks.Clear();
        _halos.Clear();
    }

    private Look FindLook(string itemId)
    {
        for (int i = 0; i < _looks.Count; i++)
        {
            if (_looks[i].ItemId == itemId) return _looks[i];
        }

        return null;
    }

    private static void Remove(Look look)
    {
        for (int i = look.Undo.Count - 1; i >= 0; i--)
        {
            try
            {
                look.Undo[i]();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        look.Undo.Clear();
        for (int i = 0; i < look.Objects.Count; i++)
        {
            if (look.Objects[i] != null) Destroy(look.Objects[i]);
        }

        look.Objects.Clear();
        look.Flickers.Clear();
        look.Walker = null;
        look.Spin = null;
    }

    // ── 항목별 ───────────────────────────────────────────────

    private Look Build(string itemId, Band band)
    {
        InspectionItem item = InspectionCatalog.Find(itemId);
        // 57차: S-3 개수대는 [소리] 틀이지만 물줄기도 보인다(민: 「시각적으로 확인할 수 있게」) — 판정·HasLook은 그대로.
        if (!AnomalyLook.HasLook(item) && itemId != FaucetItem) return null;

        Look look = new Look { ItemId = itemId, Band = band };
        if (itemId == "K-1")
        {
            look.Walker = new Walker();
            return look;
        }

        JudgeTarget jt;
        if (!JudgeTargetRegistry.TryGet(item.TargetId, out jt) || jt == null || jt.transform.parent == null)
        {
            Debug.LogWarning("[InspectionAnomalies] 씬에 " + item.TargetId + "의 대상 소품이 없어 " + itemId + " 이상을 세우지 못했습니다.");
            return null;
        }

        Transform prop = jt.transform.parent;
        Bounds b = PropBounds(prop, jt.transform);
        float glow = AnomalyLook.Glow(band);
        bool ok = true;
        switch (itemId)
        {
            case "H-1":
            {
                Vector3 at = new Vector3(b.center.x, b.max.y - 0.1f, b.center.z) + Flat(prop.forward) * (b.extents.z + 0.015f);
                // 53차 플레이 점검: 범위 0.3m 점광원은 벽에 테두리가 또렷한 초록 원판을 그렸다(「UI 같은 원」) — 범위를 넓히고 세기를 1/10로 해 번지게.
                Light l = Glow(look, at, new Color(0.45f, 1f, 0.55f), 1.4f, 0.05f * glow, 0.045f);
                AddFlicker(look, l, _lastDot, band, UnityEngine.Random.value * 100f);   // 65차(민: 「빛 관련 지시는 그냥 켜져 있는 게 아니라 이상이면 깜빡이게」)
                Halo(look, b, new Color(0.5f, 1f, 0.6f));
                break;
            }
            case "C-2":
            {
                Vector3 at = new Vector3(b.center.x, b.max.y - 0.1f, b.center.z) + Flat(prop.forward) * 0.1f;
                Light l = Glow(look, at, new Color(1f, 0.78f, 0.5f), 2.6f, 1.6f * glow, 0.08f);
                AddFlicker(look, l, _lastDot, band, UnityEngine.Random.value * 100f);   // 65차: 깜빡임
                Halo(look, b, new Color(1f, 0.8f, 0.5f));
                break;
            }
            case "S-2":
            {
                // 49차: 후광 링을 뺐다(민: 「UI스러운 원」) — 바닥 조명이 실험대를 조금 더 넓게 비추고, 접안렌즈 끝에 작은 빛점 하나.
                Color cold = new Color(0.82f, 0.9f, 1f);
                // 53차 플레이 점검: 빛을 현미경 바닥 안쪽에 두니 몸체가 0.1m 거리에서 하얗게 타 보였다 — 몸체 옆 0.15m · 재물대 높이로 빼고 세기를 1/4로.
                Vector3 at = new Vector3(b.center.x, b.min.y + b.size.y * 0.3f, b.center.z) + Flat(prop.forward) * 0.15f;
                Light l = Glow(look, at, cold, 1.25f, 0.22f * glow, 0.04f);
                float seed = UnityEngine.Random.value * 100f;
                AddFlicker(look, l, _lastDot, band, seed);   // 65차: 깜빡임 — 접안렌즈 빛점도 같은 박자로
                Renderer eye = Card(look, "접안렌즈 빛", Dot(), new Vector3(b.center.x, b.max.y - 0.02f, b.center.z), new Vector2(0.025f, 0.025f), new Color(cold.r, cold.g, cold.b, 0.85f), 0.02f);
                AddFlicker(look, null, eye, band, seed);
                break;
            }
            case "T-3":
                Mirror(look, prop, b, band, glow);
                break;
            case "L-2":
                Blind(look, prop, jt.transform, b, glow);
                break;
            case "S-1":
            {
                Vector3 p = prop.position;
                Quaternion r = prop.rotation;
                prop.RotateAround(b.center, Vector3.up, AnomalyLook.TurnDegrees(band));
                look.Undo.Add(() =>
                {
                    if (prop != null) prop.SetPositionAndRotation(p, r);
                });
                break;
            }
            case "C-1":
                ok = PlantOddPlace(look, prop, jt.transform, b, band);   // 52차: 책상 위 · 천장에 거꾸로 · 벽에 옆으로
                break;
            case "L-1":
                ok = ChairOut(look, prop, jt.transform, band);
                break;
            case "H-2":
                // 44차: 김진선님 「피 식수대」(HorrorEvent_BloodyFountain) — 물이 고이고 떨어지다가, 앞에 다가서면 8초에 걸쳐 검붉게 변한다. 없으면 옛 절차 물.
                if (!FountainEvent(look, prop, b, band)) Water(look, prop, b, band, Flat(prop.forward), new Color(0.015f, 0.02f, 0.025f, 0.8f), 0.25f, false);
                break;
            case "T-1":
            {
                // 61차(민: 「소녀가 칸에 들어간 뒤 변기에 핏물·머리카락」 — 진선님 에셋): 김진선님 핏물·머리카락 변기를 겹친다. 없으면 옛 절차 물·머리카락.
                GameObject blood = ToiletBowls.Blood(prop);
                if (blood != null)
                {
                    look.Objects.Add(blood);
                    Transform toilet = prop;
                    look.Undo.Add(() => ToiletBowls.Restore(toilet));
                    break;
                }

                Water(look, prop, b, band, Flat(prop.forward), new Color(0.03f, 0.028f, 0.02f, 0.82f), 0.3f, true);
                Hair(look, prop, b, band);
                break;
            }
            case FaucetItem:
                Faucet(look, prop, jt.transform, b, band);   // 57차: 잠겼어야 할 수도에서 물이 떨어진다
                break;
            case "C-3":
                ok = HideProp(look, prop, jt.transform);   // 51차: 사다리가 없다(점검 기준점만 남는다)
                if (ok) EmptyMark(look, prop, b);   // 65차(민: 「사다리 없는 자리에서 없다고 보고할 수 있도록」): 빈자리에 자국 — 외곽선이 그려지고 [없음]으로 보고
                break;
            case "H-4":
            {
                // 65차: 쓰레기통 — 복도에서 다가가 보면 누가 걷어찬 듯 날아간다(TrashCanKick). 정상이면 아무 일 없음.
                TrashCanKick kick = TrashCanKick.Active;
                ok = kick != null && kick.Arm();
                if (ok) look.Undo.Add(() =>
                {
                    if (kick != null) kick.Disarm();
                });
                break;
            }
            // ── 66차(민: 「점검은 안 겹칠수록 좋아 — 점검 항목을 추가해도 돼」) 새 항목. 대상은 RuntimeInspectTargets가 근무 중에 세운다 ──
            case "C-4":
                ok = FallToFloor(look, prop, jt.transform, b, 0.45f);   // 시계가 벽에서 떨어져 바닥에(66차 플레이 점검: 둥근 시계는 돌려도 티가 안 났다)
                break;
            case "S-6":
                ok = TiltOnWall(look, prop, jt.transform, b, band);   // 세계 지도가 기울거나 세로로·거꾸로
                if (ok) HideTwin(look, RuntimeInspectTargets.MapTwinPath);   // 같은 자리에 겹친 두 번째 지도
                break;
            case "C-5":
                ok = TurnAboutUp(look, prop, jt.transform, b, band <= Band.Band1 ? 135f : 180f);   // 교사 의자가 교실 쪽으로 돌아앉음
                break;
            case "K-2":
                ok = TurnOneChair(look, band);   // 경비실 의자 둘 중 하나가 돌아앉음
                break;
            case "S-4":
                ok = SpinGlobe(look, prop, jt.transform, b, band);   // 지구본이 혼자 돈다
                break;
            case "S-5":
                ok = GlowFlasks(look, glow, band);   // 선반 플라스크 유리가 붉게 빛나며 깜빡임
                break;
            case "T-4":
                ok = SinkRunning(look, band);   // 세면대 넷 중 하나에서 물이 흐른다
                break;
            case "T-5":
                ok = FallToFloor(look, prop, jt.transform, b, 0.55f);   // 수건이 바닥에 떨어져 있다
                break;
            case "L-4":
                ok = ChalkWriting(look, prop, b, band);   // 도서관 칠판에 분필 글씨
                break;
            case "L-5":
                ok = StandUp(look, prop, jt.transform, b);   // 쓰러져 있던 쓰레기통이 세워져 있다(66차 플레이 점검: 씬의 도서관 쓰레기통은 원래 누워 있다)
                break;
            case "K-3":
                ok = TipOver(look, prop, jt.transform, b);   // 화분이 쓰러져 있다
                break;
            case "H-6":
                ok = BenchOut(look, prop, jt.transform, b, band);   // 벤치 한쪽 끝이 벽에서 떨어져 복도로 돌아 나옴
                break;
            default:
                ok = false;
                break;
        }

        if (!ok)
        {
            Remove(look);
            return null;
        }

        return look;
    }

    /// <summary>[소리] 틀이지만 눈에 보이는 물도 세우는 항목(57차).</summary>
    public const string FaucetItem = "S-3";

    /// <summary>과학실 개수대(Laboratory_Sink) 수도꼭지 입 — 소품 로컬 좌표(모형 축척 77.9·x 270°). 57차 레이 측정: 월드 (51.30, 2.40, 40.465).</summary>
    private static readonly Vector3 FaucetMouthLocal = new Vector3(-0.00013f, 0.00096f, 0.01155f);

    /// <summary>
    /// S-3 물 — 수도꼭지 입에서 개수대 바닥까지 물방울(구간 1)·물줄기(구간 2+)·파문(<see cref="FaucetWater"/>).
    /// 입은 소품 로컬 좌표, 소품 크기가 바뀌어 상자 밖이면 상자 윗부분 가운데. 바닥은 입 아래로 쏜 레이가 맞는 그 소품의 면.
    /// </summary>
    private static void Faucet(Look look, Transform prop, Transform inspect, Bounds b, Band band)
    {
        Vector3 mouth = prop.TransformPoint(FaucetMouthLocal);
        Bounds grown = b;
        grown.Expand(0.05f);
        if (!grown.Contains(mouth)) mouth = new Vector3(b.center.x, b.min.y + b.size.y * 0.88f, b.center.z);

        float basin = mouth.y - 0.34f;
        float best = float.MaxValue;
        foreach (RaycastHit h in Physics.RaycastAll(mouth + Vector3.down * 0.02f, Vector3.down, 1.2f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (h.collider == null || h.collider.transform.IsChildOf(inspect) || !h.collider.transform.IsChildOf(prop)) continue;
            if (h.distance < best)
            {
                best = h.distance;
                basin = h.point.y;
            }
        }

        look.Objects.Add(FaucetWater.Create("이상 " + look.ItemId + " 물", mouth, basin, Mathf.Max(1, (int)band), Ring(), Dot()));
    }

    /// <summary>[옮김·없음] 소품을 통째로 감춘다 — 렌더러·콜라이더를 끄고(점검 기준점 아래는 남김) 거둘 때 되돌린다.</summary>
    private static bool HideProp(Look look, Transform prop, Transform keep)
    {
        List<Renderer> rs = new List<Renderer>();
        foreach (Renderer r in prop.GetComponentsInChildren<Renderer>(true))
        {
            if (r.enabled && !r.transform.IsChildOf(keep)) rs.Add(r);
        }

        List<Collider> cs = new List<Collider>();
        foreach (Collider c in prop.GetComponentsInChildren<Collider>(true))
        {
            if (c.enabled && !c.transform.IsChildOf(keep)) cs.Add(c);
        }

        if (rs.Count == 0) return false;
        for (int i = 0; i < rs.Count; i++) rs[i].enabled = false;
        for (int i = 0; i < cs.Count; i++) cs[i].enabled = false;
        look.Undo.Add(() =>
        {
            for (int i = 0; i < rs.Count; i++) if (rs[i] != null) rs[i].enabled = true;
            for (int i = 0; i < cs.Count; i++) if (cs[i] != null) cs[i].enabled = true;
        });
        return true;
    }

    // ── [빛] ─────────────────────────────────────────────────

    private Light Glow(Look look, Vector3 at, Color color, float range, float intensity, float dotSize)
    {
        GameObject go = new GameObject("이상 빛 " + look.ItemId);
        go.transform.position = at;
        Light l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = color;
        l.range = range * Mathf.Lerp(1f, LightAnomalyScale, 0.5f);
        l.intensity = intensity * LightAnomalyScale;
        l.shadows = LightShadows.None;
        l.renderMode = LightRenderMode.ForcePixel;
        look.Objects.Add(go);
        if (dotSize > 0f)
        {
            Renderer dot = Card(look, "빛점", Dot(), at, new Vector2(dotSize * 0.7f, dotSize * 0.7f), new Color(color.r, color.g, color.b, 0.55f), 0.03f);   // 53차: 빛점도 작고 흐리게
            _lastDot = dot;
        }
        else
        {
            _lastDot = null;
        }

        return l;
    }

    private Renderer _lastDot;

    // ── 66차 새 항목 ─────────────────────────────────────────

    private static readonly float[] TiltDegreesByBand = { 0f, 35f, 90f, 150f, 180f };
    private static readonly float[] SpinDegreesByBand = { 0f, 25f, 40f, 65f, 100f };
    private static readonly float[] BenchDegreesByBand = { 0f, 25f, 40f, 55f, 70f };

    private static readonly string[] ChalkLinesByBand =
    {
        "",
        "보고 있어",
        "보고 있어\n뒤에",
        "보고하지 마\n뒤에 있어",
        "보고하지 마\n보고하지 마\n뒤에 있어"
    };

    private static int BandIndex(Band band)
    {
        return Mathf.Clamp((int)band, 0, 4);
    }

    /// <summary>소품을 <paramref name="pivot"/>을 지나는 축 둘레로 돌린다(정적 배칭이면 대역). 옮겨진 것(원본 또는 대역)을 돌려준다.</summary>
    private bool TurnAround(Look look, Transform prop, Transform judge, Vector3 pivot, Vector3 axis, float degrees, out Transform moved)
    {
        Quaternion q = Quaternion.AngleAxis(degrees, axis);
        return MoveProp(look, prop, judge, pivot + q * (prop.position - pivot), q * prop.rotation, out moved);
    }

    private bool TurnAboutUp(Look look, Transform prop, Transform judge, Bounds b, float degrees)
    {
        Transform moved;
        return TurnAround(look, prop, judge, b.center, Vector3.up, degrees, out moved);
    }

    /// <summary>C-4 시계 · S-6 지도: 벽 법선(얇은 축) 둘레로 — 구간 1 35° · 2 90° · 3 150° · 4 거꾸로. 방향은 그날 고정.</summary>
    private bool TiltOnWall(Look look, Transform prop, Transform judge, Bounds b, Band band)
    {
        Vector3 normal = b.size.x < b.size.z ? Vector3.right : Vector3.forward;
        float deg = TiltDegreesByBand[BandIndex(band)];
        if (PoolIndex(look.ItemId + ".sign", 2) == 1) deg = -deg;
        Transform moved;
        return TurnAround(look, prop, judge, b.center, normal, deg, out moved);
    }

    /// <summary>K-2: 경비실 접이식 의자 둘 중 그날 하나가 돌아앉는다.</summary>
    private bool TurnOneChair(Look look, Band band)
    {
        int n = RuntimeInspectTargets.PropCount(look.ItemId);
        Transform chair = RuntimeInspectTargets.Prop(look.ItemId, PoolIndex(look.ItemId + ".chair", n));
        if (chair == null || IsBatched(chair, null)) return false;
        Bounds cb = PropBounds(chair, null);
        Transform moved;
        return TurnAround(look, chair, null, cb.center, Vector3.up, band <= Band.Band1 ? 120f : 180f, out moved);
    }

    /// <summary>S-4: 지구본이 세로축 둘레로 천천히 돈다(구간이 높을수록 빠르게). 정적 배칭이면 대역이 돈다.</summary>
    private bool SpinGlobe(Look look, Transform prop, Transform judge, Bounds b, Band band)
    {
        Transform moved;
        if (!MoveProp(look, prop, judge, prop.position, prop.rotation, out moved) || moved == null) return false;
        look.Spin = moved;
        look.SpinPivot = b.center;
        look.SpinSpeed = SpinDegreesByBand[BandIndex(band)];
        return true;
    }

    /// <summary>S-5: 선반 플라스크 유리가 붉게 빛나고(발광 재질 사본) 붉은 빛이 번진다 — 다른 [빛] 이상처럼 깜빡인다.</summary>
    private bool GlowFlasks(Look look, float glow, Band band)
    {
        Color red = new Color(1f, 0.16f, 0.1f);
        float seed = UnityEngine.Random.value * 100f;
        float hz = AnomalyLook.FlickerHz(band, PhotosensitiveSafe);
        Vector3 sum = Vector3.zero;
        int n = 0;
        for (int p = 0; p < RuntimeInspectTargets.PropCount(look.ItemId); p++)
        {
            Transform group = RuntimeInspectTargets.Prop(look.ItemId, p);
            if (group == null) continue;
            foreach (Renderer r in group.GetComponentsInChildren<Renderer>())
            {
                if (r.name.StartsWith("Inspect ")) continue;
                Material[] old = r.sharedMaterials;
                Material[] lit = new Material[old.Length];
                for (int i = 0; i < old.Length; i++)
                {
                    Material m = new Material(old[i]);
                    m.EnableKeyword("_EMISSION");
                    m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                    Color c = red * (1.1f * glow);
                    m.SetColor("_EmissionColor", c);
                    _materials.Add(m);
                    lit[i] = m;
                    look.Flickers.Add(new Flicker { Emit = m, EmitColor = c, Hz = hz, Seed = seed });
                }

                Renderer rr = r;
                rr.sharedMaterials = lit;
                look.Undo.Add(() =>
                {
                    if (rr != null) rr.sharedMaterials = old;
                });
                sum += r.bounds.center;
                n++;
            }
        }

        if (n == 0) return false;
        Light l = Glow(look, sum / n, red, 1.8f, 0.7f * glow, 0f);
        AddFlicker(look, l, null, band, seed);
        return true;
    }

    /// <summary>T-4: 세면대 넷 중 그날 하나 — 수도꼭지(벽 쪽 윗면)에서 물이 흐른다(S-3과 같은 물).</summary>
    private bool SinkRunning(Look look, Band band)
    {
        int n = RuntimeInspectTargets.PropCount(look.ItemId);
        Transform sink = RuntimeInspectTargets.Prop(look.ItemId, PoolIndex(look.ItemId + ".sink", n));
        if (sink == null) return false;
        Bounds sb = PropBounds(sink, null);
        Vector3 wall = WallDirection(sb.center, sink);
        float reach = Mathf.Abs(wall.x) * sb.extents.x + Mathf.Abs(wall.z) * sb.extents.z;
        Vector3 side = Vector3.Cross(Vector3.up, wall);   // 앞에서 봐서 오른쪽 수도꼭지(66차 플레이 점검: 두 꼭지 사이에서 흘렀다)
        Vector3 mouth = new Vector3(sb.center.x, sb.max.y - SinkMouthDrop, sb.center.z) + wall * Mathf.Max(0.05f, reach - SinkMouthInset) + side * SinkMouthSide;
        float basin = sb.center.y;
        float best = float.MaxValue;
        foreach (RaycastHit h in Physics.RaycastAll(mouth + Vector3.down * 0.03f, Vector3.down, 1f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (h.collider == null || h.collider.name.StartsWith("Inspect ")) continue;
            if (h.distance < best)
            {
                best = h.distance;
                basin = h.point.y;
            }
        }

        look.Objects.Add(FaucetWater.Create("이상 " + look.ItemId + " 물", mouth, basin, Mathf.Max(1, (int)band), Ring(), Dot()));
        return true;
    }

    /// <summary>세면대 수도꼭지 입: 윗면에서 아래로(m).</summary>
    public static float SinkMouthDrop = 0.1f;

    /// <summary>세면대 수도꼭지 입: 벽면에서 안쪽으로(m).</summary>
    public static float SinkMouthInset = 0.17f;

    /// <summary>세면대 수도꼭지 입: 가운데에서 오른쪽 꼭지까지(m).</summary>
    public static float SinkMouthSide = 0.09f;

    /// <summary>C-4 시계 · T-5 수건: 벽에 걸려 있던 것이 벽 아래 바닥에 떨어져 누워 있다(벽에서 <paramref name="pullOut"/>m).</summary>
    private bool FallToFloor(Look look, Transform prop, Transform judge, Bounds b, float pullOut)
    {
        Vector3 wall = WallDirection(b.center, prop);
        Vector3 along = Vector3.Cross(Vector3.up, wall);
        if (along.sqrMagnitude < 0.0001f) along = Vector3.right;
        Quaternion q = Quaternion.AngleAxis(90f, along.normalized);
        Vector3 spot = new Vector3(b.center.x, b.min.y, b.center.z) - wall * pullOut;
        float floor = FloorBelow(spot, b.min.y);   // 66차 플레이 점검: 벽 높이 3m 시계는 FloorY(3m 레이)로는 바닥이 안 잡혔다
        if (float.IsNaN(floor) || floor >= b.min.y) return false;
        Transform moved;
        if (!MoveProp(look, prop, judge, prop.position, q * prop.rotation, out moved) || moved == null) return false;
        SettleAt(moved, spot.x, spot.z, floor + 0.004f);
        return true;
    }

    /// <summary>L-5: 누워 있던 원통(로컬 위 = 원통 축)을 같은 자리에 똑바로 세운다.</summary>
    private bool StandUp(Look look, Transform prop, Transform judge, Bounds b)
    {
        Quaternion rot = Quaternion.FromToRotation(prop.up, Vector3.up) * prop.rotation;
        Transform moved;
        if (!MoveProp(look, prop, judge, prop.position, rot, out moved) || moved == null) return false;
        SettleAt(moved, b.center.x, b.center.z, b.min.y + 0.004f);
        return true;
    }

    /// <summary>K-3 화분: 소품 키만큼 트인 쪽으로 쓰러져 바닥에 누워 있다(쪽은 그날 고정).</summary>
    private bool TipOver(Look look, Transform prop, Transform judge, Bounds b)
    {
        Vector3 dir = OpenSide(b, prop, look.ItemId);
        Vector3 axis = Vector3.Cross(Vector3.up, dir);
        if (axis.sqrMagnitude < 0.0001f) return false;
        Vector3 pivot = new Vector3(b.center.x, b.min.y, b.center.z);
        Quaternion q = Quaternion.AngleAxis(88f, axis.normalized);
        Transform moved;
        if (!MoveProp(look, prop, judge, pivot + q * (prop.position - pivot), q * prop.rotation, out moved) || moved == null) return false;
        Bounds nb;
        if (MovedBounds(moved, out nb)) moved.position += Vector3.up * (b.min.y + 0.004f - nb.min.y);
        return true;
    }

    /// <summary>H-6: 벽에 붙은 벤치의 한쪽 끝을 축으로 다른 끝이 복도 쪽으로 돌아 나와 있다(구간 1 25° ~ 4 70° — 통로는 남긴다).</summary>
    private bool BenchOut(Look look, Transform prop, Transform judge, Bounds b, Band band)
    {
        Vector3 wall = WallDirection(b.center, prop);
        Vector3 along = b.size.x > b.size.z ? Vector3.right : Vector3.forward;
        float half = Mathf.Max(b.extents.x, b.extents.z);
        float end = PoolIndex(look.ItemId + ".end", 2) == 0 ? 1f : -1f;
        Vector3 pivot = b.center + along * end * (half - 0.15f);
        Vector3 far = b.center - along * end * half;
        float deg = BenchDegreesByBand[BandIndex(band)];
        Vector3 swung = pivot + Quaternion.AngleAxis(deg, Vector3.up) * (far - pivot);
        if (Vector3.Dot(swung - far, -wall) < 0f) deg = -deg;
        Transform moved;
        return TurnAround(look, prop, judge, pivot, Vector3.up, deg, out moved);
    }

    /// <summary>L-4: 도서관 칠판(트인 쪽 면)에 분필 글씨 — 구간이 높을수록 줄이 늘어난다.</summary>
    private bool ChalkWriting(Look look, Transform prop, Bounds b, Band band)
    {
        Vector3 n = b.size.x < b.size.z ? Vector3.right : Vector3.forward;
        if (FreeDistance(b.center, -n, prop, 8f) > FreeDistance(b.center, n, prop, 8f)) n = -n;
        float thick = Mathf.Abs(n.x) * b.extents.x + Mathf.Abs(n.z) * b.extents.z;
        float width = Mathf.Abs(n.x) > 0.5f ? b.size.z : b.size.x;
        if (_props == null) _props = Resources.Load<InspectionAnomalyPropsSO>(InspectionAnomalyPropsSO.ResourcePath);
        TMPro.TMP_FontAsset font = _props != null ? _props.ChalkFont : null;

        GameObject go = new GameObject("이상 " + look.ItemId + " 분필 글씨");
        float tilt = PoolIndex(look.ItemId + ".tilt", 2) == 0 ? -4f : 5f;
        go.transform.SetPositionAndRotation(b.center + n * (thick + 0.008f) + Vector3.down * 0.05f, Quaternion.LookRotation(-n, Vector3.up) * Quaternion.Euler(0f, 0f, tilt));
        TMPro.TextMeshPro tmp = go.AddComponent<TMPro.TextMeshPro>();
        if (font != null) tmp.font = font;
        tmp.text = ChalkLinesByBand[BandIndex(band)];
        tmp.fontSize = 3.8f;
        tmp.alignment = TMPro.TextAlignmentOptions.Center;
        tmp.enableWordWrapping = false;
        tmp.color = new Color(0.93f, 0.93f, 0.88f, 0.82f);
        tmp.rectTransform.sizeDelta = new Vector2(width * 0.85f, b.size.y * 0.9f);
        look.Objects.Add(go);
        return true;
    }

    /// <summary>66차: 그 점 아래 위를 보는 면 중 <paramref name="below"/>보다 낮은 가장 높은 것(8m까지). 없으면 NaN.</summary>
    private static float FloorBelow(Vector3 p, float below)
    {
        float best = float.NaN;
        foreach (RaycastHit h in Physics.RaycastAll(new Vector3(p.x, below - 0.02f, p.z), Vector3.down, 8f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (h.normal.y < 0.8f || h.collider.name.StartsWith("Inspect ")) continue;
            if (h.collider.GetComponentInParent<Rigidbody>() != null) continue;
            if (float.IsNaN(best) || h.point.y > best) best = h.point.y;
        }

        return best;
    }

    /// <summary>S-6: 같은 자리에 겹친 두 번째 소품을 숨긴다.</summary>
    private static void HideTwin(Look look, string path)
    {
        GameObject twin = GameObject.Find("/" + path);
        if (twin != null) HideOriginal(look, twin.transform, null, true);
    }

    /// <summary>그 점에서 가장 가까운 벽 쪽(가로 네 방향, 1.5m 안). 없으면 소품이 보는 반대쪽.</summary>
    private static Vector3 WallDirection(Vector3 from, Transform ignore)
    {
        Vector3[] dirs = { Vector3.forward, Vector3.back, Vector3.left, Vector3.right };
        Vector3 best = Vector3.back;
        float bestD = float.MaxValue;
        for (int i = 0; i < dirs.Length; i++)
        {
            float d = FreeDistance(from, dirs[i], ignore, 1.5f);
            if (d < bestD)
            {
                bestD = d;
                best = dirs[i];
            }
        }

        return best;
    }

    /// <summary>그 점에서 그 방향으로 막힘까지의 거리(소품·점검 기준점·플레이어는 무시, 최대 <paramref name="max"/>).</summary>
    private static float FreeDistance(Vector3 from, Vector3 dir, Transform ignore, float max)
    {
        float best = max;
        foreach (RaycastHit h in Physics.RaycastAll(from, dir, max, ~0, QueryTriggerInteraction.Ignore))
        {
            if (h.collider == null || h.collider.name.StartsWith("Inspect ")) continue;
            if (ignore != null && h.collider.transform.IsChildOf(ignore)) continue;
            if (h.collider.GetComponentInParent<Rigidbody>() != null) continue;
            if (h.distance < best) best = h.distance;
        }

        return best;
    }

    /// <summary>쓰러질 쪽 — 여덟 방향 중 바닥 위 0.3m에서 소품 키만큼 가장 트인 쪽(같으면 그날 무작위).</summary>
    private static Vector3 OpenSide(Bounds b, Transform prop, string key)
    {
        Vector3 from = new Vector3(b.center.x, b.min.y + 0.3f, b.center.z);
        float need = b.size.y + 0.2f;
        List<Vector3> open = new List<Vector3>();
        Vector3 bestDir = Vector3.forward;
        float bestFree = -1f;
        for (int i = 0; i < 8; i++)
        {
            Vector3 d = Quaternion.Euler(0f, i * 45f, 0f) * Vector3.forward;
            float free = FreeDistance(from, d, prop, need);
            if (free >= need - 0.001f) open.Add(d);
            if (free > bestFree)
            {
                bestFree = free;
                bestDir = d;
            }
        }

        return open.Count > 0 ? open[PoolIndex(key + ".fall", open.Count)] : bestDir;
    }

    /// <summary>옮긴 소품(또는 대역)의 렌더러 상자(점검 기준점 자식은 뺀다).</summary>
    private static bool MovedBounds(Transform t, out Bounds bounds)
    {
        bounds = new Bounds();
        bool any = false;
        foreach (Renderer r in t.GetComponentsInChildren<Renderer>())
        {
            if (r == null || !r.enabled || r.name.StartsWith("Inspect ") || r is ParticleSystemRenderer) continue;
            if (!any)
            {
                bounds = r.bounds;
                any = true;
            }
            else
            {
                bounds.Encapsulate(r.bounds);
            }
        }

        return any;
    }

    /// <summary>옮긴 것을 그 자리(가로 x·z = 상자 가운데, 바닥 = 상자 밑면)에 맞춘다.</summary>
    private static void SettleAt(Transform t, float x, float z, float floorY)
    {
        Bounds nb;
        if (!MovedBounds(t, out nb)) return;
        t.position += new Vector3(x - nb.center.x, floorY - nb.min.y, z - nb.center.z);
    }


    /// <summary>65차: [빛] 이상 깜빡임 — T-3 거울과 같은 박자(<see cref="AnomalyLook.FlickerHz"/>, 광과민 옵션이면 느린 맥동). 빛 없이 빛점만도 된다.</summary>
    private static void AddFlicker(Look look, Light light, Renderer glow, Band band, float seed)
    {
        if (light == null && glow == null) return;
        look.Flickers.Add(new Flicker
        {
            Light = light, Base = light != null ? light.intensity : 0f, Glow = glow, GlowColor = glow != null ? glow.sharedMaterial.color : Color.white,
            Hz = AnomalyLook.FlickerHz(band, PhotosensitiveSafe), Seed = seed
        });
    }

    /// <summary>
    /// 65차 C-3: 사다리가 서 있던 바닥에 짙은 자국(사다리 바닥 크기). 소품의 자식으로 붙여 점검 외곽선이 빈자리에 그려진다(사다리 렌더러는 꺼져 있다) —
    /// 그 자리를 보고 [없음](= 이상)으로 보고한다.
    /// </summary>
    private void EmptyMark(Look look, Transform prop, Bounds b)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = "이상 C-3 빈자리";
        DestroyImmediate(go.GetComponent<Collider>());
        float y = FloorY(b.center, b.min.y);
        Vector3 fwd = Flat(prop.forward).sqrMagnitude > 0.0001f ? Flat(prop.forward) : Vector3.forward;
        go.transform.SetPositionAndRotation(new Vector3(b.center.x, y + 0.004f, b.center.z), Quaternion.LookRotation(Vector3.down, fwd));
        Vector3 ext = Quaternion.Inverse(Quaternion.LookRotation(fwd, Vector3.up)) * b.size;
        go.transform.localScale = new Vector3(Mathf.Max(0.35f, Mathf.Abs(ext.x)) * 1.05f, Mathf.Max(0.35f, Mathf.Abs(ext.z)) * 1.05f, 1f);
        Renderer r = go.GetComponent<Renderer>();
        // 65차 플레이 점검: 알파 컷(Wet)이라 반투명이 무시되어 시커먼 웅덩이로 보였다 — 반투명(WetSurface) + 가장자리가 번진 네모 자국(먼지가 덜 앉은 자리).
        r.sharedMaterial = WetSurface(Footprint(), new Color(0.03f, 0.026f, 0.022f, 0.9f), 0.08f);
        r.shadowCastingMode = ShadowCastingMode.Off;
        go.transform.SetParent(prop, true);
        look.Objects.Add(go);
    }

    private void Halo(Look look, Bounds b, Color color)
    {
        if (!HaloRing) return;
        float size = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z)) * 1.45f + 0.12f;
        float alpha = 0.1f + 0.025f * (int)look.Band;   // 43차: 4구간 0.26 → 0.2
        Renderer halo = Card(look, "후광 링", Ring(), b.center, new Vector2(size, size), new Color(color.r, color.g, color.b, alpha), size * 0.5f);
        _halos.Add(new HaloFade { Mat = halo.sharedMaterial, Anchor = b.center, Alpha = alpha });
    }

    private void Mirror(Look look, Transform prop, Bounds b, Band band, float glow)
    {
        Vector3 fwd = Flat(prop.forward);
        Vector3 at = new Vector3(b.center.x, b.max.y + 0.14f, b.center.z) + fwd * 0.07f;
        Color cold = new Color(0.86f, 0.95f, 1f);
        // 53차 플레이 점검: 거울 위 벽이 통째로 하얗게 씻겼다 — 세기 1/3, 범위는 조금 넓혀 가장자리를 부드럽게.
        Light l = Glow(look, at + fwd * 0.25f, cold, 2.6f, 0.3f * glow, 0f);
        float width = Mathf.Max(0.3f, Mathf.Max(b.size.x, b.size.z) * 0.85f);
        Renderer tube = Card(look, "형광등", Bar(), at, new Vector2(width, 0.05f), new Color(cold.r, cold.g, cold.b, 0.6f), -1f);
        tube.transform.rotation = Quaternion.LookRotation(-fwd, Vector3.up);
        look.Flickers.Add(new Flicker
        {
            Light = l, Base = l.intensity, Glow = tube, GlowColor = tube.sharedMaterial.color,
            Hz = AnomalyLook.FlickerHz(band, PhotosensitiveSafe), Seed = UnityEngine.Random.value * 100f
        });
        Halo(look, b, cold);
    }

    private void Tick(Flicker f)
    {
        if (f.Light == null && f.Glow == null && f.Emit == null) return;
        float k;
        if (PhotosensitiveSafe)
        {
            float slow = Mathf.Min(f.Hz, AnomalyLook.SlowFlickerMaxHz);
            k = 0.55f + 0.45f * Mathf.Cos((Time.time * slow + f.Seed) * Mathf.PI * 2f);
        }
        else
        {
            float n = Mathf.PerlinNoise(Time.time * f.Hz, f.Seed);
            k = n > 0.64f ? 0.04f : (n > 0.58f ? 0.45f : 1f);
        }

        if (f.Light != null) f.Light.intensity = f.Base * k;
        if (f.Glow != null)
        {
            Color c = f.GlowColor;
            c.a *= Mathf.Clamp01(k);
            f.Glow.sharedMaterial.color = c;
        }

        if (f.Emit != null) f.Emit.SetColor("_EmissionColor", f.EmitColor * k);
    }

    private void Blind(Look look, Transform prop, Transform judge, Bounds b, float glow)
    {
        Material blindMat = null;
        foreach (Renderer r in prop.GetComponentsInChildren<Renderer>())
        {
            if (r.transform.IsChildOf(judge)) continue;
            if (blindMat == null) blindMat = r.sharedMaterial;
        }

        HideOriginal(look, prop, judge, false);

        // 말아 올린 블라인드 — 창 위에 얇은 막대.
        GameObject rolled = GameObject.CreatePrimitive(PrimitiveType.Cube);
        rolled.name = "이상 L-2 말린 블라인드";
        DestroyImmediate(rolled.GetComponent<Collider>());
        rolled.transform.SetPositionAndRotation(new Vector3(b.center.x, b.max.y - 0.07f, b.center.z), Quaternion.identity);
        rolled.transform.localScale = new Vector3(b.size.x, 0.14f, b.size.z);
        if (blindMat != null) rolled.GetComponent<Renderer>().sharedMaterial = blindMat;
        look.Objects.Add(rolled);

        // 창밖에서 들어오는 달빛. 블라인드의 앞(forward)이 창밖이다.
        Vector3 outward = Flat(prop.forward);
        // 창과 안쪽 소품 사이 바닥에 비스듬히 떨어지게.
        Vector3 from = b.center + outward * 1.1f + Vector3.up * 1.3f;
        Vector3 to = new Vector3(b.center.x, FloorY(b.center - outward * 1.2f, b.min.y), b.center.z) - outward * 1.2f;
        GameObject moon = new GameObject("이상 L-2 달빛");
        moon.transform.SetPositionAndRotation(from, Quaternion.LookRotation((to - from).normalized, Vector3.up));
        Light l = moon.AddComponent<Light>();
        l.type = LightType.Spot;
        l.color = new Color(0.62f, 0.72f, 1f);
        l.spotAngle = 62f;
        l.innerSpotAngle = 28f;
        l.range = 7f;
        l.intensity = 6f * glow * Mathf.Lerp(1f, LightAnomalyScale, 0.5f);   // 53차: 달빛도 약하게
        l.shadows = LightShadows.None;
        l.renderMode = LightRenderMode.ForcePixel;
        look.Objects.Add(moon);
    }

    // ── [옮김] ───────────────────────────────────────────────

    /// <summary>화분 변칙 자리(52차 민: 「천장에 거꾸로 매달려 있다던지 벽에 옆으로 달려 있다던지 — 다만 인식은 쉽게」). 0 책상 위 · 1 천장에 거꾸로 · 2 벽에 옆으로.</summary>
    public enum PlantPlace
    {
        Desk = 0,
        Ceiling = 1,
        Wall = 2
    }

    private static readonly Dictionary<int, PlantPlace> s_plantPlace = new Dictionary<int, PlantPlace>();

    /// <summary>그날 화분 자리(같은 날은 늘 같은 자리 — 다음 날 CCTV 한 컷도 같다). 1일차는 가장 알아보기 쉬운 천장.</summary>
    public static PlantPlace PlantPlaceFor(int day)
    {
        PlantPlace p;
        if (s_plantPlace.TryGetValue(day, out p)) return p;
        p = day <= 1 ? PlantPlace.Ceiling : (PlantPlace)UnityEngine.Random.Range(0, 3);
        s_plantPlace[day] = p;
        return p;
    }

    private bool PlantOddPlace(Look look, Transform prop, Transform judge, Bounds b, Band band)
    {
        switch (PlantPlaceFor(NightRun.Day))
        {
            case PlantPlace.Ceiling:
                if (PlantOnCeiling(look, prop, judge, b)) return true;
                break;
            case PlantPlace.Wall:
                if (PlantOnWall(look, prop, judge, b)) return true;
                break;
        }

        return PlantToDesk(look, prop, judge, b, band);
    }

    /// <summary>교실 책상들의 가운데(바닥 높이). 책상이 없으면 소품 자리.</summary>
    private static Vector3 RoomMiddle(Transform prop)
    {
        Transform room = prop.parent;
        Vector3 sum = Vector3.zero;
        int n = 0;
        if (room != null)
        {
            foreach (Transform t in room)
            {
                if (!t.name.StartsWith("StudentDesk")) continue;
                sum += t.position;
                n++;
            }
        }

        return n > 0 ? sum / n : prop.position;
    }

    /// <summary>원래 피벗과 바닥 가운데의 차를 새 회전에 맞춰 옮긴 피벗 자리.</summary>
    private static Vector3 PivotFor(Transform prop, Bounds b, Quaternion rot, Vector3 baseCenter)
    {
        Vector3 pivotOffset = prop.position - new Vector3(b.center.x, b.min.y, b.center.z);
        Quaternion delta = rot * Quaternion.Inverse(prop.rotation);
        return baseCenter + delta * pivotOffset;
    }

    private bool PlantOnCeiling(Look look, Transform prop, Transform judge, Bounds b)
    {
        // 책상 가운데와 원래 자리의 중간 위 — 문에서 들어서면 시야에 걸리는 높이(바닥 + 2.6m 가운데).
        Vector3 mid = Vector3.Lerp(prop.position, RoomMiddle(prop), 0.6f);
        Vector3 floor = FloorUnder(mid + Vector3.up * 0.5f);
        float ceilingY = float.MaxValue;
        foreach (RaycastHit h in Physics.RaycastAll(floor + Vector3.up * 0.3f, Vector3.up, 6f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (h.collider.transform.IsChildOf(prop) || h.point.y < floor.y + 2.2f) continue;   // 책상·사람 위는 건너뛴다
            ceilingY = Mathf.Min(ceilingY, h.point.y);
        }

        if (ceilingY == float.MaxValue) ceilingY = floor.y + 3f;

        float h0 = b.size.y;
        float top = Mathf.Min(ceilingY - 0.3f, floor.y + 2.6f + h0 * 0.5f);
        Quaternion rot = Quaternion.AngleAxis(180f, Flat(prop.right).sqrMagnitude > 0.0001f ? Flat(prop.right) : Vector3.right) * prop.rotation;
        Vector3 baseCenter = new Vector3(mid.x, top, mid.z);
        if (!MoveProp(look, prop, judge, PivotFor(prop, b, rot, baseCenter), rot)) return false;

        // 화분 밑바닥에서 천장까지 끈.
        float len = Mathf.Max(0.05f, ceilingY - top);
        GameObject rope = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        rope.name = "이상 C-1 끈";
        Collider rc = rope.GetComponent<Collider>();
        if (rc != null) Destroy(rc);
        rope.transform.position = new Vector3(mid.x, top + len * 0.5f, mid.z);
        rope.transform.localScale = new Vector3(0.012f, len * 0.5f, 0.012f);
        Renderer rr = rope.GetComponent<Renderer>();
        Shader sh = Shader.Find("Universal Render Pipeline/Lit");
        Material m = new Material(sh != null ? sh : Shader.Find("Standard"));
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", new Color(0.12f, 0.1f, 0.08f));
        rr.sharedMaterial = m;
        look.Objects.Add(rope);
        return true;
    }

    private bool PlantOnWall(Look look, Transform prop, Transform judge, Bounds b)
    {
        // 책상 가운데에서 네 방향으로 벽을 찾아 원래 자리(창가)에서 가장 먼 벽 — 눈높이에 옆으로 박혀 있다.
        Vector3 mid = RoomMiddle(prop);
        Vector3 floor = FloorUnder(mid + Vector3.up * 0.5f);
        Vector3 eye = new Vector3(mid.x, floor.y + 1.7f, mid.z);
        Vector3[] dirs = { Vector3.forward, Vector3.back, Vector3.left, Vector3.right };
        bool found = false;
        RaycastHit best = default(RaycastHit);
        float bestScore = float.NegativeInfinity;
        foreach (Vector3 d in dirs)
        {
            foreach (RaycastHit h in Physics.RaycastAll(eye, d, 12f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (h.collider.transform.IsChildOf(prop) || Mathf.Abs(h.normal.y) > 0.2f) continue;
                if (h.collider.GetComponentInParent<Rigidbody>() != null) continue;   // 플레이어
                float score = FlatDistance(h.point, prop.position);   // 65차: Flat은 단위 벡터라 .magnitude가 늘 1이었다 — 「가장 먼 벽」이 첫 벽이 되었다
                if (h.distance > 1.2f && score > bestScore)
                {
                    bestScore = score;
                    best = h;
                    found = true;
                }

                break;
            }
        }

        if (!found) return false;
        Vector3 n = Flat(best.normal);
        Quaternion rot = Quaternion.FromToRotation(prop.up, n) * prop.rotation;
        Vector3 baseCenter = new Vector3(best.point.x, floor.y + 1.7f, best.point.z) + n * 0.01f;
        return MoveProp(look, prop, judge, PivotFor(prop, b, rot, baseCenter), rot);
    }

    private bool PlantToDesk(Look look, Transform prop, Transform judge, Bounds b, Band band)
    {
        // 창가 반대편 — 같은 방의 바로 선 학생 책상을 거리순으로 늘어놓고 구간이 높을수록 먼 것.
        List<Transform> desks = new List<Transform>();
        Transform room = prop.parent;
        if (room == null) return false;
        foreach (Transform t in room)
        {
            if (!t.name.StartsWith("StudentDesk")) continue;
            if (Vector3.Dot(t.up, Vector3.up) < 0.97f || Mathf.Abs(t.position.y - prop.position.y) > 0.15f) continue;
            desks.Add(t);
        }

        if (desks.Count == 0) return false;
        Vector3 origin = prop.position;
        desks.Sort((x, y) => FlatDistance(x.position, origin).CompareTo(FlatDistance(y.position, origin)));   // 65차: Flat(단위 벡터) 길이로 정렬하던 것을 실제 거리로
        // 65차(민: 「위치가 바뀌는 점검은 이상일 때 랜덤한 좌표 풀에서」): 가까운 셋을 뺀 책상 중 그날 무작위 하나(같은 날은 같은 책상 — CCTV 한 컷도 같다).
        int skip = Mathf.Min(3, desks.Count - 1);
        Transform desk = desks[skip + PoolIndex(look.ItemId + ".desk", desks.Count - skip)];

        Bounds db = PropBounds(desk, null);
        Vector3 top = new Vector3(db.center.x, db.max.y, db.center.z);
        RaycastHit hit;
        if (Physics.Raycast(top + Vector3.up * 0.5f, Vector3.down, out hit, 1.2f, ~0, QueryTriggerInteraction.Ignore) && hit.collider.transform.IsChildOf(desk))
        {
            top.y = hit.point.y;
        }

        Vector3 pivotOffset = prop.position - new Vector3(b.center.x, b.min.y, b.center.z);
        Vector3 pos = top + pivotOffset;
        Quaternion rot = Quaternion.Euler(0f, 25f * (int)band, 0f) * prop.rotation;
        return MoveProp(look, prop, judge, pos, rot);
    }

    private bool ChairOut(Look look, Transform prop, Transform judge, Band band)
    {
        Vector3 back = -Flat(prop.forward);
        Vector3 pos = prop.position + back * AnomalyLook.PullMeters(band);
        // 65차(민: 「위치가 바뀌는 점검은 랜덤한 좌표 풀에서」): 빼 놓은 자리 + 도서관 바닥의 빈자리들(원래 자리에서 2.5~8m) 중 그날 무작위 하나.
        List<Vector3> pool = ChairPool(prop, judge);
        pool.Insert(0, pos);
        pos = pool[PoolIndex(look.ItemId + ".spot", pool.Count)];
        Transform door = NearestDoorway(pos, 12f);
        Vector3 face = door != null ? Flat(door.position - pos) : back;
        if (face.sqrMagnitude < 0.0001f) face = back;
        Quaternion rot = Quaternion.LookRotation(face.normalized, Vector3.up);
        return MoveProp(look, prop, judge, pos, rot);
    }

    private static readonly Dictionary<string, int> s_pool = new Dictionary<string, int>();

    /// <summary>65차: 그날 그 풀에서 고른 번호(같은 날 같은 키는 같은 번호 — 재시작·CCTV 다시보기도 같은 자리).</summary>
    private static int PoolIndex(string key, int count)
    {
        if (count <= 1) return 0;
        string k = NightRun.Day + "/" + key;
        int v;
        if (!s_pool.TryGetValue(k, out v) || v >= count)
        {
            v = UnityEngine.Random.Range(0, count);
            s_pool[k] = v;
        }

        return v;
    }

    /// <summary>65차 L-1: 의자를 둘 수 있는 도서관 바닥 빈자리(1.2m 격자, 같은 바닥 높이, 반지름 0.35 캡슐(바닥 위 10cm부터)이 아무것과도 겹치지 않음, 원래 자리에서 2.5~8m). 실측 41자리.</summary>
    private static List<Vector3> ChairPool(Transform prop, Transform judge)
    {
        List<Vector3> list = new List<Vector3>();
        SpaceZones zones = FindAnyObjectByType<SpaceZones>();
        Bounds box;
        if (zones == null || !zones.TryGetSpaceBox(SpaceId.Library, out box)) return list;
        float floor = prop.position.y;
        for (float x = box.min.x + 0.8f; x <= box.max.x - 0.8f; x += 1.2f)
        {
            for (float z = box.min.z + 0.8f; z <= box.max.z - 0.8f; z += 1.2f)
            {
                Vector3 p = new Vector3(x, floor, z);
                float d = FlatDistance(p, prop.position);
                if (d < 2.5f || d > 8f) continue;
                float y = FloorY(new Vector3(x, floor + 1.2f, z), float.NaN);
                if (float.IsNaN(y) || Mathf.Abs(y - floor) > 0.12f) continue;
                bool blocked = false;
                // 65차 플레이 점검: 아래 구가 바닥에 10cm 박혀 모든 자리가 막혔다(빈 목록) — 아래 구 바닥을 바닥 위 10cm로.
                foreach (Collider c in Physics.OverlapCapsule(new Vector3(x, floor + 0.45f, z), new Vector3(x, floor + 1.3f, z), 0.35f, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (c.transform.IsChildOf(prop) || (judge != null && c.transform.IsChildOf(judge))) continue;
                    if (c.GetComponentInParent<Rigidbody>() != null) continue;   // 플레이어
                    blocked = true;
                    break;
                }

                if (!blocked) list.Add(new Vector3(x, y, z));
            }
        }

        return list;
    }

    /// <summary>
    /// 소품을 그 자리·방향으로. 정적 배칭이면 원본을 숨기고 같은 프리팹을 세운다. <b>점검 기준점(<c>Inspect X</c>)은 소품을 따라간다</b>(42차) —
    /// 플레이어는 옮겨 간 소품(외곽선이 그려진 것)에 가서 보고한다.
    /// </summary>
    private bool MoveProp(Look look, Transform prop, Transform judge, Vector3 pos, Quaternion rot)
    {
        Transform moved;
        return MoveProp(look, prop, judge, pos, rot, out moved);
    }

    /// <summary>66차: 옮겨진 것(원본 또는 대역)을 함께 돌려준다. 점검 기준점이 소품 밖에 있는 묶음 소품(<paramref name="judge"/> null)은 정적 배칭이면 옮기지 못한다.</summary>
    private bool MoveProp(Look look, Transform prop, Transform judge, Vector3 pos, Quaternion rot, out Transform moved)
    {
        moved = null;
        if (!IsBatched(prop, judge))
        {
            Vector3 p = prop.position;
            Quaternion r = prop.rotation;
            prop.SetPositionAndRotation(pos, rot);
            look.Undo.Add(() =>
            {
                if (prop != null) prop.SetPositionAndRotation(p, r);
            });
            moved = prop;
            return true;
        }

        if (judge == null) return false;

        if (_props == null) _props = Resources.Load<InspectionAnomalyPropsSO>(InspectionAnomalyPropsSO.ResourcePath);
        GameObject prefab = _props != null ? _props.Find(look.ItemId) : null;
        if (prefab == null)
        {
            Debug.LogWarning("[InspectionAnomalies] " + look.ItemId + ": 정적 배칭 소품인데 대역 프리팹이 표에 없습니다(야간근무/연출/점검 이상 소품 표 다시 만들기).");
            return false;
        }

        HideOriginal(look, prop, judge, true);
        GameObject proxy = Instantiate(prefab, pos, rot);
        proxy.name = "이상 " + look.ItemId + " " + prefab.name;
        proxy.transform.localScale = prop.lossyScale;
        look.Objects.Add(proxy);
        moved = proxy.transform;

        // 점검 기준점을 대역으로 옮긴다(같은 프리팹이라 같은 상대 자리). 되돌릴 때 원래 부모로.
        Transform oldParent = judge.parent;
        Vector3 lp = judge.localPosition;
        Quaternion lr = judge.localRotation;
        Vector3 ls = judge.localScale;
        judge.SetParent(proxy.transform, false);
        judge.localPosition = lp;
        judge.localRotation = lr;
        judge.localScale = ls;
        look.Undo.Add(() =>
        {
            if (judge == null || oldParent == null) return;
            judge.SetParent(oldParent, false);
            judge.localPosition = lp;
            judge.localRotation = lr;
            judge.localScale = ls;
        });
        return true;
    }

    // ── [켬] ─────────────────────────────────────────────────

    private void Water(Look look, Transform prop, Bounds b, Band band, Vector3 fwd, Color color, float startInset, bool overflow)
    {
        Vector3 open = OpenSide(prop, b, fwd);
        float extent = Mathf.Abs(Vector3.Dot(b.extents, new Vector3(Mathf.Abs(open.x), Mathf.Abs(open.y), Mathf.Abs(open.z))));
        Vector3 front = b.center + open * (extent + 0.05f);
        float floorY = FloorY(front, b.min.y);
        float len = AnomalyLook.SpreadMeters(band);
        float wid = SpreadWidthByBand[Mathf.Clamp((int)band, 0, 4)];
        if (overflow) wid = Mathf.Max(wid, len * 0.75f);

        Vector3 start = new Vector3(b.center.x, floorY, b.center.z) + open * (extent - startInset);
        Vector3 center = start + open * (len * (overflow ? 0.4f : 0.5f));
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = "이상 " + look.ItemId + " 물";
        DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetPositionAndRotation(center + Vector3.up * 0.006f, Quaternion.LookRotation(Vector3.down, open));
        go.transform.localScale = new Vector3(wid, len, 1f);
        Renderer r = go.GetComponent<Renderer>();
        r.sharedMaterial = WetSurface(Blob(look.ItemId.GetHashCode()), color, 0.95f);
        r.shadowCastingMode = ShadowCastingMode.Off;
        look.Objects.Add(go);
    }

    /// <summary>
    /// H-2 피 식수대(44차, 김진선님 HorrorEvent_BloodyFountain). 그 연출 프리팹을 씬 식수대와 같은 자리·방향에 세우고(프리팹 속 식수대 모형은 끔 — 씬 것이 있다),
    /// 바닥 물웅덩이(물·검붉은 판 둘 다)를 구간 길이(<see cref="AnomalyLook.SpreadMeters"/> 0.6/1.4/2.2/3.4m)만큼 앞으로 늘이고 높이를 실제 바닥에 맞춘다.
    /// 앞 1m 안으로 다가서면(프리팹의 HorrorTriggerZone) 물이 검붉게 변한다 — 「가까이」 연출. 김진선님 프리팹·스크립트는 고치지 않는다.
    /// </summary>
    private bool FountainEvent(Look look, Transform prop, Bounds b, Band band)
    {
        if (_props == null) _props = Resources.Load<InspectionAnomalyPropsSO>(InspectionAnomalyPropsSO.ResourcePath);
        GameObject prefab = _props != null ? _props.FindEvent(look.ItemId) : null;
        if (prefab == null) return false;

        GameObject inst = Instantiate(prefab, prop.position, prop.rotation);
        inst.name = "이상 " + look.ItemId + " 피 식수대";
        look.Objects.Add(inst);
        Transform model = inst.transform.Find("DrinkingFountain");
        if (model != null) model.gameObject.SetActive(false);
        WatchSceneModel(inst, prop);
        // 다가섬 구역(트리거)이 식수대 앞에 있어 응시·조준 레이를 막지 않게 Ignore Raycast 층으로(트리거 판정은 그대로).
        Transform zone = inst.transform.Find("Trigger");
        if (zone != null) zone.gameObject.layer = 2;

        Vector3 fwd = Flat(prop.forward);
        float floorY = FloorY(prop.position + fwd * 0.6f, b.min.y);
        float len = Mathf.Max(0.45f, AnomalyLook.SpreadMeters(band));
        float wid = Mathf.Max(0.55f, SpreadWidthByBand[Mathf.Clamp((int)band, 0, 4)]);
        string[] puddles = { "Idle_Water/Decal_FloorPuddle", "Near_DarkRed/Decal_FloorPuddleDarkRed" };
        for (int i = 0; i < puddles.Length; i++)
        {
            Transform t = inst.transform.Find(puddles[i]);
            UnityEngine.Rendering.Universal.DecalProjector d = t != null ? t.GetComponent<UnityEngine.Rendering.Universal.DecalProjector>() : null;
            if (d == null) continue;
            // 판은 x 90°로 눕혀 아래로 비춘다 — 판의 y가 식수대 앞쪽(+z). 식수대 바로 앞(0.13m)에서 시작해 len만큼.
            Vector3 lp = t.localPosition;
            float startZ = lp.z - d.size.y * 0.5f;
            d.size = new Vector3(wid, len, d.size.z);
            lp.z = startZ + len * 0.5f;
            lp.y = floorY - prop.position.y;
            t.localPosition = lp;
        }

        // 천장 얼룩·천장 물방울(김진선님 갱신본)은 프리팹 기준 천장 3.14m — 실제 천장 바로 아래로 맞춘다.
        RaycastHit up;
        if (Physics.Raycast(new Vector3(prop.position.x, floorY + 1.2f, prop.position.z) + fwd * 0.44f, Vector3.up, out up, 6f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            Transform red = inst.transform.Find("Near_DarkRed");
            if (red != null)
            {
                foreach (Transform c in red)
                {
                    if (c.localPosition.y < 2f) continue;   // 천장에 붙은 것만(얼룩·천장 물방울)
                    Vector3 lp = c.localPosition;
                    lp.y = up.point.y - prop.position.y - (c.name.StartsWith("PS_") ? 0.06f : 0f);
                    c.localPosition = lp;
                }
            }
        }

        return true;
    }

    /// <summary>
    /// 김진선님 <c>HorrorUnseenReset</c>(연출이 끝난 뒤 안 보는 사이 처음 물로 되돌림)이 지켜볼 모델을 씬 식수대로 바꾼다.
    /// 프리팹 속 모형은 꺼 두므로 그대로면 늘 「안 본다」가 되고, 씬 식수대의 점검 기준점 상자가 시선 선을 막아도 「안 본다」가 된다 —
    /// 씬 식수대(기준점이 그 자식)를 보게 하면 둘 다 풀린다. 김진선님 코드는 고치지 않고 이 인스턴스의 필드만 반사로 바꾼다. 못 바꾸면 그 컴포넌트를 끈다.
    /// </summary>
    private static void WatchSceneModel(GameObject inst, Transform sceneModel)
    {
        foreach (MonoBehaviour mb in inst.GetComponents<MonoBehaviour>())
        {
            if (mb == null || mb.GetType().Name != "HorrorUnseenReset") continue;
            const System.Reflection.BindingFlags F = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            System.Reflection.FieldInfo watched = mb.GetType().GetField("watchedModel", F);
            System.Reflection.FieldInfo list = mb.GetType().GetField("renderers", F);
            List<Renderer> renderers = list != null ? list.GetValue(mb) as List<Renderer> : null;
            if (watched == null || renderers == null)
            {
                mb.enabled = false;
                continue;
            }

            watched.SetValue(mb, sceneModel);
            renderers.Clear();
            sceneModel.GetComponentsInChildren(true, renderers);
        }
    }

    private void Hair(Look look, Transform prop, Bounds b, Band band)
    {
        Vector3 fwd = Flat(prop.forward);
        // 변기 콜라이더는 상자라 윗면이 물탱크 높이다 — 테 높이는 바닥에서 약 0.4m로 잡는다.
        Vector3 bowl = new Vector3(b.center.x, 0f, b.center.z) + fwd * (b.extents.z * 0.35f);
        float y = b.min.y + 0.36f;
        Material mat = Wet(HairTex(), new Color(0.02f, 0.02f, 0.02f), 0.6f);

        GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Quad);
        disc.name = "이상 T-1 머리카락";
        DestroyImmediate(disc.GetComponent<Collider>());
        disc.transform.SetPositionAndRotation(new Vector3(bowl.x, y + 0.012f, bowl.z), Quaternion.LookRotation(Vector3.down, fwd));
        float size = Mathf.Min(b.size.x, b.size.z) * 0.7f;
        disc.transform.localScale = new Vector3(size, size, 1f);
        disc.GetComponent<Renderer>().sharedMaterial = mat;
        disc.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        look.Objects.Add(disc);

        if (band < Band.Band2) return;

        // 2구간부터 앞 턱 너머로 늘어진 머리카락.
        Vector3 lip = new Vector3(bowl.x, y, bowl.z) + fwd * (size * 0.5f + 0.02f);
        float drop = Mathf.Max(0.12f, y - FloorY(lip + fwd * 0.05f, b.min.y)) * (band >= Band.Band3 ? 1f : 0.5f);
        GameObject hang = GameObject.CreatePrimitive(PrimitiveType.Quad);
        hang.name = "이상 T-1 늘어진 머리카락";
        DestroyImmediate(hang.GetComponent<Collider>());
        hang.transform.SetPositionAndRotation(lip + Vector3.down * (drop * 0.5f), Quaternion.LookRotation(-fwd, Vector3.up));
        hang.transform.localScale = new Vector3(size * 0.7f, drop, 1f);
        hang.GetComponent<Renderer>().sharedMaterial = mat;
        hang.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        look.Objects.Add(hang);
    }

    // ── K-1 CCTV에만 보이는 사람 ─────────────────────────────

    private void TickWalker(Look look)
    {
        Walker w = look.Walker;
        CctvSystem cctv = CctvSystem.Active;
        if (w == null || cctv == null || cctv.ChannelCount <= 0 || w.Go != null) return;

        // 71차(민: 「CCTV 등장 장소 다양화 · 걷는 애니메이션으로 실제로 이동」): 자리는 밤 시작에 코어가 정한 것(NightRun.CctvAnomalySpot, 자리 표 CctvSpots).
        // 표의 자리가 그 채널 화면에 안 보이면(채널 자리를 고쳤을 때) 옛 방식 — 채널 카메라 앞 바닥을 비스듬히 가로지르는 길.
        CctvSpot spot = NightRun.CctvAnomalySpot;
        if (spot != null && spot.Channel >= cctv.ChannelCount) spot = null;
        w.Channel = spot != null ? spot.Channel : Mathf.Abs(NightRun.Day * 7 + 3) % cctv.ChannelCount;
        Camera cam = cctv.ChannelCamera(w.Channel);
        if (cam == null) return;
        Vector3 a, b;
        if (!CctvWalker.TryResolve(cam, spot, out a, out b)) ScreenCrossing(cam, look.Band, out a, out b);

        w.Person = CctvWalker.Create(a, b, w.Channel, "이상 K-1 화면 속 사람");
        if (w.Person == null) return;
        w.Go = w.Person.gameObject;
        look.Objects.Add(w.Go);
        // 다 지나가면 화면에서 8초 사라졌다가 반대로 걷는다(끝없이).
        w.Person.Walk(a, b, true, 8f, null);
        CctvWalker person = w.Person;
        // 51차: 3초 이어서 보면 얼굴 점프스케어(3일차부터, 밤당 한 번) — 끝나면 잠시 화면에서 사라진다.
        CctvFaceScare.Register(w.Go, w.Channel, () =>
        {
            if (person != null) person.HideFor(12f);
        });
    }

    /// <summary>자리 표가 없거나 맞지 않을 때 — 채널 카메라 앞을 안쪽 왼편에서 앞쪽 오른편으로 비스듬히 가로지르는 바닥 길(41차).</summary>
    private void ScreenCrossing(Camera cam, Band band, out Vector3 a, out Vector3 b)
    {
        Vector3 eye = cam.transform.position;
        Vector3 fwd = Flat(cam.transform.forward);
        if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
        fwd.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, fwd);
        float dist = Mathf.Lerp(6f, 3.5f, AnomalyLook.Strength(band));
        Vector3 mid = FloorUnder(eye + fwd * dist);
        float left = Free(mid + Vector3.up * 1f, -right, 2.4f);
        float rightFree = Free(mid + Vector3.up * 1f, right, 2.4f);
        a = FloorUnder(eye + fwd * (dist + 1.8f) - right * left);
        b = FloorUnder(eye + fwd * Mathf.Max(2.2f, dist - 1.2f) + right * rightFree);
    }

    // ── 도우미 ───────────────────────────────────────────────

    /// <summary>65차: 수평 거리(m). <see cref="Flat"/>은 방향(단위 벡터)이라 거리로 쓰면 안 된다.</summary>
    private static float FlatDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    private static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v.sqrMagnitude > 0.000001f ? v.normalized : Vector3.zero;
    }

    private static Bounds PropBounds(Transform prop, Transform except)
    {
        Bounds b = new Bounds(prop.position, Vector3.zero);
        bool any = false;
        foreach (Renderer r in prop.GetComponentsInChildren<Renderer>())
        {
            if (except != null && r.transform.IsChildOf(except)) continue;
            if (r is ParticleSystemRenderer || r.GetComponent<TMPro.TMP_Text>() != null) continue;
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

        return b;
    }

    private static bool IsBatched(Transform prop, Transform except)
    {
        foreach (Renderer r in prop.GetComponentsInChildren<Renderer>())
        {
            if (except != null && r.transform.IsChildOf(except)) continue;
            if (r.isPartOfStaticBatch) return true;
        }

        return false;
    }

    private static void HideOriginal(Look look, Transform prop, Transform except, bool colliders)
    {
        foreach (Renderer r in prop.GetComponentsInChildren<Renderer>())
        {
            if (!r.enabled || (except != null && r.transform.IsChildOf(except))) continue;
            Renderer rr = r;
            rr.enabled = false;
            look.Undo.Add(() =>
            {
                if (rr != null) rr.enabled = true;
            });
        }

        if (!colliders) return;
        foreach (Collider c in prop.GetComponentsInChildren<Collider>())
        {
            if (!c.enabled || c.isTrigger || (except != null && c.transform.IsChildOf(except))) continue;
            Collider cc = c;
            cc.enabled = false;
            look.Undo.Add(() =>
            {
                if (cc != null) cc.enabled = true;
            });
        }
    }

    private static Transform NearestDoorway(Vector3 from, float within)
    {
        Transform best = null;
        float bestD = within * within;
        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
        {
            string n = t.name;
            if (!(n.Contains("Doorway") || n.StartsWith("DoorWide") || n.StartsWith("DoorNarrow"))) continue;
            if (Mathf.Abs(t.position.y - from.y) > 1.5f) continue;
            Vector3 dv = t.position - from;
            dv.y = 0f;
            float d = dv.sqrMagnitude;
            if (d < bestD)
            {
                bestD = d;
                best = t;
            }
        }

        return best;
    }

    /// <summary>앞·뒤 중 더 트인 쪽(벽에 붙은 식수대·변기의 앞).</summary>
    private static Vector3 OpenSide(Transform prop, Bounds b, Vector3 fwd)
    {
        if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward;
        Vector3 at = new Vector3(b.center.x, b.min.y + 0.2f, b.center.z);
        float ahead = FreeIgnoring(prop, at, fwd, 4f);
        float behind = FreeIgnoring(prop, at, -fwd, 4f);
        return ahead >= behind ? fwd : -fwd;
    }

    private static float FreeIgnoring(Transform prop, Vector3 from, Vector3 dir, float max)
    {
        RaycastHit[] hits = Physics.RaycastAll(from, dir, max, ~0, QueryTriggerInteraction.Ignore);
        float best = max;
        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i].collider.transform.IsChildOf(prop)) continue;
            if (hits[i].distance < best) best = hits[i].distance;
        }

        return best;
    }

    private static float Free(Vector3 from, Vector3 dir, float max)
    {
        RaycastHit hit;
        if (Physics.Raycast(from, dir, out hit, max + 0.4f, ~0, QueryTriggerInteraction.Ignore)) return Mathf.Max(0.3f, hit.distance - 0.4f);
        return max;
    }

    private static float FloorY(Vector3 above, float fallback)
    {
        RaycastHit[] hits = Physics.RaycastAll(above + Vector3.up * 0.3f, Vector3.down, 3f, ~0, QueryTriggerInteraction.Ignore);
        float best = float.NegativeInfinity;
        for (int i = 0; i < hits.Length; i++)
        {
            // 바닥 — 위를 보는 면 중 가장 높은 것. 소품 윗면이 잡히지 않게 소품 밑면보다 높은 것은 건너뛴다.
            if (hits[i].normal.y < 0.8f || hits[i].point.y > fallback + 0.05f) continue;
            if (hits[i].point.y > best) best = hits[i].point.y;
        }

        return float.IsNegativeInfinity(best) ? fallback : best;
    }

    private Renderer Card(Look look, string name, Texture2D tex, Vector3 at, Vector2 size, Color color, float billboardPush)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = "이상 " + look.ItemId + " " + name;
        DestroyImmediate(go.GetComponent<Collider>());
        go.transform.position = at;
        go.transform.localScale = new Vector3(size.x, size.y, 1f);
        Renderer r = go.GetComponent<Renderer>();
        Material m = new Material(Shader.Find("Sprites/Default"));
        m.mainTexture = tex;
        m.color = color;
        _materials.Add(m);
        r.sharedMaterial = m;
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
        look.Objects.Add(go);
        if (billboardPush >= 0f) _billboards.Add(new Bill { T = go.transform, Anchor = at, Push = billboardPush });
        return r;
    }

    /// <summary>물 — 반투명(premultiply)이라 바닥 무늬는 어둡게 비치고 손전등 반사광은 그대로 남는다.</summary>
    private Material WetSurface(Texture2D tex, Color color, float smoothness)
    {
        Material m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        m.SetTexture("_BaseMap", tex);
        m.SetColor("_BaseColor", color);
        m.SetFloat("_Smoothness", smoothness);
        m.SetFloat("_Metallic", 0f);
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 1f);
        m.SetFloat("_SrcBlend", (float)BlendMode.One);
        m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0f);
        m.SetOverrideTag("RenderType", "Transparent");
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.EnableKeyword("_ALPHAPREMULTIPLY_ON");
        m.renderQueue = (int)RenderQueue.Transparent;
        _materials.Add(m);
        return m;
    }

    /// <summary>천장 높이의 점에서 아래 바닥(플레이어 무시 없이 가장 가까운 윗면).</summary>
    private static Vector3 FloorUnder(Vector3 p)
    {
        RaycastHit hit;
        if (Physics.Raycast(p + Vector3.down * 0.3f, Vector3.down, out hit, 8f, ~0, QueryTriggerInteraction.Ignore)) return hit.point;
        return DirectionStage.FloorBelow(p);
    }

    private Material Wet(Texture2D tex, Color color, float smoothness)
    {
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        Material m = new Material(lit);
        m.SetTexture("_BaseMap", tex);
        m.SetColor("_BaseColor", color);
        m.SetFloat("_Smoothness", smoothness);
        m.SetFloat("_Metallic", 0f);
        m.SetFloat("_AlphaClip", 1f);
        m.SetFloat("_Cutoff", 0.5f);
        m.EnableKeyword("_ALPHATEST_ON");
        m.renderQueue = (int)RenderQueue.AlphaTest;
        _materials.Add(m);
        return m;
    }

    // ── 절차 텍스처 ──────────────────────────────────────────

    private static Texture2D NewTex(int size, string name)
    {
        Texture2D t = new Texture2D(size, size, TextureFormat.RGBA32, false);
        t.name = name;
        t.wrapMode = TextureWrapMode.Clamp;
        return t;
    }

    private static Texture2D Ring()
    {
        if (s_ring != null) return s_ring;
        const int n = 128;
        s_ring = NewTex(n, "anomaly ring");
        Color32[] px = new Color32[n * n];
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n * 2f - 1f;
                float v = (y + 0.5f) / n * 2f - 1f;
                float r = Mathf.Sqrt(u * u + v * v);
                // 44차: 매끈한 원 대신 끊기고 번진 빛무리(렌즈에 맺힌 번짐) — 모양 단서는 남기되 UI 테두리처럼 보이지 않게.
                float ang = Mathf.Atan2(v, u);
                float broken = 0.55f + 0.45f * Mathf.Clamp01(0.5f + 0.5f * Mathf.Sin(ang * 3f + 0.7f) * Mathf.Sin(ang * 7f + 2.1f) + 0.25f * Mathf.Sin(ang * 13f));
                float wobble = 0.82f + 0.025f * Mathf.Sin(ang * 5f + 1.3f);
                float a = broken * (0.75f * Mathf.Exp(-Mathf.Pow((r - wobble) / 0.08f, 2f)) + 0.35f * Mathf.Exp(-Mathf.Pow((r - wobble) / 0.2f, 2f)));
                px[y * n + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255f));
            }
        }

        s_ring.SetPixels32(px);
        s_ring.Apply();
        return s_ring;
    }

    private static Texture2D Dot()
    {
        if (s_dot != null) return s_dot;
        const int n = 64;
        s_dot = NewTex(n, "anomaly dot");
        Color32[] px = new Color32[n * n];
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n * 2f - 1f;
                float v = (y + 0.5f) / n * 2f - 1f;
                float a = Mathf.Clamp01(Mathf.Exp(-(u * u + v * v) * 5f) * 1.3f);
                px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }

        s_dot.SetPixels32(px);
        s_dot.Apply();
        return s_dot;
    }

    private static Texture2D Bar()
    {
        if (s_bar != null) return s_bar;
        const int n = 64;
        s_bar = NewTex(n, "anomaly bar");
        Color32[] px = new Color32[n * n];
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float u = Mathf.Abs((x + 0.5f) / n * 2f - 1f);
                float v = Mathf.Abs((y + 0.5f) / n * 2f - 1f);
                float a = Mathf.Clamp01((1f - Mathf.Pow(u, 8f)) * Mathf.Exp(-v * v * 3f) * 1.2f);
                px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }

        s_bar.SetPixels32(px);
        s_bar.Apply();
        return s_bar;
    }

    /// <summary>65차 C-3: 물건이 오래 서 있던 자리 — 가장자리가 번진 네모, 테두리가 조금 더 짙고 안쪽은 얼룩진다(반투명).</summary>
    private static Texture2D Footprint()
    {
        if (s_footprint != null) return s_footprint;
        const int n = 128;
        s_footprint = NewTex(n, "anomaly footprint");
        Color32[] px = new Color32[n * n];
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float u = Mathf.Abs((x + 0.5f) / n * 2f - 1f);
                float v = Mathf.Abs((y + 0.5f) / n * 2f - 1f);
                float d = Mathf.Max(u, v);
                float inside = Mathf.Clamp01((0.92f - d) / 0.12f);
                float rim = Mathf.Exp(-Mathf.Pow((d - 0.8f) / 0.07f, 2f));
                float mottle = 0.75f + 0.25f * Mathf.PerlinNoise(x * 0.09f + 3.1f, y * 0.09f + 7.7f);
                float a = Mathf.Clamp01((inside * 0.6f + rim * 0.55f) * mottle);
                px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }

        s_footprint.SetPixels32(px);
        s_footprint.Apply();
        return s_footprint;
    }

    /// <summary>가장자리가 불규칙한 물웅덩이(알파 컷).</summary>
    private static Texture2D Blob(int seed)
    {
        const int n = 128;
        Texture2D t = NewTex(n, "anomaly water");
        System.Random rng = new System.Random(seed);
        float[] amp = new float[5];
        float[] ph = new float[5];
        for (int i = 0; i < 5; i++)
        {
            amp[i] = 0.05f + (float)rng.NextDouble() * 0.09f / (i + 1);
            ph[i] = (float)rng.NextDouble() * 6.283f;
        }

        Color32[] px = new Color32[n * n];
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n * 2f - 1f;
                float v = (y + 0.5f) / n * 2f - 1f;
                float ang = Mathf.Atan2(v, u);
                float edge = 0.82f;
                for (int i = 0; i < 5; i++) edge += amp[i] * Mathf.Sin(ang * (i + 2) + ph[i]);
                float r = Mathf.Sqrt(u * u + v * v);
                byte a = (byte)(Mathf.Clamp01((edge - r) / 0.1f) * 255f);
                px[y * n + x] = new Color32(255, 255, 255, a);
            }
        }

        t.SetPixels32(px);
        t.Apply();
        return t;
    }

    /// <summary>엉킨 검은 머리카락(가는 곡선 여러 가닥, 알파 컷).</summary>
    private static Texture2D HairTex()
    {
        if (s_hair != null) return s_hair;
        const int n = 256;
        s_hair = NewTex(n, "anomaly hair");
        Color32[] px = new Color32[n * n];
        System.Random rng = new System.Random(4021);
        for (int s = 0; s < 420; s++)
        {
            float x = (float)(rng.NextDouble() * 0.7 + 0.15) * n;
            float y = (float)(rng.NextDouble() * 0.7 + 0.15) * n;
            float a = (float)(rng.NextDouble() * Math.PI * 2);
            int steps = 60 + rng.Next(120);
            for (int i = 0; i < steps; i++)
            {
                a += (float)(rng.NextDouble() - 0.5) * 0.5f;
                x += Mathf.Cos(a) * 0.9f;
                y += Mathf.Sin(a) * 0.9f;
                int ix = (int)x;
                int iy = (int)y;
                if (ix < 0 || iy < 0 || ix >= n || iy >= n) break;
                px[iy * n + ix] = new Color32(255, 255, 255, 255);
                if (ix + 1 < n) px[iy * n + ix + 1] = new Color32(255, 255, 255, 255);
            }
        }

        s_hair.SetPixels32(px);
        s_hair.Apply();
        return s_hair;
    }
}

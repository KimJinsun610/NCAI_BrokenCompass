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
/// <item>[옮김] H-4 사물함 문이 열림(그 사물함만 잠금이 풀려 [E]로 여닫힌다 — 43차) · S-1 인체 모형이 돌아섬 · C-1 화분이 창가 반대편 책상 위로 · L-1 의자 하나가 빠져 출입구를 향함.
/// 정적 배칭으로 묶인 소품(C-1·L-1)은 원본을 숨기고 같은 프리팹을 옮긴 자리에 세운다(<see cref="InspectionAnomalyPropsSO"/>). 점검 기준점은 소품을 따라간다(42차).</item>
/// <item>[켬] H-2 식수대 앞 물(웅덩이 → 줄기 → 침수) · T-1 변기에 검은 머리카락, 넘친 물 · K-1 한 채널에 CCTV에만 보이는 사람이 천천히 지나감.</item>
/// <item>[빛] H-1 압력계 · C-2 책상 램프 · S-2 현미경 불 · T-3 거울 위 형광등 깜빡임(<see cref="PhotosensitiveSafe"/>면 2Hz 아래의 느린 맥동) · L-2 블라인드 하나가 올라가 달빛.
/// 빛 이상은 색과 함께 대상 둘레의 후광 링(정적인 모양 단서)을 같이 쓴다(<see cref="HaloRing"/>).</item>
/// </list>
/// 「가까이」 연출과 [소리] 틀은 여기서 다루지 않는다. 근무 씬에 자동으로 선다.
/// </summary>
[DisallowMultipleComponent]
public sealed class InspectionAnomalies : MonoBehaviour
{
    /// <summary>광과민 옵션 — 켜면 T-3의 깜빡임이 2Hz 아래의 느린 맥동이 된다.</summary>
    public static bool PhotosensitiveSafe;

    /// <summary>빛 이상의 후광 링(최종 기획서 접근성: 색과 함께 정적인 모양 단서).</summary>
    public static bool HaloRing = true;

    private static readonly float[] SpreadWidthByBand = { 0f, 0.6f, 0.45f, 1.2f, 2.4f };

    private sealed class Look
    {
        public string ItemId;
        public Band Band;
        public readonly List<GameObject> Objects = new List<GameObject>();
        public readonly List<Action> Undo = new List<Action>();
        public readonly List<Flicker> Flickers = new List<Flicker>();
        public Walker Walker;
    }

    private sealed class Flicker
    {
        public Light Light;
        public float Base;
        public Renderer Glow;
        public Color GlowColor;
        public float Hz;
        public float Seed;
    }

    private sealed class Walker
    {
        public GameObject Go;
        public CctvOnlyVisible Only;
        public int Channel = -1;
        public Vector3 A;
        public Vector3 B;
        public float Speed;
        public float Wait;
        public float At;
        public bool Forward = true;
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

    private readonly List<Look> _looks = new List<Look>();
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
        if (IsApplied(itemId) || itemId == "K-1") return null;
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
                    Look look = Build(rows[i].Id, rows[i].Intensity);
                    if (look != null) _looks.Add(look);
                }

                if (DirectionStage.Verbose && _looks.Count > 0) Debug.Log("[InspectionAnomalies] " + plan.Day + "일차 이상 연출: " + Summary);
            }
        }

        for (int i = 0; i < _looks.Count; i++)
        {
            Look look = _looks[i];
            for (int f = 0; f < look.Flickers.Count; f++) Tick(look.Flickers[f]);
            if (look.ItemId == "K-1") TickWalker(look);
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

    private void ClearAll()
    {
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
    }

    // ── 항목별 ───────────────────────────────────────────────

    private Look Build(string itemId, Band band)
    {
        InspectionItem item = InspectionCatalog.Find(itemId);
        if (!AnomalyLook.HasLook(item)) return null;

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
                Glow(look, at, new Color(0.45f, 1f, 0.55f), 0.45f, 0.5f * glow, 0.045f);
                Halo(look, b, new Color(0.5f, 1f, 0.6f));
                break;
            }
            case "C-2":
            {
                Vector3 at = new Vector3(b.center.x, b.max.y - 0.1f, b.center.z) + Flat(prop.forward) * 0.1f;
                Glow(look, at, new Color(1f, 0.78f, 0.5f), 2.6f, 1.6f * glow, 0.08f);
                Halo(look, b, new Color(1f, 0.8f, 0.5f));
                break;
            }
            case "S-2":
            {
                Vector3 at = new Vector3(b.center.x, b.min.y + 0.1f, b.center.z);
                Glow(look, at, new Color(0.82f, 0.9f, 1f), 0.9f, 0.8f * glow, 0.04f);   // 43차: 3구간에서 하얗게 날아갔다
                Halo(look, b, new Color(0.8f, 0.9f, 1f));
                break;
            }
            case "T-3":
                Mirror(look, prop, b, band, glow);
                break;
            case "L-2":
                Blind(look, prop, jt.transform, b, glow);
                break;
            case "H-4":
                ok = LockerDoor(look, prop, band);
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
                ok = PlantToDesk(look, prop, jt.transform, b, band);
                break;
            case "L-1":
                ok = ChairOut(look, prop, jt.transform, band);
                break;
            case "H-2":
                Water(look, prop, b, band, Flat(prop.forward), new Color(0.015f, 0.02f, 0.025f, 0.8f), 0.25f, false);
                break;
            case "T-1":
                Water(look, prop, b, band, Flat(prop.forward), new Color(0.03f, 0.028f, 0.02f, 0.82f), 0.3f, true);
                Hair(look, prop, b, band);
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

    // ── [빛] ─────────────────────────────────────────────────

    private Light Glow(Look look, Vector3 at, Color color, float range, float intensity, float dotSize)
    {
        GameObject go = new GameObject("이상 빛 " + look.ItemId);
        go.transform.position = at;
        Light l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = color;
        l.range = range;
        l.intensity = intensity;
        l.shadows = LightShadows.None;
        l.renderMode = LightRenderMode.ForcePixel;
        look.Objects.Add(go);
        if (dotSize > 0f)
        {
            Renderer dot = Card(look, "빛점", Dot(), at, new Vector2(dotSize, dotSize), new Color(color.r, color.g, color.b, 0.95f), 0.03f);
        }

        return l;
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
        Light l = Glow(look, at + fwd * 0.25f, cold, 2.2f, 0.9f * glow, 0f);
        float width = Mathf.Max(0.3f, Mathf.Max(b.size.x, b.size.z) * 0.85f);
        Renderer tube = Card(look, "형광등", Bar(), at, new Vector2(width, 0.05f), new Color(cold.r, cold.g, cold.b, 0.95f), -1f);
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
        if (f.Light == null) return;
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

        f.Light.intensity = f.Base * k;
        if (f.Glow != null)
        {
            Color c = f.GlowColor;
            c.a *= Mathf.Clamp01(k);
            f.Glow.sharedMaterial.color = c;
        }
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
        l.intensity = 6f * glow;
        l.shadows = LightShadows.None;
        l.renderMode = LightRenderMode.ForcePixel;
        look.Objects.Add(moon);
    }

    // ── [옮김] ───────────────────────────────────────────────

    private static bool LockerDoor(Look look, Transform prop, Band band)
    {
        // 사물함은 평소 잠겨 있다 — 이상인 날 이 사물함만 풀리고 문이 조금 열린 채 서 있다(43차 민: 「여닫히면 [이상]」).
        DoorHandle handle = DoorHandle.Of(prop);
        if (handle.IsValid)
        {
            Component owner = handle.Owner;
            PlayerInteractor.SetLockerUnlocked(owner, true);
            look.Undo.Add(() => PlayerInteractor.SetLockerUnlocked(owner, false));
            // 벤더 열림 애니메이션의 그 각도 지점에 세운다 — [E]로 열면 거기서 마저 열린다.
            // 애니메이션이 처음에 빨리 열리므로(0.21 = 79°) 비율이 아니라 문짝 각도를 재서 찾는다.
            Transform leaf = prop.Find("LockerDoor");
            if (leaf != null && AjarAt(handle, leaf, AnomalyLook.DoorDegrees(band)))
            {
                look.Undo.Add(() =>
                {
                    DoorHandle back = DoorHandle.Of(prop);
                    if (back.IsValid) back.SnapClosed();
                });
                return true;
            }
        }

        Transform door = prop.Find("LockerDoor");
        if (door == null)
        {
            foreach (Transform c in prop)
            {
                if (c.name.Contains("Door"))
                {
                    door = c;
                    break;
                }
            }
        }

        if (door == null) return false;
        Quaternion orig = door.localRotation;
        door.localRotation = orig * Quaternion.Euler(0f, -AnomalyLook.DoorDegrees(band), 0f);   // 경첩(피벗)을 축으로 바깥쪽으로
        look.Undo.Add(() =>
        {
            if (door != null) door.localRotation = orig;
        });
        return true;
    }

    /// <summary>문짝이 닫힌 자세에서 <paramref name="degrees"/>만큼 돌아간 애니메이션 지점을 찾아(이분 탐색) 거기에 세운다.</summary>
    private static bool AjarAt(DoorHandle handle, Transform leaf, float degrees)
    {
        Quaternion closed = leaf.localRotation;
        if (!handle.SetAjar(1f)) return false;
        float max = Quaternion.Angle(closed, leaf.localRotation);
        if (max < 1f) return false;
        float lo = 0f;
        float hi = 1f;
        for (int i = 0; i < 14; i++)
        {
            float mid = (lo + hi) * 0.5f;
            handle.SetAjar(mid);
            if (Quaternion.Angle(closed, leaf.localRotation) < degrees) lo = mid;
            else hi = mid;
        }

        return handle.SetAjar((lo + hi) * 0.5f);
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
        desks.Sort((x, y) => Flat(x.position - origin).sqrMagnitude.CompareTo(Flat(y.position - origin).sqrMagnitude));
        Transform desk = desks[Mathf.Clamp(Mathf.RoundToInt((desks.Count - 1) * AnomalyLook.FarPick(band)), 0, desks.Count - 1)];

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
        Transform door = NearestDoorway(pos, 12f);
        Vector3 face = door != null ? Flat(door.position - pos) : back;
        if (face.sqrMagnitude < 0.0001f) face = back;
        Quaternion rot = Quaternion.LookRotation(face.normalized, Vector3.up);
        return MoveProp(look, prop, judge, pos, rot);
    }

    /// <summary>
    /// 소품을 그 자리·방향으로. 정적 배칭이면 원본을 숨기고 같은 프리팹을 세운다. <b>점검 기준점(<c>Inspect X</c>)은 소품을 따라간다</b>(42차) —
    /// 플레이어는 옮겨 간 소품(외곽선이 그려진 것)에 가서 보고한다.
    /// </summary>
    private bool MoveProp(Look look, Transform prop, Transform judge, Vector3 pos, Quaternion rot)
    {
        if (!IsBatched(prop, judge))
        {
            Vector3 p = prop.position;
            Quaternion r = prop.rotation;
            prop.SetPositionAndRotation(pos, rot);
            look.Undo.Add(() =>
            {
                if (prop != null) prop.SetPositionAndRotation(p, r);
            });
            return true;
        }

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
        if (w == null || cctv == null || cctv.ChannelCount <= 0) return;

        if (w.Go == null)
        {
            int day = NightRun.Day;
            w.Channel = Mathf.Abs(day * 7 + 3) % cctv.ChannelCount;
            Camera cam = cctv.ChannelCamera(w.Channel);
            if (cam == null) return;
            Vector3 eye = cam.transform.position;
            Vector3 fwd = Flat(cam.transform.forward);
            if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
            fwd.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            float dist = Mathf.Lerp(6f, 3.5f, AnomalyLook.Strength(look.Band));
            Vector3 mid = FloorUnder(eye + fwd * dist);
            float left = Free(mid + Vector3.up * 1f, -right, 2.4f);
            float rightFree = Free(mid + Vector3.up * 1f, right, 2.4f);
            // 화면을 비스듬히 가로지른다 — 안쪽 왼편에서 앞쪽 오른편으로(좁은 복도에서도 몇 초는 걸린다).
            w.A = FloorUnder(eye + fwd * (dist + 1.8f) - right * left);
            w.B = FloorUnder(eye + fwd * Mathf.Max(2.2f, dist - 1.2f) + right * rightFree);
            w.Speed = 0.45f;
            w.Go = StandInFactory.Create("mob.blackman", w.A, w.B, string.Empty);
            if (w.Go == null) return;
            w.Go.name = "이상 K-1 화면 속 사람";
            foreach (Collider c in w.Go.GetComponentsInChildren<Collider>(true)) c.enabled = false;
            w.Only = w.Go.AddComponent<CctvOnlyVisible>();
            w.Only.Channel = w.Channel;
            look.Objects.Add(w.Go);
            w.At = 0f;
            w.Wait = 0f;
        }

        if (w.Wait > 0f)
        {
            w.Wait -= Time.deltaTime;
            if (w.Wait <= 0f && w.Only != null) w.Only.enabled = true;
            return;
        }

        float span = Mathf.Max(0.5f, Vector3.Distance(w.A, w.B));
        w.At += Time.deltaTime * w.Speed / span;
        Vector3 from = w.Forward ? w.A : w.B;
        Vector3 to = w.Forward ? w.B : w.A;
        Vector3 dir = Flat(to - from);
        w.Go.transform.position = Vector3.Lerp(from, to, Mathf.Clamp01(w.At));
        if (dir.sqrMagnitude > 0.0001f) w.Go.transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
        if (w.At >= 1f)
        {
            // 다 지나가면 화면에서 사라졌다가 잠시 뒤 반대로.
            w.At = 0f;
            w.Forward = !w.Forward;
            w.Wait = 8f;
            if (w.Only != null) w.Only.enabled = false;
        }
    }

    // ── 도우미 ───────────────────────────────────────────────

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

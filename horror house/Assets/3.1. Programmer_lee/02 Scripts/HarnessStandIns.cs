using System.Collections.Generic;
using NightDuty;
using UnityEngine;

/// <summary>
/// <b>아직 없는 것들의 대역</b>입니다. 프리팹도 음원도 오기 전에 걸어서 시험할 수 있게 하는 것이 전부이고,
/// 전부 런타임 생성물이라 플레이를 끄면 사라집니다. <b>버릴 코드</b>입니다.
///
/// <para><b>왜 필요한가.</b> 판정 배선은 다 돌지만 씬에는 아직 아무것도 <b>보이지</b> 않습니다 —
/// 판정 대상 표식은 Renderer가 없고(하네스 1479행 주석), 인체모형은 흰 큐브이고, 태블릿은 오브젝트 자체가 없고,
/// 소리는 음원이 한 건도 발주되지 않았습니다. 그래서 「무슨 일이 일어났는지」를 사람이 알 수 없습니다.</para>
///
/// <list type="bullet">
/// <item><b>F6</b> — 판정 대상·조우 지점 표식을 보이게. 색으로 종류를, 글자로 ID를 알려 줍니다.</item>
/// <item><b>F7</b> — 공간·구역 상자를 테두리로.</item>
/// <item><b>소리 대역</b> — 음원이 없는 단서가 전달되면 그 자리에 <b>파문</b>이 퍼지고 화면 아래에 한 줄이 뜹니다.
/// <see cref="AnomalyCueDirector.CueFired"/>를 듣습니다.</item>
/// <item><b>인체모형</b> — 하네스의 흰 큐브를 사람 모양(몸통·머리·팔)으로 바꿉니다. 늘 플레이어를 봅니다.</item>
/// <item><b>태블릿</b> — 경비실 자리에 물건을 하나 놓습니다. 1.2m 안으로 들어가면 줍습니다.</item>
/// </list>
///
/// <para><b>GazeProbe는 트리거 콜라이더를 무시합니다.</b> 그래서 여기서 만드는 것에는 콜라이더를 붙이지 않거나
/// 붙이더라도 트리거가 아니어야 합니다 — 응시 레이를 가로채면 판정이 어긋납니다. 표식의 콜라이더는
/// 하네스가 이미 붙여 두었으므로 여기서는 <b>보이게만</b> 하고 콜라이더를 건드리지 않습니다.</para>
/// </summary>
[AddComponentMenu("NightDuty/Debug/Harness Stand-Ins")]
[DisallowMultipleComponent]
[DefaultExecutionOrder(-800)]
public sealed class HarnessStandIns : MonoBehaviour
{
    private const string TargetPrefix = "__harness_target ";
    private const string ModelPrefix = "__harness_model ";

    /// <summary>파문이 퍼지는 시간(초).</summary>
    private const float RippleSeconds = 1.6f;

    /// <summary>파문이 닿는 최대 반지름(m).</summary>
    private const float RippleRadius = 2.2f;

    /// <summary>화면 아래 기록에 남기는 줄 수.</summary>
    private const int LogLines = 6;

    /// <summary>태블릿을 줍는 거리(m).</summary>
    private const float PickupMeters = 1.2f;

    private static HarnessStandIns s_instance;

    [SerializeField] private bool showMarkers = true;
    [SerializeField] private bool showZones;
    [SerializeField] private bool makeTablet = true;
    [SerializeField] private bool visualSounds = true;

    private Material _markerMat;
    private Material _rippleMat;
    private Material _modelMat;

    private readonly List<GameObject> _markerVisuals = new List<GameObject>();
    private readonly List<GameObject> _zoneVisuals = new List<GameObject>();
    private readonly List<Ripple> _ripples = new List<Ripple>();
    private readonly List<Note> _notes = new List<Note>();
    private readonly Dictionary<string, GameObject> _dressed = new Dictionary<string, GameObject>();

    // 점검 상태 — SpaceZones의 private을 읽는다. 「점검」이 무엇인지 화면에 보여 주려는 것뿐이다.
    private SpaceZones _zones;
    private System.Reflection.FieldInfo _fCurrent;
    private System.Reflection.FieldInfo _fDwell;
    private System.Reflection.FieldInfo _fInspected;
    private System.Reflection.FieldInfo _fDwellNeeded;
    private SpaceId _prevSpace = SpaceId.None;
    private bool _prevInspected;

    private GameObject _tablet;
    private bool _tabletTaken;
    private Camera _camera;
    private GUIStyle _line;
    private GUIStyle _tag;
    private Texture2D _px;

    private struct Ripple
    {
        public Transform Body;
        public float Born;
        public Color Tint;
    }

    private struct Note
    {
        public string Text;
        public float Born;
        public Color Tint;
    }

    // ─────────────────────────────── 설치 ───────────────────────────────

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (s_instance != null || FindAnyObjectByType<SpaceZones>() == null)
        {
            return;
        }

        // hideFlags를 붙이지 않는다 — DontSave는 플레이를 꺼도 오브젝트를 남긴다(CLAUDE.md §5.5-23).
        GameObject go = new GameObject("__HarnessStandIns (runtime)");
        go.AddComponent<HarnessStandIns>();
    }

    private void Awake()
    {
        s_instance = this;
    }

    private void OnEnable()
    {
        AnomalyCueDirector.CueFired += OnCueFired;
        EventBus.BandChanged += OnBandChanged;
        EventBus.AxisCritical += OnAxisCritical;
        EventBus.MessageSent += OnMessageSent;
    }

    private void OnDisable()
    {
        AnomalyCueDirector.CueFired -= OnCueFired;
        EventBus.BandChanged -= OnBandChanged;
        EventBus.AxisCritical -= OnAxisCritical;
        EventBus.MessageSent -= OnMessageSent;
    }

    private void OnDestroy()
    {
        if (s_instance == this)
        {
            s_instance = null;
        }

        Toss(_markerMat);
        Toss(_rippleMat);
        Toss(_modelMat);
        Toss(_px);
    }

    private static void Toss(Object o)
    {
        if (o != null)
        {
            Destroy(o);
        }
    }

    // ─────────────────────────────── 매 프레임 ───────────────────────────────

    private void Update()
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.F6))
        {
            showMarkers = !showMarkers;
            RefreshMarkers();
        }

        if (Input.GetKeyDown(KeyCode.F7))
        {
            showZones = !showZones;
            RefreshZones();
        }
#endif

        if (_camera == null)
        {
            _camera = Camera.main;
        }

        RefreshMarkers();
        DressModels();
        TendTablet();
        TendInspection();
        StepRipples();
    }

    // ─────────────────────── 표식을 보이게 (F6) ───────────────────────

    /// <summary>
    /// 하네스가 만든 표식은 콜라이더만 있고 <b>Renderer가 없습니다</b>. 그 자리에 작은 상자를 하나 더 세워 보이게 합니다.
    /// <para>표식 자체에 Renderer를 붙이지 않는 이유가 있습니다 — 붙이면 그림자를 드리우고 라이트 프로브를 먹으며,
    /// 무엇보다 <b>실제 대상이 씬에 들어왔을 때 지우기 어려워집니다</b>. 별도 오브젝트로 두면 이 컴포넌트만 꺼도 사라집니다.</para>
    /// </summary>
    private void RefreshMarkers()
    {
        if (!showMarkers)
        {
            if (_markerVisuals.Count > 0)
            {
                Clear(_markerVisuals);
            }

            return;
        }

        if (_markerVisuals.Count > 0)
        {
            return;   // 이미 세워 두었다. 표식은 밤마다 늘지 않는다.
        }

        Transform[] all = FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            if (!all[i].name.StartsWith(TargetPrefix, System.StringComparison.Ordinal))
            {
                continue;
            }

            string id = all[i].name.Substring(TargetPrefix.Length);
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = "__standin_marker " + id;
            Destroy(box.GetComponent<Collider>());   // 응시 레이를 가로채면 안 된다.
            box.transform.SetParent(all[i], false);
            box.transform.localPosition = Vector3.zero;
            box.transform.localScale = Vector3.one * 0.16f;

            Renderer r = box.GetComponent<Renderer>();
            r.sharedMaterial = MarkerMaterial();
            r.material.color = ColorOf(id);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;

            _markerVisuals.Add(box);
        }
    }

    /// <summary>종류마다 색이 다릅니다. <b>어둡게 잡습니다</b> — 씬에 블룸이 걸려 있어 밝은 Unlit은 한가운데가 하얗게 날아가고, 그러면 색으로 종류를 가르는 뜻이 사라집니다(2026-09-23 실측).</summary>
    private static Color ColorOf(string id)
    {
        if (id.StartsWith("scene.", System.StringComparison.Ordinal))
        {
            return id.EndsWith(".gate", System.StringComparison.Ordinal)
                ? new Color(0.26f, 0.13f, 0.38f)        // 퇴실 게이트 — 보라
                : new Color(0.38f, 0.19f, 0.05f);        // 조우 접근 — 주황
        }

        if (id.Contains("door") || id.Contains("stall"))
        {
            return new Color(0.38f, 0.33f, 0.08f);   // 문 — 노랑
        }

        if (id.Contains("light"))
        {
            return new Color(0.16f, 0.38f, 0.32f);   // 등 — 민트
        }

        return new Color(0.09f, 0.30f, 0.40f);       // 그 밖의 판정 대상 — 청록
    }

    // ─────────────────────── 구역 테두리 (F7) ───────────────────────

    private void RefreshZones()
    {
        if (!showZones)
        {
            Clear(_zoneVisuals);
            return;
        }

        if (_zoneVisuals.Count > 0)
        {
            return;
        }

        SpaceZones zones = FindAnyObjectByType<SpaceZones>();
        if (zones == null)
        {
            return;
        }

        System.Reflection.BindingFlags any = System.Reflection.BindingFlags.Instance |
                                             System.Reflection.BindingFlags.Public |
                                             System.Reflection.BindingFlags.NonPublic;

        AddZoneArray(zones, zones.GetType().GetField("zones", any), "Space", new Color(0.3f, 0.7f, 1f, 0.12f));
        AddZoneArray(zones, zones.GetType().GetField("signalZones", any), "Id", new Color(1f, 0.6f, 0.2f, 0.16f));
        AddZoneArray(zones, zones.GetType().GetField("zones", any), "Space", new Color(0.4f, 1f, 0.5f, 0.14f), "InspectionBox");
    }

    private void AddZoneArray(SpaceZones zones, System.Reflection.FieldInfo field, string labelField, Color tint, string boxField = "Box")
    {
        if (field == null)
        {
            return;
        }

        System.Array arr = field.GetValue(zones) as System.Array;
        if (arr == null)
        {
            return;
        }

        foreach (object z in arr)
        {
            System.Reflection.FieldInfo boxInfo = z.GetType().GetField(boxField);
            if (boxInfo == null)
            {
                continue;
            }

            Bounds b = (Bounds)boxInfo.GetValue(z);
            if (b.size.sqrMagnitude <= 0f)
            {
                continue;   // 크기 0은 점검 없는 공간이다.
            }

            System.Reflection.FieldInfo nameField = z.GetType().GetField(labelField);
            string label = nameField != null ? nameField.GetValue(z).ToString() : "?";

            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = "__standin_zone " + label;
            Destroy(box.GetComponent<Collider>());
            box.transform.position = b.center;
            box.transform.localScale = b.size;

            Renderer r = box.GetComponent<Renderer>();
            r.sharedMaterial = MarkerMaterial();
            r.material.color = tint;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;

            _zoneVisuals.Add(box);
        }
    }

    // ─────────────────────── 인체모형 대역 ───────────────────────

    /// <summary>
    /// 하네스가 세우는 <c>__harness_model</c>은 0.5 × 1.7 × 0.5 흰 큐브입니다. 그대로는 무엇인지 알 수 없어서
    /// <b>몸통·머리·팔</b>을 붙여 사람 모양으로 만들고, 늘 플레이어 쪽을 보게 합니다.
    /// <para>진짜 모형 프리팹이 오면 <see cref="PlaceSceneModel"/> 쪽만 바꾸면 되고 이 파일은 지웁니다.</para>
    /// </summary>
    private void DressModels()
    {
        Transform[] all = FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (!t.name.StartsWith(ModelPrefix, System.StringComparison.Ordinal))
            {
                continue;
            }

            if (_camera != null)
            {
                Vector3 look = _camera.transform.position - t.position;
                look.y = 0f;
                if (look.sqrMagnitude > 0.01f)
                {
                    t.rotation = Quaternion.LookRotation(look);
                }
            }

            if (_dressed.ContainsKey(t.name) && _dressed[t.name] != null)
            {
                continue;
            }

            Renderer body = t.GetComponent<Renderer>();
            if (body != null)
            {
                body.sharedMaterial = ModelMaterial();
                body.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                body.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            }

            GameObject head = Limb(t, PrimitiveType.Sphere, new Vector3(0f, 0.62f, 0f), new Vector3(0.62f, 0.18f, 0.62f));
            Limb(t, PrimitiveType.Cube, new Vector3(-0.62f, 0.12f, 0f), new Vector3(0.36f, 0.30f, 0.6f));
            Limb(t, PrimitiveType.Cube, new Vector3(0.62f, 0.12f, 0f), new Vector3(0.36f, 0.30f, 0.6f));

            _dressed[t.name] = head;
        }
    }

    private GameObject Limb(Transform parent, PrimitiveType kind, Vector3 localPos, Vector3 localScale)
    {
        GameObject go = GameObject.CreatePrimitive(kind);
        go.name = "__standin_limb";
        Destroy(go.GetComponent<Collider>());   // 응시는 몸통이 받는다.
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = localScale;

        Renderer r = go.GetComponent<Renderer>();
        r.sharedMaterial = ModelMaterial();
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        return go;
    }

    // ─────────────────────── 태블릿 대역 ───────────────────────

    /// <summary>
    /// 태블릿은 오브젝트 자체가 없습니다. 플레이어가 시작하는 자리 앞에 하나 놓고,
    /// <see cref="PickupMeters"/>m 안으로 들어가면 줍습니다.
    /// <para><b>키를 쓰지 않습니다</b> — 문 상호작용이 이미 E를 갖고 있어 같은 키를 나눠 쓰면 서로 먹습니다.
    /// 습득 게이트(기획서 §3-3)의 규칙은 아직 정해지지 않았으므로, 여기서는 <b>줍는 순간을 만들어 주기만</b> 합니다.</para>
    /// </summary>
    private void TendTablet()
    {
        if (!makeTablet || _tabletTaken || _camera == null)
        {
            return;
        }

        if (_tablet == null)
        {
            Transform player = _camera.transform;
            Vector3 spot = player.position + player.forward * 1.4f + Vector3.down * 0.35f;

            _tablet = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _tablet.name = "__standin_tablet";
            Destroy(_tablet.GetComponent<Collider>());
            _tablet.transform.position = spot;
            _tablet.transform.localScale = new Vector3(0.26f, 0.02f, 0.37f);

            Renderer r = _tablet.GetComponent<Renderer>();
            r.sharedMaterial = MarkerMaterial();
            r.material.color = new Color(0.12f, 0.34f, 0.42f);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            return;
        }

        _tablet.transform.Rotate(0f, 35f * Time.deltaTime, 0f, Space.World);

        if (Vector3.Distance(_camera.transform.position, _tablet.transform.position) > PickupMeters)
        {
            return;
        }

        _tabletTaken = true;
        Destroy(_tablet);
        Say("태블릿을 주웠습니다 — F1로 근무 지침을 봅니다", new Color(0.55f, 0.95f, 1f));
        Debug.Log("[대역] 태블릿 습득. 습득 게이트 규칙은 아직 기획 미정이라 상태만 알립니다.", this);
    }

    // ─────────────────────── 점검 표시 ───────────────────────

    /// <summary>
    /// <b>「점검」이 무엇인지 보여 줍니다.</b> 수칙 여덟 장이 점검을 성공 조건으로 쓰는데 화면에 아무 표시가 없어서,
    /// 「점검하십시오」를 읽고도 무엇을 하라는 것인지 알 수 없었습니다(2026-09-23 사용자 보고).
    /// <para>실제 규칙(<c>SpaceZones.UpdateSpace</c>): 그 공간의 <b>지정 점검 상자</b> 안에 1초 머물면 체류가 차고,
    /// <b>그 공간을 나갈 때</b> <c>InspectionCompleted</c>가 나갑니다. 방 안에서는 아무 일도 없는 것이 정상입니다 —
    /// 그 「아무 일도 없음」이 바로 사람을 헷갈리게 하던 것이라, 여기서 진행과 완료를 눈에 보이게 합니다.</para>
    /// </summary>
    private void TendInspection()
    {
        if (_zones == null)
        {
            _zones = FindAnyObjectByType<SpaceZones>();
            if (_zones == null)
            {
                return;
            }

            System.Reflection.BindingFlags any = System.Reflection.BindingFlags.Instance |
                                                 System.Reflection.BindingFlags.Public |
                                                 System.Reflection.BindingFlags.NonPublic;
            System.Type t = _zones.GetType();
            _fCurrent = t.GetField("_current", any);
            _fDwell = t.GetField("_dwell", any);
            _fInspected = t.GetField("_inspectedHere", any);
            _fDwellNeeded = t.GetField("inspectionDwellSeconds", any);
        }

        SpaceId space = CurrentSpace();
        bool inspected = ReadBool(_fInspected);

        // 공간이 바뀌는 순간이 곧 판정이 나가는 순간이다.
        if (space != _prevSpace)
        {
            if (_prevInspected && _prevSpace != SpaceId.None)
            {
                Say("[점검] " + SpaceName(_prevSpace) + " 점검 완료", new Color(0.35f, 0.75f, 0.45f));
            }

            if (space != SpaceId.None)
            {
                Say("[진입] " + SpaceName(space), new Color(0.45f, 0.55f, 0.70f));
            }
        }
        else if (inspected && !_prevInspected)
        {
            Say("[점검] " + SpaceName(space) + " 체류를 채웠습니다 — 나가면 완료됩니다", new Color(0.55f, 0.65f, 0.30f));
        }

        _prevSpace = space;
        _prevInspected = inspected;
    }

    private SpaceId CurrentSpace()
    {
        if (_fCurrent == null || _zones == null)
        {
            return SpaceId.None;
        }

        try
        {
            return (SpaceId)_fCurrent.GetValue(_zones);
        }
        catch (System.Exception)
        {
            return SpaceId.None;
        }
    }

    private bool ReadBool(System.Reflection.FieldInfo field)
    {
        if (field == null || _zones == null)
        {
            return false;
        }

        try
        {
            object v = field.GetValue(_zones);
            return v is bool && (bool)v;
        }
        catch (System.Exception)
        {
            return false;
        }
    }

    private float ReadFloat(System.Reflection.FieldInfo field)
    {
        if (field == null || _zones == null)
        {
            return 0f;
        }

        try
        {
            object v = field.GetValue(_zones);
            return v is float ? (float)v : 0f;
        }
        catch (System.Exception)
        {
            return 0f;
        }
    }

    /// <summary>공간 이름. 「Classroom_1_1」로는 1-1 교실인지 알기 어렵습니다.</summary>
    private static string SpaceName(SpaceId space)
    {
        switch (space)
        {
            case SpaceId.Classroom_1_1: return "교실 1-1";
            case SpaceId.Classroom_1_3: return "교실 1-3";
            case SpaceId.ScienceRoom: return "과학실";
            case SpaceId.Toilet: return "화장실";
            case SpaceId.Corridor: return "복도";
            default: return "건물 밖";
        }
    }

    // ─────────────────────── 소리 대역 ───────────────────────

    /// <summary>
    /// 음원이 한 건도 없어서, <b>들려야 할 것을 보이게</b> 합니다. 단서가 나간 자리에 파문이 퍼지고
    /// 화면 아래에 한 줄이 남습니다. 진짜 음원이 오면 이 구독만 끄면 됩니다.
    /// </summary>
    private void OnCueFired(SignalKind kind, string targetId, CueBindingTableSO.Binding binding)
    {
        if (!visualSounds)
        {
            return;
        }

        Color tint = kind == SignalKind.ClueIdentified
            ? new Color(0.30f, 0.60f, 0.36f)
            : new Color(0.55f, 0.44f, 0.16f);

        string what = CueName(binding != null ? binding.CueId : string.Empty);
        int shots = binding != null ? binding.ShotCount : 1;
        string shotText = shots > 1 ? " ×" + shots : string.Empty;

        Say((kind == SignalKind.ClueIdentified ? "[식별] " : "[소리] ") + what + shotText + "  → " + targetId, tint);

        Transform where = MarkerOf(targetId);
        if (where != null)
        {
            Splash(where.position, tint);
        }
    }

    /// <summary>큐 ID를 사람 말로. 표에 없으면 ID를 그대로 보여 줍니다 — 지어내지 않습니다.</summary>
    private static string CueName(string cueId)
    {
        switch (cueId)
        {
            case "chalk.3": return "분필 긁는 소리 세 번";
            case "door.back": return "등 뒤에서 문 닫히는 소리";
            case "desk.back": return "뒤쪽 책상이 울리는 소리";
            case "lectern.noise": return "칠판 긁기 + 의자 마찰";
            case "glass.break": return "유리 파손음";
            case "bench.glass.clink": return "유리 기구가 부딪히는 소리";
            case "flush": return "물 내리는 소리";
            case "sink.call": return "배관을 두드리는 소리";
            case "eraser": return "칠판지우개 소리";
            case "tree.grass": return "풀 밟는 소리";
            default: return string.IsNullOrEmpty(cueId) ? "(이름 없는 큐)" : cueId;
        }
    }

    private void OnBandChanged(SpaceId space, FearAxis axis, Band from, Band to)
    {
        if (from == to)
        {
            return;
        }

        Say("[구간] " + AxisName(axis) + " " + (int)from + " → " + (int)to + " (" + space + ")",
            new Color(1f, 0.6f, 0.45f));
    }

    private void OnAxisCritical(FearAxis axis)
    {
        Say("[한계] " + AxisName(axis) + "가 100에 닿았습니다", new Color(1f, 0.35f, 0.35f));
    }

    private void OnMessageSent(ParadoxMessage message)
    {
        Say("[문자] " + message.ParadoxId + (message.CardId.Length > 0 ? " (" + message.CardId + ")" : string.Empty),
            new Color(0.8f, 0.7f, 1f));
    }

    private static string AxisName(FearAxis axis)
    {
        switch (axis)
        {
            case FearAxis.Auditory: return "청각";
            case FearAxis.Illuminance: return "조도";
            case FearAxis.Layout: return "배치";
            default: return "신뢰";
        }
    }

    private static Transform MarkerOf(string id)
    {
        JudgeTarget target;
        return JudgeTargetRegistry.TryGet(id, out target) && target != null ? target.transform : null;
    }

    /// <summary>그 자리에서 퍼지는 파문 하나.</summary>
    private void Splash(Vector3 at, Color tint)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "__standin_ripple";
        Destroy(go.GetComponent<Collider>());
        go.transform.position = at;
        go.transform.localScale = Vector3.one * 0.1f;

        Renderer r = go.GetComponent<Renderer>();
        r.sharedMaterial = RippleMaterial();
        r.material.color = tint;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;

        Ripple ripple;
        ripple.Body = go.transform;
        ripple.Born = Time.unscaledTime;
        ripple.Tint = tint;
        _ripples.Add(ripple);
    }

    private void StepRipples()
    {
        for (int i = _ripples.Count - 1; i >= 0; i--)
        {
            Ripple ripple = _ripples[i];
            float age = (Time.unscaledTime - ripple.Born) / RippleSeconds;

            if (age >= 1f || ripple.Body == null)
            {
                if (ripple.Body != null)
                {
                    Destroy(ripple.Body.gameObject);
                }

                _ripples.RemoveAt(i);
                continue;
            }

            ripple.Body.localScale = Vector3.one * Mathf.Lerp(0.1f, RippleRadius, age);

            Renderer r = ripple.Body.GetComponent<Renderer>();
            if (r != null)
            {
                Color c = ripple.Tint;
                c.a = Mathf.Lerp(0.30f, 0f, age);
                r.material.color = c;
            }
        }
    }

    private void Say(string text, Color tint)
    {
        Note note;
        note.Text = text;
        note.Born = Time.unscaledTime;
        note.Tint = tint;
        _notes.Add(note);

        while (_notes.Count > LogLines)
        {
            _notes.RemoveAt(0);
        }
    }

    // ─────────────────────────────── 화면 ───────────────────────────────

    private void OnGUI()
    {
        EnsureStyles();

        float scale = Mathf.Clamp(Screen.width / 1920f, 0.55f, 1.2f);
        Matrix4x4 saved = GUI.matrix;
        GUIUtility.ScaleAroundPivot(new Vector2(scale, scale), Vector2.zero);

        try
        {
            float w = Screen.width / scale;
            float h = Screen.height / scale;

            DrawInspection(w);
            DrawNotes(w, h);

            if (showMarkers)
            {
                DrawTags();
            }
        }
        finally
        {
            GUI.matrix = saved;
        }
    }

    /// <summary>
    /// 화면 위 가운데에 <b>지금 어느 공간인지</b>와 <b>점검 진행</b>을 띄웁니다.
    /// 점검은 방 안에서는 아무 반응이 없어서, 이 막대가 없으면 「하고 있는 중」인지 알 수 없습니다.
    /// </summary>
    private void DrawInspection(float w)
    {
        if (_zones == null)
        {
            return;
        }

        SpaceId space = CurrentSpace();
        if (space == SpaceId.None)
        {
            return;
        }

        bool done = ReadBool(_fInspected);
        float need = Mathf.Max(0.01f, ReadFloat(_fDwellNeeded));
        float have = Mathf.Clamp(ReadFloat(_fDwell), 0f, need);
        bool inside = have > 0f || done;

        const float boxW = 300f;
        Rect box = new Rect((w - boxW) * 0.5f, 12f, boxW, inside ? 46f : 26f);

        Color saved = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.45f);
        GUI.DrawTexture(box, Pixel());
        GUI.color = saved;

        GUI.Label(new Rect(box.x + 10f, box.y + 3f, boxW - 20f, 20f), SpaceName(space), _line);

        if (!inside)
        {
            return;
        }

        Rect track = new Rect(box.x + 10f, box.y + 26f, boxW - 20f, 10f);
        GUI.color = new Color(1f, 1f, 1f, 0.15f);
        GUI.DrawTexture(track, Pixel());

        GUI.color = done ? new Color(0.35f, 0.75f, 0.45f) : new Color(0.60f, 0.62f, 0.25f);
        GUI.DrawTexture(new Rect(track.x, track.y, track.width * (done ? 1f : have / need), track.height), Pixel());
        GUI.color = saved;

        GUI.Label(new Rect(box.x + 10f, box.y + 24f, boxW - 20f, 20f),
            done ? "      점검함 — 나가면 완료됩니다" : "      점검 중 " + have.ToString("0.0") + " / " + need.ToString("0.0") + "초", _tag);
    }

    /// <summary>방금 일어난 일 몇 줄. 오래된 줄부터 옅어집니다.</summary>
    private void DrawNotes(float w, float h)
    {
        if (_notes.Count == 0)
        {
            return;
        }

        float y = h - 210f;
        Color saved = GUI.color;

        for (int i = 0; i < _notes.Count; i++)
        {
            Note note = _notes[i];
            float age = Time.unscaledTime - note.Born;
            float fade = Mathf.Clamp01(1f - (age - 6f) / 3f);

            if (fade <= 0f)
            {
                continue;
            }

            Color c = note.Tint;
            c.a = fade;
            GUI.color = new Color(0f, 0f, 0f, 0.5f * fade);
            GUI.DrawTexture(new Rect(14f, y - 2f, 620f, 22f), Pixel());
            GUI.color = c;
            GUI.Label(new Rect(20f, y, 610f, 22f), note.Text, _line);
            y += 24f;
        }

        GUI.color = saved;
    }

    /// <summary>표식 위에 ID를 적습니다. 가까운 것만 — 멀리 있는 글자는 읽히지도 않고 화면만 덮습니다.</summary>
    private void DrawTags()
    {
        if (_camera == null)
        {
            return;
        }

        Color saved = GUI.color;

        for (int i = 0; i < _markerVisuals.Count; i++)
        {
            GameObject box = _markerVisuals[i];
            if (box == null)
            {
                continue;
            }

            Vector3 world = box.transform.position + Vector3.up * 0.2f;
            float dist = Vector3.Distance(_camera.transform.position, world);
            if (dist > 9f)
            {
                continue;
            }

            Vector3 screen = _camera.WorldToScreenPoint(world);
            if (screen.z <= 0f)
            {
                continue;
            }

            float scale = Mathf.Clamp(Screen.width / 1920f, 0.55f, 1.2f);
            float x = screen.x / scale;
            float y = (Screen.height - screen.y) / scale;

            GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01(1f - dist / 9f));
            GUI.Label(new Rect(x - 90f, y - 10f, 180f, 18f), box.name.Substring("__standin_marker ".Length), _tag);
        }

        GUI.color = saved;
    }

    // ─────────────────────────────── 거들기 ───────────────────────────────

    private void Clear(List<GameObject> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] != null)
            {
                Destroy(list[i]);
            }
        }

        list.Clear();
    }

    /// <summary>표식용 불투명 재질. URP/Lit을 쓰되 빛을 받지 않게 방출로만 칠합니다.</summary>
    private Material MarkerMaterial()
    {
        if (_markerMat == null)
        {
            _markerMat = MakeUnlit(false);
        }

        return _markerMat;
    }

    private Material RippleMaterial()
    {
        if (_rippleMat == null)
        {
            _rippleMat = MakeUnlit(true);
        }

        return _rippleMat;
    }

    private Material ModelMaterial()
    {
        if (_modelMat == null)
        {
            _modelMat = MakeUnlit(false);
            _modelMat.color = new Color(0.30f, 0.29f, 0.27f);
        }

        return _modelMat;
    }

    /// <summary>
    /// 빛을 받지 않는 재질. 밤이 캄캄해졌으므로 <b>조명에 기대면 대역이 안 보입니다</b> —
    /// URP의 Unlit을 쓰고, 없으면 Lit에 방출을 켭니다.
    /// </summary>
    private static Material MakeUnlit(bool transparent)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
        {
            shader = Shader.Find("Unlit/Color");
        }

        Material m = new Material(shader);

        if (!transparent)
        {
            return m;
        }

        // 반투명으로 — 파문은 겹쳐 보여야 한다.
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 0f);
        m.SetFloat("_ZWrite", 0f);
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
        return m;
    }

    private Texture2D Pixel()
    {
        if (_px == null)
        {
            _px = new Texture2D(1, 1);
            _px.SetPixel(0, 0, Color.white);
            _px.Apply();
        }

        return _px;
    }

    private void EnsureStyles()
    {
        if (_line != null)
        {
            return;
        }

        _line = new GUIStyle(GUI.skin.label);
        _line.fontSize = 14;

        _tag = new GUIStyle(GUI.skin.label);
        _tag.fontSize = 11;
        _tag.alignment = TextAnchor.MiddleCenter;
    }
}

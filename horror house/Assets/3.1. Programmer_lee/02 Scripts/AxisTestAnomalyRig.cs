using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using NightDuty;

/// <summary>
/// 테스트 씬(_Test_AxisRig)에서 축 슬라이더를 움직이면 기획서의 공간별 <b>청각 · 배치</b> 이상현상을
/// 임시 소리와 임시 소품으로 미리 보여 주는 검증용 컴포넌트입니다. (조도는 AxisTestLightRig가 담당)
/// </summary>
/// <remarks>
/// <b>버릴 실험용 코드</b>입니다. 개인 폴더(Assembly-CSharp)에 있습니다.
///
/// 하는 일
/// - 청각 구간이 <b>오를 때</b> 그 구간의 큐를 순서대로 재생합니다(합성음). 소리 위치에 빨간 구슬이 잠깐 보입니다.
/// - 배치 구간이 바뀌면 그 구간에서 보여야 하는 소품만 켭니다. 새로 생긴 사건(문 열림·낙하 등)은 한 번 움직입니다.
/// - 화면 왼쪽 위 패널에서 공간 전환(복도·교실·과학실·화장실), 축 값 ±, 다시 재생을 할 수 있습니다.
///
/// 한계 — 판단용이 아니라 <b>표가 맞게 읽히는지</b> 확인용입니다.
/// - 소리는 톤/잡음 합성이라 실제 음원이 아닙니다. 물 내림은 실제 12초 대신 3초입니다.
/// - 공간 넷 모두 같은 복도 상자 안에 소품만 바꿔 놓습니다(교실·화장실 형태 아님).
/// - 트리거 조건(「퇴실할 때」「별도 방문」 등)은 흉내내지 않고, 구간이 오르는 순간 바로 재생합니다.
///
/// 씬 파일을 고치지 않습니다. 플레이를 시작하면 <c>__AxisTest</c> 오브젝트에 자동으로 붙고,
/// 소품은 플레이 중에만 만들어졌다가 사라집니다.
/// </remarks>
[AddComponentMenu("NightDuty/Debug/Axis Test Anomaly Rig")]
[DisallowMultipleComponent]
public sealed class AxisTestAnomalyRig : MonoBehaviour
{
    private const string SceneName = "_Test_AxisRig";
    private const string HostName = "__AxisTest";
    private const string TablePath = "Assets/_Game/ScriptableObjects/SpaceAnomalyTable.asset";

    private static readonly SpaceId[] Cycle =
    {
        SpaceId.Corridor, SpaceId.Classroom_1_1, SpaceId.ScienceRoom, SpaceId.Toilet
    };

    private static readonly string[] AxisNames = { "청각", "조도", "배치", "신뢰" };

    [Header("어느 공간 표를 볼지")]
    [SerializeField] private SpaceId _space = SpaceId.Corridor;

    [Header("이상현상 표")]
    [Tooltip("비워 두면 " + TablePath + " 를 불러옵니다(에디터 전용).")]
    [SerializeField] private SpaceAnomalyTableSO _table;

    [Header("동작")]
    [Tooltip("청각 구간이 오를 때 자동 재생")]
    [SerializeField] private bool _playOnBandRise = true;
    [SerializeField] private bool _showOverlay = true;
    [Range(0f, 1f)] [SerializeField] private float _volume = 0.8f;

    private readonly Band[] _bands = new Band[4];
    private readonly List<string> _log = new List<string>();
    private readonly Dictionary<string, Prop> _props = new Dictionary<string, Prop>();
    private readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();
    private readonly HashSet<string> _shownLayout = new HashSet<string>();

    private IFearAxisReader _reader;
    private GameObject _propRoot;
    private Coroutine _sequence;
    private Material _markerMat;
    private Transform _listener;

    // ─────────────────────────────── 자동 부착 ───────────────────────────────

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoAttach()
    {
        if (SceneManager.GetActiveScene().name != SceneName) return;
        GameObject host = GameObject.Find(HostName);
        if (host == null || host.GetComponent<AxisTestAnomalyRig>() != null) return;
        host.AddComponent<AxisTestAnomalyRig>();
    }

    // ─────────────────────────────── 수명 ───────────────────────────────

    private void Awake()
    {
#if UNITY_EDITOR
        if (_table == null)
        {
            _table = UnityEditor.AssetDatabase.LoadAssetAtPath<SpaceAnomalyTableSO>(TablePath);
        }
#endif
    }

    private void OnEnable()
    {
        EventBus.BandChanged += HandleBandChanged;
    }

    private void OnDisable()
    {
        EventBus.BandChanged -= HandleBandChanged;
        if (_propRoot != null) Destroy(_propRoot);
        _propRoot = null;
        _props.Clear();
    }

    private void Start()
    {
        if (_table == null)
        {
            Debug.LogWarning("[AnomalyRig] 이상현상 표가 없습니다. 메뉴 NightDuty > 이상현상 표 에셋 생성 을 먼저 실행하십시오.", this);
        }

        _reader = GetComponent<IFearAxisReader>();
        Camera cam = Camera.main;
        _listener = cam != null ? cam.transform : transform;
        if (FindAnyObjectByType<AudioListener>() == null)
        {
            // 테스트 씬 카메라에 리스너가 없으면 플레이 중에만 붙인다(씬에는 저장되지 않음).
            _listener.gameObject.AddComponent<AudioListener>();
        }
        SwitchSpace(_space);
    }

    // ─────────────────────────────── 이벤트 ───────────────────────────────

    private void HandleBandChanged(SpaceId space, FearAxis axis, Band from, Band to)
    {
        if (space != _space || axis == FearAxis.Trust) return;
        _bands[(int)axis] = to;
        if (from == to) return; // 기준값 재방송

        Note(AxisNames[(int)axis] + " B" + (int)from + "→B" + (int)to + "  " + Label(axis, to));

        if (axis == FearAxis.Auditory && _playOnBandRise && to > from)
        {
            PlayAuditory(to);
        }
        else if (axis == FearAxis.Layout)
        {
            ApplyLayout(to, true);
        }
    }

    private void SwitchSpace(SpaceId space)
    {
        _space = space;
        StopSequence();

        // 조명 리그도 같은 공간으로 돌리고, 축 공급원에 전체 재방송을 요청해 조명이 새 공간 표로 다시 켜지게 한다.
        AxisTestLightRig lights = FindAnyObjectByType<AxisTestLightRig>();
        if (lights != null)
        {
            System.Reflection.FieldInfo f = typeof(AxisTestLightRig).GetField(
                "_space", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (f != null) f.SetValue(lights, space);
        }

        for (int i = 0; i < 3; i++)
        {
            _bands[i] = _reader != null ? _reader.GetBand((FearAxis)i) : Band.Band0;
        }

        BuildProps(space);
        _shownLayout.Clear();
        ApplyLayout(_bands[(int)FearAxis.Layout], false);

        if (lights != null)
        {
            gameObject.SendMessage("PushAll", SendMessageOptions.DontRequireReceiver);
            lights.ReapplyCurrent();
        }

        Note("공간 전환: " + SpaceName(space));
    }

    // ─────────────────────────────── 청각 ───────────────────────────────

    private void PlayAuditory(Band band)
    {
        StopSequence();
        _sequence = StartCoroutine(PlaySequence(Cues(FearAxis.Auditory, band)));
    }

    private void StopSequence()
    {
        if (_sequence != null) StopCoroutine(_sequence);
        _sequence = null;
    }

    private IEnumerator PlaySequence(IReadOnlyList<string> cues)
    {
        for (int i = 0; i < cues.Count; i++)
        {
            string cue = cues[i];
            float wait = 0.2f;

            Sound s;
            if (TryGetSound(cue, out s))
            {
                wait = PlaySound(cue, s) + 0.4f;
            }

            // 소리 큐 중 일부(door.open.silent)는 눈으로 보이는 사건이다.
            Prop p;
            if (_props.TryGetValue(cue, out p))
            {
                p.SetVisible(true, true);
                wait = Mathf.Max(wait, 1.4f);
            }

            Note("  ▶ " + cue);
            yield return new WaitForSeconds(wait);
        }

        _sequence = null;
    }

    private struct Sound
    {
        public string Kind;      // tone | noise | thud
        public float Freq;
        public float Freq2;      // 0이 아니면 반복마다 번갈아 씀
        public float Dur;
        public int Count;
        public float Gap;
        public float Bright;     // 잡음 밝기 0~1
        public Vector3 Pos;      // 카메라 기준 (x 오른쪽, y 위, z 앞)
        public float Gain;
    }

    private static Sound S(string kind, float freq, float dur, Vector3 pos, int count = 1, float gap = 0f,
                           float bright = 0.5f, float freq2 = 0f, float gain = 1f)
    {
        return new Sound { Kind = kind, Freq = freq, Freq2 = freq2, Dur = dur, Count = count, Gap = gap,
                           Bright = bright, Pos = pos, Gain = gain };
    }

    private static bool TryGetSound(string cue, out Sound s)
    {
        Vector3 front = new Vector3(1.5f, 0f, 6f);
        Vector3 back = new Vector3(-1.5f, 0f, -4f);
        Vector3 under = new Vector3(0.3f, -0.9f, 2f);
        switch (cue)
        {
            case "amb.base":            s = S("tone", 60f, 1.2f, new Vector3(0f, 1.2f, 3f), gain: 0.25f); return true;
            case "door.close.front":    s = S("thud", 180f, 0.35f, front); return true;
            case "door.close.back":     s = S("thud", 160f, 0.35f, back); return true;
            case "handle.back":         s = S("tone", 900f, 0.07f, back, 2, 0.12f, gain: 0.5f); return true;
            case "chalk.3":             s = S("noise", 0f, 0.14f, new Vector3(0f, 0.2f, 6f), 3, 0.25f, 0.95f); return true;
            case "eraser":              s = S("noise", 0f, 0.08f, new Vector3(1f, 0f, 3f), 4, 0.07f, 0.3f); return true;
            case "desk.hit":            s = S("thud", 220f, 0.25f, new Vector3(0.5f, -0.8f, 3f)); return true;
            case "board.scratch":       s = S("noise", 0f, 0.9f, new Vector3(0f, 0.5f, 5f), bright: 1f, gain: 0.7f); return true;
            case "chair.scrape":        s = S("noise", 0f, 0.5f, new Vector3(-1f, -1f, 4f), bright: 0.45f); return true;
            case "chair.scrape.phrase": s = S("noise", 0f, 0.25f, new Vector3(-1f, -1f, 4f), 3, 0.12f, 0.45f); return true;
            case "glass.break":         s = S("noise", 0f, 0.35f, new Vector3(0f, 0f, -2f), bright: 1f); return true;
            case "glass.touch":         s = S("tone", 3200f, 0.06f, new Vector3(0.5f, -0.4f, 3f), 2, 0.1f, gain: 0.4f); return true;
            case "glass.scrape":        s = S("noise", 0f, 0.4f, new Vector3(0.5f, -0.4f, 3f), bright: 0.9f, gain: 0.5f); return true;
            case "glass.touch.under":   s = S("tone", 3200f, 0.06f, under, 2, 0.1f, gain: 0.4f); return true;
            case "glass.scrape.under":  s = S("noise", 0f, 0.4f, under, bright: 0.9f, gain: 0.5f); return true;
            case "heavy.drop":          s = S("thud", 70f, 0.6f, new Vector3(0f, -1f, 8f)); return true;
            case "flush.inner":         s = S("noise", 0f, 3f, new Vector3(1.5f, 0f, 3f), bright: 0.2f, gain: 0.8f); return true;
            case "call.worker":         s = S("tone", 440f, 0.3f, new Vector3(-1.5f, 0f, 2f), 2, 0.05f, freq2: 520f, gain: 0.5f); return true;
            case "handle.after_flush":  s = S("tone", 900f, 0.07f, new Vector3(1.5f, 0f, 3f), 2, 0.12f, gain: 0.5f); return true;
            case "breath.closed_stall": s = S("noise", 0f, 1.4f, new Vector3(1.5f, 0f, 1.5f), bright: 0.35f, gain: 0.5f); return true;
            default: s = default(Sound); return false;
        }
    }

    /// <summary>소리를 재생하고 길이(초)를 돌려준다.</summary>
    private float PlaySound(string cue, Sound s)
    {
        AudioClip clip;
        if (!_clips.TryGetValue(cue, out clip))
        {
            clip = Synthesize(cue, s);
            _clips[cue] = clip;
        }

        Vector3 pos = _listener.TransformPoint(s.Pos);
        GameObject go = new GameObject("cue:" + cue);
        go.hideFlags = HideFlags.DontSave;
        go.transform.position = pos;
        AudioSource src = go.AddComponent<AudioSource>();
        src.clip = clip;
        src.volume = _volume;
        src.spatialBlend = 1f;
        src.rolloffMode = AudioRolloffMode.Linear;
        src.minDistance = 1f;
        src.maxDistance = 40f;
        src.Play();
        Destroy(go, clip.length + 0.1f);

        GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        marker.name = "cue-marker:" + cue;
        marker.hideFlags = HideFlags.DontSave;
        Destroy(marker.GetComponent<Collider>());
        marker.transform.position = pos;
        marker.transform.localScale = Vector3.one * 0.25f;
        marker.GetComponent<Renderer>().sharedMaterial = MarkerMaterial();
        Destroy(marker, clip.length + 0.3f);

        return clip.length;
    }

    private static AudioClip Synthesize(string name, Sound s)
    {
        const int rate = 44100;
        int count = Mathf.Max(1, s.Count);
        float total = count * s.Dur + (count - 1) * s.Gap;
        int n = Mathf.CeilToInt(total * rate);
        float[] data = new float[n];
        System.Random rng = new System.Random(name.GetHashCode());
        float lp = 0f;
        float alpha = Mathf.Lerp(0.03f, 0.9f, s.Bright);

        for (int k = 0; k < count; k++)
        {
            int start = Mathf.FloorToInt(k * (s.Dur + s.Gap) * rate);
            int len = Mathf.FloorToInt(s.Dur * rate);
            float freq = (s.Freq2 > 0f && k % 2 == 1) ? s.Freq2 : s.Freq;
            for (int i = 0; i < len && start + i < n; i++)
            {
                float t = i / (float)rate;
                float u = i / (float)len;
                float v;
                switch (s.Kind)
                {
                    case "thud":
                        float f = freq * Mathf.Lerp(1.6f, 0.7f, u);
                        v = Mathf.Sin(2f * Mathf.PI * f * t) * Mathf.Exp(-6f * u);
                        v += ((float)rng.NextDouble() * 2f - 1f) * 0.25f * Mathf.Exp(-40f * u);
                        break;
                    case "noise":
                        float white = (float)rng.NextDouble() * 2f - 1f;
                        lp += alpha * (white - lp);
                        float env = Mathf.Clamp01(u * 20f) * Mathf.Clamp01((1f - u) * 6f);
                        v = lp * env * (s.Bright < 0.3f ? 2.5f : 1f);
                        if (name == "breath.closed_stall") v *= Mathf.Sin(Mathf.PI * u);
                        break;
                    default:
                        v = Mathf.Sin(2f * Mathf.PI * freq * t) * Mathf.Clamp01(u * 50f) * Mathf.Clamp01((1f - u) * 8f);
                        break;
                }

                data[start + i] = Mathf.Clamp(v * s.Gain * 0.8f, -1f, 1f);
            }
        }

        AudioClip clip = AudioClip.Create("synth:" + name, n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private Material MarkerMaterial()
    {
        if (_markerMat == null)
        {
            _markerMat = MakeMaterial(new Color(1f, 0.15f, 0.1f), true);
        }

        return _markerMat;
    }

    // ─────────────────────────────── 배치 ───────────────────────────────

    /// <summary>소품 하나. 보이기/숨기기와 「새로 생겼을 때」 움직임을 가진다.</summary>
    private sealed class Prop
    {
        public GameObject Root;
        public Action<bool> Play;   // 켤 때. 인자: 애니메이션 여부
        public Action Off;          // 끌 때(수식어 큐의 방향 복원)

        public void SetVisible(bool visible, bool animate)
        {
            if (Root != null) Root.SetActive(visible);
            if (visible && Play != null) Play(animate);
            if (!visible && Off != null) Off();
        }
    }

    private void ApplyLayout(Band band, bool animate)
    {
        IReadOnlyList<string> cues = Cues(FearAxis.Layout, band);
        HashSet<string> want = new HashSet<string>(cues);

        foreach (KeyValuePair<string, Prop> pair in _props)
        {
            string id = pair.Key;
            if (IsAuditoryOnlyProp(id)) continue;
            bool on = want.Contains(id);
            bool isNew = on && !_shownLayout.Contains(id);
            if (on && !isNew) continue;           // 이미 보이는 중: 그대로
            pair.Value.SetVisible(on, animate && isNew);
        }

        _shownLayout.Clear();
        foreach (string id in want) _shownLayout.Add(id);
    }

    /// <summary>청각 큐로만 나타나는 소품(무음 개방 문). 배치 적용이 끄지 않도록 제외.</summary>
    private static bool IsAuditoryOnlyProp(string id)
    {
        return id == "door.open.silent";
    }

    private void ReplayLayout()
    {
        _shownLayout.Clear();
        _instant = true;                 // 되돌리기는 즉시, 다시 켜기는 움직임으로
        foreach (Prop p in _props.Values) p.SetVisible(false, false);
        _instant = false;
        ApplyLayout(_bands[(int)FearAxis.Layout], true);
    }

    private void BuildProps(SpaceId space)
    {
        if (_propRoot != null) Destroy(_propRoot);
        _props.Clear();
        StopAllRotations();
        _propRoot = new GameObject("__AnomalyProps (runtime)");
        _propRoot.hideFlags = HideFlags.DontSave;

        switch (SpaceAnomalyTableSO.Group(space))
        {
            case SpaceId.Corridor: BuildCorridor(); break;
            case SpaceId.Classroom_1_1: BuildClassroom(); break;
            case SpaceId.ScienceRoom: BuildScience(); break;
            case SpaceId.Toilet: BuildToilet(); break;
        }

        foreach (Prop p in _props.Values)
        {
            if (p.Root != null) p.Root.SetActive(false);
        }
    }

    private void BuildCorridor()
    {
        Color wood = new Color(0.45f, 0.3f, 0.18f);

        // H1 — 오른쪽 벽 지정 문 자동 개방
        Transform hinge1 = Door("door.auto_open", new Vector3(1.95f, 0f, -6f), wood, false);
        AddProp("door.auto_open", hinge1.parent.gameObject, a => Swing(hinge1, -80f, a ? 1f : 0f));

        // 청각 Band4 — 왼쪽 벽 무음 개방
        Transform hinge2 = Door("door.open.silent", new Vector3(-1.95f, 0f, -2f), new Color(0.35f, 0.35f, 0.4f), true);
        AddProp("door.open.silent", hinge2.parent.gameObject, a => Swing(hinge2, 80f, a ? 2.5f : 0f));

        // H4 — 중앙 상자, 1.5m 반경
        GameObject box = Group("box.center");
        Block(box.transform, new Vector3(0f, 0.4f, 0f), Vector3.one * 0.8f, new Color(0.6f, 0.5f, 0.3f));
        Ring(box.transform, 1.5f, new Color(1f, 0.85f, 0.2f));
        box.transform.position = new Vector3(0f, 0f, -9f);
        AddProp("box.center", box, null);

        // H5 — 천장 조각 낙하, 1.5m 반경
        GameObject debris = Group("debris.fall");
        Transform slab = Block(debris.transform, new Vector3(0f, 0.08f, 0f), new Vector3(1.1f, 0.15f, 0.9f), new Color(0.75f, 0.75f, 0.72f)).transform;
        Ring(debris.transform, 1.5f, new Color(1f, 0.5f, 0.2f));
        debris.transform.position = new Vector3(0.6f, 0f, -4f);
        AddProp("debris.fall", debris, a => Fall(slab, a));

        // H6 — 나무·풀 묶음, 2m 반경
        GameObject tree = Group("tree.grass");
        Block(tree.transform, new Vector3(0f, 1f, 0f), new Vector3(0.3f, 2f, 0.3f), new Color(0.35f, 0.22f, 0.12f), PrimitiveType.Cylinder, 0.5f);
        Block(tree.transform, new Vector3(0f, 2.3f, 0f), Vector3.one * 1.4f, new Color(0.15f, 0.4f, 0.15f), PrimitiveType.Sphere);
        Block(tree.transform, new Vector3(0f, 0.01f, 0f), new Vector3(4f, 0.01f, 4f), new Color(0.2f, 0.5f, 0.15f), PrimitiveType.Cylinder, 1f);
        Ring(tree.transform, 2f, new Color(0.3f, 1f, 0.3f));
        tree.transform.position = new Vector3(0f, 0f, 3f);
        AddProp("tree.grass", tree, a => Grow(tree.transform, a));
    }

    private readonly List<Transform> _desks = new List<Transform>();
    private readonly List<Transform> _chairs = new List<Transform>();

    private void BuildClassroom()
    {
        _desks.Clear();
        _chairs.Clear();
        GameObject all = Group("desks.normal");
        Color deskC = new Color(0.55f, 0.4f, 0.25f);
        Color chairC = new Color(0.3f, 0.35f, 0.5f);

        // 2열(x = -0.9, 0.9) × 4행(z = -12 ~ -3). 카메라에서 먼 쪽이 뒤쪽.
        for (int row = 0; row < 4; row++)
        {
            for (int col = 0; col < 2; col++)
            {
                Vector3 at = new Vector3(col == 0 ? -0.9f : 0.9f, 0f, -11f + row * 2.6f);
                GameObject seat = new GameObject("seat r" + row + " c" + col);
                seat.transform.SetParent(all.transform, false);
                seat.transform.localPosition = at;

                GameObject desk = Block(seat.transform, new Vector3(0f, 0.36f, 0f), new Vector3(0.65f, 0.72f, 0.45f), deskC);
                GameObject chairPivot = new GameObject("chair");
                chairPivot.transform.SetParent(seat.transform, false);
                chairPivot.transform.localPosition = new Vector3(0f, 0f, -0.55f);
                Block(chairPivot.transform, new Vector3(0f, 0.22f, 0f), new Vector3(0.4f, 0.44f, 0.4f), chairC);
                Block(chairPivot.transform, new Vector3(0f, 0.65f, -0.18f), new Vector3(0.4f, 0.45f, 0.05f), chairC);

                _desks.Add(seat.transform);
                _chairs.Add(chairPivot.transform);
            }
        }

        AddProp("desks.normal", all, null);

        // 뒤쪽(마지막 행) 오른쪽 좌석 한 묶음이 출입문(왼쪽 벽)을 향함
        Transform turnedSeat = _desks[7];
        AddModifier("desks.turned.one", on => Turn(turnedSeat, on ? -90f : 0f));

        // 같은 열(오른쪽 열) 나머지 의자 방향 변경
        AddModifier("chairs.turned.row", on =>
        {
            for (int i = 1; i < 7; i += 2) Turn(_chairs[i], on ? 90f : 0f);
        });

        // 다른 열(왼쪽 열) 의자까지 변경
        AddModifier("chairs.turned.others", on =>
        {
            for (int i = 0; i < 8; i += 2) Turn(_chairs[i], on ? -120f : 0f);
        });
    }

    private void BuildScience()
    {
        GameObject lab = Group("lab.normal");
        Color top = new Color(0.2f, 0.2f, 0.22f);
        for (int i = 0; i < 2; i++)
        {
            Vector3 at = new Vector3(0f, 0f, -10f + i * 4.5f);
            GameObject table = Block(lab.transform, at + new Vector3(0f, 0.45f, 0f), new Vector3(2.2f, 0.9f, 1f), top);
            table.name = "lab table " + (i + 1);
            for (int b = 0; b < 3; b++)
            {
                Block(lab.transform, at + new Vector3(-0.6f + b * 0.6f, 1.02f, 0f), new Vector3(0.12f, 0.12f, 0.12f),
                      new Color(0.7f, 0.9f, 1f), PrimitiveType.Cylinder, 1f);
            }
        }

        AddProp("lab.normal", lab, null);
    }

    private void BuildToilet()
    {
        GameObject stalls = Group("stalls.base");
        Color wall = new Color(0.6f, 0.7f, 0.72f);
        Color door = new Color(0.4f, 0.55f, 0.6f);
        Transform[] hinges = new Transform[4];

        // 오른쪽 벽에 칸 4개(B1 입구 쪽 ~ B4 안쪽). 문은 복도 쪽(x=0.6)에 있음.
        for (int i = 0; i < 4; i++)
        {
            float z = -7f + i * 1.8f;
            Block(stalls.transform, new Vector3(1.3f, 1f, z - 0.8f), new Vector3(1.3f, 2f, 0.05f), wall);
            GameObject pivot = new GameObject("B" + (i + 1) + " hinge");
            pivot.transform.SetParent(stalls.transform, false);
            pivot.transform.localPosition = new Vector3(0.6f, 0f, z - 0.75f);
            Block(pivot.transform, new Vector3(0f, 1f, 0.8f), new Vector3(0.05f, 1.9f, 1.55f), door);
            hinges[i] = pivot.transform;
        }

        Block(stalls.transform, new Vector3(1.3f, 1f, -7f + 3 * 1.8f + 0.8f), new Vector3(1.3f, 2f, 0.05f), wall);
        AddProp("stalls.base", stalls, null);

        // T1: 입구 쪽 개폐형 칸(B1) 자동 개방 1회
        AddModifier("stall.entry.auto_open", on =>
        {
            if (!on) return;
            Coroutine running;
            if (_rotations.TryGetValue(hinges[0], out running) && running != null) StopCoroutine(running);
            _rotations[hinges[0]] = StartCoroutine(OpenThenClose(hinges[0]));
        });
        // 안쪽 칸(B4) 이미 열린 상태
        AddModifier("stall.inner.open", on => Turn(hinges[3], on ? -75f : 0f));
        // 개폐형 점검칸 두 곳(B1·B2) 모두 열린 상태
        AddModifier("stall.entry.open", on =>
        {
            Turn(hinges[0], on ? -75f : 0f);
            Turn(hinges[1], on ? -75f : 0f);
        });
    }

    // ─────────────────────────────── 소품 도우미 ───────────────────────────────

    private void AddProp(string id, GameObject root, Action<bool> play)
    {
        root.transform.SetParent(_propRoot.transform, true);
        _props[id] = new Prop { Root = root, Play = play };
    }

    /// <summary>오브젝트를 새로 만들지 않고 기존 소품의 방향만 바꾸는 큐. 끄면 원래 방향으로 돌아간다.</summary>
    private void AddModifier(string id, Action<bool> set)
    {
        GameObject flag = new GameObject("modifier:" + id);
        flag.transform.SetParent(_propRoot.transform, false);
        _props[id] = new Prop { Root = flag, Play = a => set(true), Off = () => set(false) };
    }

    private GameObject Group(string name)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(_propRoot.transform, false);
        return go;
    }

    private GameObject Block(Transform parent, Vector3 localPos, Vector3 size, Color color,
                             PrimitiveType type = PrimitiveType.Cube, float yScale = -1f)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        // 원기둥은 높이 2가 기본이라 절반으로 맞춘다.
        if (type == PrimitiveType.Cylinder && yScale > 0f)
        {
            size.y *= yScale;
        }
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = MakeMaterial(color, false);
        return go;
    }

    /// <summary>경첩 피벗을 만들고 반환한다. 부모는 소품 루트.</summary>
    private Transform Door(string name, Vector3 wallPos, Color color, bool leftWall)
    {
        GameObject root = Group(name);
        root.transform.position = wallPos;
        // 문틀(어두운 판)
        Block(root.transform, new Vector3(leftWall ? 0.03f : -0.03f, 1.05f, 0.5f), new Vector3(0.02f, 2.1f, 1.0f), new Color(0.08f, 0.08f, 0.08f));
        GameObject pivot = new GameObject("hinge");
        pivot.transform.SetParent(root.transform, false);
        pivot.transform.localPosition = Vector3.zero;
        Block(pivot.transform, new Vector3(leftWall ? 0.05f : -0.05f, 1.05f, 0.5f), new Vector3(0.05f, 2.05f, 0.95f), color);
        return pivot.transform;
    }

    private void Ring(Transform parent, float radius, Color color)
    {
        GameObject go = new GameObject("radius " + radius + "m");
        go.transform.SetParent(parent, false);
        LineRenderer lr = go.AddComponent<LineRenderer>();
        const int seg = 48;
        lr.useWorldSpace = false;
        lr.loop = true;
        lr.positionCount = seg;
        lr.widthMultiplier = 0.04f;
        lr.sharedMaterial = MakeMaterial(color, true);
        for (int i = 0; i < seg; i++)
        {
            float a = i * Mathf.PI * 2f / seg;
            lr.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, 0.03f, Mathf.Sin(a) * radius));
        }
    }

    private readonly List<Material> _materials = new List<Material>();

    private Material MakeMaterial(Color color, bool unlit)
    {
        Shader shader = Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        Material m = new Material(shader);
        m.hideFlags = HideFlags.DontSave;
        m.color = color;
        _materials.Add(m);
        return m;
    }

    private void OnDestroy()
    {
        for (int i = 0; i < _materials.Count; i++)
        {
            if (_materials[i] != null) Destroy(_materials[i]);
        }

        foreach (AudioClip c in _clips.Values)
        {
            if (c != null) Destroy(c);
        }
    }

    // ─────────────────────────────── 움직임 ───────────────────────────────

    private bool _instant;
    private readonly Dictionary<Transform, Coroutine> _rotations = new Dictionary<Transform, Coroutine>();

    /// <summary>문을 닫힌 상태에서 angle까지 연다. seconds가 0이면 즉시.</summary>
    private void Swing(Transform hinge, float angle, float seconds)
    {
        if (seconds > 0f) hinge.localRotation = Quaternion.identity;
        Rotate(hinge, angle, seconds);
    }

    private void Turn(Transform t, float angle)
    {
        Rotate(t, angle, _instant ? 0f : 0.8f);
    }

    /// <summary>같은 물체에 돌던 회전이 있으면 멈추고 새로 돈다.</summary>
    private void Rotate(Transform t, float angle, float seconds)
    {
        if (t == null) return;
        Coroutine running;
        if (_rotations.TryGetValue(t, out running) && running != null) StopCoroutine(running);
        _rotations[t] = StartCoroutine(RotateTo(t, Quaternion.Euler(0f, angle, 0f), seconds));
    }

    private void StopAllRotations()
    {
        foreach (Coroutine c in _rotations.Values)
        {
            if (c != null) StopCoroutine(c);
        }

        _rotations.Clear();
    }

    private static IEnumerator RotateTo(Transform t, Quaternion target, float seconds)
    {
        if (t == null) yield break;
        Quaternion from = t.localRotation;
        for (float e = 0f; e < seconds && t != null; e += Time.deltaTime)
        {
            t.localRotation = Quaternion.Slerp(from, target, Mathf.SmoothStep(0f, 1f, e / seconds));
            yield return null;
        }

        if (t != null) t.localRotation = target;
    }

    private IEnumerator OpenThenClose(Transform hinge)
    {
        if (hinge == null) yield break;
        hinge.localRotation = Quaternion.identity;
        yield return RotateTo(hinge, Quaternion.Euler(0f, -75f, 0f), 1f);
        yield return new WaitForSeconds(1.5f);
        yield return RotateTo(hinge, Quaternion.identity, 1f);
    }

    private void Fall(Transform slab, bool animate)
    {
        if (!animate)
        {
            slab.localPosition = new Vector3(0f, 0.08f, 0f);
            return;
        }

        StartCoroutine(FallRoutine(slab));
    }

    private IEnumerator FallRoutine(Transform slab)
    {
        const float top = 3.05f;
        const float bottom = 0.08f;
        slab.localPosition = new Vector3(0f, top, 0f);
        yield return new WaitForSeconds(0.3f);
        for (float e = 0f; e < 0.55f && slab != null; e += Time.deltaTime)
        {
            float u = e / 0.55f;
            slab.localPosition = new Vector3(0f, Mathf.Lerp(top, bottom, u * u), 0f);
            yield return null;
        }

        if (slab == null) yield break;
        slab.localPosition = new Vector3(0f, bottom, 0f);
        Sound s = S("thud", 90f, 0.4f, Vector3.zero);
        PlayAt("debris.impact", s, slab.position);
    }

    private void PlayAt(string cue, Sound s, Vector3 world)
    {
        s.Pos = _listener.InverseTransformPoint(world);
        PlaySound(cue, s);
    }

    private void Grow(Transform t, bool animate)
    {
        if (!animate)
        {
            t.localScale = Vector3.one;
            return;
        }

        StartCoroutine(GrowRoutine(t));
    }

    private static IEnumerator GrowRoutine(Transform t)
    {
        for (float e = 0f; e < 1.2f && t != null; e += Time.deltaTime)
        {
            t.localScale = Vector3.one * Mathf.SmoothStep(0.05f, 1f, e / 1.2f);
            yield return null;
        }

        if (t != null) t.localScale = Vector3.one;
    }

    // ─────────────────────────────── 표 읽기 ───────────────────────────────

    private IReadOnlyList<string> Cues(FearAxis axis, Band band)
    {
        return _table != null ? _table.CuesFor(_space, axis, band) : (IReadOnlyList<string>)new string[0];
    }

    private string Label(FearAxis axis, Band band)
    {
        return _table != null ? _table.LabelFor(_space, axis, band) : "(표 없음)";
    }

    private static string SpaceName(SpaceId s)
    {
        switch (s)
        {
            case SpaceId.Corridor: return "복도";
            case SpaceId.Classroom_1_1: return "교실 1-1";
            case SpaceId.Classroom_1_3: return "교실 1-3";
            case SpaceId.ScienceRoom: return "과학실";
            case SpaceId.Toilet: return "화장실";
            default: return s.ToString();
        }
    }

    private void Note(string line)
    {
        Debug.Log("[AnomalyRig] " + line, this);
        _log.Add(line);
        while (_log.Count > 7) _log.RemoveAt(0);
    }

    // ─────────────────────────────── 화면 패널 ───────────────────────────────

    private GUIStyle _wrap;
    private GUIStyle _small;
    private bool _collapsed;
    private bool _showLog = true;
    private Vector2 _logScroll;

    private const float PanelWidth = 420f;
    private const float PanelHeight = 380f;   // 펼친 상태 기준 높이(설계값)

    private void Update()
    {
        // F1: 패널 숨기기/보이기 (게임 뷰에 포커스가 있을 때)
        if (Input.GetKeyDown(KeyCode.F1)) _showOverlay = !_showOverlay;
    }

    private void OnGUI()
    {
        if (!_showOverlay) return;
        if (_wrap == null)
        {
            _wrap = new GUIStyle(GUI.skin.label) { wordWrap = true, fontSize = 13 };
            _small = new GUIStyle(_wrap) { fontSize = 12 };
        }

        // 게임 뷰가 작으면 패널 전체를 줄인다. 너비는 화면의 절반, 높이는 화면 안에 들어가게.
        float scale = Mathf.Min(Screen.width * 0.5f / PanelWidth, (Screen.height - 20f) / PanelHeight);
        scale = Mathf.Clamp(scale, 0.4f, 1.5f);
        Matrix4x4 saved = GUI.matrix;
        GUIUtility.ScaleAroundPivot(new Vector2(scale, scale), Vector2.zero);

        float maxH = (Screen.height - 20f) / scale;
        GUILayout.BeginArea(new Rect(10f / scale, 10f / scale, PanelWidth, maxH));
        GUILayout.BeginVertical(GUI.skin.box);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("◀", GUILayout.Width(32))) Step(-1);
        GUILayout.Label(SpaceName(_space), _wrap, GUILayout.Width(80));
        if (GUILayout.Button("▶", GUILayout.Width(32))) Step(1);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button(_collapsed ? "펼치기" : "접기", GUILayout.Width(56))) _collapsed = !_collapsed;
        if (GUILayout.Button("숨김(F1)", GUILayout.Width(70))) _showOverlay = false;
        GUILayout.EndHorizontal();

        for (int i = 0; i < 3; i++)
        {
            FearAxis axis = (FearAxis)i;
            int value = _reader != null ? _reader.GetValue(axis) : 0;
            GUILayout.BeginHorizontal();
            GUILayout.Label(AxisNames[i] + " " + value + " · B" + (int)_bands[i], _wrap, GUILayout.Width(100));
#if UNITY_EDITOR || NIGHTDUTY_DEBUG
            DebugAxisDriver driver = _reader as DebugAxisDriver;
            if (driver != null)
            {
                if (GUILayout.Button("−", GUILayout.Width(26))) driver.SetAxis(axis, PrevLower(value));
                if (GUILayout.Button("+", GUILayout.Width(26))) driver.SetAxis(axis, NextLower(value));
            }
#endif
            if (!_collapsed) GUILayout.Label(Label(axis, _bands[i]), _small, GUILayout.Width(PanelWidth - 190f));
            GUILayout.EndHorizontal();
        }

        if (!_collapsed)
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("청각 다시 재생")) PlayAuditory(_bands[(int)FearAxis.Auditory]);
            if (GUILayout.Button("배치 사건 다시")) ReplayLayout();
            if (GUILayout.Button(_showLog ? "기록 ▲" : "기록 ▼", GUILayout.Width(64))) _showLog = !_showLog;
            GUILayout.EndHorizontal();

            if (_showLog)
            {
                _logScroll = GUILayout.BeginScrollView(_logScroll, GUILayout.Height(110));
                for (int i = _log.Count - 1; i >= 0; i--)   // 최신이 위
                {
                    GUILayout.Label(_log[i], _small);
                }
                GUILayout.EndScrollView();
            }
        }

        GUILayout.EndVertical();
        GUILayout.EndArea();
        GUI.matrix = saved;
    }

    private void Step(int dir)
    {
        int index = Array.IndexOf(Cycle, SpaceAnomalyTableSO.Group(_space));
        if (index < 0) index = 0;
        index = (index + dir + Cycle.Length) % Cycle.Length;
        SwitchSpace(Cycle[index]);
    }

    /// <summary>다음 구간의 하한(한 구간 올림).</summary>
    private static int NextLower(int value)
    {
        int b = (int)Bands.Of(value);
        return b >= 4 ? Bands.UpperBound(Band.Band4) : Bands.LowerBound((Band)(b + 1));
    }

    /// <summary>한 구간 내림(이전 구간의 하한).</summary>
    private static int PrevLower(int value)
    {
        int b = (int)Bands.Of(value);
        return b <= 0 ? 0 : Bands.LowerBound((Band)(b - 1));
    }
}

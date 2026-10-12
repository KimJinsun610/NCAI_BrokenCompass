using NightDuty;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 인체 나무 — 현관 바닥에 박혀 배치 축만큼 자라는 나무. 103차(민: 「인체 나무는 다시 땅에 박혀 있는 걸로 · 위치도 중앙에 · 모델은 New_eggman」):
/// 천장 매달기를 그만두고 현관 한가운데(<see cref="FloorSpot"/>) 바닥에 뿌리를 두며, 대역은 New_eggman 나무(<c>Resources/StandIns/mob.tree</c>, 사람 나무 조우와 같은 모델)를 쓴다.
/// 사람 나무 대역의 「오래 바라보면 한 번 터지는 연출」(HorrorGazeTrigger·HorrorEvent)은 떼고 「바라보는 동안 화면 물듦」(HorrorGazeHold)만 남긴다.
/// 103차(민: 「제대로 응시하면 1초마다 축이 랜덤하게 조금씩 올라가게 · 화면 울렁임과 함께」): 시선 <see cref="GazeDegrees"/>° 안·<see cref="GazeRange"/>m 안·가림 없이
/// 바라보는 동안 화면이 울렁이고(<see cref="DirectionScreenFx.Kind.Wobble"/>), 1초 이어질 때마다 감각 축 하나(무작위)가 조금 오른다(<see cref="NightRun.AddLobbyTreeStare"/>).
/// 103차(민: 「3일차부터 어렴풋이 등장할 수 있도록 배치 축 조건 · 시작 크기 1.5m 정도」): <see cref="AppearDay"/>일차부터, 배치 축이 <see cref="AppearLayout"/>(구간 1) 이상일 때만 선다 —
/// 그 문턱에서 <see cref="MinLength"/>m로 나타나 100에서 <see cref="MaxLength"/>m. 밤 도중에 문턱을 넘으면 그 자리가 화면에 보이지 않을 때 선다.
/// 103차(사용자: 「어색한 조명 없애기 — 어둠에선 어둡게, 손전등을 비추면 라이팅 적용」): 나무 조명·스스로 빛남을 떼고 모델 재질의 발광도 끈다(<see cref="StandInFactory.KillGlow"/>).
/// 103차(민: 「인체 나무 근처에 가면 천천히 인체 나무 방향으로 끌려가도록」): <see cref="PullRadius"/>m 안이면 플레이어가 나무 쪽으로 천천히 끌린다(가까울수록 세게, 최대 <see cref="PullSpeed"/>m/s) —
/// 걸어서 벗어날 수 있다(걷기 1m/s). 줄기 앞 <see cref="PullStop"/>m에서 멈춘다.
/// 104차(민: 「현관에 있는 나무와 사람 나무 조우를 합쳐줘 · 현관에서 알아볼 때 삐걱+웅얼거림 · 복도로 나왔을 때 바라보면 화면 울렁임+감각 축 오름 — 붙잡힘은 없애라」):
/// 나무는 하나다. 평소엔 현관에 서 있다가, 그 밤 사람 나무 조우(<see cref="ProgramCatalog.PeopleTree"/>)가 나오면 <b>안 보는 사이</b> 현관을 떠나 복도(<see cref="TreeSpots"/>가 고른 자리)에 선다(<see cref="ComeOut"/>).
/// 복도에 있는 동안만 H1 「미분류 물체」 기준점이 붙고(1.2m 안 접근 = 위반), 바라보면 현관과 같이 화면이 울렁이고 감각 축이 오른다(옛: 오래 보면 배치 축이 올라 붙잡힘 — 뗐다, <see cref="FixedMobStare.Punishes"/>).
/// 조우 창이 끝나면 다시 안 보는 사이 현관으로 돌아간다(<see cref="GoHome"/>, 붙잡힘·재시작이면 바로). 끌림은 복도에선 H1 밖 <see cref="CorridorPullStop"/>m에서 멈춘다.
/// 현관 나무가 아직 서지 않는 밤(2일차, 3일차 배치 낮음)에 조우가 먼저 오면 그것이 첫 등장 — 그 뒤로는 이번 판 내내 현관에 선다(<see cref="SettledDay"/>).
/// 현관에서 처음 알아볼 때(시선 <see cref="NoticeDegrees"/>° · <see cref="NoticeRange"/>m 안, 가림 없이) 사람 나무 대면 소리(가지 삐걱 — 104차 사용자: 웅얼거림 「괴성」은 시끄러워 뺐다)를 한 번 낸다. 플레이어가 멀리(<see cref="LeaveSoundDistance"/>m+) 있을 때 현관을 떠나면 현관에서 먼 가지 소리.
/// 옛 설명(94차, 천장):
/// <list type="bullet">
/// <item>91차 B(날마다 자람) → 94차(민: 「에그맨 트리가 화분에 나오는 건 폐기 · 경비실 옆 정문 앞 부분에, 천장에 매달려서 점점 자라나게 ·
/// 배치 축에 따라서 성장하는 느낌」): 화분 묘목은 없앴다. 정문(<c>DoorMain</c>) 안쪽 현관, 경비실 쪽 천장에 거꾸로 매달려
/// 그 밤 <b>배치 축</b> 값만큼 아래로 자란다 — 0이면 <see cref="MinLength"/>m 봉오리, 100이면 <see cref="MaxLength"/>m(바닥 가까이).
/// 축이 오르면 천천히(<see cref="GrowSpeed"/> m/초) 자라고, 재시작으로 축이 내려가면 그만큼 움츠러든다.</item>
/// <item>밤마다 선다(근무 밤이면). (옛: 그 밤 편성에 사람 나무 조우가 있으면 세우지 않았다 — 104차에 둘을 합쳤다.)</item>
/// <item>콜라이더는 끈다(길을 막지 않게). 김진선님 「바라보면 화면 물듦」은 그대로. (옛: 손전등 없이도 윤곽만 보일 만큼 스스로 빛났다 — 103차에 뗌.)</item>
/// </list>
/// 판정·축과 무관한 무대 장치(축을 읽기만 한다). 씬·김진선님 프리팹은 고치지 않는다. 근무 씬에 자동으로 선다.
/// </summary>
[DisallowMultipleComponent]
public sealed class HumanTreeGrowth : MonoBehaviour
{
    /// <summary>대역 경로(Resources) — 103차: New_eggman 나무(사람 나무 조우 대역과 같은 모델).</summary>
    public const string PrefabPath = "StandIns/mob.tree";

    /// <summary>103차: 심는 바닥 자리(수평 위치, PlayScene 실측) — 정문 안쪽 현관(x 25.3~29.8, z 44~48)의 한가운데. 높이는 아래로 쏴서 바닥에 맞춘다.</summary>
    public static readonly Vector3 FloorSpot = new Vector3(27.55f, 2.5f, 46.0f);

    /// <summary>나무가 바라보는 쪽(현관에서 중앙 복도 쪽 — 복도에서 현관으로 들어서는 사람을 본다).</summary>
    public static readonly Vector3 Facing = new Vector3(27.55f, 2.5f, 40f);

    /// <summary>103차: 「제대로 응시」 시선 각(°) — 나무 몸통이 화면 가운데 근처.</summary>
    public const float GazeDegrees = 11f;

    /// <summary>103차: 응시로 치는 거리(m).</summary>
    public const float GazeRange = 16f;

    /// <summary>103차: 응시하는 동안 화면 울렁임 세기.</summary>
    public const float GazeWobble = 0.55f;

    private const string FxKey = "tree.lobby.gaze";

    /// <summary>103차: 그 밤·그 축 값에서 서야 하는지(일차·배치 축 조건).</summary>
    public static bool ShouldStand(int day, int layout)
    {
        return day >= AppearDay && layout >= AppearLayout;
    }

    /// <summary>103차: 이 거리(m, 수평) 안이면 나무 쪽으로 끌린다.</summary>
    public const float PullRadius = 5.5f;

    /// <summary>103차: 가장 세게 끌릴 때(줄기 가까이) 빠르기(m/s) — 걷기(1m/s)로 벗어날 수 있게.</summary>
    public const float PullSpeed = 0.45f;

    /// <summary>103차: 줄기에서 이 거리(m, 수평)까지 끌려오면 멈춘다.</summary>
    public const float PullStop = 0.9f;

    /// <summary>104차: 복도에 나와 있을 때 끌림이 멈추는 거리(m) — H1 위반(1.2m) 밖. 끌려서 위반하지 않는다.</summary>
    public const float CorridorPullStop = 1.5f;

    /// <summary>104차: 현관에서 「알아봄」으로 치는 시선 각(°)·거리(m).</summary>
    public const float NoticeDegrees = 20f;
    public const float NoticeRange = 14f;

    /// <summary>104차: 현관을 떠날 때 플레이어가 이만큼(m) 멀면 현관에서 먼 가지 소리.</summary>
    public const float LeaveSoundDistance = 10f;

    /// <summary>104차: 현관을 떠날 때 소리(가지 삐걱만).</summary>
    public const string LeaveSound = "tree.leave";

    /// <summary>104차: 나무가 있는 곳.</summary>
    public enum Place
    {
        Lobby,
        Corridor,
    }

    /// <summary>104차: 지금 있는 곳(검수용).</summary>
    public Place Where
    {
        get { return _where; }
    }

    /// <summary>104차: 조우가 끝나 현관으로 돌아갈 차례인지(안 보일 때 옮긴다, 검수용).</summary>
    public bool Returning
    {
        get { return _returning; }
    }

    /// <summary>104차: 사람 나무 조우를 처음 겪은 일차(0 = 아직). 이 일차부터는 일차·축 조건과 상관없이 현관에 선다. 새 판·그 밤 재시작이면 지운다.</summary>
    public int SettledDay
    {
        get { return _settledDay; }
    }

    /// <summary>104차: 이번 현관 자리에서 알아봄 소리를 냈는지(검수용).</summary>
    public bool Noticed
    {
        get { return _noticed; }
    }

    /// <summary>104차: 그 나무(뿌리)가 이 컴포넌트의 것인지 — 조우 정리가 지우지 않고 현관으로 돌려보낸다.</summary>
    public static bool Owns(GameObject go)
    {
        return go != null && Active != null && Active._pivot == go;
    }

    private Place _where = Place.Lobby;
    private bool _returning;
    private int _settledDay;
    private bool _noticed;
    private float _maxLength = MaxLength;
    private GameObject _model;

    /// <summary>103차: 줄기 충돌체 반지름(m) — 현관 한가운데라 걸어서 뚫고 지나가지 않게(현관 폭 4.5m, 양옆으로 돌아간다).</summary>
    public const float TrunkRadius = 0.32f;

    /// <summary>103차: 이 일차부터 선다.</summary>
    public const int AppearDay = 3;

    /// <summary>103차: 배치 축이 이 값(구간 1 문턱) 이상일 때만 선다.</summary>
    public const int AppearLayout = 25;

    /// <summary>나타날 때(배치 축 <see cref="AppearLayout"/>) 키(m). 103차: 0.5 → 1.5.</summary>
    public const float MinLength = 1.5f;

    /// <summary>배치 축 100일 때 키(m) — 천장(현관 4m) 가까이까지.</summary>
    public const float MaxLength = 3.5f;

    /// <summary>
    /// 104차 D(배치 연출 1단계 — 「돌아보니 커졌다」): 자라는 것은 <b>안 보는 사이 한 번에</b> — 보고 있는 동안은 자라지 않고, 시야에서 벗어나면 그 축 값의 키로 바로 맞춘다.
    /// (옛: 보는 앞에서 초당 0.06m로 천천히 자라 알아채기 어려웠다.)
    /// </summary>
    public const bool GrowWhileUnseen = true;

    /// <summary>104차 A: 배치 구간이 이 이상인 밤에 나무 얼굴들이 고개를 돌려 플레이어를 본다(<see cref="TreeHeads"/>). 4일차 하한(구간 2)부터 누구나.</summary>
    public const int HeadsBand = 2;

    /// <summary>움츠러드는 빠르기(m/초) — 재시작으로 축이 내려갔을 때.</summary>
    public const float ShrinkSpeed = 0.5f;

    /// <summary>배치 축 값(0~100)에 맞는 길이(m).</summary>
    public static float LengthFor(int layout)
    {
        return Mathf.Lerp(MinLength, MaxLength, Mathf.InverseLerp(AppearLayout, 100f, layout));
    }

    /// <summary>지금 근무 씬의 것. 없으면 null.</summary>
    public static HumanTreeGrowth Active { get; private set; }

    /// <summary>심은 나무의 뿌리(바닥 점, 비례로 키운다). 없으면 null(검수용).</summary>
    public GameObject Tree
    {
        get { return _pivot; }
    }

    /// <summary>지금 길이(m, 검수용).</summary>
    public float Length
    {
        get { return _length; }
    }

    private GameObject _pivot;
    private CapsuleCollider _trunk;
    private TreeHeads _heads;

    /// <summary>104차 A: 나무 얼굴 고개 돌림(검수용, 없으면 null).</summary>
    public TreeHeads Heads
    {
        get { return _heads; }
    }

    /// <summary>104차 D: 나무가 지금 화면에 보이는지(가림 포함).</summary>
    private bool TreeSeen()
    {
        Camera cam = Camera.main;
        return cam != null && _pivot != null && UnseenDespawn.VisibleTo(_pivot, cam);
    }
    private float _gaze;
    private bool _gazing;
    private DirectionScreenFx _fx;
    private Transform _player;
    private float _unitHeight = 1f;
    private float _length;
    private InspectionPlan _plan;

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
        if (!FlowAutoInstall.IsDutyScene(scene) || FlowAutoInstall.Exists<HumanTreeGrowth>(scene)) return;
        FlowAutoInstall.CreateHost<HumanTreeGrowth>(scene, "HumanTreeGrowth (auto)");
    }

    private void OnEnable()
    {
        Active = this;
        EventBus.NightRestarted += OnRestarted;
    }

    private void OnDisable()
    {
        EventBus.NightRestarted -= OnRestarted;
        Clear();
        if (Active == this) Active = null;
    }

    /// <summary>104차: 재시작 — 다음 프레임에 그 밤에 맞게 다시 세운다(그 밤에 처음 겪은 조우도 없던 일로).</summary>
    private void OnRestarted(RestartResult result)
    {
        _plan = null;
        _forceRebuild = true;
    }

    private bool _forceRebuild;

    private void Update()
    {
        if (!NightRun.IsNightActive || NightRun.Axes == null)
        {
            return;
        }

        InspectionPlan plan = NightRun.Inspections != null ? NightRun.Inspections.Plan : null;
        if (!ReferenceEquals(plan, _plan) || _forceRebuild)
        {
            _forceRebuild = false;
            _plan = plan;
            Rebuild();
        }

        int layout = LayoutNow();
        if (_where == Place.Corridor)
        {
            // 104차: 복도에 나와 있다 — 조우가 끝났으면 나무도 현관 자리도 안 보일 때 돌아간다.
            if (_returning && !TreeSeen() && !SpotSeen()) ReturnHome(layout);
            else
            {
                Tick(layout);
                return;
            }
        }

        if (!StandsInLobby(layout))
        {
            if (_pivot != null) Clear();   // 재시작으로 축이 문턱 아래로 — 거둔다
            return;
        }

        if (_pivot == null)
        {
            if (SpotSeen()) return;   // 밤 도중 문턱을 넘었다 — 그 자리가 안 보일 때 선다
            Plant(layout);
            if (_pivot == null) return;
        }

        Tick(layout);
        NoticeTick();
    }

    /// <summary>104차: 현관에 서는지 — 일차·배치 축 조건, 또는 이번 판에 이미 사람 나무 조우를 겪었다.</summary>
    private bool StandsInLobby(int layout)
    {
        return ShouldStand(NightRun.Day, layout) || (_settledDay > 0 && NightRun.Day >= _settledDay);
    }

    private void Tick(int layout)
    {
        float want = Mathf.Min(LengthFor(layout), _maxLength);
        if (want > _length)
        {
            if (!TreeSeen()) _length = want;   // 104차 D: 안 보는 사이 한 번에
        }
        else
        {
            _length = Mathf.MoveTowards(_length, want, ShrinkSpeed * Time.deltaTime);
        }

        ApplyLength();
        if (_heads != null)
        {
            IFearAxisReader shown = NightRun.Shown;
            bool band = shown != null && (int)shown.GetBand(FearAxis.Layout) >= HeadsBand;
            _heads.Watching = !NightRun.IsCaptured && (band || _where == Place.Corridor);   // 104차: 복도에 나오면 언제나 본다
        }

        GazeTick(Time.deltaTime);
        PullTick(Time.deltaTime);
    }

    /// <summary>104차: 현관에서 처음 알아볼 때 — 사람 나무 대면 소리(가지 삐걱).</summary>
    private void NoticeTick()
    {
        if (_noticed || _pivot == null || NightRun.IsCaptured || _where != Place.Lobby) return;
        if (!LookedAt(NoticeDegrees, NoticeRange)) return;
        _noticed = true;
        Vector3 at = _pivot.transform.position + Vector3.up * Mathf.Max(1f, _length * 0.6f);
        DirectionStage.PlaySound(ProgramCatalog.PeopleTree + ".confront", at, 5f, 0.85f);
        if (DirectionStage.Verbose) Debug.Log("[HumanTreeGrowth] 현관 나무를 알아봄 — 가지 삐걱");
    }

    /// <summary>
    /// 104차: 사람 나무 조우 — 현관(또는 아직 없으면 새로 심어) 나무를 복도 자리로 옮긴다. 키는 그대로(천장·벽에 맞춘 <paramref name="maxLength"/>까지),
    /// H1 기준점(<paramref name="anchorId"/>)을 붙인다. 부르는 쪽(DirectionStage)이 나무도 그 자리도 안 보일 때 부른다. 옮긴 나무 뿌리를 돌려준다.
    /// </summary>
    public GameObject ComeOut(Vector3 floor, Quaternion rotation, float maxLength, string anchorId)
    {
        bool fromLobby = _where == Place.Lobby && _pivot != null;
        if (_pivot == null)
        {
            _pivot = Plant();
            if (_pivot == null) return null;
            _length = LengthFor(LayoutNow());
        }

        if (fromLobby) LeaveTick();
        _where = Place.Corridor;
        _returning = false;
        _gaze = 0f;
        SetWobble(false);
        _maxLength = Mathf.Max(0.5f, maxLength);
        _length = Mathf.Min(_length, _maxLength);
        _pivot.transform.SetPositionAndRotation(floor, rotation);
        ApplyLength();
        if (!string.IsNullOrEmpty(anchorId))
        {
            // 기준점은 뿌리에 — 줄기 충돌체(뿌리)가 그 표식의 것이어야 응시(GazeProbe)가 이 나무를 「본 것」으로 센다(역설 H1 「눈으로만 확인」).
            // 근접(H1 1.2m)은 수평 거리만 보므로 뿌리 = 줄기 가운데.
            JudgeTarget mark = _pivot.GetComponent<JudgeTarget>();
            if (mark == null) mark = _pivot.AddComponent<JudgeTarget>();
            mark.SetIds(anchorId);
        }
        Physics.SyncTransforms();
        if (DirectionStage.Verbose) Debug.Log("[HumanTreeGrowth] 사람 나무 조우 — 복도로 나옴 " + floor.ToString("F1") + " 키 " + _length.ToString("0.00") + "m");
        return _pivot;
    }

    /// <summary>
    /// 104차: 조우가 끝났다 — H1 기준점을 떼고 나무도 현관 자리도 안 보일 때 현관으로 돌아간다(붙잡힘·재시작은 <see cref="Rebuild"/>가 그 밤에 맞게 다시 세운다).
    /// <paramref name="met"/>(대면까지 갔다)면 이 밤부터 이번 판 내내 현관에 선다 — 못 보고 거둔 것(오래 못 봄)은 치지 않는다.
    /// </summary>
    public void GoHome(bool met)
    {
        if (_where != Place.Corridor) return;
        if (met && (_settledDay <= 0 || NightRun.Day < _settledDay)) _settledDay = NightRun.Day;
        if (_pivot != null)
        {
            foreach (JudgeTarget t in _pivot.GetComponentsInChildren<JudgeTarget>(true))
            {
                if (t != null) t.SetIds();   // 표식은 비워 둔다(다시 나오면 ComeOut이 채운다)
            }
        }

        _returning = true;
    }

    private void ReturnHome(int layout)
    {
        _where = Place.Lobby;
        _returning = false;
        _noticed = false;
        _maxLength = MaxLength;
        _gaze = 0f;
        SetWobble(false);
        if (_pivot == null) return;
        if (!StandsInLobby(layout))
        {
            Clear();
            return;
        }

        Vector3 floor = LobbyFloor();
        Vector3 face = Facing - floor;
        face.y = 0f;
        _pivot.transform.SetPositionAndRotation(floor, face.sqrMagnitude > 0.01f ? Quaternion.LookRotation(face.normalized, Vector3.up) : Quaternion.identity);
        ApplyLength();
        Physics.SyncTransforms();
        if (DirectionStage.Verbose) Debug.Log("[HumanTreeGrowth] 조우 끝 — 현관으로 돌아감");
    }

    /// <summary>104차: 현관을 떠날 때 플레이어가 멀면 현관에서 먼 가지 소리.</summary>
    private void LeaveTick()
    {
        Transform player = Player();
        if (player == null) return;
        Vector3 home = LobbyFloor();
        Vector3 d = player.position - home;
        d.y = 0f;
        if (d.magnitude < LeaveSoundDistance) return;
        DirectionStage.PlaySound(LeaveSound, home + Vector3.up * 2f, 4f, 1f);
        if (DirectionStage.Verbose) Debug.Log("[HumanTreeGrowth] 현관을 떠남 — 먼 가지 소리(" + d.magnitude.ToString("0.0") + "m)");
    }

    private Transform Player()
    {
        if (_player == null)
        {
            FPController fp = FindAnyObjectByType<FPController>();
            if (fp != null) _player = fp.transform;
        }

        return _player;
    }

    /// <summary>104차: 나무가 지금 화면에 보이는지(가림 포함, 없으면 false — 검수·DirectionStage용).</summary>
    public bool SeenNow
    {
        get { return TreeSeen(); }
    }

    /// <summary>104차: 지금 키(m) — 복도 자리를 고를 때 쓴다(안 보이는 사이 자랄 몫까지).</summary>
    public float WantLength
    {
        get { return _pivot != null ? Mathf.Max(_length, LengthFor(LayoutNow())) : LengthFor(LayoutNow()); }
    }

    /// <summary>103차: 지금 끌려가는 빠르기(m/s, 검수용).</summary>
    public float Pull { get; private set; }

    private void PullTick(float dt)
    {
        Pull = 0f;
        if (_pivot == null || dt <= 0f || NightRun.IsCaptured || DeathCutscene.Playing != null || ViolationScare.Busy) return;
        if (Player() == null) return;

        Vector3 to = _pivot.transform.position - _player.position;
        to.y = 0f;
        float dist = to.magnitude;
        float stop = _where == Place.Corridor ? CorridorPullStop : PullStop;   // 104차: 복도에선 H1 밖에서 멈춘다
        if (dist >= PullRadius || dist <= stop) return;
        if (SpaceIds.Canonical(NightRun.CurrentSpace) == SpaceId.SecurityRoom || InSecurityBox()) return;   // 103차(민: 「경비실 안에 있으면 끌려가는 걸 적용하지 말라」) — 공간 신호가 오기 전(출근 직후)도 상자로 본다
        if (!SameRoom()) return;   // 벽 너머에서는 끌지 않는다
        float k = 1f - Mathf.InverseLerp(stop, PullRadius, dist);   // 가까울수록 세게
        Pull = Mathf.Lerp(0.12f, PullSpeed, k * k);
        float step = Mathf.Min(Pull * dt, dist - stop);
        _player.position += to / dist * step;
    }

    private SpaceZones _zones;

    /// <summary>103차: 플레이어 발이 경비실 상자(SpaceZones) 안인지.</summary>
    private bool InSecurityBox()
    {
        if (_zones == null) _zones = FindAnyObjectByType<SpaceZones>();
        Bounds room;
        if (_zones == null || !_zones.TryGetSpaceBox(SpaceId.SecurityRoom, out room)) return false;
        Vector3 p = _player.position;
        return p.x >= room.min.x && p.x <= room.max.x && p.z >= room.min.z && p.z <= room.max.z;
    }

    /// <summary>103차: 플레이어 가슴에서 나무 몸통까지 가리는 것이 없는지(나무·플레이어 제외) — 같은 공간일 때만 끈다.</summary>
    private bool SameRoom()
    {
        Vector3 from = _player.position + Vector3.up * 1.0f;
        Vector3 to = _pivot.transform.position + Vector3.up * Mathf.Clamp(_length * 0.5f, 0.25f, 1.0f);
        Vector3 d = to - from;
        float dist = d.magnitude;
        if (dist < 0.05f) return true;
        foreach (RaycastHit h in Physics.RaycastAll(from, d / dist, dist, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            Transform t = h.collider.transform;
            if (t.IsChildOf(_pivot.transform) || t.IsChildOf(_player)) continue;
            return false;
        }

        return true;
    }

    /// <summary>지금 제대로 바라보고 있는지(검수용).</summary>
    public bool Gazing
    {
        get { return _gazing; }
    }

    private void GazeTick(float dt)
    {
        bool watched = !NightRun.IsCaptured && Watched();
        if (!watched)
        {
            _gaze = 0f;
            SetWobble(false);
            return;
        }

        SetWobble(true);
        _gaze += dt;
        while (_gaze >= 1f)
        {
            _gaze -= 1f;
            FearAxis axis = (FearAxis)Random.Range((int)FearAxis.Auditory, (int)FearAxis.Layout + 1);
            int delta = Random.Range(NightRun.LobbyTreeMin, NightRun.LobbyTreeMax + 1);
            if (NightRun.AddLobbyTreeStare(axis, delta))
            {
                BodyMeter.Jolt(0.35f);   // 오를 때마다 툭 — 울렁임 위에 짧게
                if (DirectionStage.Verbose) Debug.Log("[HumanTreeGrowth] 응시 1초 — " + axis + " +" + delta);
            }
        }
    }

    private bool Watched()
    {
        return LookedAt(GazeDegrees, GazeRange);
    }

    /// <summary>시선 <paramref name="degrees"/>° 안(가까우면 몸통 어디든) · <paramref name="range"/>m 안 · 가림 없이 나무 몸통을 보는지.</summary>
    private bool LookedAt(float degrees, float range)
    {
        if (_pivot == null) return false;
        Camera cam = Camera.main;
        if (cam == null) return false;
        Vector3 body = _pivot.transform.position + Vector3.up * Mathf.Max(0.3f, _length * 0.6f);
        Vector3 eye = cam.transform.position;
        Vector3 to = body - eye;
        float dist = to.magnitude;
        if (dist > range || dist < 0.05f) return false;
        float tolerance = degrees + Mathf.Rad2Deg * Mathf.Atan2(_length * 0.25f, dist);   // 가까우면 몸통 어디를 봐도
        if (Vector3.Angle(cam.transform.forward, to) > tolerance) return false;
        PlayerSensors hub = PlayerSensors.Active;
        Transform player = hub != null ? hub.PlayerRoot : cam.transform.root;
        foreach (RaycastHit h in Physics.RaycastAll(eye, to / dist, dist - 0.2f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            Transform t = h.collider.transform;
            if (t.IsChildOf(_pivot.transform) || (player != null && t.IsChildOf(player))) continue;
            return false;
        }

        return true;
    }

    private void SetWobble(bool on)
    {
        if (on == _gazing) return;
        _gazing = on;
        if (_fx == null) _fx = FindAnyObjectByType<DirectionScreenFx>();
        if (_fx == null) return;
        if (on) _fx.Push(FxKey, DirectionScreenFx.Kind.Wobble, GazeWobble, 0.6f, -1f, 1.2f);
        else _fx.Release(FxKey);
    }

    /// <summary>그 밤에 맞게 다시 세운다(디버그에서도 부른다).</summary>
    public void Rebuild()
    {
        Clear();
        if (_settledDay >= NightRun.Day) _settledDay = 0;   // 104차: 새 판(1일차)·그 밤 재시작 — 그 밤에 처음 겪은 조우는 없던 일
        if (NightRun.Axes == null) return;
        int layout = LayoutNow();
        if (!StandsInLobby(layout)) return;   // 103차: 3일차 · 배치 축 25 이상부터 (104차: 또는 조우를 겪은 뒤)
        Plant(layout);
    }

    /// <summary>
    /// 103차: 나무가 읽는 배치 값 — 생존 수치와 「보이는 구간」(일차 하한 포함)의 문턱 중 큰 쪽. 생존 수치만 보면 능숙한 플레이어는 25에 닿지 않아 나무를 영영 못 본다
    /// (3일차 하한 B1 = 25 → 3일차부터 누구나 1.5m로 보고, 그 위로는 실제 축만큼 자란다).
    /// </summary>
    private static int LayoutNow()
    {
        int value = NightRun.Axes != null ? NightRun.Axes.GetValue(FearAxis.Layout) : 0;
        IFearAxisReader shown = NightRun.Shown;
        int floor = shown != null ? Bands.LowerBound(shown.GetBand(FearAxis.Layout)) : 0;
        return Mathf.Max(value, floor);
    }

    private void Plant(int layout)
    {
        _pivot = Plant();
        if (_pivot == null) return;
        _length = LengthFor(layout);
        ApplyLength();
        if (DirectionStage.Verbose) Debug.Log("[HumanTreeGrowth] " + NightRun.Day + "일차 — 현관 한가운데 바닥에 심음 " + _length.ToString("0.00") + "m (배치 축 " + layout + ")");
    }

    /// <summary>103차: 심을 자리(바닥에서 1.5m 위)가 지금 화면에 보이는지(가림 포함).</summary>
    private static bool SpotSeen()
    {
        Camera cam = Camera.main;
        if (cam == null) return false;
        Vector3 p = FloorSpot + Vector3.down * 1f + Vector3.up * 1.5f;
        Vector3 v = cam.WorldToViewportPoint(p);
        if (v.z <= 0f || v.x < -0.1f || v.x > 1.1f || v.y < -0.1f || v.y > 1.1f) return false;
        Vector3 eye = cam.transform.position;
        Vector3 d = p - eye;
        return !Physics.Raycast(eye, d.normalized, d.magnitude - 0.3f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
    }

    private void ApplyLength()
    {
        if (_pivot == null) return;
        float k = Mathf.Max(0.01f, _length / Mathf.Max(0.01f, _unitHeight));
        _pivot.transform.localScale = new Vector3(k, k, k);
        if (_trunk != null)
        {
            // 줄기는 키와 상관없이 같은 굵기로 — 축척을 되돌려 세계 기준 반지름·높이를 맞춘다.
            _trunk.radius = TrunkRadius / k;
            _trunk.height = Mathf.Max(_length, TrunkRadius * 2f) / k;
            _trunk.center = new Vector3(0f, _trunk.height * 0.5f, 0f);
        }
    }

    private void Clear()
    {
        if (_pivot != null) Destroy(_pivot);
        _pivot = null;
        _model = null;
        _trunk = null;
        _heads = null;
        _gaze = 0f;
        _where = Place.Lobby;
        _returning = false;
        _noticed = false;
        _maxLength = MaxLength;
        SetWobble(false);
    }

    /// <summary>현관 한가운데 바닥 점(아래로 쏴서 맞춘다).</summary>
    private static Vector3 LobbyFloor()
    {
        RaycastHit hit;
        if (Physics.Raycast(FloorSpot, Vector3.down, out hit, 6f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return hit.point;
        return FloorSpot + Vector3.down;
    }

    /// <summary>103차: 현관 한가운데 바닥 점에 뿌리를 두고 바로 세운다 — 뿌리(바닥 점)를 축으로 키우면 위로만 자란다.</summary>
    private GameObject Plant()
    {
        GameObject prefab = Resources.Load<GameObject>(PrefabPath);
        if (prefab == null)
        {
            Debug.LogWarning("[HumanTreeGrowth] 대역이 없습니다: Resources/" + PrefabPath);
            return null;
        }

        Vector3 floor = LobbyFloor();

        GameObject pivot = new GameObject("인체 나무(현관 바닥)");
        pivot.transform.position = floor;
        GameObject tree = Instantiate(prefab, pivot.transform);
        _model = tree;
        tree.transform.localPosition = Vector3.zero;
        tree.transform.localRotation = Quaternion.identity;
        foreach (Collider c in tree.GetComponentsInChildren<Collider>(true)) c.enabled = false;
        StripGazeEvent(tree);

        // 뿌리(묶음 상자 아랫면 가운데)를 바닥 점에 맞춘다.
        Bounds b;
        if (RenderBounds(tree, out b))
        {
            _unitHeight = Mathf.Max(0.05f, b.size.y);
            tree.transform.position += new Vector3(floor.x - b.center.x, floor.y - b.min.y, floor.z - b.center.z);
        }

        Vector3 face = Facing - floor;
        face.y = 0f;
        pivot.transform.rotation = face.sqrMagnitude > 0.01f ? Quaternion.LookRotation(face.normalized, Vector3.up) : Quaternion.identity;

        // 103차(사용자: 「인체 나무에 어색한 조명 — 어둠에선 어둡게, 손전등을 비추면 라이팅이 적용되게」): 천장 조명·스스로 빛남을 뗐다.
        // 모델 재질의 발광(김진선님 Human_Tree 재질 1·1·1)도 인스턴스에서 끈다 — 현관 어둠 그대로, 손전등·주변 등에만 비친다.
        StandInFactory.KillGlow(tree);
        _heads = tree.AddComponent<TreeHeads>();   // 104차 A: 얼굴들이 고개만 돌려 본다(배치 구간 2부터)

        // 줄기 충돌체(103차) — 한가운데라 지나가지 못하게. 판정 기준점은 없다(104차: H1 「미분류 물체」 기준점은 복도에 나와 있을 때만 — ComeOut).
        _trunk = pivot.AddComponent<CapsuleCollider>();
        _trunk.direction = 1;
        return pivot;
    }

    /// <summary>103차: 사람 나무 대역에 딸린 「오래 바라보면 한 번 터지는 연출」을 뗀다 — 현관 나무는 바라보는 동안 화면이 물드는 것(HorrorGazeHold)만.</summary>
    private static void StripGazeEvent(GameObject tree)
    {
        foreach (MonoBehaviour m in tree.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (m == null) continue;
            string n = m.GetType().Name;
            if (n == "HorrorGazeTrigger") DestroyImmediate(m);
        }

        foreach (MonoBehaviour m in tree.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (m != null && m.GetType().Name == "HorrorEvent") DestroyImmediate(m);
        }

        foreach (UnityEngine.Playables.PlayableDirector d in tree.GetComponentsInChildren<UnityEngine.Playables.PlayableDirector>(true))
        {
            if (d != null) DestroyImmediate(d);
        }
    }

    private static bool RenderBounds(GameObject go, out Bounds b)
    {
        b = new Bounds();
        bool any = false;
        foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
        {
            if (!r.enabled) continue;
            if (!any)
            {
                b = r.bounds;
                any = true;
            }
            else b.Encapsulate(r.bounds);
        }

        return any;
    }
}

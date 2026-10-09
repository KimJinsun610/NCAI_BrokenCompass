using UnityEngine;

/// <summary>
/// 몹의 아주 작은 살아 있는 움직임(71차, 민: 「노란 얼굴이 정지 상태일 때 IDLE이 적용되지 않는다 · 교실에 앉은 소년이 완전히 정지된 상태로 보인다 —
/// 과장 없이, 자세히 봤을 때 살아 있는 사람처럼 미세하게」).
/// <list type="bullet">
/// <item>원인: 새 business duck의 숨쉬기 클립(<c>NewBusinessDuck_idle</c> 「Scene」 6초)은 원본부터 손만 3°쯤 움직이고 몸통·머리는 그대로다(회전 곡선 최대 변화 0.026 — PlayScene 실측).
/// 앉은 소년(<c>mob.boy</c>)은 애니메이터 없이 굳힌 자세다. 클립을 바꾸지 않고 그 위에 뼈 회전을 조금 더한다.</item>
/// <item>숨: 가슴·척추가 앞뒤로, 어깨(쇄골)가 위아래로 · 머리: 느린 흔들림(펄린) · 가끔 작게 고쳐 앉음/고개를 기울임. 회전은 몸 기준 축(루트의 오른쪽·위·앞)으로 걸어 리그 축과 무관.</item>
/// <item>애니메이터가 그 프레임에 뼈를 썼으면 그 위에, 쓰지 않았으면(굳힌 자세·화면 밖 컬링) 지난 바탕 자세 위에 더한다 — 쌓이지 않는다.</item>
/// <item>머리 박기(<see cref="BoyHeadBang"/>)·걸어 나가기(<see cref="StandInExit"/>)·걷기(<see cref="DirectionWalker"/>) 중에는 쉰다.</item>
/// </list>
/// <see cref="StandInFactory.Create"/>가 노란 얼굴(<c>mob.duck</c>)·창밖 남자(<c>mob.windowman</c>, 같은 모델)·앉은 소년(<c>mob.boy</c>)에 붙인다.
/// </summary>
[DisallowMultipleComponent]
public sealed class ProceduralIdle : MonoBehaviour
{
    /// <summary>움직임 세기 묶음.</summary>
    public enum Profile
    {
        /// <summary>서 있는 몹 — 숨 · 무게 옮김 · 머리 흔들림 · 가끔 고개를 갸웃.</summary>
        Standing,

        /// <summary>앉은 몹 — 숨과 머리의 아주 작은 흔들림, 가끔 아주 작게 고쳐 앉음.</summary>
        Seated
    }

    private struct Bone
    {
        public Transform T;
        public Quaternion Applied;
        public Quaternion Base;
        public bool Has;
    }

    private const int Pelvis = 0, Spine = 1, Chest = 2, Neck = 3, Head = 4, ClavL = 5, ClavR = 6, ArmL = 7, ArmR = 8;
    private static readonly string[][] Names =
    {
        new[] { "Pelvis", "Hips", "pelvis" },
        new[] { "Spine", "spine" },
        new[] { "Chest", "chest", "Spine1" },
        new[] { "Neck", "neck" },
        new[] { "Head", "head" },
        new[] { "Clavicle.L", "LeftShoulder", "clavicle.L" },
        new[] { "Clavicle.R", "RightShoulder", "clavicle.R" },
        new[] { "UpperArm.L", "LeftUpperArm", "upper_arm.L" },
        new[] { "UpperArm.R", "RightUpperArm", "upper_arm.R" },
    };

    [SerializeField] private Profile profile = Profile.Standing;

    private readonly Bone[] _bones = new Bone[9];
    private float _seed;
    private float _breathPeriod;
    private float _nextShift;
    private float _shiftFrom;
    private Vector2 _shift;
    private Vector2 _shiftPrev;
    private float _shiftDur;
    private BoyHeadBang _bang;
    private StandInExit _exit;
    private DirectionWalker _walker;
    private bool _ready;

    /// <summary>그 대역에 맞는 움직임을 붙인다. 붙일 대역이 아니면 null.</summary>
    public static ProceduralIdle AttachFor(string standInId, GameObject go)
    {
        if (go == null) return null;
        Profile p;
        switch (standInId)
        {
            case "mob.duck":
            case "mob.windowman":
                p = Profile.Standing;
                break;
            case "mob.boy":
                p = Profile.Seated;
                break;
            default:
                return null;
        }

        ProceduralIdle idle = go.GetComponent<ProceduralIdle>();
        if (idle == null) idle = go.AddComponent<ProceduralIdle>();
        idle.profile = p;
        return idle;
    }

    /// <summary>지금 묶음.</summary>
    public Profile Kind
    {
        get { return profile; }
    }

    private void Start()
    {
        for (int i = 0; i < _bones.Length; i++) _bones[i].T = Find(Names[i]);
        _seed = Random.value * 100f;
        _breathPeriod = Random.Range(3.8f, 4.6f);
        _nextShift = Time.time + Random.Range(4f, 9f);
        _shiftFrom = -99f;
        _shiftDur = 1f;
        _bang = GetComponent<BoyHeadBang>();
        _exit = GetComponent<StandInExit>();
        _walker = GetComponent<DirectionWalker>();
        _ready = _bones[Chest].T != null || _bones[Spine].T != null || _bones[Head].T != null;
    }

    private Transform Find(string[] names)
    {
        foreach (Transform t in GetComponentsInChildren<Transform>(true))
        {
            for (int i = 0; i < names.Length; i++)
            {
                if (t.name == names[i]) return t;
            }
        }

        return null;
    }

    private bool Resting
    {
        get
        {
            if (_bang == null) _bang = GetComponent<BoyHeadBang>();   // 머리 박기는 대면 중에 붙는다
            if (_bang != null && _bang.enabled) return false;
            if (_exit != null && _exit.Leaving) return false;
            if (_walker != null && _walker.enabled && !_walker.Arrived) return false;
            return true;
        }
    }

    private void LateUpdate()
    {
        if (!_ready) return;
        bool rest = Resting;

        // 바탕 자세: 애니메이터가 이 프레임에 썼으면 그것, 아니면 지난 바탕(더한 것이 쌓이지 않게).
        for (int i = 0; i < _bones.Length; i++)
        {
            Transform t = _bones[i].T;
            if (t == null) continue;
            Quaternion now = t.localRotation;
            if (!_bones[i].Has || now != _bones[i].Applied) _bones[i].Base = now;
            t.localRotation = _bones[i].Base;
            _bones[i].Has = true;
            _bones[i].Applied = _bones[i].Base;
        }

        if (!rest) return;

        float time = Time.time;
        bool seated = profile == Profile.Seated;
        Transform root = transform;
        Vector3 right = root.right;
        Vector3 up = root.up;
        Vector3 fwd = root.forward;

        // 숨 — 들숨이 날숨보다 조금 짧게(살짝 비대칭).
        float phase = Mathf.Repeat(time / _breathPeriod + _seed, 1f);
        float breath = phase < 0.42f ? Mathf.Sin(phase / 0.42f * Mathf.PI * 0.5f) : Mathf.Cos((phase - 0.42f) / 0.58f * Mathf.PI * 0.5f);
        breath = breath * 2f - 1f;   // -1(내쉼) ~ 1(들이쉼)
        float chest = seated ? 1.0f : 1.3f;
        Rotate(Spine, right, -breath * chest * 0.45f);
        Rotate(Chest, right, -breath * chest);
        Rotate(Neck, right, breath * chest * 0.5f);   // 고개는 제자리에 남게 되돌림
        Rotate(ClavL, fwd, breath * (seated ? 0.8f : 1.3f));
        Rotate(ClavR, fwd, -breath * (seated ? 0.8f : 1.3f));
        Rotate(ArmL, fwd, -breath * (seated ? 0.3f : 0.9f));
        Rotate(ArmR, fwd, breath * (seated ? 0.3f : 0.9f));

        // 서 있으면 무게를 아주 천천히 옮긴다.
        if (!seated)
        {
            float sway = Mathf.Sin(time * 0.6f + _seed) * 0.7f + (Mathf.PerlinNoise(_seed, time * 0.15f) - 0.5f) * 0.8f;
            Rotate(Pelvis, fwd, sway);
            Rotate(Spine, fwd, -sway * 0.6f);
        }

        // 머리 — 느린 흔들림.
        float headSpeed = seated ? 0.09f : 0.16f;
        float yawAmp = seated ? 1.3f : 2.6f;
        float pitchAmp = seated ? 0.9f : 1.6f;
        float yaw = (Mathf.PerlinNoise(_seed + 3.1f, time * headSpeed) - 0.5f) * 2f * yawAmp;
        float pitch = (Mathf.PerlinNoise(_seed + 7.7f, time * headSpeed) - 0.5f) * 2f * pitchAmp;

        // 가끔 — 서 있으면 고개를 작게 갸웃하고 머물다 돌아오고, 앉아 있으면 아주 작게 고쳐 앉는다.
        if (time >= _nextShift)
        {
            _shiftPrev = CurrentShift(time);
            _shift = seated
                ? new Vector2(Random.Range(-1.2f, 1.2f), Random.Range(-1f, 1f))
                : new Vector2(Random.Range(-4f, 4f), Random.Range(-2.5f, 2.5f));
            _shiftFrom = time;
            _shiftDur = seated ? Random.Range(0.9f, 1.4f) : Random.Range(0.35f, 0.6f);
            _nextShift = time + (seated ? Random.Range(9f, 18f) : Random.Range(6f, 13f));
        }

        Vector2 shift = CurrentShift(time);
        Rotate(Head, up, yaw);
        Rotate(Head, right, pitch + shift.y);
        Rotate(Head, fwd, shift.x);
        if (seated) Rotate(Spine, fwd, shift.x * 0.4f);

        for (int i = 0; i < _bones.Length; i++)
        {
            if (_bones[i].T != null) _bones[i].Applied = _bones[i].T.localRotation;
        }
    }

    private Vector2 CurrentShift(float time)
    {
        float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((time - _shiftFrom) / Mathf.Max(0.05f, _shiftDur)));
        return Vector2.Lerp(_shiftPrev, _shift, k);
    }

    private void Rotate(int bone, Vector3 axis, float degrees)
    {
        Transform t = _bones[bone].T;
        if (t == null || Mathf.Abs(degrees) < 0.0001f) return;
        t.rotation = Quaternion.AngleAxis(degrees, axis) * t.rotation;
    }
}

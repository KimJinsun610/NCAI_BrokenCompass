using NightDuty;
using UnityEngine;

/// <summary>
/// 시체 낙하 변주 — 교실 문 쪽에서 보이는 시체는 쓰러져 있지 않고 <b>서 있는 채로 시작</b>한다(민 요청).
/// <list type="bullet">
/// <item>선 몹(<c>mob.boy.stand</c>)이 플레이어를 향해 서 있고, 그 자리에서 거친 숨(<c>corpse.rasp</c>)이 3D로 작게 난다.</item>
/// <item>플레이어가 <see cref="RunTriggerDistance"/> 안으로 다가오면 <see cref="StandInExit.RunTo"/>의 기괴하게 빠른 걸음으로 과학실 쪽으로 달려가 사라진다.</item>
/// <item>다가오지 않고 시선을 돌려 한 번 본 뒤 몸 어디도 화면에 없으면 그 자리에서 사라진다(바퀴벌레는 따로 남는다).</item>
/// </list>
/// 판정과 무관한 순수 연출이다.
/// </summary>
[DisallowMultipleComponent]
public sealed class CorpseRunner : MonoBehaviour
{
    /// <summary>이 거리(m, 수평) 안으로 다가오면 달린다. 교실 중앙~문 사이 거리에 맞춘 값 — 씬에서 보고 조절.</summary>
    public const float RunTriggerDistance = 7.4f;   // 1-3 교실 상자 가운데(46, 36)~교실 문(40.5, 40.9) 거리(2026-11 실측)

    /// <summary>시선을 돌려 사라지기까지: 보이지 않은 채 이 시간(초).</summary>
    public const float LookAwaySeconds = 0.5f;

    /// <summary>나타난 뒤 이 시간(초)은 사라지지 않는다.</summary>
    public const float MinShowSeconds = 1.2f;

    /// <summary>달리는 최대 시간(초).</summary>
    public const float RunMaxSeconds = 4f;

    /// <summary>거친 숨 소리 키.</summary>
    public const string RaspKey = "corpse.rasp";

    private Vector3 _target;
    private float _bornAt;
    private float _awaySince = -1f;
    private bool _seen;
    private bool _running;
    private AudioSource _rasp;

    /// <summary>달리는 중인지(시험·검수).</summary>
    public bool Running
    {
        get { return _running; }
    }

    /// <summary>떠 있는 시간 상한(초) — 아무도 다가오지 않으면 이때 사라진다.</summary>
    public const float MaxLifeSeconds = 90f;

    /// <summary>
    /// 시체가 사라진 뒤 교탁 옆(<see cref="DirectionStage.TeacherDeskCorpseSpot"/>)에 사람을 세운다.
    /// 그 자리가 지금 플레이어 화면에 보이면 보이지 않을 때까지 기다린다(눈앞에서 툭 나타나지 않게, 최대 15초).
    /// </summary>
    public static void SpawnAtDesk()
    {
        GameObject host = new GameObject("시체 교탁 대기");
        host.AddComponent<DeskSpawner>();
    }

    private sealed class DeskSpawner : MonoBehaviour
    {
        private float _until;

        private void Start()
        {
            _until = Time.time + 15f;
        }

        private void Update()
        {
            Vector3 spot = DirectionStage.FloorBelow(DirectionStage.TeacherDeskCorpseSpot + Vector3.up * 0.5f);
            Camera cam = Camera.main;
            bool visible = false;
            if (cam != null)
            {
                Vector3 v = cam.WorldToViewportPoint(spot + Vector3.up * 1.0f);
                visible = v.z > 0f && v.x > -0.05f && v.x < 1.05f && v.y > -0.05f && v.y < 1.05f;
            }

            if (visible && Time.time < _until) return;

            Vector3 door = DirectionStage.ClassroomDoorOutside;
            GameObject figure = StandInFactory.Create("mob.boy.stand", spot, door, string.Empty);
            if (figure != null)
            {
                figure.name = "시체(교탁 옆)";
                UnseenDespawn.Mark(figure);
                Attach(figure, DirectionStage.FloorBelow(door + Vector3.up * 0.5f));
            }

            Destroy(gameObject);
        }
    }

    /// <summary>선 몹에 붙인다. <paramref name="runTarget"/> = 달려갈 바닥 점(과학실 쪽).</summary>
    public static CorpseRunner Attach(GameObject figure, Vector3 runTarget)
    {
        CorpseRunner r = figure.AddComponent<CorpseRunner>();
        r._target = runTarget;
        r._bornAt = Time.time;
        r.StartRasp();
        return r;
    }

    /// <summary>어두운 교실에서도 윤곽이 보이게 몸 앞에 약한 차가운 점광(그림자 없음) — 몸을 따라다닌다.</summary>
    private void AddFaintLight()
    {
        GameObject go = new GameObject("시체 약한 빛");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, 1.4f, 0.9f);
        Light l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = new Color(0.62f, 0.72f, 1f);
        l.intensity = 1.6f;
        l.range = 4.5f;
        l.shadows = LightShadows.None;
    }

    private void StartRasp()
    {
        float volume;
        AudioClip clip = DirectionSoundTableSO.FindExact(RaspKey, out volume);
        if (clip == null) return;

        _rasp = gameObject.AddComponent<AudioSource>();
        _rasp.clip = clip;
        _rasp.loop = true;
        _rasp.volume = volume * 0.5f;   // 크게 들리지 않아도 된다(민)
        _rasp.spatialBlend = 1f;
        _rasp.rolloffMode = AudioRolloffMode.Linear;
        _rasp.minDistance = 1.5f;
        _rasp.maxDistance = 14f;
        _rasp.dopplerLevel = 0f;
        _rasp.Play();
    }

    private void Update()
    {
        if (_running) return;

        if (!NightRun.IsNightActive || Time.time - _bornAt > MaxLifeSeconds)
        {
            Destroy(gameObject);   // 밤이 끝났거나 아무도 오지 않았다
            return;
        }

        Camera cam = Camera.main;
        if (cam == null) return;

        Vector3 to = cam.transform.position - transform.position;
        to.y = 0f;
        float dist = to.magnitude;

        // 플레이어를 바라본다(고개만이 아니라 몸 방향 — 서 있는 동안 천천히).
        if (dist > 0.1f)
        {
            Quaternion want = Quaternion.LookRotation(to / dist, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, want, 120f * Time.deltaTime);
        }

        if (dist <= RunTriggerDistance)
        {
            Run();
            return;
        }

        UnseenDespawn u = GetComponent<UnseenDespawn>();
        if (u == null) u = gameObject.AddComponent<UnseenDespawn>();
        bool inView = u.Visible(cam);
        if (inView) _seen = true;
        if (Time.time - _bornAt < MinShowSeconds) return;

        if (inView || !_seen)
        {
            _awaySince = -1f;
            return;
        }

        if (_awaySince < 0f) _awaySince = Time.time;
        if (Time.time - _awaySince >= LookAwaySeconds) Destroy(gameObject);   // 시선을 돌리면 그 자리에서 사라진다
    }

    private void Run()
    {
        _running = true;
        if (_rasp != null) _rasp.Stop();

        StandInExit exit = GetComponent<StandInExit>();
        if (exit == null)
        {
            Destroy(gameObject);
            return;
        }

        Camera cam = Camera.main;
        UnseenDespawn u = GetComponent<UnseenDespawn>();
        // 벽쪽으로 가지 않고 문을 정확히 지나가게: 교탁 → 문 안쪽 → 문 밖 복도.
        Vector3 inside = DirectionStage.FloorBelow(DirectionStage.ClassroomDoorInside + Vector3.up * 0.5f);
        exit.RunPath(new[] { inside, _target }, RunMaxSeconds, () => cam != null && u != null && !u.Visible(cam));
    }
}

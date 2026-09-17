using NightDuty;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 게임 흐름(GameSession·GameTime)과 판정 코어(<see cref="NightRun"/>)를 잇는 Play 씬 구동기.
/// <list type="bullet">
/// <item>Start: <see cref="NightRun.BeginNight"/>(현재 일차, 게임 시계 분). 씬의 JudgeTarget이 모두 켜진 뒤라 대상 검사가 맞다.</item>
/// <item>Update: 게임 시계가 흐를 때만 <see cref="NightRun.Tick"/>. 판정 시간은 <b>실제 초</b>다 — 시계 배속을 곱하지 않는다.
/// DayIntro 연출 중(시계 정지)·일시정지(timeScale 0)·근무 종료 뒤에는 흐르지 않는다.</item>
/// <item>GameTime.ShiftEnded: <see cref="NightRun.RequestEndNight"/> → <see cref="EventBus.DayEnded"/> → PlayResultRouter가 결과창으로 보낸다.</item>
/// <item>파괴될 때 밤이 아직 열려 있으면(메인으로 나가기 등) <see cref="NightRun.AbandonNight"/>.</item>
/// </list>
/// <para>
/// <b>씬·프리팹에 직접 놓지 않아도 된다.</b> 씬이 로드될 때 GameTime이 있고 이 컴포넌트가 없으면 자동으로 하나 만든다
/// (통일 씬 작업과 프리팹 잠금이 겹치지 않게 하기 위함). 직접 놓아 두면 자동 생성은 건너뛴다.
/// </para>
/// <para>
/// TODO(태블릿): Tab을 열고 닫을 때 <c>NightRun.Send(JudgeSignal.Tab(bool))</c>를 보내고 GameTime도 함께 멈춰야 한다(기획서 Tab 규칙, Q6).
/// TODO(Q2): 종료 요청 수락 조건(필수 점검·오늘 조우)이 생기면 거절됐을 때의 시계·UI 처리를 정한다. 지금 코어는 항상 수락한다.
/// </para>
/// </summary>
[DisallowMultipleComponent]
public sealed class NightRunDriver : MonoBehaviour
{
    [Tooltip("비워 두면 씬에서 자동으로 찾는다.")]
    [SerializeField] private GameTime gameTime;

    private bool _begun;

    // 지금 밤을 연 구동기. 씬 전환 때 옛 구동기의 OnDestroy가 새 밤을 버리지 않게 한다.
    private static NightRunDriver s_owner;

    /// <summary>이 구동기가 연결된 게임 시계.</summary>
    public GameTime Clock => gameTime;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Install()
    {
        s_owner = null;
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
        if (!scene.IsValid() || !scene.isLoaded) return;
        foreach (NightRunDriver existing in FindObjectsByType<NightRunDriver>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (existing.gameObject.scene == scene) return;
        }

        GameTime clock = null;
        foreach (GameTime candidate in FindObjectsByType<GameTime>(FindObjectsSortMode.None))
        {
            if (candidate.gameObject.scene == scene)
            {
                clock = candidate;
                break;
            }
        }

        if (clock == null) return;   // 근무 씬이 아니다(메인·로딩·결과·시험 씬).

        GameObject go = new GameObject("NightRunDriver (auto)");
        SceneManager.MoveGameObjectToScene(go, scene);
        NightRunDriver driver = go.AddComponent<NightRunDriver>();
        driver.gameTime = clock;
    }

    private void Start()
    {
        if (gameTime == null) gameTime = FindAnyObjectByType<GameTime>();
        if (gameTime == null)
        {
            Debug.LogWarning("[NightRunDriver] 씬에 GameTime이 없어 밤을 시작하지 않습니다.", this);
            enabled = false;
            return;
        }

        gameTime.ShiftEnded += OnShiftEnded;
        NightRun.BeginNight(GameSession.CurrentDay, CurrentMinutes);
        _begun = true;
        s_owner = this;
    }

    private void Update()
    {
        if (!_begun || s_owner != this || !NightRun.IsNightActive) return;
        if (!gameTime.IsRunning || gameTime.IsEnded) return;

        NightRun.Tick(Time.deltaTime);
    }

    private void OnDestroy()
    {
        if (gameTime != null) gameTime.ShiftEnded -= OnShiftEnded;

        if (s_owner != this) return;
        s_owner = null;

        if (NightRun.IsNightActive)
        {
            NightRun.AbandonNight();
        }
    }

    private void OnShiftEnded()
    {
        if (s_owner != this || !NightRun.IsNightActive) return;   // 이미 포획·중단된 밤

        if (!NightRun.RequestEndNight() && !NightRun.IsCaptured)
        {
            Debug.LogWarning("[NightRunDriver] 근무 종료 요청이 거절됐습니다. 수락 조건 처리(Q2)가 아직 없습니다.", this);
        }
    }

    private int CurrentMinutes()
    {
        return gameTime != null ? gameTime.CurrentMinutes : -1;
    }
}

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 플레이어가 이 구역(경비실)에 <b>인게임 N분 동안 머물면</b> 태블릿 메시지 이벤트(<see cref="TabletMessageEvents"/>)를 실행한다.
/// Is Trigger 콜라이더와 같은 오브젝트에 붙이고, 씬에서는 이 오브젝트의 위치 · 크기만 조절한다.
///
/// <para><b>흐름</b> — 구역 안에 들어오면 인게임 시간을 센다 → <see cref="stayMinutes"/>이 차면 이벤트 실행 →
/// 도중에 나가면 이벤트를 멈추고(<see cref="stopOnLeave"/>) 세던 시간도 0으로 돌린다.
/// 이벤트가 끝까지 가야 「작동했다」로 기록하므로, 도중에 나갔다가 다시 와서 머물면 처음부터 다시 시작한다.</para>
///
/// <para><b>언제 작동하는가</b> — <see cref="days"/>(일차)와 <see cref="requiredCards"/>(그날 덱의 수칙)를 <b>둘 다</b> 만족할 때.
/// 비워 둔 조건은 따지지 않는다. 한 번 끝까지 가면 <see cref="once"/>에 따라 그 회차 · 그날 밤에는 다시 작동하지 않는다.</para>
///
/// <para>시간은 <see cref="GameMinutes"/>로 센다 — 일시정지 · DayIntro 연출 · 근무 종료 중에는 흐르지 않는다.</para>
///
/// <para>
/// 구역 판정은 물리 트리거 이벤트가 아니라 콜라이더 모양 안에 플레이어가 있는지를 매 프레임 직접 본다.
/// 트리거 이벤트는 머무는 도중 콜라이더가 꺼졌다 켜지거나 씬이 바뀔 때 나감(Exit)이 오지 않을 수 있어,
/// 「나가면 멈춘다」를 믿고 맡길 수 없다.
/// </para>
/// </summary>
[RequireComponent(typeof(Collider))]
public class GuardRoomLoiterTrigger : MonoBehaviour
{
    public enum OncePer
    {
        [Tooltip("회차(1~5일차 전체)에 한 번")] Run,
        [Tooltip("조건에 맞는 날마다 하룻밤에 한 번")] Night
    }

    [Header("실행할 이벤트")]
    [Tooltip("CSV(TabletMessageEvents.csv) A열의 이벤트 이름. 글자까지 같아야 한다.")]
    [SerializeField] private string eventName = "GuardRoom_Knock";

    [Tooltip("구역 안에 이만큼(인게임 분) 머물면 실행한다. 배속 30이면 인게임 1분 = 현실 2초.")]
    [SerializeField, Min(0f)] private float stayMinutes = 30f;

    [Tooltip("켜면 이벤트 도중에 구역을 나갈 때 이벤트를 멈춘다. 이미 온 메시지는 남는다.")]
    [SerializeField] private bool stopOnLeave = true;

    [Header("발동 조건 (비워 두면 따지지 않음)")]
    [Tooltip("이 일차에만 작동한다. 예: 3 → 3일차만. 비워 두면 모든 날.")]
    [SerializeField] private int[] days = new int[0];

    [Tooltip("그날 덱에 이 수칙 중 하나라도 있을 때만 작동한다. 카드 ID(H1~H6 · C1~C6 · S1~S6 · T1~T6). 비워 두면 따지지 않음.")]
    [SerializeField] private string[] requiredCards = new string[0];

    [Tooltip("끝까지 간 뒤 다시 작동하지 않는 범위.")]
    [SerializeField] private OncePer once = OncePer.Run;

    // 작동 기록은 씬이 바뀌어도(다음 날 Play 씬을 다시 불러도) 남아야 하므로 static에 둔다.
    private struct Record { public int Run; public int Day; }
    private static readonly Dictionary<string, Record> completed = new Dictionary<string, Record>();

    private Collider zone;
    private Transform player;
    private bool inside;
    private float stayed;          // 이번에 들어와서 머문 인게임 분
    private bool playingByMe;      // 이 트리거가 실행한 이벤트가 진행 중인가
    private bool blockedUntilExit; // 실행에 실패했거나 조건이 안 맞으면 나갔다 올 때까지 다시 시도하지 않는다

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatic()
    {
        completed.Clear();
    }

    private void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void Awake()
    {
        zone = GetComponent<Collider>();
    }

    private void OnEnable()
    {
        TabletMessageEvents.Finished += OnEventFinished;
    }

    private void OnDisable()
    {
        TabletMessageEvents.Finished -= OnEventFinished;
        Leave();
    }

    private void Update()
    {
        if (player == null)
        {
            FPController controller = FindAnyObjectByType<FPController>();
            if (controller == null) return;
            player = controller.transform;
        }

        bool nowInside = Contains(player.position);
        if (!nowInside)
        {
            if (inside) Leave();
            return;
        }

        inside = true;
        if (playingByMe || blockedUntilExit) return;

        stayed += GameMinutes.Delta;
        if (stayed < stayMinutes) return;

        TryPlay();
    }

    private void TryPlay()
    {
        // 조건은 실행하는 순간에 본다. 그날 덱은 밤이 시작된 뒤에야 정해진다.
        if (!IsDayAllowed() || !HasRequiredCard() || IsCompleted() || TabletMessageEvents.IsPlaying(eventName))
        {
            blockedUntilExit = true;
            return;
        }

        if (TabletMessageEvents.Play(eventName)) playingByMe = true;
        else blockedUntilExit = true; // 경고는 TabletMessageEvents가 남긴다. 매 프레임 되풀이하지 않는다.
    }

    private void Leave()
    {
        if (playingByMe && stopOnLeave) TabletMessageEvents.Stop(eventName);

        inside = false;
        stayed = 0f;
        playingByMe = false;
        blockedUntilExit = false;
    }

    private void OnEventFinished(string finishedName)
    {
        if (!playingByMe || !string.Equals(finishedName, eventName.Trim())) return;

        playingByMe = false;
        blockedUntilExit = true; // 끝까지 간 뒤 계속 서 있어도 다시 세지 않는다
        completed[Key] = new Record { Run = GameSession.RunNumber, Day = GameSession.CurrentDay };
    }

    // ─────────────────────────────── 조건 ───────────────────────────────

    private string Key
    {
        get { return eventName.Trim(); }
    }

    private bool IsDayAllowed()
    {
        if (days == null || days.Length == 0) return true;

        int today = GameSession.CurrentDay;
        for (int i = 0; i < days.Length; i++)
        {
            if (days[i] == today) return true;
        }
        return false;
    }

    private bool HasRequiredCard()
    {
        if (requiredCards == null || requiredCards.Length == 0) return true;

        IReadOnlyList<NightDuty.RuleSO> deck = NightDuty.NightRun.TodayDeck;
        if (deck == null) return false;

        for (int i = 0; i < deck.Count; i++)
        {
            if (deck[i] == null) continue;
            for (int j = 0; j < requiredCards.Length; j++)
            {
                if (string.Equals(deck[i].CardId, (requiredCards[j] ?? string.Empty).Trim(), System.StringComparison.OrdinalIgnoreCase)) return true;
            }
        }
        return false;
    }

    private bool IsCompleted()
    {
        if (!completed.TryGetValue(Key, out Record record)) return false;
        if (record.Run != GameSession.RunNumber) return false;
        return once == OncePer.Run || record.Day == GameSession.CurrentDay;
    }

    /// <summary>콜라이더 모양 안에 있는가. ClosestPoint는 안쪽 점이면 그 점을 그대로 돌려준다.</summary>
    private bool Contains(Vector3 point)
    {
        if (zone == null || !zone.enabled) return false;
        if (!zone.bounds.Contains(point)) return false; // 빠른 거절
        return (zone.ClosestPoint(point) - point).sqrMagnitude < 0.0001f;
    }

    private void OnDrawGizmos()
    {
        Collider c = zone != null ? zone : GetComponent<Collider>();
        if (c == null) return;

        Gizmos.color = playingByMe ? new Color(1f, 0.25f, 0.2f, 0.25f) : new Color(1f, 0.6f, 0.1f, 0.15f);
        Bounds b = c.bounds;
        Gizmos.DrawCube(b.center, b.size);
        Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.9f);
        Gizmos.DrawWireCube(b.center, b.size);
    }
}

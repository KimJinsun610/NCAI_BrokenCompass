using System.Collections.Generic;
using NightDuty;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 66차(민: 「직접 닫은 문이 자동으로 열림」 — 7단계): 플레이어가 [E]로 닫은 문(DoorNarrow·DoorWide 계열 — 서랍·사물함·책장은 빼고)이
/// 잠시 뒤 저절로 다시 열린다.
/// <list type="bullet">
/// <item>닫은 뒤 <see cref="MinDelay"/>~<see cref="MaxDelay"/>초가 지나고, 플레이어가 <see cref="MinDistance"/>m 넘게 떨어져 있고, 문이 화면에 보이지 않을 때 연다(보는 앞에서 열리지 않는다).</item>
/// <item>그사이 플레이어가 다시 열었거나 <see cref="GiveUpSeconds"/>초가 지나면 그만둔다. 밤이 아니거나 붙잡힌 동안은 기다린다.</item>
/// <item>여는 것은 <see cref="DoorHandle.Open"/> — 출처는 연출(<c>DoorRelay</c>)이고, 보지 않을 때 열리므로 H2 「열린 문은 열린 채로」의 관찰 신호는 나가지 않는다.</item>
/// </list>
/// 근무 씬에 스스로 선다. 씬은 고치지 않는다.
/// </summary>
[DisallowMultipleComponent]
public sealed class DoorReopener : MonoBehaviour
{
    /// <summary>닫은 뒤 가장 빨리 다시 열리는 시간(초).</summary>
    public const float MinDelay = 6f;

    /// <summary>닫은 뒤 가장 늦게 다시 열리는 시간(초) — 이 사이에서 무작위.</summary>
    public const float MaxDelay = 14f;

    /// <summary>다시 열 때 플레이어와 떨어져 있어야 하는 거리(m).</summary>
    public const float MinDistance = 3f;

    /// <summary>이만큼 지나도 열 기회가 없으면 그만둔다(초).</summary>
    public const float GiveUpSeconds = 90f;

    /// <summary>끄면 닫은 문이 그대로 닫혀 있다.</summary>
    public static bool Enabled = true;

    private sealed class Pending
    {
        public DoorHandle Door;
        public float At;
        public float Since;
    }

    private readonly List<Pending> _pending = new List<Pending>();

    /// <summary>지금 근무 씬의 것. 없으면 null.</summary>
    public static DoorReopener Active { get; private set; }

    /// <summary>다시 열기를 기다리는 문 수(시험·검수).</summary>
    public int PendingCount
    {
        get { return _pending.Count; }
    }

    /// <summary>다시 열리는 문인지(문 이름이 Door로 시작 — 서랍·사물함·책장은 아니다).</summary>
    public static bool IsRoomDoor(DoorHandle door)
    {
        return door.IsValid && door.Owner != null && door.Owner.name.StartsWith("Door") && !door.OpensByItself && !door.ClosesByItself;
    }

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
        if (!FlowAutoInstall.IsDutyScene(scene) || FlowAutoInstall.Exists<DoorReopener>(scene)) return;
        FlowAutoInstall.CreateHost<DoorReopener>(scene, "DoorReopener (auto)");
    }

    private void OnEnable()
    {
        Active = this;
        PlayerInteractor.PlayerClosedDoor += OnClosed;
    }

    private void OnDisable()
    {
        PlayerInteractor.PlayerClosedDoor -= OnClosed;
        _pending.Clear();
        if (Active == this) Active = null;
    }

    private void OnClosed(DoorHandle door)
    {
        if (!Enabled || !IsRoomDoor(door)) return;
        Forget(door.Owner);
        _pending.Add(new Pending { Door = door, Since = Time.time, At = Time.time + Random.Range(MinDelay, MaxDelay) });
    }

    /// <summary>디버그·시험: 그 문을 방금 플레이어가 닫은 것처럼 등록한다(지연은 <paramref name="delay"/>초).</summary>
    public bool DebugRegister(DoorHandle door, float delay)
    {
        if (!IsRoomDoor(door)) return false;
        Forget(door.Owner);
        _pending.Add(new Pending { Door = door, Since = Time.time, At = Time.time + Mathf.Max(0f, delay) });
        return true;
    }

    private void Forget(Component owner)
    {
        for (int i = _pending.Count - 1; i >= 0; i--)
        {
            if (_pending[i].Door.Owner == owner) _pending.RemoveAt(i);
        }
    }

    private void Update()
    {
        if (_pending.Count == 0) return;
        if (!NightRun.IsNightActive || NightRun.IsCaptured) return;
        Camera cam = Camera.main;
        Transform player = PlayerSensors.Active != null ? PlayerSensors.Active.PlayerRoot : null;
        for (int i = _pending.Count - 1; i >= 0; i--)
        {
            Pending p = _pending[i];
            if (!p.Door.IsValid || p.Door.IsOpen || p.Door.IsLocked || Time.time - p.Since > GiveUpSeconds)
            {
                _pending.RemoveAt(i);
                continue;
            }

            if (Time.time < p.At) continue;
            Vector3 at = p.Door.Owner.transform.position;
            if (player != null && SensingRules.HorizontalDistance(player.position, at) < MinDistance) continue;
            if (cam != null && UnseenDespawn.VisibleTo(p.Door.Owner.gameObject, cam)) continue;
            _pending.RemoveAt(i);
            p.Door.Open();
            if (DirectionStage.Verbose) Debug.Log("[DoorReopener] 닫은 문이 저절로 열림 — " + p.Door.Owner.name, p.Door.Owner);
        }
    }
}

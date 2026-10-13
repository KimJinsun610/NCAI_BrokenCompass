using NightDuty;
using UnityEngine;

/// <summary>
/// 104차(사용자: 「소년 교실 나가려 해도 놀람 연출」) — 머리를 박는 소년(<see cref="ProgramCatalog.BoyBang"/>)이 있는 동안 플레이어가 교실을 나서면
/// 한 번 덮친다(<see cref="ViolationScare"/> — 끝까지 지켜봤을 때와 같은 놀람 → 소년 증발). 교실 안에 한 번은 있었어야 하고, 붙인 뒤 <see cref="GraceSeconds"/>초는 보지 않는다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BoyExitScare : MonoBehaviour
{
    /// <summary>붙인 직후 이만큼(초)은 나가도 덮치지 않는다(교실 문턱에서 앉는 것을 본 경우).</summary>
    public const float GraceSeconds = 1.0f;

    private float _since;
    private bool _wasInside;
    private bool _fired;

    /// <summary>이미 덮쳤는지(검수용).</summary>
    public bool Fired
    {
        get { return _fired; }
    }

    /// <summary>단다(이미 있으면 그것).</summary>
    public static BoyExitScare Attach(GameObject boy)
    {
        if (boy == null) return null;
        BoyExitScare s = boy.GetComponent<BoyExitScare>();
        if (s == null) s = boy.AddComponent<BoyExitScare>();
        s._since = 0f;
        s._fired = false;
        s._wasInside = InClassroom();
        return s;
    }

    private static bool InClassroom()
    {
        return SpaceIds.Canonical(NightRun.CurrentSpace) == SpaceId.Classroom;
    }

    private void Update()
    {
        if (_fired || !NightRun.IsNightActive || NightRun.IsCaptured || ViolationScare.Busy || ViolationScare.Holds(gameObject)) return;
        _since += Time.deltaTime;
        bool inside = InClassroom();
        if (inside)
        {
            _wasInside = true;
            return;
        }

        if (!_wasInside || _since < GraceSeconds) return;
        GameObject boy = gameObject;
        if (ViolationScare.Play(boy, "mob.boy.stand", ProgramCatalog.BoyBang, () => ViolationScare.BoyFlee(boy), true))
        {
            _fired = true;
            Debug.Log("[소년] 교실을 나가려 함 — 덮침");
        }
    }
}

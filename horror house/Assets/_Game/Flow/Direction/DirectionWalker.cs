using UnityEngine;

/// <summary>
/// 대역을 두 점 사이로 걷게 한다(2026-10-01 — 화장실 소녀가 옆으로 걸어 칸으로 들어가는 연출).
/// <list type="bullet">
/// <item>걷기 클립은 빌더가 「제자리 걸음」으로 복사해 둔 것이다(루트 이동을 뺐다). 이동은 이 컴포넌트가 하고,
/// 애니메이터 속도를 이동 속도에 맞춰 발이 미끄러지지 않게 한다.</item>
/// <item>끝점에 닿으면 사라진다(칸 문 안으로 들어간 것처럼). 문은 열지 않는다 — 연출로 문을 열면
/// <c>DoorAutoOpenObserved</c>가 나가 H2(열린 문 유지)가 엉뚱하게 걸린다.</item>
/// </list>
/// </summary>
[DisallowMultipleComponent]
public sealed class DirectionWalker : MonoBehaviour
{
    private Vector3 _from;
    private Vector3 _to;
    private float _speed;
    private float _t;
    private float _length;
    private bool _vanish;

    /// <summary>클립 원래 속도에서의 걸음 속도(m/s, 대역 크기 반영). 빌더가 프리팹에 적는다. 0이면 애니메이터 속도를 건드리지 않는다.</summary>
    [SerializeField, Min(0f)] private float naturalSpeed;

    /// <summary>끝났는가.</summary>
    public bool Arrived { get; private set; }

    /// <summary>빌더가 쓴다.</summary>
    public void SetNaturalSpeed(float metersPerSecond)
    {
        naturalSpeed = Mathf.Max(0f, metersPerSecond);
    }

    /// <summary>걷기 시작.</summary>
    public void Walk(Vector3 from, Vector3 to, float speed, bool vanishAtEnd)
    {
        _from = from;
        _to = to;
        _speed = Mathf.Max(0.05f, speed);
        _length = Vector3.Distance(new Vector3(from.x, 0f, from.z), new Vector3(to.x, 0f, to.z));
        _t = 0f;
        _vanish = vanishAtEnd;
        Arrived = false;
        transform.position = from;

        Vector3 dir = to - from;
        dir.y = 0f;
        if (dir.sqrMagnitude > 1e-4f) transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);

        Animator a = GetComponentInChildren<Animator>();
        if (a != null && naturalSpeed > 0.01f) a.speed = _speed / naturalSpeed;
        enabled = true;
    }

    private void Update()
    {
        if (Arrived || _length <= 0.01f) return;
        _t += Time.deltaTime * _speed;
        float k = Mathf.Clamp01(_t / _length);
        transform.position = Vector3.Lerp(_from, _to, k);
        if (k < 1f) return;

        Arrived = true;
        if (_vanish)
        {
            foreach (Renderer r in GetComponentsInChildren<Renderer>()) r.enabled = false;
        }
    }
}

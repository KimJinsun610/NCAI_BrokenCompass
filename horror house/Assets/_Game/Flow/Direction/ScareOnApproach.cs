using System;
using NightDuty;
using UnityEngine;

/// <summary>
/// 80차 위반 놀람 방아쇠 — 몹에 너무 가까이 가면(<see cref="Radius"/>m) 한 번 <see cref="ViolationScare"/>를 연다.
/// 화장실 소녀(칸으로 걸어 들어가는 중에만) · 노란 얼굴(문 밖에 선 동안).
/// </summary>
[DisallowMultipleComponent]
public sealed class ScareOnApproach : MonoBehaviour
{
    private float _radius;
    private bool _onlyWhileWalking;
    private string _face;
    private string _mobId;
    private Action<GameObject> _flee;
    private bool _fired;

    /// <summary>다가감으로 치는 거리(m, 수평).</summary>
    public float Radius
    {
        get { return _radius; }
    }

    /// <summary>이미 열었는지.</summary>
    public bool Fired
    {
        get { return _fired; }
    }

    /// <summary>단다.</summary>
    public static ScareOnApproach Attach(GameObject mob, float radius, bool onlyWhileWalking, string faceStandIn, string mobId, Action<GameObject> flee)
    {
        if (mob == null) return null;
        ScareOnApproach s = mob.GetComponent<ScareOnApproach>();
        if (s == null) s = mob.AddComponent<ScareOnApproach>();
        s._radius = radius;
        s._onlyWhileWalking = onlyWhileWalking;
        s._face = faceStandIn;
        s._mobId = mobId;
        s._flee = flee;
        s._fired = false;
        return s;
    }

    private void Update()
    {
        if (_fired || !NightRun.IsNightActive || NightRun.IsCaptured || ViolationScare.Busy) return;
        if (_onlyWhileWalking)
        {
            DirectionWalker w = GetComponent<DirectionWalker>();
            if (w == null || !w.Walking) return;
        }

        Camera cam = Camera.main;
        if (cam == null) return;
        Vector3 a = cam.transform.position;
        Vector3 b = transform.position;
        a.y = 0f;
        b.y = 0f;
        if (Vector3.Distance(a, b) > _radius) return;
        _fired = true;
        GameObject self = gameObject;
        if (!ViolationScare.Play(self, _face, _mobId, () => { if (_flee != null) _flee(self); })) _fired = false;
    }
}

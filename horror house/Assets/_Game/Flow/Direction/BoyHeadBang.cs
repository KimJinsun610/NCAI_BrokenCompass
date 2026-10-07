using UnityEngine;

/// <summary>
/// 52차 소년 머리 박기(민: 「C2 교실의 _? 는 무시하십시오 — 앉은 소년을 3초 이상 바라보면 머리를 책상에 박는 연출로」).
/// 앉은 소년 대역(<c>mob.boy</c>, 애니메이터 없음)의 척추·가슴·목 뼈를 앞으로 꺾어 책상에 세 번 박고, 마지막에는 엎드린 채 멈춘다.
/// 박을 때마다 머리 부딪는 소리(<c>E.BoyBang.headbang</c>, 복도까지 들리게 크게). 대역이 거둬지면 함께 사라진다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BoyHeadBang : MonoBehaviour
{
    /// <summary>박는 횟수.</summary>
    public const int Bangs = 3;

    /// <summary>앞으로 꺾는 최대 각(°, 척추·가슴·목 합).</summary>
    public const float DownDegrees = 48f;

    private const float DownTime = 0.09f;
    private const float HoldTime = 0.08f;
    private const float UpTime = 0.42f;
    private const float PauseTime = 0.22f;
    private const float RestDegrees = 8f;

    private static readonly string[] BoneNames = { "Spine", "Chest", "Neck" };
    private static readonly float[] Weights = { 0.45f, 0.3f, 0.25f };

    private Transform[] _bones;
    private Quaternion[] _base;
    private float _start;
    private int _played;

    /// <summary>머리 박기를 시작한다(이미 하고 있으면 그대로).</summary>
    public static BoyHeadBang Play(GameObject boy)
    {
        if (boy == null) return null;
        BoyHeadBang b = boy.GetComponent<BoyHeadBang>();
        if (b == null) b = boy.AddComponent<BoyHeadBang>();
        return b;
    }

    private void Awake()
    {
        _bones = new Transform[BoneNames.Length];
        _base = new Quaternion[BoneNames.Length];
        foreach (Transform t in GetComponentsInChildren<Transform>(true))
        {
            for (int i = 0; i < BoneNames.Length; i++)
            {
                if (_bones[i] == null && t.name == BoneNames[i])
                {
                    _bones[i] = t;
                    _base[i] = t.localRotation;
                }
            }
        }

        _start = Time.time;
    }

    private float Cycle
    {
        get { return DownTime + HoldTime + UpTime + PauseTime; }
    }

    private void LateUpdate()
    {
        float t = Time.time - _start;
        int index = Mathf.FloorToInt(t / Cycle);
        float a;
        if (index >= Bangs)
        {
            a = DownDegrees * 0.85f;   // 엎드린 채 멈춤
        }
        else
        {
            float c = t - index * Cycle;
            if (c < DownTime) a = Mathf.Lerp(RestDegrees, DownDegrees, EaseIn(c / DownTime));
            else if (c < DownTime + HoldTime) a = DownDegrees;
            else if (c < DownTime + HoldTime + UpTime) a = Mathf.Lerp(DownDegrees, RestDegrees, EaseOut((c - DownTime - HoldTime) / UpTime));
            else a = RestDegrees;

            if (c >= DownTime && _played <= index)
            {
                _played = index + 1;
                Thud(index);
            }
        }

        for (int i = 0; i < _bones.Length; i++)
        {
            if (_bones[i] == null) continue;
            _bones[i].localRotation = _base[i] * Quaternion.Euler(a * Weights[i], 0f, 0f);
        }
    }

    private void Thud(int index)
    {
        Vector3 at = _bones[_bones.Length - 1] != null ? _bones[_bones.Length - 1].position : transform.position + Vector3.up;
        // 복도까지 들리게 — 최소 거리 8m, 3D 60%.
        DirectionStage.PlaySound(NightDuty.ProgramCatalog.BoyBang + ".headbang", at, 8f, 0.6f);
        if (index == 0) EncounterImpact.PlayTier(EncounterImpact.Tier.Strong);
    }

    private static float EaseIn(float x)
    {
        x = Mathf.Clamp01(x);
        return x * x;
    }

    private static float EaseOut(float x)
    {
        x = Mathf.Clamp01(x);
        return 1f - (1f - x) * (1f - x);
    }
}

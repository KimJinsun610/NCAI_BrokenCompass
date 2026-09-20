using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 손가락이 아주 조금씩 움직이게 한다. 쥔 포즈는 그대로 두고 그 위에 미세한 각도만 얹는다.
///
/// 애니메이터를 쓰지 않는 이유: 포즈를 애니메이터로 잡으면 에디터에서 맞춘 손 모양이 덮여 버린다.
/// 여기서는 <b>시작할 때의 뼈 각도를 기준</b>으로 잡고, 손가락마다 다른 위상의 사인파를 더한다.
/// 위상이 달라서 손가락이 따로따로 움직이는 것처럼 보인다.
/// </summary>
[DisallowMultipleComponent]
public class HandIdleMotion : MonoBehaviour
{
    [Header("대상")]
    [Tooltip("비워 두면 아래 이름으로 자식에서 찾는다.")]
    public List<Transform> bones = new List<Transform>();
    [Tooltip("손가락 첫 마디(손등 쪽). 굽힘의 대부분을 담당한다.")]
    public string[] boneNames = { "BoneIndexBase", "BoneMiddleBase", "BoneRingBase", "BonePinkyBase", "Bone004" };
    [Tooltip("둘째 마디. 첫 마디와 같이 굽으면 손가락이 실제로 말리는 것처럼 보인다.")]
    public string[] midBoneNames = { "BoneIndexMid", "BoneMiddleMid", "BoneRingMiddle", "BonePinkyMid", "Bone005" };
    [Tooltip("둘째 마디를 첫 마디의 몇 배로 움직일지.")]
    [Range(0f, 1.5f)] public float midWeight = 0.7f;

    [Header("세기")]
    [Tooltip("손가락이 구부러졌다 펴지는 각도. 2도만 넘어도 꼼지락거리는 티가 난다.")]
    [Range(0f, 5f)] public float degrees = 1.2f;
    [Tooltip("클수록 빨리 움직인다.")]
    public float speed = 0.8f;
    [Tooltip("손가락마다 시작 위상을 얼마나 벌릴지. 0이면 다섯 손가락이 똑같이 움직인다.")]
    public float phaseSpread = 1.7f;
    [Tooltip("구부러지는 축. 이 손 리그는 Y축이 손바닥 쪽으로 말리는 축이다(X축은 비틀기라 거의 안 보인다).")]
    public Vector3 axis = Vector3.up;

    private readonly List<Quaternion> _baseRotations = new List<Quaternion>();
    private readonly List<float> _weights = new List<float>();   // 마디별 세기
    private readonly List<int> _fingerIndex = new List<int>();   // 같은 손가락이면 같은 박자를 쓴다
    private float _seed;

    private void Start()
    {
        if (bones.Count == 0) FindBones();

        _baseRotations.Clear();
        for (int i = 0; i < bones.Count; i++)
        {
            _baseRotations.Add(bones[i] != null ? bones[i].localRotation : Quaternion.identity);

            // 인스펙터에서 직접 넣은 경우를 대비해, 목록이 모자라면 기본값으로 채운다.
            if (_weights.Count <= i) _weights.Add(1f);
            if (_fingerIndex.Count <= i) _fingerIndex.Add(i);
        }

        _seed = Random.value * 50f;
    }

    private void FindBones()
    {
        Transform[] all = GetComponentsInChildren<Transform>(true);

        AddByNames(all, boneNames, 1f);
        if (midWeight > 0.001f) AddByNames(all, midBoneNames, midWeight);

        if (bones.Count == 0)
        {
            Debug.LogWarning("[HandIdleMotion] 움직일 뼈를 찾지 못했습니다. Bones 칸에 직접 넣어 주세요.");
        }
    }

    private void AddByNames(Transform[] all, string[] names, float weight)
    {
        for (int i = 0; i < names.Length; i++)
        {
            for (int j = 0; j < all.Length; j++)
            {
                if (all[j].name != names[i]) continue;
                bones.Add(all[j]);
                _weights.Add(weight);
                _fingerIndex.Add(i);   // 첫 마디와 둘째 마디가 같은 박자로 움직여야 한 손가락처럼 보인다
                break;
            }
        }
    }

    private void LateUpdate()
    {
        if (degrees <= 0.001f) return;

        float t = Time.unscaledTime * speed;

        for (int i = 0; i < bones.Count; i++)
        {
            Transform bone = bones[i];
            if (bone == null || i >= _baseRotations.Count) continue;

            int finger = i < _fingerIndex.Count ? _fingerIndex[i] : i;
            float weight = i < _weights.Count ? _weights[i] : 1f;

            // 사인파에 노이즈를 섞어서 규칙적으로 보이지 않게 한다.
            float phase = t + finger * phaseSpread;
            float wave = Mathf.Sin(phase) * 0.6f + (Mathf.PerlinNoise(_seed + finger, phase * 0.5f) * 2f - 1f) * 0.4f;

            bone.localRotation = _baseRotations[i] * Quaternion.AngleAxis(wave * degrees * weight, axis);
        }
    }
}

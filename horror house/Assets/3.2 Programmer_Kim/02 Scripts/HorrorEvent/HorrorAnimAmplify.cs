using System;
using UnityEngine;

/// <summary>
/// 원래 애니메이션의 움직임을 <b>키운다</b> — 매 프레임 Animator가 쓴 뼈 회전을, 그 클립의 평균 자세에서 떨어진 만큼
/// <see cref="strength"/>배로 더 밀어낸다(같은 동작이 더 크게). 1이면 원래 그대로.
/// <para>
/// 평균 자세는 에디터 빌더가 클립을 고르게 샘플해 미리 구워 둔다(<see cref="Entry.reference"/>) — 런타임에 클립을 샘플하지 않는다.
/// <see cref="HorrorTreeSway"/>보다 먼저 돈다(실행 순서 -50): 키운 동작 위에 흔들림이 얹힌다.
/// Animator가 이번 프레임에 안 쓴 뼈(화면 밖 컬링)는 지난번 자기가 쓴 값이면 원래 값을 기준으로 삼아 누적되지 않는다.
/// </para>
/// </summary>
[DefaultExecutionOrder(-50)]
public class HorrorAnimAmplify : MonoBehaviour
{
    [Serializable]
    public class Entry
    {
        public Transform bone;
        [Tooltip("클립의 평균 자세(로컬 회전) — 빌더가 굽는다")]
        public Quaternion reference = Quaternion.identity;

        [NonSerialized] public Quaternion lastBase;
        [NonSerialized] public Quaternion lastWritten;
        [NonSerialized] public bool written;
    }

    [Tooltip("움직임 배율. 1 = 원래 애니메이션, 2 = 평균 자세에서 두 배 멀리. 너무 크면 관절이 꺾여 보인다.")]
    [Range(1f, 4f)] public float strength = 1.8f;

    [SerializeField] private Entry[] bones = new Entry[0];

    /// <summary>빌더용 — 뼈와 평균 자세를 채운다.</summary>
    public void SetBones(Entry[] entries)
    {
        bones = entries;
    }

    private void LateUpdate()
    {
        if (Time.deltaTime <= 0f || strength <= 1.001f) return;
        foreach (Entry e in bones)
        {
            if (e == null || e.bone == null) continue;
            Quaternion current = e.bone.localRotation;
            Quaternion basePose = e.written && current == e.lastWritten ? e.lastBase : current;
            // 평균 → 지금 자세의 차이를 strength배로(구면 외삽)
            Quaternion result = Quaternion.SlerpUnclamped(e.reference, basePose, strength);
            e.bone.localRotation = result;
            e.lastBase = basePose;
            e.lastWritten = result;
            e.written = true;
        }
    }

    private void OnDisable()
    {
        foreach (Entry e in bones)
        {
            if (e == null || e.bone == null || !e.written) continue;
            if (e.bone.localRotation == e.lastWritten) e.bone.localRotation = e.lastBase;
            e.written = false;
        }
    }
}

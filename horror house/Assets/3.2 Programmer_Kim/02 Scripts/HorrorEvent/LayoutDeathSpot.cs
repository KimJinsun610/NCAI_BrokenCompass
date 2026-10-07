using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 배치 축 붙잡힘 컷신(<see cref="LayoutDeathCutscene"/>)의 목적지 — 사람 나무가 설 자리.
/// <para>씬의 복도 출입문 쪽 바닥에 놓는다. 위치 = 나무 밑동(바닥), 여러 개 놓아도 된다.
/// 컷신은 그중 <b>플레이어가 실제로 걸어갈 수 있고(벽·사물을 피하는 길) 가장 가까운</b> 자리로 간다.</para>
/// <para>플레이어가 이 자리에서 <see cref="LayoutDeathCutscene"/>의 금지 반경(기본 2m) 안에 있으면 이 자리는 쓰지 않는다.
/// 쓸 수 있는 자리가 하나도 없으면 컷신은 재생되지 않는다.</para>
/// </summary>
[DisallowMultipleComponent]
public class LayoutDeathSpot : MonoBehaviour
{
    [Tooltip("켜면 나무가 플레이어가 다가오는 쪽을 보게 돌린다. 끄면 이 표식의 +Z 방향을 본다.")]
    public bool faceArrival = true;

    private static readonly List<LayoutDeathSpot> all = new List<LayoutDeathSpot>();

    /// <summary>씬에 켜져 있는 자리들.</summary>
    public static IReadOnlyList<LayoutDeathSpot> All { get { return all; } }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatic()
    {
        all.Clear();
    }

    private void OnEnable()
    {
        if (!all.Contains(this)) all.Add(this);
    }

    private void OnDisable()
    {
        all.Remove(this);
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Vector3 p = transform.position;
        Gizmos.color = new Color(0.55f, 0.2f, 0.1f, 0.9f);
        Gizmos.DrawWireCube(p + Vector3.up * 1.95f, new Vector3(1.2f, 3.9f, 1.2f));   // 사람 나무 크기(약 3.9m)
        Gizmos.DrawLine(p + Vector3.up * 0.05f, p + Vector3.up * 0.05f + transform.forward * 1.2f);
        UnityEditor.Handles.color = new Color(1f, 0.3f, 0.2f, 0.8f);
        UnityEditor.Handles.DrawWireDisc(p + Vector3.up * 0.03f, Vector3.up, LayoutDeathCutscene.DefaultBlockRadius);
        UnityEditor.Handles.Label(p + Vector3.up * 4.1f, "배치 사망 자리\n(빨간 원 안이면 이 자리는 안 씀)");
    }
#endif
}

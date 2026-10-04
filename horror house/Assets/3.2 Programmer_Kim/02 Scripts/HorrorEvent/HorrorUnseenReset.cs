using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 연출이 끝난 뒤, 플레이어가 대상을 <see cref="unseenSeconds"/> 동안 보지 않으면 Timeline을 처음 상태로 되돌린다.
/// <para>
/// 「본다」 = 대상 모델이 화면(시야) 안에 있고, 카메라에서 가운데까지 가린 것이 없는 상태. 화면 가운데를 겨누는지는 보지 않는다 —
/// 살짝 시선을 돌린 것만으로 되돌아가면 「안 볼 때 바뀐다」가 아니라 「눈앞에서 바뀐다」가 된다.
/// <see cref="alsoWatched"/>에 넣은 데칼(천장 얼룩 등)이 보이는 동안에도 본 것으로 친다.
/// </para>
/// <para>
/// 되돌리기 = Director를 멈춘 채 0초 자세를 쓰는 것. Stop을 쓰지 않는 이유: Activation 트랙의 Post-playback 상태가 적용되어
/// 0초 자세가 아닌 상태로 남는다. 연출이 남긴 오브젝트(<see cref="cleanupNamePrefixes"/>, 데칼·튐 등)는 이 자리 근처에서 지운다.
/// </para>
/// <para>한 번만 되돌린다. 연출(<see cref="HorrorEvent"/>)이 Play Once면 다시 재생되지 않는다.</para>
/// </summary>
public class HorrorUnseenReset : MonoBehaviour
{
    [Tooltip("지켜볼 연출. 비워 두면 같은 오브젝트에서 찾는다.")]
    [SerializeField] private HorrorEvent target;

    [Tooltip("되돌릴 Timeline의 Director. 비워 두면 같은 오브젝트에서 찾는다.")]
    [SerializeField] private PlayableDirector director;

    [Header("보고 있는가")]
    [Tooltip("보고 있어야 하는 모델. 이 오브젝트(와 자식)의 Renderer가 화면 안에 있으면 본 것으로 친다.")]
    [SerializeField] private Transform watchedModel;

    [Tooltip("이 데칼이 화면 안에 보이는 동안에도 본 것으로 친다(예: 천장 얼룩).")]
    [SerializeField] private DecalProjector[] alsoWatched = new DecalProjector[0];

    [Tooltip("이 거리(m)보다 멀면 보고 있지 않은 것으로 친다.")]
    [SerializeField, Min(0.5f)] private float maxDistance = 15f;

    [Tooltip("이 시간(초) 동안 계속 보지 않으면 되돌린다.")]
    [SerializeField, Min(0f)] private float unseenSeconds = 0.5f;

    [Header("정리")]
    [Tooltip("이 이름으로 시작하는 오브젝트를 되돌릴 때 지운다(연출 중 스폰된 자국·튐).")]
    [SerializeField] private string[] cleanupNamePrefixes = { "Decal_WetSpot_DarkRed", "PS_Splash_DarkRed" };

    [Tooltip("이 반경(m) 안의 것만 지운다.")]
    [SerializeField, Min(0.5f)] private float cleanupRadius = 4f;

    private readonly Plane[] planes = new Plane[6];
    private readonly List<Renderer> renderers = new List<Renderer>();
    private float unseenTime;

    /// <summary>되돌렸는지</summary>
    public bool HasReset { get; private set; }

    private void Awake()
    {
        if (target == null) target = GetComponent<HorrorEvent>();
        if (director == null) director = GetComponent<PlayableDirector>();
        if (watchedModel != null) watchedModel.GetComponentsInChildren(true, renderers);

        if (target == null || director == null || watchedModel == null)
            Debug.LogWarning($"[HorrorUnseenReset] {name}: Target · Director · Watched Model 중 비어 있는 것이 있습니다.", this);
    }

    private void Update()
    {
        if (HasReset || target == null || director == null) return;
        if (!target.HasPlayed || target.IsPlaying) { unseenTime = 0f; return; }   // 연출이 끝난 뒤에만

        float dt = Time.deltaTime;
        if (dt <= 0f) return;   // 일시정지

        Camera cam = Camera.main;
        if (cam == null) return;

        if (IsWatched(cam)) { unseenTime = 0f; return; }

        unseenTime += dt;
        if (unseenTime >= unseenSeconds) ResetToStart();
    }

    private bool IsWatched(Camera cam)
    {
        GeometryUtility.CalculateFrustumPlanes(cam, planes);
        Vector3 eye = cam.transform.position;

        // 모델: 보이는 Renderer가 하나라도 화면 안에 있고 가운데까지 가리지 않았으면 본다
        foreach (Renderer r in renderers)
        {
            if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
            Bounds b = r.bounds;
            if (!GeometryUtility.TestPlanesAABB(planes, b)) continue;
            if (Vector3.Distance(eye, b.center) > maxDistance) continue;

            if (!Physics.Linecast(eye, b.center, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore)) return true;
            if (hit.collider.transform.IsChildOf(watchedModel)) return true;
        }

        // 데칼: 투사 상자가 화면 안에 있고, 가운데 근처(천장 면)까지 가리지 않았으면 본다
        foreach (DecalProjector decal in alsoWatched)
        {
            if (decal == null || !decal.isActiveAndEnabled || decal.fadeFactor <= 0.01f) continue;
            Vector3 center = decal.transform.TransformPoint(decal.pivot);
            Vector3 size = Vector3.Scale(decal.size, decal.transform.lossyScale);
            Bounds b = new Bounds(center, decal.transform.rotation * size);
            b.size = new Vector3(Mathf.Abs(b.size.x), Mathf.Abs(b.size.y), Mathf.Abs(b.size.z));
            if (!GeometryUtility.TestPlanesAABB(planes, b)) continue;

            float dist = Vector3.Distance(eye, center);
            if (dist > maxDistance) continue;

            // 데칼 중심이 천장 면에서 조금 떨어져 있을 수 있다(천장 높이가 자리마다 다름). 그만큼은 막혀도 보이는 것으로 친다.
            float slack = size.z * 0.5f + 0.1f;
            if (!Physics.Linecast(eye, center, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore) || hit.distance >= dist - slack) return true;
        }

        return false;
    }

    /// <summary>Timeline을 0초 자세로 되돌린다. 보지 않을 때만 불린다.</summary>
    public void ResetToStart()
    {
        HasReset = true;
        unseenTime = 0f;

        director.Pause();        // Hold로 끝 자세를 쓰고 있던 그래프를 세운다(Stop은 Post-playback 상태를 남긴다)
        director.time = 0;
        director.Evaluate();     // 0초 자세: 처음 물·물방울, 붉은 것 꺼짐

        Cleanup();
    }

    private void Cleanup()
    {
        if (cleanupNamePrefixes == null || cleanupNamePrefixes.Length == 0) return;

        Vector3 origin = transform.position;
        foreach (Transform t in FindObjectsByType<Transform>(FindObjectsSortMode.None))
        {
            if (t == null || t.IsChildOf(transform)) continue;
            if ((t.position - origin).sqrMagnitude > cleanupRadius * cleanupRadius) continue;

            foreach (string prefix in cleanupNamePrefixes)
            {
                if (!string.IsNullOrEmpty(prefix) && t.name.StartsWith(prefix))
                {
                    Destroy(t.gameObject);
                    break;
                }
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (watchedModel == null) return;
        Gizmos.color = new Color(0.2f, 0.8f, 1f);
        Gizmos.DrawWireSphere(watchedModel.position, 0.2f);
    }
}

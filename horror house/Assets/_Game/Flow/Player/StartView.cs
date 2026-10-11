using System.Reflection;
using UnityEngine;

/// <summary>
/// 출근 시선(72차, 민: 「시작할 때 CCTV를 바라보며 시작하게 해줘」) — 근무 씬의 플레이어는 경비실 CCTV 모니터 화면을 보고 선다.
/// <list type="bullet">
/// <item>씬의 시작 자리(33.73, 46.66)는 모니터 <b>옆</b>이라 그 자리에서 돌리면 모니터 옆면만 보였다(QA 캡처) — 화면 앞 <see cref="StandDistances"/>m(책상 건너편)의
/// 빈 바닥(플레이어 캡슐이 겹치지 않고 눈에서 화면 가운데까지 가림 없음)으로 옮긴다. 못 찾으면 제자리에서 돌리기만 한다.</item>
/// <item>플레이어 몸은 화면 쪽으로 돌리고(좌우), 고개(FPController의 비공개 <c>xRot</c> + 카메라 로컬 회전)는 화면 가운데를 향해 숙인다(<see cref="MinPitch"/>~<see cref="MaxPitch"/>).</item>
/// <item><see cref="CaptureDirector"/>가 출근 자리를 적기 직전에 부른다 — 붙잡힌 뒤 재시작·피날레 강제 복귀도 같은 시선으로 돌아온다(<see cref="ApplyPitch"/>).</item>
/// <item>김진선님 FPController·씬 파일은 고치지 않는다(씬의 시작 방향 y 180°는 모니터를 등졌다).</item>
/// </list>
/// </summary>
public static class StartView
{
    /// <summary>고개를 드는 한도(도, 음수 = 위).</summary>
    public const float MinPitch = -10f;

    /// <summary>고개를 숙이는 한도(도).</summary>
    public const float MaxPitch = 30f;

    /// <summary>화면 앞에 설 자리 후보 — 화면 가운데에서 화면이 향한 쪽으로 이 거리(m, 수평). 앞에서부터 고른다.</summary>
    public static readonly float[] StandDistances = { 1.6f, 1.4f, 1.8f, 2.0f };

    /// <summary>눈높이(바닥에서, m) — 61차 PlayerBody 키 1.95m의 눈.</summary>
    private const float EyeHeight = 1.70f;

    private static FieldInfo s_xRot;

    /// <summary>
    /// 플레이어를 CCTV 화면 쪽으로 돌린다. 모니터를 못 찾으면 아무것도 안 하고 false. <paramref name="pitch"/> = 정한 고개 각도(도).
    /// </summary>
    public static bool FaceCctv(FPController player, out float pitch)
    {
        pitch = 0f;
        if (player == null) return false;
        Vector3 center, outward;
        if (!CctvSystem.TryFindScreen(out center, out outward)) return false;

        Transform body = player.transform;
        Vector3 stand;
        if (FindStandSpot(player, center, outward, out stand))
        {
            body.position = stand;
            Rigidbody moved = player.GetComponent<Rigidbody>();
            if (moved != null) moved.position = stand;
        }

        Camera cam = player.GetComponentInChildren<Camera>(true);
        Vector3 eye = cam != null ? cam.transform.position : body.position;
        float feetNow;
        if (FeetY(player, out feetNow)) eye.y = Mathf.Max(eye.y, feetNow + EyeHeight);   // 키 보정(PlayerBody)이 아직 안 됐어도 최종 눈높이로
        Vector3 flat = center - eye;
        flat.y = 0f;
        if (flat.sqrMagnitude < 0.0001f) flat = -outward;
        flat.y = 0f;
        if (flat.sqrMagnitude < 0.0001f) return false;

        body.rotation = Quaternion.LookRotation(flat.normalized, Vector3.up);
        Rigidbody rb = player.GetComponent<Rigidbody>();
        if (rb != null) rb.rotation = body.rotation;

        float drop = eye.y - center.y;
        pitch = Mathf.Clamp(Mathf.Atan2(drop, flat.magnitude) * Mathf.Rad2Deg, MinPitch, MaxPitch);
        ApplyPitch(player, pitch);
        return true;
    }

    /// <summary>화면 앞 빈 바닥 — 찾으면 플레이어 루트가 설 위치(높이는 지금 그대로).</summary>
    private static bool FindStandSpot(FPController player, Vector3 center, Vector3 outward, out Vector3 stand)
    {
        stand = player.transform.position;
        CapsuleCollider capsule = player.GetComponent<CapsuleCollider>();
        float feet;
        if (capsule == null || !FeetY(player, out feet)) return false;
        Vector3 flat = new Vector3(outward.x, 0f, outward.z);
        if (flat.sqrMagnitude < 0.0001f) return false;
        flat.Normalize();
        float radius = capsule.radius;
        float top = feet + Mathf.Max(capsule.height, 1.95f) - radius;

        for (int i = 0; i < StandDistances.Length; i++)
        {
            Vector3 at = new Vector3(center.x, 0f, center.z) + flat * StandDistances[i];
            Vector3 a = new Vector3(at.x, feet + 0.32f + radius, at.z);   // 턱(0.3m) 위부터
            Vector3 b = new Vector3(at.x, top, at.z);
            if (Blocked(Physics.OverlapCapsule(a, b, radius, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore), player.transform)) continue;
            Vector3 eye = new Vector3(at.x, feet + EyeHeight, at.z);
            Vector3 d = center - eye;
            if (Physics.Raycast(eye, d.normalized, d.magnitude - 0.15f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
            stand = new Vector3(at.x, player.transform.position.y, at.z);
            return true;
        }

        return false;
    }

    private static bool Blocked(Collider[] hits, Transform self)
    {
        for (int i = 0; i < hits.Length; i++)
        {
            if (!hits[i].transform.IsChildOf(self)) return true;
        }

        return false;
    }

    private static bool FeetY(FPController player, out float feet)
    {
        feet = player.transform.position.y;
        CapsuleCollider capsule = player.GetComponent<CapsuleCollider>();
        if (capsule == null) return false;
        float scale = player.transform.lossyScale.y;
        feet = player.transform.position.y + (capsule.center.y - capsule.height * 0.5f) * scale;
        return true;
    }

    /// <summary>고개 각도를 맞춘다(FPController가 다음 프레임에 덮어쓰지 않게 그 비공개 값까지).</summary>
    public static void ApplyPitch(FPController player, float pitch)
    {
        if (player == null) return;
        if (s_xRot == null) s_xRot = typeof(FPController).GetField("xRot", BindingFlags.Instance | BindingFlags.NonPublic);
        if (s_xRot != null) s_xRot.SetValue(player, pitch);
        Camera cam = player.GetComponentInChildren<Camera>(true);
        if (cam != null && cam.transform.parent != null) cam.transform.localEulerAngles = new Vector3(pitch, 0f, 0f);
    }
}

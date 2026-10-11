using System;
using System.Collections;
using System.Collections.Generic;
using NightDuty;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 80차 위반 놀람 연출(민: 「수칙이나 대처를 어겼을 때 — 플레이어를 잠시 붙잡고, 카메라 흔들림과 함께 몹의 얼굴이 화면을 꽉 채우고, 몹은 도망가고 플레이어는 넘어졌다가 일어나는 플로우」).
/// <list type="number">
/// <item>입력 잠금(김진선님 <c>FPController</c>를 잠시 끔) — 몹 얼굴(같은 대역을 하나 더 세움)이 눈앞 <see cref="FaceDistance"/>m에서 화면을 채우고 화면이 세게 흔들린다(<see cref="FaceSeconds"/>초).</item>
/// <item>얼굴이 뒤로 빠지며 사라지고, 원래 몹은 몹마다 다른 「도망」(<paramref name="onFlee"/>)을 한다.</item>
/// <item>플레이어가 넘어진다 — 눈높이가 바닥 가까이까지 떨어지며 옆으로 기울고, 바닥에 닿을 때 툭 흔들림·헉 소리·거친 숨. 잠시 뒤 일어나 원래 자세로.</item>
/// </list>
/// 83차: 소년·소녀·노란 얼굴 셋(인체 모형 놀람은 폐기 — 김진선님 사망 컷신으로). 연출 동안 태블릿·손(김진선님 <c>PlayerTablet</c>)을 끄고 일어나면 다시 켠다.
/// 놀람 모델은 원래 모델을 숨기는 그 프레임에 그 자리에서 나타나 덮쳐 오고, 그 자리로 물러난 뒤 원래 모델로 바뀐다 — 둘이 한 장면에 함께 보이지 않는다.
/// 붙잡힘이 일어나면(그쪽이 우선) 하지 않는다. 한 번에 하나만. 판정·축에는 아무것도 하지 않는다 — 벌점은 부르는 쪽(수칙 판정)이 이미 매겼다.
/// </summary>
[DisallowMultipleComponent]
public sealed class ViolationScare : MonoBehaviour
{
    public const float FaceDistance = 0.34f;
    public const float FaceSeconds = 0.6f;   // 82차(민: 「지속시간도 조금 줄여줘」): 0.75 → 0.6
    public const float FaceShakeDegrees = 2.5f;   // 82차(민: 「카메라 흔들림을 조금 줄여주고」): 4.5 → 2.5
    public const float PullBackSeconds = 0.15f;
    public const float FallSeconds = 0.38f;
    public const float DownSeconds = 1f;   // 82차: 1.3 → 1
    public const float RiseSeconds = 0.75f;   // 82차: 0.9 → 0.75
    public const float EyeAboveFloorDown = 0.35f;
    public const float FallRollDegrees = 28f;
    public const float FallPitchUpDegrees = 18f;

    /// <summary>얼굴이 나올 때 소리(연출 소리 표).</summary>
    public const string StingerSound = "stinger.strong";

    /// <summary>얼굴 가운데 = 머리뼈 + 이만큼 위(m).</summary>
    public const float FaceAboveHeadBone = 0.09f;

    /// <summary>놀람 모델이 원래 몹 자리에서 눈앞으로 덮쳐 오는 시간(초).</summary>
    public const float LungeSeconds = 0.1f;

    private static ViolationScare s_host;

    /// <summary>지금 연출 중인지.</summary>
    public static bool Busy { get; private set; }

    private static GameObject s_mob;

    /// <summary>83차: 지금 놀람 연출이 이 몹을 붙잡고 있는지(숨겨 둔 동안 「안 보인다」고 지워지지 않게 — <c>UnseenDespawn</c>가 본다).</summary>
    public static bool Holds(GameObject go)
    {
        return Busy && go != null && s_mob == go;
    }

    /// <summary>연출을 시작할 때(몹 ID). 시험·로그.</summary>
    public static event Action<string> Played;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Install()
    {
        s_host = null;
        Busy = false;
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
        if (!FlowAutoInstall.IsDutyScene(scene) || FlowAutoInstall.Exists<ViolationScare>(scene)) return;
        FlowAutoInstall.CreateHost<ViolationScare>(scene, "ViolationScare (auto)");
    }

    private void OnEnable()
    {
        s_host = this;
    }

    private void OnDisable()
    {
        if (s_host == this) s_host = null;
    }

    /// <summary>
    /// <paramref name="mob"/>의 놀람 연출을 시작한다. 83차: 원래 모델을 숨기는 그 프레임에 놀람 모델(대역 <paramref name="faceStandIn"/>)이 원래 모델의 자리에 서서
    /// 눈앞으로 덮쳐 와 화면을 채우고, 다시 그 자리로 물러난 뒤 원래 모델로 바뀌어 <paramref name="onFlee"/>(도망)를 한다 — 둘이 한 장면에 함께 있지 않다. 시작하지 못하면 false.
    /// </summary>
    public static bool Play(GameObject mob, string faceStandIn, string mobId, Action onFlee)
    {
        if (Busy || s_host == null || mob == null || !NightRun.IsNightActive || NightRun.IsCaptured) return false;
        s_host.StartCoroutine(s_host.Run(mob, faceStandIn, mobId, onFlee));
        return true;
    }

    private IEnumerator Run(GameObject mob, string faceStandIn, string mobId, Action onFlee)
    {
        Busy = true;
        s_mob = mob;
        Debug.Log("[놀람] 위반 놀람 — " + mobId);
        if (Played != null) Played(mobId);

        Camera cam = Camera.main;
        FPController fp = FindAnyObjectByType<FPController>();
        bool fpWas = fp != null && fp.enabled;
        if (fp != null) fp.enabled = false;
        CursorLockMode cursorWas = Cursor.lockState;
        bool cursorVisibleWas = Cursor.visible;
        Cursor.lockState = CursorLockMode.Locked;   // FPController.OnDisable이 커서를 보이게 하므로 다시 숨긴다(김진선님 사망 컷신과 같음)
        Cursor.visible = false;
        Transform ct = cam != null ? cam.transform : null;
        Vector3 lp = ct != null ? ct.localPosition : Vector3.zero;
        Quaternion lr = ct != null ? ct.localRotation : Quaternion.identity;

        // 83차(민: 「놀람 연출이 진행될 때 태블릿과 손이 잠시 사라졌다가, 일어나면 다시 나오는 걸로」): 김진선님 사망 컷신과 같은 방식 — 태블릿(손 포함)을 끈다.
        PlayerTablet tablet = cam != null ? cam.GetComponentInChildren<PlayerTablet>(true) : null;
        GameObject viewmodel = tablet != null ? tablet.gameObject : null;
        bool viewmodelWas = viewmodel != null && viewmodel.activeSelf;
        if (viewmodel != null) viewmodel.SetActive(false);

        // ① 83차(민: 「놀람 연출이 진행될 때 원본 조우 모델을 대체 — 놀람 모델과 원조 조우 모델이 한 장면에 공존하면 안돼」):
        //    원래 모델을 숨기는 그 프레임에 놀람 모델이 원래 모델의 자리·방향에 서서, 눈앞 얼굴 자리까지 덮쳐 온다. 둘이 같이 보이는 프레임이 없다.
        List<Renderer> hidden = new List<Renderer>();
        HideAll(mob, hidden);
        GameObject face = ct != null ? Face(faceStandIn, ct) : null;
        Vector3 homePos = mob.transform.position;
        Quaternion homeRot = mob.transform.rotation;
        Vector3 facePos = face != null ? face.transform.position : homePos;
        Quaternion faceRot = face != null ? face.transform.rotation : homeRot;
        if (face != null) face.transform.SetPositionAndRotation(homePos, homeRot);

        Vector3 faceAt = ct != null ? ct.position + ct.forward * FaceDistance : mob.transform.position;
        DirectionStage.PlaySound(StingerSound, faceAt, 2f, 0.2f);
        BodyMeter.Startle();

        float t = 0f;
        while (t < FaceSeconds)
        {
            t += Time.deltaTime;
            HideAll(mob, hidden);   // 다른 코드가 다시 켜도 놀람 모델이 있는 동안은 숨긴 채로
            if (face != null)
            {
                float w = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / LungeSeconds));
                face.transform.SetPositionAndRotation(Vector3.LerpUnclamped(homePos, facePos, w), Quaternion.SlerpUnclamped(homeRot, faceRot, w));
            }
            if (ct != null)
            {
                float k = 1f - 0.5f * (t / FaceSeconds);
                ct.localRotation = lr * Quaternion.Euler(UnityEngine.Random.Range(-1f, 1f) * FaceShakeDegrees * k, UnityEngine.Random.Range(-1f, 1f) * FaceShakeDegrees * k, UnityEngine.Random.Range(-1f, 1f) * FaceShakeDegrees * 0.6f * k);
            }

            yield return null;
        }

        // ② 놀람 모델이 원래 자리로 물러난다 → 그 자리에서 원래 모델로 바뀌어 도망
        if (face != null)
        {
            t = 0f;
            while (t < PullBackSeconds && face != null)
            {
                t += Time.deltaTime;
                float w = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / PullBackSeconds));
                face.transform.SetPositionAndRotation(Vector3.LerpUnclamped(homePos, facePos, w), Quaternion.SlerpUnclamped(homeRot, faceRot, w));
                HideAll(mob, hidden);
                yield return null;
            }

            Destroy(face);
        }

        for (int i = 0; i < hidden.Count; i++)
        {
            if (hidden[i] != null) hidden[i].enabled = true;
        }

        s_mob = null;   // 도망부터는 원래 조우의 정리 규칙대로
        try
        {
            if (onFlee != null) onFlee();
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }

        // ③ 넘어짐 — 눈높이를 바닥 가까이로, 옆으로 기울며 위를 본다
        if (ct != null)
        {
            float drop = Mathf.Max(0f, ct.position.y - FloorY(ct.position) - EyeAboveFloorDown);
            Vector3 down = ct.parent != null ? ct.parent.InverseTransformDirection(Vector3.down) * drop : Vector3.down * drop;
            float side = UnityEngine.Random.value < 0.5f ? -1f : 1f;
            Quaternion lying = lr * Quaternion.Euler(-FallPitchUpDegrees, 0f, FallRollDegrees * side);
            t = 0f;
            while (t < FallSeconds)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / FallSeconds);
                k = k * k;
                ct.localPosition = lp + down * k;
                ct.localRotation = Quaternion.Slerp(lr, lying, k);
                yield return null;
            }

            BodyMeter.Jolt(3f);
            DirectionStage.PlaySound("capture.gasp", ct.position, 1f, 0f);
            DirectionStage.PlaySound("body.breath", ct.position, 1f, 0f);
            yield return new WaitForSeconds(DownSeconds);

            t = 0f;
            Vector3 lowPos = ct.localPosition;
            Quaternion lowRot = ct.localRotation;
            while (t < RiseSeconds)
            {
                t += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, t / RiseSeconds);
                ct.localPosition = Vector3.Lerp(lowPos, lp, k);
                ct.localRotation = Quaternion.Slerp(lowRot, lr, k);
                yield return null;
            }

            ct.localPosition = lp;
            ct.localRotation = lr;
        }

        if (viewmodel != null) viewmodel.SetActive(viewmodelWas);   // 일어나면 태블릿·손이 다시
        if (fp != null && fpWas && !NightRun.IsCaptured) fp.enabled = true;
        Cursor.lockState = cursorWas;
        Cursor.visible = cursorVisibleWas;
        if (fp != null && fp.enabled)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        s_mob = null;
        Busy = false;
    }

    private static void HideAll(GameObject mob, List<Renderer> hidden)
    {
        if (mob == null) return;
        foreach (Renderer r in mob.GetComponentsInChildren<Renderer>())
        {
            if (!r.enabled) continue;
            r.enabled = false;
            if (!hidden.Contains(r)) hidden.Add(r);
        }
    }

    private static Transform FindHead(Transform root)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            string ln = t.name.ToLowerInvariant();
            if (ln == "head" || ln.EndsWith(":head") || ln.EndsWith("_head")) return t;
        }

        return null;
    }

    /// <summary>놀람 모델 — 눈앞 얼굴 자세의 대역(머리가 카메라 앞 <see cref="FaceDistance"/>m, 카메라를 보게). 콜라이더·연출 스크립트는 뗀다.</summary>
    private static GameObject Face(string standIn, Transform ct)
    {
        if (string.IsNullOrEmpty(standIn)) return null;
        Vector3 fwd = ct.forward;
        Vector3 flat = new Vector3(fwd.x, 0f, fwd.z);
        if (flat.sqrMagnitude < 1e-4f) flat = Vector3.forward;
        flat.Normalize();
        GameObject go = StandInFactory.Create(standIn, ct.position + flat * FaceDistance + Vector3.down * 1.6f, Quaternion.LookRotation(-flat, Vector3.up), null);
        if (go == null) return null;
        go.name = "놀람 얼굴 · " + standIn;
        foreach (Collider c in go.GetComponentsInChildren<Collider>(true)) Destroy(c);
        foreach (MonoBehaviour b in go.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (b != null) b.enabled = false;
        }

        Transform head = FindHead(go.transform);
        Vector3 target = ct.position + ct.forward * FaceDistance;
        Vector3 headPos = head != null ? head.position + Vector3.up * FaceAboveHeadBone : go.transform.position + Vector3.up * 1.6f;
        go.transform.position += target - headPos;
        Vector3 axis = Vector3.Cross(-flat, -ct.forward);
        if (axis.sqrMagnitude > 1e-6f) go.transform.RotateAround(target, axis.normalized, Vector3.Angle(-flat, -ct.forward));
        TrueColor.Apply(go);
        return go;
    }

    private static float FloorY(Vector3 eye)
    {
        RaycastHit h;
        if (Physics.Raycast(eye, Vector3.down, out h, 4f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return h.point.y;
        return eye.y - 1.9f;
    }

    // ── 몹별 도망 ─────────────────────────────────────────────

    /// <summary>화장실 소녀: 들어가려던 칸 안으로 빠르게.</summary>
    public static void GirlFlee(GameObject girl)
    {
        if (girl == null) return;
        DirectionWalker w = girl.GetComponent<DirectionWalker>();
        if (w != null) w.Hurry(4f);
    }

    /// <summary>노란 얼굴: 원래 퇴장(빠르게 걸어 나가며 사라짐).</summary>
    public static void DuckFlee(GameObject duck)
    {
        if (duck == null) return;
        StandInExit exit = duck.GetComponent<StandInExit>();
        if (exit != null) exit.Leave();
        else Destroy(duck);
    }

    /// <summary>소년: 증발 — 넘어진 사이 자리에서 사라진다.</summary>
    public static void BoyFlee(GameObject boy)
    {
        if (boy == null) return;
        foreach (Renderer r in boy.GetComponentsInChildren<Renderer>()) r.enabled = false;
        foreach (Collider c in boy.GetComponentsInChildren<Collider>()) c.enabled = false;
        Destroy(boy, 0.1f);
    }
}

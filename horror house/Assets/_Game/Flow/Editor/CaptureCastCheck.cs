using System.Collections.Generic;
using System.Text;
using NightDuty;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// 붙잡힘 장면 프리팹 검사(메뉴 「야간근무 ▸ 연출 ▸ 붙잡힘 연출 검사」, 연출표 인스펙터 [검사]).
/// 팀원 프리팹이 규약(<see cref="CaptureCastSO"/>)에 맞는지 — 원점·몹 위치·CameraMount·Animator·끝 이벤트·길이·소리 — 를 한 장으로. 고치지는 않는다.
/// </summary>
public static class CaptureCastCheck
{
    [MenuItem("야간근무/연출/붙잡힘 연출 검사")]
    public static void CheckMenu()
    {
        Debug.Log(Report(CaptureCastSO.Load()));
    }

    /// <summary>연출표 전체 보고서.</summary>
    public static string Report(CaptureCastSO cast)
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("[붙잡힘 연출 검사] 연출표 " + (AssetDatabase.Contains(cast) ? AssetDatabase.GetAssetPath(cast) : "(Resources/CaptureCast.asset 없음 — 기본값)"));
        foreach (FearAxis axis in new[] { FearAxis.Auditory, FearAxis.Illuminance, FearAxis.Layout })
        {
            sb.AppendLine();
            CheckEntry(cast.Get(axis), sb);
        }

        return sb.ToString();
    }

    private static void CheckEntry(CaptureCastSO.Entry e, StringBuilder sb)
    {
        sb.AppendLine("■ " + CaptureCastSO.Label(e.axis) + " — 정적 " + e.silenceSeconds.ToString("0.0#") + "초" + (e.turnAround ? " · 돌아보기" : string.Empty) + (e.flashlightOff ? " · 손전등 끔" : string.Empty) + (e.darkWorld ? " · 어둠 속 장면만" : " · 방과 함께") + (string.IsNullOrEmpty(e.revealSound) ? string.Empty : " · 소리 " + e.revealSound + (SoundExists(e.revealSound) ? "" : "(아직 없음)")));
        if (e.prefab == null)
        {
            sb.AppendLine("  – 프리팹 비어 있음 → 기본 장면(대역 얼굴 " + e.fallbackFace + ", " + e.faceSeconds.ToString("0.00") + "초" + (e.pullDistance > 0f ? ", 끌려감 " + e.pullDistance.ToString("0.0#") + "m" : string.Empty) + ")");
            return;
        }

        sb.AppendLine("  프리팹: " + AssetDatabase.GetAssetPath(e.prefab));
        GameObject root = PrefabUtility.LoadPrefabContents(AssetDatabase.GetAssetPath(e.prefab));
        try
        {
            Renderer[] rs = root.GetComponentsInChildren<Renderer>(true);
            if (rs.Length == 0)
            {
                sb.AppendLine("  ✗ 렌더러가 없습니다(보이는 것이 없음).");
            }
            else
            {
                Bounds b = rs[0].bounds;
                for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
                Vector3 c = b.center - root.transform.position;
                string where = c.z > 0.05f ? "앞(+Z) " + c.z.ToString("0.00") + "m" : c.z < -0.05f ? "뒤(−Z) " + (-c.z).ToString("0.00") + "m" : "제자리";
                sb.AppendLine("  ✓ 몸 중심 = 원점(플레이어 발밑) 기준 " + where + ", 옆 " + c.x.ToString("+0.00;-0.00") + "m, 높이 " + b.min.y.ToString("0.00") + "~" + b.max.y.ToString("0.00") + "m (+Z = 플레이어 시선" + (e.turnAround ? ", 돌아본 뒤" : string.Empty) + ")");
                if (b.min.y < -0.3f) sb.AppendLine("  ✗ 바닥 아래로 " + (-b.min.y).ToString("0.00") + "m 내려가 있습니다(원점 = 바닥).");
            }

            Transform mount = FindDeep(root.transform, "CameraMount");
            if (mount != null)
            {
                Vector3 m = mount.position - root.transform.position;
                sb.AppendLine("  ✓ CameraMount — 시작 위치 높이 " + m.y.ToString("0.00") + "m" + (m.y < 1.2f || m.y > 2.0f ? " (눈높이 1.5~1.7m 근처가 자연스럽습니다)" : string.Empty) + (mount.GetComponent<Camera>() != null ? " · 시야각도 따름" : string.Empty));
            }
            else
            {
                sb.AppendLine("  – CameraMount 없음 → 카메라는 플레이어 눈에 그대로(카메라를 움직이려면 CameraMount 자식을 두고 애니메이션)");
            }

            Animator[] anims = root.GetComponentsInChildren<Animator>(true);
            bool doneEvent = false;
            float longest = 0f;
            int animated = 0;
            foreach (Animator a in anims)
            {
                if (a.runtimeAnimatorController == null) continue;
                animated++;
                AnimatorController ctrl = a.runtimeAnimatorController as AnimatorController;
                AnimatorOverrideController over = a.runtimeAnimatorController as AnimatorOverrideController;
                if (ctrl == null && over != null) ctrl = over.runtimeAnimatorController as AnimatorController;
                string first = ctrl != null && ctrl.layers.Length > 0 && ctrl.layers[0].stateMachine.defaultState != null ? ctrl.layers[0].stateMachine.defaultState.name : "?";
                AnimationClip firstClip = ctrl != null && ctrl.layers.Length > 0 && ctrl.layers[0].stateMachine.defaultState != null ? ctrl.layers[0].stateMachine.defaultState.motion as AnimationClip : null;
                foreach (AnimationClip clip in a.runtimeAnimatorController.animationClips)
                {
                    if (clip == null) continue;
                    longest = Mathf.Max(longest, clip.length);
                    foreach (AnimationEvent ev in AnimationUtility.GetAnimationEvents(clip))
                    {
                        if (ev.functionName == CaptureAnimEvents.DoneEvent) doneEvent = true;
                    }
                }

                sb.AppendLine("  ✓ Animator " + a.name + " — 시작 상태 " + first + (firstClip != null ? " (" + firstClip.name + " " + firstClip.length.ToString("0.00") + "초" + (firstClip.isLooping ? ", 반복" : string.Empty) + ")" : string.Empty));
            }

            if (animated == 0) sb.AppendLine("  – Animator 없음 → 움직이지 않는 장면이 " + e.faceSeconds.ToString("0.00") + "초 보입니다.");
            else if (doneEvent) sb.AppendLine("  ✓ 끝 = CaptureDone 이벤트");
            else sb.AppendLine("  – CaptureDone 이벤트 없음 → 모든 Animator가 반복 아닌 상태를 끝낼 때(반복 상태가 있으면 최대 " + e.maxSeconds.ToString("0.#") + "초). 끝 프레임에 CaptureDone을 권합니다.");
            if (longest > e.maxSeconds) sb.AppendLine("  ✗ 가장 긴 클립 " + longest.ToString("0.0") + "초가 최대 길이 " + e.maxSeconds.ToString("0.0") + "초보다 깁니다 — 최대 길이를 늘리십시오.");
            if (e.flashlightOff && !HasEvent(anims, CaptureAnimEvents.FlashlightEvent)) sb.AppendLine("  – 손전등이 꺼진 채 시작합니다. 켜지는 순간이 필요하면 CaptureFlashlight(1) 이벤트(장면 끝나면 원래대로 돌아갑니다).");

            int sources = root.GetComponentsInChildren<AudioSource>(true).Length;
            int lights = root.GetComponentsInChildren<Light>(true).Length;
            sb.AppendLine("  – AudioSource " + sources + "개(정적 중에도 들림) · Light " + lights + "개" + (e.darkWorld && lights == 0 && !e.flashlightOff ? " — 어둠 속 장면은 방 조명만 받습니다. 얼굴이 어두우면 Light를 넣으십시오." : string.Empty));
            int solid = 0;
            foreach (Collider col in root.GetComponentsInChildren<Collider>(true)) if (!col.isTrigger) solid++;
            if (solid > 0) sb.AppendLine("  – 콜라이더 " + solid + "개 — 세울 때 끕니다.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static bool HasEvent(Animator[] anims, string fn)
    {
        foreach (Animator a in anims)
        {
            if (a.runtimeAnimatorController == null) continue;
            foreach (AnimationClip clip in a.runtimeAnimatorController.animationClips)
            {
                if (clip == null) continue;
                foreach (AnimationEvent ev in AnimationUtility.GetAnimationEvents(clip)) if (ev.functionName == fn) return true;
            }
        }

        return false;
    }

    private static bool SoundExists(string name)
    {
        float v;
        return Resources.Load<AudioClip>("Direction/" + name) != null || DirectionSoundTableSO.FindExact(name, out v) != null;
    }

    private static Transform FindDeep(Transform root, string name)
    {
        if (root.name == name) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindDeep(root.GetChild(i), name);
            if (found != null) return found;
        }

        return null;
    }
}

/// <summary>연출표 인스펙터: 규약 요약 + [검사] 버튼.</summary>
[CustomEditor(typeof(CaptureCastSO))]
public sealed class CaptureCastSOEditor : Editor
{
    private string _report = string.Empty;

    public override void OnInspectorGUI()
    {
        EditorGUILayout.HelpBox(
            "축마다 Prefab 칸에 붙잡힘 장면 프리팹을 넣으면 끝입니다(코드·씬 수정 없음). 비우면 기본 장면(대역 얼굴).\n" +
            "· 원점 = 플레이어 발밑, +Z = 플레이어 시선(돌아보기면 돌아본 뒤). 몹은 프리팹 안에 그 기준으로 둔다\n" +
            "· 암전이 걷히는 순간 켜지며 Animator가 기본 상태부터 돈다(트리거 불필요)\n" +
            "· 자식 CameraMount가 있으면 카메라가 그 위치·회전(·꺼 둔 Camera의 시야각)을 따른다\n" +
            "· 이벤트: CaptureDone(끝, 권장) · CaptureSound(이름) · CaptureFlashlight(0/1) · CaptureCue(문자열)\n" +
            "· 시험: 플레이 → F3 콘솔 「조우」 탭 [붙잡힘 장면 미리 보기]", MessageType.Info);
        DrawDefaultInspector();
        EditorGUILayout.Space();
        if (GUILayout.Button("검사(원점·CameraMount·Animator·끝 이벤트·길이)")) _report = CaptureCastCheck.Report((CaptureCastSO)target);
        if (_report.Length > 0) EditorGUILayout.HelpBox(_report, MessageType.None);
    }
}

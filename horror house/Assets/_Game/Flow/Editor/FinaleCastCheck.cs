using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// 피날레 몹 프리팹 검사(메뉴 「야간근무 ▸ 연출 ▸ 피날레 몹 검사」, 배역표 인스펙터의 [검사] 버튼).
/// 팀원 프리팹이 규약(<see cref="FinaleCastSO"/>)에 맞는지 — 피벗·키·Animator·비트 이름·끝 이벤트·두드림 이벤트·콜라이더 — 를 한 장으로 알려 준다.
/// 고치지는 않는다. ✗는 그대로 끼우면 어색해지는 것, –는 없어도 돌지만 알아 둘 것.
/// </summary>
public static class FinaleCastCheck
{
    [MenuItem("야간근무/연출/피날레 몹 검사")]
    public static void CheckMenu()
    {
        Debug.Log(Report(FinaleCastSO.Load()));
    }

    /// <summary>배역표 전체 보고서.</summary>
    public static string Report(FinaleCastSO cast)
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("[피날레 몹 검사] 배역표 " + (AssetDatabase.Contains(cast) ? AssetDatabase.GetAssetPath(cast) : "(Resources/FinaleCast.asset 없음 — 기본값)"));
        foreach (FinaleRole role in new[] { FinaleRole.WindowMan, FinaleRole.SeatFigure })
        {
            sb.AppendLine();
            CheckRole(cast, role, sb);
        }

        return sb.ToString();
    }

    private static void CheckRole(FinaleCastSO cast, FinaleRole role, StringBuilder sb)
    {
        FinaleCastSO.Role r = cast.Get(role);
        GameObject prefab = cast.PrefabFor(role);
        sb.AppendLine("■ " + FinaleBeats.Label(role) + " — 자리 " + r.anchorId + (string.IsNullOrEmpty(r.judgeId) ? string.Empty : " · 응시 판정 " + r.judgeId));
        if (StageAnchor.Find(r.anchorId) == null && !Application.isPlaying)
        {
            bool inScene = false;
            foreach (StageAnchor a in Object.FindObjectsByType<StageAnchor>(FindObjectsInactive.Include, FindObjectsSortMode.None)) if (a.AnchorId == r.anchorId) inScene = true;
            sb.AppendLine(inScene ? "  ✓ 씬에 자리 있음" : "  – 열린 씬에 자리가 없습니다(「야간근무 ▸ 연출 ▸ 고정 몹 자리 놓기」)");
        }

        if (prefab == null)
        {
            sb.AppendLine("  – 프리팹 비어 있음 → 대역 " + r.fallbackStandIn + "이 섭니다.");
            return;
        }

        if (r.prefab == null) sb.AppendLine("  – 이 칸은 비어 있어 창밖 남자 프리팹을 씁니다.");
        sb.AppendLine("  프리팹: " + AssetDatabase.GetAssetPath(prefab));

        string path = AssetDatabase.GetAssetPath(prefab);
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            CheckShape(root, sb);
            CheckColliders(root, sb);
            CheckAnimator(root, r, role, sb);
            Transform aim = root.transform.Find("Aim");
            sb.AppendLine(aim != null ? "  ✓ Aim(머리 조준점) 있음 — 높이 " + aim.position.y.ToString("0.00") + "m" : "  – Aim 없음 → 머리 높이(렌더러 위끝 −0.15m)에 자동으로 만듭니다.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void CheckShape(GameObject root, StringBuilder sb)
    {
        Renderer[] rs = root.GetComponentsInChildren<Renderer>(true);
        if (rs.Length == 0)
        {
            sb.AppendLine("  ✗ 렌더러가 없습니다(보이는 것이 없음).");
            return;
        }

        Bounds b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        Vector3 pivot = root.transform.position;
        float height = b.max.y - b.min.y;
        float foot = b.min.y - pivot.y;
        sb.AppendLine((Mathf.Abs(foot) <= 0.1f ? "  ✓" : "  ✗") + " 피벗 = 발바닥(렌더러 아래끝이 피벗에서 " + foot.ToString("+0.00;-0.00") + "m" + (Mathf.Abs(foot) <= 0.1f ? ")" : " — 0에서 ±0.1m 안이어야 바닥에 섭니다)"));
        sb.AppendLine((height >= 1.2f && height <= 2.6f ? "  ✓" : "  ✗") + " 키 " + height.ToString("0.00") + "m (1.2~2.6m, 프리팹 기본 자세 기준)");
        Vector3 offset = b.center - pivot;
        if (new Vector2(offset.x, offset.z).magnitude > 0.35f) sb.AppendLine("  ✗ 몸 중심이 피벗에서 수평으로 " + new Vector2(offset.x, offset.z).magnitude.ToString("0.00") + "m 떨어져 있습니다(피벗 = 발바닥 중앙).");
        sb.AppendLine("  – 앞 방향은 +Z(파란 축)여야 합니다 — 창 안·플레이어 쪽을 봅니다(눈으로 확인).");
    }

    private static void CheckColliders(GameObject root, StringBuilder sb)
    {
        int solid = 0;
        foreach (Collider c in root.GetComponentsInChildren<Collider>(true)) if (!c.isTrigger) solid++;
        if (solid > 0) sb.AppendLine("  – 단단한 콜라이더 " + solid + "개 — 세울 때 끕니다(응시 상자는 이쪽이 붙입니다).");
        if (root.GetComponentInChildren<Rigidbody>(true) != null) sb.AppendLine("  ✗ Rigidbody가 있습니다 — 몹이 넘어지거나 밀립니다. 빼 주십시오.");
    }

    private static void CheckAnimator(GameObject root, FinaleCastSO.Role r, FinaleRole role, StringBuilder sb)
    {
        Animator animator = root.GetComponentInChildren<Animator>(true);
        if (animator == null || animator.runtimeAnimatorController == null)
        {
            sb.AppendLine("  – Animator(컨트롤러)가 없습니다 → 움직이지 않고 서기만 합니다. 비트는 모두 건너뜁니다.");
            return;
        }

        AnimatorController ctrl = animator.runtimeAnimatorController as AnimatorController;
        AnimatorOverrideController over = animator.runtimeAnimatorController as AnimatorOverrideController;
        if (ctrl == null && over != null) ctrl = over.runtimeAnimatorController as AnimatorController;
        if (ctrl == null)
        {
            sb.AppendLine("  – 컨트롤러 형식을 읽지 못했습니다(비트 이름 검사는 건너뜀).");
            return;
        }

        sb.AppendLine("  ✓ Animator — " + ctrl.name + (animator.applyRootMotion ? " (루트 모션 켜짐: 자리에서 움직여도 됩니다)" : string.Empty));
        Dictionary<string, AnimatorState> states = new Dictionary<string, AnimatorState>();
        if (ctrl.layers.Length > 0) CollectStates(ctrl.layers[0].stateMachine, states);
        HashSet<string> triggers = new HashSet<string>();
        foreach (AnimatorControllerParameter p in ctrl.parameters) if (p.type == AnimatorControllerParameterType.Trigger) triggers.Add(p.name);

        bool anyKnockEvent = false;
        foreach (FinaleBeat beat in FinaleBeats.For(role))
        {
            string name = r.Binding(beat).NameOr(beat);
            bool trig = triggers.Contains(name);
            AnimatorState st;
            bool state = states.TryGetValue(name, out st);
            string label = FinaleBeats.Label(beat) + "(" + name + ")";
            if (!trig && !state)
            {
                sb.AppendLine(beat == FinaleBeat.Idle ? "  – " + label + ": 없음 — 기본 상태로 둡니다" : "  – " + label + ": 트리거·상태 없음 → 이 비트는 건너뜁니다");
                continue;
            }

            string how = trig ? "트리거" : "상태(CrossFade)";
            AnimationClip clip = state && st.motion is AnimationClip ? (AnimationClip)st.motion : null;
            if (trig && clip == null)
            {
                // 트리거면 그 트리거로 들어가는 상태를 찾아 클립을 본다.
                foreach (AnimatorState s in states.Values) if (s.name == name && s.motion is AnimationClip) clip = (AnimationClip)s.motion;
            }

            bool done = false, knock = false;
            if (clip != null)
            {
                foreach (AnimationEvent e in AnimationUtility.GetAnimationEvents(clip))
                {
                    if (e.functionName == FinaleAnimEvents.BeatDoneEvent) done = true;
                    if (e.functionName == FinaleAnimEvents.KnockEvent) { knock = true; anyKnockEvent = true; }
                }
            }

            string extra = string.Empty;
            if (!FinaleBeats.IsLoop(beat))
            {
                if (done) extra = " · 끝 = FinaleBeatDone 이벤트";
                else if (clip != null && !clip.isLooping) extra = " · 끝 = 상태 끝(이벤트 없음)";
                else extra = " · 끝 = 최대 " + r.Binding(beat).maxSeconds.ToString("0.#") + "초(이벤트도, 끝나는 상태도 없음 — FinaleBeatDone 이벤트를 권합니다)";
            }
            else if (clip != null && !clip.isLooping)
            {
                extra = " · 클립이 반복(Loop Time)이 아닙니다";
            }

            if (beat == FinaleBeat.Knock) extra += knock ? " · 두드림 소리 = FinaleKnock 이벤트" : " · FinaleKnock 이벤트 없음 → " + FinaleMob.FallbackKnockInterval + "초마다 소리만";
            sb.AppendLine("  ✓ " + label + ": " + how + (clip != null ? " · 클립 " + clip.name + " " + clip.length.ToString("0.00") + "초" : string.Empty) + extra);
        }

        if (role == FinaleRole.WindowMan && !anyKnockEvent && !states.ContainsKey(r.Binding(FinaleBeat.Knock).NameOr(FinaleBeat.Knock)) && !triggers.Contains(r.Binding(FinaleBeat.Knock).NameOr(FinaleBeat.Knock)))
        {
            sb.AppendLine("  – 두드림 동작이 없으면 창 앞에 선 채 소리만 납니다.");
        }
    }

    private static void CollectStates(AnimatorStateMachine sm, Dictionary<string, AnimatorState> into)
    {
        foreach (ChildAnimatorState c in sm.states) into[c.state.name] = c.state;
        foreach (ChildAnimatorStateMachine sub in sm.stateMachines) CollectStates(sub.stateMachine, into);
    }
}

/// <summary>배역표 인스펙터: 규약 요약 + [검사] 버튼.</summary>
[CustomEditor(typeof(FinaleCastSO))]
public sealed class FinaleCastSOEditor : Editor
{
    private string _report = string.Empty;

    public override void OnInspectorGUI()
    {
        EditorGUILayout.HelpBox(
            "팀원 몹은 배역의 Prefab 칸에 넣으면 끝입니다(코드·씬 수정 없음).\n" +
            "· 피벗 = 발바닥 중앙, 앞 = +Z, 키 1.2~2.6m, Rigidbody·스크립트 불필요\n" +
            "· Animator 트리거 또는 상태 이름: Idle · Appear · Knock · Seen · Vanish · Stand (없는 것은 건너뜀)\n" +
            "· 한 번짜리(Appear·Seen·Vanish) 끝 프레임에 이벤트 FinaleBeatDone, 두드림 손이 닿는 프레임에 FinaleKnock, 그 밖은 FinaleCue(문자열)\n" +
            "· 시험: 플레이 → F3 콘솔 「조우」 탭의 피날레 몹 버튼", MessageType.Info);
        DrawDefaultInspector();
        EditorGUILayout.Space();
        if (GUILayout.Button("검사(피벗·키·Animator·비트·이벤트)")) _report = FinaleCastCheck.Report((FinaleCastSO)target);
        if (_report.Length > 0) EditorGUILayout.HelpBox(_report, MessageType.None);
    }
}

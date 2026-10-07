using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 배치 사망 컷신 <b>ver2(피 비)</b> 프리팹을 만든다 — ver1 프리팹(<c>DeathCutscene_Layout</c>)의 <b>프리팹 배리언트</b>다.
/// 끌려 들어감·먹물 수치와 소리는 ver1을 그대로 물려받으므로, ver1 인스펙터에서 고치면 ver2에도 들어간다(걷는 속도·일렁임 최대·어둠은 ver2가 덮어씀).
/// ver2에서 바꾸는 것(bloodRain 켬 · 핏방울 프리팹 · ①~③ 소리 · 디버그 키 F5)만 배리언트에 덮어쓴다.
/// </summary>
public static class DeathCutsceneLayout2Builder
{
    private const string Folder = "Assets/3.2 Programmer_Kim/03 Prefebs/04 Horror/Resources";
    private const string BasePath = Folder + "/" + LayoutDeathCutscene.ResourceName + ".prefab";
    private const string PrefabPath = Folder + "/" + LayoutDeathCutscene.ResourceNameV2 + ".prefab";
    private const string DripPrefab = "Assets/3.2 Programmer_Kim/03 Prefebs/04 Horror/Bloody/HorrorProp_CeilingBloodDrip.prefab";
    private const string EggmanFbx = "Assets/2. Art/04 Materials/m_creature/m_eggmantree/Eggman_Tree.fbx";
    private const string EggmanPrefab = "Assets/3.2 Programmer_Kim/03 Prefebs/04 Horror/Creature/HorrorCreature_EggmanTree.prefab";
    private const string EggmanController = "Assets/3.2 Programmer_Kim/05 Animations/Horror/HorrorCreature_EggmanTree.controller";

    /// <summary>
    /// Eggman_Tree FBX를 감싼 프리팹을 만든다 — FBX는 고치지 않고(중첩), Animator만 붙여 내장 애니메이션 「Scene」(8초)을 반복한다.
    /// 이 클립은 반복 표시가 꺼져 있지만 첫 프레임과 끝 프레임 자세가 같아(실측 0.0°) 끝에서 자기 자신으로 넘기면 이음매 없이 돈다.
    /// </summary>
    [MenuItem("Tools/Programmer_Kim/Horror/Build Eggman Tree Prefab")]
    public static GameObject BuildEggmanTree()
    {
        GameObject fbx = AssetDatabase.LoadAssetAtPath<GameObject>(EggmanFbx);
        if (fbx == null) return null;
        AnimationClip clip = null;
        foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(EggmanFbx))
        {
            if (o is AnimationClip c && !c.name.StartsWith("__preview")) clip = c;
        }

        // 컨트롤러는 있으면 그대로 쓴다(새로 만들면 GUID가 바뀌어 참조가 끊긴다)
        var controller = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(EggmanController);
        if (controller == null)
        {
            controller = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(EggmanController);
            UnityEditor.Animations.AnimatorStateMachine sm = controller.layers[0].stateMachine;
            UnityEditor.Animations.AnimatorState state = sm.AddState("Idle");
            state.motion = clip;
            sm.defaultState = state;
            UnityEditor.Animations.AnimatorStateTransition loop = state.AddTransition(state);
            loop.hasExitTime = true;
            loop.exitTime = 1f;
            loop.duration = 0f;
        }

        var root = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
        root.name = "HorrorCreature_EggmanTree";
        Animator anim = root.GetComponent<Animator>();   // 에디터의 GetComponent는 없을 때 「가짜 null」을 돌려줘 ??를 쓰면 안 된다
        if (anim == null) anim = root.AddComponent<Animator>();
        anim.runtimeAnimatorController = controller;
        anim.applyRootMotion = false;
        // 늘 움직인다 — 화면 밖에서 Animator가 멈추면 동작 키우기·흔들림이 Animator 대신 서로의 결과 위에 쌓일 수 있다
        anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        // 동작 키우기 — 클립을 고르게 64번 샘플해 뼈마다 평균 자세를 굽는다
        if (clip != null) AddAmplify(root, clip);
        // 기존 사람 나무의 흔들림·경련(HorrorTreeSway)을 Eggman 뼈에 맞춰 붙인다
        HorrorTreeSway sway = AddSway(root);
        // 바라보면 화면이 물드는 효과(HorrorGazeHold + 볼륨 둘) — 사람 나무와 같은 설정, 흔들림도 바라보는 동안 격해진다
        AddGaze(root, sway);

        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, EggmanPrefab);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        Debug.Log("[DeathCutsceneLayout2Builder] Eggman 나무 프리팹 " + EggmanPrefab + (clip != null ? " · 애니메이션 " + clip.name + " " + clip.length + "초" : " · 애니메이션 없음"));
        return saved;
    }

    [MenuItem("Tools/Programmer_Kim/Horror/Build Death Cutscene (Layout ver2 - Blood Rain)")]
    public static void BuildMenu()
    {
        if (File.Exists(PrefabPath) &&
            !EditorUtility.DisplayDialog("사망 컷신 (배치 ver2)", "이미 있는 ver2 프리팹을 새로 만듭니다.\nver2 인스펙터에서 고친 수치는 기본값으로 돌아갑니다(ver1에서 물려받는 값은 그대로).", "새로 만들기", "취소"))
        {
            return;
        }
        Debug.Log(Build());
    }

    public static string Build()
    {
        GameObject basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BasePath);
        if (basePrefab == null) return "[DeathCutsceneLayout2Builder] ver1 프리팹이 없습니다 — 「Build Death Cutscene (Layout)」을 먼저 실행하십시오.";

        var missing = new List<string>();
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
        instance.name = LayoutDeathCutscene.ResourceNameV2;
        var so = new SerializedObject(instance.GetComponent<LayoutDeathCutscene>());

        so.FindProperty("bloodRain").boolValue = true;
        so.FindProperty("debugKey").intValue = (int)KeyCode.F5;
        // ver1에서 물려받는 값 중 ver2만 다르게(2026-10-05 사용자) — 더 느리게 걷고, 더 심하게 일렁이고, 덜 어둡게
        so.FindProperty("walkSpeed").floatValue = 1.0f;
        so.FindProperty("wobbleMax").floatValue = 1.0f;
        so.FindProperty("darkenVignette").floatValue = 0.6f;
        so.FindProperty("darkenOverall").floatValue = 0.2f;
        GameObject drip = AssetDatabase.LoadAssetAtPath<GameObject>(DripPrefab);
        if (drip == null) missing.Add("천장 핏방울 프리팹");
        so.FindProperty("bloodDripPrefab").objectReferenceValue = drip;
        // 사람 나무 모델 — ver2는 Eggman_Tree(2026-10-05 사용자). ver1은 HumanTree 그대로
        GameObject egg = AssetDatabase.LoadAssetAtPath<GameObject>(EggmanPrefab);
        if (egg == null) egg = BuildEggmanTree();
        if (egg == null) missing.Add("Eggman_Tree");
        else so.FindProperty("treePrefab").objectReferenceValue = egg;

        // 방울이 바닥에 닿는 소리 — 짧은 한 방울(1.9초)
        SerializedProperty drops = so.FindProperty("dropClips");
        var dropList = new List<AudioClip>();
        foreach (string n in new[] { "os_drip_a", "os_drip_b" })
        {
            AudioClip c = Clip(n, missing);
            if (c != null) dropList.Add(c);
        }
        drops.arraySize = dropList.Count;
        for (int i = 0; i < dropList.Count; i++) drops.GetArrayElementAtIndex(i).objectReferenceValue = dropList[i];

        // ③부터 차오르는 소리 — (이름, 볼륨, 차오르는 초, 나무에 나눠 다는 수 — 0이면 화면 소리)
        var specs = new[]
        {
            S("SFX_LAB_SinkDrip_1", 0.45f, 1.5f, 0),                 // 핏물 방울 고이는 소리(가까이)
            S("SFX_LAB_SinkDrip_3", 0.60f, 2.0f, 3),                 // 여기저기서 떨어지는 방울
            S("SFX_HALL_FountainTrickle_1", 0.50f, 3.0f, 0),         // 물 흐르는 소리
            S("SFX_TREE_HumanMurmur", 0.85f, 3.0f, 5),               // 사람들 웅성거림 — 나무 다섯 그루에서 음높이를 조금씩 달리해
            S("CAP_H1_02_TangledWhispers", 0.45f, 4.0f, 0),          // 엉킨 속삭임
        };
        SerializedProperty rain = so.FindProperty("rainSounds");
        rain.arraySize = 0;
        foreach (var s in specs)
        {
            AudioClip c = Clip(s.name, missing);
            if (c == null) continue;
            rain.arraySize++;
            SerializedProperty e = rain.GetArrayElementAtIndex(rain.arraySize - 1);
            e.FindPropertyRelative("clip").objectReferenceValue = c;
            e.FindPropertyRelative("volume").floatValue = s.volume;
            e.FindPropertyRelative("fadeIn").floatValue = s.fadeIn;
            e.FindPropertyRelative("atTrees").intValue = s.atTrees;
        }
        so.ApplyModifiedPropertiesWithoutUndo();

        GameObject saved = PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath, out bool ok);
        Object.DestroyImmediate(instance);
        AssetDatabase.SaveAssets();
        bool isVariant = saved != null && PrefabUtility.GetPrefabAssetType(saved) == PrefabAssetType.Variant;
        return "[DeathCutsceneLayout2Builder] 프리팹 " + (ok ? "저장" : "실패") + " " + PrefabPath + (isVariant ? " (ver1의 배리언트)" : "") +
               " · 차오르는 소리 " + rain.arraySize + (missing.Count > 0 ? " · <b>없음: " + string.Join(", ", missing) + "</b>" : " · 빠진 것 없음");
    }

    private static void AddAmplify(GameObject root, AnimationClip clip)
    {
        var bones = new List<Transform>();
        foreach (SkinnedMeshRenderer r in root.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            foreach (Transform b in r.bones) if (b != null && !bones.Contains(b)) bones.Add(b);
        }
        const int Samples = 64;
        var sum = new Vector4[bones.Count];
        var first = new Quaternion[bones.Count];
        var maxDev = new float[bones.Count];   // 첫 프레임에서 가장 멀어진 각(°)
        for (int s = 0; s < Samples; s++)
        {
            clip.SampleAnimation(root, clip.length * s / Samples);
            for (int i = 0; i < bones.Count; i++)
            {
                Quaternion q = bones[i].localRotation;
                if (s == 0) first[i] = q;
                maxDev[i] = Mathf.Max(maxDev[i], Quaternion.Angle(first[i], q));
                if (Quaternion.Dot(first[i], q) < 0f) q = new Quaternion(-q.x, -q.y, -q.z, -q.w);   // 같은 반구로 맞춰 더한다
                sum[i] += new Vector4(q.x, q.y, q.z, q.w);
            }
        }
        clip.SampleAnimation(root, 0f);   // 저장되는 자세는 첫 프레임
        // 클립에서 실제로 움직이는 뼈만 — 안 움직이는 뼈는 Animator가 매 프레임 쓰지 않아,
        // 흔들림(HorrorTreeSway)이 쓴 값을 다음 프레임에 또 키우며 누적된다(2026-10-05 실측: 머리가 9.6m 날아감)
        var entries = new List<HorrorAnimAmplify.Entry>();
        for (int i = 0; i < bones.Count; i++)
        {
            Vector4 v = sum[i].normalized;
            var mean = new Quaternion(v.x, v.y, v.z, v.w);
            if (maxDev[i] < 0.5f) continue;
            entries.Add(new HorrorAnimAmplify.Entry { bone = bones[i], reference = mean });
        }
        HorrorAnimAmplify amp = root.AddComponent<HorrorAnimAmplify>();
        amp.strength = 1.8f;
        amp.SetBones(entries.ToArray());
    }

    /// <summary>
    /// 흔들림 뼈 — 회전이 부모에서 자식으로 쌓이므로 줄기 아래는 작게, 몸·팔·덩굴 끝은 크게.
    /// 사람 나무(HumanTree)는 세기 0.32였지만 Eggman은 더 흔들리게 0.6.
    /// </summary>
    private const string HumanTreePrefab = "Assets/3.2 Programmer_Kim/03 Prefebs/04 Horror/Creature/HorrorCreature_HumanTree_Moving.prefab";
    private const string HumanVariantPath = Folder + "/" + LayoutDeathCutscene.ResourceNameV2HumanTree + ".prefab";

    /// <summary>
    /// ver2(피 비)와 똑같고 나무만 사람 나무(HumanTree)인 프리팹 — <b>ver2 프리팹의 배리언트</b>라
    /// ver2를 고치면(빌더 재실행 포함) 그대로 따라온다. 덮어쓰는 것은 나무 모델과 디버그 키(F4)뿐.
    /// </summary>
    [MenuItem("Tools/Programmer_Kim/Horror/Build Death Cutscene (Layout ver2 - HumanTree)")]
    public static void BuildHumanTreeMenu()
    {
        Debug.Log(BuildHumanTreeVariant());
    }

    public static string BuildHumanTreeVariant()
    {
        GameObject v2 = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (v2 == null) return "[DeathCutsceneLayout2Builder] ver2 프리팹이 없습니다 — 「Build Death Cutscene (Layout ver2 - Blood Rain)」을 먼저 실행하십시오.";
        GameObject human = AssetDatabase.LoadAssetAtPath<GameObject>(HumanTreePrefab);
        if (human == null) return "[DeathCutsceneLayout2Builder] 사람 나무 프리팹이 없습니다: " + HumanTreePrefab;

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(v2);
        instance.name = LayoutDeathCutscene.ResourceNameV2HumanTree;
        var so = new SerializedObject(instance.GetComponent<LayoutDeathCutscene>());
        so.FindProperty("treePrefab").objectReferenceValue = human;
        so.FindProperty("debugKey").intValue = (int)KeyCode.F4;
        so.ApplyModifiedPropertiesWithoutUndo();
        GameObject saved = PrefabUtility.SaveAsPrefabAsset(instance, HumanVariantPath, out bool ok);
        Object.DestroyImmediate(instance);
        AssetDatabase.SaveAssets();
        bool isVariant = saved != null && PrefabUtility.GetPrefabAssetType(saved) == PrefabAssetType.Variant;
        return "[DeathCutsceneLayout2Builder] 프리팹 " + (ok ? "저장" : "실패") + " " + HumanVariantPath + (isVariant ? " (ver2의 배리언트, 나무 = HumanTree, F4)" : "");
    }

    /// <summary>
    /// 사람 나무 프리팹의 응시 설정을 그대로 옮긴다 — 볼륨 프로필·우선순위·세기·각도·거리·페이드는 사람 나무 것을 읽어 쓴다(사람 나무를 고치면 다시 돌려 맞춤).
    /// 바라볼 점은 줄기 위(Trunk_Upper) 0.6m — 몸들이 모인 가운데(바닥에서 약 2.1m).
    /// </summary>
    private static void AddGaze(GameObject root, HorrorTreeSway sway)
    {
        GameObject human = AssetDatabase.LoadAssetAtPath<GameObject>(HumanTreePrefab);
        HorrorGazeHold src = human != null ? human.GetComponentInChildren<HorrorGazeHold>(true) : null;
        if (src == null)
        {
            Debug.LogWarning("[DeathCutsceneLayout2Builder] 사람 나무의 HorrorGazeHold를 찾지 못해 응시 효과를 붙이지 않았습니다.");
            return;
        }

        Transform upper = null;
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true)) if (t.name == "Trunk_Upper") { upper = t; break; }
        var look = new GameObject("LookPoint").transform;
        look.SetParent(upper != null ? upper : root.transform, false);
        look.position = (upper != null ? upper.position : root.transform.position + Vector3.up * 1.5f) + Vector3.up * 0.6f;

        var holder = new GameObject("E_ScreenVolume");
        holder.transform.SetParent(root.transform, false);
        var srcSo = new SerializedObject(src);
        SerializedProperty srcVolumes = srcSo.FindProperty("volumes");

        HorrorGazeHold gaze = holder.AddComponent<HorrorGazeHold>();
        var so = new SerializedObject(gaze);
        foreach (string p in new[] { "maxAngle", "maxDistance", "startDelay", "fadeInSeconds", "fadeOutSeconds" })
        {
            so.FindProperty(p).floatValue = srcSo.FindProperty(p).floatValue;
        }
        so.FindProperty("lookPoint").objectReferenceValue = look;
        so.FindProperty("lookObject").objectReferenceValue = root.transform;
        SerializedProperty volumes = so.FindProperty("volumes");
        volumes.arraySize = 0;
        for (int i = 0; i < srcVolumes.arraySize; i++)
        {
            SerializedProperty sv = srcVolumes.GetArrayElementAtIndex(i);
            var srcVol = sv.FindPropertyRelative("volume").objectReferenceValue as UnityEngine.Rendering.Volume;
            if (srcVol == null) continue;
            var vgo = new GameObject(srcVol.name);
            vgo.transform.SetParent(holder.transform, false);
            vgo.layer = srcVol.gameObject.layer;   // Viewmodel(8) — 플레이어 카메라 볼륨 마스크
            var vol = vgo.AddComponent<UnityEngine.Rendering.Volume>();
            vol.isGlobal = srcVol.isGlobal;
            vol.priority = srcVol.priority;
            vol.sharedProfile = srcVol.sharedProfile;
            vol.weight = 0f;
            volumes.arraySize++;
            SerializedProperty e = volumes.GetArrayElementAtIndex(volumes.arraySize - 1);
            e.FindPropertyRelative("volume").objectReferenceValue = vol;
            e.FindPropertyRelative("maxWeight").floatValue = sv.FindPropertyRelative("maxWeight").floatValue;
        }
        so.ApplyModifiedPropertiesWithoutUndo();

        // 바라보는 동안 흔들림이 격해지는 배율도 사람 나무 것
        HorrorTreeSway srcSway = human.GetComponent<HorrorTreeSway>();
        if (sway != null && srcSway != null)
        {
            var sw = new SerializedObject(sway);
            var ss = new SerializedObject(srcSway);
            sw.FindProperty("gaze").objectReferenceValue = gaze;
            foreach (string p in new[] { "gazeAmplitude", "gazeSpeed", "gazeTwitchRate" }) sw.FindProperty(p).floatValue = ss.FindProperty(p).floatValue;
            sw.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static HorrorTreeSway AddSway(GameObject root)
    {
        var spec = new List<KeyValuePair<string, float>>
        {
            new KeyValuePair<string, float>("Trunk_Base", 0.3f),
            new KeyValuePair<string, float>("Trunk_Mid", 0.45f),
            new KeyValuePair<string, float>("Trunk_Upper", 0.6f),
            new KeyValuePair<string, float>("Trunk_Crown", 0.8f),
        };
        for (int b = 1; b <= 7; b++)
        {
            string n = "Body_0" + b + "_";
            spec.Add(new KeyValuePair<string, float>(n + "Spine", 0.7f));
            spec.Add(new KeyValuePair<string, float>(n + "Head", 0.9f));
            spec.Add(new KeyValuePair<string, float>(n + "L_UpperArm", 1.1f));
            spec.Add(new KeyValuePair<string, float>(n + "R_UpperArm", 1.1f));
        }
        for (int t = 1; t <= 6; t++)
        {
            spec.Add(new KeyValuePair<string, float>("Tendril_0" + t + "_1", 0.8f));
            spec.Add(new KeyValuePair<string, float>("Tendril_0" + t + "_3", 1.3f));
        }

        HorrorTreeSway sway = root.AddComponent<HorrorTreeSway>();
        var so = new SerializedObject(sway);
        so.FindProperty("intensity").floatValue = 0.6f;
        SerializedProperty bonesProp = so.FindProperty("bones");
        bonesProp.arraySize = 0;
        var all = root.GetComponentsInChildren<Transform>(true);
        foreach (var kv in spec)
        {
            Transform found = null;
            foreach (Transform t in all) if (t.name == kv.Key) { found = t; break; }
            if (found == null) continue;
            bonesProp.arraySize++;
            SerializedProperty e = bonesProp.GetArrayElementAtIndex(bonesProp.arraySize - 1);
            e.FindPropertyRelative("bone").objectReferenceValue = found;
            e.FindPropertyRelative("weight").floatValue = kv.Value;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        return sway;
    }

    private struct Spec { public string name; public float volume, fadeIn; public int atTrees; }

    private static Spec S(string name, float volume, float fadeIn, int atTrees)
    {
        return new Spec { name = name, volume = volume, fadeIn = fadeIn, atTrees = atTrees };
    }

    private static AudioClip Clip(string fileName, List<string> missing)
    {
        AudioClip c = DeathCutsceneSoundLayout.FindClip(fileName);
        if (c == null && !missing.Contains(fileName)) missing.Add(fileName);
        return c;
    }
}

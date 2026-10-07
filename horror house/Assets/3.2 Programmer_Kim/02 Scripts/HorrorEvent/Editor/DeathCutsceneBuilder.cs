using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

/// <summary>
/// 청각 붙잡힘 컷신(<see cref="DeathCutscene"/>)의 프리팹과 Timeline을 만든다.
/// 「모든 소리가 뚝 끊기고 귀 바로 옆에서 기괴한 속삭임. 돌아보면 소년의 얼굴이 마구 흔들림.」
///
/// <para>한 번 만든 뒤에는 <b>Timeline 창에서 키를 직접 고친다.</b> 메뉴를 다시 돌리면 Timeline과 프리팹을 새로 만들어
/// 손으로 고친 것이 사라지므로, 이미 있으면 묻는다.</para>
///
/// 좌표는 컷신 루트 기준이다 — 루트는 재생할 때 플레이어 발밑으로 옮겨지고 +Z가 플레이어의 정면이다.
/// </summary>
public static class DeathCutsceneBuilder
{
    // 2026-10-05: 소년 모델을 NewSkirtBoy_idle로 바꿈. 새 모델에는 idle(4초, 처음·끝 자세가 같아 반복 가능)뿐이고
    // 머리 흔들기가 없어서, 옛 모델의 brrr에서 Head 회전만 뽑아 새 모델 뼈에 옮긴 클립을 굽는다(BuildHeadShakeClip).
    private const string BoyModel = "Assets/3.2 Programmer_Kim/99 Resources/05 Model/SkirtBoy/NewSkirtBoy_idle.fbx";
    private const string ShakeSourceModel = "Assets/2. Art/04 Materials/m_creature/m_skirtboy/Skirt_Boy_brrr.fbx";
    private const string TimelinePath = "Assets/3.2 Programmer_Kim/05 Animations/Horror/DeathCutscene_Auditory.playable";
    private const string PrefabFolder = "Assets/3.2 Programmer_Kim/03 Prefebs/04 Horror/Resources";
    private const string PrefabPath = PrefabFolder + "/" + DeathCutscene.ResourceName + ".prefab";

    private const string SuffocateProfile = "Assets/3.2 Programmer_Kim/04 Materials/HorrorVolume/VP_Horror_Suffocate.asset";

    // 연출 시각(초) — 2026-10-02: 소리가 끊긴 채 두리번거리는 구간을 1.5초 → 3.6초로 늘림
    // 2026-10-05 사용자 가이드로 다시 짬 — 뼈대 시각은 DeathCutsceneSoundLayout 한 곳(소리와 화면이 같은 값을 본다):
    // ① 0~6.40 빌드업 소리 속에서 좌우를 두리번거리며 고개를 천천히 숙임(시점이 꺾이는 순간 = 사물함 철컹, 문서 1.70·2.90·3.50초 × BuildupScale)
    // ② 6.40 쾅 — 다른 소리가 모두 끊김. 쾅 울림이 사라진 뒤 1.5초 정적 동안 고개를 들어 정면
    // ③ 6.70 정면을 보는 순간 화면이 확 뒤돌아(0.35초) 소년과 마주함 → 화면 어두워짐 · 머리 흔들기 · 비명
    private const float BuildupEnd = DeathCutsceneSoundLayout.BuildupEnd;   // 6.40
    private const float TurnStart = DeathCutsceneSoundLayout.TurnStart;     // 8.70
    private const float TurnEnd = DeathCutsceneSoundLayout.TurnEnd;         // 9.05
    private const float Duration = DeathCutsceneSoundLayout.EndAt;          // 12.33
    private const float BoyAppearAt = TurnStart - 0.5f;   // 등 뒤라 안 보일 때 미리 세움
    private const float ShakeStart = TurnEnd - 0.05f;     // 마주하는 순간 머리 흔들기 시작
    private const float HeadDownPitch = 28f;              // 숙인 고개(+가 아래)
    // 소년 머리 흔들기 크기 — 원본 brrr 꺾임(최대 약 93°)에 곱한다. 1 = 원본, 0.55 ≈ 최대 51°(2026-10-05 사용자 요청으로 줄임)
    private const float ShakeAmount = 0.55f;
    // 소년 머리 흔들기 속도 — 원본 brrr을 이 배속으로 돌린다(2026-10-05 사용자 요청 2배). 흔들기 길이는 그대로 두고,
    // 원본 끝에 닿으면 거꾸로 되돌려(왕복) 채운다 — 원본 처음·끝 자세가 71° 달라 처음으로 바로 돌아가면 튄다.
    private const float ShakeSpeed = 2f;
    // 사용자 조정값 0.7 → 0.2(밤 씬에서 얼굴이 하얗게 떠서). 2026-10-05 새 모델(NewSkirtBoy)은 피부가 더 밝아
    // 0.2에서도 얼굴이 하얗게 날아가 0.05로 낮춤(PlayScene_test 실측: 0.05에서 감은 눈·입이 보이고, 0이면 너무 어두움).
    private const float FaceLightIntensity = 0.05f;

    // 소년 자리: 플레이어 등 뒤 오른쪽. 고개를 돌린 시선 끝(약 146°)에 얼굴이 오게 한다.
    // 2026-10-02: 0.58m → 0.33m로 당김(같은 방향이라 돌아보는 각도는 그대로, 위아래는 aimPoint 보정이 맞춘다)
    private static readonly Vector3 BoyLocalPos = new Vector3(0.17f, 0f, -0.283f);
    private const float FaceLightForward = 0.2f;   // 얼굴 앞 — 소년과 카메라 사이
    private const float BoyYaw = -31f;      // 소년이 플레이어 눈을 향하도록
    private const float TurnYaw = 146f;     // 오른쪽 어깨 너머로 돌아보는 각
    private const float TurnPitch = 17f;    // 소년 얼굴(1.5m)이 눈(1.7m)보다 낮아 살짝 내려다봄

    [MenuItem("Tools/Programmer_Kim/Horror/Build Death Cutscene (Auditory)")]
    public static void BuildMenu()
    {
        if ((File.Exists(PrefabPath) || File.Exists(TimelinePath)) &&
            !EditorUtility.DisplayDialog("사망 컷신", "이미 있는 프리팹과 Timeline을 새로 만듭니다.\nTimeline에서 손으로 고친 키는 사라집니다.", "새로 만들기", "취소"))
        {
            return;
        }
        Debug.Log(Build());
    }

    /// <summary>대화상자 없이 만든다(자동화용). 결과 요약을 돌려준다.</summary>
    public static string Build()
    {
        GameObject boyAsset = AssetDatabase.LoadAssetAtPath<GameObject>(BoyModel);
        AnimationClip idle = LoadClip(BoyModel);
        AnimationClip brrr = LoadClip(ShakeSourceModel);
        GameObject shakeSource = AssetDatabase.LoadAssetAtPath<GameObject>(ShakeSourceModel);
        if (boyAsset == null || idle == null) return "[DeathCutsceneBuilder] 소년 모델/idle 애니메이션을 찾지 못했습니다: " + BoyModel;
        if (shakeSource == null || brrr == null) return "[DeathCutsceneBuilder] 머리 흔들기 원본(brrr)을 찾지 못했습니다: " + ShakeSourceModel;

        // ── Timeline ──
        AssetDatabase.DeleteAsset(TimelinePath);
        var timeline = ScriptableObject.CreateInstance<TimelineAsset>();
        timeline.durationMode = TimelineAsset.DurationMode.FixedLength;
        timeline.fixedDuration = Duration;
        AssetDatabase.CreateAsset(timeline, TimelinePath);

        // ── 오브젝트 ──
        var root = new GameObject(DeathCutscene.ResourceName);
        var director = root.AddComponent<PlayableDirector>();
        director.playOnAwake = false;
        director.extrapolationMode = DirectorWrapMode.None;   // 끝나면 멈춰 stopped가 온다
        director.playableAsset = timeline;
        var cutscene = root.AddComponent<DeathCutscene>();

        var camRig = new GameObject("CamRig");
        camRig.transform.SetParent(root.transform, false);
        camRig.transform.localPosition = new Vector3(0f, 1.7f, 0f);   // 재생할 때 플레이어 눈높이로 맞춘다
        var camRigAnimator = camRig.AddComponent<Animator>();
        var camTarget = new GameObject("CamTarget");
        camTarget.transform.SetParent(camRig.transform, false);

        var boy = (GameObject)PrefabUtility.InstantiatePrefab(boyAsset);
        boy.name = "Boy";
        boy.transform.SetParent(root.transform, false);
        boy.transform.localPosition = BoyLocalPos;
        boy.transform.localRotation = Quaternion.Euler(0f, BoyYaw, 0f);
        var boyAnimator = boy.GetComponent<Animator>();
        if (boyAnimator == null) boyAnimator = boy.AddComponent<Animator>();
        boyAnimator.applyRootMotion = false;
        boyAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        foreach (var r in boy.GetComponentsInChildren<SkinnedMeshRenderer>(true)) r.updateWhenOffscreen = true;
        boy.SetActive(false);   // Activation 트랙이 켠다

        // 얼굴을 비추는 아주 약한 빛 — 손전등이 꺼져 있어도 얼굴이 보이게
        var faceLight = new GameObject("FaceLight");
        faceLight.transform.SetParent(boy.transform, false);
        faceLight.transform.localPosition = new Vector3(0f, 1.55f, FaceLightForward);
        var light = faceLight.AddComponent<Light>();
        light.type = LightType.Point;
        light.range = 1.1f;
        light.intensity = FaceLightIntensity;
        light.color = new Color(0.72f, 0.82f, 1f);
        light.shadows = LightShadows.None;

        // 돌아볼 때 숨막힘 화면 — 플레이어 카메라의 Volume Mask가 Viewmodel 레이어 하나뿐이라 그 레이어에 둔다
        var volumeGo = new GameObject("SuffocateVolume");
        volumeGo.transform.SetParent(root.transform, false);
        int viewmodelLayer = LayerMask.NameToLayer("Viewmodel");
        volumeGo.layer = viewmodelLayer >= 0 ? viewmodelLayer : 8;
        var volume = volumeGo.AddComponent<UnityEngine.Rendering.Volume>();
        volume.isGlobal = true;
        volume.priority = 20f;   // 실내 기본 볼륨·공포 볼륨(10~11)보다 위
        volume.weight = 0f;
        volume.sharedProfile = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.VolumeProfile>(SuffocateProfile);
        var volumeAnimator = volumeGo.AddComponent<Animator>();

        // ── 트랙 ──
        var boyActive = timeline.CreateTrack<ActivationTrack>(null, "Boy Active");
        boyActive.postPlaybackState = ActivationTrack.PostPlaybackState.Inactive;
        var activeClip = boyActive.CreateDefaultClip();
        activeClip.start = BoyAppearAt;
        activeClip.duration = Duration - activeClip.start;
        director.SetGenericBinding(boyActive, boy);

        var camTrack = timeline.CreateTrack<AnimationTrack>(null, "Camera");
        AnimationClip camClip = BuildCameraClip();
        AssetDatabase.AddObjectToAsset(camClip, timeline);
        var camTimelineClip = camTrack.CreateClip(camClip);
        camTimelineClip.start = 0;
        camTimelineClip.duration = Duration;
        camTimelineClip.displayName = "Look down · Raise · Turn & Shake";
        director.SetGenericBinding(camTrack, camRigAnimator);

        // 소년: 바탕은 idle 반복, 그 위에 머리만 흔드는 덮어쓰기 트랙(아바타 마스크 = Head 하나)
        var boyTrack = timeline.CreateTrack<AnimationTrack>(null, "Boy idle");
        boyTrack.trackOffset = TrackOffset.ApplySceneOffsets;   // 소년은 놓인 자리 그대로
        var idleClip = boyTrack.CreateClip(idle);
        idleClip.start = activeClip.start;
        idleClip.duration = Duration - activeClip.start;
        idleClip.displayName = "idle (loop)";
        SetLoop(idleClip);
        director.SetGenericBinding(boyTrack, boyAnimator);

        Transform boyHead = FindChild(boy.transform, "Head");
        AnimationClip shakeClip = BuildHeadShakeClip(boy, boyHead, idle, shakeSource, brrr);
        AssetDatabase.AddObjectToAsset(shakeClip, timeline);
        AvatarMask headMask = BuildHeadMask(boy.transform, boyHead);
        AssetDatabase.AddObjectToAsset(headMask, timeline);
        var shakeTrack = timeline.CreateTrack<AnimationTrack>(boyTrack, "Boy head shake");
        shakeTrack.avatarMask = headMask;
        shakeTrack.applyAvatarMask = true;
        var shakeTimelineClip = shakeTrack.CreateClip(shakeClip);
        shakeTimelineClip.start = ShakeStart;
        shakeTimelineClip.duration = Mathf.Min(shakeClip.length, Duration - (float)shakeTimelineClip.start);
        shakeTimelineClip.displayName = "head shake (brrr)";

        var volumeTrack = timeline.CreateTrack<AnimationTrack>(null, "Suffocate");
        AnimationClip volumeClip = BuildVolumeClip();
        AssetDatabase.AddObjectToAsset(volumeClip, timeline);
        var volumeTimelineClip = volumeTrack.CreateClip(volumeClip);
        volumeTimelineClip.start = 0;
        volumeTimelineClip.duration = Duration;
        volumeTimelineClip.displayName = "Suffocate weight";
        director.SetGenericBinding(volumeTrack, volumeAnimator);

        // ── 컷신 연결 ──
        var so = new SerializedObject(cutscene);
        so.FindProperty("director").objectReferenceValue = director;
        so.FindProperty("camRig").objectReferenceValue = camRig.transform;
        so.FindProperty("camTarget").objectReferenceValue = camTarget.transform;
        so.FindProperty("aimPoint").objectReferenceValue = FindChild(boy.transform, "Head");
        var vols = so.FindProperty("volumes");
        vols.arraySize = 1;
        vols.GetArrayElementAtIndex(0).objectReferenceValue = volume;
        so.FindProperty("designedYaw").floatValue = TurnYaw;
        so.FindProperty("designedPitch").floatValue = TurnPitch;
        so.ApplyModifiedPropertiesWithoutUndo();

        // 소리는 사운드 담당 시각표대로 따로 짠다(소리만 다시 짤 때는 메뉴 「Rebuild Death Cutscene Sounds」)
        string soundReport = DeathCutsceneSoundLayout.Apply(root, director, timeline);

        if (!AssetDatabase.IsValidFolder(PrefabFolder))
        {
            AssetDatabase.CreateFolder(Path.GetDirectoryName(PrefabFolder).Replace('\\', '/'), "Resources");
        }
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool saved);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();

        return "[DeathCutsceneBuilder] 프리팹 " + (saved ? "저장" : "실패") + " " + PrefabPath + " · Timeline " + TimelinePath +
               " · 트랙 " + timeline.outputTrackCount + "\n" + soundReport;
    }

    /// <summary>
    /// CamTarget의 회전 곡선. 두리번거리며 고개를 숙임 → 쾅 뒤 정적 속에 고개를 들어 정면 → 확 뒤돌아 얼굴 앞에서 떨린다.
    /// </summary>
    private static AnimationClip BuildCameraClip()
    {
        var clip = new AnimationClip { name = "Cam_TurnAndShake" };
        var x = new AnimationCurve();
        var y = new AnimationCurve();
        var z = new AnimationCurve();

        // 빌드업 시각은 문서(4.40초 기준) 값 — 늘어난 빌드업 길이에 맞춰 소리와 같은 비율로 늘린다
        float B(float docTime) { return docTime * DeathCutsceneSoundLayout.BuildupScale; }

        void Key(float t, float px, float py, float pz)
        {
            x.AddKey(new Keyframe(t, px));
            y.AddKey(new Keyframe(t, py));
            z.AddKey(new Keyframe(t, pz));
        }

        // ① 빌드업: 소리가 쌓이는 동안 좌우를 두리번거리며 고개를 점점 숙인다.
        //    시점이 확 꺾이는 순간(1.70·2.90·3.50)에 사물함 「철컹」이 맞춰져 있다 — 바로 앞 키에서 0.2초 만에 꺾는다.
        Key(0f, 0f, 0f, 0f);
        Key(B(0.5f), 1f, 2f, 0f);                               // 잡힌 순간 굳음
        Key(B(1.50f), 4f, -5f, 0f);
        Key(B(1.70f), 6f, -24f, -1.5f);                         // 왼쪽으로 꺾음(철컹)
        Key(B(2.10f), 8f, -20f, -1f);
        Key(B(2.70f), 11f, -16f, -0.5f);
        Key(B(2.90f), 13f, 20f, 1.5f);                          // 오른쪽으로 꺾음(철컹)
        Key(B(3.30f), 16f, 14f, 1f);
        Key(B(3.50f), 19f, -12f, -1f);                          // 다시 왼쪽(철컹)
        Key(BuildupEnd, HeadDownPitch, -3f, 0f);             // 고개를 푹 숙인 채 — 쾅

        // ② 쾅 뒤 정적: 잠시 굳었다가 천천히 고개를 들어 정면을 본다
        Key(BuildupEnd + 0.6f, HeadDownPitch + 1f, -3f, 0f);
        Key(TurnStart - 0.25f - DeathCutsceneSoundLayout.FrontHold, 0f, 0f, 0f);   // 고개를 다 듦
        // 정면을 본 채 FrontHold + 0.25초 멈춤
        Key(TurnStart, 0f, 0f, 0f);

        // ③ 정면을 보는 순간 확 뒤돈다
        Key(TurnStart + 0.12f, 3f, 40f, 2f);
        Key(TurnEnd - 0.08f, TurnPitch - 2f, TurnYaw - 10f, -2f);
        Key(TurnEnd, TurnPitch, TurnYaw, 0f);

        // 얼굴 앞에서 떨림 — 고정 시드라 매번 같은 떨림
        var rng = new System.Random(7);
        float t0 = TurnEnd + 0.06f;
        float tEnd = Mathf.Min(ShakeStart + 3.1f, Duration - 0.05f);
        for (float t = t0; t < tEnd; t += 0.06f)
        {
            float k = 1f - Mathf.Clamp01((t - t0) / (tEnd - t0)) * 0.3f;   // 끝으로 갈수록 약간 잦아듦
            Key(t,
                TurnPitch + (float)(rng.NextDouble() * 2 - 1) * 1.6f * k,
                TurnYaw + (float)(rng.NextDouble() * 2 - 1) * 1.2f * k,
                (float)(rng.NextDouble() * 2 - 1) * 2.2f * k);
        }
        Key(Duration, TurnPitch, TurnYaw, 0f);

        Smooth(x); Smooth(y); Smooth(z);
        var path = "CamTarget";
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), "localEulerAnglesRaw.x"), x);
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), "localEulerAnglesRaw.y"), y);
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), "localEulerAnglesRaw.z"), z);
        return clip;
    }

    /// <summary>숨막힘 볼륨의 세기. 정면에서 확 뒤도는 0.35초 동안 차올라 소년과 마주할 때 최대, 끝까지 유지.</summary>
    private static AnimationClip BuildVolumeClip()
    {
        var clip = new AnimationClip { name = "Suffocate_Weight" };
        var w = new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(TurnStart, 0f),
            new Keyframe(TurnEnd + 0.05f, 1f),   // 뒤도는 0.35초 동안 확 어두워짐
            new Keyframe(Duration, 1f));
        // ClampedAuto — Auto는 키 사이에서 0 아래·1 위로 출렁였다(실측 −0.17 · 1.14). 세기는 0~1을 넘으면 안 된다.
        for (int i = 0; i < w.length; i++)
        {
            AnimationUtility.SetKeyLeftTangentMode(w, i, AnimationUtility.TangentMode.ClampedAuto);
            AnimationUtility.SetKeyRightTangentMode(w, i, AnimationUtility.TangentMode.ClampedAuto);
        }
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(UnityEngine.Rendering.Volume), "weight"), w);
        return clip;
    }

    /// <summary>
    /// 키 사이를 부드럽게 잇되 ClampedAuto — 같은 값의 키 사이(멈춘 구간)는 평평하게 둔다.
    /// Auto는 정면에서 멈춘 1초 동안 뒤이은 큰 회전에 끌려 고개가 왼쪽으로 31°까지 흘렀다(2026-10-05 실측).
    /// </summary>
    private static void Smooth(AnimationCurve c)
    {
        for (int i = 0; i < c.length; i++)
        {
            AnimationUtility.SetKeyLeftTangentMode(c, i, AnimationUtility.TangentMode.ClampedAuto);
            AnimationUtility.SetKeyRightTangentMode(c, i, AnimationUtility.TangentMode.ClampedAuto);
        }
    }

    /// <summary>클립 시작 전에는 첫 프레임 자세를 유지한다(흔들리기 전 소년은 가만히 서 있다).</summary>
    private static void SetPreExtrapolationHold(TimelineClip clip)
    {
        var prop = typeof(TimelineClip).GetProperty("preExtrapolationMode");
        if (prop != null && prop.CanWrite) prop.SetValue(clip, TimelineClip.ClipExtrapolation.Hold);
    }

    /// <summary>idle 클립을 Timeline 클립 길이만큼 반복한다.</summary>
    private static void SetLoop(TimelineClip clip)
    {
        var asset = clip.asset as AnimationPlayableAsset;
        if (asset == null) return;
        var so = new SerializedObject(asset);
        SerializedProperty loop = so.FindProperty("m_Loop");   // 0 = 원본 설정, 1 = 켬, 2 = 끔
        if (loop != null) loop.intValue = 1;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// 옛 소년(brrr)의 머리 흔들기를 새 소년 뼈로 옮긴 클립. 뼈 이름·축이 달라 커브를 그대로 쓸 수 없으므로
    /// 「brrr 첫 프레임 대비 Head가 모델 공간에서 얼마나 돌았나」를 재서, 새 모델 idle 첫 자세의 Head에 같은 만큼 돌린다.
    /// 첫 프레임은 회전 0이라 idle에서 흔들기로 넘어갈 때 머리가 튀지 않는다.
    /// </summary>
    private static AnimationClip BuildHeadShakeClip(GameObject boy, Transform boyHead, AnimationClip idle, GameObject shakeSource, AnimationClip brrr)
    {
        var clip = new AnimationClip { name = "Boy_HeadShake", frameRate = 60f };
        if (boyHead == null) return clip;

        // 새 모델 기준 자세(모델 공간) — 인스턴스를 원점에 놓고 idle 첫 프레임
        GameObject target = Object.Instantiate(boy);
        target.SetActive(true);
        target.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        idle.SampleAnimation(target, 0f);
        string headPath = AnimationUtility.CalculateTransformPath(boyHead, boy.transform);
        Transform head = target.transform.Find(headPath);
        Quaternion headRest = head.rotation;
        Quaternion parentRest = head.parent.rotation;
        Object.DestroyImmediate(target);

        // 원본 brrr
        GameObject source = Object.Instantiate(shakeSource);
        source.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        Transform srcHead = FindChild(source.transform, "Head");
        brrr.SampleAnimation(source, 0f);
        Quaternion srcRestInv = Quaternion.Inverse(srcHead.rotation);

        var x = new AnimationCurve(); var y = new AnimationCurve(); var z = new AnimationCurve(); var w = new AnimationCurve();
        Quaternion prev = Quaternion.identity;
        bool first = true;
        int frames = Mathf.CeilToInt(brrr.length * clip.frameRate);

        // 원본 brrr은 한쪽으로만 꺾였다 돌아온다(실측: 기울기 0° ~ −91°, 반대쪽 0번) — 카메라에서 보면 늘 오른쪽으로만 흔들렸다.
        // 머리가 거의 똑바로 돌아온 순간(< SwingRestDeg)마다 구간을 나누고, 구간 하나 걸러 좌우를 뒤집는다(모델 YZ 평면 대칭).
        // 똑바른 자세에서 뒤집으므로 이음매가 튀지 않고, 꺾이는 세기·타이밍은 그대로다.
        const float SwingRestDeg = 4f, SwingOutDeg = 30f;
        int swing = 0;
        bool wentOut = false;
        for (int i = 0; i <= frames; i++)
        {
            float t = Mathf.Min(i / clip.frameRate, brrr.length);
            float srcTime = Mathf.Repeat(t * ShakeSpeed, brrr.length * 2f);                  // 배속 + 왕복
            if (srcTime > brrr.length) srcTime = brrr.length * 2f - srcTime;
            brrr.SampleAnimation(source, srcTime);
            Quaternion delta = srcHead.rotation * srcRestInv;                    // 모델 공간에서 돈 만큼
            float angle = Quaternion.Angle(Quaternion.identity, delta);
            if (angle > SwingOutDeg) wentOut = true;
            else if (wentOut && angle < SwingRestDeg) { swing++; wentOut = false; }
            if (swing % 2 == 1) delta = new Quaternion(delta.x, -delta.y, -delta.z, delta.w);   // 좌우 거울
            delta = Quaternion.SlerpUnclamped(Quaternion.identity, delta, ShakeAmount);         // 흔들림 크기
            Quaternion local = Quaternion.Inverse(parentRest) * (delta * headRest);
            if (!first && Quaternion.Dot(prev, local) < 0f) local = new Quaternion(-local.x, -local.y, -local.z, -local.w);   // 부호 뒤집힘 방지
            prev = local;
            first = false;
            x.AddKey(t, local.x); y.AddKey(t, local.y); z.AddKey(t, local.z); w.AddKey(t, local.w);
        }
        Object.DestroyImmediate(source);

        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(headPath, typeof(Transform), "m_LocalRotation.x"), x);
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(headPath, typeof(Transform), "m_LocalRotation.y"), y);
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(headPath, typeof(Transform), "m_LocalRotation.z"), z);
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(headPath, typeof(Transform), "m_LocalRotation.w"), w);
        return clip;
    }

    /// <summary>덮어쓰기 트랙이 Head 하나만 건드리게 하는 마스크(나머지 뼈는 idle 그대로).</summary>
    private static AvatarMask BuildHeadMask(Transform boyRoot, Transform head)
    {
        var mask = new AvatarMask { name = "Boy_HeadOnly" };
        Transform[] all = boyRoot.GetComponentsInChildren<Transform>(true);
        mask.transformCount = all.Length;
        for (int i = 0; i < all.Length; i++)
        {
            mask.SetTransformPath(i, AnimationUtility.CalculateTransformPath(all[i], boyRoot));
            mask.SetTransformActive(i, all[i] == head);
        }
        return mask;
    }

    private static Transform FindChild(Transform root, string name)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == name) return t;
        }
        return null;
    }

    private static AnimationClip LoadClip(string path)
    {
        foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(path))
        {
            if (o is AnimationClip c && !c.name.StartsWith("__preview")) return c;
        }
        return null;
    }
}

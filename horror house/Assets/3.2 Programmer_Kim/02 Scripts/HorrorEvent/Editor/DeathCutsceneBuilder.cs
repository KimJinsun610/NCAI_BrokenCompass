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
    private const string BoyModel = "Assets/2. Art/04 Materials/m_creature/m_skirtboy/Skirt_Boy_brrr.fbx";
    private const string WhisperClip = "Assets/_Game/Resources/Ambience/Stingers/stinger_whisper.ogg";
    private const string TimelinePath = "Assets/3.2 Programmer_Kim/05 Animations/Horror/DeathCutscene_Auditory.playable";
    private const string PrefabFolder = "Assets/3.2 Programmer_Kim/03 Prefebs/04 Horror/Resources";
    private const string PrefabPath = PrefabFolder + "/" + DeathCutscene.ResourceName + ".prefab";

    private const string SuffocateProfile = "Assets/3.2 Programmer_Kim/04 Materials/HorrorVolume/VP_Horror_Suffocate.asset";

    // 연출 시각(초) — 2026-10-02: 소리가 끊긴 채 두리번거리는 구간을 1.5초 → 3.6초로 늘림
    private const float Duration = 8.0f;
    private const float WhisperAt = 0.4f;
    private const float BoyAppearAt = 0.9f;
    private const float TurnStart = 3.6f;    // 오른쪽으로 돌아보기 시작(머뭇거림)
    private const float TurnEnd = 5.0f;      // 소년과 마주함
    private const float ShakeStart = 4.95f;  // brrr 3.17초 → 8.12초(Timeline 끝에서 잘림)
    private const float FaceLightIntensity = 0.2f;   // 사용자 조정값(0.7 → 0.2, 밤 씬에서 얼굴이 하얗게 떠서)

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
        AnimationClip brrr = LoadClip(BoyModel);
        AudioClip whisper = AssetDatabase.LoadAssetAtPath<AudioClip>(WhisperClip);
        if (boyAsset == null || brrr == null) return "[DeathCutsceneBuilder] 소년 모델/brrr 애니메이션을 찾지 못했습니다: " + BoyModel;

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

        var whisperGo = new GameObject("Whisper");
        whisperGo.transform.SetParent(root.transform, false);
        var whisperSource = whisperGo.AddComponent<AudioSource>();
        whisperSource.playOnAwake = false;
        whisperSource.clip = whisper;
        whisperSource.spatialBlend = 0f;        // 귀 바로 옆 — 2D로 오른쪽에 치우침
        whisperSource.panStereo = 0.8f;
        whisperSource.volume = 1f;
        whisperSource.ignoreListenerPause = true;

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
        activeClip.duration = Duration - BoyAppearAt;
        director.SetGenericBinding(boyActive, boy);

        var camTrack = timeline.CreateTrack<AnimationTrack>(null, "Camera");
        AnimationClip camClip = BuildCameraClip();
        AssetDatabase.AddObjectToAsset(camClip, timeline);
        var camTimelineClip = camTrack.CreateClip(camClip);
        camTimelineClip.start = 0;
        camTimelineClip.duration = Duration;
        camTimelineClip.displayName = "Turn & Shake";
        director.SetGenericBinding(camTrack, camRigAnimator);

        var boyTrack = timeline.CreateTrack<AnimationTrack>(null, "Boy brrr");
        boyTrack.trackOffset = TrackOffset.ApplySceneOffsets;   // 소년은 놓인 자리 그대로
        var brrrClip = boyTrack.CreateClip(brrr);
        brrrClip.start = ShakeStart;
        brrrClip.duration = brrr.length;
        brrrClip.displayName = "brrr";
        SetPreExtrapolationHold(brrrClip);
        director.SetGenericBinding(boyTrack, boyAnimator);

        if (whisper != null)
        {
            var audio = timeline.CreateTrack<AudioTrack>(null, "Whisper");
            // 처음부터 끝까지 끊기지 않게 이어 붙인다(마지막 것은 Timeline 끝에서 잘림)
            for (float t = WhisperAt; t < Duration; t += whisper.length)
            {
                var w = audio.CreateClip(whisper);
                w.start = t;
                w.duration = Mathf.Min(whisper.length, Duration - t);
            }
            director.SetGenericBinding(audio, whisperSource);
        }

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
        var own = so.FindProperty("ownAudio");
        own.arraySize = 1;
        own.GetArrayElementAtIndex(0).objectReferenceValue = whisperSource;
        so.FindProperty("aimPoint").objectReferenceValue = FindChild(boy.transform, "Head");
        var vols = so.FindProperty("volumes");
        vols.arraySize = 1;
        vols.GetArrayElementAtIndex(0).objectReferenceValue = volume;
        so.FindProperty("designedYaw").floatValue = TurnYaw;
        so.FindProperty("designedPitch").floatValue = TurnPitch;
        so.ApplyModifiedPropertiesWithoutUndo();

        if (!AssetDatabase.IsValidFolder(PrefabFolder))
        {
            AssetDatabase.CreateFolder(Path.GetDirectoryName(PrefabFolder).Replace('\\', '/'), "Resources");
        }
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool saved);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();

        return "[DeathCutsceneBuilder] 프리팹 " + (saved ? "저장" : "실패") + " " + PrefabPath + " · Timeline " + TimelinePath +
               " · 트랙 " + timeline.outputTrackCount + " · 속삭임 " + (whisper != null ? whisper.name : "없음");
    }

    /// <summary>
    /// CamTarget의 회전 곡선. 정면을 본 채 굳어 있다가 → 머뭇 → 오른쪽 어깨 너머로 확 돌아보고 → 얼굴 앞에서 떨린다.
    /// </summary>
    private static AnimationClip BuildCameraClip()
    {
        var clip = new AnimationClip { name = "Cam_TurnAndShake" };
        var x = new AnimationCurve();
        var y = new AnimationCurve();
        var z = new AnimationCurve();

        void Key(float t, float px, float py, float pz)
        {
            x.AddKey(new Keyframe(t, px));
            y.AddKey(new Keyframe(t, py));
            z.AddKey(new Keyframe(t, pz));
        }

        // 소리가 끊긴 채 두리번거림 — 소리의 출처를 찾듯 좌우로 짧게 살피다 정면에서 굳는다
        Key(0f, 0f, 0f, 0f);
        Key(0.6f, 0f, 0f, 0f);                               // 소리가 끊긴 순간 굳음
        Key(1.3f, -1f, -24f, -1f);                           // 왼쪽을 살핌
        Key(1.8f, -1f, -26f, -1f);
        Key(2.5f, 1f, 12f, 1f);                              // 오른쪽으로 조금
        Key(2.9f, 1f, 14f, 1f);
        Key(3.3f, 0f, -4f, 0f);                              // 다시 정면 — 귀 옆 속삭임에 굳음
        Key(TurnStart, 0f, -3f, 0f);
        Key(TurnStart + 0.45f, 2f, 14f, 1.5f);               // 머뭇거리며 오른쪽으로
        Key(TurnStart + 0.75f, 3f, 22f, 2f);
        Key(TurnEnd - 0.12f, TurnPitch - 2f, TurnYaw - 8f, -2f);   // 확 돌아봄
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

    /// <summary>숨막힘 볼륨의 세기. 돌아보기 시작할 때부터 차올라 소년과 마주할 때 최대, 끝까지 유지.</summary>
    private static AnimationClip BuildVolumeClip()
    {
        var clip = new AnimationClip { name = "Suffocate_Weight" };
        var w = new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(TurnStart, 0f),
            new Keyframe(TurnEnd, 1f),
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

    private static void Smooth(AnimationCurve c)
    {
        for (int i = 0; i < c.length; i++)
        {
            AnimationUtility.SetKeyLeftTangentMode(c, i, AnimationUtility.TangentMode.Auto);
            AnimationUtility.SetKeyRightTangentMode(c, i, AnimationUtility.TangentMode.Auto);
        }
    }

    /// <summary>클립 시작 전에는 첫 프레임 자세를 유지한다(흔들리기 전 소년은 가만히 서 있다).</summary>
    private static void SetPreExtrapolationHold(TimelineClip clip)
    {
        var prop = typeof(TimelineClip).GetProperty("preExtrapolationMode");
        if (prop != null && prop.CanWrite) prop.SetValue(clip, TimelineClip.ClipExtrapolation.Hold);
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

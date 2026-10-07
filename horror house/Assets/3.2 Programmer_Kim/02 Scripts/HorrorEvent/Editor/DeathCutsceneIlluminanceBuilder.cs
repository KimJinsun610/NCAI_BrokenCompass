using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using UnityEngine.UI;

/// <summary>
/// 조도 붙잡힘 컷신(<see cref="DeathCutscene"/>)의 프리팹과 Timeline을 만든다.
/// 「손전등이 깜박이다 꺼짐 → 켜 보려는 딸깍 소리 몇 번 → 점점 어두워져 완전한 암흑 → 다시 켜지는 순간 해부 모형 얼굴이 눈앞에」
///
/// <para>청각 컷신(<see cref="DeathCutsceneBuilder"/>)과 같은 틀이다 — 플레이어 카메라가 CamTarget을 따라가고,
/// 컷신 소리만 들린다. 다른 점은 <b>플레이어 손전등을 붙잡아</b> 밝기를 Timeline으로 움직이고(<c>flashlightLevel</c>),
/// 화면 전체 검은 판(<c>blackout</c>)으로 암흑을 만든다는 것.</para>
///
/// <para>해부 모형은 몹 대역 <c>mob.dummy.stand</c>(Resources/StandIns) 프리팹을 <b>그대로 중첩</b>한다 — 모델·크기·자세가
/// 게임 안의 「복도 끝에 선 자」와 같고, 대역이 바뀌면 다시 빌드만 하면 따라온다. 모형은 CamRig(눈높이) 아래라
/// 씬마다 눈높이가 달라도 얼굴(Aim)이 늘 시선 높이에 온다.</para>
///
/// 한 번 만든 뒤 Timeline 창에서 키를 고쳤다면, 메뉴를 다시 돌리면 사라지므로 이미 있으면 묻는다.
/// </summary>
public static class DeathCutsceneIlluminanceBuilder
{
    private const string DummyPrefab = "Assets/_Game/Resources/StandIns/mob.dummy.stand.prefab";
    private const string TimelinePath = "Assets/3.2 Programmer_Kim/05 Animations/Horror/DeathCutscene_Illuminance.playable";
    private const string PrefabFolder = "Assets/3.2 Programmer_Kim/03 Prefebs/04 Horror/Resources";
    private const string PrefabPath = PrefabFolder + "/" + DeathCutscene.ResourceNameIlluminance + ".prefab";

    // ── 연출 뼈대(초) ──
    public const float FlickerStart = 0.35f;   // 잡힌 직후 잠깐 멀쩡하다가 깜박이기 시작
    public const float LightDies = 1.80f;      // 손전등이 꺼짐
    public const float DarkFull = 4.60f;       // 완전한 암흑(그 사이 켜 보려는 딸깍 4번) — 플레이어 숨소리만 남음
    public const float DarkHold = 1.90f;       // 완전한 암흑 유지 시간(2026-10-05 사용자 요청 0.9 → +1초)
    public const float RevealAt = DarkFull + DarkHold;   // 6.50 — 다시 켜짐 = 해부 모형 얼굴
    public const float CutAt = RevealAt + 1.50f;         // 8.00 — 뚝 암전
    public const float Duration = CutAt + 0.30f;

    // 켜 보려는 시도: 켬 딸깍 시각 · 끔 딸깍까지 간격(점점 다급해짐)
    private static readonly float[] TryAt = { 2.35f, 2.95f, 3.45f, 3.85f };
    private static readonly float[] TryGap = { 0.20f, 0.17f, 0.14f, 0.11f };

    // 다시 켜진 손전등 밝기 배율 — 1이면 0.4m 앞 얼굴이 하얗게 떠 주름이 묻혔다(PlayScene 실측 → 0.7 → 0.28m에서 0.5).
    // 손전등은 카메라에 붙어 있어 거리 제곱으로 밝아진다 — 광각으로 0.2m 안쪽까지 당긴 뒤 다시 맞춤.
    private const float RevealLevel = 0.25f;
    // 거리 = 카메라 ~ 얼굴(Aim, 머리 가운데). 코끝은 그보다 약 11cm 앞이다.
    // 2026-10-05: 0.46→0.34 · 0.38→0.28 → 광각(사용자 요청 — 「부담스럽게 가까이」)으로 0.22→0.17.
    // 원근 왜곡은 「가까이」에서 나온다 — 코는 눈보다 카메라에 두 배 가까워 크게 부풀고, 넓힌 시야각이 얼굴 전체를 화면에 담는다.
    private const float FaceDistanceFrom = 0.22f; // 켜지는 순간 얼굴까지 거리(m)
    private const float FaceDistanceTo = 0.17f;   // 끝날 때까지 천천히 다가옴
    private const float FaceTiltTo = 0f;          // 고개 기울임(°) — 0 = 정면. 얼굴을 축으로 기운다
    private const float RevealFovFrom = 88f;      // 켜지는 순간 세로 시야각(°, 평소 50)
    private const float RevealFovTo = 100f;       // 다가오는 동안 더 넓어짐
    private const float RevealNearClip = 0.02f;   // 기본 0.1이면 코끝(카메라 약 6cm 앞)이 잘린다
    // 눈은 Aim(머리 가운데)보다 이만큼 아래다. Aim을 시선에 맞추면 눈이 화면 가운데보다 아래라 모형이 위에서 내려다보였다(캡처 실측 약 6.5cm).
    private const float EyeBelowAim = 0.065f;
    // 눈을 시선보다 이만큼 아래에 둔다(2026-10-05 사용자 요청 「살짝 아래로」). 0 = 눈이 화면 정가운데.
    private const float FaceDrop = 0.03f;

    [MenuItem("Tools/Programmer_Kim/Horror/Build Death Cutscene (Illuminance)")]
    public static void BuildMenu()
    {
        if ((File.Exists(PrefabPath) || File.Exists(TimelinePath)) &&
            !EditorUtility.DisplayDialog("사망 컷신 (조도)", "이미 있는 프리팹과 Timeline을 새로 만듭니다.\nTimeline에서 손으로 고친 키는 사라집니다.", "새로 만들기", "취소"))
        {
            return;
        }
        Debug.Log(Build());
    }

    /// <summary>대화상자 없이 만든다(자동화용). 결과 요약을 돌려준다.</summary>
    public static string Build()
    {
        GameObject dummyAsset = AssetDatabase.LoadAssetAtPath<GameObject>(DummyPrefab);
        if (dummyAsset == null) return "[DeathCutsceneIlluminanceBuilder] 해부 모형 대역을 찾지 못했습니다: " + DummyPrefab + " (메뉴 「야간근무/연출/몹 대역·소리 연결」)";

        // ── Timeline ──
        AssetDatabase.DeleteAsset(TimelinePath);
        var timeline = ScriptableObject.CreateInstance<TimelineAsset>();
        timeline.durationMode = TimelineAsset.DurationMode.FixedLength;
        timeline.fixedDuration = Duration;
        AssetDatabase.CreateAsset(timeline, TimelinePath);

        // ── 오브젝트 ──
        var root = new GameObject(DeathCutscene.ResourceNameIlluminance);
        var director = root.AddComponent<PlayableDirector>();
        director.playOnAwake = false;
        director.extrapolationMode = DirectorWrapMode.None;   // 끝나면 멈춰 stopped가 온다
        director.playableAsset = timeline;
        var cutscene = root.AddComponent<DeathCutscene>();
        var rootAnimator = root.AddComponent<Animator>();     // 손전등 밝기·암전 값(DeathCutscene 필드)을 움직인다

        var camRig = new GameObject("CamRig");
        camRig.transform.SetParent(root.transform, false);
        camRig.transform.localPosition = new Vector3(0f, 1.7f, 0f);   // 재생할 때 플레이어 눈높이로 맞춘다
        var camRigAnimator = camRig.AddComponent<Animator>();
        var camTarget = new GameObject("CamTarget");
        camTarget.transform.SetParent(camRig.transform, false);

        // 해부 모형 — 눈높이 받침 아래, 얼굴(Aim)이 시선 높이에 오게 내리고 카메라를 마주 보게 돌린다
        var dummy = (GameObject)PrefabUtility.InstantiatePrefab(dummyAsset);
        dummy.name = "Dummy";
        dummy.transform.SetParent(camRig.transform, false);
        Transform aim = dummy.transform.Find("Aim");
        Vector3 aimLocal = aim != null ? aim.localPosition : new Vector3(0f, 1.49f, 0f);
        Vector3 eyePoint = aimLocal - new Vector3(0f, EyeBelowAim, 0f);   // 모형 공간의 눈높이 점
        float faceYaw = MeasureFaceYaw(dummy.transform);
        dummy.transform.localRotation = Quaternion.Euler(0f, 180f - faceYaw, 0f);
        dummy.transform.localPosition = new Vector3(0f, -FaceDrop, FaceDistanceFrom) - dummy.transform.localRotation * eyePoint;
        foreach (var r in dummy.GetComponentsInChildren<SkinnedMeshRenderer>(true)) r.updateWhenOffscreen = true;
        foreach (var c in dummy.GetComponentsInChildren<Collider>(true)) c.enabled = false;
        dummy.SetActive(false);   // Activation 트랙이 켠다

        // 화면 전체 검은 판 — 손전등이 꺼진 뒤 점점 어두워져 완전한 암흑
        var canvasGo = new GameObject("BlackoutCanvas");
        canvasGo.transform.SetParent(root.transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;   // HUD(시계·조준선)까지 덮는다
        var imageGo = new GameObject("Black");
        imageGo.transform.SetParent(canvasGo.transform, false);
        var rect = imageGo.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        var image = imageGo.AddComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0f);
        image.raycastTarget = false;
        image.enabled = false;

        // ── 트랙 ──
        var dummyActive = timeline.CreateTrack<ActivationTrack>(null, "Dummy Active");
        dummyActive.postPlaybackState = ActivationTrack.PostPlaybackState.Inactive;
        var activeClip = dummyActive.CreateDefaultClip();
        activeClip.start = RevealAt - 0.05f;   // 암흑 속이라 안 보일 때 미리 세움
        activeClip.duration = CutAt - activeClip.start;
        director.SetGenericBinding(dummyActive, dummy);

        var camTrack = timeline.CreateTrack<AnimationTrack>(null, "Camera · Dummy");
        AnimationClip camClip = BuildCameraClip(eyePoint, faceYaw);
        AssetDatabase.AddObjectToAsset(camClip, timeline);
        var camTimelineClip = camTrack.CreateClip(camClip);
        camTimelineClip.start = 0;
        camTimelineClip.duration = Duration;
        camTimelineClip.displayName = "Freeze · Look around in dark · Face";
        director.SetGenericBinding(camTrack, camRigAnimator);

        var fxTrack = timeline.CreateTrack<AnimationTrack>(null, "Flashlight · Blackout");
        fxTrack.trackOffset = TrackOffset.ApplySceneOffsets;   // 루트 Animator — 플레이어 발밑에 놓인 자리를 건드리지 않게
        AnimationClip fxClip = BuildFxClip();
        AssetDatabase.AddObjectToAsset(fxClip, timeline);
        var fxTimelineClip = fxTrack.CreateClip(fxClip);
        fxTimelineClip.start = 0;
        fxTimelineClip.duration = Duration;
        fxTimelineClip.displayName = "flashlightLevel · blackout";
        director.SetGenericBinding(fxTrack, rootAnimator);

        // ── 소리 ──
        var missing = new List<string>();
        var own = new List<AudioSource>();
        int clipCount = 0;
        var soundsGo = new GameObject("Sounds");
        soundsGo.transform.SetParent(root.transform, false);
        foreach (Lane lane in BuildLanes())
        {
            var go = new GameObject(lane.name);
            go.transform.SetParent(soundsGo.transform, false);
            AudioSource src = NewSource(go);
            own.Add(src);
            var track = timeline.CreateTrack<AudioTrack>(null, "SFX " + lane.name);
            director.SetGenericBinding(track, src);
            foreach (Cue cue in lane.cues)
            {
                AudioClip clip = DeathCutsceneSoundLayout.FindClip(cue.clip);
                if (clip == null)
                {
                    if (!missing.Contains(cue.clip)) missing.Add(cue.clip);
                    continue;
                }
                if (Place(track, clip, cue)) clipCount++;
            }
        }

        // 게임으로 돌아올 때(디버그 복구) — 청각 컷신과 같은 「흡」 + 떨리는 숨
        var returns = new List<AudioSource>();
        var returnGroup = new GameObject("Return").transform;
        returnGroup.SetParent(soundsGo.transform, false);
        foreach (var r in new[] { new { clip = "CAP-COM-09_2", vol = 0.8f }, new { clip = "RST-04_2", vol = 0.9f } })
        {
            AudioClip clip = DeathCutsceneSoundLayout.FindClip(r.clip);
            if (clip == null) { missing.Add(r.clip); continue; }
            var go = new GameObject("Return " + r.clip);
            go.transform.SetParent(returnGroup, false);
            AudioSource src = NewSource(go);
            src.clip = clip;
            src.volume = r.vol;
            src.ignoreListenerPause = false;   // 게임 소리와 함께 돌아오는 소리
            returns.Add(src);
        }

        // ── 컷신 연결 ──
        var so = new SerializedObject(cutscene);
        so.FindProperty("director").objectReferenceValue = director;
        so.FindProperty("camRig").objectReferenceValue = camRig.transform;
        so.FindProperty("camTarget").objectReferenceValue = camTarget.transform;
        so.FindProperty("aimPoint").objectReferenceValue = null;   // 정면을 본 채라 시선 보정 없음
        so.FindProperty("designedYaw").floatValue = 0f;
        so.FindProperty("designedPitch").floatValue = 0f;
        so.FindProperty("controlFlashlight").boolValue = true;
        so.FindProperty("flashlightLevel").floatValue = 1f;
        so.FindProperty("blackout").floatValue = 0f;
        so.FindProperty("blackoutImage").objectReferenceValue = image;
        so.FindProperty("fieldOfView").floatValue = 0f;
        so.FindProperty("nearClip").floatValue = RevealNearClip;
        so.FindProperty("debugKey").intValue = (int)KeyCode.F7;
        SetArray(so.FindProperty("ownAudio"), own);
        SetArray(so.FindProperty("returnAudio"), returns);
        so.ApplyModifiedPropertiesWithoutUndo();

        if (!AssetDatabase.IsValidFolder(PrefabFolder))
        {
            AssetDatabase.CreateFolder(Path.GetDirectoryName(PrefabFolder).Replace('\\', '/'), "Resources");
        }
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool saved);
        UnityEngine.Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();

        return "[DeathCutsceneIlluminanceBuilder] 프리팹 " + (saved ? "저장" : "실패") + " " + PrefabPath + " · Timeline " + TimelinePath +
               " · 트랙 " + timeline.outputTrackCount + " · 소리 클립 " + clipCount +
               (missing.Count > 0 ? " · <b>없는 클립: " + string.Join(", ", missing) + "</b>" : " · 빠진 클립 없음");
    }

    // ─────────────────────────────── 화면 ───────────────────────────────

    /// <summary>
    /// 손전등 밝기(flashlightLevel)와 검은 판(blackout). 깜박임은 계단(Constant)으로 — 부드럽게 이으면 깜박임이 아니라 일렁임이 된다.
    /// </summary>
    private static AnimationClip BuildFxClip()
    {
        var clip = new AnimationClip { name = "Flashlight_Blackout" };

        // 손전등: 멀쩡 → 깜박깜박(점점 잦고 약하게) → 꺼짐 → (켜 보려 해도 안 켜짐) → 켜짐 → 뚝
        var lamp = new AnimationCurve();
        float[,] keys =
        {
            { 0f, 1f }, { FlickerStart, 1f },
            { 0.42f, 0.15f }, { 0.47f, 1f },
            { 0.66f, 0.9f }, { 0.72f, 0.05f }, { 0.76f, 0.8f }, { 0.82f, 0.1f }, { 0.88f, 1f },
            { 1.10f, 0.7f }, { 1.16f, 0f }, { 1.30f, 0f }, { 1.34f, 0.6f }, { 1.40f, 0.1f }, { 1.46f, 0.85f },
            { 1.56f, 0.3f }, { 1.61f, 0.7f }, { 1.68f, 0.12f }, { 1.73f, 0.4f },
            { LightDies, 0f },
            { RevealAt, RevealLevel },
            { CutAt, 0f },
        };
        for (int i = 0; i < keys.GetLength(0); i++) lamp.AddKey(new Keyframe(keys[i, 0], keys[i, 1]));
        Stepped(lamp);

        // 검은 판: 꺼진 뒤 점점 어두워져 완전한 암흑 → 켜지는 순간 걷힘 → 끝에 뚝 암전
        var black = new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(LightDies, 0f),
            new Keyframe(LightDies + (DarkFull - LightDies) * 0.5f, 0.6f),
            new Keyframe(DarkFull, 1f),
            new Keyframe(RevealAt, 0f),
            new Keyframe(CutAt, 1f));
        for (int i = 0; i < black.length; i++)
        {
            AnimationUtility.SetKeyLeftTangentMode(black, i, AnimationUtility.TangentMode.ClampedAuto);
            AnimationUtility.SetKeyRightTangentMode(black, i, AnimationUtility.TangentMode.ClampedAuto);
        }
        // 암흑을 유지하다 켜지는 순간 0으로, 끝은 그 순간 1로 — 그 두 구간만 계단
        AnimationUtility.SetKeyRightTangentMode(black, 3, AnimationUtility.TangentMode.Constant);
        AnimationUtility.SetKeyLeftTangentMode(black, 4, AnimationUtility.TangentMode.Constant);
        AnimationUtility.SetKeyRightTangentMode(black, 4, AnimationUtility.TangentMode.Constant);
        AnimationUtility.SetKeyLeftTangentMode(black, 5, AnimationUtility.TangentMode.Constant);

        // 시야각: 그때까지는 원래 값(0) → 완전한 암흑 속에서 광각으로 바꿔 두고(안 보임), 다가오는 동안 더 넓어짐
        var fov = new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(RevealAt - 0.15f, RevealFovFrom),
            new Keyframe(CutAt, RevealFovTo));
        AnimationUtility.SetKeyRightTangentMode(fov, 0, AnimationUtility.TangentMode.Constant);
        AnimationUtility.SetKeyLeftTangentMode(fov, 1, AnimationUtility.TangentMode.Constant);
        AnimationUtility.SetKeyRightTangentMode(fov, 1, AnimationUtility.TangentMode.Linear);
        AnimationUtility.SetKeyLeftTangentMode(fov, 2, AnimationUtility.TangentMode.Linear);

        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(DeathCutscene), "flashlightLevel"), lamp);
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(DeathCutscene), "blackout"), black);
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(DeathCutscene), "fieldOfView"), fov);
        return clip;
    }

    /// <summary>
    /// CamTarget 회전 + 모형 위치·기울기(둘 다 CamRig 아래라 한 클립).
    /// 잡힌 순간 굳음 → 깜박임에 움찔 → 암흑 속에서 두리번 → 정면에 멈춘 채 켜짐 → 움찔 물러서며 떨림.
    /// </summary>
    private static AnimationClip BuildCameraClip(Vector3 eyePoint, float faceYaw)
    {
        var clip = new AnimationClip { name = "Cam_Dark_Face" };
        var x = new AnimationCurve();
        var y = new AnimationCurve();
        var z = new AnimationCurve();
        void Key(float t, float px, float py, float pz)
        {
            x.AddKey(new Keyframe(t, px));
            y.AddKey(new Keyframe(t, py));
            z.AddKey(new Keyframe(t, pz));
        }

        Key(0f, 0f, 0f, 0f);
        Key(0.5f, 1f, 1f, 0f);            // 굳음
        Key(1.16f, 2f, -2f, -0.5f);       // 깜박임에 움찔
        Key(LightDies, 3f, -1f, 0f);      // 꺼짐
        Key(2.40f, 2f, -14f, -1f);        // 암흑 속 두리번(켜 보려 딸깍거리며)
        Key(3.00f, 0f, 11f, 1f);
        Key(3.50f, 4f, -7f, 0f);
        Key(3.90f, 1f, 4f, 0f);
        Key(DarkFull, 2f, 0f, 0f);
        Key(RevealAt - 0.4f, 0f, 0f, 0f);  // 완전한 암흑 속에서 정면으로 돌아와 멈춤(안 보임)
        Key(RevealAt, 0f, 0f, 0f);
        Key(RevealAt + 0.07f, -2f, 0.5f, 1f);   // 켜지는 순간 움찔(고개가 뒤로)

        // 떨림은 정면(0°)을 중심으로 — 얼굴과 눈을 똑바로 마주한다
        var rng = new System.Random(11);
        for (float t = RevealAt + 0.13f; t < CutAt; t += 0.06f)
        {
            Key(t,
                (float)(rng.NextDouble() * 2 - 1) * 0.9f,
                (float)(rng.NextDouble() * 2 - 1) * 0.8f,
                (float)(rng.NextDouble() * 2 - 1) * 1.2f);
        }
        Key(CutAt, 0f, 0f, 0f);
        Key(Duration, 0f, 0f, 0f);
        Smooth(x); Smooth(y); Smooth(z);

        const string cam = "CamTarget";
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(cam, typeof(Transform), "localEulerAnglesRaw.x"), x);
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(cam, typeof(Transform), "localEulerAnglesRaw.y"), y);
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(cam, typeof(Transform), "localEulerAnglesRaw.z"), z);

        // 모형: 켜진 뒤 천천히 다가옴(+ 기울임 — 지금은 0).
        // 돌림은 눈높이 점을 축으로 — 모형 루트는 발밑이라 루트째 돌리면 얼굴이 옆으로 0.2m 넘게 밀려 화면 밖으로 나갔다(실측).
        // 그래서 시각마다 「눈이 시선 정면 (0, 0, 거리)에 오는 루트 위치」를 계산해 키로 굽는다.
        // faceYaw = 대역 자세에서 머리가 몸 기준으로 돌아간 각 — 그만큼 반대로 돌려 얼굴이 카메라를 정면으로 본다.
        const string dummy = "Dummy";
        float yaw = 180f - faceYaw;
        var distance = new AnimationCurve(new Keyframe(0f, FaceDistanceFrom), new Keyframe(RevealAt, FaceDistanceFrom), new Keyframe(CutAt, FaceDistanceTo));
        var tiltOf = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(RevealAt, 0f), new Keyframe(CutAt, FaceTiltTo));
        Smooth(distance); Smooth(tiltOf);
        var px = new AnimationCurve(); var py = new AnimationCurve(); var pz = new AnimationCurve(); var rz = new AnimationCurve();
        for (float t = 0f; ; t += 0.1f)
        {
            if (t > Duration) t = Duration;
            float tilt = tiltOf.Evaluate(t);
            Vector3 face = Quaternion.Euler(0f, yaw, tilt) * eyePoint;
            Vector3 pos = new Vector3(0f, -FaceDrop, distance.Evaluate(t)) - face;
            px.AddKey(t, pos.x); py.AddKey(t, pos.y); pz.AddKey(t, pos.z); rz.AddKey(t, tilt);
            if (t >= Duration) break;
        }
        Smooth(px); Smooth(py); Smooth(pz); Smooth(rz);
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(dummy, typeof(Transform), "m_LocalPosition.x"), px);
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(dummy, typeof(Transform), "m_LocalPosition.y"), py);
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(dummy, typeof(Transform), "m_LocalPosition.z"), pz);
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(dummy, typeof(Transform), "localEulerAnglesRaw.x"), AnimationCurve.Constant(0f, Duration, 0f));
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(dummy, typeof(Transform), "localEulerAnglesRaw.y"), AnimationCurve.Constant(0f, Duration, yaw));
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(dummy, typeof(Transform), "localEulerAnglesRaw.z"), rz);
        return clip;
    }

    /// <summary>
    /// 대역 자세에서 머리가 몸 정면(+Z)에서 좌우로 돌아간 각(°, +면 모형의 +X 쪽).
    /// mob.dummy.stand는 머리가 약 17° 돌아가 있어 몸을 카메라에 맞추면 얼굴이 틀어져 보였다(2026-10-05 머리 뼈·코끝 방향 실측 16.6~17°).
    /// </summary>
    private static float MeasureFaceYaw(Transform dummyRoot)
    {
        Transform head = null;
        foreach (Transform t in dummyRoot.GetComponentsInChildren<Transform>(true))
        {
            if (string.Equals(t.name, "head", StringComparison.OrdinalIgnoreCase)) { head = t; break; }
        }
        if (head == null) return 0f;
        Vector3 f = dummyRoot.InverseTransformDirection(head.forward);
        return Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
    }

    private static void Smooth(AnimationCurve c)
    {
        for (int i = 0; i < c.length; i++)
        {
            AnimationUtility.SetKeyLeftTangentMode(c, i, AnimationUtility.TangentMode.ClampedAuto);
            AnimationUtility.SetKeyRightTangentMode(c, i, AnimationUtility.TangentMode.ClampedAuto);
        }
    }

    private static void Stepped(AnimationCurve c)
    {
        for (int i = 0; i < c.length; i++)
        {
            AnimationUtility.SetKeyLeftTangentMode(c, i, AnimationUtility.TangentMode.Constant);
            AnimationUtility.SetKeyRightTangentMode(c, i, AnimationUtility.TangentMode.Constant);
        }
    }

    // ─────────────────────────────── 소리 ───────────────────────────────

    private enum Align { Peak, Start }

    private struct Cue
    {
        public string clip;
        public float at;          // Peak = 가장 큰 지점이 올 시각 · Start = 클립 시작 시각
        public float until;
        public float volume;
        public Align align;
        public float offset;      // Peak: 가장 큰 지점보다 얼마나 일찍 시작할지 · Start: clipIn
        public float easeIn;
        public float easeOut;
    }

    private static Cue C(string clip, float at, float until, float volume, Align align, float offset = 0f, float easeIn = 0f, float easeOut = 0f)
    {
        return new Cue { clip = clip, at = at, until = until, volume = volume, align = align, offset = offset, easeIn = easeIn, easeOut = easeOut };
    }

    private class Lane
    {
        public string name;
        public List<Cue> cues = new List<Cue>();
    }

    /// <summary>
    /// 시각표. 딸깍 소리는 플레이어 손전등과 같은 소리(SFX_COMMON_FlashlightClick_on/off)를 쓴다 — 늘 듣던 그 딸깍이 안 먹혀야 무섭다.
    /// 완전한 암흑(DarkFull)에서 모든 소리가 끊기고, 다시 켜지는 딸깍(RevealAt)과 얼굴 충격음이 같은 순간에 터진다.
    /// </summary>
    private static List<Lane> BuildLanes()
    {
        var lanes = new List<Lane>();
        Lane L(string name) { var l = new Lane { name = name }; lanes.Add(l); return l; }

        // ① 깜박임 — 지직거리는 접촉 불량, 꺼질 때 「퍽」
        L("Flicker").cues.Add(C("SFX_Flashlight_Flicker_1", FlickerStart - 0.05f, LightDies + 0.02f, 1f, Align.Start, 0f, 0f, 0.05f));
        // 깜박임 바탕(Flicker_1)은 작게 지직거릴 뿐이라(RMS 0.001~0.008), 빛이 크게 끊기는 순간마다 「팍」을 얹는다
        Lane pops = L("Flicker pops");
        pops.cues.Add(C("SFX_Flashlight_Flicker_3", 0.42f, 0.70f, 0.8f, Align.Peak, 0.02f, 0f, 0.1f));
        pops.cues.Add(C("SFX_Flashlight_Flicker_3", 1.16f, 1.40f, 0.8f, Align.Peak, 0.02f, 0f, 0.1f));
        pops.cues.Add(C("SFX_Flashlight_Flicker_3", 1.68f, LightDies - 0.03f, 0.6f, Align.Peak, 0.02f, 0f, 0.05f));
        L("Die").cues.Add(C("SFX_Flashlight_Flicker_2", LightDies, LightDies + 0.8f, 0.7f, Align.Peak, 0.02f, 0f, 0.4f));
        L("Die tail").cues.Add(C("CAP-COM-12_2", LightDies, LightDies + 1.2f, 1f, Align.Peak, 0.02f, 0f, 0.5f));

        // ② 켜 보려는 딸깍 — 꺼진 직후 반사적으로 한 번 + 네 번(켬·끔), 점점 다급해짐
        Lane on = L("Click on");
        Lane off = L("Click off");
        off.cues.Add(C("SFX_COMMON_FlashlightClick_off", LightDies + 0.12f, LightDies + 0.45f, 0.9f, Align.Peak, 0.03f));
        for (int i = 0; i < TryAt.Length; i++)
        {
            on.cues.Add(C("SFX_COMMON_FlashlightClick_on", TryAt[i], TryAt[i] + 0.35f, 0.9f, Align.Peak, 0.03f));
            off.cues.Add(C("SFX_COMMON_FlashlightClick_off", TryAt[i] + TryGap[i], TryAt[i] + TryGap[i] + 0.35f, 0.9f, Align.Peak, 0.03f));
        }

        // ③ 어두워지는 동안 — 낮은 어둠 웅웅거림이 차오르고 숨이 가빠짐. 완전한 암흑에서 둘 다 뚝
        L("Dark drone").cues.Add(C("CAP-S3-07_2", LightDies + 0.1f, DarkFull + 0.02f, 0.55f, Align.Start, 0f, 2.0f, 0.06f));
        L("Breath").cues.Add(C("CAP-COM-10_2", LightDies + 0.25f, DarkFull + 0.02f, 0.5f, Align.Start, 0f, 0f, 0.06f));
        // 완전한 암흑 — 다른 소리는 모두 끊기고 플레이어 거친 숨(몸 계기와 같은 소리)만. 켜지는 딸깍에서 뚝
        L("Dark breath").cues.Add(C("SFX_BODY_RoughBreath_01", DarkFull, RevealAt, 0.9f, Align.Start, 0.15f, 0.08f, 0.02f));

        // ④ 다시 켜짐 — 딸깍과 얼굴 충격음(저음·모형 중음·지직·비명)이 같은 순간. CutAt에서 모두 끊김
        L("Reveal click").cues.Add(C("CLX-24_2", RevealAt, RevealAt + 0.6f, 1f, Align.Peak, 0.03f));
        L("Reveal low").cues.Add(C("CLX-04_3", RevealAt, CutAt, 1f, Align.Peak, 0.04f, 0f, 0.15f));
        L("Reveal mid").cues.Add(C("CLX-09_3", RevealAt, CutAt, 0.9f, Align.Peak, 0.04f, 0f, 0.15f));
        L("Reveal static").cues.Add(C("CAP-COM-05_2", RevealAt - 0.02f, RevealAt + 0.3f, 0.5f, Align.Start, 0.05f, 0f, 0.1f));
        L("Reveal scream").cues.Add(C("CLX-03_3", RevealAt + 0.02f, CutAt, 0.55f, Align.Start, 0.1f, 0f, 0.08f));
        return lanes;
    }

    /// <summary>cue 하나를 트랙에 놓는다. Peak면 클립의 가장 큰 지점(10ms RMS)이 cue.at에 오도록 clipIn을 잡는다.</summary>
    private static bool Place(AudioTrack track, AudioClip clip, Cue cue)
    {
        double clipIn, start;
        if (cue.align == Align.Peak)
        {
            var info = DeathCutsceneSoundLayout.WavInfo.Read(AssetDatabase.GetAssetPath(clip));
            clipIn = Math.Max(0.0, info.peak - cue.offset);
            start = cue.at - (info.peak - clipIn);
        }
        else
        {
            clipIn = cue.offset;
            start = cue.at;
        }
        if (start < 0) { clipIn -= start; start = 0; }
        double duration = Math.Min(cue.until - start, clip.length - clipIn);
        if (duration <= 0.01) return false;

        TimelineClip tc = track.CreateClip(clip);
        tc.start = start;
        tc.clipIn = clipIn;
        tc.duration = duration;
        tc.easeInDuration = Math.Min(cue.easeIn, duration * 0.9);
        tc.easeOutDuration = Math.Min(cue.easeOut, duration - tc.easeInDuration);
        tc.displayName = clip.name;

        var asset = tc.asset as AudioPlayableAsset;
        if (asset != null)
        {
            var so = new SerializedObject(asset);
            SerializedProperty vol = so.FindProperty("m_ClipProperties.volume");
            if (vol != null) vol.floatValue = cue.volume;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        return true;
    }

    private static AudioSource NewSource(GameObject go)
    {
        var src = go.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.spatialBlend = 0f;
        src.volume = 1f;
        src.ignoreListenerPause = true;   // 모든 소리를 끊은 뒤에도 컷신 소리는 들린다
        return src;
    }

    private static void SetArray<T>(SerializedProperty prop, List<T> items) where T : UnityEngine.Object
    {
        if (prop == null) return;
        prop.arraySize = items.Count;
        for (int i = 0; i < items.Count; i++) prop.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
    }
}

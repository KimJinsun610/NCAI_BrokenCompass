using System;
using System.Collections;
using System.Collections.Generic;
using NightDuty;
using UnityEngine;

/// <summary>붙잡힘 연출기의 축별 장면 부분 — 연출표(<see cref="CaptureCastSO"/>)의 프리팹 장면 또는 기본 얼굴 장면.</summary>
public sealed partial class CaptureDirector
{
    private int _faceLayer = -1;
    private Transform _mount;
    private Camera _mountLens;
    private Camera _sceneCam;
    private bool _sceneDone;
    private bool _sceneHasDoneEvent;

    /// <summary>마지막 장면을 어떻게 틀고 끝냈는지(디버그·검수).</summary>
    public string LastScene { get; private set; }

    /// <summary>장면 프리팹이 <c>CaptureCue(문자열)</c> 이벤트를 보냈다.</summary>
    public event Action<string> SceneCued;

    /// <summary>
    /// 장면만 미리 본다(재시작·카드 없이). <paramref name="repeat"/> = 몇 번째 붙잡힘처럼 틀지(2 = 짧게, 3 = 건너뛰기 가능).
    /// 이미 진행 중이면 false. 끝나면 플레이어 방향을 되돌린다.
    /// </summary>
    public bool Preview(FearAxis axis, int repeat = 1, string sourceId = null)
    {
        if (_running) return false;
        _previewSource = sourceId;   // 64차: 개발자 모드가 「인체 모형 응시로 붙잡힘」 장면을 미리 볼 때(stare.rule.S5.figure, 70차)
        StartCoroutine(PreviewRun(axis, Mathf.Max(1, repeat)));
        return true;
    }

    private string _previewSource;

    /// <summary>이번 장면의 김진선님 컷신 — 미리 보기 출처가 있으면 그것, 아니면 지금 붙잡힌 출처로 고른다.</summary>
    private DeathCutscene CutsceneFor(FearAxis axis)
    {
        string source = _previewSource ?? (NightRun.IsCaptured ? NightRun.Cause.SourceId : null);
        return CutscenePrefab(axis, source);
    }

    private IEnumerator PreviewRun(FearAxis axis, int count)
    {
        _running = true;
        FPController player = FindAnyObjectByType<FPController>();
        GameTime clock = FindAnyObjectByType<GameTime>();
        if (clock != null) clock.Hold(this);
        if (player != null) player.enabled = false;
        Quaternion facing = player != null ? player.transform.rotation : Quaternion.identity;

        EnsureUi();
        HideOtherHud();
        yield return PreCapture(axis, count);
        AudioListener.pause = true;
        yield return PlayScene(axis, count, player);
        AudioListener.pause = false;
        EndCaptureSounds();
        if (player != null) player.transform.rotation = facing;
        yield return FadeFromBlack();
        RestoreOtherHud();
        if (player != null) player.enabled = true;
        if (clock != null) clock.Release(this);
        _previewSource = null;
        _running = false;
    }

    /// <summary>
    /// 피날레 K4 위반(11단계): 그 축의 붙잡힘 장면만 틀고 출근 자리(경비실)로 돌려놓는다 — 재시작·카드 없음(축·재시작 횟수는 그대로).
    /// 끝나면 화면이 다시 밝아져 있다. 이미 진행 중이면 곧바로 끝난다.
    /// </summary>
    public IEnumerator PlayFinaleCapture(FearAxis axis)
    {
        if (_running) yield break;
        _running = true;
        FPController player = FindAnyObjectByType<FPController>();
        GameTime clock = FindAnyObjectByType<GameTime>();
        if (clock != null) clock.Hold(this);
        if (player != null) player.enabled = false;

        EnsureUi();
        HideOtherHud();
        yield return PreCapture(axis, 1);
        AudioListener.pause = true;
        yield return PlayScene(axis, 1, player);
        MovePlayerToStart(player);
        AudioListener.pause = false;
        EndCaptureSounds();
        yield return new WaitForSecondsRealtime(0.6f);
        yield return FadeFromBlack();
        RestoreOtherHud();
        if (player != null) player.enabled = true;
        if (clock != null) clock.Release(this);
        _running = false;
    }

    /// <summary>플레이어를 출근 자리(첫 프레임 위치·방향 — 경비실)로 옮긴다. 피날레 강제 복귀가 쓴다.</summary>
    public void TeleportPlayerToStart()
    {
        MovePlayerToStart(FindAnyObjectByType<FPController>());
    }

    private void LateUpdate()
    {
        // 장면 프리팹의 CameraMount를 따른다(애니메이션 평가 뒤라 한 프레임 늦지 않는다).
        if (_mount == null || _sceneCam == null) return;
        _sceneCam.transform.SetPositionAndRotation(_mount.position, _mount.rotation);
        if (_mountLens != null) _sceneCam.fieldOfView = _mountLens.fieldOfView;
    }

    /// <summary>정적·암전 → 장면 → 암전. 끝나면 화면은 검고, 손전등은 붙잡히기 전으로 돌아가 있다.</summary>
    private IEnumerator PlayScene(FearAxis axis, int count, FPController player)
    {
        // 46차: 김진선님 축 사망 컷신(청각·조도)이 있으면 그것을 튼다 — 자기 빌드업·소리·카메라를 다 가진 Timeline이라 공용 정적·얼굴 장면은 건너뛴다.
        DeathCutscene cutscene = CutsceneFor(axis);
        if (cutscene != null)
        {
            bool played = false;
            yield return PlayCutscene(cutscene, axis, count, player, ok => played = ok);
            if (played) yield break;
        }

        CaptureCastSO.Entry e = CaptureCastSO.Load().Get(axis);
        bool skippable = count >= 3;
        Camera cam = Camera.main;
        FlashlightRelay flashlight = FlashlightRelay.Active;
        bool flashlightWasOn = flashlight != null && flashlight.IsOn;
        if (e.flashlightOff && flashlight != null) flashlight.SetOn(false);

        // ② 정적·암전 — 그동안 돌아보게 하고 장면을 세운다
        SetBlack(1f);
        SceneSoundsCut(axis, e.silenceSeconds);
        if (e.turnAround && player != null) player.transform.Rotate(0f, 180f, 0f, Space.World);

        bool usePrefab = e.prefab != null && cam != null;
        GameObject scene = null;
        GameObject lamp = null;
        float faceDist = faceDistance;
        _faceLayer = e.darkWorld ? FreeLayer() : -1;
        if (usePrefab) scene = SpawnScene(e, cam, player, count);
        else if (cam != null) scene = SpawnFace(e, cam, flashlight, out lamp, out faceDist);
        LastScene = CaptureCastSO.Label(axis) + " · " + (usePrefab ? "프리팹 " + e.prefab.name : "기본 얼굴 " + LastFaceId);

        bool skipped = false;
        yield return Wait(e.silenceSeconds, skippable, () => skipped = true);

        // ③ 장면
        if (!skipped && scene != null)
        {
            Vector3 camLocalPos = cam.transform.localPosition;
            Quaternion camLocalRot = cam.transform.localRotation;
            float fov = cam.fieldOfView;
            int mask = cam.cullingMask;
            CameraClearFlags clear = cam.clearFlags;
            Color background = cam.backgroundColor;

            // 어둠 속에 장면만 — 좁은 방에서 벽·소품이 가리지 않게 장면 층만 그린다.
            if (_faceLayer >= 0)
            {
                cam.cullingMask = 1 << _faceLayer;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.black;
            }

            // 손에 든 태블릿(뷰모델 카메라) 같은 다른 카메라도 그동안 끈다.
            List<Camera> others = new List<Camera>();
            Camera[] cams = FindObjectsByType<Camera>(FindObjectsSortMode.None);
            for (int i = 0; i < cams.Length; i++)
            {
                if (cams[i] == cam || !cams[i].enabled) continue;
                cams[i].enabled = false;
                others.Add(cams[i]);
            }

            SetBlack(0f);
            PlaySceneSound(e.revealSound, scene.transform.position + Vector3.up * 1.5f);
            SceneSoundsReveal(axis);

            if (usePrefab)
            {
                yield return RunPrefabScene(e, scene, cam, skippable);
            }
            else
            {
                if (e.flashlightOff && flashlight != null) flashlight.SetOn(true);
                cam.fieldOfView = Mathf.Clamp(2f * Mathf.Atan(faceHalfHeight / faceDist) * Mathf.Rad2Deg, 18f, fov);
                float seconds = count >= 2 ? e.faceSeconds * 0.5f : e.faceSeconds;
                float t = 0f;
                while (t < seconds)
                {
                    t += Time.unscaledDeltaTime;
                    DimFlashlightForFace(cam, e.flashlightOff ? FaceFlashlightDimRevealed : 0f);
                    if (e.pullDistance > 0f)
                    {
                        // 가지 사이로 끌려 들어간다
                        cam.transform.localPosition = camLocalPos + Vector3.forward * (e.pullDistance * Mathf.SmoothStep(0f, 1f, t / Mathf.Max(0.01f, seconds)));
                    }

                    if (skippable && Input.anyKeyDown) break;
                    yield return null;
                }

                LastScene += " → " + seconds.ToString("0.00") + "초";
            }

            _mount = null;
            cam.transform.localPosition = camLocalPos;
            cam.transform.localRotation = camLocalRot;
            cam.fieldOfView = fov;
            cam.cullingMask = mask;
            cam.clearFlags = clear;
            cam.backgroundColor = background;
            for (int i = 0; i < others.Count; i++)
            {
                if (others[i] != null) others[i].enabled = true;
            }
        }
        else if (skipped)
        {
            LastScene += " → 건너뜀";
            SceneSoundsSkipped();
        }

        SetBlack(1f);
        SceneSoundsBlack(axis);
        if (scene != null) Destroy(scene);
        if (lamp != null) Destroy(lamp);
        if (flashlight != null) flashlight.SetOn(flashlightWasOn);
    }

    // ── 김진선님 사망 컷신 ─────────────────────────────────────

    /// <summary>
    /// 그 축의 김진선님 사망 컷신 프리팹(청각 <c>Resources/DeathCutscene_Auditory</c> · 조도 <c>DeathCutscene_Illuminance</c>). 배치·신뢰 축은 없다 — null이면 연출표 장면.
    /// </summary>
    public static DeathCutscene CutscenePrefab(FearAxis axis)
    {
        return CutscenePrefab(axis, NightRun.IsCaptured ? NightRun.Cause.SourceId : null);
    }

    /// <summary>
    /// 97차(민: 「인체 모형 붙잡힘 연출은 없애 주고, 배치 사망 연출은 인체 모형으로」): 출처는 더 보지 않는다 — 64·70차 「복도 끝 인체 모형을 오래 바라봐 붙잡힘 → 이 컷신」 갈래는 없앴다
    /// (92차부터 그 응시로는 붙잡히지 않는다). 98차(민: 「배치 사망 연출은 구도와 초반 흐름이 달랐는데 조도와 똑같아졌어 — 이전 배치 사망 연출로, 모델만 인체 모형」):
    /// 배치 축은 다시 연출표 장면(돌아봄 · 어둠 속 얼굴 · 끌려 들어감)이고 얼굴만 인체 모형(<see cref="LayoutFace"/>).
    /// </summary>
    public static DeathCutscene CutscenePrefab(FearAxis axis, string sourceId)
    {
        string name = axis == FearAxis.Auditory ? DeathCutscene.ResourceName
            : axis == FearAxis.Illuminance ? DeathCutscene.ResourceNameIlluminance
            : null;
        return name != null ? Resources.Load<DeathCutscene>(name) : null;
    }

    /// <summary>
    /// 컷신을 플레이어 자리에서 튼다. 끝나는 그 순간(같은 호출 안) 화면을 검게 덮는다 — 컷신은 끝나면 카메라·조작·손전등·화면 효과를 원래대로 돌리는데,
    /// 그 한 프레임이 보이지 않게. 되돌리며 켜진 조작은 다시 끈다(재시작이 다시 켠다). 같은 축 세 번째부터 아무 키로 건너뛴다.
    /// 틀지 못하면(다른 컷신 재생 중 등) <paramref name="done"/>(false) — 연출표 장면으로 넘어간다.
    /// </summary>
    private IEnumerator PlayCutscene(DeathCutscene prefab, FearAxis axis, int count, FPController player, Action<bool> done)
    {
        DeathCutscene cs = Instantiate(prefab);
        cs.name = prefab.name;
        FitDummyFace(cs.transform);   // 64차: 인체 모형 컷신 — 얼굴이 카메라 높이에 오게
        // 58차(민: 「scream을 붙잡힘 장면으로 쓰자」): 청각 컷신의 소년이 Hit 소리에 맞춰 비명 지르며 덮쳐 온다 — 이 인스턴스의 바인딩만 바꾼다(Play 전).
        bool scream = axis == FearAxis.Auditory && CutsceneScream.Attach(cs.gameObject);
        // 96차(민: 「같은 방식으로 — 구도는 그대로, 소년 → 화장실 소녀, 인체모형 → 노란 얼굴」): 이 인스턴스의 몹만 바꾸고(머리 크기·자리 맞춤),
        // 81차 원본 색(빛 무시) 대신 95차 놀람 얼굴처럼 빛을 받게 — 손전등 줄임 · 아래 옆 빛 · 피사계 심도(CutsceneFace).
        CutsceneFace.Attach(cs);
        bool finished = false;
        cs.Finished += () =>
        {
            finished = true;
            SetBlack(1f);
        };

        if (!cs.Play(true))
        {
            Destroy(cs.gameObject);
            done(false);
            yield break;
        }

        LastScene = CaptureCastSO.Label(axis) + " · 컷신 " + prefab.name + (scream ? " · 비명" : string.Empty);
        bool skippable = count >= 3;
        float t = 0f;
        while (!finished && t < 30f)
        {
            t += Mathf.Min(Time.unscaledDeltaTime, 0.1f);   // 96차: 에디터 일시정지·끊김 한 번에 안전 시한(30초)이 다 차서 컷신이 잘리지 않게
            if (skippable && t > 0.3f && Input.anyKeyDown)
            {
                SetBlack(1f);
                cs.StopAndRestore();
                LastScene += " → 건너뜀";
                break;
            }

            yield return null;
        }

        if (!finished && cs.IsPlaying)
        {
            SetBlack(1f);
            cs.StopAndRestore();
        }

        SetBlack(1f);
        if (player != null) player.enabled = false;
        LastScene += " → " + t.ToString("0.00") + "초";
        Destroy(cs.gameObject);
        done(true);
    }

    /// <summary>
    /// 64차(민: 「엔딩 장면이 … 인체 모형의 얼굴이 강조되어야 하는데, 쇄골 쪽이 강조되네」): 김진선님 조도 컷신(<c>DeathCutscene_Illuminance</c>)의 「Dummy」는
    /// 우리 대역 <c>mob.dummy.stand</c>를 품는데, 61차에 대역 키를 1.7 → 1.9m로 올려 얼굴이 카메라(CamTarget, 바닥에서 1.7m)보다 20cm쯤 위로 올라갔다 —
    /// 마지막 확대에서 카메라가 쇄골을 본다. 이 인스턴스의 모형(Model)만 발을 기준으로 줄여 눈높이(head ~ head_end의 60%)를 카메라 높이에 맞춘다.
    /// 김진선님 프리팹·타임라인과 과학실의 모형 크기는 그대로. Dummy·CamTarget·뼈가 없으면 아무것도 하지 않는다.
    /// </summary>
    public static float FitDummyFace(Transform cutscene)
    {
        if (cutscene == null) return 1f;
        Transform dummy = FindDeep(cutscene, "Dummy");
        Transform camTarget = FindDeep(cutscene, "CamTarget");
        if (dummy == null || camTarget == null) return 1f;
        Transform model = dummy.Find("Model");
        Transform head = FindDeep(dummy, "head");
        Transform headEnd = FindDeep(dummy, "head_end");
        if (model == null || head == null || headEnd == null) return 1f;

        float feet = dummy.position.y;
        float eye = Mathf.Lerp(head.position.y, headEnd.position.y, 0.6f) - feet;
        float want = camTarget.position.y - feet;
        if (eye <= 0.01f || want <= 0.01f) return 1f;
        float f = Mathf.Clamp(want / eye, 0.75f, 1.1f);
        model.localScale *= f;
        Transform aim = dummy.Find("Aim");
        if (aim != null) aim.localPosition = new Vector3(aim.localPosition.x, aim.localPosition.y * f, aim.localPosition.z);
        return f;
    }

    // ── 프리팹 장면 ───────────────────────────────────────────

    /// <summary>장면 프리팹을 플레이어 발밑·시선 방향(+Z)에 꺼진 채로 세운다. 암전이 걷힐 때 켠다.</summary>
    private GameObject SpawnScene(CaptureCastSO.Entry e, Camera cam, FPController player, int count)
    {
        Vector3 feet = player != null ? DirectionStage.FloorBelow(player.transform.position) : cam.transform.position - Vector3.up * 1.6f;
        Vector3 fwd = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up);
        if (fwd.sqrMagnitude < 0.0001f) fwd = player != null ? player.transform.forward : Vector3.forward;

        GameObject holder = new GameObject("capture scene " + e.axis);
        holder.SetActive(false);   // 자식이 깨어나지 않게(Animator는 켤 때 기본 상태부터)
        holder.transform.SetPositionAndRotation(feet, Quaternion.LookRotation(fwd.normalized, Vector3.up));
        GameObject art = Instantiate(e.prefab, holder.transform, false);
        art.name = e.prefab.name;

        foreach (Collider col in holder.GetComponentsInChildren<Collider>(true)) col.enabled = false;
        foreach (Rigidbody rb in holder.GetComponentsInChildren<Rigidbody>(true)) rb.isKinematic = true;
        foreach (AudioSource src in holder.GetComponentsInChildren<AudioSource>(true)) src.ignoreListenerPause = true;
        if (_faceLayer >= 0)
        {
            foreach (Transform t in holder.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = _faceLayer;
        }

        _sceneHasDoneEvent = false;
        foreach (Animator a in holder.GetComponentsInChildren<Animator>(true))
        {
            a.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            if (count >= 2) a.speed *= e.repeatSpeed;
            CaptureAnimEvents relay = a.GetComponent<CaptureAnimEvents>();
            if (relay == null) relay = a.gameObject.AddComponent<CaptureAnimEvents>();
            relay.Owner = this;
            if (a.runtimeAnimatorController == null) continue;
            foreach (AnimationClip clip in a.runtimeAnimatorController.animationClips)
            {
                if (clip == null) continue;
                foreach (AnimationEvent ev in clip.events)
                {
                    if (ev.functionName == CaptureAnimEvents.DoneEvent) _sceneHasDoneEvent = true;
                }
            }
        }

        Transform mount = FindDeep(holder.transform, "CameraMount");
        _pendingMount = mount;
        _pendingLens = mount != null ? mount.GetComponent<Camera>() : null;
        if (_pendingLens != null) _pendingLens.enabled = false;
        return holder;
    }

    private Transform _pendingMount;
    private Camera _pendingLens;

    private IEnumerator RunPrefabScene(CaptureCastSO.Entry e, GameObject holder, Camera cam, bool skippable)
    {
        _sceneDone = false;
        _sceneCam = cam;
        holder.SetActive(true);
        _mount = _pendingMount;
        _mountLens = _pendingLens;
        if (_mount != null) LateUpdate();

        Animator[] anims = holder.GetComponentsInChildren<Animator>(true);
        bool animated = false;
        for (int i = 0; i < anims.Length; i++) animated |= anims[i].runtimeAnimatorController != null;
        float limit = animated ? e.maxSeconds : e.faceSeconds;

        float t = 0f;
        string ended = animated ? "최대 시간 " + e.maxSeconds.ToString("0.0") + "초" : "Animator 없음 — " + e.faceSeconds.ToString("0.00") + "초";
        while (t < limit)
        {
            yield return null;
            t += Time.deltaTime;
            if (_sceneDone)
            {
                ended = "CaptureDone 이벤트";
                break;
            }

            if (skippable && Input.anyKeyDown)
            {
                ended = "건너뜀";
                break;
            }

            if (animated && !_sceneHasDoneEvent && t > 0.1f && AllFinished(anims))
            {
                ended = "Animator 끝";
                break;
            }
        }

        _mount = null;
        _mountLens = null;
        LastScene += " → " + ended + " (" + t.ToString("0.00") + "초)" + (_pendingMount != null ? " · CameraMount" : string.Empty);
        _pendingMount = null;
        _pendingLens = null;
    }

    private static bool AllFinished(Animator[] anims)
    {
        bool any = false;
        for (int i = 0; i < anims.Length; i++)
        {
            Animator a = anims[i];
            if (a == null || a.runtimeAnimatorController == null || !a.isActiveAndEnabled) continue;
            any = true;
            if (a.IsInTransition(0)) return false;
            AnimatorStateInfo st = a.GetCurrentAnimatorStateInfo(0);
            if (st.loop || st.normalizedTime < 1f) return false;
        }

        return any;
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

    // ── 장면 이벤트(CaptureAnimEvents가 넘겨줌) ─────────────────

    internal void OnSceneDone()
    {
        _sceneDone = true;
    }

    internal void OnSceneSound(string soundName, Vector3 at)
    {
        PlaySceneSound(soundName, at);
    }

    internal void OnSceneFlashlight(bool on)
    {
        if (FlashlightRelay.Active != null) FlashlightRelay.Active.SetOn(on);
    }

    internal void OnSceneCue(string cue)
    {
        Action<string> handler = SceneCued;
        if (handler != null) handler(cue ?? string.Empty);
    }

    /// <summary>장면 소리(정적 중에도 들린다). 이름 = <c>Resources/Direction/이름</c> 또는 연출 소리 표의 정확한 키. 없으면 조용히 넘어간다.</summary>
    private static void PlaySceneSound(string soundName, Vector3 at)
    {
        if (string.IsNullOrEmpty(soundName)) return;
        float volume = 1f;
        AudioClip clip = Resources.Load<AudioClip>("Direction/" + soundName);
        if (clip == null) clip = DirectionSoundTableSO.FindExact(soundName, out volume);
        if (clip == null) return;

        GameObject go = new GameObject("capture sound " + soundName);
        go.transform.position = at;
        AudioSource src = go.AddComponent<AudioSource>();
        src.clip = clip;
        src.volume = volume;
        src.spatialBlend = 1f;
        src.ignoreListenerPause = true;
        src.Play();
        Destroy(go, clip.length + 0.2f);
        if (DirectionStage.Verbose) Debug.Log("[Capture] 소리 " + soundName + " ← " + clip.name);
    }

    // ── 기본 장면(프리팹이 빌 때): 어둠 속 대역 얼굴 ─────────────────

    /// <summary>축의 얼굴 대역을 카메라 바로 앞(조준점이 <see cref="faceDistance"/>)에 세운다. 콜라이더는 끈다.</summary>
    private GameObject SpawnFace(CaptureCastSO.Entry e, Camera cam, FlashlightRelay flashlight, out GameObject lamp, out float distance)
    {
        lamp = null;
        distance = faceDistance;
        // 96차: 소년 → 화장실 소녀 · 인체 모형 → 노란 얼굴. 98차: 배치 축은 사람 나무 대신 인체 모형(구도·흐름은 그대로).
        string id = e.axis == FearAxis.Layout ? LayoutFace : CutsceneFace.Replace(e.fallbackFace);
        Vector3 eye = cam.transform.position;
        Vector3 ground = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up);
        if (ground.sqrMagnitude < 0.0001f) ground = cam.transform.root.forward;
        ground.Normalize();
        Vector3 fwd = cam.transform.forward;   // 숙이거나 든 고개 그대로 화면 한가운데에

        GameObject go = StandInFactory.Create(id, eye + ground * 1f - Vector3.up * 1.6f, eye, null);
        if (go == null) return null;
        go.name = "capture " + id;
        // 96차: 81차 원본 색(빛 무시 — 종이처럼 납작) 대신 95차 놀람 얼굴처럼 빛을 받는다. 코앞 얼굴의 그림자가 화면을 덮지 않게 그림자만 끈다.
        foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        foreach (Collider col in go.GetComponentsInChildren<Collider>(true)) col.enabled = false;
        if (_faceLayer >= 0)
        {
            foreach (Transform child in go.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = _faceLayer;
        }

        Vector3 aimAt = FaceAim(go, id, cam);
        Vector3 offset = aimAt - go.transform.position;

        // 조준점보다 카메라 쪽으로 튀어나온 부분(손·가지)이 근평면을 뚫지 않을 만큼은 띄운다.
        float protrude = 0f;
        foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
        {
            Bounds b = r.bounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                protrude = Mathf.Max(protrude, Vector3.Dot(aimAt - corner, fwd));
            }
        }

        distance = Mathf.Max(faceDistance, protrude + cam.nearClipPlane + 0.08f);
        go.transform.position = eye + fwd * distance - offset;
        LastFaceId = id;

        // 조도는 다시 켜진 손전등이 비춘다. 손전등이 없거나 다른 축이면 작은 등을 하나 둔다.
        if (e.flashlightOff && flashlight != null) return go;

        // 96차: 위 앞에서 비추던 등(원본 색 시절 1.2) 대신 95차처럼 아래 옆 작은 빛 — 이목구비에 그늘이 진다.
        lamp = new GameObject("capture lamp");
        lamp.transform.SetParent(cam.transform, false);
        lamp.transform.localPosition = new Vector3(0.22f, -0.32f, 0.05f);
        Light light = lamp.AddComponent<Light>();
        light.type = LightType.Point;
        light.range = distance + 1.5f;
        light.intensity = CaptureLampIntensity;
        light.color = new Color(1f, 0.9f, 0.78f);
        light.shadows = LightShadows.None;
        return go;
    }

    /// <summary>
    /// 화면 가운데에 올 점. 98차: 인체 모형은 대역 조준점(응시 판정용)이 아니라 눈 높이 — 머리뼈(목·머리 이음매)에서 위로 <see cref="LayoutEyeLift"/>.
    /// 선 모형은 고개를 숙이고 있어 정수리를 보였다 — 고개를 카메라 쪽으로 든다(<see cref="ScareFace.Stare"/>, 동작 위에 덧붙임). 그 밖은 대역 조준점 그대로.
    /// </summary>
    private static Vector3 FaceAim(GameObject go, string id, Camera cam)
    {
        if (id == LayoutFace)
        {
            Transform head = null;
            foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "head") head = t;
            }

            if (head != null)
            {
                // 99차(민: 「살짝 좌측을 바라보고 있어 — 정확히 플레이어를」): 얼굴 앞 = 머리뼈 +z(선 자세의 고개가 몸 앞에서 17° 돌아가 있다), 무게 1.
                if (cam != null) ScareFace.Stare.Attach(go, head, cam, 1f, Vector3.forward);
                return head.position + Vector3.up * LayoutEyeLift * go.transform.lossyScale.y;
            }
        }

        return StandInFactory.Aim(go).position;
    }

    /// <summary>98차: 배치 축 붙잡힘 장면의 얼굴(옛 사람 나무 <c>mob.tree</c> 대신) — 복도 끝에 서 있던 그 인체 모형.</summary>
    public const string LayoutFace = "mob.dummy.stand";

    /// <summary>98차: 인체 모형 눈 높이 = 머리뼈에서 위로 이만큼(m, 대역 크기 1 기준).</summary>
    public const float LayoutEyeLift = 0.09f;

    /// <summary>기본 얼굴 장면의 아래 옆 빛 세기(96차).</summary>
    public const float CaptureLampIntensity = 0.12f;

    /// <summary>기본 얼굴 장면에서 다시 켠 손전등(조도 — 손전등이 비춰 드러나는 얼굴)의 밝기 배율(96차). 다른 축은 0(아래 옆 빛만).</summary>
    public const float FaceFlashlightDimRevealed = 0.3f;

    /// <summary>
    /// 96차: 빛을 받는 얼굴이 0.4~0.7m 앞 손전등에 하얗게 날지 않게 이번 프레임 손전등 세기를 줄인다(PlayScene 실측 — 켠 채로는 사람 나무 얼굴이 흰 석고가 됐다).
    /// 손전등 세기는 <see cref="FlashlightPower"/>가 매 Update 다시 쓰므로 Update 뒤(코루틴)에 곱하기만 한다 — 장면이 끝나면 다음 프레임 저절로 돌아온다.
    /// </summary>
    private static void DimFlashlightForFace(Camera cam, float dim)
    {
        if (cam == null) return;
        foreach (Light l in cam.GetComponentsInChildren<Light>())
        {
            if (l.type == LightType.Spot) l.intensity *= dim;
        }
    }

    /// <summary>이름 없는 사용자 층 하나(장면만 그릴 때 쓴다). 없으면 -1 — 그때는 세상과 함께 그린다.</summary>
    private static int FreeLayer()
    {
        for (int i = 31; i >= 8; i--)
        {
            if (string.IsNullOrEmpty(LayerMask.LayerToName(i))) return i;
        }

        return -1;
    }
}

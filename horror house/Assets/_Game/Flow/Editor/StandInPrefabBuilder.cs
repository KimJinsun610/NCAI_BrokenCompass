using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 몹 모델(<c>Assets/2. Art/04 Materials/m_creature/</c>)을 대역 프리팹(<c>Resources/StandIns/&lt;ID&gt;</c>)으로 만든다(2026-10-01).
/// <see cref="StandInFactory"/>는 프리팹이 있으면 그것을 쓰므로, 이 메뉴 한 번이면 연출이 완성 몹으로 바뀐다.
/// <list type="bullet">
/// <item>규약: 피벗 = 발바닥 중앙(앉은 소년은 엉덩이 아래 바닥, 천장 다리는 천장에 붙는 허리), 앞 = +Z, 키는 대역 규약값.</item>
/// <item>판정 조준점 = 자식 <c>Aim</c>(머리, 천장 다리는 다리 가운데). 응시 판정이 필요한 몹(천장 다리·창밖 남자)만 <c>Aim</c>에 단단한 상자.</item>
/// <item>자세: 애니메이션 클립의 한 순간을 굳히거나(모형·고기 인간), 뼈 방향을 직접 맞춘다(앉은 소년·매달린 다리). 원본 모델·임포트 설정은 건드리지 않는다.</item>
/// <item>반복 동작(창밖 남자의 떨림, 사람 나무의 흔들림)은 클립을 <c>Art/StandIns/</c>에 복사해 반복을 켠 컨트롤러로 돌린다.</item>
/// </list>
/// 소리 표(<c>Resources/DirectionSounds.asset</c>)도 같은 메뉴가 채운다 — 다른 폴더의 클립은 참조만 한다.
/// </summary>
public static class StandInPrefabBuilder
{
    private const string Creature = "Assets/2. Art/04 Materials/m_creature/";
    private const string OutDir = "Assets/_Game/Resources/StandIns";
    private const string AnimDir = "Assets/_Game/Art/StandIns";

    /// <summary>김진선님 인간나무 프리팹(크리처 가이드 §3). 감싸기만 한다.</summary>
    private const string KimHumanTree = "Assets/3.2 Programmer_Kim/03 Prefebs/04 Horror/Creature/HorrorCreature_HumanTree_Moving.prefab";

    private const string KimHorror = "Assets/3.2 Programmer_Kim/03 Prefebs/04 Horror/";
    private const string KimVolumes = "Assets/3.2 Programmer_Kim/04 Materials/HorrorVolume/";
    private const string ScreenFxPath = "Assets/_Game/Resources/DirectionScreenFx.asset";

    /// <summary>human tree(= NCAI_BrokenCompass/Assets/크리쳐/human tree와 같은 파일). 김진선님 폴더의 것을 참조만 한다.</summary>
    private const string HumanTree = "Assets/3.2 Programmer_Kim/99 Resources/05 Model/HumanTree/HumanTree_20k_Animated.fbx";
    private const string SoundTablePath = "Assets/_Game/Resources/DirectionSounds.asset";

    private enum Pivot
    {
        Feet,
        Seated,
        Hanging
    }

    private sealed class Spec
    {
        public string Id;
        public string Model;
        public float BindHeight;
        public float Height;
        public string PoseClipModel;
        public float PoseTime;
        public string LoopClipModel;
        public string WrapPrefab;   // 다른 사람의 완성 프리팹을 그대로 감싼다(수정하지 않고 중첩)
        public bool InPlace;   // 걷기 클립의 루트 이동을 빼서 제자리 걸음으로(이동은 DirectionWalker)
        public Action<Transform> Pose;
        public Pivot Pivot = Pivot.Feet;
        public bool GazeBox;
        public Vector3 HeadBox = new Vector3(0.45f, 0.45f, 0.4f);
        public string Note = string.Empty;
    }

    /// <summary>좌석 위 골반 높이(m). 학생 의자 좌판 0.42 + 엉덩이 두께.</summary>
    public const float SeatedPelvisHeight = 0.51f;

    /// <summary>천장 다리: 골반 위 이만큼(m)이 천장 면 — 몸통·팔은 천장 속에 숨는다.</summary>
    public const float HangAboveHips = 0.25f;

    private static List<Spec> Specs()
    {
        // 2026-10-01 민 지정: 원본은 NCAI_BrokenCompass/Assets/크리쳐 → 프로젝트 m_creature/m_businessduck·m_skirtboy·m_redgirl·m_eggmantree로 복사.
        // 인체모형·human tree는 프로젝트에 같은 파일이 이미 있다(m_humandummy). CCTV 사람만 크리쳐 폴더에 없어 blackman 유지(민 결정).
        return new List<Spec>
        {
            new Spec { Id = "mob.boy", Model = "m_skirtboy/Skirt_Boy.fbx", BindHeight = 1.72f, Height = 1.35f, Pose = PoseSeated, Pivot = Pivot.Seated, Note = "앉은 소년 = skirtboy, 책상에 팔을 올린 자세" },
            new Spec { Id = "mob.legs", Model = "m_skirtboy/Skirt_Boy.fbx", BindHeight = 1.72f, Height = 1.35f, Pose = PoseHanging, Pivot = Pivot.Hanging, GazeBox = true, Note = "천장 다리 = skirtboy, 팔은 천장 속·다리만 보임" },
            new Spec { Id = "mob.windowman", Model = "m_businessduck/Business_Duck.fbx", BindHeight = 1.78f, Height = 1.85f, GazeBox = true, HeadBox = new Vector3(0.6f, 0.55f, 0.45f), Note = "창밖 남자 = business duck" },
            new Spec { Id = "mob.duck", Model = "m_businessduck/Business_Duck.fbx", BindHeight = 1.78f, Height = 1.85f, Note = "노란 얼굴 = business duck" },
            new Spec { Id = "mob.dummy.stand", Model = "m_humandummy/humman dummy_default_motion.fbx", BindHeight = 1.07f, Height = 1.7f, PoseClipModel = "m_humandummy/humman dummy_default_motion.fbx", PoseTime = 0f, Note = "복도 끝에 선 자 = 인체모형(팔을 내린 선 자세)" },
            new Spec { Id = "mob.dummy", Model = "m_humandummy/humman dummy_wake_motion.fbx", BindHeight = 1.07f, Height = 1.7f, PoseClipModel = "m_humandummy/humman dummy_wake_motion.fbx", PoseTime = 6.6f, Note = "모형 급습 = 인체모형(일어선 직후 구부정한 자세)" },
            new Spec { Id = "mob.girl", Model = "m_redgirl/redgirl_walking.fbx", BindHeight = 1.65f, Height = 1.3f, LoopClipModel = "m_redgirl/redgirl_walking.fbx", InPlace = true, Note = "화장실 소녀 = red girl, 제자리 걷기(이동은 DirectionWalker)" },
            new Spec { Id = "mob.finale", Model = "m_blackman/blackman.fbx", BindHeight = 1.04f, Height = 1.8f, GazeBox = true, HeadBox = new Vector3(0.4f, 0.45f, 0.35f), Note = "경비실 창밖의 검은 남자(피날레 K4 결말)" },
            new Spec { Id = "mob.blackman", Model = "m_blackman/blackman.fbx", BindHeight = 1.04f, Height = 1.8f, Note = "CCTV에만 보이는 사람(기존 유지)" },
            new Spec { Id = "mob.tree", WrapPrefab = KimHumanTree, Note = "사람 나무 = 김진선님 HorrorCreature_HumanTree_Moving(흔들림 강화 + 바라보면 화면이 물듦)" }
        };
    }

    [MenuItem("야간근무/연출/몹 대역·소리 연결 (프리팹·소리 표 다시 만들기)")]
    public static void BuildAllMenu()
    {
        string report = BuildAll();
        Debug.Log(report);
        EditorUtility.DisplayDialog("몹 대역·소리 연결", report, "확인");
    }

    /// <summary>프리팹·소리 표를 다시 만든다. 보고 문자열을 돌려준다(에디터 자동화용).</summary>
    public static string BuildAll()
    {
        StringBuilder sb = new StringBuilder();
        EnsureFolder(OutDir);
        EnsureFolder(AnimDir);
        foreach (Spec s in Specs())
        {
            try
            {
                sb.AppendLine(Build(s));
            }
            catch (Exception e)
            {
                sb.AppendLine("✗ " + s.Id + ": " + e.Message);
                Debug.LogException(e);
            }
        }

        try
        {
            sb.AppendLine(BuildPhantomDoor());
        }
        catch (Exception e)
        {
            sb.AppendLine("✗ prop.phantomdoor: " + e.Message);
        }

        sb.AppendLine(CleanStale());
        sb.AppendLine(BuildSoundTable());
        sb.AppendLine(BuildScreenFx());
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return sb.ToString();
    }

    // ── 몹 ─────────────────────────────────────────────────

    private static string Build(Spec s)
    {
        if (!string.IsNullOrEmpty(s.WrapPrefab)) return BuildWrapped(s);
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath(s.Model));
        if (asset == null) return "✗ " + s.Id + ": 모델 없음 " + s.Model;

        GameObject root = new GameObject(s.Id);
        try
        {
            GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            model.name = "Model";
            model.transform.SetParent(root.transform, false);
            float scale = s.Height / s.BindHeight;
            model.transform.localScale = Vector3.one * scale;

            if (!string.IsNullOrEmpty(s.PoseClipModel))
            {
                AnimationClip pose = FirstClip(ModelPath(s.PoseClipModel));
                if (pose != null) pose.SampleAnimation(model, s.PoseTime);
            }

            if (s.Pose != null) s.Pose(model.transform);

            foreach (SkinnedMeshRenderer smr in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                smr.updateWhenOffscreen = true;   // 자세를 바꿨으니 가져온 경계 상자로 컬링하면 잘린다.
            }

            Transform pelvis = Bone(model.transform, "pelvis");
            Bounds baked = BakedBounds(model);
            Vector3 anchor;
            switch (s.Pivot)
            {
                case Pivot.Seated:
                    anchor = pelvis.position - Vector3.up * SeatedPelvisHeight;
                    break;
                case Pivot.Hanging:
                    anchor = pelvis.position + Vector3.up * HangAboveHips;
                    break;
                default:
                    Vector3 xz = pelvis != null ? pelvis.position : baked.center;
                    anchor = new Vector3(xz.x, baked.min.y, xz.z);
                    break;
            }

            model.transform.position -= anchor;   // 피벗을 루트 원점으로.
            baked = BakedBounds(model);

            GameObject aim = new GameObject("Aim");
            aim.transform.SetParent(root.transform, false);
            if (s.Pivot == Pivot.Hanging)
            {
                Bounds legs = BakedBounds(model, below: 0f);
                aim.transform.position = legs.center;
                BoxCollider box = aim.AddComponent<BoxCollider>();
                box.size = legs.size + new Vector3(0.1f, 0f, 0.1f);
            }
            else
            {
                Transform head = Bone(model.transform, "head");
                Vector3 h = head != null ? head.position : new Vector3(0f, baked.max.y - 0.15f, 0f);
                if (head != null) h = new Vector3(h.x, Mathf.Min(baked.max.y - 0.05f, h.y + (baked.max.y - h.y) * 0.55f), h.z);
                aim.transform.position = h;
                if (s.GazeBox)
                {
                    BoxCollider box = aim.AddComponent<BoxCollider>();
                    box.size = s.HeadBox;
                }
            }

            string loopNote = string.Empty;
            if (!string.IsNullOrEmpty(s.LoopClipModel))
            {
                float natural;
                AnimatorController ctrl = LoopController(s.Id, ModelPath(s.LoopClipModel), s.InPlace, out natural);
                if (s.InPlace && natural > 0.01f)
                {
                    DirectionWalker walker = root.AddComponent<DirectionWalker>();
                    walker.SetNaturalSpeed(natural * scale);
                    walker.enabled = false;   // 자리(StageAnchor)에 끝점이 있을 때만 걷는다
                    loopNote += " · 제자리 걸음 " + (natural * scale).ToString("F2") + "m/s";
                }
                if (ctrl != null)
                {
                    Animator a = model.GetComponent<Animator>();
                    if (a == null) a = model.AddComponent<Animator>();
                    a.runtimeAnimatorController = ctrl;
                    a.applyRootMotion = false;
                    a.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                    loopNote = " · 반복 " + ctrl.name + loopNote;
                }
            }
            else
            {
                // 굳힌 자세를 애니메이터가 바인드 포즈로 되돌리지 않게 컨트롤러 없는 애니메이터는 끈다.
                Animator a = model.GetComponent<Animator>();
                if (a != null) a.enabled = false;
            }

            foreach (Collider c in model.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(c);

            string path = OutDir + "/" + s.Id + ".prefab";
            PrefabUtility.SaveAsPrefabAsset(root, path);
            return "✓ " + s.Id + " ← " + s.Model + " (키 " + s.Height.ToString("F2") + "m, 조준 " + aim.transform.localPosition.ToString("F2") + loopNote + ") — " + s.Note;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    /// <summary>앉은 자세(앞 = +Z, 왼쪽 = −X). 뼈를 「자식 쪽을 향하는 방향」으로 맞춘다 — 뼈 로컬 축을 몰라도 된다.</summary>
    private static void PoseSeated(Transform model)
    {
        Point(model, "spine", "chest", new Vector3(0f, 1f, 0.12f));
        Point(model, "neck", "head", new Vector3(0f, 1f, 0.45f));            // 고개를 숙임
        Point(model, "thigh.L", "shin.L", new Vector3(-0.1f, -0.12f, 1f));
        Point(model, "thigh.R", "shin.R", new Vector3(0.1f, -0.12f, 1f));
        Point(model, "shin.L", "foot.L", new Vector3(0f, -1f, 0.08f));
        Point(model, "shin.R", "foot.R", new Vector3(0f, -1f, 0.08f));
        Point(model, "upper_arm.L", "forearm.L", new Vector3(-0.12f, -0.9f, 0.42f));
        Point(model, "upper_arm.R", "forearm.R", new Vector3(0.12f, -0.9f, 0.42f));
        Point(model, "forearm.L", "hand.L", new Vector3(0.25f, -0.15f, 1f));
        Point(model, "forearm.R", "hand.R", new Vector3(-0.25f, -0.15f, 1f));
    }

    /// <summary>천장에 매달린 자세: 팔은 머리 위로(천장 속), 다리는 늘어뜨리고 발끝은 아래.</summary>
    private static void PoseHanging(Transform model)
    {
        Point(model, "upper_arm.L", "forearm.L", new Vector3(-0.15f, 1f, 0f));
        Point(model, "upper_arm.R", "forearm.R", new Vector3(0.15f, 1f, 0f));
        Point(model, "forearm.L", "hand.L", new Vector3(-0.05f, 1f, 0f));
        Point(model, "forearm.R", "hand.R", new Vector3(0.05f, 1f, 0f));
        Point(model, "thigh.L", "shin.L", new Vector3(-0.03f, -1f, 0.02f));
        Point(model, "thigh.R", "shin.R", new Vector3(0.05f, -1f, -0.03f));
        Point(model, "shin.L", "foot.L", new Vector3(0f, -1f, 0f));
        Point(model, "shin.R", "foot.R", new Vector3(0f, -1f, 0f));
        Point(model, "foot.L", "toe.L", new Vector3(0f, -0.75f, 0.65f));
        Point(model, "foot.R", "toe.R", new Vector3(0f, -0.75f, 0.65f));
        Point(model, "neck", "head", new Vector3(0f, 1f, 0.3f));
    }

    /// <summary>옛 리그(pelvis·thigh.L)와 새 리그(Hips·LeftUpperLeg)의 뼈 이름을 함께 찾는다.</summary>
    private static readonly Dictionary<string, string> Alias = new Dictionary<string, string>
    {
        { "pelvis", "Hips" }, { "spine", "Spine" }, { "chest", "Chest" }, { "neck", "Neck" }, { "head", "Head" },
        { "thigh.L", "LeftUpperLeg" }, { "shin.L", "LeftLowerLeg" }, { "foot.L", "LeftFoot" }, { "toe.L", "LeftToes" },
        { "thigh.R", "RightUpperLeg" }, { "shin.R", "RightLowerLeg" }, { "foot.R", "RightFoot" }, { "toe.R", "RightToes" },
        { "upper_arm.L", "LeftUpperArm" }, { "forearm.L", "LeftLowerArm" }, { "hand.L", "LeftHand" },
        { "upper_arm.R", "RightUpperArm" }, { "forearm.R", "RightLowerArm" }, { "hand.R", "RightHand" }
    };

    private static Transform Bone(Transform model, string name)
    {
        Transform t = FindDeep(model, name);
        string other;
        if (t == null && Alias.TryGetValue(name, out other)) t = FindDeep(model, other);
        return t;
    }

    private static void Point(Transform model, string bone, string child, Vector3 worldDir)
    {
        Transform b = Bone(model, bone);
        Transform c = Bone(model, child);
        if (b == null || c == null) return;
        Vector3 d = c.position - b.position;
        if (d.sqrMagnitude < 1e-8f) return;
        b.rotation = Quaternion.FromToRotation(d.normalized, worldDir.normalized) * b.rotation;
    }

    /// <summary>표에서 빠진 대역 프리팹·반복 클립을 지운다(예전 매칭이 남아 다른 몹이 나오지 않게).</summary>
    private static string CleanStale()
    {
        HashSet<string> keep = new HashSet<string>(StringComparer.Ordinal) { "prop.phantomdoor" };
        HashSet<string> loops = new HashSet<string>(StringComparer.Ordinal);
        foreach (Spec s in Specs())
        {
            keep.Add(s.Id);
            if (!string.IsNullOrEmpty(s.LoopClipModel)) loops.Add(s.Id);
        }

        List<string> removed = new List<string>();
        foreach (string g in AssetDatabase.FindAssets("t:Prefab", new[] { OutDir }))
        {
            string path = AssetDatabase.GUIDToAssetPath(g);
            string id = Path.GetFileNameWithoutExtension(path);
            if (keep.Contains(id)) continue;
            AssetDatabase.DeleteAsset(path);
            removed.Add(id);
        }

        foreach (string g in AssetDatabase.FindAssets("", new[] { AnimDir }))
        {
            string path = AssetDatabase.GUIDToAssetPath(g);
            string file = Path.GetFileName(path);
            int cut = file.IndexOf(".loop", StringComparison.Ordinal);
            if (cut < 0) cut = file.IndexOf(".controller", StringComparison.Ordinal);
            if (cut < 0) continue;
            string id = file.Substring(0, cut);
            if (loops.Contains(id)) continue;
            AssetDatabase.DeleteAsset(path);
            removed.Add(file);
        }

        return removed.Count == 0 ? "– 지울 옛 대역 없음" : "✓ 옛 대역 정리: " + string.Join(", ", removed);
    }

    /// <summary>다른 사람의 완성 프리팹을 중첩해 감싼다 — 원본은 손대지 않는다. 피벗만 원점으로, 판정 조준점(Aim)만 더한다.</summary>
    private static string BuildWrapped(Spec s)
    {
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(s.WrapPrefab);
        if (asset == null) return "✗ " + s.Id + ": 프리팹 없음 " + s.WrapPrefab;
        GameObject root = new GameObject(s.Id);
        try
        {
            GameObject inner = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            inner.transform.SetParent(root.transform, false);
            inner.transform.localPosition = Vector3.zero;   // 원본 루트에 남은 씬 좌표(-32, 0, -21)를 지운다
            inner.transform.localRotation = Quaternion.identity;
            foreach (SkinnedMeshRenderer smr in inner.GetComponentsInChildren<SkinnedMeshRenderer>(true)) smr.updateWhenOffscreen = true;
            Bounds b = BakedBounds(inner);
            GameObject aim = new GameObject("Aim");
            aim.transform.SetParent(root.transform, false);
            aim.transform.position = new Vector3(0f, Mathf.Max(0.5f, b.max.y - 0.15f), 0f);
            PrefabUtility.SaveAsPrefabAsset(root, OutDir + "/" + s.Id + ".prefab");
            return "✓ " + s.Id + " ← " + Path.GetFileNameWithoutExtension(s.WrapPrefab) + " (중첩, 높이 " + b.size.y.ToString("F2") + "m) — " + s.Note;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    /// <summary>화면 효과 에셋 — 김진선님 공포 화면 톤 4종을 참조만 한다.</summary>
    private static string BuildScreenFx()
    {
        DirectionScreenFxSO fx = AssetDatabase.LoadAssetAtPath<DirectionScreenFxSO>(ScreenFxPath);
        if (fx == null)
        {
            fx = ScriptableObject.CreateInstance<DirectionScreenFxSO>();
            AssetDatabase.CreateAsset(fx, ScreenFxPath);
        }

        fx.Suffocate = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.VolumeProfile>(KimVolumes + "VP_Horror_Suffocate.asset");
        fx.Blackout = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.VolumeProfile>(KimVolumes + "VP_Horror_Blackout.asset");
        fx.Wrongness = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.VolumeProfile>(KimVolumes + "VP_Horror_Wrongness.asset");
        fx.Creep = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.VolumeProfile>(KimVolumes + "VP_Horror_Creep.asset");
        EditorUtility.SetDirty(fx);
        int n = (fx.Suffocate ? 1 : 0) + (fx.Blackout ? 1 : 0) + (fx.Wrongness ? 1 : 0) + (fx.Creep ? 1 : 0);
        return "✓ 화면 효과 프로필 " + n + "/4";
    }

    // ── 가짜 놀람 자리(김진선님 캐비닛 연출) ──────────────────────

    [MenuItem("야간근무/연출/가짜 놀람 캐비닛 놓기")]
    public static void PlaceFakeSpotsMenu()
    {
        Debug.Log(PlaceFakeSpots());
    }

    /// <summary>
    /// 김진선님 캐비닛 연출 둘을 맵의 같은 사물함 자리에 놓고 원래 사물함은 끈다(가이드 §3.4 — 삭제하지 않음).
    /// 「덜컹이는 사물함」 = CabinetBang(반복해서 열렸다 쾅) · 「열려 있는 사물함」 = CabinetCreak(삐걱 열려 멈춤).
    /// 자체 구역 트리거는 끄고 <see cref="DirectionFakeSpot"/>을 붙인다 — 가짜 놀람은 디렉터가 예산 안에서만 건다.
    /// 점검 항목 H-4(복도 사물함, x 41.7)와 떨어진 자리만 쓴다.
    /// </summary>
    public static string PlaceFakeSpots()
    {
        StringBuilder sb = new StringBuilder();
        GameObject parent = GameObject.Find("HorrorEvents");
        if (parent == null)
        {
            parent = new GameObject("HorrorEvents");
            Undo.RegisterCreatedObjectUndo(parent, "가짜 놀람 자리");
        }

        sb.AppendLine(PlaceCabinet(parent.transform, "HorrorEvent_CabinetCreak", "Interior/Corridors/LockerB_static (12)", "fake.locker.row", false));
        sb.AppendLine(PlaceCabinet(parent.transform, "HorrorEvent_CabinetBang", "Interior/Corridors/LockerB_static (20)", "fake.locker.rattle", true));
        for (int i = 0; i < BugSpots.Length; i++) sb.AppendLine(PlaceBugSwarm(parent.transform, BugSpotNames[i], BugSpots[i]));
        EditorSceneManager.MarkSceneDirty(parent.scene);
        return sb.ToString();
    }

    // 벌레 떼 자리(민 지정 방: 1-3 뒤 창고 · 화장실 · 도서관). 바닥 x·z — 높이는 바닥을 재서 맞춘다(프리팹의 무리는 루트 +3.6m = 천장 5.5 바로 아래).
    private static readonly string[] BugSpotNames = { "BugSwarm_Storeroom", "BugSwarm_ToiletLamp", "BugSwarm_LibraryAisle", "BugSwarm_LibraryDoor", "BugSwarm_LibraryNorth" };
    private static readonly Vector3[] BugSpots =
    {
        new Vector3(51.9f, 0f, 33.8f),   // 1-3 뒤 통로 너머 창고 — 천장 다리 자리(52.74, 33.16)에서 비켜 문간 쪽
        new Vector3(0.8f, 0f, 34.5f),    // 화장실 — 세면대 앞 형광등 밑. 입구(서쪽 문 -2.1, 34)에서 보인다(실측). 소녀 동선(x 3.2)과 떨어짐
        new Vector3(7.0f, 0f, 46.7f),    // 도서관 열람 탁자 서쪽 빈 바닥 — 깨진 천장(BrokenA (13)) 밑. 탁자 사이에 두면 벌레가 탁자 밑으로 숨었다(실측)
        new Vector3(12f, 0f, 42.5f),     // 도서관 정문 안쪽 — 깨진 천장(BrokenA (2)) 밑
        new Vector3(4.5f, 0f, 50f)       // 도서관 북서 서가 사이 — 깨진 천장(BrokenA (17)) 밑
    };

    /// <summary>
    /// 김진선님 <c>HorrorEvent_BugSwarm</c>을 한 자리에 놓는다(이미 있으면 위치만 다시 맞춤). 프리팹은 고치지 않고 인스턴스에서만:
    /// 자체 구역 트리거 끔 · 디버그 키(6) 끔 · 반복 재생 · <see cref="DirectionFakeSpot"/>(fake.bugs, 10m — 옆 방의 자리가 걸리지 않게).
    /// </summary>
    private static string PlaceBugSwarm(Transform parent, string name, Vector3 xz)
    {
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(KimHorror + "HorrorEvent_BugSwarm.prefab");
        if (asset == null) return "✗ HorrorEvent_BugSwarm 없음";
        Transform inst = parent.Find(name);
        if (inst == null)
        {
            GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
            Undo.RegisterCreatedObjectUndo(go, "벌레 떼 자리");
            go.name = name;
            inst = go.transform;
        }

        float floor = 1.5f;
        RaycastHit hit;
        if (Physics.Raycast(new Vector3(xz.x, 5.3f, xz.z), Vector3.down, out hit, 6f, ~0, QueryTriggerInteraction.Ignore) && hit.point.y < 1.8f) floor = hit.point.y;
        Undo.RecordObject(inst, "벌레 떼 자리");
        inst.SetPositionAndRotation(new Vector3(xz.x, floor, xz.z), Quaternion.identity);
        SilenceKimEvent(inst, true);
        DirectionFakeSpot spot = inst.GetComponent<DirectionFakeSpot>();
        if (spot == null) spot = Undo.AddComponent<DirectionFakeSpot>(inst.gameObject);
        spot.Configure(NightDuty.TensionDirector.FakeBugs, 10f, true);
        EditorUtility.SetDirty(spot);
        return "✓ " + NightDuty.TensionDirector.FakeBugs + " ← " + name + " @" + inst.position.ToString("F2");
    }

    /// <summary>김진선님 연출 인스턴스의 자체 시동(구역 트리거·디버그 숫자 키)을 끄고 재생 방식을 정한다. 프리팹 자산은 그대로.</summary>
    private static void SilenceKimEvent(Transform inst, bool replayable)
    {
        foreach (HorrorTriggerZone z in inst.GetComponentsInChildren<HorrorTriggerZone>(true))
        {
            Undo.RecordObject(z, "구역 트리거 끔");
            z.enabled = false;
            Collider c = z.GetComponent<Collider>();
            if (c != null)
            {
                Undo.RecordObject(c, "구역 트리거 끔");
                c.enabled = false;
            }
        }

        HorrorEvent he = inst.GetComponent<HorrorEvent>();
        if (he == null) return;
        SerializedObject so = new SerializedObject(he);
        so.FindProperty("playOnce").boolValue = !replayable;
        SerializedProperty key = so.FindProperty("useDebugKey");
        if (key != null) key.boolValue = false;   // 숫자 키로 아무 때나 터지지 않게 — 시험은 F3 콘솔로
        so.ApplyModifiedProperties();
    }

    private static string PlaceCabinet(Transform parent, string prefabName, string originalPath, string fakeId, bool replayable)
    {
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(KimHorror + prefabName + ".prefab");
        if (asset == null) return "✗ " + prefabName + " 없음";
        Transform inst = parent.Find(prefabName);
        GameObject original = GameObject.Find(originalPath);
        if (inst == null)
        {
            if (original == null) return "✗ 원래 사물함 없음: " + originalPath;
            GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
            Undo.RegisterCreatedObjectUndo(go, "가짜 놀람 자리");
            inst = go.transform;
        }

        if (original != null)
        {
            inst.SetPositionAndRotation(original.transform.position, original.transform.rotation);
            if (original.activeSelf)
            {
                Undo.RecordObject(original, "원래 사물함 끔");
                original.SetActive(false);
            }
        }

        SilenceKimEvent(inst, replayable);

        DirectionFakeSpot spot = inst.GetComponent<DirectionFakeSpot>();
        if (spot == null) spot = Undo.AddComponent<DirectionFakeSpot>(inst.gameObject);
        spot.Configure(fakeId, 14f, replayable);
        EditorUtility.SetDirty(spot);
        return "✓ " + fakeId + " ← " + prefabName + " @" + inst.position.ToString("F2") + " (원래 " + originalPath + " 끔, 구역 트리거 끔)";
    }

    // ── 없던 문 ────────────────────────────────────────────

    private static string BuildPhantomDoor()
    {
        const string vendor = "Assets/NOT_Lonely/HQ_AbandonedSchool/Prefabs/DoorNarrowSolid.prefab";
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(vendor);
        if (asset == null) return "– prop.phantomdoor: 벤더 문 프리팹이 없어 대역 판자 그대로";

        GameObject root = new GameObject("prop.phantomdoor");
        try
        {
            GameObject door = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            PrefabUtility.UnpackPrefabInstance(door, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            door.name = "Model";
            door.transform.SetParent(root.transform, false);

            // 그림만 남긴다 — 벤더 문 스크립트·애니메이션·콜라이더·소리가 남으면 문 발신기(DoorRelay)가 진짜 문으로 안다.
            for (int pass = 0; pass < 3; pass++)
            {
                foreach (Component c in door.GetComponentsInChildren<Component>(true))
                {
                    if (c == null || c is Transform || c is Renderer || c is MeshFilter) continue;
                    UnityEngine.Object.DestroyImmediate(c);
                }
            }

            foreach (SkinnedMeshRenderer smr in door.GetComponentsInChildren<SkinnedMeshRenderer>(true)) smr.updateWhenOffscreen = true;
            Bounds b = BakedBounds(door);
            door.transform.position -= new Vector3(b.center.x, b.min.y, b.center.z);
            GameObject aim = new GameObject("Aim");
            aim.transform.SetParent(root.transform, false);
            aim.transform.localPosition = new Vector3(0f, 1.1f, 0f);
            PrefabUtility.SaveAsPrefabAsset(root, OutDir + "/prop.phantomdoor.prefab");
            return "✓ prop.phantomdoor ← DoorNarrowSolid(그림만)";
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    // ── 반복 클립 ───────────────────────────────────────────

    private static AnimatorController LoopController(string id, string modelPath, bool inPlace, out float naturalSpeed)
    {
        naturalSpeed = 0f;
        AnimationClip src = FirstClip(modelPath);
        if (src == null) return null;

        string clipPath = AnimDir + "/" + id + ".loop.anim";
        AnimationClip copy = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
        if (copy != null) AssetDatabase.DeleteAsset(clipPath);
        copy = UnityEngine.Object.Instantiate(src);
        copy.name = id + ".loop";
        AnimationClipSettings st = AnimationUtility.GetAnimationClipSettings(copy);
        st.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(copy, st);
        if (inPlace) naturalSpeed = RemoveRootDrift(copy);
        AssetDatabase.CreateAsset(copy, clipPath);

        string ctrlPath = AnimDir + "/" + id + ".controller";
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ctrlPath) != null) AssetDatabase.DeleteAsset(ctrlPath);
        AnimatorController ctrl = AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);
        ctrl.AddParameter("Pose", AnimatorControllerParameterType.Int);          // DirectionCue 계약
        ctrl.AddParameter("Intensity", AnimatorControllerParameterType.Float);
        AnimatorState state = ctrl.layers[0].stateMachine.AddState("Loop");
        state.motion = copy;
        ctrl.layers[0].stateMachine.defaultState = state;
        return ctrl;
    }

    /// <summary>m_creature 기준 상대 경로, 또는 <c>Assets/</c>로 시작하는 전체 경로.</summary>
    private static string ModelPath(string model)
    {
        return model.StartsWith("Assets/", StringComparison.Ordinal) ? model : Creature + model;
    }

    /// <summary>
    /// 맨 위 리그 뼈(경로에 '/'가 없는 것)의 위치 곡선에서 시작→끝 이동량을 선형으로 빼 제자리 걸음으로 만든다. 원래 걸음 속도(m/s, 모델 크기 1 기준)를 돌려준다.
    /// </summary>
    private static float RemoveRootDrift(AnimationClip clip)
    {
        float len = Mathf.Max(0.01f, clip.length);
        Vector3 drift = Vector3.zero;
        foreach (EditorCurveBinding b in AnimationUtility.GetCurveBindings(clip))
        {
            if (b.path.Contains("/") || !b.propertyName.StartsWith("m_LocalPosition.", StringComparison.Ordinal) || b.propertyName.EndsWith(".y", StringComparison.Ordinal)) continue;
            AnimationCurve c = AnimationUtility.GetEditorCurve(clip, b);
            if (c == null || c.length < 2) continue;
            float d = c.keys[c.length - 1].value - c.keys[0].value;
            if (Mathf.Abs(d) < 0.01f) continue;
            Keyframe[] keys = c.keys;
            for (int i = 0; i < keys.Length; i++)
            {
                keys[i].value -= d * (keys[i].time / len);
                keys[i].inTangent -= d / len;
                keys[i].outTangent -= d / len;
            }

            AnimationUtility.SetEditorCurve(clip, b, new AnimationCurve(keys));
            if (b.propertyName.EndsWith(".x", StringComparison.Ordinal)) drift.x = d;
            else drift.z = d;
        }

        return drift.magnitude / len;
    }

    private static AnimationClip FirstClip(string path)
    {
        foreach (UnityEngine.Object o in AssetDatabase.LoadAllAssetsAtPath(path))
        {
            AnimationClip c = o as AnimationClip;
            if (c != null && !c.name.StartsWith("__preview", StringComparison.Ordinal)) return c;
        }

        return null;
    }

    // ── 소리 표 ─────────────────────────────────────────────

    /// <summary>소리 표는 <see cref="DirectionSoundTableBuilder"/>가 채운다(2026-10-04 사운드 전달본).</summary>
    private static string BuildSoundTable()
    {
        return DirectionSoundTableBuilder.Build();
    }

    // ── 씬 고정 자리 ────────────────────────────────────────

    [MenuItem("야간근무/연출/고정 몹 자리 놓기 (소년·천장 다리·창밖 남자·피날레)")]
    public static void PlaceStageAnchorsMenu()
    {
        string report = PlaceStageAnchors();
        Debug.Log(report);
    }

    /// <summary>
    /// 열린 근무 씬에 고정 자리 셋을 놓는다(있으면 옮긴다). 민 지정(2026-10-01):
    /// 소년 = 1-3 교실(Classroom02) 맨 뒤 줄, 학생 기준 오른쪽에서 둘째 책상 · 천장 다리 = 뒤 통로 너머 창고 천장(사다리 위) ·
    /// 창밖 남자 = 도서관 북쪽 <c>WallOutside_4m_WindowDouble</c> 밖.
    /// </summary>
    public static string PlaceStageAnchors()
    {
        StringBuilder sb = new StringBuilder();
        GameObject parent = GameObject.Find("DirectionAnchors");
        if (parent == null)
        {
            parent = new GameObject("DirectionAnchors");
            Undo.RegisterCreatedObjectUndo(parent, "고정 몹 자리");
        }

        // ① 소년 — 책상: 앞(+Z)이 칠판 쪽(서쪽). 학생 기준 오른쪽 = 앞을 볼 때 오른쪽.
        Transform room = GameObject.Find("Interior/Classroom02") != null ? GameObject.Find("Interior/Classroom02").transform : null;
        if (room != null)
        {
            List<Transform> desks = new List<Transform>();
            foreach (Transform t in room) if (t.name.StartsWith("StudentDeskB_Plastic_RED", StringComparison.Ordinal) && Mathf.Abs(t.position.y - 1.5f) < 0.05f) desks.Add(t);
            if (desks.Count > 0)
            {
                Vector3 front = Flat(desks[0].forward);
                Vector3 right = Quaternion.Euler(0f, 90f, 0f) * front;
                float back = float.MinValue;
                foreach (Transform d in desks) back = Mathf.Max(back, Vector3.Dot(d.position, -front));
                List<Transform> backRow = desks.FindAll(d => Vector3.Dot(d.position, -front) > back - 0.5f);
                backRow.Sort((a, b) => Vector3.Dot(b.position, right).CompareTo(Vector3.Dot(a.position, right)));
                if (backRow.Count >= 2)
                {
                    Transform desk = backRow[1];
                    Vector3 seat = desk.TransformPoint(new Vector3(0f, 0f, -0.12f));
                    seat.y = desk.position.y;
                    Anchor(parent.transform, NightDuty.StageAnchors.BoySeat, seat, Quaternion.LookRotation(Flat(desk.forward)), null, Vector3.zero, Vector3.zero, 3.5f);
                    sb.AppendLine("✓ 소년 자리 — " + desk.name + " " + desk.position.ToString("F2") + " (맨 뒤 줄 " + backRow.Count + "개 중 오른쪽에서 둘째)");
                }
            }
        }

        // ② 천장 다리 — 창고 사다리 바로 위 천장. 앞은 교실 쪽(통로).
        GameObject ladder = GameObject.Find("Interior/Classroom02/LibraryLadder (1)");
        if (ladder != null)
        {
            Vector3 p = ladder.transform.position + Vector3.up * 1f;
            RaycastHit hit;
            float ceiling = Physics.Raycast(p, Vector3.up, out hit, 6f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) ? hit.point.y : ladder.transform.position.y + 4f;
            Vector3 at = new Vector3(ladder.transform.position.x, ceiling, ladder.transform.position.z);
            Anchor(parent.transform, NightDuty.StageAnchors.LegsCeiling, at, Quaternion.LookRotation(Vector3.left), null, Vector3.zero, Vector3.zero, 1.8f);
            sb.AppendLine("✓ 천장 다리 자리 — 사다리 위 천장 " + at.ToString("F2"));
        }

        // ③ 창밖 남자 — 도서관 북쪽 겹창의 오른쪽 창(가운데는 벽기둥). 블라인드 아래 틈(약 2.4~2.9m)에 얼굴.
        Transform window = null;
        foreach (Transform t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
        {
            if (t.name == "WallOutside_4m_WindowDouble" && Vector3.Distance(t.position, new Vector3(10f, 0f, 56f)) < 0.5f) window = t;
        }

        if (window != null)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(OutDir + "/mob.windowman.prefab");
            float aimY = 1.5f;
            if (prefab != null && prefab.transform.Find("Aim") != null) aimY = prefab.transform.Find("Aim").localPosition.y;
            const float faceY = 2.78f;   // business duck 조준점(머리 위쪽)이 이 높이 — 얼굴이 블라인드 아래 틈 가운데
            const float paneX = 8.25f;   // 오른쪽 창 가운데(실측: 왼쪽 창 6.4~7.4, 벽기둥 7.5~7.9, 오른쪽 창 7.9~9.0)
            Vector3 at = new Vector3(paneX, faceY - aimY, 57.3f);
            Anchor(parent.transform, NightDuty.StageAnchors.WindowMan, at, Quaternion.LookRotation(Vector3.back), "GazeProxy",
                   new Vector3(paneX, faceY, 56.35f), new Vector3(0.9f, 0.5f, 0.06f), 4f);
            sb.AppendLine("✓ 창밖 남자 자리 — " + window.name + " 밖 " + at.ToString("F2") + " (얼굴 높이 " + faceY + ", 응시 상자는 창 안쪽 면)");
        }

        // ④ 문간의 노란 얼굴(L3) — 도서관 정문 DoorWide (2) 앞 복도(민 스크린샷), 도서관 안쪽(서쪽)을 본다. 대면 동안 문을 열어 둔다.
        GameObject libDoor = GameObject.Find("Corridors/DoorWide (2)") ?? GameObject.Find("DoorWide (2)");
        if (libDoor != null)
        {
            Vector3 d = libDoor.transform.position;
            Vector3 at = Floor(new Vector3(d.x + 0.95f, d.y, d.z + 0.2f));
            Transform a = Anchor(parent.transform, NightDuty.StageAnchors.YellowDoor, at, Quaternion.LookRotation(Vector3.left), null, Vector3.zero, Vector3.zero, 4f);
            a.GetComponent<StageAnchor>().ConfigureExtras(libDoor.transform, null, 0.45f);
            sb.AppendLine("✓ 문간의 노란 얼굴 자리 — " + libDoor.name + " 앞 " + at.ToString("F2") + " (대면 동안 문 열림)");
        }

        // ⑤ 화장실 소녀 — 바깥쪽 칸(toilet.stall.outer) 앞에서 북쪽으로 걸어 칸 안으로. 입구(서쪽)에서 보면 옆으로 걷는다.
        Bounds stall;
        SpaceZones zones = UnityEngine.Object.FindAnyObjectByType<SpaceZones>();
        if (zones != null && zones.TryGetSignalZone("toilet.stall.outer.inside", out stall))
        {
            Vector3 start = Floor(new Vector3(stall.center.x, 1.5f, 32.5f));
            Vector3 end = Floor(new Vector3(stall.center.x, 1.5f, stall.min.z + 0.35f));
            Transform a = Anchor(parent.transform, NightDuty.StageAnchors.GirlWalk, start, Quaternion.LookRotation(Vector3.left), null, Vector3.zero, Vector3.zero, 4f);
            Transform walkEnd = a.Find("WalkTo");
            if (walkEnd == null)
            {
                walkEnd = new GameObject("WalkTo").transform;
                walkEnd.SetParent(a, false);
            }

            walkEnd.position = end;
            a.GetComponent<StageAnchor>().ConfigureExtras(null, walkEnd, 0.45f);
            sb.AppendLine("✓ 화장실 소녀 길 — " + start.ToString("F2") + " → " + end.ToString("F2") + " (바깥쪽 칸)");
        }

        // ⑥ 경비실 창밖의 검은 남자(피날레 K4 결말) — 로비에서 경비실 서쪽 창 안을 본다(민 스크린샷).
        {
            Vector3 at = Floor(new Vector3(29.35f, 1.5f, 45.85f));
            Anchor(parent.transform, NightDuty.StageAnchors.FinaleWindow, at, Quaternion.LookRotation(Vector3.right), null, Vector3.zero, Vector3.zero, 3.5f);
            sb.AppendLine("✓ 경비실 창밖 검은 남자 자리 — " + at.ToString("F2") + " (창 안쪽을 봄)");
        }

        // ⑦ 내 자리 뒤에 선 무언가(피날레 「봤다」 결말) — 경비실 CRT 화면 앞 1.05m(CCTV 보는 자리 바로 뒤), CRT를 본다(꺼진 화면에 비친다).
        {
            CctvConfigSO cfg = CctvConfigSO.Load();
            GameObject monitor = cfg != null ? GameObject.Find(cfg.MonitorPath) : null;
            if (monitor != null)
            {
                Vector3 center = monitor.transform.TransformPoint(cfg.ScreenLocalCenter);
                Vector3 outward = Flat(monitor.transform.TransformDirection(Vector3.down));   // CctvSystem.ScreenOutward와 같다
                Vector3 at = Floor(center + outward * 1.05f);
                Anchor(parent.transform, NightDuty.StageAnchors.FinaleSeat, at, Quaternion.LookRotation(-outward), null, Vector3.zero, Vector3.zero, 1.6f);
                sb.AppendLine("✓ 내 자리 뒤 무언가 자리 — " + at.ToString("F2") + " (CRT를 봄)");
            }
            else
            {
                sb.AppendLine("✗ 경비실 CRT를 못 찾아 「내 자리 뒤 무언가」 자리를 놓지 못했습니다.");
            }
        }

        EditorSceneManager.MarkSceneDirty(parent.scene);
        return sb.ToString();
    }

    private static Vector3 Floor(Vector3 p)
    {
        RaycastHit hit;
        if (Physics.Raycast(new Vector3(p.x, p.y + 1.5f, p.z), Vector3.down, out hit, 4f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return hit.point;
        return p;
    }

    private static Transform Anchor(Transform parent, string id, Vector3 position, Quaternion rotation, string proxyName, Vector3 proxyPos, Vector3 proxySize, float viewDistance)
    {
        Transform t = parent.Find(id);
        if (t == null)
        {
            GameObject go = new GameObject(id);
            Undo.RegisterCreatedObjectUndo(go, "고정 몹 자리");
            t = go.transform;
            t.SetParent(parent, false);
        }

        t.SetPositionAndRotation(position, rotation);
        StageAnchor a = t.GetComponent<StageAnchor>();
        if (a == null) a = t.gameObject.AddComponent<StageAnchor>();

        Transform proxy = null;
        if (!string.IsNullOrEmpty(proxyName))
        {
            proxy = t.Find(proxyName);
            if (proxy == null)
            {
                proxy = new GameObject(proxyName).transform;
                proxy.SetParent(t, false);
            }

            proxy.SetPositionAndRotation(proxyPos, rotation);
            proxy.localScale = proxySize;
        }

        a.Configure(id, proxy, viewDistance);
        EditorUtility.SetDirty(a);
        return t;
    }

    // ── 대역 사진(확인용) ───────────────────────────────────

    /// <summary>모든 대역 프리팹을 한 장에 찍는다(위 줄 = 앞 비스듬히, 아래 줄 = 옆). 빨강 = 피벗, 하늘 = 조준점(Aim), 파랑 막대 = 앞(+Z).</summary>
    [MenuItem("야간근무/연출/대역 사진 찍기 (Temp/standins.png)")]
    public static void GalleryMenu()
    {
        string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/standins.png"));
        Debug.Log(RenderGallery(path));
        EditorUtility.RevealInFinder(path);
    }

    /// <summary>대역 사진을 <paramref name="pngPath"/>에 쓴다. 보고 문자열을 돌려준다.</summary>
    public static string RenderGallery(string pngPath)
    {
        List<string> ids = new List<string>();
        foreach (string g in AssetDatabase.FindAssets("t:Prefab", new[] { OutDir })) ids.Add(Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(g)));
        ids.Sort(StringComparer.Ordinal);
        if (ids.Count == 0) return "대역 프리팹이 없습니다.";

        UnityEngine.SceneManagement.Scene scene = EditorSceneManager.NewPreviewScene();
        RenderTexture rt = null;
        StringBuilder sb = new StringBuilder();
        try
        {
            Func<GameObject, GameObject> put = go =>
            {
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
                return go;
            };

            Camera cam = put(new GameObject("cam")).AddComponent<Camera>();
            cam.scene = scene;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.32f, 0.32f, 0.36f);
            cam.fieldOfView = 35f;
            Light light = put(new GameObject("light")).AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.4f;
            light.transform.rotation = Quaternion.Euler(35f, 160f, 0f);

            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
            Func<PrimitiveType, Color, GameObject> prim = (type, color) =>
            {
                GameObject g = put(GameObject.CreatePrimitive(type));
                UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());
                Material m = new Material(unlit);
                m.SetColor("_BaseColor", color);
                g.GetComponent<Renderer>().sharedMaterial = m;
                return g;
            };

            GameObject floor = prim(PrimitiveType.Cube, new Color(0.2f, 0.25f, 0.2f));
            floor.transform.localScale = new Vector3(3f, 0.01f, 3f);
            floor.transform.position = new Vector3(0f, -0.005f, 0f);
            prim(PrimitiveType.Sphere, Color.red).transform.localScale = Vector3.one * 0.08f;
            GameObject aimMark = prim(PrimitiveType.Sphere, Color.cyan);
            aimMark.transform.localScale = Vector3.one * 0.1f;
            GameObject fwd = prim(PrimitiveType.Cube, Color.blue);
            fwd.transform.localScale = new Vector3(0.03f, 0.03f, 0.5f);
            fwd.transform.position = new Vector3(0f, 0.01f, 0.25f);

            const int tw = 220, th = 300;
            Texture2D sheet = new Texture2D(tw * ids.Count, th * 2, TextureFormat.RGB24, false);
            rt = new RenderTexture(tw, th, 24);
            cam.targetTexture = rt;
            Texture2D shot = new Texture2D(tw, th, TextureFormat.RGB24, false);
            for (int i = 0; i < ids.Count; i++)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(OutDir + "/" + ids[i] + ".prefab");
                GameObject go = put((GameObject)UnityEngine.Object.Instantiate(prefab));
                Transform aim = go.transform.Find("Aim");
                aimMark.transform.position = aim != null ? aim.position : Vector3.zero;
                Bounds b = BakedBounds(go);
                sb.AppendLine(ids[i] + " 높이 " + b.min.y.ToString("F2") + "~" + b.max.y.ToString("F2") + " · 조준 " + (aim != null ? aim.localPosition.ToString("F2") : "없음"));
                float size = Mathf.Max(1.2f, b.size.y);
                for (int row = 0; row < 2; row++)
                {
                    Vector3 dir = row == 0 ? new Vector3(0.55f, 0.15f, 1f) : new Vector3(1f, 0.1f, 0f);
                    cam.transform.position = b.center + dir.normalized * size * 2.4f;
                    cam.transform.LookAt(b.center);
                    cam.Render();
                    RenderTexture.active = rt;
                    shot.ReadPixels(new Rect(0, 0, tw, th), 0, 0);
                    shot.Apply();
                    RenderTexture.active = null;
                    sheet.SetPixels(i * tw, (1 - row) * th, tw, th, shot.GetPixels());
                }

                UnityEngine.Object.DestroyImmediate(go);
            }

            sheet.Apply();
            Directory.CreateDirectory(Path.GetDirectoryName(pngPath));
            File.WriteAllBytes(pngPath, sheet.EncodeToPNG());
            sb.Insert(0, "대역 사진 " + ids.Count + "개 → " + pngPath + "\n" + string.Join(" · ", ids) + "\n");
        }
        finally
        {
            foreach (Camera c in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)) if (c.targetTexture == rt) c.targetTexture = null;
            EditorSceneManager.ClosePreviewScene(scene);
            if (rt != null) UnityEngine.Object.DestroyImmediate(rt);
        }

        return sb.ToString();
    }

    // ── 도구 ───────────────────────────────────────────────

    private static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.forward;
    }

    private static Transform FindDeep(Transform root, string name)
    {
        if (root.name == name) return root;
        foreach (Transform c in root)
        {
            Transform f = FindDeep(c, name);
            if (f != null) return f;
        }

        return null;
    }

    /// <summary>지금 자세의 실제 정점 경계(월드). <paramref name="below"/>가 있으면 그 높이 아래 정점만.</summary>
    private static Bounds BakedBounds(GameObject go, float? below = null)
    {
        bool any = false;
        Bounds b = new Bounds();
        foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
        {
            Mesh mesh = null;
            Matrix4x4 m;
            bool baked = false;
            SkinnedMeshRenderer smr = r as SkinnedMeshRenderer;
            if (smr != null)
            {
                mesh = new Mesh();
                smr.BakeMesh(mesh, false);   // 실측(6000.3): false가 스케일이 들어간 로컬 정점을 준다 — 회전·위치만 곱한다.
                baked = true;
                m = Matrix4x4.TRS(smr.transform.position, smr.transform.rotation, Vector3.one);
            }
            else
            {
                MeshFilter mf = r.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                mesh = mf.sharedMesh;
                m = r.transform.localToWorldMatrix;
            }

            foreach (Vector3 v in mesh.vertices)
            {
                Vector3 w = m.MultiplyPoint3x4(v);
                if (below.HasValue && w.y > below.Value) continue;
                if (!any)
                {
                    b = new Bounds(w, Vector3.zero);
                    any = true;
                }
                else b.Encapsulate(w);
            }

            if (baked) UnityEngine.Object.DestroyImmediate(mesh);
        }

        return b;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}

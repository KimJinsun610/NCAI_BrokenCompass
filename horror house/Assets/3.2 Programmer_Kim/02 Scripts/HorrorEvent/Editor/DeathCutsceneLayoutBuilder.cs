using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 배치 붙잡힘 컷신(<see cref="LayoutDeathCutscene"/>)의 프리팹을 만든다 — 사람 나무 프리팹과 소리를 연결한다.
/// 진행 수치(걷는 속도·끌려 드는 거리 등)는 프리팹 인스펙터에서 고친다. 메뉴를 다시 돌리면 그 수치도 기본값으로 돌아간다.
///
/// <para>소리는 <b>파일 이름</b>으로 찾는다. 기획 지정 소리 <c>End_C_01_…</c>·<c>End_C_03_AdultAmongWhispers</c>는 이름이
/// 그것으로 시작하는 파일을 프로젝트 어디에든 넣고 메뉴를 다시 돌리면 연결된다(없으면 그 겹만 빠진다).</para>
/// </summary>
public static class DeathCutsceneLayoutBuilder
{
    private const string PrefabFolder = "Assets/3.2 Programmer_Kim/03 Prefebs/04 Horror/Resources";
    private const string PrefabPath = PrefabFolder + "/" + LayoutDeathCutscene.ResourceName + ".prefab";
    private const string TreePrefab = "Assets/3.2 Programmer_Kim/03 Prefebs/04 Horror/Creature/HorrorCreature_HumanTree_Moving.prefab";

    [MenuItem("Tools/Programmer_Kim/Horror/Build Death Cutscene (Layout)")]
    public static void BuildMenu()
    {
        if (File.Exists(PrefabPath) &&
            !EditorUtility.DisplayDialog("사망 컷신 (배치)", "이미 있는 프리팹을 새로 만듭니다.\n인스펙터에서 고친 수치는 기본값으로 돌아갑니다.", "새로 만들기", "취소"))
        {
            return;
        }
        Debug.Log(Build());
    }

    public static string Build()
    {
        var missing = new List<string>();
        var root = new GameObject(LayoutDeathCutscene.ResourceName);
        var cutscene = root.AddComponent<LayoutDeathCutscene>();
        var so = new SerializedObject(cutscene);

        GameObject tree = AssetDatabase.LoadAssetAtPath<GameObject>(TreePrefab);
        if (tree == null) missing.Add("사람 나무 프리팹");
        so.FindProperty("treePrefab").objectReferenceValue = tree;

        // 발소리 — 복도 타일(출입문 쪽 복도). 에코·잔향은 컷신이 걸음마다 키운다
        var steps = new List<AudioClip>();
        for (int i = 1; i <= 5; i++) Add(steps, Clip("SFX_STEP_HALL_Tile_Walk_0" + i, missing));
        SetArray(so.FindProperty("stepClips"), steps);

        // 시작할 때는 울리는 발소리만 — 비틀 소리 없음(2026-10-05 사용자: 떨리는 현 CLX-29_2 「또로롱」, 숨 CAP-H1-06_2 「꺄아앙」 뺌)
        SetHits(so.FindProperty("startHits"), missing);

        // 걸어가는 동안 차오르는 겹 — 순서대로 (이름, 볼륨, 시작 진행도, 최대 진행도, 나무 자리 3D)
        // 처음 30%는 발소리만 들리게 모두 0.3 이후에 시작
        var layers = new[]
        {
            L("End_C_03", 0.85f, 0.30f, 0.90f, false, prefix: true),                // 기획 지정 — 어른들 사이 속삭임
            L("End_C_01", 0.75f, 0.45f, 1.00f, false, prefix: true),                // 기획 지정
            L("CAP_H1_02_TangledWhispers", 0.55f, 0.30f, 0.85f, false),             // 엉킨 속삭임(배치 붙잡힘용)
            L("SFX_TREE_HumanMurmur", 0.70f, 0.30f, 0.75f, true),                   // 사람 나무 웅얼거림 — 나무에서(원본이 커서 심장을 덮지 않게 0.7)
            L("SFX_TREE_BranchCreak", 0.90f, 0.30f, 0.70f, true),                   // 가지 삐걱 — 나무에서
            L("CLX-30_2", 0.45f, 0.40f, 1.00f, false),                              // 낮은 웅—
            // 두근거리는 심장 — 다가갈수록 크고 빨라진다(몸 계기의 분당 72 → 86 → 104 루프를 이어 넘김)
            // 원본이 작게(최대 0.18) 녹음돼 있어 3겹으로 겹쳐 튼다(≈3배, 최대 0.54 — 찌그러지지 않음)
            L("SFX_BODY_Heartbeat_72", 0.60f, 0.02f, 0.25f, false, outFrom: 0.35f, outTo: 0.50f, copies: 3),
            L("SFX_BODY_Heartbeat_86", 0.80f, 0.35f, 0.50f, false, outFrom: 0.68f, outTo: 0.80f, copies: 3),
            L("SFX_BODY_Heartbeat_104", 1.00f, 0.68f, 0.80f, false, copies: 3),
        };
        SerializedProperty layerProp = so.FindProperty("layers");
        layerProp.arraySize = 0;
        foreach (var l in layers)
        {
            AudioClip c = l.prefix ? ClipByPrefix(l.name, missing) : Clip(l.name, missing);
            if (c == null) continue;
            layerProp.arraySize++;
            SerializedProperty e = layerProp.GetArrayElementAtIndex(layerProp.arraySize - 1);
            e.FindPropertyRelative("clip").objectReferenceValue = c;
            e.FindPropertyRelative("volume").floatValue = l.volume;
            e.FindPropertyRelative("fromProgress").floatValue = l.from;
            e.FindPropertyRelative("fullProgress").floatValue = l.full;
            e.FindPropertyRelative("atTree").boolValue = l.atTree;
            e.FindPropertyRelative("fadeOutFrom").floatValue = l.outFrom;
            e.FindPropertyRelative("fadeOutTo").floatValue = l.outTo;
            e.FindPropertyRelative("copies").intValue = l.copies;
            e.FindPropertyRelative("clipStart").floatValue = 0f;
        }

        // 빨려 듦 — 거꾸로 감은 심벌. 가장 큰 지점이 끌려 드는 순간에 오도록 컷신이 미리 건다
        AudioClip suck = Clip("CLX-08_3", missing);
        SerializedProperty suckProp = so.FindProperty("suckIn");
        suckProp.FindPropertyRelative("clip").objectReferenceValue = suck;
        suckProp.FindPropertyRelative("volume").floatValue = 0.9f;
        suckProp.FindPropertyRelative("clipStart").floatValue = 0f;
        so.FindProperty("suckInPeak").floatValue = suck != null ? Peak(suck) : 2.8f;

        // 끌려 드는 순간 — 소년·나무 중음 · 저음 · 가지 고음 · 가까운 충격 · 속삭임 폭발(모두 가장 큰 지점부터)
        SetHits(so.FindProperty("pullHits"), missing,
            H("CLX-01_3", 1f, atPeak: true),
            H("CLX-04_3", 1f, atPeak: true),
            H("CLX-21_2", 0.8f, atPeak: true),
            H("SFX_STINGER_CloseImpact_01", 0.8f, atPeak: true),
            H("CLX-22_2", 0.7f, atPeak: true));

        // 완전히 검어진 뒤 — 마지막 쿵
        SetHits(so.FindProperty("endHits"), missing, H("CLX-12_2", 0.6f, atPeak: true));

        so.ApplyModifiedPropertiesWithoutUndo();

        if (!AssetDatabase.IsValidFolder(PrefabFolder))
        {
            AssetDatabase.CreateFolder(Path.GetDirectoryName(PrefabFolder).Replace('\\', '/'), "Resources");
        }
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool saved);
        UnityEngine.Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();

        return "[DeathCutsceneLayoutBuilder] 프리팹 " + (saved ? "저장" : "실패") + " " + PrefabPath + " · 발소리 " + steps.Count + " · 겹 " + layerProp.arraySize +
               (missing.Count > 0 ? " · <b>없음: " + string.Join(", ", missing) + "</b>(파일을 넣고 메뉴를 다시 실행)" : " · 빠진 것 없음");
    }

    // ─────────────────────────────── 도우미 ───────────────────────────────

    private struct LayerSpec { public string name; public float volume, from, full, outFrom, outTo; public bool atTree, prefix; public int copies; }

    private static LayerSpec L(string name, float volume, float from, float full, bool atTree, bool prefix = false, float outFrom = 1.01f, float outTo = 1.01f, int copies = 1)
    {
        return new LayerSpec { name = name, volume = volume, from = from, full = full, atTree = atTree, prefix = prefix, outFrom = outFrom, outTo = outTo, copies = copies };
    }

    private struct HitSpec { public string name; public float volume; public bool atPeak; }

    private static HitSpec H(string name, float volume, bool atPeak)
    {
        return new HitSpec { name = name, volume = volume, atPeak = atPeak };
    }

    private static void SetHits(SerializedProperty prop, List<string> missing, params HitSpec[] hits)
    {
        prop.arraySize = 0;
        foreach (HitSpec h in hits)
        {
            AudioClip c = Clip(h.name, missing);
            if (c == null) continue;
            prop.arraySize++;
            SerializedProperty e = prop.GetArrayElementAtIndex(prop.arraySize - 1);
            e.FindPropertyRelative("clip").objectReferenceValue = c;
            e.FindPropertyRelative("volume").floatValue = h.volume;
            // 가장 큰 지점 0.04초 앞부터 — 정적을 깨지 않고 그 프레임에 터진다(클라이맥스 사운드와 같은 규칙)
            e.FindPropertyRelative("clipStart").floatValue = h.atPeak ? Mathf.Max(0f, Peak(c) - 0.04f) : 0f;
            e.FindPropertyRelative("delay").floatValue = 0f;
        }
    }

    private static float Peak(AudioClip clip)
    {
        return (float)DeathCutsceneSoundLayout.WavInfo.Read(AssetDatabase.GetAssetPath(clip)).peak;
    }

    private static AudioClip Clip(string fileName, List<string> missing)
    {
        AudioClip c = DeathCutsceneSoundLayout.FindClip(fileName);
        if (c == null && !missing.Contains(fileName)) missing.Add(fileName);
        return c;
    }

    /// <summary>이름이 <paramref name="prefix"/>로 시작하는 오디오 클립(여럿이면 이름순 첫 번째).</summary>
    private static AudioClip ClipByPrefix(string prefix, List<string> missing)
    {
        string best = null;
        foreach (string guid in AssetDatabase.FindAssets(prefix + " t:AudioClip"))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            string n = Path.GetFileNameWithoutExtension(p);
            if (!n.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
            if (best == null || string.CompareOrdinal(n, Path.GetFileNameWithoutExtension(best)) < 0) best = p;
        }
        if (best == null)
        {
            missing.Add(prefix + "*");
            return null;
        }
        return AssetDatabase.LoadAssetAtPath<AudioClip>(best);
    }

    private static void Add(List<AudioClip> list, AudioClip c)
    {
        if (c != null) list.Add(c);
    }

    private static void SetArray<T>(SerializedProperty prop, List<T> items) where T : UnityEngine.Object
    {
        prop.arraySize = items.Count;
        for (int i = 0; i < items.Count; i++) prop.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
    }
}

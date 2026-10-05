using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 실험실 싱크대 물방울 프리팹을 만든다(여러 번 눌러도 같은 결과로 덮어씀).
/// <list type="number">
/// <item>소리 — <c>SFX_LAB_SinkDrip_1·2</c>(여러 방울을 한 파일에 녹음)를 <b>한 방울씩</b> 잘라 <c>Liquid/Audio</c>에 둔다.
///   <c>_3</c>은 물 흐르는 소리가 깔려 있어 쓰지 않는다.</item>
/// <item>재질 — 핏물 재질(<c>M_Liquid_DarkRed</c>·<c>M_Splash_DarkRed</c>)을 복사해 검푸른색으로.</item>
/// <item>튐 — <c>PS_Splash_DarkRed</c>를 복사해 작게, 검푸른 재질로.</item>
/// <item>프리팹 — <c>Laboratory_Sink</c> 모델 + 수도꼭지 끝(모델 메시에서 잼) + <see cref="LiquidDrip"/>.</item>
/// </list>
/// 원본(핏물 에셋·소리·모델)은 고치지 않는다.
/// </summary>
public static class LiquidDripBuilder
{
    private const string Root = "Assets/3.2 Programmer_Kim/03 Prefebs/04 Horror/Liquid";
    private const string AudioFolder = Root + "/Audio";
    private const string PrefabPath = Root + "/HorrorProp_LabSinkDrip.prefab";

    private const string SinkModel = "Assets/2. Art/03 Prefebs/f_sciencelab/Laboratory_Sink.fbx";
    private const string BloodFolder = "Assets/3.2 Programmer_Kim/03 Prefebs/04 Horror/Bloody/BloodyFontain";
    private const string LiquidSrc = BloodFolder + "/M_Liquid_DarkRed.mat";
    private const string SplashMatSrc = BloodFolder + "/M_Splash_DarkRed.mat";
    private const string SplashSrc = BloodFolder + "/PS_Splash_DarkRed.prefab";

    private static readonly string[] SoundSources =
    {
        "Assets/_Game/Audio/lab/SFX_LAB_SinkDrip_1.wav",
        "Assets/_Game/Audio/lab/SFX_LAB_SinkDrip_2.wav",
    };

    // 검푸른 물 — 거의 검정에 가까운 남색, 빛 받는 곳만 푸르게
    private static readonly Color MainColor = new Color(0.010f, 0.035f, 0.075f, 1f);
    private static readonly Color SecondaryColor = new Color(0.002f, 0.010f, 0.028f, 1f);
    private static readonly Color SpecularColor = new Color(0.65f, 0.80f, 1f, 1f);

    // Laboratory_Sink 메시의 재질 순서: wood, stone, ceramic, metal, dark
    private const int MetalSubmesh = 3;

    [MenuItem("Tools/Programmer_Kim/Liquid/Build Lab Sink Drip")]
    public static void Build()
    {
        EnsureFolder(Root);
        EnsureFolder(AudioFolder);

        List<AudioClip> clips = SliceDropSounds();
        Material liquid = CopyMaterial(LiquidSrc, Root + "/M_Liquid_DarkBlue.mat");
        Material splashMat = CopyMaterial(SplashMatSrc, Root + "/M_Splash_DarkBlue.mat");
        GameObject splash = BuildSplash(splashMat);
        BuildPrefab(liquid, splash, clips);

        AssetDatabase.SaveAssets();
        Debug.Log($"[LiquidDripBuilder] {PrefabPath} 완료 — 방울 소리 {clips.Count}개");
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
    }

    // ───────────────────────── 프리팹 ─────────────────────────

    private static void BuildPrefab(Material liquid, GameObject splashPrefab, List<AudioClip> clips)
    {
        var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(SinkModel);
        if (modelAsset == null)
        {
            Debug.LogError($"[LiquidDripBuilder] 모델이 없습니다: {SinkModel}");
            return;
        }

        // 열린 씬을 건드리지 않도록 임시 프리뷰 씬에서 조립한다
        var stage = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
        var root = new GameObject("HorrorProp_LabSinkDrip");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, stage);
        try
        {
            var model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset, root.transform);
            model.name = "Laboratory_Sink";

            MeshFilter mf = model.GetComponentInChildren<MeshFilter>();
            var col = mf.GetComponent<MeshCollider>();
            if (col == null) col = mf.gameObject.AddComponent<MeshCollider>();
            col.sharedMesh = mf.sharedMesh;

            Vector3 spoutPos = FindSpout(mf);
            float fall = MeasureFall(col, spoutPos);

            var spout = new GameObject("Spout").transform;
            spout.SetParent(root.transform, false);
            spout.localPosition = spoutPos;

            // 방울 — 기본 구 메시, 그림자 없음, 충돌체 없음
            var drop = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.DestroyImmediate(drop.GetComponent<Collider>());
            drop.name = "Droplet";
            drop.transform.SetParent(root.transform, false);
            drop.transform.localPosition = spoutPos;
            drop.transform.localScale = Vector3.one * 0.012f;
            var dr = drop.GetComponent<MeshRenderer>();
            dr.sharedMaterial = liquid;
            dr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            dr.receiveShadows = false;
            drop.SetActive(false);

            var splash = (GameObject)PrefabUtility.InstantiatePrefab(splashPrefab, root.transform);
            splash.name = "Splash";
            splash.transform.localPosition = spoutPos + Vector3.down * fall;

            var audioGo = new GameObject("AUD_Drop");
            audioGo.transform.SetParent(root.transform, false);
            audioGo.transform.localPosition = spoutPos + Vector3.down * fall;
            var src = audioGo.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = false;
            src.spatialBlend = 1f;            // 기존 AUD_DripLoop과 같은 거리감
            src.rolloffMode = AudioRolloffMode.Logarithmic;
            src.minDistance = 0.6f;
            src.maxDistance = 12f;
            src.dopplerLevel = 0f;

            var drip = root.AddComponent<LiquidDrip>();
            var so = new SerializedObject(drip);
            so.FindProperty("spout").objectReferenceValue = spout;
            so.FindProperty("droplet").objectReferenceValue = drop.transform;
            so.FindProperty("fallbackFallDistance").floatValue = fall;
            so.FindProperty("splash").objectReferenceValue = splash.GetComponent<ParticleSystem>();
            so.FindProperty("dropSource").objectReferenceValue = src;
            var arr = so.FindProperty("dropClips");
            arr.arraySize = clips.Count;
            for (int i = 0; i < clips.Count; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = clips[i];
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
            UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(stage);
        }
    }

    /// <summary>
    /// 수도꼭지 끝 = 금속 메시 중 몸통(뒤쪽 기둥) 앞에서 가장 낮은 점들의 가운데, 그보다 4mm 아래.
    /// 루트 기준 좌표로 돌려준다(모델 루트는 루트의 원점).
    /// </summary>
    private static Vector3 FindSpout(MeshFilter mf)
    {
        Mesh mesh = mf.sharedMesh;
        Transform tr = mf.transform;
        Transform root = tr.root;
        Vector3[] v = mesh.vertices;
        int[] idx = mesh.GetIndices(MetalSubmesh);

        // 꼭지 목: 가운데(|x| 작음) · 높은 곳(y > 1.0) · 기둥 앞(z > -0.2)
        float minY = float.MaxValue;
        var pts = new List<Vector3>();
        foreach (int i in idx)
        {
            Vector3 p = root.InverseTransformPoint(tr.TransformPoint(v[i]));
            if (Mathf.Abs(p.x) > 0.05f || p.y < 1.0f || p.z < -0.2f || p.z > 0.05f) continue;
            pts.Add(p);
            if (p.y < minY) minY = p.y;
        }

        if (pts.Count == 0)
        {
            Debug.LogWarning("[LiquidDripBuilder] 수도꼭지를 찾지 못해 실측값(0, 1.15, -0.1)을 씁니다.");
            return new Vector3(0f, 1.15f, -0.1f);
        }

        Vector3 sum = Vector3.zero;
        int n = 0;
        foreach (Vector3 p in pts)
        {
            if (p.y > minY + 0.012f) continue;
            sum += p;
            n++;
        }
        Vector3 c = sum / n;
        return new Vector3(c.x, minY - 0.004f, c.z);
    }

    private static float MeasureFall(MeshCollider col, Vector3 spoutLocal)
    {
        Physics.SyncTransforms();
        Vector3 from = col.transform.root.TransformPoint(spoutLocal);
        if (col.Raycast(new Ray(from, Vector3.down), out RaycastHit hit, 3f)) return hit.distance;

        Debug.LogWarning("[LiquidDripBuilder] 수도꼭지 아래 면을 찾지 못해 0.43m로 둡니다.");
        return 0.43f;
    }

    // ───────────────────────── 재질 · 튐 ─────────────────────────

    private static Material CopyMaterial(string src, string dst)
    {
        var source = AssetDatabase.LoadAssetAtPath<Material>(src);
        var mat = AssetDatabase.LoadAssetAtPath<Material>(dst);
        if (mat == null)
        {
            mat = new Material(source);
            AssetDatabase.CreateAsset(mat, dst);
        }
        else
        {
            mat.CopyPropertiesFromMaterial(source);
        }

        SetColor(mat, "_MainColor", MainColor);
        SetColor(mat, "_SecondaryColor", SecondaryColor);
        SetColor(mat, "_SpecularColor", SpecularColor);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static void SetColor(Material m, string prop, Color c)
    {
        if (m.HasProperty(prop)) m.SetColor(prop, c);
    }

    private static GameObject BuildSplash(Material mat)
    {
        string dst = Root + "/PS_Splash_DarkBlue.prefab";
        var src = AssetDatabase.LoadAssetAtPath<GameObject>(SplashSrc);
        var stage = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
        var go = Object.Instantiate(src);
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, stage);
        go.name = "PS_Splash_DarkBlue";
        try
        {
            var ps = go.GetComponent<ParticleSystem>();
            var main = ps.main;
            main.playOnAwake = false;                       // LiquidDrip이 닿을 때 튼다
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.045f); // 분수용(0.05~0.1)보다 작게 — 한 방울
            main.startLifetime = 0.35f;
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = mat;
            return PrefabUtility.SaveAsPrefabAsset(go, dst);
        }
        finally
        {
            Object.DestroyImmediate(go);
            UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(stage);
        }
    }

    // ───────────────────────── 소리 자르기 ─────────────────────────

    /// <summary>
    /// 포락선(10ms)의 봉우리마다 한 방울: 봉우리 20ms 앞부터 다음 봉우리 30ms 앞(최대 0.4초)까지,
    /// 끝 40%는 줄여 끊고 크기를 0.7로 맞춘다. 작은 봉우리(최대의 35% 미만)는 버린다 — 잡음이나 잔향이다.
    /// </summary>
    private static List<AudioClip> SliceDropSounds()
    {
        var made = new List<string>();
        int count = 0;
        foreach (string path in SoundSources)
        {
            if (!ReadWav(path, out float[] x, out int rate))
            {
                Debug.LogWarning($"[LiquidDripBuilder] 읽지 못했습니다: {path}");
                continue;
            }

            foreach (float[] drop in Slice(x, rate))
            {
                count++;
                string dst = $"{AudioFolder}/SFX_LiquidDrop_{count:00}.wav";
                WriteWav(dst, drop, rate);
                made.Add(dst);
            }
        }

        // 지난번보다 적게 나왔으면 남는 파일은 지운다
        for (int i = count + 1; i < 100; i++)
        {
            string old = $"{AudioFolder}/SFX_LiquidDrop_{i:00}.wav";
            if (!File.Exists(old)) break;
            AssetDatabase.DeleteAsset(old);
        }

        AssetDatabase.Refresh();

        var clips = new List<AudioClip>();
        foreach (string p in made)
        {
            if (AssetImporter.GetAtPath(p) is AudioImporter imp)
            {
                imp.forceToMono = true;
                var s = imp.defaultSampleSettings;
                s.loadType = AudioClipLoadType.DecompressOnLoad;   // 짧고 자주 난다
                s.compressionFormat = AudioCompressionFormat.ADPCM;
                imp.defaultSampleSettings = s;
                imp.SaveAndReimport();
            }
            clips.Add(AssetDatabase.LoadAssetAtPath<AudioClip>(p));
        }
        return clips;
    }

    private static List<float[]> Slice(float[] x, int rate)
    {
        int win = rate / 100;
        int n = x.Length / win;
        var env = new float[n];
        float max = 0f;
        for (int w = 0; w < n; w++)
        {
            float m = 0f;
            for (int s = w * win; s < (w + 1) * win; s++) m = Mathf.Max(m, Mathf.Abs(x[s]));
            env[w] = m;
            max = Mathf.Max(max, m);
        }

        var peaks = new List<int>();
        int last = -100;
        for (int w = 1; w < n - 1; w++)
        {
            if (env[w] < max * 0.35f || env[w] < env[w - 1] || env[w] < env[w + 1] || w - last <= 20) continue;
            peaks.Add(w);
            last = w;
        }

        var drops = new List<float[]>();
        for (int i = 0; i < peaks.Count; i++)
        {
            int start = Mathf.Max(0, (peaks[i] - 2) * win);
            int end = Mathf.Min(x.Length, start + (int)(0.4f * rate));
            if (i + 1 < peaks.Count) end = Mathf.Min(end, (peaks[i + 1] - 3) * win);
            if (end - start < rate / 20) continue; // 50ms도 안 되면 버림

            var d = new float[end - start];
            System.Array.Copy(x, start, d, 0, d.Length);

            int fade = (int)(d.Length * 0.4f);
            for (int s = 0; s < fade; s++) d[d.Length - 1 - s] *= s / (float)fade;
            int fadeIn = Mathf.Min(48, d.Length / 10); // 1ms — 잘린 자리 딸깍 방지
            for (int s = 0; s < fadeIn; s++) d[s] *= s / (float)fadeIn;

            float peak = 0f;
            foreach (float a in d) peak = Mathf.Max(peak, Mathf.Abs(a));
            if (peak > 0f) for (int s = 0; s < d.Length; s++) d[s] *= 0.7f / peak;

            drops.Add(d);
        }
        return drops;
    }

    private static bool ReadWav(string path, out float[] mono, out int rate)
    {
        mono = null;
        rate = 0;
        if (!File.Exists(path)) return false;

        byte[] b = File.ReadAllBytes(path);
        int ch = 1, bits = 16, pos = 12, off = -1, len = 0;
        while (pos + 8 <= b.Length)
        {
            string id = System.Text.Encoding.ASCII.GetString(b, pos, 4);
            int size = System.BitConverter.ToInt32(b, pos + 4);
            if (id == "fmt ")
            {
                ch = System.BitConverter.ToInt16(b, pos + 10);
                rate = System.BitConverter.ToInt32(b, pos + 12);
                bits = System.BitConverter.ToInt16(b, pos + 22);
            }
            else if (id == "data")
            {
                off = pos + 8;
                len = Mathf.Min(size, b.Length - off);
                break;
            }
            pos += 8 + size + (size & 1);
        }
        if (off < 0 || bits != 16) return false;

        int frames = len / (2 * ch);
        mono = new float[frames];
        for (int f = 0; f < frames; f++)
        {
            float sum = 0f;
            for (int c = 0; c < ch; c++) sum += System.BitConverter.ToInt16(b, off + (f * ch + c) * 2) / 32768f;
            mono[f] = sum / ch;
        }
        return true;
    }

    private static void WriteWav(string path, float[] x, int rate)
    {
        using (var w = new BinaryWriter(File.Create(path)))
        {
            int dataLen = x.Length * 2;
            w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            w.Write(36 + dataLen);
            w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
            w.Write(16);
            w.Write((short)1);
            w.Write((short)1);
            w.Write(rate);
            w.Write(rate * 2);
            w.Write((short)2);
            w.Write((short)16);
            w.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            w.Write(dataLen);
            foreach (float s in x) w.Write((short)Mathf.Clamp(Mathf.RoundToInt(s * 32767f), -32768, 32767));
        }
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}

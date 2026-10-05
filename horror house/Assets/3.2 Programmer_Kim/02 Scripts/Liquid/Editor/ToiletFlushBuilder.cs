using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 핏물이 소용돌이치며 내려가는 변기 <c>HorrorProp_ToiletA_BloodFlush</c>를 만든다(여러 번 눌러도 같은 결과로 덮어씀).
/// <list type="bullet">
/// <item>그릇 모양 — 수면 높이 0.25m부터 그릇 바닥 테(0.19m) · 배수 구멍 속(0.15m)까지 높이마다 그릇 가장자리를 재서
///   <see cref="ToiletFlush"/>에 굽는다(실측: 반지름 평균 0.14 → 0.06m, 아래로 갈수록 둥글어짐).</item>
/// <item>재질·그릇 충돌체·피 VFX 복사는 <see cref="ToiletWaterBuilder"/>와 같은 것을 쓴다(붉은 물 색도 같음).</item>
/// <item>소용돌이 — 나선 무늬 텍스처를 구워 수평 원판에 붙인다(프로젝트에 소용돌이 텍스처가 없음).</item>
/// <item>튀는 물방울 — 피 팩 <c>PS_SplatterDirectional_02</c> 사본: 작은 방울을 위로 넓게, 그릇에 닿으면 사라짐.</item>
/// <item>소리 — <c>SFX_TOILET_Flush_Hear25</c>(T1 물 내림과 같은 파일).</item>
/// </list>
/// </summary>
public static class ToiletFlushBuilder
{
    private const string Root = ToiletWaterBuilder.Root;
    private const string PrefabPath = Root + "/HorrorProp_ToiletA_BloodFlush.prefab";
    private const string FlushSound = "Assets/_Game/Audio/toilet/SFX_TOILET_Flush_Hear25.wav";

    /// <summary>수위 표 — 가득(0.25)에서 그릇 바닥 테(0.19)까지 5mm마다, 그 뒤 배수 구멍 속으로.</summary>
    private static readonly float[] Levels =
        { 0.25f, 0.245f, 0.24f, 0.235f, 0.23f, 0.225f, 0.22f, 0.215f, 0.21f, 0.205f, 0.20f, 0.195f, 0.19f, 0.17f, 0.15f };

    /// <summary>배수 구멍 속(0.19 아래)은 구멍 벽보다 조금 좁혀 구멍 안으로 사라지게.</summary>
    private const float HoleShrink = 0.75f;

    // 처음 판(어두운 줄 0.05)은 붉은 물 위에서 거의 안 보였다 — 밝은 핏물 거품 줄 + 검게 빨려 드는 가운데의 두 톤으로
    private static readonly Color SwirlColor = new Color(0.75f, 0.16f, 0.13f, 0.9f);

    [MenuItem("Tools/Programmer_Kim/Liquid/Build Toilet Flush")]
    public static void Build()
    {
        ToiletWaterBuilder.EnsureFolder(Root);

        var toilet = AssetDatabase.LoadAssetAtPath<GameObject>(ToiletWaterBuilder.ToiletPrefab);
        if (toilet == null)
        {
            Debug.LogError($"[ToiletFlushBuilder] ToiletA가 없습니다: {ToiletWaterBuilder.ToiletPrefab}");
            return;
        }

        int seg = ToiletWaterBuilder.FanSegments;
        float[] radii = MeasureBowl(toilet, seg);
        Mesh full = FullMesh(radii, seg);
        Mesh disc = DiscMesh();
        Material blood = ToiletWaterBuilder.BloodWaterMaterial();
        Material swirlMat = SwirlMaterial();
        GameObject splash = BuildSplash();

        ToiletWaterBuilder.Assemble(PrefabPath, "HorrorProp_ToiletA_BloodFlush", toilet, root =>
        {
            ToiletWaterBuilder.UseBowlCollider(root); // 물방울이 볼록 껍질 속에서 바로 죽지 않게

            var waterGo = new GameObject("Water");
            waterGo.transform.SetParent(root, false);
            var mf = waterGo.AddComponent<MeshFilter>();
            mf.sharedMesh = full; // 편집 화면용 — 실행하면 ToiletFlush가 매 프레임 다시 만든다
            var wr = waterGo.AddComponent<MeshRenderer>();
            wr.sharedMaterial = blood;
            wr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            Vector3 c = new Vector3(ToiletWaterBuilder.BowlCenterXZ.x, Levels[0], ToiletWaterBuilder.BowlCenterXZ.y);

            var swirlGo = new GameObject("Swirl");
            swirlGo.transform.SetParent(root, false);
            swirlGo.transform.localPosition = c + Vector3.up * 0.0015f;
            swirlGo.transform.localScale = new Vector3(0.22f, 1f, 0.22f);
            swirlGo.AddComponent<MeshFilter>().sharedMesh = disc;
            var sr = swirlGo.AddComponent<MeshRenderer>();
            sr.sharedMaterial = swirlMat;
            sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var splashGo = (GameObject)PrefabUtility.InstantiatePrefab(splash, root);
            splashGo.name = "Splash";
            splashGo.transform.localPosition = c;
            splashGo.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f); // 원뿔(+Z)을 위로

            var audioGo = new GameObject("AUD_Flush");
            audioGo.transform.SetParent(root, false);
            audioGo.transform.localPosition = c;
            var src = audioGo.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = false;
            src.spatialBlend = 1f;
            src.rolloffMode = AudioRolloffMode.Logarithmic;
            src.minDistance = 1f;
            src.maxDistance = 15f;
            src.dopplerLevel = 0f;

            var flush = root.gameObject.AddComponent<ToiletFlush>();
            flush.BakeBowl(ToiletWaterBuilder.BowlCenterXZ, (float[])Levels.Clone(), seg, radii);
            var so = new SerializedObject(flush);
            so.FindProperty("water").objectReferenceValue = mf;
            so.FindProperty("swirl").objectReferenceValue = swirlGo.transform;
            so.FindProperty("splash").objectReferenceValue = splashGo.GetComponent<ParticleSystem>();
            so.FindProperty("flushSource").objectReferenceValue = src;
            so.FindProperty("flushClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(FlushSound);
            so.ApplyModifiedPropertiesWithoutUndo();
        });

        AssetDatabase.SaveAssets();
        Debug.Log($"[ToiletFlushBuilder] 완료 — {PrefabPath}");
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
    }

    // ───────────────────────── 그릇 재기 ─────────────────────────

    /// <summary>높이마다 · 방향마다 그릇 가장자리(겹침 포함). levels × segments.</summary>
    private static float[] MeasureBowl(GameObject toilet, int seg)
    {
        var stage = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
        var go = (GameObject)PrefabUtility.InstantiatePrefab(toilet, stage);
        try
        {
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) c.enabled = false;
            var lod0 = go.GetComponentInChildren<MeshFilter>();
            var col = lod0.gameObject.AddComponent<MeshCollider>();
            col.sharedMesh = lod0.sharedMesh;
            Physics.SyncTransforms();

            float rimLevel = 0.19f; // 그릇 바닥 테 — 이 아래는 배수 구멍
            var radii = new float[Levels.Length * seg];
            float[] atRim = new float[seg];
            for (int l = 0; l < Levels.Length; l++)
            {
                for (int i = 0; i < seg; i++)
                {
                    float a = i * Mathf.PI * 2f / seg;
                    var dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                    float r;
                    if (Levels[l] >= rimLevel - 0.0001f)
                    {
                        r = ToiletWaterBuilder.EdgeRadius(col, dir, Levels[l]) + ToiletWaterBuilder.Overlap;
                        if (Mathf.Approximately(Levels[l], rimLevel)) atRim[i] = r;
                    }
                    else
                    {
                        // 배수 구멍 속: 테 모양에서 점점 좁아져 구멍 안으로 사라진다(재면 구멍 가장자리만 나와 줄지 않는다)
                        float depth = Mathf.InverseLerp(rimLevel, Levels[Levels.Length - 1], Levels[l]);
                        r = atRim[i] * Mathf.Lerp(HoleShrink, 0.25f, depth);
                    }
                    radii[l * seg + i] = r;
                }
            }
            return radii;
        }
        finally
        {
            Object.DestroyImmediate(go);
            UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(stage);
        }
    }

    /// <summary>편집 화면에 보일 가득 찬 수면(첫 줄).</summary>
    private static Mesh FullMesh(float[] radii, int seg)
    {
        var c = new Vector3(ToiletWaterBuilder.BowlCenterXZ.x, Levels[0], ToiletWaterBuilder.BowlCenterXZ.y);
        var verts = new List<Vector3> { c };
        var tris = new List<int>();
        for (int i = 0; i < seg; i++)
        {
            float a = i * Mathf.PI * 2f / seg;
            verts.Add(c + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * radii[i]);
            tris.Add(0);
            tris.Add(i + 1);
            tris.Add((i + 1) % seg + 1);
        }
        var mesh = new Mesh { name = "Mesh_ToiletFlushWater_Full" };
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        var n = new Vector3[verts.Count];
        for (int i = 0; i < n.Length; i++) n[i] = Vector3.up;
        mesh.normals = n;
        mesh.RecalculateBounds();
        return ToiletWaterBuilder.SaveMesh(mesh, Root + "/Mesh_ToiletFlushWater_Full.asset");
    }

    /// <summary>XZ 평면의 지름 1 원판(위를 봄), UV는 원판에 맞춘 정사각.</summary>
    private static Mesh DiscMesh()
    {
        const int n = 48;
        var verts = new List<Vector3> { Vector3.zero };
        var uvs = new List<Vector2> { new Vector2(0.5f, 0.5f) };
        var tris = new List<int>();
        for (int i = 0; i < n; i++)
        {
            float a = i * Mathf.PI * 2f / n;
            var p = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * 0.5f;
            verts.Add(p);
            uvs.Add(new Vector2(0.5f + p.x, 0.5f + p.z));
            tris.Add(0);
            tris.Add(i + 1);
            tris.Add((i + 1) % n + 1);
        }
        var mesh = new Mesh { name = "Mesh_SwirlDisc" };
        mesh.SetVertices(verts);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        var nn = new Vector3[verts.Count];
        for (int i = 0; i < nn.Length; i++) nn[i] = Vector3.up;
        mesh.normals = nn;
        mesh.RecalculateBounds();
        mesh.RecalculateTangents();
        return ToiletWaterBuilder.SaveMesh(mesh, Root + "/Mesh_SwirlDisc.asset");
    }

    // ───────────────────────── 소용돌이 ─────────────────────────

    /// <summary>나선 줄무늬(투명) — 줄은 밝은 핏물 거품, 가운데는 검게. 붉은 수면 위에서 돌며 소용돌이로 보인다.</summary>
    private static Material SwirlMaterial()
    {
        Material m = ToiletWaterBuilder.LitMaterial(Root + "/M_ToiletFlush_Swirl.mat");
        m.SetTexture("_BaseMap", SwirlTexture());
        m.SetColor("_BaseColor", SwirlColor);
        m.SetFloat("_Smoothness", 0.92f);
        m.SetFloat("_Metallic", 0f);
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 0f);
        m.SetFloat("_ZWrite", 0f);
        UnityEditor.BaseShaderGUI.SetMaterialKeywords(m);
        EditorUtility.SetDirty(m);
        return m;
    }

    /// <summary>나선 팔 4개(로그 나선). RGB = 줄은 흰색(재질 색으로 물듦)·가운데로 갈수록 검정, A = 줄 + 가운데. 256².</summary>
    private static Texture2D SwirlTexture()
    {
        string path = Root + "/T_FlushSwirl.png";
        const int n = 256;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n * 2f - 1f;
                float v = (y + 0.5f) / n * 2f - 1f;
                float r = Mathf.Sqrt(u * u + v * v);
                float ang = Mathf.Atan2(v, u);
                // 로그 나선: 각도 + k·ln(r)이 같은 곳이 한 줄
                float phase = ang * 4f + 6f * Mathf.Log(Mathf.Max(r, 0.02f));
                float band = Mathf.Pow(0.5f + 0.5f * Mathf.Cos(phase), 1.5f); // 3제곱은 줄이 가늘어 붉은 물 위에서 안 보였다
                float edge = 1f - Mathf.SmoothStep(0.7f, 1f, r);   // 가장자리 사라짐
                float core = Mathf.SmoothStep(0.35f, 0f, r);        // 가운데(빨려 드는 곳) 진하게
                float a = Mathf.Clamp01(band + core) * edge * (r < 1f ? 1f : 0f);
                float lum = Mathf.SmoothStep(0.04f, 0.22f, r); // 가운데(빨려 드는 구멍)만 검게 — 넓히면 밝은 줄이 가장자리 몇 픽셀만 남는다
                tex.SetPixel(x, y, new Color(lum, lum, lum, a));
            }
        }
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);
        if (AssetImporter.GetAtPath(path) is TextureImporter imp)
        {
            imp.alphaIsTransparency = true;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.mipmapEnabled = true;
            imp.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // ───────────────────────── 튀는 물방울 ─────────────────────────

    /// <summary>
    /// 작은 핏방울이 수면에서 위로 넓게 튄다(속도 1.0~2.2m/s → 수면 위 5~25cm). 방출량은 <see cref="ToiletFlush"/>가 진행에 맞춰 조절한다.
    /// </summary>
    private static GameObject BuildSplash()
    {
        string dst = Root + "/PS_ToiletFlushSplash.prefab";
        var stage = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
        var go = ToiletWaterBuilder.Copy(ToiletWaterBuilder.BloodVfx + "PS_SplatterDirectional_02.prefab", stage);
        go.name = "PS_ToiletFlushSplash";
        try
        {
            var ps = go.GetComponent<ParticleSystem>();
            ToiletWaterBuilder.Configure(ps, duration: 1f, life: (0.35f, 0.7f), speed: (1.0f, 2.2f), size: (0.008f, 0.02f), gravity: 1f, maxParticles: 200);
            var main = ps.main;
            main.loop = true; // 방출량을 ToiletFlush가 0으로 내려 멈춘다

            var em = ps.emission;
            em.rateOverTime = 0f;
            em.SetBursts(new ParticleSystem.Burst[0]);

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 38f;
            shape.radius = 0.06f; // 실행 중 수면 크기에 맞춰 바뀜

            ToiletWaterBuilder.KillOnHit(ps, sendMessages: false);
            return PrefabUtility.SaveAsPrefabAsset(go, dst);
        }
        finally
        {
            Object.DestroyImmediate(go);
            UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(stage);
        }
    }
}

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// ToiletA로 변기 물 프리팹 두 개를 만든다(여러 번 눌러도 같은 결과로 덮어씀).
/// <list type="number">
/// <item><c>HorrorProp_ToiletA_ClearWater</c> — 맑은 물. 연출 없음.</item>
/// <item><c>HorrorProp_ToiletA_BloodHair</c> — 붉은 물 + 물에 뜬 머리카락,
///   0.8m 안으로 다가오면 물에서 핏물이 뿜어져 나옴(<see cref="ToiletBloodGush"/>).</item>
/// </list>
/// <para>
/// <b>수면</b>은 변기 안쪽 모양을 재서 만든 부채꼴 판이다(수면 높이에서 가운데부터 사방으로 나가며 그릇 면이 수면보다
/// 높아지는 곳까지, 그보다 조금 더 — 가장자리는 도자기 속에 묻힌다). ToiletA 프리팹의 충돌체는 단순화한 볼록 껍질이라
/// 그릇 모양이 없으므로 <b>렌더 메시(LOD0)로 잰다</b>.
/// </para>
/// <para>
/// <b>머리카락</b>은 Lee님 점검 이상 T-1(<c>InspectionAnomalies.HairTex</c>, 런타임 생성)과 같은 방식·같은 시드로
/// 텍스처를 구워 에셋으로 둔다. 원본 코드는 고치지 않고 참조하지도 않는다.
/// </para>
/// <para>
/// <b>뿜어짐</b>은 피 VFX 팩(<c>RealisticBloodVFX</c>)의 <c>PS_SplatterDirectional_01</c>(물줄기) · <c>_02</c>(굵은 방울) ·
/// <c>PS__SplatterOmni_01</c>(수면 튐)을 복사해 고치고, 굵은 방울에는 핏물 분수(<c>HorrorEvent_BloodyFountain</c>)의
/// 바닥 자국 스포너(<c>UniversalDecalSpawner</c>) 설정을 그대로 옮겨 떨어진 자리에 핏자국이 남게 한다.
/// </para>
/// 원본(ToiletA·피 VFX·핏물 분수·소리)은 고치지 않는다.
/// </summary>
public static class ToiletWaterBuilder
{
    private const string Root = "Assets/3.2 Programmer_Kim/03 Prefebs/04 Horror/Toilet";
    private const string ClearPath = Root + "/HorrorProp_ToiletA_ClearWater.prefab";
    private const string BloodPath = Root + "/HorrorProp_ToiletA_BloodHair.prefab";

    private const string ToiletPrefab = "Assets/NOT_Lonely/HQ_AbandonedSchool/Prefabs/ToiletA.prefab";
    private const string BloodVfx = "Assets/0. Main/99 Resources/VFX/RealisticBloodVFX/URP/RealisticBlood/Particle Systems/";
    private const string FountainPrefab = "Assets/3.2 Programmer_Kim/03 Prefebs/04 Horror/Bloody/HorrorEvent_BloodyFountain.prefab";
    private const string FountainDrops = "Near_DarkRed/PS_Drops_DarkRed";
    private const string GushSound = "Assets/_Game/Audio/toilet/SFX_TOILET_WaterMove_3.wav";

    /// <summary>
    /// 수면 높이(변기 바닥 기준, m). 실측: 테 0.39 · 그릇 바닥 0.19~0.20 · 가운데 배수 구멍.
    /// 0.25면 앞뒤 약 0.3m · 좌우 약 0.28m 웅덩이 — 실제 변기에 고인 물 정도.
    /// </summary>
    private const float WaterY = 0.25f;
    /// <summary>그릇에서 가장 깊은 곳(배수 구멍 위) — 부채꼴의 가운데.</summary>
    private static readonly Vector2 BowlCenterXZ = new Vector2(0f, 0.42f);
    /// <summary>수면 가장자리를 그릇 면 속으로 밀어 넣는 여유(m).</summary>
    private const float Overlap = 0.012f;
    private const int FanSegments = 48;

    private static readonly Color ClearWaterColor = new Color(0.62f, 0.70f, 0.68f, 0.30f);
    // 0.13이면 어두운 화장실에서 검정으로 읽히고 검은 머리카락이 묻혔다 — 붉은 기가 남도록
    private static readonly Color BloodWaterColor = new Color(0.36f, 0.02f, 0.015f, 1f);
    private static readonly Color HairColor = new Color(0.02f, 0.02f, 0.02f, 1f);

    [MenuItem("Tools/Programmer_Kim/Liquid/Build Toilet Water")]
    public static void Build()
    {
        EnsureFolder(Root);

        var toilet = AssetDatabase.LoadAssetAtPath<GameObject>(ToiletPrefab);
        if (toilet == null)
        {
            Debug.LogError($"[ToiletWaterBuilder] ToiletA가 없습니다: {ToiletPrefab}");
            return;
        }

        Mesh water = BuildWaterMesh(toilet);
        Material clear = ClearWaterMaterial();
        Material blood = BloodWaterMaterial();
        Material hair = HairMaterial();
        GameObject gush = BuildGush();

        BuildClear(toilet, water, clear);
        BuildBlood(toilet, water, blood, hair, gush);

        AssetDatabase.SaveAssets();
        Debug.Log($"[ToiletWaterBuilder] 완료 — {ClearPath} · {BloodPath}");
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(BloodPath);
    }

    // ───────────────────────── 프리팹 ─────────────────────────

    private static void BuildClear(GameObject toilet, Mesh water, Material mat)
    {
        Assemble(ClearPath, "HorrorProp_ToiletA_ClearWater", toilet, root =>
        {
            AddWater(root, water, mat);
        });
    }

    private static void BuildBlood(GameObject toilet, Mesh water, Material waterMat, Material hairMat, GameObject gushPrefab)
    {
        Assemble(BloodPath, "HorrorProp_ToiletA_BloodHair", toilet, root =>
        {
            AddWater(root, water, waterMat);
            UseBowlCollider(root);

            // 머리카락 — 물 위 두 겹(엇갈려 촘촘하게). 테 너머로 늘어뜨린 판은 불룩한 테를 뚫고 네모 윤곽이 보여 뺐다
            var hair = new GameObject("Hair").transform;
            hair.SetParent(root, false);
            Vector3 c = new Vector3(BowlCenterXZ.x, WaterY + 0.003f, BowlCenterXZ.y);
            // 수면 너비 약 0.28m — 가닥이 그릇 바깥 벽을 뚫지 않게 그보다 작게
            HairQuad(hair, "Hair_Float", c, Vector3.up, Vector3.forward, 0.25f, 0.27f, hairMat);
            HairQuad(hair, "Hair_Float2", c + new Vector3(0.02f, 0.002f, 0.05f), Vector3.up,
                     Quaternion.Euler(0f, 57f, 0f) * Vector3.forward, 0.18f, 0.18f, hairMat);

            // 뿜어짐
            var gushGo = (GameObject)PrefabUtility.InstantiatePrefab(gushPrefab, root);
            gushGo.name = "Gush";
            gushGo.transform.localPosition = c;
            gushGo.transform.localRotation = Quaternion.Euler(-80f, 0f, 0f); // 위로, 앞(플레이어 쪽)으로 10° 기울임

            var audioGo = new GameObject("AUD_Gush");
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

            var centerT = new GameObject("WaterCenter").transform;
            centerT.SetParent(root, false);
            centerT.localPosition = c;

            var trigger = root.gameObject.AddComponent<ToiletBloodGush>();
            var so = new SerializedObject(trigger);
            so.FindProperty("center").objectReferenceValue = centerT;
            so.FindProperty("gush").objectReferenceValue = gushGo.GetComponent<ParticleSystem>();
            so.FindProperty("gushSource").objectReferenceValue = src;
            so.FindProperty("gushClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(GushSound);
            so.ApplyModifiedPropertiesWithoutUndo();
        });
    }

    /// <summary>임시 프리뷰 씬에서 ToiletA를 중첩해 조립하고 저장한다(열린 씬을 건드리지 않음).</summary>
    private static void Assemble(string path, string name, GameObject toilet, System.Action<Transform> fill)
    {
        var stage = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
        var root = new GameObject(name);
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, stage);
        try
        {
            var model = (GameObject)PrefabUtility.InstantiatePrefab(toilet, root.transform);
            model.name = "ToiletA";
            fill(root.transform);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            Object.DestroyImmediate(root);
            UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(stage);
        }
    }

    /// <summary>
    /// ToiletA의 충돌체는 그릇까지 통째로 덮는 볼록 껍질(LOD2)이라, 수면에서 나온 핏방울이 태어나자마자
    /// 그 안에서 부딪혀 사라진다(실측: 0개). 이 프리팹에서만 껍질을 끄고 실제 모양(LOD0) 오목 충돌체로 바꾼다.
    /// 원본 프리팹은 그대로 — 중첩 인스턴스의 재정의다. 플레이어는 여전히 변기에 막힌다.
    /// </summary>
    private static void UseBowlCollider(Transform root)
    {
        Transform model = root.Find("ToiletA");
        MeshFilter lod0 = model != null ? model.GetComponentInChildren<MeshFilter>() : null;
        if (lod0 == null) return;

        foreach (var c in model.GetComponentsInChildren<Collider>(true)) c.enabled = false;
        var bowl = lod0.gameObject.AddComponent<MeshCollider>();
        bowl.sharedMesh = lod0.sharedMesh;
        bowl.convex = false;
    }

    private static void AddWater(Transform root, Mesh mesh, Material mat)
    {
        var go = new GameObject("Water");
        go.transform.SetParent(root, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    /// <summary>얼굴(앞면)이 <paramref name="normal"/>을 보는 Quad. Quad의 앞면은 로컬 -Z.</summary>
    private static void HairQuad(Transform parent, string name, Vector3 pos, Vector3 normal, Vector3 up, float w, float h, Material mat)
    {
        var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Object.DestroyImmediate(q.GetComponent<Collider>());
        q.name = name;
        q.transform.SetParent(parent, false);
        q.transform.localPosition = pos;
        q.transform.localRotation = Quaternion.LookRotation(-normal, up);
        q.transform.localScale = new Vector3(w, h, 1f);
        var r = q.GetComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    // ───────────────────────── 수면 메시 ─────────────────────────

    /// <summary>
    /// 렌더 메시(LOD0)에 임시 충돌체를 씌워, 수면 높이에서 가운데부터 사방으로 그릇 면이 수면 위로 올라오는 곳을 찾는다.
    /// </summary>
    private static Mesh BuildWaterMesh(GameObject toilet)
    {
        var stage = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
        var go = (GameObject)PrefabUtility.InstantiatePrefab(toilet, stage);
        try
        {
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) c.enabled = false;
            var lod0 = go.GetComponentInChildren<MeshFilter>(); // LOD0이 첫 번째
            var col = lod0.gameObject.AddComponent<MeshCollider>();
            col.sharedMesh = lod0.sharedMesh;
            Physics.SyncTransforms();

            var verts = new List<Vector3> { new Vector3(BowlCenterXZ.x, WaterY, BowlCenterXZ.y) };
            var uvs = new List<Vector2> { new Vector2(0.5f, 0.5f) };
            var tris = new List<int>();
            float maxR = 0.01f;

            for (int i = 0; i < FanSegments; i++)
            {
                float a = i * Mathf.PI * 2f / FanSegments;
                var dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)); // 0 = 앞(+Z)
                float r = EdgeRadius(col, dir);
                Vector3 p = new Vector3(BowlCenterXZ.x, WaterY, BowlCenterXZ.y) + dir * (r + Overlap);
                verts.Add(p);
                maxR = Mathf.Max(maxR, r + Overlap);
            }

            for (int i = 1; i <= FanSegments; i++)
            {
                Vector3 p = verts[i] - verts[0];
                uvs.Add(new Vector2(0.5f + p.x / (2f * maxR), 0.5f + p.z / (2f * maxR)));
                tris.Add(0);
                tris.Add(i);
                tris.Add(i % FanSegments + 1);
            }

            var mesh = new Mesh { name = "Mesh_ToiletWater" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            var normals = new Vector3[verts.Count];
            for (int i = 0; i < normals.Length; i++) normals[i] = Vector3.up;
            mesh.normals = normals;
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return SaveMesh(mesh, Root + "/Mesh_ToiletWater.asset");
        }
        finally
        {
            Object.DestroyImmediate(go);
            UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(stage);
        }
    }

    /// <summary>가운데에서 dir로 나가며 그릇 면(위에서 내려 쏜 첫 면)이 수면 높이 이상이 되는 거리.</summary>
    private static float EdgeRadius(MeshCollider col, Vector3 dir)
    {
        var c = new Vector3(BowlCenterXZ.x, 0f, BowlCenterXZ.y);
        for (float r = 0.02f; r < 0.4f; r += 0.003f)
        {
            Vector3 p = c + dir * r;
            if (!col.Raycast(new Ray(new Vector3(p.x, 1.5f, p.z), Vector3.down), out RaycastHit h, 2f)) return r;
            if (h.point.y >= WaterY) return r;
        }
        return 0.15f;
    }

    private static Mesh SaveMesh(Mesh mesh, string path)
    {
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }
        // 같은 에셋을 덮어써 GUID를 지킨다(이미 놓인 프리팹 참조가 깨지지 않게)
        existing.Clear();
        EditorUtility.CopySerialized(mesh, existing);
        Object.DestroyImmediate(mesh);
        EditorUtility.SetDirty(existing);
        return existing;
    }

    // ───────────────────────── 재질 ─────────────────────────

    private static Material LitMaterial(string path)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(mat, path);
        }
        return mat;
    }

    /// <summary>맑은 물 — 반투명이라 그릇 바닥·배수 구멍이 비친다. 매끈해서 빛이 맺힌다.</summary>
    private static Material ClearWaterMaterial()
    {
        Material m = LitMaterial(Root + "/M_ToiletWater_Clear.mat");
        m.SetColor("_BaseColor", ClearWaterColor);
        m.SetFloat("_Smoothness", 0.96f);
        m.SetFloat("_Metallic", 0f);
        m.SetFloat("_Surface", 1f); // 투명
        m.SetFloat("_Blend", 0f);   // 알파
        m.SetFloat("_ZWrite", 0f);
        UnityEditor.BaseShaderGUI.SetMaterialKeywords(m);
        EditorUtility.SetDirty(m);
        return m;
    }

    /// <summary>붉은 물 — 불투명한 검붉은 색, 매끈하게.</summary>
    private static Material BloodWaterMaterial()
    {
        Material m = LitMaterial(Root + "/M_ToiletWater_Blood.mat");
        m.SetColor("_BaseColor", BloodWaterColor);
        m.SetFloat("_Smoothness", 0.9f);
        m.SetFloat("_Metallic", 0f);
        m.SetFloat("_Surface", 0f);
        m.SetFloat("_ZWrite", 1f);
        UnityEditor.BaseShaderGUI.SetMaterialKeywords(m);
        EditorUtility.SetDirty(m);
        return m;
    }

    /// <summary>젖은 검은 머리카락 — Lee님 T-1과 같은 값(색 0.02, 매끈함 0.6, 알파 컷 0.5). 양면.</summary>
    private static Material HairMaterial()
    {
        Material m = LitMaterial(Root + "/M_Hair_Wet.mat");
        m.SetTexture("_BaseMap", HairTexture());
        m.SetColor("_BaseColor", HairColor);
        m.SetFloat("_Smoothness", 0.6f);
        m.SetFloat("_Metallic", 0f);
        m.SetFloat("_AlphaClip", 1f);
        m.SetFloat("_Cutoff", 0.5f);
        m.SetFloat("_Cull", 0f); // 양면 — 늘어진 가닥은 뒤에서도 보인다
        UnityEditor.BaseShaderGUI.SetMaterialKeywords(m);
        EditorUtility.SetDirty(m);
        return m;
    }

    /// <summary>
    /// 엉킨 검은 머리카락 — Lee님 <c>InspectionAnomalies.HairTex</c>와 같은 알고리즘·시드(4021):
    /// 256², 가닥 420개, 가닥마다 60~180걸음 동안 방향을 ±0.25rad씩 틀며 2px 굵기로 긋는다.
    /// </summary>
    private static Texture2D HairTexture()
    {
        string path = Root + "/T_Hair_Tangle.png";
        const int n = 256;
        var px = new Color32[n * n];
        var rng = new System.Random(4021);
        for (int s = 0; s < 420; s++)
        {
            float x = (float)(rng.NextDouble() * 0.7 + 0.15) * n;
            float y = (float)(rng.NextDouble() * 0.7 + 0.15) * n;
            float a = (float)(rng.NextDouble() * System.Math.PI * 2);
            int steps = 60 + rng.Next(120);
            for (int i = 0; i < steps; i++)
            {
                a += (float)(rng.NextDouble() - 0.5) * 0.5f;
                x += Mathf.Cos(a) * 0.9f;
                y += Mathf.Sin(a) * 0.9f;
                int ix = (int)x;
                int iy = (int)y;
                if (ix < 0 || iy < 0 || ix >= n || iy >= n) break;
                px[iy * n + ix] = new Color32(255, 255, 255, 255);
                if (ix + 1 < n) px[iy * n + ix + 1] = new Color32(255, 255, 255, 255);
            }
        }

        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        tex.SetPixels32(px);
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);

        if (AssetImporter.GetAtPath(path) is TextureImporter imp)
        {
            imp.alphaIsTransparency = true;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.mipmapEnabled = true;
            imp.mipMapsPreserveCoverage = true; // 멀리서 가닥이 사라지지 않게(알파 컷)
            imp.alphaTestReferenceValue = 0.5f;
            imp.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // ───────────────────────── 뿜어짐 ─────────────────────────

    /// <summary>
    /// 물줄기(루트) + 굵은 방울(자식, 떨어진 자리에 핏자국) + 수면 튐(자식). 로컬 +Z가 뿜는 방향.
    /// </summary>
    private static GameObject BuildGush()
    {
        string dst = Root + "/PS_ToiletBloodGush.prefab";
        var stage = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
        var root = Copy(BloodVfx + "PS_SplatterDirectional_01.prefab", stage);
        root.name = "PS_ToiletBloodGush";
        try
        {
            // 물줄기 — 1.2초 동안 세차게 솟구쳤다가 잦아든다
            var spray = root.GetComponent<ParticleSystem>();
            // 3.6m/s면 수면 위 0.6m(실측 0.85) — 서 있는 사람 허리 아래라 약했다. 4.2m/s ≈ 0.9m, 가슴 높이까지.
            Configure(spray, duration: 1.2f, life: (0.6f, 1.1f), speed: (2.8f, 4.2f), size: (0.015f, 0.035f), gravity: 1f, maxParticles: 400);
            var em = spray.emission;
            em.rateOverTime = new ParticleSystem.MinMaxCurve(160f, new AnimationCurve(
                new Keyframe(0f, 1f), new Keyframe(0.35f, 0.8f), new Keyframe(1f, 0f)));
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 30) });
            var shape = spray.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 14f;
            shape.radius = 0.03f;
            KillOnHit(spray, sendMessages: false);

            // 굵은 방울 — 멀리 떨어지고, 닿은 자리에 핏자국
            var drops = Copy(BloodVfx + "PS_SplatterDirectional_02.prefab", stage);
            drops.name = "Drops";
            drops.transform.SetParent(root.transform, false);
            var dps = drops.GetComponent<ParticleSystem>();
            Configure(dps, duration: 1.2f, life: (0.9f, 1.4f), speed: (2.4f, 3.4f), size: (0.03f, 0.05f), gravity: 1f, maxParticles: 40);
            var dem = dps.emission;
            dem.rateOverTime = 0f;
            dem.SetBursts(new[] { new ParticleSystem.Burst(0f, 6), new ParticleSystem.Burst(0.35f, 5), new ParticleSystem.Burst(0.7f, 3) });
            var dshape = dps.shape;
            dshape.enabled = true;
            dshape.shapeType = ParticleSystemShapeType.Cone;
            dshape.angle = 22f;
            dshape.radius = 0.03f;
            KillOnHit(dps, sendMessages: true);
            CopyDecalSpawner(drops);

            // 수면 튐 — 시작 순간 사방으로
            var pop = Copy(BloodVfx + "PS__SplatterOmni_01.prefab", stage);
            pop.name = "SurfacePop";
            pop.transform.SetParent(root.transform, false);
            var pps = pop.GetComponent<ParticleSystem>();
            Configure(pps, duration: 0.5f, life: (0.3f, 0.5f), speed: (1f, 2f), size: (0.015f, 0.03f), gravity: 2f, maxParticles: 40);
            var pem = pps.emission;
            pem.rateOverTime = 0f;
            pem.SetBursts(new[] { new ParticleSystem.Burst(0f, 24) });
            var pshape = pps.shape;
            pshape.radius = 0.02f; // 원본 0.2m — 수면 밖 그릇 벽 속에서 태어났다
            pshape.shapeType = ParticleSystemShapeType.Hemisphere; // 수면 위쪽으로만
            KillOnHit(pps, sendMessages: false);

            return PrefabUtility.SaveAsPrefabAsset(root, dst);
        }
        finally
        {
            Object.DestroyImmediate(root);
            UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(stage);
        }
    }

    /// <summary>원본 프리팹과 연결을 끊은 사본(원본 VFX를 고치지 않으려고).</summary>
    private static GameObject Copy(string prefabPath, UnityEngine.SceneManagement.Scene stage)
    {
        var src = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        // 임시 씬에 바로 만든다(열린 씬에 잠깐이라도 생기지 않게) → 원본 연결을 끊는다
        var go = (GameObject)PrefabUtility.InstantiatePrefab(src, stage);
        PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        return go;
    }

    private static void Configure(ParticleSystem ps, float duration, (float, float) life, (float, float) speed, (float, float) size, float gravity, int maxParticles)
    {
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var m = ps.main;
        m.duration = duration;
        m.loop = false;
        m.playOnAwake = false;
        m.startLifetime = new ParticleSystem.MinMaxCurve(life.Item1, life.Item2);
        m.startSpeed = new ParticleSystem.MinMaxCurve(speed.Item1, speed.Item2);
        m.startSize = new ParticleSystem.MinMaxCurve(size.Item1, size.Item2);
        m.gravityModifier = gravity;
        m.simulationSpace = ParticleSystemSimulationSpace.World;
        m.maxParticles = maxParticles;

        // 원본 「Directional」은 옆(+X)으로 5m/s 밀고 끌림 2로 붙잡는다 — 그대로 두면 그릇 벽에 박혀 사라진다(실측 74 → 6개).
        // 뿜는 방향은 이 오브젝트의 +Z(모양: 원뿔)가 정한다.
        var vol = ps.velocityOverLifetime;
        vol.enabled = false;
        var limit = ps.limitVelocityOverLifetime;
        limit.enabled = false;
    }

    /// <summary>세상에 닿으면 사라진다(튕겨 다니지 않게).</summary>
    private static void KillOnHit(ParticleSystem ps, bool sendMessages)
    {
        var c = ps.collision;
        c.enabled = true;
        c.type = ParticleSystemCollisionType.World;
        c.mode = ParticleSystemCollisionMode.Collision3D;
        c.lifetimeLoss = 1f;
        c.bounce = 0f;
        c.sendCollisionMessages = sendMessages;
    }

    /// <summary>핏물 분수의 바닥 자국 스포너를 설정째로 옮긴다(자국 프리팹 4종 · 튐 VFX · 크기 · 수명).</summary>
    private static void CopyDecalSpawner(GameObject target)
    {
        var fountain = AssetDatabase.LoadAssetAtPath<GameObject>(FountainPrefab);
        Transform src = fountain != null ? fountain.transform.Find(FountainDrops) : null;
        var from = src != null ? src.GetComponent<UniversalDecalSpawner>() : null;
        if (from == null)
        {
            Debug.LogWarning($"[ToiletWaterBuilder] 핏자국 스포너를 찾지 못했습니다({FountainPrefab} / {FountainDrops}) — 자국 없이 만듭니다.");
            return;
        }

        var to = target.GetComponent<UniversalDecalSpawner>();
        if (to == null) to = target.AddComponent<UniversalDecalSpawner>();
        EditorUtility.CopySerialized(from, to);
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}

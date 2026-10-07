using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 칠판 지도(MapBoard.prefab)를 「낡은 종이 지도」로 만든다.
///
/// <para><b>왜</b> — 지도가 World Space Canvas의 UI Image(기본 UI 머티리얼)라 조명을 받지 않았다.
/// 어두운 교실에서도 원본 밝기 그대로 떠 보였다. 그래서 같은 자리·크기의 Quad + URP Lit(반사 없음)로 바꿔
/// 교실 밝기와 손전등에 따라 보이게 하고, 원본 그림은 그대로 둔 채 낡게 칠한 판(<see cref="AgedPath"/>)을 새로 굽는다.</para>
///
/// <para><b>배경</b> — 칠판 면(테두리 안쪽) 크기만 한 낡은 종이 판(<see cref="BackingPath"/>)을 지도 뒤에 깐다.
/// 지도와 같은 낡힘(얼룩·그을림·접힌 자국·긁힘·잡티)을 지도 가장자리의 그을린 세피아 톤 위에 입히고,
/// 지도가 붙은 자리 둘레에 옅은 그림자를 넣어 붙여 둔 종이처럼 보이게 한다. 칠판 몸통 머티리얼은 벤더 원본 그대로.</para>
///
/// <para>메뉴 「Tools ▸ Programmer_Kim ▸ Map Board ▸ Build Aged Map」 — 여러 번 눌러도 같은 결과(고정 시드).
/// 낡은 정도·배경 톤은 아래 상수로 조정한다.</para>
/// </summary>
public static class MapBoardAgingBuilder
{
    private const string PrefabPath = "Assets/3.2 Programmer_Kim/03 Prefebs/MapBoard.prefab";
    private const string SourcePath = "Assets/3.2 Programmer_Kim/99 Resources/02 Image/Map_KR.png";
    private const string AgedPath = "Assets/3.2 Programmer_Kim/99 Resources/02 Image/Map_KR_Aged.png";
    private const string BackingPath = "Assets/3.2 Programmer_Kim/99 Resources/02 Image/MapBoard_Backing.png";
    private const string MaterialPath = "Assets/3.2 Programmer_Kim/04 Materials/MapBoard_Aged.mat";
    private const string BackingMaterialPath = "Assets/3.2 Programmer_Kim/04 Materials/MapBoard_Backing.mat";
    private const string BoardMaterialPath = "Assets/NOT_Lonely/HQ_AbandonedSchool/Models/Materials/Blackboard_mtl.mat";   // 칠판 몸통 = 벤더 원본
    private const string OldTintMaterialPath = "Assets/3.2 Programmer_Kim/04 Materials/MapBoard_Board.mat";              // 폐기한 갈색 틴트판
    private const string PaperName = "MapPaper";
    private const string BackingName = "MapBacking";

    // ── 칠판 면(테두리 안쪽) — BlackboardSmall 메시 실측: 깊이 z 0.010, x ±0.96, y −0.56 ~ +0.61 ──
    private const float SlateLeft = -0.96f, SlateRight = 0.96f, SlateBottom = -0.56f, SlateTop = 0.61f;
    private const float SlateInset = 0.002f;    // 테두리 안쪽 면과 맞닿아 깜빡이지 않게
    private const float BackingZ = 0.014f;      // 칠판 면(0.010)과 지도(0.019) 사이
    private const int BackingWidth = 2048;      // 높이는 칠판 면 비율로

    // ── 낡은 정도(0~1) ──
    private const int Seed = 1987;
    private const float Brightness = 0.62f;     // 전체 밝기 — 원본이 회백색이라 어둡게 누른다
    private const float Desaturate = 0.45f;     // 색 빠짐
    private const float Yellowing = 0.75f;      // 종이 누렇게 바램(밝은 곳일수록)
    private const float StainAmount = 0.55f;    // 물·곰팡이 얼룩
    private const float EdgeBurn = 0.65f;       // 가장자리 그을림·때
    private const float FoldDarken = 0.35f;     // 접힌 자국
    private const int ScratchCount = 28;        // 긁힘
    private const float Grain = 0.045f;         // 잡티

    private static readonly Color PaperTint = new Color(1.00f, 0.88f, 0.66f);   // 누런 종이
    private static readonly Color StainTint = new Color(0.62f, 0.48f, 0.30f);   // 갈색 얼룩
    private static readonly Color BurnTint = new Color(0.22f, 0.15f, 0.09f);    // 그을린 가장자리

    // ── 배경 판 톤 — 지도 가장자리(그을린 세피아, 평균 0.15·0.10·0.06)와 지도 전체 평균(0.26·0.22·0.18) 사이 ──
    private static readonly Color BackingDark = new Color(0.15f, 0.11f, 0.07f);
    private static readonly Color BackingLight = new Color(0.30f, 0.24f, 0.17f);
    private const float BackingStain = 0.45f;
    private const float BackingEdgeBurn = 0.7f;
    private const float MapShadow = 0.45f;      // 지도 둘레 그림자(붙여 둔 종이)

    [MenuItem("Tools/Programmer_Kim/Map Board/Build Aged Map")]
    public static void BuildMenu()
    {
        Debug.Log(Build());
    }

    /// <summary>낡은 지도·배경 그림 → 머티리얼 → 프리팹 순서로 만든다. 결과 요약을 돌려준다.</summary>
    public static string Build()
    {
        Texture2D aged = BakeAgedTexture();
        if (aged == null) return "[MapBoardAgingBuilder] 원본 지도를 읽지 못했습니다: " + SourcePath;
        Material map = BuildLitMaterial(MaterialPath, aged);
        Material backing = BuildLitMaterial(BackingMaterialPath, BakeBackingTexture());
        string report = ApplyToPrefab(map, backing);

        // 앞서 만든 갈색 틴트판은 쓰지 않는다
        if (AssetDatabase.LoadAssetAtPath<Material>(OldTintMaterialPath) != null)
        {
            AssetDatabase.DeleteAsset(OldTintMaterialPath);
            report += " · 갈색 틴트 머티리얼 삭제";
        }
        return "[MapBoardAgingBuilder] " + AgedPath + " · " + BackingPath + " · " + report;
    }

    // ─────────────────────────────── 지도 ───────────────────────────────

    private static Texture2D BakeAgedTexture()
    {
        // 원본 가져오기 설정(읽기 불가)을 건드리지 않도록 파일을 직접 읽는다.
        if (!File.Exists(SourcePath)) return null;
        var src = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
        if (!src.LoadImage(File.ReadAllBytes(SourcePath))) return null;

        int w = src.width, h = src.height;
        Color[] px = src.GetPixels();
        Object.DestroyImmediate(src);

        var rng = new System.Random(Seed);
        var wear = Wear.Roll(rng, w, h, 1f);

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x;
                Color c = px[i];
                float lum = c.r * 0.299f + c.g * 0.587f + c.b * 0.114f;

                // ① 색 빠짐 + 누렇게 바램(밝은 종이 부분일수록 더)
                c = Color.Lerp(c, new Color(lum, lum, lum, c.a), Desaturate);
                Color yellow = new Color(c.r * PaperTint.r, c.g * PaperTint.g, c.b * PaperTint.b, c.a);
                c = Color.Lerp(c, yellow, Yellowing * Mathf.Lerp(0.5f, 1f, lum));

                // ② 전체를 누르고 검은 곳은 살짝 들어 바랜 잉크처럼
                c = new Color(c.r * Brightness + 0.035f, c.g * Brightness + 0.028f, c.b * Brightness + 0.018f, c.a);

                // ③~⑥ 얼룩 · 가장자리 그을림 · 접힌 자국 · 잡티
                px[i] = wear.Apply(c, x, y, StainAmount, EdgeBurn);
            }
        }
        wear.Scratch(px, rng, ScratchCount);   // ⑦ 긁힘

        return SaveTexture(px, w, h, AgedPath);
    }

    // ─────────────────────────────── 배경 판 ───────────────────────────────

    /// <summary>칠판 면 크기의 낡은 세피아 종이. 지도가 붙은 자리 둘레에는 옅은 그림자.</summary>
    private static Texture2D BakeBackingTexture()
    {
        float slateW = SlateRight - SlateLeft, slateH = SlateTop - SlateBottom;
        int w = BackingWidth;
        int h = Mathf.RoundToInt(w * slateH / slateW);
        float pxPerMeter = w / slateW;

        // 지도 크기를 받아 같은 물리 크기의 얼룩·긁힘이 되게(지도: 1208px / 1.812m)
        float scale = pxPerMeter / (1208f / 1.812f);
        var rng = new System.Random(Seed + 1);
        var wear = Wear.Roll(rng, w, h, scale);
        float ox = (float)rng.NextDouble() * 900f, oy = (float)rng.NextDouble() * 900f;

        // 지도가 붙는 자리(칠판 면 기준 픽셀) — 프리팹의 MapPaper 실측: x ±0.906, y −0.424 ~ +0.396
        Rect map = Rect.MinMaxRect((-0.906f - SlateLeft) * pxPerMeter, (-0.424f - SlateBottom) * pxPerMeter,
                                   (0.906f - SlateLeft) * pxPerMeter, (0.396f - SlateBottom) * pxPerMeter);
        float shadowSoft = 0.018f * pxPerMeter;                            // 1.8cm 번짐
        Vector2 shadowOffset = new Vector2(0.004f, -0.006f) * pxPerMeter;  // 빛이 위에서 — 그림자는 살짝 아래로

        var px = new Color[w * h];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                // 바탕: 얼룩덜룩한 세피아 + 종이 결
                float mottle = Fbm((x + ox) / (260f * scale), (y + oy) / (260f * scale));
                float fiber = Mathf.PerlinNoise((x + ox) / (3f * scale), (y + oy) / (40f * scale)) - 0.5f;
                Color c = Color.Lerp(BackingDark, BackingLight, Mathf.SmoothStep(0f, 1f, mottle));
                c = new Color(c.r + fiber * 0.025f, c.g + fiber * 0.02f, c.b + fiber * 0.015f, 1f);

                // 지도 둘레 그림자 — 사각형까지 거리로 부드럽게
                float dx = Mathf.Max(map.xMin - (x - shadowOffset.x), 0f, (x - shadowOffset.x) - map.xMax);
                float dy = Mathf.Max(map.yMin - (y - shadowOffset.y), 0f, (y - shadowOffset.y) - map.yMax);
                float shadow = Mathf.Exp(-Mathf.Pow(Mathf.Sqrt(dx * dx + dy * dy) / shadowSoft, 2f)) * MapShadow;
                c = new Color(c.r * (1f - shadow), c.g * (1f - shadow), c.b * (1f - shadow), 1f);

                px[y * w + x] = wear.Apply(c, x, y, BackingStain, BackingEdgeBurn);
            }
        }
        wear.Scratch(px, rng, Mathf.RoundToInt(ScratchCount * 1.4f));

        return SaveTexture(px, w, h, BackingPath);
    }

    // ─────────────────────────────── 낡힘(지도·배경 공용) ───────────────────────────────

    /// <summary>
    /// 얼룩·둥근 자국·가장자리 그을림·접힌 자국·잡티·긁힘. 무작위 값은 <see cref="Roll"/>에서 한 번에 뽑아
    /// 같은 시드면 늘 같은 그림이 된다. <c>scale</c>은 픽셀 밀도 비(지도 = 1) — 같은 물리 크기의 자국이 되게.
    /// </summary>
    private sealed class Wear
    {
        private int w, h;
        private float scale, ox, oy, foldX, foldY;
        private Vector2[] ringC;
        private float[] ringR;

        public static Wear Roll(System.Random rng, int w, int h, float scale)
        {
            var wear = new Wear { w = w, h = h, scale = scale };
            wear.ox = (float)rng.NextDouble() * 500f;
            wear.oy = (float)rng.NextDouble() * 500f;

            // 커피 잔 자국 같은 둥근 얼룩 몇 개(가장자리가 진함)
            const int rings = 3;
            wear.ringC = new Vector2[rings];
            wear.ringR = new float[rings];
            for (int i = 0; i < rings; i++)
            {
                wear.ringC[i] = new Vector2((float)rng.NextDouble() * w, (float)rng.NextDouble() * h);
                wear.ringR[i] = Mathf.Lerp(0.06f, 0.13f, (float)rng.NextDouble()) * h;
            }

            // 접힌 자국: 세로 하나·가로 하나(가운데 근처)
            wear.foldX = w * Mathf.Lerp(0.46f, 0.54f, (float)rng.NextDouble());
            wear.foldY = h * Mathf.Lerp(0.44f, 0.56f, (float)rng.NextDouble());
            return wear;
        }

        public Color Apply(Color c, int x, int y, float stainAmount, float edgeBurn)
        {
            float u = x / (float)w, v = y / (float)h;

            // 얼룩(낮은 주파수 노이즈 + 둥근 자국)
            float n = Fbm((x + ox) / (170f * scale), (y + oy) / (170f * scale));
            float stain = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.52f, 0.78f, n)) * stainAmount;
            for (int r = 0; r < ringC.Length; r++)
            {
                float d = Vector2.Distance(new Vector2(x, y), ringC[r]) / ringR[r];
                float rim = Mathf.Exp(-Mathf.Pow((d - 1f) / 0.06f, 2f)) * 0.5f + (d < 1f ? 0.12f : 0f);
                stain = Mathf.Max(stain, rim * stainAmount);
            }
            c = Color.Lerp(c, new Color(c.r * StainTint.r, c.g * StainTint.g, c.b * StainTint.b, c.a), stain);

            // 가장자리 그을림 — 가장자리까지 거리에 노이즈를 섞어 울퉁불퉁하게
            float edge = Mathf.Min(Mathf.Min(u, 1f - u) * w / h, Mathf.Min(v, 1f - v));   // 짧은 변 기준 0~0.5
            float wobble = (Fbm((x + ox) / (40f * scale), (y + oy) / (40f * scale)) - 0.5f) * 0.06f;
            float burn = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.16f, edge + wobble));
            c = Color.Lerp(c, new Color(BurnTint.r, BurnTint.g, BurnTint.b, c.a), burn * edgeBurn);

            // 접힌 자국: 어두운 골 + 한쪽 옅은 밝음
            float fw = 2.2f * scale, fo = 4f * scale, fs = 3f * scale;
            float fold = 0f;
            fold += Mathf.Exp(-Mathf.Pow((x - foldX) / fw, 2f)) - 0.35f * Mathf.Exp(-Mathf.Pow((x - foldX - fo) / fs, 2f));
            fold += Mathf.Exp(-Mathf.Pow((y - foldY) / fw, 2f)) - 0.35f * Mathf.Exp(-Mathf.Pow((y - foldY + fo) / fs, 2f));
            float shade = 1f - fold * FoldDarken;
            c = new Color(c.r * shade, c.g * shade, c.b * shade, c.a);

            // 잡티
            float g = (Hash(x, y) - 0.5f) * 2f * Grain;
            return new Color(Mathf.Clamp01(c.r + g), Mathf.Clamp01(c.g + g * 0.95f), Mathf.Clamp01(c.b + g * 0.85f), c.a);
        }

        /// <summary>가는 선(대부분 밝게 벗겨짐, 일부 어둡게).</summary>
        public void Scratch(Color[] px, System.Random rng, int count)
        {
            for (int s = 0; s < count; s++)
            {
                float x0 = (float)rng.NextDouble() * w, y0 = (float)rng.NextDouble() * h;
                float ang = (float)rng.NextDouble() * Mathf.PI;
                float len = Mathf.Lerp(25f, 140f, (float)rng.NextDouble()) * scale;
                bool light = rng.NextDouble() < 0.7;
                float strength = Mathf.Lerp(0.12f, 0.3f, (float)rng.NextDouble());
                for (float t = 0f; t < len; t += 0.5f)
                {
                    int x = Mathf.RoundToInt(x0 + Mathf.Cos(ang) * t + Mathf.Sin(t * 0.08f / scale) * 2f * scale);
                    int y = Mathf.RoundToInt(y0 + Mathf.Sin(ang) * t);
                    if (x < 0 || y < 0 || x >= w || y >= h) continue;
                    int i = y * w + x;
                    float k = strength * Mathf.Sin(t / len * Mathf.PI);   // 끝은 흐리게
                    Color target = light ? new Color(0.72f, 0.66f, 0.55f, px[i].a) : new Color(0.08f, 0.06f, 0.04f, px[i].a);
                    px[i] = Color.Lerp(px[i], target, k);
                }
            }
        }
    }

    private static float Fbm(float x, float y)
    {
        float sum = 0f, amp = 0.5f, freq = 1f, norm = 0f;
        for (int o = 0; o < 4; o++)
        {
            sum += Mathf.PerlinNoise(x * freq, y * freq) * amp;
            norm += amp;
            amp *= 0.5f;
            freq *= 2.1f;
        }
        return sum / norm;
    }

    private static float Hash(int x, int y)
    {
        unchecked
        {
            uint h = (uint)(x * 374761393 + y * 668265263 + Seed * 1442695041);
            h = (h ^ (h >> 13)) * 1274126177u;
            return (h ^ (h >> 16)) / (float)uint.MaxValue;
        }
    }

    private static Texture2D SaveTexture(Color[] px, int w, int h, string path)
    {
        var outTex = new Texture2D(w, h, TextureFormat.RGBA32, false, false);
        outTex.SetPixels(px);
        outTex.Apply();
        File.WriteAllBytes(path, outTex.EncodeToPNG());
        Object.DestroyImmediate(outTex);

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var ti = (TextureImporter)AssetImporter.GetAtPath(path);
        ti.textureType = TextureImporterType.Default;
        ti.sRGBTexture = true;
        ti.alphaIsTransparency = false;
        ti.mipmapEnabled = true;
        ti.wrapMode = TextureWrapMode.Clamp;
        ti.anisoLevel = 4;
        ti.maxTextureSize = 2048;
        ti.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // ─────────────────────────────── 머티리얼 ───────────────────────────────

    /// <summary>바랜 종이 — URP Lit, 거의 반사 없음.</summary>
    private static Material BuildLitMaterial(string path, Texture2D tex)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.SetTexture("_BaseMap", tex);
        mat.SetTexture("_MainTex", tex);
        mat.SetColor("_BaseColor", Color.white);
        mat.SetFloat("_Metallic", 0f);
        mat.SetFloat("_Smoothness", 0.08f);
        mat.SetFloat("_SpecularHighlights", 0f);
        mat.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
        mat.SetFloat("_EnvironmentReflections", 0f);
        mat.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();
        return mat;
    }

    // ─────────────────────────────── 프리팹 ───────────────────────────────

    /// <summary>
    /// 캔버스 UI Image를 같은 자리·크기의 조명 받는 Quad(MapPaper)로 바꾸고, 그 뒤에 칠판 면 크기의 배경(MapBacking)을 깐다.
    /// 칠판 몸통은 벤더 원본 머티리얼로 둔다. 이미 바꿨으면 머티리얼만 다시 끼운다.
    /// </summary>
    private static string ApplyToPrefab(Material map, Material backing)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var notes = new System.Collections.Generic.List<string>();
            Transform paper = root.transform.Find(PaperName);
            if (paper == null)
            {
                var image = root.GetComponentInChildren<UnityEngine.UI.Image>(true);
                if (image == null) return "프리팹에 지도 Image가 없습니다";

                // Image의 네 모서리로 자리·크기를 그대로 옮긴다
                var rt = (RectTransform)image.transform;
                var corners = new Vector3[4];
                rt.GetWorldCorners(corners);
                paper = NewQuad(root.transform, PaperName);
                paper.SetPositionAndRotation((corners[0] + corners[2]) * 0.5f, rt.rotation);   // Quad도 UI처럼 −Z 쪽에서 보인다
                Vector3 ps = root.transform.lossyScale;
                paper.localScale = new Vector3(Vector3.Distance(corners[0], corners[3]) / ps.x, Vector3.Distance(corners[0], corners[1]) / ps.y, 1f);

                // 옛 UI 지도 캔버스는 지운다(조명을 받지 않아 어두운 방에서 떠 보였다)
                Canvas canvas = image.GetComponentInParent<Canvas>();
                Object.DestroyImmediate(canvas != null ? canvas.gameObject : image.gameObject);
                notes.Add("UI 지도 → MapPaper 교체");
            }
            SetRenderer(paper, map);

            // 칠판 면 크기의 배경 — 지도와 같은 방향
            Transform back = root.transform.Find(BackingName);
            if (back == null)
            {
                back = NewQuad(root.transform, BackingName);
                notes.Add("MapBacking 추가");
            }
            back.localRotation = paper.localRotation;
            back.localPosition = new Vector3((SlateLeft + SlateRight) * 0.5f, (SlateBottom + SlateTop) * 0.5f, BackingZ);
            back.localScale = new Vector3(SlateRight - SlateLeft - SlateInset * 2f, SlateTop - SlateBottom - SlateInset * 2f, 1f);
            SetRenderer(back, backing);

            // 칠판 몸통은 벤더 원본으로(앞서 입힌 갈색 틴트 되돌림)
            var boardRenderer = root.GetComponent<MeshRenderer>();
            var vendor = AssetDatabase.LoadAssetAtPath<Material>(BoardMaterialPath);
            if (boardRenderer != null && vendor != null && boardRenderer.sharedMaterial != vendor)
            {
                boardRenderer.sharedMaterial = vendor;
                notes.Add("칠판 몸통 원본 머티리얼");
            }

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            return notes.Count > 0 ? string.Join(" · ", notes) : "머티리얼 갱신";
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static Transform NewQuad(Transform parent, string name)
    {
        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = name;
        Object.DestroyImmediate(quad.GetComponent<Collider>());   // 칠판 몸통에 콜라이더가 있다
        quad.transform.SetParent(parent, false);
        return quad.transform;
    }

    private static void SetRenderer(Transform t, Material mat)
    {
        var mr = t.GetComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.receiveShadows = true;
    }
}

using NightDuty;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 대역 만들기(최종 기획서 「대역 규약」, 2026-10-01 7단계). <c>Resources/StandIns/&lt;ID&gt;</c> 프리팹이 있으면 그것을, 없으면 같은 치수의 어두운 대역을 만든다.
/// 코드는 대역과 완성 자산을 구분하지 않는다 — 아트는 프리팹만 넣으면 된다.
/// <list type="bullet">
/// <item>사람형은 캡슐 + 어두운 무광(알베도 0.15) + 얼굴 쿼드. 키: 소년 1.35m · 모형 1.7m · 정장 남자 1.85m.</item>
/// <item>피벗은 발바닥 중앙, 앞은 +Z(플레이어를 본다).</item>
/// <item>응시 판정이 필요한 대역(천장 다리 C2 · 창밖 남자 L5)만 단단한 콜라이더를 갖는다. 나머지는 콜라이더를 빼서 길을 막지 않는다.</item>
/// <item>판정 기준점(<c>rule.L3.face</c> 등)은 루트의 <see cref="JudgeTarget"/>다(머리 높이).</item>
/// </list>
/// </summary>
public static class StandInFactory
{
    /// <summary>프리팹을 찾는 Resources 폴더.</summary>
    public const string ResourceFolder = "StandIns/";

    private static Material s_dark;
    private static Material s_face;
    private static Material s_yellow;

    /// <summary>
    /// 대역을 만든다. <paramref name="floorPoint"/>는 발 위치(천장 다리는 천장 점), <paramref name="lookAt"/>는 바라볼 점.
    /// <paramref name="anchorId"/>가 있으면 그 ID의 <see cref="JudgeTarget"/>를 붙인다.
    /// </summary>
    public static GameObject Create(string id, Vector3 floorPoint, Vector3 lookAt, string anchorId)
    {
        GameObject prefab = Resources.Load<GameObject>(ResourceFolder + id);
        GameObject go;
        if (prefab != null)
        {
            go = Object.Instantiate(prefab, floorPoint, Quaternion.identity);
            go.name = id;
        }
        else
        {
            go = Build(id);
            go.transform.position = floorPoint;
        }

        Vector3 dir = lookAt - floorPoint;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.01f) go.transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);

        if (!string.IsNullOrEmpty(anchorId))
        {
            JudgeTarget target = go.GetComponent<JudgeTarget>();
            if (target == null) target = go.AddComponent<JudgeTarget>();
            target.SetIds(anchorId);
        }

        return go;
    }

    /// <summary>대역에 단단한 응시 콜라이더가 필요한지(C2 천장 다리 · L5 창밖 남자).</summary>
    public static bool NeedsGazeCollider(string id)
    {
        return id == "mob.legs" || id == "mob.glitchman";
    }

    private static GameObject Build(string id)
    {
        GameObject root = new GameObject(id + " (대역)");
        switch (id)
        {
            case "mob.boy": Humanoid(root, 1.35f, 0.18f); break;
            case "mob.girl": Humanoid(root, 1.3f, 0.17f); break;
            case "mob.dummy": Humanoid(root, 1.7f, 0.22f); break;
            case "mob.meatman": Humanoid(root, 1.8f, 0.26f); break;
            case "mob.glitchman": Humanoid(root, 1.85f, 0.22f); break;
            case "mob.duck": Duck(root); break;
            case "mob.tree": Tree(root); break;
            case "mob.legs": Legs(root); break;
            case "prop.phantomdoor": Door(root); break;
            default: Humanoid(root, 1.7f, 0.22f); break;
        }

        if (NeedsGazeCollider(id))
        {
            Bounds b = RendererBounds(root);
            BoxCollider box = root.AddComponent<BoxCollider>();
            box.center = root.transform.InverseTransformPoint(b.center);
            box.size = b.size;
        }

        return root;
    }

    private static void Humanoid(GameObject root, float height, float radius)
    {
        GameObject body = Part(root, PrimitiveType.Capsule, Dark());
        body.transform.localScale = new Vector3(radius * 2f, (height - 0.2f) * 0.5f, radius * 2f);
        body.transform.localPosition = new Vector3(0f, (height - 0.2f) * 0.5f, 0f);

        GameObject head = Part(root, PrimitiveType.Sphere, Dark());
        head.transform.localScale = Vector3.one * 0.24f;
        head.transform.localPosition = new Vector3(0f, height - 0.12f, 0f);

        GameObject face = Part(root, PrimitiveType.Quad, Face());
        face.transform.localScale = new Vector3(0.14f, 0.18f, 1f);
        face.transform.localPosition = new Vector3(0f, height - 0.12f, 0.121f);
        face.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
    }

    private static void Duck(GameObject root)
    {
        GameObject face = Part(root, PrimitiveType.Sphere, Yellow());
        face.transform.localScale = new Vector3(0.45f, 0.5f, 0.3f);
        face.transform.localPosition = new Vector3(0f, 1.55f, 0f);
    }

    private static void Tree(GameObject root)
    {
        GameObject trunk = Part(root, PrimitiveType.Cylinder, Dark());
        trunk.transform.localScale = new Vector3(0.35f, 1.4f, 0.35f);
        trunk.transform.localPosition = new Vector3(0f, 1.4f, 0f);
        for (int i = 0; i < 4; i++)
        {
            GameObject branch = Part(root, PrimitiveType.Capsule, Dark());
            branch.transform.localScale = new Vector3(0.09f, 0.55f, 0.09f);
            branch.transform.localPosition = new Vector3(0f, 1.9f + i * 0.2f, 0f);
            branch.transform.localRotation = Quaternion.Euler(55f, i * 90f + 20f, 0f);
        }
    }

    private static void Legs(GameObject root)
    {
        for (int i = -1; i <= 1; i += 2)
        {
            GameObject leg = Part(root, PrimitiveType.Capsule, Dark());
            leg.transform.localScale = new Vector3(0.12f, 0.6f, 0.12f);
            leg.transform.localPosition = new Vector3(i * 0.1f, -0.6f, 0f);
        }
    }

    private static void Door(GameObject root)
    {
        GameObject slab = Part(root, PrimitiveType.Cube, Dark());
        slab.transform.localScale = new Vector3(1f, 2.1f, 0.08f);
        slab.transform.localPosition = new Vector3(0f, 1.05f, 0f);
    }

    private static GameObject Part(GameObject root, PrimitiveType type, Material mat)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        Collider c = go.GetComponent<Collider>();
        if (c != null) Object.Destroy(c);
        go.transform.SetParent(root.transform, false);
        Renderer r = go.GetComponent<Renderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = ShadowCastingMode.On;
        return go;
    }

    private static Bounds RendererBounds(GameObject root)
    {
        Renderer[] rs = root.GetComponentsInChildren<Renderer>();
        Bounds b = rs.Length > 0 ? rs[0].bounds : new Bounds(root.transform.position, Vector3.one);
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        return b;
    }

    private static Material Dark()
    {
        if (s_dark == null) s_dark = Make(new Color(0.15f, 0.14f, 0.14f), 0.05f);
        return s_dark;
    }

    private static Material Face()
    {
        if (s_face == null) s_face = Make(new Color(0.55f, 0.52f, 0.48f), 0.1f);
        return s_face;
    }

    private static Material Yellow()
    {
        if (s_yellow == null) s_yellow = Make(new Color(0.75f, 0.62f, 0.12f), 0.2f);
        return s_yellow;
    }

    private static Material Make(Color c, float smooth)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        Material m = new Material(shader);
        m.name = "StandIn";
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
        return m;
    }
}

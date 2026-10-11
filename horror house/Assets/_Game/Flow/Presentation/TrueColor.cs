using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 81차(민: 「네 몹 놀람 연출과 사망 연출에서 라이팅이 적용돼서 너무 하얗게 비춰져. 원본 텍스처 색상이 나오도록」).
/// 화면을 꽉 채우는 가까운 몹(놀람 얼굴 · 붙잡힘 얼굴 · 김진선님 사망 컷신의 몹)을 빛과 무관하게 <b>원본 텍스처 색 그대로</b> 그린다 —
/// 그 인스턴스의 재질만 URP Unlit 복사본(같은 기본 맵·색·타일링, 알파 컷·반투명 설정 따라감)으로 바꾼다. 에셋 재질은 고치지 않는다.
/// 0.3m 앞 손전등·점광에 하얗게 날아가던 것을 막는다(장면의 다른 것은 그대로 빛을 받는다).
/// </summary>
public static class TrueColor
{
    private const string UnlitShader = "Universal Render Pipeline/Unlit";

    /// <summary>
    /// 94차(민: 「놀람 연출에 나오는 모델들이 너무 밝아」): 빛을 받지 않는 원본 색은 실내 노출 보정(+EV)을 그대로 받아 몇 배로 밝아졌다 —
    /// 노출만큼 미리 덜어 낸다(2^−EV). 그 위에 이 배율(1 = 노출 뒤 원본 색 그대로, 작을수록 어둡게 — 어둠 속 몹답게 조금 어둡게).
    /// </summary>
    public const float Brightness = 0.8f;

    /// <summary>지금 화면 노출(색 조정 Post Exposure, EV)을 덜어 낸 배율 × <see cref="Brightness"/>.</summary>
    public static float Gain()
    {
        return Mathf.Clamp(Mathf.Pow(2f, -ExposureAt(Camera.main != null ? Camera.main.transform.position : Vector3.zero)), 0.05f, 1f) * Brightness;
    }

    /// <summary>
    /// 그 자리에 걸리는 노출(EV) — 색 조정의 Post Exposure를 덮어쓰는 볼륨 중 그 자리에 닿는 것(전역, 또는 콜라이더 안) 가운데 우선순위가 가장 높은 것.
    /// (VolumeManager의 스택은 카메라를 그릴 때만 채워져 여기서 읽으면 0이다.)
    /// </summary>
    public static float ExposureAt(Vector3 at)
    {
        float ev = 0f;
        float best = float.NegativeInfinity;
        foreach (UnityEngine.Rendering.Volume v in Object.FindObjectsByType<UnityEngine.Rendering.Volume>(FindObjectsSortMode.None))
        {
            if (v == null || !v.isActiveAndEnabled || v.weight <= 0f || v.sharedProfile == null) continue;
            UnityEngine.Rendering.Universal.ColorAdjustments ca;
            if (!v.sharedProfile.TryGet(out ca) || !ca.active || !ca.postExposure.overrideState) continue;
            if (!v.isGlobal)
            {
                bool inside = false;
                foreach (Collider c in v.GetComponents<Collider>())
                {
                    if (c.enabled && c.bounds.Contains(at)) inside = true;
                }

                if (!inside) continue;
            }

            if (v.priority < best) continue;
            best = v.priority;
            ev = ca.postExposure.value;
        }

        return ev;
    }

    private static readonly Dictionary<Material, Material> Cache = new Dictionary<Material, Material>();

    /// <summary><paramref name="root"/> 아래 렌더러를 원본 색 재질로. <paramref name="skinnedOnly"/>면 스킨드 메시(몹)만.</summary>
    public static int Apply(GameObject root, bool skinnedOnly = false)
    {
        if (root == null) return 0;
        Shader unlit = Shader.Find(UnlitShader);
        if (unlit == null) return 0;
        int changed = 0;
        foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
        {
            if (skinnedOnly && !(r is SkinnedMeshRenderer)) continue;
            if (!(r is SkinnedMeshRenderer) && !(r is MeshRenderer)) continue;   // 입자·선 등은 그대로
            Material[] mats = r.sharedMaterials;
            bool any = false;
            for (int i = 0; i < mats.Length; i++)
            {
                Material m = Convert(mats[i], unlit);
                if (m == null || m == mats[i]) continue;
                mats[i] = m;
                any = true;
            }

            if (!any) continue;
            r.sharedMaterials = mats;
            changed++;
        }

        return changed;
    }

    private static Material Convert(Material src, Shader unlit)
    {
        if (src == null || src.shader == unlit) return src;
        Material done;
        if (Cache.TryGetValue(src, out done) && done != null) return done;

        Texture map = src.HasProperty("_BaseMap") ? src.GetTexture("_BaseMap") : src.HasProperty("_MainTex") ? src.GetTexture("_MainTex") : null;
        Color color = src.HasProperty("_BaseColor") ? src.GetColor("_BaseColor") : src.HasProperty("_Color") ? src.GetColor("_Color") : Color.white;
        if (map == null && !src.HasProperty("_BaseColor") && !src.HasProperty("_Color")) return src;   // 알 수 없는 셰이더는 그대로

        Material m = new Material(unlit);
        m.name = src.name + " (원본 색)";
        m.SetTexture("_BaseMap", map);
        float gain = Gain();   // 94차: 노출만큼 덜어 냄
        m.SetColor("_BaseColor", new Color(color.r * gain, color.g * gain, color.b * gain, color.a));
        string st = src.HasProperty("_BaseMap") ? "_BaseMap" : "_MainTex";
        if (map != null && src.HasProperty(st))
        {
            m.SetTextureScale("_BaseMap", src.GetTextureScale(st));
            m.SetTextureOffset("_BaseMap", src.GetTextureOffset(st));
        }

        if (src.HasProperty("_Cull")) m.SetFloat("_Cull", src.GetFloat("_Cull"));
        bool clip = src.HasProperty("_AlphaClip") && src.GetFloat("_AlphaClip") > 0.5f;
        if (clip)
        {
            m.SetFloat("_AlphaClip", 1f);
            m.SetFloat("_Cutoff", src.HasProperty("_Cutoff") ? src.GetFloat("_Cutoff") : 0.5f);
            m.EnableKeyword("_ALPHATEST_ON");
        }

        bool transparent = src.HasProperty("_Surface") && src.GetFloat("_Surface") > 0.5f;
        if (transparent)
        {
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", src.HasProperty("_Blend") ? src.GetFloat("_Blend") : 0f);
            if (src.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", src.GetFloat("_SrcBlend"));
            if (src.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", src.GetFloat("_DstBlend"));
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        }

        m.renderQueue = src.renderQueue;
        Cache[src] = m;
        return m;
    }
}

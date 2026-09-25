// URP (Universal Render Pipeline) version of the original Shader Forge "Custom/MixerMaterial".
// The original was a Built-in RP forward shader, so it showed up magenta in this project.
//
// Kept identical to the original on purpose:
//   - shader name        "Custom/MixerMaterial"
//   - property names     _Diffuse1 / _Diffuse2 / _SpecGloss1 / _SpecGloss2 / _normal_Map / _mixAmount
//   - blend maths        albedo   = lerp(Diffuse1.rgb,   Diffuse2.rgb,   saturate(_mixAmount))
//                        specular = lerp(SpecGloss1.rgb, SpecGloss2.rgb, saturate(_mixAmount))
//                        smooth   = lerp(SpecGloss1.a,   SpecGloss2.a,   saturate(_mixAmount))
// Because the names match, the existing *_BloodMixer materials keep every texture they had.
//
// _mixAmount 0 = clean, 1 = bloody. Drive it at runtime with
//   renderer.material.SetFloat("_mixAmount", value);
//
// Lighting uses URP's own Lit forward pass (specular workflow), so shadows, fog,
// Forward+ additional lights and light cookies all behave like URP/Lit.

Shader "Custom/MixerMaterial"
{
    Properties
    {
        _Diffuse1("Diffuse 1 (clean)", 2D) = "white" {}
        _SpecGloss1("Spec/Gloss 1 (clean, gloss in A)", 2D) = "white" {}
        _Diffuse2("Diffuse 2 (bloody)", 2D) = "white" {}
        _SpecGloss2("Spec/Gloss 2 (bloody, gloss in A)", 2D) = "white" {}
        _normal_Map("Normal Map", 2D) = "bump" {}
        _BumpScale("Normal Scale", Float) = 1.0
        _mixAmount("Mix Amount (0 = clean, 1 = bloody)", Range(0.0, 1.0)) = 0.5

        // Required by the URP passes below. Not meant to be edited by hand.
        [HideInInspector] _BaseMap("Base Map", 2D) = "white" {}
        [HideInInspector] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        [HideInInspector] _Cutoff("Alpha Cutoff", Float) = 0.5
        [HideInInspector] _Surface("__surface", Float) = 0.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "Lit"
            "IgnoreProjector" = "True"
            "Queue" = "Geometry"
        }
        LOD 300

        HLSLINCLUDE
        #define _SPECULAR_SETUP 1

        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            float4 _BaseMap_TexelSize;
            float4 _Diffuse1_ST;
            half4 _BaseColor;
            half4 _SpecColor;
            half4 _EmissionColor;
            half _Cutoff;
            half _BumpScale;
            half _mixAmount;
            half _Surface;
        CBUFFER_END

        TEXTURE2D(_Diffuse1);    SAMPLER(sampler_Diffuse1);
        TEXTURE2D(_Diffuse2);
        TEXTURE2D(_SpecGloss1);
        TEXTURE2D(_SpecGloss2);
        TEXTURE2D(_normal_Map);  SAMPLER(sampler_normal_Map);

        // Parallax is not used, but URP's forward pass calls this.
        void ApplyPerPixelDisplacement(half3 viewDirTS, inout float2 uv)
        {
        }

        // Same signature URP's LitInput.hlsl provides; the forward pass calls it.
        inline void InitializeStandardLitSurfaceData(float2 uv, out SurfaceData outSurfaceData)
        {
            half blend = saturate(_mixAmount);

            half4 diffuse1 = SAMPLE_TEXTURE2D(_Diffuse1, sampler_Diffuse1, uv);
            half4 diffuse2 = SAMPLE_TEXTURE2D(_Diffuse2, sampler_Diffuse1, uv);
            half4 specGloss1 = SAMPLE_TEXTURE2D(_SpecGloss1, sampler_Diffuse1, uv);
            half4 specGloss2 = SAMPLE_TEXTURE2D(_SpecGloss2, sampler_Diffuse1, uv);

            outSurfaceData = (SurfaceData)0;
            outSurfaceData.albedo = lerp(diffuse1.rgb, diffuse2.rgb, blend);
            outSurfaceData.alpha = 1.0h;
            outSurfaceData.specular = lerp(specGloss1.rgb, specGloss2.rgb, blend);
            outSurfaceData.metallic = 0.0h;
            outSurfaceData.smoothness = lerp(specGloss1.a, specGloss2.a, blend);
            outSurfaceData.normalTS = SampleNormal(uv, TEXTURE2D_ARGS(_normal_Map, sampler_normal_Map), _BumpScale);
            outSurfaceData.occlusion = 1.0h;
            outSurfaceData.emission = 0.0h;
            outSurfaceData.clearCoatMask = 0.0h;
            outSurfaceData.clearCoatSmoothness = 0.0h;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 2.0

            // Tangent space is needed because we always sample a normal map.
            #define _NORMALMAP 1

            #pragma vertex LitPassVertex
            #pragma fragment LitPassFragment

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DYNAMICLIGHTMAP_ON
            #pragma multi_compile _ USE_LEGACY_LIGHTMAPS
            #pragma multi_compile_fragment _ LIGHTMAP_BICUBIC_SAMPLING
            #pragma multi_compile_fragment _ REFLECTION_PROBE_ROTATION
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitForwardPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma target 2.0

            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment

            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma target 2.0

            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment

            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 2.0

            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment

            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitDepthNormalsPass.hlsl"
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}

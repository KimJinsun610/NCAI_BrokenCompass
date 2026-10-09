// 상호작용 외곽선의 마스크(2026-10-03). 대상 메시를 색 없이 그려 스텐실 128 비트를 찍는다 — 외곽선(Transparent+50)보다 먼저(+49).
Shader "NightDuty/InteractionOutlineMask"
{
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+49" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "OutlineMask"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            // 67차: 앞뒤 모두 — 벽의 세계 지도처럼 화면 쪽이 뒷면인 판은 마스크가 비어 외곽선 색이 판 전체를 덮었다.
            Cull Off
            ZWrite Off
            ZTest LEqual
            ColorMask 0

            Stencil
            {
                Ref 128
                WriteMask 128
                Comp Always
                Pass Replace
            }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float4 Vert(Attributes input) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(input);
                return TransformObjectToHClip(input.positionOS.xyz);
            }

            half4 Frag() : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
}

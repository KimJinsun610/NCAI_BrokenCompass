// 상호작용 외곽선(2026-10-03). 뒤집은 껍질(inverted hull) — 뒷면을 법선 방향으로 화면 픽셀 단위만큼 밀고,
// InteractionOutlineMask가 먼저 찍은 스텐실 바깥에만 그려 실루엣 선만 남긴다.
// InteractionOutline.cs가 Graphics.RenderMesh로 플레이어 카메라에만 그린다. 씬의 머티리얼·렌더러는 건드리지 않는다.
Shader "NightDuty/InteractionOutline"
{
    Properties
    {
        _Color ("Color", Color) = (1, 0.93, 0.78, 1)
        _Width ("Width (px at 1080p)", Float) = 2.2
        _Alpha ("Alpha", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+50" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Cull Front
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha

            // 물체 자리(마스크 패스가 128 비트를 찍은 곳)에는 그리지 않는다 — 안쪽 선 없이 실루엣 바깥만 남는다.
            Stencil
            {
                Ref 128
                ReadMask 128
                Comp NotEqual
            }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Width;
                half _Alpha;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings Vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings o;
                float4 cs = TransformObjectToHClip(input.positionOS.xyz);
                float3 nWS = TransformObjectToWorldNormal(input.normalOS);
                float2 nCS = mul((float3x3)UNITY_MATRIX_VP, nWS).xy;
                float len = length(nCS);
                if (len > 1e-5)
                {
                    // 해상도와 무관하게 1080p 기준 픽셀 굵기를 유지한다(가로세로 비 보정).
                    float2 px = (nCS / len) * (_Width / 1080.0) * 2.0;
                    px.x *= _ScreenParams.y / _ScreenParams.x;
                    cs.xy += px * cs.w;
                }
                o.positionCS = cs;
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                return half4(_Color.rgb, _Color.a * _Alpha);
            }
            ENDHLSL
        }
    }
}

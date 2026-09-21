// 태블릿 액정용 언릿 셰이더.
// 조명을 받지 않고, _Glitch(0~1) 하나로 지직거림의 정도를 조절한다.
//
// 들어 있는 것: 주사선(scanline) · 잡음(static) · 가로 찢김(tear) · 깜빡임(flicker)
// _Glitch가 0이면 전부 꺼지고 단색 화면이 된다.

Shader "Custom/TabletScreen"
{
    Properties
    {
        _BaseColor("화면 바탕색", Color) = (0.04, 0.055, 0.07, 1)
        _GlowColor("지직거릴 때 섞이는 색", Color) = (0.35, 0.55, 0.6, 1)
        _Glitch("지직거림 정도", Range(0, 1)) = 0

        [Header(Scanline)]
        _ScanlineCount("주사선 개수", Float) = 140
        _ScanlineStrength("주사선 세기", Range(0, 1)) = 0.35
        _ScanlineScroll("주사선 흐르는 속도", Float) = 0.15

        [Header(Noise)]
        _NoiseStrength("잡음 세기", Range(0, 2)) = 0.6
        _NoiseScale("잡음 입자 크기", Float) = 260

        [Header(Tear)]
        _TearStrength("가로 찢김 세기", Range(0, 1)) = 0.5
        _TearBands("찢기는 띠 개수", Float) = 14
        _TearSpeed("찢김이 바뀌는 속도", Float) = 8

        [Header(Flicker)]
        _FlickerStrength("깜빡임 세기", Range(0, 1)) = 0.5
        _FlickerSpeed("깜빡임 속도", Float) = 11
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        LOD 100

        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _GlowColor;
                half _Glitch;
                float _ScanlineCount;
                half _ScanlineStrength;
                float _ScanlineScroll;
                half _NoiseStrength;
                float _NoiseScale;
                half _TearStrength;
                float _TearBands;
                float _TearSpeed;
                half _FlickerStrength;
                float _FlickerSpeed;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            // 값 하나에서 0~1 난수를 뽑는다.
            float Hash11(float p)
            {
                p = frac(p * 0.1031);
                p *= p + 33.33;
                p *= p + p;
                return frac(p);
            }

            float Hash21(float2 p)
            {
                float3 p3 = frac(p.xyx * float3(0.1031, 0.1030, 0.0973));
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float g = saturate(_Glitch);
                float2 uv = input.uv;
                float t = _Time.y;

                // --- 가로 찢김: 화면을 띠로 나누고 띠마다 좌우로 민다 ---
                float band = floor(uv.y * _TearBands);
                float bandRandom = Hash21(float2(band, floor(t * _TearSpeed)));
                // 대부분의 띠는 가만히 두고, 몇 개만 크게 민다. 그래야 '가끔 튀는' 느낌이 난다.
                float bandActive = step(1.0 - g * 0.5, bandRandom);
                uv.x += (bandRandom - 0.5) * 0.25 * _TearStrength * g * bandActive;

                half3 color = _BaseColor.rgb;

                // --- 주사선: 가로줄이 천천히 흐른다 ---
                float scan = sin((uv.y + t * _ScanlineScroll) * _ScanlineCount * 3.14159);
                color += _GlowColor.rgb * scan * _ScanlineStrength * 0.15 * (0.25 + g);

                // --- 잡음: 매 프레임 바뀌는 입자 ---
                float noise = Hash21(floor(uv * _NoiseScale) + floor(t * 40.0));
                color += (noise - 0.5) * _NoiseStrength * g;

                // --- 깜빡임: 순간적으로 밝아졌다 어두워진다 ---
                float flicker = Hash11(floor(t * _FlickerSpeed));
                color *= 1.0 + (flicker - 0.5) * _FlickerStrength * g;

                // 심할수록 화면이 살짝 달아오르게
                color += _GlowColor.rgb * 0.06 * g;

                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}

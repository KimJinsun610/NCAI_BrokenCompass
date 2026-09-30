// 경비실 CCTV 모니터 화면. 흑백 + 증폭 + 노이즈 + 주사선 + 굴러가는 띠 + 비네트.
// Resources 아래에 두어 빌드에 포함시킨다(CctvSystem이 Shader.Find로 찾는다).
// 조명을 받지 않는다 — 방이 어두워도 화면은 제 빛으로 보여야 한다.
Shader "NightDuty/CctvScreen"
{
    Properties
    {
        _MainTex ("Feed", 2D) = "black" {}
        _Gain ("Gain", Float) = 1.3
        _Gamma ("Gamma", Float) = 0.8
        _Tint ("Tint", Color) = (0.80, 0.95, 0.84, 1)
        _Noise ("Noise", Range(0, 1)) = 0.035
        _Scan ("Scanlines", Range(0, 1)) = 0.22
        _Vignette ("Vignette", Range(0, 2)) = 0.9
        _Static ("Static Burst", Range(0, 1)) = 0
        _Signal ("Signal", Range(0, 1)) = 1
        _Brightness ("Brightness", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _Gain;
                float _Gamma;
                float4 _Tint;
                float _Noise;
                float _Scan;
                float _Vignette;
                float _Static;
                float _Signal;
                float _Brightness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            float Hash(float2 p)
            {
                return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453);
            }

            half4 frag(Varyings i) : SV_Target
            {
                float t = _Time.y;
                float2 uv = i.uv;

                // 줄마다 조금씩 옆으로 흔들린다. 지직거릴 때는 크게.
                float row = floor(uv.y * 240.0);
                float wobble = (Hash(float2(row, floor(t * 24.0))) - 0.5) * 0.004 * (1.0 + _Static * 8.0);
                uv.x += wobble;

                // 브라운관처럼 가장자리가 살짝 휜다.
                float2 c = uv - 0.5;
                uv = 0.5 + c * (1.0 + 0.08 * dot(c, c));

                half3 feed = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).rgb;
                half lum = dot(feed, half3(0.299, 0.587, 0.114));
                lum = pow(saturate(lum * _Gain), _Gamma);

                float n = Hash(uv * float2(320.0, 240.0) + frac(t * 37.1) * 100.0);
                lum = lerp(lum, n, _Noise);

                // 천천히 내려가는 어두운 띠.
                float band = smoothstep(0.0, 0.08, abs(frac(uv.y * 0.5 - t * 0.07) - 0.5));
                lum *= lerp(0.82, 1.0, band);

                // 주사선.
                lum *= 1.0 - _Scan * (0.5 + 0.5 * sin(uv.y * 120.0 * 6.28318));

                // 채널 전환 지직거림과 신호 없음.
                lum = lerp(lum, n, saturate(_Static + (1.0 - _Signal)));

                // 비네트.
                lum *= saturate(1.0 - _Vignette * dot(c, c) * 1.6);

                // 휜 가장자리 바깥은 검게.
                if (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0)
                {
                    lum = 0.0;
                }

                return half4(lum * _Tint.rgb * _Brightness, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}

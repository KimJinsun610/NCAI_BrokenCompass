// 화면 울렁임. URP Full Screen Pass Renderer Feature의 Pass Material로 쓴다.
// 값은 전부 ScreenWobble.cs가 전역값으로 넣는다 — Properties 블록에 선언하면 머티리얼 값이 전역값을 가리므로 두지 않는다.
Shader "NightDuty/ScreenWobble"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZTest Always ZWrite Off Cull Off

        Pass
        {
            Name "ScreenWobble"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _WobbleWeight;     // 0~1
            float _WobbleTime;       // 게임 시간 누적(일시정지 시 멈춤)
            float _WobbleAmplitude;  // UV 흔들림 폭
            float _WobbleFrequency;  // 물결 촘촘함
            float _WobbleChroma;     // 색수차

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;

                if (_WobbleWeight <= 0.0001)
                    return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);

                float t = _WobbleTime;
                float2 fromCenter = uv - 0.5;

                // 가장자리일수록 세게. 조준선이 있는 중앙은 덜 흔들려 멀미가 줄어든다.
                float edge = lerp(0.35, 1.0, saturate(length(fromCenter) * 1.6));

                // 주파수가 다른 사인파 둘을 겹쳐 규칙적으로 보이지 않게 한다.
                float2 offset;
                offset.x = sin(uv.y * _WobbleFrequency + t * 2.1) * 0.6
                         + sin(uv.y * _WobbleFrequency * 2.3 - t * 1.3) * 0.4;
                offset.y = cos(uv.x * _WobbleFrequency * 0.8 + t * 1.7) * 0.6
                         + sin(uv.x * _WobbleFrequency * 1.9 + t * 0.9) * 0.4;
                offset *= _WobbleAmplitude * _WobbleWeight * edge;

                float2 wobbledUv = uv + offset;
                float2 chromaShift = fromCenter * _WobbleChroma * _WobbleWeight;

                half r = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, wobbledUv + chromaShift).r;
                half g = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, wobbledUv).g;
                half b = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, wobbledUv - chromaShift).b;
                return half4(r, g, b, 1);
            }
            ENDHLSL
        }
    }
}

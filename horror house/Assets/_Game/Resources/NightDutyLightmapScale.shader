// 방 어둡게(44차): 구운 라이트맵을 배수만큼 어둡게 복사할 때 쓰는 블릿. RoomDarkness가 Shader.Find로 찾는다(Resources라 빌드에 들어간다).
Shader "Hidden/NightDuty/LightmapScale"
{
    Properties
    {
        _MainTex ("Lightmap", 2D) = "black" {}
        _Scale ("Scale", Float) = 1
    }

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float _Scale;

            float4 frag (v2f_img i) : SV_Target
            {
                float4 c = tex2D(_MainTex, i.uv);
                c.rgb *= _Scale;
                return c;
            }
            ENDCG
        }
    }
}

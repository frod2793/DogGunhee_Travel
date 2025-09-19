Shader "Unlit/PixelLineTransparent"
{
    Properties
    {
        _Color ("Tint Color", Color) = (1, 1, 1, 0.5) // 색상과 투명도를 조절할 수 있는 프로퍼티
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        // 반투명 렌더링을 위한 설정
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        LOD 100

        Pass
        {
            // 알파 블렌딩, Z-버퍼 쓰기 비활성화, 양면 렌더링 설정
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color; // 인스펙터에서 설정한 색상 값을 받습니다.

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // 텍스처 색상과 인스펙터에서 설정한 색상을 곱하여 최종 색상을 결정합니다.
                // 최종 알파 값 = (텍스처의 알파 값) * (_Color의 알파 값)
                fixed4 col = tex2D(_MainTex, i.uv) * _Color;
                
                // 픽셀 아트처럼 가장자리를 날카롭게 만들고 싶다면 아래 주석을 해제하세요.
                // 알파 값이 0.01보다 작으면 픽셀을 그리지 않아 부드러운 반투명 대신 딱딱한 경계를 만듭니다.
                // clip(col.a - 0.01);

                return col;
            }
            ENDCG
        }
    }
    FallBack "Transparent/VertexLit"
}

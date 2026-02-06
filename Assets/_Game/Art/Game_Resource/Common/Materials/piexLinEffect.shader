Shader "Unlit/PixelLineEffect"
{
    Properties
    {
        _TintColor ("Tint Color", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        LOD 100

        Pass
        {
            // --- 픽셀 아트 및 라인 렌더러를 위한 설정 ---
            Cull Off        // 라인의 양면을 모두 렌더링합니다.
            ZWrite Off      // 반투명 객체가 다른 객체를 가리지 않도록 합니다.
            Blend SrcAlpha OneMinusSrcAlpha // 표준 알파 블렌딩을 사용합니다.

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            //#pragma multi_compile_fog

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR; // 정점 색상(LineRenderer의 그라데이션)을 받기 위해 추가
            };

            struct v2f
            {
                //UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR; // 프래그먼트 셰이더로 정점 색상을 전달하기 위해 추가
            };

            fixed4 _TintColor;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.color = v.color * _TintColor; // LineRenderer의 색상과 인스펙터의 Tint 색상을 곱합니다.
                //UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // 정점에서 전달받은 색상을 그대로 출력합니다.
                fixed4 col = i.color;
                // apply fog
                //UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
}
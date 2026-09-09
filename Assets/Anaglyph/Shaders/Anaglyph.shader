Shader "Custom/Anaglyph"
{
    Properties
    {
        _MainTex ("Left Eye", 2D) = "black" {}
        _RightTex ("Right Eye", 2D) = "black" {}
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Overlay"
        }

        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            CGPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"


            sampler2D _MainTex;
            sampler2D _RightTex;


            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };


            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };


            v2f vert(appdata v)
            {
                v2f o;

                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;

                return o;
            }


            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 left =
                    tex2D(_MainTex, i.uv);

                fixed4 right =
                    tex2D(_RightTex, i.uv);
                

                // Red channel from the left eye.
                // Green and blue channels from the right eye.
                return fixed4(
                    left.r,
                    right.g,
                    right.b,
                    1.0
                );
            }

            ENDCG
        }
    }
}